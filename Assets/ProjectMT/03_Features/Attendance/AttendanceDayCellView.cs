using ProjectMT.Shared.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Attendance
{
    [DisallowMultipleComponent]
    public sealed class AttendanceDayCellView : MonoBehaviour // 출석 하루 보상 표시
    {
        [SerializeField] private TMP_Text dayText;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private GameObject claimedOverlay;
        [SerializeField] private GameObject currentOutline;
        [SerializeField] private GameObject milestoneRibbon;
        [SerializeField] private Image background;

        private static readonly Color ClaimedColor = new Color32(79, 91, 103, 255);
        private static readonly Color CurrentColor = new Color32(93, 64, 42, 255);
        private static readonly Color UpcomingColor = new Color32(42, 49, 60, 255);

        public void Refresh(
            AttendanceRewardCatalog.Entry reward,
            ItemCatalog itemCatalog,
            bool claimed,
            bool current)
        {
            if (dayText != null)
            {
                dayText.text = $"DAY {reward.Day}";
            }

            if (amountText != null)
            {
                amountText.text = FormatAmount(reward.Amount);
            }

            if (rewardIcon != null)
            {
                rewardIcon.sprite = itemCatalog != null && itemCatalog.TryGet(reward.ItemId, out var definition)
                    ? definition.Icon
                    : null;
                rewardIcon.enabled = rewardIcon.sprite != null;
                rewardIcon.color = claimed ? new Color(0.55f, 0.58f, 0.62f, 1f) : Color.white;
            }

            claimedOverlay?.SetActive(claimed);
            currentOutline?.SetActive(current);
            milestoneRibbon?.SetActive(reward.IsMilestone && !current);
            if (background != null)
            {
                background.color = claimed ? ClaimedColor : current ? CurrentColor : UpcomingColor;
            }
        }

        private static string FormatAmount(long value)
        {
            return value >= 1000000L ? $"{value / 1000000f:0.#}M" :
                value >= 1000L ? $"{value / 1000f:0.#}K" : value.ToString("N0");
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            TMP_Text dayLabel,
            TMP_Text amountLabel,
            Image icon,
            GameObject claimed,
            GameObject current,
            GameObject milestone,
            Image cellBackground)
        {
            dayText = dayLabel;
            amountText = amountLabel;
            rewardIcon = icon;
            claimedOverlay = claimed;
            currentOutline = current;
            milestoneRibbon = milestone;
            background = cellBackground;
        }
#endif
    }
}
