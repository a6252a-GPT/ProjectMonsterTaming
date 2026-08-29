using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid.Generation
{
    public enum CastleGridRotation // 템플릿의 직교 회전
    {
        Degree0,
        Degree90,
        Degree180,
        Degree270
    }

    public static class CastleSpatialContract // 정식 성 생성의 G1 공간 계약
    {
        public const int BattlefieldSize = 50;
        public const int BuildAreaSize = 44;
        public const int DeploymentMargin = 3;
        public const int PalaceSize = 4;
        public const int MinimumBuildingSize = 1;
        public const int MaximumBuildingSize = 4;

        public static RectInt BattlefieldBounds => new RectInt(0, 0, BattlefieldSize, BattlefieldSize);
        public static RectInt BuildableBounds => new RectInt(
            DeploymentMargin,
            DeploymentMargin,
            BuildAreaSize,
            BuildAreaSize);
        public static RectInt PalaceBounds => CenteredBounds(PalaceSize, PalaceSize);

        public static RectInt CenteredBounds(int width, int height)
        {
            if (width < 1 || height < 1 || width > BattlefieldSize || height > BattlefieldSize)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "중앙 점유 크기가 전장 범위를 벗어났습니다.");
            }

            return new RectInt(
                (BattlefieldSize - width) / 2,
                (BattlefieldSize - height) / 2,
                width,
                height);
        }

        public static Vector2Int ToBattlefieldCell(Vector2Int buildAreaCell)
        {
            if (buildAreaCell.x < 0 || buildAreaCell.y < 0 ||
                buildAreaCell.x >= BuildAreaSize || buildAreaCell.y >= BuildAreaSize)
            {
                throw new ArgumentOutOfRangeException(nameof(buildAreaCell), "건설 영역 좌표는 0~43이어야 합니다.");
            }

            return buildAreaCell + new Vector2Int(DeploymentMargin, DeploymentMargin);
        }

        public static Vector2Int ToBuildAreaCell(Vector2Int battlefieldCell)
        {
            if (!BuildableBounds.Contains(battlefieldCell))
            {
                throw new ArgumentOutOfRangeException(nameof(battlefieldCell), "전장 좌표가 건설 영역 밖입니다.");
            }

            return battlefieldCell - new Vector2Int(DeploymentMargin, DeploymentMargin);
        }

        public static Vector3 ToWorldCenter(RectInt footprint, float cellWorldSize)
        {
            if (cellWorldSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellWorldSize), "셀 월드 크기는 0보다 커야 합니다.");
            }

            return new Vector3(
                (footprint.xMin + footprint.width * 0.5f) * cellWorldSize,
                0f,
                (footprint.yMin + footprint.height * 0.5f) * cellWorldSize);
        }

        public static Vector2Int RotatedSize(Vector2Int size, CastleGridRotation rotation)
        {
            ValidateSize(size);
            return rotation == CastleGridRotation.Degree90 || rotation == CastleGridRotation.Degree270
                ? new Vector2Int(size.y, size.x)
                : size;
        }

        public static Vector2Int RotateLocalCell(
            Vector2Int cell,
            Vector2Int sourceSize,
            CastleGridRotation rotation)
        {
            ValidateSize(sourceSize);
            if (cell.x < 0 || cell.y < 0 || cell.x >= sourceSize.x || cell.y >= sourceSize.y)
            {
                throw new ArgumentOutOfRangeException(nameof(cell), "회전할 로컬 셀이 원본 점유 영역 밖입니다.");
            }

            switch (rotation)
            {
                case CastleGridRotation.Degree0:
                    return cell;
                case CastleGridRotation.Degree90:
                    return new Vector2Int(sourceSize.y - 1 - cell.y, cell.x);
                case CastleGridRotation.Degree180:
                    return new Vector2Int(sourceSize.x - 1 - cell.x, sourceSize.y - 1 - cell.y);
                case CastleGridRotation.Degree270:
                    return new Vector2Int(cell.y, sourceSize.x - 1 - cell.x);
                default:
                    throw new ArgumentOutOfRangeException(nameof(rotation), rotation, "지원하지 않는 회전입니다.");
            }
        }

        public static bool Contains(RectInt outer, RectInt inner)
        {
            return inner.width > 0 && inner.height > 0 &&
                   inner.xMin >= outer.xMin && inner.yMin >= outer.yMin &&
                   inner.xMax <= outer.xMax && inner.yMax <= outer.yMax;
        }

        public static bool Overlaps(RectInt left, RectInt right)
        {
            return left.xMin < right.xMax && left.xMax > right.xMin &&
                   left.yMin < right.yMax && left.yMax > right.yMin;
        }

        public static float BoundaryDistance(RectInt left, RectInt right)
        {
            var deltaX = Mathf.Max(0, Mathf.Max(left.xMin - right.xMax, right.xMin - left.xMax));
            var deltaZ = Mathf.Max(0, Mathf.Max(left.yMin - right.yMax, right.yMin - left.yMax));
            return Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
        }

        public static bool TryValidateFootprint(CastlePlacementData placement, out string issueCode)
        {
            if (placement == null)
            {
                issueCode = "OFF_GRID_PLACEMENT";
                return false;
            }

            switch (placement.Kind)
            {
                case CastlePlacementKind.Wall:
                    if (placement.Width != 1 || placement.Height != 1)
                    {
                        issueCode = "INVALID_WALL_FOOTPRINT";
                        return false;
                    }

                    break;
                case CastlePlacementKind.Palace:
                    if (placement.Width != PalaceSize || placement.Height != PalaceSize)
                    {
                        issueCode = "INVALID_PALACE_PLACEMENT";
                        return false;
                    }

                    break;
                case CastlePlacementKind.Building:
                case CastlePlacementKind.DefenseBuilding:
                case CastlePlacementKind.LootBuilding:
                    if (!IsSupportedBuildingFootprint(placement.Width, placement.Height))
                    {
                        issueCode = "INVALID_BUILDING_FOOTPRINT";
                        return false;
                    }

                    break;
            }

            issueCode = string.Empty;
            return true;
        }

        public static bool CreatesNoDeployZone(CastlePlacementKind kind)
        {
            return kind == CastlePlacementKind.Wall ||
                   kind == CastlePlacementKind.Building ||
                   kind == CastlePlacementKind.DefenseBuilding ||
                   kind == CastlePlacementKind.Palace ||
                   kind == CastlePlacementKind.LootBuilding;
        }

        private static bool IsSupportedBuildingFootprint(int width, int height)
        {
            return width == height && width >= MinimumBuildingSize && width <= MaximumBuildingSize;
        }

        private static void ValidateSize(Vector2Int size)
        {
            if (size.x < 1 || size.y < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "점유 크기는 1 이상이어야 합니다.");
            }
        }
    }

    public sealed class CastleDeploymentMask // 구조물 배치 금지와 내부 구멍 조회 결과
    {
        private readonly bool[,] noDeployCells;

        private CastleDeploymentMask(int width, int height)
        {
            Width = width;
            Height = height;
            noDeployCells = new bool[width, height];
        }

        public int Width { get; }
        public int Height { get; }

        public static CastleDeploymentMask Create(
            int width,
            int height,
            IEnumerable<CastlePlacementData> placements)
        {
            if (width < 1 || height < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "배치 마스크 크기는 1 이상이어야 합니다.");
            }

            var result = new CastleDeploymentMask(width, height);
            if (placements == null)
            {
                return result;
            }

            foreach (var placement in placements)
            {
                if (placement == null || !CastleSpatialContract.CreatesNoDeployZone(placement.Kind))
                {
                    continue;
                }

                result.MarkExpanded(placement.Bounds, 1);
            }

            return result;
        }

        public bool IsNoDeploy(Vector2Int cell)
        {
            return Contains(cell) && noDeployCells[cell.x, cell.y];
        }

        public bool IsDeployable(Vector2Int cell)
        {
            return Contains(cell) && !noDeployCells[cell.x, cell.y];
        }

        public IReadOnlyList<Vector2Int> FindDeployableCells(RectInt inspectionBounds)
        {
            var result = new List<Vector2Int>();
            var minX = Mathf.Clamp(inspectionBounds.xMin, 0, Width);
            var maxX = Mathf.Clamp(inspectionBounds.xMax, 0, Width);
            var minZ = Mathf.Clamp(inspectionBounds.yMin, 0, Height);
            var maxZ = Mathf.Clamp(inspectionBounds.yMax, 0, Height);
            for (var x = minX; x < maxX; x++)
            {
                for (var z = minZ; z < maxZ; z++)
                {
                    if (!noDeployCells[x, z])
                    {
                        result.Add(new Vector2Int(x, z));
                    }
                }
            }

            return result;
        }

        private void MarkExpanded(RectInt bounds, int expansion)
        {
            var minX = Mathf.Clamp(bounds.xMin - expansion, 0, Width);
            var maxX = Mathf.Clamp(bounds.xMax + expansion, 0, Width);
            var minZ = Mathf.Clamp(bounds.yMin - expansion, 0, Height);
            var maxZ = Mathf.Clamp(bounds.yMax + expansion, 0, Height);
            for (var x = minX; x < maxX; x++)
            {
                for (var z = minZ; z < maxZ; z++)
                {
                    noDeployCells[x, z] = true;
                }
            }
        }

        private bool Contains(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
        }
    }
}
