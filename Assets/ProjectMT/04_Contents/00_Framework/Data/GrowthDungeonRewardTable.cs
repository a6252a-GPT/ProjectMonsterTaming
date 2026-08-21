using System;
using System.Collections.Generic;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
using UnityEngine;

namespace ProjectMT.Contents.Framework
{
    [Serializable]
    public sealed class GrowthDungeonRewardEntry // 성장 던전 한 단계의 파밍 기준 보상
    {
        [SerializeField, Range(1, GrowthDungeonRewardTable.AuthoredStageCount)] private int stage = 1;
        [SerializeField, Min(0)] private long gold;
        [SerializeField, Min(0)] private long commanderExperience;
        [SerializeField] private List<ItemAmount> items = new List<ItemAmount>();

        public int Stage => Math.Clamp(stage, 1, GrowthDungeonRewardTable.AuthoredStageCount);
        public long Gold => Math.Max(0L, gold);
        public long CommanderExperience => Math.Max(0L, commanderExperience);
        public IReadOnlyList<ItemAmount> Items => items != null ? items : Array.Empty<ItemAmount>();

        public bool TryCreate(
            int rewardBasisPoints,
            int stageOffset,
            int growthBasisPointsPerStage,
            out RewardBundle rewards)
        {
            rewards = null;
            if (rewardBasisPoints <= 0 ||
                stageOffset < 0 || growthBasisPointsPerStage <= 0 ||
                !TryResolveLinearValue(Gold, stageOffset, growthBasisPointsPerStage, out var stageGold) ||
                !TryResolveLinearValue(CommanderExperience, stageOffset, growthBasisPointsPerStage, out var stageExperience) ||
                !TryScale(stageGold, rewardBasisPoints, out var scaledGold) ||
                !TryScale(stageExperience, rewardBasisPoints, out var scaledExperience))
            {
                return false;
            }

            var scaledItems = new List<ItemAmount>(Items.Count);
            for (var index = 0; index < Items.Count; index++)
            {
                var item = Items[index];
                if (!item.IsValid ||
                    !TryResolveLinearValue(item.Amount, stageOffset, growthBasisPointsPerStage, out var stageAmount) ||
                    !TryScale(stageAmount, rewardBasisPoints, out var amount))
                {
                    return false;
                }

                scaledItems.Add(new ItemAmount(item.ItemId, amount));
            }

            rewards = new RewardBundle(scaledGold, scaledExperience, scaledItems);
            return !rewards.IsEmpty;
        }

        private static bool TryResolveLinearValue(
            long baseValue,
            int stageOffset,
            int growthBasisPointsPerStage,
            out long result)
        {
            result = 0L;
            if (baseValue < 0L || stageOffset < 0 || growthBasisPointsPerStage <= 0)
            {
                return false;
            }

            if (baseValue == 0L || stageOffset == 0)
            {
                result = baseValue;
                return true;
            }

            if (!TryScale(baseValue, growthBasisPointsPerStage, out var increment))
            {
                return false;
            }

            increment = Math.Max(1L, increment); // 소량 재료도 같은 수량에서 멈추지 않게 최소 1 증가
            result = increment > (long.MaxValue - baseValue) / stageOffset
                ? long.MaxValue
                : baseValue + increment * stageOffset;
            return true;
        }

        private static bool TryScale(long value, long basisPoints, out long result)
        {
            result = 0L;
            if (value < 0L || basisPoints <= 0L)
            {
                return false;
            }

            try
            {
                var scaled = decimal.Ceiling(
                    (decimal)value * basisPoints / GrowthDungeonRewardTable.BasisPointDenominator);
                result = scaled >= long.MaxValue ? long.MaxValue : (long)scaled;
            }
            catch (OverflowException)
            {
                result = long.MaxValue; // 저장 숫자 한계에서는 포화시키되 단계 진행 자체는 막지 않는다.
            }

            return true;
        }

#if UNITY_EDITOR
        public static GrowthDungeonRewardEntry EditorCreate(
            int rewardStage,
            long goldAmount,
            long experienceAmount,
            params ItemAmount[] itemRewards)
        {
            return new GrowthDungeonRewardEntry
            {
                stage = Math.Clamp(rewardStage, 1, GrowthDungeonRewardTable.AuthoredStageCount),
                gold = Math.Max(0L, goldAmount),
                commanderExperience = Math.Max(0L, experienceAmount),
                items = itemRewards == null ? new List<ItemAmount>() : new List<ItemAmount>(itemRewards)
            };
        }
#endif
    }

    [CreateAssetMenu(menuName = "ProjectMT/Content/Growth Dungeon Reward Table", fileName = "GrowthDungeonRewardTable")]
    public sealed class GrowthDungeonRewardTable : ScriptableObject // 1단계 기준 보상과 단계당 선형 증가폭
    {
        public const int BasisPointDenominator = 10000;
        public const int DefaultChallengeRewardBasisPoints = 20000; // 다음 미클리어 단계 도전 클리어 보상 200%
        public const int DefaultContinuationGrowthBasisPoints = 500; // 1단계 기준 보상의 +5%씩 선형 증가
        public const int AuthoredStageCount = 5;

