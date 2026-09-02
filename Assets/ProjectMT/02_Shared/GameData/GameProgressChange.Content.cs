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
    public sealed partial class GameProgressChange
    {
        public static GameProgressChange SetExpeditionMode(ExpeditionRunMode mode)
        {
            return new GameProgressChange
            {
                HasExpeditionMode = true,
                ExpeditionMode = mode
            };
        }

        public static GameProgressChange MarkOfflineInactive(
            string expectedLastActiveUtc,
            DateTime inactiveUtc,
            int stage)
        {
            return new GameProgressChange
            {
                HasMarkOfflineInactive = true,
                ExpectedOfflineLastActiveUtc = expectedLastActiveUtc?.Trim() ?? string.Empty,
                OfflineNextActiveUtc = inactiveUtc.ToUniversalTime(),
                OfflineNextActiveStage = Math.Max(1, stage)
            };
        }

        public static GameProgressChange SettleOfflineReward(
            string expectedLastActiveUtc,
            DateTime settledToUtc,
            int nextStage,
            OfflineRewardReceiptData receipt,
            RewardBundle rewards)
        {
            return new GameProgressChange
            {
                HasSettleOfflineReward = true,
                ExpectedOfflineLastActiveUtc = expectedLastActiveUtc?.Trim() ?? string.Empty,
                OfflineNextActiveUtc = settledToUtc.ToUniversalTime(),
                OfflineNextActiveStage = Math.Max(1, nextStage),
                OfflineReceipt = receipt?.Clone(),
                Rewards = rewards ?? RewardBundle.Empty
            };
        }

        public static GameProgressChange AcknowledgeOfflineRewards(IReadOnlyList<string> receiptIds)
        {
            var ids = new List<string>(receiptIds?.Count ?? 0);
            if (receiptIds != null)
            {
                for (var index = 0; index < receiptIds.Count; index++)
                {
                    if (!string.IsNullOrWhiteSpace(receiptIds[index]))
                    {
                        ids.Add(receiptIds[index].Trim());
                    }
                }
            }

            return new GameProgressChange
            {
                HasAcknowledgeOfflineRewards = true,
                OfflineReceiptIds = ids
            };
        }

        public static GameProgressChange RecordExpeditionFirstClear(
            int stage,
            RewardBundle rewards) // 최초 진행과 보상을 함께 기록
        {
            return new GameProgressChange
            {
                HasExpeditionFirstClear = true,
                ExpeditionFirstClearStage = stage,
                Rewards = rewards
            };
        }

        public static GameProgressChange RecordExpeditionRepeatClear(
            int stage,
            RewardBundle rewards) // 반복 보상만 기록
        {
            return new GameProgressChange
            {
                HasExpeditionRepeatClear = true,
                ExpeditionRepeatClearStage = stage,
                Rewards = rewards
            };
        }

        public static GameProgressChange RecordFoodRiot(int killCount, RewardBundle rewards) // 식량 대소동 결과 요청
        {
            return new GameProgressChange
            {
                FoodRiotBestKills = Math.Max(0, killCount),
                Rewards = rewards
            };
        }

        public bool TryAttachGrowthDungeonSettlement(
            string contentId,
            int clearedStage,
            bool recordClear,
            string keyItemId = null)
        {
            if ((recordClear && (HasGrowthDungeonClear || string.IsNullOrWhiteSpace(contentId) || clearedStage <= 0)) ||
                (!string.IsNullOrWhiteSpace(keyItemId) && ItemCosts != null))
            {
                return false;
            }

            if (recordClear)
            {
                HasGrowthDungeonClear = true;
                GrowthDungeonContentId = contentId.Trim();
                GrowthDungeonClearedStage = clearedStage;
            }

            if (!string.IsNullOrWhiteSpace(keyItemId))
            {
                ItemCosts = new[] { new ItemAmount(keyItemId.Trim(), 1L) };
            }

            return recordClear || ItemCosts != null;
        }

        public static GameProgressChange RefreshGrowthDungeonDailyKeys(
            long expectedPeriod,
            long nextPeriod,
            params ItemAmount[] targetQuantities)
        {
            return new GameProgressChange
            {
                HasGrowthDungeonDailyKeyRefresh = true,
                ExpectedGrowthDungeonDailyKeyPeriod = expectedPeriod,
                GrowthDungeonDailyKeyPeriod = nextPeriod,
                GrowthDungeonDailyKeyTargets = targetQuantities == null
                    ? Array.Empty<ItemAmount>()
                    : (ItemAmount[])targetQuantities.Clone()
            };
        }

        public static GameProgressChange RefreshAttendance(long expectedPeriod, long nextPeriod)
        {
            return new GameProgressChange
            {
                HasAttendanceRefresh = true,
                ExpectedAttendancePeriod = expectedPeriod,
                AttendancePeriod = nextPeriod
            };
        }

        public static GameProgressChange ClaimAttendance(
            int expectedDay,
            long expectedPeriod,
            RewardBundle reward)
        {
            return new GameProgressChange
            {
                HasAttendanceClaim = true,
                ExpectedAttendanceDay = expectedDay,
                ExpectedAttendanceClaimPeriod = expectedPeriod,
                AttendanceReward = reward ?? RewardBundle.Empty
            };
        }

        public static GameProgressChange AddMail(MailEntryData mail)
        {
            return new GameProgressChange
            {
                HasAddMail = true,
                MailToAdd = mail?.Clone()
            };
        }

        public static GameProgressChange CleanupExpiredMail(DateTime utcNow)
        {
            return new GameProgressChange
            {
                HasCleanupExpiredMail = true,
                MailOperationUtc = NormalizeUtc(utcNow)
            };
        }

        public static GameProgressChange ClaimMail(DateTime utcNow, params string[] mailIds)
        {
            return new GameProgressChange
            {
                HasClaimMail = true,
                MailOperationUtc = NormalizeUtc(utcNow),
                MailClaimIds = mailIds == null ? Array.Empty<string>() : (string[])mailIds.Clone()
            };
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        }

        // 08.06 안건준 추가 - 수호자의 탑 결과 요청 (식량 대소동과 별도 최고기록 집계)
        // 08.07 안건준 추가 - 성공적으로 클리어했을 때만 난이도를 1 올려서 다음 판 적 수·건물 체력 스케일링에 사용한다.
        // 08.07 안건준 수정 - 실패(전멸·시간초과)한 판까지 난이도가 오르면 테스트를 반복할수록 건물 체력이
        // 끝없이 불어나 버려서, cleared가 true일 때만 난이도를 올리도록 수정했다.
        public static GameProgressChange RecordGuardiansTowerClear(int killCount, bool cleared, RewardBundle rewards)
        {
            return RecordGuardiansTowerClear(killCount, cleared, cleared, rewards);
        }

        public static GameProgressChange RecordGuardiansTowerClear(
            int killCount,
            bool cleared,
            bool advanceDifficulty,
            RewardBundle rewards)
        {
            return new GameProgressChange
            {
                GuardiansTowerBestKills = Math.Max(0, killCount),
                IncrementGuardiansTowerDifficulty = cleared && advanceDifficulty,
                Rewards = rewards
            };
        }

        public static GameProgressChange RecordCastleRaidClear() // 성 파괴 기록 요청
        {
            return RecordCastleRaidClear(RewardBundle.Empty);
        }

        public static GameProgressChange RecordCastleRaidClear(RewardBundle rewards)
        {
            return new GameProgressChange
            {
                MarkCastleRaidCleared = true,
                Rewards = rewards ?? RewardBundle.Empty
            };
        }

        public static GameProgressChange GrantCommanderExperience(long amount)
        {
            return new GameProgressChange
            {
                CommanderExperience = Math.Max(0L, amount)
            };
        }

        public static GameProgressChange GrantItems(params ItemAmount[] itemRewards)
        {
            return new GameProgressChange
            {
                HasStandaloneItemGrant = true,
                Rewards = RewardBundle.FromItems(itemRewards)
            };
        }

        public static GameProgressChange GrantRewards(RewardBundle rewards)
        {
            return GrantRewards(rewards, null);
        }

        public static GameProgressChange GrantRewards(RewardBundle rewards, string acquireMonsterId)
        {
            var normalizedId = acquireMonsterId?.Trim();
            return new GameProgressChange
            {
                Rewards = rewards ?? RewardBundle.Empty,
                HasAcquireMonster = !string.IsNullOrEmpty(normalizedId),
                AcquireMonsterId = normalizedId
            };
        }

        public static GameProgressChange DiscardItem(string itemId, long quantity, long expectedQuantity)
        {
            return new GameProgressChange
            {
                HasDiscardItem = true,
                ItemId = itemId?.Trim(),
                ItemQuantity = quantity,
                ExpectedItemQuantity = expectedQuantity
            };
        }

        public static GameProgressChange UseItem(string itemId, long quantity, long expectedQuantity)
        {
            return new GameProgressChange
            {
                HasUseItem = true,
                ItemId = itemId?.Trim(),
                ItemQuantity = quantity,
                ExpectedItemQuantity = expectedQuantity
            };
        }

        public static GameProgressChange LevelUpCommander(int expectedLevel)
        {
            return new GameProgressChange
            {
                HasLevelUpCommander = true,
                ExpectedCommanderLevel = expectedLevel
            };
        }

        public static GameProgressChange UpgradeCommanderLegionStat(
            CommanderLegionStat stat,
            int expectedLevel)
        {
            return new GameProgressChange
            {
                HasUpgradeCommanderLegionStat = true,
                CommanderLegionStatToUpgrade = stat,
                ExpectedCommanderLegionStatLevel = expectedLevel
            };
        }

        public static GameProgressChange AcquireMonster(string monsterId) // 첫 보유 추가 요청
        {
            return new GameProgressChange
            {
                HasAcquireMonster = true,
                AcquireMonsterId = monsterId?.Trim()
            };
        }

        public static GameProgressChange AssignMonster(string monsterId, MonsterPartyKind targetParty)
        {
            return new GameProgressChange
            {
                HasFormationChange = true,
                FormationMonsterId = monsterId?.Trim(),
                TargetParty = targetParty
            };
        }

        public static GameProgressChange UnassignMonster(string monsterId)
        {
            return new GameProgressChange
            {
                HasFormationChange = true,
                FormationMonsterId = monsterId?.Trim(),
                RemoveFromFormation = true
            };
        }

        public static GameProgressChange SetMainBattleFormation(Vector2[] slotOffsets)
        {
            Vector2[] copy = null;
            if (slotOffsets != null)
            {
                copy = new Vector2[slotOffsets.Length];
                Array.Copy(slotOffsets, copy, slotOffsets.Length);
            }

            return new GameProgressChange
            {
                HasMainBattleFormation = true,
                MainBattleFormationOffsets = copy
            };
        }
    }
}
