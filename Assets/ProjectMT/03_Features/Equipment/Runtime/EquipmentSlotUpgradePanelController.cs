using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
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
            { EquipmentGrade.Common, "Green" },
            { EquipmentGrade.Rare, "Blue" },
            { EquipmentGrade.Epic, "Yellow" },
            { EquipmentGrade.Legendary, "Plum" },
            { EquipmentGrade.Mythic, "Red" },
        };

        private const string FrameVariantPrefix = "ItemFrame_01_Normal_";
        private const string EmptySlotBackgroundName = "EmptySlotGrayBg";
        private static readonly Color EmptySlotTintColor = new Color(0.5f, 0.5f, 0.5f, 1f);

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

        private EquipmentPart currentPart = EquipmentPart.Weapon;
        private bool hasSelectedPart;

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
        public void Configure(IGameProgressService progress)
        {
            EquipmentSlotUpgradeRuntime.Configure(progress);
        }

        public bool IsOpen => gameObject.activeSelf;

        public void Open()
        {
            if (gameObject.activeSelf)
            {
                return;
            }

            gameObject.SetActive(true);
            OpenStateChanged?.Invoke(true);
        }

        public void Close()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            gameObject.SetActive(false);
            OpenStateChanged?.Invoke(false);
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

        // 장비가 없을 때 normalArea 밑에 끼워 넣는 회색 배경.
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

            var backgroundObject = new GameObject(EmptySlotBackgroundName, typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(normalArea, false);
            StretchToFillParent(backgroundObject.GetComponent<RectTransform>());
            backgroundObject.GetComponent<Image>().color = EmptySlotTintColor;
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

            await EquipmentSlotUpgradeRuntime.TryUpgradeAsync(currentPart);
        }
    }
}
