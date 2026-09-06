using System;
using System.Collections.Generic;
using ProjectMT.Shared.Items;
using TMPro;
using UnityEngine;

namespace ProjectMT.Features.Inventory
{
    public sealed partial class ItemInventoryPageController
    {
        [Header("목록 정렬")]
        [SerializeField] private TMP_Dropdown sortDropdown;

        private readonly InventoryPresentationState presentationState = new InventoryPresentationState();
        private int currentSort;
        private static readonly List<string> SortLabels = new List<string> { "획득순", "등급순", "이름순" };

        private void ConfigureSortDropdown()
        {
            if (sortDropdown == null) return;
            sortDropdown.ClearOptions();
            sortDropdown.AddOptions(SortLabels);
            sortDropdown.SetValueWithoutNotify(currentSort);
            sortDropdown.onValueChanged.AddListener(HandleSortChanged);
            UpdateSortCaption();
        }

        private void ResetInventoryPresentation()
        {
            currentSort = 0;
            presentationState.Reset(progress != null ? progress.View.Items : default);
            sortDropdown?.SetValueWithoutNotify(currentSort);
            UpdateSortCaption();
        }

        private void HandleSortChanged(int value)
        {
            currentSort = Mathf.Clamp(value, 0, SortLabels.Count - 1);
            Refresh();
            UpdateSortCaption();
        }

        private void UpdateSortCaption()
        {
            if (sortDropdown != null && sortDropdown.captionText != null)
                sortDropdown.captionText.text = SortLabels[currentSort];
        }

        private IReadOnlyList<ItemInventoryEntryView> SortVisibleEntries(IReadOnlyList<ItemInventoryEntryView> entries)
        {
            var sorted = new List<ItemInventoryEntryView>(entries);
            sorted.Sort((left, right) => presentationState.Compare(left.Definition, right.Definition, currentSort));
            return sorted;
        }

        private sealed class InventoryPresentationState // 현재 진행 서비스의 획득 변화만 추적한다.
        {
            private readonly Dictionary<string, long> quantities = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, long> acquisitionOrder = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> unread = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private long sequence;

            public void Reset(ItemInventoryView inventory)
            {
                quantities.Clear();
                acquisitionOrder.Clear();
                unread.Clear();
                sequence = 0L;
                foreach (var stack in inventory.Stacks)
                {
                    quantities[stack.ItemId] = stack.Quantity;
                    acquisitionOrder[stack.ItemId] = ++sequence; // 저장된 스택 순서로 기존 아이템의 순서를 복원한다.
                }
            }

            public void Observe(ItemInventoryView inventory)
            {
                var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var stack in inventory.Stacks)
                {
                    present.Add(stack.ItemId);
                    quantities.TryGetValue(stack.ItemId, out var previous);
                    if (stack.Quantity > previous)
                    {
                        acquisitionOrder[stack.ItemId] = ++sequence;
                        unread.Add(stack.ItemId);
                    }
                    quantities[stack.ItemId] = stack.Quantity;
                }
                var removed = new List<string>();
                foreach (var id in quantities.Keys)
                    if (!present.Contains(id)) removed.Add(id);
                foreach (var id in removed)
                {
                    quantities.Remove(id);
                    acquisitionOrder.Remove(id);
                    unread.Remove(id);
                }
            }

            public bool IsNew(string itemId) => unread.Contains(itemId);
            public void MarkViewed(string itemId) => unread.Remove(itemId);

            public int Compare(ItemDefinition left, ItemDefinition right, int sort)
            {
                int order;
                if (sort == 1)
                    order = right.Grade.CompareTo(left.Grade);
                else if (sort == 2)
                    order = string.Compare(left.DisplayName, right.DisplayName, StringComparison.CurrentCulture);
                else
                {
                    acquisitionOrder.TryGetValue(left.ItemId, out var leftOrder);
                    acquisitionOrder.TryGetValue(right.ItemId, out var rightOrder);
                    order = rightOrder.CompareTo(leftOrder);
                }
                if (order != 0) return order;
                order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0 ? order : string.Compare(left.ItemId, right.ItemId, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
