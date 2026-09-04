using System;
using ProjectMT.Contents.Framework;
using ProjectMT.Features.Settings;
using ProjectMT.Shared.Audio;
using UnityEngine;
using UnityEngine.Audio;

namespace ProjectMT.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class GlobalAudioController : MonoBehaviour // Mixer 채널과 로컬 설정 연결
    {
        [Serializable]
        private sealed class ContentBgmBinding // Hosted 콘텐츠별 전역 BGM 교체·중지 규칙
        {
            [SerializeField] private string contentId;
            [SerializeField] private AudioClip clip; // null이면 콘텐츠 로컬 BGM을 위해 전역 BGM만 중지

            public string ContentId => contentId;
            public AudioClip Clip => clip;
        }

        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private string bgmVolumeParameter = "BGMVolume";
        [SerializeField] private string sfxVolumeParameter = "SFXVolume";

        [Header("BGM Routing")]
        [SerializeField] private AudioClip entryBgm;
        [SerializeField] private AudioClip castleRaidBgm;
        [SerializeField] private AudioClip[] mainBattleBgms = Array.Empty<AudioClip>();
        [SerializeField] private ContentBgmBinding[] contentBgmBindings = Array.Empty<ContentBgmBinding>();

        public AudioClip CurrentBgm => bgmSource == null ? null : bgmSource.clip;

        private void OnEnable()
        {
            AudioRuntimeSettings.Changed += ApplyMixerVolumes;
            var settings = LocalSettingsStore.Load();
            AudioRuntimeSettings.Apply(settings.bgmVolume, settings.sfxVolume, settings.vibrationEnabled);
            ApplyMixerVolumes();
        }

        private void OnDisable()
        {
            AudioRuntimeSettings.Changed -= ApplyMixerVolumes;
        }

        public void PlayBgm(AudioClip clip, bool loop = true)
        {
            if (bgmSource == null || clip == null)
            {
                return;
            }

            if (bgmSource.clip == clip && bgmSource.loop == loop && bgmSource.isPlaying)
            {
                return; // 같은 화면 재초기화에서 곡을 처음부터 다시 시작하지 않음
            }

            bgmSource.clip = clip;
            bgmSource.loop = loop;
            bgmSource.Play();
        }

        public void PlayEntryBgm()
        {
            PlayBgm(entryBgm);
        }

        public void PlayMainBattleBgm()
        {
            PlayRandomBgm(mainBattleBgms);
        }

        public void PlayCastleRaidBgm()
        {
            PlayBgm(castleRaidBgm);
        }

        public bool ApplyHostedContentBgm(ContentId contentId)
        {
            if (!contentId.IsValid || contentBgmBindings == null)
            {
                return false;
            }

            for (var index = 0; index < contentBgmBindings.Length; index++)
            {
                var binding = contentBgmBindings[index];
                if (binding == null || !string.Equals(binding.ContentId, contentId.Value, StringComparison.Ordinal))
                {
                    continue;
                }

                if (binding.Clip == null)
                {
                    StopBgm(); // 보물 던전처럼 콘텐츠 Runtime이 자체 BGM을 재생하는 경우
                }
                else
                {
                    PlayBgm(binding.Clip);
                }

                return true;
            }

            return false;
        }

        private void PlayRandomBgm(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
            {
                return;
            }

            var startIndex = UnityEngine.Random.Range(0, clips.Length);
            for (var offset = 0; offset < clips.Length; offset++)
            {
                var clip = clips[(startIndex + offset) % clips.Length];
                if (clip != null)
                {
                    PlayBgm(clip);
                    return;
                }
            }
        }

        public void StopBgm()
        {
            bgmSource?.Stop();
        }

        private void ApplyMixerVolumes()
        {
            if (audioMixer == null)
            {
                if (bgmSource != null)
                {
                    bgmSource.volume = AudioRuntimeSettings.BgmVolume;
                }

                return;
            }

            audioMixer.SetFloat(bgmVolumeParameter, AudioRuntimeSettings.ToDecibels(AudioRuntimeSettings.BgmVolume));
            audioMixer.SetFloat(sfxVolumeParameter, AudioRuntimeSettings.ToDecibels(AudioRuntimeSettings.SfxVolume));
        }

#if UNITY_EDITOR
        public void EditorConfigure(AudioMixer mixer, AudioSource bgm)
        {
            audioMixer = mixer;
            bgmSource = bgm;
        }
#endif
    }
}
