using System;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [Serializable]
    public sealed class MonsterMotionSlot // Idle·Move·Death 공통 Clip 설정
    {
        [SerializeField] private AnimationClip clip;
        [SerializeField, Min(0.01f)] private float playbackSpeed = 1f;
        [SerializeField, Min(0f)] private float crossFadeDuration = 0.08f;
        [SerializeField] private bool loop;
        [SerializeField] private MonsterFeedbackCue startFeedback;

        public AnimationClip Clip => clip;
        public float PlaybackSpeed => Mathf.Max(0.01f, playbackSpeed);
        public float CrossFadeDuration => Mathf.Max(0f, crossFadeDuration);
        public bool Loop => loop;
        public MonsterFeedbackCue StartFeedback => startFeedback;

#if UNITY_EDITOR
        public void EditorConfigure(
            AnimationClip animationClip,
            float speed,
            float fadeDuration,
            bool shouldLoop,
            MonsterFeedbackCue feedback = null)
        {
            clip = animationClip;
            playbackSpeed = Mathf.Max(0.01f, speed);
            crossFadeDuration = Mathf.Max(0f, fadeDuration);
            loop = shouldLoop;
            startFeedback = feedback;
        }
#endif
    }

    [Serializable]
    public sealed class MonsterAttackMarker // 제작자가 직접 지정하는 실제 실행 시점
    {
        [SerializeField, Range(0f, 1f)] private float normalizedTime = 0.5f;
        [SerializeField, Min(0f)] private float powerRatio = 1f;
        [SerializeField] private MonsterFeedbackCue feedbackOverride;
        [SerializeField] private string socketOverride;

        public float NormalizedTime => normalizedTime;
        public float PowerRatio => powerRatio;
        public MonsterFeedbackCue FeedbackOverride => feedbackOverride;
        public string SocketOverride => socketOverride ?? string.Empty;

#if UNITY_EDITOR
        public void EditorConfigure(
            float time,
            float ratio,
            MonsterFeedbackCue feedback = null,
            string socketPath = null)
        {
            normalizedTime = time;
            powerRatio = ratio;
            feedbackOverride = feedback;
            socketOverride = socketPath?.Trim();
        }
#endif
    }

    [Serializable]
    public sealed class MonsterAttackMotion // 공격 Clip·무작위 가중치·Marker 묶음
    {
        [SerializeField] private string motionId;
        [SerializeField] private AnimationClip clip;
        [SerializeField, Min(0.01f)] private float playbackSpeed = 1f;
        [SerializeField, Min(0f)] private float crossFadeDuration = 0.06f;
        [SerializeField, Min(0f)] private float weight = 1f;
        [SerializeField] private bool preventImmediateRepeat;
        [SerializeField] private MonsterAttackMarker[] markers = Array.Empty<MonsterAttackMarker>();
        [SerializeField] private MonsterFeedbackCue attackStartOverride;

        public string MotionId => motionId ?? string.Empty;
        public string StateName => "Attack_" + MotionId;
        public AnimationClip Clip => clip;
        public float PlaybackSpeed => Mathf.Max(0.01f, playbackSpeed);
        public float CrossFadeDuration => Mathf.Max(0f, crossFadeDuration);
        public float Weight => Mathf.Max(0f, weight);
        public bool PreventImmediateRepeat => preventImmediateRepeat;
        public MonsterAttackMarker[] Markers => markers ?? Array.Empty<MonsterAttackMarker>();
        public MonsterFeedbackCue AttackStartOverride => attackStartOverride;

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            AnimationClip animationClip,
            float speed,
            float fadeDuration,
            float selectionWeight,
            bool preventRepeat,
            MonsterAttackMarker[] attackMarkers,
            MonsterFeedbackCue startOverride = null)
        {
            motionId = id?.Trim();
            clip = animationClip;
            playbackSpeed = Mathf.Max(0.01f, speed);
            crossFadeDuration = Mathf.Max(0f, fadeDuration);
            weight = Mathf.Max(0f, selectionWeight);
            preventImmediateRepeat = preventRepeat;
            markers = attackMarkers ?? Array.Empty<MonsterAttackMarker>();
            attackStartOverride = startOverride;
        }
