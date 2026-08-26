using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.CastleRaidHex.Tests
{
    public sealed class HexCastleGarrisonCombatTests
    {
        private readonly List<Object> owned = new List<Object>();

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
        public void RuntimeRoute_OpenPartitionGate_AllowsDefenderOnlyUntilDestroyed()
        {
            var gateCoordinates = new HexCoordinates(0, 0);
            var start = gateCoordinates.Neighbor(2);
            var destination = gateCoordinates.Neighbor(4);
            var gate = CreateRuntime(new HexCastleCell(
                gateCoordinates,
                HexCastleCellKind.Gate,
                defenseLayer: 1,
                hitPoints: 100f,
                wallRole: HexCastleWallRole.Partition,
                initialBlocked: true,
                wallTier: 1,
                wallConnectionMask: (1 << 0) | (1 << 3),
                gateRole: HexCastleGateRole.OpenDefenderPassage,
                gatePassageMask: (1 << 2) | (1 << 4)));
            var cells = new Dictionary<HexCoordinates, HexCastleCellRuntime>
            {
                [start] = CreateRuntime(CreateGround(start)),
                [gateCoordinates] = gate,
                [destination] = CreateRuntime(CreateGround(destination))
            };
            var planner = new HexRoutePlanner();

            Assert.That(planner.FindTraversalRoute(
                cells, start, destination, HexCastleTraversalFaction.Defender),
                Is.EqualTo(new[] { start, gateCoordinates, destination }));
            Assert.That(planner.FindTraversalRoute(
                cells, start, destination, HexCastleTraversalFaction.Assault), Is.Empty);

            gate.ApplyDamage(gate.MaxHealth, gate.transform.position);

            Assert.That(planner.FindTraversalRoute(
                cells, start, destination, HexCastleTraversalFaction.Assault),
                Is.EqualTo(new[] { start, gateCoordinates, destination }));
        }

        [Test]
        public void Knight_ChasesAdjacentAssaultAndDealsConfiguredDamage()
        {
            var cells = CreateGroundLine(3);
            var worldRoot = Own(new GameObject("HexCombatWorld"));
            var world = worldRoot.AddComponent<HexCastleTurretCombatWorld>();
            world.Configure(null, null, 1f);
            var target = CreateAssaultUnit();
            target.transform.SetParent(worldRoot.transform, false);
            target.transform.position = new HexCoordinates(2, 0).ToWorld(1f);
            world.RegisterAssaultUnit(target);

            var unitRoot = Own(new GameObject("Knight"));
            var visual = new GameObject("Visual").transform;
            visual.SetParent(unitRoot.transform, false);
            var unit = unitRoot.AddComponent<HexCastleGarrisonUnit>();
            unit.Configure(
                HexCastleGarrisonUnitRole.Knight,
                new HexCoordinates(0, 0),
                0,
                visual,
                cells,
                world,
                Vector3.zero,
                1f,
                HexCastleThemeOneTuning.CreateDraftDefaults());
            var before = target.CurrentHealth;

            unit.Tick(1f);
            unit.Tick(1f);

            Assert.That(unit.Coordinates, Is.EqualTo(new HexCoordinates(1, 0)));
            Assert.That(unit.State, Is.EqualTo(HexCastleGarrisonState.Attack));
            Assert.That(target.CurrentHealth, Is.LessThan(before));
        }

        [Test]
        public void GarrisonDamage_ShowsHostileHealthBarAndReportsActualDamage()
        {
            var cells = CreateGroundLine(1);
            var worldRoot = Own(new GameObject("HexCombatWorld"));
            var world = worldRoot.AddComponent<HexCastleTurretCombatWorld>();
            world.Configure(null, null, 1f);
            var unitRoot = Own(new GameObject("Knight"));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
            visual.SetParent(unitRoot.transform, false);
            var unit = unitRoot.AddComponent<HexCastleGarrisonUnit>();
            unit.Configure(
                HexCastleGarrisonUnitRole.Knight,
                new HexCoordinates(0, 0),
                0,
                visual,
                cells,
                world,
                Vector3.zero,
                1f,
                HexCastleThemeOneTuning.CreateDraftDefaults());
            DamageReport observed = default;
            unit.Damaged += (_, report) => observed = report;

            Assert.That(unit.ApplyDamage(25f, unit.transform.position), Is.True);

            Assert.That(observed.AppliedDamage, Is.EqualTo(25f));
            Assert.That(unit.TryGetComponent<HexCastleOverheadHealthBar>(out var healthBar), Is.True);
            Assert.That(healthBar.IsVisible, Is.True);
            Assert.That(healthBar.FillColor, Is.EqualTo(HexCastleOverheadHealthBar.HostileColor));
            Assert.That(healthBar.FillRatio,
                Is.EqualTo(unit.Health.CurrentHealth / unit.Health.MaxHealth).Within(0.001f));
        }

        [Test]
        public void Knight_DoesNotAttackThroughBlockedCell()
        {
            var cells = CreateGroundLine(3);
            cells[new HexCoordinates(1, 0)] = CreateRuntime(new HexCastleCell(
                new HexCoordinates(1, 0),
                HexCastleCellKind.Wall,
                defenseLayer: 1,
                hitPoints: 100f,
                wallRole: HexCastleWallRole.InnerDefense,
                initialBlocked: true,
                wallTier: 1,
                wallConnectionMask: (1 << 1) | (1 << 4)));
            var worldRoot = Own(new GameObject("HexCombatWorld"));
            var world = worldRoot.AddComponent<HexCastleTurretCombatWorld>();
            world.Configure(null, null, 1f);
            var target = CreateAssaultUnit();
            target.transform.SetParent(worldRoot.transform, false);
            target.transform.position = new HexCoordinates(2, 0).ToWorld(1f);
            world.RegisterAssaultUnit(target);
            var unitRoot = Own(new GameObject("Knight"));
            var visual = new GameObject("Visual").transform;
            visual.SetParent(unitRoot.transform, false);
            var unit = unitRoot.AddComponent<HexCastleGarrisonUnit>();
            unit.Configure(
                HexCastleGarrisonUnitRole.Knight,
                new HexCoordinates(0, 0),
                0,
                visual,
                cells,
                world,
                Vector3.zero,
                1f,
                HexCastleThemeOneTuning.CreateDraftDefaults());
            var before = target.CurrentHealth;

            for (var index = 0; index < 5; index++)
            {
                unit.Tick(1f);
            }

            Assert.That(unit.Coordinates, Is.EqualTo(new HexCoordinates(0, 0)));
            Assert.That(target.CurrentHealth, Is.EqualTo(before));
        }

        [Test]
        public void Knight_JumpsExactlyOneSmallBlockerCellWithFeelFeedback()
        {
            var cells = CreateGroundLine(4);
            cells[new HexCoordinates(1, 0)] = CreateRuntime(CreateSmallBlocker(new HexCoordinates(1, 0)));
            var worldRoot = Own(new GameObject("HexCombatWorld"));
            var world = worldRoot.AddComponent<HexCastleTurretCombatWorld>();
            world.Configure(null, null, 1f);
            var target = CreateAssaultUnit();
            target.transform.SetParent(worldRoot.transform, false);
            target.transform.position = new HexCoordinates(3, 0).ToWorld(1f);
            world.RegisterAssaultUnit(target);

            var unitRoot = Own(new GameObject("Knight"));
            var visual = new GameObject("Visual").transform;
            visual.SetParent(unitRoot.transform, false);
            var unit = unitRoot.AddComponent<HexCastleGarrisonUnit>();
            unit.Configure(
                HexCastleGarrisonUnitRole.Knight,
                new HexCoordinates(0, 0),
                0,
                visual,
                cells,
                world,
                Vector3.zero,
                1f,
                HexCastleThemeOneTuning.CreateDraftDefaults());

            unit.Tick(0.1f);
            Assert.That(unit.IsJumping, Is.True);
            unit.Tick(0.5f);

            Assert.That(unit.Coordinates, Is.EqualTo(new HexCoordinates(2, 0)));
            Assert.That(unit.JumpCount, Is.EqualTo(1));
            Assert.That(unit.GetComponent<MoreMountains.Feedbacks.MMF_Player>(), Is.Not.Null);
            Assert.That(unit.MoveSpeed, Is.EqualTo(2.2f).Within(0.001f));
        }

        [Test]
        public void Knight_PatrolCanSelectGroundBeyondOneSmallBlocker()
        {
            var cells = CreateGroundLine(3);
            cells[new HexCoordinates(1, 0)] = CreateRuntime(CreateSmallBlocker(new HexCoordinates(1, 0)));
            var unitRoot = Own(new GameObject("PatrolKnight"));
            var visual = new GameObject("Visual").transform;
            visual.SetParent(unitRoot.transform, false);
            var unit = unitRoot.AddComponent<HexCastleGarrisonUnit>();
            unit.Configure(
                HexCastleGarrisonUnitRole.Knight,
                new HexCoordinates(0, 0),
                0,
                visual,
                cells,
                null,
                Vector3.zero,
                1f,
                HexCastleThemeOneTuning.CreateDraftDefaults());

            unit.Tick(0.1f); // 순찰 목적지와 점프 경로를 예약한다
            unit.Tick(0.1f); // 실제 점프를 시작한다

            Assert.That(unit.IsJumping, Is.True);
            unit.Tick(0.5f);
            Assert.That(unit.Coordinates, Is.EqualTo(new HexCoordinates(2, 0)));
            Assert.That(unit.JumpCount, Is.EqualTo(1));
        }

        [Test]
        public void Knight_CannotJumpTwoConsecutiveSmallBlockerCells()
        {
            var cells = CreateGroundLine(4);
            cells[new HexCoordinates(1, 0)] = CreateRuntime(CreateSmallBlocker(new HexCoordinates(1, 0)));
            cells[new HexCoordinates(2, 0)] = CreateRuntime(CreateSmallBlocker(new HexCoordinates(2, 0)));
            var worldRoot = Own(new GameObject("HexCombatWorld"));
            var world = worldRoot.AddComponent<HexCastleTurretCombatWorld>();
            world.Configure(null, null, 1f);
            var target = CreateAssaultUnit();
            target.transform.SetParent(worldRoot.transform, false);
            target.transform.position = new HexCoordinates(3, 0).ToWorld(1f);
            world.RegisterAssaultUnit(target);

            var unitRoot = Own(new GameObject("Knight"));
            var visual = new GameObject("Visual").transform;
            visual.SetParent(unitRoot.transform, false);
            var unit = unitRoot.AddComponent<HexCastleGarrisonUnit>();
            unit.Configure(
                HexCastleGarrisonUnitRole.Knight,
                new HexCoordinates(0, 0),
                0,
                visual,
                cells,
                world,
                Vector3.zero,
                1f,
                HexCastleThemeOneTuning.CreateDraftDefaults());

            for (var index = 0; index < 5; index++)
            {
                unit.Tick(0.5f);
            }

            Assert.That(unit.Coordinates, Is.EqualTo(new HexCoordinates(0, 0)));
            Assert.That(unit.JumpCount, Is.Zero);
        }

        [Test]
        public void Garrison_SearchesForNewTargetsOnlyEveryOneToTwoSeconds()
        {
            var cells = CreateGroundLine(1);
            var worldRoot = Own(new GameObject("HexCombatWorld"));
            var world = worldRoot.AddComponent<HexCastleTurretCombatWorld>();
            world.Configure(null, null, 1f);
            var unitRoot = Own(new GameObject("Knight"));
            var visual = new GameObject("Visual").transform;
            visual.SetParent(unitRoot.transform, false);
            var unit = unitRoot.AddComponent<HexCastleGarrisonUnit>();
            unit.Configure(
                HexCastleGarrisonUnitRole.Knight,
                new HexCoordinates(0, 0),
                3,
                visual,
                cells,
                world,
                Vector3.zero,
                1f,
                HexCastleThemeOneTuning.CreateDraftDefaults());

            unit.Tick(0.01f);
            Assert.That(unit.TargetSearchCount, Is.EqualTo(1));
            Assert.That(unit.TargetSearchInterval, Is.InRange(1f, 2f));

            for (var frame = 0; frame < 30; frame++)
            {
                unit.Tick(0.01f);
            }

            Assert.That(unit.TargetSearchCount, Is.EqualTo(1),
                "새 대상 탐색은 매 프레임 실행되면 안 됩니다.");
            unit.Tick(2f);
            Assert.That(unit.TargetSearchCount, Is.EqualTo(2));
        }

        [Test]
        public void Garrison_StaggersAndCapsRespondersWithDistinctApproachReservations()
        {
            var cells = CreateGroundBoard(4);
            var worldRoot = Own(new GameObject("HexCombatWorld"));
            var combatWorld = worldRoot.AddComponent<HexCastleTurretCombatWorld>();
            combatWorld.Configure(null, null, 1f);
            var target = CreateAssaultUnit();
            target.transform.SetParent(worldRoot.transform, false);
            target.transform.position = new HexCoordinates(2, 0).ToWorld(1f);
            combatWorld.RegisterAssaultUnit(target);

            var knightPrefab = Own(new GameObject("KnightPrefab"));
            var farmerPrefab = Own(new GameObject("FarmerPrefab"));
            var catalog = Own(ScriptableObject.CreateInstance<HexCastleGarrisonCatalog>());
            catalog.EditorConfigure(new[] { knightPrefab }, farmerPrefab);
            var garrisonWorld = worldRoot.AddComponent<HexCastleGarrisonWorld>();
            var tuning = HexCastleThemeOneTuning.CreateDraftDefaults();
            garrisonWorld.Configure(catalog, cells, Vector3.zero, 1f, 10801, combatWorld, tuning);

            var units = new List<HexCastleGarrisonUnit>();
            for (var index = 0; index < 5; index++)
            {
                var unitRoot = Own(new GameObject($"Knight_{index}"));
                var visual = new GameObject("Visual").transform;
                visual.SetParent(unitRoot.transform, false);
                var unit = unitRoot.AddComponent<HexCastleGarrisonUnit>();
                unit.Configure(
                    HexCastleGarrisonUnitRole.Knight,
                    new HexCoordinates(0, 0),
                    index,
                    visual,
                    cells,
                    combatWorld,
                    Vector3.zero,
                    1f,
                    tuning,
                    garrisonWorld);
                units.Add(unit);
            }

            units.ForEach(value => value.Tick(0.01f));
            Assert.That(units.Count(value => value.CurrentTarget != null), Is.Zero);
            units.ForEach(value => value.Tick(2f));

            Assert.That(units.Count(value => value.CurrentTarget == target), Is.EqualTo(4));
            Assert.That(garrisonWorld.ActiveResponseReservationCount, Is.EqualTo(4));
        }

        private Dictionary<HexCoordinates, HexCastleCellRuntime> CreateGroundLine(int count)
        {
            var result = new Dictionary<HexCoordinates, HexCastleCellRuntime>();
            for (var index = 0; index < count; index++)
            {
                var coordinates = new HexCoordinates(index, 0);
                result[coordinates] = CreateRuntime(CreateGround(coordinates));
            }

            return result;
        }

        private Dictionary<HexCoordinates, HexCastleCellRuntime> CreateGroundBoard(int radius)
        {
            var result = new Dictionary<HexCoordinates, HexCastleCellRuntime>();
            for (var q = -radius; q <= radius; q++)
            {
                var minimumR = Mathf.Max(-radius, -q - radius);
                var maximumR = Mathf.Min(radius, -q + radius);
                for (var r = minimumR; r <= maximumR; r++)
                {
                    var coordinates = new HexCoordinates(q, r);
                    result[coordinates] = CreateRuntime(CreateGround(coordinates));
                }
            }

            return result;
        }

        private HexCastleAssaultUnit CreateAssaultUnit()
        {
            var layout = new HexCastleFoundationGenerator().Generate(
                10801,
                2,
                HexCastleTheme.CentralCompartment);
            var start = HexCoordinates.Directions[0] * layout.BattlefieldRadius;
            var route = new HexRoutePlanner().FindMinimumBreachRoute(layout, start);
            var root = Own(new GameObject("AssaultUnit"));
            var unit = root.AddComponent<HexCastleAssaultUnit>();
            unit.ConfigureForRoute(route, 1f, 1f, 10f, 1f, 100f);
            return unit;
        }

        private HexCastleCellRuntime CreateRuntime(HexCastleCell cell)
        {
            var root = Own(new GameObject($"Cell_{cell.Coordinates.Q}_{cell.Coordinates.R}"));
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

        private static HexCastleCell CreateGround(HexCoordinates coordinates)
        {
            return new HexCastleCell(
                coordinates,
                HexCastleCellKind.Ground,
                initialBlocked: false);
        }

        private static HexCastleCell CreateSmallBlocker(HexCoordinates coordinates)
        {
            return new HexCastleCell(
                coordinates,
                HexCastleCellKind.Building,
                defenseLayer: 1,
                hitPoints: 100f,
                initialBlocked: true,
                placementId: $"blocker_{coordinates.Q}_{coordinates.R}",
                visualVariantId: "building_stage_B",
                buildingRole: HexCastleBuildingRole.Blocker,
                placementDensity: HexCastlePlacementDensity.Dense,
                buildingGrade: 1);
        }

        private T Own<T>(T value) where T : Object
        {
            owned.Add(value);
            return value;
        }

        private static Transform CreateChild(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }
    }
}
