using UnityEngine;

namespace Sim.Vehicles
{
    // Automatic gearbox. Gear: -1 reverse, 0 neutral, 1..N forward.
    public class Gearbox
    {
        float shiftTimer;

        public int Gear { get; private set; } = 1;
        public bool Shifting => shiftTimer > 0f;
        public event System.Action<int> Shifted;

        public float Ratio(VehicleDefinition d)
        {
            if (Gear == 0 || Shifting) return 0f;
            return Gear < 0 ? -d.reverseRatio : d.gearRatios[Gear - 1];
        }

        public void Step(VehicleDefinition d, float rpm, float throttle, float forwardSpeed, bool reverse, float dt)
        {
            shiftTimer = Mathf.Max(0f, shiftTimer - dt);
            if (Shifting) return;

            if (reverse && forwardSpeed < 1.5f && Gear != -1) { Set(-1, d); return; }
            if (!reverse && Gear <= 0 && forwardSpeed > -1.5f) { Set(1, d); return; }
            if (Gear < 1) return;

            int top = d.gearRatios.Length;
            float up = d.redlineRpm * Mathf.Lerp(0.55f, 0.93f, throttle);
            float down = d.redlineRpm * 0.3f;

            if (rpm > up && Gear < top) Set(Gear + 1, d);
            else if (Gear > 1 && (rpm < down || (throttle > 0.9f && rpm < d.redlineRpm * 0.5f))) Set(Gear - 1, d);
        }

        void Set(int g, VehicleDefinition d)
        {
            Gear = g;
            shiftTimer = d.shiftTime;
            Shifted?.Invoke(g);
        }
    }
}
