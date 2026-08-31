using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Shared.Combat
{
    [DisallowMultipleComponent]
    public sealed class MonsterActiveFocusPresenter : MonoBehaviour // 액티브 발동 집중 HUD
    {
        [Header("Formal View")]
        [SerializeField] private CanvasGroup overlayGroup;
        [SerializeField] private Image dimOverlay;
        [SerializeField] private CanvasGroup bannerGroup;
        [SerializeField] private RectTransform bannerRect;
        [SerializeField] private Image bannerBackground;
        [SerializeField] private Image accent;
        [SerializeField] private Image accentGlass;
        [SerializeField] private Image energyEdge;
        [SerializeField] private Image lightShard;
        [SerializeField] private RectTransform portraitStageRect;
        [SerializeField] private Image portrait;
        [SerializeField] private TMP_Text portraitFallback;
        [SerializeField] private Image skillStrip;
        [SerializeField] private TMP_Text rarityLabel;
        [SerializeField] private TMP_Text skillName;
        [SerializeField] private TMP_Text ownerName;

        private static readonly int DimColorId = Shader.PropertyToID("_DimColor");
        private static readonly int CasterCenterId = Shader.PropertyToID("_CasterCenter");
        private static readonly int CasterRadiusId = Shader.PropertyToID("_CasterRadius");
        private static readonly int TargetCenterId = Shader.PropertyToID("_TargetCenter");
        private static readonly int TargetRadiusId = Shader.PropertyToID("_TargetRadius");
        private static readonly int UseTargetId = Shader.PropertyToID("_UseTarget");

        private UnitActor caster;
        private UnitActor target;
        private Camera worldCamera;
        private MonsterActiveFocusPreset preset;
        private Material runtimeDimMaterial;
        private GameObject focusFeedbackInstance;
        private IMonsterActiveFocusFeedback focusFeedback;
        private bool currentIsMythic;
        private float elapsed;
        private float releaseElapsed;
        private float releaseRequestedAt;
        private float releaseOverlayAlpha;
        private float releaseBannerAlpha;
        private Vector2 bannerRestPosition;
        private Vector2 portraitRestPosition;
        private Vector2 skillStripRestPosition;
        private bool releaseRequested;
        private bool releasing;
        private bool showing;
        private bool layoutCached;

        public bool IsShowing => showing;
        public string CurrentSkillName => skillName != null ? skillName.text : string.Empty;
        public string CurrentOwnerName => ownerName != null ? ownerName.text : string.Empty;
        public TMP_FontAsset OwnerFont => ownerName != null ? ownerName.font : null;
        public TMP_FontAsset SkillFont => skillName != null ? skillName.font : null;

        private void Awake()
        {
            EnsureView();
            CacheLayout();
            EnsureFocusFeedback();
            HideImmediate();
        }

        private void OnDestroy()
        {
            focusFeedback?.StopImmediate();
            if (runtimeDimMaterial != null)
            {
                Destroy(runtimeDimMaterial);
                runtimeDimMaterial = null;
            }
        }

        public void Show(
            UnitActor focusCaster,
            UnitActor focusTarget,
            MonsterActiveSkill activeSkill,
            MonsterActiveFocusPreset focusPreset,
            Camera camera)
        {
            EnsureView();
            CacheLayout();
            EnsureFocusFeedback();
            caster = focusCaster;
            target = focusTarget;
            worldCamera = camera != null ? camera : Camera.main;
            preset = focusPreset;
            elapsed = 0f;
            releaseElapsed = 0f;
            releaseRequestedAt = 0f;
            releaseRequested = false;
            releasing = false;
            showing = true;

            overlayGroup.gameObject.SetActive(true);
            bannerGroup.gameObject.SetActive(true);
            overlayGroup.alpha = 0f;
            bannerGroup.alpha = 0f;
            ResetLayoutPose();
            bannerRect.anchoredPosition = bannerRestPosition + Vector2.left * 160f;
            bannerRect.localScale = Vector3.one * 0.94f;
            portraitStageRect.anchoredPosition = portraitRestPosition + Vector2.right * 135f;
            skillStrip.rectTransform.anchoredPosition = skillStripRestPosition + Vector2.left * 110f;

            var portraitSprite = focusCaster != null ? focusCaster.Presentation.Portrait : null;
            portrait.sprite = portraitSprite;
            portrait.enabled = portraitSprite != null;
            portraitFallback.gameObject.SetActive(portraitSprite == null);
            portraitFallback.text = ResolveFallbackLetter(focusCaster?.DisplayName);
            ownerName.text = string.IsNullOrWhiteSpace(focusCaster?.DisplayName)
                ? string.Empty
                : focusCaster.DisplayName;
            skillName.text = activeSkill?.DisplayName ?? "ACTIVE SKILL";

            var rarity = focusCaster != null ? focusCaster.Presentation.Rarity : MonsterRarity.Legendary;
            currentIsMythic = rarity == MonsterRarity.Mythic;
            var config = MonsterActiveFocusPresentationConfig.Current;
            var cutInBackground = config?.ResolveCutInBackground(rarity);
            bannerBackground.sprite = cutInBackground;
            bannerBackground.preserveAspect = false;
            bannerBackground.material = config?.CutInEdgeFadeMaterialTemplate;
            bannerBackground.color = cutInBackground != null
                ? Color.white
                : Color.Lerp(
                    new Color(0.01f, 0.015f, 0.035f, 0.92f),
                    preset.AccentColor,
                    rarity == MonsterRarity.Mythic ? 0.08f : 0.04f);
            var useGeneratedBackground = cutInBackground != null;
            accent.gameObject.SetActive(!useGeneratedBackground);
            accentGlass.gameObject.SetActive(!useGeneratedBackground);
            energyEdge.gameObject.SetActive(!useGeneratedBackground);
            lightShard.gameObject.SetActive(!useGeneratedBackground);
            rarityLabel.text = rarity == MonsterRarity.Mythic ? "MYTHIC ACTIVE" : "LEGENDARY ACTIVE";
            accent.color = new Color(
                preset.AccentColor.r,
                preset.AccentColor.g,
                preset.AccentColor.b,
                rarity == MonsterRarity.Mythic ? 0.3f : 0.24f);
            accentGlass.color = WithAlpha(preset.AccentColor, 0.12f);
            energyEdge.color = WithAlpha(preset.AccentColor, 0.48f);
            lightShard.color = WithAlpha(preset.AccentColor, 0.2f);
            rarityLabel.color = preset.AccentColor;
            skillStrip.color = Color.Lerp(
                new Color(0.012f, 0.018f, 0.045f, 0.94f),
                preset.AccentColor,
                rarity == MonsterRarity.Mythic ? 0.08f : 0.04f);
            PrepareDimMaterial();
            UpdateSpotlight();
            focusFeedback?.PlayEnter(preset.AccentColor, currentIsMythic);
        }

        public void Show(UnitActor focusCaster, MonsterActiveSkill activeSkill, float visibleDuration)
        {
            var config = MonsterActiveFocusPresentationConfig.Current;
            var rarity = focusCaster != null
                ? focusCaster.Presentation.Rarity
                : MonsterRarity.Legendary;
            Show(
                focusCaster,
                focusCaster?.Target,
                activeSkill,
                config != null ? config.ResolvePreset(rarity) : default,
                Camera.main);
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!showing)
            {
                return;
            }

            UpdateSpotlight();
            var step = Mathf.Max(0f, unscaledDeltaTime);
            elapsed += step;
            var releaseStart = Mathf.Max(
                releaseRequestedAt,
                preset.MinimumVisibleDuration - preset.FadeOut);
            if (releaseRequested && elapsed >= releaseStart)
            {
                if (!releasing)
                {
                    releasing = true;
                    releaseElapsed = 0f;
                    releaseOverlayAlpha = overlayGroup.alpha;
                    releaseBannerAlpha = bannerGroup.alpha;
                    focusFeedback?.PlayRelease(preset.AccentColor, currentIsMythic);
                }

                releaseElapsed = Mathf.Max(0f, elapsed - releaseStart);
                var t = Mathf.Clamp01(releaseElapsed / preset.FadeOut);
                var eased = 1f - Mathf.Pow(1f - t, 2f);
                overlayGroup.alpha = Mathf.Lerp(releaseOverlayAlpha, 0f, eased);
                bannerGroup.alpha = Mathf.Lerp(releaseBannerAlpha, 0f, eased);
                bannerRect.anchoredPosition = bannerRestPosition + Vector2.left * (120f * eased);
                bannerRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.97f, eased);
                portraitStageRect.anchoredPosition = portraitRestPosition + Vector2.right * (65f * eased);
                skillStrip.rectTransform.anchoredPosition = skillStripRestPosition + Vector2.left * (90f * eased);
                if (t >= 1f)
                {
                    HideImmediate();
                }
                return;
            }

            var enter = Mathf.Clamp01(elapsed / preset.FadeIn);
            var easedEnter = 1f - Mathf.Pow(1f - enter, 3f);
            var portraitEnter = 1f - Mathf.Pow(1f - enter, 4f);
            var stripEnter = Mathf.Clamp01(
                (elapsed - 0.025f) / Mathf.Max(0.05f, preset.FadeIn - 0.025f));
            stripEnter = 1f - Mathf.Pow(1f - stripEnter, 3f);
            overlayGroup.alpha = enter;
            bannerGroup.alpha = easedEnter;
            bannerRect.anchoredPosition = Vector2.Lerp(
                bannerRestPosition + Vector2.left * 160f,
                bannerRestPosition,
                easedEnter);
            bannerRect.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, easedEnter);
            portraitStageRect.anchoredPosition = Vector2.Lerp(
                portraitRestPosition + Vector2.right * 135f,
                portraitRestPosition,
                portraitEnter);
            skillStrip.rectTransform.anchoredPosition = Vector2.Lerp(
                skillStripRestPosition + Vector2.left * 110f,
                skillStripRestPosition,
                stripEnter);
        }

        public void BeginRelease()
        {
            if (!showing || releaseRequested)
            {
                return;
            }

            releaseRequested = true;
            releaseRequestedAt = elapsed;
        }

        public void HideImmediate()
        {
            showing = false;
            releaseRequested = false;
            releasing = false;
            elapsed = 0f;
            releaseElapsed = 0f;
            releaseRequestedAt = 0f;
            caster = null;
            target = null;
            worldCamera = null;
            focusFeedback?.StopImmediate();
            ResetLayoutPose();
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

        private void CacheLayout()
        {
            if (layoutCached || bannerRect == null || portraitStageRect == null || skillStrip == null)
            {
                return;
            }

            bannerRestPosition = bannerRect.anchoredPosition;
            portraitRestPosition = portraitStageRect.anchoredPosition;
            skillStripRestPosition = skillStrip.rectTransform.anchoredPosition;
            layoutCached = true;
        }

        private void ResetLayoutPose()
        {
            if (!layoutCached)
            {
                return;
            }

            bannerRect.anchoredPosition = bannerRestPosition;
            bannerRect.localScale = Vector3.one;
            portraitStageRect.anchoredPosition = portraitRestPosition;
            skillStrip.rectTransform.anchoredPosition = skillStripRestPosition;
        }

        private void EnsureFocusFeedback()
        {
            if (focusFeedback != null)
            {
                return;
            }

            var prefab = MonsterActiveFocusPresentationConfig.Current?.FocusFeedbackPrefab;
            if (prefab == null)
            {
                return; // FEEL 연동을 제거해도 핵심 컷인은 그대로 동작한다.
            }

            var host = overlayGroup != null && overlayGroup.transform.parent != null
                ? overlayGroup.transform.parent
                : transform;
            focusFeedbackInstance = Instantiate(prefab, host, false);
            focusFeedbackInstance.name = prefab.name;
            if (focusFeedbackInstance.transform is RectTransform feedbackRect)
            {
                SetStretchRect(feedbackRect);
            }
            if (bannerRect != null && bannerRect.parent == host)
            {
                focusFeedbackInstance.transform.SetSiblingIndex(bannerRect.GetSiblingIndex());
            }

            var behaviours = focusFeedbackInstance.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (behaviour is IMonsterActiveFocusFeedback candidate)
                {
                    focusFeedback = candidate;
                    break;
                }
            }
        }

        private void PrepareDimMaterial()
        {
            var template = MonsterActiveFocusPresentationConfig.Current?.DimMaterialTemplate;
            if (template != null && (runtimeDimMaterial == null || runtimeDimMaterial.shader != template.shader))
            {
                if (runtimeDimMaterial != null)
                {
                    Destroy(runtimeDimMaterial);
                }
                runtimeDimMaterial = new Material(template)
                {
                    name = "MonsterActiveFocusDim_Runtime"
                };
                dimOverlay.material = runtimeDimMaterial;
            }

            if (runtimeDimMaterial != null && runtimeDimMaterial.HasProperty(DimColorId))
            {
                runtimeDimMaterial.SetColor(
                    DimColorId,
                    new Color(0.012f, 0.02f, 0.045f, preset.DimAlpha));
            }
            else
            {
                dimOverlay.color = new Color(0.012f, 0.02f, 0.045f, preset.DimAlpha);
            }
        }

        private void UpdateSpotlight()
        {
            if (runtimeDimMaterial == null || worldCamera == null || caster == null)
            {
                return;
            }

            var casterPoint = ResolveViewportPoint(caster.transform.position + Vector3.up * 0.65f);
            runtimeDimMaterial.SetVector(CasterCenterId, casterPoint);
            runtimeDimMaterial.SetVector(CasterRadiusId, ResolveSpotlightRadius(caster, casterPoint.z, 1f));

            var useTarget = target != null && target != caster && target.IsAlive;
            runtimeDimMaterial.SetFloat(UseTargetId, useTarget ? 1f : 0f);
            if (!useTarget)
            {
                return;
            }
            var targetPoint = ResolveViewportPoint(target.transform.position + Vector3.up * 0.55f);
            runtimeDimMaterial.SetVector(TargetCenterId, targetPoint);
            runtimeDimMaterial.SetVector(TargetRadiusId, ResolveSpotlightRadius(target, targetPoint.z, 0.82f));
        }

        private Vector3 ResolveViewportPoint(Vector3 worldPosition)
        {
            var point = worldCamera.WorldToViewportPoint(worldPosition);
            point.x = Mathf.Clamp01(point.x);
            point.y = Mathf.Clamp01(point.y);
            return point;
        }

        private static Vector2 ResolveSpotlightRadius(UnitActor actor, float depth, float multiplier)
        {
            var body = actor != null ? actor.BodyRadius : 0.45f;
            var depthScale = Mathf.Clamp(10f / Mathf.Max(3f, depth), 0.65f, 1.5f);
            return new Vector2(
                Mathf.Clamp(0.085f * body * depthScale * multiplier, 0.055f, 0.16f),
                Mathf.Clamp(0.16f * body * depthScale * multiplier, 0.10f, 0.28f));
        }

        private void EnsureView()
        {
            if (overlayGroup != null && bannerGroup != null && bannerRect != null &&
                bannerBackground != null && accent != null && accentGlass != null &&
                energyEdge != null && lightShard != null && portraitStageRect != null &&
                portrait != null && portraitFallback != null && skillStrip != null &&
                rarityLabel != null && skillName != null && ownerName != null)
            {
                ApplyFonts();
                return;
            }

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
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.GetComponent<GraphicRaycaster>().enabled = false;

            dimOverlay = CreateImage("FocusDim", canvasObject.transform, Color.white);
            dimOverlay.rectTransform.anchorMin = Vector2.zero;
            dimOverlay.rectTransform.anchorMax = Vector2.one;
            dimOverlay.rectTransform.offsetMin = Vector2.zero;
            dimOverlay.rectTransform.offsetMax = Vector2.zero;
            dimOverlay.raycastTarget = false;
            overlayGroup = dimOverlay.gameObject.AddComponent<CanvasGroup>();

            bannerBackground = CreateImage(
                "ActiveSkillCutIn",
                canvasObject.transform,
                new Color(0.01f, 0.015f, 0.035f, 0.025f));
            bannerRect = bannerBackground.rectTransform;
            bannerRect.anchorMin = new Vector2(0f, 0.5f);
            bannerRect.anchorMax = new Vector2(0f, 0.5f);
            bannerRect.pivot = new Vector2(0f, 0.5f);
            bannerRect.anchoredPosition = new Vector2(20f, 0f);
            bannerRect.sizeDelta = new Vector2(740f, 460f);
            bannerGroup = bannerBackground.gameObject.AddComponent<CanvasGroup>();
            bannerBackground.raycastTarget = false;
            bannerBackground.material =
                MonsterActiveFocusPresentationConfig.Current?.CutInEdgeFadeMaterialTemplate;

            accent = CreateImage("GradeGeometry", bannerRect, new Color32(0xE7, 0xD3, 0x4A, 0x3D));
            SetCenterRect(accent.rectTransform, new Vector2(34f, 12f), new Vector2(650f, 320f));
            accent.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 6f);

            accentGlass = CreateImage(
                "UpperGlass",
                bannerRect,
                new Color32(0xE7, 0xD3, 0x4A, 0x1F));
            SetCenterRect(accentGlass.rectTransform, new Vector2(115f, 130f), new Vector2(560f, 96f));
            accentGlass.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -7f);

            energyEdge = CreateImage(
                "EnergyEdge",
                bannerRect,
                new Color32(0xE7, 0xD3, 0x4A, 0x7A));
            SetCenterRect(energyEdge.rectTransform, new Vector2(12f, -190f), new Vector2(680f, 4f));
            energyEdge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -3f);

            lightShard = CreateImage(
                "LightShard",
                bannerRect,
                new Color32(0xE7, 0xD3, 0x4A, 0x33));
            SetCenterRect(lightShard.rectTransform, new Vector2(292f, 164f), new Vector2(116f, 44f));
            lightShard.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 24f);

            var portraitStageObject = new GameObject("PortraitStage", typeof(RectTransform));
            portraitStageObject.transform.SetParent(bannerRect, false);
            portraitStageRect = portraitStageObject.GetComponent<RectTransform>();
            SetLeftRect(portraitStageRect, new Vector2(182f, 28f), new Vector2(450f, 390f));

            portrait = CreateImage("Portrait", portraitStageRect, Color.white);
            portrait.preserveAspect = true;
            SetStretchRect(portrait.rectTransform);
            portrait.raycastTarget = false;

            portraitFallback = CreateText(
                "PortraitFallback",
                portraitStageRect,
                116f,
                FontStyles.Bold,
                new Color(0.75f, 0.88f, 1f, 0.9f));
            SetStretchRect(portraitFallback.rectTransform);
            portraitFallback.alignment = TextAlignmentOptions.Center;

            skillStrip = CreateImage(
                "SkillNameStrip",
                bannerRect,
                new Color(0.012f, 0.018f, 0.045f, 0.94f));
            SetLeftRect(skillStrip.rectTransform, new Vector2(46f, -164f), new Vector2(600f, 96f));

            rarityLabel = CreateText(
                "Rarity",
                skillStrip.transform,
                13f,
                FontStyles.Bold,
                new Color32(0xE7, 0xD3, 0x4A, 0xFF));
            SetLeftRect(rarityLabel.rectTransform, new Vector2(20f, 34f), new Vector2(540f, 18f));
            skillName = CreateText("SkillName", skillStrip.transform, 36f, FontStyles.Bold, Color.white);
            SetLeftRect(skillName.rectTransform, new Vector2(20f, 5f), new Vector2(550f, 44f));
            ownerName = CreateText(
                "OwnerName",
                skillStrip.transform,
                15f,
                FontStyles.Normal,
                new Color(0.78f, 0.84f, 0.93f, 0.78f));
            SetLeftRect(ownerName.rectTransform, new Vector2(22f, -31f), new Vector2(546f, 20f));
            ApplyFonts();
        }

        private void ApplyFonts()
        {
            var config = MonsterActiveFocusPresentationConfig.Current;
            var body = config?.OwnerFont ?? TMP_Settings.defaultFontAsset;
            if (ownerName != null) ownerName.font = body;
            if (portraitFallback != null) portraitFallback.font = body;
            if (rarityLabel != null) rarityLabel.font = body;
            if (skillName != null) skillName.font = config?.SkillFont ?? body;
        }

        private static string ResolveFallbackLetter(string displayName)
        {
            return string.IsNullOrWhiteSpace(displayName)
                ? "?"
                : displayName.Trim().Substring(0, 1);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
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
            var gameObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableAutoSizing = true;
            text.fontSizeMin = 13f;
            text.fontSizeMax = size;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static void SetLeftRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetCenterRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetStretchRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            CanvasGroup configuredOverlayGroup,
            Image configuredDimOverlay,
            CanvasGroup configuredBannerGroup,
            RectTransform configuredBannerRect,
            Image configuredBannerBackground,
            Image configuredAccent,
            Image configuredAccentGlass,
            Image configuredEnergyEdge,
            Image configuredLightShard,
            RectTransform configuredPortraitStageRect,
            Image configuredPortrait,
            TMP_Text configuredPortraitFallback,
            Image configuredSkillStrip,
            TMP_Text configuredRarityLabel,
            TMP_Text configuredSkillName,
            TMP_Text configuredOwnerName)
        {
            overlayGroup = configuredOverlayGroup;
            dimOverlay = configuredDimOverlay;
            bannerGroup = configuredBannerGroup;
            bannerRect = configuredBannerRect;
            bannerBackground = configuredBannerBackground;
            accent = configuredAccent;
            accentGlass = configuredAccentGlass;
            energyEdge = configuredEnergyEdge;
            lightShard = configuredLightShard;
            portraitStageRect = configuredPortraitStageRect;
            portrait = configuredPortrait;
            portraitFallback = configuredPortraitFallback;
            skillStrip = configuredSkillStrip;
            rarityLabel = configuredRarityLabel;
            skillName = configuredSkillName;
            ownerName = configuredOwnerName;
            ApplyFonts();
        }
#endif
    }
}
