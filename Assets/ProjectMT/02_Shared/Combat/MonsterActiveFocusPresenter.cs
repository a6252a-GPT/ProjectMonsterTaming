using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Shared.Combat
{
    [DisallowMultipleComponent]
    public sealed class MonsterActiveFocusPresenter : MonoBehaviour // 액티브 발동 초상화·스킬명 강조
    {
        private CanvasGroup overlayGroup;
        private CanvasGroup bannerGroup;
        private RectTransform bannerRect;
        private Image portrait;
        private TMP_Text skillName;
        private TMP_Text ownerName;
        private float elapsed;
        private float duration;
        private bool showing;

        public bool IsShowing => showing;
        public string CurrentSkillName => skillName != null ? skillName.text : string.Empty;
        public string CurrentOwnerName => ownerName != null ? ownerName.text : string.Empty;
        public TMP_FontAsset OwnerFont => ownerName != null ? ownerName.font : null;
        public TMP_FontAsset SkillFont => skillName != null ? skillName.font : null;

        private void Awake()
        {
            EnsureView();
            HideImmediate();
        }

        private void Update()
        {
            if (!showing) return;
            elapsed += Time.unscaledDeltaTime;
            var enter = Mathf.Clamp01(elapsed / 0.14f);
            var exit = duration - elapsed < 0.18f
                ? Mathf.Clamp01((duration - elapsed) / 0.18f)
                : 1f;
            var visibility = enter * exit;
            overlayGroup.alpha = 0.22f * visibility;
            bannerGroup.alpha = visibility;
            bannerRect.anchoredPosition = Vector2.Lerp(
                new Vector2(-48f, 92f),
                new Vector2(0f, 92f),
                1f - Mathf.Pow(1f - enter, 3f));
            bannerRect.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, enter);
            if (elapsed >= duration) HideImmediate();
        }

        public void Show(UnitActor caster, MonsterAttackActiveSkill activeSkill, float visibleDuration)
        {
            EnsureView();
            duration = Mathf.Max(0.35f, visibleDuration);
            elapsed = 0f;
            showing = true;
            overlayGroup.gameObject.SetActive(true);
            bannerGroup.gameObject.SetActive(true);
            portrait.sprite = activeSkill?.Icon;
            portrait.enabled = portrait.sprite != null;
            skillName.text = activeSkill?.DisplayName ?? "ACTIVE SKILL";
            ownerName.text = string.IsNullOrWhiteSpace(caster?.DisplayName)
                ? string.Empty
                : caster.DisplayName;
        }

        public void HideImmediate()
        {
            showing = false;
            elapsed = 0f;
            if (overlayGroup != null)
            {
                overlayGroup.alpha = 0f;
                overlayGroup.gameObject.SetActive(false);
            }
            if (bannerGroup != null)
            {
                bannerGroup.alpha = 0f;
                bannerGroup.gameObject.SetActive(false);
            }
        }

        private void EnsureView()
        {
            if (bannerGroup != null) return;
            var canvasObject = new GameObject(
                "MonsterActiveFocusCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 460;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.GetComponent<GraphicRaycaster>().enabled = false;

            var overlay = CreateImage("SlowFocusOverlay", canvasObject.transform,
                new Color(0.015f, 0.025f, 0.055f, 1f));
            var overlayRect = overlay.rectTransform;
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            overlayGroup = overlay.gameObject.AddComponent<CanvasGroup>();
            overlay.raycastTarget = false;

            var banner = CreateImage("ActiveSkillBanner", canvasObject.transform,
                new Color(0.025f, 0.055f, 0.11f, 0.96f));
            bannerRect = banner.rectTransform;
            bannerRect.anchorMin = new Vector2(0.5f, 0.5f);
            bannerRect.anchorMax = new Vector2(0.5f, 0.5f);
            bannerRect.pivot = new Vector2(0.5f, 0.5f);
            bannerRect.sizeDelta = new Vector2(620f, 126f);
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            banner.raycastTarget = false;

            var accent = CreateImage("Accent", bannerRect, new Color(0.25f, 0.78f, 1f, 1f));
            accent.rectTransform.anchorMin = new Vector2(0f, 0f);
            accent.rectTransform.anchorMax = new Vector2(0f, 1f);
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            accent.rectTransform.anchoredPosition = Vector2.zero;
            accent.rectTransform.sizeDelta = new Vector2(7f, 0f);

            portrait = CreateImage("Portrait", bannerRect, Color.white);
            portrait.preserveAspect = true;
            portrait.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            portrait.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            portrait.rectTransform.pivot = new Vector2(0f, 0.5f);
            portrait.rectTransform.anchoredPosition = new Vector2(24f, 0f);
            portrait.rectTransform.sizeDelta = new Vector2(98f, 98f);
            portrait.raycastTarget = false;

            ownerName = CreateText("OwnerName", bannerRect, 20f, FontStyles.Bold,
                new Color(0.45f, 0.82f, 1f, 1f));
            SetTextRect(ownerName.rectTransform, new Vector2(142f, 18f), new Vector2(450f, 30f));
            skillName = CreateText("SkillName", bannerRect, 39f, FontStyles.Bold, Color.white);
            SetTextRect(skillName.rectTransform, new Vector2(142f, -18f), new Vector2(450f, 60f));
            var config = MonsterActiveFocusPresentationConfig.Current;
            ownerName.font = config?.OwnerFont ?? TMP_Settings.defaultFontAsset;
            skillName.font = config?.SkillFont ?? ownerName.font;
        }

        private static Image CreateImage(string objectName, Transform parent, Color color)
        {
            var gameObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(
            string objectName,
            Transform parent,
            float size,
            FontStyles style,
            Color color)
        {
            var gameObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableAutoSizing = true;
            text.fontSizeMin = 16f;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static void SetTextRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
