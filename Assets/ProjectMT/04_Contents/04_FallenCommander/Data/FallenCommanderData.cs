using ProjectMT.Contents.Framework;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectMT.Contents.FallenCommander
{
    [CreateAssetMenu(
        fileName = "FallenCommanderBossConfig",
        menuName = "ProjectMT/타락한 과거의 군단장/보스 설정 데이터")]
    public sealed class FallenCommanderBossConfig : ScriptableObject
    {
        [Header("보스 기본 설정")]
        [SerializeField, InspectorName("기본 최대 체력"), Min(1f)]
        private float baseMaxHealth = 2000f;
        [SerializeField, InspectorName("기본 방어력"), Min(0f)]
        private float baseDefense = 10f;
        [SerializeField, InspectorName("기본 이동속도"), Min(0f)]
        private float baseMoveSpeed = 1.6f;
        [SerializeField, InspectorName("공격 간격"), Min(0.1f)] private float attackInterval = 2f;
        [SerializeField, InspectorName("공격 가능 거리"), Min(0.1f)] private float attackRange = 8f;
        [SerializeField, InspectorName("회전 속도"), Min(1f)] private float turnSpeed = 90f;

        [Header("1. 기본 공격 - 원거리 투사체")]
        [SerializeField, InspectorName("기본 공격 설정")]
        private FallenCommanderBasicAttackData projectileBasicAttack = new FallenCommanderBasicAttackData();

        [Header("2. 근접 공격")]
        [FormerlySerializedAs("basicAttack")]
        [SerializeField, InspectorName("근접 공격 설정")]
        private FallenCommanderAttackData meleeAttack = new FallenCommanderAttackData();

        [Header("3. 위치 공격")]
        [SerializeField, InspectorName("위치 공격 설정")]
        private FallenCommanderAttackData markStrike = new FallenCommanderAttackData();

        [Header("4. 추적 낙인")]
        [SerializeField, InspectorName("추적 낙인 설정")]
        private FallenCommanderAttackData trackingMark = new FallenCommanderAttackData();
        [SerializeField, InspectorName("추적 종료 전 위치 고정시간"), Min(0.1f)]
        private float trackingMarkLockDuration = 2f;

        [Header("5. 블랙홀")]
        [FormerlySerializedAs("wideBurst")]
        [SerializeField, InspectorName("블랙홀 공격 설정")]
        private FallenCommanderAttackData blackHole = new FallenCommanderAttackData();
        [SerializeField, InspectorName("활성 유지시간"), Min(0.1f)]
        private float blackHoleActiveDuration = 3.5f;
        [SerializeField, InspectorName("중심 피해 범위"), Min(0.1f)]
        private float blackHoleCoreRadius = 1.2f;
        [SerializeField, InspectorName("생성 최소 거리"), Min(0f)]
        private float blackHoleSpawnMinDistance = 1.5f;
        [SerializeField, InspectorName("생성 최대 거리"), Min(0.1f)]
        private float blackHoleSpawnMaxDistance = 3.5f;
        [SerializeField, InspectorName("바깥쪽 흡입 속도"), Min(0f)]
        private float blackHoleOuterPullSpeed = 1.5f;
        [SerializeField, InspectorName("중심부 흡입 속도"), Min(0f)]
        private float blackHoleInnerPullSpeed = 4f;
        [SerializeField, InspectorName("중심 거리별 흡입 강도")]
        private AnimationCurve blackHolePullStrengthCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, InspectorName("생성 가능 영역 반경")]
        private Vector2 blackHoleArenaHalfExtents = new Vector2(6f, 4f);
        [SerializeField, InspectorName("종료 연출 (시각 효과 / 효과음)")]
        private FallenCommanderAttackEffectData blackHoleEndEffects = new();

        [Header("6. 직선 공격")]
        [SerializeField, InspectorName("직선 공격 설정")]
        private FallenCommanderAttackData lineStrike = new FallenCommanderAttackData();

        [Header("7. 타락의 고리")]
        [SerializeField, InspectorName("타락의 고리 설정")]
        private FallenCommanderAttackData corruptionRing = new FallenCommanderAttackData();
        [SerializeField, InspectorName("안전지대 반지름"), Min(0.1f)]
        private float corruptionRingSafeRadius = 3.5f;

        [Header("8. 충전 광역기")]
        [SerializeField, InspectorName("발동 체력 비율"), Range(0.01f, 1f)]
        private float finalChargeHealthRatio = 0.3f;
        [SerializeField, InspectorName("충전 시간"), Min(0.1f)]
        private float finalChargeDuration = 12f;
        [SerializeField, InspectorName("공격 범위 오브젝트")] private GameObject finalChargeTelegraphPrefab;
        [SerializeField, InspectorName("충전 완료 유지시간"), Min(0f)]
        private float finalChargeTelegraphHoldDuration = 0.25f;
        [SerializeField, InspectorName("공격 범위 반지름"), Min(0.1f)]
        private float finalChargeRadius = 10f;
        [SerializeField, InspectorName("연출 (시각 효과 / 효과음)")]
        private FallenCommanderAttackEffectData finalChargeEffects = new();
        [SerializeField, InspectorName("시전 모션")] private AnimationClip finalChargePreCastMotion;
        [SerializeField, InspectorName("시전 모션 속도"), Min(0.01f)]
        private float finalChargePreCastMotionSpeed = 1f;
        [SerializeField, InspectorName("시전 모션 시작 지점"), Range(0f, 1f)]
        private float finalChargePreCastMotionStart;
        [SerializeField, InspectorName("시전 모션 종료 지점"), Range(0f, 1f)]
        private float finalChargePreCastMotionEnd = 1f;
        [SerializeField, InspectorName("공격 모션")] private AnimationClip finalChargeCastMotion;
        [SerializeField, InspectorName("공격 모션 속도"), Min(0.01f)]
        private float finalChargeCastMotionSpeed = 1f;
        [SerializeField, InspectorName("공격 모션 시작 지점"), Range(0f, 1f)]
        private float finalChargeCastMotionStart;
        [SerializeField, InspectorName("공격 모션 종료 지점"), Range(0f, 1f)]
        private float finalChargeCastMotionEnd = 1f;
        [SerializeField, InspectorName("시전 연출 위치 오프셋")]
        private Vector3 finalChargeStartEffectOffset = new Vector3(0f, 2f, 0f);

        [Header("9. 제한시간 전멸기")]
        [SerializeField, InspectorName("전멸기 설정")]
        private FallenCommanderTimeoutWipeData timeoutWipe = new();

        [Header("공격 선택 조건")]
        [SerializeField, InspectorName("근접 공격 선택 거리"), Min(0.1f)]
        private float closeAttackDistance = 3f;
        [SerializeField, InspectorName("직선 공격 최소 거리"), Min(0.1f)]
        private float lineStrikeMinimumDistance = 5f;
        [SerializeField, InspectorName("직선 공격 정면 판정 기준"), Range(-1f, 1f)]
        private float lineStrikeAlignmentThreshold = 0.7f;

        [Header("페이즈 데이터")]
        [SerializeField, InspectorName("페이즈 설정 파일")]
        private FallenCommanderPhaseConfig phaseConfig;

        [Header("사망 연출")]
        [SerializeField, InspectorName("보스 사망 모션")] private AnimationClip deathMotion;
        [SerializeField, InspectorName("보스 사망 모션 재생시간 (0 = 자동)"), Min(0f)]
        private float deathMotionDuration;
        [SerializeField, InspectorName("군단장 사망 모션")] private AnimationClip commanderDeathMotion;
        [SerializeField, InspectorName("군단장 사망 모션 재생시간 (0 = 자동)"), Min(0f)]
        private float commanderDeathMotionDuration;
        [SerializeField, InspectorName("사망 후 결과창 대기시간"), Min(0f)]
        private float deathResultDelay = 3f;

        [Header("브레이크")]
        [SerializeField, InspectorName("최대 브레이크 게이지"), Min(1f)]
        private float maxBreakGauge = 100f;
        [SerializeField, InspectorName("피격 1회당 브레이크 게이지"), Min(0.1f)]
        private float breakGaugePerHit = 10f;
        [SerializeField, InspectorName("공격력 반영 배율"), Range(0.01f, 1f)]
        private float breakGaugeAttackPowerMultiplier = 0.25f;
        [SerializeField, InspectorName("2페이즈 브레이크 획득 배율"), Range(0.01f, 1f)]
        private float breakGaugePhaseTwoMultiplier = 0.75f;
        [SerializeField, InspectorName("3페이즈 브레이크 획득 배율"), Range(0.01f, 1f)]
        private float breakGaugePhaseThreeMultiplier = 0.5f;
        [SerializeField, InspectorName("브레이크 지속시간"), Min(0.1f)]
        private float breakDuration = 5f;
        [SerializeField, InspectorName("브레이크 중 받는 피해 배율"), Min(1f)]
        private float breakDamageMultiplier = 2f;
        [SerializeField, InspectorName("브레이크 모션")] private AnimationClip breakMotion;
        [SerializeField, InspectorName("브레이크 모션 재생시간 (0 = 자동)"), Min(0f)]
        private float breakMotionDuration;

        public float BaseMaxHealth => Mathf.Max(1f, baseMaxHealth);
        public float BaseDefense => Mathf.Max(0f, baseDefense);
        public float BaseMoveSpeed => Mathf.Max(0f, baseMoveSpeed);
        public float AttackInterval => attackInterval;
        public float AttackRange => attackRange;
        public float TurnSpeed => turnSpeed;
        public FallenCommanderBasicAttackData BasicAttack => projectileBasicAttack;
        public FallenCommanderAttackData MeleeAttack => meleeAttack;
        public FallenCommanderAttackData MarkStrike => markStrike;
        public FallenCommanderAttackData TrackingMark => trackingMark;
        public float TrackingMarkLockDuration => trackingMarkLockDuration;
        public FallenCommanderAttackData BlackHole => blackHole;
        public float BlackHoleActiveDuration => blackHoleActiveDuration;
        public float BlackHoleCoreRadius => blackHoleCoreRadius;
        public float BlackHoleSpawnMinDistance => blackHoleSpawnMinDistance;
        public float BlackHoleSpawnMaxDistance => blackHoleSpawnMaxDistance;
        public float BlackHoleOuterPullSpeed => blackHoleOuterPullSpeed;
        public float BlackHoleInnerPullSpeed => blackHoleInnerPullSpeed;
        public AnimationCurve BlackHolePullStrengthCurve => blackHolePullStrengthCurve;
        public Vector2 BlackHoleArenaHalfExtents => blackHoleArenaHalfExtents;
        public FallenCommanderAttackEffectData BlackHoleEndEffects => blackHoleEndEffects;
        public FallenCommanderAttackData LineStrike => lineStrike;
        public FallenCommanderAttackData CorruptionRing => corruptionRing;
        public float CorruptionRingSafeRadius => corruptionRingSafeRadius;
        public float FinalChargeHealthRatio => Mathf.Clamp(finalChargeHealthRatio, 0.01f, 1f);
        public float FinalChargeDuration => Mathf.Max(0.1f, finalChargeDuration);
        public GameObject FinalChargeTelegraphPrefab => finalChargeTelegraphPrefab;
        public float FinalChargeTelegraphHoldDuration =>
            Mathf.Max(0f, finalChargeTelegraphHoldDuration);
        public float FinalChargeRadius => Mathf.Max(0.1f, finalChargeRadius);
        public FallenCommanderAttackEffectData FinalChargeEffects => finalChargeEffects;
        public AnimationClip FinalChargePreCastMotion => finalChargePreCastMotion;
        public float FinalChargePreCastMotionSpeed => Mathf.Max(0.01f, finalChargePreCastMotionSpeed);
        public float FinalChargePreCastMotionStart => ResolveStart(finalChargePreCastMotionStart);
        public float FinalChargePreCastMotionEnd => ResolveEnd(
            finalChargePreCastMotionStart,
            finalChargePreCastMotionEnd);
        public AnimationClip FinalChargeCastMotion => finalChargeCastMotion;
        public float FinalChargeCastMotionSpeed => Mathf.Max(0.01f, finalChargeCastMotionSpeed);
        public float FinalChargeCastMotionStart => ResolveStart(finalChargeCastMotionStart);
        public float FinalChargeCastMotionEnd => ResolveEnd(
            finalChargeCastMotionStart,
            finalChargeCastMotionEnd);
        public float FinalChargeCastMotionDuration => ResolveMotionDuration(
            finalChargeCastMotion,
            FinalChargeCastMotionSpeed,
            FinalChargeCastMotionStart,
            FinalChargeCastMotionEnd);
        public Vector3 FinalChargeStartEffectOffset => finalChargeStartEffectOffset;
        public FallenCommanderTimeoutWipeData TimeoutWipe => timeoutWipe;
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

        private static float ResolveMotionDuration(
            AnimationClip motion,
            float playbackSpeed,
            float start,
            float end)
        {
            return motion == null
                ? 0f
                : Mathf.Max(
                    0.01f,
                    motion.length * (end - start) / Mathf.Max(0.01f, playbackSpeed));
        }

        private static float ResolveStart(float start)
        {
            return Mathf.Clamp(start, 0f, 0.999f);
        }

        private static float ResolveEnd(float start, float end)
        {
            if (start <= 0f && end <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp(end, ResolveStart(start) + 0.001f, 1f);
        }
    }

    public enum FallenCommanderEffectAnchor
    {
        [InspectorName("공격 지점")]
        AttackPosition,
        [InspectorName("보스 위치")]
        Boss,
        [InspectorName("군단장 위치")]
        Commander,
        [InspectorName("투사체 위치")]
        Projectile
    }

    [System.Serializable]
    public sealed class FallenCommanderBasicAttackData
    {
        [SerializeField, InspectorName("공격 범위 오브젝트")] private GameObject telegraphPrefab;
        [SerializeField, InspectorName("공격 전 경고시간"), Min(0.1f)]
        private float warningDuration = 0.4f;
        [SerializeField, InspectorName("충전 완료 유지시간"), Min(0f)]
        private float telegraphHoldDuration = 0.25f;
        [SerializeField, InspectorName("투사체 오브젝트")]
        [Tooltip("비어 있으면 안전장치로 기본 구체를 사용합니다.")]
        private GameObject projectilePrefab;
        [SerializeField, InspectorName("연출 (시각 효과 / 효과음)")]
        private FallenCommanderAttackEffectData effects = new();
        [SerializeField, InspectorName("투사체 이동 속도"), Min(0.1f)]
        private float projectileSpeed = 8f;
        [SerializeField, InspectorName("투사체 피격 반지름"), Min(0.1f)]
        private float projectileRadius = 0.5f;
        [SerializeField, InspectorName("투사체 최대 이동거리"), Min(0.1f)]
        private float maxDistance = 12f;
        [SerializeField, InspectorName("투사체 생성 높이"), Min(0f)]
        private float projectileHeight = 1f;
        [SerializeField, InspectorName("기본 공격 반복 간격"), Min(0.1f)]
        private float repeatInterval = 4.5f;
        [SerializeField, InspectorName("다른 패턴 시작 후 대기시간"), Min(0f)]
        private float patternOverlapDelay = 0.5f;

        public GameObject TelegraphPrefab => telegraphPrefab;
        public GameObject ProjectilePrefab => projectilePrefab;
        public FallenCommanderAttackEffectData Effects => effects;
        public float WarningDuration => warningDuration;
        public float TelegraphHoldDuration => Mathf.Max(0f, telegraphHoldDuration);
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
        [SerializeField, InspectorName("공격 범위 오브젝트")] private GameObject telegraphPrefab;
        [SerializeField, InspectorName("공격 전 경고시간"), Min(0.1f)]
        private float warningDuration = 2f;
        [SerializeField, InspectorName("충전 완료 유지시간"), Min(0f)]
        private float telegraphHoldDuration = 0.25f;
        [SerializeField, InspectorName("연출 (시각 효과 / 효과음)")]
        private FallenCommanderAttackEffectData effects = new();
        [SerializeField, InspectorName("시전 모션")] private AnimationClip preCastMotion;
        [SerializeField, InspectorName("시전 모션 속도"), Min(0.01f)] private float preCastMotionSpeed = 1f;
        [SerializeField, InspectorName("시전 모션 시작 지점"), Range(0f, 1f)]
        [Tooltip("0은 모션의 처음, 1은 모션의 끝입니다.")]
        private float preCastMotionStart;
        [SerializeField, InspectorName("시전 모션 종료 지점"), Range(0f, 1f)]
        [Tooltip("종료 지점에서 자세를 멈추고 실제 공격 시점을 기다립니다.")]
        private float preCastMotionEnd = 1f;
        [SerializeField, InspectorName("공격 모션")] private AnimationClip castMotion;
        [SerializeField, InspectorName("공격 모션 속도"), Min(0.01f)] private float castMotionSpeed = 1f;
        [SerializeField, InspectorName("공격 모션 시작 지점"), Range(0f, 1f)]
        [Tooltip("0은 모션의 처음, 1은 모션의 끝입니다.")]
        private float castMotionStart;
        [SerializeField, InspectorName("공격 모션 종료 지점"), Range(0f, 1f)]
        [Tooltip("공격 모션을 재생할 마지막 지점입니다.")]
        private float castMotionEnd = 1f;
        [SerializeField, InspectorName("원형 공격 반지름"), Min(0.1f)]
        private float radius = 2.5f;
        [SerializeField, InspectorName("직선 공격 너비"), Min(0.1f)]
        private float width = 2f;
        [SerializeField, InspectorName("직선 공격 길이"), Min(0.1f)]
        private float length = 8f;
        [SerializeField, InspectorName("기절 지속시간"), Min(0f)]
        private float stunDuration;

        public GameObject TelegraphPrefab => telegraphPrefab;
        public FallenCommanderAttackEffectData Effects => effects;
        public AnimationClip PreCastMotion => preCastMotion;
        public AnimationClip CastMotion => castMotion;
        public float PreCastMotionSpeed => Mathf.Max(0.01f, preCastMotionSpeed);
        public float CastMotionSpeed => Mathf.Max(0.01f, castMotionSpeed);
        public float PreCastMotionStart => ResolveStart(preCastMotionStart);
        public float PreCastMotionEnd => ResolveEnd(preCastMotionStart, preCastMotionEnd);
        public float CastMotionStart => ResolveStart(castMotionStart);
        public float CastMotionEnd => ResolveEnd(castMotionStart, castMotionEnd);
        public float CastMotionDuration => ResolveDuration(
            castMotion,
            CastMotionSpeed,
            CastMotionStart,
            CastMotionEnd);
        public float WarningDuration => warningDuration;
        public float TelegraphHoldDuration => Mathf.Max(0f, telegraphHoldDuration);
        public float Radius => radius;
        public float Width => width;
        public float Length => length;
        public float StunDuration => stunDuration;

        private static float ResolveDuration(
            AnimationClip motion,
            float playbackSpeed,
            float start,
            float end)
        {
            return motion == null
                ? 0f
                : Mathf.Max(
                    0.01f,
                    motion.length * (end - start) / Mathf.Max(0.01f, playbackSpeed));
        }

        private static float ResolveStart(float start)
        {
            return Mathf.Clamp(start, 0f, 0.999f);
        }

        private static float ResolveEnd(float start, float end)
        {
            if (start <= 0f && end <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp(end, ResolveStart(start) + 0.001f, 1f);
        }
    }

    [System.Serializable]
    public sealed class FallenCommanderAttackEffectData
    {
        [SerializeField, InspectorName("시전 효과")] private GameObject startVfxPrefab;
        [SerializeField, InspectorName("시전 효과 유지시간 (0 = 자동)"), Min(0f)] private float startVfxDuration;
        [SerializeField, InspectorName("시전 효과 위치 기준")]
        private FallenCommanderEffectAnchor startVfxAnchor;
        [SerializeField, InspectorName("시전 효과 위치 오프셋")]
        private Vector3 startVfxPositionOffset;
        [SerializeField, InspectorName("시전 효과 회전 오프셋")]
        private Vector3 startVfxRotationOffset;
        [SerializeField, HideInInspector]
        private Vector3 startVfxScale = Vector3.one;
        [SerializeField, InspectorName("시전 효과 전체 크기 배율"), Min(0.01f)]
        private float startVfxScaleMultiplier = 1f;
        [SerializeField, InspectorName("적중 효과")] private GameObject resolveVfxPrefab;
        [SerializeField, InspectorName("적중 효과 유지시간 (0 = 자동)"), Min(0f)] private float resolveVfxDuration;
        [SerializeField, InspectorName("적중 효과 위치 기준")]
        private FallenCommanderEffectAnchor resolveVfxAnchor;
        [SerializeField, InspectorName("적중 효과 위치 오프셋")]
        private Vector3 resolveVfxPositionOffset;
        [SerializeField, InspectorName("적중 효과 회전 오프셋")]
        private Vector3 resolveVfxRotationOffset;
        [SerializeField, HideInInspector]
        private Vector3 resolveVfxScale = Vector3.one;
        [SerializeField, InspectorName("적중 효과 전체 크기 배율"), Min(0.01f)]
        private float resolveVfxScaleMultiplier = 1f;
        [SerializeField, InspectorName("적중 VFX")] private GameObject hitVfxPrefab;
        [SerializeField, InspectorName("적중 VFX 유지시간 (0 = 자동)"), Min(0f)]
        private float hitVfxDuration;
        [SerializeField, InspectorName("적중 VFX 위치 오프셋")]
        private Vector3 hitVfxPositionOffset;
        [SerializeField, InspectorName("적중 VFX 회전 오프셋")]
        private Vector3 hitVfxRotationOffset;
        [SerializeField, HideInInspector]
        private Vector3 hitVfxScale = Vector3.one;
        [SerializeField, InspectorName("적중 VFX 전체 크기 배율"), Min(0.01f)]
        private float hitVfxScaleMultiplier = 1f;
        [SerializeField, InspectorName("시전 효과음")] private AudioClip startSfx;
        [SerializeField, InspectorName("시전 효과음 유지시간 (0 = 자동)"), Min(0f)] private float startSfxDuration;
        [SerializeField, InspectorName("적중 효과음")] private AudioClip resolveSfx;
        [SerializeField, InspectorName("적중 효과음 유지시간 (0 = 자동)"), Min(0f)] private float resolveSfxDuration;
        [SerializeField, InspectorName("효과음 볼륨"), Range(0f, 1f)] private float sfxVolume = 1f;
        [SerializeField, InspectorName("적중 SFX")] private AudioClip hitSfx;
        [SerializeField, InspectorName("적중 SFX 유지시간 (0 = 자동)"), Min(0f)]
        private float hitSfxDuration;
        [SerializeField, InspectorName("적중 SFX 볼륨"), Range(0f, 1f)]
        private float hitSfxVolume = 1f;

        public GameObject StartVfxPrefab => startVfxPrefab;
        public float StartVfxDuration => startVfxDuration;
        public FallenCommanderEffectAnchor StartVfxAnchor => startVfxAnchor;
        public Vector3 StartVfxPositionOffset => startVfxPositionOffset;
        public Vector3 StartVfxRotationOffset => startVfxRotationOffset;
        public Vector3 StartVfxScale => ResolveScale(startVfxScale) * ResolveScaleMultiplier(startVfxScaleMultiplier);
        public GameObject ResolveVfxPrefab => resolveVfxPrefab;
        public float ResolveVfxDuration => resolveVfxDuration;
        public FallenCommanderEffectAnchor ResolveVfxAnchor => resolveVfxAnchor;
        public Vector3 ResolveVfxPositionOffset => resolveVfxPositionOffset;
        public Vector3 ResolveVfxRotationOffset => resolveVfxRotationOffset;
        public Vector3 ResolveVfxScale => ResolveScale(resolveVfxScale) * ResolveScaleMultiplier(resolveVfxScaleMultiplier);
        public GameObject HitVfxPrefab => hitVfxPrefab;
        public float HitVfxDuration => hitVfxDuration;
        public Vector3 HitVfxPositionOffset => hitVfxPositionOffset;
        public Vector3 HitVfxRotationOffset => hitVfxRotationOffset;
        public Vector3 HitVfxScale => ResolveScale(hitVfxScale) * ResolveScaleMultiplier(hitVfxScaleMultiplier);
        public AudioClip StartSfx => startSfx;
        public float StartSfxDuration => startSfxDuration;
        public AudioClip ResolveSfx => resolveSfx;
        public float ResolveSfxDuration => resolveSfxDuration;
        public float SfxVolume => sfxVolume;
        public AudioClip HitSfx => hitSfx;
        public float HitSfxDuration => hitSfxDuration;
        public float HitSfxVolume => hitSfxVolume;

        // 기존 데이터에 크기 값이 없을 때 현재 VFX 크기를 유지하도록 기본값을 보정한다.
        private static Vector3 ResolveScale(Vector3 scale)
        {
            return scale == Vector3.zero ? Vector3.one : scale;
        }

        // 새 배율 값이 아직 저장되지 않은 기존 데이터도 기존 크기 그대로 재생되도록 보정한다.
        private static float ResolveScaleMultiplier(float multiplier)
        {
            return multiplier <= 0f ? 1f : multiplier;
        }
    }

    [System.Serializable]
    public sealed class FallenCommanderTimeoutWipeData
    {
        [SerializeField, InspectorName("공격 범위 오브젝트")]
        private GameObject telegraphPrefab;
        [SerializeField, InspectorName("발동 전 경고시간"), Min(0f)]
        private float warningDuration = 0.8f;
        [SerializeField, InspectorName("충전 완료 유지시간"), Min(0f)]
        private float telegraphHoldDuration = 0.25f;
        [SerializeField, InspectorName("공격 범위 반지름 (연출용)"), Min(0.1f)]
        [Tooltip("전멸 피해는 전장 전체에 적용되며 이 값은 경고 범위의 표시 크기만 조절합니다.")]
        private float radius = 8f;
        [SerializeField, InspectorName("연출 (시각 효과 / 효과음)")]
        private FallenCommanderAttackEffectData effects = new();
        [SerializeField, InspectorName("시전 모션")] private AnimationClip preCastMotion;
        [SerializeField, InspectorName("시전 모션 속도"), Min(0.01f)]
        private float preCastMotionSpeed = 1f;
        [SerializeField, InspectorName("시전 모션 시작 지점"), Range(0f, 1f)]
        [Tooltip("0은 모션의 처음, 1은 모션의 끝입니다.")]
        private float preCastMotionStart;
        [SerializeField, InspectorName("시전 모션 종료 지점"), Range(0f, 1f)]
        [Tooltip("종료 지점에서 자세를 멈추고 전멸 발동 시점을 기다립니다.")]
        private float preCastMotionEnd = 1f;
        [SerializeField, InspectorName("전멸 발동 모션")] private AnimationClip castMotion;
        [SerializeField, InspectorName("전멸 발동 모션 속도"), Min(0.01f)]
        private float castMotionSpeed = 1f;
        [SerializeField, InspectorName("전멸 발동 모션 시작 지점"), Range(0f, 1f)]
        [Tooltip("0은 모션의 처음, 1은 모션의 끝입니다.")]
        private float castMotionStart;
        [SerializeField, InspectorName("전멸 발동 모션 종료 지점"), Range(0f, 1f)]
        [Tooltip("전멸 발동 모션을 재생할 마지막 지점입니다.")]
        private float castMotionEnd = 1f;
        [SerializeField, InspectorName("결과창 대기시간"), Min(0f)]
        private float resultDelay = 2f;
        [SerializeField, InspectorName("전멸 경고 문구")]
        private string warningMessage = "시간 종료! 전멸 공격이 발동됩니다!";
        [SerializeField, InspectorName("경고 점멸 간격"), Min(0.05f)]
        private float warningPulseInterval = 0.45f;

        public GameObject TelegraphPrefab => telegraphPrefab;
        public float Radius => Mathf.Max(0.1f, radius);
        public FallenCommanderAttackEffectData Effects => effects;
        public AnimationClip PreCastMotion => preCastMotion;
        public float PreCastMotionSpeed => Mathf.Max(0.01f, preCastMotionSpeed);
        public AnimationClip CastMotion => castMotion;
        public float CastMotionSpeed => Mathf.Max(0.01f, castMotionSpeed);
        public float PreCastMotionStart => ResolveStart(preCastMotionStart);
        public float PreCastMotionEnd => ResolveEnd(preCastMotionStart, preCastMotionEnd);
        public float CastMotionStart => ResolveStart(castMotionStart);
        public float CastMotionEnd => ResolveEnd(castMotionStart, castMotionEnd);
        public float CastMotionDuration => ResolveDuration(
            castMotion,
            CastMotionSpeed,
            CastMotionStart,
            CastMotionEnd);
        public float WarningDuration => Mathf.Max(0f, warningDuration);
        public float TelegraphHoldDuration => Mathf.Max(0f, telegraphHoldDuration);
        public float ResultDelay => Mathf.Max(0f, resultDelay);
        public string WarningMessage => warningMessage;
        public float WarningPulseInterval => Mathf.Max(0.05f, warningPulseInterval);

        // 선택한 모션 구간과 재생 속도로 실제 재생시간을 계산한다.
        private static float ResolveDuration(
            AnimationClip motion,
            float playbackSpeed,
            float start,
            float end)
        {
            return motion == null
                ? 0f
                : Mathf.Max(
                    0.01f,
                    motion.length * (end - start) / Mathf.Max(0.01f, playbackSpeed));
        }

        private static float ResolveStart(float start)
        {
            return Mathf.Clamp(start, 0f, 0.999f);
        }

        private static float ResolveEnd(float start, float end)
        {
            if (start <= 0f && end <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp(end, ResolveStart(start) + 0.001f, 1f);
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
