using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;
using UnityEngine;

namespace ProjectMT.Features.WorldDrops
{
    [Serializable]
    public sealed class EquipmentDropChestVisualEntry // 장비 등급별 상자 외형 예약값
    {
        [SerializeField] private EquipmentGrade grade;
        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField] private Vector3 localScale = Vector3.one;

        public EquipmentGrade Grade => grade;
        public GameObject ModelPrefab => modelPrefab;
        public Vector3 LocalPosition => localPosition;
        public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);
        public Vector3 LocalScale => localScale;

        public bool TryValidate(out string error)
        {
            if (!Enum.IsDefined(typeof(EquipmentGrade), grade))
            {
                error = $"Equipment drop chest grade is invalid. Grade={grade}";
                return false;
            }

            if (modelPrefab == null)
            {
                error = $"Equipment drop chest is missing its model. Grade={grade}";
                return false;
            }

            if (localScale.x <= 0f || localScale.y <= 0f || localScale.z <= 0f)
            {
                error = $"Equipment drop chest scale must be positive. Grade={grade}";
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            EquipmentGrade equipmentGrade,
            GameObject prefab,
            Vector3 position,
            Vector3 eulerAngles,
            Vector3 scale)
        {
            grade = equipmentGrade;
            modelPrefab = prefab;
            localPosition = position;
            localEulerAngles = eulerAngles;
            localScale = scale;
        }
#endif
    }

    [CreateAssetMenu(
        menuName = "ProjectMT/World Drops/Equipment Chest Visual Catalog",
        fileName = "EquipmentDropChestVisualCatalog")]
    public sealed class EquipmentDropChestVisualCatalog : ScriptableObject // 장비 로직과 분리된 등급별 상자 외형표
    {
        [SerializeField] private List<EquipmentDropChestVisualEntry> entries =
            new List<EquipmentDropChestVisualEntry>();
        [NonSerialized] private Dictionary<EquipmentGrade, EquipmentDropChestVisualEntry> lookup;

        public IReadOnlyList<EquipmentDropChestVisualEntry> Entries => entries;

        private void OnEnable()
        {
            RebuildLookup();
        }

        public bool TryResolve(EquipmentGrade grade, out EquipmentDropChestVisualEntry entry)
        {
            lookup ??= BuildLookup();
            return lookup.TryGetValue(grade, out entry);
        }

        public bool TryValidate(out string error)
        {
            entries ??= new List<EquipmentDropChestVisualEntry>();
            var grades = new HashSet<EquipmentGrade>();
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null)
                {
                    error = $"Equipment drop chest entry is missing. Index={index}";
                    return false;
                }

                if (!entry.TryValidate(out error))
                {
                    return false;
                }

                if (!grades.Add(entry.Grade))
                {
                    error = $"Equipment drop chest grade is duplicated. Grade={entry.Grade}";
                    return false;
                }
            }

            foreach (EquipmentGrade grade in Enum.GetValues(typeof(EquipmentGrade)))
            {
                if (!grades.Contains(grade))
                {
                    error = $"Equipment drop chest grade is missing. Grade={grade}";
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

        private Dictionary<EquipmentGrade, EquipmentDropChestVisualEntry> BuildLookup()
        {
            entries ??= new List<EquipmentDropChestVisualEntry>();
            var result = new Dictionary<EquipmentGrade, EquipmentDropChestVisualEntry>();
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry != null && !result.ContainsKey(entry.Grade))
                {
                    result.Add(entry.Grade, entry);
                }
            }

            return result;
        }

#if UNITY_EDITOR
        public void EditorSetEntries(IEnumerable<EquipmentDropChestVisualEntry> values)
        {
            entries = values == null
                ? new List<EquipmentDropChestVisualEntry>()
                : new List<EquipmentDropChestVisualEntry>(values);
            RebuildLookup();
        }
#endif
    }
}
