using System;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;

namespace ProjectMT.Features.Expedition
{
    public static class ExpeditionRepeatClearRewardRules // 원정대 반복 클리어 보상
    {
        private const double GoldGrowthRate = 1.05d;

        public const long Gold = 25L; // 1단계 호환 상수
        public const long CommanderExperience = 4L; // 1단계 호환 상수
        public const long EquipmentSlotUpgradeStone = 1L;

        public static RewardBundle Create(int stage)
        {
            if (stage < 1)
            {
                return RewardBundle.Empty;
            }

            var gold = (long)Math.Round(
                Gold * Math.Pow(GoldGrowthRate, stage - 1),
                MidpointRounding.AwayFromZero);
            var experience = (long)Math.Ceiling((15d + stage) * 0.25d);
            return new RewardBundle(
                gold,
                experience,
                new[] { new ItemAmount(ItemIds.EquipmentSlotUpgradeStone, EquipmentSlotUpgradeStone) });
        }
    }
}
