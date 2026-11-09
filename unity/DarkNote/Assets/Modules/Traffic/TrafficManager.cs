using System.Collections.Generic;
using Sim.World;
using UnityEngine;

namespace Sim.Traffic
{
    // Spawns pooled traffic on nearby lanes, outside the camera view, and recycles far cars.
    public class TrafficManager : MonoBehaviour
    {
        public Transform player;
        public TrafficCar[] prefabs;
        public int maxCars = 20;
        [Range(0f, 1f)] public float density = 1f;     // AdaptiveQuality lowers this on slow phones
        public float spawnMin = 70f, spawnMax = 160f, despawnDistance = 220f;
        public float interval = 0.5f;
        public LayerMask trafficMask;

        readonly List<TrafficCar> active = new List<TrafficCar>();
        readonly Stack<TrafficCar> pool = new Stack<TrafficCar>();
        readonly Plane[] planes = new Plane[6];
        float timer;
        Camera cam;

        void Start() => cam = Camera.main;

        void Update()
        {
            if (!player) return;
            if (prefabs == null || prefabs.Length == 0) return;
            timer += Time.deltaTime;
            if (timer < interval) return;
            timer = 0f;

            Recycle();
            if (active.Count < Mathf.RoundToInt(maxCars * density)) TrySpawn();
        }

        void Recycle()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var c = active[i];
                bool far = (c.transform.position - player.position).sqrMagnitude > despawnDistance * despawnDistance;
                if (!far && !c.Finished) continue;
                c.gameObject.SetActive(false);
                pool.Push(c);
                active.RemoveAt(i);
            }
        }

        bool Blocked(Vector3 pos)
        {
            if ((player.position - pos).sqrMagnitude < 30f * 30f) return true;
            for (int i = 0; i < active.Count; i++)
                if ((active[i].transform.position - pos).sqrMagnitude < 10f * 10f) return true;
            return false;
        }

        readonly List<RoadLane> candidates = new List<RoadLane>();

        void TrySpawn()
        {
            var lanes = RoadLane.All;
            if (lanes.Count == 0) return;
            if (cam) GeometryUtility.CalculateFrustumPlanes(cam, planes);

            // Only lanes that pass through the spawn ring are candidates.
            candidates.Clear();
            for (int i = 0; i < lanes.Count; i++)
            {
                var l = lanes[i];
                if (l.Length < 30f) continue;
                float d = Vector3.Distance(l.GetPoint(l.Length * 0.5f, out _), player.position);
                if (d > spawnMin - 60f && d < spawnMax + 60f) candidates.Add(l);
            }
            if (candidates.Count == 0) return;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                var lane = candidates[Random.Range(0, candidates.Count)];
                float d = Random.Range(0f, lane.Length);
                Vector3 pos = lane.GetPoint(d, out _);

                float dist = Vector3.Distance(pos, player.position);
                if (dist < spawnMin || dist > spawnMax) continue;
                if (cam && GeometryUtility.TestPlanesAABB(planes, new Bounds(pos, Vector3.one * 5f))) continue;
                if (Blocked(pos)) continue;

                var car = pool.Count > 0 ? pool.Pop() : Instantiate(prefabs[Random.Range(0, prefabs.Length)], transform);
                car.gameObject.SetActive(true);
                car.Place(lane, d);
                active.Add(car);
                return;
            }
        }
    }
}
