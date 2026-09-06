using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace ProjectMT.Shared.Audio
{
    [DisallowMultipleComponent]
    public sealed class SfxPool : MonoBehaviour // 활성 연출 영역이 소유하는 AudioSource Voice 풀
    {
        private const float MaximumCueVolume = 2f;
        [SerializeField, Min(1)] private int maxVoices = 12; // 모바일 동시 재생 상한
        [SerializeField, Min(0)] private int prewarmVoices = 6; // 첫 재생 스파이크 완화
        [SerializeField] private AudioMixerGroup outputGroup; // 후속 설정 음량 연결점

        private readonly List<Voice> voices = new List<Voice>(); // 재사용 AudioSource 목록
        private readonly Dictionary<int, double> nextAllowedTime = new Dictionary<int, double>(); // Cue별 쿨다운
        private uint playSequence; // 같은 우선순위에서 오래된 Voice 판정

        public int VoiceCount => voices.Count;
        public int MaxVoices => maxVoices;

        private void Awake()
        {
            EnsurePrewarmed();
        }

        private void OnDisable()
        {
            StopAll();
        }

        public bool Play(SfxCue cue, Vector3 position)
        {
            if (cue == null || !cue.TrySelectClip(out var clip))
            {
                return false;
            }

            if (!cue.TryResolvePlaybackRange(clip, out var startSeconds, out var durationSeconds))
            {
                return false;
            }

            var now = AudioSettings.dspTime;
            var cooldownKey = cue.GetInstanceID();
            if (nextAllowedTime.TryGetValue(cooldownKey, out var allowedAt) && now < allowedAt)
            {
                return false;
            }

            var played = PlayClip(
                clip,
                position,
                cue.SelectVolume(),
                cue.SelectPitch(),
                cue.SpatialBlend,
                cue.Priority,
                startSeconds,
                durationSeconds);
            if (played && cue.DuplicateCooldown > 0f)
            {
                nextAllowedTime[cooldownKey] = now + cue.DuplicateCooldown;
            }

            return played;
        }

        public bool PlayClip(
            AudioClip clip,
            Vector3 position,
            float volume = 1f,
            float pitch = 1f,
            float spatialBlend = 0f,
            SfxPriority priority = SfxPriority.Normal,
            float startOffsetSeconds = 0f,
            float playbackDurationSeconds = 0f)
        {
            if (clip == null || !isActiveAndEnabled)
            {
                return false;
            }

            var start = Mathf.Clamp(startOffsetSeconds, 0f, clip.length);
            var availableDuration = clip.length - start;
            var duration = playbackDurationSeconds > 0f
                ? Mathf.Min(playbackDurationSeconds, availableDuration)
                : availableDuration;
            if (duration <= 0.001f)
            {
                return false;
            }

            EnsurePrewarmed();
            var now = AudioSettings.dspTime;
            var voice = FindVoice(now, priority);
            if (voice == null)
            {
                return false; // 낮은 우선순위 요청은 기존 중요음을 끊지 않음
            }

            var source = voice.Source;
            var resolvedVolume = float.IsNaN(volume) || float.IsInfinity(volume)
                ? 1f
                : Mathf.Clamp(volume, 0f, MaximumCueVolume);
            source.Stop();
            source.transform.position = position;
            source.clip = clip;
            source.volume = Mathf.Min(1f, resolvedVolume) *
                            (outputGroup == null ? AudioRuntimeSettings.SfxOutputGain : 1f);
            voice.Gain.Gain = Mathf.Max(1f, resolvedVolume);
            source.pitch = Mathf.Clamp(pitch, -3f, 3f);
            if (Mathf.Abs(source.pitch) < 0.01f)
            {
                source.pitch = 0.01f;
            }

            source.spatialBlend = Mathf.Clamp01(spatialBlend);
            source.outputAudioMixerGroup = outputGroup;
            if (start > 0f)
            {
                source.time = start;
            }
            source.Play();

            var finishAt = now + duration / Mathf.Abs(source.pitch);
            if (duration < availableDuration - 0.001f)
            {
                source.SetScheduledEndTime(finishAt);
            }
            voice.Priority = priority;
            voice.Sequence = ++playSequence;
            voice.FinishTime = finishAt;
            return true;
        }

        public void StopAll()
        {
            for (var i = 0; i < voices.Count; i++)
            {
                var source = voices[i]?.Source;
                if (source == null)
                {
                    continue;
                }

                source.Stop();
                source.clip = null;
                voices[i].Gain.Gain = 1f;
                voices[i].FinishTime = 0d;
                voices[i].Priority = SfxPriority.Low;
            }

            nextAllowedTime.Clear();
        }

        private void EnsurePrewarmed()
        {
            maxVoices = Mathf.Max(1, maxVoices);
            var targetCount = Mathf.Clamp(prewarmVoices, 0, maxVoices);
            while (voices.Count < targetCount)
            {
                voices.Add(CreateVoice(voices.Count));
            }
        }

        private Voice FindVoice(double now, SfxPriority requestedPriority)
        {
            for (var i = 0; i < voices.Count; i++)
            {
                var voice = voices[i];
                if (voice.Source == null || !voice.Source.isPlaying || now >= voice.FinishTime)
                {
                    return voice;
                }
            }

            if (voices.Count < maxVoices)
            {
                var created = CreateVoice(voices.Count);
                voices.Add(created);
                return created;
            }

            Voice candidate = null;
            for (var i = 0; i < voices.Count; i++)
            {
                var voice = voices[i];
                if (voice.Priority > requestedPriority)
                {
                    continue;
                }

                if (candidate == null || voice.Priority < candidate.Priority ||
                    voice.Priority == candidate.Priority && voice.Sequence < candidate.Sequence)
                {
                    candidate = voice; // 낮은 우선순위·오래된 재생부터 교체
                }
            }

            return candidate;
        }

        private Voice CreateVoice(int index)
        {
            var voiceObject = new GameObject($"SFX Voice {index + 1:00}"); // 풀 내부 Voice만 의도적으로 동적 생성
            voiceObject.transform.SetParent(transform, false);
            var source = voiceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = 24f;
            source.outputAudioMixerGroup = outputGroup;
            return new Voice(source, voiceObject.AddComponent<SfxVoiceGain>());
        }

#if UNITY_EDITOR
        public void EditorConfigure(int maximumVoices, int prewarm, AudioMixerGroup mixerGroup = null)
        {
            maxVoices = Mathf.Max(1, maximumVoices);
            prewarmVoices = Mathf.Clamp(prewarm, 0, maxVoices);
            outputGroup = mixerGroup;
        }
#endif

        private sealed class Voice
        {
            public Voice(AudioSource source, SfxVoiceGain gain)
            {
                Source = source;
                Gain = gain;
            }

            public AudioSource Source { get; }
            public SfxVoiceGain Gain { get; }
            public SfxPriority Priority { get; set; }
            public double FinishTime { get; set; }
            public uint Sequence { get; set; }
        }
    }

    internal sealed class SfxVoiceGain : MonoBehaviour // 100% 초과 Cue만 오디오 스레드에서 증폭
    {
        private volatile float gain = 1f;

        public float Gain
        {
            get => gain;
            set => gain = float.IsNaN(value) || float.IsInfinity(value)
                ? 1f
                : Mathf.Clamp(value, 1f, 2f);
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            var multiplier = gain;
            if (data == null || multiplier <= 1.0001f)
            {
                return;
            }

            for (var index = 0; index < data.Length; index++)
            {
                data[index] *= multiplier;
            }
        }
    }
}
