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
    /// <summary>
    /// PF_MonsterManagementPage에 저작된 스킬/능력치 UI에 데이터와 입력 동작만 연결한다.
    /// 이 클래스는 런타임에 UI를 생성하거나 RectTransform 배치를 변경하지 않는다.
    /// </summary>
    public sealed class MonsterManagementSkillLayout : IDisposable
    {
        private static readonly string[] PassiveIconKeys =
        {
            "passive_courage_aura", "passive_crisis_defense", "passive_entry_shield", "passive_first_wave",
            "passive_formation_bond", "passive_impact_strike", "passive_kain_duality", "passive_kill_heal",
            "passive_long_range_aim", "passive_low_hp_hunter", "passive_nth_hit_heal", "passive_nth_hit_power",
            "passive_ranged_hunter", "passive_same_target_haste", "passive_weakpoint_stack"
        };

        private readonly Dictionary<TMP_Text, TMP_Text> statIncreases = new Dictionary<TMP_Text, TMP_Text>();
        private readonly Dictionary<TMP_Text, TMP_Text> statValues = new Dictionary<TMP_Text, TMP_Text>();
        private readonly GameObject statsRoot;
        private readonly GameObject summaryRoot;
        private readonly GameObject detailRoot;
        private readonly TMP_Text detailTitle;
        private readonly TMP_Text detailCategory;
        private readonly Image detailIcon;
        private readonly GameObject detailNormalIcon;
        private readonly GameObject detailEmptyIcon;
        private readonly Button detailClose;
        private readonly Button outsideClose;
        private readonly TMP_Text detailBody;
        private readonly TMP_Text detailHint;
        private readonly ScrollRect detailScroll;
        private readonly SkillRow passiveRow;
        private readonly SkillRow activeRow;
        private GameObject previousSelection;
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
            public Button Button;
            public SkillSummary Summary;
        }

        private MonsterManagementSkillLayout(
            GameObject statsRoot,
            GameObject summaryRoot,
            GameObject detailRoot,
            TMP_Text detailTitle,
            TMP_Text detailCategory,
            Image detailIcon,
            GameObject detailNormalIcon,
            GameObject detailEmptyIcon,
            Button detailClose,
            Button outsideClose,
            TMP_Text detailBody,
            TMP_Text detailHint,
            ScrollRect detailScroll,
            SkillRow passiveRow,
            SkillRow activeRow)
        {
            this.statsRoot = statsRoot;
            this.summaryRoot = summaryRoot;
            this.detailRoot = detailRoot;
            this.detailTitle = detailTitle;
            this.detailCategory = detailCategory;
            this.detailIcon = detailIcon;
            this.detailNormalIcon = detailNormalIcon;
            this.detailEmptyIcon = detailEmptyIcon;
            this.detailClose = detailClose;
            this.outsideClose = outsideClose;
            this.detailBody = detailBody;
            this.detailHint = detailHint;
            this.detailScroll = detailScroll;
            this.passiveRow = passiveRow;
            this.activeRow = activeRow;

            passiveRow.Button.onClick.AddListener(ShowPassiveDetail);
            activeRow.Button.onClick.AddListener(ShowActiveDetail);
            detailClose.onClick.AddListener(CloseDetail);
            outsideClose.onClick.AddListener(CloseDetail);
            CloseDetail();
        }

        public static MonsterManagementSkillLayout Create(GameObject growthContent, TMP_Text textStyle)
        {
            if (growthContent == null) return null;

            var originalPanel = growthContent.transform.Find("StatsPanel");
            var originalGrid = originalPanel != null
                ? originalPanel.GetComponentInChildren<GridLayoutGroup>(true)
                : null;
            var stats = growthContent.transform.Find("MonsterStats_Runtime");
            var summary = growthContent.transform.Find("MonsterSkillSummary_Runtime");
            var displayGrid = stats != null ? stats.GetComponent<GridLayoutGroup>() : null;
            var page = growthContent.GetComponentInParent<MonsterManagementPageController>(true);
            var detail = page != null ? page.transform.Find("MonsterSkillDetail_Runtime") : null;

            if (originalGrid == null || stats == null || summary == null || displayGrid == null || detail == null)
            {
                Debug.LogError("PF_MonsterManagementPage에 정식 몬스터 관리 UI가 없습니다.", growthContent);
                return null;
            }

            if (originalGrid.transform.childCount != displayGrid.transform.childCount)
            {
                Debug.LogError("PF_MonsterManagementPage 능력치 슬롯 개수가 일치하지 않습니다.", growthContent);
                return null;
            }

            try
            {
                var passive = ReadRow(summary, "PassiveSkill", "패시브");
                var active = ReadRow(summary, "ActiveSkill", "액티브");
                var panel = Required(detail, "Panel");
                var viewport = Required<RectTransform>(panel, "DescriptionViewport");
                var layout = new MonsterManagementSkillLayout(
                    stats.gameObject,
                    summary.gameObject,
                    detail.gameObject,
                    Required<TMP_Text>(panel, "Title"),
                    Required<TMP_Text>(panel, "Category"),
                    Required<Image>(panel, "SkillIconFrame/Normal/Bg(Mask)/Skill"),
                    Required(panel, "SkillIconFrame/Normal").gameObject,
                    Required(panel, "SkillIconFrame/Empty").gameObject,
                    Required<Button>(panel, "Close"),
                    Required<Button>(detail, "OutsideClose"),
                    Required<TMP_Text>(viewport, "FullDescription"),
                    Required<TMP_Text>(panel, "ScrollHint"),
                    viewport.GetComponent<ScrollRect>(),
                    passive,
                    active);

                if (layout.detailScroll == null)
                    throw new InvalidOperationException("Monster skill detail ScrollRect is missing.");

                for (var index = 0; index < originalGrid.transform.childCount; index++)
                {
                    var sourceCard = originalGrid.transform.GetChild(index);
                    var displayCard = displayGrid.transform.GetChild(index);
                    var sourceValue = Required<TMP_Text>(sourceCard, "Value");
                    layout.statValues.Add(sourceValue, Required<TMP_Text>(displayCard, "Text_Value"));
                    layout.statIncreases.Add(sourceValue, Required<TMP_Text>(displayCard, "LevelUpDelta_Runtime"));
                }

                return layout;
            }
            catch (Exception exception)
            {
                Debug.LogError("PF_MonsterManagementPage 바인딩 실패: " + exception.Message, growthContent);
                return null;
            }
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
                ? "상세 설명이 아직 등록되지 않았습니다."
                : skill.Description;
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
            return skillId;
        }

        public void Bind(MonsterRarityCatalog catalog, string monsterId, bool visible)
        {
            if (selectedMonsterId != monsterId || !visible) CloseDetail();
            selectedMonsterId = monsterId;
            var show = visible && !string.IsNullOrEmpty(monsterId);
            summaryRoot.SetActive(show);
            statsRoot.SetActive(show);

            if (!show) return;
            if (catalog == null || !catalog.TryGetRarity(monsterId, out var rarity))
            {
                var missing = new SkillSummary("정보 없음", "몬스터의 스킬 정보를 찾을 수 없습니다.");
                BindRow(passiveRow, missing);
                BindRow(activeRow, missing);
                return;
            }

            catalog.TryGetSkillLoadout(monsterId, out var passive, out var active);
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
            var increase = next - current;
            return increase == 0m
                ? string.Empty
                : increase.ToString("+0.##;-0.##;0", CultureInfo.CurrentCulture) + (percent ? "%p" : string.Empty);
        }

        public bool TrySetStatComparison(TMP_Text target, string currentValue, string nextValue)
        {
            if (target == null || !statIncreases.TryGetValue(target, out var increase)) return false;
            if (!statValues.TryGetValue(target, out var display)) return false;
            display.text = currentValue;
            increase.text = FormatStatIncrease(currentValue, nextValue);
            increase.color = increase.text.StartsWith("-", StringComparison.Ordinal)
                ? new Color32(218, 148, 131, 255)
                : new Color32(166, 196, 110, 255);
            return true;
        }

        public bool TryCloseDetail()
        {
            if (!detailRoot.activeSelf) return false;
            CloseDetail();
            return true;
        }

        public void Dispose()
        {
            passiveRow.Button.onClick.RemoveListener(ShowPassiveDetail);
            activeRow.Button.onClick.RemoveListener(ShowActiveDetail);
            detailClose.onClick.RemoveListener(CloseDetail);
            outsideClose.onClick.RemoveListener(CloseDetail);
            CloseDetail();
            summaryRoot.SetActive(false);
            statsRoot.SetActive(false);
            statIncreases.Clear();
            statValues.Clear();
        }

        private static SkillRow ReadRow(Transform summary, string path, string category)
        {
            var row = Required(summary, path);
            return new SkillRow
            {
                Category = category,
                Name = Required<TMP_Text>(row, "Text_SkillName"),
                Description = Required<TMP_Text>(row, "Text_SkillDescription"),
                CategoryLabel = Required<TMP_Text>(row, "Category"),
                Icon = Required<Image>(row, "SkillFrame_01/Normal/Bg(Mask)/Skill"),
                NormalIcon = Required(row, "SkillFrame_01/Normal").gameObject,
                EmptyIcon = Required(row, "SkillFrame_01/Empty").gameObject,
                Button = row.GetComponent<Button>() ?? throw new InvalidOperationException(path + " Button is missing.")
            };
        }

        private static Transform Required(Transform root, string path)
        {
            var found = root != null ? root.Find(path) : null;
            return found != null ? found : throw new InvalidOperationException(path + " is missing.");
        }

        private static T Required<T>(Transform root, string path) where T : Component
        {
            var component = Required(root, path).GetComponent<T>();
            return component != null ? component : throw new InvalidOperationException(path + " " + typeof(T).Name + " is missing.");
        }

        private static void BindRow(SkillRow row, SkillSummary summary)
        {
            row.Summary = summary;
            row.Name.text = summary.Name;
            row.Description.text = summary.Description;
            row.Icon.sprite = summary.Icon;
            row.NormalIcon.SetActive(summary.Icon != null);
            row.EmptyIcon.SetActive(summary.Icon == null);
        }

        private void ShowPassiveDetail()
        {
            ShowDetail(passiveRow);
        }

        private void ShowActiveDetail()
        {
            ShowDetail(activeRow);
        }

        private void ShowDetail(SkillRow row)
        {
            if (openedRow == row && detailRoot.activeSelf)
            {
                CloseDetail();
                return;
            }

            if (!detailRoot.activeSelf)
                previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

            openedRow = row;
            detailTitle.text = row.Summary.Name;
            detailCategory.text = row.Category;
            detailCategory.color = row.CategoryLabel.color;
            detailIcon.sprite = row.Summary.Icon;
            detailNormalIcon.SetActive(row.Summary.Icon != null);
            detailEmptyIcon.SetActive(row.Summary.Icon == null);
            detailBody.text = row.Summary.Description;
            detailRoot.SetActive(true);
            Canvas.ForceUpdateCanvases();
            detailHint.gameObject.SetActive(detailBody.preferredHeight > detailScroll.viewport.rect.height);
            detailScroll.StopMovement();
            detailScroll.verticalNormalizedPosition = 1f;
            EventSystem.current?.SetSelectedGameObject(detailClose.gameObject);
        }

        private void CloseDetail()
        {
            openedRow = null;
            detailRoot.SetActive(false);
            if (previousSelection != null && previousSelection.activeInHierarchy)
                EventSystem.current?.SetSelectedGameObject(previousSelection);
            previousSelection = null;
        }
    }
}
