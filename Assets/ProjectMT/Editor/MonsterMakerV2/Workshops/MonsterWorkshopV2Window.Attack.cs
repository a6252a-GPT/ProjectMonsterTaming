using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.EditorTools.MonsterMaker;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    public sealed partial class MonsterWorkshopV2Window
    {
        partial void BuildAttackAssembler()
        {
            attackSerialized.Update();
            var info = Section("1. 프리셋 정보", "스킬 이름과 최대 기력은 몬스터별 Maker V2에서 정합니다.");
            info.Add(BoundProperty(attackSerialized, attackSerialized.FindProperty("profileId"), "프리셋 ID"));
            info.Add(BoundProperty(attackSerialized, attackSerialized.FindProperty("displayName"), "표시 이름"));
            info.Add(BoundProperty(attackSerialized, attackSerialized.FindProperty("description"), "기획 메모"));
            assemblerScroll.Add(info);

            var steps = attackSerialized.FindProperty("steps");
            var stepSection = Section($"2. 공격 Step · {steps.arraySize}개", "반복 횟수 대신 공격 한 번을 Step 하나로 추가합니다.");
            for (var index = 0; index < steps.arraySize; index++) BuildAttackStep(stepSection, steps, index);
            stepSection.Add(AddButton("+ 공격 Step 추가", () => ShowAddAttackStepMenu(steps)));
            assemblerScroll.Add(stepSection);
            BuildAttackFeel();
        }

        private void BuildAttackStep(VisualElement parent, SerializedProperty steps, int index)
        {
            var step = steps.GetArrayElementAtIndex(index);
            var pattern = (MonsterActiveAttackPattern)step.FindPropertyRelative("pattern").enumValueIndex;
            var card = new VisualElement(); card.AddToClassList("sub-card");
            card.Add(CardHeader($"#{index + 1:00} {step.FindPropertyRelative("displayName").stringValue}",
                SmallButton("▲", () => MoveArray(attackSerialized, "steps", index, -1), false, index > 0),
                SmallButton("▼", () => MoveArray(attackSerialized, "steps", index, 1), false, index < steps.arraySize - 1),
                SmallButton("복제", () => DuplicateArray(attackSerialized, "steps", index, true)),
                SmallButton("삭제", () => DeleteArray(attackSerialized, "steps", index), true, steps.arraySize > 1)));

            card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("stepId"), "Step ID"));
            card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("displayName"), "표시 이름"));
            card.Add(DelayControl(attackSerialized, step.FindPropertyRelative("delayAfterPrevious"), index == 0 ? "스킬 발동 후 딜레이" : "이전 Step 후 딜레이"));
            card.Add(AttackEnumPopup(step.FindPropertyRelative("targetPolicy"), "타깃 선택", EnumValues<MonsterActiveTargetPolicy>(), ActiveTargetLabel));
            card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("teleportBeforeAttack"), "공격 전 순간이동", ScheduleRebuild));
            if (step.FindPropertyRelative("teleportBeforeAttack").boolValue)
                card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("teleportFrontDistance"), "타깃 앞 거리(m)"));

            card.Add(AttackEnumPopup(step.FindPropertyRelative("pattern"), "공격 형태", EnumValues<MonsterActiveAttackPattern>(), ActivePatternLabel,
                () => ReconcileAttackStep(index, "공격 형태")));
            var supported = EnumValues<MonsterActiveAttackProgression>().Where(value => MonsterActiveAttackStep.SupportsProgression(pattern, value)).ToList();
            card.Add(AttackEnumPopup(step.FindPropertyRelative("progression"), "판정 진행", supported, ActiveProgressionLabel));
            card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("progressionDuration"), "진행 시간(초)"));
            card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("damageMultiplier"), "공격력 배율"));
            card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("maxTargets"), "최대 타깃"));
            BuildAttackGeometry(card, step, pattern);
            card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("telegraphDelay"), "예고 후 판정(초)"));
            card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("visualDuration"), "연출 유지(초)"));
            BuildAttackHitEffects(card, step, index);
            BuildAttackPresentationSlots(card, step, index);
            parent.Add(card);
        }

        private void BuildAttackGeometry(VisualElement card, SerializedProperty step, MonsterActiveAttackPattern pattern)
        {
            var projectile = pattern is MonsterActiveAttackPattern.PiercingProjectile or MonsterActiveAttackPattern.ExplosiveProjectile;
            if (pattern is MonsterActiveAttackPattern.Line or MonsterActiveAttackPattern.Cone or MonsterActiveAttackPattern.PiercingBeam || projectile)
                card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("range"), "사거리(m)"));
            if (pattern is MonsterActiveAttackPattern.Line or MonsterActiveAttackPattern.PiercingBeam)
                card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("width"), "공격 폭(m)"));
            if (pattern is MonsterActiveAttackPattern.SelfCircle or MonsterActiveAttackPattern.FrontCircle ||
                pattern == MonsterActiveAttackPattern.ExplosiveProjectile ||
                pattern == MonsterActiveAttackPattern.InstantMagic && (MonsterActiveInstantMagicTarget)step.FindPropertyRelative("instantMagicTarget").enumValueIndex == MonsterActiveInstantMagicTarget.TargetArea)
                card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("radius"), "범위 반경(m)"));
            if (pattern is MonsterActiveAttackPattern.Line or MonsterActiveAttackPattern.Cone or MonsterActiveAttackPattern.FrontCircle or MonsterActiveAttackPattern.PiercingBeam)
                card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("forwardOffset"), "전방 중심 거리(m)"));
            if (pattern == MonsterActiveAttackPattern.Cone)
                card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("angle"), "부채꼴 각도"));
            if (projectile)
            {
                card.Add(AttackEnumPopup(step.FindPropertyRelative("projectileFormation"), "투사체 형태", EnumValues<MonsterActiveProjectileFormation>(),
                    value => value == MonsterActiveProjectileFormation.Single ? "단일" : "부채꼴", ScheduleRebuild));
                if ((MonsterActiveProjectileFormation)step.FindPropertyRelative("projectileFormation").enumValueIndex == MonsterActiveProjectileFormation.Fan)
                {
                    card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("projectileCount"), "투사체 개수"));
                    card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("projectileFanAngle"), "부채꼴 각도"));
                }
                card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("projectileSpeed"), "투사체 속도"));
                card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("projectileCollisionRadius"), "충돌 반경(m)"));
                if (pattern == MonsterActiveAttackPattern.ExplosiveProjectile)
                    card.Add(BoundProperty(attackSerialized, step.FindPropertyRelative("explosionRadius"), "폭발 반경(m)"));
            }
            if (pattern == MonsterActiveAttackPattern.InstantMagic)
            {
                card.Add(AttackEnumPopup(step.FindPropertyRelative("instantMagicTarget"), "마법 타깃", EnumValues<MonsterActiveInstantMagicTarget>(),
                    value => value == MonsterActiveInstantMagicTarget.SingleTarget ? "단일 타깃" : "범위 타깃", () => ReconcileAttackStep(ParseArrayIndex(step.propertyPath), "마법 타깃")));
                card.Add(AttackEnumPopup(step.FindPropertyRelative("magicDirection"), "등장 방향", EnumValues<MonsterActiveMagicDirection>(),
                    value => value == MonsterActiveMagicDirection.GroundUp ? "바닥에서 솟음" : "위에서 떨어짐", () => ReconcileAttackStep(ParseArrayIndex(step.propertyPath), "등장 방향")));
            }
        }

        private void BuildAttackHitEffects(VisualElement card, SerializedProperty step, int stepIndex)
        {
            var list = step.FindPropertyRelative("hitEffects");
            var section = Section($"4. 타격 효과 · {list.arraySize}개", "에어본·넉백·기절·출혈·감속·끌어당기기는 이 공격 판정에 결합됩니다.");
            for (var index = 0; index < list.arraySize; index++)
            {
                var captured = index; var effect = list.GetArrayElementAtIndex(index);
                var shell = new VisualElement(); shell.AddToClassList("sub-card");
                shell.Add(CardHeader($"효과 {index + 1:00}", SmallButton("▲", () => MoveNestedArray(stepIndex, "hitEffects", captured, -1), false, index > 0),
                    SmallButton("▼", () => MoveNestedArray(stepIndex, "hitEffects", captured, 1), false, index < list.arraySize - 1), SmallButton("삭제", () => DeleteNestedArray(stepIndex, "hitEffects", captured), true)));
                shell.Add(AttackEnumPopup(effect.FindPropertyRelative("type"), "효과 종류", EnumValues<MonsterActiveHitEffectType>(), ActiveHitLabel,
                    ScheduleRebuild));
                var type = (MonsterActiveHitEffectType)effect.FindPropertyRelative("type").enumValueIndex;
                if (type is MonsterActiveHitEffectType.Knockback or MonsterActiveHitEffectType.Airborne or MonsterActiveHitEffectType.Bleed or MonsterActiveHitEffectType.Slow or MonsterActiveHitEffectType.Pull)
                    shell.Add(BoundProperty(attackSerialized, effect.FindPropertyRelative("magnitude"), ActiveMagnitudeLabel(type)));
                shell.Add(BoundProperty(attackSerialized, effect.FindPropertyRelative("duration"), "지속 시간(초)"));
                if (type == MonsterActiveHitEffectType.Bleed)
                {
                    shell.Add(BoundProperty(attackSerialized, effect.FindPropertyRelative("secondaryMagnitude"), "추가 피해 배율"));
                    shell.Add(BoundProperty(attackSerialized, effect.FindPropertyRelative("tickInterval"), "피해 간격(초)"));
                }
                section.Add(shell);
            }
            section.Add(AddButton("+ 타격 효과 추가", () => AddAttackHitEffect(stepIndex)));
            card.Add(section);
        }

        private void BuildAttackPresentationSlots(VisualElement card, SerializedProperty step, int stepIndex)
        {
            var list = step.FindPropertyRelative("presentationSlots");
            var stepModel = attackWorking.Steps[stepIndex];
            var section = Section($"5. VFX/SFX 공간 계약 · {list.arraySize}개", "공격 방식과 맞는 공간만 유지합니다. 실제 자산은 Maker V2에서 연결합니다.");
            for (var index = 0; index < list.arraySize; index++)
            {
                var captured = index; var slot = list.GetArrayElementAtIndex(index);
                var shell = new VisualElement(); shell.AddToClassList("sub-card");
                shell.Add(CardHeader($"공간 {index + 1:00} · {slot.FindPropertyRelative("displayName").stringValue}",
                    SmallButton("▲", () => MoveNestedArray(stepIndex, "presentationSlots", captured, -1), false, index > 0),
                    SmallButton("▼", () => MoveNestedArray(stepIndex, "presentationSlots", captured, 1), false, index < list.arraySize - 1),
                    SmallButton("복제", () => DuplicateNestedArray(stepIndex, "presentationSlots", captured)),
                    SmallButton("삭제", () => DeleteNestedArray(stepIndex, "presentationSlots", captured), true)));
                shell.Add(BoundProperty(attackSerialized, slot.FindPropertyRelative("slotId"), "공간 ID"));
                shell.Add(BoundProperty(attackSerialized, slot.FindPropertyRelative("displayName"), "표시 이름"));
                var timingProperty = slot.FindPropertyRelative("timing");
                var eventChoices = EnumValues<MonsterActivePresentationEvent>()
                    .Where(value => MonsterActiveAttackVfxCompatibility.SupportsEvent(stepModel, value)).ToList();
                shell.Add(AttackEnumPopup(timingProperty, "발생 시점", eventChoices, ActiveEventLabel, ScheduleRebuild));

                var timing = (MonsterActivePresentationEvent)timingProperty.enumValueIndex;
                var anchorProperty = slot.FindPropertyRelative("anchor");
                var anchorChoices = EnumValues<MonsterActivePresentationAnchor>()
                    .Where(value => MonsterActiveAttackVfxCompatibility.SupportsAnchor(stepModel, timing, value)).ToList();
                shell.Add(AttackEnumPopup(anchorProperty, "기준 위치", anchorChoices, ActiveAnchorLabel, ScheduleRebuild));

                var multiplicityProperty = slot.FindPropertyRelative("multiplicity");
                var multiplicityChoices = EnumValues<MonsterActivePresentationMultiplicity>()
                    .Where(value => MonsterActiveAttackVfxCompatibility.SupportsMultiplicity(stepModel, timing, value)).ToList();
                shell.Add(AttackEnumPopup(multiplicityProperty, "재생 횟수", multiplicityChoices, ActiveMultiplicityLabel, ScheduleRebuild));

                var anchor = (MonsterActivePresentationAnchor)anchorProperty.enumValueIndex;
                var multiplicity = (MonsterActivePresentationMultiplicity)multiplicityProperty.enumValueIndex;
                var attachmentChoices = EnumValues<MonsterActivePresentationAttachment>()
                    .Where(value => MonsterActiveAttackVfxCompatibility.SupportsAttachment(stepModel, timing, anchor, multiplicity, value)).ToList();
                shell.Add(AttackEnumPopup(slot.FindPropertyRelative("attachment"), "부착 방식", attachmentChoices, ActiveAttachmentLabel));

                var endProperty = slot.FindPropertyRelative("endPolicy");
                var endChoices = EnumValues<MonsterActivePresentationEndPolicy>()
                    .Where(value => MonsterActiveAttackVfxCompatibility.SupportsEndPolicy(stepModel, timing, value)).ToList();
                shell.Add(AttackEnumPopup(endProperty, "종료 규칙", endChoices, ActiveEndLabel, ScheduleRebuild));
                shell.Add(BoundProperty(attackSerialized, slot.FindPropertyRelative("description"), "제작 메모"));
                if ((MonsterActivePresentationEndPolicy)endProperty.enumValueIndex == MonsterActivePresentationEndPolicy.Timed)
                {
                    shell.Add(BoundProperty(attackSerialized, slot.FindPropertyRelative("useDuration"), "지속시간 직접 사용", ScheduleRebuild));
                    if (slot.FindPropertyRelative("useDuration").boolValue)
                        shell.Add(BoundProperty(attackSerialized, slot.FindPropertyRelative("duration"), "지속 시간(초)"));
                }
                else if (slot.FindPropertyRelative("useDuration").boolValue)
                {
                    slot.FindPropertyRelative("useDuration").boolValue = false;
                    attackSerialized.ApplyModifiedProperties();
                    attackDirty = true;
                }
                section.Add(shell);
            }
            section.Add(AddButton("+ VFX/SFX 공간 추가", () => AddAttackSlot(stepIndex)));
            card.Add(section);
        }

        private void BuildAttackFeel()
        {
            var section = Section("6. 액티브 공통 FEEL 타격감", "모든 Step의 실제 명중이 FEEL 프로필 하나를 공유합니다.");
            var feel = attackSerialized.FindProperty("impactFeel");
            var prefab = feel.FindPropertyRelative("prefab");
            var options = BasicAttackFeelPresetUtility.LoadFeelProfileOptions(prefab.objectReferenceValue as GameObject).ToList();
            var choices = options.Select(x => x.Label).ToList();
            var current = Mathf.Max(0, options.FindIndex(x => x.Profile == prefab.objectReferenceValue));
            var popup = new PopupField<string>("FEEL 프로필", choices, current); popup.AddToClassList("editor-field");
            popup.RegisterValueChangedCallback(evt =>
            {
                var index = choices.IndexOf(evt.newValue); attackSerialized.Update();
                attackSerialized.FindProperty("impactFeel").FindPropertyRelative("prefab").objectReferenceValue = index >= 0 ? options[index].Profile : null;
                attackSerialized.ApplyModifiedProperties(); attackDirty = true; RefreshState();
            });
            section.Add(popup); section.Add(AddButton("FEEL 연구소 열기", BasicAttackFeelPresetUtility.OpenFormalLab));
            assemblerScroll.Add(section);
        }

        private VisualElement DelayControl(SerializedObject serialized, SerializedProperty property, string label)
        {
            var shell = new VisualElement();
            var toggle = new Toggle("딜레이 사용") { value = property.floatValue > 0f }; toggle.AddToClassList("editor-field"); shell.Add(toggle);
            if (toggle.value) shell.Add(BoundProperty(serialized, property, label));
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (suppressUiCallbacks) return;
                serialized.Update(); var live = serialized.FindProperty(property.propertyPath); live.floatValue = evt.newValue ? Mathf.Max(0.1f, live.floatValue) : 0f;
                serialized.ApplyModifiedProperties(); MarkCurrentDirty(); ScheduleRebuild();
            });
            return shell;
        }

        private PopupField<T> AttackEnumPopup<T>(SerializedProperty property, string label, List<T> choices, Func<T, string> format, Action changed = null) where T : struct
        {
            if (choices == null || choices.Count == 0) throw new InvalidOperationException($"{label}에 표시할 수 있는 호환 옵션이 없습니다.");
            var current = (T)Enum.ToObject(typeof(T), property.enumValueIndex);
            if (!choices.Contains(current))
            {
                current = choices[0];
                property.enumValueIndex = Convert.ToInt32(current);
                attackSerialized.ApplyModifiedProperties();
                attackDirty = true;
            }
            var field = new PopupField<T>(label, choices, current, format, format); field.AddToClassList("editor-field");
            var path = property.propertyPath;
            field.RegisterValueChangedCallback(evt =>
            {
                if (suppressUiCallbacks) return;
                attackSerialized.Update(); attackSerialized.FindProperty(path).enumValueIndex = Convert.ToInt32(evt.newValue); attackSerialized.ApplyModifiedProperties();
                attackDirty = true; changed?.Invoke(); if (changed == null) RefreshState();
            });
            return field;
        }

        private void ShowAddAttackStepMenu(SerializedProperty steps)
        {
            var menu = new GenericMenu();
            foreach (var pattern in EnumValues<MonsterActiveAttackPattern>()) { var captured = pattern; menu.AddItem(new GUIContent(ActivePatternLabel(pattern)), false, () => AddAttackStep(captured)); }
            menu.ShowAsContext();
        }

        private void AddAttackStep(MonsterActiveAttackPattern pattern)
        {
            attackSerialized.Update(); var list = attackSerialized.FindProperty("steps"); var index = list.arraySize; list.InsertArrayElementAtIndex(index);
            var step = list.GetArrayElementAtIndex(index); step.FindPropertyRelative("stepId").stringValue = $"step_{index + 1:00}";
            step.FindPropertyRelative("displayName").stringValue = ActivePatternLabel(pattern); step.FindPropertyRelative("pattern").enumValueIndex = (int)pattern;
            step.FindPropertyRelative("progression").enumValueIndex = 0; step.FindPropertyRelative("delayAfterPrevious").floatValue = index == 0 ? 0f : 0.12f;
            step.FindPropertyRelative("damageMultiplier").floatValue = 1f; step.FindPropertyRelative("maxTargets").intValue = 8;
            step.FindPropertyRelative("range").floatValue = 4f; step.FindPropertyRelative("width").floatValue = 1.2f; step.FindPropertyRelative("radius").floatValue = 1.8f;
            step.FindPropertyRelative("forwardOffset").floatValue = 1.5f; step.FindPropertyRelative("angle").floatValue = 70f;
            step.FindPropertyRelative("progressionDuration").floatValue = 0.25f; step.FindPropertyRelative("telegraphDelay").floatValue = 0.12f;
            step.FindPropertyRelative("visualDuration").floatValue = 0.8f; step.FindPropertyRelative("hitEffects").arraySize = 0; step.FindPropertyRelative("presentationSlots").arraySize = 0;
            attackSerialized.ApplyModifiedProperties(); ReconcileAttackStep(index, "새 공격");
        }

        private void ReconcileAttackStep(int index, string reason)
        {
            attackSerialized.ApplyModifiedProperties();
            if (index < 0 || index >= attackWorking.Steps.Count) return;
            var step = attackWorking.Steps[index]; var reconciled = MonsterActiveAttackVfxContractTemplates.Reconcile(step, step.PresentationSlots, out var result);
            step.EditorSetPresentationSlots(reconciled); attackSerialized.Update(); attackDirty = true;
            attackMessage = $"{reason} 기준 공간 정리 · 유지 {result.Retained} · 추가 {result.Added} · 제외 {result.Archived}";
            RebuildCurrent();
        }

        private void AddAttackHitEffect(int stepIndex)
        {
            attackSerialized.Update(); var list = attackSerialized.FindProperty("steps").GetArrayElementAtIndex(stepIndex).FindPropertyRelative("hitEffects");
            var index = list.arraySize; list.InsertArrayElementAtIndex(index); var effect = list.GetArrayElementAtIndex(index);
            effect.FindPropertyRelative("type").enumValueIndex = (int)MonsterActiveHitEffectType.Stun; effect.FindPropertyRelative("magnitude").floatValue = 0.25f;
            effect.FindPropertyRelative("duration").floatValue = 0.35f; effect.FindPropertyRelative("tickInterval").floatValue = 0.5f;
            attackSerialized.ApplyModifiedProperties(); attackDirty = true; RebuildCurrent();
        }

        private void AddAttackSlot(int stepIndex)
        {
            attackSerialized.Update(); var list = attackSerialized.FindProperty("steps").GetArrayElementAtIndex(stepIndex).FindPropertyRelative("presentationSlots");
            var index = list.arraySize; list.InsertArrayElementAtIndex(index); var slot = list.GetArrayElementAtIndex(index);
            slot.FindPropertyRelative("slotId").stringValue = $"custom_{index + 1:00}"; slot.FindPropertyRelative("displayName").stringValue = "사용자 공간";
            slot.FindPropertyRelative("description").stringValue = "Monster Maker V2에서 몬스터 고유 VFX/SFX를 연결합니다.";
            slot.FindPropertyRelative("duration").floatValue = 1f; attackSerialized.ApplyModifiedProperties(); attackDirty = true; RebuildCurrent();
        }

        private void MoveArray(SerializedObject serialized, string path, int index, int direction)
        { serialized.Update(); var list = serialized.FindProperty(path); var target = Mathf.Clamp(index + direction, 0, list.arraySize - 1); if (target != index) list.MoveArrayElement(index, target); serialized.ApplyModifiedProperties(); MarkCurrentDirty(rebuild: true); }
        private void DuplicateArray(SerializedObject serialized, string path, int index, bool renewId)
        { serialized.Update(); var list = serialized.FindProperty(path); list.InsertArrayElementAtIndex(index); if (renewId) list.GetArrayElementAtIndex(index + 1).FindPropertyRelative("stepId").stringValue += "_copy"; serialized.ApplyModifiedProperties(); MarkCurrentDirty(rebuild: true); }
        private void DeleteArray(SerializedObject serialized, string path, int index)
        { serialized.Update(); var list = serialized.FindProperty(path); if (list.arraySize <= 1) { attackMessage = "오류: 공격 Step은 하나 이상 필요합니다."; RefreshState(); return; } list.DeleteArrayElementAtIndex(index); serialized.ApplyModifiedProperties(); MarkCurrentDirty(rebuild: true); }
        private void MoveNestedArray(int owner, string path, int index, int direction)
        { attackSerialized.Update(); var list = attackSerialized.FindProperty("steps").GetArrayElementAtIndex(owner).FindPropertyRelative(path); var target = Mathf.Clamp(index + direction, 0, list.arraySize - 1); if (target != index) list.MoveArrayElement(index, target); attackSerialized.ApplyModifiedProperties(); attackDirty = true; RebuildCurrent(); }
        private void DeleteNestedArray(int owner, string path, int index)
        { attackSerialized.Update(); attackSerialized.FindProperty("steps").GetArrayElementAtIndex(owner).FindPropertyRelative(path).DeleteArrayElementAtIndex(index); attackSerialized.ApplyModifiedProperties(); attackDirty = true; RebuildCurrent(); }
        private void DuplicateNestedArray(int owner, string path, int index)
        { attackSerialized.Update(); var list = attackSerialized.FindProperty("steps").GetArrayElementAtIndex(owner).FindPropertyRelative(path); list.InsertArrayElementAtIndex(index); var copy = list.GetArrayElementAtIndex(index + 1); copy.FindPropertyRelative("slotId").stringValue += "_copy"; attackSerialized.ApplyModifiedProperties(); attackDirty = true; RebuildCurrent(); }

        private static int ParseArrayIndex(string path) { var marker = path.LastIndexOf("Array.data[", StringComparison.Ordinal); if (marker < 0) return -1; marker += 11; var end = path.IndexOf(']', marker); return int.TryParse(path.Substring(marker, end - marker), out var value) ? value : -1; }
        private static string ActivePatternLabel(MonsterActiveAttackPattern value) => value switch { MonsterActiveAttackPattern.Line => "일자 피해", MonsterActiveAttackPattern.Cone => "부채꼴 피해", MonsterActiveAttackPattern.SelfCircle => "내 주변 원형", MonsterActiveAttackPattern.FrontCircle => "내 앞 원형", MonsterActiveAttackPattern.PiercingProjectile => "관통 투사체", MonsterActiveAttackPattern.ExplosiveProjectile => "폭발 투사체", MonsterActiveAttackPattern.PiercingBeam => "관통 빔", _ => "즉발 마법" };
        private static string ActiveProgressionLabel(MonsterActiveAttackProgression value) => value switch { MonsterActiveAttackProgression.Instant => "한 번에", MonsterActiveAttackProgression.Forward => "앞으로 순차", MonsterActiveAttackProgression.LeftToRight => "왼쪽에서 오른쪽", MonsterActiveAttackProgression.RightToLeft => "오른쪽에서 왼쪽", _ => "바깥쪽으로 순차" };
        private static string ActiveTargetLabel(MonsterActiveTargetPolicy value) => value == MonsterActiveTargetPolicy.SameTarget ? "이전 Step과 같은 타깃" : "이전 Step과 다른 타깃";
        private static string ActiveHitLabel(MonsterActiveHitEffectType value) => value switch { MonsterActiveHitEffectType.Knockback => "넉백", MonsterActiveHitEffectType.Airborne => "에어본", MonsterActiveHitEffectType.Stun => "기절", MonsterActiveHitEffectType.Bleed => "출혈", MonsterActiveHitEffectType.Slow => "감속", _ => "끌어당기기" };
        private static string ActiveMagnitudeLabel(MonsterActiveHitEffectType value) => value switch { MonsterActiveHitEffectType.Knockback => "밀어내는 거리(m)", MonsterActiveHitEffectType.Airborne => "띄우는 높이", MonsterActiveHitEffectType.Slow => "감속 비율(0~1)", MonsterActiveHitEffectType.Pull => "끌어당기는 거리(m)", _ => "효과 강도" };
        private static string ActiveEventLabel(MonsterActivePresentationEvent value) => value switch { MonsterActivePresentationEvent.Telegraph => "판정 예고", MonsterActivePresentationEvent.Launch => "공격 발사", MonsterActivePresentationEvent.Travel => "이동 중", MonsterActivePresentationEvent.Impact => "실제 명중", MonsterActivePresentationEvent.TeleportExit => "순간이동 출발", MonsterActivePresentationEvent.TeleportEnter => "순간이동 도착", MonsterActivePresentationEvent.MotionStart => "모션 시작", MonsterActivePresentationEvent.DeliverySpawn => "이동체 생성", MonsterActivePresentationEvent.AreaResolved => "범위 판정 완료", MonsterActivePresentationEvent.DeliveryEnd => "이동체 종료", _ => "Step 종료" };
        private static string ActiveAnchorLabel(MonsterActivePresentationAnchor value) => value switch { MonsterActivePresentationAnchor.CasterRoot => "시전자 중심", MonsterActivePresentationAnchor.AttackOrigin => "공격 시작점", MonsterActivePresentationAnchor.TargetPoint => "타깃 지점", MonsterActivePresentationAnchor.MarkerSocket => "Marker 소켓", MonsterActivePresentationAnchor.ProjectileRoot => "투사체 중심", MonsterActivePresentationAnchor.TargetRoot => "타깃 중심", MonsterActivePresentationAnchor.HitPoint => "실제 명중점", MonsterActivePresentationAnchor.AreaCenter => "범위 중심", _ => "이동 경로 시작점" };
        private static string ActiveMultiplicityLabel(MonsterActivePresentationMultiplicity value) => value switch { MonsterActivePresentationMultiplicity.OncePerStep => "Step당 한 번", MonsterActivePresentationMultiplicity.OncePerProjectile => "투사체마다", MonsterActivePresentationMultiplicity.PerTargetHit => "명중 대상마다", MonsterActivePresentationMultiplicity.PerDamageStage => "피해 단계마다", _ => "종료까지 지속" };
        private static string ActiveAttachmentLabel(MonsterActivePresentationAttachment value) => value switch { MonsterActivePresentationAttachment.World => "월드 고정", MonsterActivePresentationAttachment.FollowAnchor => "기준 위치 추적", _ => "이동체 외형" };
        private static string ActiveEndLabel(MonsterActivePresentationEndPolicy value) => value switch { MonsterActivePresentationEndPolicy.Timed => "설정 시간", MonsterActivePresentationEndPolicy.DeliveryEnd => "이동체 종료", MonsterActivePresentationEndPolicy.StepEnd => "Step 종료", MonsterActivePresentationEndPolicy.MotionEnd => "모션 종료", _ => "파티클 길이" };
    }
}
