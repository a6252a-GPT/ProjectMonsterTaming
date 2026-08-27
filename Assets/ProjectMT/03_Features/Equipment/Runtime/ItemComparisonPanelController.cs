using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.UI;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Equipment
{
    // 장비 인벤토리에서 아이템을 클릭하면 뜨는 상세 옵션 팝업(PF_ItemComparison) 컨트롤러.
    // Popup_1: 같은 부위에 장착 중인 장비가 있으면 항상 그 장비를 보여준다(없으면 클릭한 장비를 보여줌).
    // Popup_2: 장착 중인 장비와 다른 걸 클릭했을 때만 추가로 켜서, 새로 클릭한 장비를 장착 중인
    // 장비와 비교(추가옵션 우/열세·신규, 전투력 변화)해서 보여준다.
    [DisallowMultipleComponent]
    public sealed class ItemComparisonPanelController : MonoBehaviour
    {
        private const int MaxCoreOptionRows = 3;
        private const int MaxBonusOptionRows = 4;
        private const int MaxCompareOptionRows = 4;
        private const string EquippedRibbonLabel = "장착된 장비";
        private const string SelectedRibbonLabel = "선택한 장비";

        // 전투력 변화 추정용 가상 기준치(실제 파티 스탯과 무관, 장비 한 칸 교체 효과만 비교하기 위한 값).
        private const float BaselineDamage = 100f;
        private const float BaselineMaxHealth = 1000f;
        private const float BaselineDefense = 50f;
        private const float BaselineAttackInterval = 1f;
        private const float BaselineCriticalRate = 0.05f;
        private const float BaselineCriticalDamageMultiplier = 1.5f;

        private Transform popup1;
        private Transform popup2;
        private Transform dimmed;
        private Image decoLineImage;
        private Image frameBgImage;
        private Image frameInnerBorderImage;
        private Image iconImage;
        private TMP_Text itemNameText;
        private TMP_Text gradeText;
        private TMP_Text partText;
        private readonly List<Transform> coreOptionRows = new List<Transform>();
        private readonly List<TMP_Text> coreOptionNameTexts = new List<TMP_Text>();
        private readonly List<TMP_Text> coreOptionValueTexts = new List<TMP_Text>();
        private readonly List<Transform> bonusOptionRows = new List<Transform>();
        private readonly List<TMP_Text> bonusOptionNameTexts = new List<TMP_Text>();
        private readonly List<TMP_Text> bonusOptionValueTexts = new List<TMP_Text>();

        // Popup_1 상단 "장착된 장비" 리본(클릭한 아이템이 이미 장착 중일 때만 노출)
        private Transform equippedRibbon;
        private TMP_Text equippedRibbonText;
        private TMP_Text equippedRibbonTextShadow;

        // Popup_2: 같은 부위 장착 아이템과의 비교 패널
        private Image compareDecoLineImage;
        private Image compareFrameBgImage;
        private Image compareFrameInnerBorderImage;
        private Image compareIconImage;
        private TMP_Text compareItemNameText;
        private TMP_Text compareGradeText;
        private TMP_Text comparePartText;
        private TMP_Text comparePowerDeltaText;
        private readonly List<Transform> compareCoreOptionRows = new List<Transform>();
        private readonly List<TMP_Text> compareCoreOptionNameTexts = new List<TMP_Text>();
        private readonly List<TMP_Text> compareCoreOptionValueTexts = new List<TMP_Text>();
        private readonly List<RectTransform> compareCoreOptionArrowIcons = new List<RectTransform>();
        private readonly List<Transform> compareOptionRows = new List<Transform>();
        private readonly List<TMP_Text> compareOptionNameTexts = new List<TMP_Text>();
        private readonly List<TMP_Text> compareOptionValueTexts = new List<TMP_Text>();
        private readonly List<RectTransform> compareOptionArrowIcons = new List<RectTransform>();
        private readonly List<GameObject> compareOptionNewBadges = new List<GameObject>();

        // Popup_1/Popup_2 공통 "장착" 버튼(Group_Buttons)
        private Transform popup1EquipButtonRoot;
        private Transform popup2EquipButtonRoot;

        private bool referencesResolved;
        private string currentInstanceId;
        private bool equipRequestInFlight;
        private Action onEquipped;

        // MainBattleSceneRoot -> EquipmentPageController를 거쳐 내려오는 콜백. 장착/교체 후
        // 다음 전투용 파티 스탯을 즉시 갱신하기 위해 기존 장착 버튼과 동일하게 호출해준다.
        public void Configure(Action onEquippedCallback)
        {
            onEquipped = onEquippedCallback;
        }

        public void Show(EquipmentItemView clickedItem, Sprite icon)
        {
            if (clickedItem.Definition == null)
            {
                return;
            }

            EnsureReferences();
            currentInstanceId = clickedItem.InstanceId;
            gameObject.SetActive(true);
            popup1?.gameObject.SetActive(true);
            dimmed?.gameObject.SetActive(true);

            // 같은 부위에 클릭한 것과 다른 장비가 이미 장착 중이면 Popup_1은 항상 "장착 중인 장비"를
            // 기준으로 보여주고, Popup_2에서 새로 클릭한 장비를 그 기준과 비교해서 보여준다.
            // 장착된 게 없거나 클릭한 게 이미 장착 중인 장비면 Popup_1이 그 클릭한 장비를 그대로 보여준다.
            var hasComparison = EquipmentInventoryRuntime.TryGetEquipped(clickedItem.Part, out var equippedItem)
                && equippedItem.Definition != null
                && equippedItem.InstanceId != clickedItem.InstanceId;

            var referenceItem = hasComparison ? equippedItem : clickedItem;
            ApplyPopup1(referenceItem, icon);

            if (hasComparison)
            {
                ApplyComparePanel(clickedItem, equippedItem, icon);
            }
            else
            {
                popup2?.gameObject.SetActive(false);
            }
        }

        // Popup_1: 장착 중인 장비가 있으면 그 장비를, 없으면 클릭한 장비를 그대로 보여준다.
        private void ApplyPopup1(EquipmentItemView item, Sprite icon)
        {
            var gradeColor = ItemGradeFramePalette.GetColor(item.Grade);
            SetColor(decoLineImage, gradeColor);
            SetColor(frameBgImage, gradeColor);
            SetColor(frameInnerBorderImage, gradeColor);

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.color = Color.white;
                iconImage.enabled = icon != null;
            }

            if (itemNameText != null)
            {
                itemNameText.text = item.Definition.DisplayName;
            }

            if (gradeText != null)
            {
                gradeText.text = EquipmentGradeInfo.GetDisplayName(item.Grade);
                gradeText.color = gradeColor;
            }

            if (partText != null)
            {
                partText.text = EquipmentPartInfo.GetDisplayName(item.Part);
            }

            ApplyCoreOptions(item.Definition.CoreStatContributions);
            ApplyBonusOptions(item.Instance?.RandomOptions);
            ApplyEquippedRibbon(item);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // Group_Buttons(장착) 클릭 시 지금 보고 있는 아이템을 장착한다. 같은 부위에 다른 장비가
        // 이미 장착돼 있으면 EquipmentInventoryRuntime.TryEquipAsync가 자동으로 교체해준다.
        private async void HandleEquipButtonClicked()
        {
            if (equipRequestInFlight || string.IsNullOrEmpty(currentInstanceId))
            {
                return;
            }

            equipRequestInFlight = true;
            try
            {
                if (await EquipmentInventoryRuntime.TryEquipAsync(currentInstanceId))
                {
                    onEquipped?.Invoke();
                    Hide();
                }
            }
            finally
            {
                equipRequestInFlight = false;
            }
        }

        private void ApplyCoreOptions(IReadOnlyList<EquipmentStatContribution> contributions)
        {
            var count = contributions?.Count ?? 0;
            for (var i = 0; i < coreOptionRows.Count; i++)
            {
                var row = coreOptionRows[i];
                if (row == null)
                {
                    continue;
                }

                var visible = i < count;
                row.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var contribution = contributions[i];
                if (coreOptionNameTexts[i] != null)
                {
                    coreOptionNameTexts[i].text = EquipmentGradeStatTable.GetStatDisplayName(contribution.StatType);
                }

                if (coreOptionValueTexts[i] != null)
                {
                    coreOptionValueTexts[i].text = $"+{contribution.Value:0.0}%";
                }
            }
        }

        private void ApplyBonusOptions(IReadOnlyList<EquipmentOptionRollData> options)
        {
            var count = options?.Count ?? 0;
            for (var i = 0; i < bonusOptionRows.Count; i++)
            {
                var row = bonusOptionRows[i];
                if (row == null)
                {
                    continue;
                }

                var visible = i < count;
                row.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var option = options[i];
                if (bonusOptionNameTexts[i] != null)
                {
                    bonusOptionNameTexts[i].text = EquipmentOptionInfo.GetDisplayName(option.Type);
                }

                if (bonusOptionValueTexts[i] != null)
                {
                    bonusOptionValueTexts[i].text = $"+{option.Value:0.0}%";
                }
            }
        }

        // 클릭한 아이템이 지금 그 부위에 장착 중이면 "장착된 장비", 아니면 "선택한 장비"를 표시한다.
        private void ApplyEquippedRibbon(EquipmentItemView item)
        {
            if (equippedRibbon == null)
            {
                return;
            }

            equippedRibbon.gameObject.SetActive(true);
            var label = item.IsEquipped ? EquippedRibbonLabel : SelectedRibbonLabel;

            if (equippedRibbonText != null)
            {
                equippedRibbonText.text = label;
            }

            if (equippedRibbonTextShadow != null)
            {
                equippedRibbonTextShadow.text = label;
            }
        }

        // Popup_2: 장착 중인 장비와 다른 걸 클릭했을 때만 켜서, 새로 클릭한 장비를 보여주고
        // 장착 중인 장비(Popup_1) 대비 추가옵션 우열/신규 여부를 표시한다.
        private void ApplyComparePanel(EquipmentItemView clickedItem, EquipmentItemView equippedItem, Sprite icon)
        {
            if (popup2 == null)
            {
                return;
            }

            popup2.gameObject.SetActive(true);

            var gradeColor = ItemGradeFramePalette.GetColor(clickedItem.Grade);
            SetColor(compareDecoLineImage, gradeColor);
            SetColor(compareFrameBgImage, gradeColor);
            SetColor(compareFrameInnerBorderImage, gradeColor);

            if (compareIconImage != null)
            {
                compareIconImage.sprite = icon;
                compareIconImage.color = Color.white;
                compareIconImage.enabled = icon != null;
            }

            if (compareItemNameText != null)
            {
                compareItemNameText.text = clickedItem.Definition.DisplayName;
            }

            if (compareGradeText != null)
            {
                compareGradeText.text = EquipmentGradeInfo.GetDisplayName(clickedItem.Grade);
                compareGradeText.color = gradeColor;
            }

            if (comparePartText != null)
            {
                comparePartText.text = EquipmentPartInfo.GetDisplayName(clickedItem.Part);
            }

            ApplyCompareCoreOptions(clickedItem, equippedItem);
            ApplyCompareOptions(clickedItem, equippedItem);
            ApplyPowerDelta(equippedItem, clickedItem);
        }

        // Option_1~3: 새로 클릭한 장비의 기본옵션을 쓰고, 장착 중인 장비의 같은 종류 기본옵션과
        // 비교해 화살표(우세/열세)를 붙인다. 같은 부위끼리는 기본옵션 종류가 대개 같지만, 다를 때는
        // 비교 대상이 없다는 뜻이므로 화살표를 숨긴다.
        private void ApplyCompareCoreOptions(EquipmentItemView clickedItem, EquipmentItemView equippedItem)
        {
            var clickedCore = clickedItem.Definition?.CoreStatContributions;
            var count = clickedCore?.Count ?? 0;
            var equippedCore = equippedItem.Definition?.CoreStatContributions;

            for (var i = 0; i < compareCoreOptionRows.Count; i++)
            {
                var row = compareCoreOptionRows[i];
                if (row == null)
                {
                    continue;
                }

                var visible = i < count;
                row.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var clickedContribution = clickedCore[i];
                if (compareCoreOptionNameTexts[i] != null)
                {
                    compareCoreOptionNameTexts[i].text = EquipmentGradeStatTable.GetStatDisplayName(clickedContribution.StatType);
                }

                if (compareCoreOptionValueTexts[i] != null)
                {
                    compareCoreOptionValueTexts[i].text = $"+{clickedContribution.Value:0.0}%";
                }

                ApplyCompareCoreArrow(i, clickedContribution, equippedCore);
            }
        }

        private void ApplyCompareCoreArrow(
            int rowIndex,
            EquipmentStatContribution clickedContribution,
            IReadOnlyList<EquipmentStatContribution> equippedCore)
        {
            var arrow = rowIndex < compareCoreOptionArrowIcons.Count ? compareCoreOptionArrowIcons[rowIndex] : null;
            if (arrow == null)
            {
                return;
            }

            EquipmentStatContribution? matchedCore = null;
            if (equippedCore != null)
            {
                for (var i = 0; i < equippedCore.Count; i++)
                {
                    if (equippedCore[i].StatType == clickedContribution.StatType)
                    {
                        matchedCore = equippedCore[i];
                        break;
                    }
                }
            }

            if (!matchedCore.HasValue)
            {
                // 장착 중인 장비에 같은 종류의 기본옵션이 없음 - 비교 대상이 없으니 화살표를 숨긴다.
                arrow.gameObject.SetActive(false);
                return;
            }

            var matchedValue = matchedCore.Value.Value;
            if (Mathf.Approximately(matchedValue, clickedContribution.Value))
            {
                arrow.gameObject.SetActive(false);
                return;
            }

            arrow.gameObject.SetActive(true);
            var euler = arrow.localEulerAngles;
            euler.z = clickedContribution.Value > matchedValue ? 0f : 180f; // 더 낮으면 180도 뒤집어 아래로 표시
            arrow.localEulerAngles = euler;
        }

        // BonusOption_1~4: 새로 클릭한 장비의 추가옵션을 쓰고, 장착 중인 장비의 같은 종류 옵션과 비교해
        // 화살표(우세/열세) 또는 "NEW"(장착 중인 장비에 같은 종류가 없음) 표시를 붙인다.
        private void ApplyCompareOptions(EquipmentItemView clickedItem, EquipmentItemView equippedItem)
        {
            var clickedOptions = clickedItem.Instance?.RandomOptions;
            var count = clickedOptions?.Count ?? 0;
            var equippedOptions = equippedItem.Instance?.RandomOptions;

            for (var i = 0; i < compareOptionRows.Count; i++)
            {
                var row = compareOptionRows[i];
                if (row == null)
                {
                    continue;
                }

                var visible = i < count;
                row.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var clickedOption = clickedOptions[i];
                if (compareOptionNameTexts[i] != null)
                {
                    compareOptionNameTexts[i].text = EquipmentOptionInfo.GetDisplayName(clickedOption.Type);
                }

                if (compareOptionValueTexts[i] != null)
                {
                    compareOptionValueTexts[i].text = $"+{clickedOption.Value:0.0}%";
                }

                ApplyCompareArrow(i, clickedOption, equippedOptions);
            }
        }

        private void ApplyCompareArrow(
            int rowIndex,
            EquipmentOptionRollData clickedOption,
            IReadOnlyList<EquipmentOptionRollData> equippedOptions)
        {
            var arrow = rowIndex < compareOptionArrowIcons.Count ? compareOptionArrowIcons[rowIndex] : null;
            var newBadge = rowIndex < compareOptionNewBadges.Count ? compareOptionNewBadges[rowIndex] : null;

            EquipmentOptionRollData matched = null;
            if (equippedOptions != null)
            {
                for (var i = 0; i < equippedOptions.Count; i++)
                {
                    if (equippedOptions[i] != null && equippedOptions[i].Type == clickedOption.Type)
                    {
                        matched = equippedOptions[i];
                        break;
                    }
                }
            }

            if (matched == null)
            {
                // 장착 중인 장비에 같은 종류의 추가옵션이 없음 - 화살표 대신 "NEW"
                arrow?.gameObject.SetActive(false);
                newBadge?.SetActive(true);
                return;
            }

            newBadge?.SetActive(false);
            if (arrow == null)
            {
                return;
            }

            if (Mathf.Approximately(clickedOption.Value, matched.Value))
            {
                arrow.gameObject.SetActive(false);
                return;
            }

            arrow.gameObject.SetActive(true);
            var euler = arrow.localEulerAngles;
            euler.z = clickedOption.Value > matched.Value ? 0f : 180f; // 더 낮으면 180도 뒤집어 아래로 표시
            arrow.localEulerAngles = euler;
        }

        // 장착 중인 장비 -> 클릭한 장비로 바꿨을 때의 전투력 변화를 "+120" 형식으로 표시한다.
        // 실제 파티 스탯과는 무관하게, 이 장비 한 칸의 기여도 차이만 가상 기준치에 적용해 추정한다.
        private void ApplyPowerDelta(EquipmentItemView equippedItem, EquipmentItemView clickedItem)
        {
            if (comparePowerDeltaText == null)
            {
                return;
            }

            var currentTotal = EquipmentLegionBonusCalculator.CalculateTotal();
            var equippedOnly = BuildItemBonus(equippedItem);
            var clickedOnly = BuildItemBonus(clickedItem);
            var hypotheticalTotal = Add(Subtract(currentTotal, equippedOnly), clickedOnly);

            var beforePower = BuildBaselineSnapshot(currentTotal).EstimatePower();
            var afterPower = BuildBaselineSnapshot(hypotheticalTotal).EstimatePower();
            var delta = Mathf.RoundToInt(afterPower - beforePower);
            var sign = delta >= 0 ? "+" : "";
            comparePowerDeltaText.text = $"{sign}{delta:N0}";
        }

        private static UnitStatsSnapshot BuildBaselineSnapshot(EquipmentLegionBonus bonus)
        {
            return new UnitStatsSnapshot
            {
                damage = BaselineDamage * (1f + bonus.AttackPower / 100f),
                maxHealth = BaselineMaxHealth * (1f + bonus.MaxHealth / 100f),
                defense = BaselineDefense * (1f + bonus.Defense / 100f),
                attackInterval = BaselineAttackInterval / (1f + Mathf.Max(0f, bonus.AttackSpeed) / 100f),
                criticalRate = BaselineCriticalRate + bonus.CriticalRate / 100f,
                criticalDamageMultiplier = BaselineCriticalDamageMultiplier + bonus.CriticalDamage / 100f,
                damageReductionRate = Mathf.Clamp01(bonus.DamageReduction / 100f)
            };
        }

        private static EquipmentLegionBonus BuildItemBonus(EquipmentItemView item)
        {
            var bonus = default(EquipmentLegionBonus);
            if (item.Definition == null)
            {
                return bonus;
            }

            AccumulateInto(ref bonus, item.Definition.CoreStatContributions);
            var options = item.Instance?.RandomOptions;
            if (options == null)
            {
                return bonus;
            }

            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                if (option != null)
                {
                    AccumulateInto(ref bonus, EquipmentOptionInfo.ResolveContributions(option.Type, option.Value));
                }
            }

            return bonus;
        }

        private static void AccumulateInto(ref EquipmentLegionBonus bonus, IReadOnlyList<EquipmentStatContribution> contributions)
        {
            if (contributions == null)
            {
                return;
            }

            for (var i = 0; i < contributions.Count; i++)
            {
                var c = contributions[i];
                switch (c.StatType)
                {
                    case EquipmentStatType.AttackPower: bonus.AttackPower += c.Value; break;
                    case EquipmentStatType.MaxHealth: bonus.MaxHealth += c.Value; break;
                    case EquipmentStatType.Defense: bonus.Defense += c.Value; break;
                    case EquipmentStatType.AttackSpeed: bonus.AttackSpeed += c.Value; break;
                    case EquipmentStatType.MoveSpeed: bonus.MoveSpeed += c.Value; break;
                    case EquipmentStatType.CriticalRate: bonus.CriticalRate += c.Value; break;
                    case EquipmentStatType.CriticalDamage: bonus.CriticalDamage += c.Value; break;
                    case EquipmentStatType.SkillDamage: bonus.SkillDamage += c.Value; break;
                    case EquipmentStatType.BossDamage: bonus.BossDamage += c.Value; break;
                    case EquipmentStatType.NormalMonsterDamage: bonus.NormalMonsterDamage += c.Value; break;
                    case EquipmentStatType.SkillCooldownReduction: bonus.SkillCooldownReduction += c.Value; break;
                    case EquipmentStatType.DefensePenetration: bonus.DefensePenetration += c.Value; break;
                    case EquipmentStatType.DamageReduction: bonus.DamageReduction += c.Value; break;
                }
            }
        }

        private static EquipmentLegionBonus Subtract(EquipmentLegionBonus a, EquipmentLegionBonus b)
        {
            a.AttackPower -= b.AttackPower;
            a.MaxHealth -= b.MaxHealth;
            a.Defense -= b.Defense;
            a.AttackSpeed -= b.AttackSpeed;
            a.MoveSpeed -= b.MoveSpeed;
            a.CriticalRate -= b.CriticalRate;
            a.CriticalDamage -= b.CriticalDamage;
            a.SkillDamage -= b.SkillDamage;
            a.BossDamage -= b.BossDamage;
            a.NormalMonsterDamage -= b.NormalMonsterDamage;
            a.SkillCooldownReduction -= b.SkillCooldownReduction;
            a.DefensePenetration -= b.DefensePenetration;
            a.DamageReduction -= b.DamageReduction;
            return a;
        }

        private static EquipmentLegionBonus Add(EquipmentLegionBonus a, EquipmentLegionBonus b)
        {
            a.AttackPower += b.AttackPower;
            a.MaxHealth += b.MaxHealth;
            a.Defense += b.Defense;
            a.AttackSpeed += b.AttackSpeed;
            a.MoveSpeed += b.MoveSpeed;
            a.CriticalRate += b.CriticalRate;
            a.CriticalDamage += b.CriticalDamage;
            a.SkillDamage += b.SkillDamage;
            a.BossDamage += b.BossDamage;
            a.NormalMonsterDamage += b.NormalMonsterDamage;
            a.SkillCooldownReduction += b.SkillCooldownReduction;
            a.DefensePenetration += b.DefensePenetration;
            a.DamageReduction += b.DamageReduction;
            return a;
        }

        // ---------------------------------------------------------------
        // 참조 탐색 (최초 Show() 호출 시 1회만 수행)
        // ---------------------------------------------------------------

        private void EnsureReferences()
        {
            if (referencesResolved)
            {
                return;
            }

            referencesResolved = true;

            popup1 = FindDeep(transform, "Popup_1");
            popup2 = FindDeep(transform, "Popup_2");
            dimmed = FindDeep(transform, "Dimmed");

            ResolvePopup1(popup1);
            ResolvePopup2(popup2);

            WireDimmedClose();
        }

        private void ResolvePopup1(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var decoLineBox = FindDeep(root, "Popup_Box_02_DecoLine_Basic_Plum");
            decoLineImage = decoLineBox?.Find("DecoLine")?.GetComponent<Image>();

            var itemSlot = FindDeep(root, "ItemSlot");
            var frame = itemSlot != null ? FindDeep(itemSlot, "ItemFrame_01_Normal_Plum") : null;
            frameBgImage = frame?.Find("Bg")?.GetComponent<Image>();
            frameInnerBorderImage = frame?.Find("InnerBorder1")?.GetComponent<Image>();
            iconImage = itemSlot != null ? itemSlot.Find("Icon")?.GetComponent<Image>() : null;

            var group1 = FindDeep(root, "Group_1");
            itemNameText = group1?.Find("Text_ItemName")?.GetComponent<TMP_Text>();
            gradeText = group1?.Find("Text_Grade")?.GetComponent<TMP_Text>();
            partText = group1?.Find("Text")?.GetComponent<TMP_Text>();

            var group2 = FindDeep(root, "Group_2");
            equippedRibbon = group2 != null ? FindDeep(group2, "Title_Ribbon_01") : null;
            if (equippedRibbon != null)
            {
                equippedRibbonText = equippedRibbon.Find("Text (TMP)/GroupArea/Group/Text")?.GetComponent<TMP_Text>()
                    ?? FindDeep(equippedRibbon, "Text")?.GetComponent<TMP_Text>();
                equippedRibbonTextShadow = FindDeep(equippedRibbon, "Text (1)")?.GetComponent<TMP_Text>();
            }

            var group3 = FindDeep(root, "Group_3");

            coreOptionRows.Clear();
            coreOptionNameTexts.Clear();
            coreOptionValueTexts.Clear();
            for (var i = 1; i <= MaxCoreOptionRows; i++)
            {
                var row = group3 != null ? group3.Find($"Option_{i}") : null;
                coreOptionRows.Add(row);
                coreOptionNameTexts.Add(row?.Find("Text_1")?.GetComponent<TMP_Text>());
                coreOptionValueTexts.Add(FindAnyChildText(row, "Text_2", "StatText_2"));
            }

            bonusOptionRows.Clear();
            bonusOptionNameTexts.Clear();
            bonusOptionValueTexts.Clear();
            for (var i = 1; i <= MaxBonusOptionRows; i++)
            {
                var row = group3 != null ? group3.Find($"BonusOption_{i}") : null;
                bonusOptionRows.Add(row);
                bonusOptionNameTexts.Add(row?.Find("OptionText_1")?.GetComponent<TMP_Text>());
                bonusOptionValueTexts.Add(row?.Find("StatText_2")?.GetComponent<TMP_Text>());
            }

            popup1EquipButtonRoot = FindDeep(root, "Group_Buttons");
            if (popup1EquipButtonRoot != null)
            {
                EnsureButton(popup1EquipButtonRoot).onClick.AddListener(HandleEquipButtonClicked);
            }
        }

        private void ResolvePopup2(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var decoLineBox = FindDeepByPrefix(root, "Popup_Box_02_DecoLine_Basic_");
            compareDecoLineImage = decoLineBox?.Find("DecoLine")?.GetComponent<Image>();

            var itemSlot = FindDeep(root, "ItemSlot");
            var frame = itemSlot != null ? FindDeepByPrefix(itemSlot, "ItemFrame_01_Normal_") : null;
            compareFrameBgImage = frame?.Find("Bg")?.GetComponent<Image>();
            compareFrameInnerBorderImage = frame?.Find("InnerBorder1")?.GetComponent<Image>();
            compareIconImage = itemSlot != null ? itemSlot.Find("Icon")?.GetComponent<Image>() : null;

            var group1 = FindDeep(root, "Group_1");
            compareItemNameText = group1?.Find("Text_ItemName")?.GetComponent<TMP_Text>();
            compareGradeText = group1?.Find("Text_Grade")?.GetComponent<TMP_Text>();
            comparePartText = group1?.Find("Text")?.GetComponent<TMP_Text>();

            var group2 = FindDeep(root, "Group_2");
            comparePowerDeltaText = group2 != null ? FindDeep(group2, "StatText")?.GetComponent<TMP_Text>() : null;

            var group3 = FindDeep(root, "Group_3");

            compareCoreOptionRows.Clear();
            compareCoreOptionNameTexts.Clear();
            compareCoreOptionValueTexts.Clear();
            compareCoreOptionArrowIcons.Clear();
            for (var i = 1; i <= MaxCoreOptionRows; i++)
            {
                var row = group3 != null ? group3.Find($"Option_{i}") : null;
                compareCoreOptionRows.Add(row);
                compareCoreOptionNameTexts.Add(row?.Find("Text_1")?.GetComponent<TMP_Text>());
                compareCoreOptionValueTexts.Add(FindAnyChildText(row, "Text_2", "StatText_2"));
                compareCoreOptionArrowIcons.Add(row?.Find("Icon")?.GetComponent<RectTransform>());
            }

            compareOptionRows.Clear();
            compareOptionNameTexts.Clear();
            compareOptionValueTexts.Clear();
            compareOptionArrowIcons.Clear();
            compareOptionNewBadges.Clear();
            for (var i = 1; i <= MaxCompareOptionRows; i++)
            {
                var row = group3 != null ? group3.Find($"BonusOption_{i}") : null;
                compareOptionRows.Add(row);
                compareOptionNameTexts.Add(FindAnyChildText(row, "OptionText_1", "Text_1"));
                compareOptionValueTexts.Add(FindAnyChildText(row, "StatText_2", "Text_2"));
                compareOptionArrowIcons.Add(row?.Find("Icon")?.GetComponent<RectTransform>());
                compareOptionNewBadges.Add(row != null ? FindDeep(row, "TextImage_New")?.gameObject : null);
            }

            popup2EquipButtonRoot = FindDeep(root, "Group_Buttons");
            if (popup2EquipButtonRoot != null)
            {
                EnsureButton(popup2EquipButtonRoot).onClick.AddListener(HandleEquipButtonClicked);
            }
        }

        // 카드 영역(Popup_1/Popup_2)을 제외한 바깥(Dimmed)을 클릭하면 패널을 닫는다.
        private void WireDimmedClose()
        {
            if (dimmed == null)
            {
                return;
            }

            var button = EnsureButton(dimmed);
            button.onClick.RemoveListener(Hide);
            button.onClick.AddListener(Hide);
        }

        private static Button EnsureButton(Transform target)
        {
            var button = target.GetComponent<Button>();
            if (button == null)
            {
                button = target.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
            }

            var hitArea = target.GetComponent<Graphic>();
            if (hitArea == null)
            {
                var image = target.gameObject.AddComponent<Image>();
                image.color = Color.clear;
                hitArea = image;
            }

            hitArea.raycastTarget = true;
            if (button.targetGraphic == null)
            {
                button.targetGraphic = hitArea;
            }

            return button;
        }

        private static void SetColor(Image image, Color color)
        {
            if (image != null)
            {
                image.color = color;
            }
        }

        private static Transform FindDeep(Transform root, string childName)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].name == childName)
                {
                    return all[i];
                }
            }

            return null;
        }

        // 등급 접미사(Plum/Yellow/...)가 붙는 오브젝트는 접두사만으로 찾는다(팝업마다 기본 등급이 다름).
        private static Transform FindDeepByPrefix(Transform root, string namePrefix)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].name.StartsWith(namePrefix, StringComparison.Ordinal))
                {
                    return all[i];
                }
            }

            return null;
        }

        // 하이어라키 이름이 바뀔 수 있어 여러 후보를 순서대로 시도한다(직계 자식만 확인).
        private static TMP_Text FindAnyChildText(Transform row, params string[] candidateNames)
        {
            if (row == null)
            {
                return null;
            }

            for (var i = 0; i < candidateNames.Length; i++)
            {
                var text = row.Find(candidateNames[i])?.GetComponent<TMP_Text>();
                if (text != null)
                {
                    return text;
                }
            }

            return null;
        }
    }
}
