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

    internal interface ICommanderSkillEffectHandler // 효과 종류별 공용 전투 API 변환 경계
    {
        bool Supports(CommanderSkillEffectDefinition effect);
        int Apply(
            CommanderSkillDefinition definition,
            CommanderSkillEffectDefinition effect,
            Vector3 position,
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
            Vector3 position,
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
                    position,
                    damage.BaseDamage * Mathf.Max(0f, multiplier),
                    damage.Radius,
                    damage.MaxTargets));
        }
    }

    internal sealed class CommanderSkillEffectRunner // 등록된 Handler로 효과 SO 실행
    {
        private readonly ICommanderSkillEffectHandler[] handlers;

        public CommanderSkillEffectRunner(params ICommanderSkillEffectHandler[] effectHandlers)
        {
            handlers = effectHandlers ?? System.Array.Empty<ICommanderSkillEffectHandler>();
        }

        public int Apply(CommanderSkillDefinition definition, Vector3 position, float multiplier)
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

                    appliedCount += handler.Apply(definition, effect, position, multiplier);
                    break;
                }
            }

            return appliedCount;
        }
    }
}
