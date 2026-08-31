using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    internal enum BasicAttackWorkshopFamily
    {
        Melee,
        Ranged,
        Special
    }

    internal enum BasicAttackWorkshopMeleePattern
    {
        Single,
        Fan,
        Line,
        Circle
    }

    internal enum BasicAttackWorkshopRangedPattern
    {
        Projectile,
        Instant
    }

    internal enum BasicAttackWorkshopSpecialPattern
    {
        ReturningProjectile,
        Breath,
        Beam,
        TravelingWave
    }

    internal enum BasicAttackWorkshopProjectileImpact
    {
        StopOnFirstTarget,
        Pierce,
        Explosion
    }

    internal enum BasicAttackWorkshopVolley
    {
        Single,
        Spread
    }

    internal enum BasicAttackWorkshopVfxRole // 제작자는 역할을 고르고 기술 계약은 템플릿이 구성
    {
        Custom,
        MotionCueSource,
        MotionCueOrigin,
        AttackTrail,
        SweepPlane,
        AttackPath,
        CastOrLaunch,
        GroundContact,
        FollowSourceBody,
        FollowOriginBody,
        FollowTrajectoryBody,
        DeliveryVisual,
        TargetImpact,
        DamageStageImpact,
        AreaImpact,
        SequenceFinish,
        OutboundImpact,
        DeliveryTurn,
        ReturnImpact,
        DeliveryEnd,
        MotionEnd
    }

    [Serializable]
    internal sealed class BasicAttackWorkshopVfxSlot // 조립소의 편집용 VFX 빈칸
    {
        public string slotId = "vfx_slot";
        public string displayName = "새 VFX 공간";
        public string description = "Monster Maker에서 이 공간에 몬스터 고유 VFX를 배정합니다.";
        public MonsterBasicAttackVfxEvent eventType = MonsterBasicAttackVfxEvent.RecipeExecute;
        public MonsterBasicAttackVfxAnchor anchor = MonsterBasicAttackVfxAnchor.AttackOrigin;
        public MonsterBasicAttackVfxMultiplicity multiplicity =
            MonsterBasicAttackVfxMultiplicity.OncePerExecution;
        public MonsterBasicAttackVfxAssignmentScope assignmentScope =
            MonsterBasicAttackVfxAssignmentScope.MonsterShared;
        public MonsterBasicAttackVfxAttachment attachment = MonsterBasicAttackVfxAttachment.World;
        public MonsterBasicAttackVfxEndPolicy endPolicy = MonsterBasicAttackVfxEndPolicy.Timed;
        public float defaultLifetime = 1f;
        public BasicAttackWorkshopVfxRole editorRole = BasicAttackWorkshopVfxRole.Custom;
        public bool showAdvanced;

        public static BasicAttackWorkshopVfxSlot From(MonsterBasicAttackVfxSlot source)
        {
            var result = new BasicAttackWorkshopVfxSlot
            {
                slotId = source?.SlotId ?? "vfx_slot",
                displayName = source?.DisplayName ?? "새 VFX 공간",
                description = source?.Description ??
                              "Monster Maker에서 이 공간에 몬스터 고유 VFX를 배정합니다.",
                eventType = source?.EventType ?? MonsterBasicAttackVfxEvent.RecipeExecute,
                anchor = source?.Anchor ?? MonsterBasicAttackVfxAnchor.AttackOrigin,
                multiplicity = source?.Multiplicity ?? MonsterBasicAttackVfxMultiplicity.OncePerExecution,
                assignmentScope = source?.AssignmentScope ??
                                  MonsterBasicAttackVfxAssignmentScope.MonsterShared,
                attachment = source?.Attachment ?? MonsterBasicAttackVfxAttachment.World,
                endPolicy = source?.EndPolicy ?? MonsterBasicAttackVfxEndPolicy.Timed,
                defaultLifetime = source?.DefaultLifetime ?? 1f
            };
            result.editorRole = BasicAttackWorkshopVfxRoles.Resolve(result);
            return result;
        }

        public MonsterBasicAttackVfxSlot Compile()
        {
            var result = new MonsterBasicAttackVfxSlot();
            result.EditorConfigure(
                slotId,
                displayName,
                description,
                eventType,
                anchor,
                multiplicity,
                assignmentScope,
                attachment,
                endPolicy,
                defaultLifetime);
            return result;
        }
    }

    internal static class BasicAttackWorkshopVfxRoles
    {
        private readonly struct Definition
        {
            public Definition(
                BasicAttackWorkshopVfxRole role,
                string label,
                string defaultName,
                string guide,
                MonsterBasicAttackVfxEvent eventType,
                MonsterBasicAttackVfxAnchor anchor,
                MonsterBasicAttackVfxMultiplicity multiplicity,
                MonsterBasicAttackVfxAttachment attachment,
                MonsterBasicAttackVfxEndPolicy endPolicy)
            {
                Role = role;
                Label = label;
                DefaultName = defaultName;
                Guide = guide;
                EventType = eventType;
                Anchor = anchor;
                Multiplicity = multiplicity;
                Attachment = attachment;
                EndPolicy = endPolicy;
            }

            public BasicAttackWorkshopVfxRole Role { get; }
            public string Label { get; }
            public string DefaultName { get; }
            public string Guide { get; }
            public MonsterBasicAttackVfxEvent EventType { get; }
            public MonsterBasicAttackVfxAnchor Anchor { get; }
            public MonsterBasicAttackVfxMultiplicity Multiplicity { get; }
            public MonsterBasicAttackVfxAttachment Attachment { get; }
            public MonsterBasicAttackVfxEndPolicy EndPolicy { get; }
        }

        private static readonly Definition[] Definitions =
        {
            Define(BasicAttackWorkshopVfxRole.MotionCueSource, "시작 · 공격자 중심 예고", "공격 시작 예고", "공격 모션이 시작될 때 공격자 중심에서 한 번 재생합니다.", MonsterBasicAttackVfxEvent.MotionStart, MonsterBasicAttackVfxAnchor.SourceRoot, MonsterBasicAttackVfxMultiplicity.OncePerMotion),
            Define(BasicAttackWorkshopVfxRole.MotionCueOrigin, "시작 · 공격 시작점 예고", "공격 시작 예고", "공격 모션이 시작될 때 입·손·무기 등의 공격 시작점에서 한 번 재생합니다.", MonsterBasicAttackVfxEvent.MotionStart, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerMotion),
            Define(BasicAttackWorkshopVfxRole.AttackTrail, "진행 · 휘두름·내려찍기 궤적", "공격 궤적", "실제 공격 실행 순간부터 공격 시작점을 따라가며 모션 종료까지 유지합니다.", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, MonsterBasicAttackVfxAttachment.FollowAnchor, MonsterBasicAttackVfxEndPolicy.MotionEnd),
            Define(BasicAttackWorkshopVfxRole.SweepPlane, "진행 · 휩쓸기 공격 면", "휩쓸기 면", "전방 부채꼴이나 넓은 휩쓸기 범위를 공격자 중심에서 한 번 보여줍니다.", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.SourceRoot, MonsterBasicAttackVfxMultiplicity.OncePerExecution),
            Define(BasicAttackWorkshopVfxRole.AttackPath, "진행 · 찌르기·직선 경로", "공격 경로", "공격 원점에서 목표 방향으로 뻗는 직선 경로를 한 번 보여줍니다.", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.TrajectoryOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution),
            Define(BasicAttackWorkshopVfxRole.CastOrLaunch, "시작 · 발사·즉발 시전", "발사·시전", "Marker의 실제 공격 실행 순간 공격 시작점에서 한 번 재생합니다.", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution),
            Define(BasicAttackWorkshopVfxRole.GroundContact, "명중 · 지면 접촉", "지면 접촉", "내려찍기처럼 공격이 닿는 범위 중심에 한 번 재생합니다.", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AreaCenter, MonsterBasicAttackVfxMultiplicity.OncePerExecution),
            Define(BasicAttackWorkshopVfxRole.FollowSourceBody, "진행 · 공격자 추적 본체", "공격 본체", "돌진처럼 공격자 중심을 따라가며 공격 모션 종료까지 유지합니다.", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.SourceRoot, MonsterBasicAttackVfxMultiplicity.ContinuousUntilEnd, MonsterBasicAttackVfxAttachment.FollowAnchor, MonsterBasicAttackVfxEndPolicy.MotionEnd),
            Define(BasicAttackWorkshopVfxRole.FollowOriginBody, "진행 · 공격 시작점 추적 본체", "공격 본체", "브레스처럼 공격 시작점을 따라가며 공격 모션 종료까지 유지합니다.", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.ContinuousUntilEnd, MonsterBasicAttackVfxAttachment.FollowAnchor, MonsterBasicAttackVfxEndPolicy.MotionEnd),
            Define(BasicAttackWorkshopVfxRole.FollowTrajectoryBody, "진행 · 경로 추적 본체", "공격 본체", "빔처럼 이동 경로 시작점에 붙어 공격 모션 종료까지 유지합니다.", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.TrajectoryOrigin, MonsterBasicAttackVfxMultiplicity.ContinuousUntilEnd, MonsterBasicAttackVfxAttachment.FollowAnchor, MonsterBasicAttackVfxEndPolicy.MotionEnd),
            Define(BasicAttackWorkshopVfxRole.DeliveryVisual, "진행 · 투사체·이동체 본체", "이동체 본체", "배정한 Prefab 자체가 실제 투사체·파동 판정체의 외형이 됩니다.", MonsterBasicAttackVfxEvent.DeliverySpawn, MonsterBasicAttackVfxAnchor.ProjectileRoot, MonsterBasicAttackVfxMultiplicity.PerProjectile, MonsterBasicAttackVfxAttachment.DeliveryVisual, MonsterBasicAttackVfxEndPolicy.DeliveryEnd),
            Define(BasicAttackWorkshopVfxRole.TargetImpact, "명중 · 대상 실제 명중", "실제 명중", "피해가 실제로 적용된 대상의 명중 위치마다 재생합니다.", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit),
            Define(BasicAttackWorkshopVfxRole.DamageStageImpact, "명중 · 피해 단계마다", "타격별 명중", "연타·지속 공격의 각 피해 단계가 적용된 명중 위치에서 재생합니다.", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerDamageStage),
            Define(BasicAttackWorkshopVfxRole.AreaImpact, "명중 · 범위 판정 완료", "범위 효과", "원형 범위의 피해 판정이 해결된 중심에서 한 번 재생합니다.", MonsterBasicAttackVfxEvent.AreaResolved, MonsterBasicAttackVfxAnchor.AreaCenter, MonsterBasicAttackVfxMultiplicity.OncePerExecution),
            Define(BasicAttackWorkshopVfxRole.SequenceFinish, "종료 · 연타·단계 마무리", "마지막 타격", "마지막 피해 단계가 끝난 실제 명중 위치에서 한 번 재생합니다.", MonsterBasicAttackVfxEvent.SequenceEnd, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.OncePerExecution),
            Define(BasicAttackWorkshopVfxRole.OutboundImpact, "명중 · 왕복 전진 구간", "나가는 경로 명중", "왕복 이동체가 나가는 동안 피해를 준 각 대상 위치에서 재생합니다.", MonsterBasicAttackVfxEvent.OutboundTargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit),
            Define(BasicAttackWorkshopVfxRole.DeliveryTurn, "진행 · 왕복 방향 전환", "회전 전환", "왕복 이동체가 복귀로 방향을 바꾸는 지점에서 이동체마다 한 번 재생합니다.", MonsterBasicAttackVfxEvent.DeliveryTurn, MonsterBasicAttackVfxAnchor.ProjectileRoot, MonsterBasicAttackVfxMultiplicity.PerProjectile),
            Define(BasicAttackWorkshopVfxRole.ReturnImpact, "명중 · 왕복 복귀 구간", "돌아오는 경로 명중", "왕복 이동체가 돌아오는 동안 피해를 준 각 대상 위치에서 재생합니다.", MonsterBasicAttackVfxEvent.ReturnTargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit),
            Define(BasicAttackWorkshopVfxRole.DeliveryEnd, "종료 · 투사체·이동체 소멸", "이동체 소멸", "투사체·파동이 충돌·거리·수명으로 끝난 위치에서 이동체마다 재생합니다.", MonsterBasicAttackVfxEvent.DeliveryEnd, MonsterBasicAttackVfxAnchor.ProjectileRoot, MonsterBasicAttackVfxMultiplicity.PerProjectile),
            Define(BasicAttackWorkshopVfxRole.MotionEnd, "종료 · 공격 모션 종료", "공격 종료", "공격 모션이 끝나는 순간 공격 시작점에서 한 번 재생합니다.", MonsterBasicAttackVfxEvent.MotionEnd, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerMotion)
        };

        private static readonly BasicAttackWorkshopVfxRole[] PopupValues =
            new[] { BasicAttackWorkshopVfxRole.Custom }
                .Concat(Definitions.Select(definition => definition.Role))
                .ToArray();

        private static readonly string[] PopupLabels =
            new[] { "직접 설정 (고급)" }
                .Concat(Definitions.Select(definition => definition.Label))
                .ToArray();

        public static BasicAttackWorkshopVfxRole Popup(
            string label,
            BasicAttackWorkshopVfxRole current,
            MonsterBasicAttackProfile profile)
        {
            var values = GetCompatibleValues(profile, current).ToArray();
            var labels = values
                .Select(role => role == BasicAttackWorkshopVfxRole.Custom
                    ? "직접 설정 (고급)"
                    : GetLabel(role))
                .ToArray();
            var currentIndex = Mathf.Max(0, Array.IndexOf(values, current));
            var selectedIndex = EditorGUILayout.Popup(label, currentIndex, labels);
            return values[Mathf.Clamp(selectedIndex, 0, values.Length - 1)];
        }

        internal static IReadOnlyList<BasicAttackWorkshopVfxRole> GetCompatibleValues(
            MonsterBasicAttackProfile profile,
            BasicAttackWorkshopVfxRole current)
        {
            return PopupValues
                .Where(role => role == BasicAttackWorkshopVfxRole.Custom ||
                               role == current ||
                               IsCompatible(profile, role))
                .ToArray();
        }

        private static bool IsCompatible(
            MonsterBasicAttackProfile profile,
            BasicAttackWorkshopVfxRole role)
        {
            if (profile == null || !TryGet(role, out var definition))
            {
                return true;
            }

            var slot = new MonsterBasicAttackVfxSlot();
            slot.EditorConfigure(
                "compatibility_probe",
                definition.DefaultName,
                definition.Guide,
                definition.EventType,
                definition.Anchor,
                definition.Multiplicity,
                MonsterBasicAttackVfxAssignmentScope.MonsterShared,
                definition.Attachment,
                definition.EndPolicy,
                1f);
            return MonsterBasicAttackVfxCompatibility.TryValidateSlot(profile, slot, out _);
        }

        public static BasicAttackWorkshopVfxRole Resolve(BasicAttackWorkshopVfxSlot slot)
        {
            if (slot == null)
            {
                return BasicAttackWorkshopVfxRole.Custom;
            }
            return Resolve(
                slot.eventType,
                slot.anchor,
                slot.multiplicity,
                slot.attachment,
                slot.endPolicy);
        }

        public static BasicAttackWorkshopVfxRole Resolve(MonsterBasicAttackVfxSlot slot)
        {
            if (slot == null)
            {
                return BasicAttackWorkshopVfxRole.Custom;
            }
            return Resolve(
                slot.EventType,
                slot.Anchor,
                slot.Multiplicity,
                slot.Attachment,
                slot.EndPolicy);
        }

        public static string GetLabel(BasicAttackWorkshopVfxRole role)
        {
            return TryGet(role, out var definition) ? definition.Label : "직접 설정";
        }

        public static string GetGuide(BasicAttackWorkshopVfxRole role)
        {
            return TryGet(role, out var definition)
                ? definition.Guide
                : "고급 설정에서 발생 시점·위치·반복·종료 규칙을 직접 조합합니다.";
        }

        public static void Apply(BasicAttackWorkshopVfxSlot slot, BasicAttackWorkshopVfxRole role)
        {
            if (slot == null || !TryGet(role, out var definition))
            {
                return;
            }
            slot.eventType = definition.EventType;
            slot.anchor = definition.Anchor;
            slot.multiplicity = definition.Multiplicity;
            slot.attachment = definition.Attachment;
            slot.endPolicy = definition.EndPolicy;
            if (string.IsNullOrWhiteSpace(slot.displayName) ||
                slot.displayName == "새 VFX 공간" ||
                slot.displayName.StartsWith("VFX 공간", StringComparison.Ordinal))
            {
                slot.displayName = definition.DefaultName;
            }
            if (string.IsNullOrWhiteSpace(slot.description) ||
                slot.description.StartsWith("Monster Maker에서 이 공간에", StringComparison.Ordinal))
            {
                slot.description = definition.Guide;
            }
        }

        private static BasicAttackWorkshopVfxRole Resolve(
            MonsterBasicAttackVfxEvent eventType,
            MonsterBasicAttackVfxAnchor anchor,
            MonsterBasicAttackVfxMultiplicity multiplicity,
            MonsterBasicAttackVfxAttachment attachment,
            MonsterBasicAttackVfxEndPolicy endPolicy)
        {
            for (var index = 0; index < Definitions.Length; index++)
            {
                var definition = Definitions[index];
                if (definition.EventType == eventType &&
                    definition.Anchor == anchor &&
                    definition.Multiplicity == multiplicity &&
                    definition.Attachment == attachment &&
                    definition.EndPolicy == endPolicy)
                {
                    return definition.Role;
                }
            }
            return BasicAttackWorkshopVfxRole.Custom;
        }

        private static bool TryGet(
            BasicAttackWorkshopVfxRole role,
            out Definition definition)
        {
            for (var index = 0; index < Definitions.Length; index++)
            {
                if (Definitions[index].Role != role)
                {
                    continue;
                }
                definition = Definitions[index];
                return true;
            }
            definition = default;
            return false;
        }

        private static Definition Define(
            BasicAttackWorkshopVfxRole role,
            string label,
            string defaultName,
            string guide,
            MonsterBasicAttackVfxEvent eventType,
            MonsterBasicAttackVfxAnchor anchor,
            MonsterBasicAttackVfxMultiplicity multiplicity,
            MonsterBasicAttackVfxAttachment attachment = MonsterBasicAttackVfxAttachment.World,
            MonsterBasicAttackVfxEndPolicy endPolicy = MonsterBasicAttackVfxEndPolicy.Timed)
        {
            return new Definition(
                role,
                label,
                defaultName,
                guide,
                eventType,
                anchor,
                multiplicity,
                attachment,
                endPolicy);
        }
    }

    internal static class MonsterBasicAttackVfxEditorLabels // Runtime enum은 유지하고 제작 화면만 한글화
    {
        public static T Popup<T>(string label, T current) where T : struct, Enum
        {
            var values = (T[])Enum.GetValues(typeof(T));
            var labels = values.Select(value => Get((Enum)(object)value)).ToArray();
            var currentIndex = Mathf.Max(0, Array.IndexOf(values, current));
            var selectedIndex = EditorGUILayout.Popup(label, currentIndex, labels);
            return values[Mathf.Clamp(selectedIndex, 0, values.Length - 1)];
        }

        public static string Get(MonsterBasicAttackVfxAssignmentScope value)
        {
            return value switch
            {
                MonsterBasicAttackVfxAssignmentScope.MonsterShared => "몬스터 공용",
                MonsterBasicAttackVfxAssignmentScope.MotionSpecific => "공격 모션별",
                _ => value.ToString()
            };
        }

        public static string Get(MonsterBasicAttackVfxAttachment value)
        {
            return value switch
            {
                MonsterBasicAttackVfxAttachment.World => "월드 위치 고정",
                MonsterBasicAttackVfxAttachment.FollowAnchor => "기준점 따라가기",
                MonsterBasicAttackVfxAttachment.DeliveryVisual => "이동체 외형으로 사용",
                _ => value.ToString()
            };
        }

        public static string Get(MonsterBasicAttackVfxEvent value)
        {
            return value switch
            {
                MonsterBasicAttackVfxEvent.MotionStart => "공격 모션 시작",
                MonsterBasicAttackVfxEvent.RecipeExecute => "기본공격 실행",
                MonsterBasicAttackVfxEvent.DeliverySpawn => "투사체·이동체 생성",
                MonsterBasicAttackVfxEvent.TargetDamaged => "대상 실제 명중",
                MonsterBasicAttackVfxEvent.OutboundTargetDamaged => "왕복 전진 구간 명중",
                MonsterBasicAttackVfxEvent.ReturnTargetDamaged => "왕복 복귀 구간 명중",
                MonsterBasicAttackVfxEvent.AreaResolved => "범위 판정 완료",
                MonsterBasicAttackVfxEvent.SequenceEnd => "연타·단계 종료",
                MonsterBasicAttackVfxEvent.DeliveryTurn => "왕복 방향 전환",
                MonsterBasicAttackVfxEvent.DeliveryEnd => "투사체·이동체 종료",
                MonsterBasicAttackVfxEvent.MotionEnd => "공격 모션 종료",
                _ => value.ToString()
            };
        }

        public static string Get(MonsterBasicAttackVfxAnchor value)
        {
            return value switch
            {
                MonsterBasicAttackVfxAnchor.SourceRoot => "공격자 중심",
                MonsterBasicAttackVfxAnchor.AttackOrigin => "공격 시작점",
                MonsterBasicAttackVfxAnchor.MarkerSocket => "마커 지정 소켓",
                MonsterBasicAttackVfxAnchor.ProjectileRoot => "투사체·이동체 중심",
                MonsterBasicAttackVfxAnchor.TargetRoot => "피격 대상 중심",
                MonsterBasicAttackVfxAnchor.HitPoint => "실제 명중 위치",
                MonsterBasicAttackVfxAnchor.AreaCenter => "범위 중심",
                MonsterBasicAttackVfxAnchor.TrajectoryOrigin => "이동 경로 시작점",
                _ => value.ToString()
            };
        }

        public static string Get(MonsterBasicAttackVfxMultiplicity value)
        {
            return value switch
            {
                MonsterBasicAttackVfxMultiplicity.OncePerMotion => "공격 모션당 1회",
                MonsterBasicAttackVfxMultiplicity.OncePerExecution => "공격 실행당 1회",
                MonsterBasicAttackVfxMultiplicity.PerProjectile => "투사체·이동체마다",
                MonsterBasicAttackVfxMultiplicity.PerTargetHit => "명중 대상마다",
                MonsterBasicAttackVfxMultiplicity.PerDamageStage => "피해 단계마다",
                MonsterBasicAttackVfxMultiplicity.ContinuousUntilEnd => "종료까지 지속",
                _ => value.ToString()
            };
        }

        public static string Get(MonsterBasicAttackVfxEndPolicy value)
        {
            return value switch
            {
                MonsterBasicAttackVfxEndPolicy.Timed => "설정 시간 후 종료",
                MonsterBasicAttackVfxEndPolicy.DeliveryEnd => "투사체·이동체 종료 시",
                MonsterBasicAttackVfxEndPolicy.MotionEnd => "공격 모션 종료 시",
                MonsterBasicAttackVfxEndPolicy.ParticleDuration => "파티클 재생 완료 후",
                _ => value.ToString()
            };
        }

        private static string Get(Enum value)
        {
            if (value is MonsterBasicAttackVfxAssignmentScope scope)
            {
                return Get(scope);
            }
            if (value is MonsterBasicAttackVfxAttachment attachment)
            {
                return Get(attachment);
            }
            if (value is MonsterBasicAttackVfxEvent eventType)
            {
                return Get(eventType);
            }
            if (value is MonsterBasicAttackVfxAnchor anchor)
            {
                return Get(anchor);
            }
            if (value is MonsterBasicAttackVfxMultiplicity multiplicity)
            {
                return Get(multiplicity);
            }
            if (value is MonsterBasicAttackVfxEndPolicy endPolicy)
            {
                return Get(endPolicy);
            }
            return ObjectNames.NicifyVariableName(value.ToString());
        }
    }

    [Serializable]
    internal sealed class BasicAttackWorkshopRecipe // 디자이너 선택을 런타임 모듈로 컴파일하는 작업 사본
    {
        public string attackId;
        public string displayName;
        public string designMemo;
        public BasicAttackWorkshopFamily family;
        public BasicAttackWorkshopMeleePattern meleePattern;
        public BasicAttackWorkshopRangedPattern rangedPattern;
        public BasicAttackWorkshopSpecialPattern specialPattern;
        public BasicAttackWorkshopProjectileImpact projectileImpact;
        public BasicAttackWorkshopVolley volley;
        public MonsterBasicAttackProjectileTravel projectilePath;
        public MonsterBasicAttackSweepDirection sweepDirection;
        public MonsterBasicAttackCenter circleCenter;
        public bool multiHit;
        public bool dash;
        public float rangeMultiplier;
        public float radius;
        public float angle;
        public float lineWidth;
        public int maxTargets;
        public int hitCount;
        public float repeatHitInterval;
        public float breathDuration;
        public float secondaryDamageRatio;
        public int projectileCount;
        public float projectileSpreadAngle;
        public float projectileSpeed;
        public float projectileLifetime;
        public float projectileCollisionRadius;
        public float dashDistance;
        public float dashDuration;
        public float hitAreaVisibleDuration;
        public GameObject launchVfx;
        public GameObject projectileVfx;
        public GameObject impactVfx;
        public GameObject launchFeelPrefab;
        public GameObject projectileFeelPrefab;
        public GameObject impactFeelPrefab;
        public SfxCue launchSfx;
        public SfxCue projectileSfx;
        public SfxCue impactSfx;
        public float launchFeelLifetime;
        public float projectileFeelLifetime;
        public float impactFeelLifetime;
        public float launchVfxLifetime;
        public float projectileVfxLifetime;
        public float impactVfxLifetime;
        public Vector3 launchFeelPosition;
        public Vector3 projectileFeelPosition;
        public Vector3 impactFeelPosition;
        public Vector3 launchVfxPosition;
        public Vector3 projectileVfxPosition;
        public Vector3 impactVfxPosition;
        public Vector3 launchFeelEuler;
        public Vector3 projectileFeelEuler;
        public Vector3 impactFeelEuler;
        public Vector3 launchVfxEuler;
        public Vector3 projectileVfxEuler;
        public Vector3 impactVfxEuler;
        public float launchFeelScale;
        public float projectileFeelScale;
        public float impactFeelScale;
        public float launchVfxScale;
        public float projectileVfxScale;
        public float impactVfxScale;
        public List<BasicAttackWorkshopVfxSlot> vfxSlots = new List<BasicAttackWorkshopVfxSlot>();

        public void ResetBlank()
        {
            attackId = "BA_M_New";
            displayName = "새 기본공격";
            designMemo = "이 기본공격의 사용 상황, 타격 흐름, 기획 의도를 기록합니다.";
            family = BasicAttackWorkshopFamily.Melee;
            meleePattern = BasicAttackWorkshopMeleePattern.Single;
            rangedPattern = BasicAttackWorkshopRangedPattern.Projectile;
            specialPattern = BasicAttackWorkshopSpecialPattern.ReturningProjectile;
            projectileImpact = BasicAttackWorkshopProjectileImpact.StopOnFirstTarget;
            volley = BasicAttackWorkshopVolley.Single;
            projectilePath = MonsterBasicAttackProjectileTravel.Homing;
            sweepDirection = MonsterBasicAttackSweepDirection.Simultaneous;
            circleCenter = MonsterBasicAttackCenter.PrimaryTarget;
            multiHit = false;
            dash = false;
            rangeMultiplier = 1f;
            radius = 0.35f;
            angle = 70f;
            lineWidth = 0.55f;
            maxTargets = 1;
            hitCount = 3;
            repeatHitInterval = 0.08f;
            breathDuration = 0.8f;
            secondaryDamageRatio = 0.75f;
            projectileCount = 3;
            projectileSpreadAngle = 24f;
            projectileSpeed = 9f;
            projectileLifetime = 3f;
            projectileCollisionRadius = 0.25f;
            dashDistance = 1.2f;
            dashDuration = 0.1f;
            hitAreaVisibleDuration = 0.42f;
            launchVfx = null;
            projectileVfx = null;
            impactVfx = null;
            launchFeelPrefab = null;
            projectileFeelPrefab = null;
            impactFeelPrefab = null;
            launchSfx = null;
            projectileSfx = null;
            impactSfx = null;
            launchFeelLifetime = 1f;
            projectileFeelLifetime = 3f;
            impactFeelLifetime = 1f;
            launchVfxLifetime = 1f;
            projectileVfxLifetime = 3f;
            impactVfxLifetime = 1f;
            launchFeelPosition = Vector3.zero;
            projectileFeelPosition = Vector3.zero;
            impactFeelPosition = Vector3.zero;
            launchVfxPosition = Vector3.zero;
            projectileVfxPosition = Vector3.zero;
            impactVfxPosition = Vector3.zero;
            launchFeelEuler = Vector3.zero;
            projectileFeelEuler = Vector3.zero;
            impactFeelEuler = Vector3.zero;
            launchVfxEuler = Vector3.zero;
            projectileVfxEuler = Vector3.zero;
            impactVfxEuler = Vector3.zero;
            launchFeelScale = 1f;
            projectileFeelScale = 1f;
            impactFeelScale = 1f;
            launchVfxScale = 1f;
            projectileVfxScale = 1f;
            impactVfxScale = 1f;
            vfxSlots = new List<BasicAttackWorkshopVfxSlot>();
        }

        public void Load(MonsterBasicAttackProfile profile)
        {
            ResetBlank();
            if (profile == null)
            {
                return;
            }

            attackId = profile.AttackId;
            displayName = profile.DisplayName;
            designMemo = profile.DesignMemo;
            sweepDirection = profile.SweepDirection;
            circleCenter = profile.Center;
            rangeMultiplier = profile.RangeMultiplier;
            radius = profile.Radius;
            angle = profile.Angle;
            lineWidth = profile.LineWidth;
            maxTargets = profile.MaxTargets;
            multiHit = profile.SequenceModule == MonsterBasicAttackSequenceModule.Burst;
            hitCount = profile.HitCount;
            repeatHitInterval = profile.RepeatHitInterval;
            breathDuration = profile.BreathDuration;
            secondaryDamageRatio = profile.SecondaryDamageRatio;
            projectileCount = profile.ProjectileCount;
            projectileSpreadAngle = profile.ProjectileSpreadAngle;
            projectileSpeed = profile.ProjectileSpeed;
            projectileLifetime = profile.ProjectileLifetime;
            projectileCollisionRadius = profile.ProjectileCollisionRadius;
            dash = profile.MovementModule == MonsterBasicAttackMovementModule.Dash;
            dashDistance = profile.DashDistance;
            dashDuration = profile.DashDuration;
            hitAreaVisibleDuration = profile.HitAreaVisibleDuration;
            vfxSlots = profile.VfxSlots
                .Select(BasicAttackWorkshopVfxSlot.From)
                .ToList();
            LoadFeel(
                profile.LaunchFeel,
                out launchFeelPrefab,
                out launchFeelLifetime,
                out launchFeelPosition,
                out launchFeelEuler,
                out launchFeelScale);
            LoadFeel(
                profile.ProjectileFeel,
                out projectileFeelPrefab,
                out projectileFeelLifetime,
                out projectileFeelPosition,
                out projectileFeelEuler,
                out projectileFeelScale);
            LoadFeel(
                profile.ImpactFeel,
                out impactFeelPrefab,
                out impactFeelLifetime,
                out impactFeelPosition,
                out impactFeelEuler,
                out impactFeelScale);
            LoadFeedback(
                profile.LaunchFeedback,
                out launchSfx,
                out launchVfx,
                out launchVfxLifetime,
                out launchVfxPosition,
                out launchVfxEuler,
                out launchVfxScale);
            LoadFeedback(
                profile.ProjectileFeedback,
                out projectileSfx,
                out projectileVfx,
                out projectileVfxLifetime,
                out projectileVfxPosition,
                out projectileVfxEuler,
                out projectileVfxScale);
            LoadFeedback(
                profile.ImpactFeedback,
                out impactSfx,
                out impactVfx,
                out impactVfxLifetime,
                out impactVfxPosition,
                out impactVfxEuler,
                out impactVfxScale);

            if (profile.PresentationKind is MonsterBasicAttackPresentationKind.Returning or
                MonsterBasicAttackPresentationKind.Breath or MonsterBasicAttackPresentationKind.Beam or
                MonsterBasicAttackPresentationKind.Wave)
            {
                family = BasicAttackWorkshopFamily.Special;
                specialPattern = profile.PresentationKind switch
                {
                    MonsterBasicAttackPresentationKind.Breath => BasicAttackWorkshopSpecialPattern.Breath,
                    MonsterBasicAttackPresentationKind.Beam => BasicAttackWorkshopSpecialPattern.Beam,
                    MonsterBasicAttackPresentationKind.Wave => BasicAttackWorkshopSpecialPattern.TravelingWave,
                    _ => BasicAttackWorkshopSpecialPattern.ReturningProjectile
                };
                return;
            }

            if (profile.CombatType == MonsterCombatType.Melee)
            {
                family = BasicAttackWorkshopFamily.Melee;
                meleePattern = profile.Shape switch
                {
                    MonsterBasicAttackShape.Fan => BasicAttackWorkshopMeleePattern.Fan,
                    MonsterBasicAttackShape.Line => BasicAttackWorkshopMeleePattern.Line,
                    MonsterBasicAttackShape.Circle => BasicAttackWorkshopMeleePattern.Circle,
                    _ => BasicAttackWorkshopMeleePattern.Single
                };
                return;
            }

            family = BasicAttackWorkshopFamily.Ranged;
            rangedPattern = profile.DeliveryModule == MonsterBasicAttackDeliveryModule.Direct
                ? BasicAttackWorkshopRangedPattern.Instant
                : BasicAttackWorkshopRangedPattern.Projectile;
            projectilePath = profile.ProjectileTravel;
            projectileImpact = profile.CollisionModule switch
            {
                MonsterBasicAttackCollisionModule.Pierce => BasicAttackWorkshopProjectileImpact.Pierce,
                MonsterBasicAttackCollisionModule.AreaImpact => BasicAttackWorkshopProjectileImpact.Explosion,
                _ => BasicAttackWorkshopProjectileImpact.StopOnFirstTarget
            };
            volley = profile.ProjectileCount > 1
                ? BasicAttackWorkshopVolley.Spread
                : BasicAttackWorkshopVolley.Single;
        }

        public void Compile(MonsterBasicAttackProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            Normalize();
            var combatType = family == BasicAttackWorkshopFamily.Melee
                ? MonsterCombatType.Melee
                : MonsterCombatType.Ranged;
            var delivery = MonsterBasicAttackDelivery.Contact;
            var shape = MonsterBasicAttackShape.Single;
            var center = MonsterBasicAttackCenter.PrimaryTarget;
            var travel = MonsterBasicAttackProjectileTravel.None;
            var count = 1;
            var spread = 0f;
            var stopOnFirst = false;
            var ratios = BuildEqualRatios(1);

            switch (family)
            {
                case BasicAttackWorkshopFamily.Melee:
                    shape = meleePattern switch
                    {
                        BasicAttackWorkshopMeleePattern.Fan => MonsterBasicAttackShape.Fan,
                        BasicAttackWorkshopMeleePattern.Line => MonsterBasicAttackShape.Line,
                        BasicAttackWorkshopMeleePattern.Circle => MonsterBasicAttackShape.Circle,
                        _ => MonsterBasicAttackShape.Single
                    };
                    center = meleePattern == BasicAttackWorkshopMeleePattern.Circle
                        ? circleCenter
                        : MonsterBasicAttackCenter.PrimaryTarget;
                    delivery = dash
                        ? MonsterBasicAttackDelivery.Dash
                        : multiHit
                            ? MonsterBasicAttackDelivery.MultiHit
                            : MonsterBasicAttackDelivery.Contact;
                    ratios = BuildEqualRatios(dash || !multiHit ? 1 : hitCount);
                    break;

                case BasicAttackWorkshopFamily.Ranged:
                    if (rangedPattern == BasicAttackWorkshopRangedPattern.Instant)
                    {
                        delivery = multiHit
                            ? MonsterBasicAttackDelivery.MultiHit
                            : MonsterBasicAttackDelivery.Instant;
                        ratios = BuildEqualRatios(multiHit ? hitCount : 1);
                        break;
                    }

                    delivery = MonsterBasicAttackDelivery.Projectile;
                    travel = projectilePath;
                    shape = projectileImpact switch
                    {
                        BasicAttackWorkshopProjectileImpact.Pierce => MonsterBasicAttackShape.Line,
                        BasicAttackWorkshopProjectileImpact.Explosion => MonsterBasicAttackShape.Circle,
                        _ => MonsterBasicAttackShape.Single
                    };
                    stopOnFirst = projectileImpact == BasicAttackWorkshopProjectileImpact.StopOnFirstTarget;
                    if (volley == BasicAttackWorkshopVolley.Spread)
                    {
                        travel = MonsterBasicAttackProjectileTravel.Straight;
                        shape = MonsterBasicAttackShape.Fan;
                        stopOnFirst = true;
                        count = projectileCount;
                        spread = projectileSpreadAngle;
                    }
                    break;

                case BasicAttackWorkshopFamily.Special:
                    switch (specialPattern)
                    {
                        case BasicAttackWorkshopSpecialPattern.ReturningProjectile:
                            delivery = MonsterBasicAttackDelivery.ReturningProjectile;
                            shape = MonsterBasicAttackShape.Line;
                            travel = MonsterBasicAttackProjectileTravel.Returning;
                            ratios = new[] { 0.6f, 0.4f };
                            break;
                        case BasicAttackWorkshopSpecialPattern.Breath:
                            delivery = MonsterBasicAttackDelivery.Breath;
                            shape = MonsterBasicAttackShape.Fan;
                            ratios = BuildEqualRatios(hitCount);
                            break;
                        case BasicAttackWorkshopSpecialPattern.Beam:
                            delivery = MonsterBasicAttackDelivery.Beam;
                            shape = MonsterBasicAttackShape.Line;
                            break;
                        case BasicAttackWorkshopSpecialPattern.TravelingWave:
                            delivery = MonsterBasicAttackDelivery.TravelingWave;
                            shape = MonsterBasicAttackShape.Line;
                            travel = MonsterBasicAttackProjectileTravel.Straight;
                            break;
                    }
                    break;
            }

            profile.EditorConfigure(
                attackId,
                displayName,
                combatType,
                delivery,
                shape,
                center,
                travel,
                rangeMultiplier,
                radius,
                angle,
                lineWidth,
                maxTargets,
                count,
                spread,
                ratios,
                secondaryDamageRatio,
                repeatHitInterval,
                dash ? dashDistance : 0f,
                dashDuration,
                stopOnFirst,
                hitAreaVisibleDuration,
                projectileSpeed,
                projectileLifetime,
                projectileCollisionRadius);
            profile.EditorSetSweepDirection(sweepDirection);
            profile.EditorSetBreathDuration(breathDuration);
            profile.EditorSetDesignMemo(designMemo);
            profile.EditorSetVfxSlots(vfxSlots.Select(slot => slot.Compile()));
            profile.EditorSetPresentationFeedback(
                BuildFeedback(
                    launchSfx,
                    launchVfx,
                    launchVfxLifetime,
                    launchVfxPosition,
                    launchVfxEuler,
                    launchVfxScale),
                BuildFeedback(
                    projectileSfx,
                    projectileVfx,
                    projectileVfxLifetime,
                    projectileVfxPosition,
                    projectileVfxEuler,
                    projectileVfxScale),
                BuildFeedback(
                    impactSfx,
                    impactVfx,
                    impactVfxLifetime,
                    impactVfxPosition,
                    impactVfxEuler,
                    impactVfxScale)); // 기존 Recipe 데이터만 왕복 보존, UI에서는 새 입력을 노출하지 않음
            profile.EditorSetFeelFeedback(
                BuildFeel(
                    launchFeelPrefab,
                    launchFeelLifetime,
                    launchFeelPosition,
                    launchFeelEuler,
                    launchFeelScale),
                BuildFeel(
                    projectileFeelPrefab,
                    projectileFeelLifetime,
                    projectileFeelPosition,
                    projectileFeelEuler,
                    projectileFeelScale),
                BuildFeel(
                    impactFeelPrefab,
                    impactFeelLifetime,
                    impactFeelPosition,
                    impactFeelEuler,
                    impactFeelScale));
        }

        private static BasicAttackFeelCue BuildFeel(
            GameObject prefab,
            float lifetime,
            Vector3 position,
            Vector3 euler,
            float scale)
        {
            var feel = new BasicAttackFeelCue();
            feel.EditorConfigure(prefab, lifetime, position, euler, scale);
            return feel;
        }

        private static MonsterFeedbackCue BuildFeedback(
            SfxCue sfx,
            GameObject vfx,
            float lifetime,
            Vector3 position,
            Vector3 euler,
            float scale)
        {
            var feedback = new MonsterFeedbackCue();
            feedback.EditorConfigure(sfx, vfx, lifetime, position, euler, scale);
            return feedback;
        }

        private static void LoadFeedback(
            MonsterFeedbackCue feedback,
            out SfxCue sfx,
            out GameObject vfx,
            out float lifetime,
            out Vector3 position,
            out Vector3 euler,
            out float scale)
        {
            sfx = feedback?.Sfx;
            vfx = feedback?.VfxPrefab;
            lifetime = feedback?.VfxLifetime ?? 1f;
            position = feedback?.LocalPosition ?? Vector3.zero;
            euler = feedback?.LocalRotation.eulerAngles ?? Vector3.zero;
            scale = feedback?.Scale ?? 1f;
        }

        private static void LoadFeel(
            BasicAttackFeelCue feel,
            out GameObject prefab,
            out float lifetime,
            out Vector3 position,
            out Vector3 euler,
            out float scale)
        {
            prefab = feel?.Prefab;
            lifetime = feel?.Lifetime ?? 1f;
            position = feel?.LocalPosition ?? Vector3.zero;
            euler = feel?.LocalRotation.eulerAngles ?? Vector3.zero;
            scale = feel?.Scale ?? 1f;
        }

        public string RequiredIdPrefix => family switch
        {
            BasicAttackWorkshopFamily.Ranged => "BA_R_",
            BasicAttackWorkshopFamily.Special => "BA_S_",
            _ => "BA_M_"
        };

        public string BuildVfxContractSignature()
        {
            return string.Join(
                "|",
                family,
                meleePattern,
                rangedPattern,
                specialPattern,
                projectileImpact,
                volley,
                projectilePath,
                circleCenter,
                multiHit,
                dash,
                hitCount);
        }

        public void Normalize()
        {
            rangeMultiplier = Mathf.Clamp(rangeMultiplier, 0.2f, 4f);
            radius = Mathf.Clamp(radius, 0.05f, 5f);
            angle = Mathf.Clamp(angle, 5f, 180f);
            lineWidth = Mathf.Clamp(lineWidth, 0.05f, 5f);
            maxTargets = Mathf.Clamp(maxTargets, 1, MonsterBasicAttackProfile.MaximumTargets);
            hitCount = Mathf.Clamp(hitCount, 2, MonsterBasicAttackProfile.MaximumHitCount);
            repeatHitInterval = Mathf.Clamp(repeatHitInterval, 0.01f, 0.3f);
            breathDuration = Mathf.Max(0.01f, breathDuration);
            secondaryDamageRatio = Mathf.Clamp(secondaryDamageRatio, 0.1f, 1f);
            projectileCount = Mathf.Clamp(projectileCount, 2, MonsterBasicAttackProfile.MaximumProjectileCount);
            projectileSpreadAngle = Mathf.Clamp(projectileSpreadAngle, 1f, 90f);
            projectileSpeed = Mathf.Max(0.01f, projectileSpeed);
            projectileLifetime = Mathf.Max(0.01f, projectileLifetime);
            projectileCollisionRadius = Mathf.Max(0.01f, projectileCollisionRadius);
            dashDistance = Mathf.Clamp(dashDistance, 0.1f, 5f);
            dashDuration = Mathf.Clamp(dashDuration, 0.05f, 0.3f);
            hitAreaVisibleDuration = Mathf.Clamp(hitAreaVisibleDuration, 0.1f, 1f);
            launchFeelLifetime = Mathf.Clamp(launchFeelLifetime, 0.05f, 10f);
            projectileFeelLifetime = Mathf.Clamp(projectileFeelLifetime, 0.05f, 10f);
            impactFeelLifetime = Mathf.Clamp(impactFeelLifetime, 0.05f, 10f);
            launchVfxLifetime = Mathf.Clamp(launchVfxLifetime, 0.05f, 10f);
            projectileVfxLifetime = Mathf.Clamp(projectileVfxLifetime, 0.05f, 10f);
            impactVfxLifetime = Mathf.Clamp(impactVfxLifetime, 0.05f, 10f);
            launchFeelScale = Mathf.Clamp(launchFeelScale, 0.01f, 20f);
            projectileFeelScale = Mathf.Clamp(projectileFeelScale, 0.01f, 20f);
            impactFeelScale = Mathf.Clamp(impactFeelScale, 0.01f, 20f);
            launchVfxScale = Mathf.Clamp(launchVfxScale, 0.01f, 20f);
            projectileVfxScale = Mathf.Clamp(projectileVfxScale, 0.01f, 20f);
            impactVfxScale = Mathf.Clamp(impactVfxScale, 0.01f, 20f);

            if (family == BasicAttackWorkshopFamily.Melee && dash)
            {
                multiHit = false;
            }
            if (family == BasicAttackWorkshopFamily.Ranged && rangedPattern == BasicAttackWorkshopRangedPattern.Projectile)
            {
                multiHit = false;
                if (projectileImpact == BasicAttackWorkshopProjectileImpact.Pierce)
                {
                    projectilePath = MonsterBasicAttackProjectileTravel.Straight;
                    volley = BasicAttackWorkshopVolley.Single;
                }
                else if (projectileImpact == BasicAttackWorkshopProjectileImpact.Explosion)
                {
                    volley = BasicAttackWorkshopVolley.Single;
                }
            }
        }

        private static float[] BuildEqualRatios(int count)
        {
            count = Mathf.Clamp(count, 1, MonsterBasicAttackProfile.MaximumHitCount);
            var ratios = new float[count];
            var ratio = 1f / count;
            for (var index = 0; index < count; index++)
            {
                ratios[index] = ratio;
            }
            return ratios;
        }
    }
}
