using System;
using System.Threading;
using System.Threading.Tasks;
using ProjectMT.Core.SceneFlow;
using ProjectMT.Shared.GameData;
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

        private ActiveRun activeRun; // 동시에 한 판만 허용

        public ContentFlow(
            ContentCatalog catalog,
            IGameProgressService progress,
            ISceneNavigator sceneNavigator,
            SceneId mainBattleSceneId)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            this.sceneNavigator = sceneNavigator ?? throw new ArgumentNullException(nameof(sceneNavigator));
            this.mainBattleSceneId = mainBattleSceneId;
        }

        public bool IsRunning => activeRun != null;

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
            activeRun = run; // Open 전에 중복 입장 차단
            if (runner.Open(run.Context))
            {
                return true;
            }

            activeRun = null; // Hosted 열기 실패 복구
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

            return new ContentSceneContext(activeRun.Definition, activeRun.Context);
        }

        private ActiveRun CreateRun(ContentDefinition definition, IContentStartData startData, IHostedContentRunner runner)
        {
            var runInfo = new ContentRunInfo(definition.ContentId, "seed", ContentRunMode.SeedTest);
            var run = new ActiveRun(definition, runner);
            var exit = new ContentExitGate( // 현재 Run 전용 비공개 출구
                result => _ = HandleExitAsync(run, ContentOutcome.Complete, result),
                result => _ = HandleExitAsync(run, ContentOutcome.Fail, result),
                () => _ = HandleExitAsync(run, ContentOutcome.Cancel, null));
            run.Context = new ContentContext(runInfo, startData, exit);
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

            if (outcome == ContentOutcome.Complete && run.Definition.ResultAdapter != null)
            {
                if (!run.Definition.ResultAdapter.TryCreateProgressChange(result, out var change)) // 플레이 사실을 진행 변경으로 번역
                {
                    Debug.LogError($"Content result was rejected. Content={run.Definition.ContentId}");
                }
                else if (!await progress.TryApplyAndSaveAsync(change))
                {
                    Debug.LogError($"Content progress could not be saved. Content={run.Definition.ContentId}");
                }
            }

            if (!ReferenceEquals(activeRun, run))
            {
                return;
            }

            activeRun = null; // 복귀 전에 실행 잠금 해제
            if (run.Runner != null)
            {
                run.Runner.Close();
            }
            else
            {
                sceneNavigator.Load(mainBattleSceneId);
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
            public int ExitAccepted; // 첫 결과 접수 표식
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
