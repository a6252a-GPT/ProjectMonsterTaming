using ProjectMT.Shared.Unit;

namespace ProjectMT.Shared.Gacha
{
    public enum MonsterGachaChannel { Normal, Soul }

    public static class MonsterSoulRules
    {
        public const long SingleCost = 100L;
        public const long TenCost = 900L;
        public static int GetOverflowReward(MonsterRarity rarity) => rarity switch
        {
            MonsterRarity.Common => 1,
            MonsterRarity.Rare => 3,
            MonsterRarity.Epic => 8,
            MonsterRarity.Legendary => 20,
            MonsterRarity.Mythic => 68,
            _ => 0
        };
    }
}
