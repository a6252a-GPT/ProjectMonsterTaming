using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Ascension Profile", fileName = "MA_Monster")]
    public sealed class MonsterAscensionProfile : ScriptableObject // 1·3·5 Stat, 2·4 Ability 고정 슬롯
    {
        [SerializeField] private MonsterStatModifier milestone1;
        [SerializeField] private MonsterAbilityDefinition milestone2;
        [SerializeField] private MonsterStatModifier milestone3;
        [SerializeField] private MonsterAbilityDefinition milestone4;
        [SerializeField] private MonsterStatModifier milestone5;

        public MonsterStatModifier Milestone1 => milestone1;
        public MonsterAbilityDefinition Milestone2 => milestone2;
        public MonsterStatModifier Milestone3 => milestone3;
        public MonsterAbilityDefinition Milestone4 => milestone4;
        public MonsterStatModifier Milestone5 => milestone5;

        public MonsterStatModifier ResolveStatModifier(int ascensionLevel)
        {
            var result = default(MonsterStatModifier);
            if (ascensionLevel >= 1)
            {
                result += milestone1;
            }

            if (ascensionLevel >= 3)
            {
                result += milestone3;
            }

            if (ascensionLevel >= 5)
            {
                result += milestone5;
            }

            return result;
        }

        public string[] ResolveUnlockedAbilityIds(int ascensionLevel)
        {
            if (ascensionLevel >= 4)
            {
                return new[] { milestone2.AbilityId, milestone4.AbilityId };
            }

            if (ascensionLevel >= 2)
            {
                return new[] { milestone2.AbilityId };
            }

            return System.Array.Empty<string>();
        }

        public bool TryValidate(out string error)
        {
            if (milestone1.IsEmpty || milestone3.IsEmpty || milestone5.IsEmpty ||
                milestone1.HasNegativeRate || milestone3.HasNegativeRate || milestone5.HasNegativeRate)
            {
                error = $"Ascension milestones 1, 3 and 5 require non-negative Stat Modifiers. Profile={name}";
                return false;
            }

            if (milestone2 == null || milestone4 == null)
            {
                error = $"Ascension milestones 2 and 4 require Ability Definitions. Profile={name}";
                return false;
            }

            if (!milestone2.TryValidate(out error) || !milestone4.TryValidate(out error))
            {
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            MonsterStatModifier first,
            MonsterAbilityDefinition second,
            MonsterStatModifier third,
            MonsterAbilityDefinition fourth,
            MonsterStatModifier fifth)
        {
            milestone1 = first;
            milestone2 = second;
            milestone3 = third;
            milestone4 = fourth;
            milestone5 = fifth;
        }
#endif
    }
}
