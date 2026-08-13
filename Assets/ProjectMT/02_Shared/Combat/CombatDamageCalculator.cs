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
}
