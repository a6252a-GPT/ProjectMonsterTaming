using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.GameData
{
    public enum MonsterPartyKind // 편성 목적지
    {
        Main,
        Reserve
    }

    [Serializable]
    public sealed class OwnedMonsterData // 보유 몬스터 최소 기록
    {
        [SerializeField] private string monsterId;
        [SerializeField] private int level = 1;

        public OwnedMonsterData(string id, int initialLevel = 1)
        {
            monsterId = id?.Trim();
            level = Math.Max(1, initialLevel);
        }

        public string MonsterId => monsterId;
        public int Level => level;

        public OwnedMonsterData Clone()
        {
            return new OwnedMonsterData(monsterId, level);
        }

        internal bool Repair()
        {
            monsterId = monsterId?.Trim();
            level = Math.Max(1, level);
            return !string.IsNullOrEmpty(monsterId);
        }

        internal bool TryLevelUp(int expectedLevel)
        {
            if (level != expectedLevel || level == int.MaxValue)
            {
                return false;
            }

            level++;
            return true;
        }
    }

    [Serializable]
    public sealed class MonsterRosterData // 보유·본부대·예비 부대 저장 원본
    {
        public const int MainPartySlotCount = 5;
        public const int ReservePartySlotCount = 2;
        public const string StarterMonsterId = "tofu_01";

        [SerializeField] private List<OwnedMonsterData> ownedMonsters = new List<OwnedMonsterData>();
        [SerializeField] private string[] mainPartySlots = new string[MainPartySlotCount];
        [SerializeField] private string[] reservePartySlots = new string[ReservePartySlotCount];

        public static MonsterRosterData CreateDefault()
        {
            var data = new MonsterRosterData();
            data.ownedMonsters.Add(new OwnedMonsterData(StarterMonsterId));
            data.mainPartySlots[0] = StarterMonsterId;
            data.Repair();
            return data;
        }

        public MonsterRosterData Clone()
        {
            var clone = new MonsterRosterData
            {
                ownedMonsters = new List<OwnedMonsterData>(),
                mainPartySlots = ResizeSlots(mainPartySlots, MainPartySlotCount),
                reservePartySlots = ResizeSlots(reservePartySlots, ReservePartySlotCount)
            };

            if (ownedMonsters != null)
            {
                for (var index = 0; index < ownedMonsters.Count; index++)
                {
                    if (ownedMonsters[index] != null)
                    {
                        clone.ownedMonsters.Add(ownedMonsters[index].Clone());
                    }
                }
            }

            clone.Repair();
            return clone;
        }

        internal void Repair()
        {
            ownedMonsters ??= new List<OwnedMonsterData>();
            var ownedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var repairedOwned = new List<OwnedMonsterData>(ownedMonsters.Count);
            for (var index = 0; index < ownedMonsters.Count; index++)
            {
                var owned = ownedMonsters[index];
                if (owned != null && owned.Repair() && ownedIds.Add(owned.MonsterId))
                {
                    repairedOwned.Add(owned); // 처음 나온 보유 기록만 유지
                }
            }

            ownedMonsters = repairedOwned;

            if (ownedMonsters.Count == 0)
            {
                ownedMonsters.Add(new OwnedMonsterData(StarterMonsterId));
            }

            ownedIds.Clear();
            for (var index = 0; index < ownedMonsters.Count; index++)
            {
                ownedIds.Add(ownedMonsters[index].MonsterId);
            }

            mainPartySlots = ResizeSlots(mainPartySlots, MainPartySlotCount);
            reservePartySlots = ResizeSlots(reservePartySlots, ReservePartySlotCount);
            var assignedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            RepairSlots(mainPartySlots, ownedIds, assignedIds);
            RepairSlots(reservePartySlots, ownedIds, assignedIds);
            CompactSlots(mainPartySlots);
            CompactSlots(reservePartySlots);

            if (CountAssigned(mainPartySlots) == 0)
            {
                if (CountAssigned(reservePartySlots) > 0)
                {
                    mainPartySlots[0] = reservePartySlots[0];
                    reservePartySlots[0] = string.Empty;
                    CompactSlots(reservePartySlots); // 예비 첫 기를 본부대로 승격
                }
                else
                {
                    mainPartySlots[0] = ownedMonsters[0].MonsterId; // 최소 본부대 한 기 보장
                }
            }
        }

        internal bool TryAcquire(string monsterId)
        {
            var normalizedId = monsterId?.Trim();
            if (string.IsNullOrEmpty(normalizedId) || FindOwnedIndex(normalizedId) >= 0)
            {
                return false;
            }

            ownedMonsters.Add(new OwnedMonsterData(normalizedId));
            return true;
        }

        internal bool TryAssign(string monsterId, MonsterPartyKind targetKind)
        {
            var ownedIndex = FindOwnedIndex(monsterId);
            if (ownedIndex < 0)
            {
                return false;
            }

            var normalizedId = ownedMonsters[ownedIndex].MonsterId;
            var targetSlots = targetKind == MonsterPartyKind.Main ? mainPartySlots : reservePartySlots;
            if (FindSlot(targetSlots, normalizedId) >= 0 || CountAssigned(targetSlots) >= targetSlots.Length)
            {
                return false;
            }

            if (targetKind == MonsterPartyKind.Reserve &&
                FindSlot(mainPartySlots, normalizedId) >= 0 &&
                CountAssigned(mainPartySlots) <= 1)
            {
                return false; // 마지막 본부대 몬스터는 예비로 이동 금지
            }

            RemoveFromSlots(mainPartySlots, normalizedId);
            RemoveFromSlots(reservePartySlots, normalizedId);
            CompactSlots(mainPartySlots);
            CompactSlots(reservePartySlots);

            targetSlots = targetKind == MonsterPartyKind.Main ? mainPartySlots : reservePartySlots;
            targetSlots[CountAssigned(targetSlots)] = normalizedId; // 가장 왼쪽 빈칸에 배치
            return true;
        }

        internal bool TryUnassign(string monsterId)
        {
            var mainIndex = FindSlot(mainPartySlots, monsterId);
            if (mainIndex >= 0)
            {
                if (CountAssigned(mainPartySlots) <= 1)
                {
                    return false; // 본부대 최소 한 기 유지
                }

                mainPartySlots[mainIndex] = string.Empty;
                CompactSlots(mainPartySlots);
                return true;
            }

            var reserveIndex = FindSlot(reservePartySlots, monsterId);
            if (reserveIndex < 0)
            {
                return false;
            }

            reservePartySlots[reserveIndex] = string.Empty;
            CompactSlots(reservePartySlots);
            return true;
        }

        internal bool TryLevelUp(string monsterId, int expectedLevel)
        {
            var index = FindOwnedIndex(monsterId);
            return index >= 0 && ownedMonsters[index].TryLevelUp(expectedLevel);
        }

        internal bool TryGetOwned(string monsterId, out OwnedMonsterData owned)
        {
            var index = FindOwnedIndex(monsterId);
            if (index >= 0)
            {
                owned = ownedMonsters[index];
                return true;
            }

            owned = null;
            return false;
        }

        internal MonsterRosterView CreateView()
        {
            return new MonsterRosterView(this);
        }

        private static void RepairSlots(
            string[] slots,
            HashSet<string> ownedIds,
            HashSet<string> assignedIds)
        {
            for (var index = 0; index < slots.Length; index++)
            {
                var id = slots[index]?.Trim();
                if (string.IsNullOrEmpty(id) || !ownedIds.Contains(id) || !assignedIds.Add(id))
                {
                    slots[index] = string.Empty;
                    continue;
                }

                slots[index] = id;
            }
        }

        private static string[] ResizeSlots(string[] source, int size)
        {
            var result = new string[size];
            if (source != null)
            {
                Array.Copy(source, result, Math.Min(source.Length, result.Length));
            }

            for (var index = 0; index < result.Length; index++)
            {
                result[index] ??= string.Empty;
            }

            return result;
        }

        private int FindOwnedIndex(string monsterId)
        {
            if (!string.IsNullOrWhiteSpace(monsterId))
            {
                for (var index = 0; index < ownedMonsters.Count; index++)
                {
                    if (string.Equals(
                            ownedMonsters[index].MonsterId,
                            monsterId.Trim(),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return index;
                    }
                }
            }

            return -1;
        }

        private static int FindSlot(string[] slots, string monsterId)
        {
            if (!string.IsNullOrWhiteSpace(monsterId))
            {
                for (var index = 0; index < slots.Length; index++)
                {
                    if (string.Equals(slots[index], monsterId, StringComparison.OrdinalIgnoreCase))
                    {
                        return index;
                    }
                }
            }

            return -1;
        }

        private static void RemoveFromSlots(string[] slots, string monsterId)
        {
            var index = FindSlot(slots, monsterId);
            if (index >= 0)
            {
                slots[index] = string.Empty;
            }
        }

        private static int CountAssigned(string[] slots)
        {
            var count = 0;
            for (var index = 0; index < slots.Length; index++)
            {
                if (!string.IsNullOrEmpty(slots[index]))
                {
                    count++;
                }
            }

            return count;
        }

        private static void CompactSlots(string[] slots)
        {
            var writeIndex = 0;
            for (var readIndex = 0; readIndex < slots.Length; readIndex++)
            {
                if (!string.IsNullOrEmpty(slots[readIndex]))
                {
                    slots[writeIndex++] = slots[readIndex];
                }
            }

            while (writeIndex < slots.Length)
            {
                slots[writeIndex++] = string.Empty;
            }
        }

        internal IReadOnlyList<OwnedMonsterData> OwnedMonsters => ownedMonsters;
        internal string[] MainPartySlots => mainPartySlots;
        internal string[] ReservePartySlots => reservePartySlots;
    }

    public readonly struct OwnedMonsterView // UI·전투용 보유 몬스터 복사값
    {
        internal OwnedMonsterView(OwnedMonsterData data)
        {
            MonsterId = data?.MonsterId ?? string.Empty;
            Level = Math.Max(1, data?.Level ?? 1);
        }

        public string MonsterId { get; }
        public int Level { get; }
    }

    public readonly struct MonsterRosterView // 외부에 전달할 보유·편성 복사값
    {
        private readonly OwnedMonsterView[] ownedMonsters;
        private readonly string[] ownedMonsterIds;
        private readonly string[] mainPartySlots;
        private readonly string[] reservePartySlots;

        internal MonsterRosterView(MonsterRosterData data)
        {
            var owned = data.OwnedMonsters;
            ownedMonsters = new OwnedMonsterView[owned.Count];
            ownedMonsterIds = new string[owned.Count];
            for (var index = 0; index < owned.Count; index++)
            {
                ownedMonsters[index] = new OwnedMonsterView(owned[index]);
                ownedMonsterIds[index] = owned[index].MonsterId;
            }

            mainPartySlots = Copy(data.MainPartySlots);
            reservePartySlots = Copy(data.ReservePartySlots);
        }

        public IReadOnlyList<OwnedMonsterView> OwnedMonsters => ownedMonsters ?? Array.Empty<OwnedMonsterView>();
        public IReadOnlyList<string> OwnedMonsterIds => ownedMonsterIds ?? Array.Empty<string>();
        public IReadOnlyList<string> MainPartySlots => mainPartySlots ?? Array.Empty<string>();
        public IReadOnlyList<string> ReservePartySlots => reservePartySlots ?? Array.Empty<string>();

        public bool Owns(string monsterId)
        {
            if (!string.IsNullOrWhiteSpace(monsterId) && ownedMonsterIds != null)
            {
                for (var index = 0; index < ownedMonsterIds.Length; index++)
                {
                    if (string.Equals(ownedMonsterIds[index], monsterId, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool TryGetOwnedMonster(string monsterId, out OwnedMonsterView owned)
        {
            if (!string.IsNullOrWhiteSpace(monsterId) && ownedMonsters != null)
            {
                for (var index = 0; index < ownedMonsters.Length; index++)
                {
                    if (string.Equals(
                            ownedMonsters[index].MonsterId,
                            monsterId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        owned = ownedMonsters[index];
                        return true;
                    }
                }
            }

            owned = default;
            return false;
        }

        private static string[] Copy(string[] values)
        {
            if (values == null)
            {
                return Array.Empty<string>();
            }

            var copy = new string[values.Length];
            Array.Copy(values, copy, values.Length);
            return copy;
        }
    }
}
