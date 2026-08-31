using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Unit;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public readonly struct MonsterBasicAttackContractReconcileResult
    {
        public MonsterBasicAttackContractReconcileResult(int retained, int added, int archived)
        {
            Retained = retained;
            Added = added;
            Archived = archived;
        }

        public int Retained { get; }
        public int Added { get; }
        public int Archived { get; }
    }

    public static class MonsterBasicAttackVfxContractTemplates // 공격 방식별 도달 가능한 권장 연출 계약 원본
    {
        public static MonsterBasicAttackVfxSlot[] Build(MonsterBasicAttackProfile profile)
        {
            if (profile == null)
            {
                return Array.Empty<MonsterBasicAttackVfxSlot>();
            }

            var motion = MonsterBasicAttackVfxAssignmentScope.MotionSpecific;
            var shared = MonsterBasicAttackVfxAssignmentScope.MonsterShared;
            return profile.PresentationKind switch
            {
                MonsterBasicAttackPresentationKind.Sweep => new[]
                {
                    Vfx("sweep_plane", "휩쓸기 면", "전방 부채꼴을 읽히게 하는 공격 면", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.SourceRoot, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion),
                    Vfx("target_hit", "대상별 명중", "실제로 피해를 받은 각 대상의 명중 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared)
                },
                MonsterBasicAttackPresentationKind.Thrust => new[]
                {
                    Vfx("thrust_path", "찌르기 경로", "공격 원점에서 목표 방향으로 뻗는 직선 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.TrajectoryOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion),
                    Vfx("path_hit", "경로 명중", "직선 경로에서 피해를 받은 대상 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared)
                },
                MonsterBasicAttackPresentationKind.Slam => new[]
                {
                    Vfx("overhead_trail", "내려찍기 궤적", "선택된 모션의 내려찍기 궤적", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion, MonsterBasicAttackVfxAttachment.FollowAnchor, MonsterBasicAttackVfxEndPolicy.MotionEnd),
                    Vfx("ground_contact", "지면 접촉", "내려찍기가 닿은 중심점 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AreaCenter, MonsterBasicAttackVfxMultiplicity.OncePerExecution, shared),
                    Vfx("target_hit", "대상별 명중", "범위 안에서 실제 피해를 받은 각 대상의 명중 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared),
                    Vfx("area_wave", "범위 파동", "범위 판정이 해결된 뒤 펼쳐지는 원형 효과", MonsterBasicAttackVfxEvent.AreaResolved, MonsterBasicAttackVfxAnchor.AreaCenter, MonsterBasicAttackVfxMultiplicity.OncePerExecution, shared)
                },
                MonsterBasicAttackPresentationKind.Dash => new[]
                {
                    Vfx("dash_start", "돌진 시작", "공격 모션이 시작될 때의 예고 효과", MonsterBasicAttackVfxEvent.MotionStart, MonsterBasicAttackVfxAnchor.SourceRoot, MonsterBasicAttackVfxMultiplicity.OncePerMotion, motion),
                    Vfx("dash_trail", "돌진 잔상", "공격자를 따라가는 돌진 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.SourceRoot, MonsterBasicAttackVfxMultiplicity.ContinuousUntilEnd, motion, MonsterBasicAttackVfxAttachment.FollowAnchor, MonsterBasicAttackVfxEndPolicy.MotionEnd),
                    Vfx("hit", "실제 명중", "돌진 뒤 피해가 적용된 위치의 명중 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared)
                },
                MonsterBasicAttackPresentationKind.Combo => new[]
                {
                    Vfx("strike_trail", "연속 공격 궤적", "선택된 연속 공격 모션의 궤적", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion, MonsterBasicAttackVfxAttachment.FollowAnchor, MonsterBasicAttackVfxEndPolicy.MotionEnd),
                    Vfx("per_hit", "타격별 명중", "각 피해 단계의 명중 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerDamageStage, shared),
                    Vfx("final_hit", "마지막 타격", "마지막 피해 단계 뒤의 마무리 효과", MonsterBasicAttackVfxEvent.SequenceEnd, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.OncePerExecution, shared)
                },
                MonsterBasicAttackPresentationKind.Explosion => new[]
                {
                    Vfx("launch", "발사", "폭발 투사체가 생성되는 원점 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion),
                    Delivery("projectile", "폭발 투사체 본체"),
                    Vfx("contact", "대상별 폭발 명중", "폭발 범위에서 실제 피해를 받은 각 대상 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared),
                    Vfx("area_explosion", "범위 폭발", "범위 피해 해결 뒤 중심점 폭발 효과", MonsterBasicAttackVfxEvent.AreaResolved, MonsterBasicAttackVfxAnchor.AreaCenter, MonsterBasicAttackVfxMultiplicity.OncePerExecution, shared)
                },
                MonsterBasicAttackPresentationKind.Instant => new[]
                {
                    Vfx("cast", "즉발 시전", "Marker 순간 공격 원점의 시전 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion),
                    Vfx("hit", "실제 명중", "즉시 피해가 적용된 대상 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared)
                },
                MonsterBasicAttackPresentationKind.Scatter => new[]
                {
                    Vfx("multi_launch", "다중 발사", "부채꼴 탄막이 시작되는 원점 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion),
                    Delivery("projectile", "개별 투사체 본체"),
                    Vfx("hit", "개별 명중", "각 투사체가 피해를 적용한 대상 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared)
                },
                MonsterBasicAttackPresentationKind.Returning => new[]
                {
                    Vfx("launch", "왕복 발사", "왕복 투사체가 출발하는 원점 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion),
                    Delivery("projectile", "왕복 투사체 본체"),
                    Vfx("outbound_hit", "나가는 경로 명중", "전진 구간의 실제 명중 효과", MonsterBasicAttackVfxEvent.OutboundTargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared),
                    Vfx("turn", "회전 전환", "투사체가 복귀로 전환되는 지점 효과", MonsterBasicAttackVfxEvent.DeliveryTurn, MonsterBasicAttackVfxAnchor.ProjectileRoot, MonsterBasicAttackVfxMultiplicity.PerProjectile, shared),
                    Vfx("return_hit", "돌아오는 경로 명중", "복귀 구간의 실제 명중 효과", MonsterBasicAttackVfxEvent.ReturnTargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared)
                },
                MonsterBasicAttackPresentationKind.Breath => new[]
                {
                    Vfx("start", "브레스 시작", "브레스 모션 시작 예고 효과", MonsterBasicAttackVfxEvent.MotionStart, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerMotion, motion),
                    Vfx("body", "브레스 본체", "공격 원점을 따라 유지되는 브레스 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.ContinuousUntilEnd, motion, MonsterBasicAttackVfxAttachment.FollowAnchor, MonsterBasicAttackVfxEndPolicy.MotionEnd),
                    Vfx("repeated_hit", "반복 명중", "각 피해 단계에서 실제 명중한 위치 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerDamageStage, shared),
                    Vfx("end", "브레스 종료", "브레스 모션 종료 효과", MonsterBasicAttackVfxEvent.MotionEnd, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerMotion, motion)
                },
                MonsterBasicAttackPresentationKind.Beam => new[]
                {
                    Vfx("charge", "빔 충전", "빔 모션 시작의 충전 효과", MonsterBasicAttackVfxEvent.MotionStart, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerMotion, motion),
                    Vfx("beam_body", "빔 본체", "공격 원점에서 목표 방향으로 유지되는 빔 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.TrajectoryOrigin, MonsterBasicAttackVfxMultiplicity.ContinuousUntilEnd, motion, MonsterBasicAttackVfxAttachment.FollowAnchor, MonsterBasicAttackVfxEndPolicy.MotionEnd),
                    Vfx("contact_hit", "빔 접촉 명중", "직선 판정에서 피해를 받은 대상 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared),
                    Vfx("end", "빔 종료", "빔 모션 종료 효과", MonsterBasicAttackVfxEvent.MotionEnd, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerMotion, motion)
                },
                MonsterBasicAttackPresentationKind.Wave => new[]
                {
                    Vfx("start", "파동 시작", "이동 파동이 생성되는 원점 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion),
                    Delivery("wave_body", "이동 파동 본체"),
                    Vfx("path_hit", "경로 명중", "파동 경로에서 피해를 받은 대상 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared),
                    Vfx("disappear", "파동 소멸", "파동 이동체가 끝나는 위치 효과", MonsterBasicAttackVfxEvent.DeliveryEnd, MonsterBasicAttackVfxAnchor.ProjectileRoot, MonsterBasicAttackVfxMultiplicity.PerProjectile, shared)
                },
                MonsterBasicAttackPresentationKind.Shot when
                    profile.CollisionModule == MonsterBasicAttackCollisionModule.Pierce => new[]
                {
                    Vfx("launch", "발사", "관통 투사체가 생성되는 원점 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, motion),
                    Delivery("projectile", "관통 투사체 본체"),
                    Vfx("pierce_hit", "관통 명중", "경로 위에서 피해를 받은 대상별 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, shared),
                    Vfx("delivery_end", "비행 종료", "최대 거리 또는 수명으로 이동체가 끝나는 효과", MonsterBasicAttackVfxEvent.DeliveryEnd, MonsterBasicAttackVfxAnchor.ProjectileRoot, MonsterBasicAttackVfxMultiplicity.PerProjectile, shared)
                },
                MonsterBasicAttackPresentationKind.Shot => ProjectileSlots(),
                _ => ContactSlots()
            };
        }

        internal static List<BasicAttackWorkshopVfxSlot> Reconcile(
            MonsterBasicAttackProfile profile,
            IReadOnlyList<BasicAttackWorkshopVfxSlot> current,
            out MonsterBasicAttackContractReconcileResult result)
        {
            var source = current?.Where(slot => slot != null).ToList() ??
                         new List<BasicAttackWorkshopVfxSlot>();
            var templates = Build(profile);
            var reconciled = new List<BasicAttackWorkshopVfxSlot>();
            var consumed = new HashSet<BasicAttackWorkshopVfxSlot>();
            var retained = 0;
            var added = 0;

            foreach (var template in templates)
            {
                var existing = source.LastOrDefault(slot =>
                    string.Equals(slot.slotId, template.SlotId, StringComparison.OrdinalIgnoreCase));
                var item = BasicAttackWorkshopVfxSlot.From(template);
                if (existing != null)
                {
                    item.displayName = existing.displayName;
                    item.description = existing.description;
                    item.defaultLifetime = existing.defaultLifetime;
                    item.showAdvanced = existing.showAdvanced;
                    consumed.Add(existing);
                    retained++;
                }
                else
                {
                    added++;
                }
                reconciled.Add(item);
            }

            foreach (var custom in source)
            {
                if (consumed.Contains(custom) ||
                    templates.Any(template => string.Equals(
                        template.SlotId,
                        custom.slotId,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (MonsterBasicAttackVfxCompatibility.TryValidateSlot(
                        profile,
                        custom.Compile(),
                        out _))
                {
                    reconciled.Add(custom);
                    retained++;
                    consumed.Add(custom);
                }
            }

            result = new MonsterBasicAttackContractReconcileResult(
                retained,
                added,
                source.Count - consumed.Count);
            return reconciled;
        }

        private static MonsterBasicAttackVfxSlot[] ContactSlots()
        {
            return new[]
            {
                Vfx("swing_trail", "공격 궤적", "선택된 공격 모션의 휘두름 궤적", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, MonsterBasicAttackVfxAssignmentScope.MotionSpecific, MonsterBasicAttackVfxAttachment.FollowAnchor, MonsterBasicAttackVfxEndPolicy.MotionEnd),
                Vfx("hit", "실제 명중", "피해가 적용된 대상 위치의 명중 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, MonsterBasicAttackVfxAssignmentScope.MonsterShared)
            };
        }

        private static MonsterBasicAttackVfxSlot[] ProjectileSlots()
        {
            return new[]
            {
                Vfx("launch", "발사", "투사체가 생성되는 공격 원점 효과", MonsterBasicAttackVfxEvent.RecipeExecute, MonsterBasicAttackVfxAnchor.AttackOrigin, MonsterBasicAttackVfxMultiplicity.OncePerExecution, MonsterBasicAttackVfxAssignmentScope.MotionSpecific),
                Delivery("projectile", "투사체 본체"),
                Vfx("hit", "실제 명중", "피해가 적용된 실제 명중 위치 효과", MonsterBasicAttackVfxEvent.TargetDamaged, MonsterBasicAttackVfxAnchor.HitPoint, MonsterBasicAttackVfxMultiplicity.PerTargetHit, MonsterBasicAttackVfxAssignmentScope.MonsterShared)
            };
        }

        private static MonsterBasicAttackVfxSlot Delivery(string id, string label)
        {
            return Vfx(
                id,
                label,
                "배정한 Prefab이 실제 이동 판정체의 외형이 됩니다.",
                MonsterBasicAttackVfxEvent.DeliverySpawn,
                MonsterBasicAttackVfxAnchor.ProjectileRoot,
                MonsterBasicAttackVfxMultiplicity.PerProjectile,
                MonsterBasicAttackVfxAssignmentScope.MonsterShared,
                MonsterBasicAttackVfxAttachment.DeliveryVisual,
                MonsterBasicAttackVfxEndPolicy.DeliveryEnd,
                3f);
        }

        private static MonsterBasicAttackVfxSlot Vfx(
            string id,
            string label,
            string guide,
            MonsterBasicAttackVfxEvent eventType,
            MonsterBasicAttackVfxAnchor anchor,
            MonsterBasicAttackVfxMultiplicity multiplicity,
            MonsterBasicAttackVfxAssignmentScope scope,
            MonsterBasicAttackVfxAttachment attachment = MonsterBasicAttackVfxAttachment.World,
            MonsterBasicAttackVfxEndPolicy endPolicy = MonsterBasicAttackVfxEndPolicy.Timed,
            float lifetime = 1f)
        {
            var result = new MonsterBasicAttackVfxSlot();
            result.EditorConfigure(
                id,
                label,
                guide,
                eventType,
                anchor,
                multiplicity,
                scope,
                attachment,
                endPolicy,
                lifetime);
            return result;
        }
    }
}
