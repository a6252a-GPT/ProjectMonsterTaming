using System;
using ProjectMT.Shared.Audio;
using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    public enum CommanderSkillCategory
    {
        Attack,
        Buff,
        Debuff
    }

    public abstract class CommanderSkillDefinition : ScriptableObject // 표시·쿨타임·대상·효과·피드백 공통 SO
    {
        [Header("Identity")]
        [SerializeField] private string skillId;
        [SerializeField] private string displayName;
        [TextArea(2, 5)] [SerializeField] private string description;
        [SerializeField] private Sprite icon;

        [Header("Cast")]
        [SerializeField, Min(0.1f)] private float cooldown = 5f;
        [SerializeField] private CommanderSkillTargetingDefinition targeting;
        [SerializeField] private CommanderSkillEffectDefinition[] effects = Array.Empty<CommanderSkillEffectDefinition>();

        [Header("Feedback")]
        [SerializeField] private GameObject castVfxPrefab;
        [SerializeField, Min(0.05f)] private float castVfxLifetime = 1f;
        [SerializeField] private SfxCue castSfx;
        [SerializeField] private GameObject impactVfxPrefab;
        [SerializeField, Min(0.05f)] private float impactVfxLifetime = 1.5f;
        [SerializeField] private SfxCue impactSfx;

        public string SkillId => skillId?.Trim() ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public string Description => description ?? string.Empty;
        public Sprite Icon => icon;
        public float Cooldown => Mathf.Max(0.1f, cooldown);
        public CommanderSkillTargetingDefinition Targeting => targeting;
        public float TargetRange => targeting == null ? 0f : targeting.Range;
        public System.Collections.Generic.IReadOnlyList<CommanderSkillEffectDefinition> Effects =>
            effects ?? Array.Empty<CommanderSkillEffectDefinition>();
        public GameObject CastVfxPrefab => castVfxPrefab;
        public float CastVfxLifetime => Mathf.Max(0.05f, castVfxLifetime);
        public SfxCue CastSfx => castSfx;
        public GameObject ImpactVfxPrefab => impactVfxPrefab;
        public float ImpactVfxLifetime => Mathf.Max(0.05f, impactVfxLifetime);
        public SfxCue ImpactSfx => impactSfx;
        public abstract CommanderSkillCategory Category { get; }

        public bool TryGetEffect<TEffect>(out TEffect effect)
            where TEffect : CommanderSkillEffectDefinition
        {
            var source = Effects;
            for (var index = 0; index < source.Count; index++)
            {
                if (source[index] is TEffect typed)
                {
                    effect = typed;
                    return true;
                }
            }

            effect = null;
            return false;
        }

        public virtual bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(SkillId) || string.IsNullOrWhiteSpace(DisplayName))
            {
                error = "Skill id or display name is empty.";
                return false;
            }

            if (cooldown < 0.1f || float.IsNaN(cooldown) || float.IsInfinity(cooldown))
            {
                error = $"{SkillId}: cooldown is invalid.";
                return false;
            }

            if (targeting == null)
            {
                error = $"{SkillId}: targeting is missing.";
                return false;
            }

            if (!targeting.TryValidate(out error))
            {
                error = $"{SkillId}: targeting is invalid. {error}";
                return false;
            }

            if (effects == null || effects.Length == 0)
            {
                error = $"{SkillId}: at least one effect is required.";
                return false;
            }

            for (var index = 0; index < effects.Length; index++)
            {
                if (effects[index] == null)
                {
                    error = $"{SkillId}: effect {index} is missing.";
                    return false;
                }

                if (!effects[index].TryValidate(out error))
                {
                    error = $"{SkillId}: effect {index} is invalid. {error}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        protected void EditorConfigureCommon(
            string id,
            string title,
            string body,
            Sprite skillIcon,
            float cooldownSeconds,
            CommanderSkillTargetingDefinition targetingRule,
            CommanderSkillEffectDefinition[] effectRules,
            GameObject castVfx,
            float castLifetime,
            SfxCue castCue,
            GameObject impactVfx,
            float impactLifetime,
            SfxCue impactCue)
        {
            skillId = id?.Trim() ?? string.Empty;
            displayName = title?.Trim() ?? string.Empty;
            description = body?.Trim() ?? string.Empty;
            icon = skillIcon;
            cooldown = Mathf.Max(0.1f, cooldownSeconds);
            targeting = targetingRule;
            effects = effectRules ?? Array.Empty<CommanderSkillEffectDefinition>();
            castVfxPrefab = castVfx;
            castVfxLifetime = Mathf.Max(0.05f, castLifetime);
            castSfx = castCue;
            impactVfxPrefab = impactVfx;
            impactVfxLifetime = Mathf.Max(0.05f, impactLifetime);
            impactSfx = impactCue;
        }
#endif
    }
}
