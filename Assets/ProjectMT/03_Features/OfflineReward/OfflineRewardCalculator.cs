using System;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;

namespace ProjectMT.Features.OfflineReward
{
    public sealed class OfflineRewardCalculation // 저장 후보와 표시 영수증을 함께 생성한 계산 결과
    {
        public OfflineRewardCalculation(RewardBundle rewards, OfflineRewardReceiptData receipt)
        {
            Rewards = rewards ?? RewardBundle.Empty;
            Receipt = receipt;
        }

        public RewardBundle Rewards { get; }
        public OfflineRewardReceiptData Receipt { get; }
    }

    public static class OfflineRewardCalculator // 시간·단계만 입력받는 순수 방치 보상 계산
    {
        public static bool TryCalculate(
            DateTime fromUtc,
            DateTime toUtc,
            int basisStage,
            string receiptId,
            OfflineRewardConfig config,
            out OfflineRewardCalculation calculation)
        {
            calculation = null;
            fromUtc = fromUtc.ToUniversalTime();
            toUtc = toUtc.ToUniversalTime();
            if (config == null || !config.TryValidate(out _) || toUtc <= fromUtc ||
                string.IsNullOrWhiteSpace(receiptId))
            {
                return false;
            }

            var rawSeconds = (long)Math.Floor((toUtc - fromUtc).TotalSeconds);
            if (rawSeconds < config.MinimumOfflineSeconds)
            {
                return false;
            }

            var rewardedSeconds = Math.Min(rawSeconds, config.MaximumAccumulationSeconds);
            var stage = Math.Max(1, basisStage);
            if (!config.TryResolveRate(stage, out var rate))
            {
                return false;
            }

            var minutes = rewardedSeconds / 60L;
            if (!TryMultiply(minutes, rate.GoldPerMinute, out var gold) ||
                !TryMultiply(minutes, rate.CommanderExperiencePerMinute, out var experience))
            {
                return false;
            }

            var stones = rewardedSeconds / rate.UpgradeStoneIntervalSeconds;
            var rewards = new RewardBundle(
                gold,
                experience,
                stones > 0L
                    ? new[] { new ItemAmount(ItemIds.EquipmentSlotUpgradeStone, stones) }
                    : Array.Empty<ItemAmount>());
            if (rewards.IsEmpty)
            {
                return false;
            }

            var receipt = OfflineRewardReceiptData.Create(
                receiptId,
                fromUtc,
                toUtc,
                rewardedSeconds,
                stage,
                gold,
                experience,
                stones,
                rate.GoldPerMinute,
                rate.CommanderExperiencePerMinute,
                rate.UpgradeStoneIntervalSeconds,
                rawSeconds > rewardedSeconds,
                config.BalanceVersion);
            if (!receipt.IsValid)
            {
                return false;
            }

            calculation = new OfflineRewardCalculation(rewards, receipt);
            return true;
        }

        private static bool TryMultiply(long first, long second, out long result)
        {
            if (first < 0L || second < 0L || (second > 0L && first > long.MaxValue / second))
            {
                result = 0L;
                return false;
            }

            result = first * second;
            return true;
        }
    }
}
