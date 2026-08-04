using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.GameData
{
    [Serializable]
    public sealed class OwnedMonsterData // 보유 몬스터 최소 기록
    {
        [SerializeField] private string monsterId;

        public OwnedMonsterData(string id)
        {
            monsterId = id?.Trim();
        }

        public string MonsterId => monsterId;

        public OwnedMonsterData Clone()
        {
            return new OwnedMonsterData(monsterId);
        }

        internal bool Repair()
        {
            monsterId = monsterId?.Trim();
            return !string.IsNullOrEmpty(monsterId);
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

            if (assignedIds.Count == 0)
            {
                mainPartySlots[0] = ownedMonsters[0].MonsterId; // 최소 본부대 한 기 보장
            }
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

        internal IReadOnlyList<OwnedMonsterData> OwnedMonsters => ownedMonsters;
        internal string[] MainPartySlots => mainPartySlots;
        internal string[] ReservePartySlots => reservePartySlots;
    }

    public readonly struct MonsterRosterView // 외부에 전달할 보유·편성 복사값
    {
        private readonly string[] ownedMonsterIds;
        private readonly string[] mainPartySlots;
        private readonly string[] reservePartySlots;

        internal MonsterRosterView(MonsterRosterData data)
        {
            var owned = data.OwnedMonsters;
            ownedMonsterIds = new string[owned.Count];
            for (var index = 0; index < owned.Count; index++)
            {
                ownedMonsterIds[index] = owned[index].MonsterId;
            }

            mainPartySlots = Copy(data.MainPartySlots);
            reservePartySlots = Copy(data.ReservePartySlots);
        }

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
