using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Combat Profile", fileName = "MC_Monster")]
    public sealed class MonsterCombatProfile : ScriptableObject // 전투 분류와 작은 실행 Definition 연결
    {
        [SerializeField] private MonsterCombatType combatType;
        [SerializeField] private MonsterActionDefinition action;

        public MonsterCombatType CombatType => combatType;
        public MonsterActionDefinition Action => action;

        public bool TryValidate(out string error)
        {
            if (action == null)
            {
                error = $"Monster Combat action is missing. Profile={name}";
                return false;
            }

            if (action.CombatType != combatType)
            {
                error = $"Monster Combat type does not match its action. Profile={name}";
                return false;
            }

            return action.TryValidate(out error);
        }

#if UNITY_EDITOR
        public void EditorConfigure(MonsterCombatType type, MonsterActionDefinition actionDefinition)
        {
            combatType = type;
            action = actionDefinition;
        }
#endif
    }
}
