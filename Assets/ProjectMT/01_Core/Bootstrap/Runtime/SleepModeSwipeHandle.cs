using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectMT.Bootstrap
{
    [AddComponentMenu("")]
    public sealed class SleepModeSwipeHandle : Slider // 손잡이에서 시작한 드래그만 해제로 인정
    {
        [SerializeField] private SleepModeController owner;
        [SerializeField, Range(0.5f, 1f)] private float wakeThreshold = 0.9f;
        private int? activePointer;

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!IsActive() || !IsInteractable() || activePointer.HasValue ||
                eventData.button != PointerEventData.InputButton.Left || handleRect == null ||
                !RectTransformUtility.RectangleContainsScreenPoint(handleRect, eventData.position, eventData.pressEventCamera))
            {
                return;
            }
            activePointer = eventData.pointerId;
            base.OnPointerDown(eventData);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (activePointer == eventData.pointerId)
            {
                base.OnDrag(eventData);
            }
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (activePointer != eventData.pointerId)
            {
                return;
            }
            base.OnPointerUp(eventData);
            var shouldWake = normalizedValue >= wakeThreshold;
            ResetSwipe();
            if (shouldWake)
            {
                owner?.Wake(); // 포인터를 놓을 때 닫아 뒤쪽 버튼으로 입력이 새지 않게 한다
            }
        }

        public override void OnMove(AxisEventData eventData) { }

        protected override void OnDisable()
        {
            ResetSwipe();
            base.OnDisable();
        }

        public void ResetSwipe()
        {
            activePointer = null;
            SetValueWithoutNotify(minValue);
        }

#if UNITY_EDITOR
        public void EditorConfigure(SleepModeController controller)
        {
            owner = controller;
            minValue = 0f;
            maxValue = 1f;
            wholeNumbers = false;
            direction = Direction.LeftToRight;
            navigation = new Navigation { mode = Navigation.Mode.None };
            transition = Transition.None;
            ResetSwipe();
        }
#endif
    }
}