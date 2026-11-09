using UnityEngine;

namespace Sim.Game
{
    // Per-car build (paint and parts) saved as JSON in PlayerPrefs.
    public static class SaveSystem
    {
        const string BuildKey = "ows.build.", LastKey = "ows.lastVehicle";

        public static void SaveBuild(string vehicleId, string json) => PlayerPrefs.SetString(BuildKey + vehicleId, json);
        public static string LoadBuild(string vehicleId) =>
            PlayerPrefs.HasKey(BuildKey + vehicleId) ? PlayerPrefs.GetString(BuildKey + vehicleId) : null;

        public static void SaveLastVehicle(string id) { PlayerPrefs.SetString(LastKey, id); PlayerPrefs.Save(); }
        public static string LoadLastVehicle() => PlayerPrefs.GetString(LastKey, null);
    }
}
