using ProjectMT.Contents.Framework;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
using UnityEngine;

namespace ProjectMT.Contents.GuardianTrial
{
    // 08.06 안건준 추가 - 수호자의 탑 결과를 저장 변화로 변환 (식량 대소동 Adapter와 독립)
    [CreateAssetMenu(menuName = "ProjectMT/Guardian Trial/Guardians Tower Result Adapter", fileName = "GuardiansTowerResultAdapter")]
    public sealed class GuardiansTowerResultAdapter : ContentResultAdapter
    {
        [SerializeField] private RewardDefinition rewardPerKill; // 처치 수 배수 보상
        [SerializeField] private RewardDefinition clearReward; // 클리어 1회 보상

        public override bool TryCreateProgressChange(IContentResultData result, out GameProgressChange change)
        {
            if (!(result is GuardiansTowerResult towerResult) || !TryCreateRewards(towerResult, out var rewards))
            {
                change = null;
                return false;
            }

            // 08.07 안건준 수정 - 실패한 판은 난이도를 올리지 않도록 Cleared 여부를 함께 전달한다.
            change = GameProgressChange.RecordGuardiansTowerClear(towerResult.KillCount, towerResult.Cleared, rewards);
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
            if (!(result is GuardiansTowerResult towerResult) ||
                !TryCreateRewards(towerResult, out var rewards) || rewards.IsEmpty)
            {
                presentation = null;
                return false;
            }

            presentation = RewardPresentationRequest.FromBundle(rewards, itemCatalog);
            return true;
        }

        private bool TryCreateRewards(GuardiansTowerResult result, out RewardBundle rewards)
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
            }
            else if (!rewardPerKill.TryCreate(killCount, out rewards))
            {
                return false;
            }

            if (!result.Cleared || clearReward == null)
            {
                return true;
            }

            return clearReward.TryCreate(1L, out var clearedRewards) &&
                   RewardBundle.TryCombine(rewards, clearedRewards, out rewards);
        }

#if UNITY_EDITOR
        public void EditorConfigureRewards(RewardDefinition perKill, RewardDefinition onClear)
        {
            rewardPerKill = perKill;
            clearReward = onClear;
        }
#endif
    }
}
