using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Mailbox
{
    [DisallowMultipleComponent]
    public sealed class MailboxPanelController : MonoBehaviour // 우편 목록·상세·원자 수령 팝업
    {
        private enum Filter
        {
            All,
            System,
            Event,
            Combat
        }

        [SerializeField] private Button closeButton;
        [SerializeField] private Button outsideCloseButton;
        [SerializeField] private Button claimSelectedButton;
        [SerializeField] private Button claimAllButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button[] filterButtons;
        [SerializeField] private Button previousPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private TMP_Text pageText;
        [SerializeField] private MailListItemView[] listItems;
        [SerializeField] private GameObject footerRoot;
        [SerializeField] private GameObject emptyStateRoot;
        [SerializeField] private TMP_Text emptyStateText;
        [SerializeField] private GameObject detailRoot;
        [SerializeField] private TMP_Text detailTitleText;
        [SerializeField] private TMP_Text detailBodyText;
        [SerializeField] private TMP_Text detailRemainingText;
        [SerializeField] private MailAttachmentView[] attachmentViews;

        private IGameProgressService progress;
        private ItemCatalog itemCatalog;
        private Filter activeFilter;
        private string selectedMailId;
        private bool subscribed;
        private bool busy;
        private int pageIndex;

        public event Action<bool> OpenStateChanged;
        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            if (emptyStateText == null && emptyStateRoot != null)
            {
                emptyStateText = emptyStateRoot.GetComponentInChildren<TMP_Text>(true);
            }

            closeButton?.onClick.AddListener(Close);
            outsideCloseButton?.onClick.AddListener(Close);
            claimSelectedButton?.onClick.AddListener(ClaimSelected);
            claimAllButton?.onClick.AddListener(ClaimAll);
            previousPageButton?.onClick.AddListener(ShowPreviousPage);
            nextPageButton?.onClick.AddListener(ShowNextPage);
            for (var index = 0; index < filterButtons?.Length; index++)
            {
                var filterIndex = index;
                filterButtons[index]?.onClick.AddListener(() => SetFilter((Filter)filterIndex));
            }
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            closeButton?.onClick.RemoveListener(Close);
            outsideCloseButton?.onClick.RemoveListener(Close);
            claimSelectedButton?.onClick.RemoveListener(ClaimSelected);
            claimAllButton?.onClick.RemoveListener(ClaimAll);
            previousPageButton?.onClick.RemoveListener(ShowPreviousPage);
            nextPageButton?.onClick.RemoveListener(ShowNextPage);
            Unsubscribe();
        }

        public void Configure(IGameProgressService progressService, ItemCatalog catalog)
        {
            Unsubscribe();
            progress = progressService;
            itemCatalog = catalog;
            Subscribe();
            Refresh();
        }

        public void Open()
        {
            gameObject.SetActive(true);
            Refresh();
            OpenStateChanged?.Invoke(true);
        }

        public void Close()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            OpenStateChanged?.Invoke(false);
            gameObject.SetActive(false);
        }

        private void SetFilter(Filter filter)
        {
            activeFilter = filter;
            pageIndex = 0;
            selectedMailId = string.Empty;
            Refresh();
        }

        private void ShowPreviousPage()
        {
            pageIndex = Math.Max(0, pageIndex - 1);
            selectedMailId = string.Empty;
            Refresh();
        }

        private void ShowNextPage()
        {
            pageIndex++;
            selectedMailId = string.Empty;
            Refresh();
        }

        private void SelectMail(string mailId)
        {
            selectedMailId = mailId;
            Refresh();
        }

        private async void ClaimSelected()
        {
            if (!busy && !string.IsNullOrWhiteSpace(selectedMailId))
            {
                await ClaimAsync(new[] { selectedMailId });
            }
        }

        private async void ClaimAll()
        {
            if (busy || progress == null)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var ids = FilterEntries(progress.View.Mail.Entries, now)
                .Select(mail => mail.MailId)
                .ToArray();
            if (ids.Length > 0)
            {
                await ClaimAsync(ids);
            }
        }

        private async System.Threading.Tasks.Task ClaimAsync(string[] mailIds)
        {
            if (progress == null || mailIds == null || mailIds.Length == 0)
            {
                return;
            }

            busy = true;
            RefreshButtons(false, false);
            var saved = await progress.TryApplyAndSaveAsync(GameProgressChange.ClaimMail(DateTime.UtcNow, mailIds));
            busy = false;
            selectedMailId = string.Empty;
            Refresh();
            SetStatus(saved ? "첨부 보상을 받았습니다." : "우편 상태가 변경되었습니다. 다시 확인해 주세요.");
        }

        private void Refresh()
        {
            if (progress == null)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var filteredEntries = FilterEntries(progress.View.Mail.Entries, now).ToArray();
            var pageSize = Math.Max(1, listItems?.Length ?? 0);
            var pageCount = Math.Max(1, (filteredEntries.Length + pageSize - 1) / pageSize);
            pageIndex = Mathf.Clamp(pageIndex, 0, pageCount - 1);
            var entries = filteredEntries.Skip(pageIndex * pageSize).Take(pageSize).ToArray();
            if (entries.Length > 0 && !entries.Any(mail => mail.MailId == selectedMailId))
            {
                selectedMailId = entries[0].MailId;
            }
            else if (entries.Length == 0)
            {
                selectedMailId = string.Empty;
            }

            for (var index = 0; index < listItems?.Length; index++)
            {
                if (index < entries.Length)
                {
                    listItems[index]?.Bind(
                        entries[index],
                        itemCatalog,
                        now,
                        entries[index].MailId == selectedMailId,
                        SelectMail,
                        id => _ = ClaimOneFromRow(id));
                }
                else
                {
                    listItems[index]?.Clear();
                }
            }

            var hasEntries = filteredEntries.Length > 0;
            emptyStateRoot?.SetActive(!hasEntries);
            detailRoot?.SetActive(true);
            footerRoot?.SetActive(true);
            statusText?.gameObject.SetActive(true);
            claimAllButton?.gameObject.SetActive(true);
            if (emptyStateText != null)
            {
                emptyStateText.text = GetEmptyStateMessage();
            }

            var selected = entries.FirstOrDefault(mail => mail.MailId == selectedMailId);
            RefreshDetail(selected, now);
            RefreshFilterSelection();
            RefreshButtons(hasEntries && !string.IsNullOrWhiteSpace(selected.MailId), hasEntries);
            RefreshPaging(pageCount);
            SetStatus(filteredEntries.Length == 0
                ? GetEmptyStateMessage()
                : $"수령 가능한 우편 {filteredEntries.Length}개");
        }

        private async System.Threading.Tasks.Task ClaimOneFromRow(string mailId)
        {
            if (!busy)
            {
                await ClaimAsync(new[] { mailId });
            }
        }

        private IEnumerable<MailEntryView> FilterEntries(IReadOnlyList<MailEntryView> source, DateTime utcNow)
        {
            return source.Where(mail => !mail.IsExpired(utcNow) && (activeFilter == Filter.All ||
                activeFilter == Filter.System && mail.Category == MailCategory.System ||
                activeFilter == Filter.Event && mail.Category == MailCategory.Event ||
                activeFilter == Filter.Combat && mail.Category == MailCategory.Combat));
        }

        private void RefreshDetail(MailEntryView mail, DateTime utcNow)
        {
            if (string.IsNullOrWhiteSpace(mail.MailId))
            {
                if (detailTitleText != null)
                {
                    detailTitleText.text = string.Empty;
                }
                if (detailBodyText != null)
                {
                    detailBodyText.text = string.Empty;
                }
                if (detailRemainingText != null)
                {
                    detailRemainingText.text = string.Empty;
                }

                for (var index = 0; index < attachmentViews?.Length; index++)
                {
                    attachmentViews[index]?.Clear();
                }
                return;
            }

            if (detailTitleText != null)
            {
                detailTitleText.text = mail.Title;
            }
            if (detailBodyText != null)
            {
                detailBodyText.text = mail.Body;
            }
            if (detailRemainingText != null)
            {
                detailRemainingText.text = FormatRemaining(mail, utcNow);
            }

            for (var index = 0; index < attachmentViews?.Length; index++)
            {
                if (index < mail.Attachments.Count)
                {
                    attachmentViews[index]?.Bind(mail.Attachments[index], itemCatalog);
                }
                else
                {
                    attachmentViews[index]?.Clear();
                }
            }
        }

        private string GetEmptyStateMessage()
        {
            return activeFilter switch
            {
                Filter.System => "시스템 우편이 없습니다",
                Filter.Event => "이벤트 우편이 없습니다",
                Filter.Combat => "전투 우편이 없습니다",
                _ => "도착한 우편이 없습니다"
            };
        }

        private void RefreshFilterSelection()
        {
            for (var index = 0; index < filterButtons?.Length; index++)
            {
                var selected = index == (int)activeFilter;
                var focus = FindDescendant(filterButtons[index]?.transform, "Focus");
                focus?.gameObject.SetActive(selected);
                var label = filterButtons[index]?.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.color = selected ? new Color32(255, 241, 205, 255) : new Color32(178, 188, 202, 255);
                }
            }
        }

        private static Transform FindDescendant(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && child.name == childName)
                {
                    return child;
                }
            }

            return null;
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

            if (remaining.TotalDays >= 1d)
            {
                return $"{Math.Max(1, (int)Math.Ceiling(remaining.TotalDays))}일 남음";
            }

            return $"{Math.Max(1, (int)Math.Ceiling(remaining.TotalHours))}시간 남음";
        }

        private void RefreshButtons(bool canClaimSelected, bool canClaimAll)
        {
            SetButtonEnabled(claimSelectedButton, !busy && canClaimSelected);
            SetButtonEnabled(claimAllButton, !busy && canClaimAll);
        }

        private void RefreshPaging(int pageCount)
        {
            if (pageText != null)
            {
                pageText.text = $"{pageIndex + 1} / {pageCount}";
            }
            SetButtonEnabled(previousPageButton, !busy && pageIndex > 0);
            SetButtonEnabled(nextPageButton, !busy && pageIndex + 1 < pageCount);
        }

        private static void SetButtonEnabled(Button button, bool enabled)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = enabled;
            var group = button.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = enabled ? 1f : 0.45f;
                group.interactable = enabled;
                group.blocksRaycasts = enabled;
            }
        }

        private void Subscribe()
        {
            if (!subscribed && isActiveAndEnabled && progress != null)
            {
                progress.Changed += Refresh;
                subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (subscribed && progress != null)
            {
                progress.Changed -= Refresh;
            }
            subscribed = false;
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Button close,
            Button outsideClose,
            Button claimSelected,
            Button claimAll,
            TMP_Text status,
            Button[] filters,
            MailListItemView[] rows,
            GameObject emptyState,
            GameObject detail,
            TMP_Text detailTitle,
            TMP_Text detailBody,
            TMP_Text detailRemaining,
            MailAttachmentView[] attachments)
        {
            closeButton = close;
            outsideCloseButton = outsideClose;
            claimSelectedButton = claimSelected;
            claimAllButton = claimAll;
            statusText = status;
            filterButtons = filters;
            listItems = rows;
            emptyStateRoot = emptyState;
            emptyStateText = emptyState?.GetComponentInChildren<TMP_Text>(true);
            detailRoot = detail;
            detailTitleText = detailTitle;
            detailBodyText = detailBody;
            detailRemainingText = detailRemaining;
            attachmentViews = attachments;
        }

        public void EditorConfigurePaging(Button previous, Button next, TMP_Text pageLabel)
        {
            previousPageButton = previous;
            nextPageButton = next;
            pageText = pageLabel;
        }

        public void EditorConfigureFooter(GameObject footer)
        {
            footerRoot = footer;
        }
#endif
    }
}
