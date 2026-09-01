using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    internal static class DemoFloorBounds
    {
        public const string FloorNamePrefix = "Floor_Flat";

        public static bool TryGetBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
            {
                return false;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool initialized = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.gameObject.name.StartsWith(FloorNamePrefix))
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return initialized;
        }

        public static bool TryGetInteriorBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
            {
                return false;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool initialized = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null ||
                    !renderer.gameObject.name.StartsWith(FloorNamePrefix) ||
                    IsInsideCorridor(renderer.transform, root))
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return initialized;
        }

        public static bool TryGetCombinedBounds(IReadOnlyList<Transform> roots, out Bounds bounds)
        {
            bounds = default;
            bool initialized = false;
            if (roots == null)
            {
                return false;
            }

            for (int i = 0; i < roots.Count; i++)
            {
                if (!TryGetBounds(roots[i], out Bounds piece))
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = piece;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(piece);
                }
            }

            return initialized;
        }

        private static bool IsInsideCorridor(Transform target, Transform stopAt)
        {
            Transform current = target;
            while (current != null && current != stopAt)
            {
                if (current.name.StartsWith("Corridor", System.StringComparison.Ordinal))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        public static bool TryGetSurface(Transform root, out Vector3 surfacePoint)
        {
            surfacePoint = default;
            if (!TryGetBounds(root, out Bounds bounds))
            {
                return false;
            }

            surfacePoint = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            return true;
        }

        public static bool TryGetPatrolBounds(Transform roomRoot, out Vector3 center, out float radius)
        {
            center = roomRoot != null ? roomRoot.position : Vector3.zero;
            radius = 3f;

            if (!TryGetBounds(roomRoot, out Bounds bounds))
            {
                return false;
            }

            center = bounds.center;
            radius = Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.55f;
            radius = Mathf.Clamp(radius, 1.5f, 4f);
            return true;
        }
    }
}
