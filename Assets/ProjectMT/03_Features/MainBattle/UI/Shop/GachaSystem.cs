using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ProjectMT.Features.Quest;
using ProjectMT.Shared.Gacha;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Quest;
using ProjectMT.Shared.UI;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    // 몬스터 뽑기 - MonsterShop 하위 OneButton(1회)/TwoButton(10회)을 눌러
    // MonsterCatalog에 등록된 몬스터를 등급 확률(GachaProbability)에 따라 뽑는다.
    // 신규 획득이면 보유 등록, 중복이면 수동 돌파 재료(초과분은 전용 재화)로 저장한다.
    // 결과 텍스트·콘솔 로그 모두 "이름 / 등급 : X / 수량 : N" 형식으로 몬스터별 정보를 모아 표시하며,
    // 한 줄이 너무 길어지지 않도록 몬스터 ResultLineGroupSize개마다 줄바꿈 + 빈 줄을 넣는다.
    // 콘솔 로그 첫 줄에는 보유 마리 수와 함께 최대 돌파 재화(전용 재화) 보유량도 같이 표시한다.
    [DisallowMultipleComponent]
    public sealed class GachaSystem : MonoBehaviour
    {
        // 한 줄에 몬스터 몇 개까지 적을지 (초과하면 줄바꿈 + 빈 줄 삽입)
        private const int ResultLineGroupSize = 3;
        private static readonly Vector2 SummonButtonSize = new Vector2(644f, 116f);
        private static readonly Vector2 OneDrawButtonPosition = new Vector2(-334f, -347f);
        private static readonly Vector2 TenDrawButtonPosition = new Vector2(334f, -347f);

        // 이번 뽑기 묶음에서 한 몬스터가 몇 번 나왔는지, 그중 신규 획득이 있었는지 누적한다.
        private sealed class PullSummary
        {
            public MonsterDefinition Definition;
            public string DisplayName;
            public MonsterRarity Rarity;
            public int Count;
            public bool IsNew;
        }

        private sealed class PlannedPull
        {
            public MonsterDefinition Definition;
            public MonsterRarity Rarity;
            public bool WasNew;
        }

        [Header("뽑기 설정 등급,확률 카탈로그")]
        [SerializeField] private MonsterRarityCatalog rarityCatalog; // 몬스터 ↔ 등급 매칭표
        [SerializeField] private GachaProbability probability; // 등급별 확률·천장 설정
        [SerializeField] private GachaCostConfig costConfig; // 소환권 우선·다이아 비용

        [Header("뽑기 버튼")]
        [SerializeField] private Button oneDrawButton; // OneButton - 1회 뽑기
        [SerializeField] private Button tenDrawButton; // TwoButton - 10회 뽑기
        [SerializeField] private TMP_Text oneDrawCostText;
        [SerializeField] private TMP_Text tenDrawCostText;

        [Header("결과 표시")]
        [SerializeField] private TMP_Text resultText;

        [Header("확률·천장 표시")]
        [SerializeField] private TMP_Text probabilityText;
        [SerializeField] private TMP_Text pityText;

        [Header("결과 카드 Overlay")]
        [SerializeField] private GameObject resultOverlay;
        [SerializeField] private RectTransform resultItemsRoot;
        [SerializeField] private GachaResultItemView resultItemPrefab;
        [SerializeField] private TMP_Text resultTitleText;
        [SerializeField] private Button resultCloseButton;

        private IGameProgressService progress; // MainBattleSceneRoot.Initialize()에서 주입
        private MonsterCatalog monsterCatalog;
        private bool isDrawing; // 뽑기 진행 중 중복 클릭 방지
        private readonly List<GachaResultItemView> spawnedResultItems = new List<GachaResultItemView>();

        private void Awake()
        {
            ApplySummonButtonLayout();
            oneDrawButton?.onClick.AddListener(HandleOneDrawClicked);
            tenDrawButton?.onClick.AddListener(HandleTenDrawClicked);
            resultCloseButton?.onClick.AddListener(HideResults);
        }

        private void OnEnable()
        {
            ApplySummonButtonLayout();
        }

        private void OnDestroy()
        {
            oneDrawButton?.onClick.RemoveListener(HandleOneDrawClicked);
            tenDrawButton?.onClick.RemoveListener(HandleTenDrawClicked);
            resultCloseButton?.onClick.RemoveListener(HideResults);
            UnsubscribeProgress();
            ClearResultItems();
        }

        private void ApplySummonButtonLayout()
        {
            ApplySummonButtonRect(oneDrawButton, OneDrawButtonPosition);
            ApplySummonButtonRect(tenDrawButton, TenDrawButtonPosition);

            var content = oneDrawButton != null ? oneDrawButton.transform.parent : null;
            var legacyGrid = content != null ? content.GetComponent<GridLayoutGroup>() : null;
            if (legacyGrid != null)
            {
                legacyGrid.enabled = false;
            }
        }

        private static void ApplySummonButtonRect(Button button, Vector2 anchoredPosition)
        {
            if (button == null || button.transform is not RectTransform rect)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = SummonButtonSize;
        }

        // MainBattleSceneRoot가 씬 진입 시 호출. 저장 서비스·카탈로그 참조를 받아서 뽑기를 활성화한다.
        public void Configure(IGameProgressService progressService, MonsterCatalog catalog)
        {
            UnsubscribeProgress();
            progress = progressService;
            monsterCatalog = catalog;
            if (progress != null)
            {
                progress.Changed += RefreshGachaInfo;
            }

            SetResult(string.Empty);
            HideResults();
            RefreshGachaInfo();
        }

        // MainBattleSceneRoot.Shutdown()에서 호출. 씬 종료 후 잘못된 참조로 접근하지 않도록 정리.
        public void Shutdown()
        {
            UnsubscribeProgress();
            monsterCatalog = null;
            HideResults();
        }

        private async void HandleOneDrawClicked()
        {
            await DrawAsync(1);
        }

        private async void HandleTenDrawClicked()
        {
            await DrawAsync(10);
        }

        private async Task DrawAsync(int drawCount)
        {
            if (isDrawing)
            {
                return;
            }

            if (!CanDraw())
            {
                SetResult("현재 뽑을 수 없습니다");
                return;
            }

            if (!TryGetPaymentPlan(drawCount, out var payment) || !payment.CanAfford)
            {
                SetResult(BuildPaymentFailureText(payment));
                RefreshGachaInfo();
                return;
            }

            isDrawing = true;
            try
            {
                SetButtonsInteractable(false);
                HideResults();

                // 계획 수량이 요청 수량과 다르면(카탈로그 미등록 등) 저장을 시도하지 않고 중단한다.
                if (!TryPlanPulls(drawCount, out var plannedPulls) || plannedPulls.Count != drawCount)
                {
                    SetResult("등급별 몬스터 등록 정보를 확인해 주세요");
                    return;
                }

                var records = new List<GachaPullRecord>(plannedPulls.Count);
                for (var index = 0; index < plannedPulls.Count; index++)
                {
                    records.Add(new GachaPullRecord(
                        plannedPulls[index].Definition.MonsterId,
                        plannedPulls[index].Rarity));
                }

                bool saved;
                try
                {
                    saved = await progress.TryApplyAndSaveAsync(
                        GameProgressChange.RecordGachaPulls(records, payment.CreateItemCosts()));
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    saved = false;
                }

                if (!saved)
                {
                    SetResult("소환 저장에 실패했습니다 · 비용과 결과는 반영되지 않았습니다");
                    return;
                }

                // 실제 뽑기가 저장까지 성공한 경우에만 "몬스터 뽑기" 종류 퀘스트 진행도를 올린다(뽑은 마리 수만큼).
                _ = QuestRuntime.AdvanceAllOfConditionAsync(QuestConditionType.MonsterSummon, plannedPulls.Count);

                // 이번에 신규로 보유하게 된 몬스터 수만큼 "몬스터 보유" 종류 퀘스트 진행도도 함께 올린다.
                var newlyOwnedCount = 0;
                for (var index = 0; index < plannedPulls.Count; index++)
                {
                    if (plannedPulls[index].WasNew)
                    {
                        newlyOwnedCount++;
                    }
                }

                if (newlyOwnedCount > 0)
                {
                    _ = QuestRuntime.AdvanceAllOfConditionAsync(QuestConditionType.MonsterOwnedCount, newlyOwnedCount);
                }

                BuildPullSummaries(plannedPulls, out var order, out var summaries);
                var detailText = BuildResultText(order, summaries);
                var paymentText = BuildPaymentSummary(payment);
                SetResult(resultOverlay != null
                    ? $"{BuildResultHeadline(drawCount, order, summaries)} · {paymentText}"
                    : $"{detailText}\n\n사용: {paymentText}");
                ShowResults(drawCount, order, summaries);
                LogOwnedRosterDebug(); // 보유 몬스터 이름·등급·돌파·재료를 콘솔에 출력
            }
            catch (Exception exception)
            {
                // 기획하지 못한 예외가 나도 뽑기 버튼이 영구히 잠기지 않도록 방어.
                Debug.LogException(exception);
                SetResult("소환 처리 중 예상치 못한 오류가 발생했습니다");
            }
            finally
            {
                isDrawing = false;
                RefreshGachaInfo();
            }
        }

        private bool TryPlanPulls(int drawCount, out List<PlannedPull> plannedPulls)
        {
            plannedPulls = new List<PlannedPull>(drawCount);
            var pity = BuildPityState();
            var ownedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var owned = progress.View.Monsters.OwnedMonsters;
            for (var index = 0; index < owned.Count; index++)
            {
                ownedIds.Add(owned[index].MonsterId);
            }

            for (var index = 0; index < drawCount; index++)
            {
                var rarity = probability.Roll(pity);
                var definition = PickMonsterOfRarity(rarity);
                if (definition == null)
                {
                    plannedPulls.Clear();
                    return false;
                }

                plannedPulls.Add(new PlannedPull
                {
                    Definition = definition,
                    Rarity = rarity,
                    WasNew = ownedIds.Add(definition.MonsterId)
                });
                pity = pity.Advance(rarity); // 같은 10회 묶음 안에서도 천장을 순서대로 갱신
            }

            return true;
        }

        private static void BuildPullSummaries(
            List<PlannedPull> plannedPulls,
            out List<string> order,
            out Dictionary<string, PullSummary> summaries)
        {
            order = new List<string>(plannedPulls.Count);
            summaries = new Dictionary<string, PullSummary>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < plannedPulls.Count; index++)
            {
                var pull = plannedPulls[index];
                var monsterId = pull.Definition.MonsterId;
                if (!summaries.TryGetValue(monsterId, out var summary))
                {
                    summary = new PullSummary
                    {
                        Definition = pull.Definition,
                        DisplayName = pull.Definition.DisplayName,
                        Rarity = pull.Rarity
                    };
                    summaries.Add(monsterId, summary);
                    order.Add(monsterId);
                }

                summary.Count++;
                summary.IsNew |= pull.WasNew;
            }
        }

        private void ShowResults(
            int drawCount,
            List<string> order,
            Dictionary<string, PullSummary> summaries)
        {
            if (resultOverlay == null || resultItemsRoot == null || resultItemPrefab == null || progress == null)
            {
                return; // 구형 Prefab에서는 결과 문자열 표시를 그대로 사용
            }

            ClearResultItems();
            var roster = progress.View.Monsters;
            var presentationOrder = new List<string>(order);
            presentationOrder.Sort((left, right) =>
            {
                var rarityOrder = summaries[right].Rarity.CompareTo(summaries[left].Rarity);
                return rarityOrder != 0 ? rarityOrder : order.IndexOf(left).CompareTo(order.IndexOf(right));
            });

            for (var index = 0; index < presentationOrder.Count; index++)
            {
                var monsterId = presentationOrder[index];
                var summary = summaries[monsterId];
                if (summary.Definition == null || !roster.TryGetOwnedMonster(monsterId, out var owned))
                {
                    continue;
                }

                var item = Instantiate(resultItemPrefab, resultItemsRoot);
                item.name = $"Result_{index + 1:00}_{monsterId}";
                item.Bind(summary.Definition, owned, summary.Rarity, summary.Count, summary.IsNew);
                spawnedResultItems.Add(item);
            }

            if (resultTitleText != null)
            {
                var highestRarity = summaries[presentationOrder[0]].Rarity;
                var drawLabel = drawCount == 1 ? "1회 소환 결과" : "10회 소환 결과";
                resultTitleText.text = $"{drawLabel} · 최고 {RarityLabel(highestRarity)}";
            }

            UIPanelPopAnimator.RequestOpen(resultOverlay, UIPanelPopStyle.RewardPopup);
            LayoutRebuilder.ForceRebuildLayoutImmediate(resultItemsRoot);
        }

        private void HideResults()
        {
            UIPanelPopAnimator.RequestClose(resultOverlay, ClearResultItems);
        }

        private void ClearResultItems()
        {
            for (var index = spawnedResultItems.Count - 1; index >= 0; index--)
            {
                if (spawnedResultItems[index] != null)
                {
                    spawnedResultItems[index].gameObject.SetActive(false);
                    Destroy(spawnedResultItems[index].gameObject);
                }
            }

            spawnedResultItems.Clear();
        }

        // 예: "(New 쉘 / 등급 : 일반 / 수량 : 3) , (루미 / 등급 : 영웅 / 수량 : 1)"
        // 몬스터 3개마다 줄바꿈 + 빈 줄을 넣어서 한 줄이 너무 길어 잘리지 않도록 한다.
        private static string BuildResultText(List<string> order, Dictionary<string, PullSummary> summaries)
        {
            var items = new List<string>(order.Count);
            for (var index = 0; index < order.Count; index++)
            {
                var summary = summaries[order[index]];
                var itemBuilder = new StringBuilder();
                if (summary.IsNew)
                {
                    itemBuilder.Append("New ");
                }

                itemBuilder.Append(summary.DisplayName);
                itemBuilder.Append(" / 등급 : ");
                itemBuilder.Append(RarityLabel(summary.Rarity));
                itemBuilder.Append(" / 수량 : ");
                itemBuilder.Append(summary.Count);
                items.Add(WrapWithParens(itemBuilder.ToString())); // 몬스터별 구분을 위해 앞뒤에 ( ) 를 붙인다
            }

            var builder = new StringBuilder();
            AppendGrouped(builder, items);
            return builder.ToString();
        }

        // 예: "보유 몬스터 (총 8마리) / 전용 재화 : 2개
        //      (쉘 : 일반 · 1돌파) , (루미 : 영웅) , (아르 : 일반)"
        // (몬스터 3개마다 줄바꿈 + 빈 줄)
        private void LogOwnedRosterDebug()
        {
            var roster = progress.View.Monsters;
            var owned = roster.OwnedMonsters;
            if (owned.Count == 0)
            {
                Debug.Log("보유 몬스터 : (없음)");
                return;
            }

            var items = new List<string>(owned.Count);
            for (var index = 0; index < owned.Count; index++)
            {
                var entry = owned[index];
                var displayName = monsterCatalog.TryGet(entry.MonsterId, out var definition)
                    ? definition.DisplayName
                    : entry.MonsterId;
                var rarityLabel = rarityCatalog.TryGetRarity(entry.MonsterId, out var rarity)
                    ? RarityLabel(rarity)
                    : "미지정";

                var itemBuilder = new StringBuilder();
                itemBuilder.Append(displayName);
                itemBuilder.Append(" : ");
                itemBuilder.Append(rarityLabel);
                if (entry.AscensionLevel > 0)
                {
                    itemBuilder.Append(" · ");
                    itemBuilder.Append(entry.AscensionLevel);
                    itemBuilder.Append("돌파");
                }

                if (entry.AscensionMaterialCount > 0)
                {
                    itemBuilder.Append(" · 돌파 재료 ");
                    itemBuilder.Append(entry.AscensionMaterialCount);
                }

                items.Add(WrapWithParens(itemBuilder.ToString())); // 몬스터별 구분을 위해 앞뒤에 ( ) 를 붙인다
            }

            // 콘솔 목록은 로그의 첫 줄바꿈 전까지만 미리보기로 보여주므로, 총 마리 수·보유 재화를 먼저 적어서
            // 클릭해서 펼쳐보지 않아도 실제로 몇 마리·재화를 보유 중인지 바로 알 수 있게 한다.
            // 재화 = 최대 돌파(5돌파) 이후 중복 획득 시 적립되는 전용 재화 (몬스터 선택권 / 뽑기권 교환용).
            var builder = new StringBuilder("보유 몬스터 (총 ");
            builder.Append(owned.Count);
            builder.Append("마리) / 전용 재화(뽑기권 등 사용?) : ");
            builder.Append($"{progress.View.AscensionCurrency}개");
            builder.Append('\n');
            AppendGrouped(builder, items);
            Debug.Log(builder.ToString());
        }

        // 몬스터별 데이터를 서로 구분하기 쉽도록 앞뒤에 괄호를 붙인다. 예: "(루미 / 등급 : 영웅 / 수량 : 1)"
        private static string WrapWithParens(string content)
        {
            return "(" + content + ")";
        }

        // 항목을 ResultLineGroupSize개씩 묶어서 " , "로 잇고, 그 다음 묶음은 빈 줄(줄바꿈 2번)로 띄운다.
        private static void AppendGrouped(StringBuilder builder, List<string> items)
        {
            for (var index = 0; index < items.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(index % ResultLineGroupSize == 0 ? "\n\n" : " , ");
                }

                builder.Append(items[index]);
            }
        }

        private MonsterDefinition PickMonsterOfRarity(MonsterRarity rarity)
        {
            var candidates = new List<MonsterDefinition>();
            var entries = rarityCatalog.GetMonstersOfRarity(rarity);
            for (var index = 0; index < entries.Count; index++)
            {
                var definition = entries[index];
                // MonsterCatalog에 실제로 등록된 몬스터만 뽑는다 (등급 매칭표에만 있는 항목은 제외).
                if (definition != null && monsterCatalog.TryGet(definition.MonsterId, out _))
                {
                    candidates.Add(definition);
                }
            }

            return candidates.Count == 0 ? null : candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        private GachaPityState BuildPityState()
        {
            var pity = progress.View.GachaPity;
            return new GachaPityState(
                pity.PullsSinceRareOrBetter,
                pity.PullsSinceEpicOrBetter,
                pity.PullsSinceLegendaryOrBetter,
                pity.PullsSinceMythicOrBetter);
        }

        private bool CanDraw()
        {
            return progress != null && progress.IsLoaded && monsterCatalog != null &&
                   rarityCatalog != null && probability != null && costConfig != null &&
                   costConfig.TryValidate(out _);
        }

        private bool TryGetPaymentPlan(int drawCount, out GachaPaymentPlan payment)
        {
            payment = default;
            if (progress == null || !progress.IsLoaded || costConfig == null ||
                !costConfig.TryValidate(out _))
            {
                return false;
            }

            var items = progress.View.Items;
            items.TryGetQuantity(ItemIds.MonsterSummonTicket, out var tickets);
            items.TryGetQuantity(ItemIds.Diamond, out var diamonds);
            payment = costConfig.CreatePaymentPlan(drawCount, tickets, diamonds);
            return payment.IsValid;
        }

        private void RefreshGachaInfo()
        {
            if (probabilityText != null)
            {
                probabilityText.text = BuildProbabilityText();
                if (probabilityText.TryGetComponent<SummonProbabilityStripView>(out var strip))
                {
                    var entries = new List<SummonProbabilityStripView.Entry>();
                    if (probability != null && probability.RarityRates != null)
                    {
                        foreach (var rate in probability.RarityRates)
                        {
                            if (rate == null) continue;
                            ProjectMT.Features.Formation.MonsterCardView.GetRarityPalette(
                                rate.Rarity, out _, out _, out var color);
                            entries.Add(new SummonProbabilityStripView.Entry(
                                RarityLabel(rate.Rarity), rate.DropRatePercent, color));
                        }
                    }
                    strip.Show(entries);
                    probabilityText.enabled = entries.Count == 0;
                }
            }

            if (pityText != null)
            {
                pityText.text = BuildPityText();
            }

            if (TryGetPaymentPlan(GachaCostConfig.SingleDrawCount, out var onePayment))
            {
                SetPaymentText(oneDrawCostText, onePayment);
            }

            if (TryGetPaymentPlan(GachaCostConfig.TenDrawCount, out var tenPayment))
            {
                SetPaymentText(tenDrawCostText, tenPayment);
            }

            if (!isDrawing)
            {
                RefreshDrawAvailability();
            }
        }

        private void RefreshDrawAvailability()
        {
            if (oneDrawButton != null)
            {
                oneDrawButton.interactable = CanDraw() &&
                    TryGetPaymentPlan(GachaCostConfig.SingleDrawCount, out var payment) &&
                    payment.CanAfford;
            }

            if (tenDrawButton != null)
            {
                tenDrawButton.interactable = CanDraw() &&
                    TryGetPaymentPlan(GachaCostConfig.TenDrawCount, out var payment) &&
                    payment.CanAfford;
            }
        }

        private static void SetPaymentText(TMP_Text target, GachaPaymentPlan payment)
        {
            if (target == null)
            {
                return;
            }

            var prefix = payment.CanAfford ? "결제 예정" : "재화 부족";
            target.text = $"소환권 {payment.AvailableTickets:N0}장 보유 · {prefix}: " +
                          BuildPaymentSummary(payment);
        }

        private static string BuildPaymentSummary(GachaPaymentPlan payment)
        {
            if (payment.TicketsUsed > 0 && payment.DiamondCost > 0L)
            {
                return $"소환권 {payment.TicketsUsed:N0}장 + 다이아 {payment.DiamondCost:N0}";
            }

            if (payment.TicketsUsed > 0)
            {
                return $"소환권 {payment.TicketsUsed:N0}장";
            }

            return $"다이아 {payment.DiamondCost:N0}";
        }

        private static string BuildPaymentFailureText(GachaPaymentPlan payment)
        {
            if (!payment.IsValid)
            {
                return "소환 비용 정보를 불러올 수 없습니다";
            }

            return $"재화 부족 · 필요 {BuildPaymentSummary(payment)} · " +
                   $"보유 다이아 {payment.AvailableDiamonds:N0}";
        }

        private string BuildProbabilityText()
        {
            if (probability == null || probability.RarityRates == null)
            {
                return "확률 정보를 불러올 수 없습니다";
            }

            var builder = new StringBuilder();
            for (var index = 0; index < probability.RarityRates.Count; index++)
            {
                var rate = probability.RarityRates[index];
                if (rate == null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("   ");
                }

                builder.Append(RarityLabel(rate.Rarity));
                builder.Append(' ');
                builder.Append(rate.DropRatePercent.ToString("0.##"));
                builder.Append('%');
            }

            return builder.ToString();
        }

        private static string BuildResultHeadline(
            int requestedDrawCount,
            List<string> order,
            Dictionary<string, PullSummary> summaries)
        {
            var completedDraws = 0;
            var highestRarity = MonsterRarity.Common;
            for (var index = 0; index < order.Count; index++)
            {
                var summary = summaries[order[index]];
                completedDraws += summary.Count;
                if (summary.Rarity > highestRarity)
                {
                    highestRarity = summary.Rarity;
                }
            }

            var drawLabel = requestedDrawCount == completedDraws
                ? $"{completedDraws}회 소환 완료"
                : $"{completedDraws}/{requestedDrawCount}회 소환 완료";
            return $"{drawLabel} · 최고 {RarityLabel(highestRarity)} · {order.Count}종";
        }

        private string BuildPityText()
        {
            if (progress == null)
            {
                return "천장 정보를 불러오는 중입니다";
            }

            var pity = progress.View.GachaPity;
            return $"희귀 보정 {pity.PullsSinceRareOrBetter}/{GetRareGuarantee()}   " +
                   $"영웅 {pity.PullsSinceEpicOrBetter}/{GetCeiling(MonsterRarity.Epic)}\n" +
                   $"전설 {pity.PullsSinceLegendaryOrBetter}/{GetCeiling(MonsterRarity.Legendary)}   " +
                   $"신화 {pity.PullsSinceMythicOrBetter}/{GetCeiling(MonsterRarity.Mythic)}";
        }

        private int GetRareGuarantee()
        {
            var rate = FindRate(MonsterRarity.Rare);
            return rate == null || rate.RareGuaranteeInterval <= 0 ? 0 : rate.RareGuaranteeInterval;
        }

        private int GetCeiling(MonsterRarity rarity)
        {
            var rate = FindRate(rarity);
            return rate == null || rate.CeilingPulls <= 0 ? 0 : rate.CeilingPulls;
        }

        private GachaRarityRate FindRate(MonsterRarity rarity)
        {
            if (probability == null || probability.RarityRates == null)
            {
                return null;
            }

            for (var index = 0; index < probability.RarityRates.Count; index++)
            {
                var rate = probability.RarityRates[index];
                if (rate != null && rate.Rarity == rarity)
                {
                    return rate;
                }
            }

            return null;
        }

        private void UnsubscribeProgress()
        {
            if (progress != null)
            {
                progress.Changed -= RefreshGachaInfo;
            }

            progress = null;
        }

        private static string RarityLabel(MonsterRarity rarity)
        {
            switch (rarity)
            {
                case MonsterRarity.Common: return "일반";
                case MonsterRarity.Rare: return "희귀";
                case MonsterRarity.Epic: return "영웅";
                case MonsterRarity.Legendary: return "전설";
                case MonsterRarity.Mythic: return "신화";
                default: return rarity.ToString();
            }
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (interactable)
            {
                RefreshDrawAvailability();
                return;
            }

            if (oneDrawButton != null)
            {
                oneDrawButton.interactable = false;
            }
            if (tenDrawButton != null)
            {
                tenDrawButton.interactable = false;
            }
        }

        private void SetResult(string text)
        {
            if (resultText != null)
            {
                resultText.text = text;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            MonsterRarityCatalog rarity,
            GachaProbability gachaProbability,
            Button oneButton,
            Button tenButton,
            TMP_Text result = null)
        {
            rarityCatalog = rarity;
            probability = gachaProbability;
            oneDrawButton = oneButton;
            tenDrawButton = tenButton;
            resultText = result;
        }

        public void EditorConfigureCost(
            GachaCostConfig config,
            TMP_Text oneCost,
            TMP_Text tenCost)
        {
            costConfig = config;
            oneDrawCostText = oneCost;
            tenDrawCostText = tenCost;
        }

        public void EditorConfigurePresentation(
            TMP_Text rates,
            TMP_Text pity,
            GameObject overlay,
            RectTransform itemsRoot,
            GachaResultItemView itemPrefab,
            TMP_Text overlayTitle,
            Button overlayCloseButton)
        {
            probabilityText = rates;
            pityText = pity;
            resultOverlay = overlay;
            resultItemsRoot = itemsRoot;
            resultItemPrefab = itemPrefab;
            resultTitleText = overlayTitle;
            resultCloseButton = overlayCloseButton;
        }
#endif
    }
}
