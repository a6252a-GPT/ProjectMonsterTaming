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
            // 08.07 안건준 수정 - 난이도 스케일링(GuardiansTowerController에서 레벨당 +5%, 최대 100마리)이
            // 이 기준값을 곱해서 늘리므로 상한을 100으로 넓힌다. (기존 20 상한이면 배율을 곱해도 100에 못 미침)
            EnemyCount = Mathf.Clamp(enemyCount, 1, 100);
        }

        public BattlePartySnapshot Party { get; }
        public float DurationSeconds { get; }
        public int EnemyCount { get; }
    }

    // 08.06 안건준 추가 - 수호자의 탑 결과 묶음
    // 08.07 안건준 추가 - cleared(제한 시간 안에 적을 모두 처치)를 구분해서 전달한다.
    // 실패(전멸·시간초과)한 판까지 난이도가 계속 올라가면 테스트/플레이를 반복할수록 건물 체력이
    // 끝없이 불어나 버리므로, 난이도 상승은 "성공(cleared)"했을 때만 적용하기 위함이다.
    public sealed class GuardiansTowerResult : IContentResultData
    {
        public GuardiansTowerResult(int killCount, bool cleared)
        {
            KillCount = Mathf.Max(0, killCount);
            Cleared = cleared;
        }

        public int KillCount { get; }
        public bool Cleared { get; } // 08.07 안건준 추가
    }
}
