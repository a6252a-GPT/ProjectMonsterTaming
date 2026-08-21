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
        [SerializeField] private Transform normalArea;

        public void Bind(Sprite rewardIcon, long amount, string label, GameObject frameVariantTemplate)
        {
            ApplyFrameVariant(frameVariantTemplate);
            if (icon != null)
            {
                icon.sprite = rewardIcon;
                icon.enabled = rewardIcon != null;
                icon.preserveAspect = true;
            }

            if (amountText != null)
            {
                amountText.text = $"x{Math.Max(0L, amount):N0}";
            }

            gameObject.name = string.IsNullOrWhiteSpace(label)
                ? "OfflineRewardSlot"
                : $"OfflineRewardSlot_{label.Trim()}";
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
        public void EditorConfigure(Image rewardIcon, TMP_Text quantity, Transform frameRoot)
        {
            icon = rewardIcon;
            amountText = quantity;
            normalArea = frameRoot;
        }
#endif
    }
}
