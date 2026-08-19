using System;
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
    public sealed class FallenCommanderController : MonoBehaviour, IContentController, IBossDungeonHudSource
    {
        [Header("Battle")]
        [SerializeField] private CombatWorld combatWorld;
        [SerializeField] private GameObject bossPrefab;
        [SerializeField] private Transform bossSpawnPoint;

        [Header("Commander")]
        [SerializeField] private GameObject commanderRoot;
        [SerializeField] private CommanderMoveController commanderMove;
        [SerializeField, Min(1)] private int commanderMaxHearts = 5;

        [Header("Boss")]
        [SerializeField, Min(0.1f)] private float bossAttackInterval = 2f;
        [SerializeField, Min(0.1f)] private float bossAttackRange = 8f;
        [SerializeField, Min(1f)] private float bossTurnSpeed = 90f;

        // 바닥에 보여줄 공격 범위 프리팹
        [Header("Mark Strike")]
        [SerializeField] private GameObject markStrikeTelegraphPrefab;
        [SerializeField, Min(0.1f)] private float markStrikeCastTime = 3f;
        [SerializeField, Min(0.1f)] private float markStrikeRadius = 1.5f;
        [SerializeField, Min(0.1f)] private float markStrikeStunDuration = 3f;

        [Header("Break System")]
        [SerializeField, Min(1f)] private float maxBreakGauge = 100f;
        [SerializeField, Min(0.1f)] private float breakGaugePerHit = 10f;
        [SerializeField, Range(0.01f, 1f)] private float breakGaugeAttackPowerMultiplier = 0.25f;
        [SerializeField, Range(0.01f, 1f)] private float breakGaugePhaseTwoHealthRatio = 0.7f;
        [SerializeField, Range(0.01f, 1f)] private float breakGaugePhaseThreeHealthRatio = 0.4f;
        [SerializeField, Range(0.01f, 1f)] private float breakGaugePhaseTwoMultiplier = 0.75f;
        [SerializeField, Range(0.01f, 1f)] private float breakGaugePhaseThreeMultiplier = 0.5f;
        [SerializeField, Min(0.1f)] private float breakDuration = 5f;
        [SerializeField, Min(1f)] private float breakDamageMultiplier = 2f;

        private ContentContext context;
        private UnitActor bossActor;
        private HealthComponent commanderHealth;
        private FallenCommanderBossStateMachine stateMachine;
        private FallenCommanderBossFacingSmoother bossFacingSmoother;
        private ICommanderSkillContentBridge commanderSkillBridge;
        private GiantSpellbookHudPresenter hudPresenter;
        private float currentBreakGauge;
        private float breakRemainingTime;
        private bool isBroken;

        private const float BreakGaugeDamageScale = 5f;

        private float RemainingBreakGauge =>
            Mathf.Max(0f, maxBreakGauge - currentBreakGauge);

        public bool IsRunning { get; private set; }
        public event Action<GiantSpellbookHudState> HudStateChanged;

        public void Initialize(ContentContext contentContext)
        {
            Shutdown();

            context = contentContext ??
                throw new ArgumentNullException(nameof(contentContext));

            ValidateReferences();

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

        public void Shutdown()
        {
            IsRunning = false;

            ShutdownCommanderSkills();

            stateMachine?.Shutdown();
            stateMachine = null;

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
                markStrikeTelegraphPrefab == null)
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
                attackRange = bossAttackRange,
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
                bossTurnSpeed);
        }

        private void InitializeStateMachine()
        {
            stateMachine = new FallenCommanderBossStateMachine();

            stateMachine.Configure(
                combatWorld,
                bossActor,
                commanderRoot.transform,
                commanderHealth,
                bossAttackInterval,
                markStrikeTelegraphPrefab,
                markStrikeCastTime,
                markStrikeRadius,
                markStrikeStunDuration,
                HandleCommanderStunChanged,
                bossFacingSmoother);
        }

        private void HandleCommanderStunChanged(bool isStunned)
        {
            if (isStunned)
            {
                commanderMove?.SetInputEnabled(false);
                return;
            }

            if (IsRunning)
            {
                commanderMove?.SetInputEnabled(true);
            }
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
                () => isBroken ? breakDamageMultiplier : 1f);
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

            if (!isBroken && bossActor.IsAlive)
            {
                var breakGaugeDamage = breakGaugePerHit *
                    breakGaugeAttackPowerMultiplier *
                    BreakGaugeDamageScale *
                    GetHealthPhaseBreakGaugeMultiplier();

                currentBreakGauge = Mathf.Min(
                    maxBreakGauge,
                    currentBreakGauge + breakGaugeDamage);

                if (currentBreakGauge >= maxBreakGauge)
                {
                    StartBreak();
                }
            }

            PublishHudState();
        }

        private float GetHealthPhaseBreakGaugeMultiplier()
        {
            if (bossActor == null || bossActor.Health.MaxHealth <= 0f)
            {
                return 1f;
            }

            var healthRatio =
                bossActor.Health.CurrentHealth / bossActor.Health.MaxHealth;

            if (healthRatio <= breakGaugePhaseThreeHealthRatio)
            {
                return breakGaugePhaseThreeMultiplier;
            }

            if (healthRatio <= breakGaugePhaseTwoHealthRatio)
            {
                return breakGaugePhaseTwoMultiplier;
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
            breakRemainingTime = breakDuration;
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

        private void ResetBreakState()
        {
            currentBreakGauge = 0f;
            breakRemainingTime = 0f;
            isBroken = false;
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

            hudPresenter.Bind(this);
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
                maxBreakGauge,
                isBroken,
                0,
                0f,
                0,
                0,
                0f,
                breakRemainingTime,
                breakDuration));
        }

        private void HandleCommanderDamaged(DamageReport report)
        {
            Debug.Log(
                $"Commander Hearts: {report.RemainingHealth} / {commanderHealth.MaxHealth}",
                this);
        }

        private void HandleCommanderDied(DamageReport report)
        {
            Finish(ContentOutcome.Fail);
        }

        private void HandleBossDied(UnitActor actor)
        {
            if (actor != bossActor)
            {
                return;
            }

            Finish(ContentOutcome.Complete);
        }

        private void Finish(ContentOutcome outcome)
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;

            ShutdownCommanderSkills();
            stateMachine?.Shutdown();
            commanderMove?.SetInputEnabled(false);
            ReleaseHud();

            var result = new FallenCommanderResult();

            if (outcome == ContentOutcome.Complete)
            {
                context?.Exit.Complete(result);
                return;
            }

            context?.Exit.Fail(result);
        }
    }
}
