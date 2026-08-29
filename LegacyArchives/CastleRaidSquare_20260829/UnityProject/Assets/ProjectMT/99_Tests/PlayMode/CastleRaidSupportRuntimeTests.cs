using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectMT.Contents.CastleRaid;
using ProjectMT.Contents.CastleRaid.Generation;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Tests.PlayMode
{
    public sealed class CastleRaidSupportRuntimeTests
    {
        private static readonly MethodInfo ApplySupportAction = typeof(CastleAssaultUnit).GetMethod(
            "ApplySupportAction",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo ResolveReferences = typeof(CastleAssaultUnit).GetMethod(
            "ResolveReferences",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo ResolveNavigationDestination = typeof(CastleAssaultUnit).GetMethod(
            "ResolveNavigationDestination",
            BindingFlags.Instance | BindingFlags.NonPublic);

        [Test]
        public void TacticalSupport_AppliesHealAttackAndDefenseEffectsToAssaultUnit()
        {
            Assert.That(ApplySupportAction, Is.Not.Null);
            Assert.That(ResolveReferences, Is.Not.Null);
            var root = new GameObject("CastleRaidSupportRuntimeProbe");
            try
            {
                // 빈 테스트 Scene에서 NavMeshAgent가 활성화되며 경고를 남기지 않도록 비활성 계층에서 조립한다.
                root.SetActive(false);
                var health = root.AddComponent<HealthComponent>();
                var unit = root.AddComponent<CastleAssaultUnit>();
                ResolveReferences.Invoke(unit, null);
                var profile = new CastleRaidAIProfile("support_probe", CastleRaidAiPattern.TacticalSupport);

                health.Initialize(100f);
                health.ApplyDamage(new DamageRequest(null, 60f, root.transform.position));
                Invoke(unit, new CastleRaidSupportDecision(
                    CastleRaidSupportAction.Heal,
                    unit,
                    profile,
                    1f));
                Assert.That(health.CurrentHealth, Is.EqualTo(64f).Within(0.001f));

                Invoke(unit, new CastleRaidSupportDecision(
                    CastleRaidSupportAction.AttackBuff,
                    unit,
                    profile,
                    1f));
                Assert.That(unit.HasAttackBuff, Is.True);

                health.Initialize(100f);
                Invoke(unit, new CastleRaidSupportDecision(
                    CastleRaidSupportAction.DefenseBuff,
                    unit,
                    profile,
                    1f));
                unit.ApplyDefenseDamage(20f, root.transform.position);
                Assert.That(unit.HasDefenseBuff, Is.True);
                Assert.That(health.CurrentHealth, Is.EqualTo(85f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LogicalRoute_RuntimeWallDeathAdvancesToNextDefenseLayer()
        {
            var root = new GameObject("CastleRaidLogicalRouteRuntimeProbe");
            var outerObject = new GameObject("OuterWall");
            var innerObject = new GameObject("InnerWall");
            try
            {
                outerObject.transform.SetParent(root.transform, false);
                innerObject.transform.SetParent(root.transform, false);
                var outerPlacement = CreateWallPlacement("outer_wall", 2, 0);
                var innerPlacement = CreateWallPlacement("inner_wall", 4, 1);
                var palacePlacement = new CastlePlacementData(
                    "palace",
                    string.Empty,
                    string.Empty,
                    CastlePlacementKind.Palace,
                    CastleLootKind.None,
                    6,
                    2,
                    1,
                    1,
                    0,
                    700f,
                    0);
                var outer = CreateRuntimeWall(outerObject, outerPlacement);
                var inner = CreateRuntimeWall(innerObject, innerPlacement);
                var snapshot = new CastleRaidNavigationSnapshot(
                    7,
                    5,
                    1f,
                    new List<CastlePlacementData> { outerPlacement, innerPlacement, palacePlacement },
                    new List<CastleTarget> { outer, inner });
                snapshot.ResetRuntimeState();
                var planner = new CastleRaidRoutePlanner(snapshot);
                var spawn = snapshot.CellToWorld(new Vector2Int(0, 2));

                Assert.That(planner.TryResolveRoute(spawn, CastleRaidRoutePolicy.Balanced, out var first), Is.True);
                Assert.That(first.FirstObstaclePlacementId, Is.EqualTo("outer_wall"));

                outer.Health.ApplyDamage(new DamageRequest(null, 1000f, outer.transform.position));
                Assert.That(outer.IsAlive, Is.False);
                Assert.That(snapshot.NotifyDestroyed(outer.PlacementId), Is.True);
                Assert.That(planner.TryResolveRoute(spawn, CastleRaidRoutePolicy.Balanced, out var second), Is.True);
                Assert.That(second.FirstObstaclePlacementId, Is.EqualTo("inner_wall"));
                Assert.That(second.FirstObstacleDefenseLayer, Is.EqualTo(1));
                outer.Shutdown();
                inner.Shutdown();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LogicalRoute_UsesOpenApproachCellWhenAttackSlotIsUnavailable()
        {
            var unitObject = new GameObject("CastleRaidApproachUnit");
            var controllerObject = new GameObject("CastleRaidApproachController");
            var wallObject = new GameObject("CastleRaidApproachWall");
            try
            {
                unitObject.SetActive(false);
                unitObject.AddComponent<UnityEngine.AI.NavMeshAgent>();
                unitObject.AddComponent<HealthComponent>();
                var unit = unitObject.AddComponent<CastleAssaultUnit>();
                var controller = controllerObject.AddComponent<CastleRaidController>();
                var placement = CreateWallPlacement("route_wall", 2, 0);
                var wall = CreateRuntimeWall(wallObject, placement);
                var approach = new Vector3(-1.5f, 0f, 2.5f);

                typeof(CastleAssaultUnit).GetField(
                        "controller",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(unit, controller);
                typeof(CastleAssaultUnit).GetField(
                        "target",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(unit, wall);

                var stateType = typeof(CastleRaidController).GetNestedType(
                    "AssaultRouteState",
                    BindingFlags.NonPublic);
                Assert.That(stateType, Is.Not.Null);
                var state = System.Activator.CreateInstance(stateType, true);
                stateType.GetField("FirstObstaclePlacementId")?.SetValue(state, placement.PlacementId);
                stateType.GetField("RouteApproachPosition")?.SetValue(state, approach);
                stateType.GetField("HasRouteApproach")?.SetValue(state, true);
                var states = typeof(CastleRaidController).GetField(
                        "assaultRouteStates",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(controller) as System.Collections.IDictionary;
                Assert.That(states, Is.Not.Null);
                states.Add(unit, state);

                Assert.That(ResolveNavigationDestination, Is.Not.Null);
                var destination = (Vector3)ResolveNavigationDestination.Invoke(unit, null);
                Assert.That(destination, Is.EqualTo(approach));
                wall.Shutdown();
            }
            finally
            {
                Object.DestroyImmediate(wallObject);
                Object.DestroyImmediate(controllerObject);
                Object.DestroyImmediate(unitObject);
            }
        }

        private static void Invoke(CastleAssaultUnit unit, CastleRaidSupportDecision decision)
        {
            ApplySupportAction.Invoke(unit, new object[] { decision });
        }

        private static CastlePlacementData CreateWallPlacement(string id, int x, int defenseLayer)
        {
            return new CastlePlacementData(
                id,
                string.Empty,
                string.Empty,
                CastlePlacementKind.Wall,
                CastleLootKind.None,
                x,
                0,
                1,
                5,
                1,
                100f,
                0,
                CastleWallNeighborMask.None,
                null,
                defenseLayer == 0 ? CastleWallBand.OuterPerimeter : CastleWallBand.InnerDefense,
                defenseLayer,
                id);
        }

        private static CastleTarget CreateRuntimeWall(GameObject targetObject, CastlePlacementData placement)
        {
            targetObject.AddComponent<HealthComponent>();
            var target = targetObject.AddComponent<CastleTarget>();
            target.Configure(CastleTargetKind.Wall, placement.EffectiveHealth, null, null);
            target.ConfigureGenerationMetadata(placement);
            target.Initialize();
            return target;
        }
    }
}
