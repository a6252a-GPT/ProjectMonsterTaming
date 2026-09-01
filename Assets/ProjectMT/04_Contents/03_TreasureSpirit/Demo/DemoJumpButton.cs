using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ProjectMT.Contents.TreasureSpirit;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    [DisallowMultipleComponent]
    public sealed class DemoJumpButton : MonoBehaviour
    {
        private const float ButtonSize = 64f;
        private static readonly Color ReadyFill = new Color(0.28f, 0.86f, 0.94f, 0.95f);
        private static readonly Color DarkRing = new Color(0.08f, 0.1f, 0.12f, 0.88f);

        private static Sprite circleSprite;

        private PlayerCharacterController player;
        private Button button;
        private Image fill;
        private CanvasGroup canvasGroup;
        private CanvasGroup tabCanvasGroup;
        private DungeonAutomapOverlay automapOverlay;
        private bool lastVisible;
        private bool lastCanJump;
        private float lastFill = -1f;

        public static DemoJumpButton Ensure(Transform hudRoot, PlayerCharacterController playerMove)
        {
            if (hudRoot == null)
            {
                return null;
            }

            Transform existing = hudRoot.Find("JumpButton");
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            Sprite circle = GetCircleSprite();
            GameObject root = new GameObject("JumpButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(CanvasGroup));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.SetParent(hudRoot, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-96f, 96f);
            rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);

            Image background = root.GetComponent<Image>();
            background.sprite = circle;
            background.color = DarkRing;
            background.preserveAspect = true;
            background.raycastTarget = true;

            GameObject fillObject = new GameObject("CooldownFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.SetParent(rect, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
            Image fillImage = fillObject.GetComponent<Image>();
            fillImage.sprite = circle;
            fillImage.preserveAspect = true;
            fillImage.color = ReadyFill;
            fillImage.raycastTarget = false;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Radial360;
            fillImage.fillOrigin = 2;
            fillImage.fillClockwise = true;
            fillImage.fillAmount = 1f;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = "점프";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 16f;
            label.color = Color.white;
            label.raycastTarget = false;
            TMP_Text source = hudRoot.GetComponentInChildren<TMP_Text>(true);
            if (source != null && source.font != null)
            {
                label.font = source.font;
            }

            DemoJumpButton view = root.AddComponent<DemoJumpButton>();
            view.player = playerMove;
            view.button = root.GetComponent<Button>();
            view.fill = fillImage;
            view.canvasGroup = root.GetComponent<CanvasGroup>();
            view.tabCanvasGroup = CreateTabButton(hudRoot, source, view);
            ColorBlock colors = view.button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            view.button.colors = colors;
            view.button.transition = Selectable.Transition.None;
            view.button.onClick.AddListener(view.HandleClicked);
            view.Refresh();
            return view;
        }

        public void Hide()
        {
            lastFill = -1f;
            lastVisible = true;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            SetTabVisible(false);
        }

        public void Show()
        {
            lastFill = -1f;
            lastVisible = false;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
            }

            Refresh();
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }
        }

        private void Update()
        {
            Refresh();
        }

        private void HandleClicked()
        {
            player?.TryJump();
            Refresh();
        }

        private void Refresh()
        {
            bool visible = player != null && player.InputEnabled;
            bool canJump = visible && player != null && player.CanJump;
            float fillAmount = player != null ? player.JumpReadyFill : 0f;
            if (visible == lastVisible && canJump == lastCanJump && Mathf.Abs(fillAmount - lastFill) < 0.001f)
            {
                return;
            }

            lastVisible = visible;
            lastCanJump = canJump;
            lastFill = fillAmount;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }

            SetTabVisible(visible);

            if (button != null)
            {
                button.interactable = canJump;
            }

            if (fill != null)
            {
                fill.fillAmount = fillAmount;
                fill.color = ReadyFill;
            }
        }

        private void SetTabVisible(bool visible)
        {
            if (tabCanvasGroup == null)
            {
                return;
            }

            tabCanvasGroup.alpha = visible ? 1f : 0f;
            tabCanvasGroup.interactable = visible;
            tabCanvasGroup.blocksRaycasts = visible;
        }

        private static CanvasGroup CreateTabButton(Transform hudRoot, TMP_Text source, DemoJumpButton view)
        {
            Transform existing = hudRoot.Find("TabMinimapButton");
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            GameObject root = new GameObject("TabMinimapButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(CanvasGroup));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.SetParent(hudRoot, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-96f, 176f);
            rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);

            Image background = root.GetComponent<Image>();
            background.sprite = null;
            background.color = DarkRing;
            background.raycastTarget = true;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = "미니맵";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 13f;
            label.color = Color.white;
            label.raycastTarget = false;
            if (source != null && source.font != null)
            {
                label.font = source.font;
            }

            Button tabButton = root.GetComponent<Button>();
            tabButton.transition = Selectable.Transition.None;
            tabButton.onClick.AddListener(view.ToggleAutomap);
            return root.GetComponent<CanvasGroup>();
        }

        private void ToggleAutomap()
        {
            if (automapOverlay == null)
            {
                automapOverlay = Object.FindFirstObjectByType<DungeonAutomapOverlay>();
            }

            automapOverlay?.Toggle();
        }

        private static Sprite GetCircleSprite()
        {
            if (circleSprite != null)
            {
                return circleSprite;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            float radius = (size - 1) * 0.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - radius;
                    float dy = y - radius;
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            circleSprite.name = "JumpCircle";
            return circleSprite;
        }
    }
}
