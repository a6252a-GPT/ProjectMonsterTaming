using System;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Mailbox
{
    [DisallowMultipleComponent]
    public sealed class MailListItemView : MonoBehaviour // 우편 한 줄 선택·수령
    {
        [SerializeField] private Button selectButton;
        [SerializeField] private Button claimButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private TMP_Text remainingText;
        [SerializeField] private TMP_Text rewardAmountText;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private GameObject selectedOutline;

        private string mailId;

        public void Bind(
            MailEntryView mail,
            ItemCatalog itemCatalog,
            DateTime utcNow,
            bool selected,
            Action<string> select,
            Action<string> claim)
        {
            gameObject.SetActive(true);
            mailId = mail.MailId;
            selectButton?.onClick.RemoveAllListeners();
            claimButton?.onClick.RemoveAllListeners();
            selectButton?.onClick.AddListener(() => select?.Invoke(mailId));
            claimButton?.onClick.AddListener(() => claim?.Invoke(mailId));
            if (titleText != null)
            {
                titleText.text = mail.Title;
            }

            if (categoryText != null)
            {
                categoryText.text = GetCategoryName(mail.Category);
            }

            if (remainingText != null)
            {
                var categoryPrefix = categoryText == null ? $"{GetCategoryName(mail.Category)} · " : string.Empty;
                remainingText.text = $"{categoryPrefix}{FormatRemaining(mail, utcNow)}";
            }

            var first = mail.Attachments.Count > 0 ? mail.Attachments[0] : default;
            if (rewardAmountText != null)
            {
                var extra = mail.Attachments.Count > 1 ? $" +{mail.Attachments.Count - 1}" : string.Empty;
                rewardAmountText.text = first.IsValid ? $"×{first.Amount:N0}{extra}" : "첨부 없음";
            }

            if (rewardIcon != null)
            {
                rewardIcon.sprite = first.IsValid && itemCatalog != null && itemCatalog.TryGet(first.ItemId, out var definition)
                    ? definition.Icon
                    : null;
                rewardIcon.enabled = rewardIcon.sprite != null;
            }

            selectedOutline?.SetActive(selected);
            if (claimButton != null)
            {
                claimButton.interactable = !mail.IsExpired(utcNow);
            }
        }

        public void Clear()
        {
            mailId = string.Empty;
            selectButton?.onClick.RemoveAllListeners();
            claimButton?.onClick.RemoveAllListeners();
            gameObject.SetActive(false);
        }

        private static string GetCategoryName(MailCategory category)
        {
            return category switch
            {
                MailCategory.Event => "이벤트",
                MailCategory.Combat => "전투",
                _ => "시스템"
            };
        }

        private static string FormatRemaining(MailEntryView mail, DateTime utcNow)
        {
            if (!DateTime.TryParse(mail.ExpiresAtUtc, out var expires))
            {
                return "만료 정보 없음";
            }

            var remaining = expires.ToUniversalTime() - utcNow.ToUniversalTime();
            if (remaining <= TimeSpan.Zero)
            {
                return "만료됨";
            }

            return remaining.TotalDays >= 1d
                ? $"{Math.Max(1, (int)Math.Ceiling(remaining.TotalDays))}일 남음"
                : $"{Math.Max(1, (int)Math.Ceiling(remaining.TotalHours))}시간 남음";
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Button select,
            Button claim,
            TMP_Text title,
            TMP_Text category,
            TMP_Text remaining,
            TMP_Text rewardAmount,
            Image icon,
            GameObject selected)
        {
            selectButton = select;
            claimButton = claim;
            titleText = title;
            categoryText = category;
            remainingText = remaining;
            rewardAmountText = rewardAmount;
            rewardIcon = icon;
            selectedOutline = selected;
        }
#endif
    }
}
