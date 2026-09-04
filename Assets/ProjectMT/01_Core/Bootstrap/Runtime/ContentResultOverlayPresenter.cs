using System.Threading.Tasks;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class ContentResultOverlayPresenter : MonoBehaviour, IContentResultView // AppRoot 공통 최종 결과창
    {
        [SerializeField] private GameObject panelRoot; // 공통 결과 화면 루트
        [SerializeField] private TMP_Text titleText; // 콘텐츠별 승패 제목
        [SerializeField] private TMP_Text summaryText; // 최종 결과 요약
        [SerializeField] private Button confirmButton; // 확인 뒤 복귀 진행
        [SerializeField] private GameObject[] rewardSlotRoots; // 현재 페이지에 표시할 최대 3개 카드
        [SerializeField] private Image[] rewardSlotIcons; // 실제 ItemDefinition·장비 아이콘
        [SerializeField] private TMP_Text[] rewardSlotTexts;
        [SerializeField] private TMP_Text resultKickerText;
        [SerializeField] private TMP_Text rewardHeaderText;
        [SerializeField] private TMP_Text rewardPageText;
        [SerializeField] private TMP_Text confirmButtonText;
        [SerializeField] private TMP_Text continueHintText;
        [SerializeField] private Sprite fallbackRewardIcon;
        [SerializeField] private Image[] starImages; // 성공 결과 별 3개
        [SerializeField] private Sprite filledStarSprite; // 획득 별
        [SerializeField] private Sprite emptyStarSprite; // 실패 별

        public const int FixedSuccessStarCount = 3; // 조건 시스템 전까지 성공은 3별 고정
        private const int RewardsPerPage = 3;
        private static readonly float[] OneSlotX = { 0f };
        private static readonly float[] TwoSlotX = { -102f, 102f };
        private static readonly float[] ThreeSlotX = { -188f, 0f, 188f };

        private TaskCompletionSource<bool> closeSource;
        private bool confirmed; // 중복 확인 차단
        private ContentResultPresentation currentPresentation;
        private int currentRewardPage;

        public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

        private void Awake()
        {
            confirmButton?.onClick.AddListener(HandleConfirmed);
            Hide();
        }

        private void OnDestroy()
        {
            confirmButton?.onClick.RemoveListener(HandleConfirmed);
            CompleteClose();
        }

        public Task ShowAsync(ContentResultPresentation presentation)
        {
            if (presentation == null)
            {
                return Task.CompletedTask;
            }

            if (!IsConfigured())
            {
                Debug.LogError("Content result overlay references are missing.");
                return Task.CompletedTask;
            }

            CompleteClose(); // 비정상 중복 표시가 와도 이전 대기 해제
            closeSource = new TaskCompletionSource<bool>();
            titleText.text = presentation.Outcome == ContentOutcome.Fail
                ? "도전 실패"
                : "클리어!";
            summaryText.text = string.IsNullOrWhiteSpace(presentation.Summary)
                ? $"{presentation.DisplayName} 완료"
                : $"{presentation.DisplayName} · {presentation.Summary}";
            SetText(resultKickerText, $"{presentation.DisplayName} · 전투 결과");
            ApplyStarRating(presentation.Outcome);
            currentPresentation = presentation;
            currentRewardPage = 0;
            ApplyRewardPage();

            confirmed = false;
            confirmButton.interactable = true;
            UIPanelPopAnimator.RequestOpen(panelRoot, UIPanelPopStyle.RewardPopup);
            panelRoot.transform.SetAsLastSibling();

            return closeSource.Task;
        }

        private void HandleConfirmed()
        {
            if (confirmed)
            {
                return;
            }

            var pageCount = GetRewardPageCount(currentPresentation);
            if (currentRewardPage + 1 < pageCount)
            {
                currentRewardPage++;
                ApplyRewardPage();
                return;
            }

            confirmed = true;
            confirmButton.interactable = false;
            UIPanelPopAnimator.RequestClose(panelRoot, CompleteClose);
        }

        private void ApplyRewardPage()
        {
            if (currentPresentation == null || rewardSlotRoots == null || rewardSlotIcons == null ||
                rewardSlotTexts == null || rewardSlotRoots.Length < RewardsPerPage ||
                rewardSlotIcons.Length < RewardsPerPage || rewardSlotTexts.Length < RewardsPerPage)
            {
                return;
            }

            var rewardCount = currentPresentation.RewardItems.Count;
            if (rewardCount == 0)
            {
                SetText(rewardHeaderText, "보상 없음");
                SetText(confirmButtonText, "확인");
                SetText(continueHintText, "확인하면 메인 전투로 돌아갑니다");
                SetActive(rewardPageText, false);
                for (var index = 0; index < RewardsPerPage; index++)
                {
                    SetSlotActive(index, false);
                }

                return;
            }

            SetText(rewardHeaderText, "확정 보상");
            var pageCount = GetRewardPageCount(currentPresentation);
            currentRewardPage = Mathf.Clamp(currentRewardPage, 0, pageCount - 1);
            var firstRewardIndex = currentRewardPage * RewardsPerPage;
            var visibleCount = Mathf.Min(RewardsPerPage, rewardCount - firstRewardIndex);
            var positions = visibleCount == 1 ? OneSlotX : visibleCount == 2 ? TwoSlotX : ThreeSlotX;

            for (var slotIndex = 0; slotIndex < RewardsPerPage; slotIndex++)
            {
                if (slotIndex >= visibleCount)
                {
                    SetSlotActive(slotIndex, false);
                    continue;
                }

                BindRewardSlot(
                    slotIndex,
                    currentPresentation.RewardItems[firstRewardIndex + slotIndex],
                    positions[slotIndex]);
            }

            var hasMorePages = currentRewardPage + 1 < pageCount;
            SetText(confirmButtonText, hasMorePages ? "다음" : "확인");
            SetText(
                continueHintText,
                hasMorePages ? "다음 보상을 확인합니다" : "확인하면 메인 전투로 돌아갑니다");
            if (rewardPageText != null)
            {
                rewardPageText.gameObject.SetActive(pageCount > 1);
                rewardPageText.text = $"{currentRewardPage + 1} / {pageCount}";
            }
        }

        private void BindRewardSlot(
            int slotIndex,
            ProjectMT.Shared.Reward.RewardPresentationItem item,
            float anchoredX)
        {
            SetSlotActive(slotIndex, true);
            if (rewardSlotRoots[slotIndex].transform is RectTransform rootRect)
            {
                var position = rootRect.anchoredPosition;
                position.x = anchoredX;
                rootRect.anchoredPosition = position;
            }

            var icon = rewardSlotIcons[slotIndex];
            if (icon != null)
            {
                icon.sprite = item.Icon != null ? item.Icon : fallbackRewardIcon;
                icon.preserveAspect = true;
                icon.enabled = icon.sprite != null;
            }

            if (rewardSlotTexts[slotIndex] != null)
            {
                rewardSlotTexts[slotIndex].text = FormatReward(item);
            }
        }

        private void ApplyStarRating(ContentOutcome outcome)
        {
            if (starImages == null)
            {
                return;
            }

            var success = outcome != ContentOutcome.Fail;
            for (var index = 0; index < starImages.Length; index++)
            {
                var star = starImages[index];
                if (star == null)
                {
                    continue;
                }

                star.gameObject.SetActive(index < FixedSuccessStarCount);
                star.sprite = success ? filledStarSprite : emptyStarSprite;
            }
        }

        private void SetSlotActive(int index, bool active)
        {
            if (index >= 0 && index < rewardSlotRoots.Length && rewardSlotRoots[index] != null)
            {
                rewardSlotRoots[index].SetActive(active);
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private static void SetActive(Component target, bool active)
        {
            if (target != null)
            {
                target.gameObject.SetActive(active);
            }
        }

        private static int GetRewardPageCount(ContentResultPresentation presentation)
        {
            return presentation == null || presentation.RewardItems.Count == 0
                ? 1
                : Mathf.CeilToInt(presentation.RewardItems.Count / (float)RewardsPerPage);
        }

        private bool IsConfigured()
        {
            return panelRoot != null && panelRoot != gameObject && titleText != null && summaryText != null &&
                   confirmButton != null && rewardSlotRoots != null && rewardSlotRoots.Length >= RewardsPerPage &&
                   rewardSlotIcons != null && rewardSlotIcons.Length >= RewardsPerPage &&
                   rewardSlotTexts != null && rewardSlotTexts.Length >= RewardsPerPage &&
                   rewardSlotRoots[0] != null && rewardSlotRoots[1] != null && rewardSlotRoots[2] != null &&
                   rewardSlotIcons[0] != null && rewardSlotIcons[1] != null && rewardSlotIcons[2] != null &&
                   rewardSlotTexts[0] != null && rewardSlotTexts[1] != null && rewardSlotTexts[2] != null;
        }

        private void Hide()
        {
            confirmed = false;
            if (confirmButton != null)
            {
                confirmButton.interactable = true;
            }

            if (panelRoot != null && panelRoot.activeSelf)
            {
                panelRoot.SetActive(false);
            }
        }

        private void CompleteClose()
        {
            closeSource?.TrySetResult(true);
            closeSource = null;
            currentPresentation = null;
            currentRewardPage = 0;
        }

        private static string FormatReward(ProjectMT.Shared.Reward.RewardPresentationItem item)
        {
            var label = string.IsNullOrWhiteSpace(item.Label) ? "보상" : item.Label;
            return item.IsEquipment
                ? $"{label} Lv.{item.EquipmentLevel}\n×{item.Amount:N0}"
                : $"{label}\n×{item.Amount:N0}";
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            GameObject resultPanel,
            TMP_Text title,
            TMP_Text summary,
            Button confirm,
            params TMP_Text[] rewardSlots)
        {
            panelRoot = resultPanel;
            titleText = title;
            summaryText = summary;
            confirmButton = confirm;
            rewardSlotTexts = rewardSlots;
            rewardSlotRoots = new GameObject[rewardSlots?.Length ?? 0];
            rewardSlotIcons = new Image[rewardSlots?.Length ?? 0];
            for (var index = 0; rewardSlots != null && index < rewardSlots.Length; index++)
            {
                var slot = rewardSlots[index] != null ? rewardSlots[index].transform.parent : null;
                rewardSlotRoots[index] = slot != null ? slot.gameObject : null;
                rewardSlotIcons[index] = slot != null ? slot.Find("Icon")?.GetComponent<Image>() : null;
            }
        }

        public void EditorConfigureDetails(
            TMP_Text kicker,
            TMP_Text rewardHeader,
            TMP_Text rewardPage,
            TMP_Text confirmLabel,
            TMP_Text hint,
            Sprite fallbackIcon)
        {
            resultKickerText = kicker;
            rewardHeaderText = rewardHeader;
            rewardPageText = rewardPage;
            confirmButtonText = confirmLabel;
            continueHintText = hint;
            fallbackRewardIcon = fallbackIcon;
        }

        public void EditorConfigureStarRating(
            Sprite filledSprite,
            Sprite emptySprite,
            params Image[] stars)
        {
            filledStarSprite = filledSprite;
            emptyStarSprite = emptySprite;
            starImages = stars;
        }
#endif
    }
}
