using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ProjectMT.Contents.TreasureSpirit;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    [DisallowMultipleComponent]
    public sealed class DemoJumpButton : MonoBehaviour
    {
        private const string JumpObjectName = "JumpButton";
        private const string AttackObjectName = "AttackButton";
        private const string MapObjectName = "TabMinimapButton";
        private const string MapButtonLabel = "MAP";
        private const float ScreenPad = 48f;
        private const float IceSize = 112f;
        private const float JumpSize = 88f;
        private const float MinimapSize = JumpSize;
        private const float ButtonGap = 18f;
        private const float LabelFontSize = JumpSize * 0.24f;

        private static readonly Color PlateColor = new Color(0.07f, 0.08f, 0.12f, 0.94f);
        private static readonly Color MapPlateColor = Color.black;
        private static readonly Color IceRim = new Color(0.42f, 0.84f, 1f, 1f);
        private static readonly Color JumpRim = new Color(0.38f, 0.86f, 0.68f, 1f);
        private static readonly Color MapRim = new Color(1f, 0.84f, 0.1f, 1f);
        private static readonly Color MapRimOpen = new Color(1f, 0.95f, 0.35f, 1f);
        private static readonly Color CoolOverlay = new Color(0.02f, 0.03f, 0.06f, 0.7f);
        private static readonly Color LabelReady = Color.white;
        private static readonly Color LabelDim = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color MapLabelReady = new Color(1f, 0.95f, 0.2f, 1f);
        private static readonly Color MapLabelDim = new Color(1f, 0.95f, 0.2f, 0.55f);
        private static readonly Vector2 IcePosition = new Vector2(
            -(ScreenPad + (IceSize * 0.5f)),
            ScreenPad + (IceSize * 0.5f));
        private static readonly Vector2 JumpPosition = new Vector2(
            IcePosition.x - (IceSize * 0.5f) - ButtonGap - (JumpSize * 0.5f),
            ScreenPad + (JumpSize * 0.5f));
        private static readonly Vector2 MinimapPosition = new Vector2(
            JumpPosition.x,
            JumpPosition.y + (JumpSize * 0.5f) + ButtonGap + (MinimapSize * 0.5f));

        private static TMP_FontAsset cachedHudFont;

        private PlayerCharacterController player;
        private Button jumpButton;
        private Button attackButton;
        private Button mapButton;
        private Image jumpFill;
        private Image attackFill;
        private Image attackIcon;
        private Image mapRim;
        private TMP_Text jumpLabel;
        private TMP_Text mapLabel;
        private CanvasGroup jumpCanvasGroup;
        private CanvasGroup attackCanvasGroup;
        private CanvasGroup tabCanvasGroup;
        private DungeonAutomapOverlay automapOverlay;
        private Transform boundMapRoot;
        private bool lastVisible;
        private bool lastCanJump;
        private bool lastCanLantern;
        private bool lastMapOpen;
        private float lastJumpFill = -1f;
        private float lastLanternFill = -1f;

        public static DemoJumpButton Ensure(
            Transform hudRoot,
            PlayerCharacterController playerMove,
            Sprite iceIcon = null)
        {
            if (hudRoot == null)
            {
                return null;
            }

            DemoJumpButton existing = FindExisting(hudRoot);
            if (existing != null)
            {
                existing.Rebind(playerMove, iceIcon);
                return existing;
            }

            DestroyNamed(hudRoot, JumpObjectName);
            DestroyNamed(hudRoot, AttackObjectName);
            DestroyNamed(hudRoot, MapObjectName);

            TMP_FontAsset font = ResolveHudFont(hudRoot);
            ActionButton jump = CreateActionButton(
                hudRoot,
                JumpObjectName,
                JumpPosition,
                JumpSize,
                JumpRim,
                PlateColor,
                null,
                0f,
                font,
                "점프",
                LabelReady,
                true);
            DemoJumpButton view = jump.Root.AddComponent<DemoJumpButton>();
            view.player = playerMove;
            view.jumpButton = jump.Button;
            view.jumpFill = jump.Fill;
            view.jumpLabel = jump.Label;
            view.jumpCanvasGroup = jump.Group;
            view.jumpButton.onClick.AddListener(view.HandleJumpClicked);

            ActionButton attack = CreateActionButton(
                hudRoot,
                AttackObjectName,
                IcePosition,
                IceSize,
                IceRim,
                PlateColor,
                iceIcon != null ? iceIcon : DemoHudArt.Ice,
                8f,
                font,
                "얼음",
                Color.white,
                true);
            view.attackButton = attack.Button;
            view.attackFill = attack.Fill;
            view.attackIcon = attack.Icon;
            view.attackCanvasGroup = attack.Group;
            view.attackButton.onClick.AddListener(view.HandleAttackClicked);

            ActionButton map = CreateActionButton(
                hudRoot,
                MapObjectName,
                MinimapPosition,
                MinimapSize,
                MapRim,
                MapPlateColor,
                null,
                0f,
                font,
                MapButtonLabel,
                MapLabelReady,
                false);
            view.mapButton = map.Button;
            view.mapRim = map.Rim;
            view.mapLabel = map.Label;
            view.tabCanvasGroup = map.Group;
            view.mapButton.onClick.AddListener(view.ToggleAutomap);

            view.Refresh();
            return view;
        }

        public void BindAutomap(Transform mapRoot)
        {
            if (mapRoot == boundMapRoot && automapOverlay != null)
            {
                return;
            }

            boundMapRoot = mapRoot;
            automapOverlay = null;
            if (mapRoot == null)
            {
                return;
            }

            Transform fogRoot = mapRoot.Find(DungeonFogInitializer.FogRootName);
            Transform parent = fogRoot != null ? fogRoot : mapRoot;
            DungeonExplorationMap map = parent.GetComponent<DungeonExplorationMap>()
                ?? parent.GetComponentInChildren<DungeonExplorationMap>(true);
            Transform playerTransform = DemoDungeonController.Active != null
                ? DemoDungeonController.Active.PlayerTransform
                : null;
            automapOverlay = DungeonAutomapOverlay.Ensure(parent, map, playerTransform);
        }

        public void HideAutomap()
        {
            automapOverlay?.Hide();
        }

        public void Hide()
        {
            InvalidateRefresh();
            lastVisible = true;
            lastMapOpen = true;
            SetGroupVisible(jumpCanvasGroup, false);
            SetGroupVisible(attackCanvasGroup, false);
            SetGroupVisible(tabCanvasGroup, false);
            HideAutomap();
        }

        public void Show()
        {
            InvalidateRefresh();
            lastVisible = false;
            lastMapOpen = true;
            Refresh();
        }

        private void Rebind(PlayerCharacterController playerMove, Sprite iceIcon)
        {
            player = playerMove;
            if (attackIcon != null)
            {
                attackIcon.sprite = iceIcon != null ? iceIcon : DemoHudArt.Ice;
            }

            Show();
        }

        private void OnDestroy()
        {
            if (jumpButton != null)
            {
                jumpButton.onClick.RemoveListener(HandleJumpClicked);
            }

            if (attackButton != null)
            {
                attackButton.onClick.RemoveListener(HandleAttackClicked);
            }

            if (mapButton != null)
            {
                mapButton.onClick.RemoveListener(ToggleAutomap);
            }
        }

        private void Update()
        {
            Refresh();
        }

        private void HandleJumpClicked()
        {
            player?.TryJump();
            Refresh();
        }

        private void HandleAttackClicked()
        {
            player?.TryLanternStrike();
            Refresh();
        }

        private void ToggleAutomap()
        {
            BindAutomap(DemoDungeonController.Active != null
                ? DemoDungeonController.Active.ActiveMapRoot
                : null);
            automapOverlay?.Toggle();
            Refresh();
        }

        private void Refresh()
        {
            bool visible = player != null && player.InputEnabled;
            bool canJump = visible && player.CanJump;
            bool canLantern = visible && player.CanLanternStrike;
            bool mapOpen = automapOverlay != null && automapOverlay.IsVisible;
            float jumpFillAmount = player != null ? player.JumpReadyFill : 0f;
            float lanternFillAmount = player != null ? player.LanternReadyFill : 0f;
            if (visible == lastVisible &&
                canJump == lastCanJump &&
                canLantern == lastCanLantern &&
                mapOpen == lastMapOpen &&
                Mathf.Abs(jumpFillAmount - lastJumpFill) < 0.001f &&
                Mathf.Abs(lanternFillAmount - lastLanternFill) < 0.001f)
            {
                return;
            }

            lastVisible = visible;
            lastCanJump = canJump;
            lastCanLantern = canLantern;
            lastMapOpen = mapOpen;
            lastJumpFill = jumpFillAmount;
            lastLanternFill = lanternFillAmount;

            SetGroupVisible(jumpCanvasGroup, visible);
            SetGroupVisible(attackCanvasGroup, visible);
            SetGroupVisible(tabCanvasGroup, visible);

            if (jumpButton != null)
            {
                jumpButton.interactable = canJump;
            }

            if (attackButton != null)
            {
                attackButton.interactable = canLantern;
            }

            ApplyReady(jumpFill, null, jumpFillAmount, canJump, Color.white);
            ApplyLabel(jumpLabel, canJump);
            ApplyReady(attackFill, attackIcon, lanternFillAmount, canLantern, Color.white);

            if (mapRim != null)
            {
                mapRim.color = mapOpen ? MapRimOpen : MapRim;
            }

            if (mapLabel != null)
            {
                mapLabel.color = visible ? MapLabelReady : MapLabelDim;
            }
        }

        private void InvalidateRefresh()
        {
            lastJumpFill = -1f;
            lastLanternFill = -1f;
        }

        private static DemoJumpButton FindExisting(Transform hudRoot)
        {
            Transform jump = hudRoot.Find(JumpObjectName);
            DemoJumpButton view = jump != null ? jump.GetComponent<DemoJumpButton>() : null;
            if (view == null ||
                hudRoot.Find(AttackObjectName) == null ||
                hudRoot.Find(MapObjectName) == null)
            {
                return null;
            }

            return view;
        }

        private static void ApplyReady(Image fill, Image icon, float ready, bool interactable, Color readyColor)
        {
            if (fill != null)
            {
                fill.fillAmount = 1f - Mathf.Clamp01(ready);
                fill.color = CoolOverlay;
            }

            if (icon != null)
            {
                Color color = readyColor;
                if (!interactable)
                {
                    color.a *= 0.45f;
                }

                icon.color = color;
            }
        }

        private static void ApplyLabel(TMP_Text label, bool ready)
        {
            if (label != null)
            {
                label.color = ready ? LabelReady : LabelDim;
            }
        }

        private static void SetGroupVisible(CanvasGroup group, bool visible)
        {
            if (group == null)
            {
                return;
            }

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private static ActionButton CreateActionButton(
            Transform hudRoot,
            string objectName,
            Vector2 anchoredPosition,
            float size,
            Color rimColor,
            Color plateColor,
            Sprite iconSprite,
            float iconInset,
            TMP_FontAsset font,
            string fallbackLabel,
            Color labelColor,
            bool cooldown)
        {
            GameObject root = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(CanvasGroup),
                typeof(Shadow));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.SetParent(hudRoot, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(size, size);

            Image rim = root.GetComponent<Image>();
            rim.sprite = DemoHudArt.Circle;
            rim.color = rimColor;
            rim.preserveAspect = true;
            rim.raycastTarget = true;

            Shadow shadow = root.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(0f, -4f);

            Image plate = CreateChildImage(rect, "Plate", DemoHudArt.Circle, plateColor, 6f);
            plate.raycastTarget = false;

            Image icon = null;
            if (iconSprite != null)
            {
                Mask mask = plate.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = true;
                icon = CreateChildImage(plate.rectTransform, "Icon", iconSprite, Color.white, iconInset);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }

            Image fill = null;
            if (cooldown)
            {
                fill = CreateChildImage(rect, "CooldownFill", DemoHudArt.Circle, CoolOverlay, 6f);
                fill.raycastTarget = false;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Radial360;
                fill.fillOrigin = 2;
                fill.fillClockwise = true;
                fill.fillAmount = 0f;
            }

            TMP_Text label = iconSprite == null
                ? CreateLabel(rect, fallbackLabel, font, Mathf.Max(18f, LabelFontSize), labelColor)
                : null;

            Button button = root.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = rim;

            return new ActionButton
            {
                Root = root,
                Button = button,
                Fill = fill,
                Icon = icon,
                Rim = rim,
                Label = label,
                Group = root.GetComponent<CanvasGroup>()
            };
        }

        private static Image CreateChildImage(
            RectTransform parent,
            string objectName,
            Sprite sprite,
            Color color,
            float inset)
        {
            GameObject created = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = created.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            Image image = created.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            return image;
        }

        private static TMP_FontAsset ResolveHudFont(Transform hudRoot)
        {
            if (cachedHudFont != null)
            {
                return cachedHudFont;
            }

            TMP_Text[] texts = hudRoot.GetComponentsInChildren<TMP_Text>(true);
            TMP_FontAsset fallback = null;
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_FontAsset font = texts[i] != null ? texts[i].font : null;
                if (font == null)
                {
                    continue;
                }

                fallback ??= font;
                string name = font.name;
                if (name.IndexOf("Hakgyo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                if (name.IndexOf("Spoqa", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Noonnu", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    cachedHudFont = font;
                    return font;
                }
            }

            cachedHudFont = fallback;
            return fallback;
        }

        private static TMP_Text CreateLabel(
            RectTransform parent,
            string text,
            TMP_FontAsset font,
            float fontSize,
            Color color)
        {
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(parent, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 8f);
            labelRect.offsetMax = new Vector2(-8f, -8f);
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = false;
            label.fontSize = fontSize;
            label.color = color;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.extraPadding = true;
            label.outlineWidth = 0f;
            label.outlineColor = Color.clear;
            if (font != null)
            {
                label.font = font;
            }

            return label;
        }

        private static void DestroyNamed(Transform hudRoot, string childName)
        {
            Transform existing = hudRoot.Find(childName);
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }
        }

        private struct ActionButton
        {
            public GameObject Root;
            public Button Button;
            public Image Fill;
            public Image Icon;
            public Image Rim;
            public TMP_Text Label;
            public CanvasGroup Group;
        }
    }
}
