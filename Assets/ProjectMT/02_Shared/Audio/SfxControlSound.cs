using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
namespace ProjectMT.Shared.Audio
{
    [DisallowMultipleComponent]
    public sealed class SfxControlSound : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IMoveHandler, IPointerClickHandler, ISubmitHandler
    {
        private Toggle toggle;
        private Slider slider;
        private Dropdown dropdown;
        private TMP_Dropdown tmpDropdown;
        private bool tracking, dropdownArmed;
        private float before, previousValue;
        private void Awake()
        {
            toggle = GetComponent<Toggle>(); slider = GetComponent<Slider>();
            dropdown = GetComponent<Dropdown>(); tmpDropdown = GetComponent<TMP_Dropdown>();
            if (dropdown != null) dropdown.onValueChanged.AddListener(DropdownChanged);
            if (tmpDropdown != null) tmpDropdown.onValueChanged.AddListener(DropdownChanged);
        }
        private void OnEnable() { previousValue = Value; }
        private void LateUpdate() { previousValue = Value; }
        private float Value => toggle != null ? (toggle.isOn ? 1f : 0f) : slider != null ? slider.value : 0f;
        private void Begin()
        {
            if (tracking) return;
            before = previousValue; tracking = true;
        }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Begin(); }
        public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && isActiveAndEnabled) StartCoroutine(Complete()); }
        public void OnMove(AxisEventData e) { Begin(); if (isActiveAndEnabled) StartCoroutine(Complete()); }
        public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) dropdownArmed = true; }
        public void OnSubmit(BaseEventData e) { dropdownArmed = true; Begin(); if (isActiveAndEnabled) StartCoroutine(Complete()); }
        private IEnumerator Complete()
        {
            yield return null; // UI 값 변경이 끝난 다음 비교
            if (!tracking) yield break;
            tracking = false;
            if (!Mathf.Approximately(before, Value)) SfxEvents.Play2D(toggle != null ? SfxEvents.Toggle : SfxEvents.Slider);
        }
        private void DropdownChanged(int value)
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (!dropdownArmed || selected == null || selected.GetComponent<Toggle>() == null ||
                !selected.transform.IsChildOf(transform)) return;
            dropdownArmed = false;
            SfxEvents.Play2D(SfxEvents.Dropdown);
        }
        private void OnDisable() { tracking = false; dropdownArmed = false; StopAllCoroutines(); }
        private void OnDestroy()
        {
            if (dropdown != null) dropdown.onValueChanged.RemoveListener(DropdownChanged);
            if (tmpDropdown != null) tmpDropdown.onValueChanged.RemoveListener(DropdownChanged);
        }
        public static void ApplyToScene()
        {
            foreach (var selectable in Object.FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!(selectable is Toggle && (selectable.GetComponentInParent<Dropdown>() != null || selectable.GetComponentInParent<TMP_Dropdown>() != null)) &&
                    (selectable is Toggle || selectable is Slider || selectable is Dropdown || selectable is TMP_Dropdown) &&
                    selectable.GetComponent<SfxControlSound>() == null) selectable.gameObject.AddComponent<SfxControlSound>();
        }
    }
}
