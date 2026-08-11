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
                finishFeedback)
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
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            this.sceneNavigator = sceneNavigator ?? throw new ArgumentNullException(nameof(sceneNavigator));
            this.mainBattleSceneId = mainBattleSceneId;
            this.itemCatalog = itemCatalog;
            this.rewardPresentation = rewardPresentation;
            this.finishFeedback = finishFeedback ?? throw new ArgumentNullException(nameof(finishFeedback));
        }

        public bool IsRunning => activeRun != null;
        public ContentFlowPhase Phase { get; private set; } = ContentFlowPhase.Idle;

        public bool StartHosted(ContentId contentId, BattlePartySnapshot party, IHostedContentRunner runner)
        {
            if (IsRunning || runner == null || !TryGetDefinition(contentId, ContentOpenMode.MainBattleHosted, out var definition))
            {
                return false;
            }

            if (!TryCreateStartData(definition, party, out var startData))
            {
                return false;
            }

            var run = CreateRun(definition, startData, runner);
            Phase = ContentFlowPhase.Entering;
            activeRun = run; // Open 전에 중복 입장 차단
            if (runner.Open(run.Context))
            {
                Phase = ContentFlowPhase.Playing;
                return true;
            }

            activeRun = null; // Hosted 열기 실패 복구
            Phase = ContentFlowPhase.Idle;
            return false;
        }

        public bool StartSeparate(ContentId contentId, BattlePartySnapshot party)
        {
            if (IsRunning || !TryGetDefinition(contentId, ContentOpenMode.SeparateScene, out var definition))
            {
                return false;
            }

            if (!definition.SceneId.IsValid)
            {
                Debug.LogError($"Separate content has no SceneId. Content={contentId}");
                return false;
            }

            if (!TryCreateStartData(definition, party, out var startData))
            {
                return false;
            }

            Phase = ContentFlowPhase.Entering;
            activeRun = CreateRun(definition, startData, null); // 씬 이동 전 실행 등록
            sceneNavigator.Load(definition.SceneId);
            return true;
        }

        public ContentSceneContext CreateSeparateSceneContext(SceneId sceneId)
        {
            if (activeRun == null || activeRun.Definition.OpenMode != ContentOpenMode.SeparateScene || activeRun.Definition.SceneId != sceneId)
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
                activeRun.Definition.SceneId != sceneId)
            {
                return false;
            }

            activeRun = null;
            Phase = ContentFlowPhase.Idle;
            HideFinishFeedback();
            return true;
        }

        private ActiveRun CreateRun(ContentDefinition definition, IContentStartData startData, IHostedContentRunner runner)
        {
            var runInfo = new ContentRunInfo(definition.ContentId, "seed", ContentRunMode.SeedTest);
            var run = new ActiveRun(definition, runner);
            var exit = new ContentExitGate( // 현재 Run 전용 비공개 출구
                result => _ = HandleExitAsync(run, ContentOutcome.Complete, result),
                result => _ = HandleExitAsync(run, ContentOutcome.Fail, result),
                () => _ = HandleExitAsync(run, ContentOutcome.Cancel, null));
            run.Context = new ContentContext(runInfo, startData, exit, progress); // 08.07 안건준 추가 - Progress 읽기 전용 전달
            return run;
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
            if (outcome != ContentOutcome.Complete || run.Definition.ResultAdapter == null)
            {
                FinishRun(run);
                return;
            }

            var settlementView = progress.View; // 지급·표시에 같은 확정 전 상태 사용
            if (!run.Definition.ResultAdapter.TryCreateProgressChange(result, settlementView, out var change))
            {
                Debug.LogError($"Content result was rejected. Content={run.Definition.ContentId}");
                FinishRun(run);
                return;
            }

            run.PendingChange = change;
            if (run.Definition.ResultAdapter.TryCreateRewardPresentation(
                    result,
                    settlementView,
                    itemCatalog,
                    out var presentation) &&
                presentation != null && !presentation.IsEmpty)
            {
                run.PendingPresentation = presentation; // 재시도 때 동일한 표시 요청 재사용
            }

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
            }
            else
            {
                sceneNavigator.Load(mainBattleSceneId);
            }

            activeRun = null; // 종료·복귀 요청 뒤 실행 잠금 해제
            Phase = ContentFlowPhase.Idle;
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
            public ActiveRun(ContentDefinition definition, IHostedContentRunner runner)
            {
                Definition = definition;
                Runner = runner;
            }

            public ContentDefinition Definition { get; }
            public IHostedContentRunner Runner { get; }
            public ContentContext Context { get; set; }
            public GameProgressChange PendingChange { get; set; } // 저장 성공까지 보존할 동일 변경
            public RewardPresentationRequest PendingPresentation { get; set; } // 저장 성공 뒤 한 번 표시
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
