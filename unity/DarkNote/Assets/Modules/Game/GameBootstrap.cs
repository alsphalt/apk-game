using System.Linq;
using Sim.Controls;
using Sim.Mobile;
using Sim.Traffic;
using Sim.Vehicles;
using Sim.World;
using UnityEngine;

namespace Sim.Game
{
    // Drop this on an empty GameObject in an empty scene and press Play. It builds the city,
    // spawns a placeholder car from the catalog, adds controls, HUD, camera, traffic, the garage
    // and adaptive quality. Real projects replace pieces (prefabs, Addressables) but keep the wiring.
    public class GameBootstrap : MonoBehaviour
    {
        public VehicleCatalog catalog;
        public CityGeneratorSettings citySettings;      // optional, defaults are used if empty
        public string startVehicleId = "bmw_m3_g80";
        public bool leftHandTraffic = false;
        public int trafficCars = 20;

        CityGeneratorSettings settings;
        ProceduralCityStreamer streamer;
        TrafficManager traffic;
        CarCameraRig rig;
        DriverInput driver;
        Hud hud;
        GarageUI garage;
        GameObject car;
        VehicleDefinition def;
        System.Collections.Generic.List<PartDefinition> parts;

        void Start()
        {
            if (!catalog || catalog.vehicles == null || catalog.vehicles.Length == 0)
            {
                Debug.LogError("Assign a VehicleCatalog. Run Sim > Import Default Roster first.");
                return;
            }
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.LandscapeLeft;

            settings = citySettings ? Instantiate(citySettings) : ScriptableObject.CreateInstance<CityGeneratorSettings>();
            settings.leftHandTraffic = leftHandTraffic;
            parts = DefaultParts.Create();

            SetupEnvironment();
            var cam = SetupCamera();

            var cityGo = new GameObject("City");
            cityGo.SetActive(false);                       // so Awake runs only after settings are assigned
            streamer = cityGo.AddComponent<ProceduralCityStreamer>();
            streamer.settings = settings;
            cityGo.SetActive(true);
            streamer.Generator.SpawnPose(out var spawn, out _);
            streamer.Prewarm(spawn, 1);

            // Controls and UI
            var canvas = UiKit.CreateCanvas("GameUI", 10);
            var touch = new GameObject("TouchControls").AddComponent<TouchControls>();
            touch.Build(canvas);
            driver = new GameObject("Driver").AddComponent<DriverInput>();
            driver.touch = touch;
            driver.CameraToggled += () => rig.Cycle();
            hud = new GameObject("HUD").AddComponent<Hud>();
            hud.Build(canvas);

            rig = new GameObject("CameraRig").AddComponent<CarCameraRig>();
            rig.cam = cam;

            var startDef = catalog.Find(SaveSystem.LoadLastVehicle() ?? startVehicleId) ?? catalog.Find(startVehicleId) ?? catalog.vehicles[0];

            garage = new GameObject("Garage").AddComponent<GarageUI>();
            garage.catalog = catalog;
            garage.parts = parts;
            garage.CarChosen += d => SpawnCar(d, false);
            garage.Build(canvas, startDef);

            SetupTraffic();
            SpawnCar(startDef, true);

            var aq = gameObject.AddComponent<AdaptiveQuality>();
            aq.OnTrafficDensity += dens => { if (traffic) traffic.density = dens; };
        }

        void SetupEnvironment()
        {
            if (!FindFirstObjectByType<Light>())
            {
                var l = new GameObject("Sun").AddComponent<Light>();
                l.type = LightType.Directional;
                l.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                l.intensity = 1.1f;
                l.shadows = LightShadows.Soft;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.68f);
            RenderSettings.fog = true;                     // also hides chunk pop-in
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 160f;
            RenderSettings.fogEndDistance = 420f;
            RenderSettings.fogColor = new Color(0.72f, 0.80f, 0.90f);
        }

        Camera SetupCamera()
        {
            var cam = Camera.main;
            if (!cam)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.72f, 0.80f, 0.90f);
            cam.farClipPlane = 500f;
            cam.nearClipPlane = 0.3f;
            return cam;
        }

        void SetupTraffic()
        {
            var holder = new GameObject("TrafficPrefabs");
            holder.SetActive(false);
            var colors = new[]
            {
                new Color(0.8f, 0.8f, 0.82f), new Color(0.1f, 0.1f, 0.12f), new Color(0.55f, 0.1f, 0.1f),
                new Color(0.15f, 0.3f, 0.6f), new Color(0.6f, 0.6f, 0.62f), new Color(0.85f, 0.7f, 0.2f),
            };
            var prefabs = colors.Select(c => PlaceholderCarFactory.BuildTraffic(holder.transform, c)).ToArray();

            traffic = new GameObject("Traffic").AddComponent<TrafficManager>();
            traffic.prefabs = prefabs;
            traffic.maxCars = trafficCars;
        }

        void SpawnCar(VehicleDefinition d, bool first)
        {
            Vector3 ground; Quaternion rot;
            if (first || !car)
            {
                streamer.Generator.SpawnPose(out var p, out rot);
                ground = new Vector3(p.x, 0.05f, p.z);
            }
            else
            {
                ground = new Vector3(car.transform.position.x, car.transform.position.y + 0.3f, car.transform.position.z);
                rot = Quaternion.Euler(0f, car.transform.eulerAngles.y, 0f);
                Destroy(car);
            }

            def = d;
            car = PlaceholderCarFactory.Build(d, ground, rot, new Color(0.78f, 0.12f, 0.10f));
            car.SetActive(true);

            var controller = car.GetComponent<VehicleController>();
            var customizer = car.GetComponent<VehicleCustomizer>();
            customizer.build.vehicleId = d.id;

            string json = SaveSystem.LoadBuild(d.id);
            if (!string.IsNullOrEmpty(json)) customizer.LoadJson(json, id => parts.FirstOrDefault(p => p.id == id));
            else customizer.SetPaint(new Color(0.78f, 0.12f, 0.10f), 0.6f, 0.85f);

            driver.car = controller;
            hud.car = controller;
            hud.SetName(d.DisplayName);
            rig.target = car.transform;
            rig.body = car.GetComponent<Rigidbody>();
            streamer.target = car.transform;
            traffic.player = car.transform;
            garage.SetCurrent(d, customizer);
            SaveSystem.SaveLastVehicle(d.id);
        }
    }
}
