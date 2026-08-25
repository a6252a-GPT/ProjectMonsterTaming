using System;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    internal static class MonsterPositionReferenceOverlay
    {
        public static readonly Color ModelColor = new Color(0.72f, 0.48f, 1f, 1f);
        public static readonly Color AttackColor = new Color(0.12f, 0.95f, 0.9f, 1f);
        public static readonly Color HitColor = new Color(1f, 0.42f, 0.3f, 1f);
        public static readonly Color EditTargetColor = new Color(1f, 0.8f, 0.18f, 1f);

        public static Rect DrawVisibilityToolbar(
            Rect previewRect,
            float leftReservedWidth,
            ref bool showModel,
            ref bool showAttack,
            ref bool showHit)
        {
            var toolbarRect = CalculateVisibilityToolbarRect(previewRect, leftReservedWidth);
            EditorGUI.DrawRect(toolbarRect, new Color(0.025f, 0.035f, 0.05f, 0.86f));

            var allButtonWidth = Mathf.Max(1f, toolbarRect.width * 0.15f);
            var x = toolbarRect.x + 2f;
            if (GUI.Button(new Rect(x, toolbarRect.y + 2f, allButtonWidth, 20f), "모두 켜기", EditorStyles.miniButtonLeft))
            {
                showModel = true;
                showAttack = true;
                showHit = true;
            }

            x += allButtonWidth;
            if (GUI.Button(new Rect(x, toolbarRect.y + 2f, allButtonWidth, 20f), "모두 끄기", EditorStyles.miniButtonMid))
            {
                showModel = false;
                showAttack = false;
                showHit = false;
            }

            x += allButtonWidth;
            var toggleWidth = Mathf.Max(1f, (toolbarRect.xMax - 2f - x) / 3f);
            showModel = DrawReferenceToggle(
                new Rect(x, toolbarRect.y + 2f, toggleWidth, 20f),
                showModel,
                "모델 기준",
                ModelColor,
                EditorStyles.miniButtonMid);
            x += toggleWidth;
            showAttack = DrawReferenceToggle(
                new Rect(x, toolbarRect.y + 2f, toggleWidth, 20f),
                showAttack,
                "공격 기준",
                AttackColor,
                EditorStyles.miniButtonMid);
            x += toggleWidth;
            showHit = DrawReferenceToggle(
                new Rect(x, toolbarRect.y + 2f, Mathf.Max(1f, toolbarRect.xMax - 2f - x), 20f),
                showHit,
                "피격 기준",
                HitColor,
                EditorStyles.miniButtonRight);
            return toolbarRect;
        }

        public static Rect CalculateVisibilityToolbarRect(Rect previewRect, float leftReservedWidth)
        {
            const float margin = 10f;
            const float height = 24f;
            const float preferredWidth = 410f;
            const float gap = 6f;
            var availableWidth = Mathf.Max(1f, previewRect.width - margin * 2f);
            var width = Mathf.Min(preferredWidth, availableWidth);
            var topRightSpace = previewRect.width - margin * 2f - Mathf.Max(0f, leftReservedWidth) - gap;
            var y = previewRect.y + margin;
            if (topRightSpace < Mathf.Min(330f, width))
            {
                y += 55f;
            }

            return new Rect(previewRect.xMax - margin - width, y, width, height);
        }

        public static bool TryGetGuiPoint(
            Camera camera,
            Rect previewRect,
            Vector3 worldPosition,
            out Vector2 guiPoint)
        {
            return MonsterPreviewPositionHandleUtility.TryWorldToGuiPoint(
                       camera,
                       previewRect,
                       worldPosition,
                       out guiPoint) &&
                   previewRect.Contains(guiPoint);
        }

        public static void DrawPoint(Vector2 guiPoint, Color color, bool selected = false)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            var previousColor = Handles.color;
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawSolidDisc(guiPoint, Vector3.forward, selected ? 6.5f : 5f);
            Handles.color = selected ? Color.white : Color.black;
            Handles.DrawWireDisc(guiPoint, Vector3.forward, selected ? 8f : 6f);
            Handles.EndGUI();
            Handles.color = previousColor;
        }

        private static bool DrawReferenceToggle(
            Rect rect,
            bool value,
            string label,
            Color color,
            GUIStyle style)
        {
            var result = GUI.Toggle(rect, value, "     " + label, style);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(
                    new Rect(rect.x + 8f, rect.center.y - 3f, 6f, 6f),
                    result ? color : color * new Color(0.45f, 0.45f, 0.45f, 1f));
            }

            return result;
        }
    }

    internal sealed class MonsterPositionAdjustWindow : EditorWindow // 좌표 편집만 담당하는 가벼운 공용 팝업
    {
        private static readonly Vector2 WindowSize = new Vector2(720f, 620f);
        private static readonly int OrbitControlHint = "MonsterPositionAdjustOrbit".GetHashCode();
        private const float OuterMargin = 8f;
        private const float BottomHeight = 104f;
        private const float OrbitSensitivity = 0.35f;

        private MonsterMakerDraft draft;
        private MonsterMakerPreviewPositionBinding binding;
        private Func<Vector3, bool> applyValue;
        private Vector3 currentValue;
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
        private string errorMessage;

        public static bool CanOpen(MonsterMakerDraft source)
        {
            return source != null && source.VendorPrefab != null;
        }

        public static void Open(
            EditorWindow owner,
            MonsterMakerDraft source,
            MonsterMakerPreviewPositionBinding positionBinding,
            Vector3 initialValue,
            Func<Vector3, bool> onApply)
        {
            if (!CanOpen(source) || positionBinding == null || onApply == null)
            {
                return;
            }

            var window = CreateInstance<MonsterPositionAdjustWindow>();
            window.titleContent = new GUIContent("좌표 조절 · " + positionBinding.Label);
            window.minSize = WindowSize;
            window.maxSize = WindowSize;
            window.Initialize(source, positionBinding, initialValue, onApply);

            var ownerRect = owner == null
                ? new Rect(100f, 100f, WindowSize.x, WindowSize.y)
                : owner.position;
            window.position = new Rect(
                ownerRect.center.x - WindowSize.x * 0.5f,
                ownerRect.center.y - WindowSize.y * 0.5f,
                WindowSize.x,
                WindowSize.y);
            window.ShowUtility();
            window.Focus();
        }

        private void Initialize(
            MonsterMakerDraft source,
            MonsterMakerPreviewPositionBinding positionBinding,
            Vector3 initialValue,
            Func<Vector3, bool> onApply)
        {
            draft = source;
            binding = positionBinding;
            currentValue = initialValue;
            applyValue = onApply;
            BuildPreview();
        }

        private void OnDisable()
        {
            CleanupPreview();
            applyValue = null;
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Event.current.Use();
                Close();
                return;
            }

            var previewRect = new Rect(
                OuterMargin,
                OuterMargin,
                Mathf.Max(1f, position.width - OuterMargin * 2f),
                Mathf.Max(1f, position.height - BottomHeight - OuterMargin * 2f));
            var bottomRect = new Rect(
                OuterMargin,
                previewRect.yMax + 6f,
                previewRect.width,
                BottomHeight - 6f);

            // PositionHandle의 Camera 상태가 뒤에 그리는 IMGUI를 가리지 않도록 조작 UI를 먼저 그린다.
            DrawBottomControls(bottomRect);
            EditorGUI.DrawRect(previewRect, new Color(0.055f, 0.06f, 0.075f, 1f));
            if (previewUtility == null || previewRoot == null)
            {
                GUI.Label(
                    previewRect,
                    string.IsNullOrWhiteSpace(errorMessage)
                        ? "몬스터 모델 Preview를 준비하지 못했습니다."
                        : errorMessage,
                    EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                DrawPreview(previewRect);
            }
        }

        private void DrawPreview(Rect previewRect)
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
            DrawCameraHint(previewRect);
            DrawReferencePoints(previewRect);
            DrawPositionHandle(previewRect);
        }

        private void HandleCameraInput(Rect previewRect, Event current)
        {
            if (current == null)
            {
                return;
            }

            var controlId = GUIUtility.GetControlID(OrbitControlHint, FocusType.Passive, previewRect);
            var eventType = current.GetTypeForControl(controlId);
            if (eventType == EventType.ScrollWheel && previewRect.Contains(current.mousePosition) &&
                (GUIUtility.hotControl == 0 || GUIUtility.hotControl == controlId))
            {
                cameraDistanceScale = CalculateDistanceScale(cameraDistanceScale, current.delta.y);
                current.Use();
                Repaint();
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
                Repaint();
                return;
            }

            if (cameraOrbitActive &&
                ((eventType == EventType.MouseUp && current.button == 1) ||
                 eventType == EventType.MouseLeaveWindow))
            {
                if (GUIUtility.hotControl == controlId)
                {
                    GUIUtility.hotControl = 0;
                }

                cameraOrbitActive = false;
                current.Use();
                Repaint();
            }
        }

        internal static Vector2 CalculateOrbit(Vector2 currentOrbit, Vector2 pointerDelta)
        {
            return new Vector2(
                currentOrbit.x - pointerDelta.x * OrbitSensitivity,
                Mathf.Clamp(currentOrbit.y + pointerDelta.y * OrbitSensitivity, -80f, 80f));
        }

        internal static float CalculateDistanceScale(float currentScale, float wheelDeltaY)
        {
            return Mathf.Clamp(currentScale * (1f + wheelDeltaY * 0.08f), 0.15f, 8f);
        }

        private static void DrawCameraHint(Rect previewRect)
        {
            var hintRect = new Rect(previewRect.x + 10f, previewRect.y + 10f, 204f, 24f);
            EditorGUI.DrawRect(hintRect, new Color(0.025f, 0.035f, 0.05f, 0.82f));
            GUI.Label(
                new Rect(hintRect.x + 8f, hintRect.y + 3f, hintRect.width - 16f, 18f),
                "우클릭 드래그 · 회전  |  휠 · 확대/축소",
                EditorStyles.miniLabel);
        }

        private void DrawPositionHandle(Rect previewRect)
        {
            if (!TryGetHandleSpace(out var anchor, out var handleValue))
            {
                return;
            }

            var previousMatrix = Handles.matrix;
            var previousColor = Handles.color;
            var camera = previewUtility.camera;
            var previousTargetTexture = camera.targetTexture;
            var previousCameraRect = camera.rect;
            var previousPixelRect = camera.pixelRect;
            try
            {
                Handles.matrix = Matrix4x4.identity;
                // Preview 카메라가 RenderTexture를 물고 있으면 SetCamera가 previewRect를 무시한다.
                // Handle을 그리는 동안만 화면 카메라로 전환해 Unity 기본 PositionHandle 좌표를 맞춘다.
                camera.targetTexture = null;
                Handles.SetCamera(previewRect, camera);
                Handles.color = MonsterPositionReferenceOverlay.EditTargetColor;
                var worldPosition = anchor.TransformPoint(handleValue);
                EditorGUI.BeginChangeCheck();
                var changedWorld = Handles.PositionHandle(worldPosition, anchor.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    currentValue = ConvertHandleValueToStoredValue(anchor.InverseTransformPoint(changedWorld));
                    ApplyValueToPreview();
                    Repaint();
                }
            }
            finally
            {
                camera.targetTexture = previousTargetTexture;
                camera.rect = previousCameraRect;
                camera.pixelRect = previousPixelRect;
                Handles.matrix = previousMatrix;
                Handles.color = previousColor;
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

        private void DrawBottomControls(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.125f, 0.14f, 1f));
            var valueRect = new Rect(rect.x + 10f, rect.y + 6f, rect.width - 20f, 42f);
            EditorGUI.BeginChangeCheck();
            var changed = EditorGUI.Vector3Field(valueRect, new GUIContent(binding?.Label ?? "현재 좌표"), currentValue);
            if (EditorGUI.EndChangeCheck())
            {
                currentValue = changed;
                ApplyValueToPreview();
                Repaint();
            }

            const float buttonWidth = 112f;
            const float gap = 6f;
            var buttonY = rect.y + 64f;
            var cancelRect = new Rect(rect.xMax - buttonWidth, buttonY, buttonWidth, 26f);
            var applyRect = new Rect(cancelRect.x - gap - buttonWidth, buttonY, buttonWidth, 26f);
            if (GUI.Button(applyRect, "적용"))
            {
                if (applyValue?.Invoke(currentValue) != false)
                {
                    Close();
                }
                else
                {
                    ShowNotification(new GUIContent("현재 상태에서는 적용할 수 없습니다."));
                }
            }

            if (GUI.Button(cancelRect, "취소"))
            {
                Close();
            }
        }

        private void BuildPreview()
        {
            CleanupPreview();
            if (!CanOpen(draft))
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

                previewRoot = new GameObject("[Monster Position Preview]");
                previewVisual = UnityEngine.Object.Instantiate(draft.VendorPrefab, previewRoot.transform).transform;
                previewVisual.name = "Visual";
                previewVisual.localPosition = draft.VisualLocalPosition + Vector3.up * draft.GroundOffset;
                previewVisual.localRotation = Quaternion.Euler(0f, draft.FacingYawOffset, 0f);
                previewVisual.localScale = draft.VisualScale;
                attackOrigin = EnsureTransformPath(previewRoot.transform, draft.AttackOriginPath);
                attackOrigin.localPosition = draft.AttackOriginLocalPosition;
                hitCenter = EnsureTransformPath(previewRoot.transform, draft.HitCenterPath);
                hitCenter.localPosition = draft.HitCenterLocalPosition;
                valueAnchor = ResolveValueAnchor();
                PrepareModelOnlyPreview(previewRoot);
                ApplyValueToPreview();
                previewUtility.AddSingleGO(previewRoot);
                modelBounds = CalculateModelBounds(previewVisual.gameObject);
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
            distance *= 1.3f * cameraDistanceScale / Mathf.Clamp(draft.PreviewScale, 0.15f, 8f);
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

            handleValue = binding.ValueMode == MonsterMakerPreviewPositionValueMode.VisualLocal
                ? currentValue + Vector3.up * draft.GroundOffset
                : currentValue;
            return true;
        }

        private Vector3 ConvertHandleValueToStoredValue(Vector3 handleValue)
        {
            return binding.ValueMode == MonsterMakerPreviewPositionValueMode.VisualLocal
                ? handleValue - Vector3.up * draft.GroundOffset
                : handleValue;
        }

        private void ApplyValueToPreview()
        {
            if (previewRoot == null || binding == null)
            {
                return;
            }

            if (binding.ValueMode == MonsterMakerPreviewPositionValueMode.VisualLocal && previewVisual != null)
            {
                previewVisual.localPosition = currentValue + Vector3.up * draft.GroundOffset;
            }
            else if (IsStandardTarget("attackOriginLocalPosition") && attackOrigin != null)
            {
                attackOrigin.localPosition = currentValue;
            }
            else if (IsStandardTarget("hitCenterLocalPosition") && hitCenter != null)
            {
                hitCenter.localPosition = currentValue;
            }
        }

        private Transform ResolveValueAnchor()
        {
            if (binding == null || previewRoot == null)
            {
                return null;
            }

            if (binding.ValueMode != MonsterMakerPreviewPositionValueMode.AnchorOffset)
            {
                return previewRoot.transform;
            }

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
                var explicitSocket = previewRoot.transform.Find(path);
                if (explicitSocket != null)
                {
                    return explicitSocket;
                }
            }

            return attackOrigin ?? previewRoot.transform;
        }

        private bool IsStandardTarget(string propertyName)
        {
            return binding != null && string.Equals(
                binding.PropertyPath,
                propertyName,
                StringComparison.Ordinal);
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
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < transforms.Length; index++)
            {
                transforms[index].gameObject.hideFlags = HideFlags.HideAndDontSave;
            }

            var animators = root.GetComponentsInChildren<Animator>(true);
            for (var index = 0; index < animators.Length; index++)
            {
                animators[index].applyRootMotion = false;
                animators[index].enabled = false;
            }

            var cameras = root.GetComponentsInChildren<Camera>(true);
            for (var index = 0; index < cameras.Length; index++)
            {
                cameras[index].enabled = false;
            }

            var lights = root.GetComponentsInChildren<Light>(true);
            for (var index = 0; index < lights.Length; index++)
            {
                lights[index].enabled = false;
            }

            var audioSources = root.GetComponentsInChildren<AudioSource>(true);
            for (var index = 0; index < audioSources.Length; index++)
            {
                audioSources[index].enabled = false;
            }

            var particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var index = 0; index < particles.Length; index++)
            {
                particles[index].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            var trails = root.GetComponentsInChildren<TrailRenderer>(true);
            for (var index = 0; index < trails.Length; index++)
            {
                trails[index].enabled = false;
            }
        }

        private static Bounds CalculateModelBounds(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            var hasBounds = false;
            var bounds = new Bounds(model.transform.position + Vector3.up * 0.5f, Vector3.one);
            for (var index = 0; index < renderers.Length; index++)
            {
                if (!renderers[index].enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderers[index].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[index].bounds);
                }
            }

            return bounds;
        }

        private void CleanupPreview()
        {
            lastTexture = null;
            if (previewRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(previewRoot);
                previewRoot = null;
            }

            previewVisual = null;
            attackOrigin = null;
            hitCenter = null;
            valueAnchor = null;
            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }
        }
    }
}
