using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal static class DemoEndRoomSpawner
    {
        private const string EndRoomName = "EndRoom";
        private const string PrisonMarkerName = "Prison_pt";

        public static void SpawnPrison(
            Transform mapRoot,
            GameObject prisonPrefab,
            GameObject prisonContentPrefab,
            Vector3 prisonContentLocalOffset,
            float prisonYawOffset,
            BakedDungeonLoader keyState,
            DemoDungeonController controller)
        {
            if (mapRoot == null)
            {
                Debug.LogWarning("[DemoEndRoomSpawner] mapRoot가 null입니다.");
                return;
            }

            if (prisonPrefab == null)
            {
                Debug.LogError("[DemoEndRoomSpawner] prisonPrefab이 연결되지 않았습니다. BakedDungeonLoader Inspector를 확인하세요.");
                return;
            }

            Transform prisonPoint = DemoMapUtil.FindDeepChild(mapRoot, DemoMapUtil.PrisonMarkerName);
            Transform endRoom = DemoMapUtil.FindEndRoom(mapRoot);
            Transform parent = prisonPoint != null ? prisonPoint.parent : endRoom;

            if (parent == null)
            {
                Debug.LogWarning($"[DemoEndRoomSpawner] '{PrisonMarkerName}' 또는 '{EndRoomName}'을 찾지 못해 PF_Prison을 배치할 수 없습니다.");
                return;
            }

            Vector3 spawnPosition;
            Quaternion spawnRotation;

            if (prisonPoint != null)
            {
                spawnPosition = prisonPoint.position;
                spawnRotation = prisonPoint.rotation * Quaternion.Euler(0f, prisonYawOffset, 0f);
            }
            else if (endRoom != null && DemoSpawnResolver.TryGetFloorCenter(endRoom, out Vector3 floorCenter))
            {
                spawnPosition = floorCenter;
                spawnRotation = endRoom.rotation * Quaternion.Euler(0f, prisonYawOffset, 0f);
                Debug.LogWarning($"[DemoEndRoomSpawner] '{PrisonMarkerName}'이 없어 EndRoom 중심에 배치합니다.");
            }
            else
            {
                spawnPosition = parent.position;
                spawnRotation = parent.rotation * Quaternion.Euler(0f, prisonYawOffset, 0f);
                Debug.LogWarning($"[DemoEndRoomSpawner] '{PrisonMarkerName}'이 없어 부모 Transform 위치에 배치합니다.");
            }

            spawnPosition = SnapPositionToFloor(prisonPoint != null ? prisonPoint : parent, spawnPosition);

            GameObject prisonObject = Object.Instantiate(prisonPrefab, spawnPosition, spawnRotation, parent);
            prisonObject.name = "PF_Prison_Runtime";
            DemoWallHeightAdjuster.Apply(prisonObject.transform);
            AlignPrisonBottomToFloor(prisonObject, spawnPosition.y);

            SpawnPrisonVisualContent(prisonObject, prisonContentPrefab, prisonContentLocalOffset);
            ConfigurePrisonDoor(prisonObject, keyState, controller);

            Debug.Log($"[DemoEndRoomSpawner] PF_Prison 배치 ({PrisonMarkerName}, yaw={prisonYawOffset}): {prisonObject.transform.position}");
        }

        private static void SpawnPrisonVisualContent(
            GameObject prisonObject,
            GameObject prisonContentPrefab,
            Vector3 localOffset)
        {
            if (prisonObject == null || prisonContentPrefab == null)
            {
                return;
            }

            GameObject contentObject = Object.Instantiate(prisonContentPrefab, prisonObject.transform);
            contentObject.name = "PrisonContent_Visual";
            contentObject.transform.localPosition = localOffset;
            contentObject.transform.localRotation = Quaternion.identity;
            contentObject.transform.localScale = Vector3.one;

            StripToVisualOnly(contentObject);
        }

        private static void StripToVisualOnly(GameObject root)
        {
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null)
                {
                    behaviours[i].enabled = false;
                }
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }

            NavMeshAgent[] agents = root.GetComponentsInChildren<NavMeshAgent>(true);
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i] != null)
                {
                    agents[i].enabled = false;
                }
            }

            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody rigidbody = rigidbodies[i];
                if (rigidbody == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Object.Destroy(rigidbody);
                }
                else
                {
                    Object.DestroyImmediate(rigidbody);
                }
            }
        }

        private static void ConfigurePrisonDoor(
            GameObject prisonObject,
            BakedDungeonLoader keyState,
            DemoDungeonController controller)
        {
            Transform doorRoot = DemoMapUtil.FindDeepChild(prisonObject.transform, "Door_Prison");
            Transform doorVisual = doorRoot != null
                ? DemoMapUtil.FindDeepChild(doorRoot, "Dungeon_Door_Prison")
                : null;
            Transform rotateTarget = doorVisual != null ? doorVisual : doorRoot;

            DemoPrisonDoor demoDoor = prisonObject.GetComponentInChildren<DemoPrisonDoor>(true);
            if (demoDoor == null && doorRoot != null)
            {
                demoDoor = doorRoot.gameObject.AddComponent<DemoPrisonDoor>();
            }

            demoDoor?.Configure(rotateTarget, keyState, controller);
        }

        private static Vector3 SnapPositionToFloor(Transform markerOrRoom, Vector3 position)
        {
            Transform room = DemoMapUtil.FindRoomRoot(markerOrRoom) ?? markerOrRoom;
            if (DemoFloorBounds.TryGetSurface(room, out Vector3 floorPoint))
            {
                position.y = floorPoint.y;
                return position;
            }

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                position.y = hit.position.y;
            }

            return position;
        }

        private static void AlignPrisonBottomToFloor(GameObject prisonObject, float floorY)
        {
            if (prisonObject == null)
            {
                return;
            }

            Renderer[] renderers = prisonObject.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            float deltaY = floorY - bounds.min.y;
            if (Mathf.Abs(deltaY) > 0.001f)
            {
                prisonObject.transform.position += new Vector3(0f, deltaY, 0f);
            }
        }
    }
}
