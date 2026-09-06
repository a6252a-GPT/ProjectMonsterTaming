using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.CommanderSkill
{
    [Serializable]
    public sealed class CommanderSkillGrowthRule // 스킬별 레벨 성장 원본
    {

        [SerializeField] private string skillId;
        [SerializeField, Min(1)] private int maxLevel = 200;
        [SerializeField, Min(1)] private int requiredDuplicateCount = 1;
        [SerializeField, Min(1)] private int overflowUpgradeStoneAmount = 1;
        [SerializeField] private AnimationCurve damageMultiplierByLevel =
            AnimationCurve.Linear(1f, 1f, 200f, 4.98f);
        [SerializeField] private AnimationCurve ratioMultiplierByLevel;
        [SerializeField] private AnimationCurve controlDurationByLevel;

        public string SkillId => skillId?.Trim() ?? string.Empty;
        public int MaxLevel => Mathf.Max(1, maxLevel);
        public int RequiredDuplicateCount => Mathf.Max(1, requiredDuplicateCount);
        public int OverflowUpgradeStoneAmount => Mathf.Max(1, overflowUpgradeStoneAmount);

        public bool TryGetNextLevelStoneCost(int currentLevel, out long cost)
        {
            cost = 0;
            if (currentLevel < 1 || currentLevel >= MaxLevel) return false;
            cost = 1L + currentLevel / 50;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigureOverflowStoneAmount(int amount)
        {
            overflowUpgradeStoneAmount = Mathf.Max(1, amount);
        }
#endif

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

        public float GetRatioMultiplier(int level) => Evaluate(ratioMultiplierByLevel, level, 1.5f);
        public float GetControlDurationMultiplier(int level) => Evaluate(controlDurationByLevel, level, 1.25f);
        public AnimationCurve CopyDamageCurve() => CopyCurve(damageMultiplierByLevel);
        public AnimationCurve CopyRatioCurve() => CopyCurve(ratioMultiplierByLevel);
        public AnimationCurve CopyControlCurve() => CopyCurve(controlDurationByLevel);

        public static AnimationCurve CopyCurve(AnimationCurve curve) => curve == null ? null :
            new AnimationCurve(curve.keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };

        private float Evaluate(AnimationCurve curve, int level, float maximum)
        {
            var clamped = Mathf.Clamp(level, 1, MaxLevel);
            var value = curve == null || curve.length == 0
                ? Mathf.Lerp(1f, maximum, MaxLevel <= 1 ? 0f : (clamped - 1f) / (MaxLevel - 1f))
                : curve.Evaluate(clamped);
            return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Max(0f, value);
        }

        public void SetSupportCurves(AnimationCurve ratio, AnimationCurve control)
        {
            ratioMultiplierByLevel = CopyCurve(ratio);
            controlDurationByLevel = CopyCurve(control);
        }

        internal bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(SkillId))
            {
                error = "Skill id is empty.";
                return false;
            }

            if (maxLevel < 1 || maxLevel > 200 || requiredDuplicateCount < 1 || overflowUpgradeStoneAmount <= 0)
            {
                error = $"{SkillId}: level cap, duplicate reserve, or overflow stones are invalid.";
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
                if (!ValidSupportCurve(ratioMultiplierByLevel, level) ||
                    !ValidSupportCurve(controlDurationByLevel, level))
                {
                    error = $"{SkillId}: support multiplier curve is invalid at level {level}.";
                    return false;
                }
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
            AnimationCurve damageCurve)
        {
            skillId = id?.Trim() ?? string.Empty;
            maxLevel = Mathf.Max(1, levelCap);
            requiredDuplicateCount = Mathf.Max(1, duplicateCost);
            damageMultiplierByLevel = damageCurve ?? AnimationCurve.Linear(1f, 1f, maxLevel, 1f);
        }
#endif

        private bool ValidSupportCurve(AnimationCurve curve, int level)
        {
            if (curve == null || curve.length == 0) return true; // 기존 데이터는 명시적 이관 전 fallback 허용
            if (curve.keys[0].time > 1f || curve.keys[curve.length - 1].time < maxLevel) return false;
            var value = curve.Evaluate(level);
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [CreateAssetMenu(menuName = "ProjectMT/Commander Skill/Balance Config", fileName = "CommanderSkillBalanceConfig")]
    public sealed class CommanderSkillBalanceConfig : ScriptableObject // 스킬 레벨 성장 곡선 SO
    {
        private static CommanderSkillBalanceConfig runtimeDefault;

        [SerializeField] private CommanderSkillGrowthRule[] skillRules = Array.Empty<CommanderSkillGrowthRule>();
        [SerializeField] private int[] awakeningDuplicateCosts = { 2, 5, 10, 20, 40 };

        public int MaxAwakening => 5;
        public long GetOverflowUpgradeStoneAmount(string skillId) =>
            TryGetRule(skillId, out var rule) ? rule.OverflowUpgradeStoneAmount : 0;
        public bool TryGetAwakeningCost(int currentStar, out int cost)
        {
            cost = 0;
            if (currentStar < 0 || currentStar >= MaxAwakening ||
                awakeningDuplicateCosts == null || awakeningDuplicateCosts.Length != MaxAwakening) return false;
            cost = awakeningDuplicateCosts[currentStar];
            return cost > 0;
        }

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
                    CreateDefaultRule(CommanderSkillIds.Starter)
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
            if (awakeningDuplicateCosts == null ||
                awakeningDuplicateCosts.Length != MaxAwakening || Array.Exists(awakeningDuplicateCosts, value => value <= 0))
            {
                error = "Awakening costs are invalid.";
                return false;
            }
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
