using System;
using System.Collections;
using ProjectMT.Contents.Framework;
using ProjectMT.Contents.GiantSpellbook;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Input;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
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

        [Header("Boss")]
        [SerializeField] private FallenCommanderBossConfig bossConfig;

        // 바닥에 보여줄 공격 범위 프리팹
        [Header("Mark Strike")]


        [Header("Dungeon")]
        [SerializeField] private string dungeonName = "타락한 과거의 군단장";
        [SerializeField, Min(1)] private int dungeonStage = 1;
        [SerializeField, Min(1f)] private float timeLimitSeconds = 80f;

        private ContentContext context;
        private UnitActor bossActor;
        private HealthComponent commanderHealth;
        private FallenCommanderBossStateMachine stateMachine;
        private FallenCommanderBossFacingSmoother bossFacingSmoother;
        private FallenCommanderBossAnimationPresenter bossAnimationPresenter;
        private FallenCommanderBossDeathPresentation bossDeathPresentation;
        private FallenCommanderEntryPresenter entryPresenter;
        private FallenCommanderResultPresenter resultPresenter;
        private ICommanderSkillContentBridge commanderSkillBridge;
        private GiantSpellbookHudPresenter hudPresenter;
        private float currentBreakGauge;
        private float breakRemainingTime;
        private bool isBroken;
        private float remainingTime;
        private int score;
        private bool isFinishing;
        private Coroutine deathRoutine;
        private bool hasTriggeredHealthThresholdWideBurst70;
        private bool hasTriggeredHealthThresholdWideBurst40;

        private const float BreakGaugeDamageScale = 5f;

        private float RemainingBreakGauge =>
            Mathf.Max(0f, bossConfig.MaxBreakGauge - currentBreakGauge);

        public bool IsRunning { get; private set; }
        public event Action<GiantSpellbookHudState> HudStateChanged;

        public void Initialize(ContentContext contentContext)
        {
            Shutdown();

            context = contentContext ??
                throw new ArgumentNullException(nameof(contentContext));

            ValidateReferences();

            entryPresenter = FallenCommanderEntryPresenter.Create(transform);
            entryPresenter.Show(
                dungeonName,
                dungeonStage,
                BeginBattle,
                CancelEntry);
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

            commanderRoot.SetActive(true);
            commanderMove.ResetToInitialPosition();
            commanderMove.SetInputEnabled(true);

            InitializeCommanderHealth();
            SpawnBoss();
            InitializeStateMachine();
            ConfigureCommanderSkills();
            InitializeHud();

            IsRunning = true;
            PublishHudState();
        }

        private void CancelEntry()
        {
            context?.Exit.Cancel();
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

            bossDeathPresentation?.Release();
            bossDeathPresentation = null;

            if (entryPresenter != null)
            {
                entryPresenter.gameObject.SetActive(false);
                Destroy(entryPresenter.gameObject);
                entryPresenter = null;
            }

            if (resultPresenter != null)
            {
                resultPresenter.gameObject.SetActive(false);
                Destroy(resultPresenter.gameObject);
                resultPresenter = null;
            }

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
            combatWorld?.Clear();

            if (commanderRoot != null)
            {
                commanderRoot.SetActive(false);
            }

            context = null;
        }

        private void Update()
        {
            if (!IsRunning)
            {
                return;
            }

            stateMachine?.Tick(Time.deltaTime);

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
                maxHealth = 2000f,
                damage = 1f,
                defense = 10f,
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
                HandleCommanderStunChanged,
                bossFacingSmoother);
        }

        private void HandleCommanderStunChanged(bool isStunned)
        {
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
                    !commanderMove.IsInputEnabled,
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

            if (TryTriggerHealthThresholdWideBurst())
            {
                if (isBroken)
                {
                    stateMachine?.ForceWideBurstDuringBreak();
                }
                else
                {
                    StartBreak(true);
                }
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

        private bool TryTriggerHealthThresholdWideBurst()
        {
            if (bossActor == null ||
                !bossActor.IsAlive ||
                bossActor.Health.MaxHealth <= 0f)
            {
                return false;
            }

            var healthRatio =
                bossActor.Health.CurrentHealth / bossActor.Health.MaxHealth;

            if (healthRatio <= bossConfig.BreakGaugePhaseThreeHealthRatio &&
                !hasTriggeredHealthThresholdWideBurst40)
            {
                hasTriggeredHealthThresholdWideBurst40 = true;
                return true;
            }

            if (healthRatio <= bossConfig.BreakGaugePhaseTwoHealthRatio &&
                !hasTriggeredHealthThresholdWideBurst70)
            {
                hasTriggeredHealthThresholdWideBurst70 = true;
                return true;
            }

            return false;
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
        }

        private void InitializeHud()
        {
            hudPresenter =
                GetComponentInChildren<GiantSpellbookHudPresenter>(true);

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

            HudStateChanged?.Invoke(new GiantSpellbookHudState(
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
                null,
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
                bossConfig.DeathMotion,
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
            ReleaseHud();

            ShowResult(outcome);
        }

        private void BeginDeathSequence(
            ContentOutcome outcome,
            AnimationClip motion,
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

            if (isCommanderDeath)
            {
                Debug.Log(
                    "Fallen Commander death request: waiting for commander animation API.",
                    this);
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
                        motion,
                        bossConfig.DeathMotionDuration);
                }
                else
                {
                    bossAnimationPresenter?.Configure(bossActor.transform);
                    bossAnimationPresenter?.Play(
                        motion,
                        stopAfterMotion: true,
                        durationOverride: bossConfig.DeathMotionDuration);
                }
            }

            var motionDuration = isCommanderDeath
                ? motion == null
                    ? 0f
                    : Mathf.Max(0f, motion.length)
                : bossConfig.DeathMotionDuration;
            deathRoutine = StartCoroutine(
                CompleteAfterDeathMotion(outcome, motionDuration));
        }

        private IEnumerator CompleteAfterDeathMotion(
            ContentOutcome outcome,
            float motionDuration)
        {
            yield return new WaitForSeconds(motionDuration);
            bossDeathPresentation?.Release();
            bossDeathPresentation = null;

            yield return new WaitForSeconds(bossConfig.DeathResultDelay);
            deathRoutine = null;
            ReleaseHud();
            ShowResult(outcome);
        }

        private void ShowResult(ContentOutcome outcome)
        {
            resultPresenter ??= FallenCommanderResultPresenter.Create(
                transform);
            resultPresenter.Show(
                outcome,
                score,
                remainingTime,
                () => ExitContent(outcome));
        }

        private void ExitContent(ContentOutcome outcome)
        {
            var result = new FallenCommanderResult(score, remainingTime);

            if (outcome == ContentOutcome.Complete)
            {
                context?.Exit.Complete(result);
                return;
            }

            context?.Exit.Fail(result);
        }
    }
}
