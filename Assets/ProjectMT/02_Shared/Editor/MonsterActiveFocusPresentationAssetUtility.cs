#if UNITY_EDITOR
using ProjectMT.Shared.Combat;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Shared.Editor
{
    public static class MonsterActiveFocusPresentationAssetUtility // 정식 집중 HUD 자산 생성·검증
    {
        private const string RootFolder = "Assets/ProjectMT/02_Shared/Combat/Presentation";
        private const string DimMaterialPath = RootFolder + "/MAT_MonsterActiveFocusDim.mat";
        private const string EdgeFadeMaterialPath = RootFolder + "/MAT_MonsterActiveFocusEdgeFade.mat";
        private const string PrefabPath = RootFolder + "/PF_MonsterActiveFocusHud.prefab";
        private const string FocusFeedbackPrefabPath =
            "Assets/ProjectMT/05_Art/FeelPresets/ActiveFocus/PF_MonsterActiveFocusFeel.prefab";
        private const string LegendaryBackgroundPath = RootFolder + "/TX_MonsterActiveFocus_Legendary.png";
        private const string MythicBackgroundPath = RootFolder + "/TX_MonsterActiveFocus_Mythic.png";
        private const string ConfigPath =
            "Assets/ProjectMT/02_Shared/Combat/Resources/MonsterActiveFocusPresentationConfig.asset";

        [MenuItem("Tools/ProjectMT/Combat/Build Active Focus Presentation")]
        public static void BuildAssets()
        {
            EnsureFolder();
            var config = AssetDatabase.LoadAssetAtPath<MonsterActiveFocusPresentationConfig>(ConfigPath);
            if (config == null)
            {
                throw new System.InvalidOperationException($"Focus config not found: {ConfigPath}");
            }

            var legendaryBackground = EnsureBackgroundSprite(LegendaryBackgroundPath);
            var mythicBackground = EnsureBackgroundSprite(MythicBackgroundPath);
            var dimMaterial = EnsureDimMaterial();
            var edgeFadeMaterial = EnsureEdgeFadeMaterial();
            var feedbackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FocusFeedbackPrefabPath);
            var presenter = BuildPrefab(config, legendaryBackground, edgeFadeMaterial);
            config.EditorConfigure(config.OwnerFont, config.SkillFont, presenter, dimMaterial);
            config.EditorConfigureOptionalPresentation(edgeFadeMaterial, feedbackPrefab);
            config.EditorConfigureBackgrounds(legendaryBackground, mythicBackground);
            config.EditorConfigurePresets(
                MonsterActiveFocusPresentationConfig.LegendaryDefault,
                MonsterActiveFocusPresentationConfig.MythicDefault);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            if (!config.TryValidate(out var error))
            {
                throw new System.InvalidOperationException(error);
            }
            Debug.Log($"[MonsterActiveFocus] Formal presentation ready. Prefab={PrefabPath}");
        }

        [MenuItem("Tools/ProjectMT/Combat/Validate Active Focus Presentation")]
        public static void ValidateAssets()
        {
            var config = AssetDatabase.LoadAssetAtPath<MonsterActiveFocusPresentationConfig>(ConfigPath);
            var error = config == null ? $"Focus config not found: {ConfigPath}" : string.Empty;
            if (config == null || !config.TryValidate(out error))
            {
                throw new System.InvalidOperationException(error);
            }
            Debug.Log("[MonsterActiveFocus] Presentation config validation passed.");
        }

        private static Material EnsureDimMaterial()
        {
            var shader = Shader.Find("ProjectMT/UI/MonsterActiveFocusDim");
            if (shader == null)
            {
                throw new System.InvalidOperationException("Monster active focus dim shader is unavailable.");
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(DimMaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "MAT_MonsterActiveFocusDim"
                };
                AssetDatabase.CreateAsset(material, DimMaterialPath);
            }
            else
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
            material.SetColor("_DimColor", new Color(0.012f, 0.02f, 0.045f, 0.38f));
            return material;
        }

        private static Material EnsureEdgeFadeMaterial()
        {
            var shader = Shader.Find("ProjectMT/UI/MonsterActiveFocusEdgeFade");
            if (shader == null)
            {
                throw new System.InvalidOperationException("Monster active focus edge fade shader is unavailable.");
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(EdgeFadeMaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "MAT_MonsterActiveFocusEdgeFade"
                };
                AssetDatabase.CreateAsset(material, EdgeFadeMaterialPath);
            }
            else
            {
                material.shader = shader;
            }
            material.SetFloat("_LeftFeather", 0.08f);
            material.SetFloat("_RightFeather", 0.14f);
            material.SetFloat("_TopFeather", 0.11f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static MonsterActiveFocusPresenter BuildPrefab(
            MonsterActiveFocusPresentationConfig config,
            Sprite defaultBackground,
            Material edgeFadeMaterial)
        {
            var root = new GameObject(
                "PF_MonsterActiveFocusHud",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(MonsterActiveFocusPresenter));
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 460;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                root.GetComponent<GraphicRaycaster>().enabled = false;

                var dim = CreateImage("FocusDim", root.transform, Color.white);
                dim.rectTransform.anchorMin = Vector2.zero;
                dim.rectTransform.anchorMax = Vector2.one;
                dim.rectTransform.offsetMin = Vector2.zero;
                dim.rectTransform.offsetMax = Vector2.zero;
                dim.raycastTarget = false;
                var overlayGroup = dim.gameObject.AddComponent<CanvasGroup>();

                var banner = CreateImage(
                    "ActiveSkillCutIn",
                    root.transform,
                    new Color(0.01f, 0.015f, 0.035f, 0.025f));
                var bannerRect = banner.rectTransform;
                bannerRect.anchorMin = new Vector2(0f, 0.5f);
                bannerRect.anchorMax = new Vector2(0f, 0.5f);
                bannerRect.pivot = new Vector2(0f, 0.5f);
                bannerRect.anchoredPosition = new Vector2(20f, 0f);
                bannerRect.sizeDelta = new Vector2(740f, 460f);
                banner.sprite = defaultBackground;
                banner.preserveAspect = false;
                banner.color = Color.white;
                banner.material = edgeFadeMaterial;
                banner.raycastTarget = false;
                var bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();

                var accent = CreateImage(
                    "GradeGeometry",
                    bannerRect,
                    new Color32(0xE7, 0xD3, 0x4A, 0x3D));
                SetCenterRect(accent.rectTransform, new Vector2(34f, 12f), new Vector2(650f, 320f));
                accent.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 6f);

                var accentGlass = CreateImage(
                    "UpperGlass",
                    bannerRect,
                    new Color32(0xE7, 0xD3, 0x4A, 0x1F));
                SetCenterRect(accentGlass.rectTransform, new Vector2(115f, 130f), new Vector2(560f, 96f));
                accentGlass.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -7f);

                var energyEdge = CreateImage(
                    "EnergyEdge",
                    bannerRect,
                    new Color32(0xE7, 0xD3, 0x4A, 0x7A));
                SetCenterRect(energyEdge.rectTransform, new Vector2(12f, -190f), new Vector2(680f, 4f));
                energyEdge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -3f);

                var lightShard = CreateImage(
                    "LightShard",
                    bannerRect,
                    new Color32(0xE7, 0xD3, 0x4A, 0x33));
                SetCenterRect(lightShard.rectTransform, new Vector2(292f, 164f), new Vector2(116f, 44f));
                lightShard.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 24f);

                var portraitStageObject = new GameObject("PortraitStage", typeof(RectTransform));
                portraitStageObject.transform.SetParent(bannerRect, false);
                var portraitStage = portraitStageObject.GetComponent<RectTransform>();
                SetLeftRect(portraitStage, new Vector2(182f, 28f), new Vector2(450f, 390f));

                var portrait = CreateImage("Portrait", portraitStage, Color.white);
                portrait.preserveAspect = true;
                SetStretchRect(portrait.rectTransform);
                portrait.raycastTarget = false;

                var fallback = CreateText(
                    "PortraitFallback",
                    portraitStage,
                    116f,
                    FontStyles.Bold,
                    new Color(0.75f, 0.88f, 1f, 0.9f),
                    config.OwnerFont);
                SetStretchRect(fallback.rectTransform);
                fallback.alignment = TextAlignmentOptions.Center;

                var skillStrip = CreateImage(
                    "SkillNameStrip",
                    bannerRect,
                    new Color(0.012f, 0.018f, 0.045f, 0.94f));
                SetLeftRect(skillStrip.rectTransform, new Vector2(46f, -164f), new Vector2(600f, 96f));

                var rarity = CreateText(
                    "Rarity",
                    skillStrip.transform,
                    13f,
                    FontStyles.Bold,
                    new Color32(0xE7, 0xD3, 0x4A, 0xFF),
                    config.OwnerFont);
                SetLeftRect(rarity.rectTransform, new Vector2(20f, 34f), new Vector2(540f, 18f));
                var skill = CreateText(
                    "SkillName", skillStrip.transform, 36f, FontStyles.Bold, Color.white, config.SkillFont);
                SetLeftRect(skill.rectTransform, new Vector2(20f, 5f), new Vector2(550f, 44f));
                var owner = CreateText(
                    "OwnerName",
                    skillStrip.transform,
                    15f,
                    FontStyles.Normal,
                    new Color(0.78f, 0.84f, 0.93f, 0.78f),
                    config.OwnerFont);
                SetLeftRect(owner.rectTransform, new Vector2(22f, -31f), new Vector2(546f, 20f));

                var presenter = root.GetComponent<MonsterActiveFocusPresenter>();
                presenter.EditorConfigure(
                    overlayGroup,
                    dim,
                    bannerGroup,
                    bannerRect,
                    banner,
                    accent,
                    accentGlass,
                    energyEdge,
                    lightShard,
                    portraitStage,
                    portrait,
                    fallback,
                    skillStrip,
                    rarity,
                    skill,
                    owner);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                var savedPresenter = saved != null ? saved.GetComponent<MonsterActiveFocusPresenter>() : null;
                if (savedPresenter == null)
                {
                    throw new System.InvalidOperationException("Failed to save active focus HUD prefab.");
                }
                return savedPresenter;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureFolder()
        {
            const string combatFolder = "Assets/ProjectMT/02_Shared/Combat";
            if (!AssetDatabase.IsValidFolder(RootFolder))
            {
                AssetDatabase.CreateFolder(combatFolder, "Presentation");
            }
        }

        private static Sprite EnsureBackgroundSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                throw new System.InvalidOperationException($"Focus background texture not found: {path}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new System.InvalidOperationException($"Focus background Sprite import failed: {path}");
            }
            return sprite;
        }

        private static Image CreateImage(string objectName, Transform parent, Color color)
        {
            var gameObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(
            string objectName,
            Transform parent,
            float size,
            FontStyles style,
            Color color,
            TMP_FontAsset font)
        {
            var gameObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.GetComponent<TextMeshProUGUI>();
            text.font = font != null ? font : TMP_Settings.defaultFontAsset;
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
    }
}
#endif
