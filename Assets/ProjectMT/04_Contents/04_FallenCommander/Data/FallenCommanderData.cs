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

        [Header("8. 충전 광역기")]
        [SerializeField, InspectorName("공격 범위 프리팹")] private GameObject finalChargeTelegraphPrefab;

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
        public FallenCommanderAttackData MarkStrike => markStrike;
        public FallenCommanderAttackData TrackingMark => trackingMark;
        public float TrackingMarkLockDuration => trackingMarkLockDuration;
        public FallenCommanderAttackData WideBurst => wideBurst;
        public FallenCommanderAttackData LineStrike => lineStrike;
        public FallenCommanderAttackData CorruptionRing => corruptionRing;
        public float CorruptionRingSafeRadius => corruptionRingSafeRadius;
        public GameObject FinalChargeTelegraphPrefab => finalChargeTelegraphPrefab;
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
        [SerializeField, InspectorName("공격 범위 프리팹")] private GameObject telegraphPrefab;
        [SerializeField, InspectorName("투사체 프리팹")]
        [Tooltip("비어 있으면 안전장치로 Unity 기본 구체를 사용합니다.")]
        private GameObject projectilePrefab;
        [SerializeField] private FallenCommanderAttackEffectData effects = new();
        [SerializeField, Min(0.1f)] private float warningDuration = 0.4f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 8f;
        [SerializeField, Min(0.1f)] private float projectileRadius = 0.5f;
        [SerializeField, Min(0.1f)] private float maxDistance = 12f;
        [SerializeField, Min(0f)] private float projectileHeight = 1f;
        [SerializeField, Min(0.1f)] private float repeatInterval = 4.5f;
        [SerializeField, Min(0f)] private float patternOverlapDelay = 0.5f;

        public GameObject TelegraphPrefab => telegraphPrefab;
        public GameObject ProjectilePrefab => projectilePrefab;
        public FallenCommanderAttackEffectData Effects => effects;
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
        [SerializeField, InspectorName("공격 범위 프리팹")] private GameObject telegraphPrefab;
        [SerializeField] private FallenCommanderAttackEffectData effects = new();
        [SerializeField] private AnimationClip preCastMotion;
        [SerializeField, InspectorName("시전 모션 속도"), Min(0.01f)] private float preCastMotionSpeed = 1f;
        [SerializeField, Min(0f)] private float preCastMotionDuration;
        [SerializeField] private AnimationClip castMotion;
        [SerializeField, InspectorName("공격 모션 속도"), Min(0.01f)] private float castMotionSpeed = 1f;
        [SerializeField, Min(0f)] private float castMotionDuration;
        [SerializeField, Min(0.1f)] private float warningDuration = 2f;
        [SerializeField, Min(0.1f)] private float radius = 2.5f;
        [SerializeField, Min(0.1f)] private float width = 2f;
        [SerializeField, Min(0.1f)] private float length = 8f;
        [SerializeField, Min(0f)] private float stunDuration;

        public GameObject TelegraphPrefab => telegraphPrefab;
        public FallenCommanderAttackEffectData Effects => effects;
        public AnimationClip PreCastMotion => preCastMotion;
        public AnimationClip CastMotion => castMotion;
        public float PreCastMotionSpeed => Mathf.Max(0.01f, preCastMotionSpeed);
        public float CastMotionSpeed => Mathf.Max(0.01f, castMotionSpeed);
        public float PreCastMotionDuration => ResolveDuration(
            preCastMotion,
            preCastMotionDuration,
            PreCastMotionSpeed);
        public float CastMotionDuration => ResolveDuration(
            castMotion,
            castMotionDuration,
            CastMotionSpeed);
        public float WarningDuration => warningDuration;
        public float Radius => radius;
        public float Width => width;
        public float Length => length;
        public float StunDuration => stunDuration;

        private static float ResolveDuration(
            AnimationClip motion,
            float overrideDuration,
            float playbackSpeed)
        {
            return overrideDuration > 0f
                ? overrideDuration
                : motion == null
                    ? 0f
                    : Mathf.Max(0.01f, motion.length / Mathf.Max(0.01f, playbackSpeed));
        }
    }

    [System.Serializable]
    public sealed class FallenCommanderAttackEffectData
    {
        [SerializeField, InspectorName("시전 VFX")] private GameObject startVfxPrefab;
        [SerializeField, InspectorName("시전 VFX 유지시간 (0 = 자동)"), Min(0f)] private float startVfxDuration;
        [SerializeField, InspectorName("적중 VFX")] private GameObject resolveVfxPrefab;
        [SerializeField, InspectorName("적중 VFX 유지시간 (0 = 자동)"), Min(0f)] private float resolveVfxDuration;
        [SerializeField, InspectorName("시전 SFX")] private AudioClip startSfx;
        [SerializeField, InspectorName("시전 SFX 유지시간 (0 = 자동)"), Min(0f)] private float startSfxDuration;
        [SerializeField, InspectorName("적중 SFX")] private AudioClip resolveSfx;
        [SerializeField, InspectorName("적중 SFX 유지시간 (0 = 자동)"), Min(0f)] private float resolveSfxDuration;
        [SerializeField, InspectorName("SFX 볼륨"), Range(0f, 1f)] private float sfxVolume = 1f;

        public GameObject StartVfxPrefab => startVfxPrefab;
        public float StartVfxDuration => startVfxDuration;
        public GameObject ResolveVfxPrefab => resolveVfxPrefab;
        public float ResolveVfxDuration => resolveVfxDuration;
        public AudioClip StartSfx => startSfx;
        public float StartSfxDuration => startSfxDuration;
        public AudioClip ResolveSfx => resolveSfx;
        public float ResolveSfxDuration => resolveSfxDuration;
        public float SfxVolume => sfxVolume;
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
