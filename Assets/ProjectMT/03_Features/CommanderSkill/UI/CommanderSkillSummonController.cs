using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.UI;
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
            public long ConvertedUpgradeStones;
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
        [SerializeField] private int[] offerDrawCounts = { 1, 10, 30 };

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

        [SerializeField] private SkillInscriptionSequence inscriptionSequence;
        [SerializeField] private ScrollRect resultScroll;

        [Header("결과 카드 배치")]
        [SerializeField, Min(1)] private int resultColumns = 5;
        [SerializeField] private Vector2 resultCardSize = new Vector2(196f, 250f);
        [SerializeField] private Vector2 compactResultCardSize = new Vector2(188f, 216f);
        [SerializeField] private Vector2 resultCardSpacing = new Vector2(16f, 14f);
        [SerializeField, Min(0.1f)] private float singleResultScale = 1.5f;

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
            inscriptionSequence?.Finish();
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
            inscriptionSequence?.Finish();
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
            inscriptionSequence?.Finish();
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
            try
            {
                HideResults();
                SetStatus("소환 결과를 확정하는 중입니다...");
                RefreshButtons();

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
                CommanderSkillSummonReceipt receipt = null;
                try
                {
                    if (progressService is not ICommanderSkillSummonService summonService)
                    {
                        SetStatus("확정 소환 결과를 지원하는 진행 서비스가 필요합니다.");
                        return;
                    }
                    receipt = await summonService.TrySummonCommanderSkillsAsync(
                        GameProgressChange.RecordPaidCommanderSkillSummons(
                            expectedCount,
                            drawCount,
                            resultIds));
                    saved = receipt != null;
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

                ShowCommittedResults(drawCount, resultIds, ownedBefore, receipt);
                SetStatus($"{drawCount:N0}회 스킬 소환이 완료되었습니다");
            }
            catch (Exception exception)
            {
                // 기획하지 못한 예외가 나도 소환 버튼이 영구히 잠기지 않도록 방어.
                Debug.LogException(exception, this);
                if (IsCurrentRequest(requestVersion))
                {
                    SetStatus("소환 처리 중 예상치 못한 오류가 발생했습니다");
                }
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
            => ShowCommittedResults(drawCount, resultIds, ownedBefore, null);

        private void ShowCommittedResults(int drawCount, IReadOnlyList<string> resultIds, ISet<string> ownedBefore,
            CommanderSkillSummonReceipt receipt)
        {
            if (resultOverlay == null || resultItemsRoot == null || resultItemPrefab == null)
            {
                return;
            }

            ClearResults();
            var hint = resultOverlay.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "CloseHint");
            if (hint != null) hint.text = drawCount == 30 ? "화면을 터치하면 전체 결과가 바로 공개됩니다" : "화면을 터치하면 한 장씩 바로 각인됩니다";
            var summaries = new Dictionary<string, ResultSummary>(StringComparer.Ordinal);
            for (var index = 0; index < resultIds.Count; index++)
            {
                var skillId = resultIds[index];
                var converted = receipt != null && receipt.Results[index].Kind == CommanderSkillSummonResultKind.Converted;
                var resultKey = skillId + (converted ? "|upgradeStones" : "");
                if (!summaries.TryGetValue(resultKey, out var summary))
                {
                    if (!catalog.TryGet(skillId, out var definition))
                    {
                        continue;
                    }

                    summary = new ResultSummary
                    {
                        Definition = definition,
                        IsNew = receipt == null ? ownedBefore == null || !ownedBefore.Contains(skillId) :
                            receipt.Results[index].Kind == CommanderSkillSummonResultKind.New
                    };
                    summaries.Add(resultKey, summary);
                }

                summary.Count++;
                if (converted) summary.ConvertedUpgradeStones += receipt.Results[index].ConvertedUpgradeStones;
            }

            var ordered = summaries.Values
                .OrderByDescending(summary => summary.Definition.Rarity)
                .ThenByDescending(summary => summary.Count)
                .ThenBy(summary => summary.Definition.DisplayName, StringComparer.Ordinal)
                .ToArray();
            var display = new List<ResultSummary>();
            if (drawCount == 1 || drawCount == 10)
            {
                var first = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < resultIds.Count; index++)
                {
                    var id = resultIds[index];
                    if (catalog.TryGet(id, out var definition))
                        display.Add(new ResultSummary { Definition = definition, Count = 1,
                            IsNew = receipt == null ? first.Add(id) && (ownedBefore == null || !ownedBefore.Contains(id)) :
                                receipt.Results[index].Kind == CommanderSkillSummonResultKind.New,
                            ConvertedUpgradeStones = receipt?.Results[index].ConvertedUpgradeStones ?? 0L });
                }
            }
            else display.AddRange(ordered);
            if (resultSummaryText != null)
                resultSummaryText.text = $"획득 종류 {ordered.Select(s => s.Definition.SkillId).Distinct().Count():N0}개 · 중복은 별각성 재료" +
                    (receipt != null && receipt.ConvertedUpgradeStones > 0 ? $" · 최대각성 전환 +{receipt.ConvertedUpgradeStones:N0} 강화석" : "");
            resultOverlay.transform.SetAsLastSibling();
            UIPanelPopAnimator.RequestOpen(resultOverlay, UIPanelPopStyle.RewardPopup);
            DisplayResults(display, $"군단장 스킬 소환 · {drawCount:N0}회", true, drawCount == 30);
        }

        private void DisplayResults(IReadOnlyList<ResultSummary> display, string title, bool animate,
            bool mass = false, Action onComplete = null)
        {
            ClearResults();
            var grid = resultItemsRoot.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.constraintCount = Mathf.Clamp(display.Count, 1, Mathf.Max(1, resultColumns));
                grid.cellSize = display.Count > resultColumns ? compactResultCardSize : resultCardSize;
                grid.spacing = resultCardSpacing;
                grid.childAlignment = display.Count > 10 ? TextAnchor.UpperCenter : TextAnchor.MiddleCenter;
                if (resultScroll != null)
                {
                    var rows = Mathf.CeilToInt(display.Count / (float)grid.constraintCount);
                    var height = Mathf.Max(resultScroll.viewport.rect.height, rows * grid.cellSize.y + Mathf.Max(0, rows - 1) * grid.spacing.y);
                    resultItemsRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                    resultScroll.StopMovement();
                    resultItemsRoot.anchoredPosition = Vector2.zero;
                    resultScroll.vertical = display.Count > 10;
                    if (resultScroll.verticalScrollbar != null) resultScroll.verticalScrollbar.gameObject.SetActive(display.Count > 10);
                }
            }
            for (var index = 0; index < display.Count; index++)
            {
                var item = Instantiate(resultItemPrefab, resultItemsRoot);
                item.name = $"SkillResult_{index + 1:00}_{display[index].Definition.SkillId}";
                item.Bind(display[index].Definition, display[index].Count, display[index].IsNew);
                item.ShowConvertedUpgradeStones(display[index].ConvertedUpgradeStones);
                if (display.Count == 1)
                {
                    ((RectTransform)item.transform).sizeDelta = resultCardSize;
                    item.transform.localScale = Vector3.one * singleResultScale;
                }
                spawnedResults.Add(item);
            }
            if (resultTitleText != null) resultTitleText.text = title;
            LayoutRebuilder.ForceRebuildLayoutImmediate(resultItemsRoot);
            Action completed = () =>
            {
                var hint = resultOverlay.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "CloseHint");
                if (hint != null) hint.text = display.Count > 10 ? "위아래로 밀어 전체 결과를 확인하세요" : "결과를 확인한 뒤 닫아 주세요";
                onComplete?.Invoke();
            };
            if (animate) inscriptionSequence?.Play(spawnedResults, resultTitleText, resultCloseButton, mass, completed);
            else completed();
        }

        private void HideResults()
        {
            if (inscriptionSequence != null && inscriptionSequence.IsPlaying && isActiveAndEnabled && !isSummoning)
            { inscriptionSequence.SkipCurrent(); return; }
            inscriptionSequence?.Finish();
            UIPanelPopAnimator.RequestClose(resultOverlay, ClearResults);
        }

        private void ClearResults()
        {
            inscriptionSequence?.Finish();
            for (var index = spawnedResults.Count - 1; index >= 0; index--)
            {
                if (spawnedResults[index] != null)
                {
                    spawnedResults[index].gameObject.SetActive(false);
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
                if (currentRateSummaryText.TryGetComponent<SummonProbabilityStripView>(out var strip))
                {
                    var entries = new List<SummonProbabilityStripView.Entry>();
                    var total = summonConfig?.GetTotalWeight(level) ?? 0;
                    if (total > 0 && catalog != null)
                    {
                        var weights = new int[5];
                        foreach (var entry in summonConfig.GetPool(level))
                            if (entry != null && entry.Weight > 0 && catalog.TryGet(entry.SkillId, out var definition))
                                weights[(int)definition.Rarity] += entry.Weight;
                        var labels = new[] { "일반", "희귀", "영웅", "전설", "신화" };
                        for (var index = 0; index < weights.Length; index++)
                            entries.Add(new SummonProbabilityStripView.Entry(labels[index], weights[index] * 100f / total,
                                CommanderSkillSummonResultItemView.ResolveAccent((CommanderSkillRarity)index)));
                    }
                    strip.Show(entries);
                    currentRateSummaryText.enabled = entries.Count == 0;
                }
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
                        ? $"{drawCount:N0}회 소환\n<size=65%><color=#E1D5CE>{BuildPaymentText(payment)}</color></size>"
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
