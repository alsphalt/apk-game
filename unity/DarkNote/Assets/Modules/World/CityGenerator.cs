using System.Collections.Generic;
using UnityEngine;

namespace Sim.World
{
    public enum RoadClass { Local, Arterial, Highway }

    // Frees the generated mesh when a chunk is destroyed.
    public sealed class ChunkOwner : MonoBehaviour
    {
        public Mesh mesh;
        void OnDestroy() { if (mesh) Destroy(mesh); }
    }

    // Builds one city chunk (streets, lots, buildings, elevated highways, ramps and traffic lanes)
    // from grid coordinates alone, so the city is deterministic and can be infinite.
    //
    // Grid: intersections ("nodes") every blockSize metres. A chunk owns blocksPerChunk x
    // blocksPerChunk nodes and the street segments that leave them towards +x and +z.
    // Highways run north-south on a raised deck. Ground streets pass under them and are linked
    // by diagonal ramps at arterial crossings.
    public sealed class CityGenerator
    {
        const int SubRoad = 3, SubConcrete = 4;
        const float RoadY = 0.05f, PatchY = 0.08f;
        const float ULocal0 = 4f / 256f, ULocal1 = 98f / 256f;
        const float UMulti0 = 106f / 256f, UMulti1 = 226f / 256f;
        const float UPlain = 243f / 256f;

        readonly CityGeneratorSettings s;
        readonly Material[] mats = new Material[5];

        sealed class MeshAcc
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int>[] tris =
                { new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        }

        public CityGenerator(CityGeneratorSettings settings)
        {
            s = settings;
            BuildMaterials();
        }

        // ---------- grid rules ----------

        static int Mod(int a, int m) => ((a % m) + m) % m;
        bool IsHighwayCol(int i) => Mod(i, s.highwayEvery) == s.highwayOffset;
        RoadClass NsClass(int i) => IsHighwayCol(i) ? RoadClass.Highway :
            Mod(i, s.arterialEvery) == 0 ? RoadClass.Arterial : RoadClass.Local;
        RoadClass EwClass(int j) => Mod(j, s.arterialEvery) == 0 ? RoadClass.Arterial : RoadClass.Local;

        float Width(RoadClass c) =>
            c == RoadClass.Highway ? s.highwayWidth : c == RoadClass.Arterial ? s.arterialWidth : s.localWidth;
        float Speed(RoadClass c) =>
            c == RoadClass.Highway ? s.highwaySpeedKmh : c == RoadClass.Arterial ? s.arterialSpeedKmh : s.localSpeedKmh;
        float Inset(RoadClass c) => Width(c) * 0.5f + s.sidewalk;

        static float[] LaneOffsets(RoadClass c) =>
            c == RoadClass.Highway ? new[] { 2.25f, 5.75f, 9.25f } :
            c == RoadClass.Arterial ? new[] { 1.75f, 5.25f } : new[] { 2.25f };

        float Side => s.leftHandTraffic ? -1f : 1f;

        // Where to put the player: eastbound arterial lane near the origin.
        public void SpawnPose(out Vector3 pos, out Quaternion rot)
        {
            pos = new Vector3(30f, 0.9f, -1.75f * Side);
            rot = Quaternion.LookRotation(Vector3.right);
        }

        // ---------- chunk ----------

        public GameObject BuildChunk(Vector2Int cell, Transform parent)
        {
            var go = new GameObject($"Chunk {cell.x},{cell.y}");
            go.transform.SetParent(parent, false);

            float S = s.ChunkSize;
            var ground = go.AddComponent<BoxCollider>();
            ground.center = new Vector3(cell.x * S + S * 0.5f, -0.45f, cell.y * S + S * 0.5f);
            ground.size = new Vector3(S + 4f, 1f, S + 4f);

            var acc = new MeshAcc();
            int K = s.blocksPerChunk;
            for (int di = 0; di < K; di++)
            for (int dj = 0; dj < K; dj++)
            {
                int i = cell.x * K + di, j = cell.y * K + dj;
                var rng = new System.Random(unchecked(s.seed * 73856093 ^ i * 19349663 ^ j * 83492791));

                AddLot(acc, i, j);
                AddIntersection(acc, i, j);
                AddNsSegment(go, acc, i, j);
                AddEwSegment(go, acc, i, j);
                if (IsHighwayCol(i))
                {
                    AddHighwaySegment(go, acc, i, j);
                    if (Mod(j, s.arterialEvery) == 0) AddRamps(go, acc, i, j);
                }
                AddBuildings(go, acc, i, j, rng);
            }

            var mesh = new Mesh { name = go.name };
            if (acc.v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(acc.v);
            mesh.SetUVs(0, acc.uv);
            mesh.subMeshCount = 5;
            for (int k = 0; k < 5; k++) mesh.SetTriangles(acc.tris[k], k);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            go.AddComponent<ChunkOwner>().mesh = mesh;
            return go;
        }

        // ---------- streets ----------

        void AddLot(MeshAcc m, int i, int j)
        {
            float B = s.blockSize;
            float x0 = i * B, x1 = x0 + B, z0 = j * B, z1 = z0 + B;
            var uv = new Vector2(0.5f, 0.5f);
            Quad(m, SubConcrete, new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), new Vector3(x1, 0, z0), uv, uv, uv, uv);
        }

