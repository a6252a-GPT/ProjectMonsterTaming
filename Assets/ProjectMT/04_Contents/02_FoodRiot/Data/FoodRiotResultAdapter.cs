using ProjectMT.Contents.Framework;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Reward;
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

            var rewards = RewardBundle.FromGold(foodResult.KillCount); // 시드는 1마리당 골드 1
            change = GameProgressChange.RecordFoodRiot(foodResult.KillCount, rewards);
            return true;
        }

        public override bool TryCreateRewardPresentation(
            IContentResultData result,
            out RewardPresentationRequest presentation)
        {
            if (!(result is FoodRiotResult foodResult) || foodResult.KillCount <= 0)
            {
                presentation = null;
                return false;
            }

            presentation = RewardPresentationRequest.FromBundle(
                RewardBundle.FromGold(foodResult.KillCount)); // 저장되는 시드 골드와 같은 값
            return true;
        }
    }
}
