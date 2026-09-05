using System;
using System.Collections.Generic;
using ProjectMT.Contents.Framework;
using ProjectMT.Features.Quest;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Quest;
using ProjectMT.Shared.UI;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.GrowthDungeon
{
    [DisallowMultipleComponent]
    public sealed class GrowthDungeonStageEntryPopupView : MonoBehaviour // 기존 팝업 시각 참조 어댑터
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button enterButton;
        [SerializeField] private Button sweepButton;
        [SerializeField] private TMP_Text stageLabel;
        [SerializeField] private TMP_Text stageValue;
        [SerializeField] private TMP_Text rewardTitle;
        [SerializeField] private TMP_Text rewardAmount;
        [SerializeField] private TMP_Text keyCount;
        [SerializeField] private TMP_Text highestStageText;
        [SerializeField] private TMP_Text enterCostText;
        [SerializeField] private TMP_Text sweepCostText;
        private bool initialized;

        public void Initialize(
            Action previous,
            Action next,
            Action enter,
            Action sweep,
            Action close)
        {
            ResolveReferences();
            if (initialized)
            {
                return;
            }

            previousButton?.onClick.AddListener(() => previous?.Invoke());
            nextButton?.onClick.AddListener(() => next?.Invoke());
            enterButton?.onClick.AddListener(() => enter?.Invoke());
            sweepButton?.onClick.AddListener(() => sweep?.Invoke());
            closeButton?.onClick.AddListener(() => close?.Invoke());
            initialized = true;
        }

        public void Render(
            string displayName,
            GrowthDungeonEntryState state,
            int selectedStage,
            bool runtimeAvailable,
            bool busy)
        {
            ResolveReferences();
            var mode = runtimeAvailable
                ? GrowthDungeonEntryRules.ResolveMode(state, selectedStage)
                : ContentRunMode.Challenge;
            var isChallenge = mode == ContentRunMode.Challenge;
            SetText(stageLabel, runtimeAvailable
                ? (isChallenge ? "도전 단계" : "파밍 단계")
                : "준비 중");
            SetText(stageValue, Mathf.Max(1, selectedStage).ToString("00"));
            SetText(highestStageText, $"최고 클리어  {Mathf.Max(0, state.HighestClearedStage)}단계");
            SetText(rewardTitle, runtimeAvailable
                ? (isChallenge ? "도전 클리어 보상  <color=#E0BC7A>200%</color>" : "파밍 보상  <color=#E0BC7A>100%</color>")
                : displayName);
            SetText(rewardAmount, runtimeAvailable ? "클리어 결과에 따라 획득" : "콘텐츠 준비 중");
            SetText(
                keyCount,
                $"{Math.Min(state.KeyQuantity, GrowthDungeonDailyKeyRules.MaximumQuantity)} / " +
                GrowthDungeonDailyKeyRules.MaximumQuantity);

            if (previousButton != null)
            {
                previousButton.interactable = runtimeAvailable && !busy && selectedStage > 1;
            }

            if (nextButton != null)
            {
                nextButton.interactable = runtimeAvailable && !busy &&
                    selectedStage < state.MaximumSelectableStage;
            }

            if (enterButton != null)
            {
                enterButton.interactable = !busy &&
                    GrowthDungeonEntryRules.CanEnter(state, selectedStage, runtimeAvailable);
                SetButtonText(
                    enterButton,
                    runtimeAvailable
                        ? (isChallenge ? "도전 입장" : "파밍 입장")
                        : "준비 중");
            }
            SetText(enterCostText, runtimeAvailable
                ? (isChallenge ? "열쇠 미소모" : "열쇠 1개 사용")
                : string.Empty);

            if (sweepButton != null)
            {
                sweepButton.interactable = runtimeAvailable && !busy && state.CanSweep;
                SetButtonText(
                    sweepButton,
                    runtimeAvailable && state.HighestClearedStage > 0
                        ? $"{state.HighestClearedStage}단계 소탕"
                        : "소탕 불가");
            }
            SetText(sweepCostText, runtimeAvailable && state.HighestClearedStage > 0
                ? "열쇠 1개 사용"
                : string.Empty);
        }

        private void ResolveReferences()
        {
            closeButton ??= transform.Find("CloseTouchArea_80x80")?.GetComponent<Button>();
            previousButton ??= FindComponent<Button>("ApprovedVisualRoot/StageSelection/PreviousStageButton",
                "ContentRoot/MainContent/StageSelectorRoot/PreviousStageButton");
            nextButton ??= FindComponent<Button>("ApprovedVisualRoot/StageSelection/NextStageButton",
                "ContentRoot/MainContent/StageSelectorRoot/NextStageButton");
            enterButton ??= FindComponent<Button>("ApprovedVisualRoot/EnterButton", "FooterActionRoot/EnterButton");
            sweepButton ??= FindComponent<Button>("ApprovedVisualRoot/SweepButton", "FooterActionRoot/SweepButton");
            stageLabel ??= FindComponent<TMP_Text>("ApprovedVisualRoot/StageSelection/StageLabel",
                "ContentRoot/MainContent/StageSelectorRoot/StageLabel");
            stageValue ??= FindComponent<TMP_Text>("ApprovedVisualRoot/StageSelection/StageValue",
                "ContentRoot/MainContent/StageSelectorRoot/StageValue");
            rewardTitle ??= FindComponent<TMP_Text>("ApprovedVisualRoot/RewardAndKey/RewardTitle",
                "ContentRoot/MainContent/ClearRewardRoot/RewardTitle");
            rewardAmount ??= FindComponent<TMP_Text>("ApprovedVisualRoot/RewardAndKey/RewardAmount",
                "ContentRoot/MainContent/ClearRewardRoot/RewardAmount");
            keyCount ??= FindComponent<TMP_Text>("ApprovedVisualRoot/RewardAndKey/KeyCount",
                "ContentRoot/MainContent/DungeonKeyRoot/KeyCount");
            highestStageText ??= transform.Find("ApprovedVisualRoot/StageSelection/HighestStageText")?.GetComponent<TMP_Text>();
            enterCostText ??= transform.Find("ApprovedVisualRoot/EnterCostText")?.GetComponent<TMP_Text>();
            sweepCostText ??= transform.Find("ApprovedVisualRoot/SweepCostText")?.GetComponent<TMP_Text>();
        }

        private T FindComponent<T>(string preferredPath, string fallbackPath) where T : Component
        {
            return transform.Find(preferredPath)?.GetComponent<T>() ??
                transform.Find(fallbackPath)?.GetComponent<T>();
        }

        private static void SetButtonText(Button button, string value)
        {
            var text = button == null ? null : button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = value;
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }
    }
}
