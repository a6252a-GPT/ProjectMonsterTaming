using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using AP = ProjectMT.Features.CommanderSkill.CommanderSkillAwakeningParameter;

namespace ProjectMT.Features.CommanderSkill.Editor
{
    public static class CommanderSkillAwakeningAuthoring
    {
        public static CommanderSkillAwakeningStage[] CreateStages(CommanderSkillDefinition skill)
        {
            var stages = new CommanderSkillAwakeningStage[5];
            var damage = skill.Effects.OfType<CommanderAreaDamageEffectDefinition>().FirstOrDefault();
            var mark = skill.Effects.OfType<CommanderMarkEffectDefinition>().FirstOrDefault();
            for (var star = 1; star <= 5; star++)
            {
                var values = new List<CommanderSkillAwakeningModifier>();
                Mul("", AP.Cooldown, star == 5 && skill.SkillId == "CS_HeartOfBattlefield" ? 0.85f : star >= 4 ? 0.9f : 0.97f);
                if (star >= 2) Mul("", AP.TargetRange, 1.1f);
                if (star >= 3)
                {
                    switch (skill.SkillId)
                    {
                        case "CS_TrackingBlade": Add("", AP.RepeatCount, 1); if (star == 5) Add(mark.EffectId, AP.MarkRequiredHits, -1); break;
                        case "CS_DoomSpear": Mul(damage.EffectId, AP.AreaRadius, 1.15f); if (star == 5) Add(mark.EffectId, AP.MarkRequiredHits, -1); break;
                        case "CS_AbyssChain": Add("", AP.ChainCount, 1); if (star == 5) Mul("", AP.ChainRadius, 1.2f); break;
                        case "CS_PhantomCharge": Mul(damage.EffectId, AP.LineWidth, 1.15f); if (star == 5) Add("", AP.RepeatCount, 1); break;
                        case "CS_ConquerorSigil":
                            Mul("", AP.Duration, 1.2f);
                            if (star == 5) foreach (var effect in skill.Effects.OfType<CommanderUnitEffectDefinition>())
                                Mul(effect.EffectId, AP.AreaRadius, 1.2f);
                            break;
                        case "CS_PhantomBarrage": Add("", AP.RepeatCount, 1); if (star == 5) Add(mark.EffectId, AP.MarkRequiredStacks, -1); break;
                        case "CS_DeathSentence":
                            var recorded = mark.EffectsOnTrigger.OfType<CommanderRecordedHitDamageEffectDefinition>().Single();
                            Add(mark.EffectId + "/" + recorded.EffectId, AP.RecordedHitCountCap, star == 5 ? 10 : 5);
                            break;
                        case "CS_RuptureMarch": Add("", AP.RepeatCount, 1); if (star == 5) Mul(mark.EffectId, AP.MarkTriggerCooldown, 0.8f); break;
                        case "CS_HeartOfBattlefield":
                            foreach (var effect in skill.Effects.OfType<CommanderUnitEffectDefinition>()) Mul(effect.EffectId, AP.AreaRadius, 1.15f);
                            break;
                        case "CS_MarchOfDead": Mul(damage.EffectId, AP.LineWidth, 1.15f); if (star == 5) Add(mark.EffectId, AP.MarkRequiredStacks, -1); break;
                        case "CS_WarGodBrand":
                            Mul(mark.EffectId, AP.Duration, 1.2f);
                            foreach (var effect in skill.Effects.OfType<CommanderUnitEffectDefinition>()) Mul(effect.EffectId, AP.Duration, 1.2f);
                            if (star == 5) Mul(mark.EffectId, AP.MarkTriggerCooldown, 0.8f);
                            break;
                        case "CS_ApocalypseWar":
                            Mul(damage.EffectId, AP.AreaRadius, 1.15f);
                            if (star == 5) Mul(skill.Effects.OfType<CommanderGlobalModifierEffectDefinition>().Single().EffectId, AP.Duration, 1.2f);
                            break;
                        default: throw new InvalidOperationException("승인된 RC12 스킬이 아닙니다: " + skill.SkillId);
                    }
                }
                stages[star - 1] = new CommanderSkillAwakeningStage(values.ToArray());
                void Mul(string id, AP key, float amount) => values.Add(new CommanderSkillAwakeningModifier(id, key, amount));
                void Add(string id, AP key, float amount) => values.Add(new CommanderSkillAwakeningModifier(id, key, amount, CommanderSkillAwakeningOperation.Add));
            }
            return stages;
        }
    }
}
