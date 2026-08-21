using System;
using ProjectMT.Shared.Commander;
using UnityEngine;

namespace ProjectMT.Shared.Stats
{
    [CreateAssetMenu(menuName = "ProjectMT/Stats/Commander Growth Config", fileName = "CommanderGrowthConfig")]
    public sealed class CommanderGrowthConfig : ScriptableObject // 군단장 레벨·군단 공용 강화 규칙
    {
        private static CommanderGrowthConfig runtimeDefault;

        [SerializeField, Min(1)] private int maxLevel = 1000;
        [SerializeField, Min(1)] private long baseExperienceRequirement = 10L;
        [SerializeField, Min(1f)] private float experienceGrowthMultiplier = 1.1f;

        [Header("군단 공용 기본 능력치 강화")]
        [SerializeField, Range(0.0001f, 1f)] private float legionGrowthRatePerLevel = 0.01f;
        [SerializeField, Min(1)] private int legionCoreStatMaxLevel = 100;
        [SerializeField, Min(1)] private long legionCoreStatBaseGoldCost = 100L;
        [SerializeField, Min(1f)] private float legionCoreStatGoldGrowthMultiplier = 1.12f;

        [Header("군단 공용 제한 능력치 강화")]
        [SerializeField, Min(1)] private int legionAttackSpeedMaxLevel = 25;
        [SerializeField, Min(1)] private int legionMoveSpeedMaxLevel = 15;
        [SerializeField, Min(1)] private int legionAttackRangeMaxLevel = 10;
        [SerializeField, Min(1)] private int legionTrainingPointBaseCost = 1;
        [SerializeField, Min(1)] private int legionTrainingPointLevelsPerTier = 10;

        public int MaxLevel => Mathf.Max(1, maxLevel);
        public long BaseExperienceRequirement => Math.Max(1L, baseExperienceRequirement);
        public float ExperienceGrowthMultiplier => Mathf.Max(1f, experienceGrowthMultiplier);
        public float LegionGrowthRatePerLevel => Mathf.Max(0.0001f, legionGrowthRatePerLevel);

        public static CommanderGrowthConfig RuntimeDefault
        {
            get
            {
                if (runtimeDefault == null)
                {
                    runtimeDefault = CreateInstance<CommanderGrowthConfig>();
                    runtimeDefault.hideFlags = HideFlags.HideAndDontSave;
                }

                return runtimeDefault;
            }
        }

        public long GetExperienceRequirement(int level)
        {
            level = Mathf.Clamp(level, 1, MaxLevel);
            if (level >= MaxLevel)
            {
                return 0L;
            }

            var raw = BaseExperienceRequirement * Math.Pow(ExperienceGrowthMultiplier, level - 1);
            if (double.IsNaN(raw) || double.IsInfinity(raw) || raw >= long.MaxValue)
            {
                return long.MaxValue;
            }

            return Math.Max(1L, (long)Math.Round(raw, MidpointRounding.AwayFromZero));
        }

        public float GetProgress01(int level, long experience)
        {
            return Mathf.Clamp01((float)GetProgressRatio(level, experience));
        }

        public double GetProgressRatio(int level, long experience)
        {
            var requirement = GetExperienceRequirement(level);
            return requirement <= 0L
                ? 1d
                : (double)Math.Max(0L, experience) / requirement;
        }

        public bool CanLevelUp(int level, long experience)
        {
            var requirement = GetExperienceRequirement(level);
            return level >= 1 && level < MaxLevel && requirement > 0L && experience >= requirement;
        }

        public bool TryResolveLevelUp(
            int currentLevel,
            long currentExperience,
            out int resolvedLevel,
            out long resolvedExperience)
        {
            resolvedLevel = Mathf.Clamp(currentLevel, 1, MaxLevel);
            resolvedExperience = Math.Max(0L, currentExperience);
            if (!CanLevelUp(resolvedLevel, resolvedExperience))
            {
                return false;
            }

            resolvedExperience -= GetExperienceRequirement(resolvedLevel);
            resolvedLevel++;
            if (resolvedLevel >= MaxLevel)
            {
                resolvedExperience = 0L;
            }

            return true;
        }

        public int GetLegionGrowthMaxLevel(CommanderLegionStat stat)
        {
            return stat switch
            {
                CommanderLegionStat.MaxHealth => Mathf.Max(1, legionCoreStatMaxLevel),
                CommanderLegionStat.AttackPower => Mathf.Max(1, legionCoreStatMaxLevel),
                CommanderLegionStat.Defense => Mathf.Max(1, legionCoreStatMaxLevel),
                CommanderLegionStat.AttackSpeed => Mathf.Max(1, legionAttackSpeedMaxLevel),
                CommanderLegionStat.MoveSpeed => Mathf.Max(1, legionMoveSpeedMaxLevel),
                CommanderLegionStat.AttackRange => Mathf.Max(1, legionAttackRangeMaxLevel),
                _ => 0
            };
        }

        public float GetLegionGrowthRate(CommanderLegionStat stat, int level)
        {
            if (!Enum.IsDefined(typeof(CommanderLegionStat), stat))
            {
                return 0f;
            }

            var repairedLevel = Mathf.Clamp(level, 0, GetLegionGrowthMaxLevel(stat));
            return repairedLevel * LegionGrowthRatePerLevel;
        }

        public bool UsesGoldForLegionGrowth(CommanderLegionStat stat)
        {
            return stat == CommanderLegionStat.MaxHealth ||
                   stat == CommanderLegionStat.AttackPower ||
                   stat == CommanderLegionStat.Defense;
        }

        public long GetLegionGrowthGoldCost(CommanderLegionStat stat, int level)
        {
            if (!UsesGoldForLegionGrowth(stat))
            {
                return 0L;
            }

            var raw = Math.Max(1L, legionCoreStatBaseGoldCost) *
                      Math.Pow(Mathf.Max(1f, legionCoreStatGoldGrowthMultiplier), Math.Max(0, level));
            if (double.IsNaN(raw) || double.IsInfinity(raw) || raw >= long.MaxValue)
            {
                return long.MaxValue;
            }

            return Math.Max(1L, (long)Math.Round(raw, MidpointRounding.AwayFromZero));
        }

        public int GetLegionGrowthTrainingPointCost(CommanderLegionStat stat, int level)
        {
            if (UsesGoldForLegionGrowth(stat) || !Enum.IsDefined(typeof(CommanderLegionStat), stat))
            {
                return 0;
            }

            return Mathf.Max(1, legionTrainingPointBaseCost) +
                   Math.Max(0, level) / Mathf.Max(1, legionTrainingPointLevelsPerTier);
        }

        public bool TryValidate(out string error)
        {
            if (maxLevel < 1 || baseExperienceRequirement < 1L || experienceGrowthMultiplier < 1f ||
                legionGrowthRatePerLevel <= 0f || legionCoreStatMaxLevel < 1 ||
                legionCoreStatBaseGoldCost < 1L || legionCoreStatGoldGrowthMultiplier < 1f ||
                legionAttackSpeedMaxLevel < 1 || legionMoveSpeedMaxLevel < 1 ||
                legionAttackRangeMaxLevel < 1 || legionTrainingPointBaseCost < 1 ||
                legionTrainingPointLevelsPerTier < 1)
            {
                error = "Commander growth settings are invalid.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
