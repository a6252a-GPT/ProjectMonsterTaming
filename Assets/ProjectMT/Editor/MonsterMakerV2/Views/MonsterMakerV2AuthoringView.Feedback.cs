using ProjectMT.EditorTools.MonsterMaker;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    internal sealed partial class MonsterMakerV2AuthoringView
    {
        private void BuildFeedbackEditor(
            VisualElement container,
            SerializedProperty feedback,
            string timingLabel,
            string soundLabel,
            string helpText,
            MonsterMakerPreviewAnchor anchor,
            bool expanded)
        {
            if (feedback == null)
            {
                AddHelp(container, "연출 입력 데이터를 찾을 수 없습니다.", HelpBoxMessageType.Error);
                return;
            }

            var sound = feedback.FindPropertyRelative("sound");
            var legacyCue = feedback.FindPropertyRelative("sfx");
            var vfx = feedback.FindPropertyRelative("vfxPrefab");
            var hasSound = sound.objectReferenceValue != null || legacyCue.objectReferenceValue != null;
            var hasVfx = vfx.objectReferenceValue != null;
            var stateLabel = hasSound || hasVfx
                ? $"{(hasSound ? "사운드" : string.Empty)}{(hasSound && hasVfx ? " + " : string.Empty)}{(hasVfx ? "VFX" : string.Empty)} 연결됨"
                : "없음 · 정상";
            var foldout = AddSubFoldout(container, $"{timingLabel} · 선택 연출 · {stateLabel}", expanded);
            AddRelativeProperty(foldout, sound, soundLabel + " (선택)");
            if (sound.objectReferenceValue is AudioClip clip)
            {
                AddActionRow(
                    foldout,
                    ("SFX 미리듣기", () => SfxEditorAudioPreview.Play(clip, 0, false, 1f), "draft-action-button"),
                    ("SFX 정지", SfxEditorAudioPreview.StopAll, "draft-action-button"));
            }
            if (legacyCue.objectReferenceValue != null)
            {
                var legacy = AddRelativeProperty(foldout, legacyCue, "기존 SFX Cue");
                legacy?.SetEnabled(false);
                AddHelp(
                    foldout,
                    "새 AudioClip을 지정하면 전투 반영 때 현재 몬스터 전용 Cue로 교체됩니다.",
                    HelpBoxMessageType.Info);
            }

            AddRelativeProperty(foldout, vfx, "VFX Prefab (선택)");
            if (hasVfx)
            {
                AddRelativeProperty(foldout, feedback.FindPropertyRelative("vfxLifetime"), "VFX 유지 시간");
                AddRelativeProperty(foldout, feedback.FindPropertyRelative("localPosition"), "VFX 위치 보정");
                AddRelativeProperty(foldout, feedback.FindPropertyRelative("localEulerAngles"), "VFX 회전 보정");
                AddRelativeProperty(foldout, feedback.FindPropertyRelative("scale"), "VFX 크기");
                var feedbackPath = feedback.propertyPath;
                AddActionRow(
                    foldout,
                    ("VFX 위치 직접 조절 · 재생",
                        () => openFeedbackVfxAdjust?.Invoke(feedbackPath, timingLabel, anchor),
                        "draft-action-button"));
            }

            AddHelp(
                foldout,
                helpText + " 사운드와 VFX는 둘 다 비어 있어도 정상입니다.",
                HelpBoxMessageType.Info);
        }
    }
}