#endif
    }

    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Motion Profile", fileName = "MM_Monster")]
    public sealed class MonsterMotionProfile : ScriptableObject // 공통 네 동작과 공격 후보 원본
    {
        public const string IdleStateName = "Idle";
        public const string MoveStateName = "Move";
        public const string DeathStateName = "Death";

        [SerializeField] private MonsterMotionSlot idle = new MonsterMotionSlot();
        [SerializeField] private MonsterMotionSlot move = new MonsterMotionSlot();
        [SerializeField] private MonsterAttackMotion[] attacks = Array.Empty<MonsterAttackMotion>();
        [SerializeField] private MonsterMotionSlot death = new MonsterMotionSlot();

        public MonsterMotionSlot Idle => idle;
        public MonsterMotionSlot Move => move;
        public MonsterAttackMotion[] Attacks => attacks ?? Array.Empty<MonsterAttackMotion>();
        public MonsterMotionSlot Death => death;

        public bool TryValidate(out string error)
        {
            if (idle == null || idle.Clip == null || move == null || move.Clip == null ||
                death == null || death.Clip == null)
            {
                error = $"Monster Motion requires Idle, Move and Death clips. Profile={name}";
                return false;
            }

            if (attacks == null || attacks.Length == 0)
            {
                error = $"Monster Motion requires at least one Attack clip. Profile={name}";
                return false;
            }

            if (!ValidateFeedback(idle.StartFeedback, "Idle", out error) ||
                !ValidateFeedback(move.StartFeedback, "Move", out error) ||
                !ValidateFeedback(death.StartFeedback, "Death", out error))
            {
                return false;
            }

            var motionIds = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var attackIndex = 0; attackIndex < attacks.Length; attackIndex++)
            {
                var attack = attacks[attackIndex];
                if (attack == null || attack.Clip == null || string.IsNullOrWhiteSpace(attack.MotionId))
                {
                    error = $"Monster Attack motion is incomplete. Profile={name}, Index={attackIndex}";
                    return false;
                }

                if (!motionIds.Add(attack.MotionId))
                {
                    error = $"Monster Attack motion ID is duplicated. Profile={name}, Motion={attack.MotionId}";
                    return false;
                }

                if (!ValidateFeedback(attack.AttackStartOverride, $"AttackStart/{attack.MotionId}", out error))
                {
                    return false;
                }

                var markers = attack.Markers;
                if (markers.Length == 0)
                {
                    error = $"Monster Attack motion has no marker. Profile={name}, Motion={attack.MotionId}";
                    return false;
                }

                var ratioSum = 0f;
                var previousTime = -1f;
                for (var markerIndex = 0; markerIndex < markers.Length; markerIndex++)
                {
                    var marker = markers[markerIndex];
                    if (marker == null || marker.NormalizedTime < 0f || marker.NormalizedTime > 1f ||
                        marker.PowerRatio < 0f)
                    {
                        error = $"Monster Attack marker is invalid. Motion={attack.MotionId}, Index={markerIndex}";
                        return false;
                    }

                    if (marker.NormalizedTime < previousTime)
                    {
                        error = $"Monster Attack markers must be sorted. Motion={attack.MotionId}";
                        return false;
                    }

                    previousTime = marker.NormalizedTime;
                    ratioSum += marker.PowerRatio;
                    if (!ValidateFeedback(
                            marker.FeedbackOverride,
                            $"AttackMarker/{attack.MotionId}/{markerIndex}",
                            out error))
                    {
                        return false;
                    }
                }

                if (!Mathf.Approximately(ratioSum, 1f) && Mathf.Abs(ratioSum - 1f) > 0.001f)
                {
                    error = $"Monster Attack marker power ratio must sum to 1. Motion={attack.MotionId}, Sum={ratioSum:0.###}";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool ValidateFeedback(MonsterFeedbackCue cue, string role, out string error)
        {
            if (cue != null && !cue.TryValidate(out var cueError))
            {
                error = $"Monster Motion feedback is invalid. Role={role}, Detail={cueError}";
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            MonsterMotionSlot idleMotion,
            MonsterMotionSlot moveMotion,
            MonsterAttackMotion[] attackMotions,
            MonsterMotionSlot deathMotion)
        {
            idle = idleMotion;
            move = moveMotion;
            attacks = attackMotions ?? Array.Empty<MonsterAttackMotion>();
            death = deathMotion;
        }
#endif
    }
}
