using System;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Input;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.GiantSpellbook
{
    [DisallowMultipleComponent]
    public sealed class GiantSpellbookController : MonoBehaviour, IContentController, IBossDungeonHudSource // 군단장 대 보스 1:1 전투 총괄
    {
        [SerializeField] private CombatWorld combatWorld; // UnitActor 생성·타깃 탐색·공격 Tick·정리를 맡는 공용 전투 공간
        [SerializeField] private GameObject exampleEnemyPrefab; // UnitActor가 붙은 팀원 참고용 임시 적 Prefab
        [SerializeField] private Transform exampleEnemySpawn; // 예시 적 한 기를 놓을 위치, 실제 구현에서는 웨이브 SpawnPoint로 교체 가능
        [SerializeField] private GameObject commanderRoot; // 모델이 아니라 실제 위치와 회전을 움직이는 군단장 최상위 오브젝트
        [SerializeField] private CommanderMoveController commanderMove; // 터치패드와 WASD를 하나의 이동 벡터로 합치는 공용 이동 컴포넌트
        [SerializeField] private Button exitButton; // Cancel 결과를 보내 보상·저장 없이 MainBattle로 복귀하는 버튼
        [Header("Break System")]
        [SerializeField, Min(1f)]
        private float maxBreakGauge = 100f; // [임시값] 브레이크에 필요한 최대 게이지

        [SerializeField, Min(0.1f)]
        private float breakGaugePerHit = 10f; // [임시값] 보스가 한 번 맞을 때 증가하는 게이지

        [SerializeField, Range(0.01f, 1f)]
        private float breakGaugeAttackPowerMultiplier = 0.25f;

        [SerializeField, Range(0.01f, 1f)]
        private float breakGaugePhaseTwoHealthRatio = 0.7f;

        [SerializeField, Range(0.01f, 1f)]
        private float breakGaugePhaseThreeHealthRatio = 0.4f;

        [SerializeField, Range(0.01f, 1f)]
        private float breakGaugePhaseTwoMultiplier = 0.75f;

        [SerializeField, Range(0.01f, 1f)]
        private float breakGaugePhaseThreeMultiplier = 0.5f;

        [SerializeField, Min(0.1f)]
        private float breakDuration = 5f; // [임시값] 브레이크 유지 시간

        [SerializeField, Min(1f)]
        private float breakDamageMultiplier = 1.5f; // [임시값] 브레이크 중 군단장 스킬 피해 배율

        private ContentContext context; // 시작 정보와 Complete/Fail/Cancel 출구를 함께 전달하는 한 판의 공통 봉투
        private GiantSpellbookStartData startData; // 군단장 단독 보스전 시작 표식
        private UnitActor bossActor; // 생성된 보스를 기억하고 사망 이벤트를 관리
        private ICommanderSkillContentBridge commanderSkillBridge;
        private float difficultyMultiplier = 1f; // 선택 단계에 따른 보스 체력 배율

        private float currentBreakGauge; // 내부 판정용으로 현재까지 누적된 브레이크 공격량

        // 플레이어에게는 브레이크 내구도가 최대값에서 0까지 깎이는 형태로 표시한다.
        private float RemainingBreakGauge => Mathf.Max(0f, maxBreakGauge - currentBreakGauge);

        private bool isBroken; // 현재 보스가 브레이크 상태인지
        private float breakRemainingTime; // 브레이크 종료까지 남은 시간
        [SerializeField] private GiantSpellbookHudPresenter hudPresenter; // DEV Prefab Instance에 구성한 HUD 표시 담당

        [SerializeField, Min(1f)] private float timeLimitSeconds = 80f;
        private float remainingTime;
        private int score;
        [SerializeField, Min(0.1f)] private float comboResetTime = 2f;
        private float comboRemainingTime;
        private int comboCount;
        private int comboScore;
        public int CurrentScore => score;

        [Header("Boss Attack")]
        [SerializeField, Min(0.1f)] private float attackInterval = 2f;
        [SerializeField, Min(0.1f)] private float handSlamRange = 4f;
        [SerializeField, Min(0.1f)] private float handSlamCooldown = 6f;
        [SerializeField, Min(0.1f)] private float handSlamCastTime = 2.5f;
        [SerializeField, Min(0.1f)] private float handSlamRadius = 5.25f;
        [SerializeField, Min(0.1f)] private float handSlamStunDuration = 1.5f;
        [SerializeField, Min(0.1f)] private float markStrikeCooldown = 5f;
        [SerializeField, Min(0.1f)] private float markStrikeCastTime = 1.8f;
        [SerializeField, Min(0.1f)] private float markStrikeRadius = 2.75f;
        [SerializeField, Min(0.1f)] private float wideBurstCastTime = 4.5f;
        [SerializeField, Min(0.1f)] private float wideBurstStartRadius = 2f;
        [SerializeField, Min(0.1f)] private float wideBurstRadius = 12f;
        [SerializeField, Min(0.1f)] private float wideBurstStunDuration = 2.5f;
        [SerializeField, Min(1)] private int normalAttacksBeforeWide = 4;
        [SerializeField] private GameObject attackTelegraphPrefab;

        public bool IsRunning { get; private set; }
        public event Action<GiantSpellbookHudState> HudStateChanged;

        private GiantSpellbookStateMachine stateMachine;

        public void Initialize(ContentContext contentContext)
        {
            // Hosted Prefab은 파괴하지 않고 재사용한다. 이전 실행의 유닛·버튼 구독·입력 상태를 먼저 정리해야
            // 재입장했을 때 적이 중복 생성되거나 버튼 Listener가 여러 번 호출되지 않는다.
            Shutdown();

            // ContentFlow(MainBattle)와 DevBootstrap(DEV Scene)이 모두 같은 형식의 Context를 전달한다.
            // 여기서 구체 타입을 확인해 두면 잘못된 StartData를 연결했을 때 조용히 오작동하지 않고 즉시 알 수 있다.
            context = contentContext ?? throw new ArgumentNullException(nameof(contentContext));
            startData = contentContext.StartData as GiantSpellbookStartData;
            if (startData == null)
            {
                throw new ArgumentException("GiantSpellbookStartData is required.", nameof(contentContext));
            }

            if (combatWorld == null || exampleEnemyPrefab == null ||
                exampleEnemySpawn == null || commanderRoot == null || commanderMove == null)
            {
                throw new InvalidOperationException("Giant Spellbook skeleton references are missing.");
            }

            // 전투 공간을 비운 뒤 군단장을 최초 위치로 되돌린다. Hosted 재입장 시 직전 위치가 남지 않게 하는 순서다.
            combatWorld.Clear();
            commanderRoot.SetActive(true);
            commanderMove.ResetToInitialPosition();
            commanderMove.SetInputEnabled(true);
            exitButton?.onClick.AddListener(Cancel);
            currentBreakGauge = 0f;
            isBroken = false;
            breakRemainingTime = 0f;
            var stage = int.TryParse(context.RunInfo.StageId, out var selectedStage) &&
                        GrowthDungeonStageRules.IsValidStage(selectedStage)
                ? selectedStage
                : 1;
            difficultyMultiplier = GrowthDungeonStageRules.ResolveDifficultyMultiplier(stage);
            remainingTime = timeLimitSeconds;
            score = 0;
            comboRemainingTime = 0f;
            comboCount = 0;
            comboScore = 0;
            IsRunning = true;
            SpawnExampleEnemy();

            stateMachine = new GiantSpellbookStateMachine(); // FSM 생성

            ConfigureStateMachine();
            ConfigureCommanderSkills();
            hudPresenter?.Bind(this);
            PublishHudState();

        }

        private void Update()
        {
            if (!IsRunning)
            {
                return;
            }

            remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
            if (remainingTime <= 0f)
            {
                Timeout();
                return;
            }

            stateMachine?.Tick(Time.deltaTime, isBroken);

            if (isBroken)
            {
                breakRemainingTime -= Time.deltaTime;

                if (breakRemainingTime <= 0f)
                {
                    EndBreak();
                }
            }

            if (comboRemainingTime > 0f)
            {
                comboRemainingTime = Mathf.Max(0f, comboRemainingTime - Time.deltaTime);
                if (comboRemainingTime <= 0f)
                {
                    comboCount = 0;
                    comboScore = 0;
                }
            }

            PublishHudState();
        }

        public void DebugTimeout()
        {
            if (IsRunning)
            {
                Timeout();
            }
        }

        public void DebugBasicAttack()
        {
            DebugForceAttack(GiantSpellbookDebugAttack.BasicAttack);
        }

        public void DebugHandSlam()
        {
            DebugForceAttack(GiantSpellbookDebugAttack.HandSlam);
        }

        public void DebugMarkStrike()
        {
            DebugForceAttack(GiantSpellbookDebugAttack.MarkStrike);
        }

        public void DebugWideBurst()
        {
            DebugForceAttack(GiantSpellbookDebugAttack.WideBurst);
        }

        private void DebugForceAttack(GiantSpellbookDebugAttack attack)
        {
            if (IsRunning && !isBroken)
            {
                stateMachine?.DebugForceAttack(attack);
            }
        }

        public void Shutdown()
        {
            // Shutdown은 MainBattle 복귀, DEV Scene 종료, 재초기화 모두에서 호출될 수 있다.
            // 여러 번 호출돼도 안전하도록 Listener 제거와 공용 전투 정리를 같은 순서로 반복한다.
            IsRunning = false;
            exitButton?.onClick.RemoveListener(Cancel);
            ShutdownCommanderSkills();
            commanderMove?.SetInputEnabled(false);
            stateMachine?.Shutdown();
            stateMachine = null;

            if (bossActor != null)
            {
                bossActor.Health.Damaged -= HandleBossDamaged;
                bossActor.Died -= HandleBossDied; // 보스 사망 이벤트 구독 해제
                bossActor = null;
            }

            ResetBreakState();
            ReleaseHud();
            combatWorld?.Clear();
            if (commanderRoot != null)
            {
                commanderRoot.SetActive(false);
            }

            context = null;
            startData = null;
            difficultyMultiplier = 1f;
        }

        private void ConfigureStateMachine()
        {
            stateMachine ??= new GiantSpellbookStateMachine();
            stateMachine.Configure(
                combatWorld,
                bossActor,
                commanderRoot.transform,
                attackInterval,
                handSlamRange,
                handSlamCooldown,
                handSlamCastTime,
                handSlamRadius,
                handSlamStunDuration,
                markStrikeCooldown,
                markStrikeCastTime,
                markStrikeRadius,
                wideBurstCastTime,
                wideBurstStartRadius,
                wideBurstRadius,
                wideBurstStunDuration,
                normalAttacksBeforeWide,
                attackTelegraphPrefab,
                HandleBossHitMovementLock);
        }

        private void HandleBossHitMovementLock(bool locked)
        {
            if (locked)
            {
                commanderMove?.SetInputEnabled(false);
                return;
            }

            if (IsRunning && !isBroken)
            {
                commanderMove?.SetInputEnabled(true);
            }
        }

        private void SpawnExampleEnemy()
        {
            var stats = new UnitStatsSnapshot
            {
                maxHealth = 5000f * difficultyMultiplier, // 선택 단계가 오를수록 보스 체력 증가
                damage = 1f, // 아군 몬스터가 바로 죽지 않는 참고용 피해량
                defense = 0f,
                moveSpeed = 1.6f,
                attackRange = 1.1f,
                attackInterval = 1f,
                projectileSpeed = 0f,
                ranged = false,
                criticalDamageMultiplier = 1.5f
            };
            var request = new UnitSpawnRequest(
                "giant_spellbook_example_enemy",
                stats,
                UnitTeam.Enemy,
                canMove: true,
                canAttack: true,
                visualTint: new Color(1f, 0.65f, 0.65f));
            bossActor = combatWorld.SpawnUnit(
                exampleEnemyPrefab,
                request,
                exampleEnemySpawn.position,
                Quaternion.identity);

            if (bossActor == null)
            {
                Debug.LogError("Giant Spellbook boss spawn failed.", this);
                return;
            }

            bossActor.Health.Damaged += HandleBossDamaged;// 보스 피해 이벤트 구독
            bossActor.Died += HandleBossDied; // 보스 사망 이벤트 구독

        }
        // 보스가 피해를 받을 때마다 브레이크 게이지를 증가시킨다.
        private void HandleBossDamaged(DamageReport report)
        {
            score += Mathf.Max(0, Mathf.CeilToInt(report.AppliedDamage));
            // 콘텐츠가 종료된 뒤 들어온 피해 이벤트는 처리하지 않는다.
            if (!IsRunning)
            {
                return;
            }

            if (report.AppliedDamage > 0f)
            {
                comboCount = comboRemainingTime > 0f ? comboCount + 1 : 1;
                comboScore += comboCount;
                comboRemainingTime = comboResetTime;
            }

            // 브레이크 중에는 게이지를 추가로 올리지 않고, 감소한 체력만 HUD에 반영한다.
            if (isBroken)
            {
                PublishHudState();
                return;
            }

            // 사망 피해는 Died 이벤트의 Complete()에서 종료 처리한다.
            if (report.Killed)
            {
                return;
            }

            var appliedBreakGaugeDamage = breakGaugePerHit *
                breakGaugeAttackPowerMultiplier *
                GetHealthPhaseBreakGaugeMultiplier();
            currentBreakGauge = Mathf.Min(
                currentBreakGauge + appliedBreakGaugeDamage,
                maxBreakGauge);

            Debug.Log(
                $"Break Gauge Remaining: {RemainingBreakGauge} / {maxBreakGauge}",
                this);

            if (currentBreakGauge >= maxBreakGauge)
            {
                StartBreak();
                return;
            }

            PublishHudState();
        }

        private float GetHealthPhaseBreakGaugeMultiplier()
        {
            if (bossActor == null || bossActor.Health == null || bossActor.Health.MaxHealth <= 0f)
            {
                return 1f;
            }

            var healthRatio = bossActor.Health.CurrentHealth / bossActor.Health.MaxHealth;
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

        // 게이지가 가득 차면 브레이크를 시작하고 군단장 스킬 피해 배율을 활성화한다.
        private void StartBreak()
        {
            if (!IsRunning || isBroken)
            {
                return;
            }

            stateMachine?.EnterBroken();
            isBroken = true;
            breakRemainingTime = breakDuration;

            Debug.Log(
                $"BREAK started! Duration={breakDuration}, Damage x{breakDamageMultiplier}",
                this);
            PublishHudState();
        }

        // 브레이크 시간이 끝나면 스킬 배율과 게이지를 초기화한다.
        private void EndBreak()
        {
            if (!isBroken)
            {
                return;
            }

            isBroken = false;
            stateMachine?.ExitBroken();
            breakRemainingTime = 0f;
            currentBreakGauge = 0f;

            Debug.Log("BREAK ended. Gauge reset.", this);
            PublishHudState();
        }

        // 콘텐츠 종료 시 브레이크 실행값을 안전하게 초기화한다.
        private void ResetBreakState()
        {
            currentBreakGauge = 0f;
            isBroken = false;
            breakRemainingTime = 0f;
            comboRemainingTime = 0f;
            comboCount = 0;
            comboScore = 0;
        }

        private void HandleBossDied(UnitActor actor)
        {
            if (!IsRunning || actor != bossActor)
            {
                return;
            }

            Complete(); // 보스를 처치했으므로 성공 종료 처리
        }

        //보스 처치 후 전투를 정리하고 성공 결과를 전달
        private void Timeout()
        {
            Finish(ContentOutcome.Fail);
        }

        private void Complete()
        {
            Finish(ContentOutcome.Complete);
        }

        private void Finish(ContentOutcome outcome)
        {
            // 사망 이벤트가 중복으로 들어와도 성공 처리를 한 번만 실행
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;

            // 성공 처리 후 플레이어가 계속 움직이거나 나가기 버튼을 누르지 못하게 한다.
            ShutdownCommanderSkills();
            commanderMove?.SetInputEnabled(false);
            exitButton?.onClick.RemoveListener(Cancel);
            stateMachine?.Shutdown();
            stateMachine = null;

            // 전투 오브젝트를 정리하기 전에 보스 사망 이벤트 연결을 해제
            if (bossActor != null)
            {
                bossActor.Health.Damaged -= HandleBossDamaged;
                bossActor.Died -= HandleBossDied;
                bossActor = null;
            }

            ResetBreakState();
            ReleaseHud();
            combatWorld?.Clear();

            // 콘텐츠 내부에서는 저장하지 않고, 이번 판의 결과만 공용 출구로 전달
            var result = new GiantSpellbookResult();
            if (outcome == ContentOutcome.Complete)
            {
                context?.Exit.Complete(result);
            }
            else
            {
                context?.Exit.Fail(result);
            }
        }

        private void Cancel()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            ShutdownCommanderSkills();
            commanderMove.SetInputEnabled(false);
            stateMachine?.Shutdown();
            stateMachine = null;

            if (bossActor != null) //보스이벤트 해제
            {
                bossActor.Health.Damaged -= HandleBossDamaged;
                bossActor.Died -= HandleBossDied;
                bossActor = null;
            }
            ResetBreakState();
            ReleaseHud();
            combatWorld.Clear();
            // Cancel은 실패나 클리어가 아니므로 ResultAdapter와 보상 저장을 거치지 않는다.
            // MainBattle Hosted 실행에서는 ContentFlow가 Runtime을 닫고 기존 MainGameplayRoot를 다시 활성화한다.
            context.Exit.Cancel();
        }

        private void ReleaseHud()
        {
            if (hudPresenter == null)
            {
                return;
            }

            hudPresenter.Unbind();
            hudPresenter.SetVisible(false);
        }

        private void ConfigureCommanderSkills()
        {
            commanderSkillBridge = CommanderSkillContentBridgeLocator.Find(this);
            if (commanderSkillBridge == null)
            {
                Debug.LogError("Giant Spellbook commander skill bridge is missing.", this);
                return;
            }

            commanderSkillBridge.Configure(
                context.Progress,
                combatWorld,
                commanderRoot.transform,
                () => !IsRunning || commanderMove == null || !commanderMove.IsInputEnabled,
                () => isBroken ? breakDamageMultiplier : 1f);
        }

        private void ShutdownCommanderSkills()
        {
            commanderSkillBridge?.Shutdown();
            commanderSkillBridge = null;
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
                score,
                remainingTime,
                comboCount,
                comboScore,
                comboRemainingTime,
                breakRemainingTime,
                breakDuration));
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            CombatWorld world,
            GameObject enemy,
            Transform enemySpawn,
            GameObject commander,
            CommanderMoveController moveController,
            Button exit)
        {
            combatWorld = world;
            exampleEnemyPrefab = enemy;
            exampleEnemySpawn = enemySpawn;
            commanderRoot = commander;
            commanderMove = moveController;
            exitButton = exit;
        }
#endif
    }
}
