using System;
using ProjectMT.Shared.Combat;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    internal sealed class MonsterPositionAdjustWindow : EditorWindow // 좌표 편집만 담당하는 가벼운 공용 팝업
    {
        private static readonly Vector2 WindowSize = new Vector2(720f, 676f);
        private static readonly int OrbitControlHint = "MonsterPositionAdjustOrbit".GetHashCode();
        private const float OuterMargin = 8f;
        private const float BottomHeight = 104f;
        private const float VfxBottomHeight = 292f;
        private const float VfxPlaybackSpeedGaugeDefaultExponent = 1f;
        private const float OrbitSensitivity = 0.35f;

        private MonsterMakerDraft draft;
        private MonsterMakerPreviewPositionBinding binding;
        private Func<Vector3, bool> applyValue;
        private Func<Vector3, Vector3, float, float, float, float, bool> applyVfxValue;
        private Vector3 currentValue;
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

        public static void OpenVfx(
            EditorWindow owner,
            MonsterMakerDraft source,
            MonsterMakerPreviewPositionBinding positionBinding,
            GameObject vfxPrefab,
            Vector3 initialPosition,
            Vector3 initialEuler,
            float initialScale,
            float initialLifetime,
            float initialPlaybackOffset,
            float initialPlaybackSpeed,
            Func<Vector3, Vector3, float, float, float, float, bool> onApply)
        {
            if (!CanOpen(source) || positionBinding == null || vfxPrefab == null || onApply == null)
            {
                return;
            }

            var window = CreateInstance<MonsterPositionAdjustWindow>();
            window.titleContent = new GUIContent("VFX 조절 · " + positionBinding.Label);
            window.minSize = WindowSize;
            window.maxSize = WindowSize;
            window.InitializeVfx(
                source,
                positionBinding,
                vfxPrefab,
                initialPosition,
                initialEuler,
                initialScale,
                initialLifetime,
                initialPlaybackOffset,
                initialPlaybackSpeed,
                onApply);

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

        private void InitializeVfx(
            MonsterMakerDraft source,
            MonsterMakerPreviewPositionBinding positionBinding,
            GameObject vfxPrefab,
            Vector3 initialPosition,
            Vector3 initialEuler,
            float initialScale,
            float initialLifetime,
            float initialPlaybackOffset,
            float initialPlaybackSpeed,
            Func<Vector3, Vector3, float, float, float, float, bool> onApply)
        {
            draft = source;
            binding = positionBinding;
            previewVfxPrefab = vfxPrefab;
            currentValue = initialPosition;
            currentEuler = initialEuler;
            currentScale = Mathf.Max(0.01f, initialScale);
            currentLifetime = Mathf.Max(0.01f, initialLifetime);
            currentPlaybackOffset = Mathf.Max(0f, initialPlaybackOffset);
            currentPlaybackSpeed = SanitizePlaybackSpeed(initialPlaybackSpeed);
            applyVfxValue = onApply;
            BuildPreview();
            RestartVfxPreview();
            SetVfxPreviewPlaying(true);
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdateVfxPreview;
            CleanupPreview();
            applyValue = null;
            applyVfxValue = null;
            previewVfxPrefab = null;
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Event.current.Use();
                Close();
                return;
            }

            var bottomHeight = IsVfxMode ? VfxBottomHeight : BottomHeight;
            var previewRect = new Rect(
                OuterMargin,
                OuterMargin,
                Mathf.Max(1f, position.width - OuterMargin * 2f),
                Mathf.Max(1f, position.height - bottomHeight - OuterMargin * 2f));
            var bottomRect = new Rect(
                OuterMargin,
                previewRect.yMax + 6f,
                previewRect.width,
                bottomHeight - 6f);

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
            if (IsVfxMode)
            {
                DrawVfxBottomControls(rect);
                return;
            }

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

        private void DrawVfxBottomControls(Rect rect)
        {
            var content = new Rect(rect.x + 10f, rect.y + 7f, rect.width - 20f, 20f);
            EditorGUI.BeginChangeCheck();
            var changedPosition = DrawCompactVector3Field(content, "위치 보정", currentValue);
            content.y += 24f;
            var changedEuler = DrawCompactVector3Field(content, "회전 보정", currentEuler);
            content.y += 24f;
            GUI.Label(
                new Rect(content.x, content.y, 78f, content.height),
                "크기 배율",
                EditorStyles.miniLabel);
            var changedScale = EditorGUI.FloatField(
                new Rect(content.x + 78f, content.y, 150f, content.height),
                currentScale);
            if (EditorGUI.EndChangeCheck())
            {
                currentValue = changedPosition;
                currentEuler = changedEuler;
                currentScale = Mathf.Max(0.01f, changedScale);
                ApplyValueToPreview();
                Repaint();
            }

            var lifetimeRect = new Rect(
                rect.x + 10f,
                rect.y + 83f,
                rect.width - 20f,
                22f);
            GUI.Label(
                new Rect(lifetimeRect.x, lifetimeRect.y + 2f, 78f, 18f),
                new GUIContent("유지 시간", "VFX가 생성된 뒤 사라질 때까지의 시간입니다."),
                EditorStyles.miniLabel);
            EditorGUI.BeginChangeCheck();
            var changedLifetime = EditorGUI.FloatField(
                new Rect(lifetimeRect.x + 78f, lifetimeRect.y, 150f, lifetimeRect.height),
                currentLifetime);
            if (EditorGUI.EndChangeCheck())
            {
                currentLifetime = Mathf.Max(0.01f, changedLifetime);
            }
            GUI.Label(
                new Rect(lifetimeRect.x + 234f, lifetimeRect.y + 2f, 20f, 18f),
                "초",
                EditorStyles.miniLabel);

            var playbackRect = new Rect(
                rect.x + 10f,
                rect.y + 111f,
                rect.width - 20f,
                22f);
            GUI.Label(
                new Rect(playbackRect.x, playbackRect.y + 2f, 78f, 18f),
                "VFX 재생",
                EditorStyles.miniLabel);
            var x = playbackRect.x + 78f;
            if (GUI.Button(
                    new Rect(x, playbackRect.y, 82f, playbackRect.height),
                    vfxPreviewPlaying ? "일시정지" : "재생",
                    EditorStyles.miniButtonLeft))
            {
                SetVfxPreviewPlaying(!vfxPreviewPlaying);
            }

            x += 82f;
            if (GUI.Button(
                    new Rect(x, playbackRect.y, 82f, playbackRect.height),
                    "처음부터",
                    EditorStyles.miniButtonRight))
            {
                RestartVfxPreview();
            }

            x += 94f;
            GUI.Label(
                new Rect(x, playbackRect.y + 2f, playbackRect.xMax - x, 18f),
                $"현재 원본 {currentPlaybackOffset + vfxPreviewElapsed * currentPlaybackSpeed:0.00}초 · " +
                $"{currentPlaybackSpeed:0.##}배",
                EditorStyles.miniLabel);

            var offsetRect = new Rect(
                rect.x + 10f,
                rect.y + 139f,
                rect.width - 20f,
                22f);
            GUI.Label(
                new Rect(offsetRect.x, offsetRect.y + 2f, 78f, 18f),
                "내부 시작",
                EditorStyles.miniLabel);
            x = offsetRect.x + 78f;
            GUI.Label(
                new Rect(x, offsetRect.y + 2f, 48f, 18f),
                new GUIContent("시작점", "Prefab 원본 시간축에서 처음 건너뛸 구간입니다."),
                EditorStyles.miniLabel);
            x += 48f;
            EditorGUI.BeginChangeCheck();
            var changedPlaybackOffset = EditorGUI.FloatField(
                new Rect(x, offsetRect.y, 70f, offsetRect.height),
                currentPlaybackOffset);
            var playbackOffsetChanged = EditorGUI.EndChangeCheck();
            x += 74f;
            GUI.Label(new Rect(x, offsetRect.y + 2f, 20f, 18f), "초", EditorStyles.miniLabel);
            GUI.Label(
                new Rect(x + 32f, offsetRect.y + 2f, offsetRect.xMax - x - 32f, 18f),
                "앞부분을 건너뛰고 시작",
                EditorStyles.miniLabel);

            var speedRect = new Rect(
                rect.x + 10f,
                rect.y + 167f,
                rect.width - 20f,
                22f);
            GUI.Label(
                new Rect(speedRect.x, speedRect.y + 2f, 78f, 18f),
                new GUIContent("재생 속도", "VFX 내부 시간만 빠르게 또는 느리게 흐르게 합니다."),
                EditorStyles.miniLabel);

            var gaugeExponent = ResolveVfxPlaybackSpeedGaugeExponent(currentPlaybackSpeed);
            var gaugeMinExponent = -gaugeExponent;
            var gaugeMaxExponent = gaugeExponent;
            var gaugeMinSpeed = FromVfxPlaybackSpeedGaugeValue(gaugeMinExponent);
            var gaugeMaxSpeed = FromVfxPlaybackSpeedGaugeValue(gaugeMaxExponent);
            x = speedRect.x + 78f;
            GUI.Label(
                new Rect(x, speedRect.y + 2f, 48f, 18f),
                $"{gaugeMinSpeed:0.##}배",
                EditorStyles.miniLabel);
            x += 54f;
            const float gaugeTrailingWidth = 194f;
            var gaugeRect = new Rect(
                x,
                speedRect.y + 2f,
                Mathf.Max(100f, speedRect.xMax - x - gaugeTrailingWidth),
                18f);
            var changedPlaybackSpeed = currentPlaybackSpeed;
            var playbackSpeedChanged = false;
            EditorGUI.BeginChangeCheck();
            var changedGaugeValue = GUI.HorizontalSlider(
                gaugeRect,
                Mathf.Clamp(
                    ToVfxPlaybackSpeedGaugeValue(currentPlaybackSpeed),
                    gaugeMinExponent,
                    gaugeMaxExponent),
                gaugeMinExponent,
                gaugeMaxExponent);
            if (EditorGUI.EndChangeCheck())
            {
                changedPlaybackSpeed = Mathf.Round(
                    FromVfxPlaybackSpeedGaugeValue(changedGaugeValue) * 100f) / 100f;
                playbackSpeedChanged = true;
            }
            var oneX = Mathf.Lerp(
                gaugeRect.xMin,
                gaugeRect.xMax,
                Mathf.InverseLerp(gaugeMinExponent, gaugeMaxExponent, 0f));
            EditorGUI.DrawRect(
                new Rect(oneX, gaugeRect.yMax - 5f, 1f, 5f),
                new Color(0.75f, 0.75f, 0.75f, 0.8f));

            x = gaugeRect.xMax + 6f;
            GUI.Label(
                new Rect(x, speedRect.y + 2f, 48f, 18f),
                $"{gaugeMaxSpeed:0.##}배",
                EditorStyles.miniLabel);
            x += 54f;
            EditorGUI.BeginChangeCheck();
            var exactPlaybackSpeed = EditorGUI.FloatField(
                new Rect(x, speedRect.y, 62f, speedRect.height),
                changedPlaybackSpeed);
            if (EditorGUI.EndChangeCheck())
            {
                changedPlaybackSpeed = exactPlaybackSpeed;
                playbackSpeedChanged = true;
            }
            x += 62f;
            GUI.Label(new Rect(x, speedRect.y + 2f, 18f, 18f), "배", EditorStyles.miniLabel);
            x += 24f;
            var resetSpeed = GUI.Button(
                new Rect(x, speedRect.y, 48f, speedRect.height),
                "1배",
                EditorStyles.miniButton);
            if (playbackOffsetChanged || playbackSpeedChanged || resetSpeed)
            {
                currentPlaybackOffset = Mathf.Max(0f, changedPlaybackOffset);
                currentPlaybackSpeed = resetSpeed ? 1f : SanitizePlaybackSpeed(changedPlaybackSpeed);
                if (resetSpeed)
                {
                    GUI.FocusControl(null);
                }
                RestartVfxPreview();
            }

            GUI.Label(
                new Rect(rect.x + 10f, rect.y + 202f, rect.width - 20f, 38f),
                "노란 핸들 또는 숫자로 보정합니다. 유지 시간은 사라지는 시점, 시작점은 Prefab 내부에서 건너뛸 앞부분입니다. 속도 게이지는 1배 중심이며 모든 값은 실제 전투에도 동일하게 적용됩니다.",
                EditorStyles.wordWrappedMiniLabel);

            const float buttonWidth = 112f;
            const float gap = 6f;
            var buttonY = rect.yMax - 32f;
            var cancelRect = new Rect(rect.xMax - buttonWidth, buttonY, buttonWidth, 26f);
            var applyRect = new Rect(cancelRect.x - gap - buttonWidth, buttonY, buttonWidth, 26f);
            if (GUI.Button(applyRect, "적용"))
            {
                if (applyVfxValue?.Invoke(
                        currentValue,
                        currentEuler,
                        currentScale,
                        currentLifetime,
                        currentPlaybackOffset,
                        currentPlaybackSpeed) != false)
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

        private static Vector3 DrawCompactVector3Field(
            Rect rect,
            string label,
            Vector3 value)
        {
            const float labelWidth = 78f;
            const float gap = 8f;
            const float axisLabelWidth = 14f;
            GUI.Label(
                new Rect(rect.x, rect.y + 2f, labelWidth, 18f),
                label,
                EditorStyles.miniLabel);

            var fieldStart = rect.x + labelWidth;
            var fieldWidth = Mathf.Max(
                48f,
                (rect.xMax - fieldStart - gap * 2f) / 3f);
            value.x = DrawCompactAxisField(
                new Rect(fieldStart, rect.y, fieldWidth, rect.height),
                "X",
                axisLabelWidth,
                value.x);
            fieldStart += fieldWidth + gap;
            value.y = DrawCompactAxisField(
                new Rect(fieldStart, rect.y, fieldWidth, rect.height),
                "Y",
                axisLabelWidth,
                value.y);
            fieldStart += fieldWidth + gap;
            value.z = DrawCompactAxisField(
                new Rect(fieldStart, rect.y, fieldWidth, rect.height),
                "Z",
                axisLabelWidth,
                value.z);
            return value;
        }

        private static float DrawCompactAxisField(
            Rect rect,
            string axis,
            float axisLabelWidth,
            float value)
        {
            GUI.Label(
                new Rect(rect.x, rect.y + 2f, axisLabelWidth, 18f),
                axis,
                EditorStyles.miniLabel);
            return EditorGUI.FloatField(
                new Rect(
                    rect.x + axisLabelWidth,
                    rect.y,
                    Mathf.Max(1f, rect.width - axisLabelWidth),
                    rect.height),
                value);
        }

        private void SetVfxPreviewPlaying(bool playing)
        {
            vfxPreviewPlaying = playing;
            vfxPreviewLastUpdateTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= UpdateVfxPreview;
            if (playing && IsVfxMode)
            {
                EditorApplication.update += UpdateVfxPreview;
            }
            Repaint();
        }

        private void RestartVfxPreview()
        {
            if (previewVfx == null)
            {
                return;
            }

            MonsterBasicAttackVfxPlayback.RestartAtOffset(
                previewVfx.gameObject,
                currentPlaybackOffset,
                false,
                currentPlaybackSpeed);
            vfxPreviewElapsed = 0f;
            vfxPreviewLastUpdateTime = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private static float SanitizePlaybackSpeed(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 1f
                : Mathf.Max(0.01f, value);
        }

        private static float ResolveVfxPlaybackSpeedGaugeExponent(float speed)
        {
            var exponent = Mathf.Abs(ToVfxPlaybackSpeedGaugeValue(speed));
            return Mathf.Max(
                VfxPlaybackSpeedGaugeDefaultExponent,
                Mathf.Ceil(exponent));
        }

        private static float ToVfxPlaybackSpeedGaugeValue(float speed)
        {
            return Mathf.Log(SanitizePlaybackSpeed(speed), 2f);
        }

        private static float FromVfxPlaybackSpeedGaugeValue(float value)
        {
            return SanitizePlaybackSpeed(Mathf.Pow(2f, value));
        }

        private void UpdateVfxPreview()
        {
            if (!vfxPreviewPlaying || previewVfx == null)
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            var deltaTime = Mathf.Clamp(
                (float)(now - vfxPreviewLastUpdateTime),
                0f,
                0.05f);
            vfxPreviewLastUpdateTime = now;
            if (deltaTime <= 0f)
            {
                return;
            }

            MonsterBasicAttackVfxPlayback.Simulate(
                previewVfx.gameObject,
                deltaTime);
            vfxPreviewElapsed += deltaTime;
            Repaint();
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
                if (IsVfxMode)
                {
                    previewVfx = UnityEngine.Object
                        .Instantiate(previewVfxPrefab, valueAnchor)
                        .transform;
                    previewVfx.name = "VFX Preview";
                    PrepareVfxPreview(previewVfx.gameObject);
                }
                ApplyValueToPreview();
                previewUtility.AddSingleGO(previewRoot);
                modelBounds = CalculateModelBounds(previewRoot);
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

            if (IsVfxMode && previewVfx != null)
            {
                previewVfx.localPosition = currentValue;
                previewVfx.localRotation = Quaternion.Euler(currentEuler);
                MonsterBasicAttackVfxPlayback.ApplyInstanceScale(
                    previewVfx.gameObject,
                    previewVfxPrefab.transform.localScale *
                    currentScale *
                    Mathf.Max(0.01f, draft.VfxScale));
            }
            else if (binding.ValueMode == MonsterMakerPreviewPositionValueMode.VisualLocal && previewVisual != null)
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

        private bool IsVfxMode => previewVfxPrefab != null && applyVfxValue != null;

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

        private static void PrepareVfxPreview(GameObject root)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < transforms.Length; index++)
            {
                transforms[index].gameObject.hideFlags = HideFlags.HideAndDontSave;
            }

            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (var index = 0; index < behaviours.Length; index++)
            {
                behaviours[index].enabled = false;
            }
            var audioSources = root.GetComponentsInChildren<AudioSource>(true);
            for (var index = 0; index < audioSources.Length; index++)
            {
                audioSources[index].enabled = false;
            }
            var cameras = root.GetComponentsInChildren<Camera>(true);
            for (var index = 0; index < cameras.Length; index++)
            {
                cameras[index].enabled = false;
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
            EditorApplication.update -= UpdateVfxPreview;
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
            previewVfx = null;
            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }
        }
    }
}
