using System;
using System.Threading;
using System.Threading.Tasks;
using ProjectMT.Core.SceneFlow;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.Framework
{
    public sealed class ContentFlow : IContentLauncher // 콘텐츠 입장·결과·복귀 조율
    {
        private readonly ContentCatalog catalog; // 콘텐츠 Definition 조회
        private readonly IGameProgressService progress; // 결과 반영·저장
        private readonly ISceneNavigator sceneNavigator; // 별도 씬 이동
        private readonly SceneId mainBattleSceneId; // 별도 콘텐츠 복귀 대상
        private readonly ItemCatalog itemCatalog; // 보상 표시 이름 해석
        private readonly IRewardPresentationPlayer rewardPresentation; // 저장 성공 보상 표현
        private readonly IContentFinishFeedback finishFeedback; // 저장 중·실패 재시도 표시
        private readonly IContentResultView resultView; // 저장 확정 뒤 공통 결과창

        private ActiveRun activeRun; // 동시에 한 판만 허용

        public ContentFlow(
            ContentCatalog catalog,
            IGameProgressService progress,
            ISceneNavigator sceneNavigator,
            SceneId mainBattleSceneId,
            IRewardPresentationPlayer rewardPresentation,
            IContentFinishFeedback finishFeedback)
            : this(
                catalog,
                progress,
                sceneNavigator,
                mainBattleSceneId,
                null,
                rewardPresentation,
                finishFeedback,
                null)
        {
        }

        public ContentFlow(
            ContentCatalog catalog,
            IGameProgressService progress,
            ISceneNavigator sceneNavigator,
            SceneId mainBattleSceneId,
            ItemCatalog itemCatalog,
            IRewardPresentationPlayer rewardPresentation,
            IContentFinishFeedback finishFeedback)
            : this(
                catalog,
                progress,
                sceneNavigator,
                mainBattleSceneId,
                itemCatalog,
                rewardPresentation,
                finishFeedback,
                null)
        {
        }

        public ContentFlow(
            ContentCatalog catalog,
            IGameProgressService progress,
            ISceneNavigator sceneNavigator,
            SceneId mainBattleSceneId,
            ItemCatalog itemCatalog,
            IRewardPresentationPlayer rewardPresentation,
            IContentFinishFeedback finishFeedback,
            IContentResultView resultView)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            this.sceneNavigator = sceneNavigator ?? throw new ArgumentNullException(nameof(sceneNavigator));
            this.mainBattleSceneId = mainBattleSceneId;
            this.itemCatalog = itemCatalog;
            this.rewardPresentation = rewardPresentation;
            this.finishFeedback = finishFeedback ?? throw new ArgumentNullException(nameof(finishFeedback));
            this.resultView = resultView;
        }

        public bool IsRunning => activeRun != null;
        public ContentFlowPhase Phase { get; private set; } = ContentFlowPhase.Idle;
        public event Action<ContentId> HostedRunStarted;
        public event Action<ContentId> HostedRunFinished;

        public bool StartHosted(ContentId contentId, BattlePartySnapshot party, IHostedContentRunner runner)
        {
            return StartHostedInternal(
                contentId,
                party,
                runner,
                new ContentRunInfo(contentId, "seed", ContentRunMode.SeedTest));
        }

        public bool StartHosted(
            ContentId contentId,
            BattlePartySnapshot party,
            IHostedContentRunner runner,
            ContentRunMode runMode,
            int stage)
        {
            if (runMode != ContentRunMode.Challenge && runMode != ContentRunMode.Farming)
            {
                return false;
            }

            return StartHostedInternal(
                contentId,
                party,
                runner,
                new ContentRunInfo(contentId, Mathf.Max(1, stage).ToString(), runMode));
        }

        private bool StartHostedInternal(
            ContentId contentId,
            BattlePartySnapshot party,
            IHostedContentRunner runner,
            ContentRunInfo runInfo)
        {
            if (IsRunning || runner == null ||
                !TryGetDefinition(contentId, ContentOpenMode.MainBattleHosted, out var definition) ||
                !TryValidateGrowthDungeonEntry(definition, runInfo))
            {
                return false;
            }

            if (!TryCreateStartData(definition, party, out var startData))
            {
                return false;
            }

            var run = CreateRun(definition, startData, runner, runInfo);
            Phase = ContentFlowPhase.Entering;
            activeRun = run; // Open 전에 중복 입장 차단
            if (runner.Open(run.Context))
            {
                Phase = ContentFlowPhase.Playing;
                NotifyHostedRunStarted(contentId);
                return true;
            }

            activeRun = null; // Hosted 열기 실패 복구
            Phase = ContentFlowPhase.Idle;
            return false;
        }

        public bool TryGetGrowthDungeonState(ContentId contentId, out GrowthDungeonEntryState state)
        {
            state = default;
            if (!TryGetDefinition(contentId, ContentOpenMode.MainBattleHosted, out var definition) ||
                string.IsNullOrEmpty(definition.DungeonKeyItemId))
            {
                return false;
            }

            var view = progress.View;
            view.Items.TryGetQuantity(definition.DungeonKeyItemId, out var keyQuantity);
            state = new GrowthDungeonEntryState(
                contentId,
                definition.DisplayName,
                view.GrowthDungeons.GetHighestClearedStage(contentId.Value),
                keyQuantity,
                definition.SupportsSweep && definition.ResultAdapter != null);
            return true;
        }

        public bool StartSeparate(ContentId contentId, BattlePartySnapshot party)
        {
            return StartSeparate(contentId, party, default(ContentVariantId));
        }

        public bool StartSeparate(ContentId contentId, BattlePartySnapshot party, ContentVariantId variantId)
        {
            if (IsRunning || !TryGetDefinition(contentId, ContentOpenMode.SeparateScene, out var definition))
            {
                return false;
            }

            if (!definition.TryResolveSceneId(variantId, out var targetSceneId))
            {
                Debug.LogError($"Separate content variant has no SceneId. Content={contentId}, Variant={variantId}");
                return false;
            }

            if (!TryCreateStartData(definition, party, out var startData))
            {
                return false;
            }

            Phase = ContentFlowPhase.Entering;
            activeRun = CreateRun(
                definition,
                startData,
                null,
                new ContentRunInfo(contentId, "seed", ContentRunMode.SeedTest, variantId),
                targetSceneId); // 씬 이동 전 실행 등록
            sceneNavigator.Load(targetSceneId);
            return true;
        }

        public bool StartSeparate(ContentId contentId, BattlePartySnapshot party, int stage)
        {
            if (IsRunning ||
                !TryGetDefinition(contentId, ContentOpenMode.SeparateScene, out var definition) ||
                !definition.TryResolveSceneId(default, out var targetSceneId) ||
                !TryValidateCastleRaidEntry(stage, out var runMode))
            {
                return false;
            }

            if (!TryCreateStartData(definition, party, out var startData))
            {
                return false;
            }

            Phase = ContentFlowPhase.Entering;
            activeRun = CreateRun(
                definition,
                startData,
                null,
                new ContentRunInfo(contentId, stage.ToString(), runMode),
                targetSceneId);
            sceneNavigator.Load(targetSceneId);
            return true;
        }

        public bool TryGetCastleRaidState(ContentId contentId, out CastleRaidEntryState state)
        {
            state = default;
            if (!TryGetDefinition(contentId, ContentOpenMode.SeparateScene, out var definition))
            {
                return false;
            }

            state = new CastleRaidEntryState(
                contentId,
                definition.DisplayName,
                progress.View.CastleRaidHighestClearedStage);
            return true;
        }

        public ContentSceneContext CreateSeparateSceneContext(SceneId sceneId)
        {
            if (activeRun == null || activeRun.Definition.OpenMode != ContentOpenMode.SeparateScene || activeRun.SceneId != sceneId)
            {
                return null;
            }

            Phase = ContentFlowPhase.Playing;
            return new ContentSceneContext(activeRun.Definition, activeRun.Context);
        }

        public bool NotifySceneLoadFailed(SceneId sceneId) // 별도 씬 진입 실패 잠금 해제
        {
            if (activeRun == null || Phase != ContentFlowPhase.Entering ||
                activeRun.Definition.OpenMode != ContentOpenMode.SeparateScene ||
                activeRun.SceneId != sceneId)
            {
                return false;
            }

            activeRun = null;
            Phase = ContentFlowPhase.Idle;
            HideFinishFeedback();
            return true;
        }

        private ActiveRun CreateRun(
            ContentDefinition definition,
            IContentStartData startData,
            IHostedContentRunner runner,
            ContentRunInfo runInfo,
            SceneId sceneId = default)
        {
            var run = new ActiveRun(definition, runner, sceneId);
            var exit = new ContentExitGate( // 현재 Run 전용 비공개 출구
                result => _ = HandleExitAsync(run, ContentOutcome.Complete, result),
                result => _ = HandleExitAsync(run, ContentOutcome.Fail, result),
                () => _ = HandleExitAsync(run, ContentOutcome.Cancel, null));
            run.Context = new ContentContext(runInfo, startData, exit, progress); // 08.07 안건준 추가 - Progress 읽기 전용 전달
            return run;
        }

        private bool TryValidateGrowthDungeonEntry(ContentDefinition definition, ContentRunInfo runInfo)
        {
            if (runInfo.RunMode == ContentRunMode.SeedTest)
            {
                return true;
            }

            if (!int.TryParse(runInfo.StageId, out var stage) || stage <= 0 ||
                string.IsNullOrEmpty(definition.DungeonKeyItemId))
            {
                return false;
            }

            var view = progress.View;
            var highestClearedStage = view.GrowthDungeons.GetHighestClearedStage(definition.ContentId.Value);
            if (!GrowthDungeonStageRules.IsValidStage(stage))
            {
                return false;
            }

            if (runInfo.RunMode == ContentRunMode.Challenge)
            {
                return highestClearedStage < int.MaxValue &&
                       stage == highestClearedStage + 1; // 미클리어 다음 단계만 무료 도전
            }

            view.Items.TryGetQuantity(definition.DungeonKeyItemId, out var keyQuantity);
            return stage <= highestClearedStage && keyQuantity > 0L; // 파밍은 입장 시 한 개 예약
        }

        private bool TryValidateCastleRaidEntry(int stage, out ContentRunMode runMode)
        {
            runMode = default;
            var highestClearedStage = progress.View.CastleRaidHighestClearedStage;
            if (!CastleRaidStageRules.IsSelectable(stage, highestClearedStage))
            {
                return false;
            }

            runMode = CastleRaidStageRules.IsNewClear(stage, highestClearedStage)
                ? ContentRunMode.Challenge
                : ContentRunMode.Farming;
            return true;
        }

        private bool TryGetDefinition(ContentId contentId, ContentOpenMode expectedMode, out ContentDefinition definition)
        {
            definition = null;
            if (!contentId.IsValid || !catalog.TryGet(contentId, out definition))
            {
                Debug.LogError($"Content is not registered: {contentId}");
                return false;
            }

            if (definition.OpenMode != expectedMode)
            {
                Debug.LogError($"Content open mode mismatch. Content={contentId}, Expected={expectedMode}, Actual={definition.OpenMode}");
                return false;
            }

            return true;
        }

        private static bool TryCreateStartData(
            ContentDefinition definition,
            BattlePartySnapshot party,
            out IContentStartData startData)
        {
            startData = null;
            if (definition.StartDataFactory == null || party == null)
            {
                Debug.LogError($"Content start data factory or party is missing. Content={definition.ContentId}");
                return false;
            }

            startData = definition.StartDataFactory.Create(party);
            if (startData != null)
            {
                return true;
            }

            Debug.LogError($"Content start data could not be created. Content={definition.ContentId}");
            return false;
        }

        private async Task HandleExitAsync(ActiveRun run, ContentOutcome outcome, IContentResultData result)
        {
            if (run == null || !ReferenceEquals(activeRun, run) || Interlocked.Exchange(ref run.ExitAccepted, 1) != 0) // 늦거나 중복된 결과 무시
            {
                return;
            }

            Phase = ContentFlowPhase.Finishing;
            if (outcome == ContentOutcome.Cancel || run.Definition.ResultAdapter == null)
            {
                FinishRun(run);
                return;
            }

            var adapter = run.Definition.ResultAdapter;
            var runInfo = run.Context.RunInfo;
            if (outcome != ContentOutcome.Complete || !adapter.IsSuccessfulResult(result))
            {
                run.PendingResultPresentation = new ContentResultPresentation(
                    run.Definition.ContentId,
                    run.Definition.DisplayName,
                    ContentOutcome.Fail,
                    adapter.CreateResultSummary(result, runInfo, ContentOutcome.Fail),
                    null);
                await ShowResultAndFinishAsync(run);
                return;
            }

            var settlementView = progress.View; // 지급·표시에 같은 확정 전 상태 사용
            if (!adapter.TryCreateProgressChange(result, settlementView, runInfo, out var change))
            {
                Debug.LogError($"Content result was rejected. Content={run.Definition.ContentId}");
                FinishRun(run);
                return;
            }

            if (runInfo.RunMode != ContentRunMode.SeedTest &&
                !string.IsNullOrEmpty(run.Definition.DungeonKeyItemId))
            {
                if (!int.TryParse(runInfo.StageId, out var stage) ||
                    !change.TryAttachGrowthDungeonSettlement(
                        run.Definition.ContentId.Value,
                        stage,
                        runInfo.RunMode == ContentRunMode.Challenge,
                        runInfo.RunMode == ContentRunMode.Farming ? run.Definition.DungeonKeyItemId : null))
                {
                    Debug.LogError($"Growth dungeon settlement is invalid. Content={run.Definition.ContentId}");
                    FinishRun(run);
                    return;
                }
            }

            run.PendingChange = change;
            if (adapter.TryCreateRewardPresentation(
                    result,
                    settlementView,
                    runInfo,
                    itemCatalog,
                    out var presentation) &&
                presentation != null && !presentation.IsEmpty)
            {
                run.PendingPresentation = presentation; // 재시도 때 동일한 표시 요청 재사용
            }

            run.PendingResultPresentation = new ContentResultPresentation(
                run.Definition.ContentId,
                run.Definition.DisplayName,
                ContentOutcome.Complete,
                adapter.CreateResultSummary(result, runInfo, ContentOutcome.Complete),
                run.PendingPresentation);

            await TrySaveAndFinishAsync(run);
        }

        private async Task TrySaveAndFinishAsync(ActiveRun run)
        {
            if (run == null || !ReferenceEquals(activeRun, run) || Phase != ContentFlowPhase.Finishing ||
                run.PendingChange == null || Interlocked.Exchange(ref run.SettlementInFlight, 1) != 0)
            {
                return;
            }

            run.CanRetry = false;
            ShowSaving();

            var saved = false;
            try
            {
                saved = await progress.TryApplyAndSaveAsync(run.PendingChange);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                Interlocked.Exchange(ref run.SettlementInFlight, 0);
            }

            if (!ReferenceEquals(activeRun, run))
            {
                return;
            }

            if (!saved)
            {
                Debug.LogError($"Content progress could not be saved. Content={run.Definition.ContentId}");
                run.CanRetry = true;
                ShowSaveFailed(() => RetrySave(run));
                return;
            }

            HideFinishFeedback();
            if (run.PendingResultPresentation != null && resultView != null)
            {
                try
                {
                    await resultView.ShowAsync(run.PendingResultPresentation);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception); // 결과 표현 실패가 복귀를 막지 않음
                }

                if (!ReferenceEquals(activeRun, run))
                {
                    return;
                }
            }

            if (run.PendingPresentation != null)
            {
                try
                {
                    rewardPresentation?.PlayConfirmed(run.PendingPresentation); // 저장 성공 뒤에만 화면 연출 허용
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception); // 표현 실패가 콘텐츠 복귀를 막지 않음
                }
            }

            FinishRun(run);
        }

        private async Task ShowResultAndFinishAsync(ActiveRun run)
        {
            if (run == null || !ReferenceEquals(activeRun, run))
            {
                return;
            }

            HideFinishFeedback();
            if (run.PendingResultPresentation != null && resultView != null)
            {
                try
                {
                    await resultView.ShowAsync(run.PendingResultPresentation);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            if (ReferenceEquals(activeRun, run))
            {
                FinishRun(run);
            }
        }

        private void RetrySave(ActiveRun run)
        {
            if (run == null || !ReferenceEquals(activeRun, run) || Phase != ContentFlowPhase.Finishing || !run.CanRetry)
            {
                return;
            }

            run.CanRetry = false; // 연속 터치로 중복 저장 요청 금지
            _ = TrySaveAndFinishAsync(run);
        }

        private void FinishRun(ActiveRun run)
        {
            if (!ReferenceEquals(activeRun, run))
            {
                return;
            }

            HideFinishFeedback();
            if (run.Runner != null)
            {
                run.Runner.Close();
                NotifyHostedRunFinished(run.Definition.ContentId);
            }
            else
            {
                sceneNavigator.Load(mainBattleSceneId);
            }

            activeRun = null; // 종료·복귀 요청 뒤 실행 잠금 해제
            Phase = ContentFlowPhase.Idle;
        }

        private void NotifyHostedRunStarted(ContentId contentId)
        {
            try
            {
                HostedRunStarted?.Invoke(contentId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception); // BGM 등 부가 표현 오류가 콘텐츠 입장을 취소하지 않음
            }
        }

        private void NotifyHostedRunFinished(ContentId contentId)
        {
            try
            {
                HostedRunFinished?.Invoke(contentId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception); // 복귀 표현 오류가 실행 잠금 해제를 막지 않음
            }
        }

        private void ShowSaving()
        {
            try
            {
                finishFeedback.ShowSaving();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception); // 표시 오류가 저장을 막지 않음
            }
        }

        private void ShowSaveFailed(Action retry)
        {
            try
            {
                finishFeedback.ShowSaveFailed(retry);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void HideFinishFeedback()
        {
            try
            {
                finishFeedback.Hide();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private sealed class ActiveRun // 현재 한 판의 내부 상태
        {
            public ActiveRun(ContentDefinition definition, IHostedContentRunner runner, SceneId sceneId)
            {
                Definition = definition;
                Runner = runner;
                SceneId = sceneId;
            }

            public ContentDefinition Definition { get; }
            public IHostedContentRunner Runner { get; }
            public SceneId SceneId { get; }
            public ContentContext Context { get; set; }
            public GameProgressChange PendingChange { get; set; } // 저장 성공까지 보존할 동일 변경
            public RewardPresentationRequest PendingPresentation { get; set; } // 저장 성공 뒤 한 번 표시
            public ContentResultPresentation PendingResultPresentation { get; set; } // 저장 확정 뒤 닫힐 때까지 표시
            public int ExitAccepted; // 첫 결과 접수 표식
            public int SettlementInFlight; // 동시 저장 재시도 차단
            public bool CanRetry; // 실패 확인 뒤에만 재시도 허용
        }

        private sealed class ContentExitGate : IContentExit // 첫 종료 요청만 통과
        {
            private readonly Action<IContentResultData> complete;
            private readonly Action<IContentResultData> fail;
            private readonly Action cancel;
            private int requested; // 호출 측 중복 접수 차단

            public ContentExitGate(Action<IContentResultData> complete, Action<IContentResultData> fail, Action cancel)
            {
                this.complete = complete;
                this.fail = fail;
                this.cancel = cancel;
            }

            public void Complete(IContentResultData result)
            {
                if (Interlocked.Exchange(ref requested, 1) == 0)
                {
                    complete(result);
                }
            }

            public void Fail(IContentResultData result = null)
            {
                if (Interlocked.Exchange(ref requested, 1) == 0)
                {
                    fail(result);
                }
            }

            public void Cancel()
            {
                if (Interlocked.Exchange(ref requested, 1) == 0)
                {
                    cancel();
                }
            }
        }
    }
}
