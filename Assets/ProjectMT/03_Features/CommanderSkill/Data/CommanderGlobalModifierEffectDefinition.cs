using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    [CreateAssetMenu(menuName = "ProjectMT/Commander Skill/Effects/Global Modifier", fileName = "CSEffect_GlobalModifier")]
    public sealed class CommanderGlobalModifierEffectDefinition : CommanderSkillEffectDefinition
    {
        [SerializeField, Min(0.01f)] private float duration = 1f;
        [SerializeField, Min(0.01f)] private float markRequiredHitsMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float markTriggerDamageMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float cooldownRecoveryMultiplier = 1f;

        public float Duration => Mathf.Max(0.01f, duration);
        public float MarkRequiredHitsMultiplier => Mathf.Max(0.01f, markRequiredHitsMultiplier);
        public float MarkTriggerDamageMultiplier => Mathf.Max(0.01f, markTriggerDamageMultiplier);
        public float CooldownRecoveryMultiplier => Mathf.Max(0.01f, cooldownRecoveryMultiplier);

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error)) return false;
            if (!IsPositive(duration) || !IsPositive(markRequiredHitsMultiplier) ||
                !IsPositive(markTriggerDamageMultiplier) || !IsPositive(cooldownRecoveryMultiplier))
            {
                error = $"{EffectId}: global modifier values are invalid.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static bool IsPositive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

#if UNITY_EDITOR
        public void EditorConfigure(string id, float seconds, float requiredHitsMultiplier,
            float triggerDamageMultiplier, float recoveryMultiplier)
        {
            EditorConfigureId(id);
            duration = Mathf.Max(0.01f, seconds);
            markRequiredHitsMultiplier = Mathf.Max(0.01f, requiredHitsMultiplier);
            markTriggerDamageMultiplier = Mathf.Max(0.01f, triggerDamageMultiplier);
            cooldownRecoveryMultiplier = Mathf.Max(0.01f, recoveryMultiplier);
        }
#endif
    }
}
