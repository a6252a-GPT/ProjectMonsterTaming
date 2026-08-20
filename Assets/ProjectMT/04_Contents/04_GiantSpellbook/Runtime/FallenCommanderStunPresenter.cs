using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ProjectMT.Contents.FallenCommander
{
    [DisallowMultipleComponent]
    public sealed class FallenCommanderStunPresenter : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField, Min(0f)] private float tiltAngle = 9f;
        [SerializeField, Min(0.1f)] private float swaySpeed = 8f;

        private Animator animator;
        private AnimationClip stunMotion;
        private PlayableGraph playableGraph;
        private Quaternion originalLocalRotation;
        private float originalAnimatorSpeed = 1f;
        private bool isStunned;

        public void Configure(
            Transform commanderRoot,
            AnimationClip motion)
        {
            Release();

            stunMotion = motion;
            animator = commanderRoot == null
                ? null
                : commanderRoot.GetComponentInChildren<Animator>(true);
            visualRoot = animator == null ? commanderRoot : animator.transform;

            if (visualRoot != null)
            {
                originalLocalRotation = visualRoot.localRotation;
            }
        }

        public void SetStunned(bool stunned)
        {
            if (isStunned == stunned)
            {
                return;
            }

            isStunned = stunned;
            if (isStunned)
            {
                if (animator != null)
                {
                    originalAnimatorSpeed = animator.speed;
                    if (stunMotion == null)
                    {
                        animator.speed = 0f;
                    }
                    else
                    {
                        AnimationPlayableUtilities.PlayClip(
                            animator,
                            stunMotion,
                            out playableGraph);
                    }
                }

                return;
            }

            RestoreVisual();
        }

        public void PlayDeath(AnimationClip motion)
        {
            isStunned = false;
            RestoreVisual();

            if (animator == null || motion == null)
            {
                return;
            }

            AnimationPlayableUtilities.PlayClip(
                animator,
                motion,
                out playableGraph);
        }

        public void Release()
        {
            isStunned = false;
            RestoreVisual();
            animator = null;
            visualRoot = null;
            stunMotion = null;
        }

        private void LateUpdate()
        {
            if (!isStunned || visualRoot == null || stunMotion != null)
            {
                return;
            }

            var sway = Mathf.Sin(Time.time * swaySpeed) * tiltAngle;
            visualRoot.localRotation = originalLocalRotation * Quaternion.Euler(0f, 0f, sway);
        }

        private void OnDisable()
        {
            Release();
        }

        private void RestoreVisual()
        {
            if (playableGraph.IsValid())
            {
                playableGraph.Destroy();
            }

            if (visualRoot != null)
            {
                visualRoot.localRotation = originalLocalRotation;
            }

            if (animator != null)
            {
                animator.speed = originalAnimatorSpeed;
            }
        }
    }
}
