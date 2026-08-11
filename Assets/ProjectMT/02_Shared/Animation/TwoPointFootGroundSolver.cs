using UnityEngine;

namespace ProjectMT.Shared.Animation
{
    public static class TwoPointFootGroundSolver
    {
        public readonly struct Settings
        {
            public readonly LayerMask GroundLayer;
            public readonly Vector3 UpDirection;
            public readonly float ProbeUpDistance;
            public readonly float ProbeDownDistance;
            public readonly float ProbeRadius;
            public readonly float FlatContactHeightTolerance;
            public readonly float MaxGroundAngle;

            public Settings(
                LayerMask groundLayer,
                Vector3 upDirection,
                float probeUpDistance,
                float probeDownDistance,
                float probeRadius,
                float flatContactHeightTolerance,
                float maxGroundAngle)
            {
                GroundLayer = groundLayer;
                UpDirection = upDirection.sqrMagnitude > 0.0001f ? upDirection.normalized : Vector3.up;
                ProbeUpDistance = Mathf.Max(0f, probeUpDistance);
                ProbeDownDistance = Mathf.Max(0.01f, probeDownDistance);
                ProbeRadius = Mathf.Max(0.001f, probeRadius);
                FlatContactHeightTolerance = Mathf.Max(0f, flatContactHeightTolerance);
                MaxGroundAngle = Mathf.Clamp(maxGroundAngle, 0f, 89f);
            }
        }

        public readonly struct Solution
        {
            public readonly Quaternion TargetRotation;
            public readonly bool HasHeelContact;
            public readonly bool HasToeContact;
            public readonly bool IsFlatSurface;
            public readonly float ParallelAngleError;

            public bool HasTwoContacts => HasHeelContact && HasToeContact;

            public Solution(
                Quaternion targetRotation,
                bool hasHeelContact,
                bool hasToeContact,
                bool isFlatSurface,
                float parallelAngleError)
            {
                TargetRotation = targetRotation;
                HasHeelContact = hasHeelContact;
                HasToeContact = hasToeContact;
                IsFlatSurface = isFlatSurface;
                ParallelAngleError = parallelAngleError;
            }
        }

        public static bool TrySolve(
            Vector3 animatedIkPosition,
            Quaternion animatedIkRotation,
            Transform heel,
            Transform toe,
            in Settings settings,
            out Solution solution)
        {
            solution = default;
            if (heel == null || toe == null)
            {
                return false;
            }

            var hasHeelContact = TryProbe(heel.position, settings, out var heelHit);
            var hasToeContact = TryProbe(toe.position, settings, out var toeHit);
            if (!hasHeelContact && !hasToeContact)
            {
                return false;
            }

            if (!hasHeelContact || !hasToeContact)
            {
                solution = new Solution(
                    animatedIkRotation,
                    hasHeelContact,
                    hasToeContact,
                    false,
                    0f);
                return true;
            }

            var animatedSegment = toe.position - heel.position;
            if (animatedSegment.sqrMagnitude <= 0.000001f)
            {
                solution = new Solution(animatedIkRotation, true, true, false, 0f);
                return true;
            }

            var up = settings.UpDirection;
            var contactHeightDifference = Mathf.Abs(Vector3.Dot(toeHit.point - heelHit.point, up));
            var isFlatSurface = contactHeightDifference <= settings.FlatContactHeightTolerance;
            var targetSegment = isFlatSurface
                ? Vector3.ProjectOnPlane(animatedSegment, up)
                : toeHit.point - heelHit.point;

            if (targetSegment.sqrMagnitude <= 0.000001f)
            {
                targetSegment = Vector3.ProjectOnPlane(animatedSegment, up);
            }

            if (targetSegment.sqrMagnitude <= 0.000001f)
            {
                solution = new Solution(animatedIkRotation, true, true, isFlatSurface, 0f);
                return true;
            }

            if (Vector3.Dot(animatedSegment, targetSegment) < 0f)
            {
                targetSegment = -targetSegment;
            }

            var rotationCorrection = Quaternion.FromToRotation(
                animatedSegment.normalized,
                targetSegment.normalized);
            var targetRotation = rotationCorrection * animatedIkRotation;
            var localHeel = Quaternion.Inverse(animatedIkRotation) * (heel.position - animatedIkPosition);
            var localToe = Quaternion.Inverse(animatedIkRotation) * (toe.position - animatedIkPosition);
            var predictedSegment = targetRotation * (localToe - localHeel);
            var referenceNormal = isFlatSurface
                ? up
                : GetAverageNormal(heelHit.normal, toeHit.normal, up);
            var parallelAngleError = Mathf.Abs(90f - Vector3.Angle(predictedSegment, referenceNormal));

            solution = new Solution(
                targetRotation,
                true,
                true,
                isFlatSurface,
                parallelAngleError);
            return true;
        }

        private static bool TryProbe(Vector3 contactPosition, in Settings settings, out RaycastHit hit)
        {
            var origin = contactPosition + settings.UpDirection * settings.ProbeUpDistance;
            var distance = settings.ProbeUpDistance + settings.ProbeDownDistance;
            var hasHit = Physics.SphereCast(
                origin,
                settings.ProbeRadius,
                -settings.UpDirection,
                out hit,
                distance,
                settings.GroundLayer,
                QueryTriggerInteraction.Ignore);

            return hasHit && Vector3.Angle(hit.normal, settings.UpDirection) <= settings.MaxGroundAngle;
        }

        private static Vector3 GetAverageNormal(Vector3 first, Vector3 second, Vector3 fallback)
        {
            var average = first + second;
            return average.sqrMagnitude > 0.0001f ? average.normalized : fallback;
        }
    }
}
