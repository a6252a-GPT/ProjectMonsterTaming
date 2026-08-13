using System;
using System.Text;
using ProjectMT.Shared.Reward;

namespace ProjectMT.Features.Expedition
{
    public static class ExpeditionResultNoticeFormatter // 원정대 결과 한 줄 안내 조합
    {
        private const int MaximumRewardCount = 3;

        public static string ChallengeVictory(int stage, RewardPresentationRequest rewards)
        {
            var builder = new StringBuilder($"원정대 {Math.Max(1, stage)}단계 도전 성공");
            if (rewards == null || rewards.IsEmpty)
            {
                return builder.ToString();
            }

            builder.Append('\n');
            var appended = 0;
            for (var index = 0; index < rewards.Items.Count && appended < MaximumRewardCount; index++)
            {
                var item = rewards.Items[index];
                if (!item.IsValid)
                {
                    continue;
                }

                if (appended > 0)
                {
                    builder.Append(" · ");
                }

                builder.Append(item.Label);
                builder.Append(" +");
                builder.Append(item.Amount.ToString("N0"));
                appended++;
            }

            return builder.ToString();
        }

        public static string ChallengeDefeat(int lastClearedStage, bool repeatModeSaved)
        {
            if (lastClearedStage <= 0)
            {
                return "도전 실패 · 원정대 1 재도전";
            }

            return repeatModeSaved
                ? $"도전 실패 · 원정대 {lastClearedStage}단계 반복사냥으로 전환"
                : "도전 실패 · 반복 전환 저장 실패";
        }
    }
}
