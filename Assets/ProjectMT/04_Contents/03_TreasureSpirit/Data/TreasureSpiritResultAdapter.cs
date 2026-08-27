using ProjectMT.Contents.Framework;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit
{
    [CreateAssetMenu(menuName = "ProjectMT/Treasure Spirit/Result Adapter", fileName = "TreasureSpiritResultAdapter")]
    public sealed class TreasureSpiritResultAdapter : ContentResultAdapter // 결과를 공용 저장·보상 변경으로 변환
    {
        [SerializeField] private RewardDefinition clearReward;
        [SerializeField] private GrowthDungeonRewardTable stageRewards;

        public override bool TryCreateProgressChange(
            IContentResultData result,
            out GameProgressChange change)
        {
            if (!(result is TreasureSpiritResult treasureResult) ||
                !TryCreateRewards(treasureResult, default, out var rewards))
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
            if (!(result is TreasureSpiritResult treasureResult) ||
                !TryCreateRewards(treasureResult, runInfo, out var rewards))
            {
                change = null;
                return false;
            }

            change = GameProgressChange.GrantRewards(rewards);
            return true;
        }

        public override bool IsSuccessfulResult(IContentResultData result)
        {
            return result is TreasureSpiritResult treasureResult && treasureResult.Cleared;
        }

        public override string CreateResultSummary(
            IContentResultData result,
            ContentRunInfo runInfo,
            ContentOutcome outcome)
        {
            if (!(result is TreasureSpiritResult treasureResult))
            {
                return base.CreateResultSummary(result, runInfo, outcome);
            }

            var stage = string.IsNullOrWhiteSpace(runInfo.StageId) ? "1" : runInfo.StageId;
            var status = treasureResult.Cleared ? "클리어" : "실패";
            return $"{stage}단계 {status} · 처치 {treasureResult.KillCount} · 남은 시간 {Mathf.CeilToInt(treasureResult.RemainingTime)}초";
        }

        public override bool TryCreateSweepResult(
            GameProgressView progress,
            string stageId,
            out IContentResultData result)
        {
            result = new TreasureSpiritResult(true, 0, 0f, "소탕 완료");
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
            if (!(result is TreasureSpiritResult treasureResult) ||
                !TryCreateRewards(treasureResult, runInfo, out var rewards) ||
                rewards.IsEmpty)
            {
                presentation = null;
                return false;
            }

            presentation = RewardPresentationRequest.FromBundle(rewards, itemCatalog);
            return true;
        }

        private bool TryCreateRewards(
            TreasureSpiritResult result,
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
