using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleOverheadHealthBar : MonoBehaviour // 육각 전투 대상 공통 피격 HP바
    {
        public const float DamageVisibleSeconds = 1.35f;
        public static readonly Color FriendlyColor = new Color(0.12f, 0.82f, 0.24f, 1f);
        public static readonly Color HostileColor = new Color(0.92f, 0.1f, 0.08f, 1f);

        private const float CanvasScale = 0.025f;
        private const float BarWidth = 42f;
        private const float BarHeight = 6f;
        private const float FillWidth = 40f;
        private const float FillHeight = 4f;
        private const float TopPadding = 0.2f;

        private static Sprite roundedSprite;
        private GameObject visualRoot;
        private RectTransform visualRect;
        private RectTransform fillRect;
        private Image fillImage;
        private Camera targetCamera;
        private float hideAt;
        private float heightOffset = 1f;
        private float fillRatio;

        public bool IsVisible => visualRoot != null && visualRoot.activeSelf;
        public float FillRatio => fillRatio;
        public Color FillColor => fillImage == null ? Color.clear : fillImage.color;

        public static HexCastleOverheadHealthBar ShowDamage(
            Transform owner,
            HealthComponent healthSource)
        {
            if (owner == null || healthSource == null)
            {
                return null;
            }

            if (!owner.TryGetComponent<HexCastleOverheadHealthBar>(out var healthBar))
            {
                healthBar = owner.gameObject.AddComponent<HexCastleOverheadHealthBar>();
            }

            healthBar.Show(
                healthSource.CurrentHealth,
                healthSource.MaxHealth,
                healthSource.IsAlive,
                HostileColor);
            return healthBar;
        }

        public static HexCastleOverheadHealthBar ShowDamage(
            Transform owner,
            float currentHealth,
            float maximumHealth,
            bool friendly)
        {
            if (owner == null || maximumHealth <= 0f)
            {
                return null;
            }

            if (!owner.TryGetComponent<HexCastleOverheadHealthBar>(out var healthBar))
            {
                healthBar = owner.gameObject.AddComponent<HexCastleOverheadHealthBar>();
            }

            healthBar.Show(
                currentHealth,
                maximumHealth,
                currentHealth > 0f,
                friendly ? FriendlyColor : HostileColor);
            return healthBar;
        }

        public static Vector3 ResolveWorldAnchor(Transform owner)
        {
            if (owner == null)
            {
                return Vector3.zero;
            }

            var renderers = owner.GetComponentsInChildren<Renderer>(true);
            var highestPoint = owner.position.y + 0.8f;
            for (var index = 0; index < renderers.Length; index++)
            {
                var targetRenderer = renderers[index];
                if (targetRenderer == null || !targetRenderer.enabled ||
                    targetRenderer is ParticleSystemRenderer ||
                    targetRenderer is TrailRenderer ||
                    targetRenderer is LineRenderer)
                {
                    continue;
                }

                highestPoint = Mathf.Max(highestPoint, targetRenderer.bounds.max.y);
            }

            var offset = Mathf.Clamp(highestPoint - owner.position.y + TopPadding, 0.8f, 4.5f);
            return owner.position + Vector3.up * offset;
        }

        public void HideImmediately()
        {
            hideAt = 0f;
            if (visualRoot != null)
            {
                visualRoot.SetActive(false);
            }

            enabled = false;
        }

        private void Update()
        {
            if (IsVisible && Time.time >= hideAt)
            {
                HideImmediately();
            }
        }

        private void LateUpdate()
        {
            if (!IsVisible || visualRect == null)
            {
                return;
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            visualRect.position = transform.position + Vector3.up * heightOffset;
            if (targetCamera != null)
            {
                visualRect.rotation = targetCamera.transform.rotation;
            }

            ApplyFixedWorldScale();
        }

        private void OnDisable()
        {
            if (visualRoot != null)
            {
                visualRoot.SetActive(false);
            }
        }

        private void Show(
            float currentHealth,
            float maximumHealth,
            bool alive,
            Color color)
        {
            EnsureVisual();
            fillImage.color = color;
            fillRatio = maximumHealth <= 0f
                ? 0f
                : Mathf.Clamp01(currentHealth / maximumHealth);
            fillRect.sizeDelta = new Vector2(FillWidth * fillRatio, FillHeight);
            heightOffset = ResolveHeightOffset();
            if (!alive || fillRatio <= 0f)
            {
                HideImmediately();
                return;
            }

            hideAt = Time.time + DamageVisibleSeconds;
            enabled = true;
            visualRoot.SetActive(true);
            LateUpdate();
        }

        private void EnsureVisual()
        {
            if (visualRoot != null)
            {
                return;
            }

            visualRoot = new GameObject(
                "HexCastleHealthBar",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            visualRoot.transform.SetParent(transform, false);
            visualRect = visualRoot.GetComponent<RectTransform>();
            visualRect.sizeDelta = new Vector2(BarWidth, BarHeight);
            visualRect.pivot = new Vector2(0.5f, 0.5f);

            var canvas = visualRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 200;

            var canvasScaler = visualRoot.GetComponent<CanvasScaler>();
            canvasScaler.dynamicPixelsPerUnit = 10f;
            canvasScaler.referencePixelsPerUnit = 100f;

            var background = CreateImage("Background", visualRect, new Color(0f, 0f, 0f, 0.88f), out _);
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = Vector2.zero;
            background.offsetMax = Vector2.zero;

            fillRect = CreateImage("Fill", visualRect, HostileColor, out fillImage);
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(0f, 0.5f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = new Vector2(1f, 0f);
            fillRect.sizeDelta = new Vector2(FillWidth, FillHeight);
            visualRoot.SetActive(false);
        }

        private static RectTransform CreateImage(
            string objectName,
            Transform parent,
            Color color,
            out Image image)
        {
            var imageObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var rect = imageObject.GetComponent<RectTransform>();
            image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.sprite = GetRoundedSprite();
            image.type = Image.Type.Sliced;
            return rect;
        }

        private float ResolveHeightOffset()
        {
            return ResolveWorldAnchor(transform).y - transform.position.y;
        }

        private void ApplyFixedWorldScale()
        {
            var parentScale = transform.lossyScale;
            visualRect.localScale = new Vector3(
                CanvasScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                CanvasScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                CanvasScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
        }

        private static Sprite GetRoundedSprite()
        {
            if (roundedSprite != null)
            {
                return roundedSprite;
            }

            const int size = 16;
            const float radius = 4f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HexCastleHealthBar_Rounded",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var pixelX = x + 0.5f;
                    var pixelY = y + 0.5f;
                    var centerX = Mathf.Clamp(pixelX, radius, size - radius);
                    var centerY = Mathf.Clamp(pixelY, radius, size - radius);
                    var deltaX = pixelX - centerX;
                    var deltaY = pixelY - centerY;
                    texture.SetPixel(x, y, deltaX * deltaX + deltaY * deltaY <= radius * radius
                        ? Color.white
                        : Color.clear);
                }
            }

            texture.Apply(false, true);
            roundedSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            roundedSprite.name = "HexCastleHealthBar_Rounded";
            roundedSprite.hideFlags = HideFlags.HideAndDontSave;
            return roundedSprite;
        }
    }
}