        void AddIntersection(MeshAcc m, int i, int j)
        {
            if (IsHighwayCol(i)) return;
            float B = s.blockSize;
            float hx = Width(NsClass(i)) * 0.5f, hz = Width(EwClass(j)) * 0.5f;
            float x = i * B, z = j * B;
            var uv = new Vector2(UPlain, 0.5f);
            Quad(m, SubRoad, new Vector3(x - hx, PatchY, z - hz), new Vector3(x - hx, PatchY, z + hz),
                new Vector3(x + hx, PatchY, z + hz), new Vector3(x + hx, PatchY, z - hz), uv, uv, uv, uv);
        }

        void AddNsSegment(GameObject go, MeshAcc m, int i, int j)
        {
            if (IsHighwayCol(i)) return;
            var c = NsClass(i);
            float B = s.blockSize;
            var a = new Vector3(i * B, RoadY, j * B);
            var b = new Vector3(i * B, RoadY, (j + 1) * B);
            Strip(m, new[] { a, b }, Width(c), c);
            Lanes(go, a, b, c, new Vector3Int(i, j, 0), new Vector3Int(i, j + 1, 0));
        }

        void AddEwSegment(GameObject go, MeshAcc m, int i, int j)
        {
            var c = EwClass(j);
            if (c == RoadClass.Local && (IsHighwayCol(i) || IsHighwayCol(i + 1))) return;   // dead ends before the highway
            float B = s.blockSize;
            var a = new Vector3(i * B, RoadY, j * B);
            var b = new Vector3((i + 1) * B, RoadY, j * B);
            Strip(m, new[] { a, b }, Width(c), c);
            Lanes(go, a, b, c, new Vector3Int(i, j, 0), new Vector3Int(i + 1, j, 0));
        }

        void Lanes(GameObject go, Vector3 a, Vector3 b, RoadClass c, Vector3Int na, Vector3Int nb)
        {
            Vector3 dir = (b - a).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, dir) * Side;
            float speed = Speed(c);
            foreach (float off in LaneOffsets(c))
            {
                Vector3 o = right * off;
                RoadLane.Create(go.transform, new[] { a + o, b + o }, speed, na, nb);
                RoadLane.Create(go.transform, new[] { b - o, a - o }, speed, nb, na);
            }
        }

        // ---------- highway ----------

        void AddHighwaySegment(GameObject go, MeshAcc m, int i, int j)
        {
            float B = s.blockSize, h = s.highwayHeight, hw = s.highwayWidth * 0.5f;
            float X = i * B, za = j * B, zb = za + B, zm = (za + zb) * 0.5f;

            Strip(m, new[] { new Vector3(X, h, za), new Vector3(X, h, zb) }, s.highwayWidth, RoadClass.Highway);
            Box(m, SubConcrete, X - hw, X + hw, h - 1.2f, h - 0.01f, za, zb, false, true, false);        // deck body
            Box(m, SubConcrete, X - hw, X - hw + 0.3f, h, h + 0.9f, za, zb, true, true, false);          // parapets
            Box(m, SubConcrete, X + hw - 0.3f, X + hw, h, h + 0.9f, za, zb, true, true, false);
            for (int p = 0; p < 2; p++)                                                                    // pillars mid-block, clear of crossing streets
            {
                float pz = za + B * (p == 0 ? 0.25f : 0.75f);
                Box(m, SubConcrete, X - 1.5f, X + 1.5f, 0f, h - 1.2f, pz - 1.5f, pz + 1.5f, true, false, false);
                AddCollider(go, new Vector3(X, (h - 1.2f) * 0.5f, pz), new Vector3(3f, h - 1.2f, 3f));
            }
            AddCollider(go, new Vector3(X, h - 0.6f, zm), new Vector3(s.highwayWidth, 1.2f, B));
            AddCollider(go, new Vector3(X - hw + 0.15f, h + 0.45f, zm), new Vector3(0.3f, 0.9f, B));
            AddCollider(go, new Vector3(X + hw - 0.15f, h + 0.45f, zm), new Vector3(0.3f, 0.9f, B));

            // Lanes: northbound on the traffic side, southbound on the other.
            float y = h + 0.02f;
            var na = new Vector3Int(i, j, 1);
            var nb = new Vector3Int(i, j + 1, 1);
            foreach (float off in LaneOffsets(RoadClass.Highway))
            {
                float o = off * Side;
                RoadLane.Create(go.transform, new[] { new Vector3(X + o, y, za), new Vector3(X + o, y, zb) }, s.highwaySpeedKmh, na, nb);
                RoadLane.Create(go.transform, new[] { new Vector3(X - o, y, zb), new Vector3(X - o, y, za) }, s.highwaySpeedKmh, nb, na);
            }
        }

