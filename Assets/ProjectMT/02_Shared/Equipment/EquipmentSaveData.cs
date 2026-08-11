using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.Equipment
{
    // 08.10 안건준 추가 - 장비 보유·장착 저장 원본. GameProgressData 안에 필드로 들어가며,
    // "저장 데이터 초기화" 디버그 기능이 GameProgressData 전체를 CreateDefault()로 덮어쓸 때
    // 이 장비 데이터도 함께 초기화된다(별도 훅이 필요 없음).
    [Serializable]
    public sealed class EquipmentSaveData
    {
        public const int MaxTotalQuantity = 100; // 인벤토리 최대 보유 수량
        private const int PartCount = 6;

        [SerializeField] private List<EquipmentInstanceData> instances = new List<EquipmentInstanceData>();
        [SerializeField] private string[] equippedInstanceIds = new string[PartCount]; // 인덱스 = (int)EquipmentPart

        public static EquipmentSaveData CreateDefault()
        {
            var data = new EquipmentSaveData();
            data.Repair();
            return data;
        }

        public EquipmentSaveData Clone()
        {
            var sourceInstances = instances ?? new List<EquipmentInstanceData>();
            var clone = new EquipmentSaveData
            {
                instances = new List<EquipmentInstanceData>(sourceInstances.Count),
                equippedInstanceIds = new string[PartCount]
            };

            for (var i = 0; i < sourceInstances.Count; i++)
            {
                if (sourceInstances[i] != null)
                {
                    clone.instances.Add(sourceInstances[i].Clone());
                }
            }

            if (equippedInstanceIds != null)
            {
                Array.Copy(equippedInstanceIds, clone.equippedInstanceIds,
                    Math.Min(equippedInstanceIds.Length, PartCount));
            }

            return clone;
        }

        internal void Repair()
        {
            instances ??= new List<EquipmentInstanceData>();
            instances.RemoveAll(instance => instance == null || !instance.Repair());

            var ownedIds = new HashSet<string>(StringComparer.Ordinal);
            instances.RemoveAll(instance => !ownedIds.Add(instance.InstanceId));

            if (instances.Count > MaxTotalQuantity)
            {
                instances.RemoveRange(MaxTotalQuantity, instances.Count - MaxTotalQuantity);
            }

            if (equippedInstanceIds == null || equippedInstanceIds.Length != PartCount)
            {
                var resized = new string[PartCount];
                if (equippedInstanceIds != null)
                {
                    Array.Copy(equippedInstanceIds, resized, Math.Min(equippedInstanceIds.Length, PartCount));
                }

                equippedInstanceIds = resized;
            }

            for (var partIndex = 0; partIndex < PartCount; partIndex++)
            {
                var instanceId = equippedInstanceIds[partIndex];
                if (string.IsNullOrEmpty(instanceId))
                {
                    equippedInstanceIds[partIndex] = string.Empty;
                    continue;
                }

                var owned = FindIndex(instanceId);
                if (owned < 0 || instances[owned].Part != (EquipmentPart)partIndex)
                {
                    equippedInstanceIds[partIndex] = string.Empty; // 보유 목록에서 사라졌거나 부위가 맞지 않으면 해제
                }
            }
        }

        // 새로 획득한 장비들을 추가한다. 최대 보유 수량을 넘는 초과분은 조용히 버린다(임시 규칙).
        internal void Acquire(List<EquipmentInstanceData> newInstances)
        {
            if (newInstances == null)
            {
                return;
            }

            for (var i = 0; i < newInstances.Count; i++)
            {
                if (instances.Count >= MaxTotalQuantity)
                {
                    break;
                }

                var candidate = newInstances[i]?.Clone();
                if (candidate != null && candidate.IsValidForAcquire() && FindIndex(candidate.InstanceId) < 0)
                {
                    instances.Add(candidate);
                }
            }
        }

        internal bool TryEquip(string instanceId)
        {
            var index = FindIndex(instanceId);
            if (index < 0)
            {
                return false;
            }

            var partIndex = (int)instances[index].Part;
            if (partIndex < 0 || partIndex >= PartCount)
            {
                return false;
            }

            equippedInstanceIds[partIndex] = instances[index].InstanceId;
            return true;
        }

        internal bool TryUnequip(EquipmentPart part)
        {
            var partIndex = (int)part;
            if (partIndex < 0 || partIndex >= PartCount)
            {
                return false;
            }

            if (string.IsNullOrEmpty(equippedInstanceIds[partIndex]))
            {
                return false;
            }

            equippedInstanceIds[partIndex] = string.Empty;
            return true;
        }

        internal EquipmentSaveDataView CreateView()
        {
            return new EquipmentSaveDataView(this);
        }

        private int FindIndex(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return -1;
            }

            for (var i = 0; i < instances.Count; i++)
            {
                if (instances[i] != null && instances[i].InstanceId == instanceId)
                {
                    return i;
                }
            }

            return -1;
        }

        internal IReadOnlyList<EquipmentInstanceData> Instances => instances;
        internal string[] EquippedInstanceIds => equippedInstanceIds;
    }

    // 08.10 안건준 추가 - 외부(UI·능력치 계산)에 전달할 읽기 전용 장비 보유·장착 복사값.
    public readonly struct EquipmentSaveDataView
    {
        private readonly EquipmentInstanceData[] instances;
        private readonly string[] equippedInstanceIds;

        internal EquipmentSaveDataView(EquipmentSaveData data)
        {
            var source = data.Instances;
            instances = new EquipmentInstanceData[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                instances[i] = source[i]?.Clone();
            }

            var ids = data.EquippedInstanceIds;
            equippedInstanceIds = new string[ids.Length];
            Array.Copy(ids, equippedInstanceIds, ids.Length);
        }

        public IReadOnlyList<EquipmentInstanceData> Instances => instances ?? Array.Empty<EquipmentInstanceData>();

        public string GetEquippedInstanceId(EquipmentPart part)
        {
            var index = (int)part;
            return equippedInstanceIds != null && index >= 0 && index < equippedInstanceIds.Length
                ? equippedInstanceIds[index]
                : string.Empty;
        }

        public bool TryGetEquipped(EquipmentPart part, out EquipmentInstanceData instance)
        {
            var instanceId = GetEquippedInstanceId(part);
            if (!string.IsNullOrEmpty(instanceId) && instances != null)
            {
                for (var i = 0; i < instances.Length; i++)
                {
                    if (instances[i] != null && instances[i].InstanceId == instanceId)
                    {
                        instance = instances[i];
                        return true;
                    }
                }
            }

            instance = null;
            return false;
        }
    }
}
