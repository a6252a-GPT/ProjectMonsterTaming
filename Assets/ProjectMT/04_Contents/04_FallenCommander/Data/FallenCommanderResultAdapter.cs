using ProjectMT.Contents.Framework;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    [CreateAssetMenu(
        menuName = "ProjectMT/Fallen Commander/Result Adapter",
        fileName = "FallenCommanderResultAdapter")]
    public sealed class FallenCommanderResultAdapter : ContentResultAdapter
    {
        [SerializeField] private RewardDefinition clearReward;
        [SerializeField] private GrowthDungeonRewardTable stageRewards;

        public override bool TryCreateProgressChange(
            IContentResultData result,
            out GameProgressChange change)
        {
            if (!(result is FallenCommanderResult commanderResult) ||
                !TryCreateRewards(commanderResult, default, out var rewards))
            {
                change = null;
                return false;
            }

            change = GameProgressChange.GrantRewards(rewards);
            return true;
        }

        public override bool TryCreateProgressChange(
            IContentResultData result,
            GameProgressView progress,
            ContentRunInfo runInfo,
            out GameProgressChange change)
        {
            if (!(result is FallenCommanderResult commanderResult) ||
                !TryCreateRewards(commanderResult, runInfo, out var rewards))
            {
                change = null;
                return false;
            }

            change = GameProgressChange.GrantRewards(rewards);
            return true;
        }

        public override bool IsSuccessfulResult(IContentResultData result)
        {
            return result is FallenCommanderResult commanderResult && commanderResult.Cleared;
        }

        public override string CreateResultSummary(
            IContentResultData result,
            ContentRunInfo runInfo,
            ContentOutcome outcome)
        {
            if (!(result is FallenCommanderResult commanderResult))
            {
                return base.CreateResultSummary(result, runInfo, outcome);
            }

            var status = commanderResult.Cleared ? "클리어" : "실패";
            return $"{runInfo.StageId}단계 {status} · 점수 {Mathf.Max(0, commanderResult.Score)}";
        }

        public override bool TryCreateSweepResult(
            GameProgressView progress,
            string stageId,
            out IContentResultData result)
        {
            result = new FallenCommanderResult(0, 0f, cleared: true);
            return true;
        }

        public override bool TryCreateRewardPresentation(
            IContentResultData result,
            out RewardPresentationRequest presentation)
        {
            return TryCreateRewardPresentation(result, default, default, null, out presentation);
        }

        public override bool TryCreateRewardPresentation(
            IContentResultData result,
            GameProgressView progress,
            ItemCatalog itemCatalog,
            out RewardPresentationRequest presentation)
        {
            return TryCreateRewardPresentation(result, progress, default, itemCatalog, out presentation);
        }

        public override bool TryCreateRewardPresentation(
            IContentResultData result,
            GameProgressView progress,
            ContentRunInfo runInfo,
            ItemCatalog itemCatalog,
            out RewardPresentationRequest presentation)
        {
            if (!(result is FallenCommanderResult commanderResult) ||
                !TryCreateRewards(commanderResult, runInfo, out var rewards) ||
                rewards.IsEmpty)
            {
                presentation = null;
                return false;
            }

            presentation = RewardPresentationRequest.FromBundle(rewards, itemCatalog);
            return true;
        }

        private bool TryCreateRewards(
            FallenCommanderResult result,
            ContentRunInfo runInfo,
            out RewardBundle rewards)
        {
            rewards = null;
            if (result == null || !result.Cleared)
            {
                return false;
            }

            if (stageRewards != null && int.TryParse(runInfo.StageId, out var stage))
            {
                return stageRewards.TryCreate(stage, runInfo.RunMode, out rewards);
            }

            return clearReward != null && clearReward.TryCreate(1L, out rewards);
        }

#if UNITY_EDITOR
        public void EditorConfigureRewards(
            RewardDefinition fallbackClearReward,
            GrowthDungeonRewardTable rewardTable)
        {
            clearReward = fallbackClearReward;
            stageRewards = rewardTable;
        }
#endif
    }
}
