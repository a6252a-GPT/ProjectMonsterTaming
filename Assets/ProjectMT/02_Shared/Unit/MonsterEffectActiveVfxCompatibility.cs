namespace ProjectMT.Shared.Unit
{
    public static class MonsterEffectActiveVfxCompatibility // 효과 실행기가 실제 재생할 수 있는 연출 계약
    {
        public static bool TryValidateSlot(
            MonsterActivePresentationSlot slot,
            out string error)
        {
            if (slot == null)
            {
                error = "효과형 VFX/SFX 공간이 비어 있습니다.";
                return false;
            }
            if (!SupportsEvent(slot.Timing))
            {
                error = $"효과 실행기에서 발생하지 않는 연출 시점입니다. Event={slot.Timing}";
                return false;
            }
            if (!SupportsAnchor(slot.Timing, slot.Anchor))
            {
                error =
                    $"효과 연출 시점에서 사용할 수 없는 기준 위치입니다. Event={slot.Timing}, Anchor={slot.Anchor}";
                return false;
            }
            if (!SupportsMultiplicity(slot.Timing, slot.Anchor, slot.Multiplicity))
            {
                error =
                    $"효과 연출 시점·위치에서 사용할 수 없는 재생 횟수입니다. Multiplicity={slot.Multiplicity}";
                return false;
            }
            if (!SupportsAttachment(slot.Anchor, slot.Attachment))
            {
                error =
                    $"효과 연출 기준 위치에서 사용할 수 없는 부착 방식입니다. Attachment={slot.Attachment}";
                return false;
            }
            if (!SupportsEndPolicy(slot.Timing, slot.Multiplicity, slot.EndPolicy))
            {
                error =
                    $"효과 연출에서 사용할 수 없는 종료 규칙입니다. End={slot.EndPolicy}";
                return false;
            }
            if (slot.UseDuration && slot.EndPolicy != MonsterActivePresentationEndPolicy.Timed)
            {
                error = $"지속시간 직접 입력은 시간 종료 계약에서만 사용할 수 있습니다. Slot={slot.SlotId}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool SupportsEvent(MonsterActivePresentationEvent timing)
        {
            return timing is MonsterActivePresentationEvent.MotionStart or
                MonsterActivePresentationEvent.Launch or
                MonsterActivePresentationEvent.Impact or
                MonsterActivePresentationEvent.AreaResolved or
                MonsterActivePresentationEvent.StepEnd or
                MonsterActivePresentationEvent.EffectApplied or
                MonsterActivePresentationEvent.EffectExpired;
        }

        public static bool SupportsAnchor(
            MonsterActivePresentationEvent timing,
            MonsterActivePresentationAnchor anchor)
        {
            return anchor switch
            {
                MonsterActivePresentationAnchor.CasterRoot => true,
                MonsterActivePresentationAnchor.AttackOrigin or
                    MonsterActivePresentationAnchor.MarkerSocket =>
                    timing is MonsterActivePresentationEvent.MotionStart or
                        MonsterActivePresentationEvent.Launch or
                        MonsterActivePresentationEvent.StepEnd,
                MonsterActivePresentationAnchor.TargetPoint or
                    MonsterActivePresentationAnchor.TargetRoot or
                    MonsterActivePresentationAnchor.HitPoint =>
                    timing is MonsterActivePresentationEvent.Impact or
                        MonsterActivePresentationEvent.AreaResolved or
                        MonsterActivePresentationEvent.EffectApplied or
                        MonsterActivePresentationEvent.EffectExpired,
                MonsterActivePresentationAnchor.AreaCenter =>
                    timing is MonsterActivePresentationEvent.Impact or
                        MonsterActivePresentationEvent.AreaResolved or
                        MonsterActivePresentationEvent.StepEnd,
                _ => false
            };
        }

        public static bool SupportsMultiplicity(
            MonsterActivePresentationEvent timing,
            MonsterActivePresentationAnchor anchor,
            MonsterActivePresentationMultiplicity multiplicity)
        {
            return multiplicity switch
            {
                MonsterActivePresentationMultiplicity.OncePerStep => true,
                MonsterActivePresentationMultiplicity.PerTargetHit =>
                    (timing is MonsterActivePresentationEvent.Impact or
                        MonsterActivePresentationEvent.AreaResolved or
                        MonsterActivePresentationEvent.EffectApplied or
                        MonsterActivePresentationEvent.EffectExpired) &&
                    IsTargetAnchor(anchor),
                MonsterActivePresentationMultiplicity.ContinuousUntilEnd =>
                    timing is MonsterActivePresentationEvent.MotionStart or
                        MonsterActivePresentationEvent.Launch or
                        MonsterActivePresentationEvent.Impact or
                        MonsterActivePresentationEvent.AreaResolved or
                        MonsterActivePresentationEvent.EffectApplied,
                _ => false
            };
        }

        public static bool SupportsAttachment(
            MonsterActivePresentationAnchor anchor,
            MonsterActivePresentationAttachment attachment)
        {
            return attachment switch
            {
                MonsterActivePresentationAttachment.World => true,
                MonsterActivePresentationAttachment.FollowAnchor =>
                    anchor is MonsterActivePresentationAnchor.CasterRoot or
                        MonsterActivePresentationAnchor.AttackOrigin or
                        MonsterActivePresentationAnchor.MarkerSocket or
                        MonsterActivePresentationAnchor.TargetPoint or
                        MonsterActivePresentationAnchor.TargetRoot or
                        MonsterActivePresentationAnchor.HitPoint,
                _ => false
            };
        }

        public static bool SupportsEndPolicy(
            MonsterActivePresentationEvent timing,
            MonsterActivePresentationMultiplicity multiplicity,
            MonsterActivePresentationEndPolicy endPolicy)
        {
            if (multiplicity == MonsterActivePresentationMultiplicity.ContinuousUntilEnd &&
                endPolicy is not MonsterActivePresentationEndPolicy.Timed and
                    not MonsterActivePresentationEndPolicy.StepEnd)
            {
                return false;
            }

            return endPolicy switch
            {
                MonsterActivePresentationEndPolicy.Timed => true,
                MonsterActivePresentationEndPolicy.ParticleDuration => true,
                MonsterActivePresentationEndPolicy.StepEnd =>
                    timing != MonsterActivePresentationEvent.StepEnd,
                _ => false
            };
        }

        public static bool IsTargetAnchor(MonsterActivePresentationAnchor anchor)
        {
            return anchor is MonsterActivePresentationAnchor.TargetPoint or
                MonsterActivePresentationAnchor.TargetRoot or
                MonsterActivePresentationAnchor.HitPoint;
        }
    }
}
