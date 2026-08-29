using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    public static class CastleRaidSupportUtility // 지원 행동을 설명 가능한 점수로 비교
    {
        public static float ScoreHeal(
            float healthRatio,
            float recentDamagePerSecond,
            float maxHealth,
            float estimatedTimeToLive,
            CastleRaidSupportFocus focus,
            bool alreadyClaimed)
        {
            var missingRatio = 1f - Mathf.Clamp01(healthRatio);
            var pressure = Mathf.Clamp01(recentDamagePerSecond / Mathf.Max(1f, maxHealth * 0.2f));
            var emergency = estimatedTimeToLive <= 3f ? 1f : estimatedTimeToLive <= 6f ? 0.45f : 0f;
            var focusWeight = focus == CastleRaidSupportFocus.Recovery ? 1.45f :
                focus == CastleRaidSupportFocus.Adaptive ? 1f : 0.62f;
            var claimWeight = alreadyClaimed ? 0.25f : 1f;
            return (missingRatio * 1.25f + pressure + emergency) * focusWeight * claimWeight;
        }

        public static float ScoreDefenseBuff(
            float healthRatio,
            float recentDamagePerSecond,
            float maxHealth,
            bool buffActive,
            CastleRaidSupportFocus focus,
            bool alreadyClaimed)
        {
            var pressure = Mathf.Clamp01(recentDamagePerSecond / Mathf.Max(1f, maxHealth * 0.16f));
            var danger = (1f - Mathf.Clamp01(healthRatio)) * 0.55f + pressure * 1.25f;
            var focusWeight = focus == CastleRaidSupportFocus.DefenseBuff ? 1.45f :
                focus == CastleRaidSupportFocus.Adaptive ? 1f : 0.62f;
            var activeWeight = buffActive ? 0.12f : 1f;
            var claimWeight = alreadyClaimed ? 0.3f : 1f;
            return danger * focusWeight * activeWeight * claimWeight;
        }

        public static float ScoreAttackBuff(
            float estimatedDamagePerSecond,
            bool hasCombatTarget,
            bool buffActive,
            CastleRaidSupportFocus focus,
            bool alreadyClaimed)
        {
            if (!hasCombatTarget)
            {
                return 0f;
            }

            var contribution = Mathf.Clamp01(estimatedDamagePerSecond / 25f);
            var focusWeight = focus == CastleRaidSupportFocus.AttackBuff ? 1.45f :
                focus == CastleRaidSupportFocus.Adaptive ? 1f : 0.62f;
            var activeWeight = buffActive ? 0.12f : 1f;
            var claimWeight = alreadyClaimed ? 0.3f : 1f;
            return (0.35f + contribution) * focusWeight * activeWeight * claimWeight;
        }
    }

    public readonly struct CastleRaidSupportDecision
    {
        public CastleRaidSupportDecision(
            CastleRaidSupportAction action,
            CastleAssaultUnit target,
            CastleRaidAIProfile profile,
            float score)
        {
            Action = action;
            Target = target;
            Profile = profile;
            Score = score;
        }

        public CastleRaidSupportAction Action { get; }
        public CastleAssaultUnit Target { get; }
        public CastleRaidAIProfile Profile { get; }
        public float Score { get; }
        public bool IsValid => Action != CastleRaidSupportAction.None && Target != null && Target.IsAlive && Profile != null;
    }
}
