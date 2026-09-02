using System;
using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public sealed partial class MonsterActiveAttackExecutor
    {
        private void PrepareNextStep(bool fromPreviousLaunch)
        {
            if (stepIndex >= skill.Steps.Count)
            {
                currentStep = null;
                currentAttackBlock = null;
                currentAttackBlockPresentation = null;
                TryFinishAfterInFlightSteps();
                return;
            }
            currentStep = skill.Steps[stepIndex];
            currentDamageMultiplier = currentStep.ResolveDamageMultiplier(UnityEngine.Random.value);
            currentAttackBlock = skill.ResolveAttackBlock(currentStep.StepId);
            currentAttackBlockPresentation = skill.ResolvePresentation(currentStep.StepId);
            playedOncePerStepSlots.Clear();
            stepPrepared = false;
            stepFired = false;
            stepLifetimeRemaining = 0f;
            preparedMotionDuration = 0f;
            preparedMotionElapsed = 0f;
            preparedWaitDuration = 0f;
            preparingFromPreviousLaunch = fromPreviousLaunch;
            launchChainMinimumDelay = fromPreviousLaunch
                ? ScaleStepDuration(currentStep.DelayAfterPrevious)
                : 0f;
            waitRemaining = fromPreviousLaunch
                ? 0f
                : ScaleStepDuration(currentStep.DelayAfterPrevious);
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
            if (currentAttackBlock != null)
            {
                PrepareCurrentAttackBlock(target);
                return;
            }
            if (currentStep.DashBeforeAttack)
            {
                PlayPresentationEvent(
                    presentation,
                    MonsterActivePresentationEvent.DashExit,
                    target,
                    presentation?.DashExit);
                owner.AdvanceForBasicAttack(
                    target.transform.position,
                    currentStep.DashDistance,
                    owner.BodyRadius + target.BodyRadius,
                    currentStep.DashDuration / currentStep.PlaybackSpeed);
                PlayPresentationEvent(
                    presentation,
                    MonsterActivePresentationEvent.DashEnter,
                    target,
                    presentation?.DashEnter);
            }

            FaceTarget(target);
            var motionCommitDelay = 0f;
            var resolvedMotionCommitDelay = 0f;
            owner.AnimationDriver?.TryResolveActiveStepTiming(
                currentStep.StepId,
                skill.CommitNormalizedTime,
                out preparedMotionDuration,
                out resolvedMotionCommitDelay,
                currentStep.PlaybackSpeed);
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
                    out motionCommitDelay,
                    currentStep.PlaybackSpeed);
            }
            else
            {
                preparedMotionElapsed = Mathf.Min(
                    preparedMotionDuration,
                    resolvedMotionCommitDelay + ScaleStepDuration(currentStep.DelayAfterPrevious));
            }
            PlayPresentationEvent(
                presentation,
                MonsterActivePresentationEvent.Telegraph,
                target,
                presentation?.Telegraph);
            preparedWaitDuration = Mathf.Max(
                ScaleStepDuration(currentStep.TelegraphDelay),
                motionCommitDelay);
            waitRemaining = preparingFromPreviousLaunch
                ? Mathf.Max(preparedWaitDuration, launchChainMinimumDelay)
                : preparedWaitDuration;
            preparingFromPreviousLaunch = false;
            launchChainMinimumDelay = 0f;
            stepPrepared = true;
        }

        private void PrepareCurrentAttackBlock(UnitActor target)
        {
            FaceTarget(target);
            var motionCommitDelay = 0f;
            var resolvedMotionCommitDelay = 0f;
            owner.AnimationDriver?.TryResolveActiveStepTiming(
                currentStep.StepId,
                skill.CommitNormalizedTime,
                out preparedMotionDuration,
                out resolvedMotionCommitDelay,
                currentStep.PlaybackSpeed);
            if (!(stepIndex == 0 && firstStepMotionAlreadyPlaying))
            {
                owner.AnimationDriver?.PlayActiveStep(
                    currentStep.StepId,
                    skill.CommitNormalizedTime,
                    out motionCommitDelay,
                    currentStep.PlaybackSpeed);
            }
            else
            {
                preparedMotionElapsed = Mathf.Min(
                    preparedMotionDuration,
                    resolvedMotionCommitDelay + ScaleStepDuration(currentStep.DelayAfterPrevious));
            }

            currentAttackBlockSequence = unchecked(
                (owner.AnimationDriver?.ActionSequenceId ?? 0) * 397 + stepIndex + 1);
            var hitEffectStep = currentStep;
            var hitEffectPrimaryTarget = target;
            var hitEffectOwner = owner;
            var hitEffectOrigin = owner.AnimationDriver?.AttackOrigin?.position ??
                                  owner.transform.position;
            var hitEffectForward = ResolveForward(target);
            currentAttackBlockContext = new MonsterActionExecutionContext(
                world,
                owner,
                target.Health,
                owner.EffectiveStats,
                owner.RuntimeAssetSet,
                null,
                owner.AnimationDriver,
                currentAttackBlock,
                currentAttackBlockPresentation?.AttackBlockBindings,
                currentDamageMultiplier,
                1f,
                currentStep.PlaybackSpeed,
                true,
                (hitTarget, hitPoint) => ApplyAttackBlockHitEffects(
                    hitEffectStep,
                    hitEffectPrimaryTarget,
                    hitTarget,
                    hitPoint,
                    hitEffectOwner,
                    hitEffectOrigin,
                    hitEffectForward),
                currentStep.StepId,
                currentAttackBlockSequence);
            MonsterBasicAttackVfxRuntime.BeginMotion(
                CreateCurrentAttackBlockVfxContext(target.Health));
            currentAttackBlockMotionBegun = true;
            preparedWaitDuration = Mathf.Max(
                currentAttackBlock.TelegraphDelay / currentStep.PlaybackSpeed,
                motionCommitDelay);
            waitRemaining = preparingFromPreviousLaunch
                ? Mathf.Max(preparedWaitDuration, launchChainMinimumDelay)
                : preparedWaitDuration;
            preparingFromPreviousLaunch = false;
            launchChainMinimumDelay = 0f;
            stepPrepared = true;
        }

        private void FireCurrentAttackBlock()
        {
            attackBlockExecutor.Execute(currentAttackBlockContext);
            stepFired = true;
            stepPrepared = false;
            var motionRemaining = Mathf.Max(
                0f,
                preparedMotionDuration - preparedMotionElapsed - preparedWaitDuration);
            stepLifetimeRemaining = Mathf.Max(
                ResolveCurrentAttackBlockDuration(),
                motionRemaining);
            if (ShouldStartNextStepAfterLaunch())
            {
                DetachCurrentAttackBlockAndStartNext();
            }
        }

        private float ResolveCurrentAttackBlockDuration()
        {
            if (currentAttackBlock == null) return 0f;
            var duration = currentAttackBlock.ResolveActivityDuration(
                currentStep.PlaybackSpeed,
                owner?.AnimationDriver?.CurrentBreathDuration ?? 0f);
            if (currentAttackBlock.UsesProjectileVisual)
            {
                var origin = owner?.AnimationDriver?.AttackOrigin?.position ??
                             owner?.transform.position ?? Vector3.zero;
                var usesTargetEndpoint =
                    currentAttackBlock.ProjectileTravel is MonsterBasicAttackProjectileTravel.Homing or
                        MonsterBasicAttackProjectileTravel.Returning ||
                    currentAttackBlock.CollisionModule ==
                    MonsterBasicAttackCollisionModule.StopOnFirstTarget;
                var distance = usesTargetEndpoint && currentAttackBlockContext.PrimaryTarget != null
                    ? Vector3.Distance(origin, currentAttackBlockContext.PrimaryTarget.Position)
                    : currentAttackBlock.ResolveRange(1f);
                if (currentAttackBlock.ProjectileTravel ==
                    MonsterBasicAttackProjectileTravel.Returning)
                {
                    distance *= 2f;
                }
                duration = Mathf.Max(
                    duration,
                    distance /
                    Mathf.Max(
                        0.01f,
                        currentAttackBlock.ProjectileSpeed * currentStep.PlaybackSpeed));
            }
            return duration;
        }

        private void ApplyAttackBlockHitEffects(
            MonsterActiveAttackStep step,
            UnitActor primaryTarget,
            UnitActor target,
            Vector3 hitPoint,
            UnitActor source,
            Vector3 launchOrigin,
            Vector3 launchForward)
        {
            if (target == null || step == null) return;
            var origin = source == null ? launchOrigin : source.transform.position;
            var forward = source == null
                ? launchForward
                : ResolveForward(source, primaryTarget, launchForward);
            var center = ResolveEffectCenter(step, target, primaryTarget, origin, forward);
            for (var index = 0; index < step.HitEffects.Count; index++)
            {
                ApplyHitEffect(target, step.HitEffects[index], center, source, origin);
            }
        }

        private bool ShouldStartNextStepAfterLaunch()
        {
            var nextIndex = stepIndex + 1;
            return nextIndex < skill.Steps.Count &&
                   skill.Steps[nextIndex] != null &&
                   skill.Steps[nextIndex].StartMode ==
                   MonsterActiveStepStartMode.AfterPreviousLaunch;
        }

        private void DetachCurrentAttackBlockAndStartNext()
        {
            var motionContext = CreateCurrentAttackBlockVfxContext(previousStepTarget?.Health);
            if (stepLifetimeRemaining <= 0.0001f)
            {
                MonsterBasicAttackVfxRuntime.EndMotion(motionContext);
                CompletedStepCount++;
            }
            else
            {
                inFlightAttackBlocks.Add(new InFlightAttackBlock
                {
                    MotionContext = motionContext,
                    Remaining = stepLifetimeRemaining
                });
            }
            currentAttackBlockMotionBegun = false;
            stepFired = false;
            stepIndex++;
            PrepareNextStep(true);
        }

        private void TickInFlightAttackBlocks(float deltaTime)
        {
            for (var index = inFlightAttackBlocks.Count - 1; index >= 0; index--)
            {
                var item = inFlightAttackBlocks[index];
                item.Remaining = Mathf.Max(0f, item.Remaining - Mathf.Max(0f, deltaTime));
                if (item.Remaining > 0.0001f) continue;
                MonsterBasicAttackVfxRuntime.EndMotion(item.MotionContext);
                inFlightAttackBlocks.RemoveAt(index);
                CompletedStepCount++;
            }
            TryFinishAfterInFlightSteps();
        }

        private void TryFinishAfterInFlightSteps()
        {
            if (!IsRunning || currentStep != null || stepIndex < (skill?.Steps.Count ?? 0) ||
                inFlightAttackBlocks.Count > 0)
            {
                return;
            }
            IsRunning = false;
        }

        private void EndCurrentAttackBlockMotion()
        {
            if (!currentAttackBlockMotionBegun || currentAttackBlock == null || owner == null)
            {
                currentAttackBlockMotionBegun = false;
                return;
            }
            MonsterBasicAttackVfxRuntime.EndMotion(
                CreateCurrentAttackBlockVfxContext(previousStepTarget?.Health));
            currentAttackBlockMotionBegun = false;
        }

        private void FireCurrentStep()
        {
            if (currentAttackBlock != null)
            {
                FireCurrentAttackBlock();
                return;
            }
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
            var motionRemaining = Mathf.Max(
                0f,
                preparedMotionDuration - preparedMotionElapsed - preparedWaitDuration);
            stepLifetimeRemaining = Mathf.Max(
                ScaleStepDuration(currentStep.VisualDuration),
                ScaleStepDuration(currentStep.ProgressionDuration),
                motionRemaining);
        }

        private float ResolveStepActivityRemaining()
        {
            var remaining = stepLifetimeRemaining;
            for (var index = 0; index < pendingHits.Count; index++)
            {
                remaining = Mathf.Max(remaining, pendingHits[index].Delay);
            }
            for (var index = 0; index < deliveryVisuals.Count; index++)
            {
                remaining = Mathf.Max(
                    remaining,
                    deliveryVisuals[index].Duration - deliveryVisuals[index].Elapsed);
            }
            return Mathf.Max(0f, remaining);
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
            if (currentAttackBlock != null)
            {
                EndCurrentAttackBlockMotion();
            }
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
            PrepareNextStep(false);
        }

        private float ScaleStepDuration(float duration) =>
            Mathf.Max(0f, duration) / Mathf.Max(0.05f, currentStep?.PlaybackSpeed ?? 1f);

        private float ResolveStepProjectileSpeed() =>
            currentStep.ProjectileSpeed * currentStep.PlaybackSpeed;
    }
}
