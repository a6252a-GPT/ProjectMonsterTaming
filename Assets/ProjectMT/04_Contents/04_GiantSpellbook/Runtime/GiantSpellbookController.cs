using System;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Input;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.GiantSpellbook
{
    /*
     * 거대마도서 콘텐츠의 "연결 예제"를 담당한다.
     *
     * 이 클래스가 완성된 던전 규칙을 소유하는 것은 아니다. 팀원이 작업을 시작할 때 필요한 아래 흐름만
     * 실제 프로젝트의 공용 시스템과 연결해 둔 상태다.
     *
     * 1. MainBattle 또는 DEV Bootstrap이 ContentContext를 전달한다.
     * 2. ContentContext 안의 GiantSpellbookStartData에서 현재 본부대 Snapshot을 읽는다.
     * 3. CommanderMoveController로 군단장을 터치패드/WASD로 이동시킨다.
     * 4. CombatWorld에 아군 몬스터와 예시 적을 서로 다른 UnitTeam으로 생성한다.
     * 5. UnitActor가 공용 타깃 탐색과 공격을 수행하므로 이 클래스가 매 프레임 공격 코드를 만들지 않는다.
     * 6. 나가기 버튼은 ContentContext.Exit.Cancel()을 호출해 Hosted 콘텐츠를 닫고 MainBattle로 돌아간다.
     *
     * 팀원이 실제 거대마도서 규칙을 구현할 때는 SpawnExampleEnemy()를 웨이브/보스 생성 로직으로 바꾸고,
     * 완료 조건에서 context.Exit.Complete(결과 데이터)를 호출하면 된다. 공용 이동·편성 Snapshot·종료 경계는
     * 그대로 사용하면 MainBattle과 DEV Scene이 같은 Runtime Prefab을 계속 공유할 수 있다.
     *
     * [초보 팀원용 Notion 권장 읽기 순서]
     * 1. `04_1단계_현재시드구조_이해하기`
     *    - Hosted, BattlePartySnapshot, ContentFlow, Runtime Prefab이 무엇인지 쉬운 설명부터 읽는다.
     * 2. `05_2단계_현재시드에서_최종구조로_가는방법`
     *    - 성장 던전 4종을 같은 Hosted 방식으로 늘리는 과정과 Result/저장/복귀 경계를 읽는다.
     * 3. `01_기능명세서` 안의 `16_특수콘텐츠`
     *    - SPECIAL-14~18 거대 마도서의 필수 기능과 공통 군단장 이동·몬스터 추종 요구사항을 확인한다.
     * 4. `00_프로젝트기획서`의 `거대 마도서` 항목
     *    - 브레이크, 집중 공격, 군단장 스킬 성장이라는 실제 콘텐츠 목표를 확인한다.
     * 5. `08_디자인패턴_현재시드와최종구조_이해하기`
     *    - Factory, Adapter, State/Flow 같은 이름이 어렵게 느껴질 때 각 클래스가 왜 분리되어 있는지 읽는다.
     *
     * Notion 문서 제목은 링크 주소가 바뀌어도 검색할 수 있도록 제목 그대로 적었다.
     *
     * [이 Runtime Prefab이 식량 대소동에서 가져온 공용 구성]
     * `PF_GiantSpellbookRuntime.prefab`은 검증된 `PF_FoodRiotRuntime.prefab`의 구성을 기준으로 만들었다.
     * 아래 컴포넌트는 거대마도서 고유 로직이 아니라 모든 전투 콘텐츠가 함께 쓰는 기반이므로 제거하지 않는다.
     *
     * - ScenePoolScope: 유닛·투사체·피격 VFX·플로팅 숫자를 매번 Instantiate/Destroy하지 않고 재사용한다.
     * - CombatWorld: 모든 UnitActor의 타깃 탐색과 근접/원거리 공격 실행을 한 곳에서 Tick한다.
     * - CombatFeedbackPlayer: 피해 결과를 피격 VFX, 카메라 흔들림, 숫자, SFX 표현으로 전달한다.
     * - FloatingNumberPresenter: 실제 적용된 피해량을 월드 위치의 데미지 숫자로 표시한다.
     * - SfxPool: 같은 효과음이 겹칠 때 AudioSource를 재사용한다. 현재 개별 SFX Cue 배정은 후속 작업이다.
     * - CameraImpulseRig: 공용 피격 피드백이 카메라 흔들림을 요청할 수 있는 연결점이다.
     * - PF_SeedProjectile: 원거리 몬스터가 같은 CombatWorld에서 투사체 공격을 실행할 때 사용하는 공용 원본이다.
     *
     * 반대로 식량 대소동 전용 VegetableArea, 야채 생성/도망, 제한시간, 처치 수, 식량 보상 코드는 제거했다.
     * 팀원은 위 공용 구성은 유지하고 GiantSpellbookController의 적/웨이브/브레이크 규칙만 확장하면 된다.
     */
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

        private readonly Vector3[] followerOffsets =
        {
            new Vector3(-1.2f, 0f, -0.9f),
            new Vector3(0f, 0f, -1.2f),
            new Vector3(1.2f, 0f, -0.9f),
            new Vector3(-0.7f, 0f, -2f),
            new Vector3(0.7f, 0f, -2f)
        };

        private ContentContext context; // 시작 정보와 Complete/Fail/Cancel 출구를 함께 전달하는 한 판의 공통 봉투
        private GiantSpellbookStartData startData; // 입장 순간의 본부대 구성을 고정해 보관하는 읽기 전용 시작값

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
            IsRunning = true;
            SpawnFollowers(); // 팀원이 내부 규칙을 붙이기 전 편성 연결만 확인
            SpawnExampleEnemy(); // 공용 전투 연결을 확인할 임시 적 한 기
        }

        public void Shutdown()
        {
            // Shutdown은 MainBattle 복귀, DEV Scene 종료, 재초기화 모두에서 호출될 수 있다.
            // 여러 번 호출돼도 안전하도록 Listener 제거와 공용 전투 정리를 같은 순서로 반복한다.
            exitButton?.onClick.RemoveListener(Cancel);
            commanderMove?.SetInputEnabled(false);
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
                actor?.SetFollowAnchor(commanderRoot.transform, followerOffsets[i], 6.5f, 8f);
            }
        }

        private void SpawnExampleEnemy()
        {
            /*
             * 팀원이 공용 전투 연결 방식을 바로 확인할 수 있도록 만든 임시 적 한 기다.
             * 별도의 Update 공격 코드를 작성하지 않는다. UnitTeam.Enemy로 CombatWorld에 등록하면
             * 공용 UnitActor가 Player 팀 몬스터를 찾고, 반대로 Player 팀 몬스터도 이 적을 찾는다.
             *
             * maxHealth는 여러 번 맞는 모습을 보기 위해 높게 두고 damage는 1로 낮게 둔다.
             * 실제 던전을 만들 때는 이 고정 Snapshot을 데이터/SO와 웨이브 생성 코드로 교체하면 된다.
             * 전투 공용 책임 범위는 Notion `03_시드구조도`의 CombatWorld·UnitActor 부분을 참고한다.
             */
            var stats = new UnitStatsSnapshot
            {
                maxHealth = 10000f, // 여러 몬스터에게 맞아도 오래 살아 전투 흐름을 관찰할 수 있게 설정
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
            combatWorld.SpawnUnit(
                exampleEnemyPrefab,
                request,
                exampleEnemySpawn.position,
                Quaternion.identity);
        }

        private void Cancel()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            commanderMove.SetInputEnabled(false);
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
