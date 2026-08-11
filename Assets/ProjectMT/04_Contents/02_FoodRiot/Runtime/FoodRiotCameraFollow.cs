using UnityEngine;

namespace ProjectMT.Contents.FoodRiot
{
    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    public sealed class FoodRiotCameraFollow : MonoBehaviour // 군단장 중심 카메라 추적
    {
        [SerializeField] private Transform followTarget;
        [SerializeField] private Vector2 maxFollowOffset = new Vector2(3.5f, 5f);
        [SerializeField, Min(0f)] private float followSharpness = 6f;

        private Vector3 initialCameraPosition;
        private Vector3 initialTargetPosition;
        private Vector3 defaultLocalPosition;
        private bool hasDefaultLocalPosition;
        private bool initialized;

        public Transform FollowTarget => followTarget;
        public Vector2 MaxFollowOffset => maxFollowOffset;

        private void Awake()
        {
            CacheDefaultPosition();
        }

        private void OnEnable()
        {
            if (!hasDefaultLocalPosition)
            {
                CacheDefaultPosition();
            }

            transform.localPosition = defaultLocalPosition;
            initialized = false; // 콘텐츠 초기화 뒤 첫 프레임에 기준점 확정
        }

        private void OnDisable()
        {
            if (hasDefaultLocalPosition)
            {
                transform.localPosition = defaultLocalPosition;
            }

            initialized = false;
        }

        private void LateUpdate()
        {
            if (followTarget == null)
            {
                initialized = false;
                return;
            }

            if (!initialized)
            {
                ResetAnchor();
            }

            var targetDelta = followTarget.position - initialTargetPosition;
            targetDelta.x = Mathf.Clamp(targetDelta.x, -maxFollowOffset.x, maxFollowOffset.x);
            targetDelta.y = 0f;
            targetDelta.z = Mathf.Clamp(targetDelta.z, -maxFollowOffset.y, maxFollowOffset.y);
            var desiredPosition = initialCameraPosition + targetDelta;
            var followWeight = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, followWeight);
        }

        private void ResetAnchor()
        {
            if (followTarget == null)
            {
                initialized = false;
                return;
            }

            initialCameraPosition = transform.position;
            initialTargetPosition = followTarget.position;
            initialized = true;
        }

        private void CacheDefaultPosition()
        {
            defaultLocalPosition = transform.localPosition;
            hasDefaultLocalPosition = true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(Transform target, Vector2 followOffset, float sharpness)
        {
            followTarget = target;
            maxFollowOffset = new Vector2(
                Mathf.Max(0f, followOffset.x),
                Mathf.Max(0f, followOffset.y));
            followSharpness = Mathf.Max(0f, sharpness);
        }
#endif
    }
}
