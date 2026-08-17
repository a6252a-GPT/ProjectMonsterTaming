using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CastleRaidCameraController : MonoBehaviour // COC형 드래그·핀치 카메라
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField, Min(0.1f)] private float defaultOrthographicSize = 8.5f;
        [SerializeField, Min(0.1f)] private float minimumOrthographicSize = 5f;
        [SerializeField, Min(0.1f)] private float maximumOrthographicSize = 11.5f;
        [SerializeField] private Vector2 worldCenter = Vector2.zero;
        [SerializeField] private Vector2 worldSize = new Vector2(20f, 20f);
        [SerializeField, Min(0f)] private float dragThresholdPixels = 8f;
        [SerializeField, Min(0.01f)] private float wheelZoomStep = 0.18f;
        [SerializeField] private float groundPlaneY;

        private readonly Dictionary<int, Vector2> activePointers = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, Vector2> pointerStartPositions = new Dictionary<int, Vector2>();
        private readonly HashSet<int> suppressedClickPointers = new HashSet<int>();

        public float CurrentOrthographicSize => ResolveCamera() != null ? targetCamera.orthographicSize : 0f;
        public float MinimumOrthographicSize => minimumOrthographicSize;
        public float MaximumOrthographicSize => maximumOrthographicSize;

        public void ConfigureRuntimeBounds(
            Vector2 boundsCenter,
            Vector2 boundsSize,
            float defaultSize,
            float minimumSize,
            float maximumSize)
        {
            worldCenter = boundsCenter;
            worldSize = boundsSize;
            minimumOrthographicSize = minimumSize;
            maximumOrthographicSize = maximumSize;
            defaultOrthographicSize = defaultSize;
            NormalizeSettings();
            ResetView();
        }

        private void Awake()
        {
            ResetView();
        }

        public void BeginPointer(int pointerId, Vector2 screenPosition)
        {
            suppressedClickPointers.Remove(pointerId);
            activePointers[pointerId] = screenPosition;
            pointerStartPositions[pointerId] = screenPosition;

            if (activePointers.Count < 2)
            {
                return;
            }

            foreach (var activePointerId in activePointers.Keys)
            {
                suppressedClickPointers.Add(activePointerId);
            }
        }

        public void MovePointer(int pointerId, Vector2 screenPosition)
        {
            if (!activePointers.TryGetValue(pointerId, out var previousPosition) || ResolveCamera() == null)
            {
                return;
            }

            if (activePointers.Count >= 2 &&
                TryResolveOtherPointer(pointerId, out var otherPosition))
            {
                var previousDistance = Vector2.Distance(previousPosition, otherPosition);
                var currentDistance = Vector2.Distance(screenPosition, otherPosition);
                if (previousDistance > 1f && currentDistance > 1f)
                {
                    var anchor = (screenPosition + otherPosition) * 0.5f;
                    SetOrthographicSize(targetCamera.orthographicSize * previousDistance / currentDistance, anchor);
                }

                foreach (var activePointerId in activePointers.Keys)
                {
                    suppressedClickPointers.Add(activePointerId);
                }
            }
            else
            {
                if (!suppressedClickPointers.Contains(pointerId) &&
                    pointerStartPositions.TryGetValue(pointerId, out var startPosition) &&
                    Vector2.Distance(startPosition, screenPosition) >= dragThresholdPixels)
                {
                    suppressedClickPointers.Add(pointerId);
                }

                if (suppressedClickPointers.Contains(pointerId))
                {
                    Pan(previousPosition, screenPosition);
                }
            }

            activePointers[pointerId] = screenPosition;
        }

        public void EndPointer(int pointerId)
        {
            activePointers.Remove(pointerId);
            pointerStartPositions.Remove(pointerId);

            foreach (var pair in activePointers)
            {
                pointerStartPositions[pair.Key] = pair.Value;
            }
        }

        public bool ConsumeClickSuppression(int pointerId)
        {
            return suppressedClickPointers.Remove(pointerId);
        }

        public void ZoomByScroll(Vector2 screenPosition, float scrollDelta)
        {
            if (Mathf.Approximately(scrollDelta, 0f) || ResolveCamera() == null)
            {
                return;
            }

            var nextSize = targetCamera.orthographicSize * Mathf.Exp(-scrollDelta * wheelZoomStep);
            SetOrthographicSize(nextSize, screenPosition);
        }

        public void ResetView()
        {
            if (ResolveCamera() == null || !targetCamera.orthographic)
            {
                return;
            }

            NormalizeSettings();
            targetCamera.orthographicSize = defaultOrthographicSize;
            MoveGroundCenterTo(worldCenter);
            ClampToWorldBounds();
            CancelPointers();
        }

        public void CancelPointers()
        {
            activePointers.Clear();
            pointerStartPositions.Clear();
            suppressedClickPointers.Clear();
        }

        private void Pan(Vector2 previousScreenPosition, Vector2 currentScreenPosition)
        {
            if (!TryResolveGroundPoint(previousScreenPosition, out var previousWorldPosition) ||
                !TryResolveGroundPoint(currentScreenPosition, out var currentWorldPosition))
            {
                return;
            }

            var delta = previousWorldPosition - currentWorldPosition;
            targetCamera.transform.position += new Vector3(delta.x, 0f, delta.z);
            ClampToWorldBounds();
        }

        private void SetOrthographicSize(float size, Vector2 anchorScreenPosition)
        {
            NormalizeSettings();
            var hasAnchor = TryResolveGroundPoint(anchorScreenPosition, out var anchorBeforeZoom);
            targetCamera.orthographicSize = Mathf.Clamp(
                size,
                minimumOrthographicSize,
                maximumOrthographicSize);

            if (hasAnchor && TryResolveGroundPoint(anchorScreenPosition, out var anchorAfterZoom))
            {
                var delta = anchorBeforeZoom - anchorAfterZoom;
                targetCamera.transform.position += new Vector3(delta.x, 0f, delta.z);
            }

            ClampToWorldBounds();
        }

        private void ClampToWorldBounds()
        {
            if (!TryResolveViewportGroundBounds(out var groundCenter, out var groundHalfExtents))
            {
                return;
            }

            var worldHalfExtents = Vector2.Max(Vector2.zero, worldSize * 0.5f);
            var minimumX = worldCenter.x - worldHalfExtents.x + groundHalfExtents.x;
            var maximumX = worldCenter.x + worldHalfExtents.x - groundHalfExtents.x;
            var minimumZ = worldCenter.y - worldHalfExtents.y + groundHalfExtents.y;
            var maximumZ = worldCenter.y + worldHalfExtents.y - groundHalfExtents.y;

            var clampedX = minimumX <= maximumX
                ? Mathf.Clamp(groundCenter.x, minimumX, maximumX)
                : worldCenter.x;
            var clampedZ = minimumZ <= maximumZ
                ? Mathf.Clamp(groundCenter.y, minimumZ, maximumZ)
                : worldCenter.y;
            var correction = new Vector2(
                clampedX - groundCenter.x,
                clampedZ - groundCenter.y);
            if (correction.sqrMagnitude > 0.0001f)
            {
                targetCamera.transform.position += new Vector3(correction.x, 0f, correction.y);
            }
        }

        private bool TryResolveViewportGroundBounds(out Vector2 center, out Vector2 halfExtents)
        {
            center = default;
            halfExtents = default;
            var width = Mathf.Max(1, targetCamera.pixelWidth);
            var height = Mathf.Max(1, targetCamera.pixelHeight);
            if (!TryResolveGroundPoint(new Vector2(width * 0.5f, height * 0.5f), out var centerWorld))
            {
                return false;
            }

            var maximumX = 0f;
            var maximumZ = 0f;
            if (!TryExpandGroundExtents(new Vector2(0f, 0f), centerWorld, ref maximumX, ref maximumZ) ||
                !TryExpandGroundExtents(new Vector2(width, 0f), centerWorld, ref maximumX, ref maximumZ) ||
                !TryExpandGroundExtents(new Vector2(0f, height), centerWorld, ref maximumX, ref maximumZ) ||
                !TryExpandGroundExtents(new Vector2(width, height), centerWorld, ref maximumX, ref maximumZ))
            {
                return false;
            }

            center = new Vector2(centerWorld.x, centerWorld.z);
            halfExtents = new Vector2(maximumX, maximumZ);
            return true;
        }

        private bool TryExpandGroundExtents(
            Vector2 screenPosition,
            Vector3 centerWorld,
            ref float maximumX,
            ref float maximumZ)
        {
            if (!TryResolveGroundPoint(screenPosition, out var cornerWorld))
            {
                return false;
            }

            maximumX = Mathf.Max(maximumX, Mathf.Abs(cornerWorld.x - centerWorld.x));
            maximumZ = Mathf.Max(maximumZ, Mathf.Abs(cornerWorld.z - centerWorld.z));
            return true;
        }

        private void MoveGroundCenterTo(Vector2 destination)
        {
            var screenCenter = new Vector2(
                Mathf.Max(1, targetCamera.pixelWidth) * 0.5f,
                Mathf.Max(1, targetCamera.pixelHeight) * 0.5f);
            if (!TryResolveGroundPoint(screenCenter, out var currentCenter))
            {
                return;
            }

            var correction = new Vector2(
                destination.x - currentCenter.x,
                destination.y - currentCenter.z);
            if (correction.sqrMagnitude > 0.0001f)
            {
                targetCamera.transform.position += new Vector3(correction.x, 0f, correction.y);
            }
        }

        private bool TryResolveGroundPoint(Vector2 screenPosition, out Vector3 worldPosition)
        {
            worldPosition = default;
            var ray = targetCamera.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, groundPlaneY, 0f));
            if (!plane.Raycast(ray, out var distance))
            {
                return false;
            }

            worldPosition = ray.GetPoint(distance);
            return true;
        }

        private bool TryResolveOtherPointer(int pointerId, out Vector2 position)
        {
            foreach (var pair in activePointers)
            {
                if (pair.Key == pointerId)
                {
                    continue;
                }

                position = pair.Value;
                return true;
            }

            position = default;
            return false;
        }

        private Camera ResolveCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            return targetCamera;
        }

        private void NormalizeSettings()
        {
            minimumOrthographicSize = Mathf.Max(0.1f, minimumOrthographicSize);
            maximumOrthographicSize = Mathf.Max(minimumOrthographicSize, maximumOrthographicSize);
            defaultOrthographicSize = Mathf.Clamp(
                defaultOrthographicSize,
                minimumOrthographicSize,
                maximumOrthographicSize);
            worldSize = Vector2.Max(Vector2.one, worldSize);
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            Camera camera,
            float defaultSize,
            float minimumSize,
            float maximumSize,
            Vector2 boundsCenter,
            Vector2 boundsSize)
        {
            targetCamera = camera != null ? camera : GetComponent<Camera>();
            ConfigureRuntimeBounds(boundsCenter, boundsSize, defaultSize, minimumSize, maximumSize);
        }
#endif
    }
}
