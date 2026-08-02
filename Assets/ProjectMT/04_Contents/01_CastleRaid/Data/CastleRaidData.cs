using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    public sealed class CastleRaidStartData : IContentStartData // 군단의 역습 시작값
    {
        public CastleRaidStartData(BattlePartySnapshot party, int deploymentLimit)
        {
            Party = party;
            DeploymentLimit = Mathf.Clamp(deploymentLimit, 1, 5); // 하단 배치 최대 5기
        }

        public BattlePartySnapshot Party { get; } // 투입 가능한 부대 사진
        public int DeploymentLimit { get; } // 이번 판 배치 한도
    }

    public sealed class CastleRaidResult : IContentResultData // 성 침공 플레이 사실
    {
        public CastleRaidResult(bool mainCastleDestroyed)
        {
            MainCastleDestroyed = mainCastleDestroyed;
        }

        public bool MainCastleDestroyed { get; } // 최종 성 파괴 여부
    }

}
