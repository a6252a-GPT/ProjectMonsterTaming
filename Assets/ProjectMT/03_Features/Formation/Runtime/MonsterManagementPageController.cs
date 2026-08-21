using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectMT.Features.Quest;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Quest;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProjectMT.Features.Formation
{
    [DisallowMultipleComponent]
    public sealed class MonsterManagementPageController : MonoBehaviour // 몬스터 조회·성장 관리창
    {
        private const int PreviewLayer = 31; // 다른 전투 UI와 겹치지 않는 미리보기 전용 번호
        private const float BreakthroughMarkerOffsetY = -31f;

        [Header("Page")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button growthTabButton;
        [SerializeField] private Button breakthroughTabButton;
        [SerializeField] private Image growthTabBackground;
        [SerializeField] private Image growthTabInnerBorder;
        [SerializeField] private Image breakthroughTabBackground;
        [SerializeField] private Image breakthroughTabInnerBorder;
        [SerializeField] private TMP_Text growthTabLabel;
        [SerializeField] private TMP_Text breakthroughTabLabel;
        [SerializeField] private GameObject growthContent;
        [SerializeField] private GameObject breakthroughContent;

        [Header("Selected Monster")]
        [SerializeField] private TMP_Text selectedNameLabel;
        [SerializeField] private TMP_Text selectedLevelLabel;
        [SerializeField] private Image rarityBadge;
        [SerializeField] private Outline rarityBadgeOutline;
        [SerializeField] private TMP_Text rarityLabel;

        [Header("Growth Stats")]
        [SerializeField] private TMP_Text healthStatLabel;
        [SerializeField] private TMP_Text attackSpeedStatLabel;
        [SerializeField] private TMP_Text attackStatLabel;
        [SerializeField] private TMP_Text criticalStatLabel;
        [SerializeField] private TMP_Text defenseStatLabel;
        [SerializeField] private TMP_Text moveSpeedStatLabel;

        [Header("Growth Action")]
        [SerializeField] private TMP_Text nextLevelLabel;
        [SerializeField] private TMP_Text goldCostLabel;
        [SerializeField] private Button levelUpButton;
        [SerializeField] private TMP_Text levelUpButtonLabel;

        [Header("Roster")]
        [SerializeField] private TMP_Text rosterCountLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private MonsterRosterListView rosterList;

        [Header("Preview")]
        [SerializeField] private RawImage previewImage;
        [SerializeField] private GameObject previewPlaceholder;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Light previewLight;
        [SerializeField] private Transform previewAnchor;

        [Header("Breakthrough")]
        [SerializeField] private Button[] breakthroughStageButtons = Array.Empty<Button>();
        [SerializeField] private TMP_Text breakthroughStageTitle;
        [SerializeField] private TMP_Text breakthroughEffectLabel;
        [SerializeField] private TMP_Text breakthroughSequenceLabel;
        [SerializeField] private TMP_Text breakthroughProgressCaption;
        [SerializeField] private TMP_Text breakthroughProgressLabel;
        [SerializeField] private Button breakthroughActionButton;
        [SerializeField] private TMP_Text breakthroughActionLabel;

        [Header("Data")]
        [SerializeField] private MonsterRarityCatalog rarityCatalog;

        private readonly List<UnityAction> stageActions = new List<UnityAction>();
        private IGameProgressService progress;
        private MonsterCatalog catalog;
        private GameObject previewInstance;
        private string selectedMonsterId;
        private int selectedBreakthroughStage = 1;
        private bool showingBreakthrough;
        private bool isBusy;

        public event Action<bool> OpenStateChanged;
        public bool IsOpen => this != null && gameObject.activeInHierarchy;

        private void Awake()
        {
            closeButton?.onClick.AddListener(ClosePage);
            growthTabButton?.onClick.AddListener(ShowGrowthTab);
            breakthroughTabButton?.onClick.AddListener(ShowBreakthroughTab);
            levelUpButton?.onClick.AddListener(HandleLevelUpClicked);
            ConfigureStageActions();
            breakthroughActionButton?.onClick.AddListener(HandleBreakthroughClicked);
            var previewMask = 1 << PreviewLayer;
            if (previewCamera != null)
            {
                previewCamera.cullingMask = previewMask;
                previewCamera.clearFlags = CameraClearFlags.SolidColor;
                previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }

            if (previewLight != null)
            {
                previewLight.cullingMask = previewMask;
            }

        }

        private void Update()
        {
            if (IsOpen && previewInstance != null)
            {
                previewInstance.transform.Rotate(0f, 18f * Time.unscaledDeltaTime, 0f, Space.World);
            }
        }

        private void OnDisable()
        {
            ClearPreview();
        }

        private void OnDestroy()
        {
            Shutdown();
            closeButton?.onClick.RemoveListener(ClosePage);
            growthTabButton?.onClick.RemoveListener(ShowGrowthTab);
            breakthroughTabButton?.onClick.RemoveListener(ShowBreakthroughTab);
            levelUpButton?.onClick.RemoveListener(HandleLevelUpClicked);
            breakthroughActionButton?.onClick.RemoveListener(HandleBreakthroughClicked);
            RemoveStageActions();
        }

        public void Configure(IGameProgressService progressService, MonsterCatalog monsterCatalog)
        {
            if (progress != null)
            {
                progress.Changed -= HandleProgressChanged;
            }

            progress = progressService ?? throw new ArgumentNullException(nameof(progressService));
            catalog = monsterCatalog ?? throw new ArgumentNullException(nameof(monsterCatalog));
            progress.Changed += HandleProgressChanged;
            selectedMonsterId = SelectFirstOwnedMonster(progress.View.Monsters);
        }

        public void Shutdown()
        {
            if (progress != null)
            {
                progress.Changed -= HandleProgressChanged;
            }

            SetPageOpen(false, false);
            ClearPreview();
            progress = null;
            catalog = null;
            selectedMonsterId = null;
            isBusy = false;
            OpenStateChanged = null;
        }

        public void OpenPage()
        {
            if (progress == null || catalog == null || IsOpen)
            {
                return;
            }

            if (!progress.View.Monsters.Owns(selectedMonsterId))
            {
                selectedMonsterId = SelectFirstOwnedMonster(progress.View.Monsters);
            }

            showingBreakthrough = false;
            selectedBreakthroughStage = 0;
            SetPageOpen(true, true);
            SetStatus(string.Empty);
            RefreshView();
            rosterList?.ResetScrollPosition();
        }

        public void ClosePage()
        {
            SetPageOpen(false, true);
        }

        private void SetPageOpen(bool open, bool notify)
        {
            var wasOpen = IsOpen;
            if (open && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (previewCamera != null)
            {
                previewCamera.enabled = open;
            }

            if (previewLight != null)
            {
                previewLight.enabled = open;
            }

            if (!open)
            {
                ClearPreview();
            }

            var isNowOpen = open && gameObject.activeInHierarchy;
            if (notify && wasOpen != isNowOpen)
            {
                OpenStateChanged?.Invoke(isNowOpen);
            }

            if (!open && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void ShowGrowthTab()
        {
            if (!isBusy)
            {
                showingBreakthrough = false;
                RefreshView();
            }
        }

        private void ShowBreakthroughTab()
        {
            if (!isBusy)
            {
                showingBreakthrough = true;
                RefreshView();
            }
        }

        private void HandleCardSelected(string monsterId)
        {
            if (!isBusy && !string.IsNullOrWhiteSpace(monsterId))
            {
                selectedMonsterId = monsterId;
                selectedBreakthroughStage = 0;
                SetStatus(string.Empty);
                RefreshView();
            }
        }

        private async void HandleLevelUpClicked()
        {
            if (isBusy || progress == null ||
                !progress.View.Monsters.TryGetOwnedMonster(selectedMonsterId, out var owned) ||
                !MonsterLevelRules.TryGetNextLevelCost(owned.Level, out _))
            {
                return;
            }

            var saved = await ApplyAndSaveAsync(
                GameProgressChange.LevelUpMonster(selectedMonsterId, owned.Level),
                $"{ResolveDisplayName(selectedMonsterId)} 레벨업 완료");
            if (saved)
            {
                _ = QuestRuntime.AdvanceAllOfConditionAsync(QuestConditionType.MonsterLevelUp, 1L);
            }
        }

        private async void HandleBreakthroughClicked()
        {
            if (isBusy || progress == null ||
                !progress.View.Monsters.TryGetOwnedMonster(selectedMonsterId, out var owned) ||
                MonsterAscension.IsMaxAscension(owned.AscensionLevel) ||
                owned.AscensionMaterialCount <= 0 ||
                selectedBreakthroughStage != owned.AscensionLevel + 1)
            {
                return;
            }

            var nextStage = owned.AscensionLevel + 1;
            // 0으로 비워두면 갱신 시 다음 미완료 단계가 자동 선택돼 "돌파"가 계속 이어진다.
            selectedBreakthroughStage = 0;
            var saved = await ApplyAndSaveAsync(
                GameProgressChange.AscendMonster(selectedMonsterId, owned.AscensionLevel),
                $"{ResolveDisplayName(selectedMonsterId)} {nextStage}단계 돌파 완료");
            if (saved)
            {
                _ = QuestRuntime.AdvanceAllOfConditionAsync(QuestConditionType.MonsterAscension, 1L);
            }
        }

        private async Task<bool> ApplyAndSaveAsync(GameProgressChange change, string successMessage)
        {
            isBusy = true;
            SetControlsInteractable(false);
            SetStatus("저장 중...");
            var saved = false;
            try
            {
                saved = await progress.TryApplyAndSaveAsync(change);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                isBusy = false;
            }

            if (this == null)
            {
                return saved;
            }

            SetStatus(saved ? successMessage : "변경을 저장하지 못했습니다");
            if (IsOpen)
            {
                RefreshView();
            }

            return saved;
        }

        private void HandleProgressChanged()
        {
            if (this == null || progress == null)
            {
                return; // 파괴 예약 뒤 남은 이벤트 호출 무시
            }

            if (!isBusy && IsOpen)
            {
                if (!progress.View.Monsters.Owns(selectedMonsterId))
                {
                    selectedMonsterId = SelectFirstOwnedMonster(progress.View.Monsters);
                }

                RefreshView();
            }
        }

        private void RefreshView()
        {
            if (progress == null || catalog == null)
            {
                return;
            }

            UpdateTabState();
            RefreshRoster(progress.View.Monsters);
            RefreshSelectedDetails(progress.View);
        }

        private void UpdateTabState()
        {
            var inactiveBackground = new Color32(0x24, 0x24, 0x24, 0xFF);
            var inactiveLight = new Color32(0x31, 0x30, 0x31, 0xFF);
            var activeBackground = new Color32(0x5C, 0x7D, 0x8C, 0xFF);
            var activeLight = new Color32(0x73, 0x9A, 0xA5, 0xFF);

            growthContent?.SetActive(!showingBreakthrough);
            breakthroughContent?.SetActive(showingBreakthrough);
            SetColor(growthTabBackground, showingBreakthrough ? inactiveBackground : activeBackground);
            SetColor(growthTabInnerBorder, showingBreakthrough ? inactiveLight : activeLight);
            SetColor(breakthroughTabBackground, showingBreakthrough ? activeBackground : inactiveBackground);
            SetColor(breakthroughTabInnerBorder, showingBreakthrough ? activeLight : inactiveLight);
            SetColor(growthTabLabel, showingBreakthrough ? new Color32(0xB0, 0xAD, 0xAA, 0xFF) : Color.white);
            SetColor(breakthroughTabLabel, showingBreakthrough ? Color.white : new Color32(0xB0, 0xAD, 0xAA, 0xFF));
        }

        private void RefreshRoster(MonsterRosterView roster)
        {
            var owned = MonsterRosterCardSorter.CreateSorted(roster, rarityCatalog);
            SetText(rosterCountLabel, $"보유 {owned.Count} / {MonsterRosterListView.MaxCardCount}");
            var displayCount = rosterList != null ? rosterList.EnsureCardCount(owned.Count) : 0;
            var cards = rosterList?.Cards;
            for (var index = 0; index < displayCount; index++)
            {
                var card = cards?[index];
                if (card == null)
                {
                    continue;
                }

                var ownedMonster = owned[index];
                if (!catalog.TryGet(ownedMonster.MonsterId, out var definition))
                {
                    card.BindEmpty("등록 정보 없음");
                    continue;
                }

                card.BindMonster(
                    definition,
                    ownedMonster,
                    string.Equals(ownedMonster.MonsterId, selectedMonsterId, StringComparison.OrdinalIgnoreCase),
                    GetAssignmentLabel(roster, ownedMonster.MonsterId),
                    HandleCardSelected);
            }

            if (owned.Count > displayCount)
            {
                SetStatus($"현재 목록에는 앞의 {displayCount}마리만 표시됩니다.");
            }
        }

        private void RefreshSelectedDetails(GameProgressView view)
        {
            var roster = view.Monsters;
            if (!roster.TryGetOwnedMonster(selectedMonsterId, out var owned) ||
                !catalog.TryGet(selectedMonsterId, out var definition))
            {
                SetText(selectedNameLabel, "보유 몬스터 없음");
                SetText(selectedLevelLabel, string.Empty);
                SetText(rarityLabel, string.Empty);
                SetControlsInteractable(false);
                ClearPreview();
                return;
            }

            var rarity = MonsterRarity.Common;
            rarityCatalog?.TryGetRarity(selectedMonsterId, out rarity);
            SetText(selectedNameLabel, definition.DisplayName);
            SetText(selectedLevelLabel, $"Lv. {owned.Level}  ·  {(definition.Ranged ? "원거리" : "근거리")}");
            SetText(rarityLabel, GetRarityName(rarity));
            MonsterCardView.GetRarityPalette(rarity, out var rarityColor, out var rarityBorder, out _);
            SetColor(rarityBadge, rarityColor);
            SetOutlineColor(rarityBadgeOutline, rarityBorder);

            var currentMultiplier = MonsterLevelRules.GetStatMultiplier(owned.Level);
            var hasNextLevel = MonsterLevelRules.TryGetNextLevelCost(owned.Level, out var cost);
            var nextMultiplier = hasNextLevel
                ? MonsterLevelRules.GetStatMultiplier(owned.Level + 1)
                : currentMultiplier;
            SetStatComparison(healthStatLabel, definition.MaxHealth, currentMultiplier, nextMultiplier, "0.##");
            SetStatComparison(attackSpeedStatLabel, definition.AttackSpeed, currentMultiplier, nextMultiplier, "0.##");
            SetStatComparison(attackStatLabel, definition.AttackPower, currentMultiplier, nextMultiplier, "0.##");
            SetStatComparison(criticalStatLabel, "0%", "0%");
            SetStatComparison(defenseStatLabel, definition.Defense, currentMultiplier, nextMultiplier, "0.##");
            SetStatComparison(moveSpeedStatLabel, definition.MoveSpeed, currentMultiplier, nextMultiplier, "0.##");

            SetText(nextLevelLabel, hasNextLevel ? $"Lv. {owned.Level} → Lv. {owned.Level + 1}" : "최대 레벨");
            SetText(goldCostLabel, hasNextLevel
                ? $"필요 골드  {cost:N0}  /  보유 {view.Gold:N0}"
                : $"보유 골드  {view.Gold:N0}");
            SetText(levelUpButtonLabel, hasNextLevel ? "레벨업" : "최대 레벨");
            if (levelUpButton != null)
            {
                levelUpButton.interactable = !isBusy && hasNextLevel && view.Gold >= cost;
            }

            RefreshBreakthrough(owned);
            ShowPreview(definition);
            SetCardsInteractable(!isBusy);
            if (growthTabButton != null)
            {
                growthTabButton.interactable = !isBusy;
            }

            if (breakthroughTabButton != null)
            {
                breakthroughTabButton.interactable = !isBusy;
            }
        }

        private void RefreshBreakthrough(OwnedMonsterView owned)
        {
            var currentLevel = owned.AscensionLevel;
            var materialCount = owned.AscensionMaterialCount;
            currentLevel = Mathf.Clamp(currentLevel, 0, MonsterAscension.MaxAscensionLevel);
            materialCount = Mathf.Max(0, materialCount);
            var nextStage = Mathf.Clamp(currentLevel + 1, 1, MonsterAscension.MaxAscensionLevel);
            var maximumSelectableStage = Mathf.Max(nextStage, currentLevel);
            if (selectedBreakthroughStage < 1 || selectedBreakthroughStage > maximumSelectableStage)
            {
                selectedBreakthroughStage = nextStage;
            }
            for (var index = 0; index < breakthroughStageButtons.Length; index++)
            {
                var button = breakthroughStageButtons[index];
                if (button == null)
                {
                    continue;
                }

                var stage = index + 1;
                var completed = stage <= currentLevel;
                var isNext = stage == currentLevel + 1;
                button.interactable = !isBusy && stage <= Mathf.Min(currentLevel + 1, MonsterAscension.MaxAscensionLevel);
                SetColor(button.targetGraphic,
                    completed
                        ? new Color32(0xCA, 0xB0, 0x46, 0xFF)
                        : isNext
                            ? new Color32(0x39, 0x76, 0xC9, 0xFF)
                            : new Color32(0x45, 0x45, 0x48, 0xFF));
            }

            RefreshBreakthroughStageMarkers(currentLevel);

            SetText(breakthroughStageTitle, $"{selectedBreakthroughStage}단계 돌파");
            SetText(breakthroughEffectLabel, GetBreakthroughEffect(selectedBreakthroughStage));
            SetText(breakthroughSequenceLabel,
                selectedBreakthroughStage <= currentLevel
                    ? "완료한 돌파 단계입니다."
                    : selectedBreakthroughStage == currentLevel + 1
                        ? materialCount > 0
                            ? "중복 재료 1개를 사용해 돌파할 수 있습니다."
                            : "같은 몬스터를 다시 획득하면 돌파 재료가 쌓입니다."
                        : "앞 단계를 먼저 완료해야 합니다.");
            SetText(breakthroughProgressCaption, "중복 재료");
            SetText(breakthroughProgressLabel, $"보유 {materialCount}개");
            SetText(breakthroughActionLabel,
                MonsterAscension.IsMaxAscension(currentLevel)
                    ? "최대 돌파"
                    : selectedBreakthroughStage <= currentLevel
                        ? "완료"
                        : selectedBreakthroughStage == currentLevel + 1
                            ? materialCount > 0 ? "돌파" : "중복 재료 필요"
                            : "선행 돌파 필요");
            if (breakthroughActionButton != null)
            {
                breakthroughActionButton.interactable = !isBusy &&
                                                         !MonsterAscension.IsMaxAscension(currentLevel) &&
                                                         selectedBreakthroughStage == currentLevel + 1 &&
                                                         materialCount > 0;
            }
        }

        private void RefreshBreakthroughStageMarkers(int currentLevel)
        {
            if (breakthroughStageButtons.Length == 0 || breakthroughStageButtons[0] == null)
            {
                return;
            }

            var nodeRoot = breakthroughStageButtons[0].transform.parent;
            if (nodeRoot == null)
            {
                return;
            }

            var completedMarker = nodeRoot.Find("CompleteMark")?.GetComponent<TMP_Text>();
            var incompleteMarker = nodeRoot.Find("NextMark")?.GetComponent<TMP_Text>();
            UpdateBreakthroughStageMarker(
                completedMarker,
                currentLevel,
                "완료",
                new Color32(0xD8, 0xC1, 0x89, 0xFF));
            UpdateBreakthroughStageMarker(
                incompleteMarker,
                currentLevel < MonsterAscension.MaxAscensionLevel ? currentLevel + 1 : 0,
                "미완료",
                new Color32(0xC9, 0xC3, 0xB8, 0xFF));
        }

        private void UpdateBreakthroughStageMarker(
            TMP_Text marker,
            int stage,
            string label,
            Color color)
        {
            if (marker == null)
            {
                return;
            }

            var hasTarget = stage >= 1 &&
                            stage <= breakthroughStageButtons.Length &&
                            breakthroughStageButtons[stage - 1] != null;
            marker.gameObject.SetActive(hasTarget);
            if (!hasTarget)
            {
                return;
            }

            var targetRect = breakthroughStageButtons[stage - 1].transform as RectTransform;
            if (targetRect != null)
            {
                marker.rectTransform.anchoredPosition =
                    targetRect.anchoredPosition + new Vector2(0f, BreakthroughMarkerOffsetY);
            }

            SetText(marker, label);
            SetColor(marker, color);
        }

        private void SelectBreakthroughStage(int stage)
        {
            if (isBusy || progress == null ||
                !progress.View.Monsters.TryGetOwnedMonster(selectedMonsterId, out var owned) ||
                stage > Mathf.Min(owned.AscensionLevel + 1, MonsterAscension.MaxAscensionLevel))
            {
                return;
            }

            selectedBreakthroughStage = stage;
            RefreshBreakthrough(owned);
        }

        private void ConfigureStageActions()
        {
            RemoveStageActions();
            for (var index = 0; index < breakthroughStageButtons.Length; index++)
            {
                var stage = index + 1;
                UnityAction action = () => SelectBreakthroughStage(stage);
                stageActions.Add(action);
                breakthroughStageButtons[index]?.onClick.AddListener(action);
            }
        }

        private void RemoveStageActions()
        {
            for (var index = 0; index < stageActions.Count && index < breakthroughStageButtons.Length; index++)
            {
                breakthroughStageButtons[index]?.onClick.RemoveListener(stageActions[index]);
            }

            stageActions.Clear();
        }

        private void ShowPreview(MonsterDefinition definition)
        {
            ClearPreview();
            var canShow = IsOpen && definition != null && definition.PreviewPrefab != null && previewAnchor != null;
            previewPlaceholder?.SetActive(!canShow);
            if (!canShow)
            {
                return;
            }

            previewInstance = Instantiate(definition.PreviewPrefab, previewAnchor);
            previewInstance.name = $"Preview_{definition.MonsterId}";
            previewInstance.transform.SetPositionAndRotation(previewAnchor.position, Quaternion.Euler(0f, 180f, 0f));
            SetLayerRecursively(previewInstance, PreviewLayer);
            ApplyPreviewTint(previewInstance, definition.VisualTint);
            DisablePreviewGameplay(previewInstance);
            FitPreviewCamera(previewInstance);
        }

        private void ClearPreview()
        {
            if (previewInstance != null)
            {
                Destroy(previewInstance);
                previewInstance = null;
            }
        }

        private void FitPreviewCamera(GameObject target)
        {
            if (previewCamera == null || target == null)
            {
                return;
            }

            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            var targetCenter = previewAnchor.position;
            target.transform.position += targetCenter - bounds.center;
            var radius = Mathf.Max(0.5f, bounds.extents.magnitude);
            var distance = Mathf.Max(1.8f, radius / Mathf.Tan(previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.25f);
            previewCamera.transform.position = targetCenter + new Vector3(0f, radius * 0.15f, -distance);
            previewCamera.transform.LookAt(targetCenter);
            previewCamera.nearClipPlane = 0.05f;
            previewCamera.farClipPlane = Mathf.Max(20f, distance + radius * 4f);
        }

        private void SetControlsInteractable(bool interactable)
        {
            if (levelUpButton != null)
            {
                levelUpButton.interactable = interactable;
            }

            if (growthTabButton != null)
            {
                growthTabButton.interactable = interactable;
            }

            if (breakthroughTabButton != null)
            {
                breakthroughTabButton.interactable = interactable;
            }

            if (breakthroughActionButton != null)
            {
                breakthroughActionButton.interactable = interactable;
            }

            SetCardsInteractable(interactable);
        }

        private void SetCardsInteractable(bool interactable)
        {
            rosterList?.SetCardsInteractable(interactable);
        }

        private static void DisablePreviewGameplay(GameObject root)
        {
            foreach (var behaviour in root.GetComponentsInChildren<Behaviour>(true))
            {
                behaviour.enabled = false;
            }

            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
            {
                body.detectCollisions = false;
                body.isKinematic = true;
            }
        }

        private static void ApplyPreviewTint(GameObject root, Color tint)
        {
            foreach (var feedback in root.GetComponentsInChildren<UnitVisualFeedback>(true))
            {
                feedback.SetTint(tint);
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static string GetAssignmentLabel(MonsterRosterView roster, string monsterId)
        {
            if (Contains(roster.MainPartySlots, monsterId))
            {
                return "본대";
            }

            return Contains(roster.ReservePartySlots, monsterId) ? "예비" : string.Empty;
        }

        private static bool Contains(IReadOnlyList<string> values, string monsterId)
        {
            for (var index = 0; index < values.Count; index++)
            {
                if (string.Equals(values[index], monsterId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string SelectFirstOwnedMonster(MonsterRosterView roster)
        {
            return roster.OwnedMonsterIds.Count > 0 ? roster.OwnedMonsterIds[0] : string.Empty;
        }

        private string ResolveDisplayName(string monsterId)
        {
            return catalog != null && catalog.TryGet(monsterId, out var definition)
                ? definition.DisplayName
                : monsterId;
        }

        private static string GetRarityName(MonsterRarity rarity)
        {
            return rarity switch
            {
                MonsterRarity.Rare => "희귀",
                MonsterRarity.Epic => "영웅",
                MonsterRarity.Legendary => "전설",
                MonsterRarity.Mythic => "신화",
                _ => "일반"
            };
        }

        private static string GetBreakthroughEffect(int stage)
        {
            if (MonsterAscension.IsStatBoostMilestone(stage))
            {
                return "전체 기본 능력치 +10%";
            }

            return MonsterAscension.IsSpecialEffectMilestone(stage)
                ? "전용 특수 효과 해금 (효과 미정)"
                : "돌파 효과 정보 없음";
        }

        private static void SetStatComparison(
            TMP_Text target,
            float baseValue,
            float currentMultiplier,
            float nextMultiplier,
            string format)
        {
            SetStatComparison(
                target,
                (baseValue * currentMultiplier).ToString(format),
                (baseValue * nextMultiplier).ToString(format));
        }

        private static void SetStatComparison(
            TMP_Text target,
            string currentValue,
            string nextValue)
        {
            if (target != null)
            {
                target.richText = true;
            }

            SetText(target,
                $"<color=#F1EBDD>{currentValue}</color> " +
                $"<color=#D8C07A>→</color> " +
                $"<color=#A6C46E>{nextValue}</color>");
        }

        private void SetStatus(string message)
        {
            SetText(statusLabel, message);
            if (statusLabel != null)
            {
                statusLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }

        private static void SetColor(Graphic target, Color color)
        {
            if (target != null)
            {
                target.color = color;
            }
        }

        private static void SetOutlineColor(Outline target, Color color)
        {
            if (target != null)
            {
                target.effectColor = color;
            }
        }
    }
}
