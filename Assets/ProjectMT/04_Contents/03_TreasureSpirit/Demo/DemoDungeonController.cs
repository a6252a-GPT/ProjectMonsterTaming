using System.Collections;
using ProjectMT.Contents.TreasureSpirit;
using ProjectMT.Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    /// <summary>
    /// 베이크 던전 데모 컨트롤러. BakedDungeonLoader로 5종 맵을 순환합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoDungeonController : MonoBehaviour
    {
        [Header("베이크 맵")]
        [SerializeField] private BakedDungeonLoader bakedDungeonLoader;

        [Header("전투 / 유닛")]
        [SerializeField] private FollowerSpawner followerSpawner;
        [SerializeField] private GameObject commanderRoot;
        [SerializeField] private PlayerCharacterController commanderMove;

        [Header("HUD")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text killCountText;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button exitButton;
        [SerializeField] private ContentClearOverlay clearOverlay;

        [Header("던전 설정")]
        [SerializeField] private float timeLimitSeconds = 100f;
        [SerializeField] private string exitSceneName = "00_Entry";
        [SerializeField] private string nextSceneName = "00_Entry";

        private float timeRemaining;
        private int killCount;
        private BakedDungeonLoader keyState;

        public bool IsRunning { get; private set; }

        private void Awake()
        {
            BindExitButton();
        }

        private void Start()
        {
            if (!IsRunning)
            {
                Initialize();
            }
        }

        public void Initialize()
        {
            ShutdownInternal();

            if (bakedDungeonLoader == null)
            {
                Debug.LogError("[DemoDungeonController] BakedDungeonLoader가 연결되지 않았습니다.");
                return;
            }

            bakedDungeonLoader.LoadNextMap();
            keyState = bakedDungeonLoader;

            if (bakedDungeonLoader.ActiveMapInstance == null)
            {
                Debug.LogError("[DemoDungeonController] 베이크 맵 로드에 실패했습니다.");
                return;
            }

            StartCoroutine(BuildNavMeshAndSpawnRoutine());
        }

        private IEnumerator BuildNavMeshAndSpawnRoutine()
        {
            yield return new WaitForFixedUpdate();
            yield return null;

            GameObject mapInstance = bakedDungeonLoader.ActiveMapInstance;
            if (mapInstance == null)
            {
                yield break;
            }

            DemoDoorBinder.Bind(mapInstance.transform);
            bakedDungeonLoader.KeyGranted += OnKeyGranted;

            if (!DemoNavMeshBuilder.BuildForMap(mapInstance))
            {
                Debug.LogError("[DemoDungeonController] NavMesh 빌드에 실패했습니다.");
            }

            yield return new WaitForFixedUpdate();

            bakedDungeonLoader.PlaceCommander();

            if (commanderRoot != null)
            {
                commanderRoot.SetActive(true);
            }

            bakedDungeonLoader.SpawnRoomContents();
            bakedDungeonLoader.SpawnEndRoomPrison(this);

            clearOverlay?.Hide();

            commanderMove?.SetInputEnabled(true);

            timeRemaining = timeLimitSeconds;
            killCount = 0;
            IsRunning = true;
            Time.timeScale = 1f;

            SpawnFollower();
            UpdateHud();
        }

        private void ShutdownInternal()
        {
            IsRunning = false;
            StopAllCoroutines();
            clearOverlay?.Hide();
            commanderMove?.SetInputEnabled(false);

            if (keyState != null)
            {
                keyState.KeyGranted -= OnKeyGranted;
            }

            bakedDungeonLoader?.ClearMap();
            keyState = null;

            if (commanderRoot != null)
            {
                commanderRoot.SetActive(false);
            }
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
                OnTimeOut();
            }
        }

        private void SpawnFollower()
        {
            if (followerSpawner == null || bakedDungeonLoader?.ActiveMapInstance == null)
            {
                return;
            }

            Transform mapRoot = bakedDungeonLoader.ActiveMapInstance.transform;
            Vector3 spawnPosition;

            if (DemoSpawnResolver.TryGetSpawnPosition(mapRoot, 0.5f, out spawnPosition))
            {
                spawnPosition += new Vector3(0f, 0f, -1.2f);
            }
            else if (commanderRoot != null)
            {
                spawnPosition = commanderRoot.transform.position + new Vector3(0f, 0f, -1.2f);
            }
            else
            {
                return;
            }

            DemoSpawnResolver.TrySnapToNavMesh(ref spawnPosition, 3f);
            followerSpawner.SpawnFollower(spawnPosition);
        }

        private void OnKeyGranted()
        {
            UpdateHud();
        }

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

            if (statusText != null)
            {
                bool hasKey = keyState != null && keyState.HasKey;
                statusText.text = hasKey ? "열쇠 획득: O" : "열쇠 획득: X";
            }

            if (killCountText != null)
            {
                killCountText.text = $"처치 : {killCount}";
            }
        }

        public void CompleteDungeon()
        {
            if (!IsRunning)
            {
                return;
            }

            FinishGame(true, "던전 탈출 성공!", "보상: 던전 클리어 상자");
        }

        private void OnTimeOut()
        {
            if (!IsRunning)
            {
                return;
            }

            FinishGame(false, "제한 시간이 초과되었습니다.", "보상 없음");
        }

        private void FinishGame(bool isSuccess, string summary, string reward)
        {
            IsRunning = false;
            Time.timeScale = 0f;
            commanderMove?.SetInputEnabled(false);

            if (resultText != null)
            {
                resultText.gameObject.SetActive(true);
                resultText.text = summary;
            }

            if (clearOverlay != null && clearOverlay.TryShow(
                summary,
                reward,
                OnConfirmAndReload,
                isSuccess ? "클리어" : "실패"))
            {
                return;
            }

            OnConfirmAndReload();
        }

        private void OnConfirmAndReload()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(nextSceneName);
        }

        private void OnExitButtonClicked()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(exitSceneName);
        }

        private void BindExitButton()
        {
            if (exitButton == null)
            {
                return;
            }

            exitButton.onClick.RemoveListener(OnExitButtonClicked);
            exitButton.onClick.AddListener(OnExitButtonClicked);
        }

        private void OnDestroy()
        {
            exitButton?.onClick.RemoveListener(OnExitButtonClicked);
        }
    }
}
