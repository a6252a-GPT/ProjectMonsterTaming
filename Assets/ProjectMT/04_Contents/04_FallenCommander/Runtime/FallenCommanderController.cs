using System;
using System.Collections;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Input;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.FallenCommander
{
    [DisallowMultipleComponent]
    public sealed class FallenCommanderController : MonoBehaviour, IContentController, IBossDungeonHudSource, IBossDungeonTimeoutController, IBossDungeonBossKillController, IBossDungeonBossHealthDebugController, IBossDungeonAttackDebugController
    {
        [Header("Battle")]
        [SerializeField] private CombatWorld combatWorld;
        [SerializeField] private GameObject bossPrefab;
        [SerializeField] private Transform bossSpawnPoint;

        [Header("Commander")]
        [SerializeField] private GameObject commanderRoot;
        [SerializeField] private CommanderMoveController commanderMove;
        [SerializeField, Min(1)] private int commanderMaxHearts = 5;
        [SerializeField] private Sprite commanderHeartSprite;
        [SerializeField] private Button exitButton;

        [Header("Boss")]
        [SerializeField] private FallenCommanderBossConfig bossConfig;

        [Header("Dungeon")]
        [SerializeField, Min(1f)] private float timeLimitSeconds = 80f;
        [SerializeField, Min(0f)] private float battleStartDelaySeconds = 2f;
        [SerializeField, HideInInspector, Min(0f)] private float timeoutWipeWarningDuration = 0.8f;
        [SerializeField, HideInInspector, Min(0f)] private float timeoutWipeDeathResultDelay = 2f;
        [SerializeField, Min(0f)] private float timeoutWarningStartSeconds = 5f;

        private ContentContext context;
        private UnitActor bossActor;
        private HealthComponent commanderHealth;
        private FallenCommanderBossStateMachine stateMachine;
        private FallenCommanderBossFacingSmoother bossFacingSmoother;
        private FallenCommanderBossAnimationPresenter bossAnimationPresenter;
        private FallenCommanderBossAnimationPresenter commanderDeathAnimationPresenter;
        private FallenCommanderBossDeathPresentation bossDeathPresentation;
        private ICommanderSkillContentBridge commanderSkillBridge;
        private FallenCommanderHudPresenter hudPresenter;
        private FallenCommanderStartData startData;
        private float difficultyMultiplier = 1f;
        private float currentBreakGauge;
        private float breakRemainingTime;
        private bool isBroken;
        private float remainingTime;
        private int score;
        private bool isFinishing;
        private Coroutine deathRoutine;
        private float battleStartDelayRemaining;
        private bool isBattleStartDelay;
        private bool hasTriggeredFinalCharge;
        private bool isFinalChargePending;
        private readonly FallenCommanderFinalChargePattern finalChargePattern = new();
        private readonly FallenCommanderTimeoutWipePattern timeoutWipePattern = new();
        private FallenCommanderBossPhase currentBossPhase;
        private FallenCommanderBossPhase requestedBossPhase;
        private FallenCommanderAttackPattern pendingPhaseAttack;
        private float phaseTransitionRemainingTime;
        private float phaseIntroNoticeRemainingTime;
        private bool isPhaseTransitionActive;
        private bool isWaitingForPhaseSignature;
        private string phaseTransitionMessage = string.Empty;
        private bool isDebugPhaseJump;
        private bool isCommanderStunned;
        private int lastLoggedBossHealthPercent;

        private const float BreakGaugeDamageScale = 5f;
        private float RemainingBreakGauge =>
            Mathf.Max(0f, bossConfig.MaxBreakGauge - currentBreakGauge);
        private FallenCommanderTimeoutWipeData TimeoutWipe => bossConfig?.TimeoutWipe;

        public bool IsRunning { get; private set; }
        public event Action<FallenCommanderHudState> HudStateChanged;

        public void Initialize(ContentContext contentContext)
        {
            Shutdown();

            context = contentContext ??
                throw new ArgumentNullException(nameof(contentContext));
            startData = contentContext.StartData as FallenCommanderStartData;
            if (startData == null)
            {
                throw new ArgumentException(
                    "FallenCommanderStartData is required.",
                    nameof(contentContext));
            }

            ValidateReferences();
            BeginBattle();
        }

        // 전투에 필요한 런타임 상태를 초기화하고 보스·군단장·HUD를 준비한다.
        private void BeginBattle()
        {
            if (context == null || IsRunning)
            {
                return;
            }

            remainingTime = timeLimitSeconds;
            battleStartDelayRemaining = Mathf.Max(0f, battleStartDelaySeconds);
            isBattleStartDelay = battleStartDelayRemaining > 0f;
            hasTriggeredFinalCharge = false;
            isFinalChargePending = false;
            finalChargePattern.Cancel();
            currentBossPhase = FallenCommanderBossPhase.Phase1;
            requestedBossPhase = FallenCommanderBossPhase.Phase1;
            pendingPhaseAttack = FallenCommanderAttackPattern.Basic;
            phaseTransitionRemainingTime = 0f;
            phaseIntroNoticeRemainingTime = 0f;
            isPhaseTransitionActive = false;
            isWaitingForPhaseSignature = false;
            phaseTransitionMessage = string.Empty;
            isDebugPhaseJump = false;
            timeoutWipePattern.Cancel();
            score = 0;
            isFinishing = false;
            var stage = int.TryParse(context.RunInfo.StageId, out var selectedStage) &&
                        GrowthDungeonStageRules.IsValidStage(selectedStage)
                ? selectedStage
                : 1;
            difficultyMultiplier = GrowthDungeonStageRules.ResolveDifficultyMultiplier(stage);

            commanderRoot.SetActive(true);
            commanderMove.ResetToInitialPosition();
            commanderMove.SetInputEnabled(true);
            exitButton?.onClick.RemoveListener(Cancel);
            exitButton?.onClick.AddListener(Cancel);

            InitializeCommanderHealth();
            SpawnBoss();
            InitializeStateMachine();
            ConfigureCommanderSkills();
            InitializeHud();
            PresentInitialPhase();

            IsRunning = true;
            PublishHudState();
        }

        // 진행 중인 전투 상태와 이벤트·연출·입력 연결을 안전하게 정리한다.
        public void Shutdown()
        {
            IsRunning = false;
            battleStartDelayRemaining = 0f;
            isBattleStartDelay = false;
            hasTriggeredFinalCharge = false;
            isFinalChargePending = false;
            finalChargePattern.Cancel();
            currentBossPhase = FallenCommanderBossPhase.Phase1;
            requestedBossPhase = FallenCommanderBossPhase.Phase1;
            pendingPhaseAttack = FallenCommanderAttackPattern.Basic;
            phaseTransitionRemainingTime = 0f;
            phaseIntroNoticeRemainingTime = 0f;
            isPhaseTransitionActive = false;
            isWaitingForPhaseSignature = false;
            phaseTransitionMessage = string.Empty;
            isDebugPhaseJump = false;
            timeoutWipePattern.Cancel();

            if (deathRoutine != null)
            {
                StopCoroutine(deathRoutine);
                deathRoutine = null;
            }

            ShutdownCommanderSkills();

            stateMachine?.Shutdown();
            stateMachine = null;

            bossAnimationPresenter?.Stop();
            bossAnimationPresenter = null;

            commanderDeathAnimationPresenter?.Stop();
            commanderDeathAnimationPresenter = null;

            bossDeathPresentation?.Release();
            bossDeathPresentation = null;

            bossFacingSmoother?.Shutdown();
            bossFacingSmoother = null;

            if (bossActor != null)
            {
                bossActor.Health.Damaged -= HandleBossDamaged;
                bossActor.Died -= HandleBossDied;
                bossActor = null;
            }

            ResetBreakState();
            ReleaseHud();
            ReleaseCommanderHealth();

            commanderMove?.SetInputEnabled(false);
            exitButton?.onClick.RemoveListener(Cancel);
            combatWorld?.Clear();

            if (commanderRoot != null)
            {
                commanderRoot.SetActive(false);
            }

            context = null;
            startData = null;
            difficultyMultiplier = 1f;
            isCommanderStunned = false;
        }

        // 전투 준비·타이머·특수 패턴·보스 FSM을 매 프레임 순서대로 갱신한다.
        private void Update()
        {
            if (!IsRunning)
            {
                return;
            }

            phaseIntroNoticeRemainingTime = Mathf.Max(
                0f,
                phaseIntroNoticeRemainingTime - Time.deltaTime);

            if (timeoutWipePattern.IsActive)
            {
                var hasResolved = timeoutWipePattern.Tick(Time.unscaledDeltaTime);
                PublishHudState();
                if (hasResolved)
                {
                    BeginDeathSequence(
                        ContentOutcome.Fail,
                        true,
                        timeoutWipePattern.ResultDelay);
                }

                return;
            }

            if (isBattleStartDelay)
            {
                battleStartDelayRemaining = Mathf.Max(
                    0f,
                    battleStartDelayRemaining - Time.deltaTime);
                PublishHudState();

                if (battleStartDelayRemaining <= 0f)
                {
                    isBattleStartDelay = false;
                    PlayInitialPhaseSound();
                    PublishHudState();
                }

                return;
            }
            remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
            if (remainingTime <= 0f)
            {
                BeginTimeoutWipeSequence();
                return;
            }

            if (finalChargePattern.IsActive)
            {
                if (finalChargePattern.Tick(Time.deltaTime))
                {
                    ResolveFinalCharge();
                    return;
                }

                PublishHudState();
                return;
            }

            if (isPhaseTransitionActive)
            {
                stateMachine?.Tick(Time.deltaTime);
                phaseTransitionRemainingTime = Mathf.Max(
                    0f,
                    phaseTransitionRemainingTime - Time.deltaTime);
                if (phaseTransitionRemainingTime <= 0f)
                {
                    isPhaseTransitionActive = false;
                    stateMachine?.CompletePhaseTransition(pendingPhaseAttack);
                    isWaitingForPhaseSignature =
                        pendingPhaseAttack != FallenCommanderAttackPattern.Basic;
                    pendingPhaseAttack = FallenCommanderAttackPattern.Basic;
                }

                PublishHudState();
                return;
            }

            stateMachine?.Tick(Time.deltaTime);

            if (isWaitingForPhaseSignature && stateMachine != null && stateMachine.IsIdle)
            {
                isWaitingForPhaseSignature = false;
            }

            if (!isWaitingForPhaseSignature &&
                (TryStartNextPhaseTransition() || TryStartPendingFinalCharge()))
            {
                PublishHudState();
                return;
            }

            if (isBroken)
            {
                breakRemainingTime = Mathf.Max(
                    0f,
                    breakRemainingTime - Time.deltaTime);

                if (breakRemainingTime <= 0f)
                {
                    EndBreak();
                }
            }

            PublishHudState();
        }

        // 전투 시작에 필요한 참조와 서로 의존하는 보스 설정값을 검증한다.
        private void ValidateReferences()
        {
            if (combatWorld == null ||
                bossPrefab == null ||
                bossSpawnPoint == null ||
                commanderRoot == null ||
                commanderMove == null ||
                bossConfig == null ||
                bossConfig.PhaseConfig == null ||
                bossConfig.BasicAttack == null ||
                bossConfig.BasicAttack.TelegraphPrefab == null ||
                bossConfig.MeleeAttack == null ||
                bossConfig.MeleeAttack.TelegraphPrefab == null ||
                bossConfig.MarkStrike == null ||
                bossConfig.MarkStrike.TelegraphPrefab == null ||
                bossConfig.TrackingMark == null ||
                bossConfig.TrackingMark.TelegraphPrefab == null ||
                bossConfig.BlackHole == null ||
                bossConfig.BlackHole.TelegraphPrefab == null ||
                bossConfig.LineStrike == null ||
                bossConfig.LineStrike.TelegraphPrefab == null ||
                bossConfig.CorruptionRing == null ||
                bossConfig.CorruptionRing.TelegraphPrefab == null ||
                bossConfig.TwistedBattlefield == null ||
                bossConfig.TwistedBattlefield.TelegraphPrefab == null ||
                bossConfig.FallingBarrage == null ||
                bossConfig.FallingBarrage.ProjectilePrefab == null ||
                bossConfig.FallingBarrage.TelegraphPrefab == null ||
                bossConfig.FinalChargeTelegraphPrefab == null ||
                bossConfig.TimeoutWipe == null)
            {
                throw new InvalidOperationException(
                    "Fallen Commander references are missing.");
            }

            if (!bossConfig.PhaseConfig.TryValidate(out var phaseError))
            {
                throw new InvalidOperationException(
                    $"Fallen Commander phase settings are invalid: {phaseError}");
            }

            if (!bossConfig.TwistedBattlefield.TryValidate(
                    out var twistedBattlefieldError))
            {
                throw new InvalidOperationException(
                    "Fallen Commander twisted battlefield settings are invalid: " +
                    twistedBattlefieldError);
            }

            if (!bossConfig.FallingBarrage.TryValidate(out var fallingBarrageError))
            {
                throw new InvalidOperationException(
                    "Fallen Commander falling barrage settings are invalid: " +
                    fallingBarrageError);
            }

            if (bossConfig.TrackingMark == null ||
                bossConfig.CorruptionRing == null ||
                bossConfig.TrackingMarkLockDuration >= bossConfig.TrackingMark.WarningDuration ||
                bossConfig.CorruptionRingSafeRadius >= bossConfig.CorruptionRing.Radius ||
                bossConfig.BlackHoleCoreRadius >= bossConfig.BlackHole.Radius ||
                bossConfig.BlackHoleSpawnMinDistance > bossConfig.BlackHoleSpawnMaxDistance)
            {
                throw new InvalidOperationException(
                    "Fallen Commander phase or attack range settings are invalid.");
            }
        }

        private void InitializeCommanderHealth()
        {
            commanderHealth =
                commanderRoot.GetComponent<HealthComponent>();

            if (commanderHealth == null)
            {
                commanderHealth =
                    commanderRoot.AddComponent<HealthComponent>();
            }

            commanderHealth.Initialize(
                commanderMaxHearts,
                1f);

            commanderHealth.Damaged += HandleCommanderDamaged;
            commanderHealth.Died += HandleCommanderDied;
        }

        private void ReleaseCommanderHealth()
        {
            if (commanderHealth == null)
            {
                return;
            }

            commanderHealth.Damaged -= HandleCommanderDamaged;
            commanderHealth.Died -= HandleCommanderDied;
            commanderHealth = null;
        }

        private void SpawnBoss()
        {
            var stats = new UnitStatsSnapshot
            {
                maxHealth = bossConfig.BaseMaxHealth * difficultyMultiplier,
                damage = 1f,
                defense = bossConfig.BaseDefense * difficultyMultiplier,
                moveSpeed = bossConfig.BaseMoveSpeed,
                attackRange = bossConfig.AttackRange,
                attackInterval = 1f,
                projectileSpeed = 0f,
                ranged = false,
                criticalDamageMultiplier = 1.5f
            };

            var request = new UnitSpawnRequest(
                "fallen_commander_boss",
                stats,
                UnitTeam.Enemy,
                canMove: true,
                canAttack: false, //FallenCommanderBossStateMachine이 담당
                visualTint: Color.white);

            bossActor = combatWorld.SpawnUnit(
                bossPrefab,
                request,
                bossSpawnPoint.position,
                bossSpawnPoint.rotation);

            if (bossActor == null)
            {
                throw new InvalidOperationException(
                    "Fallen Commander boss spawn failed.");
            }

            bossActor.Died += HandleBossDied;
            bossActor.Health.Damaged += HandleBossDamaged;

            bossAnimationPresenter =
                bossActor.GetComponent<FallenCommanderBossAnimationPresenter>();
            if (bossAnimationPresenter == null)
            {
                bossAnimationPresenter =
                    bossActor.gameObject.AddComponent<FallenCommanderBossAnimationPresenter>();
            }
            bossAnimationPresenter.Configure(bossActor.transform);

            // 군단장 강제 타깃 지정
            bossActor.ForceTarget(
                commanderHealth,
                float.PositiveInfinity);

            InitializeBossFacing();
        }

        private void InitializeBossFacing()
        {
            bossFacingSmoother =
                bossActor.GetComponent<FallenCommanderBossFacingSmoother>();

            if (bossFacingSmoother == null)
            {
                bossFacingSmoother =
                    bossActor.gameObject.AddComponent<FallenCommanderBossFacingSmoother>();
            }

            bossFacingSmoother.Configure(
                commanderRoot.transform,
                bossConfig.TurnSpeed);

            lastLoggedBossHealthPercent = 100;
            Debug.Log("보스 체력: 100%", this);
        }

        // 보스 설정과 분리된 페이즈 데이터를 새 공격 상태 머신에 주입한다.
        private void InitializeStateMachine()
        {
            stateMachine = new FallenCommanderBossStateMachine();

            stateMachine.Configure(
                combatWorld,
                bossActor,
                commanderRoot.transform,
                commanderHealth,
                bossConfig.AttackInterval,
                bossAnimationPresenter,
                bossConfig.BreakMotion,
                bossConfig.BreakMotionDuration,
                bossConfig.BasicAttack,
                bossConfig.MeleeAttack,
                bossConfig.MarkStrike,
                bossConfig.TrackingMark,
                bossConfig.TrackingMarkLockDuration,
                bossConfig.BlackHole,
                bossConfig.BlackHoleActiveDuration,
                bossConfig.BlackHoleCoreRadius,
                bossConfig.BlackHoleSpawnMinDistance,
                bossConfig.BlackHoleSpawnMaxDistance,
                bossConfig.BlackHoleOuterPullSpeed,
                bossConfig.BlackHoleInnerPullSpeed,
                bossConfig.BlackHolePullStrengthCurve,
                commanderMove.InitialPosition,
                bossConfig.BlackHoleArenaHalfExtents,
                bossConfig.BlackHoleEndEffects,
                bossConfig.LineStrike,
                bossConfig.CorruptionRing,
                bossConfig.CorruptionRingSafeRadius,
                bossConfig.TwistedBattlefield,
                bossConfig.FallingBarrage,
                bossConfig.CloseAttackDistance,
                bossConfig.LineStrikeMinimumDistance,
                bossConfig.LineStrikeAlignmentThreshold,
                bossConfig.PhaseConfig,
                HandleCommanderStunChanged,
                HandleBossAttackStarted,
                bossFacingSmoother);
            stateMachine.SetPhase(currentBossPhase);
        }

        private void HandleCommanderStunChanged(bool isStunned)
        {
            isCommanderStunned = isStunned;
            commanderMove?.SetInputEnabled(IsRunning && !isStunned);
            Debug.Log(
                $"군단장 기절 : {(isStunned ? "시작" : "해제")}",
                this);
        }

        private void HandleBossAttackStarted(FallenCommanderAttackPattern attack)
        {
            if (attack != FallenCommanderAttackPattern.FallingBarrage ||
                bossConfig?.FallingBarrage == null)
            {
                return;
            }

            hudPresenter?.ShowAttackWarning(
                bossConfig.FallingBarrage.WarningMessage,
                bossConfig.FallingBarrage.WarningMessageDuration);
        }

        private void ConfigureCommanderSkills()
        {
            commanderSkillBridge = CommanderSkillContentBridgeLocator.Find(this);
            if (commanderSkillBridge == null)
            {
                Debug.LogError(
                    "Fallen Commander skill bridge is missing.",
                    this);
                return;
            }

            commanderSkillBridge.Configure(
                context.Progress,
                combatWorld,
                commanderRoot.transform,
                () => !IsRunning ||
                    isBattleStartDelay ||
                    timeoutWipePattern.IsActive ||
                    commanderMove == null ||
                    !commanderMove.IsInputEnabled ||
                    isCommanderStunned,
                () => isBroken ? bossConfig.BreakDamageMultiplier : 1f);
        }

        private void ShutdownCommanderSkills()
        {
            commanderSkillBridge?.Shutdown();
            commanderSkillBridge = null;
        }

        // 보스 피격 점수·페이즈·충전 광역기·브레이크 처리를 우선순위대로 수행한다.
        private void HandleBossDamaged(DamageReport report)
        {
            if (!IsRunning || bossActor == null)
            {
                return;
            }

            if (!isDebugPhaseJump)
            {
                score += Mathf.CeilToInt(report.AppliedDamage);
            }
            LogBossHealthThresholds();

            var phaseChanged = TryAdvanceBossPhase();
            var finalChargeScheduled = TryStartFinalCharge();

            if (phaseChanged ||
                isPhaseTransitionActive ||
                requestedBossPhase > currentBossPhase ||
                finalChargeScheduled ||
                isFinalChargePending)
            {
                PublishHudState();
                return;
            }

            if (!isBroken && bossActor.IsAlive)
            {
                var breakGaugeDamage = bossConfig.BreakGaugePerHit *
                    bossConfig.BreakGaugeAttackPowerMultiplier *
                    BreakGaugeDamageScale *
                    GetHealthThresholdBreakGaugeMultiplier();

                currentBreakGauge = Mathf.Min(
                    bossConfig.MaxBreakGauge,
                    currentBreakGauge + breakGaugeDamage);

                if (currentBreakGauge >= bossConfig.MaxBreakGauge)
                {
                    StartBreak();
                }
            }

            PublishHudState();
        }

        // 현재 체력으로 도달한 최종 페이즈를 예약하고 한 단계씩 순차 전환한다.
        private bool TryAdvanceBossPhase()
        {
            if (bossActor == null ||
                !bossActor.IsAlive ||
                bossActor.Health.MaxHealth <= 0f)
            {
                return false;
            }

            var healthRatio =
                bossActor.Health.CurrentHealth / bossActor.Health.MaxHealth;
            var targetPhase = bossConfig.PhaseConfig
                .GetPhaseForHealthRatio(healthRatio)
                .Phase;
            if (targetPhase <= requestedBossPhase)
            {
                return false;
            }

            requestedBossPhase = targetPhase;
            TryStartNextPhaseTransition();
            return true;
        }

        // 예약된 다음 페이즈가 있으면 현재 페이즈에서 정확히 한 단계만 전환한다.
        private bool TryStartNextPhaseTransition()
        {
            if (isPhaseTransitionActive ||
                isWaitingForPhaseSignature ||
                finalChargePattern.IsActive ||
                requestedBossPhase <= currentBossPhase ||
                bossActor == null ||
                !bossActor.IsAlive)
            {
                return false;
            }

            currentBossPhase = (FallenCommanderBossPhase)((int)currentBossPhase + 1);
            var phaseData = bossConfig.PhaseConfig.GetPhase(currentBossPhase);
            pendingPhaseAttack = phaseData.HasSignatureAttack
                ? phaseData.SignatureAttack
                : FallenCommanderAttackPattern.Basic;
            phaseTransitionRemainingTime = phaseData.TransitionDuration;
            phaseIntroNoticeRemainingTime = 0f;
            phaseTransitionMessage = phaseData.TransitionMessage;
            isPhaseTransitionActive = true;
            isBroken = false;
            breakRemainingTime = 0f;
            currentBreakGauge = 0f;
            stateMachine?.BeginPhaseTransition(
                currentBossPhase,
                phaseTransitionRemainingTime);
            PlayPhaseTransitionSound(phaseData);
            Debug.Log($"보스 {((int)currentBossPhase)} 페이즈 진입", this);
            return true;
        }

        // 전투 준비시간 안에 1페이즈 문구를 표시하고 사운드는 전투 시작 시점에 예약한다.
        private void PresentInitialPhase()
        {
            var phaseData = bossConfig.PhaseConfig.GetPhase(
                FallenCommanderBossPhase.Phase1);
            phaseIntroNoticeRemainingTime = battleStartDelayRemaining > 0f
                ? Mathf.Min(battleStartDelayRemaining, phaseData.TransitionDuration)
                : phaseData.TransitionDuration;
            phaseTransitionMessage = phaseData.TransitionMessage;

            if (!isBattleStartDelay)
            {
                PlayPhaseTransitionSound(phaseData);
            }
        }

        // 준비시간이 끝난 순간 1페이즈 진입 사운드를 한 번 재생한다.
        private void PlayInitialPhaseSound()
        {
            var phaseData = bossConfig.PhaseConfig.GetPhase(
                FallenCommanderBossPhase.Phase1);
            PlayPhaseTransitionSound(phaseData);
        }

        // 페이즈에 사운드가 지정된 경우 보스 위치에서 한 번 재생한다.
        private void PlayPhaseTransitionSound(FallenCommanderPhaseData phaseData)
        {
            if (phaseData?.TransitionSound == null || bossActor == null)
            {
                return;
            }

            AudioSource.PlayClipAtPoint(
                phaseData.TransitionSound,
                bossActor.transform.position);
        }

        // 충전 종료 시 표시된 원형 범위 안의 군단장에게만 하트 1개 피해를 적용한다.
        private void ResolveFinalCharge()
        {
            var effectPosition = finalChargePattern.CenterPosition;
            var effectDirection = bossActor == null
                ? Vector3.forward
                : bossActor.transform.forward;
            if (!finalChargePattern.Complete(out var commanderInside))
            {
                return;
            }

            FallenCommanderAttackEffectPlayer.PlayResolve(
                bossConfig.FinalChargeEffects,
                effectPosition,
                effectDirection,
                bossActor == null ? null : bossActor.transform.parent,
                bossActor == null ? null : bossActor.transform,
                commanderRoot == null ? null : commanderRoot.transform);
            bossAnimationPresenter?.Play(
                bossConfig.FinalChargeCastMotion,
                stopAfterMotion: true,
                durationOverride: bossConfig.FinalChargeCastMotionDuration,
                playbackSpeed: bossConfig.FinalChargeCastMotionSpeed,
                normalizedStart: bossConfig.FinalChargeCastMotionStart,
                normalizedEnd: bossConfig.FinalChargeCastMotionEnd);

            if (commanderHealth != null &&
                commanderHealth.IsAlive &&
                commanderInside)
            {
                FallenCommanderAttackEffectPlayer.PlayHit(
                    bossConfig.FinalChargeEffects,
                    commanderRoot.transform.position,
                    effectDirection,
                    bossActor == null ? null : bossActor.transform.parent,
                    bossActor == null ? null : bossActor.transform,
                    commanderRoot.transform);
                commanderHealth.ApplyDamage(new DamageRequest(
                    bossActor,
                    1f,
                    commanderRoot.transform.position));
                Debug.Log("충전 광역 공격 적중: 하트 1개 피해", this);
            }

            if (!IsRunning || commanderHealth == null || !commanderHealth.IsAlive)
            {
                return;
            }

            InitializeStateMachine();
            PublishHudState();
        }

        // 일반 전투 흐름을 중지하고 제한시간 전멸기 모듈을 시작한다.
        private void BeginTimeoutWipeSequence()
        {
            if (!IsRunning ||
                isFinishing ||
                timeoutWipePattern.IsActive ||
                commanderRoot == null ||
                commanderHealth == null)
            {
                return;
            }

            finalChargePattern.Cancel();
            isPhaseTransitionActive = false;
            phaseTransitionRemainingTime = 0f;
            pendingPhaseAttack = FallenCommanderAttackPattern.Basic;
            ShutdownCommanderSkills();
            stateMachine?.Shutdown();
            commanderMove?.SetInputEnabled(false);
            timeoutWipePattern.Begin(
                TimeoutWipe,
                timeoutWipeWarningDuration,
                timeoutWipeDeathResultDelay,
                bossActor,
                commanderRoot.transform,
                commanderHealth,
                bossAnimationPresenter,
                bossActor == null ? null : bossActor.transform.parent);
            PublishHudState();
        }

        // 체력 조건에 도달하면 충전 광역기를 예약하고 선행 페이즈 연출이 없을 때 시작한다.
        private bool TryStartFinalCharge()
        {
            if (hasTriggeredFinalCharge ||
                bossActor == null ||
                !bossActor.IsAlive ||
                bossActor.Health.MaxHealth <= 0f)
            {
                return false;
            }

            var healthRatio =
                bossActor.Health.CurrentHealth / bossActor.Health.MaxHealth;
            if (healthRatio > bossConfig.FinalChargeHealthRatio)
            {
                return false;
            }

            isFinalChargePending = true;
            TryStartPendingFinalCharge();
            return true;
        }

        // 예약된 페이즈와 보장 패턴이 끝난 뒤 대기 중인 충전 광역기를 시작한다.
        private bool TryStartPendingFinalCharge()
        {
            if (!isFinalChargePending ||
                isPhaseTransitionActive ||
                isWaitingForPhaseSignature ||
                requestedBossPhase > currentBossPhase)
            {
                return false;
            }

            return StartFinalCharge();
        }

        // 일반 보스 패턴을 중지하고 충전 광역기 상태와 범위 전조를 시작한다.
        private bool StartFinalCharge()
        {
            if (finalChargePattern.IsActive ||
                timeoutWipePattern.IsActive ||
                isFinishing ||
                bossActor == null ||
                !bossActor.IsAlive)
            {
                return false;
            }

            if (!finalChargePattern.Begin(
                    bossActor.transform,
                    commanderRoot.transform,
                    bossConfig.FinalChargeTelegraphPrefab,
                    bossConfig.FinalChargeDuration,
                    bossConfig.FinalChargeTelegraphHoldDuration,
                    bossConfig.FinalChargeRadius))
            {
                return false;
            }

            hasTriggeredFinalCharge = true;
            isFinalChargePending = false;
            isBroken = false;
            breakRemainingTime = 0f;
            currentBreakGauge = 0f;
            isPhaseTransitionActive = false;
            isWaitingForPhaseSignature = false;
            phaseTransitionRemainingTime = 0f;
            pendingPhaseAttack = FallenCommanderAttackPattern.Basic;
            stateMachine?.Shutdown();
            bossAnimationPresenter?.PlayPreCast(
                bossConfig.FinalChargePreCastMotion,
                playbackSpeed: bossConfig.FinalChargePreCastMotionSpeed,
                normalizedStart: bossConfig.FinalChargePreCastMotionStart,
                normalizedEnd: bossConfig.FinalChargePreCastMotionEnd);
            FallenCommanderAttackEffectPlayer.PlayStart(
                bossConfig.FinalChargeEffects,
                bossActor.transform.TransformPoint(
                    bossConfig.FinalChargeStartEffectOffset),
                bossActor.transform.forward,
                bossActor.transform,
                bossActor.transform,
                commanderRoot == null ? null : commanderRoot.transform);
            Debug.Log(
                $"충전 광역 공격 준비 시작: {bossConfig.FinalChargeDuration:0.0}초",
                this);
            return true;
        }

        // 현재 페이즈에 맞는 브레이크 게이지 획득 배율을 반환한다.
        private float GetHealthThresholdBreakGaugeMultiplier()
        {
            if (bossActor == null ||
                !bossActor.IsAlive ||
                bossActor.Health.MaxHealth <= 0f)
            {
                return 1f;
            }

            if (currentBossPhase == FallenCommanderBossPhase.Phase3)
            {
                return bossConfig.BreakGaugePhaseThreeMultiplier;
            }

            if (currentBossPhase == FallenCommanderBossPhase.Phase2)
            {
                return bossConfig.BreakGaugePhaseTwoMultiplier;
            }

            return 1f;
        }

        private void StartBreak()
        {
            if (!IsRunning || isBroken)
            {
                return;
            }

            isBroken = true;
            breakRemainingTime = bossConfig.BreakDuration;
            stateMachine?.EnterBroken();
            PublishHudState();
        }

        private void EndBreak()
        {
            if (!isBroken)
            {
                return;
            }

            isBroken = false;
            breakRemainingTime = 0f;
            currentBreakGauge = 0f;
            stateMachine?.ExitBroken();
            PublishHudState();
        }

        // 브레이크와 페이즈 예약 상태를 최초 전투 상태로 되돌린다.
        private void ResetBreakState()
        {
            currentBreakGauge = 0f;
            breakRemainingTime = 0f;
            isBroken = false;
            currentBossPhase = FallenCommanderBossPhase.Phase1;
            requestedBossPhase = FallenCommanderBossPhase.Phase1;
            pendingPhaseAttack = FallenCommanderAttackPattern.Basic;
            phaseTransitionRemainingTime = 0f;
            phaseIntroNoticeRemainingTime = 0f;
            isPhaseTransitionActive = false;
            isWaitingForPhaseSignature = false;
            phaseTransitionMessage = string.Empty;
            isCommanderStunned = false;
        }

        private void InitializeHud()
        {
            hudPresenter =
                GetComponentInChildren<FallenCommanderHudPresenter>(true);

            if (hudPresenter == null)
            {
                Debug.LogError(
                    "Fallen Commander HUD presenter is missing.",
                    this);
                return;
            }

            hudPresenter.SetCommanderHeartSprite(commanderHeartSprite);
            hudPresenter.Bind(
                this,
                context.RunInfo.RunMode == ContentRunMode.SeedTest);
        }

        private void ReleaseHud()
        {
            if (hudPresenter == null)
            {
                return;
            }

            hudPresenter.Unbind();
            hudPresenter.SetVisible(false);
            hudPresenter = null;
        }

        // 현재 전투 값과 페이즈 전환 문구를 HUD에 전달한다.
        private void PublishHudState()
        {
            if (bossActor == null)
            {
                return;
            }

            HudStateChanged?.Invoke(new FallenCommanderHudState(
                bossActor.Health.CurrentHealth,
                bossActor.Health.MaxHealth,
                RemainingBreakGauge,
                bossConfig.MaxBreakGauge,
                isBroken,
                score,
                remainingTime,
                0,
                0,
                0f,
                breakRemainingTime,
                bossConfig.BreakDuration,
                commanderHealth == null
                    ? 0
                    : Mathf.CeilToInt(commanderHealth.CurrentHealth),
                commanderMaxHearts,
                stateMachine != null && stateMachine.IsCommanderStunned,
                stateMachine == null
                    ? 0f
                    : stateMachine.CommanderStunRemainingTime,
                bossConfig.MarkStrike.StunDuration,
                finalChargePattern.IsActive,
                finalChargePattern.RemainingTime,
                finalChargePattern.IsActive
                    ? finalChargePattern.Duration
                    : bossConfig.FinalChargeDuration,
                timeoutWipePattern.IsActive,
                !isBattleStartDelay &&
                !timeoutWipePattern.IsActive &&
                    remainingTime > 0f &&
                    remainingTime <= timeoutWarningStartSeconds,
                timeoutWarningStartSeconds,
                isPhaseTransitionActive || phaseIntroNoticeRemainingTime > 0f,
                (int)currentBossPhase,
                phaseTransitionMessage,
                TimeoutWipe?.WarningMessage,
                TimeoutWipe?.WarningPulseInterval ?? 0.45f));
        }

        private void HandleCommanderDamaged(DamageReport report)
        {
            PublishHudState();
            Debug.Log(
                $"군단장 체력: {report.RemainingHealth} / {commanderHealth.MaxHealth}",
                this);
        }

        private void HandleCommanderDied(DamageReport report)
        {
            if (timeoutWipePattern.IsActive)
            {
                return;
            }

            BeginDeathSequence(
                ContentOutcome.Fail,
                true);
        }

        private void HandleBossDied(UnitActor actor)
        {
            if (actor != bossActor)
            {
                return;
            }

            if (timeoutWipePattern.IsActive)
            {
                return;
            }

            finalChargePattern.Cancel();
            isPhaseTransitionActive = false;
            phaseTransitionRemainingTime = 0f;
            pendingPhaseAttack = FallenCommanderAttackPattern.Basic;
            PublishHudState();

            BeginDeathSequence(
                ContentOutcome.Complete,
                false);
        }

        public void DebugTimeout()
        {
            if (IsRunning)
            {
                remainingTime = 0f;
                BeginTimeoutWipeSequence();
            }
        }

        public void DebugReduceTimeTenSeconds()
        {
            if (!IsRunning || isFinishing || timeoutWipePattern.IsActive)
            {
                return;
            }

            remainingTime = Mathf.Max(0f, remainingTime - 10f);
            PublishHudState();
            if (remainingTime <= 0f)
            {
                BeginTimeoutWipeSequence();
            }
        }

        public void DebugKillBoss()
        {
            if (!IsRunning ||
                timeoutWipePattern.IsActive ||
                bossActor == null ||
                !bossActor.IsAlive)
            {
                return;
            }

            bossActor.Health.ApplyDamage(new DamageRequest(
                null,
                bossActor.Health.CurrentHealth,
                bossActor.transform.position));
        }

        public void DebugDamageBossTenPercent()
        {
            if (!IsRunning ||
                timeoutWipePattern.IsActive ||
                bossActor == null ||
                !bossActor.IsAlive)
            {
                return;
            }

            bossActor.Health.ApplyDamage(new DamageRequest(
                null,
                bossActor.Health.MaxHealth * 0.1f,
                bossActor.transform.position));
        }

        // DEV 버튼에서 지정한 페이즈 체력으로 보스를 초기화하고 정상 전환 절차를 실행한다.
        public void DebugSetBossPhase(int phaseNumber)
        {
            if (!IsRunning ||
                isBattleStartDelay ||
                timeoutWipePattern.IsActive ||
                isFinishing ||
                bossActor == null ||
                !bossActor.IsAlive)
            {
                return;
            }

            var targetPhase = (FallenCommanderBossPhase)Mathf.Clamp(
                phaseNumber,
                (int)FallenCommanderBossPhase.Phase1,
                (int)FallenCommanderBossPhase.Phase3);
            finalChargePattern.Cancel();
            hasTriggeredFinalCharge = false;
            isFinalChargePending = false;
            stateMachine?.Shutdown();
            ResetBreakState();
            bossActor.Health.Heal(bossActor.Health.MaxHealth);
            InitializeStateMachine();

            if (targetPhase == FallenCommanderBossPhase.Phase1)
            {
                PublishHudState();
                return;
            }

            var targetRatio = bossConfig.PhaseConfig
                .GetPhase(targetPhase)
                .HealthRatio;
            var debugDamage = bossActor.Health.MaxHealth * (1f - targetRatio);
            isDebugPhaseJump = true;
            try
            {
                bossActor.Health.ApplyDamage(new DamageRequest(
                    bossActor,
                    debugDamage,
                    bossActor.transform.position));
            }
            finally
            {
                isDebugPhaseJump = false;
            }
        }

        private void LogBossHealthThresholds()
        {
            if (bossActor == null || bossActor.Health.MaxHealth <= 0f)
            {
                return;
            }

            var healthRatio = Mathf.Clamp01(
                bossActor.Health.CurrentHealth / bossActor.Health.MaxHealth);
            var currentHealthPercent = Mathf.Clamp(
                Mathf.FloorToInt(healthRatio * 10f) * 10,
                0,
                100);

            for (var percent = lastLoggedBossHealthPercent - 10;
                 percent >= currentHealthPercent;
                 percent -= 10)
            {
                Debug.Log($"보스 체력: {percent}%", this);
            }

            lastLoggedBossHealthPercent = Mathf.Min(
                lastLoggedBossHealthPercent,
                currentHealthPercent);
        }

        public void DebugBasicAttack()
        {
            if (!IsRunning || isBattleStartDelay || finalChargePattern.IsActive || timeoutWipePattern.IsActive)
            {
                return;
            }

            stateMachine?.DebugForceBasicAttack();
        }

        public void DebugMeleeAttack()
        {
            if (!IsRunning || isBattleStartDelay || finalChargePattern.IsActive || timeoutWipePattern.IsActive)
            {
                return;
            }

            stateMachine?.DebugForceMeleeAttack();
        }

        public void DebugLineStrike()
        {
            if (!IsRunning || isBattleStartDelay || finalChargePattern.IsActive || timeoutWipePattern.IsActive)
            {
                return;
            }

            stateMachine?.DebugForceLineStrike();
        }

        public void DebugCorruptionRing()
        {
            if (!IsRunning || isBattleStartDelay || finalChargePattern.IsActive || timeoutWipePattern.IsActive)
            {
                return;
            }

            stateMachine?.DebugForceCorruptionRing();
        }

        // DEV 버튼에서 연속 장판 공격을 현재 페이즈 설정으로 강제 실행한다.
        public void DebugTwistedBattlefield()
        {
            if (!IsRunning ||
                isBattleStartDelay ||
                finalChargePattern.IsActive ||
                timeoutWipePattern.IsActive)
            {
                return;
            }

            stateMachine?.DebugForceTwistedBattlefield();
        }

        public void DebugFallingBarrage()
        {
            if (!IsRunning ||
                isBattleStartDelay ||
                finalChargePattern.IsActive ||
                timeoutWipePattern.IsActive)
            {
                return;
            }

            stateMachine?.DebugForceFallingBarrage();
        }

        public void DebugMarkStrike()
        {
            if (!IsRunning || isBattleStartDelay || finalChargePattern.IsActive || timeoutWipePattern.IsActive)
            {
                return;
            }

            stateMachine?.DebugForceMarkStrike();
        }

        public void DebugTrackingMark()
        {
            if (!IsRunning || isBattleStartDelay || finalChargePattern.IsActive || timeoutWipePattern.IsActive)
            {
                return;
            }

            stateMachine?.DebugForceTrackingMark();
        }

        public void DebugWideBurst()
        {
            if (!IsRunning || isBattleStartDelay || finalChargePattern.IsActive || timeoutWipePattern.IsActive)
            {
                return;
            }

            stateMachine?.DebugForceBlackHole();
        }

        public void DebugChargedWideBurst()
        {
            if (!IsRunning || isBattleStartDelay || timeoutWipePattern.IsActive)
            {
                return;
            }

            StartFinalCharge();
        }

        public bool PreviewBossAttack(FallenCommanderAttackData attack)
        {
            if (!Application.isPlaying ||
                bossActor == null ||
                bossAnimationPresenter == null ||
                attack == null)
            {
                return false;
            }

            bossAnimationPresenter.Configure(bossActor.transform);
            bossAnimationPresenter.PlaySequence(
                attack.PreCastMotion,
                attack.WarningDuration + attack.TelegraphHoldDuration,
                attack.PreCastMotionSpeed,
                attack.CastMotion,
                attack.CastMotionDuration,
                attack.CastMotionSpeed,
                attack.PreCastMotionStart,
                attack.PreCastMotionEnd,
                attack.CastMotionStart,
                attack.CastMotionEnd);
            return true;
        }

        public bool PreviewBossMotion(
            AnimationClip motion,
            float duration,
            float playbackSpeed = 1f,
            float normalizedStart = 0f,
            float normalizedEnd = 1f)
        {
            if (!Application.isPlaying ||
                bossActor == null ||
                bossAnimationPresenter == null ||
                motion == null)
            {
                return false;
            }

            bossAnimationPresenter.Configure(bossActor.transform);
            bossAnimationPresenter.Play(
                motion,
                stopAfterMotion: true,
                durationOverride: duration,
                playbackSpeed: playbackSpeed,
                normalizedStart: normalizedStart,
                normalizedEnd: normalizedEnd);
            return true;
        }

        private void Finish(ContentOutcome outcome)
        {
            if (!IsRunning || isFinishing)
            {
                return;
            }

            isFinishing = true;
            IsRunning = false;

            ShutdownCommanderSkills();
            stateMachine?.Shutdown();
            commanderMove?.SetInputEnabled(false);
            exitButton?.onClick.RemoveListener(Cancel);
            ReleaseHud();

            ExitContent(outcome);
        }

        private void BeginDeathSequence(
            ContentOutcome outcome,
            bool isCommanderDeath,
            float resultDelayOverride = -1f)
        {
            if (!IsRunning || isFinishing)
            {
                return;
            }

            isFinishing = true;
            IsRunning = false;
            ShutdownCommanderSkills();
            stateMachine?.Shutdown();
            commanderMove?.SetInputEnabled(false);
            exitButton?.onClick.RemoveListener(Cancel);

            if (isCommanderDeath)
            {
                commanderDeathAnimationPresenter =
                    commanderRoot.GetComponent<FallenCommanderBossAnimationPresenter>();
                if (commanderDeathAnimationPresenter == null)
                {
                    commanderDeathAnimationPresenter =
                        commanderRoot.AddComponent<FallenCommanderBossAnimationPresenter>();
                }

                commanderDeathAnimationPresenter.Configure(commanderRoot.transform);
                commanderDeathAnimationPresenter.Play(
                    bossConfig.CommanderDeathMotion,
                    stopAfterMotion: true,
                    durationOverride: bossConfig.CommanderDeathMotionDuration);
            }
            else
            {
                bossDeathPresentation =
                    FallenCommanderBossDeathPresentation.Create(
                        bossActor,
                        transform);
                if (bossDeathPresentation != null)
                {
                    bossDeathPresentation.Play(
                        bossConfig.DeathMotion,
                        bossConfig.DeathMotionDuration);
                }
                else
                {
                    bossAnimationPresenter?.Configure(bossActor.transform);
                    bossAnimationPresenter?.Play(
                        bossConfig.DeathMotion,
                        stopAfterMotion: true,
                        durationOverride: bossConfig.DeathMotionDuration);
                }
            }

            var resultDelay = resultDelayOverride >= 0f
                ? resultDelayOverride
                : bossConfig.DeathResultDelay;
            deathRoutine = StartCoroutine(
                CompleteAfterDeath(outcome, resultDelay));
        }

        // 사망 연출의 결과 대기 시간을 실제 시간 기준으로 보낸 뒤 콘텐츠 결과를 반환한다.
        private IEnumerator CompleteAfterDeath(
            ContentOutcome outcome,
            float resultDelay)
        {
            yield return new WaitForSecondsRealtime(resultDelay);
            bossDeathPresentation?.Release();
            bossDeathPresentation = null;
            commanderDeathAnimationPresenter?.Stop();
            commanderDeathAnimationPresenter = null;
            deathRoutine = null;
            ReleaseHud();
            ExitContent(outcome);
        }

        private void ExitContent(ContentOutcome outcome)
        {
            var timeoutWipeElapsed = timeoutWipePattern.ConsumeElapsedRealtime();
            if (timeoutWipeElapsed >= 0f)
            {
                Debug.Log(
                    $"시간 종료 전멸 연출 완료: {timeoutWipeElapsed:0.00}초",
                    this);
            }

            var result = new FallenCommanderResult(
                score,
                remainingTime,
                outcome == ContentOutcome.Complete);

            if (outcome == ContentOutcome.Complete)
            {
                context?.Exit.Complete(result);
                return;
            }

            context?.Exit.Fail(result);
        }

        private void Cancel()
        {
            if (context == null || isFinishing)
            {
                return;
            }

            IsRunning = false;
            isFinishing = true;
            timeoutWipePattern.Cancel();
            ShutdownCommanderSkills();
            stateMachine?.Shutdown();
            commanderMove?.SetInputEnabled(false);
            exitButton?.onClick.RemoveListener(Cancel);
            ReleaseHud();
            context.Exit.Cancel();
        }
    }
}
