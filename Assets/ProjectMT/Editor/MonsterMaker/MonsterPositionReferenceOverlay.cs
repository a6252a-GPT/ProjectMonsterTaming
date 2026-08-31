using ProjectMT.Shared.Combat;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    // V1/V2 Maker와 좌표 조정 창이 함께 사용하는 프리뷰 기준점 오버레이.
    internal static class MonsterPositionReferenceOverlay
    {
        public static readonly Color ModelColor = new Color(0.72f, 0.48f, 1f, 1f);
        public static readonly Color AttackColor = new Color(0.12f, 0.95f, 0.9f, 1f);
        public static readonly Color HitColor = new Color(1f, 0.42f, 0.3f, 1f);
        public static readonly Color EditTargetColor = new Color(1f, 0.8f, 0.18f, 1f);

        public static Rect DrawVisibilityToolbar(
            Rect previewRect,
            float leftReservedWidth,
            ref bool showModel,
            ref bool showAttack,
            ref bool showHit)
        {
            var toolbarRect = CalculateVisibilityToolbarRect(previewRect, leftReservedWidth);
            EditorGUI.DrawRect(toolbarRect, new Color(0.025f, 0.035f, 0.05f, 0.86f));

            var allButtonWidth = Mathf.Max(1f, toolbarRect.width * 0.15f);
            var x = toolbarRect.x + 2f;
            if (GUI.Button(new Rect(x, toolbarRect.y + 2f, allButtonWidth, 20f), "모두 켜기", EditorStyles.miniButtonLeft))
            {
                showModel = true;
                showAttack = true;
                showHit = true;
            }

            x += allButtonWidth;
            if (GUI.Button(new Rect(x, toolbarRect.y + 2f, allButtonWidth, 20f), "모두 끄기", EditorStyles.miniButtonMid))
            {
                showModel = false;
                showAttack = false;
                showHit = false;
            }

            x += allButtonWidth;
            var toggleWidth = Mathf.Max(1f, (toolbarRect.xMax - 2f - x) / 3f);
            showModel = DrawReferenceToggle(
                new Rect(x, toolbarRect.y + 2f, toggleWidth, 20f),
                showModel,
                "모델 기준",
                ModelColor,
                EditorStyles.miniButtonMid);
            x += toggleWidth;
            showAttack = DrawReferenceToggle(
                new Rect(x, toolbarRect.y + 2f, toggleWidth, 20f),
                showAttack,
                "공격 기준",
                AttackColor,
                EditorStyles.miniButtonMid);
            x += toggleWidth;
            showHit = DrawReferenceToggle(
                new Rect(x, toolbarRect.y + 2f, Mathf.Max(1f, toolbarRect.xMax - 2f - x), 20f),
                showHit,
                "피격 기준",
                HitColor,
                EditorStyles.miniButtonRight);
            return toolbarRect;
        }

        public static Rect CalculateVisibilityToolbarRect(Rect previewRect, float leftReservedWidth)
        {
            const float margin = 10f;
            const float height = 24f;
            const float preferredWidth = 410f;
            const float gap = 6f;
            var availableWidth = Mathf.Max(1f, previewRect.width - margin * 2f);
            var width = Mathf.Min(preferredWidth, availableWidth);
            var topRightSpace = previewRect.width - margin * 2f - Mathf.Max(0f, leftReservedWidth) - gap;
            var y = previewRect.y + margin;
            if (topRightSpace < Mathf.Min(330f, width))
                y += 55f;

            return new Rect(previewRect.xMax - margin - width, y, width, height);
        }

        public static bool TryGetGuiPoint(
            Camera camera,
            Rect previewRect,
            Vector3 worldPosition,
            out Vector2 guiPoint)
        {
            return MonsterPreviewPositionHandleUtility.TryWorldToGuiPoint(
                       camera,
                       previewRect,
                       worldPosition,
                       out guiPoint) &&
                   previewRect.Contains(guiPoint);
        }

        public static void DrawPoint(Vector2 guiPoint, Color color, bool selected = false)
        {
            if (Event.current.type != EventType.Repaint) return;

            var previousColor = Handles.color;
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawSolidDisc(guiPoint, Vector3.forward, selected ? 6.5f : 5f);
            Handles.color = selected ? Color.white : Color.black;
            Handles.DrawWireDisc(guiPoint, Vector3.forward, selected ? 8f : 6f);
            Handles.EndGUI();
            Handles.color = previousColor;
        }

        private static bool DrawReferenceToggle(
            Rect rect,
            bool value,
            string label,
            Color color,
            GUIStyle style)
        {
            var result = GUI.Toggle(rect, value, "     " + label, style);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(
                    new Rect(rect.x + 8f, rect.center.y - 3f, 6f, 6f),
                    result ? color : color * new Color(0.45f, 0.45f, 0.45f, 1f));
            }

            return result;
        }
    }
}
