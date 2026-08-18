using System;
using System.Threading;
using System.Threading.Tasks;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
using UnityEngine;

namespace ProjectMT.Contents.Framework
{
    public sealed class GrowthDungeonSweepService : IGrowthDungeonSweepService // 1회 소탕 저장·결과 조율
    {
        private readonly ContentCatalog catalog;
        private readonly IGameProgressService progress;
        private readonly ItemCatalog itemCatalog;
        private readonly IRewardPresentationPlayer rewardPresentation;
        private readonly IContentFinishFeedback finishFeedback;
        private readonly IContentResultView resultView;

        private int busy;

        public GrowthDungeonSweepService(
            ContentCatalog catalog,
            IGameProgressService progress,
            ItemCatalog itemCatalog,
            IRewardPresentationPlayer rewardPresentation,
            IContentFinishFeedback finishFeedback,
            IContentResultView resultView)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            this.itemCatalog = itemCatalog;
            this.rewardPresentation = rewardPresentation;
            this.finishFeedback = finishFeedback ?? throw new ArgumentNullException(nameof(finishFeedback));
            this.resultView = resultView;
        }

        public bool IsBusy => Volatile.Read(ref busy) != 0;

        public async Task<bool> TrySweepAsync(ContentId contentId)
        {
            if (Interlocked.Exchange(ref busy, 1) != 0)
            {
                return false;
            }

            try
            {
                if (!TryCreateSettlement(
                        contentId,
                        out var change,
                        out var rewardRequest,
                        out var resultPresentation))
                {
                    return false;
                }

                while (!await TrySaveAsync(change))
                {
                    var retrySource = new TaskCompletionSource<bool>();
                    finishFeedback.ShowSaveFailed(() => retrySource.TrySetResult(true));
                    await retrySource.Task; // 같은 변경 묶음만 재시도
                }

                finishFeedback.Hide();
                if (resultView != null)
                {
                    try
                    {
                        await resultView.ShowAsync(resultPresentation);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }

                if (rewardRequest != null && !rewardRequest.IsEmpty)
                {
                    try
                    {
                        rewardPresentation?.PlayConfirmed(rewardRequest);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }

                return true;
            }
            finally
            {
                Interlocked.Exchange(ref busy, 0);
            }
        }

        private bool TryCreateSettlement(
            ContentId contentId,
            out GameProgressChange change,
            out RewardPresentationRequest rewardRequest,
            out ContentResultPresentation resultPresentation)
        {
            change = null;
            rewardRequest = null;
            resultPresentation = null;
            if (!contentId.IsValid || !catalog.TryGet(contentId, out var definition) ||
                definition == null || definition.OpenMode != ContentOpenMode.MainBattleHosted ||
                !definition.SupportsSweep || definition.ResultAdapter == null)
            {
                return false;
            }

            var settlementView = progress.View;
            var stage = settlementView.GrowthDungeons.GetHighestClearedStage(contentId.Value);
            settlementView.Items.TryGetQuantity(definition.DungeonKeyItemId, out var keyQuantity);
            if (stage <= 0 || keyQuantity <= 0L)
            {
                return false;
            }

            var stageId = stage.ToString();
            var runInfo = new ContentRunInfo(contentId, stageId, ContentRunMode.Farming);
            var adapter = definition.ResultAdapter;
            if (!adapter.TryCreateSweepResult(settlementView, stageId, out var result) ||
                !adapter.IsSuccessfulResult(result) ||
                !adapter.TryCreateProgressChange(result, settlementView, runInfo, out change) ||
                !change.TryAttachGrowthDungeonSettlement(
                    contentId.Value,
                    stage,
                    recordClear: false,
                    definition.DungeonKeyItemId))
            {
                change = null;
                return false;
            }

            adapter.TryCreateRewardPresentation(
                result,
                settlementView,
                runInfo,
                itemCatalog,
                out rewardRequest);
            resultPresentation = new ContentResultPresentation(
                contentId,
                definition.DisplayName,
                ContentOutcome.Complete,
                "소탕 · " + adapter.CreateResultSummary(result, runInfo, ContentOutcome.Complete),
                rewardRequest);
            return true;
        }

        private async Task<bool> TrySaveAsync(GameProgressChange change)
        {
            finishFeedback.ShowSaving();
            try
            {
                return await progress.TryApplyAndSaveAsync(change);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }
    }
}
