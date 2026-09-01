using System;

namespace ProjectMT.Shared.Unit
{
    public static class MonsterBasicAttackVfxCompatibility // 공격 모듈이 실제 발생시킬 수 있는 연출 계약만 허용
    {
        public static bool SupportsEvent(
            MonsterBasicAttackProfile profile,
            MonsterBasicAttackVfxEvent eventType)
        {
            if (profile == null)
            {
                return false;
            }

            return eventType switch
            {
                MonsterBasicAttackVfxEvent.MotionStart => true,
                MonsterBasicAttackVfxEvent.RecipeExecute => true,
                MonsterBasicAttackVfxEvent.Telegraph => profile.TelegraphDelay > 0f,
                MonsterBasicAttackVfxEvent.TargetDamaged => true,
                MonsterBasicAttackVfxEvent.MotionEnd => true,
                MonsterBasicAttackVfxEvent.DashExit or MonsterBasicAttackVfxEvent.DashEnter =>
                    profile.MovementModule == MonsterBasicAttackMovementModule.Dash,
                MonsterBasicAttackVfxEvent.DeliverySpawn => profile.UsesProjectileVisual,
                MonsterBasicAttackVfxEvent.DeliveryEnd => profile.UsesProjectileVisual,
                MonsterBasicAttackVfxEvent.AreaResolved =>
                    profile.Shape == MonsterBasicAttackShape.Circle ||
                    profile.CollisionModule == MonsterBasicAttackCollisionModule.AreaImpact,
                MonsterBasicAttackVfxEvent.SequenceEnd =>
                    profile.DeliveryModule == MonsterBasicAttackDeliveryModule.Direct,
                MonsterBasicAttackVfxEvent.OutboundTargetDamaged =>
                    profile.SequenceModule == MonsterBasicAttackSequenceModule.ReturnPasses,
                MonsterBasicAttackVfxEvent.ReturnTargetDamaged =>
                    profile.SequenceModule == MonsterBasicAttackSequenceModule.ReturnPasses,
                MonsterBasicAttackVfxEvent.DeliveryTurn =>
                    profile.SequenceModule == MonsterBasicAttackSequenceModule.ReturnPasses,
                _ => false
            };
        }

        public static bool TryValidateSlot(
            MonsterBasicAttackProfile profile,
            MonsterBasicAttackVfxSlot slot,
            out string error)
        {
            if (profile == null || slot == null)
            {
                error = "Basic Attack profile or VFX slot is missing.";
                return false;
            }

            if (!SupportsEvent(profile, slot.EventType))
            {
                error =
                    $"VFX event cannot occur in this attack. Attack={profile.AttackId}, Slot={slot.SlotId}, Event={slot.EventType}";
                return false;
            }

            if ((slot.IsDeliveryVisual ||
                 slot.Anchor == MonsterBasicAttackVfxAnchor.ProjectileRoot ||
                 slot.Multiplicity == MonsterBasicAttackVfxMultiplicity.PerProjectile ||
                 slot.EndPolicy == MonsterBasicAttackVfxEndPolicy.DeliveryEnd) &&
                !profile.UsesProjectileVisual)
            {
                error =
                    $"Moving-delivery VFX contract requires a Projectile or Traveling Area. Attack={profile.AttackId}, Slot={slot.SlotId}";
                return false;
            }

            if (!SupportsAnchor(profile, slot.EventType, slot.Anchor))
            {
                error =
                    $"VFX anchor is not available at this attack event. Attack={profile.AttackId}, Slot={slot.SlotId}, Anchor={slot.Anchor}";
                return false;
            }

            if (!SupportsMultiplicity(profile, slot.EventType, slot.Multiplicity))
            {
                error =
                    $"VFX multiplicity is not available at this attack event. Attack={profile.AttackId}, Slot={slot.SlotId}, Multiplicity={slot.Multiplicity}";
                return false;
            }

            if (!SupportsAttachment(profile, slot.EventType, slot.Anchor, slot.Attachment))
            {
                error =
                    $"VFX attachment cannot be honored by this anchor. Attack={profile.AttackId}, Slot={slot.SlotId}, Attachment={slot.Attachment}";
                return false;
            }

            if (!SupportsEndPolicy(profile, slot.EventType, slot.EndPolicy))
            {
                error =
                    $"VFX end policy cannot be honored at this event. Attack={profile.AttackId}, Slot={slot.SlotId}, End={slot.EndPolicy}";
                return false;
            }

            if (slot.Multiplicity == MonsterBasicAttackVfxMultiplicity.PerDamageStage &&
                profile.HitCount < 2)
            {
                error =
                    $"Per-damage-stage VFX requires two or more damage stages. Attack={profile.AttackId}, Slot={slot.SlotId}";
                return false;
            }

            if ((slot.EventType is MonsterBasicAttackVfxEvent.OutboundTargetDamaged or
                 MonsterBasicAttackVfxEvent.ReturnTargetDamaged or
                 MonsterBasicAttackVfxEvent.DeliveryTurn) &&
                profile.SequenceModule != MonsterBasicAttackSequenceModule.ReturnPasses)
            {
                error =
                    $"Return-pass VFX contract requires a returning attack. Attack={profile.AttackId}, Slot={slot.SlotId}";
                return false;
            }

            error = null;
            return true;
        }

