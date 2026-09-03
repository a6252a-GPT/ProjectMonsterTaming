using System;
using UnityEngine;

namespace ProjectMT.Shared.Equipment
{
    public enum EquipmentOptionGroup
    {
        Core,
        Offense,
        UtilityDefense
    }

    [CreateAssetMenu(menuName = "ProjectMT/Equipment/Equipment Balance Config", fileName = "EquipmentBalanceConfig")]
    public sealed class EquipmentBalanceConfig : ScriptableObject // 장비 드랍·고정값·추가 옵션의 단일 밸런스 원본
    {
        private static EquipmentBalanceConfig runtimeDefault;

        private const int GradeCount = 5;
        private const int OptionTypeCount = 13;

        [Header("장비 레벨")]
        [SerializeField, Min(1)] private int maximumItemLevel = 200;
        [SerializeField] private int[] itemLevelOffsets = { 0, -1, -2 };
        [SerializeField] private float[] itemLevelWeights = { 50f, 30f, 20f };
        [SerializeField, Min(0f)] private float primaryCoreGrowthPerLevel = 0.03f;
        [SerializeField, Min(0f)] private float secondaryCoreGrowthPerLevel = 0.003f;
        [SerializeField] private float[] optionGrowthPerLevel =
        {
            0.03f, 0.03f, 0.03f, 0.003f, 0.003f, 0.003f, 0.003f,
            0.01f, 0.01f, 0.01f, 0.003f, 0.003f, 0.003f
        };

        public int MaximumItemLevel => maximumItemLevel;
        public int LevelOutcomeCount => itemLevelOffsets?.Length ?? 0;
        public int GetLevelOffset(int index) => itemLevelOffsets[index];
        public float GetLevelWeight(int index) => itemLevelWeights[index];
        public float PrimaryCoreGrowthPerLevel => primaryCoreGrowthPerLevel;
        public float SecondaryCoreGrowthPerLevel => secondaryCoreGrowthPerLevel;

        public float GetOptionGrowthPerLevel(EquipmentOptionType type)
        {
            if (!Enum.IsDefined(typeof(EquipmentOptionType), type))
                throw new ArgumentOutOfRangeException(nameof(type));
            return optionGrowthPerLevel[(int)type];
        }

        [Header("등급")]
        [SerializeField] private float[] dropWeights = { 68f, 20f, 8f, 3f, 1f };
        [SerializeField] private float[] coreStatBudgetPercent = { 3f, 5f, 8f, 12f, 18f };
        [SerializeField] private float[] randomOptionGradeMultiplier = { 1f, 1.5f, 2.2f, 3.2f, 4.5f };
        [SerializeField] private int[] randomOptionCount = { 1, 1, 2, 3, 4 };

        [Header("장갑·장신구 고정 능력치")]
        [SerializeField] private float[] gloveCriticalRatePercent = { 1f, 2f, 3f, 4f, 5f };
        [SerializeField] private float[] gloveCriticalDamagePercent = { 5f, 10f, 15f, 20f, 25f };
        [SerializeField] private float[] ringAttackSpeedPercent = { 2f, 4f, 6f, 8f, 10f };
        [SerializeField] private float[] ringMoveSpeedPercent = { 1f, 2f, 3f, 4f, 5f };

        [Header("랜덤 추가 옵션")]
        [SerializeField] private float[] optionBaseValuesPercent =
        {
            2f, 2f, 2f, 1f, 0.5f, 1f, 5f, 2f, 2f, 2f, 1f, 2f, 1f
        };
        [SerializeField, Min(0f)] private float coreGroupWeight = 50f;
        [SerializeField, Min(0f)] private float offenseGroupWeight = 30f;
        [SerializeField, Min(0f)] private float utilityDefenseGroupWeight = 20f;
        [SerializeField, Range(0f, 1f)] private float minimumRandomMultiplier = 0.8f;
        [SerializeField, Min(1f)] private float maximumRandomMultiplier = 1.2f;

