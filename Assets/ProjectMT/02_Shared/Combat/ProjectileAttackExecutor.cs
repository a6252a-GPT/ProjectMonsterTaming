using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public sealed class ProjectileAttackExecutor : IMonsterActionExecutor // 원거리 세 방식 발사 실행기
    {
        public bool Execute(MonsterActionExecutionContext context)
        {
            var action = context.AssetSet?.CombatProfile?.Action as ProjectileActionDefinition;
            if (action == null || context.World == null || context.Source == null ||
                context.PrimaryTarget == null || !context.PrimaryTarget.IsAlive)
            {
                return false;
            }

            var origin = context.AnimationDriver != null
                ? context.AnimationDriver.ResolveSocket(context.Marker?.SocketOverride).position
                : context.Source.transform.position + Vector3.up * 0.45f;
            var targetPosition = context.PrimaryTarget.Position + Vector3.up * 0.4f;
            var rotation = targetPosition == origin
                ? context.Source.transform.rotation
                : Quaternion.LookRotation((targetPosition - origin).normalized, Vector3.up);
            var instance = context.World.RentMonsterObject(action.ProjectilePrefab, origin, rotation);
            var projectile = instance != null ? instance.GetComponent<MonsterProjectileActor>() : null;
            if (projectile == null)
            {
                if (instance != null)
                {
                    context.World.ReturnMonsterObject(instance);
                }

                return context.World.ApplyMonsterDamage(
                    context.Source,
                    context.PrimaryTarget,
                    context.Damage);
            }

            projectile.Launch(
                context.World,
                context.Source,
                context.PrimaryTarget,
                action,
                context.Damage,
                targetPosition);
            return true;
        }
    }
}
