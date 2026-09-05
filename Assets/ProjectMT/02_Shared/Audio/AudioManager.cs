using System;
using ProjectMT.Shared.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectMT.Shared.Audio
{

    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {    
        private const float RescanInterval = 1.5f;

        [Header("클릭/팝업 효과음")]
        [SerializeField, HideInInspector] private AudioClip[] buttonClickClips = Array.Empty<AudioClip>();
        [SerializeField, HideInInspector] private AudioClip[] popupOpenClips = Array.Empty<AudioClip>();
        [SerializeField, HideInInspector] private AudioClip[] popupCloseClips = Array.Empty<AudioClip>();

        [Header("음량")]
        [SerializeField, HideInInspector, Range(0f, 1f)] private float buttonClickVolume = 1f;
        [SerializeField, HideInInspector, Range(0f, 1f)] private float popupOpenVolume = 1f;
        [SerializeField, HideInInspector, Range(0f, 1f)] private float popupCloseVolume = 1f;

        [Header("토글 체크 시 해당 소리 음소거")]
        [SerializeField, HideInInspector] private bool muteButtonClick;
        [SerializeField, HideInInspector] private bool mutePopupOpen;
        [SerializeField, HideInInspector] private bool mutePopupClose;

        [Header("오디오소스")]
        [SerializeField] private AudioSource sfxSource;

        public static AudioManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (transform.parent != null)
            {
                transform.SetParent(null, true);
            }

            DontDestroyOnLoad(gameObject);
            EnsureSfxSource();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            RescanButtons();
            InvokeRepeating(nameof(RescanButtons), RescanInterval, RescanInterval);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RescanButtons();
        }

        private void RescanButtons()
        {
            UIButtonClickSound.ApplyToAllButtonsInScene();
            SfxControlSound.ApplyToScene();
        }

        public static void PlayButtonClick()
        {
            if (SfxEvents.TryResolve(SfxEvents.Button, out _)) { SfxEvents.Play2D(SfxEvents.Button); return; }
            if (Instance != null && !Instance.muteButtonClick)
            {
                Instance.PlayManagedOrLegacy(SfxEvents.Button, Instance.buttonClickClips, Instance.buttonClickVolume);
            }
        }

        public static void PlayPopupOpen()
        {
            if (SfxEvents.TryResolve(SfxEvents.Open, out _)) { SfxEvents.Play2D(SfxEvents.Open); return; }
            if (Instance != null && !Instance.mutePopupOpen)
            {
                Instance.PlayManagedOrLegacy(SfxEvents.Open, Instance.popupOpenClips, Instance.popupOpenVolume);
            }
        }

        public static void PlayPopupClose()
        {
            if (SfxEvents.TryResolve(SfxEvents.Close, out _)) { SfxEvents.Play2D(SfxEvents.Close); return; }
            if (Instance != null && !Instance.mutePopupClose)
            {
                Instance.PlayManagedOrLegacy(SfxEvents.Close, Instance.popupCloseClips, Instance.popupCloseVolume);
            }
        }

        private void PlayManagedOrLegacy(string eventId, AudioClip[] clips, float volume)
        {
            if (SfxEvents.TryResolve(eventId, out _))
            {
                SfxEvents.Play2D(eventId); // 기존 버튼 부착·팝업 호출·음소거는 유지
                return;
            }
            PlayOneOf(clips, volume);
        }

        private void PlayOneOf(AudioClip[] clips, float volume)
        {
            if (clips == null || clips.Length == 0)
            {
                return;
            }

            var clip = clips.Length == 1 ? clips[0] : clips[UnityEngine.Random.Range(0, clips.Length)];
            if (clip == null)
            {
                return;
            }

            EnsureSfxSource();
            sfxSource.PlayOneShot(clip, volume * AudioRuntimeSettings.SfxVolume);
        }

        private void EnsureSfxSource()
        {
            if (sfxSource != null)
            {
                return;
            }

            sfxSource = GetComponent<AudioSource>();
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
            }

            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f; // UI 효과음은 항상 2D
        }
    }
}
