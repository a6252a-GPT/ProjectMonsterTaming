using System.Collections.Generic;
using NUnit.Framework;
using ProjectMT.Contents.CastleRaid;
using ProjectMT.Contents.CastleRaid.Generation;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Tests.EditMode
{
    public sealed class CastleRaidAIContractTests
    {
        [Test]
        public void DefenseLayerConversion_TracksOutsideToPalaceInwardOrder()
        {
            Assert.That(CastleAssaultRouteMath.ResolveNextInternalLayer(-1, 4), Is.EqualTo(0));
            Assert.That(CastleAssaultRouteMath.ResolveDisplayLayer(0, 4), Is.EqualTo(4));
            Assert.That(CastleAssaultRouteMath.ResolveDisplayLayer(1, 4), Is.EqualTo(3));
            Assert.That(CastleAssaultRouteMath.ResolveDisplayLayer(3, 4), Is.EqualTo(1));
            Assert.That(CastleAssaultRouteMath.ResolveNextInternalLayer(3, 4), Is.EqualTo(-1));
        }

        [Test]
        public void BreachCrossing_RequiresInwardPassNearDestroyedWall()
        {
            var wall = Vector3.zero;
            var inward = Vector3.forward;

            Assert.That(CastleAssaultRouteMath.HasCrossedInward(
                new Vector3(0.2f, 0f, -0.5f),
                new Vector3(0.2f, 0f, 0.5f),
                wall,
                inward,
                0.9f), Is.True);
            Assert.That(CastleAssaultRouteMath.HasCrossedInward(
                new Vector3(2f, 0f, -0.5f),
                new Vector3(2f, 0f, 0.5f),
                wall,
                inward,
                0.9f), Is.False);
        }

        [Test]
        public void BreachInside_RecoversProgressWhenLinkTraversalSkippedTheCrossingSample()
        {
            Assert.That(CastleAssaultRouteMath.IsAtBreachInside(
                new Vector3(0.15f, 0f, 1.05f),
                Vector3.zero,
                Vector3.forward,
                0.9f), Is.True);
            Assert.That(CastleAssaultRouteMath.IsAtBreachInside(
                new Vector3(1.5f, 0f, 1.05f),
                Vector3.zero,
                Vector3.forward,
                0.9f), Is.False);
        }

        [Test]
        public void BreachDirection_UsesActualAttackSideWhenItStillAdvancesTowardPalace()
        {
            var wall = new Vector3(2.5f, 0f, 9.5f);
            var palace = Vector3.zero;

            Assert.That(
                CastleBreachLinkMath.ResolveInwardDirectionFromAttackApproach(
                    wall,
                    palace,
                    new Vector3(2.5f, 0f, 14.5f),
                    CastleWallNeighborMask.North | CastleWallNeighborMask.South),
                Is.EqualTo(Vector3.back));
            Assert.That(
                CastleBreachLinkMath.ResolveInwardDirectionFromAttackApproach(
                    wall,
                    palace,
                    new Vector3(7.5f, 0f, 9.5f),
                    CastleWallNeighborMask.North | CastleWallNeighborMask.South),
                Is.EqualTo(Vector3.left));
        }

        [Test]
        public void AdditionalBreach_RequiresLargeRouteSavings()
        {
            Assert.That(CastleAssaultRouteMath.ShouldOpenAdditionalBreach(6.4f, 10f, false), Is.True);
            Assert.That(CastleAssaultRouteMath.ShouldOpenAdditionalBreach(6.6f, 10f, false), Is.False);
            Assert.That(CastleAssaultRouteMath.ShouldOpenAdditionalBreach(7.7f, 10f, true), Is.True);
            Assert.That(CastleAssaultRouteMath.ShouldOpenAdditionalBreach(7.9f, 10f, true), Is.False);
        }

        [Test]
        public void AdditionalBreach_FallsBackOnlyWhenOpenedRouteIsUnavailable()
        {
            Assert.That(
                CastleAssaultRouteMath.ShouldOpenAdditionalBreach(4f, float.PositiveInfinity, false),
                Is.True);
            Assert.That(
                CastleAssaultRouteMath.ShouldOpenAdditionalBreach(float.PositiveInfinity, 4f, false),
                Is.False);
        }

        [Test]
        public void AdditionalBreach_StopsWhileAnotherBreachIsPendingOrRouteCapIsReached()
        {
            Assert.That(
                CastleAssaultRouteMath.ShouldOpenAdditionalBreach(2f, 10f, false, 1, 4, 1),
                Is.False);
            Assert.That(
                CastleAssaultRouteMath.ShouldOpenAdditionalBreach(2f, 10f, false, 4, 4, 0),
                Is.False);
            Assert.That(
                CastleAssaultRouteMath.ShouldOpenAdditionalBreach(2f, 10f, false, 3, 4, 0),
                Is.True);
        }

        [Test]
        public void PendingOuterBreach_PausesOnlyUntilFirstRouteExists()
        {
            Assert.That(CastleAssaultRouteMath.ShouldWaitForPendingBreach(0, 1), Is.True);
            Assert.That(CastleAssaultRouteMath.ShouldWaitForPendingBreach(1, 1), Is.False);
            Assert.That(CastleAssaultRouteMath.ShouldWaitForPendingBreach(0, 0), Is.False);
        }

        [Test]
        public void IncidentalBuilding_RequiresFinitePathInsideSmallClearRadius()
        {
            Assert.That(CastleAssaultRouteMath.IsIncidentalBuilding(2.34f, 2.35f), Is.True);
            Assert.That(CastleAssaultRouteMath.IsIncidentalBuilding(2.36f, 2.35f), Is.False);
            Assert.That(CastleAssaultRouteMath.IsIncidentalBuilding(float.PositiveInfinity, 2.35f), Is.False);
        }

        [Test]
        public void RouteTopologyCache_ReusesResultUntilTopologyChanges()
        {
            var cache = new CastleRaidRouteTopologyCache();

            cache.StoreContinuation(101, true);
            Assert.That(cache.TryGetContinuation(101, out var cached), Is.True);
            Assert.That(cached, Is.True);

            cache.Invalidate();
            Assert.That(cache.TryGetContinuation(101, out _), Is.False);
            cache.StoreContinuation(101, false);
            Assert.That(cache.TryGetContinuation(101, out cached), Is.True);
            Assert.That(cached, Is.False);

            cache.Reset();
            Assert.That(cache.CachedRouteCount, Is.Zero);
            Assert.That(cache.TryGetContinuation(101, out _), Is.False);
        }

        [Test]
        public void LogicalRoutePlanner_SelectsFirstObstacleAndAdvancesAfterDestruction()
        {
            var placements = new List<CastlePlacementData>
            {
                CreatePlacement("outer_wall", CastlePlacementKind.Wall, 2, 0, 1, 5, 0),
                CreatePlacement("inner_wall", CastlePlacementKind.Wall, 4, 0, 1, 5, 1),
                CreatePlacement("palace", CastlePlacementKind.Palace, 6, 2, 1, 1, 0)
            };
            var snapshot = new CastleRaidNavigationSnapshot(7, 5, 1f, placements);
            var planner = new CastleRaidRoutePlanner(snapshot);
            var spawn = snapshot.CellToWorld(new Vector2Int(0, 2));

            Assert.That(planner.TryResolveRoute(spawn, CastleRaidRoutePolicy.Balanced, out var first), Is.True);
            Assert.That(first.FirstObstaclePlacementId, Is.EqualTo("outer_wall"));
            Assert.That(first.FirstObstacleDefenseLayer, Is.Zero);

            Assert.That(planner.TryResolveRoute(spawn, CastleRaidRoutePolicy.Balanced, out _), Is.True);
            Assert.That(planner.FieldBuildCount, Is.EqualTo(1)); // 같은 지형은 공유 비용장을 재사용

            Assert.That(snapshot.NotifyDestroyed("outer_wall"), Is.True);
            Assert.That(planner.TryResolveRoute(spawn, CastleRaidRoutePolicy.Balanced, out var second), Is.True);
            Assert.That(second.FirstObstaclePlacementId, Is.EqualTo("inner_wall"));
            Assert.That(second.FirstObstacleDefenseLayer, Is.EqualTo(1));
            Assert.That(planner.FieldBuildCount, Is.EqualTo(2));
        }

        [Test]
        public void SequentialSpawnCoordinator_JoinsOnlyNearbySameRouteWithoutRewritingEarlierUnit()
        {
            var coordinator = new CastleRaidAssaultCoordinator();
            var route = new CastleRaidRoutePlan(
                101,
                0,
                Vector2Int.zero,
                Vector2Int.one,
                "outer_wall",
                0,
                10f);
            var oppositeRoute = new CastleRaidRoutePlan(
                101,
                4,
                Vector2Int.zero,
                Vector2Int.one,
                "outer_wall",
                0,
                10f);

            var first = coordinator.RegisterSequentialSpawn(1, Vector3.zero, route, 0f);
            var second = coordinator.RegisterSequentialSpawn(2, Vector3.right, route, 1f);
            var opposite = coordinator.RegisterSequentialSpawn(3, Vector3.right * 2f, oppositeRoute, 2f);

            Assert.That(second.CohortId, Is.EqualTo(first.CohortId));
            Assert.That(opposite.CohortId, Is.Not.EqualTo(first.CohortId));
            Assert.That(coordinator.TryGetAssignment(1, out var preserved), Is.True);
            Assert.That(preserved.CohortId, Is.EqualTo(first.CohortId));
            Assert.That(preserved.RouteId, Is.EqualTo(first.RouteId));
        }

        [Test]
        public void RouteBreachLedger_BlocksOnlyDuplicateWorkInsideSameRoute()
        {
            var ledger = new CastleRaidRouteBreachLedger();

            Assert.That(ledger.TryReserve(10, 100), Is.True);
            Assert.That(ledger.TryReserve(10, 100), Is.True);
            Assert.That(ledger.TryReserve(10, 101), Is.False);
            Assert.That(ledger.TryReserve(20, 200), Is.True); // 다른 방향의 순차 소환은 독립 돌파 가능
            Assert.That(ledger.Count, Is.EqualTo(2));

            ledger.ReleaseWall(100);
            Assert.That(ledger.TryReserve(10, 101), Is.True);
            Assert.That(ledger.Count, Is.EqualTo(2));
        }

        [Test]
        public void LogicalRoutePlanner_ThirtySequentialQueriesShareOneFiftyByFiftyCostField()
        {
            var placements = new List<CastlePlacementData>
            {
                CreatePlacement("outer_wall", CastlePlacementKind.Wall, 18, 0, 1, 50, 0),
                CreatePlacement("inner_wall", CastlePlacementKind.Wall, 32, 0, 1, 50, 1),
                CreatePlacement("palace", CastlePlacementKind.Palace, 42, 24, 2, 2, 0)
            };
            var snapshot = new CastleRaidNavigationSnapshot(50, 50, 1f, placements);
            var planner = new CastleRaidRoutePlanner(snapshot);

            for (var index = 0; index < 30; index++)
            {
                var spawn = snapshot.CellToWorld(new Vector2Int(index % 12, 2 + index));
                Assert.That(planner.TryResolveRoute(spawn, CastleRaidRoutePolicy.Balanced, out var route), Is.True);
                Assert.That(route.FirstObstaclePlacementId, Is.EqualTo("outer_wall"));
            }

            Assert.That(planner.FieldBuildCount, Is.EqualTo(1));
        }

        [Test]
        public void DefenseDamage_RemembersLiveAggressorForImmediateCounterattack()
        {
            var unitObject = new GameObject("AssaultUnit_AggroTest");
            var defenderObject = new GameObject("Defender_AggroTest");
            try
            {
                var unitHealth = unitObject.AddComponent<HealthComponent>();
                var unit = unitObject.AddComponent<CastleAssaultUnit>();
                typeof(CastleAssaultUnit).GetMethod(
                        "ResolveReferences",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(unit, null);
                unitHealth.Initialize(100f);

                defenderObject.AddComponent<HealthComponent>();
                var defender = defenderObject.AddComponent<CastleTarget>();
                defender.EditorConfigure(CastleTargetKind.Defender, 50f, null, null);
                defender.Initialize();

                unit.ApplyDefenseDamage(5f, unitObject.transform.position, defender);
                Assert.That(unit.RecentThreatAggressor, Is.SameAs(defender));

                defender.Health.ApplyDamage(new ProjectMT.Shared.Combat.DamageRequest(
                    null,
                    100f,
                    defenderObject.transform.position));
                Assert.That(unit.RecentThreatAggressor, Is.Null);
                defender.Shutdown();
            }
            finally
            {
                Object.DestroyImmediate(defenderObject);
                Object.DestroyImmediate(unitObject);
            }
        }

        [Test]
        public void TurretDamage_RemembersLinkedBuildingAsActiveThreat()
        {
            var unitObject = new GameObject("AssaultUnit_TurretAggroTest");
            var turretObject = new GameObject("Turret_AggroTest");
            try
            {
                var unitHealth = unitObject.AddComponent<HealthComponent>();
                var unit = unitObject.AddComponent<CastleAssaultUnit>();
                typeof(CastleAssaultUnit).GetMethod(
                        "ResolveReferences",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(unit, null);
                unitHealth.Initialize(100f);

                turretObject.AddComponent<HealthComponent>();
                var turretTarget = turretObject.AddComponent<CastleTarget>();
                turretObject.AddComponent<CastleTurretRuntime>();
                turretTarget.EditorConfigure(CastleTargetKind.Building, 50f, null, null);
                turretTarget.Initialize();

                unit.ApplyDefenseDamage(5f, unitObject.transform.position, turretTarget);
                Assert.That(unit.RecentThreatAggressor, Is.SameAs(turretTarget));
                turretTarget.Shutdown();
            }
            finally
            {
                Object.DestroyImmediate(turretObject);
                Object.DestroyImmediate(unitObject);
            }
        }

        [Test]
        public void SupportUtility_PrioritizesEmergencyHealAndHonorsClaimPenalty()
        {
            var emergency = CastleRaidSupportUtility.ScoreHeal(
                0.2f,
                20f,
                100f,
                2f,
                CastleRaidSupportFocus.Recovery,
                false);
            var healthy = CastleRaidSupportUtility.ScoreHeal(
                0.95f,
                0f,
                100f,
                float.PositiveInfinity,
                CastleRaidSupportFocus.Recovery,
                false);
            var claimed = CastleRaidSupportUtility.ScoreHeal(
                0.2f,
                20f,
                100f,
                2f,
                CastleRaidSupportFocus.Recovery,
                true);

            Assert.That(emergency, Is.GreaterThan(healthy));
            Assert.That(claimed, Is.LessThan(emergency));
        }

        [Test]
        public void SupportUtility_AttackBuffRequiresAnActiveCombatTarget()
        {
            Assert.That(CastleRaidSupportUtility.ScoreAttackBuff(
                20f,
                false,
                false,
                CastleRaidSupportFocus.AttackBuff,
                false), Is.Zero);
            Assert.That(CastleRaidSupportUtility.ScoreAttackBuff(
                20f,
                true,
                false,
                CastleRaidSupportFocus.AttackBuff,
                false), Is.GreaterThan(0f));
        }

        [Test]
        public void ProfileCatalog_UpsertsAndResolvesMonsterSpecificPolicy()
        {
            var catalog = ScriptableObject.CreateInstance<CastleRaidAIProfileCatalog>();
            try
            {
                catalog.EditorUpsert(
                    "support_test",
                    CastleRaidAiPattern.TacticalSupport,
                    CastleRaidSupportFocus.Recovery,
                    6f,
                    3f,
                    4f,
                    0.3f,
                    0.15f,
                    0.7f);

                Assert.That(catalog.TryValidate(out var error), Is.True, error);
                var profile = catalog.Resolve("SUPPORT_TEST");
                Assert.That(profile.Pattern, Is.EqualTo(CastleRaidAiPattern.TacticalSupport));
                Assert.That(profile.SupportFocus, Is.EqualTo(CastleRaidSupportFocus.Recovery));
                Assert.That(profile.SupportRange, Is.EqualTo(6f));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void ProfileCatalog_UnknownMonsterFallsBackToBalancedAdvance()
        {
            var catalog = ScriptableObject.CreateInstance<CastleRaidAIProfileCatalog>();
            try
            {
                Assert.That(
                    catalog.Resolve("unknown").Pattern,
                    Is.EqualTo(CastleRaidAiPattern.BalancedAdvance));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        private static CastlePlacementData CreatePlacement(
            string id,
            CastlePlacementKind kind,
            int x,
            int z,
            int width,
            int height,
            int defenseLayer)
        {
            return new CastlePlacementData(
                id,
                string.Empty,
                string.Empty,
                kind,
                CastleLootKind.None,
                x,
                z,
                width,
                height,
                kind == CastlePlacementKind.Wall ? 1 : 0,
                kind == CastlePlacementKind.Palace ? 700f : 100f,
                0,
                CastleWallNeighborMask.None,
                null,
                kind == CastlePlacementKind.Wall ? CastleWallBand.InnerDefense : CastleWallBand.None,
                defenseLayer,
                id);
        }
    }
}
