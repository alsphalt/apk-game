using Sim.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Sim.Vehicles
{
    public enum DriveType { FWD, RWD, AWD }
    public enum EngineLayout { I3, I4, I5, I6, V6, V8, V10, V12, F4, F6 }
    public enum BodyType { Sedan, Coupe, Hatch, SUV, Wagon, Sports, Super, Truck }

    // One asset per car. All driving feel comes from these numbers.
    [CreateAssetMenu(menuName = "Sim/Vehicle Definition")]
    public class VehicleDefinition : ScriptableObject, IContent
    {
        [Header("Identity")]
        public string id;
        public string brand, model;
        public int year;
        public BodyType body;
        public AssetReferenceGameObject prefab;
        public Sprite icon;

        [Header("Engine")]
        public AnimationCurve torqueNm = AnimationCurve.Linear(1000, 300, 6500, 300); // x = rpm, y = Nm
        public float idleRpm = 800f;
        public float redlineRpm = 7000f;
        public float engineBrakingNm = 60f;
        public bool turbo;
        public float turboLag = 0.5f;
        public bool supercharged;
        public EngineLayout layout = EngineLayout.I4;   // drives the synthesised engine sound
        public float speedLimiterKmh = 0f;               // 0 = none

        [Header("Transmission")]
        public DriveType drive = DriveType.RWD;
        public float[] gearRatios = { 3.6f, 2.2f, 1.5f, 1.1f, 0.9f, 0.75f };
        public float reverseRatio = 3.4f;
        public float finalDrive = 3.5f;
        public float shiftTime = 0.25f;
        public float drivetrainEfficiency = 0.88f;
        [Range(0f, 1f)] public float awdFrontSplit = 0.4f;

        [Header("Chassis")]
        public float massKg = 1500f;
        public float wheelbase = 2.7f;
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.35f, 0.05f);
        public float dragCoefficient = 0.32f;
        public float frontalAreaM2 = 2.2f;
        public float downforce = 0.3f;           // N per (m/s)^2

        [Header("Wheels and tyres")]
        public float wheelRadius = 0.34f;
        public float wheelMass = 20f;
        public float gripFront = 1.9f;           // friction stiffness
        public float gripRear = 2.0f;
        public float brakeTorque = 3500f;
        [Range(0f, 1f)] public float brakeBiasFront = 0.62f;

        [Header("Suspension")]
        public float spring = 40000f;
        public float damper = 4500f;
        public float travel = 0.22f;
        public float antiRoll = 6000f;

        [Header("Steering")]
        public float maxSteerAngle = 32f;
        [Range(0.2f, 1f)] public float highSpeedSteerFactor = 0.4f;

        [Header("Driver aids")]
        public bool abs = true;
        public bool tractionControl = true;
        public float tcSlipLimit = 0.35f;

        [Header("Audio")]
        public EngineSoundProfile sound;

        public string Id => id;

        // Peak power read from the torque curve, in horsepower. Used for UI stats.
        public float PeakPowerHp()
        {
            float best = 0f;
            for (float rpm = idleRpm; rpm <= redlineRpm; rpm += 100f)
                best = Mathf.Max(best, torqueNm.Evaluate(rpm) * rpm / 9549f);
            return best / 0.7457f;
        }
        public string DisplayName => brand + " " + model;
    }
}
