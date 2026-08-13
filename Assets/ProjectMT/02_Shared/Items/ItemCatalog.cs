using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.Items
{
    [CreateAssetMenu(menuName = "ProjectMT/Items/Item Catalog", fileName = "ItemCatalog")]
    public sealed class ItemCatalog : ScriptableObject // 일반 아이템 Definition 등록부
    {
        [SerializeField] private List<ItemDefinition> definitions = new List<ItemDefinition>();
        [NonSerialized] private Dictionary<string, ItemDefinition> lookup;

        public IReadOnlyList<ItemDefinition> Definitions => definitions;

        private void OnEnable()
        {
            RebuildLookup();
        }

        public bool TryGet(string itemId, out ItemDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                definition = null;
                return false;
            }

            lookup ??= BuildLookup();
            return lookup.TryGetValue(itemId.Trim(), out definition);
        }

        public bool TryValidate(out string error)
        {
            definitions ??= new List<ItemDefinition>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (definition == null)
                {
                    error = $"Item Catalog has a missing definition. Index={index}";
                    return false;
                }

                if (!definition.TryValidate(out error))
                {
                    return false;
                }

                if (!ids.Add(definition.ItemId))
                {
                    error = $"Item ID is duplicated. Item={definition.ItemId}";
                    return false;
                }
            }

            error = null;
            return true;
        }

        public bool TryValidateRuntimeCatalog(out string error)
        {
            if (!TryValidate(out error))
            {
                return false;
            }

            for (var index = 0; index < ItemIds.RequiredCatalogIds.Count; index++)
            {
                var itemId = ItemIds.RequiredCatalogIds[index];
                if (!TryGet(itemId, out var definition))
                {
                    error = $"Required item is missing from Item Catalog. Item={itemId}";
                    return false;
                }

                if (!ItemIds.TryGetRequiredCategory(itemId, out var expectedCategory) ||
                    definition.Category != expectedCategory)
                {
                    error = $"Required item category is invalid. Item={itemId}, " +
                            $"Expected={expectedCategory}, Actual={definition.Category}";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private void RebuildLookup()
        {
            lookup = BuildLookup();
        }

        private Dictionary<string, ItemDefinition> BuildLookup()
        {
            definitions ??= new List<ItemDefinition>();
            var result = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (definition != null && !string.IsNullOrWhiteSpace(definition.ItemId) &&
                    !result.ContainsKey(definition.ItemId))
                {
                    result.Add(definition.ItemId, definition);
                }
            }

            return result;
        }

#if UNITY_EDITOR
        public void EditorSetDefinitions(IEnumerable<ItemDefinition> values)
        {
            definitions = values == null
                ? new List<ItemDefinition>()
                : new List<ItemDefinition>(values);
            RebuildLookup();
        }
#endif
    }
}
