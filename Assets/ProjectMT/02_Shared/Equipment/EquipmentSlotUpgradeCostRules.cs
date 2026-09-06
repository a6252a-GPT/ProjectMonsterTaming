using UnityEngine;

namespace ProjectMT.Shared.Equipment
{
    // 장비 슬롯 강화는 전용 강화석만 소비한다. 레벨은 0부터 시작한다.
    public static class EquipmentSlotUpgradeCostRules
    {
        private const int LevelsPerStoneTier = 10;

        public static long GetNextGoldCost(int level) => 0L; // 기존 조회 API 호환

        // 다음 강화에 필요한 장비 슬롯 강화석 개수.
        public static int GetNextStoneCost(int level)
        {
            return 1 + Mathf.Max(0, level) / LevelsPerStoneTier;
        }
    }
}
