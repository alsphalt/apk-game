using Sim.World;
using UnityEngine;

namespace Sim.Traffic
{
    // Cheap AI car: follows a lane polyline kinematically (no wheel physics), brakes for whatever
    // is ahead, waits its turn at ground junctions and smooths lane changes at junctions.
    // Use a low-poly prefab with a kinematic Rigidbody and a box collider.
    [RequireComponent(typeof(Rigidbody))]
    public class TrafficCar : MonoBehaviour
    {
        public LayerMask obstacleMask = ~0;
        public float lookAhead = 25f;
        public float accel = 3f, brake = 8f;

        static readonly RaycastHit[] hits = new RaycastHit[8];
        Rigidbody rb;
        RoadLane lane;
        float along, speed, desired, waitTime;
        Vector3 offset;                       // visual lag that decays after switching lanes
        Vector3Int heldNode;
        bool holding;

        public bool Finished { get; private set; }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
        }

        void OnDisable() => ReleaseHeld();

        void ReleaseHeld()
        {
            if (holding) JunctionLock.Release(heldNode, this);
            holding = false;
        }

        public void Place(RoadLane l, float distance)
        {
            ReleaseHeld();
            lane = l;
            along = distance;
            desired = l.speedLimitKmh / 3.6f * Random.Range(0.8f, 1.05f);
            speed = desired * 0.7f;
            Finished = false;
            offset = Vector3.zero;
            waitTime = 0f;
            Vector3 p = l.GetPoint(along, out var tan);
            var rot = Quaternion.LookRotation(tan);
            transform.SetPositionAndRotation(p, rot);
            rb.position = p; rb.rotation = rot;
        }

        static float StopSpeed(float gap, float brake) => Mathf.Sqrt(2f * brake * 0.7f * Mathf.Max(0f, gap));

        void FixedUpdate()
        {
            if (!lane) { Finished = true; return; }       // chunk streamed out under us
            float dt = Time.fixedDeltaTime;
            float target = desired;

            // Only other vehicles count as obstacles (they have rigidbodies). Static road
            // geometry is ignored so slopes and ramps never look like walls.
            float look = Mathf.Max(lookAhead, speed * 2.5f);
            int n = Physics.RaycastNonAlloc(transform.position + Vector3.up * 0.5f, transform.forward,
                hits, look, obstacleMask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int h = 0; h < n; h++)
                if (hits[h].rigidbody && hits[h].rigidbody != rb && hits[h].distance < nearest) nearest = hits[h].distance;
            if (nearest < float.MaxValue) target = Mathf.Min(target, StopSpeed(nearest - 5f, brake));

            // Ground junctions: reserve before entering.
            float remaining = lane.Length - along;
            if (lane.hasNodes && lane.endNode.z == 0)
            {
                if (remaining < 16f && !(holding && heldNode == lane.endNode))
                {
                    if (JunctionLock.TryEnter(lane.endNode, this)) { ReleaseHeld(); heldNode = lane.endNode; holding = true; waitTime = 0f; }
                    else
                    {
                        waitTime += dt;
                        if (waitTime < 6f) target = Mathf.Min(target, StopSpeed(remaining - 5f, brake));   // after 6 s give up and go
                    }
                }
            }
            if (holding && lane.endNode != heldNode && along > 10f) ReleaseHeld();

            speed = Mathf.MoveTowards(speed, target, (target < speed ? brake : accel) * dt);
            along += speed * dt;

            if (along >= lane.Length)
            {
                var nextLane = lane.PickNext();
                if (nextLane == null) { Finished = true; return; }
                float over = along - lane.Length;
                Vector3 oldPos = rb.position;
                lane = nextLane;
                along = over;
                desired = lane.speedLimitKmh / 3.6f * Random.Range(0.8f, 1.05f);
                offset = oldPos - lane.GetPoint(along, out _);
                offset.y = 0f;
            }

            offset *= Mathf.Exp(-6f * dt);
            Vector3 p = lane.GetPoint(along, out var tan) + offset;
            rb.MovePosition(p);
            if (tan.sqrMagnitude > 0.001f)
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(tan), 1f - Mathf.Exp(-8f * dt)));
        }
    }
}
