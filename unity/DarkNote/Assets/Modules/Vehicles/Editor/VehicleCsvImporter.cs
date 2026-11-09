#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sim.Vehicles.Editor
{
    // Menu: Sim > Import Vehicles CSV
    // One row per car. Re-importing updates existing assets, so physics tuning lives in the CSV.
    // Suspension, brakes and centre of mass are derived from mass, wheelbase and weight split,
    // so a heavy SUV and a light coupe feel different without hand tuning each asset.
    public static class VehicleCsvImporter
    {
        static readonly Dictionary<string, string> GearPresets = new Dictionary<string, string>
        {
            { "ZF8HP", "4.714|3.143|2.106|1.667|1.285|1.0|0.839|0.667" },
            { "ZF8HP_SUPRA", "5.25|3.36|2.17|1.72|1.32|1.0|0.82|0.64" },
            { "AT10", "4.696|2.985|2.146|1.769|1.520|1.275|1.0|0.854|0.689|0.636" },
            { "MT82", "3.66|2.43|1.69|1.32|1.0|0.65" },
            { "DCT7", "3.9|2.7|1.95|1.45|1.08|0.85|0.69" },
            { "MAN5", "3.5|2.0|1.4|1.0|0.8" },
            { "MAN6", "3.6|2.2|1.5|1.1|0.9|0.75" },
            { "MAN6_M", "4.23|2.53|1.67|1.23|1.0|0.83" },
        };

        [MenuItem("Sim/Import Vehicles CSV")]
        public static void ImportMenu()
        {
            string path = EditorUtility.OpenFilePanel("Vehicles CSV", Application.dataPath, "csv");
            if (!string.IsNullOrEmpty(path)) Import(path);
        }

        // Menu: Sim > Import Default Roster. Imports Assets/Content/vehicles.csv if present.
        [MenuItem("Sim/Import Default Roster (Assets/Content/vehicles.csv)")]
        public static void ImportDefault()
        {
            string path = Path.Combine(Application.dataPath, "Content/vehicles.csv");
            if (!File.Exists(path)) { Debug.LogError("Not found: " + path); return; }
            Import(path);
        }

        public static void Import(string path)
        {
            var lines = File.ReadAllLines(path).Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#")).ToArray();
            var head = lines[0].Split(',').Select(h => h.Trim()).ToArray();
            var created = new List<VehicleDefinition>();

            foreach (var line in lines.Skip(1))
            {
                var c = line.Split(',');
                if (c.Length != head.Length) { Debug.LogError($"Skipped row (expected {head.Length} columns, got {c.Length}): {line}"); continue; }

                string F(string name) { int i = Array.IndexOf(head, name); return i < 0 ? "" : c[i].Trim(); }
                float N(string name, float fallback) =>
                    float.TryParse(F(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

                string id = F("id"), brand = F("brand");
                string dir = $"Assets/Content/Vehicles/{brand}";
                Directory.CreateDirectory(Path.Combine(Application.dataPath, "../" + dir));
                string asset = $"{dir}/{id}.asset";

                var def = AssetDatabase.LoadAssetAtPath<VehicleDefinition>(asset);
                bool isNew = def == null;
                if (isNew) def = ScriptableObject.CreateInstance<VehicleDefinition>();

                Fill(def, F, N);

                if (isNew) AssetDatabase.CreateAsset(def, asset);
                else EditorUtility.SetDirty(def);
                created.Add(def);
            }

            // Catalog used by the prototype bootstrap and the garage.
            const string catPath = "Assets/Content/VehicleCatalog.asset";
            var cat = AssetDatabase.LoadAssetAtPath<VehicleCatalog>(catPath);
            if (cat == null) { cat = ScriptableObject.CreateInstance<VehicleCatalog>(); AssetDatabase.CreateAsset(cat, catPath); }
            cat.vehicles = created.ToArray();
            EditorUtility.SetDirty(cat);

            AssetDatabase.SaveAssets();
            Debug.Log($"Imported {created.Count} vehicles into {catPath}. For shipping: assign real prefabs and sound profiles, then label them 'vehicle' in Addressables.");
        }

        static void Fill(VehicleDefinition def, Func<string, string> F, Func<string, float, float> N)
        {
            def.id = F("id"); def.brand = F("brand"); def.model = F("model");
            def.year = (int)N("year", 2000);
            def.body = (BodyType)Enum.Parse(typeof(BodyType), F("body"), true);
            def.drive = (DriveType)Enum.Parse(typeof(DriveType), F("drive"), true);
            def.layout = (EngineLayout)Enum.Parse(typeof(EngineLayout), F("layout"), true);

            def.massKg = N("mass", 1500f);
            def.redlineRpm = N("redline", 7000f);
            int aspiration = (int)N("turbo", 0);
            def.turbo = aspiration == 1 || aspiration == 2;
            def.turboLag = aspiration == 2 ? 0.8f : 0.35f;
            def.supercharged = aspiration == 3;
            def.idleRpm = def.layout == EngineLayout.I3 || def.layout == EngineLayout.F4 ? 800f : 850f;

            string g = F("gears");
            if (GearPresets.TryGetValue(g, out var preset)) g = preset;
            def.gearRatios = g.Split('|').Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray();
            def.finalDrive = N("final", 3.5f);
            def.reverseRatio = N("reverse", 3.4f);
            def.dragCoefficient = N("cd", 0.32f);
            def.frontalAreaM2 = N("area", 2.2f);
            def.wheelRadius = N("wheelR", 0.34f);
            def.downforce = N("downforce", 0.2f);
            def.speedLimiterKmh = N("vmax", 0f);

            // Dual-clutch and sporty cars shift faster.
            def.shiftTime = g == GearPresets["DCT7"] ? 0.12f : (g.Split('|').Length >= 8 ? 0.2f : 0.28f);
            if (def.drive == DriveType.AWD) def.awdFrontSplit = Mathf.Clamp01(N("awdFront", 0.4f));

            // Chassis: derive suspension and brakes from mass and geometry.
            float grip = N("grip", 1.9f), front = N("front", 0.52f), wb = N("wb", 2.7f), sport = N("sport", 1f);
            bool tall = def.body == BodyType.SUV || def.body == BodyType.Truck;
            def.wheelbase = wb;
            def.gripFront = grip;
            def.gripRear = grip * (def.drive == DriveType.RWD ? 1.05f : 1f);
            def.travel = tall ? 0.30f : 0.20f;
            float perWheel = def.massKg * 0.25f;
            def.spring = perWheel * 9.81f / (def.travel * 0.5f) * sport;
            float zeta = 0.45f * Mathf.Sqrt(sport);
            def.damper = 2f * zeta * Mathf.Sqrt(def.spring * perWheel);
            def.antiRoll = def.spring * (tall ? 0.18f : 0.22f);
            def.centerOfMassOffset = new Vector3(0f, tall ? -0.15f : -0.30f, (front - 0.5f) * wb);
            def.brakeTorque = def.massKg * 3.4f * Mathf.Clamp(sport, 1f, 1.15f);
            def.brakeBiasFront = Mathf.Clamp(front + 0.08f, 0.5f, 0.75f);
            def.wheelMass = tall ? 28f : 20f;
            def.maxSteerAngle = tall ? 30f : 33f;
            def.highSpeedSteerFactor = def.body == BodyType.Super || def.body == BodyType.Sports ? 0.30f : 0.38f;
            def.tractionControl = true; def.abs = true;
            def.engineBrakingNm = 40f + 0.05f * N("nm", 400f);

            def.torqueNm = BuildCurve(N("hp", 200f), def.redlineRpm, def.idleRpm, aspiration);

            // Sanity check: the CSV torque figure should be near what the curve produces.
            float csvNm = N("nm", 0f);
            float peak = 0f;
            for (float r = def.idleRpm; r <= def.redlineRpm; r += 50f) peak = Mathf.Max(peak, def.torqueNm.Evaluate(r));
            if (csvNm > 0f && Mathf.Abs(peak - csvNm) / csvNm > 0.2f)
                Debug.LogWarning($"{def.id}: curve peak torque {peak:F0} Nm differs from CSV {csvNm:F0} Nm. Check hp, nm and redline.");
        }

        // Typical torque shape scaled so peak power matches the CSV horsepower.
        static AnimationCurve BuildCurve(float hp, float redline, float idle, int aspiration)
        {
            float[][] shape;
            if (aspiration == 1)      shape = new[] { new[] { 0f, .55f }, new[] { .22f, 1f }, new[] { .5f, 1f }, new[] { .72f, .97f }, new[] { .9f, .85f }, new[] { 1f, .78f } };
            else if (aspiration == 2) shape = new[] { new[] { 0f, .35f }, new[] { .3f, .6f }, new[] { .45f, 1f }, new[] { .7f, .98f }, new[] { .9f, .85f }, new[] { 1f, .75f } };
            else if (aspiration == 3) shape = new[] { new[] { 0f, .8f }, new[] { .2f, .95f }, new[] { .5f, 1f }, new[] { .75f, .96f }, new[] { .9f, .88f }, new[] { 1f, .8f } };
            else                      shape = new[] { new[] { 0f, .6f }, new[] { .35f, .85f }, new[] { .6f, 1f }, new[] { .8f, .97f }, new[] { .95f, .88f }, new[] { 1f, .8f } };

            float maxKw = 0f;
            foreach (var p in shape)
            {
                float rpm = Mathf.Lerp(idle, redline, p[0]);
                maxKw = Mathf.Max(maxKw, p[1] * rpm / 9549f);
            }
            float peakNm = hp * 0.7457f / maxKw;

            var curve = new AnimationCurve();
            foreach (var p in shape)
                curve.AddKey(Mathf.Lerp(idle, redline, p[0]), p[1] * peakNm);
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0.3f);
            return curve;
        }
    }
}
#endif
