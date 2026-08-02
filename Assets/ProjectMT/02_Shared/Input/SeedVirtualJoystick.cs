using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectMT.Shared.Input
{
    [DisallowMultipleComponent]
    public sealed class SeedVirtualJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler // 시드 모바일 조이스틱
    {
        [SerializeField] private RectTransform background; // 입력 기준 원판
        [SerializeField] private RectTransform handle; // 손가락 표시 손잡이
        [SerializeField, Min(10f)] private float movementRange = 70f; // 최대 이동 반경

        public Vector2 Value { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (background == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint))
            {
                return;
            }

            var radius = Mathf.Max(1f, movementRange);
            var clamped = Vector2.ClampMagnitude(localPoint, radius);
            Value = clamped / radius; // -1~1 이동값으로 변환
            if (handle != null)
            {
                handle.anchoredPosition = clamped;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ResetValue();
        }

        private void OnDisable()
        {
            ResetValue();
        }

        private void ResetValue()
        {
            Value = Vector2.zero; // 손을 떼면 즉시 정지
            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(RectTransform joystickBackground, RectTransform joystickHandle, float range)
        {
            background = joystickBackground;
            handle = joystickHandle;
            movementRange = range;
        }
#endif
    }
}