        public static bool SupportsAnchor(
            MonsterBasicAttackProfile profile,
            MonsterBasicAttackVfxEvent eventType,
            MonsterBasicAttackVfxAnchor anchor)
        {
            if (profile == null) return false;
            var targetEvent = eventType is MonsterBasicAttackVfxEvent.TargetDamaged or
                MonsterBasicAttackVfxEvent.OutboundTargetDamaged or
                MonsterBasicAttackVfxEvent.ReturnTargetDamaged;
            return anchor switch
            {
                MonsterBasicAttackVfxAnchor.SourceRoot => true,
                MonsterBasicAttackVfxAnchor.AttackOrigin => true,
                MonsterBasicAttackVfxAnchor.MarkerSocket =>
                    eventType is MonsterBasicAttackVfxEvent.MotionStart or
                        MonsterBasicAttackVfxEvent.RecipeExecute or
                        MonsterBasicAttackVfxEvent.MotionEnd,
                MonsterBasicAttackVfxAnchor.ProjectileRoot =>
                    profile.UsesProjectileVisual &&
                    (targetEvent || eventType is MonsterBasicAttackVfxEvent.DeliverySpawn or
                        MonsterBasicAttackVfxEvent.AreaResolved or
                        MonsterBasicAttackVfxEvent.DeliveryTurn or
                        MonsterBasicAttackVfxEvent.DeliveryEnd),
                MonsterBasicAttackVfxAnchor.TargetRoot =>
                    targetEvent || eventType is MonsterBasicAttackVfxEvent.SequenceEnd or
                        MonsterBasicAttackVfxEvent.Telegraph,
                MonsterBasicAttackVfxAnchor.HitPoint =>
                    targetEvent || eventType == MonsterBasicAttackVfxEvent.SequenceEnd,
                MonsterBasicAttackVfxAnchor.AreaCenter =>
                    eventType is MonsterBasicAttackVfxEvent.RecipeExecute or
                        MonsterBasicAttackVfxEvent.AreaResolved or
                        MonsterBasicAttackVfxEvent.Telegraph,
                MonsterBasicAttackVfxAnchor.TrajectoryOrigin =>
                    eventType is MonsterBasicAttackVfxEvent.MotionStart or
                        MonsterBasicAttackVfxEvent.RecipeExecute or
                        MonsterBasicAttackVfxEvent.MotionEnd,
                _ => false
            };
        }

