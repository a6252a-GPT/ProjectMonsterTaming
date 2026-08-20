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
    public static class FallenCommanderHealthThresholdRules
    {
        public static int CountNewWideBursts(
            float healthRatio,
            float phaseTwoRatio,
            float phaseThreeRatio,
            ref bool phaseTwoTriggered,
            ref bool phaseThreeTriggered)
        {
            var count = 0;
            if (healthRatio <= phaseTwoRatio && !phaseTwoTriggered)
            {
                phaseTwoTriggered = true;
                count++;
            }

            if (healthRatio <= phaseThreeRatio && !phaseThreeTriggered)
            {
                phaseThreeTriggered = true;
                count++;
            }

            return count;
        }
    }

    [DisallowMultipleComponent]
    public sealed class FallenCommanderController : MonoBehaviour, IContentController, IBossDungeonHudSource, IBossDungeonTimeoutController, IBossDungeonBossKillController, IBossDungeonAttackDebugController
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
        private bool hasTriggeredHealthThresholdWideBurst70;
        private bool hasTriggeredHealthThresholdWideBurst40;
        private int pendingThresholdWideBurstCount;
        private bool isCommanderStunned;

        private const float BreakGaugeDamageScale = 5f;

        private float RemainingBreakGauge =>
            Mathf.Max(0f, bossConfig.MaxBreakGauge - currentBreakGauge);

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

        private void BeginBattle()
        {
            if (context == null || IsRunning)
            {
                return;
            }

            remainingTime = timeLimitSeconds;
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

            IsRunning = true;
            PublishHudState();
        }

        public void Shutdown()
        {
            IsRunning = false;

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

        private void Update()
        {
            if (!IsRunning)
            {
                return;
            }

            stateMachine?.Tick(Time.deltaTime);
            TryDispatchPendingThresholdWideBurst();

            remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
            if (remainingTime <= 0f)
            {
                Finish(ContentOutcome.Fail);
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

        private void ValidateReferences()
        {
            if (combatWorld == null ||
                bossPrefab == null ||
                bossSpawnPoint == null ||
                commanderRoot == null ||
                commanderMove == null ||
                bossConfig == null ||
                bossConfig.MarkStrikeTelegraphPrefab == null)
            {
                throw new InvalidOperationException(
                    "Fallen Commander references are missing.");
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
                maxHealth = 2000f * difficultyMultiplier,
                damage = 1f,
                defense = 10f * difficultyMultiplier,
                moveSpeed = 1.6f,
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
        }

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
                bossConfig.MarkStrikeTelegraphPrefab,
                bossConfig.MarkStrike,
                bossConfig.WideBurst,
                bossConfig.LineStrike,
                bossConfig.CloseAttackDistance,
                bossConfig.LineStrikeMinimumDistance,
                bossConfig.LineStrikeAlignmentThreshold,
                HandleCommanderStunChanged,
                bossFacingSmoother);
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

        private void HandleBossDamaged(DamageReport report)
        {
            if (!IsRunning || bossActor == null)
            {
                return;
            }

            score += Mathf.CeilToInt(report.AppliedDamage);

            QueueHealthThresholdWideBursts();
            TryDispatchPendingThresholdWideBurst();

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
                    StartBreak(true);
                }
            }

            PublishHudState();
        }

        private float GetHealthThresholdBreakGaugeMultiplier()
        {
            if (bossActor == null ||
                !bossActor.IsAlive ||
                bossActor.Health.MaxHealth <= 0f)
            {
                return 1f;
            }

            var healthRatio =
                bossActor.Health.CurrentHealth / bossActor.Health.MaxHealth;

            if (healthRatio <= bossConfig.BreakGaugePhaseThreeHealthRatio)
            {
                return bossConfig.BreakGaugePhaseThreeMultiplier;
            }

            if (healthRatio <= bossConfig.BreakGaugePhaseTwoHealthRatio)
            {
                return bossConfig.BreakGaugePhaseTwoMultiplier;
            }

            return 1f;
        }

        private void QueueHealthThresholdWideBursts()
        {
            if (bossActor == null ||
                !bossActor.IsAlive ||
                bossActor.Health.MaxHealth <= 0f)
            {
                return;
            }

            var healthRatio =
                bossActor.Health.CurrentHealth / bossActor.Health.MaxHealth;

            pendingThresholdWideBurstCount +=
                FallenCommanderHealthThresholdRules.CountNewWideBursts(
                    healthRatio,
                    bossConfig.BreakGaugePhaseTwoHealthRatio,
                    bossConfig.BreakGaugePhaseThreeHealthRatio,
                    ref hasTriggeredHealthThresholdWideBurst70,
                    ref hasTriggeredHealthThresholdWideBurst40);
        }

        private void TryDispatchPendingThresholdWideBurst()
        {
            if (!IsRunning || pendingThresholdWideBurstCount <= 0)
            {
                return;
            }

            if (!isBroken)
            {
                pendingThresholdWideBurstCount--;
                StartBreak(true);
                return;
            }

            if (stateMachine == null || !stateMachine.CanForceWideBurstDuringBreak)
            {
                return;
            }

            pendingThresholdWideBurstCount--;
            breakRemainingTime = Mathf.Max(
                breakRemainingTime,
                bossConfig.WideBurst.WarningDuration);
            stateMachine.ForceWideBurstDuringBreak();
        }

        private void StartBreak(bool triggerWideBurst)
        {
            if (!IsRunning || isBroken)
            {
                return;
            }

            isBroken = true;
            breakRemainingTime = bossConfig.BreakDuration;
            stateMachine?.EnterBroken(triggerWideBurst);
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

        private void ResetBreakState()
        {
            currentBreakGauge = 0f;
            breakRemainingTime = 0f;
            isBroken = false;
            hasTriggeredHealthThresholdWideBurst70 = false;
            hasTriggeredHealthThresholdWideBurst40 = false;
            pendingThresholdWideBurstCount = 0;
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
                bossConfig.MarkStrike.StunDuration));
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

            BeginDeathSequence(
                ContentOutcome.Complete,
                false);
        }

        public void DebugTimeout()
        {
            if (IsRunning)
            {
                Finish(ContentOutcome.Fail);
            }
        }

        public void DebugKillBoss()
        {
            if (!IsRunning || bossActor == null || !bossActor.IsAlive)
            {
                return;
            }

            bossActor.Health.ApplyDamage(new DamageRequest(
                null,
                bossActor.Health.CurrentHealth,
                bossActor.transform.position));
        }

        public void DebugBasicAttack()
        {
            stateMachine?.DebugForceBasicAttack();
        }

        public void DebugLineStrike()
        {
            stateMachine?.DebugForceLineStrike();
        }

        public void DebugMarkStrike()
        {
            stateMachine?.DebugForceMarkStrike();
        }

        public void DebugWideBurst()
        {
            stateMachine?.DebugForceWideBurst();
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
                attack.PreCastMotionDuration,
                attack.CastMotion,
                attack.CastMotionDuration);
            return true;
        }

        public bool PreviewBossMotion(AnimationClip motion, float duration)
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
                durationOverride: duration);
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
            bool isCommanderDeath)
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

            deathRoutine = StartCoroutine(
                CompleteAfterDeath(outcome));
        }

        private IEnumerator CompleteAfterDeath(ContentOutcome outcome)
        {
            yield return new WaitForSeconds(bossConfig.DeathResultDelay);
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
            ShutdownCommanderSkills();
            stateMachine?.Shutdown();
            commanderMove?.SetInputEnabled(false);
            exitButton?.onClick.RemoveListener(Cancel);
            ReleaseHud();
            context.Exit.Cancel();
        }
    }
}
