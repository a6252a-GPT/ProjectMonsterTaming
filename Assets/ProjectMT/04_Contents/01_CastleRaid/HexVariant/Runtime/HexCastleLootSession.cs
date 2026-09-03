using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Features.Equipment;
using ProjectMT.Features.WorldDrops;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    public static class HexCastleLootRules
    {
        private static readonly string[] KeyItemIds =
        {
            ItemIds.FoodRiotKey,
            ItemIds.TreasureSpiritKey,
            ItemIds.FallenCommanderKey,
            ItemIds.GuardiansTowerKey
        };

        public static long ResolveGoldTotal(int stage)
        {
            return 10000L + (Mathf.Clamp(stage, 1, CastleRaidStageRules.MaximumStage) - 1L) * 1000L;
        }

        public static int ResolveEquipmentTotal(int difficulty)
        {
            return 2 + (Mathf.Clamp(difficulty, 1, 10) - 1) / 2;
        }

        public static int ResolveKeyTotal(int difficulty)
        {
            difficulty = Mathf.Clamp(difficulty, 1, 10);
            if (difficulty <= 3) return 2;
            if (difficulty <= 6) return 3;
            if (difficulty <= 9) return 4;
            return 6;
        }

        public static long ResolveShare(long total, int index, int count)
        {
            if (total <= 0L || index < 0 || count <= 0 || index >= count)
            {
                return 0L;
            }

            return total / count + (index < total % count ? 1L : 0L);
        }

        public static string ResolveKeyItemId(int seed, int index)
        {
            var value = (seed + index) % KeyItemIds.Length;
            return KeyItemIds[value < 0 ? value + KeyItemIds.Length : value];
        }
    }

    public readonly struct HexCastleLootCapture
    {
        public HexCastleLootCapture(
            RewardBundle itemRewards,
            IReadOnlyList<EquipmentInstanceData> equipmentRewards)
        {
            ItemRewards = itemRewards ?? RewardBundle.Empty;
            EquipmentRewards = equipmentRewards ?? Array.Empty<EquipmentInstanceData>();
        }

        public RewardBundle ItemRewards { get; }
        public IReadOnlyList<EquipmentInstanceData> EquipmentRewards { get; }
        public static HexCastleLootCapture Empty =>
            new HexCastleLootCapture(RewardBundle.Empty, Array.Empty<EquipmentInstanceData>());
    }

    public sealed class HexCastleLootSession
    {
        private readonly Dictionary<HexCoordinates, DropAllocation> allocations =
            new Dictionary<HexCoordinates, DropAllocation>();
        private readonly Dictionary<string, long> earned =
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private readonly IGameProgressService progress;
        private readonly ItemCatalog itemCatalog;
        private readonly WorldItemDropRuntime worldDrops;
        private readonly EquipmentBalanceConfig equipmentBalance;
        private readonly EquipmentWorldDropRuntime equipmentWorldDrops;
        private readonly HexEquipmentRewardContext equipmentRewardContext;

        public HexCastleLootSession(
            Transform parent,
            IEnumerable<HexCastleCellRuntime> cells,
            IGameProgressService progressService,
            ItemCatalog catalog,
            WorldItemDropVisualCatalog visualCatalog,
            EquipmentBalanceConfig balance,
            EquipmentDropChestVisualCatalog equipmentVisualCatalog,
            Transform pickupTarget,
            Camera camera,
            int stage,
            int difficulty,
            int seed,
            HexEquipmentRewardContext rewardContext)
        {
            progress = progressService;
            itemCatalog = catalog;
            equipmentBalance = balance;
            equipmentRewardContext = rewardContext;
            BuildAllocations(cells, stage, difficulty, seed);
            if (parent != null && catalog != null && visualCatalog != null)
            {
                worldDrops = WorldItemDropRuntime.Create(
                    parent,
                    progressService,
                    catalog,
                    visualCatalog,
                    pickupTarget,
                    camera);
            }

            if (parent != null && balance != null && equipmentVisualCatalog != null)
            {
                equipmentWorldDrops = EquipmentWorldDropRuntime.Create(
                    parent,
                    progressService,
                    equipmentVisualCatalog,
                    pickupTarget,
                    camera);
            }
        }

        public int EarnedItemTypeCount => earned.Count;

        public bool HandleDestroyed(HexCastleCellRuntime cell)
        {
            if (cell == null || !allocations.Remove(cell.Coordinates, out var allocation))
            {
                return false;
            }

            if (allocation.EquipmentCount > 0)
            {
                SpawnEquipment(cell.Coordinates, allocation, cell.transform.position + Vector3.up * 0.55f);
                return true;
            }

            var amount = ResolveGrantableAmount(allocation.ItemId, allocation.ItemAmount);
            if (amount <= 0L)
            {
                return true;
            }

            earned.TryGetValue(allocation.ItemId, out var current);
            earned[allocation.ItemId] = current + amount;
            worldDrops?.TrySpawn(new WorldItemDropRequest(
                allocation.ItemId,
                amount,
                cell.transform.position + Vector3.up * 0.55f));
            return true;
        }

        public HexCastleLootCapture CaptureRewards()
        {
            worldDrops?.CollectAllActive();
            equipmentWorldDrops?.CollectAllActive();
            earned.TryGetValue(ItemIds.Gold, out var gold);
            var items = earned
                .Where(pair => !string.Equals(pair.Key, ItemIds.Gold, StringComparison.OrdinalIgnoreCase) &&
                               pair.Value > 0L)
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ItemAmount(pair.Key, pair.Value))
                .ToArray();
            var itemRewards = new RewardBundle(gold, 0L, items);
            var equipmentRewards = equipmentWorldDrops != null &&
                                   equipmentWorldDrops.TryGetPendingEquipment(out var equipment)
                ? equipment
                : new List<EquipmentInstanceData>();
            return new HexCastleLootCapture(itemRewards, equipmentRewards);
        }

        private void BuildAllocations(
            IEnumerable<HexCastleCellRuntime> cells,
            int stage,
            int difficulty,
            int seed)
        {
            var rewardCells = (cells ?? Array.Empty<HexCastleCellRuntime>())
                .Where(value => value != null && value.LootKind != HexCastleLootKind.None)
                .OrderBy(value => value.Coordinates)
                .ToArray();
            AddRoleAllocations(
                rewardCells.Where(value => value.LootKind == HexCastleLootKind.Gold).ToArray(),
                ItemIds.Gold,
                HexCastleLootRules.ResolveGoldTotal(stage));
            var equipmentCells = rewardCells
                .Where(value => value.LootKind == HexCastleLootKind.Equipment)
                .ToArray();
            var equipmentTotal = HexCastleLootRules.ResolveEquipmentTotal(difficulty);
            for (var index = 0; index < equipmentCells.Length; index++)
            {
                var cell = equipmentCells[index];
                allocations[cell.Coordinates] = DropAllocation.Equipment(
                    (int)HexCastleLootRules.ResolveShare(equipmentTotal, index, equipmentCells.Length),
                    seed * 397 ^ cell.Coordinates.GetHashCode() ^ index * 7919);
            }

            var keyCells = rewardCells.Where(value => value.LootKind == HexCastleLootKind.Key).ToArray();
            var keyTotal = HexCastleLootRules.ResolveKeyTotal(difficulty);
            for (var index = 0; index < keyCells.Length; index++)
            {
                allocations[keyCells[index].Coordinates] = DropAllocation.Item(
                    HexCastleLootRules.ResolveKeyItemId(seed, index),
                    HexCastleLootRules.ResolveShare(keyTotal, index, keyCells.Length));
            }
        }

        private void AddRoleAllocations(
            IReadOnlyList<HexCastleCellRuntime> cells,
            string itemId,
            long total)
        {
            for (var index = 0; index < cells.Count; index++)
            {
                allocations[cells[index].Coordinates] = DropAllocation.Item(
                    itemId,
                    HexCastleLootRules.ResolveShare(total, index, cells.Count));
            }
        }

        private void SpawnEquipment(HexCoordinates coordinates, DropAllocation allocation, Vector3 position)
        {
            if (allocation.EquipmentCount <= 0 || equipmentBalance == null || equipmentWorldDrops == null)
            {
                return;
            }

            for (var index = 0; index < allocation.EquipmentCount && equipmentWorldDrops.AvailableCapacity > 0; index++)
            {
                var equipment = equipmentRewardContext != null
                    ? equipmentRewardContext.Resolve(coordinates, index, allocation.RollSeed, equipmentBalance)
                    : EquipmentDropRoller.RollSingle(
                        equipmentBalance,
                        1,
                        new System.Random(unchecked(allocation.RollSeed * 486187739 + index * 16777619)));
                if (!equipmentWorldDrops.TrySpawn(new EquipmentWorldDropRequest(equipment, position)))
                {
                    break;
                }
            }
        }

        private long ResolveGrantableAmount(string itemId, long requested)
        {
            if (requested <= 0L || itemCatalog == null || !itemCatalog.TryGet(itemId, out var definition))
            {
                return 0L;
            }

            var owned = 0L;
            if (progress != null)
            {
                progress.View.Items.TryGetQuantity(itemId, out owned);
            }
            earned.TryGetValue(itemId, out var pending);
            return Math.Max(0L, Math.Min(requested, definition.MaxQuantity - owned - pending));
        }

        private readonly struct DropAllocation
        {
            private DropAllocation(
                string itemId,
                long itemAmount,
                int equipmentCount,
                int rollSeed)
            {
                ItemId = itemId;
                ItemAmount = itemAmount;
                EquipmentCount = equipmentCount;
                RollSeed = rollSeed;
            }

            public string ItemId { get; }
            public long ItemAmount { get; }
            public int EquipmentCount { get; }
            public int RollSeed { get; }

            public static DropAllocation Item(string itemId, long amount)
            {
                return new DropAllocation(itemId, amount, 0, 0);
            }

            public static DropAllocation Equipment(int count, int seed)
            {
                return new DropAllocation(string.Empty, 0L, Mathf.Max(0, count), seed);
            }
        }
    }
}

