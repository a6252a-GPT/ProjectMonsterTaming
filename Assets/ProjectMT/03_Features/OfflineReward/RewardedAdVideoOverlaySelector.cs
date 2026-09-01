using System;
using UnityEngine;

namespace ProjectMT.Features.OfflineReward
{
    // 영상별로 고정 클립을 가진 AdVideoOverlay_1..N 중 하나를 무작위로 골라 재생을 위임한다.
    // Preload 단계에서 고른 오브젝트를 Play까지 그대로 사용해, 미리 준비해 둔 영상과
    // 실제로 재생하는 영상이 서로 어긋나지 않게 한다.
    [DisallowMultipleComponent]
    public sealed class RewardedAdVideoOverlaySelector : MonoBehaviour
    {
        [SerializeField] private RewardedAdVideoOverlay[] overlays;

        private RewardedAdVideoOverlay selected;

        public void PreloadNextClip(Action<bool> completed)
        {
            var candidate = PickOverlay();
            if (candidate == null)
            {
                Debug.LogWarning("[RewardedAdVideoOverlaySelector] 연결된 AdVideoOverlay가 없습니다.");
                completed?.Invoke(false);
                return;
            }

            selected = candidate;
            selected.PreloadNextClip(completed);
        }

        public void Play(Action watchedFullyCallback, Action skippedCallback)
        {
            // Preload에서 고른 오브젝트가 있으면 그대로 쓰고, 없으면(프리로드 없이 바로 눌린 경우) 새로 고른다.
            var overlay = selected != null ? selected : PickOverlay();
            selected = null;
            if (overlay == null)
            {
                Debug.LogWarning("[RewardedAdVideoOverlaySelector] 연결된 AdVideoOverlay가 없습니다.");
                skippedCallback?.Invoke();
                return;
            }

            overlay.Play(watchedFullyCallback, skippedCallback);
        }

        private RewardedAdVideoOverlay PickOverlay()
        {
            if (overlays == null || overlays.Length == 0)
            {
                return null;
            }

            var validCount = 0;
            for (var i = 0; i < overlays.Length; i++)
            {
                if (overlays[i] != null)
                {
                    validCount++;
                }
            }

            if (validCount == 0)
            {
                return null;
            }

            var index = UnityEngine.Random.Range(0, validCount);
            var seen = 0;
            foreach (var overlay in overlays)
            {
                if (overlay == null)
                {
                    continue;
                }
                if (seen == index)
                {
                    return overlay;
                }
                seen++;
            }

            return null;
        }
    }
}
