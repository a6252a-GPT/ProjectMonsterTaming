using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    /// <summary>
    /// Hard1의 Ceiling_SquareLarge 바닥에서 위로 Fire Spray를 설치합니다.
    /// </summary>
    internal static class DemoFirePillarTrapInstaller
    {
        public static void Install(Transform mapRoot)
        {
            if (mapRoot == null || mapRoot.name.IndexOf("Hard1", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            Transform[] transforms = mapRoot.GetComponentsInChildren<Transform>(true);
            Transform parent = mapRoot;
            int count = 0;

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform ceiling = transforms[i];
                if (ceiling == null || !IsCeilingSquareLarge(ceiling.name))
                {
                    continue;
                }

                DemoFirePillarTrap.Spawn(parent, ceiling, count);
                count++;
            }
        }

        private static bool IsCeilingSquareLarge(string objectName)
        {
            return !string.IsNullOrEmpty(objectName) &&
                   (objectName == "Ceiling_SquareLarge" ||
                    objectName.StartsWith("Ceiling_SquareLarge "));
        }
    }
}
