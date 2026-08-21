using System;
using System.Collections.Generic;
using ProjectMT.Shared.CommanderSkill;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectMT.Features.CommanderSkill
{
    [CreateAssetMenu(menuName = "ProjectMT/Commander Skill/Catalog", fileName = "CommanderSkillCatalog")]
    public sealed class CommanderSkillCatalog : ScriptableObject // 정의·성장·전용 소환 연결
    {
        [FormerlySerializedAs("attackSkills")]
        [SerializeField] private CommanderSkillDefinition[] skills = Array.Empty<CommanderSkillDefinition>();
        [SerializeField] private CommanderSkillBalanceConfig balanceConfig;
        [SerializeField] private CommanderSkillSummonConfig summonConfig;

        private Dictionary<string, CommanderSkillDefinition> lookup;
        private CommanderAttackSkillDefinition[] attackSkills;

        public IReadOnlyList<CommanderSkillDefinition> Skills => skills ?? Array.Empty<CommanderSkillDefinition>();
        public IReadOnlyList<CommanderAttackSkillDefinition> AttackSkills
        {
            get
            {
                EnsureLookup();
                return attackSkills ?? Array.Empty<CommanderAttackSkillDefinition>();
            }
        }
        public CommanderSkillBalanceConfig BalanceConfig =>
            balanceConfig == null ? CommanderSkillBalanceConfig.RuntimeDefault : balanceConfig;
        public CommanderSkillSummonConfig SummonConfig =>
            summonConfig == null ? CommanderSkillSummonConfig.RuntimeDefault : summonConfig;

        public bool TryGet(string skillId, out CommanderSkillDefinition definition)
        {
            EnsureLookup();
            return lookup.TryGetValue(skillId?.Trim() ?? string.Empty, out definition) && definition != null;
        }

        public bool TryGetAttack(string skillId, out CommanderAttackSkillDefinition definition)
        {
            if (TryGet(skillId, out var candidate) && candidate is CommanderAttackSkillDefinition attack)
            {
                definition = attack;
                return true;
            }

            definition = null;
            return false;
        }

        public bool TryValidate(out string error)
        {
            if (balanceConfig == null)
            {
                error = "Commander skill balance config is missing.";
                return false;
            }

            if (!balanceConfig.TryValidate(out error))
            {
                error = $"Commander skill balance config is invalid. {error}";
                return false;
            }

            if (skills == null || skills.Length == 0)
            {
                error = "At least one commander skill definition is required.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < skills.Length; index++)
            {
                var definition = skills[index];
                if (definition == null)
                {
                    error = $"Skill definition {index} is missing.";
                    return false;
                }

                if (!definition.TryValidate(out error))
                {
                    error = $"{definition.name}: {error}";
                    return false;
                }

                if (!ids.Add(definition.SkillId))
                {
                    error = $"Duplicate commander skill id: {definition.SkillId}";
                    return false;
                }

                if (!balanceConfig.TryGetRule(definition.SkillId, out _))
                {
                    error = $"Missing growth rule for {definition.SkillId}.";
                    return false;
                }
            }

            var rules = balanceConfig.SkillRules;
            for (var index = 0; index < rules.Count; index++)
            {
                var rule = rules[index];
                if (rule != null && !ids.Contains(rule.SkillId))
                {
                    error = $"Growth rule has no registered commander skill definition: {rule.SkillId}";
                    return false;
                }
            }

            if (summonConfig == null)
            {
                error = "Commander skill summon config is missing.";
                return false;
            }

            if (!summonConfig.TryValidate(balanceConfig, out error))
            {
                error = $"Commander skill summon config is invalid. {error}";
                return false;
            }

            var summonLevels = summonConfig.Levels;
            for (var levelIndex = 0; levelIndex < summonLevels.Count; levelIndex++)
            {
                var pool = summonLevels[levelIndex]?.Pool;
                if (pool == null)
                {
                    continue;
                }

                for (var entryIndex = 0; entryIndex < pool.Count; entryIndex++)
                {
                    var entry = pool[entryIndex];
                    if (entry != null && !ids.Contains(entry.SkillId))
                    {
                        error = $"Summon pool has no registered commander skill definition: {entry.SkillId}";
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        private void OnEnable()
        {
            lookup = null;
            attackSkills = null;
        }

        private void EnsureLookup()
        {
            if (lookup != null)
            {
                return;
            }

            lookup = new Dictionary<string, CommanderSkillDefinition>(StringComparer.Ordinal);
            var attacks = new List<CommanderAttackSkillDefinition>();
            if (skills == null)
            {
                attackSkills = Array.Empty<CommanderAttackSkillDefinition>();
                return;
            }

            for (var index = 0; index < skills.Length; index++)
            {
                var definition = skills[index];
                if (definition != null && !string.IsNullOrWhiteSpace(definition.SkillId))
                {
                    lookup[definition.SkillId] = definition;
                    if (definition is CommanderAttackSkillDefinition attack)
                    {
                        attacks.Add(attack);
                    }
                }
            }

            attackSkills = attacks.ToArray();
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            CommanderSkillBalanceConfig skillBalance,
            params CommanderSkillDefinition[] definitions)
        {
            EditorConfigure(skillBalance, summonConfig ?? CommanderSkillSummonConfig.RuntimeDefault, definitions);
        }

        public void EditorConfigure(
            CommanderSkillBalanceConfig skillBalance,
            CommanderSkillSummonConfig skillSummon,
            params CommanderSkillDefinition[] definitions)
        {
            balanceConfig = skillBalance;
            summonConfig = skillSummon;
            skills = definitions ?? Array.Empty<CommanderSkillDefinition>();
            lookup = null;
            attackSkills = null;
        }
#endif
    }
}
