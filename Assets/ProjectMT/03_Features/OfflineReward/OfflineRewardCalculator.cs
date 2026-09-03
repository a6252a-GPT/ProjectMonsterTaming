using System;
using System.Collections.Generic;
using ProjectMT.Features.Equipment;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;

namespace ProjectMT.Features.OfflineReward
{
    public enum OfflineRewardCalculationStatus
    {
        NotDue,
        Ready,
        InventoryBlocked,
        InvalidData
    }

    public sealed class OfflineRewardCalculation
    {
        public OfflineRewardCalculation(RewardBundle rewards, OfflineRewardReceiptData receipt)
        {
            Rewards = rewards ?? RewardBundle.Empty;
            Receipt = receipt;
        }

        public RewardBundle Rewards { get; }
        public OfflineRewardReceiptData Receipt { get; }
    }

    // 최초 추첨값만 보관한다. 인벤토리 잠금·장착 변경 뒤에도 이 원시값으로 정리 계획만 다시 만든다.
    public sealed class OfflineRewardRollSnapshot
    {
        internal OfflineRewardRollSnapshot(OfflineRewardCalculation raw, bool legacy)
        {
            Raw = raw;
            IsLegacy = legacy;
        }

        internal OfflineRewardCalculation Raw { get; }
        internal bool IsLegacy { get; }
    }

