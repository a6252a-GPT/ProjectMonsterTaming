using UnityEngine;

namespace ProjectMT.Contents.FallenCommander.Editor
{
    internal static class FallenCommanderPreviewCleanup
    {
        public static void Destroy(GameObject instance)
        {
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }
        }

        public static void Destroy(ref FallenCommanderTelegraphView telegraph)
        {
            if (telegraph == null)
            {
                return;
            }

            Destroy(telegraph.gameObject);
            telegraph = null;
        }
    }
}
