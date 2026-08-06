using System;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Input;
using ProjectMT.Shared.UI;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.GuardianTrial
{
    // 08.06 안건준 추가 - 수호자의 탑 진행·결과 총괄
    // 식량 대소동(FoodRiotController)과 코드/프리팹/데이터를 완전히 분리한 전용 콘텐츠 컨트롤러.
    // 차이점: 적은 시작할 때 한 번만 스폰(재보충 없음), 제한 시간 1분, 방어 건물 4개 관리, 상단 처치/남은 게이지 표시.
    [DisallowMultipleComponent]
    public sealed class GuardiansTowerController : MonoBehaviour, IContentController
    {
        [Header("Runtime")]
        [SerializeField] private CombatWorld combatWorld; // 유닛 생성·정리 공간
        [SerializeField] private GameObject followerPrefab; // 아군 추종자 원본
        [SerializeField] private GameObject enemyPrefab; // 침입하는 적 원본 (수호자의 탑 전용)
        [SerializeField] private GameObject commanderRoot; // 직접 조작 군단장
        [SerializeField] private CommanderMoveController commanderMove; // 군단장 이동 입력
        [SerializeField] private Transform enemyAreaCenter; // 적 스폰 구역 중심
        [SerializeField] private Vector2 enemyAreaHalfExtents = new Vector2(5.5f, 3.5f); // 스폰 구역 반쪽 크기
        [SerializeField] private GuardiansTowerStructure[] structures = new GuardiansTowerStructure[0]; // 네 모서리 방어 건물

        [Header("HUD")]
        [SerializeField] private TMP_Text timerText; // 남은 시간 표시
        [SerializeField] private TMP_Text resultText; // 조작 안내·결과 문구
        [SerializeField] private TMP_Text enemyGaugeText; // "잡은 수량 X / 남은 수량 Y"
        [SerializeField] private Image enemyGaugeFillImage; // Type=Filled 상단 게이지
        [SerializeField] private Button exitButton; // 콘텐츠 나가기 버튼
        [SerializeField] private ContentClearOverlay clearOverlay; // 종료 결과 화면

        private ContentContext context; // 결과 반환 통로
        private GuardiansTowerStartData startData; // 이번 판 시작 정보
        private float timeRemaining; // 남은 제한 시간
        private int killCount; // 이번 판 처치 수
        private int enemyTotalCount; // 시작 시 스폰한 총 적 수 (게이지 분모, 판 중 고정)
        private int enemyAliveCount; // 현재 생존 적 수

        public bool IsRunning { get; private set; }

        public void Initialize(ContentContext contentContext)
        {
            Shutdown(); // 재초기화 전 이전 판 정리
            context = contentContext ?? throw new ArgumentNullException(nameof(contentContext));
            startData = contentContext.StartData as GuardiansTowerStartData;
            if (startData == null || startData.Party == null)
            {
                throw new ArgumentException("GuardiansTowerStartData is required.", nameof(contentContext));
            }

            if (combatWorld == null || followerPrefab == null || enemyPrefab == null || commanderRoot == null)
            {
                throw new InvalidOperationException("Guardians Tower runtime references are missing.");
            }

            combatWorld.Clear(); // 이전 판의 남은 유닛 제거
            clearOverlay?.Hide();
            commanderRoot.SetActive(true);
            commanderMove?.SetInputEnabled(true);
            exitButton?.onClick.AddListener(Cancel);
            timeRemaining = startData.DurationSeconds;
            killCount = 0;
            enemyTotalCount = startData.EnemyCount;
            enemyAliveCount = 0;
            IsRunning = true;
            if (resultText != null)
            {
                resultText.text = "이동 키나 조이스틱으로 움직이세요";
            }

            SpawnFollowers();
            InitializeStructures(); // 방어 건물 체력 100으로 초기화
            for (var i = 0; i < enemyTotalCount; i++)
            {
                SpawnEnemy(i); // 시작할 때 한 번만 스폰 (재보충 없음)
            }

            UpdateHud();
        }

        public void Shutdown()
        {
            clearOverlay?.Hide();
            exitButton?.onClick.RemoveListener(Cancel);
            commanderMove?.SetInputEnabled(false);
            ShutdownStructures();
            combatWorld?.Clear();
            if (commanderRoot != null)
            {
                commanderRoot.SetActive(false);
            }

            context = null;
            startData = null;
            IsRunning = false;
        }

        private void Update()
        {
            if (!IsRunning)
            {
                return;
            }

            timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
            UpdateHud();
            if (timeRemaining <= 0f)
            {
                Complete();
            }
        }

        private void SpawnFollowers()
        {
            var offsets = new[] // 군단장 뒤쪽 추종 대형
            {
                new Vector3(-1.2f, 0f, -0.9f),
                new Vector3(0f, 0f, -1.2f),
                new Vector3(1.2f, 0f, -0.9f),
                new Vector3(-0.7f, 0f, -2f),
                new Vector3(0.7f, 0f, -2f)
            };
            var partyUnits = startData.Party.Units;
            for (var i = 0; i < partyUnits.Length && i < offsets.Length; i++)
            {
                var spawnPosition = commanderRoot.transform.position + offsets[i];
                var request = new UnitSpawnRequest(
                    partyUnits[i].UnitId,
                    partyUnits[i].Stats,
                    UnitTeam.Player,
                    visualTint: partyUnits[i].VisualTint);
                var actor = combatWorld.SpawnUnit(followerPrefab, request, spawnPosition, Quaternion.identity);
                actor?.SetFollowAnchor(commanderRoot.transform, offsets[i], 6.5f, 8f);
            }
        }

        private void InitializeStructures()
        {
            if (structures == null)
            {
                return;
            }

            for (var i = 0; i < structures.Length; i++)
            {
                structures[i]?.Initialize(combatWorld);
            }
        }

        private void ShutdownStructures()
        {
            if (structures == null)
            {
                return;
            }

            for (var i = 0; i < structures.Length; i++)
            {
                structures[i]?.Shutdown();
            }
        }

        private void SpawnEnemy(int sequence)
        {
            var center = enemyAreaCenter == null ? transform.position : enemyAreaCenter.position;
            var position = center + new Vector3(
                UnityEngine.Random.Range(-enemyAreaHalfExtents.x, enemyAreaHalfExtents.x),
                0f,
                UnityEngine.Random.Range(-enemyAreaHalfExtents.y, enemyAreaHalfExtents.y));
            var stats = new UnitStatsSnapshot // 침입 적: 직접 이동·공격하여 건물·아군을 위협
            {
                maxHealth = 6f,
                damage = 4f,
                defense = 0f,
                moveSpeed = 2.2f,
                attackRange = 0.6f,
                attackInterval = 1f,
                projectileSpeed = 0f,
                ranged = false
            };
            var request = new UnitSpawnRequest($"guardians_tower_enemy_{sequence}", stats, UnitTeam.Enemy);
            var actor = combatWorld.SpawnUnit(enemyPrefab, request, position, Quaternion.identity);
            if (actor == null)
            {
                return;
            }

            enemyAliveCount++;
            actor.Died += HandleEnemyDied; // 요청: 재보충 없이 처치 수만 집계
        }

        private void HandleEnemyDied(UnitActor actor)
        {
            if (!IsRunning)
            {
                return;
            }

            killCount++;
            enemyAliveCount = Mathf.Max(0, enemyAliveCount - 1);
            UpdateHud();
            if (enemyAliveCount <= 0)
            {
                Complete(); // 남은 적을 모두 잡으면 제한 시간 전에도 즉시 종료
            }
        }

        private void Complete()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            commanderMove?.SetInputEnabled(false);
            combatWorld.Clear();
            if (resultText != null)
            {
                resultText.text = clearOverlay == null ? $"완료 · 처치 {killCount}" : string.Empty;
            }

            var result = new GuardiansTowerResult(killCount); // 최종 처치 수를 보상 계층에 전달
            if (clearOverlay != null &&
                clearOverlay.TryShow($"처치 {killCount}마리", $"골드 +{killCount}", () => CompleteClear(result)))
            {
                return;
            }

            CompleteClear(result);
        }

        private void CompleteClear(GuardiansTowerResult result)
        {
            context?.Exit.Complete(result);
        }

        private void Cancel()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            commanderMove?.SetInputEnabled(false);
            combatWorld.Clear();
            context.Exit.Cancel(); // 보상 없이 콘텐츠 종료
        }

        private void UpdateHud()
        {
            if (timerText != null)
            {
                timerText.text = $"남은 시간 {Mathf.CeilToInt(timeRemaining)}초";
            }

            if (enemyGaugeText != null)
            {
                enemyGaugeText.text = $"잡은 수량 {killCount} / 남은 수량 {enemyAliveCount}";
            }

            if (enemyGaugeFillImage != null)
            {
                enemyGaugeFillImage.fillAmount = enemyTotalCount <= 0 ? 0f : (float)enemyAliveCount / enemyTotalCount; // 남은 비율만큼 게이지 감소
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            CombatWorld world,
            GameObject follower,
            GameObject enemy,
            GameObject commander,
            CommanderMoveController moveController,
            Transform areaCenter,
            GuardiansTowerStructure[] structureList,
            TMP_Text timer,
            TMP_Text result,
            TMP_Text gaugeText,
            Image gaugeFill,
            Button exit,
            ContentClearOverlay overlay)
        {
            combatWorld = world;
            followerPrefab = follower;
            enemyPrefab = enemy;
            commanderRoot = commander;
            commanderMove = moveController;
            enemyAreaCenter = areaCenter;
            structures = structureList ?? new GuardiansTowerStructure[0];
            timerText = timer;
            resultText = result;
            enemyGaugeText = gaugeText;
            enemyGaugeFillImage = gaugeFill;
            exitButton = exit;
            clearOverlay = overlay;
        }
#endif
    }
}
