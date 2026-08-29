using System;
using System.Collections.Generic;
using ProjectMT.Contents.CastleRaid.Generation;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    public static class CastleDeploymentAreaResolver // 성 밖에서 이어지는 실제 배치 셀을 계산한다
    {
        public const int WallClearanceCells = 1;

        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.left,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.up
        };

        public static IReadOnlyCollection<Vector2Int> ResolveExteriorCells(
            CastleGenerationCandidate candidate,
            RectInt displayBounds,
            int wallClearanceCells = WallClearanceCells)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (displayBounds.width <= 0 || displayBounds.height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(displayBounds), "배치 영역 크기는 1칸 이상이어야 합니다.");
            }

            wallClearanceCells = Mathf.Max(0, wallClearanceCells);
            var blocked = new HashSet<Vector2Int>();
            foreach (var placement in candidate.Placements)
            {
                AddBlockedRect(blocked, displayBounds, placement.Bounds);
                if (placement.Kind == CastlePlacementKind.Wall && wallClearanceCells > 0)
                {
                    var bounds = placement.Bounds;
                    var clearanceBounds = new RectInt(
                        bounds.xMin - wallClearanceCells,
                        bounds.yMin - wallClearanceCells,
                        bounds.width + wallClearanceCells * 2,
                        bounds.height + wallClearanceCells * 2);
                    AddBlockedRect(blocked, displayBounds, clearanceBounds); // 대각선도 한 칸 띄운다
                }
            }

            var exterior = new HashSet<Vector2Int>();
            var frontier = new Queue<Vector2Int>();
            for (var x = displayBounds.xMin; x < displayBounds.xMax; x++)
            {
                TryEnqueue(new Vector2Int(x, displayBounds.yMin), displayBounds, blocked, exterior, frontier);
                TryEnqueue(new Vector2Int(x, displayBounds.yMax - 1), displayBounds, blocked, exterior, frontier);
            }

            for (var z = displayBounds.yMin + 1; z < displayBounds.yMax - 1; z++)
            {
                TryEnqueue(new Vector2Int(displayBounds.xMin, z), displayBounds, blocked, exterior, frontier);
                TryEnqueue(new Vector2Int(displayBounds.xMax - 1, z), displayBounds, blocked, exterior, frontier);
            }

            while (frontier.Count > 0)
            {
                var cell = frontier.Dequeue();
                foreach (var direction in Directions)
                {
                    TryEnqueue(cell + direction, displayBounds, blocked, exterior, frontier);
                }
            }

            return exterior;
        }

        private static void AddBlockedRect(HashSet<Vector2Int> blocked, RectInt displayBounds, RectInt bounds)
        {
            var minimumX = Mathf.Max(displayBounds.xMin, bounds.xMin);
            var maximumX = Mathf.Min(displayBounds.xMax, bounds.xMax);
            var minimumZ = Mathf.Max(displayBounds.yMin, bounds.yMin);
            var maximumZ = Mathf.Min(displayBounds.yMax, bounds.yMax);
            for (var z = minimumZ; z < maximumZ; z++)
            {
                for (var x = minimumX; x < maximumX; x++)
                {
                    blocked.Add(new Vector2Int(x, z));
                }
            }
        }

        private static void TryEnqueue(
            Vector2Int cell,
            RectInt bounds,
            ISet<Vector2Int> blocked,
            ISet<Vector2Int> visited,
            Queue<Vector2Int> frontier)
        {
            if (!bounds.Contains(cell) || blocked.Contains(cell) || !visited.Add(cell))
            {
                return;
            }

            frontier.Enqueue(cell);
        }
    }
}
