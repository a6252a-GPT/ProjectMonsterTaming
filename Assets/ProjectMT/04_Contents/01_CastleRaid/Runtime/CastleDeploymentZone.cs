using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace ProjectMT.Contents.CastleRaid
{
    [DisallowMultipleComponent]
    public sealed class CastleDeploymentZone : MonoBehaviour // 성 외곽 배치 판정과 영역 표시
    {
        [SerializeField] private Vector2 outerHalfExtents = new Vector2(9.2f, 9.2f); // 배치 링 바깥 경계
        [SerializeField] private Vector2 innerHalfExtents = new Vector2(6.2f, 6.2f); // 성 주변 제외 경계
        [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 1f; // 걸을 수 있는 점 탐색 반경

        private readonly HashSet<Vector2Int> allowedLocalCells = new HashSet<Vector2Int>();
        private RectInt cellGridBounds;
        private float cellSize = 1f;
        private bool usesExteriorCellMask;
        private GameObject visualRoot;
        private Mesh areaMesh;
        private Material areaMaterial;
        private Texture2D areaTexture;
        private bool visualVisible = true;

        public Vector2 OuterHalfExtents => outerHalfExtents;
        public Vector2 InnerHalfExtents => innerHalfExtents;
        public bool UsesExteriorCellMask => usesExteriorCellMask;
        public int AllowedCellCount => allowedLocalCells.Count;
        public bool IsVisualVisible => visualRoot != null && visualRoot.activeSelf;

        public void SetVisualVisible(bool visible)
        {
            visualVisible = visible;
            if (visualRoot != null && visualRoot.activeSelf != visible)
            {
                visualRoot.SetActive(visible); // 판정은 유지하고 표시만 전환
            }
        }

        public void ConfigureBounds(Vector2 outer, Vector2 inner, float sampleRadius = 1f)
        {
            ClearExteriorCellMask();
            outerHalfExtents = Vector2.Max(Vector2.one * 0.1f, outer);
            innerHalfExtents = Vector2.Min(
                outerHalfExtents,
                Vector2.Max(Vector2.zero, inner));
            navMeshSampleRadius = Mathf.Max(0.1f, sampleRadius);
        }

        public void ConfigureExteriorCells(
            RectInt gridBounds,
            IReadOnlyCollection<Vector2Int> exteriorCells,
            float targetCellSize,
            float sampleRadius = 1f)
        {
            cellGridBounds = gridBounds;
            cellSize = Mathf.Max(0.01f, targetCellSize);
            navMeshSampleRadius = Mathf.Max(0.1f, sampleRadius);
            outerHalfExtents = new Vector2(gridBounds.width, gridBounds.height) * (cellSize * 0.5f);
            innerHalfExtents = Vector2.zero;
            allowedLocalCells.Clear();
            if (exteriorCells != null)
            {
                foreach (var cell in exteriorCells)
                {
                    if (gridBounds.Contains(cell))
                    {
                        allowedLocalCells.Add(cell - gridBounds.position);
                    }
                }
            }

            usesExteriorCellMask = true;
            BuildWorldVisuals();
        }

        public bool ContainsWorldPosition(Vector3 worldPosition)
        {
            var local = transform.InverseTransformPoint(worldPosition);
            if (usesExteriorCellMask)
            {
                var x = Mathf.FloorToInt(local.x / cellSize + cellGridBounds.width * 0.5f);
                var z = Mathf.FloorToInt(local.z / cellSize + cellGridBounds.height * 0.5f);
                return x >= 0 && x < cellGridBounds.width &&
                       z >= 0 && z < cellGridBounds.height &&
                       allowedLocalCells.Contains(new Vector2Int(x, z));
            }

            var insideOuter = Mathf.Abs(local.x) <= outerHalfExtents.x &&
                              Mathf.Abs(local.z) <= outerHalfExtents.y;
            var outsideInner = Mathf.Abs(local.x) >= innerHalfExtents.x ||
                               Mathf.Abs(local.z) >= innerHalfExtents.y;
            return insideOuter && outsideInner; // 두 사각형 사이만 허용
        }

        public bool TryResolveSpawnPoint(Camera worldCamera, Vector2 screenPosition, out Vector3 spawnPoint)
        {
            spawnPoint = default;
            if (worldCamera == null)
            {
                return false;
            }

            var plane = new Plane(transform.up, transform.position); // 화면 클릭을 맵 평면과 교차
            var ray = worldCamera.ScreenPointToRay(screenPosition);
            if (!plane.Raycast(ray, out var distance))
            {
                return false;
            }

            var worldPoint = ray.GetPoint(distance);
            if (!ContainsWorldPosition(worldPoint) ||
                !NavMesh.SamplePosition(worldPoint, out var hit, navMeshSampleRadius, NavMesh.AllAreas) ||
                !ContainsWorldPosition(hit.position))
            {
                return false;
            }

            spawnPoint = hit.position; // NavMesh 위 최종 소환점
            return true;
        }

        private void BuildWorldVisuals()
        {
            ReleaseWorldVisuals();
            visualRoot = new GameObject("DeploymentAreaVisual");
            visualRoot.transform.SetParent(transform, false);
            visualRoot.hideFlags = HideFlags.DontSave;
            visualRoot.SetActive(visualVisible);

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
            if (shader == null)
            {
                return;
            }

            areaTexture = BuildSmoothedAreaTexture();
            areaMaterial = CreateMaterial(shader, "Runtime_CastleDeploymentArea", Color.white);
            areaMaterial.mainTexture = areaTexture;
            areaMesh = BuildAreaMesh();
            CreateMeshObject("DeploymentAreaSmooth", areaMesh, areaMaterial);
        }

        private Mesh BuildAreaMesh()
        {
            var halfWidth = cellGridBounds.width * cellSize * 0.5f;
            var halfHeight = cellGridBounds.height * cellSize * 0.5f;
            var mesh = new Mesh
            {
                name = "Runtime_CastleDeploymentAreaMesh",
                hideFlags = HideFlags.DontSave,
                vertices = new[]
                {
                    new Vector3(-halfWidth, 0.035f, -halfHeight),
                    new Vector3(-halfWidth, 0.035f, halfHeight),
                    new Vector3(halfWidth, 0.035f, halfHeight),
                    new Vector3(halfWidth, 0.035f, -halfHeight)
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
                uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right }
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private Texture2D BuildSmoothedAreaTexture()
        {
            const int pixelsPerCell = 8;
            const int blurRadius = 8; // 한 셀 폭으로 윤곽을 보간해 계단 모양을 곡선으로 바꾼다
            var width = Mathf.Max(1, cellGridBounds.width * pixelsPerCell);
            var height = Mathf.Max(1, cellGridBounds.height * pixelsPerCell);
            var source = new float[width * height];
            foreach (var cell in allowedLocalCells)
            {
                var startX = cell.x * pixelsPerCell;
                var startZ = cell.y * pixelsPerCell;
                for (var z = startZ; z < startZ + pixelsPerCell; z++)
                {
                    for (var x = startX; x < startX + pixelsPerCell; x++)
                    {
                        source[z * width + x] = 1f;
                    }
                }
            }

            var horizontal = new float[source.Length];
            var smoothed = new float[source.Length];
            BlurHorizontal(source, horizontal, width, height, blurRadius);
            BlurVertical(horizontal, smoothed, width, height, blurRadius);

            var fillColor = new Color(0.16f, 0.88f, 0.72f, 1f);
            var outlineColor = new Color(0.24f, 1f, 0.82f, 1f);
            var pixels = new Color32[smoothed.Length];
            for (var index = 0; index < smoothed.Length; index++)
            {
                var density = smoothed[index];
                var inside = Mathf.SmoothStep(0.34f, 0.66f, density);
                var edge = 1f - Mathf.Clamp01(Mathf.Abs(density - 0.5f) / 0.12f);
                var color = Color.Lerp(fillColor, outlineColor, edge);
                color.a = Mathf.Max(inside * 0.10f, edge * 0.95f);
                pixels[index] = color;
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = "Runtime_CastleDeploymentAreaMask",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void CreateMeshObject(string objectName, Mesh mesh, Material material)
        {
            var visual = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(visualRoot.transform, false);
            visual.hideFlags = HideFlags.DontSave;
            visual.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Material CreateMaterial(Shader shader, string materialName, Color color)
        {
            return new Material(shader)
            {
                name = materialName,
                color = color,
                hideFlags = HideFlags.DontSave
            };
        }

        private static void BlurHorizontal(float[] source, float[] target, int width, int height, int radius)
        {
            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var sum = 0f;
                    for (var offset = -radius; offset <= radius; offset++)
                    {
                        var sampleX = x + offset;
                        if (sampleX < 0 || sampleX >= width)
                        {
                            continue;
                        }

                        sum += source[z * width + sampleX];
                    }

                    target[z * width + x] = sum / (radius * 2 + 1); // 맵 바깥은 빈 공간으로 흐리게 처리
                }
            }
        }

        private static void BlurVertical(float[] source, float[] target, int width, int height, int radius)
        {
            for (var z = 0; z < height; z++)
            {
                for (var x = 0; x < width; x++)
                {
                    var sum = 0f;
                    for (var offset = -radius; offset <= radius; offset++)
                    {
                        var sampleZ = z + offset;
                        if (sampleZ < 0 || sampleZ >= height)
                        {
                            continue;
                        }

                        sum += source[sampleZ * width + x];
                    }

                    target[z * width + x] = sum / (radius * 2 + 1); // 맵 바깥은 빈 공간으로 흐리게 처리
                }
            }
        }

        private void ClearExteriorCellMask()
        {
            usesExteriorCellMask = false;
            allowedLocalCells.Clear();
            ReleaseWorldVisuals();
        }

        private void ReleaseWorldVisuals()
        {
            ReleaseObject(visualRoot);
            ReleaseObject(areaMesh);
            ReleaseObject(areaMaterial);
            ReleaseObject(areaTexture);
            visualRoot = null;
            areaMesh = null;
            areaMaterial = null;
            areaTexture = null;
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
            ReleaseWorldVisuals();
        }

#if UNITY_EDITOR
        public void EditorConfigure(Vector2 outer, Vector2 inner, float sampleRadius = 1f)
        {
            ConfigureBounds(outer, inner, sampleRadius);
        }
#endif
    }
}
