using System;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public sealed partial class MonsterEffectActiveWorkshopWindow
    {
        private void NormalizeForRole(MonsterEffectActiveRole role)
        {
            var groups = serializedProfile.FindProperty("groups");
            var allowedTargets = TargetsFor(role);
            var effects = EffectsFor(role);
            for (var groupIndex = 0; groupIndex < groups.arraySize; groupIndex++)
            {
                var group = groups.GetArrayElementAtIndex(groupIndex);
                var target = (MonsterSkillTargetType)group.FindPropertyRelative("target").enumValueIndex;
                if (!allowedTargets.Contains(target))
                {
                    group.FindPropertyRelative("target").enumValueIndex = (int)allowedTargets[0];
                }
                var list = group.FindPropertyRelative("effects");
                for (var effectIndex = 0; effectIndex < list.arraySize; effectIndex++)
                {
                    var effect = list.GetArrayElementAtIndex(effectIndex);
                    var type = (MonsterSkillEffectType)effect.FindPropertyRelative("type").enumValueIndex;
                    if (!effects.Contains(type))
                    {
                        ConfigureEffectDefaults(effect, effects[0], effectIndex);
                    }
                }
            }
        }

        private void AddGroup(SerializedProperty groups)
        {
            var index = groups.arraySize;
            var groupId = BuildUniqueId(groups, "groupId", "group", false);
            groups.InsertArrayElementAtIndex(index);
            var group = groups.GetArrayElementAtIndex(index);
            group.FindPropertyRelative("groupId").stringValue = groupId;
            group.FindPropertyRelative("displayName").stringValue = $"효과 묶음 {index + 1}";
            group.FindPropertyRelative("delayAfterPrevious").floatValue = index == 0 ? 0f : 0.15f;
            var role = (MonsterEffectActiveRole)serializedProfile.FindProperty("role").enumValueIndex;
            group.FindPropertyRelative("target").enumValueIndex =
                (int)(role == MonsterEffectActiveRole.Debuff
                    ? MonsterSkillTargetType.TargetAreaEnemies
                    : MonsterSkillTargetType.AllAllies);
            group.FindPropertyRelative("includeCaster").boolValue = true;
            group.FindPropertyRelative("radius").floatValue = 5f;
            group.FindPropertyRelative("maxTargets").intValue = 8;
            group.FindPropertyRelative("effects").ClearArray();
            AddEffect(group.FindPropertyRelative("effects"));
            group.FindPropertyRelative("presentationSlots").ClearArray();
            AddSlot(group.FindPropertyRelative("presentationSlots"), false);
            OnChanged();
        }

        private void AddEffect(SerializedProperty effects)
        {
            var index = effects.arraySize;
            effects.InsertArrayElementAtIndex(index);
            var effect = effects.GetArrayElementAtIndex(index);
            var role = (MonsterEffectActiveRole)serializedProfile.FindProperty("role").enumValueIndex;
            ConfigureEffectDefaults(effect, EffectsFor(role)[0], index);
            OnChanged();
        }

        private static void ConfigureEffectDefaults(
            SerializedProperty effect,
            MonsterSkillEffectType type,
            int index)
        {
            effect.FindPropertyRelative("effectId").stringValue =
                $"effect_{index + 1:00}_{type.ToString().ToLowerInvariant()}";
            effect.FindPropertyRelative("type").enumValueIndex = (int)type;
            effect.FindPropertyRelative("magnitudeMode").enumValueIndex =
                (int)MonsterSkillMagnitudeMode.Fixed;
            effect.FindPropertyRelative("delay").floatValue = 0f;
            effect.FindPropertyRelative("radius").floatValue = 0f;
            effect.FindPropertyRelative("maxTargets").intValue = 1;
            effect.FindPropertyRelative("repeatCount").intValue = 1;
            effect.FindPropertyRelative("stackPolicy").enumValueIndex =
                (int)MonsterSkillStackPolicy.StrongestWins;
            effect.FindPropertyRelative("repeatInterval").floatValue = 1f;

            var source = MonsterSkillValueSource.Flat;
            var magnitude = 0.2f;
            var duration = UsesDuration(type) ? 5f : 0f;
            if (type is MonsterSkillEffectType.Heal or MonsterSkillEffectType.Shield)
            {
                source = MonsterSkillValueSource.AttackPowerRatio;
                magnitude = type == MonsterSkillEffectType.Heal ? 1.5f : 2f;
            }
            else if (type is MonsterSkillEffectType.EnergyGain or MonsterSkillEffectType.EnergyDrain)
            {
                source = MonsterSkillValueSource.TargetEnergyCapacityRatio;
                magnitude = 0.15f;
            }
            else if (type == MonsterSkillEffectType.Taunt)
            {
                magnitude = 0f;
                duration = 3f;
            }
            else if (type == MonsterSkillEffectType.Stun)
            {
                magnitude = 0f;
                duration = 1.2f;
            }
            else if (type == MonsterSkillEffectType.Pull)
            {
                magnitude = 1.5f;
                duration = 0.35f;
            }
            effect.FindPropertyRelative("valueSource").enumValueIndex = (int)source;
            effect.FindPropertyRelative("magnitude").floatValue = magnitude;
            effect.FindPropertyRelative("maximumMagnitude").floatValue = magnitude;
            effect.FindPropertyRelative("duration").floatValue = duration;
        }

        private void AddSlot(SerializedProperty slots, bool duration)
        {
            var index = slots.arraySize;
            var slotId = BuildUniqueId(
                slots,
                "slotId",
                duration ? "target_loop" : "apply",
                true);
            slots.InsertArrayElementAtIndex(index);
            var slot = slots.GetArrayElementAtIndex(index);
            slot.FindPropertyRelative("slotId").stringValue = slotId;
            slot.FindPropertyRelative("displayName").stringValue = duration
                ? "대상 지속 효과"
                : index == 0 ? "효과 적용" : $"효과 적용 {index + 1}";
            slot.FindPropertyRelative("timing").enumValueIndex =
                (int)MonsterActivePresentationEvent.AreaResolved;
            slot.FindPropertyRelative("anchor").enumValueIndex =
                (int)(duration
                    ? MonsterActivePresentationAnchor.TargetRoot
                    : MonsterActivePresentationAnchor.AreaCenter);
            slot.FindPropertyRelative("multiplicity").enumValueIndex =
                (int)(duration
                    ? MonsterActivePresentationMultiplicity.ContinuousUntilEnd
                    : MonsterActivePresentationMultiplicity.OncePerStep);
            slot.FindPropertyRelative("attachment").enumValueIndex =
                (int)(duration
                    ? MonsterActivePresentationAttachment.FollowAnchor
                    : MonsterActivePresentationAttachment.World);
            slot.FindPropertyRelative("endPolicy").enumValueIndex =
                (int)(duration
                    ? MonsterActivePresentationEndPolicy.Timed
                    : MonsterActivePresentationEndPolicy.ParticleDuration);
            slot.FindPropertyRelative("description").stringValue = duration
                ? "지속 시간 동안 대상을 따라가는 Loop VFX/SFX 공간"
                : "효과가 적용되는 순간 재생하는 VFX/SFX 공간";
            slot.FindPropertyRelative("useDuration").boolValue = duration;
            slot.FindPropertyRelative("duration").floatValue = duration ? 5f : 1f;
            OnChanged();
        }

        private void MoveGroupAndCommit(SerializedProperty groups, int from, int to)
        {
            if (groups == null || from < 0 || from >= groups.arraySize ||
                to < 0 || to >= groups.arraySize) return;
            groups.MoveArrayElement(from, to);
            OnChanged();
        }

        private void DuplicateGroupAndCommit(SerializedProperty groups, int index, string sourceName)
        {
            if (groups == null || index < 0 || index >= groups.arraySize) return;
            var groupId = BuildUniqueId(groups, "groupId", "group", false);
            groups.InsertArrayElementAtIndex(index);
            var copy = groups.GetArrayElementAtIndex(index + 1);
            copy.FindPropertyRelative("groupId").stringValue = groupId;
            copy.FindPropertyRelative("displayName").stringValue =
                string.IsNullOrWhiteSpace(sourceName) ? "효과 묶음 복제" : sourceName + " 복제";
            OnChanged();
        }

        private void DeleteGroupAndCommit(SerializedProperty groups, int index)
        {
            if (groups == null || groups.arraySize <= 1 || index < 0 || index >= groups.arraySize) return;
            groups.DeleteArrayElementAtIndex(index);
            OnChanged();
        }

        private void DeleteEffectAndCommit(SerializedProperty effects, int index)
        {
            if (effects == null || effects.arraySize <= 1 || index < 0 || index >= effects.arraySize) return;
            effects.DeleteArrayElementAtIndex(index);
            OnChanged();
        }

        private void DeleteSlotAndCommit(SerializedProperty slots, int index)
        {
            if (slots == null || index < 0 || index >= slots.arraySize) return;
            slots.DeleteArrayElementAtIndex(index);
            OnChanged();
        }

        private static string BuildUniqueId(
            SerializedProperty items,
            string idProperty,
            string prefix,
            bool firstPlain)
        {
            for (var number = 1; number <= 999; number++)
            {
                var candidate = firstPlain && number == 1
                    ? prefix
                    : $"{prefix}_{number:00}";
                var used = false;
                for (var index = 0; index < items.arraySize; index++)
                {
                    if (string.Equals(
                            items.GetArrayElementAtIndex(index)
                                .FindPropertyRelative(idProperty)
                                .stringValue,
                            candidate,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        used = true;
                        break;
                    }
                }
                if (!used) return candidate;
            }
            return $"{prefix}_{Guid.NewGuid():N}";
        }

        private static MonsterEffectActiveGroup CreateDefaultGroup(int index)
        {
            var effect = new MonsterSkillEffect();
            effect.EditorConfigure(
                "effect_01_heal",
                MonsterSkillEffectType.Heal,
                MonsterSkillValueSource.AttackPowerRatio,
                1.5f);
            var slot = new MonsterActivePresentationSlot();
            slot.EditorConfigure(
                "apply",
                "효과 적용",
                MonsterActivePresentationEvent.AreaResolved,
                MonsterActivePresentationAnchor.AreaCenter,
                "효과 적용 순간의 공통 VFX/SFX 공간",
                false,
                1f,
                MonsterActivePresentationMultiplicity.OncePerStep,
                MonsterActivePresentationAttachment.World,
                MonsterActivePresentationEndPolicy.ParticleDuration);
            var group = new MonsterEffectActiveGroup();
            group.EditorConfigure(
                $"group_{index + 1:00}",
                "아군 지원",
                0f,
                MonsterSkillTargetType.AllAllies,
                true,
                5f,
                8,
                new[] { effect },
                new[] { slot });
            return group;
        }

        private static MonsterSkillTargetType[] TargetsFor(MonsterEffectActiveRole role) =>
            role switch
            {
                MonsterEffectActiveRole.Guard => GuardTargets,
                MonsterEffectActiveRole.Debuff => EnemyTargets,
                _ => AllyTargets
            };
        private static MonsterSkillEffectType[] EffectsFor(MonsterEffectActiveRole role) =>
            role switch
            {
                MonsterEffectActiveRole.Guard => GuardEffects,
                MonsterEffectActiveRole.Debuff => DebuffEffects,
                _ => SupportEffects
            };

        private static bool UsesDuration(MonsterSkillEffectType type) =>
            type is MonsterSkillEffectType.AttackBuff or MonsterSkillEffectType.DefenseBuff or
                MonsterSkillEffectType.AttackSpeedBuff or MonsterSkillEffectType.AttackDebuff or
                MonsterSkillEffectType.DefenseDebuff or MonsterSkillEffectType.AttackSpeedDebuff or
                MonsterSkillEffectType.MoveSpeedDebuff or MonsterSkillEffectType.DamageReduction or
                MonsterSkillEffectType.Mark or MonsterSkillEffectType.Slow or MonsterSkillEffectType.Stun or
                MonsterSkillEffectType.Pull or MonsterSkillEffectType.Taunt or MonsterSkillEffectType.Shield;

        private static void DrawPresentationEventPopup(SerializedProperty property)
        {
            var values = new[]
            {
                MonsterActivePresentationEvent.MotionStart,
                MonsterActivePresentationEvent.AreaResolved,
                MonsterActivePresentationEvent.StepEnd
            };
            var labels = new[] { "모션 시작", "효과 적용", "효과 묶음 종료" };
            var current = Array.IndexOf(values, (MonsterActivePresentationEvent)property.enumValueIndex);
            var selected = EditorGUILayout.Popup("발생 시점", Mathf.Max(0, current), labels);
            property.enumValueIndex = (int)values[Mathf.Clamp(selected, 0, values.Length - 1)];
        }

        private static void DrawPresentationAnchorPopup(SerializedProperty property)
        {
            var values = new[]
            {
                MonsterActivePresentationAnchor.CasterRoot,
                MonsterActivePresentationAnchor.AttackOrigin,
                MonsterActivePresentationAnchor.MarkerSocket,
                MonsterActivePresentationAnchor.TargetRoot,
                MonsterActivePresentationAnchor.HitPoint,
                MonsterActivePresentationAnchor.AreaCenter
            };
            var labels = new[] { "시전자 중심", "공격 시작점", "지정 소켓", "대상 중심", "타격 지점", "효과 영역 중심" };
            var current = Array.IndexOf(values, (MonsterActivePresentationAnchor)property.enumValueIndex);
            var selected = EditorGUILayout.Popup("기준 위치", Mathf.Max(0, current), labels);
            property.enumValueIndex = (int)values[Mathf.Clamp(selected, 0, values.Length - 1)];
        }

        private static string RoleBadge(MonsterEffectActiveRole role) => role switch
        {
            MonsterEffectActiveRole.Support => "지원",
            MonsterEffectActiveRole.Guard => "수호",
            MonsterEffectActiveRole.Debuff => "디버프",
            _ => role.ToString()
        };

        private static string RoleDescription(MonsterEffectActiveRole role) => role switch
        {
            MonsterEffectActiveRole.Support => "회복·공격력·공격속도·기력을 아군에게 제공합니다.",
            MonsterEffectActiveRole.Guard => "보호막·방어력·피해감소·도발로 아군을 보호합니다.",
            MonsterEffectActiveRole.Debuff => "적의 전투 능력·기력을 낮추고 표식·제어를 적용합니다.",
            _ => string.Empty
        };

        private static Color RoleColor(MonsterEffectActiveRole role) => role switch
        {
            MonsterEffectActiveRole.Support => new Color(0.29f, 0.78f, 0.58f),
            MonsterEffectActiveRole.Guard => new Color(0.33f, 0.62f, 0.95f),
            MonsterEffectActiveRole.Debuff => new Color(0.78f, 0.42f, 0.83f),
            _ => Color.white
        };

        private static string TargetLabel(MonsterSkillTargetType target) => target switch
        {
            MonsterSkillTargetType.Self => "자신",
            MonsterSkillTargetType.CurrentTarget => "현재 공격 대상",
            MonsterSkillTargetType.NearestEnemy => "가장 가까운 적",
            MonsterSkillTargetType.FarthestEnemy => "가장 먼 적",
            MonsterSkillTargetType.LowestHealthEnemy => "체력이 가장 낮은 적",
            MonsterSkillTargetType.HighestAttackEnemy => "공격력이 가장 높은 적",
            MonsterSkillTargetType.RangedEnemyFirst => "원거리 적 우선",
            MonsterSkillTargetType.LowestHealthAlly => "체력이 가장 낮은 아군",
            MonsterSkillTargetType.HighestAttackAlly => "공격력이 가장 높은 아군",
            MonsterSkillTargetType.NearbyAllies => "내 주변 아군",
            MonsterSkillTargetType.AllAllies => "모든 아군",
            MonsterSkillTargetType.TargetAreaEnemies => "대상 주변 적",
            _ => target.ToString()
        };

        private static string EffectLabel(MonsterSkillEffectType type) => type switch
        {
            MonsterSkillEffectType.Heal => "회복",
            MonsterSkillEffectType.Shield => "보호막",
            MonsterSkillEffectType.AttackBuff => "공격력 증가",
            MonsterSkillEffectType.DefenseBuff => "방어력 증가",
            MonsterSkillEffectType.AttackSpeedBuff => "공격속도 증가",
            MonsterSkillEffectType.AttackDebuff => "공격력 감소",
            MonsterSkillEffectType.DefenseDebuff => "방어력 감소",
            MonsterSkillEffectType.AttackSpeedDebuff => "공격속도 감소",
            MonsterSkillEffectType.MoveSpeedDebuff => "이동속도 감소",
            MonsterSkillEffectType.Mark => "받는 피해 증가",
            MonsterSkillEffectType.Slow => "둔화",
            MonsterSkillEffectType.Stun => "기절",
            MonsterSkillEffectType.Pull => "끌어당기기",
            MonsterSkillEffectType.Taunt => "도발",
            MonsterSkillEffectType.EnergyGain => "기력 회복",
            MonsterSkillEffectType.EnergyDrain => "기력 감소",
            MonsterSkillEffectType.DamageReduction => "받는 피해 감소",
            _ => type.ToString()
        };

        private static float EffectHealthRatio(MonsterEffectActiveRole role, float progress) =>
            role == MonsterEffectActiveRole.Support
                ? Mathf.Lerp(0.5f, 0.9f, progress)
                : role == MonsterEffectActiveRole.Debuff
                    ? Mathf.Lerp(0.8f, 0.58f, progress)
                    : 0.72f;

        private static float EffectEnergyRatio(MonsterEffectActiveRole role, float progress) =>
            role == MonsterEffectActiveRole.Support
                ? Mathf.Lerp(0.35f, 0.78f, progress)
                : role == MonsterEffectActiveRole.Debuff
                    ? Mathf.Lerp(0.7f, 0.3f, progress)
                    : 0.46f;
    }
}
