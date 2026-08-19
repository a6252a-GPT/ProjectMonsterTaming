using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal static class DemoNavMeshBuilder
    {
        private const string ProxyRootName = "DemoNavMeshProxies";

        public static NavMeshSurface BuildForMap(GameObject mapInstance)
        {
            if (mapInstance == null)
            {
                return null;
            }

            DestroyExistingProxies(mapInstance.transform);

            Transform proxyRoot = CreateProxyRoot(mapInstance.transform);
            int proxyCount = CreateFloorBoxProxies(mapInstance.transform, proxyRoot);

            if (proxyCount == 0)
            {
                Debug.LogWarning("[DemoNavMeshBuilder] Floor_Flat 프록시가 없습니다. NavMesh 품질이 떨어질 수 있습니다.");
            }

            NavMeshSurface surface = proxyRoot.GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                surface = proxyRoot.gameObject.AddComponent<NavMeshSurface>();
            }

            ConfigureSurface(surface, proxyCount);
            surface.BuildNavMesh();
            return surface;
        }

        public static void DestroyExistingProxies(Transform mapRoot)
        {
            if (mapRoot == null)
            {
                return;
            }

            Transform existing = mapRoot.Find(ProxyRootName);
            if (existing == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(existing.gameObject);
            }
            else
            {
                Object.DestroyImmediate(existing.gameObject);
            }
        }

        private static Transform CreateProxyRoot(Transform mapRoot)
        {
            GameObject proxyRootObject = new GameObject(ProxyRootName);
            Transform proxyRoot = proxyRootObject.transform;
            proxyRoot.SetParent(mapRoot, false);
            proxyRoot.localPosition = Vector3.zero;
            proxyRoot.localRotation = Quaternion.identity;
            proxyRoot.localScale = Vector3.one;
            return proxyRoot;
        }

        private static int CreateFloorBoxProxies(Transform mapRoot, Transform proxyRoot)
        {
            Renderer[] renderers = mapRoot.GetComponentsInChildren<Renderer>(true);
            int count = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.gameObject.name.StartsWith(DemoFloorBounds.FloorNamePrefix))
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                if (bounds.size.sqrMagnitude <= 0.001f)
                {
                    continue;
                }

                GameObject proxyObject = new GameObject($"Proxy_{renderer.gameObject.name}_{count}");
                Transform proxyTransform = proxyObject.transform;
                proxyTransform.SetParent(proxyRoot, false);
                proxyTransform.position = bounds.center;
                proxyTransform.rotation = Quaternion.identity;

                BoxCollider boxCollider = proxyObject.AddComponent<BoxCollider>();
                boxCollider.size = bounds.size;
                boxCollider.center = Vector3.zero;

                count++;
            }

            return count;
        }

        private static void ConfigureSurface(NavMeshSurface surface, int proxyCount)
        {
            surface.agentTypeID = 0;
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.defaultArea = 0;
            surface.layerMask = ~0;
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;

            Transform proxyRoot = surface.transform;
            Bounds bounds = CalculateBounds(proxyRoot);
            if (proxyCount > 0 && bounds.size.sqrMagnitude > 0.001f)
            {
                surface.center = proxyRoot.InverseTransformPoint(bounds.center);
                surface.size = bounds.size;
            }
            else
            {
                surface.center = Vector3.zero;
                surface.size = new Vector3(80f, 10f, 80f);
            }
        }

        private static Bounds CalculateBounds(Transform root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            Bounds bounds = default;
            bool initialized = false;

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null)
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = colliders[i].bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(colliders[i].bounds);
                }
            }

            if (!initialized)
            {
                bounds = new Bounds(root.position, Vector3.one);
            }

            bounds.size = new Vector3(
                Mathf.Max(bounds.size.x, 1f),
                Mathf.Max(bounds.size.y, 1f),
                Mathf.Max(bounds.size.z, 1f));

            return bounds;
        }
    }
}
