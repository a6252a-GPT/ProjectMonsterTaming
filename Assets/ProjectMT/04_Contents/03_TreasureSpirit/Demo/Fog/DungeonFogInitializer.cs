using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    public static class DungeonFogInitializer
    {
        public const string FogRootName = "FogOfWarRoot";

        public static void Install(Transform mapRoot)
        {
            if (mapRoot == null)
            {
                return;
            }

            ClearExisting(mapRoot);

            GameObject rootObject = new GameObject(FogRootName);
            rootObject.transform.SetParent(mapRoot, false);
            DungeonDistanceFog fog = rootObject.AddComponent<DungeonDistanceFog>();
            fog.Initialize(mapRoot);
            DungeonExplorationMap exploration = rootObject.AddComponent<DungeonExplorationMap>();
            exploration.Initialize(mapRoot);
            DungeonAutomapOverlay.Ensure(rootObject.transform, exploration, null);
            KeepFireEffectsVisible(mapRoot);
        }

        public static void RevealPlayerArea(Transform mapRoot, Transform player)
        {
            if (mapRoot == null || player == null)
            {
                return;
            }

            Transform fogRoot = mapRoot.Find(FogRootName);
            DungeonDistanceFog fog = fogRoot != null
                ? fogRoot.GetComponent<DungeonDistanceFog>()
                : null;
            DungeonExplorationMap exploration = fogRoot != null
                ? fogRoot.GetComponent<DungeonExplorationMap>()
                : null;
            DungeonAutomapOverlay overlay = fogRoot != null
                ? fogRoot.GetComponentInChildren<DungeonAutomapOverlay>(true)
                : null;

            fog?.SetPlayer(player);
            exploration?.SetPlayer(player);
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
