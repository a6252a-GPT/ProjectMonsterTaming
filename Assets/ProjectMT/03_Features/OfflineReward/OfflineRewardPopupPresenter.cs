using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.OfflineReward
{
    [DisallowMultipleComponent]
    public sealed class OfflineRewardPopupPresenter : MonoBehaviour // 정산 완료 영수증 표시·확인 저장
    {
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text capText;
        [SerializeField] private TMP_Text goldRateText;
        [SerializeField] private TMP_Text experienceRateText;
        [SerializeField] private TMP_Text stoneRateText;
        [SerializeField] private TMP_Text equipmentRateText;
        [SerializeField] private TMP_Text goldRewardText;
        [SerializeField] private TMP_Text experienceRewardText;
        [SerializeField] private TMP_Text stoneRewardText;
        [SerializeField] private TMP_Text equipmentRewardText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button adButton;
        [SerializeField] private Button claimButton;
        [SerializeField] private Button closeButton;

        private Func<Task<bool>> acknowledge;
        private Action<OfflineRewardPresentation> confirmed;
        private OfflineRewardPresentation current;
        private bool busy;

        private GameObject DisplayRoot => transform.parent != null ? transform.parent.gameObject : gameObject;
        public bool IsOpen => DisplayRoot.activeSelf;

        private void Awake()
        {
            adButton?.onClick.RemoveListener(HandleAdClicked);
            adButton?.onClick.AddListener(HandleAdClicked);
            claimButton?.onClick.RemoveListener(HandleClaimClicked);
            claimButton?.onClick.AddListener(HandleClaimClicked);
            closeButton?.onClick.RemoveListener(Hide);
            closeButton?.onClick.AddListener(Hide);
            if (adButton != null)
            {
                adButton.interactable = false; // 광고 SDK 연결 전 비활성 유지
            }

            DisplayRoot.SetActive(false);
        }

        public void Show(
            OfflineRewardPresentation presentation,
            Func<Task<bool>> acknowledgeRequest,
            Action<OfflineRewardPresentation> onConfirmed)
        {
            if (presentation == null)
            {
                return;
            }

            current = presentation;
            acknowledge = acknowledgeRequest;
            confirmed = onConfirmed;
            busy = false;
            Bind(presentation);
            DisplayRoot.SetActive(true);
            if (claimButton != null)
            {
                claimButton.interactable = true;
            }

            if (closeButton != null)
            {
                closeButton.interactable = true;
            }
        }

        public void Hide()
        {
            if (busy)
            {
                return;
            }

            DisplayRoot.SetActive(false); // X는 영수증을 지우지 않아 다음 접속에 재표시
        }

        private void Bind(OfflineRewardPresentation presentation)
        {
            Set(timeText, $"방치 시간  {FormatDuration(presentation.ElapsedSeconds)}");
            Set(
                stageText,
                presentation.MixedBasis
                    ? "최종 원정대 : 접속별 기준"
                    : $"최종 원정대 : {presentation.BasisStage}");
            Set(capText, presentation.Capped ? "최대 누적 시간이 적용되었습니다" : "정상 누적");
            Set(
                goldRateText,
                presentation.MixedBasis
                    ? $"+{presentation.Gold:N0} · 접속별 합산"
                    : $"+{presentation.Gold:N0} · {presentation.GoldPerMinute:N0}/60s");
            Set(
                experienceRateText,
                presentation.MixedBasis
                    ? $"+{presentation.CommanderExperience:N0} · 접속별 합산"
                    : $"+{presentation.CommanderExperience:N0} · {presentation.CommanderExperiencePerMinute:N0}/60s");
            Set(
                stoneRateText,
                presentation.MixedBasis
                    ? $"+{presentation.UpgradeStone:N0} · 접속별 합산"
                    : $"+{presentation.UpgradeStone:N0} · 1/{presentation.UpgradeStoneIntervalSeconds:N0}s");
            Set(equipmentRateText, "준비 중");
            Set(goldRewardText, $"골드\n+{presentation.Gold:N0}");
            Set(experienceRewardText, $"군단장 경험치\n+{presentation.CommanderExperience:N0}");
            Set(stoneRewardText, $"장비 슬롯 강화석\n+{presentation.UpgradeStone:N0}");
            Set(equipmentRewardText, "장비\n준비 중");
            Set(statusText, "정산 저장 완료");
        }

        private async void HandleClaimClicked()
        {
            if (busy || acknowledge == null || current == null)
            {
                return;
            }

            busy = true;
            if (claimButton != null)
            {
                claimButton.interactable = false;
            }

            if (closeButton != null)
            {
                closeButton.interactable = false;
            }

            Set(statusText, "확인 상태 저장 중...");
            bool saved;
            try
            {
                saved = await acknowledge();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                saved = false;
            }

            busy = false;
            if (!saved)
            {
                Set(statusText, "저장하지 못했습니다. 다시 눌러주세요");
                if (claimButton != null)
                {
                    claimButton.interactable = true;
                }

                if (closeButton != null)
                {
                    closeButton.interactable = true;
                }

                return;
            }

            var completed = current;
            DisplayRoot.SetActive(false);
            current = null;
            acknowledge = null;
            confirmed?.Invoke(completed);
            confirmed = null;
        }

        private void HandleAdClicked()
        {
            Set(statusText, "광고 2배는 현재 준비 중입니다");
        }

        private static string FormatDuration(long totalSeconds)
        {
            totalSeconds = Math.Max(0L, totalSeconds);
            var hours = totalSeconds / 3600L;
            var minutes = totalSeconds % 3600L / 60L;
            var seconds = totalSeconds % 60L;
            return $"{hours:00}:{minutes:00}:{seconds:00}";
        }

        private static void Set(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            TMP_Text offlineTime,
            TMP_Text basisStage,
            TMP_Text capState,
            TMP_Text goldRate,
            TMP_Text experienceRate,
            TMP_Text stoneRate,
            TMP_Text equipmentRate,
            TMP_Text goldReward,
            TMP_Text experienceReward,
            TMP_Text stoneReward,
            TMP_Text equipmentReward,
            TMP_Text state,
            Button advertisement,
            Button claim,
            Button close)
        {
            timeText = offlineTime;
            stageText = basisStage;
            capText = capState;
            goldRateText = goldRate;
            experienceRateText = experienceRate;
            stoneRateText = stoneRate;
            equipmentRateText = equipmentRate;
            goldRewardText = goldReward;
            experienceRewardText = experienceReward;
            stoneRewardText = stoneReward;
            equipmentRewardText = equipmentReward;
            statusText = state;
            adButton = advertisement;
            claimButton = claim;
            closeButton = close;
        }
#endif
    }
}
