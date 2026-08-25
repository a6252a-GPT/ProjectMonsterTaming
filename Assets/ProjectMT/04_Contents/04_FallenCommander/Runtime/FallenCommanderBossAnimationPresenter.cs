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
        private float sequenceNextSpeed = 1f;
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
            float playbackSpeed = 1f)
        {
            if (animator == null || motion == null)
            {
                return;
            }

            StopGraph();
            PlayGraph(motion, playbackSpeed);
            stopWhenFinished = stopAfterMotion;
            playbackRemaining = stopAfterMotion
                ? ResolveDuration(motion, durationOverride, playbackSpeed)
                : 0f;
        }

        public void PlaySequence(
            AnimationClip firstMotion,
            float firstDuration,
            float firstSpeed,
            AnimationClip secondMotion,
            float secondDuration,
            float secondSpeed)
        {
            if (animator == null)
            {
                return;
            }

            if (firstMotion == null)
            {
                Play(secondMotion, true, secondDuration, secondSpeed);
                return;
            }

            StopGraph();
            PlayGraph(firstMotion, firstSpeed);
            sequenceNextMotion = secondMotion;
            sequenceNextDuration = secondDuration;
            sequenceNextSpeed = secondSpeed;
            sequenceWaiting = secondMotion != null;
            stopWhenFinished = secondMotion == null;
            playbackRemaining = ResolveDuration(firstMotion, firstDuration, firstSpeed);
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
                        var nextSpeed = sequenceNextSpeed;
                        sequenceWaiting = false;
                        sequenceNextMotion = null;
                        sequenceNextDuration = 0f;
                        sequenceNextSpeed = 1f;
                        Play(nextMotion, true, nextDuration, nextSpeed);
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
            sequenceNextSpeed = 1f;
            sequenceWaiting = false;
            CurrentMotionName = string.Empty;
        }

        private void PlayGraph(AnimationClip motion, float playbackSpeed)
        {
            var playable = AnimationPlayableUtilities.PlayClip(
                animator,
                motion,
                out playableGraph);
            playable.SetSpeed(Mathf.Max(0.01f, playbackSpeed));
            CurrentMotionName = motion.name;
        }

        private static float ResolveDuration(
            AnimationClip motion,
            float durationOverride,
            float playbackSpeed)
        {
            return durationOverride > 0f
                ? durationOverride
                : Mathf.Max(0.01f, motion.length / Mathf.Max(0.01f, playbackSpeed));
        }
    }
}
