using ProjectMT.Contents.Framework;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    [CreateAssetMenu(menuName = "ProjectMT/Castle Raid/Result Adapter", fileName = "CastleRaidResultAdapter")]
    public sealed class CastleRaidResultAdapter : ContentResultAdapter // 성 파괴를 진행 기록으로 번역
    {
        [SerializeField] private RewardDefinition firstClearReward; // 최초 승리 1회 보상

        public override bool TryCreateProgressChange(IContentResultData result, out GameProgressChange change)
        {
            if (!(result is CastleRaidResult castleResult) || !castleResult.MainCastleDestroyed)
            {
                change = null;
                return false;
            }

            change = GameProgressChange.RecordCastleRaidClear(); // 첫 클리어 기록 요청
            return true;
        }

        public override bool TryCreateProgressChange(
            IContentResultData result,
            GameProgressView progress,
            out GameProgressChange change)
        {
            if (!(result is CastleRaidResult castleResult) || !castleResult.MainCastleDestroyed)
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
            if (!(result is CastleRaidResult castleResult) || !castleResult.MainCastleDestroyed ||
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
