using UnityEngine;

namespace Sim.World
{
    // All the numbers that shape a procedural city. Make another asset for another city.
    [CreateAssetMenu(menuName = "Sim/City Generator Settings")]
    public class CityGeneratorSettings : ScriptableObject
    {
        public string cityName = "Test City";
        public int seed = 1234;

        [Header("Grid")]
        public float blockSize = 100f;           // distance between intersections
        public int blocksPerChunk = 2;           // chunk size = blockSize * blocksPerChunk
        public int arterialEvery = 4;            // every Nth street is a wide 4-lane road
        public int highwayEvery = 16;            // a highway column every N blocks
        public int highwayOffset = 8;            // which column inside that period
        public float highwayHeight = 7f;

        [Header("Widths (m)")]
        public float localWidth = 9f;
        public float arterialWidth = 14f;
        public float highwayWidth = 22f;
        public float sidewalk = 3f;

        [Header("Buildings")]
        public float floorHeight = 3.4f;
        public float bayWidth = 3.5f;
        public int minFloors = 2;
        public int maxFloors = 28;
        [Range(0f, 0.5f)] public float emptyLotChance = 0.1f;

        [Header("Traffic")]
        public float localSpeedKmh = 40f;
        public float arterialSpeedKmh = 60f;
        public float highwaySpeedKmh = 110f;
        public float rampSpeedKmh = 50f;
        public bool leftHandTraffic = false;      // set true for Kenya, UK, Japan

        [Header("Materials (optional, generated if empty)")]
        public Material roadMaterial;
        public Material concreteMaterial;
        public Material[] facadeMaterials;        // 3 tints

        public float ChunkSize => blockSize * blocksPerChunk;
    }
}
