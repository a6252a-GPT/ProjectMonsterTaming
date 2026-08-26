using System.Collections;
using ProjectMT.Contents.Framework;
using ProjectMT.Contents.TreasureSpirit;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
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
        [SerializeField] private GameObject cardPanel;
        [SerializeField] private ItemDefinition keyItemDefinition;
        [SerializeField] private GameObject contentResultOverlayPrefab;
        [SerializeField] private GrowthDungeonRewardTable rewardTable;
        [SerializeField] private string contentDisplayName = "보물 정령 숨바꼭질";

        [Header("던전 설정")]
        [SerializeField] private float timeLimitSeconds = 100f;
        [SerializeField] private string exitSceneName = "00_Entry";
        [SerializeField] private string nextSceneName = "00_Entry";
        [SerializeField] private float resultTextHideDelay = 20f;
        [SerializeField] private float keyCardHideDelay = 10f;

        private float timeRemaining;
        private int killCount;
        private BakedDungeonLoader keyState;
        private Coroutine resultTextHideRoutine;
        private Coroutine keyCardHideRoutine;
        private IContentResultView resultView;

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

            commanderMove?.SetInputEnabled(true);

            timeRemaining = timeLimitSeconds;
            killCount = 0;
            IsRunning = true;
            Time.timeScale = 1f;

            SpawnFollower();
            UpdateHud();
            EnsureCardPanel();
            HideCardPanel();
            BeginResultTextAutoHide();
        }

        private void ShutdownInternal()
        {
            IsRunning = false;
            StopAllCoroutines();
            commanderMove?.SetInputEnabled(false);

            if (keyState != null)
            {
                keyState.KeyGranted -= OnKeyGranted;
            }

            bakedDungeonLoader?.ClearMap();
            keyState = null;
            HideCardPanel();

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
            ShowKeyCardPanel();
        }

        private void BeginResultTextAutoHide()
        {
            if (resultTextHideRoutine != null)
            {
                StopCoroutine(resultTextHideRoutine);
            }

            if (resultText != null)
            {
                resultText.gameObject.SetActive(true);
            }

            resultTextHideRoutine = StartCoroutine(HideResultTextAfterDelay());
        }

        private IEnumerator HideResultTextAfterDelay()
        {
            yield return new WaitForSecondsRealtime(resultTextHideDelay);
            if (resultText != null && IsRunning)
            {
                resultText.gameObject.SetActive(false);
            }

            resultTextHideRoutine = null;
        }

        private void ShowKeyCardPanel()
        {
            EnsureCardPanel();
            if (cardPanel == null)
            {
                return;
            }

            BindKeyCardContents();
            cardPanel.SetActive(true);

            if (keyCardHideRoutine != null)
            {
                StopCoroutine(keyCardHideRoutine);
            }

            keyCardHideRoutine = StartCoroutine(HideKeyCardAfterDelay());
        }

        private IEnumerator HideKeyCardAfterDelay()
        {
            yield return new WaitForSecondsRealtime(keyCardHideDelay);
            HideCardPanel();
            keyCardHideRoutine = null;
        }

        private void HideCardPanel()
        {
            if (cardPanel != null)
            {
                cardPanel.SetActive(false);
            }
        }

        private void EnsureCardPanel()
        {
            if (cardPanel != null)
            {
                return;
            }

            Transform hudRoot = resultText != null
                ? resultText.transform.parent
                : (statusText != null ? statusText.transform.parent : null);
            if (hudRoot == null)
            {
                return;
            }

            Transform existing = hudRoot.Find("CardPanel");
            if (existing != null)
            {
                cardPanel = existing.gameObject;
                return;
            }

            TMP_FontAsset font = resultText != null
                ? resultText.font
                : (statusText != null ? statusText.font : null);

            GameObject panelObject = new GameObject("CardPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.SetParent(hudRoot, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(520f, 220f);

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.sprite = CreateWhiteSprite();
            panelImage.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);
            panelImage.raycastTarget = false;

            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(panelRect, false);
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(86f, 0f);
            iconRect.sizeDelta = new Vector2(112f, 112f);
            Image iconImage = iconObject.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            GameObject titleObject = CreateCardText("Title", panelRect, font, 32f, FontStyles.Bold);
            RectTransform titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(168f, -28f);
            titleRect.sizeDelta = new Vector2(-188f, 48f);

            GameObject bodyObject = CreateCardText("Description", panelRect, font, 22f, FontStyles.Normal);
            RectTransform bodyRect = bodyObject.GetComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.pivot = new Vector2(0f, 1f);
            bodyRect.anchoredPosition = new Vector2(168f, -84f);
            bodyRect.sizeDelta = new Vector2(-188f, -108f);

            cardPanel = panelObject;
        }

        private static GameObject CreateCardText(
            string objectName,
            Transform parent,
            TMP_FontAsset font,
            float fontSize,
            FontStyles style)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            return textObject;
        }

        private static Sprite CreateWhiteSprite()
        {
            Texture2D texture = Texture2D.whiteTexture;
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private void BindKeyCardContents()
        {
            if (cardPanel == null)
            {
                return;
            }

            Transform iconTransform = cardPanel.transform.Find("Icon");
            Image iconImage = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            if (iconImage != null)
            {
                iconImage.sprite = keyItemDefinition != null ? keyItemDefinition.Icon : null;
                iconImage.enabled = iconImage.sprite != null;
            }

            Transform titleTransform = cardPanel.transform.Find("Title");
            TMP_Text titleText = titleTransform != null ? titleTransform.GetComponent<TMP_Text>() : null;
            if (titleText != null)
            {
                titleText.text = keyItemDefinition != null ? keyItemDefinition.DisplayName : "열쇠 획득";
            }

            Transform bodyTransform = cardPanel.transform.Find("Description");
            TMP_Text bodyText = bodyTransform != null ? bodyTransform.GetComponent<TMP_Text>() : null;
            if (bodyText != null)
            {
                bodyText.text = keyItemDefinition != null
                    ? keyItemDefinition.Description
                    : "감옥 문을 열 수 있는 열쇠를 획득했습니다.";
            }
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

            FinishGame(true, "던전 탈출 성공");
        }

        private void OnTimeOut()
        {
            if (!IsRunning)
            {
                return;
            }

            FinishGame(false, "제한 시간이 초과되었습니다");
        }

        public void FailDungeon(string summary)
        {
            if (!IsRunning)
            {
                return;
            }

            FinishGame(false, string.IsNullOrWhiteSpace(summary) ? "던전 실패" : summary);
        }

        private void FinishGame(bool isSuccess, string summary)
        {
            IsRunning = false;
            Time.timeScale = 0f;
            commanderMove?.SetInputEnabled(false);
            HideCardPanel();

            if (resultText != null)
            {
                resultText.gameObject.SetActive(false);
            }

            StartCoroutine(ShowContentResultThenReload(isSuccess, summary));
        }

        private IEnumerator ShowContentResultThenReload(bool isSuccess, string summary)
        {
            IContentResultView view = ResolveResultView();
            if (view != null)
            {
                var task = view.ShowAsync(CreateResultPresentation(isSuccess, summary));
                while (task != null && !task.IsCompleted)
                {
                    yield return null;
                }
            }
            else
            {
                Debug.LogWarning("[DemoDungeonController] 식량대소동과 같은 결과창을 찾지 못했습니다.");
                yield return new WaitForSecondsRealtime(2f);
            }

            OnConfirmAndReload();
        }

        private ContentResultPresentation CreateResultPresentation(bool isSuccess, string summary)
        {
            ContentOutcome outcome = isSuccess ? ContentOutcome.Complete : ContentOutcome.Fail;
            RewardPresentationRequest rewards = null;
            if (isSuccess &&
                rewardTable != null &&
                rewardTable.TryCreate(1, ContentRunMode.SeedTest, out RewardBundle bundle))
            {
                rewards = RewardPresentationRequest.FromBundle(bundle);
            }

            return new ContentResultPresentation(
                new ContentId("treasure_spirit"),
                contentDisplayName,
                outcome,
                summary,
                rewards);
        }

        private IContentResultView ResolveResultView()
        {
            if (resultView != null)
            {
                return resultView;
            }

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IContentResultView view)
                {
                    resultView = view;
                    return resultView;
                }
            }

            GameObject prefab = ResolveResultOverlayPrefab();
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = Instantiate(prefab);
            instance.name = "PF_ContentResultOverlay_Runtime";
            resultView = instance.GetComponent<IContentResultView>() ??
                         instance.GetComponentInChildren<IContentResultView>(true);
            return resultView;
        }

        private GameObject ResolveResultOverlayPrefab()
        {
            if (contentResultOverlayPrefab != null)
            {
                return contentResultOverlayPrefab;
            }

#if UNITY_EDITOR
            const string overlayGuid = "69159d92b2e62204a92b65cace4a6bfd";
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(overlayGuid);
            contentResultOverlayPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#endif
            return contentResultOverlayPrefab;
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
