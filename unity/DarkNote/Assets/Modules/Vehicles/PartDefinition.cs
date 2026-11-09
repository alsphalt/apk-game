using Sim.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Sim.Vehicles
{
    public enum PartSlot
    {
        Wheels, FrontBumper, RearBumper, SideSkirts, Spoiler, Hood, Exhaust,
        Engine, Turbo, Tyres, Suspension, Brakes, WeightReduction
    }

    // A visual and/or performance upgrade. Add new parts as assets only.
    [CreateAssetMenu(menuName = "Sim/Part Definition")]
    public class PartDefinition : ScriptableObject, IContent
    {
        public string id;
        public string displayName;
        public PartSlot slot;
        public int price;
        public AssetReferenceGameObject prefab;   // optional visual, attaches to Socket_<slot>
        public StatModifiers modifiers = StatModifiers.Identity;
        public string[] fitsVehicleIds;           // empty = fits every car

        public string Id => id;
    }
}
