using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.VegetableRiot
{
    public sealed class VegetableRiotStartData : IContentStartData // 야채 대소동 시작 조건
    {
        public VegetableRiotStartData(BattlePartySnapshot party, float durationSeconds, int activeVegetableCount)
        {
            Party = party;
            DurationSeconds = Mathf.Max(1f, durationSeconds); // 최소 1초 보장
            ActiveVegetableCount = Mathf.Clamp(activeVegetableCount, 5, 10); // 시드 허용 개체 수 제한
        }

        public BattlePartySnapshot Party { get; }
        public float DurationSeconds { get; }
        public int ActiveVegetableCount { get; }
    }

    public sealed class VegetableRiotResult : IContentResultData // 처치 수 결과 묶음
    {
        public VegetableRiotResult(int killCount)
        {
            KillCount = Mathf.Max(0, killCount);
        }

        public int KillCount { get; }
    }

}
