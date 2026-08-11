using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;

namespace ProjectMT.Features.Expedition
{
    public static class ExpeditionRepeatClearRewardRules // 원정대 반복 클리어 보상
    {
        public const long Gold = 5L;
        public const long CommanderExperience = 25L; // 시드: 최초 보상의 25%
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
