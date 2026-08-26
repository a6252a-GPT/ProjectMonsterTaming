using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ProjectMT.Contents.TreasureSpirit;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal static class DemoRoomContentSpawner
    {
        private const string GuardMarkerName = "Guard_pt";

        public static void Spawn(
            Transform mapRoot,
            GameObject chestPrefab,
            GameObject mimicPrefab,
            GameObject guardPrefab,
            int guardsPerRoom,
            float guardSpreadDistance,
            float chestHeightOffset,
            Transform playerTransform,
            BakedDungeonLoader keyState)
        {
            if (mapRoot == null)
            {
                return;
            }

            Transform contentRoot = new GameObject("DemoRoomContent").transform;
            contentRoot.SetParent(mapRoot, false);

            SpawnChestsAndMimics(
                mapRoot,
                contentRoot,
                chestPrefab,
                mimicPrefab,
                chestHeightOffset,
                playerTransform,
                keyState);
            DemoSpikeFloorTrapInstaller.Install(mapRoot, contentRoot);
            SpawnGuards(mapRoot, contentRoot, guardPrefab, guardsPerRoom, guardSpreadDistance, playerTransform);
        }

        private static void SpawnChestsAndMimics(
            Transform mapRoot,
            Transform contentRoot,
            GameObject chestPrefab,
            GameObject mimicPrefab,
            float chestHeightOffset,
            Transform playerTransform,
            BakedDungeonLoader keyState)
        {
            List<Transform> chestMarkers = DemoMapUtil.CollectChestMarkers(mapRoot);
            if (chestMarkers.Count == 0)
            {
                return;
            }

            chestMarkers.Sort(CompareMarkerRooms);

            int keyChestCount = 0;
            int mimicCount = 0;

            for (int i = 0; i < chestMarkers.Count; i++)
            {
                Transform marker = chestMarkers[i];
                Transform room = DemoMapUtil.FindRoomRoot(marker) ?? marker.parent;
                Vector3 position = marker.position;
                position.y += chestHeightOffset;
                Quaternion rotation = marker.rotation;

                if (i == 0)
                {
                    if (chestPrefab == null)
                    {
                        Debug.LogWarning("[DemoRoomContentSpawner] 열쇠 상자 프리팹이 비어 있습니다.");
                        continue;
                    }

                    SpawnKeyChest(contentRoot, chestPrefab, room, position, rotation, playerTransform, keyState);
                    keyChestCount++;
                }
                else
                {
                    if (chestPrefab == null || mimicPrefab == null)
                    {
                        Debug.LogWarning("[DemoRoomContentSpawner] 미믹 상자/미믹 프리팹이 비어 있습니다.");
                        continue;
                    }

                    SpawnMimicChest(contentRoot, chestPrefab, mimicPrefab, room, position, rotation, playerTransform);
                    mimicCount++;
                }
            }

            Debug.Log(
                $"[DemoRoomContentSpawner] Chest_pt {chestMarkers.Count}개: 열쇠 상자 {keyChestCount}개, 미믹 상자 {mimicCount}개");
        }

        private static int CompareMarkerRooms(Transform a, Transform b)
        {
            Transform roomA = DemoMapUtil.FindRoomRoot(a);
            Transform roomB = DemoMapUtil.FindRoomRoot(b);
            string nameA = roomA != null ? roomA.name : a.name;
            string nameB = roomB != null ? roomB.name : b.name;
            return string.CompareOrdinal(nameA, nameB);
        }

        private static void SpawnKeyChest(
            Transform contentRoot,
            GameObject chestPrefab,
            Transform room,
            Vector3 position,
            Quaternion rotation,
            Transform playerTransform,
            BakedDungeonLoader keyState)
        {
            GameObject chestObject = Object.Instantiate(chestPrefab, position, rotation, contentRoot);
            chestObject.name = $"DemoKeyChest_{room.name}";

            DemoChestInteraction interaction = chestObject.GetComponent<DemoChestInteraction>();
            if (interaction == null)
            {
                interaction = chestObject.AddComponent<DemoChestInteraction>();
            }

            interaction.SetupKeyChest(playerTransform, keyState);
        }

        private static void SpawnMimicChest(
            Transform contentRoot,
            GameObject chestPrefab,
            GameObject mimicPrefab,
            Transform room,
            Vector3 position,
            Quaternion rotation,
            Transform playerTransform)
        {
            GameObject chestObject = Object.Instantiate(chestPrefab, position, rotation, contentRoot);
            chestObject.name = $"DemoMimicChest_{room.name}";

            DemoChestInteraction interaction = chestObject.GetComponent<DemoChestInteraction>();
            if (interaction == null)
            {
                interaction = chestObject.AddComponent<DemoChestInteraction>();
            }

            interaction.SetupMimicChest(playerTransform, mimicPrefab);
        }

        private static void SpawnGuards(
            Transform mapRoot,
            Transform contentRoot,
            GameObject guardPrefab,
            int guardsPerRoom,
            float guardSpreadDistance,
            Transform playerTransform)
        {
            if (guardPrefab == null)
            {
                return;
            }

            List<Transform> guardMarkers = DemoMapUtil.CollectMarkers(mapRoot, GuardMarkerName);
            if (guardMarkers.Count == 0)
            {
                return;
            }

            int spawnedCount = 0;
            int guardsPerMarker = Mathf.Max(1, guardsPerRoom);

            for (int i = 0; i < guardMarkers.Count; i++)
            {
                Transform marker = guardMarkers[i];
                Transform room = DemoMapUtil.FindRoomRoot(marker) ?? marker.parent;
                DemoFloorBounds.TryGetPatrolBounds(room, out Vector3 patrolCenter, out float patrolRadius);

                for (int g = 0; g < guardsPerMarker; g++)
                {
                    float side = guardsPerMarker == 1 ? 0f : (g == 0 ? -1f : 1f);
                    if (guardsPerMarker > 2)
                    {
                        side = -1f + (2f * g / (guardsPerMarker - 1));
                    }

                    Vector3 spawnPosition = marker.position + marker.right * (side * guardSpreadDistance);
                    Quaternion spawnRotation = marker.rotation;
                    DemoSpawnResolver.TrySnapToNavMesh(ref spawnPosition, 4f);

                    GameObject guardObject = Object.Instantiate(
                        guardPrefab,
                        spawnPosition,
                        spawnRotation,
                        contentRoot);
                    guardObject.name = $"DemoGuard_{room.name}_{i + 1}_{g + 1}";

                    ConfigureGuard(guardObject, patrolCenter, patrolRadius, playerTransform);
                    spawnedCount++;
                }
            }

            Debug.Log(
                $"[DemoRoomContentSpawner] Guard_pt {guardMarkers.Count}개 x {guardsPerMarker}명 = 경비병 {spawnedCount}명 배치");
        }

        private static void ConfigureGuard(
            GameObject guardObject,
            Vector3 patrolCenter,
            float patrolRadius,
            Transform playerTransform)
        {
            NavMeshAgent agent = guardObject.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.autoTraverseOffMeshLink = false;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
                agent.Warp(guardObject.transform.position);
            }

            EnsureSolidCollider(guardObject);

            GuardAI guardAi = guardObject.GetComponent<GuardAI>();
            if (guardAi == null)
            {
                return;
            }

            if (playerTransform != null)
            {
                guardAi.SetTargetPlayer(playerTransform);
            }

            guardAi.SetPatrolBounds(patrolCenter, patrolRadius);
        }

        private static void EnsureSolidCollider(GameObject target)
        {
            Collider existing = target.GetComponent<Collider>();
            if (existing != null)
            {
                existing.isTrigger = false;
                return;
            }

            CapsuleCollider capsule = target.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.45f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.isTrigger = false;
        }
    }
}
