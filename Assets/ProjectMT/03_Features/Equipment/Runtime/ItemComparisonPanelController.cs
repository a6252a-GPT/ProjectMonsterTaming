using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Equipment
{
    // 장비 인벤토리에서 아이템을 클릭하면 뜨는 상세 옵션 팝업(PF_ItemComparison_2) 컨트롤러.
    // Popup_1: 같은 부위에 장착 중인 장비가 있으면 항상 그 장비를 보여준다(없으면 클릭한 장비를 보여줌).
    // Popup_2: 장착 중인 장비와 다른 걸 클릭했을 때만 추가로 켜서, 새로 클릭한 장비를 장착 중인
    // 장비와 비교(추가옵션 우/열세·신규, 전투력 변화)해서 보여준다.
    [DisallowMultipleComponent]
    public sealed class ItemComparisonPanelController : MonoBehaviour
    {
        private const int MaxCoreOptionRows = 3;
        private const int MaxBonusOptionRows = 4;
        private const int MaxCompareOptionRows = 4;
        private const string EquippedRibbonLabel = "장착된 장비";
        private const string SelectedRibbonLabel = "선택한 장비";

        private Transform popup1;
        private Transform popup2;
        private Transform dimmed;
        private Image decoLineImage;
        private Image frameBgImage;
        private Image frameInnerBorderImage;
        private Image iconImage;
        private TMP_Text itemNameText;
        private TMP_Text gradeText;
        private TMP_Text partText;
        private readonly List<Transform> coreOptionRows = new List<Transform>();
        private readonly List<TMP_Text> coreOptionNameTexts = new List<TMP_Text>();
        private readonly List<TMP_Text> coreOptionValueTexts = new List<TMP_Text>();
        private readonly List<Transform> bonusOptionRows = new List<Transform>();
        private readonly List<TMP_Text> bonusOptionNameTexts = new List<TMP_Text>();
        private readonly List<TMP_Text> bonusOptionValueTexts = new List<TMP_Text>();

        // Popup_1 상단 "장착된 장비"/"선택한 장비" 리본
        private Transform equippedRibbon;
        private TMP_Text equippedRibbonText;
        private TMP_Text equippedRibbonTextShadow;

        private Image compareDecoLineImage;
        private Image compareFrameBgImage;
        private Image compareFrameInnerBorderImage;
        private Image compareIconImage;
        private TMP_Text compareItemNameText;
        private TMP_Text compareGradeText;
        private TMP_Text comparePartText;
        private TMP_Text comparePowerDeltaText;
        private readonly List<Transform> compareCoreOptionRows = new List<Transform>();
        private readonly List<TMP_Text> compareCoreOptionNameTexts = new List<TMP_Text>();
        private readonly List<TMP_Text> compareCoreOptionValueTexts = new List<TMP_Text>();
        private readonly List<RectTransform> compareCoreOptionArrowIcons = new List<RectTransform>();
        private readonly List<Transform> compareOptionRows = new List<Transform>();
        private readonly List<TMP_Text> compareOptionNameTexts = new List<TMP_Text>();
        private readonly List<TMP_Text> compareOptionValueTexts = new List<TMP_Text>();
        private readonly List<RectTransform> compareOptionArrowIcons = new List<RectTransform>();
        private readonly List<GameObject> compareOptionNewBadges = new List<GameObject>();

        // Popup_1/Popup_2 공통 "장착" 버튼(Group_Buttons)
        private Transform popup1EquipButtonRoot;
        private Transform popup2EquipButtonRoot;
        private Button popup1EquipButton;
        private Button popup2EquipButton;
        private TMP_Text popup1EquipButtonText;
        private TMP_Text popup2EquipButtonText;
        private RectTransform closeButtonRoot;
        private Button closeButton;
        private Button dimmedButton;

        private bool referencesResolved;
        private string currentInstanceId;
        private EquipmentPart currentPart;
        private bool currentItemIsEquipped;
        private bool equipRequestInFlight;
        private Action onEquipped;

        // MainBattleSceneRoot -> EquipmentPageController를 거쳐 내려오는 콜백. 장착/교체 후
        // 다음 전투용 파티 스탯을 즉시 갱신하기 위해 기존 장착 버튼과 동일하게 호출해준다.
        public void Configure(Action onEquippedCallback)
        {
            onEquipped = onEquippedCallback;
        }

        public void Show(EquipmentItemView clickedItem, Sprite icon)
        {
            if (clickedItem.Definition == null)
            {
                return;
            }

            EnsureReferences();
            currentInstanceId = clickedItem.InstanceId;
            currentPart = clickedItem.Part;
            currentItemIsEquipped = clickedItem.IsEquipped;
            gameObject.SetActive(true);
            popup1?.gameObject.SetActive(true);
            dimmed?.gameObject.SetActive(true);

            var hasComparison = EquipmentInventoryRuntime.TryGetEquipped(clickedItem.Part, out var equippedItem)
                && equippedItem.Definition != null
                && equippedItem.InstanceId != clickedItem.InstanceId;

            var referenceItem = hasComparison ? equippedItem : clickedItem;
            ApplyPopup1(referenceItem, icon);
            // Popup_1/Popup_2/닫기 버튼 위치는 코드로 옮기지 않고 프리팹에 배치된 그대로 사용한다.
            SetActionState(hasComparison);

            if (hasComparison)
            {
                ApplyComparePanel(clickedItem, equippedItem, icon);
            }
            else
            {
                popup2?.gameObject.SetActive(false);
            }
        }

        private void ApplyPopup1(EquipmentItemView item, Sprite icon)
        {
            var gradeColor = ItemGradeFramePalette.GetColor(item.Grade);
            SetColor(decoLineImage, gradeColor);
            SetColor(frameBgImage, gradeColor);
            SetColor(frameInnerBorderImage, gradeColor);

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.color = Color.white;
                iconImage.enabled = icon != null;
            }

            if (itemNameText != null)
            {
                itemNameText.text = item.Definition.DisplayName;
            }

            if (gradeText != null)
            {
                gradeText.text = EquipmentGradeInfo.GetDisplayName(item.Grade);
                gradeText.color = gradeColor;
            }

            if (partText != null)
            {
                partText.text = EquipmentPartInfo.GetDisplayName(item.Part);
            }

            ApplyCoreOptions(item.Definition.CoreStatContributions);
            ApplyBonusOptions(item.Instance?.RandomOptions);
            ApplyEquippedRibbon(item);
        }

        public void Hide()
        {
            if (equipRequestInFlight)
            {
                return;
            }

            gameObject.SetActive(false);
        }

        private async void HandleEquipButtonClicked()
        {
            if (equipRequestInFlight || string.IsNullOrEmpty(currentInstanceId))
            {
                return;
            }

            SetRequestInFlight(true);
            try
            {
                var saved = currentItemIsEquipped
                    ? await EquipmentInventoryRuntime.TryUnequipAsync(currentPart)
                    : await EquipmentInventoryRuntime.TryEquipAsync(currentInstanceId);
                if (saved)
                {
                    onEquipped?.Invoke();
                    gameObject.SetActive(false);
                }
            }
            finally
            {
                SetRequestInFlight(false);
            }
        }

        private void SetActionState(bool hasComparison)
        {
            popup1EquipButtonRoot?.gameObject.SetActive(!hasComparison);
            popup2EquipButtonRoot?.gameObject.SetActive(hasComparison);
            if (popup1EquipButtonText != null)
            {
                popup1EquipButtonText.text = currentItemIsEquipped ? "해제" : "장착";
            }

            if (popup2EquipButtonText != null)
            {
                popup2EquipButtonText.text = "장착";
            }

            SetRequestInFlight(false);
        }

        private void SetRequestInFlight(bool inFlight)
        {
            equipRequestInFlight = inFlight;
            if (popup1EquipButton != null) popup1EquipButton.interactable = !inFlight;
            if (popup2EquipButton != null) popup2EquipButton.interactable = !inFlight;
            if (closeButton != null) closeButton.interactable = !inFlight;
            if (dimmedButton != null) dimmedButton.interactable = !inFlight;
        }

        private void ApplyCoreOptions(IReadOnlyList<EquipmentStatContribution> contributions)
        {
            var count = contributions?.Count ?? 0;
            for (var i = 0; i < coreOptionRows.Count; i++)
            {
                var row = coreOptionRows[i];
                if (row == null)
                {
                    continue;
                }

                var visible = i < count;
                row.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var contribution = contributions[i];
                if (coreOptionNameTexts[i] != null)
                {
                    coreOptionNameTexts[i].text = EquipmentGradeStatTable.GetStatDisplayName(contribution.StatType);
                }

                if (coreOptionValueTexts[i] != null)
                {
                    coreOptionValueTexts[i].text = $"+{contribution.Value:0.0}%";
                }
            }
        }

        private void ApplyBonusOptions(IReadOnlyList<EquipmentOptionRollData> options)
        {
            var count = options?.Count ?? 0;
            for (var i = 0; i < bonusOptionRows.Count; i++)
            {
                var row = bonusOptionRows[i];
                if (row == null)
                {
                    continue;
                }

                var visible = i < count;
                row.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var option = options[i];
                if (bonusOptionNameTexts[i] != null)
                {
                    bonusOptionNameTexts[i].text = EquipmentOptionInfo.GetDisplayName(option.Type);
                }

                if (bonusOptionValueTexts[i] != null)
                {
                    bonusOptionValueTexts[i].text = $"+{option.Value:0.0}%";
                }
            }
        }

        // 클릭한 아이템이 지금 그 부위에 장착 중이면 "장착된 장비", 아니면 "선택한 장비"를 표시한다.
        private void ApplyEquippedRibbon(EquipmentItemView item)
        {
            if (equippedRibbon == null)
            {
                return;
            }

            equippedRibbon.gameObject.SetActive(true);
            var label = item.IsEquipped ? EquippedRibbonLabel : SelectedRibbonLabel;

            if (equippedRibbonText != null)
            {
                equippedRibbonText.text = label;
            }

            if (equippedRibbonTextShadow != null)
            {
                equippedRibbonTextShadow.text = label;
            }
        }

        private void ApplyComparePanel(EquipmentItemView clickedItem, EquipmentItemView equippedItem, Sprite icon)
        {
            if (popup2 == null)
            {
                return;
            }

            popup2.gameObject.SetActive(true);

            var gradeColor = ItemGradeFramePalette.GetColor(clickedItem.Grade);
            SetColor(compareDecoLineImage, gradeColor);
            SetColor(compareFrameBgImage, gradeColor);
            SetColor(compareFrameInnerBorderImage, gradeColor);

            if (compareIconImage != null)
            {
                compareIconImage.sprite = icon;
                compareIconImage.color = Color.white;
                compareIconImage.enabled = icon != null;
            }

            if (compareItemNameText != null)
            {
                compareItemNameText.text = clickedItem.Definition.DisplayName;
            }

            if (compareGradeText != null)
            {
                compareGradeText.text = EquipmentGradeInfo.GetDisplayName(clickedItem.Grade);
                compareGradeText.color = gradeColor;
            }

            if (comparePartText != null)
            {
                comparePartText.text = EquipmentPartInfo.GetDisplayName(clickedItem.Part);
            }

            ApplyCompareCoreOptions(clickedItem, equippedItem);
            ApplyCompareOptions(clickedItem, equippedItem);
            ApplyPowerDelta(equippedItem, clickedItem);
        }

        // Option_1~3: 새로 클릭한 장비의 기본옵션을 쓰고, 장착 중인 장비의 같은 종류 기본옵션과
        // 비교해 화살표(우세/열세)를 붙인다. 같은 부위끼리는 기본옵션 종류가 대개 같지만, 다를 때는
        // 비교 대상이 없다는 뜻이므로 화살표를 숨긴다.
        private void ApplyCompareCoreOptions(EquipmentItemView clickedItem, EquipmentItemView equippedItem)
        {
            var clickedCore = clickedItem.Definition?.CoreStatContributions;
            var count = clickedCore?.Count ?? 0;
            var equippedCore = equippedItem.Definition?.CoreStatContributions;

            for (var i = 0; i < compareCoreOptionRows.Count; i++)
            {
                var row = compareCoreOptionRows[i];
                if (row == null)
                {
                    continue;
                }

                var visible = i < count;
                row.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var clickedContribution = clickedCore[i];
                if (compareCoreOptionNameTexts[i] != null)
                {
                    compareCoreOptionNameTexts[i].text = EquipmentGradeStatTable.GetStatDisplayName(clickedContribution.StatType);
                }

                if (compareCoreOptionValueTexts[i] != null)
                {
                    compareCoreOptionValueTexts[i].text = $"+{clickedContribution.Value:0.0}%";
                }

                ApplyCompareCoreArrow(i, clickedContribution, equippedCore);
            }
        }

        private void ApplyCompareCoreArrow(
            int rowIndex,
            EquipmentStatContribution clickedContribution,
            IReadOnlyList<EquipmentStatContribution> equippedCore)
        {
            var arrow = rowIndex < compareCoreOptionArrowIcons.Count ? compareCoreOptionArrowIcons[rowIndex] : null;
            if (arrow == null)
            {
                return;
            }

            EquipmentStatContribution? matchedCore = null;
            if (equippedCore != null)
            {
                for (var i = 0; i < equippedCore.Count; i++)
                {
                    if (equippedCore[i].StatType == clickedContribution.StatType)
                    {
                        matchedCore = equippedCore[i];
                        break;
                    }
                }
            }

            if (!matchedCore.HasValue)
            {
                arrow.gameObject.SetActive(false);
                return;
            }

            var matchedValue = matchedCore.Value.Value;
            if (Mathf.Approximately(matchedValue, clickedContribution.Value))
            {
                arrow.gameObject.SetActive(false);
                return;
            }

            arrow.gameObject.SetActive(true);
            var euler = arrow.localEulerAngles;
            euler.z = clickedContribution.Value > matchedValue ? 0f : 180f; // 더 낮으면 180도 뒤집어 아래로 표시
            arrow.localEulerAngles = euler;
        }

        // BonusOption_1~4: 새로 클릭한 장비의 추가옵션을 쓰고, 장착 중인 장비의 같은 종류 옵션과 비교해
        // 화살표(우세/열세) 또는 "NEW"(장착 중인 장비에 같은 종류가 없음) 표시를 붙인다.
        private void ApplyCompareOptions(EquipmentItemView clickedItem, EquipmentItemView equippedItem)
        {
            var clickedOptions = clickedItem.Instance?.RandomOptions;
            var count = clickedOptions?.Count ?? 0;
            var equippedOptions = equippedItem.Instance?.RandomOptions;

            for (var i = 0; i < compareOptionRows.Count; i++)
            {
                var row = compareOptionRows[i];
                if (row == null)
                {
                    continue;
                }

                var visible = i < count;
                row.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var clickedOption = clickedOptions[i];
                if (compareOptionNameTexts[i] != null)
                {
                    compareOptionNameTexts[i].text = EquipmentOptionInfo.GetDisplayName(clickedOption.Type);
                }

                if (compareOptionValueTexts[i] != null)
                {
                    compareOptionValueTexts[i].text = $"+{clickedOption.Value:0.0}%";
                }

                ApplyCompareArrow(i, clickedOption, equippedOptions);
            }
        }

        private void ApplyCompareArrow(
            int rowIndex,
            EquipmentOptionRollData clickedOption,
            IReadOnlyList<EquipmentOptionRollData> equippedOptions)
        {
            var arrow = rowIndex < compareOptionArrowIcons.Count ? compareOptionArrowIcons[rowIndex] : null;
            var newBadge = rowIndex < compareOptionNewBadges.Count ? compareOptionNewBadges[rowIndex] : null;

            EquipmentOptionRollData matched = null;
            if (equippedOptions != null)
            {
                for (var i = 0; i < equippedOptions.Count; i++)
                {
                    if (equippedOptions[i] != null && equippedOptions[i].Type == clickedOption.Type)
                    {
                        matched = equippedOptions[i];
                        break;
                    }
                }
            }

            if (matched == null)
            {
                // 장착 중인 장비에 같은 종류의 추가옵션이 없음 - 화살표 자리에 "NEW" 표시
                arrow?.gameObject.SetActive(false);
                AlignNewBadgeWithArrow(rowIndex);
                newBadge?.SetActive(true);
                return;
            }

            newBadge?.SetActive(false);
            if (arrow == null)
            {
                return;
            }

            if (Mathf.Approximately(clickedOption.Value, matched.Value))
            {
                arrow.gameObject.SetActive(false);
                return;
            }

            arrow.gameObject.SetActive(true);
            var euler = arrow.localEulerAngles;
            euler.z = clickedOption.Value > matched.Value ? 0f : 180f; // 더 낮으면 180도 뒤집어 아래로 표시
            arrow.localEulerAngles = euler;
        }

        private void AlignNewBadgeWithArrow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= compareOptionArrowIcons.Count ||
                rowIndex >= compareOptionNewBadges.Count)
            {
                return;
            }

            var arrow = compareOptionArrowIcons[rowIndex];
            var badge = compareOptionNewBadges[rowIndex]?.transform as RectTransform;
            if (arrow == null || badge == null)
            {
                return;
            }

            badge.anchorMin = arrow.anchorMin;
            badge.anchorMax = arrow.anchorMax;
            badge.pivot = arrow.pivot;
            badge.anchoredPosition = arrow.anchoredPosition;
        }

        // 장착 중인 장비 -> 클릭한 장비로 바꿨을 때의 전투력 변화를 "+120" 형식으로 표시한다.
        // 실제 파티 스탯과는 무관하게, 이 장비 한 칸의 기여도 차이만 가상 기준치에 적용해 추정한다.
        private void ApplyPowerDelta(EquipmentItemView equippedItem, EquipmentItemView clickedItem)
        {
            if (comparePowerDeltaText == null)
            {
                return;
            }

            var delta = EquipmentUpgradeEvaluator.EvaluatePowerDelta(clickedItem);
            var sign = delta >= 0 ? "+" : "";
            comparePowerDeltaText.text = $"{sign}{delta:N0}";
        }

        // ---------------------------------------------------------------
        // 참조 탐색 (최초 Show() 호출 시 1회만 수행)
        // ---------------------------------------------------------------

        private void EnsureReferences()
        {
            if (referencesResolved)
            {
                return;
            }

            referencesResolved = true;

            popup1 = FindDeep(transform, "Popup_1");
            popup2 = FindDeep(transform, "Popup_2");
            dimmed = FindDeep(transform, "Dimmed");

            ResolvePopup1(popup1);
            ResolvePopup2(popup2);

            closeButtonRoot = FindDeep(transform, "CloseTouchArea_80x80") as RectTransform;
            if (closeButtonRoot != null)
            {
                closeButton = EnsureButton(closeButtonRoot);
                closeButton.onClick.RemoveListener(Hide);
                closeButton.onClick.AddListener(Hide);
            }

            WireDimmedClose();
        }

        private void ResolvePopup1(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var decoLineBox = FindDeep(root, "Popup_Box_02_DecoLine_Basic_Plum");
            decoLineImage = decoLineBox?.Find("DecoLine")?.GetComponent<Image>();

            var itemSlot = FindDeep(root, "ItemSlot");
            var frame = itemSlot != null ? FindDeep(itemSlot, "ItemFrame_01_Normal_Plum") : null;
            frameBgImage = frame?.Find("Bg")?.GetComponent<Image>();
            frameInnerBorderImage = frame?.Find("InnerBorder1")?.GetComponent<Image>();
            iconImage = itemSlot != null ? itemSlot.Find("Icon")?.GetComponent<Image>() : null;

            var group1 = FindDeep(root, "Group_1");
            itemNameText = group1?.Find("Text_ItemName")?.GetComponent<TMP_Text>();
            gradeText = group1?.Find("Text_Grade")?.GetComponent<TMP_Text>();
            partText = group1?.Find("Text")?.GetComponent<TMP_Text>();

            var group2 = FindDeep(root, "Group_2");
            equippedRibbon = group2 != null ? FindDeep(group2, "Title_Ribbon_01") : null;
            if (equippedRibbon != null)
            {
                equippedRibbonText = equippedRibbon.Find("Text (TMP)/GroupArea/Group/Text")?.GetComponent<TMP_Text>()
                    ?? FindDeep(equippedRibbon, "Text")?.GetComponent<TMP_Text>();
                equippedRibbonTextShadow = FindDeep(equippedRibbon, "Text (1)")?.GetComponent<TMP_Text>();
            }

            var group3 = FindDeep(root, "Group_3");

            coreOptionRows.Clear();
            coreOptionNameTexts.Clear();
            coreOptionValueTexts.Clear();
            for (var i = 1; i <= MaxCoreOptionRows; i++)
            {
                var row = group3 != null ? group3.Find($"Option_{i}") : null;
                coreOptionRows.Add(row);
                coreOptionNameTexts.Add(row?.Find("Text_1")?.GetComponent<TMP_Text>());
                coreOptionValueTexts.Add(FindAnyChildText(row, "Text_2", "StatText_2"));
            }

            bonusOptionRows.Clear();
            bonusOptionNameTexts.Clear();
            bonusOptionValueTexts.Clear();
            for (var i = 1; i <= MaxBonusOptionRows; i++)
            {
                var row = group3 != null ? group3.Find($"BonusOption_{i}") : null;
                bonusOptionRows.Add(row);
                bonusOptionNameTexts.Add(row?.Find("OptionText_1")?.GetComponent<TMP_Text>());
                bonusOptionValueTexts.Add(row?.Find("StatText_2")?.GetComponent<TMP_Text>());
            }

            // 기존 Group_Buttons(Button_02_Green)는 더 이상 쓰지 않고 SelectGroup_Buttons(Button_02_Brown)로 대체됐다.
            var legacyButtonGroup = FindDeep(root, "Group_Buttons");
            legacyButtonGroup?.gameObject.SetActive(false);

            popup1EquipButtonRoot = FindDeep(root, "SelectGroup_Buttons");
            if (popup1EquipButtonRoot != null)
            {
                var actionRoot = FindDeep(popup1EquipButtonRoot, "Button_02_Brown") ?? popup1EquipButtonRoot;
                actionRoot.gameObject.SetActive(true);
                popup1EquipButton = EnsureButton(actionRoot);
                popup1EquipButton.onClick.RemoveListener(HandleEquipButtonClicked);
                popup1EquipButton.onClick.AddListener(HandleEquipButtonClicked);
                popup1EquipButtonText = FindDeep(actionRoot, "Text (TMP)")?.GetComponent<TMP_Text>()
                    ?? actionRoot.GetComponentInChildren<TMP_Text>(true);
            }
        }

        private void ResolvePopup2(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var decoLineBox = FindDeepByPrefix(root, "Popup_Box_02_DecoLine_Basic_");
            compareDecoLineImage = decoLineBox?.Find("DecoLine")?.GetComponent<Image>();

            var itemSlot = FindDeep(root, "ItemSlot");
            var frame = itemSlot != null ? FindDeepByPrefix(itemSlot, "ItemFrame_01_Normal_") : null;
            compareFrameBgImage = frame?.Find("Bg")?.GetComponent<Image>();
            compareFrameInnerBorderImage = frame?.Find("InnerBorder1")?.GetComponent<Image>();
            compareIconImage = itemSlot != null ? itemSlot.Find("Icon")?.GetComponent<Image>() : null;

            var group1 = FindDeep(root, "Group_1");
            compareItemNameText = group1?.Find("Text_ItemName")?.GetComponent<TMP_Text>();
            compareGradeText = group1?.Find("Text_Grade")?.GetComponent<TMP_Text>();
            comparePartText = group1?.Find("Text")?.GetComponent<TMP_Text>();

            var group2 = FindDeep(root, "Group_2");
            comparePowerDeltaText = group2 != null ? FindDeep(group2, "StatText")?.GetComponent<TMP_Text>() : null;

            var group3 = FindDeep(root, "Group_3");

            compareCoreOptionRows.Clear();
            compareCoreOptionNameTexts.Clear();
            compareCoreOptionValueTexts.Clear();
            compareCoreOptionArrowIcons.Clear();
            for (var i = 1; i <= MaxCoreOptionRows; i++)
            {
                var row = group3 != null ? group3.Find($"Option_{i}") : null;
                compareCoreOptionRows.Add(row);
                compareCoreOptionNameTexts.Add(row?.Find("Text_1")?.GetComponent<TMP_Text>());
                compareCoreOptionValueTexts.Add(FindAnyChildText(row, "Text_2", "StatText_2"));
                compareCoreOptionArrowIcons.Add(row?.Find("Icon")?.GetComponent<RectTransform>());
            }

            compareOptionRows.Clear();
            compareOptionNameTexts.Clear();
            compareOptionValueTexts.Clear();
            compareOptionArrowIcons.Clear();
            compareOptionNewBadges.Clear();
            for (var i = 1; i <= MaxCompareOptionRows; i++)
            {
                var row = group3 != null ? group3.Find($"BonusOption_{i}") : null;
                compareOptionRows.Add(row);
                compareOptionNameTexts.Add(FindAnyChildText(row, "OptionText_1", "Text_1"));
                compareOptionValueTexts.Add(FindAnyChildText(row, "StatText_2", "Text_2"));
                compareOptionArrowIcons.Add(row?.Find("Icon")?.GetComponent<RectTransform>());
                compareOptionNewBadges.Add(row != null ? FindDeep(row, "TextImage_New")?.gameObject : null);
            }

            popup2EquipButtonRoot = FindDeep(root, "Group_Buttons");
            if (popup2EquipButtonRoot != null)
            {
                var dismantleButton = FindDeep(popup2EquipButtonRoot, "Button_02_Red");
                dismantleButton?.gameObject.SetActive(false);
                var actionRoot = FindDeep(popup2EquipButtonRoot, "Button_02_Brown") ?? popup2EquipButtonRoot;
                actionRoot.gameObject.SetActive(true);
                popup2EquipButton = EnsureButton(actionRoot);
                popup2EquipButton.onClick.RemoveListener(HandleEquipButtonClicked);
                popup2EquipButton.onClick.AddListener(HandleEquipButtonClicked);
                popup2EquipButtonText = actionRoot.GetComponentInChildren<TMP_Text>(true);
            }
        }

        // 카드 영역(Popup_1/Popup_2)을 제외한 바깥(Dimmed)을 클릭하면 패널을 닫는다.
        private void WireDimmedClose()
        {
            if (dimmed == null)
            {
                return;
            }

            dimmedButton = EnsureButton(dimmed);
            dimmedButton.onClick.RemoveListener(Hide);
            dimmedButton.onClick.AddListener(Hide);
        }

        private static Button EnsureButton(Transform target)
        {
            var button = target.GetComponent<Button>();
            if (button == null)
            {
                button = target.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
            }

            var hitArea = target.GetComponent<Graphic>();
            if (hitArea == null)
            {
                var image = target.gameObject.AddComponent<Image>();
                image.color = Color.clear;
                hitArea = image;
            }

            hitArea.raycastTarget = true;
            if (button.targetGraphic == null)
            {
                button.targetGraphic = hitArea;
            }

            return button;
        }

        private static void SetColor(Image image, Color color)
        {
            if (image != null)
            {
                image.color = color;
            }
        }

        private static Transform FindDeep(Transform root, string childName)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].name == childName)
                {
                    return all[i];
                }
            }

            return null;
        }

        // 등급 접미사(Plum/Yellow/...)가 붙는 오브젝트는 접두사만으로 찾는다(팝업마다 기본 등급이 다름).
        private static Transform FindDeepByPrefix(Transform root, string namePrefix)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].name.StartsWith(namePrefix, StringComparison.Ordinal))
                {
                    return all[i];
                }
            }

            return null;
        }

        // 하이어라키 이름이 바뀔 수 있어 여러 후보를 순서대로 시도한다(직계 자식만 확인).
        private static TMP_Text FindAnyChildText(Transform row, params string[] candidateNames)
        {
            if (row == null)
            {
                return null;
            }

            for (var i = 0; i < candidateNames.Length; i++)
            {
                var text = row.Find(candidateNames[i])?.GetComponent<TMP_Text>();
                if (text != null)
                {
                    return text;
                }
            }

            return null;
        }
    }
}