    public static class OfflineRewardCalculator
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
                fromUtc, toUtc, basisStage, receiptId, config, default,
                EquipmentBalanceConfig.RuntimeDefault, null, out calculation);
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
            var status = TryRoll(
                fromUtc, toUtc, basisStage, receiptId, config, equipmentBalance, random, out var snapshot);
            return status == OfflineRewardCalculationStatus.Ready &&
                   TryPlan(snapshot, equipment, equipmentBalance, null, out calculation) ==
                   OfflineRewardCalculationStatus.Ready;
        }

        public static OfflineRewardCalculationStatus TryRoll(
            DateTime fromUtc,
            DateTime toUtc,
            int basisStage,
            string receiptId,
            OfflineRewardConfig config,
            EquipmentBalanceConfig equipmentBalance,
            Random random,
            out OfflineRewardRollSnapshot snapshot)
        {
            snapshot = null;
            fromUtc = fromUtc.ToUniversalTime();
            toUtc = toUtc.ToUniversalTime();
            equipmentBalance ??= EquipmentBalanceConfig.RuntimeDefault;
            if (config == null || !config.TryValidate(out _) ||
                !equipmentBalance.TryValidate(out _) || basisStage < 1 || toUtc <= fromUtc ||
                string.IsNullOrWhiteSpace(receiptId))
            {
                return OfflineRewardCalculationStatus.InvalidData;
            }

            var rawSeconds = (long)Math.Floor((toUtc - fromUtc).TotalSeconds);
            if (rawSeconds < config.MinimumOfflineSeconds)
            {
                return OfflineRewardCalculationStatus.NotDue;
            }

            var rewardedSeconds = Math.Min(rawSeconds, config.MaximumAccumulationSeconds);
            var stage = basisStage;
            if (!config.TryResolveRate(stage, out var rate))
            {
                return OfflineRewardCalculationStatus.InvalidData;
            }

            if (!config.UsesScaledRewards && !config.UsesIndependentRewards)
            {
                if (!TryCalculateLegacy(
                        fromUtc, toUtc, rawSeconds, rewardedSeconds, stage, receiptId, config, rate,
                        out var legacyCalculation))
                {
                    return OfflineRewardCalculationStatus.InvalidData;
                }

                snapshot = new OfflineRewardRollSnapshot(legacyCalculation, true);
                return OfflineRewardCalculationStatus.Ready;
            }

            var rng = random ?? new Random();
            var minutes = rewardedSeconds / 60L;
            var multiplier = config.UsesIndependentRewards
                ? BasisPointDenominator
                : rate.RewardMultiplierBasisPoints;
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
                    return OfflineRewardCalculationStatus.InvalidData;
                }

                randomStoneCount = rewardedSeconds / rate.UpgradeStoneIntervalSeconds;
                effectiveEquipmentChance = rate.EquipmentChanceBasisPointsPerMinute;
            }
            else if (!TryScale(minutes, config.BaseGoldPerMinute, multiplier, out gold) ||
                     !TryScale(minutes, config.BaseCommanderExperiencePerMinute, multiplier, out experience) ||
                     !TryScale(minutes, config.BaseUpgradeStonePerMinute, multiplier, out randomStoneCount))
            {
                return OfflineRewardCalculationStatus.InvalidData;
            }
            else
            {
                effectiveEquipmentChance = ResolveEffectiveChance(
                    config.BaseEquipmentChanceBasisPointsPerMinute, multiplier);
            }

            if (randomStoneCount > int.MaxValue)
            {
                return OfflineRewardCalculationStatus.InvalidData;
            }

            var slotStones = 0L;
            var skillStones = 0L;
            var potentialStones = 0L;
            for (var index = 0; index < randomStoneCount; index++)
            {
                switch (rng.Next(3))
                {
                    case 0: slotStones++; break;
                    case 1: skillStones++; break;
                    default: potentialStones++; break;
                }
            }

            var rolledEquipment = new List<EquipmentInstanceData>();
            for (var minute = 0L; minute < minutes; minute++)
            {
                if (rng.Next(BasisPointDenominator) < effectiveEquipmentChance)
                {
                    rolledEquipment.Add(EquipmentDropRoller.RollSingle(equipmentBalance, stage, rng));
                }
            }

            var items = new List<ItemAmount>(3);
            AddItem(items, ItemIds.EquipmentSlotUpgradeStone, slotStones);
            AddItem(items, ItemIds.CommanderSkillUpgradeStone, skillStones);
            AddItem(items, ItemIds.LegionPotentialUpgradeStone, potentialStones);
            var rewards = new RewardBundle(gold, experience, items);
            if (rewards.IsEmpty && rolledEquipment.Count == 0)
            {
                return OfflineRewardCalculationStatus.NotDue;
            }

            var rawReceipt = OfflineRewardReceiptData.Create(
                receiptId, fromUtc, toUtc, rewardedSeconds, stage, gold, experience,
                slotStones, skillStones, potentialStones, receiptGoldRate, receiptExperienceRate,
                multiplier, effectiveEquipmentChance, rolledEquipment, Array.Empty<string>(),
                rolledEquipment.Count, 0, 0L, 0L, rawSeconds > rewardedSeconds, config.BalanceVersion);
            if (!rawReceipt.IsValid)
            {
                return OfflineRewardCalculationStatus.InvalidData;
            }

            snapshot = new OfflineRewardRollSnapshot(
                new OfflineRewardCalculation(rewards, rawReceipt), false);
            return OfflineRewardCalculationStatus.Ready;
        }

        public static OfflineRewardCalculationStatus TryPlan(
            OfflineRewardRollSnapshot snapshot,
            GameProgressView progress,
            EquipmentBalanceConfig equipmentBalance,
            out OfflineRewardCalculation calculation)
        {
            calculation = null;
            equipmentBalance ??= EquipmentBalanceConfig.RuntimeDefault;
            if (!equipmentBalance.TryValidate(out _))
            {
                return OfflineRewardCalculationStatus.InvalidData;
            }

            return TryPlan(
                snapshot,
                progress.Equipment,
                equipmentBalance,
                EquipmentLegionBonusCalculator.CalculateTotal(progress, equipmentBalance),
                out calculation);
        }

        public static OfflineRewardCalculationStatus TryPlan(
            OfflineRewardRollSnapshot snapshot,
            EquipmentSaveDataView equipment,
            EquipmentBalanceConfig equipmentBalance,
            EquipmentLegionBonus? baselineBonus,
            out OfflineRewardCalculation calculation)
        {
            calculation = null;
            equipmentBalance ??= EquipmentBalanceConfig.RuntimeDefault;
            var raw = snapshot?.Raw;
            if (raw?.Receipt == null || !raw.Receipt.IsValid || !equipmentBalance.TryValidate(out _))
            {
                return OfflineRewardCalculationStatus.InvalidData;
            }

            if (snapshot.IsLegacy)
            {
                calculation = new OfflineRewardCalculation(raw.Rewards, raw.Receipt.Clone());
                return OfflineRewardCalculationStatus.Ready;
            }

            var receipt = raw.Receipt;
            var status = TryPlanInventory(
                equipment,
                equipmentBalance,
                baselineBonus ?? EquipmentLegionBonusCalculator.CalculateEquipmentTotal(equipment, equipmentBalance),
                receipt.EquipmentRewards,
                out var kept,
                out var existingDismantleIds,
                out var dismantledCount,
                out var dismantleStones,
                out var existingDismantleStones);
            if (status != OfflineRewardCalculationStatus.Ready)
            {
                return status;
            }

            if (receipt.EquipmentSlotUpgradeStone > long.MaxValue - dismantleStones ||
                !OfflineRewardReceiptData.TryParseUtc(receipt.SettledFromUtc, out var fromUtc) ||
                !OfflineRewardReceiptData.TryParseUtc(receipt.SettledToUtc, out var toUtc))
            {
                return OfflineRewardCalculationStatus.InvalidData;
            }

            var items = new List<ItemAmount>(3);
            AddItem(items, ItemIds.EquipmentSlotUpgradeStone, receipt.EquipmentSlotUpgradeStone + dismantleStones);
            AddItem(items, ItemIds.CommanderSkillUpgradeStone, receipt.CommanderSkillUpgradeStone);
            AddItem(items, ItemIds.LegionPotentialUpgradeStone, receipt.LegionPotentialUpgradeStone);
            var rewards = new RewardBundle(receipt.Gold, receipt.CommanderExperience, items);
            var plannedReceipt = OfflineRewardReceiptData.Create(
                receipt.ReceiptId, fromUtc, toUtc, receipt.ElapsedSeconds, receipt.BasisStage,
                receipt.Gold, receipt.CommanderExperience, receipt.EquipmentSlotUpgradeStone,
                receipt.CommanderSkillUpgradeStone, receipt.LegionPotentialUpgradeStone,
                receipt.GoldPerMinute, receipt.CommanderExperiencePerMinute,
                receipt.RewardMultiplierBasisPoints, receipt.EquipmentChanceBasisPointsPerMinute,
                kept, existingDismantleIds, receipt.RolledEquipmentCount, dismantledCount,
                dismantleStones, existingDismantleStones, receipt.Capped, receipt.BalanceVersion);
            if (!plannedReceipt.IsValid)
            {
                return OfflineRewardCalculationStatus.InvalidData;
            }

            calculation = new OfflineRewardCalculation(rewards, plannedReceipt);
            return OfflineRewardCalculationStatus.Ready;
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

        private static OfflineRewardCalculationStatus TryPlanInventory(
            EquipmentSaveDataView current,
            EquipmentBalanceConfig equipmentBalance,
            EquipmentLegionBonus baselineBonus,
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
            var replacementProtectedIds =
                EquipmentUpgradeEvaluator.GetBestReplacementInstanceIds(current, newEquipment, equipmentBalance, baselineBonus);
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
                    return OfflineRewardCalculationStatus.InvalidData;
                }

                if (!hasProactiveThreshold || instance.Grade > proactiveMaximumGrade ||
                    replacementProtectedIds.Contains(instance.InstanceId))
                {
                    remainingNewCount++;
                    continue;
                }

                var stone = EquipmentDismantleRules.GetUpgradeStoneAmount(instance.Grade);
                if (stone <= 0 || autoDismantleStones > long.MaxValue - stone)
                {
                    return OfflineRewardCalculationStatus.InvalidData;
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
                if (instance != null && !instance.IsLocked && !equippedIds.Contains(instance.InstanceId) &&
                    !replacementProtectedIds.Contains(instance.InstanceId))
                {
                    candidates.Add(new DismantleCandidate(instance, false));
                }
            }

            for (var index = 0; index < newEquipment.Count; index++)
            {
                if (!discardedNewIds.Contains(newEquipment[index].InstanceId) &&
                    !replacementProtectedIds.Contains(newEquipment[index].InstanceId))
                {
                    candidates.Add(new DismantleCandidate(newEquipment[index], true));
                }
            }

            candidates.Sort(CompareCandidates);
            if (candidates.Count < overflow)
            {
                return OfflineRewardCalculationStatus.InventoryBlocked;
            }

            for (var index = 0; index < overflow; index++)
            {
                var candidate = candidates[index];
                var stone = EquipmentDismantleRules.GetUpgradeStoneAmount(candidate.Instance.Grade);
                if (stone <= 0 || autoDismantleStones > long.MaxValue - stone)
                {
                    return OfflineRewardCalculationStatus.InvalidData;
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
            return OfflineRewardCalculationStatus.Ready;
        }

        private static int CompareCandidates(DismantleCandidate left, DismantleCandidate right)
        {
            var gradeComparison = left.Instance.Grade.CompareTo(right.Instance.Grade);
            if (gradeComparison != 0)
            {
                return gradeComparison;
            }

            var levelComparison = left.Instance.ItemLevel.CompareTo(right.Instance.ItemLevel);
            if (levelComparison != 0)
            {
                return levelComparison;
            }

            if (left.IsNew != right.IsNew)
            {
                return left.IsNew ? -1 : 1;
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
