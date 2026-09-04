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
        [SerializeField] private int itemLevel; // 구형 누락값은 버전 이관에서만 보완
        [SerializeField] private List<EquipmentOptionRollData> randomOptions = new List<EquipmentOptionRollData>();
        [SerializeField] private bool isLocked;

        public EquipmentInstanceData(
            string instanceId,
            EquipmentPart part,
            EquipmentGrade grade,
            int itemLevel,
            List<EquipmentOptionRollData> randomOptions,
            bool isLocked = false)
        {
            this.instanceId = instanceId;
            this.part = part;
            this.grade = grade;
            this.itemLevel = itemLevel;
            this.randomOptions = randomOptions ?? new List<EquipmentOptionRollData>();
            this.isLocked = isLocked;
        }

        // 구형 테스트/도구 호출 호환: 레벨을 생략한 장비는 기본 레벨 1로 만든다.
        public EquipmentInstanceData(
            string instanceId,
            EquipmentPart part,
            EquipmentGrade grade,
            List<EquipmentOptionRollData> randomOptions)
            : this(instanceId, part, grade, 1, randomOptions)
        {
        }

        public string InstanceId => instanceId;
        public EquipmentPart Part => part;
        public EquipmentGrade Grade => grade;
        public int ItemLevel => itemLevel;
        public IReadOnlyList<EquipmentOptionRollData> RandomOptions => randomOptions;
        public bool IsLocked => isLocked;

        public EquipmentInstanceData Clone()
        {
            var sourceOptions = randomOptions ?? new List<EquipmentOptionRollData>();
            var clonedOptions = new List<EquipmentOptionRollData>(sourceOptions.Count);
            for (var i = 0; i < sourceOptions.Count; i++)
            {
                clonedOptions.Add(sourceOptions[i]?.Clone());
            }

            return new EquipmentInstanceData(instanceId, part, grade, itemLevel, clonedOptions, isLocked);
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
            if (itemLevel < 1 || string.IsNullOrEmpty(instanceId) ||
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

        internal void MigrateLegacyLevel()
        {
            if (itemLevel == 0) itemLevel = 1; // 옵션 확정값은 보존
        }

        internal bool IsValidForAcquire(int maximumItemLevel)
        {
            if (randomOptions != null)
            {
                if (randomOptions.Count > MaxRandomOptionCount) return false;
                foreach (var option in randomOptions)
                    if (option == null || !option.Repair()) return false; // 신규 손상값을 보정으로 숨기지 않음
            }
            if (itemLevel < 1 || itemLevel > maximumItemLevel || !Repair())
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
