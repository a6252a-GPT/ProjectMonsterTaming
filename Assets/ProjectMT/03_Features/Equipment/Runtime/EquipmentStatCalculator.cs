using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;

namespace ProjectMT.Features.Equipment
{
    public static class EquipmentStatCalculator
    {
        public static List<EquipmentStatContribution> GetCoreContributions(
            EquipmentInstanceData instance, EquipmentBalanceConfig balance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            if (instance.ItemLevel < 1 || !Enum.IsDefined(typeof(EquipmentPart), instance.Part) ||
                !Enum.IsDefined(typeof(EquipmentGrade), instance.Grade))
                throw new ArgumentOutOfRangeException(nameof(instance));
            if (!balance.TryValidate(out var error)) throw new ArgumentException(error, nameof(balance));
            var values = EquipmentGradeStatTable.GetCoreStatContributions(instance.Part, instance.Grade, balance);
            for (var index = 0; index < values.Count; index++)
            {
                var contribution = values[index];
                var primary = contribution.StatType == EquipmentStatType.AttackPower ||
                              contribution.StatType == EquipmentStatType.MaxHealth ||
                              contribution.StatType == EquipmentStatType.Defense;
                var rate = primary ? balance.PrimaryCoreGrowthPerLevel : balance.SecondaryCoreGrowthPerLevel;
                var value = contribution.Value * EquipmentLevelRules.GetMultiplier(instance.ItemLevel, rate);
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new OverflowException("Equipment core value overflow.");
                values[index] = new EquipmentStatContribution(contribution.StatType, value, contribution.IsRelativeToBase);
            }
            return values;
        }

        public static List<EquipmentStatContribution> GetTotalContributions(
            EquipmentInstanceData instance, EquipmentBalanceConfig balance)
        {
            var result = GetCoreContributions(instance, balance);
            foreach (var option in instance.RandomOptions)
            {
                if (option == null) continue;
                result.AddRange(EquipmentOptionInfo.ResolveContributions(option.Type, option.Value)); // 확정값 재배율 금지
            }
            return result;
        }
    }
}
