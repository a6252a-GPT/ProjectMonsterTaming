using System;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Stats;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Commander
{
    [DisallowMultipleComponent]
    public sealed class CommanderGrowthPageView : MonoBehaviour
    {
        [SerializeField] private Button closeButton;

        [Header("군단장 성장")]
        [SerializeField] private TMP_Text commanderLevelText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text experienceText;
        [SerializeField] private Slider experienceSlider;
        [SerializeField] private Button levelUpButton;
        [SerializeField] private TMP_Text levelUpButtonText;
        [SerializeField] private GameObject levelUpReadyBadge;
        [SerializeField] private TMP_Text goldText;

        private IGameProgressService progress;
        private CommanderGrowthConfig config;
        private Action levelChanged;
        private bool savePending;

        private void Awake()
        {
            closeButton?.onClick.AddListener(Close);
            levelUpButton?.onClick.AddListener(LevelUp);
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            closeButton?.onClick.RemoveListener(Close);
            levelUpButton?.onClick.RemoveListener(LevelUp);
            if (progress != null)
            {
                progress.Changed -= Refresh;
            }
        }

        public void Configure(
            IGameProgressService progressService,
            CommanderGrowthConfig growthConfig,
            Action onLevelChanged = null)
        {
            if (progress != null)
            {
                progress.Changed -= Refresh;
            }

            progress = progressService;
            config = growthConfig;
            levelChanged = onLevelChanged;
            if (progress != null)
            {
                progress.Changed += Refresh;
            }

            Refresh();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void Refresh()
        {
            if (progress == null || !progress.IsLoaded || config == null)
            {
                return;
            }

            var progressView = progress.View;
            var commander = progressView.Commander;
            var requirement = config.GetExperienceRequirement(commander.Level);
            var progressRatio = config.GetProgressRatio(commander.Level, commander.Experience);
            var progress01 = config.GetProgress01(commander.Level, commander.Experience);
            var isMaxLevel = commander.Level >= config.MaxLevel;
            var canLevelUp = !isMaxLevel && config.CanLevelUp(commander.Level, commander.Experience);

            SetText(goldText, $"보유 골드  {progressView.Gold:N0}");
            SetText(commanderLevelText, $"군단장 LV. {commander.Level:N0} ({FormatPercent(progressRatio)})");
            SetText(levelText, $"LV. {commander.Level:N0}");
            SetText(
                experienceText,
                isMaxLevel
                    ? "MAX"
                    : $"{commander.Experience:N0} / {requirement:N0} ({FormatPercent(progressRatio)})");
            if (experienceSlider != null)
            {
                experienceSlider.SetValueWithoutNotify(progress01);
            }

            if (levelUpButton != null)
            {
                levelUpButton.interactable = canLevelUp && !savePending;
            }

            SetText(levelUpButtonText, isMaxLevel ? "MAX" : "레벨 업");
            levelUpReadyBadge?.SetActive(canLevelUp);
        }

        private async void LevelUp()
        {
            if (savePending || progress == null || !progress.IsLoaded || config == null)
            {
                return;
            }

            var commander = progress.View.Commander;
            if (!config.CanLevelUp(commander.Level, commander.Experience))
            {
                Refresh();
                return;
            }

            savePending = true;
            Refresh();
            try
            {
                if (await progress.TryApplyAndSaveAsync(GameProgressChange.LevelUpCommander(commander.Level)))
                {
                    levelChanged?.Invoke();
                }
            }
            finally
            {
                savePending = false;
                Refresh();
            }
        }

        private static string FormatPercent(double ratio) => $"{Math.Max(0d, ratio) * 100d:0.0}%";

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
