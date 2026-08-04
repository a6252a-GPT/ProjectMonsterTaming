using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Catalog", fileName = "MonsterCatalog")]
    public sealed class MonsterCatalog : ScriptableObject // 몬스터 Definition 등록부
    {
        [SerializeField] private List<MonsterDefinition> definitions = new List<MonsterDefinition>();

        public IReadOnlyList<MonsterDefinition> Definitions => definitions;

        public bool TryGet(string monsterId, out MonsterDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(monsterId))
            {
                for (var index = 0; index < definitions.Count; index++)
                {
                    var candidate = definitions[index];
                    if (candidate != null && string.Equals(
                            candidate.MonsterId,
                            monsterId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        definition = candidate;
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }

        public bool TryValidate(out string error)
        {
            if (definitions == null || definitions.Count == 0)
            {
                error = "Monster Catalog is empty.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (definition == null)
                {
                    error = $"Monster Catalog has a missing definition. Index={index}";
                    return false;
                }

                if (!definition.TryValidate(out error))
                {
                    return false;
                }

                if (!ids.Add(definition.MonsterId))
                {
                    error = $"Monster ID is duplicated. Monster={definition.MonsterId}";
                    return false;
                }
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorSetDefinitions(IEnumerable<MonsterDefinition> values)
        {
            definitions = values == null
                ? new List<MonsterDefinition>()
                : new List<MonsterDefinition>(values);
        }
#endif
    }
}
