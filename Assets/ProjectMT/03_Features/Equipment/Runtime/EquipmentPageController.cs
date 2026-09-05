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
    public sealed partial class EquipmentPageController : MonoBehaviour
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
            public GameObject TextLevel; // 현재 인스턴스의 하단 레벨 표시, 빈 슬롯에서 초기화
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
            public TMP_Text LevelText;
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
        private int questDismantleHintStep;
        private bool requestInFlight;
        private Action combatInputSaved;
        private IGameProgressService progress;
        private Coroutine pendingInventoryRefresh;

        // 퀘스트 안내용 읽기 전용 상태·버튼 노출. 실제 장비 기능의 동작은 바꾸지 않는다.
        public bool IsDismantleMode => currentMode == EquipmentPageMode.Dismantle;
        public int QuestDismantleHintStep => questDismantleHintStep;
        public Button QuestDismantleModeTabButton => dismantleModeTabButton;
        public Button QuestDismantleGradeButton => dismantleGradeButton;
        public Button QuestDismantleAutoSelectButton => dismantleAutoSelectButton;
        public Button QuestDismantleActionButton => dismantleButton;
        public Button QuestAutoEquipButton => equipButton;

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
            if (pendingInventoryRefresh != null)
            {
                StopCoroutine(pendingInventoryRefresh);
                pendingInventoryRefresh = null;
            }

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
                    Icon = previewRoot.Find("Icon")?.GetComponent<Image>(),
                    LevelText = previewRoot.Find("Text_Level")?.GetComponent<TMP_Text>()
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

        // ---------------------------------------------------------------
        // 편성 전체에 적용되는 장비 보너스 카드
        // ---------------------------------------------------------------

        private static readonly Regex PowerPattern = new Regex(@"전투력\s*[\d,]+");
    }
}
