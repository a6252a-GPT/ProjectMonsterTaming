using System;
using System.Collections.Generic;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProjectMT.Features.Inventory
{
    [DisallowMultipleComponent]
    public sealed class ItemInventoryPageController : MonoBehaviour // 일반 아이템 목록·상세·사용 연결
    {
        private const string GradeFrameTemplateRootName = "ItemGradeFrameTemplates";
        private const string FrameVariantPrefix = ItemGradeFramePalette.FrameVariantPrefix;
        private const string QuantityHitAreaName = "HitArea_48";

        private static readonly Dictionary<ItemGrade, string> FrameVariantSuffixByGrade =
            new Dictionary<ItemGrade, string>
            {
                { ItemGrade.Common, ItemGradeFramePalette.GetSuffix(ItemGrade.Common) },
                { ItemGrade.Rare, ItemGradeFramePalette.GetSuffix(ItemGrade.Rare) },
                { ItemGrade.Epic, ItemGradeFramePalette.GetSuffix(ItemGrade.Epic) },
                { ItemGrade.Legendary, ItemGradeFramePalette.GetSuffix(ItemGrade.Legendary) },
                { ItemGrade.Mythic, ItemGradeFramePalette.GetSuffix(ItemGrade.Mythic) }
            };

        private static readonly ItemCategory?[] FilterCategories =
        {
            null,
            ItemCategory.Consumable,
            ItemCategory.Currency,
            ItemCategory.SummonTicket,
            ItemCategory.DungeonKey,
            ItemCategory.UpgradeMaterial
        };

        private static readonly string[] FilterLabels =
        {
            "전체",
            "소비",
            "재화",
            "소환권",
            "던전 열쇠",
            "강화 재료"
        };

        private static readonly Color SelectedFilterColor = new Color32(244, 197, 72, 255);
        private static readonly Color NormalFilterColor = new Color32(236, 232, 225, 255);

        [Header("페이지")]
        [SerializeField] private GameObject inventoryPanel;
        [SerializeField] private GameObject detailPanel;
        [SerializeField] private Button inventoryCloseButton;
        [SerializeField] private Button detailCloseButton;
        [SerializeField] private TMP_Text capacityText;
        [SerializeField] private RectTransform slotContent;
        [SerializeField] private Button[] filterButtons = Array.Empty<Button>();

        [Header("상세 정보")]
        [SerializeField] private GameObject detailItemVisual;
        [SerializeField] private Image detailItemIcon;
        [SerializeField] private TMP_Text rarityText;
        [SerializeField] private TMP_Text ownedCountText;
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private TMP_Text itemTypeText;
        [SerializeField] private TMP_Text primaryEffectLabel;
        [SerializeField] private TMP_Text primaryEffectValue;
        [SerializeField] private TMP_Text optionOneText;
        [SerializeField] private TMP_Text optionTwoText;
        [SerializeField] private TMP_Text descriptionText;

        [Header("아이템 행동")]
        [SerializeField] private GameObject itemActionArea;
        [SerializeField] private GameObject quantityGaugeRoot;
        [SerializeField] private Slider quantitySlider;
        [SerializeField] private TMP_Text quantityRangeText;
        [SerializeField] private Button discardPairButton;
        [SerializeField] private Button discardSoloButton;
        [SerializeField] private Button useButton;

        [Header("임시 카테고리 아이콘")]
        [SerializeField] private Sprite[] categoryFallbackIcons = Array.Empty<Sprite>();

        private readonly List<SlotBinding> slots = new List<SlotBinding>();
        private readonly Dictionary<ItemGrade, GameObject> gradeFrameTemplates =
            new Dictionary<ItemGrade, GameObject>();
        private IGameProgressService progress;
        private ItemCatalog catalog;
        private IReadOnlyList<ItemInventoryEntryView> visibleEntries = Array.Empty<ItemInventoryEntryView>();
        private ItemCategory? currentFilter;
        private string selectedItemId;
        private bool referencesReady;
        private bool actionPending;
        private Vector2 pairedUsePosition;
        private Vector2 soloActionPosition;

        public event Action<bool> OpenStateChanged;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnDestroy()
        {
            DetachProgress();
            RemoveUiListeners();
        }

        public void Configure(IGameProgressService progressService, ItemCatalog itemCatalog)
        {
            EnsureReferences();
            DetachProgress();
            progress = progressService;
            catalog = itemCatalog;
            if (progress != null)
            {
                progress.Changed += HandleProgressChanged;
            }

            currentFilter = null;
            selectedItemId = null;
            Refresh();
            Close();
        }

        public void Shutdown()
        {
            Close();
            DetachProgress();
            catalog = null;
            visibleEntries = Array.Empty<ItemInventoryEntryView>();
            selectedItemId = null;
        }

        public void Open()
        {
            EnsureReferences();
            if (progress == null || catalog == null)
            {
                Debug.LogWarning("ItemInventoryPageController: 진행 데이터와 ItemCatalog 연결이 필요합니다.", this);
                return;
            }

            var changed = !gameObject.activeSelf;
            UIPanelPopAnimator.RequestOpen(gameObject);
            inventoryPanel?.SetActive(true);
            Refresh();
            if (changed)
            {
                OpenStateChanged?.Invoke(true);
            }
        }

        public void Close()
        {
            var changed = gameObject.activeSelf;
            selectedItemId = null;
            detailPanel?.SetActive(false);
            SetSlotSelection(-1);
            UIPanelPopAnimator.RequestClose(gameObject, () =>
            {
                if (changed)
                {
                    OpenStateChanged?.Invoke(false);
                }
            });
        }

        private void EnsureReferences()
        {
            if (referencesReady)
            {
                return;
            }

            if (inventoryPanel == null || detailPanel == null || inventoryCloseButton == null ||
                detailCloseButton == null || capacityText == null || slotContent == null ||
                filterButtons == null || filterButtons.Length != FilterCategories.Length)
            {
                Debug.LogError("ItemInventoryPageController: 필수 UI 참조가 비어 있습니다.", this);
                return;
            }

            inventoryCloseButton.onClick.AddListener(Close);
            detailCloseButton.onClick.AddListener(CloseDetail);
            ConfigureFilterButtons();
            filterButtons[0]?.onClick.AddListener(HandleFilterAll);
            filterButtons[1]?.onClick.AddListener(HandleFilterConsumable);
            filterButtons[2]?.onClick.AddListener(HandleFilterCurrency);
            filterButtons[3]?.onClick.AddListener(HandleFilterOtherOne);
            filterButtons[4]?.onClick.AddListener(HandleFilterOtherTwo);
            filterButtons[5]?.onClick.AddListener(HandleFilterOtherThree);
            EnsureSliderHitArea(quantitySlider);
            EnsureFullRectHitArea(discardPairButton);
            EnsureFullRectHitArea(discardSoloButton);
            EnsureFullRectHitArea(useButton);
            quantitySlider?.onValueChanged.AddListener(HandleQuantityChanged);
            discardPairButton?.onClick.AddListener(DiscardSelected);
            discardSoloButton?.onClick.AddListener(DiscardSelected);
            useButton?.onClick.AddListener(UseSelected);

            if (useButton != null)
            {
                pairedUsePosition = ((RectTransform)useButton.transform).anchoredPosition;
            }

            if (discardSoloButton != null)
            {
                soloActionPosition = ((RectTransform)discardSoloButton.transform).anchoredPosition;
            }

            CacheGradeFrameTemplates();
            BuildSlotBindings();
            referencesReady = slots.Count > 0;
            if (!referencesReady)
            {
                Debug.LogError("ItemInventoryPageController: 인벤토리 슬롯을 찾지 못했습니다.", this);
                RemoveUiListeners();
                return;
            }

            CloseDetail();
        }

        private void BuildSlotBindings()
        {
            slots.Clear();
            for (var index = 0; index < slotContent.childCount; index++)
            {
                var child = slotContent.GetChild(index);
                if (!child.name.StartsWith("InventorySlotCell_", StringComparison.Ordinal))
                {
                    continue;
                }

                var button = child.GetComponent<Button>();
                var iconRoot = FindDescendant(child, "Item");
                var quantityRoot = FindDescendant(child, "Quantity");
                var selectionRoot = FindDescendant(child, "Check");
                if (button == null || iconRoot == null || quantityRoot == null || selectionRoot == null)
                {
                    continue;
                }

                var slot = new SlotBinding(
                    child.gameObject,
                    button,
                    iconRoot.GetComponent<Image>(),
                    quantityRoot.GetComponent<TMP_Text>(),
                    selectionRoot.gameObject,
                    FindDescendant(child, "NormalArea"),
                    FindDescendant(child, "Add_1")?.gameObject,
                    FindDescendant(child, "Add_2")?.gameObject,
                    FindDescendant(child, "Lock")?.gameObject,
                    FindDescendant(child, "Text_Level")?.gameObject,
                    gradeFrameTemplates);
                var capturedIndex = slots.Count;
                slot.ClickAction = () => SelectSlot(capturedIndex);
                button.onClick.AddListener(slot.ClickAction);
                slots.Add(slot);
            }
        }

        private void Refresh()
        {
            if (this == null || !referencesReady || progress == null || catalog == null)
            {
                return;
            }

            if (!EnsureSlotsAlive())
            {
                return;
            }

            var allEntries = ItemInventoryProjection.Build(progress.View.Items, catalog);
            visibleEntries = currentFilter.HasValue
                ? ItemInventoryProjection.Build(progress.View.Items, catalog, currentFilter)
                : allEntries;
            if (capacityText != null)
            {
                capacityText.text = $"{allEntries.Count:N0} / {slots.Count:N0}";
            }

            for (var index = 0; index < slots.Count; index++)
            {
                if (index < visibleEntries.Count)
                {
                    slots[index].Bind(visibleEntries[index], ResolveIcon(visibleEntries[index].Definition));
                }
                else
                {
                    slots[index].Clear();
                }
            }

            UpdateFilterVisuals();
            var selectedIndex = FindVisibleIndex(selectedItemId);
            if (selectedIndex >= 0)
            {
                ShowDetail(selectedIndex, false);
            }
            else
            {
                CloseDetail();
            }
        }

        private void SelectSlot(int index)
        {
            if (index < 0 || index >= visibleEntries.Count)
            {
                return;
            }

            ShowDetail(index, true);
        }

        private void ShowDetail(int index, bool resetQuantity)
        {
            var entry = visibleEntries[index];
            var definition = entry.Definition;
            if (definition == null)
            {
                CloseDetail();
                return;
            }

            selectedItemId = definition.ItemId;
            detailPanel.SetActive(true);
            detailItemVisual?.SetActive(true);
            if (detailItemIcon != null)
            {
                detailItemIcon.sprite = ResolveIcon(definition);
                detailItemIcon.enabled = detailItemIcon.sprite != null;
            }

            rarityText.text = $"{GetGradeDisplayName(definition.Grade)} 아이템";
            ownedCountText.text = $"보유 {entry.Quantity:N0}";
            itemNameText.text = definition.DisplayName;
            itemTypeText.text = GetCategoryLabel(definition.Category);
            primaryEffectLabel.text = definition.IsUsable ? "사용 효과" : "주요 용도";
            primaryEffectValue.text = definition.IsUsable ? "사용 시 즉시 적용" : GetCategoryPurpose(definition.Category);
            optionOneText.text = definition.AllowMultiUse
                ? "여러 개 한 번에 사용 가능"
                : definition.IsUsable
                    ? "1개씩 사용 가능"
                    : "전용 콘텐츠에서 사용";
            optionTwoText.text = definition.IsDiscardable ? "버리기 가능" : "버리기 불가";
            descriptionText.text = string.IsNullOrWhiteSpace(definition.Description)
                ? "아이템 설명이 아직 등록되지 않았습니다."
                : definition.Description;

            if (resetQuantity && quantitySlider != null)
            {
                quantitySlider.SetValueWithoutNotify(quantitySlider.minValue); // 프리팹 범위의 최소 수량으로 초기화
            }

            ConfigureActions(entry);
            SetSlotSelection(index);
        }

        private void ConfigureActions(ItemInventoryEntryView entry)
        {
            var canUse = entry.CanUse;
            var canDiscard = entry.CanDiscard;
            itemActionArea?.SetActive(canUse || canDiscard);
            quantityGaugeRoot?.SetActive(entry.ShowQuantityGauge);
            discardPairButton?.gameObject.SetActive(canUse && canDiscard);
            discardSoloButton?.gameObject.SetActive(!canUse && canDiscard);
            useButton?.gameObject.SetActive(canUse);

            if (useButton != null)
            {
                ((RectTransform)useButton.transform).anchoredPosition = canDiscard
                    ? pairedUsePosition
                    : soloActionPosition;
                useButton.interactable = canUse && !actionPending;
            }

            if (discardPairButton != null)
            {
                discardPairButton.interactable = canDiscard && !actionPending;
            }

            if (discardSoloButton != null)
            {
                discardSoloButton.interactable = canDiscard && !actionPending;
            }

            UpdateQuantityText(entry);
        }

        private async void UseSelected()
        {
            await ApplySelectedItemAction(true);
        }

        private async void DiscardSelected()
        {
            await ApplySelectedItemAction(false);
        }

        private async System.Threading.Tasks.Task ApplySelectedItemAction(bool use)
        {
            if (actionPending || progress == null || string.IsNullOrWhiteSpace(selectedItemId))
            {
                return;
            }

            var selectedIndex = FindVisibleIndex(selectedItemId);
            if (selectedIndex < 0)
            {
                return;
            }

            var entry = visibleEntries[selectedIndex];
            if ((use && !entry.CanUse) || (!use && !entry.CanDiscard))
            {
                return;
            }

            var quantity = GetSelectedQuantity(entry);
            var service = progress;
            actionPending = true;
            ConfigureActions(entry);
            try
            {
                var change = use
                    ? GameProgressChange.UseItem(selectedItemId, quantity, entry.Quantity)
                    : GameProgressChange.DiscardItem(selectedItemId, quantity, entry.Quantity);
                if (!await service.TryApplyAndSaveAsync(change))
                {
                    Debug.LogWarning($"ItemInventoryPageController: 아이템 {(use ? "사용" : "버리기")} 적용에 실패했습니다. Item={selectedItemId}", this);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                actionPending = false;
                if (this != null)
                {
                    Refresh();
                }
            }
        }

        private long GetSelectedQuantity(ItemInventoryEntryView entry)
        {
            if (!entry.ShowQuantityGauge || quantitySlider == null)
            {
                return 1L;
            }

            var scaled = (entry.Quantity - 1L) * (double)quantitySlider.normalizedValue;
            return Math.Min(entry.Quantity, 1L + (long)Math.Round(scaled, MidpointRounding.AwayFromZero));
        }

        private void HandleQuantityChanged(float _)
        {
            var selectedIndex = FindVisibleIndex(selectedItemId);
            if (selectedIndex >= 0)
            {
                UpdateQuantityText(visibleEntries[selectedIndex]);
            }
        }

        private void UpdateQuantityText(ItemInventoryEntryView entry)
        {
            if (quantityRangeText != null)
            {
                quantityRangeText.text = $"{GetSelectedQuantity(entry):N0} / {entry.Quantity:N0}";
            }
        }

        private void CloseDetail()
        {
            selectedItemId = null;
            detailPanel?.SetActive(false);
            SetSlotSelection(-1);
        }

        private void SetSlotSelection(int selectedIndex)
        {
            for (var index = 0; index < slots.Count; index++)
            {
                if (slots[index].IsAlive)
                {
                    slots[index].SetSelected(index == selectedIndex);
                }
            }
        }

        private void SetFilter(ItemCategory? category)
        {
            currentFilter = category;
            selectedItemId = null;
            Refresh();
        }

        private void HandleFilterAll() => SetFilter(FilterCategories[0]);
        private void HandleFilterConsumable() => SetFilter(FilterCategories[1]);
        private void HandleFilterCurrency() => SetFilter(FilterCategories[2]);
        private void HandleFilterOtherOne() => SetFilter(FilterCategories[3]);
        private void HandleFilterOtherTwo() => SetFilter(FilterCategories[4]);
        private void HandleFilterOtherThree() => SetFilter(FilterCategories[5]);

        private void ConfigureFilterButtons()
        {
            for (var index = 0; index < filterButtons.Length; index++)
            {
                var button = filterButtons[index];
                if (button == null)
                {
                    continue;
                }

                EnsureFullRectHitArea(button);
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = FilterLabels[index];
                    label.gameObject.SetActive(true);
                }

                var icon = button.transform.Find("Icon");
                icon?.gameObject.SetActive(false); // 필터는 아이콘 대신 텍스트 사용
            }
        }

        private static void EnsureFullRectHitArea(Button button)
        {
            if (button == null)
            {
                return;
            }

            var hitArea = button.GetComponent<Graphic>();
            if (hitArea == null)
            {
                var image = button.gameObject.AddComponent<Image>();
                image.color = Color.clear;
                hitArea = image;
            }

            hitArea.raycastTarget = true;
            if (button.targetGraphic == null)
            {
                button.targetGraphic = hitArea;
            }
        }

        private static void EnsureSliderHitArea(Slider slider)
        {
            if (slider == null)
            {
                return;
            }

            var hitArea = slider.transform.Find(QuantityHitAreaName) as RectTransform;
            if (hitArea == null)
            {
                var hitAreaObject = new GameObject(QuantityHitAreaName, typeof(RectTransform), typeof(Image));
                hitArea = (RectTransform)hitAreaObject.transform;
                hitArea.SetParent(slider.transform, false);
                hitArea.SetAsFirstSibling();
            }

            hitArea.anchorMin = Vector2.zero;
            hitArea.anchorMax = Vector2.one;
            hitArea.pivot = new Vector2(0.5f, 0.5f);
            hitArea.offsetMin = new Vector2(0f, -12f);
            hitArea.offsetMax = new Vector2(0f, 12f); // 24px Slider를 최소 48px 터치 영역으로 확장

            var image = hitArea.GetComponent<Image>() ?? hitArea.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
        }

        private void UpdateFilterVisuals()
        {
            for (var index = 0; index < filterButtons.Length; index++)
            {
                var button = filterButtons[index];
                if (button == null)
                {
                    continue;
                }

                var selected = FilterCategories[index] == currentFilter;
                button.interactable = true;
                var focus = button.transform.Find("Focus");
                focus?.gameObject.SetActive(selected);
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.color = selected ? SelectedFilterColor : NormalFilterColor;
                }
            }
        }

        private void HandleProgressChanged()
        {
            if (this == null || !isActiveAndEnabled)
            {
                return;
            }

            Refresh();
        }

        private bool EnsureSlotsAlive()
        {
            if (slotContent == null)
            {
                return false;
            }

            var needsRebuild = slots.Count == 0;
            if (!needsRebuild)
            {
                for (var index = 0; index < slots.Count; index++)
                {
                    if (!slots[index].IsAlive)
                    {
                        needsRebuild = true;
                        break;
                    }
                }
            }

            if (!needsRebuild)
            {
                return true;
            }

            for (var index = 0; index < slots.Count; index++)
            {
                slots[index].RemoveListener();
            }

            BuildSlotBindings();
            referencesReady = slots.Count > 0;
            return referencesReady;
        }

        private void DetachProgress()
        {
            if (progress != null)
            {
                progress.Changed -= HandleProgressChanged;
            }

            progress = null;
        }

        private void RemoveUiListeners()
        {
            inventoryCloseButton?.onClick.RemoveListener(Close);
            detailCloseButton?.onClick.RemoveListener(CloseDetail);
            if (filterButtons != null && filterButtons.Length == FilterCategories.Length)
            {
                filterButtons[0]?.onClick.RemoveListener(HandleFilterAll);
                filterButtons[1]?.onClick.RemoveListener(HandleFilterConsumable);
                filterButtons[2]?.onClick.RemoveListener(HandleFilterCurrency);
                filterButtons[3]?.onClick.RemoveListener(HandleFilterOtherOne);
                filterButtons[4]?.onClick.RemoveListener(HandleFilterOtherTwo);
                filterButtons[5]?.onClick.RemoveListener(HandleFilterOtherThree);
            }

            quantitySlider?.onValueChanged.RemoveListener(HandleQuantityChanged);
            discardPairButton?.onClick.RemoveListener(DiscardSelected);
            discardSoloButton?.onClick.RemoveListener(DiscardSelected);
            useButton?.onClick.RemoveListener(UseSelected);
            for (var index = 0; index < slots.Count; index++)
            {
                slots[index].RemoveListener();
            }
        }

        private int FindVisibleIndex(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return -1;
            }

            for (var index = 0; index < visibleEntries.Count; index++)
            {
                if (string.Equals(
                        visibleEntries[index].Definition?.ItemId,
                        itemId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private Sprite ResolveIcon(ItemDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            if (definition.Icon != null)
            {
                return definition.Icon;
            }

            var index = (int)definition.Category;
            return categoryFallbackIcons != null && index >= 0 && index < categoryFallbackIcons.Length
                ? categoryFallbackIcons[index]
                : null;
        }

        private static string GetCategoryLabel(ItemCategory category)
        {
            return category switch
            {
                ItemCategory.Consumable => "소비 아이템",
                ItemCategory.Currency => "재화",
                ItemCategory.SummonTicket => "소환권",
                ItemCategory.DungeonKey => "입장 열쇠",
                ItemCategory.UpgradeMaterial => "강화 재료",
                _ => "기타"
            };
        }

        private static string GetGradeDisplayName(ItemGrade grade)
        {
            return grade switch
            {
                ItemGrade.Common => "일반",
                ItemGrade.Rare => "희귀",
                ItemGrade.Epic => "영웅",
                ItemGrade.Legendary => "전설",
                ItemGrade.Mythic => "신화",
                _ => grade.ToString()
            };
        }

        private static string GetCategoryPurpose(ItemCategory category)
        {
            return category switch
            {
                ItemCategory.Currency => "성장·상점 결제",
                ItemCategory.SummonTicket => "소환 시 사용",
                ItemCategory.DungeonKey => "콘텐츠 입장",
                ItemCategory.UpgradeMaterial => "성장·강화",
                _ => "소지품"
            };
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            for (var index = 0; index < root.childCount; index++)
            {
                var child = root.GetChild(index);
                if (child.name == objectName)
                {
                    return child;
                }

                var nested = FindDescendant(child, objectName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void CacheGradeFrameTemplates()
        {
            gradeFrameTemplates.Clear();
            var templateRoot = FindDescendant(transform, GradeFrameTemplateRootName);
            if (templateRoot == null)
            {
                Debug.LogError($"ItemInventoryPageController: {GradeFrameTemplateRootName}를 찾지 못했습니다.", this);
                return;
            }

            foreach (var pair in FrameVariantSuffixByGrade)
            {
                var template = FindDescendant(templateRoot, FrameVariantPrefix + pair.Value);
                if (template != null)
                {
                    gradeFrameTemplates[pair.Key] = template.gameObject;
                }
            }

            if (gradeFrameTemplates.Count != FrameVariantSuffixByGrade.Count)
            {
                Debug.LogError("ItemInventoryPageController: 일반 아이템 등급 프레임 5종 연결이 필요합니다.", this);
            }
        }

        private sealed class SlotBinding
        {
            private readonly GameObject root;
            private readonly Button button;
            private readonly Image icon;
            private readonly TMP_Text quantity;
            private readonly GameObject selection;
            private readonly Transform normalArea;
            private readonly GameObject addOne;
            private readonly GameObject addTwo;
            private readonly GameObject lockRoot;
            private readonly GameObject levelRoot;
            private readonly IReadOnlyDictionary<ItemGrade, GameObject> gradeFrameTemplates;
            private readonly GameObject emptyFrame;
            private readonly Image[] emptyFrameImages;
            private readonly Color[] emptyFrameColors;
            private GameObject currentFrame;
            private Image[] frameImages = Array.Empty<Image>();
            private Color[] frameColors = Array.Empty<Color>();
            private bool frameDimmed;

            public SlotBinding(
                GameObject slotRoot,
                Button slotButton,
                Image itemIcon,
                TMP_Text quantityText,
                GameObject selectionRoot,
                Transform normalRoot,
                GameObject addOneRoot,
                GameObject addTwoRoot,
                GameObject lockObject,
                GameObject levelObject,
                IReadOnlyDictionary<ItemGrade, GameObject> frameTemplates)
            {
                root = slotRoot;
                button = slotButton;
                icon = itemIcon;
                quantity = quantityText;
                selection = selectionRoot;
                normalArea = normalRoot;
                addOne = addOneRoot;
                addTwo = addTwoRoot;
                lockRoot = lockObject;
                levelRoot = levelObject;
                gradeFrameTemplates = frameTemplates;
                emptyFrame = normalRoot != null && normalRoot.childCount > 0
                    ? normalRoot.GetChild(0).gameObject
                    : null;
                emptyFrameImages = emptyFrame != null
                    ? emptyFrame.GetComponentsInChildren<Image>(true)
                    : Array.Empty<Image>();
                emptyFrameColors = new Color[emptyFrameImages.Length];
                for (var index = 0; index < emptyFrameImages.Length; index++)
                {
                    emptyFrameColors[index] = emptyFrameImages[index].color;
                }

                currentFrame = emptyFrame;
                CacheFrameColors();
            }

            public UnityAction ClickAction { get; set; }

            public bool IsAlive => root != null;

            public void Bind(ItemInventoryEntryView entry, Sprite itemIcon)
            {
                if (root == null)
                {
                    return;
                }

                root.SetActive(true);
                button.interactable = entry.Definition != null;
                normalArea?.gameObject.SetActive(true);
                addOne?.SetActive(false);
                addTwo?.SetActive(false);
                lockRoot?.SetActive(false);
                levelRoot?.SetActive(false);
                selection.SetActive(false);
                if (entry.Definition != null)
                {
                    ApplyFrameVariant(entry.Definition.Grade);
                }
                SetFrameDimmed(false);
                icon.sprite = itemIcon;
                icon.enabled = itemIcon != null;
                quantity.text = entry.Quantity > 0L ? $"x{entry.Quantity:N0}" : string.Empty;
                quantity.gameObject.SetActive(entry.Quantity > 0L);
            }

            public void Clear()
            {
                if (root == null)
                {
                    return;
                }

                root.SetActive(true);
                button.interactable = false;
                normalArea?.gameObject.SetActive(true);
                addOne?.SetActive(false);
                addTwo?.SetActive(false);
                lockRoot?.SetActive(false);
                levelRoot?.SetActive(false);
                selection.SetActive(false);
                ShowEmptyFrame();
                icon.sprite = null;
                icon.enabled = false;
                quantity.text = string.Empty;
                quantity.gameObject.SetActive(false);
            }

            public void SetSelected(bool selected)
            {
                if (selection == null || button == null)
                {
                    return;
                }

                selection.SetActive(selected && button.interactable);
            }

            public void RemoveListener()
            {
                if (ClickAction != null && button != null)
                {
                    button.onClick.RemoveListener(ClickAction);
                }
            }

            private void SetFrameDimmed(bool dimmed)
            {
                if (frameDimmed == dimmed)
                {
                    return;
                }

                const float dimFactor = 0.26f;
                for (var index = 0; index < frameImages.Length; index++)
                {
                    var original = frameColors[index];
                    frameImages[index].color = dimmed
                        ? new Color(
                            original.r * dimFactor,
                            original.g * dimFactor,
                            original.b * dimFactor,
                            original.a)
                        : original;
                }

                frameDimmed = dimmed;
            }

            private void ApplyFrameVariant(ItemGrade grade)
            {
                if (normalArea == null || gradeFrameTemplates == null ||
                    !FrameVariantSuffixByGrade.TryGetValue(grade, out var suffix) ||
                    !gradeFrameTemplates.TryGetValue(grade, out var template) || template == null)
                {
                    return;
                }

                var desiredName = FrameVariantPrefix + suffix;
                if (currentFrame != null && currentFrame.name == desiredName && !frameDimmed)
                {
                    return;
                }

                if (currentFrame != null)
                {
                    currentFrame.SetActive(false);
                    if (currentFrame != emptyFrame)
                    {
                        UnityEngine.Object.Destroy(currentFrame);
                    }
                }

                currentFrame = UnityEngine.Object.Instantiate(template, normalArea);
                currentFrame.name = desiredName;
                currentFrame.SetActive(true);

                var rect = currentFrame.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                }

                frameDimmed = false;
                CacheFrameColors();
            }

            private void ShowEmptyFrame()
            {
                if (currentFrame != null && currentFrame != emptyFrame)
                {
                    currentFrame.SetActive(false);
                    UnityEngine.Object.Destroy(currentFrame);
                }

                currentFrame = emptyFrame;
                frameImages = emptyFrameImages;
                frameColors = emptyFrameColors;
                frameDimmed = false;
                if (emptyFrame != null)
                {
                    emptyFrame.SetActive(true);
                }

                SetFrameDimmed(true);
            }

            private void CacheFrameColors()
            {
                frameImages = currentFrame != null
                    ? currentFrame.GetComponentsInChildren<Image>(true)
                    : Array.Empty<Image>();
                frameColors = new Color[frameImages.Length];
                for (var index = 0; index < frameImages.Length; index++)
                {
                    frameColors[index] = frameImages[index].color;
                }
            }
        }

    }
}
