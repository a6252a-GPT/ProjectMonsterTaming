using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ProjectMT.Contents.FallenCommander
{

    [DisallowMultipleComponent]
    public sealed class FallenCommanderBossAnimationPresenter : MonoBehaviour
    {
        private Animator animator;
        private PlayableGraph playableGraph;
        private AnimationClipPlayable activePlayable;
        private float activeMotionRemaining;
        private double activeMotionEndTime;
        private bool activeMotionClamped;
        private float playbackRemaining;
        private bool stopWhenFinished;
        private AnimationClip sequenceNextMotion;
        private float sequenceNextDuration;
        private float sequenceNextSpeed = 1f;
        private float sequenceNextStart;
        private float sequenceNextEnd = 1f;
        private bool sequenceWaiting;

        public bool IsPlaying => playableGraph.IsValid();
        public string CurrentMotionName { get; private set; } = string.Empty;

        public void Configure(Transform bossRoot)
        {
            Stop();
            if (bossRoot == null)
            {
                animator = null;
                return;
            }

            var animators = bossRoot.GetComponentsInChildren<Animator>(true);
            animator = animators.Length == 0
                ? null
                : animators[0];
        }

        public void Play(
            AnimationClip motion,
            bool stopAfterMotion = false,
            float durationOverride = 0f,
            float playbackSpeed = 1f,
            float normalizedStart = 0f,
            float normalizedEnd = 1f)
        {
            if (animator == null || motion == null)
            {
                return;
            }

            StopGraph();
            PlayGraph(motion, playbackSpeed, false, normalizedStart, normalizedEnd);
            stopWhenFinished = stopAfterMotion;
            playbackRemaining = stopAfterMotion
                ? ResolveDuration(
                    motion,
                    durationOverride,
                    playbackSpeed,
                    normalizedStart,
                    normalizedEnd)
                : 0f;
        }

        // 시전 모션을 한 번만 재생하고 다음 공격 모션 전까지 마지막 자세를 유지한다.
        public void PlayPreCast(
            AnimationClip motion,
            float playbackSpeed = 1f,
            float normalizedStart = 0f,
            float normalizedEnd = 1f)
        {
            if (animator == null || motion == null)
            {
                return;
            }

            StopGraph();
            PlayGraph(motion, playbackSpeed, true, normalizedStart, normalizedEnd);
        }

        public void PlaySequence(
            AnimationClip firstMotion,
            float firstDuration,
            float firstSpeed,
            AnimationClip secondMotion,
            float secondDuration,
            float secondSpeed,
            float firstStart = 0f,
            float firstEnd = 1f,
            float secondStart = 0f,
            float secondEnd = 1f)
        {
            if (animator == null)
            {
                return;
            }

            if (firstMotion == null)
            {
                Play(
                    secondMotion,
                    true,
                    secondDuration,
                    secondSpeed,
                    secondStart,
                    secondEnd);
                return;
            }

            StopGraph();
            PlayGraph(firstMotion, firstSpeed, true, firstStart, firstEnd);
            sequenceNextMotion = secondMotion;
            sequenceNextDuration = secondDuration;
            sequenceNextSpeed = secondSpeed;
            sequenceNextStart = secondStart;
            sequenceNextEnd = secondEnd;
            sequenceWaiting = secondMotion != null;
            stopWhenFinished = secondMotion == null;
            playbackRemaining = ResolveDuration(
                firstMotion,
                firstDuration,
                firstSpeed,
                firstStart,
                firstEnd);
        }

        public void Stop()
        {
            StopGraph();
            animator = null;
        }

        public void StopPlayback()
        {
            StopGraph();
        }

        private void OnDisable()
        {
            StopGraph();
        }

        private void Update()
        {
            ClampMotionAtLastFrame(Time.deltaTime);

            if (!stopWhenFinished || !playableGraph.IsValid())
            {
                if (sequenceWaiting && playableGraph.IsValid())
                {
                    playbackRemaining = Mathf.Max(
                        0f,
                        playbackRemaining - Time.deltaTime);
                    if (playbackRemaining <= 0f)
                    {
                        var nextMotion = sequenceNextMotion;
                        var nextDuration = sequenceNextDuration;
                        var nextSpeed = sequenceNextSpeed;
                        var nextStart = sequenceNextStart;
                        var nextEnd = sequenceNextEnd;
                        sequenceWaiting = false;
                        sequenceNextMotion = null;
                        sequenceNextDuration = 0f;
                        sequenceNextSpeed = 1f;
                        sequenceNextStart = 0f;
                        sequenceNextEnd = 1f;
                        Play(
                            nextMotion,
                            true,
                            nextDuration,
                            nextSpeed,
                            nextStart,
                            nextEnd);
                    }
                }

                return;
            }

            playbackRemaining = Mathf.Max(
                0f,
                playbackRemaining - Time.deltaTime);
            if (playbackRemaining <= 0f)
            {
                StopGraph();
            }
        }

        // 시전 클립의 반복 설정과 관계없이 한 번 재생 후 마지막 자세를 유지한다.
        private void ClampMotionAtLastFrame(float deltaTime)
        {
            if (activeMotionClamped ||
                !playableGraph.IsValid() ||
                !activePlayable.IsValid())
            {
                return;
            }

            activeMotionRemaining = Mathf.Max(
                0f,
                activeMotionRemaining - deltaTime);
            if (activeMotionRemaining > 0f)
            {
                return;
            }

            activePlayable.SetTime(activeMotionEndTime);
            activePlayable.SetSpeed(0d);
            activeMotionClamped = true;
        }

        private void StopGraph()
        {
            if (playableGraph.IsValid())
            {
                playableGraph.Destroy();
            }

            playbackRemaining = 0f;
            activePlayable = default;
            activeMotionRemaining = 0f;
            activeMotionEndTime = 0d;
            activeMotionClamped = false;
            stopWhenFinished = false;
            sequenceNextMotion = null;
            sequenceNextDuration = 0f;
            sequenceNextSpeed = 1f;
            sequenceNextStart = 0f;
            sequenceNextEnd = 1f;
            sequenceWaiting = false;
            CurrentMotionName = string.Empty;
        }

        private void PlayGraph(
            AnimationClip motion,
            float playbackSpeed,
            bool holdLastFrame,
            float normalizedStart,
            float normalizedEnd)
        {
            var playable = AnimationPlayableUtilities.PlayClip(
                animator,
                motion,
                out playableGraph);
            var safePlaybackSpeed = Mathf.Max(0.01f, playbackSpeed);
            var safeStart = Mathf.Clamp(normalizedStart, 0f, 0.999f);
            var safeEnd = Mathf.Clamp(normalizedEnd, safeStart + 0.001f, 1f);
            var startTime = motion.length * safeStart;
            var endTime = motion.length * safeEnd;
            playable.SetTime(startTime);
            playable.SetSpeed(safePlaybackSpeed);
            activePlayable = playable;
            activeMotionRemaining = holdLastFrame
                ? Mathf.Max(0.01f, (endTime - startTime) / safePlaybackSpeed)
                : 0f;
            activeMotionEndTime = holdLastFrame
                ? Math.Max(startTime, endTime - 0.0001d)
                : 0d;
            activeMotionClamped = !holdLastFrame;
            CurrentMotionName = motion.name;
        }

        private static float ResolveDuration(
            AnimationClip motion,
            float durationOverride,
            float playbackSpeed,
            float normalizedStart,
            float normalizedEnd)
        {
            var safeStart = Mathf.Clamp(normalizedStart, 0f, 0.999f);
            var safeEnd = Mathf.Clamp(normalizedEnd, safeStart + 0.001f, 1f);
            return durationOverride > 0f
                ? durationOverride
                : Mathf.Max(
                    0.01f,
                    motion.length * (safeEnd - safeStart) /
                    Mathf.Max(0.01f, playbackSpeed));
        }
    }
}
