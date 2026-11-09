using System.Collections.Generic;
using UnityEngine;

namespace Sim.Traffic
{
    // One car at a time may cross a ground junction. Cheap and deadlock-safe enough for traffic
    // (cars give up waiting after a few seconds, see TrafficCar). Not used on highways.
    public static class JunctionLock
    {
        static readonly Dictionary<Vector3Int, TrafficCar> owners = new Dictionary<Vector3Int, TrafficCar>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => owners.Clear();

        public static bool TryEnter(Vector3Int node, TrafficCar car)
        {
            if (owners.TryGetValue(node, out var o) && o && o != car && o.isActiveAndEnabled) return false;
            owners[node] = car;
            return true;
        }

        public static void Release(Vector3Int node, TrafficCar car)
        {
            if (owners.TryGetValue(node, out var o) && o == car) owners.Remove(node);
        }
    }
}
