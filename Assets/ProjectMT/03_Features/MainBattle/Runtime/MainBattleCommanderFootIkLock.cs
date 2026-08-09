using UnityEngine;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class MainBattleCommanderFootIkLock : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        private bool targetsCaptured;
        private Vector3 leftFootPosition;
        private Vector3 rightFootPosition;
        private Quaternion leftFootRotation;
        private Quaternion rightFootRotation;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }

        private void OnEnable()
        {
            targetsCaptured = false;
        }

        private void OnDisable()
        {
            targetsCaptured = false;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !animator.isHuman)
            {
                return;
            }

            if (!targetsCaptured)
            {
                leftFootPosition = animator.GetIKPosition(AvatarIKGoal.LeftFoot);
                rightFootPosition = animator.GetIKPosition(AvatarIKGoal.RightFoot);
                leftFootRotation = animator.GetIKRotation(AvatarIKGoal.LeftFoot);
                rightFootRotation = animator.GetIKRotation(AvatarIKGoal.RightFoot);
                targetsCaptured = true;
            }

            ApplyFootLock(AvatarIKGoal.LeftFoot, leftFootPosition, leftFootRotation);
            ApplyFootLock(AvatarIKGoal.RightFoot, rightFootPosition, rightFootRotation);
        }

        private void ApplyFootLock(AvatarIKGoal goal, Vector3 position, Quaternion rotation)
        {
            animator.SetIKPositionWeight(goal, 1f);
            animator.SetIKRotationWeight(goal, 1f);
            animator.SetIKPosition(goal, position);
            animator.SetIKRotation(goal, rotation);
        }
    }
}
