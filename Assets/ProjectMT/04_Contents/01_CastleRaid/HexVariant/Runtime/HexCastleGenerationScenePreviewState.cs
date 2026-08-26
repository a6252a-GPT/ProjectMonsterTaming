using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleGenerationScenePreviewState : MonoBehaviour // 임시 Preview가 건드린 상태를 완전히 되돌린다
    {
        [SerializeField] private GameObject hiddenHost;
        [SerializeField] private bool hiddenHostWasActive;
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private bool cameraWasEnabled;
        [SerializeField] private Vector3 cameraPosition;
        [SerializeField] private Quaternion cameraRotation;
        [SerializeField] private bool cameraWasOrthographic;
        [SerializeField] private float orthographicSize;
        [SerializeField] private float fieldOfView;
        [SerializeField] private float nearClipPlane;
        [SerializeField] private float farClipPlane;
        [SerializeField] private CameraClearFlags clearFlags;
        [SerializeField] private Color backgroundColor;

        public void Capture(GameObject host, Camera camera)
        {
            hiddenHost = host;
            hiddenHostWasActive = host != null && host.activeSelf;
            sceneCamera = camera;
            if (camera == null)
            {
                return;
            }

            cameraWasEnabled = camera.enabled;
            cameraPosition = camera.transform.position;
            cameraRotation = camera.transform.rotation;
            cameraWasOrthographic = camera.orthographic;
            orthographicSize = camera.orthographicSize;
            fieldOfView = camera.fieldOfView;
            nearClipPlane = camera.nearClipPlane;
            farClipPlane = camera.farClipPlane;
            clearFlags = camera.clearFlags;
            backgroundColor = camera.backgroundColor;
        }

        public void Restore()
        {
            if (hiddenHost != null)
            {
                hiddenHost.SetActive(hiddenHostWasActive);
            }

            if (sceneCamera == null)
            {
                return;
            }

            sceneCamera.enabled = cameraWasEnabled;
            sceneCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            sceneCamera.orthographic = cameraWasOrthographic;
            sceneCamera.orthographicSize = orthographicSize;
            sceneCamera.fieldOfView = fieldOfView;
            sceneCamera.nearClipPlane = nearClipPlane;
            sceneCamera.farClipPlane = farClipPlane;
            sceneCamera.clearFlags = clearFlags;
            sceneCamera.backgroundColor = backgroundColor;
        }
    }
}
