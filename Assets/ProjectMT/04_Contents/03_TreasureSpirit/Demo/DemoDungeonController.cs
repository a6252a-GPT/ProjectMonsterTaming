using System;
using System.Collections;
using ProjectMT.Contents.Framework;
using ProjectMT.Contents.TreasureSpirit;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    /// <summary>
    /// 베이크 던전 데모 컨트롤러. BakedDungeonLoader로 5종 맵을 순환합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoDungeonController : MonoBehaviour, IContentController
    {
        [Header("베이크 맵")]
        [SerializeField] private BakedDungeonLoader bakedDungeonLoader;

        [Header("전투 / 유닛")]
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

        [Header("던전 설정")]
        [SerializeField] private float timeLimitSeconds = 100f;
        [SerializeField] private float resultTextHideDelay = 20f;
        [SerializeField] private float keyCardHideDelay = 10f;

        private ContentContext context;
        private TreasureSpiritStartData startData;
        private float difficultyMultiplier = 1f;
        private float timeRemaining;
        private int killCount;
        private BakedDungeonLoader keyState;
        private Coroutine resultTextHideRoutine;
        private Coroutine keyCardHideRoutine;
        private readonly DemoLifeHud lifeHud = new DemoLifeHud();
        private DemoJumpButton jumpButton;

        public bool IsRunning { get; private set; }

        private void Awake()
        {
            BindExitButton();
        }

        public void Initialize(ContentContext contentContext)
        {
            Shutdown();
            context = contentContext ?? throw new ArgumentNullException(nameof(contentContext));
            startData = contentContext.StartData as TreasureSpiritStartData;
            if (startData == null || startData.Party == null)
            {
                throw new ArgumentException("TreasureSpiritStartData is required.", nameof(contentContext));
            }

            if (bakedDungeonLoader == null)
            {
                throw new InvalidOperationException("Treasure Spirit runtime references are missing.");
            }

            var stage = int.TryParse(context.RunInfo.StageId, out var selectedStage) &&
                        GrowthDungeonStageRules.IsValidStage(selectedStage)
                ? selectedStage
                : 1;
            difficultyMultiplier = GrowthDungeonStageRules.ResolveDifficultyMultiplier(stage);

            bakedDungeonLoader.LoadNextMap();
            keyState = bakedDungeonLoader;

            if (bakedDungeonLoader.ActiveMapInstance == null)
            {
                throw new InvalidOperationException("Treasure Spirit baked map loading failed.");
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

            bakedDungeonLoader.SpawnRoomContents(difficultyMultiplier);
            bakedDungeonLoader.SpawnEndRoomPrison(this);

            commanderMove?.SetInputEnabled(true);
            BindLifeHud();

            timeRemaining = startData != null ? startData.DurationSeconds : timeLimitSeconds;
            killCount = 0;
            IsRunning = true;
            Time.timeScale = 1f;

            UpdateHud();
            EnsureCardPanel();
            HideCardPanel();
            BeginResultTextAutoHide();
        }

        private void ShutdownInternal()
        {
            IsRunning = false;
            StopAllCoroutines();
            UnbindLifeHud();
            commanderMove?.SetInputEnabled(false);

            if (keyState != null)
            {
                keyState.KeyGranted -= OnKeyGranted;
            }

            bakedDungeonLoader?.ClearMap();
            keyState = null;
            HideCardPanel();
            DemoChestQuizOverlay.HideActive();
            lifeHud.Hide();
            jumpButton?.Hide();

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

        private void BindLifeHud()
        {
            Transform hudRoot = ResolveHudRoot();
            lifeHud.Ensure(hudRoot);
            lifeHud.Show();
            jumpButton = DemoJumpButton.Ensure(hudRoot, commanderMove);
            jumpButton?.Show();

            if (commanderMove == null)
            {
                return;
            }

            commanderMove.LivesChanged -= OnPlayerLivesChanged;
            commanderMove.LivesChanged += OnPlayerLivesChanged;
            commanderMove.ResetLives();
        }

        private void UnbindLifeHud()
        {
            if (commanderMove != null)
            {
                commanderMove.LivesChanged -= OnPlayerLivesChanged;
            }
        }

        private void OnPlayerLivesChanged(int current, int max)
        {
            lifeHud.SetLives(current, max);
        }

        private Transform ResolveHudRoot()
        {
            if (timerText != null)
            {
                return timerText.transform.parent;
            }

            if (killCountText != null)
            {
                return killCountText.transform.parent;
            }

            return statusText != null ? statusText.transform.parent : null;
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
            commanderMove?.SetInputEnabled(false);
            HideCardPanel();
            DemoChestQuizOverlay.HideActive();

            if (resultText != null)
            {
                resultText.gameObject.SetActive(false);
            }

            var result = new TreasureSpiritResult(isSuccess, killCount, timeRemaining, summary);
            if (isSuccess)
            {
                context?.Exit.Complete(result);
            }
            else
            {
                context?.Exit.Fail(result);
            }
        }

        private void OnExitButtonClicked()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            commanderMove?.SetInputEnabled(false);
            context?.Exit.Cancel();
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

        public void Shutdown()
        {
            ShutdownInternal();
            context = null;
            startData = null;
            difficultyMultiplier = 1f;
            timeRemaining = 0f;
            killCount = 0;
            Time.timeScale = 1f;
        }
    }
}
