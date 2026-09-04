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
        internal bool TryApply(
            GameProgressChange change,
            CommanderGrowthConfig commanderGrowthConfig = null,
            ItemCatalog itemCatalog = null,
            EquipmentBalanceConfig equipmentBalanceConfig = null,
            CommanderSkillBalanceConfig commanderSkillBalanceConfig = null,
            CommanderSkillSummonConfig commanderSkillSummonConfig = null)
        {
            if (change == null)
            {
                return false;
            }

            var questRejected = false;
            RejectInvalidQuestClaim(change, ref questRejected);
            RejectInvalidQuestDailyReset(change, ref questRejected);
            RejectInvalidQuestWeeklyReset(change, ref questRejected);
            ApplyQuestProgress(change, ref questRejected);
            if (questRejected)
            {
                return false;
            }

            if (change.HasStandaloneItemGrant &&
                (change.Rewards == null || change.Rewards.IsEmpty))
            {
                return false;
            }

            if (change.HasExpeditionMode && !IsValidExpeditionMode(change.ExpeditionMode))
            {
                return false;
            }

            if (change.MarkCastleRaidCleared &&
                (!CastleRaidStageRules.IsValidStage(change.CastleRaidClearedStage) ||
                 change.CastleRaidClearedStage > castleRaidHighestClearedStage + 1 ||
                 (change.CastleRaidClearedStage <= castleRaidHighestClearedStage &&
                  change.Rewards != null && !change.Rewards.IsEmpty)))
            {
                return false; // 건너뛴 단계 진행과 최초 보상 중복 지급 차단
            }

            if (change.HasGrowthDungeonDailyKeyRefresh &&
                !TryApplyGrowthDungeonDailyKeyRefresh(change, itemCatalog))
            {
                return false;
            }

            if (change.HasAttendanceRefresh && !TryApplyAttendanceRefresh(change))
            {
                return false;
            }

            if (change.HasAttendanceClaim &&
                !TryApplyAttendanceClaim(change, commanderGrowthConfig, itemCatalog))
            {
                return false;
            }

            if (change.HasAddMail && !TryApplyMailAdd(change))
            {
                return false;
            }

            if (change.HasCleanupExpiredMail && !TryApplyMailCleanup(change))
            {
                return false;
            }

            if (change.HasClaimMail && !TryApplyMailClaim(change, commanderGrowthConfig, itemCatalog))
            {
                return false;
            }

            if ((change.HasMarkOfflineInactive || change.HasSettleOfflineReward || change.HasAcknowledgeOfflineRewards) &&
                !TryApplyOfflineRewardProgress(change))
                {
                    return false;
                }

            if (change.HasSetCommanderSkillAutoUse)
            {
                commanderSkills ??= CommanderSkillProgressData.CreateDefault();
                if (!commanderSkills.TrySetAutoUse(
                        change.ExpectedCommanderSkillAutoUse,
                        change.NewCommanderSkillAutoUse))
                {
                    return false;
                }
            }

            if (change.HasEquipCommanderSkill)
            {
                commanderSkills ??= CommanderSkillProgressData.CreateDefault();
                if (!commanderSkills.TryEquip(
                        change.CommanderSkillSlotIndex,
                        change.ExpectedCommanderSkillId,
                        change.NewCommanderSkillId,
                        commanderSkillBalanceConfig))
                {
                    return false;
                }
            }

            if (change.HasRecordCommanderSkillSummon)
            {
                commanderSkills ??= CommanderSkillProgressData.CreateDefault();
                var summonConfig = commanderSkillSummonConfig ?? CommanderSkillSummonConfig.RuntimeDefault;
                var summonedSkillIds = change.SummonedCommanderSkillIds;
                if (summonedSkillIds == null || summonedSkillIds.Count == 0)
                {
                    return false;
                }

                if (change.CommanderSkillSummonRequiresPayment)
                {
                    if (!summonConfig.TryGetOffer(
                            change.CommanderSkillSummonDrawCount,
                            out var offer) ||
                        offer.DrawCount != summonedSkillIds.Count)
                    {
                        return false;
                    }

                    var availableTickets = items.GetQuantity(summonConfig.TicketItemId);
                    var payment = summonConfig.CalculatePayment(offer, availableTickets);
                    var costs = new List<ItemAmount>(2);
                    if (payment.TicketCost > 0)
                    {
                        costs.Add(new ItemAmount(summonConfig.TicketItemId, payment.TicketCost));
                    }

                    if (payment.DiamondCost > 0L)
                    {
                        costs.Add(new ItemAmount(ItemIds.Diamond, payment.DiamondCost));
                    }

                    if (!ItemInventoryTransactions.TrySpend(
                            items,
                            costs,
                            itemCatalog,
                            out var spentCommanderSkillPayment))
                    {
                        return false;
                    }

                    items = spentCommanderSkillPayment; // 소환권·다이아·결과를 같은 후보에서만 반영
                }

                if (!commanderSkills.TryRecordSummons(
                        change.ExpectedCommanderSkillSummonCount,
                        summonedSkillIds,
                        commanderSkillBalanceConfig,
                        summonConfig))
                {
                    return false;
                }
            }

            if (change.HasLevelUpCommanderSkill)
            {
                commanderSkills ??= CommanderSkillProgressData.CreateDefault();
                var balance = commanderSkillBalanceConfig ?? CommanderSkillBalanceConfig.RuntimeDefault;
                if (!balance.TryGetRule(change.CommanderSkillToLevelUpId, out var rule) ||
                    !rule.TryGetNextLevelGoldCost(change.ExpectedCommanderSkillLevel, out var goldCost))
                {
                    return false;
                }

                var nextCommanderSkills = commanderSkills.Clone(balance, commanderSkillSummonConfig);
                if (!ItemInventoryTransactions.TrySpend(
                        items,
                        new[] { new ItemAmount(ItemIds.Gold, goldCost) },
                        itemCatalog,
                        out var spentSkillUpgradeItems) ||
                    !nextCommanderSkills.TryLevelUp(
                        change.CommanderSkillToLevelUpId,
                        change.ExpectedCommanderSkillLevel,
                        balance))
                {
                    return false;
                }

                items = spentSkillUpgradeItems;
                commanderSkills = nextCommanderSkills;
            }

            if (change.ItemCosts != null && change.ItemCosts.Count > 0)
            {
                if (!ItemInventoryTransactions.TrySpend(items, change.ItemCosts, itemCatalog, out var spentItems))
                {
                    return false;
                }

                items = spentItems; // 보상과 같은 후보 데이터에서 먼저 비용 차감
            }

            if (change.HasExpeditionMode)
            {
                expeditionMode = change.ExpeditionMode;
            }

            if (change.HasExpeditionFirstClear && !TryApplyExpeditionFirstClear(change))
            {
                return false;
            }

            if (change.HasExpeditionRepeatClear && !TryApplyExpeditionRepeatClear(change))
            {
                return false;
            }

            if (!TryApplyRewards(change.Rewards, commanderGrowthConfig, itemCatalog))
            {
                return false;
            }

            ApplyQuestClaim(change);
            ApplyQuestDailyReset(change);
            ApplyQuestWeeklyReset(change);
            ApplyQuestProgressReset(change);

            if (change.HasSettleOfflineReward &&
                !TryApplyOfflineEquipmentSettlement(change.OfflineReceipt,
                    (equipmentBalanceConfig ?? EquipmentBalanceConfig.RuntimeDefault).MaximumItemLevel))
            {
                return false;
            }

            if (change.FoodRiotBestKills >= 0)
            {
                foodRiotBestKills = Math.Max(foodRiotBestKills, change.FoodRiotBestKills);
            }

            if (change.GuardiansTowerBestKills >= 0) // 08.06 안건준 추가
            {
                guardiansTowerBestKills = Math.Max(guardiansTowerBestKills, change.GuardiansTowerBestKills);
            }

            if (change.HasGrowthDungeonClear)
            {
                growthDungeons ??= GrowthDungeonProgressData.CreateDefault();
                if (!growthDungeons.RecordClear(change.GrowthDungeonContentId, change.GrowthDungeonClearedStage))
                {
                    return false;
                }
            }

            if (change.IncrementGuardiansTowerDifficulty) // 08.07 안건준 추가 - 클리어할 때마다 난이도 1 증가
            {
                guardiansTowerDifficultyLevel = Math.Max(0, guardiansTowerDifficultyLevel + 1);
            }

            if (change.MarkCastleRaidCleared)
            {
                castleRaidFirstClear = true;
                castleRaidHighestClearedStage = Math.Max(
                    castleRaidHighestClearedStage,
                    change.CastleRaidClearedStage);
            }

            if (change.CommanderExperience > 0L)
            {
                commander ??= CommanderProgressData.CreateDefault();
                commander.GrantExperience(change.CommanderExperience, commanderGrowthConfig);
            }

            if (change.HasLevelUpCommander)
            {
                commander ??= CommanderProgressData.CreateDefault();
                if (!commander.TryLevelUp(change.ExpectedCommanderLevel, commanderGrowthConfig))
                {
                    return false;
                }

                commanderLegionGrowth ??= CommanderLegionGrowthData.CreateDefault();
                commanderLegionGrowth.GrantTrainingPoints(1); // 저장된 수동 레벨업 1회당 1포인트
            }

            if (change.HasUpgradeCommanderLegionStat)
            {
                var growthConfig = commanderGrowthConfig ?? CommanderGrowthConfig.RuntimeDefault;
                commanderLegionGrowth ??= CommanderLegionGrowthData.CreateDefault();
                var nextGrowth = commanderLegionGrowth.Clone();
                var currentLevel = nextGrowth.GetLevel(change.CommanderLegionStatToUpgrade);
                var maxLevel = growthConfig.GetLegionGrowthMaxLevel(change.CommanderLegionStatToUpgrade);
                if (currentLevel != change.ExpectedCommanderLegionStatLevel || currentLevel >= maxLevel)
                {
                    return false;
                }

                var nextItems = items;
                if (growthConfig.UsesGoldForLegionGrowth(change.CommanderLegionStatToUpgrade))
                {
                    var goldCost = growthConfig.GetLegionGrowthGoldCost(
                        change.CommanderLegionStatToUpgrade,
                        currentLevel);
                    if (!ItemInventoryTransactions.TrySpend(
                            items,
                            new[] { new ItemAmount(ItemIds.Gold, goldCost) },
                            itemCatalog,
                            out nextItems))
                    {
                        return false;
                    }
                }
                else if (!nextGrowth.TrySpendTrainingPoints(
                             growthConfig.GetLegionGrowthTrainingPointCost(
                                 change.CommanderLegionStatToUpgrade,
                                 currentLevel)))
                {
                    return false;
                }

                if (!nextGrowth.TryLevelUp(
                        change.CommanderLegionStatToUpgrade,
                        change.ExpectedCommanderLegionStatLevel,
                        maxLevel))
                {
                    return false;
                }

                items = nextItems;
                commanderLegionGrowth = nextGrowth;
            }

            if (change.HasAcquireMonster &&
                (string.IsNullOrEmpty(change.AcquireMonsterId) ||
                 !monsters.TryAcquire(change.AcquireMonsterId)))
            {
                return false;
            }

            if (change.HasFormationChange)
            {
                if (string.IsNullOrEmpty(change.FormationMonsterId) ||
                    (!change.RemoveFromFormation &&
                     change.TargetParty != MonsterPartyKind.Main &&
                     change.TargetParty != MonsterPartyKind.Reserve))
                {
                    return false;
                }

                var formationChanged = change.RemoveFromFormation
                    ? monsters.TryUnassign(change.FormationMonsterId)
                    : monsters.TryAssign(change.FormationMonsterId, change.TargetParty);
                if (!formationChanged)
                {
                    return false;
                }
            }

            if (change.HasMainBattleFormation &&
                (change.MainBattleFormationOffsets == null ||
                 !mainBattleFormation.TrySet(change.MainBattleFormationOffsets)))
            {
                return false;
            }

            if (change.HasLevelUpMonster)
            {
                if (string.IsNullOrEmpty(change.LevelUpMonsterId) ||
                    !monsters.TryGetOwned(change.LevelUpMonsterId, out var owned) ||
                    owned.Level != change.ExpectedMonsterLevel ||
                    !MonsterLevelRules.TryGetNextLevelCost(owned.Level, out var cost) ||
                    !ItemInventoryTransactions.TrySpend(
                        items,
                        new[] { new ItemAmount(ItemIds.Gold, cost) },
                        itemCatalog,
                        out var spentItems) ||
                    !monsters.TryLevelUp(change.LevelUpMonsterId, change.ExpectedMonsterLevel))
                {
                    return false;
                }

                items = spentItems; // 레벨 증가와 같은 후보 데이터에서 차감
            }

            if (change.HasAscendMonster &&
                (string.IsNullOrEmpty(change.AscendMonsterId) ||
                 !monsters.TryAscend(change.AscendMonsterId, change.ExpectedAscensionLevel)))
            {
                return false;
            }

            if (change.HasClaimMonsterCollectionFiveStarReward)
            {
                if (string.IsNullOrWhiteSpace(change.CollectionRewardMonsterId) ||
                    !monsters.TryClaimCollectionFiveStarReward(change.CollectionRewardMonsterId) ||
                    !ItemInventoryTransactions.TryGrantCoreBalance(
                        items,
                        ItemIds.Diamond,
                        MonsterCollectionRewardRules.FiveStarDiamondReward,
                        out var collectionRewardItems))
                {
                    return false;
                }

                items = collectionRewardItems; // 수령 플래그와 다이아를 같은 저장 후보에서 확정
            }

            if (change.GachaPulls != null)
            {
                if (change.GachaPulls.Count == 0)
                {
                    return false;
                }

                for (var index = 0; index < change.GachaPulls.Count; index++)
                {
                    var pull = change.GachaPulls[index];
                    if (!TryApplyGachaPull(pull.MonsterId, pull.Rarity))
                    {
                        return false;
                    }
                }
            }
            else if (change.HasGachaPull &&
                     !TryApplyGachaPull(change.GachaPullMonsterId, change.GachaPullRarity))
            {
                return false;
            }

            // 08.10 안건준 추가 - 장비 획득/장착/해제
            if (change.HasAcquireEquipment)
            {
                equipment ??= EquipmentSaveData.CreateDefault();
                if (!equipment.TryAcquire(change.AcquireEquipmentInstances,
                        (equipmentBalanceConfig ?? EquipmentBalanceConfig.RuntimeDefault).MaximumItemLevel))
                {
                    return false;
                }
            }

            if (change.HasEquipItem &&
                (string.IsNullOrEmpty(change.EquipItemInstanceId) ||
                 !(equipment ??= EquipmentSaveData.CreateDefault()).TryEquip(change.EquipItemInstanceId)))
            {
                return false;
            }

            if (change.HasEquipItems &&
                !(equipment ??= EquipmentSaveData.CreateDefault()).TryEquipBatch(change.EquipItemInstanceIds))
            {
                return false;
            }

            if (change.HasUnequipItem &&
                !(equipment ??= EquipmentSaveData.CreateDefault()).TryUnequip(change.UnequipItemPart))
            {
                return false;
            }

            if (change.HasSetEquipmentLock &&
                (string.IsNullOrEmpty(change.EquipmentLockInstanceId) ||
                 !(equipment ??= EquipmentSaveData.CreateDefault()).TrySetLocked(
                     change.EquipmentLockInstanceId,
                     change.ExpectedEquipmentLockValue,
                     change.EquipmentLockValue)))
            {
                return false;
            }

            if (change.HasDismantleEquipment)
            {
                equipment ??= EquipmentSaveData.CreateDefault();
                if (!equipment.TryDismantle(change.DismantleEquipmentInstanceIds, out var upgradeStoneAmount) ||
                    !TryApplyRewards(
                        RewardBundle.FromItems(new ItemAmount(ItemIds.EquipmentSlotUpgradeStone, upgradeStoneAmount)),
                        commanderGrowthConfig,
                        itemCatalog))
                {
                    return false;
                }
            }

            if (change.HasSetOfflineAutoDismantlePolicy &&
                !(equipment ??= EquipmentSaveData.CreateDefault()).TrySetOfflineAutoDismantlePolicy(
                    change.ExpectedOfflineAutoDismantlePolicy,
                    change.OfflineAutoDismantlePolicy))
            {
                return false;
            }

            if (change.HasDiscardItem)
            {
                if (!ItemInventoryTransactions.TryDiscard(
                        items,
                        itemCatalog,
                        change.ItemId,
                        change.ItemQuantity,
                        change.ExpectedItemQuantity,
                        out var discardedItems))
                {
                    return false;
                }

                items = discardedItems;
            }

            if (change.HasUseItem)
            {
                if (!ItemInventoryTransactions.TryUse(
                        items,
                        itemCatalog,
                        change.ItemId,
                        change.ItemQuantity,
                        change.ExpectedItemQuantity,
                        out var consumedItems,
                        out var useResult))
                {
                    return false;
                }

                items = consumedItems;
                if (!TryApplyRewards(useResult.Rewards, commanderGrowthConfig, itemCatalog))
                {
                    return false;
                }
            }

            // 장비 부위 슬롯 영구 강화(+1): 비용 확인 → 재화 차감 → 레벨 증가 순서로 처리한다.
            if (change.HasUpgradeEquipmentSlot)
            {
                equipmentSlotUpgrade ??= EquipmentSlotUpgradeData.CreateDefault();
                var currentLevel = equipmentSlotUpgrade.GetLevel(change.UpgradeEquipmentSlotPart);
                if (currentLevel != change.ExpectedEquipmentSlotLevel)
                {
                    return false; // 요청 시점과 처리 시점 사이에 레벨이 달라짐(중복 클릭 등)
                }

                var goldCost = EquipmentSlotUpgradeCostRules.GetNextGoldCost(currentLevel);
                var stoneCost = EquipmentSlotUpgradeCostRules.GetNextStoneCost(currentLevel);
                var costs = new[]
                {
                    new ItemAmount(ItemIds.Gold, goldCost),
                    new ItemAmount(ItemIds.EquipmentSlotUpgradeStone, stoneCost)
                };

                if (!ItemInventoryTransactions.TrySpend(items, costs, itemCatalog, out var slotUpgradeItems))
                {
                    return false; // 재화 부족
                }

                if (!equipmentSlotUpgrade.TryLevelUp(change.UpgradeEquipmentSlotPart, change.ExpectedEquipmentSlotLevel))
                {
                    return false;
                }

                items = slotUpgradeItems;
            }

            // 잠재능력 슬롯 최초 배정. 이미 값이 있으면 실패해 중복 배정을 막는다.
            if (change.HasAssignCommanderPotentialSlot)
            {
                commanderPotential ??= CommanderPotentialData.CreateDefault();
                if (!commanderPotential.TryAssignSlot(
                        change.CommanderPotentialSlotIndex,
                        change.CommanderPotentialOptionType,
                        change.CommanderPotentialGrade,
                        change.CommanderPotentialValue))
                {
                    return false;
                }
            }

            // "잠재 능력 변경": 강화석 1개 차감 후 잠기지 않은 대상 슬롯들을 새 옵션으로 교체.
            // 해금됐지만 아직 비어있는("대기 중") 슬롯도 이 버튼으로 함께 처음 배정된다(값 있는 슬롯은 교체, 빈 슬롯은 신규 배정).
            if (change.HasRerollCommanderPotentialSlots)
            {
                var entries = change.CommanderPotentialRerollEntries;
                if (entries == null || entries.Count == 0)
                {
                    return false;
                }

                commanderPotential ??= CommanderPotentialData.CreateDefault();

                // 유효 슬롯·잠금·중복 대상·같은 옵션 최대 2개를 차감 전에 한 번에 검증한다.
                if (!commanderPotential.CanApplyOptionReroll(entries, 2))
                {
                    return false;
                }

                if (!ItemInventoryTransactions.TrySpend(
                        items,
                        new[] { new ItemAmount(ItemIds.LegionPotentialUpgradeStone, 1) },
                        itemCatalog,
                        out var rerollItems))
                {
                    return false; // 잠재능력 강화석 부족
                }

                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var target = commanderPotential.GetSlot(entry.SlotIndex);
                    if (target != null && target.HasValue)
                    {
                        commanderPotential.TryReplaceSlot(entry.SlotIndex, entry.OptionType, entry.Grade, entry.Value);
                    }
                    else
                    {
                        commanderPotential.TryAssignSlot(entry.SlotIndex, entry.OptionType, entry.Grade, entry.Value);
                    }
                }

                // "잠재 능력 변경" 1회 성공마다 "수호자의 힘" 경험치 +10, 100 채우면 다음 단계로 승급.
                commanderPotential.AddExperience(CommanderPotentialData.ExperiencePerReroll);

                items = rerollItems;
            }

            // "옵션 스탯 변경": 강화석 1개 차감 후 옵션 종류·등급은 그대로 두고 수치만 교체.
            // 잠금은 옵션 자체가 바뀌는 "잠재 능력 변경"만 막는 용도라, 여기서는 잠긴 슬롯도 대상이 된다(값만 있으면 됨).
            if (change.HasRerollCommanderPotentialValues)
            {
                var valueEntries = change.CommanderPotentialValueRerollEntries;
                if (valueEntries == null || valueEntries.Count == 0)
                {
                    return false;
                }

                commanderPotential ??= CommanderPotentialData.CreateDefault();

                if (!commanderPotential.CanApplyValueReroll(
                        valueEntries,
                        equipmentBalanceConfig ?? EquipmentBalanceConfig.RuntimeDefault))
                {
                    return false;
                }

                if (!ItemInventoryTransactions.TrySpend(
                        items,
                        new[] { new ItemAmount(ItemIds.LegionPotentialUpgradeStone, 1) },
                        itemCatalog,
                        out var valueRerollItems))
                {
                    return false; // 잠재능력 강화석 부족
                }

                for (var i = 0; i < valueEntries.Count; i++)
                {
                    var entry = valueEntries[i];
                    if (!commanderPotential.TryRerollSlotValue(entry.SlotIndex, entry.Value))
                    {
                        return false;
                    }
                }

                // "옵션 스탯 변경" 1회 성공마다 "수호자의 힘" 경험치 +10.
                commanderPotential.AddExperience(CommanderPotentialData.ExperiencePerReroll);

                items = valueRerollItems;
            }

            // 자물쇠 아이콘 클릭으로 재추첨 대상 제외 여부를 토글. 잠그는 경우에만 강화석을
            // 소모하며(해제는 무료), 이미 잠긴 슬롯 수에 따라 비용이 1→2→4→8→16개로 2배씩 늘어난다.
            if (change.HasSetCommanderPotentialLocked)
            {
                commanderPotential ??= CommanderPotentialData.CreateDefault();

                var lockTarget = commanderPotential.GetSlot(change.CommanderPotentialLockSlotIndex);
                if (lockTarget == null || !lockTarget.HasValue ||
                    lockTarget.Locked != change.ExpectedCommanderPotentialLocked)
                {
                    return false;
                }

                if (change.NewCommanderPotentialLocked)
                {
                    var alreadyLockedCount = commanderPotential.CountLockedSlots();
                    var lockCost = CommanderPotentialData.GetLockStoneCost(alreadyLockedCount);
                    if (!ItemInventoryTransactions.TrySpend(
                            items,
                            new[] { new ItemAmount(ItemIds.LegionPotentialUpgradeStone, lockCost) },
                            itemCatalog,
                            out var lockItems))
                    {
                        return false; // 잠재능력 강화석 부족
                    }

                    items = lockItems;
                }

                commanderPotential.TrySetSlotLocked(
                    change.CommanderPotentialLockSlotIndex,
                    change.ExpectedCommanderPotentialLocked,
                    change.NewCommanderPotentialLocked);
            }

            Repair(
                commanderGrowthConfig,
                commanderSkillBalanceConfig,
                commanderSkillSummonConfig); // 변경 후 불변식 재확인
            return true;
        }
    }
}
