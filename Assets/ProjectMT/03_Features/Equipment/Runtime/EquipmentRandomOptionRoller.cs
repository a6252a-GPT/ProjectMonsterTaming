using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;

namespace ProjectMT.Features.Equipment
{
    // 신규 장비의 추가 옵션은 그룹 가중치로 뽑고 같은 장비 안에서는 중복하지 않는다.
    public static class EquipmentRandomOptionRoller
    {
        public static List<EquipmentOptionRollData> Roll(EquipmentGrade grade, int itemLevel, Random random)
        {
            return Roll(grade, itemLevel, EquipmentBalanceConfig.RuntimeDefault, random);
        }

        public static List<EquipmentOptionRollData> Roll(
            EquipmentGrade grade,
            int itemLevel,
            EquipmentBalanceConfig balance,
            Random random)
        {
            if (balance == null)
            {
                throw new ArgumentNullException(nameof(balance));
            }

            if (!balance.TryValidate(out var error)) throw new ArgumentException(error, nameof(balance));
            if (itemLevel < 1 || itemLevel > balance.MaximumItemLevel)
                throw new ArgumentOutOfRangeException(nameof(itemLevel));
            if (!Enum.IsDefined(typeof(EquipmentGrade), grade)) throw new ArgumentOutOfRangeException(nameof(grade));
            var rng = random ?? new Random();
            var optionCount = balance.GetRandomOptionCount(grade);
            var gradeMultiplier = balance.GetRandomOptionGradeMultiplier(grade);
            var pickedTypes = new HashSet<EquipmentOptionType>();
            var result = new List<EquipmentOptionRollData>(optionCount);

            for (var slot = 0; slot < optionCount; slot++)
            {
                var type = PickWeightedType(pickedTypes, balance, rng);
                pickedTypes.Add(type);

                var randomMultiplier = balance.MinimumRandomMultiplier + (float)rng.NextDouble() *
                    (balance.MaximumRandomMultiplier - balance.MinimumRandomMultiplier);
                var levelMultiplier = EquipmentLevelRules.GetMultiplier(itemLevel, balance.GetOptionGrowthPerLevel(type));
                var value = EquipmentOptionInfo.GetBaseValue(type, balance) * gradeMultiplier * levelMultiplier * randomMultiplier;
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                    throw new InvalidOperationException("Equipment option value is invalid.");
                result.Add(new EquipmentOptionRollData(type, value));
            }

            return result;
        }

        private static EquipmentOptionType PickWeightedType(
            HashSet<EquipmentOptionType> pickedTypes,
            EquipmentBalanceConfig balance,
            Random rng)
        {
            var types = EquipmentOptionInfo.AllTypes;
            var weights = new float[types.Length];
            var totalWeight = 0f;
            for (var i = 0; i < types.Length; i++)
            {
                weights[i] = pickedTypes.Contains(types[i]) ? 0f : balance.GetOptionWeight(types[i]);
                totalWeight += weights[i];
            }

            if (totalWeight <= 0f)
            {
                throw new InvalidOperationException("No equipment option can be selected.");
            }

            var roll = (float)rng.NextDouble() * totalWeight;
            var accumulated = 0f;
            for (var i = 0; i < types.Length; i++)
            {
                accumulated += weights[i];
                if (roll < accumulated)
                {
                    return types[i];
                }
            }

            return types[types.Length - 1];
        }
    }
}
