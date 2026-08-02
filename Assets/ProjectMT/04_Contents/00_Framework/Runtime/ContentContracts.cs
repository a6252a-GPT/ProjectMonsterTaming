using System;
using ProjectMT.Core.SceneFlow;
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
        bool StartSeparate(ContentId contentId, BattlePartySnapshot party);
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
        public ContentContext(ContentRunInfo runInfo, IContentStartData startData, IContentExit exit)
        {
            RunInfo = runInfo;
            StartData = startData ?? throw new ArgumentNullException(nameof(startData));
            Exit = exit ?? throw new ArgumentNullException(nameof(exit));
        }

        public ContentRunInfo RunInfo { get; }
        public IContentStartData StartData { get; } // 콘텐츠별 읽기 전용 시작값
        public IContentExit Exit { get; } // 완료·실패·취소 반환 통로
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