        // Four ramps at each highway / arterial crossing (j is the arterial row).
        // Northbound off at node (hx, j-1) down to (hx+side, j); on from (hx+side, j) up to (hx, j+1).
        // Southbound off at node (hx, j+1) down to (hx-side, j); on from (hx-side, j) up to (hx, j-1).
        void AddRamps(GameObject go, MeshAcc m, int hx, int j)
        {
            float B = s.blockSize, y = s.highwayHeight + 0.02f, outer = 9.25f * Side;
            float X = hx * B;
            int side = s.leftHandTraffic ? -1 : 1;

            Vector3 nbAt(int k) => new Vector3(X + outer, y, k * B);
            Vector3 sbAt(int k) => new Vector3(X - outer, y, k * B);
            Vector3 ground(int col) => new Vector3(col * B, RoadY + 0.01f, j * B);

            // northbound off-ramp and on-ramp (traffic side)
            Ramp(go, m, nbAt(j - 1), ground(hx + side), new Vector3Int(hx, j - 1, 1), new Vector3Int(hx + side, j, 0), 0.25f);
            Ramp(go, m, ground(hx + side), nbAt(j + 1), new Vector3Int(hx + side, j, 0), new Vector3Int(hx, j + 1, 1), 0.5f);
            // southbound
            Ramp(go, m, sbAt(j + 1), ground(hx - side), new Vector3Int(hx, j + 1, 1), new Vector3Int(hx - side, j, 0), 0.25f);
            Ramp(go, m, ground(hx - side), sbAt(j - 1), new Vector3Int(hx - side, j, 0), new Vector3Int(hx, j - 1, 1), 0.5f);
        }

        void Ramp(GameObject go, MeshAcc m, Vector3 from, Vector3 to, Vector3Int na, Vector3Int nb, float weight)
        {
            const int N = 10;
            var pts = new Vector3[N + 1];
            for (int k = 0; k <= N; k++)
            {
                float t = k / (float)N;
                float e = t * t * (3f - 2f * t);                     // smoothstep keeps both ends flat
                Vector3 p = Vector3.Lerp(from, to, t);
                p.y = Mathf.Lerp(from.y, to.y, e);
                pts[k] = p;
            }
            Strip(m, pts, 8f, RoadClass.Local);
            RoadLane.Create(go.transform, pts, s.rampSpeedKmh, na, nb, weight);

            for (int k = 0; k < N; k++)                               // oriented colliders along the ramp
            {
                Vector3 d = pts[k + 1] - pts[k];
                var seg = new GameObject("RampSeg");
                seg.transform.SetParent(go.transform, false);
                seg.transform.SetPositionAndRotation((pts[k] + pts[k + 1]) * 0.5f, Quaternion.LookRotation(d));
                var bc = seg.AddComponent<BoxCollider>();
                bc.size = new Vector3(8f, 0.6f, d.magnitude + 0.2f);
                bc.center = new Vector3(0f, -0.3f, 0f);
            }
        }

        // ---------- buildings ----------

