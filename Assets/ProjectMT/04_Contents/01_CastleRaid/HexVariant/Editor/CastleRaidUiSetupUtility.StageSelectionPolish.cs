using System;
using System.Linq;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.GameData;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    public static partial class CastleRaidUiSetupUtility
    {
        private const string StagePanelSkin = GuiRoot + "/Shared/Sprite_Common/Frame/PanelFrame/PanelFrame_03_White_";
        private const string StageRowFrame = GuiRoot + "/Shared/Sprite_Common/Frame/ListFrame/ListFrame_01~04_White_Border2_Px6.png";

        [MenuItem("Tools/ProjectMT/Castle Raid/Polish Stage Selection")]
        public static void ApplyStageSelectionPolish()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("PlayMode를 종료한 뒤 선택 화면을 적용하세요.");
            font = ResolveFont();
            var root = PrefabUtility.LoadPrefabContents(PagePrefabPath);
            try
            {
                PolishStageSelection(root);
                PrefabUtility.SaveAsPrefabAsset(root, PagePrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void PolishStageSelection(GameObject root)
        {
            font = ResolveFont();
            var navigation = FindNamed<RectTransform>(root, "NavigationArea");
            var detail = FindNamed<RectTransform>(root, "DetailArea");
            var controller = root.GetComponent<CastleRaidStageSelectionController>();
            var serialized = new SerializedObject(controller);
            var selected = serialized.FindProperty("previewSelectedStage").intValue;
            var highest = serialized.FindProperty("previewHighestClearedStage").intValue;
            RemoveOwnedChild(navigation, "StageDetailSkin");
            var backdrop = Panel("StageDetailSkin", navigation, Vector2.zero, new Vector2(424f, 688f),
                new Color32(35, 37, 43, 255), StagePanelSkin + "Bg.png");
            backdrop.transform.SetAsFirstSibling();
            ApplyHudPanelSkin(backdrop.rectTransform);

            StageText(root.transform, "ProgressLabel", new Vector2(0f, 307f), new Vector2(372f, 28f), 15f);
            var track = HudRect(root.transform, "ProgressTrack", new Vector2(0f, 276f), new Vector2(372f, 12f));
            track.GetComponent<Image>().sprite = GuiImage("Prefabs_Slider/Slider_01_Brown.prefab", "Bg").sprite;
            track.GetComponent<Image>().type = UnityEngine.UI.Image.Type.Sliced;
            var fill = FindChild(track, "ProgressFill").GetComponent<Image>();
            fill.color = new Color32(205, 163, 88, 255);

            var fortress = HudRect(navigation, "FortressIcon", new Vector2(0f, 180f), new Vector2(210f, 146f));
            fortress.GetComponent<Image>().sprite = Sprite(CastleArt);
            fortress.GetComponent<Image>().color = Color.white;
            fortress.GetComponent<Image>().type = UnityEngine.UI.Image.Type.Simple;
            StageText(navigation, "SelectedCaption", new Vector2(0f, 89f), new Vector2(360f, 24f), 13f);
            StageText(navigation, "SelectedStage", new Vector2(0f, 53f), new Vector2(372f, 44f), 32f);
            StageText(navigation, "SelectedFront", new Vector2(0f, 16f), new Vector2(372f, 28f), 16f);
            FindChild(navigation, "SelectedTheme").gameObject.SetActive(false);

            var rewards = HudRect(navigation, "FirstClearReward", new Vector2(0f, -135f), new Vector2(372f, 142f));
            rewards.GetComponent<Image>().enabled = false;
            StageText(rewards, "RewardTitle", new Vector2(0f, 97f), new Vector2(372f, 28f), 16f);
            FindChild(rewards, "RewardValue").gameObject.SetActive(false);
            var diamond = StageRewardTile(rewards, "DiamondTile", "Diamond", "다이아", -94f);
            var ticket = StageRewardTile(rewards, "TicketTile", "Ticket", "소환권", 94f);
            diamond.text = CastleRaidStageRules.ResolveDiamondReward(selected).ToString("N0");
            ticket.text = CastleRaidStageRules.ResolveMonsterSummonTicketReward(selected).ToString("N0");
            serialized.FindProperty("diamondValueLabel").objectReferenceValue = diamond;
            serialized.FindProperty("ticketValueLabel").objectReferenceValue = ticket;
            StageText(navigation, "ClearState", new Vector2(0f, -234f), new Vector2(372f, 34f), 13f);
            var enter = HudRect(navigation, "EnterButton", new Vector2(0f, -291f), new Vector2(372f, 60f));
            ApplyGuiSkin(enter, "Prefabs_Button/Button_02_Brown.prefab", "Bg", "Light", "HighLight");
            enter.GetComponent<Image>().color = new Color32(192, 132, 45, 255);
            var buttonSkin = FindChild(enter, "GuiSkin").gameObject.AddComponent<CanvasGroup>();
            buttonSkin.blocksRaycasts = false;
            buttonSkin.interactable = false;
            serialized.FindProperty("enterButtonSkin").objectReferenceValue = buttonSkin;
            HudRect(enter, "BattleIcon", new Vector2(-66f, 0f), new Vector2(30f, 30f));
            StageText(enter, "Label", new Vector2(25f, 0f), new Vector2(138f, 36f), 21f);
            FindChild(navigation, "EntryHint").gameObject.SetActive(false);

            StageText(detail, "ListTitle", new Vector2(-170f, 319f), new Vector2(300f, 34f), 23f);
            var hint = StageText(detail, "ListHint", new Vector2(182f, 319f), new Vector2(250f, 28f), 13f);
            hint.text = "100 STAGES · 최초 보상";
            RemoveOwnedChild(detail, "StageColumnHeaders");
            var headers = Rect("StageColumnHeaders", detail, new Vector2(-5f, 281f), new Vector2(604f, 24f));
            Text("NumberHeading", headers, "스테이지", new Vector2(-212f, 0f), new Vector2(144f, 24f), 12f,
                TextAlignmentOptions.Left, new Color32(156, 164, 175, 255));
            Text("RewardHeading", headers, "최초 클리어 보상", new Vector2(38f, 0f), new Vector2(245f, 24f), 12f,
                TextAlignmentOptions.Center, new Color32(156, 164, 175, 255));
            Text("StateHeading", headers, "상태", new Vector2(254f, 0f), new Vector2(72f, 24f), 12f,
                TextAlignmentOptions.Center, new Color32(156, 164, 175, 255));
            var viewport = HudRect(detail, "StageViewport", new Vector2(0f, -41f), new Vector2(660f, 604f));
            viewport.GetComponent<Image>().sprite = Sprite(StagePanelSkin + "Bg.png");
            viewport.GetComponent<Image>().color = new Color32(26, 28, 33, 255);
            var scrollbar = HudRect(viewport, "Scrollbar", new Vector2(321f, 0f), new Vector2(6f, 580f));
            var content = FindChild(viewport, "Content").GetComponent<RectTransform>();
            var frames = serialized.FindProperty("selectionFrames");
            var plates = serialized.FindProperty("statePlates");
            frames.arraySize = plates.arraySize = CastleRaidStageRules.MaximumStage;
            var cursor = 12f;
            for (var stage = 1; stage <= CastleRaidStageRules.MaximumStage; stage++)
            {
                if ((stage - 1) % 10 == 0)
                {
                    var band = HudRect(content, $"FrontBand_{(stage - 1) / 10 + 1:00}",
                        new Vector2(0f, -cursor - 18f), new Vector2(604f, 36f));
                    band.GetComponent<Image>().sprite = Sprite(StagePanelSkin + "Bg.png");
                    band.GetComponent<Image>().color = new Color32(49, 48, 47, 255);
                    StageText(band, "Label", new Vector2(-189f, 0f), new Vector2(202f, 26f), 14f);
                    StageText(band, "BandReward", new Vector2(105f, 0f), new Vector2(374f, 24f), 11f);
                    cursor += 44f;
                }
                var row = HudRect(content, $"StageButton_{stage:000}", new Vector2(0f, -cursor - 30f), new Vector2(604f, 60f));
                row.GetComponent<Image>().sprite = Sprite(StagePanelSkin + "Bg.png");
                row.GetComponent<Image>().pixelsPerUnitMultiplier = 2f;
                row.GetComponent<Image>().color = stage == selected ? new Color32(77, 62, 43, 255)
                    : stage <= highest ? new Color32(38, 51, 51, 255) : new Color32(39, 42, 48, 255);
                FindChild(row, "StageDivider").gameObject.SetActive(false);
                StageText(row, "StageNumber", new Vector2(-212f, 0f), new Vector2(144f, 30f), 17f);
                HudRect(row, "DiamondIcon", new Vector2(-111f, 0f), new Vector2(27f, 27f));
                HudRect(row, "TicketIcon", new Vector2(43f, 0f), new Vector2(27f, 27f));
                var value = StageText(row, "Reward", new Vector2(50f, 0f), new Vector2(286f, 30f), 16f);
                value.text = $"<pos=0>{CastleRaidStageRules.ResolveDiamondReward(stage):N0}" +
                    $"<pos=154>{CastleRaidStageRules.ResolveMonsterSummonTicketReward(stage):N0}";
                value.color = stage <= highest + 1 ? new Color32(241, 225, 191, 255) : new Color32(174, 177, 183, 255);
                var plate = HudRect(row, "StatePlate", new Vector2(254f, 0f), new Vector2(72f, 30f));
                plate.GetComponent<Image>().sprite = Sprite(StagePanelSkin + "Bg.png");
                plate.GetComponent<Image>().color = stage <= highest ? new Color32(40, 91, 83, 255)
                    : stage == highest + 1 ? new Color32(137, 98, 44, 255) : new Color32(48, 50, 55, 255);
                StageText(row, "State", new Vector2(254f, 0f), new Vector2(72f, 26f), 13f);
                RemoveOwnedChild(row, "SelectionFrame_GUIPro");
                var focus = Image("SelectionFrame_GUIPro", row, Vector2.zero, Vector2.zero,
                    Sprite(StageRowFrame), new Color32(218, 176, 100, 255), false);
                Stretch(focus.rectTransform);
                focus.pixelsPerUnitMultiplier = 2f;
                focus.enabled = stage == selected;
                frames.GetArrayElementAtIndex(stage - 1).objectReferenceValue = focus;
                plates.GetArrayElementAtIndex(stage - 1).objectReferenceValue = plate.GetComponent<Image>();
                cursor += 66f;
            }
            content.sizeDelta = new Vector2(0f, cursor + 12f);
            var footer = FindNamed<TMP_Text>(root, "FooterHint");
            footer.text = "잠긴 스테이지도 보상 확인 가능  ·  클리어한 스테이지는 재도전 가능";
            footer.fontSize = 14f;
            MatchStageReference(root, serialized);
            ApplyApprovedStageConceptA(root, serialized);
            serialized.FindProperty("polishedPresentation").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var previewEnabled = serialized.FindProperty("previewWithoutRuntime").boolValue;
            controller.EditorSetPreview(highest, selected);
            serialized.Update();
            serialized.FindProperty("previewWithoutRuntime").boolValue = previewEnabled;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void MatchStageReference(GameObject root, SerializedObject serialized)
        {
            var shell = FindNamed<RectTransform>(root, "MediumShell");
            var contentRoot = FindNamed<RectTransform>(root, "ContentRoot");
            contentRoot.offsetMin = new Vector2(44f, 82f);
            contentRoot.offsetMax = new Vector2(-44f, -138f);
            var navigation = FindNamed<RectTransform>(root, "NavigationArea");
            var detail = FindNamed<RectTransform>(root, "DetailArea");
            navigation.Find("FrameVisual_GUIPro").gameObject.SetActive(false);
            detail.Find("FrameVisual_GUIPro").gameObject.SetActive(false);
            FindChild(contentRoot, "AreaDivider_2px").gameObject.SetActive(false);
            var outer = shell.Find("FrameVisual_GUIPro");
            outer.Find("Bg").GetComponent<Image>().color = new Color32(30, 31, 35, 255);
            outer.Find("InnerBorder").GetComponent<Image>().color = new Color32(103, 91, 73, 255);
            outer.Find("TitleBg").GetComponent<Image>().color = new Color32(76, 74, 74, 255);
            var close = FindNamed<RectTransform>(root, "CloseTouchArea_80x80");
            close.anchoredPosition = new Vector2(-35f, -35f);
            close.sizeDelta = new Vector2(64f, 64f);
            var panel = HudRect(navigation, "StageDetailSkin", Vector2.zero, new Vector2(444f, 738f));
            ReferencePanel(panel, new Color32(27, 31, 37, 255), new Color32(105, 94, 76, 255));
            RemoveOwnedChild(detail, "StageListSkin");
            var listPanel = Panel("StageListSkin", detail, Vector2.zero, new Vector2(688f, 738f), Color.white, CardBg);
            listPanel.transform.SetAsFirstSibling();
            ReferencePanel(listPanel.rectTransform, new Color32(26, 29, 33, 255), new Color32(52, 58, 65, 255));

            var progress = FindNamed<TMP_Text>(root, "ProgressLabel");
            var track = FindNamed<RectTransform>(root, "ProgressTrack");
            progress.transform.SetParent(shell, false);
            progress.rectTransform.anchorMin = progress.rectTransform.anchorMax = new Vector2(0.5f,0.5f);
            progress.rectTransform.anchoredPosition = new Vector2(-502f, 373f);
            progress.rectTransform.sizeDelta = new Vector2(136f, 30f);
            progress.fontSize = 18f;
            progress.alignment = TextAlignmentOptions.Left;
            progress.text = "공략 진행도";
            track.SetParent(shell, false);
            track.anchorMin = track.anchorMax = new Vector2(0.5f,0.5f);
            track.anchoredPosition = new Vector2(126f,373f);
            track.sizeDelta = new Vector2(862f,12f);
            RemoveOwnedChild(shell, "ProgressCount");
            var count = Text("ProgressCount", shell, "026 / 100", new Vector2(-368f,373f),new Vector2(106f,30f),19f,
                TextAlignmentOptions.Left,new Color32(210,210,210,255));
            serialized.FindProperty("progressCountLabel").objectReferenceValue=count;

            RemoveOwnedChild(navigation,"FortressFrame_GUIPro");
            var fortressFrame=Panel("FortressFrame_GUIPro",navigation,new Vector2(0f,268f),new Vector2(142f,142f),Color.white,CardBg);
            fortressFrame.transform.SetSiblingIndex(1);
            ReferencePanel(fortressFrame.rectTransform,new Color32(30,35,43,255),new Color32(81,94,112,255));
            var fortress=HudRect(navigation,"FortressIcon",new Vector2(0f,268f),new Vector2(104f,104f));
            fortress.GetComponent<Image>().sprite=Sprite(FortressIcon);
            fortress.GetComponent<Image>().color=new Color32(225,196,127,255);
            StageText(navigation,"SelectedCaption",new Vector2(0f,179f),new Vector2(360f,26f),17f);
            StageText(navigation,"SelectedStage",new Vector2(0f,140f),new Vector2(400f,48f),38f).color=new Color32(248,233,201,255);
            StageText(navigation,"SelectedFront",new Vector2(0f,96f),new Vector2(200f,30f),19f);
            RemoveOwnedChild(navigation,"FrontPlate_GUIPro");
            var frontPlate=Panel("FrontPlate_GUIPro",navigation,new Vector2(0f,96f),new Vector2(174f,30f),Color.white,CardBg);
            frontPlate.transform.SetSiblingIndex(2);
            ReferencePanel(frontPlate.rectTransform,new Color32(37,35,31,255),new Color32(113,94,62,255));
            var theme=StageText(navigation,"SelectedTheme",new Vector2(0f,61f),new Vector2(372f,27f),17f);
            theme.gameObject.SetActive(true);
            var rewards=HudRect(navigation,"FirstClearReward",new Vector2(0f,-96f),new Vector2(372f,166f));
            StageText(rewards,"RewardTitle",new Vector2(0f,108f),new Vector2(372f,30f),20f);
            foreach(var tileName in new[]{"DiamondTile","TicketTile"})
            {
                var tile=HudRect(rewards,tileName,new Vector2(tileName=="DiamondTile"?-80f:80f,0f),new Vector2(150f,166f));
                ReferencePanel(tile,new Color32(27,31,37,255),new Color32(105,92,68,255));
                var icon=tile.GetComponentsInChildren<Image>(true).Single(x=>x.name=="Diamond"||x.name=="Ticket");
                icon.rectTransform.anchoredPosition=new Vector2(0f,36f);
                icon.rectTransform.sizeDelta=new Vector2(56f,56f);
                StageText(tile,"Quantity",new Vector2(0f,-28f),new Vector2(134f,40f),31f).color=new Color32(247,232,199,255);
                StageText(tile,"CurrencyName",new Vector2(0f,-62f),new Vector2(134f,26f),17f);
            }
            StageText(navigation,"ClearState",new Vector2(0f,-213f),new Vector2(400f,32f),17f).fontStyle=FontStyles.Normal;
            var enter=HudRect(navigation,"EnterButton",new Vector2(0f,-286f),new Vector2(390f,68f));
            enter.GetComponent<Image>().sprite=Sprite(GuiRoot+"/Shared/Sprite_Common/Frame/ListFrame/ListFrame_06_White_Bg.png");
            enter.GetComponent<Image>().pixelsPerUnitMultiplier=2f;
            var buttonSkin=enter.Find("GuiSkin");
            var light=buttonSkin.Find("Light").GetComponent<Image>();
            light.color=new Color32(202,155,69,145);
            var outline=Image("Outline",buttonSkin,Vector2.zero,Vector2.zero,
                Sprite(GuiRoot+"/Shared/Sprite_Common/Frame/ListFrame/ListFrame_06_White_Border.png"),new Color32(225,183,102,255),false);
            Stretch(outline.rectTransform);
            outline.pixelsPerUnitMultiplier=2f;
            HudRect(enter,"BattleIcon",new Vector2(-82f,0f),new Vector2(45f,45f));
            StageText(enter,"Label",new Vector2(28f,0f),new Vector2(172f,42f),26f);
            var entryHint=StageText(navigation,"EntryHint",new Vector2(0f,-344f),new Vector2(412f,26f),15f);
            entryHint.text="현재 도전 가능 단계가 자동 선택됩니다";
            entryHint.gameObject.SetActive(true);

            StageText(detail,"ListTitle",new Vector2(-170f,337f),new Vector2(300f,36f),25f).color=new Color32(248,233,201,255);
            StageText(detail,"ListHint",new Vector2(194f,337f),new Vector2(250f,28f),16f).text="100 STAGES";
            RemoveOwnedChild(detail,"VisibleFront_GUIPro");
            var band=Panel("VisibleFront_GUIPro",detail,new Vector2(0f,286f),new Vector2(664f,44f),Color.white,CardBg);
            ReferencePanel(band.rectTransform,new Color32(33,38,47,255),new Color32(66,74,86,255));
            var bandText=Text("VisibleFrontLabel",band.transform,"전선 001–010",new Vector2(-195f,0f),new Vector2(238f,32f),20f,
                TextAlignmentOptions.Left,new Color32(240,223,191,255),FontStyles.Bold);
            serialized.FindProperty("visibleFrontLabel").objectReferenceValue=bandText;
            var headers=HudRect(detail,"StageColumnHeaders",new Vector2(-2f,244f),new Vector2(644f,26f));
            StageText(headers,"NumberHeading",new Vector2(-200f,0f),new Vector2(160f,24f),15f);
            StageText(headers,"RewardHeading",new Vector2(43f,0f),new Vector2(260f,24f),15f);
            StageText(headers,"StateHeading",new Vector2(270f,0f),new Vector2(82f,24f),15f);
            var viewport=HudRect(detail,"StageViewport",new Vector2(0f,-46f),new Vector2(676f,556f));
            viewport.GetComponent<Image>().color=Color.clear;
            HudRect(viewport,"Scrollbar",new Vector2(334f,0f),new Vector2(5f,552f));
            var content=FindChild(viewport,"Content").GetComponent<RectTransform>();
            content.offsetMin=new Vector2(8f,content.offsetMin.y);
            content.offsetMax=new Vector2(-12f,content.offsetMax.y);
            var statusIcons=serialized.FindProperty("rowStatusIcons");
            statusIcons.arraySize=100;
            var clearedIcon=Sprite(GuiRoot+"/Shared/Icons/PictoIcon/128/check_round.png");
            var lockedIcon=Sprite(GuiRoot+"/Shared/Icons/PictoIcon/128/lock.png");
            var challengeIcon=Sprite(GuiRoot+"/Shared/Sprite_Common/Button/Button_Circle_01_White_Bg.png");
            serialized.FindProperty("clearedStatusIcon").objectReferenceValue=clearedIcon;
            serialized.FindProperty("lockedStatusIcon").objectReferenceValue=lockedIcon;
            serialized.FindProperty("challengeStatusIcon").objectReferenceValue=challengeIcon;
            float cursor=0f;
            for(int stage=1;stage<=100;stage++)
            {
                if((stage-1)%10==0)
                {
                    var front=HudRect(content,$"FrontBand_{(stage-1)/10+1:00}",new Vector2(0f,-cursor-18f),new Vector2(644f,36f));
                    front.gameObject.SetActive(stage!=1);
                    front.GetComponent<Image>().color=new Color32(35,40,47,255);
                    StageText(front,"Label",new Vector2(-199f,0f),new Vector2(224f,28f),17f);
                    var bandReward=StageText(front,"BandReward",new Vector2(160f,0f),new Vector2(300f,26f),13f);
                    bandReward.alignment=TextAlignmentOptions.Left;
                    bandReward.text=$"최고 보상<pos=116>{CastleRaidStageRules.ResolveDiamondReward(stage+9):N0}<pos=246>{CastleRaidStageRules.ResolveMonsterSummonTicketReward(stage+9):N0}";
                    RemoveOwnedChild(front,"BandDiamond");
                    RemoveOwnedChild(front,"BandTicket");
                    Icon("BandDiamond",front,ItemIcon(DiamondItemDefinitionPath),new Vector2(104f,0f),new Vector2(24f,24f),Color.white);
                    Icon("BandTicket",front,ItemIcon(SummonTicketItemDefinitionPath),new Vector2(236f,0f),new Vector2(24f,24f),Color.white);
                    if(stage!=1) cursor+=44f;
                }
                var row=HudRect(content,$"StageButton_{stage:000}",new Vector2(0f,-cursor-29f),new Vector2(644f,58f));
                StageText(row,"StageNumber",new Vector2(-200f,0f),new Vector2(160f,30f),18f);
                HudRect(row,"DiamondIcon",new Vector2(-58f,0f),new Vector2(27f,27f));
                HudRect(row,"TicketIcon",new Vector2(80f,0f),new Vector2(27f,27f));
                StageText(row,"Reward",new Vector2(79f,0f),new Vector2(230f,30f),18f);
                var plate=HudRect(row,"StatePlate",new Vector2(270f,0f),new Vector2(82f,31f));
                plate.GetComponent<Image>().sprite=Sprite(GuiRoot+"/Shared/Sprite_Common/Frame/ListFrame/ListFrame_06_White_Bg.png");
                plate.GetComponent<Image>().pixelsPerUnitMultiplier=3f;
                StageText(row,"State",new Vector2(270f,0f),new Vector2(82f,28f),15f);
                FindChild(row,"SelectionFrame_GUIPro").GetComponent<Image>().pixelsPerUnitMultiplier=4f;
                RemoveOwnedChild(row,"StatusIcon_GUIPro");
                var status=Icon("StatusIcon_GUIPro",row,lockedIcon,new Vector2(-305f,0f),new Vector2(24f,24f),new Color32(108,117,130,255));
                statusIcons.GetArrayElementAtIndex(stage-1).objectReferenceValue=status;
                RemoveOwnedChild(row,"RowBorder_GUIPro");
                var border=Image("RowBorder_GUIPro",row,Vector2.zero,Vector2.zero,Sprite(StageRowFrame),new Color32(60,69,80,255),false);
                Stretch(border.rectTransform);
                border.pixelsPerUnitMultiplier=6f;
                border.transform.SetAsFirstSibling();
                AddStageGradient(row,new Color32(95,111,135,22));
                cursor+=64f;
            }
            content.sizeDelta=new Vector2(0f,cursor+8f);
            var footerRoot=FindNamed<RectTransform>(root,"FooterActionRoot");
            footerRoot.anchoredPosition=new Vector2(0f,12f);
            footerRoot.sizeDelta=new Vector2(-88f,52f);
            var footer=FindNamed<TMP_Text>(root,"FooterHint");
            footer.fontSize=16f;
            footer.color=new Color32(167,172,181,255);
            foreach(var parent in new[]{navigation,(Transform)rewards})
            {
                var name=parent==navigation?"ActionDeco_GUIPro":"RewardDeco_GUIPro";
                RemoveOwnedChild(parent,name);
                var line=Image(name,parent,new Vector2(parent==navigation?0f:-151f,parent==navigation?-238f:108f),new Vector2(parent==navigation?390f:90f,4f),
                    Sprite(GuiRoot+"/Shared/Sprite_Common/Popup/Popup_Box_02_White_DecoLine.png"),new Color32(156,132,88,90),false);
                line.transform.SetSiblingIndex(parent==navigation?3:0);
                if(parent==rewards)
                {
                    RemoveOwnedChild(parent,"RewardDecoRight_GUIPro");
                    Image("RewardDecoRight_GUIPro",parent,new Vector2(151f,108f),new Vector2(90f,4f),line.sprite,line.color,false);
                }
            }
        }

        private static void ApplyApprovedStageConceptA(GameObject root, SerializedObject serialized)
        {
            var navigation = FindNamed<RectTransform>(root, "NavigationArea");
            var detail = FindNamed<RectTransform>(root, "DetailArea");
            var shell = FindNamed<RectTransform>(root, "MediumShell");
            var outer = shell.Find("FrameVisual_GUIPro");
            outer.Find("Bg").GetComponent<Image>().color = new Color32(29, 27, 31, 255);
            outer.Find("InnerBorder").GetComponent<Image>().color = new Color32(126, 104, 76, 255);
            outer.Find("TitleBg").GetComponent<Image>().color = new Color32(80, 75, 77, 255);
            ApplyStageASurface(FindChild(navigation, "StageDetailSkin") as RectTransform,
                new Color32(38, 35, 41, 255), new Color32(91, 80, 75, 255));
            ApplyStageASurface(FindChild(detail, "StageListSkin") as RectTransform,
                new Color32(38, 35, 41, 255), new Color32(91, 80, 75, 255));

            const string artPath = "Assets/ProjectMT/05_Art/UI/CastleRaid/UI_CastleRaid_Assault.png";
            var art = Sprite(artPath);
            if (art == null) throw new InvalidOperationException("군단의 역습 승인 삽화를 찾지 못했습니다.");
            var ratio = art.rect.width / art.rect.height;
            var artSize = new Vector2(406f, 406f / ratio);
            var frame = HudRect(navigation, "FortressFrame_GUIPro", new Vector2(0f, 235f), artSize + Vector2.one * 4f);
            ApplyStageASurface(frame, new Color32(38, 35, 41, 255), new Color32(105, 86, 73, 255));
            var image = HudRect(navigation, "FortressIcon", frame.anchoredPosition, artSize).GetComponent<Image>();
            image.sprite = art;
            image.color = Color.white;
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var fitter = image.GetComponent<AspectRatioFitter>() ?? image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            fitter.aspectRatio = ratio; // 원본 전체 구도와 가로세로 비율 보존

            var muted = new Color32(163, 158, 158, 255);
            var cream = new Color32(241, 231, 211, 255);
            var gold = new Color32(232, 192, 117, 255);
            ConfigureStageAText(navigation, "SelectedCaption", new Vector2(0f, 100f), new Vector2(380f, 26f), 16f, muted);
            ConfigureStageAText(navigation, "SelectedStage", new Vector2(0f, 65f), new Vector2(406f, 46f), 34f, cream);
            ConfigureStageAText(navigation, "SelectedFront", new Vector2(0f, 22f), new Vector2(174f, 30f), 17f, gold);
            var frontPlate = HudRect(navigation, "FrontPlate_GUIPro", new Vector2(0f, 22f), new Vector2(174f, 32f));
            ApplyStageASurface(frontPlate, new Color32(48, 40, 36, 255), new Color32(133, 99, 56, 255), 1f);
            ConfigureStageAText(navigation, "SelectedTheme", new Vector2(0f, -15f), new Vector2(372f, 27f), 15f, muted);

            var rewards = HudRect(navigation, "FirstClearReward", new Vector2(0f, -132f), new Vector2(372f, 130f));
            ConfigureStageAText(rewards, "RewardTitle", new Vector2(0f, 83f), new Vector2(372f, 28f), 18f, gold);
            foreach (var tileName in new[] { "DiamondTile", "TicketTile" })
            {
                var tile = HudRect(rewards, tileName, new Vector2(tileName == "DiamondTile" ? -80f : 80f, 0f), new Vector2(150f, 130f));
                ApplyStageASurface(tile, new Color32(43, 39, 45, 255), new Color32(108, 86, 68, 255));
                var icon = tile.GetComponentsInChildren<Image>(true).Single(x => x.name == "Diamond" || x.name == "Ticket");
                icon.rectTransform.anchoredPosition = new Vector2(0f, 29f);
                icon.rectTransform.sizeDelta = new Vector2(56f, 56f);
                icon.color = Color.white;
                icon.preserveAspect = true;
                ConfigureStageAText(tile, "Quantity", new Vector2(0f, -21f), new Vector2(134f, 38f), 27f, cream);
                ConfigureStageAText(tile, "CurrencyName", new Vector2(0f, -49f), new Vector2(134f, 24f), 15f, muted);
            }
            ConfigureStageAText(navigation, "ClearState", new Vector2(0f, -220f), new Vector2(406f, 30f), 14f, gold);
            foreach (var deco in root.GetComponentsInChildren<Image>(true).Where(x => x.name.Contains("Deco_GUIPro") || x.name == "RewardDecoRight_GUIPro"))
                deco.gameObject.SetActive(false);

            var enter = HudRect(navigation, "EnterButton", new Vector2(0f, -286f), new Vector2(390f, 66f));
            ApplyStageASurface(enter, new Color32(119, 55, 65, 255), new Color32(203, 142, 85, 255));
            var skin = enter.Find("GuiSkin").gameObject.AddComponent<CanvasGroup>();
            skin.blocksRaycasts = false;
            skin.interactable = false;
            serialized.FindProperty("enterButtonSkin").objectReferenceValue = skin;
            HudRect(enter, "BattleIcon", new Vector2(-82f, 0f), new Vector2(43f, 43f));
            ConfigureStageAText(enter, "Label", new Vector2(28f, 0f), new Vector2(172f, 40f), 24f, cream);
            ConfigureStageAText(navigation, "EntryHint", new Vector2(0f, -344f), new Vector2(412f, 28f), 13f, muted);

            ConfigureStageAText(detail, "ListTitle", new Vector2(-170f, 337f), new Vector2(300f, 36f), 24f, cream).fontStyle = FontStyles.Bold;
            ConfigureStageAText(detail, "ListHint", new Vector2(194f, 337f), new Vector2(250f, 28f), 15f, muted);
            var band = FindChild(detail, "VisibleFront_GUIPro") as RectTransform;
            ApplyStageASurface(band, new Color32(47, 43, 50, 255), new Color32(91, 80, 75, 255));
            FindChild(band, "VisibleFrontLabel").GetComponent<TMP_Text>().fontSize = 19f;
            foreach (var text in FindChild(detail, "StageColumnHeaders").GetComponentsInChildren<TMP_Text>(true))
            {
                text.fontSize = 14f;
                text.color = muted;
            }
            foreach (var row in FindChild(detail, "Content").GetComponentsInChildren<RectTransform>(true).Where(x => x.name.StartsWith("StageButton_")))
            {
                var bg = row.GetComponent<Image>();
                bg.sprite = Sprite(GuiRoot + "/Shared/Sprite_Common/Frame/ListFrame/ListFrame_06_White_Bg.png");
                bg.pixelsPerUnitMultiplier = 3f;
                var gradient = row.Find("SurfaceGradient_GUIPro");
                if (gradient != null) gradient.gameObject.SetActive(false);
                var border = row.Find("RowBorder_GUIPro").GetComponent<Image>();
                border.sprite = Sprite(StageRowFrame);
                border.color = new Color32(70, 66, 72, 255);
                border.pixelsPerUnitMultiplier = 6f;
                var selection = row.Find("SelectionFrame_GUIPro").GetComponent<Image>();
                selection.sprite = border.sprite;
                selection.color = new Color32(211, 157, 67, 255);
                selection.pixelsPerUnitMultiplier = 3f;
                ConfigureStageAText(row, "StageNumber", new Vector2(-200f, 0f), new Vector2(160f, 30f), 17f, cream);
                ConfigureStageAText(row, "Reward", new Vector2(79f, 0f), new Vector2(230f, 30f), 17f, cream);
                ConfigureStageAText(row, "State", new Vector2(270f, 0f), new Vector2(82f, 28f), 14f, cream);
                var plate = FindChild(row, "StatePlate").GetComponent<Image>();
                plate.pixelsPerUnitMultiplier = 3f;
            }
            foreach (var front in FindChild(detail, "Content").GetComponentsInChildren<RectTransform>(true).Where(x => x.name.StartsWith("FrontBand_")))
            {
                front.GetComponent<Image>().color = new Color32(47, 43, 50, 255);
                var label = FindChild(front, "Label").GetComponent<TMP_Text>();
                label.text = label.text.Replace(" – ", "-").Replace(" - ", "-");
                label.fontSize = 16f;
                label.color = gold;
                FindChild(front, "BandReward").GetComponent<TMP_Text>().color = muted;
            }
            FindNamed<TMP_Text>(root, "FooterHint").fontSize = 14f;
            FindNamed<TMP_Text>(root, "FooterHint").color = muted;
        }

        private static void ApplyStageASurface(RectTransform target, Color background, Color borderColor, float borderPixels = 2f)
        {
            ReferencePanel(target, background, borderColor);
            target.GetComponent<Image>().pixelsPerUnitMultiplier = 3f;
            var border = target.Find("GuiSkin/Border").GetComponent<Image>();
            border.sprite = Sprite(StageRowFrame);
            border.pixelsPerUnitMultiplier = 6f / borderPixels;
            var gradient = target.Find("SurfaceGradient_GUIPro");
            if (gradient != null) gradient.gameObject.SetActive(false);
        }

        private static TMP_Text ConfigureStageAText(Transform parent, string name, Vector2 position, Vector2 size, float points, Color color)
        {
            var text = StageText(parent, name, position, size, points);
            text.fontStyle = FontStyles.Normal;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }
        private static void ReferencePanel(RectTransform target, Color background, Color borderColor)
        {
            var image=target.GetComponent<Image>();
            image.sprite=Sprite(GuiRoot+"/Shared/Sprite_Common/Frame/ListFrame/ListFrame_06_White_Bg.png");
            image.type=UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier=1.5f;
            image.color=background;
            RemoveOwnedChild(target,"GuiSkin");
            var skin=Rect("GuiSkin",target,Vector2.zero,Vector2.zero);
            Stretch(skin);
            skin.SetAsFirstSibling();
            var border=Image("Border",skin,Vector2.zero,Vector2.zero,
                Sprite(GuiRoot+"/Shared/Sprite_Common/Frame/ListFrame/ListFrame_06_White_Border.png"),borderColor,false);
            Stretch(border.rectTransform);
            border.pixelsPerUnitMultiplier=1.5f;
            AddStageGradient(target,new Color32(87,105,133,18));
        }
        private static void AddStageGradient(RectTransform target, Color color)
        {
            RemoveOwnedChild(target,"SurfaceGradient_GUIPro");
            var gradient=Image("SurfaceGradient_GUIPro",target,Vector2.zero,Vector2.zero,
                Sprite(StagePanelSkin+"Gradient.png"),color,false);
            Stretch(gradient.rectTransform,4f);
            gradient.transform.SetAsFirstSibling();
        }
        private static TMP_Text StageRewardTile(RectTransform rewards, string name, string iconName, string caption, float x)
        {
            var icon = FindChild(rewards, iconName).GetComponent<Image>();
            icon.transform.SetParent(rewards, false);
            RemoveOwnedChild(rewards, name);
            var tile = Panel(name, rewards, new Vector2(x, 0f), new Vector2(158f, 142f), Color.white, CardBg);
            ApplyGuiSkin(tile.rectTransform, "Prefabs_Frame/ItemFrame/ItemFrame_01_Normal_Gray.prefab",
                "Bg", "Border", "InnerBorder1", "InnerBorder2");
            tile.color = new Color32(48, 49, 54, 255);
            tile.pixelsPerUnitMultiplier = 2f;
            foreach (var layer in tile.GetComponentsInChildren<Image>(true)) layer.pixelsPerUnitMultiplier = 2f;
            icon.transform.SetParent(tile.transform, false);
            icon.rectTransform.anchoredPosition = new Vector2(0f, 30f);
            icon.rectTransform.sizeDelta = new Vector2(46f, 46f);
            Text("CurrencyName", tile.transform, caption, new Vector2(0f, -14f), new Vector2(138f, 24f), 13f,
                TextAlignmentOptions.Center, new Color32(181, 186, 194, 255));
            return Text("Quantity", tile.transform, "0", new Vector2(0f, -43f), new Vector2(138f, 32f), 23f,
                TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        }

        private static TMP_Text StageText(Transform parent, string name, Vector2 position, Vector2 size, float points)
        {
            var text = HudRect(parent, name, position, size).GetComponent<TMP_Text>();
            text.fontSize = points;
            text.enableAutoSizing = false;
            text.raycastTarget = false;
            return text;
        }
    }
}