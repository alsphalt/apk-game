using UnityEngine;

namespace Sim.Vehicles
{
    // Simple engine: free-revs when the clutch is open, follows the wheels when locked.
    // Turbo builds boost over time, which gives turbo cars their delayed punch.
    public class EngineModel
    {
        readonly VehicleDefinition d;
        float limiterTimer;

        public float Rpm { get; private set; }
        public float Boost { get; private set; }
        public bool OnLimiter => limiterTimer > 0f;

        public EngineModel(VehicleDefinition def)
        {
            d = def;
            Rpm = def.idleRpm;
        }

        public float Step(float throttle, float drivenRpm, bool clutchLocked, float dt, float powerMult)
        {
            if (clutchLocked)
                Rpm = Mathf.Lerp(Rpm, Mathf.Max(drivenRpm, d.idleRpm), 1f - Mathf.Exp(-20f * dt));
            else
            {
                float target = Mathf.Lerp(d.idleRpm, d.redlineRpm, throttle);
                Rpm = Mathf.Lerp(Rpm, target, 1f - Mathf.Exp(-6f * dt));
            }

            if (d.turbo)
            {
                float want = throttle * Mathf.InverseLerp(d.idleRpm * 1.5f, d.redlineRpm * 0.5f, Rpm);
                Boost = Mathf.MoveTowards(Boost, want, dt / Mathf.Max(0.05f, d.turboLag));
            }
            else Boost = 0f;

            if (Rpm >= d.redlineRpm) limiterTimer = 0.08f;
            limiterTimer = Mathf.Max(0f, limiterTimer - dt);
            Rpm = Mathf.Min(Rpm, d.redlineRpm * 1.02f);

            if (limiterTimer > 0f) return 0f;

            float torque = d.torqueNm.Evaluate(Rpm) * throttle * powerMult;
            if (d.turbo) torque *= Mathf.Lerp(0.6f, 1f, Boost);
            torque -= (1f - throttle) * d.engineBrakingNm * (Rpm / d.redlineRpm);
            return torque;
        }
    }
}
