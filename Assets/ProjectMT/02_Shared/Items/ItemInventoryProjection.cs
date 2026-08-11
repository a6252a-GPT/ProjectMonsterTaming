using System;
using System.Collections.Generic;

namespace ProjectMT.Shared.Items
{
    public readonly struct ItemInventoryEntryView // 목록과 상세창이 공유하는 조회값
    {
        public ItemInventoryEntryView(ItemDefinition definition, long quantity)
        {
            Definition = definition;
            Quantity = Math.Max(0L, quantity);
        }

        public ItemDefinition Definition { get; }
        public long Quantity { get; }
        public bool CanUse => Definition != null && Definition.IsUsable && Quantity > 0L;
        public bool CanDiscard => Definition != null && Definition.IsDiscardable && Quantity > 0L;
        public bool ShowQuantityGauge =>
            CanUse && Definition.AllowMultiUse && Definition.IsStackable && Quantity > 1L;
    }

    public static class ItemInventoryProjection // 저장 목록을 필터·정렬된 UI 항목으로 변환
    {
        public static IReadOnlyList<ItemInventoryEntryView> Build(
            ItemInventoryView inventory,
            ItemCatalog catalog,
            ItemCategory? category = null)
        {
            if (catalog == null)
            {
                return Array.Empty<ItemInventoryEntryView>();
            }

            var source = inventory.Stacks;
            var entries = new List<ItemInventoryEntryView>(source.Count);
            for (var index = 0; index < source.Count; index++)
            {
                var stack = source[index];
                if (!catalog.TryGet(stack.ItemId, out var definition) ||
                    (category.HasValue && definition.Category != category.Value))
                {
                    continue;
                }

                entries.Add(new ItemInventoryEntryView(definition, stack.Quantity));
            }

            entries.Sort(CompareEntries);
            return entries;
        }

        private static int CompareEntries(ItemInventoryEntryView left, ItemInventoryEntryView right)
        {
            var order = left.Definition.SortOrder.CompareTo(right.Definition.SortOrder);
            if (order != 0)
            {
                return order;
            }

            order = left.Definition.Category.CompareTo(right.Definition.Category);
            return order != 0
                ? order
                : string.Compare(
                    left.Definition.ItemId,
                    right.Definition.ItemId,
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
