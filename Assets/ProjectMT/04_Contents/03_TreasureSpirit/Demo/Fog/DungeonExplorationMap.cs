using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    public enum AutomapPoiType
    {
        Key = 0,
        Prison = 1
    }

    public readonly struct AutomapPoi
    {
        public AutomapPoi(AutomapPoiType type, Vector3 worldPosition)
        {
            Type = type;
            WorldPosition = worldPosition;
        }

        public AutomapPoiType Type { get; }
        public Vector3 WorldPosition { get; }
    }

    public sealed class DungeonExplorationMap : MonoBehaviour
    {
        public const int TextureSize = 512;
        public const float RevealRadius = 6.5f;

        private Transform player;
        private Bounds worldBounds;
        private Texture2D layoutTexture;
        private Texture2D exploreTexture;
        private Texture2D displayTexture;
        private Color32[] layoutPixels;
        private Color32[] explorePixels;
        private Color32[] displayPixels;
        private readonly List<AutomapPoi> pointsOfInterest = new List<AutomapPoi>();
        private bool exploreDirty = true;
        private float nextApplyTime;
        private Vector2Int lastStampPixel = new Vector2Int(int.MinValue, 0);

        public Texture DisplayTexture => displayTexture;
        public Bounds WorldBounds => worldBounds;
        public IReadOnlyList<AutomapPoi> PointsOfInterest => pointsOfInterest;

        public void Initialize(Transform mapRoot, Transform playerTransform)
        {
            player = playerTransform;
            if (!TryResolveBounds(mapRoot, out worldBounds))
            {
                worldBounds = new Bounds(mapRoot != null ? mapRoot.position : Vector3.zero, new Vector3(64f, 4f, 64f));
            }

            layoutPixels = new Color32[TextureSize * TextureSize];
            explorePixels = new Color32[TextureSize * TextureSize];
            displayPixels = new Color32[TextureSize * TextureSize];
            BakeLayout(mapRoot);
            layoutTexture = CreateTexture(layoutPixels, false);
            exploreTexture = CreateTexture(explorePixels, true);
            displayTexture = CreateTexture(displayPixels, true);
            CollectPointsOfInterest(mapRoot);
            RebuildDisplay();
        }

        public void SetPlayer(Transform playerTransform)
        {
            player = playerTransform;
        }

        public Vector2 WorldToNormalized(Vector3 worldPosition)
        {
            float u = Mathf.InverseLerp(worldBounds.min.x, worldBounds.max.x, worldPosition.x);
            float v = Mathf.InverseLerp(worldBounds.min.z, worldBounds.max.z, worldPosition.z);
            return new Vector2(u, v);
        }

        public bool IsExplored(Vector3 worldPosition)
        {
            if (explorePixels == null)
            {
                return false;
            }

            Vector2Int pixel = WorldToPixel(worldPosition);
            return explorePixels[(pixel.y * TextureSize) + pixel.x].r >= 12;
        }

        private void LateUpdate()
        {
            if (player == null)
            {
                return;
            }

            Stamp(player.position, RevealRadius);
            if (exploreDirty && Time.unscaledTime >= nextApplyTime)
            {
                RebuildDisplay();
            }
        }

        private void OnDestroy()
        {
            DestroyTexture(ref layoutTexture);
            DestroyTexture(ref exploreTexture);
            DestroyTexture(ref displayTexture);
        }

        private void Stamp(Vector3 worldPosition, float radius)
        {
            Vector2Int center = WorldToPixel(worldPosition);
            if (center == lastStampPixel)
            {
                return;
            }

            lastStampPixel = center;
            int pixelRadius = Mathf.Max(1, Mathf.CeilToInt(radius / CellSize()));
            int radiusSq = pixelRadius * pixelRadius;
            bool changed = false;

            for (int dy = -pixelRadius; dy <= pixelRadius; dy++)
            {
                int y = center.y + dy;
                if (y < 0 || y >= TextureSize)
                {
                    continue;
                }

                for (int dx = -pixelRadius; dx <= pixelRadius; dx++)
                {
                    if ((dx * dx) + (dy * dy) > radiusSq)
                    {
                        continue;
                    }

                    int x = center.x + dx;
                    if (x < 0 || x >= TextureSize)
                    {
                        continue;
                    }

                    int index = (y * TextureSize) + x;
                    if (explorePixels[index].r == 255)
                    {
                        continue;
                    }

                    explorePixels[index] = new Color32(255, 255, 255, 255);
                    changed = true;
                }
            }

            if (changed)
            {
                exploreDirty = true;
            }
        }

        private void RebuildDisplay()
        {
            Color32 unexplored = new Color32(8, 8, 10, 255);
            Color32 floor = new Color32(148, 150, 156, 255);
            Color32 wall = new Color32(18, 36, 92, 255);
            Color32 empty = new Color32(52, 54, 58, 255);

            for (int i = 0; i < displayPixels.Length; i++)
            {
                if (explorePixels[i].r < 12)
                {
                    displayPixels[i] = unexplored;
                    continue;
                }

                Color32 layout = layoutPixels[i];
                if (layout.g > 40)
                {
                    displayPixels[i] = wall;
                }
                else if (layout.r > 40)
                {
                    displayPixels[i] = floor;
                }
                else
                {
                    displayPixels[i] = empty;
                }
            }

            displayTexture.SetPixels32(displayPixels);
            displayTexture.Apply(false, false);
            exploreDirty = false;
            nextApplyTime = Time.unscaledTime + 0.12f;
        }

        private void BakeLayout(Transform mapRoot)
        {
            if (mapRoot == null)
            {
                return;
            }

            Renderer[] renderers = mapRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                string objectName = renderer.gameObject.name;
                if (objectName.StartsWith(DemoFloorBounds.FloorNamePrefix))
                {
                    StampBounds(renderer.bounds, 180, 0);
                }
                else if (objectName.StartsWith("Wall_"))
                {
                    StampBounds(renderer.bounds, 0, 220);
                }
            }
        }

        private void CollectPointsOfInterest(Transform mapRoot)
        {
            pointsOfInterest.Clear();
            if (mapRoot == null)
            {
                return;
            }

            List<Transform> chests = DemoMapUtil.CollectChestMarkers(mapRoot);
            if (chests.Count > 0)
            {
                chests.Sort(CompareChestMarkers);
                pointsOfInterest.Add(new AutomapPoi(AutomapPoiType.Key, chests[0].position));
            }

            Transform prison = DemoMapUtil.FindDeepChild(mapRoot, DemoMapUtil.PrisonMarkerName);
            if (prison == null)
            {
                prison = DemoMapUtil.FindEndRoom(mapRoot);
            }

            if (prison != null)
            {
                pointsOfInterest.Add(new AutomapPoi(AutomapPoiType.Prison, prison.position));
            }
        }

        private static int CompareChestMarkers(Transform a, Transform b)
        {
            Transform roomA = DemoMapUtil.FindRoomRoot(a);
            Transform roomB = DemoMapUtil.FindRoomRoot(b);
            string nameA = roomA != null ? roomA.name : a.name;
            string nameB = roomB != null ? roomB.name : b.name;
            return string.CompareOrdinal(nameA, nameB);
        }

        private void StampBounds(Bounds bounds, byte floorValue, byte wallValue)
        {
            Vector2Int min = WorldToPixel(new Vector3(bounds.min.x, 0f, bounds.min.z));
            Vector2Int max = WorldToPixel(new Vector3(bounds.max.x, 0f, bounds.max.z));
            int x0 = Mathf.Clamp(Mathf.Min(min.x, max.x) - 1, 0, TextureSize - 1);
            int x1 = Mathf.Clamp(Mathf.Max(min.x, max.x) + 1, 0, TextureSize - 1);
            int y0 = Mathf.Clamp(Mathf.Min(min.y, max.y) - 1, 0, TextureSize - 1);
            int y1 = Mathf.Clamp(Mathf.Max(min.y, max.y) + 1, 0, TextureSize - 1);

            for (int y = y0; y <= y1; y++)
            {
                int row = y * TextureSize;
                for (int x = x0; x <= x1; x++)
                {
                    int index = row + x;
                    Color32 pixel = layoutPixels[index];
                    if (floorValue > pixel.r)
                    {
                        pixel.r = floorValue;
                    }

                    if (wallValue > pixel.g)
                    {
                        pixel.g = wallValue;
                    }

                    layoutPixels[index] = pixel;
                }
            }
        }

        private Vector2Int WorldToPixel(Vector3 worldPosition)
        {
            float u = Mathf.InverseLerp(worldBounds.min.x, worldBounds.max.x, worldPosition.x);
            float v = Mathf.InverseLerp(worldBounds.min.z, worldBounds.max.z, worldPosition.z);
            int x = Mathf.Clamp(Mathf.FloorToInt(u * (TextureSize - 1)), 0, TextureSize - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(v * (TextureSize - 1)), 0, TextureSize - 1);
            return new Vector2Int(x, y);
        }

        private float CellSize()
        {
            return Mathf.Max(worldBounds.size.x, worldBounds.size.z) / TextureSize;
        }

        private static bool TryResolveBounds(Transform mapRoot, out Bounds bounds)
        {
            if (DemoFloorBounds.TryGetBounds(mapRoot, out bounds))
            {
                bounds.Expand(new Vector3(4f, 0f, 4f));
                return true;
            }

            bounds = default;
            return false;
        }

        private static Texture2D CreateTexture(Color32[] pixels, bool readable)
        {
            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = readable ? "AutomapExplore" : "AutomapLayout"
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, !readable);
            return texture;
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            Destroy(texture);
            texture = null;
        }
    }
}