        void AddBuildings(GameObject go, MeshAcc m, int i, int j, System.Random rng)
        {
            if (IsHighwayCol(i) || IsHighwayCol(i + 1)) return;       // ramp zone stays empty
            float B = s.blockSize;
            float x0 = i * B + Inset(NsClass(i)), x1 = (i + 1) * B - Inset(NsClass(i + 1));
            float z0 = j * B + Inset(EwClass(j)), z1 = (j + 1) * B - Inset(EwClass(j + 1));

            float n = Mathf.PerlinNoise(i * 0.07f + (s.seed % 1000) * 0.13f, j * 0.07f + (s.seed % 777) * 0.11f);
            int nx = rng.Next(1, 3), nz = rng.Next(1, 3);
            const float gap = 4f;
            float pw = (x1 - x0 - gap * (nx - 1)) / nx, pd = (z1 - z0 - gap * (nz - 1)) / nz;
            if (pw < 12f || pd < 12f) return;

            for (int a = 0; a < nx; a++)
            for (int b = 0; b < nz; b++)
            {
                if (rng.NextDouble() < s.emptyLotChance) continue;
                float shrink = (float)rng.NextDouble() * 0.1f;
                float px0 = x0 + a * (pw + gap) + pw * shrink, px1 = x0 + a * (pw + gap) + pw - pw * shrink;
                float pz0 = z0 + b * (pd + gap) + pd * shrink, pz1 = z0 + b * (pd + gap) + pd - pd * shrink;

                float f = n * n * (0.6f + 0.8f * (float)rng.NextDouble());
                int floors = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(s.minFloors, s.maxFloors, f)), s.minFloors, s.maxFloors);
                int sub = rng.Next(3);
                bool tower = floors > 12 && rng.NextDouble() < 0.6;
                int lowFloors = tower ? Mathf.RoundToInt(floors * 0.6f) : floors;

