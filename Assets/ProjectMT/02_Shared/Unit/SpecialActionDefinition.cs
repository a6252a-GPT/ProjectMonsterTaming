using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public enum MonsterSpecialActionMode // 세부 특수 기획 전 첫 확장점
    {
        AreaBuff
    }

    public enum MonsterBuffTargetTeam
    {
        Allies,
        Enemies
    }

    public enum MonsterBuffStackPolicy
    {
        RefreshDuration,
        ReplaceIfStronger
    }

    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Action/Special", fileName = "SpecialAction")]
    public sealed class SpecialActionDefinition : MonsterActionDefinition // 초기 범위 Buff 데이터
    {
        [SerializeField] private string effectId;
        [SerializeField] private MonsterSpecialActionMode mode;
        [SerializeField] private MonsterBuffTargetTeam targetTeam = MonsterBuffTargetTeam.Allies;
        [SerializeField, Min(0.01f)] private float radius = 2f;
        [SerializeField, Min(1)] private int maxTargets = 5;
        [SerializeField, Min(0.01f)] private float duration = 3f;
        [SerializeField] private MonsterBuffStackPolicy stackPolicy = MonsterBuffStackPolicy.RefreshDuration;
        [SerializeField] private MonsterStatModifier modifier;

        public override MonsterCombatType CombatType => MonsterCombatType.Special;
        public string EffectId => effectId ?? string.Empty;
        public MonsterSpecialActionMode Mode => mode;
        public MonsterBuffTargetTeam TargetTeam => targetTeam;
        public float Radius => Mathf.Max(0.01f, radius);
        public int MaxTargets => Mathf.Max(1, maxTargets);
        public float Duration => Mathf.Max(0.01f, duration);
        public MonsterBuffStackPolicy StackPolicy => stackPolicy;
        public MonsterStatModifier Modifier => modifier;

        public override bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(effectId) || radius <= 0f || maxTargets < 1 ||
                duration <= 0f || modifier.IsEmpty)
            {
                error = $"Special Area Buff settings are incomplete. Action={name}";
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            MonsterBuffTargetTeam team,
            float effectRadius,
            int targetLimit,
            float effectDuration,
            MonsterBuffStackPolicy policy,
            MonsterStatModifier statModifier)
        {
            effectId = id?.Trim();
            mode = MonsterSpecialActionMode.AreaBuff;
            targetTeam = team;
            radius = effectRadius;
            maxTargets = targetLimit;
            duration = effectDuration;
            stackPolicy = policy;
            modifier = statModifier;
        }
#endif
    }
}
