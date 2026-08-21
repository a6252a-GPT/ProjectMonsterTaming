using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectMT.Core.SceneFlow;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Reward;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Contents.Framework
{
    [Serializable]
    public struct ContentId : IEquatable<ContentId> // 콘텐츠 고정 식별자
    {
        [SerializeField] private string value; // 직렬화용 원본 문자열

        public ContentId(string value)
        {
            this.value = value == null ? string.Empty : value.Trim();
        }

        public string Value => value ?? string.Empty;
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(ContentId other)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(Value, other.Value); // 대소문자 무시 비교
        }

        public override bool Equals(object obj)
        {
            return obj is ContentId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(ContentId left, ContentId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ContentId left, ContentId right)
        {
            return !left.Equals(right);
        }
    }

    public enum ContentRunMode // 콘텐츠 실행 방식
    {
        Challenge,
        Farming,
        SeedTest
    }

    public enum ContentOutcome // 콘텐츠 종료 결과
    {
        Complete,
        Fail,
        Cancel
    }

    public enum ContentFlowPhase // 공통 콘텐츠 한 판의 수직 흐름
    {
        Idle,
        Entering,
        Playing,
        Finishing
    }

    public interface IContentStartData // 타입 시작 데이터 표식
    {
    }

    public interface IContentResultData // 타입 결과 데이터 표식
    {
    }

    public interface IContentExit // 결과를 한 번 돌려주는 출구
    {
        void Complete(IContentResultData result);
        void Fail(IContentResultData result = null);
        void Cancel();
    }

    public interface IContentController // 콘텐츠 실행 수명 계약
    {
        bool IsRunning { get; }
        void Initialize(ContentContext context);
        void Shutdown();
    }

    public interface IHostedContentRunner // MainBattle 내부 실행 계약
    {
        bool Open(ContentContext context);
        void Close();
    }

    public interface IContentLauncher // MainBattle에 공개한 입장 계약
    {
        bool IsRunning { get; }
        bool StartHosted(ContentId contentId, BattlePartySnapshot party, IHostedContentRunner runner);
        bool StartHosted(
            ContentId contentId,
            BattlePartySnapshot party,
            IHostedContentRunner runner,
            ContentRunMode runMode,
            int stage);
        bool StartSeparate(ContentId contentId, BattlePartySnapshot party);
        bool TryGetGrowthDungeonState(ContentId contentId, out GrowthDungeonEntryState state);
    }

    public interface IGrowthDungeonSweepService // RuntimePrefab을 열지 않는 1회 소탕 정산
    {
        bool IsBusy { get; }
        Task<bool> TrySweepAsync(ContentId contentId);
    }

    public interface IContentFinishFeedback // 저장 중·실패 재시도 공통 표시
    {
        void ShowSaving();
        void ShowSaveFailed(Action retry);
        void Hide();
    }

    public interface IContentResultView // 저장 확정 뒤 닫힐 때까지 대기하는 공통 결과창
    {
        Task ShowAsync(ContentResultPresentation presentation);
    }

    public sealed class ContentResultPresentation // 보상 계산 권한이 없는 결과 표시값
    {
        public ContentResultPresentation(
            ContentId contentId,
            string displayName,
            ContentOutcome outcome,
            string summary,
            RewardPresentationRequest rewards)
        {
            ContentId = contentId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? contentId.Value : displayName.Trim();
            Outcome = outcome;
            Summary = summary?.Trim() ?? string.Empty;
            Rewards = rewards ?? new RewardPresentationRequest();
        }

        public ContentId ContentId { get; }
        public string DisplayName { get; }
        public ContentOutcome Outcome { get; }
        public string Summary { get; }
        public RewardPresentationRequest Rewards { get; }
        public IReadOnlyList<RewardPresentationItem> RewardItems => Rewards.Items;
    }

    public readonly struct GrowthDungeonEntryState // 카드 표시·입장 검증용 Snapshot
    {
        public GrowthDungeonEntryState(
            ContentId contentId,
            string displayName,
            int highestClearedStage,
            long keyQuantity,
            bool supportsSweep)
        {
            ContentId = contentId;
            DisplayName = displayName ?? contentId.Value;
            HighestClearedStage = Math.Max(0, highestClearedStage);
            NextChallengeStage = GrowthDungeonStageRules.ResolveNextChallengeStage(HighestClearedStage);
            HasChallengeStage = HighestClearedStage < int.MaxValue;
            MaximumSelectableStage = NextChallengeStage;
            KeyQuantity = Math.Max(0L, keyQuantity);
            SupportsSweep = supportsSweep;
        }

        public ContentId ContentId { get; }
        public string DisplayName { get; }
        public int HighestClearedStage { get; }
        public int NextChallengeStage { get; }
        public int MaximumSelectableStage { get; }
        public bool HasChallengeStage { get; }
        public long KeyQuantity { get; }
        public bool SupportsSweep { get; }
        public bool CanSweep => SupportsSweep && HighestClearedStage > 0 && KeyQuantity > 0L;
    }

    public readonly struct ContentRunInfo // 한 판의 고정 식별 정보
    {
        public ContentRunInfo(ContentId contentId, string stageId, ContentRunMode runMode)
        {
            ContentId = contentId;
            StageId = stageId ?? string.Empty;
            RunMode = runMode;
        }

        public ContentId ContentId { get; }
        public string StageId { get; }
        public ContentRunMode RunMode { get; }
    }

    public sealed class ContentContext // 시작값과 출구를 담은 봉투
    {
        // 08.07 안건준 추가 - progress는 선택 값(옵션)이다. 기존 호출부(3개 인자)는 그대로 컴파일되며
        // Progress는 null이 되어 대부분의 콘텐츠 컨트롤러 동작에 영향이 없다. 진행 데이터를 읽어야 하는
        // 콘텐츠(예: 수호자의 탑 난이도 스케일링)만 선택적으로 사용한다.
        public ContentContext(ContentRunInfo runInfo, IContentStartData startData, IContentExit exit, IGameProgressService progress = null)
        {
            RunInfo = runInfo;
            StartData = startData ?? throw new ArgumentNullException(nameof(startData));
            Exit = exit ?? throw new ArgumentNullException(nameof(exit));
            Progress = progress;
        }

        public ContentRunInfo RunInfo { get; }
        public IContentStartData StartData { get; } // 콘텐츠별 읽기 전용 시작값
        public IContentExit Exit { get; } // 완료·실패·취소 반환 통로
        public IGameProgressService Progress { get; } // 08.07 안건준 추가 - 진행 데이터 읽기 전용 접근(선택적)
    }

    public sealed class ContentSceneContext : ISceneContext // 별도 씬 콘텐츠 권한 봉투
    {
        public ContentSceneContext(ContentDefinition definition, ContentContext contentContext)
        {
            Definition = definition;
            ContentContext = contentContext;
        }

        public ContentDefinition Definition { get; }
        public ContentContext ContentContext { get; }
    }
}
