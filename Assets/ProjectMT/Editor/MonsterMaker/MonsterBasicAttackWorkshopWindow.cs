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

    internal enum BasicAttackPreviewPositionTarget
    {
        None,
        Launch,
        Projectile,
        Impact
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
            BasicAttackWorkshopVfxRole current)
        {
            var currentIndex = Mathf.Max(0, Array.IndexOf(PopupValues, current));
            var selectedIndex = EditorGUILayout.Popup(label, currentIndex, PopupLabels);
            return PopupValues[Mathf.Clamp(selectedIndex, 0, PopupValues.Length - 1)];
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

    public sealed class MonsterBasicAttackWorkshopWindow : EditorWindow // 버튼을 눌렀을 때만 여는 독립 조립소
    {
        private const float LibraryWidth = 285f;
        private const float AssemblerWidth = 480f;
        private const float AssemblerContentWidth = AssemblerWidth - 33f; // Toolbar의 3px 내부 여백까지 포함해 액티브와 실제 폭을 맞춤
        private const float MinimumPreviewWidth = 300f;
        private const string StandardFeelTargetPath =
            "Assets/ProjectMT/03_Features/Expedition/Prefabs/PF_Enemy_Knight_T1.prefab";
        private readonly List<MonsterBasicAttackProfile> profiles = new List<MonsterBasicAttackProfile>();
        private BasicAttackWorkshopRecipe recipe;
        private MonsterBasicAttackProfile workingProfile;
        private MonsterBasicAttackProfile loadedProfile;
        private MonsterMakerDraft originDraft;
        private Vector2 libraryScroll;
        private Vector2 recipeScroll;
        private string search = string.Empty;
        private string message;
        private MessageType messageType;
        private bool workCopyDirty;
        private MonsterImpactStrength previewImpactStrength = MonsterImpactStrength.Standard;
        private BasicAttackPreviewPositionTarget previewPositionTarget;
        private bool previewPositionDragging;
        private bool previewPositionDragTopDown;
        private Vector2 previewPositionDragMouseStart;
        private Vector3 previewPositionDragValueStart;
        private Vector3 previewPositionDragWorldOffset;
        private int previewPositionHotControl;
        private PreviewRenderUtility previewUtility;
        private GameObject previewRoot;
        private Material previewGroundMaterial;
        private Material previewSourceMaterial;
        private Material previewTargetMaterial;
        private Material previewAttackMaterial;
        private GameObject previewAttacker;
        private GameObject previewTarget;
        private GameObject previewAttackMover;
        private readonly List<GameObject> previewAttackMovers = new List<GameObject>();
        private GameObject previewImpactPulse;
        private GameObject previewLaunchEffect;
        private GameObject previewImpactEffect;
        private GameObject previewLaunchFeel;
        private GameObject previewImpactFeel;
        private Vector3 previewAttackerStart;
        private Vector3 previewTargetStart;
        private Vector3 previewTargetBaseScale = Vector3.one * 0.45f;
        private double previewPlaybackStart;
        private bool previewPlaying;
        private int previewMotionIndex;
        private int previewNextImpactIndex;
        private float previewLastImpactElapsed = -1f;
        private bool previewLastImpactHasFeedback;
        private float previewStandaloneImpactTime = 0.55f;
        private float previewActivationElapsed = -1f;
        private bool previewDeliveryActivated;
        private bool previewUpdateSubscribed;
        private Rect lastAssemblerContentRect; // 최소 창 폭 QA용 실제 중앙 콘텐츠 경계
        private Rect lastAssemblerViewportRect; // 세로 스크롤이 실제로 보여 주는 중앙 폭
        private Rect lastVfxHeaderRightmostRect; // VFX 공간 헤더의 삭제 버튼 경계
        private Rect lastAssemblerPanelRect; // 저장 영역까지 포함한 중앙 패널 경계
        private Rect lastSaveRightmostRect; // 두 저장 버튼 중 가장 오른쪽 버튼 경계
        private Rect lastPreviewColumnRect; // 우측 미리보기 열 경계
        private Rect lastPreviewToolbarRightmostRect; // VFX 위치 버튼 행의 가장 오른쪽 버튼 경계

        public static event Action PresetAssigned;

        [MenuItem("JC Tool/Monster/기본공격 조립소")]
        public static void OpenStandalone()
        {
            Open(null);
        }

        public static void Open(MonsterMakerDraft draft)
        {
            foreach (var staleWindow in Resources.FindObjectsOfTypeAll<MonsterBasicAttackWorkshopWindow>())
            {
                staleWindow.Close();
            }

            var window = CreateInstance<MonsterBasicAttackWorkshopWindow>();
            window.titleContent = new GUIContent("기본공격 조립소");
            window.minSize = new Vector2(1100f, 700f);
            var mainWindow = EditorGUIUtility.GetMainWindowPosition();
            var width = Mathf.Clamp(mainWindow.width - 120f, 1100f, 1380f);
            var height = Mathf.Clamp(mainWindow.height - 120f, 700f, 900f);
            window.position = new Rect(
                mainWindow.x + (mainWindow.width - width) * 0.5f,
                mainWindow.y + (mainWindow.height - height) * 0.5f,
                width,
                height);
            window.originDraft = draft;
            window.StartBlank();
            window.ShowUtility();
            window.Focus();
        }

        internal int PreviewSceneHandle => previewUtility?.camera != null
            ? previewUtility.camera.gameObject.scene.handle
            : 0;

        private void OnEnable()
        {
            titleContent = new GUIContent("기본공격 조립소");
            minSize = new Vector2(1100f, 700f);
            RefreshProfiles();
            if (recipe == null)
            {
                StartBlank();
            }
            EditorApplication.projectChanged += RefreshProfiles;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= RefreshProfiles;
            SetPreviewUpdateSubscribed(false);
            DisposePreview();
            if (workingProfile != null)
            {
                DestroyImmediate(workingProfile);
            }
        }

        private void OnDestroy()
        {
            SetPreviewUpdateSubscribed(false);
            DisposePreview();
            if (workingProfile != null)
            {
                DestroyImmediate(workingProfile);
                workingProfile = null;
            }
        }

        private void OnGUI()
        {
            EnsureWorkingProfile();
            MonsterWorkshopVisualTheme.DrawHeader(
                "기본공격 조립소",
                "공격 방식 조립 · 몬스터별 연출 계약 · 독립 판정 미리보기");
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLibrary();
                DrawAssembler();
                DrawPreview();
            }
        }

        private void DrawLibrary()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(LibraryWidth)))
            {
                GUILayout.Label("저장된 프리셋", EditorStyles.boldLabel);
                if (MonsterWorkshopVisualTheme.DrawTintedButton(
                        new GUIContent("+ 빈 기본공격 조립"),
                        MonsterWorkshopVisualTheme.PrimaryColor,
                        28f))
                {
                    StartBlank();
                }

                if (originDraft != null)
                {
                    var assigned = originDraft.BasicAttackProfile;
                    GUILayout.Label(
                        assigned == null
                            ? $"현재 {originDraft.MonsterId} · 미배정"
                            : $"현재 {originDraft.MonsterId} · [{assigned.AttackId}]",
                        EditorStyles.miniLabel);
                    using (new EditorGUI.DisabledScope(assigned == null))
                    {
                        var label = assigned == null
                            ? "현재 배정 프리셋 없음"
                            : "현재 배정 프리셋 불러오기";
                        if (GUILayout.Button(label, GUILayout.Height(24f)))
                        {
                            LoadProfile(assigned);
                        }
                    }
                }

                search = EditorGUILayout.TextField("검색", search);
                libraryScroll = MonsterWorkshopVisualTheme.BeginVerticalScrollView(libraryScroll);
                DrawProfileGroup("공식 기본공격 15종", true);
                DrawProfileGroup("사용자 프리셋", false);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawProfileGroup(string title, bool builtIn)
        {
            var drewHeader = false;
            foreach (var profile in profiles)
            {
                if (profile == null || MonsterBasicAttackPresetUtility.IsBuiltInProfile(profile) != builtIn ||
                    !MatchesSearch(profile))
                {
                    continue;
                }

                if (!drewHeader)
                {
                    GUILayout.Space(4f);
                    GUILayout.Label(title, EditorStyles.miniBoldLabel);
                    drewHeader = true;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    var usage = MonsterBasicAttackPresetUtility.CountDraftUsages(profile);
                    var content = new GUIContent(
                        $"[{profile.AttackId}] {profile.DisplayName}",
                        $"현재 {usage}마리가 사용하는 프리셋");
                    if (MonsterWorkshopVisualTheme.DrawPresetButton(
                            content,
                            profile == loadedProfile))
                    {
                        LoadProfile(profile);
                    }
                    GUILayout.Label(
                        new GUIContent(usage.ToString(), "사용 중인 몬스터 수"),
                        EditorStyles.centeredGreyMiniLabel,
                        GUILayout.Width(24f));
                }
            }
        }

        private void DrawAssembler()
        {
            using (var assemblerScope = new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox,
                       GUILayout.Width(AssemblerWidth)))
            {
                GUILayout.Label("기본공격 조립", EditorStyles.boldLabel);
                GUILayout.Label(
                    loadedProfile == null ? "빈 작업 사본 · 아직 프리셋 자산이 아닙니다." :
                    $"직접 수정 중: {AssetDatabase.GetAssetPath(loadedProfile)}",
                    EditorStyles.wordWrappedMiniLabel);
                if (loadedProfile != null)
                {
                    GUILayout.Label(
                        "프리셋 ID만 잠깁니다. 나머지 설정은 바로 편집하고 아래의 저장 버튼으로 반영하세요.",
                        EditorStyles.wordWrappedMiniLabel);
                    if (GUILayout.Button("다른 프리셋으로 복제", EditorStyles.miniButton, GUILayout.Height(22f)))
                    {
                        ForkLoadedAsNew();
                    }
                }

                recipeScroll = MonsterWorkshopVisualTheme.BeginVerticalScrollView(recipeScroll);
                using (var contentScope = new EditorGUILayout.VerticalScope(GUILayout.Width(AssemblerContentWidth)))
                {
                    EditorGUI.BeginChangeCheck();
                    using (new EditorGUI.DisabledScope(loadedProfile != null))
                    {
                        recipe.attackId = EditorGUILayout.TextField("프리셋 ID", recipe.attackId);
                    }
                    if (loadedProfile != null)
                    {
                        GUILayout.Label(
                            "저장된 프리셋의 ID와 공격 계열은 자산 경로·참조 보호를 위해 고정됩니다.",
                            EditorStyles.wordWrappedMiniLabel);
                    }
                    recipe.displayName = EditorGUILayout.TextField("표시 이름", recipe.displayName);
                    EditorGUILayout.LabelField("기획 메모");
                    recipe.designMemo = MonsterWorkshopVisualTheme.DrawWrappedTextArea(
                        recipe.designMemo,
                        54f,
                        AssemblerContentWidth - 8f);

                    GUILayout.Space(6f);
                    GUILayout.Label("1. 공격 계열", EditorStyles.boldLabel);
                    var previousFamily = recipe.family;
                    using (new EditorGUI.DisabledScope(loadedProfile != null))
                    {
                        recipe.family = (BasicAttackWorkshopFamily)GUILayout.Toolbar(
                            (int)recipe.family,
                            new[] { "근거리", "원거리", "특수" },
                            GUILayout.Height(28f));
                    }
                    if (recipe.family != previousFamily)
                    {
                        recipe.attackId = FindNextPresetId(recipe.family);
                    }

                    GUILayout.Space(6f);
                    GUILayout.Label("2. 공격 방식", EditorStyles.boldLabel);
                    switch (recipe.family)
                    {
                        case BasicAttackWorkshopFamily.Melee:
                            DrawMeleeOptions();
                            break;
                        case BasicAttackWorkshopFamily.Ranged:
                            DrawRangedOptions();
                            break;
                        case BasicAttackWorkshopFamily.Special:
                            DrawSpecialOptions();
                            break;
                    }

                    GUILayout.Space(6f);
                    GUILayout.Label("3. 공용 판정 수치", EditorStyles.boldLabel);
                    recipe.rangeMultiplier = EditorGUILayout.Slider("사거리 배율", recipe.rangeMultiplier, 0.2f, 4f);
                    recipe.maxTargets = EditorGUILayout.IntSlider(
                        "최대 대상 수", recipe.maxTargets, 1, MonsterBasicAttackProfile.MaximumTargets);
                    if (recipe.maxTargets > 1)
                    {
                        recipe.secondaryDamageRatio = EditorGUILayout.Slider(
                            "부대상 피해 배율", recipe.secondaryDamageRatio, 0.1f, 1f);
                    }
                    recipe.hitAreaVisibleDuration = EditorGUILayout.Slider(
                        "판정 표시 시간", recipe.hitAreaVisibleDuration, 0.1f, 1f);

                    GUILayout.Space(6f);
                    DrawPresentationOptions();

                    if (EditorGUI.EndChangeCheck())
                    {
                        recipe.Normalize();
                        CompileWorkingProfile();
                        workCopyDirty = true;
                        message = null;
                    }
                    if (Event.current.type == EventType.Repaint)
                    {
                        lastAssemblerContentRect = contentScope.rect;
                    }
                }
                EditorGUILayout.EndScrollView();
                if (Event.current.type == EventType.Repaint)
                {
                    lastAssemblerViewportRect = GUILayoutUtility.GetLastRect();
                }

                DrawSaveAndAssignControls();
                if (Event.current.type == EventType.Repaint)
                {
                    lastAssemblerPanelRect = assemblerScope.rect;
                }
            }
        }

        private void DrawMeleeOptions()
        {
            recipe.meleePattern = (BasicAttackWorkshopMeleePattern)GUILayout.Toolbar(
                (int)recipe.meleePattern,
                new[] { "단일", "부채꼴", "직선", "원형" });

            if (recipe.meleePattern == BasicAttackWorkshopMeleePattern.Fan)
            {
                recipe.angle = EditorGUILayout.Slider("부채꼴 각도", recipe.angle, 5f, 180f);
                EditorGUILayout.LabelField("휘두름 방향");
                recipe.sweepDirection = (MonsterBasicAttackSweepDirection)GUILayout.Toolbar(
                    (int)recipe.sweepDirection,
                    new[] { "동시", "좌→우", "우→좌" });
            }
            else if (recipe.meleePattern == BasicAttackWorkshopMeleePattern.Line)
            {
                recipe.lineWidth = EditorGUILayout.Slider("직선 폭", recipe.lineWidth, 0.05f, 5f);
            }
            else
            {
                recipe.radius = EditorGUILayout.Slider("판정 반경", recipe.radius, 0.05f, 5f);
            }

            if (recipe.meleePattern == BasicAttackWorkshopMeleePattern.Circle)
            {
                EditorGUILayout.LabelField("원형 중심");
                recipe.circleCenter = (MonsterBasicAttackCenter)GUILayout.Toolbar(
                    (int)recipe.circleCenter,
                    new[] { "시전자 중심", "주 대상 중심" });
            }

            recipe.dash = EditorGUILayout.Toggle("공격 전 실제 XZ 돌진", recipe.dash);
            if (recipe.dash)
            {
                recipe.dashDistance = EditorGUILayout.Slider("돌진 거리", recipe.dashDistance, 0.1f, 5f);
                recipe.dashDuration = EditorGUILayout.Slider("돌진 시간", recipe.dashDuration, 0.05f, 0.3f);
                EditorGUILayout.HelpBox("돌진과 연타는 동시에 조립하지 않습니다.", MessageType.None);
            }
            else
            {
                DrawHitSequenceOptions();
            }
        }

        private void DrawRangedOptions()
        {
            recipe.rangedPattern = (BasicAttackWorkshopRangedPattern)GUILayout.Toolbar(
                (int)recipe.rangedPattern,
                new[] { "투사체", "즉발 원거리" });
            if (recipe.rangedPattern == BasicAttackWorkshopRangedPattern.Instant)
            {
                recipe.radius = EditorGUILayout.Slider("목표 판정 반경", recipe.radius, 0.05f, 5f);
                DrawHitSequenceOptions();
                return;
            }

            EditorGUILayout.LabelField("이동 궤적");
            var pathIndex = recipe.projectilePath == MonsterBasicAttackProjectileTravel.Straight ? 1 : 0;
            pathIndex = GUILayout.Toolbar(pathIndex, new[] { "유도", "직선" });
            recipe.projectilePath = pathIndex == 1
                ? MonsterBasicAttackProjectileTravel.Straight
                : MonsterBasicAttackProjectileTravel.Homing;
            EditorGUILayout.LabelField("충돌 처리");
            recipe.projectileImpact = (BasicAttackWorkshopProjectileImpact)GUILayout.Toolbar(
                (int)recipe.projectileImpact,
                new[] { "첫 대상 정지", "관통", "폭발" });
            EditorGUILayout.LabelField("발사 구성");
            recipe.volley = (BasicAttackWorkshopVolley)GUILayout.Toolbar(
                (int)recipe.volley,
                new[] { "1발", "부채꼴 확산" });

            if (recipe.projectileImpact == BasicAttackWorkshopProjectileImpact.Pierce)
            {
                recipe.lineWidth = EditorGUILayout.Slider("관통 폭", recipe.lineWidth, 0.05f, 5f);
                EditorGUILayout.HelpBox("관통은 직선 궤적 1발로 자동 정규화됩니다.", MessageType.None);
            }
            else if (recipe.projectileImpact == BasicAttackWorkshopProjectileImpact.Explosion)
            {
                recipe.radius = EditorGUILayout.Slider("폭발 반경", recipe.radius, 0.05f, 5f);
                EditorGUILayout.HelpBox("폭발은 1발로 자동 정규화됩니다.", MessageType.None);
            }

            if (recipe.volley == BasicAttackWorkshopVolley.Spread)
            {
                recipe.projectileCount = EditorGUILayout.IntSlider(
                    "발사 수", recipe.projectileCount, 2, MonsterBasicAttackProfile.MaximumProjectileCount);
                recipe.projectileSpreadAngle = EditorGUILayout.Slider(
                    "확산 각도", recipe.projectileSpreadAngle, 1f, 90f);
            }
            DrawProjectileValues();
        }

        private void DrawSpecialOptions()
        {
            recipe.specialPattern = (BasicAttackWorkshopSpecialPattern)GUILayout.Toolbar(
                (int)recipe.specialPattern,
                new[] { "왕복", "브레스", "빔", "진행 파동" });
            switch (recipe.specialPattern)
            {
                case BasicAttackWorkshopSpecialPattern.ReturningProjectile:
                    recipe.lineWidth = EditorGUILayout.Slider("왕복 경로 폭", recipe.lineWidth, 0.05f, 5f);
                    DrawProjectileValues();
                    EditorGUILayout.HelpBox("전진 60% + 복귀 40%의 2단 피해로 컴파일됩니다.", MessageType.None);
                    break;
                case BasicAttackWorkshopSpecialPattern.Breath:
                    recipe.angle = EditorGUILayout.Slider("브레스 각도", recipe.angle, 5f, 180f);
                    recipe.hitCount = EditorGUILayout.IntSlider(
                        "지속 타격 수", recipe.hitCount, 2, MonsterBasicAttackProfile.MaximumHitCount);
                    recipe.breathDuration = Mathf.Max(
                        0.01f,
                        EditorGUILayout.FloatField("기본 유지 시간(초)", recipe.breathDuration));
                    EditorGUILayout.HelpBox(
                        $"첫 피해부터 {recipe.hitCount}단계 피해를 약 " +
                        $"{recipe.breathDuration / Mathf.Max(1, recipe.hitCount):0.###}초 간격으로 분배하고, " +
                        "브레스 본체는 유지 시간이 끝날 때까지 계속됩니다.",
                        MessageType.None);
                    break;
                case BasicAttackWorkshopSpecialPattern.Beam:
                    recipe.lineWidth = EditorGUILayout.Slider("빔 폭", recipe.lineWidth, 0.05f, 5f);
                    break;
                case BasicAttackWorkshopSpecialPattern.TravelingWave:
                    recipe.lineWidth = EditorGUILayout.Slider("파동 폭", recipe.lineWidth, 0.05f, 5f);
                    DrawProjectileValues();
                    break;
            }
        }

        private void DrawHitSequenceOptions()
        {
            recipe.multiHit = EditorGUILayout.Toggle("연타", recipe.multiHit);
            if (!recipe.multiHit)
            {
                return;
            }
            recipe.hitCount = EditorGUILayout.IntSlider(
                "타격 수", recipe.hitCount, 2, MonsterBasicAttackProfile.MaximumHitCount);
            recipe.repeatHitInterval = EditorGUILayout.Slider(
                "타격 간격", recipe.repeatHitInterval, 0.01f, 0.3f);
            EditorGUILayout.HelpBox("총 피해 100%를 타격 수로 균등 분배합니다.", MessageType.None);
        }

        private void DrawProjectileValues()
        {
            recipe.projectileSpeed = EditorGUILayout.FloatField("이동 속도", recipe.projectileSpeed);
            recipe.projectileLifetime = EditorGUILayout.FloatField("수명", recipe.projectileLifetime);
            recipe.projectileCollisionRadius = EditorGUILayout.Slider(
                "이동 충돌 반경", recipe.projectileCollisionRadius, 0.01f, 2f);
        }

        private void DrawPresentationOptions()
        {
            DrawVfxContractOptions();
            GUILayout.Space(8f);
            GUILayout.Label("5. 기본공격 FEEL 타격감", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "FEEL은 공용 타격감만 저장합니다. 몬스터 고유 VFX는 위 공간 계약을 따라 Monster Maker에서 배정합니다.",
                MessageType.Info);
            DrawPresentationPhaseCard(
                "실제 명중 FEEL 프로필",
                workingProfile != null && workingProfile.UsesProjectileVisual
                    ? "투사체가 실제 대상/경로에 닿아 피해가 적용된 지점의 타격감"
                    : "Marker에서 실제 피해가 적용된 대상 지점의 타격감",
                ref recipe.impactFeelPrefab,
                ref recipe.impactFeelLifetime,
                ref recipe.impactFeelPosition,
                ref recipe.impactFeelEuler,
                ref recipe.impactFeelScale);

            EditorGUILayout.HelpBox(
                "효과와 주요값, 타격점은 FEEL 연구소에서 프로필 단위로 수정합니다. 이곳에서는 저장된 프로필만 연결합니다.",
                MessageType.None);
        }

        private void DrawVfxContractOptions()
        {
            GUILayout.Label("4. 몬스터 고유 VFX 공간", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "여기서는 실제 VFX를 고르지 않습니다. 이 기본공격이 사용할 수 있는 VFX 종류·개수·발생 시점·기준 공간·반복·종료 방식만 정의합니다. 모든 공간은 선택 사항이며 Monster Maker에서 몬스터별로 사용 여부를 결정합니다.",
                MessageType.Info);

            recipe.vfxSlots ??= new List<BasicAttackWorkshopVfxSlot>();
            GUILayout.Label(
                $"현재 공간 {recipe.vfxSlots.Count}개 · 목록의 한 줄이 서로 다른 VFX 종류 1개입니다.",
                EditorStyles.wordWrappedMiniLabel);
            for (var index = 0; index < recipe.vfxSlots.Count; index++)
            {
                var slot = recipe.vfxSlots[index] ?? new BasicAttackWorkshopVfxSlot();
                recipe.vfxSlots[index] = slot;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(
                            $"VFX 공간 {index + 1:00}",
                            EditorStyles.miniBoldLabel,
                            GUILayout.MinWidth(0f),
                            GUILayout.ExpandWidth(true));
                        using (new EditorGUI.DisabledScope(index == 0))
                        {
                            if (GUILayout.Button("▲", GUILayout.Width(28f)))
                            {
                                (recipe.vfxSlots[index - 1], recipe.vfxSlots[index]) =
                                    (recipe.vfxSlots[index], recipe.vfxSlots[index - 1]);
                                GUIUtility.ExitGUI();
                            }
                        }
                        using (new EditorGUI.DisabledScope(index >= recipe.vfxSlots.Count - 1))
                        {
                            if (GUILayout.Button("▼", GUILayout.Width(28f)))
                            {
                                (recipe.vfxSlots[index + 1], recipe.vfxSlots[index]) =
                                    (recipe.vfxSlots[index], recipe.vfxSlots[index + 1]);
                                GUIUtility.ExitGUI();
                            }
                        }
                        var remove = GUILayout.Button("삭제", GUILayout.Width(44f));
                        if (Event.current.type == EventType.Repaint)
                        {
                            lastVfxHeaderRightmostRect = GUILayoutUtility.GetLastRect();
                        }
                        if (remove)
                        {
                            recipe.vfxSlots.RemoveAt(index);
                            GUIUtility.ExitGUI();
                        }
                    }

                    var inferredRole = BasicAttackWorkshopVfxRoles.Resolve(slot);
                    if (slot.editorRole != BasicAttackWorkshopVfxRole.Custom &&
                        inferredRole != slot.editorRole)
                    {
                        slot.editorRole = inferredRole;
                    }
                    var selectedRole = BasicAttackWorkshopVfxRoles.Popup(
                        "공간 역할",
                        slot.editorRole);
                    if (selectedRole != slot.editorRole)
                    {
                        slot.editorRole = selectedRole;
                        BasicAttackWorkshopVfxRoles.Apply(slot, selectedRole);
                    }
                    GUILayout.Label(
                        BasicAttackWorkshopVfxRoles.GetGuide(slot.editorRole),
                        EditorStyles.wordWrappedMiniLabel);

                    slot.displayName = EditorGUILayout.TextField("Maker 표시 이름", slot.displayName);
                    EditorGUILayout.PrefixLabel("용도 설명");
                    slot.description = MonsterWorkshopVisualTheme.DrawWrappedTextArea(
                        slot.description,
                        38f,
                        AssemblerContentWidth - 8f);
                    slot.assignmentScope = MonsterBasicAttackVfxEditorLabels.Popup(
                        "몬스터 적용",
                        slot.assignmentScope);
                    GUILayout.Label(
                        "선택 계약 · 각 몬스터가 VFX/SFX 사용 여부를 따로 결정합니다.",
                        EditorStyles.wordWrappedMiniLabel);

                    EditorGUILayout.HelpBox(
                        $"시점  {MonsterBasicAttackVfxEditorLabels.Get(slot.eventType)}    " +
                        $"위치  {MonsterBasicAttackVfxEditorLabels.Get(slot.anchor)}\n" +
                        $"반복  {MonsterBasicAttackVfxEditorLabels.Get(slot.multiplicity)}    " +
                        $"종료  {MonsterBasicAttackVfxEditorLabels.Get(slot.endPolicy)}" +
                        (slot.attachment == MonsterBasicAttackVfxAttachment.DeliveryVisual
                            ? "\n이동 판정체 외형  시점 보정 없음"
                            : "\n몬스터별 VFX 시점 보정  항상 사용 · 기본 0초"),
                        MessageType.None);

                    slot.showAdvanced = EditorGUILayout.Foldout(
                        slot.showAdvanced,
                        "고급 설정 · 기술 계약",
                        true);
                    if (slot.showAdvanced)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            slot.slotId = EditorGUILayout.TextField(
                                new GUIContent(
                                    "안정 슬롯 ID",
                                    "몬스터별 배정 데이터를 보존하는 키입니다. 출시 후에는 바꾸지 않습니다."),
                                slot.slotId);
                            slot.attachment = MonsterBasicAttackVfxEditorLabels.Popup(
                                "부착 방식",
                                slot.attachment);

                            if (slot.attachment == MonsterBasicAttackVfxAttachment.DeliveryVisual)
                            {
                                slot.eventType = MonsterBasicAttackVfxEvent.DeliverySpawn;
                                slot.anchor = MonsterBasicAttackVfxAnchor.ProjectileRoot;
                                slot.multiplicity = MonsterBasicAttackVfxMultiplicity.PerProjectile;
                                slot.endPolicy = MonsterBasicAttackVfxEndPolicy.DeliveryEnd;
                                EditorGUILayout.HelpBox(
                                    "배정한 Prefab이 실제 이동 판정체의 외형이 됩니다. 시점·위치·반복·종료 규칙은 고정됩니다.",
                                    workingProfile != null && workingProfile.UsesProjectileVisual
                                        ? MessageType.None
                                        : MessageType.Warning);
                            }
                            using (new EditorGUI.DisabledScope(
                                       slot.attachment == MonsterBasicAttackVfxAttachment.DeliveryVisual))
                            {
                                slot.eventType = MonsterBasicAttackVfxEditorLabels.Popup(
                                    "발생 시점",
                                    slot.eventType);
                                slot.anchor = MonsterBasicAttackVfxEditorLabels.Popup(
                                    "발생 위치",
                                    slot.anchor);
                                slot.multiplicity = MonsterBasicAttackVfxEditorLabels.Popup(
                                    "반복 방식",
                                    slot.multiplicity);
                                slot.endPolicy = MonsterBasicAttackVfxEditorLabels.Popup(
                                    "종료 방식",
                                    slot.endPolicy);
                            }
                            if (slot.endPolicy is MonsterBasicAttackVfxEndPolicy.Timed or
                                MonsterBasicAttackVfxEndPolicy.ParticleDuration)
                            {
                                slot.defaultLifetime = Mathf.Max(
                                    0.01f,
                                    EditorGUILayout.FloatField("기본 유지 시간", slot.defaultLifetime));
                            }
                        }
                    }

                    slot.editorRole = BasicAttackWorkshopVfxRoles.Resolve(slot);
                    if (!MonsterBasicAttackVfxResolver.UsesSafeSlotId(slot.slotId))
                    {
                        EditorGUILayout.HelpBox(
                            "슬롯 ID는 영문·숫자·밑줄·하이픈만 사용할 수 있습니다.",
                            MessageType.Error);
                    }
                    else if (recipe.vfxSlots.Count(candidate =>
                                 candidate != null &&
                                 string.Equals(
                                     candidate.slotId,
                                     slot.slotId,
                                     StringComparison.OrdinalIgnoreCase)) > 1)
                    {
                        EditorGUILayout.HelpBox(
                            "같은 기본공격 안에서 슬롯 ID가 중복되었습니다.",
                            MessageType.Error);
                    }
                }
            }

            if (GUILayout.Button("+ VFX 공간 추가", GUILayout.Height(27f)))
            {
                recipe.vfxSlots.Add(new BasicAttackWorkshopVfxSlot
                {
                    slotId = $"vfx_{recipe.vfxSlots.Count + 1:00}",
                    displayName = $"VFX 공간 {recipe.vfxSlots.Count + 1:00}",
                    editorRole = BasicAttackWorkshopVfxRole.Custom
                });
            }
        }

        private void DrawPresentationPhaseCard(
            string title,
            string positionGuide,
            ref GameObject feelPrefab,
            ref float lifetime,
            ref Vector3 position,
            ref Vector3 euler,
            ref float scale)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(title, EditorStyles.miniBoldLabel);
                var options = BasicAttackFeelPresetUtility.LoadFeelProfileOptions(feelPrefab);
                var labels = options.Select(option => option.Label).ToArray();
                var currentIndex = 0;
                for (var index = 0; index < options.Length; index++)
                {
                    if (options[index].Profile == feelPrefab)
                    {
                        currentIndex = index;
                        break;
                    }
                }
                var selectedIndex = EditorGUILayout.Popup("FEEL 프로필", currentIndex, labels);
                if (selectedIndex != currentIndex && selectedIndex >= 0 && selectedIndex < options.Length)
                {
                    feelPrefab = options[selectedIndex].Profile;
                    ApplyFeelProfileDefaults(feelPrefab, ref lifetime, ref position, ref euler, ref scale);
                }
                DrawFeelPrefabStatus(feelPrefab);
                GUILayout.Label(positionGuide, EditorStyles.wordWrappedMiniLabel);
                if (feelPrefab != null)
                {
                    var metadata = feelPrefab.GetComponent<BasicAttackFeelProfileMetadata>();
                    var displayLifetime = metadata?.Lifetime ?? lifetime;
                    var displayPosition = metadata?.LocalPosition ?? position;
                    var displayEuler = metadata?.LocalEulerAngles ?? euler;
                    var displayScale = metadata?.Scale ?? scale;
                    GUILayout.Label(
                        $"현재 프로필 값 · 수명 {displayLifetime:0.00}s · 위치 {displayPosition} · 회전 {displayEuler} · 배율 {displayScale:0.00}",
                        EditorStyles.wordWrappedMiniLabel);
                }
                if (GUILayout.Button("FEEL 연구소 열기"))
                {
                    BasicAttackFeelPresetUtility.OpenFormalLab();
                }
            }
        }

        private static void ApplyFeelProfileDefaults(
            GameObject profile,
            ref float lifetime,
            ref Vector3 position,
            ref Vector3 euler,
            ref float scale)
        {
            var metadata = profile != null ? profile.GetComponent<BasicAttackFeelProfileMetadata>() : null;
            lifetime = metadata?.Lifetime ?? 0.85f;
            position = metadata?.LocalPosition ?? Vector3.zero;
            euler = metadata?.LocalEulerAngles ?? Vector3.zero;
            scale = metadata?.Scale ?? 1f;
        }

        private static void DrawFeelPrefabStatus(GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            var runtime = prefab.GetComponent(typeof(IBasicAttackFeelRuntime)) as IBasicAttackFeelRuntime;
            if (runtime == null)
            {
                EditorGUILayout.HelpBox(
                    "Prefab 루트에 BasicAttackFeelRuntimeAdapter가 없습니다. MMF_Player와 어댑터를 같은 루트에 추가하세요.",
                    MessageType.Error);
                return;
            }
            if (!runtime.IsBasicAttackFeelConfigured)
            {
                EditorGUILayout.HelpBox(
                    "BasicAttackFeelRuntimeAdapter의 MMF_Player가 연결되지 않았습니다.",
                    MessageType.Error);
                return;
            }

            EditorGUILayout.HelpBox("FEEL 전용 프리셋 계약 통과", MessageType.None);
        }

        private static SfxCue DrawSfxCueField(string label, SfxCue cue)
        {
            var result = (SfxCue)EditorGUILayout.ObjectField(label, cue, typeof(SfxCue), false);
            if (result != null && !result.HasPlayableClip)
            {
                EditorGUILayout.HelpBox(
                    $"선택한 사운드 묶음에 재생 가능한 AudioClip이 없습니다: {result.name}",
                    MessageType.Error);
            }
            return result;
        }

        private void DrawSaveAndAssignControls()
        {
            if (workingProfile.TryValidate(out var error))
            {
                EditorGUILayout.HelpBox(
                    $"조합 검증 통과 · {BuildCompositionSummary(workingProfile)}",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (MonsterWorkshopVisualTheme.DrawTintedButton(
                        new GUIContent("새 프리셋으로 저장"),
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

            GUILayout.Space(4f);
            using (new EditorGUI.DisabledScope(originDraft == null || loadedProfile == null || workCopyDirty))
            {
                var label = originDraft == null
                    ? "몬스터메이커에서 열면 바로 배정할 수 있습니다"
                    : workCopyDirty
                        ? "먼저 저장해야 현재 몬스터에 배정할 수 있습니다"
                        : $"[{loadedProfile?.AttackId}] → {originDraft.MonsterId}에게 배정";
                if (MonsterWorkshopVisualTheme.DrawTintedButton(
                        new GUIContent(label),
                        MonsterWorkshopVisualTheme.FeelColor,
                        32f))
                {
                    AssignLoadedToOrigin();
                }
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                EditorGUILayout.HelpBox(message, messageType);
            }
        }

        private void DrawPreview()
        {
            using (var previewScope = new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                DrawPreviewPositionToolbar();
                DrawPreviewMotionContext();
                var totalPreviewHeight = Mathf.Max(480f, position.height - 335f);
                var eachPreviewHeight = totalPreviewHeight * 0.5f;
                GUILayout.Label("탑다운 판정 평면도", EditorStyles.boldLabel);
                var topDownRect = GUILayoutUtility.GetRect(
                    MinimumPreviewWidth,
                    10000f,
                    eachPreviewHeight,
                    eachPreviewHeight,
                    GUILayout.ExpandWidth(true));
                RenderPreview(topDownRect, true);

                GUILayout.Label("사선 연출 미리보기", EditorStyles.boldLabel);
                var presentationRect = GUILayoutUtility.GetRect(
                    MinimumPreviewWidth,
                    10000f,
                    eachPreviewHeight,
                    eachPreviewHeight,
                    GUILayout.ExpandWidth(true));
                RenderPreview(presentationRect, false);

                if (MonsterWorkshopVisualTheme.DrawTintedButton(
                        new GUIContent("공격 미리보기 재생"),
                        MonsterWorkshopVisualTheme.PreviewColor,
                        30f))
                {
                    PlayPreviewAttack();
                }
                GUILayout.Label(BuildPreviewTimingSummary(), EditorStyles.wordWrappedMiniLabel);
                GUILayout.Label(BuildCompositionSummary(workingProfile), EditorStyles.wordWrappedMiniLabel);
                if (workingProfile.Shape == MonsterBasicAttackShape.Fan)
                {
                    GUILayout.Label($"휘두름: {SweepDirectionName(workingProfile.SweepDirection)}", EditorStyles.wordWrappedMiniLabel);
                }
                EditorGUILayout.HelpBox(
                    "청록색 외곽선이 실제 공용 판정 모양입니다. VFX·SFX는 이 판정과 분리된 후속 슬롯입니다.",
                    MessageType.None);
                if (Event.current.type == EventType.Repaint)
                {
                    lastPreviewColumnRect = previewScope.rect;
                }
            }
        }

        private void DrawPreviewPositionToolbar()
        {
            if (workingProfile != null && !workingProfile.UsesProjectileVisual &&
                previewPositionTarget == BasicAttackPreviewPositionTarget.Projectile)
            {
                previewPositionTarget = BasicAttackPreviewPositionTarget.None;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("VFX 위치 직접 조절", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawPreviewPositionTargetButton(BasicAttackPreviewPositionTarget.Launch, "① 시작/총구");
                    if (workingProfile != null && workingProfile.UsesProjectileVisual)
                    {
                        DrawPreviewPositionTargetButton(BasicAttackPreviewPositionTarget.Projectile, "② 이동체");
                    }
                    DrawPreviewPositionTargetButton(BasicAttackPreviewPositionTarget.Impact, "③ 명중/폭발");
                }

                GUILayout.Label(
                    MonsterPositionAdjustWindow.CanOpen(originDraft)
                        ? "버튼을 누르면 현재 몬스터 모델만 보이는 별도 좌표 창이 열립니다."
                        : "Monster Maker에서 몬스터를 연 뒤 조립소를 열어야 직접 조절할 수 있습니다.",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawPreviewPositionTargetButton(BasicAttackPreviewPositionTarget target, string label)
        {
            var canOpen = target != BasicAttackPreviewPositionTarget.None &&
                          MonsterPositionAdjustWindow.CanOpen(originDraft) &&
                          !previewPlaying;
            using (new EditorGUI.DisabledScope(!canOpen))
            {
                var open = GUILayout.Button(
                    previewPlaying ? "재생 중 · 먼저 정지" : label,
                    GUILayout.Height(23f));
                if (Event.current.type == EventType.Repaint)
                {
                    lastPreviewToolbarRightmostRect = GUILayoutUtility.GetLastRect();
                }
                if (open)
                {
                    StopPreviewPlayback();
                    previewPositionTarget = target;
                    previewPositionDragging = false;
                    OpenPreviewPositionPopup(target);
                    Repaint();
                }
            }
        }

        private void OpenPreviewPositionPopup(BasicAttackPreviewPositionTarget target)
        {
            if (!MonsterPositionAdjustWindow.CanOpen(originDraft) || target == BasicAttackPreviewPositionTarget.None)
            {
                return;
            }

            var anchor = target == BasicAttackPreviewPositionTarget.Impact
                ? MonsterMakerPreviewAnchor.HitCenter
                : MonsterMakerPreviewAnchor.AttackOrigin;
            var binding = new MonsterMakerPreviewPositionBinding(
                "basicAttack." + target,
                PreviewPositionTargetName(target) + " VFX 위치",
                MonsterMakerPreviewPositionValueMode.AnchorOffset,
                anchor);
            var targetDraft = originDraft;
            MonsterPositionAdjustWindow.Open(
                this,
                targetDraft,
                binding,
                GetPreviewPositionValue(target),
                value =>
                {
                    if (originDraft != targetDraft || previewPlaying)
                    {
                        return false;
                    }

                    ApplyPreviewPositionValue(target, value);
                    return true;
                });
        }

        private void DrawPreviewMotionContext()
        {
            var attacks = originDraft?.Attacks;
            if (attacks == null || attacks.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "독립 조립 미리보기 · 몬스터 Motion이 없으므로 Recipe 시작을 동작 55% 지점으로 가정합니다. 이 값은 프리셋에 저장되지 않습니다.",
                    MessageType.None);
                return;
            }

            previewMotionIndex = Mathf.Clamp(previewMotionIndex, 0, attacks.Count - 1);
            var labels = attacks
                .Select((attack, index) => attack?.Clip == null
                    ? $"공격 {index + 1:00} · Clip 없음"
                    : $"공격 {index + 1:00} · {attack.Clip.name}")
                .ToArray();
            if (labels.Length > 1)
            {
                var selected = EditorGUILayout.Popup("타이밍 기준 Motion", previewMotionIndex, labels);
                if (selected != previewMotionIndex)
                {
                    previewMotionIndex = selected;
                    StopPreviewPlayback();
                }
            }
            else
            {
                EditorGUILayout.LabelField("타이밍 기준 Motion", labels[0]);
            }
        }

        private MonsterMakerAttackDraft ResolveSelectedPreviewAttack()
        {
            var attacks = originDraft?.Attacks;
            if (attacks == null || attacks.Count == 0)
            {
                return null;
            }
            previewMotionIndex = Mathf.Clamp(previewMotionIndex, 0, attacks.Count - 1);
            return attacks[previewMotionIndex];
        }

        private float ResolvePreviewDuration()
        {
            var attack = ResolveSelectedPreviewAttack();
            if (attack?.Clip == null)
            {
                return 1.15f;
            }
            return Mathf.Max(0.05f, attack.Clip.length / attack.PlaybackSpeed);
        }

        private List<float> ResolvePreviewImpactTimes()
        {
            var attack = ResolveSelectedPreviewAttack();
            var result = attack?.Markers
                .Where(item => item != null)
                .Select(item => Mathf.Clamp01(item.NormalizedTime))
                .OrderBy(time => time)
                .ToList();
            if (result == null || result.Count == 0)
            {
                return new List<float> { Mathf.Clamp01(previewStandaloneImpactTime) };
            }
            return result;
        }

        private float ResolvePreviewBaseAttackRange()
        {
            return workingProfile != null && workingProfile.CombatType == MonsterCombatType.Melee
                ? 2f
                : 4f;
        }

        private float ResolvePreviewActivationTimeSeconds(float motionDuration)
        {
            var markers = ResolvePreviewImpactTimes();
            var normalized = markers.Count == 0 ? previewStandaloneImpactTime : markers[0];
            return Mathf.Clamp01(normalized) * Mathf.Max(0.05f, motionDuration);
        }

        private float ResolvePreviewTravelDuration()
        {
            if (workingProfile == null || !workingProfile.UsesProjectileVisual)
            {
                return 0f;
            }

            var distance = previewAttacker != null && previewTarget != null
                ? Vector3.Distance(previewAttackerStart, previewTargetStart)
                : Mathf.Min(
                    workingProfile.ResolveRange(ResolvePreviewBaseAttackRange()),
                    4.5f);
            return Mathf.Max(0.01f, distance / Mathf.Max(0.01f, workingProfile.ProjectileSpeed));
        }

        private List<float> ResolvePreviewImpactTimesSeconds(float motionDuration)
        {
            var result = new List<float>();
            if (workingProfile == null)
            {
                return result;
            }

            var activation = ResolvePreviewActivationTimeSeconds(motionDuration);
            if (!workingProfile.UsesProjectileVisual)
            {
                for (var hitIndex = 0; hitIndex < workingProfile.HitCount; hitIndex++)
                {
                    result.Add(activation + hitIndex * workingProfile.RepeatHitInterval);
                }
                return result;
            }

            var travelDuration = ResolvePreviewTravelDuration();
            result.Add(activation + travelDuration);
            if (workingProfile.SequenceModule == MonsterBasicAttackSequenceModule.ReturnPasses)
            {
                result.Add(activation + travelDuration +
                           Mathf.Max(1f / 60f, 0.2f / Mathf.Max(0.01f, workingProfile.ProjectileSpeed)));
            }
            return result;
        }

        private float ResolvePreviewPlaybackDuration(float motionDuration)
        {
            var activation = ResolvePreviewActivationTimeSeconds(motionDuration);
            var impacts = ResolvePreviewImpactTimesSeconds(motionDuration);
            var duration = Mathf.Max(0.05f, motionDuration);
            if (workingProfile != null &&
                workingProfile.SequenceModule == MonsterBasicAttackSequenceModule.ReturnPasses)
            {
                duration = Mathf.Max(duration, activation + ResolvePreviewTravelDuration() * 2f + 0.1f);
            }
            if (impacts.Count > 0)
            {
                duration = Mathf.Max(duration, impacts[impacts.Count - 1] + 0.3f);
            }
            if (workingProfile?.LaunchFeedback?.VfxPrefab != null)
            {
                duration = Mathf.Max(duration, activation + workingProfile.LaunchFeedback.VfxLifetime);
            }
            if (workingProfile?.LaunchFeel?.Prefab != null)
            {
                duration = Mathf.Max(duration, activation + workingProfile.LaunchFeel.Lifetime);
            }
            if (workingProfile?.ImpactFeedback?.VfxPrefab != null && impacts.Count > 0)
            {
                duration = Mathf.Max(
                    duration,
                    impacts[impacts.Count - 1] + workingProfile.ImpactFeedback.VfxLifetime);
            }
            if (workingProfile?.ImpactFeel?.Prefab != null && impacts.Count > 0)
            {
                duration = Mathf.Max(duration, impacts[impacts.Count - 1] + workingProfile.ImpactFeel.Lifetime);
            }
            return duration;
        }

        private string BuildPreviewTimingSummary()
        {
            if (workingProfile == null)
            {
                return "타이밍 계산 전";
            }

            var motionDuration = ResolvePreviewDuration();
            var activation = ResolvePreviewActivationTimeSeconds(motionDuration);
            var markers = ResolvePreviewImpactTimes();
            var normalized = markers.Count == 0 ? previewStandaloneImpactTime : markers[0];
            var impacts = ResolvePreviewImpactTimesSeconds(motionDuration);
            var impactText = impacts.Count == 0
                ? "피해 없음"
                : string.Join(" / ", impacts.Select((time, index) => $"피해 {index + 1}: {time:0.000}초"));
            var activationName = workingProfile.UsesProjectileVisual ? "발사" : "Recipe 실행";
            return $"{activationName}: 동작 {normalized:0.000} ({activation:0.000}초) · {impactText}";
        }

        private void TriggerPreviewImpact(float elapsed, int hitIndex)
        {
            previewLastImpactElapsed = elapsed;
            var playFeedback = hitIndex == 0 || workingProfile?.RepeatImpactFeedback != false;
            previewLastImpactHasFeedback = playFeedback;
            if (playFeedback)
            {
                PlaySfxPreview(recipe.impactSfx);
            }
            if (previewImpactEffect != null)
            {
                previewImpactEffect.SetActive(playFeedback);
                if (playFeedback)
                {
                    foreach (var particle in previewImpactEffect.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }
            if (previewImpactFeel != null)
            {
                previewImpactFeel.SetActive(playFeedback);
                if (playFeedback)
                {
                    PlayFeelPreview(
                        previewImpactFeel,
                        previewTarget,
                        ResolvePreviewFeelIntensity(previewImpactStrength));
                }
            }
        }

        private void StartBlank()
        {
            recipe ??= new BasicAttackWorkshopRecipe();
            recipe.ResetBlank();
            if (profiles.Count == 0)
            {
                RefreshProfiles();
            }
            recipe.attackId = FindNextPresetId(recipe.family);
            loadedProfile = null;
            workCopyDirty = true;
            message = "기존 프리셋 복제가 아닌 빈 근거리 단일 공격에서 시작했습니다.";
            messageType = MessageType.Info;
            CompileWorkingProfile();
            Repaint();
        }

        private void LoadProfile(MonsterBasicAttackProfile profile)
        {
            if (profile == null)
            {
                return;
            }
            recipe ??= new BasicAttackWorkshopRecipe();
            recipe.Load(profile);
            loadedProfile = profile;
            workCopyDirty = false;
            message = $"프리셋을 작업 사본으로 불러왔습니다: [{profile.AttackId}] {profile.DisplayName}";
            messageType = MessageType.Info;
            CompileWorkingProfile();
            Repaint();
        }

        private void SaveAsNew()
        {
            CompileWorkingProfile();
            if (!ValidateIdentityAndRecipe(null, out var error))
            {
                SetError(error);
                return;
            }

            EnsureCustomFolder();
            var fileName = SanitizeToken(recipe.attackId);
            var path = $"{MonsterBasicAttackPresetUtility.CustomProfileRoot}/{fileName}.asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                SetError($"같은 ID의 프리셋 자산이 이미 있습니다: {path}");
                return;
            }
            var asset = CreateInstance<MonsterBasicAttackProfile>();
            recipe.Compile(asset);
            asset.name = fileName;
            AssetDatabase.CreateAsset(asset, path);
            if (!MonsterBasicAttackPresetUtility.TrySaveRecipe(asset, out error))
            {
                AssetDatabase.DeleteAsset(path);
                DestroyImmediate(asset);
                SetError(error);
                return;
            }

            loadedProfile = asset;
            workCopyDirty = false;
            message = $"새 프리셋을 저장했습니다: {path}";
            messageType = MessageType.Info;
            RefreshProfiles();
        }

        private void UpdateLoaded()
        {
            if (loadedProfile == null)
            {
                return;
            }
            CompileWorkingProfile();
            if (!ValidateIdentityAndRecipe(loadedProfile, out var error))
            {
                SetError(error);
                return;
            }

            var usageCount = MonsterBasicAttackPresetUtility.CountDraftUsages(loadedProfile);
            if (usageCount > 0 && !EditorUtility.DisplayDialog(
                    "공유 프리셋 업데이트",
                    $"이 프리셋을 {usageCount}마리가 사용 중입니다. 저장하면 모두에게 적용됩니다.",
                    "업데이트", "취소"))
            {
                return;
            }

            Undo.RecordObject(loadedProfile, "기본공격 프리셋 업데이트");
            recipe.Compile(loadedProfile);
            if (!MonsterBasicAttackPresetUtility.TrySaveRecipe(loadedProfile, out error))
            {
                Undo.PerformUndo();
                SetError(error);
                return;
            }

            workCopyDirty = false;
            message = $"프리셋을 업데이트했습니다: [{loadedProfile.AttackId}] {loadedProfile.DisplayName}";
            messageType = MessageType.Info;
            RefreshProfiles();
        }

        private void ForkLoadedAsNew()
        {
            if (loadedProfile == null || recipe == null)
            {
                return;
            }

            loadedProfile = null;
            recipe.attackId = FindNextPresetId(recipe.family);
            workCopyDirty = true;
            message = $"새 프리셋 작업 사본으로 분기했습니다. 새 ID는 {recipe.attackId}입니다.";
            messageType = MessageType.Info;
            CompileWorkingProfile();
            Repaint();
        }

        private void AssignLoadedToOrigin()
        {
            if (originDraft == null || loadedProfile == null || workCopyDirty)
            {
                return;
            }
            Undo.RecordObject(originDraft, "기본공격 프리셋 배정");
            originDraft.EditorSetBasicAttackProfile(loadedProfile);
            originDraft.EditorAdoptBasicAttackProfileTuning();
            EditorUtility.SetDirty(originDraft);
            AssetDatabase.SaveAssetIfDirty(originDraft);
            MonsterBasicAttackPresetUtility.InvalidateUsageCache();
            message = $"{originDraft.MonsterId}에게 [{loadedProfile.AttackId}]을 배정했습니다.";
            messageType = MessageType.Info;
            PresetAssigned?.Invoke();
        }

        private bool ValidateIdentityAndRecipe(MonsterBasicAttackProfile excluded, out string error)
        {
            var id = recipe.attackId?.Trim();
            if (string.IsNullOrWhiteSpace(id) || id.Any(character =>
                    !char.IsLetterOrDigit(character) && character != '_'))
            {
                error = "프리셋 ID는 영문·숫자·밑줄만 사용할 수 있습니다.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(recipe.displayName))
            {
                error = "표시 이름을 입력해야 합니다.";
                return false;
            }
            if (!id.StartsWith(recipe.RequiredIdPrefix, StringComparison.OrdinalIgnoreCase))
            {
                error = $"현재 공격 계열의 프리셋 ID는 {recipe.RequiredIdPrefix}로 시작해야 합니다.";
                return false;
            }
            if (excluded != null &&
                !string.Equals(id, excluded.AttackId, StringComparison.OrdinalIgnoreCase))
            {
                error = $"저장된 프리셋의 ID는 바꿀 수 없습니다. 새 ID가 필요하면 새 프리셋으로 분기하세요: {excluded.AttackId}";
                return false;
            }
            if (excluded != null)
            {
                var assetName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(excluded));
                if (!string.Equals(assetName, excluded.AttackId, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"프리셋 ID와 파일명이 다릅니다. 새 프리셋으로 분기해 정리하세요: {assetName} / {excluded.AttackId}";
                    return false;
                }
            }
            if (!workingProfile.TryValidate(out error))
            {
                return false;
            }

            var duplicate = FindProfiles().FirstOrDefault(profile =>
                profile != null && profile != excluded &&
                string.Equals(profile.AttackId, id, StringComparison.OrdinalIgnoreCase));
            if (duplicate != null)
            {
                error = $"같은 프리셋 ID가 이미 있습니다: {AssetDatabase.GetAssetPath(duplicate)}";
                return false;
            }
            return true;
        }

        private void EnsureWorkingProfile()
        {
            recipe ??= new BasicAttackWorkshopRecipe();
            if (string.IsNullOrWhiteSpace(recipe.attackId))
            {
                recipe.ResetBlank();
            }
            if (workingProfile == null)
            {
                workingProfile = CreateInstance<MonsterBasicAttackProfile>();
                workingProfile.hideFlags = HideFlags.HideAndDontSave;
                CompileWorkingProfile();
            }
        }

        private void CompileWorkingProfile()
        {
            if (recipe == null)
            {
                return;
            }
            if (workingProfile == null)
            {
                workingProfile = CreateInstance<MonsterBasicAttackProfile>();
                workingProfile.hideFlags = HideFlags.HideAndDontSave;
            }
            recipe.Compile(workingProfile);
            RebuildPreview();
        }

        private void RefreshProfiles()
        {
            profiles.Clear();
            profiles.AddRange(FindProfiles());
            Repaint();
        }

        private static IEnumerable<MonsterBasicAttackProfile> FindProfiles()
        {
            return AssetDatabase.FindAssets(
                    "t:MonsterBasicAttackProfile",
                    new[] { MonsterBasicAttackPresetUtility.ProfileRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterBasicAttackProfile>)
                .Where(profile => profile != null)
                .OrderBy(profile => MonsterBasicAttackPresetUtility.IsBuiltInProfile(profile) ? 0 : 1)
                .ThenBy(profile => profile.AttackId, StringComparer.OrdinalIgnoreCase);
        }

        private bool MatchesSearch(MonsterBasicAttackProfile profile)
        {
            return string.IsNullOrWhiteSpace(search) ||
                   profile.AttackId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   profile.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string FindNextPresetId(BasicAttackWorkshopFamily family)
        {
            var prefix = family switch
            {
                BasicAttackWorkshopFamily.Ranged => "BA_R_",
                BasicAttackWorkshopFamily.Special => "BA_S_",
                _ => "BA_M_"
            };
            var maximum = 0;
            foreach (var profile in profiles)
            {
                if (profile == null || !profile.AttackId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var suffix = profile.AttackId.Substring(prefix.Length);
                if (int.TryParse(suffix, out var number))
                {
                    maximum = Mathf.Max(maximum, number);
                }
            }
            return $"{prefix}{maximum + 1:00}";
        }

        private static string BuildCompositionSummary(MonsterBasicAttackProfile profile)
        {
            return profile == null
                ? "조립 정보 없음"
                : $"{CombatTypeName(profile)} · {DeliveryName(profile)} · {CollisionName(profile)} · " +
                  $"{SequenceName(profile)} · {MovementName(profile)} · {ShapeName(profile.Shape)}";
        }

        private static string CombatTypeName(MonsterBasicAttackProfile profile)
        {
            return profile.PresentationKind is MonsterBasicAttackPresentationKind.Returning or
                MonsterBasicAttackPresentationKind.Breath or MonsterBasicAttackPresentationKind.Beam or
                MonsterBasicAttackPresentationKind.Wave
                ? "특수"
                : profile.CombatType == MonsterCombatType.Melee ? "근거리" : "원거리";
        }

        private static string DeliveryName(MonsterBasicAttackProfile profile)
        {
            return profile.PresentationKind switch
            {
                MonsterBasicAttackPresentationKind.Returning => "왕복 투사체",
                MonsterBasicAttackPresentationKind.Breath => "브레스",
                MonsterBasicAttackPresentationKind.Beam => "빔",
                MonsterBasicAttackPresentationKind.Wave => "진행 파동",
                MonsterBasicAttackPresentationKind.Instant => "즉발",
                _ when profile.DeliveryModule == MonsterBasicAttackDeliveryModule.Projectile => "투사체",
                _ => "직접 판정"
            };
        }

        private static string CollisionName(MonsterBasicAttackProfile profile)
        {
            return profile.CollisionModule switch
            {
                MonsterBasicAttackCollisionModule.StopOnFirstTarget => "첫 대상 정지",
                MonsterBasicAttackCollisionModule.Pierce => "관통",
                MonsterBasicAttackCollisionModule.AreaImpact => "범위 폭발",
                MonsterBasicAttackCollisionModule.PassThrough => "통과",
                _ => "즉시 판정"
            };
        }

        private static string SequenceName(MonsterBasicAttackProfile profile)
        {
            return profile.SequenceModule switch
            {
                MonsterBasicAttackSequenceModule.Burst => $"{profile.HitCount}연타",
                MonsterBasicAttackSequenceModule.ReturnPasses => "왕복 2단",
                _ => "단타"
            };
        }

        private static string MovementName(MonsterBasicAttackProfile profile)
        {
            return profile.MovementModule == MonsterBasicAttackMovementModule.Dash ? "실제 돌진" : "제자리";
        }

        private static string ShapeName(MonsterBasicAttackShape shape)
        {
            return shape switch
            {
                MonsterBasicAttackShape.Fan => "부채꼴",
                MonsterBasicAttackShape.Line => "직선",
                MonsterBasicAttackShape.Circle => "원형",
                _ => "단일"
            };
        }

        private static string SweepDirectionName(MonsterBasicAttackSweepDirection direction)
        {
            return direction switch
            {
                MonsterBasicAttackSweepDirection.LeftToRight => "좌→우",
                MonsterBasicAttackSweepDirection.RightToLeft => "우→좌",
                _ => "동시"
            };
        }

        private void RebuildPreview()
        {
            ClearPreviewContents();
            if (workingProfile == null)
            {
                return;
            }

            MonsterWorkshopPreviewSceneRecovery.RecoverOrphanedScenesIfNeeded();
            if (previewUtility != null && !MonsterWorkshopPreviewSceneRecovery.HasRenderingMask(previewUtility))
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }
            if (previewUtility == null)
            {
                previewUtility = new PreviewRenderUtility();
                previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
                previewUtility.camera.backgroundColor = new Color(0.055f, 0.065f, 0.08f, 1f);
                previewUtility.camera.nearClipPlane = 0.05f;
                previewUtility.camera.farClipPlane = 30f;
                previewUtility.camera.orthographic = true;
                previewUtility.camera.orthographicSize = 3.4f;
                previewUtility.camera.transform.position = new Vector3(0f, 10f, 2.1f);
                previewUtility.camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                previewUtility.lights[0].intensity = 1.25f;
                previewUtility.lights[0].transform.rotation = Quaternion.Euler(45f, 35f, 0f);
                previewUtility.ambientColor = new Color(0.35f, 0.35f, 0.4f);
            }

            previewRoot = new GameObject("[Basic Attack Workshop Preview]")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            previewGroundMaterial = CreatePreviewMaterial(new Color(0.11f, 0.13f, 0.16f));
            previewSourceMaterial = CreatePreviewMaterial(new Color(0.15f, 0.8f, 0.7f));
            previewTargetMaterial = CreatePreviewMaterial(new Color(0.95f, 0.35f, 0.3f));
            previewAttackMaterial = CreatePreviewMaterial(new Color(1f, 0.8f, 0.15f));
            CreatePreviewPrimitive(
                PrimitiveType.Cube,
                "Ground",
                new Vector3(0f, -0.08f, 1.8f),
                new Vector3(8f, 0.1f, 8f),
                previewGroundMaterial);
            previewAttacker = CreatePreviewPrimitive(
                PrimitiveType.Capsule,
                "Attacker",
                new Vector3(0f, 0f, 0.15f),
                new Vector3(0.55f, 0.45f, 0.55f),
                previewSourceMaterial);
            var previewAttackRange = ResolvePreviewBaseAttackRange();
            var resolvedRange = Mathf.Min(workingProfile.ResolveRange(previewAttackRange), 4.5f);
            var targetPoint = Vector3.forward * Mathf.Max(0.7f, resolvedRange);
            previewTarget = CreateStandardFeelTargetPreview(targetPoint);
            previewAttackMovers.Clear();
            if (workingProfile.UsesProjectileVisual)
            {
                for (var index = 0; index < workingProfile.ProjectileCount; index++)
                {
                    var mover = CreateProjectilePreview(
                        workingProfile.ProjectileFeedback,
                        workingProfile.ProjectileFeel,
                        previewAttacker.transform.localPosition);
                    mover.name = $"Attack Delivery {index + 1:00}";
                    mover.SetActive(false);
                    previewAttackMovers.Add(mover);
                }
            }
            previewAttackMover = previewAttackMovers.FirstOrDefault();
            previewLaunchEffect = CreateFeedbackPreview(
                workingProfile.LaunchFeedback,
                "Launch VFX",
                previewAttacker.transform.localPosition);
            previewImpactEffect = CreateFeedbackPreview(
                workingProfile.ImpactFeedback,
                "Impact VFX",
                targetPoint);
            previewLaunchFeel = CreateFeelPreview(
                workingProfile.LaunchFeel,
                "Launch FEEL",
                previewAttacker.transform.localPosition);
            previewImpactFeel = CreateFeelPreview(
                workingProfile.ImpactFeel,
                "Impact FEEL",
                targetPoint);
            previewImpactPulse = CreatePreviewPrimitive(
                PrimitiveType.Sphere,
                "Impact / Explosion",
                targetPoint,
                Vector3.one * 0.01f,
                previewAttackMaterial);
            previewImpactPulse.SetActive(false);
            previewAttackerStart = previewAttacker.transform.localPosition;
            previewTargetStart = previewTarget.transform.localPosition;
            MonsterAttackAreaIndicator.Create(
                previewRoot.transform,
                workingProfile,
                Vector3.zero,
                Vector3.forward,
                targetPoint,
                previewAttackRange,
                new Color(0.1f, 1f, 0.85f, 1f),
                false);
            previewUtility.AddSingleGO(previewRoot);
        }

        private void RenderPreview(Rect rect, bool topDown)
        {
            if (previewUtility != null && !MonsterWorkshopPreviewSceneRecovery.HasRenderingMask(previewUtility))
            {
                RebuildPreview();
            }
            ConfigurePreviewCamera(rect, topDown);
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }
            if (Event.current.type != EventType.Repaint || previewUtility == null || rect.width <= 1f)
            {
                return;
            }
            if (topDown)
            {
                previewUtility.camera.orthographic = true;
                previewUtility.camera.orthographicSize = 3.4f;
                previewUtility.camera.transform.position = new Vector3(0f, 10f, 2.1f);
                previewUtility.camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            }
            else
            {
                previewUtility.camera.orthographic = false;
                previewUtility.camera.fieldOfView = 34f;
                previewUtility.camera.transform.position = new Vector3(5.2f, 6.4f, -6.8f);
                previewUtility.camera.transform.LookAt(new Vector3(0f, 0f, 2.1f));
            }
            previewUtility.camera.aspect = rect.width / Mathf.Max(1f, rect.height);
            previewUtility.BeginPreview(rect, GUIStyle.none);
            previewUtility.Render(true);
            var texture = previewUtility.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, false);
            DrawPreviewPositionMarkers(rect);
        }

        private void ConfigurePreviewCamera(Rect rect, bool topDown)
        {
            if (previewUtility == null || rect.width <= 1f || rect.height <= 1f)
            {
                return;
            }

            if (topDown)
            {
                previewUtility.camera.orthographic = true;
                previewUtility.camera.orthographicSize = 3.4f;
                previewUtility.camera.transform.position = new Vector3(0f, 10f, 2.1f);
                previewUtility.camera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            }
            else
            {
                previewUtility.camera.orthographic = false;
                previewUtility.camera.fieldOfView = 34f;
                previewUtility.camera.transform.position = new Vector3(5.2f, 6.4f, -6.8f);
                previewUtility.camera.transform.LookAt(new Vector3(0f, 0f, 2.1f));
            }

            previewUtility.camera.aspect = rect.width / Mathf.Max(1f, rect.height);
        }

        private void HandlePreviewPositionInput(Rect rect, bool topDown, Event current)
        {
            if (current == null || previewUtility == null || previewRoot == null)
            {
                return;
            }

            if (previewPositionDragging && previewPositionDragTopDown == topDown)
            {
                if (current.rawType == EventType.MouseUp)
                {
                    previewPositionDragging = false;
                    if (GUIUtility.hotControl == previewPositionHotControl)
                    {
                        GUIUtility.hotControl = 0;
                    }
                    current.Use();
                    return;
                }

                if (current.type == EventType.MouseDrag)
                {
                    var value = previewPositionDragValueStart;
                    if (topDown)
                    {
                        if (!TryGetPreviewMarkerWorld(previewPositionTarget, out var markerWorld) ||
                            !MonsterPreviewPositionHandleUtility.TryGuiPointToHorizontalPlane(
                                previewUtility.camera,
                                rect,
                                current.mousePosition,
                                markerWorld.y,
                                out var planePoint))
                        {
                            return;
                        }

                        var worldPoint = planePoint + previewPositionDragWorldOffset;
                        var localPoint = previewRoot.transform.InverseTransformPoint(worldPoint);
                        var anchor = GetPreviewPositionAnchorLocal(previewPositionTarget);
                        value.x = localPoint.x - anchor.x;
                        value.z = localPoint.z - anchor.z;
                    }
                    else
                    {
                        value = MonsterPreviewPositionHandleUtility.ApplyHeightDrag(
                            previewPositionDragValueStart,
                            current.mousePosition.y - previewPositionDragMouseStart.y);
                    }

                    ApplyPreviewPositionValue(previewPositionTarget, value);
                    current.Use();
                    return;
                }
            }

            if (current.type != EventType.MouseDown || current.button != 0 || !rect.Contains(current.mousePosition))
            {
                return;
            }

            var target = ResolveClosestPreviewPositionTarget(rect, current.mousePosition);
            if (target == BasicAttackPreviewPositionTarget.None ||
                !TryGetPreviewMarkerWorld(target, out var worldPosition))
            {
                return;
            }

            StopPreviewPlayback();
            previewPositionTarget = target;
            previewPositionDragTopDown = topDown;
            previewPositionDragMouseStart = current.mousePosition;
            previewPositionDragValueStart = GetPreviewPositionValue(target);
            previewPositionDragWorldOffset = Vector3.zero;
            if (topDown && MonsterPreviewPositionHandleUtility.TryGuiPointToHorizontalPlane(
                    previewUtility.camera,
                    rect,
                    current.mousePosition,
                    worldPosition.y,
                    out var mouseWorld))
            {
                previewPositionDragWorldOffset = worldPosition - mouseWorld;
            }

            previewPositionDragging = true;
            previewPositionHotControl = GUIUtility.GetControlID(
                "BasicAttackPositionHandle".GetHashCode(),
                FocusType.Passive);
            GUIUtility.hotControl = previewPositionHotControl;
            current.Use();
            Repaint();
        }

        private BasicAttackPreviewPositionTarget ResolveClosestPreviewPositionTarget(Rect rect, Vector2 mousePosition)
        {
            var targets = workingProfile != null && workingProfile.UsesProjectileVisual
                ? new[]
                {
                    BasicAttackPreviewPositionTarget.Launch,
                    BasicAttackPreviewPositionTarget.Projectile,
                    BasicAttackPreviewPositionTarget.Impact
                }
                : new[]
                {
                    BasicAttackPreviewPositionTarget.Launch,
                    BasicAttackPreviewPositionTarget.Impact
                };
            var closest = BasicAttackPreviewPositionTarget.None;
            var closestDistance = 14f;
            foreach (var target in targets)
            {
                if (!TryGetPreviewMarkerWorld(target, out var worldPosition) ||
                    !MonsterPreviewPositionHandleUtility.TryWorldToGuiPoint(
                        previewUtility.camera,
                        rect,
                        worldPosition,
                        out var guiPoint))
                {
                    continue;
                }

                var distance = Vector2.Distance(mousePosition, guiPoint);
                if (distance > closestDistance ||
                    Mathf.Approximately(distance, closestDistance) && target != previewPositionTarget)
                {
                    continue;
                }

                closest = target;
                closestDistance = distance;
            }

            return closest;
        }

        private void DrawPreviewPositionMarkers(Rect rect)
        {
            DrawPreviewPositionMarker(
                rect,
                BasicAttackPreviewPositionTarget.Launch,
                new Color(0.15f, 0.95f, 0.9f, 1f));
            if (workingProfile != null && workingProfile.UsesProjectileVisual)
            {
                DrawPreviewPositionMarker(
                    rect,
                    BasicAttackPreviewPositionTarget.Projectile,
                    new Color(1f, 0.82f, 0.18f, 1f));
            }
            DrawPreviewPositionMarker(
                rect,
                BasicAttackPreviewPositionTarget.Impact,
                new Color(1f, 0.32f, 0.26f, 1f));
        }

        private void DrawPreviewPositionMarker(
            Rect rect,
            BasicAttackPreviewPositionTarget target,
            Color color)
        {
            if (!TryGetPreviewMarkerWorld(target, out var worldPosition) ||
                !MonsterPreviewPositionHandleUtility.TryWorldToGuiPoint(
                    previewUtility.camera,
                    rect,
                    worldPosition,
                    out var guiPoint) ||
                !rect.Contains(guiPoint))
            {
                return;
            }

            var selected = previewPositionTarget == target;
            Handles.BeginGUI();
            Handles.color = selected ? Color.white : color;
            Handles.DrawSolidDisc(guiPoint, Vector3.forward, selected ? 7f : 5f);
            Handles.color = Color.black;
            Handles.DrawWireDisc(guiPoint, Vector3.forward, selected ? 8f : 6f);
            Handles.EndGUI();
        }

        private bool TryGetPreviewMarkerWorld(BasicAttackPreviewPositionTarget target, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (previewRoot == null || target == BasicAttackPreviewPositionTarget.None)
            {
                return false;
            }

            var localPosition = GetPreviewPositionAnchorLocal(target) + GetPreviewPositionValue(target);
            worldPosition = previewRoot.transform.TransformPoint(localPosition);
            return true;
        }

        private Vector3 GetPreviewPositionAnchorLocal(BasicAttackPreviewPositionTarget target)
        {
            return target switch
            {
                BasicAttackPreviewPositionTarget.Launch => previewAttackerStart,
                BasicAttackPreviewPositionTarget.Projectile when previewPlaying &&
                    previewAttackMover != null && previewAttackMover.activeSelf =>
                    previewAttackMover.transform.localPosition,
                BasicAttackPreviewPositionTarget.Projectile =>
                    Vector3.Lerp(previewAttackerStart, previewTargetStart, 0.5f),
                BasicAttackPreviewPositionTarget.Impact => previewTargetStart,
                _ => Vector3.zero
            };
        }

        private Vector3 GetPreviewPositionValue(BasicAttackPreviewPositionTarget target)
        {
            return target switch
            {
                BasicAttackPreviewPositionTarget.Launch => recipe?.launchVfxPosition ?? Vector3.zero,
                BasicAttackPreviewPositionTarget.Projectile => recipe?.projectileVfxPosition ?? Vector3.zero,
                BasicAttackPreviewPositionTarget.Impact => recipe?.impactVfxPosition ?? Vector3.zero,
                _ => Vector3.zero
            };
        }

        private void ApplyPreviewPositionValue(BasicAttackPreviewPositionTarget target, Vector3 value)
        {
            if (recipe == null)
            {
                return;
            }

            switch (target)
            {
                case BasicAttackPreviewPositionTarget.Launch:
                    recipe.launchVfxPosition = value;
                    break;
                case BasicAttackPreviewPositionTarget.Projectile:
                    recipe.projectileVfxPosition = value;
                    break;
                case BasicAttackPreviewPositionTarget.Impact:
                    recipe.impactVfxPosition = value;
                    break;
                default:
                    return;
            }

            recipe.Normalize();
            CompileWorkingProfile();
            workCopyDirty = true;
            message = null;
            Repaint();
        }

        private static string PreviewPositionTargetName(BasicAttackPreviewPositionTarget target)
        {
            return target switch
            {
                BasicAttackPreviewPositionTarget.Launch => "① 총구",
                BasicAttackPreviewPositionTarget.Projectile => "② 이동체",
                BasicAttackPreviewPositionTarget.Impact => "③ 명중",
                _ => "위치"
            };
        }

        private static string FormatPosition(Vector3 value)
        {
            return $"({value.x:0.##}, {value.y:0.##}, {value.z:0.##})";
        }

        private GameObject CreatePreviewPrimitive(
            PrimitiveType type,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = objectName;
            item.hideFlags = HideFlags.HideAndDontSave;
            item.transform.SetParent(previewRoot.transform, false);
            item.transform.localPosition = localPosition;
            item.transform.localScale = localScale;
            var collider = item.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyImmediate(collider);
            }
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        private void PlayPreviewAttack()
        {
            if (workingProfile == null || previewAttacker == null || previewTarget == null)
            {
                return;
            }
            previewPlaybackStart = EditorApplication.timeSinceStartup;
            previewPlaying = true;
            previewNextImpactIndex = 0;
            previewLastImpactElapsed = -1f;
            previewLastImpactHasFeedback = false;
            previewActivationElapsed = -1f;
            previewDeliveryActivated = false;
            ResetPreviewPlaybackObjects();
            SetPreviewUpdateSubscribed(true);
            Repaint();
        }

        private void ActivatePreviewDelivery(float elapsed)
        {
            if (previewDeliveryActivated)
            {
                return;
            }

            previewDeliveryActivated = true;
            previewActivationElapsed = elapsed;
            PlaySfxPreview(recipe.launchSfx);
            if (workingProfile.UsesProjectileVisual)
            {
                PlaySfxPreview(recipe.projectileSfx);
                foreach (var mover in previewAttackMovers)
                {
                    if (mover != null)
                    {
                        mover.SetActive(true);
                        PlayFeelPreview(mover, mover);
                    }
                }
            }
            if (previewLaunchEffect != null)
            {
                previewLaunchEffect.SetActive(true);
            }
            if (previewLaunchFeel != null)
            {
                previewLaunchFeel.SetActive(true);
                PlayFeelPreview(previewLaunchFeel, previewAttacker);
            }
            if (workingProfile.MovementModule == MonsterBasicAttackMovementModule.Dash)
            {
                var direction = (previewTargetStart - previewAttackerStart).normalized;
                previewAttacker.transform.localPosition = previewAttackerStart +
                    direction * Mathf.Min(workingProfile.DashDistance, 1.5f);
            }
        }

        private void TickPreviewPlayback()
        {
            if (!previewPlaying || workingProfile == null || previewAttacker == null || previewTarget == null)
            {
                return;
            }

            var elapsed = (float)(EditorApplication.timeSinceStartup - previewPlaybackStart);
            var motionDuration = ResolvePreviewDuration();
            var playbackDuration = ResolvePreviewPlaybackDuration(motionDuration);
            var activationTime = ResolvePreviewActivationTimeSeconds(motionDuration);
            if (!previewDeliveryActivated && elapsed >= activationTime)
            {
                ActivatePreviewDelivery(activationTime);
            }

            var impactTimes = ResolvePreviewImpactTimesSeconds(motionDuration);
            while (previewNextImpactIndex < impactTimes.Count && elapsed >= impactTimes[previewNextImpactIndex])
            {
                TriggerPreviewImpact(elapsed, previewNextImpactIndex);
                previewNextImpactIndex++;
            }
            var impactAge = previewLastImpactElapsed < 0f
                ? float.MaxValue
                : elapsed - previewLastImpactElapsed;
            var impactPulse = 1f + Mathf.Clamp01(1f - impactAge / 0.14f) * 0.35f;
            previewTarget.transform.localScale = previewTargetBaseScale * impactPulse;

            if (!previewDeliveryActivated)
            {
                var windup = Mathf.Sin(Mathf.Clamp01(elapsed / Mathf.Max(0.01f, activationTime)) * Mathf.PI);
                previewAttacker.transform.localScale = new Vector3(0.55f, 0.45f, 0.55f) *
                                                       (1f + windup * 0.12f);
            }
            else
            {
                previewAttacker.transform.localScale = new Vector3(0.55f, 0.45f, 0.55f);
            }

            var travelAge = previewDeliveryActivated ? Mathf.Max(0f, elapsed - previewActivationElapsed) : 0f;
            var travelDuration = ResolvePreviewTravelDuration();
            for (var index = 0; index < previewAttackMovers.Count; index++)
            {
                var mover = previewAttackMovers[index];
                if (mover == null || !mover.activeSelf)
                {
                    continue;
                }

                var travel = Mathf.Clamp01(travelAge / Mathf.Max(0.01f, travelDuration));
                if (workingProfile.ProjectileTravel == MonsterBasicAttackProjectileTravel.Returning)
                {
                    travel = travelAge <= travelDuration
                        ? Mathf.Clamp01(travelAge / Mathf.Max(0.01f, travelDuration))
                        : 1f - Mathf.Clamp01((travelAge - travelDuration) / Mathf.Max(0.01f, travelDuration));
                }

                var ratio = previewAttackMovers.Count <= 1
                    ? 0f
                    : index / (float)(previewAttackMovers.Count - 1) - 0.5f;
                var endOffset = Quaternion.Euler(0f, ratio * workingProfile.ProjectileSpreadAngle, 0f) *
                                (previewTargetStart - previewAttackerStart);
                mover.transform.localPosition = Vector3.Lerp(
                    previewAttackerStart,
                    previewAttackerStart + endOffset,
                    travel);
                SimulateParticles(mover, travelAge);
                if (workingProfile.ProjectileTravel != MonsterBasicAttackProjectileTravel.Returning &&
                    travelAge >= travelDuration)
                {
                    mover.SetActive(false);
                }
                else if (workingProfile.ProjectileTravel == MonsterBasicAttackProjectileTravel.Returning &&
                         travelAge >= travelDuration * 2f)
                {
                    mover.SetActive(false);
                }
            }

            if (previewImpactPulse != null)
            {
                var explosion = workingProfile.CollisionModule == MonsterBasicAttackCollisionModule.AreaImpact ||
                                workingProfile.Shape == MonsterBasicAttackShape.Circle;
                var pulse = Mathf.Clamp01(impactAge / 0.28f);
                var active = explosion && impactAge >= 0f && pulse < 1f;
                previewImpactPulse.SetActive(active);
                if (active)
                {
                    var radius = Mathf.Max(workingProfile.Radius, 0.35f);
                    previewImpactPulse.transform.localScale = Vector3.one *
                        Mathf.Lerp(0.05f, radius * 2f, Mathf.SmoothStep(0f, 1f, pulse));
                }
            }
            if (previewLaunchEffect != null)
            {
                var launchLifetime = Mathf.Max(0.05f, workingProfile.LaunchFeedback?.VfxLifetime ?? 0.4f);
                var launchAge = previewActivationElapsed < 0f
                    ? float.MaxValue
                    : elapsed - previewActivationElapsed;
                previewLaunchEffect.SetActive(launchAge >= 0f && launchAge < launchLifetime);
                if (previewLaunchEffect.activeSelf)
                {
                    SimulateParticles(previewLaunchEffect, launchAge);
                }
            }
            if (previewLaunchFeel != null)
            {
                var launchLifetime = Mathf.Max(0.05f, workingProfile.LaunchFeel?.Lifetime ?? 0.4f);
                var launchAge = previewActivationElapsed < 0f
                    ? float.MaxValue
                    : elapsed - previewActivationElapsed;
                previewLaunchFeel.SetActive(launchAge >= 0f && launchAge < launchLifetime);
            }
            if (previewImpactEffect != null)
            {
                var lifetime = Mathf.Max(0.05f, workingProfile.ImpactFeedback?.VfxLifetime ?? 0.4f);
                var impactActive = previewLastImpactHasFeedback && impactAge >= 0f && impactAge < lifetime;
                previewImpactEffect.SetActive(impactActive);
                if (impactActive)
                {
                    SimulateParticles(previewImpactEffect, impactAge);
                }
            }
            if (previewImpactFeel != null)
            {
                var lifetime = Mathf.Max(0.05f, workingProfile.ImpactFeel?.Lifetime ?? 0.4f);
                previewImpactFeel.SetActive(
                    previewLastImpactHasFeedback && impactAge >= 0f && impactAge < lifetime);
            }

            if (elapsed >= playbackDuration)
            {
                StopPreviewPlayback();
            }
            Repaint();
        }

        private void StopPreviewPlayback()
        {
            previewPlaying = false;
            previewDeliveryActivated = false;
            previewActivationElapsed = -1f;
            ResetPreviewPlaybackObjects();
            SetPreviewUpdateSubscribed(false);
        }

        private void SetPreviewUpdateSubscribed(bool subscribed)
        {
            if (previewUpdateSubscribed == subscribed)
            {
                return;
            }

            previewUpdateSubscribed = subscribed;
            if (subscribed)
            {
                EditorApplication.update += TickPreviewPlayback;
            }
            else
            {
                EditorApplication.update -= TickPreviewPlayback;
            }
        }

        private void ResetPreviewPlaybackObjects()
        {
            if (previewAttacker != null)
            {
                previewAttacker.transform.localPosition = previewAttackerStart;
                previewAttacker.transform.localScale = new Vector3(0.55f, 0.45f, 0.55f);
            }
            if (previewTarget != null)
            {
                previewTarget.transform.localPosition = previewTargetStart;
                previewTarget.transform.localScale = previewTargetBaseScale;
            }
            foreach (var mover in previewAttackMovers)
            {
                if (mover == null)
                {
                    continue;
                }
                mover.transform.localPosition = previewAttackerStart;
                mover.SetActive(false);
            }
            if (previewImpactPulse != null)
            {
                previewImpactPulse.transform.localPosition = previewTargetStart;
                previewImpactPulse.transform.localScale = Vector3.one * 0.01f;
                previewImpactPulse.SetActive(false);
            }
            if (previewLaunchEffect != null)
            {
                previewLaunchEffect.SetActive(false);
            }
            if (previewImpactEffect != null)
            {
                previewImpactEffect.SetActive(false);
            }
            ResetFeelPreview(previewLaunchFeel);
            ResetFeelPreview(previewImpactFeel);
            previewNextImpactIndex = 0;
            previewLastImpactElapsed = -1f;
            previewLastImpactHasFeedback = false;
        }

        private GameObject CreateProjectilePreview(
            MonsterFeedbackCue feedback,
            BasicAttackFeelCue feel,
            Vector3 position)
        {
            var root = new GameObject("Attack Delivery")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            root.transform.SetParent(previewRoot.transform, false);
            root.transform.localPosition = position;
            if (feedback?.VfxPrefab != null)
            {
                CreateFeedbackChild(root.transform, feedback);
            }
            else
            {
                var marker = CreatePreviewPrimitive(
                    PrimitiveType.Sphere,
                    "Fallback Projectile",
                    position,
                    Vector3.one * 0.22f,
                    previewAttackMaterial);
                marker.transform.SetParent(root.transform, true);
            }
            if (feel?.Prefab != null)
            {
                CreateFeelChild(root.transform, feel);
            }
            return root;
        }

        private GameObject CreateStandardFeelTargetPreview(Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StandardFeelTargetPath);
            if (prefab == null)
            {
                previewTargetBaseScale = Vector3.one * 0.45f;
                return CreatePreviewPrimitive(
                    PrimitiveType.Sphere,
                    "Primary Target",
                    position,
                    previewTargetBaseScale,
                    previewTargetMaterial);
            }

            var root = new GameObject("Primary Target · FEEL Test Model")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            root.transform.SetParent(previewRoot.transform, false);
            root.transform.localPosition = position;
            var visual = Instantiate(prefab);
            SetPreviewHideFlags(visual);
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            foreach (var behaviour in visual.GetComponentsInChildren<Behaviour>(true))
            {
                behaviour.enabled = false;
            }

            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                previewTargetBaseScale = Vector3.one;
                return root;
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }
            var center = root.transform.InverseTransformPoint(bounds.center);
            var bottom = root.transform.InverseTransformPoint(
                new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            visual.transform.localPosition -= new Vector3(center.x, bottom.y, center.z);
            var largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            var fitScale = largest <= 0.0001f ? 1f : 1.1f / largest;
            previewTargetBaseScale = Vector3.one * fitScale;
            root.transform.localScale = previewTargetBaseScale;
            return root;
        }

        private GameObject CreateFeedbackPreview(
            MonsterFeedbackCue feedback,
            string objectName,
            Vector3 anchorPosition)
        {
            if (feedback?.VfxPrefab == null)
            {
                return null;
            }
            var root = new GameObject(objectName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            root.transform.SetParent(previewRoot.transform, false);
            root.transform.localPosition = anchorPosition;
            CreateFeedbackChild(root.transform, feedback);
            root.SetActive(false);
            return root;
        }

        private static void CreateFeedbackChild(Transform parent, MonsterFeedbackCue feedback)
        {
            var instance = Instantiate(feedback.VfxPrefab);
            instance.name = feedback.VfxPrefab.name + " [Preview]";
            SetPreviewHideFlags(instance);
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = feedback.LocalPosition;
            instance.transform.localRotation = feedback.LocalRotation;
            instance.transform.localScale = feedback.VfxPrefab.transform.localScale * feedback.Scale;
            instance.SetActive(true);
            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }
        }

        private GameObject CreateFeelPreview(
            BasicAttackFeelCue feel,
            string objectName,
            Vector3 anchorPosition)
        {
            if (feel?.Prefab == null)
            {
                return null;
            }

            var root = new GameObject(objectName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            root.transform.SetParent(previewRoot.transform, false);
            root.transform.localPosition = anchorPosition;
            CreateFeelChild(root.transform, feel);
            root.SetActive(false);
            return root;
        }

        private static void CreateFeelChild(Transform parent, BasicAttackFeelCue feel)
        {
            var instance = Instantiate(feel.Prefab);
            instance.name = feel.Prefab.name + " [FEEL Preview]";
            SetPreviewHideFlags(instance);
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = feel.LocalPosition;
            instance.transform.localRotation = feel.LocalRotation;
            instance.transform.localScale = feel.Prefab.transform.localScale * feel.Scale;
            instance.SetActive(true);
        }

        private static float ResolvePreviewFeelIntensity(MonsterImpactStrength strength)
        {
            return strength switch
            {
                MonsterImpactStrength.Light => 0.62f,
                MonsterImpactStrength.Heavy => 1.45f,
                _ => 1f
            };
        }

        private static void PlayFeelPreview(GameObject root, GameObject target, float intensity = 1f)
        {
            var runtime = root?.GetComponentsInChildren<MonoBehaviour>(true)
                .OfType<IBasicAttackFeelRuntime>()
                .FirstOrDefault();
            runtime?.PlayBasicAttackFeel(
                root.transform.position,
                target,
                intensity,
                BasicAttackFeelPlaybackOptions.None);
        }

        private static void ResetFeelPreview(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var runtime = root.GetComponentsInChildren<MonoBehaviour>(true)
                .OfType<IBasicAttackFeelRuntime>()
                .FirstOrDefault();
            runtime?.ResetBasicAttackFeel();
            root.SetActive(false);
        }

        private static void SetPreviewHideFlags(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private static void SimulateParticles(GameObject root, float time)
        {
            if (root == null)
            {
                return;
            }
            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Simulate(Mathf.Max(0f, time), true, true, false);
            }
        }

        private static void PlaySfxPreview(SfxCue cue)
        {
            if (cue == null || !cue.TrySelectClip(out var clip) || clip == null)
            {
                return;
            }
            var audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            var method = audioUtil?.GetMethods(
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)
                .FirstOrDefault(candidate => candidate.Name == "PlayPreviewClip");
            if (method == null)
            {
                return;
            }
            var parameters = method.GetParameters();
            var arguments = parameters.Length switch
            {
                1 => new object[] { clip },
                2 => new object[] { clip, 0 },
                _ => new object[] { clip, 0, false }
            };
            try
            {
                method.Invoke(null, arguments);
            }
            catch
            {
                // Unity 버전별 AudioUtil 시그니처 차이는 미리보기만 생략합니다.
            }
        }

        private static Material CreatePreviewMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return shader == null
                ? null
                : new Material(shader)
                {
                    color = color,
                    hideFlags = HideFlags.HideAndDontSave
                };
        }

        private void DisposePreview()
        {
            ClearPreviewContents();
            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }
        }

        private void ClearPreviewContents()
        {
            SetPreviewUpdateSubscribed(false);
            if (previewRoot != null)
            {
                DestroyImmediate(previewRoot);
            }
            previewAttacker = null;
            previewTarget = null;
            previewAttackMover = null;
            previewAttackMovers.Clear();
            previewImpactPulse = null;
            previewLaunchEffect = null;
            previewImpactEffect = null;
            previewLaunchFeel = null;
            previewImpactFeel = null;
            previewPlaying = false;
            previewDeliveryActivated = false;
            previewActivationElapsed = -1f;
            previewNextImpactIndex = 0;
            previewLastImpactElapsed = -1f;
            previewLastImpactHasFeedback = false;
            previewRoot = null;
            DestroyMaterial(ref previewGroundMaterial);
            DestroyMaterial(ref previewSourceMaterial);
            DestroyMaterial(ref previewTargetMaterial);
            DestroyMaterial(ref previewAttackMaterial);
        }

        private static void DestroyMaterial(ref Material material)
        {
            if (material != null)
            {
                DestroyImmediate(material);
                material = null;
            }
        }

        private static void EnsureCustomFolder()
        {
            if (!AssetDatabase.IsValidFolder(MonsterBasicAttackPresetUtility.CustomProfileRoot))
            {
                AssetDatabase.CreateFolder(MonsterBasicAttackPresetUtility.ProfileRoot, "Custom");
            }
        }

        private static string SanitizeToken(string value)
        {
            return new string((value ?? string.Empty).Trim().Where(character =>
                char.IsLetterOrDigit(character) || character == '_').ToArray());
        }

        private void SetError(string error)
        {
            message = error;
            messageType = MessageType.Error;
        }
    }
}
