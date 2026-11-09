using System.Collections.Generic;
using UnityEngine;

namespace Sim.World
{
    // Streams procedurally generated chunks around a target. Builds at most one chunk per frame.
    // Use this for generated cities and ChunkStreamer for hand-made Addressables chunks.
    public class ProceduralCityStreamer : MonoBehaviour
    {
        public CityGeneratorSettings settings;
        public Transform target;
        public int loadRadius = 2;
        public int unloadRadius = 3;

        readonly Dictionary<Vector2Int, GameObject> loaded = new Dictionary<Vector2Int, GameObject>();
        public CityGenerator Generator { get; private set; }

        void Awake() => Generator = new CityGenerator(settings);

        Vector2Int CellOf(Vector3 p)
        {
            float S = settings.ChunkSize;
            return new Vector2Int(Mathf.FloorToInt(p.x / S), Mathf.FloorToInt(p.z / S));
        }

        // Builds everything near a point immediately (call before spawning the player).
        public void Prewarm(Vector3 position, int radius)
        {
            var here = CellOf(position);
            for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++) Ensure(here + new Vector2Int(x, y));
        }

        void Ensure(Vector2Int cell)
        {
            if (!loaded.ContainsKey(cell)) loaded[cell] = Generator.BuildChunk(cell, transform);
        }

        void Update()
        {
            if (!target) return;
            var here = CellOf(target.position);

            List<Vector2Int> remove = null;
            foreach (var kv in loaded)
            {
                var d = kv.Key - here;
                if (Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) > unloadRadius)
                    (remove ??= new List<Vector2Int>()).Add(kv.Key);
            }
            if (remove != null)
                foreach (var c in remove) { Destroy(loaded[c]); loaded.Remove(c); }

            Vector2Int best = default; int bestD = int.MaxValue; bool found = false;
            for (int x = -loadRadius; x <= loadRadius; x++)
            for (int y = -loadRadius; y <= loadRadius; y++)
            {
                var cell = here + new Vector2Int(x, y);
                if (loaded.ContainsKey(cell)) continue;
                int d = x * x + y * y;
                if (d < bestD) { bestD = d; best = cell; found = true; }
            }
            if (found) Ensure(best);
        }
    }
}