        public static bool SupportsMultiplicity(
            MonsterBasicAttackProfile profile,
            MonsterBasicAttackVfxEvent eventType,
            MonsterBasicAttackVfxMultiplicity multiplicity)
        {
            if (profile == null) return false;
            var targetEvent = eventType is MonsterBasicAttackVfxEvent.TargetDamaged or
                MonsterBasicAttackVfxEvent.OutboundTargetDamaged or
                MonsterBasicAttackVfxEvent.ReturnTargetDamaged;
            return multiplicity switch
            {
                MonsterBasicAttackVfxMultiplicity.OncePerMotion =>
                    eventType is MonsterBasicAttackVfxEvent.MotionStart or
                        MonsterBasicAttackVfxEvent.MotionEnd,
                MonsterBasicAttackVfxMultiplicity.OncePerExecution => true,
                MonsterBasicAttackVfxMultiplicity.PerProjectile =>
                    profile.UsesProjectileVisual &&
                    (targetEvent || eventType is MonsterBasicAttackVfxEvent.DeliverySpawn or
                        MonsterBasicAttackVfxEvent.AreaResolved or
                        MonsterBasicAttackVfxEvent.DeliveryTurn or
                        MonsterBasicAttackVfxEvent.DeliveryEnd),
                MonsterBasicAttackVfxMultiplicity.PerTargetHit => targetEvent,
                MonsterBasicAttackVfxMultiplicity.PerDamageStage =>
                    targetEvent && profile.HitCount >= 2,
                MonsterBasicAttackVfxMultiplicity.ContinuousUntilEnd =>
                    eventType is MonsterBasicAttackVfxEvent.MotionStart or
                        MonsterBasicAttackVfxEvent.RecipeExecute or
                        MonsterBasicAttackVfxEvent.DeliverySpawn,
                _ => false
            };
        }

        public static bool SupportsAttachment(
            MonsterBasicAttackProfile profile,
            MonsterBasicAttackVfxEvent eventType,
            MonsterBasicAttackVfxAnchor anchor,
            MonsterBasicAttackVfxAttachment attachment)
        {
            if (profile == null) return false;
            return attachment switch
            {
                MonsterBasicAttackVfxAttachment.World => true,
                MonsterBasicAttackVfxAttachment.FollowAnchor =>
                    anchor is MonsterBasicAttackVfxAnchor.SourceRoot or
                        MonsterBasicAttackVfxAnchor.AttackOrigin or
                        MonsterBasicAttackVfxAnchor.MarkerSocket or
                        MonsterBasicAttackVfxAnchor.ProjectileRoot or
                        MonsterBasicAttackVfxAnchor.TargetRoot or
                        MonsterBasicAttackVfxAnchor.TrajectoryOrigin,
                MonsterBasicAttackVfxAttachment.DeliveryVisual =>
                    profile.UsesProjectileVisual &&
                    eventType == MonsterBasicAttackVfxEvent.DeliverySpawn &&
                    anchor == MonsterBasicAttackVfxAnchor.ProjectileRoot,
                _ => false
            };
        }

        public static bool SupportsEndPolicy(
            MonsterBasicAttackProfile profile,
            MonsterBasicAttackVfxEvent eventType,
            MonsterBasicAttackVfxEndPolicy endPolicy)
        {
            if (profile == null) return false;
            var deliveryContext = eventType is MonsterBasicAttackVfxEvent.DeliverySpawn or
                MonsterBasicAttackVfxEvent.TargetDamaged or
                MonsterBasicAttackVfxEvent.OutboundTargetDamaged or
                MonsterBasicAttackVfxEvent.ReturnTargetDamaged or
                MonsterBasicAttackVfxEvent.AreaResolved or
                MonsterBasicAttackVfxEvent.DeliveryTurn;
            return endPolicy switch
            {
                MonsterBasicAttackVfxEndPolicy.Timed => true,
                MonsterBasicAttackVfxEndPolicy.ParticleDuration => true,
                MonsterBasicAttackVfxEndPolicy.MotionEnd => true,
                MonsterBasicAttackVfxEndPolicy.DeliveryEnd =>
                    profile.UsesProjectileVisual && deliveryContext,
                _ => false
            };
        }
    }
}
