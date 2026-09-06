using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex.Tests
{
    public sealed class HexCastleCellOwnershipTests
    {
        private readonly List<GameObject> owned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var target in owned)
            {
                if (target != null)
                {
                    Object.DestroyImmediate(target);
                }
            }

            owned.Clear();
        }

        [Test]
        public void OpenCell_HasNoHealthOrCollider()
        {
            var cell = new HexCastleCell(new HexCoordinates(1, 0), HexCastleCellKind.Ground);
            var runtime = CreateRuntime(cell);

            Assert.That(runtime.IsBlocked, Is.False);
            Assert.That(runtime.IsDamageable, Is.False);
            Assert.That(runtime.Health, Is.Null);
            Assert.That(runtime.FootprintCollider, Is.Null);
        }

        [Test]
        public void TurretRuntime_CopiesRangeAndWallTraversalContract()
        {
            var cell = new HexCastleCell(
                new HexCoordinates(4, 0),
                HexCastleCellKind.DefenseBuilding,
                defenseLayer: 1,
                hitPoints: 220f,
                initialBlocked: true,
                placementId: "TEST_TURRET",
                visualVariantId: "building_tower_base_blue",
                buildingRole: HexCastleBuildingRole.Turret,
                placementDensity: HexCastlePlacementDensity.Dense,
                buildingGrade: 2,
                turretWeaponKind: HexCastleTurretWeaponKind.Cannon,
                turretRangeCells: 3,
                turretCanAttackAcrossWalls: true);
            var runtime = CreateRuntime(cell);

            Assert.That(runtime.TurretWeaponKind, Is.EqualTo(HexCastleTurretWeaponKind.Cannon));
            Assert.That(runtime.BuildingGrade, Is.EqualTo(2));
            Assert.That(runtime.TurretRangeCells, Is.EqualTo(3));
            Assert.That(runtime.TurretCanAttackAcrossWalls, Is.True);
        }

        [Test]
        public void OpenPartitionGate_AllowsOnlyDefendersUntilCellIsDestroyed()
        {
            var cell = new HexCastleCell(
                new HexCoordinates(4, 0),
                HexCastleCellKind.Gate,
                defenseLayer: 1,
                hitPoints: 100f,
                wallRole: HexCastleWallRole.Partition,
                initialBlocked: true,
                wallTier: 1,
                wallConnectionMask: (1 << 0) | (1 << 3),
                gateRole: HexCastleGateRole.OpenDefenderPassage,
                gatePassageMask: (1 << 2) | (1 << 4));
            var runtime = CreateRuntime(cell);

            Assert.That(cell.CanTraverseWithoutBreaking(HexCastleTraversalFaction.Defender), Is.True);
            Assert.That(cell.CanTraverseWithoutBreaking(HexCastleTraversalFaction.Assault), Is.False);
            Assert.That(cell.CanTraverseBetween(2, 4, HexCastleTraversalFaction.Defender), Is.True);
            Assert.That(cell.CanTraverseBetween(2, 4, HexCastleTraversalFaction.Assault), Is.False);
            Assert.That(cell.CanEnterFrom(0, HexCastleTraversalFaction.Defender), Is.False);
            Assert.That(runtime.CanTraverse(HexCastleTraversalFaction.Defender), Is.True);
            Assert.That(runtime.CanTraverse(HexCastleTraversalFaction.Assault), Is.False);
            Assert.That(runtime.CanTraverseBetween(2, 4, HexCastleTraversalFaction.Defender), Is.True);
            Assert.That(runtime.CanEnterFrom(0, HexCastleTraversalFaction.Defender), Is.False);

            Assert.That(runtime.ApplyDamage(100f, runtime.transform.position), Is.True);
            Assert.That(runtime.CanTraverse(HexCastleTraversalFaction.Defender), Is.True);
            Assert.That(runtime.CanTraverse(HexCastleTraversalFaction.Assault), Is.True);
        }

        [Test]
        public void DestroyingOneWallCell_OpensOnlyThatCell()
        {
            var first = CreateRuntime(new HexCastleCell(
                new HexCoordinates(0, 0), HexCastleCellKind.Wall, hitPoints: 100f));
            var second = CreateRuntime(new HexCastleCell(
                new HexCoordinates(1, 0), HexCastleCellKind.Wall, hitPoints: 100f));

            Assert.That(first.ApplyDamage(100f, first.transform.position), Is.True);

            Assert.That(first.IsDestroyed, Is.True);
            Assert.That(first.IsBlocked, Is.False);
            Assert.That(first.FootprintCollider.enabled, Is.False);
            Assert.That(first.TileVisualRoot.gameObject.activeSelf, Is.True);
            Assert.That(first.ContentVisualRoot.gameObject.activeSelf, Is.False);
            Assert.That(second.IsDestroyed, Is.False);
            Assert.That(second.IsBlocked, Is.True);
            Assert.That(second.CurrentHealth, Is.EqualTo(100f));
        }

        [Test]
        public void DamageableStructure_ShowsRedHealthBarOnDamageAndHidesOnDeath()
        {
            var runtime = CreateRuntime(new HexCastleCell(
                new HexCoordinates(0, 0),
                HexCastleCellKind.Building,
                hitPoints: 100f,
                initialBlocked: true,
                placementId: "TEST_BUILDING",
                visualVariantId: "building_house_A",
                buildingRole: HexCastleBuildingRole.Blocker,
                placementDensity: HexCastlePlacementDensity.Dense,
                buildingGrade: 1));

            Assert.That(runtime.TryGetComponent<HexCastleOverheadHealthBar>(out _), Is.False);
            Assert.That(runtime.ApplyDamage(25f, runtime.transform.position), Is.True);

            Assert.That(runtime.TryGetComponent<HexCastleOverheadHealthBar>(out var healthBar), Is.True);
            Assert.That(healthBar.IsVisible, Is.True);
            Assert.That(healthBar.FillRatio, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(healthBar.FillColor, Is.EqualTo(HexCastleOverheadHealthBar.HostileColor));

            Assert.That(runtime.ApplyDamage(75f, runtime.transform.position), Is.True);
            Assert.That(runtime.IsDestroyed, Is.True);
            Assert.That(healthBar.IsVisible, Is.False);
        }

        [Test]
        public void TowerCell_WallAndTowerVisualsShareOneRuntimeAndHealth()
        {
            var runtime = CreateRuntime(new HexCastleCell(
                new HexCoordinates(0, 1), HexCastleCellKind.Tower, hitPoints: 180f));
            var wall = CreateChild("WallVisual", runtime.ContentVisualRoot);
            var tower = CreateChild("TowerVisual", runtime.ContentVisualRoot);

            Assert.That(wall.parent, Is.EqualTo(runtime.ContentVisualRoot));
            Assert.That(tower.parent, Is.EqualTo(runtime.ContentVisualRoot));
            Assert.That(runtime.GetComponents<HexCastleCellRuntime>().Length, Is.EqualTo(1));
            Assert.That(runtime.GetComponents<HealthComponent>().Length, Is.EqualTo(1));
            Assert.That(runtime.GetComponents<Collider>().Length, Is.EqualTo(1));
            Assert.That(runtime.ContentVisualRoot.GetComponentsInChildren<HealthComponent>(true), Is.Empty);
            Assert.That(runtime.ContentVisualRoot.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [TestCase(2, 54, 6)]
        [TestCase(3, 114, 18)]
        [TestCase(4, 192, 30)]
        public void ThemeOne_WallNetworkUsesCornersForEveryResolvableTwoWayConnection(
            int defenseLayerCount,
            int expectedNetworkCells,
            int expectedPartitionCells)
        {
            var layout = new HexCastleFoundationGenerator().Generate(
                10801,
                defenseLayerCount,
                HexCastleTheme.CentralCompartment);
            var topology = HexCastleWallTopologyResolver.Build(layout);
            var wallPathCells = layout.Cells.Values.Where(value => value.IsWallPathCell).ToArray();
            var towerCells = wallPathCells.Where(value => value.Kind == HexCastleCellKind.Tower).ToArray();
            var gateCells = wallPathCells.Where(value => value.Kind == HexCastleCellKind.Gate).ToArray();

            Assert.That(wallPathCells.Length, Is.EqualTo(expectedNetworkCells));
            Assert.That(layout.Enumerate(HexCastleCellKind.Wall).Count(),
                Is.EqualTo(expectedNetworkCells - towerCells.Length - gateCells.Length));
            Assert.That(gateCells.Length,
                Is.InRange(defenseLayerCount * 2 + defenseLayerCount - 1,
                    defenseLayerCount * 2 + (defenseLayerCount - 1) * 2));
            Assert.That(wallPathCells.Count(value => value.WallRole == HexCastleWallRole.Partition),
                Is.EqualTo(expectedPartitionCells));
            Assert.That(towerCells.All(value => value.InitialBlocked && value.HitPoints > 0f), Is.True);
            Assert.That(wallPathCells.All(value => value.HasExplicitWallConnections), Is.True);
            Assert.That(topology.Values.All(value =>
                    value.ConnectionCount >= 2 && value.ConnectionCount <= 4),
                Is.True);
            Assert.That(wallPathCells.All(value =>
            {
                var cellTopology = topology[value.Coordinates];
                var shouldBeTower = cellTopology.IsJunction;
                return (value.Kind == HexCastleCellKind.Tower) == shouldBeTower;
            }), Is.True);

            Assert.That(towerCells.All(value => topology[value.Coordinates].ConnectionCount >= 3), Is.True);
            Assert.That(wallPathCells.Where(value => value.Kind == HexCastleCellKind.Wall)
                .All(value =>
                {
                    var directions = topology[value.Coordinates].GetDirections();
                    Assert.DoesNotThrow(() => HexCastleWallVisualResolver.ResolveDirections(
                        HexCastleCellKind.Wall,
                        directions[0],
                        directions[1]));
                    return true;
                }), Is.True);

            for (var layerIndex = 0; layerIndex < layout.WallRadii.Count; layerIndex++)
            {
                var radius = layout.WallRadii[layerIndex];
                for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
                {
                    var coordinates = HexCoordinates.Directions[direction] * radius;
                    Assert.That(layout.Cells[coordinates].Kind, Is.EqualTo(HexCastleCellKind.Tower));
                    Assert.That(topology[coordinates].ConnectionCount,
                        Is.EqualTo(layerIndex == 0 || layerIndex == layout.WallRadii.Count - 1 ? 3 : 4));
                }
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void ThemeOne_GatesUseEveryWallRingAndClearDefenderPassageRules(int defenseLayerCount)
        {
            foreach (var seed in new[] { 10801, 10802, 22017 })
            {
                var layout = new HexCastleFoundationGenerator().Generate(
                    seed,
                    defenseLayerCount,
                    HexCastleTheme.CentralCompartment);
                var gates = layout.Enumerate(HexCastleCellKind.Gate).ToArray();
                var closed = gates.Where(value => value.GateRole == HexCastleGateRole.ClosedWall).ToArray();
                var open = gates.Where(value => value.GateRole == HexCastleGateRole.OpenDefenderPassage).ToArray();

                Assert.That(closed.Length, Is.EqualTo(defenseLayerCount * 2));
                Assert.That(open.Length, Is.InRange(defenseLayerCount - 1, (defenseLayerCount - 1) * 2));

                for (var layerIndex = 0; layerIndex < defenseLayerCount; layerIndex++)
                {
                    var radius = layout.WallRadii[layerIndex];
                    var ringGates = closed
                        .Where(value => value.Coordinates.DistanceFromOrigin == radius)
                        .ToArray();
                    Assert.That(ringGates.Length, Is.EqualTo(2));
                    Assert.That(ringGates.All(value =>
                        value.DefenseLayer == layerIndex + 1 &&
                        value.WallRole != HexCastleWallRole.Partition &&
                        value.GatePassageMask == 0 &&
                        value.HitPoints < layout.Cells.Values
                            .Where(cell => cell.Kind == HexCastleCellKind.Wall && cell.WallTier == value.WallTier)
                            .Max(cell => cell.HitPoints)), Is.True);
                    Assert.That(ringGates
                        .GroupBy(value => System.Math.Min(5, System.Math.Max(0, value.PathIndex / radius)))
                        .All(group => group.Count() <= 2), Is.True);
                }

                for (var bandIndex = 0; bandIndex < defenseLayerCount - 1; bandIndex++)
                {
                    var outerRadius = layout.WallRadii[bandIndex + 1];
                    var bandGates = open.Where(value => value.DefenseLayer == bandIndex + 1).ToArray();
                    Assert.That(bandGates.Length, Is.InRange(1, 2));
                    foreach (var gate in bandGates)
                    {
                        Assert.That(gate.Coordinates.DistanceFromOrigin, Is.EqualTo(outerRadius - 1));
                        Assert.That(gate.WallRole, Is.EqualTo(HexCastleWallRole.Partition));
                        Assert.That(gate.CanTraverseWithoutBreaking(HexCastleTraversalFaction.Defender), Is.True);
                        Assert.That(gate.CanTraverseWithoutBreaking(HexCastleTraversalFaction.Assault), Is.False);

                        var approaches = Enumerable.Range(0, HexCoordinates.Directions.Length)
                            .Where(direction => (gate.GatePassageMask & 1 << direction) != 0)
                            .Select(direction => layout.Cells[gate.Coordinates.Neighbor(direction)])
                            .ToArray();
                        Assert.That(approaches.Length, Is.EqualTo(2));
                        Assert.That(approaches.All(value =>
                            value.Kind == HexCastleCellKind.Ground && value.IsOpen), Is.True);
                    }
                }

                Assert.That(new HexCastleValidator().Validate(layout).IsValid, Is.True);
            }
        }

        [Test]
        public void ThemeOne_OpenPartitionGateChanceIsGeneratorInputNotWindowMemo()
        {
            foreach (var rule in new[]
                     {
                         new { Chance = 0f, ExpectedPerBand = 1 },
                         new { Chance = 1f, ExpectedPerBand = 2 }
                     })
            {
                var tuning = HexCastleThemeOneTuning.CreateDraftDefaults();
                JsonUtility.FromJsonOverwrite(
                    $"{{\"openPartitionGateCountPerBand\":1," +
                    $"\"openPartitionAdditionalGateChance\":{rule.Chance:0}," +
                    "\"openPartitionGateMaximumPerBand\":2}",
                    tuning);

                for (var defenseLayerCount = 2; defenseLayerCount <= 4; defenseLayerCount++)
                {
                    var layout = new HexCastleFoundationGenerator().Generate(
                        10801,
                        defenseLayerCount,
                        HexCastleTheme.CentralCompartment,
                        tuning);
                    var openGates = layout.Enumerate(HexCastleCellKind.Gate)
                        .Where(value => value.GateRole == HexCastleGateRole.OpenDefenderPassage)
                        .ToArray();

                    Assert.That(openGates.Length,
                        Is.EqualTo((defenseLayerCount - 1) * rule.ExpectedPerBand));
                    for (var bandIndex = 0; bandIndex < defenseLayerCount - 1; bandIndex++)
                    {
                        Assert.That(openGates.Count(value => value.DefenseLayer == bandIndex + 1),
                            Is.EqualTo(rule.ExpectedPerBand));
                    }

                    Assert.That(new HexCastleValidator().Validate(layout).IsValid, Is.True);
                }
            }
        }

        [Test]
        public void DifficultyOneToTen_ScalesVisibleRewardsTurretsBarracksGatesAndDefenders()
        {
            var expectedSnareCounts = new[] { 2, 2, 2, 4, 4, 4, 4, 6, 6, 8 };
            var expectedSpikePlateCounts = new[] { 1, 2, 4, 2, 4, 4, 6, 6, 8, 8 };
            var expectedBlastMineCounts = new[] { 1, 1, 1, 2, 2, 4, 4, 4, 6, 8 };
            var previousRewardCount = 0;
            var previousTurretCount = 0;
            var previousInitialDefenderCount = 0;
            var previousTrapCount = 0;
            Assert.That(HexCastleTrapBalance.Resolve(
                HexCastleTrapType.Snare, 1).TriggerDelaySeconds, Is.Zero);
            Assert.That(HexCastleTrapBalance.Resolve(
                HexCastleTrapType.SpikePlate, 1).TriggerDelaySeconds, Is.Zero);
            Assert.That(HexCastleTrapBalance.Resolve(
                HexCastleTrapType.BlastMine, 1).TriggerDelaySeconds, Is.EqualTo(0.85f).Within(0.001f));
            for (var difficulty = 1; difficulty <= 10; difficulty++)
            {
                var seed = 10801 + difficulty;
                var profile = HexCastleDifficultyProfile.Resolve(difficulty, seed);
                var candidate = new HexCastleGenerationPipeline().GenerateFoundationForDifficulty(
                    seed,
                    difficulty,
                    HexCastleTheme.CentralCompartment);
                var layout = candidate.Layout;
                var buildings = layout.Cells.Values.Where(value => value.IsBuildingCell).ToArray();
                var rewardCount = buildings.Count(value => value.Kind == HexCastleCellKind.RewardBuilding);
                var turretCount = buildings.Count(value => value.BuildingRole == HexCastleBuildingRole.Turret);
                var initialDefenderCount = profile.InitialKnightCount + profile.InitialFarmerCount;

                Assert.That(candidate.Validation.IsValid, Is.True,
                    $"난이도 {difficulty}: {string.Join(" | ", candidate.Validation.Errors)}");
                Assert.That(layout.DifficultyLevel, Is.EqualTo(difficulty));
                Assert.That(layout.DefenseLayerCount, Is.EqualTo(profile.DefenseLayerCount));
                Assert.That(profile.SnareTrapCount, Is.EqualTo(expectedSnareCounts[difficulty - 1]));
                Assert.That(profile.SpikePlateTrapCount, Is.EqualTo(expectedSpikePlateCounts[difficulty - 1]));
                Assert.That(profile.BlastMineCount, Is.EqualTo(expectedBlastMineCounts[difficulty - 1]));
                Assert.That(layout.TrapPlacements.Count, Is.EqualTo(profile.TotalTrapCount));
                Assert.That(layout.TrapPlacements.Count(value =>
                    value.TrapType == HexCastleTrapType.Snare), Is.EqualTo(profile.SnareTrapCount));
                Assert.That(layout.TrapPlacements.Count(value =>
                    value.TrapType == HexCastleTrapType.SpikePlate), Is.EqualTo(profile.SpikePlateTrapCount));
                Assert.That(layout.TrapPlacements.Count(value =>
                    value.TrapType == HexCastleTrapType.BlastMine), Is.EqualTo(profile.BlastMineCount));
                Assert.That(rewardCount, Is.EqualTo(profile.RewardBuildingCount));
                Assert.That(turretCount,
                    Is.EqualTo(profile.TurretCount + HexCastleFoundationGenerator.PalaceGuardTurretCount));
                Assert.That(buildings.Count(value =>
                        value.BuildingRole == HexCastleBuildingRole.KnightBarracks),
                    Is.EqualTo(profile.KnightBarracksCount +
                               HexCastleFoundationGenerator.PalaceGuardBarracksCount));
                Assert.That(buildings.Count(value =>
                        value.BuildingRole == HexCastleBuildingRole.FarmerBarracks),
                    Is.EqualTo(profile.FarmerBarracksCount));
                Assert.That(buildings.Where(value =>
                        value.BuildingRole == HexCastleBuildingRole.KnightBarracks)
                    .All(value => value.BuildingGrade > profile.FarmerBarracksGrade), Is.True);
                Assert.That(layout.Enumerate(HexCastleCellKind.Gate)
                    .Where(value => value.GateRole == HexCastleGateRole.ClosedWall)
                    .GroupBy(value => value.DefenseLayer)
                    .All(group => group.Count() == profile.ClosedGateCountPerWallRing), Is.True);
                if (difficulty >= 7)
                {
                    Assert.That(layout.Enumerate(HexCastleCellKind.Gate)
                        .Where(value => value.GateRole == HexCastleGateRole.OpenDefenderPassage)
                        .GroupBy(value => value.DefenseLayer)
                        .All(group => group.Count() == 2), Is.True);
                }

                Assert.That(rewardCount, Is.GreaterThanOrEqualTo(previousRewardCount));
                Assert.That(turretCount, Is.GreaterThanOrEqualTo(previousTurretCount));
                Assert.That(initialDefenderCount, Is.GreaterThanOrEqualTo(previousInitialDefenderCount));
                Assert.That(profile.TotalTrapCount, Is.GreaterThanOrEqualTo(previousTrapCount));
                Assert.That(180f * profile.KnightHealthMultiplier,
                    Is.GreaterThan(90f * profile.FarmerHealthMultiplier));
                Assert.That(18f * profile.KnightAttackMultiplier,
                    Is.GreaterThan(8f * profile.FarmerAttackMultiplier));
                previousRewardCount = rewardCount;
                previousTurretCount = turretCount;
                previousInitialDefenderCount = initialDefenderCount;
                previousTrapCount = profile.TotalTrapCount;
            }
        }

        [Test]
        public void DifficultyOneToTen_AllFormalThemes_Seed10801_GenerateValidProceduralLayouts()
        {
            ValidateDifficultyGrid(10801);
        }

        [Test]
        public void DifficultyOneToTen_AllFormalThemes_Seed10802_GenerateValidProceduralLayouts()
        {
            ValidateDifficultyGrid(10802);
        }

        [Test]
        public void DifficultyOneToTen_AllFormalThemes_Seed10803_GenerateValidProceduralLayouts()
        {
            ValidateDifficultyGrid(10803);
        }

        private static void ValidateDifficultyGrid(int seed)
        {
            var pipeline = new HexCastleGenerationPipeline();
            foreach (var theme in HexCastleThemeCatalog.Themes)
            {
                for (var difficulty = 1; difficulty <= 10; difficulty++)
                {
                    HexCastleCandidate candidate;
                    try
                    {
                        candidate = pipeline.GenerateFoundationForDifficulty(
                            seed,
                            difficulty,
                            theme);
                    }
                    catch (System.Exception exception)
                    {
                        Assert.Fail(
                            $"{theme} Seed {seed} 난이도 {difficulty} 생성 예외: {exception}");
                        return;
                    }

                    Assert.That(candidate.Validation.IsValid, Is.True,
                        $"{theme} Seed {seed} 난이도 {difficulty}: " +
                        string.Join(" | ", candidate.Validation.Errors));
                }
            }
        }

        [Test]
        public void DifficultyWallCountMapping_UsesSeedOnlyForLevelsFiveAndSix()
        {
            for (var difficulty = 1; difficulty <= 3; difficulty++)
            {
                Assert.That(HexCastleDifficultyProfile.ResolveDefenseLayerCount(difficulty, 1), Is.EqualTo(2));
            }

            Assert.That(HexCastleDifficultyProfile.ResolveDefenseLayerCount(4, 1), Is.EqualTo(3));
            for (var difficulty = 7; difficulty <= 10; difficulty++)
            {
                Assert.That(HexCastleDifficultyProfile.ResolveDefenseLayerCount(difficulty, 1), Is.EqualTo(4));
            }

            foreach (var difficulty in new[] { 5, 6 })
            {
                var first = HexCastleDifficultyProfile.ResolveDefenseLayerCount(difficulty, 10801);
                var repeat = HexCastleDifficultyProfile.ResolveDefenseLayerCount(difficulty, 10801);
                var changed = HexCastleDifficultyProfile.ResolveDefenseLayerCount(difficulty, 10802);
                Assert.That(first, Is.EqualTo(repeat));
                Assert.That(first, Is.EqualTo(3).Or.EqualTo(4));
                Assert.That(changed, Is.EqualTo(3).Or.EqualTo(4));
                Assert.That(changed, Is.Not.EqualTo(first));
            }
        }

        [Test]
        public void ProceduralThemeSelection_IsDeterministicAndNeverRepeatsCurrentTheme()
        {
            foreach (var currentTheme in HexCastleThemeCatalog.Themes)
            {
                for (var difficulty = 1; difficulty <= 10; difficulty++)
                {
                    var seed = 10801 + difficulty;
                    var first = HexCastleThemeCatalog.ResolveNextProceduralTheme(
                        currentTheme,
                        seed,
                        difficulty);
                    var repeat = HexCastleThemeCatalog.ResolveNextProceduralTheme(
                        currentTheme,
                        seed,
                        difficulty);

                    Assert.That(first, Is.EqualTo(repeat));
                    Assert.That(first, Is.Not.EqualTo(currentTheme),
                        $"{currentTheme} 난이도 {difficulty}의 다음 절차 테마가 반복됐습니다.");
                    Assert.That(HexCastleThemeCatalog.Themes, Does.Contain(first));
                }
            }
        }

        [Test]
        public void ThemeOne_ConfirmedPalaceGuardAndTurretCycleComeFromRules()
        {
            var tuning = HexCastleThemeOneTuning.CreateDraftDefaults();
            JsonUtility.FromJsonOverwrite(
                "{\"turretWeaponCycle\":[3]," +
                "\"turretBandLevels\":[" +
                "{\"defenseLayerCount\":2,\"firstBandLevel\":3,\"secondBandLevel\":1,\"thirdBandLevel\":1}," +
                "{\"defenseLayerCount\":3,\"firstBandLevel\":2,\"secondBandLevel\":1,\"thirdBandLevel\":1}," +
                "{\"defenseLayerCount\":4,\"firstBandLevel\":3,\"secondBandLevel\":2,\"thirdBandLevel\":1}]}",
                tuning);
            var layout = new HexCastleFoundationGenerator().Generate(
                10801,
                2,
                HexCastleTheme.CentralCompartment,
                tuning);
            var buildings = layout.Cells.Values.Where(value => value.IsBuildingCell).ToArray();

            Assert.That(
                buildings.Count(value => value.BuildingRole == HexCastleBuildingRole.KnightBarracks),
                Is.EqualTo(HexCastleFoundationGenerator.PalaceGuardBarracksCount));
            Assert.That(
                buildings.Where(value => value.BuildingRole == HexCastleBuildingRole.Turret)
                    .Select(value => value.TurretWeaponKind),
                Is.All.EqualTo(HexCastleTurretWeaponKind.Fireball));
            Assert.That(
                buildings.Where(value => value.BuildingRole == HexCastleBuildingRole.Turret)
                    .Select(value => value.BuildingGrade),
                Is.All.EqualTo(3));
            Assert.That(
                buildings.Where(value =>
                        value.BuildingRole != HexCastleBuildingRole.Blocker &&
                        value.BuildingRole != HexCastleBuildingRole.Turret)
                    .All(value => value.BuildingGrade ==
                                  tuning.ResolveBuildingGrade(value.BuildingRole)),
                Is.True);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void ThemeOne_BuildingsUseDenseAndSparseRowsWithRequiredRoles(int defenseLayerCount)
        {
            var layout = new HexCastleFoundationGenerator().Generate(
                10801,
                defenseLayerCount,
                HexCastleTheme.CentralCompartment);
            var buildings = layout.Cells.Values.Where(value => value.IsBuildingCell).ToArray();

            Assert.That(buildings.Count(value => value.BuildingRole == HexCastleBuildingRole.GoldStorage), Is.EqualTo(1));
            Assert.That(buildings.Count(value => value.BuildingRole == HexCastleBuildingRole.EquipmentForge), Is.EqualTo(1));
            Assert.That(buildings.Count(value => value.BuildingRole == HexCastleBuildingRole.KeyVault), Is.EqualTo(1));
            Assert.That(buildings.All(value =>
                value.InitialBlocked && value.HitPoints > 0f && value.BuildingGrade > 0), Is.True);

            for (var bandIndex = 0; bandIndex < layout.WallRadii.Count - 1; bandIndex++)
            {
                var innerRadius = layout.WallRadii[bandIndex];
                var outerRadius = layout.WallRadii[bandIndex + 1];
                Assert.That(buildings.Where(value => value.DefenseLayer == bandIndex + 1)
                    .All(value => value.Coordinates.DistanceFromOrigin > innerRadius &&
                                  value.Coordinates.DistanceFromOrigin < outerRadius), Is.True);
                Assert.That(buildings.Any(value =>
                    value.DefenseLayer == bandIndex + 1 &&
                    value.Coordinates.DistanceFromOrigin == innerRadius + 1 &&
                    value.PlacementDensity == HexCastlePlacementDensity.Dense), Is.True);
                if (outerRadius - innerRadius > 2)
                {
                    Assert.That(buildings.Any(value =>
                        value.DefenseLayer == bandIndex + 1 &&
                        value.Coordinates.DistanceFromOrigin > innerRadius + 1 &&
                        value.PlacementDensity == HexCastlePlacementDensity.Sparse), Is.True);
                }
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void ThemeOne_FirstRowFillsEveryLegalCellBeforeSparseRow(int defenseLayerCount)
        {
            var tuning = HexCastleThemeOneTuning.CreateDraftDefaults();
            Assert.That(tuning.DenseOccupancy, Is.EqualTo(1f));

            foreach (var seed in new[] { 10801, 10802, 22017 })
            {
                var layout = new HexCastleFoundationGenerator().Generate(
                    seed,
                    defenseLayerCount,
                    HexCastleTheme.CentralCompartment,
                    tuning);
                var buildings = layout.Cells.Values.Where(cell => cell.IsBuildingCell).ToArray();
                var openGateApproaches = new HashSet<HexCoordinates>();
                foreach (var gate in layout.Enumerate(HexCastleCellKind.Gate).Where(cell =>
                             cell.GateRole == HexCastleGateRole.OpenDefenderPassage))
                {
                    for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
                    {
                        if ((gate.GatePassageMask & 1 << direction) != 0)
                        {
                            openGateApproaches.Add(gate.Coordinates.Neighbor(direction));
                        }
                    }
                }

                var barracksNeighborCells = new HashSet<HexCoordinates>(
                    buildings.Where(cell =>
                            cell.BuildingRole == HexCastleBuildingRole.KnightBarracks ||
                            cell.BuildingRole == HexCastleBuildingRole.FarmerBarracks)
                        .SelectMany(barracks =>
                            HexCoordinates.Directions.Select(direction =>
                                barracks.Coordinates + direction)));

                for (var bandIndex = 0; bandIndex < layout.WallRadii.Count - 1; bandIndex++)
                {
                    var innerRadius = layout.WallRadii[bandIndex];
                    var denseRadius = innerRadius + 1;
                    var unexplainedFirstRowGaps = layout.Cells.Values
                        .Where(cell =>
                            cell.Coordinates.DistanceFromOrigin == denseRadius &&
                            cell.Kind == HexCastleCellKind.Ground &&
                            !openGateApproaches.Contains(cell.Coordinates) &&
                            !barracksNeighborCells.Contains(cell.Coordinates))
                        .Select(cell => cell.Coordinates)
                        .ToArray();
                    Assert.That(
                        unexplainedFirstRowGaps,
                        Is.Empty,
                        $"Seed {seed}, {defenseLayerCount}중벽, 방어선 {bandIndex + 1}의 첫 열에 불필요한 빈 칸이 있습니다.");

                    if (layout.WallRadii[bandIndex + 1] - innerRadius <= 2)
                    {
                        continue;
                    }

                    var denseBuildingCount = buildings.Count(cell =>
                        cell.DefenseLayer == bandIndex + 1 &&
                        cell.Coordinates.DistanceFromOrigin == denseRadius);
                    var sparseBuildingCount = buildings.Count(cell =>
                        cell.DefenseLayer == bandIndex + 1 &&
                        cell.Coordinates.DistanceFromOrigin > denseRadius);
                    Assert.That(
                        denseBuildingCount,
                        Is.GreaterThan(sparseBuildingCount),
                        $"Seed {seed}, {defenseLayerCount}중벽, 방어선 {bandIndex + 1}은 첫 열을 먼저 채워야 합니다.");
                }
            }
        }

        [TestCase(2, 1, 0, 6, 4)]
        [TestCase(3, 2, 1, 6, 3)]
        [TestCase(4, 3, 2, 8, 4)]
        public void ThemeOne_BarracksAndTurretsFollowBandAndCombatRules(
            int defenseLayerCount,
            int expectedKnightBarracks,
            int expectedFarmerBarracks,
            int expectedTurrets,
            int expectedInnerBandTurrets)
        {
            foreach (var seed in new[] { 10801, 10802, 22017 })
            {
                var layout = new HexCastleFoundationGenerator().Generate(
                    seed,
                    defenseLayerCount,
                    HexCastleTheme.CentralCompartment);
                var buildings = layout.Cells.Values.Where(value => value.IsBuildingCell).ToArray();
                var barracks = buildings.Where(value =>
                    value.BuildingRole == HexCastleBuildingRole.KnightBarracks ||
                    value.BuildingRole == HexCastleBuildingRole.FarmerBarracks).ToArray();
                var turrets = buildings.Where(value =>
                    value.BuildingRole == HexCastleBuildingRole.Turret).ToArray();

                Assert.That(barracks.Count(value =>
                    value.BuildingRole == HexCastleBuildingRole.KnightBarracks),
                    Is.EqualTo(expectedKnightBarracks));
                Assert.That(barracks.Count(value =>
                    value.BuildingRole == HexCastleBuildingRole.FarmerBarracks),
                    Is.EqualTo(expectedFarmerBarracks));
                var palaceGuard = barracks.Where(value =>
                    value.BuildingRole == HexCastleBuildingRole.KnightBarracks &&
                    value.DefenseLayer == 0 &&
                    value.Coordinates.DistanceFromOrigin ==
                    HexCastleFoundationGenerator.PalaceFootprintRadius + 1).ToArray();
                Assert.That(palaceGuard.Length, Is.EqualTo(1));
                Assert.That(barracks.Except(palaceGuard).All(value => value.DefenseLayer >= 2), Is.True);
                Assert.That(barracks.All(value => HexCoordinates.Directions.Count(direction =>
                    layout.TryGetCell(value.Coordinates + direction, out var neighbor) &&
                    neighbor.Kind == HexCastleCellKind.Ground &&
                    neighbor.IsOpen) >= 2), Is.True);
                var minimumBarracksSeparation = HexCastleThemeOneTuning.CreateDraftDefaults()
                    .MinimumBarracksSeparationCells;
                for (var left = 0; left < barracks.Length; left++)
                {
                    for (var right = left + 1; right < barracks.Length; right++)
                    {
                        Assert.That(
                            barracks[left].Coordinates.DistanceTo(barracks[right].Coordinates),
                            Is.GreaterThanOrEqualTo(minimumBarracksSeparation),
                            $"Seed {seed}, {defenseLayerCount}중벽 병영류는 최소 {minimumBarracksSeparation}칸 떨어져야 합니다.");
                    }
                }

                Assert.That(turrets.Length, Is.EqualTo(expectedTurrets));
                var palaceGuardTurrets = turrets.Where(value =>
                    value.DefenseLayer == 0 &&
                    value.Coordinates.DistanceFromOrigin ==
                    HexCastleFoundationGenerator.PalaceFootprintRadius + 1).ToArray();
                Assert.That(
                    palaceGuardTurrets.Length,
                    Is.EqualTo(HexCastleFoundationGenerator.PalaceGuardTurretCount));
                Assert.That(palaceGuardTurrets.All(value =>
                    value.Coordinates.DistanceTo(palaceGuard[0].Coordinates) > 1), Is.True);
                Assert.That(turrets.Count(value => value.DefenseLayer == 1),
                    Is.EqualTo(expectedInnerBandTurrets));
                Assert.That(turrets.All(value =>
                    value.TurretRangeCells >= 2 &&
                    value.TurretRangeCells <= 4 &&
                    value.TurretCanAttackAcrossWalls), Is.True);
                Assert.That(turrets.Where(value =>
                        value.TurretWeaponKind == HexCastleTurretWeaponKind.Cannon ||
                        value.TurretWeaponKind == HexCastleTurretWeaponKind.Ballista)
                    .All(value => value.BuildingGrade <= 2), Is.True);
                Assert.That(turrets.Where(value =>
                        value.TurretWeaponKind == HexCastleTurretWeaponKind.Fireball)
                    .All(value => value.BuildingGrade <= 3), Is.True);
            }
        }

        [Test]
        public void ThemeOne_UsesOnlyApprovedExactBuildingVisuals()
        {
            var tuning = HexCastleThemeOneTuning.CreateDraftDefaults();
            Assert.That(tuning.BlockerVariants.Select(value => value.VisualVariantId),
                Is.EquivalentTo(new[]
                {
                    "building_windmill_blue",
                    "building_shrine_blue",
                    "building_home_B_blue",
                    "building_home_A_blue",
                    "building_townhall_blue",
                    "building_stage_B",
                    "building_stage_C"
                }));

            var buildings = new HexCastleFoundationGenerator().Generate(
                    10801,
                    4,
                    HexCastleTheme.CentralCompartment,
                    tuning)
                .Cells.Values
                .Where(value => value.IsBuildingCell)
                .ToArray();
            Assert.That(buildings.Where(value => value.BuildingRole == HexCastleBuildingRole.KnightBarracks)
                .All(value => value.VisualVariantId == "building_barracks_blue"), Is.True);
            Assert.That(buildings.Where(value => value.BuildingRole == HexCastleBuildingRole.FarmerBarracks)
                .All(value => value.VisualVariantId == "building_tent_blue"), Is.True);
            Assert.That(buildings.Where(value => value.BuildingRole == HexCastleBuildingRole.TrainingYard)
                .All(value => value.VisualVariantId == "building_archeryrange_blue"), Is.True);
            Assert.That(buildings.Where(value => value.BuildingRole == HexCastleBuildingRole.Church)
                .All(value => value.VisualVariantId == "building_church_blue"), Is.True);
            Assert.That(buildings.Where(value => value.BuildingRole == HexCastleBuildingRole.GoldStorage)
                .All(value => value.VisualVariantId == "building_market_yellow"), Is.True);
            Assert.That(buildings.Where(value => value.BuildingRole == HexCastleBuildingRole.EquipmentForge)
                .All(value => value.VisualVariantId == "building_blacksmith_green"), Is.True);
            Assert.That(buildings.Where(value => value.BuildingRole == HexCastleBuildingRole.KeyVault)
                .All(value => value.VisualVariantId == "building_mine_red"), Is.True);
            Assert.That(buildings.Where(value => value.BuildingRole == HexCastleBuildingRole.Turret)
                .All(value => value.VisualVariantId == "building_tower_base_blue" &&
                              value.TurretWeaponKind != HexCastleTurretWeaponKind.None &&
                              value.BuildingGrade >= 1 && value.BuildingGrade <= 3 &&
                              value.TurretRangeCells >= 2 && value.TurretRangeCells <= 4 &&
                              value.TurretCanAttackAcrossWalls), Is.True);
            Assert.That(buildings.Select(value => value.VisualVariantId),
                Has.None.EqualTo("resource_stone"));
            Assert.That(buildings.Select(value => value.VisualVariantId),
                Has.None.EqualTo("building_well_blue"));
            Assert.That(buildings.Select(value => value.VisualVariantId),
                Has.None.EqualTo("building_scaffolding"));
            Assert.That(buildings.Select(value => value.VisualVariantId),
                Has.None.EqualTo("building_tower_catapult_blue"));
            Assert.That(buildings.Select(value => value.VisualVariantId),
                Has.None.EqualTo("building_tower_B_blue"));
            Assert.That(buildings.Select(value => value.VisualVariantId),
                Has.None.EqualTo("building_tower_cannon_blue"));
        }

        [Test]
        public void ThemeOne_PalaceRingHasOneGuardBarracksAndTwoGuardTurrets()
        {
            var layout = new HexCastleFoundationGenerator().Generate(
                10801,
                4,
                HexCastleTheme.CentralCompartment);
            var palaceCells = layout.Enumerate(HexCastleCellKind.Palace).ToArray();
            var center = palaceCells.Single(value => value.Coordinates.DistanceFromOrigin == 0);

            Assert.That(layout.PalaceRadius, Is.EqualTo(1));
            Assert.That(layout.WallRadii, Is.EqualTo(new[] { 3, 5, 8, 11 }));
            Assert.That(palaceCells.Length, Is.EqualTo(7));
            Assert.That(palaceCells.All(value =>
                value.Coordinates.DistanceFromOrigin <= 1 &&
                value.InitialBlocked &&
                value.HitPoints == HexCastleParityContract.PalaceHealth), Is.True);
            Assert.That(palaceCells.Sum(value => value.RewardValue), Is.EqualTo(500));
            Assert.That(center.VisualVariantId, Is.EqualTo("building_castle_blue"));
            Assert.That(palaceCells.Where(value => value != center)
                .All(value => value.VisualVariantId == "PalaceFootprint"), Is.True);
            var innerRing = HexCoordinates.EnumerateRing(2)
                .Select(coordinates => layout.Cells[coordinates])
                .ToArray();
            Assert.That(innerRing.Count(value =>
                value.BuildingRole == HexCastleBuildingRole.KnightBarracks &&
                value.DefenseLayer == 0), Is.EqualTo(1));
            Assert.That(innerRing.Count(value =>
                value.BuildingRole == HexCastleBuildingRole.Turret &&
                value.DefenseLayer == 0), Is.EqualTo(2));
            Assert.That(innerRing.Count(value => value.Kind == HexCastleCellKind.Ground), Is.EqualTo(9));
        }

        [TestCase(3, 18, 12, 6)]
        [TestCase(5, 30, 24, 6)]
        [TestCase(8, 48, 42, 6)]
        [TestCase(11, 66, 60, 6)]
        public void WallRing_ResolvesDeterministicallyAndReversesWithoutChange(
            int radius,
            int expectedCells,
            int expectedStraight,
            int expectedCornerA)
        {
            var path = HexCoordinates.EnumerateRing(radius).ToArray();
            var resolved = ResolvePath(path);
            var reversed = ResolvePath(path.Reverse().ToArray());

            Assert.That(path.Length, Is.EqualTo(expectedCells));
            Assert.That(resolved.Values.Count(value => value.VisualKind == HexCastleWallVisualKind.Straight),
                Is.EqualTo(expectedStraight));
            Assert.That(resolved.Values.Count(value => value.VisualKind == HexCastleWallVisualKind.CornerAOutside),
                Is.EqualTo(expectedCornerA));
            foreach (var pair in resolved)
            {
                Assert.That(reversed[pair.Key].VisualKind, Is.EqualTo(pair.Value.VisualKind));
                Assert.That(reversed[pair.Key].RotationStep, Is.EqualTo(pair.Value.RotationStep));
            }
        }

        private HexCastleCellRuntime CreateRuntime(HexCastleCell cell)
        {
            var root = new GameObject($"Cell_{cell.Coordinates.Q}_{cell.Coordinates.R}");
            owned.Add(root);
            var runtime = root.AddComponent<HexCastleCellRuntime>();
            var tile = CreateChild("TileVisualRoot", root.transform);
            var content = CreateChild("ContentVisualRoot", root.transform);
            if (!cell.InitialBlocked)
            {
                runtime.Configure(cell, null, null, tile, content);
                return runtime;
            }

            var health = root.AddComponent<HealthComponent>();
            var collider = root.AddComponent<BoxCollider>();
            runtime.Configure(cell, health, collider, tile, content);
            return runtime;
        }

        private static Transform CreateChild(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Dictionary<HexCoordinates, HexCastleWallVisualResolution> ResolvePath(
            IReadOnlyList<HexCoordinates> path)
        {
            var result = new Dictionary<HexCoordinates, HexCastleWallVisualResolution>();
            for (var index = 0; index < path.Count; index++)
            {
                var current = path[index];
                result[current] = HexCastleWallVisualResolver.Resolve(
                    HexCastleCellKind.Wall,
                    path[(index - 1 + path.Count) % path.Count],
                    current,
                    path[(index + 1) % path.Count],
                    HexCastleWallCurvePlacement.Outside);
            }

            return result;
        }
    }
}
