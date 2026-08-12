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
        [SerializeField] private TMP_Text[] overviewCoreBonusTexts = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] coreBonusLevelTexts = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] coreBonusValueTexts = Array.Empty<TMP_Text>();

        private IGameProgressService progress;
        private CommanderGrowthConfig config;
        private Action levelChanged;
        private bool savePending;
        private string feedbackMessage;

        private void Awake()
        {
            closeButton?.onClick.AddListener(Close);
            levelUpButton?.onClick.AddListener(LevelUp);
        }

        private void OnEnable()
        {
            feedbackMessage = null;
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
            var currentRate = config.GetAccumulatedCoreStatRate(commander.Level);
            var nextLevel = Mathf.Min(commander.Level + 1, config.MaxLevel);
            var nextRate = config.GetAccumulatedCoreStatRate(nextLevel);

            SetText(
                goldText,
                feedbackMessage ??
                (isMaxLevel
                    ? $"최대 군단 보너스  {FormatBonus(currentRate)}"
                    : $"다음 레벨 군단 보너스  {FormatBonus(nextRate)}"));
            SetText(
                commanderLevelText,
                $"군단장 LV. {commander.Level:N0} · 핵심 능력치 {FormatBonus(currentRate)}");
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

            SetTexts(overviewCoreBonusTexts, FormatBonus(currentRate));
            SetTexts(coreBonusLevelTexts, $"LV. {commander.Level:N0}");
            SetTexts(
                coreBonusValueTexts,
                isMaxLevel
                    ? $"현재 {FormatBonus(currentRate)} · MAX"
                    : $"현재 {FormatBonus(currentRate)}  →  다음 {FormatBonus(nextRate)}");
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
            feedbackMessage = null;
            Refresh();
            try
            {
                var saved = await progress.TryApplyAndSaveAsync(
                    GameProgressChange.LevelUpCommander(commander.Level));
                if (saved)
                {
                    feedbackMessage = "레벨업 저장 완료 · 다음 전투부터 적용";
                    levelChanged?.Invoke();
                }
                else
                {
                    feedbackMessage = "레벨업 저장 실패 · 다시 시도해 주세요";
                }
            }
            catch (Exception exception)
            {
                feedbackMessage = "레벨업 저장 실패 · 다시 시도해 주세요";
                Debug.LogException(exception);
            }
            finally
            {
                savePending = false;
                Refresh();
            }
        }

        private static string FormatPercent(double ratio) => $"{Math.Max(0d, ratio) * 100d:0.0}%";

        private static string FormatBonus(float rate) => $"+{Mathf.Max(0f, rate) * 100f:0.#}%";

        private static void SetTexts(TMP_Text[] targets, string value)
        {
            if (targets == null)
            {
                return;
            }

            for (var index = 0; index < targets.Length; index++)
            {
                SetText(targets[index], value);
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
