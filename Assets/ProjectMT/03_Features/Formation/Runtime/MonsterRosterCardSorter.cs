using System;
using System.Collections.Generic;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;

namespace ProjectMT.Features.Formation
{
    internal static class MonsterRosterCardSorter // 보유 목록의 표시 순서만 정렬
    {
        internal static List<OwnedMonsterView> CreateSorted(
            MonsterRosterView roster,
            MonsterRarityCatalog rarityCatalog)
        {
            var entries = new List<SortEntry>(roster.OwnedMonsters.Count);
            for (var index = 0; index < roster.OwnedMonsters.Count; index++)
            {
                var owned = roster.OwnedMonsters[index];
                var rarity = MonsterRarity.Common;
                rarityCatalog?.TryGetRarity(owned.MonsterId, out rarity);
                entries.Add(new SortEntry(
                    owned,
                    IsAssigned(roster, owned.MonsterId),
                    rarity,
                    index));
            }

            entries.Sort(Compare);
            var result = new List<OwnedMonsterView>(entries.Count);
            for (var index = 0; index < entries.Count; index++)
            {
                result.Add(entries[index].Owned);
            }

            return result;
        }

        private static int Compare(SortEntry left, SortEntry right)
        {
            var assignedOrder = right.Assigned.CompareTo(left.Assigned);
            if (assignedOrder != 0)
            {
                return assignedOrder;
            }

            var rarityOrder = right.Rarity.CompareTo(left.Rarity);
            return rarityOrder != 0
                ? rarityOrder
                : left.OriginalIndex.CompareTo(right.OriginalIndex);
        }

        private static bool IsAssigned(MonsterRosterView roster, string monsterId)
        {
            return Contains(roster.MainPartySlots, monsterId) ||
                   Contains(roster.ReservePartySlots, monsterId);
        }

        private static bool Contains(IReadOnlyList<string> values, string monsterId)
        {
            for (var index = 0; index < values.Count; index++)
            {
                if (string.Equals(values[index], monsterId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct SortEntry
        {
            public SortEntry(
                OwnedMonsterView owned,
                bool assigned,
                MonsterRarity rarity,
                int originalIndex)
            {
                Owned = owned;
                Assigned = assigned;
                Rarity = rarity;
                OriginalIndex = originalIndex;
            }

            public OwnedMonsterView Owned { get; }
            public bool Assigned { get; }
            public MonsterRarity Rarity { get; }
            public int OriginalIndex { get; }
        }
    }
}
