using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.Equipment
{
    public static class EquipmentUpgradeEvaluator
    {
        public static int EvaluatePowerDelta(EquipmentItemView candidate)
        {
            return Mathf.RoundToInt(EvaluatePowerDeltaExact(candidate));
        }

        public static float EvaluatePowerDeltaExact(EquipmentItemView candidate)
        {
            if (candidate.Instance == null || candidate.Balance == null || candidate.IsEquipped)
            {
                return 0f;
            }

            EquipmentInventoryRuntime.TryGetEquipped(candidate.Part, out var equipped);
            return EvaluatePowerDelta(
                equipped.Instance,
                candidate.Instance,
                EquipmentLegionBonusCalculator.CalculateTotal(),
                candidate.Balance);
        }

        public static float EvaluatePowerDelta(
            EquipmentInstanceData equipped,
            EquipmentInstanceData candidate,
            EquipmentLegionBonus baselineBonus,
            EquipmentBalanceConfig balance)
        {
            if (candidate == null || balance == null)
            {
                return 0f;
            }

            var equippedValues = equipped == null ? null : EquipmentStatCalculator.GetTotalContributions(equipped, balance);
            var candidateValues = EquipmentStatCalculator.GetTotalContributions(candidate, balance);
            var before = EstimatePower(baselineBonus);
            var after = baselineBonus;
            foreach (EquipmentStatType statType in Enum.GetValues(typeof(EquipmentStatType)))
            {
                Set(
                    ref after,
                    statType,
                    baselineBonus.GetValue(statType) -
                    Sum(equippedValues, statType) +
                    Sum(candidateValues, statType));
            }

            return EstimatePower(after) - before;
        }

        public static IReadOnlyList<string> GetBestUpgradeInstanceIds()
        {
            var items = EquipmentInventoryRuntime.GetItems();
            var bestByPart = new Dictionary<EquipmentPart, (EquipmentItemView item, float delta, int order)>();
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var delta = EvaluatePowerDeltaExact(item);
                if (delta <= 0f)
                {
                    continue;
                }

                if (!bestByPart.TryGetValue(item.Part, out var best) ||
                    IsPreferred(item.Instance, true, index, delta, best.item.Instance, true, best.order, best.delta))
                {
                    bestByPart[item.Part] = (item, delta, index);
                }
            }

            var result = new List<string>(bestByPart.Count);
            foreach (EquipmentPart part in Enum.GetValues(typeof(EquipmentPart)))
            {
                if (bestByPart.TryGetValue(part, out var best))
                {
                    result.Add(best.item.InstanceId);
                }
            }

            return result;
        }

        // 자동분해가 잠금·장착 외에 보존해야 할 부위별 최선 교체품 ID.
        public static HashSet<string> GetBestReplacementInstanceIds(
            EquipmentSaveDataView current,
            IReadOnlyList<EquipmentInstanceData> additions,
            EquipmentBalanceConfig balance,
            EquipmentLegionBonus? baselineBonus = null)
        {
            var protectedIds = new HashSet<string>(StringComparer.Ordinal);
            var baseline = baselineBonus ?? EquipmentLegionBonusCalculator.CalculateEquipmentTotal(current, balance);
            var existing = current.Instances;
            foreach (EquipmentPart part in Enum.GetValues(typeof(EquipmentPart)))
            {
                current.TryGetEquipped(part, out var equipped);
                EquipmentInstanceData best = null;
                var bestIsExisting = false;
                var bestOrder = int.MaxValue;
                var bestDelta = float.NegativeInfinity;

                for (var index = 0; index < existing.Count; index++)
                {
                    var candidate = existing[index];
                    Consider(candidate, true, index);
                }

                var newItems = additions ?? Array.Empty<EquipmentInstanceData>();
                for (var index = 0; index < newItems.Count; index++)
                {
                    Consider(newItems[index], false, index);
                }

                if (best != null)
                {
                    protectedIds.Add(best.InstanceId);
                }

                void Consider(EquipmentInstanceData candidate, bool isExisting, int order)
                {
                    if (candidate == null || candidate.Part != part ||
                        candidate.InstanceId == equipped?.InstanceId)
                    {
                        return;
                    }

                    var delta = EvaluatePowerDelta(equipped, candidate, baseline, balance);
                    if (equipped != null && delta <= 0f)
                    {
                        return;
                    }

                    if (best == null ||
                        IsPreferred(
                            candidate,
                            isExisting,
                            order,
                            delta,
                            best,
                            bestIsExisting,
                            bestOrder,
                            bestDelta))
                    {
                        best = candidate;
                        bestIsExisting = isExisting;
                        bestOrder = order;
                        bestDelta = delta;
                    }
                }
            }

            return protectedIds;
        }

        private static bool IsPreferred(
            EquipmentInstanceData candidate,
            bool candidateIsExisting,
            int candidateOrder,
            float candidateDelta,
            EquipmentInstanceData current,
            bool currentIsExisting,
            int currentOrder,
            float currentDelta)
        {
            if (candidateDelta != currentDelta)
            {
                return candidateDelta > currentDelta;
            }

            if (candidateIsExisting != currentIsExisting)
            {
                return candidateIsExisting;
            }

            if (candidate.Grade != current.Grade)
            {
                return candidate.Grade > current.Grade;
            }

            if (candidate.ItemLevel != current.ItemLevel)
            {
                return candidate.ItemLevel > current.ItemLevel;
            }

            return candidateOrder < currentOrder;
        }

        private static float Sum(
            IReadOnlyList<EquipmentStatContribution> values,
            EquipmentStatType statType)
        {
            var result = 0f;
            if (values == null)
            {
                return result;
            }

            for (var index = 0; index < values.Count; index++)
            {
                if (values[index].StatType == statType)
                {
                    result += values[index].Value;
                }
            }

            return result;
        }

        private static void Set(ref EquipmentLegionBonus target, EquipmentStatType statType, float value)
        {
            switch (statType)
            {
                case EquipmentStatType.AttackPower: target.AttackPower = value; break;
                case EquipmentStatType.MaxHealth: target.MaxHealth = value; break;
                case EquipmentStatType.Defense: target.Defense = value; break;
                case EquipmentStatType.AttackSpeed: target.AttackSpeed = value; break;
                case EquipmentStatType.MoveSpeed: target.MoveSpeed = value; break;
                case EquipmentStatType.CriticalRate: target.CriticalRate = value; break;
                case EquipmentStatType.CriticalDamage: target.CriticalDamage = value; break;
                case EquipmentStatType.SkillDamage: target.SkillDamage = value; break;
                case EquipmentStatType.BossDamage: target.BossDamage = value; break;
                case EquipmentStatType.NormalMonsterDamage: target.NormalMonsterDamage = value; break;
                case EquipmentStatType.SkillCooldownReduction: target.SkillCooldownReduction = value; break;
                case EquipmentStatType.DefensePenetration: target.DefensePenetration = value; break;
                case EquipmentStatType.DamageReduction: target.DamageReduction = value; break;
            }
        }

        internal static float EstimatePower(EquipmentLegionBonus stats)
        {
            var damageBonusPercent = stats.AttackPower + stats.SkillDamage + stats.BossDamage +
                                     stats.NormalMonsterDamage + stats.DefensePenetration;
            var attackSpeedBonusPercent = stats.AttackSpeed + stats.SkillCooldownReduction + stats.MoveSpeed;
            return new UnitStatsSnapshot
            {
                damage = 100f * (1f + damageBonusPercent / 100f),
                maxHealth = 1000f * (1f + stats.MaxHealth / 100f),
                defense = 50f * (1f + stats.Defense / 100f),
                attackInterval = 1f / (1f + Mathf.Max(0f, attackSpeedBonusPercent) / 100f),
                criticalRate = 0.05f + stats.CriticalRate / 100f,
                criticalDamageMultiplier = 1.5f + stats.CriticalDamage / 100f,
                damageReductionRate = Mathf.Clamp01(stats.DamageReduction / 100f)
            }.EstimatePower();
        }
    }
}
