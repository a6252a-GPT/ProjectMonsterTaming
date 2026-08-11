using UnityEngine;

namespace ProjectMT.Shared.Animation
{
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class CommanderLocomotionPresenter : MonoBehaviour // 직접 이동 군단장 애니 구동
    {
        [SerializeField] private Transform motionSource;
        [SerializeField] private Animator animator;
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField, Min(0f)] private float movingSpeedThreshold = 0.05f;
        [SerializeField, Min(0f)] private float speedDampTime = 0.08f;

        private int speedHash;
        private Vector3 previousPosition;
        private bool canDriveSpeed;
        private bool hasPreviousPosition;

        public Transform MotionSource => motionSource;
        public Animator TargetAnimator => animator;
        public float CurrentPlanarSpeed { get; private set; }
        public float CurrentSpeedValue { get; private set; }
        public bool CanDriveSpeed => canDriveSpeed;

        private void Awake()
        {
            ResolveBindings();
        }

        private void OnEnable()
        {
            ResolveBindings();
            previousPosition = motionSource == null ? transform.position : motionSource.position;
            hasPreviousPosition = true;
            SetSpeedImmediate(0f);
        }

        private void OnDisable()
        {
            SetSpeedImmediate(0f);
            hasPreviousPosition = false;
        }

        private void LateUpdate()
        {
            if (!canDriveSpeed || animator == null || motionSource == null)
            {
                ResolveBindings();
                if (!canDriveSpeed || motionSource == null)
                {
                    return;
                }
            }

            var currentPosition = motionSource.position;
            if (!hasPreviousPosition)
            {
                previousPosition = currentPosition;
                hasPreviousPosition = true;
                return;
            }

            var delta = currentPosition - previousPosition;
            delta.y = 0f;
            previousPosition = currentPosition;

            var deltaTime = Time.deltaTime;
            CurrentPlanarSpeed = deltaTime > 0f ? delta.magnitude / deltaTime : 0f;
            CurrentSpeedValue = CurrentPlanarSpeed > movingSpeedThreshold ? 1f : 0f;
            animator.SetFloat(speedHash, CurrentSpeedValue, speedDampTime, deltaTime);
        }

        private void ResolveBindings()
        {
            if (motionSource == null)
            {
                motionSource = transform;
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            speedHash = Animator.StringToHash(speedParameter);
            canDriveSpeed = HasFloatParameter(animator, speedHash);
        }

        private void SetSpeedImmediate(float value)
        {
            CurrentPlanarSpeed = 0f;
            CurrentSpeedValue = value;
            if (canDriveSpeed && animator != null)
            {
                animator.SetFloat(speedHash, value);
            }
        }

        private static bool HasFloatParameter(Animator target, int parameterHash)
        {
            if (target == null || target.runtimeAnimatorController == null)
            {
                return false;
            }

            var parameters = target.parameters;
            for (var index = 0; index < parameters.Length; index++)
            {
                var parameter = parameters[index];
                if (parameter.nameHash == parameterHash && parameter.type == AnimatorControllerParameterType.Float)
                {
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        public void EditorConfigure(Transform source, Animator targetAnimator)
        {
            motionSource = source;
            animator = targetAnimator;
            ResolveBindings();
        }
#endif
    }
}
