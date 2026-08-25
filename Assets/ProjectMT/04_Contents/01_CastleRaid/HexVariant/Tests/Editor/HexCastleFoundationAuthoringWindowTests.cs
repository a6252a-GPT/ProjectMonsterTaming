using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex.Editor.Tests
{
    public sealed class HexCastleFoundationAuthoringWindowTests
    {
        private const string ThemeOneRulesPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Data/Foundation/HexCastleTheme1Rules.asset";

        [Test]
        public void ThemeOneRules_UsesRaisedOpenPartitionGateChance()
        {
            var rules = UnityEditor.AssetDatabase.LoadAssetAtPath<HexCastleThemeOneRules>(ThemeOneRulesPath);

            Assert.That(rules, Is.Not.Null);
            Assert.That(rules.Readiness, Is.EqualTo(HexCastleThemeOneReadiness.StageReady));
            Assert.That(rules.IsVisualApproved, Is.True);
            Assert.That(rules.CanApproveStageLayout, Is.True);
            Assert.That(rules.Tuning.DraftVersion, Is.EqualTo(HexCastleThemeOneTuning.CurrentDraftVersion));
            Assert.That(rules.Tuning.PalaceGuardBarracksCount, Is.EqualTo(1));
            Assert.That(rules.Tuning.PalaceGuardTurretCount, Is.EqualTo(2));
            Assert.That(rules.Tuning.DenseOccupancy, Is.EqualTo(1f));
            Assert.That(rules.Tuning.SparseOccupancy, Is.EqualTo(0.28f).Within(0.0001f));
            Assert.That(rules.Tuning.FixedBuildingGrades.Count, Is.EqualTo(7));
            Assert.That(rules.Tuning.TurretBandLevels.Count, Is.EqualTo(3));
            Assert.That(
                rules.Tuning.TurretWeaponCycle,
                Is.EqualTo(new[]
                {
                    HexCastleTurretWeaponKind.Cannon,
                    HexCastleTurretWeaponKind.Ballista,
                    HexCastleTurretWeaponKind.Fireball
                }));
            Assert.That(rules.Tuning.OpenPartitionGateCountPerBand, Is.EqualTo(1));
            Assert.That(rules.Tuning.OpenPartitionAdditionalGateChance, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(rules.Tuning.OpenPartitionGateMaximumPerBand, Is.EqualTo(2));
            Assert.That(rules.Tuning.LayerQuotas.All(value =>
                value.FutureTrapCount == 0 && value.FutureInitialDefenderCount == 0), Is.True);
        }

        [Test]
        public void GeneratorWindow_UsesDedicatedHexMenuAndFoundationPipeline()
        {
            Assert.That(
                HexCastleAuthoringWindow.MenuPath,
                Is.EqualTo("JC Tool/군단의 역습 육각/성 생성기"));

            var candidate = new HexCastleGenerationPipeline().GenerateFoundation(
                10801,
                3,
                HexCastleTheme.CentralCompartment);

            Assert.That(candidate.Validation.IsValid, Is.True);
            Assert.That(candidate.Layout.Seed, Is.EqualTo(10801));
            Assert.That(candidate.Layout.Theme, Is.EqualTo(HexCastleTheme.CentralCompartment));
            Assert.That(candidate.Layout.WallRadii, Is.EqualTo(new[] { 3, 5, 8 }));
            Assert.That(candidate.Layout.BattlefieldRadius, Is.EqualTo(10));
            Assert.That(candidate.Layout.Cells.Count, Is.EqualTo(331));
            Assert.That(candidate.Layout.Enumerate(HexCastleCellKind.Palace).Count(), Is.EqualTo(7));
            Assert.That(candidate.Layout.Cells.Values.Count(cell => cell.IsBuildingCell), Is.EqualTo(57));
            Assert.That(candidate.Layout.Cells.Values.Count(cell =>
                cell.PlacementDensity == HexCastlePlacementDensity.Dense), Is.EqualTo(41));
            Assert.That(candidate.Layout.Cells.Values.Count(cell =>
                cell.PlacementDensity == HexCastlePlacementDensity.Sparse), Is.EqualTo(16));
            Assert.That(candidate.Layout.Cells.Values.Count(cell =>
                cell.BuildingRole == HexCastleBuildingRole.Turret && cell.DefenseLayer == 0), Is.EqualTo(2));
            Assert.That(candidate.Layout.Cells.Values.Count(cell =>
                cell.BuildingRole == HexCastleBuildingRole.GoldStorage), Is.EqualTo(1));
            Assert.That(candidate.Layout.Cells.Values.Count(cell =>
                cell.BuildingRole == HexCastleBuildingRole.EquipmentForge), Is.EqualTo(1));
            Assert.That(candidate.Layout.Cells.Values.Count(cell =>
                cell.BuildingRole == HexCastleBuildingRole.KeyVault), Is.EqualTo(1));
            Assert.That(candidate.Difficulty.TotalBuildingGrade, Is.GreaterThan(0));
        }

        [TestCase(2, 7, 169)]
        [TestCase(3, 10, 331)]
        [TestCase(4, 13, 547)]
        public void GeneratorWindow_DefenseLayerSelectionResolvesCanonicalBoard(
            int defenseLayerCount,
            int expectedBoardRadius,
            int expectedCellCount)
        {
            var candidate = new HexCastleGenerationPipeline().GenerateFoundation(
                10801,
                defenseLayerCount,
                HexCastleTheme.CentralCompartment);

            Assert.That(candidate.Validation.IsValid, Is.True);
            Assert.That(candidate.Layout.BattlefieldRadius, Is.EqualTo(expectedBoardRadius));
            Assert.That(candidate.Layout.Cells.Count, Is.EqualTo(expectedCellCount));
            Assert.That(candidate.Layout.Cells.Values.Count(cell => cell.InitialBlocked), Is.GreaterThan(0));
        }

        [Test]
        public void GeneratorWindow_TwoDimensionalPreviewIsDeterministicForSameSeed()
        {
            var candidate = new HexCastleGenerationPipeline().GenerateFoundation(
                10801,
                4,
                HexCastleTheme.CentralCompartment);
            Texture2D first = null;
            Texture2D repeat = null;
            try
            {
                first = HexCastlePreviewExporter.BuildTexture(candidate, 320);
                repeat = HexCastlePreviewExporter.BuildTexture(candidate, 320);

                Assert.That(first.width, Is.EqualTo(320));
                Assert.That(first.height, Is.EqualTo(320));
                Assert.That(first.GetPixels32(), Is.EqualTo(repeat.GetPixels32()));
                Assert.That(first.GetPixels32().Distinct().Count(), Is.GreaterThan(8));
            }
            finally
            {
                if (first != null)
                {
                    Object.DestroyImmediate(first);
                }

                if (repeat != null)
                {
                    Object.DestroyImmediate(repeat);
                }
            }
        }

        [Test]
        public void GeneratorWindow_TwoDimensionalPreviewUsesDistinctSolidRoleColors()
        {
            var candidate = new HexCastleGenerationPipeline().GenerateFoundation(
                10801,
                4,
                HexCastleTheme.CentralCompartment);
            var requiredRoles = new[]
            {
                HexCastleBuildingRole.KnightBarracks,
                HexCastleBuildingRole.FarmerBarracks,
                HexCastleBuildingRole.Turret,
                HexCastleBuildingRole.TrainingYard,
                HexCastleBuildingRole.Church,
                HexCastleBuildingRole.GoldStorage,
                HexCastleBuildingRole.EquipmentForge,
                HexCastleBuildingRole.KeyVault,
                HexCastleBuildingRole.Blocker
            };
            var roleColors = requiredRoles.Select(role =>
            {
                var cell = candidate.Layout.Cells.Values.FirstOrDefault(value => value.BuildingRole == role);
                Assert.That(cell, Is.Not.Null, role.ToString());
                return HexCastleVisualPalette.ResolveColor(
                    cell,
                    candidate.Layout.Theme,
                    HexCastlePreviewColorMode.Architecture);
            }).ToArray();

            Assert.That(roleColors.Distinct().Count(), Is.EqualTo(requiredRoles.Length));

            var topologyCells = new[]
            {
                candidate.Layout.Enumerate(HexCastleCellKind.Wall)
                    .First(value => value.WallRole != HexCastleWallRole.Partition),
                candidate.Layout.Enumerate(HexCastleCellKind.Tower).First(),
                candidate.Layout.Enumerate(HexCastleCellKind.Gate)
                    .First(value => value.GateRole == HexCastleGateRole.ClosedWall),
                candidate.Layout.Enumerate(HexCastleCellKind.Gate)
                    .First(value => value.GateRole == HexCastleGateRole.OpenDefenderPassage)
            };
            var topologyColors = topologyCells.Select(cell => HexCastleVisualPalette.ResolveColor(
                cell,
                candidate.Layout.Theme,
                HexCastlePreviewColorMode.Architecture)).ToArray();

            Assert.That(topologyColors.Distinct().Count(), Is.EqualTo(topologyCells.Length));
            Assert.That(roleColors.Intersect(topologyColors).Count(), Is.Zero);
        }
    }
}
