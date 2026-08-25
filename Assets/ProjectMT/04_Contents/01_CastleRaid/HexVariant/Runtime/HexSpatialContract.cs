using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    public static class HexSpatialContract
    {
        // 아래 고정 50m 상수는 기존 Hex 도구를 단계적으로 이식하기 위한 호환 API다.
        // 정식 Foundation/Board 생성은 ResolveBoardRadius와 EnumerateBoardCells만 사용한다.
        public const float BattlefieldSize = 50f;
        public const float BuildAreaSize = 44f;
        public const float DeploymentMargin = 3f;
        public const float PalaceWorldSize = 4f;
        public const float PalaceCoreWorldSize = 12f;
        public const float CellArea = 1f;
        public const float TileFlatWidth = 2f;
        public const float TileHeight = 1f;
        public const float TileTopY = 0f;
        public const float TileBottomY = -1f;
        public const int MinimumDeploymentRings = 2;

        public static readonly float CellOuterRadius = TileFlatWidth / Mathf.Sqrt(3f);
        public static readonly float CellWidth = TileFlatWidth;
        public static readonly float CellDepth = CellOuterRadius * 2f;
        public static readonly float RowPitch = CellOuterRadius * 1.5f;
        public static readonly float CellInRadius = TileFlatWidth * 0.5f;
        public static readonly Rect BattlefieldBounds = CreateCenteredBounds(BattlefieldSize);
        public static readonly Rect BuildBounds = CreateCenteredBounds(BuildAreaSize);

        public static Vector3 ToWorld(HexCoordinates coordinates)
        {
            return coordinates.ToWorld(CellOuterRadius);
        }

        public static HexCoordinates FromWorld(Vector3 position)
        {
            return HexCoordinates.FromWorld(position, CellOuterRadius);
        }

        public static Vector3 GetEdgeMidpoint(int direction)
        {
            var normalized = PositiveModulo(direction, HexCoordinates.Directions.Length);
            return ToWorld(HexCoordinates.Directions[normalized]) * 0.5f;
        }

        public static bool ContainsBattlefieldCenter(HexCoordinates coordinates)
        {
            return Contains(BattlefieldBounds, ToWorld(coordinates));
        }

        public static bool ContainsBuildCell(HexCoordinates coordinates)
        {
            return GetWorldCorners(coordinates).All(corner => Contains(BuildBounds, corner));
        }

        public static IEnumerable<HexCoordinates> EnumerateBattlefieldCells()
        {
            var halfDepth = BattlefieldSize * 0.5f;
            var minimumR = Mathf.FloorToInt(-halfDepth / RowPitch) - 1;
            var maximumR = Mathf.CeilToInt(halfDepth / RowPitch) + 1;
            for (var r = minimumR; r <= maximumR; r++)
            {
                var rowOffset = r * 0.5f;
                var minimumQ = Mathf.FloorToInt(BattlefieldBounds.xMin / CellWidth - rowOffset) - 1;
                var maximumQ = Mathf.CeilToInt(BattlefieldBounds.xMax / CellWidth - rowOffset) + 1;
                for (var q = minimumQ; q <= maximumQ; q++)
                {
                    var coordinates = new HexCoordinates(q, r);
                    if (ContainsBattlefieldCenter(coordinates))
                    {
                        yield return coordinates;
                    }
                }
            }
        }

        public static Vector3[] GetWorldCorners(HexCoordinates coordinates)
        {
            var center = ToWorld(coordinates);
            var corners = new Vector3[6];
            for (var index = 0; index < corners.Length; index++)
            {
                var angle = Mathf.Deg2Rad * (30f + index * 60f);
                corners[index] = center + new Vector3(
                    Mathf.Cos(angle) * CellOuterRadius,
                    0f,
                    Mathf.Sin(angle) * CellOuterRadius);
            }

            return corners;
        }

        public static int ResolveBoardRadius(
            IEnumerable<HexCoordinates> occupiedCoordinates,
            int deploymentRings = MinimumDeploymentRings)
        {
            if (occupiedCoordinates == null)
            {
                throw new ArgumentNullException(nameof(occupiedCoordinates));
            }

            var values = occupiedCoordinates.ToArray();
            if (values.Length == 0)
            {
                throw new InvalidOperationException("Board Radius를 계산할 점유 Cell이 없습니다.");
            }

            return values.Max(value => value.DistanceFromOrigin) + Mathf.Max(0, deploymentRings);
        }

        public static IEnumerable<HexCoordinates> EnumerateBoardCells(int boardRadius)
        {
            if (boardRadius < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(boardRadius));
            }

            return HexCoordinates.EnumerateRadius(boardRadius);
        }

        public static int ResolveBoardCellCount(int boardRadius)
        {
            if (boardRadius < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(boardRadius));
            }

            return 1 + 3 * boardRadius * (boardRadius + 1);
        }

        public static Bounds ResolveStructureBounds(IEnumerable<HexCoordinates> coordinates)
        {
            if (coordinates == null)
            {
                throw new ArgumentNullException(nameof(coordinates));
            }

            var initialized = false;
            var bounds = default(Bounds);
            foreach (var coordinate in coordinates)
            {
                foreach (var corner in GetWorldCorners(coordinate))
                {
                    if (!initialized)
                    {
                        bounds = new Bounds(corner, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(corner);
                    }
                }
            }

            if (!initialized)
            {
                throw new InvalidOperationException("Bounds를 계산할 육각 Cell이 없습니다.");
            }

            return bounds;
        }

        public static Rect ResolveSquarePreviewBounds(Bounds structureBounds)
        {
            var minimum = new Vector2(
                structureBounds.min.x - DeploymentMargin,
                structureBounds.min.z - DeploymentMargin);
            var maximum = new Vector2(
                structureBounds.max.x + DeploymentMargin,
                structureBounds.max.z + DeploymentMargin);
            var width = maximum.x - minimum.x;
            var height = maximum.y - minimum.y;
            var side = Mathf.Max(width, height);
            var center = (minimum + maximum) * 0.5f;
            var half = side * 0.5f;
            return ClampSquare(
                Rect.MinMaxRect(center.x - half, center.y - half, center.x + half, center.y + half),
                BattlefieldBounds);
        }

        private static Rect ClampSquare(Rect square, Rect limits)
        {
            var side = Mathf.Min(square.width, Mathf.Min(limits.width, limits.height));
            var xMin = Mathf.Clamp(square.center.x - side * 0.5f, limits.xMin, limits.xMax - side);
            var yMin = Mathf.Clamp(square.center.y - side * 0.5f, limits.yMin, limits.yMax - side);
            return new Rect(xMin, yMin, side, side);
        }

        private static Rect CreateCenteredBounds(float size)
        {
            var half = size * 0.5f;
            return Rect.MinMaxRect(-half, -half, half, half);
        }

        private static bool Contains(Rect bounds, Vector3 point)
        {
            const float epsilon = 0.0001f;
            return point.x >= bounds.xMin - epsilon && point.x <= bounds.xMax + epsilon &&
                   point.z >= bounds.yMin - epsilon && point.z <= bounds.yMax + epsilon;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
