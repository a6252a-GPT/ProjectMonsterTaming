using System;
using ProjectMT.Shared.Equipment;
using UnityEngine;

namespace ProjectMT.Features.WorldDrops
{
    public readonly struct EquipmentWorldDropRequest // 고유 장비 인스턴스와 월드 표시 위치
    {
        public EquipmentWorldDropRequest(EquipmentInstanceData instance, Vector3 position)
        {
            Instance = instance?.Clone();
            Position = position;
        }

        public EquipmentInstanceData Instance { get; }
        public Vector3 Position { get; }

        public bool IsValid =>
            Instance != null &&
            !string.IsNullOrWhiteSpace(Instance.InstanceId) &&
            Enum.IsDefined(typeof(EquipmentPart), Instance.Part) &&
            Enum.IsDefined(typeof(EquipmentGrade), Instance.Grade) &&
            IsFinite(Position.x) &&
            IsFinite(Position.y) &&
            IsFinite(Position.z);

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
