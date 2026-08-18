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
        [SerializeField, Min(0.01f)] private float movementSmoothTime = 0.08f;
        [SerializeField, Min(0.01f)] private float zoomSmoothTime = 0.1f;
        [SerializeField, Min(0f)] private float inertiaDeceleration = 48f;
        [SerializeField, Min(0f)] private float maximumInertiaSpeed = 24f;
        [SerializeField] private float groundPlaneY;

        private readonly Dictionary<int, Vector2> activePointers = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, Vector2> pointerStartPositions = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, float> pointerMoveTimes = new Dictionary<int, float>();
        private readonly HashSet<int> suppressedClickPointers = new HashSet<int>();
        private Vector2 targetGroundCenter;
        private Vector2 smoothCenterVelocity;
        private Vector2 panInertiaVelocity;
        private Vector2 zoomAnchorWorld;
        private Vector2 zoomAnchorOffsetPerSize;
        private float targetOrthographicSize;
        private float smoothZoomVelocity;
        private bool zoomAnchorActive;
        private bool viewInitialized;

        public float CurrentOrthographicSize => ResolveCamera() != null ? targetCamera.orthographicSize : 0f;
        public float TargetOrthographicSize => targetOrthographicSize;
        public Vector2 TargetGroundCenter => targetGroundCenter;
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

        private void LateUpdate()
        {
            TickCamera(Time.unscaledDeltaTime);
        }

        public void BeginPointer(int pointerId, Vector2 screenPosition)
        {
            EnsureViewInitialized();
            if (activePointers.Count == 0)
            {
                SyncTargetsFromCamera();
            }

            panInertiaVelocity = Vector2.zero;
            zoomAnchorActive = false;
            suppressedClickPointers.Remove(pointerId);
            activePointers[pointerId] = screenPosition;
            pointerStartPositions[pointerId] = screenPosition;
            pointerMoveTimes[pointerId] = Time.unscaledTime;

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
                var previousAnchor = (previousPosition + otherPosition) * 0.5f;
                var currentAnchor = (screenPosition + otherPosition) * 0.5f;
                if (previousDistance > 1f && currentDistance > 1f)
                {
                    var nextSize = targetOrthographicSize * previousDistance / currentDistance;
                    if (TryResolveGroundPoint(previousAnchor, out var gestureAnchorWorld))
                    {
                        SetTargetOrthographicSize(nextSize, currentAnchor, gestureAnchorWorld);
                    }
                    else
                    {
                        SetTargetOrthographicSize(nextSize, currentAnchor);
                    }
                }
                else
                {
                    PanTarget(previousAnchor, currentAnchor, out _);
                }

                panInertiaVelocity = Vector2.zero;
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
                    if (PanTarget(previousPosition, screenPosition, out var worldDelta))
                    {
                        var now = Time.unscaledTime;
                        var elapsed = pointerMoveTimes.TryGetValue(pointerId, out var previousTime)
                            ? Mathf.Max(1f / 120f, now - previousTime)
                            : 1f / 60f;
                        panInertiaVelocity = Vector2.ClampMagnitude(
                            worldDelta / elapsed,
                            maximumInertiaSpeed);
                    }
                }
            }

            activePointers[pointerId] = screenPosition;
            pointerMoveTimes[pointerId] = Time.unscaledTime;
        }

        public void EndPointer(int pointerId)
        {
            var wasMultiTouch = activePointers.Count >= 2;
            activePointers.Remove(pointerId);
            pointerStartPositions.Remove(pointerId);
            pointerMoveTimes.Remove(pointerId);

            foreach (var pair in activePointers)
            {
                pointerStartPositions[pair.Key] = pair.Value;
                pointerMoveTimes[pair.Key] = Time.unscaledTime;
            }

            if (wasMultiTouch || activePointers.Count > 0)
            {
                panInertiaVelocity = Vector2.zero;
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

            EnsureViewInitialized();
            panInertiaVelocity = Vector2.zero;
            var nextSize = targetOrthographicSize * Mathf.Exp(-scrollDelta * wheelZoomStep);
            SetTargetOrthographicSize(nextSize, screenPosition);
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
            targetOrthographicSize = targetCamera.orthographicSize;
            targetGroundCenter = TryResolveGroundCenter(out var center) ? center : worldCenter;
            smoothCenterVelocity = Vector2.zero;
            smoothZoomVelocity = 0f;
            panInertiaVelocity = Vector2.zero;
            zoomAnchorActive = false;
            viewInitialized = true;
            CancelPointers();
        }

        public void CancelPointers()
        {
            activePointers.Clear();
            pointerStartPositions.Clear();
            pointerMoveTimes.Clear();
            suppressedClickPointers.Clear();
            panInertiaVelocity = Vector2.zero;
            zoomAnchorActive = false;
        }

        private bool PanTarget(
            Vector2 previousScreenPosition,
            Vector2 currentScreenPosition,
            out Vector2 worldDelta)
        {
            worldDelta = default;
            if (!TryResolveGroundPoint(previousScreenPosition, out var previousWorldPosition) ||
                !TryResolveGroundPoint(currentScreenPosition, out var currentWorldPosition))
            {
                return false;
            }

            var delta = previousWorldPosition - currentWorldPosition;
            worldDelta = new Vector2(delta.x, delta.z);
            zoomAnchorActive = false;
            targetGroundCenter = ClampGroundCenter(
                targetGroundCenter + worldDelta,
                targetOrthographicSize);
            return true;
        }

        private void SetTargetOrthographicSize(
            float size,
            Vector2 anchorScreenPosition,
            Vector3? fixedWorldAnchor = null)
        {
            NormalizeSettings();
            EnsureViewInitialized();
            var clampedSize = Mathf.Clamp(
                size,
                minimumOrthographicSize,
                maximumOrthographicSize);
            if (TryResolveGroundCenter(out var currentCenter) &&
                TryResolveGroundPoint(anchorScreenPosition, out var screenAnchorWorld))
            {
                var currentSize = Mathf.Max(0.01f, targetCamera.orthographicSize);
                zoomAnchorWorld = fixedWorldAnchor.HasValue
                    ? new Vector2(fixedWorldAnchor.Value.x, fixedWorldAnchor.Value.z)
                    : new Vector2(screenAnchorWorld.x, screenAnchorWorld.z);
                zoomAnchorOffsetPerSize = new Vector2(
                    screenAnchorWorld.x - currentCenter.x,
                    screenAnchorWorld.z - currentCenter.y) / currentSize;
                targetGroundCenter = zoomAnchorWorld - zoomAnchorOffsetPerSize * clampedSize;
                zoomAnchorActive = true;
            }
            else
            {
                zoomAnchorActive = false;
            }

            targetOrthographicSize = clampedSize;
            targetGroundCenter = ClampGroundCenter(targetGroundCenter, targetOrthographicSize);
        }

        private void TickCamera(float deltaTime)
        {
            if (ResolveCamera() == null || !targetCamera.orthographic || deltaTime <= 0f)
            {
                return;
            }

            EnsureViewInitialized();
            var step = Mathf.Min(0.05f, deltaTime);
            if (activePointers.Count == 0 && panInertiaVelocity.sqrMagnitude > 0.0001f)
            {
                var requestedCenter = targetGroundCenter + panInertiaVelocity * step;
                var clampedCenter = ClampGroundCenter(requestedCenter, targetOrthographicSize);
                if (!Mathf.Approximately(requestedCenter.x, clampedCenter.x))
                {
                    panInertiaVelocity.x = 0f;
                }

                if (!Mathf.Approximately(requestedCenter.y, clampedCenter.y))
                {
                    panInertiaVelocity.y = 0f;
                }

                targetGroundCenter = clampedCenter;
                panInertiaVelocity = Vector2.MoveTowards(
                    panInertiaVelocity,
                    Vector2.zero,
                    inertiaDeceleration * step);
            }

            targetOrthographicSize = Mathf.Clamp(
                targetOrthographicSize,
                minimumOrthographicSize,
                maximumOrthographicSize);
            targetGroundCenter = ClampGroundCenter(targetGroundCenter, targetOrthographicSize);

            var nextSize = Mathf.SmoothDamp(
                targetCamera.orthographicSize,
                targetOrthographicSize,
                ref smoothZoomVelocity,
                zoomSmoothTime,
                Mathf.Infinity,
                step);
            Vector2 nextCenter;
            if (zoomAnchorActive)
            {
                nextCenter = ClampGroundCenter(
                    zoomAnchorWorld - zoomAnchorOffsetPerSize * nextSize,
                    nextSize);
                smoothCenterVelocity = Vector2.zero;
            }
            else
            {
                var currentCenter = TryResolveGroundCenter(out var resolvedCenter)
                    ? resolvedCenter
                    : targetGroundCenter;
                nextCenter = Vector2.SmoothDamp(
                    currentCenter,
                    targetGroundCenter,
                    ref smoothCenterVelocity,
                    movementSmoothTime,
                    Mathf.Infinity,
                    step);
            }

            targetCamera.orthographicSize = Mathf.Clamp(
                nextSize,
                minimumOrthographicSize,
                maximumOrthographicSize);
            MoveGroundCenterTo(nextCenter);
            ClampToWorldBounds();

            if (zoomAnchorActive && Mathf.Abs(targetCamera.orthographicSize - targetOrthographicSize) < 0.0001f)
            {
                targetGroundCenter = ClampGroundCenter(
                    zoomAnchorWorld - zoomAnchorOffsetPerSize * targetOrthographicSize,
                    targetOrthographicSize);
                zoomAnchorActive = false;
            }
        }

        private void EnsureViewInitialized()
        {
            if (viewInitialized || ResolveCamera() == null)
            {
                return;
            }

            SyncTargetsFromCamera();
            viewInitialized = true;
        }

        private void SyncTargetsFromCamera()
        {
            NormalizeSettings();
            targetOrthographicSize = Mathf.Clamp(
                targetCamera.orthographicSize,
                minimumOrthographicSize,
                maximumOrthographicSize);
            targetGroundCenter = TryResolveGroundCenter(out var center)
                ? ClampGroundCenter(center, targetOrthographicSize)
                : worldCenter;
            smoothCenterVelocity = Vector2.zero;
            smoothZoomVelocity = 0f;
            panInertiaVelocity = Vector2.zero;
            zoomAnchorActive = false;
        }

        private Vector2 ClampGroundCenter(Vector2 center, float orthographicSize)
        {
            if (!TryResolveViewportGroundBounds(out _, out var currentHalfExtents))
            {
                return center;
            }

            var currentSize = Mathf.Max(0.01f, targetCamera.orthographicSize);
            var groundHalfExtents = currentHalfExtents * (orthographicSize / currentSize);
            var worldHalfExtents = Vector2.Max(Vector2.zero, worldSize * 0.5f);
            var minimumX = worldCenter.x - worldHalfExtents.x + groundHalfExtents.x;
            var maximumX = worldCenter.x + worldHalfExtents.x - groundHalfExtents.x;
            var minimumZ = worldCenter.y - worldHalfExtents.y + groundHalfExtents.y;
            var maximumZ = worldCenter.y + worldHalfExtents.y - groundHalfExtents.y;
            return new Vector2(
                minimumX <= maximumX ? Mathf.Clamp(center.x, minimumX, maximumX) : worldCenter.x,
                minimumZ <= maximumZ ? Mathf.Clamp(center.y, minimumZ, maximumZ) : worldCenter.y);
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

        private bool TryResolveGroundCenter(out Vector2 center)
        {
            var screenCenter = new Vector2(
                Mathf.Max(1, targetCamera.pixelWidth) * 0.5f,
                Mathf.Max(1, targetCamera.pixelHeight) * 0.5f);
            if (TryResolveGroundPoint(screenCenter, out var worldPosition))
            {
                center = new Vector2(worldPosition.x, worldPosition.z);
                return true;
            }

            center = default;
            return false;
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
            movementSmoothTime = Mathf.Max(0.01f, movementSmoothTime);
            zoomSmoothTime = Mathf.Max(0.01f, zoomSmoothTime);
            inertiaDeceleration = Mathf.Max(0f, inertiaDeceleration);
            maximumInertiaSpeed = Mathf.Max(0f, maximumInertiaSpeed);
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

        public void EditorStep(float deltaTime)
        {
            TickCamera(deltaTime);
        }
#endif
    }
}
