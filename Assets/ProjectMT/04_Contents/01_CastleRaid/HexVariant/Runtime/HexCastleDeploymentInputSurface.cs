using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleDeploymentInputSurface : MonoBehaviour, IPointerClickHandler,
        IPointerDownHandler, IPointerUpHandler, IDragHandler, IScrollHandler // 배치와 카메라 입력을 한 면에서 처리
    {
        [SerializeField] private HexCastleRaidController controller;
        [SerializeField] private HexCastleCameraController cameraController;

        private void OnEnable()
        {
            cameraController?.SetExternalPointerInput(true);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null &&
                eventData.button == PointerEventData.InputButton.Left &&
                (cameraController == null || !cameraController.ConsumeClickSuppression(eventData.pointerId)))
            {
                controller?.TryDeployAtScreenPosition(eventData.position);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData != null)
            {
                cameraController?.BeginPointer(eventData.pointerId, eventData.position);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData != null)
            {
                cameraController?.MovePointer(eventData.pointerId, eventData.position);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData != null)
            {
                cameraController?.EndPointer(eventData.pointerId);
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (eventData != null)
            {
                cameraController?.ZoomByScroll(eventData.position, eventData.scrollDelta.y);
            }
        }

        private void OnDisable()
        {
            cameraController?.CancelPointers();
            cameraController?.SetExternalPointerInput(false);
        }

        public void Configure(
            HexCastleRaidController raidController,
            HexCastleCameraController raidCameraController)
        {
            if (cameraController != raidCameraController)
            {
                cameraController?.SetExternalPointerInput(false);
            }

            controller = raidController;
            cameraController = raidCameraController;
            cameraController?.SetExternalPointerInput(isActiveAndEnabled);
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            HexCastleRaidController raidController,
            HexCastleCameraController raidCameraController)
        {
            Configure(raidController, raidCameraController);
        }
#endif
    }
}
