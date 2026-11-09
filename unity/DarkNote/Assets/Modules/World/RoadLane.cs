using System.Collections.Generic;
using UnityEngine;

namespace Sim.World
{
    // One direction of one road, as a polyline. Hand-authored lanes use Transform points.
    // Procedural lanes use worldPoints and graph nodes, so traffic can find the next lane
    // even when the neighbouring chunk streams in later.
    public class RoadLane : MonoBehaviour
    {
        public static readonly List<RoadLane> All = new List<RoadLane>();
        static readonly Dictionary<Vector3Int, List<RoadLane>> StartingAt = new Dictionary<Vector3Int, List<RoadLane>>();

        public Transform[] points;           // ordered along the direction of travel
        public Vector3[] worldPoints;        // alternative to points (procedural lanes)
        public float speedLimitKmh = 50f;
        public RoadLane[] next;              // explicit continuations (hand-authored junctions)

        // Graph nodes: x,y = grid cell, z = level (0 ground, 1 highway).
        public bool hasNodes;
        public Vector3Int startNode, endNode;
        public float weight = 1f;            // chance weight when a junction offers several lanes

        Vector3[] pos;
        Vector3 startDir, endDir;     // flat, normalised
        float[] cumulative;
        public float Length { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); StartingAt.Clear(); }

        public static RoadLane Create(Transform parent, Vector3[] pts, float speedKmh,
            Vector3Int start, Vector3Int end, float weight = 1f)
        {
            var go = new GameObject("Lane");
            go.SetActive(false);                         // set data before OnEnable runs
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<RoadLane>();
            l.worldPoints = pts; l.speedLimitKmh = speedKmh;
            l.hasNodes = true; l.startNode = start; l.endNode = end; l.weight = weight;
            go.SetActive(true);
            return l;
        }

        void OnEnable()
        {
            Bake();
            All.Add(this);
            if (hasNodes)
            {
                if (!StartingAt.TryGetValue(startNode, out var list)) StartingAt[startNode] = list = new List<RoadLane>();
                list.Add(this);
            }
        }

        void OnDisable()
        {
            All.Remove(this);
            if (hasNodes && StartingAt.TryGetValue(startNode, out var list)) list.Remove(this);
        }

        void Bake()
        {
            if (worldPoints != null && worldPoints.Length > 0) pos = worldPoints;
            else if (points != null && points.Length > 0)
            {
                pos = new Vector3[points.Length];
                for (int i = 0; i < points.Length; i++) pos[i] = points[i] != null ? points[i].position : Vector3.zero;
            }
            else pos = new[] { transform.position, transform.position + Vector3.forward };

            // A single point would make the direction maths read past the end of the array.
            if (pos.Length < 2) pos = new[] { pos[0], pos[0] + Vector3.forward };
            cumulative = new float[pos.Length];
            for (int i = 1; i < pos.Length; i++)
                cumulative[i] = cumulative[i - 1] + Vector3.Distance(pos[i - 1], pos[i]);
            Length = cumulative[pos.Length - 1];
            startDir = Flat(pos[1] - pos[0]);
            endDir = Flat(pos[pos.Length - 1] - pos[pos.Length - 2]);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

        bool CanContinueOnto(RoadLane o) =>
            o && o.endNode != startNode && Vector3.Dot(endDir, o.startDir) > -0.5f;   // no U-turns or wrong-way entry

        public Vector3 GetPoint(float distance, out Vector3 tangent)
        {
            if (pos == null || pos.Length < 2)
            {
                tangent = transform.forward;
                return transform.position;
            }
            distance = Mathf.Clamp(distance, 0f, Length);
            int i = 1;
            while (i < pos.Length - 1 && cumulative[i] < distance) i++;
            float seg = Mathf.Max(0.001f, cumulative[i] - cumulative[i - 1]);
            float t = (distance - cumulative[i - 1]) / seg;
            Vector3 a = pos[i - 1], b = pos[i];
            tangent = (b - a).normalized;
            return Vector3.Lerp(a, b, t);
        }

        // Picks the lane to continue on at the end of this one. Returns null at a dead end.
        public RoadLane PickNext()
        {
            if (next != null && next.Length > 0) return next[Random.Range(0, next.Length)];
            if (!hasNodes || !StartingAt.TryGetValue(endNode, out var options)) return null;

            float total = 0f;
            foreach (var o in options)
                if (CanContinueOnto(o)) total += o.weight;
            if (total <= 0f) return null;

            float r = Random.value * total;
            foreach (var o in options)
            {
                if (!CanContinueOnto(o)) continue;
                r -= o.weight;
                if (r <= 0f) return o;
            }
            return null;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            if (pos != null)
                for (int i = 1; i < pos.Length; i++) Gizmos.DrawLine(pos[i - 1], pos[i]);
            else if (points != null)
                for (int i = 1; i < points.Length; i++)
                    if (points[i - 1] && points[i]) Gizmos.DrawLine(points[i - 1].position, points[i].position);
        }
    }
}
