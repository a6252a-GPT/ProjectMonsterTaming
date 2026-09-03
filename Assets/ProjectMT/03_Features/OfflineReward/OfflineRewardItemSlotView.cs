using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.OfflineReward
{
    [DisallowMultipleComponent]
    public sealed class OfflineRewardItemSlotView : MonoBehaviour // 기존 인벤토리 슬롯의 방치 보상 표시 Adapter
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Transform normalArea;
        [SerializeField] private TMP_Text equipmentLevelText;
        private int boundEquipmentLevel;

        public void Bind(
            Sprite rewardIcon,
            long amount,
            string label,
            GameObject frameVariantTemplate,
            int equipmentLevel = 0)
        {
            ApplyFrameVariant(frameVariantTemplate);
            ResolveLevelText();
            if (icon != null)
            {
                icon.sprite = rewardIcon;
                icon.enabled = rewardIcon != null;
                icon.preserveAspect = true;
            }

            if (amountText != null)
            {
                amountText.text = $"×{Math.Max(0L, amount):N0}";
            }

            if (labelText != null)
            {
                labelText.text = label ?? string.Empty;
            }

            boundEquipmentLevel = equipmentLevel;
            RefreshEquipmentLevel();

            gameObject.name = string.IsNullOrWhiteSpace(label)
                ? "OfflineRewardSlot"
                : $"OfflineRewardSlot_{label.Trim()}";
        }

        private void OnEnable() => RefreshEquipmentLevel();

        private void OnDisable()
        {
            if (equipmentLevelText != null)
            {
                equipmentLevelText.text = string.Empty;
                equipmentLevelText.gameObject.SetActive(false);
            }
        }

        private void RefreshEquipmentLevel()
        {
            if (amountText != null)
            {
                amountText.gameObject.SetActive(boundEquipmentLevel <= 0);
            }
            ResolveLevelText();
            if (equipmentLevelText != null)
            {
                equipmentLevelText.text = boundEquipmentLevel > 0 ? $"Lv.{boundEquipmentLevel}" : string.Empty;
                equipmentLevelText.gameObject.SetActive(boundEquipmentLevel > 0);
            }
        }

        private void ResolveLevelText()
        {
            if (equipmentLevelText != null)
            {
                return;
            }

            var texts = GetComponentsInChildren<TMP_Text>(true);
            for (var index = 0; index < texts.Length; index++)
            {
                if (texts[index].name == "Text_Level")
                {
                    equipmentLevelText = texts[index];
                    return;
                }
            }
        }

        private void ApplyFrameVariant(GameObject template)
        {
            if (normalArea == null || template == null)
            {
                return;
            }

            var current = normalArea.childCount > 0 ? normalArea.GetChild(0) : null;
            if (current != null && current.name == template.name)
            {
                return;
            }

            if (current != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(current.gameObject);
                }
                else
                {
                    DestroyImmediate(current.gameObject);
                }
            }

            var instance = Instantiate(template, normalArea);
            instance.name = template.name;
            instance.SetActive(true);
            if (instance.TryGetComponent<RectTransform>(out var rect))
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Image rewardIcon,
            TMP_Text quantity,
            Transform frameRoot,
            TMP_Text level = null)
        {
            icon = rewardIcon;
            amountText = quantity;
            normalArea = frameRoot;
            equipmentLevelText = level;
        }
#endif
    }
}
