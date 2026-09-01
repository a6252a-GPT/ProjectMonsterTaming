using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit
{
    public sealed class TreasureSpiritStartData : IContentStartData // 보물 정령 시작 조건
    {
        public TreasureSpiritStartData(BattlePartySnapshot party, float durationSeconds)
        {
            Party = party;
            DurationSeconds = Mathf.Max(1f, durationSeconds);
        }

        public BattlePartySnapshot Party { get; }
        public float DurationSeconds { get; }
    }

    public sealed class TreasureSpiritResult : IContentResultData // 보물 정령 한 판 결과
    {
        public TreasureSpiritResult(
            bool cleared,
            int killCount,
            float remainingTime,
            string message = null,
            string capturedMonsterId = null)
        {
            Cleared = cleared;
            KillCount = Mathf.Max(0, killCount);
            RemainingTime = Mathf.Max(0f, remainingTime);
            Message = message?.Trim() ?? string.Empty;
            CapturedMonsterId = capturedMonsterId?.Trim() ?? string.Empty;
        }

        public bool Cleared { get; }
        public int KillCount { get; }
        public float RemainingTime { get; }
        public string Message { get; }
        public string CapturedMonsterId { get; }
    }
}
