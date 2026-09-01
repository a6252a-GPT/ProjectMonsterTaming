using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class HexCastleCameraController : MonoBehaviour // 육각 성 크기에 맞춰지는 Perspective 이동 카메라
    {
        private const int MousePointerId = -1;
        private const float ScrollUnitsPerNotch = 120f;
        private const float DefaultStructureHeight = 4.8f;
        private const float ShadowDistancePadding = 12f;

        [Header("투영")]
        [SerializeField] private Camera targetCamera;
        [SerializeField, Range(20f, 70f)] private float fieldOfView = 32f;
        [SerializeField, Range(20f, 70f)] private float tiltDegrees = 38f;
        [SerializeField, Range(1.01f, 1.3f)] private float fitPadding = 1.08f;
        [SerializeField, Range(0.6f, 1f)] private float initialZoomRatio = 0.70f;
        [SerializeField, Range(-0.2f, 0.2f)] private float verticalScreenOffset = 0.10f;
        [SerializeField, Range(0.25f, 0.85f)] private float minimumZoomRatio = 0.48f;
        [SerializeField, Range(1.05f, 2.5f)] private float maximumZoomRatio = 1.5f;
        [SerializeField, Range(2, 20)] private int defaultBattlefieldRadius = 10;
        [SerializeField] private float groundPlaneY;

        [Header("조작")]
        [SerializeField, Min(0f)] private float dragThresholdPixels = 8f;
        [SerializeField, Min(0.01f)] private float wheelZoomStep = 0.18f;
        [SerializeField, Range(10f, 180f)] private float rotationSpeedDegrees = 90f;
        [SerializeField, Range(0.05f, 0.5f)] private float rotationCenteringDuration = 0.22f;
        [SerializeField, Min(0f)] private float minimumPanRange = 6f;
        [SerializeField, Min(0f)] private float extraPanRange = 3.5f;
        [SerializeField, Min(0.01f)] private float movementSmoothTime = 0.08f;
        [SerializeField, Min(0.01f)] private float zoomSmoothTime = 0.10f;
        [SerializeField, Min(0f)] private float inertiaDeceleration = 48f;
        [SerializeField, Min(0f)] private float maximumInertiaSpeed = 24f;

        private readonly Dictionary<int, Vector2> activePointers = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, Vector2> pointerStartPositions = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, float> pointerMoveTimes = new Dictionary<int, float>();
        private readonly HashSet<int> suppressedClickPointers = new HashSet<int>();
        private Vector2 worldCenter;
        private Vector2 worldSize;
        private Vector2 defaultViewportHalfExtents;
        private Vector2 targetGroundCenter;
        private Vector2 rotationFocusGroundCenter;
        private Vector2 rotationCenteringStartGroundCenter;
        private Vector2 rotationPivotGroundCenter;
        private Vector2 smoothCenterVelocity;
        private Vector2 panInertiaVelocity;
        private Vector2 zoomAnchorWorld;
        private Vector2 zoomAnchorOffsetPerDistance;
        private float defaultDistance;
        private float minimumDistance;
        private float maximumDistance;
        private float targetDistance;
        private float smoothDistanceVelocity;
        private float yawDegrees;
        private float rotationInput;
        private float rotationPivotDistance;
        private float rotationCenteringElapsed;
        private float requiredShadowDistance;
        private float shadowDistanceBeforeOverride;
        private UniversalRenderPipelineAsset shadowDistanceOverrideAsset;
        private int shadowDistanceOverrideDepth;
        private bool zoomAnchorActive;
        private bool rotationCentering;
        private bool externalPointerInput;
        private bool viewInitialized;
        private bool boundsConfigured;

        public float CurrentDistance => ResolveCurrentDistance();
        public float TargetDistance => targetDistance;
        public float DefaultDistance => defaultDistance;
        public float MinimumDistance => minimumDistance;
        public float MaximumDistance => maximumDistance;
        public float InitialZoomRatio => initialZoomRatio;
        public float VerticalScreenOffset => verticalScreenOffset;
        public float YawDegrees => yawDegrees;
        public float RotationSpeedDegrees => rotationSpeedDegrees;
        public float RotationCenteringDuration => rotationCenteringDuration;
        public float MinimumPanRange => minimumPanRange;
        public float ExtraPanRange => extraPanRange;
        public float RequiredShadowDistance => requiredShadowDistance;
        public Vector2 TargetGroundCenter => targetGroundCenter;
        public Vector2 RotationFocusGroundCenter => rotationFocusGroundCenter;
        public Vector2 WorldSize => worldSize;
        public bool IsRotationCentering => rotationCentering;
        public bool UsesExternalPointerInput => externalPointerInput;
        public bool IsPerspective => ResolveCamera() != null && !targetCamera.orthographic;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;
            Initialize();
        }

        private void Update()
        {
            if (!externalPointerInput)
            {
                HandleDirectInput();
            }
        }

        private void LateUpdate()
        {
            TickRotation(Time.unscaledDeltaTime);
            TickCamera(Time.unscaledDeltaTime);
        }

        private void OnValidate()
        {
            NormalizeSettings();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
            RestoreShadowDistance();
            if (targetCamera != null)
            {
                targetCamera.ResetProjectionMatrix();
            }
        }

        public void ConfigureBounds(int battlefieldRadius, float cellSize)
        {
            ConfigureBounds(ResolveBoardBounds(
                Mathf.Max(1, battlefieldRadius),
                Mathf.Max(0.1f, cellSize)));
        }

        public void ConfigureBounds(Bounds bounds)
        {
            if (bounds.size.x <= 0.001f || bounds.size.z <= 0.001f)
            {
                return;
            }

            InitializeCameraOnly();
            NormalizeSettings();
            ApplyProjectionAndRotation();
            worldCenter = new Vector2(bounds.center.x, bounds.center.z);
            rotationFocusGroundCenter = worldCenter;
            worldSize = new Vector2(bounds.size.x, bounds.size.z);
            var fitDistance = ResolveFitDistance(
                targetCamera,
                bounds,
                worldCenter,
                groundPlaneY,
                tiltDegrees,
                fitPadding);
            defaultDistance = Mathf.Max(1f, fitDistance * initialZoomRatio);
            minimumDistance = Mathf.Max(3.5f, defaultDistance * minimumZoomRatio);
            maximumDistance = Mathf.Max(defaultDistance, defaultDistance * maximumZoomRatio);
            requiredShadowDistance = Mathf.Min(
                targetCamera.farClipPlane,
                ResolveRequiredShadowDistance(maximumDistance, bounds.extents));
            boundsConfigured = true;
            defaultViewportHalfExtents = Vector2.zero;
            ResetView();
            if (TryResolveViewportGroundBounds(out _, out var halfExtents))
            {
                defaultViewportHalfExtents = halfExtents;
            }
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
            if (activePointers.Count >= 2)
            {
                foreach (var activePointerId in activePointers.Keys)
                {
                    suppressedClickPointers.Add(activePointerId);
                }
            }
        }

        public void MovePointer(int pointerId, Vector2 screenPosition)
        {
            if (!activePointers.TryGetValue(pointerId, out var previousPosition) || ResolveCamera() == null)
            {
                return;
            }

            if (activePointers.Count >= 2 && TryResolveOtherPointer(pointerId, out var otherPosition))
            {
                var previousDistance = Vector2.Distance(previousPosition, otherPosition);
                var currentDistance = Vector2.Distance(screenPosition, otherPosition);
                var previousAnchor = (previousPosition + otherPosition) * 0.5f;
                var currentAnchor = (screenPosition + otherPosition) * 0.5f;
                if (previousDistance > 1f && currentDistance > 1f)
                {
                    var nextDistance = targetDistance * previousDistance / currentDistance;
                    if (TryResolveGroundPoint(previousAnchor, out var gestureAnchorWorld))
                    {
                        SetTargetDistance(nextDistance, currentAnchor, gestureAnchorWorld);
                    }
                    else
                    {
                        SetTargetDistance(nextDistance, currentAnchor);
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

                if (suppressedClickPointers.Contains(pointerId) &&
                    PanTarget(previousPosition, screenPosition, out var worldDelta))
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
            var normalizedScrollDelta = Mathf.Abs(scrollDelta) > 1f
                ? scrollDelta / ScrollUnitsPerNotch
                : scrollDelta;
            SetTargetDistance(
                targetDistance * Mathf.Exp(-normalizedScrollDelta * wheelZoomStep),
                screenPosition);
        }

        public void BeginRotateLeft()
        {
            BeginRotation(-1f);
        }

        public void BeginRotateRight()
        {
            BeginRotation(1f);
        }

        public void StopRotation()
        {
            if (!Mathf.Approximately(rotationInput, 0f))
            {
                targetGroundCenter = rotationPivotGroundCenter;
                targetDistance = rotationPivotDistance;
                ApplyViewImmediate(rotationPivotGroundCenter, rotationPivotDistance);
            }

            rotationInput = 0f;
            rotationCentering = false;
            rotationCenteringElapsed = 0f;
        }

        public void SetRotationFocus(Vector3 worldPosition)
        {
            rotationFocusGroundCenter = new Vector2(worldPosition.x, worldPosition.z);
        }

        public void SetExternalPointerInput(bool enabled)
        {
            if (externalPointerInput == enabled)
            {
                return;
            }

            externalPointerInput = enabled;
            CancelPointers();
        }

        [ContextMenu("육각 성 전체 보기")]
        public void ResetView()
        {
            if (ResolveCamera() == null)
            {
                return;
            }

            if (!boundsConfigured)
            {
                ConfigureBounds(defaultBattlefieldRadius, HexSpatialContract.CellOuterRadius);
                return;
            }

            NormalizeSettings();
            yawDegrees = 0f;
            ApplyProjectionAndRotation();
            targetGroundCenter = worldCenter;
            targetDistance = defaultDistance;
            ApplyViewImmediate(targetGroundCenter, targetDistance);
            smoothCenterVelocity = Vector2.zero;
            smoothDistanceVelocity = 0f;
            panInertiaVelocity = Vector2.zero;
            zoomAnchorActive = false;
            rotationInput = 0f;
            rotationCentering = false;
            rotationCenteringElapsed = 0f;
            rotationCenteringStartGroundCenter = targetGroundCenter;
            rotationPivotGroundCenter = targetGroundCenter;
            rotationPivotDistance = targetDistance;
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

        private void Initialize()
        {
            InitializeCameraOnly();
            if (targetCamera == null)
            {
                return;
            }

            if (!boundsConfigured)
            {
                ConfigureBounds(defaultBattlefieldRadius, HexSpatialContract.CellOuterRadius);
            }
            else
            {
                ApplyProjectionAndRotation();
                SyncTargetsFromCamera();
            }
        }

        private void InitializeCameraOnly()
        {
            targetCamera ??= GetComponent<Camera>();
        }

        private void HandleDirectInput()
        {
            var touchscreen = Touchscreen.current;
            var hasActiveTouch = false;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    var pointerId = touch.touchId.ReadValue();
                    var pressed = touch.press.isPressed;
                    hasActiveTouch |= pressed;
                    if (touch.press.wasPressedThisFrame)
                    {
                        BeginPointer(pointerId, touch.position.ReadValue());
                    }
                    else if (pressed && !activePointers.ContainsKey(pointerId))
                    {
                        BeginPointer(pointerId, touch.position.ReadValue());
                    }

                    if (pressed && activePointers.ContainsKey(pointerId))
                    {
                        MovePointer(pointerId, touch.position.ReadValue());
                    }

                    if (touch.press.wasReleasedThisFrame)
                    {
                        EndPointer(pointerId);
                    }
                }
            }

            var mouse = Mouse.current;
            if (mouse == null || hasActiveTouch)
            {
                if (hasActiveTouch && activePointers.ContainsKey(MousePointerId))
                {
                    EndPointer(MousePointerId);
                }

                return;
            }

            var pointerPosition = mouse.position.ReadValue();
            var wheel = mouse.scroll.ReadValue().y / 120f;
            if (Mathf.Abs(wheel) > 0.001f)
            {
                ZoomByScroll(pointerPosition, wheel);
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                BeginPointer(MousePointerId, pointerPosition);
            }

            if (mouse.leftButton.isPressed && activePointers.ContainsKey(MousePointerId))
            {
                MovePointer(MousePointerId, pointerPosition);
            }

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                EndPointer(MousePointerId);
            }
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
                targetDistance);
            return true;
        }

        private void SetTargetDistance(
            float distance,
            Vector2 anchorScreenPosition,
            Vector3? fixedWorldAnchor = null)
        {
            NormalizeSettings();
            EnsureViewInitialized();
            var clampedDistance = Mathf.Clamp(distance, minimumDistance, maximumDistance);
            if (TryResolveGroundCenter(out var currentCenter) &&
                TryResolveGroundPoint(anchorScreenPosition, out var screenAnchorWorld))
            {
                var currentDistance = Mathf.Max(0.01f, ResolveCurrentDistance());
                zoomAnchorWorld = fixedWorldAnchor.HasValue
                    ? new Vector2(fixedWorldAnchor.Value.x, fixedWorldAnchor.Value.z)
                    : new Vector2(screenAnchorWorld.x, screenAnchorWorld.z);
                zoomAnchorOffsetPerDistance = new Vector2(
                    screenAnchorWorld.x - currentCenter.x,
                    screenAnchorWorld.z - currentCenter.y) / currentDistance;
                targetGroundCenter = zoomAnchorWorld - zoomAnchorOffsetPerDistance * clampedDistance;
                zoomAnchorActive = true;
            }
            else
            {
                zoomAnchorActive = false;
            }

            targetDistance = clampedDistance;
            targetGroundCenter = ClampGroundCenter(targetGroundCenter, targetDistance);
        }

        private void TickCamera(float deltaTime)
        {
            if (ResolveCamera() == null || targetCamera.orthographic || deltaTime <= 0f)
            {
                return;
            }

            EnsureViewInitialized();
            if (!Mathf.Approximately(rotationInput, 0f))
            {
                return;
            }

            var step = Mathf.Min(0.05f, deltaTime);
            if (activePointers.Count == 0 && panInertiaVelocity.sqrMagnitude > 0.0001f)
            {
                var requestedCenter = targetGroundCenter + panInertiaVelocity * step;
                var clampedCenter = ClampGroundCenter(requestedCenter, targetDistance);
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

            targetDistance = Mathf.Clamp(targetDistance, minimumDistance, maximumDistance);
            targetGroundCenter = ClampGroundCenter(targetGroundCenter, targetDistance);
            var nextDistance = Mathf.SmoothDamp(
                ResolveCurrentDistance(),
                targetDistance,
                ref smoothDistanceVelocity,
                zoomSmoothTime,
                Mathf.Infinity,
                step);
            Vector2 nextCenter;
            if (zoomAnchorActive)
            {
                nextCenter = ClampGroundCenter(
                    zoomAnchorWorld - zoomAnchorOffsetPerDistance * nextDistance,
                    nextDistance);
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

            ApplyViewImmediate(nextCenter, nextDistance);
            if (zoomAnchorActive && Mathf.Abs(nextDistance - targetDistance) < 0.0001f)
            {
                targetGroundCenter = ClampGroundCenter(
                    zoomAnchorWorld - zoomAnchorOffsetPerDistance * targetDistance,
                    targetDistance);
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
            ApplyProjectionAndRotation();
            targetDistance = Mathf.Clamp(
                ResolveCurrentDistance(),
                minimumDistance,
                maximumDistance);
            targetGroundCenter = TryResolveGroundCenter(out var center)
                ? ClampGroundCenter(center, targetDistance)
                : worldCenter;
            smoothCenterVelocity = Vector2.zero;
            smoothDistanceVelocity = 0f;
            panInertiaVelocity = Vector2.zero;
            zoomAnchorActive = false;
            viewInitialized = true;
        }

        private Vector2 ClampGroundCenter(Vector2 center, float distance)
        {
            var referenceHalfExtents = defaultViewportHalfExtents;
            if (referenceHalfExtents.sqrMagnitude <= 0.0001f &&
                !TryResolveViewportGroundBounds(out _, out referenceHalfExtents))
            {
                return center;
            }

            var referenceDistance = Mathf.Max(0.01f, defaultDistance);
            var groundHalfExtents = referenceHalfExtents * (distance / referenceDistance);
            var worldHalfExtents = Vector2.Max(Vector2.zero, worldSize * 0.5f);
            var panRangeX = Mathf.Max(
                minimumPanRange,
                worldHalfExtents.x - groundHalfExtents.x + extraPanRange);
            var panRangeZ = Mathf.Max(
                minimumPanRange,
                worldHalfExtents.y - groundHalfExtents.y + extraPanRange);
            return new Vector2(
                Mathf.Clamp(center.x, worldCenter.x - panRangeX, worldCenter.x + panRangeX),
                Mathf.Clamp(center.y, worldCenter.y - panRangeZ, worldCenter.y + panRangeZ));
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

        private bool TryResolveGroundCenter(out Vector2 center)
        {
            if (TryResolveGroundPoint(ResolveFocusScreenPosition(), out var worldPosition))
            {
                center = new Vector2(worldPosition.x, worldPosition.z);
                return true;
            }

            center = default;
            return false;
        }

        private Vector2 ResolveFocusScreenPosition()
        {
            return new Vector2(
                Mathf.Max(1, targetCamera.pixelWidth) * 0.5f,
                Mathf.Max(1, targetCamera.pixelHeight) * (0.5f + verticalScreenOffset));
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

        private float ResolveCurrentDistance()
        {
            if (targetCamera == null)
            {
                return defaultDistance;
            }

            var plane = new Plane(Vector3.up, new Vector3(0f, groundPlaneY, 0f));
            var ray = new Ray(targetCamera.transform.position, targetCamera.transform.forward);
            return plane.Raycast(ray, out var distance)
                ? Mathf.Max(0.01f, distance)
                : Mathf.Max(0.01f, defaultDistance);
        }

        private void ApplyProjectionAndRotation()
        {
            targetCamera.ResetProjectionMatrix();
            targetCamera.orthographic = false;
            targetCamera.fieldOfView = fieldOfView;
            var projection = targetCamera.projectionMatrix;
            projection.m12 = -2f * verticalScreenOffset;
            targetCamera.projectionMatrix = projection;
            targetCamera.transform.rotation = Quaternion.Euler(tiltDegrees, yawDegrees, 0f);
        }

        private void BeginRotation(float direction)
        {
            if (ResolveCamera() == null)
            {
                return;
            }

            EnsureViewInitialized();
            rotationPivotGroundCenter = TryResolveGroundCenter(out var resolvedCenter)
                ? resolvedCenter
                : targetGroundCenter;
            rotationCenteringStartGroundCenter = rotationPivotGroundCenter;
            rotationPivotDistance = Mathf.Clamp(
                ResolveCurrentDistance(),
                minimumDistance,
                maximumDistance);
            rotationCenteringElapsed = 0f;
            rotationCentering = Vector2.Distance(
                rotationPivotGroundCenter,
                rotationFocusGroundCenter) > 0.001f;
            panInertiaVelocity = Vector2.zero;
            zoomAnchorActive = false;
            smoothCenterVelocity = Vector2.zero;
            smoothDistanceVelocity = 0f;
            targetGroundCenter = rotationPivotGroundCenter;
            targetDistance = rotationPivotDistance;
            rotationInput = Mathf.Sign(direction);
        }

        private void TickRotation(float deltaTime)
        {
            if (Mathf.Approximately(rotationInput, 0f) || deltaTime <= 0f)
            {
                return;
            }

            yawDegrees = Mathf.Repeat(
                yawDegrees + rotationInput * rotationSpeedDegrees * deltaTime + 180f,
                360f) - 180f;
            if (rotationCentering)
            {
                var centeringStep = Mathf.Min(
                    deltaTime,
                    rotationCenteringDuration - rotationCenteringElapsed);
                rotationCenteringElapsed += centeringStep;
                var progress = Mathf.Clamp01(rotationCenteringElapsed / rotationCenteringDuration);
                rotationPivotGroundCenter = Vector2.Lerp(
                    rotationCenteringStartGroundCenter,
                    rotationFocusGroundCenter,
                    Mathf.SmoothStep(0f, 1f, progress));
                ApplyViewImmediate(rotationPivotGroundCenter, rotationPivotDistance);
                if (progress < 1f)
                {
                    return;
                }

                rotationPivotGroundCenter = rotationFocusGroundCenter;
                targetGroundCenter = rotationFocusGroundCenter;
                rotationCentering = false;
                ApplyViewImmediate(rotationPivotGroundCenter, rotationPivotDistance);
            }

            ApplyViewImmediate(rotationPivotGroundCenter, rotationPivotDistance);
        }

        private void ApplyViewImmediate(Vector2 center, float distance)
        {
            ApplyProjectionAndRotation();
            var focus = new Vector3(center.x, groundPlaneY, center.y);
            targetCamera.transform.position =
                focus - targetCamera.transform.forward * Mathf.Max(0.01f, distance);
        }

        private void HandleBeginCameraRendering(ScriptableRenderContext _, Camera renderingCamera)
        {
            if (renderingCamera != targetCamera || requiredShadowDistance <= 0f)
            {
                return;
            }

            if (shadowDistanceOverrideDepth > 0)
            {
                shadowDistanceOverrideDepth++;
                return;
            }

            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipelineAsset) ||
                pipelineAsset.shadowDistance >= requiredShadowDistance)
            {
                return;
            }

            shadowDistanceOverrideAsset = pipelineAsset;
            shadowDistanceBeforeOverride = pipelineAsset.shadowDistance;
            shadowDistanceOverrideDepth = 1;
            pipelineAsset.shadowDistance = requiredShadowDistance;
        }

        private void HandleEndCameraRendering(ScriptableRenderContext _, Camera renderingCamera)
        {
            if (renderingCamera != targetCamera || shadowDistanceOverrideDepth <= 0)
            {
                return;
            }

            shadowDistanceOverrideDepth--;
            if (shadowDistanceOverrideDepth == 0)
            {
                RestoreShadowDistance();
            }
        }

        private void RestoreShadowDistance()
        {
            shadowDistanceOverrideDepth = 0;
            if (shadowDistanceOverrideAsset != null)
            {
                shadowDistanceOverrideAsset.shadowDistance = shadowDistanceBeforeOverride;
            }

            shadowDistanceOverrideAsset = null;
            shadowDistanceBeforeOverride = 0f;
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
            fieldOfView = Mathf.Clamp(fieldOfView, 20f, 70f);
            tiltDegrees = Mathf.Clamp(tiltDegrees, 20f, 70f);
            fitPadding = Mathf.Clamp(fitPadding, 1.01f, 1.3f);
            initialZoomRatio = Mathf.Clamp(initialZoomRatio, 0.6f, 1f);
            verticalScreenOffset = Mathf.Clamp(verticalScreenOffset, -0.2f, 0.2f);
            minimumZoomRatio = Mathf.Clamp(minimumZoomRatio, 0.25f, 0.85f);
            maximumZoomRatio = Mathf.Max(1.05f, maximumZoomRatio);
            defaultBattlefieldRadius = Mathf.Clamp(defaultBattlefieldRadius, 2, 20);
            dragThresholdPixels = Mathf.Max(0f, dragThresholdPixels);
            wheelZoomStep = Mathf.Max(0.01f, wheelZoomStep);
            rotationSpeedDegrees = Mathf.Clamp(rotationSpeedDegrees, 10f, 180f);
            rotationCenteringDuration = Mathf.Clamp(rotationCenteringDuration, 0.05f, 0.5f);
            minimumPanRange = Mathf.Max(0f, minimumPanRange);
            extraPanRange = Mathf.Max(0f, extraPanRange);
            movementSmoothTime = Mathf.Max(0.01f, movementSmoothTime);
            zoomSmoothTime = Mathf.Max(0.01f, zoomSmoothTime);
            inertiaDeceleration = Mathf.Max(0f, inertiaDeceleration);
            maximumInertiaSpeed = Mathf.Max(0f, maximumInertiaSpeed);
        }

        private static Bounds ResolveBoardBounds(int battlefieldRadius, float cellSize)
        {
            var initialized = false;
            var bounds = default(Bounds);
            foreach (var coordinates in HexCoordinates.EnumerateRadius(battlefieldRadius))
            {
                var center = coordinates.ToWorld(cellSize);
                for (var index = 0; index < 6; index++)
                {
                    var angle = Mathf.Deg2Rad * (30f + index * 60f);
                    var corner = center + new Vector3(
                        Mathf.Cos(angle) * cellSize,
                        0f,
                        Mathf.Sin(angle) * cellSize);
                    if (!initialized)
                    {
                        bounds = new Bounds(corner, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(corner);
                    }

                    bounds.Encapsulate(corner + Vector3.up * DefaultStructureHeight);
                }
            }

            return bounds;
        }

        public static float ResolveFitDistance(
            Camera camera,
            Bounds bounds,
            Vector2 focusCenter,
            float focusHeight,
            float cameraTiltDegrees,
            float padding)
        {
            if (camera == null)
            {
                return 1f;
            }

            var rotation = Quaternion.Euler(cameraTiltDegrees, 0f, 0f);
            var forward = rotation * Vector3.forward;
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var focus = new Vector3(focusCenter.x, focusHeight, focusCenter.y);
            var verticalTangent = Mathf.Tan(
                Mathf.Clamp(camera.fieldOfView, 1f, 179f) * 0.5f * Mathf.Deg2Rad);
            var aspect = camera.aspect;
            if (!float.IsFinite(aspect) || aspect <= 0.01f)
            {
                aspect = 16f / 9f;
            }

            var horizontalTangent = verticalTangent * aspect;
            var fitDistance = 0f;
            for (var x = 0; x < 2; x++)
            {
                for (var y = 0; y < 2; y++)
                {
                    for (var z = 0; z < 2; z++)
                    {
                        var corner = new Vector3(
                            x == 0 ? bounds.min.x : bounds.max.x,
                            y == 0 ? bounds.min.y : bounds.max.y,
                            z == 0 ? bounds.min.z : bounds.max.z);
                        var relative = corner - focus;
                        var depthOffset = Vector3.Dot(relative, forward);
                        fitDistance = Mathf.Max(
                            fitDistance,
                            Mathf.Abs(Vector3.Dot(relative, right)) /
                            Mathf.Max(0.001f, horizontalTangent) - depthOffset,
                            Mathf.Abs(Vector3.Dot(relative, up)) /
                            Mathf.Max(0.001f, verticalTangent) - depthOffset);
                    }
                }
            }

            return Mathf.Max(1f, fitDistance) * Mathf.Max(1f, padding);
        }

        public static float ResolveRequiredShadowDistance(float maximumCameraDistance, Vector3 worldExtents)
        {
            var safeExtents = new Vector3(
                Mathf.Abs(worldExtents.x),
                Mathf.Abs(worldExtents.y),
                Mathf.Abs(worldExtents.z));
            return Mathf.Ceil(
                Mathf.Max(0f, maximumCameraDistance) + safeExtents.magnitude + ShadowDistancePadding);
        }

#if UNITY_EDITOR
        public void EditorConfigure(int battlefieldRadius, Camera camera = null)
        {
            targetCamera = camera != null ? camera : GetComponent<Camera>();
            ConfigureBounds(battlefieldRadius, HexSpatialContract.CellOuterRadius);
        }

        public void EditorStep(float deltaTime)
        {
            TickRotation(deltaTime);
            TickCamera(deltaTime);
        }
#endif
    }
}