        [SerializeField] private string contentId;
        [SerializeField, Min(1)]
        private int challengeRewardBasisPoints = DefaultChallengeRewardBasisPoints;
        [SerializeField, Min(1)]
        private int continuationGrowthBasisPoints = DefaultContinuationGrowthBasisPoints;
        [SerializeField] private List<GrowthDungeonRewardEntry> entries = new List<GrowthDungeonRewardEntry>();

        public string ContentId => contentId?.Trim() ?? string.Empty;
        public int ChallengeRewardBasisPoints => Math.Max(1, challengeRewardBasisPoints);
        public int ContinuationGrowthBasisPoints => continuationGrowthBasisPoints > 0
            ? Math.Clamp(continuationGrowthBasisPoints, 1, BasisPointDenominator)
            : DefaultContinuationGrowthBasisPoints;
        public IReadOnlyList<GrowthDungeonRewardEntry> Entries =>
            entries != null ? entries : Array.Empty<GrowthDungeonRewardEntry>();

        public bool TryCreate(int stage, ContentRunMode runMode, out RewardBundle rewards)
        {
            rewards = null;
            if (!GrowthDungeonStageRules.IsValidStage(stage) ||
                (runMode != ContentRunMode.Challenge && runMode != ContentRunMode.Farming &&
                 runMode != ContentRunMode.SeedTest))
            {
                return false;
            }

            for (var index = 0; index < Entries.Count; index++)
            {
                var entry = Entries[index];
                if (entry != null && entry.Stage == 1)
                {
                    var basisPoints = runMode == ContentRunMode.Challenge
                        ? ChallengeRewardBasisPoints
                        : BasisPointDenominator;
                    return entry.TryCreate(
                        basisPoints,
                        stage - 1,
                        ContinuationGrowthBasisPoints,
                        out rewards);
                }
            }

            return false;
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(ContentId) || Entries.Count != AuthoredStageCount)
            {
                error = $"Growth dungeon reward table must contain five stages. Asset={name}";
                return false;
            }

            var seen = new HashSet<int>();
            GrowthDungeonRewardEntry baseEntry = null;
            for (var index = 0; index < Entries.Count; index++)
            {
                var entry = Entries[index];
                if (entry == null || !seen.Add(entry.Stage) ||
                    !entry.TryCreate(BasisPointDenominator, 0, ContinuationGrowthBasisPoints, out _))
                {
                    error = $"Growth dungeon reward entry is invalid. Asset={name}, Index={index}";
                    return false;
                }

                if (entry.Stage == 1)
                {
                    baseEntry = entry;
                }
            }

            if (baseEntry == null)
            {
                error = $"Growth dungeon reward table has no stage 1 base. Asset={name}";
                return false;
            }

            for (var index = 0; index < Entries.Count; index++)
            {
                var entry = Entries[index];
                if (!entry.TryCreate(BasisPointDenominator, 0, ContinuationGrowthBasisPoints, out var authored) ||
                    !baseEntry.TryCreate(
                        BasisPointDenominator,
                        entry.Stage - 1,
                        ContinuationGrowthBasisPoints,
                        out var expected) ||
                    !RewardsMatch(authored, expected))
                {
                    error = $"Growth dungeon reward preview must follow the linear growth rule. Asset={name}, Stage={entry.Stage}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool RewardsMatch(RewardBundle left, RewardBundle right)
        {
            if (left == null || right == null || left.Gold != right.Gold ||
                left.CommanderExperience != right.CommanderExperience || left.Items.Count != right.Items.Count)
            {
                return false;
            }

            for (var index = 0; index < left.Items.Count; index++)
            {
                if (!string.Equals(left.Items[index].ItemId, right.Items[index].ItemId, StringComparison.OrdinalIgnoreCase) ||
                    left.Items[index].Amount != right.Items[index].Amount)
                {
                    return false;
                }
            }

            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string rewardContentId,
            int challengeBasisPoints,
            params GrowthDungeonRewardEntry[] stageRewards)
        {
            EditorConfigureWithContinuation(
                rewardContentId,
                challengeBasisPoints,
                DefaultContinuationGrowthBasisPoints,
                stageRewards);
        }

        public void EditorConfigureWithContinuation(
            string rewardContentId,
            int challengeBasisPoints,
            int continuationBasisPoints,
            params GrowthDungeonRewardEntry[] stageRewards)
        {
            contentId = rewardContentId?.Trim();
            challengeRewardBasisPoints = Math.Max(1, challengeBasisPoints);
            continuationGrowthBasisPoints = Math.Clamp(continuationBasisPoints, 1, BasisPointDenominator);
            entries = stageRewards == null
                ? new List<GrowthDungeonRewardEntry>()
                : new List<GrowthDungeonRewardEntry>(stageRewards);
        }
#endif
    }
}
