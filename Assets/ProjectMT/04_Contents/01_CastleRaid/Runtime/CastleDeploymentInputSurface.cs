using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectMT.Contents.CastleRaid
{
    [DisallowMultipleComponent]
    public sealed class CastleDeploymentInputSurface : MonoBehaviour, IPointerClickHandler // UI 클릭을 월드 배치로 전달
    {
        [SerializeField] private CastleRaidController controller; // 실제 배치 판정 담당

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null)
            {
                controller?.TryDeployAtScreenPosition(eventData.position);
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(CastleRaidController raidController)
        {
            controller = raidController;
        }
#endif
    }
}
