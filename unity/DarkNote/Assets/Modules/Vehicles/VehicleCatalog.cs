using System.Linq;
using UnityEngine;

namespace Sim.Vehicles
{
    // Simple list of every car, filled by the CSV importer. Good enough for prototypes.
    // For shipping, load definitions by Addressables label through ContentRegistry instead.
    [CreateAssetMenu(menuName = "Sim/Vehicle Catalog")]
    public class VehicleCatalog : ScriptableObject
    {
        public VehicleDefinition[] vehicles = new VehicleDefinition[0];

        public string[] Brands() => vehicles.Where(v => v).Select(v => v.brand).Distinct().OrderBy(b => b).ToArray();
        public VehicleDefinition[] OfBrand(string brand) =>
            vehicles.Where(v => v && v.brand == brand).OrderBy(v => v.model).ToArray();
        public VehicleDefinition Find(string id) => vehicles.FirstOrDefault(v => v && v.id == id);
    }
}
