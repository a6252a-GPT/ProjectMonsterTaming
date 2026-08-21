using ProjectMT.Features.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace ProjectMT.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class SleepModeController : MonoBehaviour // 전투를 멈추지 않는 저전력 표시 모드
    {
        [SerializeField] private GameObject displayRoot;
        [SerializeField] private TMP_Text clockText;
        [SerializeField] private TMP_Text hintText;
        [SerializeField, Min(1)] private int sleepRenderInterval = 4;

        private LocalSettingsData settings;
        private float idleSeconds;
        private bool sleeping;
        private int previousRenderInterval = 1;
        private int lastDisplayedMinute = -1;

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

            if (HasInputThisFrame())
            {
                if (sleeping)
                {
                    Wake();
                }
                else
                {
                    idleSeconds = 0f;
                }

                return;
            }

            if (sleeping)
            {
                RefreshClock();
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
            displayRoot?.SetActive(true);
            transform.SetAsLastSibling();
            if (hintText != null)
            {
                hintText.text = "화면을 터치하면 절전 모드가 해제됩니다";
            }

            RefreshClock(true);
        }

        public void Wake()
        {
            var wasSleeping = sleeping;
            sleeping = false;
            idleSeconds = 0f;
            lastDisplayedMinute = -1;
            if (wasSleeping)
            {
                OnDemandRendering.renderFrameInterval = Mathf.Max(1, previousRenderInterval);
            }

            displayRoot?.SetActive(false);
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
            return Touchscreen.current?.primaryTouch.press.wasPressedThisFrame == true ||
                   Mouse.current?.leftButton.wasPressedThisFrame == true ||
                   Keyboard.current?.anyKey.wasPressedThisFrame == true ||
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
#endif
    }
}
