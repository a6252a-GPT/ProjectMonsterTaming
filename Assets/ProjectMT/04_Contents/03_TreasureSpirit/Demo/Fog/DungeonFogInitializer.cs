using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    public static class DungeonFogInitializer
    {
        public const string FogRootName = "FogOfWarRoot";

        public static void Install(Transform mapRoot, Transform player)
        {
            if (mapRoot == null)
            {
                return;
            }

            ClearExisting(mapRoot);

            GameObject rootObject = new GameObject(FogRootName);
            rootObject.transform.SetParent(mapRoot, false);
            DungeonDistanceFog fog = rootObject.AddComponent<DungeonDistanceFog>();
            fog.Initialize(mapRoot, player);
            DungeonExplorationMap exploration = rootObject.AddComponent<DungeonExplorationMap>();
            exploration.Initialize(mapRoot, player);
            DungeonAutomapOverlay.Ensure(rootObject.transform, exploration, player);
            KeepFireEffectsVisible(mapRoot);
        }

        public static void RevealPlayerArea(Transform player)
        {
            if (player == null)
            {
                return;
            }

            DungeonDistanceFog fog = Object.FindFirstObjectByType<DungeonDistanceFog>();
            fog?.SetPlayer(player);
            DungeonExplorationMap exploration = Object.FindFirstObjectByType<DungeonExplorationMap>();
            exploration?.SetPlayer(player);
            DungeonAutomapOverlay overlay = Object.FindFirstObjectByType<DungeonAutomapOverlay>();
            overlay?.Bind(exploration, player);
        }

        private static void KeepFireEffectsVisible(Transform mapRoot)
        {
            if (mapRoot == null)
            {
                return;
            }

            ParticleSystemRenderer[] renderers = mapRoot.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                ParticleSystemRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                string objectName = renderer.gameObject.name;
                if (objectName.IndexOf("Fire", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    objectName.IndexOf("Torch", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                renderer.sortingOrder = 8;
            }
        }

        private static void ClearExisting(Transform mapRoot)
        {
            Transform existing = mapRoot.Find(FogRootName);
            if (existing != null)
            {
                Object.Destroy(existing.gameObject);
            }
        }
    }
}
