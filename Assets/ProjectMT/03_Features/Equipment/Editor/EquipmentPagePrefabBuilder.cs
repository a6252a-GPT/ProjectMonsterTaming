using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProjectMT.Features.Equipment.EditorTools
{
    // PF_CommanderEquipmentPage.prefab 안에 "실제로 저장되는" UI 오브젝트를 만들어주는 에디터 도구.
    // 이 스크립트는 오직 PrefabPath(PF_CommanderEquipmentPage.prefab) 만 열고 저장하며, 다른 프리팹
    // (PF_ManagementUI, PF_CommanderGrowthPage, PF_UIStandard_PopupMedium 등)은 절대 건드리지 않는다.
    // 자동 실행([InitializeOnLoad] 등)은 쓰지 않고, 메뉴를 수동으로 눌렀을 때만 실행된다(공용 프레임에
    // 변경이 새어나가는 사고를 막기 위함).
    //
    // 메뉴 1번(UnpackAndEnsureControllerOnly) - 안전, 겹치는 UI를 새로 만들지 않음:
    //  1) 장비창 프리팹 루트가 공용 팝업 프레임(PF_UIStandard_PopupMedium)의 중첩 프리팹 인스턴스이면
    //     완전히 Unpack해서 독립된 오브젝트로 만든다(겉모습·좌표·컴포넌트는 유지). 이후 저장해도 다시는
    //     다른 프리팹(성장창 등)에 영향을 줄 수 없게 되는 핵심 안전장치다.
    //  2) EquipmentPageController 컴포넌트가 프리팹 루트에 없으면 새로 추가하고 카탈로그를 연결한다.
    //  인벤토리 스크롤뷰/PageText/SelectedItemNext 등을 유니티에서 이미 직접 만들어둔 경우 이 메뉴만
    //  실행하면 된다.
    //
    // 메뉴 2번(Build, 옛날 방식) - 위 1)2)에 더해 다음도 자동 생성한다(아직 수동 작업을 하지 않은
    // 프리팹에만 사용. 이미 수동으로 만든 프리팹에 실행하면 이름이 달라 중복 오브젝트가 생김):
    //  3) InventoryPagingBar(이전/다음 버튼 + "1 / N" 표시)를 프리팹에 실제 오브젝트로 저장.
    //  4) SelectedItemStat(기본옵션 전용, 왼쪽) / SelectedItemRandomOptionStat(추가 랜덤옵션 전용, 오른쪽)으로 텍스트 칸 분리.
    //  5) "EmptySlot_16~20" 자리에 "InventorySlot_16~20"을 새로 만들어 끼워 넣고(InventorySlot_01을
    //     복제), 기존 EmptySlot_16~20은 지우지 않고 비활성화만 한다.
    //  6) 아직 안 쓰는 "Lv.12" 같은 레벨 텍스트(Text_Level)를 프리팹 자체에서 전부 비활성화한다.
    //
    // 메뉴 위치: Tools > ProjectMT > 장비창 > (1번 또는 2번).
    // (이미 처리된 항목은 다시 실행해도 중복 생성되지 않도록 각 단계가 스스로 확인한다.)
    public static class EquipmentPagePrefabBuilder
    {
        private const string PrefabPath = "Assets/ProjectMT/03_Features/Equipment/Prefabs/PF_CommanderEquipmentPage.prefab";
        private const int ExtraSlotStart = 16;
        private const int ExtraSlotEnd = 20;

        // 겹치는 UI를 새로 만들지 않고 ① 공용 팝업 프레임과의 중첩 해제, ② EquipmentPageController
        // 부착 + 카탈로그 연결만 안전하게 처리한다. 인벤토리 UI를 이미 수동으로 만들어둔 경우 사용.
        [MenuItem("Tools/ProjectMT/장비창/1) 장비창 프리팹 독립화+컨트롤러 연결만(안전, 겹치는 UI 생성 없음)")]
        public static void UnpackAndEnsureControllerOnly()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                UnpackSharedBase(root);
                EnsureEquipmentPageController(root);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("EquipmentPagePrefabBuilder: 공유 프레임 분리 + EquipmentPageController 연결만 완료하고 저장했습니다(다른 UI 오브젝트는 만들지 않음).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // "옛날 방식"(버튼 페이지바, SelectedItemRandomOptionStat, InventorySlot_16~20) UI를 프리팹에
        // 자동으로 만들어준다. 인벤토리 스크롤뷰/PageText/SelectedItemNext를 이미 수동으로 만들어둔
        // 프리팹에는 실행하지 말 것(이름이 달라서 중복 생성됨).
        [MenuItem("Tools/ProjectMT/장비창/2) [옛날 방식] 장비창 프리팹 독립화+정리(공유프레임 분리-페이지바-옵션분리-슬롯20개-Lv숨김)")]
        public static void Build()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                UnpackSharedBase(root);
                EnsureEquipmentPageController(root);

                var equipmentContent = FindDeep(root.transform, "EquipmentContent");
                if (equipmentContent == null)
                {
                    Debug.LogError("EquipmentPagePrefabBuilder: EquipmentContent를 찾을 수 없습니다.");
                    return;
                }

                BuildPagingBar(equipmentContent);
                SplitDetailStatText(root.transform);
                ExtendInventorySlotsTo20(root.transform);
                DisableAllTextLevel(root.transform);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("EquipmentPagePrefabBuilder: 장비창 프리팹 독립화+정리(공유프레임 분리/페이지바/옵션분리/슬롯20개/Lv숨김)를 완료하고 저장했습니다.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // 장비창 프리팹 루트가 공용 팝업 프레임(PF_UIStandard_PopupMedium)의 중첩 프리팹 인스턴스이면
        // 완전히 Unpack한다. 저장 시 변경이 공용 프레임 쪽으로 새어나가 다른 프리팹(성장창 등)까지
        // 깨지는 것을 막는 핵심 안전장치다.
        private static void UnpackSharedBase(GameObject root)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(root))
            {
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Debug.Log("EquipmentPagePrefabBuilder: 공용 팝업 프레임(PF_UIStandard_PopupMedium) 중첩을 풀어 장비창을 독립시켰습니다.");
            }
        }

        // EquipmentPageController 컴포넌트가 프리팹 루트에 없으면 새로 추가하고, 카탈로그 참조가
        // 비어 있으면 프로젝트에서 하나 찾아 연결한다.
        private static void EnsureEquipmentPageController(GameObject root)
        {
            var controller = root.GetComponent<EquipmentPageController>();
            if (controller == null)
            {
                controller = root.AddComponent<EquipmentPageController>();
                Debug.Log("EquipmentPagePrefabBuilder: EquipmentPageController 컴포넌트가 프리팹에 없어서 새로 추가했습니다.");
            }

            var serialized = new SerializedObject(controller);
            var catalogProperty = serialized.FindProperty("catalog");
            if (catalogProperty != null && catalogProperty.objectReferenceValue == null)
            {
                var guids = AssetDatabase.FindAssets("t:EquipmentCatalog");
                if (guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    catalogProperty.objectReferenceValue = AssetDatabase.LoadAssetAtPath<EquipmentCatalog>(path);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        // 인벤토리 페이지 넘김 바(이전/다음 버튼 + "1 / N" 표시)를 실제 프리팹 오브젝트로 생성한다.
        private static void BuildPagingBar(Transform parent)
        {
            var existing = FindDeep(parent, "InventoryPagingBar");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var bar = new GameObject("InventoryPagingBar", typeof(RectTransform));
            bar.transform.SetParent(parent, false);
            var barRect = (RectTransform)bar.transform;
            barRect.anchorMin = new Vector2(0.5f, 0f);
            barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(273f, 118f);
            barRect.sizeDelta = new Vector2(260f, 40f);

            CreatePagingButton(bar.transform, "PrevPageButton", "<", new Vector2(-100f, 0f));
            CreatePagingButton(bar.transform, "NextPageButton", ">", new Vector2(100f, 0f));
            CreatePagingLabel(bar.transform);
        }

        private static void CreatePagingButton(Transform parent, string name, string label, Vector2 anchoredPosition)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            var rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(48f, 36f);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.45f);
            buttonObject.AddComponent<Button>();

            var textObject = new GameObject("Label", typeof(RectTransform));
            textObject.transform.SetParent(buttonObject.transform, false);
            var textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 22f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        private static void CreatePagingLabel(Transform parent)
        {
            var labelObject = new GameObject("PageLabel", typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            var rect = (RectTransform)labelObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(140f, 36f);

            var text = labelObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = 22f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = "1 / 1";
        }

        // SelectedItemStat은 왼쪽(기본옵션=핵심 능력치) 전용으로 좁혀서 재배치하고, 그 오른쪽에
        // "추가 랜덤 옵션" 전용 텍스트(SelectedItemRandomOptionStat)를 새로 만들어 나눠서 표시한다.
        private static void SplitDetailStatText(Transform root)
        {
            var coreStatTransform = FindDeep(root, "SelectedItemStat");
            if (coreStatTransform == null)
            {
                Debug.LogWarning("EquipmentPagePrefabBuilder: SelectedItemStat을 찾을 수 없어 옵션 텍스트 분리를 건너뜁니다.");
                return;
            }

            var coreRect = (RectTransform)coreStatTransform;
            coreRect.anchorMin = new Vector2(0.5f, 0.5f);
            coreRect.anchorMax = new Vector2(0.5f, 0.5f);
            coreRect.pivot = new Vector2(0.5f, 0.5f);
            coreRect.anchoredPosition = new Vector2(-190f, -25f);
            coreRect.sizeDelta = new Vector2(190f, 70f);

            var coreText = coreStatTransform.GetComponent<TMP_Text>();
            if (coreText != null)
            {
                coreText.fontSize = 18f;
                coreText.text = string.Empty;
            }

            var existingOptionText = FindDeep(root, "SelectedItemRandomOptionStat");
            if (existingOptionText != null)
            {
                Object.DestroyImmediate(existingOptionText.gameObject);
            }

            var optionObject = Object.Instantiate(coreStatTransform.gameObject, coreStatTransform.parent);
            optionObject.name = "SelectedItemRandomOptionStat";

            var optionRect = (RectTransform)optionObject.transform;
            optionRect.anchorMin = new Vector2(0.5f, 0.5f);
            optionRect.anchorMax = new Vector2(0.5f, 0.5f);
            optionRect.pivot = new Vector2(0.5f, 0.5f);
            optionRect.anchoredPosition = new Vector2(15f, -25f);
            optionRect.sizeDelta = new Vector2(220f, 85f);

            var optionText = optionObject.GetComponent<TMP_Text>();
            if (optionText != null)
            {
                optionText.fontSize = 18f;
                optionText.text = string.Empty;
            }
        }

        // 인벤토리 슬롯을 15개 -> 20개로 늘린다. "EmptySlot_16~20" 자리에 InventorySlot_01을 복제해서
        // "InventorySlot_16~20"을 새로 만들고 같은 위치/크기에 배치한 뒤, 기존 EmptySlot_16~20은
        // 지우지 않고 비활성화만 한다.
        private static void ExtendInventorySlotsTo20(Transform root)
        {
            var template = FindDeep(root, "InventorySlot_01");
            if (template == null)
            {
                Debug.LogWarning("EquipmentPagePrefabBuilder: InventorySlot_01을 찾을 수 없어 슬롯 확장을 건너뜁니다.");
                return;
            }

            for (var i = ExtraSlotStart; i <= ExtraSlotEnd; i++)
            {
                var emptySlotName = $"EmptySlot_{i}";
                var emptySlot = FindDeep(root, emptySlotName);
                if (emptySlot == null)
                {
                    Debug.LogWarning($"EquipmentPagePrefabBuilder: {emptySlotName}을 찾을 수 없습니다.");
                    continue;
                }

                var newSlotName = $"InventorySlot_{i:00}";
                var existingNewSlot = FindDeep(root, newSlotName);
                if (existingNewSlot != null)
                {
                    Object.DestroyImmediate(existingNewSlot.gameObject);
                }

                var clone = Object.Instantiate(template.gameObject, template.parent);
                clone.name = newSlotName;
                clone.SetActive(true);

                var cloneRect = (RectTransform)clone.transform;
                var emptyRect = (RectTransform)emptySlot;
                cloneRect.anchorMin = emptyRect.anchorMin;
                cloneRect.anchorMax = emptyRect.anchorMax;
                cloneRect.pivot = emptyRect.pivot;
                cloneRect.anchoredPosition = emptyRect.anchoredPosition;
                cloneRect.sizeDelta = emptyRect.sizeDelta;
                cloneRect.localScale = emptyRect.localScale;

                // 새 슬롯을 EmptySlot이 있던 형제 순서 자리로 옮겨서 하이어라키 순서도 자연스럽게 맞춘다.
                clone.transform.SetSiblingIndex(emptySlot.GetSiblingIndex());

                // 지우지 않고 비활성화만 한다.
                emptySlot.gameObject.SetActive(false);
            }
        }

        // 아직 레벨 시스템을 쓰지 않아서 목업에 미리 박혀 있던 "Lv.12" 같은 표시용 텍스트를 전부
        // 비활성화한다. 컨트롤러 코드가 런타임에도 매번 꺼주지만, 프리팹 자체에서 꺼두면 에디터에서
        // 프리팹만 열어봐도(플레이 모드가 아니어도) 안 보인다.
        private static void DisableAllTextLevel(Transform root)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].name == "Text_Level" && all[i].gameObject.activeSelf)
                {
                    all[i].gameObject.SetActive(false);
                }
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
    }
}
