using System;
using Sim.Vehicles;
using UnityEngine;

namespace Sim.Controls
{
    // Merges touch, keyboard and tilt into a VehicleInput. Brake held at standstill engages
    // reverse (and then the brake pedal drives the car backwards), like most mobile racers.
    public class DriverInput : MonoBehaviour
    {
        public VehicleController car;
        public TouchControls touch;
        public bool useKeyboard = true;
        public float steerSpeed = 7f;

        public event Action CameraToggled;

        float steer, stopTimer;
        bool reverseMode;

        public bool InReverse => reverseMode;

        void Update()
        {
            if (!car) return;
            float thr = 0f, brk = 0f, st = 0f, hb = 0f;
            bool cam = false;

            if (touch)
            {
                thr = touch.Throttle; brk = touch.Brake; st = touch.Steer; hb = touch.Handbrake;
                cam = touch.ConsumeCamera();
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            if (useKeyboard)
            {
                float kt = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) ? 1f : 0f;
                float kb = (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) ? 1f : 0f;
                float ks = Input.GetAxisRaw("Horizontal");
                thr = Mathf.Max(thr, kt); brk = Mathf.Max(brk, kb);
                if (Mathf.Abs(ks) > Mathf.Abs(st)) st = ks;
                if (Input.GetKey(KeyCode.Space)) hb = 1f;
                if (Input.GetKeyDown(KeyCode.C)) cam = true;
            }
            if (touch && touch.tiltSteering && SystemInfo.supportsAccelerometer)
            {
                float tilt = Input.acceleration.x * 2.5f;
                if (Mathf.Abs(tilt) < 0.05f) tilt = 0f;
                st = Mathf.Clamp(tilt, -1f, 1f);
            }
#endif
            if (cam) CameraToggled?.Invoke();

            float dt = Time.deltaTime;
            steer = Mathf.MoveTowards(steer, st, steerSpeed * dt * (Mathf.Abs(st) > Mathf.Abs(steer) ? 1f : 1.6f));

            float v = car.ForwardSpeed;
            if (!reverseMode)
            {
                if (brk > 0.3f && thr < 0.1f && Mathf.Abs(v) < 0.8f)
                {
                    stopTimer += dt;
                    if (stopTimer > 0.3f) reverseMode = true;
                }
                else stopTimer = 0f;
            }
            else if (thr > 0.3f && brk < 0.1f && Mathf.Abs(v) < 1f)
            {
                reverseMode = false; stopTimer = 0f;
            }

            car.input = new VehicleInput
            {
                throttle = reverseMode ? brk : thr,
                brake = reverseMode ? thr : brk,
                steer = steer,
                handbrake = hb,
                reverse = reverseMode
            };
        }
    }
}
