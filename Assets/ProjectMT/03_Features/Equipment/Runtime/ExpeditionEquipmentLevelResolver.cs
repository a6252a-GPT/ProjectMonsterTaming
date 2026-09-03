using System;
using ProjectMT.Shared.GameData;

namespace ProjectMT.Features.Equipment
{
    // 장비 획득 레벨의 원정대 진행 기준을 한 곳에서 계산한다.
    public static class ExpeditionEquipmentLevelResolver
    {
        public const int StagesPerDifficulty = 100;

        public static int ResolveRunStage(ExpeditionDifficulty difficulty, int stage)
        {
            if (!Enum.IsDefined(typeof(ExpeditionDifficulty), difficulty))
            {
                throw new ArgumentOutOfRangeException(nameof(difficulty));
            }

            if (stage < 1 || stage > StagesPerDifficulty)
            {
                throw new ArgumentOutOfRangeException(nameof(stage));
            }

            return difficulty == ExpeditionDifficulty.Hard
                ? StagesPerDifficulty + stage
                : stage;
        }

        // UI에서 선택한 난이도와 무관하게 실제로 가장 멀리 진행한 축을 쓴다.
        public static int ResolveHighestClearedStage(GameProgressView progress)
        {
            return progress.HardLastClearedStage > 0
                ? StagesPerDifficulty + progress.HardLastClearedStage
                : Math.Max(1, progress.NormalLastClearedStage);
        }
    }
}
