using ProjectMT.Shared.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectMT.Shared.UI
{
    // 버튼 클릭 효과음. UIButtonClickPunch와 완전히 같은 방식으로 동작한다:
    // 개별 버튼에 수동으로 붙이지 않고, 클릭 펀치를 붙이는 모든 지점에서 함께 붙여주고
    // (UIPanelPopAnimator.OnEnable 등), AudioManager가 주기적으로 씬 전체를 훑어
    // 그 외 상시 노출 HUD 버튼까지 보완한다.
    // OnPointerDown을 쓰는 이유는 UIButtonClickPunch 클래스 주석과 동일: onClick 핸들러가
    // 먼저 실행되어 interactable을 false로 바꾸면 이후의 OnPointerClick은 실행되지 않는다.
    [DisallowMultipleComponent]
    public sealed class UIButtonClickSound : MonoBehaviour, IPointerDownHandler
    {
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (button != null && !button.interactable)
            {
                return;
            }

            AudioManager.PlayButtonClick();
        }

        // buttonObject에 아직 없으면 하나 붙인다. Button 컴포넌트가 없으면 아무 것도 하지 않는다.
        public static void EnsureOn(GameObject buttonObject)
        {
            if (buttonObject == null || buttonObject.GetComponent<Button>() == null)
            {
                return;
            }

            if (buttonObject.GetComponent<UIButtonClickSound>() == null)
            {
                buttonObject.AddComponent<UIButtonClickSound>();
            }
        }

        // root 하위의 모든 Button에 클릭 효과음을 한 번씩 붙인다. 이미 붙어 있으면 건너뛴다.
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
                if (target != null && target.GetComponent<UIButtonClickSound>() == null)
                {
                    target.gameObject.AddComponent<UIButtonClickSound>();
                }
            }
        }

        // 씬 전체(비활성 포함)를 훑어 아직 못 붙은 버튼에 붙인다. AudioManager의 주기적 보완 스캔용.
        public static void ApplyToAllButtonsInScene()
        {
            var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < buttons.Length; i++)
            {
                var target = buttons[i];
                if (target != null && target.GetComponent<UIButtonClickSound>() == null)
                {
                    target.gameObject.AddComponent<UIButtonClickSound>();
                }
            }
        }
    }
}
