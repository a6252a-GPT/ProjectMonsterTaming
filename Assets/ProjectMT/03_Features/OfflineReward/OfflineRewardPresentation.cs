using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;

namespace ProjectMT.Features.OfflineReward
{
    public sealed class OfflineRewardPresentation // 여러 미확인 영수증을 한 화면에 합친 표시값
    {
        private readonly string[] receiptIds;
        private readonly EquipmentInstanceData[] equipmentRewards;

        private OfflineRewardPresentation(
            string[] ids,
            long elapsedSeconds,
            int basisStage,
            long gold,
            long experience,
            long stone,
            long equipmentSlotStone,
            long commanderSkillStone,
            long legionPotentialStone,
            long goldRate,
            long experienceRate,
            int stoneInterval,
            int multiplierBasisPoints,
            int equipmentChanceBasisPoints,
            EquipmentInstanceData[] equipment,
            int rolledEquipmentCount,
            int autoDismantledEquipmentCount,
            long autoDismantleUpgradeStone,
            bool capped,
            bool mixedBasis)
        {
            receiptIds = ids;
            ElapsedSeconds = elapsedSeconds;
            BasisStage = basisStage;
            Gold = gold;
            CommanderExperience = experience;
            UpgradeStone = stone;
            EquipmentSlotUpgradeStone = equipmentSlotStone;
            CommanderSkillUpgradeStone = commanderSkillStone;
            LegionPotentialUpgradeStone = legionPotentialStone;
            GoldPerMinute = goldRate;
            CommanderExperiencePerMinute = experienceRate;
            UpgradeStoneIntervalSeconds = stoneInterval;
            RewardMultiplierBasisPoints = multiplierBasisPoints;
            EquipmentChanceBasisPointsPerMinute = equipmentChanceBasisPoints;
            equipmentRewards = equipment ?? Array.Empty<EquipmentInstanceData>();
            RolledEquipmentCount = rolledEquipmentCount;
            AutoDismantledEquipmentCount = autoDismantledEquipmentCount;
            AutoDismantleUpgradeStone = autoDismantleUpgradeStone;
            Capped = capped;
            MixedBasis = mixedBasis;
        }

        public IReadOnlyList<string> ReceiptIds => receiptIds;
        public long ElapsedSeconds { get; }
        public int BasisStage { get; }
        public long Gold { get; }
        public long CommanderExperience { get; }
        public long UpgradeStone { get; }
        public long EquipmentSlotUpgradeStone { get; }
        public long CommanderSkillUpgradeStone { get; }
        public long LegionPotentialUpgradeStone { get; }
        public long GoldPerMinute { get; }
        public long CommanderExperiencePerMinute { get; }
        public int UpgradeStoneIntervalSeconds { get; }
        public int RewardMultiplierBasisPoints { get; }
        public int EquipmentChanceBasisPointsPerMinute { get; }
        public IReadOnlyList<EquipmentInstanceData> EquipmentRewards => equipmentRewards;
        public int RolledEquipmentCount { get; }
        public int AutoDismantledEquipmentCount { get; }
        public long AutoDismantleUpgradeStone { get; }
        public bool Capped { get; }
        public bool MixedBasis { get; }

        public RewardPresentationRequest CreateAcquirePresentation(ItemCatalog itemCatalog = null)
        {
            var items = new List<ItemAmount>(3)
            {
                new ItemAmount(ItemIds.EquipmentSlotUpgradeStone, EquipmentSlotUpgradeStone),
                new ItemAmount(ItemIds.CommanderSkillUpgradeStone, CommanderSkillUpgradeStone),
                new ItemAmount(ItemIds.LegionPotentialUpgradeStone, LegionPotentialUpgradeStone)
            };
            return RewardPresentationRequest.FromBundle(
                new RewardBundle(Gold, CommanderExperience, items),
                itemCatalog);
        }

