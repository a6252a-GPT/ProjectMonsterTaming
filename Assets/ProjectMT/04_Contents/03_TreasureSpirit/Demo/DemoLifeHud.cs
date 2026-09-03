using UnityEngine;
using UnityEngine.UI;
using ProjectMT.Contents.Framework;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal sealed class DemoLifeHud
    {
        private const int HeartCount = 5;
        private const float HeartSize = 52f;
        private const float HeartSpacing = 8f;

        private static readonly Color FilledColor = new Color(0.92f, 0.18f, 0.3f, 1f);
        private static readonly Color EmptyColor = new Color(0.22f, 0.18f, 0.2f, 0.7f);

        private Image[] hearts = System.Array.Empty<Image>();
        private GameObject root;
        private static Sprite heartSprite;
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

            ShiftOverlappingHud(hudRoot);

            root = new GameObject("LifeHud", typeof(RectTransform));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(hudRoot, false);
            rootRect.SetAsFirstSibling();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(24f, -18f);
            float width = HeartCount * HeartSize + (HeartCount - 1) * HeartSpacing;
            rootRect.sizeDelta = new Vector2(width, HeartSize);

            HorizontalLayoutGroup layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = HeartSpacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Sprite sprite = GetHeartSprite();
            hearts = new Image[HeartCount];
            for (int i = 0; i < HeartCount; i++)
            {
                GameObject heartObject = new GameObject($"Heart_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
                RectTransform rect = heartObject.GetComponent<RectTransform>();
                rect.SetParent(rootRect, false);

                LayoutElement element = heartObject.GetComponent<LayoutElement>();
                element.minWidth = HeartSize;
                element.minHeight = HeartSize;
                element.preferredWidth = HeartSize;
                element.preferredHeight = HeartSize;

                Image image = heartObject.GetComponent<Image>();
                image.sprite = sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.color = FilledColor;
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

                hearts[i].gameObject.SetActive(i < visible);
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

        private static void ShiftOverlappingHud(Transform hudRoot)
        {
            Transform killText = hudRoot.Find("KillText");
            if (killText is RectTransform killRect)
            {
                killRect.anchoredPosition = new Vector2(140f, -156f);
            }

            Transform statusText = hudRoot.Find("StatusText");
            if (statusText is RectTransform statusRect)
            {
                statusRect.anchoredPosition = new Vector2(140f, -248f);
            }
        }

        private static Sprite GetHeartSprite()
        {
            if (heartSprite != null)
            {
                return heartSprite;
            }

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (size - 1f) - 0.5f) * 2.55f;
                    float ny = (y / (size - 1f) - 0.42f) * 2.7f;
                    float a = nx * nx + ny * ny - 1f;
                    float value = a * a * a - nx * nx * ny * ny * ny;
                    float alpha = Mathf.Clamp01(0.5f - value * 18f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, true);
            heartSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                64f);
            heartSprite.name = "DemoLifeHeart";
            return heartSprite;
        }
    }
}
