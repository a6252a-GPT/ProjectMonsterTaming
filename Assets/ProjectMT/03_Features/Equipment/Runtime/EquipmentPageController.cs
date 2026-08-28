using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Equipment
{
    // 장비창(PF_CommanderEquipmentPage) 전체를 실제 장비 데이터와 연결하는 컨트롤러.
    // - 부위+등급으로 중첩(스택)하지 않는다. 랜덤 옵션 때문에 아이템마다 능력치가 달라서 보유 장비는
    //   전부 개별 인스턴스로 취급한다.
    // - 보유 수량만큼 슬롯을 최대 100개까지 생성·재사용하고 하나의 세로 목록에서 연속 스크롤한다.
    // - 데이터는 세션 한정이 아니라 GameProgressData(저장 파일)에 영구 저장된다.
    [DisallowMultipleComponent]
    public sealed class EquipmentPageController : MonoBehaviour
    {
        private const int AuthoredInventorySlotCount = 20; // 프리팹에 미리 둔 재사용 슬롯 수
        private const int MaxInventorySlotCount = EquipmentInventoryRuntime.MaxTotalQuantity;
        private const string AutoEquipButtonText = "자동 장착";

        private enum EquipmentPageMode
        {
            Equip,
            Dismantle
        }

        // 인벤토리 슬롯 1개에 대한 런타임 바인딩 정보. InventorySlot_01~20 각각에 대해 하나씩 만든다.
        private sealed class SlotView
        {
            public GameObject LayoutRoot;
            public Transform Root;
            public Image ItemIcon;
            public Transform NormalArea;
            public GameObject AddIndicator; // 비어있을 때 표시하는 "+" 표시(Add_1)
            public GameObject TextLevel; // 슬롯이 비어있을 때는 숨겨야 하는 목업 레벨 텍스트(값은 건드리지 않음)
            public GameObject CheckObject;
            public GameObject FocusObject;
            public GameObject LockObject;
            public Image UpgradeArrow;
            public TMP_Text EquippedLabelText; // 장착 중인 아이템을 인벤토리에서도 구분할 수 있도록 아이콘 아래 "[장착]" 표시
            public Button ClickButton;
            public string BoundInstanceId; // 이 슬롯이 현재 표시 중인 인스턴스 ID (없으면 null → 빈 슬롯)
        }

        private sealed class DismantlePreviewSlot
        {
            public GameObject Root;
            public Image Frame;
            public Image Icon;
        }

        [SerializeField] private EquipmentCatalog catalog;

        private readonly List<SlotView> slots = new List<SlotView>();
        private readonly Dictionary<EquipmentPart, Transform> commanderSlots = new Dictionary<EquipmentPart, Transform>();
        private readonly Dictionary<EquipmentPart, Transform> filterTabs = new Dictionary<EquipmentPart, Transform>();

        // 부위별 대표 아이콘 스프라이트. 군단장 장착 슬롯(WeaponSlot 등)은 부위마다 고정이라 항상 올바른
        // 아이콘이 미리 박혀 있으므로, 그 스프라이트를 그대로 재사용해서 인벤토리 슬롯도 실제 부위에 맞는
        // 아이콘을 보여주도록 한다.
        private readonly Dictionary<EquipmentPart, Sprite> partIconSprites = new Dictionary<EquipmentPart, Sprite>();

        // 테두리 색은 런타임 tint가 아니라, 목업에 있는 등급별 완성 프레임
        // (ItemFrame_01_Normal_Gray/Blue/Plum/Yellow/Red)을 그대로 재사용한다.
        private static readonly Dictionary<EquipmentGrade, string> FrameVariantSuffixByGrade = new Dictionary<EquipmentGrade, string>
        {
            { EquipmentGrade.Common, ItemGradeFramePalette.GetSuffix(EquipmentGrade.Common) },
            { EquipmentGrade.Rare, ItemGradeFramePalette.GetSuffix(EquipmentGrade.Rare) },
            { EquipmentGrade.Epic, ItemGradeFramePalette.GetSuffix(EquipmentGrade.Epic) },
            { EquipmentGrade.Legendary, ItemGradeFramePalette.GetSuffix(EquipmentGrade.Legendary) },
            { EquipmentGrade.Mythic, ItemGradeFramePalette.GetSuffix(EquipmentGrade.Mythic) },
        };

        private static readonly Color SelectedFilterColor = new Color32(244, 197, 72, 255);
        private static readonly Color NormalFilterColor = new Color32(236, 232, 225, 255);

        private const string FrameVariantPrefix = ItemGradeFramePalette.FrameVariantPrefix;
        private readonly Dictionary<string, GameObject> frameVariantTemplates = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Color> frameVariantSwatchColors = new Dictionary<string, Color>();
        private Transform frameVariantTemplateStorage;

        [SerializeField] private TMP_FontAsset equippedLabelFont;
        [SerializeField] private Sprite upgradeArrowSprite;

        private TMP_Text capacityText;
        private TMP_Text sortLabelText;
        private Transform allFilterTab;
        private Transform statGrid;
        private TMP_Text commanderSummaryText;
        private Transform selectedItemName;
        private Transform selectedItemStat; // "기본옵션"(핵심 능력치) 전용 텍스트
        private Transform selectedItemRandomOptionStat; // "추가 랜덤 옵션" 전용 텍스트
        private Transform equipButtonRoot;
        private Button equipButton;
        private TMP_Text equipButtonText;
        private Transform lockButtonRoot;
        private Button lockButton;
        private TMP_Text lockButtonText;
        private Transform equipModeTabRoot;
        private Button equipModeTabButton;
        private Image equipModeTabImage;
        private Transform dismantleModeTabRoot;
        private Button dismantleModeTabButton;
        private Image dismantleModeTabImage;
        private GameObject equipmentModeContentRoot;
        private GameObject dismantleSummaryRoot;
        private GameObject equipmentActionRoot;
        private GameObject dismantleActionRoot;
        private TMP_Text dismantleSummaryCountText;
        private TMP_Text dismantleSummaryRewardText;
        private TMP_Text dismantleBottomSummaryText;
        private readonly List<DismantlePreviewSlot> dismantlePreviewSlots = new List<DismantlePreviewSlot>();
        private Transform dismantleGradeButtonRoot;
        private Button dismantleGradeButton;
        private TMP_Text dismantleGradeButtonText;
        private Transform dismantleAutoSelectButtonRoot;
        private Button dismantleAutoSelectButton;
        private TMP_Text dismantleAutoSelectButtonText;
        private Transform dismantleButtonRoot;
        private Button dismantleButton;
        private TMP_Text dismantleButtonText;
        private Transform dismantleClearButtonRoot;
        private Button dismantleClearButton;
        private Transform offlineAutoDismantleOpenButtonRoot;
        private Button offlineAutoDismantleOpenButton;
        private TMP_Text offlineAutoDismantleOpenButtonText;
        private OfflineAutoDismantleSettingsPanelController offlineAutoDismantleSettingsPanel;
        private GameObject dismantleConfirmRoot;
        private TMP_Text dismantleConfirmSummaryText;
        private Button dismantleConfirmCancelButton;
        private Button dismantleConfirmAcceptButton;
        private ScrollRect inventoryScrollRect;
        private RectTransform inventoryContentRoot;
        private GameObject inventorySlotCellTemplate;
        private ItemComparisonPanelController itemComparisonPanel;

        private EquipmentPart? currentFilter; // null = 전체
        private bool sortGradeDescending = true;
        private string selectedInstanceId; // 현재 상세 영역에 표시 중인 장비 인스턴스 ID
        private EquipmentGrade dismantleGradeThreshold = EquipmentGrade.Common;
        private readonly HashSet<string> dismantleSelection = new HashSet<string>();
        private EquipmentPageMode currentMode = EquipmentPageMode.Equip;
        private bool requestInFlight;
        private Action combatInputSaved;
        private IGameProgressService progress;

        private void Awake()
        {
            CacheReferences();
            BuildModeTabs();
            BuildFilterButtons();
            BuildSortButton();
            BuildInventorySlots();
            BuildEquipButton();
            BuildDismantleControls();
            BuildLockButton();
        }

        private void OnEnable()
        {
            EquipmentInventoryRuntime.Changed += HandleInventoryChanged;
            SetPageMode(EquipmentPageMode.Equip, false);
            RefreshAll();
            ResetInventoryScrollPosition();
        }

        private void OnDisable()
        {
            EquipmentInventoryRuntime.Changed -= HandleInventoryChanged;
            CloseDismantleConfirmation();
            offlineAutoDismantleSettingsPanel?.Close();
            itemComparisonPanel?.Hide();
        }

        // MainBattleSceneRoot가 씬 조립 시점에 진행 데이터 서비스를 주입한다. 실제 보유/장착 데이터는
        // EquipmentInventoryRuntime(정적 파사드)이 들고 있으므로, 여기서는 그 파사드에 서비스와
        // 카탈로그를 연결해주기만 하면 된다.
        public void Configure(IGameProgressService progress)
        {
            Configure(progress, EquipmentBalanceConfig.RuntimeDefault, null);
        }

        public void Configure(
            IGameProgressService progress,
            EquipmentBalanceConfig balance,
            Action onCombatInputSaved = null)
        {
            this.progress = progress;
            combatInputSaved = onCombatInputSaved;
            EquipmentInventoryRuntime.Configure(progress, ResolveCatalog(), balance);
            offlineAutoDismantleSettingsPanel?.Configure(progress);
            if (isActiveAndEnabled)
            {
                RefreshAll(); // 활성화가 데이터 주입보다 먼저 끝난 경우 버튼 상태를 즉시 복구
            }
        }

        // ---------------------------------------------------------------
        // 참조 탐색 (이름 기반, 프리팹 내부 구조를 몰라도 안전하게 찾을 수 있도록 재귀 탐색한다)
        // ---------------------------------------------------------------

        private void CacheReferences()
        {
            capacityText = FindDeep(transform, "Capacity")?.GetComponent<TMP_Text>();
            sortLabelText = FindDeep(transform, "SortLabel")?.GetComponent<TMP_Text>();
            statGrid = FindDeep(transform, "StatGrid");
            commanderSummaryText = FindDeep(transform, "CommanderSummary")?.GetComponent<TMP_Text>();
            selectedItemName = FindDeep(transform, "SelectedItemName");
            selectedItemStat = FindDeep(transform, "SelectedItemStat");
            // 추가 랜덤옵션 전용 칸: 새 이름(SelectedItemNext) 우선, 예전 이름도 후보로 유지한다.
            selectedItemRandomOptionStat = FindDeepAny(transform, "SelectedItemNext", "SelectedItemRandomOptionStat");
            equipButtonRoot = FindDeep(transform, "EquipButton");
            lockButtonRoot = FindDeep(transform, "EquipmentLockButton");
            equipModeTabRoot = FindDeep(transform, "EquipModeTabButton");
            dismantleModeTabRoot = FindDeep(transform, "DismantleModeTabButton");
            equipmentModeContentRoot = FindDeep(transform, "EquipmentModeContentRoot")?.gameObject;
            dismantleSummaryRoot = FindDeep(transform, "DismantleSummaryRoot")?.gameObject;
            equipmentActionRoot = FindDeep(transform, "SelectedEquipmentAction")?.gameObject;
            dismantleActionRoot = FindDeep(transform, "DismantleActionRoot")?.gameObject;
            dismantleSummaryCountText = FindDeep(transform, "DismantleSummaryCount")?.GetComponent<TMP_Text>();
            dismantleSummaryRewardText = FindDeep(transform, "DismantleSummaryReward")?.GetComponent<TMP_Text>();
            dismantleBottomSummaryText = FindDeep(transform, "DismantleBottomSummary")?.GetComponent<TMP_Text>();
            dismantleGradeButtonRoot = FindDeep(transform, "DismantleGradeButton");
            dismantleAutoSelectButtonRoot = FindDeep(transform, "DismantleAutoSelectButton");
            dismantleButtonRoot = FindDeep(transform, "DismantleButton");
            dismantleClearButtonRoot = FindDeep(transform, "DismantleClearButton");
            offlineAutoDismantleOpenButtonRoot = FindDeep(transform, "OfflineAutoDismantleOpenButton");
            var equipmentPageRoot = transform.parent != null ? transform.parent : transform;
            offlineAutoDismantleSettingsPanel =
                FindDeep(equipmentPageRoot, "PF_OfflineAutoDismantleSettingsPopup")
                    ?.GetComponent<OfflineAutoDismantleSettingsPanelController>();
            dismantleConfirmRoot = FindDeep(transform, "DismantleConfirmRoot")?.gameObject;
            dismantleConfirmSummaryText = FindDeep(transform, "DismantleConfirmSummary")?.GetComponent<TMP_Text>();
            dismantleConfirmCancelButton = FindDeep(transform, "DismantleConfirmCancelButton")?.GetComponent<Button>();
            dismantleConfirmAcceptButton = FindDeep(transform, "DismantleConfirmAcceptButton")?.GetComponent<Button>();

            dismantlePreviewSlots.Clear();
            for (var index = 1; index <= 8; index++)
            {
                var previewRoot = FindDeep(transform, $"DismantlePreview_{index:00}");
                if (previewRoot == null)
                {
                    continue;
                }

                dismantlePreviewSlots.Add(new DismantlePreviewSlot
                {
                    Root = previewRoot.gameObject,
                    Frame = previewRoot.GetComponent<Image>(),
                    Icon = previewRoot.Find("Icon")?.GetComponent<Image>()
                });
            }

            commanderSlots[EquipmentPart.Weapon] = FindDeep(transform, "WeaponSlot");
            commanderSlots[EquipmentPart.Helmet] = FindDeep(transform, "HelmetSlot");
            commanderSlots[EquipmentPart.Armor] = FindDeep(transform, "ArmorSlot");
            commanderSlots[EquipmentPart.Glove] = FindDeep(transform, "GloveSlot");
            commanderSlots[EquipmentPart.Ring] = FindDeep(transform, "RingSlot");
            commanderSlots[EquipmentPart.Boots] = FindDeep(transform, "BootsSlot");

            CachePartIconSprites();
            CacheFrameVariantTemplates();
        }

        private EquipmentCatalog ResolveCatalog()
        {
            if (catalog != null)
            {
                return catalog;
            }

#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets("t:EquipmentCatalog");
            if (guids.Length > 0)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<EquipmentCatalog>(path);
            }
#endif
            return catalog;
        }

        // 군단장 장착 슬롯 6개는 부위별로 고정된 오브젝트라 항상 올바른 아이콘 스프라이트를 갖고 있다.
        // 그 스프라이트를 부위별 대표 아이콘으로 캐시해서 인벤토리 슬롯에도 그대로 쓴다.
        private void CachePartIconSprites()
        {
            foreach (var pair in commanderSlots)
            {
                var image = pair.Value != null
                    ? pair.Value.GetComponentsInChildren<Image>(true)
                        .FirstOrDefault(candidate => candidate.name == "Item" && candidate.sprite != null)
                    : null;
                if (image != null && image.sprite != null)
                {
                    partIconSprites[pair.Key] = image.sprite;
                }
            }
        }

        // 목업 곳곳(인벤토리 슬롯들)에 흩어져 있는 등급별 프레임을 이름별로 하나씩 찾아서,
        // 눈에 보이지 않는 보관용 오브젝트 아래에 복제해둔다. 이후 슬롯에 실제 등급을 표시할 때
        // 이 복제본을 다시 복제해서 끼워 넣는 방식으로 "기존 프레임 그대로"의 테두리를 재사용한다.
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

                var swatch = FindFrameSwatchGraphic(clone.transform);
                if (swatch != null)
                {
                    frameVariantSwatchColors[suffix] = swatch.color;
                }
            }
        }

        // 프레임 템플릿의 배경("Bg") 색을 찾는다. 분해창 등 단색 UI에서 재사용한다.
        private static Graphic FindFrameSwatchGraphic(Transform frameRoot)
        {
            var namedBg = frameRoot.Find("Bg");
            var namedGraphic = namedBg != null ? namedBg.GetComponent<Graphic>() : null;
            if (namedGraphic != null)
            {
                return namedGraphic;
            }

            var graphics = frameRoot.GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null && graphics[i].name != "Icon")
                {
                    return graphics[i];
                }
            }

            return null;
        }

        // 등급에 맞는 프레임 템플릿을 복제해서 normalArea 밑에 끼워 넣는다.
        // (이미 올바른 등급 프레임이 끼워져 있으면 아무 것도 하지 않는다.)
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
                return; // 이미 올바른 등급 프레임
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

            var rect = instance.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }

        // 자식 전체(비활성 포함)에서 이름이 일치하는 첫 Transform을 찾는다.
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

        // 하이어라키 이름이 바뀔 수 있어 여러 후보를 순서대로 시도한다(먼저 찾은 이름이 우선한다).
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
        // 상단 장착/분해 탭
        // ---------------------------------------------------------------

        private void BuildModeTabs()
        {
            if (equipModeTabRoot != null)
            {
                equipModeTabButton = EnsureButton(equipModeTabRoot);
                equipModeTabImage = equipModeTabRoot.GetComponent<Image>();
                equipModeTabButton.onClick.AddListener(() => SetPageMode(EquipmentPageMode.Equip));
            }

            if (dismantleModeTabRoot != null)
            {
                dismantleModeTabButton = EnsureButton(dismantleModeTabRoot);
                dismantleModeTabImage = dismantleModeTabRoot.GetComponent<Image>();
                dismantleModeTabButton.onClick.AddListener(() => SetPageMode(EquipmentPageMode.Dismantle));
            }
        }

        private void SetPageMode(EquipmentPageMode mode, bool refresh = true)
        {
            if (requestInFlight)
            {
                return;
            }

            currentMode = mode;
            CloseDismantleConfirmation();
            offlineAutoDismantleSettingsPanel?.Close();
            itemComparisonPanel?.Hide();
            if (currentMode == EquipmentPageMode.Equip)
            {
                ClearDismantleSelection();
            }
            else
            {
                selectedInstanceId = null;
            }

            if (equipmentModeContentRoot != null)
            {
                equipmentModeContentRoot.SetActive(currentMode == EquipmentPageMode.Equip);
            }

            if (dismantleSummaryRoot != null)
            {
                dismantleSummaryRoot.SetActive(currentMode == EquipmentPageMode.Dismantle);
            }

            if (equipmentActionRoot != null)
            {
                equipmentActionRoot.SetActive(currentMode == EquipmentPageMode.Equip);
            }

            if (dismantleActionRoot != null)
            {
                dismantleActionRoot.SetActive(currentMode == EquipmentPageMode.Dismantle);
            }

            RefreshModeTabVisuals();
            if (refresh)
            {
                RefreshAll();
                ResetInventoryScrollPosition();
            }
        }

        private void RefreshModeTabVisuals()
        {
            var equipSelected = currentMode == EquipmentPageMode.Equip;
            if (equipModeTabImage != null)
            {
                equipModeTabImage.color = equipSelected
                    ? new Color32(105, 177, 53, 255)
                    : new Color32(46, 44, 49, 255);
            }

            if (dismantleModeTabImage != null)
            {
                dismantleModeTabImage.color = equipSelected
                    ? new Color32(46, 44, 49, 255)
                    : new Color32(105, 177, 53, 255);
            }
        }

        // ---------------------------------------------------------------
        // 부위 필터 탭 - 6부위를 아이콘 없이 한글 텍스트로 구분한다.
        // ---------------------------------------------------------------

        private void BuildFilterButtons()
        {
            allFilterTab = FindDeep(transform, "Filter_All_SELECTED");
            AddFilterTab("Filter_Weapon", EquipmentPart.Weapon);
            AddFilterTab("Filter_Shield", EquipmentPart.Helmet);
            AddFilterTab("Filter_Armor", EquipmentPart.Armor);
            AddFilterTab("Filter_Boots", EquipmentPart.Boots);
            AddFilterTab("Filter_Glove", EquipmentPart.Glove);
            AddFilterTab("Filter_Accessory", EquipmentPart.Ring);

            if (allFilterTab != null)
            {
                ConfigureTextFilterTab(allFilterTab, "전체");
                var button = EnsureButton(allFilterTab);
                button.onClick.AddListener(() => SetFilter(null));
            }
        }

        private void AddFilterTab(string objectName, EquipmentPart part)
        {
            var tab = FindDeep(transform, objectName);
            if (tab == null)
            {
                return;
            }

            filterTabs[part] = tab;
            ConfigureTextFilterTab(tab, EquipmentPartInfo.GetDisplayName(part));
            var button = EnsureButton(tab);
            button.onClick.AddListener(() => SetFilter(part));
        }

        private static void ConfigureTextFilterTab(Transform tab, string labelText)
        {
            var label = tab.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = labelText;
                label.gameObject.SetActive(true);
            }

            var icon = tab.Find("Icon");
            icon?.gameObject.SetActive(false); // 부위 필터는 텍스트만 표시
        }

        private void SetFilter(EquipmentPart? part)
        {
            currentFilter = part;
            RefreshFilterHighlight();
            RefreshInventoryList();
            ResetInventoryScrollPosition();
        }

        // 각 탭의 "Focus" 하위 오브젝트를 선택 상태 표시로 사용한다(활성=선택됨).
        private void RefreshFilterHighlight()
        {
            SetFilterVisual(allFilterTab, currentFilter == null);
            foreach (var pair in filterTabs)
            {
                SetFilterVisual(pair.Value, currentFilter == pair.Key);
            }
        }

        private static void SetFilterVisual(Transform tab, bool selected)
        {
            var focus = tab != null ? tab.Find("Focus") : null;
            if (focus != null)
            {
                focus.gameObject.SetActive(selected);
            }

            var label = tab != null ? tab.GetComponentInChildren<TMP_Text>(true) : null;
            if (label != null)
            {
                label.color = selected ? SelectedFilterColor : NormalFilterColor;
            }
        }

        // ---------------------------------------------------------------
        // 정렬 버튼 - 클릭할 때마다 등급 내림차순 ↔ 오름차순으로 전환한다.
        // ---------------------------------------------------------------

        private void BuildSortButton()
        {
            var sortLabelTransform = FindDeep(transform, "SortLabel");
            if (sortLabelTransform == null)
            {
                return;
            }

            var button = EnsureButton(sortLabelTransform);
            button.onClick.AddListener(ToggleSort);
            RefreshSortLabelText();
        }

        private void ToggleSort()
        {
            sortGradeDescending = !sortGradeDescending;
            RefreshSortLabelText();
            RefreshInventoryList();
            ResetInventoryScrollPosition();
        }

        private void RefreshSortLabelText()
        {
            if (sortLabelText != null)
            {
                sortLabelText.text = sortGradeDescending ? "장착 가능 우선 · 등급 높은순" : "장착 가능 우선 · 등급 낮은순";
            }
        }

        // ---------------------------------------------------------------
        // 인벤토리 연속 스크롤 + 재사용 슬롯 풀
        // ---------------------------------------------------------------

        private static readonly string[] InventoryScrollViewNameCandidates =
        {
            "InventorySlotScroll View",
            "InventorySlotScrollView",
            "PF_InventoryScroll View",
            "PF_InventoryScrollView",
            "InventoryScrollView",
            "InventoryScroll View"
        };

        private static readonly Regex InventorySlotNamePattern =
            new Regex(@"^InventorySlot_\d{2,3}(?:_1)?$");

        private void BuildInventorySlots()
        {
            Transform scrollViewTransform = null;
            for (var i = 0; i < InventoryScrollViewNameCandidates.Length; i++)
            {
                scrollViewTransform = FindDeep(transform, InventoryScrollViewNameCandidates[i]);
                if (scrollViewTransform != null)
                {
                    break;
                }
            }

            if (scrollViewTransform == null)
            {
                return;
            }

            inventoryScrollRect = scrollViewTransform.GetComponent<ScrollRect>();
            inventoryContentRoot = inventoryScrollRect != null ? inventoryScrollRect.content : null;
            if (inventoryContentRoot == null)
            {
                return;
            }

            for (var i = 1; i <= AuthoredInventorySlotCount; i++)
            {
                var slotRoot = FindDeepAny(transform, $"InventorySlot_{i:00}_1", $"InventorySlot_{i:00}");
                if (slotRoot != null)
                {
                    AddSlotView(slotRoot, false);
                }
            }

            if (slots.Count > 0)
            {
                inventorySlotCellTemplate = slots[0].LayoutRoot;
            }
        }

        private void AddSlotView(Transform slotRoot, bool resetClickListeners)
        {
            var layoutRoot = slotRoot.gameObject;
            if (slotRoot.parent != null && slotRoot.parent.parent == inventoryContentRoot)
            {
                layoutRoot = slotRoot.parent.gameObject;
            }

            var view = new SlotView
            {
                LayoutRoot = layoutRoot,
                Root = slotRoot,
                ItemIcon = FindDeep(slotRoot, "Item")?.GetComponent<Image>(),
                NormalArea = FindDeep(slotRoot, "NormalArea"),
                AddIndicator = FindDeep(slotRoot, "Add_1")?.gameObject,
                TextLevel = FindDeep(slotRoot, "Text_Level")?.gameObject,
                CheckObject = FindDeep(slotRoot, "Check")?.gameObject,
                FocusObject = FindDeep(slotRoot, "Focus")?.gameObject,
                LockObject = FindDeep(slotRoot, "Lock")?.gameObject
            };

            view.EquippedLabelText = CreateEquippedLabel(slotRoot);
            view.UpgradeArrow = CreateUpgradeArrow(slotRoot);
            view.ClickButton = EnsureButton(slotRoot);
            if (resetClickListeners)
            {
                view.ClickButton.onClick.RemoveAllListeners();
            }

            var capturedView = view;
            view.ClickButton.onClick.AddListener(() => HandleSlotClicked(capturedView));

            var holdTrigger = PointerHoldTrigger.EnsureOn(slotRoot.gameObject);
            holdTrigger?.Configure(() => HandleSlotHoldStart(capturedView), null);

            slots.Add(view);
        }

        private int EnsureInventorySlotCount(int requestedCount)
        {
            var visibleCount = Mathf.Clamp(requestedCount, 0, MaxInventorySlotCount);
            while (slots.Count < visibleCount && inventorySlotCellTemplate != null && inventoryContentRoot != null)
            {
                var slotNumber = slots.Count + 1;
                var cell = Instantiate(inventorySlotCellTemplate, inventoryContentRoot);
                cell.name = $"InventorySlotCell_{slotNumber:000}";
                cell.SetActive(true);

                var slotRoot = cell.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(candidate => InventorySlotNamePattern.IsMatch(candidate.name));
                if (slotRoot == null)
                {
                    Destroy(cell);
                    break;
                }

                slotRoot.name = $"InventorySlot_{slotNumber:00}_1";
                AddSlotView(slotRoot, true);
            }

            var availableCount = Mathf.Min(visibleCount, slots.Count);
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i].LayoutRoot != null)
                {
                    slots[i].LayoutRoot.SetActive(i < availableCount);
                }
            }

            return availableCount;
        }

        private void RebuildInventoryLayout()
        {
            if (inventoryContentRoot == null)
            {
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(inventoryContentRoot);
            Canvas.ForceUpdateCanvases();
        }

        private void ResetInventoryScrollPosition()
        {
            if (inventoryScrollRect == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            inventoryScrollRect.StopMovement();
            inventoryScrollRect.verticalNormalizedPosition = 1f;
        }

        // 인벤토리에서도 장착 중인 아이템을 구분할 수 있도록 아이콘 아래에 "[장착]" 텍스트를 표시한다.
        private TMP_Text CreateEquippedLabel(Transform slotRoot)
        {
            var existing = FindDeep(slotRoot, "EquippedLabel")?.GetComponent<TMP_Text>();
            if (existing != null)
            {
                return existing;
            }

            var labelObject = new GameObject("EquippedLabel", typeof(RectTransform));
            labelObject.transform.SetParent(slotRoot, false);
            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 10f);
            rect.sizeDelta = new Vector2(0f, 24f);

            var text = labelObject.AddComponent<TextMeshProUGUI>();
            if (equippedLabelFont != null)
            {
                text.font = equippedLabelFont;
            }

            text.fontSize = 18f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.outlineWidth = 0.2f;
            text.outlineColor = Color.black;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private Image CreateUpgradeArrow(Transform slotRoot)
        {
            var existing = FindDeep(slotRoot, "UpgradeArrow")?.GetComponent<Image>();
            if (existing != null)
            {
                existing.sprite = upgradeArrowSprite;
                return existing;
            }

            var arrowObject = new GameObject("UpgradeArrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            arrowObject.transform.SetParent(slotRoot, false);
            var rect = arrowObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(48f, 48f);

            var image = arrowObject.GetComponent<Image>();
            image.sprite = upgradeArrowSprite;
            image.color = new Color32(50, 220, 105, 255);
            image.preserveAspect = true;
            image.raycastTarget = false;
            arrowObject.SetActive(false);
            return image;
        }

        private static Button EnsureButton(Transform target)
        {
            var button = target.GetComponent<Button>();
            if (button == null)
            {
                button = target.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None; // 목업 비주얼을 그대로 유지, 클릭 판정만 추가
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

        private void HandleSlotClicked(SlotView view)
        {
            if (string.IsNullOrEmpty(view.BoundInstanceId))
            {
                return; // 빈 슬롯
            }

            if (currentMode == EquipmentPageMode.Dismantle)
            {
                if (!EquipmentInventoryRuntime.TryGetItem(view.BoundInstanceId, out var dismantleItem) ||
                    dismantleItem.IsEquipped || dismantleItem.IsLocked)
                {
                    return;
                }

                if (!dismantleSelection.Add(view.BoundInstanceId))
                {
                    dismantleSelection.Remove(view.BoundInstanceId);
                }

                selectedInstanceId = null;
                CloseDismantleConfirmation();
                RefreshSelection();
                return;
            }

            selectedInstanceId = view.BoundInstanceId;
            RefreshSelection();
            ShowItemComparisonPopup(view.BoundInstanceId);
        }

        // 누르는 순간 비교창을 열어 모바일에서도 즉시 반응하게 한다.
        private void HandleSlotHoldStart(SlotView view)
        {
            if (currentMode != EquipmentPageMode.Equip || string.IsNullOrEmpty(view.BoundInstanceId))
            {
                return;
            }

            selectedInstanceId = view.BoundInstanceId;
            RefreshSelection();
            ShowItemComparisonPopup(view.BoundInstanceId);
        }

        private void ShowItemComparisonPopup(string instanceId)
        {
            if (!EquipmentInventoryRuntime.TryGetItem(instanceId, out var item) || item.Definition == null)
            {
                return;
            }

            EnsureItemComparisonPanel();
            if (itemComparisonPanel == null)
            {
                return;
            }

            partIconSprites.TryGetValue(item.Part, out var icon);
            if (icon == null)
            {
                CachePartIconSprites(); // 늦게 준비된 장착 슬롯 아이콘 재수집
                partIconSprites.TryGetValue(item.Part, out icon);
            }

            if (icon == null)
            {
                icon = item.Definition.Icon;
            }
            itemComparisonPanel.Show(item, icon);
        }

        // PF_ItemComparison은 장비 페이지가 아니라 씬의 관리 UI 쪽에 배치돼 있어 루트부터 재귀 탐색한다.
        private void EnsureItemComparisonPanel()
        {
            if (itemComparisonPanel != null)
            {
                return;
            }

            var panelRoot = FindDeep(transform.root, "PF_ItemComparison_2");
            if (panelRoot == null)
            {
                return;
            }

            itemComparisonPanel = panelRoot.GetComponent<ItemComparisonPanelController>()
                ?? panelRoot.gameObject.AddComponent<ItemComparisonPanelController>();
            itemComparisonPanel.Configure(combatInputSaved);
        }

        // ---------------------------------------------------------------
        // 자동 장착 버튼
        // ---------------------------------------------------------------

        private void BuildEquipButton()
        {
            if (equipButtonRoot == null)
            {
                return;
            }

            equipButton = EnsureButton(equipButtonRoot);
            equipButtonText = equipButtonRoot.GetComponentInChildren<TMP_Text>(true);
            equipButton.onClick.AddListener(HandleEquipButtonClicked);
        }

        private async void HandleEquipButtonClicked()
        {
            if (currentMode != EquipmentPageMode.Equip || requestInFlight)
            {
                return;
            }

            requestInFlight = true;
            RefreshSelection();
            try
            {
                if (await EquipmentInventoryRuntime.TryAutoEquipAsync())
                {
                    combatInputSaved?.Invoke();
                }
            }
            finally
            {
                requestInFlight = false;
                RefreshAll();
            }
        }

        private void BuildDismantleControls()
        {
            if (dismantleGradeButtonRoot != null)
            {
                dismantleGradeButton = EnsureButton(dismantleGradeButtonRoot);
                dismantleGradeButtonText = dismantleGradeButtonRoot.GetComponentInChildren<TMP_Text>(true);
                dismantleGradeButton.onClick.AddListener(CycleDismantleGradeThreshold);
            }

            if (dismantleAutoSelectButtonRoot != null)
            {
                dismantleAutoSelectButton = EnsureButton(dismantleAutoSelectButtonRoot);
                dismantleAutoSelectButtonText = dismantleAutoSelectButtonRoot.GetComponentInChildren<TMP_Text>(true);
                dismantleAutoSelectButton.onClick.AddListener(ToggleDismantleAutoSelection);
            }

            if (dismantleButtonRoot != null)
            {
                dismantleButton = EnsureButton(dismantleButtonRoot);
                dismantleButtonText = dismantleButtonRoot.GetComponentInChildren<TMP_Text>(true);
                dismantleButton.onClick.AddListener(HandleDismantleButtonClicked);
            }

            if (dismantleClearButtonRoot != null)
            {
                dismantleClearButton = EnsureButton(dismantleClearButtonRoot);
                dismantleClearButton.onClick.AddListener(() =>
                {
                    ClearDismantleSelection();
                    RefreshSelection();
                });
            }

            if (offlineAutoDismantleOpenButtonRoot != null)
            {
                offlineAutoDismantleOpenButton = EnsureButton(offlineAutoDismantleOpenButtonRoot);
                offlineAutoDismantleOpenButtonText =
                    offlineAutoDismantleOpenButtonRoot.GetComponentInChildren<TMP_Text>(true);
                offlineAutoDismantleOpenButton.onClick.AddListener(OpenOfflineAutoDismantleSettings);
            }

            if (dismantleConfirmCancelButton != null)
            {
                dismantleConfirmCancelButton.onClick.AddListener(CloseDismantleConfirmation);
            }

            if (dismantleConfirmAcceptButton != null)
            {
                dismantleConfirmAcceptButton.onClick.AddListener(HandleDismantleConfirmed);
            }
        }

        private void BuildLockButton()
        {
            if (lockButtonRoot == null)
            {
                return;
            }

            lockButton = EnsureButton(lockButtonRoot);
            lockButtonText = lockButtonRoot.GetComponentInChildren<TMP_Text>(true);
            lockButton.onClick.AddListener(HandleLockButtonClicked);
        }

        private void CycleDismantleGradeThreshold()
        {
            if (requestInFlight)
            {
                return;
            }

            dismantleGradeThreshold = (EquipmentGrade)(((int)dismantleGradeThreshold + 1) % 5);
            ClearDismantleSelection();
            RefreshSelection();
        }

        private void OpenOfflineAutoDismantleSettings()
        {
            if (requestInFlight || offlineAutoDismantleSettingsPanel == null)
            {
                return;
            }

            offlineAutoDismantleSettingsPanel.Configure(progress);
            offlineAutoDismantleSettingsPanel.Open();
        }

        private void ToggleDismantleAutoSelection()
        {
            if (requestInFlight)
            {
                return;
            }

            if (dismantleSelection.Count > 0)
            {
                ClearDismantleSelection();
                RefreshSelection();
                return;
            }

            var candidates = EquipmentInventoryRuntime.GetDismantleCandidateIds(dismantleGradeThreshold);
            for (var index = 0; index < candidates.Count; index++)
            {
                dismantleSelection.Add(candidates[index]);
            }

            if (dismantleSelection.Count > 0)
            {
                selectedInstanceId = null;
            }

            CloseDismantleConfirmation();
            RefreshSelection();
        }

        private async void HandleLockButtonClicked()
        {
            if (requestInFlight || currentMode != EquipmentPageMode.Equip ||
                string.IsNullOrEmpty(selectedInstanceId) ||
                !EquipmentInventoryRuntime.TryGetItem(selectedInstanceId, out var item))
            {
                return;
            }

            requestInFlight = true;
            RefreshSelection();
            try
            {
                await EquipmentInventoryRuntime.TrySetLockedAsync(selectedInstanceId, !item.IsLocked);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                requestInFlight = false;
                RefreshAll();
            }
        }

        private void HandleDismantleButtonClicked()
        {
            PruneDismantleSelection();
            if (requestInFlight || currentMode != EquipmentPageMode.Dismantle || dismantleSelection.Count == 0)
            {
                RefreshSelection();
                return;
            }

            OpenDismantleConfirmation();
        }

        private async void HandleDismantleConfirmed()
        {
            PruneDismantleSelection();
            if (requestInFlight || currentMode != EquipmentPageMode.Dismantle || dismantleSelection.Count == 0)
            {
                CloseDismantleConfirmation();
                RefreshSelection();
                return;
            }

            var targets = dismantleSelection.ToArray();
            requestInFlight = true;
            CloseDismantleConfirmation();
            RefreshSelection();
            try
            {
                if (await EquipmentInventoryRuntime.TryDismantleAsync(targets))
                {
                    dismantleSelection.Clear();
                    selectedInstanceId = null;
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                requestInFlight = false;
                CloseDismantleConfirmation();
                RefreshAll();
            }
        }

        private void OpenDismantleConfirmation()
        {
            if (dismantleConfirmRoot == null)
            {
                HandleDismantleConfirmed();
                return;
            }

            if (dismantleConfirmSummaryText != null)
            {
                dismantleConfirmSummaryText.text =
                    $"선택 장비 {dismantleSelection.Count}개를 분해하고\n장비 슬롯 강화석 {CalculateSelectedDismantleReward():N0}개를 획득합니다.";
            }

            dismantleConfirmRoot.SetActive(true);
        }

        private void ClearDismantleSelection()
        {
            dismantleSelection.Clear();
            CloseDismantleConfirmation();
        }

        private void CloseDismantleConfirmation()
        {
            if (dismantleConfirmRoot != null)
            {
                dismantleConfirmRoot.SetActive(false);
            }
        }

        private void PruneDismantleSelection()
        {
            dismantleSelection.RemoveWhere(instanceId =>
                !EquipmentInventoryRuntime.TryGetItem(instanceId, out var item) || item.IsEquipped || item.IsLocked);
            if (dismantleSelection.Count == 0)
            {
                CloseDismantleConfirmation();
            }
        }

        // ---------------------------------------------------------------
        // 이벤트 핸들러 / 새로 그리기
        // ---------------------------------------------------------------

        private void HandleInventoryChanged() => RefreshAll();

        private void RefreshAll()
        {
            RefreshInventoryList();
            RefreshCommanderSlots();
            RefreshCommanderStats();
        }

        private void RefreshInventoryList()
        {
            IEnumerable<EquipmentItemView> query = EquipmentInventoryRuntime.GetItems();
            if (currentFilter.HasValue)
            {
                query = query.Where(item => item.Part == currentFilter.Value);
            }

            var evaluated = query.Select((item, index) => new
            {
                Item = item,
                OriginalIndex = index,
                PowerDelta = EquipmentUpgradeEvaluator.EvaluatePowerDelta(item)
            });
            var ordered = evaluated
                .OrderByDescending(entry => entry.PowerDelta > 0)
                .ThenByDescending(entry => entry.PowerDelta);
            ordered = sortGradeDescending
                ? ordered.ThenByDescending(entry => (int)entry.Item.Grade)
                : ordered.ThenBy(entry => (int)entry.Item.Grade);
            var list = ordered
                .ThenBy(entry => entry.Item.Part)
                .ThenBy(entry => entry.OriginalIndex)
                .Select(entry => entry.Item)
                .ToList();
            var visibleSlotCount = EnsureInventorySlotCount(list.Count);

            for (var i = 0; i < slots.Count; i++)
            {
                if (i < visibleSlotCount)
                {
                    BindSlot(slots[i], list[i]);
                }
                else
                {
                    ClearSlot(slots[i]);
                }
            }

            // 선택된 장비가 더 이상 보유 목록에 없으면 선택 해제.
            if (!string.IsNullOrEmpty(selectedInstanceId) && !EquipmentInventoryRuntime.TryGetItem(selectedInstanceId, out _))
            {
                selectedInstanceId = null;
            }

            PruneDismantleSelection();

            RefreshCapacityText();
            RebuildInventoryLayout();
            RefreshSelection();
        }

        // 슬롯 오브젝트 자체는 항상 켜둔 채로("+" 표시가 기본으로 보이도록), 아이템이 있을 때만 아이콘
        // 영역(NormalArea/Item)을 켜고 "+" 표시(Add_1)를 끈다. 아이콘은 원래 색(고유 아트) 그대로 보여야
        // 하므로 등급색으로 물들이지 않고, 테두리는 목업에 있던 등급별 완성 프레임을 그대로 갖다 끼운다.
        private void BindSlot(SlotView view, EquipmentItemView item)
        {
            view.BoundInstanceId = item.InstanceId;

            if (view.AddIndicator != null)
            {
                view.AddIndicator.SetActive(false);
            }

            if (view.NormalArea != null)
            {
                view.NormalArea.gameObject.SetActive(true);
            }

            if (view.ItemIcon != null)
            {
                view.ItemIcon.gameObject.SetActive(true);
                if (partIconSprites.TryGetValue(item.Part, out var partSprite) && partSprite != null)
                {
                    view.ItemIcon.sprite = partSprite;
                }

                view.ItemIcon.color = currentMode == EquipmentPageMode.Dismantle && (item.IsEquipped || item.IsLocked)
                    ? new Color32(125, 125, 125, 255)
                    : Color.white; // 분해 보호 장비만 어둡게 표시
            }

            ApplyFrameVariant(view.NormalArea, item.Grade); // 등급에 맞는 기존 프레임(테두리)으로 교체

            // Lv 표시는 나중에 실제 레벨 시스템이 붙으면 다시 켤 것이므로, 지금은 항상 비활성화한다.
            if (view.TextLevel != null)
            {
                view.TextLevel.SetActive(false);
            }

            if (view.EquippedLabelText != null)
            {
                view.EquippedLabelText.text = item.IsEquipped ? "[장착]" : string.Empty;
            }

            if (view.CheckObject != null)
            {
                view.CheckObject.SetActive(currentMode == EquipmentPageMode.Dismantle &&
                                           dismantleSelection.Contains(item.InstanceId));
            }

            if (view.FocusObject != null)
            {
                view.FocusObject.SetActive(currentMode == EquipmentPageMode.Equip &&
                                           item.InstanceId == selectedInstanceId);
            }

            if (view.UpgradeArrow != null)
            {
                view.UpgradeArrow.gameObject.SetActive(currentMode == EquipmentPageMode.Equip &&
                                                       EquipmentUpgradeEvaluator.EvaluatePowerDelta(item) > 0);
            }

            if (view.LockObject != null)
            {
                view.LockObject.SetActive(item.IsLocked);
            }

            if (view.ClickButton != null)
            {
                view.ClickButton.interactable = currentMode != EquipmentPageMode.Dismantle ||
                                                (!item.IsEquipped && !item.IsLocked);
            }
        }

        // 재사용 풀에서 비활성화하기 전에 슬롯 내부 표시와 선택 상태를 초기화한다.
        private void ClearSlot(SlotView view)
        {
            view.BoundInstanceId = null;

            if (view.NormalArea != null)
            {
                view.NormalArea.gameObject.SetActive(false);
            }

            if (view.ItemIcon != null)
            {
                view.ItemIcon.gameObject.SetActive(false);
            }

            if (view.AddIndicator != null)
            {
                view.AddIndicator.SetActive(true);
            }

            if (view.TextLevel != null)
            {
                view.TextLevel.SetActive(false);
            }

            if (view.EquippedLabelText != null)
            {
                view.EquippedLabelText.text = string.Empty;
            }

            if (view.CheckObject != null)
            {
                view.CheckObject.SetActive(false);
            }

            if (view.FocusObject != null)
            {
                view.FocusObject.SetActive(false);
            }

            if (view.UpgradeArrow != null)
            {
                view.UpgradeArrow.gameObject.SetActive(false);
            }

            if (view.LockObject != null)
            {
                view.LockObject.SetActive(false);
            }

            if (view.ClickButton != null)
            {
                view.ClickButton.interactable = false;
            }
        }

        private void RefreshCapacityText()
        {
            if (capacityText != null)
            {
                capacityText.text = $"{EquipmentInventoryRuntime.TotalQuantity} / {EquipmentInventoryRuntime.MaxTotalQuantity}";
            }
        }

        private void RefreshSelection()
        {
            PruneDismantleSelection();
            for (var i = 0; i < slots.Count; i++)
            {
                var view = slots[i];
                if (view.CheckObject != null)
                {
                    view.CheckObject.SetActive(view.BoundInstanceId != null &&
                        currentMode == EquipmentPageMode.Dismantle &&
                        dismantleSelection.Contains(view.BoundInstanceId));
                }

                if (view.FocusObject != null)
                {
                    view.FocusObject.SetActive(view.BoundInstanceId != null &&
                        currentMode == EquipmentPageMode.Equip &&
                        view.BoundInstanceId == selectedInstanceId);
                }
            }

            EquipmentItemView selectedItem = default;
            var hasSelection = currentMode == EquipmentPageMode.Equip &&
                                !string.IsNullOrEmpty(selectedInstanceId) &&
                                EquipmentInventoryRuntime.TryGetItem(selectedInstanceId, out selectedItem);

            var nameText = selectedItemName?.GetComponent<TMP_Text>();
            var coreStatText = selectedItemStat?.GetComponent<TMP_Text>();
            var optionStatText = selectedItemRandomOptionStat?.GetComponent<TMP_Text>();
            if (hasSelection && selectedItem.Definition != null)
            {
                if (nameText != null)
                {
                    nameText.text = selectedItem.Definition.DisplayName;
                }

                var randomOptionText = BuildRandomOptionText(selectedItem);
                if (optionStatText != null)
                {
                    // 기본옵션(핵심 능력치)과 추가 랜덤 옵션을 서로 다른 텍스트 칸에 나눠서 표시한다.
                    if (coreStatText != null)
                    {
                        coreStatText.text = selectedItem.Definition.GetCoreStatSummary();
                    }

                    optionStatText.text = randomOptionText;
                }
                else if (coreStatText != null)
                {
                    // Tools > ProjectMT > 장비창 메뉴를 아직 실행하지 않아 전용 칸이 없는 경우의 임시 대체.
                    var combined = selectedItem.Definition.GetCoreStatSummary();
                    if (!string.IsNullOrEmpty(randomOptionText))
                    {
                        combined += "\n[랜덤 옵션]\n" + randomOptionText;
                    }

                    coreStatText.text = combined;
                }
            }
            else
            {
                if (nameText != null)
                {
                    nameText.text = "장비를 선택하세요";
                }

                if (coreStatText != null)
                {
                    coreStatText.text = string.Empty;
                }

                if (optionStatText != null)
                {
                    optionStatText.text = string.Empty;
                }
            }

            if (equipButton != null)
            {
                equipButton.interactable = currentMode == EquipmentPageMode.Equip &&
                                           !requestInFlight &&
                                           EquipmentUpgradeEvaluator.GetBestUpgradeInstanceIds().Count > 0;
            }

            if (equipButtonText != null)
            {
                equipButtonText.text = AutoEquipButtonText;
            }

            if (equipButtonRoot != null)
            {
                equipButtonRoot.gameObject.SetActive(currentMode == EquipmentPageMode.Equip);
            }

            var showLock = currentMode == EquipmentPageMode.Equip && hasSelection;
            if (lockButtonRoot != null)
            {
                lockButtonRoot.gameObject.SetActive(showLock);
            }

            if (lockButton != null)
            {
                lockButton.interactable = showLock && !requestInFlight;
            }

            if (lockButtonText != null)
            {
                lockButtonText.text = hasSelection && selectedItem.IsLocked ? "해제" : "잠금";
            }

            RefreshDismantleSummary();
            RefreshDismantleControls();
        }

        private long CalculateSelectedDismantleReward()
        {
            var result = 0L;
            foreach (var instanceId in dismantleSelection)
            {
                if (EquipmentInventoryRuntime.TryGetItem(instanceId, out var item))
                {
                    result += EquipmentDismantleRules.GetUpgradeStoneAmount(item.Grade);
                }
            }

            return result;
        }

        private void RefreshDismantleSummary()
        {
            var stoneAmount = CalculateSelectedDismantleReward();
            if (dismantleSummaryCountText != null)
            {
                dismantleSummaryCountText.text = $"선택 장비 {dismantleSelection.Count}개";
            }

            if (dismantleSummaryRewardText != null)
            {
                dismantleSummaryRewardText.text = $"획득 강화석 {stoneAmount:N0}개";
            }

            if (dismantleBottomSummaryText != null)
            {
                dismantleBottomSummaryText.text = $"선택 {dismantleSelection.Count}개 / 강화석 {stoneAmount:N0}개";
            }

            var selectedItems = dismantleSelection
                .Select(instanceId => EquipmentInventoryRuntime.TryGetItem(instanceId, out var item) ? item : default)
                .Where(item => !string.IsNullOrEmpty(item.InstanceId))
                .Take(dismantlePreviewSlots.Count)
                .ToList();
            for (var index = 0; index < dismantlePreviewSlots.Count; index++)
            {
                var preview = dismantlePreviewSlots[index];
                var visible = index < selectedItems.Count;
                preview.Root.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var item = selectedItems[index];
                if (preview.Icon != null && partIconSprites.TryGetValue(item.Part, out var icon))
                {
                    preview.Icon.sprite = icon;
                    preview.Icon.color = Color.white;
                }

                if (preview.Frame != null)
                {
                    preview.Frame.color = GetDismantlePreviewColor(item.Grade);
                }
            }
        }

        // 분해 미리보기 배경색. 인벤토리 프레임에서 뽑은 실제 색을 우선 쓰고, 없으면 팔레트 근사값을 쓴다.
        private Color GetDismantlePreviewColor(EquipmentGrade grade)
        {
            if (FrameVariantSuffixByGrade.TryGetValue(grade, out var suffix)
                && frameVariantSwatchColors.TryGetValue(suffix, out var sampledColor))
            {
                return sampledColor;
            }

            return ItemGradeFramePalette.GetColor(grade);
        }

        private void RefreshDismantleControls()
        {
            var isDismantleMode = currentMode == EquipmentPageMode.Dismantle;
            if (dismantleGradeButtonRoot != null)
            {
                dismantleGradeButtonRoot.gameObject.SetActive(isDismantleMode);
            }

            if (dismantleGradeButtonText != null)
            {
                dismantleGradeButtonText.text = $"{EquipmentGradeInfo.GetDisplayName(dismantleGradeThreshold)} 이하";
            }

            if (dismantleGradeButton != null)
            {
                dismantleGradeButton.interactable = isDismantleMode && !requestInFlight;
            }

            if (dismantleAutoSelectButtonRoot != null)
            {
                dismantleAutoSelectButtonRoot.gameObject.SetActive(isDismantleMode);
            }

            if (dismantleAutoSelectButtonText != null)
            {
                dismantleAutoSelectButtonText.text = dismantleSelection.Count > 0 ? "선택 해제" : "이하 전체 선택";
            }

            if (dismantleAutoSelectButton != null)
            {
                dismantleAutoSelectButton.interactable = isDismantleMode && !requestInFlight;
            }

            if (dismantleButtonRoot != null)
            {
                dismantleButtonRoot.gameObject.SetActive(isDismantleMode);
            }

            if (dismantleButton != null)
            {
                dismantleButton.interactable = isDismantleMode && dismantleSelection.Count > 0 && !requestInFlight;
            }

            if (dismantleButtonText != null)
            {
                dismantleButtonText.text = requestInFlight
                    ? "처리 중"
                    : "분해";
            }

            if (dismantleClearButton != null)
            {
                dismantleClearButton.interactable = isDismantleMode && dismantleSelection.Count > 0 && !requestInFlight;
            }

            if (offlineAutoDismantleOpenButtonRoot != null)
            {
                offlineAutoDismantleOpenButtonRoot.gameObject.SetActive(isDismantleMode);
            }

            if (offlineAutoDismantleOpenButtonText != null)
            {
                var policy = progress != null && progress.IsLoaded
                    ? progress.View.Equipment.OfflineAutoDismantlePolicy
                    : OfflineAutoDismantlePolicy.Common;
                offlineAutoDismantleOpenButtonText.text =
                    $"방치 설정\n{OfflineAutoDismantlePolicyInfo.GetDisplayName(policy)}";
            }

            if (offlineAutoDismantleOpenButton != null)
            {
                offlineAutoDismantleOpenButton.interactable =
                    isDismantleMode && !requestInFlight && progress != null && progress.IsLoaded;
            }
        }

        // "추가 랜덤 옵션" 전용 텍스트 칸에 넣을 내용만 만든다(핵심 능력치는 별도로
        // selectedItem.Definition.GetCoreStatSummary()가 표시).
        private static string BuildRandomOptionText(EquipmentItemView item)
        {
            var options = item.Instance?.RandomOptions;
            if (options == null || options.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (var i = 0; i < options.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(EquipmentOptionInfo.FormatOption(options[i].Type, options[i].Value));
            }

            return builder.ToString();
        }

        // ---------------------------------------------------------------
        // 군단장 장착 슬롯 6개 (WeaponSlot 등) - 장착 중인 장비 아이콘을 표시한다.
        // ---------------------------------------------------------------

        // 미장착 상태는 인벤토리의 빈 슬롯("+")과 완전히 같은 모습으로 보여야 한다. 군단장 슬롯에도
        // 인벤토리와 동일한 구조로 Add_1("+" 표시)이 이미 있으므로, 그걸 그대로 켜고/끄는 방식으로 맞춘다.
        private void RefreshCommanderSlots()
        {
            foreach (var pair in commanderSlots)
            {
                var slotTransform = pair.Value;
                if (slotTransform == null)
                {
                    continue;
                }

                var itemFrame = slotTransform.Find("ItemFrame_01");
                if (itemFrame == null)
                {
                    continue;
                }

                // Lv 표시는 나중에 실제 레벨 시스템이 붙으면 다시 켤 것이므로, 지금은 항상 비활성화한다.
                var commanderLevelText = slotTransform.Find("Text_Level")?.gameObject;
                if (commanderLevelText != null)
                {
                    commanderLevelText.SetActive(false);
                }

                var icon = itemFrame.Find("Item")?.GetComponent<Image>();
                var normalArea = itemFrame.Find("NormalArea");
                var addIndicator = itemFrame.Find("Add_1")?.gameObject;
                if (icon == null)
                {
                    continue;
                }

                if (EquipmentInventoryRuntime.TryGetEquipped(pair.Key, out var equipped))
                {
                    if (addIndicator != null)
                    {
                        addIndicator.SetActive(false);
                    }

                    if (normalArea != null)
                    {
                        normalArea.gameObject.SetActive(true);
                    }

                    icon.gameObject.SetActive(true);
                    icon.color = Color.white; // 아이콘은 고유 색 그대로 유지

                    ApplyFrameVariant(normalArea, equipped.Grade); // 등급에 맞는 기존 프레임(테두리)으로 교체
                }
                else
                {
                    if (normalArea != null)
                    {
                        normalArea.gameObject.SetActive(false);
                    }

                    icon.gameObject.SetActive(false);

                    if (addIndicator != null)
                    {
                        addIndicator.SetActive(true); // 인벤토리 빈 슬롯과 동일한 "+" 표시
                    }
                }
            }
        }

        // ---------------------------------------------------------------
        // 편성 전체에 적용되는 장비 보너스 카드
        // ---------------------------------------------------------------

        private static readonly Regex PowerPattern = new Regex(@"전투력\s*[\d,]+");

        private void RefreshCommanderStats()
        {
            var stats = EquipmentLegionBonusCalculator.CalculateTotal();

            if (statGrid != null)
            {
                for (var i = 0; i < statGrid.childCount; i++)
                {
                    var card = statGrid.GetChild(i);
                    var label = card.Find("Label")?.GetComponent<TMP_Text>();
                    var value = card.Find("Value")?.GetComponent<TMP_Text>();
                    if (label == null || value == null)
                    {
                        continue;
                    }

                    var statType = ResolveStatType(label.text);
                    value.text = FormatStatValue(statType, stats.GetValue(statType));
                }
            }

            if (commanderSummaryText != null)
            {
                const string powerText = "군단 장비 보너스";
                commanderSummaryText.text = PowerPattern.IsMatch(commanderSummaryText.text)
                    ? PowerPattern.Replace(commanderSummaryText.text, powerText)
                    : powerText;
            }
        }

        // StatGrid 카드는 6개(공격력/체력/방어력/공격속도/이동속도/치명타)뿐이라 나머지 7개 능력치
        // (치피/스킬·보스·일반 몬스터 피해/쿨감/방관/피해감소)는 아직 표시할 카드가 없다. 카드가
        // 추가되면 이 매핑에 항목을 늘리면 된다.
        private static EquipmentStatType ResolveStatType(string labelText)
        {
            if (labelText.Contains("공격속도")) return EquipmentStatType.AttackSpeed;
            if (labelText.Contains("이동속도")) return EquipmentStatType.MoveSpeed;
            if (labelText.Contains("공격")) return EquipmentStatType.AttackPower;
            if (labelText.Contains("방어")) return EquipmentStatType.Defense;
            if (labelText.Contains("치명")) return EquipmentStatType.CriticalRate;
            return EquipmentStatType.MaxHealth; // "체력"
        }

        private static string FormatStatValue(EquipmentStatType statType, float value)
        {
            return $"+{value:0.#}%";
        }
    }
}
