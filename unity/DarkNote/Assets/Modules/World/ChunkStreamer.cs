using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Sim.World
{
    // Keeps only the city squares near the player in memory. Loads one per frame
    // so streaming never causes a long hitch on phones.
    public class ChunkStreamer : MonoBehaviour
    {
        public CityDefinition city;
        public Transform target;
        public int loadRadius = 2;       // in chunks
        public int unloadRadius = 3;

        readonly Dictionary<Vector2Int, ChunkEntry> lookup = new Dictionary<Vector2Int, ChunkEntry>();
        readonly Dictionary<Vector2Int, AsyncOperationHandle<GameObject>> loaded =
            new Dictionary<Vector2Int, AsyncOperationHandle<GameObject>>();
        readonly HashSet<Vector2Int> loading = new HashSet<Vector2Int>();

        void Awake()
        {
            if (!city || city.chunks == null) return;
            foreach (var c in city.chunks) lookup[c.cell] = c;
        }

        Vector2Int CellOf(Vector3 p) =>
            new Vector2Int(Mathf.FloorToInt(p.x / city.chunkSize), Mathf.FloorToInt(p.z / city.chunkSize));

        void Update()
        {
            if (!target) return;
            var here = CellOf(target.position);
            UnloadFar(here);
            LoadNearest(here);
        }

        void LoadNearest(Vector2Int here)
        {
            if (loading.Count > 0) return;     // one at a time

            Vector2Int best = default;
            float bestDist = float.MaxValue;
            bool found = false;

            for (int x = -loadRadius; x <= loadRadius; x++)
            for (int y = -loadRadius; y <= loadRadius; y++)
            {
                var cell = here + new Vector2Int(x, y);
                if (!lookup.ContainsKey(cell) || loaded.ContainsKey(cell)) continue;
                float d = x * x + y * y;
                if (d < bestDist) { bestDist = d; best = cell; found = true; }
            }
            if (!found) return;

            loading.Add(best);
            var origin = new Vector3(best.x * city.chunkSize, 0f, best.y * city.chunkSize);
            var handle = Addressables.InstantiateAsync(lookup[best].prefab, origin, Quaternion.identity, transform);
            loaded[best] = handle;
            handle.Completed += _ => loading.Remove(best);
        }

        void UnloadFar(Vector2Int here)
        {
            List<Vector2Int> remove = null;
            foreach (var kv in loaded)
            {
                var d = kv.Key - here;
                if (Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) <= unloadRadius) continue;
                if (!kv.Value.IsDone) continue;
                (remove ??= new List<Vector2Int>()).Add(kv.Key);
            }
            if (remove == null) return;
            foreach (var cell in remove)
            {
                Addressables.ReleaseInstance(loaded[cell]);
                loaded.Remove(cell);
            }
        }
    }
}
