using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectMT.Shared.Unit
{
    public enum MonsterImpactStrength // 공격자가 만드는 충격 강도
    {
        Standard,
        Light,
        Heavy
    }

    public enum MonsterReactionWeight // 피격자의 시각 반동 체급
    {
        Standard,
        Light,
        Heavy
    }

    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Combat Profile", fileName = "MC_Monster")]
    public sealed class MonsterCombatProfile : ScriptableObject // 전투 분류와 작은 실행 Definition 연결
    {
        [SerializeField] private MonsterCombatType combatType;
        [SerializeField] private MonsterActionDefinition action;
        [FormerlySerializedAs("impactWeight")]
        [SerializeField] private MonsterImpactStrength impactStrength = MonsterImpactStrength.Standard;
        [SerializeField] private MonsterReactionWeight reactionWeight = MonsterReactionWeight.Standard;

        public MonsterCombatType CombatType => combatType;
        public MonsterActionDefinition Action => action;
        public MonsterImpactStrength ImpactStrength => impactStrength;
        public MonsterReactionWeight ReactionWeight => reactionWeight;

        public bool TryValidate(out string error)
        {
            if (!System.Enum.IsDefined(typeof(MonsterImpactStrength), impactStrength) ||
                !System.Enum.IsDefined(typeof(MonsterReactionWeight), reactionWeight))
            {
                error = $"Monster impact classification is invalid. Profile={name}";
                return false;
            }

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

        public void EditorSetImpact(MonsterImpactStrength strength, MonsterReactionWeight weight)
        {
            impactStrength = strength;
            reactionWeight = weight;
        }
#endif
    }
}
