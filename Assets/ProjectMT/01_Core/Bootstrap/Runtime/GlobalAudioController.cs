using ProjectMT.Features.Settings;
using ProjectMT.Shared.Audio;
using UnityEngine;
using UnityEngine.Audio;

namespace ProjectMT.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class GlobalAudioController : MonoBehaviour // Mixer 채널과 로컬 설정 연결
    {
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private string bgmVolumeParameter = "BGMVolume";
        [SerializeField] private string sfxVolumeParameter = "SFXVolume";

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

            bgmSource.clip = clip;
            bgmSource.loop = loop;
            bgmSource.Play();
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
