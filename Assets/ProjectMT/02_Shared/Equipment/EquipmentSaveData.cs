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
        [SerializeField] private OfflineAutoDismantlePolicy offlineAutoDismantlePolicy =
            OfflineAutoDismantlePolicy.Common; // 기존·신규 계정 기본값: 일반 이하

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
                equippedInstanceIds = new string[PartCount],
                offlineAutoDismantlePolicy = offlineAutoDismantlePolicy
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
            if (!OfflineAutoDismantlePolicyInfo.IsValid(offlineAutoDismantlePolicy))
            {
                offlineAutoDismantlePolicy = OfflineAutoDismantlePolicy.Common;
            }

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

        // 신규 장비 묶음은 일부 유실 없이 전량 검증·전량 추가한다.
        internal bool TryAcquire(IReadOnlyList<EquipmentInstanceData> newInstances)
        {
            if (newInstances == null || newInstances.Count == 0 ||
                instances.Count + newInstances.Count > MaxTotalQuantity)
            {
                return false;
            }

            var accepted = new List<EquipmentInstanceData>(newInstances.Count);
            var acceptedIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < newInstances.Count; i++)
            {
                var candidate = newInstances[i]?.Clone();
                if (candidate == null || !candidate.IsValidForAcquire() ||
                    FindIndex(candidate.InstanceId) >= 0 || !acceptedIds.Add(candidate.InstanceId))
                {
                    return false;
                }

                accepted.Add(candidate);
            }

            instances.AddRange(accepted);
            return true;
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

        internal bool TryEquipBatch(IReadOnlyList<string> instanceIds)
        {
            if (instanceIds == null || instanceIds.Count == 0 || instanceIds.Count > PartCount)
            {
                return false;
            }

            var nextByPart = new Dictionary<int, string>(instanceIds.Count);
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < instanceIds.Count; index++)
            {
                var instanceId = instanceIds[index]?.Trim();
                var ownedIndex = FindIndex(instanceId);
                if (ownedIndex < 0 || !uniqueIds.Add(instanceId))
                {
                    return false;
                }

                var partIndex = (int)instances[ownedIndex].Part;
                if (partIndex < 0 || partIndex >= PartCount || nextByPart.ContainsKey(partIndex))
                {
                    return false;
                }

                nextByPart.Add(partIndex, instances[ownedIndex].InstanceId);
            }

            var changed = false;
            foreach (var pair in nextByPart)
            {
                if (!string.Equals(equippedInstanceIds[pair.Key], pair.Value, StringComparison.Ordinal))
                {
                    changed = true;
                }
            }

            if (!changed)
            {
                return false;
            }

            foreach (var pair in nextByPart)
            {
                equippedInstanceIds[pair.Key] = pair.Value;
            }

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

        internal bool TrySetLocked(string instanceId, bool expectedValue, bool nextValue)
        {
            var index = FindIndex(instanceId);
            return index >= 0 && instances[index].TrySetLocked(expectedValue, nextValue);
        }

        internal bool TrySetOfflineAutoDismantlePolicy(
            OfflineAutoDismantlePolicy expected,
            OfflineAutoDismantlePolicy next)
        {
            if (!OfflineAutoDismantlePolicyInfo.IsValid(next) || offlineAutoDismantlePolicy != expected)
            {
                return false;
            }

            offlineAutoDismantlePolicy = next;
            return true;
        }

        internal void MigrateOfflineAutoDismantlePolicy()
        {
            offlineAutoDismantlePolicy = OfflineAutoDismantlePolicy.Common;
        }

        internal bool TryDismantle(IReadOnlyList<string> instanceIds, out long upgradeStoneAmount)
        {
            upgradeStoneAmount = 0L;
            if (instanceIds == null || instanceIds.Count == 0)
            {
                return false;
            }

            var indexes = new List<int>(instanceIds.Count);
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < instanceIds.Count; i++)
            {
                var instanceId = instanceIds[i]?.Trim();
                var index = FindIndex(instanceId);
                if (index < 0 || !uniqueIds.Add(instanceId) || instances[index].IsLocked || IsEquipped(instanceId))
                {
                    upgradeStoneAmount = 0L;
                    return false;
                }

                var reward = EquipmentDismantleRules.GetUpgradeStoneAmount(instances[index].Grade);
                if (reward <= 0 || upgradeStoneAmount > long.MaxValue - reward)
                {
                    upgradeStoneAmount = 0L;
                    return false;
                }

                upgradeStoneAmount += reward;
                indexes.Add(index);
            }

            indexes.Sort((left, right) => right.CompareTo(left));
            for (var i = 0; i < indexes.Count; i++)
            {
                instances.RemoveAt(indexes[i]);
            }

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

        private bool IsEquipped(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) || equippedInstanceIds == null)
            {
                return false;
            }

            for (var i = 0; i < equippedInstanceIds.Length; i++)
            {
                if (string.Equals(equippedInstanceIds[i], instanceId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        internal IReadOnlyList<EquipmentInstanceData> Instances => instances;
        internal string[] EquippedInstanceIds => equippedInstanceIds;
        internal OfflineAutoDismantlePolicy OfflineAutoDismantlePolicy => offlineAutoDismantlePolicy;
    }

    // 08.10 안건준 추가 - 외부(UI·능력치 계산)에 전달할 읽기 전용 장비 보유·장착 복사값.
    public readonly struct EquipmentSaveDataView
    {
        private readonly EquipmentInstanceData[] instances;
        private readonly string[] equippedInstanceIds;
        private readonly OfflineAutoDismantlePolicy offlineAutoDismantlePolicy;

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
            offlineAutoDismantlePolicy = data.OfflineAutoDismantlePolicy;
        }

        public IReadOnlyList<EquipmentInstanceData> Instances => instances ?? Array.Empty<EquipmentInstanceData>();
        public OfflineAutoDismantlePolicy OfflineAutoDismantlePolicy => offlineAutoDismantlePolicy;

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
