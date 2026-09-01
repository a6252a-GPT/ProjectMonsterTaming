using UnityEditor;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander.Editor
{
    internal static class FallenCommanderPreviewMotionPlayer
    {
        public static void Sample(
            Animator animator,
            AnimationClip motion,
            float time,
            float playbackSpeed,
            float normalizedStart,
            float normalizedEnd)
        {
            if (animator == null || motion == null)
            {
                return;
            }

            var safeStart = Mathf.Clamp(normalizedStart, 0f, 0.999f);
            var safeEnd = Mathf.Clamp(normalizedEnd, safeStart + 0.001f, 1f);
            var startTime = motion.length * safeStart;
            var endTime = motion.length * safeEnd;
            var sampleTime = Mathf.Clamp(
                startTime + time * Mathf.Max(0.01f, playbackSpeed),
                startTime,
                endTime);

            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animator.gameObject, motion, sampleTime);
            AnimationMode.EndSampling();
        }

        public static void FaceTarget(Transform source, Transform target)
        {
            if (source == null || target == null)
            {
                return;
            }

            var direction = target.position - source.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            source.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }
}
