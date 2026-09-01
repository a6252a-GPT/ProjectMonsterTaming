using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    public sealed class DungeonAutomapOverlay : MonoBehaviour
    {
        private DungeonExplorationMap explorationMap;
        private Transform player;
        private CanvasGroup canvasGroup;
        private RawImage mapImage;
        private RectTransform playerMarker;
        private RectTransform poiRoot;
        private readonly List<RectTransform> poiMarkers = new List<RectTransform>();
        private bool visible;
        private int lastPoiCount = -1;
        private Vector2 lastMarkerUv = new Vector2(-1f, -1f);
        private float lastMarkerYaw = float.NaN;

        public static DungeonAutomapOverlay Ensure(Transform parent, DungeonExplorationMap map, Transform playerTransform)
        {
            DungeonAutomapOverlay overlay = parent != null
                ? parent.GetComponentInChildren<DungeonAutomapOverlay>(true)
                : FindFirstObjectByType<DungeonAutomapOverlay>();
            if (overlay == null)
            {
                GameObject root = new GameObject("AutomapOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
                if (parent != null)
                {
                    root.transform.SetParent(parent, false);
                }

                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 80;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                overlay = root.AddComponent<DungeonAutomapOverlay>();
                overlay.Build();
            }

            overlay.Bind(map, playerTransform);
            return overlay;
        }

        public void Bind(DungeonExplorationMap map, Transform playerTransform)
        {
            explorationMap = map;
            player = playerTransform;
            if (mapImage != null && map != null)
            {
                mapImage.texture = map.DisplayTexture;
            }
        }

        public void Hide()
        {
            visible = false;
            ApplyVisible();
        }

        public void Toggle()
        {
            visible = !visible;
            ApplyVisible();
        }

        private void Update()
        {
            if (WasTogglePressed())
            {
                visible = !visible;
                ApplyVisible();
            }

            if (visible)
            {
                RefreshPlayerMarker();
                RefreshPoiMarkers();
            }
        }

        private void Build()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            RectTransform root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            CreateImage("Dimmer", root, new Color(0.02f, 0.015f, 0.01f, 0.72f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            GameObject frame = CreateImage("MapFrame", root, new Color(0.07f, 0.14f, 0.36f, 0.96f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 600f));
            RectTransform frameRect = frame.GetComponent<RectTransform>();

            GameObject mapObject = new GameObject("Map", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            RectTransform mapRect = mapObject.GetComponent<RectTransform>();
            mapRect.SetParent(frameRect, false);
            mapRect.anchorMin = Vector2.zero;
            mapRect.anchorMax = Vector2.one;
            mapRect.offsetMin = new Vector2(12f, 12f);
            mapRect.offsetMax = new Vector2(-12f, -12f);
            mapImage = mapObject.GetComponent<RawImage>();
            mapImage.color = Color.white;
            mapImage.raycastTarget = false;

            GameObject markerObject = CreateImage("PlayerMarker", mapImage.rectTransform, new Color(1f, 0.22f, 0.16f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 22f));
            playerMarker = markerObject.GetComponent<RectTransform>();
            Image markerImage = markerObject.GetComponent<Image>();
            markerImage.sprite = CreateTriangleSprite();
            markerImage.preserveAspect = true;
            markerImage.raycastTarget = false;

            GameObject poiObject = new GameObject("PoiRoot", typeof(RectTransform));
            poiRoot = poiObject.GetComponent<RectTransform>();
            poiRoot.SetParent(mapImage.rectTransform, false);
            poiRoot.anchorMin = Vector2.zero;
            poiRoot.anchorMax = Vector2.one;
            poiRoot.offsetMin = Vector2.zero;
            poiRoot.offsetMax = Vector2.zero;

            ApplyVisible();
        }

        private void ApplyVisible()
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void RefreshPlayerMarker()
        {
            if (playerMarker == null || explorationMap == null || player == null || mapImage == null)
            {
                return;
            }

            if (mapImage.texture == null)
            {
                mapImage.texture = explorationMap.DisplayTexture;
            }

            Vector2 uv = explorationMap.WorldToNormalized(player.position);
            if ((uv - lastMarkerUv).sqrMagnitude < 0.0000001f &&
                Mathf.Abs(player.eulerAngles.y - lastMarkerYaw) < 0.2f)
            {
                return;
            }

            lastMarkerUv = uv;
            lastMarkerYaw = player.eulerAngles.y;
            Rect rect = mapImage.rectTransform.rect;
            playerMarker.anchoredPosition = new Vector2((uv.x - 0.5f) * rect.width, (uv.y - 0.5f) * rect.height);
            playerMarker.localEulerAngles = new Vector3(0f, 0f, -lastMarkerYaw);
        }

        private void RefreshPoiMarkers()
        {
            if (poiRoot == null || explorationMap == null || mapImage == null)
            {
                return;
            }

            IReadOnlyList<AutomapPoi> points = explorationMap.PointsOfInterest;
            EnsurePoiMarkerCount(points.Count);
            if (poiMarkers.Count != lastPoiCount)
            {
                lastPoiCount = poiMarkers.Count;
                playerMarker?.SetAsLastSibling();
            }

            Rect rect = mapImage.rectTransform.rect;

            for (int i = 0; i < poiMarkers.Count; i++)
            {
                RectTransform marker = poiMarkers[i];
                if (marker == null)
                {
                    continue;
                }

                if (i >= points.Count)
                {
                    marker.gameObject.SetActive(false);
                    continue;
                }

                AutomapPoi poi = points[i];
                bool show = explorationMap.IsExplored(poi.WorldPosition);
                marker.gameObject.SetActive(show);
                if (!show)
                {
                    continue;
                }

                Vector2 uv = explorationMap.WorldToNormalized(poi.WorldPosition);
                marker.anchoredPosition = new Vector2((uv.x - 0.5f) * rect.width, (uv.y - 0.5f) * rect.height);
            }
        }

        private void EnsurePoiMarkerCount(int count)
        {
            while (poiMarkers.Count < count)
            {
                AutomapPoi poi = explorationMap.PointsOfInterest[poiMarkers.Count];
                poiMarkers.Add(CreatePoiMarker(poi.Type));
            }
        }

        private RectTransform CreatePoiMarker(AutomapPoiType type)
        {
            bool isKey = type == AutomapPoiType.Key;
            Vector2 size = isKey ? new Vector2(16f, 16f) : new Vector2(20f, 20f);
            Color color = isKey
                ? new Color(1f, 0.82f, 0.18f, 1f)
                : new Color(0.22f, 0.32f, 0.62f, 1f);

            GameObject marker = CreateImage(
                isKey ? "KeyMarker" : "PrisonMarker",
                poiRoot,
                color,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                size);
            RectTransform rect = marker.GetComponent<RectTransform>();
            if (isKey)
            {
                rect.localEulerAngles = new Vector3(0f, 0f, 45f);
            }

            return rect;
        }

        private static bool WasTogglePressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                return true;
            }

            Gamepad gamepad = Gamepad.current;
            return gamepad != null && gamepad.selectButton.wasPressedThisFrame;
        }

        private static GameObject CreateImage(
            string objectName,
            Transform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 sizeOrOffsetMax)
        {
            bool stretch = anchorMin != anchorMax;
            GameObject created = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = created.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            if (stretch)
            {
                rect.offsetMin = offsetMin;
                rect.offsetMax = sizeOrOffsetMax;
            }
            else
            {
                rect.anchoredPosition = offsetMin;
                rect.sizeDelta = sizeOrOffsetMax;
            }

            Image image = created.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return created;
        }

        private static Sprite CreateTriangleSprite()
        {
            const int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 solid = new Color32(255, 255, 255, 255);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float t = y / (float)(size - 1);
                int half = Mathf.RoundToInt((1f - t) * (size * 0.48f));
                int mid = size / 2;
                for (int x = 0; x < size; x++)
                {
                    pixels[(y * size) + x] = x >= mid - half && x <= mid + half ? solid : clear;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.2f), 32f);
        }
    }
}
