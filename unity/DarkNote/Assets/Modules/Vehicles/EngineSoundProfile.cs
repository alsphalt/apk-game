using UnityEngine;
using UnityEngine.Audio;

namespace Sim.Vehicles
{
    // Loopable recordings (or synthesised loops) of one engine at steady rpm bands.
    [CreateAssetMenu(menuName = "Sim/Engine Sound Profile")]
    public class EngineSoundProfile : ScriptableObject
    {
        [Header("On throttle loops (low, mid, high rpm)")]
        public AudioClip lowOn, midOn, highOn;

        [Header("Off throttle loops (low, mid, high rpm)")]
        public AudioClip lowOff, midOff, highOff;

        [Header("Extras")]
        public AudioClip turboSpool;
        public AudioClip blowOff;
        public AudioClip shift;
        public AudioClip limiter;
        public AudioClip exhaustPop;

        [Header("Pitch range across the rev range")]
        public float minPitch = 0.7f;
        public float maxPitch = 1.7f;

        public AudioMixerGroup output;
    }
}
