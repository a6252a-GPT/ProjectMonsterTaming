using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit
{
    public class MazeCameraFollow : MonoBehaviour
    {
        [Header("추적 대상")]
        public Transform target;

        [Header("카메라 오프셋 설정")]
        [SerializeField] private Vector3 offset = new Vector3(0, 5f, -2f);
        [SerializeField] private float followSpeed = 10f;

        private float shakeRemaining;
        private float shakeDuration;
        private float shakeStrength;

        public void BindTarget(Transform followTarget, bool snapImmediate = true)
        {
            target = followTarget;
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

            transform.position = target.position + offset;
            transform.LookAt(target.position);
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

            Vector3 targetPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
            if (shakeRemaining > 0f)
            {
                shakeRemaining = Mathf.Max(0f, shakeRemaining - Time.deltaTime);
                float falloff = shakeRemaining / shakeDuration;
                transform.position += Random.insideUnitSphere * (shakeStrength * falloff);
            }

            transform.LookAt(target.position);
        }
    }
}
