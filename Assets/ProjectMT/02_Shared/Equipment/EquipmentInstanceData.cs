using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.Equipment
{
    // 08.10 안건준 추가 - 보유 장비 1개(고유 인스턴스). 문서 규칙: 같은 부위+등급이라도 랜덤 옵션이
    // 서로 다를 수 있어 더 이상 "부위+등급"으로 중첩(스택)하지 않고, 아이템마다 고유 ID로 구분한다.
    [Serializable]
    public sealed class EquipmentInstanceData
    {
        public const int MaxRandomOptionCount = 4;

        [SerializeField] private string instanceId;
        [SerializeField] private EquipmentPart part;
        [SerializeField] private EquipmentGrade grade;
        [SerializeField] private List<EquipmentOptionRollData> randomOptions = new List<EquipmentOptionRollData>();
        [SerializeField] private bool isLocked;

        public EquipmentInstanceData(
            string instanceId,
            EquipmentPart part,
            EquipmentGrade grade,
            List<EquipmentOptionRollData> randomOptions,
            bool isLocked = false)
        {
            this.instanceId = instanceId;
            this.part = part;
            this.grade = grade;
            this.randomOptions = randomOptions ?? new List<EquipmentOptionRollData>();
            this.isLocked = isLocked;
        }

        public string InstanceId => instanceId;
        public EquipmentPart Part => part;
        public EquipmentGrade Grade => grade;
        public IReadOnlyList<EquipmentOptionRollData> RandomOptions => randomOptions;
        public bool IsLocked => isLocked;

        public EquipmentInstanceData Clone()
        {
            var sourceOptions = randomOptions ?? new List<EquipmentOptionRollData>();
            var clonedOptions = new List<EquipmentOptionRollData>(sourceOptions.Count);
            for (var i = 0; i < sourceOptions.Count; i++)
            {
                if (sourceOptions[i] != null)
                {
                    clonedOptions.Add(sourceOptions[i].Clone());
                }
            }

            return new EquipmentInstanceData(instanceId, part, grade, clonedOptions, isLocked);
        }

        internal bool TrySetLocked(bool expectedValue, bool nextValue)
        {
            if (isLocked != expectedValue)
            {
                return false;
            }

            isLocked = nextValue;
            return true;
        }

        internal bool Repair()
        {
            instanceId = instanceId?.Trim();
            if (string.IsNullOrEmpty(instanceId) ||
                !Enum.IsDefined(typeof(EquipmentPart), part) ||
                !Enum.IsDefined(typeof(EquipmentGrade), grade))
            {
                return false;
            }

            randomOptions ??= new List<EquipmentOptionRollData>();
            randomOptions.RemoveAll(option => option == null || !option.Repair());
            if (randomOptions.Count > MaxRandomOptionCount)
            {
                randomOptions.RemoveRange(MaxRandomOptionCount, randomOptions.Count - MaxRandomOptionCount);
            }

            return true;
        }

        // 기존 저장의 중복 옵션은 유지하되 신규 획득 데이터에는 허용하지 않는다.
        internal bool IsValidForAcquire()
        {
            if (!Repair())
            {
                return false;
            }

            var optionTypes = new HashSet<EquipmentOptionType>();
            for (var i = 0; i < randomOptions.Count; i++)
            {
                if (!optionTypes.Add(randomOptions[i].Type))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
