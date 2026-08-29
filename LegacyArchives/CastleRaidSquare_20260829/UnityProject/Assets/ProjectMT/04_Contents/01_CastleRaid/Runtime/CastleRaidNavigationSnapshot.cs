using System;
using System.Collections.Generic;
using ProjectMT.Contents.CastleRaid.Generation;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    public enum CastleRaidRoutePolicy // 논리 경로의 파괴 비용 성향
    {
        Balanced,
        WallBreaker,
        PalaceRush
    }

    public readonly struct CastleRaidRoutePlan
    {
        public CastleRaidRoutePlan(
            int routeId,
            int sectorId,
            Vector2Int startCell,
            Vector2Int goalCell,
            string firstObstaclePlacementId,
            int firstObstacleDefenseLayer,
            float estimatedCost)
            : this(
                routeId,
                sectorId,
                startCell,
                startCell,
                goalCell,
                firstObstaclePlacementId,
                firstObstacleDefenseLayer,
                estimatedCost)
        {
        }

        public CastleRaidRoutePlan(
            int routeId,
            int sectorId,
            Vector2Int startCell,
            Vector2Int approachCell,
            Vector2Int goalCell,
            string firstObstaclePlacementId,
            int firstObstacleDefenseLayer,
            float estimatedCost)
        {
            RouteId = routeId;
            SectorId = sectorId;
            StartCell = startCell;
            ApproachCell = approachCell;
            GoalCell = goalCell;
            FirstObstaclePlacementId = firstObstaclePlacementId ?? string.Empty;
            FirstObstacleDefenseLayer = firstObstacleDefenseLayer;
            EstimatedCost = estimatedCost;
        }

        public int RouteId { get; }
        public int SectorId { get; }
        public Vector2Int StartCell { get; }
        public Vector2Int ApproachCell { get; }
        public Vector2Int GoalCell { get; }
        public string FirstObstaclePlacementId { get; }
        public int FirstObstacleDefenseLayer { get; }
        public float EstimatedCost { get; }
        public bool IsValid => RouteId != 0 && !float.IsInfinity(EstimatedCost);
        public bool HasFirstObstacle => !string.IsNullOrWhiteSpace(FirstObstaclePlacementId);
    }

    public sealed class CastleRaidNavigationSnapshot // 생성 후보를 전투 중 읽기 전용 격자로 보존
    {
        internal sealed class PlacementState
        {
            public string PlacementId;
            public CastlePlacementKind Kind;
            public RectInt Bounds;
            public int DefenseLayer;
            public float EffectiveHealth;
            public CastleTarget Target;
            public bool Alive;

            public bool IsBreakable => Kind == CastlePlacementKind.Wall ||
                                       Kind == CastlePlacementKind.Building ||
                                       Kind == CastlePlacementKind.DefenseBuilding ||
                                       Kind == CastlePlacementKind.LootBuilding;
        }

        private readonly PlacementState[] placements;
        private readonly int[] placementByCell;
        private readonly Dictionary<string, int> placementIndexById =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly float originX;
        private readonly float originZ;
        private readonly Vector3 worldOrigin;
        private int palacePlacementIndex = -1;

        public CastleRaidNavigationSnapshot(
            int width,
            int height,
            float logicalCellSize,
            IReadOnlyList<CastlePlacementData> sourcePlacements,
            IReadOnlyList<CastleTarget> runtimeTargets = null,
            Vector3 stageWorldOrigin = default)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            CellSize = Mathf.Max(0.1f, logicalCellSize);
            worldOrigin = stageWorldOrigin;
            originX = worldOrigin.x - Width * CellSize * 0.5f;
            originZ = worldOrigin.z - Height * CellSize * 0.5f;
            placementByCell = new int[Width * Height];
            Array.Fill(placementByCell, -1);

            var targetById = BuildTargetMap(runtimeTargets);
            var placementCount = sourcePlacements == null ? 0 : sourcePlacements.Count;
            placements = new PlacementState[placementCount];
            for (var index = 0; index < placementCount; index++)
            {
                var source = sourcePlacements[index];
                if (source == null)
                {
                    continue;
                }

                var placementId = source.PlacementId ?? string.Empty;
                var state = new PlacementState
                {
                    PlacementId = placementId,
                    Kind = source.Kind,
                    Bounds = source.Bounds,
                    DefenseLayer = source.Kind == CastlePlacementKind.Wall
                        ? Mathf.Max(0, source.WallDefenseLayer)
                        : -1,
                    EffectiveHealth = Mathf.Max(1f, source.EffectiveHealth),
                    Alive = IsBreakable(source.Kind)
                };
                targetById.TryGetValue(placementId, out state.Target);
                placements[index] = state;
                if (!string.IsNullOrWhiteSpace(placementId))
                {
                    placementIndexById[placementId] = index;
                }

                if (source.Kind == CastlePlacementKind.Palace)
                {
                    palacePlacementIndex = index;
                }

                if (state.IsBreakable)
                {
                    StoreOccupiedCells(index, state.Bounds);
                }
            }
        }

        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }
        public int TopologyVersion { get; private set; }
        public bool HasPalace => palacePlacementIndex >= 0;

        public Vector3 PalaceWorldPosition => palacePlacementIndex < 0 || placements[palacePlacementIndex] == null
            ? worldOrigin
            : CellBoundsCenterToWorld(placements[palacePlacementIndex].Bounds);

        public void ResetRuntimeState()
        {
            var changed = false;
            for (var index = 0; index < placements.Length; index++)
            {
                var placement = placements[index];
                if (placement == null || !placement.IsBreakable)
                {
                    continue;
                }

                var alive = placement.Target == null || placement.Target.IsAlive;
                changed |= placement.Alive != alive;
                placement.Alive = alive;
            }

            if (changed || TopologyVersion == 0)
            {
                TopologyVersion++;
            }
        }

        public bool NotifyDestroyed(string placementId)
        {
            if (string.IsNullOrWhiteSpace(placementId) ||
                !placementIndexById.TryGetValue(placementId, out var placementIndex) ||
                placements[placementIndex] == null || !placements[placementIndex].Alive)
            {
                return false;
            }

            placements[placementIndex].Alive = false;
            TopologyVersion++;
            return true;
        }

        public bool TryResolveTarget(string placementId, out CastleTarget target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(placementId) ||
                !placementIndexById.TryGetValue(placementId, out var placementIndex))
            {
                return false;
            }

            var placement = placements[placementIndex];
            target = placement?.Target;
            return target != null;
        }

        public bool TryWorldToCell(Vector3 worldPosition, out Vector2Int cell)
        {
            var x = Mathf.FloorToInt((worldPosition.x - originX) / CellSize);
            var z = Mathf.FloorToInt((worldPosition.z - originZ) / CellSize);
            cell = new Vector2Int(
                Mathf.Clamp(x, 0, Width - 1),
                Mathf.Clamp(z, 0, Height - 1));
            return x >= 0 && x < Width && z >= 0 && z < Height;
        }

        public Vector3 CellToWorld(Vector2Int cell)
        {
            return new Vector3(
                originX + (Mathf.Clamp(cell.x, 0, Width - 1) + 0.5f) * CellSize,
                worldOrigin.y,
                originZ + (Mathf.Clamp(cell.y, 0, Height - 1) + 0.5f) * CellSize);
        }

        internal int CellIndex(Vector2Int cell)
        {
            return cell.y * Width + cell.x;
        }

        internal Vector2Int CellFromIndex(int index)
        {
            return new Vector2Int(index % Width, index / Width);
        }

        internal bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
        }

        internal PlacementState PlacementAt(int cellIndex)
        {
            if (cellIndex < 0 || cellIndex >= placementByCell.Length)
            {
                return null;
            }

            var placementIndex = placementByCell[cellIndex];
            return placementIndex < 0 || placementIndex >= placements.Length
                ? null
                : placements[placementIndex];
        }

        internal IEnumerable<int> EnumeratePalaceCellIndices()
        {
            if (palacePlacementIndex < 0 || placements[palacePlacementIndex] == null)
            {
                yield break;
            }

            var bounds = placements[palacePlacementIndex].Bounds;
            for (var z = bounds.yMin; z < bounds.yMax; z++)
            {
                for (var x = bounds.xMin; x < bounds.xMax; x++)
                {
                    var cell = new Vector2Int(x, z);
                    if (IsInside(cell))
                    {
                        yield return CellIndex(cell);
                    }
                }
            }
        }

        private static Dictionary<string, CastleTarget> BuildTargetMap(IReadOnlyList<CastleTarget> runtimeTargets)
        {
            var result = new Dictionary<string, CastleTarget>(StringComparer.Ordinal);
            if (runtimeTargets == null)
            {
                return result;
            }

            for (var index = 0; index < runtimeTargets.Count; index++)
            {
                var target = runtimeTargets[index];
                if (target != null && !string.IsNullOrWhiteSpace(target.PlacementId))
                {
                    result[target.PlacementId] = target;
                }
            }

            return result;
        }

        private void StoreOccupiedCells(int placementIndex, RectInt bounds)
        {
            for (var z = bounds.yMin; z < bounds.yMax; z++)
            {
                for (var x = bounds.xMin; x < bounds.xMax; x++)
                {
                    var cell = new Vector2Int(x, z);
                    if (IsInside(cell))
                    {
                        placementByCell[CellIndex(cell)] = placementIndex;
                    }
                }
            }
        }

        private Vector3 CellBoundsCenterToWorld(RectInt bounds)
        {
            return new Vector3(
                originX + (bounds.xMin + bounds.width * 0.5f) * CellSize,
                worldOrigin.y,
                originZ + (bounds.yMin + bounds.height * 0.5f) * CellSize);
        }

        private static bool IsBreakable(CastlePlacementKind kind)
        {
            return kind == CastlePlacementKind.Wall ||
                   kind == CastlePlacementKind.Building ||
                   kind == CastlePlacementKind.DefenseBuilding ||
                   kind == CastlePlacementKind.LootBuilding;
        }
    }

    public sealed class CastleRaidRoutePlanner // 공유 비용장을 사용해 왕궁까지의 첫 장애물만 찾는다
    {
        private static readonly Vector2Int[] Directions =
        {
            new Vector2Int(0, 1),
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, -1),
            new Vector2Int(-1, 1)
        };

        private sealed class RouteField
        {
            public readonly float[] Costs;
            public readonly int[] NextCells;
            public int TopologyVersion = -1;

            public RouteField(int cellCount)
            {
                Costs = new float[cellCount];
                NextCells = new int[cellCount];
            }
        }

        private readonly struct HeapNode
        {
            public HeapNode(int cellIndex, float cost)
            {
                CellIndex = cellIndex;
                Cost = cost;
            }

            public int CellIndex { get; }
            public float Cost { get; }
        }

        private readonly CastleRaidNavigationSnapshot snapshot;
        private readonly RouteField[] fields;
        private readonly List<HeapNode> heap;

        public CastleRaidRoutePlanner(CastleRaidNavigationSnapshot navigationSnapshot)
        {
            snapshot = navigationSnapshot ?? throw new ArgumentNullException(nameof(navigationSnapshot));
            var cellCount = snapshot.Width * snapshot.Height;
            fields = new[]
            {
                new RouteField(cellCount),
                new RouteField(cellCount),
                new RouteField(cellCount)
            };
            heap = new List<HeapNode>(cellCount * 2);
        }

        public int FieldBuildCount { get; private set; }

        public void Invalidate()
        {
            for (var index = 0; index < fields.Length; index++)
            {
                fields[index].TopologyVersion = -1;
            }
        }

        public bool TryResolveRoute(
            Vector3 worldPosition,
            CastleRaidRoutePolicy policy,
            out CastleRaidRoutePlan plan)
        {
            plan = default;
            if (!snapshot.HasPalace)
            {
                return false;
            }

            snapshot.TryWorldToCell(worldPosition, out var startCell); // 외곽 입력은 가장 가까운 논리 셀로 붙인다
            var field = ResolveField(policy);
            var startIndex = snapshot.CellIndex(startCell);
            if (float.IsInfinity(field.Costs[startIndex]))
            {
                return false;
            }

            var firstObstacleId = string.Empty;
            var firstObstacleLayer = -1;
            var currentIndex = startIndex;
            var goalIndex = startIndex;
            var approachIndex = startIndex;
            var guardLimit = snapshot.Width * snapshot.Height;
            for (var guard = 0; guard < guardLimit; guard++)
            {
                var placement = snapshot.PlacementAt(currentIndex);
                if (placement != null && placement.Alive && placement.IsBreakable)
                {
                    firstObstacleId = placement.PlacementId;
                    firstObstacleLayer = placement.DefenseLayer;
                    break;
                }

                var nextIndex = field.NextCells[currentIndex];
                goalIndex = currentIndex;
                if (nextIndex < 0 || nextIndex == currentIndex)
                {
                    break;
                }

                approachIndex = currentIndex;
                currentIndex = nextIndex;
            }

            var sectorId = ResolveSector(worldPosition, snapshot.PalaceWorldPosition);
            var routeId = string.IsNullOrWhiteSpace(firstObstacleId)
                ? 1000 + sectorId
                : StableRouteId(firstObstacleId);
            plan = new CastleRaidRoutePlan(
                routeId,
                sectorId,
                startCell,
                snapshot.CellFromIndex(approachIndex),
                snapshot.CellFromIndex(goalIndex),
                firstObstacleId,
                firstObstacleLayer,
                field.Costs[startIndex]);
            return true;
        }

        private RouteField ResolveField(CastleRaidRoutePolicy policy)
        {
            var field = fields[Mathf.Clamp((int)policy, 0, fields.Length - 1)];
            if (field.TopologyVersion == snapshot.TopologyVersion)
            {
                return field;
            }

            BuildField(field, policy);
            field.TopologyVersion = snapshot.TopologyVersion;
            FieldBuildCount++;
            return field;
        }

        private void BuildField(RouteField field, CastleRaidRoutePolicy policy)
        {
            heap.Clear();
            for (var index = 0; index < field.Costs.Length; index++)
            {
                field.Costs[index] = float.PositiveInfinity;
                field.NextCells[index] = -1;
            }

            foreach (var palaceIndex in snapshot.EnumeratePalaceCellIndices())
            {
                field.Costs[palaceIndex] = 0f;
                PushHeap(new HeapNode(palaceIndex, 0f));
            }

            while (heap.Count > 0)
            {
                var current = PopHeap();
                if (current.Cost > field.Costs[current.CellIndex] + 0.0001f)
                {
                    continue;
                }

                var currentCell = snapshot.CellFromIndex(current.CellIndex);
                for (var directionIndex = 0; directionIndex < Directions.Length; directionIndex++)
                {
                    var direction = Directions[directionIndex];
                    var predecessor = currentCell - direction;
                    if (!snapshot.IsInside(predecessor) ||
                        directionIndex >= 4 && !CanUseDiagonal(predecessor, currentCell))
                    {
                        continue;
                    }

                    var predecessorIndex = snapshot.CellIndex(predecessor);
                    var movementCost = directionIndex < 4 ? snapshot.CellSize : snapshot.CellSize * 1.41421356f;
                    var candidateCost = current.Cost + movementCost + ResolveEntryCost(current.CellIndex, policy);
                    if (candidateCost + 0.0001f >= field.Costs[predecessorIndex])
                    {
                        continue;
                    }

                    field.Costs[predecessorIndex] = candidateCost;
                    field.NextCells[predecessorIndex] = current.CellIndex;
                    PushHeap(new HeapNode(predecessorIndex, candidateCost));
                }
            }
        }

        private bool CanUseDiagonal(Vector2Int from, Vector2Int to)
        {
            var sideA = new Vector2Int(from.x, to.y);
            var sideB = new Vector2Int(to.x, from.y);
            return IsOpenCornerCell(sideA) && IsOpenCornerCell(sideB);
        }

        private bool IsOpenCornerCell(Vector2Int cell)
        {
            if (!snapshot.IsInside(cell))
            {
                return false;
            }

            var placement = snapshot.PlacementAt(snapshot.CellIndex(cell));
            return placement == null || !placement.Alive || !placement.IsBreakable;
        }

        private float ResolveEntryCost(int cellIndex, CastleRaidRoutePolicy policy)
        {
            var placement = snapshot.PlacementAt(cellIndex);
            if (placement == null || !placement.Alive || !placement.IsBreakable)
            {
                return 0f;
            }

            var currentHealth = placement.Target != null && placement.Target.Health != null && placement.Target.IsAlive
                ? placement.Target.Health.CurrentHealth
                : placement.EffectiveHealth;
            var footprintArea = Mathf.Max(1, placement.Bounds.width * placement.Bounds.height);
            var weight = ResolveDestructionWeight(placement.Kind, policy);
            return 0.25f + currentHealth / 10f * weight / footprintArea;
        }

        private static float ResolveDestructionWeight(CastlePlacementKind kind, CastleRaidRoutePolicy policy)
        {
            var wall = kind == CastlePlacementKind.Wall;
            switch (policy)
            {
                case CastleRaidRoutePolicy.WallBreaker:
                    return wall ? 0.45f : 1.2f;
                case CastleRaidRoutePolicy.PalaceRush:
                    return wall ? 0.75f : 1.65f;
                default:
                    return wall ? 1f : 1.2f;
            }
        }

        private void PushHeap(HeapNode node)
        {
            var index = heap.Count;
            heap.Add(node);
            while (index > 0)
            {
                var parent = (index - 1) / 2;
                if (heap[parent].Cost <= node.Cost)
                {
                    break;
                }

                heap[index] = heap[parent];
                index = parent;
            }

            heap[index] = node;
        }

        private HeapNode PopHeap()
        {
            var result = heap[0];
            var lastIndex = heap.Count - 1;
            var last = heap[lastIndex];
            heap.RemoveAt(lastIndex);
            if (heap.Count == 0)
            {
                return result;
            }

            var index = 0;
            while (true)
            {
                var left = index * 2 + 1;
                if (left >= heap.Count)
                {
                    break;
                }

                var right = left + 1;
                var child = right < heap.Count && heap[right].Cost < heap[left].Cost ? right : left;
                if (heap[child].Cost >= last.Cost)
                {
                    break;
                }

                heap[index] = heap[child];
                index = child;
            }

            heap[index] = last;
            return result;
        }

        private static int ResolveSector(Vector3 worldPosition, Vector3 palacePosition)
        {
            var direction = worldPosition - palacePosition;
            var angle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
            if (angle < 0f)
            {
                angle += 360f;
            }

            return Mathf.FloorToInt((angle + 22.5f) / 45f) % 8;
        }

        private static int StableRouteId(string value)
        {
            unchecked
            {
                var hash = (int)2166136261;
                for (var index = 0; index < value.Length; index++)
                {
                    hash = (hash ^ value[index]) * 16777619;
                }

                hash &= int.MaxValue;
                return hash == 0 ? 1 : hash;
            }
        }
    }
}
