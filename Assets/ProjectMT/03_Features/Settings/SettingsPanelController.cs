using System;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Settings
{
    [DisallowMultipleComponent]
    public sealed class SettingsPanelController : MonoBehaviour // 환경설정 중형 팝업과 로컬 적용
    {
        public enum Tab
        {
            System,
            Graphics,
            Sound,
            Account
        }

        [Header("공통")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button outsideCloseButton;
        [SerializeField] private Button resetTabButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button[] tabButtons;
        [SerializeField] private GameObject[] tabPages;

        [Header("시스템")]
        [SerializeField] private Toggle sleepModeToggle;
        [SerializeField] private Button[] sleepDelayButtons;
        [SerializeField] private Button replayGuideButton;
        [SerializeField] private TMP_Text versionText;

        [Header("그래픽")]
        [SerializeField] private Button[] qualityButtons;
        [SerializeField] private Button[] frameRateButtons;
        [SerializeField] private Toggle damageNumbersToggle;
        [SerializeField] private Toggle unitHealthBarsToggle;

        [Header("사운드")]
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private TMP_Text bgmValueText;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TMP_Text sfxValueText;
        [SerializeField] private Toggle vibrationToggle;

        [Header("계정")]
        [SerializeField] private TMP_Text accountStateText;
        [SerializeField] private TMP_Text userIdText;
        [SerializeField] private Button copyUserIdButton;
        [SerializeField] private Button googleLinkButton;
        [SerializeField] private Button customerSupportButton;
        [SerializeField] private Button termsButton;
        [SerializeField] private Button privacyButton;
        [SerializeField] private Button logoutButton;
        [SerializeField] private Button deleteDataButton;
        [SerializeField] private GameObject deleteConfirmRoot;
        [SerializeField] private TMP_Text deleteConfirmMessageText;
        [SerializeField] private Button deleteCancelButton;
        [SerializeField] private Button deleteConfirmButton;

        private static readonly int[] SleepDelayOptions = { 1, 3, 5, 10 };
        private static readonly int[] FrameRateOptions = { 30, 60 };

        private LocalSettingsData data;
        private Tab activeTab;
        private bool bound;
        private bool refreshing;

        public static event Action<LocalSettingsData> SettingsChanged;
        public event Action<bool> OpenStateChanged;

        public bool IsOpen => gameObject.activeSelf;
        public Tab ActiveTab => activeTab;
        public LocalSettingsData CurrentData => data?.Clone();

        private void Awake()
        {
            BindControls();
            data = LocalSettingsStore.Load();
            ShowTab(Tab.System);
            deleteConfirmRoot?.SetActive(false);
            RefreshControls();
        }

        public void Open()
        {
            UIPanelPopAnimator.RequestOpen(gameObject);
            data = LocalSettingsStore.Load();
            ShowTab(Tab.System);
            deleteConfirmRoot?.SetActive(false);
            ApplyRuntimeSettings();
            RefreshControls();
            SetStatus("현재 기기에 저장된 설정입니다.");
            OpenStateChanged?.Invoke(true);
        }

        public void Close()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            deleteConfirmRoot?.SetActive(false);
            UIPanelPopAnimator.RequestClose(gameObject, () => OpenStateChanged?.Invoke(false));
        }

        public void ShowTab(Tab tab)
        {
            activeTab = tab;
            for (var i = 0; i < tabPages?.Length; i++)
            {
                tabPages[i]?.SetActive(i == (int)tab);
            }

            RefreshSelection(tabButtons, (int)tab);
            SetStatus(tab == Tab.Account
                ? "외부 계정·지원 기능은 서비스 계약 연결 후 활성화됩니다."
                : "변경한 값은 즉시 적용하고 이 기기에 저장합니다.");
        }

        private void BindControls()
        {
            if (bound)
            {
                return;
            }

            bound = true;
            closeButton?.onClick.AddListener(Close);
            outsideCloseButton?.onClick.AddListener(Close);
            resetTabButton?.onClick.AddListener(ResetCurrentTab);
            for (var i = 0; i < tabButtons?.Length; i++)
            {
                var index = i;
                tabButtons[i]?.onClick.AddListener(() => ShowTab((Tab)index));
            }

            sleepModeToggle?.onValueChanged.AddListener(value => Change(settings => settings.sleepModeEnabled = value));
            for (var i = 0; i < sleepDelayButtons?.Length; i++)
            {
                var index = i;
                sleepDelayButtons[i]?.onClick.AddListener(() => Change(settings =>
                    settings.sleepDelayMinutes = SleepDelayOptions[Mathf.Clamp(index, 0, SleepDelayOptions.Length - 1)]));
            }

            replayGuideButton?.onClick.AddListener(() => SetStatus("튜토리얼 다시 보기는 튜토리얼 기능 연결 후 제공됩니다."));
            for (var i = 0; i < qualityButtons?.Length; i++)
            {
                var index = i;
                qualityButtons[i]?.onClick.AddListener(() => Change(settings => settings.qualityLevel = index));
            }

            for (var i = 0; i < frameRateButtons?.Length; i++)
            {
                var index = i;
                frameRateButtons[i]?.onClick.AddListener(() => Change(settings =>
                    settings.targetFrameRate = FrameRateOptions[Mathf.Clamp(index, 0, FrameRateOptions.Length - 1)]));
            }

            damageNumbersToggle?.onValueChanged.AddListener(value => Change(settings => settings.damageNumbersVisible = value));
            unitHealthBarsToggle?.onValueChanged.AddListener(value => Change(settings => settings.unitHealthBarsVisible = value));
            bgmSlider?.onValueChanged.AddListener(value => Change(settings => settings.bgmVolume = value));
            sfxSlider?.onValueChanged.AddListener(value => Change(settings => settings.sfxVolume = value));
            vibrationToggle?.onValueChanged.AddListener(value => Change(settings => settings.vibrationEnabled = value));
            copyUserIdButton?.onClick.AddListener(CopyUserId);
            googleLinkButton?.onClick.AddListener(() => SetStatus("Google 로그인은 인증 계약 연결 후 활성화됩니다."));
            logoutButton?.onClick.AddListener(RequestLogout);
            deleteDataButton?.onClick.AddListener(OpenDeleteConfirmation);
            deleteCancelButton?.onClick.AddListener(CloseDeleteConfirmation);
            deleteConfirmButton?.onClick.AddListener(ConfirmDeleteProgress);
        }

        private void Change(Action<LocalSettingsData> change)
        {
            if (refreshing)
            {
                return;
            }

            data ??= LocalSettingsStore.Load();
            change?.Invoke(data);
            data.Normalize();
            LocalSettingsStore.Save(data);
            ApplyRuntimeSettings();
            RefreshControls();
            SetStatus(activeTab == Tab.Sound
                ? "BGM·효과음 채널 음량을 즉시 적용했습니다."
                : "설정을 저장하고 즉시 적용했습니다.");
            SettingsChanged?.Invoke(data.Clone());
        }

        private void ApplyRuntimeSettings()
        {
            if (!Application.isPlaying || data == null)
            {
                return;
            }

            Application.targetFrameRate = data.targetFrameRate;
            AudioRuntimeSettings.Apply(data.bgmVolume, data.sfxVolume, data.vibrationEnabled);
            var names = QualitySettings.names;
            if (names.Length > 0)
            {
                var qualityIndex = data.qualityLevel switch
                {
                    0 => 0,
                    2 => names.Length - 1,
                    _ => (names.Length - 1) / 2
                };
                QualitySettings.SetQualityLevel(qualityIndex, true);
            }

            foreach (var feedback in FindObjectsByType<CombatFeedbackPlayer>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                feedback.SetDisplayOptions(data.damageNumbersVisible, data.unitHealthBarsVisible);
            }
        }

        private void RefreshControls()
        {
            if (data == null)
            {
                return;
            }

            refreshing = true;
            sleepModeToggle?.SetIsOnWithoutNotify(data.sleepModeEnabled);
            RefreshSelection(sleepDelayButtons, Array.IndexOf(SleepDelayOptions, data.sleepDelayMinutes));
            RefreshSelection(qualityButtons, data.qualityLevel);
            RefreshSelection(frameRateButtons, Array.IndexOf(FrameRateOptions, data.targetFrameRate));
            damageNumbersToggle?.SetIsOnWithoutNotify(data.damageNumbersVisible);
            unitHealthBarsToggle?.SetIsOnWithoutNotify(data.unitHealthBarsVisible);
            bgmSlider?.SetValueWithoutNotify(data.bgmVolume);
            sfxSlider?.SetValueWithoutNotify(data.sfxVolume);
            vibrationToggle?.SetIsOnWithoutNotify(data.vibrationEnabled);
            if (bgmValueText != null)
            {
                bgmValueText.text = $"{Mathf.RoundToInt(data.bgmVolume * 100f)}%";
            }

            if (sfxValueText != null)
            {
                sfxValueText.text = $"{Mathf.RoundToInt(data.sfxVolume * 100f)}%";
            }

            if (versionText != null)
            {
                versionText.text = $"게임 버전  {Application.version}";
            }

            if (accountStateText != null)
            {
                accountStateText.text = data.accountLoggedIn ? "게스트 계정 · 로그인됨" : "로그아웃 상태";
            }

            if (userIdText != null)
            {
                userIdText.text = $"사용자 ID  ·  {AccountSessionStore.FormatUserId(data.guestUserId)}";
            }

            copyUserIdButton?.gameObject.SetActive(true);
            if (googleLinkButton != null)
            {
                googleLinkButton.interactable = true; // 실제 인증 대신 준비 상태 안내
            }

            SetUnavailable(customerSupportButton);
            SetUnavailable(termsButton);
            SetUnavailable(privacyButton);
            if (logoutButton != null)
            {
                logoutButton.interactable = data.accountLoggedIn;
            }

            if (deleteDataButton != null)
            {
                deleteDataButton.interactable = true;
            }

            refreshing = false;
        }

        private void ResetCurrentTab()
        {
            var defaults = LocalSettingsStore.CreateDefaults();
            Change(settings =>
            {
                switch (activeTab)
                {
                    case Tab.System:
                        settings.sleepModeEnabled = defaults.sleepModeEnabled;
                        settings.sleepDelayMinutes = defaults.sleepDelayMinutes;
                        break;
                    case Tab.Graphics:
                        settings.qualityLevel = defaults.qualityLevel;
                        settings.targetFrameRate = defaults.targetFrameRate;
                        settings.damageNumbersVisible = defaults.damageNumbersVisible;
                        settings.unitHealthBarsVisible = defaults.unitHealthBarsVisible;
                        break;
                    case Tab.Sound:
                        settings.bgmVolume = defaults.bgmVolume;
                        settings.sfxVolume = defaults.sfxVolume;
                        settings.vibrationEnabled = defaults.vibrationEnabled;
                        break;
                    case Tab.Account:
                        break;
                }
            });
            SetStatus(activeTab == Tab.Account
                ? "계정 탭에는 복원할 로컬 설정이 없습니다."
                : "현재 탭을 기본값으로 복원했습니다.");
        }

        private void CopyUserId()
        {
            if (string.IsNullOrWhiteSpace(userIdText?.text))
            {
                return;
            }

            GUIUtility.systemCopyBuffer = userIdText.text;
            SetStatus("사용자 ID를 복사했습니다.");
        }

        private void RequestLogout()
        {
            CloseDeleteConfirmation();
            SetStatus("로그아웃하고 타이틀 화면으로 이동합니다.");
            AccountRuntimeBridge.RequestLogout();
        }

        private void OpenDeleteConfirmation()
        {
            if (deleteConfirmRoot == null)
            {
                SetStatus("데이터 삭제 확인창 연결이 필요합니다.");
                return;
            }

            if (deleteConfirmMessageText != null)
            {
                deleteConfirmMessageText.text = "모든 진행 데이터를 초기화합니다. 이 작업은 되돌릴 수 없습니다.";
            }

            deleteConfirmRoot.SetActive(true); // 첫 번째 삭제 입력은 확인창만 연다
        }

        private void CloseDeleteConfirmation()
        {
            deleteConfirmRoot?.SetActive(false);
        }

        private async void ConfirmDeleteProgress()
        {
            if (deleteConfirmButton != null)
            {
                deleteConfirmButton.interactable = false;
            }

            if (deleteConfirmMessageText != null)
            {
                deleteConfirmMessageText.text = "진행 데이터를 초기화하는 중입니다...";
            }

            var deleted = await AccountRuntimeBridge.RequestDeleteProgressAsync();
            if (this == null)
            {
                return;
            }

            if (!deleted)
            {
                if (deleteConfirmButton != null)
                {
                    deleteConfirmButton.interactable = true;
                }

                SetStatus("진행 데이터를 초기화하지 못했습니다. 잠시 후 다시 시도하세요.");
                return;
            }

            CloseDeleteConfirmation();
        }

        private static void RefreshSelection(Button[] buttons, int selectedIndex)
        {
            if (buttons == null)
            {
                return;
            }

            for (var i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                var selected = i == selectedIndex;
                var normal = button.transform.Find("Normal_01");
                var alternate = button.transform.Find("Normal_02");
                var focus = button.transform.Find("Focus");
                normal?.gameObject.SetActive(!selected);
                alternate?.gameObject.SetActive(false);
                focus?.gameObject.SetActive(selected);
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.color = selected
                        ? new Color32(255, 241, 205, 255)
                        : new Color32(190, 198, 210, 255);
                }
            }
        }

        private static void SetUnavailable(Selectable selectable)
        {
            if (selectable != null)
            {
                selectable.interactable = false;
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Button close,
            Button outsideClose,
            Button reset,
            TMP_Text status,
            Button[] tabs,
            GameObject[] pages,
            Toggle sleepToggle,
            Button[] sleepButtons,
            Button guideButton,
            TMP_Text gameVersion,
            Button[] quality,
            Button[] frameRates,
            Toggle damageToggle,
            Toggle healthToggle,
            Slider bgm,
            TMP_Text bgmText,
            Slider sfx,
            TMP_Text sfxText,
            Toggle haptic,
            TMP_Text accountState,
            TMP_Text userId,
            Button copyId,
            Button google,
            Button support,
            Button termsOfService,
            Button privacyPolicy,
            Button deleteData)
        {
            closeButton = close;
            outsideCloseButton = outsideClose;
            resetTabButton = reset;
            statusText = status;
            tabButtons = tabs;
            tabPages = pages;
            sleepModeToggle = sleepToggle;
            sleepDelayButtons = sleepButtons;
            replayGuideButton = guideButton;
            versionText = gameVersion;
            qualityButtons = quality;
            frameRateButtons = frameRates;
            damageNumbersToggle = damageToggle;
            unitHealthBarsToggle = healthToggle;
            bgmSlider = bgm;
            bgmValueText = bgmText;
            sfxSlider = sfx;
            sfxValueText = sfxText;
            vibrationToggle = haptic;
            accountStateText = accountState;
            userIdText = userId;
            copyUserIdButton = copyId;
            googleLinkButton = google;
            customerSupportButton = support;
            termsButton = termsOfService;
            privacyButton = privacyPolicy;
            deleteDataButton = deleteData;
        }

        public void EditorConfigureAccountActions(
            Button logout,
            GameObject confirmRoot,
            TMP_Text confirmMessage,
            Button cancel,
            Button confirm)
        {
            logoutButton = logout;
            deleteConfirmRoot = confirmRoot;
            deleteConfirmMessageText = confirmMessage;
            deleteCancelButton = cancel;
            deleteConfirmButton = confirm;
        }
#endif
    }
}
