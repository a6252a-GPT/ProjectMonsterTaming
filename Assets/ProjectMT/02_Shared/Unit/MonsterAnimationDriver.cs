using System;
using ProjectMT.Shared.Combat;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [DisallowMultipleComponent]
    public sealed class MonsterAnimationDriver : MonoBehaviour // 정식 Monster 공통 Mecanim 재생기
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform socketRoot;
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private Transform hitCenter;

        private MonsterRuntimeAssetSet assetSet;
        private MonsterMotionProfile motionProfile;
        private MonsterAttackMotion currentAttack;
        private int currentAttackIndex = -1;
        private int previousAttackIndex = -1;
        private int nextMarkerIndex;
        private int actionSequenceId;
        private float attackElapsed;
        private float motionDuration;
        private float attackDuration;
        private float currentBreathDuration;
        private string lastAttackMotionId;
        private float previousNormalizedTime;
        private string currentStateName;
        private float desiredAnimatorSpeed = 1f;
        private bool locallyPaused;

        public bool IsReady => animator != null && assetSet != null && motionProfile != null;
        public bool IsAttackPlaying => currentAttack != null;
        public int ActionSequenceId => actionSequenceId;
        public string CurrentMotionId => currentAttack?.MotionId ?? lastAttackMotionId ?? string.Empty;
        public float CurrentBreathDuration => currentBreathDuration;
        public MonsterFeedbackCue CurrentAttackStartFeedback => currentAttack?.AttackStartOverride;
        public float CurrentNormalizedTime => currentAttack == null || motionDuration <= 0f
            ? 0f
            : Mathf.Clamp01(attackElapsed / motionDuration);
        public Transform AttackOrigin => attackOrigin != null ? attackOrigin : transform;
        public Transform HitCenter => hitCenter != null ? hitCenter : transform;

        public bool TryGetNextAttackMarkerDelay(out float delay)
        {
            delay = 0f;
            if (currentAttack?.Markers == null ||
                nextMarkerIndex < 0 ||
                nextMarkerIndex >= currentAttack.Markers.Length)
            {
                return false;
            }

            var marker = currentAttack.Markers[nextMarkerIndex];
            delay = Mathf.Max(0f, motionDuration * marker.NormalizedTime - attackElapsed);
            return true;
        }

        public void SetLocallyPaused(bool paused)
        {
            locallyPaused = paused;
            if (animator != null)
            {
                animator.speed = paused ? 0f : Mathf.Max(0.01f, desiredAnimatorSpeed);
            }
        }

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>(); // Adapter Root에 명시된 Animator만 허용
            }

            if (socketRoot == null)
            {
                socketRoot = transform;
            }
        }

        public bool Initialize(MonsterRuntimeAssetSet runtimeAssetSet)
        {
            Shutdown();
            assetSet = runtimeAssetSet;
            motionProfile = runtimeAssetSet?.MotionProfile;
            if (animator == null || runtimeAssetSet == null || runtimeAssetSet.AnimatorController == null ||
                motionProfile == null)
            {
                return false;
            }

            animator.runtimeAnimatorController = runtimeAssetSet.AnimatorController;
            animator.applyRootMotion = false;
            PlayIdle(true);
            return true;
        }

        public void PlayIdle(bool restart = false)
        {
            PlayMotion(MonsterMotionProfile.IdleStateName, motionProfile?.Idle, restart);
        }

        public void PlayMove()
        {
            PlayMotion(MonsterMotionProfile.MoveStateName, motionProfile?.Move, false);
        }

        public bool TryBeginAttack(
            float attackInterval,
            int sequenceId,
            Action<int, MonsterAttackMarker> onMarker,
            float profileBreathDuration = 0f)
        {
            if (!IsReady || currentAttack != null)
            {
                return false;
            }

            currentAttackIndex = SelectAttackIndex(motionProfile.Attacks, previousAttackIndex);
            if (currentAttackIndex < 0)
            {
                return false;
            }

            currentAttack = motionProfile.Attacks[currentAttackIndex];
            lastAttackMotionId = currentAttack.MotionId;
            previousAttackIndex = currentAttackIndex;
            actionSequenceId = sequenceId;
            attackElapsed = 0f;
            previousNormalizedTime = -0.0001f;
            nextMarkerIndex = 0;

            var clipLength = Mathf.Max(0.01f, currentAttack.Clip.length);
            var resolvedSpeed = ResolveAttackPlaybackSpeed(
                currentAttack.Clip,
                currentAttack.PlaybackSpeed,
                attackInterval);
            motionDuration = clipLength / resolvedSpeed;
            currentBreathDuration = profileBreathDuration > 0f
                ? currentAttack.ResolveBreathDuration(profileBreathDuration)
                : 0f;
            attackDuration = motionDuration;
            if (currentBreathDuration > 0f && currentAttack.Markers.Length > 0)
            {
                var recipeStart = motionDuration * currentAttack.Markers[0].NormalizedTime;
                attackDuration = Mathf.Max(attackDuration, recipeStart + currentBreathDuration);
            }
            PlayAnimatorState(currentAttack.StateName, resolvedSpeed, currentAttack.CrossFadeDuration);
            currentStateName = currentAttack.StateName;

            MonsterAttackMarkerEvaluator.EvaluatePassed(
                currentAttack.Markers,
                previousNormalizedTime,
                0f,
                ref nextMarkerIndex,
                onMarker);
            previousNormalizedTime = 0f;
            return true;
        }

        public bool TickAttack(float deltaTime, Action<int, MonsterAttackMarker> onMarker)
        {
            if (currentAttack == null)
            {
                return true;
            }

            attackElapsed = Mathf.Min(attackDuration, attackElapsed + Mathf.Max(0f, deltaTime));
            var normalizedTime = CurrentNormalizedTime;
            MonsterAttackMarkerEvaluator.EvaluatePassed(
                currentAttack.Markers,
                previousNormalizedTime,
                normalizedTime,
                ref nextMarkerIndex,
                onMarker);
            previousNormalizedTime = normalizedTime;
            if (attackElapsed + 0.0001f < attackDuration)
            {
                return false;
            }

            currentAttack = null;
            currentAttackIndex = -1;
            nextMarkerIndex = 0;
            currentBreathDuration = 0f;
            return true;
        }

        public float PlayDeath()
        {
            currentAttack = null;
            var death = motionProfile?.Death;
            if (death == null || death.Clip == null)
            {
                return 0.38f;
            }

            PlayAnimatorState(MonsterMotionProfile.DeathStateName, death.PlaybackSpeed, death.CrossFadeDuration);
            currentStateName = MonsterMotionProfile.DeathStateName;
            return Mathf.Max(0.05f, death.Clip.length / death.PlaybackSpeed);
        }

        public Transform ResolveSocket(string pathOverride)
        {
            if (string.IsNullOrWhiteSpace(pathOverride))
            {
                return AttackOrigin;
            }

            var root = socketRoot != null ? socketRoot : transform;
            return root.Find(pathOverride) ?? AttackOrigin;
        }

        public void Shutdown()
        {
            locallyPaused = false;
            desiredAnimatorSpeed = 1f;
            if (animator != null)
            {
                animator.speed = 1f;
            }

            assetSet = null;
            motionProfile = null;
            currentAttack = null;
            currentAttackIndex = -1;
            previousAttackIndex = -1;
            nextMarkerIndex = 0;
            actionSequenceId = 0;
            attackElapsed = 0f;
            motionDuration = 0f;
            attackDuration = 0f;
            currentBreathDuration = 0f;
            lastAttackMotionId = string.Empty;
            previousNormalizedTime = 0f;
            currentStateName = string.Empty;
        }

        private void PlayMotion(string stateName, MonsterMotionSlot motion, bool restart)
        {
            if (!IsReady || motion == null || motion.Clip == null ||
                !restart && string.Equals(currentStateName, stateName, StringComparison.Ordinal))
            {
                return;
            }

            currentAttack = null;
            PlayAnimatorState(stateName, motion.PlaybackSpeed, motion.CrossFadeDuration);
            currentStateName = stateName;
        }

        private void PlayAnimatorState(string stateName, float speed, float crossFadeDuration)
        {
            if (animator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            desiredAnimatorSpeed = Mathf.Max(0.01f, speed);
            animator.speed = locallyPaused ? 0f : desiredAnimatorSpeed;
            if (crossFadeDuration <= 0f)
            {
                animator.Play(stateName, 0, 0f);
            }
            else
            {
                animator.CrossFadeInFixedTime(stateName, crossFadeDuration, 0, 0f);
            }
        }

        public static float ResolveAttackPlaybackSpeed(
            AnimationClip clip,
            float authoredSpeed,
            float attackInterval)
        {
            var safeAuthoredSpeed = Mathf.Max(0.01f, authoredSpeed);
            if (clip == null)
            {
                return safeAuthoredSpeed;
            }

            var fitIntervalSpeed = Mathf.Max(0.01f, clip.length) / Mathf.Max(0.05f, attackInterval);
            return Mathf.Max(safeAuthoredSpeed, fitIntervalSpeed);
        }

        private static int SelectAttackIndex(MonsterAttackMotion[] attacks, int previousIndex)
        {
            if (attacks == null || attacks.Length == 0)
            {
                return -1;
            }

            var eligibleCount = 0;
            var totalWeight = 0f;
            var lastEligibleIndex = -1;
            for (var index = 0; index < attacks.Length; index++)
            {
                var attack = attacks[index];
                if (attack == null || attack.Clip == null ||
                    index == previousIndex && attack.PreventImmediateRepeat && attacks.Length > 1)
                {
                    continue;
                }

                eligibleCount++;
                totalWeight += attack.Weight;
                lastEligibleIndex = index;
            }

            if (eligibleCount == 0)
            {
                if (previousIndex >= 0 && previousIndex < attacks.Length &&
                    attacks[previousIndex]?.Clip != null)
                {
                    return previousIndex;
                }

                for (var index = 0; index < attacks.Length; index++)
                {
                    if (attacks[index]?.Clip != null)
                    {
                        return index;
                    }
                }

                return -1;
            }

            if (totalWeight <= 0f)
            {
                var selection = UnityEngine.Random.Range(0, eligibleCount);
                for (var index = 0; index < attacks.Length; index++)
                {
                    var attack = attacks[index];
                    if (attack == null || attack.Clip == null ||
                        index == previousIndex && attack.PreventImmediateRepeat && attacks.Length > 1)
                    {
                        continue;
                    }

                    if (selection-- == 0)
                    {
                        return index;
                    }
                }
            }

            var roll = UnityEngine.Random.value * totalWeight;
            for (var index = 0; index < attacks.Length; index++)
            {
                var attack = attacks[index];
                if (attack == null || attack.Clip == null ||
                    index == previousIndex && attack.PreventImmediateRepeat && attacks.Length > 1)
                {
                    continue;
                }

                roll -= attack.Weight;
                if (roll <= 0f)
                {
                    return index;
                }
            }

            return lastEligibleIndex;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Animator targetAnimator,
            Transform root,
            Transform origin,
            Transform center)
        {
            animator = targetAnimator;
            socketRoot = root;
            attackOrigin = origin;
            hitCenter = center;
        }
#endif
    }
}
