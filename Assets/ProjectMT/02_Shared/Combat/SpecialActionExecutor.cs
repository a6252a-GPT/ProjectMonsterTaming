using System.Collections.Generic;
using ProjectMT.Shared.Unit;

namespace ProjectMT.Shared.Combat
{
    public sealed class SpecialActionExecutor : IMonsterActionExecutor // 초기 범위 Buff 공용 실행기
    {
        private readonly List<UnitActor> targets = new List<UnitActor>();

        public bool Execute(MonsterActionExecutionContext context)
        {
            var action = context.AssetSet?.CombatProfile?.Action as SpecialActionDefinition;
            if (action == null || context.World == null || context.Source == null)
            {
                return false;
            }

            var targetTeam = action.TargetTeam == MonsterBuffTargetTeam.Allies
                ? context.Source.Team
                : context.Source.Team == UnitTeam.Player ? UnitTeam.Enemy : UnitTeam.Player;
            context.World.CollectUnits(
                targetTeam,
                context.Source.transform.position,
                action.Radius,
                action.MaxTargets,
                targets);

            for (var index = 0; index < targets.Count; index++)
            {
                targets[index].ApplyMonsterBuff(
                    action.EffectId,
                    action.Modifier * context.Source.SupportOutputMultiplier,
                    action.Duration,
                    action.StackPolicy);
            }

            return targets.Count > 0;
        }
    }
}
