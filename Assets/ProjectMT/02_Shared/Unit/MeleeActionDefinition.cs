using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public enum MonsterMeleeAttackMode
    {
        Single,
        Area
    }

    public enum MonsterMeleeAreaCenter
    {
        Source,
        PrimaryTarget
    }

    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Action/Melee", fileName = "MeleeAction")]
    public sealed class MeleeActionDefinition : MonsterActionDefinition // 근거리 단일·범위 데이터
    {
        [SerializeField] private MonsterMeleeAttackMode mode;
        [SerializeField] private MonsterMeleeAreaCenter areaCenter = MonsterMeleeAreaCenter.PrimaryTarget;
        [SerializeField, Min(0.01f)] private float areaRadius = 1.5f;
        [SerializeField, Min(1)] private int maxTargets = 4;

        public override MonsterCombatType CombatType => MonsterCombatType.Melee;
        public MonsterMeleeAttackMode Mode => mode;
        public MonsterMeleeAreaCenter AreaCenter => areaCenter;
        public float AreaRadius => Mathf.Max(0.01f, areaRadius);
        public int MaxTargets => mode == MonsterMeleeAttackMode.Single ? 1 : Mathf.Max(1, maxTargets);

        public override bool TryValidate(out string error)
        {
            if (mode == MonsterMeleeAttackMode.Area && (areaRadius <= 0f || maxTargets < 1))
            {
                error = $"Melee Area settings are invalid. Action={name}";
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            MonsterMeleeAttackMode attackMode,
            float radius,
            int targetLimit,
            MonsterMeleeAreaCenter center = MonsterMeleeAreaCenter.PrimaryTarget)
        {
            mode = attackMode;
            areaCenter = center;
            areaRadius = radius;
            maxTargets = targetLimit;
        }
#endif
    }
}
