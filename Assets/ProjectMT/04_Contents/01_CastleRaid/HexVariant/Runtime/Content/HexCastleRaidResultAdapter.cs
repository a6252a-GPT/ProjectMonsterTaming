using System;
using System.Collections.Generic;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectMT.Contents.CastleRaidHex
{
    [CreateAssetMenu(
        menuName = "ProjectMT/Castle Raid Hex/Result Adapter",
        fileName = "HexCastleRaidResultAdapter")]
    public sealed class HexCastleRaidResultAdapter : ContentResultAdapter
    {
        [FormerlySerializedAs("firstClearReward")]
        [SerializeField] private RewardDefinition stageOneReward;

        public override bool TryCreateProgressChange(IContentResultData result, out GameProgressChange change)
        {
            if (!(result is IObjectiveCompletionResultData castleResult) || !castleResult.ObjectiveCompleted)
            {
                change = null;
                return false;
            }

            TryCreateFirstClearReward(CastleRaidStageRules.MinimumStage, out var firstClear);
            if (!RewardBundle.TryCombine(firstClear, ResolveLootRewards(result), out var combined))
            {
                change = null;
                return false;
            }

            change = GameProgressChange.RecordCastleRaidClear(
                CastleRaidStageRules.MinimumStage,
                combined,
                ResolveEquipmentRewards(result));
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

            return TryCreateStageProgressChange(
                CastleRaidStageRules.MinimumStage,
                progress,
                ResolveLootRewards(result),
                ResolveEquipmentRewards(result),
                out change);
        }

        public override bool TryCreateProgressChange(
            IContentResultData result,
            GameProgressView progress,
            ContentRunInfo runInfo,
            out GameProgressChange change)
        {
            if (!(result is IObjectiveCompletionResultData castleResult) ||
                !castleResult.ObjectiveCompleted ||
                !TryResolveStage(runInfo, out var stage))
            {
                change = null;
                return false;
            }

            return TryCreateStageProgressChange(
                stage,
                progress,
                ResolveLootRewards(result),
                ResolveEquipmentRewards(result),
                out change);
        }

        public override bool TryCreateRewardPresentation(
            IContentResultData result,
            GameProgressView progress,
            ItemCatalog itemCatalog,
            out RewardPresentationRequest presentation)
        {
            if (!(result is IObjectiveCompletionResultData castleResult) || !castleResult.ObjectiveCompleted)
            {
                presentation = null;
                return false;
            }

            var rewards = ResolveLootRewards(result);
            if (!progress.CastleRaidFirstClear &&
                TryCreateFirstClearReward(CastleRaidStageRules.MinimumStage, out var firstClear) &&
                !RewardBundle.TryCombine(firstClear, rewards, out rewards))
            {
                presentation = null;
                return false;
            }

            if (rewards.IsEmpty)
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
            if (!(result is IObjectiveCompletionResultData castleResult) ||
                !castleResult.ObjectiveCompleted ||
                !TryResolveStage(runInfo, out var stage))
            {
                presentation = null;
                return false;
            }

            var rewards = ResolveLootRewards(result);
            if (CastleRaidStageRules.IsNewClear(stage, progress.CastleRaidHighestClearedStage) &&
                TryCreateFirstClearReward(stage, out var firstClear) &&
                !RewardBundle.TryCombine(firstClear, rewards, out rewards))
            {
                presentation = null;
                return false;
            }

            if (rewards.IsEmpty)
            {
                presentation = null;
                return false;
            }

            presentation = RewardPresentationRequest.FromBundle(rewards, itemCatalog);
            return true;
        }

        public override string CreateResultSummary(
            IContentResultData result,
            ContentRunInfo runInfo,
            ContentOutcome outcome)
        {
            if (!TryResolveStage(runInfo, out var stage))
            {
                return outcome == ContentOutcome.Fail
                    ? "요새 공략에 실패했습니다."
                    : "요새 공략을 완료했습니다.";
            }

            var failureReason = (result as HexCastleRaidResult)?.FailureReason;
            return outcome == ContentOutcome.Fail
                ? failureReason == HexCastleRaidFailureReason.TimeExpired
                    ? $"STAGE {stage:000} 제한 시간을 초과했습니다."
                    : $"STAGE {stage:000} 공격 부대가 전멸했습니다."
                : $"STAGE {stage:000} 왕궁을 파괴했습니다.";
        }

        private bool TryCreateStageProgressChange(
            int stage,
            GameProgressView progress,
            RewardBundle lootRewards,
            IReadOnlyList<EquipmentInstanceData> equipmentRewards,
            out GameProgressChange change)
        {
            if (!CastleRaidStageRules.IsValidStage(stage) ||
                stage > progress.CastleRaidHighestClearedStage + 1)
            {
                change = null;
                return false;
            }

            lootRewards ??= RewardBundle.Empty;
            equipmentRewards ??= Array.Empty<EquipmentInstanceData>();
            if (!CastleRaidStageRules.IsNewClear(stage, progress.CastleRaidHighestClearedStage))
            {
                change = GameProgressChange.GrantRewardsAndEquipment(lootRewards, equipmentRewards);
                return true;
            }

            if (!TryCreateFirstClearReward(stage, out var firstClearRewards) ||
                !RewardBundle.TryCombine(firstClearRewards, lootRewards, out var rewards))
            {
                change = null;
                return false;
            }

            change = GameProgressChange.RecordCastleRaidClear(stage, rewards, equipmentRewards);
            return true;
        }

        private static RewardBundle ResolveLootRewards(IContentResultData result)
        {
            return (result as HexCastleRaidResult)?.LootRewards ?? RewardBundle.Empty;
        }

        private static IReadOnlyList<EquipmentInstanceData> ResolveEquipmentRewards(IContentResultData result)
        {
            return (result as HexCastleRaidResult)?.EquipmentRewards ?? Array.Empty<EquipmentInstanceData>();
        }

        private bool TryCreateFirstClearReward(int stage, out RewardBundle rewards)
        {
            rewards = CastleRaidStageRules.CreateFirstClearReward(stage);
            return rewards != null && !rewards.IsEmpty;
        }

        private static bool TryResolveStage(ContentRunInfo runInfo, out int stage)
        {
            if (runInfo.RunMode == ContentRunMode.SeedTest)
            {
                stage = CastleRaidStageRules.MinimumStage;
                return true;
            }

            return int.TryParse(runInfo.StageId, out stage) &&
                   CastleRaidStageRules.IsValidStage(stage);
        }

#if UNITY_EDITOR
        public void EditorConfigureReward(RewardDefinition reward)
        {
            stageOneReward = reward;
        }
#endif
    }
}
