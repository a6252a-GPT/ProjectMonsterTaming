using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal static class DemoHudArt
    {
        private static Sprite circle;
        private static Sprite heart;
        private static Sprite ice;

        public static Sprite Circle => circle != null ? circle : circle = CreateCircle();
        public static Sprite Heart => heart != null ? heart : heart = CreateHeart();
        public static Sprite Ice => ice != null ? ice : ice = CreateIce();

        private static Sprite CreateCircle()
        {
            const int size = 96;
            float radius = (size - 1) * 0.5f;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - radius;
                    float dy = y - radius;
                    float alpha = Mathf.Clamp01(radius - 0.6f - Mathf.Sqrt((dx * dx) + (dy * dy)));
                    pixels[(y * size) + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            return ToSprite(pixels, size, "HudCircle");
        }

        private static Sprite CreateHeart()
        {
            const int size = 80;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (size - 1f) - 0.5f) * 2.35f;
                    float ny = (y / (size - 1f) - 0.38f) * 2.45f;
                    float a = (nx * nx) + (ny * ny) - 1f;
                    float value = (a * a * a) - (nx * nx * ny * ny * ny);
                    float fill = Mathf.Clamp01(0.38f - (value * 18f));
                    pixels[(y * size) + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(fill * 255f));
                }
            }

            return ToSprite(pixels, size, "HudHeart");
        }

        private static Sprite CreateIce()
        {
            const int size = 96;
            float center = (size - 1) * 0.5f;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float radius = Mathf.Sqrt((dx * dx) + (dy * dy));
                    float angle = Mathf.Atan2(dy, dx);
                    float arm = Mathf.Lerp(0.12f, 0.74f, Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 3f)), 2.2f));
                    float flake = Mathf.Clamp01((arm - radius) * 16f);
                    float core = Mathf.Clamp01((0.18f - radius) * 18f);
                    float alpha = Mathf.Max(flake, core) * Mathf.Clamp01((0.88f - radius) * 16f);
                    pixels[(y * size) + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }

            return ToSprite(pixels, size, "HudIce");
        }

        private static Sprite ToSprite(Color32[] pixels, int size, string spriteName)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = spriteName
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = spriteName;
            return sprite;
        }
    }
}
