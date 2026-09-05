using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    public sealed class MainBattlePlacementDimGraphic : MaskableGraphic // 화면 전체에서 배치영역만 뚫린 딤
    {
        private readonly Vector2[] hole = new Vector2[4];
        private bool hasHole;

        public void SetHole(IReadOnlyList<Vector2> points)
        {
            hasHole = points != null && points.Count == hole.Length;
            if (hasHole)
            {
                var rect = rectTransform.rect;
                var outer = new[]
                {
                    new Vector2(rect.xMin, rect.yMin),
                    new Vector2(rect.xMax, rect.yMin),
                    new Vector2(rect.xMax, rect.yMax),
                    new Vector2(rect.xMin, rect.yMax)
                };
                var used = new bool[points.Count];
                for (var outerIndex = 0; outerIndex < outer.Length; outerIndex++)
                {
                    var nearest = -1;
                    var nearestDistance = float.PositiveInfinity;
                    for (var pointIndex = 0; pointIndex < points.Count; pointIndex++)
                    {
                        if (used[pointIndex])
                        {
                            continue;
                        }

                        var distance = (points[pointIndex] - outer[outerIndex]).sqrMagnitude;
                        if (distance < nearestDistance)
                        {
                            nearest = pointIndex;
                            nearestDistance = distance;
                        }
                    }

                    hole[outerIndex] = points[nearest];
                    used[nearest] = true;
                }
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var rect = rectTransform.rect;
            var outer = new[]
            {
                new Vector2(rect.xMin, rect.yMin),
                new Vector2(rect.xMax, rect.yMin),
                new Vector2(rect.xMax, rect.yMax),
                new Vector2(rect.xMin, rect.yMax)
            };

            if (!hasHole)
            {
                AddQuad(vertexHelper, outer[0], outer[1], outer[2], outer[3], color);
                return;
            }

            for (var index = 0; index < outer.Length; index++)
            {
                var next = (index + 1) % outer.Length;
                AddQuad(vertexHelper, outer[index], outer[next], hole[next], hole[index], color);
            }
        }

        private static void AddQuad(
            VertexHelper helper,
            Vector2 first,
            Vector2 second,
            Vector2 third,
            Vector2 fourth,
            Color32 color)
        {
            var start = helper.currentVertCount;
            helper.AddVert(first, color, Vector2.zero);
            helper.AddVert(second, color, Vector2.right);
            helper.AddVert(third, color, Vector2.one);
            helper.AddVert(fourth, color, Vector2.up);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
        }
    }
}
