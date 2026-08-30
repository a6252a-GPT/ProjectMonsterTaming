using UnityEngine;

namespace ProjectMT.Contents.FallenCommander.Editor
{
    internal static class FallenCommanderPreviewRangeRenderer
    {
        public static FallenCommanderTelegraphView CreateCircle(
            GameObject prefab,
            Transform parent,
            Vector3 position,
            float radius,
            Color color)
        {
            return Prepare(FallenCommanderTelegraphView.CreateCircle(
                prefab,
                parent,
                position,
                radius,
                color));
        }

        public static FallenCommanderTelegraphView CreateLine(
            GameObject prefab,
            Transform parent,
            Vector3 position,
            Vector3 direction,
            float width,
            float length,
            Color color)
        {
            return Prepare(FallenCommanderTelegraphView.CreateLine(
                prefab,
                parent,
                position,
                direction,
                width,
                length,
                color));
        }

        private static FallenCommanderTelegraphView Prepare(
            FallenCommanderTelegraphView telegraph)
        {
            if (telegraph != null)
            {
                telegraph.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }

            return telegraph;
        }
    }
}
