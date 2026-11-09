using UnityEngine;

namespace Sim.Vehicles
{
    // Procedural engine sound, so every car has a distinct engine note with no audio files.
    // If the car's EngineSoundProfile has recorded loops, this component switches itself off
    // and EngineAudio takes over. Add this next to VehicleController.
    [RequireComponent(typeof(VehicleController))]
    public class SynthEngineAudio : MonoBehaviour
    {
        public bool interior;
        public float interiorCutoff = 1800f;
        public float volume = 0.8f;

        VehicleController car;
        EngineSynth synth;
        AudioLowPassFilter lowpass;
        AudioSource src;

        void Start()
        {
            car = GetComponent<VehicleController>();
            if (!car || !car.def) { enabled = false; return; }
            var s = car.def.sound;
            if (s && s.lowOn) { enabled = false; return; }

            int sr = AudioSettings.outputSampleRate;
            synth = new EngineSynth(car.def.layout, car.def.turbo, car.def.supercharged, sr)
            { RedlineRpm = car.def.redlineRpm };

            // A looping dummy clip keeps the source "playing" so OnAudioFilterRead runs.
            src = gameObject.AddComponent<AudioSource>();
            src.clip = AudioClip.Create("synth", 1024, 1, sr, false);
            src.loop = true;
            src.spatialBlend = 1f;
            src.minDistance = 4f; src.maxDistance = 70f;
            src.volume = volume;
            src.Play();
            lowpass = gameObject.AddComponent<AudioLowPassFilter>();
        }

        void Update()
        {
            if (synth == null) return;
            synth.Rpm = car.Engine.Rpm;
            synth.Throttle = car.input.throttle;
            synth.Boost = car.Engine.Boost;
            synth.Limiter = car.Engine.OnLimiter;
            lowpass.cutoffFrequency = interior ? interiorCutoff : 22000f;
        }

        // Runs on the audio thread.
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (synth == null) return;
            synth.Render(data, channels);
        }
    }
}
