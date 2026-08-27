using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit
{
    [CreateAssetMenu(menuName = "ProjectMT/Treasure Spirit/Start Data Factory", fileName = "TreasureSpiritStartDataFactory")]
    public sealed class TreasureSpiritStartDataFactory : ContentStartDataFactory // 편성을 보물 정령 시작값으로 변환
    {
        [SerializeField, Min(1f)] private float durationSeconds = 100f;

        public override IContentStartData Create(BattlePartySnapshot party)
        {
            return new TreasureSpiritStartData(party, durationSeconds);
        }
    }
}
