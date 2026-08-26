using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    /// <summary>
    /// 모든 베이크 맵의 Chest_pt(및 Chest-pt)마다 상자 주변 가시 바닥을 설치합니다.
    /// </summary>
    internal static class DemoSpikeFloorTrapInstaller
    {
        public static void Install(Transform mapRoot, Transform contentRoot)
        {
            if (mapRoot == null || contentRoot == null)
            {
                return;
            }

            var markers = DemoMapUtil.CollectChestMarkers(mapRoot);
            int installedCount = 0;

            for (int i = 0; i < markers.Count; i++)
            {
                Transform marker = markers[i];
                if (marker == null)
                {
                    continue;
                }

                DemoSpikeFloorTrap.SpawnAround(contentRoot, mapRoot, marker.position, marker.rotation);
                installedCount++;
            }

            if (installedCount == 0)
            {
                Debug.LogWarning(
                    "[DemoSpikeFloorTrapInstaller] Chest_pt를 찾지 못해 가시 바닥을 설치하지 못했습니다. " +
                    $"맵={mapRoot.name}");
                return;
            }

            Debug.Log($"[DemoSpikeFloorTrapInstaller] 상자 가시 바닥 {installedCount}개 설치 ({mapRoot.name})");
        }
    }
}
