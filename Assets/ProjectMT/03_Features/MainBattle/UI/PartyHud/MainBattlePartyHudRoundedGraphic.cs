using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [AddComponentMenu("ProjectMT/UI/Main Battle Party HUD Rounded Graphic")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MainBattlePartyHudRoundedGraphic : MaskableGraphic // 외부 이미지 없이 그리는 둥근 정보판
    {
        [SerializeField, Min(0f)] private float cornerRadius = 8f;
        [SerializeField, Range(2, 12)] private int cornerSegments = 5;

        public float CornerRadius => cornerRadius;

        public void Configure(float radius, Color graphicColor)
        {
            cornerRadius = Mathf.Max(0f, radius);
            color = graphicColor;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var radius = Mathf.Min(cornerRadius, Mathf.Min(rect.width, rect.height) * 0.5f);
            if (radius <= 0.01f)
            {
                AddQuad(vertexHelper, rect, color);
                return;
            }

            var center = rect.center;
            var centerIndex = vertexHelper.currentVertCount;
            vertexHelper.AddVert(center, color, Vector2.zero);

            AddCorner(vertexHelper, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f);
            AddCorner(vertexHelper, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f, 360f);
            AddCorner(vertexHelper, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f);
            AddCorner(vertexHelper, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f);

            var perimeterCount = vertexHelper.currentVertCount - centerIndex - 1;
            for (var index = 0; index < perimeterCount; index++)
            {
                var current = centerIndex + 1 + index;
                var next = centerIndex + 1 + ((index + 1) % perimeterCount);
                vertexHelper.AddTriangle(centerIndex, next, current);
            }
        }

        private void AddCorner(
            VertexHelper vertexHelper,
            Vector2 center,
            float radius,
            float startAngle,
            float endAngle)
        {
            var segmentCount = Mathf.Max(2, cornerSegments);
            for (var index = 0; index <= segmentCount; index++)
            {
                var angle = Mathf.Lerp(startAngle, endAngle, index / (float)segmentCount) * Mathf.Deg2Rad;
                var position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                vertexHelper.AddVert(position, color, Vector2.zero);
            }
        }

        private static void AddQuad(VertexHelper vertexHelper, Rect rect, Color32 graphicColor)
        {
            vertexHelper.AddVert(new Vector2(rect.xMin, rect.yMin), graphicColor, Vector2.zero);
            vertexHelper.AddVert(new Vector2(rect.xMin, rect.yMax), graphicColor, Vector2.zero);
            vertexHelper.AddVert(new Vector2(rect.xMax, rect.yMax), graphicColor, Vector2.zero);
            vertexHelper.AddVert(new Vector2(rect.xMax, rect.yMin), graphicColor, Vector2.zero);
            vertexHelper.AddTriangle(0, 1, 2);
            vertexHelper.AddTriangle(0, 2, 3);
        }
    }
}
