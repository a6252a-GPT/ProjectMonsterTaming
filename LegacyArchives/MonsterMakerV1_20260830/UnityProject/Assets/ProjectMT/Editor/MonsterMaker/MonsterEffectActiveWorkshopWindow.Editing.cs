using System;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public sealed partial class MonsterEffectActiveWorkshopWindow
    {
        private void DrawGroups()
        {
            var groups = serializedProfile.FindProperty("groups");
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"효과 묶음 · {groups.arraySize}개", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (groups.arraySize < MonsterEffectActiveProfile.MaximumGroupCount &&
                    MonsterWorkshopVisualTheme.DrawTintedButton(
                        new GUIContent("+ 효과 묶음"),
                        MonsterWorkshopVisualTheme.PrimaryColor,
                        24f,
                        92f))
                {
                    AddGroup(groups);
                }
            }

            for (var groupIndex = 0; groupIndex < groups.arraySize; groupIndex++)
            {
                DrawGroup(groups, groupIndex);
            }
        }

        private void DrawGroup(SerializedProperty groups, int groupIndex)
        {
            var group = groups.GetArrayElementAtIndex(groupIndex);
            var name = group.FindPropertyRelative("displayName").stringValue;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label($"#{groupIndex + 1:00} {name}", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(groupIndex == 0))
                    {
                        if (GUILayout.Button("▲", EditorStyles.miniButton, GUILayout.Width(28f)))
                        {
                            MoveGroupAndCommit(groups, groupIndex, groupIndex - 1);
                            return;
                        }
                    }
                    using (new EditorGUI.DisabledScope(groupIndex >= groups.arraySize - 1))
                    {
                        if (GUILayout.Button("▼", EditorStyles.miniButton, GUILayout.Width(28f)))
                        {
                            MoveGroupAndCommit(groups, groupIndex, groupIndex + 1);
                            return;
                        }
                    }
                    if (GUILayout.Button("복제", EditorStyles.miniButton, GUILayout.Width(42f)))
                    {
                        DuplicateGroupAndCommit(groups, groupIndex, name);
                        return;
                    }
                    using (new EditorGUI.DisabledScope(groups.arraySize <= 1))
                    {
                        var delete = GUILayout.Button("삭제", EditorStyles.miniButton, GUILayout.Width(42f));
                        if (Event.current.type == EventType.Repaint)
                        {
                            lastGroupHeaderRightmostRect = GUILayoutUtility.GetLastRect();
                        }
                        if (delete)
                        {
                            DeleteGroupAndCommit(groups, groupIndex);
                            return;
                        }
                    }
                }

                EditorGUILayout.PropertyField(group.FindPropertyRelative("groupId"), new GUIContent("묶음 ID"));
                EditorGUILayout.PropertyField(group.FindPropertyRelative("displayName"), new GUIContent("표시 이름"));
                DrawDelay(group.FindPropertyRelative("delayAfterPrevious"),
                    groupIndex == 0 ? "스킬 발동 → 이 묶음" : "이전 묶음 → 이 묶음");
                DrawTarget(group);
                GUILayout.Space(4f);
                DrawEffects(group);
                GUILayout.Space(4f);
                DrawPresentationContracts(group);
            }
        }

        private static void DrawDelay(SerializedProperty property, string label)
        {
            var enabled = property.floatValue > 0f;
            var next = EditorGUILayout.ToggleLeft($"{label} · 딜레이 사용", enabled);
            if (next != enabled) property.floatValue = next ? 0.15f : 0f;
            if (!next) return;
            EditorGUI.indentLevel++;
            property.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField("딜레이(초)", property.floatValue));
            EditorGUI.indentLevel--;
        }

        private void DrawTarget(SerializedProperty group)
        {
            var role = (MonsterEffectActiveRole)serializedProfile.FindProperty("role").enumValueIndex;
            var allowed = TargetsFor(role);
            var targetProperty = group.FindPropertyRelative("target");
            var current = (MonsterSkillTargetType)targetProperty.enumValueIndex;
            var selected = Mathf.Max(0, Array.IndexOf(allowed, current));
            selected = EditorGUILayout.Popup("대상", selected, allowed.Select(TargetLabel).ToArray());
            targetProperty.enumValueIndex = (int)allowed[selected];

            var target = allowed[selected];
            if (target is MonsterSkillTargetType.NearbyAllies or MonsterSkillTargetType.AllAllies)
            {
                EditorGUILayout.PropertyField(
                    group.FindPropertyRelative("includeCaster"),
                    new GUIContent("시전자 포함"));
            }
            if (target is MonsterSkillTargetType.NearbyAllies or MonsterSkillTargetType.TargetAreaEnemies)
            {
                EditorGUILayout.PropertyField(group.FindPropertyRelative("radius"), new GUIContent("범위(m)"));
            }
            if (target is MonsterSkillTargetType.NearbyAllies or MonsterSkillTargetType.AllAllies or
                MonsterSkillTargetType.TargetAreaEnemies)
            {
                group.FindPropertyRelative("maxTargets").intValue = EditorGUILayout.IntSlider(
                    "최대 대상",
                    group.FindPropertyRelative("maxTargets").intValue,
                    1,
                    32);
            }
        }

        private void DrawEffects(SerializedProperty group)
        {
            var effects = group.FindPropertyRelative("effects");
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"적용 효과 · {effects.arraySize}개", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+ 효과", EditorStyles.miniButton, GUILayout.Width(68f)))
                {
                    AddEffect(effects);
                }
            }

            for (var index = 0; index < effects.arraySize; index++)
            {
                var effect = effects.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label($"효과 {index + 1:00}", EditorStyles.miniBoldLabel);
                        GUILayout.FlexibleSpace();
                        using (new EditorGUI.DisabledScope(effects.arraySize <= 1))
                        {
                            if (GUILayout.Button("삭제", EditorStyles.miniButton, GUILayout.Width(42f)))
                            {
                                DeleteEffectAndCommit(effects, index);
                                return;
                            }
                        }
                    }
                    DrawEffect(effect, index);
                }
            }
        }

        private void DrawEffect(SerializedProperty effect, int index)
        {
            var role = (MonsterEffectActiveRole)serializedProfile.FindProperty("role").enumValueIndex;
            var allowed = EffectsFor(role);
            var typeProperty = effect.FindPropertyRelative("type");
            var current = (MonsterSkillEffectType)typeProperty.enumValueIndex;
            var selected = Mathf.Max(0, Array.IndexOf(allowed, current));
            var next = EditorGUILayout.Popup("효과 종류", selected, allowed.Select(EffectLabel).ToArray());
            if (next != selected)
            {
                typeProperty.enumValueIndex = (int)allowed[next];
                ConfigureEffectDefaults(effect, allowed[next], index);
            }
            var type = (MonsterSkillEffectType)typeProperty.enumValueIndex;
            effect.FindPropertyRelative("effectId").stringValue =
                $"effect_{index + 1:00}_{type.ToString().ToLowerInvariant()}";

            DrawMagnitude(effect, type);
            if (UsesDuration(type))
            {
                effect.FindPropertyRelative("duration").floatValue = Mathf.Max(0.05f,
                    EditorGUILayout.FloatField("지속 시간(초)", effect.FindPropertyRelative("duration").floatValue));
            }
            if (type == MonsterSkillEffectType.Heal)
            {
                var duration = effect.FindPropertyRelative("duration");
                var periodic = duration.floatValue > 0f;
                var nextPeriodic = EditorGUILayout.ToggleLeft("지속 회복 사용", periodic);
                if (nextPeriodic != periodic) duration.floatValue = nextPeriodic ? 4f : 0f;
                if (nextPeriodic)
                {
                    duration.floatValue = Mathf.Max(0.1f,
                        EditorGUILayout.FloatField("지속 시간(초)", duration.floatValue));
                    var interval = effect.FindPropertyRelative("repeatInterval");
                    interval.floatValue = Mathf.Max(0.05f,
                        EditorGUILayout.FloatField("회복 간격(초)", Mathf.Max(0.05f, interval.floatValue)));
                }
            }
        }

        private static void DrawMagnitude(SerializedProperty effect, MonsterSkillEffectType type)
        {
            var source = effect.FindPropertyRelative("valueSource");
            var magnitude = effect.FindPropertyRelative("magnitude");
            if (type is MonsterSkillEffectType.Heal or MonsterSkillEffectType.Shield)
            {
                var sources = new[]
                {
                    MonsterSkillValueSource.AttackPowerRatio,
                    MonsterSkillValueSource.TargetMaxHealthRatio,
                    MonsterSkillValueSource.Flat
                };
                var current = (MonsterSkillValueSource)source.enumValueIndex;
                var selected = Mathf.Max(0, Array.IndexOf(sources, current));
                selected = EditorGUILayout.Popup(
                    "수치 기준",
                    selected,
                    new[] { "시전자 공격력 비례", "대상 최대 체력 비례", "고정 수치" });
                source.enumValueIndex = (int)sources[selected];
                magnitude.floatValue = Mathf.Max(0f,
                    EditorGUILayout.FloatField(
                        sources[selected] == MonsterSkillValueSource.Flat ? "수치" : "비율(1 = 100%)",
                        magnitude.floatValue));
                return;
            }
            if (type is MonsterSkillEffectType.EnergyGain or MonsterSkillEffectType.EnergyDrain)
            {
                var percentage = source.enumValueIndex == (int)MonsterSkillValueSource.TargetEnergyCapacityRatio;
                var next = EditorGUILayout.ToggleLeft("대상 최대 기력 비율 사용", percentage);
                source.enumValueIndex = (int)(next
                    ? MonsterSkillValueSource.TargetEnergyCapacityRatio
                    : MonsterSkillValueSource.Flat);
                magnitude.floatValue = Mathf.Max(0f,
                    EditorGUILayout.FloatField(next ? "기력 비율(1 = 100%)" : "기력 수치", magnitude.floatValue));
                return;
            }

            source.enumValueIndex = (int)MonsterSkillValueSource.Flat;
            var maximum = type == MonsterSkillEffectType.Pull
                ? MonsterActiveHitEffect.MaximumPullDistance
                : 1f;
            magnitude.floatValue = Mathf.Clamp(
                EditorGUILayout.FloatField(
                    type == MonsterSkillEffectType.Pull ? "당기는 거리(m)" : "효과 비율(1 = 100%)",
                    magnitude.floatValue),
                0f,
                maximum);
        }

        private void DrawPresentationContracts(SerializedProperty group)
        {
            var slots = group.FindPropertyRelative("presentationSlots");
            group.isExpanded = EditorGUILayout.Foldout(
                group.isExpanded,
                $"VFX/SFX 계약 · {slots.arraySize}개",
                true);
            if (!group.isExpanded) return;

            EditorGUI.indentLevel++;
            GUILayout.Label(
                "실제 자산은 몬스터 메이커에서 연결합니다. 지속 공간은 지정 시간만큼 Loop VFX를 재생합니다.",
                EditorStyles.wordWrappedMiniLabel);
            for (var index = 0; index < slots.arraySize; index++)
            {
                var slot = slots.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label($"공간 {index + 1:00}", EditorStyles.miniBoldLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("삭제", EditorStyles.miniButton, GUILayout.Width(42f)))
                        {
                            DeleteSlotAndCommit(slots, index);
                            EditorGUI.indentLevel--;
                            return;
                        }
                    }
                    EditorGUILayout.PropertyField(slot.FindPropertyRelative("slotId"), new GUIContent("공간 ID"));
                    EditorGUILayout.PropertyField(slot.FindPropertyRelative("displayName"), new GUIContent("표시 이름"));
                    DrawPresentationEventPopup(slot.FindPropertyRelative("timing"));
                    DrawPresentationAnchorPopup(slot.FindPropertyRelative("anchor"));
                    var duration = slot.FindPropertyRelative("useDuration");
                    EditorGUILayout.PropertyField(duration, new GUIContent("지속시간 사용"));
                    if (duration.boolValue)
                    {
                        EditorGUILayout.PropertyField(
                            slot.FindPropertyRelative("duration"),
                            new GUIContent("재생 시간(초)"));
                        slot.FindPropertyRelative("multiplicity").enumValueIndex =
                            (int)MonsterActivePresentationMultiplicity.ContinuousUntilEnd;
                    }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ 일반 공간", EditorStyles.miniButton)) AddSlot(slots, false);
                if (GUILayout.Button("+ 지속 공간", EditorStyles.miniButton)) AddSlot(slots, true);
            }
            EditorGUI.indentLevel--;
        }

        private void DrawValidation()
        {
            if (profile.TryValidate(out var error))
            {
                EditorGUILayout.HelpBox(
                    $"사용 가능 · 효과 묶음 {profile.Groups.Count}개 · 예상 발동 {profile.EstimateDuration():0.##}초",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
            if (!string.IsNullOrWhiteSpace(message))
            {
                EditorGUILayout.HelpBox(message, messageType);
            }
        }

        private void DrawSaveControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (MonsterWorkshopVisualTheme.DrawTintedButton(
                        new GUIContent(loadedProfile == null ? "새 프리셋으로 저장" : "복제 후 새 프리셋 저장"),
                        MonsterWorkshopVisualTheme.PrimaryColor,
                        30f))
                {
                    SaveAsNew();
                }
                using (new EditorGUI.DisabledScope(loadedProfile == null))
                {
                    var update = MonsterWorkshopVisualTheme.DrawTintedButton(
                        new GUIContent("현재 프리셋에 저장"),
                        MonsterWorkshopVisualTheme.PreviewColor,
                        30f);
                    if (Event.current.type == EventType.Repaint)
                    {
                        lastSaveRightmostRect = GUILayoutUtility.GetLastRect();
                    }
                    if (update)
                    {
                        UpdateLoaded();
                    }
                }
            }

            if (originDraft == null) return;
            using (new EditorGUI.DisabledScope(loadedProfile == null))
            {
                if (MonsterWorkshopVisualTheme.DrawTintedButton(
                        new GUIContent($"[{loadedProfile?.ProfileId ?? "미저장"}] → {originDraft.MonsterId}에게 배정"),
                        MonsterWorkshopVisualTheme.FeelColor,
                        28f))
                {
                    AssignLoadedToOrigin();
                }
            }
        }

        private void AssignLoadedToOrigin()
        {
            if (originDraft == null || loadedProfile == null || dirty)
            {
                return;
            }

            Undo.RecordObject(originDraft, "효과형 액티브 배정");
            originDraft.EditorSetActiveEffectProfile(loadedProfile);
            EditorUtility.SetDirty(originDraft);
            AssetDatabase.SaveAssetIfDirty(originDraft);
            message = "몬스터 메이커에 효과형 액티브를 배정했습니다.";
            messageType = MessageType.Info;
            PresetAssigned?.Invoke();
            MonsterWorkshopAssignmentEvents.NotifyPresetAssigned();
        }
    }
}
