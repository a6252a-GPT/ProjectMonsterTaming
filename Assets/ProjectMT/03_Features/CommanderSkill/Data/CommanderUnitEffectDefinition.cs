using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    public enum CommanderSkillUnitEffectType
    {
        Heal,
        Shield,
        AttackBuff,
        DefenseBuff,
        AttackSpeedBuff,
        DamageReduction,
        DamageReflect,
        Cleanse,
        EnergyGain,
        AttackDebuff,
        DefenseDebuff,
        AttackSpeedDebuff,
        MoveSpeedDebuff,
        Slow,
        Stun,
        Mark,
        EnergyDrain
    }

    public enum CommanderSkillEffectScope
    {
        PrimaryTarget,
        Area
    }

    public enum CommanderSkillEffectValueSource
    {
        Flat,
        TargetMaxHealthRatio,
        TargetMissingHealthRatio,
        TargetEnergyCapacityRatio
    }

    [CreateAssetMenu(
        menuName = "ProjectMT/Commander Skill/Effects/Unit Effect",
        fileName = "CSEffect_Unit")]
    public sealed class CommanderUnitEffectDefinition : CommanderSkillEffectDefinition // 효과형 액티브의 군단장 전용 효과 블록
    {
        [SerializeField] private CommanderSkillUnitEffectType effectType;
        [SerializeField] private CommanderSkillEffectValueSource valueSource;
        [SerializeField, Min(0f)] private float magnitude = 0.2f;
        [SerializeField, Min(0f)] private float duration = 5f;
        [SerializeField] private CommanderSkillEffectScope scope;
        [SerializeField, Min(0.1f)] private float radius = 4f;
        [SerializeField, Min(1)] private int maxTargets = 8;
        [SerializeField] private MonsterBuffStackPolicy stackPolicy = MonsterBuffStackPolicy.RefreshDuration;

        public CommanderSkillUnitEffectType EffectType => effectType;
        public CommanderSkillEffectValueSource ValueSource => valueSource;
        public float Magnitude => Mathf.Max(0f, magnitude);
        public float Duration => Mathf.Max(0f, duration);
        public CommanderSkillEffectScope Scope => scope;
        public float Radius => Mathf.Max(0.1f, radius);
        public int MaxTargets => Mathf.Max(1, maxTargets);
        public MonsterBuffStackPolicy StackPolicy => stackPolicy;

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error))
            {
                return false;
            }

            if (!System.Enum.IsDefined(typeof(CommanderSkillUnitEffectType), effectType) ||
                !System.Enum.IsDefined(typeof(CommanderSkillEffectValueSource), valueSource) ||
                !System.Enum.IsDefined(typeof(CommanderSkillEffectScope), scope) ||
                !System.Enum.IsDefined(typeof(MonsterBuffStackPolicy), stackPolicy) ||
                magnitude < 0f || float.IsNaN(magnitude) || float.IsInfinity(magnitude) ||
                duration < 0f || float.IsNaN(duration) || float.IsInfinity(duration) ||
                radius < 0.1f || float.IsNaN(radius) || float.IsInfinity(radius) ||
                maxTargets < 1)
            {
                error = $"{EffectId}: unit effect values are invalid.";
                return false;
            }

            if (RequiresMagnitude(effectType) && magnitude <= 0f)
            {
                error = $"{EffectId}: this effect requires a positive magnitude.";
                return false;
            }

            if (UsesRatioMagnitude(effectType, valueSource) && magnitude > 1f)
            {
                error = $"{EffectId}: ratio magnitude must be between 0 and 1.";
                return false;
            }

            if (!IsValueSourceCompatible(effectType, valueSource))
            {
                error = $"{EffectId}: value source is not compatible with {effectType}.";
                return false;
            }

            if (RequiresDuration(effectType) && duration <= 0f)
            {
                error = $"{EffectId}: this effect requires a positive duration.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool IsCompatible(CommanderSkillCategory category, CommanderSkillUnitEffectType type)
        {
            return category switch
            {
                CommanderSkillCategory.Buff => type is CommanderSkillUnitEffectType.Heal or
                    CommanderSkillUnitEffectType.Shield or CommanderSkillUnitEffectType.AttackBuff or
                    CommanderSkillUnitEffectType.DefenseBuff or CommanderSkillUnitEffectType.AttackSpeedBuff or
                    CommanderSkillUnitEffectType.DamageReduction or CommanderSkillUnitEffectType.DamageReflect or
                    CommanderSkillUnitEffectType.Cleanse or CommanderSkillUnitEffectType.EnergyGain,
                CommanderSkillCategory.Debuff => type is CommanderSkillUnitEffectType.AttackDebuff or
                    CommanderSkillUnitEffectType.DefenseDebuff or
                    CommanderSkillUnitEffectType.AttackSpeedDebuff or
                    CommanderSkillUnitEffectType.MoveSpeedDebuff or CommanderSkillUnitEffectType.Slow or
                    CommanderSkillUnitEffectType.Stun or CommanderSkillUnitEffectType.Mark or
                    CommanderSkillUnitEffectType.EnergyDrain,
                _ => false
            };
        }

        public static bool RequiresDuration(CommanderSkillUnitEffectType type)
        {
            return type is CommanderSkillUnitEffectType.Shield or CommanderSkillUnitEffectType.AttackBuff or
                CommanderSkillUnitEffectType.DefenseBuff or CommanderSkillUnitEffectType.AttackSpeedBuff or
                CommanderSkillUnitEffectType.DamageReduction or CommanderSkillUnitEffectType.DamageReflect or
                CommanderSkillUnitEffectType.AttackDebuff or CommanderSkillUnitEffectType.DefenseDebuff or
                CommanderSkillUnitEffectType.AttackSpeedDebuff or CommanderSkillUnitEffectType.MoveSpeedDebuff or
                CommanderSkillUnitEffectType.Slow or CommanderSkillUnitEffectType.Stun or
                CommanderSkillUnitEffectType.Mark;
        }

        public static bool IsValueSourceCompatible(
            CommanderSkillUnitEffectType type,
            CommanderSkillEffectValueSource source)
        {
            return type switch
            {
                CommanderSkillUnitEffectType.Heal => source is CommanderSkillEffectValueSource.Flat or
                    CommanderSkillEffectValueSource.TargetMaxHealthRatio or
                    CommanderSkillEffectValueSource.TargetMissingHealthRatio,
                CommanderSkillUnitEffectType.Shield => source is CommanderSkillEffectValueSource.Flat or
                    CommanderSkillEffectValueSource.TargetMaxHealthRatio,
                CommanderSkillUnitEffectType.EnergyGain or CommanderSkillUnitEffectType.EnergyDrain =>
                    source is CommanderSkillEffectValueSource.Flat or
                        CommanderSkillEffectValueSource.TargetEnergyCapacityRatio,
                _ => source == CommanderSkillEffectValueSource.Flat
            };
        }

        public static bool UsesRatioMagnitude(
            CommanderSkillUnitEffectType type,
            CommanderSkillEffectValueSource source)
        {
            if (source != CommanderSkillEffectValueSource.Flat)
            {
                return true;
            }

            return type is CommanderSkillUnitEffectType.AttackBuff or
                CommanderSkillUnitEffectType.DefenseBuff or
                CommanderSkillUnitEffectType.AttackSpeedBuff or
                CommanderSkillUnitEffectType.DamageReduction or
                CommanderSkillUnitEffectType.DamageReflect or
                CommanderSkillUnitEffectType.AttackDebuff or
                CommanderSkillUnitEffectType.DefenseDebuff or
                CommanderSkillUnitEffectType.AttackSpeedDebuff or
                CommanderSkillUnitEffectType.MoveSpeedDebuff or
                CommanderSkillUnitEffectType.Slow or
                CommanderSkillUnitEffectType.Mark;
        }

        private static bool RequiresMagnitude(CommanderSkillUnitEffectType type)
        {
            return type is not CommanderSkillUnitEffectType.Cleanse and not CommanderSkillUnitEffectType.Stun;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            CommanderSkillUnitEffectType type,
            CommanderSkillEffectValueSource source,
            float value,
            float effectDuration,
            CommanderSkillEffectScope targetScope,
            float effectRadius,
            int targetCount,
            MonsterBuffStackPolicy policy)
        {
            EditorConfigureId(id);
            effectType = type;
            valueSource = source;
            magnitude = Mathf.Max(0f, value);
            duration = Mathf.Max(0f, effectDuration);
            scope = targetScope;
            radius = Mathf.Max(0.1f, effectRadius);
            maxTargets = Mathf.Max(1, targetCount);
            stackPolicy = policy;
        }
#endif
    }

}
