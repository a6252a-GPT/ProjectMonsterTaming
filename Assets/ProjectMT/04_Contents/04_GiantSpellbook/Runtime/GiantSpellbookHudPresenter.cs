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
            bool isBroken)
        {
            BossHealth = bossHealth;
            BossMaxHealth = bossMaxHealth;
            RemainingBreakGauge = remainingBreakGauge;
            MaxBreakGauge = maxBreakGauge;
            IsBroken = isBroken;
        }

        public float BossHealth { get; }
        public float BossMaxHealth { get; }
        public float RemainingBreakGauge { get; }
        public float MaxBreakGauge { get; }
        public bool IsBroken { get; }
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

        private GiantSpellbookController controller;

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
        }
        // HUD 전체의 표시 여부
        public void SetVisible(bool visible)
        {
            if (hudRoot != null)
            {
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

            SetHorizontalFill(bossHealthFill, healthRatio);
            SetHorizontalFill(breakGaugeFill, breakRatio);

            if (bossHealthValue != null)
            {
                //소수점 올림해서 정수처럼 표시
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
