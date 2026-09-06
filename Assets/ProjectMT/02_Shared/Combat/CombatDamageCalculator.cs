using System;
using ProjectMT.Shared.Stats;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public readonly struct CombatDamageResult
    {
        public CombatDamageResult(float amount, bool isCritical)
        {
            Amount = amount;
            IsCritical = isCritical;
        }

        public float Amount { get; }
        public bool IsCritical { get; }
    }

    public static class CombatDamageCalculator // 치명타·방어·관통·피해감소의 단일 계산 지점
    {
        public static CombatDamageResult Calculate(
            float baseDamage,
            UnitStatsSnapshot attacker,
            UnitStatsSnapshot defender,
            CombatStatConfig config,
            float criticalRoll01)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (baseDamage <= 0f || float.IsNaN(baseDamage) || float.IsInfinity(baseDamage))
            {
                return new CombatDamageResult(0f, false);
            }

            var criticalRate = Mathf.Clamp(attacker.criticalRate, 0f, config.CriticalRateCap);
            var isCritical = Mathf.Clamp01(criticalRoll01) < criticalRate;
            var criticalMultiplier = attacker.criticalDamageMultiplier >= 1f
                ? Mathf.Min(attacker.criticalDamageMultiplier, config.CriticalDamageMultiplierCap)
                : config.BaseCriticalDamageMultiplier;

            var amount = baseDamage * (isCritical ? criticalMultiplier : 1f);
            var penetration = Mathf.Clamp(
                attacker.defensePenetrationRate,
                0f,
                config.DefensePenetrationCap);
            var effectiveDefense = Mathf.Max(0f, defender.defense) * (1f - penetration);
            amount *= config.DefenseK / (config.DefenseK + effectiveDefense);
            amount *= 1f - Mathf.Clamp(
                defender.damageReductionRate,
                0f,
                config.DamageReductionCap);

            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f)
            {
                return new CombatDamageResult(0f, isCritical);
            }

            return new CombatDamageResult(Mathf.Max(config.MinimumDamage, amount), isCritical);
        }
    }

    public static class CombatPowerGrowthWeights // 전투 시뮬레이션이 아닌 종합 성장 지표용 가치 계수
    {
        public const float MoveSpeed = 0.30f;
        public const float AttackRange = 0.25f;
        public const float SkillDamage = 0.50f;
        public const float DefensePenetration = 0.50f;
        public const float SkillCooldownReduction = 0.40f;
        public const float BossDamage = 0.30f;
        public const float NormalMonsterDamage = 0.30f;
        public const float UnlockedAbility = 0.07f;
        public const int MaxCountedAbilities = 2;
    }

    public static class CombatPowerCalculator // 최종 Snapshot의 종합 성장 지표 계산
    {
        public static float Calculate(UnitStatsSnapshot stats, CombatStatConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var expectedDps = CalculateExpectedDps(stats, config);
            var effectiveHealth = CalculateEffectiveHealth(stats, config);
            var power = Mathf.Sqrt(expectedDps * effectiveHealth) * config.CombatPowerDisplayScale;
            return IsFinitePositive(power) ? power : 0f;
        }

        public static float Calculate(
            UnitStatsSnapshot stats,
            CombatPowerGrowthSnapshot growth,
            int unlockedAbilityCount,
            CombatStatConfig config)
        {
            var corePower = Calculate(stats, config);
            if (!IsFinitePositive(corePower))
            {
                return 0f;
            }

            var multiplier = CalculateGrowthMultiplier(stats, growth, unlockedAbilityCount);
            var power = corePower * multiplier;
            return IsFinitePositive(power) ? power : 0f;
        }

        public static float CalculateGrowthMultiplier(
            UnitStatsSnapshot stats,
            CombatPowerGrowthSnapshot growth,
            int unlockedAbilityCount)
        {
            var bonus =
                growth.MoveSpeedGrowthRate * CombatPowerGrowthWeights.MoveSpeed +
                growth.AttackRangeGrowthRate * CombatPowerGrowthWeights.AttackRange +
                SafeRate(stats.skillDamageRate) * CombatPowerGrowthWeights.SkillDamage +
                SafeRate(stats.defensePenetrationRate) * CombatPowerGrowthWeights.DefensePenetration +
                SafeRate(stats.skillCooldownReductionRate) * CombatPowerGrowthWeights.SkillCooldownReduction +
                SafeRate(stats.bossDamageRate) * CombatPowerGrowthWeights.BossDamage +
                SafeRate(stats.normalMonsterDamageRate) * CombatPowerGrowthWeights.NormalMonsterDamage +
                Mathf.Clamp(unlockedAbilityCount, 0, CombatPowerGrowthWeights.MaxCountedAbilities) *
                CombatPowerGrowthWeights.UnlockedAbility;

            return IsFiniteNonNegative(bonus) ? 1f + bonus : 1f;
        }

        public static float CalculateExpectedDps(UnitStatsSnapshot stats, CombatStatConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var criticalRate = Mathf.Clamp(stats.criticalRate, 0f, config.CriticalRateCap);
            var criticalDamage = stats.criticalDamageMultiplier >= 1f
                ? Mathf.Min(stats.criticalDamageMultiplier, config.CriticalDamageMultiplierCap)
                : config.BaseCriticalDamageMultiplier;
            var expectedCriticalMultiplier = 1f + criticalRate * (criticalDamage - 1f);
            var expectedDps = Mathf.Max(0f, stats.damage) /
                              Mathf.Max(0.01f, stats.attackInterval) *
                              expectedCriticalMultiplier;
            return IsFinitePositive(expectedDps) ? expectedDps : 0f;
        }

        public static float CalculateEffectiveHealth(UnitStatsSnapshot stats, CombatStatConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var defenseMultiplier = 1f + Mathf.Max(0f, stats.defense) / config.DefenseK;
            var reduction = Mathf.Clamp(stats.damageReductionRate, 0f, config.DamageReductionCap);
            var effectiveHealth = Mathf.Max(1f, stats.maxHealth) *
                                  defenseMultiplier /
                                  Mathf.Max(0.01f, 1f - reduction);
            return IsFinitePositive(effectiveHealth) ? effectiveHealth : 0f;
        }

        private static float SafeRate(float value) =>
            IsFiniteNonNegative(value) ? value : 0f;

        private static bool IsFiniteNonNegative(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinitePositive(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
