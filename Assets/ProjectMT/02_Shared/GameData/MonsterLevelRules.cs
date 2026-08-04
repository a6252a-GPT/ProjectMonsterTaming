using System;

namespace ProjectMT.Shared.GameData
{
    public static class MonsterLevelRules // 임시 몬스터 레벨 비용·능력치 규칙
    {
        public const int BaseLevelUpCost = 10;
        public const double CostGrowthMultiplier = 1.1d;
        public const float StatGrowthPerLevel = 0.01f;

        public static bool TryGetNextLevelCost(int currentLevel, out int cost)
        {
            cost = 0;
            if (currentLevel < 1 || currentLevel == int.MaxValue)
            {
                return false;
            }

            var rawCost = BaseLevelUpCost * Math.Pow(CostGrowthMultiplier, currentLevel - 1);
            if (double.IsNaN(rawCost) || double.IsInfinity(rawCost) || rawCost > int.MaxValue)
            {
                return false;
            }

            cost = Math.Max(
                BaseLevelUpCost,
                (int)Math.Round(rawCost, MidpointRounding.AwayFromZero));
            return true;
        }

        public static float GetStatMultiplier(int level)
        {
            return 1f + StatGrowthPerLevel * Math.Max(0, level - 1); // 기본 능력치 기준 선형 증가
        }
    }
}
