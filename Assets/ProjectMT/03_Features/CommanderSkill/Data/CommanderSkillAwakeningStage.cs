using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Features.CommanderSkill
{
    public enum CommanderSkillAwakeningParameter
    {
        Cooldown, TargetRange, RepeatCount, ChainCount, ChainRadius, AreaRadius, LineWidth,
        MaxTargets, Duration, MarkRequiredHits, MarkRequiredStacks, MarkTriggerCooldown, RecordedHitCountCap
    }
    public enum CommanderSkillAwakeningOperation { Add, Multiply }

    [Serializable]
    public struct CommanderSkillAwakeningModifier
    {
        [SerializeField] private string targetEffectId;
        [SerializeField] private CommanderSkillAwakeningParameter parameter;
        [SerializeField] private CommanderSkillAwakeningOperation operation;
        [SerializeField] private float value;
        public string TargetEffectId => targetEffectId ?? string.Empty;
        public CommanderSkillAwakeningParameter Parameter => parameter;
        public CommanderSkillAwakeningOperation Operation => operation;
        public float Value => value;
        public CommanderSkillAwakeningModifier(string target, CommanderSkillAwakeningParameter key, float amount,
            CommanderSkillAwakeningOperation mode = CommanderSkillAwakeningOperation.Multiply)
        { targetEffectId = target; parameter = key; operation = mode; value = amount; }
    }

    [Serializable]
    public sealed class CommanderSkillAwakeningStage
    {
        [SerializeField] private CommanderSkillAwakeningModifier[] modifiers = Array.Empty<CommanderSkillAwakeningModifier>();
        public CommanderSkillAwakeningStage() { }
        public CommanderSkillAwakeningStage(params CommanderSkillAwakeningModifier[] values) =>
            modifiers = values == null ? Array.Empty<CommanderSkillAwakeningModifier>() : (CommanderSkillAwakeningModifier[])values.Clone();
        public CommanderSkillAwakeningModifier[] CopyModifiers() =>
            modifiers == null ? Array.Empty<CommanderSkillAwakeningModifier>() : (CommanderSkillAwakeningModifier[])modifiers.Clone();
    }

    public readonly struct CommanderSkillAwakeningSnapshot
    {
        private readonly CommanderSkillAwakeningModifier[] modifiers;
        private readonly string prefix;
        public CommanderSkillAwakeningSnapshot(CommanderSkillAwakeningStage stage)
        { modifiers = stage?.CopyModifiers(); prefix = string.Empty; }
        private CommanderSkillAwakeningSnapshot(CommanderSkillAwakeningModifier[] values, string root)
        { modifiers = values; prefix = root; }
        public CommanderSkillAwakeningSnapshot ForTrigger(string effectId) => new CommanderSkillAwakeningSnapshot(modifiers, effectId + "/");
        public float Resolve(CommanderSkillAwakeningParameter parameter, float original, string effectId = "")
        {
            var key = string.IsNullOrEmpty(effectId) ? string.Empty : prefix + effectId;
            if (modifiers == null) return original;
            foreach (var modifier in modifiers)
                if (modifier.Parameter == parameter && modifier.TargetEffectId == key)
                    return modifier.Operation == CommanderSkillAwakeningOperation.Add
                        ? original + modifier.Value : original * modifier.Value;
            return original;
        }
    }

    public static class CommanderSkillAwakeningValidation
    {
        public static bool TryValidate(CommanderSkillDefinition skill, out string error)
        {
            error = string.Empty;
            if (skill.AwakeningStages.Count == 0) return true;
            if (skill.AwakeningStages.Count != 5) { error = "각성 단계는 5개여야 합니다."; return false; }
            for (var star = 0; star < 5; star++)
            {
                var stage = skill.AwakeningStages[star];
                if (stage == null) { error = "각성 단계가 비어 있습니다."; return false; }
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var modifier in stage.CopyModifiers())
                {
                    if (!Enum.IsDefined(typeof(CommanderSkillAwakeningParameter), modifier.Parameter) ||
                        !Enum.IsDefined(typeof(CommanderSkillAwakeningOperation), modifier.Operation) ||
                        float.IsNaN(modifier.Value) || float.IsInfinity(modifier.Value) ||
                        !keys.Add(modifier.TargetEffectId + ":" + modifier.Parameter) ||
                        !TryGetBase(skill, modifier, out var original, out var limit))
                    { error = $"{star + 1}성: 각성 대상·파라미터가 없거나 중복/잘못된 값입니다."; return false; }
                    var value = modifier.Operation == CommanderSkillAwakeningOperation.Add
                        ? original + modifier.Value : original * modifier.Value;
                    var count = modifier.Parameter is CommanderSkillAwakeningParameter.RepeatCount or
                        CommanderSkillAwakeningParameter.ChainCount or CommanderSkillAwakeningParameter.MaxTargets or
                        CommanderSkillAwakeningParameter.MarkRequiredHits or CommanderSkillAwakeningParameter.MarkRequiredStacks or
                        CommanderSkillAwakeningParameter.RecordedHitCountCap;
                    var allowZero = modifier.Parameter is CommanderSkillAwakeningParameter.MarkTriggerCooldown or
                        CommanderSkillAwakeningParameter.RecordedHitCountCap;
                    if (float.IsNaN(value) || float.IsInfinity(value) || (allowZero ? value < 0f : value <= 0f) ||
                        value > limit || (count && Mathf.Abs(value - Mathf.Round(value)) > 0.0001f))
                    { error = $"{star + 1}성: 합성 결과가 유효 범위 또는 정수 계약을 벗어났습니다."; return false; }
                }
            }
            return true;
        }

        private static bool TryGetBase(CommanderSkillDefinition skill, CommanderSkillAwakeningModifier modifier,
            out float value, out float limit)
        {
            value = 0f; limit = float.MaxValue;
            var parameter = modifier.Parameter;
            if (string.IsNullOrEmpty(modifier.TargetEffectId))
            {
                switch (parameter)
                {
                    case CommanderSkillAwakeningParameter.Cooldown: value = skill.Cooldown; return true;
                    case CommanderSkillAwakeningParameter.TargetRange: value = skill.TargetRange; return true;
                    case CommanderSkillAwakeningParameter.RepeatCount:
                        value = skill.Pattern.RepeatCount; limit = 32;
                        return skill.Pattern.Type is CommanderSkillPatternType.Burst or CommanderSkillPatternType.Barrage or CommanderSkillPatternType.Pulse;
                    case CommanderSkillAwakeningParameter.ChainCount:
                        value = skill.Pattern.ChainCount; limit = 32; return skill.Pattern.Type == CommanderSkillPatternType.Chain;
                    case CommanderSkillAwakeningParameter.ChainRadius:
                        value = skill.Pattern.ChainRadius; return skill.Pattern.Type == CommanderSkillPatternType.Chain;
                    case CommanderSkillAwakeningParameter.Duration:
                        value = skill.Pattern.Duration; return skill.Pattern.Type == CommanderSkillPatternType.PersistentArea;
                    default: return false;
                }
            }
            var parts = modifier.TargetEffectId.Split('/');
            if (parts.Length > 2) return false;
            CommanderSkillEffectDefinition effect = null;
            foreach (var candidate in skill.Effects) if (candidate != null && candidate.EffectId == parts[0]) { effect = candidate; break; }
            if (parts.Length == 2)
            {
                if (effect is not CommanderMarkEffectDefinition root) return false;
                effect = null;
                foreach (var child in root.EffectsOnTrigger) if (child != null && child.EffectId == parts[1]) { effect = child; break; }
            }
            if (effect is CommanderAreaDamageEffectDefinition damage)
            {
                if (parameter == CommanderSkillAwakeningParameter.AreaRadius) { value = damage.Radius; return true; }
                if (parameter == CommanderSkillAwakeningParameter.LineWidth) { value = damage.LineWidth; return true; }
                if (parameter == CommanderSkillAwakeningParameter.MaxTargets) { value = damage.MaxTargets; limit = 64; return true; }
            }
            if (effect is CommanderUnitEffectDefinition unit)
            {
                if (parameter == CommanderSkillAwakeningParameter.AreaRadius) { value = unit.Radius; return unit.Scope == CommanderSkillEffectScope.Area; }
                if (parameter == CommanderSkillAwakeningParameter.MaxTargets) { value = unit.MaxTargets; limit = 64; return unit.Scope != CommanderSkillEffectScope.PrimaryTarget; }
                if (parameter == CommanderSkillAwakeningParameter.Duration) { value = unit.Duration; return value > 0; }
            }
            if (effect is CommanderMarkEffectDefinition mark)
            {
                if (parameter == CommanderSkillAwakeningParameter.Duration) { value = mark.Duration; return true; }
                if (parameter == CommanderSkillAwakeningParameter.MarkRequiredHits) { value = mark.RequiredHits; limit = int.MaxValue; return mark.TriggerType == CommanderMarkTriggerType.HitCount; }
                if (parameter == CommanderSkillAwakeningParameter.MarkRequiredStacks) { value = mark.RequiredStacks; limit = mark.MaxStacks; return mark.TriggerType == CommanderMarkTriggerType.StackReached; }
                if (parameter == CommanderSkillAwakeningParameter.MarkTriggerCooldown) { value = mark.TriggerCooldown; return true; }
            }
            if (effect is CommanderRecordedHitDamageEffectDefinition recorded && parameter == CommanderSkillAwakeningParameter.RecordedHitCountCap)
            { value = recorded.MaximumRecordedHits; limit = int.MaxValue; return true; }
            if (effect is CommanderGlobalModifierEffectDefinition global && parameter == CommanderSkillAwakeningParameter.Duration)
            { value = global.Duration; return true; }
            return false;
        }
    }
}
