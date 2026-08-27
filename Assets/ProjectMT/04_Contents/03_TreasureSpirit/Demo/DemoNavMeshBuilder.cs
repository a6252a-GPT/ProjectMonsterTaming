using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal static class DemoNavMeshBuilder
    {
        private static NavMeshDataInstance navMeshInstance;

        public static bool BuildForMap(GameObject mapInstance)
        {
            if (mapInstance == null)
            {
                return false;
            }

            RemoveNavMesh();
            DemoMapColliderBaker.Bake(mapInstance.transform);

            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
            CollectFloorColliderSources(mapInstance.transform, sources);
            CollectDoorwaySources(mapInstance.transform, sources);
            if (sources.Count == 0)
            {
                Debug.LogError("[DemoNavMeshBuilder] Floor 메시를 찾지 못해 NavMesh를 만들 수 없습니다.");
                return false;
            }

            Bounds bounds = CalculateFloorBounds(mapInstance.transform);
            bounds.Expand(new Vector3(2f, 4f, 2f));

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.2f;
            settings.agentHeight = 1.6f;
            settings.agentSlope = 50f;
            settings.agentClimb = 0.5f;
            settings.minRegionArea = 0.1f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.06f;

            NavMeshData navMeshData = NavMeshBuilder.BuildNavMeshData(
                settings,
                sources,
                bounds,
                mapInstance.transform.position,
                mapInstance.transform.rotation);

            if (navMeshData == null)
            {
                Debug.LogError("[DemoNavMeshBuilder] NavMesh 데이터 생성에 실패했습니다.");
                return false;
            }

            navMeshInstance = NavMesh.AddNavMeshData(
                navMeshData,
                mapInstance.transform.position,
                mapInstance.transform.rotation);

            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            int vertexCount = triangulation.vertices != null ? triangulation.vertices.Length : 0;
            if (vertexCount == 0)
            {
                Debug.LogError("[DemoNavMeshBuilder] NavMesh 삼각형이 없습니다.");
                return false;
            }

            Debug.Log($"[DemoNavMeshBuilder] NavMesh 베이크 완료 (floors={sources.Count}, verts={vertexCount})");
            return true;
        }

        public static void RemoveNavMesh()
        {
            if (navMeshInstance.valid)
            {
                navMeshInstance.Remove();
            }

            navMeshInstance = default;
        }

        public static void DestroyExistingProxies(Transform mapRoot)
        {
            RemoveNavMesh();

            if (mapRoot == null)
            {
                return;
            }

            Transform existing = mapRoot.Find("DemoNavMeshProxies");
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

        private static void CollectFloorColliderSources(Transform mapRoot, List<NavMeshBuildSource> sources)
        {
            BoxCollider[] colliders = mapRoot.GetComponentsInChildren<BoxCollider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                BoxCollider floorCollider = colliders[i];
                if (floorCollider == null ||
                    !floorCollider.enabled ||
                    floorCollider.isTrigger ||
                    !floorCollider.gameObject.name.StartsWith(DemoFloorBounds.FloorNamePrefix))
                {
                    continue;
                }

                NavMeshBuildSource source = new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    size = floorCollider.size,
                    transform = floorCollider.transform.localToWorldMatrix * Matrix4x4.Translate(floorCollider.center),
                    area = 0
                };
                sources.Add(source);
            }
        }

        private static void CollectDoorwaySources(Transform mapRoot, List<NavMeshBuildSource> sources)
        {
            Transform[] allTransforms = mapRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform door = allTransforms[i];
                if (door == null || !IsDoorwayName(door.name))
                {
                    continue;
                }

                Vector3 center = door.position;
                if (DemoFloorBounds.TryGetSurface(door.parent != null ? door.parent : door, out Vector3 floorPoint))
                {
                    center.y = floorPoint.y + 0.05f;
                }
                else
                {
                    center.y += 0.05f;
                }

                NavMeshBuildSource source = new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    size = new Vector3(2.6f, 0.25f, 2.6f),
                    transform = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one),
                    area = 0
                };
                sources.Add(source);
            }
        }

        private static bool IsDoorwayName(string objectName)
        {
            return objectName == "NorthDoor" ||
                   objectName == "SouthDoor" ||
                   objectName == "EastDoor" ||
                   objectName == "WestDoor";
        }

        private static Bounds CalculateFloorBounds(Transform mapRoot)
        {
            MeshRenderer[] renderers = mapRoot.GetComponentsInChildren<MeshRenderer>(true);
            Bounds bounds = new Bounds(mapRoot.position, Vector3.one);
            bool initialized = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer renderer = renderers[i];
                if (renderer == null || !renderer.gameObject.name.StartsWith(DemoFloorBounds.FloorNamePrefix))
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }
    }
}
