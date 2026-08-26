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
            var profile = action?.BasicAttackProfile;
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
                context.Stats.attackRange);

            if (profile.MovementModule == MonsterBasicAttackMovementModule.Dash)
            {
                var primaryActor = ResolveActor(context.PrimaryTarget);
                var stopDistance = context.Source.BodyRadius + (primaryActor?.BodyRadius ?? 0.25f);
                context.Source.AdvanceForBasicAttack(
                    primaryPosition,
                    profile.DashDistance,
                    stopDistance,
                    profile.DashDuration);
                origin = context.Source.transform.position;
                forward = ResolveForward(forward, origin, primaryPosition);
            }

            var attackRotation = Quaternion.LookRotation(forward, Vector3.up);
            context.World.PlayMonsterFeedbackAt(
                profile.LaunchFeedback,
                origin,
                attackRotation,
                context.AssetSet.BodyProfile?.VfxScale ?? 1f);
            context.World.PlayBasicAttackFeelAt(
                profile.LaunchFeel,
                origin,
                attackRotation,
                context.AssetSet.BodyProfile?.VfxScale ?? 1f,
                ResolveUnitFeelTarget(context.Source),
                ResolveFeelIntensity(context));

            if (profile.UsesProjectileVisual)
            {
                return LaunchProjectiles(context, profile, action as ProjectileActionDefinition, origin, forward);
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

        private bool LaunchProjectiles(
            MonsterActionExecutionContext context,
            MonsterBasicAttackProfile profile,
            ProjectileActionDefinition action,
            Vector3 origin,
            Vector3 forward)
        {
            if (action == null)
            {
                return false;
            }

            var presentation = profile.ProjectileFeedback;
            var projectilePrefab = presentation?.VfxPrefab != null
                ? presentation.VfxPrefab
                : action.ProjectilePrefab;
            if (projectilePrefab == null)
            {
                return ApplyInstantHit(context, profile, origin, forward, 0);
            }

            var feedback = ResolveImpactFeedback(context, profile);
            var vfxScale = context.AssetSet.BodyProfile?.VfxScale ?? 1f;
            var volley = new MonsterBasicAttackProjectileVolley();
            var spawned = false;
            var projectileCount = profile.ProjectileCount;
            for (var index = 0; index < projectileCount; index++)
            {
                var ratio = projectileCount <= 1 ? 0f : index / (float)(projectileCount - 1) - 0.5f;
                var direction = Quaternion.Euler(0f, ratio * profile.ProjectileSpreadAngle, 0f) * forward;
                var rotation = Quaternion.LookRotation(direction, Vector3.up);
                var spawnPosition = origin;
                if (presentation != null)
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
                if (presentation != null)
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
                    context.Stats.attackRange,
                    origin,
                    context.PrimaryTarget.Position + Vector3.up * 0.4f,
                    direction,
                    volley,
                    feedback,
                    vfxScale);
                spawned = true;
            }

            if (spawned)
            {
                var presentationSfx = presentation?.Sfx;
                context.World.PlayMonsterSfx(
                    presentationSfx ?? (profile.LaunchFeedback?.Sfx == null ? action.LaunchSfx : null),
                    origin);
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
            for (var hitIndex = 1; hitIndex < profile.HitCount; hitIndex++)
            {
                yield return WaitForCombatSeconds(context.World, profile.RepeatHitInterval);
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
            var delay = Mathf.Clamp(profile.HitAreaVisibleDuration / (sliceCount + 2f), 0.025f, 0.08f);
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
                    profile.ResolveRange(context.Stats.attackRange),
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
                    var targetApplied = context.World.ApplyMonsterDamage(
                        context.Source,
                        target.Health,
                        context.Damage * targetRatio,
                        ResolveFeelTargetMotionFlags(feelOwnsTargetMotion, true));
                    applied |= targetApplied;
                    if (targetApplied && !feelPlayed && profile.ImpactFeel?.HasFeel == true)
                    {
                        context.World.PlayBasicAttackFeelAt(
                            profile.ImpactFeel,
                            target.transform.position + Vector3.up * 0.4f,
                            Quaternion.LookRotation(forward, Vector3.up),
                            context.AssetSet.BodyProfile?.VfxScale ?? 1f,
                            feelTarget,
                            feelIntensity);
                        feelPlayed = true;
                    }
                    appliedCount++;
                }
            }

            if (!applied && primaryActor == null && context.PrimaryTarget != null && context.PrimaryTarget.IsAlive)
            {
                applied = context.World.ApplyMonsterDamage(
                    context.Source,
                    context.PrimaryTarget,
                    context.Damage);
            }
            if (applied)
            {
                if (!feelPlayed)
                {
                    context.World.PlayBasicAttackFeelAt(
                        profile.ImpactFeel,
                        context.PrimaryTarget.Position + Vector3.up * 0.4f,
                        Quaternion.LookRotation(forward, Vector3.up),
                        context.AssetSet.BodyProfile?.VfxScale ?? 1f,
                        ResolveTargetGameObject(context.PrimaryTarget),
                        feelIntensity);
                }
                context.World.PlayMonsterFeedbackAt(
                    ResolveImpactFeedback(context, profile),
                    context.PrimaryTarget.Position + Vector3.up * 0.4f,
                    Quaternion.LookRotation(forward, Vector3.up),
                    context.AssetSet.BodyProfile?.VfxScale ?? 1f);
            }
        }

        private static IEnumerator WaitForCombatSeconds(CombatWorld world, float duration)
        {
            var elapsed = 0f;
            var required = Mathf.Max(0f, duration);
            while (elapsed < required)
            {
                if (world == null)
                {
                    yield break;
                }

                if (!world.IsPaused)
                {
                    elapsed += Time.deltaTime;
                    if (elapsed >= required)
                    {
                        yield break;
                    }
                }

                yield return null;
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
                applied |= context.World.ApplyMonsterDamage(
                    context.Source,
                    target.Health,
                    context.Damage * ratio * targetRatio,
                    ResolveFeelTargetMotionFlags(feelOwnsTargetMotion, target == primaryActor));
            }

            if (primaryActor == null && context.PrimaryTarget.IsAlive)
            {
                applied |= context.World.ApplyMonsterDamage(
                    context.Source,
                    context.PrimaryTarget,
                    context.Damage * ratio);
            }

            if (applied && playActionFeedback)
            {
                var feedbackPosition = profile.Shape == MonsterBasicAttackShape.Circle &&
                                       profile.Center == MonsterBasicAttackCenter.Source
                    ? origin + Vector3.up * 0.4f
                    : context.PrimaryTarget.Position + Vector3.up * 0.4f;
                context.World.PlayMonsterFeedbackAt(
                    ResolveImpactFeedback(context, profile),
                    feedbackPosition,
                    Quaternion.LookRotation(forward, Vector3.up),
                    context.AssetSet.BodyProfile?.VfxScale ?? 1f);
                context.World.PlayBasicAttackFeelAt(
                    profile.ImpactFeel,
                    feedbackPosition,
                    Quaternion.LookRotation(forward, Vector3.up),
                    context.AssetSet.BodyProfile?.VfxScale ?? 1f,
                    feelTarget,
                    feelIntensity);
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
                        profile.ResolveRange(context.Stats.attackRange),
                        profile.Angle,
                        profile.MaxTargets,
                        targets);
                    break;
                case MonsterBasicAttackShape.Line:
                    context.World.CollectUnitsInLine(
                        opponentTeam,
                        origin,
                        forward,
                        profile.ResolveRange(context.Stats.attackRange),
                        profile.LineWidth,
                        profile.MaxTargets,
                        targets);
                    break;
                case MonsterBasicAttackShape.Circle:
                    var center = profile.Center == MonsterBasicAttackCenter.Source
                        ? origin
                        : context.PrimaryTarget.Position;
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
                : context.Source.transform.position + Vector3.up * 0.4f;
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

        private static DamageFeedbackFlags ResolveFeelTargetMotionFlags(
            bool feelOwnsTargetMotion,
            bool isFeelTarget)
        {
            return isFeelTarget && feelOwnsTargetMotion
                ? DamageFeedbackFlags.BasicAttackFeelTargetMotion
                : DamageFeedbackFlags.None;
        }

        private static MonsterFeedbackCue ResolveImpactFeedback(
            MonsterActionExecutionContext context,
            MonsterBasicAttackProfile profile)
        {
            if (context.Marker?.FeedbackOverride != null && context.Marker.FeedbackOverride.HasAnyFeedback)
            {
                return context.Marker.FeedbackOverride;
            }
            if (profile?.ImpactFeedback != null && profile.ImpactFeedback.HasAnyFeedback)
            {
                return profile.ImpactFeedback; // 이전 Recipe 연출 호환, 신규 제작은 Monster 전용 Marker 사용
            }
            return context.AssetSet.FeedbackProfile?.AttackMarker;
        }
    }
}
