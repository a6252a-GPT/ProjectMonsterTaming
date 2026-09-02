using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    internal readonly struct CommanderSkillExecutionContext
    {
        public CommanderSkillExecutionContext(
            CommanderSkillRuntime owner,
            ICommanderSkillCombatGateway combat,
            ICommanderSkillFeedbackGateway feedback,
            Transform castOrigin,
            float effectMultiplier)
        {
            Owner = owner;
            Combat = combat;
            Feedback = feedback;
            CastOrigin = castOrigin;
            EffectMultiplier = effectMultiplier;
        }

        public CommanderSkillRuntime Owner { get; }
        public ICommanderSkillCombatGateway Combat { get; }
        public ICommanderSkillFeedbackGateway Feedback { get; }
        public Transform CastOrigin { get; }
        public float EffectMultiplier { get; }
    }

    internal readonly struct CommanderSkillImpactContext // 기본공격과 같은 시전자→대상→판정 좌표 계약
    {
        public CommanderSkillImpactContext(
            Vector3 castOrigin,
            UnitActor primaryTarget,
            Vector3 position,
            Vector3 forward)
        {
            CastOrigin = castOrigin;
            PrimaryTarget = primaryTarget;
            Position = position;
            forward.y = 0f;
            Forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        public Vector3 CastOrigin { get; }
        public UnitActor PrimaryTarget { get; }
        public Vector3 Position { get; }
        public Vector3 Forward { get; }
    }

    internal interface ICommanderSkillExecutor // 분류별 실행기 등록 경계
    {
        bool Supports(CommanderSkillDefinition definition);
        bool TryExecute(CommanderSkillDefinition definition, CommanderSkillExecutionContext context);
    }

    internal sealed class CommanderAttackSkillExecutor : ICommanderSkillExecutor // 공격형 투사체 전달 전담
    {
        public bool Supports(CommanderSkillDefinition definition)
        {
            return definition is CommanderAttackSkillDefinition;
        }

        public bool TryExecute(CommanderSkillDefinition definition, CommanderSkillExecutionContext context)
        {
            if (definition is not CommanderAttackSkillDefinition attack ||
                context.Owner == null || context.Combat == null || context.Feedback == null ||
                context.CastOrigin == null || !context.Combat.IsReady)
            {
                return false;
            }

            var target = context.Combat.FindTarget(context.CastOrigin.position, attack.Targeting);
            if (target == null)
            {
                return false;
            }

            var start = context.CastOrigin.position + Vector3.up * 1.15f;
            var destination = target.transform.position + Vector3.up * 0.45f;
            var direction = destination - start;
            var rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : Quaternion.identity;

            if (attack.DeliveryModule == MonsterBasicAttackDeliveryModule.Direct)
            {
                context.Owner.PlayCastFeedback(attack, start, rotation);
                context.Owner.ResolveImpact(
                    attack,
                    new CommanderSkillImpactContext(start, target, destination, direction),
                    context.EffectMultiplier);
                return true;
            }

            var projectileObject = context.Feedback.Rent(attack.ProjectilePrefab, start, rotation);
            var projectile = projectileObject == null
                ? null
                : projectileObject.GetComponent<CommanderSkillProjectile>();
            if (projectile == null)
            {
                context.Feedback.Return(projectileObject);
                return false;
            }

            projectile.Launch(
                context.Owner,
                attack,
                target,
                destination,
                context.EffectMultiplier);
            context.Owner.PlayCastFeedback(attack, start, rotation);
            return true;
        }
    }

    internal sealed class CommanderEffectSkillExecutor : ICommanderSkillExecutor // 버프·디버프 즉시 전달 전담
    {
        public bool Supports(CommanderSkillDefinition definition)
        {
            return definition is CommanderEffectSkillDefinition;
        }

        public bool TryExecute(CommanderSkillDefinition definition, CommanderSkillExecutionContext context)
        {
            if (definition is not CommanderEffectSkillDefinition effectSkill ||
                context.Owner == null || context.Combat == null || context.Feedback == null ||
                context.CastOrigin == null || !context.Combat.IsReady)
            {
                return false;
            }

            var target = context.Combat.FindTarget(context.CastOrigin.position, effectSkill.Targeting);
            if (target == null)
            {
                return false;
            }

            var start = context.CastOrigin.position + Vector3.up * 1.15f;
            var destination = target.transform.position + Vector3.up * 0.45f;
            var direction = destination - start;
            var rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : Quaternion.identity;
            context.Owner.PlayCastFeedback(effectSkill, start, rotation);
            context.Owner.ResolveImpact(
                effectSkill,
                new CommanderSkillImpactContext(start, target, destination, direction),
                context.EffectMultiplier);
            return true;
        }
    }

    internal interface ICommanderSkillEffectHandler // 효과 종류별 공용 전투 API 변환 경계
    {
        bool Supports(CommanderSkillEffectDefinition effect);
        int Apply(
            CommanderSkillDefinition definition,
            CommanderSkillEffectDefinition effect,
            CommanderSkillImpactContext impact,
            float multiplier);
    }

    internal sealed class CommanderAreaDamageEffectHandler : ICommanderSkillEffectHandler
    {
        private readonly ICommanderSkillCombatGateway combat;

        public CommanderAreaDamageEffectHandler(ICommanderSkillCombatGateway combatGateway)
        {
            combat = combatGateway;
        }

        public bool Supports(CommanderSkillEffectDefinition effect)
        {
            return effect is CommanderAreaDamageEffectDefinition;
        }

        public int Apply(
            CommanderSkillDefinition definition,
            CommanderSkillEffectDefinition effect,
            CommanderSkillImpactContext impact,
            float multiplier)
        {
            if (combat == null || definition == null || effect is not CommanderAreaDamageEffectDefinition damage)
            {
                return 0;
            }

            return combat.ApplyAreaDamage(
                new CommanderSkillDamageRequest(
                    definition.SkillId,
                    damage.DamageKind,
                    damage.Shape,
                    damage.Center,
                    impact.CastOrigin,
                    impact.PrimaryTarget,
                    impact.Position,
                    impact.Forward,
                    definition.TargetRange,
                    damage.Radius,
                    damage.ForwardOffset,
                    damage.Angle,
                    damage.LineWidth,
                    damage.MaxTargets,
                    damage.BaseDamage * Mathf.Max(0f, multiplier)));
        }
    }

    internal sealed class CommanderUnitEffectHandler : ICommanderSkillEffectHandler
    {
        private readonly ICommanderSkillCombatGateway combat;

        public CommanderUnitEffectHandler(ICommanderSkillCombatGateway combatGateway)
        {
            combat = combatGateway;
        }

        public bool Supports(CommanderSkillEffectDefinition effect)
        {
            return effect is CommanderUnitEffectDefinition;
        }

        public int Apply(
            CommanderSkillDefinition definition,
            CommanderSkillEffectDefinition effect,
            CommanderSkillImpactContext impact,
            float multiplier)
        {
            if (combat == null || definition?.Targeting == null ||
                effect is not CommanderUnitEffectDefinition unitEffect)
            {
                return 0;
            }

            return combat.ApplyUnitEffect(
                new CommanderSkillUnitEffectRequest(
                    definition.SkillId,
                    unitEffect,
                    definition.Targeting.TargetTeam,
                    impact.PrimaryTarget,
                    impact.Position,
                    multiplier));
        }
    }

    internal sealed class CommanderSkillEffectRunner // 등록된 Handler로 효과 SO 실행
    {
        private readonly ICommanderSkillEffectHandler[] handlers;

        public CommanderSkillEffectRunner(params ICommanderSkillEffectHandler[] effectHandlers)
        {
            handlers = effectHandlers ?? System.Array.Empty<ICommanderSkillEffectHandler>();
        }

        public int Apply(
            CommanderSkillDefinition definition,
            CommanderSkillImpactContext impact,
            float multiplier)
        {
            if (definition == null)
            {
                return 0;
            }

            var appliedCount = 0;
            var effects = definition.Effects;
            for (var index = 0; index < effects.Count; index++)
            {
                var effect = effects[index];
                for (var handlerIndex = 0; handlerIndex < handlers.Length; handlerIndex++)
                {
                    var handler = handlers[handlerIndex];
                    if (handler == null || !handler.Supports(effect))
                    {
                        continue;
                    }

                    appliedCount += handler.Apply(definition, effect, impact, multiplier);
                    break;
                }
            }

            return appliedCount;
        }
    }
}
