using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectMT.Shared.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonColorFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISubmitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Graphic[] graphics = Array.Empty<Graphic>();
        [SerializeField] private Color disabledTint = Color.white;
        private Button button;
        private bool hovered;
        private bool pressed;
        private bool selected;
        private float submitUntil;

        private void Awake() => button = GetComponent<Button>();
        private void OnEnable()
        {
            button = GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            hovered = pressed = false;
            selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            submitUntil = 0f;
        }

        private void LateUpdate()
        {
            var enabledInput = button != null && button.IsActive() && button.IsInteractable();
            if (!enabledInput) { pressed = false; submitUntil = 0f; }
            var factor = enabledInput && ((pressed && hovered) || Time.unscaledTime < submitUntil) ? 0.76f
                : enabledInput && hovered ? 1.08f : 1f;
            var tint = enabledInput ? new Color(factor, factor, factor, 1f) : disabledTint;
            if (enabledInput && factor == 1f && selected) tint = button.colors.selectedColor;
            foreach (var graphic in graphics)
            {
                if (graphic == null) continue;
                if (graphic.canvasRenderer.GetColor() != tint) graphic.canvasRenderer.SetColor(tint); // 선택·잠금의 Graphic.color는 보존한다.
            }
        }

        private void OnDisable()
        {
            hovered = pressed = false;
            submitUntil = 0f;
            foreach (var graphic in graphics)
                if (graphic != null) graphic.canvasRenderer.SetColor(Color.white);
        }

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) => hovered = false;
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && button.IsInteractable()) hovered = pressed = true;
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) pressed = false;
        }
        public void OnSubmit(BaseEventData eventData)
        {
            if (button.IsInteractable()) submitUntil = Time.unscaledTime + 0.12f;
        }
        public void OnSelect(BaseEventData eventData) => selected = true;
        public void OnDeselect(BaseEventData eventData) => selected = false;
    }
}