        public static bool TryCreate(
            IReadOnlyList<OfflineRewardReceiptView> receipts,
            out OfflineRewardPresentation presentation)
        {
            presentation = null;
            if (receipts == null || receipts.Count == 0)
            {
                return false;
            }

            var ids = new List<string>(receipts.Count);
            long elapsed = 0L;
            long gold = 0L;
            long experience = 0L;
            long stone = 0L;
            long equipmentSlotStone = 0L;
            long commanderSkillStone = 0L;
            long legionPotentialStone = 0L;
            var stage = 1;
            long goldRate = 0L;
            long experienceRate = 0L;
            var stoneInterval = 1;
            var multiplierBasisPoints = 10000;
            var equipmentChanceBasisPoints = 0;
            var equipment = new List<EquipmentInstanceData>();
            var rolledEquipmentCount = 0;
            var autoDismantledEquipmentCount = 0;
            long autoDismantleUpgradeStone = 0L;
            var capped = false;
            for (var index = 0; index < receipts.Count; index++)
            {
                var receipt = receipts[index];
                if (string.IsNullOrWhiteSpace(receipt.ReceiptId) ||
                    !TryAdd(elapsed, receipt.ElapsedSeconds, out elapsed) ||
                    !TryAdd(gold, receipt.Gold, out gold) ||
                    !TryAdd(experience, receipt.CommanderExperience, out experience) ||
                    !TryAdd(stone, receipt.UpgradeStone, out stone) ||
                    !TryAdd(equipmentSlotStone, receipt.EquipmentSlotUpgradeStone, out equipmentSlotStone) ||
                    !TryAdd(commanderSkillStone, receipt.CommanderSkillUpgradeStone, out commanderSkillStone) ||
                    !TryAdd(legionPotentialStone, receipt.LegionPotentialUpgradeStone, out legionPotentialStone) ||
                    !TryAdd(autoDismantleUpgradeStone, receipt.AutoDismantleUpgradeStone, out autoDismantleUpgradeStone) ||
                    receipt.RolledEquipmentCount > int.MaxValue - rolledEquipmentCount ||
                    receipt.AutoDismantledEquipmentCount > int.MaxValue - autoDismantledEquipmentCount)
                {
                    return false;
                }

                ids.Add(receipt.ReceiptId);
                stage = receipt.BasisStage;
                goldRate = receipt.GoldPerMinute;
                experienceRate = receipt.CommanderExperiencePerMinute;
                stoneInterval = receipt.UpgradeStoneIntervalSeconds;
                multiplierBasisPoints = receipt.RewardMultiplierBasisPoints;
                equipmentChanceBasisPoints = receipt.EquipmentChanceBasisPointsPerMinute;
                rolledEquipmentCount += receipt.RolledEquipmentCount;
                autoDismantledEquipmentCount += receipt.AutoDismantledEquipmentCount;
                for (var equipmentIndex = 0; equipmentIndex < receipt.EquipmentRewards.Count; equipmentIndex++)
                {
                    if (receipt.EquipmentRewards[equipmentIndex] != null)
                    {
                        equipment.Add(receipt.EquipmentRewards[equipmentIndex].Clone());
                    }
                }
                capped |= receipt.Capped;
            }

            if (!TryAdd(equipmentSlotStone, autoDismantleUpgradeStone, out var totalEquipmentSlotStone))
            {
                return false;
            }

            presentation = new OfflineRewardPresentation(
                ids.ToArray(),
                elapsed,
                stage,
                gold,
                experience,
                stone,
                totalEquipmentSlotStone,
                commanderSkillStone,
                legionPotentialStone,
                goldRate,
                experienceRate,
                stoneInterval,
                multiplierBasisPoints,
                equipmentChanceBasisPoints,
                equipment.ToArray(),
                rolledEquipmentCount,
                autoDismantledEquipmentCount,
                autoDismantleUpgradeStone,
                capped,
                receipts.Count > 1);
            return true;
        }

        private static bool TryAdd(long first, long second, out long result)
        {
            if (first < 0L || second < 0L || first > long.MaxValue - second)
            {
                result = 0L;
                return false;
            }

            result = first + second;
            return true;
        }
    }
}
