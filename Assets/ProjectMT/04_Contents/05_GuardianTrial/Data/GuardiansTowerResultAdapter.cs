using ProjectMT.Contents.Framework;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Reward;
using UnityEngine;

namespace ProjectMT.Contents.GuardianTrial
{
    // 08.06 안건준 추가 - 수호자의 탑 결과를 저장 변화로 변환 (식량 대소동 Adapter와 독립)
    [CreateAssetMenu(menuName = "ProjectMT/Guardian Trial/Guardians Tower Result Adapter", fileName = "GuardiansTowerResultAdapter")]
    public sealed class GuardiansTowerResultAdapter : ContentResultAdapter
    {
        public override bool TryCreateProgressChange(IContentResultData result, out GameProgressChange change)
        {
            if (!(result is GuardiansTowerResult towerResult))
            {
                change = null;
                return false;
            }

            var rewards = RewardBundle.FromGold(towerResult.KillCount); // 처치 1마리당 골드 1
            change = GameProgressChange.RecordGuardiansTowerClear(towerResult.KillCount, rewards);
            return true;
        }

        public override bool TryCreateRewardPresentation(
            IContentResultData result,
            out RewardPresentationRequest presentation)
        {
            if (!(result is GuardiansTowerResult towerResult) || towerResult.KillCount <= 0)
            {
                presentation = null;
                return false;
            }

            presentation = RewardPresentationRequest.FromBundle(
                RewardBundle.FromGold(towerResult.KillCount)); // 저장되는 골드와 같은 값
            return true;
        }
    }
}
