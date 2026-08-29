using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectMT.Contents.CastleRaid.Generation;
using UnityEngine;

namespace ProjectMT.Tests.EditMode
{
    public sealed class CastleSpatialContractTests
    {
        [Test]
        public void FrozenGridContract_Uses50By50WithCentered44By44BuildArea()
        {
            Assert.That(CastleSpatialContract.BattlefieldBounds, Is.EqualTo(new RectInt(0, 0, 50, 50)));
            Assert.That(CastleSpatialContract.BuildableBounds, Is.EqualTo(new RectInt(3, 3, 44, 44)));
            Assert.That(CastleSpatialContract.PalaceBounds, Is.EqualTo(new RectInt(23, 23, 4, 4)));
            Assert.That(CastleSpatialContract.ToBattlefieldCell(Vector2Int.zero), Is.EqualTo(new Vector2Int(3, 3)));
            Assert.That(CastleSpatialContract.ToBattlefieldCell(new Vector2Int(43, 43)), Is.EqualTo(new Vector2Int(46, 46)));
            Assert.That(CastleSpatialContract.ToBuildAreaCell(new Vector2Int(46, 46)), Is.EqualTo(new Vector2Int(43, 43)));
        }

        [Test]
        public void WorldCenter_UsesFootprintCenterInsteadOfModelPivot()
        {
            var center = CastleSpatialContract.ToWorldCenter(CastleSpatialContract.PalaceBounds, 2f);

            Assert.That(center, Is.EqualTo(new Vector3(50f, 0f, 50f)));
        }

        [Test]
        public void Rotation_PreservesEveryLocalCellAndSwapsRectangularSize()
        {
            var sourceSize = new Vector2Int(3, 2);
            var rotatedSize = CastleSpatialContract.RotatedSize(sourceSize, CastleGridRotation.Degree90);
            var rotated = new HashSet<Vector2Int>();
            for (var x = 0; x < sourceSize.x; x++)
            {
                for (var z = 0; z < sourceSize.y; z++)
                {
                    rotated.Add(CastleSpatialContract.RotateLocalCell(
                        new Vector2Int(x, z),
                        sourceSize,
                        CastleGridRotation.Degree90));
                }
            }

            Assert.That(rotatedSize, Is.EqualTo(new Vector2Int(2, 3)));
            Assert.That(rotated.Count, Is.EqualTo(6));
            Assert.That(rotated.All(cell =>
                cell.x >= 0 && cell.y >= 0 && cell.x < rotatedSize.x && cell.y < rotatedSize.y), Is.True);
        }

        [Test]
        public void FootprintContract_AcceptsWallsAndSupportedSquareBuildings()
        {
            AssertFootprintValid(Placement("wall", CastlePlacementKind.Wall, 1));
            for (var size = 1; size <= 4; size++)
            {
                AssertFootprintValid(Placement($"building_{size}", CastlePlacementKind.Building, size));
            }

            AssertFootprintValid(Placement("palace", CastlePlacementKind.Palace, 4));
        }

        [Test]
        public void FootprintContract_RejectsWrongWallBuildingAndPalaceSizes()
        {
            AssertFootprintInvalid(Placement("wall", CastlePlacementKind.Wall, 2), "INVALID_WALL_FOOTPRINT");
            AssertFootprintInvalid(Placement("building", CastlePlacementKind.Building, 5), "INVALID_BUILDING_FOOTPRINT");
            AssertFootprintInvalid(Placement("palace", CastlePlacementKind.Palace, 3), "INVALID_PALACE_PLACEMENT");
        }

        [Test]
        public void BoundaryAndOverlap_UseFullFootprints()
        {
            var building = new RectInt(10, 10, 4, 4);
            var adjacent = new RectInt(14, 11, 1, 1);
            var oneCellGap = new RectInt(15, 11, 1, 1);

            Assert.That(CastleSpatialContract.Overlaps(building, adjacent), Is.False);
            Assert.That(CastleSpatialContract.BoundaryDistance(building, adjacent), Is.Zero);
            Assert.That(CastleSpatialContract.BoundaryDistance(building, oneCellGap), Is.EqualTo(1f));
            Assert.That(CastleSpatialContract.Overlaps(building, new RectInt(13, 13, 2, 2)), Is.True);
        }

