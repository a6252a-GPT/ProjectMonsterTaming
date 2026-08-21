using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectMT.Contents.CastleRaid
{
    [CreateAssetMenu(menuName = "ProjectMT/Castle Raid/Start Data Factory", fileName = "CastleRaidStartDataFactory")]
    public sealed class CastleRaidStartDataFactory : ContentStartDataFactory // 성 침공 시작값 생성
    {
        [FormerlySerializedAs("deploymentLimit")]
        [SerializeField, Range(1, 3)] private int summonsPerSlot = 3; // 몬스터 슬롯별 소환 수

        public override IContentStartData Create(BattlePartySnapshot party)
        {
            return new CastleRaidStartData(party, summonsPerSlot);
        }
    }
}
