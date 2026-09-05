using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Shared.Combat
{
    [DisallowMultipleComponent]
    public sealed class MonsterActiveFocusPresenter : MonoBehaviour // 액티브 발동 집중 HUD
    {
        public const int CanvasSortingOrder = -100; // 전투 연출은 모든 오버레이 UI 뒤에 그린다
        public const bool TargetSpotlightEnabled = false; // 집중 원형은 시전자에게만 표시한다

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
        private static readonly int UseTargetId = Shader.PropertyToID("_UseTarget");

        private UnitActor caster;
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
        private bool casterAccentEnabled;
        private MonsterActiveFocusStyle focusStyle;
        private float minimumVisibleDuration;

        public bool IsShowing => showing;
        public string CurrentSkillName => skillName != null ? skillName.text : string.Empty;
        public string CurrentOwnerName => ownerName != null ? ownerName.text : string.Empty;
        public TMP_FontAsset OwnerFont => ownerName != null ? ownerName.font : null;
        public TMP_FontAsset SkillFont => skillName != null ? skillName.font : null;

        private void Awake()
        {
            ApplyFonts();
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
            Camera camera,
            MonsterActiveFocusStyle? style = null)
        {
            ApplyFonts();
            CacheLayout();
            EnsureFocusFeedback();
            caster = focusCaster;
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
            casterAccentEnabled = config == null || config.CasterAccentEnabled;
            focusStyle = style ?? MonsterActiveFocusStyles.Current;
            minimumVisibleDuration = casterAccentEnabled && focusStyle == MonsterActiveFocusStyle.ClassicDim
                ? preset.MinimumVisibleDuration : config != null
                ? config.ResolveMinimumVisibleDuration(preset) : 0.8f;
            skillStrip.material = config?.CutInEdgeFadeMaterialTemplate;
            rarityLabel.text = rarity == MonsterRarity.Mythic ? "MYTHIC ACTIVE" : "LEGENDARY ACTIVE";
            rarityLabel.color = WithAlpha(preset.AccentColor, 1f);
            skillStrip.color = new Color(0.018f, 0.020f, 0.018f, currentIsMythic ? 0.80f : 0.76f);
            PrepareDimMaterial();
            UpdateSpotlight();
            dimOverlay.enabled = !casterAccentEnabled || MonsterActiveFocusStyles.Dim(focusStyle) > 0f;
            if (focusFeedback is IMonsterActiveCasterFeedback casterFeedback)
            {
                casterFeedback.BindCaster(focusCaster != null ? focusCaster.transform : null,
                    focusCaster != null ? focusCaster.BodyRadius : 0.45f);
            }
            if (focusFeedback is IMonsterActiveStyleFeedback styleFeedback) styleFeedback.SetStyle(focusStyle);
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

            var step = Mathf.Max(0f, unscaledDeltaTime);
            elapsed += step;
            if (casterAccentEnabled && focusStyle != MonsterActiveFocusStyle.ClassicDim)
                dimOverlay.enabled = !releaseRequested && elapsed < MonsterActiveFocusStyles.Lead(focusStyle) &&
                                     MonsterActiveFocusStyles.Dim(focusStyle) > 0f;
            UpdateSpotlight();
            var releaseStart = Mathf.Max(
                releaseRequestedAt,
                minimumVisibleDuration - preset.FadeOut);
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
            overlayGroup.alpha = casterAccentEnabled && focusStyle != MonsterActiveFocusStyle.ClassicDim ? 1f : enter;
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
            if (casterAccentEnabled && focusStyle != MonsterActiveFocusStyle.ClassicDim) dimOverlay.enabled = false;
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
                    new Color(0.012f, 0.02f, 0.045f, casterAccentEnabled && focusStyle != MonsterActiveFocusStyle.ClassicDim ? MonsterActiveFocusStyles.Dim(focusStyle) : preset.DimAlpha));
            }
            else
            {
                dimOverlay.color = new Color(0.012f, 0.02f, 0.045f, casterAccentEnabled && focusStyle != MonsterActiveFocusStyle.ClassicDim ? MonsterActiveFocusStyles.Dim(focusStyle) : preset.DimAlpha);
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
            var casterEnterScale = casterAccentEnabled && focusStyle != MonsterActiveFocusStyle.ClassicDim ? 1.0f : preset.ResolveCasterSpotlightScale(elapsed);
            runtimeDimMaterial.SetVector(
                CasterRadiusId,
                ResolveSpotlightRadius(caster, casterPoint.z, casterEnterScale));
            runtimeDimMaterial.SetFloat(UseTargetId, TargetSpotlightEnabled ? 1f : 0f);
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
