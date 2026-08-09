using UnityEngine;

namespace ProjectMT.Features.Formation
{
    [DisallowMultipleComponent]
    public sealed class FormationCommanderIdlePresenter : MonoBehaviour
    {
        private const string BaseLayerName = "Base Layer";

        [SerializeField] private Animator animator;
        [SerializeField] private string[] idleStateNames;

        private int lastStateIndex = -1;
        private string currentStateName = string.Empty;
        private bool originalApplyRootMotion;
        private bool rootMotionCaptured;
        private bool footTargetsCaptured;
        private Vector3 leftFootTargetPosition;
        private Vector3 rightFootTargetPosition;
        private Quaternion leftFootTargetRotation;
        private Quaternion rightFootTargetRotation;

        public string CurrentStateName => currentStateName;

        private void OnEnable()
        {
            PlayRandomIdleState();
        }

        private void OnDisable()
        {
            footTargetsCaptured = false;
            currentStateName = string.Empty;
            RestoreRootMotion();
        }

        private void OnDestroy()
        {
            RestoreRootMotion();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !animator.isHuman || string.IsNullOrEmpty(currentStateName))
            {
                return;
            }

            if (!footTargetsCaptured)
            {
                leftFootTargetPosition = animator.GetIKPosition(AvatarIKGoal.LeftFoot);
                rightFootTargetPosition = animator.GetIKPosition(AvatarIKGoal.RightFoot);
                leftFootTargetRotation = animator.GetIKRotation(AvatarIKGoal.LeftFoot);
                rightFootTargetRotation = animator.GetIKRotation(AvatarIKGoal.RightFoot);
                footTargetsCaptured = true;
            }

            ApplyFootLock(AvatarIKGoal.LeftFoot, leftFootTargetPosition, leftFootTargetRotation);
            ApplyFootLock(AvatarIKGoal.RightFoot, rightFootTargetPosition, rightFootTargetRotation);
        }

        private void PlayRandomIdleState()
        {
            var stateIndex = SelectStateIndex();
            if (animator == null || stateIndex < 0)
            {
                return;
            }

            var stateName = idleStateNames[stateIndex];
            var stateHash = Animator.StringToHash($"{BaseLayerName}.{stateName}");
            if (!animator.HasState(0, stateHash))
            {
                return;
            }

            lastStateIndex = stateIndex;
            currentStateName = stateName;
            footTargetsCaptured = false;
            originalApplyRootMotion = animator.applyRootMotion;
            rootMotionCaptured = true;
            animator.applyRootMotion = false;
            animator.Play(stateHash, 0, 0f);
        }

        private int SelectStateIndex()
        {
            if (idleStateNames == null || idleStateNames.Length == 0)
            {
                return -1;
            }

            var validCount = 0;
            for (var i = 0; i < idleStateNames.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(idleStateNames[i]))
                {
                    validCount++;
                }
            }

            if (validCount == 0)
            {
                return -1;
            }

            var startIndex = Random.Range(0, idleStateNames.Length);
            for (var offset = 0; offset < idleStateNames.Length; offset++)
            {
                var index = (startIndex + offset) % idleStateNames.Length;
                if (!string.IsNullOrWhiteSpace(idleStateNames[index]) &&
                    (validCount == 1 || index != lastStateIndex))
                {
                    return index;
                }
            }

            return lastStateIndex;
        }

        private void ApplyFootLock(AvatarIKGoal goal, Vector3 position, Quaternion rotation)
        {
            animator.SetIKPositionWeight(goal, 1f);
            animator.SetIKRotationWeight(goal, 1f);
            animator.SetIKPosition(goal, position);
            animator.SetIKRotation(goal, rotation);
        }

        private void RestoreRootMotion()
        {
            if (!rootMotionCaptured || animator == null)
            {
                return;
            }

            animator.applyRootMotion = originalApplyRootMotion;
            rootMotionCaptured = false;
        }
    }
}
