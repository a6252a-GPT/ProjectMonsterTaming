using ProjectMT.Contents.Framework;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    [CreateAssetMenu(
        fileName = "FallenCommanderBossConfig",
        menuName = "ProjectMT/Giant Spellbook/Fallen Commander Boss Config")]
    public sealed class FallenCommanderBossConfig : ScriptableObject
    {
        [Header("Boss")]
        [SerializeField, Min(0.1f)] private float attackInterval = 2f;
        [SerializeField, Min(0.1f)] private float attackRange = 8f;
        [SerializeField, Min(1f)] private float turnSpeed = 90f;

        [Header("Basic Attack")]
        [SerializeField] private FallenCommanderAttackData basicAttack = new FallenCommanderAttackData();

        [Header("Mark Strike")]
        [SerializeField] private GameObject markStrikeTelegraphPrefab;
        [SerializeField] private FallenCommanderAttackData markStrike = new FallenCommanderAttackData();

        [Header("Wide Burst")]
        [SerializeField] private FallenCommanderAttackData wideBurst = new FallenCommanderAttackData();

        [Header("Line Strike")]
        [SerializeField] private FallenCommanderAttackData lineStrike = new FallenCommanderAttackData();

        [Header("Death")]
        [SerializeField] private AnimationClip deathMotion;
        [SerializeField, Min(0f)] private float deathMotionDuration;
        [SerializeField, Min(0f)] private float deathResultDelay = 3f;

        [Header("Break")]
        [SerializeField, Min(1f)] private float maxBreakGauge = 100f;
        [SerializeField, Min(0.1f)] private float breakGaugePerHit = 10f;
        [SerializeField, Range(0.01f, 1f)] private float breakGaugeAttackPowerMultiplier = 0.25f;
        [SerializeField, Range(0.01f, 1f)] private float breakGaugePhaseTwoHealthRatio = 0.7f;
        [SerializeField, Range(0.01f, 1f)] private float breakGaugePhaseThreeHealthRatio = 0.4f;
        [SerializeField, Range(0.01f, 1f)] private float breakGaugePhaseTwoMultiplier = 0.75f;
        [SerializeField, Range(0.01f, 1f)] private float breakGaugePhaseThreeMultiplier = 0.5f;
        [SerializeField, Min(0.1f)] private float breakDuration = 5f;
        [SerializeField, Min(1f)] private float breakDamageMultiplier = 2f;
        [SerializeField] private AnimationClip breakMotion;
        [SerializeField, Min(0f)] private float breakMotionDuration;

        public float AttackInterval => attackInterval;
        public float AttackRange => attackRange;
        public float TurnSpeed => turnSpeed;
        public FallenCommanderAttackData BasicAttack => basicAttack;
        public GameObject MarkStrikeTelegraphPrefab => markStrikeTelegraphPrefab;
        public FallenCommanderAttackData MarkStrike => markStrike;
        public FallenCommanderAttackData WideBurst => wideBurst;
        public FallenCommanderAttackData LineStrike => lineStrike;
        public AnimationClip DeathMotion => deathMotion;
        public float DeathMotionDuration => ResolveDuration(deathMotion, deathMotionDuration);
        public float DeathResultDelay => deathResultDelay;
        public float MaxBreakGauge => maxBreakGauge;
        public float BreakGaugePerHit => breakGaugePerHit;
        public float BreakGaugeAttackPowerMultiplier => breakGaugeAttackPowerMultiplier;
        public float BreakGaugePhaseTwoHealthRatio => breakGaugePhaseTwoHealthRatio;
        public float BreakGaugePhaseThreeHealthRatio => breakGaugePhaseThreeHealthRatio;
        public float BreakGaugePhaseTwoMultiplier => breakGaugePhaseTwoMultiplier;
        public float BreakGaugePhaseThreeMultiplier => breakGaugePhaseThreeMultiplier;
        public float BreakDuration => breakDuration;
        public float BreakDamageMultiplier => breakDamageMultiplier;
        public AnimationClip BreakMotion => breakMotion;
        public float BreakMotionDuration => ResolveDuration(breakMotion, breakMotionDuration);

        private static float ResolveDuration(AnimationClip motion, float overrideDuration)
        {
            return overrideDuration > 0f
                ? overrideDuration
                : motion == null
                    ? 0f
                    : Mathf.Max(0.01f, motion.length);
        }
    }

    [System.Serializable]
    public sealed class FallenCommanderAttackData
    {
        [SerializeField] private AnimationClip preCastMotion;
        [SerializeField, Min(0f)] private float preCastMotionDuration;
        [SerializeField] private AnimationClip castMotion;
        [SerializeField, Min(0f)] private float castMotionDuration;
        [SerializeField, Min(0.1f)] private float warningDuration = 2f;
        [SerializeField, Min(0.1f)] private float radius = 2.5f;
        [SerializeField, Min(0.1f)] private float width = 2f;
        [SerializeField, Min(0.1f)] private float length = 8f;
        [SerializeField, Min(0f)] private float stunDuration;

        public AnimationClip PreCastMotion => preCastMotion;
        public AnimationClip CastMotion => castMotion;
        public float PreCastMotionDuration => ResolveDuration(preCastMotion, preCastMotionDuration);
        public float CastMotionDuration => ResolveDuration(castMotion, castMotionDuration);
        public float WarningDuration => warningDuration;
        public float Radius => radius;
        public float Width => width;
        public float Length => length;
        public float StunDuration => stunDuration;

        private static float ResolveDuration(AnimationClip motion, float overrideDuration)
        {
            return overrideDuration > 0f
                ? overrideDuration
                : motion == null
                    ? 0f
                    : Mathf.Max(0.01f, motion.length);
        }
    }

    public sealed class FallenCommanderStartData : IContentStartData
    {
    }

    public sealed class FallenCommanderResult : IContentResultData
    {
        public FallenCommanderResult(int score, float remainingTime)
        {
            Score = score;
            RemainingTime = remainingTime;
        }

        public int Score { get; }
        public float RemainingTime { get; }
    }
}
