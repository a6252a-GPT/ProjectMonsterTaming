using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectMT.Contents.CastleRaid
{
    [DisallowMultipleComponent]
    public sealed class CastleDeploymentInputSurface : MonoBehaviour, IPointerClickHandler,
        IPointerDownHandler, IPointerUpHandler, IDragHandler, IScrollHandler // 탭 배치·카메라 조작 분리
    {
        [SerializeField] private CastleRaidController controller; // 실제 배치 판정 담당
        [SerializeField] private CastleRaidCameraController cameraController;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null &&
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
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            CastleRaidController raidController,
            CastleRaidCameraController raidCameraController = null)
        {
            controller = raidController;
            cameraController = raidCameraController;
        }
#endif
    }
}
