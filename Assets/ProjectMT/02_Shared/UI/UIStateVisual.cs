using System;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Shared.UI
{
    [DisallowMultipleComponent]
    public sealed class UIStateVisual : MonoBehaviour
    {
        [Serializable]
        public struct Target
        {
            public Graphic Graphic;
            public Color Active;
            public Color Inactive;
        }

        [SerializeField] private GameObject stateIndicator;
        [SerializeField] private Selectable interactableSource;
        [SerializeField] private bool manualState;
        [SerializeField] private Target[] targets = Array.Empty<Target>();

        public void SetSelected(bool selected)
        {
            manualState = selected;
            Refresh();
        }

        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            var active = stateIndicator != null ? stateIndicator.activeSelf
                : interactableSource != null ? interactableSource.IsInteractable() : manualState;
            foreach (var target in targets)
            {
                if (target.Graphic == null) continue;
                var color = active ? target.Active : target.Inactive;
                if (target.Graphic.color != color) target.Graphic.color = color; // 색상만 담당하며 입력과 배치는 원래 컨트롤러가 유지한다.
            }
        }
    }
}
