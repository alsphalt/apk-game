using System;

namespace Sim.Vehicles
{
    // Pure C# engine sound synthesiser (no Unity types, so it can be unit tested).
    // Each cylinder fires an exhaust pulse at its place in the 720 degree cycle. Pulse shape,
    // firing order and gains depend on the layout, so a V8, an I5 and a flat-4 sound different.
    // It is an approximation, not a recording. For hero cars, use EngineAudio with real loops.
    public sealed class EngineSynth
    {
        // Set from the main thread each frame.
        public volatile float Rpm = 800f;
        public volatile float Throttle;
        public volatile float Boost;
        public volatile bool Limiter;
        public float RedlineRpm = 7000f;

        readonly float[] fire;       // firing positions, fraction of the 720 degree cycle
        readonly float[] gain;
        readonly float[] table;      // one exhaust pulse
        readonly int sampleRate;
        readonly bool turbo, supercharged;
        double crank;                // 0..1 over 720 degrees
        float rpmSmooth = 800f, lp1, lp2, hp, noiseLp;
        uint rng = 2463534242u;
        float turboPhase, scPhase;
        int limiterClock;

        public EngineSynth(EngineLayout layout, bool turbo, bool supercharged, int sampleRate)
        {
            this.turbo = turbo; this.supercharged = supercharged; this.sampleRate = sampleRate;
            Firing(layout, out fire, out gain);
            table = BuildPulse(layout, sampleRate);
        }

        static void Firing(EngineLayout l, out float[] f, out float[] g)
        {
            switch (l)
            {
                case EngineLayout.V6: // 90 degree V6 with uneven 150/90 firing
                    f = new[] { 0f, 150f, 240f, 390f, 480f, 630f };
                    for (int i = 0; i < 6; i++) f[i] /= 720f;
                    g = new[] { 1f, 0.9f, 1f, 0.9f, 1f, 0.9f };
                    return;
                case EngineLayout.V8: // cross-plane: even firing, uneven exhaust energy gives the burble
                    f = Even(8); g = new[] { 1f, 0.8f, 1.1f, 0.75f, 1.15f, 0.8f, 1.05f, 0.85f }; return;
                case EngineLayout.F4:
                    f = Even(4); g = new[] { 1f, 0.7f, 1f, 0.7f }; return;
                case EngineLayout.I3: f = Even(3); break;
                case EngineLayout.I4: f = Even(4); break;
                case EngineLayout.I5: f = Even(5); break;
                case EngineLayout.I6: f = Even(6); break;
                case EngineLayout.V10: f = Even(10); break;
                case EngineLayout.V12: f = Even(12); break;
                default: f = Even(6); break;
            }
            g = new float[f.Length];
            for (int i = 0; i < g.Length; i++) g[i] = 1f;
        }

        static float[] Even(int n)
        {
            var f = new float[n];
            for (int i = 0; i < n; i++) f[i] = i / (float)n;
            return f;
        }

        static float[] BuildPulse(EngineLayout l, int sr)
        {
            // Lower resonance for bigger cylinders.
            float f1;
            switch (l)
            {
                case EngineLayout.I3: f1 = 150f; break;
                case EngineLayout.I4: f1 = 130f; break;
                case EngineLayout.I5: f1 = 120f; break;
                case EngineLayout.I6: f1 = 110f; break;
                case EngineLayout.V6: f1 = 115f; break;
                case EngineLayout.V8: f1 = 85f; break;
                case EngineLayout.V10: f1 = 100f; break;
                case EngineLayout.V12: f1 = 95f; break;
                case EngineLayout.F4: f1 = 120f; break;
                default: f1 = 105f; break;
            }
            int n = (int)(0.045f * sr);
            var t = new float[n];
            float peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                float s = i / (float)sr;
                float body = (float)Math.Exp(-90f * s) *
                             ((float)Math.Sin(2 * Math.PI * f1 * s) + 0.5f * (float)Math.Sin(2 * Math.PI * f1 * 3.1f * s + 0.6f));
                float click = 0.4f * (float)Math.Exp(-400f * s);
                t[i] = body + click;
                peak = Math.Max(peak, Math.Abs(t[i]));
            }
            for (int i = 0; i < n; i++) t[i] /= peak;
            return t;
        }

        float Noise()
        {
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            return (rng / (float)uint.MaxValue) * 2f - 1f;
        }

        // Fills `data` (interleaved). Call from OnAudioFilterRead.
        public void Render(float[] data, int channels)
        {
            float targetRpm = Rpm, thr = Throttle, boost = Boost;
            bool lim = Limiter;
            int frames = data.Length / channels;
            float redline = Math.Max(1000f, RedlineRpm);
            float cyc = (float)table.Length / sampleRate;

            for (int n = 0; n < frames; n++)
            {
                rpmSmooth += (targetRpm - rpmSmooth) * 0.002f;
                float rpm = rpmSmooth;
                float rn = Math.Min(1.2f, rpm / redline);
                double cps = rpm / 120.0;                 // 720 degree cycles per second
                crank += cps / sampleRate;
                if (crank >= 1.0) crank -= 1.0;

                float s = 0f;
                for (int c = 0; c < fire.Length; c++)
                {
                    float d = (float)(crank - fire[c]);
                    if (d < 0f) d += 1f;
                    float tSec = d / (float)cps;
                    if (tSec >= cyc) continue;
                    int idx = Math.Min(table.Length - 1, (int)(tSec * sampleRate));
                    s += table[idx] * gain[c];
                }
                s *= 0.5f / (float)Math.Sqrt(fire.Length);

                // Load: closed throttle is quieter and duller than open throttle.
                float load = 0.3f + 0.7f * thr;
                s *= load;

                // One-pole low-pass whose cutoff opens with throttle and rpm.
                float fc = (500f + 4500f * thr) * (0.6f + 0.8f * rn);
                float a = 1f - (float)Math.Exp(-2.0 * Math.PI * fc / sampleRate);
                lp1 += (s - lp1) * a;
                lp2 += (lp1 - lp2) * a;
                float o = lp2;

                // Intake and mechanical noise.
                noiseLp += (Noise() - noiseLp) * 0.15f;
                o += noiseLp * 0.05f * thr * rn * rn;

                // Forced induction.
                if (turbo && boost > 0.01f)
                {
                    turboPhase += (3500f + boost * 7000f) / sampleRate * 6.2831853f;
                    if (turboPhase > 6.2831853f) turboPhase -= 6.2831853f;
                    o += (float)Math.Sin(turboPhase) * 0.025f * boost;
                }
                if (supercharged)
                {
                    scPhase += (rpm / 60f * 8f) / sampleRate * 6.2831853f;
                    if (scPhase > 6.2831853f) scPhase -= 6.2831853f;
                    o += (float)Math.Sin(scPhase) * 0.03f * (0.3f + 0.7f * thr);
                }

                // Fuel cut at the limiter: chops the sound at about 20 Hz.
                limiterClock++;
                if (lim && (limiterClock / (sampleRate / 40)) % 2 == 0) o = 0f;

                // Soft clip and remove DC.
                o = o / (1f + Math.Abs(o));
                hp += (o - hp) * 0.001f;
                o -= hp;
                o *= 3.5f;

                for (int ch = 0; ch < channels; ch++) data[n * channels + ch] = o;
            }
        }
    }
}
