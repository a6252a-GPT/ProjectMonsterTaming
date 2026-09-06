using System;
using System.Collections.Generic;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.UI;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using AP = ProjectMT.Features.CommanderSkill.CommanderSkillAwakeningParameter;

namespace ProjectMT.Features.CommanderSkill
{
    [DisallowMultipleComponent]
    public sealed partial class CommanderSkillPageController : MonoBehaviour // 장착 슬롯·보유 목록·상세 관리창
    {
        private enum SkillFilter
        {
            All,
            Attack,
            Buff,
            Debuff
        }

        private sealed class SkillFrameView
        {
            public Image Background;
            public Image Border;
        }

        private sealed class SlotView
        {
            public Button Button;
            public Image Icon;
            public TMP_Text LevelText;
            public TMP_Text NumberText;
            public Image[] Stars;
            public GameObject LockRoot;
            public GameObject FocusRoot;
            public SkillFrameView Frame;
        }

        private sealed class SkillCardView
        {
            public GameObject Root;
            public Button Button;
            public Image Icon;
            public TMP_Text NameText;
            public TMP_Text CategoryText;
            public TMP_Text StatsText;
            public TMP_Text LevelText;
            public GameObject EquippedRoot;
            public GameObject FocusRoot;
            public SkillFrameView Frame;
            public string SkillId;
            public Image[] Stars;
            public Image CardBorder;
            public TMP_Text EquippedText;
            public Image Background;
        }

        private static readonly Color SelectedColor = new Color32(226, 187, 82, 255);
        private static readonly Color NormalColor = new Color32(222, 216, 207, 255);
        private static readonly Color DisabledColor = new Color32(154, 147, 138, 255);

        private readonly SlotView[] slots = new SlotView[CommanderSkillSlotRules.SlotCount];
        private readonly List<SkillCardView> cards = new List<SkillCardView>(16);
        private readonly Button[] filterButtons = new Button[4];
        private readonly TMP_Text[] filterLabels = new TMP_Text[4];
        private readonly Image[] filterBackgrounds = new Image[4];
        private readonly Image[] filterBorders = new Image[4];

        private Button closeButton;
        private TMP_Text ownedCountText;
        private Image detailIcon;
        private SkillFrameView detailFrame;
        private TMP_Text detailNameText;
        private TMP_Text detailCategoryText;
        private TMP_Text detailStatsText;
        private TMP_Text detailDescriptionText;
        private TMP_Text detailLevelText;
        private TMP_Text detailMaterialText;
        private TMP_Text selectedSlotText;
        private TMP_Text feedbackText;
        private Button levelUpButton;
        private Button awakenButton;
        private TMP_Text awakenButtonText;
        private TMP_Text levelUpButtonText;
        private Button equipButton;
        private TMP_Text equipButtonText;
        private Image levelUpButtonBorder;
        private Image equipButtonBorder;

        private IGameProgressService progress;
        private CommanderSkillCatalog catalog;
        private SkillFilter currentFilter;
        private string selectedSkillId;
        private int selectedSlotIndex;
        private bool referencesCached;
        private bool listenersBound;
        private bool requestInFlight;
        private int lifetimeVersion;
        private string feedbackMessage;

        public event Action<bool> OpenStateChanged;
        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            EnsureReferencesAndListeners();
        }

        private void OnEnable()
        {
            EnsureReferencesAndListeners();
            SubscribeProgress();
            RefreshAll();
        }

        private void OnDisable()
        {
            lifetimeVersion++;
            requestInFlight = false;
            UnsubscribeProgress();
        }

        public void Configure(IGameProgressService progressService, CommanderSkillCatalog skillCatalog)
        {
            lifetimeVersion++;
            requestInFlight = false;
            UnsubscribeProgress();

            progress = progressService;
            catalog = skillCatalog;
            SubscribeProgress();

            EnsureReferencesAndListeners();
            EnsureSelection();
            RefreshAll();
        }

        public void Shutdown()
        {
            lifetimeVersion++;
            UnsubscribeProgress();

            progress = null;
            catalog = null;
            requestInFlight = false;
            feedbackMessage = string.Empty;
        }

        public void Open()
        {
            EnsureReferencesAndListeners();
            if (!gameObject.activeSelf)
            {
                UIPanelPopAnimator.RequestOpen(gameObject);
                OpenStateChanged?.Invoke(true);
            }

            EnsureSelection();
            RefreshAll();
        }

