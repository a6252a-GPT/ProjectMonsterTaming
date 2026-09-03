using System;
using System.Linq;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    public static partial class CastleRaidUiSetupUtility
    {
        private const string HudGuiPrefabs = GuiRoot + "/Theme_Dark/Prefabs/";
        private const string HudCardSkin = "Prefabs_Frame/CardFrame/CardFrame_02_Blue.prefab";
        private const string HudPanelSprites = GuiRoot + "/Shared/Sprite_Common/Frame/PanelFrame/PanelFrame_03_White_";

        [MenuItem("Tools/ProjectMT/Castle Raid/Polish Battle HUD")]
        public static void ApplyBattleHudPolish()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("PlayMode를 종료한 뒤 HUD를 적용하세요.");
            }

            font = ResolveFont();
            var rootObject = PrefabUtility.LoadPrefabContents(HudPrefabPath);
            try
            {
                PolishBattleHud(rootObject);
                PrefabUtility.SaveAsPrefabAsset(rootObject, HudPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rootObject);
            }
        }

        private static void PolishBattleHud(GameObject rootObject)
        {
            var root = rootObject.GetComponent<RectTransform>();
            var view = rootObject.GetComponent<HexCastleBattleHudView>();
            if (font == null || view == null || !view.HasRuntimeBindings)
            {
                throw new InvalidOperationException("정식 HUD 폰트·타이머·실패창 연결이 필요합니다.");
            }

            var stage = HudRect(root, "StageCard", new Vector2(204f, -78f), new Vector2(360f, 108f));
            ApplyHudPanelSkin(stage);
            HudRect(stage, "Fortress", new Vector2(-144f, 12f), new Vector2(40f, 40f));
            HudText(stage, "CastleInfoText", new Vector2(28f, 17f), new Vector2(274f, 56f), 14f,
                TextAlignmentOptions.Left);
            var deployment = HudText(stage, "DeploymentText", new Vector2(28f, -32f),
                new Vector2(274f, 26f), 18f, TextAlignmentOptions.Left);
            deployment.text = "남은 병력  15 / 15";
            deployment.color = new Color32(232, 204, 143, 255);

            var status = HudRect(root, "StatusPanel", new Vector2(0f, 252f), new Vector2(900f, 42f));
            status.anchorMin = status.anchorMax = new Vector2(0.5f, 0f);
            var toast = AssetDatabase.LoadAssetAtPath<GameObject>(HudGuiPrefabs + "Prefabs_HUD/ToastMessage_03.prefab");
            status.GetComponent<Image>().sprite = toast.GetComponent<Image>().sprite;
            status.GetComponent<Image>().color = new Color32(18, 26, 34, 220);
            HudText(status, "StatusText", Vector2.zero, new Vector2(864f, 34f), 18f,
                TextAlignmentOptions.Center);
            var timer = (RectTransform)FindChild(root, "BattleTimerBadge");
            timer.SetParent(root, false);
            timer.anchorMin = timer.anchorMax = new Vector2(0.5f, 1f);
            timer.anchoredPosition = new Vector2(0f, -66f);
            timer.sizeDelta = new Vector2(180f, 84f);
            ApplyHudPanelSkin(timer);
            var clock = HudRect(timer, "UrgencyAccent", new Vector2(-47f, -12f), new Vector2(24f, 28f));
            clock.GetComponent<Image>().sprite = Sprite(GuiRoot + "/Shared/Sprite_Common/HUD/Timer_01_Icon.png");
            clock.GetComponent<Image>().type = UnityEngine.UI.Image.Type.Simple;
            clock.GetComponent<Image>().preserveAspect = true;
            HudText(timer, "Caption", new Vector2(0f, 22f), new Vector2(148f, 20f), 12f,
                TextAlignmentOptions.Center);
            HudText(timer, "TimerText", new Vector2(17f, -12f), new Vector2(96f, 38f), 30f,
                TextAlignmentOptions.Center);
            foreach (var buttonName in new[] { "RotateCameraLeftButton", "RotateCameraRightButton" })
            {
                var control = (RectTransform)FindChild(root, buttonName);
                var isLeft = buttonName == "RotateCameraLeftButton";
                control.anchorMin = control.anchorMax = new Vector2(isLeft ? 0f : 1f, 0f);
                control.anchoredPosition = new Vector2(isLeft ? 142f : -142f, 120f);
                control.sizeDelta = new Vector2(96f, 96f);
                ApplyGuiSkin(control, "Prefabs_Button/Button_Circle_02.prefab", "Bg", "InnerBorder", "InnerGlow");
                HudRect(control, "Icon", Vector2.zero, new Vector2(38f, 38f));
                RemoveOwnedChild(control, "RotationCaption");
                Text("RotationCaption", control, isLeft ? "왼쪽 회전" : "오른쪽 회전", new Vector2(0f, -66f),
                    new Vector2(120f, 26f), 16f, TextAlignmentOptions.Center,
                    new Color32(255, 231, 176, 255), FontStyles.Bold).raycastTarget = false;
            }
            var exitControl = HudRect(root, "ExitButton", new Vector2(-58f, -56f), new Vector2(68f, 64f));
            exitControl.anchorMin = exitControl.anchorMax = new Vector2(1f, 1f);
            ApplyGuiSkin(exitControl, "Prefabs_Button/Button_02_Red.prefab",
                "Bg", "Light", "HighLight");

            var dock = HudRect(root, "BottomDeploymentDock", new Vector2(0f, 120f),
                new Vector2(880f, 206f));
            dock.GetComponent<Image>().color = Color.clear;
            RemoveOwnedChild(dock, "GuiSkin");
            FindChild(root, "HudVignette").gameObject.SetActive(false);
            FindChild(dock, "DockInner").gameObject.SetActive(false);
            FindChild(dock, "DockHint").gameObject.SetActive(false);
            var title = HudText(dock, "DockTitle", new Vector2(18f, -20f), new Vector2(180f, 26f),
                17f, TextAlignmentOptions.Left);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 0.5f);

            var slots = new HexCastleBattleHudView.DeploymentSlot[HexCastleRaidStartData.DeploymentSlotCount];
            FindChild(dock, "UnitButton_9").gameObject.SetActive(false);
            FindChild(dock, "UnitButton_10").gameObject.SetActive(false);
            for (var index = 0; index < slots.Length; index++)
            {
                var card = HudRect(dock, $"UnitButton_{index + 1}",
                    new Vector2((index - 2f) * HexCastleBattleHudView.DeploymentSlotSpacing, -12f),
                    new Vector2(160f, 152f));
                ApplyGuiSkin(card, HudCardSkin, "Bg(Mask)", "InnerBorder", "Border", "HightLight");
                var name = HudText(card, "Label", new Vector2(0f, -39f), new Vector2(148f, 25f),
                    18f, TextAlignmentOptions.Center);
                name.text = $"부대 {index + 1}";
                name.textWrappingMode = TextWrappingModes.NoWrap;
                name.overflowMode = TextOverflowModes.Ellipsis;

                var ai = HudRect(card, "AITag", new Vector2(0f, -62f), new Vector2(144f, 28f));
                ai.GetComponent<Image>().sprite = Sprite(ButtonBg);
                ai.GetComponent<Image>().color = new Color32(39, 61, 84, 255);
                HudText(ai, "Label", Vector2.zero, new Vector2(134f, 24f), 13f,
                    TextAlignmentOptions.Center);

                RemoveOwnedChild(card, "Portrait");
                RemoveOwnedChild(card, "CountBadge");
                RemoveOwnedChild(card, "SelectedFrame");
                var portrait = Image("Portrait", card, new Vector2(0f, 18f), new Vector2(98f, 98f),
                    null, Color.white, false);
                portrait.preserveAspect = true;
                portrait.enabled = false;
                portrait.transform.SetSiblingIndex(1);
                var namePlate = GuiLayer(card, HudCardSkin, "BottomBar", "GuiNamePlate");
                namePlate.rectTransform.anchorMin = namePlate.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                namePlate.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                namePlate.rectTransform.anchoredPosition = new Vector2(0f, -49f);
                namePlate.rectTransform.sizeDelta = new Vector2(152f, 50f);
                namePlate.transform.SetSiblingIndex(2);
                var badge = Panel("CountBadge", card, new Vector2(51f, 56f), new Vector2(52f, 32f),
                    new Color32(27, 41, 60, 255), ButtonBg);
                badge.raycastTarget = false;
                var count = Text("RemainingCount", badge.transform, "×3", Vector2.zero,
                    new Vector2(48f, 29f), 22f, TextAlignmentOptions.Center,
                    new Color32(255, 231, 176, 255), FontStyles.Bold);
                var selected = GuiLayer(card, HudCardSkin, "FocusBorder", "SelectedFrame");
                selected.color = new Color32(139, 238, 255, 255);
                selected.enabled = false;
                var visual = card.GetComponent<CanvasGroup>();
                if (visual == null)
                {
                    visual = card.gameObject.AddComponent<CanvasGroup>();
                }
                slots[index] = new HexCastleBattleHudView.DeploymentSlot
                {
                    Root = card,
                    Portrait = portrait,
                    Count = count,
                    Selection = selected,
                    Visual = visual,
                    Background = card.GetComponent<Image>(),
                    RarityBorder = card.Find("GuiSkin/InnerBorder").GetComponent<Image>(),
                    RarityHighlight = card.Find("GuiSkin/HightLight").GetComponent<Image>()
                };
            }

            var description = HudRect(root, "AIProfileDescriptionPanel", new Vector2(0f, 290f),
                new Vector2(740f, 100f));
            ApplyHudPanelSkin(description);
            HudText(description, "DescriptionText", Vector2.zero, new Vector2(692f, 76f), 18f,
                TextAlignmentOptions.Center);
            var raritySkins = new[] { "Gray", "Blue", "Plum", "Yellow", "Red" };
            var rarityStyles = raritySkins.Select((skin, index) =>
            {
                var path = $"Prefabs_Frame/CardFrame/CardFrame_02_{skin}.prefab";
                return new HexCastleBattleHudView.DeploymentRarityStyle
                {
                    Rarity = (MonsterRarity)index,
                    Background = GuiImage(path, "Bg(Mask)").color,
                    Border = GuiImage(path, "InnerBorder").color,
                    Highlight = GuiImage(path, "HightLight").color
                };
            }).ToArray();
            view.EditorConfigureDeployment(dock, slots, rarityStyles);
            view.ConfigureDeployment(HexCastleRaidStartData.DeploymentSlotCount);
        }

        private static void ApplyHudPanelSkin(RectTransform target)
        {
            var background = target.GetComponent<Image>();
            background.sprite = Sprite(HudPanelSprites + "Bg.png");
            background.type = UnityEngine.UI.Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 2f;
            background.color = new Color32(35, 37, 43, 240);
            RemoveOwnedChild(target, "GuiSkin");
            var skin = Rect("GuiSkin", target, Vector2.zero, Vector2.zero);
            Stretch(skin);
            skin.SetAsFirstSibling();
            var border = Image("Border", skin, Vector2.zero, Vector2.zero,
                Sprite(HudPanelSprites + "Border.png"), new Color32(10, 14, 20, 255), false);
            Stretch(border.rectTransform);
            border.pixelsPerUnitMultiplier = 2f;
            var inner = Image("InnerBorder", skin, Vector2.zero, Vector2.zero,
                Sprite(HudPanelSprites + "InnerBorder.png"), new Color32(86, 96, 111, 255), false);
            Stretch(inner.rectTransform);
            inner.rectTransform.sizeDelta = new Vector2(-4f, -4f);
            inner.pixelsPerUnitMultiplier = 2f;
        }

        private static void ApplyGuiSkin(RectTransform target, string prefabPath, string backgroundName,
            params string[] layers)
        {
            var source = GuiImage(prefabPath, backgroundName);
            var background = target.GetComponent<Image>();
            background.sprite = source.sprite;
            background.color = source.color;
            background.type = source.type;
            background.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            RemoveOwnedChild(target, "GuiSkin");
            var skin = Rect("GuiSkin", target, Vector2.zero, Vector2.zero);
            Stretch(skin);
            skin.SetAsFirstSibling();
            foreach (var layer in layers) GuiLayer(skin, prefabPath, layer, layer);
        }

        private static Image GuiLayer(RectTransform parent, string prefabPath, string sourceName, string name)
        {
            RemoveOwnedChild(parent, name);
            var source = GuiImage(prefabPath, sourceName);
            var copy = Image(name, parent, Vector2.zero, Vector2.zero, source.sprite, source.color, false);
            copy.type = source.type;
            copy.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            var from = source.rectTransform;
            var to = copy.rectTransform;
            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.pivot = from.pivot;
            to.sizeDelta = from.sizeDelta;
            to.anchoredPosition = from.anchoredPosition;
            return copy;
        }

        private static Image GuiImage(string prefabPath, string name)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(HudGuiPrefabs + prefabPath);
            if (source == null) throw new InvalidOperationException($"GUI 원본을 찾지 못했습니다: {prefabPath}");
            return source.GetComponentsInChildren<Image>(true).First(image => image.name == name);
        }

        private static RectTransform HudRect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var rect = FindChild(parent, name) as RectTransform;
            if (rect == null)
            {
                throw new InvalidOperationException($"HUD 요소를 찾지 못했습니다: {name}");
            }

            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static TMP_Text HudText(Transform parent, string name, Vector2 position, Vector2 size,
            float sizeInPoints, TextAlignmentOptions alignment)
        {
            var text = HudRect(parent, name, position, size).GetComponent<TMP_Text>();
            text.fontSize = sizeInPoints;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }
    }
}
