using UnityEngine;
using UnityEngine.UI;
using ProjectMT.Contents.Framework;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal sealed class DemoLifeHud
    {
        private const int HeartCount = 5;
        private const float HeartSize = 44f;
        private const float HeartSpacing = 6f;

        private static readonly Color FilledColor = new Color(0.94f, 0.22f, 0.34f, 1f);
        private static readonly Color EmptyColor = new Color(0.16f, 0.1f, 0.12f, 0.72f);
        private static readonly Color OutlineColor = new Color(0.05f, 0.02f, 0.03f, 0.9f);

        private Image[] hearts = System.Array.Empty<Image>();
        private GameObject root;
        private GrowthDungeonHudView authoredHud;

        public void Ensure(Transform hudRoot)
        {
            if (hudRoot == null)
            {
                return;
            }

            if (root != null)
            {
                return;
            }

            authoredHud = hudRoot.GetComponent<GrowthDungeonHudView>();
            if (authoredHud != null)
            {
                hearts = authoredHud.Hearts;
                return;
            }

            Transform existing = hudRoot.Find("LifeHud");
            if (existing != null)
            {
                Object.Destroy(existing.gameObject);
            }

            PlaceStatusColumn(hudRoot);

            root = new GameObject("LifeHud", typeof(RectTransform), typeof(CanvasGroup));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(hudRoot, false);
            rootRect.SetAsFirstSibling();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(28f, -22f);
            float width = HeartCount * HeartSize + (HeartCount - 1) * HeartSpacing;
            rootRect.sizeDelta = new Vector2(width, HeartSize);

            HorizontalLayoutGroup layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = HeartSpacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Sprite sprite = DemoHudArt.Heart;
            hearts = new Image[HeartCount];
            for (int i = 0; i < HeartCount; i++)
            {
                GameObject heartObject = new GameObject($"Heart_{i}", typeof(RectTransform), typeof(LayoutElement));
                RectTransform rect = heartObject.GetComponent<RectTransform>();
                rect.SetParent(rootRect, false);

                LayoutElement element = heartObject.GetComponent<LayoutElement>();
                element.minWidth = HeartSize;
                element.minHeight = HeartSize;
                element.preferredWidth = HeartSize;
                element.preferredHeight = HeartSize;

                CreateHeartLayer(rect, "Outline", sprite, OutlineColor, -3f);
                Image image = CreateHeartLayer(rect, "Fill", sprite, FilledColor, 0f);
                hearts[i] = image;
            }
        }

        public void SetLives(int current, int max)
        {
            if (authoredHud != null)
            {
                authoredHud.SetHearts(current, max);
                return;
            }
            int filled = Mathf.Clamp(current, 0, hearts.Length);
            int visible = Mathf.Clamp(max, 0, hearts.Length);
            for (int i = 0; i < hearts.Length; i++)
            {
                if (hearts[i] == null)
                {
                    continue;
                }

                hearts[i].transform.parent.gameObject.SetActive(i < visible);
                hearts[i].color = i < filled ? FilledColor : EmptyColor;
            }
        }

        public void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        public void Show()
        {
            if (root != null)
            {
                root.SetActive(true);
            }
        }

        private static Image CreateHeartLayer(
            RectTransform parent,
            string objectName,
            Sprite sprite,
            Color color,
            float outset)
        {
            GameObject layer = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = layer.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(outset, outset);
            rect.offsetMax = new Vector2(-outset, -outset);
            Image image = layer.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = color;
            return image;
        }

        private static void PlaceStatusColumn(Transform hudRoot)
        {
            const float left = 28f;
            const float killTop = -(22f + HeartSize + 12f);
            PlaceHudText(hudRoot, "KillText", left, killTop);
            PlaceHudText(hudRoot, "StatusText", left, killTop - 38f);
        }

        private static void PlaceHudText(Transform hudRoot, string childName, float x, float y)
        {
            if (hudRoot.Find(childName) is RectTransform rect)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(x, y);
                rect.sizeDelta = new Vector2(280f, 36f);
            }
        }
    }
}
