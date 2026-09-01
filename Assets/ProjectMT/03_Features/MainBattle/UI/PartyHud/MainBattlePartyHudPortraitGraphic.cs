using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [AddComponentMenu("ProjectMT/UI/Main Battle Party HUD Portrait")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MainBattlePartyHudPortraitGraphic : MaskableGraphic // 원형 선 없이 가장자리만 투명화
    {
        [SerializeField] private Sprite sprite;
        [SerializeField, Range(0.5f, 0.98f)] private float fadeStart = 0.76f;
        [SerializeField, Range(24, 96)] private int segmentCount = 56;

        public Sprite Sprite
        {
            get => sprite;
            set
            {
                if (sprite == value)
                {
                    return;
                }

                sprite = value;
                SetAllDirty();
            }
        }

        public float FadeStart => fadeStart;
        public override Texture mainTexture => sprite != null ? sprite.texture : s_WhiteTexture;

        public void Configure(float radialFadeStart, int segments = 56)
        {
            fadeStart = Mathf.Clamp(radialFadeStart, 0.5f, 0.98f);
            segmentCount = Mathf.Clamp(segments, 24, 96);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (sprite == null)
            {
                return;
            }

            var rect = GetPixelAdjustedRect();
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (radius <= 0f)
            {
                return;
            }

            var uv = DataUtility.GetOuterUV(sprite);
            var center = rect.center;
            var innerRadius = radius * fadeStart;
            var centerUv = new Vector2((uv.x + uv.z) * 0.5f, (uv.y + uv.w) * 0.5f);
            vertexHelper.AddVert(center, color, centerUv);

            var opaqueColor = (Color32)color;
            var transparentColor = opaqueColor;
            transparentColor.a = 0;
            var segments = Mathf.Max(24, segmentCount);
            for (var index = 0; index <= segments; index++)
            {
                var normalized = index / (float)segments;
                var angle = normalized * Mathf.PI * 2f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertexHelper.AddVert(
                    center + direction * innerRadius,
                    opaqueColor,
                    ResolveUv(uv, direction * (fadeStart * 0.5f)));
                vertexHelper.AddVert(
                    center + direction * radius,
                    transparentColor,
                    ResolveUv(uv, direction * 0.5f));
            }

            for (var index = 0; index < segments; index++)
            {
                var inner = 1 + index * 2;
                var outer = inner + 1;
                var nextInner = inner + 2;
                var nextOuter = outer + 2;
                vertexHelper.AddTriangle(0, nextInner, inner);
                vertexHelper.AddTriangle(inner, nextOuter, outer);
                vertexHelper.AddTriangle(inner, nextInner, nextOuter);
            }
        }

        private static Vector2 ResolveUv(Vector4 outerUv, Vector2 normalizedOffset)
        {
            var width = outerUv.z - outerUv.x;
            var height = outerUv.w - outerUv.y;
            return new Vector2(
                Mathf.Lerp(outerUv.x, outerUv.z, normalizedOffset.x + 0.5f),
                Mathf.Lerp(outerUv.y, outerUv.w, normalizedOffset.y + 0.5f));
        }
    }
}
