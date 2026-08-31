using System;
using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public sealed class MonsterActiveAttackExecutor // 조립된 Step을 순서대로 한 번씩 실행
    {
        private sealed class PendingHit
        {
            public UnitActor Target;
            public float Delay;
            public Vector3 EffectCenter;
        }

        private sealed class DeliveryVisual
        {
            public GameObject Instance;
            public Vector3 Start;
            public Vector3 End;
            public float Duration;
            public float Elapsed;
        }

        private sealed class TrackedVfx
        {
            public GameObject Instance;
            public MonsterActivePresentationEndPolicy EndPolicy;
        }

        private readonly List<UnitActor> targetBuffer = new List<UnitActor>();
        private readonly List<UnitActor> stepTargets = new List<UnitActor>();
        private readonly List<PendingHit> pendingHits = new List<PendingHit>();
        private readonly List<DeliveryVisual> deliveryVisuals = new List<DeliveryVisual>();
        private readonly List<TrackedVfx> trackedVfx = new List<TrackedVfx>();
        private readonly HashSet<int> uniqueTargets = new HashSet<int>();
        private UnitActor owner;
        private CombatWorld world;
        private MonsterAttackActiveSkill skill;
        private UnitActor lockedTarget;
        private UnitActor previousStepTarget;
        private MonsterActiveAttackStep currentStep;
        private int stepIndex;
        private float waitRemaining;
        private bool stepPrepared;
        private bool feelPlayedForStep;
        private bool firstStepMotionAlreadyPlaying;
        private bool stepFired;

        public bool IsRunning { get; private set; }
        public int CompletedStepCount { get; private set; }

        public bool Begin(
            UnitActor source,
            CombatWorld combatWorld,
            MonsterAttackActiveSkill active,
            UnitActor initialTarget,
            bool initialStepMotionAlreadyPlaying = false)
        {
            Reset();
            if (source == null || combatWorld == null || active == null || active.Steps.Count == 0)
            {
                return false;
            }

            owner = source;
            world = combatWorld;
            skill = active;
            lockedTarget = initialTarget;
            firstStepMotionAlreadyPlaying = initialStepMotionAlreadyPlaying;
            IsRunning = true;
            PrepareNextStep();
            return true;
        }

        public bool Tick(float deltaTime)
        {
            if (!IsRunning) return true;
            if (owner == null || world == null || skill == null || !owner.IsAlive)
            {
                Reset();
                return true;
            }

            var remainingDelta = Mathf.Max(0f, deltaTime);
            var safety = 0;
            while (IsRunning && safety++ < 96)
            {
                if (pendingHits.Count > 0 || deliveryVisuals.Count > 0)
                {
                    TickPendingHits(remainingDelta);
                    TickDeliveryVisuals(remainingDelta);
                    if (pendingHits.Count == 0 && deliveryVisuals.Count == 0)
                    {
                        CompleteCurrentStep();
                    }
                    break;
                }

                if (waitRemaining > 0f)
                {
                    waitRemaining -= remainingDelta;
                    if (waitRemaining > 0f) break;
                    remainingDelta = Mathf.Max(0f, -waitRemaining);
                    waitRemaining = 0f;
                }

                if (!stepPrepared)
                {
                    PrepareCurrentStep();
                    if (!IsRunning) break;
                    if (waitRemaining > 0f) continue;
                }

                FireCurrentStep();
                if (pendingHits.Count == 0 && deliveryVisuals.Count == 0)
                {
                    CompleteCurrentStep();
                    continue;
                }
                TickPendingHits(remainingDelta);
                TickDeliveryVisuals(remainingDelta);
                if (pendingHits.Count == 0 && deliveryVisuals.Count == 0)
                {
                    CompleteCurrentStep();
                }
                break;
            }
            return !IsRunning;
        }

        public void Reset()
        {
            ReleaseTrackedVfx(null);
            for (var index = deliveryVisuals.Count - 1; index >= 0; index--)
            {
                if (deliveryVisuals[index].Instance != null) world?.ReturnMonsterObject(deliveryVisuals[index].Instance);
            }
            owner = null;
            world = null;
            skill = null;
            lockedTarget = null;
            previousStepTarget = null;
            currentStep = null;
            stepIndex = 0;
            waitRemaining = 0f;
            stepPrepared = false;
            firstStepMotionAlreadyPlaying = false;
            stepFired = false;
            IsRunning = false;
            CompletedStepCount = 0;
            targetBuffer.Clear();
            stepTargets.Clear();
            pendingHits.Clear();
            deliveryVisuals.Clear();
            trackedVfx.Clear();
            uniqueTargets.Clear();
        }

        private void PrepareNextStep()
        {
            if (stepIndex >= skill.Steps.Count)
            {
                IsRunning = false;
                return;
            }
            currentStep = skill.Steps[stepIndex];
            stepPrepared = false;
            stepFired = false;
            waitRemaining = currentStep.DelayAfterPrevious;
        }

        private void PrepareCurrentStep()
        {
            var target = ResolveStepTarget();
            if (target == null)
            {
                CompleteCurrentStep();
                return;
            }

            previousStepTarget = target;
            var presentation = skill.ResolvePresentation(currentStep.StepId);
            if (currentStep.TeleportBeforeAttack)
            {
                PlayPresentationEvent(
                    presentation,
                    MonsterActivePresentationEvent.TeleportExit,
                    target,
                    presentation?.TeleportExit);
                var direction = owner.transform.position - target.transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.0001f) direction = -target.transform.forward;
                var destination = target.transform.position +
                                  direction.normalized * currentStep.TeleportFrontDistance;
                owner.TryTeleportForActive(destination);
                PlayPresentationEvent(
                    presentation,
                    MonsterActivePresentationEvent.TeleportEnter,
                    target,
                    presentation?.TeleportEnter);
            }

            FaceTarget(target);
            var motionCommitDelay = 0f;
            PlayPresentationEvent(
                presentation,
                MonsterActivePresentationEvent.MotionStart,
                target,
                null);
            if (!(stepIndex == 0 && firstStepMotionAlreadyPlaying))
            {
                owner.AnimationDriver?.PlayActiveStep(
                    currentStep.StepId,
                    skill.CommitNormalizedTime,
                    out motionCommitDelay);
            }
            PlayPresentationEvent(
                presentation,
                MonsterActivePresentationEvent.Telegraph,
                target,
                presentation?.Telegraph);
            waitRemaining = Mathf.Max(currentStep.TelegraphDelay, motionCommitDelay);
            stepPrepared = true;
        }

        private void FireCurrentStep()
        {
            var presentation = skill.ResolvePresentation(currentStep.StepId);
            PlayPresentationEvent(
                presentation,
                MonsterActivePresentationEvent.Launch,
                previousStepTarget,
                presentation?.Launch);
            ResolveStepTargets(previousStepTarget);
            feelPlayedForStep = false;
            BuildPendingHits(previousStepTarget);
            SpawnDeliveryVisuals(presentation, previousStepTarget);
            PlayPresentationEvent(
                presentation,
                MonsterActivePresentationEvent.DeliverySpawn,
                previousStepTarget,
                null,
                true);
            PlayPresentationEvent(
                presentation,
                MonsterActivePresentationEvent.Travel,
                previousStepTarget,
                presentation?.Travel);
            stepFired = true;
            stepPrepared = false;
        }

        private void TickPendingHits(float deltaTime)
        {
            for (var index = pendingHits.Count - 1; index >= 0; index--)
            {
                var pending = pendingHits[index];
                pending.Delay -= Mathf.Max(0f, deltaTime);
                if (pending.Delay > 0f) continue;
                ApplyHit(pending.Target, pending.EffectCenter);
                pendingHits.RemoveAt(index);
            }
        }

        private void TickDeliveryVisuals(float deltaTime)
        {
            for (var index = deliveryVisuals.Count - 1; index >= 0; index--)
            {
                var delivery = deliveryVisuals[index];
                delivery.Elapsed += Mathf.Max(0f, deltaTime);
                var ratio = delivery.Duration <= 0f ? 1f : Mathf.Clamp01(delivery.Elapsed / delivery.Duration);
                if (delivery.Instance != null)
                {
                    delivery.Instance.transform.position = Vector3.Lerp(delivery.Start, delivery.End, ratio);
                }
                if (ratio < 1f) continue;
                if (delivery.Instance != null) world.ReturnMonsterObject(delivery.Instance);
                deliveryVisuals.RemoveAt(index);
            }
        }

        private void CompleteCurrentStep()
        {
            if (stepFired)
            {
                var presentation = skill.ResolvePresentation(currentStep.StepId);
                PlayPresentationEvent(
                    presentation,
                    MonsterActivePresentationEvent.AreaResolved,
                    previousStepTarget,
                    null);
                PlayPresentationEvent(
                    presentation,
                    MonsterActivePresentationEvent.DeliveryEnd,
                    previousStepTarget,
                    null);
                ReleaseTrackedVfx(MonsterActivePresentationEndPolicy.DeliveryEnd);
                PlayPresentationEvent(
                    presentation,
                    MonsterActivePresentationEvent.StepEnd,
                    previousStepTarget,
                    null);
                ReleaseTrackedVfx(MonsterActivePresentationEndPolicy.StepEnd);
                ReleaseTrackedVfx(MonsterActivePresentationEndPolicy.MotionEnd);
                ReleaseTrackedVfx(null);
            }
            CompletedStepCount++;
            stepIndex++;
            pendingHits.Clear();
            stepTargets.Clear();
            uniqueTargets.Clear();
            stepFired = false;
            PrepareNextStep();
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
                    var duration = Mathf.Max(0.05f, Vector3.Distance(origin, end) / currentStep.ProjectileSpeed);
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

        private UnitActor ResolveStepTarget()
        {
            var previous = previousStepTarget != null && previousStepTarget.IsAlive &&
                           previousStepTarget.IsCombatReady
                ? previousStepTarget
                : null;
            var initial = lockedTarget != null && lockedTarget.IsAlive && lockedTarget.IsCombatReady
                ? lockedTarget
                : null;
            if (previous == null && initial != null)
            {
                return initial; // 첫 Step은 현재 전투 타깃을 기준으로 시작
            }
            if (currentStep.TargetPolicy == MonsterActiveTargetPolicy.SameTarget && previous != null)
            {
                return previous;
            }

            world.CollectUnits(OpponentTeam, owner.transform.position, float.PositiveInfinity, 256, targetBuffer);
            for (var index = 0; index < targetBuffer.Count; index++)
            {
                var candidate = targetBuffer[index];
                if (candidate != null && candidate != previous) return candidate;
            }
            return previous ?? initial ?? (targetBuffer.Count > 0 ? targetBuffer[0] : null);
        }

        private void ResolveStepTargets(UnitActor primary)
        {
            stepTargets.Clear();
            uniqueTargets.Clear();
            var origin = owner.transform.position;
            var forward = ResolveForward(primary);
            switch (currentStep.Pattern)
            {
                case MonsterActiveAttackPattern.Line:
                case MonsterActiveAttackPattern.PiercingBeam:
                    world.CollectUnitsInLine(OpponentTeam, origin, forward, currentStep.Range,
                        currentStep.Width, currentStep.MaxTargets, targetBuffer);
                    AddUnique(targetBuffer);
                    break;
                case MonsterActiveAttackPattern.Cone:
                    world.CollectUnitsInFan(OpponentTeam, origin, forward, currentStep.Range,
                        currentStep.Angle, currentStep.MaxTargets, targetBuffer);
                    AddUnique(targetBuffer);
                    break;
                case MonsterActiveAttackPattern.SelfCircle:
                    world.CollectUnits(OpponentTeam, origin, currentStep.Radius,
                        currentStep.MaxTargets, targetBuffer);
                    AddUnique(targetBuffer);
                    break;
                case MonsterActiveAttackPattern.FrontCircle:
                    world.CollectUnits(OpponentTeam, origin + forward * currentStep.ForwardOffset,
                        currentStep.Radius, currentStep.MaxTargets, targetBuffer);
                    AddUnique(targetBuffer);
                    break;
                case MonsterActiveAttackPattern.PiercingProjectile:
                    CollectProjectileLines(origin, forward);
                    break;
                case MonsterActiveAttackPattern.ExplosiveProjectile:
                    CollectExplosions(origin, forward, primary);
                    break;
                case MonsterActiveAttackPattern.InstantMagic:
                    if (currentStep.InstantMagicTarget == MonsterActiveInstantMagicTarget.SingleTarget)
                    {
                        AddUnique(primary);
                    }
                    else
                    {
                        world.CollectUnits(OpponentTeam, primary.transform.position, currentStep.Radius,
                            currentStep.MaxTargets, targetBuffer);
                        AddUnique(targetBuffer);
                    }
                    break;
            }
        }

        private void CollectProjectileLines(Vector3 origin, Vector3 forward)
        {
            for (var projectileIndex = 0; projectileIndex < currentStep.ProjectileCount; projectileIndex++)
            {
                var direction = ResolveProjectileDirection(forward, projectileIndex);
                world.CollectUnitsInLine(OpponentTeam, origin, direction, currentStep.Range,
                    currentStep.ProjectileCollisionRadius * 2f, currentStep.MaxTargets, targetBuffer);
                AddUnique(targetBuffer);
            }
        }

        private void CollectExplosions(Vector3 origin, Vector3 forward, UnitActor primary)
        {
            for (var projectileIndex = 0; projectileIndex < currentStep.ProjectileCount; projectileIndex++)
            {
                var direction = ResolveProjectileDirection(forward, projectileIndex);
                var center = projectileIndex == 0 && primary != null
                    ? primary.transform.position
                    : origin + direction * currentStep.Range;
                world.CollectUnits(OpponentTeam, center, currentStep.ExplosionRadius,
                    currentStep.MaxTargets, targetBuffer);
                AddUnique(targetBuffer);
            }
        }

        private void BuildPendingHits(UnitActor primary)
        {
            pendingHits.Clear();
            if (stepTargets.Count == 0) return;
            var origin = owner.transform.position;
            var forward = ResolveForward(primary);
            var center = currentStep.Pattern == MonsterActiveAttackPattern.SelfCircle
                ? origin
                : currentStep.Pattern is MonsterActiveAttackPattern.FrontCircle or MonsterActiveAttackPattern.InstantMagic
                    ? primary.transform.position
                    : origin;
            var minAxis = float.PositiveInfinity;
            var maxAxis = float.NegativeInfinity;
            var axes = new float[stepTargets.Count];
            for (var index = 0; index < stepTargets.Count; index++)
            {
                var offset = stepTargets[index].transform.position - center;
                offset.y = 0f;
                var axis = currentStep.Progression switch
                {
                    MonsterActiveAttackProgression.Forward => Vector3.Dot(offset, forward),
                    MonsterActiveAttackProgression.LeftToRight => Vector3.Dot(offset, Vector3.Cross(Vector3.up, forward)),
                    MonsterActiveAttackProgression.RightToLeft => -Vector3.Dot(offset, Vector3.Cross(Vector3.up, forward)),
                    MonsterActiveAttackProgression.Outward => offset.magnitude,
                    _ => 0f
                };
                axes[index] = axis;
                minAxis = Mathf.Min(minAxis, axis);
                maxAxis = Mathf.Max(maxAxis, axis);
            }

            for (var index = 0; index < stepTargets.Count; index++)
            {
                var delay = 0f;
                if (currentStep.Progression != MonsterActiveAttackProgression.Instant && maxAxis > minAxis)
                {
                    delay += Mathf.InverseLerp(minAxis, maxAxis, axes[index]) * currentStep.ProgressionDuration;
                }
                if (currentStep.IsProjectile)
                {
                    delay += Vector3.Distance(origin, stepTargets[index].transform.position) /
                             currentStep.ProjectileSpeed;
                }
                pendingHits.Add(new PendingHit
                {
                    Target = stepTargets[index],
                    Delay = delay,
                    EffectCenter = ResolveEffectCenter(stepTargets[index], primary, origin, forward)
                });
            }
        }

        private Vector3 ResolveEffectCenter(UnitActor target, UnitActor primary, Vector3 origin, Vector3 forward)
        {
            switch (currentStep.Pattern)
            {
                case MonsterActiveAttackPattern.SelfCircle:
                    return origin;
                case MonsterActiveAttackPattern.FrontCircle:
                    return origin + forward * currentStep.ForwardOffset;
                case MonsterActiveAttackPattern.InstantMagic:
                    return primary == null ? origin : primary.transform.position;
                case MonsterActiveAttackPattern.ExplosiveProjectile:
                    var closest = primary == null ? origin + forward * currentStep.Range : primary.transform.position;
                    var closestDistance = (target.transform.position - closest).sqrMagnitude;
                    for (var projectileIndex = 1; projectileIndex < currentStep.ProjectileCount; projectileIndex++)
                    {
                        var candidate = origin + ResolveProjectileDirection(forward, projectileIndex) * currentStep.Range;
                        var distance = (target.transform.position - candidate).sqrMagnitude;
                        if (distance >= closestDistance) continue;
                        closest = candidate;
                        closestDistance = distance;
                    }
                    return closest;
                default:
                    return origin;
            }
        }

        private void ApplyHit(UnitActor target, Vector3 effectCenter)
        {
            if (target == null || !target.IsAlive || !target.IsCombatReady) return;
            var amount = owner.EffectiveStats.damage * currentStep.DamageMultiplier;
            var feel = skill.ImpactFeel;
            var feelIntensity = Mathf.Clamp(currentStep.DamageMultiplier, 0.5f, 2f);
            var feelOwnsTargetMotion = !feelPlayedForStep &&
                                       world.WillPlayBasicAttackFeelTargetMotion(
                                           feel,
                                           target.gameObject,
                                           feelIntensity);
            var feedbackFlags = feelOwnsTargetMotion
                ? DamageFeedbackFlags.BasicAttackFeelTargetMotion
                : DamageFeedbackFlags.None;
            if (!world.ApplyMonsterSkillDamage(owner, target.Health, amount, feedbackFlags)) return;
            var presentation = skill.ResolvePresentation(currentStep.StepId);
            var impactRotation = Quaternion.LookRotation(ResolveForward(target), Vector3.up);
            var bodyScale = owner.RuntimeAssetSet?.BodyProfile?.VfxScale ?? 1f;
            PlayPresentationEvent(
                presentation,
                MonsterActivePresentationEvent.Impact,
                target,
                presentation?.Impact);
            if (!feelPlayedForStep && feel?.HasFeel == true)
            {
                world.PlayBasicAttackFeelAt(
                    feel,
                    target.transform.position,
                    impactRotation,
                    bodyScale,
                    target.gameObject,
                    feelIntensity);
                feelPlayedForStep = true;
            }
            for (var index = 0; index < currentStep.HitEffects.Count; index++)
            {
                ApplyHitEffect(target, currentStep.HitEffects[index], effectCenter);
            }
        }

        private void ApplyHitEffect(UnitActor target, MonsterActiveHitEffect effect, Vector3 effectCenter)
        {
            switch (effect.Type)
            {
                case MonsterActiveHitEffectType.Knockback:
                    target.TryApplyActiveKnockback(target.transform.position - owner.transform.position,
                        effect.Magnitude, effect.Duration, effect.SecondaryMagnitude);
                    break;
                case MonsterActiveHitEffectType.Airborne:
                    target.TryApplyActiveAirborne(effect.Magnitude, effect.Duration);
                    break;
                case MonsterActiveHitEffectType.Stun:
                    target.TryApplyActiveStun(effect.Duration);
                    break;
                case MonsterActiveHitEffectType.Bleed:
                    target.ApplyActiveBleed(owner, effect.Magnitude, effect.Duration, effect.TickInterval);
                    break;
                case MonsterActiveHitEffectType.Slow:
                    target.ApplyActiveSlow(effect.Magnitude, effect.Duration);
                    break;
                case MonsterActiveHitEffectType.Pull:
                    target.TryApplyActivePull(effectCenter, effect.Magnitude, effect.Duration);
                    break;
            }
        }

        private void AddUnique(IReadOnlyList<UnitActor> candidates)
        {
            for (var index = 0; index < candidates.Count && stepTargets.Count < currentStep.MaxTargets; index++)
            {
                AddUnique(candidates[index]);
            }
        }

        private void AddUnique(UnitActor candidate)
        {
            if (candidate == null || !candidate.IsAlive || candidate.Team != OpponentTeam) return;
            if (uniqueTargets.Add(candidate.GetInstanceID())) stepTargets.Add(candidate);
        }

        private Vector3 ResolveProjectileDirection(Vector3 forward, int index)
        {
            if (currentStep.ProjectileCount <= 1) return forward;
            if (index <= 0) return forward; // 기준탄은 항상 실제 타깃 방향과 일치
            var sideIndex = (index + 1) / 2;
            var sideCount = Mathf.CeilToInt((currentStep.ProjectileCount - 1) * 0.5f);
            var yawStep = currentStep.ProjectileFanAngle * 0.5f / Mathf.Max(1, sideCount);
            var yaw = yawStep * sideIndex * (index % 2 == 1 ? -1f : 1f);
            return Quaternion.AngleAxis(yaw, Vector3.up) * forward;
        }

        private Vector3 ResolveForward(UnitActor target)
        {
            var forward = target == null
                ? owner.transform.forward
                : target.transform.position - owner.transform.position;
            forward.y = 0f;
            return forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
        }

        private void FaceTarget(UnitActor target)
        {
            var forward = ResolveForward(target);
            owner.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
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
            var position = ResolvePresentationPosition(slot.Anchor, target, occurrence);
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
                        slot.UseDuration ? slot.Duration : slot.Feedback.VfxLifetime);
                }
                return;
            }
            var timedInstance = SpawnPresentationVfx(slot.Feedback, position, rotation, null);
            if (timedInstance != null)
            {
                world.ScheduleMonsterObjectReturn(
                    timedInstance,
                    slot.UseDuration ? slot.Duration : slot.Feedback.VfxLifetime);
            }
        }

        private GameObject SpawnPresentationVfx(
            MonsterFeedbackCue cue,
            Vector3 position,
            Quaternion rotation,
            Transform parent)
        {
            var bodyScale = owner.RuntimeAssetSet?.BodyProfile?.VfxScale ?? 1f;
            var instance = world.SpawnMonsterActiveVfx(cue, position, rotation, parent, bodyScale);
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
            MonsterActivePresentationAnchor anchor,
            UnitActor target,
            int occurrence)
        {
            var origin = owner.AnimationDriver?.AttackOrigin?.position ?? owner.transform.position;
            var forward = ResolveForward(target);
            switch (anchor)
            {
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
                return currentStep.MagicDirection == MonsterActiveMagicDirection.GroundUp
                    ? Quaternion.LookRotation(Vector3.up, forward)
                    : Quaternion.LookRotation(Vector3.down, forward);
            }
            return Quaternion.LookRotation(forward, Vector3.up);
        }

        private Transform ResolvePresentationParent(
            MonsterActivePresentationAnchor anchor,
            UnitActor target)
        {
            return anchor switch
            {
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

        private Vector3 ResolveTargetHitPoint(UnitActor target)
        {
            return target == null
                ? owner.transform.position
                : target.AnimationDriver?.HitCenter?.position ?? target.transform.position;
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

        private UnitTeam OpponentTeam => owner.Team == UnitTeam.Player ? UnitTeam.Enemy : UnitTeam.Player;
    }
}
