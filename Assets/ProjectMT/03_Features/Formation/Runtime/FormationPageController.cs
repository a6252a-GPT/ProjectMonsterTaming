using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectMT.Shared.GameData;
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

        [Header("Cards")]
        [SerializeField] private Transform formationSlotsRoot;
        [SerializeField] private Transform ownedGridRoot;
        [SerializeField] private MonsterCardView cardPrefab;

        [Header("Preview")]
        [SerializeField] private RawImage previewImage;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Light previewLight;
        [SerializeField] private Transform previewAnchor;
        [SerializeField] private Camera worldCamera;

        private readonly List<MonsterCardView> formationCards = new List<MonsterCardView>();
        private readonly List<MonsterCardView> ownedCards = new List<MonsterCardView>();
        private IGameProgressService progress;
        private MonsterCatalog catalog;
        private Func<BattlePartySnapshot> refreshParty;
        private GameObject previewInstance;
        private string selectedMonsterId;
        private MonsterPartyKind activeParty = MonsterPartyKind.Main;
        private bool isBusy;
        private bool worldCameraStateCaptured;
        private bool worldCameraWasEnabled;
        private int worldCameraCullingMask;
        private CameraClearFlags worldCameraClearFlags;
        private Color worldCameraBackgroundColor;

        public event Action<BattlePartySnapshot> PartyChanged;
        public bool IsOpen => pageRoot != null && pageRoot.activeSelf;

        private void Awake()
        {
            openButton?.onClick.AddListener(OpenPage);
            closeButton?.onClick.AddListener(ClosePage);
            mainTabButton?.onClick.AddListener(SelectMainTab);
            reserveTabButton?.onClick.AddListener(SelectReserveTab);
            levelUpButton?.onClick.AddListener(HandleLevelUpClicked);
            formationButton?.onClick.AddListener(HandleFormationClicked);
            SetPageOpen(false);
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
            if (IsOpen || worldCameraStateCaptured)
            {
                SetPageOpen(false); // 상위 오브젝트 비활성화에도 월드 카메라 복구
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
            progress = null;
            catalog = null;
            refreshParty = null;
            selectedMonsterId = null;
            isBusy = false;
            PartyChanged = null;
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
            SetStatus(string.Empty);
            RefreshView();
        }

        public void ClosePage()
        {
            SetPageOpen(false);
        }

        private void SetPageOpen(bool open)
        {
            if (open)
            {
                if (!worldCameraStateCaptured)
                {
                    worldCameraWasEnabled = worldCamera != null && worldCamera.enabled;
                    if (worldCamera != null)
                    {
                        worldCameraCullingMask = worldCamera.cullingMask;
                        worldCameraClearFlags = worldCamera.clearFlags;
                        worldCameraBackgroundColor = worldCamera.backgroundColor;
                    }

                    worldCameraStateCaptured = true;
                }

                if (worldCamera != null)
                {
                    worldCamera.enabled = true;
                    worldCamera.cullingMask = 0; // UI 출력은 유지하고 월드 드로우만 차단
                    worldCamera.clearFlags = CameraClearFlags.SolidColor;
                    worldCamera.backgroundColor = new Color(0.008f, 0.012f, 0.022f, 1f);
                }
            }

            pageRoot?.SetActive(open);
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
                if (worldCameraStateCaptured && worldCamera != null)
                {
                    worldCamera.cullingMask = worldCameraCullingMask;
                    worldCamera.clearFlags = worldCameraClearFlags;
                    worldCamera.backgroundColor = worldCameraBackgroundColor;
                    worldCamera.enabled = worldCameraWasEnabled;
                }

                worldCameraStateCaptured = false;
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

            await ApplyAndSaveAsync(
                GameProgressChange.LevelUpMonster(selectedMonsterId, owned.Level),
                $"{ResolveDisplayName(selectedMonsterId)} 레벨업 완료");
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
            await ApplyAndSaveAsync(change, successMessage);
        }

        private async Task ApplyAndSaveAsync(GameProgressChange change, string successMessage)
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
                return;
            }

            SetStatus(saved ? successMessage : "변경을 저장하지 못했습니다");
            if (IsOpen)
            {
                RefreshView();
            }
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
            UpdateTabState();
            RefreshFormationCards(roster);
            RefreshOwnedCards(roster);
            RefreshSelectedDetails(view);
        }

        private void UpdateTabState()
        {
            SetText(mainTabLabel, activeParty == MonsterPartyKind.Main ? "● 메인 편성" : "메인 편성");
            SetText(reserveTabLabel, activeParty == MonsterPartyKind.Reserve ? "● 예비 편성" : "예비 편성");
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
            var ownedMonsters = roster.OwnedMonsters;
            EnsureCardCount(ownedCards, ownedMonsters.Count, ownedGridRoot);
            for (var index = 0; index < ownedCards.Count; index++)
            {
                var card = ownedCards[index];
                var visible = index < ownedMonsters.Count;
                card.gameObject.SetActive(visible);
                if (!visible)
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
                SetText(currencyLabel, $"보유 골드 {view.TemporaryGold:N0}");
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
            SetText(currencyLabel, $"보유 골드 {view.TemporaryGold:N0}");

            var hasLevelCost = MonsterLevelRules.TryGetNextLevelCost(owned.Level, out var cost);
            var canLevel = hasLevelCost && view.TemporaryGold >= cost;
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
            ClearPreview();
            if (!IsOpen || definition == null || definition.PreviewPrefab == null || previewAnchor == null)
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

            if (formationButton != null)
            {
                formationButton.interactable = interactable;
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

            for (var index = 0; index < ownedCards.Count; index++)
            {
                ownedCards[index].SetInteractable(interactable);
            }
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
                feedback.SetTint(tint); // 공유 Material을 복제하지 않고 Preview만 색상 변경
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
            return partyKind == MonsterPartyKind.Main ? "메인" : "예비";
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
            ownedGridRoot = gridRoot;
            cardPrefab = monsterCard;
            previewImage = modelImage;
            previewCamera = modelCamera;
            previewLight = modelLight;
            previewAnchor = modelAnchor;
            worldCamera = mainWorldCamera;
        }
#endif
    }
}
