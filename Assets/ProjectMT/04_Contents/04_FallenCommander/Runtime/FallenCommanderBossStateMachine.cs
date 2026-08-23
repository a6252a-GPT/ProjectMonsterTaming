using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    public enum FallenCommanderBossPhase
    {
        Phase1 = 1,
        Phase2 = 2,
        Phase3 = 3
    }

    public enum FallenCommanderAttackPattern
    {
        Basic,
        Melee,
        Mark,
        TrackingMark,
        Wide,
        Line,
        Ring
    }

    public static class FallenCommanderAttackSelectionRules
    {
        public static FallenCommanderAttackPattern Select(
            float distance,
            float forwardAlignment,
            float closeAttackDistance,
            float wideBurstRadius,
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
                    ? FallenCommanderAttackPattern.Wide
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

            return safeDistance <= Mathf.Max(closeDistance, wideBurstRadius) &&
                   previousAttack != FallenCommanderAttackPattern.Wide
                ? FallenCommanderAttackPattern.Wide
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
            WideBurst,
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
        private float wideBurstCastTime;
        private float wideBurstRadius;
        private float wideBurstStunDuration;
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

        private FallenCommanderBasicAttackData basicAttack;
        private FallenCommanderAttackData meleeAttackMotion;
        private FallenCommanderAttackData markStrikeMotion;
        private FallenCommanderAttackData trackingMarkMotion;
        private FallenCommanderAttackData wideBurstMotion;
        private FallenCommanderAttackData lineStrikeMotion;
        private FallenCommanderAttackData corruptionRingMotion;

        public bool IsCommanderStunned => isCommanderStunned;
        public float CommanderStunRemainingTime => commanderStunRemaining;
        public FallenCommanderAttackPattern LastSelectedAttack { get; private set; }
        public FallenCommanderTelegraphView ActiveTelegraph => activeTelegraph;
        // 위치 공격 범위로 사용할 프리팹
        private GameObject markStrikeTelegraphPrefab;

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
        private static readonly Color TrackingMarkLockedColor =
            new Color(1f, 0.2f, 0.15f, 0.85f);
        private static readonly Color WideTelegraphColor =
            new Color(1f, 0.75f, 0.05f, 0.75f);
        private static readonly Color CorruptionRingTelegraphColor =
            new Color(0.65f, 0.05f, 0.15f, 0.8f);
        private static readonly Color CorruptionRingSafeColor =
            new Color(0.1f, 0.9f, 0.45f, 0.8f);

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
            GameObject telegraphPrefab,
            FallenCommanderAttackData markMotion,
            FallenCommanderAttackData trackingMotion,
            float trackingLockDuration,
            FallenCommanderAttackData wideMotion,
            FallenCommanderAttackData lineMotion,
            FallenCommanderAttackData ringMotion,
            float ringSafeRadius,
            float closeDistance,
            float lineMinimumDistance,
            float lineAlignmentThreshold,
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
            markStrikeTelegraphPrefab = telegraphPrefab;
            markStrikeMotion = markMotion;
            markStrikeCastTime = Mathf.Max(0.1f, markMotion == null ? 0f : markMotion.WarningDuration);
            markStrikeRadius = Mathf.Max(0.1f, markMotion == null ? 0f : markMotion.Radius);
            markStrikeStunDuration = Mathf.Max(0.1f, markMotion == null ? 0f : markMotion.StunDuration);
            trackingMarkMotion = trackingMotion;
            trackingMarkCastTime = Mathf.Max(0.1f, trackingMotion == null ? 0f : trackingMotion.WarningDuration);
            trackingMarkLockDuration = Mathf.Clamp(trackingLockDuration, 0.1f, trackingMarkCastTime);
            trackingMarkRadius = Mathf.Max(0.1f, trackingMotion == null ? 0f : trackingMotion.Radius);
            wideBurstMotion = wideMotion;
            wideBurstCastTime = Mathf.Max(0.1f, wideMotion == null ? 0f : wideMotion.WarningDuration);
            wideBurstRadius = Mathf.Max(0.1f, wideMotion == null ? 0f : wideMotion.Radius);
            wideBurstStunDuration = Mathf.Max(0f, wideMotion == null ? 0f : wideMotion.StunDuration);
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
                meleeAttackMotion != null &&
                trackingMarkMotion != null &&
                corruptionRingMotion != null &&
                markStrikeTelegraphPrefab != null &&
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
                case BossState.WideBurst:
                case BossState.LineStrike:
                case BossState.CorruptionRing:
                    TickAttack(deltaTime);
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

        public void CompletePhaseTransition(FallenCommanderAttackPattern signatureAttack)
        {
            if (!isActive || !isPhaseTransitionActive)
            {
                return;
            }

            isPhaseTransitionActive = false;
            switch (signatureAttack)
            {
                case FallenCommanderAttackPattern.Wide:
                    BeginWideBurst();
                    break;
                case FallenCommanderAttackPattern.TrackingMark:
                    BeginTrackingMark();
                    break;
                default:
                    attackCooldownRemaining = 0f;
                    ResumeBossTracking();
                    break;
            }
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

        public void DebugForceWideBurst()
        {
            if (!PrepareDebugAttack())
            {
                return;
            }

            BeginWideBurst();
        }

        public void ForceWideBurst()
        {
            if (!isActive ||
                bossActor == null ||
                !bossActor.IsAlive ||
                currentState == BossState.Broken ||
                currentState == BossState.Dead ||
                currentState == BossState.WideBurst)
            {
                return;
            }

            BeginWideBurst();
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
            bossFacingSmoother = null;
            animationPresenter = null;
            breakMotion = null;
            breakMotionDuration = 0f;
            markStrikeTelegraphPrefab = null;
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
                wideBurstRadius,
                lineStrikeMinimumDistance,
                lineStrikeAlignmentThreshold,
                LastSelectedAttack);
            selected = ResolvePhaseAttack(selected);

            switch (selected)
            {
                case FallenCommanderAttackPattern.Basic:
                    if (LastSelectedAttack == FallenCommanderAttackPattern.Mark)
                    {
                        BeginWideBurst();
                    }
                    else if (LastSelectedAttack == FallenCommanderAttackPattern.Wide)
                    {
                        BeginCorruptionRing();
                    }
                    else if (LastSelectedAttack == FallenCommanderAttackPattern.Ring)
                    {
                        BeginTrackingMark();
                    }
                    else
                    {
                        BeginMarkStrike();
                    }
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
                case FallenCommanderAttackPattern.Wide:
                    BeginWideBurst();
                    break;
                case FallenCommanderAttackPattern.Ring:
                    BeginCorruptionRing();
                    break;
                default:
                    BeginLineStrike();
                    break;
            }
        }

        private FallenCommanderAttackPattern ResolvePhaseAttack(
            FallenCommanderAttackPattern selected)
        {
            FallenCommanderAttackPattern resolved;
            switch (currentPhase)
            {
                case FallenCommanderBossPhase.Phase1:
                    resolved = selected == FallenCommanderAttackPattern.Wide ||
                        selected == FallenCommanderAttackPattern.Basic ||
                        selected == FallenCommanderAttackPattern.Ring ||
                        selected == FallenCommanderAttackPattern.TrackingMark
                            ? FallenCommanderAttackPattern.Mark
                            : selected;
                    break;

                case FallenCommanderBossPhase.Phase2:
                    resolved = selected == FallenCommanderAttackPattern.Basic
                        ? FallenCommanderAttackPattern.Ring
                        : selected == FallenCommanderAttackPattern.TrackingMark
                            ? FallenCommanderAttackPattern.Mark
                            : selected;
                    break;

                default:
                    resolved = selected == FallenCommanderAttackPattern.Mark
                        ? FallenCommanderAttackPattern.TrackingMark
                        : selected == FallenCommanderAttackPattern.Basic
                            ? FallenCommanderAttackPattern.Ring
                            : selected == FallenCommanderAttackPattern.Wide &&
                                LastSelectedAttack == FallenCommanderAttackPattern.Ring
                                ? FallenCommanderAttackPattern.TrackingMark
                                : selected;
                    break;
            }

            if (resolved != LastSelectedAttack)
            {
                return resolved;
            }

            return currentPhase == FallenCommanderBossPhase.Phase1
                ? resolved == FallenCommanderAttackPattern.Mark
                    ? FallenCommanderAttackPattern.Line
                    : FallenCommanderAttackPattern.Mark
                : resolved == FallenCommanderAttackPattern.Ring
                    ? FallenCommanderAttackPattern.Wide
                    : FallenCommanderAttackPattern.Ring;
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

            activeBasicTelegraph = FallenCommanderTelegraphView.CreateLine(
                markStrikeTelegraphPrefab,
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
            animationPresenter.Play(markStrikeMotion?.PreCastMotion);

            // 이전 공격 표시 제거
            DestroyActiveTelegraph();

            activeTelegraph = FallenCommanderTelegraphView.CreateCircle(
                markStrikeTelegraphPrefab,
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
            animationPresenter.Play(trackingMarkMotion?.PreCastMotion);
            DestroyActiveTelegraph();

            activeTelegraph = FallenCommanderTelegraphView.CreateCircle(
                markStrikeTelegraphPrefab,
                bossActor.transform.parent,
                markStrikePosition,
                trackingMarkRadius,
                TrackingMarkTelegraphColor);
            telegraphDuration = trackingMarkCastTime;
        }

        private void BeginWideBurst()
        {
            LastSelectedAttack = FallenCommanderAttackPattern.Wide;
            DelayNextBasicAttackForPatternStart();
            BeginCircleAttack(
                BossState.WideBurst,
                bossActor.transform.position,
                wideBurstCastTime,
                wideBurstRadius,
                wideBurstMotion,
                WideTelegraphColor);
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
            animationPresenter.Play(lineStrikeMotion?.PreCastMotion);
            DestroyActiveTelegraph();

            activeTelegraph = FallenCommanderTelegraphView.CreateLine(
                markStrikeTelegraphPrefab,
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
            animationPresenter.Play(corruptionRingMotion?.PreCastMotion);
            DestroyActiveTelegraph();

            activeTelegraph = FallenCommanderTelegraphView.CreateCircle(
                markStrikeTelegraphPrefab,
                bossActor.transform.parent,
                markStrikePosition,
                corruptionRingOuterRadius,
                CorruptionRingTelegraphColor);
            activeRingSafeTelegraph = FallenCommanderTelegraphView.CreateCircle(
                markStrikeTelegraphPrefab,
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
            animationPresenter.Play(motion?.PreCastMotion);
            DestroyActiveTelegraph();

            activeTelegraph = FallenCommanderTelegraphView.CreateCircle(
                markStrikeTelegraphPrefab,
                bossActor.transform.parent,
                position,
                radius,
                telegraphColor);
            telegraphDuration = castTime;
        }

        private void TickOverlappingBasicAttack(float deltaTime)
        {
            if (!isActive ||
                currentState == BossState.Broken ||
                currentState == BossState.Dead ||
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
                markStrikeTelegraphPrefab,
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
                    DestroyActiveTelegraph();
                    activeTelegraph = FallenCommanderTelegraphView.CreateCircle(
                        markStrikeTelegraphPrefab,
                        bossActor.transform.parent,
                        markStrikePosition,
                        trackingMarkRadius,
                        TrackingMarkLockedColor);
                    telegraphDuration = trackingMarkLockDuration;
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

            var motion = GetCurrentMotion();
            animationPresenter.Play(
                motion?.CastMotion,
                stopAfterMotion: true,
                durationOverride: motion == null ? 0f : motion.CastMotionDuration);

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
                return forwardDistance >= 0f &&
                    forwardDistance <= lineStrikeLength &&
                    sideDistance <= lineStrikeWidth * 0.5f;
            }

            var radius = currentState == BossState.Melee
                ? meleeAttackRadius
                : currentState == BossState.WideBurst
                    ? wideBurstRadius
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
                    : currentState == BossState.WideBurst
                        ? wideBurstStunDuration
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
                    : currentState == BossState.WideBurst
                    ? wideBurstMotion
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
