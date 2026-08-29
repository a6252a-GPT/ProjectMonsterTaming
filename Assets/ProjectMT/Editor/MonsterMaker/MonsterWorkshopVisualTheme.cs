using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    internal static class MonsterWorkshopVisualTheme // 두 조립소가 공유하는 절제된 시각 언어
    {
        public static readonly Color PrimaryColor = new Color(0.28f, 0.82f, 0.74f, 1f);
        public static readonly Color PreviewColor = new Color(0.38f, 0.62f, 0.94f, 1f);
        public static readonly Color FeelColor = new Color(0.95f, 0.72f, 0.3f, 1f);
        public static readonly Color DangerColor = new Color(0.88f, 0.42f, 0.42f, 1f);
        private static GUIStyle headerTitleStyle;
        private static GUIStyle headerSubtitleStyle;
        private static GUIStyle headerBadgeStyle;
        private static GUIStyle presetButtonStyle;
        private static GUIStyle wrappedTextAreaStyle;

        public static void DrawHeader(string title, string subtitle)
        {
            EnsureStyles();
            var rect = GUILayoutUtility.GetRect(1f, 58f, GUILayout.ExpandWidth(true), GUILayout.Height(58f));
            EditorGUI.DrawRect(rect, new Color(0.075f, 0.09f, 0.12f, 1f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), new Color(0.15f, 0.78f, 0.72f, 1f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0.23f, 0.28f, 0.35f, 1f));

            GUI.Label(new Rect(rect.x + 16f, rect.y + 7f, rect.width - 260f, 23f), title, headerTitleStyle);
            GUI.Label(new Rect(rect.x + 17f, rect.y + 31f, rect.width - 250f, 18f), subtitle, headerSubtitleStyle);
            GUI.Label(
                new Rect(rect.xMax - 205f, rect.y + 17f, 188f, 22f),
                "MONSTER MAKER · WORKSHOP",
                headerBadgeStyle);
            GUILayout.Space(8f);
        }

        public static bool DrawPresetButton(GUIContent content, bool selected, float height = 26f)
        {
            EnsureStyles();
            var rect = GUILayoutUtility.GetRect(
                content,
                presetButtonStyle,
                GUILayout.Height(height),
                GUILayout.ExpandWidth(true));
            var hovered = rect.Contains(Event.current.mousePosition);
            var background = selected
                ? new Color(0.12f, 0.25f, 0.29f, 1f)
                : hovered
                    ? new Color(0.16f, 0.18f, 0.22f, 1f)
                    : new Color(0.105f, 0.115f, 0.14f, 1f);
            EditorGUI.DrawRect(rect, background);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0.2f, 0.23f, 0.28f, 1f));
            if (selected)
            {
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), new Color(0.2f, 0.88f, 0.78f, 1f));
            }
            return GUI.Button(rect, content, presetButtonStyle);
        }

        public static bool DrawTintedButton(
            GUIContent content,
            Color color,
            float height,
            float width = 0f)
        {
            var previous = GUI.backgroundColor;
            GUI.backgroundColor = Color.Lerp(Color.white, color, 0.58f);
            try
            {
                return width > 0f
                    ? GUILayout.Button(content, GUILayout.Width(width), GUILayout.Height(height))
                    : GUILayout.Button(content, GUILayout.Height(height));
            }
            finally
            {
                GUI.backgroundColor = previous;
            }
        }

        public static Vector2 BeginVerticalScrollView(Vector2 scrollPosition)
        {
            scrollPosition.x = 0f;
            var result = EditorGUILayout.BeginScrollView(
                scrollPosition,
                false,
                false,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUI.skin.scrollView);
            result.x = 0f;
            return result;
        }

        public static string DrawWrappedTextArea(string value, float minHeight, float width)
        {
            EnsureStyles();
            return EditorGUILayout.TextArea(
                value ?? string.Empty,
                wrappedTextAreaStyle,
                GUILayout.Width(width),
                GUILayout.MinHeight(minHeight));
        }

        private static void EnsureStyles()
        {
            headerTitleStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.94f, 0.97f, 1f, 1f) }
            };
            headerSubtitleStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.58f, 0.64f, 0.71f, 1f) }
            };
            headerBadgeStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.42f, 0.78f, 0.75f, 1f) }
            };
            presetButtonStyle ??= new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 6, 1, 1),
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.86f, 0.89f, 0.93f, 1f) },
                hover = { textColor = Color.white },
                active = { textColor = new Color(0.75f, 1f, 0.94f, 1f) },
                focused = { textColor = Color.white }
            };
            wrappedTextAreaStyle ??= new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                clipping = TextClipping.Clip
            };
        }
    }
}
