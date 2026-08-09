using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class HudMenuOutsideTapCatcher : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerClickHandler // 짧은 바깥 터치만 닫기
    {
        [SerializeField] private HudQuickMenuController menu;
        [SerializeField] private float maximumTapDistance = 24f;
        [SerializeField] private float maximumTapDuration = 0.4f;

        private Vector2 pressedPosition;
        private float pressedTime;
        private bool pressed;
        private bool eligibleTap;

        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = true;
            eligibleTap = false;
            pressedPosition = eventData.position;
            pressedTime = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!pressed)
            {
                return;
            }

            pressed = false;
            var shortEnough = Time.unscaledTime - pressedTime <= maximumTapDuration;
            var closeEnough = Vector2.Distance(pressedPosition, eventData.position) <= maximumTapDistance;
            eligibleTap = shortEnough && closeEnough;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eligibleTap)
            {
                menu?.CloseMenu();
            }

            eligibleTap = false;
        }

        private void OnDisable()
        {
            pressed = false;
            eligibleTap = false;
        }

#if UNITY_EDITOR
        public void EditorConfigure(HudQuickMenuController controller)
        {
            menu = controller;
        }
#endif
    }
}
