using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectMT.Contents.CastleRaidHex
{
    public sealed class HexRouteResult
    {
        internal HexRouteResult(
            IEnumerable<HexCoordinates> path,
            float totalCost,
            int wallCellsToBreak,
            int structuresToBreak,
            IEnumerable<int> crossedDefenseLayers)
        {
            Path = path.ToArray();
            TotalCost = totalCost;
            WallCellsToBreak = wallCellsToBreak;
            StructuresToBreak = structuresToBreak;
            CrossedDefenseLayers = crossedDefenseLayers.OrderBy(value => value).ToArray();
        }

        public IReadOnlyList<HexCoordinates> Path { get; }
        public float TotalCost { get; }
        public int WallCellsToBreak { get; }
        public int StructuresToBreak { get; }
        public IReadOnlyList<int> CrossedDefenseLayers { get; }
        public bool IsComplete => Path.Count > 0;
    }

    public sealed class HexRoutePlanner
    {
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
                var lastIndex = values.Count - 1;
                values[0] = values[lastIndex];
                values.RemoveAt(lastIndex);
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

        public HexRouteResult FindMinimumBreachRoute(HexCastleLayout layout, HexCoordinates start)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (!layout.TryGetCell(start, out var startCell) ||
                startCell.Kind != HexCastleCellKind.Deployment)
            {
                throw new ArgumentOutOfRangeException(nameof(start), "시작점은 외곽 배치 셀이어야 합니다.");
            }

            var costs = new Dictionary<HexCoordinates, float> { [start] = 0f };
            var previous = new Dictionary<HexCoordinates, HexCoordinates>();
            var heap = new MinimumHeap();
            heap.Push(new QueueNode(start, 0f));
            HexCoordinates? destination = null;

            while (heap.Count > 0)
            {
                var current = heap.Pop();
                if (!costs.TryGetValue(current.Coordinates, out var bestCost) ||
                    current.Cost > bestCost + 0.0001f)
                {
                    continue;
                }

                var currentCell = layout.Cells[current.Coordinates];
                if (currentCell.Kind == HexCastleCellKind.Palace &&
                    current.Coordinates == new HexCoordinates(0, 0))
                {
                    destination = current.Coordinates;
                    break;
                }

                for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
                {
                    var neighborCoordinates = current.Coordinates.Neighbor(direction);
                    if (!layout.TryGetCell(neighborCoordinates, out var neighbor))
                    {
                        continue;
                    }

                    var candidateCost = current.Cost + ResolveEntryCost(neighbor);
                    if (costs.TryGetValue(neighborCoordinates, out var knownCost) &&
                        candidateCost >= knownCost - 0.0001f)
                    {
                        continue;
                    }

                    costs[neighborCoordinates] = candidateCost;
                    previous[neighborCoordinates] = current.Coordinates;
                    heap.Push(new QueueNode(neighborCoordinates, candidateCost));
                }
            }

            if (!destination.HasValue)
            {
                return new HexRouteResult(Array.Empty<HexCoordinates>(), float.PositiveInfinity, 0, 0, Array.Empty<int>());
            }

            var path = ReconstructPath(start, destination.Value, previous);
            var crossedLayers = new HashSet<int>();
            var wallCells = 0;
            var structures = 0;
            foreach (var coordinates in path)
            {
                var cell = layout.Cells[coordinates];
                if (cell.IsWallPathCell)
                {
                    if (cell.DefenseLayer > 0)
                    {
                        wallCells++;
                        crossedLayers.Add(cell.DefenseLayer);
                    }
                    else
                    {
                        structures++;
                    }
                }
                else if (cell.IsBreakable)
                {
                    structures++;
                }
            }

            return new HexRouteResult(path, costs[destination.Value], wallCells, structures, crossedLayers);
        }

        public IReadOnlyList<HexCoordinates> FindTraversalRoute(
            HexCastleLayout layout,
            HexCoordinates start,
            HexCoordinates destination,
            HexCastleTraversalFaction faction)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (!layout.TryGetCell(start, out var startCell) ||
                !layout.TryGetCell(destination, out var destinationCell) ||
                !startCell.CanTraverseWithoutBreaking(faction) ||
                !destinationCell.CanTraverseWithoutBreaking(faction))
            {
                return Array.Empty<HexCoordinates>();
            }

            var queue = new Queue<HexCoordinates>();
            var visited = new HashSet<HexCoordinates> { start };
            var previous = new Dictionary<HexCoordinates, HexCoordinates>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == destination)
                {
                    return ReconstructPath(start, destination, previous);
                }

                var currentCell = layout.Cells[current];
                for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
                {
                    var neighborCoordinates = current.Neighbor(direction);
                    if (visited.Contains(neighborCoordinates) ||
                        !layout.TryGetCell(neighborCoordinates, out var neighborCell) ||
                        !CanTraverseStep(currentCell, neighborCell, direction, faction))
                    {
                        continue;
                    }

                    visited.Add(neighborCoordinates);
                    previous[neighborCoordinates] = current;
                    queue.Enqueue(neighborCoordinates);
                }
            }

            return Array.Empty<HexCoordinates>();
        }

        public IReadOnlyList<HexCoordinates> FindTraversalRoute(
            IReadOnlyDictionary<HexCoordinates, HexCastleCellRuntime> runtimeCells,
            HexCoordinates start,
            HexCoordinates destination,
            HexCastleTraversalFaction faction)
        {
            if (runtimeCells == null)
            {
                throw new ArgumentNullException(nameof(runtimeCells));
            }

            if (!runtimeCells.TryGetValue(start, out var startCell) || startCell == null ||
                !runtimeCells.TryGetValue(destination, out var destinationCell) || destinationCell == null ||
                !startCell.CanTraverse(faction) || !destinationCell.CanTraverse(faction))
            {
                return Array.Empty<HexCoordinates>();
            }

            var queue = new Queue<HexCoordinates>();
            var visited = new HashSet<HexCoordinates> { start };
            var previous = new Dictionary<HexCoordinates, HexCoordinates>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == destination)
                {
                    return ReconstructPath(start, destination, previous);
                }

                var currentCell = runtimeCells[current];
                for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
                {
                    var neighborCoordinates = current.Neighbor(direction);
                    if (visited.Contains(neighborCoordinates) ||
                        !runtimeCells.TryGetValue(neighborCoordinates, out var neighborCell) ||
                        neighborCell == null ||
                        !CanTraverseStep(currentCell, neighborCell, direction, faction))
                    {
                        continue;
                    }

                    visited.Add(neighborCoordinates);
                    previous[neighborCoordinates] = current;
                    queue.Enqueue(neighborCoordinates);
                }
            }

            return Array.Empty<HexCoordinates>();
        }

        public IReadOnlyList<HexCoordinates> FindTraversalRouteWithSingleBlockerJump(
            IReadOnlyDictionary<HexCoordinates, HexCastleCellRuntime> runtimeCells,
            HexCoordinates start,
            HexCoordinates destination,
            HexCastleTraversalFaction faction,
            Func<HexCastleCellRuntime, bool> canJumpOver)
        {
            if (runtimeCells == null)
            {
                throw new ArgumentNullException(nameof(runtimeCells));
            }

            if (canJumpOver == null)
            {
                return FindTraversalRoute(runtimeCells, start, destination, faction);
            }

            if (!runtimeCells.TryGetValue(start, out var startCell) || startCell == null ||
                !runtimeCells.TryGetValue(destination, out var destinationCell) || destinationCell == null ||
                !startCell.CanTraverse(faction) || !destinationCell.CanTraverse(faction))
            {
                return Array.Empty<HexCoordinates>();
            }

            var queue = new Queue<HexCoordinates>();
            var visited = new HashSet<HexCoordinates> { start };
            var previous = new Dictionary<HexCoordinates, HexCoordinates>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == destination)
                {
                    return ReconstructPath(start, destination, previous);
                }

                var currentCell = runtimeCells[current];
                for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
                {
                    var neighborCoordinates = current.Neighbor(direction);
                    if (runtimeCells.TryGetValue(neighborCoordinates, out var neighborCell) &&
                        neighborCell != null &&
                        CanTraverseStep(currentCell, neighborCell, direction, faction))
                    {
                        Enqueue(neighborCoordinates, current);
                        continue;
                    }

                    if (neighborCell == null || !canJumpOver(neighborCell))
                    {
                        continue;
                    }

                    var landingCoordinates = neighborCoordinates.Neighbor(direction);
                    var entryDirection = (direction + 3) % HexCoordinates.Directions.Length;
                    if (!runtimeCells.TryGetValue(landingCoordinates, out var landingCell) || landingCell == null ||
                        !currentCell.CanEnterFrom(direction, faction) ||
                        !landingCell.CanEnterFrom(entryDirection, faction))
                    {
                        continue;
                    }

                    Enqueue(landingCoordinates, current);
                }
            }

            return Array.Empty<HexCoordinates>();

            void Enqueue(HexCoordinates coordinates, HexCoordinates from)
            {
                if (!visited.Add(coordinates))
                {
                    return;
                }

                previous[coordinates] = from;
                queue.Enqueue(coordinates);
            }
        }

        public static bool CanTraverseStep(
            HexCastleCell from,
            HexCastleCell to,
            int directionFromSource,
            HexCastleTraversalFaction faction)
        {
            if (from == null || to == null || directionFromSource < 0 ||
                directionFromSource >= HexCoordinates.Directions.Length)
            {
                return false;
            }

            var entryDirection = (directionFromSource + 3) % HexCoordinates.Directions.Length;
            return from.CanEnterFrom(directionFromSource, faction) &&
                   to.CanEnterFrom(entryDirection, faction);
        }

        public static bool CanTraverseStep(
            HexCastleCellRuntime from,
            HexCastleCellRuntime to,
            int directionFromSource,
            HexCastleTraversalFaction faction)
        {
            if (from == null || to == null || directionFromSource < 0 ||
                directionFromSource >= HexCoordinates.Directions.Length)
            {
                return false;
            }

            var entryDirection = (directionFromSource + 3) % HexCoordinates.Directions.Length;
            return from.CanEnterFrom(directionFromSource, faction) &&
                   to.CanEnterFrom(entryDirection, faction);
        }

        private static IReadOnlyList<HexCoordinates> ReconstructPath(
            HexCoordinates start,
            HexCoordinates destination,
            IReadOnlyDictionary<HexCoordinates, HexCoordinates> previous)
        {
            var result = new List<HexCoordinates> { destination };
            var current = destination;
            while (current != start)
            {
                if (!previous.TryGetValue(current, out current))
                {
                    return Array.Empty<HexCoordinates>();
                }

                result.Add(current);
            }

            result.Reverse();
            return result;
        }

        private static float ResolveEntryCost(HexCastleCell cell)
        {
            // 길막과 파괴 비용은 모델이 아니라 Cell의 현재 설계 HP가 결정한다.
            return cell.IsBreakable ? cell.HitPoints : 1f;
        }
    }
}
