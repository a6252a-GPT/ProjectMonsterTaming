using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public enum UnitTargetPriority // 공용 전투 대상 우선순위
    {
        Nearest,
        LowestHealth,
        RangedFirst
    }

    public readonly struct UnitCombatBehavior // 콘텐츠가 유닛에 주입하는 작은 전투 판단값
    {
        public UnitCombatBehavior(
            UnitTargetPriority targetPriority,
            float preferredRangeRatio = 1f,
            float retreatRangeRatio = 0f,
            float retargetInterval = 0.2f,
            float targetLoadPenalty = 0f)
        {
            TargetPriority = targetPriority;
            PreferredRangeRatio = Mathf.Clamp(preferredRangeRatio, 0.2f, 1f);
            RetreatRangeRatio = Mathf.Clamp(retreatRangeRatio, 0f, PreferredRangeRatio - 0.05f);
            RetargetInterval = Mathf.Clamp(retargetInterval, 0.08f, 1f);
            TargetLoadPenalty = Mathf.Clamp(targetLoadPenalty, 0f, 10f);
        }

        public UnitTargetPriority TargetPriority { get; }
        public float PreferredRangeRatio { get; }
        public float RetreatRangeRatio { get; }
        public float RetargetInterval { get; }
        public float TargetLoadPenalty { get; }
        public bool UsesRetreat => RetreatRangeRatio > 0f;

        public static UnitCombatBehavior Default => new UnitCombatBehavior(UnitTargetPriority.Nearest);
    }
}
