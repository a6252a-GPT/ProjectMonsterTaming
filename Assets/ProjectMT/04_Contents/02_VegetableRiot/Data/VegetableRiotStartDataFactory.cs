using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.VegetableRiot
{
    [CreateAssetMenu(menuName = "ProjectMT/Vegetable Riot/Start Data Factory", fileName = "VegetableRiotStartDataFactory")]
    public sealed class VegetableRiotStartDataFactory : ContentStartDataFactory // 설정값으로 시작 데이터 생성
    {
        [SerializeField, Min(1f)] private float durationSeconds = 20f; // 제한 시간
        [SerializeField, Range(5, 10)] private int activeVegetableCount = 8; // 동시에 유지할 야채 수

        public override IContentStartData Create(BattlePartySnapshot party)
        {
            return new VegetableRiotStartData(party, durationSeconds, activeVegetableCount);
        }
    }
}