        public float MinimumRandomMultiplier => Mathf.Clamp01(minimumRandomMultiplier);
        public float MaximumRandomMultiplier => Mathf.Max(MinimumRandomMultiplier, maximumRandomMultiplier);

        public static EquipmentBalanceConfig RuntimeDefault
        {
            get
            {
                if (runtimeDefault == null)
                {
                    runtimeDefault = CreateInstance<EquipmentBalanceConfig>();
                    runtimeDefault.hideFlags = HideFlags.HideAndDontSave;
                }

                return runtimeDefault;
            }
        }

        public float GetDropWeight(EquipmentGrade grade) => GetGradeValue(dropWeights, grade);
        public float GetCoreStatBudgetPercent(EquipmentGrade grade) => GetGradeValue(coreStatBudgetPercent, grade);
        public float GetRandomOptionGradeMultiplier(EquipmentGrade grade) =>
            Mathf.Max(0f, GetGradeValue(randomOptionGradeMultiplier, grade));
        public int GetRandomOptionCount(EquipmentGrade grade) =>
            Mathf.Clamp(GetGradeValue(randomOptionCount, grade), 0, 4);
        public float GetGloveCriticalRatePercent(EquipmentGrade grade) =>
            GetGradeValue(gloveCriticalRatePercent, grade);
        public float GetGloveCriticalDamagePercent(EquipmentGrade grade) =>
            GetGradeValue(gloveCriticalDamagePercent, grade);
        public float GetRingAttackSpeedPercent(EquipmentGrade grade) =>
            GetGradeValue(ringAttackSpeedPercent, grade);
        public float GetRingMoveSpeedPercent(EquipmentGrade grade) =>
            GetGradeValue(ringMoveSpeedPercent, grade);

        public float GetOptionBaseValuePercent(EquipmentOptionType type)
        {
            var index = (int)type;
            return index >= 0 && index < optionBaseValuesPercent.Length
                ? Mathf.Max(0f, optionBaseValuesPercent[index])
                : 0f;
        }

        public float GetOptionWeight(EquipmentOptionType type)
        {
            var group = GetOptionGroup(type);
            var groupWeight = group switch
            {
                EquipmentOptionGroup.Core => coreGroupWeight,
                EquipmentOptionGroup.Offense => offenseGroupWeight,
                _ => utilityDefenseGroupWeight
            };
            return Mathf.Max(0f, groupWeight) / GetGroupTypeCount(group);
        }

        public static EquipmentOptionGroup GetOptionGroup(EquipmentOptionType type)
        {
            switch (type)
            {
                case EquipmentOptionType.AttackPower:
                case EquipmentOptionType.Defense:
                case EquipmentOptionType.MaxHealth:
                    return EquipmentOptionGroup.Core;
                case EquipmentOptionType.AttackSpeed:
                case EquipmentOptionType.CriticalRate:
                case EquipmentOptionType.CriticalDamage:
                case EquipmentOptionType.SkillDamage:
                case EquipmentOptionType.BossDamage:
                case EquipmentOptionType.NormalMonsterDamage:
                    return EquipmentOptionGroup.Offense;
                default:
                    return EquipmentOptionGroup.UtilityDefense;
            }
        }

