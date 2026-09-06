using System.Collections;
using System.Threading.Tasks;
using ProjectMT.Shared.Audio;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class GachaSummonVideoOverlay : MonoBehaviour // 저장된 소환 결과 앞에 재생하는 영상
    {
        [SerializeField] private VideoClip singleClip;
        [SerializeField] private VideoClip tenClip;
        [SerializeField] private VideoPlayer player;
        [SerializeField] private RawImage display;
        [SerializeField] private Button skipButton;
        [SerializeField] private AudioSource videoAudio;
        [SerializeField] private CanvasGroup overlayGroup;
        [SerializeField] private CanvasGroup videoGroup;
        private bool finishRequested;
        private TaskCompletionSource<bool> completion;
        private RenderTexture target;
        private Coroutine watchdog;
        private bool ownsAudioPause;
        private bool previousAudioPause;
        private bool bound;

        public bool IsPlaying => completion != null;

        private void Awake() => Bind();

        private void Bind()
        {
            if (bound || skipButton == null || player == null) return;
            bound = true;
            skipButton.onClick.AddListener(Skip);
            player.loopPointReached += OnEnded;
            player.errorReceived += OnError;
            player.frameReady += OnFrame;
        }

        public Task<bool> PlayAsync(int count)
        {
            return PlayAsync(count == 10 ? tenClip : singleClip,
                Resources.Load<AudioClip>(count == 10 ? "GachaVideo/Summon_Ten" : "GachaVideo/Summon_Single"));
        }

        public Task<bool> PlayAsync(VideoClip clip, AudioClip audioClip)
        {
            Cancel();
            if (clip == null || player == null || display == null || skipButton == null || videoAudio == null || overlayGroup == null || videoGroup == null)
                return Task.FromResult(true); // 영상 문제로 이미 저장된 결과를 숨기지 않음

            gameObject.SetActive(true);
            finishRequested = false;
            overlayGroup.alpha = 0f;
            videoGroup.alpha = 0f;
            Bind();
            completion = new TaskCompletionSource<bool>();
            var task = completion.Task;
            try
            {
                previousAudioPause = AudioListener.pause;
                ownsAudioPause = true;
                AudioListener.pause = true; // 전투·배경 소리는 영상 종료까지 일시 정지
                videoAudio.ignoreListenerPause = true;
                videoAudio.clip = audioClip;
                ApplyVolume();

                AudioRuntimeSettings.Changed += ApplyVolume;
                target = new RenderTexture((int)clip.width, (int)clip.height, 0, RenderTextureFormat.ARGB32);
                target.Create();
                display.texture = target;
                display.enabled = false;
                player.playOnAwake = false;
                player.isLooping = false;
                player.skipOnDrop = true;
                player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                player.renderMode = VideoRenderMode.RenderTexture;
                player.targetTexture = target;
                player.clip = clip;
                player.sendFrameReadyEvents = true;
                player.audioOutputMode = VideoAudioOutputMode.None;


                ApplyVolume();
                skipButton.interactable = true;
                player.Prepare();
                watchdog = StartCoroutine(WatchPlayback(clip.length));
                }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[GachaSummonVideo] 영상 초기화 실패: {exception.Message}");
                Complete(true);
            }
            return task;
        }

        private void ApplyVolume()
        {
            if (videoAudio != null) videoAudio.volume = AudioRuntimeSettings.SfxVolume;
        }
        private void OnFrame(VideoPlayer source, long frame)
        {
            if (IsPlaying && !display.enabled)
            {
                display.enabled = true;
                if (videoAudio != null && videoAudio.clip != null) videoAudio.Play();
            }
        }
        private void OnEnded(VideoPlayer source) => finishRequested = true;
        private void OnError(VideoPlayer source, string message)
        {
            Debug.LogWarning($"[GachaSummonVideo] 영상 재생 실패: {message}");
            finishRequested = true;
        }
        private IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
        {
            var start = Time.unscaledTime;
            while (Time.unscaledTime - start < duration)
            {
                group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - start) / duration));
                yield return null;
            }
            group.alpha = to;
        }
        private IEnumerator WatchPlayback(double duration)
        {
            yield return Fade(overlayGroup, 0f, 1f, 0.18f);
            var deadline = Time.realtimeSinceStartupAsDouble + 10d;
            while (!finishRequested && !player.isPrepared && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            if (!player.isPrepared) finishRequested = true;
            if (!finishRequested)
            {
                player.Play();
                deadline = Time.realtimeSinceStartupAsDouble + duration + 5d;
                while (!finishRequested && !display.enabled && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                if (!finishRequested) yield return Fade(videoGroup, 0f, 1f, 0.12f);
                while (!finishRequested && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
            }
            yield return Fade(videoGroup, videoGroup.alpha, 0f, 0.1f);
            yield return Fade(overlayGroup, 1f, 0f, 0.12f);
            watchdog = null;
            Complete(true); // 정상 종료·스킵·디코더 실패 모두 저장된 카드 공개로 복귀
        }
        public void Skip() => finishRequested = true;
        public void Cancel() => Complete(false);

        private void Complete(bool showCards)
        {
            var pending = completion;
            completion = null;
            if (this != null && watchdog != null) StopCoroutine(watchdog);
            watchdog = null;
            if (player != null)
            {
                player.Stop();
                player.targetTexture = null;
                player.clip = null;
            }
            if (videoAudio != null) { videoAudio.Stop(); videoAudio.clip = null; }
            if (display != null) { display.texture = null; display.enabled = false; }
            if (target != null) { target.Release(); Destroy(target); target = null; }
            AudioRuntimeSettings.Changed -= ApplyVolume;
            if (ownsAudioPause)
            {
                ownsAudioPause = false;
                AudioListener.pause = previousAudioPause;
            }
            if (this != null && gameObject.activeSelf) gameObject.SetActive(false);
            pending?.TrySetResult(showCards);
        }

        private void OnDisable() => Cancel();
        private void OnDestroy()
        {
            Cancel();
            if (!bound) return;
            if (skipButton != null) skipButton.onClick.RemoveListener(Skip);
            if (player == null) return;
            player.loopPointReached -= OnEnded;
            player.errorReceived -= OnError;
            player.frameReady -= OnFrame;
        }
    }
}