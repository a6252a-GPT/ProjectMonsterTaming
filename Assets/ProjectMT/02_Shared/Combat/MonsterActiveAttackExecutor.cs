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
        }

        private readonly List<UnitActor> targetBuffer = new List<UnitActor>();
        private readonly List<UnitActor> stepTargets = new List<UnitActor>();
        private readonly List<PendingHit> pendingHits = new List<PendingHit>();
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
                if (pendingHits.Count > 0)
                {
                    TickPendingHits(remainingDelta);
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
                if (pendingHits.Count == 0)
                {
                    CompleteCurrentStep();
                    continue;
                }
                TickPendingHits(remainingDelta);
                break;
            }
            return !IsRunning;
        }

        public void Reset()
        {
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
            IsRunning = false;
            CompletedStepCount = 0;
            targetBuffer.Clear();
            stepTargets.Clear();
            pendingHits.Clear();
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
            if (pendingHits.Count > 0)
            {
                PlayPresentationEvent(
                    presentation,
                    MonsterActivePresentationEvent.Travel,
                    previousStepTarget,
                    presentation?.Travel);
            }
            stepPrepared = false;
        }

        private void TickPendingHits(float deltaTime)
        {
            for (var index = pendingHits.Count - 1; index >= 0; index--)
            {
                var pending = pendingHits[index];
                pending.Delay -= Mathf.Max(0f, deltaTime);
                if (pending.Delay > 0f) continue;
                ApplyHit(pending.Target);
                pendingHits.RemoveAt(index);
            }
            if (pendingHits.Count == 0) CompleteCurrentStep();
        }

        private void CompleteCurrentStep()
        {
            CompletedStepCount++;
            stepIndex++;
            pendingHits.Clear();
            stepTargets.Clear();
            uniqueTargets.Clear();
            PrepareNextStep();
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
                pendingHits.Add(new PendingHit { Target = stepTargets[index], Delay = delay });
            }
        }

        private void ApplyHit(UnitActor target)
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
                ApplyHitEffect(target, currentStep.HitEffects[index]);
            }
        }

        private void ApplyHitEffect(UnitActor target, MonsterActiveHitEffect effect)
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
            var ratio = index / (float)(currentStep.ProjectileCount - 1);
            var yaw = Mathf.Lerp(-currentStep.ProjectileFanAngle * 0.5f,
                currentStep.ProjectileFanAngle * 0.5f, ratio);
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
            MonsterFeedbackCue legacyCue)
        {
            if (presentation?.Slots.Count > 0)
            {
                for (var index = 0; index < presentation.Slots.Count; index++)
                {
                    var slot = presentation.Slots[index];
                    if (slot == null || slot.Timing != timing || slot.Feedback == null) continue;
                    PlayPresentationSlot(slot, target);
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
            UnitActor target)
        {
            var rotation = target == null
                ? owner.transform.rotation
                : Quaternion.LookRotation(ResolveForward(target), Vector3.up);
            var position = owner.transform.position;
            switch (slot.Anchor)
            {
                case MonsterActivePresentationAnchor.AttackOrigin:
                    position = owner.AnimationDriver?.AttackOrigin?.position ?? owner.transform.position;
                    break;
                case MonsterActivePresentationAnchor.TargetPoint:
                    position = target == null ? owner.transform.position : target.transform.position;
                    break;
            }
            world.PlayMonsterFeedbackAt(
                slot.Feedback,
                position,
                rotation,
                owner.RuntimeAssetSet?.BodyProfile?.VfxScale ?? 1f);
        }

        private UnitTeam OpponentTeam => owner.Team == UnitTeam.Player ? UnitTeam.Enemy : UnitTeam.Player;
    }
}
