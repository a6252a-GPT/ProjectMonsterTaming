using UnityEditor;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander.Editor
{
    internal static class FallenCommanderPreviewEffectPlayer
    {
        private static GameObject previewAudioRoot;

        static FallenCommanderPreviewEffectPlayer()
        {
            AssemblyReloadEvents.beforeAssemblyReload += StopAudio;
            EditorApplication.quitting += StopAudio;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static bool PlayAudio(AudioClip clip, float volume)
        {
            if (clip == null)
            {
                return false;
            }

            StopAudio();

            previewAudioRoot = new GameObject("[FallenCommander Audio Preview]");
            previewAudioRoot.hideFlags = HideFlags.HideAndDontSave;

            if (Object.FindFirstObjectByType<AudioListener>() == null)
            {
                previewAudioRoot.AddComponent<AudioListener>();
            }

            var audioSource = previewAudioRoot.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.clip = clip;
            audioSource.volume = Mathf.Clamp01(volume);
            audioSource.spatialBlend = 0f;
            audioSource.Play();
            return true;
        }

        public static void StopAudio()
        {
            if (previewAudioRoot == null)
            {
                return;
            }

            var audioSource = previewAudioRoot.GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.Stop();
            }

            Object.DestroyImmediate(previewAudioRoot);
            previewAudioRoot = null;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            StopAudio();
        }
    }
}
