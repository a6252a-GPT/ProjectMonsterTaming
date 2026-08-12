using System;
using System.Collections.Generic;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Input;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.GiantSpellbook
{
    [DisallowMultipleComponent]
    public sealed class GiantSpellbookController : MonoBehaviour, IContentController // 거대마도서 팀 작업용 빈 실행 골격
    {
        [SerializeField] private CombatWorld combatWorld; // UnitActor 생성·타깃 탐색·공격 Tick·정리를 맡는 공용 전투 공간
        [SerializeField] private GameObject followerPrefab; // 정식 Monster 외형 연결이 없을 때만 사용하는 공용 대체 Prefab
        [SerializeField] private GameObject exampleEnemyPrefab; // UnitActor가 붙은 팀원 참고용 임시 적 Prefab
        [SerializeField] private Transform exampleEnemySpawn; // 예시 적 한 기를 놓을 위치, 실제 구현에서는 웨이브 SpawnPoint로 교체 가능
        [SerializeField] private GameObject commanderRoot; // 모델이 아니라 실제 위치와 회전을 움직이는 군단장 최상위 오브젝트
        [SerializeField] private CommanderMoveController commanderMove; // 터치패드와 WASD를 하나의 이동 벡터로 합치는 공용 이동 컴포넌트
        [SerializeField] private Button exitButton; // Cancel 결과를 보내 보상·저장 없이 MainBattle로 복귀하는 버튼

        [Header("Break System")]
        [SerializeField, Min(1f)]
        private float maxBreakGauge = 100f; // [임시값] 브레이크에 필요한 최대 게이지

        [SerializeField, Min(0.1f)]
        private float breakGaugePerHit = 20f; // [임시값] 보스가 한 번 맞을 때 증가하는 게이지

        [SerializeField, Min(0.1f)]
        private float breakDuration = 5f; // [임시값] 브레이크 유지 시간

        [SerializeField, Min(1f)]
        private float breakDamageMultiplier = 1.5f; // [임시값] 브레이크 중 아군 공격력 배율

        private readonly Vector3[] followerOffsets =
        {
            new Vector3(-1.2f, 0f, -0.9f),
            new Vector3(0f, 0f, -1.2f),
            new Vector3(1.2f, 0f, -0.9f),
            new Vector3(-0.7f, 0f, -2f),
            new Vector3(0.7f, 0f, -2f)
        };

        // 생성된 아군들을 기억해 브레이크 중 공격력 배율을 적용한다.
        private readonly List<UnitActor> followerActors = new();

        private ContentContext context; // 시작 정보와 Complete/Fail/Cancel 출구를 함께 전달하는 한 판의 공통 봉투
        private GiantSpellbookStartData startData; // 입장 순간의 본부대 구성을 고정해 보관하는 읽기 전용 시작값
        private UnitActor bossActor; // 생성된 보스를 기억하고 사망 이벤트를 관리

        private float currentBreakGauge; // 내부 판정용으로 현재까지 누적된 브레이크 공격량

        // 플레이어에게는 브레이크 내구도가 최대값에서 0까지 깎이는 형태로 표시한다.
        private float RemainingBreakGauge => Mathf.Max(0f, maxBreakGauge - currentBreakGauge);

        private bool isBroken; // 현재 보스가 브레이크 상태인지
        private float breakRemainingTime; // 브레이크 종료까지 남은 시간

        public bool IsRunning { get; private set; }

        public void Initialize(ContentContext contentContext)
        {
            // Hosted Prefab은 파괴하지 않고 재사용한다. 이전 실행의 유닛·버튼 구독·입력 상태를 먼저 정리해야
            // 재입장했을 때 적이 중복 생성되거나 버튼 Listener가 여러 번 호출되지 않는다.
            Shutdown();

            // ContentFlow(MainBattle)와 DevBootstrap(DEV Scene)이 모두 같은 형식의 Context를 전달한다.
            // 여기서 구체 타입을 확인해 두면 잘못된 StartData를 연결했을 때 조용히 오작동하지 않고 즉시 알 수 있다.
            context = contentContext ?? throw new ArgumentNullException(nameof(contentContext));
            startData = contentContext.StartData as GiantSpellbookStartData;
            if (startData?.Party == null)
            {
                throw new ArgumentException("GiantSpellbookStartData is required.", nameof(contentContext));
            }

            if (combatWorld == null || followerPrefab == null || exampleEnemyPrefab == null ||
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
            followerActors.Clear();
            currentBreakGauge = 0f;
            isBroken = false;
            breakRemainingTime = 0f;
            IsRunning = true;
            SpawnFollowers(); // 팀원이 내부 규칙을 붙이기 전 편성 연결만 확인
            SpawnExampleEnemy(); // 공용 전투 연결을 확인할 임시 적 한 기
        }

        private void Update()
        {
            if (!IsRunning || !isBroken)
            {
                return;
            }

            breakRemainingTime -= Time.deltaTime;

            if (breakRemainingTime <= 0f)
            {
                EndBreak();
            }
        }

        public void Shutdown()
        {
            // Shutdown은 MainBattle 복귀, DEV Scene 종료, 재초기화 모두에서 호출될 수 있다.
            // 여러 번 호출돼도 안전하도록 Listener 제거와 공용 전투 정리를 같은 순서로 반복한다.
            exitButton?.onClick.RemoveListener(Cancel);
            commanderMove?.SetInputEnabled(false);

            if (bossActor != null)
            {
                bossActor.Health.Damaged -= HandleBossDamaged;
                bossActor.Died -= HandleBossDied; // 보스 사망 이벤트 구독 해제
                bossActor = null;
            }

            ResetBreakState();
            combatWorld?.Clear();
            if (commanderRoot != null)
            {
                commanderRoot.SetActive(false);
            }

            context = null;
            startData = null;
            IsRunning = false;
        }

        private void SpawnFollowers()
        {
            /*
             * BattlePartySnapshot은 입장 시점에 계산이 끝난 아군 데이터다.
             * UnitId, 전투 능력치, 표시 색상, 정식 RuntimeAssetSet을 그대로 UnitSpawnRequest에 복사한다.
             * RuntimeAssetSet에 정식 Monster Prefab이 있으면 CombatWorld가 followerPrefab보다 그 Prefab을 우선 사용한다.
             *
             * UnitTeam.Player로 생성한 뒤 SetFollowAnchor를 호출하면 몬스터는 군단장 주변 지정 위치를 유지한다.
             * 적(UnitTeam.Enemy)을 발견하면 공용 UnitActor가 자동으로 접근·공격하고, 전투가 끝나면 다시 군단장을 따른다.
             * 이 개념이 어렵다면 Notion `04_1단계_현재시드구조_이해하기`에서 Snapshot 설명을 먼저 읽는다.
             */
            var partyUnits = startData.Party.Units;
            for (var i = 0; i < partyUnits.Length && i < followerOffsets.Length; i++)
            {
                var partyUnit = partyUnits[i];
                if (partyUnit == null)
                {
                    continue;
                }

                var request = new UnitSpawnRequest(
                    partyUnit.UnitId,
                    partyUnit.Stats,
                    UnitTeam.Player,
                    visualTint: partyUnit.VisualTint,
                    runtimeAssetSet: partyUnit.RuntimeAssetSet);
                var actor = combatWorld.SpawnUnit(
                    followerPrefab,
                    request,
                    commanderRoot.transform.position + followerOffsets[i],
                    Quaternion.identity);

                if (actor == null)
                {
                    continue;
                }

                actor?.SetFollowAnchor(commanderRoot.transform, followerOffsets[i], 6.5f, 8f);

                followerActors.Add(actor); // 브레이크 공격력 적용을 위해 생성된 아군 보관
            }
        }

        private void SpawnExampleEnemy()
        {
            var stats = new UnitStatsSnapshot
            {
                maxHealth = 5000f, // 임시 테스트 수치!!
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
            // 콘텐츠 종료·보스 사망·브레이크 상태에서는 게이지를 올리지 않는다.
            if (!IsRunning || report.Killed || isBroken)
            {
                return;
            }

            currentBreakGauge = Mathf.Min(
                currentBreakGauge + breakGaugePerHit,
                maxBreakGauge);

            Debug.Log(
                $"Break Gauge Remaining: {RemainingBreakGauge} / {maxBreakGauge}",
                this);

            if (currentBreakGauge >= maxBreakGauge)
            {
                StartBreak();
            }
        }

        // 게이지가 가득 차면 브레이크를 시작하고 아군 공격력을 증가시킨다.
        private void StartBreak()
        {
            if (!IsRunning || isBroken)
            {
                return;
            }

            isBroken = true;
            breakRemainingTime = breakDuration;

            for (var i = 0; i < followerActors.Count; i++)
            {
                var follower = followerActors[i];
                if (follower != null && follower.IsAlive)
                {
                    follower.SetDamageMultiplier(breakDamageMultiplier);
                }
            }

            Debug.Log(
                $"BREAK started! Duration={breakDuration}, Damage x{breakDamageMultiplier}",
                this);
        }

        // 브레이크 시간이 끝나면 공격력을 복구하고 게이지를 초기화한다.
        private void EndBreak()
        {
            if (!isBroken)
            {
                return;
            }

            isBroken = false;
            breakRemainingTime = 0f;
            currentBreakGauge = 0f;

            for (var i = 0; i < followerActors.Count; i++)
            {
                var follower = followerActors[i];
                if (follower != null)
                {
                    follower.SetDamageMultiplier(1f);
                }
            }

            Debug.Log("BREAK ended. Gauge reset.", this);
        }

        // 콘텐츠 종료 시 브레이크 배율과 실행값을 안전하게 초기화한다.
        private void ResetBreakState()
        {
            for (var i = 0; i < followerActors.Count; i++)
            {
                var follower = followerActors[i];
                if (follower != null)
                {
                    follower.SetDamageMultiplier(1f);
                }
            }

            followerActors.Clear();
            currentBreakGauge = 0f;
            isBroken = false;
            breakRemainingTime = 0f;
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
        private void Complete()
        {
            // 사망 이벤트가 중복으로 들어와도 성공 처리를 한 번만 실행
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;

            // 성공 처리 후 플레이어가 계속 움직이거나 나가기 버튼을 누르지 못하게 한다.
            commanderMove?.SetInputEnabled(false);
            exitButton?.onClick.RemoveListener(Cancel);

            // 전투 오브젝트를 정리하기 전에 보스 사망 이벤트 연결을 해제
            if (bossActor != null)
            {
                bossActor.Health.Damaged -= HandleBossDamaged;
                bossActor.Died -= HandleBossDied;
                bossActor = null;
            }

            ResetBreakState();
            combatWorld?.Clear();

            // 콘텐츠 내부에서는 저장하지 않고, 이번 판의 결과만 공용 출구로 전달
            var result = new GiantSpellbookResult();
            context?.Exit.Complete(result);
        }

        private void Cancel()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            commanderMove.SetInputEnabled(false);

            if (bossActor != null) //보스이벤트 해제
            {
                bossActor.Health.Damaged -= HandleBossDamaged;
                bossActor.Died -= HandleBossDied;
                bossActor = null;
            }
            ResetBreakState();
            combatWorld.Clear();
            // Cancel은 실패나 클리어가 아니므로 ResultAdapter와 보상 저장을 거치지 않는다.
            // MainBattle Hosted 실행에서는 ContentFlow가 Runtime을 닫고 기존 MainGameplayRoot를 다시 활성화한다.
            context.Exit.Cancel();
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            CombatWorld world,
            GameObject follower,
            GameObject enemy,
            Transform enemySpawn,
            GameObject commander,
            CommanderMoveController moveController,
            Button exit)
        {
            combatWorld = world;
            followerPrefab = follower;
            exampleEnemyPrefab = enemy;
            exampleEnemySpawn = enemySpawn;
            commanderRoot = commander;
            commanderMove = moveController;
            exitButton = exit;
        }
#endif
    }
}
