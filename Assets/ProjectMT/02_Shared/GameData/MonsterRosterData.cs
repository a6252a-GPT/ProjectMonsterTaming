using System;
using System.Collections.Generic;
using ProjectMT.Shared.Unit;
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
        [SerializeField] private int ascensionLevel; // 직접 완료한 돌파 횟수 (0~5)
        [SerializeField] private int ascensionMaterialCount; // 같은 몬스터 중복 획득 재료
        [SerializeField] private bool collectionFiveStarRewardClaimed; // 도감 5돌파 보상 최초 1회 수령

        public OwnedMonsterData(
            string id,
            int initialLevel = 1,
            int initialAscensionLevel = 0,
            int initialAscensionMaterialCount = 0,
            bool initialCollectionFiveStarRewardClaimed = false)
        {
            monsterId = id?.Trim();
            level = Math.Max(1, initialLevel);
            ascensionLevel = Math.Max(
                0,
                Math.Min(initialAscensionLevel, MonsterAscension.MaxAscensionLevel));
            ascensionMaterialCount = Math.Max(
                0,
                Math.Min(initialAscensionMaterialCount, GetRemainingAscensionCount()));
            collectionFiveStarRewardClaimed = initialCollectionFiveStarRewardClaimed;
        }

        public string MonsterId => monsterId;
        public int Level => level;
        public int AscensionLevel => ascensionLevel;
        public int AscensionMaterialCount => ascensionMaterialCount;
        public bool CollectionFiveStarRewardClaimed => collectionFiveStarRewardClaimed;

        public OwnedMonsterData Clone()
        {
            return new OwnedMonsterData(
                monsterId,
                level,
                ascensionLevel,
                ascensionMaterialCount,
                collectionFiveStarRewardClaimed);
        }

        internal bool Repair()
        {
            monsterId = monsterId?.Trim();
            level = Math.Max(1, level);
            ascensionLevel = Math.Max(0, Math.Min(ascensionLevel, MonsterAscension.MaxAscensionLevel));
            ascensionMaterialCount = Math.Max(
                0,
                Math.Min(ascensionMaterialCount, GetRemainingAscensionCount()));
            return !string.IsNullOrEmpty(monsterId);
        }

        internal void ReplaceMonsterId(string replacementId)
        {
            monsterId = replacementId?.Trim();
        }

        internal void MergeRetiredProgress(OwnedMonsterData retired)
        {
            if (retired == null)
            {
                return;
            }

            level = Math.Max(level, retired.level);
            var preservedProgress = Math.Max(
                ascensionLevel + ascensionMaterialCount,
                retired.ascensionLevel + retired.ascensionMaterialCount);
            ascensionLevel = Math.Max(ascensionLevel, retired.ascensionLevel);
            ascensionMaterialCount = Math.Max(0, preservedProgress - ascensionLevel);
            collectionFiveStarRewardClaimed |= retired.collectionFiveStarRewardClaimed;
            Repair(); // 합쳐진 진행값을 현재 돌파 상한에 맞춘다
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

        internal bool TryAddAscensionMaterial()
        {
            if (ascensionMaterialCount >= GetRemainingAscensionCount())
            {
                return false;
            }

            ascensionMaterialCount++;
            return true;
        }

        internal bool TryAscend(int expectedAscensionLevel)
        {
            if (ascensionLevel != expectedAscensionLevel ||
                MonsterAscension.IsMaxAscension(ascensionLevel) ||
                ascensionMaterialCount <= 0)
            {
                return false;
            }

            ascensionMaterialCount--;
            ascensionLevel++;
            return true;
        }

        internal bool TryClaimCollectionFiveStarReward()
        {
            if (!MonsterAscension.IsMaxAscension(ascensionLevel) || collectionFiveStarRewardClaimed)
            {
                return false;
            }

            collectionFiveStarRewardClaimed = true;
            return true;
        }

        private int GetRemainingAscensionCount()
        {
            return Math.Max(0, MonsterAscension.MaxAscensionLevel - ascensionLevel);
        }
    }

    [Serializable]
    public sealed class MonsterRosterData // 보유·본부대·예비 부대 저장 원본
    {
        public const int MainPartySlotCount = 5; // 슬롯 확장 없이 고정 편성
        public const int ReservePartySlotCount = 3;
        public const string StarterMonsterId = "lumi_01";

        private static readonly Dictionary<string, string> RetiredMonsterIds =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "spike_01", "lumi_01" },
                { "tofu_01", "lumi_01" },
                { "tofu_02", "rabi_01" },
                { "tofu_03", "rabi_queen_01" },
                { "tofu_04", "aru_01" },
                { "tofu_05", "lumi_01" },
                { "tofu_06", "shakun_01" },
                { "tofu_07", "lucy_01" },
                { "tofu_08", "mukuk_01" },
                { "nerea_01", "chamchi_01" },
                { "ru_01", "rabi_queen_01" },
                { "rubea_01", "phoenix_01" },
                { "shell_01", "rabi_01" },
                { "grisu_fire_01", "angeonjun_01" },
                { "mingyu_legend_01", "werewolf_01" }
            };

        [SerializeField] private List<OwnedMonsterData> ownedMonsters = new List<OwnedMonsterData>();
        [SerializeField] private List<string> collectedMonsterIds = new List<string>();
        [SerializeField] private List<string> unconfirmedCollectionMonsterIds = new List<string>();
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
                collectedMonsterIds = collectedMonsterIds == null
                    ? new List<string>()
                    : new List<string>(collectedMonsterIds),
                unconfirmedCollectionMonsterIds = unconfirmedCollectionMonsterIds == null
                    ? new List<string>()
                    : new List<string>(unconfirmedCollectionMonsterIds),
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

            collectedMonsterIds ??= new List<string>();
            var collectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var repairedCollectedIds = new List<string>(collectedMonsterIds.Count + ownedMonsters.Count);
            for (var index = 0; index < collectedMonsterIds.Count; index++)
            {
                var id = collectedMonsterIds[index]?.Trim();
                if (!string.IsNullOrEmpty(id) && collectedIds.Add(id))
                {
                    repairedCollectedIds.Add(id);
                }
            }

            // 기존 저장 데이터에는 최초 수집 목록이 없으므로 현재 보유 목록을 최초 수집 기록으로 보완한다.
            for (var index = 0; index < ownedMonsters.Count; index++)
            {
                var id = ownedMonsters[index].MonsterId;
                if (!string.IsNullOrEmpty(id) && collectedIds.Add(id))
                {
                    repairedCollectedIds.Add(id);
                }
            }

            collectedMonsterIds = repairedCollectedIds;

            unconfirmedCollectionMonsterIds ??= new List<string>();
            var unconfirmedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var repairedUnconfirmedIds = new List<string>(unconfirmedCollectionMonsterIds.Count);
            for (var index = 0; index < unconfirmedCollectionMonsterIds.Count; index++)
            {
                var id = unconfirmedCollectionMonsterIds[index]?.Trim();
                if (!string.IsNullOrEmpty(id) && collectedIds.Contains(id) && unconfirmedIds.Add(id))
                {
                    repairedUnconfirmedIds.Add(id);
                }
            }

            unconfirmedCollectionMonsterIds = repairedUnconfirmedIds;

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

        internal void MigrateRetiredMonsterIds()
        {
            ownedMonsters ??= new List<OwnedMonsterData>();
            var migrated = new List<OwnedMonsterData>(ownedMonsters.Count);
            var byId = new Dictionary<string, OwnedMonsterData>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < ownedMonsters.Count; index++)
            {
                var owned = ownedMonsters[index];
                if (owned == null || !owned.Repair())
                {
                    continue;
                }

                var migratedId = ResolveRetiredMonsterId(owned.MonsterId);
                owned.ReplaceMonsterId(migratedId);
                if (byId.TryGetValue(migratedId, out var existing))
                {
                    existing.MergeRetiredProgress(owned); // 이미 정식 몬스터를 보유했다면 높은 진행값 보존
                    continue;
                }

                byId.Add(migratedId, owned);
                migrated.Add(owned);
            }

            ownedMonsters = migrated;
            if (collectedMonsterIds != null)
            {
                for (var index = 0; index < collectedMonsterIds.Count; index++)
                {
                    collectedMonsterIds[index] = ResolveRetiredMonsterId(collectedMonsterIds[index]);
                }
            }
            if (unconfirmedCollectionMonsterIds != null)
            {
                for (var index = 0; index < unconfirmedCollectionMonsterIds.Count; index++)
                {
                    unconfirmedCollectionMonsterIds[index] = ResolveRetiredMonsterId(unconfirmedCollectionMonsterIds[index]);
                }
            }
            MigrateSlotIds(mainPartySlots);
            MigrateSlotIds(reservePartySlots);
            Repair();
        }

        internal bool TryAcquire(string monsterId)
        {
            var normalizedId = monsterId?.Trim();
            if (string.IsNullOrEmpty(normalizedId) || FindOwnedIndex(normalizedId) >= 0)
            {
                return false;
            }

            ownedMonsters.Add(new OwnedMonsterData(normalizedId));
            collectedMonsterIds ??= new List<string>();
            if (!ContainsCollectedMonster(normalizedId))
            {
                collectedMonsterIds.Add(normalizedId);
                unconfirmedCollectionMonsterIds ??= new List<string>();
                if (!ContainsId(unconfirmedCollectionMonsterIds, normalizedId))
                {
                    unconfirmedCollectionMonsterIds.Add(normalizedId);
                }
            }
            return true;
        }

        internal bool TryAcknowledgeCollectionNew(string monsterId)
        {
            var normalizedId = monsterId?.Trim();
            if (string.IsNullOrEmpty(normalizedId) || unconfirmedCollectionMonsterIds == null)
            {
                return false;
            }

            for (var index = 0; index < unconfirmedCollectionMonsterIds.Count; index++)
            {
                if (string.Equals(unconfirmedCollectionMonsterIds[index], normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    unconfirmedCollectionMonsterIds.RemoveAt(index);
                    return true;
                }
            }

            return false;
        }

        private bool ContainsCollectedMonster(string monsterId)
        {
            if (collectedMonsterIds == null || string.IsNullOrWhiteSpace(monsterId))
            {
                return false;
            }

            for (var index = 0; index < collectedMonsterIds.Count; index++)
            {
                if (string.Equals(collectedMonsterIds[index], monsterId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsId(IReadOnlyList<string> values, string value)
        {
            if (values == null || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            for (var index = 0; index < values.Count; index++)
            {
                if (string.Equals(values[index], value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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

        // 최대 돌파까지 필요한 수만 중복 재료로 보관한다. 초과분은 호출자가 전용 재화로 바꾼다.
        internal bool TryAddAscensionMaterial(string monsterId)
        {
            var index = FindOwnedIndex(monsterId);
            return index >= 0 && ownedMonsters[index].TryAddAscensionMaterial();
        }

        internal bool TryAscend(string monsterId, int expectedAscensionLevel)
        {
            var index = FindOwnedIndex(monsterId);
            return index >= 0 && ownedMonsters[index].TryAscend(expectedAscensionLevel);
        }

        internal bool TryClaimCollectionFiveStarReward(string monsterId)
        {
            var index = FindOwnedIndex(monsterId);
            return index >= 0 && ownedMonsters[index].TryClaimCollectionFiveStarReward();
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

        private static void MigrateSlotIds(string[] slots)
        {
            if (slots == null)
            {
                return;
            }

            for (var index = 0; index < slots.Length; index++)
            {
                slots[index] = ResolveRetiredMonsterId(slots[index]);
            }
        }

        private static string ResolveRetiredMonsterId(string monsterId)
        {
            var normalizedId = monsterId?.Trim();
            return !string.IsNullOrEmpty(normalizedId) && RetiredMonsterIds.TryGetValue(normalizedId, out var replacement)
                ? replacement
                : normalizedId ?? string.Empty;
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
        internal IReadOnlyList<string> CollectedMonsterIds => collectedMonsterIds;
        internal IReadOnlyList<string> UnconfirmedCollectionMonsterIds => unconfirmedCollectionMonsterIds;
        internal string[] MainPartySlots => mainPartySlots;
        internal string[] ReservePartySlots => reservePartySlots;
    }

    public readonly struct OwnedMonsterView // UI·전투용 보유 몬스터 복사값
    {
        internal OwnedMonsterView(OwnedMonsterData data)
        {
            MonsterId = data?.MonsterId ?? string.Empty;
            Level = Math.Max(1, data?.Level ?? 1);
            AscensionLevel = Math.Max(0, data?.AscensionLevel ?? 0);
            AscensionMaterialCount = Math.Max(0, data?.AscensionMaterialCount ?? 0);
            CollectionFiveStarRewardClaimed = data?.CollectionFiveStarRewardClaimed ?? false;
        }

        public string MonsterId { get; }
        public int Level { get; }
        public int AscensionLevel { get; } // 직접 완료한 돌파 횟수
        public int AscensionMaterialCount { get; } // 보유한 중복 돌파 재료
        public bool CollectionFiveStarRewardClaimed { get; } // 도감 5돌파 보상 수령 여부
    }

    public readonly struct MonsterRosterView // 외부에 전달할 보유·편성 복사값
    {
        private readonly OwnedMonsterView[] ownedMonsters;
        private readonly string[] ownedMonsterIds;
        private readonly string[] collectedMonsterIds;
        private readonly string[] unconfirmedCollectionMonsterIds;
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

            collectedMonsterIds = Copy(data.CollectedMonsterIds);
            unconfirmedCollectionMonsterIds = Copy(data.UnconfirmedCollectionMonsterIds);

            mainPartySlots = Copy(data.MainPartySlots);
            reservePartySlots = Copy(data.ReservePartySlots);
        }

        public IReadOnlyList<OwnedMonsterView> OwnedMonsters => ownedMonsters ?? Array.Empty<OwnedMonsterView>();
        public IReadOnlyList<string> OwnedMonsterIds => ownedMonsterIds ?? Array.Empty<string>();
        public IReadOnlyList<string> CollectedMonsterIds => collectedMonsterIds ?? Array.Empty<string>();
        public IReadOnlyList<string> UnconfirmedCollectionMonsterIds => unconfirmedCollectionMonsterIds ?? Array.Empty<string>();
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

        public bool HasCollected(string monsterId)
        {
            if (!string.IsNullOrWhiteSpace(monsterId) && collectedMonsterIds != null)
            {
                for (var index = 0; index < collectedMonsterIds.Length; index++)
                {
                    if (string.Equals(collectedMonsterIds[index], monsterId, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IsCollectionNew(string monsterId)
        {
            if (!string.IsNullOrWhiteSpace(monsterId) && unconfirmedCollectionMonsterIds != null)
            {
                for (var index = 0; index < unconfirmedCollectionMonsterIds.Length; index++)
                {
                    if (string.Equals(unconfirmedCollectionMonsterIds[index], monsterId, StringComparison.OrdinalIgnoreCase))
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

        private static string[] Copy(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                copy[index] = values[index] ?? string.Empty;
            }

            return copy;
        }
    }
}
