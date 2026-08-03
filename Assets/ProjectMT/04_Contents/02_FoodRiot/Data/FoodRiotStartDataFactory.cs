using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.FoodRiot
{
    [CreateAssetMenu(menuName = "ProjectMT/Food Riot/Start Data Factory", fileName = "FoodRiotStartDataFactory")]
    public sealed class FoodRiotStartDataFactory : ContentStartDataFactory // 설정값으로 시작 데이터 생성
    {
        [SerializeField, Min(1f)] private float durationSeconds = 20f; // 제한 시간
        [SerializeField, Range(5, 10)] private int activeVegetableCount = 8; // 동시에 유지할 야채 수

        public override IContentStartData Create(BattlePartySnapshot party)
        {
            return new FoodRiotStartData(party, durationSeconds, activeVegetableCount);
        }
    }
}
