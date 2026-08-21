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
            smoothedRotation = transform.rotation;
            isTrackingEnabled = true;
            isActive = target != null;
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
