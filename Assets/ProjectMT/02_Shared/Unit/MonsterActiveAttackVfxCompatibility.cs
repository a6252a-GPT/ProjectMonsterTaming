using System;

namespace ProjectMT.Shared.Unit
{
    public static class MonsterActiveAttackVfxCompatibility // 공격 판정과 연출 계약의 도달 가능성
    {
        public static bool TryValidateSlot(
            MonsterActiveAttackStep step,
            MonsterActivePresentationSlot slot,
            out string error)
        {
            if (step == null || slot == null)
            {
                error = "액티브 Step 또는 VFX/SFX 공간이 비어 있습니다.";
                return false;
            }
            if (!SupportsEvent(step, slot.Timing))
            {
                error =
                    $"공격 형태에서 발생하지 않는 연출 시점입니다. Step={step.StepId}, Event={slot.Timing}";
                return false;
            }
            if (!SupportsAnchor(step, slot.Timing, slot.Anchor))
            {
                error =
                    $"연출 시점에서 사용할 수 없는 기준 위치입니다. Step={step.StepId}, Event={slot.Timing}, Anchor={slot.Anchor}";
                return false;
            }
            if (!SupportsMultiplicity(step, slot.Timing, slot.Multiplicity))
            {
                error =
                    $"연출 시점에서 사용할 수 없는 재생 횟수입니다. Step={step.StepId}, Event={slot.Timing}, Multiplicity={slot.Multiplicity}";
                return false;
            }
            if (!SupportsAttachment(step, slot.Timing, slot.Anchor, slot.Multiplicity, slot.Attachment))
            {
                error =
                    $"연출 시점·기준 위치에서 사용할 수 없는 부착 방식입니다. Step={step.StepId}, Attachment={slot.Attachment}";
                return false;
            }
            if (!SupportsEndPolicy(step, slot.Timing, slot.EndPolicy))
            {
                error =
                    $"연출 시점에서 사용할 수 없는 정리 정책입니다. Step={step.StepId}, End={slot.EndPolicy}";
                return false;
            }
            if (slot.UseDuration && slot.EndPolicy != MonsterActivePresentationEndPolicy.Timed)
            {
                error =
                    $"지속시간 직접 입력은 시간 종료 계약에서만 사용할 수 있습니다. Step={step.StepId}, Slot={slot.SlotId}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool SupportsEvent(
            MonsterActiveAttackStep step,
            MonsterActivePresentationEvent timing)
        {
            return timing switch
            {
                MonsterActivePresentationEvent.Telegraph => true,
                MonsterActivePresentationEvent.Launch => true,
                MonsterActivePresentationEvent.Impact => true,
                MonsterActivePresentationEvent.MotionStart => true,
                MonsterActivePresentationEvent.StepEnd => true,
                MonsterActivePresentationEvent.DashExit => step.DashBeforeAttack,
                MonsterActivePresentationEvent.DashEnter => step.DashBeforeAttack,
                MonsterActivePresentationEvent.Travel => UsesTravel(step.Pattern),
                MonsterActivePresentationEvent.DeliverySpawn => UsesProjectileDelivery(step.Pattern),
                MonsterActivePresentationEvent.DeliveryEnd => UsesProjectileDelivery(step.Pattern),
                MonsterActivePresentationEvent.AreaResolved => UsesAreaResolution(step),
                _ => false
            };
        }

        public static bool SupportsAnchor(
            MonsterActiveAttackStep step,
            MonsterActivePresentationEvent timing,
            MonsterActivePresentationAnchor anchor)
        {
            return anchor switch
            {
                MonsterActivePresentationAnchor.CasterRoot => true,
                MonsterActivePresentationAnchor.AttackOrigin => true,
                MonsterActivePresentationAnchor.MarkerSocket =>
                    timing is MonsterActivePresentationEvent.MotionStart or
                        MonsterActivePresentationEvent.Launch,
                MonsterActivePresentationAnchor.ProjectileRoot =>
                    UsesProjectileDelivery(step.Pattern) &&
                    timing is MonsterActivePresentationEvent.DeliverySpawn or
                        MonsterActivePresentationEvent.DeliveryEnd,
                MonsterActivePresentationAnchor.TargetRoot =>
                    timing is MonsterActivePresentationEvent.Telegraph or
                        MonsterActivePresentationEvent.Impact,
                MonsterActivePresentationAnchor.HitPoint =>
                    timing == MonsterActivePresentationEvent.Impact,
                MonsterActivePresentationAnchor.AreaCenter =>
                    UsesAreaResolution(step) &&
                    timing is MonsterActivePresentationEvent.Telegraph or
                        MonsterActivePresentationEvent.Impact or
                        MonsterActivePresentationEvent.AreaResolved,
                MonsterActivePresentationAnchor.TrajectoryOrigin =>
                    UsesTrajectory(step.Pattern) &&
                    timing is MonsterActivePresentationEvent.Launch or
                        MonsterActivePresentationEvent.Travel or
                        MonsterActivePresentationEvent.StepEnd,
                MonsterActivePresentationAnchor.TargetPoint =>
                    timing is MonsterActivePresentationEvent.Telegraph or
                        MonsterActivePresentationEvent.Impact or
                        MonsterActivePresentationEvent.AreaResolved,
                _ => false
            };
        }

        public static bool SupportsMultiplicity(
            MonsterActiveAttackStep step,
            MonsterActivePresentationEvent timing,
            MonsterActivePresentationMultiplicity multiplicity)
        {
            return multiplicity switch
            {
                MonsterActivePresentationMultiplicity.OncePerStep => true,
                MonsterActivePresentationMultiplicity.OncePerProjectile =>
                    UsesProjectileDelivery(step.Pattern) &&
                    (timing is MonsterActivePresentationEvent.Launch or
                         MonsterActivePresentationEvent.DeliverySpawn or
                         MonsterActivePresentationEvent.DeliveryEnd ||
                     step.Pattern == MonsterActiveAttackPattern.ExplosiveProjectile &&
                     timing == MonsterActivePresentationEvent.AreaResolved),
                MonsterActivePresentationMultiplicity.PerTargetHit =>
                    timing == MonsterActivePresentationEvent.Impact,
                MonsterActivePresentationMultiplicity.PerDamageStage => false,
                MonsterActivePresentationMultiplicity.ContinuousUntilEnd =>
                    timing is MonsterActivePresentationEvent.MotionStart or
                        MonsterActivePresentationEvent.Launch or
                        MonsterActivePresentationEvent.Travel or
                        MonsterActivePresentationEvent.DeliverySpawn,
                _ => false
            };
        }

        public static bool SupportsAttachment(
            MonsterActiveAttackStep step,
            MonsterActivePresentationEvent timing,
            MonsterActivePresentationAnchor anchor,
            MonsterActivePresentationMultiplicity multiplicity,
            MonsterActivePresentationAttachment attachment)
        {
            if (step == null) return false;
            return attachment switch
            {
                MonsterActivePresentationAttachment.World => true,
                MonsterActivePresentationAttachment.FollowAnchor =>
                    anchor is MonsterActivePresentationAnchor.CasterRoot or
                        MonsterActivePresentationAnchor.AttackOrigin or
                        MonsterActivePresentationAnchor.MarkerSocket or
                        MonsterActivePresentationAnchor.TargetPoint or
                        MonsterActivePresentationAnchor.TargetRoot or
                        MonsterActivePresentationAnchor.HitPoint or
                        MonsterActivePresentationAnchor.TrajectoryOrigin,
                MonsterActivePresentationAttachment.DeliveryVisual =>
                    UsesProjectileDelivery(step.Pattern) &&
                    timing == MonsterActivePresentationEvent.DeliverySpawn &&
                    anchor == MonsterActivePresentationAnchor.ProjectileRoot &&
                    multiplicity == MonsterActivePresentationMultiplicity.OncePerProjectile,
                _ => false
            };
        }

        public static bool SupportsEndPolicy(
            MonsterActiveAttackStep step,
            MonsterActivePresentationEvent timing,
            MonsterActivePresentationEndPolicy endPolicy)
        {
            if (step == null) return false;
            return endPolicy switch
            {
                MonsterActivePresentationEndPolicy.Timed => true,
                MonsterActivePresentationEndPolicy.ParticleDuration => true,
                MonsterActivePresentationEndPolicy.DeliveryEnd =>
                    UsesProjectileDelivery(step.Pattern) &&
                    timing is MonsterActivePresentationEvent.DeliverySpawn or
                        MonsterActivePresentationEvent.Travel or
                        MonsterActivePresentationEvent.Impact,
                MonsterActivePresentationEndPolicy.StepEnd =>
                    timing != MonsterActivePresentationEvent.StepEnd,
                // Executor에는 클립 종료 콜백이 없으므로 MotionEnd를 허용하면 실제로는 StepEnd처럼 동작한다.
                MonsterActivePresentationEndPolicy.MotionEnd => false,
                _ => false
            };
        }

        public static bool UsesProjectileDelivery(MonsterActiveAttackPattern pattern)
        {
            return pattern is MonsterActiveAttackPattern.PiercingProjectile or
                MonsterActiveAttackPattern.ExplosiveProjectile;
        }

        public static bool UsesTravel(MonsterActiveAttackPattern pattern)
        {
            return UsesProjectileDelivery(pattern) || pattern == MonsterActiveAttackPattern.PiercingBeam;
        }

        public static bool UsesDelivery(MonsterActiveAttackPattern pattern)
        {
            return UsesProjectileDelivery(pattern);
        }

        public static bool UsesTrajectory(MonsterActiveAttackPattern pattern)
        {
            return pattern is MonsterActiveAttackPattern.Line or
                MonsterActiveAttackPattern.Cone or
                MonsterActiveAttackPattern.PiercingProjectile or
                MonsterActiveAttackPattern.ExplosiveProjectile or
                MonsterActiveAttackPattern.PiercingBeam;
        }

        public static bool UsesAreaResolution(MonsterActiveAttackStep step)
        {
            if (step == null) return false;
            return step.Pattern is MonsterActiveAttackPattern.Cone or
                       MonsterActiveAttackPattern.SelfCircle or
                       MonsterActiveAttackPattern.FrontCircle or
                       MonsterActiveAttackPattern.ExplosiveProjectile ||
                   step.Pattern == MonsterActiveAttackPattern.InstantMagic &&
                   step.InstantMagicTarget == MonsterActiveInstantMagicTarget.TargetArea;
        }
    }
}
