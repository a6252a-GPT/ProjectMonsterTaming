using ProjectMT.Shared.Unit;
using UnityEngine;
using AP = ProjectMT.Features.CommanderSkill.CommanderSkillAwakeningParameter;

namespace ProjectMT.Features.CommanderSkill
{
    internal readonly struct CommanderSkillExecutionContext
    {
        public CommanderSkillExecutionContext(
            CommanderSkillRuntime owner,
            ICommanderSkillCombatGateway combat,
            ICommanderSkillFeedbackGateway feedback,
            Transform castOrigin,
            CommanderSkillGrowthSnapshot effectMultiplier)
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
        public CommanderSkillGrowthSnapshot EffectMultiplier { get; }
    }

    internal readonly struct CommanderSkillImpactContext // 기본공격과 같은 시전자→대상→판정 좌표 계약
    {
        public CommanderSkillImpactContext(
            Vector3 castOrigin,
            UnitActor primaryTarget,
            Vector3 position,
            Vector3 forward,
            ProjectMT.Shared.Combat.CombatDamageOrigin damageOrigin = ProjectMT.Shared.Combat.CombatDamageOrigin.CommanderSkill,
            int recordedHitCount = 0,
            float recordedDamage = 0f)
        {
            CastOrigin = castOrigin;
            PrimaryTarget = primaryTarget;
            Position = position;
            forward.y = 0f;
            Forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            DamageOrigin = damageOrigin;
            RecordedHitCount = Mathf.Max(0, recordedHitCount);
            RecordedDamage = Mathf.Max(0f, recordedDamage);
        }

        public Vector3 CastOrigin { get; }
        public UnitActor PrimaryTarget { get; }
        public Vector3 Position { get; }
        public Vector3 Forward { get; }
        public ProjectMT.Shared.Combat.CombatDamageOrigin DamageOrigin { get; }
        public int RecordedHitCount { get; }
        public float RecordedDamage { get; }
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
            return context.Owner.TryStartPattern(attack, context.EffectMultiplier);
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

            return context.Owner.TryStartPattern(effectSkill, context.EffectMultiplier);
        }
    }

    internal interface ICommanderSkillEffectHandler // 효과 종류별 공용 전투 API 변환 경계
    {
        bool Supports(CommanderSkillEffectDefinition effect);
        int Apply(
            CommanderSkillDefinition definition,
            CommanderSkillEffectDefinition effect,
            CommanderSkillImpactContext impact,
            CommanderSkillGrowthSnapshot multiplier);
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
            CommanderSkillGrowthSnapshot multiplier)
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
                    multiplier.Resolve(AP.TargetRange, definition.TargetRange),
                    multiplier.Resolve(AP.AreaRadius, damage.Radius, damage.EffectId),
                    damage.ForwardOffset,
                    damage.Angle,
                    multiplier.Resolve(AP.LineWidth, damage.LineWidth, damage.EffectId),
                    multiplier.ResolveCount(AP.MaxTargets, damage.MaxTargets, damage.EffectId),
                    damage.BaseDamage * damage.PerHitMultiplier * multiplier.DamageMultiplier,
                    impact.DamageOrigin));
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
            CommanderSkillGrowthSnapshot multiplier)
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

    internal sealed class CommanderMarkEffectHandler : ICommanderSkillEffectHandler
    {
        private readonly CommanderSkillRuntime owner;
        public CommanderMarkEffectHandler(CommanderSkillRuntime runtime) { owner = runtime; }
        public bool Supports(CommanderSkillEffectDefinition effect) => effect is CommanderMarkEffectDefinition;
        public int Apply(CommanderSkillDefinition definition, CommanderSkillEffectDefinition effect,
            CommanderSkillImpactContext impact, CommanderSkillGrowthSnapshot multiplier)
        {
            return effect is CommanderMarkEffectDefinition mark
                ? owner.ApplyCommanderMark(definition, mark, impact, multiplier)
                : 0;
        }
    }

    internal sealed class CommanderPullEffectHandler : ICommanderSkillEffectHandler
    {
        private readonly CommanderSkillRuntime owner;
        public CommanderPullEffectHandler(CommanderSkillRuntime runtime) => owner = runtime;
        public bool Supports(CommanderSkillEffectDefinition effect) => effect is CommanderPullEffectDefinition;
        public int Apply(CommanderSkillDefinition definition, CommanderSkillEffectDefinition effect,
            CommanderSkillImpactContext impact, CommanderSkillGrowthSnapshot multiplier) =>
            effect is CommanderPullEffectDefinition pull && definition.Category == CommanderSkillCategory.Attack
                ? owner.ApplyPull(pull, impact, multiplier) : 0;
    }

    internal sealed class CommanderRecordedHitDamageEffectHandler : ICommanderSkillEffectHandler
    {
        private readonly ICommanderSkillCombatGateway combat;
        public CommanderRecordedHitDamageEffectHandler(ICommanderSkillCombatGateway combatGateway) { combat = combatGateway; }
        public bool Supports(CommanderSkillEffectDefinition effect) => effect is CommanderRecordedHitDamageEffectDefinition;

        public int Apply(CommanderSkillDefinition definition, CommanderSkillEffectDefinition effect,
            CommanderSkillImpactContext impact, CommanderSkillGrowthSnapshot multiplier)
        {
            if (combat == null || definition == null || impact.PrimaryTarget == null ||
                effect is not CommanderRecordedHitDamageEffectDefinition recorded ||
                !definition.TryGetEffect<CommanderAreaDamageEffectDefinition>(out var sourceDamage)) return 0;
            return combat.ApplyAreaDamage(new CommanderSkillDamageRequest(
                definition.SkillId,
                sourceDamage.DamageKind,
                MonsterBasicAttackShape.Single,
                MonsterBasicAttackCenter.PrimaryTarget,
                impact.CastOrigin,
                impact.PrimaryTarget,
                impact.Position,
                impact.Forward,
                definition.TargetRange,
                0.1f,
                0f,
                90f,
                0.05f,
                1,
                sourceDamage.BaseDamage * (recorded.BaseMultiplier + Mathf.Min(impact.RecordedHitCount,
                    multiplier.ResolveCount(AP.RecordedHitCountCap, recorded.MaximumRecordedHits, recorded.EffectId)) *
                    recorded.MultiplierPerRecordedHit) * multiplier.DamageMultiplier,
                ProjectMT.Shared.Combat.CombatDamageOrigin.CommanderMarkTrigger));
        }
    }

    internal sealed class CommanderSkillEffectRunner // 등록된 Handler로 효과 SO 실행
    {
        private readonly CommanderSkillCombatGateway combat;
        private readonly ICommanderSkillEffectHandler[] handlers;

        public CommanderSkillEffectRunner(CommanderSkillCombatGateway combatGateway, params ICommanderSkillEffectHandler[] effectHandlers)
        {
            combat = combatGateway;
            handlers = effectHandlers ?? System.Array.Empty<ICommanderSkillEffectHandler>();
        }

        public int Apply(
            CommanderSkillDefinition definition,
            CommanderSkillImpactContext impact,
            CommanderSkillGrowthSnapshot multiplier)
        {
            return Apply(definition, definition?.Effects, impact, multiplier);
        }

        public int Apply(CommanderSkillDefinition definition,
            System.Collections.Generic.IReadOnlyList<CommanderSkillEffectDefinition> effects,
            CommanderSkillImpactContext impact, CommanderSkillGrowthSnapshot multiplier)
        {
            if (definition == null || effects == null)
            {
                return 0;
            }

            combat.BeginImpact();
            try
            {
                var appliedCount = 0;
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
            finally { combat.EndImpact(); }
        }
    }
}
