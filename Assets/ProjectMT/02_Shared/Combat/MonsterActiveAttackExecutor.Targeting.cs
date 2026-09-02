using System;
using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public sealed partial class MonsterActiveAttackExecutor
    {
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
                    delay += Mathf.InverseLerp(minAxis, maxAxis, axes[index]) *
                             ScaleStepDuration(currentStep.ProgressionDuration);
                }
                if (currentStep.IsProjectile)
                {
                    delay += Vector3.Distance(origin, stepTargets[index].transform.position) /
                             ResolveStepProjectileSpeed();
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
            return ResolveEffectCenter(currentStep, target, primary, origin, forward);
        }

        private static Vector3 ResolveEffectCenter(
            MonsterActiveAttackStep step,
            UnitActor target,
            UnitActor primary,
            Vector3 origin,
            Vector3 forward)
        {
            if (step == null) return origin;
            switch (step.Pattern)
            {
                case MonsterActiveAttackPattern.SelfCircle:
                    return origin;
                case MonsterActiveAttackPattern.FrontCircle:
                    return origin + forward * step.ForwardOffset;
                case MonsterActiveAttackPattern.InstantMagic:
                    return primary == null ? origin : primary.transform.position;
                case MonsterActiveAttackPattern.ExplosiveProjectile:
                    var closest = primary == null ? origin + forward * step.Range : primary.transform.position;
                    var closestDistance = (target.transform.position - closest).sqrMagnitude;
                    for (var projectileIndex = 1; projectileIndex < step.ProjectileCount; projectileIndex++)
                    {
                        var count = Mathf.Max(1, step.ProjectileCount);
                        var spreadRatio = count <= 1
                            ? 0f
                            : Mathf.Clamp(projectileIndex, 0, count - 1) / (float)(count - 1) - 0.5f;
                        var direction = Quaternion.AngleAxis(
                            spreadRatio * step.ProjectileFanAngle,
                            Vector3.up) * forward;
                        var candidate = origin + direction * step.Range;
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
            if (currentAttackBlock != null)
                return currentAttackBlock.ResolveProjectileDirection(forward, index);
            var count = Mathf.Max(1, currentStep.ProjectileCount);
            var spreadRatio = count <= 1
                ? 0f
                : Mathf.Clamp(index, 0, count - 1) / (float)(count - 1) - 0.5f;
            return Quaternion.AngleAxis(
                spreadRatio * currentStep.ProjectileFanAngle,
                Vector3.up) * forward;
        }

        private Vector3 ResolveForward(UnitActor target)
        {
            var forward = target == null
                ? owner.transform.forward
                : target.transform.position - owner.transform.position;
            forward.y = 0f;
            return forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
        }

        private static Vector3 ResolveForward(
            UnitActor source,
            UnitActor target,
            Vector3 fallback)
        {
            if (source == null) return fallback.sqrMagnitude < 0.0001f
                ? Vector3.forward
                : fallback.normalized;
            var forward = target == null
                ? source.transform.forward
                : target.transform.position - source.transform.position;
            forward.y = 0f;
            return forward.sqrMagnitude < 0.0001f
                ? Vector3.forward
                : forward.normalized;
        }

        private void FaceTarget(UnitActor target)
        {
            var forward = ResolveForward(target);
            owner.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        private Vector3 ResolveTargetHitPoint(UnitActor target)
        {
            return target == null
                ? owner.transform.position
                : target.AnimationDriver?.HitCenter?.position ?? target.transform.position;
        }
    }
}
