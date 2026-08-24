using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectMT.Shared.UI
{
    // 버튼을 누르면 짧게 줄어들었다가 살짝 튕기며 돌아오는 클릭 피드백(모바일 게임에서 흔한 "버튼 펀치").
    // 개별 버튼에 수동으로 붙이지 않고, UIPanelPopAnimator.OnEnable이 하위 버튼 전체에 자동으로 붙여준다.
    // 상시 노출되는 HUD 고정 버튼은 EnsureOn(...)으로 직접 붙인다.
    //
    // button.onClick.AddListener(...)를 Awake()에서 거는 대신 IPointerDownHandler를 직접 구현한다.
    // 이유 1) 비활성 오브젝트에 AddComponent로 붙으면 Unity가 Awake 호출 자체를 그 오브젝트가
    // 처음 활성화될 때까지 미루는데, 그 타이밍은 호출부마다 달라 보장하기 어렵다. 클릭 이벤트는
    // 오브젝트가 반드시 활성 상태여야만 발생하므로(비활성 UI는 클릭할 수 없다), 포인터 이벤트
    // 시점에는 Awake가 항상 이미 끝나 있음이 보장된다.
    // 이유 2) OnPointerClick(뗄 때) 대신 OnPointerDown(누를 때)을 쓴다. 저장 로직이 있는 버튼들은
    // onClick 리스너 안에서 중복 클릭 방지를 위해 button.interactable을 즉시 false로 바꾸는데,
    // Unity는 한 오브젝트에 여러 IPointerClickHandler가 있으면 먼저 붙은 컴포넌트 순서대로
    // 호출한다(Button이 먼저, 나중에 AddComponent된 이 스크립트가 나중). 그래서 OnPointerClick을
    // 쓰면 Button의 onClick(=interactable을 false로 바꾸는 로직)이 먼저 실행된 뒤에 이 스크립트가
    // 호출되어, interactable 체크에 걸려 펀치가 재생되지 않는 문제가 있었다. 누르는 순간(Down)에
    // 반응하면 그 문제와 무관하게 항상 먼저 재생된다.
    [DisallowMultipleComponent]
    public sealed class UIButtonClickPunch : MonoBehaviour, IPointerDownHandler
    {
        private const float PunchScale = 0.92f;
        private const float DownDuration = 0.07f;
        private const float UpDuration = 0.13f;

        private Button button;
        private Transform visualTarget;
        private Vector3 baseScale;
        private bool baseScaleCaptured;
        private Sequence sequence;

        private void Awake()
        {
            CaptureState();
        }

        private void OnDisable()
        {
            sequence?.Kill();
            sequence = null;
            if (baseScaleCaptured)
            {
                visualTarget.localScale = baseScale;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            CaptureState();
            if (button != null && !button.interactable)
            {
                return;
            }

            PlayPunch();
        }

        // Awake가 지연될 수 있으므로(클래스 주석 참고), 실제로 필요한 시점(클릭)에 한 번 더
        // 안전하게 값을 채운다. 이미 채워져 있으면 아무 것도 하지 않는다.
        private void CaptureState()
        {
            if (baseScaleCaptured)
            {
                return;
            }

            button = GetComponent<Button>();
            if (visualTarget == null)
            {
                visualTarget = transform;
            }

            baseScale = visualTarget.localScale;
            baseScaleCaptured = true;
        }

        private void PlayPunch()
        {
            sequence?.Kill();
            visualTarget.localScale = baseScale;
            sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Append(visualTarget.DOScale(baseScale * PunchScale, DownDuration).SetEase(Ease.OutQuad));
            sequence.Append(visualTarget.DOScale(baseScale, UpDuration).SetEase(Ease.OutBack));
        }

        // 버튼 배경과 강조 이미지가 형제 오브젝트로 분리돼 있어(예: Button_02_Red 위에
        // Button_02_Gray가 따로 있는 구조) 버튼 자신만 확대/축소하면 배경만 그대로 남아 어색해
        // 보이는 경우, visualTarget을 지정해 그 상위(둘을 함께 담은 부모)를 대신 움직인다.
        public void SetVisualTarget(Transform target)
        {
            if (target == null || target == visualTarget)
            {
                return;
            }

            visualTarget = target;
            baseScaleCaptured = false;
            CaptureState();
        }

        // buttonObject에 아직 없으면 하나 붙인다. Button 컴포넌트가 없으면 아무 것도 하지 않는다.
        // visualRoot를 주면 버튼 자신 대신 그 트랜스폼을 스케일 애니메이션 대상으로 쓴다.
        public static void EnsureOn(GameObject buttonObject, Transform visualRoot = null)
        {
            if (buttonObject == null || buttonObject.GetComponent<Button>() == null)
            {
                return;
            }

            var punch = buttonObject.GetComponent<UIButtonClickPunch>();
            if (punch == null)
            {
                punch = buttonObject.AddComponent<UIButtonClickPunch>();
            }

            if (visualRoot != null)
            {
                punch.SetVisualTarget(visualRoot);
            }
        }

        // root 하위의 모든 Button에 클릭 펀치를 한 번씩 붙인다. 이미 붙어 있으면 건너뛴다.
        public static void ApplyToAllButtonsUnder(Transform root)
        {
            if (root == null)
            {
                return;
            }

            var buttons = root.GetComponentsInChildren<Button>(true);
            for (var i = 0; i < buttons.Length; i++)
            {
                var target = buttons[i];
                if (target != null && target.GetComponent<UIButtonClickPunch>() == null)
                {
                    target.gameObject.AddComponent<UIButtonClickPunch>();
                }
            }
        }
    }
}
