using ProjectMT.Contents.Framework;
using ProjectMT.Shared.GameData;
using UnityEngine;

namespace ProjectMT.Contents.FoodRiot
{
    [CreateAssetMenu(menuName = "ProjectMT/Food Riot/Result Adapter", fileName = "FoodRiotResultAdapter")]
    public sealed class FoodRiotResultAdapter : ContentResultAdapter // 처치 결과를 저장 변화로 변환
    {
        public override bool TryCreateProgressChange(IContentResultData result, out GameProgressChange change)
        {
            if (!(result is FoodRiotResult foodResult))
            {
                change = null;
                return false;
            }

            change = GameProgressChange.RecordFoodRiot(foodResult.KillCount, foodResult.KillCount); // 시드는 1마리당 골드 1
            return true;
        }
    }
}
