using ProjectMT.EditorTools.MonsterMaker;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    internal sealed partial class MonsterMakerV2AuthoringView
    {
        private void BuildVoiceSfx()
        {
            var container = Section("voice-sfx");
            AddHelp(
                container,
                "이곳은 타격·발사·효과음이 아니라 몬스터 자체가 공격하며 내는 음성·기합입니다. " +
                "액티브 종류가 공격형이든 효과형이든 각각 시작 시 1회 재생됩니다.",
                HelpBoxMessageType.Info);
            BuildVoiceSfxEditor(
                container,
                serializedDraft.FindProperty("basicAttackVoiceSfx"),
                "기본공격 자체 SFX",
                "기본공격 모션이 시작될 때 몬스터가 내는 짧은 음성·기합");
            BuildVoiceSfxEditor(
                container,
                serializedDraft.FindProperty("activeSkillVoiceSfx"),
                "액티브스킬 자체 SFX",
                draft?.UseActiveSkill == true
                    ? "액티브 집중 연출과 스킬 모션이 시작될 때 몬스터가 내는 음성·기합"
                    : "액티브를 사용하지 않는 몬스터입니다. 음원은 미리 보관해도 전투에서 재생되지 않습니다.");
        }

        private void BuildVoiceSfxEditor(
            VisualElement container,
            SerializedProperty voice,
            string title,
            string timingHelp)
        {
            if (voice == null)
            {
                AddHelp(container, $"음성 입력 데이터를 찾을 수 없습니다: {title}", HelpBoxMessageType.Error);
                return;
            }

            var sound = voice.FindPropertyRelative("sound");
            var volume = voice.FindPropertyRelative("volume");
            var clip = sound.objectReferenceValue as AudioClip;
            var foldout = AddSubFoldout(
                container,
                $"{title} · {(clip == null ? "미배정" : clip.name)}",
                true,
                "voice-sfx:" + voice.propertyPath);
            AddRelativeProperty(foldout, sound, "원본 AudioClip (선택)");
            if (clip != null)
            {
                AddVolumePercentProperty(foldout, volume, "음량");
                var voicePath = voice.propertyPath;
                AddSfxActionRow(
                    foldout,
                    () => openSfxAdjust?.Invoke(voicePath, title),
                    () => PreviewSfx(voicePath));
            }
            AddHelp(
                foldout,
                timingHelp + ". 비워 두면 무음으로 처리됩니다.",
                HelpBoxMessageType.Info);
        }

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
                AddSfxActionRow(
                    foldout,
                    () => openSfxAdjust?.Invoke(feedback.propertyPath, timingLabel),
                    () => PreviewSfx(feedback.propertyPath));
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

        private void PreviewSfx(string sourcePath)
        {
            serializedDraft?.ApplyModifiedProperties();
            var source = serializedDraft?.FindProperty(sourcePath);
            var clip = source?.FindPropertyRelative("sound").objectReferenceValue as AudioClip;
            if (clip == null) return;

            var volume = source.FindPropertyRelative("soundVolume") ?? source.FindPropertyRelative("volume");
            var start = source.FindPropertyRelative("soundStartOffsetSeconds");
            var cut = source.FindPropertyRelative("soundEndCutSeconds");
            var overridePitch = source.FindPropertyRelative("overrideSoundPitch");
            var pitch = source.FindPropertyRelative("soundPitch");
            SfxEditorAudioPreview.StopAll();
            SfxEditorAudioPreview.PlaySegment(
                clip,
                start?.floatValue ?? 0f,
                cut?.floatValue ?? 0f,
                volume?.floatValue ?? 1f,
                overridePitch?.boolValue == true ? pitch?.floatValue ?? 1f : 1f);
        }
    }
}
