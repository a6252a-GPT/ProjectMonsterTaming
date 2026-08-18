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
        [SerializeField] private GrowthDungeonRewardTable stageRewards; // 5단계 Farming·Challenge 보상표

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

        public override bool TryCreateProgressChange(
            IContentResultData result,
            GameProgressView progress,
            ContentRunInfo runInfo,
            out GameProgressChange change)
        {
            if (!(result is GuardiansTowerResult towerResult) ||
                !TryCreateRewards(towerResult, runInfo, out var rewards))
            {
                change = null;
                return false;
            }

            change = GameProgressChange.RecordGuardiansTowerClear(
                towerResult.KillCount,
                towerResult.Cleared,
                runInfo.RunMode != ContentRunMode.Farming,
                rewards);
            return true;
        }

        public override bool IsSuccessfulResult(IContentResultData result)
        {
            return result is GuardiansTowerResult towerResult && towerResult.Cleared;
        }

        public override string CreateResultSummary(
            IContentResultData result,
            ContentRunInfo runInfo,
            ContentOutcome outcome)
        {
            if (!(result is GuardiansTowerResult towerResult))
            {
                return base.CreateResultSummary(result, runInfo, outcome);
            }

            var status = towerResult.Cleared ? "클리어" : "실패";
            return $"{runInfo.StageId}단계 {status} · 처치 {Mathf.Max(0, towerResult.KillCount)}마리";
        }

        public override bool TryCreateSweepResult(
            GameProgressView progress,
            string stageId,
            out IContentResultData result)
        {
            result = new GuardiansTowerResult(Mathf.Max(0, progress.GuardiansTowerBestKills), cleared: true);
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

        public override bool TryCreateRewardPresentation(
            IContentResultData result,
            GameProgressView progress,
            ContentRunInfo runInfo,
            ItemCatalog itemCatalog,
            out RewardPresentationRequest presentation)
        {
            if (!(result is GuardiansTowerResult towerResult) ||
                !TryCreateRewards(towerResult, runInfo, out var rewards) || rewards.IsEmpty)
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

            if (!TryCreateKillRewards(result, out rewards))
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

        private bool TryCreateRewards(
            GuardiansTowerResult result,
            ContentRunInfo runInfo,
            out RewardBundle rewards)
        {
            rewards = null;
            if (result == null || !TryCreateKillRewards(result, out var killRewards))
            {
                return false;
            }

            if (!result.Cleared || stageRewards == null ||
                !int.TryParse(runInfo.StageId, out var stage))
            {
                return TryCreateRewards(result, out rewards);
            }

            return stageRewards.TryCreate(stage, runInfo.RunMode, out var stageReward) &&
                   RewardBundle.TryCombine(killRewards, stageReward, out rewards);
        }

        private bool TryCreateKillRewards(GuardiansTowerResult result, out RewardBundle rewards)
        {
            var killCount = Mathf.Max(0, result.KillCount);
            if (rewardPerKill == null)
            {
                rewards = RewardBundle.FromGold(killCount);
                return true;
            }

            return rewardPerKill.TryCreate(killCount, out rewards);
        }

#if UNITY_EDITOR
        public void EditorConfigureRewards(
            RewardDefinition perKill,
            RewardDefinition onClear,
            GrowthDungeonRewardTable rewardTable = null)
        {
            rewardPerKill = perKill;
            clearReward = onClear;
            stageRewards = rewardTable;
        }
#endif
    }
}
