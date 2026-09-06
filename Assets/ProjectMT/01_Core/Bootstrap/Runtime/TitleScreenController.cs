using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ProjectMT.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class TitleScreenController : MonoBehaviour // 영상 타이틀과 임시 로그인 진입
    {
        [SerializeField] private VideoPlayer titleVideo;
        [SerializeField] private GameObject loginDock;
        [SerializeField] private CanvasGroup loginDockGroup;
        [SerializeField] private Button screenTouchButton;
        [SerializeField] private GameObject loadingPlate;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text loadingStatusText;
        [SerializeField] private Button guestLoginButton;
        [SerializeField] private Button googleLoginButton;

        private Action guestLogin;
        private Action googleLogin;
        private Action continueSession;
        private bool bound;
        private Coroutine popupRoutine;

        private void Awake()
        {
            Bind();
            PrepareVideo();
        }

        public void Configure(Action onGuestLogin, Action onGoogleLogin, Action onContinueSession = null)
        {
            guestLogin = onGuestLogin;
            googleLogin = onGoogleLogin;
            continueSession = onContinueSession;
            Bind();
            PrepareVideo();
        }

        public void ShowTitle()
        {
            gameObject.SetActive(true);
            HideAccountPopup(false);
            screenTouchButton?.gameObject.SetActive(true);
            loadingPlate?.SetActive(false);
            PlayVideo();
        }

        public void ShowLoading(string message)
        {
            gameObject.SetActive(true);
            HideAccountPopup(false);
            screenTouchButton?.gameObject.SetActive(false);
            loadingPlate?.SetActive(true);
            ShowStatus(message);
            PlayVideo();
        }

        public void ShowStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message ?? string.Empty;
            }

            if (loadingStatusText != null)
            {
                loadingStatusText.text = message ?? string.Empty;
            }
        }

        public void Shutdown()
        {
            guestLogin = null;
            googleLogin = null;
            continueSession = null;
            if (popupRoutine != null)
            {
                StopCoroutine(popupRoutine);
                popupRoutine = null;
            }
            if (titleVideo != null)
            {
                titleVideo.Stop();
            }
        }

        private void Bind()
        {
            if (bound)
            {
                return;
            }

            bound = true;
            screenTouchButton?.onClick.AddListener(HandleScreenTouch);
            guestLoginButton?.onClick.AddListener(() => guestLogin?.Invoke());
            googleLoginButton?.onClick.AddListener(() => googleLogin?.Invoke());
        }

        private void HandleScreenTouch()
        {
            if (continueSession != null)
            {
                continueSession.Invoke();
                return;
            }

            if (loginDock != null && loginDock.activeSelf)
            {
                HideAccountPopup(true); // 팝업 바깥 영역 터치로 닫기
                return;
            }

            ShowAccountPopup();
        }

        private void ShowAccountPopup()
        {
            if (loginDock == null)
            {
                return;
            }

            if (popupRoutine != null)
            {
                StopCoroutine(popupRoutine);
            }

            loginDock.SetActive(true);
            ShowStatus("원하는 로그인 방식을 선택하세요.");
            popupRoutine = StartCoroutine(AnimateAccountPopup());
        }

        private void HideAccountPopup(bool keepTouchArea)
        {
            if (popupRoutine != null)
            {
                StopCoroutine(popupRoutine);
                popupRoutine = null;
            }

            loginDock?.SetActive(false);
            if (!keepTouchArea)
            {
                return;
            }

            screenTouchButton?.gameObject.SetActive(true);
        }

        private IEnumerator AnimateAccountPopup()
        {
            if (loginDockGroup == null)
            {
                popupRoutine = null;
                yield break;
            }

            loginDockGroup.alpha = 0f;
            loginDock.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
            var elapsed = 0f;
            const float duration = 0.18f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                loginDockGroup.alpha = eased;
                loginDock.transform.localScale = Vector3.LerpUnclamped(new Vector3(0.9f, 0.9f, 1f), Vector3.one, eased);
                yield return null;
            }

            loginDockGroup.alpha = 1f;
            loginDock.transform.localScale = Vector3.one;
            popupRoutine = null;
        }

        private void PrepareVideo()
        {
            if (titleVideo == null)
            {
                return;
            }

            titleVideo.isLooping = true;
            titleVideo.playOnAwake = true;
            titleVideo.audioOutputMode = VideoAudioOutputMode.None; // 타이틀 영상 내 음원은 사용하지 않음
            titleVideo.skipOnDrop = true;
        }

        private void PlayVideo()
        {
            if (titleVideo == null || titleVideo.clip == null || titleVideo.isPlaying)
            {
                return;
            }

            if (titleVideo.isPrepared)
            {
                titleVideo.Play();
                return;
            }

            titleVideo.Prepare();
            titleVideo.prepareCompleted -= HandlePrepared;
            titleVideo.prepareCompleted += HandlePrepared;
        }

        private static void HandlePrepared(VideoPlayer player)
        {
            player.prepareCompleted -= HandlePrepared;
            player.Play();
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            VideoPlayer video,
            GameObject dock,
            CanvasGroup dockGroup,
            Button touchButton,
            GameObject loading,
            TMP_Text status,
            TMP_Text loadingStatus,
            Button guest,
            Button google)
        {
            titleVideo = video;
            loginDock = dock;
            loginDockGroup = dockGroup;
            screenTouchButton = touchButton;
            loadingPlate = loading;
            statusText = status;
            loadingStatusText = loadingStatus;
            guestLoginButton = guest;
            googleLoginButton = google;
        }
#endif
    }
}
