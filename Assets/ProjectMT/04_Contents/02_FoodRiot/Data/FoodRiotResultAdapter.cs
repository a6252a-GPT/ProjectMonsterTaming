using ProjectMT.Contents.Framework;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
using UnityEngine;

namespace ProjectMT.Contents.FoodRiot
{
    [CreateAssetMenu(menuName = "ProjectMT/Food Riot/Result Adapter", fileName = "FoodRiotResultAdapter")]
    public sealed class FoodRiotResultAdapter : ContentResultAdapter // 처치 결과를 저장 변화로 변환
    {
        [SerializeField] private RewardDefinition rewardPerKill; // 처치 수 배수 보상

        public override bool TryCreateProgressChange(IContentResultData result, out GameProgressChange change)
        {
            if (!(result is FoodRiotResult foodResult) || !TryCreateRewards(foodResult, out var rewards))
            {
                change = null;
                return false;
            }

            change = GameProgressChange.RecordFoodRiot(foodResult.KillCount, rewards);
            return true;
        }

        public override bool TryCreateProgressChange(
            IContentResultData result,
            GameProgressView progress,
            ContentRunInfo runInfo,
            out GameProgressChange change)
        {
            return TryCreateProgressChange(result, out change);
        }

        public override string CreateResultSummary(
            IContentResultData result,
            ContentRunInfo runInfo,
            ContentOutcome outcome)
        {
            return result is FoodRiotResult foodResult
                ? $"{runInfo.StageId}단계 · 처치 {Mathf.Max(0, foodResult.KillCount)}마리"
                : base.CreateResultSummary(result, runInfo, outcome);
        }

        public override bool TryCreateSweepResult(
            GameProgressView progress,
            string stageId,
            out IContentResultData result)
        {
            result = new FoodRiotResult(Mathf.Max(0, progress.FoodRiotBestKills));
            return true;
        }

        public override bool TryCreateRewardPresentation(
            IContentResultData result,
            out RewardPresentationRequest presentation)
        {
            return TryCreateRewardPresentation(result, default, null, out presentation);
        }

        public override bool TryCreateRewardPresentation(
            IContentResultData result,
            GameProgressView progress,
            ItemCatalog itemCatalog,
            out RewardPresentationRequest presentation)
        {
            if (!(result is FoodRiotResult foodResult) ||
                !TryCreateRewards(foodResult, out var rewards) || rewards.IsEmpty)
            {
                presentation = null;
                return false;
            }

            presentation = RewardPresentationRequest.FromBundle(rewards, itemCatalog);
            return true;
        }

        private bool TryCreateRewards(FoodRiotResult result, out RewardBundle rewards)
        {
            rewards = null;
            if (result == null)
            {
                return false;
            }

            var killCount = Mathf.Max(0, result.KillCount);
            if (rewardPerKill == null)
            {
                rewards = RewardBundle.FromGold(killCount); // 미연결 에셋에서도 기존 보상 보존
                return true;
            }

            return rewardPerKill.TryCreate(killCount, out rewards);
        }

#if UNITY_EDITOR
        public void EditorConfigureReward(RewardDefinition perKill)
        {
            rewardPerKill = perKill;
        }
#endif
    }
}
