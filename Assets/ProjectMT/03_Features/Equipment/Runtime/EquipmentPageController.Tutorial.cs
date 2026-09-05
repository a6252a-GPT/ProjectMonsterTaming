using ProjectMT.Features.Quest;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Equipment
{
    public sealed partial class EquipmentPageController
    {
        internal Button QuestEquipCandidateButton
        {
            get
            {
                foreach (var slot in slots)
                {
                    if (slot.ClickButton == null || string.IsNullOrEmpty(slot.BoundInstanceId)) continue;
                    if (!EquipmentInventoryRuntime.TryGetItem(slot.BoundInstanceId, out var item) || item.IsEquipped) continue;
                    if (QuestTutorialInteraction.CanInteract(slot.ClickButton.transform as RectTransform)) return slot.ClickButton;
                }
                return null;
            }
        }

        internal Button QuestEquipDetailButton
        {
            get
            {
                if (itemComparisonPanel == null || !itemComparisonPanel.gameObject.activeInHierarchy) return null;
                foreach (var button in itemComparisonPanel.GetComponentsInChildren<Button>())
                {
                    var label = button.GetComponentInChildren<TMP_Text>();
                    if (label != null && label.text == "장착" && QuestTutorialInteraction.CanInteract(button.transform as RectTransform)) return button;
                }
                return null;
            }
        }
    }
}
