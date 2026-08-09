using System.Collections.Generic;
using ProjectMT.Shared.Unit;

namespace ProjectMT.Shared.Combat
{
    public sealed class MeleeAttackExecutor : IMonsterActionExecutor // 근거리 단일·범위 공용 실행기
    {
        private readonly List<UnitActor> targets = new List<UnitActor>();

        public bool Execute(MonsterActionExecutionContext context)
        {
            var action = context.AssetSet?.CombatProfile?.Action as MeleeActionDefinition;
            if (action == null || context.World == null || context.Source == null ||
                context.PrimaryTarget == null || !context.PrimaryTarget.IsAlive)
            {
                return false;
            }

            if (action.Mode == MonsterMeleeAttackMode.Single)
            {
                return context.World.ApplyMonsterDamage(
                    context.Source,
                    context.PrimaryTarget,
                    context.Damage);
            }

            var center = action.AreaCenter == MonsterMeleeAreaCenter.Source
                ? context.Source.transform.position
                : context.PrimaryTarget.Position;
            var opponentTeam = context.Source.Team == UnitTeam.Player ? UnitTeam.Enemy : UnitTeam.Player;
            context.World.CollectUnits(opponentTeam, center, action.AreaRadius, action.MaxTargets, targets);
            var hitAny = false;
            for (var index = 0; index < targets.Count; index++)
            {
                hitAny |= context.World.ApplyMonsterDamage(
                    context.Source,
                    targets[index].Health,
                    context.Damage);
            }

            var primaryComponent = context.PrimaryTarget as UnityEngine.Component;
            var primaryActor = primaryComponent != null ? primaryComponent.GetComponent<UnitActor>() : null;
            if (primaryActor == null && targets.Count < action.MaxTargets)
            {
                hitAny |= context.World.ApplyMonsterDamage(
                    context.Source,
                    context.PrimaryTarget,
                    context.Damage);
            }

            return hitAny;
        }
    }
}
