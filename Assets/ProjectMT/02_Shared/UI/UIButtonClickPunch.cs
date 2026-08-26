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
    // Awake()에서 onClick.AddListener로 거는 대신 IPointerDownHandler를 직접 구현한다.
    // 1) 비활성 오브젝트에 AddComponent하면 Awake 호출이 활성화 시점까지 미뤄지는데, 포인터
    //    이벤트는 오브젝트가 활성 상태여야만 발생하므로 그 시점엔 Awake가 항상 끝나 있다.
    // 2) OnPointerClick 대신 OnPointerDown을 쓴다. Button의 onClick이 먼저 실행되어 저장 로직이
    //    interactable을 false로 바꾸면, 나중에 실행되는 OnPointerClick에서는 펀치가 재생되지
    //    않는 문제가 있었다. 누르는 순간(Down)에 반응하면 그 문제와 무관하게 항상 먼저 재생된다.
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

        // Awake 지연에 대비해(클래스 주석 참고) 클릭 시점에 한 번 더 안전하게 채운다(이미 채워졌으면 무시).
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

        // 버튼 배경이 형제 오브젝트로 분리된 경우(예: Button_02_Red + Button_02_Gray) 버튼만
        // 확대되면 배경이 그대로 남아 어색하므로, 둘을 담은 부모를 대신 움직이게 지정한다.
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
