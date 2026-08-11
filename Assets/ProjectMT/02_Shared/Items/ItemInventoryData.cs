using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.Items
{
    [Serializable]
    public sealed class ItemStackData // 저장 파일에 남는 일반 아이템 한 종류
    {
        [SerializeField] private string itemId;
        [SerializeField] private long quantity;

        internal ItemStackData(string itemId, long quantity)
        {
            this.itemId = itemId?.Trim();
            this.quantity = quantity;
        }

        internal string ItemId => itemId ?? string.Empty;
        internal long Quantity => quantity;

        internal ItemStackData Clone()
        {
            return new ItemStackData(itemId, quantity);
        }

        internal void SetQuantity(long value)
        {
            quantity = value;
        }
    }

    [Serializable]
    public sealed class ItemInventoryData // 일반 아이템 수량 저장 원본
    {
        [SerializeField] private List<ItemStackData> stacks = new List<ItemStackData>();

        public static ItemInventoryData CreateDefault()
        {
            return new ItemInventoryData();
        }

        public ItemInventoryData Clone()
        {
            var clone = new ItemInventoryData();
            var source = stacks ?? new List<ItemStackData>();
            for (var index = 0; index < source.Count; index++)
            {
                if (source[index] != null)
                {
                    clone.stacks.Add(source[index].Clone());
                }
            }

            return clone;
        }

        internal void Repair()
        {
            stacks ??= new List<ItemStackData>();
            var repaired = new List<ItemStackData>(stacks.Count);
            var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < stacks.Count; index++)
            {
                var stack = stacks[index];
                if (stack == null || string.IsNullOrWhiteSpace(stack.ItemId) || stack.Quantity <= 0L)
                {
                    continue;
                }

                var itemId = stack.ItemId.Trim();
                if (!indexes.TryGetValue(itemId, out var repairedIndex))
                {
                    indexes.Add(itemId, repaired.Count);
                    repaired.Add(new ItemStackData(itemId, stack.Quantity));
                    continue;
                }

                var current = repaired[repairedIndex].Quantity;
                var merged = current > long.MaxValue - stack.Quantity
                    ? long.MaxValue
                    : current + stack.Quantity;
                repaired[repairedIndex].SetQuantity(merged);
            }

            stacks = repaired;
        }

        internal bool TryGrant(IReadOnlyList<ItemAmount> grants, ItemCatalog catalog)
        {
            if (catalog == null || grants == null || grants.Count == 0)
            {
                return false;
            }

            var totals = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var definitions = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < grants.Count; index++)
            {
                var grant = grants[index];
                if (!grant.IsValid || !catalog.TryGet(grant.ItemId, out var definition))
                {
                    return false;
                }

                var canonicalId = definition.ItemId;
                totals.TryGetValue(canonicalId, out var accumulated);
                if (accumulated > long.MaxValue - grant.Amount)
                {
                    return false;
                }

                totals[canonicalId] = accumulated + grant.Amount;
                definitions[canonicalId] = definition;
            }

            foreach (var pair in totals)
            {
                var current = GetQuantity(pair.Key);
                var maximum = definitions[pair.Key].MaxQuantity;
                if (current > maximum || pair.Value > maximum - current)
                {
                    return false;
                }
            }

            foreach (var pair in totals)
            {
                var stackIndex = FindIndex(pair.Key);
                if (stackIndex >= 0)
                {
                    stacks[stackIndex].SetQuantity(stacks[stackIndex].Quantity + pair.Value);
                }
                else
                {
                    stacks.Add(new ItemStackData(definitions[pair.Key].ItemId, pair.Value));
                }
            }

            return true;
        }

        internal bool TryGrantCoreBalance(string itemId, long amount)
        {
            if (!ItemIds.TryGetCoreBalanceId(itemId, out var canonicalId) || amount <= 0L)
            {
                return false;
            }

            var current = GetQuantity(canonicalId);
            if (current > long.MaxValue - amount)
            {
                return false;
            }

            var stackIndex = FindIndex(canonicalId);
            if (stackIndex >= 0)
            {
                stacks[stackIndex].SetQuantity(current + amount);
            }
            else
            {
                stacks.Add(new ItemStackData(canonicalId, amount));
            }

            return true;
        }

        internal void MergeLegacyCoreBalance(string itemId, long amount)
        {
            if (!ItemIds.TryGetCoreBalanceId(itemId, out var canonicalId) || amount <= 0L)
            {
                return;
            }

            var current = GetQuantity(canonicalId);
            var merged = current > long.MaxValue - amount
                ? long.MaxValue
                : current + amount;
            var stackIndex = FindIndex(canonicalId);
            if (stackIndex >= 0)
            {
                stacks[stackIndex].SetQuantity(merged);
            }
            else
            {
                stacks.Add(new ItemStackData(canonicalId, merged));
            }
        }

        internal bool TrySpend(IReadOnlyList<ItemAmount> costs, ItemCatalog catalog)
        {
            if (costs == null || costs.Count == 0)
            {
                return false;
            }

            var totals = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < costs.Count; index++)
            {
                var cost = costs[index];
                if (!cost.IsValid)
                {
                    return false;
                }

                string canonicalId;
                if (catalog != null && catalog.TryGet(cost.ItemId, out var definition))
                {
                    canonicalId = definition.ItemId;
                }
                else if (!ItemIds.TryGetCoreBalanceId(cost.ItemId, out canonicalId))
                {
                    return false;
                }

                totals.TryGetValue(canonicalId, out var accumulated);
                if (accumulated > long.MaxValue - cost.Amount)
                {
                    return false;
                }

                totals[canonicalId] = accumulated + cost.Amount;
            }

            foreach (var pair in totals)
            {
                if (GetQuantity(pair.Key) < pair.Value)
                {
                    return false;
                }
            }

            foreach (var pair in totals)
            {
                if (!TryRemoveAvailable(pair.Key, pair.Value))
                {
                    return false;
                }
            }

            return true;
        }

        internal bool TryRemove(string itemId, long amount, long expectedQuantity)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0L || expectedQuantity < amount)
            {
                return false;
            }

            var index = FindIndex(itemId);
            if (index < 0 || stacks[index].Quantity != expectedQuantity)
            {
                return false;
            }

            var remaining = stacks[index].Quantity - amount;
            if (remaining == 0L)
            {
                stacks.RemoveAt(index);
            }
            else
            {
                stacks[index].SetQuantity(remaining);
            }

            return true;
        }

        public ItemInventoryView CreateView()
        {
            return new ItemInventoryView(stacks);
        }

        internal long GetQuantity(string itemId)
        {
            var index = FindIndex(itemId);
            return index >= 0 ? stacks[index].Quantity : 0L;
        }

        private bool TryRemoveAvailable(string itemId, long amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0L)
            {
                return false;
            }

            var index = FindIndex(itemId);
            if (index < 0 || stacks[index].Quantity < amount)
            {
                return false;
            }

            var remaining = stacks[index].Quantity - amount;
            if (remaining == 0L)
            {
                stacks.RemoveAt(index);
            }
            else
            {
                stacks[index].SetQuantity(remaining);
            }

            return true;
        }

        private int FindIndex(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return -1;
            }

            for (var index = 0; index < stacks.Count; index++)
            {
                if (string.Equals(stacks[index].ItemId, itemId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }
    }

    public readonly struct ItemStackView // UI에 전달할 일반 아이템 수량 복사값
    {
        public ItemStackView(string itemId, long quantity)
        {
            ItemId = itemId ?? string.Empty;
            Quantity = Math.Max(0L, quantity);
        }

        public string ItemId { get; }
        public long Quantity { get; }
    }

    public readonly struct ItemInventoryView // 외부에서 저장 원본을 바꾸지 못하는 목록
    {
        private readonly ItemStackView[] stacks;

        internal ItemInventoryView(IReadOnlyList<ItemStackData> source)
        {
            if (source == null || source.Count == 0)
            {
                stacks = Array.Empty<ItemStackView>();
                return;
            }

            stacks = new ItemStackView[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                stacks[index] = new ItemStackView(source[index].ItemId, source[index].Quantity);
            }
        }

        public IReadOnlyList<ItemStackView> Stacks => stacks ?? Array.Empty<ItemStackView>();

        public bool TryGetQuantity(string itemId, out long quantity)
        {
            if (!string.IsNullOrWhiteSpace(itemId) && stacks != null)
            {
                for (var index = 0; index < stacks.Length; index++)
                {
                    if (string.Equals(stacks[index].ItemId, itemId.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        quantity = stacks[index].Quantity;
                        return true;
                    }
                }
            }

            quantity = 0L;
            return false;
        }
    }
}
