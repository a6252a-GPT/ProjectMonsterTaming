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
        internal MonsterGachaChannel GachaChannel { get; private set; }
        public static GameProgressChange LevelUpMonster(string monsterId, int expectedLevel)
        {
            return new GameProgressChange
            {
                HasLevelUpMonster = true,
                LevelUpMonsterId = monsterId?.Trim(),
                ExpectedMonsterLevel = expectedLevel
            };
        }

        public static GameProgressChange AscendMonster(string monsterId, int expectedAscensionLevel)
        {
            return new GameProgressChange
            {
                HasAscendMonster = true,
                AscendMonsterId = monsterId?.Trim(),
                ExpectedAscensionLevel = expectedAscensionLevel
            };
        }

        // 뽑기 결과 한 건 반영 요청. 신규면 획득, 중복이면 돌파 재료(초과분은 전용 재화),
        // 그리고 천장 카운터까지 한 번에 갱신된다 (GameProgressData.TryApplyGachaPull 참고).
        public static GameProgressChange RecordGachaPull(string monsterId, MonsterRarity rarity)
        {
            return new GameProgressChange
            {
                HasGachaPull = true,
                GachaPullMonsterId = monsterId?.Trim(),
                GachaPullRarity = rarity
            };
        }

        public static GameProgressChange RecordGachaPulls(
            IReadOnlyList<GachaPullRecord> pulls,
            IReadOnlyList<ItemAmount> itemCosts,
            MonsterGachaChannel channel = MonsterGachaChannel.Normal)
        {
            var pullCopy = new List<GachaPullRecord>(pulls?.Count ?? 0);
            if (pulls != null)
            {
                for (var index = 0; index < pulls.Count; index++)
                {
                    pullCopy.Add(pulls[index]);
                }
            }

            var costCopy = new List<ItemAmount>(itemCosts?.Count ?? 0);
            if (itemCosts != null)
            {
                for (var index = 0; index < itemCosts.Count; index++)
                {
                    costCopy.Add(itemCosts[index]);
                }
            }

            if (channel == MonsterGachaChannel.Soul)
            {
                costCopy.Clear();
                if (pullCopy.Count != GachaCostConfig.SingleDrawCount && pullCopy.Count != GachaCostConfig.TenDrawCount)
                    pullCopy.Clear();
                else
                    costCopy.Add(new ItemAmount(ItemIds.MonsterSoulStone,
                        pullCopy.Count == GachaCostConfig.TenDrawCount ? MonsterSoulRules.TenCost : MonsterSoulRules.SingleCost));
            }
            else if (channel != MonsterGachaChannel.Normal)
                pullCopy.Clear();

            return new GameProgressChange
            {
                GachaPulls = pullCopy,
                GachaChannel = channel,
                ItemCosts = costCopy
            };
        }

        // 08.10 안건준 추가 - 장비 드랍 결과(최대 6개)를 인벤토리에 추가 요청.
        public static GameProgressChange AcquireEquipment(List<EquipmentInstanceData> instances)
        {
            return new GameProgressChange
            {
                HasAcquireEquipment = true,
                AcquireEquipmentInstances = instances ?? new List<EquipmentInstanceData>()
            };
        }

        public static GameProgressChange RecordCastleRaidClear(
            int stage,
            RewardBundle rewards,
            IReadOnlyList<EquipmentInstanceData> equipment)
        {
            var equipmentCopy = CopyEquipment(equipment);
            return new GameProgressChange
            {
                MarkCastleRaidCleared = true,
                CastleRaidClearedStage = stage,
                Rewards = rewards ?? RewardBundle.Empty,
                HasAcquireEquipment = equipmentCopy.Count > 0,
                AcquireEquipmentInstances = equipmentCopy
            };
        }

        public static GameProgressChange GrantRewardsAndEquipment(
            RewardBundle rewards,
            IReadOnlyList<EquipmentInstanceData> equipment)
        {
            var equipmentCopy = CopyEquipment(equipment);
            return new GameProgressChange
            {
                Rewards = rewards ?? RewardBundle.Empty,
                HasAcquireEquipment = equipmentCopy.Count > 0,
                AcquireEquipmentInstances = equipmentCopy
            };
        }

        private static List<EquipmentInstanceData> CopyEquipment(
            IReadOnlyList<EquipmentInstanceData> equipment)
        {
            var copy = new List<EquipmentInstanceData>(equipment?.Count ?? 0);
            if (equipment == null)
            {
                return copy;
            }

            for (var index = 0; index < equipment.Count; index++)
            {
                if (equipment[index] != null)
                {
                    copy.Add(equipment[index].Clone());
                }
            }

            return copy;
        }

        // 지정한 인스턴스를 장착한다. 같은 부위에 이미 장착 중인 장비가 있으면 자동으로 교체된다.
        public static GameProgressChange EquipItem(string instanceId)
        {
            return new GameProgressChange
            {
                HasEquipItem = true,
                EquipItemInstanceId = instanceId?.Trim()
            };
        }

        public static GameProgressChange EquipItems(IReadOnlyList<string> instanceIds)
        {
            var copiedIds = new List<string>(instanceIds?.Count ?? 0);
            if (instanceIds != null)
            {
                for (var index = 0; index < instanceIds.Count; index++)
                {
                    copiedIds.Add(instanceIds[index]?.Trim());
                }
            }

            return new GameProgressChange
            {
                HasEquipItems = true,
                EquipItemInstanceIds = copiedIds
            };
        }

        public static GameProgressChange UnequipItem(EquipmentPart part)
        {
            return new GameProgressChange
            {
                HasUnequipItem = true,
                UnequipItemPart = part
            };
        }

        public static GameProgressChange SetEquipmentLock(
            string instanceId,
            bool expectedValue,
            bool nextValue)
        {
            return new GameProgressChange
            {
                HasSetEquipmentLock = true,
                EquipmentLockInstanceId = instanceId?.Trim(),
                ExpectedEquipmentLockValue = expectedValue,
                EquipmentLockValue = nextValue
            };
        }

        public static GameProgressChange DismantleEquipment(IReadOnlyList<string> instanceIds)
        {
            var copiedIds = new List<string>(instanceIds?.Count ?? 0);
            if (instanceIds != null)
            {
                for (var index = 0; index < instanceIds.Count; index++)
                {
                    copiedIds.Add(instanceIds[index]?.Trim());
                }
            }

            return new GameProgressChange
            {
                HasDismantleEquipment = true,
                DismantleEquipmentInstanceIds = copiedIds
            };
        }

        public static GameProgressChange SetOfflineAutoDismantlePolicy(
            OfflineAutoDismantlePolicy expected,
            OfflineAutoDismantlePolicy next)
        {
            return new GameProgressChange
            {
                HasSetOfflineAutoDismantlePolicy = true,
                ExpectedOfflineAutoDismantlePolicy = expected,
                OfflineAutoDismantlePolicy = next
            };
        }

        // 장비 부위 슬롯을 +1 강화한다.
        public static GameProgressChange UpgradeEquipmentSlot(EquipmentPart part, int expectedLevel)
        {
            return new GameProgressChange
            {
                HasUpgradeEquipmentSlot = true,
                UpgradeEquipmentSlotPart = part,
                ExpectedEquipmentSlotLevel = expectedLevel
            };
        }

        // 군단장 잠재능력 슬롯에 랜덤으로 뽑힌 옵션 1개를 최초로 배정한다.
        // 이미 값이 있는 슬롯이면 TryApply에서 실패 처리된다.
        public static GameProgressChange AssignCommanderPotentialSlot(
            int slotIndex,
            EquipmentOptionType optionType,
            EquipmentGrade grade,
            float value)
        {
            return new GameProgressChange
            {
                HasAssignCommanderPotentialSlot = true,
                CommanderPotentialSlotIndex = slotIndex,
                CommanderPotentialOptionType = optionType,
                CommanderPotentialGrade = grade,
                CommanderPotentialValue = value
            };
        }

        // "잠재 능력 변경": 강화석 1개를 소모해 잠기지 않은 슬롯들을 새로 뽑은 옵션으로 교체한다.
        // 추첨(랜덤)은 호출 전에 이미 끝나 있고, 여기서는 그 결과를 결정론적으로 반영만 한다.
        public static GameProgressChange RerollCommanderPotentialSlots(
            IReadOnlyList<CommanderPotentialRerollEntry> entries)
        {
            return new GameProgressChange
            {
                HasRerollCommanderPotentialSlots = true,
                CommanderPotentialRerollEntries = entries
            };
        }

        // "옵션 스탯 변경": 강화석 1개를 소모해 옵션 종류·등급은 유지하고 수치만 다시 뽑는다.
        // 잠금은 옵션 자체가 바뀌는 "잠재 능력 변경"만 막는 용도라 잠긴 슬롯도 여기서는 대상이 된다.
        public static GameProgressChange RerollCommanderPotentialValues(
            IReadOnlyList<CommanderPotentialRerollEntry> entries)
        {
            return new GameProgressChange
            {
                HasRerollCommanderPotentialValues = true,
                CommanderPotentialValueRerollEntries = entries
            };
        }

        // 잠재능력 슬롯의 자물쇠 아이콘 클릭 시 잠금/해제를 토글한다.
        public static GameProgressChange SetCommanderPotentialLocked(int slotIndex, bool expectedLocked, bool newLocked)
        {
            return new GameProgressChange
            {
                HasSetCommanderPotentialLocked = true,
                CommanderPotentialLockSlotIndex = slotIndex,
                ExpectedCommanderPotentialLocked = expectedLocked,
                NewCommanderPotentialLocked = newLocked
            };
        }

        public static GameProgressChange SetCommanderSkillAutoUse(bool expectedValue, bool newValue)
        {
            return new GameProgressChange
            {
                HasSetCommanderSkillAutoUse = true,
                ExpectedCommanderSkillAutoUse = expectedValue,
                NewCommanderSkillAutoUse = newValue
            };
        }

        public static GameProgressChange EquipCommanderSkill(
            int slotIndex,
            string expectedSkillId,
            string newSkillId)
        {
            return new GameProgressChange
            {
                HasEquipCommanderSkill = true,
                CommanderSkillSlotIndex = slotIndex,
                ExpectedCommanderSkillId = expectedSkillId?.Trim() ?? string.Empty,
                NewCommanderSkillId = newSkillId?.Trim() ?? string.Empty
            };
        }

        public static GameProgressChange RecordCommanderSkillSummon(
            int expectedSummonCount,
            string summonedSkillId)
        {
            return new GameProgressChange
            {
                HasRecordCommanderSkillSummon = true,
                ExpectedCommanderSkillSummonCount = expectedSummonCount,
                SummonedCommanderSkillIds = new[] { summonedSkillId?.Trim() ?? string.Empty }
            };
        }

        public static GameProgressChange RecordPaidCommanderSkillSummons(
            int expectedSummonCount,
            int drawCount,
            IReadOnlyList<string> summonedSkillIds)
        {
            var copiedIds = summonedSkillIds == null
                ? Array.Empty<string>()
                : summonedSkillIds.Select(id => id?.Trim() ?? string.Empty).ToArray();
            return new GameProgressChange
            {
                HasRecordCommanderSkillSummon = true,
                ExpectedCommanderSkillSummonCount = expectedSummonCount,
                SummonedCommanderSkillIds = copiedIds,
                CommanderSkillSummonRequiresPayment = true,
                CommanderSkillSummonDrawCount = drawCount
            };
        }

        public static GameProgressChange LevelUpCommanderSkill(
            string skillId,
            int expectedLevel)
        {
            return new GameProgressChange
            {
                HasLevelUpCommanderSkill = true,
                CommanderSkillToLevelUpId = skillId?.Trim() ?? string.Empty,
                ExpectedCommanderSkillLevel = expectedLevel
            };
        }

        // 구형 테스트/도구 호출 호환: 세 번째 인자는 과거 중복 수량 값이며,
        // 현재는 스킬 밸런스 설정에서 필요한 수량을 결정하므로 사용하지 않는다.
        public static GameProgressChange LevelUpCommanderSkill(
            string skillId,
            int expectedLevel,
            int legacyExpectedDuplicateCount)
        {
            return LevelUpCommanderSkill(skillId, expectedLevel);
        }

        public static GameProgressChange AcknowledgeMonsterCollectionNew(string monsterId)
        {
            return new GameProgressChange
            {
                HasAcknowledgeMonsterCollectionNew = true,
                AcknowledgeCollectionMonsterId = monsterId?.Trim()
            };
        }

        public static GameProgressChange ClaimMonsterCollectionFiveStarReward(string monsterId)
        {
            return new GameProgressChange
            {
                HasClaimMonsterCollectionFiveStarReward = true,
                CollectionRewardMonsterId = monsterId?.Trim()
            };
        }
    }
}
