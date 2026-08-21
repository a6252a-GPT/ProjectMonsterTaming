using UnityEngine;

namespace ProjectMT.Shared.Equipment
{
    // 장비 슬롯 강화 비용 규칙(골드·강화석). 레벨은 0부터 시작한다.
    public static class EquipmentSlotUpgradeCostRules
    {
        private const float GoldCostBase = 100f;
        private const float GoldCostGrowth = 1.12f;
        private const int LevelsPerStoneTier = 10;

        // 다음 강화(level → level+1)에 필요한 골드.
        public static long GetNextGoldCost(int level)
        {
            var raw = GoldCostBase * Mathf.Pow(GoldCostGrowth, Mathf.Max(0, level));
            return (long)Mathf.Round(raw);
        }

        // 다음 강화에 필요한 장비 슬롯 강화석 개수.
        public static int GetNextStoneCost(int level)
        {
            return 1 + Mathf.Max(0, level) / LevelsPerStoneTier;
        }
    }
}
