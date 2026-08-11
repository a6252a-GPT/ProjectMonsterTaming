using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
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
        private const string EquipButtonEquipText = "장착";
        private const string EquipButtonUnequipText = "해제";

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
            public TMP_Text EquippedLabelText; // 장착 중인 아이템을 인벤토리에서도 구분할 수 있도록 아이콘 아래 "[장착]" 표시
            public Button ClickButton;
            public string BoundInstanceId; // 이 슬롯이 현재 표시 중인 인스턴스 ID (없으면 null → 빈 슬롯)
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
        // (ItemFrame_01_Normal_Green/Blue/Yellow/Plum/Red)을 그대로 재사용한다.
        private static readonly Dictionary<EquipmentGrade, string> FrameVariantSuffixByGrade = new Dictionary<EquipmentGrade, string>
        {
            { EquipmentGrade.Common, "Green" },
            { EquipmentGrade.Rare, "Blue" },
            { EquipmentGrade.Epic, "Yellow" },
            { EquipmentGrade.Legendary, "Plum" },
            { EquipmentGrade.Mythic, "Red" },
        };

        private const string FrameVariantPrefix = "ItemFrame_01_Normal_";
        private readonly Dictionary<string, GameObject> frameVariantTemplates = new Dictionary<string, GameObject>();
        private Transform frameVariantTemplateStorage;

        [SerializeField] private TMP_FontAsset equippedLabelFont;

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
        private ScrollRect inventoryScrollRect;
        private RectTransform inventoryContentRoot;
        private GameObject inventorySlotCellTemplate;

        private EquipmentPart? currentFilter; // null = 전체
        private bool sortGradeDescending = true;
        private string selectedInstanceId; // 현재 상세 영역에 표시 중인 장비 인스턴스 ID

        private void Awake()
        {
            CacheReferences();
            BuildFilterButtons();
            BuildSortButton();
            BuildInventorySlots();
            BuildEquipButton();
        }

        private void OnEnable()
        {
            EquipmentInventoryRuntime.Changed += HandleInventoryChanged;
            RefreshAll();
            ResetInventoryScrollPosition();
        }

        private void OnDisable()
        {
            EquipmentInventoryRuntime.Changed -= HandleInventoryChanged;
        }

        // MainBattleSceneRoot가 씬 조립 시점에 진행 데이터 서비스를 주입한다. 실제 보유/장착 데이터는
        // EquipmentInventoryRuntime(정적 파사드)이 들고 있으므로, 여기서는 그 파사드에 서비스와
        // 카탈로그를 연결해주기만 하면 된다.
        public void Configure(IGameProgressService progress)
        {
            EquipmentInventoryRuntime.Configure(progress, ResolveCatalog());
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
                var image = pair.Value != null ? pair.Value.Find("ItemFrame_01/Item")?.GetComponent<Image>() : null;
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
            }
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
        // 부위 필터 탭 - 필터 UI 목업에는 Button이 연결돼 있지 않아 런타임에 추가한다.
        // 목업 탭(전체/무기/방패/방어구/장신구/신발) 5분류를 우리 6부위에 맞춰 대응시킨다.
        // 장갑(Glove)은 전용 탭이 없어 "전체"에서만 표시된다.
        // ---------------------------------------------------------------

        private void BuildFilterButtons()
        {
            allFilterTab = FindDeep(transform, "Filter_All_SELECTED");
            AddFilterTab("Filter_Weapon", EquipmentPart.Weapon);
            AddFilterTab("Filter_Shield", EquipmentPart.Helmet);
            AddFilterTab("Filter_Armor", EquipmentPart.Armor);
            AddFilterTab("Filter_Accessory", EquipmentPart.Ring);
            AddFilterTab("Filter_Boots", EquipmentPart.Boots);

            if (allFilterTab != null)
            {
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
            var button = EnsureButton(tab);
            button.onClick.AddListener(() => SetFilter(part));
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
            SetFocusActive(allFilterTab, currentFilter == null);
            foreach (var pair in filterTabs)
            {
                SetFocusActive(pair.Value, currentFilter == pair.Key);
            }
        }

        private static void SetFocusActive(Transform tab, bool active)
        {
            var focus = tab != null ? tab.Find("Focus") : null;
            if (focus != null)
            {
                focus.gameObject.SetActive(active);
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
                CheckObject = FindDeep(slotRoot, "Check")?.gameObject
            };

            view.EquippedLabelText = CreateEquippedLabel(slotRoot);
            view.ClickButton = EnsureButton(slotRoot);
            if (resetClickListeners)
            {
                view.ClickButton.onClick.RemoveAllListeners();
            }

            var capturedView = view;
            view.ClickButton.onClick.AddListener(() => HandleSlotClicked(capturedView));
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

        private void HandleSlotClicked(SlotView view)
        {
            if (string.IsNullOrEmpty(view.BoundInstanceId))
            {
                return; // 빈 슬롯
            }

            selectedInstanceId = view.BoundInstanceId;
            RefreshSelection();
        }

        // ---------------------------------------------------------------
        // 장착 버튼
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
            if (string.IsNullOrEmpty(selectedInstanceId) ||
                !EquipmentInventoryRuntime.TryGetItem(selectedInstanceId, out var item))
            {
                return;
            }

            if (item.IsEquipped)
            {
                await EquipmentInventoryRuntime.TryUnequipAsync(item.Part);
            }
            else
            {
                await EquipmentInventoryRuntime.TryEquipAsync(selectedInstanceId);
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

            var ordered = sortGradeDescending
                ? query.OrderByDescending(item => (int)item.Grade).ThenBy(item => item.Part)
                : query.OrderBy(item => (int)item.Grade).ThenBy(item => item.Part);
            var list = ordered.ToList();
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

                view.ItemIcon.color = Color.white; // 아이콘 고유 색은 그대로 유지
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
                view.CheckObject.SetActive(item.InstanceId == selectedInstanceId);
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
            for (var i = 0; i < slots.Count; i++)
            {
                var view = slots[i];
                if (view.CheckObject != null)
                {
                    view.CheckObject.SetActive(view.BoundInstanceId != null && view.BoundInstanceId == selectedInstanceId);
                }
            }

            EquipmentItemView selectedItem = default;
            var hasSelection = !string.IsNullOrEmpty(selectedInstanceId) &&
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
                equipButton.interactable = hasSelection;
            }

            if (equipButtonText != null)
            {
                equipButtonText.text = hasSelection && selectedItem.IsEquipped
                    ? EquipButtonUnequipText
                    : EquipButtonEquipText;
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
        // 군단장 능력치 카드(StatGrid) + 총전투력(CommanderSummary)
        // ---------------------------------------------------------------

        private static readonly Regex PowerPattern = new Regex(@"전투력\s*[\d,]+");

        private void RefreshCommanderStats()
        {
            var stats = CommanderEquipmentStatsCalculator.CalculateTotal();

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
                var power = stats.EstimatePower();
                var powerText = $"전투력 {power:N0}";
                commanderSummaryText.text = PowerPattern.IsMatch(commanderSummaryText.text)
                    ? PowerPattern.Replace(commanderSummaryText.text, powerText)
                    : commanderSummaryText.text; // 형식이 다르면 임의로 덮어쓰지 않는다
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
            switch (statType)
            {
                case EquipmentStatType.AttackSpeed:
                case EquipmentStatType.MoveSpeed:
                    return value.ToString("0.00");
                case EquipmentStatType.CriticalRate:
                    return $"{value:0}%";
                default:
                    return value.ToString("N0");
            }
        }
    }
}
