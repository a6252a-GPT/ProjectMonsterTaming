using System;
using System.Linq;
using NUnit.Framework;
using ProjectMT.Contents.CastleRaid.Generation;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Tests.EditMode
{
    public sealed class CastleGenerationTests
    {
        private const string RulesPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/Data/Generation/CastleGenerationRules_Default.asset";

        [Test]
        public void DefaultRules_UseTenFormalThemesAndVariableCompartmentTemplates()
        {
            var rules = LoadRules();

            Assert.That(rules.TryValidate(out var error), Is.True, error);
            Assert.That(rules.RulesVersion, Is.EqualTo(18));
            Assert.That(rules.GridWidth, Is.EqualTo(50));
            Assert.That(rules.GridHeight, Is.EqualTo(50));
            Assert.That(rules.DeploymentMargin, Is.EqualTo(3));
            Assert.That(rules.BuildableBounds, Is.EqualTo(new RectInt(3, 3, 44, 44)));
            Assert.That(rules.PalaceSize, Is.EqualTo(4));
            Assert.That(rules.MinimumDistrictCount, Is.EqualTo(8));
            Assert.That(rules.MaximumDistrictCount, Is.EqualTo(60));
            Assert.That(
                rules.LayoutThemeRules.Select(value => value.Theme),
                Is.EquivalentTo(CastleGenerationRules.SupportedLayoutThemes));
            Assert.That(rules.Templates.Select(template => template.TemplateId), Is.EquivalentTo(new[]
            {
                "palace_core_12x12",
                "district_standard_6x6_10x10",
                "district_wide_8x6_14x10",
                "district_large_9x9_14x14",
                "district_outer_step_5x5_14x14",
                "district_hex_cell_7x5",
                "district_hex_queen_15x13",
                "district_petal_4x4_22x22",
                "district_geometric_4x4_30x30"
            }));
        }

        [TestCase(3, 39, 31)]
        [TestCase(4, 43, 39)]
        public void NestedDefensePreset_UsesRectangularSteppedSilhouette(
            int defenseLayerCount,
            int expectedLongSide,
            int expectedShortSide)
        {
            var candidate = new CastleGenerator().Generate(
                LoadRules(),
                20260816,
                CastleLayoutTheme.CentralCompartmentFortress,
                defenseLayerCount);
            var outerRing = candidate.Compartments
                .Where(value => value.DefenseRing == defenseLayerCount - 1)
                .ToArray();
            var minX = outerRing.Min(value => value.Bounds.xMin);
            var minZ = outerRing.Min(value => value.Bounds.yMin);
            var maxX = outerRing.Max(value => value.Bounds.xMax);
            var maxZ = outerRing.Max(value => value.Bounds.yMax);
            var sides = new[] { maxX - minX, maxZ - minZ }.OrderByDescending(value => value).ToArray();
            var northDepths = outerRing
                .Where(value => value.CompartmentId.Contains($"ring_{defenseLayerCount - 1:00}_n_"))
                .Select(value => value.Bounds.yMax)
                .Distinct()
                .Count();

            Assert.That(sides, Is.EqualTo(new[] { expectedLongSide, expectedShortSide }));
            Assert.That(northDepths, Is.GreaterThan(1));
        }

        [TestCase(2, 8, 10)]
        [TestCase(3, 20, 20)]
        [TestCase(4, 36, 36)]
        public void DefenseLayerPreset_BuildsRequestedNestedProtection(
            int defenseLayerCount,
            int minimumCompartments,
            int maximumCompartments)
        {
            var candidate = new CastleGenerator().Generate(
                LoadRules(),
                20260816,
                CastleLayoutTheme.CentralCompartmentFortress,
                defenseLayerCount);
            var regular = candidate.Compartments
                .Where(value => value.Role != CastleCompartmentRole.PalaceCore)
                .ToArray();

            Assert.That(candidate.Validation.IsValid, Is.True,
                string.Join("\n", candidate.Validation.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            Assert.That(candidate.RequestedDefenseLayerCount, Is.EqualTo(defenseLayerCount));
            Assert.That(candidate.ProtectionDepth, Is.EqualTo(defenseLayerCount));
            Assert.That(regular.Length, Is.InRange(minimumCompartments, maximumCompartments));
            Assert.That(candidate.PalaceExposedSideCount, Is.Zero);
            Assert.That(
                candidate.Compartments.Select(value => value.DefenseRing).Distinct().OrderBy(value => value),
                Is.EqualTo(Enumerable.Range(0, defenseLayerCount)));
        }

        [TestCase(CastleLayoutTheme.CentralCompartmentFortress, 8, 10, 2)]
        [TestCase(CastleLayoutTheme.DiamondRadialFortress, 8, 10, 2)]
        [TestCase(CastleLayoutTheme.HoneycombCompartmentFortress, 12, 12, 2)]
        [TestCase(CastleLayoutTheme.HexHoneycombFortress, 8, 12, 2)]
        [TestCase(CastleLayoutTheme.PetalBloomFortress, 8, 8, 2)]
        [TestCase(CastleLayoutTheme.CrystalMandalaFortress, 8, 8, 2)]
        [TestCase(CastleLayoutTheme.TwinSpiralFortress, 8, 8, 2)]
        [TestCase(CastleLayoutTheme.FractalBastionFortress, 8, 8, 2)]
        [TestCase(CastleLayoutTheme.VoronoiCrystalFortress, 8, 8, 2)]
        [TestCase(CastleLayoutTheme.IrisShutterFortress, 8, 8, 2)]
        public void EachLayoutTheme_BuildsOneProtectedCompartmentGraph(
            CastleLayoutTheme theme,
            int minimumCompartments,
            int maximumCompartments,
            int minimumProtectionDepth)
        {
            var candidate = new CastleGenerator().Generate(LoadRules(), 20260816, theme);
            var regularCount = candidate.Compartments.Count(value => value.Role != CastleCompartmentRole.PalaceCore);

            Assert.That(candidate.Validation.IsValid, Is.True,
                string.Join("\n", candidate.Validation.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            Assert.That(regularCount, Is.InRange(minimumCompartments, maximumCompartments));
            Assert.That(candidate.PalaceExposedSideCount, Is.Zero);
            Assert.That(candidate.ProtectionDepth, Is.GreaterThanOrEqualTo(minimumProtectionDepth));
            Assert.That(candidate.Compartments.All(value => value.ConnectedCompartmentIds.Count > 0), Is.True);
        }

        [Test]
        public void SharedWalls_AreMergedOnceAndCarryNeighborMasks()
        {
            var candidate = new CastleGenerator().Generate(
                LoadRules(),
                3317,
                CastleLayoutTheme.CentralCompartmentFortress);
            var walls = candidate.Placements.Where(value => value.Kind == CastlePlacementKind.Wall).ToArray();

            Assert.That(walls.Select(value => new Vector2Int(value.X, value.Z)).Distinct().Count(), Is.EqualTo(walls.Length));
            Assert.That(walls.Any(value => value.OwnerDistrictIds.Count > 1), Is.True);
            Assert.That(walls.Any(value => (value.WallNeighborMask & CastleWallNeighborMask.North) != 0), Is.True);
            Assert.That(walls.Any(value => (value.WallNeighborMask & CastleWallNeighborMask.East) != 0), Is.True);
        }

        [TestCase(CastleLayoutTheme.CentralCompartmentFortress)]
        [TestCase(CastleLayoutTheme.DiamondRadialFortress)]
        [TestCase(CastleLayoutTheme.HoneycombCompartmentFortress)]
        [TestCase(CastleLayoutTheme.HexHoneycombFortress)]
        [TestCase(CastleLayoutTheme.PetalBloomFortress)]
        [TestCase(CastleLayoutTheme.CrystalMandalaFortress)]
        [TestCase(CastleLayoutTheme.TwinSpiralFortress)]
        [TestCase(CastleLayoutTheme.FractalBastionFortress)]
        [TestCase(CastleLayoutTheme.VoronoiCrystalFortress)]
        [TestCase(CastleLayoutTheme.IrisShutterFortress)]
        public void CompletedWallNetwork_UsesOneTierPerClassifiedDefenseLine(CastleLayoutTheme theme)
        {
            var rules = LoadRules();
            var candidate = new CastleGenerator().Generate(rules, 20260816, theme);
            var walls = candidate.Placements.Where(value => value.Kind == CastlePlacementKind.Wall).ToArray();
            var lines = walls.GroupBy(value => value.WallLineId, StringComparer.Ordinal).ToArray();

            Assert.That(candidate.Validation.IsValid, Is.True,
                string.Join("\n", candidate.Validation.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            Assert.That(walls.All(value => !string.IsNullOrWhiteSpace(value.WallLineId)), Is.True);
            Assert.That(walls.All(value => value.WallBand != CastleWallBand.None), Is.True);
            Assert.That(lines.All(line => line.Select(value => value.WallTier).Distinct().Count() == 1), Is.True);
            Assert.That(lines.All(line => line.Select(value => value.WallBand).Distinct().Count() == 1), Is.True);
            Assert.That(lines.All(line => line.Select(value => value.WallDefenseLayer).Distinct().Count() == 1), Is.True);
            Assert.That(
                walls.Where(value => value.WallBand == CastleWallBand.OuterPerimeter),
                Is.Not.Empty.And.All.Matches<CastlePlacementData>(value =>
                    value.WallDefenseLayer == 0));
            Assert.That(
                walls.Where(value => value.WallBand == CastleWallBand.OuterPerimeter)
                    .Select(value => value.WallTier)
                    .Distinct().Count(),
                Is.EqualTo(1));
            Assert.That(
                walls.Where(value => value.WallBand == CastleWallBand.CoreDefense),
                Is.Not.Empty.And.All.Matches<CastlePlacementData>(value =>
                    value.OwnerDistrictIds.Contains("palace_core") && value.WallTier >= rules.PalaceWallTier));
            Assert.That(walls.Any(value => value.WallBand == CastleWallBand.Partition), Is.True);
        }

        [Test]
        public void CentralFortress_UsesVariableRectangularCompartmentSizes()
        {
            var candidate = new CastleGenerator().Generate(LoadRules(), 8012);
            var regular = candidate.Compartments
                .Where(value => value.Role != CastleCompartmentRole.PalaceCore)
                .ToArray();
            var sizes = regular.Select(value => value.Bounds.size).Distinct().ToArray();

            Assert.That(sizes.Length, Is.GreaterThanOrEqualTo(4));
            Assert.That(regular.Any(value => value.Bounds.width != value.Bounds.height), Is.True);
            Assert.That(regular.Any(value => value.Bounds.width > 7 || value.Bounds.height > 7), Is.True);
            Assert.That(
                candidate.Compartments.Single(value => value.Role == CastleCompartmentRole.PalaceCore).Bounds.size,
                Is.EqualTo(new Vector2Int(12, 12)));
        }

        [Test]
        public void CentralFortress_BatchMaintainsDenseConnectedSilhouette()
        {
            var rules = LoadRules();
            var generator = new CastleGenerator();

            foreach (var seed in Enumerable.Range(640, 64))
            {
                var candidate = generator.Generate(rules, seed);
                var regularCount = candidate.Compartments.Count(value =>
                    value.Role != CastleCompartmentRole.PalaceCore);

                Assert.That(candidate.Validation.IsValid, Is.True, $"seed={seed}");
                Assert.That(regularCount, Is.InRange(8, 10), $"seed={seed}");
                Assert.That(candidate.Compactness, Is.GreaterThanOrEqualTo(0.79f), $"seed={seed}");
                Assert.That(
                    candidate.Compartments.All(value => value.ConnectedCompartmentIds.Count > 0),
                    Is.True,
                    $"seed={seed}");
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void DiamondRadialFortress_UsesSteppedDiamondShellAndKeepsRequestedDepth(int defenseLayerCount)
        {
            var candidate = new CastleGenerator().Generate(
                LoadRules(),
                20260816,
                CastleLayoutTheme.DiamondRadialFortress,
                defenseLayerCount);
            var regular = candidate.Compartments
                .Where(value => value.Role != CastleCompartmentRole.PalaceCore)
                .ToArray();
            var expectedCount = defenseLayerCount == 2 ? 8 : defenseLayerCount == 3 ? 20 : 36;

            Assert.That(candidate.Validation.IsValid, Is.True,
                string.Join("\n", candidate.Validation.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            Assert.That(candidate.ProtectionDepth, Is.EqualTo(defenseLayerCount));
            Assert.That(regular.Length, Is.EqualTo(expectedCount));
            Assert.That(regular.All(value => value.CompartmentId.StartsWith("diamond_", StringComparison.Ordinal)), Is.True);
            Assert.That(
                Enumerable.Range(1, defenseLayerCount - 1).All(ring =>
                    regular.Count(value => value.DefenseRing == ring) == 4 * (ring + 1)),
                Is.True,
                "각 방어층은 네 축 격실과 마름모 대각선 계단을 가져야 합니다.");
            var minX = candidate.Compartments.Min(value => value.Bounds.xMin);
            var minZ = candidate.Compartments.Min(value => value.Bounds.yMin);
            var maxX = candidate.Compartments.Max(value => value.Bounds.xMax);
            var maxZ = candidate.Compartments.Max(value => value.Bounds.yMax);
            var envelopeCorners = new[]
            {
                new Vector2Int(minX, minZ),
                new Vector2Int(minX, maxZ - 1),
                new Vector2Int(maxX - 1, minZ),
                new Vector2Int(maxX - 1, maxZ - 1)
            };
            Assert.That(
                envelopeCorners.All(corner => candidate.Compartments.All(value => !value.Bounds.Contains(corner))),
                Is.True,
                "마름모 포락 사각형의 네 모서리는 비어 있어야 합니다.");
            Assert.That(regular.All(value => value.Bounds.width >= 5 && value.Bounds.width <= 8), Is.True);
            Assert.That(regular.All(value => value.Bounds.height >= 5 && value.Bounds.height <= 8), Is.True);
            Assert.That(regular.Select(value => value.Bounds.size).Distinct().Count(), Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void DiamondRadialFortress_BatchKeepsDistinctOpenCornerSilhouette()
        {
            var rules = LoadRules();
            var generator = new CastleGenerator();
            foreach (var defenseLayerCount in new[] { 2, 3, 4 })
            {
                foreach (var seed in Enumerable.Range(9100, 64))
                {
                    var candidate = generator.Generate(
                        rules,
                        seed,
                        CastleLayoutTheme.DiamondRadialFortress,
                        defenseLayerCount);
                    var minX = candidate.Compartments.Min(value => value.Bounds.xMin);
                    var minZ = candidate.Compartments.Min(value => value.Bounds.yMin);
                    var maxX = candidate.Compartments.Max(value => value.Bounds.xMax);
                    var maxZ = candidate.Compartments.Max(value => value.Bounds.yMax);
                    var envelopeCorners = new[]
                    {
                        new Vector2Int(minX, minZ),
                        new Vector2Int(minX, maxZ - 1),
                        new Vector2Int(maxX - 1, minZ),
                        new Vector2Int(maxX - 1, maxZ - 1)
                    };

                    Assert.That(candidate.Validation.IsValid, Is.True, $"layers={defenseLayerCount}, seed={seed}");
                    Assert.That(candidate.ProtectionDepth, Is.EqualTo(defenseLayerCount),
                        $"layers={defenseLayerCount}, seed={seed}");
                    Assert.That(
                        envelopeCorners.All(corner => candidate.Compartments.All(value => !value.Bounds.Contains(corner))),
                        Is.True,
                        $"layers={defenseLayerCount}, seed={seed}");
                    Assert.That(candidate.Compactness, Is.LessThan(0.82f),
                        $"layers={defenseLayerCount}, seed={seed}");
                }
            }
        }

        [Test]
        public void DiamondRadialFortress_BatchProducesMultipleRealStructureSkeletons()
        {
            var rules = LoadRules();
            var generator = new CastleGenerator();
            foreach (var defenseLayerCount in new[] { 2, 3, 4 })
            {
                var candidates = Enumerable.Range(12100, 200)
                    .Select(seed => generator.Generate(
                        rules,
                        seed,
                        CastleLayoutTheme.DiamondRadialFortress,
                        defenseLayerCount))
                    .ToArray();
                var profileCounts = candidates
                    .GroupBy(value => value.StructureVariant)
                    .ToDictionary(group => group.Key, group => group.Count());

                Assert.That(candidates.All(value => value.Validation.IsValid), Is.True,
                    $"layers={defenseLayerCount}");
                Assert.That(candidates.Select(value => value.StructureHash).Distinct().Count(),
                    Is.GreaterThanOrEqualTo(150),
                    $"layers={defenseLayerCount}");
                Assert.That(profileCounts.Count, Is.GreaterThanOrEqualTo(7),
                    $"layers={defenseLayerCount}");
                Assert.That(profileCounts.Values.Max(), Is.LessThanOrEqualTo(50),
                    $"layers={defenseLayerCount}");
                Assert.That(candidates.All(value => value.StructureHash != value.LayoutHash), Is.True,
                    $"layers={defenseLayerCount}");
            }
        }

        [Test]
        public void DiamondRadialFortress_SameSeedPreservesInnerSkeletonWhenLayersGrow()
        {
            var rules = LoadRules();
            var generator = new CastleGenerator();
            var doubleWall = generator.Generate(
                rules,
                20260816,
                CastleLayoutTheme.DiamondRadialFortress,
                2);
            var tripleWall = generator.Generate(
                rules,
                20260816,
                CastleLayoutTheme.DiamondRadialFortress,
                3);
            var quadrupleWall = generator.Generate(
                rules,
                20260816,
                CastleLayoutTheme.DiamondRadialFortress,
                4);

            Assert.That(tripleWall.StructureVariant, Is.EqualTo(doubleWall.StructureVariant));
            Assert.That(quadrupleWall.StructureVariant, Is.EqualTo(doubleWall.StructureVariant));
            Assert.That(
                ExtractRingBounds(tripleWall, 1),
                Is.EqualTo(ExtractRingBounds(doubleWall, 1)));
            Assert.That(
                ExtractRingBounds(quadrupleWall, 1),
                Is.EqualTo(ExtractRingBounds(doubleWall, 1)));
            Assert.That(
                ExtractRingBounds(quadrupleWall, 2),
                Is.EqualTo(ExtractRingBounds(tripleWall, 2)));
        }

        [TestCase(2, 12)]
        [TestCase(3, 32)]
        [TestCase(4, 60)]
        public void HoneycombFortress_BuildsStaggeredCellRingsAndRequestedDepth(
            int defenseLayerCount,
            int expectedCompartmentCount)
        {
            var candidate = new CastleGenerator().Generate(
                LoadRules(),
                20260816,
                CastleLayoutTheme.HoneycombCompartmentFortress,
                defenseLayerCount);
            var regular = candidate.Compartments
                .Where(value => value.Role != CastleCompartmentRole.PalaceCore)
                .ToArray();

            Assert.That(candidate.Validation.IsValid, Is.True,
                string.Join("\n", candidate.Validation.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            Assert.That(candidate.ProtectionDepth, Is.EqualTo(defenseLayerCount));
            Assert.That(regular.Length, Is.EqualTo(expectedCompartmentCount));
            Assert.That(regular.All(value => value.CompartmentId.StartsWith("honey_", StringComparison.Ordinal)), Is.True);
            Assert.That(
                Enumerable.Range(1, defenseLayerCount - 1).All(ring =>
                    regular.Count(value => value.DefenseRing == ring) == 8 * ring + 4),
                Is.True,
                "각 방어층은 네 모서리와 두 배로 잘게 분할한 네 변 격실을 가져야 합니다.");
            Assert.That(regular.All(value => value.Bounds.width >= 5 && value.Bounds.width <= 8), Is.True);
            Assert.That(regular.All(value => value.Bounds.height >= 5 && value.Bounds.height <= 8), Is.True);
            Assert.That(regular.Select(value => value.Bounds.size).Distinct().Count(), Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void HoneycombFortress_BatchProducesProfilesAndSteppedOuterCells()
        {
            var rules = LoadRules();
            var generator = new CastleGenerator();
            foreach (var defenseLayerCount in new[] { 2, 3, 4 })
            {
                var candidates = Enumerable.Range(15100 + defenseLayerCount * 1000, 120)
                    .Select(seed => generator.Generate(
                        rules,
                        seed,
                        CastleLayoutTheme.HoneycombCompartmentFortress,
                        defenseLayerCount))
                    .ToArray();

                Assert.That(candidates.All(value => value.Validation.IsValid), Is.True,
                    "invalid: " + string.Join(", ", candidates
                        .Where(value => !value.Validation.IsValid)
                        .Select(value => $"{value.Seed}:{value.StructureVariant}[{string.Join("|", value.Validation.Issues.Select(issue => issue.Code + ":" + issue.Message))}]")));
                Assert.That(candidates.Select(value => value.StructureVariant).Distinct().Count(), Is.GreaterThanOrEqualTo(7),
                    $"layers={defenseLayerCount}");
                Assert.That(candidates.Select(value => value.StructureHash).Distinct().Count(), Is.GreaterThanOrEqualTo(110),
                    $"layers={defenseLayerCount}");
                if (defenseLayerCount == (int)CastleDefenseLayerPreset.Quadruple)
                {
                    Assert.That(candidates.All(HasHoneycombOuterStep), Is.True,
                        "no step: " + string.Join(", ", candidates
                            .Where(value => !HasHoneycombOuterStep(value))
                            .Select(value => $"{value.Seed}:{value.StructureVariant}")));
                }
            }
        }

        [Test]
        public void HoneycombFortress_SameSeedPreservesInnerSkeletonWhenLayersGrow()
        {
            var rules = LoadRules();
            var generator = new CastleGenerator();
            var doubleWall = generator.Generate(
                rules,
                20260816,
                CastleLayoutTheme.HoneycombCompartmentFortress,
                2);
            var tripleWall = generator.Generate(
                rules,
                20260816,
                CastleLayoutTheme.HoneycombCompartmentFortress,
                3);
            var quadrupleWall = generator.Generate(
                rules,
                20260816,
                CastleLayoutTheme.HoneycombCompartmentFortress,
                4);

            Assert.That(tripleWall.StructureVariant, Is.EqualTo(doubleWall.StructureVariant));
            Assert.That(quadrupleWall.StructureVariant, Is.EqualTo(doubleWall.StructureVariant));
            Assert.That(ExtractRingBounds(tripleWall, 1), Is.EqualTo(ExtractRingBounds(doubleWall, 1)));
            Assert.That(ExtractRingBounds(quadrupleWall, 1), Is.EqualTo(ExtractRingBounds(doubleWall, 1)));
            Assert.That(ExtractRingBounds(quadrupleWall, 2), Is.EqualTo(ExtractRingBounds(tripleWall, 2)));
        }

        [TestCase(2, 8, 12)]
        [TestCase(3, 18, 26)]
        [TestCase(4, 30, 42)]
        public void HexHoneycombFortress_BuildsMixedHiveChambersAndRequestedDepth(
            int defenseLayerCount,
            int minimumCompartmentCount,
            int maximumCompartmentCount)
        {
            var candidate = new CastleGenerator().Generate(
                LoadRules(),
                20260816,
                CastleLayoutTheme.HexHoneycombFortress,
                defenseLayerCount);
            var core = candidate.Compartments.Single(value => value.Role == CastleCompartmentRole.PalaceCore);
            var regular = candidate.Compartments
                .Where(value => value.Role != CastleCompartmentRole.PalaceCore)
                .ToArray();
            var wallCells = candidate.Placements
                .Where(value => value.Kind == CastlePlacementKind.Wall)
                .Select(value => new Vector2Int(value.X, value.Z))
                .ToHashSet();

            Assert.That(candidate.Validation.IsValid, Is.True,
                string.Join("\n", candidate.Validation.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            Assert.That(candidate.ProtectionDepth, Is.EqualTo(defenseLayerCount));
            Assert.That(regular.Length, Is.InRange(minimumCompartmentCount, maximumCompartmentCount));
            Assert.That(core.HasCustomFootprint, Is.True);
            Assert.That(new[] { core.Bounds.width, core.Bounds.height }.OrderBy(value => value),
                Is.EqualTo(new[] { 13, 15 }));
            Assert.That(core.FootprintCells.Count, Is.GreaterThan(regular[0].FootprintCells.Count));
            Assert.That(
                Enumerable.Range(CastleSpatialContract.PalaceBounds.xMin, CastleSpatialContract.PalaceBounds.width)
                    .SelectMany(x => Enumerable.Range(
                        CastleSpatialContract.PalaceBounds.yMin,
                        CastleSpatialContract.PalaceBounds.height),
                        (x, z) => new Vector2Int(x, z))
                    .All(cell => core.ContainsFootprintCell(cell) && !wallCells.Contains(cell)),
                Is.True,
                "융합 왕궁 코어의 내부에 중앙 4×4 왕궁 자리가 온전히 남아야 합니다.");
            Assert.That(regular.All(value => value.CompartmentId.StartsWith("hex_", StringComparison.Ordinal)), Is.True);
            Assert.That(regular.All(value => value.HasCustomFootprint), Is.True);
            Assert.That(regular.Select(value => value.FootprintCells.Count).Distinct().OrderBy(value => value),
                Is.EqualTo(new[] { 23, 43, 63 }));
            Assert.That(regular.All(HasCardinallyConnectedFootprint), Is.True);
            Assert.That(
                regular.Where(value => value.CompartmentId.Contains("_small_"))
                    .All(value => value.FootprintCells.Count == 23 && HasTrueHexFootprint(value)),
                Is.True);
            Assert.That(
                regular.Where(value => value.CompartmentId.Contains("_medium_"))
                    .All(value => value.FootprintCells.Count == 43),
                Is.True);
            Assert.That(
                regular.Where(value => value.CompartmentId.Contains("_brood_"))
                    .All(value => value.FootprintCells.Count == 63),
                Is.True);
            Assert.That(regular.Any(value => value.CompartmentId.Contains("_small_")), Is.True);
            Assert.That(regular.Any(value => value.CompartmentId.Contains("_medium_")), Is.True);
            Assert.That(regular.Any(value => value.CompartmentId.Contains("_brood_")), Is.True);
            Assert.That(regular.Count(value => value.DefenseRing == 1), Is.InRange(8, 12));
            if (defenseLayerCount >= 3)
            {
                Assert.That(regular.Count(value => value.DefenseRing == 2), Is.InRange(10, 18));
            }

            if (defenseLayerCount >= 4)
            {
                Assert.That(regular.Count(value => value.DefenseRing == 3), Is.InRange(12, 24));
            }
        }

        [Test]
        public void HexHoneycombFortress_BatchCoversEightGridPhases()
        {
            var rules = LoadRules();
            var generator = new CastleGenerator();
            foreach (var defenseLayerCount in new[] { 2, 3, 4 })
            {
                var candidates = Enumerable.Range(19100 + defenseLayerCount * 1000, 120)
                    .Select(seed => generator.Generate(
                        rules,
                        seed,
                        CastleLayoutTheme.HexHoneycombFortress,
                        defenseLayerCount))
                    .ToArray();

                Assert.That(candidates.All(value => value.Validation.IsValid), Is.True,
                    "invalid: " + string.Join(", ", candidates
                        .Where(value => !value.Validation.IsValid)
                        .Select(value => $"{value.Seed}:{string.Join("|", value.Validation.Issues.Select(issue => issue.Code))}")));
                Assert.That(candidates.Select(value => value.StructureVariant).Distinct().Count(), Is.EqualTo(8));
                Assert.That(candidates.Select(value => value.StructureHash).Distinct().Count(), Is.GreaterThanOrEqualTo(60));
                Assert.That(
                    candidates.Select(value => value.Compartments.Count(compartment =>
                        compartment.Role != CastleCompartmentRole.PalaceCore)).Distinct().Count(),
                    Is.GreaterThanOrEqualTo(5));
                Assert.That(candidates.All(candidate =>
                {
                    var regular = candidate.Compartments
                        .Where(value => value.Role != CastleCompartmentRole.PalaceCore)
                        .ToArray();
                    return regular.Any(value => value.CompartmentId.Contains("_small_")) &&
                           regular.Any(value => value.CompartmentId.Contains("_medium_")) &&
                           regular.Any(value => value.CompartmentId.Contains("_brood_"));
                }), Is.True);
                Assert.That(candidates.All(value => value.ProtectionDepth == defenseLayerCount), Is.True);
            }
        }

        [Test]
        public void HexHoneycombFortress_SameSeedPreservesInnerHexShellsWhenLayersGrow()
        {
            var rules = LoadRules();
            var generator = new CastleGenerator();
            var doubleWall = generator.Generate(rules, 20260816, CastleLayoutTheme.HexHoneycombFortress, 2);
            var tripleWall = generator.Generate(rules, 20260816, CastleLayoutTheme.HexHoneycombFortress, 3);
            var quadrupleWall = generator.Generate(rules, 20260816, CastleLayoutTheme.HexHoneycombFortress, 4);

            Assert.That(tripleWall.StructureVariant, Is.EqualTo(doubleWall.StructureVariant));
            Assert.That(quadrupleWall.StructureVariant, Is.EqualTo(doubleWall.StructureVariant));
            Assert.That(ExtractRingFootprints(tripleWall, 1), Is.EqualTo(ExtractRingFootprints(doubleWall, 1)));
            Assert.That(ExtractRingFootprints(quadrupleWall, 1), Is.EqualTo(ExtractRingFootprints(doubleWall, 1)));
            Assert.That(ExtractRingFootprints(quadrupleWall, 2), Is.EqualTo(ExtractRingFootprints(tripleWall, 2)));
        }

        [TestCase(2, 8)]
        [TestCase(3, 16)]
        [TestCase(4, 24)]
        public void PetalBloomFortress_BuildsEightPetalsPerRingAndRequestedDepth(
            int defenseLayerCount,
            int expectedCompartmentCount)
        {
            var candidate = new CastleGenerator().Generate(
                LoadRules(),
                20260816,
                CastleLayoutTheme.PetalBloomFortress,
                defenseLayerCount);
            var core = candidate.Compartments.Single(value => value.Role == CastleCompartmentRole.PalaceCore);
            var regular = candidate.Compartments
                .Where(value => value.Role != CastleCompartmentRole.PalaceCore)
                .ToArray();

            Assert.That(candidate.Validation.IsValid, Is.True,
                string.Join("\n", candidate.Validation.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            Assert.That(candidate.ProtectionDepth, Is.EqualTo(defenseLayerCount));
            Assert.That(candidate.PalaceExposedSideCount, Is.Zero);
            Assert.That(regular.Length, Is.EqualTo(expectedCompartmentCount));
            Assert.That(core.HasCustomFootprint, Is.True);
            Assert.That(core.Bounds.size, Is.EqualTo(new Vector2Int(12, 12)));
            Assert.That(core.FootprintCells.Count, Is.LessThan(12 * 12));
            Assert.That(regular.All(value => value.CompartmentId.StartsWith("petal_", StringComparison.Ordinal)), Is.True);
            Assert.That(regular.All(value => value.HasCustomFootprint && HasCardinallyConnectedFootprint(value)), Is.True);
            Assert.That(regular.All(value =>
                value.Bounds.width >= 4 && value.Bounds.width <= 22 &&
                value.Bounds.height >= 4 && value.Bounds.height <= 22), Is.True);
            Assert.That(
                Enumerable.Range(1, defenseLayerCount - 1)
                    .All(ring => regular.Count(value => value.DefenseRing == ring) == 8),
                Is.True,
                "각 방어층은 8개의 독립 꽃잎 격실로 구성되어야 합니다.");
        }

        [Test]
        public void PetalBloomFortress_BatchCoversEightBloomProfiles()
        {
            var rules = LoadRules();
            var generator = new CastleGenerator();
            foreach (var defenseLayerCount in new[] { 2, 3, 4 })
            {
                var candidates = Enumerable.Range(20260800, 120)
                    .Select(seed => generator.Generate(
                        rules,
                        seed,
                        CastleLayoutTheme.PetalBloomFortress,
                        defenseLayerCount))
                    .ToArray();

                Assert.That(candidates.All(value => value.Validation.IsValid), Is.True,
                    $"layers={defenseLayerCount}");
                Assert.That(candidates.All(value => value.ProtectionDepth == defenseLayerCount), Is.True,
                    $"layers={defenseLayerCount}");
                Assert.That(candidates.Select(value => value.StructureVariant).Distinct().Count(), Is.EqualTo(8),
                    $"layers={defenseLayerCount}");
                Assert.That(candidates.Select(value => value.StructureHash).Distinct().Count(), Is.GreaterThanOrEqualTo(90),
                    $"layers={defenseLayerCount}");
            }
        }

        [Test]
        public void PetalBloomFortress_SameSeedPreservesInnerPetalsWhenLayersGrow()
        {
            var rules = LoadRules();
            var generator = new CastleGenerator();
            var doubleWall = generator.Generate(rules, 20260816, CastleLayoutTheme.PetalBloomFortress, 2);
            var tripleWall = generator.Generate(rules, 20260816, CastleLayoutTheme.PetalBloomFortress, 3);
            var quadrupleWall = generator.Generate(rules, 20260816, CastleLayoutTheme.PetalBloomFortress, 4);

            Assert.That(tripleWall.StructureVariant, Is.EqualTo(doubleWall.StructureVariant));
            Assert.That(quadrupleWall.StructureVariant, Is.EqualTo(doubleWall.StructureVariant));
            Assert.That(ExtractRingFootprints(tripleWall, 1), Is.EqualTo(ExtractRingFootprints(doubleWall, 1)));
            Assert.That(ExtractRingFootprints(quadrupleWall, 1), Is.EqualTo(ExtractRingFootprints(doubleWall, 1)));
            Assert.That(ExtractRingFootprints(quadrupleWall, 2), Is.EqualTo(ExtractRingFootprints(tripleWall, 2)));
        }

        [TestCase(CastleLayoutTheme.CrystalMandalaFortress, "mandala_")]
        [TestCase(CastleLayoutTheme.TwinSpiralFortress, "spiral_")]
        [TestCase(CastleLayoutTheme.FractalBastionFortress, "fractal_")]
        [TestCase(CastleLayoutTheme.VoronoiCrystalFortress, "voronoi_")]
        [TestCase(CastleLayoutTheme.IrisShutterFortress, "iris_")]
        public void GeometricFortress_BuildsEightConnectedRoomsPerRing(
            CastleLayoutTheme theme,
            string compartmentPrefix)
        {
            var rules = LoadRules();
            var generator = new CastleGenerator();
            foreach (var defenseLayerCount in new[] { 2, 3, 4 })
            {
                var candidate = generator.Generate(rules, 20260816, theme, defenseLayerCount);
                var regular = candidate.Compartments
                    .Where(value => value.Role != CastleCompartmentRole.PalaceCore)
                    .ToArray();

                Assert.That(candidate.Validation.IsValid, Is.True,
                    $"{theme}/{defenseLayerCount}: " + string.Join("\n", candidate.Validation.Issues
                        .Select(issue => $"{issue.Code}: {issue.Message}")));
                Assert.That(candidate.ProtectionDepth, Is.EqualTo(defenseLayerCount));
                Assert.That(candidate.PalaceExposedSideCount, Is.Zero);
                Assert.That(regular.Length, Is.EqualTo(8 * (defenseLayerCount - 1)));
                Assert.That(regular.All(value =>
                    value.CompartmentId.StartsWith(compartmentPrefix, StringComparison.Ordinal)), Is.True);
                Assert.That(regular.All(value =>
                    value.HasCustomFootprint && HasCardinallyConnectedFootprint(value)), Is.True);
                Assert.That(regular.All(value =>
                    value.Bounds.width >= 4 && value.Bounds.width <= 30 &&
                    value.Bounds.height >= 4 && value.Bounds.height <= 30), Is.True);
                Assert.That(
                    Enumerable.Range(1, defenseLayerCount - 1)
                        .All(ring => regular.Count(value => value.DefenseRing == ring) == 8),
                    Is.True,
                    $"{theme} 각 방어층은 8개의 독립 격실로 구성되어야 합니다.");
            }
        }

        [TestCase(CastleLayoutTheme.CrystalMandalaFortress)]
        [TestCase(CastleLayoutTheme.TwinSpiralFortress)]
        [TestCase(CastleLayoutTheme.FractalBastionFortress)]
        [TestCase(CastleLayoutTheme.VoronoiCrystalFortress)]
        [TestCase(CastleLayoutTheme.IrisShutterFortress)]
        public void GeometricFortress_SameSeedPreservesInnerRingsWhenLayersGrow(CastleLayoutTheme theme)
        {
            var rules = LoadRules();
            var generator = new CastleGenerator();
            var doubleWall = generator.Generate(rules, 20260816, theme, 2);
            var tripleWall = generator.Generate(rules, 20260816, theme, 3);
            var quadrupleWall = generator.Generate(rules, 20260816, theme, 4);

            Assert.That(tripleWall.StructureVariant, Is.EqualTo(doubleWall.StructureVariant));
            Assert.That(quadrupleWall.StructureVariant, Is.EqualTo(doubleWall.StructureVariant));
            Assert.That(ExtractRingFootprints(tripleWall, 1), Is.EqualTo(ExtractRingFootprints(doubleWall, 1)));
            Assert.That(ExtractRingFootprints(quadrupleWall, 1), Is.EqualTo(ExtractRingFootprints(doubleWall, 1)));
            Assert.That(ExtractRingFootprints(quadrupleWall, 2), Is.EqualTo(ExtractRingFootprints(tripleWall, 2)));
        }

        [TestCase(CastleLayoutTheme.CrystalMandalaFortress)]
        [TestCase(CastleLayoutTheme.TwinSpiralFortress)]
        [TestCase(CastleLayoutTheme.FractalBastionFortress)]
        [TestCase(CastleLayoutTheme.VoronoiCrystalFortress)]
        [TestCase(CastleLayoutTheme.IrisShutterFortress)]
        public void GeometricFortress_SeedBatchCoversEightProfiles(CastleLayoutTheme theme)
        {
            var rules = LoadRules();
            var generator = new CastleGenerator();
            var candidates = Enumerable.Range(20261000, 32)
                .SelectMany(seed => new[] { 2, 3, 4 }
                    .Select(layers => generator.Generate(rules, seed, theme, layers)))
                .ToArray();

            Assert.That(candidates.All(value => value.Validation.IsValid), Is.True,
                $"{theme}: " + string.Join(", ", candidates
                    .Where(value => !value.Validation.IsValid)
                    .Select(value => $"{value.Seed}/{value.RequestedDefenseLayerCount}:" +
                                     string.Join("|", value.Validation.Issues.Select(issue => issue.Code)))));
            Assert.That(candidates.Select(value => value.StructureVariant).Distinct().Count(), Is.EqualTo(8));
            Assert.That(candidates.Select(value => value.StructureHash).Distinct().Count(), Is.GreaterThanOrEqualTo(24));
        }

        [Test]
        public void TwinSpiralFortress_OuterPerimeterKeepsSweptBladeDepthVariation()
        {
            var rules = LoadRules();
            var generator = new CastleGenerator();
            var centerX = rules.GridWidth * 0.5d;
            var centerZ = rules.GridHeight * 0.5d;
            foreach (var defenseLayerCount in new[] { 2, 3, 4 })
            {
                foreach (var seed in Enumerable.Range(20261000, 32))
                {
                    var candidate = generator.Generate(
                        rules,
                        seed,
                        CastleLayoutTheme.TwinSpiralFortress,
                        defenseLayerCount);
                    var outerDepths = candidate.Placements
                        .Where(value => value.WallBand == CastleWallBand.OuterPerimeter)
                        .Select(value => Math.Max(
                            Math.Abs(value.X + 0.5d - centerX),
                            Math.Abs(value.Z + 0.5d - centerZ)))
                        .Distinct()
                        .ToArray();

                    Assert.That(outerDepths.Length, Is.GreaterThanOrEqualTo(4),
                        $"layers={defenseLayerCount}, seed={seed}");
                    Assert.That(outerDepths.Max() - outerDepths.Min(), Is.GreaterThanOrEqualTo(3d),
                        $"layers={defenseLayerCount}, seed={seed}");
                }
            }
        }

        [Test]
        public void SameSeed_DifferentFormalThemesProduceDifferentLayouts()
        {
            var rules = LoadRules();
            var generator = new CastleGenerator();
            var contracts = new[]
            {
                (CastleLayoutTheme.CentralCompartmentFortress, "ring_"),
                (CastleLayoutTheme.DiamondRadialFortress, "diamond_"),
                (CastleLayoutTheme.HoneycombCompartmentFortress, "honey_"),
                (CastleLayoutTheme.HexHoneycombFortress, "hex_"),
                (CastleLayoutTheme.PetalBloomFortress, "petal_"),
                (CastleLayoutTheme.CrystalMandalaFortress, "mandala_"),
                (CastleLayoutTheme.TwinSpiralFortress, "spiral_"),
                (CastleLayoutTheme.FractalBastionFortress, "fractal_"),
                (CastleLayoutTheme.VoronoiCrystalFortress, "voronoi_"),
                (CastleLayoutTheme.IrisShutterFortress, "iris_")
            };
            var candidates = contracts
                .Select(contract => generator.Generate(rules, 20260816, contract.Item1, 3))
                .ToArray();

            Assert.That(candidates.Select(value => value.LayoutHash).Distinct().Count(),
                Is.EqualTo(contracts.Length));
            for (var index = 0; index < contracts.Length; index++)
            {
                Assert.That(candidates[index].Compartments.Select(value => value.CompartmentId),
                    Has.Some.StartsWith(contracts[index].Item2), contracts[index].Item1.ToString());
            }
        }

        [Test]
        public void SameSeed_ProducesIdenticalLayoutAndDifficulty()
        {
            var rules = LoadRules();
            var generator = new CastleGenerator();

            var first = generator.Generate(rules, 20260815);
            var second = generator.Generate(rules, 20260815);

            Assert.That(second.LayoutHash, Is.EqualTo(first.LayoutHash));
            Assert.That(second.Difficulty.MinimumClearDamage, Is.EqualTo(first.Difficulty.MinimumClearDamage));
            Assert.That(second.Placements.Count, Is.EqualTo(first.Placements.Count));
        }

        [Test]
        public void SameSeed_DifferentDefenseLayerPresetsProduceDifferentLayouts()
        {
            var rules = LoadRules();
            var generator = new CastleGenerator();
            var doubleWall = generator.Generate(rules, 20260816, CastleLayoutTheme.CentralCompartmentFortress, 2);
            var tripleWall = generator.Generate(rules, 20260816, CastleLayoutTheme.CentralCompartmentFortress, 3);
            var quadrupleWall = generator.Generate(rules, 20260816, CastleLayoutTheme.CentralCompartmentFortress, 4);

            Assert.That(tripleWall.LayoutHash, Is.Not.EqualTo(doubleWall.LayoutHash));
            Assert.That(quadrupleWall.LayoutHash, Is.Not.EqualTo(tripleWall.LayoutHash));
            Assert.That(quadrupleWall.Difficulty.MinimumClearDamage,
                Is.GreaterThan(tripleWall.Difficulty.MinimumClearDamage));
            Assert.That(tripleWall.Difficulty.MinimumClearDamage,
                Is.GreaterThan(doubleWall.Difficulty.MinimumClearDamage));
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentLayouts()
        {
            var rules = LoadRules();
            var generator = new CastleGenerator();

            var first = generator.Generate(rules, 101);
            var second = generator.Generate(rules, 102);

            Assert.That(second.LayoutHash, Is.Not.EqualTo(first.LayoutHash));
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(77)]
        [TestCase(777)]
        [TestCase(7777)]
        [TestCase(-19)]
        public void GeneratedCandidate_PassesValidationAndHasClearPath(int seed)
        {
            var candidate = new CastleGenerator().Generate(LoadRules(), seed);

            Assert.That(
                candidate.Validation.IsValid,
                Is.True,
                string.Join("\n", candidate.Validation.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
            Assert.That(candidate.Difficulty.HasClearPath, Is.True);
            Assert.That(candidate.Difficulty.MinimumClearDamage, Is.GreaterThan(candidate.Difficulty.PalaceDamage));
            Assert.That(
                candidate.Difficulty.MinimumClearDamage,
                Is.EqualTo(candidate.Difficulty.MandatoryObstacleDamage + candidate.Difficulty.PalaceDamage).Within(0.001f));
            Assert.That(candidate.Difficulty.MandatoryPlacementIds.Count, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void GeneratedCandidate_HasOneCenteredPalaceAndLimitedLootDistricts()
        {
            var rules = LoadRules();
            var candidate = new CastleGenerator().Generate(rules, 1401);
            var palace = candidate.Placements.Single(placement => placement.Kind == CastlePlacementKind.Palace);
            var loot = candidate.Placements.Where(placement => placement.Kind == CastlePlacementKind.LootBuilding).ToArray();

            Assert.That(palace.Occupies(candidate.GridWidth / 2, candidate.GridHeight / 2), Is.True);
            Assert.That(loot.Length, Is.LessThanOrEqualTo(rules.MaximumSpecialDistrictCount));
            Assert.That(loot.Count(placement => placement.LootKind == CastleLootKind.Gold), Is.LessThanOrEqualTo(rules.MaximumGoldDistrictCount));
            Assert.That(loot.Count(placement => placement.LootKind == CastleLootKind.Equipment), Is.LessThanOrEqualTo(rules.MaximumEquipmentDistrictCount));
            Assert.That(loot.Count(placement => placement.LootKind == CastleLootKind.Key), Is.LessThanOrEqualTo(rules.MaximumKeyDistrictCount));
            Assert.That(loot.Sum(placement => placement.RewardBudgetCost), Is.LessThanOrEqualTo(rules.MaximumRewardBudget));
        }

        [Test]
        public void GeneratedBatch_ProducesMoreThanOneDifficultyBand()
        {
            var rules = LoadRules();
            var generator = new CastleGenerator();
            var clearDamages = Enumerable.Range(300, 24)
                .Select(seed => generator.Generate(rules, seed))
                .Select(candidate => candidate.Difficulty.MinimumClearDamage)
                .Distinct()
                .ToArray();

            Assert.That(clearDamages.Length, Is.GreaterThan(1));
        }

        [Test]
        public void HigherWallTier_IncreasesMinimumClearDamageForSameSeed()
        {
            var source = LoadRules();
            var lower = UnityEngine.Object.Instantiate(source);
            var higher = UnityEngine.Object.Instantiate(source);
            try
            {
                lower.EditorSetWallTierRange(2, 2);
                higher.EditorSetWallTierRange(3, 3);
                var generator = new CastleGenerator();

                var lowerCandidate = generator.Generate(lower, 90210);
                var higherCandidate = generator.Generate(higher, 90210);

                Assert.That(higherCandidate.Difficulty.MinimumClearDamage, Is.GreaterThan(lowerCandidate.Difficulty.MinimumClearDamage));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lower);
                UnityEngine.Object.DestroyImmediate(higher);
            }
        }

        [Test]
        public void ApprovedLayout_CopiesValidatedCandidateWithoutRegeneration()
        {
            var candidate = new CastleGenerator().Generate(LoadRules(), 5150);
            var layout = ScriptableObject.CreateInstance<CastleStageLayout>();
            try
            {
                layout.EditorStore("castle_stage_test", candidate);

                Assert.That(layout.StageId, Is.EqualTo("castle_stage_test"));
                Assert.That(layout.Seed, Is.EqualTo(candidate.Seed));
                Assert.That(layout.LayoutHash, Is.EqualTo(candidate.LayoutHash));
                Assert.That(layout.StructureHash, Is.EqualTo(candidate.StructureHash));
                Assert.That(layout.StructureVariant, Is.EqualTo(candidate.StructureVariant));
                Assert.That(layout.LayoutTheme, Is.EqualTo(candidate.Theme));
                Assert.That(layout.RequestedDefenseLayerCount, Is.EqualTo(candidate.RequestedDefenseLayerCount));
                Assert.That(layout.Compartments.Count, Is.EqualTo(candidate.Compartments.Count));
                Assert.That(layout.ProtectionDepth, Is.EqualTo(candidate.ProtectionDepth));
                Assert.That(layout.Placements.Count, Is.EqualTo(candidate.Placements.Count));
                Assert.That(layout.Difficulty.MinimumClearDamage, Is.EqualTo(candidate.Difficulty.MinimumClearDamage));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(layout);
            }
        }

        private static string[] ExtractRingBounds(CastleGenerationCandidate candidate, int defenseRing)
        {
            return candidate.Compartments
                .Where(value => value.DefenseRing == defenseRing)
                .OrderBy(value => value.CompartmentId, StringComparer.Ordinal)
                .Select(value =>
                    $"{value.CompartmentId}:{value.Bounds.x}:{value.Bounds.y}:{value.Bounds.width}:{value.Bounds.height}")
                .ToArray();
        }

        private static string[] ExtractRingFootprints(CastleGenerationCandidate candidate, int defenseRing)
        {
            return candidate.Compartments
                .Where(value => value.DefenseRing == defenseRing)
                .OrderBy(value => value.CompartmentId, StringComparer.Ordinal)
                .Select(value => $"{value.CompartmentId}:{string.Join(";", value.FootprintCells.Select(cell => $"{cell.x},{cell.y}"))}")
                .ToArray();
        }

        private static bool HasTrueHexFootprint(CastleCompartmentData compartment)
        {
            if (compartment.Bounds.size == new Vector2Int(7, 5))
            {
                return compartment.FootprintCells
                    .GroupBy(value => value.y)
                    .OrderBy(group => group.Key)
                    .Select(group => group.Count())
                    .SequenceEqual(new[] { 3, 5, 7, 5, 3 });
            }

            if (compartment.Bounds.size == new Vector2Int(5, 7))
            {
                return compartment.FootprintCells
                    .GroupBy(value => value.x)
                    .OrderBy(group => group.Key)
                    .Select(group => group.Count())
                    .SequenceEqual(new[] { 3, 5, 7, 5, 3 });
            }

            return false;
        }

        private static bool HasCardinallyConnectedFootprint(CastleCompartmentData compartment)
        {
            var remaining = compartment.FootprintCells.ToHashSet();
            if (remaining.Count == 0)
            {
                return false;
            }

            var open = new System.Collections.Generic.Queue<Vector2Int>();
            var first = remaining.First();
            remaining.Remove(first);
            open.Enqueue(first);
            while (open.Count > 0)
            {
                var cell = open.Dequeue();
                foreach (var neighbor in new[]
                         {
                             cell + Vector2Int.up,
                             cell + Vector2Int.right,
                             cell + Vector2Int.down,
                             cell + Vector2Int.left
                         })
                {
                    if (remaining.Remove(neighbor))
                    {
                        open.Enqueue(neighbor);
                    }
                }
            }

            return remaining.Count == 0;
        }

        private static bool HasHoneycombOuterStep(CastleGenerationCandidate candidate)
        {
            var ring = candidate.RequestedDefenseLayerCount - 1;
            var prefix = $"honey_{ring:00}";
            var outer = candidate.Compartments
                .Where(value => value.DefenseRing == ring)
                .ToArray();
            var north = outer.Where(value => value.CompartmentId.StartsWith(prefix + "_n_", StringComparison.Ordinal))
                .Select(value => value.Bounds.yMax).Distinct().Count() > 1;
            var east = outer.Where(value => value.CompartmentId.StartsWith(prefix + "_e_", StringComparison.Ordinal))
                .Select(value => value.Bounds.xMax).Distinct().Count() > 1;
            var south = outer.Where(value => value.CompartmentId.StartsWith(prefix + "_s_", StringComparison.Ordinal))
                .Select(value => value.Bounds.yMin).Distinct().Count() > 1;
            var west = outer.Where(value => value.CompartmentId.StartsWith(prefix + "_w_", StringComparison.Ordinal))
                .Select(value => value.Bounds.xMin).Distinct().Count() > 1;
            return north || east || south || west;
        }

        private static CastleGenerationRules LoadRules()
        {
            var rules = AssetDatabase.LoadAssetAtPath<CastleGenerationRules>(RulesPath);
            Assert.That(rules, Is.Not.Null, $"기본 생성 규칙 자산이 없습니다: {RulesPath}");
            return rules;
        }
    }
}
