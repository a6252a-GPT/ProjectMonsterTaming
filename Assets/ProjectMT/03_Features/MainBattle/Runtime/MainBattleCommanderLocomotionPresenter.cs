using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class MainBattleCommanderLocomotionPresenter : MonoBehaviour // 군단장 이동 애니
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField, Min(0f)] private float movingSpeedThreshold = 0.05f;
        [SerializeField, Min(0f)] private float speedDampTime = 0.08f;

        private int speedHash;
        private Vector3 previousPosition;
        private bool canDriveSpeed;
        private bool hasPreviousPosition;

        public float CurrentPlanarSpeed { get; private set; }
        public float CurrentSpeedValue { get; private set; }

        private void Awake()
        {
            ResolveAnimator();
        }

        private void OnEnable()
        {
            ResolveAnimator();
            previousPosition = transform.position;
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
            if (!canDriveSpeed || animator == null)
            {
                ResolveAnimator();
                if (!canDriveSpeed)
                {
                    return;
                }
            }

            var currentPosition = transform.position;
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
            CurrentSpeedValue = CalculateSpeedValue(CurrentPlanarSpeed);
            animator.SetFloat(speedHash, CurrentSpeedValue, speedDampTime, deltaTime);
        }

        private float CalculateSpeedValue(float planarSpeed)
        {
            if (planarSpeed <= movingSpeedThreshold)
            {
                return 0f;
            }

            return 1f; // 이동 중 VTP3D Jog 고정
        }

        private void ResolveAnimator()
        {
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
    }
}
