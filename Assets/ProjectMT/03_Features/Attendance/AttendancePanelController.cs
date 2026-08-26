using System;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Attendance
{
    [DisallowMultipleComponent]
    public sealed class AttendancePanelController : MonoBehaviour // 28일 출석 수령 팝업
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private Button outsideCloseButton;
        [SerializeField] private Button claimButton;
        [SerializeField] private TMP_Text cycleText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text claimButtonText;
        [SerializeField] private AttendanceDayCellView[] dayCells;
        [SerializeField] private AttendanceRewardCatalog rewardCatalog;

        private IGameProgressService progress;
        private ItemCatalog itemCatalog;
        private bool subscribed;
        private bool busy;

        public event Action<bool> OpenStateChanged;
        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            closeButton?.onClick.AddListener(Close);
            outsideCloseButton?.onClick.AddListener(Close);
            claimButton?.onClick.AddListener(ClaimCurrent);
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
            claimButton?.onClick.RemoveListener(ClaimCurrent);
            Unsubscribe();
        }

        public void Configure(
            IGameProgressService progressService,
            ItemCatalog catalog,
            AttendanceRewardCatalog attendanceRewards = null)
        {
            Unsubscribe();
            progress = progressService;
            itemCatalog = catalog;
            if (attendanceRewards != null)
            {
                rewardCatalog = attendanceRewards;
            }

            Subscribe();
            Refresh();
        }

        public void Open()
        {
            UIPanelPopAnimator.RequestOpen(gameObject);
            Refresh();
            OpenStateChanged?.Invoke(true);
        }

        public void Close()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            UIPanelPopAnimator.RequestClose(gameObject, () => OpenStateChanged?.Invoke(false));
        }

        private async void ClaimCurrent()
        {
            if (busy || progress == null || rewardCatalog == null)
            {
                return;
            }

            var attendance = progress.View.Attendance;
            if (!attendance.HasPendingReward ||
                !rewardCatalog.TryCreateReward(attendance.PendingRewardDay, out var reward))
            {
                SetStatus("현재 수령할 출석 보상이 없습니다.");
                return;
            }

            busy = true;
            RefreshClaimButton(attendance);
            var saved = await progress.TryApplyAndSaveAsync(GameProgressChange.ClaimAttendance(
                attendance.PendingRewardDay,
                attendance.LastProcessedPeriod,
                reward));
            busy = false;
            Refresh();
            SetStatus(saved ? "출석 보상을 받았습니다." : "보상 상태가 변경되었습니다. 다시 확인해 주세요.");
        }

        private void Refresh()
        {
            if (progress == null || rewardCatalog == null)
            {
                return;
            }

            var attendance = progress.View.Attendance;
            if (cycleText != null)
            {
                cycleText.text = $"{attendance.Cycle}회차 · 매일 05:00 갱신";
            }

            for (var index = 0; index < dayCells?.Length; index++)
            {
                var day = index + 1;
                if (dayCells[index] != null && rewardCatalog.TryGet(day, out var reward))
                {
                    dayCells[index].Refresh(
                        reward,
                        itemCatalog,
                        day <= attendance.ClaimedThroughDay,
                        attendance.HasPendingReward && day == attendance.PendingRewardDay);
                }
            }

            RefreshClaimButton(attendance);
            SetStatus(attendance.HasPendingReward
                ? $"DAY {attendance.PendingRewardDay} 보상을 받을 수 있습니다."
                : $"DAY {attendance.NextRewardDay} 보상은 다음 출석 갱신 후 열립니다.");
        }

        private void RefreshClaimButton(AttendanceProgressView attendance)
        {
            var canClaim = !busy && attendance.HasPendingReward;
            if (claimButton != null)
            {
                claimButton.interactable = canClaim;
            }

            if (claimButtonText != null)
            {
                claimButtonText.text = busy ? "수령 중..." : canClaim ? "오늘 보상 받기" : "수령 완료";
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
            Button claim,
            TMP_Text cycle,
            TMP_Text status,
            TMP_Text claimLabel,
            AttendanceDayCellView[] cells,
            AttendanceRewardCatalog catalog)
        {
            closeButton = close;
            outsideCloseButton = outsideClose;
            claimButton = claim;
            cycleText = cycle;
            statusText = status;
            claimButtonText = claimLabel;
            dayCells = cells;
            rewardCatalog = catalog;
        }
#endif
    }
}
