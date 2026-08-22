using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Equipment
{
    // 장비 슬롯 강화 패널(PF_EquipmentSlotUpgrade) UI 컨트롤러: 패널 열기/닫기, 부위 탭 전환,
    // 텍스트 갱신, 강화 버튼 클릭 처리를 담당한다. 강화 계산은 EquipmentSlotUpgradeCalculator,
    // 저장 데이터 연동은 EquipmentSlotUpgradeRuntime이 담당한다.
    [DisallowMultipleComponent]
    public sealed class EquipmentSlotUpgradePanelController : MonoBehaviour
    {
        // 닫기 버튼 이름 후보.
        private static readonly string[] CloseButtonNameCandidates = { "UpgradeClose", "UpgradeButton_Close", "Close" };

        // 등급별 완성 프레임 이름 접미사.
        private static readonly Dictionary<EquipmentGrade, string> FrameVariantSuffixByGrade = new Dictionary<EquipmentGrade, string>
        {
            { EquipmentGrade.Common, ItemGradeFramePalette.GetSuffix(EquipmentGrade.Common) },
            { EquipmentGrade.Rare, ItemGradeFramePalette.GetSuffix(EquipmentGrade.Rare) },
            { EquipmentGrade.Epic, ItemGradeFramePalette.GetSuffix(EquipmentGrade.Epic) },
            { EquipmentGrade.Legendary, ItemGradeFramePalette.GetSuffix(EquipmentGrade.Legendary) },
            { EquipmentGrade.Mythic, ItemGradeFramePalette.GetSuffix(EquipmentGrade.Mythic) },
        };

        private const string FrameVariantPrefix = ItemGradeFramePalette.FrameVariantPrefix;
        private const string EmptySlotBackgroundName = "EmptySlotGrayBg";
        private static readonly Color EmptySlotTintColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        // 색이 있는 픽셀로 볼지 판단하는 채도 임계값(오차 보정용).
        private const float ChromaticThreshold = 0.01f;

        private readonly Dictionary<EquipmentPart, Button> selectButtons = new Dictionary<EquipmentPart, Button>();
        private readonly Dictionary<EquipmentPart, GameObject> slotDisplays = new Dictionary<EquipmentPart, GameObject>();
        private readonly Dictionary<EquipmentPart, TMP_Text> levelTexts = new Dictionary<EquipmentPart, TMP_Text>();

        // 부위별 대표 아이콘(장비가 없을 때 재사용).
        private readonly Dictionary<EquipmentPart, Sprite> partIconSprites = new Dictionary<EquipmentPart, Sprite>();
        private readonly Dictionary<string, GameObject> frameVariantTemplates = new Dictionary<string, GameObject>();
        private Transform frameVariantTemplateStorage;

        private Button closeButton;
        private Button upgradeButton;
        private TMP_Text headerText;
        private TMP_Text statText;
        private TMP_Text statText2;
        private TMP_Text totalText;
        private TMP_Text upgradeMaterialText; // 강화 재료(보유/필요) 표시
        private TMP_Text upHeaderStatText; // 현재(다음 1강화) 강화 능력 증가율 표시
        private TMP_Text downHeaderStatText; // 다다음(그 다음 강화) 강화 능력 증가율 표시

        private EquipmentPart currentPart = EquipmentPart.Weapon;
        private bool hasSelectedPart;
        private Action combatInputSaved;

        public event Action<bool> OpenStateChanged;

        private void Awake()
        {
            CacheReferences();

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }

            if (upgradeButton != null)
            {
                upgradeButton.onClick.AddListener(HandleUpgradeButtonClicked);
            }

            LogMissingReferences();
        }

        private void OnEnable()
        {
            EquipmentInventoryRuntime.Changed += HandleDataChanged;
            EquipmentSlotUpgradeRuntime.Changed += HandleDataChanged;
            RefreshAll();
        }

        private void OnDisable()
        {
            EquipmentInventoryRuntime.Changed -= HandleDataChanged;
            EquipmentSlotUpgradeRuntime.Changed -= HandleDataChanged;
        }

        // MainBattleSceneRoot 등 씬 조립 시점에 진행 데이터 서비스를 주입한다.
        public void Configure(IGameProgressService progress, Action onCombatInputSaved = null)
        {
            combatInputSaved = onCombatInputSaved;
            EquipmentSlotUpgradeRuntime.Configure(progress);
        }

        public bool IsOpen => gameObject.activeSelf;

        public void Open()
        {
            if (gameObject.activeSelf)
            {
                return;
            }

            // 이 패널도 군단장 3D 프리뷰(발 IK 고정)를 포함하므로 스케일/이동 없는 FadeOnly를
            // 쓴다. 자세한 이유는 UIPanelPopStyle.FadeOnly 주석 참고.
            UIPanelPopAnimator.RequestOpen(gameObject, UIPanelPopStyle.FadeOnly);
            OpenStateChanged?.Invoke(true);
        }

        public void Close()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            UIPanelPopAnimator.RequestClose(gameObject, () => OpenStateChanged?.Invoke(false));
        }

        public void SelectPart(EquipmentPart part)
        {
            currentPart = part;
            hasSelectedPart = true;

            foreach (var pair in slotDisplays)
            {
                pair.Value?.SetActive(pair.Key == part);
            }

            RefreshDetailPanel();
            RefreshUpgradeButtonState();
        }

        // ---------------------------------------------------------------
        // 참조 탐색 (이름 기반, 프리팹 내부 구조를 몰라도 안전하게 찾을 수 있도록 재귀 탐색한다)
        // ---------------------------------------------------------------

        private void CacheReferences()
        {
            var closeButtonTransform = FindDeepAny(transform, CloseButtonNameCandidates);
            if (closeButtonTransform != null)
            {
                closeButton = EnsureButton(closeButtonTransform);
            }

            headerText = FindDeep(transform, "Header")?.GetComponent<TMP_Text>();
            statText = FindDeep(transform, "StatText")?.GetComponent<TMP_Text>();
            statText2 = FindDeep(transform, "StatText2")?.GetComponent<TMP_Text>();
            totalText = FindDeep(transform, "TotalText")?.GetComponent<TMP_Text>();
            upgradeMaterialText = FindDeep(transform, "UpgradeMaterialText")?.GetComponent<TMP_Text>();
            upHeaderStatText = FindDeep(transform, "UpHeaderStat")?.GetComponent<TMP_Text>();
            downHeaderStatText = FindDeep(transform, "DownHeaderStat")?.GetComponent<TMP_Text>();

            var upgradeButtonTransform = FindDeep(transform, "UpgradeButton");
            if (upgradeButtonTransform != null)
            {
                upgradeButton = EnsureButton(upgradeButtonTransform);
            }

            // 부위별 탭(선택 버튼) / 표시 오브젝트 / 레벨 텍스트 이름. 이름이 여러 벌 존재할 수 있어
            // 후보 배열 순서대로 찾는다.
            CachePart(EquipmentPart.Weapon, new[] { "WeaponBox" }, new[] { "WeaponSlot" }, new[] { "WeaponLvText2", "WeaponLvText" });
            CachePart(EquipmentPart.Helmet,
                new[] { "AccessorySkillBox", "HelmetBox" },
                new[] { "HelmetSlot", "AccessorySlot" },
                new[] { "AccessoryLvText2", "AccessoryLvText", "HelmetLvText2", "HelmetLvText" });
            CachePart(EquipmentPart.Armor, new[] { "ArmorBox" }, new[] { "ArmorSlot" }, new[] { "ArmorLvText2", "ArmorLvText" });
            CachePart(EquipmentPart.Boots, new[] { "BootsBox" }, new[] { "BootsSlot" }, new[] { "BootsLvText2", "BootsLvText" });
            CachePart(EquipmentPart.Glove, new[] { "GloveBox" }, new[] { "GloveSlot" }, new[] { "GloveLvText2", "GloveLvText" });
            CachePart(EquipmentPart.Ring, new[] { "RingBox" }, new[] { "RingSlot" }, new[] { "RingLvText2", "RingLvText" });

            CachePartIconSprites();
            CacheFrameVariantTemplates();
        }

        // 슬롯에 이미 박혀 있는 부위 대표 아이콘 스프라이트를 캐시한다.
        private void CachePartIconSprites()
        {
            foreach (var pair in slotDisplays)
            {
                var image = pair.Value != null ? pair.Value.transform.Find("ItemFrame_01/Item")?.GetComponent<Image>() : null;
                if (image != null && image.sprite != null)
                {
                    partIconSprites[pair.Key] = image.sprite;
                }
            }
        }

        // 부위별 슬롯에 흩어져 있는 등급별 프레임을 이름별로 찾아 보이지 않는 곳에 복제해 둔다.
        private void CacheFrameVariantTemplates()
        {
            var storageObject = new GameObject("EquipmentFrameTemplates(Hidden)");
            storageObject.transform.SetParent(transform, false);
            storageObject.SetActive(false);
            frameVariantTemplateStorage = storageObject.transform;

            var all = transform.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                var name = all[i].name;
                if (!name.StartsWith(FrameVariantPrefix))
                {
                    continue;
                }

                var suffix = name.Substring(FrameVariantPrefix.Length);
                if (frameVariantTemplates.ContainsKey(suffix))
                {
                    continue;
                }

                var clone = Instantiate(all[i].gameObject, frameVariantTemplateStorage);
                clone.name = name;
                frameVariantTemplates[suffix] = clone;
            }
        }

        // 등급에 맞는 프레임 템플릿을 복제해서 normalArea 밑에 끼워 넣는다.
        private void ApplyFrameVariant(Transform normalArea, EquipmentGrade grade)
        {
            if (normalArea == null || !FrameVariantSuffixByGrade.TryGetValue(grade, out var suffix))
            {
                return;
            }

            var desiredName = FrameVariantPrefix + suffix;
            var current = normalArea.childCount > 0 ? normalArea.GetChild(0) : null;
            if (current != null && current.name == desiredName)
            {
                return;
            }

            if (!frameVariantTemplates.TryGetValue(suffix, out var template) || template == null)
            {
                return;
            }

            if (current != null)
            {
                Destroy(current.gameObject);
            }

            var instance = Instantiate(template, normalArea);
            instance.name = desiredName;
            instance.SetActive(true);
            StretchToFillParent(instance.GetComponent<RectTransform>());
        }

        // 장비가 없을 때 normalArea 밑에 끼워 넣는 회색 프레임.
        // 기본 등급 프레임을 그대로 복제해 모양(테두리 등)은 유지하고, 등급색이 들어간 부분만 회색으로 바꾼다.
        private void ApplyEmptyBackground(Transform normalArea)
        {
            if (normalArea == null)
            {
                return;
            }

            var current = normalArea.childCount > 0 ? normalArea.GetChild(0) : null;
            if (current != null && current.name == EmptySlotBackgroundName)
            {
                return;
            }

            if (current != null)
            {
                Destroy(current.gameObject);
            }

            if (!frameVariantTemplates.TryGetValue(FrameVariantSuffixByGrade[EquipmentGrade.Common], out var template) || template == null)
            {
                return;
            }

            var instance = Instantiate(template, normalArea);
            instance.name = EmptySlotBackgroundName;
            instance.SetActive(true);
            StretchToFillParent(instance.GetComponent<RectTransform>());
            GrayOutChromaticImages(instance.transform);
        }

        // 프레임 하위 이미지 중 채도가 있는(등급색이 칠해진) 것만 무채색으로 바꾸고,
        // 이미 무채색인 테두리·음영 부분은 원래 모습 그대로 둔다.
        private static void GrayOutChromaticImages(Transform root)
        {
            var images = root.GetComponentsInChildren<Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                var color = images[i].color;
                var isChromatic = Mathf.Max(color.r, Mathf.Max(color.g, color.b)) - Mathf.Min(color.r, Mathf.Min(color.g, color.b)) > ChromaticThreshold;
                if (isChromatic)
                {
                    images[i].color = new Color(EmptySlotTintColor.r, EmptySlotTintColor.g, EmptySlotTintColor.b, color.a);
                }
            }
        }

        private static void StretchToFillParent(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // 슬롯 아이콘과 배경(등급 프레임 또는 회색 배경)을 갱신한다.
        private void RefreshSlotVisual(EquipmentPart part, bool hasItem, EquipmentGrade grade)
        {
            if (!slotDisplays.TryGetValue(part, out var slotObject) || slotObject == null)
            {
                return;
            }

            var itemFrame = slotObject.transform.Find("ItemFrame_01");
            if (itemFrame == null)
            {
                return;
            }

            var icon = itemFrame.Find("Item")?.GetComponent<Image>();
            var normalArea = itemFrame.Find("NormalArea");
            var addIndicator = itemFrame.Find("Add_1")?.gameObject;

            if (addIndicator != null)
            {
                addIndicator.SetActive(false);
            }

            if (normalArea != null)
            {
                normalArea.gameObject.SetActive(true);
            }

            if (icon != null)
            {
                icon.gameObject.SetActive(true);
                if (!hasItem && partIconSprites.TryGetValue(part, out var fallbackSprite) && fallbackSprite != null)
                {
                    icon.sprite = fallbackSprite;
                }

                icon.color = hasItem ? Color.white : EmptySlotTintColor;
            }

            if (hasItem)
            {
                ApplyFrameVariant(normalArea, grade);
            }
            else
            {
                ApplyEmptyBackground(normalArea);
            }
        }

        private void CachePart(EquipmentPart part, string[] boxNames, string[] slotNames, string[] levelTextNames)
        {
            var box = FindDeepAny(transform, boxNames);
            if (box != null)
            {
                var button = EnsureButton(box);
                selectButtons[part] = button;
                button.onClick.AddListener(() => SelectPart(part));
            }

            var slot = FindDeepAny(transform, slotNames);
            if (slot != null)
            {
                slotDisplays[part] = slot.gameObject;
            }

            var levelText = FindDeepAny(transform, levelTextNames)?.GetComponent<TMP_Text>();
            if (levelText != null)
            {
                levelTexts[part] = levelText;
            }
        }

        private void LogMissingReferences()
        {
            if (closeButton == null)
            {
                Debug.LogWarning("EquipmentSlotUpgradePanelController: 닫기 버튼(UpgradeClose)을 찾지 못했습니다.", this);
            }

            if (upgradeButton == null)
            {
                Debug.LogWarning("EquipmentSlotUpgradePanelController: 강화 버튼(UpgradeButton)을 찾지 못했습니다.", this);
            }

            if (headerText == null || statText == null || statText2 == null)
            {
                Debug.LogWarning("EquipmentSlotUpgradePanelController: Header/StatText/StatText2 중 일부를 찾지 못했습니다.", this);
            }

            if (totalText == null)
            {
                Debug.LogWarning("EquipmentSlotUpgradePanelController: TotalText를 찾지 못했습니다.", this);
            }

            if (upgradeMaterialText == null)
            {
                Debug.LogWarning("EquipmentSlotUpgradePanelController: UpgradeMaterialText를 찾지 못했습니다.", this);
            }

            if (upHeaderStatText == null || downHeaderStatText == null)
            {
                Debug.LogWarning("EquipmentSlotUpgradePanelController: UpHeaderStat/DownHeaderStat 중 일부를 찾지 못했습니다.", this);
            }

            foreach (EquipmentPart part in System.Enum.GetValues(typeof(EquipmentPart)))
            {
                if (!slotDisplays.ContainsKey(part))
                {
                    Debug.LogWarning($"EquipmentSlotUpgradePanelController: {part} 부위의 탭/슬롯 오브젝트를 찾지 못했습니다.", this);
                }
            }
        }

        private static Button EnsureButton(Transform target)
        {
            var button = target.GetComponent<Button>();
            if (button == null)
            {
                button = target.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None; // 목업 비주얼을 그대로 유지, 클릭 판정만 추가
            }

            return button;
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

        private static Transform FindDeepAny(Transform root, params string[] candidateNames)
        {
            for (var i = 0; i < candidateNames.Length; i++)
            {
                var found = FindDeep(root, candidateNames[i]);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        // ---------------------------------------------------------------
        // 새로 그리기
        // ---------------------------------------------------------------

        private void HandleDataChanged() => RefreshAll();

        private void RefreshAll()
        {
            if (!hasSelectedPart)
            {
                SelectPart(currentPart);
            }
            else
            {
                RefreshDetailPanel();
                RefreshUpgradeButtonState();
            }

            RefreshLevelTexts();
        }

        // Header("무기 : 레어")·StatText(기본 옵션)·StatText2(슬롯 강화 보너스) 갱신.
        private void RefreshDetailPanel()
        {
            var hasItem = EquipmentInventoryRuntime.TryGetEquipped(currentPart, out var item) && item.Definition != null;

            RefreshSlotVisual(currentPart, hasItem, hasItem ? item.Grade : EquipmentGrade.Common);

            if (headerText != null)
            {
                // 부위명 + 등급으로 표기한다("무기 : 레어").
                var partName = EquipmentPartInfo.GetDisplayName(currentPart);
                headerText.text = hasItem
                    ? $"{partName} : {EquipmentGradeInfo.GetDisplayName(item.Grade)}"
                    : $"{partName} : -";
            }

            if (statText != null)
            {
                statText.text = hasItem ? item.Definition.GetCoreStatSummary() : "장착된 장비가 없습니다";
            }

            if (statText2 != null)
            {
                statText2.text = BuildSlotBonusText(currentPart);
            }

            RefreshUpgradeMaterialText(currentPart);
            RefreshUpgradeRateTexts(currentPart);
        }

        // "강화 재료" 표시: 부위별 보유 강화석 / 다음 강화에 필요한 강화석 개수.
        // 강화석은 공용 재화지만, 필요 개수는 부위·현재 레벨에 따라 달라지므로 부위마다 따로 계산한다.
        private void RefreshUpgradeMaterialText(EquipmentPart part)
        {
            if (upgradeMaterialText == null)
            {
                return;
            }

            if (!EquipmentSlotUpgradeCalculator.IsSlotUpgradeSupported(part))
            {
                upgradeMaterialText.text = "-";
                return;
            }

            var owned = EquipmentSlotUpgradeRuntime.EnhancementStoneBalance;
            var required = EquipmentSlotUpgradeRuntime.GetNextStoneCost(part);
            upgradeMaterialText.text = $"{owned} / {required}";
        }

        // UpHeaderStat = 선택된 부위의 현재 레벨 능력 증가율, DownHeaderStat = 강화 1회 후(다음 레벨) 능력 증가율.
        private void RefreshUpgradeRateTexts(EquipmentPart part)
        {
            if (upHeaderStatText == null && downHeaderStatText == null)
            {
                return;
            }

            if (!EquipmentSlotUpgradeCalculator.IsSlotUpgradeSupported(part))
            {
                SetOptionalText(upHeaderStatText, "-");
                SetOptionalText(downHeaderStatText, "-");
                return;
            }

            var level = EquipmentSlotUpgradeRuntime.GetLevel(part);
            var currentBonusPercent = EquipmentSlotUpgradeCalculator.GetBonusBudgetPercent(level);
            var nextBonusPercent = EquipmentSlotUpgradeCalculator.GetBonusBudgetPercent(level + 1);

            SetOptionalText(upHeaderStatText, $"+{currentBonusPercent:0.0}%");
            SetOptionalText(downHeaderStatText, $"+{nextBonusPercent:0.0}%");
        }

        private static void SetOptionalText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        private static string BuildSlotBonusText(EquipmentPart part)
        {
            if (!EquipmentSlotUpgradeCalculator.IsSlotUpgradeSupported(part))
            {
                return "슬롯 강화 미지원";
            }

            var bonus = EquipmentSlotUpgradeRuntime.GetBonus(part);
            var lines = new List<string>();
            AppendBonusLine(lines, EquipmentStatType.AttackPower, bonus.AttackPowerPercent);
            AppendBonusLine(lines, EquipmentStatType.MaxHealth, bonus.MaxHealthPercent);
            AppendBonusLine(lines, EquipmentStatType.Defense, bonus.DefensePercent);
            return lines.Count > 0 ? string.Join("\n", lines) : "+0%";
        }

        private static void AppendBonusLine(List<string> lines, EquipmentStatType statType, float value)
        {
            if (value == 0f)
            {
                return;
            }

            lines.Add($"{EquipmentGradeStatTable.GetStatDisplayName(statType)} +{value:0.00}%");
        }

        // 부위별 "+N" 레벨 텍스트 및 총합(TotalText) 갱신.
        private void RefreshLevelTexts()
        {
            foreach (var pair in levelTexts)
            {
                var level = EquipmentSlotUpgradeRuntime.GetLevel(pair.Key);
                pair.Value.text = level > 0 ? $"+{level}" : string.Empty;
            }

            if (totalText != null)
            {
                totalText.text = $"LV : {EquipmentSlotUpgradeRuntime.TotalLevel}";
            }
        }

        // 장갑·장신구 등 슬롯 강화 미지원 부위를 보는 중에는 강화 버튼을 눌러도 반응하지 않도록 막는다.
        private void RefreshUpgradeButtonState()
        {
            if (upgradeButton != null)
            {
                upgradeButton.interactable = EquipmentSlotUpgradeCalculator.IsSlotUpgradeSupported(currentPart);
            }
        }

        private async void HandleUpgradeButtonClicked()
        {
            if (!EquipmentSlotUpgradeCalculator.IsSlotUpgradeSupported(currentPart))
            {
                return;
            }

            if (await EquipmentSlotUpgradeRuntime.TryUpgradeAsync(currentPart))
            {
                combatInputSaved?.Invoke();
            }
        }
    }
}
