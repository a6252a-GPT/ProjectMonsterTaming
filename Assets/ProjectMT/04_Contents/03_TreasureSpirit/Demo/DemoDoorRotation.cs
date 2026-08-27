using System.Collections;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal static class DemoDoorRotation
    {
        public static IEnumerator RotateLocalY(Transform pivot, float openAngle, float openSpeed)
        {
            if (pivot == null)
            {
                yield break;
            }

            Quaternion startRotation = pivot.localRotation;
            Quaternion targetRotation = startRotation * Quaternion.Euler(0f, openAngle, 0f);

            float progress = 0f;
            while (progress < 1f)
            {
                progress += Time.deltaTime * openSpeed;
                pivot.localRotation = Quaternion.Slerp(startRotation, targetRotation, progress);
                yield return null;
            }

            pivot.localRotation = targetRotation;
        }
    }
}
