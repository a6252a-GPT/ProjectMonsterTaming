using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleDeploymentAreaVisual : MonoBehaviour // 선택 중인 몬스터의 배치 가능 육각 셀 표시
    {
        private const float VisualHeight = 0.055f;
        private const float FillRadiusRatio = 0.86f;
        private const float OutlineInnerRadiusRatio = 0.84f;
        private const float OutlineOuterRadiusRatio = 0.98f;

        private GameObject visualRoot;
        private Mesh areaMesh;
        private Material fillMaterial;
        private Material outlineMaterial;
        private int allowedCellCount;
        private bool visible;

        public int AllowedCellCount => allowedCellCount;
        public bool IsVisible => visualRoot != null && visualRoot.activeSelf;

        public void Configure(IEnumerable<HexCastleCellRuntime> cells)
        {
            ReleaseVisuals();
            var coordinates = cells == null
                ? new List<HexCoordinates>()
                : cells.Where(value => value != null &&
                                       value.Kind == HexCastleCellKind.Deployment &&
                                       !value.InitialBlocked)
                    .Select(value => value.Coordinates)
                    .Distinct()
                    .OrderBy(value => value)
                    .ToList();
            allowedCellCount = coordinates.Count;
            if (allowedCellCount == 0)
            {
                return;
            }

            var shader = Shader.Find("Sprites/Default") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("UI/Default");
            if (shader == null)
            {
                throw new System.InvalidOperationException("Hex 배치 영역 표시 Shader를 찾을 수 없습니다.");
            }

            areaMesh = BuildAreaMesh(coordinates);
            fillMaterial = CreateMaterial(
                shader,
                "Runtime_CRHex_DeploymentFill",
                new Color(0.10f, 0.95f, 0.62f, 0.18f),
                3100);
            outlineMaterial = CreateMaterial(
                shader,
                "Runtime_CRHex_DeploymentOutline",
                new Color(0.26f, 1f, 0.78f, 0.86f),
                3101);

            visualRoot = new GameObject("02_DeploymentAreaVisual", typeof(MeshFilter), typeof(MeshRenderer));
            visualRoot.transform.SetParent(transform, false);
            visualRoot.transform.localPosition = Vector3.zero;
            visualRoot.hideFlags = HideFlags.DontSave;
            visualRoot.GetComponent<MeshFilter>().sharedMesh = areaMesh;
            var renderer = visualRoot.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { fillMaterial, outlineMaterial };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            visualRoot.SetActive(visible);
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (visualRoot != null && visualRoot.activeSelf != value)
            {
                visualRoot.SetActive(value);
            }
        }

        private static Mesh BuildAreaMesh(IReadOnlyList<HexCoordinates> coordinates)
        {
            var vertices = new List<Vector3>(coordinates.Count * 20);
            var fillTriangles = new List<int>(coordinates.Count * 18);
            var outlineTriangles = new List<int>(coordinates.Count * 36);
            foreach (var coordinate in coordinates)
            {
                var center = HexSpatialContract.ToWorld(coordinate) + Vector3.up * VisualHeight;
                var fillStart = vertices.Count;
                vertices.Add(center);
                for (var index = 0; index < 6; index++)
                {
                    vertices.Add(center + ResolveCorner(index, FillRadiusRatio));
                }

                for (var index = 0; index < 6; index++)
                {
                    fillTriangles.Add(fillStart);
                    fillTriangles.Add(fillStart + 1 + (index + 1) % 6);
                    fillTriangles.Add(fillStart + 1 + index);
                }

                var outlineStart = vertices.Count;
                for (var index = 0; index < 6; index++)
                {
                    vertices.Add(center + ResolveCorner(index, OutlineOuterRadiusRatio));
                    vertices.Add(center + ResolveCorner(index, OutlineInnerRadiusRatio));
                }

                for (var index = 0; index < 6; index++)
                {
                    var next = (index + 1) % 6;
                    var currentOuter = outlineStart + index * 2;
                    var currentInner = currentOuter + 1;
                    var nextOuter = outlineStart + next * 2;
                    var nextInner = nextOuter + 1;
                    outlineTriangles.Add(currentOuter);
                    outlineTriangles.Add(currentInner);
                    outlineTriangles.Add(nextOuter);
                    outlineTriangles.Add(currentInner);
                    outlineTriangles.Add(nextInner);
                    outlineTriangles.Add(nextOuter);
                }
            }

            var mesh = new Mesh
            {
                name = "MESH_CRHex_DeploymentArea_Runtime",
                hideFlags = HideFlags.DontSave
            };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(fillTriangles, 0);
            mesh.SetTriangles(outlineTriangles, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 ResolveCorner(int index, float radiusRatio)
        {
            var angle = Mathf.Deg2Rad * (30f + index * 60f);
            var radius = HexSpatialContract.CellOuterRadius * radiusRatio;
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        private static Material CreateMaterial(
            Shader shader,
            string materialName,
            Color color,
            int renderQueue)
        {
            var material = new Material(shader)
            {
                name = materialName,
                color = color,
                renderQueue = renderQueue,
                hideFlags = HideFlags.DontSave
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            return material;
        }

        private void ReleaseVisuals()
        {
            ReleaseObject(visualRoot);
            ReleaseObject(areaMesh);
            ReleaseObject(fillMaterial);
            ReleaseObject(outlineMaterial);
            visualRoot = null;
            areaMesh = null;
            fillMaterial = null;
            outlineMaterial = null;
            allowedCellCount = 0;
        }

        private static void ReleaseObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void OnDestroy()
        {
            ReleaseVisuals();
        }
    }
}
