using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public sealed class ProjectileAttackExecutor : IMonsterActionExecutor // 투사체·즉발 공용 원거리 실행기
    {
        private readonly List<UnitActor> nearbyTargets = new List<UnitActor>();

        public bool Execute(MonsterActionExecutionContext context)
        {
            var action = context.AssetSet?.CombatProfile?.Action as ProjectileActionDefinition;
            if (action == null || context.World == null || context.Source == null ||
                context.PrimaryTarget == null || !context.PrimaryTarget.IsAlive)
            {
                return false;
            }

            var impactFeedback = context.Marker?.FeedbackOverride ??
                                 context.AssetSet.FeedbackProfile?.AttackMarker;
            var vfxScale = context.AssetSet.BodyProfile?.VfxScale ?? 1f;
            if (action.DeliveryMode == MonsterRangedDeliveryMode.Instant)
            {
                return ExecuteInstant(context, action, impactFeedback, vfxScale);
            }

            var origin = context.AnimationDriver != null
                ? context.AnimationDriver.ResolveSocket(context.Marker?.SocketOverride).position
                : context.Source.transform.position + Vector3.up * 0.45f;
            var targetPosition = context.PrimaryTarget.Position + Vector3.up * 0.4f;
            var rotation = targetPosition == origin
                ? context.Source.transform.rotation
                : Quaternion.LookRotation((targetPosition - origin).normalized, Vector3.up);
            var instance = context.World.RentMonsterObject(action.ProjectilePrefab, origin, rotation);
            var projectile = instance != null
                ? instance.GetComponent<MonsterProjectileActor>() ?? instance.AddComponent<MonsterProjectileActor>()
                : null;
            if (projectile == null)
            {
                if (instance != null)
                {
                    context.World.ReturnMonsterObject(instance);
                }

                return ExecuteFallbackImpact(context, action, impactFeedback, vfxScale);
            }

            context.World.PlayMonsterSfx(action.LaunchSfx, origin);
            projectile.enabled = true;
            projectile.Launch(
                context.World,
                context.Source,
                context.PrimaryTarget,
                action,
                context.Damage,
                targetPosition,
                impactFeedback,
                vfxScale);
            return true;
        }

        private bool ExecuteInstant(
            MonsterActionExecutionContext context,
            ProjectileActionDefinition action,
            MonsterFeedbackCue impactFeedback,
            float vfxScale)
        {
            return action.Mode switch
            {
                MonsterProjectileAttackMode.Single => ApplyInstantSingle(context, impactFeedback, vfxScale),
                MonsterProjectileAttackMode.Area => ApplyInstantArea(context, action, impactFeedback, vfxScale),
                _ => false
            };
        }

        private bool ExecuteFallbackImpact(
            MonsterActionExecutionContext context,
            ProjectileActionDefinition action,
            MonsterFeedbackCue impactFeedback,
            float vfxScale)
        {
            return action.Mode == MonsterProjectileAttackMode.Area
                ? ApplyInstantArea(context, action, impactFeedback, vfxScale)
                : ApplyInstantSingle(context, impactFeedback, vfxScale);
        }

        private static bool ApplyInstantSingle(
            MonsterActionExecutionContext context,
            MonsterFeedbackCue impactFeedback,
            float vfxScale)
        {
            var impactPosition = context.PrimaryTarget.Position + Vector3.up * 0.4f;
            var applied = context.World.ApplyMonsterDamage(
                context.Source,
                context.PrimaryTarget,
                context.Damage);
            if (applied)
            {
                context.World.PlayMonsterFeedbackAt(
                    impactFeedback,
                    impactPosition,
                    Quaternion.identity,
                    vfxScale);
            }

            return applied;
        }

        private bool ApplyInstantArea(
            MonsterActionExecutionContext context,
            ProjectileActionDefinition action,
            MonsterFeedbackCue impactFeedback,
            float vfxScale)
        {
            var center = context.PrimaryTarget.Position + Vector3.up * 0.4f;
            var opponentTeam = context.Source.Team == UnitTeam.Player ? UnitTeam.Enemy : UnitTeam.Player;
            context.World.CollectUnits(
                opponentTeam,
                center,
                action.ImpactRadius,
                action.MaxImpactTargets,
                nearbyTargets);

            var applied = false;
            for (var index = 0; index < nearbyTargets.Count; index++)
            {
                applied |= context.World.ApplyMonsterDamage(
                    context.Source,
                    nearbyTargets[index].Health,
                    context.Damage);
            }

            var component = context.PrimaryTarget as Component;
            if (component != null && component.GetComponent<UnitActor>() == null &&
                nearbyTargets.Count < action.MaxImpactTargets)
            {
                applied |= context.World.ApplyMonsterDamage(
                    context.Source,
                    context.PrimaryTarget,
                    context.Damage);
            }

            if (applied)
            {
                context.World.PlayMonsterFeedbackAt(
                    impactFeedback,
                    center,
                    Quaternion.identity,
                    vfxScale);
            }

            return applied;
        }
    }
}
