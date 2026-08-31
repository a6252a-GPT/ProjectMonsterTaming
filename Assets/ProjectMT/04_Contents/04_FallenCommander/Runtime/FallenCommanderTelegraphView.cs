using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    [DisallowMultipleComponent]
    public sealed class FallenCommanderTelegraphView : MonoBehaviour
    {
        private const int CircleSegments = 64;
        public const float GroundOffset = 0.06f;
        private const float LineNearWidthRatio = 0.12f;

        private Transform progressFill;
        private float progressFillYScale;
        private LineRenderer maximumOutline;
        private Material outlineMaterial;
        private Mesh lineFillMesh;
        private Vector3[] lineFillSourceVertices;
        private Vector3[] lineFillVertices;
        private Vector2[] lineFillSourceUvs;
        private Vector2[] lineFillUvs;
        private int lineNearLeftIndex = -1;
        private int lineNearRightIndex = -1;
        private int lineFarLeftIndex = -1;
        private int lineFarRightIndex = -1;
        private bool isLine;
        private bool usesLineAreaFill;
        private float maximumRadius;
        private float maximumWidth;
        private float maximumLength;

        public bool IsLine => isLine;
        public float MaximumRadius => maximumRadius;
        public float MaximumWidth => maximumWidth;
        public float MaximumLength => maximumLength;
        public float Progress { get; private set; }

        // 직선 공격의 진행 거리에 따라 좁은 시작점에서 넓은 끝점까지 반폭을 계산한다.
        public static float CalculateLineHalfWidth(
            float width,
            float length,
            float forwardDistance)
        {
            var safeWidth = Mathf.Max(0.1f, width);
            var safeLength = Mathf.Max(0.1f, length);
            var progress = Mathf.Clamp01(forwardDistance / safeLength);
            return Mathf.Lerp(
                safeWidth * 0.5f * LineNearWidthRatio,
                safeWidth * 0.5f,
                progress);
        }

        public static FallenCommanderTelegraphView CreateCircle(
            GameObject fillPrefab,
            Transform parent,
            Vector3 position,
            float radius,
            Color color)
        {
            var view = Create(fillPrefab, parent, position, Quaternion.identity, color);
            if (view == null)
            {
                return null;
            }

            view.ConfigureCircle(radius, color);
            return view;
        }

        public static FallenCommanderTelegraphView CreateLine(
            GameObject fillPrefab,
            Transform parent,
            Vector3 origin,
            Vector3 direction,
            float width,
            float length,
            Color color)
        {
            var rotation = Quaternion.LookRotation(direction, Vector3.up);
            var view = Create(fillPrefab, parent, origin, rotation, color);
            if (view == null)
            {
                return null;
            }

            view.ConfigureLine(width, length, color);
            return view;
        }

        // 기본 투사체의 실제 이동 판정 폭과 길이에 맞는 직사각형 전조를 생성한다.
        public static FallenCommanderTelegraphView CreateRectangle(
            GameObject fillPrefab,
            Transform parent,
            Vector3 origin,
            Vector3 direction,
            float width,
            float length,
            Color color)
        {
            var rotation = Quaternion.LookRotation(direction, Vector3.up);
            var view = Create(fillPrefab, parent, origin, rotation, color);
            if (view == null)
            {
                return null;
            }

            view.ConfigureRectangle(width, length, color);
            return view;
        }

        public void SetProgress(float progress)
        {
            Progress = Mathf.Clamp01(progress);
            if (progressFill == null)
            {
                return;
            }

            if (isLine)
            {
                if (usesLineAreaFill && lineFillMesh != null)
                {
                    progressFill.localPosition = new Vector3(0f, 0f, maximumLength * 0.5f);
                    progressFill.localScale = new Vector3(
                        maximumWidth,
                        progressFillYScale,
                        maximumLength);
                    UpdateLineAreaFill(Progress);
                    return;
                }

                var filledLength = Mathf.Max(0.01f, maximumLength * Progress);
                progressFill.localPosition = new Vector3(0f, 0f, filledLength * 0.5f);
                progressFill.localScale = new Vector3(
                    maximumWidth,
                    progressFillYScale,
                    filledLength);
                return;
            }

            var diameter = Mathf.Max(0.01f, maximumRadius * 2f * Progress);
            progressFill.localPosition = Vector3.zero;
            progressFill.localScale = new Vector3(
                diameter,
                progressFillYScale,
                diameter);
        }

        private static FallenCommanderTelegraphView Create(
            GameObject fillPrefab,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            Color color)
        {
            if (fillPrefab == null)
            {
                return null;
            }

            position.y += GroundOffset;
            var root = new GameObject("FallenCommanderTelegraph");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);
            var view = root.AddComponent<FallenCommanderTelegraphView>();

            var fill = Instantiate(fillPrefab, root.transform);
            fill.name = "ProgressFill";
            fill.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            view.progressFill = fill.transform;
            view.progressFillYScale = Mathf.Max(0.001f, fill.transform.localScale.y);
            ApplyColor(fill, new Color(color.r, color.g, color.b, color.a * 0.65f));

            var outlineObject = new GameObject("MaximumOutline");
            outlineObject.transform.SetParent(root.transform, false);
            view.maximumOutline = outlineObject.AddComponent<LineRenderer>();
            view.maximumOutline.useWorldSpace = false;
            view.maximumOutline.loop = true;
            view.maximumOutline.alignment = LineAlignment.TransformZ;
            view.maximumOutline.textureMode = LineTextureMode.Stretch;
            view.maximumOutline.startColor = color;
            view.maximumOutline.endColor = color;

            var sourceRenderer = fill.GetComponentInChildren<Renderer>(true);
            var sourceMaterial = sourceRenderer == null ? null : sourceRenderer.sharedMaterial;
            if (sourceMaterial != null)
            {
                view.outlineMaterial = new Material(sourceMaterial);
                view.maximumOutline.sharedMaterial = view.outlineMaterial;
            }

            ApplyColor(outlineObject, new Color(color.r, color.g, color.b, 1f));
            return view;
        }

        private void ConfigureCircle(float radius, Color color)
        {
            isLine = false;
            maximumRadius = Mathf.Max(0.1f, radius);
            maximumWidth = 0f;
            maximumLength = 0f;
            maximumOutline.widthMultiplier = Mathf.Clamp(maximumRadius * 0.035f, 0.06f, 0.16f);
            maximumOutline.positionCount = CircleSegments;
            for (var index = 0; index < CircleSegments; index++)
            {
                var angle = Mathf.PI * 2f * index / CircleSegments;
                maximumOutline.SetPosition(index, new Vector3(
                    Mathf.Cos(angle) * maximumRadius,
                    0.03f,
                    Mathf.Sin(angle) * maximumRadius));
            }

            ApplyColor(maximumOutline.gameObject, color);
            SetProgress(0f);
        }

        // 직선 공격 전조를 바깥쪽으로 넓어지는 쐐기형 외곽선으로 구성한다.
        private void ConfigureLine(float width, float length, Color color)
        {
            isLine = true;
            maximumRadius = 0f;
            maximumWidth = Mathf.Max(0.1f, width);
            maximumLength = Mathf.Max(0.1f, length);
            usesLineAreaFill = TryConfigureLineAreaFill();
            var nearHalfWidth = CalculateLineHalfWidth(
                maximumWidth,
                maximumLength,
                0f);
            var farHalfWidth = CalculateLineHalfWidth(
                maximumWidth,
                maximumLength,
                maximumLength);
            maximumOutline.widthMultiplier = Mathf.Clamp(maximumWidth * 0.04f, 0.06f, 0.14f);
            maximumOutline.positionCount = 4;
            maximumOutline.SetPosition(0, new Vector3(-nearHalfWidth, 0.03f, 0f));
            maximumOutline.SetPosition(1, new Vector3(-farHalfWidth, 0.03f, maximumLength));
            maximumOutline.SetPosition(2, new Vector3(farHalfWidth, 0.03f, maximumLength));
            maximumOutline.SetPosition(3, new Vector3(nearHalfWidth, 0.03f, 0f));
            ApplyColor(maximumOutline.gameObject, color);
            SetProgress(0f);
        }

        // 앞뒤 폭이 동일한 직사각형 외곽선을 실제 공격 범위 크기로 구성한다.
        private void ConfigureRectangle(float width, float length, Color color)
        {
            isLine = true;
            usesLineAreaFill = false;
            maximumRadius = 0f;
            maximumWidth = Mathf.Max(0.1f, width);
            maximumLength = Mathf.Max(0.1f, length);
            var halfWidth = maximumWidth * 0.5f;
            maximumOutline.widthMultiplier = Mathf.Clamp(
                maximumWidth * 0.04f,
                0.06f,
                0.14f);
            maximumOutline.positionCount = 4;
            maximumOutline.SetPosition(0, new Vector3(-halfWidth, 0.03f, 0f));
            maximumOutline.SetPosition(1, new Vector3(-halfWidth, 0.03f, maximumLength));
            maximumOutline.SetPosition(2, new Vector3(halfWidth, 0.03f, maximumLength));
            maximumOutline.SetPosition(3, new Vector3(halfWidth, 0.03f, 0f));
            ApplyColor(maximumOutline.gameObject, color);
            SetProgress(0f);
        }

        // 전찬우 수정: 삼각 인디케이터 크기는 고정하고 원본 메시 내부를 진행 방향으로 채운다.
        private bool TryConfigureLineAreaFill()
        {
            var meshFilter = progressFill.GetComponentInChildren<MeshFilter>(true);
            var sourceMesh = meshFilter == null ? null : meshFilter.sharedMesh;
            if (sourceMesh == null || sourceMesh.vertexCount != 4)
            {
                return false;
            }

            lineFillSourceVertices = sourceMesh.vertices;
            lineFillSourceUvs = sourceMesh.uv;
            if (lineFillSourceUvs == null ||
                lineFillSourceUvs.Length != lineFillSourceVertices.Length)
            {
                lineFillSourceUvs = new Vector2[lineFillSourceVertices.Length];
            }

            var minimumZ = float.MaxValue;
            var maximumZ = float.MinValue;
            foreach (var vertex in lineFillSourceVertices)
            {
                minimumZ = Mathf.Min(minimumZ, vertex.z);
                maximumZ = Mathf.Max(maximumZ, vertex.z);
            }

            for (var index = 0; index < lineFillSourceVertices.Length; index++)
            {
                var vertex = lineFillSourceVertices[index];
                if (Mathf.Approximately(vertex.z, minimumZ))
                {
                    if (lineNearLeftIndex < 0 ||
                        vertex.x < lineFillSourceVertices[lineNearLeftIndex].x)
                    {
                        lineNearLeftIndex = index;
                    }

                    if (lineNearRightIndex < 0 ||
                        vertex.x > lineFillSourceVertices[lineNearRightIndex].x)
                    {
                        lineNearRightIndex = index;
                    }
                }

                if (Mathf.Approximately(vertex.z, maximumZ))
                {
                    if (lineFarLeftIndex < 0 ||
                        vertex.x < lineFillSourceVertices[lineFarLeftIndex].x)
                    {
                        lineFarLeftIndex = index;
                    }

                    if (lineFarRightIndex < 0 ||
                        vertex.x > lineFillSourceVertices[lineFarRightIndex].x)
                    {
                        lineFarRightIndex = index;
                    }
                }
            }

            if (lineNearLeftIndex < 0 ||
                lineNearRightIndex < 0 ||
                lineFarLeftIndex < 0 ||
                lineFarRightIndex < 0)
            {
                return false;
            }

            lineFillMesh = Instantiate(sourceMesh);
            lineFillMesh.name = $"{sourceMesh.name}_RuntimeAreaFill";
            lineFillMesh.hideFlags = HideFlags.HideAndDontSave;
            lineFillMesh.MarkDynamic();
            meshFilter.sharedMesh = lineFillMesh;
            lineFillVertices = (Vector3[])lineFillSourceVertices.Clone();
            lineFillUvs = (Vector2[])lineFillSourceUvs.Clone();
            return true;
        }

        private void UpdateLineAreaFill(float progress)
        {
            for (var index = 0; index < lineFillVertices.Length; index++)
            {
                lineFillVertices[index] = lineFillSourceVertices[index];
                lineFillUvs[index] = lineFillSourceUvs[index];
            }

            lineFillVertices[lineFarLeftIndex] = Vector3.Lerp(
                lineFillSourceVertices[lineNearLeftIndex],
                lineFillSourceVertices[lineFarLeftIndex],
                progress);
            lineFillVertices[lineFarRightIndex] = Vector3.Lerp(
                lineFillSourceVertices[lineNearRightIndex],
                lineFillSourceVertices[lineFarRightIndex],
                progress);
            lineFillUvs[lineFarLeftIndex] = Vector2.Lerp(
                lineFillSourceUvs[lineNearLeftIndex],
                lineFillSourceUvs[lineFarLeftIndex],
                progress);
            lineFillUvs[lineFarRightIndex] = Vector2.Lerp(
                lineFillSourceUvs[lineNearRightIndex],
                lineFillSourceUvs[lineFarRightIndex],
                progress);

            lineFillMesh.vertices = lineFillVertices;
            lineFillMesh.uv = lineFillUvs;
            lineFillMesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (lineFillMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(lineFillMesh);
                }
                else
                {
                    DestroyImmediate(lineFillMesh);
                }

                lineFillMesh = null;
            }

            if (outlineMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(outlineMaterial);
                }
                else
                {
                    DestroyImmediate(outlineMaterial);
                }

                outlineMaterial = null;
            }
        }

        private static void ApplyColor(GameObject target, Color color)
        {
            foreach (var lineRenderer in target.GetComponentsInChildren<LineRenderer>(true))
            {
                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
            }

            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_Color", color);
                propertyBlock.SetColor("_BaseColor", color);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
