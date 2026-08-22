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
        [SerializeField] private TMP_Text[] rewardSlotTexts; // 최대 3개 확정 보상 카드
        [SerializeField] private Image[] starImages; // 성공 결과 별 3개
        [SerializeField] private Sprite filledStarSprite; // 획득 별
        [SerializeField] private Sprite emptyStarSprite; // 실패 별

        public const int FixedSuccessStarCount = 3; // 조건 시스템 전까지 성공은 3별 고정

        private TaskCompletionSource<bool> closeSource;
        private bool confirmed; // 중복 확인 차단

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
            ApplyStarRating(presentation.Outcome);
            ApplyRewardSlots(presentation);

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

            confirmed = true;
            confirmButton.interactable = false;
            UIPanelPopAnimator.RequestClose(panelRoot, CompleteClose);
        }

        private void ApplyRewardSlots(ContentResultPresentation presentation)
        {
            if (rewardSlotTexts == null || rewardSlotTexts.Length < 3)
            {
                return;
            }

            if (presentation.RewardItems.Count == 0)
            {
                SetSlot(0, presentation.Outcome == ContentOutcome.Fail ? "실패" : "완료");
                SetSlot(1, "보상 없음");
                SetSlot(2, presentation.Outcome == ContentOutcome.Fail ? "미소모" : "저장 완료");
                return;
            }

            if (presentation.RewardItems.Count == 1)
            {
                SetSlot(0, "클리어");
                SetSlot(1, FormatReward(presentation.RewardItems[0]));
                SetSlot(2, "저장 완료");
                return;
            }

            if (presentation.RewardItems.Count == 2)
            {
                SetSlot(0, "클리어");
                SetSlot(1, FormatReward(presentation.RewardItems[0]));
                SetSlot(2, FormatReward(presentation.RewardItems[1]));
                return;
            }

            SetSlot(0, FormatReward(presentation.RewardItems[0]));
            SetSlot(1, FormatReward(presentation.RewardItems[1]));
            SetSlot(
                2,
                presentation.RewardItems.Count == 3
                        ? FormatReward(presentation.RewardItems[2])
                        : $"외 {presentation.RewardItems.Count - 2}종");
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

        private void SetSlot(int index, string value)
        {
            if (index >= 0 && index < rewardSlotTexts.Length && rewardSlotTexts[index] != null)
            {
                rewardSlotTexts[index].text = value;
            }
        }

        private bool IsConfigured()
        {
            return panelRoot != null && panelRoot != gameObject && titleText != null && summaryText != null &&
                   confirmButton != null && rewardSlotTexts != null && rewardSlotTexts.Length >= 3 &&
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
        }

        private static string FormatReward(ProjectMT.Shared.Reward.RewardPresentationItem item)
        {
            var label = string.IsNullOrWhiteSpace(item.Label) ? "보상" : item.Label;
            return $"{label}\n+{item.Amount:N0}";
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
