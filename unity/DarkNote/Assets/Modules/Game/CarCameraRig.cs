using UnityEngine;

namespace Sim.Game
{
    // Chase, far chase, hood and cockpit views. Press the CAM button to cycle.
    public class CarCameraRig : MonoBehaviour
    {
        public enum Mode { Chase, Far, Hood, Cockpit }

        public Transform target;
        public Camera cam;
        public Mode mode = Mode.Chase;
        public float baseFov = 60f;
        public Rigidbody body;                      // for speed-based FOV

        public void Cycle() => mode = (Mode)(((int)mode + 1) % 4);

        void LateUpdate()
        {
            if (!target || !cam) return;
            float dt = Time.deltaTime;

            Vector3 flatFwd = target.forward; flatFwd.y = 0f;
            if (flatFwd.sqrMagnitude < 0.01f) flatFwd = Vector3.forward;
            var yaw = Quaternion.LookRotation(flatFwd.normalized, Vector3.up);

            if (mode == Mode.Chase || mode == Mode.Far)
            {
                Vector3 off = mode == Mode.Chase ? new Vector3(0f, 1.8f, -5.8f) : new Vector3(0f, 3.2f, -9.5f);
                Vector3 want = target.position + yaw * off;
                cam.transform.position = Vector3.Lerp(cam.transform.position, want, 1f - Mathf.Exp(-9f * dt));
                Vector3 look = target.position + Vector3.up * 1.2f + yaw * Vector3.forward * 3f;
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation,
                    Quaternion.LookRotation(look - cam.transform.position), 1f - Mathf.Exp(-12f * dt));
            }
            else
            {
                Vector3 local = mode == Mode.Hood ? new Vector3(0f, 0.95f, 0.9f) : new Vector3(-0.35f, 0.95f, 0.15f);
                cam.transform.position = target.TransformPoint(local);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, target.rotation, 1f - Mathf.Exp(-20f * dt));
            }

            float speed = body ? body.linearVelocity.magnitude : 0f;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, baseFov + Mathf.Clamp(speed * 0.25f, 0f, 14f), 1f - Mathf.Exp(-3f * dt));
        }
    }
}
