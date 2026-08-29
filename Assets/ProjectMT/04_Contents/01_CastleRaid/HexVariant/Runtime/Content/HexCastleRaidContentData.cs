using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    public sealed class HexCastleRaidStartData : IPartyDeploymentStartData // 육각 전장 부대 투입값
    {
        private const int MaxUnitSlotCount = 10; // 편성창 본부대 슬롯 수

        public HexCastleRaidStartData(BattlePartySnapshot party, int summonsPerSlot)
        {
            Party = party;
            SummonsPerSlot = Mathf.Clamp(summonsPerSlot, 1, 3);
            UnitSlotCount = Mathf.Min(MaxUnitSlotCount, Party?.Units.Length ?? 0);
            DeploymentLimit = UnitSlotCount * SummonsPerSlot;
        }

        public BattlePartySnapshot Party { get; }
        public int UnitSlotCount { get; }
        public int SummonsPerSlot { get; }
        public int DeploymentLimit { get; }
    }
}
