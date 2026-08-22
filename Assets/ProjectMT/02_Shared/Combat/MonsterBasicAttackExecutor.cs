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

            if (profile.Delivery == MonsterBasicAttackDelivery.Dash)
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

            if (profile.UsesProjectileVisual)
            {
                return LaunchProjectiles(context, profile, action as ProjectileActionDefinition, origin, forward);
            }

            if (profile.Delivery == MonsterBasicAttackDelivery.MultiHit ||
                profile.Delivery == MonsterBasicAttackDelivery.Breath)
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
            if (action?.ProjectilePrefab == null)
            {
                return ApplyInstantHit(context, profile, origin, forward, 0);
            }

            var feedback = ResolveImpactFeedback(context);
            var vfxScale = context.AssetSet.BodyProfile?.VfxScale ?? 1f;
            var volley = new MonsterBasicAttackProjectileVolley();
            var spawned = false;
            var projectileCount = profile.ProjectileCount;
            for (var index = 0; index < projectileCount; index++)
            {
                var ratio = projectileCount <= 1 ? 0f : index / (float)(projectileCount - 1) - 0.5f;
                var direction = Quaternion.Euler(0f, ratio * profile.ProjectileSpreadAngle, 0f) * forward;
                var instance = context.World.RentMonsterObject(
                    action.ProjectilePrefab,
                    origin,
                    Quaternion.LookRotation(direction, Vector3.up));
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
                context.World.PlayMonsterSfx(action.LaunchSfx, origin);
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
                yield return new WaitForSeconds(profile.RepeatHitInterval);
                if (context.Source == null || !context.Source.IsAlive ||
                    context.PrimaryTarget == null || !context.PrimaryTarget.IsAlive)
                {
                    yield break;
                }

                ApplyInstantHit(context, profile, origin, forward, hitIndex, profile.Delivery != MonsterBasicAttackDelivery.Breath);
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
                    context.Damage * ratio * targetRatio);
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
                    ResolveImpactFeedback(context),
                    feedbackPosition,
                    Quaternion.identity,
                    context.AssetSet.BodyProfile?.VfxScale ?? 1f);
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

        private static MonsterFeedbackCue ResolveImpactFeedback(MonsterActionExecutionContext context)
        {
            return context.Marker?.FeedbackOverride ?? context.AssetSet.FeedbackProfile?.AttackMarker;
        }
    }
}
