using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectMT.Features.Quest;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Quest;
using ProjectMT.Shared.UI;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Formation
{
    [DisallowMultipleComponent]
    public sealed class FormationPageController : MonoBehaviour // 보유 목록과 편성을 한 화면에서 관리
    {
        private const int PreviewLayer = 5; // Unity 기본 UI 레이어만 미리보기 카메라에 노출

        [Header("Page")]
        [SerializeField] private GameObject pageRoot;
        [SerializeField] private Button openButton;
        [SerializeField] private bool showStandaloneOpenButton = true;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button mainTabButton;
        [SerializeField] private Button reserveTabButton;
        [SerializeField] private TMP_Text mainTabLabel;
        [SerializeField] private TMP_Text reserveTabLabel;

        [Header("Selected Monster")]
        [SerializeField] private TMP_Text selectedNameLabel;
        [SerializeField] private TMP_Text selectedLevelLabel;
        [SerializeField] private TMP_Text selectedStatsLabel;
        [SerializeField] private TMP_Text currencyLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Button levelUpButton;
        [SerializeField] private TMP_Text levelUpButtonLabel;
        [SerializeField] private Button formationButton;
        [SerializeField] private TMP_Text formationButtonLabel;
        [SerializeField] private Button positionFormationButton;

        [Header("Cards")]
        [SerializeField] private Transform formationSlotsRoot;
        [SerializeField] private MonsterRosterListView ownedRosterList;
        [SerializeField] private MonsterCardView cardPrefab;
        [SerializeField] private TMP_Text ownedCountLabel;
        [SerializeField] private TMP_Text capacityLabel;
        [SerializeField] private TMP_Text formationGuideLabel;

        [Header("Preview")]
        [SerializeField] private RawImage previewImage;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Light previewLight;
        [SerializeField] private Transform previewAnchor;
        [SerializeField] private Transform formationPreviewSlotsRoot;
        [SerializeField] private Material activeSlotMaterial;
        [SerializeField] private Material lockedSlotMaterial;
        [SerializeField] private Color mainPartyRingColor = new Color32(231, 190, 94, 255);
        [SerializeField] private Color reservePartyRingColor = new Color32(79, 189, 184, 255);
        [SerializeField] private Camera worldCamera;

        private readonly List<MonsterCardView> formationCards = new List<MonsterCardView>();
        private readonly List<Transform> formationPreviewSlots = new List<Transform>();
        private readonly List<MonsterPreviewPresentation> formationPreviewInstances = new List<MonsterPreviewPresentation>();
        private MaterialPropertyBlock ringProperties;
        private IGameProgressService progress;
        private MonsterCatalog catalog;
        private Func<BattlePartySnapshot> refreshParty;
        private MonsterPreviewPresentation preview;
        private string selectedMonsterId;
        private MonsterPartyKind activeParty = MonsterPartyKind.Main;
        private bool isBusy;
        private bool questFormationCardExplicitlySelected;
        private Color mainTabActiveColor;
        private Color reserveTabInactiveColor;
        private bool tabColorsCaptured;

        public event Action<BattlePartySnapshot> PartyChanged;
        public event Action<bool> OpenStateChanged;
        public event Action PositionFormationRequested;
        public bool IsOpen => pageRoot != null && pageRoot.activeSelf;
        public bool HasQuestFormationCardSelection => questFormationCardExplicitlySelected;
        public Button QuestFormationActionButton => formationButton;

        public Button QuestFormationCandidateButton
        {
            get
            {
                var cards = ownedRosterList?.Cards;
                if (cards == null)
                {
                    return null;
                }

                for (var index = 0; index < cards.Count; index++)
                {
                    var card = cards[index];
                    if (card != null && card.gameObject.activeInHierarchy && !card.IsAssigned &&
                        card.ClickButton != null && card.ClickButton.interactable)
                    {
                        return card.ClickButton;
                    }
                }

                for (var index = 0; index < cards.Count; index++)
                {
                    var card = cards[index];
                    if (card != null && card.gameObject.activeInHierarchy && card.ClickButton != null &&
                        card.ClickButton.interactable)
                    {
                        return card.ClickButton;
                    }
                }

                return null;
            }
        }

        private void Awake()
        {
            openButton?.onClick.AddListener(OpenPage);
            closeButton?.onClick.AddListener(ClosePage);
            mainTabButton?.onClick.AddListener(SelectMainTab);
            reserveTabButton?.onClick.AddListener(SelectReserveTab);
            levelUpButton?.onClick.AddListener(HandleLevelUpClicked);
            formationButton?.onClick.AddListener(HandleFormationClicked);
            positionFormationButton?.onClick.AddListener(HandlePositionFormationClicked);
            CacheFormationPreviewSlots();
            if (formationGuideLabel == null && pageRoot != null)
            {
                foreach (var label in pageRoot.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (label.name != "Guide") continue;
                    formationGuideLabel = label;
                    break;
                }
            }
            CaptureTabColors();
            SetPageOpen(false);
        }

        private void Update()
        {
            if (!IsOpen) return;
            preview?.Tick(Time.unscaledDeltaTime);
            foreach (var instance in formationPreviewInstances) instance?.Tick(Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            if (IsOpen)
            {
                SetPageOpen(false);
            }
        }

        private void OnDestroy()
        {
            Shutdown();
            openButton?.onClick.RemoveListener(OpenPage);
            closeButton?.onClick.RemoveListener(ClosePage);
            mainTabButton?.onClick.RemoveListener(SelectMainTab);
            reserveTabButton?.onClick.RemoveListener(SelectReserveTab);
            levelUpButton?.onClick.RemoveListener(HandleLevelUpClicked);
            formationButton?.onClick.RemoveListener(HandleFormationClicked);
            positionFormationButton?.onClick.RemoveListener(HandlePositionFormationClicked);
        }

        public void Configure(
            IGameProgressService progressService,
            MonsterCatalog monsterCatalog,
            Func<BattlePartySnapshot> partyRefresh)
        {
            if (progress != null)
            {
                progress.Changed -= HandleProgressChanged;
            }

            progress = progressService ?? throw new ArgumentNullException(nameof(progressService));
            catalog = monsterCatalog ?? throw new ArgumentNullException(nameof(monsterCatalog));
            refreshParty = partyRefresh ?? throw new ArgumentNullException(nameof(partyRefresh));
            progress.Changed += HandleProgressChanged;
            selectedMonsterId = SelectFirstOwnedMonster(progress.View.Monsters);
            if (openButton != null)
            {
                openButton.interactable = true;
            }
        }

        public void Shutdown()
        {
            if (progress != null)
            {
                progress.Changed -= HandleProgressChanged;
            }

            SetPageOpen(false);
            ClearPreview();
            ClearFormationPreview();
            progress = null;
            catalog = null;
            refreshParty = null;
            selectedMonsterId = null;
            isBusy = false;
            PartyChanged = null;
            PositionFormationRequested = null;
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

            SetPageOpen(true);
            questFormationCardExplicitlySelected = false;
            SetStatus(string.Empty);
            RefreshView();
            ownedRosterList?.ResetScrollPosition();
        }

        public void ClosePage()
        {
            SetPageOpen(false);
        }

        private void SetPageOpen(bool open)
        {
            var wasOpen = IsOpen;
            if (pageRoot != null)
            {
                if (open)
                {
                    // 이 페이지는 군단장 3D 프리뷰(발 IK 고정)를 포함하므로 스케일/이동 없는
                    // FadeOnly를 쓴다. 자세한 이유는 UIPanelPopStyle.FadeOnly 주석 참고.
                    UIPanelPopAnimator.RequestOpen(pageRoot, UIPanelPopStyle.FadeOnly);

                    // PageRoot가 기본 비활성이라 Awake() 시점에 EnsureOn을 부르면 초기화가 미뤄진다.
                    // PageRoot가 막 활성화된 직후인 여기서 붙여야 클릭 연출이 확실히 동작한다.
                    UIButtonClickPunch.EnsureOn(formationButton?.gameObject);
                    UIButtonClickPunch.EnsureOn(positionFormationButton?.gameObject);
                    UIButtonClickSound.EnsureOn(formationButton?.gameObject);
                    UIButtonClickSound.EnsureOn(positionFormationButton?.gameObject);
                }
                else
                {
                    UIPanelPopAnimator.RequestClose(pageRoot);
                }
            }

            if (openButton != null)
            {
                openButton.gameObject.SetActive(showStandaloneOpenButton && !open);
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
                ClearFormationPreview();
            }

            if (wasOpen != open)
            {
                OpenStateChanged?.Invoke(open);
            }
        }

        private void SelectMainTab()
        {
            if (!isBusy)
            {
                activeParty = MonsterPartyKind.Main;
                RefreshView();
            }
        }

        private void SelectReserveTab()
        {
            if (!isBusy)
            {
                activeParty = MonsterPartyKind.Reserve;
                RefreshView();
            }
        }

        private void HandleCardSelected(string monsterId)
        {
            if (!isBusy && !string.IsNullOrWhiteSpace(monsterId))
            {
                selectedMonsterId = monsterId;
                questFormationCardExplicitlySelected = true;
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

        private async void HandleFormationClicked()
        {
            if (isBusy || progress == null || string.IsNullOrEmpty(selectedMonsterId))
            {
                return;
            }

            var roster = progress.View.Monsters;
            var isInActiveParty = TryGetAssignment(roster, selectedMonsterId, out var assignedParty, out _) &&
                                  assignedParty == activeParty;
            var change = isInActiveParty
                ? GameProgressChange.UnassignMonster(selectedMonsterId)
                : GameProgressChange.AssignMonster(selectedMonsterId, activeParty);
            var successMessage = isInActiveParty
                ? "편성 해제 완료 · 다음 전투부터 적용"
                : $"{GetPartyName(activeParty)} 편성 완료 · 다음 전투부터 적용";
            var saved = await ApplyAndSaveAsync(change, successMessage);
            if (saved && !isInActiveParty)
            {
                _ = QuestRuntime.AdvanceAllOfConditionAsync(QuestConditionType.MonsterFormation, 1L);
            }
        }

        private void HandlePositionFormationClicked()
        {
            if (!isBusy && progress != null && IsOpen && activeParty == MonsterPartyKind.Main)
            {
                PositionFormationRequested?.Invoke();
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
                if (saved)
                {
                    var snapshot = refreshParty();
                    PartyChanged?.Invoke(snapshot);
                }
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

            var view = progress.View;
            var roster = view.Monsters;
            UpdateTabState(roster);
            if (formationPreviewSlotsRoot != null)
            {
                RefreshFormationPreview(roster);
            }
            else
            {
                RefreshFormationCards(roster);
            }

            RefreshOwnedCards(roster);
            RefreshSelectedDetails(view);
            if (pageRoot != null)
            {
                UIButtonClickPunch.ApplyToAllButtonsUnder(pageRoot.transform); // 새로 생성된 몬스터 카드 버튼도 포함
                UIButtonClickSound.ApplyToAllButtonsUnder(pageRoot.transform);
            }
        }

        private void UpdateTabState(MonsterRosterView roster)
        {
            var mainSlots = roster.MainPartySlots;
            var reserveSlots = roster.ReservePartySlots;
            SetText(mainTabLabel, $"본부대 {CountAssigned(mainSlots)} / {MonsterRosterData.MainPartySlotCount}");
            SetText(reserveTabLabel, $"예비부대 {CountAssigned(reserveSlots)} / {MonsterRosterData.ReservePartySlotCount}");

            var activeSlots = activeParty == MonsterPartyKind.Main ? mainSlots : reserveSlots;
            var maximum = activeParty == MonsterPartyKind.Main
                ? MonsterRosterData.MainPartySlotCount
                : MonsterRosterData.ReservePartySlotCount;
            SetText(capacityLabel, $"{GetPartyName(activeParty)} 편집 · {CountAssigned(activeSlots)} / {maximum}");
            SetText(formationGuideLabel, "금색 원: 본부대 5 · 청록색 원: 예비 3 · 탭으로 편집 부대 선택");
            SetTabVisual(mainTabButton, activeParty == MonsterPartyKind.Main);
            SetTabVisual(reserveTabButton, activeParty == MonsterPartyKind.Reserve);
            if (positionFormationButton != null)
            {
                positionFormationButton.gameObject.SetActive(activeParty == MonsterPartyKind.Main);
                positionFormationButton.interactable = !isBusy;
            }
        }

        private void RefreshFormationCards(MonsterRosterView roster)
        {
            var slots = activeParty == MonsterPartyKind.Main ? roster.MainPartySlots : roster.ReservePartySlots;
            EnsureCardCount(formationCards, slots.Count, formationSlotsRoot);
            for (var index = 0; index < formationCards.Count; index++)
            {
                var card = formationCards[index];
                var visible = index < slots.Count;
                card.gameObject.SetActive(visible);
                if (!visible)
                {
                    formationPreviewInstances[index]?.Dispose();
                    formationPreviewInstances[index] = null;
                    continue;
                }

                var monsterId = slots[index];
                if (!string.IsNullOrEmpty(monsterId) && catalog.TryGet(monsterId, out var definition) &&
                    roster.TryGetOwnedMonster(monsterId, out var owned))
                {
                    card.BindMonster(
                        definition,
                        owned,
                        string.Equals(monsterId, selectedMonsterId, StringComparison.OrdinalIgnoreCase),
                        $"{GetPartyName(activeParty)} {index + 1}",
                        HandleCardSelected);
                }
                else
                {
                    card.BindEmpty($"{index + 1}번 빈 슬롯");
                }
            }
        }

        private void RefreshOwnedCards(MonsterRosterView roster)
        {
            var ownedMonsters = MonsterRosterCardSorter.CreateSorted(
                roster,
                cardPrefab != null ? cardPrefab.RarityCatalog : null);
            SetText(ownedCountLabel, $"보유 {ownedMonsters.Count} / {MonsterRosterListView.MaxCardCount}");
            var displayCount = ownedRosterList != null
                ? ownedRosterList.EnsureCardCount(ownedMonsters.Count)
                : 0;
            var cards = ownedRosterList?.Cards;
            for (var index = 0; index < displayCount; index++)
            {
                var card = cards?[index];
                if (card == null)
                {
                    continue;
                }

                var owned = ownedMonsters[index];
                if (!catalog.TryGet(owned.MonsterId, out var definition))
                {
                    card.BindEmpty("등록 정보 없음");
                    continue;
                }

                TryGetAssignment(roster, owned.MonsterId, out var partyKind, out var slotIndex);
                var assignment = slotIndex < 0 ? string.Empty : $"{GetPartyName(partyKind)} {slotIndex + 1}";
                card.BindMonster(
                    definition,
                    owned,
                    string.Equals(owned.MonsterId, selectedMonsterId, StringComparison.OrdinalIgnoreCase),
                    assignment,
                    HandleCardSelected);
            }

            if (ownedMonsters.Count > displayCount)
            {
                SetStatus($"현재 목록에는 앞의 {displayCount}마리만 표시됩니다.");
            }
        }

        private void RefreshFormationPreview(MonsterRosterView roster)
        {
            CacheFormationPreviewSlots();
            while (formationPreviewInstances.Count < formationPreviewSlots.Count) formationPreviewInstances.Add(null);
            var visibleSlotCount = MonsterRosterData.MainPartySlotCount + MonsterRosterData.ReservePartySlotCount;

            for (var index = 0; index < formationPreviewSlots.Count; index++)
            {
                var slotRoot = formationPreviewSlots[index];
                var visible = index < visibleSlotCount;
                slotRoot.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var isMain = index < MonsterRosterData.MainPartySlotCount;
                var slots = isMain ? roster.MainPartySlots : roster.ReservePartySlots;
                var partyIndex = isMain ? index : index - MonsterRosterData.MainPartySlotCount;
                slotRoot.localPosition = GetFormationPreviewSlotPosition(
                    isMain ? MonsterPartyKind.Main : MonsterPartyKind.Reserve, partyIndex);
                var ring = slotRoot.Find("GroundSlotRing")?.GetComponent<MeshRenderer>();
                if (ring != null)
                {
                    ring.sharedMaterial = activeSlotMaterial;
                    ringProperties ??= new MaterialPropertyBlock();
                    ring.GetPropertyBlock(ringProperties);
                    var color = isMain ? mainPartyRingColor : reservePartyRingColor;
                    ringProperties.SetColor("_BaseColor", color);
                    ringProperties.SetColor("_Color", color);
                    ring.SetPropertyBlock(ringProperties); // 공유 원본 Material 색상은 보존
                }

                if (partyIndex >= slots.Count || string.IsNullOrEmpty(slots[partyIndex]) ||
                    !catalog.TryGet(slots[partyIndex], out var definition) || !MonsterPreviewPresentation.CanShow(definition))
                {
                    formationPreviewInstances[index]?.Dispose();
                    formationPreviewInstances[index] = null;
                    continue;
                }

                var previous = formationPreviewInstances[index];
                if (previous != null && previous.Root != null && previous.MonsterId == definition.MonsterId) continue;
                previous?.Dispose();
                formationPreviewInstances[index] = null;
                var anchor = slotRoot.Find("MonsterPreviewAnchor") ?? slotRoot;
                var instance = MonsterPreviewPresentation.Create(definition, anchor, PreviewLayer, 0f, index * 0.173f);
                formationPreviewInstances[index] = instance;
                instance?.UseAuthoredScale(slotRoot); // 정식 모델 사이의 크기 차이를 같은 상자로 정규화하지 않음
            }
        }

        public static Vector3 GetFormationPreviewSlotPosition(MonsterPartyKind party, int index)
        {
            var count = party == MonsterPartyKind.Main
                ? MonsterRosterData.MainPartySlotCount
                : MonsterRosterData.ReservePartySlotCount;
            if (index < 0 || index >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            const float radius = 3.45f; // 기존 원형 크기 유지
            var totalCount = MonsterRosterData.MainPartySlotCount + MonsterRosterData.ReservePartySlotCount;
            var slotIndex = party == MonsterPartyKind.Main ? index : MonsterRosterData.MainPartySlotCount + index;
            var angle = -90f + (slotIndex - 0.5f) * (360f / totalCount); // 45도 균등 간격·반 칸 회전으로 중앙 가림 완화
            var radians = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians) * radius, 0f, -Mathf.Cos(radians) * radius);
        }

        private void RefreshSelectedDetails(GameProgressView view)
        {
            var roster = view.Monsters;
            if (!roster.TryGetOwnedMonster(selectedMonsterId, out var owned) ||
                !catalog.TryGet(selectedMonsterId, out var definition))
            {
                SetText(selectedNameLabel, "보유 몬스터 없음");
                SetText(selectedLevelLabel, string.Empty);
                SetText(selectedStatsLabel, string.Empty);
                SetText(currencyLabel, $"보유 골드 {view.Gold:N0}");
                SetControlsInteractable(false);
                ClearPreview();
                return;
            }

            var multiplier = MonsterLevelRules.GetStatMultiplier(owned.Level);
            SetText(selectedNameLabel, definition.DisplayName);
            SetText(selectedLevelLabel, $"Lv. {owned.Level}  ·  {(definition.Ranged ? "원거리" : "근거리")}");
            SetText(selectedStatsLabel,
                $"체력  {definition.MaxHealth * multiplier:0.##}\n" +
                $"공격력  {definition.AttackPower * multiplier:0.##}\n" +
                $"방어력  {definition.Defense * multiplier:0.##}\n" +
                $"공격속도  {definition.AttackSpeed * multiplier:0.##}\n" +
                $"이동속도  {definition.MoveSpeed * multiplier:0.##}\n" +
                $"사거리  {definition.AttackRange * multiplier:0.##}");
            SetText(currencyLabel, $"보유 골드 {view.Gold:N0}");

            var hasLevelCost = MonsterLevelRules.TryGetNextLevelCost(owned.Level, out var cost);
            var canLevel = hasLevelCost && view.Gold >= cost;
            SetText(levelUpButtonLabel, hasLevelCost ? $"레벨업  {cost:N0} 골드" : "최대 레벨");
            if (levelUpButton != null)
            {
                levelUpButton.interactable = !isBusy && canLevel;
            }

            UpdateFormationButton(roster);
            ShowPreview(definition);
            SetCardsInteractable(!isBusy);
            if (mainTabButton != null)
            {
                mainTabButton.interactable = !isBusy;
            }

            if (reserveTabButton != null)
            {
                reserveTabButton.interactable = !isBusy;
            }
        }

        private void UpdateFormationButton(MonsterRosterView roster)
        {
            var assigned = TryGetAssignment(roster, selectedMonsterId, out var assignedParty, out _);
            var inActiveParty = assigned && assignedParty == activeParty;
            var targetSlots = activeParty == MonsterPartyKind.Main ? roster.MainPartySlots : roster.ReservePartySlots;
            var targetCount = CountAssigned(targetSlots);
            var canChange = true;
            string label;
            if (inActiveParty)
            {
                if (activeParty == MonsterPartyKind.Main && targetCount <= 1)
                {
                    label = "메인 최소 1기 유지";
                    canChange = false;
                }
                else
                {
                    label = "편성 해제";
                }
            }
            else if (targetCount >= targetSlots.Count)
            {
                label = $"{GetPartyName(activeParty)} 가득 참";
                canChange = false;
            }
            else
            {
                label = $"{GetPartyName(activeParty)}에 편성";
            }

            SetText(formationButtonLabel, label);
            if (formationButton != null)
            {
                formationButton.interactable = !isBusy && canChange;
            }
        }

        private void EnsureCardCount(List<MonsterCardView> cards, int count, Transform parent)
        {
            if (cardPrefab == null || parent == null)
            {
                return;
            }

            while (cards.Count < count)
            {
                var card = Instantiate(cardPrefab, parent);
                card.name = $"MonsterCard_{cards.Count + 1}";
                cards.Add(card);
            }
        }

        private void ShowPreview(MonsterDefinition definition)
        {
            if (!IsOpen || !MonsterPreviewPresentation.CanShow(definition) || previewAnchor == null)
            {
                ClearPreview();
                return;
            }
            if (preview != null && preview.Root != null && preview.MonsterId == definition.MonsterId) return;
            ClearPreview();
            preview = MonsterPreviewPresentation.Create(definition, previewAnchor, PreviewLayer, 165f);
            preview?.FitCamera(previewCamera); // 구형 단일 몬스터 표시 경로만 사용. 정식 편성 카메라는 건드리지 않는다.
        }

        private void ClearPreview()
        {
            preview?.Dispose();
            preview = null;
        }

        private void ClearFormationPreview()
        {
            foreach (var instance in formationPreviewInstances) instance?.Dispose();
            formationPreviewInstances.Clear();
        }

        private void CacheFormationPreviewSlots()
        {
            if (formationPreviewSlots.Count > 0 || formationPreviewSlotsRoot == null)
            {
                return;
            }

            foreach (Transform child in formationPreviewSlotsRoot)
            {
                if (child.name.StartsWith("FormationSlot_", StringComparison.Ordinal))
                {
                    formationPreviewSlots.Add(child);
                }
            }

            formationPreviewSlots.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        }

        private void CaptureTabColors()
        {
            if (tabColorsCaptured || mainTabButton == null || reserveTabButton == null ||
                mainTabButton.targetGraphic == null || reserveTabButton.targetGraphic == null)
            {
                return;
            }

            mainTabActiveColor = mainTabButton.targetGraphic.color;
            reserveTabInactiveColor = reserveTabButton.targetGraphic.color;
            tabColorsCaptured = true;
        }

        private void SetTabVisual(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            CaptureTabColors();
            if (tabColorsCaptured && button.targetGraphic != null)
            {
                button.targetGraphic.color = selected ? mainTabActiveColor : reserveTabInactiveColor;
                button.GetComponent<UIStateVisual>()?.SetSelected(selected);
            }

            var innerBorder = button.transform.Find("InnerBorder1");
            if (innerBorder != null)
            {
                innerBorder.gameObject.SetActive(true);
                var innerImage = innerBorder.GetComponent<Image>();
                if (innerImage != null)
                {
                    innerImage.color = selected
                        ? new Color32(0x73, 0x9A, 0xA5, 0xFF)
                        : new Color32(0x31, 0x30, 0x31, 0xFF);
                }
            }

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.color = selected
                    ? Color.white
                    : new Color32(0xB0, 0xAD, 0xAA, 0xFF);
            }
        }



        private void SetControlsInteractable(bool interactable)
        {
            if (levelUpButton != null)
            {
                levelUpButton.interactable = interactable;
            }

            if (formationButton != null)
            {
                formationButton.interactable = interactable;
            }

            if (positionFormationButton != null)
            {
                positionFormationButton.interactable = interactable;
            }

            if (mainTabButton != null)
            {
                mainTabButton.interactable = interactable;
            }

            if (reserveTabButton != null)
            {
                reserveTabButton.interactable = interactable;
            }

            SetCardsInteractable(interactable);
        }

        private void SetCardsInteractable(bool interactable)
        {
            for (var index = 0; index < formationCards.Count; index++)
            {
                formationCards[index].SetInteractable(interactable);
            }

            ownedRosterList?.SetCardsInteractable(interactable);
        }



        private static bool TryGetAssignment(
            MonsterRosterView roster,
            string monsterId,
            out MonsterPartyKind partyKind,
            out int slotIndex)
        {
            slotIndex = FindSlot(roster.MainPartySlots, monsterId);
            if (slotIndex >= 0)
            {
                partyKind = MonsterPartyKind.Main;
                return true;
            }

            slotIndex = FindSlot(roster.ReservePartySlots, monsterId);
            partyKind = MonsterPartyKind.Reserve;
            return slotIndex >= 0;
        }

        private static int FindSlot(IReadOnlyList<string> slots, string monsterId)
        {
            if (slots != null && !string.IsNullOrWhiteSpace(monsterId))
            {
                for (var index = 0; index < slots.Count; index++)
                {
                    if (string.Equals(slots[index], monsterId, StringComparison.OrdinalIgnoreCase))
                    {
                        return index;
                    }
                }
            }

            return -1;
        }

        private static int CountAssigned(IReadOnlyList<string> slots)
        {
            var count = 0;
            if (slots != null)
            {
                for (var index = 0; index < slots.Count; index++)
                {
                    if (!string.IsNullOrEmpty(slots[index]))
                    {
                        count++;
                    }
                }
            }

            return count;
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

        private static string GetPartyName(MonsterPartyKind partyKind)
        {
            return partyKind == MonsterPartyKind.Main ? "본부대" : "예비부대";
        }

        private void SetStatus(string message)
        {
            SetText(statusLabel, message);
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }

#if UNITY_EDITOR
        public void EditorSetStandaloneOpenButtonVisible(bool visible)
        {
            showStandaloneOpenButton = visible;
            if (openButton != null)
            {
                openButton.gameObject.SetActive(visible && !IsOpen);
            }
        }

        public void EditorConfigure(
            GameObject contentPage,
            Button opener,
            Button closer,
            Button mainTab,
            Button reserveTab,
            TMP_Text mainTabText,
            TMP_Text reserveTabText,
            TMP_Text selectedName,
            TMP_Text selectedLevel,
            TMP_Text selectedStats,
            TMP_Text currency,
            TMP_Text status,
            Button levelButton,
            TMP_Text levelButtonText,
            Button assignButton,
            TMP_Text assignButtonText,
            Transform slotsRoot,
            Transform gridRoot,
            MonsterCardView monsterCard,
            RawImage modelImage,
            Camera modelCamera,
            Light modelLight,
            Transform modelAnchor,
            Camera mainWorldCamera)
        {
            pageRoot = contentPage;
            openButton = opener;
            closeButton = closer;
            mainTabButton = mainTab;
            reserveTabButton = reserveTab;
            mainTabLabel = mainTabText;
            reserveTabLabel = reserveTabText;
            selectedNameLabel = selectedName;
            selectedLevelLabel = selectedLevel;
            selectedStatsLabel = selectedStats;
            currencyLabel = currency;
            statusLabel = status;
            levelUpButton = levelButton;
            levelUpButtonLabel = levelButtonText;
            formationButton = assignButton;
            formationButtonLabel = assignButtonText;
            formationSlotsRoot = slotsRoot;
            ownedRosterList = gridRoot != null
                ? gridRoot.GetComponentInParent<MonsterRosterListView>()
                : null;
            cardPrefab = monsterCard;
            previewImage = modelImage;
            previewCamera = modelCamera;
            previewLight = modelLight;
            previewAnchor = modelAnchor;
            worldCamera = mainWorldCamera;
        }


        public void EditorConfigureFormal(
            GameObject contentPage,
            Button opener,
            Button closer,
            Button mainTab,
            Button reserveTab,
            TMP_Text mainTabText,
            TMP_Text reserveTabText,
            Button assignButton,
            TMP_Text assignButtonText,
            Transform gridRoot,
            MonsterCardView monsterCard,
            TMP_Text rosterCount,
            TMP_Text slotCapacity,
            RawImage modelImage,
            Camera modelCamera,
            Light modelLight,
            Transform previewSlots,
            Material activeRing,
            Material lockedRing,
            TMP_Text status,
            Camera mainWorldCamera)
        {
            pageRoot = contentPage;
            openButton = opener;
            closeButton = closer;
            mainTabButton = mainTab;
            reserveTabButton = reserveTab;
            mainTabLabel = mainTabText;
            reserveTabLabel = reserveTabText;
            selectedNameLabel = null;
            selectedLevelLabel = null;
            selectedStatsLabel = null;
            currencyLabel = null;
            statusLabel = status;
            levelUpButton = null;
            levelUpButtonLabel = null;
            formationButton = assignButton;
            formationButtonLabel = assignButtonText;
            formationSlotsRoot = null;
            ownedRosterList = gridRoot != null
                ? gridRoot.GetComponentInParent<MonsterRosterListView>()
                : null;
            cardPrefab = monsterCard;
            ownedCountLabel = rosterCount;
            capacityLabel = slotCapacity;
            previewImage = modelImage;
            previewCamera = modelCamera;
            previewLight = modelLight;
            previewAnchor = null;
            formationPreviewSlotsRoot = previewSlots;
            activeSlotMaterial = activeRing;
            lockedSlotMaterial = lockedRing;
            worldCamera = mainWorldCamera;
        }
#endif
    }
}
