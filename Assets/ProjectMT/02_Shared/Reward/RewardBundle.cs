using System;
using System.Collections.Generic;
using ProjectMT.Shared.Items;

namespace ProjectMT.Shared.Reward
{
    public sealed class RewardBundle // 실제 지급에 사용하는 최소 보상 묶음
    {
        public static readonly RewardBundle Empty = new RewardBundle(0L, 0L);
        private readonly ItemAmount[] items;

        public RewardBundle(
            long gold,
            long commanderExperience = 0L,
            IEnumerable<ItemAmount> itemRewards = null)
        {
            Gold = Math.Max(0L, gold);
            CommanderExperience = Math.Max(0L, commanderExperience);
            items = itemRewards == null
                ? Array.Empty<ItemAmount>()
                : new List<ItemAmount>(itemRewards).ToArray();
        }

        public long Gold { get; }
        public long CommanderExperience { get; }
        public IReadOnlyList<ItemAmount> Items => items;
        public bool IsEmpty => Gold <= 0L && CommanderExperience <= 0L && items.Length == 0;

        public static RewardBundle FromGold(long amount)
        {
            return amount <= 0L ? Empty : new RewardBundle(amount);
        }

        public static RewardBundle FromCommanderExperience(long amount)
        {
            return amount <= 0L ? Empty : new RewardBundle(0L, amount);
        }

        public static RewardBundle FromItems(params ItemAmount[] itemRewards)
        {
            return itemRewards == null || itemRewards.Length == 0
                ? Empty
                : new RewardBundle(0L, 0L, itemRewards);
        }

        public static bool TryCombine(RewardBundle first, RewardBundle second, out RewardBundle combined)
        {
            first ??= Empty;
            second ??= Empty;
            if (first.Gold > long.MaxValue - second.Gold ||
                first.CommanderExperience > long.MaxValue - second.CommanderExperience)
            {
                combined = null;
                return false;
            }

            var mergedItems = new List<ItemAmount>(first.Items.Count + second.Items.Count);
            var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (!TryAppendItems(first.Items, mergedItems, indexes) ||
                !TryAppendItems(second.Items, mergedItems, indexes))
            {
                combined = null;
                return false;
            }

            combined = new RewardBundle(
                first.Gold + second.Gold,
                first.CommanderExperience + second.CommanderExperience,
                mergedItems);
            return true;
        }

        private static bool TryAppendItems(
            IReadOnlyList<ItemAmount> source,
            List<ItemAmount> destination,
            Dictionary<string, int> indexes)
        {
            for (var index = 0; index < source.Count; index++)
            {
                var reward = source[index];
                if (!reward.IsValid)
                {
                    return false;
                }

                if (!indexes.TryGetValue(reward.ItemId, out var destinationIndex))
                {
                    indexes.Add(reward.ItemId, destination.Count);
                    destination.Add(reward);
                    continue;
                }

                var current = destination[destinationIndex];
                if (current.Amount > long.MaxValue - reward.Amount)
                {
                    return false;
                }

                destination[destinationIndex] = new ItemAmount(
                    current.ItemId,
                    current.Amount + reward.Amount);
            }

            return true;
        }
    }
}
