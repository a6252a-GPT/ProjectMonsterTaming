using System;
using UnityEngine;

namespace ProjectMT.Shared.Stats
{
    [CreateAssetMenu(menuName = "ProjectMT/Stats/Commander Growth Config", fileName = "CommanderGrowthConfig")]
    public sealed class CommanderGrowthConfig : ScriptableObject // 군단장 경험치 곡선과 군단 공용 기본 성장
    {
        private static CommanderGrowthConfig runtimeDefault;

        [SerializeField, Min(1)] private int maxLevel = 1000;
        [SerializeField, Min(1)] private long baseExperienceRequirement = 10L;
        [SerializeField, Min(1f)] private float experienceGrowthMultiplier = 1.1f;
        [SerializeField, Min(0f)] private float coreStatRatePerLevel = 0.01f;

        public int MaxLevel => Mathf.Max(1, maxLevel);
        public long BaseExperienceRequirement => Math.Max(1L, baseExperienceRequirement);
        public float ExperienceGrowthMultiplier => Mathf.Max(1f, experienceGrowthMultiplier);
        public float CoreStatRatePerLevel => Mathf.Max(0f, coreStatRatePerLevel);

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

        public float GetAccumulatedCoreStatRate(int level)
        {
            return CoreStatRatePerLevel * Math.Max(0, Mathf.Clamp(level, 1, MaxLevel) - 1);
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

        public bool TryValidate(out string error)
        {
            if (maxLevel < 1 || baseExperienceRequirement < 1L ||
                experienceGrowthMultiplier < 1f || coreStatRatePerLevel < 0f)
            {
                error = "Commander growth settings are invalid.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
