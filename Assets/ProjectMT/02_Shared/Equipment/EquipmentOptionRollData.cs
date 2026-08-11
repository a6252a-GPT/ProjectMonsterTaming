using System;
using UnityEngine;

namespace ProjectMT.Shared.Equipment
{
    // 08.10 안건준 추가 - 장비 하나에 붙은 랜덤 추가 옵션 1건(종류 + 확정값)을 저장한다.
    // 문서 규칙: 옵션은 장비 획득 시 확정되고, 재접속해도 다시 추첨하지 않으므로 그대로 저장한다.
    [Serializable]
    public sealed class EquipmentOptionRollData
    {
        [SerializeField] private EquipmentOptionType type;
        [SerializeField] private float value;

        public EquipmentOptionRollData(EquipmentOptionType type, float value)
        {
            this.type = type;
            this.value = value;
        }

        public EquipmentOptionType Type => type;
        public float Value => value;

        public EquipmentOptionRollData Clone()
        {
            return new EquipmentOptionRollData(type, value);
        }

        internal bool Repair()
        {
            return Enum.IsDefined(typeof(EquipmentOptionType), type) &&
                   !float.IsNaN(value) &&
                   !float.IsInfinity(value) &&
                   value > 0f;
        }
    }
}
