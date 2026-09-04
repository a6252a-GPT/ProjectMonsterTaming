using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Audio;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.Audio
{
    public sealed partial class SfxManagerWindow
    {
        private int previewSequence;

        private void RefreshDetails()
        {
            if (detailHost == null)
            {
                return;
            }

            detailHost.Clear();
            if (workspaceMode == SfxWorkspaceMode.Spaces)
            {
                detailHost.Add(selectedSpace == null
                    ? BuildNoSpaceSelectionState()
                    : BuildSelectedSpaceDetails());
                return;
            }

            if (selectedCue == null)
            {
                detailHost.Add(BuildNoSelectionState());
                return;
            }

            detailHost.Add(BuildSelectedCueDetails());
        }

        private VisualElement BuildNoSelectionState()
        {
            var empty = new VisualElement();
            empty.AddToClassList("sfx-detail-empty");
            var icon = new Label("♫");
            icon.AddToClassList("sfx-detail-empty-icon");
            var title = new Label("편집할 Cue를 선택하세요");
            title.AddToClassList("sfx-detail-empty-title");
            var description = new Label("왼쪽에서 영역을 고르고 Cue를 선택하면\n사운드와 재생 설정을 바로 편집할 수 있습니다.");
            description.AddToClassList("sfx-detail-empty-description");
            empty.Add(icon);
            empty.Add(title);
            empty.Add(description);
            return empty;
        }

        private VisualElement BuildSelectedCueDetails()
        {
            var root = new VisualElement();
            root.AddToClassList("sfx-details");
            root.Add(BuildDetailHeader());

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.name = "sfx-detail-scroll";
            scroll.AddToClassList("sfx-detail-scroll");
            scroll.Add(BuildSourceSection());
            scroll.Add(BuildClipSection());
            scroll.Add(BuildVolumeSection());
            scroll.Add(BuildAdvancedSection());
            root.Add(scroll);
            return root;
        }

        private VisualElement BuildDetailHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("sfx-detail-header");

            var text = new VisualElement();
            text.AddToClassList("sfx-detail-title-area");
            var eyebrow = new Label("SELECTED CUE");
            eyebrow.AddToClassList("sfx-detail-eyebrow");
            var title = new Label(selectedCue.name);
            title.AddToClassList("sfx-detail-title");
            var path = new Label(BuildCompactPath(AssetDatabase.GetAssetPath(selectedCue)));
            path.AddToClassList("sfx-detail-path");
            text.Add(eyebrow);
            text.Add(title);
            text.Add(path);
            header.Add(text);

            var actions = new VisualElement();
            actions.AddToClassList("sfx-detail-actions");
            var play = new Button(() => PreviewCue(selectedCue)) { text = "▶ 미리듣기" };
            play.name = "preview-selected-sfx";
            play.SetEnabled(selectedCue.HasPlayableClip);
            play.AddToClassList("sfx-preview-button");
            var stop = new Button(StopPreview) { text = "■" };
            stop.tooltip = "미리듣기 정지";
            stop.AddToClassList("sfx-icon-button");
            actions.Add(play);
            actions.Add(stop);
            header.Add(actions);
            return header;
        }

        private VisualElement BuildSourceSection()
        {
            var section = MakeSection("분류와 원본", "Cue 파일은 이동하지 않고 카탈로그 분류만 바뀝니다.");
            var entry = FindSelectedEntry();
            var categories = catalog.Categories.Where(category => category != null).ToArray();
            var labels = categories.Select(category => category.DisplayName).ToList();
            var selectedIndex = Mathf.Max(0, Array.FindIndex(categories, category => category.Id == entry?.CategoryId));
            var categoryField = new DropdownField("영역", labels, selectedIndex);
            categoryField.name = "sfx-category-field";
            categoryField.AddToClassList("sfx-field");
            categoryField.RegisterValueChangedCallback(evt =>
            {
                var nextIndex = labels.IndexOf(evt.newValue);
                if (nextIndex >= 0 && nextIndex < categories.Length)
                {
                    AssignCategory(categories[nextIndex].Id);
                }
            });
            section.Add(categoryField);

            var originRow = new VisualElement();
            originRow.AddToClassList("sfx-origin-row");
            var mainAsset = AssetDatabase.IsMainAsset(selectedCue);
            var origin = new Label(mainAsset ? "독립 Cue 자산" : "제작 자산 내부 Cue");
            origin.AddToClassList(mainAsset ? "sfx-origin-badge" : "sfx-origin-badge--generated");
            var dirty = new Label(EditorUtility.IsDirty(selectedCue) ? "저장 대기" : "저장됨");
            dirty.AddToClassList(EditorUtility.IsDirty(selectedCue) ? "sfx-dirty-badge" : "sfx-saved-badge");
            originRow.Add(origin);
            originRow.Add(dirty);
            section.Add(originRow);

            var projectButton = new Button(() =>
            {
                Selection.activeObject = selectedCue;
                EditorGUIUtility.PingObject(selectedCue);
                SetStatus($"Project 창에서 {selectedCue.name}을 표시했습니다.");
            }) { text = "Project에서 Cue 위치 표시" };
            projectButton.AddToClassList("sfx-secondary-button");
            section.Add(projectButton);

            if (!mainAsset)
            {
                section.Add(MakeNotice(
                    "이 Cue는 제작소가 만든 내부 자산입니다. 여기서 조절할 수 있지만 제작소에서 원본을 다시 저장하면 일부 값이 다시 생성될 수 있습니다.",
                    true));
            }

            return section;
        }

        private VisualElement BuildClipSection()
        {
            var count = selectedCue.Clips?.Count ?? 0;
            var section = MakeSection(
                $"사운드 원본  ·  {CountPlayableClips(selectedCue)}개",
                "여러 클립은 재생할 때 하나가 무작위로 선택됩니다.");

            if (count == 0)
            {
                section.Add(MakeNotice("아직 연결된 사운드가 없습니다. 아래 칸이나 점선 영역에 AudioClip을 넣어주세요.", true));
            }
            else
            {
                for (var index = 0; index < count; index++)
                {
                    section.Add(BuildClipRow(index, selectedCue.Clips[index]));
                }
            }

            var addField = new ObjectField("사운드 추가")
            {
                name = "add-sfx-audio-clip",
                objectType = typeof(AudioClip),
                allowSceneObjects = false
            };
            addField.AddToClassList("sfx-field");
            addField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is AudioClip clip)
                {
                    AddClips(new[] { clip });
                    addField.schedule.Execute(() => addField.SetValueWithoutNotify(null));
                }
            });
            section.Add(addField);

            var drop = new VisualElement { name = "sfx-audio-drop-zone" };
            drop.AddToClassList("sfx-drop-zone");
            var dropTitle = new Label("AudioClip을 여기에 드래그");
            dropTitle.AddToClassList("sfx-drop-title");
            var dropDescription = new Label("여러 개를 한 번에 추가할 수 있습니다");
            dropDescription.AddToClassList("sfx-drop-description");
            drop.Add(dropTitle);
            drop.Add(dropDescription);
            RegisterClipDropZone(drop);
            section.Add(drop);

            var unlinkNote = new Label("× 버튼은 Cue 연결만 해제합니다. 원본 오디오 파일은 삭제하지 않습니다.");
            unlinkNote.AddToClassList("sfx-safe-note");
            section.Add(unlinkNote);
            return section;
        }

        private VisualElement BuildClipRow(int index, AudioClip clip)
        {
            var row = new VisualElement();
            row.AddToClassList("sfx-clip-row");

            var number = new Label((index + 1).ToString("00"));
            number.AddToClassList("sfx-clip-number");
            row.Add(number);

            var field = new ObjectField
            {
                objectType = typeof(AudioClip),
                allowSceneObjects = false
            };
            field.SetValueWithoutNotify(clip);
            field.AddToClassList("sfx-clip-field");
            field.RegisterValueChangedCallback(evt => ReplaceClip(index, evt.newValue as AudioClip));
            row.Add(field);

            var play = new Button(() => PreviewClip(clip, selectedCue)) { text = "▶" };
            play.tooltip = "이 클립만 미리듣기";
            play.SetEnabled(clip != null);
            play.AddToClassList("sfx-clip-action");
            row.Add(play);

            var up = new Button(() => MoveClip(index, index - 1)) { text = "↑" };
            up.tooltip = "위로 이동";
            up.SetEnabled(index > 0);
            up.AddToClassList("sfx-clip-action");
            row.Add(up);

            var down = new Button(() => MoveClip(index, index + 1)) { text = "↓" };
            down.tooltip = "아래로 이동";
            down.SetEnabled(index < (selectedCue.Clips?.Count ?? 0) - 1);
            down.AddToClassList("sfx-clip-action");
            row.Add(down);

            var remove = new Button(() => RemoveClip(index)) { text = "×" };
            remove.tooltip = "Cue에서 연결만 해제";
            remove.AddToClassList("sfx-clip-remove");
            row.Add(remove);
            return row;
        }

        private VisualElement BuildVolumeSection()
        {
            var section = MakeSection("개별 Cue 음량", "게임 설정의 전체 효과음 음량과 별도로 적용됩니다.");
            var range = selectedCue.VolumeRange;
            var summary = new Label($"실제 재생 범위  {FormatVolume(range)}");
            summary.AddToClassList("sfx-volume-summary");

            var minSlider = new Slider("최소 음량", 0f, 2f) { value = range.x, showInputField = true };
            minSlider.name = "sfx-volume-min";
            minSlider.AddToClassList("sfx-field");
            minSlider.RegisterValueChangedCallback(evt =>
            {
                SetVector2Property("volumeRange", evt.newValue, selectedCue.VolumeRange.y, "SFX 최소 음량 변경");
                summary.text = $"실제 재생 범위  {FormatVolume(selectedCue.VolumeRange)}";
            });
            section.Add(minSlider);

            var maxSlider = new Slider("최대 음량", 0f, 2f) { value = range.y, showInputField = true };
            maxSlider.name = "sfx-volume-max";
            maxSlider.AddToClassList("sfx-field");
            maxSlider.RegisterValueChangedCallback(evt =>
            {
                SetVector2Property("volumeRange", selectedCue.VolumeRange.x, evt.newValue, "SFX 최대 음량 변경");
                summary.text = $"실제 재생 범위  {FormatVolume(selectedCue.VolumeRange)}";
            });
            section.Add(maxSlider);

            var boostHelp = new HelpBox(
                "1.0은 100%입니다. 1.0 초과는 최대 2.0(200%)까지 증폭하며 원본에 따라 클리핑이 생길 수 있습니다.",
                HelpBoxMessageType.Info);
            boostHelp.AddToClassList("sfx-help");
            section.Add(boostHelp);

            section.Add(summary);
            return section;
        }

        private VisualElement BuildAdvancedSection()
        {
            var foldout = new Foldout { text = "고급 재생 설정", value = false };
            foldout.AddToClassList("sfx-advanced-foldout");

            var pitch = selectedCue.PitchRange;
            var pitchMin = new FloatField("최소 피치") { value = pitch.x };
            pitchMin.AddToClassList("sfx-field");
            pitchMin.RegisterValueChangedCallback(evt =>
                SetVector2Property(
                    "pitchRange",
                    Mathf.Clamp(evt.newValue, -3f, 3f),
                    selectedCue.PitchRange.y,
                    "SFX 최소 피치 변경"));
            foldout.Add(pitchMin);

            var pitchMax = new FloatField("최대 피치") { value = pitch.y };
            pitchMax.AddToClassList("sfx-field");
            pitchMax.RegisterValueChangedCallback(evt =>
                SetVector2Property(
                    "pitchRange",
                    selectedCue.PitchRange.x,
                    Mathf.Clamp(evt.newValue, -3f, 3f),
                    "SFX 최대 피치 변경"));
            foldout.Add(pitchMax);

            var spatial = new Slider("공간감  2D ↔ 3D", 0f, 1f)
            {
                value = selectedCue.SpatialBlend,
                showInputField = true
            };
            spatial.AddToClassList("sfx-field");
            spatial.RegisterValueChangedCallback(evt =>
                SetFloatProperty("spatialBlend", evt.newValue, 0f, 1f, "SFX 공간감 변경"));
            foldout.Add(spatial);

            var cooldown = new FloatField("중복 재생 제한(초)") { value = selectedCue.DuplicateCooldown };
            cooldown.AddToClassList("sfx-field");
            cooldown.RegisterValueChangedCallback(evt =>
                SetFloatProperty("duplicateCooldown", evt.newValue, 0f, float.MaxValue, "SFX 중복 제한 변경"));
            foldout.Add(cooldown);

            var priority = new EnumField("동시 재생 우선순위", selectedCue.Priority);
            priority.AddToClassList("sfx-field");
            priority.RegisterValueChangedCallback(evt => SetPriority((SfxPriority)evt.newValue));
            foldout.Add(priority);

            var note = new Label("피치 변화는 Runtime 재생에서 적용됩니다. 관리창 미리듣기는 클립과 음량 확인용입니다.");
            note.AddToClassList("sfx-safe-note");
            foldout.Add(note);
            return foldout;
        }

        private static VisualElement MakeSection(string title, string description)
        {
            var section = new VisualElement();
            section.AddToClassList("sfx-section");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("sfx-section-title");
            var descriptionLabel = new Label(description);
            descriptionLabel.AddToClassList("sfx-section-description");
            section.Add(titleLabel);
            section.Add(descriptionLabel);
            return section;
        }

        private static VisualElement MakeNotice(string message, bool warning)
        {
            var notice = new Label(message);
            notice.AddToClassList(warning ? "sfx-notice--warning" : "sfx-notice");
            return notice;
        }

        private void RegisterClipDropZone(VisualElement dropZone)
        {
            dropZone.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (!DragAndDrop.objectReferences.OfType<AudioClip>().Any())
                {
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                dropZone.AddToClassList("sfx-drop-zone--active");
                evt.StopPropagation();
            });
            dropZone.RegisterCallback<DragLeaveEvent>(_ =>
                dropZone.RemoveFromClassList("sfx-drop-zone--active"));
            dropZone.RegisterCallback<DragPerformEvent>(evt =>
            {
                var clips = DragAndDrop.objectReferences.OfType<AudioClip>().ToArray();
                if (clips.Length == 0)
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                dropZone.RemoveFromClassList("sfx-drop-zone--active");
                AddClips(clips);
                evt.StopPropagation();
            });
        }

        private void AddClips(IEnumerable<AudioClip> sourceClips)
        {
            if (selectedCue == null)
            {
                return;
            }

            var clipsToAdd = sourceClips
                .Where(clip => clip != null)
                .Distinct()
                .Where(clip => selectedCue.Clips == null || !selectedCue.Clips.Contains(clip))
                .ToArray();
            if (clipsToAdd.Length == 0)
            {
                SetStatus("이미 연결된 사운드입니다.", true);
                return;
            }

            Undo.RecordObject(selectedCue, "SFX 사운드 추가");
            var serialized = new SerializedObject(selectedCue);
            var clips = serialized.FindProperty("clips");
            foreach (var clip in clipsToAdd)
            {
                var index = clips.arraySize;
                clips.InsertArrayElementAtIndex(index);
                clips.GetArrayElementAtIndex(index).objectReferenceValue = clip;
            }
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(selectedCue);
            RefreshCueAfterEdit(true);
            SetStatus($"{selectedCue.name}에 사운드 {clipsToAdd.Length}개를 추가했습니다.");
        }

        private void ReplaceClip(int index, AudioClip clip)
        {
            if (selectedCue == null || index < 0 || index >= (selectedCue.Clips?.Count ?? 0))
            {
                return;
            }

            if (clip != null && selectedCue.Clips.Where((_, i) => i != index).Contains(clip))
            {
                SetStatus("같은 사운드가 이미 이 Cue에 연결되어 있습니다.", true);
                RefreshDetails();
                return;
            }

            Undo.RecordObject(selectedCue, "SFX 사운드 교체");
            var serialized = new SerializedObject(selectedCue);
            serialized.FindProperty("clips").GetArrayElementAtIndex(index).objectReferenceValue = clip;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(selectedCue);
            RefreshCueAfterEdit(true);
            SetStatus(clip == null ? "사운드 연결을 비웠습니다." : $"{clip.name}으로 교체했습니다.");
        }

        private void RemoveClip(int index)
        {
            if (selectedCue == null || index < 0 || index >= (selectedCue.Clips?.Count ?? 0))
            {
                return;
            }

            Undo.RecordObject(selectedCue, "SFX 사운드 연결 해제");
            var serialized = new SerializedObject(selectedCue);
            var clips = serialized.FindProperty("clips");
            clips.GetArrayElementAtIndex(index).objectReferenceValue = null;
            clips.DeleteArrayElementAtIndex(index);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(selectedCue);
            StopPreview();
            RefreshCueAfterEdit(true);
            SetStatus("Cue에서 사운드 연결만 해제했습니다. 원본 파일은 유지됩니다.");
        }

        private void MoveClip(int sourceIndex, int targetIndex)
        {
            if (selectedCue == null || sourceIndex == targetIndex || sourceIndex < 0 ||
                targetIndex < 0 || targetIndex >= (selectedCue.Clips?.Count ?? 0))
            {
                return;
            }

            Undo.RecordObject(selectedCue, "SFX 사운드 순서 변경");
            var serialized = new SerializedObject(selectedCue);
            serialized.FindProperty("clips").MoveArrayElement(sourceIndex, targetIndex);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(selectedCue);
            RefreshCueAfterEdit(true);
            SetStatus("사운드 재생 후보 순서를 변경했습니다.");
        }

        private void SetVector2Property(string propertyName, float x, float y, string undoName)
        {
            if (selectedCue == null)
            {
                return;
            }

            Undo.RecordObject(selectedCue, undoName);
            var serialized = new SerializedObject(selectedCue);
            serialized.FindProperty(propertyName).vector2Value = new Vector2(x, y);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(selectedCue);
            RefreshCueAfterEdit(false);
        }

        private void SetFloatProperty(string propertyName, float value, float minimum, float maximum, string undoName)
        {
            if (selectedCue == null)
            {
                return;
            }

            Undo.RecordObject(selectedCue, undoName);
            var serialized = new SerializedObject(selectedCue);
            serialized.FindProperty(propertyName).floatValue = Mathf.Clamp(value, minimum, maximum);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(selectedCue);
            RefreshCueAfterEdit(false);
        }

        private void SetPriority(SfxPriority value)
        {
            if (selectedCue == null)
            {
                return;
            }

            Undo.RecordObject(selectedCue, "SFX 우선순위 변경");
            var serialized = new SerializedObject(selectedCue);
            serialized.FindProperty("priority").enumValueIndex = (int)value;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(selectedCue);
            RefreshCueAfterEdit(false);
        }

        private void RefreshCueAfterEdit(bool rebuildDetails)
        {
            cueListView?.Rebuild();
            RefreshHeaderCounts();
            if (rebuildDetails)
            {
                RefreshDetails();
            }
        }

        private void PreviewCue(SfxCue cue)
        {
            var clips = cue?.Clips?.Where(clip => clip != null).ToArray();
            if (clips == null || clips.Length == 0)
            {
                SetStatus("미리들을 사운드가 없습니다.", true);
                return;
            }

            var clip = clips[Math.Abs(previewSequence++) % clips.Length];
            PreviewClip(clip, cue);
        }

        private void PreviewClip(AudioClip clip, SfxCue cue)
        {
            if (clip == null)
            {
                return;
            }

            var range = cue != null ? cue.VolumeRange : Vector2.one;
            var volume = Mathf.Clamp01((range.x + range.y) * 0.5f);
            if (global::SfxEditorAudioPreview.Play(clip, 0, false, volume))
            {
                SetStatus($"미리듣기: {clip.name}  ·  Cue 평균 음량 {Mathf.RoundToInt(volume * 100f)}%");
            }
            else
            {
                SetStatus("Unity 오디오 미리듣기를 시작하지 못했습니다.", false, true);
            }
        }

        private void StopPreview()
        {
            global::SfxEditorAudioPreview.StopAll();
        }
    }
}
