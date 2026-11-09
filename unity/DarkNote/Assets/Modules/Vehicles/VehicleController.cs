using UnityEngine;

namespace Sim.Vehicles
{
    [System.Serializable]
    public struct VehicleInput
    {
        public float throttle;   // 0..1
        public float brake;      // 0..1
        public float steer;      // -1..1
        public float handbrake;  // 0..1
        public bool reverse;
    }

    // Order of wheels: 0 front-left, 1 front-right, 2 rear-left, 3 rear-right.
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleController : MonoBehaviour
    {
        public VehicleDefinition def;
        public StatModifiers mods = StatModifiers.Identity;
        public WheelCollider[] wheels = new WheelCollider[4];
        public Transform[] wheelMeshes = new Transform[4];
        public VehicleInput input;

        Rigidbody rb;

        public EngineModel Engine { get; private set; }
        public Gearbox Box { get; private set; }
        public float SpeedKmh { get; private set; }
        public float ForwardSpeed { get; private set; }   // m/s, negative when rolling backwards
        public float RpmNormalized => Engine.Rpm / def.redlineRpm;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            Apply();
        }

        // Call again after changing def or mods (customization).
        public void Apply()
        {
            if (!def) { Debug.LogError("VehicleController has no VehicleDefinition", this); return; }
            rb.mass = def.massKg + mods.massDelta;
            rb.centerOfMass = def.centerOfMassOffset;
            rb.linearDamping = 0.02f;
            Engine = new EngineModel(def);
            Box = new Gearbox();

            for (int i = 0; i < 4; i++)
            {
                var w = wheels[i];
                if (!w) { Debug.LogError("VehicleController is missing wheel collider " + i, this); return; }
                w.radius = def.wheelRadius;
                w.mass = def.wheelMass;
                w.suspensionDistance = def.travel;
                w.suspensionSpring = new JointSpring
                {
                    spring = def.spring * mods.suspensionMult,
                    damper = def.damper,
                    targetPosition = 0.5f
                };
                SetGrip(w, (i < 2 ? def.gripFront : def.gripRear) * mods.gripMult);
            }
        }

        static void SetGrip(WheelCollider w, float stiffness)
        {
            var f = new WheelFrictionCurve
            {
                extremumSlip = 0.4f, extremumValue = 1f,
                asymptoteSlip = 0.8f, asymptoteValue = 0.5f, stiffness = stiffness
            };
            w.forwardFriction = f;
            f.extremumSlip = 0.25f; f.asymptoteSlip = 0.5f;
            w.sidewaysFriction = f;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            float fwd = Vector3.Dot(rb.linearVelocity, transform.forward);
            SpeedKmh = Mathf.Abs(fwd) * 3.6f;
            ForwardSpeed = fwd;

            // Electronic speed limiter: fades throttle out over the last 5 km/h.
            float thr = input.throttle;
            if (def.speedLimiterKmh > 0f)
                thr *= Mathf.Clamp01((def.speedLimiterKmh - SpeedKmh) / 5f);

            float lim = Mathf.Lerp(1f, def.highSpeedSteerFactor, Mathf.Clamp01(SpeedKmh / 160f));
            float steer = input.steer * def.maxSteerAngle * lim;
            wheels[0].steerAngle = steer;
            wheels[1].steerAngle = steer;

            Box.Step(def, Engine.Rpm, thr, fwd, input.reverse, dt);
            float ratio = Box.Ratio(def) * def.finalDrive;
            bool locked = Mathf.Abs(ratio) > 0.01f;

            float drivenRpm = DrivenWheelRpm() * Mathf.Abs(ratio);
            float engineTq = Engine.Step(thr, drivenRpm, locked, dt, mods.powerMult);
            float wheelTq = locked ? engineTq * ratio * def.drivetrainEfficiency : 0f;

            DriveWheels(wheelTq);
            Brakes();
            Aero(fwd);
            AntiRoll(0, 1);
            AntiRoll(2, 3);
            UpdateMeshes();
        }

        float DrivenWheelRpm()
        {
            float sum = 0f; int n = 0;
            for (int i = 0; i < 4; i++)
            {
                bool driven = def.drive == DriveType.AWD || (def.drive == DriveType.FWD ? i < 2 : i >= 2);
                if (!driven) continue;
                sum += wheels[i].rpm; n++;
            }
            return n > 0 ? Mathf.Abs(sum / n) : 0f;
        }

        void DriveWheels(float wheelTq)
        {
            float front = def.drive == DriveType.FWD ? 1f : def.drive == DriveType.AWD ? def.awdFrontSplit : 0f;
            for (int i = 0; i < 4; i++)
            {
                float share = i < 2 ? front : 1f - front;
                float tq = wheelTq * share * 0.5f;

                if (def.tractionControl && Mathf.Abs(tq) > 1f &&
                    wheels[i].GetGroundHit(out var hit) && Mathf.Abs(hit.forwardSlip) > def.tcSlipLimit)
                    tq *= 0.4f;

                wheels[i].motorTorque = tq;
            }
        }

        void Brakes()
        {
            float total = input.brake * def.brakeTorque * mods.brakeMult;
            for (int i = 0; i < 4; i++)
            {
                float share = i < 2 ? def.brakeBiasFront : 1f - def.brakeBiasFront;
                float b = total * share * 0.5f;

                if (def.abs && wheels[i].GetGroundHit(out var hit) && hit.forwardSlip < -0.35f) b *= 0.3f;
                if (i >= 2) b += input.handbrake * def.brakeTorque * 1.5f;

                wheels[i].brakeTorque = b;
            }
            // Handbrake loosens the rear for drifting.
            float rear = def.gripRear * mods.gripMult * (1f - 0.5f * input.handbrake);
            SetGrip(wheels[2], rear);
            SetGrip(wheels[3], rear);
        }

        void Aero(float fwd)
        {
            Vector3 v = rb.linearVelocity;
            float speed = v.magnitude;
            rb.AddForce(-0.5f * 1.225f * def.dragCoefficient * mods.dragMult * def.frontalAreaM2 * speed * v);
            rb.AddForce(-transform.up * def.downforce * speed * speed);
        }

        void AntiRoll(int l, int r)
        {
            float travelL = Travel(wheels[l], out bool gl);
            float travelR = Travel(wheels[r], out bool gr);
            float force = (travelL - travelR) * def.antiRoll;
            if (gl) rb.AddForceAtPosition(wheels[l].transform.up * -force, wheels[l].transform.position);
            if (gr) rb.AddForceAtPosition(wheels[r].transform.up * force, wheels[r].transform.position);
        }

        float Travel(WheelCollider w, out bool grounded)
        {
            grounded = w.GetGroundHit(out var hit);
            if (!grounded) return 1f;
            return (-w.transform.InverseTransformPoint(hit.point).y - w.radius) / w.suspensionDistance;
        }

        void UpdateMeshes()
        {
            for (int i = 0; i < 4; i++)
            {
                if (!wheelMeshes[i]) continue;
                wheels[i].GetWorldPose(out var p, out var q);
                wheelMeshes[i].SetPositionAndRotation(p, q);
            }
        }
    }
}
