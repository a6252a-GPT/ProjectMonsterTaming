using ProjectMT.Shared.Animation;
using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    [DefaultExecutionOrder(640)]
    [DisallowMultipleComponent]
    public sealed class MainBattleCommanderFootIkLock : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private HumanoidFootContactRig footContactRig;

        [Header("Animator Settling")]
        [SerializeField] private int baseLayerIndex;
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField, Min(0f)] private float animatorIdleSpeedThreshold = 0.03f;

        [Header("Two Point Ground Probe")]
        [SerializeField] private LayerMask groundLayer = ~0;
        [SerializeField, Min(0f)] private float probeUpDistance = 0.12f;
        [SerializeField, Min(0.01f)] private float probeDownDistance = 0.30f;
        [SerializeField, Min(0.001f)] private float probeRadius = 0.015f;
        [SerializeField, Min(0f)] private float flatContactHeightTolerance = 0.005f;
        [SerializeField, Min(0f)] private float flatParallelAngleTolerance = 0.5f;
        [SerializeField, Range(0f, 89f)] private float maxGroundAngle = 50f;
        [SerializeField, Min(0.01f)] private float maxHorizontalCorrection = 0.20f;

        [Header("Rotation Release")]
        [SerializeField, Min(0f)] private float rotationUnlockDegreesPerSecond = 8f;
        [SerializeField, Min(0f)] private float rotationUnlockMinimumDelta = 0.05f;

        [Header("Blend")]
        [SerializeField, Min(0f)] private float lockBlendInDuration = 0.18f;
        [SerializeField, Min(0f)] private float lockBlendOutDuration = 0.08f;
        [SerializeField, Range(0f, 1f)] private float positionWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float rotationWeight = 1f;

        private readonly FootState leftFoot = new FootState(AvatarIKGoal.LeftFoot);
        private readonly FootState rightFoot = new FootState(AvatarIKGoal.RightFoot);
        private int speedParameterHash;
        private bool hasSpeedParameter;
        private float previousRotationYaw;
        private bool hasRotationSample;
        private bool isRotationActive;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ResetRuntimeState();
        }

        private void OnDisable()
        {
            ResetRuntimeState();
        }

        private void Update()
        {
            UpdateRotationActivity();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!CanUseAnimatorIk())
            {
                return;
            }

            var releaseForRotation = isRotationActive;
            var shouldLock = !releaseForRotation && IsAnimatorIdleBlendSettled();
            UpdateFoot(leftFoot, shouldLock, releaseForRotation);
            UpdateFoot(rightFoot, shouldLock, releaseForRotation);
        }

        private void ResolveReferences()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (footContactRig == null)
            {
                footContactRig = GetComponent<HumanoidFootContactRig>();
            }

            if (footContactRig == null)
            {
                footContactRig = GetComponentInParent<HumanoidFootContactRig>();
            }

            CacheFootData();
            RefreshAnimatorParameterCache();
        }

        private void CacheFootData()
        {
            if (animator == null || !animator.isHuman)
            {
                return;
            }

            leftFoot.Bone = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot.Bone = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (footContactRig == null || !footContactRig.IsConfigured)
            {
                return;
            }

            leftFoot.Heel = footContactRig.LeftHeel;
            leftFoot.Toe = footContactRig.LeftToe;
            rightFoot.Heel = footContactRig.RightHeel;
            rightFoot.Toe = footContactRig.RightToe;
        }

        private bool CanUseAnimatorIk()
        {
            if (animator == null || footContactRig == null)
            {
                ResolveReferences();
            }

            return animator != null
                && animator.enabled
                && animator.isActiveAndEnabled
                && animator.isHuman
                && footContactRig != null
                && footContactRig.IsConfigured
                && leftFoot.IsConfigured
                && rightFoot.IsConfigured;
        }

        private bool IsAnimatorIdleBlendSettled()
        {
            if (animator == null)
            {
                return false;
            }

            var layerIndex = Mathf.Clamp(baseLayerIndex, 0, animator.layerCount - 1);
            if (animator.IsInTransition(layerIndex))
            {
                return false;
            }

            return !hasSpeedParameter
                || Mathf.Abs(animator.GetFloat(speedParameterHash)) <= animatorIdleSpeedThreshold;
        }

        private void RefreshAnimatorParameterCache()
        {
            speedParameterHash = Animator.StringToHash(speedParameter);
            hasSpeedParameter = false;
            if (animator == null)
            {
                return;
            }

            var parameters = animator.parameters;
            for (var index = 0; index < parameters.Length; index++)
            {
                var parameter = parameters[index];
                if (parameter.type == AnimatorControllerParameterType.Float &&
                    parameter.nameHash == speedParameterHash)
                {
                    hasSpeedParameter = true;
                    return;
                }
            }
        }

        private void UpdateFoot(FootState foot, bool shouldLock, bool releaseImmediately)
        {
            if (!foot.IsConfigured)
            {
                return;
            }

            if (releaseImmediately)
            {
                ReleaseFootImmediately(foot);
                return;
            }

            if (shouldLock && (!foot.HasTarget || IsFootTooFarFromLock(foot)))
            {
                CaptureFootTarget(foot);
            }

            var targetWeight = shouldLock && foot.HasTarget ? 1f : 0f;
            var blendDuration = targetWeight > foot.Weight
                ? lockBlendInDuration
                : lockBlendOutDuration;
            SmoothFootWeight(foot, targetWeight, blendDuration);

            if (foot.Weight <= 0.001f)
            {
                foot.Weight = 0f;
                if (!shouldLock)
                {
                    foot.HasTarget = false;
                }
            }

            var appliedWeight = Mathf.SmoothStep(0f, 1f, foot.Weight);
            animator.SetIKPositionWeight(foot.Goal, appliedWeight * positionWeight);
            animator.SetIKRotationWeight(foot.Goal, appliedWeight * rotationWeight);
            if (foot.Weight <= 0f || !foot.HasTarget)
            {
                return;
            }

            var currentAnimatedPosition = animator.GetIKPosition(foot.Goal);
            var targetPosition = foot.TargetPosition;
            targetPosition.y = currentAnimatedPosition.y;
            animator.SetIKPosition(foot.Goal, targetPosition);
            animator.SetIKRotation(foot.Goal, foot.TargetRotation);
        }

        private void ReleaseFootImmediately(FootState foot)
        {
            foot.Weight = 0f;
            foot.WeightVelocity = 0f;
            foot.HasTarget = false;
            animator.SetIKPositionWeight(foot.Goal, 0f);
            animator.SetIKRotationWeight(foot.Goal, 0f);
        }

        private static void SmoothFootWeight(FootState foot, float targetWeight, float blendDuration)
        {
            if (blendDuration <= 0f)
            {
                foot.Weight = targetWeight;
                foot.WeightVelocity = 0f;
                return;
            }

            foot.Weight = Mathf.SmoothDamp(
                foot.Weight,
                targetWeight,
                ref foot.WeightVelocity,
                blendDuration,
                Mathf.Infinity,
                Time.deltaTime);

            if (Mathf.Abs(foot.Weight - targetWeight) <= 0.001f)
            {
                foot.Weight = targetWeight;
                foot.WeightVelocity = 0f;
            }
        }

        private bool IsFootTooFarFromLock(FootState foot)
        {
            var maxDistance = Mathf.Max(0.01f, maxHorizontalCorrection);
            var currentAnimatedPosition = animator.GetIKPosition(foot.Goal);
            var offset = currentAnimatedPosition - foot.TargetPosition;
            offset.y = 0f;
            return offset.sqrMagnitude > maxDistance * maxDistance;
        }

        private void CaptureFootTarget(FootState foot)
        {
            var animatedPosition = animator.GetIKPosition(foot.Goal);
            var animatedRotation = animator.GetIKRotation(foot.Goal);
            var settings = new TwoPointFootGroundSolver.Settings(
                groundLayer,
                transform.up,
                probeUpDistance,
                probeDownDistance,
                probeRadius,
                flatContactHeightTolerance,
                maxGroundAngle);

            if (!TwoPointFootGroundSolver.TrySolve(
                    animatedPosition,
                    animatedRotation,
                    foot.Heel,
                    foot.Toe,
                    settings,
                    out var solution))
            {
                foot.HasTarget = false;
                return;
            }

            if (solution.IsFlatSurface && solution.ParallelAngleError > flatParallelAngleTolerance)
            {
                foot.HasTarget = false;
                return;
            }

            foot.TargetPosition = animatedPosition;
            foot.TargetRotation = solution.TargetRotation;
            foot.HasTarget = true;
        }

        private void UpdateRotationActivity()
        {
            var currentYaw = transform.eulerAngles.y;
            if (!hasRotationSample)
            {
                previousRotationYaw = currentYaw;
                hasRotationSample = true;
                isRotationActive = false;
                return;
            }

            var yawDelta = Mathf.Abs(Mathf.DeltaAngle(previousRotationYaw, currentYaw));
            var safeDeltaTime = Mathf.Max(0.0001f, Time.deltaTime);
            var yawSpeed = yawDelta / safeDeltaTime;
            isRotationActive = yawDelta >= rotationUnlockMinimumDelta
                && yawSpeed >= rotationUnlockDegreesPerSecond;
            previousRotationYaw = currentYaw;
        }

        private void ResetRuntimeState()
        {
            ResetFoot(leftFoot);
            ResetFoot(rightFoot);
            hasRotationSample = false;
            isRotationActive = false;
        }

        private static void ResetFoot(FootState foot)
        {
            foot.Weight = 0f;
            foot.WeightVelocity = 0f;
            foot.HasTarget = false;
        }

        private sealed class FootState
        {
            public readonly AvatarIKGoal Goal;
            public Transform Bone;
            public Transform Heel;
            public Transform Toe;
            public Vector3 TargetPosition;
            public Quaternion TargetRotation;
            public float Weight;
            public float WeightVelocity;
            public bool HasTarget;

            public bool IsConfigured => Bone != null && Heel != null && Toe != null;

            public FootState(AvatarIKGoal goal)
            {
                Goal = goal;
                TargetRotation = Quaternion.identity;
            }
        }
    }
}
