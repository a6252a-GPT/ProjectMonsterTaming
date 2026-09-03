using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Equipment
{
    public sealed partial class EquipmentPageController
    {
        private void BuildInventorySlots()
        {
            Transform scrollViewTransform = null;
            for (var i = 0; i < InventoryScrollViewNameCandidates.Length; i++)
            {
                scrollViewTransform = FindDeep(transform, InventoryScrollViewNameCandidates[i]);
                if (scrollViewTransform != null)
                {
                    break;
                }
            }

            if (scrollViewTransform == null)
            {
                return;
            }

            inventoryScrollRect = scrollViewTransform.GetComponent<ScrollRect>();
            inventoryContentRoot = inventoryScrollRect != null ? inventoryScrollRect.content : null;
            if (inventoryContentRoot == null)
            {
                return;
            }

            for (var i = 1; i <= AuthoredInventorySlotCount; i++)
            {
                var slotRoot = FindDeepAny(transform, $"InventorySlot_{i:00}_1", $"InventorySlot_{i:00}");
                if (slotRoot != null)
                {
                    AddSlotView(slotRoot, false);
                }
            }

            if (slots.Count > 0)
            {
                inventorySlotCellTemplate = slots[0].LayoutRoot;
            }
        }

        private void AddSlotView(Transform slotRoot, bool resetClickListeners)
        {
            var layoutRoot = slotRoot.gameObject;
            if (slotRoot.parent != null && slotRoot.parent.parent == inventoryContentRoot)
            {
                layoutRoot = slotRoot.parent.gameObject;
            }

            var view = new SlotView
            {
                LayoutRoot = layoutRoot,
                Root = slotRoot,
                ItemIcon = FindDeep(slotRoot, "Item")?.GetComponent<Image>(),
                NormalArea = FindDeep(slotRoot, "NormalArea"),
                AddIndicator = FindDeep(slotRoot, "Add_1")?.gameObject,
                TextLevel = FindDeep(slotRoot, "Text_Level")?.gameObject,
                CheckObject = FindDeep(slotRoot, "Check")?.gameObject,
                FocusObject = FindDeep(slotRoot, "Focus")?.gameObject,
                LockObject = FindDeep(slotRoot, "Lock")?.gameObject
            };

            view.EquippedLabelText = CreateEquippedLabel(slotRoot);
            view.UpgradeArrow = CreateUpgradeArrow(slotRoot);
            view.ClickButton = EnsureButton(slotRoot);
            if (resetClickListeners)
            {
                view.ClickButton.onClick.RemoveAllListeners();
            }

            var capturedView = view;
            view.ClickButton.onClick.AddListener(() => HandleSlotClicked(capturedView));

            var holdTrigger = PointerHoldTrigger.EnsureOn(slotRoot.gameObject);
            holdTrigger?.Configure(() => HandleSlotHoldStart(capturedView), null);

            slots.Add(view);
        }

        private int EnsureInventorySlotCount(int requestedCount)
        {
            var visibleCount = Mathf.Clamp(requestedCount, 0, MaxInventorySlotCount);
            while (slots.Count < visibleCount && inventorySlotCellTemplate != null && inventoryContentRoot != null)
            {
                var slotNumber = slots.Count + 1;
                var cell = Instantiate(inventorySlotCellTemplate, inventoryContentRoot);
                cell.name = $"InventorySlotCell_{slotNumber:000}";
                cell.SetActive(true);

                var slotRoot = cell.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(candidate => InventorySlotNamePattern.IsMatch(candidate.name));
                if (slotRoot == null)
                {
                    Destroy(cell);
                    break;
                }

                slotRoot.name = $"InventorySlot_{slotNumber:00}_1";
                AddSlotView(slotRoot, true);
            }

            var availableCount = Mathf.Min(visibleCount, slots.Count);
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i].LayoutRoot != null)
                {
                    slots[i].LayoutRoot.SetActive(i < availableCount);
                }
            }

            return availableCount;
        }

        private void RebuildInventoryLayout()
        {
            if (inventoryContentRoot == null)
            {
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(inventoryContentRoot);
            Canvas.ForceUpdateCanvases();
        }

        private void ResetInventoryScrollPosition()
        {
            if (inventoryScrollRect == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            inventoryScrollRect.StopMovement();
            inventoryScrollRect.verticalNormalizedPosition = 1f;
        }

        // 인벤토리에서도 장착 중인 아이템을 구분할 수 있도록 아이콘 아래에 "[장착]" 텍스트를 표시한다.
        private TMP_Text CreateEquippedLabel(Transform slotRoot)
        {
            var existing = FindDeep(slotRoot, "EquippedLabel")?.GetComponent<TMP_Text>();
            if (existing != null)
            {
                return existing;
            }

            var labelObject = new GameObject("EquippedLabel", typeof(RectTransform));
            labelObject.transform.SetParent(slotRoot, false);
            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 10f);
            rect.sizeDelta = new Vector2(0f, 24f);

            var text = labelObject.AddComponent<TextMeshProUGUI>();
            if (equippedLabelFont != null)
            {
                text.font = equippedLabelFont;
            }

            text.fontSize = 18f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.outlineWidth = 0.2f;
            text.outlineColor = Color.black;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private Image CreateUpgradeArrow(Transform slotRoot)
        {
            var existing = FindDeep(slotRoot, "UpgradeArrow")?.GetComponent<Image>();
            if (existing != null)
            {
                existing.sprite = upgradeArrowSprite;
                return existing;
            }

            var arrowObject = new GameObject("UpgradeArrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            arrowObject.transform.SetParent(slotRoot, false);
            var rect = arrowObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(48f, 48f);

            var image = arrowObject.GetComponent<Image>();
            image.sprite = upgradeArrowSprite;
            image.color = new Color32(50, 220, 105, 255);
            image.preserveAspect = true;
            image.raycastTarget = false;
            arrowObject.SetActive(false);
            return image;
        }

        private static Button EnsureButton(Transform target)
        {
            var button = target.GetComponent<Button>();
            if (button == null)
            {
                button = target.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None; // 목업 비주얼을 그대로 유지, 클릭 판정만 추가
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

        private void HandleSlotClicked(SlotView view)
        {
            if (string.IsNullOrEmpty(view.BoundInstanceId))
            {
                return; // 빈 슬롯
            }

            if (currentMode == EquipmentPageMode.Dismantle)
            {
                if (!EquipmentInventoryRuntime.TryGetItem(view.BoundInstanceId, out var dismantleItem) ||
                    dismantleItem.IsEquipped || dismantleItem.IsLocked)
                {
                    return;
                }

                if (!dismantleSelection.Add(view.BoundInstanceId))
                {
                    dismantleSelection.Remove(view.BoundInstanceId);
                }

                selectedInstanceId = null;
                CloseDismantleConfirmation();
                RefreshSelection();
                return;
            }

            selectedInstanceId = view.BoundInstanceId;
            RefreshSelection();
            ShowItemComparisonPopup(view.BoundInstanceId);
        }

        // 누르는 순간 비교창을 열어 모바일에서도 즉시 반응하게 한다.
        private void HandleSlotHoldStart(SlotView view)
        {
            if (currentMode != EquipmentPageMode.Equip || string.IsNullOrEmpty(view.BoundInstanceId))
            {
                return;
            }

            selectedInstanceId = view.BoundInstanceId;
            RefreshSelection();
            ShowItemComparisonPopup(view.BoundInstanceId);
        }

        private void ShowItemComparisonPopup(string instanceId)
        {
            if (!EquipmentInventoryRuntime.TryGetItem(instanceId, out var item) || item.Definition == null)
            {
                return;
            }

            EnsureItemComparisonPanel();
            if (itemComparisonPanel == null)
            {
                return;
            }

            partIconSprites.TryGetValue(item.Part, out var icon);
            if (icon == null)
            {
                CachePartIconSprites(); // 늦게 준비된 장착 슬롯 아이콘 재수집
                partIconSprites.TryGetValue(item.Part, out icon);
            }

            if (icon == null)
            {
                icon = item.Definition.Icon;
            }
            itemComparisonPanel.Show(item, icon);
        }

        // PF_ItemComparison은 장비 페이지가 아니라 씬의 관리 UI 쪽에 배치돼 있어 루트부터 재귀 탐색한다.
        private void EnsureItemComparisonPanel()
        {
            if (itemComparisonPanel != null)
            {
                return;
            }

            var panelRoot = FindDeep(transform.root, "PF_ItemComparison_2");
            if (panelRoot == null)
            {
                return;
            }

            itemComparisonPanel = panelRoot.GetComponent<ItemComparisonPanelController>()
                ?? panelRoot.gameObject.AddComponent<ItemComparisonPanelController>();
            itemComparisonPanel.Configure(combatInputSaved);
        }

        private void RefreshInventoryList()
        {
            IEnumerable<EquipmentItemView> query = EquipmentInventoryRuntime.GetItems();
            if (currentFilter.HasValue)
            {
                query = query.Where(item => item.Part == currentFilter.Value);
            }

            var evaluated = query.Select((item, index) => new
            {
                Item = item,
                OriginalIndex = index,
                PowerDelta = EquipmentUpgradeEvaluator.EvaluatePowerDelta(item)
            });
            // 표시 순서는 "장착 가능 우선" 다음에 사용자가 선택한 등급 순서를 가장 먼저 적용한다.
            // 이전에는 전투력 차이가 등급보다 먼저 비교되어, 등급 높은순을 골라도 낮은 등급 장비가
            // 위에 표시될 수 있었다.
            var ordered = evaluated
                .OrderByDescending(entry => entry.PowerDelta > 0);
            ordered = sortGradeDescending
                ? ordered.ThenByDescending(entry => (int)entry.Item.Grade)
                : ordered.ThenBy(entry => (int)entry.Item.Grade);
            var list = ordered
                // 같은 장착 가능 여부·등급 안에서만 더 높은 전투력 상승 장비를 먼저 보여준다.
                .ThenByDescending(entry => entry.PowerDelta)
                .ThenBy(entry => entry.Item.Part)
                .ThenBy(entry => entry.OriginalIndex)
                .Select(entry => entry.Item)
                .ToList();
            var visibleSlotCount = EnsureInventorySlotCount(list.Count);

            for (var i = 0; i < slots.Count; i++)
            {
                if (i < visibleSlotCount)
                {
                    BindSlot(slots[i], list[i]);
                }
                else
                {
                    ClearSlot(slots[i]);
                }
            }

            // 선택된 장비가 더 이상 보유 목록에 없으면 선택 해제.
            if (!string.IsNullOrEmpty(selectedInstanceId) && !EquipmentInventoryRuntime.TryGetItem(selectedInstanceId, out _))
            {
                selectedInstanceId = null;
            }

            PruneDismantleSelection();

            RefreshCapacityText();
            RebuildInventoryLayout();
            RefreshSelection();
        }

        // 슬롯 오브젝트 자체는 항상 켜둔 채로("+" 표시가 기본으로 보이도록), 아이템이 있을 때만 아이콘
        // 영역(NormalArea/Item)을 켜고 "+" 표시(Add_1)를 끈다. 아이콘은 원래 색(고유 아트) 그대로 보여야
        // 하므로 등급색으로 물들이지 않고, 테두리는 목업에 있던 등급별 완성 프레임을 그대로 갖다 끼운다.
        private void BindSlot(SlotView view, EquipmentItemView item)
        {
            view.BoundInstanceId = item.InstanceId;

            if (view.AddIndicator != null)
            {
                view.AddIndicator.SetActive(false);
            }

            if (view.NormalArea != null)
            {
                view.NormalArea.gameObject.SetActive(true);
            }

            if (view.ItemIcon != null)
            {
                view.ItemIcon.gameObject.SetActive(true);
                if (partIconSprites.TryGetValue(item.Part, out var partSprite) && partSprite != null)
                {
                    view.ItemIcon.sprite = partSprite;
                }

                view.ItemIcon.color = currentMode == EquipmentPageMode.Dismantle && (item.IsEquipped || item.IsLocked)
                    ? new Color32(125, 125, 125, 255)
                    : Color.white; // 분해 보호 장비만 어둡게 표시
            }

            ApplyFrameVariant(view.NormalArea, item.Grade); // 등급에 맞는 기존 프레임(테두리)으로 교체

            // Lv 표시는 나중에 실제 레벨 시스템이 붙으면 다시 켤 것이므로, 지금은 항상 비활성화한다.
            if (view.TextLevel != null)
            {
                view.TextLevel.SetActive(false);
            }

            if (view.EquippedLabelText != null)
            {
                view.EquippedLabelText.text = item.IsEquipped ? "[장착]" : string.Empty;
            }

            if (view.CheckObject != null)
            {
                view.CheckObject.SetActive(currentMode == EquipmentPageMode.Dismantle &&
                                           dismantleSelection.Contains(item.InstanceId));
            }

            if (view.FocusObject != null)
            {
                view.FocusObject.SetActive(currentMode == EquipmentPageMode.Equip &&
                                           item.InstanceId == selectedInstanceId);
            }

            if (view.UpgradeArrow != null)
            {
                view.UpgradeArrow.gameObject.SetActive(currentMode == EquipmentPageMode.Equip &&
                                                       EquipmentUpgradeEvaluator.EvaluatePowerDelta(item) > 0);
            }

            if (view.LockObject != null)
            {
                view.LockObject.SetActive(item.IsLocked);
            }

            if (view.ClickButton != null)
            {
                view.ClickButton.interactable = currentMode != EquipmentPageMode.Dismantle ||
                                                (!item.IsEquipped && !item.IsLocked);
            }
        }

        // 재사용 풀에서 비활성화하기 전에 슬롯 내부 표시와 선택 상태를 초기화한다.
        private void ClearSlot(SlotView view)
        {
            view.BoundInstanceId = null;

            if (view.NormalArea != null)
            {
                view.NormalArea.gameObject.SetActive(false);
            }

            if (view.ItemIcon != null)
            {
                view.ItemIcon.gameObject.SetActive(false);
            }

            if (view.AddIndicator != null)
            {
                view.AddIndicator.SetActive(true);
            }

            if (view.TextLevel != null)
            {
                view.TextLevel.SetActive(false);
            }

            if (view.EquippedLabelText != null)
            {
                view.EquippedLabelText.text = string.Empty;
            }

            if (view.CheckObject != null)
            {
                view.CheckObject.SetActive(false);
            }

            if (view.FocusObject != null)
            {
                view.FocusObject.SetActive(false);
            }

            if (view.UpgradeArrow != null)
            {
                view.UpgradeArrow.gameObject.SetActive(false);
            }

            if (view.LockObject != null)
            {
                view.LockObject.SetActive(false);
            }

            if (view.ClickButton != null)
            {
                view.ClickButton.interactable = false;
            }
        }

        private void RefreshCapacityText()
        {
            if (capacityText != null)
            {
                capacityText.text = $"{EquipmentInventoryRuntime.TotalQuantity} / {EquipmentInventoryRuntime.MaxTotalQuantity}";
            }
        }

        private void RefreshSelection()
        {
            PruneDismantleSelection();
            for (var i = 0; i < slots.Count; i++)
            {
                var view = slots[i];
                if (view.CheckObject != null)
                {
                    view.CheckObject.SetActive(view.BoundInstanceId != null &&
                        currentMode == EquipmentPageMode.Dismantle &&
                        dismantleSelection.Contains(view.BoundInstanceId));
                }

                if (view.FocusObject != null)
                {
                    view.FocusObject.SetActive(view.BoundInstanceId != null &&
                        currentMode == EquipmentPageMode.Equip &&
                        view.BoundInstanceId == selectedInstanceId);
                }
            }

            EquipmentItemView selectedItem = default;
            var hasSelection = currentMode == EquipmentPageMode.Equip &&
                                !string.IsNullOrEmpty(selectedInstanceId) &&
                                EquipmentInventoryRuntime.TryGetItem(selectedInstanceId, out selectedItem);

            var nameText = selectedItemName?.GetComponent<TMP_Text>();
            var coreStatText = selectedItemStat?.GetComponent<TMP_Text>();
            var optionStatText = selectedItemRandomOptionStat?.GetComponent<TMP_Text>();
            if (hasSelection && selectedItem.Definition != null)
            {
                if (nameText != null)
                {
                    nameText.text = selectedItem.Definition.DisplayName;
                }

                var randomOptionText = BuildRandomOptionText(selectedItem);
                if (optionStatText != null)
                {
                    // 기본옵션(핵심 능력치)과 추가 랜덤 옵션을 서로 다른 텍스트 칸에 나눠서 표시한다.
                    if (coreStatText != null)
                    {
                        coreStatText.text = selectedItem.Definition.GetCoreStatSummary();
                    }

                    optionStatText.text = randomOptionText;
                }
                else if (coreStatText != null)
                {
                    // Tools > ProjectMT > 장비창 메뉴를 아직 실행하지 않아 전용 칸이 없는 경우의 임시 대체.
                    var combined = selectedItem.Definition.GetCoreStatSummary();
                    if (!string.IsNullOrEmpty(randomOptionText))
                    {
                        combined += "\n[랜덤 옵션]\n" + randomOptionText;
                    }

                    coreStatText.text = combined;
                }
            }
            else
            {
                if (nameText != null)
                {
                    nameText.text = "장비를 선택하세요";
                }

                if (coreStatText != null)
                {
                    coreStatText.text = string.Empty;
                }

                if (optionStatText != null)
                {
                    optionStatText.text = string.Empty;
                }
            }

            if (equipButton != null)
            {
                equipButton.interactable = currentMode == EquipmentPageMode.Equip &&
                                           !requestInFlight &&
                                           EquipmentUpgradeEvaluator.GetBestUpgradeInstanceIds().Count > 0;
            }

            if (equipButtonText != null)
            {
                equipButtonText.text = AutoEquipButtonText;
            }

            if (equipButtonRoot != null)
            {
                equipButtonRoot.gameObject.SetActive(currentMode == EquipmentPageMode.Equip);
            }

            var showLock = currentMode == EquipmentPageMode.Equip && hasSelection;
            if (lockButtonRoot != null)
            {
                lockButtonRoot.gameObject.SetActive(showLock);
            }

            if (lockButton != null)
            {
                lockButton.interactable = showLock && !requestInFlight;
            }

            if (lockButtonText != null)
            {
                lockButtonText.text = hasSelection && selectedItem.IsLocked ? "해제" : "잠금";
            }

            RefreshDismantleSummary();
            RefreshDismantleControls();
        }

        // "추가 랜덤 옵션" 전용 텍스트 칸에 넣을 내용만 만든다(핵심 능력치는 별도로
        // selectedItem.Definition.GetCoreStatSummary()가 표시).
        private static string BuildRandomOptionText(EquipmentItemView item)
        {
            var options = item.Instance?.RandomOptions;
            if (options == null || options.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (var i = 0; i < options.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(EquipmentOptionInfo.FormatOption(options[i].Type, options[i].Value));
            }

            return builder.ToString();
        }
    }
}
