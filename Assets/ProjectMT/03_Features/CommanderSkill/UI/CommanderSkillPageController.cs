using System;
using System.Collections.Generic;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.CommanderSkill
{
    [DisallowMultipleComponent]
    public sealed class CommanderSkillPageController : MonoBehaviour // 장착 슬롯·보유 목록·상세 관리창
    {
        private enum SkillFilter
        {
            All,
            Attack,
            Buff,
            Debuff
        }

        private sealed class SlotView
        {
            public Button Button;
            public Image Icon;
            public TMP_Text LevelText;
            public TMP_Text NumberText;
            public GameObject LockRoot;
            public GameObject FocusRoot;
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
            public string SkillId;
        }

        private static readonly Color SelectedColor = new Color32(255, 188, 67, 255);
        private static readonly Color NormalColor = new Color32(225, 230, 238, 255);
        private static readonly Color DisabledColor = new Color32(110, 116, 126, 255);

        private readonly SlotView[] slots = new SlotView[CommanderSkillSlotRules.SlotCount];
        private readonly List<SkillCardView> cards = new List<SkillCardView>(12);
        private readonly Button[] filterButtons = new Button[4];
        private readonly TMP_Text[] filterLabels = new TMP_Text[4];
        private readonly Image[] filterBackgrounds = new Image[4];

        private Button closeButton;
        private TMP_Text ownedCountText;
        private Image detailIcon;
        private TMP_Text detailNameText;
        private TMP_Text detailCategoryText;
        private TMP_Text detailStatsText;
        private TMP_Text detailDescriptionText;
        private TMP_Text detailLevelText;
        private TMP_Text detailMaterialText;
        private TMP_Text selectedSlotText;
        private TMP_Text feedbackText;
        private Button levelUpButton;
        private TMP_Text levelUpButtonText;
        private Button equipButton;
        private TMP_Text equipButtonText;

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
                gameObject.SetActive(true);
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

            gameObject.SetActive(false);
            OpenStateChanged?.Invoke(false);
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
            equipButton?.onClick.AddListener(HandleEquipClicked);
            for (var index = 0; index < slots.Length; index++)
            {
                var slotIndex = index;
                slots[index]?.Button?.onClick.AddListener(() => SelectSlot(slotIndex));
            }

            for (var index = 0; index < cards.Count; index++)
            {
                var cardIndex = index;
                cards[index].Button?.onClick.AddListener(() => SelectCard(cardIndex));
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
            detailNameText = FindDeep(transform, "SelectedSkillName")?.GetComponent<TMP_Text>();
            detailCategoryText = FindDeep(transform, "SelectedSkillCategory")?.GetComponent<TMP_Text>();
            detailStatsText = FindDeep(transform, "SelectedSkillStats")?.GetComponent<TMP_Text>();
            detailDescriptionText = FindDeep(transform, "SelectedSkillDescription")?.GetComponent<TMP_Text>();
            detailLevelText = FindDeep(transform, "SelectedSkillLevel")?.GetComponent<TMP_Text>();
            detailMaterialText = FindDeep(transform, "SelectedSkillMaterial")?.GetComponent<TMP_Text>();
            selectedSlotText = FindDeep(transform, "SelectedSlotLabel")?.GetComponent<TMP_Text>();
            feedbackText = FindDeep(transform, "SkillPageFeedback")?.GetComponent<TMP_Text>();
            levelUpButton = FindDeep(transform, "SkillLevelUpButton")?.GetComponent<Button>();
            levelUpButtonText = levelUpButton?.GetComponentInChildren<TMP_Text>(true);
            equipButton = FindDeep(transform, "SkillEquipButton")?.GetComponent<Button>();
            equipButtonText = equipButton?.GetComponentInChildren<TMP_Text>(true);

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
                        FocusRoot = FindDeep(root, "SlotFocus")?.gameObject
                    };
            }

            for (var index = 0; index < 12; index++)
            {
                var root = FindDeep(transform, $"OwnedSkillCard_{index + 1}");
                if (root == null)
                {
                    continue;
                }

                cards.Add(new SkillCardView
                {
                    Root = root.gameObject,
                    Button = root.GetComponent<Button>(),
                    Icon = FindDeep(root, "SkillIcon")?.GetComponent<Image>(),
                    NameText = FindDeep(root, "SkillName")?.GetComponent<TMP_Text>(),
                    CategoryText = FindDeep(root, "SkillCategory")?.GetComponent<TMP_Text>(),
                    StatsText = FindDeep(root, "SkillCardStats")?.GetComponent<TMP_Text>(),
                    LevelText = FindDeep(root, "SkillLevel")?.GetComponent<TMP_Text>(),
                    EquippedRoot = FindDeep(root, "EquippedBadge")?.gameObject,
                    FocusRoot = FindDeep(root, "CardFocus")?.gameObject
                });
            }

            var filterNames = new[] { "Filter_All", "Filter_Attack", "Filter_Buff", "Filter_Debuff" };
            for (var index = 0; index < filterNames.Length; index++)
            {
                var filter = FindDeep(transform, filterNames[index]);
                filterButtons[index] = filter?.GetComponent<Button>();
                filterLabels[index] = filter?.GetComponentInChildren<TMP_Text>(true);
                filterBackgrounds[index] = filter?.GetComponent<Image>();
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
                        owned.Level,
                        owned.DuplicateCount));
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
            if (this != null && isActiveAndEnabled)
            {
                RefreshAll();
            }
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

            var view = progress.View.CommanderSkills;
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
            var view = progress.View.CommanderSkills;
            RefreshSlots(view);
            RefreshCards(view);
            RefreshFilters();
            RefreshDetail(view);
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
                slot.FocusRoot?.SetActive(index == selectedSlotIndex);
                if (slot.NumberText != null)
                {
                    slot.NumberText.text = $"{index + 1}번";
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
            var visibleCount = 0;
            for (var ownedIndex = 0; ownedIndex < view.OwnedSkills.Count && visibleCount < cards.Count; ownedIndex++)
            {
                var owned = view.OwnedSkills[ownedIndex];
                if (!catalog.TryGet(owned.SkillId, out var definition) || !MatchesFilter(definition.Category))
                {
                    continue;
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
                    card.CategoryText.text = GetCategoryLabel(definition.Category);
                }

                if (card.StatsText != null)
                {
                    card.StatsText.text = BuildCardStats(definition, owned.Level);
                }

                card.LevelText.text = $"Lv.{owned.Level}";
                card.EquippedRoot?.SetActive(FindEquippedSlot(view, owned.SkillId) >= 0);
                card.FocusRoot?.SetActive(owned.SkillId == selectedSkillId);
            }

            for (var index = visibleCount; index < cards.Count; index++)
            {
                cards[index].SkillId = string.Empty;
                cards[index].Root.SetActive(false);
            }

            if (ownedCountText != null)
            {
                ownedCountText.text = $"{visibleCount} / {view.OwnedSkills.Count}";
            }
        }

        private void RefreshFilters()
        {
            for (var index = 0; index < filterButtons.Length; index++)
            {
                var selected = index == (int)currentFilter;
                if (filterLabels[index] != null)
                {
                    filterLabels[index].color = selected ? SelectedColor : NormalColor;
                }

                if (filterBackgrounds[index] != null)
                {
                    filterBackgrounds[index].color = selected
                        ? new Color32(78, 59, 30, 245)
                        : new Color32(32, 36, 43, 235);
                }
            }
        }

        private void RefreshDetail(CommanderSkillProgressView view)
        {
            var hasOwned = TryGetOwnedSkill(selectedSkillId, out var owned);
            CommanderSkillDefinition definition = null;
            var hasDefinition = hasOwned && catalog.TryGet(selectedSkillId, out definition);
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
                detailCategoryText.text = hasDefinition ? GetCategoryLabel(definition.Category) : string.Empty;
            }

            if (detailStatsText != null)
            {
                detailStatsText.text = hasDefinition ? BuildDetailStats(definition, owned.Level) : string.Empty;
            }

            if (detailDescriptionText != null)
            {
                detailDescriptionText.text = hasDefinition ? definition.Description : string.Empty;
            }

            if (detailLevelText != null)
            {
                detailLevelText.text = hasOwned && TryGetGrowthRule(owned.SkillId, out var growthRule)
                    ? $"Lv.{owned.Level} / {growthRule.MaxLevel}"
                    : string.Empty;
            }

            if (detailMaterialText != null)
            {
                detailMaterialText.text = hasOwned && TryGetGrowthRule(owned.SkillId, out var materialRule)
                    ? $"중복 재료 {owned.DuplicateCount} / {materialRule.RequiredDuplicateCount}"
                    : string.Empty;
            }

            if (selectedSlotText != null)
            {
                selectedSlotText.text = $"선택 슬롯  {selectedSlotIndex + 1}번";
            }

            var canLevelUp = hasOwned && TryGetGrowthRule(owned.SkillId, out var levelRule) &&
                             owned.Level < levelRule.MaxLevel &&
                             owned.DuplicateCount >= levelRule.RequiredDuplicateCount && !requestInFlight;
            if (levelUpButton != null)
            {
                levelUpButton.interactable = canLevelUp;
            }

            if (levelUpButtonText != null)
            {
                levelUpButtonText.text = hasOwned && TryGetGrowthRule(owned.SkillId, out var capRule) &&
                                             owned.Level >= capRule.MaxLevel
                    ? "최대 레벨"
                    : "레벨업";
            }

            var slotUnlocked = view.IsSlotUnlocked(selectedSlotIndex);
            var currentSkillId = view.GetEquippedSkillId(selectedSlotIndex);
            var isSameSkill = currentSkillId == selectedSkillId;
            if (equipButton != null)
            {
                equipButton.interactable = hasOwned && slotUnlocked && !isSameSkill && !requestInFlight;
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

        private string BuildCardStats(CommanderSkillDefinition definition, int level)
        {
            if (definition is CommanderAttackSkillDefinition attack && attack.AreaDamageEffect != null)
            {
                var damage = attack.AreaDamageEffect.BaseDamage * GetEffectMultiplier(definition.SkillId, level);
                return $"피해 {damage:0.#}  ·  {definition.Cooldown:0.#}초";
            }

            return $"쿨타임 {definition.Cooldown:0.#}초";
        }

        private string BuildDetailStats(CommanderSkillDefinition definition, int level)
        {
            if (definition is CommanderAttackSkillDefinition attack && attack.AreaDamageEffect != null)
            {
                var damage = attack.AreaDamageEffect.BaseDamage * GetEffectMultiplier(definition.SkillId, level);
                return $"피해 {damage:0.#}  ·  쿨타임 {definition.Cooldown:0.#}초  ·  범위 {attack.AreaDamageEffect.Radius:0.#}m";
            }

            return $"쿨타임 {definition.Cooldown:0.#}초  ·  대상 거리 {definition.TargetRange:0.#}m";
        }

        private float GetEffectMultiplier(string skillId, int level)
        {
            return TryGetGrowthRule(skillId, out var rule) ? rule.GetDamageMultiplier(level) : 1f;
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
