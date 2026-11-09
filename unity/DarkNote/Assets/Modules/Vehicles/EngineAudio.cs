using UnityEngine;

namespace Sim.Vehicles
{
    // Crossfades rpm-banded loops by rpm and throttle. Add next to VehicleController.
    [RequireComponent(typeof(VehicleController))]
    public class EngineAudio : MonoBehaviour
    {
        public bool interior;             // true when the camera is in the cabin
        public float interiorCutoff = 1800f;

        VehicleController car;
        AudioSource[] loops = new AudioSource[6];   // lowOn, midOn, highOn, lowOff, midOff, highOff
        AudioSource turbo, oneShot;
        AudioLowPassFilter lowpass;
        float lastThrottle;

        void Start()
        {
            car = GetComponent<VehicleController>();
            if (!car || !car.def) { enabled = false; return; }
            var s = car.def.sound;
            if (!s) { enabled = false; return; }

            AudioClip[] clips = { s.lowOn, s.midOn, s.highOn, s.lowOff, s.midOff, s.highOff };
            for (int i = 0; i < 6; i++) loops[i] = MakeSource(clips[i], true, s);
            turbo = MakeSource(s.turboSpool, true, s);
            oneShot = MakeSource(null, false, s);
            lowpass = gameObject.AddComponent<AudioLowPassFilter>();
            car.Box.Shifted += _ => { if (s.shift) oneShot.PlayOneShot(s.shift); };
        }

        AudioSource MakeSource(AudioClip clip, bool loop, EngineSoundProfile s)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = loop;
            src.spatialBlend = 1f;
            src.minDistance = 4f;
            src.maxDistance = 60f;
            src.outputAudioMixerGroup = s.output;
            src.volume = 0f;
            if (clip && loop) src.Play();
            return src;
        }

        static float Bell(float x, float center, float width) =>
            Mathf.Clamp01(1f - Mathf.Abs(x - center) / width);

        void Update()
        {
            var s = car.def.sound;
            float r = Mathf.Clamp01(car.RpmNormalized);
            float thr = car.input.throttle;

            float lo = Bell(r, 0.15f, 0.4f), mid = Bell(r, 0.5f, 0.35f), hi = Bell(r, 0.88f, 0.4f);
            float sum = Mathf.Max(0.001f, lo + mid + hi);
            float[] w = { lo / sum, mid / sum, hi / sum };
            float pitch = Mathf.Lerp(s.minPitch, s.maxPitch, r);

            for (int i = 0; i < 3; i++)
            {
                Set(loops[i], w[i] * thr, pitch);
                Set(loops[i + 3], w[i] * (1f - thr), pitch);
            }
            Set(turbo, car.Engine.Boost * 0.6f, Mathf.Lerp(0.8f, 1.8f, car.Engine.Boost));

            if (lastThrottle > 0.7f && thr < 0.2f && car.Engine.Boost > 0.5f && s.blowOff)
                oneShot.PlayOneShot(s.blowOff);
            lastThrottle = thr;

            lowpass.cutoffFrequency = interior ? interiorCutoff : 22000f;
        }

        static void Set(AudioSource src, float volume, float pitch)
        {
            if (!src.clip) return;
            src.volume = Mathf.Clamp01(volume);
            src.pitch = pitch;
        }
    }
}
