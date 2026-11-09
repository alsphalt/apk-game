using System;

namespace Sim.Vehicles
{
    // Applied on top of a VehicleDefinition by customization parts.
    [Serializable]
    public struct StatModifiers
    {
        public float powerMult;
        public float gripMult;
        public float brakeMult;
        public float suspensionMult;
        public float dragMult;
        public float massDelta;

        public static StatModifiers Identity => new StatModifiers
        {
            powerMult = 1f, gripMult = 1f, brakeMult = 1f,
            suspensionMult = 1f, dragMult = 1f, massDelta = 0f
        };

        public StatModifiers Combine(StatModifiers o) => new StatModifiers
        {
            powerMult = powerMult * o.powerMult,
            gripMult = gripMult * o.gripMult,
            brakeMult = brakeMult * o.brakeMult,
            suspensionMult = suspensionMult * o.suspensionMult,
            dragMult = dragMult * o.dragMult,
            massDelta = massDelta + o.massDelta
        };
    }
}
