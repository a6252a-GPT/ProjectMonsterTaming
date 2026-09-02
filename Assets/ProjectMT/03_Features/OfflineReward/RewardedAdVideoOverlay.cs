using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ProjectMT.Features.OfflineReward
{
    // 실제 광고 SDK 대신 로컬 영상 파일로 "광고 시청" 연출을 대신한다.
    // 0 ~ skipUnlockSeconds: 버튼이 잠겨있고 카운트다운만 보여준다(연속 클릭/즉시 이탈 방지용).
    // skipUnlockSeconds ~ rewardWatchSeconds: 버튼이 "SKIP"으로 열리며, 누르면 보상 없이 원래 팝업으로 돌아간다.
    // rewardWatchSeconds 이상: 버튼이 "보상받기"로 바뀌며, 누르면 즉시 2배 보상을 받고 패널이 닫힌다.
    // 영상이 마지막 프레임까지 재생되면, 시청 시간이 30초에 못 미쳐도 자동으로 2배 보상을 지급하고 팝업을 닫는다.
    [DisallowMultipleComponent]
    public sealed class RewardedAdVideoOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject displayRoot;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RawImage videoImage;
        [SerializeField] private Button skipButton;
        [SerializeField] private float skipUnlockSeconds = 10f;
        [SerializeField] private float rewardWatchSeconds = 30f;

        // Resources/이 폴더 아래에 놓인 VideoClip을 전부 후보로 삼아 매번 무작위로 하나를 재생한다.
        // 새 영상 파일을 이 폴더(Assets/ProjectMT/05_Art/Media/Resources/Video)에 추가하기만 하면
        // 별도 연결 작업 없이 자동으로 후보 목록에 들어온다.
        [SerializeField] private string resourcesFolder = "Video";

        private static readonly Color SkipTextLockedColor = new Color(100f / 255f, 100f / 255f, 100f / 255f, 1f);
        private static readonly Color SkipTextReadyColor = Color.white;
        private const string SkipLabel = "SKIP";
        private const string RewardLabel = "보상받기";

        private Action onWatchedFully;
        private Action onSkipped;
        private RenderTexture renderTexture;
        private Image backgroundImage;
        private TMP_Text skipButtonText;
        private VideoClip pendingPreloadClip;
        private VideoClip preparedClip;
        private Action<bool> preloadCompleted;
        private float elapsed;
        private bool playing;
        private bool preloading;
        private bool firstFrameReceived;
        private bool skipReady;
        private bool rewardReady;
        private int lastDisplayedCount;

        private GameObject DisplayRoot => displayRoot != null ? displayRoot : gameObject;

        private void Awake()
        {
            backgroundImage = GetComponent<Image>();
            skipButton?.onClick.RemoveListener(HandleSkipClicked);
            skipButton?.onClick.AddListener(HandleSkipClicked);
            if (skipButton != null)
            {
                skipButtonText = skipButton.GetComponentInChildren<TMP_Text>(true);
            }
            if (videoPlayer != null)
            {
                videoPlayer.playOnAwake = false;
            }
            // AdVideoOverlay 자기 자신이 DisplayRoot이기 때문에, 씬에서 이미 비활성 상태로 저장해두면 된다.
            // 여기서 SetActive(false)를 부르면 Play()가 처음 활성화하는 순간 Awake가 실행되며
            // 곧바로 다시 꺼버리는 자기 비활성화 루프가 생기므로 호출하지 않는다.
        }

        // 서로 해상도가 다른 영상을 번갈아 재생해도 이전 프레임이 남지 않도록
        // 선택된 클립 해상도에 맞춰 RenderTexture를 재생성한다.
        private void EnsureRenderTexture(VideoClip clip)
        {
            if (videoPlayer == null || clip == null)
            {
                return;
            }

            var width = Mathf.Max(16, (int)clip.width);
            var height = Mathf.Max(16, (int)clip.height);
            if (renderTexture != null && (renderTexture.width != width || renderTexture.height != height))
            {
                if (videoPlayer.targetTexture == renderTexture)
                {
                    videoPlayer.targetTexture = null;
                }

                renderTexture.Release();
                Destroy(renderTexture);
                renderTexture = null;
            }

            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(width, height, 0)
                {
                    name = "RewardedAdVideoOverlay_RT"
                };
            }

            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = renderTexture;
            if (videoImage != null)
            {
                videoImage.texture = renderTexture;
            }
        }

        private void ClearRenderTexture()
        {
            if (renderTexture == null)
            {
                return;
            }

            var previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previous;
        }

        private void OnDisable()
        {
            playing = false;
            preloading = false;
            pendingPreloadClip = null;
            preparedClip = null;
            preloadCompleted = null;
            UnsubscribeVideoEvents();
            StopPlayback();
            ResumeGame();
        }

        private void OnDestroy()
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                renderTexture = null;
            }
        }

        public void Play(Action watchedFullyCallback, Action skippedCallback)
        {
            // 팝업이 열릴 때 미리 Prepare()해 둔 영상이 있으면 그대로 사용한다.
            // 버튼을 누른 시점에 디코더 준비를 시작하면, 영상 대신 소리만 먼저 나오는 구간이 생길 수 있다.
            var selectedClip = preparedClip != null ? preparedClip :
                (pendingPreloadClip != null ? pendingPreloadClip : PickRandomClip());
            if (videoPlayer == null || selectedClip == null)
            {
                Debug.LogWarning("[RewardedAdVideoOverlay] VideoPlayer가 없거나 " + resourcesFolder + " 폴더에 재생 가능한 VideoClip이 없습니다.");
                skippedCallback?.Invoke();
                return;
            }

            var usePreparedClip = preparedClip == selectedClip &&
                                  videoPlayer.clip == selectedClip &&
                                  videoPlayer.isPrepared;

            onWatchedFully = watchedFullyCallback;
            onSkipped = skippedCallback;
            elapsed = 0f;
            playing = false; // 실제 디코딩이 시작되기 전까지는 시청 타이머를 돌리지 않는다.
            firstFrameReceived = false;
            skipReady = false;
            rewardReady = false;
            lastDisplayedCount = -1;

            // AdVideoOverlay는 씬에서 처음엔 비활성 상태라 Awake()가 아직 안 돌았을 수 있다.
            // Awake() 타이밍에 의존하지 않도록 여기서 한 번 더 직접 찾아둔다.
            if (skipButtonText == null && skipButton != null)
            {
                skipButtonText = skipButton.GetComponentInChildren<TMP_Text>(true);
            }
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }

            preloading = false;
            pendingPreloadClip = null;
            preloadCompleted = null;
            videoPlayer.prepareCompleted -= HandlePreloadCompleted;
            if (!usePreparedClip)
            {
                videoPlayer.Stop();
            }

            EnsureRenderTexture(selectedClip);
            ClearRenderTexture();
            HideVideoUiUntilFirstFrame();

            videoPlayer.clip = selectedClip;
            videoPlayer.isLooping = false;

            // Skip 버튼은 최소 시청 시간이 끝나기 전까지 잠겨있고 숫자 카운트다운을 보여준다.
            // 버튼 이미지의 잠금 색(100,100,100)은 Button의 Disabled Color로 처리되고,
            // 텍스트 색은 별도 Graphic이라 여기서 직접 맞춰준다.
            if (skipButton != null)
            {
                skipButton.interactable = false;
            }
            if (skipButtonText != null)
            {
                skipButtonText.color = SkipTextLockedColor;
                skipButtonText.text = Mathf.CeilToInt(skipUnlockSeconds).ToString();
            }

            PauseGame();
            DisplayRoot.SetActive(true);
            preparedClip = null;
            if (usePreparedClip)
            {
                SubscribePlaybackEvents();
                videoPlayer.frame = 0;
                videoPlayer.Play();
                return;
            }

            // GameObject를 막 활성화한 프레임에는 VideoPlayer가 아직 준비되지 않아
            // 곧바로 Prepare()를 호출하면 경고가 날 수 있어 한 프레임 미뤄서 호출한다.
            StartCoroutine(PrepareNextFrame());
        }

        // 방치 보상 팝업이 열리는 동안 다음 광고 영상 한 편을 미리 디코더에 준비한다.
        // 광고 버튼은 이 콜백으로 준비 완료가 된 뒤에만 열리므로, 클릭 뒤에 검은 화면을 기다리지 않는다.
        public void PreloadNextClip(Action<bool> completed)
        {
            if (completed != null)
            {
                preloadCompleted += completed;
            }

            if (videoPlayer == null)
            {
                CompletePreload(false);
                return;
            }

            if (preparedClip != null && videoPlayer.clip == preparedClip && videoPlayer.isPrepared)
            {
                CompletePreload(true);
                return;
            }

            var selectedClip = PickRandomClip();
            if (selectedClip == null)
            {
                CompletePreload(false);
                return;
            }

            preloading = true;
            pendingPreloadClip = selectedClip;
            preparedClip = null;
            DisplayRoot.SetActive(true);
            EnsureRenderTexture(selectedClip);
            ClearRenderTexture();
            HideVideoUiUntilFirstFrame();

            // 이전 광고 재생의 prepareCompleted 핸들러가 남아 있으면 Prepare 완료와 동시에
            // 재생이 시작될 수 있다. 사전 준비는 반드시 정지 상태를 유지해야 한다.
            UnsubscribeVideoEvents();
            videoPlayer.Stop();
            videoPlayer.clip = selectedClip;
            videoPlayer.isLooping = false;
            videoPlayer.prepareCompleted -= HandlePreloadCompleted;
            videoPlayer.prepareCompleted += HandlePreloadCompleted;
            videoPlayer.errorReceived -= HandleErrorReceived;
            videoPlayer.errorReceived += HandleErrorReceived;
            videoPlayer.Prepare();
        }

        // 광고 영상을 보는 동안 뒤에서 게임 진행 소리가 겹쳐 나오지 않도록 게임을 일시정지한다.
        // VideoPlayer의 Update Mode가 Unscaled Game Time이라 timeScale = 0이어도 영상 자체는 정상 재생된다.
        private void PauseGame()
        {
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        private void ResumeGame()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        // 이 오브젝트 전용으로 Video Player에 클립이 이미 지정돼 있으면(영상별로 오브젝트를 나눈 구성)
        // 그 클립을 그대로 쓴다. 지정된 클립이 없을 때만 Resources 폴더에서 무작위로 고른다.
        // Resources.LoadAll은 빌드에도 그대로 동작하므로, 폴더에 영상을 추가/삭제만 하면
        // 코드 수정이나 인스펙터 재연결 없이 다음 재생부터 바로 후보 목록에 반영된다.
        private VideoClip PickRandomClip()
        {
            if (videoPlayer != null && videoPlayer.clip != null)
            {
                return videoPlayer.clip;
            }

            if (string.IsNullOrEmpty(resourcesFolder))
            {
                return null;
            }

            var candidates = Resources.LoadAll<VideoClip>(resourcesFolder);
            if (candidates == null || candidates.Length == 0)
            {
                return null;
            }

            return candidates[UnityEngine.Random.Range(0, candidates.Length)];
        }

        private void HideVideoUiUntilFirstFrame()
        {
            // 이전 영상 프레임·검은 배경·Skip 버튼을 모두 숨겨서, 준비 중인 영역 자체가 보이지 않게 한다.
            if (videoImage != null)
            {
                videoImage.enabled = false;
            }
            if (backgroundImage != null)
            {
                backgroundImage.enabled = false;
            }
            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(false);
            }
        }

        private IEnumerator PrepareNextFrame()
        {
            yield return null;
            if (videoPlayer == null || !DisplayRoot.activeInHierarchy)
            {
                yield break;
            }

            SubscribePlaybackEvents();
            videoPlayer.Prepare();
        }

        private void SubscribePlaybackEvents()
        {
            videoPlayer.prepareCompleted -= HandlePrepareCompleted;
            videoPlayer.prepareCompleted += HandlePrepareCompleted;
            videoPlayer.frameReady -= HandleFrameReady;
            videoPlayer.frameReady += HandleFrameReady;
            videoPlayer.errorReceived -= HandleErrorReceived;
            videoPlayer.errorReceived += HandleErrorReceived;
            videoPlayer.loopPointReached -= HandleVideoEnded;
            videoPlayer.loopPointReached += HandleVideoEnded;
            videoPlayer.sendFrameReadyEvents = true;
        }

        private void HandlePreloadCompleted(VideoPlayer source)
        {
            if (source != videoPlayer || !preloading || source.clip != pendingPreloadClip)
            {
                return;
            }

            preparedClip = source.clip;
            CompletePreload(true);
        }

        private void CompletePreload(bool succeeded)
        {
            preloading = false;
            pendingPreloadClip = null;
            if (!succeeded)
            {
                preparedClip = null;
            }

            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted -= HandlePreloadCompleted;
                videoPlayer.errorReceived -= HandleErrorReceived;
            }

            var callback = preloadCompleted;
            preloadCompleted = null;
            callback?.Invoke(succeeded);
        }

        // Prepare 완료는 첫 프레임이 RenderTexture에 기록되기 전일 수 있다.
        // 실제 화면 표시와 시청 타이머 시작은 HandleFrameReady에서 처리한다.
        private void HandlePrepareCompleted(VideoPlayer source)
        {
            if (source != videoPlayer)
            {
                return;
            }

            source.frame = 0;
            source.Play();
        }

        private void HandleFrameReady(VideoPlayer source, long frame)
        {
            if (source != videoPlayer || firstFrameReceived)
            {
                return;
            }

            firstFrameReceived = true;
            if (backgroundImage != null)
            {
                backgroundImage.enabled = true;
            }
            if (videoImage != null)
            {
                videoImage.enabled = true;
            }
            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(true);
            }

            playing = true;
            elapsed = 0f;
        }

        private void HandleVideoEnded(VideoPlayer source)
        {
            if (source != videoPlayer || !playing || !DisplayRoot.activeSelf)
            {
                return;
            }

            Complete(true);
        }

        // 코덱 미지원(예: 일부 환경의 HEVC) 등으로 재생 자체가 실패하면 무한 대기 대신
        // Skip과 동일하게 처리해 팝업이 멈춰있지 않도록 한다.
        private void HandleErrorReceived(VideoPlayer source, string message)
        {
            if (preloading)
            {
                Debug.LogWarning("[RewardedAdVideoOverlay] 광고 영상 사전 준비 실패: " + message);
                CompletePreload(false);
                return;
            }

            Debug.LogError("[RewardedAdVideoOverlay] 영상 재생 실패: " + message);
            UnsubscribeVideoEvents();
            if (DisplayRoot.activeSelf)
            {
                Complete(false);
            }
        }

        private void UnsubscribeVideoEvents()
        {
            if (videoPlayer == null)
            {
                return;
            }

            videoPlayer.prepareCompleted -= HandlePrepareCompleted;
            videoPlayer.prepareCompleted -= HandlePreloadCompleted;
            videoPlayer.frameReady -= HandleFrameReady;
            videoPlayer.errorReceived -= HandleErrorReceived;
            videoPlayer.loopPointReached -= HandleVideoEnded;
        }

        private void Update()
        {
            if (!playing)
            {
                return;
            }

            elapsed += Time.unscaledDeltaTime;
            UpdateSkipButtonState();
        }

        private void UpdateSkipButtonState()
        {
            if (elapsed < skipUnlockSeconds)
            {
                var count = Mathf.CeilToInt(skipUnlockSeconds - elapsed);
                if (count != lastDisplayedCount)
                {
                    lastDisplayedCount = count;
                    if (skipButtonText != null)
                    {
                        skipButtonText.text = count.ToString();
                    }
                }

                return;
            }

            if (!skipReady)
            {
                skipReady = true;
                if (skipButton != null)
                {
                    skipButton.interactable = true;
                }
                if (skipButtonText != null)
                {
                    skipButtonText.color = SkipTextReadyColor;
                    skipButtonText.text = SkipLabel;
                }
            }

            if (!rewardReady && elapsed >= rewardWatchSeconds)
            {
                rewardReady = true;
                if (skipButtonText != null)
                {
                    skipButtonText.text = RewardLabel;
                }
            }

        }

        private void HandleSkipClicked()
        {
            if (!DisplayRoot.activeSelf || !skipReady)
            {
                return;
            }

            Complete(rewardReady);
        }

        private void Complete(bool watchedFully)
        {
            playing = false;
            UnsubscribeVideoEvents();
            StopPlayback();
            DisplayRoot.SetActive(false);

            var fullyCallback = onWatchedFully;
            var skippedCallback = onSkipped;
            onWatchedFully = null;
            onSkipped = null;

            if (watchedFully)
            {
                fullyCallback?.Invoke();
            }
            else
            {
                skippedCallback?.Invoke();
            }
        }

        private void StopPlayback()
        {
            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                videoPlayer.Stop();
            }
        }
    }
}
