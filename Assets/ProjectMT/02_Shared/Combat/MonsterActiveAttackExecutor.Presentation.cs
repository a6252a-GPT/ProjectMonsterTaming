using System;
using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public sealed partial class MonsterActiveAttackExecutor
    {
        private MonsterBasicAttackVfxContext CreateCurrentAttackBlockVfxContext(IDamageable target)
        {
            var origin = owner?.AnimationDriver?.AttackOrigin?.position ??
                         owner?.transform.position ?? Vector3.zero;
            var hitPoint = target?.Position ?? origin;
            var forward = hitPoint - origin;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = owner == null ? Vector3.forward : owner.transform.forward;
            }
            var areaCenter = currentAttackBlock?.Center switch
            {
                MonsterBasicAttackCenter.Source => origin,
                MonsterBasicAttackCenter.Forward =>
                    origin + forward.normalized * currentAttackBlock.ForwardOffset,
                _ => hitPoint
            };
            return new MonsterBasicAttackVfxContext(
                world,
                currentAttackBlock,
                owner?.RuntimeAssetSet?.FeedbackProfile,
                owner,
                target,
                owner?.AnimationDriver,
                null,
                null,
                origin,
                hitPoint,
                areaCenter,
                Quaternion.LookRotation(
                    forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized,
                    Vector3.up),
                0,
                currentAttackBlockPresentation?.AttackBlockBindings,
                currentStep?.StepId,
                currentAttackBlockSequence,
                currentStep?.PlaybackSpeed ?? 1f);
        }

        private void SpawnDeliveryVisuals(
            MonsterActiveAttackPresentationBinding presentation,
            UnitActor target)
        {
            if (presentation == null || !currentStep.IsProjectile) return;
            var origin = owner.AnimationDriver?.AttackOrigin?.position ?? owner.transform.position;
            var forward = ResolveForward(target);
            for (var slotIndex = 0; slotIndex < presentation.Slots.Count; slotIndex++)
            {
                var slot = presentation.Slots[slotIndex];
                if (slot == null || slot.Timing != MonsterActivePresentationEvent.DeliverySpawn ||
                    slot.Attachment != MonsterActivePresentationAttachment.DeliveryVisual ||
                    slot.Feedback == null)
                {
                    continue;
                }
                var count = slot.Multiplicity == MonsterActivePresentationMultiplicity.OncePerProjectile
                    ? currentStep.ProjectileCount
                    : 1;
                for (var projectileIndex = 0; projectileIndex < count; projectileIndex++)
                {
                    var direction = ResolveProjectileDirection(forward, projectileIndex);
                    var end = currentStep.Pattern == MonsterActiveAttackPattern.ExplosiveProjectile &&
                              projectileIndex == 0 && target != null
                        ? ResolveTargetHitPoint(target)
                        : origin + direction * currentStep.Range;
                    var rotation = Quaternion.LookRotation(direction, Vector3.up);
                    var instance = SpawnPresentationVfx(slot.Feedback, origin, rotation, null);
                    var duration = Mathf.Max(
                        0.05f / currentStep.PlaybackSpeed,
                        Vector3.Distance(origin, end) / ResolveStepProjectileSpeed());
                    deliveryVisuals.Add(new DeliveryVisual
                    {
                        Instance = instance,
                        Start = origin,
                        End = end,
                        Duration = duration,
                        Elapsed = 0f
                    });
                }
            }
        }

        private void PlayAtOwner(MonsterFeedbackCue cue)
        {
            world.PlayMonsterFeedback(cue, owner.AnimationDriver, null,
                owner.RuntimeAssetSet?.BodyProfile?.VfxScale ?? 1f);
        }

        private void PlayAtTarget(MonsterFeedbackCue cue, UnitActor target)
        {
            if (target == null) return;
            world.PlayMonsterFeedbackAt(
                cue,
                target.transform.position,
                Quaternion.LookRotation(ResolveForward(target), Vector3.up),
                owner.RuntimeAssetSet?.BodyProfile?.VfxScale ?? 1f);
        }

        private void PlayPresentationEvent(
            MonsterActiveAttackPresentationBinding presentation,
            MonsterActivePresentationEvent timing,
            UnitActor target,
            MonsterFeedbackCue legacyCue,
            bool skipDeliveryVisual = false)
        {
            if (presentation?.Slots.Count > 0)
            {
                for (var index = 0; index < presentation.Slots.Count; index++)
                {
                    var slot = presentation.Slots[index];
                    if (slot == null || slot.Timing != timing || slot.Feedback == null) continue;
                    if (skipDeliveryVisual && slot.Attachment == MonsterActivePresentationAttachment.DeliveryVisual)
                    {
                        continue;
                    }
                    if (slot.Multiplicity == MonsterActivePresentationMultiplicity.OncePerStep &&
                        !playedOncePerStepSlots.Add(slot.SlotId))
                    {
                        continue;
                    }
                    var count = ResolvePresentationCount(slot, timing);
                    for (var occurrence = 0; occurrence < count; occurrence++)
                    {
                        PlayPresentationSlot(slot, target, occurrence);
                    }
                }
                return;
            }

            if (timing == MonsterActivePresentationEvent.Telegraph ||
                timing == MonsterActivePresentationEvent.Impact)
            {
                PlayAtTarget(legacyCue, target);
            }
            else
            {
                PlayAtOwner(legacyCue);
            }
        }

        private void PlayPresentationSlot(
            MonsterActiveAttackPresentationCueBinding slot,
            UnitActor target,
            int occurrence)
        {
            var rotation = ResolvePresentationRotation(slot, target, occurrence);
            var position = ResolvePresentationPosition(slot, target, occurrence);
            var parent = slot.Attachment == MonsterActivePresentationAttachment.FollowAnchor
                ? ResolvePresentationParent(slot.Anchor, target)
                : null;
            if (slot.EndPolicy is MonsterActivePresentationEndPolicy.DeliveryEnd or
                MonsterActivePresentationEndPolicy.StepEnd or MonsterActivePresentationEndPolicy.MotionEnd)
            {
                var instance = SpawnPresentationVfx(slot.Feedback, position, rotation, parent);
                if (instance != null)
                {
                    trackedVfx.Add(new TrackedVfx { Instance = instance, EndPolicy = slot.EndPolicy });
                }
                return;
            }
            if (slot.Attachment == MonsterActivePresentationAttachment.FollowAnchor)
            {
                var instance = SpawnPresentationVfx(slot.Feedback, position, rotation, parent);
                if (instance != null)
                {
                    world.ScheduleMonsterObjectReturn(
                        instance,
                        ScaleStepDuration(slot.UseDuration ? slot.Duration : slot.Feedback.VfxLifetime));
                }
                return;
            }
            var timedInstance = SpawnPresentationVfx(slot.Feedback, position, rotation, null);
            if (timedInstance != null)
            {
                world.ScheduleMonsterObjectReturn(
                    timedInstance,
                    ScaleStepDuration(slot.UseDuration ? slot.Duration : slot.Feedback.VfxLifetime));
            }
        }

        private GameObject SpawnPresentationVfx(
            MonsterFeedbackCue cue,
            Vector3 position,
            Quaternion rotation,
            Transform parent)
        {
            var bodyScale = owner.RuntimeAssetSet?.BodyProfile?.VfxScale ?? 1f;
            var instance = world.SpawnMonsterActiveVfx(
                cue,
                position,
                rotation,
                parent,
                bodyScale,
                currentStep?.PlaybackSpeed ?? 1f);
            if (instance != null && cue?.VfxPrefab != null)
            {
                MonsterBasicAttackVfxPlayback.ApplyInstanceScale(
                    instance,
                    cue.VfxPrefab.transform.localScale * cue.Scale * Mathf.Max(0.01f, bodyScale));
            }
            return instance;
        }

        private int ResolvePresentationCount(
            MonsterActiveAttackPresentationCueBinding slot,
            MonsterActivePresentationEvent timing)
        {
            return slot.Multiplicity switch
            {
                MonsterActivePresentationMultiplicity.OncePerProjectile => Mathf.Max(1, currentStep.ProjectileCount),
                MonsterActivePresentationMultiplicity.PerTargetHit when timing != MonsterActivePresentationEvent.Impact =>
                    Mathf.Max(1, stepTargets.Count),
                _ => 1
            };
        }

        private Vector3 ResolvePresentationPosition(
            MonsterActiveAttackPresentationCueBinding slot,
            UnitActor target,
            int occurrence)
        {
            var origin = owner.AnimationDriver?.AttackOrigin?.position ?? owner.transform.position;
            var forward = ResolveForward(target);
            switch (slot.Anchor)
            {
                case MonsterActivePresentationAnchor.CasterRoot:
                    return owner.transform.position;
                case MonsterActivePresentationAnchor.AttackOrigin:
                case MonsterActivePresentationAnchor.MarkerSocket:
                case MonsterActivePresentationAnchor.TrajectoryOrigin:
                    return origin;
                case MonsterActivePresentationAnchor.TargetPoint:
                case MonsterActivePresentationAnchor.TargetRoot:
                    return target == null ? owner.transform.position : target.transform.position;
                case MonsterActivePresentationAnchor.HitPoint:
                    return ResolveTargetHitPoint(target);
                case MonsterActivePresentationAnchor.AreaCenter:
                    if (currentStep.Pattern == MonsterActiveAttackPattern.ExplosiveProjectile)
                    {
                        var explosiveDirection = ResolveProjectileDirection(forward, occurrence);
                        return occurrence == 0 && previousStepTarget != null
                            ? previousStepTarget.transform.position
                            : origin + explosiveDirection * currentStep.Range;
                    }
                    return ResolveEffectCenter(target, previousStepTarget, owner.transform.position, forward);
                case MonsterActivePresentationAnchor.ProjectileRoot:
                    if (slot.Timing == MonsterActivePresentationEvent.DeliverySpawn)
                    {
                        return origin;
                    }
                    var direction = ResolveProjectileDirection(forward, occurrence);
                    return currentStep.Pattern == MonsterActiveAttackPattern.ExplosiveProjectile &&
                           target != null && occurrence == 0
                        ? ResolveTargetHitPoint(target)
                        : origin + direction * currentStep.Range;
                default:
                    return owner.transform.position;
            }
        }

        private Quaternion ResolvePresentationRotation(
            MonsterActiveAttackPresentationCueBinding slot,
            UnitActor target,
            int occurrence)
        {
            var forward = slot.Anchor == MonsterActivePresentationAnchor.ProjectileRoot
                ? ResolveProjectileDirection(ResolveForward(target), occurrence)
                : ResolveForward(target);
            if (currentStep.Pattern == MonsterActiveAttackPattern.InstantMagic)
            {
                return currentStep.MagicDirection switch
                {
                    MonsterActiveMagicDirection.GroundUp =>
                        Quaternion.LookRotation(Vector3.up, forward),
                    MonsterActiveMagicDirection.SkyDown =>
                        Quaternion.LookRotation(Vector3.down, forward),
                    _ => Quaternion.LookRotation(forward, Vector3.up)
                };
            }
            return Quaternion.LookRotation(forward, Vector3.up);
        }

        private Transform ResolvePresentationParent(
            MonsterActivePresentationAnchor anchor,
            UnitActor target)
        {
            return anchor switch
            {
                MonsterActivePresentationAnchor.CasterRoot => owner.transform,
                MonsterActivePresentationAnchor.AttackOrigin or
                MonsterActivePresentationAnchor.MarkerSocket or
                MonsterActivePresentationAnchor.TrajectoryOrigin => owner.AnimationDriver?.AttackOrigin ?? owner.transform,
                MonsterActivePresentationAnchor.TargetRoot or
                    MonsterActivePresentationAnchor.TargetPoint => target == null ? null : target.transform,
                MonsterActivePresentationAnchor.HitPoint => target == null
                    ? null
                    : target.AnimationDriver?.HitCenter ?? target.transform,
                _ => null
            };
        }

        private void ReleaseTrackedVfx(MonsterActivePresentationEndPolicy? policy)
        {
            for (var index = trackedVfx.Count - 1; index >= 0; index--)
            {
                var tracked = trackedVfx[index];
                if (policy.HasValue && tracked.EndPolicy != policy.Value) continue;
                if (tracked.Instance != null) world?.ReturnMonsterObject(tracked.Instance);
                trackedVfx.RemoveAt(index);
            }
        }
    }
}
