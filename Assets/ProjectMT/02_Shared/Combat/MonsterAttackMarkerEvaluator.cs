using System;
using ProjectMT.Shared.Unit;

namespace ProjectMT.Shared.Combat
{
    public static class MonsterAttackMarkerEvaluator // Preview와 Runtime이 공유하는 Marker 통과 검사
    {
        public static void EvaluatePassed(
            MonsterAttackMarker[] markers,
            float previousNormalizedTime,
            float currentNormalizedTime,
            ref int nextMarkerIndex,
            Action<int, MonsterAttackMarker> onPassed)
        {
            if (markers == null || markers.Length == 0 || onPassed == null ||
                currentNormalizedTime < previousNormalizedTime)
            {
                return;
            }

            nextMarkerIndex = Math.Max(0, nextMarkerIndex);
            while (nextMarkerIndex < markers.Length)
            {
                var marker = markers[nextMarkerIndex];
                if (marker == null)
                {
                    nextMarkerIndex++;
                    continue;
                }

                var markerTime = marker.NormalizedTime;
                if (markerTime > currentNormalizedTime)
                {
                    break;
                }

                var passed = previousNormalizedTime < markerTime ||
                             markerTime <= 0f && previousNormalizedTime < 0f;
                var markerIndex = nextMarkerIndex;
                nextMarkerIndex++;
                if (passed)
                {
                    onPassed(markerIndex, marker);
                }
            }
        }
    }
}
