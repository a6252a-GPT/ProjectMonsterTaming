using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    public static class FallenCommanderCorruptionRingEffectPlayer
    {
        public static void Play(
            FallenCommanderAttackEffectData effects,
            Vector3 center,
            Vector3 direction,
            Transform parent,
            Transform boss,
            Transform commander,
            float safeRadius,
            float outerRadius)
        {
            var safeOuterRadius = Mathf.Max(0.1f, outerRadius);
            var instance = FallenCommanderAttackEffectPlayer.PlayResolveVfx(
                effects,
                center,
                direction,
                parent,
                boss,
                commander,
                areaScale: Vector3.one * safeOuterRadius);
            if (instance != null &&
                instance.TryGetComponent<FallenCommanderRingVfxView>(out var ringView))
            {
                ringView.Configure(safeRadius, safeOuterRadius);
            }

            FallenCommanderAttackEffectPlayer.PlayResolveSfx(
                effects,
                center,
                direction,
                boss,
                commander);
        }
    }
}
