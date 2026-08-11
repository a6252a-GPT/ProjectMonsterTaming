using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;

namespace ProjectMT.Features.Expedition
{
    public static class ExpeditionFirstClearRewardRules // 원정대 최초 클리어 보상
    {
        public const long Gold = 20L;
        public const long CommanderExperience = 100L; // 시드 최초 클리어 경험치
        public const long EquipmentSlotUpgradeStone = 1L;

        public static RewardBundle Create(int stage)
        {
            return stage < 1
                ? RewardBundle.Empty
                : new RewardBundle(
                    Gold,
                    CommanderExperience,
                    new[] { new ItemAmount(ItemIds.EquipmentSlotUpgradeStone, EquipmentSlotUpgradeStone) });
        }
    }
}
