using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    [DisallowMultipleComponent]
    public sealed class FallenCommanderCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform followTarget;
        [SerializeField, Min(0f)] private float followSmoothing = 8f;

        private void LateUpdate()
        {
            if (!Application.isPlaying || followTarget == null)
            {
                return;
            }

            var targetPosition = followTarget.position;
            targetPosition.y = transform.position.y;

            if (followSmoothing <= 0f)
            {
                transform.position = targetPosition;
                return;
            }

            var t = 1f - Mathf.Exp(-followSmoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, t);
        }

#if UNITY_EDITOR
        public void EditorConfigure(Transform target, float smoothing)
        {
            followTarget = target;
            followSmoothing = Mathf.Max(0f, smoothing);
        }
#endif
    }
}
