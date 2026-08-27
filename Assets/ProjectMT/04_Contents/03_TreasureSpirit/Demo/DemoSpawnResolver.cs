using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal static class DemoSpawnResolver
    {
        public static bool TryGetSpawnPosition(Transform mapRoot, float heightOffset, out Vector3 spawnPosition)
        {
            spawnPosition = default;

            if (mapRoot == null)
            {
                return false;
            }

            Transform startPoint = DemoMapUtil.FindStartPoint(mapRoot);
            if (startPoint == null)
            {
                Debug.LogWarning("[DemoSpawnResolver] Start_pt를 찾지 못했습니다.");
                return false;
            }

            if (startPoint.name == DemoMapUtil.StartMarkerName)
            {
                spawnPosition = ApplyHeight(startPoint.position, heightOffset);
                return true;
            }

            if (DemoFloorBounds.TryGetSurface(startPoint, out Vector3 roomFloorSurface))
            {
                spawnPosition = ApplyHeight(roomFloorSurface, heightOffset);
                return true;
            }

            spawnPosition = ApplyHeight(startPoint.position, heightOffset);
            return true;
        }

        public static bool TrySnapToNavMesh(ref Vector3 spawnPosition, float sampleRadius = 4f)
        {
            if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            {
                spawnPosition = hit.position;
                return true;
            }

            return false;
        }

        public static bool TryGetFloorCenter(Transform root, out Vector3 center)
        {
            return DemoFloorBounds.TryGetSurface(root, out center);
        }

        private static Vector3 ApplyHeight(Vector3 floorCenter, float heightOffset)
        {
            return new Vector3(floorCenter.x, floorCenter.y + heightOffset, floorCenter.z);
        }
    }
}
