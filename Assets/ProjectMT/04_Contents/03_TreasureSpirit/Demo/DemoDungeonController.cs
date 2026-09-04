using System;
using System.Collections;
using System.Collections.Generic;
using ProjectMT.Contents.Framework;
using ProjectMT.Contents.TreasureSpirit;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    /// <summary>
    /// 베이크 던전 데모 컨트롤러. 성장 던전 단계에 맞는 베이크 맵을 로드합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoDungeonController : MonoBehaviour, IContentController
    {
        [Header("베이크 맵")]
        [SerializeField] private BakedDungeonLoader bakedDungeonLoader;

        [Header("전투 / 유닛")]
        [SerializeField] private GameObject commanderRoot;
        [SerializeField] private PlayerCharacterController commanderMove;
        [SerializeField] private MonsterCatalog monsterCatalog;
        [SerializeField] private CombatWorld combatWorld;
        [SerializeField] private GameObject followerPrefab;
        [SerializeField] private float followerVisualScale = 1f;

        [Header("HUD")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private GrowthDungeonHudView growthHud;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text killCountText;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button exitButton;
        [SerializeField] private GameObject cardPanel;
        [SerializeField] private ItemDefinition keyItemDefinition;
        [SerializeField] private GameObject contentResultOverlayPrefab; // FoodRiot과 같은 PF_ContentResultOverlay
        [SerializeField] private TreasureSpiritResultAdapter resultAdapter;
        [SerializeField] private Sprite iceSkillIcon;

        [Header("던전 설정")]
        [SerializeField] private float timeLimitSeconds = 100f;
        [SerializeField] private float resultTextHideDelay = 20f;
        [SerializeField] private float keyCardHideDelay = 10f;

        private const string ContentDisplayName = "보물 정령 숨바꼭질";

        private ContentContext context;
        private TreasureSpiritStartData startData;
        private float difficultyMultiplier = 1f;
        private float timeRemaining;
        private int killCount;
        private BakedDungeonLoader keyState;
        private Coroutine resultTextHideRoutine;
        private Coroutine keyCardHideRoutine;
        private Coroutine localResultOverlayRoutine;
        private GameObject localResultOverlayInstance;
        private readonly DemoLifeHud lifeHud = new DemoLifeHud();
        private DemoJumpButton jumpButton;
        private readonly List<GameObject> spawnedFollowers = new List<GameObject>();

        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }
        public static DemoDungeonController Active { get; private set; }
        public static bool IsGameplayPaused => Active != null && Active.IsPaused;
        public Transform PlayerTransform => commanderMove != null
            ? commanderMove.transform
            : commanderRoot != null ? commanderRoot.transform : null;
        public CombatWorld CombatWorld => combatWorld;
        public Transform ActiveMapRoot => bakedDungeonLoader != null && bakedDungeonLoader.ActiveMapInstance != null
            ? bakedDungeonLoader.ActiveMapInstance.transform
            : null;

        private int displayedSeconds = int.MinValue;
        private int displayedKills = int.MinValue;
        private int displayedHasKey = -1;

        private void Awake()
        {
            BindExitButton();
        }

        private void OnEnable()
        {
            Active = this;
        }

        private void OnDisable()
        {
            if (Active == this)
            {
                Active = null;
            }
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
            growthHud?.SetStage(stage);

            bakedDungeonLoader.LoadMapForStage(stage);
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

            DungeonFogInitializer.RevealPlayerArea(
                mapInstance.transform,
                commanderRoot != null ? commanderRoot.transform : null);

            SpawnFollowers();
            bakedDungeonLoader.SpawnRoomContents(difficultyMultiplier);
            bakedDungeonLoader.SpawnEndRoomPrison(this);

            commanderMove?.SetInputEnabled(true);
            BindLifeHud();

            timeRemaining = startData != null ? startData.DurationSeconds : timeLimitSeconds;
            killCount = 0;
            IsRunning = true;
            SetGameplayPaused(false);
            DemoDungeonAudio.Active?.StartBeds();
            displayedSeconds = int.MinValue;
            displayedKills = int.MinValue;
            displayedHasKey = -1;

            UpdateHud();
            EnsureCardPanel();
            HideCardPanel();
            BeginResultTextAutoHide();
        }

        private void SpawnFollowers()
        {
            Transform commander = commanderMove != null
                ? commanderMove.transform
                : commanderRoot != null ? commanderRoot.transform : null;
            if (commander == null)
            {
                Debug.LogError("[DemoDungeonController] 군단장이 없어 팔로워를 스폰할 수 없습니다.");
                return;
            }

            DemoPartyFollowerSpawner.Spawn(
                commander,
                startData.Party,
                monsterCatalog,
                combatWorld,
                followerPrefab,
                spawnedFollowers,
                followerVisualScale);
        }

        public void SetGameplayPaused(bool paused)
        {
            IsPaused = paused && IsRunning;
            combatWorld?.SetPaused(IsPaused);
        }

        private void ShutdownInternal()
        {
            IsRunning = false;
            IsPaused = false;
            combatWorld?.SetPaused(false);
            StopAllCoroutines();
            UnbindLifeHud();
            commanderMove?.SetInputEnabled(false);
            DemoPartyFollowerSpawner.Despawn(combatWorld, spawnedFollowers);

            if (keyState != null)
            {
                keyState.KeyGranted -= OnKeyGranted;
            }

            DemoDungeonAudio.Active?.StopBeds();
            DemoCombatRoster.Clear();
            DemoArrowProjectile.ClearPool();
            DemoIceArrowProjectile.ClearPool();
            bakedDungeonLoader?.ClearMap();
            DemoDungeonAtmosphere.Restore();
            keyState = null;
            HideCardPanel();
            DemoChestQuizOverlay.HideActive();
            HideLocalResultOverlay();
            lifeHud.Hide();
            jumpButton?.Hide();

            if (commanderRoot != null)
            {
                commanderRoot.SetActive(false);
            }
        }

        private void Update()
        {
            if (!IsRunning || IsPaused)
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
            Vector3 keyPosition = commanderMove != null ? commanderMove.transform.position : transform.position;
            DemoDungeonAudio.PlayKey(keyPosition);
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
            if (growthHud != null)
            {
                growthHud.SetTimer(timeRemaining);
                growthHud.SetObjective($"감옥 열쇠 {(keyState != null && keyState.HasKey ? 1 : 0)} / 1");
                growthHud.SetAuxiliary(killCount.ToString());
                return;
            }
            int seconds = Mathf.CeilToInt(timeRemaining);
            if (timerText != null && seconds != displayedSeconds)
            {
                displayedSeconds = seconds;
                timerText.text = $"남은 시간: {seconds}초";
            }

            int hasKey = keyState != null && keyState.HasKey ? 1 : 0;
            if (statusText != null && hasKey != displayedHasKey)
            {
                displayedHasKey = hasKey;
                statusText.text = hasKey == 1 ? "열쇠 획득: O" : "열쇠 획득: X";
            }

            if (killCountText != null && killCount != displayedKills)
            {
                displayedKills = killCount;
                killCountText.text = $"처치 : {killCount}";
            }
        }

        private void BindLifeHud()
        {
            Transform hudRoot = ResolveHudRoot();
            lifeHud.Ensure(hudRoot);
            lifeHud.Show();
            jumpButton = DemoJumpButton.Ensure(hudRoot, commanderMove, iceSkillIcon);
            jumpButton?.BindAutomap(
                bakedDungeonLoader != null && bakedDungeonLoader.ActiveMapInstance != null
                    ? bakedDungeonLoader.ActiveMapInstance.transform
                    : null);
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
            jumpButton?.HideAutomap();
            if (resultText != null)
            {
                resultText.text = string.Empty;
            }

            var result = new TreasureSpiritResult(
                isSuccess,
                killCount,
                timeRemaining,
                summary,
                isSuccess ? resultAdapter?.CapturedMonsterId : null);
            if (isSuccess)
            {
                DemoDungeonAudio.PlayClear();
            }
            else
            {
                DemoDungeonAudio.PlayFail();
            }

            if (ShouldShowLocalResultOverlay())
            {
                if (localResultOverlayRoutine != null)
                {
                    StopCoroutine(localResultOverlayRoutine);
                }

                localResultOverlayRoutine = StartCoroutine(ShowSharedResultOverlayThenExit(isSuccess, result));
                return;
            }

            SubmitExit(isSuccess, result);
        }

        private IEnumerator ShowSharedResultOverlayThenExit(bool isSuccess, TreasureSpiritResult result)
        {
            HideLocalResultOverlay();
            if (contentResultOverlayPrefab == null)
            {
                Debug.LogError("Treasure Spirit content result overlay prefab is missing.");
                SubmitExit(isSuccess, result);
                localResultOverlayRoutine = null;
                yield break;
            }

            localResultOverlayInstance = Instantiate(contentResultOverlayPrefab);
            var view = localResultOverlayInstance.GetComponentInChildren<IContentResultView>(true);
            if (view == null)
            {
                Debug.LogError("Treasure Spirit content result overlay is not an IContentResultView.");
                HideLocalResultOverlay();
                SubmitExit(isSuccess, result);
                localResultOverlayRoutine = null;
                yield break;
            }

            var runInfo = context != null
                ? context.RunInfo
                : new ContentRunInfo(
                    new ContentId(GrowthDungeonProgressIds.TreasureSpirit),
                    "1",
                    ContentRunMode.SeedTest);
            var outcome = isSuccess ? ContentOutcome.Complete : ContentOutcome.Fail;
            RewardPresentationRequest rewards = null;
            if (resultAdapter != null)
            {
                resultAdapter.TryCreateRewardPresentation(result, default, runInfo, null, out rewards);
            }

            var task = view.ShowAsync(
                new ContentResultPresentation(
                    runInfo.ContentId,
                    ContentDisplayName,
                    outcome,
                    resultAdapter != null
                        ? resultAdapter.CreateResultSummary(result, runInfo, outcome)
                        : result.Message,
                    rewards));
            while (!task.IsCompleted)
            {
                yield return null;
            }

            HideLocalResultOverlay();
            SubmitExit(isSuccess, result);
            localResultOverlayRoutine = null;
        }

        private void HideLocalResultOverlay()
        {
            if (localResultOverlayInstance == null)
            {
                return;
            }

            Destroy(localResultOverlayInstance);
            localResultOverlayInstance = null;
        }

        private void SubmitExit(bool isSuccess, TreasureSpiritResult result)
        {
            if (isSuccess)
            {
                context?.Exit.Complete(result);
            }
            else
            {
                context?.Exit.Fail(result);
            }
        }

        private bool ShouldShowLocalResultOverlay()
        {
            return context == null || context.Exit is DebugContentExit;
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
        }
    }
}
