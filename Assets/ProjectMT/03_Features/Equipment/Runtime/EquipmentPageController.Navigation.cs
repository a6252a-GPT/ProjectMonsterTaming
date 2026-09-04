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
        // ---------------------------------------------------------------
        // 상단 장착/분해 탭
        // ---------------------------------------------------------------

        private void BuildModeTabs()
        {
            if (equipModeTabRoot != null)
            {
                equipModeTabButton = EnsureButton(equipModeTabRoot);
                equipModeTabImage = equipModeTabRoot.GetComponent<Image>();
                equipModeTabButton.onClick.AddListener(() => SetPageMode(EquipmentPageMode.Equip));
            }

            if (dismantleModeTabRoot != null)
            {
                dismantleModeTabButton = EnsureButton(dismantleModeTabRoot);
                dismantleModeTabImage = dismantleModeTabRoot.GetComponent<Image>();
                dismantleModeTabButton.onClick.AddListener(() => SetPageMode(EquipmentPageMode.Dismantle));
            }
        }

        private void SetPageMode(EquipmentPageMode mode, bool refresh = true)
        {
            if (requestInFlight)
            {
                return;
            }

            var modeChanged = currentMode != mode;
            currentMode = mode;
            if (modeChanged && currentMode == EquipmentPageMode.Dismantle)
            {
                // 장비 분해 퀘스트: 분해 탭 → 전체 선택 → 분해.
                questDismantleHintStep = 1;
            }
            CloseDismantleConfirmation();
            offlineAutoDismantleSettingsPanel?.Close();
            itemComparisonPanel?.Hide();
            if (currentMode == EquipmentPageMode.Equip)
            {
                ClearDismantleSelection();
            }
            else
            {
                selectedInstanceId = null;
            }

            if (equipmentModeContentRoot != null)
            {
                equipmentModeContentRoot.SetActive(currentMode == EquipmentPageMode.Equip);
            }

            if (dismantleSummaryRoot != null)
            {
                dismantleSummaryRoot.SetActive(currentMode == EquipmentPageMode.Dismantle);
            }

            if (equipmentActionRoot != null)
            {
                equipmentActionRoot.SetActive(currentMode == EquipmentPageMode.Equip);
            }

            if (dismantleActionRoot != null)
            {
                dismantleActionRoot.SetActive(currentMode == EquipmentPageMode.Dismantle);
            }

            RefreshModeTabVisuals();
            if (refresh)
            {
                RefreshAll();
                ResetInventoryScrollPosition();
            }
        }

        private void RefreshModeTabVisuals()
        {
            var equipSelected = currentMode == EquipmentPageMode.Equip;
            if (equipModeTabImage != null)
            {
                equipModeTabImage.color = equipSelected
                    ? new Color32(105, 177, 53, 255)
                    : new Color32(46, 44, 49, 255);
            }

            if (dismantleModeTabImage != null)
            {
                dismantleModeTabImage.color = equipSelected
                    ? new Color32(46, 44, 49, 255)
                    : new Color32(105, 177, 53, 255);
            }
        }

        // ---------------------------------------------------------------
        // 부위 필터 탭 - 6부위를 아이콘 없이 한글 텍스트로 구분한다.
        // ---------------------------------------------------------------

        private void BuildFilterButtons()
        {
            allFilterTab = FindDeep(transform, "Filter_All_SELECTED");
            AddFilterTab("Filter_Weapon", EquipmentPart.Weapon);
            AddFilterTab("Filter_Shield", EquipmentPart.Helmet);
            AddFilterTab("Filter_Armor", EquipmentPart.Armor);
            AddFilterTab("Filter_Boots", EquipmentPart.Boots);
            AddFilterTab("Filter_Glove", EquipmentPart.Glove);
            AddFilterTab("Filter_Accessory", EquipmentPart.Ring);

            if (allFilterTab != null)
            {
                ConfigureTextFilterTab(allFilterTab, "전체");
                var button = EnsureButton(allFilterTab);
                button.onClick.AddListener(() => SetFilter(null));
            }
        }

        private void AddFilterTab(string objectName, EquipmentPart part)
        {
            var tab = FindDeep(transform, objectName);
            if (tab == null)
            {
                return;
            }

            filterTabs[part] = tab;
            ConfigureTextFilterTab(tab, EquipmentPartInfo.GetDisplayName(part));
            var button = EnsureButton(tab);
            button.onClick.AddListener(() => SetFilter(part));
        }

        private static void ConfigureTextFilterTab(Transform tab, string labelText)
        {
            var label = tab.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = labelText;
                label.gameObject.SetActive(true);
            }

            var icon = tab.Find("Icon");
            icon?.gameObject.SetActive(false); // 부위 필터는 텍스트만 표시
        }

        private void SetFilter(EquipmentPart? part)
        {
            currentFilter = part;
            RefreshFilterHighlight();
            RefreshInventoryList();
            ResetInventoryScrollPosition();
        }

        // 각 탭의 "Focus" 하위 오브젝트를 선택 상태 표시로 사용한다(활성=선택됨).
        private void RefreshFilterHighlight()
        {
            SetFilterVisual(allFilterTab, currentFilter == null);
            foreach (var pair in filterTabs)
            {
                SetFilterVisual(pair.Value, currentFilter == pair.Key);
            }
        }

        private static void SetFilterVisual(Transform tab, bool selected)
        {
            var focus = tab != null ? tab.Find("Focus") : null;
            if (focus != null)
            {
                focus.gameObject.SetActive(selected);
            }

            var label = tab != null ? tab.GetComponentInChildren<TMP_Text>(true) : null;
            if (label != null)
            {
                label.color = selected ? SelectedFilterColor : NormalFilterColor;
            }
        }

        // ---------------------------------------------------------------
        // 정렬 버튼 - 클릭할 때마다 등급 내림차순 ↔ 오름차순으로 전환한다.
        // ---------------------------------------------------------------

        private void BuildSortButton()
        {
            var sortLabelTransform = FindDeep(transform, "SortLabel");
            if (sortLabelTransform == null)
            {
                return;
            }

            var button = EnsureButton(sortLabelTransform);
            button.onClick.AddListener(ToggleSort);
            RefreshSortLabelText();
        }

        private void ToggleSort()
        {
            sortGradeDescending = !sortGradeDescending;
            RefreshSortLabelText();
            RefreshInventoryList();
            ResetInventoryScrollPosition();
        }

        private void RefreshSortLabelText()
        {
            if (sortLabelText != null)
            {
                sortLabelText.text = sortGradeDescending ? "장착 가능 우선 · 등급 높은순" : "장착 가능 우선 · 등급 낮은순";
            }
        }
    }
}
