using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Equipment
{
    // 08.09 안건준 추가 - 장비창(PF_CommanderEquipmentPage) 전체를 실제 장비 데이터와 연결하는 컨트롤러.
    //
    // 이 스크립트 하나가 EquipmentContent 아래에 있는
    //  1) 보유 장비 목록(OwnedEquipmentPanel: InventorySlot_01~15, 필터·정렬·수량 표시)
    //  2) 장착 버튼(EquipButton), 선택 장비 상세 정보(SelectedItemName/Stat)
    //  3) 군단장 장착 슬롯 6개(WeaponSlot 등)와 능력치 카드(StatGrid), 총전투력(CommanderSummary)
    // 를 모두 코드에서 이름으로 찾아 연결한다(팀원이 만든 프리팹 구조를 그대로 유지하면서
    // 인스펙터에 미리 참조를 걸어두지 않아도 되도록, transform 이름 기반으로 자동 탐색한다).
    //
    // 기존 프리팹/씬 파일은 건드리지 않고 이 컴포넌트 하나만 EquipmentContent에 추가해서 동작한다.
    [DisallowMultipleComponent]
    public sealed class EquipmentPageController : MonoBehaviour
    {
        private const int InventorySlotCount = 15;
        private const string EquipButtonEquipText = "장착";
        private const string EquipButtonUnequipText = "해제";

        // 인벤토리 슬롯 1개에 대한 런타임 바인딩 정보. InventorySlot_01~15 각각에 대해 하나씩 만든다.
        private sealed class SlotView
        {
            public Transform Root;
            public Image ItemIcon;
            public Transform NormalArea;
            public GameObject AddIndicator; // 08.09 안건준 추가 - 비어있을 때 표시하는 "+" 표시(Add_1)
            public GameObject TextLevel; // 08.09 안건준 추가 - 슬롯이 비어있을 때는 숨겨야 하는 목업 레벨 텍스트(값은 건드리지 않음)
            public GameObject CheckObject;
            public TMP_Text StackCountText;
            public TMP_Text EquippedLabelText; // 08.09 안건준 추가 - 장착 중인 스택을 인벤토리에서도 구분할 수 있도록 아이콘 위에 "[장착]" 표시
            public Button ClickButton;
            public EquipmentStack BoundStack; // 이 슬롯이 현재 표시 중인 스택 (없으면 null → 빈 슬롯)
        }

        private readonly List<SlotView> slots = new List<SlotView>();
        private readonly Dictionary<EquipmentPart, Transform> commanderSlots = new Dictionary<EquipmentPart, Transform>();
        private readonly Dictionary<EquipmentPart, Transform> filterTabs = new Dictionary<EquipmentPart, Transform>();

        // 08.09 안건준 추가 - 부위별 대표 아이콘 스프라이트. 군단장 장착 슬롯(WeaponSlot 등)은
        // 부위마다 고정이라 항상 올바른 아이콘이 미리 박혀 있으므로, 그 스프라이트를 그대로 재사용해서
        // 인벤토리 슬롯도 "실제로 담긴 부위"에 맞는 아이콘을 보여주도록 한다.
        // (기존에는 슬롯 위치마다 목업 때 박혀 있던 서로 다른 아이콘을 색상만 바꿔서 재사용했기 때문에
        //  무기를 장착해도 장갑 아이콘이 뜨는 등 부위와 아이콘이 어긋나는 문제가 있었다.)
        private readonly Dictionary<EquipmentPart, Sprite> partIconSprites = new Dictionary<EquipmentPart, Sprite>();

        // 08.09 안건준 추가 - 요청사항: 테두리 색은 런타임 tint로 흉내내지 말고, 목업에 이미 있는
        // 등급별 완성 프레임(ItemFrame_01_Normal_Green/Blue/Yellow/Plum/Red)을 "기존에 꺼 그대로" 재사용한다.
        // 각 슬롯의 NormalArea 밑에 있는 프레임 오브젝트를 실제 등급에 맞는 것으로 통째로 교체하는 방식.
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
        private Transform selectedItemStat;
        private Transform equipButtonRoot;
        private Button equipButton;
        private TMP_Text equipButtonText;

        private EquipmentPart? currentFilter; // null = 전체
        private bool sortGradeDescending = true;
        private string selectedKey; // 현재 상세 영역에 표시 중인 장비 종류 Key

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
            EquipmentInventoryRuntime.InventoryChanged += HandleInventoryChanged;
            EquipmentInventoryRuntime.EquippedChanged += HandleEquippedChanged;
            RefreshAll();
        }

        private void OnDisable()
        {
            EquipmentInventoryRuntime.InventoryChanged -= HandleInventoryChanged;
            EquipmentInventoryRuntime.EquippedChanged -= HandleEquippedChanged;
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

        // 08.09 안건준 추가 - 군단장 장착 슬롯 6개는 부위별로 고정된 오브젝트라 항상 올바른 아이콘
        // 스프라이트를 갖고 있다. 그 스프라이트를 부위별 대표 아이콘으로 캐시해서 인벤토리 슬롯에도 그대로 쓴다.
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

        // 08.09 안건준 추가 - 목업 곳곳(인벤토리 슬롯들)에 흩어져 있는 등급별 프레임
        // (ItemFrame_01_Normal_Green/Blue/Yellow/Plum/Red)을 이름별로 하나씩 찾아서,
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

        // ---------------------------------------------------------------
        // 부위 필터 탭 - 필터 UI 목업에는 Button이 연결돼 있지 않아 런타임에 추가한다.
        // 목업 탭(전체/무기/방패/방어구/장신구/신발) 5분류를 우리 6부위에 맞춰 대응시킨다.
        // 장갑(Glove)은 전용 탭이 없어 "전체"에서만 표시된다 (팀 확인 필요 - 요약에 안내).
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
        }

        private void RefreshSortLabelText()
        {
            if (sortLabelText != null)
            {
                sortLabelText.text = sortGradeDescending ? "장착 가능 우선 · 등급 높은순" : "장착 가능 우선 · 등급 낮은순";
            }
        }

        // ---------------------------------------------------------------
        // 인벤토리 슬롯 15개
        // ---------------------------------------------------------------

        private void BuildInventorySlots()
        {
            for (var i = 1; i <= InventorySlotCount; i++)
            {
                var slotName = $"InventorySlot_{i:00}";
                var slotRoot = FindDeep(transform, slotName);
                if (slotRoot == null)
                {
                    continue;
                }

                var view = new SlotView
                {
                    Root = slotRoot,
                    ItemIcon = slotRoot.Find("ItemFrame_01/Item")?.GetComponent<Image>(),
                    NormalArea = slotRoot.Find("ItemFrame_01/NormalArea"),
                    AddIndicator = slotRoot.Find("ItemFrame_01/Add_1")?.gameObject,
                    TextLevel = slotRoot.Find("Text_Level")?.gameObject,
                    CheckObject = slotRoot.Find("Check")?.gameObject
                };

                view.StackCountText = CreateStackCountLabel(slotRoot);
                view.EquippedLabelText = CreateEquippedLabel(slotRoot);
                view.ClickButton = EnsureButton(slotRoot);
                var capturedView = view;
                view.ClickButton.onClick.AddListener(() => HandleSlotClicked(capturedView));

                slots.Add(view);
            }
        }

        // 08.09 안건준 추가 - 요청사항: 중첩 수량(+N)은 기존 Text_Level(임시 Lv 표기, 데이터 연결 금지)을
        // 재사용하지 않고 새 텍스트 오브젝트를 만들어 표시한다.
        private static TMP_Text CreateStackCountLabel(Transform slotRoot)
        {
            var labelObject = new GameObject("StackCountLabel", typeof(RectTransform));
            labelObject.transform.SetParent(slotRoot, false);
            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-6f, 6f);
            rect.sizeDelta = new Vector2(60f, 28f);

            var text = labelObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 20f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.BottomRight;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        // 08.09 안건준 추가 - 요청사항: 인벤토리에서도 어떤 스택이 지금 장착 중인지 구분할 수 있도록,
        // 아이콘 그림 아래쪽에 "[장착]" 글자를 새 텍스트 오브젝트로 표시한다.
        private TMP_Text CreateEquippedLabel(Transform slotRoot)
        {
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
            if (view.BoundStack == null)
            {
                return; // 빈 슬롯
            }

            selectedKey = view.BoundStack.Key;
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

        private void HandleEquipButtonClicked()
        {
            if (string.IsNullOrEmpty(selectedKey) ||
                !EquipmentInventoryRuntime.TryGetStack(selectedKey, out var stack))
            {
                return;
            }

            if (stack.IsEquipped)
            {
                EquipmentInventoryRuntime.TryUnequip(stack.Definition.Part);
            }
            else
            {
                EquipmentInventoryRuntime.TryEquip(selectedKey);
            }
        }

        // ---------------------------------------------------------------
        // 이벤트 핸들러 / 새로 그리기
        // ---------------------------------------------------------------

        private void HandleInventoryChanged() => RefreshAll();
        private void HandleEquippedChanged(EquipmentPart part) => RefreshAll();

        private void RefreshAll()
        {
            RefreshInventoryList();
            RefreshCommanderSlots();
            RefreshCommanderStats();
        }

        private void RefreshInventoryList()
        {
            var query = EquipmentInventoryRuntime.Stacks.AsEnumerable();
            if (currentFilter.HasValue)
            {
                query = query.Where(s => s.Definition.Part == currentFilter.Value);
            }

            var ordered = sortGradeDescending
                ? query.OrderByDescending(s => (int)s.Definition.Grade).ThenBy(s => s.Definition.Part)
                : query.OrderBy(s => (int)s.Definition.Grade).ThenBy(s => s.Definition.Part);
            var list = ordered.ToList();

            for (var i = 0; i < slots.Count; i++)
            {
                if (i < list.Count)
                {
                    BindSlot(slots[i], list[i]);
                }
                else
                {
                    ClearSlot(slots[i]);
                }
            }

            // 선택된 장비가 더 이상 보유 목록에 없으면(수량 0 등) 선택 해제.
            if (!string.IsNullOrEmpty(selectedKey) && !EquipmentInventoryRuntime.TryGetStack(selectedKey, out _))
            {
                selectedKey = null;
            }

            RefreshCapacityText();
            RefreshSelection();
        }

        // 08.09 안건준 수정 - 슬롯 오브젝트 자체는 항상 켜둔 채로("+" 표시가 기본으로 보이도록),
        // 아이템이 있을 때만 아이콘 영역(NormalArea/Item)을 켜고 "+" 표시(Add_1)를 끄는 방식으로 변경했다.
        // 아이콘도 부위별 대표 스프라이트(partIconSprites)로 바꿔서, 실제 담긴 부위와 다른 아이콘이
        // 보이던 문제(예: 무기를 장착했는데 장갑 아이콘처럼 보이는 문제)를 해결한다.
        // 08.09 안건준 수정 - 요청사항: 아이템 아이콘은 원래 색(고유 아트) 그대로 보여야 하므로 더 이상
        // 등급색으로 물들이지 않는다. 테두리는 런타임 tint가 아니라, 목업에 있던 등급별 완성 프레임
        // (ApplyFrameVariant)을 그대로 갖다 끼워서 표시한다.
        private void BindSlot(SlotView view, EquipmentStack stack)
        {
            view.BoundStack = stack;

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
                if (partIconSprites.TryGetValue(stack.Definition.Part, out var partSprite) && partSprite != null)
                {
                    view.ItemIcon.sprite = partSprite; // 부위에 맞는 아이콘으로 교체 (기존엔 슬롯 위치의 목업 아이콘이 그대로 남아있었음)
                }

                view.ItemIcon.color = Color.white; // 아이콘 고유 색은 그대로 유지
            }

            ApplyFrameVariant(view.NormalArea, stack.Definition.Grade); // 등급에 맞는 기존 프레임(테두리)으로 교체

            // 08.09 안건준 수정 - 요청사항: Lv 표시는 나중에 실제 레벨 시스템이 붙으면 다시 켤 것이므로,
            // 지금은 항상 비활성화한다(목업 값 그대로 노출되는 것을 방지).
            if (view.TextLevel != null)
            {
                view.TextLevel.SetActive(false);
            }

            if (view.StackCountText != null)
            {
                view.StackCountText.text = stack.TotalQuantity > 1 ? $"+{stack.TotalQuantity - 1}" : string.Empty;
            }

            // 08.09 안건준 추가 - 요청사항: 인벤토리에서도 어떤 스택이 지금 장착 중인지 아이콘 아래에 표시한다.
            if (view.EquippedLabelText != null)
            {
                view.EquippedLabelText.text = stack.IsEquipped ? "[장착]" : string.Empty;
            }

            if (view.CheckObject != null)
            {
                view.CheckObject.SetActive(stack.Key == selectedKey);
            }
        }

        // 08.09 안건준 수정 - 요청사항: 보유하지 않은 슬롯도 "+" 슬롯 형태로 항상 보이고,
        // 새로 얻은 장비가 그 위에 채워지는 방식으로 동작해야 하므로 슬롯 자체를 끄지 않는다.
        private void ClearSlot(SlotView view)
        {
            view.BoundStack = null;

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

            if (view.StackCountText != null)
            {
                view.StackCountText.text = string.Empty;
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
                    view.CheckObject.SetActive(view.BoundStack != null && view.BoundStack.Key == selectedKey);
                }
            }

            EquipmentStack selectedStack = null;
            if (!string.IsNullOrEmpty(selectedKey))
            {
                EquipmentInventoryRuntime.TryGetStack(selectedKey, out selectedStack);
            }

            var nameText = selectedItemName?.GetComponent<TMP_Text>();
            var statText = selectedItemStat?.GetComponent<TMP_Text>();
            if (selectedStack != null)
            {
                if (nameText != null)
                {
                    nameText.text = $"{selectedStack.Definition.DisplayName} (보유 {selectedStack.TotalQuantity}개)";
                }

                if (statText != null)
                {
                    statText.text = selectedStack.Definition.GetStatSummary();
                }
            }
            else
            {
                if (nameText != null)
                {
                    nameText.text = "장비를 선택하세요";
                }

                if (statText != null)
                {
                    statText.text = string.Empty;
                }
            }

            if (equipButton != null)
            {
                equipButton.interactable = selectedStack != null;
            }

            if (equipButtonText != null)
            {
                equipButtonText.text = selectedStack != null && selectedStack.IsEquipped
                    ? EquipButtonUnequipText
                    : EquipButtonEquipText;
            }
        }

        // ---------------------------------------------------------------
        // 군단장 장착 슬롯 6개 (WeaponSlot 등) - 장착 중인 장비 아이콘을 표시한다.
        // ---------------------------------------------------------------

        // 08.09 안건준 수정 - 요청사항: 미장착 상태는 색만 회색으로 바꾸는 게 아니라, 인벤토리의
        // 빈 슬롯("+")과 완전히 같은 모습으로 보여야 한다. 군단장 슬롯에도 인벤토리와 동일한 구조로
        // Add_1("+" 표시)이 이미 있으므로, 그걸 그대로 켜고/끄는 방식으로 인벤토리 빈 슬롯과 똑같이 맞춘다.
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

                // 08.09 안건준 수정 - 요청사항: Lv 표시는 나중에 실제 레벨 시스템이 붙으면 다시 켤 것이므로,
                // 지금은 장착 여부와 관계없이 항상 비활성화한다(목업 값 그대로 노출되는 것을 방지).
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

                var equipped = EquipmentInventoryRuntime.GetEquippedStack(pair.Key);
                if (equipped != null)
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
                    icon.color = Color.white; // 08.09 안건준 수정 - 아이콘은 고유 색 그대로 유지

                    ApplyFrameVariant(normalArea, equipped.Definition.Grade); // 등급에 맞는 기존 프레임(테두리)으로 교체
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
