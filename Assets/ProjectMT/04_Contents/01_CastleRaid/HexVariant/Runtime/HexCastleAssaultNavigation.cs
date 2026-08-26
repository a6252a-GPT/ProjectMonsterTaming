using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    public enum HexCastleAssaultRoutePolicy
    {
        Balanced = 0,
        ResourceRaider = 1,
        TurretHunter = 2,
        WallBreaker = 3,
        DirectAdvance = 4
    }

    public sealed class HexCastleAssaultRoutePlan
    {
        internal HexCastleAssaultRoutePlan(
            IReadOnlyList<HexCoordinates> path,
            float totalCost,
            HexCoordinates destinationApproach,
            HexCoordinates firstObstacle,
            HexCoordinates firstObstacleApproach,
            bool hasFirstObstacle,
            int routeId,
            int sectorId,
            int topologyVersion)
        {
            Path = path ?? Array.Empty<HexCoordinates>();
            TotalCost = totalCost;
            DestinationApproach = destinationApproach;
            FirstObstacle = firstObstacle;
            FirstObstacleApproach = firstObstacleApproach;
            HasFirstObstacle = hasFirstObstacle;
            RouteId = routeId;
            SectorId = sectorId;
            TopologyVersion = topologyVersion;
        }

        public IReadOnlyList<HexCoordinates> Path { get; }
        public float TotalCost { get; }
        public HexCoordinates DestinationApproach { get; }
        public HexCoordinates FirstObstacle { get; }
        public HexCoordinates FirstObstacleApproach { get; }
        public bool HasFirstObstacle { get; }
        public int RouteId { get; }
        public int SectorId { get; }
        public int TopologyVersion { get; }
        public bool IsComplete => Path.Count > 0;
    }

    public sealed class HexCastleAssaultNavigationSnapshot // 파괴 상태를 반영한 Hex 전략 비용장
    {
        private readonly struct FieldKey : IEquatable<FieldKey>
        {
            public FieldKey(
                HexCastleAssaultRoutePolicy policy,
                int expectedDefenseLayer,
                int damageBand,
                int speedBand,
                int topologyVersion)
            {
                Policy = policy;
                ExpectedDefenseLayer = expectedDefenseLayer;
                DamageBand = damageBand;
                SpeedBand = speedBand;
                TopologyVersion = topologyVersion;
            }

            public HexCastleAssaultRoutePolicy Policy { get; }
            public int ExpectedDefenseLayer { get; }
            public int DamageBand { get; }
            public int SpeedBand { get; }
            public int TopologyVersion { get; }

            public bool Equals(FieldKey other)
            {
                return Policy == other.Policy && ExpectedDefenseLayer == other.ExpectedDefenseLayer &&
                       DamageBand == other.DamageBand && SpeedBand == other.SpeedBand &&
                       TopologyVersion == other.TopologyVersion;
            }

            public override bool Equals(object obj)
            {
                return obj is FieldKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = (int)Policy;
                    hash = hash * 397 ^ ExpectedDefenseLayer;
                    hash = hash * 397 ^ DamageBand;
                    hash = hash * 397 ^ SpeedBand;
                    hash = hash * 397 ^ TopologyVersion;
                    return hash;
                }
            }
        }

        private readonly struct QueueNode
        {
            public QueueNode(HexCoordinates coordinates, float cost)
            {
                Coordinates = coordinates;
                Cost = cost;
            }

            public HexCoordinates Coordinates { get; }
            public float Cost { get; }
        }

        private sealed class MinimumHeap
        {
            private readonly List<QueueNode> values = new List<QueueNode>();

            public int Count => values.Count;

            public void Push(QueueNode node)
            {
                values.Add(node);
                var index = values.Count - 1;
                while (index > 0)
                {
                    var parent = (index - 1) / 2;
                    if (values[parent].Cost <= values[index].Cost)
                    {
                        break;
                    }

                    (values[parent], values[index]) = (values[index], values[parent]);
                    index = parent;
                }
            }

            public QueueNode Pop()
            {
                var result = values[0];
                var last = values.Count - 1;
                values[0] = values[last];
                values.RemoveAt(last);
                var index = 0;
                while (true)
                {
                    var left = index * 2 + 1;
                    if (left >= values.Count)
                    {
                        break;
                    }

                    var right = left + 1;
                    var child = right < values.Count && values[right].Cost < values[left].Cost
                        ? right
                        : left;
                    if (values[index].Cost <= values[child].Cost)
                    {
                        break;
                    }

                    (values[index], values[child]) = (values[child], values[index]);
                    index = child;
                }

                return result;
            }
        }

        private readonly IReadOnlyDictionary<HexCoordinates, HexCastleCellRuntime> cells;
        private readonly HashSet<HexCoordinates> palaceFootprint;
        private readonly HashSet<HexCoordinates> palaceApproaches;
        private readonly Dictionary<FieldKey, Dictionary<HexCoordinates, float>> fields =
            new Dictionary<FieldKey, Dictionary<HexCoordinates, float>>();
        private readonly float cellTravelDistance;

        public HexCastleAssaultNavigationSnapshot(
            IReadOnlyDictionary<HexCoordinates, HexCastleCellRuntime> runtimeCells,
            float cellSize)
        {
            cells = runtimeCells ?? throw new ArgumentNullException(nameof(runtimeCells));
            cellTravelDistance = Mathf.Max(0.1f, cellSize) * 1.7320508f;
            palaceFootprint = new HashSet<HexCoordinates>(cells
                .Where(pair => pair.Value != null && pair.Value.Kind == HexCastleCellKind.Palace)
                .Select(pair => pair.Key));
            palaceApproaches = new HashSet<HexCoordinates>();
            foreach (var palace in palaceFootprint)
            {
                for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
                {
                    var candidate = palace.Neighbor(direction);
                    if (!palaceFootprint.Contains(candidate) && cells.ContainsKey(candidate))
                    {
                        palaceApproaches.Add(candidate);
                    }
                }
            }

            if (palaceFootprint.Count == 0 || palaceApproaches.Count == 0)
            {
                throw new InvalidOperationException("Hex 왕궁 점유 Cell과 외곽 공격 Cell이 필요합니다.");
            }
        }

        public int CachedFieldCount => fields.Count;
        public IReadOnlyCollection<HexCoordinates> PalaceFootprint => palaceFootprint;
        public IReadOnlyCollection<HexCoordinates> PalaceApproaches => palaceApproaches;

        public void Invalidate()
        {
            fields.Clear();
        }

        public bool TryResolveRoute(
            HexCoordinates start,
            HexCastleAssaultRoutePolicy policy,
            int expectedDefenseLayer,
            float damagePerSecond,
            float moveSpeed,
            int topologyVersion,
            out HexCastleAssaultRoutePlan plan)
        {
            plan = null;
            if (!cells.ContainsKey(start) || palaceFootprint.Contains(start))
            {
                return false;
            }

            var damageBand = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1f, damagePerSecond) / 10f));
            var speedBand = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0.1f, moveSpeed) * 4f));
            var key = new FieldKey(
                policy,
                Mathf.Max(0, expectedDefenseLayer),
                damageBand,
                speedBand,
                topologyVersion);
            if (!fields.TryGetValue(key, out var field))
            {
                field = BuildReverseField(
                    policy,
                    key.ExpectedDefenseLayer,
                    damageBand * 10f,
                    speedBand / 4f);
                fields.Add(key, field);
            }

            if (!field.ContainsKey(start))
            {
                return false;
            }

            var path = ReconstructPath(
                start,
                field,
                policy,
                key.ExpectedDefenseLayer,
                damageBand * 10f,
                speedBand / 4f);
            if (path.Count == 0)
            {
                return false;
            }

            var hasObstacle = false;
            var obstacle = default(HexCoordinates);
            var obstacleApproach = start;
            for (var index = 1; index < path.Count; index++)
            {
                if (!cells.TryGetValue(path[index], out var cell) || cell == null || !cell.IsBlocked)
                {
                    continue;
                }

                hasObstacle = true;
                obstacle = path[index];
                obstacleApproach = path[index - 1];
                break;
            }

            var sector = ResolveSector(start);
            var routeAnchor = hasObstacle ? obstacle : path[path.Count - 1];
            var routeId = ResolveRouteId(sector, routeAnchor);
            plan = new HexCastleAssaultRoutePlan(
                path,
                field[start],
                path[path.Count - 1],
                obstacle,
                obstacleApproach,
                hasObstacle,
                routeId,
                sector,
                topologyVersion);
            return true;
        }

        public bool TryResolveOpenApproachRoute(
            HexCoordinates start,
            HexCoordinates target,
            int maximumRangeCells,
            out IReadOnlyList<HexCoordinates> route,
            out HexCoordinates approach)
        {
            return TryResolveOpenApproachRoute(
                start,
                target,
                maximumRangeCells,
                null,
                out route,
                out approach);
        }

        public bool TryResolveOpenApproachRoute(
            HexCoordinates start,
            HexCoordinates target,
            int maximumRangeCells,
            IReadOnlyCollection<HexCoordinates> excludedApproaches,
            out IReadOnlyList<HexCoordinates> route,
            out HexCoordinates approach)
        {
            return TryResolveOpenApproachRoute(
                start,
                target,
                maximumRangeCells,
                excludedApproaches,
                null,
                out route,
                out approach);
        }

        public bool TryResolveOpenApproachRoute(
            HexCoordinates start,
            HexCoordinates target,
            int maximumRangeCells,
            IReadOnlyCollection<HexCoordinates> excludedApproaches,
            Predicate<HexCoordinates> approachPredicate,
            out IReadOnlyList<HexCoordinates> route,
            out HexCoordinates approach)
        {
            route = Array.Empty<HexCoordinates>();
            approach = start;
            var maximumRange = Mathf.Max(1, maximumRangeCells);
            var candidates = cells.Keys
                .Where(value => value.DistanceTo(target) <= maximumRange &&
                                !palaceFootprint.Contains(value) &&
                                (excludedApproaches == null || !excludedApproaches.Contains(value)) &&
                                (approachPredicate == null || approachPredicate(value)) &&
                                cells[value] != null && cells[value].CanTraverse(HexCastleTraversalFaction.Assault))
                .OrderBy(value => start.DistanceTo(value))
                .ThenBy(value => value)
                .ToArray();
            for (var index = 0; index < candidates.Length; index++)
            {
                var candidateRoute = new HexRoutePlanner().FindTraversalRoute(
                    cells,
                    start,
                    candidates[index],
                    HexCastleTraversalFaction.Assault);
                if (candidateRoute.Count == 0)
                {
                    continue;
                }

                route = candidateRoute;
                approach = candidates[index];
                return true;
            }

            return false;
        }

        public bool TryResolveOpenFollowRoute(
            HexCoordinates start,
            HexCoordinates target,
            int stopRangeCells,
            out IReadOnlyList<HexCoordinates> route,
            out HexCoordinates approach)
        {
            route = Array.Empty<HexCoordinates>();
            approach = start;
            if (!cells.TryGetValue(target, out var targetCell) || targetCell == null ||
                !targetCell.CanTraverse(HexCastleTraversalFaction.Assault))
            {
                return false;
            }

            var fullRoute = new HexRoutePlanner().FindTraversalRoute(
                cells,
                start,
                target,
                HexCastleTraversalFaction.Assault);
            if (fullRoute.Count == 0)
            {
                return false;
            }

            var stopIndex = Mathf.Max(0, fullRoute.Count - 1 - Mathf.Max(1, stopRangeCells));
            route = fullRoute.Take(stopIndex + 1).ToArray();
            approach = route[route.Count - 1];
            return true;
        }

        private Dictionary<HexCoordinates, float> BuildReverseField(
            HexCastleAssaultRoutePolicy policy,
            int expectedDefenseLayer,
            float damagePerSecond,
            float moveSpeed)
        {
            var result = new Dictionary<HexCoordinates, float>();
            var heap = new MinimumHeap();
            foreach (var goal in palaceApproaches)
            {
                if (!CanUseCell(goal, expectedDefenseLayer))
                {
                    continue;
                }

                result[goal] = 0f;
                heap.Push(new QueueNode(goal, 0f));
            }

            while (heap.Count > 0)
            {
                var current = heap.Pop();
                if (!result.TryGetValue(current.Coordinates, out var currentCost) ||
                    current.Cost > currentCost + 0.0001f)
                {
                    continue;
                }

                var entryCost = ResolveEntryCost(
                    current.Coordinates,
                    policy,
                    expectedDefenseLayer,
                    damagePerSecond,
                    moveSpeed);
                if (float.IsPositiveInfinity(entryCost))
                {
                    continue;
                }

                for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
                {
                    var predecessor = current.Coordinates.Neighbor(direction);
                    if (!CanUseCell(predecessor, expectedDefenseLayer) || palaceFootprint.Contains(predecessor))
                    {
                        continue;
                    }

                    var candidate = currentCost + entryCost;
                    if (result.TryGetValue(predecessor, out var known) && candidate >= known - 0.0001f)
                    {
                        continue;
                    }

                    result[predecessor] = candidate;
                    heap.Push(new QueueNode(predecessor, candidate));
                }
            }

            return result;
        }

        private IReadOnlyList<HexCoordinates> ReconstructPath(
            HexCoordinates start,
            IReadOnlyDictionary<HexCoordinates, float> field,
            HexCastleAssaultRoutePolicy policy,
            int expectedDefenseLayer,
            float damagePerSecond,
            float moveSpeed)
        {
            var result = new List<HexCoordinates> { start };
            var visited = new HashSet<HexCoordinates> { start };
            var current = start;
            var guard = cells.Count + 1;
            while (!palaceApproaches.Contains(current) && guard-- > 0)
            {
                var found = false;
                var best = default(HexCoordinates);
                var bestCost = float.PositiveInfinity;
                for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
                {
                    var neighbor = current.Neighbor(direction);
                    if (!field.TryGetValue(neighbor, out var remaining) ||
                        !CanUseCell(neighbor, expectedDefenseLayer) || palaceFootprint.Contains(neighbor))
                    {
                        continue;
                    }

                    var entry = ResolveEntryCost(
                        neighbor,
                        policy,
                        expectedDefenseLayer,
                        damagePerSecond,
                        moveSpeed);
                    var candidate = entry + remaining;
                    if (candidate >= bestCost - 0.0001f)
                    {
                        continue;
                    }

                    best = neighbor;
                    bestCost = candidate;
                    found = true;
                }

                if (!found || !visited.Add(best))
                {
                    return Array.Empty<HexCoordinates>();
                }

                current = best;
                result.Add(current);
            }

            return palaceApproaches.Contains(current) ? result : Array.Empty<HexCoordinates>();
        }

        private bool CanUseCell(HexCoordinates coordinates, int expectedDefenseLayer)
        {
            if (!cells.TryGetValue(coordinates, out var cell) || cell == null ||
                palaceFootprint.Contains(coordinates))
            {
                return false;
            }

            if (!cell.IsBlocked)
            {
                return true;
            }

            if (!cell.IsDamageable || !cell.IsAlive)
            {
                return false;
            }

            return cell.WallRole == HexCastleWallRole.Partition || !IsRingWall(cell) ||
                   expectedDefenseLayer > 0 && cell.DefenseLayer <= expectedDefenseLayer;
        }

        private float ResolveEntryCost(
            HexCoordinates coordinates,
            HexCastleAssaultRoutePolicy policy,
            int expectedDefenseLayer,
            float damagePerSecond,
            float moveSpeed)
        {
            if (!CanUseCell(coordinates, expectedDefenseLayer))
            {
                return float.PositiveInfinity;
            }

            var travel = cellTravelDistance / Mathf.Max(0.1f, moveSpeed);
            var cell = cells[coordinates];
            if (!cell.IsBlocked)
            {
                return travel;
            }

            var destruction = cell.CurrentHealth / Mathf.Max(1f, damagePerSecond);
            return travel + destruction * ResolveDestructionWeight(cell, policy);
        }

        private static float ResolveDestructionWeight(
            HexCastleCellRuntime cell,
            HexCastleAssaultRoutePolicy policy)
        {
            var isWall = cell.Kind == HexCastleCellKind.Wall || cell.Kind == HexCastleCellKind.Tower ||
                         cell.Kind == HexCastleCellKind.Gate;
            var isTurret = cell.BuildingRole == HexCastleBuildingRole.Turret;
            switch (policy)
            {
                case HexCastleAssaultRoutePolicy.ResourceRaider:
                    return isWall ? 1.35f : 0.55f;
                case HexCastleAssaultRoutePolicy.TurretHunter:
                    return isTurret ? 0.45f : isWall ? 1.15f : 1f;
                case HexCastleAssaultRoutePolicy.WallBreaker:
                    return isWall ? 0.45f : 1.2f;
                case HexCastleAssaultRoutePolicy.DirectAdvance:
                    return isWall ? 0.75f : 1.65f;
                default:
                    return isWall ? 1f : 1.2f;
            }
        }

        private static bool IsRingWall(HexCastleCellRuntime cell)
        {
            return cell != null && cell.DefenseLayer > 0 &&
                   cell.WallRole != HexCastleWallRole.None &&
                   cell.WallRole != HexCastleWallRole.Partition;
        }

        private static int ResolveSector(HexCoordinates coordinates)
        {
            var point = coordinates.ToWorld(1f);
            if (point.sqrMagnitude <= 0.0001f)
            {
                return 0;
            }

            point.Normalize();
            var best = 0;
            var bestDot = float.NegativeInfinity;
            for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
            {
                var axis = HexCoordinates.Directions[direction].ToWorld(1f).normalized;
                var dot = Vector3.Dot(point, axis);
                if (dot > bestDot)
                {
                    best = direction;
                    bestDot = dot;
                }
            }

            return best;
        }

        private static int ResolveRouteId(int sector, HexCoordinates anchor)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + sector;
                hash = hash * 31 + anchor.Q;
                hash = hash * 31 + anchor.R;
                return hash;
            }
        }
    }
}
