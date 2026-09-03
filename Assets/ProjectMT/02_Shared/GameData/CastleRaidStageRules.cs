using System;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;

namespace ProjectMT.Shared.GameData
{
    public static class CastleRaidStageRules // 군단의 역습 1~100단계 진행·보상 단일 기준
    {
        public const int MinimumStage = 1;
        public const int MaximumStage = 100;
        public const int StagesPerDifficulty = 10;
        public const int MaximumDifficulty = 10;
        public const long StageOneDiamondReward = 300L;
        public const long DiamondGrowthPerStage = StageOneDiamondReward / 2L;
        public const long MonsterSummonTicketReward = 10L;
        public const int StageOneGenerationSeed = 10801;

        public static bool IsValidStage(int stage)
        {
            return stage >= MinimumStage && stage <= MaximumStage;
        }

        public static int ResolveDifficulty(int stage)
        {
            return Math.Clamp(((Math.Clamp(stage, MinimumStage, MaximumStage) - 1) /
                               StagesPerDifficulty) + 1, 1, MaximumDifficulty);
        }

        public static int ResolveFirstStage(int difficulty)
        {
            return (Math.Clamp(difficulty, 1, MaximumDifficulty) - 1) * StagesPerDifficulty + 1;
        }

        public static int ResolveLastStage(int difficulty)
        {
            return Math.Min(MaximumStage, ResolveFirstStage(difficulty) + StagesPerDifficulty - 1);
        }

        public static int ResolveNextChallengeStage(int highestClearedStage)
        {
            return Math.Clamp(highestClearedStage + 1, MinimumStage, MaximumStage);
        }

        public static int ResolveMaximumSelectableStage(int highestClearedStage)
        {
            return ResolveNextChallengeStage(highestClearedStage);
        }

        public static bool HasChallengeStage(int highestClearedStage)
        {
            return highestClearedStage < MaximumStage;
        }

        public static bool IsSelectable(int stage, int highestClearedStage)
        {
            return IsValidStage(stage) && stage <= ResolveMaximumSelectableStage(highestClearedStage);
        }

        public static bool IsNewClear(int stage, int highestClearedStage)
        {
            return IsValidStage(stage) && stage == highestClearedStage + 1;
        }

        public static long ResolveDiamondReward(int stage)
        {
            var validStage = Math.Clamp(stage, MinimumStage, MaximumStage);
            return StageOneDiamondReward +
                   (validStage - MinimumStage) * DiamondGrowthPerStage;
        }

        public static RewardBundle CreateFirstClearReward(int stage)
        {
            if (!IsValidStage(stage))
            {
                return RewardBundle.Empty;
            }

            return RewardBundle.FromItems(
                new ItemAmount(ItemIds.Diamond, ResolveDiamondReward(stage)),
                new ItemAmount(ItemIds.MonsterSummonTicket, ResolveMonsterSummonTicketReward(stage)));
        }

        public static long ResolveMonsterSummonTicketReward(int stage)
        {
            return MonsterSummonTicketReward;
        }

        public static int ResolveGenerationSeed(int stage)
        {
            var validStage = Math.Clamp(stage, MinimumStage, MaximumStage);
            return unchecked(StageOneGenerationSeed + (validStage - 1) * 7919);
        }
    }
}
