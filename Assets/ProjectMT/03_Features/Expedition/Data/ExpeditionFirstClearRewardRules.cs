using ProjectMT.Shared.Reward;

namespace ProjectMT.Features.Expedition
{
    public static class ExpeditionFirstClearRewardRules // 원정대 최초 클리어 보상
    {
        public const long Gold = 20L;

        public static RewardBundle Create(int stage)
        {
            return stage < 1 ? RewardBundle.Empty : RewardBundle.FromGold(Gold);
        }
    }
}
