using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.GiantSpellbook
{
    // HUD에 표시할 값을 한 번에 전달하기 위한 데이터 묶음
    public readonly struct GiantSpellbookHudState
    {
        public GiantSpellbookHudState(
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
            float breakDuration)
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
    }

    // 같은 GameObject에 이 컴포넌트를 여러 개 추가하지 못하게 막는다.
    [DisallowMultipleComponent]
    public sealed class GiantSpellbookHudPresenter : MonoBehaviour
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
        [SerializeField] private Button debugBasicAttackButton;
        [SerializeField] private Button debugHandSlamButton;
        [SerializeField] private Button debugMarkStrikeButton;
        [SerializeField] private Button debugWideBurstButton;

        private GiantSpellbookController controller;
        private static Font runtimeKoreanFont;

        // Controller와 HUD를 연결한다.
        //Controller.Initialize()에서 호출
        public void Bind(GiantSpellbookController targetController)
        {
            //기존 이벤트 구독을 먼저 해제
            Unbind();

            controller = targetController;
            if (controller != null)
            {
                // Controller가 HudStateChanged 이벤트를 발생시키면
                // 이 Presenter의 Render()를 실행한다.
                controller.HudStateChanged += Render;
            }

            SetVisible(true);
            EnsureRuntimeControls();
            debugTimeoutButton?.onClick.RemoveListener(HandleDebugTimeout);
            debugTimeoutButton?.onClick.AddListener(HandleDebugTimeout);
            debugBasicAttackButton?.onClick.RemoveListener(HandleDebugBasicAttack);
            debugBasicAttackButton?.onClick.AddListener(HandleDebugBasicAttack);
            debugHandSlamButton?.onClick.RemoveListener(HandleDebugHandSlam);
            debugHandSlamButton?.onClick.AddListener(HandleDebugHandSlam);
            debugMarkStrikeButton?.onClick.RemoveListener(HandleDebugMarkStrike);
            debugMarkStrikeButton?.onClick.AddListener(HandleDebugMarkStrike);
            debugWideBurstButton?.onClick.RemoveListener(HandleDebugWideBurst);
            debugWideBurstButton?.onClick.AddListener(HandleDebugWideBurst);
        }

        // Controller와 HUD의 연결을 해제
        public void Unbind()
        {
            if (controller == null)
            {
                return;
            }

            // Bind()에서 등록했던 Render()를 이벤트에서 제거
            controller.HudStateChanged -= Render; 
            controller = null;
            debugTimeoutButton?.onClick.RemoveListener(HandleDebugTimeout);
            debugBasicAttackButton?.onClick.RemoveListener(HandleDebugBasicAttack);
            debugHandSlamButton?.onClick.RemoveListener(HandleDebugHandSlam);
            debugMarkStrikeButton?.onClick.RemoveListener(HandleDebugMarkStrike);
            debugWideBurstButton?.onClick.RemoveListener(HandleDebugWideBurst);
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

                hudRoot.SetActive(visible);
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }
        // Controller가 새로운 HUD 상태를 전달하면 호출
        private void Render(GiantSpellbookHudState state)
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
            controller?.DebugTimeout();
        }

        private void HandleDebugBasicAttack()
        {
            controller?.DebugBasicAttack();
        }

        private void HandleDebugHandSlam()
        {
            controller?.DebugHandSlam();
        }

        private void HandleDebugMarkStrike()
        {
            controller?.DebugMarkStrike();
        }

        private void HandleDebugWideBurst()
        {
            controller?.DebugWideBurst();
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

            if (debugTimeoutButton == null)
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
            debugMarkStrikeButton ??= CreateRuntimeButton(
                "DebugMarkStrikeButton_Runtime",
                "위치 공격",
                new Vector2(228f, -182f),
                new Color(0.3f, 0.65f, 0.8f, 1f));
            debugWideBurstButton ??= CreateRuntimeButton(
                "DebugWideBurstButton_Runtime",
                "광역기",
                new Vector2(328f, -182f),
                new Color(0.7f, 0.25f, 0.75f, 1f));
        }

        private Button CreateRuntimeButton(string name, string labelText, Vector2 position, Color color)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(hudRoot.transform, false);
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
