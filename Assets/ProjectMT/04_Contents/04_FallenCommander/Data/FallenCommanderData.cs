using ProjectMT.Contents.Framework;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectMT.Contents.FallenCommander
{
    [CreateAssetMenu(
        fileName = "FallenCommanderBossConfig",
        menuName = "ProjectMT/Fallen Commander/Boss Config")]
    public sealed class FallenCommanderBossConfig : ScriptableObject
    {
        [Header("Boss")]
        [SerializeField, Min(0.1f)] private float attackInterval = 2f;
        [SerializeField, Min(0.1f)] private float attackRange = 8f;
        [SerializeField, Min(1f)] private float turnSpeed = 90f;

        [Header("1. 기본 공격 - 원거리 투사체")]
        [SerializeField] private FallenCommanderBasicAttackData projectileBasicAttack = new FallenCommanderBasicAttackData();

        [Header("2. 근접 공격")]
        [FormerlySerializedAs("basicAttack")]
        [SerializeField] private FallenCommanderAttackData meleeAttack = new FallenCommanderAttackData();

        [Header("3. 위치 공격")]
        [SerializeField] private GameObject markStrikeTelegraphPrefab;
        [SerializeField] private FallenCommanderAttackData markStrike = new FallenCommanderAttackData();

        [Header("4. 추적 낙인")]
        [SerializeField] private FallenCommanderAttackData trackingMark = new FallenCommanderAttackData();
        [SerializeField, Min(0.1f)] private float trackingMarkLockDuration = 2f;

        [Header("5. 광역기")]
        [SerializeField] private FallenCommanderAttackData wideBurst = new FallenCommanderAttackData();

        [Header("6. 직선 공격")]
        [SerializeField] private FallenCommanderAttackData lineStrike = new FallenCommanderAttackData();

        [Header("7. 타락의 고리")]
        [SerializeField] private FallenCommanderAttackData corruptionRing = new FallenCommanderAttackData();
        [SerializeField, Min(0.1f)] private float corruptionRingSafeRadius = 3.5f;

        [Header("Attack Selection")]
        [SerializeField, Min(0.1f)] private float closeAttackDistance = 3f;
        [SerializeField, Min(0.1f)] private float lineStrikeMinimumDistance = 5f;
        [SerializeField, Range(-1f, 1f)] private float lineStrikeAlignmentThreshold = 0.7f;

        [Header("Phase Data")]
        [SerializeField] private FallenCommanderPhaseConfig phaseConfig;

        [Header("Death")]
        [SerializeField] private AnimationClip deathMotion;
        [SerializeField, Min(0f)] private float deathMotionDuration;
        [SerializeField] private AnimationClip commanderDeathMotion;
        [SerializeField, Min(0f)] private float commanderDeathMotionDuration;
        [SerializeField, Min(0f)] private float deathResultDelay = 3f;

        [Header("Break")]
        [SerializeField, Min(1f)] private float maxBreakGauge = 100f;
        [SerializeField, Min(0.1f)] private float breakGaugePerHit = 10f;
        [SerializeField, Range(0.01f, 1f)] private float breakGaugeAttackPowerMultiplier = 0.25f;
        [SerializeField, Range(0.01f, 1f)] private float breakGaugePhaseTwoMultiplier = 0.75f;
        [SerializeField, Range(0.01f, 1f)] private float breakGaugePhaseThreeMultiplier = 0.5f;
        [SerializeField, Min(0.1f)] private float breakDuration = 5f;
        [SerializeField, Min(1f)] private float breakDamageMultiplier = 2f;
        [SerializeField] private AnimationClip breakMotion;
        [SerializeField, Min(0f)] private float breakMotionDuration;

        public float AttackInterval => attackInterval;
        public float AttackRange => attackRange;
        public float TurnSpeed => turnSpeed;
        public FallenCommanderBasicAttackData BasicAttack => projectileBasicAttack;
        public FallenCommanderAttackData MeleeAttack => meleeAttack;
        public GameObject MarkStrikeTelegraphPrefab => markStrikeTelegraphPrefab;
        public FallenCommanderAttackData MarkStrike => markStrike;
        public FallenCommanderAttackData TrackingMark => trackingMark;
        public float TrackingMarkLockDuration => trackingMarkLockDuration;
        public FallenCommanderAttackData WideBurst => wideBurst;
        public FallenCommanderAttackData LineStrike => lineStrike;
        public FallenCommanderAttackData CorruptionRing => corruptionRing;
        public float CorruptionRingSafeRadius => corruptionRingSafeRadius;
        public float CloseAttackDistance => closeAttackDistance;
        public float LineStrikeMinimumDistance => lineStrikeMinimumDistance;
        public float LineStrikeAlignmentThreshold => lineStrikeAlignmentThreshold;
        public FallenCommanderPhaseConfig PhaseConfig => phaseConfig;
        public AnimationClip DeathMotion => deathMotion;
        public float DeathMotionDuration => ResolveDuration(deathMotion, deathMotionDuration);
        public AnimationClip CommanderDeathMotion => commanderDeathMotion;
        public float CommanderDeathMotionDuration => ResolveDuration(
            commanderDeathMotion,
            commanderDeathMotionDuration);
        public float DeathResultDelay => deathResultDelay;
        public float MaxBreakGauge => maxBreakGauge;
        public float BreakGaugePerHit => breakGaugePerHit;
        public float BreakGaugeAttackPowerMultiplier => breakGaugeAttackPowerMultiplier;
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
    public sealed class FallenCommanderBasicAttackData
    {
        [SerializeField, Min(0.1f)] private float warningDuration = 0.4f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 8f;
        [SerializeField, Min(0.1f)] private float projectileRadius = 0.5f;
        [SerializeField, Min(0.1f)] private float maxDistance = 12f;
        [SerializeField, Min(0f)] private float projectileHeight = 1f;
        [SerializeField, Min(0.1f)] private float repeatInterval = 4.5f;
        [SerializeField, Min(0f)] private float patternOverlapDelay = 0.5f;

        public float WarningDuration => warningDuration;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileRadius => projectileRadius;
        public float MaxDistance => maxDistance;
        public float ProjectileHeight => projectileHeight;
        public float RepeatInterval => repeatInterval;
        public float PatternOverlapDelay => patternOverlapDelay;
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
        public FallenCommanderResult(int score, float remainingTime, bool cleared)
        {
            Score = score;
            RemainingTime = remainingTime;
            Cleared = cleared;
        }

        public int Score { get; }
        public float RemainingTime { get; }
        public bool Cleared { get; }
    }
}
