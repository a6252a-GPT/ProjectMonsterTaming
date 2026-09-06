using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Video;

namespace ProjectMT.Shared.UI
{
    // 콘텐츠가 UI 구현 어셈블리에 의존하지 않고 공통 영상 연출을 사용한다.
    public interface ISkippableVideoOverlay
    {
        bool IsPlaying { get; }
        bool IsScreenCovered { get; }
        Task<bool> PlayAsync(VideoClip clip, AudioClip audioClip);
        void Cancel();
    }
}
