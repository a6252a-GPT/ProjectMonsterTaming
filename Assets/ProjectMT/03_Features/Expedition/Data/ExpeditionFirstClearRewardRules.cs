using System;
using System.Collections.Generic;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;

namespace ProjectMT.Features.Expedition
{
    public static class ExpeditionFirstClearRewardRules // 원정대 최초 클리어 보상
    {
        private const double GoldGrowthRate = 1.05d;
        private const int MilestoneInterval = 10;
        private const int FinalMilestoneStage = 40;
        private const long MilestoneSummonTicket = 10L;

        public const long Gold = 110L; // 1단계 호환 상수
        public const long CommanderExperience = 16L; // 1단계 호환 상수
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
            var experience = 15L + stage;
            var items = new List<ItemAmount>
            {
                new ItemAmount(ItemIds.EquipmentSlotUpgradeStone, EquipmentSlotUpgradeStone)
            };
            if (stage <= FinalMilestoneStage && stage % MilestoneInterval == 0)
            {
                items.Add(new ItemAmount(ItemIds.MonsterSummonTicket, MilestoneSummonTicket));
            }

            return new RewardBundle(gold, experience, items);
        }
    }
}
