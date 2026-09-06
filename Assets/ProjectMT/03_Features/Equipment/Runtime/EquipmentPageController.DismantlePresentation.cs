using ProjectMT.Shared.Equipment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Equipment
{
    public sealed partial class EquipmentPageController
    {
        [SerializeField] private Vector2 equipInventoryPosition = new Vector2(10f, -55f);
        [SerializeField] private Vector2 dismantleInventoryPosition = new Vector2(10f, -75f);
        private RectTransform dismantlePreviewContent;
        private GameObject dismantlePreviewTemplate;
        private GameObject dismantleGradeOptions;
        private GameObject dismantleBulkCaption;

        private void BuildDismantlePreviewList()
        {
            dismantleBulkCaption = FindDeep(transform, "DismantleBulkCaption")?.gameObject;
            var scroll = FindDeep(transform, "DismantlePreviewScroll")?.GetComponent<ScrollRect>();
            if (scroll == null || scroll.content == null) return;
            dismantlePreviewContent = scroll.content;
            dismantlePreviewTemplate = FindDeep(dismantlePreviewContent, "DismantlePreviewTemplate")?.gameObject;
            if (dismantlePreviewTemplate == null) return;
            dismantlePreviewSlots.Clear();
            dismantlePreviewTemplate.SetActive(false);
            dismantleGradeOptions = FindDeep(transform, "DismantleGradeOptions")?.gameObject;
            if (dismantleGradeOptions == null) return;
            for (var index = 0; index < 5; index++)
            {
                var grade = (EquipmentGrade)index;
                var button = FindDeep(dismantleGradeOptions.transform, $"GradeOption_{index}")?.GetComponent<Button>();
                if (button != null) button.onClick.AddListener(() => SelectDismantleGrade(grade));
            }
            dismantleGradeOptions.SetActive(false);
        }

        private void OpenDismantleGradeOptions()
        {
            if (requestInFlight) return;
            if (dismantleGradeOptions == null)
            {
                CycleDismantleGradeThreshold();
                return;
            }
            dismantleGradeOptions.SetActive(!dismantleGradeOptions.activeSelf);
            if (dismantleGradeOptions.activeSelf) dismantleGradeOptions.transform.SetAsLastSibling();
        }

        private void EnsureDismantlePreviewCount(int count)
        {
            if (dismantlePreviewTemplate == null) return;
            while (dismantlePreviewSlots.Count < Mathf.Min(count, MaxInventorySlotCount))
            {
                var cell = Instantiate(dismantlePreviewTemplate, dismantlePreviewContent);
                cell.name = $"DismantleSelected_{dismantlePreviewSlots.Count:000}";
                var slot = FindDeep(cell.transform, "PreviewSlot");
                var view = new SlotView
                {
                    LayoutRoot = cell,
                    Root = slot,
                    ItemIcon = FindDeep(slot, "Item")?.GetComponent<Image>(),
                    NormalArea = FindDeep(slot, "NormalArea"),
                    AddIndicator = FindDeep(slot, "Add_1")?.gameObject,
                    TextLevel = slot.Find("Text_Level")?.gameObject,
                    CheckObject = FindDeep(slot, "Check")?.gameObject,
                    FocusObject = FindDeep(slot, "Focus")?.gameObject,
                    LockObject = FindDeep(slot, "Lock")?.gameObject,
                    UpgradeArrow = FindDeep(slot, "UpgradeArrow")?.GetComponent<Image>(),
                    EquippedLabelText = FindDeep(slot, "EquippedLabel")?.GetComponent<TMP_Text>(),
                    ClickButton = slot.GetComponent<Button>()
                };
                view.ClickButton.onClick.RemoveAllListeners();
                view.ClickButton.onClick.AddListener(() => RemoveDismantlePreview(view));
                var remove = FindDeep(slot, "RemoveSelection")?.GetComponent<Button>();
                if (remove != null)
                {
                    remove.onClick.RemoveAllListeners();
                    remove.onClick.AddListener(() => RemoveDismantlePreview(view));
                }
                dismantlePreviewSlots.Add(new DismantlePreviewSlot { Root = cell, InventoryView = view });
            }
        }

        private void RemoveDismantlePreview(SlotView view)
        {
            if (requestInFlight || currentMode != EquipmentPageMode.Dismantle ||
                string.IsNullOrEmpty(view.BoundInstanceId)) return;
            if (!dismantleSelection.Remove(view.BoundInstanceId)) return;
            CloseDismantleConfirmation();
            RefreshSelection();
        }
    }
}
