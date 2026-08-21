using System;
using System.Collections.Generic;
using ProjectMT.Features.Equipment;
using ProjectMT.Shared.Equipment;
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
        private const int BasisPointDenominator = 10000;

        public static bool TryCalculate(
            DateTime fromUtc,
            DateTime toUtc,
            int basisStage,
            string receiptId,
            OfflineRewardConfig config,
            out OfflineRewardCalculation calculation)
        {
            return TryCalculate(
                fromUtc,
                toUtc,
                basisStage,
                receiptId,
                config,
                default,
                EquipmentBalanceConfig.RuntimeDefault,
                null,
                out calculation);
        }

        public static bool TryCalculate(
            DateTime fromUtc,
            DateTime toUtc,
            int basisStage,
            string receiptId,
            OfflineRewardConfig config,
            EquipmentSaveDataView equipment,
            EquipmentBalanceConfig equipmentBalance,
            Random random,
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

            if (!config.UsesScaledRewards && !config.UsesIndependentRewards)
            {
                return TryCalculateLegacy(
                    fromUtc,
                    toUtc,
                    rawSeconds,
                    rewardedSeconds,
                    stage,
                    receiptId,
                    config,
                    rate,
                    out calculation);
            }

            equipmentBalance ??= EquipmentBalanceConfig.RuntimeDefault;
            var rng = random ?? new Random();
            var minutes = rewardedSeconds / 60L;
            var multiplier = config.UsesIndependentRewards ? BasisPointDenominator : rate.RewardMultiplierBasisPoints;
            var receiptGoldRate = config.UsesIndependentRewards
                ? rate.GoldPerMinute
                : config.BaseGoldPerMinute;
            var receiptExperienceRate = config.UsesIndependentRewards
                ? rate.CommanderExperiencePerMinute
                : config.BaseCommanderExperiencePerMinute;
            long gold;
            long experience;
            long randomStoneCount;
            int effectiveEquipmentChance;
            if (config.UsesIndependentRewards)
            {
                if (!TryMultiply(minutes, rate.GoldPerMinute, out gold) ||
                    !TryMultiply(minutes, rate.CommanderExperiencePerMinute, out experience))
                {
                    return false;
                }

                randomStoneCount = rewardedSeconds / rate.UpgradeStoneIntervalSeconds;
                effectiveEquipmentChance = rate.EquipmentChanceBasisPointsPerMinute;
            }
            else if (!TryScale(minutes, config.BaseGoldPerMinute, multiplier, out gold) ||
                     !TryScale(minutes, config.BaseCommanderExperiencePerMinute, multiplier, out experience) ||
                     !TryScale(minutes, config.BaseUpgradeStonePerMinute, multiplier, out randomStoneCount))
            {
                return false;
            }
            else
            {
                effectiveEquipmentChance = ResolveEffectiveChance(
                    config.BaseEquipmentChanceBasisPointsPerMinute,
                    multiplier);
            }

            if (randomStoneCount > int.MaxValue)
            {
                return false;
            }

            var slotStones = 0L;
            var skillStones = 0L;
            var potentialStones = 0L;
            for (var index = 0; index < randomStoneCount; index++)
            {
                switch (rng.Next(3))
                {
                    case 0:
                        slotStones++;
                        break;
                    case 1:
                        skillStones++;
                        break;
                    default:
                        potentialStones++;
                        break;
                }
            }

            var rolledEquipment = new List<EquipmentInstanceData>();
            for (var minute = 0L; minute < minutes; minute++)
            {
                if (rng.Next(BasisPointDenominator) < effectiveEquipmentChance)
                {
                    rolledEquipment.Add(EquipmentDropRoller.RollSingle(equipmentBalance, rng));
                }
            }

            if (!TryPlanInventory(
                    equipment,
                    rolledEquipment,
                    out var keptEquipment,
                    out var existingDismantleIds,
                    out var autoDismantledCount,
                    out var autoDismantleStones,
                    out var existingDismantleStones) ||
                slotStones > long.MaxValue - autoDismantleStones)
            {
                return false;
            }

            var itemRewards = new List<ItemAmount>(3);
            AddItem(itemRewards, ItemIds.EquipmentSlotUpgradeStone, slotStones + autoDismantleStones);
            AddItem(itemRewards, ItemIds.CommanderSkillUpgradeStone, skillStones);
            AddItem(itemRewards, ItemIds.LegionPotentialUpgradeStone, potentialStones);
            var rewards = new RewardBundle(gold, experience, itemRewards);
            if (rewards.IsEmpty && keptEquipment.Count == 0)
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
                slotStones,
                skillStones,
                potentialStones,
                receiptGoldRate,
                receiptExperienceRate,
                multiplier,
                effectiveEquipmentChance,
                keptEquipment,
                existingDismantleIds,
                rolledEquipment.Count,
                autoDismantledCount,
                autoDismantleStones,
                existingDismantleStones,
                rawSeconds > rewardedSeconds,
                config.BalanceVersion);
            if (!receipt.IsValid)
            {
                return false;
            }

            calculation = new OfflineRewardCalculation(rewards, receipt);
            return true;
        }

        private static bool TryCalculateLegacy(
            DateTime fromUtc,
            DateTime toUtc,
            long rawSeconds,
            long rewardedSeconds,
            int stage,
            string receiptId,
            OfflineRewardConfig config,
            OfflineRewardRateEntry rate,
            out OfflineRewardCalculation calculation)
        {
            calculation = null;
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
            calculation = new OfflineRewardCalculation(rewards, receipt);
            return receipt.IsValid;
        }

        private static bool TryPlanInventory(
            EquipmentSaveDataView current,
            IReadOnlyList<EquipmentInstanceData> rolled,
            out List<EquipmentInstanceData> kept,
            out List<string> existingDismantleIds,
            out int autoDismantledCount,
            out long autoDismantleStones,
            out long existingDismantleStones)
        {
            kept = new List<EquipmentInstanceData>(rolled?.Count ?? 0);
            existingDismantleIds = new List<string>();
            autoDismantledCount = 0;
            autoDismantleStones = 0L;
            existingDismantleStones = 0L;
            var existing = current.Instances;
            var newEquipment = rolled ?? Array.Empty<EquipmentInstanceData>();
            var discardedNewIds = new HashSet<string>(StringComparer.Ordinal);
            var remainingNewCount = 0;
            var proactivelyDismantledCount = 0;
            var hasProactiveThreshold = OfflineAutoDismantlePolicyInfo.TryGetMaximumGrade(
                current.OfflineAutoDismantlePolicy,
                out var proactiveMaximumGrade);
            for (var index = 0; index < newEquipment.Count; index++)
            {
                var instance = newEquipment[index];
                if (instance == null)
                {
                    return false;
                }

                if (!hasProactiveThreshold || instance.Grade > proactiveMaximumGrade)
                {
                    remainingNewCount++;
                    continue;
                }

                var stone = EquipmentDismantleRules.GetUpgradeStoneAmount(instance.Grade);
                if (stone <= 0 || autoDismantleStones > long.MaxValue - stone)
                {
                    return false;
                }

                discardedNewIds.Add(instance.InstanceId);
                autoDismantleStones += stone;
                proactivelyDismantledCount++;
            }

            var overflow = Math.Max(0, existing.Count + remainingNewCount - EquipmentSaveData.MaxTotalQuantity);

            var equippedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (EquipmentPart part in Enum.GetValues(typeof(EquipmentPart)))
            {
                var equippedId = current.GetEquippedInstanceId(part);
                if (!string.IsNullOrEmpty(equippedId))
                {
                    equippedIds.Add(equippedId);
                }
            }

            var candidates = new List<DismantleCandidate>(existing.Count + newEquipment.Count);
            for (var index = 0; index < existing.Count; index++)
            {
                var instance = existing[index];
                if (instance != null && !instance.IsLocked && !equippedIds.Contains(instance.InstanceId))
                {
                    candidates.Add(new DismantleCandidate(instance, false));
                }
            }

            for (var index = 0; index < newEquipment.Count; index++)
            {
                if (!discardedNewIds.Contains(newEquipment[index].InstanceId))
                {
                    candidates.Add(new DismantleCandidate(newEquipment[index], true));
                }
            }

            candidates.Sort(CompareCandidates);
            if (candidates.Count < overflow)
            {
                return false;
            }

            for (var index = 0; index < overflow; index++)
            {
                var candidate = candidates[index];
                var stone = EquipmentDismantleRules.GetUpgradeStoneAmount(candidate.Instance.Grade);
                if (stone <= 0 || autoDismantleStones > long.MaxValue - stone)
                {
                    return false;
                }

                autoDismantleStones += stone;
                if (candidate.IsNew)
                {
                    discardedNewIds.Add(candidate.Instance.InstanceId);
                }
                else
                {
                    existingDismantleIds.Add(candidate.Instance.InstanceId);
                    existingDismantleStones += stone;
                }
            }

            for (var index = 0; index < newEquipment.Count; index++)
            {
                if (!discardedNewIds.Contains(newEquipment[index].InstanceId))
                {
                    kept.Add(newEquipment[index].Clone());
                }
            }

            autoDismantledCount = proactivelyDismantledCount + overflow;
            return true;
        }

        private static int CompareCandidates(DismantleCandidate left, DismantleCandidate right)
        {
            var gradeComparison = left.Instance.Grade.CompareTo(right.Instance.Grade);
            if (gradeComparison != 0)
            {
                return gradeComparison;
            }

            if (left.IsNew != right.IsNew)
            {
                return left.IsNew ? -1 : 1; // 같은 등급이면 신규 보상을 먼저 정리해 기존 선택을 보존
            }

            return string.CompareOrdinal(left.Instance.InstanceId, right.Instance.InstanceId);
        }

        private static int ResolveEffectiveChance(int baseChance, int multiplierBasisPoints)
        {
            var scaled = (long)Math.Max(0, baseChance) * Math.Max(1, multiplierBasisPoints) /
                         BasisPointDenominator;
            return (int)Math.Clamp(scaled, 0L, BasisPointDenominator);
        }

        private static bool TryScale(long minutes, long baseRate, int multiplierBasisPoints, out long result)
        {
            result = 0L;
            if (!TryMultiply(minutes, baseRate, out var baseTotal) || multiplierBasisPoints <= 0 ||
                baseTotal > long.MaxValue / multiplierBasisPoints)
            {
                return false;
            }

            result = baseTotal * multiplierBasisPoints / BasisPointDenominator;
            return true;
        }

        private static void AddItem(List<ItemAmount> rewards, string itemId, long amount)
        {
            if (amount > 0L)
            {
                rewards.Add(new ItemAmount(itemId, amount));
            }
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

        private readonly struct DismantleCandidate
        {
            public DismantleCandidate(EquipmentInstanceData instance, bool isNew)
            {
                Instance = instance;
                IsNew = isNew;
            }

            public EquipmentInstanceData Instance { get; }
            public bool IsNew { get; }
        }
    }
}
