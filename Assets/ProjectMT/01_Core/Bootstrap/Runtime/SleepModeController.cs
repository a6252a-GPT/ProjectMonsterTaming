using System.Collections.Generic;
using ProjectMT.Features.Expedition;
using ProjectMT.Features.Settings;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Quest;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProjectMT.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class SleepModeController : MonoBehaviour // 전투를 멈추지 않는 저전력 표시 모드
    {
        [SerializeField] private GameObject displayRoot;
        [SerializeField] private TMP_Text clockText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private TMP_Text connectionText;
        [SerializeField] private TMP_Text batteryText;
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text battleStatusText;
        [SerializeField] private SleepModeSwipeHandle wakeSwipe;
        [SerializeField, Min(1)] private int sleepRenderInterval = 4;

        private LocalSettingsData settings;
        private float idleSeconds;
        private bool sleeping;
        private int previousRenderInterval = 1;
        private int lastDisplayedMinute = -1;
        private float nextStatusRefresh;
        private ExpeditionController expedition;
        private Canvas presentationCanvas;
        private int previousCanvasOrder;
        private readonly List<Canvas> loweredCanvases = new List<Canvas>();

        public bool IsSleeping => sleeping;

        private void OnEnable()
        {
            SettingsPanelController.SettingsChanged += HandleSettingsChanged;
            settings = LocalSettingsStore.Load();
            previousRenderInterval = Mathf.Max(1, OnDemandRendering.renderFrameInterval);
            ApplyScreenPolicy();
            Wake();
        }

        private void OnDisable()
        {
            SettingsPanelController.SettingsChanged -= HandleSettingsChanged;
            Wake();
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        private void Update()
        {
            if (settings == null || !settings.sleepModeEnabled)
            {
                return;
            }

            if (sleeping)
            {
                if (HasWakeKeyThisFrame() || (wakeSwipe == null && HasInputThisFrame()))
                {
                    Wake();
                    return;
                }

                RefreshClock();
                if (Time.unscaledTime >= nextStatusRefresh)
                {
                    RefreshStatus();
                }
                return;
            }

            if (HasInputThisFrame())
            {
                idleSeconds = 0f;
                return;
            }

            idleSeconds += Time.unscaledDeltaTime;
            if (idleSeconds >= Mathf.Max(60, settings.sleepDelayMinutes * 60))
            {
                EnterSleep();
            }
        }

        public void EnterSleep()
        {
            if (sleeping || settings == null || !settings.sleepModeEnabled)
            {
                return;
            }

            sleeping = true;
            previousRenderInterval = Mathf.Max(1, OnDemandRendering.renderFrameInterval);
            OnDemandRendering.renderFrameInterval = Mathf.Max(1, sleepRenderInterval);
            BringPresentationForward();
            displayRoot?.SetActive(true);
            transform.SetAsLastSibling();
            if (hintText != null)
            {
                hintText.text = wakeSwipe != null ? "밀어서 절전 해제" : "화면을 터치하면 절전 모드가 해제됩니다";
            }

            wakeSwipe?.ResetSwipe();
            expedition = FindFirstObjectByType<ExpeditionController>();
            RefreshClock(true);
            RefreshStatus();
        }

        public void Wake()
        {
            var wasSleeping = sleeping;
            sleeping = false;
            idleSeconds = 0f;
            lastDisplayedMinute = -1;
            expedition = null;
            if (wasSleeping)
            {
                OnDemandRendering.renderFrameInterval = Mathf.Max(1, previousRenderInterval);
            }

            displayRoot?.SetActive(false);
            RestorePresentationOrder();
        }

        private void BringPresentationForward()
        {
            presentationCanvas = GetComponentInParent<Canvas>();
            if (presentationCanvas == null)
            {
                return;
            }

            presentationCanvas = presentationCanvas.rootCanvas;
            previousCanvasOrder = presentationCanvas.sortingOrder;
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas != presentationCanvas && canvas.sortingOrder == short.MaxValue &&
                    canvas.sortingLayerID == presentationCanvas.sortingLayerID)
                {
                    loweredCanvases.Add(canvas);
                    canvas.sortingOrder = short.MaxValue - 1; // 최상위 보상창도 절전 중에는 화면 뒤로 보낸다
                }
            }
            presentationCanvas.sortingOrder = short.MaxValue;
        }

        private void RestorePresentationOrder()
        {
            if (presentationCanvas != null && presentationCanvas.sortingOrder == short.MaxValue)
            {
                presentationCanvas.sortingOrder = previousCanvasOrder;
            }
            presentationCanvas = null;
            foreach (var canvas in loweredCanvases)
            {
                if (canvas != null && canvas.sortingOrder == short.MaxValue - 1)
                {
                    canvas.sortingOrder = short.MaxValue;
                }
            }
            loweredCanvases.Clear();
        }

        private void RefreshStatus()
        {
            nextStatusRefresh = Time.unscaledTime + 1f; // 절전 정보는 초당 한 번만 갱신
            var reachability = Application.internetReachability;
            SetText(connectionText, reachability == NetworkReachability.NotReachable ? "오프라인" :
                reachability == NetworkReachability.ReachableViaCarrierDataNetwork ? "모바일" : "Wi-Fi / LAN");
            var battery = SystemInfo.batteryLevel;
            SetText(batteryText, battery < 0f ? "--" : $"{Mathf.RoundToInt(battery * 100f)}%");

            var scene = SceneManager.GetActiveScene().name;
            var progressService = QuestProgressServiceHub.Current;
            if (scene == "01_MainBattle" && progressService != null)
            {
                var progress = progressService.View;
                var repeat = progress.ExpeditionMode == ExpeditionRunMode.Repeat;
                var stage = repeat ? Mathf.Max(1, progress.ActiveLastClearedStage) : progress.ActiveChallengeStage;
                var difficulty = progress.Difficulty == ExpeditionDifficulty.Hard ? "하드" : "일반";
                SetText(stageText, $"{difficulty} 원정대 {stage}\n<size=75%>{(repeat ? "반복 사냥" : "단계 도전")}</size>");
                SetText(battleStatusText, expedition != null && expedition.IsRunning ? "몬스터 처치 중..." : "전투 준비 중...");
            }
            else
            {
                SetText(stageText, scene == "03_CastleRaidHex" ? "군단의 역습" : "절전 모드");
                SetText(battleStatusText, "게임이 계속 진행됩니다");
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null && target.text != value)
            {
                target.text = value;
            }
        }

        private static bool HasWakeKeyThisFrame()
        {
            return Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
                   Keyboard.current?.enterKey.wasPressedThisFrame == true ||
                   Keyboard.current?.spaceKey.wasPressedThisFrame == true ||
                   Gamepad.current?.buttonSouth.wasPressedThisFrame == true;
        }

        private void HandleSettingsChanged(LocalSettingsData updated)
        {
            settings = updated?.Clone() ?? LocalSettingsStore.Load();
            ApplyScreenPolicy();
            if (!settings.sleepModeEnabled)
            {
                Wake();
            }
            else
            {
                idleSeconds = 0f;
            }
        }

        private void ApplyScreenPolicy()
        {
            Screen.sleepTimeout = settings != null && settings.sleepModeEnabled
                ? SleepTimeout.NeverSleep
                : SleepTimeout.SystemSetting;
        }

        private void RefreshClock(bool force = false)
        {
            var now = System.DateTime.Now;
            if (!force && lastDisplayedMinute == now.Minute)
            {
                return;
            }

            lastDisplayedMinute = now.Minute;
            if (clockText != null)
            {
                clockText.text = now.ToString("HH:mm");
            }
        }

        private static bool HasInputThisFrame()
        {
            return Touchscreen.current?.primaryTouch.press.isPressed == true ||
                   Mouse.current?.leftButton.isPressed == true ||
                   Keyboard.current?.anyKey.isPressed == true ||
                   Gamepad.current?.buttonSouth.wasPressedThisFrame == true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(GameObject root, TMP_Text clock, TMP_Text hint, int renderInterval = 4)
        {
            displayRoot = root;
            clockText = clock;
            hintText = hint;
            sleepRenderInterval = Mathf.Max(1, renderInterval);
        }
        public void EditorConfigurePresentation(TMP_Text connection, TMP_Text battery, TMP_Text stage,
            TMP_Text battleStatus, SleepModeSwipeHandle swipe)
        {
            connectionText = connection;
            batteryText = battery;
            stageText = stage;
            battleStatusText = battleStatus;
            wakeSwipe = swipe;
        }
#endif
    }
}
