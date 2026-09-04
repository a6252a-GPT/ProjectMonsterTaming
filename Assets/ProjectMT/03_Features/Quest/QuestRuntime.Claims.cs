using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProjectMT.Features.Commander;
using ProjectMT.Features.Equipment;
using ProjectMT.Shared.Commander;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Quest;
using ProjectMT.Shared.Reward;
using UnityEngine;

namespace ProjectMT.Features.Quest
{
    public static partial class QuestRuntime
    {
        // 완료된 퀘스트의 보상을 수령한다. 우편함이 아직 없어서 지급과 함께 전체 정보를 콘솔에 출력하고,
        // 동시에 RewardClaimed 이벤트로 questId·bundle 값을 내보낸다(우편함 도입 시 그대로 재사용).
        public static async Task<bool> TryClaimRewardAsync(QuestId questId)
        {
            if (!IsReady)
            {
                Debug.LogWarning($"[Quest] 보상 수령 실패: QuestRuntime이 아직 준비되지 않음 (questId={questId.Value})");
                return false;
            }

            if (!catalog.TryGet(questId, out var definition))
            {
                Debug.LogWarning($"[Quest] 보상 수령 실패: 카탈로그에 없는 퀘스트 ID (questId={questId.Value})");
                return false;
            }

            if (definition.IsRepeatingTemplate)
            {
                await FlushPendingProgressAsync();
                return await TryClaimRepeatingRewardAsync(questId, definition);
            }

            await FlushPendingProgressAsync();

            // 원정대 클리어 퀘스트는 화면에 LastClearedStage 기준 진행도를 보여준다(위 GetTrackedProgress 참고).
            // 이벤트 연결 전에 이미 그 단계를 깬 세이브처럼 저장된 카운터가 아직 못 따라간 경우,
            // 저장 값을 먼저 맞춰야 보상 수령 검증(RejectInvalidQuestClaim)을 통과할 수 있다.
            await SyncExpeditionProgressAsync(definition);
            // 보유 종 수 도달 등 임계값형 조건도 마찬가지로, 퀘스트가 생기기 전부터 이미 조건을 채워 둔
            // 경우를 대비해 저장 값을 현재 실제 값으로 한 번 맞춰 둔다.
            await SyncThresholdProgressAsync(definition.QuestId, definition.ConditionType, definition.TargetValue);

            if (!CanClaimReward(questId))
            {
                var view = GetProgress(questId);
                Debug.LogWarning(
                    $"[Quest] 보상 수령 실패: 완료되지 않았거나 이미 수령함 (questId={questId.Value}, " +
                    $"진행도={view.CurrentProgress}/{ResolveTargetValue(definition)}, 완료={view.Completed}, 수령={view.RewardClaimed})");
                return false;
            }

            if (!definition.TryCreateRewardBundle(out var bundle))
            {
                Debug.LogWarning(
                    $"[Quest] 보상 수령 실패: 보상 정의가 비어있거나 잘못됨 (questId={questId.Value}, " +
                    $"reward={(definition.Reward != null ? definition.Reward.name : "null")})");
                return false;
            }

            var claimBundle = ResolveQuestClaimReward(bundle, out var rewardCapped);
            var applied = await progress.TryApplyAndSaveAsync(
                GameProgressChange.ClaimQuestReward(questId, claimBundle));
            if (applied)
            {
                Debug.Log($"[Quest] 보상 수령: {definition.DisplayName}");
                LogCappedQuestReward(definition, rewardCapped);
                LogQuestSnapshot(definition, GetProgress(questId));
                RewardClaimed?.Invoke(questId, claimBundle);
            }
            else
            {
                Debug.LogWarning(
                    $"[Quest] 보상 수령 실패: 저장 적용이 거절됨(questId={questId.Value}). " +
                    "같은 프레임에 다른 진행도 갱신과 겹쳤을 수 있으니 다시 시도해 보세요.");
            }

            return applied;
        }

        // 퀘스트는 한 번만 받을 수 있으므로 인벤토리가 꽉 찼다는 이유로 완료 상태에 영구 정체시키지 않는다.
        // 유효한 아이템 보상은 현재 남은 보유 한도까지만 지급하고 골드·경험치·수령 상태는 함께 저장한다.
        // 카탈로그가 없거나 정의가 잘못된 경우에는 원본 보상을 유지해 GameProgressData의 기존 검증이 실패하게 한다.
        private static RewardBundle ResolveQuestClaimReward(RewardBundle bundle, out bool rewardCapped)
        {
            rewardCapped = false;
            bundle ??= RewardBundle.Empty;
            if (bundle.Items.Count == 0 || progress == null || ItemCatalogHub.Current == null)
            {
                return bundle;
            }

            var itemCatalog = ItemCatalogHub.Current;
            var totals = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var definitions = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);
            var itemOrder = new List<string>();
            for (var i = 0; i < bundle.Items.Count; i++)
            {
                var reward = bundle.Items[i];
                if (!reward.IsValid || !itemCatalog.TryGet(reward.ItemId, out var definition))
                {
                    return bundle;
                }

                var itemId = definition.ItemId;
                totals.TryGetValue(itemId, out var total);
                if (total > long.MaxValue - reward.Amount)
                {
                    return bundle;
                }

                if (!totals.ContainsKey(itemId))
                {
                    itemOrder.Add(itemId);
                }

                totals[itemId] = total + reward.Amount;
                definitions[itemId] = definition;
            }

            var resolvedItems = new List<ItemAmount>(itemOrder.Count);
            var inventory = progress.View.Items;
            for (var i = 0; i < itemOrder.Count; i++)
            {
                var itemId = itemOrder[i];
                inventory.TryGetQuantity(itemId, out var owned);
                var maximum = definitions[itemId].MaxQuantity;
                var available = owned >= maximum ? 0L : maximum - Math.Max(0L, owned);
                var granted = Math.Min(totals[itemId], available);
                if (granted < totals[itemId])
                {
                    rewardCapped = true;
                }

                if (granted > 0L)
                {
                    resolvedItems.Add(new ItemAmount(itemId, granted));
                }
            }

            return new RewardBundle(bundle.Gold, bundle.CommanderExperience, resolvedItems);
        }

        private static void LogCappedQuestReward(QuestDefinition definition, bool rewardCapped)
        {
            if (rewardCapped)
            {
                Debug.LogWarning(
                    $"[Quest] 보유 한도에 도달한 아이템 보상은 남은 수량까지만 지급했습니다. " +
                    $"(questId={definition.QuestId.Value})");
            }
        }
    }
}
