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
        // 반복 퀘스트 템플릿은 선형 체인이 아니라 퀘스트 풀에서 뽑히므로 여기서 제외한다.
        // 시작 퀘스트 자체가 미사용이면, 그 퀘스트가 이미 끝난 것처럼 취급하고 체인을 계속 이어서 찾는다.
        public bool TryGetFirst(QuestType type, out QuestDefinition definition)
        {
            for (var i = 0; i < definitions.Count; i++)
            {
                var candidate = definitions[i];
                if (candidate != null && candidate.QuestType == type && !candidate.HasPrerequisite &&
                    !candidate.IsRepeatingTemplate)
                {
                    if (candidate.IsEnabled)
                    {
                        definition = candidate;
                        return true;
                    }

                    return TryGetNext(candidate.QuestId, out definition);
                }
            }

            definition = null;
            return false;
        }

        // completedQuestId를 선행 퀘스트로 요구하는 다음 퀘스트(메인 퀘스트가 한 줄로 이어질 때 사용).
        // 찾은 퀘스트가 미사용이면 화면에 보여주지 않고, 그 퀘스트의 ID를 새 완료 기준 삼아 계속 다음을 찾는다
        // (미사용 퀘스트가 체인 중간에 있어도 앞뒤 퀘스트가 자연스럽게 이어진다).
        public bool TryGetNext(QuestId completedQuestId, out QuestDefinition definition)
        {
            var anchor = completedQuestId;
            while (true)
            {
                QuestDefinition candidate = null;
                for (var i = 0; i < definitions.Count; i++)
                {
                    var current = definitions[i];
                    if (current != null && !current.IsRepeatingTemplate && current.HasPrerequisite &&
                        current.PrerequisiteQuestId == anchor)
                    {
                        candidate = current;
                        break;
                    }
                }

                if (candidate == null)
                {
                    definition = null;
                    return false;
                }

                if (candidate.IsEnabled)
                {
                    definition = candidate;
                    return true;
                }

                anchor = candidate.QuestId;
            }
        }

        // 반복 퀘스트 풀 대상 목록(QuestType까지 일치). 등록 순서를 그대로 유지해서 호출부가 필터링하기 쉽게 한다.
        // 미사용으로 꺼둔 템플릿은 풀에서 아예 제외한다.
        public IEnumerable<QuestDefinition> GetRepeatingTemplates(QuestType type)
        {
            for (var i = 0; i < definitions.Count; i++)
            {
                var candidate = definitions[i];
                if (candidate != null && candidate.IsEnabled && candidate.IsRepeatingTemplate && candidate.QuestType == type)
                {
                    yield return candidate;
                }
            }
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
