using UnityEngine;

namespace Sim.Vehicles
{
    // Put on every renderer that should take the car's paint (body, mirrors, bumpers).
    // Uses a MaterialPropertyBlock so materials stay shared and batched.
    public class PaintTarget : MonoBehaviour
    {
        public Renderer target;
        public int materialIndex;

        static readonly int Base = Shader.PropertyToID("_BaseColor");
        static readonly int Metal = Shader.PropertyToID("_Metallic");
        static readonly int Smooth = Shader.PropertyToID("_Smoothness");

        MaterialPropertyBlock block;

        void Reset() => target = GetComponent<Renderer>();

        public void Apply(Color color, float metallic, float smoothness)
        {
            if (!target) return;
            block ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(block, materialIndex);
            block.SetColor(Base, color);
            block.SetFloat(Metal, metallic);
            block.SetFloat(Smooth, smoothness);
            target.SetPropertyBlock(block, materialIndex);
        }
    }
}
