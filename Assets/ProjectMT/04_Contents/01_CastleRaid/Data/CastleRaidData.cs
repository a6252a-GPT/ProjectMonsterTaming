using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    public sealed class CastleRaidStartData : IContentStartData // 군단의 역습 시작값
    {
        private const int MaxUnitSlotCount = 10; // 편성창 본부대 슬롯 수

        public CastleRaidStartData(BattlePartySnapshot party, int summonsPerSlot)
        {
            Party = party;
            SummonsPerSlot = Mathf.Clamp(summonsPerSlot, 1, 3);
            UnitSlotCount = Mathf.Min(MaxUnitSlotCount, Party?.Units.Length ?? 0);
            DeploymentLimit = UnitSlotCount * SummonsPerSlot;
        }

        public BattlePartySnapshot Party { get; } // 투입 가능한 부대 사진
        public int UnitSlotCount { get; } // 본부대와 연결된 하단 슬롯 수
        public int SummonsPerSlot { get; } // 슬롯별 소환 가능 수
        public int DeploymentLimit { get; } // 현재 편성 기준 총 소환 한도
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
