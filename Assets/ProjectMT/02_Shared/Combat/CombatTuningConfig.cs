using System;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    [Serializable]
    public sealed class CombatImpactPresetData // 표 편집용 명중 반응 한 행
    {
        [SerializeField, Min(0f)] private float targetHitStop;
        [SerializeField, Min(0f)] private float attackerHitStop;
        [SerializeField, Min(0f)] private float recoilDistance;
        [SerializeField, Min(0f)] private float recoilHeight;
        [SerializeField, Min(0.01f)] private float recoilDuration = 0.1f;
        [SerializeField, Min(0f)] private float attackerLungeDistance;
        [SerializeField, Min(0.01f)] private float attackerLungeDuration = 0.1f;
        [SerializeField, Min(0f)] private float cameraImpulse;

        public CombatImpactPresetData()
        {
        }

        public CombatImpactPresetData(
            float targetStop,
            float attackerStop,
            float recoil,
            float height,
            float recoilSeconds,
            float lunge,
            float lungeSeconds,
            float camera)
        {
            targetHitStop = targetStop;
            attackerHitStop = attackerStop;
            recoilDistance = recoil;
            recoilHeight = height;
            recoilDuration = recoilSeconds;
            attackerLungeDistance = lunge;
            attackerLungeDuration = lungeSeconds;
            cameraImpulse = camera;
        }

        public CombatImpactPreset ToRuntime()
        {
            return new CombatImpactPreset(
                Mathf.Max(0f, targetHitStop),
                Mathf.Max(0f, attackerHitStop),
                Mathf.Max(0f, recoilDistance),
                Mathf.Max(0f, recoilHeight),
                Mathf.Max(0.01f, recoilDuration),
                Mathf.Max(0f, attackerLungeDistance),
                Mathf.Max(0.01f, attackerLungeDuration),
                Mathf.Max(0f, cameraImpulse));
        }

        public bool TryValidate(out string error)
        {
            if (targetHitStop < 0f || attackerHitStop < 0f || recoilDistance < 0f ||
                recoilHeight < 0f || recoilDuration <= 0f || attackerLungeDistance < 0f ||
                attackerLungeDuration <= 0f || cameraImpulse < 0f)
            {
                error = "Impact preset contains a negative value or zero duration.";
                return false;
            }

            error = null;
            return true;
        }
    }

    [CreateAssetMenu(
        menuName = "ProjectMT/Combat/Combat Tuning Config",
        fileName = "CombatTuningConfig")]
    public sealed class CombatTuningConfig : ScriptableObject // 공용 타격감·MainBattle 실제 넉백·간격 튜닝 원본
    {
        private static CombatTuningConfig runtimeDefault;

        [Header("근접 타격감")]
        [SerializeField] private CombatImpactPresetData meleeLight = CreateMeleeLight();
        [SerializeField] private CombatImpactPresetData meleeStandard = CreateMeleeStandard();
        [SerializeField] private CombatImpactPresetData meleeHeavy = CreateMeleeHeavy();

        [Header("원거리 명중 타격감")]
        [SerializeField] private CombatImpactPresetData rangedLight = CreateRangedLight();
        [SerializeField] private CombatImpactPresetData rangedStandard = CreateRangedStandard();
        [SerializeField] private CombatImpactPresetData rangedHeavy = CreateRangedHeavy();

        [Header("피격 체급 배율")]
        [SerializeField, Min(0.1f)] private float lightReactionDistanceMultiplier = 1.18f;
        [SerializeField, Min(0.1f)] private float standardReactionDistanceMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float heavyReactionDistanceMultiplier = 0.68f;
        [SerializeField, Min(0.1f)] private float lightReactionDurationMultiplier = 0.92f;
        [SerializeField, Min(0.1f)] private float standardReactionDurationMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float heavyReactionDurationMultiplier = 1.08f;

        [Header("강조·상한")]
        [SerializeField, Min(1f)] private float criticalOrKillEmphasis = 1.12f;
        [SerializeField, Min(1f)] private float criticalOrKillTargetStopMultiplier = 1.16f;
        [SerializeField, Range(0.01f, 0.12f)] private float maximumHitStop = 0.06f;

        [Header("MainBattle 실시간 전투 간격")]
        [SerializeField, Range(0.75f, 3f)] private float mainBattleEnemySpawnSpreadMultiplier = 2.2624435f; // 최종 이웃 간격 2.5m
        [SerializeField, Range(0.4f, 3f)] private float mainBattlePlayerPairDistance = 0.85f;
        [SerializeField, Range(0.4f, 3f)] private float mainBattleEnemyPairDistance = 0.8f;
        [SerializeField, Range(0.4f, 2f)] private float mainBattleOpposingPairDistance = 0.65f;
        [SerializeField, Range(0f, 8f)] private float mainBattlePairSeparationSpeed = 0.65f;
        [SerializeField, Range(0f, 5f)] private float mainBattleUnitCorrectionSpeed = 0.28f;

        [Header("MainBattle 실제 피격 넉백")]
        [SerializeField, Range(0f, 1.5f)] private float mainBattleActualKnockbackDistanceMultiplier = 0.65f;
        [SerializeField, Range(0f, 0.6f)] private float mainBattleActualKnockbackMaxDistance = 0.34f;
        [SerializeField, Range(0.25f, 1.5f)] private float mainBattleActualKnockbackDurationMultiplier = 0.72f;
        [SerializeField, Range(0f, 0.3f)] private float mainBattleLightPostKnockbackStagger = 0.06f;
        [SerializeField, Range(0f, 0.3f)] private float mainBattleStandardPostKnockbackStagger = 0.1f;
        [SerializeField, Range(0f, 0.3f)] private float mainBattleHeavyPostKnockbackStagger = 0.15f;

        [Header("MainBattle 일반 적 거리 AI")]
        [SerializeField, Range(0.2f, 1f)] private float enemyMeleePreferredRangeRatio = 0.72f;
        [SerializeField, Range(0f, 0.95f)] private float enemyMeleeRetreatRangeRatio;
        [SerializeField, Range(0.08f, 1f)] private float enemyMeleeRetargetInterval = 0.18f;
        [SerializeField, Min(0f)] private float enemyMeleeTargetLoadPenalty = 0.9f;
        [SerializeField, Range(0.2f, 1f)] private float enemyRangedPreferredRangeRatio = 0.9f;
        [SerializeField, Range(0f, 0.95f)] private float enemyRangedRetreatRangeRatio = 0.38f;
        [SerializeField, Range(0.08f, 1f)] private float enemyRangedRetargetInterval = 0.28f;
        [SerializeField, Min(0f)] private float enemyRangedTargetLoadPenalty = 0.8f;

        public float MaximumHitStop => Mathf.Clamp(maximumHitStop, 0.01f, 0.12f);
        public float CriticalOrKillEmphasis => Mathf.Max(1f, criticalOrKillEmphasis);
        public float CriticalOrKillTargetStopMultiplier => Mathf.Max(1f, criticalOrKillTargetStopMultiplier);
        public float MainBattleEnemySpawnSpreadMultiplier => Mathf.Clamp(mainBattleEnemySpawnSpreadMultiplier, 0.75f, 3f);
        public float MainBattlePlayerPairDistance => Mathf.Clamp(mainBattlePlayerPairDistance, 0.4f, 3f);
        public float MainBattleEnemyPairDistance => Mathf.Clamp(mainBattleEnemyPairDistance, 0.4f, 3f);
        public float MainBattleOpposingPairDistance => Mathf.Clamp(mainBattleOpposingPairDistance, 0.4f, 2f);
        public float MainBattlePairSeparationSpeed => Mathf.Clamp(mainBattlePairSeparationSpeed, 0f, 8f);
        public float MainBattleUnitCorrectionSpeed => Mathf.Clamp(mainBattleUnitCorrectionSpeed, 0f, 5f);
        public float MainBattleActualKnockbackDistanceMultiplier =>
            Mathf.Clamp(mainBattleActualKnockbackDistanceMultiplier, 0f, 1.5f);
        public float MainBattleActualKnockbackMaxDistance =>
            Mathf.Clamp(mainBattleActualKnockbackMaxDistance, 0f, 0.6f);
        public float MainBattleActualKnockbackDurationMultiplier =>
            Mathf.Clamp(mainBattleActualKnockbackDurationMultiplier, 0.25f, 1.5f);
        public float MainBattleLightPostKnockbackStagger =>
            Mathf.Clamp(mainBattleLightPostKnockbackStagger, 0f, 0.3f);
        public float MainBattleStandardPostKnockbackStagger =>
            Mathf.Clamp(mainBattleStandardPostKnockbackStagger, 0f, 0.3f);
        public float MainBattleHeavyPostKnockbackStagger =>
            Mathf.Clamp(mainBattleHeavyPostKnockbackStagger, 0f, 0.3f);

        public static CombatTuningConfig RuntimeDefault
        {
            get
            {
                if (runtimeDefault == null)
                {
                    runtimeDefault = CreateInstance<CombatTuningConfig>();
                    runtimeDefault.name = "CombatTuningConfig_RuntimeDefault";
                    runtimeDefault.hideFlags = HideFlags.HideAndDontSave;
                    runtimeDefault.ResetToDefaults();
                }

                return runtimeDefault;
            }
        }

        public CombatImpactPreset ResolveBaseImpactPreset(MonsterImpactStrength strength, bool ranged)
        {
            var data = ranged
                ? strength switch
                {
                    MonsterImpactStrength.Light => rangedLight,
                    MonsterImpactStrength.Heavy => rangedHeavy,
                    _ => rangedStandard
                }
                : strength switch
                {
                    MonsterImpactStrength.Light => meleeLight,
                    MonsterImpactStrength.Heavy => meleeHeavy,
                    _ => meleeStandard
                };
            var fallback = ranged ? CreateRangedStandard() : CreateMeleeStandard();
            return (data ?? fallback).ToRuntime();
        }

        public float ResolveReactionDistanceMultiplier(MonsterReactionWeight weight)
        {
            return Mathf.Max(0.1f, weight switch
            {
                MonsterReactionWeight.Light => lightReactionDistanceMultiplier,
                MonsterReactionWeight.Heavy => heavyReactionDistanceMultiplier,
                _ => standardReactionDistanceMultiplier
            });
        }

        public float ResolveReactionDurationMultiplier(MonsterReactionWeight weight)
        {
            return Mathf.Max(0.1f, weight switch
            {
                MonsterReactionWeight.Light => lightReactionDurationMultiplier,
                MonsterReactionWeight.Heavy => heavyReactionDurationMultiplier,
                _ => standardReactionDurationMultiplier
            });
        }

        public UnitCombatBehavior CreateMainBattleEnemyBehavior(bool ranged)
        {
            return ranged
                ? new UnitCombatBehavior(
                    UnitTargetPriority.Nearest,
                    enemyRangedPreferredRangeRatio,
                    enemyRangedRetreatRangeRatio,
                    enemyRangedRetargetInterval,
                    enemyRangedTargetLoadPenalty)
                : new UnitCombatBehavior(
                    UnitTargetPriority.Nearest,
                    enemyMeleePreferredRangeRatio,
                    enemyMeleeRetreatRangeRatio,
                    enemyMeleeRetargetInterval,
                    enemyMeleeTargetLoadPenalty);
        }

        public bool TryValidate(out string error)
        {
            var presets = new[]
            {
                meleeLight, meleeStandard, meleeHeavy,
                rangedLight, rangedStandard, rangedHeavy
            };
            for (var index = 0; index < presets.Length; index++)
            {
                if (presets[index] == null)
                {
                    error = $"Combat impact preset is missing. Index={index}.";
                    return false;
                }

                if (!presets[index].TryValidate(out var presetError))
                {
                    error = $"Combat impact preset is invalid. Index={index}. {presetError}";
                    return false;
                }
            }

            if (lightReactionDistanceMultiplier <= 0f || standardReactionDistanceMultiplier <= 0f ||
                heavyReactionDistanceMultiplier <= 0f || lightReactionDurationMultiplier <= 0f ||
                standardReactionDurationMultiplier <= 0f || heavyReactionDurationMultiplier <= 0f ||
                criticalOrKillEmphasis < 1f || criticalOrKillTargetStopMultiplier < 1f ||
                maximumHitStop < 0.01f || maximumHitStop > 0.12f ||
                mainBattleEnemySpawnSpreadMultiplier < 0.75f || mainBattleEnemySpawnSpreadMultiplier > 3f ||
                mainBattlePlayerPairDistance < 0.4f || mainBattlePlayerPairDistance > 3f ||
                mainBattleEnemyPairDistance < 0.4f || mainBattleEnemyPairDistance > 3f ||
                mainBattleOpposingPairDistance < 0.4f || mainBattleOpposingPairDistance > 2f ||
                mainBattlePairSeparationSpeed < 0f || mainBattlePairSeparationSpeed > 8f ||
                mainBattleUnitCorrectionSpeed < 0f || mainBattleUnitCorrectionSpeed > 5f ||
                mainBattleActualKnockbackDistanceMultiplier < 0f ||
                mainBattleActualKnockbackDistanceMultiplier > 1.5f ||
                mainBattleActualKnockbackMaxDistance < 0f || mainBattleActualKnockbackMaxDistance > 0.6f ||
                mainBattleActualKnockbackDurationMultiplier < 0.25f ||
                mainBattleActualKnockbackDurationMultiplier > 1.5f ||
                mainBattleLightPostKnockbackStagger < 0f || mainBattleLightPostKnockbackStagger > 0.3f ||
                mainBattleStandardPostKnockbackStagger < 0f || mainBattleStandardPostKnockbackStagger > 0.3f ||
                mainBattleHeavyPostKnockbackStagger < 0f || mainBattleHeavyPostKnockbackStagger > 0.3f ||
                enemyMeleePreferredRangeRatio < 0.2f || enemyMeleePreferredRangeRatio > 1f ||
                enemyRangedPreferredRangeRatio < 0.2f || enemyRangedPreferredRangeRatio > 1f ||
                enemyMeleeRetreatRangeRatio < 0f || enemyRangedRetreatRangeRatio < 0f ||
                enemyMeleeRetreatRangeRatio >= enemyMeleePreferredRangeRatio ||
                enemyRangedRetreatRangeRatio >= enemyRangedPreferredRangeRatio ||
                enemyMeleeRetargetInterval < 0.08f || enemyMeleeRetargetInterval > 1f ||
                enemyRangedRetargetInterval < 0.08f || enemyRangedRetargetInterval > 1f ||
                enemyMeleeTargetLoadPenalty < 0f || enemyRangedTargetLoadPenalty < 0f)
            {
                error = "Combat reaction, knockback, range, or enemy spacing values are invalid.";
                return false;
            }

            error = null;
            return true;
        }

        public void ResetToDefaults()
        {
            meleeLight = CreateMeleeLight();
            meleeStandard = CreateMeleeStandard();
            meleeHeavy = CreateMeleeHeavy();
            rangedLight = CreateRangedLight();
            rangedStandard = CreateRangedStandard();
            rangedHeavy = CreateRangedHeavy();
            lightReactionDistanceMultiplier = 1.18f;
            standardReactionDistanceMultiplier = 1f;
            heavyReactionDistanceMultiplier = 0.68f;
            lightReactionDurationMultiplier = 0.92f;
            standardReactionDurationMultiplier = 1f;
            heavyReactionDurationMultiplier = 1.08f;
            criticalOrKillEmphasis = 1.12f;
            criticalOrKillTargetStopMultiplier = 1.16f;
            maximumHitStop = 0.06f;
            mainBattleEnemySpawnSpreadMultiplier = 2.2624435f;
            mainBattlePlayerPairDistance = 0.85f;
            mainBattleEnemyPairDistance = 0.8f;
            mainBattleOpposingPairDistance = 0.65f;
            mainBattlePairSeparationSpeed = 0.65f;
            mainBattleUnitCorrectionSpeed = 0.28f;
            mainBattleActualKnockbackDistanceMultiplier = 0.65f;
            mainBattleActualKnockbackMaxDistance = 0.34f;
            mainBattleActualKnockbackDurationMultiplier = 0.72f;
            mainBattleLightPostKnockbackStagger = 0.06f;
            mainBattleStandardPostKnockbackStagger = 0.1f;
            mainBattleHeavyPostKnockbackStagger = 0.15f;
            enemyMeleePreferredRangeRatio = 0.72f;
            enemyMeleeRetreatRangeRatio = 0f;
            enemyMeleeRetargetInterval = 0.18f;
            enemyMeleeTargetLoadPenalty = 0.9f;
            enemyRangedPreferredRangeRatio = 0.9f;
            enemyRangedRetreatRangeRatio = 0.38f;
            enemyRangedRetargetInterval = 0.28f;
            enemyRangedTargetLoadPenalty = 0.8f;
        }

        private static CombatImpactPresetData CreateMeleeLight()
        {
            return new CombatImpactPresetData(0.018f, 0.014f, 0.18f, 0.08f, 0.14f, 0.16f, 0.15f, 0f);
        }

        private static CombatImpactPresetData CreateMeleeStandard()
        {
            return new CombatImpactPresetData(0.028f, 0.022f, 0.30f, 0.13f, 0.17f, 0.26f, 0.18f, 0.024f);
        }

        private static CombatImpactPresetData CreateMeleeHeavy()
        {
            return new CombatImpactPresetData(0.048f, 0.038f, 0.48f, 0.20f, 0.22f, 0.38f, 0.22f, 0.055f);
        }

        private static CombatImpactPresetData CreateRangedLight()
        {
            return new CombatImpactPresetData(0.016f, 0f, 0.14f, 0.06f, 0.13f, 0f, 0.10f, 0f);
        }

        private static CombatImpactPresetData CreateRangedStandard()
        {
            return new CombatImpactPresetData(0.020f, 0f, 0.22f, 0.09f, 0.15f, 0f, 0.11f, 0.012f);
        }

        private static CombatImpactPresetData CreateRangedHeavy()
        {
            return new CombatImpactPresetData(0.035f, 0f, 0.36f, 0.14f, 0.20f, 0f, 0.13f, 0.04f);
        }
    }
}
