using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Shared.UI
{
    // 버튼을 누르면 짧게 줄어들었다가 살짝 튕기며 돌아오는 클릭 피드백(모바일 게임에서 흔한 "버튼 펀치").
    // 개별 버튼에 수동으로 붙이지 않고, UIPanelPopAnimator.OnEnable이 하위 버튼 전체에 자동으로 붙여준다.
    // 상시 노출되는 HUD 고정 버튼은 EnsureOn(...)으로 직접 붙인다.
    [DisallowMultipleComponent]
    public sealed class UIButtonClickPunch : MonoBehaviour
    {
        private const float PunchScale = 0.92f;
        private const float DownDuration = 0.07f;
        private const float UpDuration = 0.13f;

        private Button button;
        private Vector3 baseScale;
        private Sequence sequence;

        private void Awake()
        {
            button = GetComponent<Button>();
            baseScale = transform.localScale;
            button.onClick.AddListener(PlayPunch);
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(PlayPunch);
            }
        }

        private void OnDisable()
        {
            sequence?.Kill();
            sequence = null;
            transform.localScale = baseScale;
        }

        private void PlayPunch()
        {
            if (button != null && !button.interactable)
            {
                return;
            }

            sequence?.Kill();
            transform.localScale = baseScale;
            sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Append(transform.DOScale(baseScale * PunchScale, DownDuration).SetEase(Ease.OutQuad));
            sequence.Append(transform.DOScale(baseScale, UpDuration).SetEase(Ease.OutBack));
        }

        // buttonObject에 아직 없으면 하나 붙인다. Button 컴포넌트가 없으면 아무 것도 하지 않는다.
        public static void EnsureOn(GameObject buttonObject)
        {
            if (buttonObject == null || buttonObject.GetComponent<Button>() == null)
            {
                return;
            }

            if (buttonObject.GetComponent<UIButtonClickPunch>() == null)
            {
                buttonObject.AddComponent<UIButtonClickPunch>();
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
