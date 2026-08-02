using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    [CreateAssetMenu(menuName = "ProjectMT/Castle Raid/Start Data Factory", fileName = "CastleRaidStartDataFactory")]
    public sealed class CastleRaidStartDataFactory : ContentStartDataFactory // 성 침공 시작값 생성
    {
        [SerializeField, Range(1, 5)] private int deploymentLimit = 5; // 한 판 소환 한도

        public override IContentStartData Create(BattlePartySnapshot party)
        {
            return new CastleRaidStartData(party, deploymentLimit);
        }
    }
}
