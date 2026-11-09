using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sim.Mobile
{
    // Watches frame time and moves between quality steps so slow phones stay smooth
    // and fast phones get the best look. Reads and writes only render scale, shadow
    // distance and a traffic density callback, so it works with any scene.
    public class AdaptiveQuality : MonoBehaviour
    {
        [System.Serializable]
        public struct Step
        {
            public float renderScale;
            public float shadowDistance;
            public float trafficDensity;
        }

        public int targetFps = 30;
        public Step[] steps =
        {
            new Step { renderScale = 0.6f,  shadowDistance = 20f, trafficDensity = 0.4f },
            new Step { renderScale = 0.75f, shadowDistance = 35f, trafficDensity = 0.6f },
            new Step { renderScale = 0.9f,  shadowDistance = 50f, trafficDensity = 0.85f },
            new Step { renderScale = 1.0f,  shadowDistance = 70f, trafficDensity = 1f },
        };
        public int startStep = 2;
        public float sampleSeconds = 3f;
        public float upgradeAfterSeconds = 12f;
        public System.Action<float> OnTrafficDensity;

        int step;
        float acc, frames, goodTime;

        void Start()
        {
            if (steps == null || steps.Length == 0) { enabled = false; return; }
            step = Mathf.Clamp(startStep, 0, steps.Length - 1);
            Application.targetFrameRate = targetFps;
            Apply();
        }

        void Update()
        {
            acc += Time.unscaledDeltaTime;
            frames++;
            if (acc < sampleSeconds) return;

            float avg = acc / frames;
            float target = 1f / targetFps;
            acc = 0f; frames = 0f;

            if (avg > target * 1.2f && step > 0) { step--; goodTime = 0f; Apply(); }
            else if (avg < target * 0.8f && step < steps.Length - 1)
            {
                goodTime += sampleSeconds;
                if (goodTime >= upgradeAfterSeconds) { step++; goodTime = 0f; Apply(); }
            }
            else goodTime = 0f;
        }

        void Apply()
        {
            var s = steps[step];
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp) urp.renderScale = s.renderScale;
            QualitySettings.shadowDistance = s.shadowDistance;
            OnTrafficDensity?.Invoke(s.trafficDensity);
        }
    }
}
