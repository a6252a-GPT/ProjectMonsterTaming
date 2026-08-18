using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectMT.Features.Quest;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Quest;
using ProjectMT.Shared.Reward;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.Quest
{
    // QuestCatalog 인스펙터를 퀘스트 전용 편집 화면으로 대체한다.
    // "새 퀘스트 추가" 버튼 하나로 에셋 생성 + 카탈로그 등록까지 한 번에 끝내고,
    // 각 퀘스트의 모든 항목(이름·설명·조건·목표·선행·보상·해금 대상)을 카탈로그 화면에서 바로 수정한다.
    [CustomEditor(typeof(QuestCatalog))]
    public sealed class QuestCatalogEditor : Editor
    {
        private const string DefaultFolder = "Assets/ProjectMT/03_Features/Quest/Data";

        private readonly struct ItemCatalogEntry
        {
            public ItemCatalogEntry(string id, string label, int sortOrder)
            {
                Id = id;
                Label = label;
                SortOrder = sortOrder;
            }

            public string Id { get; }
            public string Label { get; }
            public int SortOrder { get; }
        }

        private SerializedProperty definitionsProperty;
        private readonly Dictionary<int, bool> expandedByInstanceId = new Dictionary<int, bool>();
        private List<ItemCatalogEntry> itemCatalogEntries = new List<ItemCatalogEntry>();

        private void OnEnable()
        {
            definitionsProperty = serializedObject.FindProperty("definitions");
            RefreshItemCatalogEntries();
        }

        // 프로젝트에 등록된 ItemCatalog(들)을 찾아 "얻을 수 있는 아이템" 전체 목록을 만든다.
        // 뽑기권·강화석 등 실제 게임에서 보상으로 줄 수 있는 아이템이면 여기에 자동으로 잡힌다.
        private void RefreshItemCatalogEntries()
        {
            var entries = new List<ItemCatalogEntry>();
            var guids = AssetDatabase.FindAssets("t:ItemCatalog");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(path);
                if (catalog == null)
                {
                    continue;
                }

                foreach (var itemDefinition in catalog.Definitions)
                {
                    if (itemDefinition == null || string.IsNullOrWhiteSpace(itemDefinition.ItemId))
                    {
                        continue;
                    }

                    var label = $"{itemDefinition.DisplayName} ({itemDefinition.ItemId})";
                    entries.Add(new ItemCatalogEntry(itemDefinition.ItemId, label, itemDefinition.SortOrder));
                }
            }

            entries.Sort((a, b) =>
            {
                var order = a.SortOrder.CompareTo(b.SortOrder);
                return order != 0 ? order : string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase);
            });

            itemCatalogEntries = entries;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var catalog = (QuestCatalog)target;

            DrawToolbar(catalog);
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField($"등록된 퀘스트: {definitionsProperty.arraySize}개", EditorStyles.miniBoldLabel);
            EditorGUILayout.Space(4f);

            var moveUpIndex = -1;
            var moveDownIndex = -1;
            var removeIndex = -1;

            for (var i = 0; i < definitionsProperty.arraySize; i++)
            {
                DrawQuestElement(catalog, i, ref moveUpIndex, ref moveDownIndex, ref removeIndex);
            }

            if (moveUpIndex >= 1)
            {
                definitionsProperty.MoveArrayElement(moveUpIndex, moveUpIndex - 1);
            }

            if (moveDownIndex >= 0 && moveDownIndex < definitionsProperty.arraySize - 1)
            {
                definitionsProperty.MoveArrayElement(moveDownIndex, moveDownIndex + 1);
            }

            if (removeIndex >= 0)
            {
                HandleRemove(catalog, removeIndex);
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10f);
            if (GUILayout.Button("+ 새 퀘스트 추가", GUILayout.Height(30f)))
            {
                AddNewQuest(catalog);
            }
        }

        private void DrawToolbar(QuestCatalog catalog)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("전체 검증", GUILayout.Height(22f)))
            {
                if (catalog.TryValidate(out var error))
                {
                    Debug.Log($"[QuestCatalog] 검증 통과 · 퀘스트 {catalog.Definitions.Count}개", catalog);
                }
                else
                {
                    Debug.LogError($"[QuestCatalog] 검증 실패 · {error}", catalog);
                }
            }

            if (GUILayout.Button("에셋 저장", GUILayout.Height(22f)))
            {
                AssetDatabase.SaveAssets();
            }

            if (GUILayout.Button("아이템 목록 새로고침", GUILayout.Height(22f)))
            {
                RefreshItemCatalogEntries();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawQuestElement(
            QuestCatalog catalog,
            int index,
            ref int moveUpIndex,
            ref int moveDownIndex,
            ref int removeIndex)
        {
            var elementProperty = definitionsProperty.GetArrayElementAtIndex(index);
            var definition = elementProperty.objectReferenceValue as QuestDefinition;
            var key = elementProperty.objectReferenceInstanceIDValue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            expandedByInstanceId.TryGetValue(key, out var expanded);
            var header = definition != null
                ? $"{index + 1}. [{definition.QuestId.Value}] {definition.DisplayName}  ({QuestConditionTypeInfo.GetDisplayName(definition.ConditionType)} {definition.TargetValue})"
                : $"{index + 1}. (비어 있는 슬롯)";
            var newExpanded = EditorGUILayout.Foldout(expanded, header, true);
            if (newExpanded != expanded)
            {
                expandedByInstanceId[key] = newExpanded;
            }

            GUI.enabled = index > 0;
            if (GUILayout.Button("▲", GUILayout.Width(24f)))
            {
                moveUpIndex = index;
            }

            GUI.enabled = index < definitionsProperty.arraySize - 1;
            if (GUILayout.Button("▼", GUILayout.Width(24f)))
            {
                moveDownIndex = index;
            }

            GUI.enabled = true;
            if (GUILayout.Button("삭제", GUILayout.Width(44f)))
            {
                removeIndex = index;
            }

            EditorGUILayout.EndHorizontal();

            if (newExpanded)
            {
                if (definition == null)
                {
                    EditorGUILayout.PropertyField(elementProperty, new GUIContent("퀘스트 에셋"));
                }
                else
                {
                    EditorGUI.indentLevel++;
                    DrawQuestFields(definition, catalog);
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawQuestFields(QuestDefinition definition, QuestCatalog catalog)
        {
            var so = new SerializedObject(definition);
            so.Update();

            var idProp = so.FindProperty("questId").FindPropertyRelative("value");
            var nameProp = so.FindProperty("displayName");
            var descProp = so.FindProperty("description");
            var typeProp = so.FindProperty("questType");
            var conditionProp = so.FindProperty("conditionType");
            var targetProp = so.FindProperty("targetValue");
            var prerequisiteValueProp = so.FindProperty("prerequisiteQuestId").FindPropertyRelative("value");
            var rewardProp = so.FindProperty("reward");
            var unlockListProp = so.FindProperty("unlockTargets");

            EditorGUILayout.PropertyField(idProp, new GUIContent("퀘스트 ID"));
            EditorGUILayout.PropertyField(nameProp, new GUIContent("표시 이름"));
            EditorGUILayout.PropertyField(descProp, new GUIContent("설명"));
            DrawEnumPopupKorean<QuestType>(typeProp, "종류", QuestTypeInfo.GetDisplayName);
            DrawEnumPopupKorean<QuestConditionType>(conditionProp, "조건 종류", QuestConditionTypeInfo.GetDisplayName);
            EditorGUILayout.PropertyField(targetProp, new GUIContent("목표 수치"));

            DrawPrerequisitePopup(prerequisiteValueProp, catalog, definition);

            EditorGUILayout.PropertyField(rewardProp, new GUIContent("보상"));
            DrawRewardInline(definition, rewardProp, catalog);

            EditorGUILayout.Space(4f);
            DrawUnlockTargets(unlockListProp);

            so.ApplyModifiedProperties();

            if (!definition.TryValidate(out var error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Warning);
            }
        }

        // enum 팝업을 기본(영문 멤버명) 대신 한글 표시 이름으로 그린다. QuestType·QuestConditionType처럼
        // enumValueIndex가 Enum.GetValues() 순서와 같은 값(0, 1, 2 ...) 열거형에 사용한다.
        private static void DrawEnumPopupKorean<TEnum>(SerializedProperty enumProp, string label, Func<TEnum, string> getDisplayName)
            where TEnum : Enum
        {
            var values = (TEnum[])Enum.GetValues(typeof(TEnum));
            var labels = new string[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                labels[i] = getDisplayName(values[i]);
            }

            var currentIndex = Mathf.Clamp(enumProp.enumValueIndex, 0, values.Length - 1);
            var newIndex = EditorGUILayout.Popup(label, currentIndex, labels);
            if (newIndex != currentIndex)
            {
                enumProp.enumValueIndex = newIndex;
            }
        }

        private static void DrawPrerequisitePopup(SerializedProperty prerequisiteValueProp, QuestCatalog catalog, QuestDefinition self)
        {
            var others = catalog.Definitions.Where(d => d != null && d != self).ToList();
            var labels = new string[others.Count + 1];
            var ids = new string[others.Count + 1];
            labels[0] = "(없음 - 체인 시작)";
            ids[0] = string.Empty;
            for (var i = 0; i < others.Count; i++)
            {
                labels[i + 1] = $"[{others[i].QuestId.Value}] {others[i].DisplayName}";
                ids[i + 1] = others[i].QuestId.Value;
            }

            var currentValue = prerequisiteValueProp.stringValue ?? string.Empty;
            var currentIndex = Array.IndexOf(ids, currentValue);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var newIndex = EditorGUILayout.Popup("선행 퀘스트", currentIndex, labels);
            if (newIndex != currentIndex)
            {
                prerequisiteValueProp.stringValue = ids[newIndex];
            }
        }

        private void DrawRewardInline(QuestDefinition definition, SerializedProperty rewardProp, QuestCatalog catalog)
        {
            var reward = rewardProp.objectReferenceValue as RewardDefinition;
            EditorGUI.indentLevel++;
            if (reward == null)
            {
                if (GUILayout.Button("새 보상 에셋 생성"))
                {
                    var created = CreateRewardAsset(definition);
                    rewardProp.objectReferenceValue = created;
                }
            }
            else
            {
                if (IsRewardSharedWithOtherQuest(reward, definition, catalog))
                {
                    EditorGUILayout.HelpBox(
                        "이 보상 에셋은 다른 퀘스트와 공유되고 있습니다. 여기서 수정하면 그 퀘스트의 보상도 같이 바뀝니다.",
                        MessageType.Warning);
                    if (GUILayout.Button("이 퀘스트 전용 보상으로 분리"))
                    {
                        var cloned = CloneRewardAsset(definition, reward);
                        rewardProp.objectReferenceValue = cloned;
                        reward = cloned;
                    }
                }

                var rewardSo = new SerializedObject(reward);
                rewardSo.Update();
                EditorGUILayout.PropertyField(rewardSo.FindProperty("gold"), new GUIContent("골드"));
                EditorGUILayout.PropertyField(rewardSo.FindProperty("commanderExperience"), new GUIContent("군단장 경험치"));
                DrawRewardItemList(rewardSo.FindProperty("items"));
                rewardSo.ApplyModifiedProperties();
                if (GUI.changed)
                {
                    EditorUtility.SetDirty(reward);
                }
            }

            EditorGUI.indentLevel--;
        }

        // 같은 보상 에셋을 다른 퀘스트가 이미 쓰고 있는지 확인한다(의도치 않은 보상 공유 방지).
        private static bool IsRewardSharedWithOtherQuest(RewardDefinition reward, QuestDefinition self, QuestCatalog catalog)
        {
            if (reward == null || catalog == null)
            {
                return false;
            }

            var definitions = catalog.Definitions;
            for (var i = 0; i < definitions.Count; i++)
            {
                var other = definitions[i];
                if (other != null && other != self && other.Reward == reward)
                {
                    return true;
                }
            }

            return false;
        }

        private static RewardDefinition CloneRewardAsset(QuestDefinition definition, RewardDefinition source)
        {
            var folder = ResolveFolder(AssetDatabase.GetAssetPath(definition));
            var safeId = string.IsNullOrWhiteSpace(definition.QuestId.Value) ? "New" : definition.QuestId.Value;
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/RD_Quest_{safeId}.asset");

            var clone = ScriptableObject.CreateInstance<RewardDefinition>();
            clone.EditorConfigure(source.Gold, source.CommanderExperience, source.Items);
            AssetDatabase.CreateAsset(clone, path);
            AssetDatabase.SaveAssets();
            return clone;
        }

        // "아이템" 목록을 기본 배열 그리기 대신, 프로젝트의 모든 아이템(뽑기권·강화석 등)을
        // 드롭다운에서 고를 수 있는 전용 UI로 그린다. 목록에 없는 값이면 직접 입력도 가능하다.
        private void DrawRewardItemList(SerializedProperty itemsProp)
        {
            EditorGUILayout.LabelField("아이템");
            EditorGUI.indentLevel++;

            var removeIndex = -1;
            for (var i = 0; i < itemsProp.arraySize; i++)
            {
                var element = itemsProp.GetArrayElementAtIndex(i);
                var itemIdProp = element.FindPropertyRelative("itemId");
                var amountProp = element.FindPropertyRelative("amount");

                EditorGUILayout.BeginHorizontal();
                DrawItemIdPopup(itemIdProp);
                EditorGUILayout.LabelField("수량", GUILayout.Width(30f));
                amountProp.longValue = EditorGUILayout.LongField(amountProp.longValue, GUILayout.Width(70f));
                if (GUILayout.Button("-", GUILayout.Width(22f)))
                {
                    removeIndex = i;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (removeIndex >= 0)
            {
                itemsProp.DeleteArrayElementAtIndex(removeIndex);
            }

            if (GUILayout.Button("+ 아이템 추가", GUILayout.Width(110f)))
            {
                itemsProp.arraySize++;
                var newElement = itemsProp.GetArrayElementAtIndex(itemsProp.arraySize - 1);
                newElement.FindPropertyRelative("itemId").stringValue = itemCatalogEntries.Count > 0
                    ? itemCatalogEntries[0].Id
                    : string.Empty;
                newElement.FindPropertyRelative("amount").longValue = 1L;
            }

            EditorGUI.indentLevel--;
        }

        private void DrawItemIdPopup(SerializedProperty itemIdProp)
        {
            var currentId = itemIdProp.stringValue ?? string.Empty;
            var matchIndex = -1;
            for (var i = 0; i < itemCatalogEntries.Count; i++)
            {
                if (string.Equals(itemCatalogEntries[i].Id, currentId, StringComparison.OrdinalIgnoreCase))
                {
                    matchIndex = i;
                    break;
                }
            }

            var labels = new string[itemCatalogEntries.Count + 1];
            labels[0] = "(직접 입력)";
            for (var i = 0; i < itemCatalogEntries.Count; i++)
            {
                labels[i + 1] = itemCatalogEntries[i].Label;
            }

            var selectedIndex = matchIndex < 0 ? 0 : matchIndex + 1;
            var newIndex = EditorGUILayout.Popup(selectedIndex, labels, GUILayout.MinWidth(200f));
            if (newIndex != selectedIndex && newIndex > 0)
            {
                itemIdProp.stringValue = itemCatalogEntries[newIndex - 1].Id;
            }

            if (newIndex == 0)
            {
                itemIdProp.stringValue = EditorGUILayout.TextField(itemIdProp.stringValue, GUILayout.MinWidth(120f));
            }
        }

        private static RewardDefinition CreateRewardAsset(QuestDefinition definition)
        {
            var folder = ResolveFolder(AssetDatabase.GetAssetPath(definition));
            var safeId = string.IsNullOrWhiteSpace(definition.QuestId.Value) ? "New" : definition.QuestId.Value;
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/RD_Quest_{safeId}.asset");

            var reward = ScriptableObject.CreateInstance<RewardDefinition>();
            reward.EditorConfigure(100L, 0L, null);
            AssetDatabase.CreateAsset(reward, path);
            AssetDatabase.SaveAssets();
            return reward;
        }

        private static void DrawUnlockTargets(SerializedProperty listProp)
        {
            EditorGUILayout.LabelField("해금 대상");
            EditorGUI.indentLevel++;
            foreach (QuestUnlockTarget value in Enum.GetValues(typeof(QuestUnlockTarget)))
            {
                var existingIndex = IndexOfUnlockTarget(listProp, value);
                var has = existingIndex >= 0;
                var newHas = EditorGUILayout.ToggleLeft(QuestUnlockTargetInfo.GetDisplayName(value), has);
                if (newHas == has)
                {
                    continue;
                }

                if (newHas)
                {
                    listProp.arraySize++;
                    listProp.GetArrayElementAtIndex(listProp.arraySize - 1).enumValueIndex = (int)value;
                }
                else
                {
                    listProp.DeleteArrayElementAtIndex(existingIndex);
                }
            }

            EditorGUI.indentLevel--;
        }

        private static int IndexOfUnlockTarget(SerializedProperty listProp, QuestUnlockTarget value)
        {
            for (var i = 0; i < listProp.arraySize; i++)
            {
                if (listProp.GetArrayElementAtIndex(i).enumValueIndex == (int)value)
                {
                    return i;
                }
            }

            return -1;
        }

        private void HandleRemove(QuestCatalog catalog, int removeIndex)
        {
            var elementProperty = definitionsProperty.GetArrayElementAtIndex(removeIndex);
            var toRemove = elementProperty.objectReferenceValue as QuestDefinition;

            // 이 퀘스트만 쓰는 전용 보상이면 퀘스트와 같이 지운다. 다른 퀘스트와 공유 중이면
            // 그 퀘스트의 보상까지 같이 사라지면 안 되므로 남겨 둔다.
            var reward = toRemove != null ? toRemove.Reward : null;
            var rewardIsExclusive = reward != null && !IsRewardSharedWithOtherQuest(reward, toRemove, catalog);

            var choice = toRemove != null
                ? EditorUtility.DisplayDialogComplex(
                    "퀘스트 삭제",
                    $"'{toRemove.DisplayName}' ({toRemove.QuestId.Value})를 카탈로그에서 제거합니다.\n에셋 파일도 함께 삭제할까요?"
                        + (rewardIsExclusive ? "\n(이 퀘스트 전용 보상 에셋도 함께 삭제됩니다)" : string.Empty),
                    "카탈로그에서만 제외",
                    "취소",
                    "에셋 파일도 삭제")
                : 0;

            if (choice == 1)
            {
                return; // 취소
            }

            RemoveArrayElementCleanly(definitionsProperty, removeIndex);

            if (choice == 2 && toRemove != null)
            {
                var path = AssetDatabase.GetAssetPath(toRemove);
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.DeleteAsset(path);
                }

                if (rewardIsExclusive)
                {
                    var rewardPath = AssetDatabase.GetAssetPath(reward);
                    if (!string.IsNullOrEmpty(rewardPath))
                    {
                        AssetDatabase.DeleteAsset(rewardPath);
                    }
                }
            }

            EditorUtility.SetDirty(catalog);
        }

        private static void RemoveArrayElementCleanly(SerializedProperty arrayProperty, int index)
        {
            var element = arrayProperty.GetArrayElementAtIndex(index);
            if (element.propertyType == SerializedPropertyType.ObjectReference && element.objectReferenceValue != null)
            {
                element.objectReferenceValue = null;
            }

            arrayProperty.DeleteArrayElementAtIndex(index);
        }

        private void AddNewQuest(QuestCatalog catalog)
        {
            var folder = ResolveFolder(AssetDatabase.GetAssetPath(catalog));
            var nextNumber = catalog.Definitions.Count + 1;
            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/QD_{nextNumber:000}_New.asset");
            var previous = catalog.Definitions.Count > 0 ? catalog.Definitions[catalog.Definitions.Count - 1] : null;

            var newQuest = ScriptableObject.CreateInstance<QuestDefinition>();
            newQuest.EditorConfigure(
                new QuestId($"quest_{nextNumber:000}_new"),
                "새 퀘스트",
                string.Empty,
                QuestType.Main,
                QuestConditionType.MonsterKill,
                1L,
                previous != null ? previous.QuestId : default,
                null, // 이전 퀘스트의 보상 에셋을 그대로 물려주면 두 퀘스트가 같은 보상을 공유하게 되므로 항상 비워 둔다.
                Array.Empty<QuestUnlockTarget>());

            AssetDatabase.CreateAsset(newQuest, assetPath);

            // 퀘스트 에셋과 함께 이 퀘스트 전용 보상 에셋도 바로 만들어 둔다(다른 퀘스트와 공유되지 않는 새 파일).
            var reward = CreateRewardAsset(newQuest);
            newQuest.EditorSetReward(reward);
            EditorUtility.SetDirty(newQuest);

            AssetDatabase.SaveAssets();

            definitionsProperty.arraySize++;
            definitionsProperty.GetArrayElementAtIndex(definitionsProperty.arraySize - 1).objectReferenceValue = newQuest;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);

            expandedByInstanceId[newQuest.GetInstanceID()] = true;
            EditorGUIUtility.PingObject(newQuest);
        }

        private static string ResolveFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return DefaultFolder;
            }

            var folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            return string.IsNullOrEmpty(folder) ? DefaultFolder : folder;
        }
    }
}
