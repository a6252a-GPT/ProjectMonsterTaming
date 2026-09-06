using System;
using ProjectMT.Shared.Audio;
using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    public enum CommanderSkillRarity { Common, Rare, Epic, Legendary, Mythic }

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
        [SerializeField] private CommanderSkillRarity rarity;

        [Header("Cast")]
        [SerializeField, Min(0f)] private float castTime;
        [SerializeField, Min(0.1f)] private float cooldown = 5f;
        [SerializeField] private CommanderSkillTargetingDefinition targeting;
        [SerializeField] private CommanderSkillEffectDefinition[] effects = Array.Empty<CommanderSkillEffectDefinition>();
        [SerializeField] private CommanderSkillPatternConfig pattern = new CommanderSkillPatternConfig();

        [Header("Feedback")]
        [SerializeField] private GameObject castingVfxPrefab;
        [SerializeField, Min(0.05f)] private float castingVfxLifetime = 1f;
        [SerializeField] private Vector3 castingVfxLocalOffset;
        [SerializeField] private Vector3 castingVfxLocalEuler;
        [SerializeField, Min(0.01f)] private float castingVfxScale = 1f;
        [SerializeField] private SfxCue castingSfx;
        [SerializeField] private GameObject castVfxPrefab;
        [SerializeField, Min(0.05f)] private float castVfxLifetime = 1f;
        [SerializeField] private Vector3 castVfxLocalOffset;
        [SerializeField] private Vector3 castVfxLocalEuler;
        [SerializeField, Min(0.01f)] private float castVfxScale = 1f;
        [SerializeField] private SfxCue castSfx;
        [SerializeField] private GameObject impactVfxPrefab;
        [SerializeField, Min(0.05f)] private float impactVfxLifetime = 1.5f;
        [SerializeField] private Vector3 impactVfxLocalOffset;
        [SerializeField] private Vector3 impactVfxLocalEuler;
        [SerializeField, Min(0.01f)] private float impactVfxScale = 1f;
        [SerializeField] private SfxCue impactSfx;
        [SerializeField] private GameObject persistentVfxPrefab;
        [SerializeField] private Vector3 persistentVfxLocalOffset;
        [SerializeField] private Vector3 persistentVfxLocalEuler;
        [SerializeField, Min(0.01f)] private float persistentVfxScale = 1f;
        [SerializeField] private CommanderMarkFeedbackAnchor persistentVfxAnchor = CommanderMarkFeedbackAnchor.WorldPosition;

        public string SkillId => skillId?.Trim() ?? string.Empty;
        public string DisplayName => displayName ?? string.Empty;
        public string Description => description ?? string.Empty;
        public Sprite Icon => icon;
        public CommanderSkillRarity Rarity => rarity;
        public float CastTime => Mathf.Max(0f, castTime);
        public float Cooldown => Mathf.Max(0.1f, cooldown);
        public CommanderSkillTargetingDefinition Targeting => targeting;
        public float TargetRange => targeting == null ? 0f : targeting.Range;
        public System.Collections.Generic.IReadOnlyList<CommanderSkillEffectDefinition> Effects =>
            effects ?? Array.Empty<CommanderSkillEffectDefinition>();
        public CommanderSkillPatternConfig Pattern => pattern ??= new CommanderSkillPatternConfig();
        public GameObject CastingVfxPrefab => castingVfxPrefab;
        public float CastingVfxLifetime => Mathf.Max(0.05f, castingVfxLifetime);
        public Vector3 CastingVfxLocalOffset => castingVfxLocalOffset;
        public Vector3 CastingVfxLocalEuler => castingVfxLocalEuler;
        public float CastingVfxScale => Mathf.Max(0.01f, castingVfxScale);
        public SfxCue CastingSfx => castingSfx;
        public GameObject CastVfxPrefab => castVfxPrefab;
        public float CastVfxLifetime => Mathf.Max(0.05f, castVfxLifetime);
        public Vector3 CastVfxLocalOffset => castVfxLocalOffset;
        public Vector3 CastVfxLocalEuler => castVfxLocalEuler;
        public float CastVfxScale => Mathf.Max(0.01f, castVfxScale);
        public SfxCue CastSfx => castSfx;
        public GameObject ImpactVfxPrefab => impactVfxPrefab;
        public float ImpactVfxLifetime => Mathf.Max(0.05f, impactVfxLifetime);
        public Vector3 ImpactVfxLocalOffset => impactVfxLocalOffset;
        public Vector3 ImpactVfxLocalEuler => impactVfxLocalEuler;
        public float ImpactVfxScale => Mathf.Max(0.01f, impactVfxScale);
        public SfxCue ImpactSfx => impactSfx;
        public GameObject PersistentVfxPrefab => persistentVfxPrefab;
        public Vector3 PersistentVfxLocalOffset => persistentVfxLocalOffset;
        public Vector3 PersistentVfxLocalEuler => persistentVfxLocalEuler;
        public float PersistentVfxScale => Mathf.Max(0.01f, persistentVfxScale);
        public CommanderMarkFeedbackAnchor PersistentVfxAnchor => persistentVfxAnchor;
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

            if (castTime < 0f || float.IsNaN(castTime) || float.IsInfinity(castTime))
            {
                error = $"{SkillId}: cast time is invalid.";
                return false;
            }

            if (cooldown < 0.1f || float.IsNaN(cooldown) || float.IsInfinity(cooldown))
            {
                error = $"{SkillId}: cooldown is invalid.";
                return false;
            }

            if (!IsFinite(castingVfxLocalOffset) || !IsFinite(castingVfxLocalEuler) ||
                !IsFinite(castVfxLocalOffset) || !IsFinite(castVfxLocalEuler) ||
                 !IsFinite(impactVfxLocalOffset) || !IsFinite(impactVfxLocalEuler) ||
                 !IsFinite(persistentVfxLocalOffset) || !IsFinite(persistentVfxLocalEuler) ||
                 !IsFinitePositive(castingVfxScale) || !IsFinitePositive(castVfxScale) ||
                 !IsFinitePositive(impactVfxScale) || !IsFinitePositive(persistentVfxScale))
            {
                error = $"{SkillId}: VFX transform values are invalid.";
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

            if (!Pattern.TryValidate(out error))
            {
                error = $"{SkillId}: pattern is invalid. {error}";
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
            EditorConfigureCommon(
                id,
                title,
                body,
                skillIcon,
                0f,
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

        protected void EditorConfigureCommon(
            string id,
            string title,
            string body,
            Sprite skillIcon,
            float castTimeSeconds,
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
            castTime = Mathf.Max(0f, castTimeSeconds);
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

        public void EditorConfigureFeedbackTransforms(
            Vector3 castOffset,
            Vector3 castEuler,
            float castScale,
            Vector3 impactOffset,
            Vector3 impactEuler,
            float impactScale)
        {
            castVfxLocalOffset = castOffset;
            castVfxLocalEuler = castEuler;
            castVfxScale = Mathf.Max(0.01f, castScale);
            impactVfxLocalOffset = impactOffset;
            impactVfxLocalEuler = impactEuler;
            impactVfxScale = Mathf.Max(0.01f, impactScale);
        }

        public void EditorConfigureCastingFeedback(
            GameObject castingVfx,
            float castingLifetime,
            SfxCue castingCue,
            Vector3 castingOffset,
            Vector3 castingEuler,
            float castingScale)
        {
            castingVfxPrefab = castingVfx;
            castingVfxLifetime = Mathf.Max(0.05f, castingLifetime);
            castingSfx = castingCue;
            castingVfxLocalOffset = castingOffset;
            castingVfxLocalEuler = castingEuler;
            castingVfxScale = Mathf.Max(0.01f, castingScale);
        }

        public void EditorConfigureV2(CommanderSkillRarity skillRarity, CommanderSkillPatternConfig patternConfig)
        {
            rarity = skillRarity;
            pattern = patternConfig ?? new CommanderSkillPatternConfig();
        }

        public void EditorConfigurePersistentFeedback(GameObject prefab, Vector3 offset, Vector3 euler,
            float scale, CommanderMarkFeedbackAnchor anchor)
        {
            persistentVfxPrefab = prefab;
            persistentVfxLocalOffset = offset;
            persistentVfxLocalEuler = euler;
            persistentVfxScale = Mathf.Max(0.01f, scale);
            persistentVfxAnchor = anchor;
        }
#endif

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && IsFinite(value);
        }
    }
}
