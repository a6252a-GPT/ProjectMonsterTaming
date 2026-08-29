using ProjectMT.Contents.Framework;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [CreateAssetMenu(
        menuName = "ProjectMT/Castle Raid Hex/Result Adapter",
        fileName = "HexCastleRaidResultAdapter")]
    public sealed class HexCastleRaidResultAdapter : ContentResultAdapter
    {
        [SerializeField] private RewardDefinition firstClearReward;

        public override bool TryCreateProgressChange(IContentResultData result, out GameProgressChange change)
        {
            if (!(result is IObjectiveCompletionResultData castleResult) || !castleResult.ObjectiveCompleted)
            {
                change = null;
                return false;
            }

            change = GameProgressChange.RecordCastleRaidClear();
            return true;
        }

        public override bool TryCreateProgressChange(
            IContentResultData result,
            GameProgressView progress,
            out GameProgressChange change)
        {
            if (!(result is IObjectiveCompletionResultData castleResult) || !castleResult.ObjectiveCompleted)
            {
                change = null;
                return false;
            }

            var rewards = RewardBundle.Empty;
            if (!progress.CastleRaidFirstClear && firstClearReward != null &&
                !firstClearReward.TryCreate(1L, out rewards))
            {
                change = null;
                return false;
            }

            change = GameProgressChange.RecordCastleRaidClear(rewards);
            return true;
        }

        public override bool TryCreateRewardPresentation(
            IContentResultData result,
            GameProgressView progress,
            ItemCatalog itemCatalog,
            out RewardPresentationRequest presentation)
        {
            if (!(result is IObjectiveCompletionResultData castleResult) || !castleResult.ObjectiveCompleted ||
                progress.CastleRaidFirstClear || firstClearReward == null ||
                !firstClearReward.TryCreate(1L, out var rewards) || rewards.IsEmpty)
            {
                presentation = null;
                return false;
            }

            presentation = RewardPresentationRequest.FromBundle(rewards, itemCatalog);
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigureReward(RewardDefinition reward)
        {
            firstClearReward = reward;
        }
#endif
    }
}
