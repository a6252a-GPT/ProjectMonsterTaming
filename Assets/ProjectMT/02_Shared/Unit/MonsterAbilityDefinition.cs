using System;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public enum MonsterAbilityMode // 2·4돌파 Ability의 두 규격
    {
        Passive,
        AutoActive
    }

    public enum MonsterSkillAugmentTarget
    {
        Passive,
        Active
    }

    public enum MonsterSkillAugmentOperation
    {
        MagnitudeMultiplier,
        DurationBonusSeconds,
        CooldownReductionRate,
        TriggerCountReduction,
        MaxTargetsBonus,
        RepeatCountBonus
    }

    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Ability Definition", fileName = "Ability_Monster")]
    public class MonsterAbilityDefinition : ScriptableObject // 실제 효과 구현이 참조할 안정 ID 계약
    {
        [SerializeField] private string abilityId;
        [SerializeField] private string displayName;
        [SerializeField] private MonsterAbilityMode mode;
        [SerializeField] private string triggerPolicyId;
        [SerializeField] private bool skillAugmentConfigured;
        [SerializeField] private MonsterSkillAugmentTarget augmentTarget;
        [SerializeField] private MonsterSkillAugmentOperation augmentOperation;
        [SerializeField, Min(0f)] private float augmentScalarValue = 0.15f;
        [SerializeField, Min(1)] private int augmentIntegerValue = 1;

        public string AbilityId => abilityId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? AbilityId : displayName;
        public MonsterAbilityMode Mode => mode;
        public string TriggerPolicyId => triggerPolicyId ?? string.Empty;
        public bool IsSkillAugment => skillAugmentConfigured;
        public MonsterSkillAugmentTarget AugmentTarget => augmentTarget;
        public MonsterSkillAugmentOperation AugmentOperation => augmentOperation;
        public float AugmentScalarValue => Mathf.Max(0f, augmentScalarValue);
        public int AugmentIntegerValue => Mathf.Max(1, augmentIntegerValue);

        public virtual bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                error = $"Monster Ability ID is blank. Ability={name}";
                return false;
            }

            if (skillAugmentConfigured)
            {
                if (!Enum.IsDefined(typeof(MonsterSkillAugmentTarget), augmentTarget) ||
                    !Enum.IsDefined(typeof(MonsterSkillAugmentOperation), augmentOperation))
                {
                    error = $"Monster skill augment target or operation is invalid. Ability={abilityId}";
                    return false;
                }

                switch (augmentOperation)
                {
                    case MonsterSkillAugmentOperation.MagnitudeMultiplier:
                    case MonsterSkillAugmentOperation.DurationBonusSeconds:
                        if (augmentScalarValue <= 0f || augmentScalarValue > 10f)
                        {
                            error = $"Monster skill augment scalar is invalid. Ability={abilityId}";
                            return false;
                        }
                        break;
                    case MonsterSkillAugmentOperation.CooldownReductionRate:
                        if (augmentScalarValue <= 0f || augmentScalarValue >= 1f)
                        {
                            error = $"Monster skill cooldown reduction must be between 0 and 1. Ability={abilityId}";
                            return false;
                        }
                        break;
                    default:
                        if (augmentIntegerValue < 1 || augmentIntegerValue > 5)
                        {
                            error = $"Monster skill augment integer value is invalid. Ability={abilityId}";
                            return false;
                        }
                        break;
                }
            }
            else if (mode == MonsterAbilityMode.AutoActive && string.IsNullOrWhiteSpace(triggerPolicyId))
            {
                error = $"Auto Active ability requires an explicit Trigger Policy ID. Ability={abilityId}";
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string localizedName,
            MonsterAbilityMode abilityMode,
            string triggerId)
        {
            abilityId = id?.Trim();
            displayName = localizedName?.Trim();
            mode = abilityMode;
            triggerPolicyId = triggerId?.Trim();
            skillAugmentConfigured = false;
        }

        public void EditorConfigureSkillAugment(
            string id,
            string localizedName,
            MonsterSkillAugmentTarget target,
            MonsterSkillAugmentOperation operation,
            float scalarValue,
            int integerValue)
        {
            abilityId = id?.Trim();
            displayName = localizedName?.Trim();
            mode = MonsterAbilityMode.Passive;
            triggerPolicyId = string.Empty;
            skillAugmentConfigured = true;
            augmentTarget = target;
            augmentOperation = operation;
            augmentScalarValue = Mathf.Max(0f, scalarValue);
            augmentIntegerValue = Mathf.Max(1, integerValue);
        }
#endif
    }
}
