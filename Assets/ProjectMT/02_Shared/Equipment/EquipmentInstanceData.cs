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
        [SerializeField] private string instanceId;
        [SerializeField] private EquipmentPart part;
        [SerializeField] private EquipmentGrade grade;
        [SerializeField] private List<EquipmentOptionRollData> randomOptions = new List<EquipmentOptionRollData>();

        public EquipmentInstanceData(
            string instanceId,
            EquipmentPart part,
            EquipmentGrade grade,
            List<EquipmentOptionRollData> randomOptions)
        {
            this.instanceId = instanceId;
            this.part = part;
            this.grade = grade;
            this.randomOptions = randomOptions ?? new List<EquipmentOptionRollData>();
        }

        public string InstanceId => instanceId;
        public EquipmentPart Part => part;
        public EquipmentGrade Grade => grade;
        public IReadOnlyList<EquipmentOptionRollData> RandomOptions => randomOptions;

        public EquipmentInstanceData Clone()
        {
            var clonedOptions = new List<EquipmentOptionRollData>(randomOptions.Count);
            for (var i = 0; i < randomOptions.Count; i++)
            {
                if (randomOptions[i] != null)
                {
                    clonedOptions.Add(randomOptions[i].Clone());
                }
            }

            return new EquipmentInstanceData(instanceId, part, grade, clonedOptions);
        }

        internal bool Repair()
        {
            randomOptions ??= new List<EquipmentOptionRollData>();
            randomOptions.RemoveAll(option => option == null);
            return !string.IsNullOrEmpty(instanceId);
        }
    }
}
