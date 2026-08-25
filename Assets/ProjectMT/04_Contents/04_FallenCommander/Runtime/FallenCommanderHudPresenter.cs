using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.FallenCommander
{
    public interface IBossDungeonHudSource
    {
        event Action<FallenCommanderHudState> HudStateChanged;
    }

    public interface IBossDungeonTimeoutController
    {
        void DebugTimeout();
        void DebugReduceTimeTenSeconds();
    }

    public interface IBossDungeonBossKillController
    {
        void DebugKillBoss();
    }

    public interface IBossDungeonBossHealthDebugController
    {
        void DebugDamageBossTenPercent();
        void DebugSetBossPhase(int phaseNumber);
    }

    public interface IBossDungeonAttackDebugController
    {
        void DebugBasicAttack();
        void DebugMeleeAttack();
        void DebugMarkStrike();
        void DebugTrackingMark();
        void DebugWideBurst();
        void DebugChargedWideBurst();
        void DebugLineStrike();
        void DebugCorruptionRing();
    }

    // HUD에 표시할 값을 한 번에 전달하기 위한 데이터 묶음
    public readonly struct FallenCommanderHudState
    {
        public FallenCommanderHudState(
            float bossHealth,
            float bossMaxHealth,
            float remainingBreakGauge,
            float maxBreakGauge,
            bool isBroken,
            int score,
            float remainingTime,
            int comboCount,
            int comboScore,
            float comboRemainingTime,
            float breakRemainingTime,
            float breakDuration,
            int commanderCurrentHearts = 0,
            int commanderMaxHearts = 0,
            bool isCommanderStunned = false,
            float commanderStunRemainingTime = 0f,
            float commanderStunDuration = 0f,
            bool isFinalChargeActive = false,
            float finalChargeRemainingTime = 0f,
            float finalChargeDuration = 0f,
            bool isTimeoutWipeActive = false,
            bool isTimeoutWarningActive = false,
            float timeoutWarningDuration = 0f,
            bool isPhaseTransitionActive = false,
            int bossPhase = 1,
            string phaseTransitionMessage = "")
        {
            BossHealth = bossHealth;
            BossMaxHealth = bossMaxHealth;
            RemainingBreakGauge = remainingBreakGauge;
            MaxBreakGauge = maxBreakGauge;
            IsBroken = isBroken;
            Score = score;
            RemainingTime = remainingTime;
            ComboCount = comboCount;
            ComboScore = comboScore;
            ComboRemainingTime = comboRemainingTime;
            BreakRemainingTime = breakRemainingTime;
            BreakDuration = breakDuration;
            CommanderCurrentHearts = commanderCurrentHearts;
            CommanderMaxHearts = commanderMaxHearts;
            IsCommanderStunned = isCommanderStunned;
            CommanderStunRemainingTime = commanderStunRemainingTime;
            CommanderStunDuration = commanderStunDuration;
            IsFinalChargeActive = isFinalChargeActive;
            FinalChargeRemainingTime = finalChargeRemainingTime;
            FinalChargeDuration = finalChargeDuration;
            IsTimeoutWipeActive = isTimeoutWipeActive;
            IsTimeoutWarningActive = isTimeoutWarningActive;
            TimeoutWarningDuration = timeoutWarningDuration;
            IsPhaseTransitionActive = isPhaseTransitionActive;
            BossPhase = bossPhase;
            PhaseTransitionMessage = phaseTransitionMessage;
        }

        public float BossHealth { get; }
        public float BossMaxHealth { get; }
        public float RemainingBreakGauge { get; }
        public float MaxBreakGauge { get; }
        public bool IsBroken { get; }
        public int Score { get; }
        public float RemainingTime { get; }
        public int ComboCount { get; }
        public int ComboScore { get; }
        public float ComboRemainingTime { get; }
        public float BreakRemainingTime { get; }
        public float BreakDuration { get; }
        public int CommanderCurrentHearts { get; }
        public int CommanderMaxHearts { get; }
        public bool IsCommanderStunned { get; }
        public float CommanderStunRemainingTime { get; }
        public float CommanderStunDuration { get; }
        public bool IsFinalChargeActive { get; }
        public float FinalChargeRemainingTime { get; }
        public float FinalChargeDuration { get; }
        public bool IsTimeoutWipeActive { get; }
        public bool IsTimeoutWarningActive { get; }
        public float TimeoutWarningDuration { get; }
        public bool IsPhaseTransitionActive { get; }
        public int BossPhase { get; }
        public string PhaseTransitionMessage { get; }
    }

    // 같은 GameObject에 이 컴포넌트를 여러 개 추가하지 못하게 막는다.
    [DisallowMultipleComponent]
    public sealed class FallenCommanderHudPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject hudRoot;
        [SerializeField] private Image bossHealthFill;
        [SerializeField] private Image breakGaugeFill;
        [SerializeField] private Text bossHealthValue;
        [SerializeField] private Text breakGaugeValue;
        [SerializeField] private GameObject breakNotice;
        [SerializeField] private Text scoreValue;
        [SerializeField] private Text timerValue;
        [SerializeField] private Text comboScoreValue;
        [SerializeField] private Image breakDurationFill;
        [SerializeField] private Button debugTimeoutButton;
        [SerializeField] private Button debugReduceTimeButton;
        [SerializeField] private Button debugRestartBattleButton;
        [SerializeField] private Button debugBasicAttackButton;
        [SerializeField] private Button debugHandSlamButton;
        [SerializeField] private Button debugLineStrikeButton;
        [SerializeField] private Button debugMarkStrikeButton;
        [SerializeField] private Button debugTrackingMarkButton;
        [SerializeField] private Button debugWideBurstButton;
        [SerializeField] private Button debugChargedWideBurstButton;
        [SerializeField] private Button debugCorruptionRingButton;
        [SerializeField] private Button debugBossKillButton;
        [SerializeField] private Button debugBossHealthButton;
        [SerializeField] private Button debugPhase1Button;
        [SerializeField] private Button debugPhase2Button;
        [SerializeField] private Button debugPhase3Button;
        [SerializeField] private Sprite commanderHeartSprite;
        [SerializeField] private GameObject finalChargeRoot;
        [SerializeField] private Image finalChargeFill;
        [SerializeField] private Text finalChargeWarning;
        [SerializeField] private Text finalChargeTimeValue;
        [SerializeField] private Text phaseTransitionNotice;
        [SerializeField, Range(0f, 1f)] private float timeoutWarningMinAlpha = 0.35f;
        [SerializeField, Min(0.05f)] private float timeoutWarningPulseInterval = 0.45f;

        private IBossDungeonHudSource hudSource;
        private IBossDungeonTimeoutController timeoutController;
        private IBossDungeonBossKillController bossKillController;
        private IBossDungeonBossHealthDebugController bossHealthDebugController;
        private IBossDungeonAttackDebugController attackDebugController;
        private bool showDebugControls;
        private bool keepRestartVisibleWhenUnbound;
        private static Font runtimeKoreanFont;
        private RectTransform commanderHeartRoot;
        private Text commanderStunNotice;
        private CanvasGroup finalChargeCanvasGroup;
        private bool isTimeoutWarningPulsing;
        private readonly List<Graphic> commanderHeartGraphics = new List<Graphic>();
        private readonly Dictionary<GameObject, bool> hiddenHudRootChildren =
            new Dictionary<GameObject, bool>();
        private int renderedCommanderMaxHearts = -1;

        // Controller와 HUD를 연결한다.
        //Controller.Initialize()에서 호출
        public void Bind(
            IBossDungeonHudSource targetController,
            bool showDebugButtons)
        {
            //기존 이벤트 구독을 먼저 해제
            Unbind();

            hudSource = targetController;
            timeoutController = targetController as IBossDungeonTimeoutController;
            bossKillController = targetController as IBossDungeonBossKillController;
            bossHealthDebugController = targetController as IBossDungeonBossHealthDebugController;
            attackDebugController = targetController as IBossDungeonAttackDebugController;
            showDebugControls = showDebugButtons;
            keepRestartVisibleWhenUnbound = showDebugButtons;
            if (hudSource != null)
            {
                // Controller가 HudStateChanged 이벤트를 발생시키면
                // 이 Presenter의 Render()를 실행한다.
                hudSource.HudStateChanged += Render;
            }

            SetVisible(true);
            finalChargeCanvasGroup = finalChargeRoot == null
                ? null
                : finalChargeRoot.GetComponent<CanvasGroup>();
            finalChargeRoot?.SetActive(false);
            phaseTransitionNotice?.gameObject.SetActive(false);
            EnsureRuntimeControls();
            ConfigureAttackDebugLabels();
            ApplyHudLayout();
            SetControlVisibility();
            debugTimeoutButton?.onClick.RemoveListener(HandleDebugTimeout);
            debugTimeoutButton?.onClick.AddListener(HandleDebugTimeout);
            debugReduceTimeButton?.onClick.RemoveListener(HandleDebugReduceTime);
            debugReduceTimeButton?.onClick.AddListener(HandleDebugReduceTime);
            debugBasicAttackButton?.onClick.RemoveListener(HandleDebugBasicAttack);
            debugBasicAttackButton?.onClick.AddListener(HandleDebugBasicAttack);
            debugHandSlamButton?.onClick.RemoveListener(HandleDebugHandSlam);
            debugHandSlamButton?.onClick.AddListener(HandleDebugHandSlam);
            debugLineStrikeButton?.onClick.RemoveListener(HandleDebugLineStrike);
            debugLineStrikeButton?.onClick.AddListener(HandleDebugLineStrike);
            debugMarkStrikeButton?.onClick.RemoveListener(HandleDebugMarkStrike);
            debugMarkStrikeButton?.onClick.AddListener(HandleDebugMarkStrike);
            debugTrackingMarkButton?.onClick.RemoveListener(HandleDebugTrackingMark);
            debugTrackingMarkButton?.onClick.AddListener(HandleDebugTrackingMark);
            debugWideBurstButton?.onClick.RemoveListener(HandleDebugWideBurst);
            debugWideBurstButton?.onClick.AddListener(HandleDebugWideBurst);
            debugChargedWideBurstButton?.onClick.RemoveListener(HandleDebugChargedWideBurst);
            debugChargedWideBurstButton?.onClick.AddListener(HandleDebugChargedWideBurst);
            debugCorruptionRingButton?.onClick.RemoveListener(HandleDebugCorruptionRing);
            debugCorruptionRingButton?.onClick.AddListener(HandleDebugCorruptionRing);
            debugBossKillButton?.onClick.RemoveListener(HandleDebugKillBoss);
            debugBossKillButton?.onClick.AddListener(HandleDebugKillBoss);
            debugBossHealthButton?.onClick.RemoveListener(HandleDebugBossHealth);
            debugPhase1Button?.onClick.RemoveListener(HandleDebugPhase1);
            debugPhase1Button?.onClick.AddListener(HandleDebugPhase1);
            debugPhase2Button?.onClick.RemoveListener(HandleDebugPhase2);
            debugPhase2Button?.onClick.AddListener(HandleDebugPhase2);
            debugPhase3Button?.onClick.RemoveListener(HandleDebugPhase3);
            debugPhase3Button?.onClick.AddListener(HandleDebugPhase3);
            phaseTransitionNotice?.gameObject.SetActive(false);
            isTimeoutWarningPulsing = false;
            SetFinalChargeAlpha(1f);
            debugBossHealthButton?.onClick.AddListener(HandleDebugBossHealth);
        }

        // Controller와 HUD의 연결을 해제
        public void Unbind()
        {
            if (hudSource == null)
            {
                return;
            }

            // Bind()에서 등록했던 Render()를 이벤트에서 제거
            hudSource.HudStateChanged -= Render;
            hudSource = null;
            timeoutController = null;
            bossKillController = null;
            bossHealthDebugController = null;
            attackDebugController = null;
            showDebugControls = false;
            debugTimeoutButton?.onClick.RemoveListener(HandleDebugTimeout);
            debugReduceTimeButton?.onClick.RemoveListener(HandleDebugReduceTime);
            debugBasicAttackButton?.onClick.RemoveListener(HandleDebugBasicAttack);
            debugHandSlamButton?.onClick.RemoveListener(HandleDebugHandSlam);
            debugLineStrikeButton?.onClick.RemoveListener(HandleDebugLineStrike);
            debugMarkStrikeButton?.onClick.RemoveListener(HandleDebugMarkStrike);
            debugTrackingMarkButton?.onClick.RemoveListener(HandleDebugTrackingMark);
            debugWideBurstButton?.onClick.RemoveListener(HandleDebugWideBurst);
            debugChargedWideBurstButton?.onClick.RemoveListener(HandleDebugChargedWideBurst);
            debugCorruptionRingButton?.onClick.RemoveListener(HandleDebugCorruptionRing);
            debugBossKillButton?.onClick.RemoveListener(HandleDebugKillBoss);
            debugBossHealthButton?.onClick.RemoveListener(HandleDebugBossHealth);
            debugPhase1Button?.onClick.RemoveListener(HandleDebugPhase1);
            debugPhase2Button?.onClick.RemoveListener(HandleDebugPhase2);
            debugPhase3Button?.onClick.RemoveListener(HandleDebugPhase3);
        }

        private void SetControlVisibility()
        {
            var hasTimedBattle = timeoutController != null;
            var hasAttackDebug = attackDebugController != null;

            scoreValue?.gameObject.SetActive(hasTimedBattle);
            timerValue?.gameObject.SetActive(hasTimedBattle);
            comboScoreValue?.gameObject.SetActive(false);
            debugTimeoutButton?.gameObject.SetActive(
                hasTimedBattle && showDebugControls);
            debugReduceTimeButton?.gameObject.SetActive(
                hasTimedBattle && showDebugControls);
            debugRestartBattleButton?.gameObject.SetActive(
                keepRestartVisibleWhenUnbound);
            debugBasicAttackButton?.gameObject.SetActive(
                hasAttackDebug && showDebugControls);
            debugHandSlamButton?.gameObject.SetActive(
                hasAttackDebug && showDebugControls);
            debugLineStrikeButton?.gameObject.SetActive(
                hasAttackDebug && showDebugControls);
            debugMarkStrikeButton?.gameObject.SetActive(
                hasAttackDebug && showDebugControls);
            debugTrackingMarkButton?.gameObject.SetActive(
                hasAttackDebug && showDebugControls);
            debugWideBurstButton?.gameObject.SetActive(
                hasAttackDebug && showDebugControls);
            debugChargedWideBurstButton?.gameObject.SetActive(
                hasAttackDebug && showDebugControls);
            debugCorruptionRingButton?.gameObject.SetActive(
                hasAttackDebug && showDebugControls);
            debugBossKillButton?.gameObject.SetActive(
                bossKillController != null && showDebugControls);
            debugBossHealthButton?.gameObject.SetActive(
                bossHealthDebugController != null && showDebugControls);
            debugPhase1Button?.gameObject.SetActive(
                bossHealthDebugController != null && showDebugControls);
            debugPhase2Button?.gameObject.SetActive(
                bossHealthDebugController != null && showDebugControls);
            debugPhase3Button?.gameObject.SetActive(
                bossHealthDebugController != null && showDebugControls);
        }
        // HUD 전체의 표시 여부
        public void SetVisible(bool visible)
        {
            if (hudRoot != null)
            {
                var parent = hudRoot.transform.parent;
                while (parent != null)
                {
                    if (parent.localScale == Vector3.zero)
                    {
                        parent.localScale = Vector3.one;
                    }

                    parent = parent.parent;
                }

                if (visible)
                {
                    hudRoot.SetActive(true);
                    RestoreHudRootChildren();
                }
                else if (CanKeepRestartVisible())
                {
                    hudRoot.SetActive(true);
                    HideHudRootChildrenExceptRestart();
                }
                else
                {
                    hiddenHudRootChildren.Clear();
                    hudRoot.SetActive(false);
                }
            }

            if (!visible && commanderHeartRoot != null)
            {
                commanderHeartRoot.gameObject.SetActive(false);
            }

            if (!visible && commanderStunNotice != null)
            {
                commanderStunNotice.gameObject.SetActive(false);
            }

            if (!visible && finalChargeRoot != null)
            {
                finalChargeRoot.SetActive(false);
            }

            if (!visible)
            {
                isTimeoutWarningPulsing = false;
                SetFinalChargeAlpha(1f);
            }

            if (!visible)
            {
                phaseTransitionNotice?.gameObject.SetActive(false);
            }
        }

        // DEV 재시작 버튼이 HUD Canvas의 직접 자식일 때 종료 후에도 표시한다.
        private bool CanKeepRestartVisible()
        {
            return keepRestartVisibleWhenUnbound &&
                debugRestartBattleButton != null &&
                debugRestartBattleButton.transform.parent == hudRoot.transform;
        }

        // 재시작 버튼을 제외한 HUD Canvas의 직접 자식 상태를 저장하고 숨긴다.
        private void HideHudRootChildrenExceptRestart()
        {
            hiddenHudRootChildren.Clear();
            foreach (Transform child in hudRoot.transform)
            {
                if (child == debugRestartBattleButton.transform)
                {
                    child.gameObject.SetActive(true);
                    continue;
                }

                hiddenHudRootChildren[child.gameObject] = child.gameObject.activeSelf;
                child.gameObject.SetActive(false);
            }
        }

        // 재시작 전에 저장했던 HUD Canvas 자식 활성 상태를 복원한다.
        private void RestoreHudRootChildren()
        {
            foreach (var entry in hiddenHudRootChildren)
            {
                if (entry.Key != null)
                {
                    entry.Key.SetActive(entry.Value);
                }
            }

            hiddenHudRootChildren.Clear();
        }

        private void Update()
        {
            if (!isTimeoutWarningPulsing || finalChargeCanvasGroup == null)
            {
                return;
            }

            var pulse = Mathf.PingPong(
                Time.unscaledTime / Mathf.Max(0.05f, timeoutWarningPulseInterval),
                1f);
            SetFinalChargeAlpha(Mathf.Lerp(timeoutWarningMinAlpha, 1f, pulse));
        }

        private void OnDestroy()
        {
            Unbind();
        }
        // Controller가 새로운 HUD 상태를 전달하면 호출
        // 전달받은 전투 상태를 체력·게이지·경고·페이즈 UI에 반영한다.
        private void Render(FallenCommanderHudState state)
        {
            // 현재 체력을 0~1 비율로 계산한다.
            // 최대 체력이 0이면 나눗셈을 할 수 없으므로 0을 사용한다.
            var healthRatio = state.BossMaxHealth > 0f
                ? state.BossHealth / state.BossMaxHealth
                : 0f;
            // 남은 브레이크 게이지를 0~1 비율로 계산한다.
            var breakRatio = state.MaxBreakGauge > 0f
                ? state.RemainingBreakGauge / state.MaxBreakGauge
                : 0f;
            var breakDurationRatio = state.BreakDuration > 0f
                ? state.BreakRemainingTime / state.BreakDuration
                : 0f;

            SetHorizontalFill(bossHealthFill, healthRatio);
            SetHorizontalFill(breakGaugeFill, breakRatio);
            SetHorizontalFill(breakDurationFill, state.IsBroken ? breakDurationRatio : 0f);
            var finalChargeRatio = state.FinalChargeDuration > 0f
                ? 1f - state.FinalChargeRemainingTime / state.FinalChargeDuration
                : 0f;
            var timeoutWarningRatio = state.TimeoutWarningDuration > 0f
                ? 1f - state.RemainingTime / state.TimeoutWarningDuration
                : 0f;
            SetHorizontalFill(
                finalChargeFill,
                state.IsTimeoutWipeActive
                    ? 1f
                    : state.IsTimeoutWarningActive
                        ? timeoutWarningRatio
                    : state.IsFinalChargeActive
                        ? finalChargeRatio
                        : 0f);
            RenderCommanderHearts(state.CommanderCurrentHearts, state.CommanderMaxHearts);

            if (phaseTransitionNotice != null)
            {
                phaseTransitionNotice.gameObject.SetActive(state.IsPhaseTransitionActive);
                if (state.IsPhaseTransitionActive)
                {
                    phaseTransitionNotice.text = string.IsNullOrWhiteSpace(
                        state.PhaseTransitionMessage)
                            ? $"{state.BossPhase} 페이즈"
                            : state.PhaseTransitionMessage;
                }
            }

            if (finalChargeRoot != null)
            {
                finalChargeRoot.SetActive(
                    state.IsFinalChargeActive ||
                    state.IsTimeoutWarningActive ||
                    state.IsTimeoutWipeActive);
            }

            isTimeoutWarningPulsing =
                state.IsTimeoutWarningActive || state.IsTimeoutWipeActive;
            if (!isTimeoutWarningPulsing)
            {
                SetFinalChargeAlpha(1f);
            }

            if (finalChargeWarning != null)
            {
                finalChargeWarning.text = state.IsTimeoutWipeActive
                    ? "시간 종료! 전멸 공격이 발동됩니다!"
                    : state.IsTimeoutWarningActive
                        ? "경고! 곧 전멸 공격이 발동됩니다!"
                        : "경고! 보스가 강력한 광역 공격을 준비합니다!";
            }

            if (finalChargeTimeValue != null)
            {
                finalChargeTimeValue.text = state.IsTimeoutWipeActive
                    ? "전멸 공격 발동!"
                    : state.IsTimeoutWarningActive
                        ? $"전멸까지 {state.RemainingTime:0.0}초"
                        : $"광역 공격까지 {state.FinalChargeRemainingTime:0.0}초";
            }

            if (commanderStunNotice != null)
            {
                commanderStunNotice.gameObject.SetActive(state.IsCommanderStunned);
                if (state.IsCommanderStunned)
                {
                    commanderStunNotice.text = $"기절 {state.CommanderStunRemainingTime:0.0}s";
                }
            }

            if (bossHealthValue != null)
            {
                // 소수점 올림해서 정수처럼 표시
                bossHealthValue.text =
                    $"{Mathf.CeilToInt(state.BossHealth)} / {Mathf.CeilToInt(state.BossMaxHealth)}";
            }

            if (breakGaugeValue != null)
            {
                breakGaugeValue.text =
                    $"{Mathf.CeilToInt(state.RemainingBreakGauge)} / {Mathf.CeilToInt(state.MaxBreakGauge)}";
            }

            if (breakNotice != null)
            {
                breakNotice.SetActive(state.IsBroken);
            }

            if (scoreValue != null)
            {
                scoreValue.text = $"Score {state.Score}";
            }

            if (timerValue != null)
            {
                var seconds = Mathf.CeilToInt(Mathf.Max(0f, state.RemainingTime));
                timerValue.text = $"Time {seconds / 60:00}:{seconds % 60:00}";
            }

            if (comboScoreValue != null)
            {
                comboScoreValue.text = $"Combo x{state.ComboCount}  +{state.ComboScore}";
            }
        }

        private void HandleDebugTimeout()
        {
            timeoutController?.DebugTimeout();
        }

        private void HandleDebugReduceTime()
        {
            timeoutController?.DebugReduceTimeTenSeconds();
        }

        private void HandleDebugBasicAttack()
        {
            attackDebugController?.DebugBasicAttack();
        }

        private void HandleDebugHandSlam()
        {
            attackDebugController?.DebugMeleeAttack();
        }

        private void SetFinalChargeAlpha(float alpha)
        {
            if (finalChargeCanvasGroup != null)
            {
                finalChargeCanvasGroup.alpha = Mathf.Clamp01(alpha);
            }
        }

        private void HandleDebugLineStrike()
        {
            attackDebugController?.DebugLineStrike();
        }

        private void HandleDebugMarkStrike()
        {
            attackDebugController?.DebugMarkStrike();
        }

        private void HandleDebugTrackingMark()
        {
            attackDebugController?.DebugTrackingMark();
        }

        private void HandleDebugWideBurst()
        {
            attackDebugController?.DebugWideBurst();
        }

        private void HandleDebugChargedWideBurst()
        {
            attackDebugController?.DebugChargedWideBurst();
        }

        private void HandleDebugCorruptionRing()
        {
            attackDebugController?.DebugCorruptionRing();
        }

        private void HandleDebugKillBoss()
        {
            bossKillController?.DebugKillBoss();
        }

        private void HandleDebugBossHealth()
        {
            bossHealthDebugController?.DebugDamageBossTenPercent();
        }

        private void HandleDebugPhase1()
        {
            bossHealthDebugController?.DebugSetBossPhase(1);
        }

        private void HandleDebugPhase2()
        {
            bossHealthDebugController?.DebugSetBossPhase(2);
        }

        private void HandleDebugPhase3()
        {
            bossHealthDebugController?.DebugSetBossPhase(3);
        }

        private void ConfigureAttackDebugLabels()
        {
            SetButtonLabel(debugBasicAttackButton, "기본 공격");
            SetButtonLabel(debugHandSlamButton, "근접 공격");
            SetButtonLabel(debugLineStrikeButton, "직선 공격");
            SetButtonLabel(debugTrackingMarkButton, "추적 낙인");
            SetButtonLabel(debugWideBurstButton, "블랙홀");
            SetButtonLabel(debugChargedWideBurstButton, "충전 광역기");
            SetButtonLabel(debugCorruptionRingButton, "타락의 고리");
            SetButtonLabel(debugBossHealthButton, "보스 체력 -10%");
            SetButtonLabel(debugPhase1Button, "1 페이즈");
            SetButtonLabel(debugPhase2Button, "2 페이즈");
            SetButtonLabel(debugPhase3Button, "3 페이즈");
            SetButtonLabel(debugReduceTimeButton, "시간 -10초");
            SetButtonLabel(debugRestartBattleButton, "전투 재시작");
        }

        private static void SetButtonLabel(Button button, string text)
        {
            var label = button == null
                ? null
                : button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = text;
            }
        }

        private void EnsureRuntimeControls()
        {
            if (hudRoot == null)
            {
                return;
            }

            if (scoreValue == null)
            {
                scoreValue = CreateRuntimeText("ScoreValue_Runtime", new Vector2(28f, -118f), new Vector2(220f, 30f));
            }

            if (timerValue == null)
            {
                timerValue = CreateRuntimeText("TimerValue_Runtime", new Vector2(710f, -118f), new Vector2(175f, 30f));
            }

            if (comboScoreValue == null)
            {
                comboScoreValue = CreateRuntimeText("ComboScoreValue_Runtime", new Vector2(366f, -180f), new Vector2(220f, 30f));
                comboScoreValue.color = new Color(1f, 0.85f, 0.25f, 1f);
            }

            if (breakDurationFill == null)
            {
                breakDurationFill = CreateRuntimeGauge(
                    "BreakDurationGauge_Runtime",
                    new Vector2(254f, -113f),
                    new Vector2(500f, 8f),
                    new Color(0.2f, 0.8f, 1f, 1f));
            }

            if (phaseTransitionNotice == null)
            {
                var editorNotice = hudRoot.transform.Find(
                    "BossPhaseNotice_Editor");
                phaseTransitionNotice = editorNotice == null
                    ? null
                    : editorNotice.GetComponent<Text>();
            }

            if (phaseTransitionNotice == null)
            {
                phaseTransitionNotice = CreateRuntimeText(
                    "BossPhaseNotice_Runtime",
                    new Vector2(0f, 80f),
                    new Vector2(500f, 100f),
                    hudRoot.transform);
                var rect = phaseTransitionNotice.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, 80f);
                phaseTransitionNotice.alignment = TextAnchor.MiddleCenter;
                phaseTransitionNotice.fontSize = 48;
                phaseTransitionNotice.fontStyle = FontStyle.Bold;
                phaseTransitionNotice.color = new Color(1f, 0.75f, 0.2f, 1f);
                phaseTransitionNotice.text = "2 페이즈";
            }

            if (showDebugControls && debugTimeoutButton == null)
            {
                var buttonObject = new GameObject("DebugTimeoutButton_Runtime");
                buttonObject.transform.SetParent(hudRoot.transform, false);
                var rect = buttonObject.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(28f, -150f);
                rect.sizeDelta = new Vector2(160f, 26f);
                var image = buttonObject.AddComponent<Image>();
                image.color = new Color(0.8f, 0.35f, 0.15f, 1f);
                debugTimeoutButton = buttonObject.AddComponent<Button>();
                debugTimeoutButton.targetGraphic = image;
                var label = CreateRuntimeText("Label", new Vector2(0f, 0f), new Vector2(160f, 26f), buttonObject.transform);
                var labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.anchoredPosition = Vector2.zero;
                labelRect.sizeDelta = Vector2.zero;
                label.alignment = TextAnchor.MiddleCenter;
                label.text = "시간 종료";
                label.color = Color.white;
                ConfigureRuntimeButtonLabel(label);
                debugTimeoutButton.onClick.AddListener(HandleDebugTimeout);
            }

            if (debugReduceTimeButton == null)
            {
                var editorButton = hudRoot.transform.Find(
                    "BossStatusPanel/Testbutton/DebugReduceTimeButton_Editor");
                debugReduceTimeButton = editorButton == null
                    ? null
                    : editorButton.GetComponent<Button>();
            }

            if (debugRestartBattleButton == null)
            {
                var editorButton = hudRoot.transform.Find(
                    "DebugRestartBattleButton_Editor");
                debugRestartBattleButton = editorButton == null
                    ? null
                    : editorButton.GetComponent<Button>();
            }

            if (debugCorruptionRingButton == null)
            {
                var editorButton = hudRoot.transform.Find(
                    "BossStatusPanel/Testbutton/DebugCorruptionRingButton_Editor");
                debugCorruptionRingButton = editorButton == null
                    ? null
                    : editorButton.GetComponent<Button>();
            }

            if (debugTrackingMarkButton == null)
            {
                var editorButton = hudRoot.transform.Find(
                    "BossStatusPanel/Testbutton/DebugTrackingMarkButton_Editor");
                debugTrackingMarkButton = editorButton == null
                    ? null
                    : editorButton.GetComponent<Button>();
            }

            if (debugPhase1Button == null)
            {
                var editorButton = hudRoot.transform.Find(
                    "PhaseDebugButtons_Editor/DebugPhase1Button_Editor");
                debugPhase1Button = editorButton == null
                    ? null
                    : editorButton.GetComponent<Button>();
            }

            if (debugPhase2Button == null)
            {
                var editorButton = hudRoot.transform.Find(
                    "PhaseDebugButtons_Editor/DebugPhase2Button_Editor");
                debugPhase2Button = editorButton == null
                    ? null
                    : editorButton.GetComponent<Button>();
            }

            if (debugPhase3Button == null)
            {
                var editorButton = hudRoot.transform.Find(
                    "PhaseDebugButtons_Editor/DebugPhase3Button_Editor");
                debugPhase3Button = editorButton == null
                    ? null
                    : editorButton.GetComponent<Button>();
            }

            EnsureCommanderHeartRoot();

            if (!showDebugControls)
            {
                return;
            }

            debugBasicAttackButton ??= CreateRuntimeButton(
                "DebugBasicAttackButton_Runtime",
                "기본 공격",
                new Vector2(28f, -182f),
                new Color(0.35f, 0.55f, 0.8f, 1f));
            debugHandSlamButton ??= CreateRuntimeButton(
                "DebugHandSlamButton_Runtime",
                "내려찍기",
                new Vector2(128f, -182f),
                new Color(0.85f, 0.35f, 0.2f, 1f));
            debugLineStrikeButton ??= CreateRuntimeButton(
                "DebugLineStrikeButton_Runtime",
                "직선 공격",
                new Vector2(428f, -182f),
                new Color(0.25f, 0.45f, 0.85f, 1f));
            debugMarkStrikeButton ??= CreateRuntimeButton(
                "DebugMarkStrikeButton_Runtime",
                "위치 공격",
                new Vector2(228f, -182f),
                new Color(0.3f, 0.65f, 0.8f, 1f));
            debugWideBurstButton ??= CreateRuntimeButton(
                "DebugWideBurstButton_Runtime",
                "블랙홀",
                new Vector2(328f, -182f),
                new Color(0.7f, 0.25f, 0.75f, 1f));
            debugChargedWideBurstButton ??= CreateRuntimeButton(
                "DebugChargedWideBurstButton_Runtime",
                "충전 광역기",
                new Vector2(528f, -182f),
                new Color(0.75f, 0.18f, 0.18f, 1f));
            debugCorruptionRingButton ??= CreateRuntimeButton(
                "DebugCorruptionRingButton_Runtime",
                "타락의 고리",
                new Vector2(628f, -182f),
                new Color(0.55f, 0.08f, 0.2f, 1f));
            debugTrackingMarkButton ??= CreateRuntimeButton(
                "DebugTrackingMarkButton_Runtime",
                "추적 낙인",
                new Vector2(728f, -182f),
                new Color(0.15f, 0.55f, 0.8f, 1f));
            debugReduceTimeButton ??= CreateRuntimeButton(
                "DebugReduceTimeButton_Runtime",
                "시간 -10초",
                new Vector2(128f, -150f),
                new Color(0.8f, 0.35f, 0.15f, 1f));
            if (debugBossKillButton == null)
            {
                var editorButton =
                    hudRoot.transform.Find("DebugBossKillButton_Editor");
                debugBossKillButton = editorButton == null
                    ? null
                    : editorButton.GetComponent<Button>();
            }

            debugBossKillButton ??= CreateRuntimeButton(
                "DebugBossKillButton_Runtime",
                "보스 처치",
                new Vector2(196f, -150f),
                new Color(0.65f, 0.18f, 0.18f, 1f));

            if (debugBossHealthButton == null)
            {
                var editorButton = hudRoot.transform.Find(
                    "BossStatusPanel/Testbutton/DebugBossHealthButton_Editor");
                debugBossHealthButton = editorButton == null
                    ? null
                    : editorButton.GetComponent<Button>();
            }

            debugBossHealthButton ??= CreateRuntimeButton(
                "DebugBossHealthButton_Runtime",
                "보스 체력 -10%",
                new Vector2(196f, -182f),
                new Color(0.65f, 0.45f, 0.18f, 1f));

            if (debugPhase1Button == null ||
                debugPhase2Button == null ||
                debugPhase3Button == null)
            {
                var phaseRoot = hudRoot.transform.Find("PhaseDebugButtons_Runtime");
                if (phaseRoot == null)
                {
                    var phaseRootObject = new GameObject("PhaseDebugButtons_Runtime");
                    phaseRootObject.transform.SetParent(hudRoot.transform, false);
                    var phaseRect = phaseRootObject.AddComponent<RectTransform>();
                    phaseRect.anchorMin = new Vector2(0f, 1f);
                    phaseRect.anchorMax = new Vector2(0f, 1f);
                    phaseRect.pivot = new Vector2(0f, 1f);
                    phaseRect.anchoredPosition = new Vector2(28f, -180f);
                    phaseRect.sizeDelta = new Vector2(110f, 108f);
                    var grid = phaseRootObject.AddComponent<GridLayoutGroup>();
                    grid.cellSize = new Vector2(110f, 32f);
                    grid.spacing = new Vector2(0f, 6f);
                    grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    grid.constraintCount = 1;
                    phaseRoot = phaseRootObject.transform;
                }

                debugPhase1Button ??= CreateRuntimeButton(
                    "DebugPhase1Button_Runtime",
                    "1 페이즈",
                    Vector2.zero,
                    new Color(0.25f, 0.55f, 0.8f, 1f),
                    phaseRoot);
                debugPhase2Button ??= CreateRuntimeButton(
                    "DebugPhase2Button_Runtime",
                    "2 페이즈",
                    Vector2.zero,
                    new Color(0.7f, 0.45f, 0.15f, 1f),
                    phaseRoot);
                debugPhase3Button ??= CreateRuntimeButton(
                    "DebugPhase3Button_Runtime",
                    "3 페이즈",
                    Vector2.zero,
                    new Color(0.65f, 0.15f, 0.2f, 1f),
                    phaseRoot);
            }
        }

        private Button CreateRuntimeButton(
            string name,
            string labelText,
            Vector2 position,
            Color color,
            Transform parent = null)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent == null ? hudRoot.transform : parent, false);
            var rect = buttonObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(92f, 26f);
            var image = buttonObject.AddComponent<Image>();
            image.color = color;
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            var label = CreateRuntimeText("Label", Vector2.zero, new Vector2(92f, 26f), buttonObject.transform);
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = labelText;
            label.color = Color.white;
            ConfigureRuntimeButtonLabel(label);
            return button;
        }

        private static void ConfigureRuntimeButtonLabel(Text label)
        {
            label.fontSize = 14;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 10;
            label.resizeTextMaxSize = 16;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private Text CreateRuntimeText(string name, Vector2 position, Vector2 size, Transform parent = null)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent == null ? hudRoot.transform : parent, false);
            var rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = textObject.AddComponent<Text>();
            text.font = GetRuntimeFont();
            text.fontSize = 26;
            text.color = Color.white;
            return text;
        }

        private Image CreateRuntimeGauge(string name, Vector2 position, Vector2 size, Color color)
        {
            var backgroundObject = new GameObject($"{name}_Background");
            backgroundObject.transform.SetParent(hudRoot.transform, false);
            var backgroundRect = backgroundObject.AddComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 1f);
            backgroundRect.anchorMax = new Vector2(0f, 1f);
            backgroundRect.pivot = new Vector2(0f, 1f);
            backgroundRect.anchoredPosition = position;
            backgroundRect.sizeDelta = size;
            var background = backgroundObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.45f);

            var fillObject = new GameObject(name);
            fillObject.transform.SetParent(backgroundObject.transform, false);
            var fillRect = fillObject.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = Vector2.zero;
            var fill = fillObject.AddComponent<Image>();
            fill.color = color;
            return fill;
        }

        private static Font GetRuntimeFont()
        {
            if (runtimeKoreanFont != null)
            {
                return runtimeKoreanFont;
            }

            runtimeKoreanFont = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 26);
            return runtimeKoreanFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void ApplyHudLayout()
        {
            var scoreRect = scoreValue == null ? null : scoreValue.rectTransform;
            var timerRect = timerValue == null ? null : timerValue.rectTransform;
            var timeoutRect = debugTimeoutButton == null
                ? null
                : debugTimeoutButton.GetComponent<RectTransform>();
            var bossKillRect = debugBossKillButton == null
                ? null
                : debugBossKillButton.GetComponent<RectTransform>();

            SetTopRight(scoreRect,
                new Vector2(468f, -44f), new Vector2(240f, 34f));
            SetTopRight(timerRect,
                new Vector2(468f, 0f), new Vector2(240f, 36f));
            LayoutTimeoutButton(timeoutRect);
            SetTopRight(
                bossKillRect,
                new Vector2(-32f, -118f),
                new Vector2(110f, 32f));

            if (scoreValue != null)
            {
                scoreValue.alignment = TextAnchor.UpperRight;
            }

            if (timerValue != null)
            {
                timerValue.alignment = TextAnchor.UpperRight;
            }

            if (commanderHeartRoot != null)
            {
                SetTopLeft(
                    commanderHeartRoot,
                    new Vector2(32f, -28f),
                    new Vector2(320f, 48f));
            }

            SetTopLeft(
                commanderStunNotice == null
                    ? null
                    : commanderStunNotice.rectTransform,
                new Vector2(32f, -84f),
                new Vector2(220f, 34f));
        }

        private static void LayoutTimeoutButton(RectTransform buttonRect)
        {
            if (buttonRect == null)
            {
                return;
            }

            if (buttonRect.parent is RectTransform container &&
                container.name == "Testbutton")
            {
                return;
            }

            SetTopRight(
                buttonRect,
                new Vector2(-150f, -118f),
                new Vector2(110f, 32f));
        }

        private static void SetTopLeft(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void SetTopRight(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        public void SetCommanderHeartSprite(Sprite heartSprite)
        {
            if (commanderHeartSprite == heartSprite)
            {
                return;
            }

            commanderHeartSprite = heartSprite;
            renderedCommanderMaxHearts = -1;
            ClearCommanderHearts();
        }

        private void EnsureCommanderHeartRoot()
        {
            if (hudRoot == null || commanderHeartRoot != null)
            {
                return;
            }

            var uiRoot = hudRoot.transform.parent == null
                ? hudRoot.transform
                : hudRoot.transform.parent;
            var existingRoot = FindDescendant(
                uiRoot,
                "CommanderHeartHud_Editor");
            if (existingRoot != null)
            {
                commanderHeartRoot = existingRoot as RectTransform;
                commanderHeartGraphics.Clear();
                commanderHeartGraphics.AddRange(
                    existingRoot.GetComponentsInChildren<Graphic>(true));
                renderedCommanderMaxHearts = commanderHeartGraphics.Count;

                if (commanderHeartSprite == null && commanderHeartGraphics.Count > 0)
                {
                    commanderHeartSprite =
                        (commanderHeartGraphics[0] as Image)?.sprite;
                }

                var existingNotice = FindDescendant(
                    uiRoot,
                    "CommanderStunNotice_Editor");
                commanderStunNotice = existingNotice == null
                    ? null
                    : existingNotice.GetComponent<Text>();
                return;
            }

            var rootObject = new GameObject("CommanderHeartHud_Runtime");
            rootObject.transform.SetParent(uiRoot, false);
            commanderHeartRoot = rootObject.AddComponent<RectTransform>();
            commanderHeartRoot.anchorMin = new Vector2(0f, 1f);
            commanderHeartRoot.anchorMax = new Vector2(0f, 1f);
            commanderHeartRoot.pivot = new Vector2(0f, 1f);
            commanderHeartRoot.anchoredPosition = new Vector2(28f, -24f);
            commanderHeartRoot.sizeDelta = new Vector2(320f, 48f);

            var layout = rootObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            commanderStunNotice = CreateRuntimeText(
                "CommanderStunNotice_Runtime",
                new Vector2(28f, -76f),
                new Vector2(220f, 34f));
            commanderStunNotice.fontSize = 24;
            commanderStunNotice.color = new Color(1f, 0.75f, 0.2f, 1f);
            commanderStunNotice.gameObject.SetActive(false);
        }

        private static Transform FindDescendant(
            Transform root,
            string objectName)
        {
            if (root == null)
            {
                return null;
            }

            var descendants = root.GetComponentsInChildren<Transform>(true);
            foreach (var descendant in descendants)
            {
                if (descendant.name == objectName)
                {
                    return descendant;
                }
            }

            return null;
        }

        private void RenderCommanderHearts(int currentHearts, int maxHearts)
        {
            EnsureCommanderHeartRoot();

            var safeMaxHearts = Mathf.Max(0, maxHearts);
            if (renderedCommanderMaxHearts != safeMaxHearts)
            {
                RebuildCommanderHearts(safeMaxHearts);
            }

            var safeCurrentHearts = Mathf.Clamp(currentHearts, 0, safeMaxHearts);
            if (commanderHeartRoot != null)
            {
                commanderHeartRoot.gameObject.SetActive(
                    safeMaxHearts > 0 && safeCurrentHearts > 0);
            }

            for (var index = 0; index < commanderHeartGraphics.Count; index++)
            {
                var isFull = index < safeCurrentHearts;
                var graphic = commanderHeartGraphics[index];
                graphic.color = isFull
                    ? commanderHeartSprite == null
                        ? new Color(0.95f, 0.15f, 0.2f, 1f)
                        : Color.white
                    : new Color(0.2f, 0.2f, 0.2f, 0.45f);
            }
        }

        private void RebuildCommanderHearts(int maxHearts)
        {
            ClearCommanderHearts();
            renderedCommanderMaxHearts = maxHearts;

            if (commanderHeartRoot == null)
            {
                return;
            }

            for (var index = 0; index < maxHearts; index++)
            {
                var heartObject = new GameObject($"Heart_{index + 1}");
                heartObject.transform.SetParent(commanderHeartRoot, false);
                var rect = heartObject.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(40f, 40f);

                if (commanderHeartSprite != null)
                {
                    var image = heartObject.AddComponent<Image>();
                    image.sprite = commanderHeartSprite;
                    image.preserveAspect = true;
                    commanderHeartGraphics.Add(image);
                    continue;
                }

                var text = heartObject.AddComponent<Text>();
                text.font = GetRuntimeFont();
                text.fontSize = 36;
                text.alignment = TextAnchor.MiddleCenter;
                text.text = "♥";
                text.color = new Color(0.95f, 0.15f, 0.2f, 1f);
                commanderHeartGraphics.Add(text);
            }
        }

        private void ClearCommanderHearts()
        {
            for (var index = 0; index < commanderHeartGraphics.Count; index++)
            {
                var graphic = commanderHeartGraphics[index];
                if (graphic != null)
                {
                    Destroy(graphic.gameObject);
                }
            }

            commanderHeartGraphics.Clear();
        }
        // Image 막대를 전달받은 비율에 맞춰 가로로 줄이는 함수
        private static void SetHorizontalFill(Image fillImage, float ratio)
        {
            if (fillImage == null)
            {
                return;
            }

            // 비율을 무조건 0~1 범위로 제한한다.
            var clampedRatio = Mathf.Clamp01(ratio);
            fillImage.fillAmount = clampedRatio;

            // Source Image가 없는 UGUI Image는 Filled 타입이어도 fillAmount를 화면에 반영하지 않는다.
            // 임시 색상 사각형도 정상 작동하도록 왼쪽을 기준으로 실제 가로 크기를 함께 줄인다.
            var fillTransform = fillImage.rectTransform;
            fillTransform.pivot = new Vector2(0f, fillTransform.pivot.y);
            var localScale = fillTransform.localScale;
            localScale.x = fillImage.sprite == null ? clampedRatio : 1f;
            fillTransform.localScale = localScale;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            GameObject root,
            Image healthFill,
            Image breakFill,
            Text healthValue,
            Text breakValue,
            GameObject notice)
        {
            hudRoot = root;
            bossHealthFill = healthFill;
            breakGaugeFill = breakFill;
            bossHealthValue = healthValue;
            breakGaugeValue = breakValue;
            breakNotice = notice;
        }
#endif
    }
}
