using System;
using System.Linq;
using ProjectMT.Contents.CastleRaidHex;
using ProjectMT.Features.MainBattle;
using ProjectMT.Features.WorldDrops;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Pooling;
using ProjectMT.Shared.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    public static partial class CastleRaidUiSetupUtility // 군단의 역습 정식 선택 UI·HUD·씬 연결 재현 도구
    {
        private const string PagePrefabPath =
            "Assets/ProjectMT/03_Features/MainBattle/Prefabs/PF_CastleRaidStageSelectionPage.prefab";
        private const string HudPrefabPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Prefabs/PF_CastleRaidHexHUD.prefab";
        private const string MainBattleScenePath = "Assets/ProjectMT/00_Scenes/01_MainBattle.unity";
        private const string CastleRaidScenePath = "Assets/ProjectMT/00_Scenes/03_CastleRaidHex.unity";
        private const string DevUiScenePath = "Assets/ProjectMT/00_Scenes/DEV_UIManagement.unity";
        private const string StandardMediumPath =
            "Assets/ProjectMT/02_Shared/UI/Prefabs/Standard/PF_UIStandard_PopupMedium.prefab";
        private const string ContentResultOverlayPath =
            "Assets/ProjectMT/01_Core/Bootstrap/Prefabs/PF_ContentResultOverlay.prefab";
        private const string ItemCatalogPath =
            "Assets/ProjectMT/02_Shared/Items/Data/ItemCatalog.asset";
        private const string ItemDropVisualCatalogPath =
            "Assets/ProjectMT/03_Features/WorldDrops/Data/WorldItemDropVisualCatalog.asset";
        private const string EquipmentBalanceConfigPath =
            "Assets/ProjectMT/02_Shared/Equipment/Data/EquipmentBalanceConfig.asset";
        private const string EquipmentDropVisualCatalogPath =
            "Assets/ProjectMT/03_Features/WorldDrops/Data/EquipmentDropChestVisualCatalog.asset";
        private const string GuiRoot =
            "Assets/ThirdParty/08_UI/GUI Pro - Minimal Game Dark/GUI Pro-MinimalGame";
        private const string CardBg = GuiRoot +
            "/Shared/Sprite_Common/Frame/CardFrame/CardFrame_04_White_Bg.png";
        private const string CardInner = GuiRoot +
            "/Shared/Sprite_Common/Frame/CardFrame/CardFrame_04_White_InnerBorder.png";
        private const string TitleBg = GuiRoot +
            "/Shared/Sprite_Common/Frame/CardFrame/CardFrame_04_White_TitleBg.png";
        private const string PanelLine = GuiRoot +
            "/Shared/Sprite_Common/Frame/PanelFrame/PanelFrame_02_White_Line.png";
        private const string ButtonBg = GuiRoot +
            "/Shared/Sprite_Common/Button/Button_02_White_Bg.png";
        private const string CastleArt = GuiRoot +
            "/Theme_Dark/Sprites/~Demo/Demo_Image/Image_Map_Castle.png";
        private const string BattleIcon = GuiRoot +
            "/Theme_Dark/Sprites/~Demo/Demo_Icon/Icon_Battle.png";
        private const string FortressIcon = GuiRoot +
            "/Shared/Icons/PictoIcon/256/fortress_1.png";
        private const string DiamondItemDefinitionPath =
            "Assets/ProjectMT/02_Shared/Items/Data/Definitions/Currency/Item_Currency_Diamond.asset";
        private const string SummonTicketItemDefinitionPath =
            "Assets/ProjectMT/02_Shared/Items/Data/Definitions/SummonTicket/Item_Ticket_MonsterSummon.asset";
        private const string ExitIcon = GuiRoot +
            "/Shared/Icons/PictoIcon/128/exit_1.png";
        private const string ArrowLeftIcon = GuiRoot +
            "/Shared/Icons/PictoIcon/128/arrow_left.png";
        private const string ArrowRightIcon = GuiRoot +
            "/Shared/Icons/PictoIcon/128/arrow_right.png";
        private const float StagePageSplit = 0.40f;

        private sealed class StagePageBindings
        {
            public Button Close;
            public Button Enter;
            public TMP_Text EnterLabel;
            public ScrollRect Scroll;
            public Button[] StageButtons;
            public TMP_Text[] StageNumbers;
            public TMP_Text[] StageRewards;
            public TMP_Text[] StageStates;
            public TMP_Text ProgressLabel;
            public UnityEngine.UI.Image ProgressFill;
            public TMP_Text SelectedStage;
            public TMP_Text SelectedFront;
            public TMP_Text SelectedTheme;
            public TMP_Text Reward;
            public TMP_Text ClearState;
        }

        private static TMP_FontAsset font;
        private static Sprite diamondRewardIcon;
        private static Sprite summonTicketRewardIcon;

        [MenuItem("Tools/ProjectMT/Castle Raid/Rebuild Stage UI And HUD")]
        public static void RebuildAll()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isDirty)
            {
                throw new InvalidOperationException(
                    $"현재 씬에 저장하지 않은 변경이 있습니다: {activeScene.path}");
            }

            font = ResolveFont();
            if (font == null)
            {
                throw new InvalidOperationException("정식 TMP 폰트를 찾지 못했습니다.");
            }
            diamondRewardIcon = ItemIcon(DiamondItemDefinitionPath); // 인벤토리 실사용 아이콘을 단일 원본으로 사용
            summonTicketRewardIcon = ItemIcon(SummonTicketItemDefinitionPath);

            var pagePrefab = BuildStageSelectionPage();
            var hudPrefab = BuildBattleHud();
            ApplyBattleTimerAndFailureUi();
            ConnectCastleRaidScene(hudPrefab);
            ConnectMainBattleScene(pagePrefab);
            ConnectDevUiScene(pagePrefab);
            Debug.Log(
                "[CastleRaidUiSetupUtility] 1~100 탑형 선택 UI, GUI Pro HUD, MainBattle/DEV 씬 연결 완료.");
        }

        public static void RunOnceFromCommandLine()
        {
            RebuildAll();
        }

        [MenuItem("Tools/ProjectMT/Castle Raid/Apply Battle Timer And Failure UI")]
        public static void ApplyBattleTimerAndFailureUi()
        {
            font = ResolveFont();
            if (font == null)
            {
                throw new InvalidOperationException("정식 TMP 폰트를 찾지 못했습니다.");
            }

            var rootObject = PrefabUtility.LoadPrefabContents(HudPrefabPath);
            try
            {
                var root = rootObject.GetComponent<RectTransform>();
                if (root == null)
                {
                    throw new InvalidOperationException("군단의 역습 HUD Root가 RectTransform이 아닙니다.");
                }

                RemoveOwnedChild(root, "FailureOverlay");

                var timerPanel = FindChild(root, "BattleTimerBadge") as RectTransform;
                var timerText = timerPanel == null
                    ? null
                    : FindChild(timerPanel, "TimerText")?.GetComponent<TMP_Text>();
                var timerAccent = timerPanel == null
                    ? null
                    : FindChild(timerPanel, "UrgencyAccent")?.GetComponent<UnityEngine.UI.Image>();
                if (timerPanel == null)
                {
                    var statusPanel = FindChild(root, "StatusPanel") as RectTransform;
                    var statusText = statusPanel == null
                        ? null
                        : FindChild(statusPanel, "StatusText")?.GetComponent<TMP_Text>();
                    if (statusPanel == null || statusText == null)
                    {
                        throw new InvalidOperationException("기존 HUD의 StatusPanel/StatusText를 찾지 못했습니다.");
                    }

                    statusText.rectTransform.anchoredPosition = new Vector2(-78f, 0f);
                    statusText.rectTransform.sizeDelta = new Vector2(510f, 38f);
                    var createdTimerPanel = Panel(
                        "BattleTimerBadge",
                        statusPanel,
                        new Vector2(286f, 0f),
                        new Vector2(134f, 48f),
                        new Color32(35, 43, 54, 255),
                        CardBg);
                    timerPanel = createdTimerPanel.rectTransform;
                    timerAccent = Image(
                        "UrgencyAccent",
                        timerPanel,
                        new Vector2(-61f, 0f),
                        new Vector2(5f, 38f),
                        null,
                        new Color32(198, 145, 55, 255),
                        false);
                    Text(
                        "Caption",
                        timerPanel,
                        "제한 시간",
                        new Vector2(-18f, 12f),
                        new Vector2(76f, 16f),
                        10f,
                        TextAlignmentOptions.Center,
                        new Color32(184, 193, 204, 255),
                        FontStyles.Bold);
                    timerText = Text(
                        "TimerText",
                        timerPanel,
                        "03:00",
                        new Vector2(25f, -8f),
                        new Vector2(82f, 25f),
                        21f,
                        TextAlignmentOptions.Center,
                        Color.white,
                        FontStyles.Bold);
                }
                else if (timerText == null || timerAccent == null)
                {
                    throw new InvalidOperationException("기존 BattleTimerBadge의 TimerText/UrgencyAccent 연결이 없습니다.");
                }

                var overlay = Image(
                    "FailureOverlay",
                    root,
                    Vector2.zero,
                    Vector2.zero,
                    null,
                    new Color32(3, 5, 9, 255),
                    true);
                Stretch(overlay.rectTransform);
                var resultOverlayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ContentResultOverlayPath);
                var sourceStage = resultOverlayPrefab == null
                    ? null
                    : FindChild(resultOverlayPrefab.transform, "ClearResultStage_920x900") as RectTransform;
                var resultPresenter = resultOverlayPrefab == null
                    ? null
                    : resultOverlayPrefab.GetComponents<MonoBehaviour>()
                        .FirstOrDefault(component =>
                            component.GetType().Name == "ContentResultOverlayPresenter");
                if (sourceStage == null || resultPresenter == null)
                {
                    throw new InvalidOperationException("공용 클리어 결과창의 920x900 스테이지를 찾지 못했습니다.");
                }

                var stageObject = UnityEngine.Object.Instantiate(
                    sourceStage.gameObject,
                    overlay.rectTransform,
                    false);
                stageObject.name = "FailureResultStage_920x900";
                var stageRect = stageObject.GetComponent<RectTransform>();
                stageRect.anchorMin = stageRect.anchorMax = new Vector2(0.5f, 0.5f);
                stageRect.pivot = new Vector2(0.5f, 0.5f);
                stageRect.anchoredPosition = Vector2.zero;
                stageRect.sizeDelta = new Vector2(920f, 900f);
                stageRect.localScale = Vector3.one;

                var resultKicker = FindChild(stageRect, "ResultKicker")?.GetComponent<TMP_Text>();
                var titleText = FindChild(stageRect, "TitleText")?.GetComponent<TMP_Text>();
                var reasonText = FindChild(stageRect, "SummaryText")?.GetComponent<TMP_Text>();
                var rewardHeader = FindChild(stageRect, "RewardHeader");
                var rewardHeaderLabel = rewardHeader == null
                    ? null
                    : FindChild(rewardHeader, "Label")?.GetComponent<TMP_Text>();
                var continueHint = FindChild(stageRect, "ContinueHint")?.GetComponent<TMP_Text>();
                var retryButton = FindChild(stageRect, "ConfirmButton")?.GetComponent<Button>();
                if (resultKicker == null || titleText == null || reasonText == null ||
                    rewardHeaderLabel == null || continueHint == null || retryButton == null)
                {
                    throw new InvalidOperationException("공용 결과 스테이지의 필수 UI 구성이 불완전합니다.");
                }

                resultKicker.text = "군단의 역습 · 전투 결과";
                resultKicker.font = font;
                UnityEngine.Object.DestroyImmediate(titleText.gameObject);
                titleText = Text(
                    "TitleText",
                    stageRect,
                    "공략 실패",
                    new Vector2(0f, 356f),
                    new Vector2(520f, 84f),
                    50f,
                    TextAlignmentOptions.Center,
                    new Color32(255, 222, 214, 255),
                    FontStyles.Bold);
                rewardHeaderLabel.text = "실패 원인";
                rewardHeaderLabel.font = font;
                rewardHeaderLabel.color = new Color32(221, 151, 132, 255);
                continueHint.text = "같은 요새·같은 편성으로 비용 없이 재도전할 수 있습니다";
                continueHint.font = font;

                reasonText.text = "제한 시간 초과";
                reasonText.font = font;
                reasonText.fontSize = 30f;
                reasonText.color = new Color32(255, 168, 146, 255);
                reasonText.alignment = TextAlignmentOptions.Center;
                reasonText.rectTransform.anchoredPosition = new Vector2(0f, 0f);
                reasonText.rectTransform.sizeDelta = new Vector2(680f, 56f);
                rewardHeader.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 70f);

                var detailText = Text(
                    "FailureDetailText",
                    stageRect,
                    "현재 요새 공략에 실패했습니다.\n같은 성과 같은 편성으로 비용 없이 다시 도전할 수 있습니다.",
                    new Vector2(0f, -86f),
                    new Vector2(680f, 108f),
                    20f,
                    TextAlignmentOptions.Center,
                    new Color32(214, 219, 226, 255),
                    FontStyles.Normal,
                    true);
                detailText.font = font;

                foreach (var hiddenName in new[]
                         {
                             "VictoryIllustration",
                             "Confetti",
                             "VictoryLight",
                             "RewardSparkleLeft",
                             "RewardSparkleRight",
                             "RewardPackage",
                             "ClearBadge",
                             "PrimaryReward",
                             "RewardPageText"
                         })
                {
                    FindChild(stageRect, hiddenName)?.gameObject.SetActive(false);
                }

                var centerGlow = FindChild(stageRect, "CenterGlow")?.GetComponent<Graphic>();
                if (centerGlow != null)
                {
                    centerGlow.color = new Color32(132, 34, 30, 65);
                }

                var titleRibbon = FindChild(stageRect, "VictoryRibbon")?.GetComponent<Graphic>();
                if (titleRibbon != null)
                {
                    titleRibbon.color = new Color32(128, 65, 59, 255);
                }

                var resultSerialized = new SerializedObject(resultPresenter);
                var emptyStarSprite = resultSerialized.FindProperty("emptyStarSprite")?.objectReferenceValue as Sprite;
                if (emptyStarSprite == null)
                {
                    throw new InvalidOperationException("공용 결과창의 빈 별 Sprite 연결을 찾지 못했습니다.");
                }

                for (var starIndex = 1; starIndex <= 3; starIndex++)
                {
                    var starImage = FindChild(stageRect, $"Star_{starIndex}")?.GetComponent<UnityEngine.UI.Image>();
                    if (starImage == null)
                    {
                        throw new InvalidOperationException($"공용 결과창의 Star_{starIndex}를 찾지 못했습니다.");
                    }

                    starImage.sprite = emptyStarSprite;
                    starImage.color = Color.white;
                    starImage.gameObject.SetActive(true);
                }

                retryButton.gameObject.SetActive(true);
                retryButton.name = "FreeRetryButton";
                var retryRect = retryButton.GetComponent<RectTransform>();
                retryRect.anchoredPosition = new Vector2(-166f, -318f);
                retryRect.sizeDelta = new Vector2(300f, 76f);
                SetButtonLabel(retryButton, "무료 재도전");
                FindChild(retryRect, "Check")?.gameObject.SetActive(false);

                var leaveObject = UnityEngine.Object.Instantiate(retryButton.gameObject, stageRect);
                leaveObject.name = "LeaveButton";
                var leaveButton = leaveObject.GetComponent<Button>();
                var leaveRect = leaveButton.GetComponent<RectTransform>();
                leaveRect.anchoredPosition = new Vector2(166f, -318f);
                leaveRect.sizeDelta = new Vector2(300f, 76f);
                SetButtonLabel(leaveButton, "나가기");
                var leaveGraphic = leaveButton.targetGraphic as UnityEngine.UI.Image;
                if (leaveGraphic != null)
                {
                    leaveGraphic.color = new Color32(102, 57, 57, 255);
                }

                // 원본 리본보다 뒤에 있던 제목이 실패색 틴트에 가려지지 않도록 최상단에 둔다.
                titleText.transform.SetAsLastSibling();

                var itemCatalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalogPath);
                var dropCatalog = AssetDatabase.LoadAssetAtPath<WorldItemDropVisualCatalog>(ItemDropVisualCatalogPath);
                var equipmentBalance = AssetDatabase.LoadAssetAtPath<EquipmentBalanceConfig>(
                    EquipmentBalanceConfigPath);
                var equipmentDropCatalog = AssetDatabase.LoadAssetAtPath<EquipmentDropChestVisualCatalog>(
                    EquipmentDropVisualCatalogPath);
                if (itemCatalog == null || dropCatalog == null ||
                    equipmentBalance == null || equipmentDropCatalog == null)
                {
                    throw new InvalidOperationException("군단의 역습 아이템·장비 월드 드랍 카탈로그를 찾지 못했습니다.");
                }

                var view = rootObject.GetComponent<HexCastleBattleHudView>() ??
                           rootObject.AddComponent<HexCastleBattleHudView>();
                view.EditorConfigure(
                    timerText,
                    timerAccent,
                    overlay.gameObject,
                    reasonText,
                    detailText,
                    retryButton,
                    leaveButton,
                    itemCatalog,
                    dropCatalog,
                    equipmentBalance,
                    equipmentDropCatalog);
                overlay.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(rootObject, HudPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[CastleRaidUiSetupUtility] 공용 클리어 스타일 실패 결과창과 180초 HUD를 적용했습니다.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rootObject);
            }
        }

        private static GameObject BuildStageSelectionPage()
        {
            var root = Rect("PF_CastleRaidStageSelectionPage", null, Vector2.zero, new Vector2(1920f, 1080f));
            var blocker = Image("InputBlocker", root, Vector2.zero, Vector2.zero,
                null, new Color32(4, 7, 11, 162), true);
            Stretch(blocker.rectTransform);

            var shellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StandardMediumPath);
            if (shellPrefab == null)
            {
                throw new InvalidOperationException("공용 중형 백패널을 찾지 못했습니다.");
            }

            var shell = (GameObject)PrefabUtility.InstantiatePrefab(shellPrefab, root);
            shell.name = "MediumShell";
            var bindings = new StagePageBindings();
            BuildStagePageChrome(shell, bindings);
            BuildStageTowerList(FindNamed<RectTransform>(shell, "DetailArea"), bindings);
            BuildStageDetail(FindNamed<RectTransform>(shell, "NavigationArea"), bindings);
            root.gameObject.AddComponent<UIPanelPopAnimator>();

            var controller = root.gameObject.AddComponent<CastleRaidStageSelectionController>();
            controller.EditorConfigure(
                bindings.Close, bindings.Enter, bindings.EnterLabel, bindings.Scroll,
                bindings.StageButtons, bindings.StageNumbers, bindings.StageRewards,
                bindings.StageStates, bindings.ProgressLabel,
                bindings.ProgressFill, bindings.SelectedStage, bindings.SelectedFront,
                bindings.SelectedTheme, bindings.Reward, bindings.ClearState, false, 26, 27);

            PolishStageSelection(root.gameObject);
            var saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PagePrefabPath);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return saved;
        }

        private static void BuildStagePageChrome(GameObject shell, StagePageBindings bindings)
        {
            var title = FindNamed<TMP_Text>(shell, "TitleText");
            title.text = "군단의 역습";
            bindings.Close = FindNamed<Button>(shell, "CloseTouchArea_80x80");

            var navigation = FindNamed<RectTransform>(shell, "NavigationArea");
            var detail = FindNamed<RectTransform>(shell, "DetailArea");
            var divider = FindNamed<RectTransform>(shell, "AreaDivider_2px");
            navigation.anchorMin = Vector2.zero;
            navigation.anchorMax = new Vector2(StagePageSplit, 1f);
            navigation.offsetMin = Vector2.zero;
            navigation.offsetMax = Vector2.zero;
            detail.anchorMin = new Vector2(StagePageSplit, 0f);
            detail.anchorMax = Vector2.one;
            detail.offsetMin = Vector2.zero;
            detail.offsetMax = Vector2.zero;
            divider.anchorMin = new Vector2(StagePageSplit, 0f);
            divider.anchorMax = new Vector2(StagePageSplit, 1f);
            divider.anchoredPosition = Vector2.zero;
            divider.sizeDelta = new Vector2(2f, 0f);

            bindings.ProgressLabel = Text("ProgressLabel", navigation, "공략 진척도  026 / 100",
                new Vector2(0f, 300f), new Vector2(400f, 30f), 16f,
                TextAlignmentOptions.Center, new Color32(224, 228, 233, 255), FontStyles.Bold);
            var track = Image("ProgressTrack", navigation, new Vector2(0f, 272f),
                new Vector2(400f, 10f), null, new Color32(41, 48, 58, 255), false);
            bindings.ProgressFill = Image("ProgressFill", track.rectTransform, Vector2.zero,
                Vector2.zero, null, new Color32(210, 157, 59, 255), false);
            Stretch(bindings.ProgressFill.rectTransform);
            bindings.ProgressFill.rectTransform.anchorMax = new Vector2(0.26f, 1f);

            var footer = FindNamed<RectTransform>(shell, "FooterActionRoot");
            Text("FooterHint", footer,
                "1~100 연속 공략  ·  잠긴 단계도 보상 미리보기  ·  클리어 단계 재도전 가능",
                Vector2.zero, new Vector2(1050f, 34f), 15f,
                TextAlignmentOptions.Center, new Color32(169, 177, 187, 255));
        }

        private static void BuildStageTowerList(RectTransform detailArea, StagePageBindings bindings)
        {
            Text("ListTitle", detailArea, "요새 공략 목록", new Vector2(-170f, 319f),
                new Vector2(300f, 34f), 21f, TextAlignmentOptions.Left,
                Color.white, FontStyles.Bold);
            Text("ListHint", detailArea, "100 STAGES · 최초 보상",
                new Vector2(150f, 319f), new Vector2(300f, 28f), 14f,
                TextAlignmentOptions.Right, new Color32(157, 169, 183, 255));

            var viewportImage = Panel("StageViewport", detailArea, new Vector2(0f, -21f),
                new Vector2(660f, 638f), new Color32(12, 17, 23, 225), PanelLine);
            var viewport = viewportImage.rectTransform;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport, Vector2.zero, Vector2.zero);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(14f, 0f);
            content.offsetMax = new Vector2(-24f, 0f);

            bindings.StageButtons = new Button[CastleRaidStageRules.MaximumStage];
            bindings.StageNumbers = new TMP_Text[bindings.StageButtons.Length];
            bindings.StageRewards = new TMP_Text[bindings.StageButtons.Length];
            bindings.StageStates = new TMP_Text[bindings.StageButtons.Length];
            var cursor = 12f;
            for (var stage = CastleRaidStageRules.MinimumStage;
                 stage <= CastleRaidStageRules.MaximumStage;
                 stage++)
            {
                if ((stage - 1) % CastleRaidStageRules.StagesPerDifficulty == 0)
                {
                    var difficulty = CastleRaidStageRules.ResolveDifficulty(stage);
                    var band = Image($"FrontBand_{difficulty:00}", content,
                        new Vector2(0f, -cursor - 17f), new Vector2(604f, 34f),
                        Sprite(TitleBg), new Color32(47, 59, 75, 255), false,
                        new Vector2(0.5f, 1f));
                    Text("Label", band.rectTransform,
                        $"전선 {stage:000} - {stage + 9:000}",
                        new Vector2(-175f, 0f), new Vector2(230f, 26f), 15f,
                        TextAlignmentOptions.Left, new Color32(231, 202, 138, 255), FontStyles.Bold);
                    Text("BandReward", band.rectTransform,
                        $"최고 보상  다이아 {CastleRaidStageRules.ResolveDiamondReward(stage + 9):N0}  ·  " +
                        $"소환권 {CastleRaidStageRules.ResolveMonsterSummonTicketReward(stage + 9):N0}",
                        new Vector2(125f, 0f), new Vector2(340f, 24f), 12f,
                        TextAlignmentOptions.Right, new Color32(171, 181, 193, 255));
                    cursor += 42f;
                }

                var row = Button($"StageButton_{stage:000}", content,
                    new Vector2(0f, -cursor - 28f), new Vector2(604f, 56f),
                    new Color32(31, 37, 46, 255), new Vector2(0.5f, 1f));
                row.image.sprite = Sprite(CardBg);
                row.image.type = UnityEngine.UI.Image.Type.Sliced;
                bindings.StageButtons[stage - 1] = row;
                bindings.StageNumbers[stage - 1] = Text("StageNumber", row.transform,
                    $"STAGE {stage:000}", new Vector2(-230f, 0f), new Vector2(130f, 30f),
                    17f, TextAlignmentOptions.Left, Color.white, FontStyles.Bold);
                Image("StageDivider", row.transform, new Vector2(-154f, 0f),
                    new Vector2(2f, 32f), null, new Color32(83, 94, 108, 190), false);
                Icon("DiamondIcon", row.transform, diamondRewardIcon, new Vector2(-132f, 0f),
                    new Vector2(24f, 24f), Color.white);
                Icon("TicketIcon", row.transform, summonTicketRewardIcon, new Vector2(30f, 0f),
                    new Vector2(24f, 24f), Color.white);
                bindings.StageRewards[stage - 1] = Text("Reward", row.transform,
                    $"<pos=0>다이아 {CastleRaidStageRules.ResolveDiamondReward(stage):N0}" +
                    $"<pos=165>소환권 {CastleRaidStageRules.ResolveMonsterSummonTicketReward(stage):N0}",
                    new Vector2(45f, 0f), new Vector2(320f, 28f), 13f,
                    TextAlignmentOptions.Left, new Color32(255, 224, 151, 255));
                Panel("StatePlate", row.transform, new Vector2(263f, 0f),
                    new Vector2(70f, 32f), new Color32(17, 22, 28, 185), CardInner);
                bindings.StageStates[stage - 1] = Text("State", row.transform, "잠김",
                    new Vector2(263f, 0f), new Vector2(70f, 28f), 14f,
                    TextAlignmentOptions.Center, new Color32(145, 151, 160, 255), FontStyles.Bold);
                cursor += 62f;
            }
            content.sizeDelta = new Vector2(0f, cursor + 14f);

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 40f;
            bindings.Scroll = scroll;

            var scrollbarTrack = Image("Scrollbar", viewport, new Vector2(321f, 0f),
                new Vector2(8f, 606f), null, new Color32(39, 46, 56, 255), false);
            var handle = Image("Handle", scrollbarTrack.rectTransform, Vector2.zero, Vector2.zero,
                null, new Color32(201, 151, 59, 255), true);
            Stretch(handle.rectTransform, 1f);
            var scrollbar = scrollbarTrack.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        private static void BuildStageDetail(RectTransform navigationArea, StagePageBindings bindings)
        {
            var fortress = Image("FortressIcon", navigationArea, new Vector2(0f, 202f),
                new Vector2(88f, 88f), Sprite(FortressIcon),
                new Color32(224, 200, 151, 255), false);
            fortress.preserveAspect = true;
            Text("SelectedCaption", navigationArea, "선택 스테이지",
                new Vector2(0f, 145f), new Vector2(400f, 24f), 13f,
                TextAlignmentOptions.Center, new Color32(151, 162, 174, 255), FontStyles.Bold);
            bindings.SelectedStage = Text("SelectedStage", navigationArea, "STAGE 027",
                new Vector2(0f, 111f), new Vector2(400f, 40f), 30f,
                TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
            bindings.SelectedFront = Text("SelectedFront", navigationArea,
                "전선 021-030", new Vector2(0f, 75f), new Vector2(400f, 26f),
                16f, TextAlignmentOptions.Center, new Color32(230, 196, 125, 255), FontStyles.Bold);
            bindings.SelectedTheme = Text("SelectedTheme", navigationArea,
                "절차 요새 09 · 고유 전장", new Vector2(0f, 46f),
                new Vector2(400f, 24f), 13f, TextAlignmentOptions.Center,
                new Color32(168, 179, 191, 255));

            var rewardPanel = Panel("FirstClearReward", navigationArea, new Vector2(0f, -29f),
                new Vector2(400f, 116f), new Color32(38, 47, 59, 245), PanelLine);
            Text("RewardTitle", rewardPanel.rectTransform, "최초 클리어 보상", new Vector2(0f, 38f),
                new Vector2(365f, 24f), 14f, TextAlignmentOptions.Center,
                new Color32(229, 199, 132, 255), FontStyles.Bold);
            Icon("Diamond", rewardPanel.rectTransform, diamondRewardIcon, new Vector2(-142f, 5f),
                new Vector2(31f, 31f), Color.white);
            Icon("Ticket", rewardPanel.rectTransform, summonTicketRewardIcon, new Vector2(-142f, -30f),
                new Vector2(31f, 31f), Color.white);
            bindings.Reward = Text("RewardValue", rewardPanel.rectTransform,
                "다이아 4,200\n소환권 10", new Vector2(40f, -13f),
                new Vector2(280f, 70f), 15f, TextAlignmentOptions.Left,
                Color.white, FontStyles.Bold, true);

            bindings.ClearState = Text("ClearState", navigationArea,
                "신규 도전 · 최초 보상 획득 가능", new Vector2(0f, -112f),
                new Vector2(400f, 42f), 13f, TextAlignmentOptions.Center,
                new Color32(255, 226, 153, 255), FontStyles.Bold, true);
            bindings.Enter = Button("EnterButton", navigationArea, new Vector2(0f, -174f),
                new Vector2(390f, 58f), new Color32(192, 132, 45, 255));
            Icon("BattleIcon", bindings.Enter.transform, Sprite(BattleIcon), new Vector2(-120f, 0f),
                new Vector2(30f, 30f), Color.white);
            bindings.EnterLabel = Text("Label", bindings.Enter.transform, "공략 시작",
                new Vector2(30f, 0f), new Vector2(240f, 34f), 19f,
                TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
            Text("EntryHint", navigationArea,
                "현재 도전 가능 단계가 자동 선택됩니다.",
                new Vector2(0f, -221f), new Vector2(400f, 30f), 12f,
                TextAlignmentOptions.Center, new Color32(139, 151, 165, 255));
        }

        private static GameObject BuildBattleHud()
        {
            var rootObject = new GameObject(
                "PF_CastleRaidHexHUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var root = rootObject.GetComponent<RectTransform>();
            var canvas = rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = rootObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var input = Image("DeploymentInputSurface", root, Vector2.zero, Vector2.zero,
                null, Color.clear, true);
            Stretch(input.rectTransform);
            input.gameObject.AddComponent<HexCastleDeploymentInputSurface>();
            Image("HudVignette", root, new Vector2(0f, -490f), new Vector2(1920f, 200f),
                null, new Color32(5, 8, 12, 160), false);

            var stageCard = Panel("StageCard", root, new Vector2(236f, -78f),
                new Vector2(420f, 126f), new Color32(19, 27, 37, 238), CardBg,
                new Vector2(0f, 1f));
            Icon("Fortress", stageCard.rectTransform, Sprite(FortressIcon),
                new Vector2(-163f, 17f), new Vector2(62f, 62f),
                new Color32(238, 203, 128, 255));
            Text("DeploymentText", stageCard.rectTransform, "배치 0 / 10",
                new Vector2(30f, 28f), new Vector2(285f, 34f), 23f,
                TextAlignmentOptions.Left, Color.white, FontStyles.Bold);
            Text("CastleInfoText", stageCard.rectTransform,
                "STAGE 027 · 난이도 3 · 절차 요새", new Vector2(30f, -20f),
                new Vector2(285f, 46f), 14f, TextAlignmentOptions.Left,
                new Color32(190, 199, 210, 255), FontStyles.Normal, true);

            var statusPanel = Panel("StatusPanel", root, new Vector2(0f, -51f),
                new Vector2(730f, 64f), new Color32(20, 29, 40, 225), PanelLine,
                new Vector2(0.5f, 1f));
            Text("StatusText", statusPanel.rectTransform,
                "몬스터를 선택한 뒤 외곽 육각 칸에 배치하세요", Vector2.zero,
                new Vector2(680f, 38f), 20f, TextAlignmentOptions.Center,
                new Color32(240, 242, 245, 255), FontStyles.Bold);

            var rotateLeft = Button("RotateCameraLeftButton", root, new Vector2(-205f, -78f),
                new Vector2(64f, 64f), new Color32(40, 52, 67, 242), new Vector2(1f, 1f));
            Icon("Icon", rotateLeft.transform, Sprite(ArrowLeftIcon), Vector2.zero,
                new Vector2(32f, 32f), Color.white);
            rotateLeft.gameObject.AddComponent<HexCastleCameraHoldButton>();
            var rotateRight = Button("RotateCameraRightButton", root, new Vector2(-133f, -78f),
                new Vector2(64f, 64f), new Color32(40, 52, 67, 242), new Vector2(1f, 1f));
            Icon("Icon", rotateRight.transform, Sprite(ArrowRightIcon), Vector2.zero,
                new Vector2(32f, 32f), Color.white);
            rotateRight.gameObject.AddComponent<HexCastleCameraHoldButton>();
            var exit = Button("ExitButton", root, new Vector2(-57f, -78f),
                new Vector2(68f, 64f), new Color32(100, 51, 51, 245), new Vector2(1f, 1f));
            Icon("Icon", exit.transform, Sprite(ExitIcon), Vector2.zero,
                new Vector2(32f, 32f), Color.white);

            var generation = Panel("GenerationControls", root, new Vector2(0f, -125f),
                new Vector2(790f, 48f), new Color32(17, 23, 31, 218), PanelLine,
                new Vector2(0.5f, 1f));
            for (var index = 0; index < CastleRaidStageRules.MaximumDifficulty; index++)
            {
                var button = Button($"Difficulty{index + 1}Button", generation.rectTransform,
                    new Vector2(-342f + index * 54f, 0f), new Vector2(48f, 34f),
                    new Color32(51, 64, 80, 255));
                Text("Label", button.transform, $"D{index + 1}", Vector2.zero,
                    new Vector2(44f, 25f), 13f, TextAlignmentOptions.Center,
                    Color.white, FontStyles.Bold);
            }
            var regenerate = Button("RegenerateCastleButton", generation.rectTransform,
                new Vector2(270f, 0f), new Vector2(170f, 36f), new Color32(132, 92, 37, 255));
            Text("Label", regenerate.transform, "DEV 요새 재생성", Vector2.zero,
                new Vector2(160f, 25f), 13f, TextAlignmentOptions.Center,
                Color.white, FontStyles.Bold);

            var dock = Panel("BottomDeploymentDock", root, new Vector2(0f, 112f),
                new Vector2(1710f, 194f), new Color32(15, 22, 31, 242), CardBg,
                new Vector2(0.5f, 0f));
            Panel("DockInner", dock.rectTransform, Vector2.zero, new Vector2(1672f, 156f),
                new Color32(68, 85, 105, 210), CardInner).raycastTarget = false;
            Text("DockTitle", dock.rectTransform, "공격 부대 배치", new Vector2(-715f, 73f),
                new Vector2(220f, 28f), 17f, TextAlignmentOptions.Left,
                new Color32(231, 202, 138, 255), FontStyles.Bold);
            Text("DockHint", dock.rectTransform, "부대를 선택한 뒤 전장 외곽의 육각 칸을 누르세요",
                new Vector2(-375f, 73f), new Vector2(450f, 26f), 13f,
                TextAlignmentOptions.Left, new Color32(143, 156, 171, 255));

            for (var index = 0; index < 10; index++)
            {
                var unit = Button($"UnitButton_{index + 1}", dock.rectTransform,
                    new Vector2(-738f + index * 164f, -18f), new Vector2(154f, 104f),
                    new Color32(37, 50, 65, 255));
                Text("Label", unit.transform, $"부대 {index + 1}\n1", new Vector2(0f, 17f),
                    new Vector2(140f, 56f), 17f, TextAlignmentOptions.Center,
                    Color.white, FontStyles.Bold, true);
                var aiTag = Button("AITag", unit.transform, new Vector2(0f, -34f),
                    new Vector2(124f, 25f), new Color32(28, 89, 102, 255));
                Text("Label", aiTag.transform, "돌격", Vector2.zero, new Vector2(116f, 20f),
                    11f, TextAlignmentOptions.Center, new Color32(181, 240, 246, 255),
                    FontStyles.Bold);
            }

            var aiPanel = Panel("AIProfileDescriptionPanel", root, new Vector2(0f, 247f),
                new Vector2(720f, 104f), new Color32(18, 29, 39, 248), PanelLine,
                new Vector2(0.5f, 0f));
            aiPanel.gameObject.AddComponent<Button>().targetGraphic = aiPanel;
            Text("DescriptionText", aiPanel.rectTransform,
                "<b>부대 · 돌격</b>\n가장 가까운 성벽을 빠르게 돌파합니다.  ·  눌러서 닫기",
                Vector2.zero, new Vector2(670f, 72f), 15f, TextAlignmentOptions.Center,
                new Color32(224, 231, 238, 255), FontStyles.Normal, true);
            aiPanel.gameObject.SetActive(false);

            var saved = PrefabUtility.SaveAsPrefabAsset(rootObject, HudPrefabPath);
            UnityEngine.Object.DestroyImmediate(rootObject);
            return saved;
        }

        private static void ConnectCastleRaidScene(GameObject hudPrefab)
        {
            var scene = EditorSceneManager.OpenScene(CastleRaidScenePath, OpenSceneMode.Single);
            var controller = FindInScene<HexCastleRaidController>(scene);
            if (controller == null)
            {
                throw new InvalidOperationException("03_CastleRaidHex의 Controller를 찾지 못했습니다.");
            }

            var serialized = new SerializedObject(controller);
            var rules = Ref<HexCastleThemeOneRules>(serialized, "themeRules");
            var visualSet = Ref<HexCastleVisualSet>(serialized, "visualSet");
            var attackCatalog = Ref<HexCastleTurretAttackCatalog>(serialized, "turretAttackCatalog");
            var anchor = Ref<Transform>(serialized, "stageAnchor");
            var worldCamera = Ref<Camera>(serialized, "deploymentCamera");
            var cameraController = Ref<HexCastleCameraController>(serialized, "cameraController");
            var pool = Ref<ScenePoolScope>(serialized, "poolScope");
            var sfx = Ref<SfxPool>(serialized, "sfxPool");
            var feedback = Ref<CombatFeedbackPlayer>(serialized, "combatFeedback");
            var difficulty = serialized.FindProperty("difficultyLevel").intValue;
            var seed = serialized.FindProperty("generationSeed").intValue;

            var sceneHuds = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                .Select(value => value.gameObject)
                .Where(value =>
                    value.name == "PF_CastleRaidHUD" ||
                    value.name.StartsWith(
                        "PF_CastleRaidHexHUD",
                        StringComparison.Ordinal))
                .Distinct()
                .ToArray();
            var legacyHuds = sceneHuds
                .Where(value => value.name == "PF_CastleRaidHUD")
                .ToArray();
            var currentHuds = sceneHuds
                .Where(value => value.name.StartsWith(
                    "PF_CastleRaidHexHUD",
                    StringComparison.Ordinal))
                .ToArray();
            var placementSource = legacyHuds.FirstOrDefault() ?? currentHuds.FirstOrDefault();
            var parent = placementSource != null ? placementSource.transform.parent : null;
            var sibling = placementSource != null
                ? placementSource.transform.GetSiblingIndex()
                : scene.rootCount;
            foreach (var legacyHud in legacyHuds)
            {
                legacyHud.SetActive(false); // 구형 HUD는 비교·복구용으로 보존
            }

            foreach (var currentHud in currentHuds)
            {
                UnityEngine.Object.DestroyImmediate(currentHud);
            }

            var hud = parent != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, parent)
                : (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, scene);
            hud.name = "PF_CastleRaidHexHUD";
            var maximumSibling = hud.transform.parent == null
                ? scene.rootCount - 1
                : hud.transform.parent.childCount - 1;
            hud.transform.SetSiblingIndex(Mathf.Clamp(sibling, 0, maximumSibling));

            var unitButtons = Enumerable.Range(1, 10)
                .Select(index => FindNamed<Button>(hud, $"UnitButton_{index}"))
                .ToArray();
            var unitLabels = unitButtons
                .Select(button => button.transform.Find("Label").GetComponent<TMP_Text>())
                .ToArray();
            var aiButtons = unitButtons
                .Select(button => button.transform.Find("AITag").GetComponent<Button>())
                .ToArray();
            var aiLabels = aiButtons
                .Select(button => button.transform.Find("Label").GetComponent<TMP_Text>())
                .ToArray();
            var difficultyButtons = Enumerable.Range(1, 10)
                .Select(index => FindNamed<Button>(hud, $"Difficulty{index}Button"))
                .ToArray();
            var descriptionPanel = FindNamed<Transform>(hud, "AIProfileDescriptionPanel").gameObject;
            var surface = FindNamed<HexCastleDeploymentInputSurface>(hud, "DeploymentInputSurface");
            var rotateLeft = FindNamed<Button>(hud, "RotateCameraLeftButton");
            var rotateRight = FindNamed<Button>(hud, "RotateCameraRightButton");
            surface.EditorConfigure(controller, cameraController);
            rotateLeft.GetComponent<HexCastleCameraHoldButton>().EditorConfigure(cameraController, -1);
            rotateRight.GetComponent<HexCastleCameraHoldButton>().EditorConfigure(cameraController, 1);

            controller.EditorConfigure(
                rules, visualSet, attackCatalog, anchor, worldCamera, cameraController,
                pool, sfx, feedback,
                FindNamed<TMP_Text>(hud, "DeploymentText"),
                FindNamed<TMP_Text>(hud, "StatusText"),
                FindNamed<TMP_Text>(hud, "CastleInfoText"),
                unitButtons, unitLabels, aiButtons, aiLabels,
                descriptionPanel, FindNamed<TMP_Text>(hud, "DescriptionText"),
                difficultyButtons, FindNamed<Button>(hud, "RegenerateCastleButton"),
                rotateLeft, rotateRight, FindNamed<Button>(hud, "ExitButton"),
                surface, difficulty, seed);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConnectMainBattleScene(GameObject pagePrefab)
        {
            var scene = EditorSceneManager.OpenScene(MainBattleScenePath, OpenSceneMode.Single);
            var hud = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(value => value.name == "MainBattleHUD");
            if (hud == null)
            {
                throw new InvalidOperationException("01_MainBattle의 MainBattleHUD를 찾지 못했습니다.");
            }

            foreach (var existing in hud.Cast<Transform>()
                         .Where(value => value.name.StartsWith(
                             "PF_CastleRaidStageSelectionPage", StringComparison.Ordinal))
                         .Select(value => value.gameObject)
                         .ToArray())
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }

            var page = (GameObject)PrefabUtility.InstantiatePrefab(pagePrefab, hud);
            page.name = "PF_CastleRaidStageSelectionPage";
            Stretch(page.GetComponent<RectTransform>());
            page.SetActive(false);
            page.transform.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConnectDevUiScene(GameObject pagePrefab)
        {
            var scene = EditorSceneManager.OpenScene(DevUiScenePath, OpenSceneMode.Single);
            var windowsRoot = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(value => value.name == "RuntimeWindowsRoot_EditorOnly");
            if (windowsRoot == null)
            {
                throw new InvalidOperationException(
                    "DEV_UIManagement의 RuntimeWindowsRoot_EditorOnly를 찾지 못했습니다.");
            }

            foreach (var existing in windowsRoot.Cast<Transform>()
                         .Where(value => value.name == "Slot_PF_CastleRaidStageSelectionPage")
                         .Select(value => value.gameObject)
                         .ToArray())
            {
                UnityEngine.Object.DestroyImmediate(existing);
            }

            var slot = Rect("Slot_PF_CastleRaidStageSelectionPage", windowsRoot,
                new Vector2(-3060f, -7900f), new Vector2(1920f, 1080f));
            var slotImage = slot.gameObject.AddComponent<UnityEngine.UI.Image>();
            slotImage.color = new Color32(8, 12, 18, 255);
            slotImage.raycastTarget = false;
            var page = (GameObject)PrefabUtility.InstantiatePrefab(pagePrefab, slot);
            page.name = "PF_CastleRaidStageSelectionPage_REFERENCE";
            Stretch(page.GetComponent<RectTransform>());
            page.SetActive(true);
            page.GetComponent<CastleRaidStageSelectionController>().EditorSetPreview(26, 27);
            slot.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static TMP_FontAsset ResolveFont()
        {
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                       "Assets/ProjectMT/05_Art/Fonts/FontAssets/TMP_SpoqaHanSansNeo_Body.asset") ??
                   TMP_Settings.defaultFontAsset;
        }

        private static Transform FindChild(Transform root, string name)
        {
            return root == null
                ? null
                : root.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(value => value.name == name);
        }

        private static void RemoveOwnedChild(Transform root, string name)
        {
            var child = FindChild(root, name);
            if (child != null)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void SetButtonLabel(Button button, string value)
        {
            var label = button == null
                ? null
                : button.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.name == "Label");
            if (label == null)
            {
                throw new InvalidOperationException($"표준 버튼 Label을 찾지 못했습니다: {button?.name}");
            }

            label.font = font;
            label.text = value;
            label.fontSize = 17f;
        }

        private static RectTransform Rect(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Vector2? anchor = null)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            if (parent != null)
            {
                rect.SetParent(parent, false);
            }
            var resolvedAnchor = anchor ?? new Vector2(0.5f, 0.5f);
            rect.anchorMin = resolvedAnchor;
            rect.anchorMax = resolvedAnchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            rect.localScale = Vector3.one;
        }

        private static UnityEngine.UI.Image Image(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Sprite sprite,
            Color color,
            bool raycast,
            Vector2? anchor = null)
        {
            var rect = Rect(name, parent, position, size, anchor);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            image.type = sprite != null
                ? UnityEngine.UI.Image.Type.Sliced
                : UnityEngine.UI.Image.Type.Simple;
            return image;
        }

        private static UnityEngine.UI.Image Panel(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color,
            string spritePath,
            Vector2? anchor = null)
        {
            return Image(name, parent, position, size, Sprite(spritePath), color, false, anchor);
        }

        private static Button Button(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color,
            Vector2? anchor = null)
        {
            var image = Image(name, parent, position, size, Sprite(ButtonBg), color, true, anchor);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.42f, 0.42f, 0.42f, 0.82f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            return button;
        }

        private static TMP_Text Text(
            string name,
            Transform parent,
            string value,
            Vector2 position,
            Vector2 size,
            float fontSize,
            TextAlignmentOptions alignment,
            Color color,
            FontStyles style = FontStyles.Normal,
            bool wrap = false)
        {
            var rect = Rect(name, parent, position, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.text = value;
            text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        private static UnityEngine.UI.Image Icon(
            string name,
            Transform parent,
            Sprite sprite,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            var image = Image(name, parent, position, size, sprite, color, false);
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = true;
            return image;
        }

        private static Sprite Sprite(string path)
        {
            var result = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (result == null)
            {
                throw new InvalidOperationException($"GUI Pro Sprite를 찾지 못했습니다: {path}");
            }
            return result;
        }

        private static Sprite ItemIcon(string definitionPath)
        {
            var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(definitionPath);
            if (definition == null || definition.Icon == null)
            {
                throw new InvalidOperationException($"인벤토리 아이템 아이콘을 찾지 못했습니다: {definitionPath}");
            }
            return definition.Icon;
        }

        private static T Ref<T>(SerializedObject serializedObject, string propertyName)
            where T : UnityEngine.Object
        {
            return serializedObject.FindProperty(propertyName)?.objectReferenceValue as T;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .FirstOrDefault();
        }

        private static T FindNamed<T>(GameObject root, string name) where T : Component
        {
            var match = root.GetComponentsInChildren<T>(true)
                .FirstOrDefault(value => value.name == name);
            if (match == null)
            {
                throw new InvalidOperationException(
                    $"{root.name}에서 {name} ({typeof(T).Name})을 찾지 못했습니다.");
            }
            return match;
        }
    }
}
