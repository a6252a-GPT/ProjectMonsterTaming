using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Features.CommanderSkill.Editor
{
    public static class CommanderSkillSupportAuthoring
    {
        public static bool IsSupportId(string id) => id is "CS_AbyssChain" or "CS_ConquerorSigil"
            or "CS_HeartOfBattlefield" or "CS_WarGodBrand";

        public static CommanderEffectSkillDefinition Create(CommanderSkillDefinition source,
            List<ScriptableObject> owned)
        {
            if (source == null || !IsSupportId(source.SkillId)) throw new ArgumentException("지원 전환 대상이 아닙니다.");
            var result = ScriptableObject.CreateInstance<CommanderEffectSkillDefinition>();
            owned.Add(result);
            result.name = source.name;
            var targeting = ScriptableObject.CreateInstance<CommanderSkillTargetingDefinition>();
            owned.Add(targeting);
            targeting.name = "__Targeting";
            var id = source.SkillId;
            var ally = id is "CS_ConquerorSigil" or "CS_HeartOfBattlefield";
            targeting.EditorConfigure(ally ? CommanderSkillTargetTeam.Ally : CommanderSkillTargetTeam.Enemy,
                id == "CS_HeartOfBattlefield" ? CommanderSkillTargetSelection.LowestHealth :
                id == "CS_ConquerorSigil" ? CommanderSkillTargetSelection.MostCrowded :
                id == "CS_WarGodBrand" ? CommanderSkillTargetSelection.Strongest : CommanderSkillTargetSelection.Nearest, 20f);
            var effects = new List<CommanderSkillEffectDefinition>();
            var pattern = new CommanderSkillPatternConfig();
            pattern.EditorConfigure(id == "CS_AbyssChain" ? CommanderSkillPatternType.Chain :
                id == "CS_ConquerorSigil" ? CommanderSkillPatternType.PersistentArea : CommanderSkillPatternType.Single,
                1, id == "CS_AbyssChain" ? 0.12f : 0f, id == "CS_ConquerorSigil" ? 6f : 1f, 1f, 0f, 4, 4.5f);
            switch (id)
            {
                case "CS_AbyssChain":
                    effects.Add(Unit("slow", CommanderSkillUnitEffectType.Slow, 0.2f, 3f));
                    effects.Add(Unit("defense_down", CommanderSkillUnitEffectType.DefenseDebuff, 0.15f, 3f));
                    break;
                case "CS_ConquerorSigil":
                    effects.Add(Unit("attack_up", CommanderSkillUnitEffectType.AttackBuff, 0.1f, 1.2f, 4.5f));
                    effects.Add(Unit("protection", CommanderSkillUnitEffectType.DamageReduction, 0.1f, 1.2f, 4.5f));
                    break;
                case "CS_HeartOfBattlefield":
                    effects.Add(Unit("heal", CommanderSkillUnitEffectType.Heal, 0.12f, 0f, 6.5f, CommanderSkillEffectValueSource.TargetMaxHealthRatio));
                    effects.Add(Unit("shield", CommanderSkillUnitEffectType.Shield, 0.15f, 5f, 6.5f, CommanderSkillEffectValueSource.TargetMaxHealthRatio));
                    break;
                case "CS_WarGodBrand":
                    effects.Add(Unit("exposure", CommanderSkillUnitEffectType.Mark, 0.15f, 10f));
                    var trigger = Unit("trigger_attack_down", CommanderSkillUnitEffectType.AttackDebuff, 0.1f, 2f);
                    var mark = ScriptableObject.CreateInstance<CommanderMarkEffectDefinition>();
                    owned.Add(mark);
                    mark.name = "__Effect_SupportMark";
                    mark.EditorConfigure(id + "_support_mark", "WarGodBrandSupport", 10f,
                        CommanderSkillEffectScope.PrimaryTarget, 0.1f, 1, CommanderMarkTriggerType.MarkTriggered,
                        1, 1, 1, false, true, 0.5f, new CommanderSkillEffectDefinition[] { trigger });
                    if (source.TryGetEffect<CommanderMarkEffectDefinition>(out var oldMark))
                        mark.EditorConfigureFeedback(Copy(oldMark.OnApply), Copy(oldMark.Loop), Copy(oldMark.OnStack),
                            Copy(oldMark.OnTrigger), Copy(oldMark.OnRemove));
                    effects.Add(mark);
                    break;
            }
            result.EditorConfigure(id, source.DisplayName, source.Description, source.Icon,
                ally ? CommanderSkillCategory.Buff : CommanderSkillCategory.Debuff,
                source.CastTime, source.Cooldown, targeting, effects.ToArray(),
                source.CastVfxPrefab, source.CastVfxLifetime, source.CastSfx,
                source.ImpactVfxPrefab, source.ImpactVfxLifetime, source.ImpactSfx);
            result.EditorConfigureV2(source.Rarity, pattern);
            result.EditorConfigureCastingFeedback(source.CastingVfxPrefab, source.CastingVfxLifetime,
                source.CastingSfx, source.CastingVfxLocalOffset, source.CastingVfxLocalEuler, source.CastingVfxScale);
            result.EditorConfigureFeedbackTransforms(source.CastVfxLocalOffset, source.CastVfxLocalEuler,
                source.CastVfxScale, source.ImpactVfxLocalOffset, source.ImpactVfxLocalEuler, source.ImpactVfxScale);
            result.EditorConfigurePersistentFeedback(source.PersistentVfxPrefab, source.PersistentVfxLocalOffset,
                source.PersistentVfxLocalEuler, source.PersistentVfxScale, source.PersistentVfxAnchor);
            result.EditorConfigureAutoUse(id == "CS_HeartOfBattlefield"
                ? CommanderSkillAutoUseCondition.AllyHealthBelow : CommanderSkillAutoUseCondition.Always, 0.85f);
            CloneOwnedReferences(source, owned);
            if (!result.TryValidate(out var error)) throw new InvalidOperationException(error);
            return result;

            CommanderUnitEffectDefinition Unit(string suffix, CommanderSkillUnitEffectType kind, float value,
                float duration, float radius = 0f, CommanderSkillEffectValueSource valueSource = CommanderSkillEffectValueSource.Flat)
            {
                var effect = ScriptableObject.CreateInstance<CommanderUnitEffectDefinition>();
                owned.Add(effect);
                effect.name = "__Effect_" + suffix;
                effect.EditorConfigure(id + "_" + suffix, kind, valueSource, value, duration,
                    radius > 0f ? CommanderSkillEffectScope.Area : CommanderSkillEffectScope.PrimaryTarget,
                    Mathf.Max(0.1f, radius), radius > 0f ? 5 : 1, MonsterBuffStackPolicy.RefreshDuration);
                return effect;
            }
        }

        private static CommanderMarkFeedbackSlot Copy(CommanderMarkFeedbackSlot slot) =>
            slot == null ? new CommanderMarkFeedbackSlot() : JsonUtility.FromJson<CommanderMarkFeedbackSlot>(JsonUtility.ToJson(slot));

        private static void CloneOwnedReferences(CommanderSkillDefinition source, List<ScriptableObject> owned)
        {
            var oldPath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(oldPath)) return;
            var copies = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            for (var index = 0; index < owned.Count; index++)
            {
                using var serialized = new SerializedObject(owned[index]);
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var value = property.objectReferenceValue;
                    if (value == null || AssetDatabase.GetAssetPath(value) != oldPath) continue;
                    if (value is not ScriptableObject subasset || value == source)
                        throw new InvalidOperationException("지원 스킬 이관 중 지원하지 않는 내부 참조입니다.");
                    if (!copies.TryGetValue(value, out var copy))
                    {
                        copy = UnityEngine.Object.Instantiate(subasset);
                        copy.name = value.name;
                        copies.Add(value, copy);
                        owned.Add((ScriptableObject)copy);
                    }
                    property.objectReferenceValue = copy;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
