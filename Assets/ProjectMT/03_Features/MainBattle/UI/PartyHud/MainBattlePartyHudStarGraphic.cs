using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [AddComponentMenu("ProjectMT/UI/Main Battle Party HUD Star")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MainBattlePartyHudStarGraphic : MaskableGraphic // 돌파 수를 표시하는 독립 별
    {
        [SerializeField, Range(0.2f, 0.8f)] private float innerRadiusRatio = 0.46f;

        public void Configure(Color graphicColor)
        {
            color = graphicColor;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var rect = GetPixelAdjustedRect();
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (radius <= 0f)
            {
                return;
            }

            var center = rect.center;
            vertexHelper.AddVert(center, color, Vector2.zero);
            for (var index = 0; index < 10; index++)
            {
                var pointRadius = index % 2 == 0 ? radius : radius * innerRadiusRatio;
                var angle = (90f + index * 36f) * Mathf.Deg2Rad;
                var position = center + new Vector2(-Mathf.Cos(angle), Mathf.Sin(angle)) * pointRadius;
                vertexHelper.AddVert(position, color, Vector2.zero);
            }

            for (var index = 0; index < 10; index++)
            {
                vertexHelper.AddTriangle(0, index + 1, ((index + 1) % 10) + 1);
            }
        }
    }
}