        public bool TryValidate(out string error)
        {
            if (!TryValidateLevelRules(out error)) return false;

            if (!HasLength(dropWeights, GradeCount) || !HasLength(coreStatBudgetPercent, GradeCount) ||
                !HasLength(randomOptionGradeMultiplier, GradeCount) || !HasLength(randomOptionCount, GradeCount) ||
                !HasLength(gloveCriticalRatePercent, GradeCount) || !HasLength(gloveCriticalDamagePercent, GradeCount) ||
                !HasLength(ringAttackSpeedPercent, GradeCount) || !HasLength(ringMoveSpeedPercent, GradeCount) ||
                !HasLength(optionBaseValuesPercent, OptionTypeCount))
            {
                error = "Equipment balance arrays have invalid lengths.";
                return false;
            }

            var totalDropWeight = 0f;
            for (var index = 0; index < dropWeights.Length; index++)
            {
                if (!IsFiniteNonNegative(dropWeights[index]) || !IsFiniteNonNegative(coreStatBudgetPercent[index]) ||
                    !IsFiniteNonNegative(randomOptionGradeMultiplier[index]) || randomOptionCount[index] < 0 ||
                    randomOptionCount[index] > 4)
                {
                    error = "Equipment grade balance contains an invalid value.";
                    return false;
                }

                totalDropWeight += dropWeights[index];
            }

            if (!IsFiniteNonNegative(totalDropWeight) || totalDropWeight <= 0f ||
                !IsFiniteNonNegative(coreGroupWeight) || !IsFiniteNonNegative(offenseGroupWeight) ||
                !IsFiniteNonNegative(utilityDefenseGroupWeight) ||
                !IsFiniteNonNegative(minimumRandomMultiplier) || !IsFiniteNonNegative(maximumRandomMultiplier) ||
                coreGroupWeight < 0f || offenseGroupWeight < 0f ||
                utilityDefenseGroupWeight < 0f || coreGroupWeight + offenseGroupWeight + utilityDefenseGroupWeight <= 0f ||
                minimumRandomMultiplier < 0f || maximumRandomMultiplier < minimumRandomMultiplier)
            {
                error = "Equipment option weights or random range are invalid.";
                return false;
            }

            for (var index = 0; index < optionBaseValuesPercent.Length; index++)
            {
                if (!IsFiniteNonNegative(optionBaseValuesPercent[index]))
                {
                    error = "Equipment option base value is invalid.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private bool TryValidateLevelRules(out string error)
        {
            if (maximumItemLevel < 1 || itemLevelOffsets == null || itemLevelOffsets.Length == 0 ||
                !HasLength(itemLevelWeights, itemLevelOffsets.Length) ||
                !HasLength(optionGrowthPerLevel, OptionTypeCount) ||
                !IsFiniteNonNegative(primaryCoreGrowthPerLevel) || primaryCoreGrowthPerLevel <= 0f ||
                !IsFiniteNonNegative(secondaryCoreGrowthPerLevel) || secondaryCoreGrowthPerLevel <= 0f)
            {
                error = "Equipment level settings are invalid.";
                return false;
            }

            var totalWeight = 0d;
            for (var index = 0; index < itemLevelOffsets.Length; index++)
            {
                if (itemLevelOffsets[index] > 0 || !IsFiniteNonNegative(itemLevelWeights[index]))
                {
                    error = "Equipment level offsets or weights are invalid.";
                    return false;
                }
                totalWeight += itemLevelWeights[index];
            }

            if (totalWeight <= 0d)
            {
                error = "Equipment level weights must have a positive sum.";
                return false;
            }

            for (var index = 0; index < optionGrowthPerLevel.Length; index++)
            {
                if (!IsFiniteNonNegative(optionGrowthPerLevel[index]) || optionGrowthPerLevel[index] <= 0f)
                {
                    error = "Equipment option level growth must be finite and positive.";
                    return false;
                }
            }
            error = null;
            return true;
        }

        private static int GetGroupTypeCount(EquipmentOptionGroup group)
        {
            return group switch
            {
                EquipmentOptionGroup.Core => 3,
                EquipmentOptionGroup.Offense => 6,
                _ => 4
            };
        }

        private static float GetGradeValue(float[] values, EquipmentGrade grade)
        {
            var index = (int)grade;
            return values != null && index >= 0 && index < values.Length ? values[index] : 0f;
        }

        private static int GetGradeValue(int[] values, EquipmentGrade grade)
        {
            var index = (int)grade;
            return values != null && index >= 0 && index < values.Length ? values[index] : 0;
        }

        private static bool HasLength(Array array, int expected) => array != null && array.Length == expected;
        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
