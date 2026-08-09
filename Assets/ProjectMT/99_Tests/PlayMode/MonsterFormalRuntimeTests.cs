using System.Collections;
using NUnit.Framework;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Pooling;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectMT.Tests.PlayMode
{
    public sealed class MonsterFormalRuntimeTests // 공용 Action Executor·Buff·Projectile 회귀
    {
        [UnityTest]
        public IEnumerator MeleeSingleExecutor_AppliesMarkerPowerRatioOnce()
        {
            var fixture = new CombatFixture();
            var action = ScriptableObject.CreateInstance<MeleeActionDefinition>();
            var combat = ScriptableObject.CreateInstance<MonsterCombatProfile>();
            var assetSet = ScriptableObject.CreateInstance<MonsterRuntimeAssetSet>();
            try
            {
                var source = fixture.CreateUnit("source", UnitTeam.Player, Vector3.zero);
                var target = fixture.CreateUnit("target", UnitTeam.Enemy, Vector3.right);
                action.EditorConfigure(MonsterMeleeAttackMode.Single, 1f, 1);
                combat.EditorConfigure(MonsterCombatType.Melee, action);
                assetSet.EditorConfigure(null, null, null, null, combat, null, null);
                var marker = CreateMarker(0.5f, 0.5f);
                var context = new MonsterActionExecutionContext(
                    fixture.World,
                    source,
                    target.Health,
                    fixture.Stats,
                    assetSet,
                    marker,
                    null);

                Assert.That(new MeleeAttackExecutor().Execute(context), Is.True);
                Assert.That(target.Health.CurrentHealth, Is.EqualTo(95f).Within(0.001f));
                yield return null;
                Assert.That(target.Health.CurrentHealth, Is.EqualTo(95f).Within(0.001f));
            }
            finally
            {
                Object.Destroy(assetSet);
                Object.Destroy(combat);
                Object.Destroy(action);
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator MeleeAreaExecutor_HitsOnlyConfiguredNearbyTargetCount()
        {
            var fixture = new CombatFixture();
            var action = ScriptableObject.CreateInstance<MeleeActionDefinition>();
            var combat = ScriptableObject.CreateInstance<MonsterCombatProfile>();
            var assetSet = ScriptableObject.CreateInstance<MonsterRuntimeAssetSet>();
            try
            {
                var source = fixture.CreateUnit("source", UnitTeam.Player, Vector3.zero);
                var primary = fixture.CreateUnit("primary", UnitTeam.Enemy, Vector3.right);
                var nearby = fixture.CreateUnit("nearby", UnitTeam.Enemy, Vector3.right * 1.45f);
                var outside = fixture.CreateUnit("outside", UnitTeam.Enemy, Vector3.right * 3f);
                action.EditorConfigure(
                    MonsterMeleeAttackMode.Area,
                    0.75f,
                    2,
                    MonsterMeleeAreaCenter.PrimaryTarget);
                combat.EditorConfigure(MonsterCombatType.Melee, action);
                assetSet.EditorConfigure(null, null, null, null, combat, null, null);
                var context = new MonsterActionExecutionContext(
                    fixture.World,
                    source,
                    primary.Health,
                    fixture.Stats,
                    assetSet,
                    CreateMarker(0.5f, 1f),
                    null);

                Assert.That(new MeleeAttackExecutor().Execute(context), Is.True);
                Assert.That(primary.Health.CurrentHealth, Is.EqualTo(90f).Within(0.001f));
                Assert.That(nearby.Health.CurrentHealth, Is.EqualTo(90f).Within(0.001f));
                Assert.That(outside.Health.CurrentHealth, Is.EqualTo(100f).Within(0.001f));
                yield return null;
            }
            finally
            {
                Object.Destroy(assetSet);
                Object.Destroy(combat);
                Object.Destroy(action);
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator ProjectileSingleExecutor_UsesPooledFormalProjectile()
        {
            var fixture = new CombatFixture();
            var projectilePrefab = new GameObject("FormalProjectilePrefab");
            projectilePrefab.SetActive(false);
            projectilePrefab.AddComponent<MonsterProjectileActor>();
            var action = ScriptableObject.CreateInstance<ProjectileActionDefinition>();
            var combat = ScriptableObject.CreateInstance<MonsterCombatProfile>();
            var assetSet = ScriptableObject.CreateInstance<MonsterRuntimeAssetSet>();
            try
            {
                var source = fixture.CreateUnit("source", UnitTeam.Player, Vector3.zero);
                var target = fixture.CreateUnit("target", UnitTeam.Enemy, Vector3.right);
                action.EditorConfigure(
                    MonsterProjectileAttackMode.Single,
                    projectilePrefab,
                    20f,
                    1f,
                    0.2f,
                    1,
                    1f,
                    1);
                combat.EditorConfigure(MonsterCombatType.Ranged, action);
                assetSet.EditorConfigure(null, null, null, null, combat, null, null);
                var context = new MonsterActionExecutionContext(
                    fixture.World,
                    source,
                    target.Health,
                    fixture.Stats,
                    assetSet,
                    CreateMarker(0.5f, 1f),
                    null);

                Assert.That(new ProjectileAttackExecutor().Execute(context), Is.True);
                yield return new WaitForSeconds(0.15f);

                Assert.That(target.Health.CurrentHealth, Is.EqualTo(90f).Within(0.001f));
                Assert.That(fixture.Pool.ActiveCount, Is.Zero);
            }
            finally
            {
                Object.Destroy(assetSet);
                Object.Destroy(combat);
                Object.Destroy(action);
                Object.Destroy(projectilePrefab);
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator ProjectilePiercingExecutor_HitsEachTargetOnceAndStopsAtLimit()
        {
            var fixture = new CombatFixture();
            var projectilePrefab = new GameObject("PiercingProjectilePrefab");
            projectilePrefab.SetActive(false);
            projectilePrefab.AddComponent<MonsterProjectileActor>();
            var action = ScriptableObject.CreateInstance<ProjectileActionDefinition>();
            var combat = ScriptableObject.CreateInstance<MonsterCombatProfile>();
            var assetSet = ScriptableObject.CreateInstance<MonsterRuntimeAssetSet>();
            try
            {
                var source = fixture.CreateUnit("source", UnitTeam.Player, Vector3.zero);
                var first = fixture.CreateUnit("first", UnitTeam.Enemy, Vector3.forward * 0.8f);
                var primary = fixture.CreateUnit("primary", UnitTeam.Enemy, Vector3.forward * 1.6f);
                var outsideLimit = fixture.CreateUnit("outside", UnitTeam.Enemy, Vector3.forward * 2.4f);
                action.EditorConfigure(
                    MonsterProjectileAttackMode.Piercing,
                    projectilePrefab,
                    12f,
                    1f,
                    0.25f,
                    2,
                    1f,
                    1);
                combat.EditorConfigure(MonsterCombatType.Ranged, action);
                assetSet.EditorConfigure(null, null, null, null, combat, null, null);
                var context = new MonsterActionExecutionContext(
                    fixture.World,
                    source,
                    primary.Health,
                    fixture.Stats,
                    assetSet,
                    CreateMarker(0.5f, 1f),
                    null);

                Assert.That(new ProjectileAttackExecutor().Execute(context), Is.True);
                yield return new WaitForSeconds(0.3f);

                Assert.That(first.Health.CurrentHealth, Is.EqualTo(90f).Within(0.001f));
                Assert.That(primary.Health.CurrentHealth, Is.EqualTo(90f).Within(0.001f));
                Assert.That(outsideLimit.Health.CurrentHealth, Is.EqualTo(100f).Within(0.001f));
                Assert.That(fixture.Pool.ActiveCount, Is.Zero);
            }
            finally
            {
                Object.Destroy(assetSet);
                Object.Destroy(combat);
                Object.Destroy(action);
                Object.Destroy(projectilePrefab);
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator ProjectileAreaExecutor_DamagesOnlyTargetsInsideImpactRadius()
        {
            var fixture = new CombatFixture();
            var projectilePrefab = new GameObject("AreaProjectilePrefab");
            projectilePrefab.SetActive(false);
            projectilePrefab.AddComponent<MonsterProjectileActor>();
            var action = ScriptableObject.CreateInstance<ProjectileActionDefinition>();
            var combat = ScriptableObject.CreateInstance<MonsterCombatProfile>();
            var assetSet = ScriptableObject.CreateInstance<MonsterRuntimeAssetSet>();
            try
            {
                var source = fixture.CreateUnit("source", UnitTeam.Player, Vector3.zero);
                var primary = fixture.CreateUnit("primary", UnitTeam.Enemy, Vector3.forward);
                var nearby = fixture.CreateUnit("nearby", UnitTeam.Enemy, Vector3.forward * 1.4f);
                var outside = fixture.CreateUnit("outside", UnitTeam.Enemy, Vector3.forward * 3f);
                action.EditorConfigure(
                    MonsterProjectileAttackMode.Area,
                    projectilePrefab,
                    20f,
                    1f,
                    0.2f,
                    1,
                    0.6f,
                    2);
                combat.EditorConfigure(MonsterCombatType.Ranged, action);
                assetSet.EditorConfigure(null, null, null, null, combat, null, null);
                var context = new MonsterActionExecutionContext(
                    fixture.World,
                    source,
                    primary.Health,
                    fixture.Stats,
                    assetSet,
                    CreateMarker(0.5f, 1f),
                    null);

                Assert.That(new ProjectileAttackExecutor().Execute(context), Is.True);
                yield return new WaitForSeconds(0.15f);

                Assert.That(primary.Health.CurrentHealth, Is.EqualTo(90f).Within(0.001f));
                Assert.That(nearby.Health.CurrentHealth, Is.EqualTo(90f).Within(0.001f));
                Assert.That(outside.Health.CurrentHealth, Is.EqualTo(100f).Within(0.001f));
                Assert.That(fixture.Pool.ActiveCount, Is.Zero);
            }
            finally
            {
                Object.Destroy(assetSet);
                Object.Destroy(combat);
                Object.Destroy(action);
                Object.Destroy(projectilePrefab);
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator SpecialExecutor_AppliesAndExpiresConfiguredAreaBuff()
        {
            var fixture = new CombatFixture();
            var action = ScriptableObject.CreateInstance<SpecialActionDefinition>();
            var combat = ScriptableObject.CreateInstance<MonsterCombatProfile>();
            var assetSet = ScriptableObject.CreateInstance<MonsterRuntimeAssetSet>();
            try
            {
                var source = fixture.CreateUnit("source", UnitTeam.Player, Vector3.zero);
                var ally = fixture.CreateUnit("ally", UnitTeam.Player, Vector3.right * 0.5f);
                action.EditorConfigure(
                    "test_health_buff",
                    MonsterBuffTargetTeam.Allies,
                    2f,
                    2,
                    0.05f,
                    MonsterBuffStackPolicy.RefreshDuration,
                    new MonsterStatModifier(1f, 0f, 0f, 0f, 0f, 0f));
                combat.EditorConfigure(MonsterCombatType.Special, action);
                assetSet.EditorConfigure(null, null, null, null, combat, null, null);
                var context = new MonsterActionExecutionContext(
                    fixture.World,
                    source,
                    source.Health,
                    fixture.Stats,
                    assetSet,
                    CreateMarker(0.5f, 1f),
                    null);

                Assert.That(new SpecialActionExecutor().Execute(context), Is.True);
                Assert.That(ally.Health.MaxHealth, Is.EqualTo(200f).Within(0.001f));
                yield return new WaitForSeconds(0.1f);
                Assert.That(ally.Health.MaxHealth, Is.EqualTo(100f).Within(0.001f));
            }
            finally
            {
                Object.Destroy(assetSet);
                Object.Destroy(combat);
                Object.Destroy(action);
                fixture.Dispose();
            }
        }

        private static MonsterAttackMarker CreateMarker(float time, float ratio)
        {
            var marker = new MonsterAttackMarker();
            marker.EditorConfigure(time, ratio);
            return marker;
        }

        private sealed class CombatFixture
        {
            private readonly GameObject root;

            public CombatFixture()
            {
                root = new GameObject("MonsterFormalRuntimeFixture");
                Pool = root.AddComponent<ScenePoolScope>();
                World = root.AddComponent<CombatWorld>();
                World.EditorConfigure(Pool, null, null);
                Stats = new UnitStatsSnapshot
                {
                    maxHealth = 100f,
                    damage = 10f,
                    defense = 0f,
                    moveSpeed = 0f,
                    attackRange = 2f,
                    attackInterval = 1f,
                    projectileSpeed = 20f,
                    ranged = false
                };
            }

            public CombatWorld World { get; }
            public ScenePoolScope Pool { get; }
            public UnitStatsSnapshot Stats { get; }

            public UnitActor CreateUnit(string id, UnitTeam team, Vector3 position)
            {
                var gameObject = new GameObject(id);
                gameObject.transform.SetParent(root.transform, false);
                gameObject.transform.position = position;
                var actor = gameObject.AddComponent<UnitActor>();
                actor.Initialize(
                    new UnitSpawnRequest(id, Stats, team, canMove: false, canAttack: false),
                    World,
                    null);
                return actor;
            }

            public void Dispose()
            {
                if (World != null)
                {
                    World.Clear();
                }

                Object.Destroy(root);
            }
        }
    }
}
