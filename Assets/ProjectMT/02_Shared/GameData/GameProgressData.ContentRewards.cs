using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Commander;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.Gacha;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
using ProjectMT.Shared.Stats;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectMT.Shared.GameData
{
    public sealed partial class GameProgressData
    {
        private bool TryApplyGrowthDungeonDailyKeyRefresh(
            GameProgressChange change,
            ItemCatalog itemCatalog)
        {
            if (itemCatalog == null || change.GrowthDungeonDailyKeyTargets == null ||
                change.GrowthDungeonDailyKeyTargets.Count != GrowthDungeonDailyKeyRules.KeyItemIds.Count)
            {
                return false;
            }

            growthDungeons ??= GrowthDungeonProgressData.CreateDefault();
            var candidateItems = (items ?? ItemInventoryData.CreateDefault()).Clone();
            for (var index = 0; index < change.GrowthDungeonDailyKeyTargets.Count; index++)
            {
                var target = change.GrowthDungeonDailyKeyTargets[index];
                if (!target.IsValid || target.Amount > GrowthDungeonDailyKeyRules.MaximumQuantity ||
                    !string.Equals(
                        target.ItemId,
                        GrowthDungeonDailyKeyRules.KeyItemIds[index],
                        StringComparison.OrdinalIgnoreCase) ||
                    !candidateItems.TrySetQuantity(target.ItemId, target.Amount, itemCatalog))
                {
                    return false;
                }
            }

            if (!growthDungeons.TryAdvanceDailyKeyPeriod(
                    change.ExpectedGrowthDungeonDailyKeyPeriod,
                    change.GrowthDungeonDailyKeyPeriod))
            {
                return false;
            }

            items = candidateItems;
            itemViewCache = null;
            return true;
        }

        private bool TryApplyAttendanceRefresh(GameProgressChange change)
        {
            attendance ??= AttendanceProgressData.CreateDefault();
            var candidate = attendance.Clone();
            if (!candidate.TryRefresh(change.ExpectedAttendancePeriod, change.AttendancePeriod))
            {
                return false;
            }

            attendance = candidate;
            return true;
        }

        private bool TryApplyAttendanceClaim(
            GameProgressChange change,
            CommanderGrowthConfig commanderGrowthConfig,
            ItemCatalog itemCatalog)
        {
            if (change.AttendanceReward == null || change.AttendanceReward.IsEmpty)
            {
                return false;
            }

            attendance ??= AttendanceProgressData.CreateDefault();
            var candidate = attendance.Clone();
            if (!candidate.TryClaim(change.ExpectedAttendanceDay, change.ExpectedAttendanceClaimPeriod) ||
                !TryApplyRewards(change.AttendanceReward, commanderGrowthConfig, itemCatalog))
            {
                return false;
            }

            attendance = candidate;
            return true;
        }

        private bool TryApplyMailAdd(GameProgressChange change)
        {
            mail ??= MailProgressData.CreateDefault();
            var candidate = mail.Clone();
            if (!candidate.TryAdd(change.MailToAdd))
            {
                return false;
            }

            mail = candidate;
            return true;
        }

        private bool TryApplyMailCleanup(GameProgressChange change)
        {
            mail ??= MailProgressData.CreateDefault();
            var candidate = mail.Clone();
            if (candidate.RemoveExpired(change.MailOperationUtc) <= 0)
            {
                return false;
            }

            mail = candidate;
            return true;
        }

        private bool TryApplyMailClaim(
            GameProgressChange change,
            CommanderGrowthConfig commanderGrowthConfig,
            ItemCatalog itemCatalog)
        {
            mail ??= MailProgressData.CreateDefault();
            var candidate = mail.Clone();
            if (!candidate.TryCreateClaim(
                    change.MailClaimIds,
                    change.MailOperationUtc,
                    out var rewards,
                    out var normalizedIds) ||
                !TryApplyRewards(rewards, commanderGrowthConfig, itemCatalog) ||
                !candidate.TryRemoveClaimed(normalizedIds))
            {
                return false;
            }

            mail = candidate;
            return true;
        }

        private bool TryApplyRewards(
            RewardBundle rewards,
            CommanderGrowthConfig commanderGrowthConfig,
            ItemCatalog itemCatalog)
        {
            if (rewards == null || rewards.IsEmpty)
            {
                return true;
            }

            var candidateItems = items ?? ItemInventoryData.CreateDefault();
            if (rewards.Gold > 0L)
            {
                if (!ItemInventoryTransactions.TryGrantCoreBalance(
                        candidateItems,
                        ItemIds.Gold,
                        rewards.Gold,
                        out var withGold))
                {
                    return false;
                }

                candidateItems = withGold;
            }

            if (rewards.Items.Count > 0)
            {
                if (!ItemInventoryTransactions.TryGrant(
                        candidateItems,
                        rewards.Items,
                        itemCatalog,
                        out var withItems))
                {
                    return false;
                }

                candidateItems = withItems;
            }

            commander ??= CommanderProgressData.CreateDefault();
            commander.GrantExperience(rewards.CommanderExperience, commanderGrowthConfig);
            items = candidateItems;

            return true;
        }

        private bool TryApplyOfflineRewardProgress(GameProgressChange change)
        {
            var operationCount = (change.HasMarkOfflineInactive ? 1 : 0) +
                                 (change.HasSettleOfflineReward ? 1 : 0) +
                                 (change.HasAcknowledgeOfflineRewards ? 1 : 0);
            if (operationCount != 1)
            {
                return false;
            }

            offlineRewards ??= OfflineRewardProgressData.CreateDefault();
            if (change.HasMarkOfflineInactive)
            {
                return (change.Rewards == null || change.Rewards.IsEmpty) &&
                       offlineRewards.TryMarkInactive(
                           change.ExpectedOfflineLastActiveUtc,
                           change.OfflineNextActiveUtc,
                           change.OfflineNextActiveStage);
            }

            if (change.HasAcknowledgeOfflineRewards)
            {
                return (change.Rewards == null || change.Rewards.IsEmpty) &&
                       offlineRewards.TryAcknowledge(change.OfflineReceiptIds);
            }

            return MatchesOfflineReceipt(change.OfflineReceipt, change.Rewards) &&
                   offlineRewards.TrySettle(
                       change.ExpectedOfflineLastActiveUtc,
                       change.OfflineNextActiveUtc,
                       change.OfflineNextActiveStage,
                       change.OfflineReceipt);
        }

        private static bool MatchesOfflineReceipt(
            OfflineRewardReceiptData receipt,
            RewardBundle rewards)
        {
            if (receipt == null || !receipt.IsValid || rewards == null || rewards.IsEmpty ||
                receipt.Gold != rewards.Gold ||
                receipt.CommanderExperience != rewards.CommanderExperience)
            {
                return false;
            }

            var expected = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            AddExpectedItem(expected, ItemIds.EquipmentSlotUpgradeStone, receipt.TotalEquipmentSlotUpgradeStone);
            AddExpectedItem(expected, ItemIds.CommanderSkillUpgradeStone, receipt.CommanderSkillUpgradeStone);
            AddExpectedItem(expected, ItemIds.LegionPotentialUpgradeStone, receipt.LegionPotentialUpgradeStone);
            if (rewards.Items.Count != expected.Count)
            {
                return false;
            }

            var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < rewards.Items.Count; index++)
            {
                var item = rewards.Items[index];
                if (!item.IsValid || !matched.Add(item.ItemId) ||
                    !expected.TryGetValue(item.ItemId, out var amount) || amount != item.Amount)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryApplyOfflineEquipmentSettlement(OfflineRewardReceiptData receipt, int maximumItemLevel)
        {
            if (receipt == null || !receipt.IsValid)
            {
                return false;
            }

            equipment ??= EquipmentSaveData.CreateDefault();
            if (receipt.ExistingAutoDismantleInstanceIds.Count > 0)
            {
                if (!equipment.TryDismantle(
                        receipt.ExistingAutoDismantleInstanceIds,
                        out var existingDismantleStones) ||
                    existingDismantleStones != receipt.ExistingAutoDismantleUpgradeStone)
                {
                    return false;
                }
            }
            else if (receipt.ExistingAutoDismantleUpgradeStone != 0L)
            {
                return false;
            }

            return receipt.EquipmentRewards.Count == 0 ||
                   equipment.TryAcquire(receipt.EquipmentRewards, maximumItemLevel);
        }

        private static void AddExpectedItem(
            IDictionary<string, long> expected,
            string itemId,
            long amount)
        {
            if (amount > 0L)
            {
                expected.Add(itemId, amount);
            }
        }

        private bool TryApplyExpeditionFirstClear(GameProgressChange change)
        {
            if (expeditionMode != ExpeditionRunMode.Challenge ||
                change.ExpeditionFirstClearStage != ActiveChallengeStage ||
                change.Rewards == null || change.Rewards.IsEmpty)
            {
                return false;
            }

            if (expeditionDifficulty == ExpeditionDifficulty.Normal)
            {
                lastClearedStage = Math.Max(lastClearedStage, change.ExpeditionFirstClearStage);
                if (lastClearedStage >= ExpeditionMaximumStage)
                {
                    lastClearedStage = ExpeditionMaximumStage;
                    currentChallengeStage = ExpeditionMaximumStage;
                    expeditionDifficulty = ExpeditionDifficulty.Hard; // 일반 100 직후 하드 1 자동 시작
                    hardCurrentChallengeStage = Math.Max(1, hardCurrentChallengeStage);
                }
                else
                {
                    currentChallengeStage = Math.Max(currentChallengeStage, change.ExpeditionFirstClearStage + 1);
                }
            }
            else
            {
                hardLastClearedStage = Math.Max(hardLastClearedStage, change.ExpeditionFirstClearStage);
                hardCurrentChallengeStage = hardLastClearedStage >= ExpeditionMaximumStage
                    ? ExpeditionMaximumStage
                    : Math.Max(hardCurrentChallengeStage, change.ExpeditionFirstClearStage + 1);
                if (hardLastClearedStage >= ExpeditionMaximumStage)
                {
                    hardLastClearedStage = ExpeditionMaximumStage;
                    expeditionMode = ExpeditionRunMode.Repeat; // 최종 단계 최초 보상 중복 차단
                }
            }

            return true;
        }

        private bool TryApplyExpeditionRepeatClear(GameProgressChange change)
        {
            return expeditionMode == ExpeditionRunMode.Repeat &&
                   ActiveLastClearedStage > 0 &&
                   change.ExpeditionRepeatClearStage == ActiveLastClearedStage &&
                   change.Rewards != null &&
                   !change.Rewards.IsEmpty;
        }

        // 뽑기 한 번의 결과를 반영한다: 천장 카운터 갱신 + (신규 획득 / 중복 재료 / 전용 재화) 중 하나.
        private bool TryApplyGachaPull(string monsterId, MonsterRarity rarity,
            ProjectMT.Shared.Gacha.MonsterGachaChannel channel = ProjectMT.Shared.Gacha.MonsterGachaChannel.Normal)
        {
            if (string.IsNullOrEmpty(monsterId))
            {
                return false;
            }

            if (ProjectMT.Shared.Gacha.MonsterSoulRules.GetOverflowReward(rarity) <= 0) return false;
            if (channel == ProjectMT.Shared.Gacha.MonsterGachaChannel.Soul)
            {
                soulMonsterPity ??= GachaPityData.CreateDefault();
                soulMonsterPity.RegisterPull(rarity);
            }
            else
            {
                gachaPity ??= GachaPityData.CreateDefault();
                gachaPity.RegisterPull(rarity);
            }

            if (!monsters.TryGetOwned(monsterId, out var existingOwned))
            {
                return monsters.TryAcquire(monsterId); // 최초 획득 = 0돌파
            }

            if (!MonsterAscension.IsMaxAscension(existingOwned.AscensionLevel) &&
                monsters.TryAddAscensionMaterial(monsterId))
            {
                return true; // 중복 획득은 수동 돌파에 쓸 재료로 보관
            }

            // 이미 최대 돌파이거나 최대 돌파까지 필요한 재료를 모두 보유한 뒤의 초과 중복이다.
            if (!ItemInventoryTransactions.TryGrantCoreBalance(
                    items,
                    ItemIds.AscensionStone,
                    ProjectMT.Shared.Gacha.MonsterSoulRules.GetOverflowReward(rarity),
                    out var ascensionItems))
            {
                return false;
            }

            items = ascensionItems;
            return true;
        }
    }
}
