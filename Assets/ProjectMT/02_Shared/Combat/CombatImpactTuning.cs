using ProjectMT.Shared.Unit;

namespace ProjectMT.Shared.Combat
{
    public readonly struct CombatImpactPreset // 판정과 분리된 명중 표현값
    {
        public CombatImpactPreset(
            float targetHitStop,
            float attackerHitStop,
            float recoilDistance,
            float recoilHeight,
            float recoilDuration,
            float attackerLungeDistance,
            float attackerLungeDuration,
            float cameraImpulse)
        {
            TargetHitStop = targetHitStop;
            AttackerHitStop = attackerHitStop;
            RecoilDistance = recoilDistance;
            RecoilHeight = recoilHeight;
            RecoilDuration = recoilDuration;
            AttackerLungeDistance = attackerLungeDistance;
            AttackerLungeDuration = attackerLungeDuration;
            CameraImpulse = cameraImpulse;
        }

        public float TargetHitStop { get; }
        public float AttackerHitStop { get; }
        public float RecoilDistance { get; }
        public float RecoilHeight { get; }
        public float RecoilDuration { get; }
        public float AttackerLungeDistance { get; }
        public float AttackerLungeDuration { get; }
        public float CameraImpulse { get; }
    }

    public static class CombatImpactTuning // 다수전에서도 과하지 않은 3단계 프리셋
    {
        private static CombatTuningConfig configured;

        public static CombatTuningConfig ActiveConfig => configured ?? CombatTuningConfig.RuntimeDefault;
        public static float MaximumHitStop => ActiveConfig.MaximumHitStop;

        public static void Configure(CombatTuningConfig config)
        {
            configured = config != null && config.TryValidate(out _)
                ? config
                : CombatTuningConfig.RuntimeDefault;
        }

        public static CombatImpactPreset Resolve(
            MonsterImpactStrength strength,
            MonsterReactionWeight reactionWeight,
            bool ranged,
            bool critical,
            bool killed)
        {
            var tuning = ActiveConfig;
            var preset = tuning.ResolveBaseImpactPreset(strength, ranged);
            var recoilMultiplier = tuning.ResolveReactionDistanceMultiplier(reactionWeight);
            var durationMultiplier = tuning.ResolveReactionDurationMultiplier(reactionWeight);
            var emphasis = critical || killed ? tuning.CriticalOrKillEmphasis : 1f;
            var targetStop = critical || killed
                ? System.Math.Min(
                    MaximumHitStop,
                    preset.TargetHitStop * tuning.CriticalOrKillTargetStopMultiplier)
                : preset.TargetHitStop;

            return new CombatImpactPreset(
                targetStop,
                preset.AttackerHitStop,
                preset.RecoilDistance * recoilMultiplier * emphasis,
                preset.RecoilHeight * recoilMultiplier * emphasis,
                preset.RecoilDuration * durationMultiplier,
                preset.AttackerLungeDistance * emphasis,
                preset.AttackerLungeDuration,
                preset.CameraImpulse * emphasis);
        }
    }
}
