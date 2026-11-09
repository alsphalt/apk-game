using System;
using Sim.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Sim.World
{
    [Serializable]
    public struct ChunkEntry
    {
        public Vector2Int cell;
        public AssetReferenceGameObject prefab;   // buildings, props, road lanes for that square
    }

    // One asset per city. Add a city by creating a definition and chunk prefabs.
    [CreateAssetMenu(menuName = "Sim/City Definition")]
    public class CityDefinition : ScriptableObject, IContent
    {
        public string id;
        public string displayName;
        public float chunkSize = 200f;
        public Vector3 spawnPoint;
        public ChunkEntry[] chunks;

        public string Id => id;
    }
}
