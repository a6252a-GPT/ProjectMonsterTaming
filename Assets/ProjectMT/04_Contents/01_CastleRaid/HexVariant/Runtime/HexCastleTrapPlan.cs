using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    public enum HexCastleTrapType
    {
        Snare = 0,
        SpikePlate = 1,
        BlastMine = 2
    }

    public sealed class HexCastleTrapPlacement
    {
        public HexCastleTrapPlacement(
            HexCoordinates coordinates,
            HexCastleTrapType trapType,
            int defenseBand,
            int regionId,
            string placementId)
        {
            Coordinates = coordinates;
            TrapType = trapType;
            DefenseBand = Mathf.Max(1, defenseBand);
            RegionId = Mathf.Clamp(regionId, 1, HexCoordinates.Directions.Length);
            PlacementId = placementId ?? string.Empty;
        }

        public HexCoordinates Coordinates { get; }
        public HexCastleTrapType TrapType { get; }
        public int DefenseBand { get; }
        public int RegionId { get; }
        public string PlacementId { get; }
    }

    public readonly struct HexCastleTrapBalance
    {
        private HexCastleTrapBalance(
            HexCastleTrapType trapType,
            float damageRatio,
            float splashDamageRatio,
            float effectDuration,
            float movementSpeedMultiplier,
            int maximumCharges,
            float rearmSeconds,
            int blastRadiusCells,
            float triggerDelaySeconds)
        {
            TrapType = trapType;
            DamageRatio = Mathf.Max(0f, damageRatio);
            SplashDamageRatio = Mathf.Max(0f, splashDamageRatio);
            EffectDuration = Mathf.Max(0f, effectDuration);
            MovementSpeedMultiplier = Mathf.Clamp(movementSpeedMultiplier, 0.1f, 1f);
            MaximumCharges = Mathf.Max(1, maximumCharges);
            RearmSeconds = Mathf.Max(0f, rearmSeconds);
            BlastRadiusCells = Mathf.Max(0, blastRadiusCells);
            TriggerDelaySeconds = Mathf.Max(0f, triggerDelaySeconds);
        }

        public HexCastleTrapType TrapType { get; }
        public float DamageRatio { get; }
        public float SplashDamageRatio { get; }
        public float EffectDuration { get; }
        public float MovementSpeedMultiplier { get; }
        public int MaximumCharges { get; }
        public float RearmSeconds { get; }
        public int BlastRadiusCells { get; }
        public float TriggerDelaySeconds { get; }

        public static HexCastleTrapBalance Resolve(HexCastleTrapType trapType, int difficultyLevel)
        {
            var difficultyMultiplier = 1f + (Mathf.Clamp(difficultyLevel, 1, 10) - 1) * 0.025f;
            switch (trapType)
            {
                case HexCastleTrapType.Snare:
                    return new HexCastleTrapBalance(
                        trapType,
                        0.08f * difficultyMultiplier,
                        0f,
                        1.4f,
                        1f,
                        1,
                        0f,
                        0,
                        0f);
                case HexCastleTrapType.SpikePlate:
                    return new HexCastleTrapBalance(
                        trapType,
                        0.05f * difficultyMultiplier,
                        0f,
                        1.2f,
                        0.70f,
                        3,
                        2.5f,
                        0,
                        0f);
                case HexCastleTrapType.BlastMine:
                    return new HexCastleTrapBalance(
                        trapType,
                        0.14f * difficultyMultiplier,
                        0.08f * difficultyMultiplier,
                        0.35f,
                        1f,
                        1,
                        0f,
                        1,
                        0.85f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(trapType), trapType, null);
            }
        }
    }

    internal static class HexCastleTrapPlanner
    {
        private sealed class TrapSlot
        {
            public HexCoordinates Coordinates;
            public int DefenseBand;
            public int RegionId;
        }

        public static IReadOnlyList<HexCastleTrapPlacement> Build(
            IReadOnlyDictionary<HexCoordinates, HexCastleCell> cells,
            IReadOnlyList<int> wallRadii,
            HexCastleDifficultyProfile profile,
            int buildRadius,
            int seed)
        {
            if (profile == null || profile.TotalTrapCount <= 0)
            {
                return Array.Empty<HexCastleTrapPlacement>();
            }

            var slots = cells.Values
                .Where(cell => cell.Kind == HexCastleCellKind.Ground &&
                               cell.IsOpen &&
                               cell.Coordinates.DistanceFromOrigin >
                               HexCastleFoundationGenerator.PalaceFootprintRadius &&
                               cell.Coordinates.DistanceFromOrigin <= buildRadius)
                .Select(cell => new TrapSlot
                {
                    Coordinates = cell.Coordinates,
                    DefenseBand = ResolveDefenseBand(cell.Coordinates.DistanceFromOrigin, wallRadii),
                    RegionId = ResolveRegion(cell.Coordinates)
                })
                .ToArray();
            var placements = new List<HexCastleTrapPlacement>(profile.TotalTrapCount);
            var zoneUsage = new Dictionary<int, int>();
            var typeZoneUsage = new Dictionary<int, int>();

            Place(HexCastleTrapType.BlastMine, profile.BlastMineCount);
            Place(HexCastleTrapType.Snare, profile.SnareTrapCount);
            Place(HexCastleTrapType.SpikePlate, profile.SpikePlateTrapCount);
            return placements.OrderBy(value => value.PlacementId, StringComparer.Ordinal).ToArray();

            void Place(HexCastleTrapType trapType, int count)
            {
                for (var typeIndex = 0; typeIndex < count; typeIndex++)
                {
                    var slot = slots
                        .Where(candidate => CanPlace(candidate.Coordinates, trapType, placements))
                        .OrderBy(candidate => ResolveUsage(zoneUsage, candidate))
                        .ThenBy(candidate => ResolveTypeUsage(typeZoneUsage, trapType, candidate))
                        .ThenBy(candidate => ResolvePlacementScore(
                            seed ^ ((int)trapType + 1) * 7919,
                            candidate.DefenseBand,
                            candidate.RegionId,
                            candidate.Coordinates))
                        .ThenBy(candidate => candidate.Coordinates)
                        .FirstOrDefault();
                    if (slot == null)
                    {
                        throw new InvalidOperationException(
                            $"난이도 {profile.Level} 함정 {profile.TotalTrapCount}개를 안전 간격으로 배치할 수 없습니다.");
                    }

                    var placementIndex = placements.Count + 1;
                    var placement = new HexCastleTrapPlacement(
                        slot.Coordinates,
                        trapType,
                        slot.DefenseBand,
                        slot.RegionId,
                        $"TRAP_D{profile.Level:00}_{placementIndex:00}_{trapType}");
                    placements.Add(placement);
                    Increment(zoneUsage, ResolveZoneKey(slot));
                    Increment(typeZoneUsage, ResolveTypeZoneKey(trapType, slot));
                }
            }
        }

        private static bool CanPlace(
            HexCoordinates coordinates,
            HexCastleTrapType trapType,
            IEnumerable<HexCastleTrapPlacement> placements)
        {
            foreach (var placement in placements)
            {
                var distance = coordinates.DistanceTo(placement.Coordinates);
                if (distance == 0)
                {
                    return false;
                }

                if (trapType == HexCastleTrapType.BlastMine &&
                    placement.TrapType == HexCastleTrapType.BlastMine &&
                    distance < 2)
                {
                    return false;
                }
            }

            return true;
        }

        private static int ResolveDefenseBand(int distance, IReadOnlyList<int> wallRadii)
        {
            for (var index = 0; index < wallRadii.Count; index++)
            {
                if (distance <= wallRadii[index])
                {
                    return index + 1;
                }
            }

            return wallRadii.Count;
        }

        private static int ResolveRegion(HexCoordinates coordinate)
        {
            var bestDirection = 0;
            var bestDistance = int.MaxValue;
            for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
            {
                var axis = HexCoordinates.Directions[direction] * coordinate.DistanceFromOrigin;
                var distance = coordinate.DistanceTo(axis);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestDirection = direction;
                }
            }

            return bestDirection + 1;
        }

        private static int ResolvePlacementScore(
            int seed,
            int defenseBand,
            int regionId,
            HexCoordinates coordinates)
        {
            unchecked
            {
                var value = seed;
                value = value * 397 ^ defenseBand;
                value = value * 397 ^ regionId;
                value = value * 397 ^ coordinates.Q;
                value = value * 397 ^ coordinates.R;
                return value & int.MaxValue;
            }
        }

        private static int ResolveUsage(IReadOnlyDictionary<int, int> usage, TrapSlot slot)
        {
            return usage.TryGetValue(ResolveZoneKey(slot), out var count) ? count : 0;
        }

        private static int ResolveTypeUsage(
            IReadOnlyDictionary<int, int> usage,
            HexCastleTrapType trapType,
            TrapSlot slot)
        {
            return usage.TryGetValue(ResolveTypeZoneKey(trapType, slot), out var count) ? count : 0;
        }

        private static int ResolveZoneKey(TrapSlot slot)
        {
            return slot.DefenseBand * 10 + slot.RegionId;
        }

        private static int ResolveTypeZoneKey(HexCastleTrapType trapType, TrapSlot slot)
        {
            return ((int)trapType + 1) * 1000 + ResolveZoneKey(slot);
        }

        private static void Increment(IDictionary<int, int> usage, int key)
        {
            usage.TryGetValue(key, out var count);
            usage[key] = count + 1;
        }
    }
}
