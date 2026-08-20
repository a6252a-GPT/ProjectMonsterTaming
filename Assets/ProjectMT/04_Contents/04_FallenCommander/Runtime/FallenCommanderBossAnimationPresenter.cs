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
        private float playbackRemaining;
        private bool stopWhenFinished;
        private AnimationClip sequenceNextMotion;
        private float sequenceNextDuration;
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
            float durationOverride = 0f)
        {
            if (animator == null || motion == null)
            {
                return;
            }

            StopGraph();
            PlayGraph(motion);
            stopWhenFinished = stopAfterMotion;
            playbackRemaining = stopAfterMotion
                ? ResolveDuration(motion, durationOverride)
                : 0f;
        }

        public void PlaySequence(
            AnimationClip firstMotion,
            float firstDuration,
            AnimationClip secondMotion,
            float secondDuration)
        {
            if (animator == null)
            {
                return;
            }

            if (firstMotion == null)
            {
                Play(secondMotion, true, secondDuration);
                return;
            }

            StopGraph();
            PlayGraph(firstMotion);
            sequenceNextMotion = secondMotion;
            sequenceNextDuration = secondDuration;
            sequenceWaiting = secondMotion != null;
            stopWhenFinished = secondMotion == null;
            playbackRemaining = ResolveDuration(firstMotion, firstDuration);
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
                        sequenceWaiting = false;
                        sequenceNextMotion = null;
                        sequenceNextDuration = 0f;
                        Play(nextMotion, true, nextDuration);
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

        private void StopGraph()
        {
            if (playableGraph.IsValid())
            {
                playableGraph.Destroy();
            }

            playbackRemaining = 0f;
            stopWhenFinished = false;
            sequenceNextMotion = null;
            sequenceNextDuration = 0f;
            sequenceWaiting = false;
            CurrentMotionName = string.Empty;
        }

        private void PlayGraph(AnimationClip motion)
        {
            AnimationPlayableUtilities.PlayClip(
                animator,
                motion,
                out playableGraph);
            CurrentMotionName = motion.name;
        }

        private static float ResolveDuration(
            AnimationClip motion,
            float durationOverride)
        {
            return durationOverride > 0f
                ? durationOverride
                : Mathf.Max(0.01f, motion.length);
        }
    }
}
