using ProjectMT.Shared.Unit;

namespace ProjectMT.Shared.Combat
{
    public readonly struct CombatImpactPreset // 판정과 분리된 명중 표현값
    {
        public CombatImpactPreset(
            float targetHitStop,
            float attackerHitStop,
            float recoilDistance,
            float recoilDuration,
            float cameraImpulse)
        {
            TargetHitStop = targetHitStop;
            AttackerHitStop = attackerHitStop;
            RecoilDistance = recoilDistance;
            RecoilDuration = recoilDuration;
            CameraImpulse = cameraImpulse;
        }

        public float TargetHitStop { get; }
        public float AttackerHitStop { get; }
        public float RecoilDistance { get; }
        public float RecoilDuration { get; }
        public float CameraImpulse { get; }
    }

    public static class CombatImpactTuning // 다수전에서도 과하지 않은 3단계 프리셋
    {
        public const float MaximumHitStop = 0.06f;

        public static CombatImpactPreset Resolve(
            MonsterImpactStrength strength,
            MonsterReactionWeight reactionWeight,
            bool ranged,
            bool critical,
            bool killed)
        {
            CombatImpactPreset preset;
            if (ranged)
            {
                preset = strength switch
                {
                    MonsterImpactStrength.Light => new CombatImpactPreset(0.016f, 0f, 0.07f, 0.10f, 0f),
                    MonsterImpactStrength.Heavy => new CombatImpactPreset(0.035f, 0f, 0.18f, 0.15f, 0.04f),
                    _ => new CombatImpactPreset(0.020f, 0f, 0.10f, 0.11f, 0.012f)
                };
            }
            else
            {
                preset = strength switch
                {
                    MonsterImpactStrength.Light => new CombatImpactPreset(0.018f, 0.014f, 0.08f, 0.10f, 0f),
                    MonsterImpactStrength.Heavy => new CombatImpactPreset(0.048f, 0.038f, 0.24f, 0.17f, 0.055f),
                    _ => new CombatImpactPreset(0.028f, 0.022f, 0.14f, 0.13f, 0.024f)
                };
            }

            var recoilMultiplier = reactionWeight switch
            {
                MonsterReactionWeight.Light => 1.18f,
                MonsterReactionWeight.Heavy => 0.68f,
                _ => 1f
            };
            var durationMultiplier = reactionWeight switch
            {
                MonsterReactionWeight.Light => 0.92f,
                MonsterReactionWeight.Heavy => 1.08f,
                _ => 1f
            };
            var emphasis = critical || killed ? 1.12f : 1f;
            var targetStop = critical || killed
                ? System.Math.Min(MaximumHitStop, preset.TargetHitStop * 1.16f)
                : preset.TargetHitStop;

            return new CombatImpactPreset(
                targetStop,
                preset.AttackerHitStop,
                preset.RecoilDistance * recoilMultiplier * emphasis,
                preset.RecoilDuration * durationMultiplier,
                preset.CameraImpulse * emphasis);
        }
    }
}
