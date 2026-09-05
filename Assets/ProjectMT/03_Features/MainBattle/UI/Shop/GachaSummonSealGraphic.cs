using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GachaSummonSealGraphic : MaskableGraphic
    {
        [SerializeField] private bool isCardBackGlow;
        public bool IsCardBackGlow
        {
            get => isCardBackGlow;
            set { isCardBackGlow = value; SetVerticesDirty(); }
        }
        public bool IsMythic { get; set; }
        public bool IsBurst { get; set; }
        private float pulse;
        public float Pulse { get => pulse; set { pulse = value; SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (IsCardBackGlow) { DrawCardBackGlow(vh); return; }
            var radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.47f;
            if (IsBurst)
            {
                var rays = IsMythic ? 20 : 12;
                for (var i = 0; i < rays; i++)
                {
                    var angle = i * Mathf.PI * 2f / rays;
                    var length = radius * (i % 2 == 0 ? 1f : 0.74f);
                    Ray(vh, angle, radius * 0.17f, length, IsMythic ? 0.035f : 0.05f);
                }
                Ring(vh, radius * (0.35f + 0.52f * pulse), 3f * (1f - pulse), 96);
                if (IsMythic) Ring(vh, radius * (0.25f + 0.42f * pulse), 2f, 6);
                return;
            }
            Ring(vh, radius, 2.5f, 96);
            Ring(vh, radius * 0.83f, 1.5f, IsMythic ? 8 : 96);
            Ring(vh, radius * 0.56f, 2.5f, IsMythic ? 8 : 6);
            for (var i = 0; i < 12; i++)
            {
                var direction = Point(i * Mathf.PI / 6f);
                Line(vh, direction * radius * 0.9f, direction * radius * 0.98f, 3f);
            }
            var points = IsMythic ? 8 : 6;
            for (var i = 0; i < points; i++)
                Line(vh, Point(i * Mathf.PI * 2f / points) * radius * 0.56f,
                    Point((i + (IsMythic ? 3 : 2)) * Mathf.PI * 2f / points) * radius * 0.56f, 2f);
        }

        private void Update()
        {
            if (IsCardBackGlow) SetVerticesDirty();
        }

        private void DrawCardBackGlow(VertexHelper vh)
        {
            var rect = rectTransform.rect;
            var half = rect.size * 0.5f - Vector2.one * 16f;
            var near = color;
            near.a *= 0.9f + 0.1f * Mathf.Sin(Time.unscaledTime * 1.8f);
            var far = color;
            far.a = 0f;
            const int steps = 6;
            var cornerRadius = Mathf.Min(half.x, half.y) * 0.28f;
            for (var corner = 0; corner < 4; corner++)
            {
                var middle = (corner * 90f + 45f) * Mathf.Deg2Rad;
                var center = rect.center + new Vector2(Mathf.Sign(Mathf.Cos(middle)) * (half.x - cornerRadius), Mathf.Sign(Mathf.Sin(middle)) * (half.y - cornerRadius));
                for (var step = 0; step <= steps; step++)
                {
                    var angle = (corner * 90f + step * 90f / steps) * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    vh.AddVert(center + direction * cornerRadius, near, Vector2.zero);
                    vh.AddVert(center + direction * (cornerRadius + 6f), far, Vector2.zero);
                }
            }
            var count = 4 * (steps + 1);
            for (var index = 0; index < count; index++)
            {
                var next = (index + 1) % count;
                vh.AddTriangle(index * 2, next * 2, index * 2 + 1);
                vh.AddTriangle(next * 2, next * 2 + 1, index * 2 + 1);
            }
        }
        private void Ray(VertexHelper vh, float angle, float inner, float outer, float spread)
        {
            var start = vh.currentVertCount;
            var center = rectTransform.rect.center;
            var transparent = color; transparent.a = 0f;
            vh.AddVert(Point(angle - spread) * inner + center, color, Vector2.zero);
            vh.AddVert(Point(angle + spread) * inner + center, color, Vector2.zero);
            vh.AddVert(Point(angle + spread * 0.3f) * outer + center, transparent, Vector2.zero);
            vh.AddVert(Point(angle - spread * 0.3f) * outer + center, transparent, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private void Ring(VertexHelper vh, float radius, float width, int sides)
        {
            for (var i = 0; i < sides; i++)
                Line(vh, Point(i * Mathf.PI * 2f / sides) * radius, Point((i + 1) * Mathf.PI * 2f / sides) * radius, width);
        }
        private static Vector2 Point(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        private void Line(VertexHelper vh, Vector2 a, Vector2 b, float width)
        {
            var delta = (b - a).normalized;
            var normal = new Vector2(-delta.y, delta.x) * width * 0.5f;
            var start = vh.currentVertCount;
            var center = rectTransform.rect.center;
            vh.AddVert(a - normal + center, color, Vector2.zero);
            vh.AddVert(a + normal + center, color, Vector2.zero);
            vh.AddVert(b + normal + center, color, Vector2.zero);
            vh.AddVert(b - normal + center, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
