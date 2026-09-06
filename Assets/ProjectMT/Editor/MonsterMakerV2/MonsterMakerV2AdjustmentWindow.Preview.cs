using System;
using ProjectMT.EditorTools.MonsterMaker;
using ProjectMT.Shared.Combat;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    internal sealed partial class MonsterMakerV2AdjustmentWindow
    {
        private static readonly int OrbitControlHint =
            "MonsterMakerV2AdjustmentOrbit".GetHashCode();
        private static readonly int PositionHandleControlHint =
            "MonsterMakerV2AdjustmentPositionHandle".GetHashCode();
        private const float OrbitSensitivity = 0.35f;
        private const float PositionHandleLengthPixels = 72f;
        private const float PositionHandleHitRadius = 11f;

        private MonsterMakerDraft draft;
        private MonsterMakerPreviewPositionBinding binding;
        private Vector3 currentPosition;
        private Vector3 currentEuler;
        private float currentScale = 1f;
        private float currentLifetime = 1f;
        private float currentPlaybackOffset;
        private float currentPlaybackSpeed = 1f;
        private float vfxPreviewElapsed;
        private double vfxPreviewLastUpdateTime;
        private bool vfxPreviewPlaying;
        private GameObject previewVfxPrefab;
        private Transform previewVfx;
        private PreviewRenderUtility previewUtility;
        private Texture lastTexture;
        private GameObject previewRoot;
        private Transform previewVisual;
        private Transform attackOrigin;
        private Transform hitCenter;
        private Transform valueAnchor;
        private Bounds modelBounds;
        private bool showModelReference = true;
        private bool showAttackReference = true;
        private bool showHitReference = true;
        private float cameraYaw = 145f;
        private float cameraPitch = 12f;
        private float cameraDistanceScale = 1f;
        private bool cameraOrbitActive;
        private PositionHandleAxis activePositionAxis;
        private Vector2 positionDragMouseStart;
        private Vector3 positionDragWorldStart;
        private Vector3 positionDragAxisWorld;
        private Vector2 positionDragAxisGui;
        private float positionDragUnitsPerPixel;
        private string errorMessage;
        private GameObject externalPreviewModelPrefab;
        private float externalPreviewModelHeight = 2f;
        private Vector3 externalAttackOriginLocalPosition;
        private Vector3 externalHitCenterLocalPosition;
        private bool externalLifetimeEditable = true;

        private bool IsVfxMode => previewVfxPrefab != null && applyVfx != null;
        private bool IsExternalPreview => externalPreviewModelPrefab != null;

        private void DrawPreviewGUI()
        {
            var size = previewIMGUI == null
                ? new Vector2(640f, 360f)
                : previewIMGUI.contentRect.size;
            var rect = new Rect(0f, 0f, Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
            EditorGUI.DrawRect(rect, new Color(0.055f, 0.06f, 0.075f, 1f));
            if (previewUtility == null || previewRoot == null)
            {
                GUI.Label(
                    rect,
                    string.IsNullOrWhiteSpace(errorMessage)
                        ? "3D 모델 Preview를 준비하지 못했습니다."
                        : errorMessage,
                    EditorStyles.centeredGreyMiniLabel);
                return;
            }

            DrawPreviewCanvas(rect);
        }

        private void DrawPreviewCanvas(Rect previewRect)
        {
            HandleCameraInput(previewRect, Event.current);
            ConfigureCamera(previewRect);
            if (Event.current.type == EventType.Repaint)
            {
                var renderRect = new Rect(0f, 0f, previewRect.width, previewRect.height);
                previewUtility.BeginPreview(renderRect, GUIStyle.none);
                previewUtility.Render(true);
                lastTexture = previewUtility.EndPreview();
            }

            if (lastTexture != null)
            {
                GUI.DrawTexture(previewRect, lastTexture, ScaleMode.StretchToFill, false);
            }

            MonsterPositionReferenceOverlay.DrawVisibilityToolbar(
                previewRect,
                0f,
                ref showModelReference,
                ref showAttackReference,
                ref showHitReference);
            DrawReferencePoints(previewRect);
            DrawPositionHandle(previewRect);
        }

        private void HandleCameraInput(Rect previewRect, Event current)
        {
            if (current == null) return;
            var controlId = GUIUtility.GetControlID(OrbitControlHint, FocusType.Passive, previewRect);
            var eventType = current.GetTypeForControl(controlId);
            if (eventType == EventType.ScrollWheel && previewRect.Contains(current.mousePosition) &&
                (GUIUtility.hotControl == 0 || GUIUtility.hotControl == controlId))
            {
                cameraDistanceScale = CalculateDistanceScale(cameraDistanceScale, current.delta.y);
                current.Use();
                MarkPreviewDirty();
                return;
            }

            if (eventType == EventType.MouseDown && current.button == 1 &&
                previewRect.Contains(current.mousePosition) && GUIUtility.hotControl == 0)
            {
                GUIUtility.hotControl = controlId;
                cameraOrbitActive = true;
                current.Use();
                return;
            }

            if (eventType == EventType.MouseDrag && current.button == 1 && GUIUtility.hotControl == controlId)
            {
                var orbit = CalculateOrbit(new Vector2(cameraYaw, cameraPitch), current.delta);
                cameraYaw = orbit.x;
                cameraPitch = orbit.y;
                current.Use();
                MarkPreviewDirty();
                return;
            }

            if (!cameraOrbitActive ||
                ((eventType != EventType.MouseUp || current.button != 1) &&
                 eventType != EventType.MouseLeaveWindow))
            {
                return;
            }

            if (GUIUtility.hotControl == controlId) GUIUtility.hotControl = 0;
            cameraOrbitActive = false;
            current.Use();
            MarkPreviewDirty();
        }

        internal static Vector2 CalculateOrbit(Vector2 currentOrbit, Vector2 pointerDelta)
        {
            return new Vector2(
                currentOrbit.x - pointerDelta.x * OrbitSensitivity,
                Mathf.Clamp(currentOrbit.y + pointerDelta.y * OrbitSensitivity, -80f, 80f));
        }

        internal static float CalculateDistanceScale(float currentScaleValue, float wheelDeltaY)
        {
            return Mathf.Clamp(currentScaleValue * (1f + wheelDeltaY * 0.08f), 0.15f, 8f);
        }

        private void DrawPositionHandle(Rect previewRect)
        {
            if (!TryGetHandleSpace(out var anchor, out var handleValue)) return;
            var camera = previewUtility.camera;
            var worldPosition = anchor.TransformPoint(handleValue);
            if (!MonsterPreviewPositionHandleUtility.TryWorldToGuiPoint(
                    camera,
                    previewRect,
                    worldPosition,
                    out var originGui))
            {
                return;
            }

            var worldLength = ResolvePositionHandleWorldLength(camera, worldPosition, previewRect.height);
            var axes = new[]
            {
                new PositionHandleAxisView(PositionHandleAxis.X, anchor.right, new Color(0.95f, 0.22f, 0.18f, 1f)),
                new PositionHandleAxisView(PositionHandleAxis.Y, anchor.up, new Color(0.28f, 0.95f, 0.34f, 1f)),
                new PositionHandleAxisView(PositionHandleAxis.Z, anchor.forward, new Color(0.18f, 0.48f, 1f, 1f))
            };
            for (var index = 0; index < axes.Length; index++)
            {
                axes[index] = axes[index].WithEnd(
                    ResolveAxisGuiEnd(camera, previewRect, worldPosition, axes[index].WorldAxis, worldLength, originGui));
            }

            HandlePositionHandleInput(previewRect, anchor, axes, worldLength, originGui, Event.current);
            if (Event.current.type == EventType.Repaint)
                DrawPositionAxes(originGui, axes);
        }

        private void HandlePositionHandleInput(
            Rect previewRect,
            Transform anchor,
            PositionHandleAxisView[] axes,
            float worldLength,
            Vector2 originGui,
            Event current)
        {
            if (current == null) return;
            var controlId = GUIUtility.GetControlID(PositionHandleControlHint, FocusType.Passive, previewRect);
            var eventType = current.GetTypeForControl(controlId);
            if (GUIUtility.hotControl == controlId && activePositionAxis != PositionHandleAxis.None)
            {
                if (eventType == EventType.MouseDrag)
                {
                    var pixelDelta = Vector2.Dot(
                        current.mousePosition - positionDragMouseStart,
                        positionDragAxisGui);
                    var changedWorld = positionDragWorldStart +
                                       positionDragAxisWorld * pixelDelta * positionDragUnitsPerPixel;
                    currentPosition = ConvertHandleValueToStoredValue(anchor.InverseTransformPoint(changedWorld));
                    ApplyValueToPreview();
                    SyncPositionFieldsFromHandle();
                    current.Use();
                    MarkPreviewDirty();
                    return;
                }

                if ((eventType == EventType.MouseUp && current.button == 0) ||
                    eventType == EventType.MouseLeaveWindow)
                {
                    GUIUtility.hotControl = 0;
                    activePositionAxis = PositionHandleAxis.None;
                    current.Use();
                    MarkPreviewDirty();
                }
                return;
            }

            if (eventType != EventType.MouseDown || current.button != 0 ||
                !previewRect.Contains(current.mousePosition) || GUIUtility.hotControl != 0)
            {
                return;
            }

            var selected = PositionHandleAxis.None;
            var selectedDistance = PositionHandleHitRadius;
            var selectedView = default(PositionHandleAxisView);
            for (var index = 0; index < axes.Length; index++)
            {
                var distance = DistanceToSegment(current.mousePosition, originGui, axes[index].GuiEnd);
                if (distance >= selectedDistance) continue;
                selected = axes[index].Axis;
                selectedDistance = distance;
                selectedView = axes[index];
            }
            if (selected == PositionHandleAxis.None) return;

            var guiVector = selectedView.GuiEnd - originGui;
            var pixelLength = Mathf.Max(1f, guiVector.magnitude);
            activePositionAxis = selected;
            positionDragMouseStart = current.mousePosition;
            positionDragWorldStart = anchor.TransformPoint(
                binding.ValueMode == MonsterMakerPreviewPositionValueMode.VisualLocal
                    ? currentPosition + Vector3.up * draft.GroundOffset
                    : currentPosition);
            positionDragAxisWorld = selectedView.WorldAxis.normalized;
            positionDragAxisGui = guiVector / pixelLength;
            positionDragUnitsPerPixel = worldLength / pixelLength;
            GUIUtility.hotControl = controlId;
            current.Use();
            MarkPreviewDirty();
        }

        private static Vector2 ResolveAxisGuiEnd(
            Camera camera,
            Rect previewRect,
            Vector3 worldPosition,
            Vector3 worldAxis,
            float worldLength,
            Vector2 originGui)
        {
            if (MonsterPreviewPositionHandleUtility.TryWorldToGuiPoint(
                    camera,
                    previewRect,
                    worldPosition + worldAxis.normalized * worldLength,
                    out var endGui) &&
                Vector2.Distance(originGui, endGui) >= 8f)
            {
                return endGui;
            }

            var cameraAxis = camera.transform.InverseTransformDirection(worldAxis.normalized);
            var direction = new Vector2(cameraAxis.x, -cameraAxis.y);
            if (direction.sqrMagnitude < 0.01f) direction = new Vector2(0.7f, -0.7f);
            return originGui + direction.normalized * PositionHandleLengthPixels;
        }

        private static float ResolvePositionHandleWorldLength(Camera camera, Vector3 worldPosition, float pixelHeight)
        {
            if (camera == null) return 1f;
            if (camera.orthographic)
                return 2f * camera.orthographicSize / Mathf.Max(1f, pixelHeight) * PositionHandleLengthPixels;
            var depth = Mathf.Max(0.05f, Vector3.Dot(worldPosition - camera.transform.position, camera.transform.forward));
            var verticalSize = 2f * depth * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return verticalSize / Mathf.Max(1f, pixelHeight) * PositionHandleLengthPixels;
        }

        internal static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            var segment = end - start;
            if (segment.sqrMagnitude <= 0.0001f) return Vector2.Distance(point, start);
            var ratio = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude);
            return Vector2.Distance(point, start + segment * ratio);
        }

        private void DrawPositionAxes(Vector2 origin, PositionHandleAxisView[] axes)
        {
            var previousColor = Handles.color;
            Handles.BeginGUI();
            for (var index = 0; index < axes.Length; index++)
            {
                var color = axes[index].Axis == activePositionAxis
                    ? Color.Lerp(axes[index].Color, Color.white, 0.52f)
                    : axes[index].Color;
                var start = new Vector3(origin.x, origin.y, 0f);
                var end = new Vector3(axes[index].GuiEnd.x, axes[index].GuiEnd.y, 0f);
                Handles.color = new Color(0f, 0f, 0f, 0.5f);
                Handles.DrawAAPolyLine(6f, start, end);
                Handles.color = color;
                Handles.DrawAAPolyLine(3f, start, end);
                Handles.DrawSolidDisc(end, Vector3.forward, 5f);
            }
            Handles.color = Color.white;
            Handles.DrawSolidDisc(new Vector3(origin.x, origin.y, 0f), Vector3.forward, 3.5f);
            Handles.color = previousColor;
            Handles.EndGUI();
        }

        private void SyncPositionFieldsFromHandle()
        {
            updatingUi = true;
            try
            {
                positionField?.SetValueWithoutNotify(currentPosition);
                vfxPositionField?.SetValueWithoutNotify(currentPosition);
            }
            finally
            {
                updatingUi = false;
            }
        }

        private void DrawReferencePoints(Rect previewRect)
        {
            DrawReferencePoint(
                previewRect,
                showModelReference,
                previewVisual == null ? Vector3.zero : previewVisual.position,
                MonsterPositionReferenceOverlay.ModelColor,
                IsStandardTarget("visualLocalPosition"));
            DrawReferencePoint(
                previewRect,
                showAttackReference,
                attackOrigin == null ? Vector3.zero : attackOrigin.position,
                MonsterPositionReferenceOverlay.AttackColor,
                IsStandardTarget("attackOriginLocalPosition"));
            DrawReferencePoint(
                previewRect,
                showHitReference,
                hitCenter == null ? Vector3.zero : hitCenter.position,
                MonsterPositionReferenceOverlay.HitColor,
                IsStandardTarget("hitCenterLocalPosition"));

            if (binding.ValueMode == MonsterMakerPreviewPositionValueMode.AnchorOffset &&
                TryGetHandleSpace(out var anchor, out var handleValue) &&
                MonsterPositionReferenceOverlay.TryGetGuiPoint(
                    previewUtility.camera,
                    previewRect,
                    anchor.TransformPoint(handleValue),
                    out var guiPoint))
            {
                MonsterPositionReferenceOverlay.DrawPoint(
                    guiPoint,
                    MonsterPositionReferenceOverlay.EditTargetColor,
                    true);
            }
        }

        private void DrawReferencePoint(
            Rect previewRect,
            bool visible,
            Vector3 worldPosition,
            Color color,
            bool selected)
        {
            if (!visible || !MonsterPositionReferenceOverlay.TryGetGuiPoint(
                    previewUtility.camera,
                    previewRect,
                    worldPosition,
                    out var guiPoint))
            {
                return;
            }

            MonsterPositionReferenceOverlay.DrawPoint(guiPoint, color, selected);
        }

        private void SetVfxPreviewPlaying(bool playing)
        {
            vfxPreviewPlaying = playing;
            vfxPreviewLastUpdateTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= UpdateVfxPreview;
            if (playing && IsVfxMode) EditorApplication.update += UpdateVfxPreview;
            RefreshPlaybackControls();
            MarkPreviewDirty();
        }

        private void RestartVfxPreview()
        {
            if (previewVfx == null) return;
            MonsterBasicAttackVfxPlayback.RestartAtOffset(
                previewVfx.gameObject,
                currentPlaybackOffset,
                false,
                currentPlaybackSpeed);
            vfxPreviewElapsed = 0f;
            vfxPreviewLastUpdateTime = EditorApplication.timeSinceStartup;
            UpdatePlaybackStatusLabel();
            MarkPreviewDirty();
        }

        private void UpdateVfxPreview()
        {
            if (!vfxPreviewPlaying || previewVfx == null) return;
            var now = EditorApplication.timeSinceStartup;
            var deltaTime = Mathf.Clamp((float)(now - vfxPreviewLastUpdateTime), 0f, 0.05f);
            vfxPreviewLastUpdateTime = now;
            if (deltaTime <= 0f) return;
            MonsterBasicAttackVfxPlayback.Simulate(previewVfx.gameObject, deltaTime);
            vfxPreviewElapsed += deltaTime;
            UpdatePlaybackStatusLabel();
            MarkPreviewDirty();
        }

        private void BuildPreview()
        {
            CleanupPreview();
            if (!IsExternalPreview && !CanOpen(draft))
            {
                errorMessage = "3D 모델 프리팹을 먼저 지정하세요.";
                return;
            }

            try
            {
                previewUtility = new PreviewRenderUtility();
                PrefabPreviewStage.ConfigureUniversalCamera(previewUtility.camera);
                previewUtility.camera.backgroundColor = new Color(0.055f, 0.06f, 0.075f, 1f);
                previewUtility.ambientColor = new Color(0.48f, 0.5f, 0.56f, 1f);
                if (previewUtility.lights.Length > 0)
                {
                    PrefabPreviewStage.ConfigureLight(
                        previewUtility.lights[0],
                        2.1f,
                        Quaternion.Euler(42f, -32f, 0f),
                        new Color(1f, 0.96f, 0.9f, 1f));
                }
                if (previewUtility.lights.Length > 1)
                {
                    PrefabPreviewStage.ConfigureLight(
                        previewUtility.lights[1],
                        1.15f,
                        Quaternion.Euler(325f, 138f, 0f),
                        new Color(0.58f, 0.7f, 1f, 1f));
                }

                previewRoot = new GameObject("[Monster Maker V2 Adjustment Preview]");
                var modelPrefab = IsExternalPreview ? externalPreviewModelPrefab : draft.VendorPrefab;
                previewVisual = Instantiate(modelPrefab, previewRoot.transform).transform;
                previewVisual.name = "Visual";
                if (IsExternalPreview)
                {
                    previewVisual.localPosition = Vector3.zero;
                    previewVisual.localRotation = Quaternion.identity;
                    previewVisual.localScale = Vector3.one;
                    GroundAndFit(previewVisual.gameObject, externalPreviewModelHeight);
                    attackOrigin = EnsureTransformPath(previewRoot.transform, "__AttackOrigin");
                    attackOrigin.localPosition = externalAttackOriginLocalPosition;
                    hitCenter = EnsureTransformPath(previewRoot.transform, "__HitCenter");
                    hitCenter.localPosition = externalHitCenterLocalPosition;
                }
                else
                {
                    previewVisual.localPosition = draft.VisualLocalPosition + Vector3.up * draft.GroundOffset;
                    previewVisual.localRotation = Quaternion.Euler(0f, draft.FacingYawOffset, 0f);
                    previewVisual.localScale = draft.VisualScale;
                    attackOrigin = EnsureTransformPath(previewRoot.transform, draft.AttackOriginPath);
                    attackOrigin.localPosition = draft.AttackOriginLocalPosition;
                    hitCenter = EnsureTransformPath(previewRoot.transform, draft.HitCenterPath);
                    hitCenter.localPosition = draft.HitCenterLocalPosition;
                }
                valueAnchor = ResolveValueAnchor();
                PrepareModelOnlyPreview(previewRoot);
                // Vendor VFX의 과장된 Renderer Bounds가 카메라를 밀어내지 않도록 모델만으로 구도를 고정한다.
                modelBounds = CalculateModelBounds(previewRoot);
                if (IsVfxMode)
                {
                    previewVfx = Instantiate(previewVfxPrefab, valueAnchor).transform;
                    previewVfx.name = "VFX Preview";
                    PrepareVfxPreview(previewVfx.gameObject);
                }
                ApplyValueToPreview();
                previewUtility.AddSingleGO(previewRoot);
                errorMessage = null;
            }
            catch (Exception exception)
            {
                errorMessage = exception.Message;
                CleanupPreview();
            }
        }

        private void ConfigureCamera(Rect rect)
        {
            var camera = previewUtility.camera;
            camera.orthographic = false;
            camera.fieldOfView = 36f;
            camera.aspect = rect.width / Mathf.Max(1f, rect.height);
            var center = modelBounds.center;
            var radius = Mathf.Max(0.35f, modelBounds.extents.magnitude);
            var verticalHalfAngle = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            var horizontalHalfAngle = Mathf.Atan(Mathf.Tan(verticalHalfAngle) * Mathf.Max(0.2f, camera.aspect));
            var limitingAngle = Mathf.Min(verticalHalfAngle, horizontalHalfAngle);
            var distance = radius / Mathf.Max(0.05f, Mathf.Sin(limitingAngle));
            var previewScale = IsExternalPreview ? 1f : draft.PreviewScale;
            distance *= 1.3f * cameraDistanceScale / Mathf.Clamp(previewScale, 0.15f, 8f);
            var direction = Quaternion.Euler(cameraPitch, cameraYaw, 0f) * Vector3.forward;
            camera.transform.position = center - direction.normalized * distance;
            camera.transform.rotation = Quaternion.LookRotation(center - camera.transform.position, Vector3.up);
        }

        private bool TryGetHandleSpace(out Transform anchor, out Vector3 handleValue)
        {
            anchor = valueAnchor == null ? previewRoot?.transform : valueAnchor;
            if (anchor == null)
            {
                handleValue = Vector3.zero;
                return false;
            }
            handleValue = !IsExternalPreview && binding.ValueMode == MonsterMakerPreviewPositionValueMode.VisualLocal
                ? currentPosition + Vector3.up * draft.GroundOffset
                : currentPosition;
            return true;
        }

        private Vector3 ConvertHandleValueToStoredValue(Vector3 handleValue)
        {
            return !IsExternalPreview && binding.ValueMode == MonsterMakerPreviewPositionValueMode.VisualLocal
                ? handleValue - Vector3.up * draft.GroundOffset
                : handleValue;
        }

        private void ApplyValueToPreview()
        {
            if (previewRoot == null || binding == null) return;
            if (IsVfxMode && previewVfx != null)
            {
                previewVfx.localPosition = currentPosition;
                previewVfx.localRotation = Quaternion.Euler(currentEuler);
                MonsterBasicAttackVfxPlayback.ApplyInstanceScale(
                    previewVfx.gameObject,
                    previewVfxPrefab.transform.localScale *
                    currentScale *
                    (IsExternalPreview ? 1f : Mathf.Max(0.01f, draft.VfxScale)));
            }
            else if (binding.ValueMode == MonsterMakerPreviewPositionValueMode.VisualLocal && previewVisual != null)
            {
                previewVisual.localPosition = currentPosition + Vector3.up * draft.GroundOffset;
            }
            else if (IsStandardTarget("attackOriginLocalPosition") && attackOrigin != null)
            {
                attackOrigin.localPosition = currentPosition;
            }
            else if (IsStandardTarget("hitCenterLocalPosition") && hitCenter != null)
            {
                hitCenter.localPosition = currentPosition;
            }
        }

        private Transform ResolveValueAnchor()
        {
            if (binding == null || previewRoot == null) return null;
            if (binding.ValueMode != MonsterMakerPreviewPositionValueMode.AnchorOffset)
                return previewRoot.transform;
            return binding.Anchor switch
            {
                MonsterMakerPreviewAnchor.AttackOrigin => attackOrigin ?? previewRoot.transform,
                MonsterMakerPreviewAnchor.HitCenter => hitCenter ?? previewRoot.transform,
                MonsterMakerPreviewAnchor.Socket => ResolveSocket(binding.SocketPath),
                _ => previewRoot.transform
            };
        }

        private Transform ResolveSocket(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                var socket = previewRoot.transform.Find(path);
                if (socket != null) return socket;
            }
            return attackOrigin ?? previewRoot.transform;
        }

        private bool IsStandardTarget(string propertyName)
        {
            return binding != null && string.Equals(binding.PropertyPath, propertyName, StringComparison.Ordinal);
        }

        private static Transform EnsureTransformPath(Transform root, string path)
        {
            var current = root;
            var parts = (path ?? string.Empty).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < parts.Length; index++)
            {
                var child = current.Find(parts[index]);
                if (child == null)
                {
                    child = new GameObject(parts[index]).transform;
                    child.SetParent(current, false);
                }
                current = child;
            }
            return current;
        }

        private static void PrepareModelOnlyPreview(GameObject root)
        {
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
                item.gameObject.hideFlags = HideFlags.HideAndDontSave;
            foreach (var item in root.GetComponentsInChildren<Animator>(true))
            {
                item.applyRootMotion = false;
                item.enabled = false;
            }
            foreach (var item in root.GetComponentsInChildren<Camera>(true)) item.enabled = false;
            foreach (var item in root.GetComponentsInChildren<Light>(true)) item.enabled = false;
            foreach (var item in root.GetComponentsInChildren<AudioSource>(true)) item.enabled = false;
            foreach (var item in root.GetComponentsInChildren<ParticleSystem>(true))
                item.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var item in root.GetComponentsInChildren<TrailRenderer>(true)) item.enabled = false;
        }

        private static void PrepareVfxPreview(GameObject root)
        {
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
                item.gameObject.hideFlags = HideFlags.HideAndDontSave;
            foreach (var item in root.GetComponentsInChildren<MonoBehaviour>(true)) item.enabled = false;
            foreach (var item in root.GetComponentsInChildren<AudioSource>(true)) item.enabled = false;
            foreach (var item in root.GetComponentsInChildren<Camera>(true)) item.enabled = false;
            MonsterBasicAttackVfxPlayback.ApplyBrightnessScale(
                root,
                MonsterBasicAttackVfxPlayback.DefaultMainBattleBrightnessScale);
        }

        private static Bounds CalculateModelBounds(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            var hasBounds = false;
            var bounds = new Bounds(model.transform.position + Vector3.up * 0.5f, Vector3.one);
            foreach (var renderer in renderers)
            {
                if (!renderer.enabled) continue;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return bounds;
        }

        private static void GroundAndFit(GameObject instance, float desiredHeight)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            if (bounds.size.y > 0.001f) instance.transform.localScale *= desiredHeight / bounds.size.y;
            renderers = instance.GetComponentsInChildren<Renderer>(true);
            bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            instance.transform.position += Vector3.down * bounds.min.y;
        }

        private void MarkPreviewDirty()
        {
            previewIMGUI?.MarkDirtyRepaint();
        }

        private void CleanupPreview()
        {
            EditorApplication.update -= UpdateVfxPreview;
            if (activePositionAxis != PositionHandleAxis.None)
            {
                GUIUtility.hotControl = 0;
                activePositionAxis = PositionHandleAxis.None;
            }
            lastTexture = null;
            if (previewRoot != null)
            {
                DestroyImmediate(previewRoot);
                previewRoot = null;
            }
            previewVisual = null;
            attackOrigin = null;
            hitCenter = null;
            valueAnchor = null;
            previewVfx = null;
            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }
        }

        private enum PositionHandleAxis
        {
            None,
            X,
            Y,
            Z
        }

        private readonly struct PositionHandleAxisView
        {
            public PositionHandleAxisView(PositionHandleAxis axis, Vector3 worldAxis, Color color)
                : this(axis, worldAxis, Vector2.zero, color)
            {
            }

            private PositionHandleAxisView(
                PositionHandleAxis axis,
                Vector3 worldAxis,
                Vector2 guiEnd,
                Color color)
            {
                Axis = axis;
                WorldAxis = worldAxis;
                GuiEnd = guiEnd;
                Color = color;
            }

            public PositionHandleAxis Axis { get; }
            public Vector3 WorldAxis { get; }
            public Vector2 GuiEnd { get; }
            public Color Color { get; }

            public PositionHandleAxisView WithEnd(Vector2 guiEnd) =>
                new PositionHandleAxisView(Axis, WorldAxis, guiEnd, Color);
        }
    }
}
