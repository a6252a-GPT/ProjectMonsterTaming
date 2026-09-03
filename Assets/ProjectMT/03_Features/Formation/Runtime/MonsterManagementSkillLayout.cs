using System;
using System.Collections.Generic;
using System.Globalization;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectMT.Features.Formation
{
    public sealed class MonsterManagementSkillLayout : IDisposable // 원본 Prefab을 보존하는 관리창 표시 레이아웃
    {
        private static readonly string[] PassiveIconKeys =
        {
            "passive_courage_aura", "passive_crisis_defense", "passive_entry_shield", "passive_first_wave",
            "passive_formation_bond", "passive_impact_strike", "passive_kain_duality", "passive_kill_heal",
            "passive_long_range_aim", "passive_low_hp_hunter", "passive_nth_hit_heal", "passive_nth_hit_power",
            "passive_ranged_hunter", "passive_same_target_haste", "passive_weakpoint_stack"
        };
        private readonly List<Action> restore = new List<Action>();
        private readonly Dictionary<TMP_Text, TMP_Text> statIncreases = new Dictionary<TMP_Text, TMP_Text>();
        private readonly Dictionary<TMP_Text, TMP_Text> statValues = new Dictionary<TMP_Text, TMP_Text>();
        private readonly List<GameObject> guiRoots = new List<GameObject>();
        private readonly TMP_Text textStyle;
        private readonly Image frameStyle;
        private readonly GridLayoutGroup grid;
        private GameObject summaryRoot;
        private GameObject detailRoot;
        private TMP_Text detailTitle;
        private TMP_Text detailCategory;
        private RectTransform detailPanel;
        private RectTransform detailIconFrame;
        private Image detailIcon;
        private GameObject detailNormalIcon;
        private GameObject detailEmptyIcon;
        private Button detailClose;
        private GameObject previousSelection;
        private TMP_Text nameStyle;
        private TMP_Text bodyStyle;
        private TMP_Text detailBody;
        private TMP_Text detailHint;
        private ScrollRect detailScroll;
        private SkillRow passiveRow;
        private SkillRow activeRow;
        private string selectedMonsterId;
        private SkillRow openedRow;

        public readonly struct SkillSummary
        {
            public readonly string Name;
            public readonly string Description;
            public readonly Sprite Icon;

            public SkillSummary(string name, string description, Sprite icon = null)
            {
                Name = name;
                Description = description;
                Icon = icon;
            }
        }

        private sealed class SkillRow
        {
            public string Category;
            public TMP_Text Name;
            public TMP_Text CategoryLabel;
            public TMP_Text Description;
            public Image Icon;
            public GameObject NormalIcon;
            public GameObject EmptyIcon;
            public SkillSummary Summary;
        }

        private MonsterManagementSkillLayout(TMP_Text textStyle, Image frameStyle, GridLayoutGroup grid)
        {
            this.textStyle = textStyle;
            this.frameStyle = frameStyle;
            this.grid = grid;
        }

        public static MonsterManagementSkillLayout Create(GameObject growthContent, TMP_Text textStyle)
        {
            if (growthContent == null || textStyle == null) return null;
            var panel = growthContent.transform.Find("StatsPanel") as RectTransform;
            var grid = panel != null ? panel.GetComponentInChildren<GridLayoutGroup>(true) : null;
            if (grid == null || grid.transform.childCount != 6) return null;
            var skillTemplate = Resources.Load<GameObject>("MonsterManagementGUI/SkillInfo");
            var statsTemplate = Resources.Load<GameObject>("MonsterManagementGUI/Group_StatsList");
            if (skillTemplate == null || statsTemplate == null) return null;
            var frame = grid.transform.GetChild(0).Find("CardBorder")?.GetComponent<Image>();
            var layout = new MonsterManagementSkillLayout(textStyle, frame, grid);
            try
            {
                layout.BuildSourceGui(growthContent.transform, panel, skillTemplate, statsTemplate);
                return layout;
            }
            catch { layout.Dispose(); throw; }
        }

        public static SkillSummary DescribeSkill(MonsterSkillDefinitionBase skill, bool active, MonsterRarity rarity)
        {
            if (skill == null)
            {
                if (active && rarity < MonsterRarity.Legendary)
                    return new SkillSummary("액티브 없음", "이 몬스터는 패시브 스킬을 사용합니다.");
                return new SkillSummary("스킬 준비 중", "아직 스킬이 연결되지 않았습니다.");
            }
            var name = string.IsNullOrWhiteSpace(skill.DisplayName) ? "이름 미등록" : skill.DisplayName;
            var description = string.IsNullOrWhiteSpace(skill.Description)
                ? "상세 설명이 아직 등록되지 않았습니다." : skill.Description;
            if (!skill.AuthoringEnabled)
            {
                name += " · 준비 중";
                description = "현재 적용되지 않는 스킬입니다.\n" + description;
            }
            var icon = Resources.Load<Sprite>("MonsterSkillIcons/" + GetSkillIconKey(skill.SkillId));
            return new SkillSummary(name, description, icon != null ? icon : skill.Icon);
        }

        public static string GetSkillIconKey(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId)) return string.Empty;
            foreach (var key in PassiveIconKeys)
                if (skillId == key || skillId.StartsWith(key + "_", StringComparison.Ordinal)) return key;
            return skillId; // 액티브는 몬스터별 고유 효과 그림 사용
        }

        public void Bind(MonsterRarityCatalog catalog, string monsterId, bool visible)
        {
            if (summaryRoot == null) return;
            if (selectedMonsterId != monsterId || !visible) CloseDetail();
            selectedMonsterId = monsterId;
            summaryRoot.SetActive(visible && !string.IsNullOrEmpty(monsterId));
            foreach (var root in guiRoots)
                if (root != null) root.SetActive(visible && !string.IsNullOrEmpty(monsterId));
            MonsterPassiveSkill passive = null;
            MonsterActiveSkill active = null;
            if (catalog == null || !catalog.TryGetRarity(monsterId, out var rarity))
            {
                var missing = new SkillSummary("정보 없음", "몬스터의 스킬 정보를 찾을 수 없습니다.");
                BindRow(passiveRow, missing);
                BindRow(activeRow, missing);
                return;
            }
            catalog.TryGetSkillLoadout(monsterId, out passive, out active); // 전투 Snapshot과 같은 배정표 사용
            BindRow(passiveRow, DescribeSkill(passive, false, rarity));
            BindRow(activeRow, DescribeSkill(active, true, rarity));
        }

        public static string FormatStatIncrease(string currentValue, string nextValue)
        {
            if (currentValue == nextValue) return string.Empty;
            var percent = currentValue.EndsWith("%", StringComparison.Ordinal) && nextValue.EndsWith("%", StringComparison.Ordinal);
            var currentNumber = percent ? currentValue.Substring(0, currentValue.Length - 1) : currentValue;
            var nextNumber = percent ? nextValue.Substring(0, nextValue.Length - 1) : nextValue;
            if (!decimal.TryParse(currentNumber, NumberStyles.Number, CultureInfo.CurrentCulture, out var current) ||
                !decimal.TryParse(nextNumber, NumberStyles.Number, CultureInfo.CurrentCulture, out var next))
                return "다음 " + nextValue;
            var increase = next - current; // 표시된 현재/다음 값의 차이만 표시하고 성장 계산은 변경하지 않음
            return increase == 0m ? string.Empty : increase.ToString("+0.##;-0.##;0", CultureInfo.CurrentCulture) + (percent ? "%p" : string.Empty);
        }

        public bool TrySetStatComparison(TMP_Text target, string currentValue, string nextValue)
        {
            if (target == null || !statIncreases.TryGetValue(target, out var increase)) return false;
            if (!statValues.TryGetValue(target, out var display)) return false;
            display.text = currentValue;
            increase.text = FormatStatIncrease(currentValue, nextValue);
            increase.color = increase.text.StartsWith("-", StringComparison.Ordinal)
                ? new Color32(218, 148, 131, 255) : new Color32(166, 196, 110, 255);
            return true;
        }

        private void BuildSourceGui(Transform growthContent, RectTransform panel,
            GameObject skillTemplate, GameObject statsTemplate)
        {
            const float width = 416f;
            var action = growthContent.Find("GrowthActionPanel") as RectTransform;
            bodyStyle = action?.Find("GoldCost")?.GetComponent<TMP_Text>() ?? textStyle;
            nameStyle = action?.GetComponentInChildren<Button>(true)?.GetComponentInChildren<TMP_Text>(true) ?? textStyle;
            var oldActive = panel.gameObject.activeSelf;
            restore.Add(() => { if (panel != null) panel.gameObject.SetActive(oldActive); });
            panel.gameObject.SetActive(false); // 원본 능력치 카드와 연결은 그대로 보존
            var stats = CloneGui(statsTemplate, growthContent, "MonsterStats_Runtime");
            stats.localScale = Vector3.one;
            Place(stats, new Vector2(panel.anchoredPosition.x, -136f), new Vector2(width, 138f));
            var sourceGrid = stats.GetComponent<GridLayoutGroup>();
            if (sourceGrid == null || stats.childCount != grid.transform.childCount)
                throw new InvalidOperationException("Monster management GUI stats template mismatch.");
            sourceGrid.cellSize = new Vector2(202f, 42f);
            sourceGrid.spacing = new Vector2(12f, 6f);
            sourceGrid.padding = new RectOffset();
            sourceGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            sourceGrid.constraintCount = 2;
            sourceGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
            LayoutRebuilder.ForceRebuildLayoutImmediate(stats);
            for (var index = 0; index < grid.transform.childCount; index++)
            {
                var original = grid.transform.GetChild(index);
                var card = stats.GetChild(index);
                var icon = card.Find("Icon").GetComponent<Image>();
                icon.sprite = original.Find("StatIcon").GetComponent<Image>().sprite; // 게임에서 사용 중인 아이콘 유지
                icon.preserveAspect = true;
                icon.rectTransform.localScale = Vector3.one;
                Place(icon.rectTransform, new Vector2(-77.5f, 0f), new Vector2(29f, 29f));
                var title = card.Find("Text_Title").GetComponent<TMP_Text>();
                UseKoreanFont(title, bodyStyle);
                title.text = original.Find("Label").GetComponent<TMP_Text>().text;
                Place(title.rectTransform, new Vector2(20f, 10f), new Vector2(136f, 15f));
                SetTextSize(title, 10.5f, 10.5f);
                title.color = new Color32(181, 175, 165, 255);
                var value = card.Find("Text_Value").GetComponent<TMP_Text>();
                value.textWrappingMode = TextWrappingModes.NoWrap;
                value.overflowMode = TextOverflowModes.Ellipsis;
                value.richText = false;
                value.text = string.Empty;
                Place(value.rectTransform, new Vector2(-4f, -7f), new Vector2(88f, 23f));
                SetTextSize(value, 17f, 12f);
                value.color = new Color32(237, 232, 219, 255);
                var increase = NewText("LevelUpDelta_Runtime", card, string.Empty,
                    new Vector2(64f, -8f), new Vector2(52f, 18f),
                    10.5f, TextAlignmentOptions.Right);
                increase.enableAutoSizing = true;
                increase.fontSizeMin = 8f;
                increase.fontSizeMax = 10.5f;
                var target = original.Find("Value").GetComponent<TMP_Text>();
                statValues.Add(target, value);
                statIncreases.Add(target, increase);
            }
            if (action != null)
            {
                RememberRect(action);
                Place(action, new Vector2(panel.anchoredPosition.x, -265f), new Vector2(422f, 96f)); // 기존 사각 배경·4면 테두리 보존
                CompactLabel(action.Find("NextLevelCaption")?.GetComponent<TMP_Text>(), new Vector2(-90f, 29f), new Vector2(220f, 16f), 11f);
                CompactLabel(action.Find("NextLevelValue")?.GetComponent<TMP_Text>(), new Vector2(-90f, 8f), new Vector2(220f, 22f), 17f);
                CompactLabel(action.Find("GoldCost")?.GetComponent<TMP_Text>(), new Vector2(-90f, -23f), new Vector2(220f, 34f), 12f);
                var actionButton = action.GetComponentInChildren<Button>(true);
                if (actionButton != null)
                {
                    RememberRect((RectTransform)actionButton.transform);
                    Place((RectTransform)actionButton.transform, new Vector2(129f, 0f), new Vector2(150f, 50f));
                }
            }
            var summary = NewRect("MonsterSkillSummary_Runtime", growthContent);
            summaryRoot = summary.gameObject;
            Place(summary, new Vector2(panel.anchoredPosition.x, -7f), new Vector2(width, 88f));
            passiveRow = CreateSourceSkillRow(skillTemplate, summary, "패시브", -1f);
            activeRow = CreateSourceSkillRow(skillTemplate, summary, "액티브", 1f);
            var page = growthContent.GetComponentInParent<MonsterManagementPageController>(true);
            BuildDetail(page != null ? page.transform : growthContent.parent, width);
        }

        private RectTransform CloneGui(GameObject template, Transform parent, string name)
        {
            var clone = UnityEngine.Object.Instantiate(template, parent, false);
            if (parent.gameObject != summaryRoot) guiRoots.Add(clone); // 스킬 두 칸은 summaryRoot가 함께 정리
            clone.name = name;
            foreach (var node in clone.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = parent.gameObject.layer;
            foreach (var graphic in clone.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            var rect = (RectTransform)clone.transform;
            Place(rect, Vector2.zero, rect.sizeDelta);
            return rect;
        }

        private void UseKoreanFont(TMP_Text text, TMP_Text style = null)
        {
            style = style != null ? style : textStyle;
            text.font = style.font;
            text.fontSharedMaterial = style.fontSharedMaterial;
            text.richText = false;
            text.raycastTarget = false;
        }

        private SkillRow CreateSourceSkillRow(GameObject template, RectTransform parent, string category, float side)
        {
            var rect = CloneGui(template, parent, category == "패시브" ? "PassiveSkill" : "ActiveSkill");
            var width = (parent.sizeDelta.x - 12f) * 0.5f;
            Place(rect, new Vector2(side * (width + 12f) * 0.5f, 0f), new Vector2(width, parent.sizeDelta.y));
            var background = rect.Find("Bg").GetComponent<Image>();
            background.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var frame = (RectTransform)rect.Find("SkillFrame_01");
            Place(frame, new Vector2(-width * 0.5f + 44f, 0f), new Vector2(199f, 199f));
            frame.localScale = Vector3.one * (64f / 199f);
            foreach (Transform state in frame) state.gameObject.SetActive(state.name == "Empty");
            var empty = frame.Find("Empty").gameObject;
            empty.transform.Find("Icon")?.gameObject.SetActive(false); // 없는 스킬 아이콘은 장식으로 대체하지 않음
            var normal = frame.Find("Normal").gameObject;
            var icon = normal.transform.Find("Bg(Mask)/Skill").GetComponent<Image>();
            icon.sprite = null;
            icon.color = Color.white;
            icon.preserveAspect = true;
            var name = rect.Find("Text_SkillName").GetComponent<TMP_Text>();
            var description = rect.Find("Text_SkillDescription").GetComponent<TMP_Text>();
            UseKoreanFont(name, nameStyle);
            UseKoreanFont(description, bodyStyle);
            Place(name.rectTransform, new Vector2(40f, -9.5f), new Vector2(106f, 23f));
            name.fontSize = name.fontSizeMax = 15f;
            name.fontSizeMin = 12f;
            name.enableAutoSizing = true;
            name.alignment = TextAlignmentOptions.Left;
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            description.gameObject.SetActive(false); // 전체 설명은 터치 팝업에만 표시
            var categoryLabel = NewText("Category", rect, category,
                new Vector2(-13f, 13f), new Vector2(106f, 16f), 11f);
            UseKoreanFont(categoryLabel, bodyStyle);
            categoryLabel.color = category == "액티브" ? new Color32(219, 187, 111, 255) : new Color32(181, 175, 165, 255);
            var row = new SkillRow
            {
                Category = category, Name = name, Description = description, CategoryLabel = categoryLabel,
                Icon = icon, NormalIcon = normal, EmptyIcon = empty
            };
            button.onClick.AddListener(() => ShowDetail(row));
            return row;
        }

        private static void BindRow(SkillRow row, SkillSummary summary)
        {
            row.Summary = summary;
            row.Name.text = summary.Name;
            row.Description.text = summary.Description;
            row.Icon.sprite = summary.Icon;
            row.NormalIcon.SetActive(summary.Icon != null);
            row.EmptyIcon.SetActive(summary.Icon == null); // 빈 사각 슬롯의 크기와 위치 고정
        }

        private void BuildDetail(Transform parent, float width)
        {
            var overlay = NewRect("MonsterSkillDetail_Runtime", parent);
            detailRoot = overlay.gameObject;
            Stretch(overlay);
            var outside = NewRect("OutsideClose", overlay);
            Stretch(outside);
            outside.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.48f);
            var outsideButton = outside.gameObject.AddComponent<Button>();
            outsideButton.transition = Selectable.Transition.None;
            outsideButton.navigation = new Navigation { mode = Navigation.Mode.None };
            outsideButton.onClick.AddListener(CloseDetail);
            var rect = NewRect("Panel", overlay);
            detailPanel = rect;
            Place(rect, Vector2.zero, new Vector2(width, 280f));
            rect.gameObject.AddComponent<Image>().color = new Color32(44, 41, 47, 255);
            var blockInsideClick = rect.gameObject.AddComponent<Button>();
            blockInsideClick.transition = Selectable.Transition.None;
            blockInsideClick.navigation = new Navigation { mode = Navigation.Mode.None };
            AddDetailFrame(rect, "GUIFrame", "DetailFrame", new Color32(31, 31, 31, 255), 0f, 3f);
            AddDetailFrame(rect, "GUIInnerFrame", "DetailInnerFrame", new Color32(255, 255, 255, 82), 3f, 5.4f);
            detailIconFrame = UnityEngine.Object.Instantiate((RectTransform)passiveRow.NormalIcon.transform.parent, rect, false);
            detailIconFrame.name = "SkillIconFrame";
            detailIconFrame.localScale = Vector3.one * (80f / 199f);
            detailNormalIcon = detailIconFrame.Find("Normal").gameObject;
            detailEmptyIcon = detailIconFrame.Find("Empty").gameObject;
            detailIcon = detailNormalIcon.transform.Find("Bg(Mask)/Skill").GetComponent<Image>();
            detailCategory = NewText("Category", rect, string.Empty, new Vector2(-80f, 79f), new Vector2(216f, 20f), 12f);
            UseKoreanFont(detailCategory, bodyStyle);
            detailTitle = NewText("Title", rect, string.Empty, new Vector2(-80f, 46f), new Vector2(216f, 54f), 22f);
            UseKoreanFont(detailTitle, nameStyle);
            detailTitle.textWrappingMode = TextWrappingModes.Normal;
            SetTextSize(detailTitle, 22f, 16f);
            var closeRect = NewRect("Close", rect);
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = Vector2.one;
            closeRect.anchoredPosition = new Vector2(-3f, -3f);
            closeRect.sizeDelta = new Vector2(62f, 62f);
            closeRect.gameObject.AddComponent<Image>().color = Color.clear;
            detailClose = closeRect.gameObject.AddComponent<Button>();
            detailClose.onClick.AddListener(CloseDetail);
            detailClose.navigation = new Navigation { mode = Navigation.Mode.None };
            var closeLabel = NewText("Label", closeRect, "닫기", Vector2.zero, new Vector2(54f, 24f), 12f, TextAlignmentOptions.Center);
            UseKoreanFont(closeLabel, bodyStyle);
            closeLabel.color = new Color32(198, 191, 174, 255);
            var viewport = NewRect("DescriptionViewport", rect);
            Place(viewport, new Vector2(0f, -60f), new Vector2(width - 52f, 112f));
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            detailBody = NewText("FullDescription", viewport, string.Empty, Vector2.zero, new Vector2(width - 52f, 112f), 15f, TextAlignmentOptions.TopLeft);
            UseKoreanFont(detailBody, bodyStyle);
            detailBody.lineSpacing = 8f;
            detailBody.color = new Color32(222, 216, 206, 255);
            detailBody.textWrappingMode = TextWrappingModes.Normal;
            detailBody.overflowMode = TextOverflowModes.Overflow;
            detailBody.rectTransform.anchorMin = detailBody.rectTransform.anchorMax = new Vector2(0f, 1f);
            detailBody.rectTransform.pivot = new Vector2(0f, 1f);
            detailScroll = viewport.gameObject.AddComponent<ScrollRect>();
            detailScroll.viewport = viewport;
            detailScroll.content = detailBody.rectTransform;
            detailScroll.horizontal = false;
            detailScroll.movementType = ScrollRect.MovementType.Clamped;
            detailHint = NewText("ScrollHint", rect, "위아래로 밀어 전체 설명 보기", new Vector2(-width * 0.5f + 26f, -120f), new Vector2(width - 52f, 18f), 11f);
            UseKoreanFont(detailHint, bodyStyle);
            detailHint.color = new Color32(174, 165, 147, 255);
            CloseDetail();
        }

        private void ShowDetail(SkillRow row)
        {
            if (openedRow == row && detailRoot.activeSelf) { CloseDetail(); return; }
            if (!detailRoot.activeSelf) previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            openedRow = row;
            detailTitle.text = row.Summary.Name;
            detailCategory.text = row.Category;
            detailCategory.color = row.CategoryLabel.color;
            detailIcon.sprite = row.Summary.Icon;
            detailNormalIcon.SetActive(row.Summary.Icon != null);
            detailEmptyIcon.SetActive(row.Summary.Icon == null);
            detailBody.text = row.Summary.Description;
            detailRoot.SetActive(true);
            detailRoot.transform.SetAsLastSibling();
            var height = detailBody.GetPreferredValues(detailBody.text, detailBody.rectTransform.rect.width, Mathf.Infinity).y;
            var panelHeight = Mathf.Clamp(height + 172f, 260f, 400f);
            detailPanel.sizeDelta = new Vector2(detailPanel.sizeDelta.x, panelHeight);
            var top = panelHeight * 0.5f;
            Place(detailIconFrame, new Vector2(-142f, top - 68f), new Vector2(199f, 199f));
            detailCategory.rectTransform.anchoredPosition = new Vector2(-80f, top - 51f);
            detailTitle.rectTransform.anchoredPosition = new Vector2(-80f, top - 87f);
            var viewportHeight = panelHeight - 168f;
            Place(detailScroll.viewport, new Vector2(0f, -56f), new Vector2(detailPanel.sizeDelta.x - 52f, viewportHeight));
            detailBody.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(viewportHeight, height + 4f));
            detailHint.rectTransform.anchoredPosition = new Vector2(-detailPanel.sizeDelta.x * 0.5f + 26f, -top + 13f);
            detailHint.gameObject.SetActive(height > viewportHeight);
            detailScroll.StopMovement();
            detailScroll.verticalNormalizedPosition = 1f;
            EventSystem.current?.SetSelectedGameObject(detailClose.gameObject);
        }

        public bool TryCloseDetail()
        {
            if (detailRoot == null || !detailRoot.activeSelf) return false;
            CloseDetail();
            return true;
        }

        private void CloseDetail()
        {
            openedRow = null;
            if (detailRoot != null) detailRoot.SetActive(false);
            if (previousSelection != null && previousSelection.activeInHierarchy)
                EventSystem.current?.SetSelectedGameObject(previousSelection);
            previousSelection = null;
        }

        private void CompactLabel(TMP_Text label, Vector2 position, Vector2 size, float fontSize,
            TextAlignmentOptions? alignment = null)
        {
            if (label == null) return;
            RememberRect(label.rectTransform);
            var oldSize = label.fontSize;
            var oldMin = label.fontSizeMin;
            var oldMax = label.fontSizeMax;
            var oldAuto = label.enableAutoSizing;
            var oldText = label.text;
            var oldRichText = label.richText;
            var oldAlignment = label.alignment;
            restore.Add(() =>
            {
                if (label == null) return;
                label.fontSize = oldSize;
                label.fontSizeMin = oldMin;
                label.fontSizeMax = oldMax;
                label.enableAutoSizing = oldAuto;
                label.text = oldText;
                label.richText = oldRichText;
                label.alignment = oldAlignment;
            });
            Place(label.rectTransform, position, size);
            label.fontSize = label.fontSizeMax = fontSize;
            label.fontSizeMin = 11f;
            label.enableAutoSizing = true;
            if (alignment.HasValue) label.alignment = alignment.Value;
        }

        private TMP_Text NewText(string name, Transform parent, string value, Vector2 position, Vector2 size,
            float fontSize, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var rect = NewRect(name, parent);
            Place(rect, position, size);
            if (alignment == TextAlignmentOptions.Left || alignment == TextAlignmentOptions.TopLeft) rect.pivot = new Vector2(0f, 0.5f);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = textStyle.font;
            text.fontSharedMaterial = textStyle.fontSharedMaterial;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Normal;
            text.color = new Color32(237, 232, 219, 255);
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.richText = false;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private void AddDetailFrame(RectTransform parent, string name, string key, Color color, float inset, float pixelMultiplier)
        {
            var sprite = Resources.Load<Sprite>("MonsterManagementGUI/" + key);
            if (sprite == null && frameStyle == null) return;
            var rect = NewRect(name, parent);
            Stretch(rect);
            rect.sizeDelta = Vector2.one * (-2f * inset);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : frameStyle.sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = pixelMultiplier;
            image.fillCenter = false;
            image.color = color;
            image.raycastTarget = false;
        }

        private static void SetTextSize(TMP_Text text, float size, float minimum)
        {
            text.fontSize = text.fontSizeMax = size;
            text.fontSizeMin = minimum;
            text.enableAutoSizing = minimum < size;
            text.margin = Vector4.zero;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = rect.sizeDelta = Vector2.zero;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
            gameObject.transform.SetParent(parent, false);
            return (RectTransform)gameObject.transform;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void RememberRect(RectTransform rect)
        {
            var min = rect.anchorMin;
            var max = rect.anchorMax;
            var pivot = rect.pivot;
            var position = rect.anchoredPosition;
            var size = rect.sizeDelta;
            restore.Add(() =>
            {
                if (rect == null) return;
                rect.anchorMin = min;
                rect.anchorMax = max;
                rect.pivot = pivot;
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
            });
        }

        public void Dispose()
        {
            foreach (var root in guiRoots)
            {
                if (root == null) continue;
                root.SetActive(false);
                MonsterPreviewPresentation.DestroyOwned(root);
            }
            guiRoots.Clear();
            if (summaryRoot != null) summaryRoot.SetActive(false);
            if (detailRoot != null) detailRoot.SetActive(false);
            MonsterPreviewPresentation.DestroyOwned(summaryRoot);
            MonsterPreviewPresentation.DestroyOwned(detailRoot);
            summaryRoot = detailRoot = null;
            for (var index = restore.Count - 1; index >= 0; index--) restore[index]();
            restore.Clear();
            statIncreases.Clear();
            statValues.Clear();
            if (grid != null) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)grid.transform);
        }
    }
}
