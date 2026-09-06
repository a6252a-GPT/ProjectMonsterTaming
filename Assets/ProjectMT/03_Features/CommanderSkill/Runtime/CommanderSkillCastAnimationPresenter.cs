using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    [DisallowMultipleComponent]
    public sealed class CommanderSkillCastAnimationPresenter : MonoBehaviour
    {
        private const string IdleState = "Base Layer.WorldIdle";
        private const string MoveState = "Base Layer.ExploreMove";
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        [SerializeField] private Animator animator;
        [SerializeField, Min(0f)] private float crossFadeDuration = 0.08f;

        private float remaining;
        private bool hasSpeedParameter;

        public int LastPlayedAttackNumber { get; private set; }
        public bool IsPlaying => remaining > 0f;

        public void Configure(Animator targetAnimator)
        {
            animator = targetAnimator != null ? targetAnimator : GetComponentInChildren<Animator>(true);
            hasSpeedParameter = HasFloatParameter(animator, SpeedHash);
            remaining = 0f;
            LastPlayedAttackNumber = 0;
            if (animator != null) animator.applyRootMotion = false;
        }

        public bool Play(string skillId)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                Configure(null);
            if (animator == null || animator.runtimeAnimatorController == null) return false;

            var attackNumber = CommanderSkillCastAnimationRules.ResolveAttackNumber(skillId);
            var stateHash = Animator.StringToHash(CommanderSkillCastAnimationRules.StateName(attackNumber));
            if (!animator.HasState(0, stateHash)) return false;

            LastPlayedAttackNumber = attackNumber;
            remaining = ResolveDuration(attackNumber);
            animator.CrossFadeInFixedTime(stateHash, crossFadeDuration, 0, 0f);
            return true;
        }

        public void Stop()
        {
            if (animator != null && remaining > 0f) ReturnToLocomotion();
            remaining = 0f;
        }

        private void Update()
        {
            if (remaining <= 0f || animator == null) return;
            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            if (remaining <= 0f) ReturnToLocomotion();
        }

        private void OnDisable()
        {
            remaining = 0f;
        }

        private void ReturnToLocomotion()
        {
            var moving = hasSpeedParameter && animator.GetFloat(SpeedHash) > 0.05f;
            animator.CrossFadeInFixedTime(
                Animator.StringToHash(moving ? MoveState : IdleState),
                crossFadeDuration,
                0,
                0f);
        }

        private float ResolveDuration(int attackNumber)
        {
            var expectedName = CommanderSkillCastAnimationRules.ClipName(attackNumber);
            var clips = animator.runtimeAnimatorController.animationClips;
            for (var index = 0; index < clips.Length; index++)
            {
                var clip = clips[index];
                if (clip != null && (clip.name == expectedName || clip.name == expectedName + "_inplace"))
                    return Mathf.Max(0.1f, clip.length / CommanderSkillCastAnimationRules.StatePlaybackSpeed);
            }
            return 1f;
        }

        private static bool HasFloatParameter(Animator target, int hash)
        {
            if (target == null || target.runtimeAnimatorController == null) return false;
            var parameters = target.parameters;
            for (var index = 0; index < parameters.Length; index++)
                if (parameters[index].nameHash == hash && parameters[index].type == AnimatorControllerParameterType.Float)
                    return true;
            return false;
        }
    }
}