        [Test]
        public void NoDeployMask_ExpandsStructuresByOneCellIncludingDiagonals()
        {
            var placements = new[]
            {
                Placement("wall", CastlePlacementKind.Wall, 1, 10, 10),
                Placement("building", CastlePlacementKind.Building, 3, 20, 20),
                Placement("defender", CastlePlacementKind.Defender, 1, 30, 30)
            };
            var mask = CastleDeploymentMask.Create(50, 50, placements);

            Assert.That(mask.IsNoDeploy(new Vector2Int(9, 9)), Is.True);
            Assert.That(mask.IsNoDeploy(new Vector2Int(11, 11)), Is.True);
            Assert.That(mask.IsNoDeploy(new Vector2Int(19, 19)), Is.True);
            Assert.That(mask.IsNoDeploy(new Vector2Int(23, 23)), Is.True);
            Assert.That(mask.IsDeployable(new Vector2Int(18, 18)), Is.True);
            Assert.That(mask.IsDeployable(new Vector2Int(30, 30)), Is.True);
        }

        [Test]
        public void InternalDeployHole_IsReportedUntilProtectedInteriorIsFilled()
        {
            var walls = BuildWallRing(new RectInt(10, 10, 5, 5));
            var openMask = CastleDeploymentMask.Create(50, 50, walls);

            Assert.That(
                openMask.FindDeployableCells(new RectInt(10, 10, 5, 5)),
                Is.EquivalentTo(new[] { new Vector2Int(12, 12) }));

            walls.Add(Placement("core", CastlePlacementKind.Building, 3, 11, 11));
            var filledMask = CastleDeploymentMask.Create(50, 50, walls);
            Assert.That(filledMask.FindDeployableCells(new RectInt(10, 10, 5, 5)), Is.Empty);
        }

        [Test]
        public void BuildableBounds_IncludeOnlyCells3Through46()
        {
            Assert.That(CastleSpatialContract.Contains(
                CastleSpatialContract.BuildableBounds,
                new RectInt(3, 3, 1, 1)), Is.True);
            Assert.That(CastleSpatialContract.Contains(
                CastleSpatialContract.BuildableBounds,
                new RectInt(46, 46, 1, 1)), Is.True);
            Assert.That(CastleSpatialContract.Contains(
                CastleSpatialContract.BuildableBounds,
                new RectInt(2, 3, 1, 1)), Is.False);
            Assert.That(CastleSpatialContract.Contains(
                CastleSpatialContract.BuildableBounds,
                new RectInt(47, 46, 1, 1)), Is.False);
        }

        private static CastlePlacementData Placement(
            string id,
            CastlePlacementKind kind,
            int size,
            int x = 3,
            int z = 3)
        {
            return new CastlePlacementData(
                id,
                "test",
                "test",
                kind,
                CastleLootKind.None,
                x,
                z,
                size,
                size,
                kind == CastlePlacementKind.Wall ? 1 : 0,
                100f,
                0);
        }

        private static List<CastlePlacementData> BuildWallRing(RectInt bounds)
        {
            var result = new List<CastlePlacementData>();
            for (var x = bounds.xMin; x < bounds.xMax; x++)
            {
                result.Add(Placement($"wall_bottom_{x}", CastlePlacementKind.Wall, 1, x, bounds.yMin));
                result.Add(Placement($"wall_top_{x}", CastlePlacementKind.Wall, 1, x, bounds.yMax - 1));
            }

            for (var z = bounds.yMin + 1; z < bounds.yMax - 1; z++)
            {
                result.Add(Placement($"wall_left_{z}", CastlePlacementKind.Wall, 1, bounds.xMin, z));
                result.Add(Placement($"wall_right_{z}", CastlePlacementKind.Wall, 1, bounds.xMax - 1, z));
            }

            return result;
        }

        private static void AssertFootprintValid(CastlePlacementData placement)
        {
            Assert.That(CastleSpatialContract.TryValidateFootprint(placement, out var code), Is.True, code);
            Assert.That(code, Is.Empty);
        }

        private static void AssertFootprintInvalid(CastlePlacementData placement, string expectedCode)
        {
            Assert.That(CastleSpatialContract.TryValidateFootprint(placement, out var code), Is.False);
            Assert.That(code, Is.EqualTo(expectedCode));
        }
    }
}
