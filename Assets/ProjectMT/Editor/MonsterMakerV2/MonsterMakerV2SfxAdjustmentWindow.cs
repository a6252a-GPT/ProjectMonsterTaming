using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    internal sealed class MonsterMakerV2SfxAdjustmentWindow : EditorWindow // 한 SFX만 집중 조절하는 V2 보정 창
    {
        private const string LayoutPath =
            "Assets/ProjectMT/Editor/MonsterMakerV2/UI/MonsterMakerV2SfxAdjustmentWindow.uxml";
        private const string StylePath =
            "Assets/ProjectMT/Editor/MonsterMakerV2/UI/MonsterMakerV2SfxAdjustmentWindow.uss";
        private const float MinimumDuration = 0.001f;
        private static readonly Vector2 WindowSize = new Vector2(600f, 610f);

        private AudioClip clip;
        private string sourceLabel;
        private float initialVolume = 1f;
        private float currentVolume = 1f;
        private float initialStartOffset;
        private float initialEndCut;
        private bool initialOverridePitch;
        private float initialPitch = 1f;
        private float currentStartOffset;
        private float currentEndCut;
        private bool currentOverridePitch;
        private float currentPitch = 1f;
        private Func<float, float, float, bool, float, bool> apply;
        private bool updatingUi;

        private Label clipNameLabel;
        private Label clipInfoLabel;
        private Label rangeStatusLabel;
        private Label volumeStatusLabel;
        private Label pitchStatusLabel;
        private Slider volumeSlider;
        private FloatField volumeField;
        private HelpBox volumeHelp;
        private MinMaxSlider rangeSlider;
        private FloatField startOffsetField;
        private FloatField endCutField;
        private Toggle pitchOverrideToggle;
        private VisualElement pitchControls;
        private Slider pitchSlider;
        private FloatField pitchField;
        private HelpBox pitchHelp;

        internal static void Open(
            EditorWindow owner,
            string label,
            AudioClip sourceClip,
            float volume,
            float startOffset,
            float endCut,
            bool overridePitch,
            float pitch,
            Func<float, float, float, bool, float, bool> onApply)
        {
            if (sourceClip == null || sourceClip.length <= MinimumDuration || onApply == null)
            {
                return;
            }

            var window = CreateInstance<MonsterMakerV2SfxAdjustmentWindow>();
            window.titleContent = new GUIContent("V2 SFX 보정 · " + label);
            window.minSize = WindowSize;
            window.maxSize = WindowSize;
            window.Initialize(
                label,
                sourceClip,
                volume,
                startOffset,
                endCut,
                overridePitch,
                pitch,
                onApply);
            PlaceAndShow(window, owner);
        }

        private void Initialize(
            string label,
            AudioClip sourceClip,
            float volume,
            float startOffset,
            float endCut,
            bool overridePitch,
            float pitch,
            Func<float, float, float, bool, float, bool> onApply)
        {
            sourceLabel = string.IsNullOrWhiteSpace(label) ? "SFX" : label.Trim();
            clip = sourceClip;
            currentVolume = SanitizeVolume(volume);
            initialVolume = currentVolume;
            apply = onApply;
            currentEndCut = SanitizeTime(endCut);
            currentStartOffset = Mathf.Clamp(
                SanitizeTime(startOffset),
                0f,
                Mathf.Max(0f, clip.length - currentEndCut - MinimumDuration));
            currentEndCut = Mathf.Clamp(
                currentEndCut,
                0f,
                Mathf.Max(0f, clip.length - currentStartOffset - MinimumDuration));
            currentOverridePitch = overridePitch;
            currentPitch = SanitizePitch(pitch);
            initialStartOffset = currentStartOffset;
            initialEndCut = currentEndCut;
            initialOverridePitch = currentOverridePitch;
            initialPitch = currentPitch;
        }

        private static void PlaceAndShow(MonsterMakerV2SfxAdjustmentWindow window, EditorWindow owner)
        {
            var ownerRect = owner == null
                ? new Rect(100f, 100f, WindowSize.x, WindowSize.y)
                : owner.position;
            window.ShowUtility();
            window.position = new Rect(
                ownerRect.center.x - WindowSize.x * 0.5f,
                ownerRect.center.y - WindowSize.y * 0.5f,
                WindowSize.x,
                WindowSize.y);
            window.Focus();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            if (clip == null || apply == null)
            {
                // Utility 창의 콜백은 도메인 리로드를 넘겨 직렬화할 수 없다.
                // 컴파일 전에 열려 있던 낡은 창은 오류를 내지 않고 닫고 카드에서 다시 열게 한다.
                rootVisualElement.schedule.Execute(Close);
                return;
            }

            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (layout == null || style == null)
            {
                rootVisualElement.Add(new HelpBox("SFX 보정 창 UI 자산을 찾지 못했습니다.", HelpBoxMessageType.Error));
                return;
            }

            rootVisualElement.styleSheets.Add(style);
            layout.CloneTree(rootVisualElement);
            rootVisualElement.Q<Label>("adjust-title").text = "SFX 보정 · " + sourceLabel;
            clipNameLabel = rootVisualElement.Q<Label>("clip-name");
            clipInfoLabel = rootVisualElement.Q<Label>("clip-info");
            rangeStatusLabel = rootVisualElement.Q<Label>("range-status");
            volumeStatusLabel = rootVisualElement.Q<Label>("volume-status");
            pitchStatusLabel = rootVisualElement.Q<Label>("pitch-status");
            volumeSlider = rootVisualElement.Q<Slider>("volume-slider");
            volumeField = rootVisualElement.Q<FloatField>("volume-field");
            volumeHelp = rootVisualElement.Q<HelpBox>("volume-help");
            rangeSlider = rootVisualElement.Q<MinMaxSlider>("range-slider");
            startOffsetField = rootVisualElement.Q<FloatField>("start-offset-field");
            endCutField = rootVisualElement.Q<FloatField>("end-cut-field");
            pitchOverrideToggle = rootVisualElement.Q<Toggle>("pitch-override");
            pitchControls = rootVisualElement.Q<VisualElement>("pitch-controls");
            pitchSlider = rootVisualElement.Q<Slider>("pitch-slider");
            pitchField = rootVisualElement.Q<FloatField>("pitch-field");
            pitchHelp = rootVisualElement.Q<HelpBox>("pitch-help");

            startOffsetField.isDelayed = true;
            endCutField.isDelayed = true;
            volumeField.isDelayed = false;
            pitchField.isDelayed = false;
            rangeSlider.lowLimit = 0f;
            rangeSlider.highLimit = clip.length;
            volumeSlider.lowValue = 0f;
            volumeSlider.highValue = 2f;
            pitchSlider.lowValue = 0.5f;
            pitchSlider.highValue = 2f;

            BindControls();
            RefreshControls();
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);
            rootVisualElement.schedule.Execute(PreviewCurrentRange);
        }

        private void BindControls()
        {
            volumeSlider.RegisterValueChangedCallback(evt =>
            {
                if (updatingUi) return;
                currentVolume = Mathf.Round(SanitizeVolume(evt.newValue) * 100f) / 100f;
                RefreshControls();
                PreviewCurrentRange();
            });
            volumeField.RegisterValueChangedCallback(evt =>
            {
                if (updatingUi) return;
                currentVolume = SanitizeVolume(evt.newValue / 100f);
                RefreshControls();
                PreviewCurrentRange();
            });
            rangeSlider.RegisterValueChangedCallback(evt =>
            {
                if (updatingUi) return;
                currentStartOffset = Mathf.Clamp(evt.newValue.x, 0f, clip.length - MinimumDuration);
                var end = Mathf.Clamp(evt.newValue.y, currentStartOffset + MinimumDuration, clip.length);
                currentEndCut = clip.length - end;
                RefreshControls();
            });
            startOffsetField.RegisterValueChangedCallback(evt =>
            {
                if (updatingUi) return;
                currentStartOffset = Mathf.Clamp(
                    SanitizeTime(evt.newValue),
                    0f,
                    Mathf.Max(0f, clip.length - currentEndCut - MinimumDuration));
                RefreshControls();
            });
            endCutField.RegisterValueChangedCallback(evt =>
            {
                if (updatingUi) return;
                currentEndCut = Mathf.Clamp(
                    SanitizeTime(evt.newValue),
                    0f,
                    Mathf.Max(0f, clip.length - currentStartOffset - MinimumDuration));
                RefreshControls();
            });
            pitchOverrideToggle.RegisterValueChangedCallback(evt =>
            {
                if (updatingUi) return;
                currentOverridePitch = evt.newValue;
                RefreshControls();
                PreviewCurrentRange();
            });
            pitchSlider.RegisterValueChangedCallback(evt =>
            {
                if (updatingUi) return;
                currentPitch = Mathf.Round(SanitizePitch(evt.newValue) * 100f) / 100f;
                RefreshControls();
                PreviewCurrentRange();
            });
            pitchField.RegisterValueChangedCallback(evt =>
            {
                if (updatingUi) return;
                currentPitch = SanitizePitch(evt.newValue);
                RefreshControls();
                PreviewCurrentRange();
            });

            rootVisualElement.Q<Button>("preview-button").clicked += PreviewCurrentRange;
            rootVisualElement.Q<Button>("stop-button").clicked += SfxEditorAudioPreview.StopAll;
            rootVisualElement.Q<Button>("ping-button").clicked += () => EditorGUIUtility.PingObject(clip);
            rootVisualElement.Q<Button>("defaults-button").clicked += ResetToDefaults;
            rootVisualElement.Q<Button>("reset-button").clicked += ResetToInitialValues;
            rootVisualElement.Q<Button>("cancel-button").clicked += Close;
            rootVisualElement.Q<Button>("apply-button").clicked += ApplyAndClose;
        }

        private void RefreshControls()
        {
            var end = clip.length - currentEndCut;
            var duration = Mathf.Max(0f, end - currentStartOffset);
            updatingUi = true;
            try
            {
                clipNameLabel.text = clip.name;
                clipInfoLabel.text = $"원본 {clip.length:0.###}초 · {clip.frequency:N0} Hz · {clip.channels}ch";
                volumeSlider.SetValueWithoutNotify(currentVolume);
                volumeField.SetValueWithoutNotify(currentVolume * 100f);
                volumeStatusLabel.text = $"{currentVolume * 100f:0}%";
                volumeHelp.text = currentVolume > 1f
                    ? $"현재 {currentVolume * 100f:0}% 증폭 중입니다. 원본에 따라 클리핑이 생기면 낮춰 주세요."
                    : "100%를 넘기면 약한 원본을 최대 200%까지 증폭합니다.";
                rangeStatusLabel.text =
                    $"재생 구간 {currentStartOffset:0.###}초 — {end:0.###}초  ·  길이 {duration:0.###}초";
                rangeSlider.SetValueWithoutNotify(new Vector2(currentStartOffset, end));
                startOffsetField.SetValueWithoutNotify(currentStartOffset);
                endCutField.SetValueWithoutNotify(currentEndCut);
                pitchOverrideToggle.SetValueWithoutNotify(currentOverridePitch);
                pitchControls.SetEnabled(currentOverridePitch);
                pitchSlider.SetValueWithoutNotify(currentPitch);
                pitchField.SetValueWithoutNotify(currentPitch);
                pitchStatusLabel.text = currentOverridePitch
                    ? $"고정 {currentPitch:0.##}배"
                    : "기존 랜덤 변주 유지";
                pitchHelp.text = currentOverridePitch
                    ? $"전투용 Cue와 현재 미리듣기에 {currentPitch:0.##}배 고정 피치가 함께 적용됩니다."
                    : "직접 지정을 끄면 Maker Cue의 약한 랜덤 피치 변주(0.98~1.02배)를 그대로 유지합니다.";
            }
            finally
            {
                updatingUi = false;
            }
        }

        private void PreviewCurrentRange()
        {
            SfxEditorAudioPreview.StopAll();
            SfxEditorAudioPreview.PlaySegment(
                clip,
                currentStartOffset,
                currentEndCut,
                currentVolume,
                currentOverridePitch ? currentPitch : 1f);
        }

        private void ResetToDefaults()
        {
            currentStartOffset = 0f;
            currentEndCut = 0f;
            currentVolume = 1f;
            currentOverridePitch = false;
            currentPitch = 1f;
            RefreshControls();
            PreviewCurrentRange();
        }

        private void ResetToInitialValues()
        {
            currentStartOffset = initialStartOffset;
            currentEndCut = initialEndCut;
            currentVolume = initialVolume;
            currentOverridePitch = initialOverridePitch;
            currentPitch = initialPitch;
            RefreshControls();
            PreviewCurrentRange();
        }

        private void ApplyAndClose()
        {
            var applied = apply?.Invoke(
                currentVolume,
                currentStartOffset,
                currentEndCut,
                currentOverridePitch,
                currentPitch) != false;
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
            SfxEditorAudioPreview.StopAll();
            apply = null;
            clip = null;
        }

        private static float SanitizeTime(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
        }

        private static float SanitizePitch(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp(value, 0.5f, 2f);
        }

        private static float SanitizeVolume(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp(value, 0f, 2f);
        }
    }
}
