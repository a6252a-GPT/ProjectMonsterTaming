using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    public static class CastleAssaultRouteMath // 기본형 공략 경로의 순수 계산
    {
        public static int ResolveNextInternalLayer(int enteredLayer, int totalLayerCount)
        {
            var layerCount = Mathf.Max(1, totalLayerCount);
            var nextLayer = Mathf.Max(-1, enteredLayer) + 1;
            return nextLayer < layerCount ? nextLayer : -1;
        }

        public static int ResolveDisplayLayer(int internalLayer, int totalLayerCount)
        {
            if (internalLayer < 0 || internalLayer >= totalLayerCount)
            {
                return 0;
            }

            return totalLayerCount - internalLayer;
        }

        public static float EstimateRouteSeconds(
            float pathDistance,
            float moveSpeed,
            float remainingHealth,
            float damagePerSecond,
            float continuationDistance = 0f)
        {
            var movementSeconds = (Mathf.Max(0f, pathDistance) + Mathf.Max(0f, continuationDistance)) /
                                  Mathf.Max(0.1f, moveSpeed);
            var destructionSeconds = Mathf.Max(0f, remainingHealth) / Mathf.Max(0.1f, damagePerSecond);
            return movementSeconds + destructionSeconds;
        }

        public static bool ShouldOpenAdditionalBreach(
            float newBreachSeconds,
            float openedRouteSeconds,
            bool wallBreaker,
            int openedOuterRouteCount = 0,
            int maximumOuterRouteCount = int.MaxValue,
            int pendingOuterBreachCount = 0)
        {
            if (pendingOuterBreachCount > 0 || openedOuterRouteCount >= Mathf.Max(1, maximumOuterRouteCount))
            {
                return false;
            }

            if (float.IsNaN(newBreachSeconds) || float.IsInfinity(newBreachSeconds))
            {
                return false;
            }

            if (float.IsNaN(openedRouteSeconds) || float.IsInfinity(openedRouteSeconds))
            {
                return true;
            }

            var requiredRatio = wallBreaker ? 0.78f : 0.65f; // 새 돌파가 확실히 빠를 때만 외곽벽을 더 연다
            return Mathf.Max(0f, newBreachSeconds) <= Mathf.Max(0f, openedRouteSeconds) * requiredRatio;
        }

        public static bool ShouldWaitForPendingBreach(int openedRouteCount, int pendingOuterBreachCount)
        {
            return openedRouteCount <= 0 && pendingOuterBreachCount > 0;
        }

        public static bool IsIncidentalBuilding(float pathDistance, float clearRadius)
        {
            return !float.IsNaN(pathDistance) && !float.IsInfinity(pathDistance) &&
                   pathDistance >= 0f && pathDistance <= Mathf.Max(0f, clearRadius);
        }

        public static bool HasCrossedInward(
            Vector3 previousPosition,
            Vector3 currentPosition,
            Vector3 wallPosition,
            Vector3 inward,
            float lateralTolerance)
        {
            inward.y = 0f;
            if (inward.sqrMagnitude <= 0.5f)
            {
                return false;
            }

            inward.Normalize();
            var previousOffset = previousPosition - wallPosition;
            var currentOffset = currentPosition - wallPosition;
            previousOffset.y = 0f;
            currentOffset.y = 0f;
            var previousDepth = Vector3.Dot(previousOffset, inward);
            var currentDepth = Vector3.Dot(currentOffset, inward);
            if (previousDepth > 0.05f || currentDepth <= 0.05f)
            {
                return false;
            }

            var depthSpan = currentDepth - previousDepth;
            var crossingTime = depthSpan <= 0.0001f ? 1f : Mathf.Clamp01(-previousDepth / depthSpan);
            var crossingPoint = Vector3.Lerp(previousPosition, currentPosition, crossingTime);
            var lateral = crossingPoint - wallPosition;
            lateral.y = 0f;
            lateral -= inward * Vector3.Dot(lateral, inward);
            return lateral.sqrMagnitude <= Mathf.Max(0.1f, lateralTolerance) * Mathf.Max(0.1f, lateralTolerance);
        }

        public static bool IsAtBreachInside(
            Vector3 currentPosition,
            Vector3 wallPosition,
            Vector3 inward,
            float lateralTolerance)
        {
            inward.y = 0f;
            if (inward.sqrMagnitude <= 0.5f)
            {
                return false;
            }

            inward.Normalize();
            var offset = currentPosition - wallPosition;
            offset.y = 0f;
            var inwardDepth = Vector3.Dot(offset, inward);
            if (inwardDepth <= 0.2f)
            {
                return false;
            }

            var lateral = offset - inward * inwardDepth;
            var tolerance = Mathf.Max(0.1f, lateralTolerance);
            return lateral.sqrMagnitude <= tolerance * tolerance;
        }
    }
}
