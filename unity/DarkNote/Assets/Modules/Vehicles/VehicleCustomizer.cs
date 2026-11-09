using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Sim.Vehicles
{
    [Serializable]
    public class BuildSave
    {
        public string vehicleId;
        public string paintHex = "#c8201a";
        public float metallic = 0.6f, smoothness = 0.85f;
        public List<string> partIds = new List<string>();
    }

    // Add to the car prefab. Create child objects named Socket_<PartSlot> (for example
    // Socket_Spoiler) where visual parts attach.
    [RequireComponent(typeof(VehicleController))]
    public class VehicleCustomizer : MonoBehaviour
    {
        public BuildSave build = new BuildSave();

        VehicleController car;
        PaintTarget[] paints;
        readonly Dictionary<PartSlot, PartDefinition> installed = new Dictionary<PartSlot, PartDefinition>();
        readonly Dictionary<PartSlot, GameObject> visuals = new Dictionary<PartSlot, GameObject>();

        void Awake()
        {
            car = GetComponent<VehicleController>();
            paints = GetComponentsInChildren<PaintTarget>(true);
        }

        public void SetPaint(Color color, float metallic, float smoothness)
        {
            build.paintHex = "#" + ColorUtility.ToHtmlStringRGB(color);
            build.metallic = metallic;
            build.smoothness = smoothness;
            foreach (var p in paints) p.Apply(color, metallic, smoothness);
        }

        public async void Install(PartDefinition part)
        {
            if (part.fitsVehicleIds != null && part.fitsVehicleIds.Length > 0 &&
                !part.fitsVehicleIds.Contains(car.def.id)) return;

            installed[part.slot] = part;

            if (visuals.TryGetValue(part.slot, out var old) && old) Addressables.ReleaseInstance(old);
            visuals.Remove(part.slot);

            if (part.prefab != null && part.prefab.RuntimeKeyIsValid())
            {
                var socket = transform.Find("Socket_" + part.slot);
                if (socket)
                    visuals[part.slot] = await Addressables.InstantiateAsync(part.prefab, socket).Task;
            }
            Recalculate();
        }

        public bool Has(PartDefinition part) =>
            installed.TryGetValue(part.slot, out var p) && p == part;

        public void Remove(PartSlot slot)
        {
            installed.Remove(slot);
            if (visuals.TryGetValue(slot, out var v) && v) Addressables.ReleaseInstance(v);
            visuals.Remove(slot);
            Recalculate();
        }

        void Recalculate()
        {
            var total = StatModifiers.Identity;
            foreach (var p in installed.Values) total = total.Combine(p.modifiers);
            car.mods = total;
            car.Apply();
            build.partIds = installed.Values.Select(p => p.id).ToList();
        }

        public string ToJson() => JsonUtility.ToJson(build);

        // Pass the registry lookup so saves keep working when new parts are added.
        public void LoadJson(string json, Func<string, PartDefinition> findPart)
        {
            build = JsonUtility.FromJson<BuildSave>(json);
            if (ColorUtility.TryParseHtmlString(build.paintHex, out var c))
                SetPaint(c, build.metallic, build.smoothness);
            foreach (var id in build.partIds.ToList())
            {
                var part = findPart(id);
                if (part) Install(part);
            }
        }
    }
}
