using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.GuardianTrial
{
    // 08.06 안건준 추가 - 수호자의 탑 시작 조건 (식량 대소동과 완전히 분리된 전용 데이터)
    public sealed class GuardiansTowerStartData : IContentStartData
    {
        public GuardiansTowerStartData(BattlePartySnapshot party, float durationSeconds, int enemyCount)
        {
            Party = party;
            DurationSeconds = Mathf.Max(1f, durationSeconds); // 최소 1초 보장
            EnemyCount = Mathf.Clamp(enemyCount, 1, 20); // 한 번만 스폰할 적 수 제한
        }

        public BattlePartySnapshot Party { get; }
        public float DurationSeconds { get; }
        public int EnemyCount { get; }
    }

    // 08.06 안건준 추가 - 수호자의 탑 결과 묶음
    public sealed class GuardiansTowerResult : IContentResultData
    {
        public GuardiansTowerResult(int killCount)
        {
            KillCount = Mathf.Max(0, killCount);
        }

        public int KillCount { get; }
    }
}
