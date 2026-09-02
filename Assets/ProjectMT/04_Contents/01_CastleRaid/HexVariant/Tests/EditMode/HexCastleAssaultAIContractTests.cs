using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.CastleRaidHex.Tests
{
    public sealed class HexCastleAssaultAIContractTests
    {
        private readonly List<GameObject> owned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var index = owned.Count - 1; index >= 0; index--)
            {
                if (owned[index] != null)
                {
                    Object.DestroyImmediate(owned[index]);
                }
            }
            owned.Clear();
        }

        [Test]
        public void Navigation_SelectsOuterLayerBeforeInnerLayerAndStopsAtPalacePerimeter()
        {
            var cells = CreateTwoLayerBoard();
            var navigation = new HexCastleAssaultNavigationSnapshot(cells, 1f);
            var start = HexCoordinates.Directions[0] * 7;

            Assert.That(navigation.TryResolveRoute(
                start,
                HexCastleAssaultRoutePolicy.Balanced,
                2,
                50f,
                3f,
                1,
                out var route), Is.True);

            Assert.That(route.HasFirstObstacle, Is.True);
            Assert.That(cells[route.FirstObstacle].DefenseLayer, Is.EqualTo(2));
            Assert.That(cells[route.FirstObstacle].WallRole, Is.EqualTo(HexCastleWallRole.OuterPerimeter));
            Assert.That(route.DestinationApproach.DistanceFromOrigin, Is.EqualTo(2));
            Assert.That(route.Path.All(value => value.DistanceFromOrigin > 1), Is.True);
        }

        [Test]
        public void Navigation_AfterOuterBreachTargetsExactNextInnerLayer()
        {
            var cells = CreateTwoLayerBoard();
            var start = HexCoordinates.Directions[0] * 7;
            var firstNavigation = new HexCastleAssaultNavigationSnapshot(cells, 1f);
            Assert.That(firstNavigation.TryResolveRoute(
                start,
                HexCastleAssaultRoutePolicy.Balanced,
                2,
                50f,
                3f,
                1,
                out var outerRoute), Is.True);

            var outerWall = cells[outerRoute.FirstObstacle];
            Assert.That(outerWall.ApplyDamage(outerWall.MaxHealth, outerWall.transform.position), Is.True);
            var secondNavigation = new HexCastleAssaultNavigationSnapshot(cells, 1f);
            Assert.That(secondNavigation.TryResolveRoute(
                start,
                HexCastleAssaultRoutePolicy.Balanced,
                1,
                50f,
                3f,
                2,
                out var innerRoute), Is.True);

            Assert.That(innerRoute.HasFirstObstacle, Is.True);
            Assert.That(cells[innerRoute.FirstObstacle].DefenseLayer, Is.EqualTo(1));
            Assert.That(innerRoute.Path, Does.Contain(outerWall.Coordinates));
        }

        [Test]
        public void Navigation_ReusesOneCostFieldUntilTopologyInvalidation()
        {
            var cells = CreateTwoLayerBoard();
            var navigation = new HexCastleAssaultNavigationSnapshot(cells, 1f);
            var start = HexCoordinates.Directions[0] * 7;

            for (var index = 0; index < 30; index++)
            {
                Assert.That(navigation.TryResolveRoute(
                    start,
                    HexCastleAssaultRoutePolicy.Balanced,
                    2,
                    50f,
                    3f,
                    1,
                    out _), Is.True);
            }

            Assert.That(navigation.CachedFieldCount, Is.EqualTo(1));
            navigation.Invalidate();
            Assert.That(navigation.CachedFieldCount, Is.Zero);
        }

        [Test]
        public void CellRuntime_PreservesRingAndPartitionWallRolesForAI()
        {
            var ring = CreateRuntime(new HexCastleCell(
                new HexCoordinates(3, 0),
                HexCastleCellKind.Wall,
                defenseLayer: 1,
                hitPoints: 100f,
                wallRole: HexCastleWallRole.CoreDefense,
                initialBlocked: true));
            var partition = CreateRuntime(new HexCastleCell(
                new HexCoordinates(2, 0),
                HexCastleCellKind.Gate,
                defenseLayer: 1,
                hitPoints: 80f,
                wallRole: HexCastleWallRole.Partition,
                initialBlocked: true));

            Assert.That(ring.WallRole, Is.EqualTo(HexCastleWallRole.CoreDefense));
            Assert.That(partition.WallRole, Is.EqualTo(HexCastleWallRole.Partition));
        }

        [Test]
        public void ProfileCatalog_ContainsCurrentHexMonsterPolicyValues()
        {
            var catalog = Resources.Load<HexCastleAssaultAIProfileCatalog>(
                HexCastleAssaultAIProfileCatalog.DefaultResourcesPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryValidate(out var error), Is.True, error);
            Assert.That(catalog.Entries.Count, Is.EqualTo(41));
            Assert.That(catalog.Resolve("aru_01").Pattern, Is.EqualTo(HexCastleAssaultPattern.TacticalSupport));
            Assert.That(catalog.Resolve("aru_01").SupportFocus, Is.EqualTo(HexCastleAssaultSupportFocus.DefenseBuff));
            Assert.That(catalog.Resolve("chamchi_01").Pattern, Is.EqualTo(HexCastleAssaultPattern.DefenderHunter));
            Assert.That(catalog.Resolve("castley_01").Pattern, Is.EqualTo(HexCastleAssaultPattern.WallBreaker));
            Assert.That(catalog.Resolve("floria_01").Pattern, Is.EqualTo(HexCastleAssaultPattern.TacticalSupport));
        }

        [Test]
        public void AIPresentation_ExplainsEveryAssignedPatternAndSupportFocus()
        {
            var catalog = Resources.Load<HexCastleAssaultAIProfileCatalog>(
                HexCastleAssaultAIProfileCatalog.DefaultResourcesPath);

            Assert.That(catalog, Is.Not.Null);
            foreach (var profile in catalog.Entries)
            {
                Assert.That(HexCastleAssaultAIPresentation.ResolveTag(profile), Is.Not.Empty);
                Assert.That(HexCastleAssaultAIPresentation.ResolveDescription(profile), Is.Not.Empty);
            }
            Assert.That(HexCastleAssaultAIPresentation.ResolveTag(catalog.Resolve("floria_01")),
                Is.EqualTo("회복 지원"));
            Assert.That(HexCastleAssaultAIPresentation.ResolveTag(catalog.Resolve("lucy_01")),
                Is.EqualTo("공격 지원"));
        }

        [Test]
        public void AssaultWorld_SequentialUnitsChooseOneOfNearestThreeOuterWalls()
        {
            var cells = CreateTwoLayerBoard();
            var worldObject = new GameObject("HexAssaultWorld");
            owned.Add(worldObject);
            var world = worldObject.AddComponent<HexCastleAssaultWorld>();
            world.Configure(cells, 1f, 2, null);
            var start = HexCoordinates.Directions[0] * 7;
            var first = CreateAssaultUnit(world, cells, start, "castley_01");
            var second = CreateAssaultUnit(world, cells, start, "castley_01");

            first.RefreshStrategicDecision();
            second.RefreshStrategicDecision();

            Assert.That(first.CurrentTarget.IsValid, Is.True);
            Assert.That(second.CurrentTarget.IsValid, Is.True);
            Assert.That(first.CurrentTarget.Structure.DefenseLayer, Is.EqualTo(2));
            var nearestThree = cells.Values
                .Where(value => value != null && value.IsAlive && value.DefenseLayer == 2 &&
                                value.WallRole != HexCastleWallRole.Partition)
                .OrderBy(value => start.DistanceTo(value.Coordinates))
                .ThenBy(value => value.Coordinates)
                .Take(3)
                .ToArray();
            Assert.That(nearestThree, Does.Contain(first.CurrentTarget.Structure));
            Assert.That(nearestThree, Does.Contain(second.CurrentTarget.Structure));
            Assert.That(first.CurrentIntent, Is.EqualTo(HexCastleAssaultIntentKind.InitialBreach));
            Assert.That(second.CurrentIntent, Is.EqualTo(HexCastleAssaultIntentKind.InitialBreach));
            Assert.That(second.CurrentTarget.Structure, Is.EqualTo(first.CurrentTarget.Structure),
                "합류 가능한 후발 유닛은 기존 공격대의 돌파 벽을 실제로 재사용해야 합니다.");
            Assert.That(second.RouteId, Is.EqualTo(first.RouteId));
            Assert.That(world.ActiveCohortCount, Is.EqualTo(1));
            Assert.That(world.ActiveBreachReservationOwnerCount, Is.EqualTo(2));

            world.UnregisterUnit(first);
            Assert.That(world.ActiveCohortCount, Is.EqualTo(1));
            Assert.That(world.ActiveBreachReservationOwnerCount, Is.EqualTo(1));
            Assert.That(world.ActiveOuterBreachRouteCount, Is.EqualTo(1));
            world.UnregisterUnit(second);
            Assert.That(world.ActiveCohortCount, Is.EqualTo(0));
            Assert.That(world.ActiveBreachReservationOwnerCount, Is.Zero);
            Assert.That(world.ActiveOuterBreachRouteCount, Is.Zero,
                "생존 소유자가 없는 돌파 예약은 즉시 반환해야 합니다.");
        }

        [Test]
        public void AssaultWorld_PartialDamageInvalidatesRouteOnlyWhenHealthBandChanges()
        {
            var cells = CreateTwoLayerBoard();
            var worldObject = new GameObject("HexAssaultWorld");
            owned.Add(worldObject);
            var world = worldObject.AddComponent<HexCastleAssaultWorld>();
            world.Configure(cells, 1f, 2, null);
            var unit = CreateAssaultUnit(
                world,
                cells,
                HexCoordinates.Directions[0] * 7,
                "kimhyeona_01");
            unit.RefreshStrategicDecision();
            var wall = unit.CurrentTarget.Structure;
            var fullHealthTopology = world.TopologyVersion;

            Assert.That(wall.ApplyDamage(20f, wall.transform.position), Is.True);
            Assert.That(world.TopologyVersion, Is.EqualTo(fullHealthTopology),
                "같은 25% 체력 구간의 작은 피해는 경로장을 매번 폐기하면 안 됩니다.");

            Assert.That(wall.ApplyDamage(30f, wall.transform.position), Is.True);
            Assert.That(world.TopologyVersion, Is.GreaterThan(fullHealthTopology),
                "벽 체력이 다음 비용 구간으로 내려가면 경로 비용을 다시 계산해야 합니다.");
            Assert.That(world.CachedRouteFieldCount, Is.Zero);
            Assert.That(unit.NeedsStrategicDecision, Is.True);
        }

        [Test]
        public void AssaultWorld_DestroyedCellChangesTopologyAndRetargetsNextLayer()
        {
            var cells = CreateTwoLayerBoard();
            var worldObject = new GameObject("HexAssaultWorld");
            owned.Add(worldObject);
            var world = worldObject.AddComponent<HexCastleAssaultWorld>();
            world.Configure(cells, 1f, 2, null);
            var unit = CreateAssaultUnit(
                world,
                cells,
                HexCoordinates.Directions[0] * 7,
                "kimhyeona_01");
            unit.RefreshStrategicDecision();
            var outerWall = unit.CurrentTarget.Structure;
            var previousTopology = world.TopologyVersion;

            Assert.That(outerWall.ApplyDamage(outerWall.MaxHealth, outerWall.transform.position), Is.True);
            Assert.That(world.TopologyVersion, Is.GreaterThan(previousTopology));
            Assert.That(unit.NeedsStrategicDecision, Is.True);
            unit.RefreshStrategicDecision();

            Assert.That(unit.CurrentTarget.IsValid, Is.True);
            Assert.That(unit.CurrentTarget.Structure.DefenseLayer, Is.EqualTo(1));
        }

        [Test]
        public void ResourceRaider_SelectsReachableNearbyRewardBuildingAfterInitialBreach()
        {
            var cells = CreateTwoLayerBoard();
            var start = HexCoordinates.Directions[0] * 7;
            var buildingCoordinates = new HexCoordinates(6, 1);
            var building = CreateRuntime(new HexCastleCell(
                buildingCoordinates,
                HexCastleCellKind.RewardBuilding,
                hitPoints: 90f,
                rewardValue: 100,
                initialBlocked: true,
                lootKind: HexCastleLootKind.Gold,
                placementId: "BUILDING_PRIORITY",
                buildingRole: HexCastleBuildingRole.GoldStorage,
                placementDensity: HexCastlePlacementDensity.Sparse,
                buildingGrade: 1));
            cells[buildingCoordinates] = building;
            var worldObject = new GameObject("HexAssaultWorld");
            owned.Add(worldObject);
            var world = worldObject.AddComponent<HexCastleAssaultWorld>();
            world.Configure(cells, 1f, 2, null);
            var unit = CreateAssaultUnit(world, cells, start, "dubi_01");

            unit.RefreshStrategicDecision();
            var initialWall = unit.CurrentTarget.Structure;
            initialWall.ApplyDamage(initialWall.MaxHealth, initialWall.transform.position);
            unit.RefreshStrategicDecision();

            Assert.That(unit.AIProfile.Pattern, Is.EqualTo(HexCastleAssaultPattern.ResourceRaider));
            Assert.That(unit.CurrentTarget.Structure, Is.EqualTo(building));
        }

        [Test]
        public void RecentTurretThreat_TemporarilyOverridesBalancedRouteTarget()
        {
            var cells = CreateTwoLayerBoard();
            var start = HexCoordinates.Directions[0] * 7;
            var turretCoordinates = new HexCoordinates(6, 1);
            var turret = CreateRuntime(new HexCastleCell(
                turretCoordinates,
                HexCastleCellKind.DefenseBuilding,
                hitPoints: 120f,
                initialBlocked: true,
                placementId: "RECENT_TURRET",
                buildingRole: HexCastleBuildingRole.Turret,
                placementDensity: HexCastlePlacementDensity.Sparse,
                buildingGrade: 1,
                turretWeaponKind: HexCastleTurretWeaponKind.Ballista,
                turretRangeCells: 3,
                turretCanAttackAcrossWalls: true));
            cells[turretCoordinates] = turret;
            var worldObject = new GameObject("HexAssaultWorld");
            owned.Add(worldObject);
            var world = worldObject.AddComponent<HexCastleAssaultWorld>();
            world.Configure(cells, 1f, 2, null);
            var unit = CreateAssaultUnit(world, cells, start, "kimhyeona_01");

            unit.ApplyDamage(5f, unit.transform.position, null, turret);
            unit.RefreshStrategicDecision();

            Assert.That(unit.AIProfile.Pattern, Is.EqualTo(HexCastleAssaultPattern.GeneralAdvance));
            Assert.That(unit.CurrentTarget.Structure, Is.EqualTo(turret));
        }

        [Test]
        public void LongRangeThreatBehindIntactWall_CannotOverrideRequiredBreach()
        {
            var cells = CreateTwoLayerBoard();
            var start = HexCoordinates.Directions[0] * 7;
            var turretCoordinates = new HexCoordinates(4, 0);
            var turret = CreateRuntime(new HexCastleCell(
                turretCoordinates,
                HexCastleCellKind.DefenseBuilding,
                hitPoints: 120f,
                initialBlocked: true,
                placementId: "INNER_TURRET",
                buildingRole: HexCastleBuildingRole.Turret,
                placementDensity: HexCastlePlacementDensity.Dense,
                buildingGrade: 1,
                turretWeaponKind: HexCastleTurretWeaponKind.Ballista,
                turretRangeCells: 3,
                turretCanAttackAcrossWalls: true));
            cells[turretCoordinates] = turret;
            var worldObject = new GameObject("HexAssaultWorld");
            owned.Add(worldObject);
            var world = worldObject.AddComponent<HexCastleAssaultWorld>();
            world.Configure(cells, 1f, 2, null, null, 41);
            var unit = CreateAssaultUnit(world, cells, start, "kimhyeona_01", 4f);

            unit.ApplyDamage(5f, unit.transform.position, null, turret);
            unit.RefreshStrategicDecision();

            Assert.That(unit.CurrentIntent, Is.EqualTo(HexCastleAssaultIntentKind.InitialBreach));
            Assert.That(unit.CurrentTarget.Structure, Is.Not.EqualTo(turret));
            Assert.That(unit.CurrentTarget.Structure.DefenseLayer, Is.EqualTo(2));
            Assert.That(world.IsAttackLaneOpen(
                new HexCoordinates(6, 0),
                new HexCastleAssaultTarget(turret, false)), Is.False);

            var separatingWall = cells[new HexCoordinates(5, 0)];
            separatingWall.ApplyDamage(separatingWall.MaxHealth, separatingWall.transform.position);
            Assert.That(world.IsAttackLaneOpen(
                new HexCoordinates(6, 0),
                new HexCastleAssaultTarget(turret, false)), Is.True);
        }

        [Test]
        public void AttackSlotLease_AssignsDifferentApproachesToSameWall()
        {
            var cells = CreateTwoLayerBoard();
            var worldObject = new GameObject("HexAssaultWorld");
            owned.Add(worldObject);
            var world = worldObject.AddComponent<HexCastleAssaultWorld>();
            world.Configure(cells, 1f, 2, null);
            var start = HexCoordinates.Directions[0] * 7;
            var first = CreateAssaultUnit(world, cells, start, "kimhyeona_01");
            var second = CreateAssaultUnit(world, cells, start, "kimhyeona_01");

            Assert.That(world.TryResolveDecision(first, out var firstDecision), Is.True);
            Assert.That(world.TryResolveDecision(second, out var secondDecision), Is.True);
            Assert.That(firstDecision.Target.Structure, Is.EqualTo(secondDecision.Target.Structure));
            Assert.That(firstDecision.Approach, Is.Not.EqualTo(secondDecision.Approach));
        }

        [Test]
        public void TacticalSupport_SelectsAndHealsDamagedNearbyAlly()
        {
            var cells = CreateTwoLayerBoard();
            var worldObject = new GameObject("HexAssaultWorld");
            owned.Add(worldObject);
            var world = worldObject.AddComponent<HexCastleAssaultWorld>();
            world.Configure(cells, 1f, 2, null);
            var start = HexCoordinates.Directions[0] * 7;
            var support = CreateAssaultUnit(world, cells, start, "floria_01");
            var ally = CreateAssaultUnit(world, cells, start.Neighbor(2), "kimhyeona_01");
            ally.ApplyDamage(50f);
            var damagedHealth = ally.CurrentHealth;

            Assert.That(world.TryResolveSupportDecision(support, out var target, out var action), Is.True);
            Assert.That(target, Is.EqualTo(ally));
            Assert.That(action, Is.EqualTo(HexCastleAssaultSupportAction.Heal));
            target.ApplySupport(action, support.AIProfile);
            Assert.That(ally.CurrentHealth, Is.GreaterThan(damagedHealth));
        }

        [Test]
        public void TacticalSupport_TentativeClaimPreventsDuplicateTargetActionPileup()
        {
            var cells = CreateTwoLayerBoard();
            var worldObject = new GameObject("HexAssaultWorld");
            owned.Add(worldObject);
            var world = worldObject.AddComponent<HexCastleAssaultWorld>();
            world.Configure(cells, 1f, 2, null, null, 331);
            var start = HexCoordinates.Directions[0] * 7;
            var firstSupport = CreateAssaultUnit(world, cells, start, "floria_01");
            var secondSupport = CreateAssaultUnit(world, cells, start.Neighbor(2), "floria_01");
            var firstAlly = CreateAssaultUnit(world, cells, start.Neighbor(1), "kimhyeona_01");
            var secondAlly = CreateAssaultUnit(world, cells, start.Neighbor(3), "phoenix_01");
            firstAlly.ApplyDamage(50f);
            secondAlly.ApplyDamage(50f);

            Assert.That(world.TryResolveSupportDecision(
                firstSupport,
                out var firstTarget,
                out var firstAction), Is.True);
            Assert.That(world.TryResolveSupportDecision(
                secondSupport,
                out var secondTarget,
                out var secondAction), Is.True);

            Assert.That(firstAction, Is.Not.EqualTo(HexCastleAssaultSupportAction.None));
            Assert.That(secondAction, Is.Not.EqualTo(HexCastleAssaultSupportAction.None));
            Assert.That(secondTarget == firstTarget && secondAction == firstAction, Is.False,
                "동시에 판단한 지원형 둘이 같은 대상의 같은 효과에 겹치면 안 됩니다.");
            Assert.That(world.ActiveSupportClaimCount, Is.EqualTo(2));
        }

        [Test]
        public void InitialBreach_UsesNearestThreeWithDescendingWeightedFrequency()
        {
            var cells = CreateTwoLayerBoard();
            var worldObject = new GameObject("HexAssaultWorld");
            owned.Add(worldObject);
            var world = worldObject.AddComponent<HexCastleAssaultWorld>();
            world.Configure(cells, 1f, 2, null, null, 19317);
            var start = HexCoordinates.Directions[0] * 7;
            var nearestThree = cells.Values
                .Where(value => value != null && value.IsAlive && value.DefenseLayer == 2 &&
                                value.WallRole != HexCastleWallRole.Partition)
                .OrderBy(value => start.DistanceTo(value.Coordinates))
                .ThenBy(value => value.Coordinates)
                .Take(3)
                .ToArray();
            var counts = new int[3];

            for (var index = 0; index < 180; index++)
            {
                var unit = CreateAssaultUnit(world, cells, start, "castley_01");
                unit.RefreshStrategicDecision();
                var selected = System.Array.IndexOf(nearestThree, unit.CurrentTarget.Structure);
                Assert.That(selected, Is.InRange(0, 2));
                counts[selected]++;
                world.UnregisterUnit(unit);
            }

            Assert.That(counts[0], Is.GreaterThan(counts[1]));
            Assert.That(counts[1], Is.GreaterThan(counts[2]));
            Assert.That(counts[0], Is.InRange(75, 120));
            Assert.That(counts[1], Is.InRange(35, 75));
            Assert.That(counts[2], Is.InRange(15, 45));
        }

        [Test]
        public void ThreatInterrupt_AfterThreatDiesResumesCommittedResourceTarget()
        {
            var cells = CreateTwoLayerBoard();
            var start = HexCoordinates.Directions[0] * 7;
            var resource = CreateRuntime(new HexCastleCell(
                new HexCoordinates(6, 1),
                HexCastleCellKind.RewardBuilding,
                hitPoints: 120f,
                rewardValue: 100,
                initialBlocked: true,
                lootKind: HexCastleLootKind.Gold,
                placementId: "GOLD_REWARD",
                buildingRole: HexCastleBuildingRole.GoldStorage,
                placementDensity: HexCastlePlacementDensity.Sparse,
                buildingGrade: 1));
            var turret = CreateRuntime(new HexCastleCell(
                new HexCoordinates(6, -1),
                HexCastleCellKind.DefenseBuilding,
                hitPoints: 80f,
                initialBlocked: true,
                placementId: "THREAT_TURRET",
                buildingRole: HexCastleBuildingRole.Turret,
                placementDensity: HexCastlePlacementDensity.Sparse,
                buildingGrade: 1,
                turretWeaponKind: HexCastleTurretWeaponKind.Ballista,
                turretRangeCells: 3,
                turretCanAttackAcrossWalls: true));
            cells[resource.Coordinates] = resource;
            cells[turret.Coordinates] = turret;
            var worldObject = new GameObject("HexAssaultWorld");
            owned.Add(worldObject);
            var world = worldObject.AddComponent<HexCastleAssaultWorld>();
            world.Configure(cells, 1f, 2, null, null, 13);
            var unit = CreateAssaultUnit(world, cells, start, "dubi_01");

            unit.RefreshStrategicDecision();
            var initialWall = unit.CurrentTarget.Structure;
            initialWall.ApplyDamage(initialWall.MaxHealth, initialWall.transform.position);
            unit.RefreshStrategicDecision();
            Assert.That(unit.CurrentTarget.Structure, Is.EqualTo(resource));
            Assert.That(unit.CommittedTarget.Structure, Is.EqualTo(resource));

            unit.ApplyDamage(5f, unit.transform.position, null, turret);
            unit.RefreshStrategicDecision();
            Assert.That(unit.CurrentIntent, Is.EqualTo(HexCastleAssaultIntentKind.Threat));
            Assert.That(unit.CurrentTarget.Structure, Is.EqualTo(turret));

            turret.ApplyDamage(turret.MaxHealth, turret.transform.position);
            unit.RefreshStrategicDecision();
            Assert.That(unit.CurrentTarget.Structure, Is.EqualTo(resource));
            Assert.That(unit.CurrentIntent, Is.EqualTo(HexCastleAssaultIntentKind.Specialist));
        }

        [Test]
        public void SharedThreat_NearbyGeneralInterruptsThenReturnsToCommittedWall()
        {
            var cells = CreateTwoLayerBoard();
            var start = HexCoordinates.Directions[0] * 7;
            var turret = CreateRuntime(new HexCastleCell(
                new HexCoordinates(6, 1),
                HexCastleCellKind.DefenseBuilding,
                hitPoints: 80f,
                initialBlocked: true,
                placementId: "SHARED_THREAT",
                buildingRole: HexCastleBuildingRole.Turret,
                placementDensity: HexCastlePlacementDensity.Sparse,
                buildingGrade: 1,
                turretWeaponKind: HexCastleTurretWeaponKind.Cannon,
                turretRangeCells: 3,
                turretCanAttackAcrossWalls: true));
            cells[turret.Coordinates] = turret;
            var worldObject = new GameObject("HexAssaultWorld");
            owned.Add(worldObject);
            var world = worldObject.AddComponent<HexCastleAssaultWorld>();
            world.Configure(cells, 1f, 2, null, null, 71);
            var victim = CreateAssaultUnit(world, cells, start, "kimhyeona_01");
            var responder = CreateAssaultUnit(world, cells, start.Neighbor(2), "phoenix_01");
            responder.RefreshStrategicDecision();
            var committedWall = responder.CommittedTarget.Structure;

            victim.ApplyDamage(5f, victim.transform.position, null, turret);
            responder.RefreshStrategicDecision();
            Assert.That(responder.CurrentIntent, Is.EqualTo(HexCastleAssaultIntentKind.Threat));
            Assert.That(responder.CurrentTarget.Structure, Is.EqualTo(turret));

            turret.ApplyDamage(turret.MaxHealth, turret.transform.position);
            responder.RefreshStrategicDecision();
            Assert.That(responder.CurrentTarget.Structure, Is.EqualTo(committedWall));
        }

        [Test]
        public void TacticalSupport_StrategicDecisionTargetsAllyInsteadOfWall()
        {
            var cells = CreateTwoLayerBoard();
            var worldObject = new GameObject("HexAssaultWorld");
            owned.Add(worldObject);
            var world = worldObject.AddComponent<HexCastleAssaultWorld>();
            world.Configure(cells, 1f, 2, null, null, 5);
            var start = HexCoordinates.Directions[0] * 7;
            var support = CreateAssaultUnit(world, cells, start, "floria_01");
            var ally = CreateAssaultUnit(world, cells, start.Neighbor(2), "kimhyeona_01");
            ally.ApplyDamage(40f);

            support.RefreshStrategicDecision();

            Assert.That(support.CurrentIntent, Is.EqualTo(HexCastleAssaultIntentKind.Support));
            Assert.That(support.CurrentTarget.Kind, Is.EqualTo(HexCastleAssaultTargetKind.Ally));
            Assert.That(support.CurrentTarget.Ally, Is.EqualTo(ally));
            Assert.That(support.CurrentSupportAction, Is.EqualTo(HexCastleAssaultSupportAction.Heal));
            Assert.That(support.HasSelectedInitialWall, Is.False);
        }

        [Test]
        public void TacticalSupport_AfterCastingResumesGeneralAdvanceDuringCooldown()
        {
            var cells = CreateTwoLayerBoard();
            var worldObject = new GameObject("HexAssaultWorld");
            owned.Add(worldObject);
            var world = worldObject.AddComponent<HexCastleAssaultWorld>();
            world.Configure(cells, 1f, 2, null, null, 13);
            var start = HexCoordinates.Directions[0] * 7;
            var support = CreateAssaultUnit(world, cells, start, "floria_01");
            var ally = CreateAssaultUnit(world, cells, start.Neighbor(2), "kimhyeona_01");
            ally.ApplyDamage(40f);

            support.RefreshStrategicDecision();
            typeof(HexCastleAssaultUnit)
                .GetMethod("TickSupportTarget", System.Reflection.BindingFlags.Instance |
                                                System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(support, null);

            Assert.That(support.CanPerformSupportAction, Is.False, "지원 직후 쿨다운 시작");
            Assert.That(world.TryResolveSupportDecision(support, out _, out _), Is.False,
                "쿨다운 중 아군 목표를 다시 잡아 제자리 대기하면 안 됩니다.");
            support.RefreshStrategicDecision();
            Assert.That(support.CurrentIntent, Is.EqualTo(HexCastleAssaultIntentKind.InitialBreach));
            Assert.That(support.CurrentTarget.Kind, Is.Not.EqualTo(HexCastleAssaultTargetKind.Ally));
        }

        [Test]
        public void TacticalSupport_DoesNotBuffAllyAcrossClosedWallRing()
        {
            var cells = CreateTwoLayerBoard();
            var worldObject = new GameObject("HexAssaultWorld");
            owned.Add(worldObject);
            var world = worldObject.AddComponent<HexCastleAssaultWorld>();
            world.Configure(cells, 1f, 2, null, null, 9);
            var support = CreateAssaultUnit(
                world,
                cells,
                HexCoordinates.Directions[0] * 7,
                "floria_01");
            var ally = CreateAssaultUnit(
                world,
                cells,
                HexCoordinates.Directions[0] * 4,
                "kimhyeona_01");
            ally.ApplyDamage(40f);

            support.RefreshStrategicDecision();

            Assert.That(support.CurrentTarget.Kind, Is.Not.EqualTo(HexCastleAssaultTargetKind.Ally));
            Assert.That(support.CurrentIntent, Is.EqualTo(HexCastleAssaultIntentKind.InitialBreach));
        }

        private Dictionary<HexCoordinates, HexCastleCellRuntime> CreateTwoLayerBoard()
        {
            var result = new Dictionary<HexCoordinates, HexCastleCellRuntime>();
            foreach (var coordinates in HexCoordinates.EnumerateRadius(7))
            {
                var distance = coordinates.DistanceFromOrigin;
                HexCastleCell cell;
                if (distance <= 1)
                {
                    cell = new HexCastleCell(
                        coordinates,
                        HexCastleCellKind.Palace,
                        hitPoints: 1000f,
                        initialBlocked: true,
                        placementId: $"PALACE_{coordinates.Q}_{coordinates.R}");
                }
                else if (distance == 3 || distance == 5)
                {
                    var outer = distance == 5;
                    cell = new HexCastleCell(
                        coordinates,
                        HexCastleCellKind.Wall,
                        defenseLayer: outer ? 2 : 1,
                        hitPoints: outer ? 180f : 140f,
                        wallRole: outer ? HexCastleWallRole.OuterPerimeter : HexCastleWallRole.CoreDefense,
                        initialBlocked: true,
                        placementId: $"WALL_{distance}_{coordinates.Q}_{coordinates.R}");
                }
                else
                {
                    cell = new HexCastleCell(
                        coordinates,
                        distance > 5 ? HexCastleCellKind.Deployment : HexCastleCellKind.Ground,
                        initialBlocked: false);
                }

                result.Add(coordinates, CreateRuntime(cell));
            }

            return result;
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
                runtime.Configure(cell, null, null, null, tile, content);
                return runtime;
            }

            var health = root.AddComponent<HealthComponent>();
            var collider = root.AddComponent<BoxCollider>();
            var obstacle = root.AddComponent<NavMeshObstacle>();
            runtime.Configure(cell, health, collider, obstacle, tile, content);
            return runtime;
        }

        private HexCastleAssaultUnit CreateAssaultUnit(
            HexCastleAssaultWorld world,
            IReadOnlyDictionary<HexCoordinates, HexCastleCellRuntime> cells,
            HexCoordinates start,
            string monsterId,
            float attackRange = 1.1f)
        {
            var root = new GameObject($"Assault_{monsterId}_{owned.Count}");
            owned.Add(root);
            var unit = root.AddComponent<HexCastleAssaultUnit>();
            unit.ConfigureForPartyUnit(
                world,
                start,
                cells,
                1f,
                Vector3.zero,
                new BattleUnitSnapshot(
                    monsterId,
                    new UnitStatsSnapshot
                    {
                        maxHealth = 100f,
                        damage = 20f,
                        moveSpeed = 3f,
                        attackRange = attackRange,
                        attackInterval = 1f
                    }));
            return unit;
        }

        private static Transform CreateChild(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }
    }
}
