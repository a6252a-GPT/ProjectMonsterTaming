using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public enum MonsterAbilityMode // 2·4돌파 Ability의 두 규격
    {
        Passive,
        AutoActive
    }

    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Ability Definition", fileName = "Ability_Monster")]
    public class MonsterAbilityDefinition : ScriptableObject // 실제 효과 구현이 참조할 안정 ID 계약
    {
        [SerializeField] private string abilityId;
        [SerializeField] private string displayName;
        [SerializeField] private MonsterAbilityMode mode;
        [SerializeField] private string triggerPolicyId;

        public string AbilityId => abilityId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? AbilityId : displayName;
        public MonsterAbilityMode Mode => mode;
        public string TriggerPolicyId => triggerPolicyId ?? string.Empty;

        public virtual bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                error = $"Monster Ability ID is blank. Ability={name}";
                return false;
            }

            if (mode == MonsterAbilityMode.AutoActive && string.IsNullOrWhiteSpace(triggerPolicyId))
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
        }
#endif
    }
}
