using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.CommanderSkill
{
    [Serializable]
    public sealed class CommanderSkillGrowthRule // 스킬별 레벨 성장 원본
    {
        private const long DefaultBaseGoldCost = 100L;
        private const float DefaultGoldCostGrowthMultiplier = 1.06f;
        private const long GoldCostRoundingUnit = 10L;

        [SerializeField] private string skillId;
        [SerializeField, Min(1)] private int maxLevel = 200;
        [SerializeField, Min(1)] private int requiredDuplicateCount = 1;
        [SerializeField, Min(1)] private long baseGoldCost = DefaultBaseGoldCost;
        [SerializeField, Min(1f)] private float goldCostGrowthMultiplier = DefaultGoldCostGrowthMultiplier;
        [SerializeField] private AnimationCurve damageMultiplierByLevel =
            AnimationCurve.Linear(1f, 1f, 200f, 4.98f);

        public string SkillId => skillId?.Trim() ?? string.Empty;
        public int MaxLevel => Mathf.Max(1, maxLevel);
        public int RequiredDuplicateCount => Mathf.Max(1, requiredDuplicateCount);
        public long BaseGoldCost => baseGoldCost > 0L ? baseGoldCost : DefaultBaseGoldCost;
        public float GoldCostGrowthMultiplier => goldCostGrowthMultiplier >= 1f
            ? goldCostGrowthMultiplier
            : DefaultGoldCostGrowthMultiplier;

        public CommanderSkillGrowthRule()
        {
        }

        internal CommanderSkillGrowthRule(
            string id,
            int levelCap,
            int duplicateCost,
            AnimationCurve damageCurve)
        {
            skillId = id?.Trim() ?? string.Empty;
            maxLevel = Mathf.Max(1, levelCap);
            requiredDuplicateCount = Mathf.Max(1, duplicateCost);
            damageMultiplierByLevel = damageCurve ?? AnimationCurve.Linear(1f, 1f, maxLevel, 1f);
        }

        public bool TryGetNextLevelGoldCost(int currentLevel, out long cost)
        {
            cost = 0L;
            if (currentLevel < 1 || currentLevel >= MaxLevel)
            {
                return false;
            }

            var rawCost = BaseGoldCost * Math.Pow(GoldCostGrowthMultiplier, currentLevel - 1);
            if (double.IsNaN(rawCost) || rawCost <= 0d)
            {
                return false;
            }

            if (double.IsInfinity(rawCost) || rawCost >= long.MaxValue)
            {
                cost = long.MaxValue;
                return true;
            }

            var rounded = Math.Round(
                rawCost / GoldCostRoundingUnit,
                MidpointRounding.AwayFromZero) * GoldCostRoundingUnit;
            cost = Math.Max(GoldCostRoundingUnit, (long)rounded);
            return true;
        }

        public float GetDamageMultiplier(int level)
        {
            var clampedLevel = Mathf.Clamp(level, 1, MaxLevel);
            if (damageMultiplierByLevel == null || damageMultiplierByLevel.length == 0)
            {
                return 1f;
            }

            var value = damageMultiplierByLevel.Evaluate(clampedLevel);
            return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Max(0f, value);
        }

        internal bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(SkillId))
            {
                error = "Skill id is empty.";
                return false;
            }

            if (maxLevel < 1 || requiredDuplicateCount < 1 || baseGoldCost < 0L ||
                (goldCostGrowthMultiplier != 0f && goldCostGrowthMultiplier < 1f) ||
                float.IsNaN(goldCostGrowthMultiplier) ||
                float.IsInfinity(goldCostGrowthMultiplier))
            {
                error = $"{SkillId}: level cap, duplicate reserve, or gold cost is invalid.";
                return false;
            }

            if (!TryGetNextLevelGoldCost(1, out _) && MaxLevel > 1)
            {
                error = $"{SkillId}: gold cost curve is invalid.";
                return false;
            }

            if (damageMultiplierByLevel == null || damageMultiplierByLevel.length == 0 ||
                damageMultiplierByLevel.keys[0].time > 1f ||
                damageMultiplierByLevel.keys[damageMultiplierByLevel.length - 1].time < maxLevel)
            {
                error = $"{SkillId}: damage multiplier curve must cover level 1 through {maxLevel}.";
                return false;
            }

            for (var level = 1; level <= maxLevel; level++)
            {
                var multiplier = damageMultiplierByLevel.Evaluate(level);
                if (multiplier <= 0f || float.IsNaN(multiplier) || float.IsInfinity(multiplier))
                {
                    error = $"{SkillId}: damage multiplier is invalid at level {level}.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            int levelCap,
            int duplicateCost,
            AnimationCurve damageCurve,
            long goldBaseCost = DefaultBaseGoldCost,
            float goldGrowthMultiplier = DefaultGoldCostGrowthMultiplier)
        {
            skillId = id?.Trim() ?? string.Empty;
            maxLevel = Mathf.Max(1, levelCap);
            requiredDuplicateCount = Mathf.Max(1, duplicateCost);
            baseGoldCost = Math.Max(1L, goldBaseCost);
            goldCostGrowthMultiplier = Mathf.Max(1f, goldGrowthMultiplier);
            damageMultiplierByLevel = damageCurve ?? AnimationCurve.Linear(1f, 1f, maxLevel, 1f);
        }
#endif
    }

    [CreateAssetMenu(menuName = "ProjectMT/Commander Skill/Balance Config", fileName = "CommanderSkillBalanceConfig")]
    public sealed class CommanderSkillBalanceConfig : ScriptableObject // 스킬 레벨 성장 곡선 SO
    {
        private static CommanderSkillBalanceConfig runtimeDefault;

        [SerializeField] private CommanderSkillGrowthRule[] skillRules = Array.Empty<CommanderSkillGrowthRule>();

        private Dictionary<string, CommanderSkillGrowthRule> lookup;

        public static CommanderSkillBalanceConfig RuntimeDefault
        {
            get
            {
                if (runtimeDefault != null)
                {
                    return runtimeDefault;
                }

                runtimeDefault = CreateInstance<CommanderSkillBalanceConfig>();
                runtimeDefault.hideFlags = HideFlags.HideAndDontSave;
                runtimeDefault.skillRules = new[]
                {
                    CreateDefaultRule(CommanderSkillIds.Fireball),
                    CreateDefaultRule(CommanderSkillIds.IceCrystalOrb)
                };
                return runtimeDefault;
            }
        }

        public IReadOnlyList<CommanderSkillGrowthRule> SkillRules =>
            skillRules ?? Array.Empty<CommanderSkillGrowthRule>();

        public bool TryGetRule(string skillId, out CommanderSkillGrowthRule rule)
        {
            EnsureLookup();
            return lookup.TryGetValue(skillId?.Trim() ?? string.Empty, out rule) && rule != null;
        }

        public bool TryValidate(out string error)
        {
            if (skillRules == null || skillRules.Length == 0)
            {
                error = "At least one skill growth rule is required.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < skillRules.Length; index++)
            {
                var rule = skillRules[index];
                if (rule == null)
                {
                    error = $"Rule {index} is missing.";
                    return false;
                }

                if (!rule.TryValidate(out error))
                {
                    error = $"Rule {index}: {error}";
                    return false;
                }

                if (!ids.Add(rule.SkillId))
                {
                    error = $"Duplicate skill growth rule: {rule.SkillId}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private void OnEnable()
        {
            lookup = null;
        }

        private void EnsureLookup()
        {
            if (lookup != null)
            {
                return;
            }

            lookup = new Dictionary<string, CommanderSkillGrowthRule>(StringComparer.Ordinal);
            var rules = SkillRules;
            for (var index = 0; index < rules.Count; index++)
            {
                var rule = rules[index];
                if (rule != null && !string.IsNullOrWhiteSpace(rule.SkillId))
                {
                    lookup[rule.SkillId] = rule;
                }
            }
        }

        private static CommanderSkillGrowthRule CreateDefaultRule(string skillId)
        {
            var curve = AnimationCurve.Linear(1f, 1f, 200f, 4.98f);
            curve.preWrapMode = WrapMode.ClampForever;
            curve.postWrapMode = WrapMode.ClampForever;
            return new CommanderSkillGrowthRule(skillId, 200, 1, curve);
        }

#if UNITY_EDITOR
        public void EditorConfigure(params CommanderSkillGrowthRule[] rules)
        {
            skillRules = rules ?? Array.Empty<CommanderSkillGrowthRule>();
            lookup = null;
        }
#endif
    }
}
