using ProjectMT.Shared.Audio;
using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    [CreateAssetMenu(
        menuName = "ProjectMT/Commander Skill/Effect Definition",
        fileName = "CS_Effect")]
    public sealed class CommanderEffectSkillDefinition : CommanderSkillDefinition // 버프·디버프형 군단장 스킬 SO
    {
        [SerializeField] private CommanderSkillCategory category = CommanderSkillCategory.Buff;

        public override CommanderSkillCategory Category => category;

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error))
            {
                return false;
            }

            if (category is not CommanderSkillCategory.Buff and not CommanderSkillCategory.Debuff)
            {
                error = $"{SkillId}: effect skill category must be buff or debuff.";
                return false;
            }

            if ((category == CommanderSkillCategory.Buff &&
                 Targeting.TargetTeam != CommanderSkillTargetTeam.Ally) ||
                (category == CommanderSkillCategory.Debuff &&
                 Targeting.TargetTeam != CommanderSkillTargetTeam.Enemy))
            {
                error = $"{SkillId}: target team does not match the effect category.";
                return false;
            }

            for (var index = 0; index < Effects.Count; index++)
            {
                if (Effects[index] is not CommanderUnitEffectDefinition unitEffect ||
                    !CommanderUnitEffectDefinition.IsCompatible(category, unitEffect.EffectType))
                {
                    error = $"{SkillId}: effect {index} is not compatible with {category}.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string title,
            string body,
            Sprite skillIcon,
            CommanderSkillCategory skillCategory,
            float castTimeSeconds,
            float cooldownSeconds,
            CommanderSkillTargetingDefinition targetingRule,
            CommanderUnitEffectDefinition[] effectRules,
            GameObject castVfx,
            float castLifetime,
            SfxCue castCue,
            GameObject impactVfx,
            float impactLifetime,
            SfxCue impactCue)
        {
            category = skillCategory;
            EditorConfigureCommon(
                id,
                title,
                body,
                skillIcon,
                castTimeSeconds,
                cooldownSeconds,
                targetingRule,
                effectRules,
                castVfx,
                castLifetime,
                castCue,
                impactVfx,
                impactLifetime,
                impactCue);
        }
#endif
    }
}
