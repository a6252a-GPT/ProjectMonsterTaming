using UnityEngine;

namespace ProjectMT.Contents.GuardianTrial
{
    // 08.07 안건준 추가 - 수호자의 탑 전용 카메라 추적.
    // 기존에는 카메라가 한 자리에 고정되어 있었는데, 군단장(followTarget) 위치를 따라
    // 카메라 루트(06_CameraRoot)를 이동시켜 화면이 플레이어를 따라오도록 한다.
    // 카메라 본체(GuardiansTowerCamera)에는 손대지 않으므로 흔들림 연출(CameraImpulseRig)과
    // 충돌하지 않는다. (Rig는 카메라 자신의 로컬 위치만 흔들고, 이 스크립트는 그 부모를 옮긴다)
    // 다른 던전에는 영향이 없는 수호자의 탑 전용 컴포넌트다.
    [DisallowMultipleComponent]
    public sealed class GuardiansTowerCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform cameraRoot; // 이동시킬 카메라의 부모 (06_CameraRoot)
        [SerializeField] private Transform followTarget; // 추적 대상 (Commander)
        [SerializeField, Min(0f)] private float followSmoothing = 8f; // 클수록 즉시 따라감(0이면 완전 즉시 스냅)

        private void Awake()
        {
            if (cameraRoot == null)
            {
                cameraRoot = transform;
            }
        }

        private void LateUpdate()
        {
            if (cameraRoot == null || followTarget == null)
            {
                return;
            }

            var targetPosition = followTarget.position;
            targetPosition.y = cameraRoot.position.y; // 카메라 높이는 고정, 수평(X/Z)만 추적

            if (followSmoothing <= 0f)
            {
                cameraRoot.position = targetPosition;
                return;
            }

            var t = 1f - Mathf.Exp(-followSmoothing * Time.deltaTime); // 프레임레이트 독립적인 지수 감쇠
            cameraRoot.position = Vector3.Lerp(cameraRoot.position, targetPosition, t);
        }

#if UNITY_EDITOR
        public void EditorConfigure(Transform root, Transform target, float smoothing)
        {
            cameraRoot = root;
            followTarget = target;
            followSmoothing = smoothing;
        }
#endif
    }
}
