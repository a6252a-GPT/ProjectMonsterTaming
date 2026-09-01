using System.Collections.Generic;
using ProjectMT.Contents.TreasureSpirit;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal static class DemoPartyFollowerSpawner
    {
        private static readonly Vector3[] FollowOffsets =
        {
            new Vector3(-1.2f, 0f, -0.9f),
            new Vector3(0f, 0f, -1.2f),
            new Vector3(1.2f, 0f, -0.9f),
            new Vector3(-0.7f, 0f, -2f),
            new Vector3(0.7f, 0f, -2f)
        };

        public static void Spawn(
            Transform commander,
            BattlePartySnapshot party,
            MonsterCatalog catalog,
            CombatWorld combatWorld,
            GameObject followerPrefab,
            List<GameObject> spawned,
            float visualScaleMultiplier = 1f)
        {
            if (commander == null || party == null || catalog == null || combatWorld == null || spawned == null)
            {
                Debug.LogError("[DemoPartyFollowerSpawner] 군단장, 파티, 카탈로그 또는 CombatWorld가 없습니다.");
                return;
            }

            BattleUnitSnapshot[] units = party.Units;
            int spawnedCount = 0;
            for (int i = 0; i < units.Length && spawnedCount < 1; i++)
            {
                BattleUnitSnapshot unit = units[i];
                if (unit == null)
                {
                    continue;
                }

                if (!catalog.TryGet(unit.UnitId, out MonsterDefinition definition) || definition == null)
                {
                    Debug.LogError($"[DemoPartyFollowerSpawner] MonsterDefinition을 찾지 못했습니다. Id={unit.UnitId}");
                    continue;
                }

                MonsterRuntimeAssetSet runtimeSet = unit.RuntimeAssetSet != null
                    ? unit.RuntimeAssetSet
                    : definition.RuntimeAssetSet;
                Vector3 spawnPosition = commander.position + FollowOffsets[spawnedCount];
                UnitActor actor = CreateCombatFollower(
                    commander,
                    unit,
                    definition,
                    runtimeSet,
                    combatWorld,
                    followerPrefab,
                    spawnPosition,
                    FollowOffsets[spawnedCount],
                    visualScaleMultiplier);
                if (actor != null)
                {
                    spawned.Add(actor.gameObject);
                    spawnedCount++;
                }
            }

            if (spawnedCount <= 0)
            {
                Debug.LogWarning("[DemoPartyFollowerSpawner] 스폰할 파티 유닛이 없습니다.");
            }
        }

        public static void Despawn(CombatWorld combatWorld, List<GameObject> spawned)
        {
            combatWorld?.Clear();
            spawned?.Clear();
        }

        private static UnitActor CreateCombatFollower(
            Transform commander,
            BattleUnitSnapshot unit,
            MonsterDefinition definition,
            MonsterRuntimeAssetSet runtimeSet,
            CombatWorld combatWorld,
            GameObject followerPrefab,
            Vector3 spawnPosition,
            Vector3 followOffset,
            float visualScaleMultiplier)
        {
            if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 15f, NavMesh.AllAreas))
            {
                spawnPosition = hit.position;
            }

            UnitStatsSnapshot stats = BuildStats(unit, definition);
            var request = new UnitSpawnRequest(
                unit.UnitId,
                stats,
                UnitTeam.Player,
                false,
                true,
                0f,
                unit.VisualTint,
                runtimeSet,
                0,
                Mathf.Max(0.01f, visualScaleMultiplier),
                false,
                1f,
                unit.PassiveSkill,
                unit.ActiveSkill,
                unit.Level);
            UnitActor actor = combatWorld.SpawnUnit(
                runtimeSet != null && runtimeSet.VisualAdapterPrefab != null
                    ? runtimeSet.VisualAdapterPrefab
                    : followerPrefab,
                request,
                spawnPosition,
                commander.rotation);
            if (actor == null)
            {
                Debug.LogError($"[DemoPartyFollowerSpawner] CombatWorld 스폰에 실패했습니다. Id={unit.UnitId}");
                return null;
            }

            ConfigureNavigation(actor, definition, runtimeSet, spawnPosition);
            DemoFollowerNavBridge bridge = actor.GetComponent<DemoFollowerNavBridge>();
            if (bridge == null)
            {
                bridge = actor.gameObject.AddComponent<DemoFollowerNavBridge>();
            }

            bridge.Initialize(actor, commander, followOffset);
            return actor;
        }

        private static UnitStatsSnapshot BuildStats(BattleUnitSnapshot unit, MonsterDefinition definition)
        {
            UnitStatsSnapshot stats = unit.Stats;
            if (definition == null)
            {
                return stats;
            }

            stats.maxHealth = definition.MaxHealth;
            stats.damage = definition.AttackPower;
            stats.defense = definition.Defense;
            stats.moveSpeed = definition.MoveSpeed;
            stats.attackRange = definition.AttackRange;
            stats.attackInterval = definition.AttackSpeed > 0f ? 1f / definition.AttackSpeed : 1f;
            stats.ranged = definition.Ranged;
            if (stats.ranged && stats.projectileSpeed <= 0f)
            {
                stats.projectileSpeed = 9f;
            }

            return stats;
        }

        private static void ConfigureNavigation(
            UnitActor actor,
            MonsterDefinition definition,
            MonsterRuntimeAssetSet runtimeSet,
            Vector3 spawnPosition)
        {
            MonsterBodyProfile body = runtimeSet != null ? runtimeSet.BodyProfile : null;
            NavMeshAgent agent = actor.GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                agent = actor.gameObject.AddComponent<NavMeshAgent>();
            }

            agent.enabled = false;
            agent.speed = definition != null ? definition.MoveSpeed : 2.5f;
            agent.angularSpeed = 720f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 0.35f;
            agent.radius = body != null ? body.BodyRadius : 0.35f;
            agent.height = body != null ? body.BodyHeight : 1.4f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            CapsuleCollider collider = actor.GetComponent<CapsuleCollider>();
            if (collider == null)
            {
                collider = actor.gameObject.AddComponent<CapsuleCollider>();
            }

            collider.enabled = true;
            if (body != null)
            {
                collider.radius = body.BodyRadius;
                collider.height = body.BodyHeight;
                collider.center = new Vector3(0f, body.BodyHeight * 0.5f + body.GroundOffset, 0f);
            }
            else
            {
                collider.radius = 0.35f;
                collider.height = 1.4f;
                collider.center = new Vector3(0f, 0.7f, 0f);
            }

            Rigidbody rigidbody = actor.GetComponent<Rigidbody>();
            if (rigidbody == null)
            {
                rigidbody = actor.gameObject.AddComponent<Rigidbody>();
            }

            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            agent.enabled = true;
            if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 15f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }
    }
}
