using ProjectMT.Shared.Reward;

namespace ProjectMT.Features.Expedition
{
    public static class ExpeditionRepeatClearRewardRules // 원정대 반복 클리어 보상
    {
        public const long Gold = 5L;

        public static RewardBundle Create(int stage)
        {
            return stage < 1 ? RewardBundle.Empty : RewardBundle.FromGold(Gold);
        }
    }
}
