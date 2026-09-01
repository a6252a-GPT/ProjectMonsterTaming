using System;
using ProjectMT.EditorTools.MonsterMaker;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    internal sealed partial class MonsterMakerV2AdjustmentWindow : EditorWindow // V2 전용 좌표·VFX 보정 창
    {
        private const string LayoutPath =
            "Assets/ProjectMT/Editor/MonsterMakerV2/UI/MonsterMakerV2AdjustmentWindow.uxml";
        private static readonly Vector2 PositionWindowSize = new Vector2(780f, 700f);
        private static readonly Vector2 VfxWindowSize = new Vector2(860f, 830f);

        private Func<Vector3, bool> applyPosition;
        private Func<Vector3, Vector3, float, float, float, float, bool> applyVfx;
        private Vector3 initialPosition;
        private Vector3 initialEuler;
        private float initialScale = 1f;
        private float initialLifetime = 1f;
        private float initialPlaybackOffset;
        private float initialPlaybackSpeed = 1f;
        private bool updatingUi;

        private Label titleLabel;
        private Label captionLabel;
        private Label playbackStatusLabel;
        private Label speedMinLabel;
        private Label speedMaxLabel;
        private VisualElement positionControls;
        private VisualElement vfxControls;
        private Vector3Field positionField;
        private Vector3Field vfxPositionField;
        private Vector3Field vfxEulerField;
        private FloatField vfxScaleField;
        private FloatField vfxLifetimeField;
        private FloatField vfxOffsetField;
        private Slider vfxSpeedSlider;
        private FloatField vfxSpeedField;
        private Button playPauseButton;
        private IMGUIContainer previewIMGUI;

        internal static bool CanOpen(MonsterMakerDraft source)
        {
            return source != null && source.VendorPrefab != null;
        }

        internal static void OpenPosition(
            EditorWindow owner,
            MonsterMakerDraft source,
            MonsterMakerPreviewPositionBinding positionBinding,
            Vector3 value,
            Func<Vector3, bool> onApply)
        {
            if (!CanOpen(source) || positionBinding == null || onApply == null)
            {
                return;
            }

            var window = CreateInstance<MonsterMakerV2AdjustmentWindow>();
            window.titleContent = new GUIContent("V2 좌표 보정 · " + positionBinding.Label);
            window.minSize = PositionWindowSize;
            window.InitializePosition(source, positionBinding, value, onApply);
            PlaceAndShow(window, owner, PositionWindowSize);
        }

        internal static void OpenVfx(
            EditorWindow owner,
            MonsterMakerDraft source,
            MonsterMakerPreviewPositionBinding positionBinding,
            GameObject vfxPrefab,
            Vector3 position,
            Vector3 euler,
            float scale,
            float lifetime,
            float playbackOffset,
            float playbackSpeed,
            Func<Vector3, Vector3, float, float, float, float, bool> onApply)
        {
            if (!CanOpen(source) || positionBinding == null || vfxPrefab == null || onApply == null)
            {
                return;
            }

            var window = CreateInstance<MonsterMakerV2AdjustmentWindow>();
            window.titleContent = new GUIContent("V2 VFX 보정 · " + positionBinding.Label);
            window.minSize = VfxWindowSize;
            window.InitializeVfx(
                source,
                positionBinding,
                vfxPrefab,
                position,
                euler,
                scale,
                lifetime,
                playbackOffset,
                playbackSpeed,
                onApply);
            PlaceAndShow(window, owner, VfxWindowSize);
        }

        private static void PlaceAndShow(
            MonsterMakerV2AdjustmentWindow window,
            EditorWindow owner,
            Vector2 size)
        {
            var ownerRect = owner == null
                ? new Rect(100f, 100f, size.x, size.y)
                : owner.position;
            window.ShowUtility();
            window.position = new Rect(
                ownerRect.center.x - size.x * 0.5f,
                ownerRect.center.y - size.y * 0.5f,
                size.x,
                size.y);
            window.Focus();
        }

        private void InitializePosition(
            MonsterMakerDraft source,
            MonsterMakerPreviewPositionBinding positionBinding,
            Vector3 value,
            Func<Vector3, bool> onApply)
        {
            draft = source;
            binding = positionBinding;
            currentPosition = value;
            initialPosition = value;
            applyPosition = onApply;
            BuildPreview();
        }

        private void InitializeVfx(
            MonsterMakerDraft source,
            MonsterMakerPreviewPositionBinding positionBinding,
            GameObject vfxPrefab,
            Vector3 position,
            Vector3 euler,
            float scale,
            float lifetime,
            float playbackOffset,
            float playbackSpeed,
            Func<Vector3, Vector3, float, float, float, float, bool> onApply)
        {
            draft = source;
            binding = positionBinding;
            previewVfxPrefab = vfxPrefab;
            currentPosition = position;
            currentEuler = euler;
            currentScale = Mathf.Max(0.01f, scale);
            currentLifetime = Mathf.Max(0.01f, lifetime);
            currentPlaybackOffset = Mathf.Max(0f, playbackOffset);
            currentPlaybackSpeed = SanitizePlaybackSpeed(playbackSpeed);
            initialPosition = currentPosition;
            initialEuler = currentEuler;
            initialScale = currentScale;
            initialLifetime = currentLifetime;
            initialPlaybackOffset = currentPlaybackOffset;
            initialPlaybackSpeed = currentPlaybackSpeed;
            applyVfx = onApply;
            BuildPreview();
            RestartVfxPreview();
            SetVfxPreviewPlaying(true);
        }

        public void CreateGUI()
        {
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            rootVisualElement.Clear();
            if (layout == null)
            {
                rootVisualElement.Add(new HelpBox("V2 보정 창 UXML을 찾지 못했습니다.", HelpBoxMessageType.Error));
                return;
            }

            layout.CloneTree(rootVisualElement);
            var adjustmentHelp = rootVisualElement.Q<HelpBox>(className: "adjust-help");
            if (adjustmentHelp != null)
                adjustmentHelp.style.display = MonsterMakerV2HelpPreferences.ShowContextHelp
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            titleLabel = rootVisualElement.Q<Label>("adjust-title");
            captionLabel = rootVisualElement.Q<Label>("adjust-caption");
            playbackStatusLabel = rootVisualElement.Q<Label>("vfx-playback-status");
            speedMinLabel = rootVisualElement.Q<Label>("speed-min-label");
            speedMaxLabel = rootVisualElement.Q<Label>("speed-max-label");
            positionControls = rootVisualElement.Q<VisualElement>("position-controls");
            vfxControls = rootVisualElement.Q<VisualElement>("vfx-controls");
            positionField = rootVisualElement.Q<Vector3Field>("position-field");
            vfxPositionField = rootVisualElement.Q<Vector3Field>("vfx-position-field");
            vfxEulerField = rootVisualElement.Q<Vector3Field>("vfx-euler-field");
            vfxScaleField = rootVisualElement.Q<FloatField>("vfx-scale-field");
            vfxLifetimeField = rootVisualElement.Q<FloatField>("vfx-lifetime-field");
            vfxOffsetField = rootVisualElement.Q<FloatField>("vfx-offset-field");
            vfxSpeedSlider = rootVisualElement.Q<Slider>("vfx-speed-slider");
            vfxSpeedField = rootVisualElement.Q<FloatField>("vfx-speed-field");
            playPauseButton = rootVisualElement.Q<Button>("vfx-play-pause");

            // 숫자를 지우고 다시 입력하는 중간 상태를 최소값으로 덮어쓰지 않는다.
            // Enter 또는 포커스 이탈 시에만 아래 변경 콜백이 실행되어 유효 범위를 확정한다.
            if (vfxScaleField != null) vfxScaleField.isDelayed = true;
            if (vfxLifetimeField != null) vfxLifetimeField.isDelayed = true;
            if (vfxOffsetField != null) vfxOffsetField.isDelayed = true;
            if (vfxSpeedField != null) vfxSpeedField.isDelayed = true;

            var previewHost = rootVisualElement.Q<VisualElement>("adjust-preview-host");
            previewIMGUI = new IMGUIContainer(DrawPreviewGUI)
            {
                name = "adjust-preview-imgui",
                focusable = true
            };
            previewIMGUI.style.flexGrow = 1f;
            previewHost?.Add(previewIMGUI);

            BindControls();
            RefreshAllControls();
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        private void BindControls()
        {
            positionField?.RegisterValueChangedCallback(evt =>
            {
                if (updatingUi) return;
                currentPosition = evt.newValue;
                ApplyValueToPreview();
                MarkPreviewDirty();
            });
            vfxPositionField?.RegisterValueChangedCallback(evt =>
            {
                if (updatingUi) return;
                currentPosition = evt.newValue;
                ApplyValueToPreview();
                MarkPreviewDirty();
            });
            vfxEulerField?.RegisterValueChangedCallback(evt =>
            {
                if (updatingUi) return;
                currentEuler = evt.newValue;
                ApplyValueToPreview();
                MarkPreviewDirty();
            });
            vfxScaleField?.RegisterValueChangedCallback(evt =>
            {
                if (updatingUi) return;
                currentScale = Mathf.Max(0.01f, evt.newValue);
                vfxScaleField.SetValueWithoutNotify(currentScale);
                ApplyValueToPreview();
                MarkPreviewDirty();
            });
            vfxLifetimeField?.RegisterValueChangedCallback(evt =>
            {
                if (updatingUi) return;
                currentLifetime = Mathf.Max(0.01f, evt.newValue);
                vfxLifetimeField.SetValueWithoutNotify(currentLifetime);
            });
            vfxOffsetField?.RegisterValueChangedCallback(evt =>
            {
                if (updatingUi) return;
                currentPlaybackOffset = Mathf.Max(0f, evt.newValue);
                vfxOffsetField.SetValueWithoutNotify(currentPlaybackOffset);
                RestartVfxPreview();
                RefreshPlaybackControls();
            });
            vfxSpeedSlider?.RegisterValueChangedCallback(evt =>
            {
                if (updatingUi) return;
                currentPlaybackSpeed = Mathf.Round(FromPlaybackSpeedGauge(evt.newValue) * 100f) / 100f;
                RestartVfxPreview();
                RefreshPlaybackControls();
            });
            vfxSpeedField?.RegisterValueChangedCallback(evt =>
            {
                if (updatingUi) return;
                currentPlaybackSpeed = SanitizePlaybackSpeed(evt.newValue);
                RestartVfxPreview();
                RefreshPlaybackControls();
            });

            if (playPauseButton != null)
            {
                playPauseButton.clicked += () => SetVfxPreviewPlaying(!vfxPreviewPlaying);
            }
            var restartButton = rootVisualElement.Q<Button>("vfx-restart");
            if (restartButton != null) restartButton.clicked += RestartVfxPreview;
            var speedReset = rootVisualElement.Q<Button>("vfx-speed-reset");
            if (speedReset != null)
            {
                speedReset.clicked += () =>
                {
                    currentPlaybackSpeed = 1f;
                    RestartVfxPreview();
                    RefreshPlaybackControls();
                };
            }
            var reset = rootVisualElement.Q<Button>("reset-button");
            if (reset != null) reset.clicked += ResetToInitialValues;
            var cancel = rootVisualElement.Q<Button>("cancel-button");
            if (cancel != null) cancel.clicked += Close;
            var apply = rootVisualElement.Q<Button>("apply-button");
            if (apply != null) apply.clicked += ApplyAndClose;
        }

        private void RefreshAllControls()
        {
            updatingUi = true;
            try
            {
                if (titleLabel != null)
                {
                    titleLabel.text = IsVfxMode
                        ? $"VFX 보정 · {binding?.Label}"
                        : $"좌표 보정 · {binding?.Label}";
                }
                if (captionLabel != null)
                {
                    captionLabel.text = IsVfxMode
                        ? "위치·회전·크기·수명·재생 시작점과 속도를 한 번에 확인합니다."
                        : "노란 핸들과 정확한 숫자 입력을 함께 사용할 수 있습니다.";
                }
                if (positionControls != null)
                    positionControls.style.display = IsVfxMode ? DisplayStyle.None : DisplayStyle.Flex;
                if (vfxControls != null)
                    vfxControls.style.display = IsVfxMode ? DisplayStyle.Flex : DisplayStyle.None;
                positionField?.SetValueWithoutNotify(currentPosition);
                vfxPositionField?.SetValueWithoutNotify(currentPosition);
                vfxEulerField?.SetValueWithoutNotify(currentEuler);
                vfxScaleField?.SetValueWithoutNotify(currentScale);
                vfxLifetimeField?.SetValueWithoutNotify(currentLifetime);
                vfxOffsetField?.SetValueWithoutNotify(currentPlaybackOffset);
            }
            finally
            {
                updatingUi = false;
            }
            RefreshPlaybackControls();
        }

        private void RefreshPlaybackControls()
        {
            if (!IsVfxMode) return;
            var exponent = ResolvePlaybackSpeedGaugeExponent(currentPlaybackSpeed);
            updatingUi = true;
            try
            {
                if (vfxSpeedSlider != null)
                {
                    vfxSpeedSlider.lowValue = -exponent;
                    vfxSpeedSlider.highValue = exponent;
                    vfxSpeedSlider.SetValueWithoutNotify(
                        Mathf.Clamp(ToPlaybackSpeedGauge(currentPlaybackSpeed), -exponent, exponent));
                }
                vfxSpeedField?.SetValueWithoutNotify(currentPlaybackSpeed);
                if (speedMinLabel != null) speedMinLabel.text = $"{FromPlaybackSpeedGauge(-exponent):0.##}배";
                if (speedMaxLabel != null) speedMaxLabel.text = $"{FromPlaybackSpeedGauge(exponent):0.##}배";
                if (playPauseButton != null)
                    playPauseButton.text = vfxPreviewPlaying ? "Ⅱ 일시정지" : "▶ 재생";
                UpdatePlaybackStatusLabel();
            }
            finally
            {
                updatingUi = false;
            }
        }

        private void UpdatePlaybackStatusLabel()
        {
            if (playbackStatusLabel != null)
            {
                playbackStatusLabel.text =
                    $"원본 {currentPlaybackOffset + vfxPreviewElapsed * currentPlaybackSpeed:0.00}초 · " +
                    $"{currentPlaybackSpeed:0.##}배";
            }
        }

        private void ResetToInitialValues()
        {
            currentPosition = initialPosition;
            currentEuler = initialEuler;
            currentScale = initialScale;
            currentLifetime = initialLifetime;
            currentPlaybackOffset = initialPlaybackOffset;
            currentPlaybackSpeed = initialPlaybackSpeed;
            ApplyValueToPreview();
            if (IsVfxMode) RestartVfxPreview();
            RefreshAllControls();
            MarkPreviewDirty();
        }

        private void ApplyAndClose()
        {
            var applied = IsVfxMode
                ? applyVfx?.Invoke(
                    currentPosition,
                    currentEuler,
                    currentScale,
                    currentLifetime,
                    currentPlaybackOffset,
                    currentPlaybackSpeed) != false
                : applyPosition?.Invoke(currentPosition) != false;
            if (applied)
            {
                Close();
                return;
            }

            ShowNotification(new GUIContent("현재 상태에서는 적용할 수 없습니다."));
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape) return;
            evt.StopImmediatePropagation();
            Close();
        }

        private void OnDisable()
        {
            CleanupPreview();
            applyPosition = null;
            applyVfx = null;
            previewVfxPrefab = null;
        }

        internal static float SanitizePlaybackSpeed(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 1f
                : Mathf.Max(0.01f, value);
        }

        internal static float ResolvePlaybackSpeedGaugeExponent(float speed)
        {
            return Mathf.Max(1f, Mathf.Ceil(Mathf.Abs(ToPlaybackSpeedGauge(speed))));
        }

        internal static float ToPlaybackSpeedGauge(float speed)
        {
            return Mathf.Log(SanitizePlaybackSpeed(speed), 2f);
        }

        internal static float FromPlaybackSpeedGauge(float value)
        {
            return SanitizePlaybackSpeed(Mathf.Pow(2f, value));
        }
    }
}
