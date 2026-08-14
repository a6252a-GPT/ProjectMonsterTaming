using System;
using System.Collections;
using Unity.AI.Navigation; // ★ NavMeshSurface 사용을 위한 네임스페이스
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.UI;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectMT.Contents.TreasureSpirit
{
    [DisallowMultipleComponent]
    public sealed class DungeonController : MonoBehaviour, IContentController
    {
        [Header("미로 및 전투 환경")]
        [SerializeField] private MazeGenerator mazeGenerator;
        [SerializeField] private NavMeshSurface navMeshSurface; // ★ NavMeshSurface 참조 추가
        [SerializeField] private CombatWorld combatWorld;
        [SerializeField] private FollowerSpawner followerSpawner;
        [SerializeField] private GameObject commanderRoot;
        [SerializeField] private PlayerCharacterController commanderMove;

        [Header("HUD 요소")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text killCountText; // ★ 킬 카운트 텍스트 추가
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button exitButton;
        [SerializeField] private ContentClearOverlay clearOverlay;

        [Header("던전 설정")]
        [SerializeField] private float timeLimitSeconds = 100f;
        [SerializeField] private string nextSceneName = "LobbyScene";

        private ContentContext context;
        private float timeRemaining;
        private int killCount; // ★ 처치 수치 저장 변수
        private Coroutine clearGuideCoroutine;

        public bool IsRunning { get; private set; }

        private void Start()
        {
            if (!IsRunning && context == null)
            {
                Initialize();
            }
        }

        public void Initialize(ContentContext contentContext)
        {
            Shutdown();
            context = contentContext;
            InitializeInternal();
        }

        public void Initialize()
        {
            Shutdown();
            InitializeInternal();
        }

        private void InitializeInternal()
        {
            if (mazeGenerator != null)
            {
                mazeGenerator.GenerateMaze();
            }
            else
            {
                Debug.LogError("MazeGenerator가 누락되었습니다.");
                return;
            }

            // 미로 생성 후 한 프레임 대기하고 NavMesh 빌드 및 스폰 진행
            StartCoroutine(BuildNavMeshAndSpawnRoutine());
        }

        private IEnumerator BuildNavMeshAndSpawnRoutine()
        {
            // 1. Instantiate된 미로 오브젝트들의 Collider가 물리 엔진에 등록될 때까지 대기
            yield return new WaitForFixedUpdate();
            yield return null;

            // 2. NavMesh 실시간 빌드
            if (navMeshSurface == null && mazeGenerator != null)
            {
                navMeshSurface = mazeGenerator.GetComponent<NavMeshSurface>();
            }

            if (navMeshSurface != null)
            {
                navMeshSurface.BuildNavMesh();
                Debug.Log("✅ NavMesh 빌드 완료!");
            }
            else
            {
                Debug.LogError("❌ NavMeshSurface를 찾을 수 없습니다.");
                yield break;
            }

            // 3. 환경 및 HUD 초기화
            combatWorld.Clear();
            clearOverlay?.Hide();
            exitButton?.onClick.AddListener(OnExitButtonClicked);

            commanderRoot.SetActive(true);
            commanderMove?.SetInputEnabled(true);

            timeRemaining = timeLimitSeconds;
            killCount = 0; // ★ 킬 카운트 초기화
            IsRunning = true;
            Time.timeScale = 1f;

            // 4. NavMesh 생성이 끝난 것을 확인한 후 안전하게 팔로워 스폰
            SpawnFollower();
            UpdateHud();
        }

        public void Shutdown()
        {
            IsRunning = false;
            StopAllCoroutines();
            clearOverlay?.Hide();
            exitButton?.onClick.RemoveListener(OnExitButtonClicked);
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
            if (!IsRunning) return;

            timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
            UpdateHud();

            if (timeRemaining <= 0f)
            {
                OnTimeOut();
            }
        }

        private void SpawnFollower()
        {
            if (followerSpawner == null || commanderRoot == null)
            {
                Debug.LogWarning("FollowerSpawner 또는 commanderRoot가 설정되지 않았습니다.");
                return;
            }

            Vector3 offset = new Vector3(0f, 0f, -1.2f);
            Vector3 spawnPosition = commanderRoot.transform.position + offset;

            // NavMesh의 유효한 위치인지 검증 후 보정 스폰
            if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                spawnPosition = hit.position;
            }

            followerSpawner.SpawnFollower(spawnPosition);
        }

        /// <summary>
        /// 팔로워가 적을 처치했을 때 호출하여 킬수를 1 올립니다.
        /// </summary>
        public void AddKillCount()
        {
            killCount++;
            UpdateHud();
        }

        private void UpdateHud()
        {
            if (timerText != null)
            {
                timerText.text = $"남은 시간: {Mathf.CeilToInt(timeRemaining)}초";
            }

            if (statusText != null && mazeGenerator != null)
            {
                statusText.text = mazeGenerator.HasKey ? "열쇠 획득: O" : "열쇠 획득: X";
            }

            // ★ HUD 처치 텍스트 업데이트 ("처치 : 0")
            if (killCountText != null)
            {
                killCountText.text = $"처치 : {killCount}";
            }
        }

        public void CompleteDungeon()
        {
            if (!IsRunning) return;

            FinishGame(true, "던전 탈출 성공!", "보상: 던전 클리어 상자");
        }

        private void OnTimeOut()
        {
            if (!IsRunning) return;

            FinishGame(false, "제한 시간이 초과되었습니다.", "보상 없음");
        }

        private void FinishGame(bool isSuccess, string summary, string reward)
        {
            IsRunning = false;
            Time.timeScale = 0f;

            commanderMove?.SetInputEnabled(false);

            if (resultText != null)
            {
                resultText.text = summary;
            }

            if (clearOverlay != null && clearOverlay.TryShow(
                summary,
                reward,
                OnConfirmAndLoadScene,
                isSuccess ? "클리어" : "실패"))
            {
                return;
            }

            OnConfirmAndLoadScene();
        }

        private void OnConfirmAndLoadScene()
        {
            Time.timeScale = 1f;

            if (context != null)
            {
                context.Exit.Complete(null);
            }
            else
            {
                SceneManager.LoadScene(nextSceneName);
            }
        }

        private void OnExitButtonClicked()
        {
            IsRunning = false;
            Time.timeScale = 1f;

            if (context != null)
            {
                context.Exit.Cancel();
            }
            else
            {
                Shutdown();
                SceneManager.LoadScene(nextSceneName);
            }
        }

        private void OnDestroy()
        {
            Shutdown();
        }
    }
}