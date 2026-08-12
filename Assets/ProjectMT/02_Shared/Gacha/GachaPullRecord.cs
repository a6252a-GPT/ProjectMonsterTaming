using ProjectMT.Shared.Unit;

namespace ProjectMT.Shared.Gacha
{
    public readonly struct GachaPullRecord // 저장 후보에 넣을 한 번의 소환 결과
    {
        public GachaPullRecord(string monsterId, MonsterRarity rarity)
        {
            MonsterId = monsterId?.Trim();
            Rarity = rarity;
        }

        public string MonsterId { get; }
        public MonsterRarity Rarity { get; }
    }
}
