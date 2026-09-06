using ProjectMT.Shared.CommanderSkill;
using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    public readonly struct CommanderSkillGrowthSnapshot
    {
        public CommanderSkillGrowthSnapshot(float power, float ratio, float control, float externalDamage = 1f,
            CommanderSkillAwakeningSnapshot awakening = default, CommanderSkillCastState cast = null, int hitIndex = 0)
        {
            Power = Safe(power);
            Ratio = Safe(ratio);
            ControlDuration = Safe(control);
            ExternalDamage = Safe(externalDamage);
            Awakening = awakening;
            Cast = cast;
            HitIndex = hitIndex;
        }

        public float Power { get; }
        public float Ratio { get; }
        public float ControlDuration { get; }
        public float ExternalDamage { get; }
        public CommanderSkillAwakeningSnapshot Awakening { get; }
        internal CommanderSkillCastState Cast { get; }
        internal int HitIndex { get; }
        internal CommanderSkillGrowthSnapshot WithCast(CommanderSkillCastState cast) =>
            new CommanderSkillGrowthSnapshot(Power, Ratio, ControlDuration, ExternalDamage, Awakening, cast, HitIndex);
        internal CommanderSkillGrowthSnapshot ForHit(int index) =>
            new CommanderSkillGrowthSnapshot(Power, Ratio, ControlDuration, ExternalDamage, Awakening, Cast, index);
        public CommanderSkillGrowthSnapshot WithAwakening(CommanderSkillAwakeningSnapshot values) =>
            new CommanderSkillGrowthSnapshot(Power, Ratio, ControlDuration, ExternalDamage, values, Cast, HitIndex);
        public CommanderSkillGrowthSnapshot ForTrigger(string id) => WithAwakening(Awakening.ForTrigger(id));
        public float Resolve(CommanderSkillAwakeningParameter key, float value, string id = "") =>
            Awakening.Resolve(key, value, id);
        public int ResolveCount(CommanderSkillAwakeningParameter key, int value, string id = "") =>
            Mathf.RoundToInt(Resolve(key, value, id));
        public float DamageMultiplier => Power * ExternalDamage;
        public static CommanderSkillGrowthSnapshot FromRule(CommanderSkillGrowthRule rule, int level, float externalDamage = 1f)
            => new CommanderSkillGrowthSnapshot(rule?.GetDamageMultiplier(level) ?? 1f,
                rule?.GetRatioMultiplier(level) ?? 1f, rule?.GetControlDurationMultiplier(level) ?? 1f, externalDamage);
        public static implicit operator CommanderSkillGrowthSnapshot(float multiplier) =>
            new CommanderSkillGrowthSnapshot(multiplier, multiplier, 1f);
        public static CommanderSkillGrowthSnapshot operator *(CommanderSkillGrowthSnapshot snapshot, float damageFactor) =>
            new CommanderSkillGrowthSnapshot(snapshot.Power, snapshot.Ratio, snapshot.ControlDuration,
                snapshot.ExternalDamage * Safe(damageFactor), snapshot.Awakening, snapshot.Cast, snapshot.HitIndex);
        private static float Safe(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
    }

    public static class CommanderSkillValueResolver
    {
        public static float Magnitude(CommanderUnitEffectDefinition effect, CommanderSkillGrowthSnapshot growth)
        {
            if (effect == null) return 0f;
            if (effect.EffectType is CommanderSkillUnitEffectType.Stun or CommanderSkillUnitEffectType.Cleanse)
                return effect.Magnitude;
            var ratio = CommanderUnitEffectDefinition.UsesRatioMagnitude(effect.EffectType, effect.ValueSource);
            var value = effect.Magnitude * (ratio ? growth.Ratio : growth.Power);
            if (!ratio) return value;
            var cap = effect.EffectType switch
            {
                CommanderSkillUnitEffectType.Heal => 1f,
                CommanderSkillUnitEffectType.AttackBuff or CommanderSkillUnitEffectType.DefenseBuff => 1f,
                _ => 0.5f
            };
            return Mathf.Clamp(value, 0f, cap);
        }

        public static float Duration(CommanderUnitEffectDefinition effect, CommanderSkillGrowthSnapshot growth) =>
            effect.EffectType == CommanderSkillUnitEffectType.Stun
                ? Mathf.Min(3f, growth.Resolve(CommanderSkillAwakeningParameter.Duration, effect.Duration, effect.EffectId) * growth.ControlDuration) :
                growth.Resolve(CommanderSkillAwakeningParameter.Duration, effect.Duration, effect.EffectId);
    }
}
