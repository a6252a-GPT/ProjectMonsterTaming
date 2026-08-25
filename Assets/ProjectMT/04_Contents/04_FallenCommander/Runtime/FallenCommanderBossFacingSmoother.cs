using UnityEngine;

namespace ProjectMT.Contents.FallenCommander
{
    [DisallowMultipleComponent]
    public sealed class FallenCommanderBossFacingSmoother : MonoBehaviour
    {
        private Transform target;
        private Quaternion smoothedRotation;
        private float turnSpeed;
        private bool isActive;
        private bool isTrackingEnabled;

        public void Configure(Transform facingTarget, float degreesPerSecond)
        {
            target = facingTarget;
            turnSpeed = Mathf.Max(1f, degreesPerSecond);
            isTrackingEnabled = true;
            isActive = target != null;
            FaceTargetImmediately();
        }

        // 보스가 등장하는 첫 프레임부터 대상을 바라보도록 회전을 즉시 맞춘다.
        private void FaceTargetImmediately()
        {
            smoothedRotation = transform.rotation;
            if (target == null)
            {
                return;
            }

            var direction = target.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            smoothedRotation = Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);
            transform.rotation = smoothedRotation;
        }

        public void SetTrackingEnabled(bool enabled)
        {
            isTrackingEnabled = enabled;
        }

        public void Shutdown()
        {
            isActive = false;
            isTrackingEnabled = false;
            target = null;
        }

        private void LateUpdate()
        {
            if (!isActive)
            {
                return;
            }

            if (isTrackingEnabled && target != null)
            {
                var direction = target.position - transform.position;
                direction.y = 0f;

                if (direction.sqrMagnitude >= 0.0001f)
                {
                    var targetRotation = Quaternion.LookRotation(
                        direction.normalized,
                        Vector3.up);

                    smoothedRotation = Quaternion.RotateTowards(
                        smoothedRotation,
                        targetRotation,
                        turnSpeed * Time.deltaTime);
                }
            }

            transform.rotation = smoothedRotation;
        }
    }
}
