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
    }
}
