using System;
using System.Collections.Generic;
using ProjectMT.Shared.Equipment;

namespace ProjectMT.Features.WorldDrops
{
    public sealed class EquipmentDropBuffer // 저장 전 고유 장비를 Instance ID로 보존
    {
        private readonly Dictionary<string, EquipmentInstanceData> pending =
            new Dictionary<string, EquipmentInstanceData>(StringComparer.Ordinal);

        public int Count => pending.Count;
        public bool HasItems => pending.Count > 0;

        public bool TryAdd(EquipmentInstanceData instance)
        {
            var clone = instance?.Clone();
            return clone != null &&
                   !string.IsNullOrWhiteSpace(clone.InstanceId) &&
                   pending.TryAdd(clone.InstanceId, clone);
        }

        public bool TryCreateSnapshot(out List<EquipmentInstanceData> equipment)
        {
            if (pending.Count == 0)
            {
                equipment = new List<EquipmentInstanceData>();
                return false;
            }

            var ids = new List<string>(pending.Keys);
            ids.Sort(StringComparer.Ordinal);
            equipment = new List<EquipmentInstanceData>(ids.Count);
            for (var index = 0; index < ids.Count; index++)
            {
                equipment.Add(pending[ids[index]].Clone());
            }

            return true;
        }

        public bool TryCommit(IReadOnlyList<EquipmentInstanceData> snapshot)
        {
            if (snapshot == null || snapshot.Count == 0)
            {
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < snapshot.Count; index++)
            {
                var id = snapshot[index]?.InstanceId;
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id) || !pending.ContainsKey(id))
                {
                    return false;
                }
            }

            foreach (var id in ids)
            {
                pending.Remove(id);
            }

            return true;
        }
    }
}
