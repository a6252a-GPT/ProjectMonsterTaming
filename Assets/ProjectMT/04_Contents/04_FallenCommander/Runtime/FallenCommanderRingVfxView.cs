using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    [DisallowMultipleComponent]
    public sealed class FallenCommanderRingVfxView : MonoBehaviour
    {
        [SerializeField] private ParticleSystem[] ringParticles;
        [SerializeField, Min(0.1f)] private float particleSizeMultiplier = 2f;

        private float[] baseStartSizeMultipliers;

        public void Configure(float safeRadius, float outerRadius)
        {
            var safeOuterRatio = Mathf.Clamp01(
                Mathf.Max(0f, safeRadius) / Mathf.Max(0.01f, outerRadius));
            var middleRadius = (1f + safeOuterRatio) * 0.5f;
            var halfThickness = Mathf.Max(0.01f, (1f - safeOuterRatio) * 0.5f);

            var particles = ringParticles == null || ringParticles.Length == 0
                ? GetComponentsInChildren<ParticleSystem>(true)
                : ringParticles;
            CacheBaseStartSizes(particles);
            for (var index = 0; index < particles.Length; index++)
            {
                if (particles[index] == null)
                {
                    continue;
                }

                particles[index].Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles[index].main;
                main.scalingMode = ParticleSystemScalingMode.Shape;
                main.startSizeMultiplier =
                    baseStartSizeMultipliers[index] * particleSizeMultiplier;
                var shape = particles[index].shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Donut;
                shape.radius = middleRadius;
                shape.donutRadius = halfThickness;
                shape.arc = 360f;
                particles[index].Play(true);
            }
        }

        private void CacheBaseStartSizes(ParticleSystem[] particles)
        {
            if (baseStartSizeMultipliers != null &&
                baseStartSizeMultipliers.Length == particles.Length)
            {
                return;
            }

            baseStartSizeMultipliers = new float[particles.Length];
            for (var index = 0; index < particles.Length; index++)
            {
                baseStartSizeMultipliers[index] = particles[index] == null
                    ? 1f
                    : particles[index].main.startSizeMultiplier;
            }
        }
    }
}
