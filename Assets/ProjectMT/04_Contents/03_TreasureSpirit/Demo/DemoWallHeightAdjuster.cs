using System;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    /// <summary>
    /// 벽/기둥/문의 Transform localScale.y를 1에서 0.6으로 바꿉니다.
    /// </summary>
    internal static class DemoWallHeightAdjuster
    {
        private const float SourceScaleY = 1f;
        private const float TargetScaleY = 0.6f;

        public static void Apply(Transform root)
        {
            if (root == null)
            {
                return;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            int adjustedCount = 0;

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform target = transforms[i];
                if (target == null || !IsWallColumnOrDoor(target.name))
                {
                    continue;
                }

                Vector3 scale = target.localScale;
                if (!Mathf.Approximately(scale.y, SourceScaleY))
                {
                    continue;
                }

                scale.y = TargetScaleY;
                target.localScale = scale;
                adjustedCount++;
            }

            if (adjustedCount > 0)
            {
                Debug.Log($"[DemoWallHeightAdjuster] localScale.y 1→0.6 적용: {adjustedCount}개 ({root.name})");
            }
        }

        private static bool IsWallColumnOrDoor(string objectName)
        {
            return objectName.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   objectName.IndexOf("Column", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   objectName.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
