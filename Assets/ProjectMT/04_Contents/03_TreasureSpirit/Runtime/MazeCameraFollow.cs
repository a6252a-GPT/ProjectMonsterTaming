using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit
{
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class MazeCameraFollow : MonoBehaviour
    {
        public static readonly Vector3 DefaultOffset = new Vector3(0f, 8f, -3f);

        [Header("추적 대상")]
        [SerializeField] private Transform target;

        [Header("카메라 오프셋 설정")]
        [SerializeField] private Vector3 offset = DefaultOffset;
        [SerializeField] private float followSpeed = 10f;
        [SerializeField, Range(30f, 80f)] private float fieldOfView = 50f;

        private Camera followCamera;
        private Quaternion followRotation;
        private float shakeRemaining;
        private float shakeDuration;
        private float shakeStrength;

        public Transform Target => target;

        public static MazeCameraFollow ResolveFrom(Transform origin)
        {
            if (origin != null)
            {
                var follow = origin.root.GetComponentInChildren<MazeCameraFollow>(true);
                if (follow != null)
                {
                    return follow;
                }
            }

            var mainCamera = Camera.main;
            return mainCamera != null ? mainCamera.GetComponent<MazeCameraFollow>() : null;
        }

        private void Awake()
        {
            followCamera = GetComponent<Camera>();
            CacheFollowRotation();
            ApplyPerspective();
        }

        private void OnValidate()
        {
            CacheFollowRotation();
            ApplyPerspective();
        }

        public void BindTarget(Transform followTarget, bool snapImmediate = true)
        {
            target = followTarget;
            CacheFollowRotation();
            ApplyPerspective();
            if (snapImmediate)
            {
                SnapToTarget();
            }
        }

        public void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            transform.SetPositionAndRotation(target.position + offset, followRotation);
        }

        public void PlayHitShake(float duration = 0.2f, float strength = 0.16f)
        {
            shakeDuration = Mathf.Max(0.05f, duration);
            shakeRemaining = shakeDuration;
            shakeStrength = Mathf.Max(0f, strength);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var blend = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            var followPosition = Vector3.Lerp(transform.position, target.position + offset, blend);
            if (shakeRemaining > 0f)
            {
                shakeRemaining = Mathf.Max(0f, shakeRemaining - Time.deltaTime);
                var falloff = shakeRemaining / shakeDuration;
                followPosition += Random.insideUnitSphere * (shakeStrength * falloff);
            }

            transform.SetPositionAndRotation(followPosition, followRotation);
        }

        private void CacheFollowRotation()
        {
            var lookDirection = -offset;
            if (lookDirection.sqrMagnitude < 0.0001f)
            {
                lookDirection = Vector3.forward;
            }

            followRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        private void ApplyPerspective()
        {
            if (followCamera == null)
            {
                followCamera = GetComponent<Camera>();
            }

            if (followCamera == null)
            {
                return;
            }

            followCamera.orthographic = false;
            followCamera.fieldOfView = Mathf.Clamp(fieldOfView, 30f, 80f);
        }
    }
}
