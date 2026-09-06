using System;
using System.Collections;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Input;
using ProjectMT.Shared.Pooling;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.FallenCommander
{
    [DisallowMultipleComponent]
    public sealed class FallenCommanderController : MonoBehaviour, IContentController, IBossDungeonHudSource
    {
        private const string CommanderDamageVoiceResourcePath =
            "Audio/CommanderVoice/SFX_CommanderDamageVoice";

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
        [SerializeField] private FallenCommanderExitConfirmationDialog exitConfirmationDialog;

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
        private GameObject activeBossPrefab;
        private Vector3 bossBaseLocalScale = Vector3.one;
        private FallenCommanderPhaseData pendingPhaseBossPresentation;
        private HealthComponent commanderHealth;
        private FallenCommanderBossStateMachine stateMachine;
        private FallenCommanderBossFacingSmoother bossFacingSmoother;
        private FallenCommanderBossAnimationPresenter bossAnimationPresenter;
        private FallenCommanderBossAnimationPresenter commanderAnimationPresenter;
        private FallenCommanderBossDeathPresentation bossDeathPresentation;
        private ICommanderSkillContentBridge commanderSkillBridge;
        private FallenCommanderHudPresenter hudPresenter;
        private FallenCommanderDebugController debugController;
        private FallenCommanderStartData startData;
        private FallenCommanderBossStatsConfig bossStats;
        private FallenCommanderAttackSetConfig attackSet;
        private FallenCommanderFinalChargeConfig finalChargeConfig;
        private FallenCommanderPresentationConfig presentationConfig;
        private float difficultyMultiplier = 1f;
        private int score;
        private Coroutine deathRoutine;
        private readonly FallenCommanderBattleFlow battleFlow = new();
        private readonly FallenCommanderPhaseRuntime phaseRuntime = new();
        private readonly FallenCommanderBreakRuntime breakRuntime = new();
        private bool hasTriggeredFinalCharge;
        private bool isFinalChargePending;
        private readonly FallenCommanderFinalChargePattern finalChargePattern = new();
        private readonly FallenCommanderTimeoutWipePattern timeoutWipePattern = new();
        private readonly FallenCommanderDamageDelayQueue delayedDamageQueue = new();
        private bool isDebugPhaseJump;
        private bool isCommanderStunned;
        private int lastLoggedBossHealthPercent;
        private SfxCue commanderDamageVoice;
        private GameObject phaseVideoObject;
        private ProjectMT.Shared.UI.ISkippableVideoOverlay phaseVideo;
        private bool isPhaseVideoActive;
        private float phaseVideoPreviousTimeScale;
        private int phaseVideoVersion;

        private float RemainingBreakGauge =>
            breakRuntime.RemainingGauge(bossConfig.MaxBreakGauge);
        private FallenCommanderTimeoutWipeData TimeoutWipe => bossConfig?.TimeoutWipe;

        public bool IsRunning => battleFlow.IsRunning;
#if UNITY_EDITOR
        public IBossDungeonDebugController DebugController => debugController;
#endif
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

            commanderDamageVoice = Resources.Load<SfxCue>(CommanderDamageVoiceResourcePath);

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

            battleFlow.Begin(timeLimitSeconds, battleStartDelaySeconds);
            bossStats = FallenCommanderBossConfigViews.CreateStats(bossConfig);
            attackSet = FallenCommanderBossConfigViews.CreateAttackSet(bossConfig);
            finalChargeConfig = FallenCommanderBossConfigViews.CreateFinalCharge(bossConfig);
            presentationConfig = FallenCommanderBossConfigViews.CreatePresentation(bossConfig);
            hasTriggeredFinalCharge = false;
            isFinalChargePending = false;
            finalChargePattern.Cancel();
            delayedDamageQueue.Clear();
            phaseRuntime.Configure(bossConfig.PhaseConfig);
            breakRuntime.Reset();
            isDebugPhaseJump = false;
            timeoutWipePattern.Cancel();
            score = 0;
            var stage = int.TryParse(context.RunInfo.StageId, out var selectedStage) &&
                        GrowthDungeonStageRules.IsValidStage(selectedStage)
                ? selectedStage
                : 1;
            difficultyMultiplier = GrowthDungeonStageRules.ResolveDifficultyMultiplier(stage);

            commanderRoot.SetActive(true);
            commanderMove.ResetToInitialPosition();
            commanderMove.EvadeStarted -= HandleCommanderEvadeStarted;
            commanderMove.EvadeStarted += HandleCommanderEvadeStarted;
            commanderMove.SetInputEnabled(true);
            ConfigureExitConfirmation();

            InitializeCommanderHealth();
            SpawnBoss();
            ApplyPhaseBossPresentation(phaseRuntime.CurrentData);
            InitializeStateMachine();
            ConfigureCommanderSkills();
            InitializeHud();
            PresentInitialPhase();

            PublishHudState();
        }

        // 진행 중인 전투 상태와 이벤트·연출·입력 연결을 안전하게 정리한다.
        public void Shutdown()
        {
            CancelPhaseVideo();
            battleFlow.Reset();
            hasTriggeredFinalCharge = false;
            isFinalChargePending = false;
            finalChargePattern.Cancel();
            phaseRuntime.Reset();
            breakRuntime.Reset();
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

            commanderAnimationPresenter?.Stop();
            commanderAnimationPresenter = null;

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
            activeBossPrefab = null;
            bossBaseLocalScale = Vector3.one;
            pendingPhaseBossPresentation = null;

            ResetBreakState();
            ReleaseHud();
            ReleaseCommanderHealth();

            if (commanderMove != null)
            {
                commanderMove.EvadeStarted -= HandleCommanderEvadeStarted;
                commanderMove.SetInputEnabled(false);
            }
            ReleaseExitConfirmation();
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

            if (isPhaseVideoActive)
            {
                if (phaseVideoObject != null && phaseVideo.IsScreenCovered)
                    TryApplyPendingPhaseBossPresentation(force: true);
                return;
            }
            delayedDamageQueue.Tick(Time.deltaTime);
            if (!IsRunning)
            {
                return;
            }

            phaseRuntime.TickNotice(Time.deltaTime);

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

            if (battleFlow.IsStartDelayActive)
            {
                var battleStarted = battleFlow.TickStartDelay(Time.deltaTime);
                PublishHudState();

                if (battleStarted)
                {
                    PlayInitialPhaseSound();
                    PublishHudState();
                }

                return;
            }
            if (battleFlow.TickTimeLimit(Time.deltaTime))
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

            if (phaseRuntime.IsTransitionActive)
            {
                TryApplyPendingPhaseBossPresentation();
                stateMachine?.Tick(Time.deltaTime);
                if (phaseRuntime.TickTransition(
                        Time.deltaTime,
                        out var signatureAttack))
                {
                    TryApplyPendingPhaseBossPresentation(force: true);
                    stateMachine?.CompletePhaseTransition(signatureAttack);
                    ApplyCurrentPhaseBossScale();
                }

                PublishHudState();
                return;
            }

            stateMachine?.Tick(Time.deltaTime);

            phaseRuntime.CompleteSignatureIfIdle(stateMachine != null && stateMachine.IsIdle);

            ApplyCurrentPhaseBossScale();

            if (!phaseRuntime.IsWaitingForSignature &&
                (TryStartNextPhaseTransition() || TryStartPendingFinalCharge()))
            {
                PublishHudState();
                return;
            }

            if (breakRuntime.Tick(Time.deltaTime))
            {
                EndBreak();
            }

            PublishHudState();
        }

        private void LateUpdate()
        {
            ApplyBossHitFeedbackScale();
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

            if (!bossConfig.HasValidCommanderDamageMultiplier)
            {
                throw new InvalidOperationException(
                    "Fallen Commander damage multiplier must be a finite value greater than zero.");
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
            bossActor = CreateBossActor(
                bossPrefab,
                bossSpawnPoint.position,
                bossSpawnPoint.rotation);

            if (bossActor == null)
            {
                throw new InvalidOperationException(
                    "Fallen Commander boss spawn failed.");
            }

            activeBossPrefab = bossPrefab;
            ConfigureBossActor(logFullHealth: true);
        }

        private UnitActor CreateBossActor(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation)
        {
            var stats = new UnitStatsSnapshot
            {
                maxHealth = bossStats.BaseMaxHealth * difficultyMultiplier,
                damage = 1f,
                defense = bossStats.BaseDefense * difficultyMultiplier,
                moveSpeed = bossStats.BaseMoveSpeed,
                attackRange = bossStats.AttackRange,
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

            return combatWorld.SpawnUnit(
                prefab,
                request,
                position,
                rotation);
        }

        private void ConfigureBossActor(bool logFullHealth)
        {
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

            InitializeBossFacing(logFullHealth);
        }

        private void InitializeBossFacing(bool logFullHealth)
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

            bossBaseLocalScale = bossActor.transform.localScale;
            var healthRatio = bossActor.Health.MaxHealth <= 0f
                ? 1f
                : bossActor.Health.CurrentHealth / bossActor.Health.MaxHealth;
            lastLoggedBossHealthPercent = Mathf.FloorToInt(healthRatio * 100f);
            if (logFullHealth)
            {
                Debug.Log("보스 체력: 100%", this);
            }
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
                bossAnimationPresenter,
                bossStats,
                attackSet,
                presentationConfig,
                commanderMove.InitialPosition,
                HandleCommanderStunChanged,
                hudPresenter == null ? null : hudPresenter.ShowAttackWarning,
                bossFacingSmoother,
                delayedDamageQueue.Schedule,
                delayedDamageQueue.Clear);
            stateMachine.SetPhase(phaseRuntime.CurrentPhase);
        }

        private void HandleCommanderStunChanged(bool isStunned)
        {
            isCommanderStunned = isStunned;
            commanderMove?.SetInputEnabled(IsRunning && !isStunned);
            Debug.Log(
                $"군단장 기절 : {(isStunned ? "시작" : "해제")}",
                this);
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
                    battleFlow.IsStartDelayActive ||
                    timeoutWipePattern.IsActive ||
                    commanderMove == null ||
                    !commanderMove.IsInputEnabled ||
                    isCommanderStunned,
                () => bossStats.CommanderDamageMultiplier *
                    (breakRuntime.IsBroken ? bossConfig.BreakDamageMultiplier : 1f));
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

            if (isDebugPhaseJump)
            {
                PublishHudState();
                return;
            }

            var phaseChanged = TryAdvanceBossPhase();
            var finalChargeScheduled = TryStartFinalCharge();

            if (phaseChanged ||
                phaseRuntime.IsTransitionActive ||
                phaseRuntime.RequestedPhase > phaseRuntime.CurrentPhase ||
                finalChargeScheduled ||
                isFinalChargePending)
            {
                PublishHudState();
                return;
            }

            if (!breakRuntime.IsBroken && bossActor.IsAlive)
            {
                if (breakRuntime.ApplyHit(
                        bossConfig.MaxBreakGauge,
                        bossConfig.BreakGaugePerHit,
                        bossConfig.BreakGaugeAttackPowerMultiplier,
                        GetHealthThresholdBreakGaugeMultiplier()))
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
            if (!phaseRuntime.RequestForHealth(healthRatio))
            {
                return false;
            }

            TryStartNextPhaseTransition();
            return true;
        }

        // 예약된 다음 페이즈가 있으면 현재 페이즈에서 정확히 한 단계만 전환한다.
        private bool TryStartNextPhaseTransition()
        {
            var isBlocked = finalChargePattern.IsActive ||
                bossActor == null ||
                !bossActor.IsAlive;
            if (!phaseRuntime.TryBeginNextTransition(isBlocked, out var phaseData))
            {
                return false;
            }

            breakRuntime.Reset();
            pendingPhaseBossPresentation = phaseData;
            stateMachine?.BeginPhaseTransition(
                phaseRuntime.CurrentPhase,
                phaseRuntime.TransitionRemainingTime);
            if (!TryPlayPhaseVideo(phaseData.Phase))
                PlayPhaseTransitionSound(phaseData);
            Debug.Log($"보스 {((int)phaseRuntime.CurrentPhase)} 페이즈 진입", this);
            return true;
        }

        private bool TryPlayPhaseVideo(FallenCommanderBossPhase phase)
        {
            var resource = phase == FallenCommanderBossPhase.Phase2 ? "FallenCommanderVideo/Lucy_Phase1To2"
                : phase == FallenCommanderBossPhase.Phase3 ? "FallenCommanderVideo/Lucy_Phase2To3" : null;
            if (resource == null) return false;
            var clip = Resources.Load<UnityEngine.Video.VideoClip>(resource);
            var prefab = Resources.Load<GameObject>("GachaVideo/PF_GachaSummonVideo");
            if (clip == null || prefab == null) return false;
            if (phaseVideoObject == null)
            {
                phaseVideoObject = Instantiate(prefab);
                phaseVideo = phaseVideoObject.GetComponent<ProjectMT.Shared.UI.ISkippableVideoOverlay>();
            }
            if (phaseVideo == null) return false;
            PlayPhaseVideo(clip, Resources.Load<AudioClip>(resource));
            return true;
        }

        private async void PlayPhaseVideo(UnityEngine.Video.VideoClip clip, AudioClip audioClip)
        {
            var version = ++phaseVideoVersion;
            phaseVideoPreviousTimeScale = Time.timeScale;
            isPhaseVideoActive = true;
            Time.timeScale = 0f;
            try
            {
                var completed = await phaseVideo.PlayAsync(clip, audioClip);
                if (this == null || version != phaseVideoVersion || !completed || !IsRunning) return;
                TryApplyPendingPhaseBossPresentation(force: true);
                if (phaseRuntime.TickTransition(phaseRuntime.TransitionRemainingTime, out var signatureAttack))
                    stateMachine?.CompletePhaseTransition(signatureAttack);
                ApplyCurrentPhaseBossScale();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                if (version == phaseVideoVersion)
                {
                    ReleasePhaseVideoPause();
                    if (this != null && IsRunning) PublishHudState();
                }
            }
        }

        private void ReleasePhaseVideoPause()
        {
            if (!isPhaseVideoActive) return;
            isPhaseVideoActive = false;
            Time.timeScale = phaseVideoPreviousTimeScale;
        }

        private void CancelPhaseVideo()
        {
            phaseVideoVersion++;
            if (phaseVideoObject != null) phaseVideo?.Cancel();
            ReleasePhaseVideoPause();
        }

        private void OnDisable() => CancelPhaseVideo();
        private void OnDestroy()
        {
            CancelPhaseVideo();
            if (phaseVideoObject != null) Destroy(phaseVideoObject);
        }
        // 전투 준비시간 안에 1페이즈 문구를 표시하고 사운드는 전투 시작 시점에 예약한다.
        private void PresentInitialPhase()
        {
            var phaseData = phaseRuntime.Begin(battleFlow.StartDelayRemaining);

            if (!battleFlow.IsStartDelayActive)
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

        private void ApplyPhaseBossPresentation(FallenCommanderPhaseData phaseData)
        {
            if (phaseData == null || bossActor == null)
            {
                return;
            }

            var targetPrefab = ResolveBossPrefab(phaseData.Phase);
            if (targetPrefab != null && targetPrefab != activeBossPrefab)
            {
                ReplaceBossActor(targetPrefab);
            }

            if (bossActor != null)
            {
                bossActor.transform.localScale =
                    bossBaseLocalScale * phaseData.BossScaleMultiplier;
            }
        }

        private void TryApplyPendingPhaseBossPresentation(bool force = false)
        {
            if (pendingPhaseBossPresentation == null ||
                (!force && hudPresenter != null &&
                 !hudPresenter.IsPhaseTransitionScreenCovered))
            {
                return;
            }

            var phaseData = pendingPhaseBossPresentation;
            pendingPhaseBossPresentation = null;
            var previousStateMachine = stateMachine;
            ApplyPhaseBossPresentation(phaseData);
            if (stateMachine != previousStateMachine)
            {
                stateMachine?.BeginPhaseTransition(
                    phaseRuntime.CurrentPhase,
                    phaseRuntime.TransitionRemainingTime);
            }
        }

        private void ApplyCurrentPhaseBossScale()
        {
            if (phaseRuntime.IsTransitionActive || bossActor == null)
            {
                return;
            }

            var phaseData = phaseRuntime.CurrentData;
            if (phaseData != null)
            {
                bossActor.transform.localScale =
                    bossBaseLocalScale * phaseData.BossScaleMultiplier;
            }
        }

        private void ApplyBossHitFeedbackScale()
        {
            if (phaseRuntime.IsTransitionActive || bossActor == null)
            {
                return;
            }

            var phaseData = phaseRuntime.CurrentData;
            if (phaseData == null)
            {
                return;
            }

            var expectedScale = bossBaseLocalScale * phaseData.BossScaleMultiplier;
            var currentScale = bossActor.transform.localScale;
            if (IsSameScale(currentScale, expectedScale))
            {
                return;
            }

            var hitPulseScale = new Vector3(
                ResolveScaleRatio(currentScale.x, bossBaseLocalScale.x),
                ResolveScaleRatio(currentScale.y, bossBaseLocalScale.y),
                ResolveScaleRatio(currentScale.z, bossBaseLocalScale.z));
            bossActor.transform.localScale = Vector3.Scale(expectedScale, hitPulseScale);
        }

        private static bool IsSameScale(Vector3 left, Vector3 right)
        {
            return Mathf.Approximately(left.x, right.x) &&
                   Mathf.Approximately(left.y, right.y) &&
                   Mathf.Approximately(left.z, right.z);
        }

        private static float ResolveScaleRatio(float value, float baseValue)
        {
            return Mathf.Abs(baseValue) <= 0.0001f ? 1f : value / baseValue;
        }

        private GameObject ResolveBossPrefab(FallenCommanderBossPhase phase)
        {
            var resolvedPrefab = bossPrefab;
            for (var phaseNumber = (int)FallenCommanderBossPhase.Phase1;
                 phaseNumber <= (int)phase;
                 phaseNumber++)
            {
                var phaseData = bossConfig.PhaseConfig.GetPhase(
                    (FallenCommanderBossPhase)phaseNumber);
                if (phaseData?.BossPrefabOverride != null)
                {
                    resolvedPrefab = phaseData.BossPrefabOverride;
                }
            }

            return resolvedPrefab;
        }

        private void ReplaceBossActor(GameObject replacementPrefab)
        {
            var previousBoss = bossActor;
            if (previousBoss == null || replacementPrefab == null)
            {
                return;
            }

            var healthRatio = previousBoss.Health.MaxHealth <= 0f
                ? 1f
                : previousBoss.Health.CurrentHealth / previousBoss.Health.MaxHealth;
            var spawnPosition = previousBoss.transform.position;
            var spawnRotation = previousBoss.transform.rotation;
            var shouldRecreateStateMachine = stateMachine != null;

            stateMachine?.Shutdown();
            stateMachine = null;
            bossAnimationPresenter?.Stop();
            bossAnimationPresenter = null;
            bossFacingSmoother?.Shutdown();
            bossFacingSmoother = null;

            previousBoss.Health.Damaged -= HandleBossDamaged;
            previousBoss.Died -= HandleBossDied;
            previousBoss.Shutdown();
            ReturnBossToPool(previousBoss);

            bossActor = CreateBossActor(
                replacementPrefab,
                spawnPosition,
                spawnRotation);
            if (bossActor == null)
            {
                throw new InvalidOperationException(
                    "Fallen Commander phase boss spawn failed.");
            }

            activeBossPrefab = replacementPrefab;
            RestoreBossHealthRatio(healthRatio);
            ConfigureBossActor(logFullHealth: false);

            if (shouldRecreateStateMachine)
            {
                InitializeStateMachine();
            }
        }

        private void RestoreBossHealthRatio(float healthRatio)
        {
            RestoreBossHealthRatio(bossActor, healthRatio);
        }

        private static void RestoreBossHealthRatio(
            UnitActor actor,
            float healthRatio)
        {
            if (actor == null || actor.Health == null)
            {
                return;
            }

            var clampedRatio = Mathf.Clamp01(healthRatio);
            if (clampedRatio >= 0.9999f)
            {
                return;
            }

            var maxHealth = actor.Health.MaxHealth;
            var preservedHealth = Mathf.Max(1f, maxHealth * clampedRatio);
            actor.Health.ApplyDamage(new DamageRequest(
                actor,
                maxHealth - preservedHealth,
                actor.transform.position));
        }

        private static void ReturnBossToPool(UnitActor boss)
        {
            var pooledInstance = boss.GetComponent<PooledInstance>();
            if (pooledInstance?.Owner != null)
            {
                pooledInstance.Owner.Return(boss.gameObject);
                return;
            }

            Destroy(boss.gameObject);
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
            if (!finalChargePattern.Complete(out _))
            {
                return;
            }

            FallenCommanderAttackEffectPlayer.PlayResolve(
                finalChargeConfig.Effects,
                effectPosition,
                effectDirection,
                bossActor == null ? null : bossActor.transform.parent,
                bossActor == null ? null : bossActor.transform,
                commanderRoot == null ? null : commanderRoot.transform);
            bossAnimationPresenter?.Play(
                finalChargeConfig.CastMotion,
                stopAfterMotion: true,
                durationOverride: finalChargeConfig.CastMotionDuration,
                playbackSpeed: finalChargeConfig.CastMotionSpeed,
                normalizedStart: finalChargeConfig.CastMotionStart,
                normalizedEnd: finalChargeConfig.CastMotionEnd);

            if (!IsRunning || commanderHealth == null || !commanderHealth.IsAlive)
            {
                return;
            }

            InitializeStateMachine();
            ResumeBossTrackingAfterFinalCharge();
            var radius = finalChargeConfig.Radius;
            var effects = finalChargeConfig.Effects;
            var stunDuration = finalChargeConfig.StunDuration;
            var effectParent = bossActor == null ? null : bossActor.transform.parent;
            var effectAnchor = bossActor == null ? null : bossActor.transform;
            delayedDamageQueue.Schedule(finalChargeConfig.DamageDelay, () =>
            {
                if (!IsRunning ||
                    commanderRoot == null ||
                    commanderHealth == null ||
                    !commanderHealth.IsAlive ||
                    bossActor == null)
                {
                    return;
                }

                var offset = commanderRoot.transform.position - effectPosition;
                offset.y = 0f;
                if (offset.sqrMagnitude > radius * radius)
                {
                    return;
                }

                FallenCommanderAttackEffectPlayer.PlayHit(
                    effects,
                    commanderRoot.transform.position,
                    effectDirection,
                    effectParent,
                    effectAnchor,
                    commanderRoot.transform);
                commanderHealth.ApplyDamage(new DamageRequest(
                    bossActor,
                    1f,
                    commanderRoot.transform.position));
                if (stunDuration > 0f)
                {
                    stateMachine?.LockCommanderStun(stunDuration);
                }

                Debug.Log("충전 광역 공격 적중: 하트 1개 피해", this);
            });
            PublishHudState();
        }

        // 일반 전투 흐름을 중지하고 제한시간 전멸기 모듈을 시작한다.
        private void BeginTimeoutWipeSequence()
        {
            if (!IsRunning ||
                battleFlow.IsFinishing ||
                timeoutWipePattern.IsActive ||
                commanderRoot == null ||
                commanderHealth == null)
            {
                return;
            }

            finalChargePattern.Cancel();
            phaseRuntime.CancelTransition();
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
            if (healthRatio > finalChargeConfig.HealthRatio)
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
                phaseRuntime.IsTransitionActive ||
                phaseRuntime.IsWaitingForSignature ||
                phaseRuntime.RequestedPhase > phaseRuntime.CurrentPhase)
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
                battleFlow.IsFinishing ||
                bossActor == null ||
                !bossActor.IsAlive)
            {
                return false;
            }

            if (!finalChargePattern.Begin(
                    bossActor.transform,
                    commanderRoot.transform,
                    finalChargeConfig.TelegraphPrefab,
                    finalChargeConfig.Duration,
                    finalChargeConfig.TelegraphHoldDuration,
                    finalChargeConfig.Radius))
            {
                return false;
            }

            hasTriggeredFinalCharge = true;
            isFinalChargePending = false;
            breakRuntime.Reset();
            phaseRuntime.CancelTransition();
            PauseBossTrackingForFinalCharge();
            stateMachine?.Shutdown();
            bossAnimationPresenter?.PlayPreCast(
                finalChargeConfig.PreCastMotion,
                playbackSpeed: finalChargeConfig.PreCastMotionSpeed,
                normalizedStart: finalChargeConfig.PreCastMotionStart,
                normalizedEnd: finalChargeConfig.PreCastMotionEnd);
            FallenCommanderAttackEffectPlayer.PlayStart(
                finalChargeConfig.Effects,
                bossActor.transform.TransformPoint(
                    finalChargeConfig.StartEffectOffset),
                bossActor.transform.forward,
                bossActor.transform,
                bossActor.transform,
                commanderRoot == null ? null : commanderRoot.transform);
            Debug.Log(
                $"충전 광역 공격 준비 시작: {finalChargeConfig.Duration:0.0}초",
                this);
            return true;
        }

        private void PauseBossTrackingForFinalCharge()
        {
            if (bossActor == null || bossActor.Health == null)
            {
                return;
            }

            bossFacingSmoother?.SetTrackingEnabled(false);
            bossActor.ForceTarget(
                bossActor.Health,
                float.PositiveInfinity);
        }

        private void ResumeBossTrackingAfterFinalCharge()
        {
            if (bossActor == null || commanderHealth == null)
            {
                return;
            }

            bossActor.ForceTarget(
                commanderHealth,
                float.PositiveInfinity);
            bossFacingSmoother?.SetTrackingEnabled(true);
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

            if (phaseRuntime.CurrentPhase == FallenCommanderBossPhase.Phase3)
            {
                return bossConfig.BreakGaugePhaseThreeMultiplier;
            }

            if (phaseRuntime.CurrentPhase == FallenCommanderBossPhase.Phase2)
            {
                return bossConfig.BreakGaugePhaseTwoMultiplier;
            }

            return 1f;
        }

        private void StartBreak()
        {
            if (!IsRunning || breakRuntime.IsBroken)
            {
                return;
            }

            breakRuntime.Enter(bossConfig.BreakDuration);
            stateMachine?.EnterBroken();
            PublishHudState();
        }

        private void EndBreak()
        {
            if (!breakRuntime.IsBroken)
            {
                return;
            }

            breakRuntime.Exit();
            stateMachine?.ExitBroken();
            PublishHudState();
        }

        // 브레이크와 페이즈 예약 상태를 최초 전투 상태로 되돌린다.
        private void ResetBreakState()
        {
            breakRuntime.Reset();
            phaseRuntime.Reset();
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
            hudPresenter.SetStage(int.TryParse(context.RunInfo.StageId, out var hudStage) ? hudStage : 1);
#if UNITY_EDITOR
            debugController = new FallenCommanderDebugController(
                () => IsRunning,
                () => battleFlow.IsStartDelayActive,
                () => battleFlow.IsFinishing,
                () => timeoutWipePattern.IsActive,
                () => finalChargePattern.IsActive,
                () => bossActor,
                () => stateMachine,
                PrepareDebugStandardAttack,
                battleFlow,
                BeginTimeoutWipeSequence,
                PublishHudState,
                ExecuteDebugPhaseJump,
                () => { StartFinalCharge(); });
#else
            debugController = null;
#endif
            hudPresenter.Bind(
                this,
                debugController,
                ShouldShowDebugControls());
        }

        private bool ShouldShowDebugControls()
        {
#if UNITY_EDITOR
            return context.RunInfo.RunMode == ContentRunMode.SeedTest;
#else
            return false;
#endif
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
            debugController = null;
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
                breakRuntime.IsBroken,
                score,
                battleFlow.RemainingTime,
                0,
                0,
                0f,
                breakRuntime.RemainingTime,
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
                    : finalChargeConfig.Duration,
                timeoutWipePattern.IsActive,
                !battleFlow.IsStartDelayActive &&
                !timeoutWipePattern.IsActive &&
                    battleFlow.RemainingTime > 0f &&
                    battleFlow.RemainingTime <= timeoutWarningStartSeconds,
                timeoutWarningStartSeconds,
                !isPhaseVideoActive && (phaseRuntime.IsTransitionActive || phaseRuntime.IntroNoticeRemainingTime > 0f),
                (int)phaseRuntime.CurrentPhase,
                phaseRuntime.TransitionMessage,
                TimeoutWipe?.WarningMessage,
                TimeoutWipe?.WarningPulseInterval ?? 0.45f,
                finalChargeConfig.WarningMessage,
                phaseRuntime.CurrentData?.TransitionFadeColor ?? Color.black,
                phaseRuntime.CurrentData?.TransitionFadeAlpha ?? 1f,
                phaseRuntime.CurrentData?.TransitionFadeDuration ?? 0.15f));
        }

        private void HandleCommanderDamaged(DamageReport report)
        {
            if (commanderRoot != null)
            {
                combatWorld?.PlayMonsterSfx(
                    commanderDamageVoice,
                    commanderRoot.transform.position);
            }

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
            phaseRuntime.CancelTransition();
            PublishHudState();

            BeginDeathSequence(
                ContentOutcome.Complete,
                false);
        }

        // DEV 버튼에서 지정한 페이즈 체력으로 보스를 초기화하고 정상 전환 절차를 실행한다.
        private void ExecuteDebugPhaseJump(int phaseNumber)
        {
            if (isPhaseVideoActive) return;
            if (!IsRunning ||
                battleFlow.IsStartDelayActive ||
                timeoutWipePattern.IsActive ||
                battleFlow.IsFinishing ||
                bossActor == null ||
                !bossActor.IsAlive)
            {
                return;
            }

            var targetPhase = (FallenCommanderBossPhase)Mathf.Clamp(
                phaseNumber,
                (int)FallenCommanderBossPhase.Phase1,
                (int)FallenCommanderBossPhase.Phase3);
            PrepareDebugPhaseJump();
            finalChargePattern.Cancel();
            hasTriggeredFinalCharge = false;
            isFinalChargePending = false;
            ResetBreakState();
            bossActor.Health.Heal(bossActor.Health.MaxHealth);

            var targetPhaseData = bossConfig.PhaseConfig.GetPhase(targetPhase);
            if (targetPhaseData == null)
            {
                PublishHudState();
                return;
            }

            var targetRatio = targetPhaseData.HealthRatio;
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

            if (!phaseRuntime.BeginForcedTransition(targetPhase, out targetPhaseData))
            {
                PublishHudState();
                return;
            }

            pendingPhaseBossPresentation = targetPhaseData;
            stateMachine?.BeginPhaseTransition(
                targetPhase,
                phaseRuntime.TransitionRemainingTime);
            if (!TryPlayPhaseVideo(targetPhaseData.Phase))
                PlayPhaseTransitionSound(targetPhaseData);
            PublishHudState();
        }

        private void PrepareDebugStandardAttack()
        {
            phaseRuntime.CancelTransition();
            delayedDamageQueue.Clear();
        }

        private void PrepareDebugPhaseJump()
        {
            pendingPhaseBossPresentation = null;
            PrepareDebugStandardAttack();
            stateMachine?.PrepareDebugPhaseJump();
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
            if (!battleFlow.TryBeginFinishing())
            {
                return;
            }

            ShutdownCommanderSkills();
            stateMachine?.Shutdown();
            commanderMove?.SetInputEnabled(false);
            ReleaseExitConfirmation();
            ReleaseHud();

            ExitContent(outcome);
        }

        private void BeginDeathSequence(
            ContentOutcome outcome,
            bool isCommanderDeath,
            float resultDelayOverride = -1f)
        {
            if (!battleFlow.TryBeginFinishing())
            {
                return;
            }
            ShutdownCommanderSkills();
            stateMachine?.Shutdown();
            commanderMove?.SetInputEnabled(false);
            ReleaseExitConfirmation();

            if (isCommanderDeath)
            {
                commanderAnimationPresenter = EnsureCommanderAnimationPresenter();
                commanderAnimationPresenter.Configure(commanderRoot.transform);
                commanderAnimationPresenter.Play(
                    presentationConfig.CommanderDeathMotion,
                    stopAfterMotion: true,
                    durationOverride: presentationConfig.CommanderDeathMotionDuration);
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
                        presentationConfig.DeathMotion,
                        presentationConfig.DeathMotionDuration);
                }
                else
                {
                    bossAnimationPresenter?.Configure(bossActor.transform);
                    bossAnimationPresenter?.Play(
                        presentationConfig.DeathMotion,
                        stopAfterMotion: true,
                        durationOverride: presentationConfig.DeathMotionDuration);
                }
            }

            var resultDelay = resultDelayOverride >= 0f
                ? resultDelayOverride
                : presentationConfig.DeathResultDelay;
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
            commanderAnimationPresenter?.Stop();
            commanderAnimationPresenter = null;
            deathRoutine = null;
            ReleaseHud();
            ExitContent(outcome);
        }

        private void HandleCommanderEvadeStarted(bool isForward)
        {
            if (!IsRunning || commanderRoot == null)
            {
                return;
            }

            var motion = isForward
                ? presentationConfig.CommanderEvadeForwardMotion
                : presentationConfig.CommanderEvadeBackwardMotion;
            commanderAnimationPresenter = EnsureCommanderAnimationPresenter();
            commanderAnimationPresenter.Configure(commanderRoot.transform);
            commanderAnimationPresenter.Play(motion, stopAfterMotion: true);
        }

        private FallenCommanderBossAnimationPresenter EnsureCommanderAnimationPresenter()
        {
            var presenter = commanderAnimationPresenter != null
                ? commanderAnimationPresenter
                : commanderRoot.GetComponent<FallenCommanderBossAnimationPresenter>();
            return presenter != null
                ? presenter
                : commanderRoot.AddComponent<FallenCommanderBossAnimationPresenter>();
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
                battleFlow.RemainingTime,
                outcome == ContentOutcome.Complete);

            if (outcome == ContentOutcome.Complete)
            {
                context?.Exit.Complete(result);
                return;
            }

            context?.Exit.Fail(result);
        }

        private void ConfigureExitConfirmation()
        {
            exitButton?.onClick.RemoveListener(OpenExitConfirmation);
            exitButton?.onClick.AddListener(OpenExitConfirmation);
            if (exitConfirmationDialog == null)
            {
                return;
            }

            exitConfirmationDialog.GiveUpRequested -= Cancel;
            exitConfirmationDialog.GiveUpRequested += Cancel;
            exitConfirmationDialog.RetryRequested -= RetryBattle;
            exitConfirmationDialog.RetryRequested += RetryBattle;
            exitConfirmationDialog.Initialize();
        }

        private void ReleaseExitConfirmation()
        {
            exitButton?.onClick.RemoveListener(OpenExitConfirmation);
            if (exitConfirmationDialog == null)
            {
                return;
            }

            exitConfirmationDialog.GiveUpRequested -= Cancel;
            exitConfirmationDialog.RetryRequested -= RetryBattle;
            exitConfirmationDialog.Release();
        }

        private void OpenExitConfirmation()
        {
            if (context == null || battleFlow.IsFinishing)
            {
                return;
            }

            exitConfirmationDialog?.Open();
        }

        private void RetryBattle()
        {
            if (context == null || battleFlow.IsFinishing)
            {
                return;
            }

            var restartContext = context;
            Shutdown();
            Initialize(restartContext);
        }

        private void Cancel()
        {
            if (context == null || battleFlow.IsFinishing)
            {
                return;
            }

            if (!battleFlow.TryBeginFinishing())
            {
                return;
            }
            timeoutWipePattern.Cancel();
            ShutdownCommanderSkills();
            stateMachine?.Shutdown();
            commanderMove?.SetInputEnabled(false);
            ReleaseExitConfirmation();
            ReleaseHud();
            context.Exit.Cancel();
        }
    }
}