                Building(m, sub, px0, px1, pz0, pz1, 0f, lowFloors);
                float H = floors * s.floorHeight;
                if (tower)
                {
                    float ix = (px1 - px0) * 0.15f, iz = (pz1 - pz0) * 0.15f;
                    Building(m, sub, px0 + ix, px1 - ix, pz0 + iz, pz1 - iz, lowFloors * s.floorHeight, floors - lowFloors);
                }
                AddCollider(go, new Vector3((px0 + px1) * 0.5f, H * 0.5f, (pz0 + pz1) * 0.5f), new Vector3(px1 - px0, H, pz1 - pz0));
            }
        }

        void Building(MeshAcc m, int sub, float x0, float x1, float z0, float z1, float baseY, int floors)
        {
            float y1 = baseY + floors * s.floorHeight;
            float uX = Mathf.Max(1, Mathf.RoundToInt((z1 - z0) / s.bayWidth));   // +X / -X faces run along z
            float uZ = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / s.bayWidth));
            float v = floors;
            Vector2 solid = new Vector2(0.02f, 0.02f);

            // +X, -X, +Z, -Z walls (vertex order verified for outward normals), then roof.
            Quad(m, sub, new Vector3(x1, baseY, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, baseY, z1),
                new Vector2(0, 0), new Vector2(0, v), new Vector2(uX, v), new Vector2(uX, 0));
            Quad(m, sub, new Vector3(x0, baseY, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x0, baseY, z0),
                new Vector2(0, 0), new Vector2(0, v), new Vector2(uX, v), new Vector2(uX, 0));
            Quad(m, sub, new Vector3(x1, baseY, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x0, baseY, z1),
                new Vector2(0, 0), new Vector2(0, v), new Vector2(uZ, v), new Vector2(uZ, 0));
            Quad(m, sub, new Vector3(x0, baseY, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, baseY, z0),
                new Vector2(0, 0), new Vector2(0, v), new Vector2(uZ, v), new Vector2(uZ, 0));
            Quad(m, sub, new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0),
                solid, solid, solid, solid);
        }

        // ---------- mesh helpers ----------

        // Quad with vertices a,b,c,d in clockwise order seen from the visible side.
        static void Quad(MeshAcc m, int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = m.v.Count;
            m.v.Add(a); m.v.Add(b); m.v.Add(c); m.v.Add(d);
            m.uv.Add(ua); m.uv.Add(ub); m.uv.Add(uc); m.uv.Add(ud);
            var t = m.tris[sub];
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        // Plain concrete box. Walls always; top and bottom optional.
        static void Box(MeshAcc m, int sub, float x0, float x1, float y0, float y1, float z0, float z1,
            bool top, bool bottom, bool unused)
        {
            var u = new Vector2(0.5f, 0.5f);
            Quad(m, sub, new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), u, u, u, u);
            Quad(m, sub, new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0), u, u, u, u);
            Quad(m, sub, new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), u, u, u, u);
            Quad(m, sub, new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), u, u, u, u);
            if (top)
                Quad(m, sub, new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), u, u, u, u);
            if (bottom)
                Quad(m, sub, new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), u, u, u, u);
        }

        // Textured road strip along a polyline. Texture zone depends on road class.
        static void Strip(MeshAcc m, Vector3[] pts, float width, RoadClass c)
        {
            float u0 = c == RoadClass.Local ? ULocal0 : UMulti0;
            float u1 = c == RoadClass.Local ? ULocal1 : UMulti1;
            float dist = 0f;
            for (int k = 0; k < pts.Length - 1; k++)
            {
                Vector3 d = pts[k + 1] - pts[k];
                Vector3 flat = new Vector3(d.x, 0f, d.z).normalized;
                Vector3 r = Vector3.Cross(Vector3.up, flat) * (width * 0.5f);
                float len = d.magnitude;
                float v0 = dist / 8f, v1 = (dist + len) / 8f;
                Quad(m, SubRoad, pts[k] - r, pts[k + 1] - r, pts[k + 1] + r, pts[k] + r,
                    new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0));
                dist += len;
            }
        }

        static void AddCollider(GameObject go, Vector3 center, Vector3 size)
        {
            var bc = go.AddComponent<BoxCollider>();
            bc.center = center; bc.size = size;
        }

        // ---------- materials and generated textures ----------

        void BuildMaterials()
        {
            var tints = new[] { new Color(0.82f, 0.82f, 0.80f), new Color(0.80f, 0.62f, 0.52f), new Color(0.62f, 0.72f, 0.82f) };
            Texture2D facade = null;
            for (int k = 0; k < 3; k++)
            {
                if (s.facadeMaterials != null && k < s.facadeMaterials.Length && s.facadeMaterials[k]) { mats[k] = s.facadeMaterials[k]; continue; }
                facade = facade ? facade : MakeFacadeTexture();
                mats[k] = MakeMaterial("Facade" + k, facade, tints[k], 0.15f);
            }
            mats[SubRoad] = s.roadMaterial ? s.roadMaterial : MakeMaterial("Road", MakeRoadTexture(), Color.white, 0.05f);
            mats[SubConcrete] = s.concreteMaterial ? s.concreteMaterial : MakeMaterial("Concrete", MakeConcreteTexture(), Color.white, 0.05f);
        }

        // URP/Lit must be included in the build (Project Settings > Graphics > Always Included Shaders)
        // or you must assign materials in the settings asset, otherwise Shader.Find returns null on device.
        static Material MakeMaterial(string name, Texture2D tex, Color tint, float smoothness)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            var mat = new Material(sh) { name = name };
            mat.mainTexture = tex;
            mat.color = tint;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            return mat;
        }

        static Texture2D NewTex(int w, int h, string name)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name };
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Bilinear;
            t.anisoLevel = 4;
            return t;
        }

        static Texture2D MakeFacadeTexture()
        {
            const int N = 64;
            var t = NewTex(N, N, "FacadeTile");
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                bool window = x >= 14 && x < 50 && y >= 16 && y < 52;
                bool frame = x >= 12 && x < 52 && y >= 14 && y < 54;
                float g = window ? 0.22f + 0.25f * (y - 16) / 36f : frame ? 0.55f : 0.9f;
                if (!window && !frame) g -= ((x * 7 + y * 13) % 5) * 0.01f;
                byte b = (byte)(Mathf.Clamp01(g) * 255f);
                px[y * N + x] = new Color32(b, b, (byte)Mathf.Min(255, b + (window ? 25 : 0)), 255);
            }
            t.SetPixels32(px); t.Apply(true);
            return t;
        }

        static Texture2D MakeRoadTexture()
        {
            const int W = 256, H = 64;
            var t = NewTex(W, H, "RoadAtlas");
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float g = 0.20f + ((x * 31 + y * 17) % 7) * 0.006f;
                Color c = new Color(g, g, g * 1.03f);
                if (x < 102)                                      // zone A: local road
                {
                    if ((x >= 6 && x < 9) || (x >= 93 && x < 96)) c = Color.white;
                    if ((x >= 47 && x < 50) || (x >= 53 && x < 56)) c = new Color(0.9f, 0.75f, 0.1f);
                }
                else if (x < 230)                                 // zone B: multi-lane road
                {
                    if ((x >= 108 && x < 111) || (x >= 221 && x < 224)) c = Color.white;
                    if ((x >= 163 && x < 166) || (x >= 168 && x < 171)) c = new Color(0.9f, 0.75f, 0.1f);
                    if (((x >= 133 && x < 135) || (x >= 197 && x < 199)) && y < 32) c = Color.white;
                }
                px[y * W + x] = c;
            }
            t.SetPixels32(px); t.Apply(true);
            return t;
        }

        static Texture2D MakeConcreteTexture()
        {
            var t = NewTex(16, 16, "Concrete");
            var px = new Color32[256];
            for (int k = 0; k < 256; k++)
            {
                byte b = (byte)(170 + (k * 37 % 11));
                px[k] = new Color32(b, b, b, 255);
            }
            t.SetPixels32(px); t.Apply(true);
            return t;
        }
    }
}
