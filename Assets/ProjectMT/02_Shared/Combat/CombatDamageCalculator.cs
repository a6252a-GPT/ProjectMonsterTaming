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

    public static class CombatPowerCalculator // 최종 Snapshot의 안내용 전투력 계산
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

        private static bool IsFinitePositive(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
