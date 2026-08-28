using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.Equipment
{
    public static class EquipmentUpgradeEvaluator // 비교창·정렬·자동 장착이 같은 전투력 기준을 공유
    {
        public static int EvaluatePowerDelta(EquipmentItemView candidate)
        {
            if (candidate.Definition == null || string.IsNullOrEmpty(candidate.InstanceId) || candidate.IsEquipped)
            {
                return 0;
            }

            var total = EquipmentLegionBonusCalculator.CalculateTotal();
            EquipmentInventoryRuntime.TryGetEquipped(candidate.Part, out var equipped);
            var before = EstimatePower(total.AttackPower, total.MaxHealth, total.Defense, total.AttackSpeed,
                total.CriticalRate, total.CriticalDamage, total.DamageReduction);
            var after = EstimatePower(
                Replace(total.AttackPower, equipped, candidate, EquipmentStatType.AttackPower),
                Replace(total.MaxHealth, equipped, candidate, EquipmentStatType.MaxHealth),
                Replace(total.Defense, equipped, candidate, EquipmentStatType.Defense),
                Replace(total.AttackSpeed, equipped, candidate, EquipmentStatType.AttackSpeed),
                Replace(total.CriticalRate, equipped, candidate, EquipmentStatType.CriticalRate),
                Replace(total.CriticalDamage, equipped, candidate, EquipmentStatType.CriticalDamage),
                Replace(total.DamageReduction, equipped, candidate, EquipmentStatType.DamageReduction));
            return Mathf.RoundToInt(after - before);
        }

        public static IReadOnlyList<string> GetBestUpgradeInstanceIds()
        {
            var items = EquipmentInventoryRuntime.GetItems();
            var bestByPart = new Dictionary<EquipmentPart, (string instanceId, int delta)>();
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var delta = EvaluatePowerDelta(item);
                if (delta > 0 &&
                    (!bestByPart.TryGetValue(item.Part, out var currentBest) || delta > currentBest.delta))
                {
                    bestByPart[item.Part] = (item.InstanceId, delta);
                }
            }

            var result = new List<string>(bestByPart.Count);
            foreach (EquipmentPart part in Enum.GetValues(typeof(EquipmentPart)))
            {
                if (bestByPart.TryGetValue(part, out var best))
                {
                    result.Add(best.instanceId);
                }
            }

            return result;
        }

        private static float Replace(float total, EquipmentItemView equipped, EquipmentItemView candidate,
            EquipmentStatType statType) => total - GetItemValue(equipped, statType) + GetItemValue(candidate, statType);

        private static float GetItemValue(EquipmentItemView item, EquipmentStatType statType)
        {
            var result = Sum(item.Definition?.CoreStatContributions, statType);
            var options = item.Instance?.RandomOptions;
            if (options == null) return result;
            for (var index = 0; index < options.Count; index++)
            {
                var option = options[index];
                if (option != null) result += Sum(EquipmentOptionInfo.ResolveContributions(option.Type, option.Value), statType);
            }
            return result;
        }

        private static float Sum(IReadOnlyList<EquipmentStatContribution> values, EquipmentStatType statType)
        {
            var result = 0f;
            if (values == null) return result;
            for (var index = 0; index < values.Count; index++)
            {
                if (values[index].StatType == statType) result += values[index].Value;
            }
            return result;
        }

        private static float EstimatePower(float attackPower, float maxHealth, float defense, float attackSpeed,
            float criticalRate, float criticalDamage, float damageReduction)
        {
            return new UnitStatsSnapshot
            {
                damage = 100f * (1f + attackPower / 100f),
                maxHealth = 1000f * (1f + maxHealth / 100f),
                defense = 50f * (1f + defense / 100f),
                attackInterval = 1f / (1f + Mathf.Max(0f, attackSpeed) / 100f),
                criticalRate = 0.05f + criticalRate / 100f,
                criticalDamageMultiplier = 1.5f + criticalDamage / 100f,
                damageReductionRate = Mathf.Clamp01(damageReduction / 100f)
            }.EstimatePower();
        }
    }
}
