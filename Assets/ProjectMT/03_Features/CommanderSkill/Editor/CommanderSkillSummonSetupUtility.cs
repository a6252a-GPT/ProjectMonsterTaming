#if UNITY_EDITOR
using System;
using System.Linq;
using ProjectMT.Bootstrap;
using ProjectMT.Features.CommanderSkill;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.Items;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectMT.EditorTools.CommanderSkill
{
    public static class CommanderSkillSummonSetupUtility // 전용 소환 SO·Prefab·DEV 검수 슬롯 재생성
    {
        private const string BalancePath = "Assets/ProjectMT/03_Features/CommanderSkill/Resources/CommanderSkills/Rules/CommanderSkillBalanceConfig.asset";
        private const string SummonPath = "Assets/ProjectMT/03_Features/CommanderSkill/Resources/CommanderSkills/Rules/CommanderSkillSummonConfig.asset";
        private const string CatalogPath = "Assets/ProjectMT/03_Features/CommanderSkill/Resources/CommanderSkills/CommanderSkillCatalog.asset";
        private const string ProjectConfigPath = "Assets/ProjectMT/01_Core/Bootstrap/Data/ProjectConfig.asset";
        private const string ResultPrefabPath = "Assets/ProjectMT/03_Features/CommanderSkill/Prefabs/PF_CommanderSkillSummonResultItem.prefab";
        private const string PagePrefabPath = "Assets/ProjectMT/03_Features/CommanderSkill/Prefabs/PF_CommanderSkillSummonPage.prefab";
        private const string ShopPrefabPath = "Assets/ProjectMT/03_Features/Shop/Prefabs/PF_ShopPage.prefab";
        private const string DevScenePath = "Assets/ProjectMT/00_Scenes/DEV_UIManagement.unity";

        private const string FrameBgPath = "Assets/ThirdParty/08_UI/GUI Pro - Minimal Game Dark/GUI Pro-MinimalGame/Shared/Sprite_Common/Frame/ShopFrame/ShopFrame_01~02_White_Bg.png";
        private const string FrameGradientPath = "Assets/ThirdParty/08_UI/GUI Pro - Minimal Game Dark/GUI Pro-MinimalGame/Shared/Sprite_Common/Frame/ShopFrame/ShopFrame_01~02_White_Gradient.png";
        private const string FrameBorderPath = "Assets/ThirdParty/08_UI/GUI Pro - Minimal Game Dark/GUI Pro-MinimalGame/Shared/Sprite_Common/Frame/ShopFrame/ShopFrame_01~02_White_Border.png";
        private const string ButtonBgPath = "Assets/ThirdParty/08_UI/GUI Pro - Minimal Game Dark/GUI Pro-MinimalGame/Shared/Sprite_Common/Button/Button_02_White_Bg.png";
        private const string TicketDefinitionPath = "Assets/ProjectMT/02_Shared/Items/Data/Definitions/SummonTicket/Item_Ticket_CommanderSkillSummon.asset";
        private const string GrimoirePath = "Assets/ProjectMT/05_Art/UI/CommanderSkill/UI_CommanderSkill_SummonGrimoire.png";
        private const string DiscPath = "Assets/ProjectMT/03_Features/CommanderSkill/UI/Generated/SkillDisc.png";
        private const string RingPath = "Assets/ProjectMT/03_Features/CommanderSkill/UI/Generated/SkillRing.png";
        private const string TitleFontPath = "Assets/ProjectMT/05_Art/Fonts/FontAssets/TMP_HakgyoansimYeohaeng_Title.asset";
        private const string ButtonFontPath = "Assets/ProjectMT/05_Art/Fonts/FontAssets/TMP_NoonnuBasicGothic_Button.asset";
        private const string BodyFontPath = "Assets/ProjectMT/05_Art/Fonts/FontAssets/TMP_SpoqaHanSansNeo_Body.asset";

        private static readonly Color Background = new Color32(12, 15, 22, 255);
        private static readonly Color PanelSecondary = new Color32(18, 23, 33, 242);
        private static readonly Color Border = new Color32(88, 101, 119, 220);
        private static readonly Color TextPrimary = new Color32(240, 244, 249, 255);
        private static readonly Color TextSecondary = new Color32(166, 182, 199, 255);
        private static readonly Color Teal = new Color32(42, 173, 188, 255);
        private static readonly Color Ember = new Color32(224, 110, 50, 255);
        private static readonly Color Ice = new Color32(65, 190, 224, 255);

        private static TMP_FontAsset titleFont;
        private static TMP_FontAsset buttonFont;
        private static TMP_FontAsset bodyFont;
        private static Sprite frameBg;
        private static Sprite frameGradient;
        private static Sprite frameBorder;
        private static Sprite buttonBg;

        [MenuItem("ProjectMT/Commander Skill/Rebuild Summon UI")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before rebuilding commander skill summon UI.");
            }

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isDirty)
            {
                throw new InvalidOperationException($"Active scene has unsaved changes: {activeScene.path}");
            }

            LoadStyleAssets();
            EnsureDataAssets(out var catalog);
            var resultPrefab = BuildResultPrefab();
            var pagePrefab = BuildPagePrefab(catalog, resultPrefab);
            IntegrateShopPrefab(pagePrefab);
            IntegrateDevManagementScene(catalog, resultPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("COMMANDER_SKILL_SUMMON_SETUP=PASS " +
                      $"Page={PagePrefabPath} Result={ResultPrefabPath} Shop={ShopPrefabPath} Dev={DevScenePath}");
        }

        private static void LoadStyleAssets()
        {
            titleFont = Require<TMP_FontAsset>(TitleFontPath);
            buttonFont = Require<TMP_FontAsset>(ButtonFontPath);
            bodyFont = Require<TMP_FontAsset>(BodyFontPath);
            frameBg = Require<Sprite>(FrameBgPath);
            frameGradient = Require<Sprite>(FrameGradientPath);
            frameBorder = Require<Sprite>(FrameBorderPath);
            buttonBg = Require<Sprite>(ButtonBgPath);
            Require<Sprite>(GrimoirePath);
            Require<Sprite>(DiscPath);
            Require<Sprite>(RingPath);
        }

        private static void EnsureDataAssets(out CommanderSkillCatalog catalog)
        {
            var balance = Require<CommanderSkillBalanceConfig>(BalancePath);
            catalog = Require<CommanderSkillCatalog>(CatalogPath);
            var projectConfig = Require<ProjectConfig>(ProjectConfigPath);
            if (!balance.TryValidate(out var balanceError))
            {
                throw new InvalidOperationException(
                    $"Commander skill balance config is invalid. {balanceError}");
            }

            var summon = AssetDatabase.LoadAssetAtPath<CommanderSkillSummonConfig>(SummonPath);
            if (summon == null)
            {
                summon = ScriptableObject.CreateInstance<CommanderSkillSummonConfig>();
                AssetDatabase.CreateAsset(summon, SummonPath);
                summon.EditorConfigure(
                    ItemIds.CommanderSkillSummonTicket,
                    new[] { CreateLevel(0, catalog), CreateLevel(30, catalog), CreateLevel(100, catalog) },
                    new[] { CreateOffer(1), CreateOffer(10), CreateOffer(30) },
                    30);
                EditorUtility.SetDirty(summon);
            }
            else if (!summon.TryValidate(balance, out var summonError))
            {
                throw new InvalidOperationException(
                    $"Commander skill summon config is invalid. {summonError}");
            }

            catalog.EditorConfigure(balance, summon, catalog.Skills.ToArray());
            projectConfig.EditorConfigureCommanderSkillBalance(balance);
            projectConfig.EditorConfigureCommanderSkillSummon(summon);
            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(projectConfig);
            if (!catalog.TryValidate(out var error))
            {
                throw new InvalidOperationException($"Commander skill catalog is invalid after summon setup: {error}");
            }
        }

        private static CommanderSkillSummonLevelRule CreateLevel(
            int threshold,
            CommanderSkillCatalog catalog)
        {
            var entries = catalog.Skills
                .Where(skill => skill != null)
                .Select(skill =>
                {
                    var entry = new CommanderSkillSummonPoolEntry();
                    entry.EditorConfigure(skill.SkillId, 100);
                    return entry;
                })
                .ToArray();
            var level = new CommanderSkillSummonLevelRule();
            level.EditorConfigure(threshold, entries);
            return level;
        }

        private static CommanderSkillSummonOffer CreateOffer(int drawCount)
        {
            var offer = new CommanderSkillSummonOffer();
            offer.EditorConfigure(drawCount, drawCount);
            return offer;
        }

        private static CommanderSkillSummonResultItemView BuildResultPrefab()
        {
            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = CreateRect("PF_CommanderSkillSummonResultItem", null);
                SceneManager.MoveGameObjectToScene(root, previewScene);
                SetSize(root.GetComponent<RectTransform>(), Vector2.zero, new Vector2(220f, 260f));
                var background = root.AddComponent<Image>();
                background.sprite = frameBg;
                background.type = Image.Type.Sliced;
                background.color = new Color32(20, 25, 35, 255);
                background.raycastTarget = false;

                var glow = CreateImage(root.transform, "ResultGlow", frameGradient,
                    new Color(Ember.r, Ember.g, Ember.b, 0.3f));
                Stretch(glow.rectTransform, new Vector2(-10f, -10f), new Vector2(10f, 10f));
                glow.transform.SetAsFirstSibling();
                var border = CreateImage(root.transform, "ResultFrame", frameBorder, Ember);
                Stretch(border.rectTransform, Vector2.zero, Vector2.zero);
                var iconPlate = CreateImage(root.transform, "IconPlate", Require<Sprite>(DiscPath),
                    new Color32(34, 42, 55, 255));
                SetSize(iconPlate.rectTransform, new Vector2(0f, 45f), new Vector2(156f, 156f));
                var icon = CreateImage(iconPlate.transform, "SkillIcon", null, Color.white);
                SetSize(icon.rectTransform, Vector2.zero, new Vector2(132f, 132f));
                icon.preserveAspect = true;

                var meta = CreateImage(root.transform, "MetaPanel", null, new Color32(10, 14, 21, 235));
                SetSize(meta.rectTransform, new Vector2(0f, -92f), new Vector2(198f, 62f));
                var name = CreateText(meta.transform, "SkillName", "스킬", 22f, TextPrimary,
                    TextAlignmentOptions.Center, buttonFont);
                SetSize(name.rectTransform, new Vector2(0f, 12f), new Vector2(184f, 30f));
                var category = CreateText(meta.transform, "SkillCategory", "공격형", 16f, Ember,
                    TextAlignmentOptions.Center, bodyFont);
                SetSize(category.rectTransform, new Vector2(0f, -16f), new Vector2(184f, 24f));

                var quantityPlate = CreateImage(root.transform, "QuantityPlate", null, new Color32(8, 12, 18, 230));
                SetSize(quantityPlate.rectTransform, new Vector2(67f, 104f), new Vector2(70f, 36f));
                var quantity = CreateText(quantityPlate.transform, "Quantity", "×1", 20f, TextPrimary,
                    TextAlignmentOptions.Center, buttonFont);
                Stretch(quantity.rectTransform, Vector2.zero, Vector2.zero);
                var newBadge = CreateImage(root.transform, "NewBadge", buttonBg, Teal);
                SetSize(newBadge.rectTransform, new Vector2(-67f, 104f), new Vector2(72f, 36f));
                var newText = CreateText(newBadge.transform, "Label", "NEW", 17f, Color.white,
                    TextAlignmentOptions.Center, buttonFont);
                Stretch(newText.rectTransform, Vector2.zero, Vector2.zero);
                newBadge.gameObject.SetActive(false);

                var view = root.AddComponent<CommanderSkillSummonResultItemView>();
                view.EditorConfigure(border, glow, icon, name, category, quantity, newBadge.gameObject);
                return PrefabUtility.SaveAsPrefabAsset(root, ResultPrefabPath)
                    .GetComponent<CommanderSkillSummonResultItemView>();
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static GameObject BuildPagePrefab(
            CommanderSkillCatalog catalog,
            CommanderSkillSummonResultItemView resultPrefab)
        {
            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = CreateRect("PF_CommanderSkillSummonPage", null);
                SceneManager.MoveGameObjectToScene(root, previewScene);
                SetSize(root.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1350f, 850f));
                var rootImage = root.AddComponent<Image>();
                rootImage.color = Background;
                rootImage.raycastTarget = true;

                var header = CreatePanel(root.transform, "Header", new Color32(17, 21, 29, 255));
                SetAnchors(header.rectTransform, new Vector2(0f, 1f), Vector2.one,
                    new Vector2(20f, -112f), new Vector2(-20f, -12f));
                var title = CreateText(header.transform, "Title", "군단장 스킬 소환", 36f, TextPrimary,
                    TextAlignmentOptions.Left, titleFont);
                SetSize(title.rectTransform, new Vector2(-406f, 20f), new Vector2(490f, 48f));
                var subtitle = CreateText(header.transform, "Subtitle",
                    "전용 소환권으로 스킬을 획득하고, 중복은 레벨업 재료로 저장합니다", 19f,
                    TextSecondary, TextAlignmentOptions.Left, bodyFont);
                SetSize(subtitle.rectTransform, new Vector2(-316f, -23f), new Vector2(670f, 34f));

                var ticketDefinition = Require<ItemDefinition>(TicketDefinitionPath);
                var ticketBar = CreatePanel(header.transform, "TicketBar", new Color32(12, 17, 25, 255));
                SetSize(ticketBar.rectTransform, new Vector2(460f, 0f), new Vector2(300f, 60f));
                var ticketIcon = CreateImage(ticketBar.transform, "TicketIcon", ticketDefinition.Icon, Color.white);
                SetSize(ticketIcon.rectTransform, new Vector2(-112f, 0f), new Vector2(44f, 44f));
                ticketIcon.preserveAspect = true;
                var ticketText = CreateText(ticketBar.transform, "TicketText", "스킬 소환권  0", 21f,
                    TextPrimary, TextAlignmentOptions.Center, buttonFont);
                SetSize(ticketText.rectTransform, new Vector2(20f, 0f), new Vector2(230f, 48f));

                var stage = CreatePanel(root.transform, "SummonStage", PanelSecondary);
                SetAnchors(stage.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(20f, 174f), new Vector2(-20f, -124f));
                AddBorder(stage.transform);
                var stageGlow = CreateImage(stage.transform, "StageGlow", frameGradient,
                    new Color32(34, 82, 96, 80));
                Stretch(stageGlow.rectTransform, new Vector2(6f, 6f), new Vector2(-6f, -6f));
                var grimoire = CreateImage(stage.transform, "SummonGrimoire", Require<Sprite>(GrimoirePath), Color.white);
                SetSize(grimoire.rectTransform, new Vector2(-95f, 0f), new Vector2(505f, 505f));
                grimoire.preserveAspect = true;
                CreateOrbitSkill(stage.transform, catalog, CommanderSkillIds.Starter,
                    new Vector2(-390f, 72f), Ember);
                CreateOrbitSkill(stage.transform, catalog, "CS_DoomSpear",
                    new Vector2(225f, 120f), Ice);

                var infoCard = CreatePanel(stage.transform, "SummonLevelCard", new Color32(20, 29, 41, 250));
                SetSize(infoCard.rectTransform, new Vector2(460f, 64f), new Vector2(325f, 254f));
                AddBorder(infoCard.transform);
                infoCard.raycastTarget = true;
                var infoButton = infoCard.gameObject.AddComponent<Button>();
                infoButton.targetGraphic = infoCard;
                ConfigureButtonColors(infoButton, new Color32(20, 29, 41, 255), new Color32(31, 52, 66, 255));
                var infoHint = CreateText(infoCard.transform, "InfoHint", "확률 정보  >", 17f, Teal,
                    TextAlignmentOptions.TopRight, buttonFont);
                SetSize(infoHint.rectTransform, new Vector2(0f, 98f), new Vector2(285f, 28f));
                var levelText = CreateText(infoCard.transform, "SummonLevel", "소환 Lv.1", 31f,
                    TextPrimary, TextAlignmentOptions.Center, titleFont);
                SetSize(levelText.rectTransform, new Vector2(0f, 58f), new Vector2(285f, 48f));
                var progressBar = CreateImage(infoCard.transform, "ProgressBar", null, new Color32(8, 12, 18, 255));
                SetSize(progressBar.rectTransform, new Vector2(0f, 13f), new Vector2(270f, 22f));
                var progressFill = CreateImage(progressBar.transform, "Fill", null, Teal);
                progressFill.type = Image.Type.Filled;
                progressFill.fillMethod = Image.FillMethod.Horizontal;
                progressFill.fillOrigin = 0;
                progressFill.fillAmount = 0f;
                Stretch(progressFill.rectTransform, new Vector2(3f, 3f), new Vector2(-3f, -3f));
                var progressText = CreateText(infoCard.transform, "ProgressText", "0 / 30", 17f,
                    TextSecondary, TextAlignmentOptions.Center, bodyFont);
                SetSize(progressText.rectTransform, new Vector2(0f, -17f), new Vector2(280f, 26f));
                var rateSummary = CreateText(infoCard.transform, "RateSummary", "화염구 50%  ·  얼음 수정구 50%",
                    17f, TextSecondary, TextAlignmentOptions.Center, bodyFont);
                SetSize(rateSummary.rectTransform, new Vector2(0f, -72f), new Vector2(285f, 66f));
                var stageMessage = CreateText(stage.transform, "StageMessage",
                    "현재 제작 스킬 2종 · 소환 단계와 확률은 1차 검증 시드입니다", 18f,
                    TextSecondary, TextAlignmentOptions.Center, bodyFont);
                SetSize(stageMessage.rectTransform, new Vector2(0f, -246f), new Vector2(900f, 30f));

                var status = CreateText(root.transform, "StatusText", "", 17f, TextSecondary,
                    TextAlignmentOptions.Center, bodyFont);
                SetSize(status.rectTransform, new Vector2(0f, -254f), new Vector2(1000f, 28f));
                var offerButtons = new Button[3];
                var offerTexts = new TMP_Text[3];
                var ad = CreateOfferButton(root.transform, "Advertisement10", new Vector2(-492f, -343f),
                    "광고 10회\n<color=#8996A6>SDK 준비 중</color>", new Color32(72, 76, 86, 255), out _);
                ad.interactable = false;
                var positions = new[] { -164f, 164f, 492f };
                var counts = new[] { 1, 10, 30 };
                for (var index = 0; index < counts.Length; index++)
                {
                    offerButtons[index] = CreateOfferButton(root.transform, $"Summon{counts[index]}",
                        new Vector2(positions[index], -343f),
                        $"{counts[index]:N0}회 소환\n<color=#B9C8D8>소환권 {counts[index]:N0}</color>",
                        index == 2 ? new Color32(157, 86, 39, 255) : new Color32(25, 113, 139, 255),
                        out offerTexts[index]);
                }

                BuildLevelInfoPopup(root.transform,
                    out var levelPopup, out var levelClose, out var previous, out var next,
                    out var inspectedLevel, out var inspectedThreshold, out var inspectedProbability,
                    out var inspectedReward);
                BuildResultOverlay(root.transform,
                    out var resultOverlay, out var resultItemsRoot, out var resultTitle,
                    out var resultSummary, out var resultClose);
                var controller = root.AddComponent<CommanderSkillSummonController>();
                controller.EditorConfigure(
                    levelText, progressText, progressFill, ticketText, rateSummary, status,
                    ad, offerButtons, offerTexts, counts,
                    infoButton, levelPopup, levelClose, previous, next,
                    inspectedLevel, inspectedThreshold, inspectedProbability, inspectedReward,
                    resultOverlay, resultItemsRoot, resultPrefab, resultTitle, resultSummary, resultClose);
                return PrefabUtility.SaveAsPrefabAsset(root, PagePrefabPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void CreateOrbitSkill(
            Transform parent, CommanderSkillCatalog catalog, string skillId, Vector2 position, Color accent)
        {
            if (!catalog.TryGet(skillId, out var definition))
            {
                return;
            }

            var root = CreateImage(parent, $"Orbit_{skillId}", Require<Sprite>(DiscPath),
                new Color32(25, 34, 45, 255));
            SetSize(root.rectTransform, position, new Vector2(126f, 126f));
            var ring = CreateImage(root.transform, "Ring", Require<Sprite>(RingPath), accent);
            Stretch(ring.rectTransform, new Vector2(-5f, -5f), new Vector2(5f, 5f));
            var icon = CreateImage(root.transform, "SkillIcon", definition.Icon, Color.white);
            SetSize(icon.rectTransform, Vector2.zero, new Vector2(94f, 94f));
            icon.preserveAspect = true;
        }

        private static Button CreateOfferButton(
            Transform parent, string name, Vector2 position, string label, Color color, out TMP_Text labelText)
        {
            var image = CreateImage(parent, name, buttonBg, color);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            SetSize(image.rectTransform, position, new Vector2(300f, 112f));
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ConfigureButtonColors(button, color, Color.Lerp(color, Color.white, 0.16f));
            AddBorder(image.transform);
            labelText = CreateText(image.transform, "Label", label, 22f, Color.white,
                TextAlignmentOptions.Center, buttonFont);
            Stretch(labelText.rectTransform, new Vector2(12f, 8f), new Vector2(-12f, -8f));
            return button;
        }

        private static void BuildLevelInfoPopup(
            Transform parent, out GameObject popup, out Button close, out Button previous, out Button next,
            out TMP_Text level, out TMP_Text threshold, out TMP_Text probability, out TMP_Text reward)
        {
            var overlay = CreateImage(parent, "CommanderSkillSummonLevelInfoPopup", null,
                new Color32(5, 7, 11, 235));
            Stretch(overlay.rectTransform, Vector2.zero, Vector2.zero);
            overlay.raycastTarget = true;
            popup = overlay.gameObject;
            var panel = CreatePanel(overlay.transform, "LevelInfoPanel", new Color32(24, 29, 39, 255));
            SetSize(panel.rectTransform, Vector2.zero, new Vector2(690f, 690f));
            AddBorder(panel.transform);
            var title = CreateText(panel.transform, "Title", "소환 레벨 정보", 34f, TextPrimary,
                TextAlignmentOptions.Center, titleFont);
            SetSize(title.rectTransform, new Vector2(0f, 294f), new Vector2(520f, 48f));
            close = CreateSmallButton(panel.transform, "Close", "×", new Vector2(305f, 304f),
                new Vector2(54f, 54f), new Color32(65, 73, 86, 255));
            previous = CreateSmallButton(panel.transform, "PreviousLevel", "‹", new Vector2(-246f, 218f),
                new Vector2(66f, 66f), new Color32(35, 87, 101, 255));
            next = CreateSmallButton(panel.transform, "NextLevel", "›", new Vector2(246f, 218f),
                new Vector2(66f, 66f), new Color32(35, 87, 101, 255));
            level = CreateText(panel.transform, "InspectedLevel", "Lv.1  ·  현재", 31f, TextPrimary,
                TextAlignmentOptions.Center, titleFont);
            SetSize(level.rectTransform, new Vector2(0f, 224f), new Vector2(360f, 48f));
            threshold = CreateText(panel.transform, "Threshold", "누적 소환 0회부터 적용", 19f,
                TextSecondary, TextAlignmentOptions.Center, bodyFont);
            SetSize(threshold.rectTransform, new Vector2(0f, 174f), new Vector2(520f, 34f));
            var poolPanel = CreatePanel(panel.transform, "ProbabilityPanel", new Color32(14, 19, 28, 255));
            SetSize(poolPanel.rectTransform, new Vector2(0f, 22f), new Vector2(590f, 250f));
            AddBorder(poolPanel.transform);
            var poolTitle = CreateText(poolPanel.transform, "Header", "현재 소환 풀 · 개별 확률", 21f, Teal,
                TextAlignmentOptions.TopLeft, buttonFont);
            SetSize(poolTitle.rectTransform, new Vector2(0f, 92f), new Vector2(530f, 34f));
            probability = CreateText(poolPanel.transform, "ProbabilityRows",
                "화염구        50.0%  ·  공격형\n얼음 수정구   50.0%  ·  공격형", 22f,
                TextPrimary, TextAlignmentOptions.TopLeft, bodyFont);
            SetSize(probability.rectTransform, new Vector2(0f, -16f), new Vector2(530f, 150f));
            var rewardPanel = CreatePanel(panel.transform, "RewardPanel", new Color32(18, 23, 32, 255));
            SetSize(rewardPanel.rectTransform, new Vector2(0f, -205f), new Vector2(590f, 124f));
            AddBorder(rewardPanel.transform);
            var rewardTitle = CreateText(rewardPanel.transform, "Header", "레벨 달성 보상", 20f, TextPrimary,
                TextAlignmentOptions.TopLeft, buttonFont);
            SetSize(rewardTitle.rectTransform, new Vector2(0f, 33f), new Vector2(530f, 30f));
            reward = CreateText(rewardPanel.transform, "RewardText",
                "정식 밸런스 확정 후 연결됩니다", 18f, TextSecondary,
                TextAlignmentOptions.BottomLeft, bodyFont);
            SetSize(reward.rectTransform, new Vector2(0f, -25f), new Vector2(530f, 40f));
            popup.SetActive(false);
        }

        private static void BuildResultOverlay(
            Transform parent, out GameObject overlayRoot, out RectTransform itemsRoot,
            out TMP_Text title, out TMP_Text summary, out Button close)
        {
            var overlay = CreateImage(parent, "CommanderSkillSummonResultOverlay", null,
                new Color32(4, 6, 10, 244));
            Stretch(overlay.rectTransform, Vector2.zero, Vector2.zero);
            overlay.raycastTarget = true;
            overlayRoot = overlay.gameObject;
            title = CreateText(overlay.transform, "ResultTitle", "군단장 스킬 소환 결과", 38f,
                TextPrimary, TextAlignmentOptions.Center, titleFont);
            SetSize(title.rectTransform, new Vector2(0f, 352f), new Vector2(900f, 52f));
            summary = CreateText(overlay.transform, "ResultSummary", "중복은 스킬 레벨업 재료로 저장됩니다", 19f,
                TextSecondary, TextAlignmentOptions.Center, bodyFont);
            SetSize(summary.rectTransform, new Vector2(0f, 307f), new Vector2(1000f, 34f));
            var gridPanel = CreatePanel(overlay.transform, "ResultPanel", new Color32(15, 20, 29, 245));
            SetSize(gridPanel.rectTransform, new Vector2(0f, 18f), new Vector2(1140f, 530f));
            AddBorder(gridPanel.transform);
            var grid = CreateRect("ResultItemsRoot", gridPanel.transform).GetComponent<RectTransform>();
            SetSize(grid, Vector2.zero, new Vector2(1040f, 450f));
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(220f, 260f);
            layout.spacing = new Vector2(30f, 24f);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 4;
            itemsRoot = grid;
            close = CreateSmallButton(overlay.transform, "CloseResults", "확인", new Vector2(0f, -363f),
                new Vector2(260f, 66f), new Color32(28, 125, 151, 255));
            var hint = CreateText(overlay.transform, "CloseHint", "결과를 확인한 뒤 닫아 주세요", 16f,
                TextSecondary, TextAlignmentOptions.Center, bodyFont);
            SetSize(hint.rectTransform, new Vector2(0f, -405f), new Vector2(500f, 26f));
            overlayRoot.SetActive(false);
        }

        private static void IntegrateShopPrefab(GameObject pagePrefab)
        {
            var root = PrefabUtility.LoadPrefabContents(ShopPrefabPath);
            try
            {
                var skillShop = root.transform.Find("ShopPanel/Panel/PanelGroup/RightPanel/RightPoint/SkillShop");
                if (skillShop == null)
                {
                    throw new InvalidOperationException("PF_ShopPage SkillShop root is missing.");
                }

                for (var index = skillShop.childCount - 1; index >= 0; index--)
                {
                    Object.DestroyImmediate(skillShop.GetChild(index).gameObject);
                }

                Stretch(skillShop as RectTransform, Vector2.zero, Vector2.zero);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(pagePrefab, skillShop);
                instance.name = "PF_CommanderSkillSummonPage";
                Stretch(instance.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
                PrefabUtility.SaveAsPrefabAsset(root, ShopPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void IntegrateDevManagementScene(
            CommanderSkillCatalog catalog, CommanderSkillSummonResultItemView resultPrefab)
        {
            var scene = EditorSceneManager.OpenScene(DevScenePath, OpenSceneMode.Additive);
            try
            {
                var runtimeRoot = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .FirstOrDefault(transform => transform.name == "RuntimeWindowsRoot_EditorOnly");
                if (runtimeRoot == null)
                {
                    throw new InvalidOperationException("DEV_UIManagement RuntimeWindowsRoot_EditorOnly is missing.");
                }

                var mainSlot = runtimeRoot.Find("Slot_PF_ShopPage_CommanderSkill");
                if (mainSlot != null)
                {
                    ConfigureShopPreview(mainSlot.gameObject, false, false, catalog, resultPrefab);
                }

                RemoveChild(runtimeRoot, "Slot_PF_ShopPage_CommanderSkill_LevelInfo");
                RemoveChild(runtimeRoot, "Slot_PF_ShopPage_CommanderSkill_Result");
                CreateDevShopSlot(runtimeRoot, "Slot_PF_ShopPage_CommanderSkill_LevelInfo",
                    new Vector2(13260f, 2000f), "PF_ShopPage · 군단장 스킬 소환 레벨 정보",
                    true, false, catalog, resultPrefab);
                CreateDevShopSlot(runtimeRoot, "Slot_PF_ShopPage_CommanderSkill_Result",
                    new Vector2(13260f, 700f), "PF_ShopPage · 군단장 스킬 소환 결과",
                    false, true, catalog, resultPrefab);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException("Failed to save DEV_UIManagement scene.");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void CreateDevShopSlot(
            Transform runtimeRoot, string slotName, Vector2 position, string label,
            bool showLevelInfo, bool showResult, CommanderSkillCatalog catalog,
            CommanderSkillSummonResultItemView resultPrefab)
        {
            var slot = CreateRect(slotName, runtimeRoot);
            slot.tag = "EditorOnly";
            SetSize(slot.GetComponent<RectTransform>(), position, new Vector2(1920f, 1080f));
            var preview = CreateImage(slot.transform, "PreviewScreen_1920x1080", null,
                new Color32(7, 9, 14, 255));
            Stretch(preview.rectTransform, Vector2.zero, Vector2.zero);
            var labelText = CreateText(slot.transform, $"Label_{slotName}", label, 30f,
                TextPrimary, TextAlignmentOptions.Center, buttonFont);
            SetSize(labelText.rectTransform, new Vector2(0f, 570f), new Vector2(1900f, 54f));
            var shop = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>(ShopPrefabPath), slot.transform);
            shop.name = "PF_ShopPage_REFERENCE";
            Stretch(shop.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            ConfigureShopPreview(shop, showLevelInfo, showResult, catalog, resultPrefab);
        }

        private static void ConfigureShopPreview(
            GameObject slotOrShop, bool showLevelInfo, bool showResult,
            CommanderSkillCatalog catalog, CommanderSkillSummonResultItemView resultPrefab)
        {
            var shop = slotOrShop.name == "PF_ShopPage_REFERENCE"
                ? slotOrShop
                : slotOrShop.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(transform => transform.name == "PF_ShopPage_REFERENCE")?.gameObject;
            if (shop == null)
            {
                return;
            }

            SetNamedActive(shop.transform, "MonsterShop", false);
            SetNamedActive(shop.transform, "SkillShop", true);
            SetNamedActive(shop.transform, "DiamondShop", false);
            SetNamedActive(shop.transform, "ContentShop", false);
            SetNamedActive(shop.transform, "PackageShop", false);
            SetNamedActive(shop.transform, "MonthlySubscriptionShop", false);
            SetNamedActive(shop.transform, "GachaSubMenu", true);
            FindNamed(shop.transform, "CommanderSkillSummonLevelInfoPopup")?.gameObject.SetActive(showLevelInfo);
            FindNamed(shop.transform, "CommanderSkillSummonResultOverlay")?.gameObject.SetActive(showResult);
            var resultRoot = FindNamed(shop.transform, "ResultItemsRoot");
            if (!showResult || resultRoot == null)
            {
                return;
            }

            for (var index = resultRoot.childCount - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(resultRoot.GetChild(index).gameObject);
            }

            if (catalog.TryGet(CommanderSkillIds.Starter, out var fire))
            {
                var item = (GameObject)PrefabUtility.InstantiatePrefab(resultPrefab.gameObject, resultRoot);
                item.GetComponent<CommanderSkillSummonResultItemView>().Bind(fire, 16, false);
            }

            if (catalog.TryGet("CS_DoomSpear", out var ice))
            {
                var item = (GameObject)PrefabUtility.InstantiatePrefab(resultPrefab.gameObject, resultRoot);
                item.GetComponent<CommanderSkillSummonResultItemView>().Bind(ice, 14, false);
            }

            var resultTitle = FindNamed(shop.transform, "ResultTitle")?.GetComponent<TMP_Text>();
            var resultSummary = FindNamed(shop.transform, "ResultSummary")?.GetComponent<TMP_Text>();
            if (resultTitle != null)
            {
                resultTitle.text = "군단장 스킬 소환 결과 · 30회";
            }

            if (resultSummary != null)
            {
                resultSummary.text = "획득 종류 2개  ·  중복은 스킬 레벨업 재료로 저장됩니다";
            }
        }

        private static Image CreatePanel(Transform parent, string name, Color color)
        {
            var image = CreateImage(parent, name, frameBg, color);
            image.type = Image.Type.Sliced;
            return image;
        }

        private static void AddBorder(Transform parent)
        {
            var border = CreateImage(parent, "Border", frameBorder, Border);
            border.type = Image.Type.Sliced;
            Stretch(border.rectTransform, Vector2.zero, Vector2.zero);
            border.transform.SetAsLastSibling();
        }

        private static Button CreateSmallButton(
            Transform parent, string name, string label, Vector2 position, Vector2 size, Color color)
        {
            var image = CreateImage(parent, name, buttonBg, color);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            SetSize(image.rectTransform, position, size);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ConfigureButtonColors(button, color, Color.Lerp(color, Color.white, 0.18f));
            var text = CreateText(image.transform, "Label", label, size.y > 60f ? 25f : 22f,
                Color.white, TextAlignmentOptions.Center, buttonFont);
            Stretch(text.rectTransform, new Vector2(6f, 4f), new Vector2(-6f, -4f));
            return button;
        }

        private static void ConfigureButtonColors(Button button, Color normal, Color highlighted)
        {
            var colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.pressedColor = Color.Lerp(normal, Color.black, 0.18f);
            colors.selectedColor = highlighted;
            colors.disabledColor = new Color32(66, 70, 78, 150);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            return gameObject;
        }

        private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var gameObject = CreateRect(name, parent);
            var image = gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sprite == null ? Image.Type.Simple : Image.Type.Sliced;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(
            Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions alignment, TMP_FontAsset font)
        {
            var gameObject = CreateRect(name, parent);
            var label = gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        private static void SetSize(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void SetAnchors(
            RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private static void RemoveChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void SetNamedActive(Transform root, string name, bool active)
        {
            var target = FindNamed(root, name);
            if (target != null)
            {
                target.gameObject.SetActive(active);
            }
        }

        private static Transform FindNamed(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => transform.name == name);
        }

        private static T Require<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException($"Required asset is missing: {path}");
            }

            return asset;
        }
    }
}
#endif
