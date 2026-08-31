using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Unit;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public readonly struct MonsterActiveAttackContractReconcileResult
    {
        public MonsterActiveAttackContractReconcileResult(int retained, int added, int archived)
        {
            Retained = retained;
            Added = added;
            Archived = archived;
        }

        public int Retained { get; }
        public int Added { get; }
        public int Archived { get; }
    }

    public static class MonsterActiveAttackVfxContractTemplates // 공격 형태별 실제 발생 이벤트 계약
    {
        public static MonsterActivePresentationSlot[] Build(MonsterActiveAttackStep step)
        {
            if (step == null) return Array.Empty<MonsterActivePresentationSlot>();

            var result = new List<MonsterActivePresentationSlot>();
            if (step.TeleportBeforeAttack)
            {
                result.Add(Slot(
                    "teleport_exit",
                    "순간이동 출발",
                    "시전자가 원래 위치에서 사라지는 순간",
                    MonsterActivePresentationEvent.TeleportExit,
                    MonsterActivePresentationAnchor.CasterRoot));
                result.Add(Slot(
                    "teleport_enter",
                    "순간이동 도착",
                    "타깃 앞으로 이동한 직후",
                    MonsterActivePresentationEvent.TeleportEnter,
                    MonsterActivePresentationAnchor.CasterRoot));
            }

            switch (step.Pattern)
            {
                case MonsterActiveAttackPattern.Line:
                    result.Add(Telegraph(MonsterActivePresentationAnchor.TargetPoint));
                    result.Add(Slot(
                        "line_path",
                        "일자 공격 경로",
                        "공격 원점에서 전방 직선 판정을 보여 주는 효과",
                        MonsterActivePresentationEvent.Launch,
                        MonsterActivePresentationAnchor.TrajectoryOrigin));
                    result.Add(TargetHit());
                    break;
                case MonsterActiveAttackPattern.Cone:
                    result.Add(Telegraph(MonsterActivePresentationAnchor.AreaCenter));
                    result.Add(Slot(
                        "cone_sweep",
                        "부채꼴 공격 면",
                        "전방 부채꼴 판정 방향을 보여 주는 효과",
                        MonsterActivePresentationEvent.Launch,
                        MonsterActivePresentationAnchor.TrajectoryOrigin));
                    result.Add(TargetHit());
                    result.Add(AreaResolved("cone_finish", "부채꼴 판정 완료"));
                    break;
                case MonsterActiveAttackPattern.SelfCircle:
                    result.Add(Telegraph(MonsterActivePresentationAnchor.AreaCenter));
                    result.Add(Slot(
                        "self_cast",
                        "자기 중심 발동",
                        "시전자 중심에서 원형 공격이 시작되는 효과",
                        MonsterActivePresentationEvent.Launch,
                        MonsterActivePresentationAnchor.CasterRoot));
                    result.Add(TargetHit());
                    result.Add(AreaResolved("area_wave", "원형 범위 파동"));
                    break;
                case MonsterActiveAttackPattern.FrontCircle:
                    result.Add(Telegraph(MonsterActivePresentationAnchor.AreaCenter));
                    result.Add(Slot(
                        "front_cast",
                        "전방 원형 발동",
                        "공격 원점에서 전방 원형 지점을 향한 발동 효과",
                        MonsterActivePresentationEvent.Launch,
                        MonsterActivePresentationAnchor.AttackOrigin));
                    result.Add(TargetHit());
                    result.Add(AreaResolved("area_wave", "전방 범위 파동"));
                    break;
                case MonsterActiveAttackPattern.PiercingProjectile:
                    AddProjectileBase(result, false);
                    result.Add(Slot(
                        "pierce_hit",
                        "관통 명중",
                        "각 투사체가 실제로 피해를 준 대상 위치",
                        MonsterActivePresentationEvent.Impact,
                        MonsterActivePresentationAnchor.HitPoint,
                        MonsterActivePresentationMultiplicity.PerTargetHit));
                    result.Add(DeliveryEnd());
                    break;
                case MonsterActiveAttackPattern.ExplosiveProjectile:
                    AddProjectileBase(result, true);
                    result.Add(TargetHit());
                    result.Add(AreaResolved(
                        "area_explosion",
                        "범위 폭발",
                        MonsterActivePresentationMultiplicity.OncePerProjectile));
                    result.Add(DeliveryEnd());
                    break;
                case MonsterActiveAttackPattern.PiercingBeam:
                    result.Add(Slot(
                        "beam_charge",
                        "빔 충전",
                        "빔 공격 모션이 시작될 때 공격 원점에 재생",
                        MonsterActivePresentationEvent.MotionStart,
                        MonsterActivePresentationAnchor.AttackOrigin));
                    result.Add(Slot(
                        "beam_body",
                        "관통 빔 본체",
                        "Step이 끝날 때까지 공격 원점을 따라가는 빔 효과",
                        MonsterActivePresentationEvent.Travel,
                        MonsterActivePresentationAnchor.TrajectoryOrigin,
                        MonsterActivePresentationMultiplicity.ContinuousUntilEnd,
                        MonsterActivePresentationAttachment.FollowAnchor,
                        MonsterActivePresentationEndPolicy.StepEnd));
                    result.Add(TargetHit());
                    result.Add(Slot(
                        "beam_end",
                        "빔 종료",
                        "빔 Step 판정이 모두 끝난 순간",
                        MonsterActivePresentationEvent.StepEnd,
                        MonsterActivePresentationAnchor.AttackOrigin));
                    break;
                case MonsterActiveAttackPattern.InstantMagic:
                    var area = step.InstantMagicTarget == MonsterActiveInstantMagicTarget.TargetArea;
                    result.Add(Telegraph(area
                        ? MonsterActivePresentationAnchor.AreaCenter
                        : MonsterActivePresentationAnchor.TargetPoint));
                    result.Add(Slot(
                        "magic_cast",
                        "즉발 마법 시전",
                        step.MagicDirection == MonsterActiveMagicDirection.GroundUp
                            ? "바닥에서 위로 나타나는 마법 시전"
                            : "위에서 아래로 떨어지는 마법 시전",
                        MonsterActivePresentationEvent.Launch,
                        MonsterActivePresentationAnchor.AttackOrigin));
                    result.Add(TargetHit());
                    if (area) result.Add(AreaResolved("magic_area", "마법 범위 완료"));
                    break;
            }

            return result.ToArray();
        }

        public static List<MonsterActivePresentationSlot> Reconcile(
            MonsterActiveAttackStep step,
            IReadOnlyList<MonsterActivePresentationSlot> current,
            out MonsterActiveAttackContractReconcileResult result)
        {
            var source = current?.Where(slot => slot != null).ToList() ??
                         new List<MonsterActivePresentationSlot>();
            var templates = Build(step);
            var reconciled = new List<MonsterActivePresentationSlot>();
            var consumed = new HashSet<MonsterActivePresentationSlot>();
            var retained = 0;
            var added = 0;

            foreach (var template in templates)
            {
                var existing = source.LastOrDefault(slot =>
                    string.Equals(slot.SlotId, template.SlotId, StringComparison.OrdinalIgnoreCase));
                existing ??= source.LastOrDefault(slot =>
                    !consumed.Contains(slot) &&
                    slot.Timing == template.Timing &&
                    slot.Anchor == template.Anchor);
                existing ??= source.LastOrDefault(slot =>
                    !consumed.Contains(slot) &&
                    slot.Timing == template.Timing &&
                    MonsterActiveAttackVfxCompatibility.TryValidateSlot(step, slot, out _));
                if (existing != null)
                {
                    consumed.Add(existing);
                    retained++;
                    reconciled.Add(existing);
                }
                else
                {
                    added++;
                    reconciled.Add(template);
                }
            }

            foreach (var custom in source)
            {
                if (consumed.Contains(custom) ||
                    templates.Any(template => string.Equals(
                        template.SlotId,
                        custom.SlotId,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                if (MonsterActiveAttackVfxCompatibility.TryValidateSlot(step, custom, out _))
                {
                    reconciled.Add(custom);
                    consumed.Add(custom);
                    retained++;
                }
            }

            result = new MonsterActiveAttackContractReconcileResult(
                retained,
                added,
                source.Count - consumed.Count);
            return reconciled;
        }

        private static void AddProjectileBase(
            List<MonsterActivePresentationSlot> result,
            bool explosive)
        {
            result.Add(Telegraph(MonsterActivePresentationAnchor.TargetPoint));
            result.Add(Slot(
                "launch",
                explosive ? "폭발 투사체 발사" : "관통 투사체 발사",
                "공격 원점에서 투사체가 생성되는 순간",
                MonsterActivePresentationEvent.Launch,
                MonsterActivePresentationAnchor.AttackOrigin));
            result.Add(Slot(
                "projectile",
                explosive ? "폭발 투사체 본체" : "관통 투사체 본체",
                "배정한 Prefab이 실제 이동체 외형으로 사용됩니다.",
                MonsterActivePresentationEvent.DeliverySpawn,
                MonsterActivePresentationAnchor.ProjectileRoot,
                MonsterActivePresentationMultiplicity.OncePerProjectile,
                MonsterActivePresentationAttachment.DeliveryVisual,
                MonsterActivePresentationEndPolicy.DeliveryEnd));
        }

        private static MonsterActivePresentationSlot Telegraph(
            MonsterActivePresentationAnchor anchor)
        {
            return Slot(
                "telegraph",
                "판정 예고",
                "실제 피해가 적용되기 전에 공격 범위를 알리는 효과",
                MonsterActivePresentationEvent.Telegraph,
                anchor);
        }

        private static MonsterActivePresentationSlot TargetHit()
        {
            return Slot(
                "target_hit",
                "대상별 실제 명중",
                "피해가 실제 적용된 각 대상 위치",
                MonsterActivePresentationEvent.Impact,
                MonsterActivePresentationAnchor.HitPoint,
                MonsterActivePresentationMultiplicity.PerTargetHit);
        }

        private static MonsterActivePresentationSlot AreaResolved(
            string id,
            string label,
            MonsterActivePresentationMultiplicity multiplicity =
                MonsterActivePresentationMultiplicity.OncePerStep)
        {
            return Slot(
                id,
                label,
                "범위 안의 대상 판정이 완료된 중심 위치",
                MonsterActivePresentationEvent.AreaResolved,
                MonsterActivePresentationAnchor.AreaCenter,
                multiplicity);
        }

        private static MonsterActivePresentationSlot DeliveryEnd()
        {
            return Slot(
                "delivery_end",
                "이동체 종료",
                "최대 거리 또는 충돌로 이동체가 끝나는 위치",
                MonsterActivePresentationEvent.DeliveryEnd,
                MonsterActivePresentationAnchor.ProjectileRoot,
                MonsterActivePresentationMultiplicity.OncePerProjectile);
        }

        private static MonsterActivePresentationSlot Slot(
            string id,
            string label,
            string description,
            MonsterActivePresentationEvent timing,
            MonsterActivePresentationAnchor anchor,
            MonsterActivePresentationMultiplicity multiplicity =
                MonsterActivePresentationMultiplicity.OncePerStep,
            MonsterActivePresentationAttachment attachment =
                MonsterActivePresentationAttachment.World,
            MonsterActivePresentationEndPolicy endPolicy =
                MonsterActivePresentationEndPolicy.Timed)
        {
            var slot = new MonsterActivePresentationSlot();
            slot.EditorConfigure(
                id,
                label,
                timing,
                anchor,
                description,
                false,
                1f,
                multiplicity,
                attachment,
                endPolicy);
            return slot;
        }
    }
}
