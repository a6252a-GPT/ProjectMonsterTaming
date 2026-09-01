using System.Collections;
using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public sealed class MonsterBasicAttackExecutor : IMonsterActionExecutor // 15종 Profile을 조립 실행하는 단일 경계
    {
        private readonly List<UnitActor> targets = new List<UnitActor>();

        public bool Execute(MonsterActionExecutionContext context)
        {
            var action = context.AssetSet?.CombatProfile?.Action;
            var profile = context.ResolvedAttackBlock;
            if (profile == null || context.World == null || context.Source == null ||
                context.PrimaryTarget == null || !context.PrimaryTarget.IsAlive)
            {
                return false;
            }

            var origin = ResolveOrigin(context);
            var primaryPosition = context.PrimaryTarget.Position;
            var forward = ResolveForward(context.Source.transform.forward, origin, primaryPosition);
            context.World.ShowMonsterBasicAttackArea(
                profile,
                context.Source,
                origin,
                forward,
                primaryPosition,
                context.ResolvedAttackRange);

            if (profile.MovementModule == MonsterBasicAttackMovementModule.Dash)
            {
                var primaryActor = ResolveActor(context.PrimaryTarget);
                var stopDistance = context.Source.BodyRadius + (primaryActor?.BodyRadius ?? 0.25f);
                if (profile.VfxSlots.Count > 0)
                {
                    MonsterBasicAttackVfxRuntime.Dispatch(
                        MonsterBasicAttackVfxEvent.DashExit,
                        CreateVfxContext(
                            context,
                            profile,
                            origin,
                            primaryPosition,
                            primaryPosition,
                            Quaternion.LookRotation(forward, Vector3.up)));
                }
                context.Source.AdvanceForBasicAttack(
                    primaryPosition,
                    profile.DashDistance,
                    stopDistance,
                    profile.DashDuration / context.PlaybackSpeed);
                origin = context.Source.transform.position;
                forward = ResolveForward(forward, origin, primaryPosition);
                if (profile.VfxSlots.Count > 0)
                {
                    MonsterBasicAttackVfxRuntime.Dispatch(
                        MonsterBasicAttackVfxEvent.DashEnter,
                        CreateVfxContext(
                            context,
                            profile,
                            origin,
                            primaryPosition,
                            primaryPosition,
                            Quaternion.LookRotation(forward, Vector3.up)));
                }
            }

            var attackRotation = Quaternion.LookRotation(forward, Vector3.up);
            if (profile.VfxSlots.Count > 0)
            {
                MonsterBasicAttackVfxRuntime.Dispatch(
                    MonsterBasicAttackVfxEvent.RecipeExecute,
                    CreateVfxContext(context, profile, origin, primaryPosition, primaryPosition, attackRotation));
            }
            else
            {
                context.World.PlayMonsterFeedbackAt(
                    profile.LaunchFeedback,
                    origin,
                    attackRotation,
                    context.AssetSet?.BodyProfile?.VfxScale ?? 1f);
            }
            context.World.PlayBasicAttackFeelAt(
                profile.LaunchFeel,
                origin,
                attackRotation,
                context.AssetSet?.BodyProfile?.VfxScale ?? 1f,
                ResolveUnitFeelTarget(context.Source),
                ResolveFeelIntensity(context));

            if (profile.UsesProjectileVisual)
            {
                return LaunchProjectiles(
                    context,
                    profile,
                    context.AttackBlock == null ? action as ProjectileActionDefinition : null,
                    origin,
                    forward);
            }

            if (profile.Progression != MonsterBasicAttackProgression.Simultaneous &&
                profile.SequenceModule == MonsterBasicAttackSequenceModule.Single)
            {
                context.World.StartCoroutine(ContinueProgressiveHit(context, profile, origin, forward));
                return true;
            }

            if (profile.Shape == MonsterBasicAttackShape.Fan &&
                profile.SweepDirection != MonsterBasicAttackSweepDirection.Simultaneous &&
                profile.SequenceModule == MonsterBasicAttackSequenceModule.Single)
            {
                context.World.StartCoroutine(ContinueFanSweep(context, profile, origin, forward));
                return true;
            }

            if (profile.SequenceModule == MonsterBasicAttackSequenceModule.Burst)
            {
                var firstApplied = ApplyInstantHit(context, profile, origin, forward, 0);
                context.World.StartCoroutine(ContinueRepeatedHit(context, profile, origin, forward));
                return firstApplied;
            }

            return ApplyInstantHit(context, profile, origin, forward, 0);
        }

        private IEnumerator ContinueProgressiveHit(
            MonsterActionExecutionContext context,
            MonsterBasicAttackProfile profile,
            Vector3 origin,
            Vector3 forward)
        {
            CollectTargets(context, profile, origin, forward);
            var ordered = new List<UnitActor>(targets);
            var primaryActor = ResolveActor(context.PrimaryTarget);
            if (primaryActor != null && !ordered.Contains(primaryActor))
            {
                ordered.Insert(0, primaryActor);
            }
            if (ordered.Count == 0)
            {
                ApplyInstantHit(context, profile, origin, forward, 0);
                yield break;
            }

            var center = profile.Center switch
            {
                MonsterBasicAttackCenter.Source => origin,
                MonsterBasicAttackCenter.Forward => origin + forward * profile.ForwardOffset,
                _ => context.PrimaryTarget.Position
            };
            var side = Vector3.Cross(Vector3.up, forward);
            float Axis(UnitActor actor)
            {
                var offset = actor.transform.position - center;
                offset.y = 0f;
                return profile.Progression switch
                {
                    MonsterBasicAttackProgression.Forward => Vector3.Dot(offset, forward),
                    MonsterBasicAttackProgression.LeftToRight => Vector3.Dot(offset, side),
                    MonsterBasicAttackProgression.RightToLeft => -Vector3.Dot(offset, side),
                    MonsterBasicAttackProgression.Outward => offset.magnitude,
                    _ => 0f
                };
            }
            ordered.Sort((left, right) => Axis(left).CompareTo(Axis(right)));
            var minimum = Axis(ordered[0]);
            var maximum = Axis(ordered[ordered.Count - 1]);
            var elapsedRatio = 0f;
            var applied = false;
            var feelPlayed = false;
            var feelIntensity = ResolveFeelIntensity(context);
            for (var index = 0; index < ordered.Count; index++)
            {
                var target = ordered[index];
                if (target == null || !target.IsAlive) continue;
                var ratio = maximum > minimum
                    ? Mathf.InverseLerp(minimum, maximum, Axis(target))
                    : 0f;
                var wait = Mathf.Max(0f, ratio - elapsedRatio) *
                           profile.ProgressionDuration / context.PlaybackSpeed;
                if (wait > 0f)
                {
                    yield return WaitForCombatSeconds(context.World, wait);
                }
                elapsedRatio = ratio;
                var isPrimary = target == primaryActor;
                var hitPoint = ResolveHitPoint(target);
                var feelTarget = ResolveUnitFeelTarget(target);
                var ownsMotion = !feelPlayed &&
                    context.World.WillPlayBasicAttackFeelTargetMotion(
                        profile.ImpactFeel,
                        feelTarget,
                        feelIntensity);
                var targetApplied = ApplyDamage(
                    context,
                    target.Health,
                    context.Damage * (isPrimary ? 1f : profile.SecondaryDamageRatio),
                    ResolveDamageFeedbackFlags(ownsMotion, true, false),
                    hitPoint);
                applied |= targetApplied;
                if (!targetApplied) continue;
                MonsterBasicAttackVfxRuntime.Dispatch(
                    MonsterBasicAttackVfxEvent.TargetDamaged,
                    CreateVfxContext(
                        context,
                        profile,
                        origin,
                        hitPoint,
                        center,
                        Quaternion.LookRotation(forward, Vector3.up),
                        target: target.Health));
                if (!feelPlayed && profile.ImpactFeel?.HasFeel == true)
                {
                    context.World.PlayBasicAttackFeelAt(
                        profile.ImpactFeel,
                        hitPoint,
                        Quaternion.LookRotation(forward, Vector3.up),
                        context.AssetSet?.BodyProfile?.VfxScale ?? 1f,
                        feelTarget,
                        feelIntensity);
                    feelPlayed = true;
                }
            }

            if (!applied) yield break;
            if (profile.Shape == MonsterBasicAttackShape.Circle)
            {
                MonsterBasicAttackVfxRuntime.Dispatch(
                    MonsterBasicAttackVfxEvent.AreaResolved,
                    CreateVfxContext(
                        context,
                        profile,
                        origin,
                        center + Vector3.up * 0.4f,
                        center,
                        Quaternion.LookRotation(forward, Vector3.up)));
            }
            MonsterBasicAttackVfxRuntime.Dispatch(
                MonsterBasicAttackVfxEvent.SequenceEnd,
                CreateVfxContext(
                    context,
                    profile,
                    origin,
                    ResolveHitPoint(context.PrimaryTarget),
                    center,
                    Quaternion.LookRotation(forward, Vector3.up)));
        }

        private bool LaunchProjectiles(
            MonsterActionExecutionContext context,
            MonsterBasicAttackProfile profile,
            ProjectileActionDefinition action,
            Vector3 origin,
            Vector3 forward)
        {
            var usesPresentationContract = profile.VfxSlots.Count > 0;
            var presentation = usesPresentationContract ? null : profile.ProjectileFeedback;
            var hasDeliveryVisual = MonsterBasicAttackVfxResolver.TryResolveDeliveryVisual(
                profile,
                context.ResolvedAttackBlockBindings,
                context.MotionIdOverride ?? context.AnimationDriver?.CurrentMotionId,
                out _,
                out var deliveryBinding);
            var projectilePrefab = hasDeliveryVisual
                ? deliveryBinding.Prefab
                : presentation?.VfxPrefab != null
                    ? presentation.VfxPrefab
                    : profile.ProjectileCarrierPrefab != null
                        ? profile.ProjectileCarrierPrefab
                        : action?.ProjectilePrefab;
            if (projectilePrefab == null)
            {
                return ApplyInstantHit(context, profile, origin, forward, 0);
            }
            var hideCarrierVisuals = usesPresentationContract && !hasDeliveryVisual;

            var feedback = ResolveImpactFeedback(context, profile);
            var vfxScale = context.AssetSet?.BodyProfile?.VfxScale ?? 1f;
            var volley = new MonsterBasicAttackProjectileVolley();
            var spawned = false;
            var projectileCount = profile.ProjectileCount;
            for (var index = 0; index < projectileCount; index++)
            {
                var direction = profile.ResolveProjectileDirection(forward, index);
                var rotation = Quaternion.LookRotation(direction, Vector3.up);
                var spawnPosition = origin;
                if (hasDeliveryVisual)
                {
                    spawnPosition += rotation * deliveryBinding.LocalPosition;
                    rotation *= deliveryBinding.LocalRotation;
                }
                else if (presentation != null)
                {
                    spawnPosition += rotation * presentation.LocalPosition;
                    rotation *= presentation.LocalRotation;
                }
                var instance = context.World.RentMonsterObject(
                    projectilePrefab,
                    spawnPosition,
                    rotation);
                if (instance == null)
                {
                    continue;
                }

                var legacy = instance.GetComponent<MonsterProjectileActor>();
                if (legacy != null)
                {
                    legacy.enabled = false;
                }

                var projectile = instance.GetComponent<MonsterBasicAttackProjectileActor>() ??
                                 instance.AddComponent<MonsterBasicAttackProjectileActor>();
                if (hasDeliveryVisual)
                {
                    MonsterBasicAttackVfxPlayback.ApplyInstanceScale(
                        instance,
                        projectilePrefab.transform.localScale *
                        deliveryBinding.Scale * Mathf.Max(0.01f, vfxScale));
                    MonsterBasicAttackVfxPlayback.RestartAtOffset(
                        instance,
                        deliveryBinding.PlaybackOffset,
                        playbackSpeed: deliveryBinding.PlaybackSpeed * context.PlaybackSpeed);
                }
                else if (presentation != null)
                {
                    instance.transform.localScale = projectilePrefab.transform.localScale *
                        presentation.Scale * Mathf.Max(0.01f, vfxScale);
                }
                projectile.enabled = true;
                projectile.Launch(
                    context.World,
                    context.Source,
                    context.PrimaryTarget,
                    action,
                    profile,
                    context.Damage,
                    context.ResolvedAttackRange,
                    origin,
                    ResolveHitPoint(context.PrimaryTarget),
                    direction,
                    volley,
                    feedback,
                    vfxScale,
                    context.ResolvedAttackBlockBindings,
                    context.MotionIdOverride,
                    context.SequenceIdOverride,
                    context.PlaybackSpeed,
                    context.ApplyAsSkillDamage,
                    context.HitCallback,
                    hideCarrierVisuals);
                MonsterBasicAttackVfxRuntime.Dispatch(
                    MonsterBasicAttackVfxEvent.DeliverySpawn,
                    CreateVfxContext(
                        context,
                        profile,
                        origin,
                        ResolveHitPoint(context.PrimaryTarget),
                        context.PrimaryTarget.Position,
                        rotation,
                        projectile.transform));
                spawned = true;
            }

            if (spawned)
            {
                if (!usesPresentationContract)
                {
                    var presentationSfx = presentation?.Sfx;
                    context.World.PlayMonsterSfx(
                        presentationSfx ?? (profile.LaunchFeedback?.Sfx == null ? action?.LaunchSfx : null),
                        origin);
                }
                return true;
            }

            return ApplyInstantHit(context, profile, origin, forward, 0);
        }

        private IEnumerator ContinueRepeatedHit(
            MonsterActionExecutionContext context,
            MonsterBasicAttackProfile profile,
            Vector3 origin,
            Vector3 forward)
        {
            var motionBreathDuration = context.AnimationDriver?.CurrentBreathDuration ?? 0f;
            var repeatInterval = profile.ResolveRepeatHitInterval(motionBreathDuration) /
                                 context.PlaybackSpeed;
            for (var hitIndex = 1; hitIndex < profile.HitCount; hitIndex++)
            {
                yield return WaitForCombatSeconds(context.World, repeatInterval);
                if (context.Source == null || !context.Source.IsAlive ||
                    context.PrimaryTarget == null || !context.PrimaryTarget.IsAlive)
                {
                    yield break;
                }

                ApplyInstantHit(context, profile, origin, forward, hitIndex, profile.RepeatImpactFeedback);
            }
        }

        private IEnumerator ContinueFanSweep(
            MonsterActionExecutionContext context,
            MonsterBasicAttackProfile profile,
            Vector3 origin,
            Vector3 forward)
        {
            const int sliceCount = 3;
            var hitActors = new HashSet<int>();
            var sliceTargets = new List<UnitActor>();
            var primaryActor = ResolveActor(context.PrimaryTarget);
            var opponentTeam = context.Source.Team == UnitTeam.Player ? UnitTeam.Enemy : UnitTeam.Player;
            var sliceAngle = profile.Angle / sliceCount + 2f;
            var firstYaw = -profile.Angle * 0.5f + sliceAngle * 0.5f;
            var lastYaw = profile.Angle * 0.5f - sliceAngle * 0.5f;
            var delay = Mathf.Clamp(profile.HitAreaVisibleDuration / (sliceCount + 2f), 0.025f, 0.08f) /
                        context.PlaybackSpeed;
            var applied = false;
            var appliedCount = 0;
            var feelPlayed = false;
            var feelIntensity = ResolveFeelIntensity(context);

            for (var sliceIndex = 0; sliceIndex < sliceCount; sliceIndex++)
            {
                if (sliceIndex > 0)
                {
                    yield return WaitForCombatSeconds(context.World, delay);
                }
                if (context.Source == null || !context.Source.IsAlive)
                {
                    yield break;
                }

                var ratio = sliceCount <= 1 ? 0f : sliceIndex / (float)(sliceCount - 1);
                if (profile.SweepDirection == MonsterBasicAttackSweepDirection.RightToLeft)
                {
                    ratio = 1f - ratio;
                }
                var sliceForward = Quaternion.Euler(0f, Mathf.Lerp(firstYaw, lastYaw, ratio), 0f) * forward;
                context.World.CollectUnitsInFan(
                    opponentTeam,
                    origin,
                    sliceForward,
                    profile.ResolveRange(context.ResolvedAttackRange),
                    sliceAngle,
                    profile.MaxTargets,
                    sliceTargets);

                foreach (var target in sliceTargets)
                {
                    if (target == null || appliedCount >= profile.MaxTargets || !hitActors.Add(target.GetInstanceID()))
                    {
                        continue;
                    }
                    var targetRatio = target == primaryActor ? 1f : profile.SecondaryDamageRatio;
                    var feelTarget = ResolveUnitFeelTarget(target);
                    var feelOwnsTargetMotion = !feelPlayed &&
                        context.World.WillPlayBasicAttackFeelTargetMotion(
                            profile.ImpactFeel,
                            feelTarget,
                            feelIntensity);
                    var hitPoint = ResolveHitPoint(target);
                    var targetApplied = ApplyDamage(
                        context,
                        target.Health,
                        context.Damage * targetRatio,
                        ResolveDamageFeedbackFlags(
                            feelOwnsTargetMotion,
                            true,
                            profile.HitCount > 1),
                        hitPoint);
                    if (targetApplied)
                    {
                        MonsterBasicAttackVfxRuntime.Dispatch(
                            MonsterBasicAttackVfxEvent.TargetDamaged,
                            CreateVfxContext(
                                context,
                                profile,
                                origin,
                                hitPoint,
                                context.PrimaryTarget.Position,
                                Quaternion.LookRotation(forward, Vector3.up),
                                target: target.Health));
                    }
                    applied |= targetApplied;
                    if (targetApplied && !feelPlayed && profile.ImpactFeel?.HasFeel == true)
                    {
                        context.World.PlayBasicAttackFeelAt(
                            profile.ImpactFeel,
                            ResolveHitPoint(target),
                            Quaternion.LookRotation(forward, Vector3.up),
                            context.AssetSet?.BodyProfile?.VfxScale ?? 1f,
                            feelTarget,
                            feelIntensity);
                        feelPlayed = true;
                    }
                    appliedCount++;
                }
            }

            if (!applied && primaryActor == null && context.PrimaryTarget != null && context.PrimaryTarget.IsAlive)
            {
                applied = ApplyDamage(
                    context,
                    context.PrimaryTarget,
                    context.Damage,
                    DamageFeedbackFlags.None,
                    ResolveHitPoint(context.PrimaryTarget));
                if (applied)
                {
                    MonsterBasicAttackVfxRuntime.Dispatch(
                        MonsterBasicAttackVfxEvent.TargetDamaged,
                        CreateVfxContext(
                            context,
                            profile,
                            origin,
                            ResolveHitPoint(context.PrimaryTarget),
                            context.PrimaryTarget.Position,
                            Quaternion.LookRotation(forward, Vector3.up)));
                }
            }
            if (applied)
            {
                if (!feelPlayed)
                {
                    context.World.PlayBasicAttackFeelAt(
                        profile.ImpactFeel,
                        ResolveHitPoint(context.PrimaryTarget),
                        Quaternion.LookRotation(forward, Vector3.up),
                        context.AssetSet?.BodyProfile?.VfxScale ?? 1f,
                        ResolveTargetGameObject(context.PrimaryTarget),
                        feelIntensity);
                }
                context.World.PlayMonsterFeedbackAt(
                    ResolveImpactFeedback(context, profile),
                    ResolveHitPoint(context.PrimaryTarget),
                    Quaternion.LookRotation(forward, Vector3.up),
                    context.AssetSet?.BodyProfile?.VfxScale ?? 1f);
            }
        }

        private static IEnumerator WaitForCombatSeconds(CombatWorld world, float duration)
        {
            var elapsed = 0f;
            var required = Mathf.Max(0f, duration);
            while (elapsed < required)
            {
                // 연속된 두 대기가 같은 저프레임 Delta를 중복 소비해 여러 타격을
                // 한 프레임에 접는 일을 막고, 각 간격은 실제 다음 프레임부터 센다.
                yield return null;
                if (world == null)
                {
                    yield break;
                }

                if (!world.IsPaused)
                {
                    elapsed += Time.deltaTime;
                }
            }
        }

        private bool ApplyInstantHit(
            MonsterActionExecutionContext context,
            MonsterBasicAttackProfile profile,
            Vector3 origin,
            Vector3 forward,
            int hitIndex,
            bool playActionFeedback = true)
        {
            CollectTargets(context, profile, origin, forward);
            var primaryActor = ResolveActor(context.PrimaryTarget);
            if (primaryActor != null)
            {
                var primaryIndex = targets.IndexOf(primaryActor);
                if (primaryIndex >= 0)
                {
                    targets.RemoveAt(primaryIndex);
                }
                targets.Insert(0, primaryActor);
                if (targets.Count > profile.MaxTargets)
                {
                    targets.RemoveAt(targets.Count - 1);
                }
            }

            var ratio = profile.ResolveDamageRatio(hitIndex);
            var applied = false;
            var feelTarget = ResolveTargetGameObject(context.PrimaryTarget);
            var feelIntensity = ResolveFeelIntensity(context);
            var feelOwnsTargetMotion = playActionFeedback &&
                context.World.WillPlayBasicAttackFeelTargetMotion(
                    profile.ImpactFeel,
                    feelTarget,
                    feelIntensity);
            for (var index = 0; index < targets.Count; index++)
            {
                var target = targets[index];
                if (target == null)
                {
                    continue;
                }

                var targetRatio = target == primaryActor ? 1f : profile.SecondaryDamageRatio;
                var hitPoint = ResolveHitPoint(target);
                var targetApplied = ApplyDamage(
                    context,
                    target.Health,
                    context.Damage * ratio * targetRatio,
                    ResolveDamageFeedbackFlags(
                        feelOwnsTargetMotion,
                        target == primaryActor,
                        profile.HitCount > 1),
                    hitPoint);
                applied |= targetApplied;
                if (targetApplied)
                {
                    MonsterBasicAttackVfxRuntime.Dispatch(
                        MonsterBasicAttackVfxEvent.TargetDamaged,
                        CreateVfxContext(
                            context,
                            profile,
                            origin,
                            hitPoint,
                            context.PrimaryTarget.Position,
                            Quaternion.LookRotation(forward, Vector3.up),
                            damageStage: hitIndex,
                            target: target.Health));
                }
            }

            if (primaryActor == null && context.PrimaryTarget.IsAlive)
            {
                var primaryHitPoint = ResolveHitPoint(context.PrimaryTarget);
                var primaryApplied = ApplyDamage(
                    context,
                    context.PrimaryTarget,
                    context.Damage * ratio,
                    ResolveDamageFeedbackFlags(false, false, profile.HitCount > 1),
                    primaryHitPoint);
                applied |= primaryApplied;
                if (primaryApplied)
                {
                    MonsterBasicAttackVfxRuntime.Dispatch(
                        MonsterBasicAttackVfxEvent.TargetDamaged,
                        CreateVfxContext(
                            context,
                            profile,
                            origin,
                            primaryHitPoint,
                            context.PrimaryTarget.Position,
                            Quaternion.LookRotation(forward, Vector3.up),
                            damageStage: hitIndex));
                }
            }

            if (applied && playActionFeedback)
            {
                var feedbackPosition = profile.Shape == MonsterBasicAttackShape.Circle
                    ? profile.Center switch
                    {
                        MonsterBasicAttackCenter.Source => origin + Vector3.up * 0.4f,
                        MonsterBasicAttackCenter.Forward =>
                            origin + forward * profile.ForwardOffset + Vector3.up * 0.4f,
                        _ => ResolveHitPoint(context.PrimaryTarget)
                    }
                    : ResolveHitPoint(context.PrimaryTarget);
                context.World.PlayMonsterFeedbackAt(
                    ResolveImpactFeedback(context, profile),
                    feedbackPosition,
                    Quaternion.LookRotation(forward, Vector3.up),
                    context.AssetSet?.BodyProfile?.VfxScale ?? 1f);
                context.World.PlayBasicAttackFeelAt(
                    profile.ImpactFeel,
                    feedbackPosition,
                    Quaternion.LookRotation(forward, Vector3.up),
                    context.AssetSet?.BodyProfile?.VfxScale ?? 1f,
                    feelTarget,
                    feelIntensity);
            }

            if (applied && profile.Shape == MonsterBasicAttackShape.Circle)
            {
                var areaCenter = profile.Center switch
                {
                    MonsterBasicAttackCenter.Source => origin,
                    MonsterBasicAttackCenter.Forward => origin + forward * profile.ForwardOffset,
                    _ => context.PrimaryTarget.Position
                };
                MonsterBasicAttackVfxRuntime.Dispatch(
                    MonsterBasicAttackVfxEvent.AreaResolved,
                    CreateVfxContext(
                        context,
                        profile,
                        origin,
                        areaCenter + Vector3.up * 0.4f,
                        areaCenter,
                        Quaternion.LookRotation(forward, Vector3.up),
                        damageStage: hitIndex));
            }
            if (applied && hitIndex == profile.HitCount - 1)
            {
                MonsterBasicAttackVfxRuntime.Dispatch(
                    MonsterBasicAttackVfxEvent.SequenceEnd,
                    CreateVfxContext(
                        context,
                        profile,
                        origin,
                        ResolveHitPoint(context.PrimaryTarget),
                        context.PrimaryTarget.Position,
                        Quaternion.LookRotation(forward, Vector3.up),
                        damageStage: hitIndex));
            }

            return applied;
        }

        private void CollectTargets(
            MonsterActionExecutionContext context,
            MonsterBasicAttackProfile profile,
            Vector3 origin,
            Vector3 forward)
        {
            var opponentTeam = context.Source.Team == UnitTeam.Player ? UnitTeam.Enemy : UnitTeam.Player;
            switch (profile.Shape)
            {
                case MonsterBasicAttackShape.Fan:
                    context.World.CollectUnitsInFan(
                        opponentTeam,
                        origin,
                        forward,
                        profile.ResolveRange(context.ResolvedAttackRange),
                        profile.Angle,
                        profile.MaxTargets,
                        targets);
                    break;
                case MonsterBasicAttackShape.Line:
                    context.World.CollectUnitsInLine(
                        opponentTeam,
                        origin,
                        forward,
                        profile.ResolveRange(context.ResolvedAttackRange),
                        profile.LineWidth,
                        profile.MaxTargets,
                        targets);
                    break;
                case MonsterBasicAttackShape.Circle:
                    var center = profile.Center switch
                    {
                        MonsterBasicAttackCenter.Source => origin,
                        MonsterBasicAttackCenter.Forward => origin + forward * profile.ForwardOffset,
                        _ => context.PrimaryTarget.Position
                    };
                    context.World.CollectUnits(
                        opponentTeam,
                        center,
                        profile.Radius,
                        profile.MaxTargets,
                        targets);
                    break;
                default:
                    targets.Clear();
                    break;
            }
        }

        private static Vector3 ResolveOrigin(MonsterActionExecutionContext context)
        {
            var socket = context.AnimationDriver?.ResolveSocket(context.Marker?.SocketOverride);
            return socket != null
                ? socket.position
                : context.Source.transform.position;
        }

        private static Vector3 ResolveForward(Vector3 fallback, Vector3 origin, Vector3 target)
        {
            var forward = target - origin;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = fallback;
                forward.y = 0f;
            }
            return forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
        }

        private static UnitActor ResolveActor(IDamageable target)
        {
            var component = target as Component;
            return component != null ? component.GetComponent<UnitActor>() : null;
        }

        private static Vector3 ResolveHitPoint(UnitActor target)
        {
            if (target == null) return Vector3.zero;
            return target.AnimationDriver?.HitCenter?.position ??
                   target.transform.position + Vector3.up * 0.4f;
        }

        private static Vector3 ResolveHitPoint(IDamageable target)
        {
            if (target == null) return Vector3.zero;
            var actor = ResolveActor(target);
            return actor != null
                ? ResolveHitPoint(actor)
                : target.Position + Vector3.up * 0.4f;
        }

        private static GameObject ResolveTargetGameObject(IDamageable target)
        {
            var actor = ResolveActor(target);
            return actor != null ? ResolveUnitFeelTarget(actor) : (target as Component)?.gameObject;
        }

        private static GameObject ResolveUnitFeelTarget(UnitActor actor)
        {
            if (actor == null)
            {
                return null;
            }

            var visual = actor.transform.Find("Visual") ?? actor.transform.Find("VisualRoot");
            return visual != null ? visual.gameObject : actor.gameObject;
        }

        private static float ResolveFeelIntensity(MonsterActionExecutionContext context)
        {
            return context.AssetSet?.CombatProfile?.ImpactStrength switch
            {
                MonsterImpactStrength.Light => 0.62f,
                MonsterImpactStrength.Heavy => 1.45f,
                _ => 1f
            };
        }

        private static DamageFeedbackFlags ResolveDamageFeedbackFlags(
            bool feelOwnsTargetMotion,
            bool isFeelTarget,
            bool separateFloatingNumber)
        {
            var flags = isFeelTarget && feelOwnsTargetMotion
                ? DamageFeedbackFlags.BasicAttackFeelTargetMotion
                : DamageFeedbackFlags.None;
            if (separateFloatingNumber)
            {
                flags |= DamageFeedbackFlags.SeparateFloatingNumber;
            }
            return flags;
        }

        private static MonsterBasicAttackVfxContext CreateVfxContext(
            MonsterActionExecutionContext context,
            MonsterBasicAttackProfile profile,
            Vector3 origin,
            Vector3 hitPoint,
            Vector3 areaCenter,
            Quaternion rotation,
            Transform projectile = null,
            int damageStage = 0,
            IDamageable target = null)
        {
            return new MonsterBasicAttackVfxContext(
                context.World,
                profile,
                context.AssetSet?.FeedbackProfile,
                context.Source,
                target ?? context.PrimaryTarget,
                context.AnimationDriver,
                projectile,
                context.Marker?.SocketOverride,
                origin,
                hitPoint,
                areaCenter,
                rotation,
                damageStage,
                context.ResolvedAttackBlockBindings,
                context.MotionIdOverride,
                context.SequenceIdOverride,
                context.PlaybackSpeed);
        }

        private static bool ApplyDamage(
            MonsterActionExecutionContext context,
            IDamageable target,
            float amount,
            DamageFeedbackFlags flags,
            Vector3 hitPoint)
        {
            var applied = context.ApplyAsSkillDamage
                ? context.World.ApplyMonsterSkillDamage(context.Source, target, amount, flags)
                : context.World.ApplyMonsterDamage(context.Source, target, amount, flags);
            if (!applied || context.HitCallback == null)
            {
                return applied;
            }
            var actor = ResolveActor(target);
            if (actor != null)
            {
                context.HitCallback(actor, hitPoint);
            }
            return true;
        }

        private static MonsterFeedbackCue ResolveImpactFeedback(
            MonsterActionExecutionContext context,
            MonsterBasicAttackProfile profile)
        {
            if (profile?.VfxSlots.Count > 0)
            {
                return null;
            }
            if (context.Marker?.FeedbackOverride != null && context.Marker.FeedbackOverride.HasAnyFeedback)
            {
                return context.Marker.FeedbackOverride;
            }
            if (profile?.ImpactFeedback != null && profile.ImpactFeedback.HasAnyFeedback)
            {
                return profile.ImpactFeedback; // 이전 Recipe 연출 호환, 신규 제작은 Monster 전용 Marker 사용
            }
            return context.AssetSet?.FeedbackProfile?.AttackMarker;
        }
    }
}