        public void Close()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            UIPanelPopAnimator.RequestClose(gameObject, () => OpenStateChanged?.Invoke(false));
        }

        private void EnsureReferencesAndListeners()
        {
            if (!referencesCached)
            {
                CacheReferences();
                referencesCached = true;
            }

            if (listenersBound)
            {
                return;
            }

            closeButton?.onClick.AddListener(Close);
            levelUpButton?.onClick.AddListener(HandleLevelUpClicked);
            awakenButton?.onClick.AddListener(HandleAwakenClicked);
            equipButton?.onClick.AddListener(HandleEquipClicked);
            for (var index = 0; index < slots.Length; index++)
            {
                var slotIndex = index;
                slots[index]?.Button?.onClick.AddListener(() => SelectSlot(slotIndex));
            }

            for (var index = 0; index < cards.Count; index++)
            {
                BindCardListener(cards[index], index);
            }

            for (var index = 0; index < filterButtons.Length; index++)
            {
                var filterIndex = index;
                filterButtons[index]?.onClick.AddListener(() => SelectFilter((SkillFilter)filterIndex));
            }

            listenersBound = true;
        }

        private void CacheReferences()
        {
            closeButton = FindDeep(transform, "CloseTouchArea_80x80")?.GetComponent<Button>();
            ownedCountText = FindDeep(transform, "OwnedSkillCount")?.GetComponent<TMP_Text>();
            detailIcon = FindDeep(transform, "SelectedSkillIcon")?.GetComponent<Image>();
            detailFrame = CacheSkillFrame(FindDeep(transform, "SelectedSkillIconFrame"));
            detailNameText = FindDeep(transform, "SelectedSkillName")?.GetComponent<TMP_Text>();
            detailCategoryText = FindDeep(transform, "SelectedSkillCategory")?.GetComponent<TMP_Text>();
            detailStatsText = FindDeep(transform, "SelectedSkillStats")?.GetComponent<TMP_Text>();
            detailDescriptionText = FindDeep(transform, "SelectedSkillDescription")?.GetComponent<TMP_Text>();
            detailLevelText = FindDeep(transform, "SelectedSkillLevel")?.GetComponent<TMP_Text>();
            detailMaterialText = FindDeep(transform, "SelectedSkillMaterial")?.GetComponent<TMP_Text>();
            selectedSlotText = FindDeep(transform, "SelectedSlotLabel")?.GetComponent<TMP_Text>();
            feedbackText = FindDeep(transform, "SkillPageFeedback")?.GetComponent<TMP_Text>();
            levelUpButton = FindDeep(transform, "SkillLevelUpButton")?.GetComponent<Button>();
            awakenButton = FindDeep(transform, "SkillAwakenButton")?.GetComponent<Button>();
            awakenButtonText = awakenButton?.GetComponentInChildren<TMP_Text>(true);
            levelUpButtonText = levelUpButton?.GetComponentInChildren<TMP_Text>(true);
            equipButton = FindDeep(transform, "SkillEquipButton")?.GetComponent<Button>();
            equipButtonText = equipButton?.GetComponentInChildren<TMP_Text>(true);
            levelUpButtonBorder = FindDeep(levelUpButton?.transform, "InnerBorder1")?.GetComponent<Image>();
            equipButtonBorder = FindDeep(equipButton?.transform, "InnerBorder1")?.GetComponent<Image>();

            for (var index = 0; index < slots.Length; index++)
            {
                var root = FindDeep(transform, $"EquippedSkillSlot_{index + 1}");
                slots[index] = root == null
                    ? null
                    : new SlotView
                    {
                        Button = root.GetComponent<Button>(),
                        Icon = FindDeep(root, "SkillIcon")?.GetComponent<Image>(),
                        LevelText = FindDeep(root, "SkillLevel")?.GetComponent<TMP_Text>(),
                        NumberText = FindDeep(root, "SlotNumber")?.GetComponent<TMP_Text>(),
                        LockRoot = FindDeep(root, "SlotLock")?.gameObject,
                        FocusRoot = FindDeep(root, "SlotFocus")?.gameObject,
                        Frame = CacheSkillFrame(root),
                        Stars = CacheStars(root)
                    };
            }

            for (var index = 0; index < 12; index++)
            {
                var root = FindDeep(transform, $"OwnedSkillCard_{index + 1}");
                if (root == null)
                {
                    continue;
                }

                cards.Add(CreateCardView(root));
            }

            var filterNames = new[] { "Filter_All", "Filter_Attack", "Filter_Buff", "Filter_Debuff" };
            for (var index = 0; index < filterNames.Length; index++)
            {
                var filter = FindDeep(transform, filterNames[index]);
                filterButtons[index] = filter?.GetComponent<Button>();
                filterLabels[index] = filter?.GetComponentInChildren<TMP_Text>(true);
                filterBackgrounds[index] = FindDeep(filter, "Bg")?.GetComponent<Image>() ?? filter?.GetComponent<Image>();
                filterBorders[index] = FindDeep(filter, "InnerBorder1")?.GetComponent<Image>();
            }
        }

        private void SelectSlot(int slotIndex)
        {
            if (progress == null || slotIndex < 0 || slotIndex >= slots.Length)
            {
                return;
            }

            selectedSlotIndex = slotIndex;
            var skillId = progress.View.CommanderSkills.GetEquippedSkillId(slotIndex);
            if (!string.IsNullOrEmpty(skillId))
            {
                selectedSkillId = skillId;
            }

            feedbackMessage = string.Empty;
            RefreshAll();
        }

        private void SelectCard(int cardIndex)
        {
            if (cardIndex < 0 || cardIndex >= cards.Count || string.IsNullOrEmpty(cards[cardIndex].SkillId))
            {
                return;
            }

            selectedSkillId = cards[cardIndex].SkillId;
            feedbackMessage = string.Empty;
            RefreshAll();
        }

        private void SelectFilter(SkillFilter filter)
        {
            currentFilter = filter;
            feedbackMessage = string.Empty;
            RefreshAll();
            var scroll = GetComponentInChildren<ScrollRect>();
            if (scroll != null) { Canvas.ForceUpdateCanvases(); scroll.StopMovement(); scroll.verticalNormalizedPosition = 1f; }
        }

        private async void HandleEquipClicked()
        {
            var progressService = progress;
            if (requestInFlight || progressService == null || string.IsNullOrEmpty(selectedSkillId))
            {
                return;
            }

            var requestVersion = lifetimeVersion;
            var view = progressService.View.CommanderSkills;
            if (!view.IsSlotUnlocked(selectedSlotIndex))
            {
                feedbackMessage = "잠긴 슬롯입니다.";
                RefreshAll();
                return;
            }

            var expectedSkillId = view.GetEquippedSkillId(selectedSlotIndex);
            if (expectedSkillId == selectedSkillId)
            {
                return;
            }

            requestInFlight = true;
            RefreshAll();
            try
            {
                var saved = await progressService.TryApplyAndSaveAsync(
                    GameProgressChange.EquipCommanderSkill(
                        selectedSlotIndex,
                        expectedSkillId,
                        selectedSkillId));
                if (IsCurrentRequest(requestVersion))
                {
                    feedbackMessage = saved
                        ? $"{selectedSlotIndex + 1}번 슬롯에 장착했습니다."
                        : "장착 상태가 변경되어 다시 확인해 주세요.";
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (IsCurrentRequest(requestVersion))
                {
                    feedbackMessage = "장착 저장에 실패했습니다.";
                }
            }
            finally
            {
                if (IsCurrentRequest(requestVersion))
                {
                    requestInFlight = false;
                    RefreshAll();
                }
            }
        }

        private async void HandleLevelUpClicked()
        {
            var progressService = progress;
            if (requestInFlight || progressService == null || !TryGetOwnedSkill(selectedSkillId, out var owned))
            {
                return;
            }

            var requestVersion = lifetimeVersion;
            requestInFlight = true;
            RefreshAll();
            try
            {
                var saved = await progressService.TryApplyAndSaveAsync(
                    GameProgressChange.LevelUpCommanderSkill(
                        owned.SkillId,
                        owned.Level));
                if (IsCurrentRequest(requestVersion))
                {
                    feedbackMessage = saved
                        ? $"{owned.Level + 1}레벨이 되었습니다."
                        : "레벨업 조건이 변경되어 다시 확인해 주세요.";
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (IsCurrentRequest(requestVersion))
                {
                    feedbackMessage = "레벨업 저장에 실패했습니다.";
                }
            }
            finally
            {
                if (IsCurrentRequest(requestVersion))
                {
                    requestInFlight = false;
                    RefreshAll();
                }
            }
        }

        private void HandleProgressChanged()
        {
            if (this != null && isActiveAndEnabled) RefreshAll();
        }

        private async void HandleAwakenClicked()
        {
            var service = progress;
            if (requestInFlight || service == null || !TryGetOwnedSkill(selectedSkillId, out var owned)) return;
            var version = lifetimeVersion;
            requestInFlight = true;
            RefreshAll();
            try
            {
                var saved = await service.TryApplyAndSaveAsync(GameProgressChange.AwakenCommanderSkill(
                    owned.SkillId, owned.AwakeningLevel, owned.DuplicateCount));
                if (IsCurrentRequest(version)) feedbackMessage = saved ? $"{owned.AwakeningLevel + 1}성으로 각성했습니다." : "각성 조건이 바뀌었습니다. 다시 확인해 주세요.";
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (IsCurrentRequest(version)) feedbackMessage = "각성 저장에 실패했습니다. 중복은 소비되지 않았습니다.";
            }
            finally { if (IsCurrentRequest(version)) { requestInFlight = false; RefreshAll(); } }
        }

        private bool IsCurrentRequest(int requestVersion)
        {
            return this != null && isActiveAndEnabled && requestVersion == lifetimeVersion;
        }

        private void SubscribeProgress()
        {
            if (progress == null || !isActiveAndEnabled)
            {
                return;
            }

            progress.Changed -= HandleProgressChanged;
            progress.Changed += HandleProgressChanged;
        }

        private void UnsubscribeProgress()
        {
            if (progress != null)
            {
                progress.Changed -= HandleProgressChanged;
            }
        }

        private void EnsureSelection()
        {
            if (progress == null)
            {
                return;
            }

            var progressView = progress.View;
            var view = progressView.CommanderSkills;
            selectedSlotIndex = Mathf.Clamp(selectedSlotIndex, 0, CommanderSkillSlotRules.SlotCount - 1);
            if (TryGetOwnedSkill(selectedSkillId, out _))
            {
                return;
            }

            var equipped = view.GetEquippedSkillId(selectedSlotIndex);
            if (!string.IsNullOrEmpty(equipped))
            {
                selectedSkillId = equipped;
            }
            else if (view.OwnedSkills.Count > 0)
            {
                selectedSkillId = view.OwnedSkills[0].SkillId;
            }
        }

        private void RefreshAll()
        {
            if (!referencesCached || progress == null || catalog == null)
            {
                return;
            }

            EnsureSelection();
            var progressView = progress.View;
            var view = progressView.CommanderSkills;
            RefreshSlots(view);
            RefreshCards(view);
            RefreshFilters();
            RefreshDetail(view, progressView.Gold);
            RefreshGrowthPresentation(view);
        }

        private void RefreshSlots(CommanderSkillProgressView view)
        {
            for (var index = 0; index < slots.Length; index++)
            {
                var slot = slots[index];
                if (slot == null)
                {
                    continue;
                }

                var unlocked = view.IsSlotUnlocked(index);
                var skillId = view.GetEquippedSkillId(index);
                var hasDefinition = catalog.TryGet(skillId, out var definition);
                slot.Button.interactable = unlocked && !requestInFlight;
                slot.LockRoot?.SetActive(!unlocked);
                if (slot.LevelText != null) { var plate = slot.LevelText.transform.parent.Find("LevelBackground"); if (plate != null) plate.gameObject.SetActive(hasDefinition); }
                slot.FocusRoot?.SetActive(index == selectedSlotIndex);
                RefreshStars(slot.Stars, TryGetOwnedSkill(skillId, out var starOwned), starOwned.AwakeningLevel);
                RefreshSkillFrame(slot.Frame, hasDefinition ? definition : null);
                if (slot.NumberText != null)
                {
                    slot.NumberText.text = $"{index + 1}번";
                    slot.NumberText.color = hasDefinition ? new Color32(193,181,158,255) : new Color32(120,113,103,255);
                }

                if (slot.Icon != null)
                {
                    slot.Icon.sprite = hasDefinition ? definition.Icon : null;
                    slot.Icon.enabled = hasDefinition;
                    slot.Icon.color = unlocked ? Color.white : DisabledColor;
                }

                if (slot.LevelText != null)
                {
                    slot.LevelText.text = TryGetOwnedSkill(skillId, out var owned)
                        ? $"Lv.{owned.Level}"
                        : string.Empty;
                }
            }
        }

        private void RefreshCards(CommanderSkillProgressView view)
        {
            var requiredCount = 0;
            for (var ownedIndex = 0; ownedIndex < view.OwnedSkills.Count; ownedIndex++)
            {
                var owned = view.OwnedSkills[ownedIndex];
                if (catalog.TryGet(owned.SkillId, out var definition) && MatchesFilter(definition.Category))
                {
                    requiredCount++;
                }
            }
            EnsureCardCapacity(requiredCount);

            var visibleCount = 0;
            for (var ownedIndex = 0; ownedIndex < view.OwnedSkills.Count; ownedIndex++)
            {
                var owned = view.OwnedSkills[ownedIndex];
                if (!catalog.TryGet(owned.SkillId, out var definition) || !MatchesFilter(definition.Category))
                {
                    continue;
                }
                if (visibleCount >= cards.Count)
                {
                    break; // 프리팹 템플릿이 없는 수명 테스트/비정상 뷰에서도 안전하게 종료
                }

                var card = cards[visibleCount++];
                card.SkillId = owned.SkillId;
                card.Root.SetActive(true);
                card.Button.interactable = !requestInFlight;
                card.Icon.sprite = definition.Icon;
                card.Icon.enabled = definition.Icon != null;
                card.NameText.text = definition.DisplayName;
                if (card.CategoryText != null)
                {
                    card.CategoryText.text = BuildRarityCategory(definition, false);
                }

                if (card.StatsText != null)
                {
                    card.StatsText.text = BuildAwakeningAvailability(owned);
                    card.StatsText.color = owned.AwakeningLevel >= 5 ? new Color32(166,154,126,255) : new Color32(220,194,132,255);
                }

                card.LevelText.text = $"Lv.{owned.Level}";
                card.EquippedRoot?.SetActive(FindEquippedSlot(view, owned.SkillId) >= 0);
                card.FocusRoot?.SetActive(owned.SkillId == selectedSkillId);
                RefreshSkillFrame(card.Frame, definition);
                RefreshStars(card.Stars, true, owned.AwakeningLevel);
                if (card.CardBorder != null) { Color color = RarityColor(definition.Rarity); color.a = owned.SkillId == selectedSkillId ? 1f : .7f; card.CardBorder.color = color; }
                if (card.EquippedText != null) card.EquippedText.text = $"{FindEquippedSlot(view, owned.SkillId) + 1}번 장착";
                if (card.Background != null)
                {
                    card.Background.color = owned.SkillId == selectedSkillId
                        ? new Color32(56, 54, 47, 255)
                        : new Color32(44, 41, 43, 255);
                }
            }

            for (var index = visibleCount; index < cards.Count; index++)
            {
                cards[index].SkillId = string.Empty;
                cards[index].Root.SetActive(false);
            }

            if (ownedCountText != null)
            {
                ownedCountText.text = $"{visibleCount}종";
            }
        }

        private void EnsureCardCapacity(int requiredCount)
        {
            if (cards.Count == 0) return;
            var template = cards[0].Root;
            var parent = template.transform.parent;
            while (cards.Count < requiredCount)
            {
                var clone = Instantiate(template, parent);
                clone.name = $"OwnedSkillCard_{cards.Count + 1}";
                var card = CreateCardView(clone.transform);
                cards.Add(card);
                if (listenersBound) BindCardListener(card, cards.Count - 1);
            }
        }

        private static SkillCardView CreateCardView(Transform root)
        {
            return new SkillCardView
            {
                Root = root.gameObject,
                Button = root.GetComponent<Button>(),
                Icon = FindDeep(root, "SkillIcon")?.GetComponent<Image>(),
                NameText = FindDeep(root, "SkillName")?.GetComponent<TMP_Text>(),
                CategoryText = FindDeep(root, "SkillCategory")?.GetComponent<TMP_Text>(),
                StatsText = FindDeep(root, "SkillCardStats")?.GetComponent<TMP_Text>(),
                LevelText = FindDeep(root, "SkillLevel")?.GetComponent<TMP_Text>(),
                EquippedRoot = FindDeep(root, "EquippedBadge")?.gameObject,
                FocusRoot = FindDeep(root, "CardFocus")?.gameObject,
                Frame = CacheSkillFrame(root),
                Background = FindDeep(root, "CardBackground")?.GetComponent<Image>(),
                CardBorder = FindDeep(root, "CardBorder")?.GetComponent<Image>(),
                EquippedText = FindDeep(root, "EquippedBadge")?.GetComponentInChildren<TMP_Text>(true),
                Stars = CacheStars(root)
            };
        }

        private void BindCardListener(SkillCardView card, int cardIndex)
        {
            card?.Button?.onClick.AddListener(() => SelectCard(cardIndex));
        }

        private void RefreshFilters()
        {
            for (var i = 0; i < filterButtons.Length; i++)
            {
                var selected = i == (int)currentFilter;
                if (filterLabels[i] != null) filterLabels[i].color = selected ? new Color32(234,212,161,255) : new Color32(199,190,175,255);
                if (filterBackgrounds[i] != null) { filterBackgrounds[i].sprite = selected ? filterSelectedSprite : filterNormalSprite; filterBackgrounds[i].color = Color.white; }
            }
        }

        private void RefreshDetail(CommanderSkillProgressView view, long gold)
        {
            var hasOwned = TryGetOwnedSkill(selectedSkillId, out var owned);
            CommanderSkillDefinition definition = null;
            var hasDefinition = hasOwned && catalog.TryGet(selectedSkillId, out definition);
            RefreshSkillFrame(detailFrame, hasDefinition ? definition : null);
            if (detailIcon != null)
            {
                detailIcon.sprite = hasDefinition ? definition.Icon : null;
                detailIcon.enabled = hasDefinition && definition.Icon != null;
            }

            if (detailNameText != null)
            {
                detailNameText.text = hasDefinition ? definition.DisplayName : "스킬을 선택해 주세요";
            }

            if (detailCategoryText != null)
            {
                detailCategoryText.text = hasDefinition ? BuildRarityCategory(definition) : string.Empty;
            }

            if (detailStatsText != null)
            {
                detailStatsText.text = hasDefinition ? "쿨타임" : string.Empty;
            }

            if (detailDescriptionText != null)
            {
                detailDescriptionText.text = hasDefinition ? BuildPresentationDescription(definition, owned.Level) : string.Empty;
            }

            if (detailLevelText != null)
            {
                detailLevelText.text = hasOwned && TryGetGrowthRule(owned.SkillId, out var growthRule)
                    ? $"Lv.{owned.Level}"
                    : string.Empty;
            }

            if (detailMaterialText != null)
            {
                detailMaterialText.text = BuildLevelUpCostText(gold, hasOwned, owned);
            }

            if (selectedSlotText != null)
            {
                selectedSlotText.text = $"선택 슬롯  {selectedSlotIndex + 1}번";
            }

            var levelUpCost = 0L;
            var awakeningCost = 0;
            var canAwaken = hasOwned && catalog.BalanceConfig.TryGetAwakeningCost(owned.AwakeningLevel, out awakeningCost)
                && owned.DuplicateCount >= awakeningCost && !requestInFlight;
            if (awakenButton != null) { awakenButton.interactable = canAwaken; RefreshActionButton(awakenButton, null, false); }
            if (awakenButtonText != null) awakenButtonText.text = hasOwned
                ? owned.AwakeningLevel >= catalog.BalanceConfig.MaxAwakening ? "최대 각성" :
                    "별각성" : "별각성";
            var hasLevelUpCost = hasOwned && TryGetGrowthRule(owned.SkillId, out var levelRule) &&
                                 levelRule.TryGetNextLevelGoldCost(owned.Level, out levelUpCost);
            var canLevelUp = hasLevelUpCost && gold >= levelUpCost && !requestInFlight;
            if (levelUpButton != null)
            {
                levelUpButton.interactable = canLevelUp;
                RefreshActionButton(levelUpButton, levelUpButtonBorder, canLevelUp);
            }

            if (levelUpButtonText != null)
            {
                levelUpButtonText.text = hasOwned && TryGetGrowthRule(owned.SkillId, out var capRule) &&
                                         owned.Level >= capRule.MaxLevel
                    ? "최대 레벨"
                    : hasLevelUpCost ? "강화" : "강화 불가";
            }

            var slotUnlocked = view.IsSlotUnlocked(selectedSlotIndex);
            var currentSkillId = view.GetEquippedSkillId(selectedSlotIndex);
            var isSameSkill = currentSkillId == selectedSkillId;
            if (equipButton != null)
            {
                equipButton.interactable = hasOwned && slotUnlocked && !isSameSkill && !requestInFlight;
                RefreshActionButton(equipButton, equipButtonBorder, false);
            }

            if (equipButtonText != null)
            {
                equipButtonText.text = !slotUnlocked ? "슬롯 잠김" : isSameSkill ? "장착 중" : "장착";
            }

            if (feedbackText != null)
            {
                feedbackText.text = requestInFlight ? "저장 중..." : feedbackMessage;
            }
        }

        private static SkillFrameView CacheSkillFrame(Transform root)
        {
            var normal = FindDeep(root, "ItemFrame_01_Normal_Yellow");
            return new SkillFrameView
            {
                Background = FindDeep(normal, "Bg")?.GetComponent<Image>(),
                Border = FindDeep(root, "VisibleRarityBorder")?.GetComponent<Image>() ?? FindDeep(normal, "InnerBorder1")?.GetComponent<Image>()
            };
        }

        private static void RefreshSkillFrame(SkillFrameView frame, CommanderSkillDefinition definition)
        {
            var background = new Color32(48, 45, 48, 255);
            var border = definition != null ? RarityColor(definition.Rarity) : new Color32(73, 69, 67, 255);
            if (frame?.Background != null) frame.Background.color = background;
            if (frame?.Border != null) frame.Border.color = border;
        }

        private static void RefreshActionButton(Button button, Image border, bool primary)
        {
            var group = button.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = button.interactable ? 1f : .65f;
        }

        private string BuildCardStats(CommanderSkillDefinition definition, int level)
        {
            var growth = GetSupportGrowth(definition.SkillId, level);
            var cooldown = growth.Resolve(AP.Cooldown, definition.Cooldown);
            if (definition is CommanderAttackSkillDefinition attack && attack.AreaDamageEffect != null)
            {
                var damage = attack.AreaDamageEffect.BaseDamage * attack.AreaDamageEffect.PerHitMultiplier * GetEffectMultiplier(definition.SkillId, level);
                return $"피해 {damage:0.#}  ·  {cooldown:0.#}초";
            }

            if (definition is CommanderEffectSkillDefinition)
            {
                var multiplier = GetSupportGrowth(definition.SkillId, level);
                for (var index = 0; index < definition.Effects.Count; index++)
                {
                    if (definition.Effects[index] is CommanderUnitEffectDefinition effect)
                    {
                        return $"{BuildEffectSummary(effect, multiplier)}  ·  {cooldown:0.#}초";
                    }
                }
            }

            return $"쿨타임 {definition.Cooldown:0.#}초";
        }

        private string BuildDetailStats(CommanderSkillDefinition definition, int level)
        {
            var growth = GetSupportGrowth(definition.SkillId, level);
            var cooldown = growth.Resolve(AP.Cooldown, definition.Cooldown);
            var range = growth.Resolve(AP.TargetRange, definition.TargetRange);
            if (definition is CommanderAttackSkillDefinition attack && attack.AreaDamageEffect != null)
            {
                var damage = attack.AreaDamageEffect.BaseDamage * attack.AreaDamageEffect.PerHitMultiplier * GetEffectMultiplier(definition.SkillId, level);
                var shape = attack.AreaDamageEffect.Shape switch
                {
                    MonsterBasicAttackShape.Fan => $"부채꼴 {attack.AreaDamageEffect.Angle:0.#}도",
                    MonsterBasicAttackShape.Line => $"직선 폭 {growth.Resolve(AP.LineWidth, attack.AreaDamageEffect.LineWidth, attack.AreaDamageEffect.EffectId):0.#}m",
                    MonsterBasicAttackShape.Circle => $"원형 반경 {growth.Resolve(AP.AreaRadius, attack.AreaDamageEffect.Radius, attack.AreaDamageEffect.EffectId):0.#}m",
                    _ => "단일 대상"
                };
                return $"캐스팅 {definition.CastTime:0.#}초 · 피해 {damage:0.#} · 쿨타임 {cooldown:0.#}초 · {shape}";
            }

            if (definition is CommanderEffectSkillDefinition)
            {
                var summaries = new List<string>();
                var multiplier = GetSupportGrowth(definition.SkillId, level);
                for (var index = 0; index < definition.Effects.Count; index++)
                {
                    if (definition.Effects[index] is CommanderUnitEffectDefinition effect)
                    {
                        summaries.Add(BuildEffectSummary(effect, multiplier));
                    }
                }

                var effectText = summaries.Count > 0 ? string.Join("  ·  ", summaries) : "효과 없음";
                return $"캐스팅 {definition.CastTime:0.#}초 · 쿨타임 {cooldown:0.#}초 · 대상 거리 {range:0.#}m\n{effectText}";
            }

            return $"캐스팅 {definition.CastTime:0.#}초  ·  쿨타임 {definition.Cooldown:0.#}초  ·  대상 거리 {definition.TargetRange:0.#}m";
        }

        private static string BuildEffectSummary(CommanderUnitEffectDefinition effect, CommanderSkillGrowthSnapshot multiplier)
        {
            var label = effect.EffectType switch
            {
                CommanderSkillUnitEffectType.Heal => "회복",
                CommanderSkillUnitEffectType.Shield => "보호막",
                CommanderSkillUnitEffectType.AttackBuff => "공격 증가",
                CommanderSkillUnitEffectType.DefenseBuff => "방어 증가",
                CommanderSkillUnitEffectType.AttackSpeedBuff => "공속 증가",
                CommanderSkillUnitEffectType.DamageReduction => "피해 감소",
                CommanderSkillUnitEffectType.DamageReflect => "피해 반사",
                CommanderSkillUnitEffectType.Cleanse => "약화 1개 정화",
                CommanderSkillUnitEffectType.EnergyGain => "기력 회복",
                CommanderSkillUnitEffectType.AttackDebuff => "공격 감소",
                CommanderSkillUnitEffectType.DefenseDebuff => "방어 감소",
                CommanderSkillUnitEffectType.AttackSpeedDebuff => "공속 감소",
                CommanderSkillUnitEffectType.MoveSpeedDebuff => "이속 감소",
                CommanderSkillUnitEffectType.Slow => "감속",
                CommanderSkillUnitEffectType.Stun => "기절",
                CommanderSkillUnitEffectType.Mark => "받는 피해 증가",
                _ => "기력 감소"
            };
            if (effect.EffectType == CommanderSkillUnitEffectType.Cleanse)
            {
                return label;
            }
            if (effect.EffectType == CommanderSkillUnitEffectType.Stun)
            {
                return $"{label} {CommanderSkillValueResolver.Duration(effect, multiplier):0.#}초";
            }

            var value = CommanderSkillValueResolver.Magnitude(effect, multiplier);
            var source = effect.ValueSource switch
            {
                CommanderSkillEffectValueSource.TargetMaxHealthRatio => "최대 체력",
                CommanderSkillEffectValueSource.TargetMissingHealthRatio => "잃은 체력",
                CommanderSkillEffectValueSource.TargetEnergyCapacityRatio => "기력 용량",
                _ => string.Empty
            };
            var percentage = effect.ValueSource != CommanderSkillEffectValueSource.Flat ||
                             effect.EffectType is CommanderSkillUnitEffectType.AttackBuff or
                                 CommanderSkillUnitEffectType.DefenseBuff or
                                 CommanderSkillUnitEffectType.AttackSpeedBuff or
                                 CommanderSkillUnitEffectType.DamageReduction or
                                 CommanderSkillUnitEffectType.DamageReflect or
                                 CommanderSkillUnitEffectType.AttackDebuff or
                                 CommanderSkillUnitEffectType.DefenseDebuff or
                                 CommanderSkillUnitEffectType.AttackSpeedDebuff or
                                 CommanderSkillUnitEffectType.MoveSpeedDebuff or
                                 CommanderSkillUnitEffectType.Slow or CommanderSkillUnitEffectType.Mark;
            var amount = percentage ? $"{value * 100f:0.#}%" : value.ToString("0.#");
            var duration = CommanderUnitEffectDefinition.RequiresDuration(effect.EffectType)
                ? $" / {CommanderSkillValueResolver.Duration(effect, multiplier):0.#}초"
                : string.Empty;
            return string.IsNullOrEmpty(source)
                ? $"{label} {amount}{duration}"
                : $"{label} {source} {amount}{duration}";
        }

        private float GetEffectMultiplier(string skillId, int level)
        {
            return TryGetGrowthRule(skillId, out var rule) ? rule.GetDamageMultiplier(level) : 1f;
        }

        private CommanderSkillGrowthSnapshot GetSupportGrowth(string skillId, int level)
        {
            var growth = TryGetGrowthRule(skillId, out var rule) ? CommanderSkillGrowthSnapshot.FromRule(rule, level) : (CommanderSkillGrowthSnapshot)1f;
            return catalog != null && catalog.TryGet(skillId, out var definition) && TryGetOwnedSkill(skillId, out var owned)
                ? growth.WithAwakening(definition.CaptureAwakening(owned.AwakeningLevel)) : growth;
        }

        private static string BuildAwakeningSummary(CommanderSkillDefinition definition, int star)
        {
            if (star >= 5) return "최대 각성 · 이후 중복은 골드로 전환됩니다.";
            if (definition.AwakeningStages.Count <= star) return "각성 특성 데이터 미설정";
            var previous = star > 0 ? definition.AwakeningStages[star - 1].CopyModifiers() : Array.Empty<CommanderSkillAwakeningModifier>();
            var changes = new HashSet<string>();
            foreach (var modifier in definition.AwakeningStages[star].CopyModifiers())
            {
                var unchanged = false;
                foreach (var old in previous)
                    if (old.Parameter == modifier.Parameter && old.TargetEffectId == modifier.TargetEffectId &&
                        old.Operation == modifier.Operation && old.Value == modifier.Value) { unchanged = true; break; }
                if (unchanged) continue;
                var label = modifier.Parameter switch
                {
                    AP.Cooldown => "기본 쿨타임", AP.TargetRange => "사거리", AP.RepeatCount => "반복",
                    AP.ChainCount => "연쇄 대상", AP.ChainRadius => "연쇄 반경", AP.AreaRadius => "효과 반경",
                    AP.LineWidth => "직선 폭", AP.MaxTargets => "최대 대상", AP.Duration => "지속시간",
                    AP.MarkRequiredHits => "각인 필요 피격", AP.MarkRequiredStacks => "각인 필요 스택",
                    AP.MarkTriggerCooldown => "각인 내부 쿨타임", _ => "기록 상한"
                };
                changes.Add(label + (modifier.Operation == CommanderSkillAwakeningOperation.Add
                    ? $" {modifier.Value:+0;-0;0}" : $" ×{modifier.Value:0.##}"));
            }
            return $"{star + 1}성: {string.Join(" · ", changes)}";
        }

        private string BuildLevelUpCostText(
            long gold,
            bool hasOwned,
            OwnedCommanderSkillView owned)
        {
            if (!hasOwned || !TryGetGrowthRule(owned.SkillId, out var rule))
            {
                return string.Empty;
            }

            if (!rule.TryGetNextLevelGoldCost(owned.Level, out var cost))
            {
                return "최대 레벨";
            }

            return gold >= cost
                ? $"{cost:N0}"
                : $"<color=#D77C73>{cost:N0} 부족</color>";
        }

        private bool TryGetGrowthRule(string skillId, out CommanderSkillGrowthRule rule)
        {
            if (catalog != null && catalog.BalanceConfig.TryGetRule(skillId, out rule))
            {
                return true;
            }

            rule = null;
            return false;
        }

        private bool MatchesFilter(CommanderSkillCategory category)
        {
            return currentFilter == SkillFilter.All ||
                   (currentFilter == SkillFilter.Attack && category == CommanderSkillCategory.Attack) ||
                   (currentFilter == SkillFilter.Buff && category == CommanderSkillCategory.Buff) ||
                   (currentFilter == SkillFilter.Debuff && category == CommanderSkillCategory.Debuff);
        }

        private bool TryGetOwnedSkill(string skillId, out OwnedCommanderSkillView owned)
        {
            if (progress != null)
            {
                var ownedSkills = progress.View.CommanderSkills.OwnedSkills;
                for (var index = 0; index < ownedSkills.Count; index++)
                {
                    if (ownedSkills[index].SkillId == skillId)
                    {
                        owned = ownedSkills[index];
                        return true;
                    }
                }
            }

            owned = default;
            return false;
        }

        private static int FindEquippedSlot(CommanderSkillProgressView view, string skillId)
        {
            for (var index = 0; index < CommanderSkillSlotRules.SlotCount; index++)
            {
                if (view.GetEquippedSkillId(index) == skillId)
                {
                    return index;
                }
            }

            return -1;
        }

        private static string GetCategoryLabel(CommanderSkillCategory category)
        {
            return category switch
            {
                CommanderSkillCategory.Buff => "버프형",
                CommanderSkillCategory.Debuff => "디버프형",
                _ => "공격형"
            };
        }

        private static Transform FindDeep(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == objectName)
            {
                return root;
            }

            for (var index = 0; index < root.childCount; index++)
            {
                var result = FindDeep(root.GetChild(index), objectName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void OnDestroy()
        {
            Shutdown();
        }
    }
}
