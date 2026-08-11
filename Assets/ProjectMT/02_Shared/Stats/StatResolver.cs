using System;
using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Stats
{
    public static class StatResolver // 모든 영구 성장 출처를 전투 Snapshot으로 해석
    {
        private static readonly int StatCount = Enum.GetValues(typeof(StatId)).Length;

        public static UnitStatsSnapshot Resolve(
            UnitStatsSnapshot baseStats,
            IReadOnlyList<StatModifier> modifiers,
            CombatStatConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            var flat = new float[StatCount];
            var additiveRate = new float[StatCount];
            var finalMultiplier = new float[StatCount];
            for (var index = 0; index < finalMultiplier.Length; index++)
            {
                finalMultiplier[index] = 1f;
            }

            if (modifiers != null)
            {
                for (var index = 0; index < modifiers.Count; index++)
                {
                    var modifier = modifiers[index];
                    if (!modifier.IsValid)
                    {
                        continue;
                    }

                    var statIndex = (int)modifier.StatId;
                    switch (modifier.Operation)
                    {
                        case StatOperation.Flat:
                            flat[statIndex] += modifier.Value;
                            break;
                        case StatOperation.AdditiveRate:
                            additiveRate[statIndex] += modifier.Value;
                            break;
                        case StatOperation.FinalMultiplier:
                            finalMultiplier[statIndex] *= modifier.Value;
                            break;
                    }
                }
            }

            var baseAttackSpeed = 1f / Mathf.Max(0.01f, baseStats.attackInterval);
            var attackSpeed = ResolveValue(
                baseAttackSpeed,
                StatId.AttackSpeed,
                flat,
                additiveRate,
                finalMultiplier);
            attackSpeed = Mathf.Clamp(
                attackSpeed,
                0.01f,
                baseAttackSpeed * (1f + config.AttackSpeedBonusRateCap));

            var moveSpeed = ResolveValue(
                baseStats.moveSpeed,
                StatId.MoveSpeed,
                flat,
                additiveRate,
                finalMultiplier);
            moveSpeed = Mathf.Clamp(
                moveSpeed,
                0f,
                baseStats.moveSpeed * (1f + config.MoveSpeedBonusRateCap));

            var attackRange = ResolveValue(
                baseStats.attackRange,
                StatId.AttackRange,
                flat,
                additiveRate,
                finalMultiplier);
            attackRange = Mathf.Clamp(
                attackRange,
                0.01f,
                baseStats.attackRange * (1f + config.AttackRangeBonusRateCap));

            return new UnitStatsSnapshot
            {
                maxHealth = Mathf.Max(1f, ResolveValue(
                    baseStats.maxHealth,
                    StatId.MaxHealth,
                    flat,
                    additiveRate,
                    finalMultiplier)),
                damage = Mathf.Max(0f, ResolveValue(
                    baseStats.damage,
                    StatId.AttackPower,
                    flat,
                    additiveRate,
                    finalMultiplier)),
                defense = Mathf.Max(0f, ResolveValue(
                    baseStats.defense,
                    StatId.Defense,
                    flat,
                    additiveRate,
                    finalMultiplier)),
                moveSpeed = moveSpeed,
                attackRange = attackRange,
                attackInterval = 1f / attackSpeed,
                projectileSpeed = baseStats.projectileSpeed,
                ranged = baseStats.ranged,
                criticalRate = Mathf.Clamp(
                    ResolveValue(baseStats.criticalRate, StatId.CriticalRate, flat, additiveRate, finalMultiplier),
                    0f,
                    config.CriticalRateCap),
                criticalDamageMultiplier = Mathf.Clamp(
                    ResolveValue(
                        baseStats.criticalDamageMultiplier,
                        StatId.CriticalDamage,
                        flat,
                        additiveRate,
                        finalMultiplier),
                    1f,
                    config.CriticalDamageMultiplierCap),
                skillDamageRate = Mathf.Max(0f, ResolveValue(
                    baseStats.skillDamageRate,
                    StatId.SkillDamage,
                    flat,
                    additiveRate,
                    finalMultiplier)),
                bossDamageRate = Mathf.Max(0f, ResolveValue(
                    baseStats.bossDamageRate,
                    StatId.BossDamage,
                    flat,
                    additiveRate,
                    finalMultiplier)),
                normalMonsterDamageRate = Mathf.Max(0f, ResolveValue(
                    baseStats.normalMonsterDamageRate,
                    StatId.NormalMonsterDamage,
                    flat,
                    additiveRate,
                    finalMultiplier)),
                skillCooldownReductionRate = Mathf.Clamp(
                    ResolveValue(
                        baseStats.skillCooldownReductionRate,
                        StatId.SkillCooldownReduction,
                        flat,
                        additiveRate,
                        finalMultiplier),
                    0f,
                    config.SkillCooldownReductionCap),
                defensePenetrationRate = Mathf.Clamp(
                    ResolveValue(
                        baseStats.defensePenetrationRate,
                        StatId.DefensePenetration,
                        flat,
                        additiveRate,
                        finalMultiplier),
                    0f,
                    config.DefensePenetrationCap),
                damageReductionRate = Mathf.Clamp(
                    ResolveValue(
                        baseStats.damageReductionRate,
                        StatId.DamageReduction,
                        flat,
                        additiveRate,
                        finalMultiplier),
                    0f,
                    config.DamageReductionCap)
            };
        }

        private static float ResolveValue(
            float baseValue,
            StatId statId,
            float[] flat,
            float[] additiveRate,
            float[] finalMultiplier)
        {
            var index = (int)statId;
            return (baseValue + flat[index]) * (1f + additiveRate[index]) * finalMultiplier[index];
        }
    }
}
