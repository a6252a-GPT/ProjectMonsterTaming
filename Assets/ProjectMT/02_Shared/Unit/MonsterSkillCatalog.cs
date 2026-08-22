using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Skill Catalog", fileName = "MonsterSkillCatalog")]
    public sealed class MonsterSkillCatalog : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/ProjectMT/02_Shared/Unit/Data/MonsterSkillCatalog.asset";

        [SerializeField] private MonsterPassiveSkill[] passiveSkills = Array.Empty<MonsterPassiveSkill>();
        [SerializeField] private MonsterActiveSkill[] activeSkills = Array.Empty<MonsterActiveSkill>();

        private Dictionary<string, MonsterSkillDefinitionBase> lookup;

        public IReadOnlyList<MonsterPassiveSkill> PassiveSkills => passiveSkills ?? Array.Empty<MonsterPassiveSkill>();
        public IReadOnlyList<MonsterActiveSkill> ActiveSkills => activeSkills ?? Array.Empty<MonsterActiveSkill>();

        public bool Contains(MonsterSkillDefinitionBase skill)
        {
            return skill != null && TryGet(skill.SkillId, out var registered) && registered == skill;
        }

        public bool TryGet(string skillId, out MonsterSkillDefinitionBase skill)
        {
            EnsureLookup();
            return lookup.TryGetValue(skillId?.Trim() ?? string.Empty, out skill) && skill != null;
        }

        public bool TryValidate(out string error)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var passives = passiveSkills ?? Array.Empty<MonsterPassiveSkill>();
            var actives = activeSkills ?? Array.Empty<MonsterActiveSkill>();
            if (passives.Length == 0 || actives.Length == 0)
            {
                error = "Monster skill catalog requires passive and active presets.";
                return false;
            }

            for (var index = 0; index < passives.Length; index++)
            {
                if (!ValidateEntry(passives[index], "Passive", index, ids, out error))
                {
                    return false;
                }
            }

            for (var index = 0; index < actives.Length; index++)
            {
                if (!ValidateEntry(actives[index], "Active", index, ids, out error))
                {
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool ValidateEntry(
            MonsterSkillDefinitionBase skill,
            string category,
            int index,
            ISet<string> ids,
            out string error)
        {
            if (skill == null)
            {
                error = $"Monster skill catalog has an empty {category} entry. Index={index}";
                return false;
            }

            if (!skill.TryValidate(out error))
            {
                error = $"Monster skill catalog {category} entry is invalid. Index={index}, Detail={error}";
                return false;
            }

            if (!ids.Add(skill.SkillId))
            {
                error = $"Monster skill ID is duplicated. Skill={skill.SkillId}";
                return false;
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

            lookup = new Dictionary<string, MonsterSkillDefinitionBase>(StringComparer.OrdinalIgnoreCase);
            AddToLookup(passiveSkills);
            AddToLookup(activeSkills);
        }

        private void AddToLookup<TSkill>(TSkill[] skills)
            where TSkill : MonsterSkillDefinitionBase
        {
            if (skills == null)
            {
                return;
            }

            for (var index = 0; index < skills.Length; index++)
            {
                var skill = skills[index];
                if (skill != null && !string.IsNullOrWhiteSpace(skill.SkillId))
                {
                    lookup[skill.SkillId] = skill;
                }
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(MonsterPassiveSkill[] passives, MonsterActiveSkill[] actives)
        {
            passiveSkills = passives ?? Array.Empty<MonsterPassiveSkill>();
            activeSkills = actives ?? Array.Empty<MonsterActiveSkill>();
            lookup = null;
        }
#endif
    }
}
