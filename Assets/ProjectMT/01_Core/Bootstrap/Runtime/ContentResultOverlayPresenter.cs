using System.Threading.Tasks;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.UI;
using TMPro;
using UnityEngine;

namespace ProjectMT.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class ContentResultOverlayPresenter : MonoBehaviour, IContentResultView // AppRoot 공통 최종 결과창
    {
        [SerializeField] private ContentClearOverlay overlay; // 공용 결과 시각 원본 재사용
        [SerializeField] private TMP_Text[] rewardSlotTexts; // 최대 3개 확정 보상 카드

        private TaskCompletionSource<bool> closeSource;

        private void Awake()
        {
            overlay?.Hide();
        }

        private void OnDestroy()
        {
            closeSource?.TrySetResult(true);
            closeSource = null;
        }

        public Task ShowAsync(ContentResultPresentation presentation)
        {
            if (presentation == null || overlay == null)
            {
                return Task.CompletedTask;
            }

            closeSource?.TrySetResult(true); // 비정상 중복 표시가 와도 이전 대기 해제
            closeSource = new TaskCompletionSource<bool>();
            var title = presentation.Outcome == ContentOutcome.Fail
                ? $"{presentation.DisplayName} 실패"
                : $"{presentation.DisplayName} 완료";
            if (!overlay.TryShow(
                    presentation.Summary,
                    FormatPrimaryReward(presentation),
                    HandleConfirmed,
                    title))
            {
                closeSource.TrySetResult(true);
            }
            else
            {
                ApplyRewardSlots(presentation);
            }

            return closeSource.Task;
        }

        private void HandleConfirmed()
        {
            closeSource?.TrySetResult(true);
            closeSource = null;
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

        private void SetSlot(int index, string value)
        {
            if (index >= 0 && index < rewardSlotTexts.Length && rewardSlotTexts[index] != null)
            {
                rewardSlotTexts[index].text = value;
            }
        }

        private static string FormatPrimaryReward(ContentResultPresentation presentation)
        {
            return presentation.RewardItems.Count == 0
                ? presentation.Outcome == ContentOutcome.Fail ? "획득 보상 없음" : "보상 없음"
                : FormatReward(presentation.RewardItems[0]);
        }

        private static string FormatReward(ProjectMT.Shared.Reward.RewardPresentationItem item)
        {
            var label = string.IsNullOrWhiteSpace(item.Label) ? "보상" : item.Label;
            return $"{label}\n+{item.Amount:N0}";
        }

#if UNITY_EDITOR
        public void EditorConfigure(ContentClearOverlay resultOverlay, params TMP_Text[] rewardSlots)
        {
            overlay = resultOverlay;
            rewardSlotTexts = rewardSlots;
        }
#endif
    }
}
