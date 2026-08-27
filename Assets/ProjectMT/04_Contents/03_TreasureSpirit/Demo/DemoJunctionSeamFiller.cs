using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    /// <summary>
    /// 바닥 타일 사이 벌어진 틈만, 기존 바닥과 같은 색·재질로 메웁니다. Wall_DoorStand는 건드리지 않습니다.
    /// </summary>
    internal static class DemoJunctionSeamFiller
    {
        private const float MinGap = 0.03f;
        private const float MaxFloorGap = 1.15f;
        private const float MinOverlap = 0.55f;
        private const float Embed = 0.14f;

        public static void Install(Transform mapRoot)
        {
            if (mapRoot == null)
            {
                return;
            }

            List<Renderer> floors = CollectFloorRenderers(mapRoot);
            if (floors.Count == 0)
            {
                Debug.LogWarning("[DemoJunctionSeamFiller] 바닥 타일을 찾지 못해 틈새 보강을 건너뜁니다.");
                return;
            }

            Transform folder = new GameObject("SeamFillers").transform;
            folder.SetParent(mapRoot, false);
            int filled = FillFloorSeams(floors, folder);
            Debug.Log($"[DemoJunctionSeamFiller] 바닥 이음 {filled} ({mapRoot.name})");
        }

        private static int FillFloorSeams(List<Renderer> pieces, Transform folder)
        {
            int filled = 0;

            for (int i = 0; i < pieces.Count; i++)
            {
                Renderer first = pieces[i];
                if (first == null)
                {
                    continue;
                }

                Bounds a = first.bounds;
                for (int j = i + 1; j < pieces.Count; j++)
                {
                    Renderer second = pieces[j];
                    if (second == null)
                    {
                        continue;
                    }

                    if (!TryGetSeam(a, second.bounds, out Vector3 center, out Vector3 size))
                    {
                        continue;
                    }

                    CreateFillBox(folder, center, size, first);
                    filled++;
                }
            }

            return filled;
        }

        private static bool TryGetSeam(Bounds a, Bounds b, out Vector3 center, out Vector3 size)
        {
            center = Vector3.zero;
            size = Vector3.zero;

            float gapX = AxisSeparation(a.min.x, a.max.x, b.min.x, b.max.x);
            float gapZ = AxisSeparation(a.min.z, a.max.z, b.min.z, b.max.z);
            float overlapX = AxisOverlap(a.min.x, a.max.x, b.min.x, b.max.x);
            float overlapZ = AxisOverlap(a.min.z, a.max.z, b.min.z, b.max.z);
            float overlapY = AxisOverlap(a.min.y, a.max.y, b.min.y, b.max.y);
            if (overlapY < 0.02f)
            {
                return false;
            }

            bool seamOnX = gapX >= MinGap && gapX <= MaxFloorGap && overlapZ >= MinOverlap;
            bool seamOnZ = gapZ >= MinGap && gapZ <= MaxFloorGap && overlapX >= MinOverlap;
            if (seamOnX == seamOnZ)
            {
                return false;
            }

            float minY = Mathf.Min(a.min.y, b.min.y);
            float maxY = Mathf.Max(a.max.y, b.max.y);
            if (seamOnX)
            {
                float x0 = a.max.x <= b.min.x ? a.max.x : b.max.x;
                float x1 = a.max.x <= b.min.x ? b.min.x : a.min.x;
                float z0 = Mathf.Max(a.min.z, b.min.z);
                float z1 = Mathf.Min(a.max.z, b.max.z);
                center = new Vector3((x0 + x1) * 0.5f, (minY + maxY) * 0.5f, (z0 + z1) * 0.5f);
                size = new Vector3(Mathf.Max(0.08f, x1 - x0) + Embed * 2f, Mathf.Max(0.1f, maxY - minY), (z1 - z0) + Embed);
            }
            else
            {
                float z0 = a.max.z <= b.min.z ? a.max.z : b.max.z;
                float z1 = a.max.z <= b.min.z ? b.min.z : a.min.z;
                float x0 = Mathf.Max(a.min.x, b.min.x);
                float x1 = Mathf.Min(a.max.x, b.max.x);
                center = new Vector3((x0 + x1) * 0.5f, (minY + maxY) * 0.5f, (z0 + z1) * 0.5f);
                size = new Vector3((x1 - x0) + Embed, Mathf.Max(0.1f, maxY - minY), Mathf.Max(0.08f, z1 - z0) + Embed * 2f);
            }

            return size.x < 8f && size.z < 8f;
        }

        private static float AxisSeparation(float aMin, float aMax, float bMin, float bMax)
        {
            if (aMax <= bMin)
            {
                return bMin - aMax;
            }

            if (bMax <= aMin)
            {
                return aMin - bMax;
            }

            return 0f;
        }

        private static float AxisOverlap(float aMin, float aMax, float bMin, float bMax)
        {
            return Mathf.Max(0f, Mathf.Min(aMax, bMax) - Mathf.Max(aMin, bMin));
        }

        private static List<Renderer> CollectFloorRenderers(Transform mapRoot)
        {
            Renderer[] renderers = mapRoot.GetComponentsInChildren<Renderer>(true);
            List<Renderer> matched = new List<Renderer>(renderers.Length);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null && IsFloor(renderer.gameObject.name))
                {
                    matched.Add(renderer);
                }
            }

            return matched;
        }

        private static void CreateFillBox(Transform parent, Vector3 worldPosition, Vector3 worldSize, Renderer floor)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "SeamFloor";
            box.transform.SetParent(parent, false);
            box.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
            box.transform.localScale = worldSize;
            box.isStatic = false;

            MeshRenderer meshRenderer = box.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                return;
            }

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (floor != null && floor.sharedMaterial != null)
            {
                meshRenderer.sharedMaterial = floor.sharedMaterial;
                ApplyFloorUvAndVertexColor(box, floor);
            }
        }

        private static void ApplyFloorUvAndVertexColor(GameObject fillBox, Renderer floor)
        {
            MeshFilter floorFilter = floor.GetComponent<MeshFilter>();
            MeshFilter fillFilter = fillBox.GetComponent<MeshFilter>();
            Mesh floorMesh = floorFilter != null ? floorFilter.sharedMesh : null;
            if (floorMesh == null || fillFilter == null || fillFilter.sharedMesh == null)
            {
                return;
            }

            Mesh fillMesh = UnityEngine.Object.Instantiate(fillFilter.sharedMesh);
            Vector2[] floorUv = floorMesh.uv;
            Vector2[] fillUv = fillMesh.uv;
            if (floorUv != null && floorUv.Length > 0 && fillUv != null && fillUv.Length > 0)
            {
                Vector2 uvMin = floorUv[0];
                Vector2 uvMax = floorUv[0];
                for (int i = 1; i < floorUv.Length; i++)
                {
                    uvMin = Vector2.Min(uvMin, floorUv[i]);
                    uvMax = Vector2.Max(uvMax, floorUv[i]);
                }

                Vector2 uvSize = uvMax - uvMin;
                if (uvSize.x < 0.0001f)
                {
                    uvSize.x = 0.0001f;
                }

                if (uvSize.y < 0.0001f)
                {
                    uvSize.y = 0.0001f;
                }

                for (int i = 0; i < fillUv.Length; i++)
                {
                    fillUv[i] = uvMin + Vector2.Scale(fillUv[i], uvSize);
                }

                fillMesh.uv = fillUv;
            }

            if (floorMesh.HasVertexAttribute(VertexAttribute.Color) && floorMesh.colors != null && floorMesh.colors.Length > 0)
            {
                Color[] source = floorMesh.colors;
                Color average = Color.black;
                for (int i = 0; i < source.Length; i++)
                {
                    average += source[i];
                }

                average /= source.Length;
                Color[] fillColors = new Color[fillMesh.vertexCount];
                for (int i = 0; i < fillColors.Length; i++)
                {
                    fillColors[i] = average;
                }

                fillMesh.colors = fillColors;
            }

            fillFilter.sharedMesh = fillMesh;
        }

        private static bool IsFloor(string objectName)
        {
            return !string.IsNullOrEmpty(objectName) &&
                   objectName.StartsWith(DemoFloorBounds.FloorNamePrefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
