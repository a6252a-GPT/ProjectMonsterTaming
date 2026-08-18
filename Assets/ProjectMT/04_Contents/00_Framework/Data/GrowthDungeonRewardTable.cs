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
        [SerializeField, Range(1, GrowthDungeonStageRules.MaximumStage)] private int stage = 1;
        [SerializeField, Min(0)] private long gold;
        [SerializeField, Min(0)] private long commanderExperience;
        [SerializeField] private List<ItemAmount> items = new List<ItemAmount>();

        public int Stage => Math.Clamp(stage, 1, GrowthDungeonStageRules.MaximumStage);
        public long Gold => Math.Max(0L, gold);
        public long CommanderExperience => Math.Max(0L, commanderExperience);
        public IReadOnlyList<ItemAmount> Items => items != null ? items : Array.Empty<ItemAmount>();

        public bool TryCreate(int rewardBasisPoints, out RewardBundle rewards)
        {
            rewards = null;
            if (rewardBasisPoints <= 0 || rewardBasisPoints > GrowthDungeonRewardTable.BasisPointDenominator ||
                !TryScale(Gold, rewardBasisPoints, out var scaledGold) ||
                !TryScale(CommanderExperience, rewardBasisPoints, out var scaledExperience))
            {
                return false;
            }

            var scaledItems = new List<ItemAmount>(Items.Count);
            for (var index = 0; index < Items.Count; index++)
            {
                var item = Items[index];
                if (!item.IsValid || !TryScale(item.Amount, rewardBasisPoints, out var amount))
                {
                    return false;
                }

                scaledItems.Add(new ItemAmount(item.ItemId, amount));
            }

            rewards = new RewardBundle(scaledGold, scaledExperience, scaledItems);
            return !rewards.IsEmpty;
        }

        private static bool TryScale(long value, int basisPoints, out long result)
        {
            result = 0L;
            if (value < 0L || (value > 0L && value > (long.MaxValue - (GrowthDungeonRewardTable.BasisPointDenominator - 1L)) / basisPoints))
            {
                return false;
            }

            result = (value * basisPoints + GrowthDungeonRewardTable.BasisPointDenominator - 1L) /
                     GrowthDungeonRewardTable.BasisPointDenominator; // Challenge 소수 보상은 올림
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
                stage = Math.Clamp(rewardStage, 1, GrowthDungeonStageRules.MaximumStage),
                gold = Math.Max(0L, goldAmount),
                commanderExperience = Math.Max(0L, experienceAmount),
                items = itemRewards == null ? new List<ItemAmount>() : new List<ItemAmount>(itemRewards)
            };
        }
#endif
    }

    [CreateAssetMenu(menuName = "ProjectMT/Content/Growth Dungeon Reward Table", fileName = "GrowthDungeonRewardTable")]
    public sealed class GrowthDungeonRewardTable : ScriptableObject // Farming·Challenge가 공유하는 5단계 보상 원본
    {
        public const int BasisPointDenominator = 10000;
        public const int DefaultChallengeRewardBasisPoints = 2500;

        [SerializeField] private string contentId;
        [SerializeField, Range(1, BasisPointDenominator)]
        private int challengeRewardBasisPoints = DefaultChallengeRewardBasisPoints;
        [SerializeField] private List<GrowthDungeonRewardEntry> entries = new List<GrowthDungeonRewardEntry>();

        public string ContentId => contentId?.Trim() ?? string.Empty;
        public int ChallengeRewardBasisPoints => Math.Clamp(challengeRewardBasisPoints, 1, BasisPointDenominator);
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
                if (entry != null && entry.Stage == stage)
                {
                    var basisPoints = runMode == ContentRunMode.Challenge
                        ? ChallengeRewardBasisPoints
                        : BasisPointDenominator;
                    return entry.TryCreate(basisPoints, out rewards);
                }
            }

            return false;
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(ContentId) || Entries.Count != GrowthDungeonStageRules.MaximumStage)
            {
                error = $"Growth dungeon reward table must contain five stages. Asset={name}";
                return false;
            }

            var seen = new HashSet<int>();
            for (var index = 0; index < Entries.Count; index++)
            {
                var entry = Entries[index];
                if (entry == null || !seen.Add(entry.Stage) ||
                    !entry.TryCreate(BasisPointDenominator, out _))
                {
                    error = $"Growth dungeon reward entry is invalid. Asset={name}, Index={index}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string rewardContentId,
            int challengeBasisPoints,
            params GrowthDungeonRewardEntry[] stageRewards)
        {
            contentId = rewardContentId?.Trim();
            challengeRewardBasisPoints = Math.Clamp(challengeBasisPoints, 1, BasisPointDenominator);
            entries = stageRewards == null
                ? new List<GrowthDungeonRewardEntry>()
                : new List<GrowthDungeonRewardEntry>(stageRewards);
        }
#endif
    }
}
