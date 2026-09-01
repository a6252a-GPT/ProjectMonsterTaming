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
            var before = EstimatePower(total);
            var after = EstimatePower(new EquipmentLegionBonus
            {
                AttackPower = Replace(total.AttackPower, equipped, candidate, EquipmentStatType.AttackPower),
                MaxHealth = Replace(total.MaxHealth, equipped, candidate, EquipmentStatType.MaxHealth),
                Defense = Replace(total.Defense, equipped, candidate, EquipmentStatType.Defense),
                AttackSpeed = Replace(total.AttackSpeed, equipped, candidate, EquipmentStatType.AttackSpeed),
                MoveSpeed = Replace(total.MoveSpeed, equipped, candidate, EquipmentStatType.MoveSpeed),
                CriticalRate = Replace(total.CriticalRate, equipped, candidate, EquipmentStatType.CriticalRate),
                CriticalDamage = Replace(total.CriticalDamage, equipped, candidate, EquipmentStatType.CriticalDamage),
                SkillDamage = Replace(total.SkillDamage, equipped, candidate, EquipmentStatType.SkillDamage),
                BossDamage = Replace(total.BossDamage, equipped, candidate, EquipmentStatType.BossDamage),
                NormalMonsterDamage = Replace(total.NormalMonsterDamage, equipped, candidate, EquipmentStatType.NormalMonsterDamage),
                SkillCooldownReduction = Replace(total.SkillCooldownReduction, equipped, candidate, EquipmentStatType.SkillCooldownReduction),
                DefensePenetration = Replace(total.DefensePenetration, equipped, candidate, EquipmentStatType.DefensePenetration),
                DamageReduction = Replace(total.DamageReduction, equipped, candidate, EquipmentStatType.DamageReduction)
            });
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

        // 스킬피해/보스피해/일반몬스터피해/방어관통은 상황부 데미지 배율이라 공격력과 동일하게,
        // 스킬 쿨타임 감소·이동속도는 초당 행동 횟수를 늘리는 효과라 공격속도와 동일하게 합산해 근사한다.
        // (실제 UnitStatsSnapshot.EstimatePower는 이 두 그룹 외 스탯을 반영하지 않기 때문)
        private static float EstimatePower(EquipmentLegionBonus stats)
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
