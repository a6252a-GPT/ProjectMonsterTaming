using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Features.WorldDrops
{
    [Serializable]
    public sealed class WorldItemDropVisualEntry // 아이템 ID별 Project 자산 외형 매핑
    {
        [SerializeField] private string itemId;
        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField] private Vector3 localScale = Vector3.one;

        public string ItemId => itemId ?? string.Empty;
        public GameObject ModelPrefab => modelPrefab;
        public Vector3 LocalPosition => localPosition;
        public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);
        public Vector3 LocalScale => localScale;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                error = "World item drop visual has a blank Item ID.";
                return false;
            }

            if (modelPrefab == null)
            {
                error = $"World item drop visual is missing its model. Item={itemId}";
                return false;
            }

            if (localScale.x <= 0f || localScale.y <= 0f || localScale.z <= 0f)
            {
                error = $"World item drop visual scale must be positive. Item={itemId}";
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            GameObject prefab,
            Vector3 position,
            Vector3 eulerAngles,
            Vector3 scale)
        {
            itemId = id?.Trim();
            modelPrefab = prefab;
            localPosition = position;
            localEulerAngles = eulerAngles;
            localScale = scale;
        }
#endif
    }

    [CreateAssetMenu(
        menuName = "ProjectMT/World Drops/Item Visual Catalog",
        fileName = "WorldItemDropVisualCatalog")]
    public sealed class WorldItemDropVisualCatalog : ScriptableObject // 일반 아이템 드랍 외형 등록부
    {
        [SerializeField] private List<WorldItemDropVisualEntry> entries = new List<WorldItemDropVisualEntry>();
        [NonSerialized] private Dictionary<string, WorldItemDropVisualEntry> lookup;

        public IReadOnlyList<WorldItemDropVisualEntry> Entries => entries;

        private void OnEnable()
        {
            RebuildLookup();
        }

        public bool TryResolve(string itemId, out WorldItemDropVisualEntry entry)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                entry = null;
                return false;
            }

            lookup ??= BuildLookup();
            return lookup.TryGetValue(itemId.Trim(), out entry);
        }

        public bool TryValidate(out string error)
        {
            entries ??= new List<WorldItemDropVisualEntry>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null)
                {
                    error = $"World item drop visual entry is missing. Index={index}";
                    return false;
                }

                if (!entry.TryValidate(out error))
                {
                    return false;
                }

                if (!ids.Add(entry.ItemId))
                {
                    error = $"World item drop visual ID is duplicated. Item={entry.ItemId}";
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

        private Dictionary<string, WorldItemDropVisualEntry> BuildLookup()
        {
            entries ??= new List<WorldItemDropVisualEntry>();
            var result = new Dictionary<string, WorldItemDropVisualEntry>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.ItemId) &&
                    !result.ContainsKey(entry.ItemId))
                {
                    result.Add(entry.ItemId, entry);
                }
            }

            return result;
        }

#if UNITY_EDITOR
        public void EditorSetEntries(IEnumerable<WorldItemDropVisualEntry> values)
        {
            entries = values == null
                ? new List<WorldItemDropVisualEntry>()
                : new List<WorldItemDropVisualEntry>(values);
            RebuildLookup();
        }
#endif
    }
}
