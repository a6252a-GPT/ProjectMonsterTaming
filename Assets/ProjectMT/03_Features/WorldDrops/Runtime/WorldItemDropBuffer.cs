using System;
using System.Collections.Generic;
using ProjectMT.Shared.Items;

namespace ProjectMT.Features.WorldDrops
{
    public sealed class WorldItemDropBuffer // 저장 전 획득분을 ID별로 모으는 메모리 원장
    {
        private readonly Dictionary<string, long> pending =
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        public int ItemTypeCount => pending.Count;
        public bool HasItems => pending.Count > 0;

        public bool TryAdd(ItemAmount amount)
        {
            if (!amount.IsValid)
            {
                return false;
            }

            pending.TryGetValue(amount.ItemId, out var current);
            try
            {
                pending[amount.ItemId] = checked(current + amount.Amount);
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        public bool TryCreateSnapshot(out ItemAmount[] items)
        {
            if (pending.Count == 0)
            {
                items = Array.Empty<ItemAmount>();
                return false;
            }

            var ids = new List<string>(pending.Keys);
            ids.Sort(StringComparer.OrdinalIgnoreCase);
            items = new ItemAmount[ids.Count];
            for (var index = 0; index < ids.Count; index++)
            {
                var id = ids[index];
                items[index] = new ItemAmount(id, pending[id]);
            }

            return true;
        }

        public bool TryCommit(IReadOnlyList<ItemAmount> snapshot)
        {
            if (snapshot == null || snapshot.Count == 0)
            {
                return false;
            }

            var amountsById = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < snapshot.Count; index++)
            {
                var amount = snapshot[index];
                if (!amount.IsValid)
                {
                    return false;
                }

                amountsById.TryGetValue(amount.ItemId, out var accumulated);
                try
                {
                    amountsById[amount.ItemId] = checked(accumulated + amount.Amount);
                }
                catch (OverflowException)
                {
                    return false;
                }
            }

            foreach (var pair in amountsById)
            {
                if (!pending.TryGetValue(pair.Key, out var current) || current < pair.Value)
                {
                    return false;
                }
            }

            foreach (var pair in amountsById)
            {
                var remaining = pending[pair.Key] - pair.Value;
                if (remaining == 0L)
                {
                    pending.Remove(pair.Key);
                }
                else
                {
                    pending[pair.Key] = remaining;
                }
            }

            return true;
        }

        public void Clear()
        {
            pending.Clear();
        }
    }
}
