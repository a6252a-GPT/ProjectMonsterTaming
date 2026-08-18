using System.Collections.Generic;
using ProjectMT.Shared.Quest;
using UnityEngine;

namespace ProjectMT.Features.Quest
{
    // 퀘스트 등록부. ContentCatalog·SceneCatalog와 동일하게 ScriptableObject 목록 + ID 조회 방식을 쓴다.
    [CreateAssetMenu(menuName = "ProjectMT/Quest/Catalog", fileName = "QuestCatalog")]
    public sealed class QuestCatalog : ScriptableObject
    {
        [SerializeField] private List<QuestDefinition> definitions = new List<QuestDefinition>();

        public IReadOnlyList<QuestDefinition> Definitions => definitions;

        public bool TryGet(QuestId questId, out QuestDefinition definition)
        {
            for (var i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] != null && definitions[i].QuestId == questId)
                {
                    definition = definitions[i];
                    return true;
                }
            }

            definition = null;
            return false;
        }

        // 특정 종류에서 선행 퀘스트가 없는 첫 퀘스트(체인 시작점). 여러 개면 등록 순서상 첫 항목을 반환한다.
        public bool TryGetFirst(QuestType type, out QuestDefinition definition)
        {
            for (var i = 0; i < definitions.Count; i++)
            {
                var candidate = definitions[i];
                if (candidate != null && candidate.QuestType == type && !candidate.HasPrerequisite)
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        // completedQuestId를 선행 퀘스트로 요구하는 다음 퀘스트(메인 퀘스트가 한 줄로 이어질 때 사용).
        public bool TryGetNext(QuestId completedQuestId, out QuestDefinition definition)
        {
            for (var i = 0; i < definitions.Count; i++)
            {
                var candidate = definitions[i];
                if (candidate != null && candidate.HasPrerequisite && candidate.PrerequisiteQuestId == completedQuestId)
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public bool TryValidate(out string error)
        {
            var seenIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                {
                    error = $"Quest catalog has an empty slot at index {i}. Asset={name}";
                    return false;
                }

                if (!definition.TryValidate(out error))
                {
                    return false;
                }

                if (!seenIds.Add(definition.QuestId.Value))
                {
                    error = $"Quest ID is duplicated: {definition.QuestId.Value}. Asset={name}";
                    return false;
                }
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorSetDefinitions(IEnumerable<QuestDefinition> values)
        {
            definitions = values == null ? new List<QuestDefinition>() : new List<QuestDefinition>(values);
        }
#endif
    }
}
