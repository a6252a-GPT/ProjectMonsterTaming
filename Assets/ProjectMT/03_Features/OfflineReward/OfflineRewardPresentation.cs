using System;
using System.Collections.Generic;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;

namespace ProjectMT.Features.OfflineReward
{
    public sealed class OfflineRewardPresentation // 여러 미확인 영수증을 한 화면에 합친 표시값
    {
        private readonly string[] receiptIds;

        private OfflineRewardPresentation(
            string[] ids,
            long elapsedSeconds,
            int basisStage,
            long gold,
            long experience,
            long stone,
            long goldRate,
            long experienceRate,
            int stoneInterval,
            bool capped,
            bool mixedBasis)
        {
            receiptIds = ids;
            ElapsedSeconds = elapsedSeconds;
            BasisStage = basisStage;
            Gold = gold;
            CommanderExperience = experience;
            UpgradeStone = stone;
            GoldPerMinute = goldRate;
            CommanderExperiencePerMinute = experienceRate;
            UpgradeStoneIntervalSeconds = stoneInterval;
            Capped = capped;
            MixedBasis = mixedBasis;
        }

        public IReadOnlyList<string> ReceiptIds => receiptIds;
        public long ElapsedSeconds { get; }
        public int BasisStage { get; }
        public long Gold { get; }
        public long CommanderExperience { get; }
        public long UpgradeStone { get; }
        public long GoldPerMinute { get; }
        public long CommanderExperiencePerMinute { get; }
        public int UpgradeStoneIntervalSeconds { get; }
        public bool Capped { get; }
        public bool MixedBasis { get; }

        public RewardPresentationRequest CreateAcquirePresentation()
        {
            return new RewardPresentationRequest(
                new RewardPresentationItem(RewardPresentationKind.Gold, "골드", Gold),
                new RewardPresentationItem(
                    RewardPresentationKind.CommanderExperience,
                    "군단장 경험치",
                    CommanderExperience),
                new RewardPresentationItem(
                    RewardPresentationKind.Item,
                    ItemIds.GetFallbackDisplayName(ItemIds.EquipmentSlotUpgradeStone),
                    UpgradeStone,
                    ItemIds.EquipmentSlotUpgradeStone));
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
            var stage = 1;
            long goldRate = 0L;
            long experienceRate = 0L;
            var stoneInterval = 1;
            var capped = false;
            for (var index = 0; index < receipts.Count; index++)
            {
                var receipt = receipts[index];
                if (string.IsNullOrWhiteSpace(receipt.ReceiptId) ||
                    !TryAdd(elapsed, receipt.ElapsedSeconds, out elapsed) ||
                    !TryAdd(gold, receipt.Gold, out gold) ||
                    !TryAdd(experience, receipt.CommanderExperience, out experience) ||
                    !TryAdd(stone, receipt.UpgradeStone, out stone))
                {
                    return false;
                }

                ids.Add(receipt.ReceiptId);
                stage = receipt.BasisStage;
                goldRate = receipt.GoldPerMinute;
                experienceRate = receipt.CommanderExperiencePerMinute;
                stoneInterval = receipt.UpgradeStoneIntervalSeconds;
                capped |= receipt.Capped;
            }

            presentation = new OfflineRewardPresentation(
                ids.ToArray(),
                elapsed,
                stage,
                gold,
                experience,
                stone,
                goldRate,
                experienceRate,
                stoneInterval,
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
