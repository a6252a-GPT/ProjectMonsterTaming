using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    public enum FallenCommanderBossPhase
    {
        [InspectorName("1 페이즈")]
        Phase1 = 1,
        [InspectorName("2 페이즈")]
        Phase2 = 2,
        [InspectorName("3 페이즈")]
        Phase3 = 3
    }

    public enum FallenCommanderAttackPattern
    {
        [InspectorName("기본 공격 - 원거리 투사체")]
        Basic,
        [InspectorName("근접 공격")]
        Melee,
        [InspectorName("위치 공격")]
        Mark,
        [InspectorName("추적 낙인")]
        TrackingMark,
        [InspectorName("블랙홀")]
        BlackHole,
        [InspectorName("직선 공격")]
        Line,
        [InspectorName("타락의 고리")]
        Ring
    }

    public static class FallenCommanderAttackSelectionRules
    {
        public static FallenCommanderAttackPattern Select(
            float distance,
            float forwardAlignment,
            float closeAttackDistance,
            float blackHoleRadius,
            float lineStrikeMinimumDistance,
            float lineStrikeAlignmentThreshold,
            FallenCommanderAttackPattern previousAttack)
        {
            var safeDistance = Mathf.Max(0f, distance);
            var closeDistance = Mathf.Max(0.1f, closeAttackDistance);
            var lineDistance = Mathf.Max(closeDistance, lineStrikeMinimumDistance);
            var alignment = Mathf.Clamp(forwardAlignment, -1f, 1f);
            var alignmentThreshold = Mathf.Clamp(lineStrikeAlignmentThreshold, -1f, 1f);

            if (safeDistance <= closeDistance)
            {
                return previousAttack == FallenCommanderAttackPattern.Melee
                    ? FallenCommanderAttackPattern.BlackHole
                    : FallenCommanderAttackPattern.Melee;
            }

            if (safeDistance >= lineDistance &&
                alignment >= alignmentThreshold &&
                previousAttack != FallenCommanderAttackPattern.Line)
            {
                return FallenCommanderAttackPattern.Line;
            }

            if (alignment < alignmentThreshold &&
                previousAttack != FallenCommanderAttackPattern.Mark)
            {
                return FallenCommanderAttackPattern.Mark;
            }

            return safeDistance <= Mathf.Max(closeDistance, blackHoleRadius) &&
                   previousAttack != FallenCommanderAttackPattern.BlackHole
                ? FallenCommanderAttackPattern.BlackHole
                : FallenCommanderAttackPattern.Basic;
        }
    }

    public sealed class FallenCommanderBossStateMachine
    {
        private enum BossState
        {
            Idle,
            Melee,
            MarkStrike,
            TrackingMark,
            BlackHole,
            LineStrike,
            CorruptionRing,
            Broken,
            Dead
        }

        private CombatWorld combatWorld;
        private UnitActor bossActor;
        private Transform commanderRoot;
        private HealthComponent commanderHealth;
        private FallenCommanderBossFacingSmoother bossFacingSmoother;
        private FallenCommanderBossAnimationPresenter animationPresenter;
        private AnimationClip breakMotion;
        private float breakMotionDuration;

        private BossState currentState;
        private float attackInterval;
        private float attackCooldownRemaining;
        private float markStrikeStunDuration;
        private float trackingMarkCastTime;
        private float trackingMarkLockDuration;
        private float trackingMarkRadius;
        private float basicAttackWarningTime;
        private float basicProjectileSpeed;
        private float basicProjectileRadius;
        private float basicProjectileMaxDistance;
        private float basicProjectileHeight;
        private float basicAttackRepeatInterval;
        private float basicPatternOverlapDelay;
        private float basicAttackCooldownRemaining;
        private float basicPatternDelayRemaining;
        private float basicWindupRemaining;
        private float meleeAttackCastTime;
        private float meleeAttackRadius;
        private float blackHoleRadius;
        private float lineStrikeCastTime;
        private float lineStrikeWidth;
        private float lineStrikeLength;
        private float lineStrikeStunDuration;
        private float corruptionRingCastTime;
        private float corruptionRingSafeRadius;
        private float corruptionRingOuterRadius;
        private float closeAttackDistance;
        private float lineStrikeMinimumDistance;
        private float lineStrikeAlignmentThreshold;
        private float commanderStunRemaining;
        private System.Action<bool> commanderStunChanged;
        private bool isCommanderStunned;
        private bool isActive;
        private bool isBasicWindupActive;
        private bool isBasicProjectileActive;
        private bool isPhaseTransitionActive;
        private FallenCommanderBossPhase currentPhase = FallenCommanderBossPhase.Phase1;
        private FallenCommanderPhaseConfig phaseConfig;

        private FallenCommanderBasicAttackData basicAttack;
        private FallenCommanderAttackData meleeAttackMotion;
        private FallenCommanderAttackData markStrikeMotion;
        private FallenCommanderAttackData trackingMarkMotion;
        private FallenCommanderAttackData blackHoleMotion;
        private readonly FallenCommanderBlackHolePattern blackHolePattern = new();
        private FallenCommanderAttackData lineStrikeMotion;
        private FallenCommanderAttackData corruptionRingMotion;

        public bool IsCommanderStunned => isCommanderStunned;
        public float CommanderStunRemainingTime => commanderStunRemaining;
        // 예약된 페이즈나 특수 패턴을 안전하게 시작할 수 있는 기본 대기 상태인지 알려준다.
        public bool IsIdle => isActive &&
            !isPhaseTransitionActive &&
            currentState == BossState.Idle;
        public FallenCommanderAttackPattern LastSelectedAttack { get; private set; }
        public FallenCommanderTelegraphView ActiveTelegraph =>
            blackHolePattern.ActiveTelegraph ?? activeTelegraph;
        // 현재 바닥에 생성되어 있는 범위 오브젝트
        private FallenCommanderTelegraphView activeTelegraph;
        private FallenCommanderTelegraphView activeRingSafeTelegraph;
        private FallenCommanderTelegraphView activeBasicTelegraph;
        private FallenCommanderBasicProjectileView activeBasicProjectile;

        // 범위가 나타난 후 공격까지 걸리는 시간
        private float markStrikeCastTime;

        // 실제 공격 반지름
        private float markStrikeRadius;
        private float telegraphDuration;

        // 현재 공격이 끝날 때까지 남은 시간
        private float stateTimeRemaining;

        // 처음 지정한 군단장의 위치
        private Vector3 markStrikePosition;
        private Vector3 lineStrikeDirection;
        private Vector3 basicProjectilePosition;
        private Vector3 basicProjectileDirection;
        private float basicProjectileDistanceRemaining;

        private static readonly Color BasicTelegraphColor =
            new Color(0.2f, 0.85f, 1f, 0.8f);
        private static readonly Color MeleeTelegraphColor =
            new Color(1f, 0.25f, 0.05f, 0.75f);
        private static readonly Color LineTelegraphColor =
            new Color(0.15f, 0.45f, 1f, 0.75f);
        private static readonly Color MarkTelegraphColor =
            new Color(0.9f, 0.15f, 0.8f, 0.75f);
        private static readonly Color TrackingMarkTelegraphColor =
            new Color(0.25f, 0.75f, 1f, 0.75f);
        private static readonly Color BlackHoleTelegraphColor =
            new Color(0.55f, 0.1f, 0.85f, 0.8f);
        private static readonly Color CorruptionRingTelegraphColor =
            new Color(0.65f, 0.05f, 0.15f, 0.8f);
        private static readonly Color CorruptionRingSafeColor =
            new Color(0.1f, 0.9f, 0.45f, 0.8f);

        // 전투 참조와 보스 공격·페이즈 데이터를 런타임 상태 머신에 연결한다.
        public void Configure(
            CombatWorld world,
            UnitActor boss,
            Transform commander,
            HealthComponent commanderTarget,
            float interval,
            FallenCommanderBossAnimationPresenter animations,
            AnimationClip brokenMotion,
            float brokenMotionDuration,
            FallenCommanderBasicAttackData basicAttackData,
            FallenCommanderAttackData meleeMotion,
            FallenCommanderAttackData markMotion,
            FallenCommanderAttackData trackingMotion,
            float trackingLockDuration,
            FallenCommanderAttackData blackHoleAttack,
            float blackHoleActiveDuration,
            float blackHoleCoreRadius,
            float blackHoleSpawnMinDistance,
            float blackHoleSpawnMaxDistance,
            float blackHoleOuterPullSpeed,
            float blackHoleInnerPullSpeed,
            AnimationCurve blackHolePullStrengthCurve,
            Vector3 blackHoleArenaCenter,
            Vector2 blackHoleArenaHalfExtents,
            FallenCommanderAttackEffectData blackHoleEndingEffects,
            FallenCommanderAttackData lineMotion,
            FallenCommanderAttackData ringMotion,
            float ringSafeRadius,
            float closeDistance,
            float lineMinimumDistance,
            float lineAlignmentThreshold,
            FallenCommanderPhaseConfig phases,
            System.Action<bool> stunChanged,
            FallenCommanderBossFacingSmoother facingSmoother)
        {
            Shutdown();

            combatWorld = world;
            bossActor = boss;
            commanderRoot = commander;
            commanderHealth = commanderTarget;
            bossFacingSmoother = facingSmoother;
            animationPresenter = animations;
            breakMotion = brokenMotion;
            this.breakMotionDuration = brokenMotionDuration;

            // 0초나 음수가 되는 것을 막기
            attackInterval = Mathf.Max(0.1f, interval);
            basicAttack = basicAttackData;
            basicAttackWarningTime = Mathf.Max(0.1f, basicAttackData == null ? 0f : basicAttackData.WarningDuration);
            basicProjectileSpeed = Mathf.Max(0.1f, basicAttackData == null ? 0f : basicAttackData.ProjectileSpeed);
            basicProjectileRadius = Mathf.Max(0.1f, basicAttackData == null ? 0f : basicAttackData.ProjectileRadius);
            basicProjectileMaxDistance = Mathf.Max(0.1f, basicAttackData == null ? 0f : basicAttackData.MaxDistance);
            basicProjectileHeight = Mathf.Max(0f, basicAttackData == null ? 0f : basicAttackData.ProjectileHeight);
            basicAttackRepeatInterval = Mathf.Max(0.1f, basicAttackData == null ? 0f : basicAttackData.RepeatInterval);
            basicPatternOverlapDelay = Mathf.Max(0f, basicAttackData == null ? 0f : basicAttackData.PatternOverlapDelay);
            meleeAttackMotion = meleeMotion;
            meleeAttackCastTime = Mathf.Max(0.1f, meleeMotion == null ? 0f : meleeMotion.WarningDuration);
            meleeAttackRadius = Mathf.Max(0.1f, meleeMotion == null ? 0f : meleeMotion.Radius);
            markStrikeMotion = markMotion;
            markStrikeCastTime = Mathf.Max(0.1f, markMotion == null ? 0f : markMotion.WarningDuration);
            markStrikeRadius = Mathf.Max(0.1f, markMotion == null ? 0f : markMotion.Radius);
            markStrikeStunDuration = Mathf.Max(0.1f, markMotion == null ? 0f : markMotion.StunDuration);
            trackingMarkMotion = trackingMotion;
            trackingMarkCastTime = Mathf.Max(0.1f, trackingMotion == null ? 0f : trackingMotion.WarningDuration);
            trackingMarkLockDuration = Mathf.Clamp(trackingLockDuration, 0.1f, trackingMarkCastTime);
            trackingMarkRadius = Mathf.Max(0.1f, trackingMotion == null ? 0f : trackingMotion.Radius);
            blackHoleMotion = blackHoleAttack;
            blackHoleRadius = Mathf.Max(0.1f, blackHoleAttack == null ? 0f : blackHoleAttack.Radius);
            blackHolePattern.Configure(
                blackHoleAttack,
                blackHoleActiveDuration,
                blackHoleCoreRadius,
                blackHoleSpawnMinDistance,
                blackHoleSpawnMaxDistance,
                blackHoleOuterPullSpeed,
                blackHoleInnerPullSpeed,
                blackHolePullStrengthCurve,
                blackHoleArenaCenter,
                blackHoleArenaHalfExtents,
                blackHoleEndingEffects,
                BlackHoleTelegraphColor);
            lineStrikeMotion = lineMotion;
            lineStrikeCastTime = Mathf.Max(0.1f, lineMotion == null ? 0f : lineMotion.WarningDuration);
            lineStrikeWidth = Mathf.Max(0.1f, lineMotion == null ? 0f : lineMotion.Width);
            lineStrikeLength = Mathf.Max(0.1f, lineMotion == null ? 0f : lineMotion.Length);
            lineStrikeStunDuration = Mathf.Max(0f, lineMotion == null ? 0f : lineMotion.StunDuration);
            corruptionRingMotion = ringMotion;
            corruptionRingCastTime = Mathf.Max(0.1f, ringMotion == null ? 0f : ringMotion.WarningDuration);
            corruptionRingOuterRadius = Mathf.Max(0.2f, ringMotion == null ? 0f : ringMotion.Radius);
            corruptionRingSafeRadius = Mathf.Clamp(ringSafeRadius, 0.1f, corruptionRingOuterRadius - 0.1f);
            closeAttackDistance = Mathf.Max(0.1f, closeDistance);
            lineStrikeMinimumDistance = Mathf.Max(closeAttackDistance, lineMinimumDistance);
            lineStrikeAlignmentThreshold = Mathf.Clamp(lineAlignmentThreshold, -1f, 1f);
            phaseConfig = phases;
            commanderStunChanged = stunChanged;

            attackCooldownRemaining = attackInterval;
            currentState = BossState.Idle;
            LastSelectedAttack = FallenCommanderAttackPattern.Mark;

            isActive =
                combatWorld != null &&
                bossActor != null &&
                commanderRoot != null &&
                commanderHealth != null &&
                basicAttack != null &&
                basicAttack.TelegraphPrefab != null &&
                meleeAttackMotion != null &&
                meleeAttackMotion.TelegraphPrefab != null &&
                markStrikeMotion != null &&
                markStrikeMotion.TelegraphPrefab != null &&
                trackingMarkMotion != null &&
                trackingMarkMotion.TelegraphPrefab != null &&
                blackHoleMotion != null &&
                blackHoleMotion.TelegraphPrefab != null &&
                lineStrikeMotion != null &&
                lineStrikeMotion.TelegraphPrefab != null &&
                corruptionRingMotion != null &&
                corruptionRingMotion.TelegraphPrefab != null &&
                phaseConfig != null &&
                animationPresenter != null &&
                bossFacingSmoother != null &&
                commanderStunChanged != null;
        }

        public void Tick(float deltaTime)
        {
            if (!isActive ||
                bossActor == null ||
                !bossActor.IsAlive ||
                commanderHealth == null ||
                !commanderHealth.IsAlive)
            {
                return;
            }

            TickCommanderStun(deltaTime);

            if (isPhaseTransitionActive)
            {
                return;
            }

            switch (currentState)
            {
                case BossState.Idle:
                    TickIdle(deltaTime);
                    break;

                case BossState.Melee:
                case BossState.MarkStrike:
                case BossState.TrackingMark:
                case BossState.LineStrike:
                case BossState.CorruptionRing:
                    TickAttack(deltaTime);
                    break;

                case BossState.BlackHole:
                    TickBlackHole(deltaTime);
                    break;

                case BossState.Broken:
                case BossState.Dead:
                    break;
            }

            TickOverlappingBasicAttack(deltaTime);
        }

        public void EnterBroken()
        {
            if (!isActive ||
                currentState == BossState.Broken)
            {
                return;
            }

            DestroyActiveTelegraph();
            CancelOverlappingBasicAttack();
            animationPresenter?.StopPlayback();
            animationPresenter?.Play(
                breakMotion,
                stopAfterMotion: true,
                durationOverride: breakMotionDuration);
            stateTimeRemaining = 0f;
            currentState = BossState.Broken;
            bossFacingSmoother.SetTrackingEnabled(false);
            bossActor.ForceTarget(
                bossActor.Health,
                float.PositiveInfinity);
        }

        public void ExitBroken()
        {
            if (!isActive ||
                currentState != BossState.Broken)
            {
                return;
            }

            DestroyActiveTelegraph();
            attackCooldownRemaining = attackInterval;
            basicAttackCooldownRemaining = basicAttackRepeatInterval;
            currentState = BossState.Idle;
            animationPresenter?.StopPlayback();
            bossActor.ForceTarget(
                commanderHealth,
                float.PositiveInfinity);
            bossFacingSmoother.SetTrackingEnabled(true);
        }

        public void DebugForceBasicAttack()
        {
            if (!CanStartOverlappingBasicAttack())
            {
                return;
            }

            BeginBasicAttack();
        }

        public void SetPhase(FallenCommanderBossPhase phase)
        {
            currentPhase = phase;
        }

        public void BeginPhaseTransition(
            FallenCommanderBossPhase phase,
            float transitionDuration)
        {
            if (!isActive || bossActor == null || !bossActor.IsAlive)
            {
                return;
            }

            currentPhase = phase;
            isPhaseTransitionActive = true;
            DestroyActiveTelegraph();
            CancelOverlappingBasicAttack();
            animationPresenter?.StopPlayback();
            stateTimeRemaining = 0f;
            attackCooldownRemaining = Mathf.Max(0.1f, transitionDuration);
            basicAttackCooldownRemaining = basicAttackRepeatInterval;
            currentState = BossState.Idle;
            PauseBossTracking();
        }

        // 전환을 끝내고 페이즈 데이터에 지정된 대표 공격을 시작한다.
        public void CompletePhaseTransition(FallenCommanderAttackPattern signatureAttack)
        {
            if (!isActive || !isPhaseTransitionActive)
            {
                return;
            }

            isPhaseTransitionActive = false;
            BeginPatternAttack(signatureAttack);
        }

        public void DebugForceMeleeAttack()
        {
            if (!PrepareDebugAttack())
            {
                return;
            }

            BeginMeleeAttack();
        }

        public void DebugForceMarkStrike()
        {
            if (!PrepareDebugAttack())
            {
                return;
            }

            BeginMarkStrike();
        }

        public void DebugForceTrackingMark()
        {
            if (!PrepareDebugAttack())
            {
                return;
            }

            BeginTrackingMark();
        }

        public void DebugForceBlackHole()
        {
            if (!PrepareDebugAttack())
            {
                return;
            }

            BeginBlackHole();
        }

        public void ForceBlackHole()
        {
            if (!isActive ||
                bossActor == null ||
                !bossActor.IsAlive ||
                currentState == BossState.Broken ||
                currentState == BossState.Dead ||
                currentState == BossState.BlackHole)
            {
                return;
            }

            BeginBlackHole();
        }

        public void DebugForceLineStrike()
        {
            if (!PrepareDebugAttack())
            {
                return;
            }

            BeginLineStrike();
        }

        public void DebugForceCorruptionRing()
        {
            if (!PrepareDebugAttack())
            {
                return;
            }

            BeginCorruptionRing();
        }

        private bool PrepareDebugAttack()
        {
            if (!isActive ||
                bossActor == null ||
                !bossActor.IsAlive ||
                commanderHealth == null ||
                !commanderHealth.IsAlive ||
                currentState == BossState.Dead)
            {
                return false;
            }

            DestroyActiveTelegraph();
            stateTimeRemaining = 0f;
            isPhaseTransitionActive = false;
            currentState = BossState.Idle;
            attackCooldownRemaining = 0f;
            return true;
        }

        // 생성된 공격 표시와 런타임 참조를 모두 정리한다.
        public void Shutdown()
        {
            DestroyActiveTelegraph();
            CancelOverlappingBasicAttack();
            ReleaseCommanderStun();
            animationPresenter?.Stop();

            isActive = false;
            combatWorld = null;
            bossActor = null;
            commanderRoot = null;
            commanderHealth = null;
            phaseConfig = null;
            bossFacingSmoother = null;
            animationPresenter = null;
            breakMotion = null;
            blackHoleMotion = null;
            breakMotionDuration = 0f;
            telegraphDuration = 0f;
            commanderStunChanged = null;
            attackCooldownRemaining = 0f;
            basicAttackCooldownRemaining = 0f;
            basicPatternDelayRemaining = 0f;
            basicWindupRemaining = 0f;
            commanderStunRemaining = 0f;
            markStrikeStunDuration = 0f;
            stateTimeRemaining = 0f;
            markStrikePosition = Vector3.zero;
            lineStrikeDirection = Vector3.zero;
            basicProjectilePosition = Vector3.zero;
            basicProjectileDirection = Vector3.zero;
            basicProjectileDistanceRemaining = 0f;
            isBasicWindupActive = false;
            isBasicProjectileActive = false;
            isPhaseTransitionActive = false;
            currentPhase = FallenCommanderBossPhase.Phase1;
            currentState = BossState.Idle;
        }

        // 대기 중 공격 간격을 갱신하고 거리 조건에 맞는 다음 페이즈 공격을 시작한다.
        private void TickIdle(float deltaTime)
        {
            attackCooldownRemaining =
                Mathf.Max(0f, attackCooldownRemaining - deltaTime);

            if (attackCooldownRemaining > 0f)
            {
                return;
            }

            var toCommander = commanderRoot.position - bossActor.transform.position;
            toCommander.y = 0f;
            var distance = toCommander.magnitude;
            var alignment = distance <= 0.001f
                ? 1f
                : Vector3.Dot(
                    bossActor.transform.forward,
                    toCommander / distance);
            var selected = FallenCommanderAttackSelectionRules.Select(
                distance,
                alignment,
                closeAttackDistance,
                blackHoleRadius,
                lineStrikeMinimumDistance,
                lineStrikeAlignmentThreshold,
                LastSelectedAttack);
            selected = ResolvePhaseAttack(selected);

            BeginPatternAttack(selected);
        }

        // 선택된 패턴에 대응하는 실제 공격 상태를 시작한다.
        private void BeginPatternAttack(FallenCommanderAttackPattern selected)
        {
            switch (selected)
            {
                case FallenCommanderAttackPattern.Basic:
                    attackCooldownRemaining = 0f;
                    ResumeBossTracking();
                    break;
                case FallenCommanderAttackPattern.Melee:
                    BeginMeleeAttack();
                    break;
                case FallenCommanderAttackPattern.Mark:
                    BeginMarkStrike();
                    break;
                case FallenCommanderAttackPattern.TrackingMark:
                    BeginTrackingMark();
                    break;
                case FallenCommanderAttackPattern.BlackHole:
                    BeginBlackHole();
                    break;
                case FallenCommanderAttackPattern.Ring:
                    BeginCorruptionRing();
                    break;
                default:
                    BeginLineStrike();
                    break;
            }
        }

        // 현재 페이즈 데이터의 스킬 장바구니로 공격 후보를 보정한다.
        private FallenCommanderAttackPattern ResolvePhaseAttack(
            FallenCommanderAttackPattern selected)
        {
            var phase = phaseConfig?.GetPhase(currentPhase);
            return phase == null
                ? selected
                : phase.ResolveAttack(selected, LastSelectedAttack);
        }

        private void BeginBasicAttack()
        {
            var origin = bossActor.transform.position;
            basicProjectileDirection = commanderRoot.position - origin;
            basicProjectileDirection.y = 0f;
            if (basicProjectileDirection.sqrMagnitude < 0.001f)
            {
                basicProjectileDirection = bossActor.transform.forward;
            }

            basicProjectileDirection.Normalize();
            basicProjectilePosition = origin + Vector3.up * basicProjectileHeight;
            basicProjectileDistanceRemaining = basicProjectileMaxDistance;
            basicWindupRemaining = basicAttackWarningTime;
            isBasicWindupActive = true;
            isBasicProjectileActive = false;
            DestroyActiveBasicTelegraph();
            FallenCommanderAttackEffectPlayer.PlayStart(
                basicAttack.Effects,
                basicProjectilePosition,
                basicProjectileDirection,
                bossActor.transform.parent);

            activeBasicTelegraph = FallenCommanderTelegraphView.CreateLine(
                basicAttack.TelegraphPrefab,
                bossActor.transform.parent,
                origin,
                basicProjectileDirection,
                basicProjectileRadius * 2f,
                basicProjectileMaxDistance,
                BasicTelegraphColor);
        }

        private void BeginMeleeAttack()
        {
            LastSelectedAttack = FallenCommanderAttackPattern.Melee;
            DelayNextBasicAttackForPatternStart();
            BeginCircleAttack(
                BossState.Melee,
                bossActor.transform.position,
                meleeAttackCastTime,
                meleeAttackRadius,
                meleeAttackMotion,
                MeleeTelegraphColor);
        }

        private void BeginMarkStrike()
        {
            LastSelectedAttack = FallenCommanderAttackPattern.Mark;
            DelayNextBasicAttackForPatternStart();
            // 공격을 시작한 순간의 군단장 위치를 저장
            markStrikePosition = commanderRoot.position;

            // 위치 공격 시전 시간
            stateTimeRemaining = markStrikeCastTime;

            // FSM의 현재 상태를 위치 공격으로 변경
            currentState = BossState.MarkStrike;
            PauseBossTracking();
            animationPresenter.Play(
                markStrikeMotion?.PreCastMotion,
                playbackSpeed: markStrikeMotion == null ? 1f : markStrikeMotion.PreCastMotionSpeed);
            FallenCommanderAttackEffectPlayer.PlayStart(
                markStrikeMotion?.Effects,
                markStrikePosition,
                bossActor.transform.forward,
                bossActor.transform.parent);

            // 이전 공격 표시 제거
            DestroyActiveTelegraph();

            activeTelegraph = FallenCommanderTelegraphView.CreateCircle(
                markStrikeMotion.TelegraphPrefab,
                bossActor.transform.parent,
                markStrikePosition,
                markStrikeRadius,
                MarkTelegraphColor);
            telegraphDuration = markStrikeCastTime;
        }

        private void BeginTrackingMark()
        {
            LastSelectedAttack = FallenCommanderAttackPattern.TrackingMark;
            DelayNextBasicAttackForPatternStart();
            markStrikePosition = commanderRoot.position;
            stateTimeRemaining = trackingMarkCastTime;
            currentState = BossState.TrackingMark;
            PauseBossTracking();
            animationPresenter.Play(
                trackingMarkMotion?.PreCastMotion,
                playbackSpeed: trackingMarkMotion == null ? 1f : trackingMarkMotion.PreCastMotionSpeed);
            FallenCommanderAttackEffectPlayer.PlayStart(
                trackingMarkMotion?.Effects,
                markStrikePosition,
                bossActor.transform.forward,
                bossActor.transform.parent);
            DestroyActiveTelegraph();

            activeTelegraph = FallenCommanderTelegraphView.CreateCircle(
                trackingMarkMotion.TelegraphPrefab,
                bossActor.transform.parent,
                markStrikePosition,
                trackingMarkRadius,
                TrackingMarkTelegraphColor);
            telegraphDuration = trackingMarkCastTime;
        }

        // 플레이어 근처에 고정되는 블랙홀 경고와 흡입 패턴을 시작한다.
        private void BeginBlackHole()
        {
            LastSelectedAttack = FallenCommanderAttackPattern.BlackHole;
            DelayNextBasicAttackForPatternStart();
            CancelOverlappingBasicAttack();
            DestroyActiveTelegraph();
            currentState = BossState.BlackHole;
            PauseBossTracking();
            if (!blackHolePattern.Begin(
                    bossActor,
                    commanderRoot,
                    commanderHealth,
                    combatWorld,
                    animationPresenter,
                    bossActor.transform.parent))
            {
                attackCooldownRemaining = attackInterval;
                currentState = BossState.Idle;
                ResumeBossTracking();
            }
        }

        private void BeginLineStrike()
        {
            LastSelectedAttack = FallenCommanderAttackPattern.Line;
            DelayNextBasicAttackForPatternStart();
            var origin = bossActor.transform.position;
            lineStrikeDirection = commanderRoot.position - origin;
            lineStrikeDirection.y = 0f;
            if (lineStrikeDirection.sqrMagnitude < 0.001f)
            {
                lineStrikeDirection = bossActor.transform.forward;
            }

            lineStrikeDirection.Normalize();
            stateTimeRemaining = lineStrikeCastTime;
            currentState = BossState.LineStrike;
            PauseBossTracking();
            animationPresenter.Play(
                lineStrikeMotion?.PreCastMotion,
                playbackSpeed: lineStrikeMotion == null ? 1f : lineStrikeMotion.PreCastMotionSpeed);
            FallenCommanderAttackEffectPlayer.PlayStart(
                lineStrikeMotion?.Effects,
                origin,
                lineStrikeDirection,
                bossActor.transform.parent);
            DestroyActiveTelegraph();

            activeTelegraph = FallenCommanderTelegraphView.CreateLine(
                lineStrikeMotion.TelegraphPrefab,
                bossActor.transform.parent,
                origin,
                lineStrikeDirection,
                lineStrikeWidth,
                lineStrikeLength,
                LineTelegraphColor);
            telegraphDuration = lineStrikeCastTime;
        }

        private void BeginCorruptionRing()
        {
            LastSelectedAttack = FallenCommanderAttackPattern.Ring;
            CancelOverlappingBasicAttack();
            basicAttackCooldownRemaining = basicAttackRepeatInterval;
            markStrikePosition = bossActor.transform.position;
            stateTimeRemaining = corruptionRingCastTime;
            currentState = BossState.CorruptionRing;
            PauseBossTracking();
            animationPresenter.Play(
                corruptionRingMotion?.PreCastMotion,
                playbackSpeed: corruptionRingMotion == null ? 1f : corruptionRingMotion.PreCastMotionSpeed);
            FallenCommanderAttackEffectPlayer.PlayStart(
                corruptionRingMotion?.Effects,
                markStrikePosition,
                bossActor.transform.forward,
                bossActor.transform.parent);
            DestroyActiveTelegraph();

            activeTelegraph = FallenCommanderTelegraphView.CreateCircle(
                corruptionRingMotion.TelegraphPrefab,
                bossActor.transform.parent,
                markStrikePosition,
                corruptionRingOuterRadius,
                CorruptionRingTelegraphColor);
            activeRingSafeTelegraph = FallenCommanderTelegraphView.CreateCircle(
                corruptionRingMotion.TelegraphPrefab,
                bossActor.transform.parent,
                markStrikePosition + Vector3.up * 0.035f,
                corruptionRingSafeRadius,
                CorruptionRingSafeColor);
            activeRingSafeTelegraph?.SetProgress(1f);
            telegraphDuration = corruptionRingCastTime;
        }

        private void BeginCircleAttack(
            BossState state,
            Vector3 position,
            float castTime,
            float radius,
            FallenCommanderAttackData motion,
            Color telegraphColor)
        {
            markStrikePosition = position;
            stateTimeRemaining = castTime;
            currentState = state;
            PauseBossTracking();
            animationPresenter.Play(
                motion?.PreCastMotion,
                playbackSpeed: motion == null ? 1f : motion.PreCastMotionSpeed);
            FallenCommanderAttackEffectPlayer.PlayStart(
                motion?.Effects,
                position,
                bossActor.transform.forward,
                bossActor.transform.parent);
            DestroyActiveTelegraph();

            activeTelegraph = FallenCommanderTelegraphView.CreateCircle(
                motion.TelegraphPrefab,
                bossActor.transform.parent,
                position,
                radius,
                telegraphColor);
            telegraphDuration = castTime;
        }

        // 페이즈에서 허용한 경우 다른 패턴과 별개로 기본 투사체 공격을 갱신한다.
        private void TickOverlappingBasicAttack(float deltaTime)
        {
            if (!isActive ||
                phaseConfig?.GetPhase(currentPhase)?.AllowOverlappingBasicAttack != true ||
                currentState == BossState.Broken ||
                currentState == BossState.Dead ||
                currentState == BossState.BlackHole ||
                currentState == BossState.CorruptionRing ||
                currentState == BossState.TrackingMark &&
                stateTimeRemaining <= trackingMarkLockDuration)
            {
                return;
            }

            basicPatternDelayRemaining = Mathf.Max(
                0f,
                basicPatternDelayRemaining - Mathf.Max(0f, deltaTime));

            if (isBasicWindupActive)
            {
                TickBasicAttackWindup(deltaTime);
                return;
            }

            if (isBasicProjectileActive)
            {
                TickBasicProjectile(deltaTime);
                return;
            }

            basicAttackCooldownRemaining = Mathf.Max(
                0f,
                basicAttackCooldownRemaining - Mathf.Max(0f, deltaTime));
            if (basicAttackCooldownRemaining <= 0f &&
                basicPatternDelayRemaining <= 0f)
            {
                BeginBasicAttack();
            }
        }

        private void TickBasicAttackWindup(float deltaTime)
        {
            basicWindupRemaining = Mathf.Max(0f, basicWindupRemaining - deltaTime);

            if (activeBasicTelegraph != null && basicAttackWarningTime > 0f)
            {
                var progress = 1f - basicWindupRemaining / basicAttackWarningTime;
                activeBasicTelegraph.SetProgress(progress);
            }

            if (basicWindupRemaining > 0f)
            {
                return;
            }

            DestroyActiveBasicTelegraph();
            activeBasicProjectile = FallenCommanderBasicProjectileView.Create(
                basicAttack.ProjectilePrefab,
                bossActor.transform.parent,
                basicProjectilePosition,
                basicProjectileRadius,
                BasicTelegraphColor);
            isBasicWindupActive = false;
            isBasicProjectileActive = true;
        }

        private void TickBasicProjectile(float deltaTime)
        {
            var travelDistance = Mathf.Min(
                basicProjectileDistanceRemaining,
                basicProjectileSpeed * Mathf.Max(0f, deltaTime));
            var nextPosition = basicProjectilePosition +
                basicProjectileDirection * travelDistance;
            var hitCommander = IsCommanderInsideProjectilePath(
                basicProjectilePosition,
                nextPosition);

            basicProjectilePosition = nextPosition;
            basicProjectileDistanceRemaining = Mathf.Max(
                0f,
                basicProjectileDistanceRemaining - travelDistance);
            activeBasicProjectile?.MoveTo(basicProjectilePosition);

            if (hitCommander)
            {
                FallenCommanderAttackEffectPlayer.PlayResolve(
                    basicAttack.Effects,
                    basicProjectilePosition,
                    basicProjectileDirection,
                    bossActor.transform.parent);
                combatWorld.AttackDamageable(
                    bossActor,
                    commanderHealth,
                    bossActor.EffectiveStats);

                if (!isActive)
                {
                    return;
                }

                FinishBasicProjectile();
                return;
            }

            if (basicProjectileDistanceRemaining <= 0f)
            {
                FinishBasicProjectile();
            }
        }

        private bool IsCommanderInsideProjectilePath(Vector3 start, Vector3 end)
        {
            var commanderPosition = commanderRoot.position;
            start.y = 0f;
            end.y = 0f;
            commanderPosition.y = 0f;

            var segment = end - start;
            var segmentLengthSquared = segment.sqrMagnitude;
            var progress = segmentLengthSquared <= 0.0001f
                ? 0f
                : Mathf.Clamp01(Vector3.Dot(
                    commanderPosition - start,
                    segment) / segmentLengthSquared);
            var nearest = start + segment * progress;
            return (commanderPosition - nearest).sqrMagnitude <=
                basicProjectileRadius * basicProjectileRadius;
        }

        private void FinishBasicProjectile()
        {
            DestroyActiveBasicProjectile();
            isBasicProjectileActive = false;
            basicAttackCooldownRemaining = basicAttackRepeatInterval;
        }

        private bool CanStartOverlappingBasicAttack()
        {
            return isActive &&
                bossActor != null &&
                bossActor.IsAlive &&
                commanderHealth != null &&
                commanderHealth.IsAlive &&
                currentState != BossState.Broken &&
                currentState != BossState.Dead &&
                currentState != BossState.BlackHole &&
                currentState != BossState.CorruptionRing &&
                !(currentState == BossState.TrackingMark &&
                    stateTimeRemaining <= trackingMarkLockDuration) &&
                !isBasicWindupActive &&
                !isBasicProjectileActive;
        }

        private void DelayNextBasicAttackForPatternStart()
        {
            basicPatternDelayRemaining = Mathf.Max(
                basicPatternDelayRemaining,
                basicPatternOverlapDelay);
        }

        private void CancelOverlappingBasicAttack()
        {
            DestroyActiveBasicTelegraph();
            DestroyActiveBasicProjectile();
            isBasicWindupActive = false;
            isBasicProjectileActive = false;
            basicWindupRemaining = 0f;
        }

        // 블랙홀의 경고·흡입·피해가 끝나면 보스를 일반 대기 상태로 돌린다.
        private void TickBlackHole(float deltaTime)
        {
            if (!blackHolePattern.Tick(deltaTime))
            {
                return;
            }

            if (!isActive ||
                bossActor == null ||
                commanderHealth == null ||
                !commanderHealth.IsAlive ||
                bossFacingSmoother == null)
            {
                return;
            }

            attackCooldownRemaining = attackInterval;
            currentState = BossState.Idle;
            ResumeBossTracking();
        }

        private void TickAttack(float deltaTime)
        {
            var previousStateTimeRemaining = stateTimeRemaining;
            stateTimeRemaining =
                Mathf.Max(0f, stateTimeRemaining - deltaTime);

            if (currentState == BossState.TrackingMark)
            {
                if (stateTimeRemaining > trackingMarkLockDuration)
                {
                    markStrikePosition = commanderRoot.position;
                    if (activeTelegraph != null)
                    {
                        var telegraphPosition = markStrikePosition;
                        telegraphPosition.y += 0.025f;
                        activeTelegraph.transform.position = telegraphPosition;
                    }
                }
                else if (previousStateTimeRemaining > trackingMarkLockDuration)
                {
                    CancelOverlappingBasicAttack();
                    basicAttackCooldownRemaining = basicAttackRepeatInterval;
                }
            }

            if (activeTelegraph != null &&
                telegraphDuration > 0f &&
                stateTimeRemaining >= 0f)
            {
                var progress = 1f - stateTimeRemaining / telegraphDuration;
                activeTelegraph.SetProgress(progress);
            }

            if (stateTimeRemaining > 0f)
            {
                return;
            }

            var motion = GetCurrentMotion();
            var effectPosition = currentState == BossState.LineStrike
                ? bossActor.transform.position
                : markStrikePosition;
            var effectDirection = currentState == BossState.LineStrike
                ? lineStrikeDirection
                : bossActor.transform.forward;
            FallenCommanderAttackEffectPlayer.PlayResolve(
                motion?.Effects,
                effectPosition,
                effectDirection,
                bossActor.transform.parent);

            if (IsCommanderInsideCurrentAttack())
            {
                var stunDuration = GetCurrentStunDuration();
                if (stunDuration > 0f)
                {
                    LockCommanderStun(stunDuration);
                }

                combatWorld.AttackDamageable(
                    bossActor,
                    commanderHealth,
                    bossActor.EffectiveStats);

                if (!isActive)
                {
                    return;
                }
            }

            animationPresenter.Play(
                motion?.CastMotion,
                stopAfterMotion: true,
                durationOverride: motion == null ? 0f : motion.CastMotionDuration,
                playbackSpeed: motion == null ? 1f : motion.CastMotionSpeed);

            DestroyActiveTelegraph();

            attackCooldownRemaining = attackInterval;
            currentState = BossState.Idle;
            ResumeBossTracking();
        }

        private bool IsCommanderInsideCurrentAttack()
        {
            if (currentState == BossState.CorruptionRing)
            {
                var offset = commanderRoot.position - markStrikePosition;
                offset.y = 0f;
                var distanceSquared = offset.sqrMagnitude;
                return distanceSquared > corruptionRingSafeRadius * corruptionRingSafeRadius &&
                    distanceSquared <= corruptionRingOuterRadius * corruptionRingOuterRadius;
            }

            if (currentState == BossState.LineStrike)
            {
                var offset = commanderRoot.position - bossActor.transform.position;
                offset.y = 0f;
                var forwardDistance = Vector3.Dot(offset, lineStrikeDirection);
                var sideDistance = Mathf.Abs(Vector3.Dot(
                    offset,
                    Vector3.Cross(Vector3.up, lineStrikeDirection)));
                var allowedHalfWidth = FallenCommanderTelegraphView.CalculateLineHalfWidth(
                    lineStrikeWidth,
                    lineStrikeLength,
                    forwardDistance);
                return forwardDistance >= 0f &&
                    forwardDistance <= lineStrikeLength &&
                    sideDistance <= allowedHalfWidth;
            }

            var radius = currentState == BossState.Melee
                ? meleeAttackRadius
                : currentState == BossState.TrackingMark
                        ? trackingMarkRadius
                        : markStrikeRadius;
            return IsCommanderInsideCircle(radius);
        }

        private float GetCurrentStunDuration()
        {
            return currentState == BossState.Melee
                ? meleeAttackMotion == null ? 0f : meleeAttackMotion.StunDuration
                : currentState == BossState.MarkStrike
                    ? markStrikeStunDuration
                    : currentState == BossState.TrackingMark
                        ? 0f
                    : currentState == BossState.LineStrike
                            ? lineStrikeStunDuration
                            : 0f;
        }

        private FallenCommanderAttackData GetCurrentMotion()
        {
            return currentState == BossState.Melee
                ? meleeAttackMotion
                : currentState == BossState.MarkStrike
                    ? markStrikeMotion
                : currentState == BossState.TrackingMark
                        ? trackingMarkMotion
                    : currentState == BossState.LineStrike
                            ? lineStrikeMotion
                            : corruptionRingMotion;
        }

        private void PauseBossTracking()
        {
            bossFacingSmoother.SetTrackingEnabled(false);
            bossActor.ForceTarget(
                bossActor.Health,
                float.PositiveInfinity);
        }

        private void ResumeBossTracking()
        {
            bossActor.ForceTarget(
                commanderHealth,
                float.PositiveInfinity);
            bossFacingSmoother.SetTrackingEnabled(true);
        }

        private void LockCommanderStun(float duration)
        {
            commanderStunRemaining = Mathf.Max(
                commanderStunRemaining,
                Mathf.Max(0.1f, duration));

            if (isCommanderStunned)
            {
                return;
            }

            isCommanderStunned = true;
            commanderStunChanged?.Invoke(true);
        }

        private void TickCommanderStun(float deltaTime)
        {
            if (!isCommanderStunned)
            {
                return;
            }

            commanderStunRemaining = Mathf.Max(
                0f,
                commanderStunRemaining - Mathf.Max(0f, deltaTime));

            if (commanderStunRemaining > 0f)
            {
                return;
            }

            ReleaseCommanderStun();
        }

        private void ReleaseCommanderStun()
        {
            commanderStunRemaining = 0f;

            if (!isCommanderStunned)
            {
                return;
            }

            isCommanderStunned = false;
            commanderStunChanged?.Invoke(false);
        }

        // 범위 안에 있는지 검사
        private bool IsCommanderInsideMarkStrike()
        {
            return IsCommanderInsideCircle(markStrikeRadius);
        }

        private bool IsCommanderInsideCircle(float radius)
        {
            var offset = commanderRoot.position - markStrikePosition;
            offset.y = 0f;

            return offset.sqrMagnitude <=
                radius * radius;
        }

        private void DestroyActiveTelegraph()
        {
            blackHolePattern.Cancel();

            if (activeTelegraph != null)
            {
                Object.Destroy(activeTelegraph.gameObject);
                activeTelegraph = null;
            }

            if (activeRingSafeTelegraph != null)
            {
                Object.Destroy(activeRingSafeTelegraph.gameObject);
                activeRingSafeTelegraph = null;
            }

            telegraphDuration = 0f;
        }

        private void DestroyActiveBasicTelegraph()
        {
            if (activeBasicTelegraph == null)
            {
                return;
            }

            Object.Destroy(activeBasicTelegraph.gameObject);
            activeBasicTelegraph = null;
        }

        private void DestroyActiveBasicProjectile()
        {
            if (activeBasicProjectile == null)
            {
                return;
            }

            Object.Destroy(activeBasicProjectile.gameObject);
            activeBasicProjectile = null;
        }
    }
}
