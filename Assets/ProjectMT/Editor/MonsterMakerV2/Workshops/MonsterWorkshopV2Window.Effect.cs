using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    public sealed partial class MonsterWorkshopV2Window
    {
        partial void BuildEffectAssembler()
        {
            effectSerialized.Update();
            var info = Section("1. 프리셋 정보", "발동 규칙은 공격 액티브와 같고, 이 화면에서는 지원·수호·디버프 효과만 조립합니다.");
            info.Add(BoundProperty(effectSerialized, effectSerialized.FindProperty("profileId"), "프리셋 ID"));
            info.Add(BoundProperty(effectSerialized, effectSerialized.FindProperty("displayName"), "표시 이름"));
            info.Add(BoundProperty(effectSerialized, effectSerialized.FindProperty("description"), "기획 메모"));
            info.Add(EffectEnumPopup(effectSerialized.FindProperty("role"), "역할", EnumValues<MonsterEffectActiveRole>(), EffectRoleLabel, ReconcileEffectRole));
            assemblerScroll.Add(info);

            var groups = effectSerialized.FindProperty("groups");
            var section = Section($"2. 효과 묶음 · {groups.arraySize}개", "한 묶음 안의 효과는 같은 시점에 같은 대상 규칙으로 적용됩니다.");
            for (var index = 0; index < groups.arraySize; index++) BuildEffectGroup(section, groups, index);
            section.Add(AddButton("+ 효과 묶음 추가", AddEffectGroup));
            assemblerScroll.Add(section);
        }

        private void BuildEffectGroup(VisualElement parent, SerializedProperty groups, int index)
        {
            var group = groups.GetArrayElementAtIndex(index);
            var card = new VisualElement(); card.AddToClassList("sub-card");
            card.Add(CardHeader($"#{index + 1:00} {group.FindPropertyRelative("displayName").stringValue}",
                SmallButton("▲", () => MoveEffectArray("groups", index, -1), false, index > 0), SmallButton("▼", () => MoveEffectArray("groups", index, 1), false, index < groups.arraySize - 1),
                SmallButton("복제", () => DuplicateEffectArray("groups", index, "groupId")), SmallButton("삭제", () => DeleteEffectGroup(index), true, groups.arraySize > 1)));
            card.Add(BoundProperty(effectSerialized, group.FindPropertyRelative("groupId"), "묶음 ID"));
            card.Add(BoundProperty(effectSerialized, group.FindPropertyRelative("displayName"), "표시 이름"));
            card.Add(EffectOptionalFloat(group.FindPropertyRelative("delayAfterPrevious"), index == 0 ? "스킬 발동 후 딜레이" : "이전 묶음 후 딜레이", "딜레이 사용", 0.1f));

            var role = (MonsterEffectActiveRole)effectSerialized.FindProperty("role").enumValueIndex;
            var targets = EnumValues<MonsterSkillTargetType>().Where(value => MonsterEffectActiveProfile.IsTargetAllowed(role, value)).ToList();
            card.Add(EffectEnumPopup(group.FindPropertyRelative("target"), "효과 대상", targets, EffectTargetLabel, ScheduleRebuild));
            var target = (MonsterSkillTargetType)group.FindPropertyRelative("target").enumValueIndex;
            if (target is MonsterSkillTargetType.NearbyAllies or MonsterSkillTargetType.TargetAreaEnemies)
            {
                card.Add(BoundProperty(effectSerialized, group.FindPropertyRelative("radius"), "범위 반경(m)"));
                card.Add(BoundProperty(effectSerialized, group.FindPropertyRelative("maxTargets"), "최대 대상"));
            }
            if (target != MonsterSkillTargetType.Self)
                card.Add(BoundProperty(effectSerialized, group.FindPropertyRelative("includeCaster"), "시전자도 포함"));

            BuildSkillEffects(card, group, index, role);
            BuildEffectPresentationSlots(card, group, index);
            parent.Add(card);
        }

        private void BuildSkillEffects(VisualElement card, SerializedProperty group, int groupIndex, MonsterEffectActiveRole role)
        {
            var list = group.FindPropertyRelative("effects");
            var section = Section($"3. 적용 효과 · {list.arraySize}개", "역할에 맞는 효과만 선택할 수 있습니다.");
            var allowed = EnumValues<MonsterSkillEffectType>().Where(value => MonsterEffectActiveProfile.IsEffectAllowed(role, value)).ToList();
            for (var index = 0; index < list.arraySize; index++)
            {
                var captured = index; var effect = list.GetArrayElementAtIndex(index);
                var shell = new VisualElement(); shell.AddToClassList("sub-card");
                shell.Add(CardHeader($"효과 {index + 1:00}", SmallButton("▲", () => MoveEffectNested(groupIndex, "effects", captured, -1), false, index > 0),
                    SmallButton("▼", () => MoveEffectNested(groupIndex, "effects", captured, 1), false, index < list.arraySize - 1),
                    SmallButton("복제", () => DuplicateEffectNested(groupIndex, "effects", captured, "effectId")),
                    SmallButton("삭제", () => DeleteEffectNested(groupIndex, "effects", captured, true), true, list.arraySize > 1)));
                shell.Add(BoundProperty(effectSerialized, effect.FindPropertyRelative("effectId"), "효과 ID"));
                shell.Add(EffectEnumPopup(effect.FindPropertyRelative("type"), "효과 종류", allowed, SkillEffectLabel, ScheduleRebuild));
                shell.Add(EffectEnumPopup(effect.FindPropertyRelative("valueSource"), "수치 기준", EnumValues<MonsterSkillValueSource>(), EffectValueSourceLabel));
                shell.Add(EffectEnumPopup(effect.FindPropertyRelative("magnitudeMode"), "수치 방식", EnumValues<MonsterSkillMagnitudeMode>(),
                    value => value == MonsterSkillMagnitudeMode.Fixed ? "고정값" : "범위 무작위", ScheduleRebuild));
                shell.Add(BoundProperty(effectSerialized, effect.FindPropertyRelative("magnitude"), "기본 수치"));
                if ((MonsterSkillMagnitudeMode)effect.FindPropertyRelative("magnitudeMode").enumValueIndex == MonsterSkillMagnitudeMode.RandomRange)
                    shell.Add(BoundProperty(effectSerialized, effect.FindPropertyRelative("maximumMagnitude"), "최대 수치"));
                var type = (MonsterSkillEffectType)effect.FindPropertyRelative("type").enumValueIndex;
                shell.Add(EffectOptionalFloat(effect.FindPropertyRelative("delay"), "효과 시작 딜레이(초)", "시작 딜레이 사용", 0.1f));
                if (EffectUsesDuration(type) || type == MonsterSkillEffectType.Heal)
                    shell.Add(EffectOptionalFloat(effect.FindPropertyRelative("duration"), "지속 시간(초)", "지속시간 사용", 3f));
                if (type == MonsterSkillEffectType.Heal && effect.FindPropertyRelative("duration").floatValue > 0f)
                {
                    shell.Add(BoundProperty(effectSerialized, effect.FindPropertyRelative("repeatInterval"), "회복 간격(초)"));
                    shell.Add(BoundProperty(effectSerialized, effect.FindPropertyRelative("repeatCount"), "회복 횟수"));
                }
                if (EffectUsesDuration(type))
                    shell.Add(EffectEnumPopup(effect.FindPropertyRelative("stackPolicy"), "중첩 규칙", EnumValues<MonsterSkillStackPolicy>(), StackPolicyLabel));
                section.Add(shell);
            }
            section.Add(AddButton("+ 효과 추가", () => AddSkillEffect(groupIndex, role)));
            card.Add(section);
        }

        private void BuildEffectPresentationSlots(VisualElement card, SerializedProperty group, int groupIndex)
        {
            var list = group.FindPropertyRelative("presentationSlots");
            var section = Section($"4. VFX/SFX 공간 계약 · {list.arraySize}개", "시전자 VFX와 효과받은 대상 VFX를 각각 공간으로 정의합니다.");
            for (var index = 0; index < list.arraySize; index++)
            {
                var captured = index; var slot = list.GetArrayElementAtIndex(index);
                var shell = new VisualElement(); shell.AddToClassList("sub-card");
                shell.Add(CardHeader($"공간 {index + 1:00} · {slot.FindPropertyRelative("displayName").stringValue}",
                    SmallButton("▲", () => MoveEffectNested(groupIndex, "presentationSlots", captured, -1), false, index > 0),
                    SmallButton("▼", () => MoveEffectNested(groupIndex, "presentationSlots", captured, 1), false, index < list.arraySize - 1),
                    SmallButton("복제", () => DuplicateEffectNested(groupIndex, "presentationSlots", captured, "slotId")),
                    SmallButton("삭제", () => DeleteEffectNested(groupIndex, "presentationSlots", captured, false), true)));
                shell.Add(BoundProperty(effectSerialized, slot.FindPropertyRelative("slotId"), "공간 ID"));
                shell.Add(BoundProperty(effectSerialized, slot.FindPropertyRelative("displayName"), "표시 이름"));
                shell.Add(EffectEnumPopup(slot.FindPropertyRelative("timing"), "발생 시점", EnumValues<MonsterActivePresentationEvent>(), ActiveEventLabel));
                shell.Add(EffectEnumPopup(slot.FindPropertyRelative("anchor"), "기준 위치", EnumValues<MonsterActivePresentationAnchor>(), ActiveAnchorLabel));
                shell.Add(EffectEnumPopup(slot.FindPropertyRelative("multiplicity"), "재생 횟수", EnumValues<MonsterActivePresentationMultiplicity>(), ActiveMultiplicityLabel));
                shell.Add(EffectEnumPopup(slot.FindPropertyRelative("attachment"), "부착 방식", EnumValues<MonsterActivePresentationAttachment>(), ActiveAttachmentLabel));
                shell.Add(EffectEnumPopup(slot.FindPropertyRelative("endPolicy"), "종료 규칙", EnumValues<MonsterActivePresentationEndPolicy>(), ActiveEndLabel));
                shell.Add(BoundProperty(effectSerialized, slot.FindPropertyRelative("description"), "제작 메모"));
                shell.Add(BoundProperty(effectSerialized, slot.FindPropertyRelative("useDuration"), "지속시간 직접 사용", ScheduleRebuild));
                if (slot.FindPropertyRelative("useDuration").boolValue)
                    shell.Add(BoundProperty(effectSerialized, slot.FindPropertyRelative("duration"), "지속 시간(초)"));
                section.Add(shell);
            }
            section.Add(AddButton("+ VFX/SFX 공간 추가", () => AddEffectSlot(groupIndex)));
            card.Add(section);
        }

        private PopupField<T> EffectEnumPopup<T>(SerializedProperty property, string label, List<T> choices, Func<T, string> format, Action changed = null) where T : struct
        {
            var current = (T)Enum.ToObject(typeof(T), property.enumValueIndex);
            if (!choices.Contains(current)) { current = choices[0]; property.enumValueIndex = Convert.ToInt32(current); effectSerialized.ApplyModifiedProperties(); }
            var field = new PopupField<T>(label, choices, current, format, format); field.AddToClassList("editor-field"); var path = property.propertyPath;
            field.RegisterValueChangedCallback(evt =>
            {
                if (suppressUiCallbacks) return;
                effectSerialized.Update(); effectSerialized.FindProperty(path).enumValueIndex = Convert.ToInt32(evt.newValue); effectSerialized.ApplyModifiedProperties();
                effectDirty = true; changed?.Invoke(); if (changed == null) RefreshState();
            });
            return field;
        }

        private VisualElement EffectOptionalFloat(SerializedProperty property, string valueLabel, string toggleLabel, float defaultValue)
        {
            var shell = new VisualElement(); var path = property.propertyPath;
            var toggle = new Toggle(toggleLabel) { value = property.floatValue > 0f }; toggle.AddToClassList("editor-field"); shell.Add(toggle);
            if (toggle.value) shell.Add(BoundProperty(effectSerialized, property, valueLabel));
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (suppressUiCallbacks) return;
                effectSerialized.Update(); var live = effectSerialized.FindProperty(path); live.floatValue = evt.newValue ? Mathf.Max(defaultValue, live.floatValue) : 0f;
                effectSerialized.ApplyModifiedProperties(); effectDirty = true; ScheduleRebuild();
            });
            return shell;
        }

        private void ReconcileEffectRole()
        {
            effectSerialized.Update(); var role = (MonsterEffectActiveRole)effectSerialized.FindProperty("role").enumValueIndex;
            var groups = effectSerialized.FindProperty("groups");
            var targetDefault = role == MonsterEffectActiveRole.Support ? MonsterSkillTargetType.AllAllies : role == MonsterEffectActiveRole.Guard ? MonsterSkillTargetType.Self : MonsterSkillTargetType.TargetAreaEnemies;
            var effectDefault = role == MonsterEffectActiveRole.Support ? MonsterSkillEffectType.Heal : role == MonsterEffectActiveRole.Guard ? MonsterSkillEffectType.Shield : MonsterSkillEffectType.DefenseDebuff;
            for (var groupIndex = 0; groupIndex < groups.arraySize; groupIndex++)
            {
                var group = groups.GetArrayElementAtIndex(groupIndex); var target = group.FindPropertyRelative("target");
                if (!MonsterEffectActiveProfile.IsTargetAllowed(role, (MonsterSkillTargetType)target.enumValueIndex)) target.enumValueIndex = (int)targetDefault;
                var effects = group.FindPropertyRelative("effects");
                for (var effectIndex = 0; effectIndex < effects.arraySize; effectIndex++)
                {
                    var type = effects.GetArrayElementAtIndex(effectIndex).FindPropertyRelative("type");
                    if (!MonsterEffectActiveProfile.IsEffectAllowed(role, (MonsterSkillEffectType)type.enumValueIndex)) type.enumValueIndex = (int)effectDefault;
                }
            }
            effectSerialized.ApplyModifiedProperties(); effectDirty = true;
            effectMessage = "역할에 맞지 않는 대상·효과만 안전한 기본값으로 정리했습니다."; RebuildCurrent();
        }

        private void AddEffectGroup()
        {
            effectSerialized.Update(); var groups = effectSerialized.FindProperty("groups"); var index = groups.arraySize;
            groups.InsertArrayElementAtIndex(index); var group = groups.GetArrayElementAtIndex(index); var role = (MonsterEffectActiveRole)effectSerialized.FindProperty("role").enumValueIndex;
            group.FindPropertyRelative("groupId").stringValue = $"group_{index + 1:00}"; group.FindPropertyRelative("displayName").stringValue = "새 효과 묶음";
            group.FindPropertyRelative("delayAfterPrevious").floatValue = index == 0 ? 0f : 0.12f;
            group.FindPropertyRelative("target").enumValueIndex = role == MonsterEffectActiveRole.Support ? (int)MonsterSkillTargetType.AllAllies : role == MonsterEffectActiveRole.Guard ? (int)MonsterSkillTargetType.Self : (int)MonsterSkillTargetType.TargetAreaEnemies;
            group.FindPropertyRelative("includeCaster").boolValue = true; group.FindPropertyRelative("radius").floatValue = 5f; group.FindPropertyRelative("maxTargets").intValue = 8;
            group.FindPropertyRelative("effects").arraySize = 0;
            var slots = group.FindPropertyRelative("presentationSlots"); slots.arraySize = 2;
            ConfigureEffectSlot(slots.GetArrayElementAtIndex(0), "caster_vfx", "시전자 VFX",
                MonsterActivePresentationEvent.Launch, MonsterActivePresentationAnchor.CasterRoot);
            ConfigureEffectSlot(slots.GetArrayElementAtIndex(1), "target_vfx", "효과 대상 VFX",
                MonsterActivePresentationEvent.Impact, MonsterActivePresentationAnchor.TargetRoot);
            effectSerialized.ApplyModifiedProperties(); AddSkillEffect(index, role);
        }

        private void AddSkillEffect(int groupIndex, MonsterEffectActiveRole role)
        {
            effectSerialized.Update(); var list = effectSerialized.FindProperty("groups").GetArrayElementAtIndex(groupIndex).FindPropertyRelative("effects"); var index = list.arraySize;
            list.InsertArrayElementAtIndex(index); var effect = list.GetArrayElementAtIndex(index);
            effect.FindPropertyRelative("effectId").stringValue = $"effect_{index + 1:00}";
            effect.FindPropertyRelative("type").enumValueIndex = role == MonsterEffectActiveRole.Support ? (int)MonsterSkillEffectType.Heal : role == MonsterEffectActiveRole.Guard ? (int)MonsterSkillEffectType.Shield : (int)MonsterSkillEffectType.DefenseDebuff;
            effect.FindPropertyRelative("valueSource").enumValueIndex = (int)MonsterSkillValueSource.AttackPowerRatio; effect.FindPropertyRelative("magnitude").floatValue = 1f;
            effect.FindPropertyRelative("maximumMagnitude").floatValue = 1f; effect.FindPropertyRelative("repeatCount").intValue = 1;
            effect.FindPropertyRelative("stackPolicy").enumValueIndex = (int)MonsterSkillStackPolicy.RefreshDuration;
            effectSerialized.ApplyModifiedProperties(); effectDirty = true; RebuildCurrent();
        }

        private void AddEffectSlot(int groupIndex)
        {
            effectSerialized.Update(); var list = effectSerialized.FindProperty("groups").GetArrayElementAtIndex(groupIndex).FindPropertyRelative("presentationSlots"); var index = list.arraySize;
            list.InsertArrayElementAtIndex(index); var slot = list.GetArrayElementAtIndex(index);
            slot.FindPropertyRelative("slotId").stringValue = $"effect_vfx_{index + 1:00}"; slot.FindPropertyRelative("displayName").stringValue = index == 0 ? "시전자 VFX" : "효과 대상 VFX";
            slot.FindPropertyRelative("timing").enumValueIndex = (int)MonsterActivePresentationEvent.Launch;
            slot.FindPropertyRelative("anchor").enumValueIndex = index == 0 ? (int)MonsterActivePresentationAnchor.CasterRoot : (int)MonsterActivePresentationAnchor.TargetRoot;
            slot.FindPropertyRelative("description").stringValue = "Monster Maker V2에서 몬스터 고유 VFX/SFX를 연결합니다."; slot.FindPropertyRelative("duration").floatValue = 1f;
            effectSerialized.ApplyModifiedProperties(); effectDirty = true; RebuildCurrent();
        }

        private static void ConfigureEffectSlot(SerializedProperty slot, string id, string title,
            MonsterActivePresentationEvent timing, MonsterActivePresentationAnchor anchor)
        {
            slot.FindPropertyRelative("slotId").stringValue = id;
            slot.FindPropertyRelative("displayName").stringValue = title;
            slot.FindPropertyRelative("timing").enumValueIndex = (int)timing;
            slot.FindPropertyRelative("anchor").enumValueIndex = (int)anchor;
            slot.FindPropertyRelative("multiplicity").enumValueIndex = (int)MonsterActivePresentationMultiplicity.OncePerStep;
            slot.FindPropertyRelative("attachment").enumValueIndex = (int)MonsterActivePresentationAttachment.World;
            slot.FindPropertyRelative("endPolicy").enumValueIndex = (int)MonsterActivePresentationEndPolicy.Timed;
            slot.FindPropertyRelative("description").stringValue = "Monster Maker V2에서 몬스터 고유 VFX/SFX를 연결합니다.";
            slot.FindPropertyRelative("useDuration").boolValue = false;
            slot.FindPropertyRelative("duration").floatValue = 1f;
        }

        private void MoveEffectArray(string path, int index, int direction)
        { effectSerialized.Update(); var list = effectSerialized.FindProperty(path); var target = Mathf.Clamp(index + direction, 0, list.arraySize - 1); if (target != index) list.MoveArrayElement(index, target); effectSerialized.ApplyModifiedProperties(); effectDirty = true; RebuildCurrent(); }
        private void DuplicateEffectArray(string path, int index, string id)
        { effectSerialized.Update(); var list = effectSerialized.FindProperty(path); list.InsertArrayElementAtIndex(index); list.GetArrayElementAtIndex(index + 1).FindPropertyRelative(id).stringValue += "_copy"; effectSerialized.ApplyModifiedProperties(); effectDirty = true; RebuildCurrent(); }
        private void DeleteEffectGroup(int index)
        { effectSerialized.Update(); var list = effectSerialized.FindProperty("groups"); if (list.arraySize <= 1) { effectMessage = "오류: 효과 묶음은 하나 이상 필요합니다."; RefreshState(); return; } list.DeleteArrayElementAtIndex(index); effectSerialized.ApplyModifiedProperties(); effectDirty = true; RebuildCurrent(); }
        private void MoveEffectNested(int owner, string path, int index, int direction)
        { effectSerialized.Update(); var list = effectSerialized.FindProperty("groups").GetArrayElementAtIndex(owner).FindPropertyRelative(path); var target = Mathf.Clamp(index + direction, 0, list.arraySize - 1); if (target != index) list.MoveArrayElement(index, target); effectSerialized.ApplyModifiedProperties(); effectDirty = true; RebuildCurrent(); }
        private void DuplicateEffectNested(int owner, string path, int index, string id)
        { effectSerialized.Update(); var list = effectSerialized.FindProperty("groups").GetArrayElementAtIndex(owner).FindPropertyRelative(path); list.InsertArrayElementAtIndex(index); list.GetArrayElementAtIndex(index + 1).FindPropertyRelative(id).stringValue += "_copy"; effectSerialized.ApplyModifiedProperties(); effectDirty = true; RebuildCurrent(); }
        private void DeleteEffectNested(int owner, string path, int index, bool requireOne)
        { effectSerialized.Update(); var list = effectSerialized.FindProperty("groups").GetArrayElementAtIndex(owner).FindPropertyRelative(path); if (requireOne && list.arraySize <= 1) { effectMessage = "오류: 효과는 하나 이상 필요합니다."; RefreshState(); return; } list.DeleteArrayElementAtIndex(index); effectSerialized.ApplyModifiedProperties(); effectDirty = true; RebuildCurrent(); }

        private static bool EffectUsesDuration(MonsterSkillEffectType type) => type is MonsterSkillEffectType.Shield or MonsterSkillEffectType.AttackBuff or MonsterSkillEffectType.DefenseBuff or MonsterSkillEffectType.AttackSpeedBuff or MonsterSkillEffectType.AttackDebuff or MonsterSkillEffectType.DefenseDebuff or MonsterSkillEffectType.AttackSpeedDebuff or MonsterSkillEffectType.MoveSpeedDebuff or MonsterSkillEffectType.Mark or MonsterSkillEffectType.Slow or MonsterSkillEffectType.Stun or MonsterSkillEffectType.Pull or MonsterSkillEffectType.Taunt or MonsterSkillEffectType.DamageReduction;
        private static string EffectRoleLabel(MonsterEffectActiveRole value) => value switch { MonsterEffectActiveRole.Support => "지원", MonsterEffectActiveRole.Guard => "수호", _ => "디버프" };
        private static string EffectTargetLabel(MonsterSkillTargetType value) => value switch { MonsterSkillTargetType.Self => "내 자신", MonsterSkillTargetType.CurrentTarget => "현재 타깃", MonsterSkillTargetType.NearestEnemy => "가장 가까운 적", MonsterSkillTargetType.FarthestEnemy => "가장 먼 적", MonsterSkillTargetType.LowestHealthEnemy => "체력이 가장 낮은 적", MonsterSkillTargetType.HighestAttackEnemy => "공격력이 가장 높은 적", MonsterSkillTargetType.RangedEnemyFirst => "원거리 적 우선", MonsterSkillTargetType.LowestHealthAlly => "체력이 가장 낮은 아군", MonsterSkillTargetType.HighestAttackAlly => "공격력이 가장 높은 아군", MonsterSkillTargetType.NearbyAllies => "주변 아군", MonsterSkillTargetType.AllAllies => "모든 아군", MonsterSkillTargetType.TargetAreaEnemies => "타깃 주변 적", _ => value.ToString() };
        private static string SkillEffectLabel(MonsterSkillEffectType value) => value switch { MonsterSkillEffectType.Heal => "회복", MonsterSkillEffectType.Shield => "보호막", MonsterSkillEffectType.AttackBuff => "공격력 증가", MonsterSkillEffectType.DefenseBuff => "방어력 증가", MonsterSkillEffectType.AttackSpeedBuff => "공격속도 증가", MonsterSkillEffectType.EnergyGain => "기력 회복", MonsterSkillEffectType.AttackDebuff => "공격력 감소", MonsterSkillEffectType.DefenseDebuff => "방어력 감소", MonsterSkillEffectType.AttackSpeedDebuff => "공격속도 감소", MonsterSkillEffectType.MoveSpeedDebuff => "이동속도 감소", MonsterSkillEffectType.Mark => "표식", MonsterSkillEffectType.Slow => "감속", MonsterSkillEffectType.Stun => "기절", MonsterSkillEffectType.Pull => "끌어당기기", MonsterSkillEffectType.EnergyDrain => "기력 감소", MonsterSkillEffectType.Taunt => "도발", MonsterSkillEffectType.DamageReduction => "피해 감소", _ => value.ToString() };
        private static string EffectValueSourceLabel(MonsterSkillValueSource value) => value switch { MonsterSkillValueSource.Flat => "고정 수치", MonsterSkillValueSource.AttackPowerRatio => "시전자 공격력 비율", MonsterSkillValueSource.MaxHealthRatio => "시전자 최대 체력 비율", MonsterSkillValueSource.TargetMaxHealthRatio => "대상 최대 체력 비율", MonsterSkillValueSource.TargetMissingHealthRatio => "대상 잃은 체력 비율", MonsterSkillValueSource.TargetEnergyCapacityRatio => "대상 최대 기력 비율", _ => "받은 피해 비율" };
        private static string StackPolicyLabel(MonsterSkillStackPolicy value) => value switch { MonsterSkillStackPolicy.Replace => "새 효과로 교체", MonsterSkillStackPolicy.RefreshDuration => "지속시간 갱신", MonsterSkillStackPolicy.Stack => "중첩", _ => "강한 효과 우선" };
    }
}
