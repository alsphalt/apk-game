using System.Collections.Generic;
using UnityEngine;

namespace Sim.Vehicles
{
    // Stat-only starter parts, created at runtime so the garage works with no assets.
    // Replace with PartDefinition assets (with prefabs) as art arrives.
    public static class DefaultParts
    {
        static PartDefinition P(string id, string name, PartSlot slot, int price, StatModifiers m)
        {
            var p = ScriptableObject.CreateInstance<PartDefinition>();
            p.id = id; p.displayName = name; p.slot = slot; p.price = price; p.modifiers = m;
            return p;
        }

        static StatModifiers M(float power = 1f, float grip = 1f, float brake = 1f, float susp = 1f, float drag = 1f, float mass = 0f)
        {
            var m = StatModifiers.Identity;
            m.powerMult = power; m.gripMult = grip; m.brakeMult = brake;
            m.suspensionMult = susp; m.dragMult = drag; m.massDelta = mass;
            return m;
        }

        public static List<PartDefinition> Create() => new List<PartDefinition>
        {
            P("eng_stage1", "Engine Stage 1 (+8% power)", PartSlot.Engine, 2500, M(power: 1.08f)),
            P("eng_stage2", "Engine Stage 2 (+18% power)", PartSlot.Turbo, 6000, M(power: 1.18f)),
            P("tyres_sport", "Sport Tyres (+10% grip)", PartSlot.Tyres, 1800, M(grip: 1.10f)),
            P("susp_sport", "Sport Suspension", PartSlot.Suspension, 2200, M(susp: 1.25f, grip: 1.03f)),
            P("brakes_big", "Big Brakes (+25%)", PartSlot.Brakes, 2800, M(brake: 1.25f, mass: 6f)),
            P("weight_red", "Weight Reduction (-80 kg)", PartSlot.WeightReduction, 3000, M(mass: -80f)),
            P("aero_kit", "Aero Kit (less drag, more grip)", PartSlot.Spoiler, 2000, M(drag: 0.95f, grip: 1.04f, mass: 8f)),
        };
    }
}
