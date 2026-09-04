using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.Audio
{
    public enum SfxPriority // 동시 재생 한도 초과 시 보존 우선순위
    {
        Low,
        Normal,
        High
    }

    [CreateAssetMenu(menuName = "ProjectMT/Audio/SFX Cue", fileName = "SfxCue")]
    public sealed class SfxCue : ScriptableObject // 클립 선택과 모바일 재생 제한 설정
    {
        [SerializeField] private AudioClip[] clips; // 같은 역할의 변형 클립
        [SerializeField] private Vector2 volumeRange = new Vector2(0.9f, 1f); // 재생 음량 범위
        [SerializeField] private Vector2 pitchRange = new Vector2(0.96f, 1.04f); // 반복감 완화 피치
        [SerializeField, Range(0f, 1f)] private float spatialBlend; // 0은 UI, 1은 월드 사운드
        [SerializeField, Min(0f)] private float duplicateCooldown = 0.04f; // 같은 Cue 연속 재생 제한
        [SerializeField] private SfxPriority priority = SfxPriority.Normal; // Voice 부족 시 우선순위
        [SerializeField, Min(0f)] private float startOffsetSeconds; // 원본 앞부분 건너뛰기
        [SerializeField, Min(0f)] private float endCutSeconds; // 원본 뒷부분 자르기

        public IReadOnlyList<AudioClip> Clips => clips;
        public Vector2 VolumeRange => volumeRange;
        public Vector2 PitchRange => pitchRange;
        public float SpatialBlend => spatialBlend;
        public float DuplicateCooldown => duplicateCooldown;
        public SfxPriority Priority => priority;
        public float StartOffsetSeconds => Mathf.Max(0f, startOffsetSeconds);
        public float EndCutSeconds => Mathf.Max(0f, endCutSeconds);
        public AudioClip PrimaryClip
        {
            get
            {
                if (clips == null) return null;
                for (var index = 0; index < clips.Length; index++)
                {
                    if (clips[index] != null) return clips[index];
                }
                return null;
            }
        }
        public bool HasPlayableClip
        {
            get
            {
                if (clips == null)
                {
                    return false;
                }

                for (var index = 0; index < clips.Length; index++)
                {
                    if (clips[index] != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool TrySelectClip(out AudioClip clip)
        {
            clip = null;
            if (clips == null || clips.Length == 0)
            {
                return false;
            }

            var startIndex = Random.Range(0, clips.Length);
            for (var offset = 0; offset < clips.Length; offset++)
            {
                clip = clips[(startIndex + offset) % clips.Length];
                if (clip != null)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryResolvePlaybackRange(
            AudioClip clip,
            out float startSeconds,
            out float durationSeconds)
        {
            startSeconds = 0f;
            durationSeconds = 0f;
            if (clip == null || clip.length <= 0f ||
                float.IsNaN(startOffsetSeconds) || float.IsInfinity(startOffsetSeconds) ||
                float.IsNaN(endCutSeconds) || float.IsInfinity(endCutSeconds))
            {
                return false;
            }

            startSeconds = Mathf.Max(0f, startOffsetSeconds);
            durationSeconds = clip.length - startSeconds - Mathf.Max(0f, endCutSeconds);
            return durationSeconds > 0.001f;
        }

        public float SelectVolume()
        {
            return Random.Range(Mathf.Min(volumeRange.x, volumeRange.y), Mathf.Max(volumeRange.x, volumeRange.y));
        }

        public float SelectPitch()
        {
            return Random.Range(Mathf.Min(pitchRange.x, pitchRange.y), Mathf.Max(pitchRange.x, pitchRange.y));
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            AudioClip[] sourceClips,
            Vector2 volume,
            Vector2 pitch,
            float blend,
            float cooldown,
            SfxPriority cuePriority,
            float playbackStartOffsetSeconds = 0f,
            float playbackEndCutSeconds = 0f)
        {
            clips = sourceClips;
            volumeRange = volume;
            pitchRange = pitch;
            spatialBlend = Mathf.Clamp01(blend);
            duplicateCooldown = Mathf.Max(0f, cooldown);
            priority = cuePriority;
            startOffsetSeconds = float.IsNaN(playbackStartOffsetSeconds) || float.IsInfinity(playbackStartOffsetSeconds)
                ? 0f : Mathf.Max(0f, playbackStartOffsetSeconds);
            endCutSeconds = float.IsNaN(playbackEndCutSeconds) || float.IsInfinity(playbackEndCutSeconds)
                ? 0f : Mathf.Max(0f, playbackEndCutSeconds);
        }
#endif
    }
}
