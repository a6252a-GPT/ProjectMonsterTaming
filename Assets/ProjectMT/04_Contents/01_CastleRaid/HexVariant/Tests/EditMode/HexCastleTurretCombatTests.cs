using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Pooling;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.CastleRaidHex.Tests
{
    public sealed class HexCastleTurretCombatTests
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
        public void IndependentCatalog_ResolvesOnlySupportedWeaponLevels()
        {
            var profiles = new List<HexCastleTurretAttackProfile>();
            for (var weaponIndex = (int)HexCastleTurretWeaponKind.Cannon;
                 weaponIndex <= (int)HexCastleTurretWeaponKind.Fireball;
                 weaponIndex++)
            {
                var weapon = (HexCastleTurretWeaponKind)weaponIndex;
                for (var level = 1;
                     level <= HexCastleTurretAttackCatalog.ResolveSupportedMaximumLevel(weapon);
                     level++)
                {
                    profiles.Add(CreateProfile(weapon, level));
                }
            }

            var catalog = ScriptableObject.CreateInstance<HexCastleTurretAttackCatalog>();
            owned.Add(catalog);
            catalog.EditorConfigure(profiles.ToArray());

            Assert.That(catalog.IsComplete, Is.True);
            Assert.That(catalog.Resolve(HexCastleTurretWeaponKind.Cannon, 1), Is.SameAs(profiles[0]));
            Assert.That(catalog.Resolve(HexCastleTurretWeaponKind.Ballista, 2), Is.SameAs(profiles[3]));
            Assert.That(catalog.Resolve(HexCastleTurretWeaponKind.Fireball, 3), Is.SameAs(profiles[6]));
            Assert.That(catalog.Resolve(HexCastleTurretWeaponKind.Cannon, 3), Is.Null);
            Assert.That(catalog.Resolve(HexCastleTurretWeaponKind.Ballista, 3), Is.Null);
            Assert.That(catalog.Resolve(HexCastleTurretWeaponKind.None, 1), Is.Null);
        }

        [Test]
        public void CombatWorld_UsesAxialRangeAndExplicitAcrossWallRule()
        {
            var world = CreateWorld(1f);
            var source = CreateCellRuntime(CreateTurretCell(new HexCoordinates(0, 0)));
            source.transform.position = source.Coordinates.ToWorld(1f);
            var blocker = CreateCellRuntime(new HexCastleCell(
                new HexCoordinates(0, 1),
                HexCastleCellKind.Wall,
                defenseLayer: 1,
                hitPoints: 100f,
                initialBlocked: true));
            blocker.transform.position = blocker.Coordinates.ToWorld(1f);
            var target = CreateAssaultUnit();
            target.transform.position = new HexCoordinates(0, 2).ToWorld(1f);
            world.RegisterCell(source);
            world.RegisterCell(blocker);
            world.RegisterAssaultUnit(target);

            var blocked = world.FindTarget(
                source,
                source.transform.position,
                3,
                HexCastleTurretTargetPriority.Nearest,
                0.05f,
                false);
            var acrossWall = world.FindTarget(
                source,
                source.transform.position,
                3,
                HexCastleTurretTargetPriority.Nearest,
                0.05f,
                true);
            var outsideRange = world.FindTarget(
                source,
                source.transform.position,
                1,
                HexCastleTurretTargetPriority.Nearest,
                0.05f,
                true);

            Assert.That(blocked, Is.Null);
            Assert.That(acrossWall, Is.SameAs(target));
            Assert.That(outsideRange, Is.Null);
        }

        [Test]
        public void Runtime_FiresPooledProjectileAndStopsWhenCellDies()
        {
            var world = CreateWorld(1f);
            var structure = CreateCellRuntime(CreateTurretCell(new HexCoordinates(0, 0)));
            var visual = CreateVisual(structure.ContentVisualRoot, HexCastleTurretWeaponKind.Cannon, 1);
            var profile = CreateProfile(HexCastleTurretWeaponKind.Cannon, 1);
            var target = CreateAssaultUnit();
            target.transform.position = new HexCoordinates(0, 1).ToWorld(1f);
            world.RegisterCell(structure);
            world.RegisterAssaultUnit(target);
            var turret = structure.gameObject.AddComponent<HexCastleTurretRuntime>();
            turret.Configure(world, structure, visual, profile);
            var healthBefore = target.CurrentHealth;

            turret.Tick(1f, 10f);
            Assert.That(turret.ProjectilesFired, Is.EqualTo(1));
            var projectile = Resources.FindObjectsOfTypeAll<HexCastleTurretProjectile>()
                .Single(value => value.gameObject.activeInHierarchy);
            projectile.Tick(0.4f);

            Assert.That(turret.HitCount, Is.EqualTo(1));
            Assert.That(target.CurrentHealth, Is.EqualTo(healthBefore - profile.Data.baseDamage).Within(0.001f));
            Assert.That(world.PoolScope.ActiveCount, Is.EqualTo(0));

            var firedBeforeDestruction = turret.ProjectilesFired;
            Assert.That(structure.ApplyDamage(structure.MaxHealth, structure.transform.position), Is.True);
            turret.Tick(10f, 20f);

            Assert.That(structure.IsAlive, Is.False);
            Assert.That(turret.ProjectilesFired, Is.EqualTo(firedBeforeDestruction));
        }

        [Test]
        public void Runtime_LongCannonMuzzleConvergesOnAdjacentCellAndFires()
        {
            var world = CreateWorld(1f);
            var structure = CreateCellRuntime(CreateTurretCell(new HexCoordinates(0, 0)));
            var visual = CreateVisual(structure.ContentVisualRoot, HexCastleTurretWeaponKind.Cannon, 1);
            visual.Muzzle.localPosition = new Vector3(0.004f, 0.651f, 0.699f);
            var profile = CreateProfile(HexCastleTurretWeaponKind.Cannon, 1);
            var target = CreateAssaultUnit();
            target.transform.position = new HexCoordinates(0, 1).ToWorld(1f) + Vector3.up * 0.42f;
            world.RegisterCell(structure);
            world.RegisterAssaultUnit(target);
            var turret = structure.gameObject.AddComponent<HexCastleTurretRuntime>();
            turret.Configure(world, structure, visual, profile);

            for (var step = 0; step < 40 && turret.ProjectilesFired == 0; step++)
            {
                turret.Tick(0.1f, 10f + step * 0.1f);
            }

            Assert.That(turret.CurrentTarget, Is.SameAs(target));
            Assert.That(turret.ProjectilesFired, Is.GreaterThan(0),
                "회전축이 아니라 실제 총구 위치를 기준으로 근거리 표적에 수렴해야 합니다.");
        }

        [TestCase(HexCastleTurretWeaponKind.Cannon)]
        [TestCase(HexCastleTurretWeaponKind.Ballista)]
        [TestCase(HexCastleTurretWeaponKind.Fireball)]
        public void Runtime_KeepsHeadRotationOnYawAxisDuringAimAndRecoil(
            HexCastleTurretWeaponKind weaponKind)
        {
            var world = CreateWorld(1f);
            var structure = CreateCellRuntime(CreateTurretCell(new HexCoordinates(0, 0), weaponKind));
            var visual = CreateVisual(structure.ContentVisualRoot, weaponKind, 1);
            var profile = CreateProfile(weaponKind, 1);
            var target = CreateAssaultUnit();
            target.transform.position = new HexCoordinates(1, 0).ToWorld(1f) - Vector3.up * 0.5f;
            world.RegisterCell(structure);
            world.RegisterAssaultUnit(target);
            var turret = structure.gameObject.AddComponent<HexCastleTurretRuntime>();
            turret.Configure(world, structure, visual, profile);
            var prefabYaw = visual.YawPivot.localRotation;
            var prefabPitch = visual.PitchPivot.localRotation;

            for (var step = 0; step < 20; step++)
            {
                turret.Tick(0.05f, 10f + step * 0.05f);
                Assert.That(Quaternion.Angle(visual.PitchPivot.localRotation, prefabPitch), Is.LessThan(0.01f));
            }

            var yawDelta = Quaternion.Inverse(prefabYaw) * visual.YawPivot.localRotation;
            Assert.That(turret.ProjectilesFired, Is.GreaterThan(0));
            Assert.That(Mathf.Abs(yawDelta.x), Is.LessThan(0.0001f));
            Assert.That(Mathf.Abs(yawDelta.z), Is.LessThan(0.0001f));
        }

        [Test]
        public void Ballista_AlwaysFiresOneDirectProjectileAndDamagesOnlyFirstTarget()
        {
            var world = CreateWorld(1f);
            var structure = CreateCellRuntime(CreateTurretCell(
                new HexCoordinates(0, 0),
                HexCastleTurretWeaponKind.Ballista));
            var visual = CreateVisual(structure.ContentVisualRoot, HexCastleTurretWeaponKind.Ballista, 1);
            var profile = CreateProfile(HexCastleTurretWeaponKind.Ballista, 1);
            var target = CreateAssaultUnit();
            target.transform.position = new HexCoordinates(0, 1).ToWorld(1f);
            var rearTarget = CreateAssaultUnit();
            rearTarget.transform.position = new HexCoordinates(0, 2).ToWorld(1f);
            world.RegisterCell(structure);
            world.RegisterAssaultUnit(target);
            world.RegisterAssaultUnit(rearTarget);
            var turret = structure.gameObject.AddComponent<HexCastleTurretRuntime>();
            turret.Configure(world, structure, visual, profile);
            var targetHealthBefore = target.CurrentHealth;
            var rearHealthBefore = rearTarget.CurrentHealth;

            turret.Tick(1f, 10f);
            var projectile = Resources.FindObjectsOfTypeAll<HexCastleTurretProjectile>()
                .Single(value => value.gameObject.activeInHierarchy);
            projectile.Tick(1f);

            Assert.That(profile.Data.impactType, Is.EqualTo(HexCastleTurretImpactType.Direct));
            Assert.That(profile.Data.projectileCount, Is.EqualTo(1));
            Assert.That(profile.Data.pierceCount, Is.EqualTo(1));
            Assert.That(profile.Data.piercingDamageRatio, Is.Zero);
            Assert.That(turret.ProjectilesFired, Is.EqualTo(1));
            Assert.That(turret.HitCount, Is.EqualTo(1));
            Assert.That(target.CurrentHealth, Is.EqualTo(targetHealthBefore - profile.Data.baseDamage).Within(0.001f));
            Assert.That(rearTarget.CurrentHealth, Is.EqualTo(rearHealthBefore));
            Assert.That(projectile.IsConfigured, Is.False);
            Assert.That(world.PoolScope.ActiveCount, Is.EqualTo(0),
                "발리스타 화살은 첫 대상 타격 직후 반환되어야 합니다.");
        }

        private HexCastleTurretCombatWorld CreateWorld(float cellSize)
        {
            var root = new GameObject("HexTurretCombatWorld");
            owned.Add(root);
            var pool = root.AddComponent<ScenePoolScope>();
            var world = root.AddComponent<HexCastleTurretCombatWorld>();
            world.Configure(pool, null, cellSize);
            return world;
        }

        private HexCastleCellRuntime CreateCellRuntime(HexCastleCell cell)
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

        [Test]
        public void AssaultUnit_DamageEventReportsActualAppliedDamageForFloatingNumber()
        {
            var unit = CreateAssaultUnit();
            var received = false;
            var captured = default(DamageReport);
            unit.Damaged += (_, report) =>
            {
                received = true;
                captured = report;
            };
            var hitPoint = unit.transform.position + Vector3.up * 0.5f;

            Assert.That(unit.ApplyDamage(125f, hitPoint), Is.True);
            Assert.That(received, Is.True);
            Assert.That(captured.Request.HitPoint, Is.EqualTo(hitPoint));
            Assert.That(captured.Request.Amount, Is.EqualTo(125f));
            Assert.That(captured.AppliedDamage, Is.EqualTo(100f));
            Assert.That(captured.RemainingHealth, Is.Zero);
            Assert.That(captured.Killed, Is.True);
        }

        private HexCastleAssaultUnit CreateAssaultUnit()
        {
            var layout = new HexCastleFoundationGenerator().Generate(
                10801,
                2,
                HexCastleTheme.CentralCompartment);
            var start = HexCoordinates.Directions[0] * layout.BattlefieldRadius;
            var route = new HexRoutePlanner().FindMinimumBreachRoute(layout, start);
            var root = new GameObject("AssaultUnit");
            owned.Add(root);
            var unit = root.AddComponent<HexCastleAssaultUnit>();
            unit.ConfigureForRoute(
                route,
                1f,
                1f,
                10f,
                1f,
                100f);
            return unit;
        }

        private HexCastleTurretAttackProfile CreateProfile(
            HexCastleTurretWeaponKind weaponKind,
            int level)
        {
            var projectilePrefab = new GameObject($"Projectile_{weaponKind}_{level}");
            owned.Add(projectilePrefab);
            var profile = ScriptableObject.CreateInstance<HexCastleTurretAttackProfile>();
            owned.Add(profile);
            profile.EditorConfigure(new HexCastleTurretAttackProfileData
            {
                weaponKind = weaponKind,
                level = level,
                impactType = weaponKind == HexCastleTurretWeaponKind.Ballista
                    ? HexCastleTurretImpactType.Pierce
                    : weaponKind == HexCastleTurretWeaponKind.Fireball
                        ? HexCastleTurretImpactType.ExplosionArea
                        : HexCastleTurretImpactType.Direct,
                targetPriority = HexCastleTurretTargetPriority.Nearest,
                sourceSearchRange = 4f,
                baseDamage = 25f,
                cooldown = 1f,
                projectileCount = 1,
                projectileVolleySize = 1,
                projectileSpeed = 10f,
                projectileHitRadius = 0.05f,
                projectileLifetime = 2f,
                pierceCount = 2,
                piercingDamageRatio = 0.7f,
                explosionRadius = 1.5f,
                projectileScale = 1f,
                targetAimHeight = 0.35f,
                headTurnSpeed = 720f,
                fireAngleTolerance = 5f,
                loadedProjectileReloadRatio = 0.5f,
                recoilDistance = 0.05f,
                recoilTiltAngle = 2f,
                recoilKickDuration = 0.05f,
                recoilReturnDuration = 0.1f,
                recoilSettleDuration = 0.05f,
                impactVfxLifetime = 0.2f,
                impactVfxScale = 1f,
                projectilePrefab = projectilePrefab
            });
            return profile;
        }

        private static HexCastleCell CreateTurretCell(
            HexCoordinates coordinates,
            HexCastleTurretWeaponKind weaponKind = HexCastleTurretWeaponKind.Cannon)
        {
            return new HexCastleCell(
                coordinates,
                HexCastleCellKind.DefenseBuilding,
                defenseLayer: 1,
                hitPoints: 220f,
                initialBlocked: true,
                placementId: "TEST_TURRET",
                visualVariantId: "building_tower_base_blue",
                buildingRole: HexCastleBuildingRole.Turret,
                placementDensity: HexCastlePlacementDensity.Dense,
                buildingGrade: 1,
                turretWeaponKind: weaponKind,
                turretRangeCells: 3,
                turretCanAttackAcrossWalls: true);
        }

        private static HexCastleTurretVisual CreateVisual(
            Transform parent,
            HexCastleTurretWeaponKind weaponKind,
            int level)
        {
            var head = CreateChild("Head", parent);
            var bodyMount = CreateChild("Joint_BodyMount", head);
            var yaw = CreateChild("YawPivot", bodyMount);
            var pitch = CreateChild("PitchPivot", yaw);
            pitch.localPosition = Vector3.up * 0.35f;
            var muzzle = CreateChild("Muzzle", pitch);
            muzzle.localPosition = Vector3.forward * 0.2f;
            var visual = parent.parent.gameObject.AddComponent<HexCastleTurretVisual>();
            visual.Configure(weaponKind, level, head);
            return visual;
        }

        private static Transform CreateChild(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }
    }
}
