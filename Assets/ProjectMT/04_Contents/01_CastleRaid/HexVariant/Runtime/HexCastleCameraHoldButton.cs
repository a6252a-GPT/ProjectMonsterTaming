using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class HexCastleCameraHoldButton : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerExitHandler
    {
        [SerializeField] private HexCastleCameraController cameraController;
        [SerializeField, Range(-1, 1)] private int direction = 1;

        private Button button;
        private bool isHolding;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void OnDisable()
        {
            Release();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            button ??= GetComponent<Button>();
            if (cameraController == null || button == null || !button.IsInteractable())
            {
                return;
            }

            isHolding = true;
            if (direction < 0)
            {
                cameraController.BeginRotateLeft();
            }
            else
            {
                cameraController.BeginRotateRight();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
        }

        private void Release()
        {
            if (!isHolding)
            {
                return;
            }

            isHolding = false;
            cameraController?.StopRotation();
        }

#if UNITY_EDITOR
        public void EditorConfigure(HexCastleCameraController controller, int rotateDirection)
        {
            cameraController = controller;
            direction = rotateDirection < 0 ? -1 : 1;
        }
#endif
    }
}
