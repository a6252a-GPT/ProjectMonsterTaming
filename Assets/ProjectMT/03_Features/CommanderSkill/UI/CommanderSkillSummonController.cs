using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProjectMT.Features.CommanderSkill
{
    [DisallowMultipleComponent]
    public sealed class CommanderSkillSummonController : MonoBehaviour // 몬스터 뽑기와 분리된 전용 소환 흐름
    {
        private sealed class ResultSummary
        {
            public CommanderSkillDefinition Definition;
            public int Count;
            public bool IsNew;
        }

        [Header("메인 정보")]
        [SerializeField] private TMP_Text summonLevelText;
        [SerializeField] private TMP_Text summonProgressText;
        [SerializeField] private Image summonProgressFill;
        [SerializeField] private TMP_Text summonTicketText;
        [SerializeField] private TMP_Text currentRateSummaryText;
        [SerializeField] private TMP_Text statusText;

        [Header("소환 버튼")]
        [SerializeField] private Button advertisementButton;
        [SerializeField] private Button[] offerButtons = Array.Empty<Button>();
        [SerializeField] private TMP_Text[] offerTexts = Array.Empty<TMP_Text>();
        [SerializeField] private int[] offerDrawCounts = { 10, 30, 300 };

        [Header("소환 레벨 정보")]
        [SerializeField] private Button levelInfoButton;
        [SerializeField] private GameObject levelInfoPopup;
        [SerializeField] private Button levelInfoCloseButton;
        [SerializeField] private Button previousLevelButton;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private TMP_Text inspectedLevelText;
        [SerializeField] private TMP_Text inspectedThresholdText;
        [SerializeField] private TMP_Text inspectedProbabilityText;
        [SerializeField] private TMP_Text inspectedRewardText;

        [Header("소환 결과")]
        [SerializeField] private GameObject resultOverlay;
        [SerializeField] private RectTransform resultItemsRoot;
        [SerializeField] private CommanderSkillSummonResultItemView resultItemPrefab;
        [SerializeField] private TMP_Text resultTitleText;
        [SerializeField] private TMP_Text resultSummaryText;
        [SerializeField] private Button resultCloseButton;

        private readonly List<CommanderSkillSummonResultItemView> spawnedResults =
            new List<CommanderSkillSummonResultItemView>();
        private UnityAction[] offerActions = Array.Empty<UnityAction>();
        private IGameProgressService progress;
        private CommanderSkillCatalog catalog;
        private CommanderSkillSummonConfig summonConfig;
        private bool listenersBound;
        private bool isSummoning;
        private int lifetimeVersion;
        private int inspectedLevel = 1;

        private void Awake()
        {
            BindListeners();
        }

        private void OnEnable()
        {
            BindListeners();
            SubscribeProgress();
            HideResults();
            HideLevelInfo();
            Refresh();
        }

        private void OnDisable()
        {
            lifetimeVersion++;
            isSummoning = false;
            UnsubscribeProgress();
            HideResults();
            HideLevelInfo();
        }

        private void OnDestroy()
        {
            Shutdown();
            UnbindListeners();
            ClearResults();
        }

        public void Configure(IGameProgressService progressService, CommanderSkillCatalog skillCatalog)
        {
            lifetimeVersion++;
            isSummoning = false;
            UnsubscribeProgress();
            progress = progressService;
            catalog = skillCatalog;
            summonConfig = catalog?.SummonConfig ?? CommanderSkillSummonConfig.RuntimeDefault;
            SubscribeProgress();

            inspectedLevel = progress == null ? 1 : progress.View.CommanderSkills.SummonLevel;
            Refresh();
        }

        public void Shutdown()
        {
            lifetimeVersion++;
            isSummoning = false;
            UnsubscribeProgress();
            progress = null;
            catalog = null;
            summonConfig = null;
            HideResults();
            HideLevelInfo();
        }

        private void BindListeners()
        {
            if (listenersBound)
            {
                return;
            }

            listenersBound = true;
            levelInfoButton?.onClick.AddListener(ShowLevelInfo);
            levelInfoCloseButton?.onClick.AddListener(HideLevelInfo);
            previousLevelButton?.onClick.AddListener(ShowPreviousLevel);
            nextLevelButton?.onClick.AddListener(ShowNextLevel);
            resultCloseButton?.onClick.AddListener(HideResults);

            var count = Mathf.Min(offerButtons?.Length ?? 0, offerDrawCounts?.Length ?? 0);
            offerActions = new UnityAction[count];
            for (var index = 0; index < count; index++)
            {
                var drawCount = offerDrawCounts[index];
                UnityAction action = () => RequestSummon(drawCount);
                offerActions[index] = action;
                offerButtons[index]?.onClick.AddListener(action);
            }
        }

        private void UnbindListeners()
        {
            if (!listenersBound)
            {
                return;
            }

            listenersBound = false;
            if (levelInfoButton != null)
            {
                levelInfoButton.onClick.RemoveListener(ShowLevelInfo);
            }

            if (levelInfoCloseButton != null)
            {
                levelInfoCloseButton.onClick.RemoveListener(HideLevelInfo);
            }

            if (previousLevelButton != null)
            {
                previousLevelButton.onClick.RemoveListener(ShowPreviousLevel);
            }

            if (nextLevelButton != null)
            {
                nextLevelButton.onClick.RemoveListener(ShowNextLevel);
            }

            if (resultCloseButton != null)
            {
                resultCloseButton.onClick.RemoveListener(HideResults);
            }

            var count = Mathf.Min(offerButtons?.Length ?? 0, offerActions.Length);
            for (var index = 0; index < count; index++)
            {
                if (offerButtons[index] != null)
                {
                    offerButtons[index].onClick.RemoveListener(offerActions[index]);
                }
            }

            offerActions = Array.Empty<UnityAction>();
        }

        private async void RequestSummon(int drawCount)
        {
            await SummonAsync(drawCount);
        }

        private async Task SummonAsync(int drawCount)
        {
            var progressService = progress;
            if (isSummoning || progressService == null || !progressService.IsLoaded ||
                catalog == null || summonConfig == null)
            {
                SetStatus("스킬 소환 설정을 확인해 주세요");
                return;
            }

            if (!catalog.TryValidate(out var catalogError))
            {
                SetStatus(catalogError);
                return;
            }

            if (!summonConfig.TryGetOffer(drawCount, out var offer))
            {
                SetStatus("지원하지 않는 스킬 소환 상품입니다");
                return;
            }

            progressService.View.Items.TryGetQuantity(summonConfig.TicketItemId, out var tickets);
            progressService.View.Items.TryGetQuantity(ItemIds.Diamond, out var diamonds);
            var payment = summonConfig.CalculatePayment(offer, tickets);
            if (!payment.CanAfford(diamonds))
            {
                SetStatus($"소환권 부족분 결제에 다이아가 {payment.DiamondCost - diamonds:N0}개 부족합니다");
                Refresh();
                return;
            }

            var requestVersion = lifetimeVersion;
            isSummoning = true;
            HideResults();
            SetStatus("소환 결과를 확정하는 중입니다...");
            RefreshButtons();
            try
            {
                var expectedCount = progressService.View.CommanderSkills.SummonCount;
                var ownedBefore = new HashSet<string>(
                    progressService.View.CommanderSkills.OwnedSkills.Select(skill => skill.SkillId),
                    StringComparer.Ordinal);
                if (!TryPlanResults(expectedCount, drawCount, out var resultIds))
                {
                    SetStatus("현재 소환 풀을 확인해 주세요");
                    return;
                }

                bool saved;
                try
                {
                    saved = await progressService.TryApplyAndSaveAsync(
                        GameProgressChange.RecordPaidCommanderSkillSummons(
                            expectedCount,
                            drawCount,
                            resultIds));
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                    saved = false;
                }

                if (!IsCurrentRequest(requestVersion))
                {
                    return;
                }

                if (!saved)
                {
                    SetStatus("소환 저장에 실패했습니다 · 소환권과 결과는 반영되지 않았습니다");
                    return;
                }

                ShowResults(drawCount, resultIds, ownedBefore);
                SetStatus($"{drawCount:N0}회 스킬 소환이 완료되었습니다");
            }
            finally
            {
                if (IsCurrentRequest(requestVersion))
                {
                    isSummoning = false;
                    Refresh();
                }
            }
        }

        private bool TryPlanResults(int expectedCount, int drawCount, out List<string> resultIds)
        {
            resultIds = new List<string>(Mathf.Max(0, drawCount));
            var seed = unchecked(Environment.TickCount * 397 ^ expectedCount ^ DateTime.UtcNow.Ticks.GetHashCode());
            var random = new System.Random(seed);
            for (var index = 0; index < drawCount; index++)
            {
                var level = summonConfig.GetSummonLevel(
                    expectedCount > int.MaxValue - index ? int.MaxValue : expectedCount + index);
                var skillId = summonConfig.RollSkillId(random, level);
                if (string.IsNullOrWhiteSpace(skillId) || !catalog.TryGet(skillId, out _))
                {
                    resultIds.Clear();
                    return false;
                }

                resultIds.Add(skillId);
            }

            return resultIds.Count == drawCount;
        }

        private void ShowResults(int drawCount, IReadOnlyList<string> resultIds, ISet<string> ownedBefore)
        {
            if (resultOverlay == null || resultItemsRoot == null || resultItemPrefab == null)
            {
                return;
            }

            ClearResults();
            var summaries = new Dictionary<string, ResultSummary>(StringComparer.Ordinal);
            for (var index = 0; index < resultIds.Count; index++)
            {
                var skillId = resultIds[index];
                if (!summaries.TryGetValue(skillId, out var summary))
                {
                    if (!catalog.TryGet(skillId, out var definition))
                    {
                        continue;
                    }

                    summary = new ResultSummary
                    {
                        Definition = definition,
                        IsNew = ownedBefore == null || !ownedBefore.Contains(skillId)
                    };
                    summaries.Add(skillId, summary);
                }

                summary.Count++;
            }

            var ordered = summaries.Values
                .OrderByDescending(summary => summary.Count)
                .ThenBy(summary => summary.Definition.DisplayName, StringComparer.Ordinal)
                .ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                var item = Instantiate(resultItemPrefab, resultItemsRoot);
                item.name = $"SkillResult_{index + 1:00}_{ordered[index].Definition.SkillId}";
                item.Bind(ordered[index].Definition, ordered[index].Count, ordered[index].IsNew);
                spawnedResults.Add(item);
            }

            if (resultTitleText != null)
            {
                resultTitleText.text = $"군단장 스킬 소환 결과 · {drawCount:N0}회";
            }

            if (resultSummaryText != null)
            {
                resultSummaryText.text = $"획득 종류 {ordered.Length:N0}개  ·  중복은 스킬 레벨업 재료로 저장됩니다";
            }

            resultOverlay.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(resultItemsRoot);
        }

        private void HideResults()
        {
            if (resultOverlay != null)
            {
                resultOverlay.SetActive(false);
            }

            ClearResults();
        }

        private void ClearResults()
        {
            for (var index = spawnedResults.Count - 1; index >= 0; index--)
            {
                if (spawnedResults[index] != null)
                {
                    Destroy(spawnedResults[index].gameObject);
                }
            }

            spawnedResults.Clear();
        }

        private void ShowLevelInfo()
        {
            inspectedLevel = progress == null
                ? 1
                : progress.View.CommanderSkills.SummonLevel;
            RefreshLevelInfo();
            levelInfoPopup?.SetActive(true);
        }

        private void HideLevelInfo()
        {
            if (levelInfoPopup != null)
            {
                levelInfoPopup.SetActive(false);
            }
        }

        private void ShowPreviousLevel()
        {
            inspectedLevel = Mathf.Max(1, inspectedLevel - 1);
            RefreshLevelInfo();
        }

        private void ShowNextLevel()
        {
            inspectedLevel = Mathf.Min(summonConfig?.MaxSummonLevel ?? 1, inspectedLevel + 1);
            RefreshLevelInfo();
        }

        private void Refresh()
        {
            var view = progress?.View.CommanderSkills ?? default;
            var level = Mathf.Max(1, view.SummonLevel);
            if (summonLevelText != null)
            {
                summonLevelText.text = $"소환 Lv.{level}";
            }

            var count = Mathf.Max(0, view.SummonCount);
            if (summonConfig != null && summonConfig.TryGetNextLevelThreshold(level, out var nextThreshold))
            {
                var start = summonConfig.GetLevelStartCount(level);
                var span = Mathf.Max(1, nextThreshold - start);
                var progressCount = Mathf.Clamp(count - start, 0, span);
                if (summonProgressText != null)
                {
                    summonProgressText.text = $"{progressCount:N0} / {span:N0}";
                }

                if (summonProgressFill != null)
                {
                    summonProgressFill.fillAmount = progressCount / (float)span;
                }
            }
            else
            {
                if (summonProgressText != null)
                {
                    summonProgressText.text = $"누적 {count:N0}회 · MAX";
                }

                if (summonProgressFill != null)
                {
                    summonProgressFill.fillAmount = 1f;
                }
            }

            var tickets = 0L;
            if (progress != null && summonConfig != null)
            {
                progress.View.Items.TryGetQuantity(summonConfig.TicketItemId, out tickets);
            }

            if (summonTicketText != null)
            {
                summonTicketText.text = $"스킬 소환권  {tickets:N0}";
            }

            if (currentRateSummaryText != null)
            {
                currentRateSummaryText.text = BuildProbabilityText(level, compact: true);
            }

            RefreshButtons();
            if (levelInfoPopup != null && levelInfoPopup.activeSelf)
            {
                RefreshLevelInfo();
            }
        }

        private void RefreshButtons()
        {
            if (advertisementButton != null)
            {
                advertisementButton.interactable = false; // 광고 SDK 연결 전 지급 금지
            }

            var tickets = 0L;
            var diamonds = 0L;
            if (progress != null && summonConfig != null)
            {
                progress.View.Items.TryGetQuantity(summonConfig.TicketItemId, out tickets);
                progress.View.Items.TryGetQuantity(ItemIds.Diamond, out diamonds);
            }

            var count = Mathf.Min(offerButtons?.Length ?? 0, offerDrawCounts?.Length ?? 0);
            for (var index = 0; index < count; index++)
            {
                var drawCount = offerDrawCounts[index];
                CommanderSkillSummonOffer offer = null;
                var validOffer = summonConfig != null && summonConfig.TryGetOffer(drawCount, out offer);
                var payment = validOffer
                    ? summonConfig.CalculatePayment(offer, tickets)
                    : default;
                if (offerButtons[index] != null)
                {
                    offerButtons[index].interactable = !isSummoning && progress != null && progress.IsLoaded &&
                                                       validOffer && payment.CanAfford(diamonds);
                }

                if (offerTexts != null && index < offerTexts.Length && offerTexts[index] != null)
                {
                    offerTexts[index].text = validOffer
                        ? $"{drawCount:N0}회 소환\n<color=#B9C8D8>{BuildPaymentText(payment)}</color>"
                        : $"{drawCount:N0}회 · 준비 중";
                }
            }
        }

        private static string BuildPaymentText(CommanderSkillSummonPayment payment)
        {
            if (payment.DiamondCost <= 0L)
            {
                return $"소환권 {payment.TicketCost:N0}";
            }

            if (payment.TicketCost <= 0)
            {
                return $"다이아 {payment.DiamondCost:N0}";
            }

            return $"소환권 {payment.TicketCost:N0} + 다이아 {payment.DiamondCost:N0}";
        }

        private void RefreshLevelInfo()
        {
            if (summonConfig == null)
            {
                return;
            }

            inspectedLevel = Mathf.Clamp(inspectedLevel, 1, summonConfig.MaxSummonLevel);
            var currentLevel = progress == null ? 1 : progress.View.CommanderSkills.SummonLevel;
            if (inspectedLevelText != null)
            {
                inspectedLevelText.text = inspectedLevel == currentLevel
                    ? $"Lv.{inspectedLevel}  ·  현재"
                    : $"Lv.{inspectedLevel}";
            }

            if (inspectedThresholdText != null)
            {
                inspectedThresholdText.text =
                    $"누적 소환 {summonConfig.GetLevelStartCount(inspectedLevel):N0}회부터 적용";
            }

            if (inspectedProbabilityText != null)
            {
                inspectedProbabilityText.text = BuildProbabilityText(inspectedLevel, compact: false);
            }

            if (inspectedRewardText != null)
            {
                inspectedRewardText.text = "레벨 달성 보상은 정식 밸런스 확정 후 연결됩니다";
            }

            if (previousLevelButton != null)
            {
                previousLevelButton.interactable = inspectedLevel > 1;
            }

            if (nextLevelButton != null)
            {
                nextLevelButton.interactable = inspectedLevel < summonConfig.MaxSummonLevel;
            }
        }

        private string BuildProbabilityText(int summonLevel, bool compact)
        {
            if (summonConfig == null || catalog == null)
            {
                return "확률 정보 없음";
            }

            var total = summonConfig.GetTotalWeight(summonLevel);
            if (total <= 0)
            {
                return "확률 정보 없음";
            }

            var lines = new List<string>();
            var pool = summonConfig.GetPool(summonLevel);
            for (var index = 0; index < pool.Count; index++)
            {
                var entry = pool[index];
                if (entry == null || entry.Weight <= 0 || !catalog.TryGet(entry.SkillId, out var definition))
                {
                    continue;
                }

                var probability = entry.Weight * 100f / total;
                lines.Add(compact
                    ? $"{definition.DisplayName} {probability:0.#}%"
                    : $"{definition.DisplayName,-10}  {probability,5:0.0}%  ·  {CategoryLabel(definition.Category)}");
            }

            return string.Join(compact ? "  ·  " : "\n", lines);
        }

        private static string CategoryLabel(CommanderSkillCategory category)
        {
            return category switch
            {
                CommanderSkillCategory.Buff => "버프형",
                CommanderSkillCategory.Debuff => "디버프형",
                _ => "공격형"
            };
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message ?? string.Empty;
            }
        }

        private void UnsubscribeProgress()
        {
            if (progress != null)
            {
                progress.Changed -= Refresh;
            }
        }

        private void SubscribeProgress()
        {
            if (progress == null || !isActiveAndEnabled)
            {
                return;
            }

            progress.Changed -= Refresh;
            progress.Changed += Refresh;
        }

        private bool IsCurrentRequest(int requestVersion)
        {
            return this != null && isActiveAndEnabled && requestVersion == lifetimeVersion;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            TMP_Text levelText,
            TMP_Text progressLabel,
            Image progressFill,
            TMP_Text ticketLabel,
            TMP_Text rateSummary,
            TMP_Text statusLabel,
            Button adButton,
            Button[] drawButtons,
            TMP_Text[] drawButtonTexts,
            int[] drawCounts,
            Button infoButton,
            GameObject infoPopup,
            Button infoClose,
            Button previousButton,
            Button nextButton,
            TMP_Text infoLevel,
            TMP_Text infoThreshold,
            TMP_Text infoProbability,
            TMP_Text infoReward,
            GameObject results,
            RectTransform resultsRoot,
            CommanderSkillSummonResultItemView resultPrefab,
            TMP_Text resultsTitle,
            TMP_Text resultsSummary,
            Button resultsClose)
        {
            summonLevelText = levelText;
            summonProgressText = progressLabel;
            summonProgressFill = progressFill;
            summonTicketText = ticketLabel;
            currentRateSummaryText = rateSummary;
            statusText = statusLabel;
            advertisementButton = adButton;
            offerButtons = drawButtons ?? Array.Empty<Button>();
            offerTexts = drawButtonTexts ?? Array.Empty<TMP_Text>();
            offerDrawCounts = drawCounts ?? Array.Empty<int>();
            levelInfoButton = infoButton;
            levelInfoPopup = infoPopup;
            levelInfoCloseButton = infoClose;
            previousLevelButton = previousButton;
            nextLevelButton = nextButton;
            inspectedLevelText = infoLevel;
            inspectedThresholdText = infoThreshold;
            inspectedProbabilityText = infoProbability;
            inspectedRewardText = infoReward;
            resultOverlay = results;
            resultItemsRoot = resultsRoot;
            resultItemPrefab = resultPrefab;
            resultTitleText = resultsTitle;
            resultSummaryText = resultsSummary;
            resultCloseButton = resultsClose;
        }
#endif
    }
}
