using System;
using UnityEngine;

namespace ProjectMT.Shared.Equipment
{
    // 장비 부위별 영구 강화 레벨 저장 데이터. 레벨은 0부터 시작한다(0 = 미강화).
    [Serializable]
    public sealed class EquipmentSlotUpgradeData
    {
        private const int PartCount = 6; // EquipmentPart 값 개수와 일치

        [SerializeField] private int[] levels = new int[PartCount]; // 인덱스 = (int)EquipmentPart

        public static EquipmentSlotUpgradeData CreateDefault()
        {
            var data = new EquipmentSlotUpgradeData();
            data.Repair();
            return data;
        }

        public EquipmentSlotUpgradeData Clone()
        {
            var clone = new EquipmentSlotUpgradeData { levels = new int[PartCount] };
            Array.Copy(levels, clone.levels, PartCount);
            return clone;
        }

        internal void Repair()
        {
            if (levels == null || levels.Length != PartCount)
            {
                var resized = new int[PartCount];
                if (levels != null)
                {
                    Array.Copy(levels, resized, Math.Min(levels.Length, PartCount));
                }

                levels = resized;
            }

            for (var i = 0; i < PartCount; i++)
            {
                if (levels[i] < 0)
                {
                    levels[i] = 0;
                }
            }
        }

        // 현재 레벨 조회.
        internal int GetLevel(EquipmentPart part)
        {
            var index = (int)part;
            return index >= 0 && index < levels.Length ? levels[index] : 0;
        }

        // CAS(compare-and-set) 방식으로 레벨을 +1 한다. expectedLevel이 현재 값과 다르면 실패.
        internal bool TryLevelUp(EquipmentPart part, int expectedLevel)
        {
            var index = (int)part;
            if (index < 0 || index >= levels.Length || levels[index] != expectedLevel)
            {
                return false;
            }

            levels[index] = expectedLevel + 1;
            return true;
        }

        internal EquipmentSlotUpgradeView CreateView() => new EquipmentSlotUpgradeView(this);

        internal int[] Levels => levels;
    }

    // 외부(UI·능력치 계산)에 전달할 읽기 전용 슬롯 강화 레벨 복사값.
    public readonly struct EquipmentSlotUpgradeView
    {
        private readonly int[] levels;

        internal EquipmentSlotUpgradeView(EquipmentSlotUpgradeData data)
        {
            var source = data?.Levels;
            if (source == null)
            {
                levels = Array.Empty<int>();
                return;
            }

            levels = new int[source.Length];
            Array.Copy(source, levels, source.Length);
        }

        public int GetLevel(EquipmentPart part)
        {
            var index = (int)part;
            return levels != null && index >= 0 && index < levels.Length ? levels[index] : 0;
        }

        // 부위별 레벨을 모두 더한 값. TotalText("LV : N") 표시에 사용한다.
        public int TotalLevel
        {
            get
            {
                if (levels == null)
                {
                    return 0;
                }

                var total = 0;
                for (var i = 0; i < levels.Length; i++)
                {
                    total += levels[i];
                }

                return total;
            }
        }
    }
}
