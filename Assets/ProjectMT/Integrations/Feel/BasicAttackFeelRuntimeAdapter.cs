using System;
using System.Collections.Generic;
using System.Linq;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Integrations.Feel
{
    [DisallowMultipleComponent]
    public sealed class BasicAttackFeelRuntimeAdapter : MonoBehaviour, IBasicAttackFeelRuntime
    {
        private const string GlobalFeedbackPrefix = "[Global]";
        private const string PrefabTargetToken = "[PrefabTarget]";
        private const float GlobalFeedbackInterval = 0.09f;

        [SerializeField] private MMF_Player player;
        [NonSerialized] private MMF_ReferenceHolder targetReference;
        private TrailRenderer[] trails = Array.Empty<TrailRenderer>();
        private ParticleSystem[] particles = Array.Empty<ParticleSystem>();
        private readonly List<MMF_Feedback> globalFeedbacks = new List<MMF_Feedback>();
        private readonly List<MMF_Feedback> targetMotionFeedbacks = new List<MMF_Feedback>();
        private readonly Dictionary<MMF_Feedback, bool> targetMotionDefaultActive =
            new Dictionary<MMF_Feedback, bool>();
        private bool initialized;
        private bool safeSpringFeedbacksPrepared;
        private Transform ownedMotionTarget;
        private int ownedMotionTargetId;
        private Vector3 ownedMotionTargetLocalPosition;
        private Quaternion ownedMotionTargetLocalRotation;
        private Vector3 ownedMotionTargetScale;

        private static float nextGlobalFeedbackTime;
        private static float strongestRecentGlobalIntensity;
        private static readonly Dictionary<int, BasicAttackFeelRuntimeAdapter> TargetMotionOwners =
            new Dictionary<int, BasicAttackFeelRuntimeAdapter>();

        public MMF_Player Player => player;
        public bool IsBasicAttackFeelConfigured => player != null;

        public bool HasBasicAttackTargetMotion(float intensity = 1f)
        {
            if (player?.FeedbacksList == null)
            {
                return false;
            }
            intensity = Mathf.Max(0f, intensity);
            return player.FeedbacksList.Any(feedback =>
                feedback != null && feedback.Active &&
                ResolveTargetMotionChannel(feedback) != TargetMotionChannel.None &&
                AcceptsIntensity(feedback, intensity));
        }

        private void Awake()
        {
            CacheTransientVisuals();
        }

        public void PlayBasicAttackFeel(
            Vector3 position,
            GameObject target = null,
            float intensity = 1f,
            BasicAttackFeelPlaybackOptions options = BasicAttackFeelPlaybackOptions.IncludeGlobalFeedback)
        {
            if (player == null)
            {
                return;
            }

            if (targetReference == null)
            {
                CacheTransientVisuals(); // Awake가 호출되지 않는 Editor Preview 인스턴스도 준비한다.
            }

            ResetBasicAttackFeel();
            EnsureRuntimeSafeSpringFeedbacks();
            BindTarget(target);
            if (targetReference != null && targetReference.GameObjectReference == null)
            {
                return; // 대상 없는 Editor 시간축 검증에서는 자동 Target Acquisition을 건너뛴다.
            }
            intensity = Mathf.Max(0f, intensity);
            var includeGlobal = (options & BasicAttackFeelPlaybackOptions.IncludeGlobalFeedback) != 0;
            var ownsTargetMotion = includeGlobal
                ? HasTargetMotionFeedbackForIntensity(intensity)
                : SelectTargetMotionFeedbacks(intensity);
            if (ownsTargetMotion)
            {
                AcquireTargetMotion(target);
            }
            SetGlobalFeedbacksActive(TryClaimGlobalFeedback(intensity), includeGlobal);
            player.Initialization(true);
            initialized = true;
            ClearTransientVisuals();
            player.PlayFeedbacks(position, intensity);
        }

        public void ResetBasicAttackFeel()
        {
            if (player != null && initialized)
            {
                player.StopFeedbacks();
                player.RestoreInitialValues();
                player.ResetFeedbacks();
            }
            initialized = false;
            ReleaseTargetMotion();
            RestoreTargetMotionFeedbackStates();
            ClearTransientVisuals();
        }

        private void OnDisable()
        {
            ResetBasicAttackFeel();
        }

        private static bool TryClaimGlobalFeedback(float intensity)
        {
            var now = Time.unscaledTime;
            if (now < nextGlobalFeedbackTime && intensity <= strongestRecentGlobalIntensity)
            {
                return false;
            }

            strongestRecentGlobalIntensity = now >= nextGlobalFeedbackTime
                ? intensity
                : Mathf.Max(strongestRecentGlobalIntensity, intensity);
            nextGlobalFeedbackTime = now + GlobalFeedbackInterval;
            return true;
        }

        private void SetGlobalFeedbacksActive(bool budgetGranted, bool includeSharedCombatFeedback)
        {
            foreach (var feedback in globalFeedbacks)
            {
                if (feedback != null)
                {
                    feedback.Active = budgetGranted &&
                                      (includeSharedCombatFeedback || !IsSharedCombatGlobalFeedback(feedback));
                }
            }
        }

        private static bool IsSharedCombatGlobalFeedback(MMF_Feedback feedback)
        {
            return feedback is MMF_CameraShake ||
                   feedback is MMF_CameraFieldOfView ||
                   feedback is MMF_FreezeFrame ||
                   feedback is MMF_TimescaleModifier;
        }

        private void ClearTransientVisuals()
        {
            foreach (var trail in trails)
            {
                if (trail != null)
                {
                    trail.Clear();
                }
            }

            foreach (var particle in particles)
            {
                if (particle != null)
                {
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void CacheTransientVisuals()
        {
            trails = GetComponentsInChildren<TrailRenderer>(true);
            particles = GetComponentsInChildren<ParticleSystem>(true);
            CacheFeedbackReferences();
        }

        private void CacheFeedbackReferences()
        {
            targetReference = null; // SerializeReference 목록 안의 실제 Holder를 다시 잡는다.
            globalFeedbacks.Clear();
            targetMotionFeedbacks.Clear();
            targetMotionDefaultActive.Clear();
            if (player != null && player.FeedbacksList != null)
            {
                foreach (var feedback in player.FeedbacksList)
                {
                    if (feedback?.Label?.StartsWith(
                            GlobalFeedbackPrefix,
                            StringComparison.Ordinal) == true)
                    {
                        globalFeedbacks.Add(feedback);
                    }
                    if (targetReference == null && feedback is MMF_ReferenceHolder reference)
                    {
                        targetReference = reference;
                    }
                    if (ResolveTargetMotionChannel(feedback) != TargetMotionChannel.None)
                    {
                        targetMotionFeedbacks.Add(feedback);
                        targetMotionDefaultActive[feedback] = feedback.Active;
                    }
                }
            }
        }

        private void EnsureRuntimeSafeSpringFeedbacks()
        {
            if (safeSpringFeedbacksPrepared || player?.FeedbacksList == null)
            {
                return;
            }

            safeSpringFeedbacksPrepared = true;
            var replaced = false;
            for (var index = 0; index < player.FeedbacksList.Count; index++)
            {
                var feedback = player.FeedbacksList[index];
                if (feedback?.GetType() == typeof(MMF_PositionSpring))
                {
                    var safeFeedback = new BasicAttackSafePositionSpring();
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(feedback), safeFeedback);
                    player.FeedbacksList[index] = safeFeedback;
                    replaced = true;
                }
                else if (feedback?.GetType() == typeof(MMF_RotationSpring))
                {
                    var safeFeedback = new BasicAttackSafeRotationSpring();
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(feedback), safeFeedback);
                    player.FeedbacksList[index] = safeFeedback;
                    replaced = true;
                }
                else if (feedback?.GetType() == typeof(MMF_SquashAndStretchSpring))
                {
                    var safeFeedback = new BasicAttackSafeSquashAndStretchSpring();
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(feedback), safeFeedback);
                    player.FeedbacksList[index] = safeFeedback;
                    replaced = true;
                }
                else if (feedback?.GetType() == typeof(MMF_ScaleSpring))
                {
                    var safeFeedback = new BasicAttackSafeScaleSpring();
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(feedback), safeFeedback);
                    player.FeedbacksList[index] = safeFeedback;
                    replaced = true;
                }
            }

            if (replaced)
            {
                CacheFeedbackReferences();
            }
        }

        private bool SelectTargetMotionFeedbacks(float intensity)
        {
            MMF_Feedback position = null;
            MMF_Feedback rotation = null;
            MMF_Feedback scale = null;
            var positionRank = float.NegativeInfinity;
            var rotationRank = float.NegativeInfinity;
            var scaleRank = float.NegativeInfinity;

            foreach (var feedback in targetMotionFeedbacks)
            {
                feedback.Active = false;
                if (!targetMotionDefaultActive.TryGetValue(feedback, out var defaultActive) ||
                    !defaultActive || !AcceptsIntensity(feedback, intensity))
                {
                    continue;
                }

                var rank = feedback.Timing.UseIntensityInterval
                    ? feedback.Timing.IntensityIntervalMin
                    : 0f;
                switch (ResolveTargetMotionChannel(feedback))
                {
                    case TargetMotionChannel.Position when rank > positionRank:
                        position = feedback;
                        positionRank = rank;
                        break;
                    case TargetMotionChannel.Rotation when rank > rotationRank:
                        rotation = feedback;
                        rotationRank = rank;
                        break;
                    case TargetMotionChannel.Scale when rank > scaleRank:
                        scale = feedback;
                        scaleRank = rank;
                        break;
                }
            }

            if (position != null)
            {
                position.Active = true;
            }
            if (rotation != null)
            {
                rotation.Active = true;
            }
            if (scale != null)
            {
                scale.Active = true;
            }
            return position != null || rotation != null || scale != null;
        }

        private bool HasTargetMotionFeedbackForIntensity(float intensity)
        {
            RestoreTargetMotionFeedbackStates();
            foreach (var feedback in targetMotionFeedbacks)
            {
                if (feedback != null && feedback.Active && AcceptsIntensity(feedback, intensity))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool AcceptsIntensity(MMF_Feedback feedback, float intensity)
        {
            return !feedback.Timing.UseIntensityInterval ||
                   (intensity >= feedback.Timing.IntensityIntervalMin &&
                    intensity <= feedback.Timing.IntensityIntervalMax);
        }

        private static TargetMotionChannel ResolveTargetMotionChannel(MMF_Feedback feedback)
        {
            return feedback switch
            {
                MMF_Position => TargetMotionChannel.Position,
                MMF_PositionShake => TargetMotionChannel.Position,
                MMF_PositionSpring => TargetMotionChannel.Position,
                MMF_Rotation => TargetMotionChannel.Rotation,
                MMF_RotationShake => TargetMotionChannel.Rotation,
                MMF_RotationSpring => TargetMotionChannel.Rotation,
                MMF_Scale => TargetMotionChannel.Scale,
                MMF_ScaleShake => TargetMotionChannel.Scale,
                MMF_ScaleSpring => TargetMotionChannel.Scale,
                MMF_SquashAndStretch => TargetMotionChannel.Scale,
                MMF_SquashAndStretchSpring => TargetMotionChannel.Scale,
                _ => TargetMotionChannel.None
            };
        }

        private void RestoreTargetMotionFeedbackStates()
        {
            foreach (var feedback in targetMotionFeedbacks)
            {
                if (feedback != null && targetMotionDefaultActive.TryGetValue(feedback, out var active))
                {
                    feedback.Active = active;
                }
            }
        }

        private void AcquireTargetMotion(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            var targetTransform = target.transform;
            var targetId = targetTransform.GetInstanceID();
            if (TargetMotionOwners.TryGetValue(targetId, out var previousOwner) &&
                previousOwner != null && previousOwner != this)
            {
                previousOwner.CancelTargetMotionForTakeover();
            }

            ownedMotionTarget = targetTransform;
            ownedMotionTargetId = targetId;
            ownedMotionTargetLocalPosition = SanitizePosition(targetTransform.localPosition);
            ownedMotionTargetLocalRotation = SanitizeRotation(targetTransform.localRotation);
            ownedMotionTargetScale = SanitizeScale(targetTransform.localScale);
            if (!IsFinite(targetTransform.localPosition))
            {
                targetTransform.localPosition = ownedMotionTargetLocalPosition;
            }
            if (!IsFinite(targetTransform.localRotation))
            {
                targetTransform.localRotation = ownedMotionTargetLocalRotation;
            }
            if (!IsFinite(targetTransform.localScale))
            {
                targetTransform.localScale = ownedMotionTargetScale;
            }
            TargetMotionOwners[targetId] = this;
        }

        private void CancelTargetMotionForTakeover()
        {
            if (player != null && initialized)
            {
                player.StopFeedbacks();
                player.RestoreInitialValues();
                player.ResetFeedbacks();
            }
            initialized = false;
            ReleaseTargetMotion();
            RestoreTargetMotionFeedbackStates();
            ClearTransientVisuals();
        }

        private void ReleaseTargetMotion()
        {
            if (ownedMotionTargetId == 0)
            {
                ownedMotionTarget = null;
                return;
            }

            if (TargetMotionOwners.TryGetValue(ownedMotionTargetId, out var owner) && owner == this)
            {
                TargetMotionOwners.Remove(ownedMotionTargetId);
                if (ownedMotionTarget != null)
                {
                    ownedMotionTarget.localPosition = ownedMotionTargetLocalPosition;
                    ownedMotionTarget.localRotation = ownedMotionTargetLocalRotation;
                    ownedMotionTarget.localScale = SanitizeScale(ownedMotionTargetScale);
                }
            }
            ownedMotionTarget = null;
            ownedMotionTargetId = 0;
            ownedMotionTargetLocalPosition = Vector3.zero;
            ownedMotionTargetLocalRotation = Quaternion.identity;
            ownedMotionTargetScale = Vector3.one;
        }

        private static Vector3 SanitizePosition(Vector3 position)
        {
            return IsFinite(position) ? position : Vector3.zero;
        }

        private static Quaternion SanitizeRotation(Quaternion rotation)
        {
            return IsFinite(rotation) ? rotation : Quaternion.identity;
        }

        private static Vector3 SanitizeScale(Vector3 scale)
        {
            return IsFinite(scale) ? scale : Vector3.one;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void BindTarget(GameObject target)
        {
            if (targetReference == null || target == null || player?.FeedbacksList == null)
            {
                return;
            }

            targetReference.GameObjectReference = target;
            targetReference.ForceReferenceOnAll = false; // 아래 허용된 Feedback만 개별 바인딩한다.
            for (var index = 0; index < player.FeedbacksList.Count; index++)
            {
                var feedback = player.FeedbacksList[index];
                if (feedback == null || !feedback.HasAutomatedTargetAcquisition ||
                    feedback.Label?.IndexOf(PrefabTargetToken, StringComparison.Ordinal) >= 0)
                {
                    continue; // Prefab 내부 Light처럼 자체 Target을 가진 Feedback은 피격자로 덮어쓰지 않는다.
                }

                feedback.Owner = player; // 교체된 Spring도 첫 재생부터 Target Acquisition이 가능하게 한다.
                feedback.SetIndexInFeedbacksList(index);
                feedback.ForcedReferenceHolder = targetReference;
                feedback.ForceAutomateTargetAcquisition();
            }
        }

        private enum TargetMotionChannel
        {
            None,
            Position,
            Rotation,
            Scale
        }

#if UNITY_EDITOR
        public void EditorConfigure(MMF_Player feedbackPlayer, MMF_ReferenceHolder referenceHolder = null)
        {
            player = feedbackPlayer;
            targetReference = referenceHolder;
            CacheTransientVisuals();
        }
#endif
    }

    internal static class BasicAttackSpringSafety
    {
        private const float MaximumFrameDelta = 0.1f;
        private const float MinimumSubstepsPerSecond = 240f;

        public static void Simulate(
            ref Vector3 current,
            ref Vector3 target,
            ref Vector3 velocity,
            Vector3 damping,
            Vector3 frequency,
            float deltaTime,
            Vector3 fallback,
            float maximumOffset,
            float maximumVelocity)
        {
            target = ClampAround(IsFinite(target) ? target : fallback, fallback, maximumOffset);
            current = ClampAround(IsFinite(current) ? current : fallback, fallback, maximumOffset);
            velocity = Clamp(IsFinite(velocity) ? velocity : Vector3.zero, maximumVelocity);

            var remaining = Mathf.Clamp(IsFinite(deltaTime) ? deltaTime : 0f, 0f, MaximumFrameDelta);
            var maximumFrequency = Mathf.Max(
                Mathf.Abs(frequency.x),
                Mathf.Abs(frequency.y),
                Mathf.Abs(frequency.z));
            var maximumStep = 1f / Mathf.Max(
                MinimumSubstepsPerSecond,
                maximumFrequency * 12f);
            while (remaining > 0f)
            {
                var step = Mathf.Min(remaining, maximumStep);
                MMMaths.Spring(ref current.x, target.x, ref velocity.x, damping.x, frequency.x, step);
                MMMaths.Spring(ref current.y, target.y, ref velocity.y, damping.y, frequency.y, step);
                MMMaths.Spring(ref current.z, target.z, ref velocity.z, damping.z, frequency.z, step);
                current = ClampAround(IsFinite(current) ? current : fallback, fallback, maximumOffset);
                velocity = Clamp(IsFinite(velocity) ? velocity : Vector3.zero, maximumVelocity);
                remaining -= step;
            }
        }

        public static void Simulate(
            ref float current,
            ref float target,
            ref float velocity,
            float damping,
            float frequency,
            float deltaTime,
            float fallback,
            float maximumOffset,
            float maximumVelocity)
        {
            target = ClampAround(IsFinite(target) ? target : fallback, fallback, maximumOffset);
            current = ClampAround(IsFinite(current) ? current : fallback, fallback, maximumOffset);
            velocity = Mathf.Clamp(IsFinite(velocity) ? velocity : 0f, -maximumVelocity, maximumVelocity);

            var remaining = Mathf.Clamp(IsFinite(deltaTime) ? deltaTime : 0f, 0f, MaximumFrameDelta);
            var maximumStep = 1f / Mathf.Max(
                MinimumSubstepsPerSecond,
                Mathf.Abs(frequency) * 12f);
            while (remaining > 0f)
            {
                var step = Mathf.Min(remaining, maximumStep);
                MMMaths.Spring(ref current, target, ref velocity, damping, frequency, step);
                current = ClampAround(IsFinite(current) ? current : fallback, fallback, maximumOffset);
                velocity = Mathf.Clamp(IsFinite(velocity) ? velocity : 0f, -maximumVelocity, maximumVelocity);
                remaining -= step;
            }
        }

        public static Vector3 ClampAround(Vector3 value, Vector3 fallback, float maximumOffset)
        {
            if (!IsFinite(value))
            {
                return fallback;
            }

            maximumOffset = Mathf.Max(0.01f, maximumOffset);
            return new Vector3(
                Mathf.Clamp(value.x, fallback.x - maximumOffset, fallback.x + maximumOffset),
                Mathf.Clamp(value.y, fallback.y - maximumOffset, fallback.y + maximumOffset),
                Mathf.Clamp(value.z, fallback.z - maximumOffset, fallback.z + maximumOffset));
        }

        private static float ClampAround(float value, float fallback, float maximumOffset)
        {
            maximumOffset = Mathf.Max(0.01f, maximumOffset);
            return Mathf.Clamp(value, fallback - maximumOffset, fallback + maximumOffset);
        }

        private static Vector3 Clamp(Vector3 value, float maximumMagnitude)
        {
            maximumMagnitude = Mathf.Max(0.01f, maximumMagnitude);
            return new Vector3(
                Mathf.Clamp(value.x, -maximumMagnitude, maximumMagnitude),
                Mathf.Clamp(value.y, -maximumMagnitude, maximumMagnitude),
                Mathf.Clamp(value.z, -maximumMagnitude, maximumMagnitude));
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [Serializable]
    internal sealed class BasicAttackSafePositionSpring : MMF_PositionSpring
    {
        private const float MaximumPositionOffset = 8f;
        private const float MaximumPositionVelocity = 120f;

        protected override void UpdateSpring()
        {
            BasicAttackSpringSafety.Simulate(
                ref _currentValue,
                ref _targetValue,
                ref _velocity,
                new Vector3(DampingX, DampingY, DampingZ),
                new Vector3(FrequencyX, FrequencyY, FrequencyZ),
                FeedbackDeltaTime,
                _initialPosition,
                MaximumPositionOffset,
                MaximumPositionVelocity);
            ApplyValue();
        }

        protected override void ApplyValue()
        {
            if (AnimatePositionTarget == null)
            {
                return;
            }

            _currentValue = BasicAttackSpringSafety.ClampAround(
                _currentValue,
                _initialPosition,
                MaximumPositionOffset);
            base.ApplyValue();
        }
    }

    [Serializable]
    internal sealed class BasicAttackSafeRotationSpring : MMF_RotationSpring
    {
        private const float MaximumRotationOffset = 720f;
        private const float MaximumRotationVelocity = 7200f;

        protected override void UpdateSpring()
        {
            BasicAttackSpringSafety.Simulate(
                ref _currentValue,
                ref _targetValue,
                ref _velocity,
                new Vector3(DampingX, DampingY, DampingZ),
                new Vector3(FrequencyX, FrequencyY, FrequencyZ),
                FeedbackDeltaTime,
                _initialRotation,
                MaximumRotationOffset,
                MaximumRotationVelocity);
            ApplyValue();
        }

        protected override void ApplyValue()
        {
            if (AnimateRotationTarget == null)
            {
                return;
            }

            _currentValue = BasicAttackSpringSafety.ClampAround(
                _currentValue,
                _initialRotation,
                MaximumRotationOffset);
            base.ApplyValue();
        }
    }

    [Serializable]
    internal sealed class BasicAttackSafeSquashAndStretchSpring : MMF_SquashAndStretchSpring
    {
        private const float MinimumScaleRatio = 0.35f;
        private const float MaximumScaleRatio = 2.5f;

        protected override void GetInitialValues()
        {
            _initialScale = AnimateScaleTarget.localScale;
            _currentValue = GetDriverScale(_initialScale);
            _targetValue = _currentValue;
        }

        protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1f)
        {
            if (!Active || !MMF_SquashAndStretchSpring.FeedbackTypeAuthorized ||
                AnimateScaleTarget == null)
            {
                return;
            }

            if (_coroutine != null)
            {
                Owner.StopCoroutine(_coroutine);
            }

            switch (Mode)
            {
                case Modes.MoveTo:
                    _targetValue = UnityEngine.Random.Range(MoveToMin, MoveToMax);
                    break;
                case Modes.MoveToAdditive:
                    _targetValue += UnityEngine.Random.Range(MoveToMin, MoveToMax);
                    break;
                case Modes.Bump:
                    _velocity = UnityEngine.Random.Range(BumpScaleMin, BumpScaleMax) *
                                ComputeIntensity(feedbacksIntensity, position);
                    break;
            }
            _coroutine = Owner.StartCoroutine(Spring());
        }

        protected override void UpdateSpring()
        {
            var fallback = GetDriverScale(_initialScale);
            BasicAttackSpringSafety.Simulate(
                ref _currentValue,
                ref _targetValue,
                ref _velocity,
                Damping,
                Frequency,
                FeedbackDeltaTime,
                fallback,
                Mathf.Max(1f, Mathf.Abs(fallback) * 2.5f),
                Mathf.Max(8f, Mathf.Max(Mathf.Abs(BumpScaleMin), Mathf.Abs(BumpScaleMax)) * 3f));
            ApplyValue();
        }

        protected override void ApplyValue()
        {
            if (AnimateScaleTarget == null)
            {
                return;
            }

            var driverScale = GetDriverScale(_initialScale);
            if (!IsFinite(driverScale) || Mathf.Abs(driverScale) < 0.0001f)
            {
                driverScale = 1f;
            }
            if (!IsFinite(_targetValue))
            {
                _targetValue = driverScale;
            }
            if (!IsFinite(_currentValue))
            {
                _currentValue = _targetValue;
                _velocity = 0f;
            }

            var ratio = _currentValue / driverScale;
            if (!IsFinite(ratio))
            {
                ratio = 1f;
                _velocity = 0f;
            }
            ratio = Mathf.Clamp(ratio, MinimumScaleRatio, MaximumScaleRatio);
            _currentValue = driverScale * ratio;
            var inverseRatio = 1f / Mathf.Sqrt(ratio);
            var multiplier = Vector3.one;
            switch (Axis)
            {
                case PossibleAxis.XtoYZ:
                    multiplier = new Vector3(ratio, inverseRatio, inverseRatio);
                    break;
                case PossibleAxis.XtoY:
                    multiplier = new Vector3(ratio, inverseRatio, 1f);
                    break;
                case PossibleAxis.XtoZ:
                    multiplier = new Vector3(ratio, 1f, inverseRatio);
                    break;
                case PossibleAxis.YtoXZ:
                    multiplier = new Vector3(inverseRatio, ratio, inverseRatio);
                    break;
                case PossibleAxis.YtoX:
                    multiplier = new Vector3(inverseRatio, ratio, 1f);
                    break;
                case PossibleAxis.YtoZ:
                    multiplier = new Vector3(1f, ratio, inverseRatio);
                    break;
                case PossibleAxis.ZtoXZ:
                    multiplier = new Vector3(inverseRatio, inverseRatio, ratio);
                    break;
                case PossibleAxis.ZtoX:
                    multiplier = new Vector3(inverseRatio, 1f, ratio);
                    break;
                case PossibleAxis.ZtoY:
                    multiplier = new Vector3(1f, inverseRatio, ratio);
                    break;
            }

            _velocity = ClampVelocity(_velocity);
            _newScale = Vector3.Scale(_initialScale, multiplier);
            AnimateScaleTarget.localScale = IsFinite(_newScale) ? _newScale : _initialScale;
        }

        private float GetDriverScale(Vector3 scale)
        {
            return Axis switch
            {
                PossibleAxis.YtoXZ or PossibleAxis.YtoX or PossibleAxis.YtoZ => scale.y,
                PossibleAxis.ZtoXZ or PossibleAxis.ZtoX or PossibleAxis.ZtoY => scale.z,
                _ => scale.x
            };
        }

        private float ClampVelocity(float velocity)
        {
            if (!IsFinite(velocity))
            {
                return 0f;
            }
            var limit = Mathf.Max(1f, Mathf.Max(Mathf.Abs(BumpScaleMin), Mathf.Abs(BumpScaleMax)) * 3f);
            return Mathf.Clamp(velocity, -limit, limit);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [Serializable]
    internal sealed class BasicAttackSafeScaleSpring : MMF_ScaleSpring
    {
        private const float MinimumScaleRatio = 0.35f;
        private const float MaximumScaleRatio = 2.5f;

        protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1f)
        {
            if (!Active || !MMF_ScaleSpring.FeedbackTypeAuthorized || AnimateScaleTarget == null)
            {
                return;
            }

            if (_coroutine != null)
            {
                Owner.StopCoroutine(_coroutine);
            }

            switch (Mode)
            {
                case Modes.MoveTo:
                    _targetValue = new Vector3(
                        UnityEngine.Random.Range(MoveToScaleMin.x, MoveToScaleMax.x),
                        UnityEngine.Random.Range(MoveToScaleMin.y, MoveToScaleMax.y),
                        UnityEngine.Random.Range(MoveToScaleMin.z, MoveToScaleMax.z));
                    break;
                case Modes.MoveToAdditive:
                    _targetValue += new Vector3(
                        UnityEngine.Random.Range(MoveToScaleMin.x, MoveToScaleMax.x),
                        UnityEngine.Random.Range(MoveToScaleMin.y, MoveToScaleMax.y),
                        UnityEngine.Random.Range(MoveToScaleMin.z, MoveToScaleMax.z));
                    break;
                case Modes.Bump:
                    var intensity = ComputeIntensity(feedbacksIntensity, position);
                    _velocity = new Vector3(
                        UnityEngine.Random.Range(BumpScaleMin.x, BumpScaleMax.x),
                        UnityEngine.Random.Range(BumpScaleMin.y, BumpScaleMax.y),
                        UnityEngine.Random.Range(BumpScaleMin.z, BumpScaleMax.z)) * intensity;
                    break;
            }
            _coroutine = Owner.StartCoroutine(Spring());
        }

        protected override void UpdateSpring()
        {
            BasicAttackSpringSafety.Simulate(
                ref _currentValue,
                ref _targetValue,
                ref _velocity,
                new Vector3(DampingX, DampingY, DampingZ),
                new Vector3(FrequencyX, FrequencyY, FrequencyZ),
                FeedbackDeltaTime,
                _initialScale,
                Mathf.Max(1f, BasicAttackSpringSafety.IsFinite(_initialScale)
                    ? Mathf.Max(Mathf.Abs(_initialScale.x), Mathf.Abs(_initialScale.y), Mathf.Abs(_initialScale.z)) * 2.5f
                    : 2.5f),
                24f);
            ApplyValue();
        }

        protected override void ApplyValue()
        {
            if (AnimateScaleTarget == null)
            {
                return;
            }

            _currentValue = new Vector3(
                ClampAxis(_currentValue.x, _initialScale.x, ref _velocity.x),
                ClampAxis(_currentValue.y, _initialScale.y, ref _velocity.y),
                ClampAxis(_currentValue.z, _initialScale.z, ref _velocity.z));
            AnimateScaleTarget.localScale = _currentValue;
        }

        private static float ClampAxis(float value, float initial, ref float velocity)
        {
            if (!IsFinite(initial) || Mathf.Abs(initial) < 0.0001f)
            {
                initial = 1f;
            }
            if (!IsFinite(value))
            {
                velocity = 0f;
                return initial;
            }
            if (!IsFinite(velocity))
            {
                velocity = 0f;
            }
            var ratio = Mathf.Clamp(value / initial, MinimumScaleRatio, MaximumScaleRatio);
            return initial * ratio;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
