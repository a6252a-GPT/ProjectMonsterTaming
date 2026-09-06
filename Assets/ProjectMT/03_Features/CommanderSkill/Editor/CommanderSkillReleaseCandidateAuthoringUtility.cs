using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Features.CommanderSkill.Editor
{
    public static class CommanderSkillReleaseCandidateAuthoringUtility
    {
        private const string Root = "Assets/ProjectMT/03_Features/CommanderSkill/Resources/CommanderSkills/ReleaseCandidate12";
        private const string MarkRoot = Root + "/Marks";
        private const string CatalogPath = "Assets/ProjectMT/03_Features/CommanderSkill/Resources/CommanderSkills/CommanderSkillCatalog.asset";
        private const string BalancePath = "Assets/ProjectMT/03_Features/CommanderSkill/Resources/CommanderSkills/Rules/CommanderSkillBalanceConfig.asset";
        private const float BaseDamage = 20f;

        [MenuItem("Tools/ProjectMT/Commander Skill/Build Release Candidate 12")]
        public static void Build()
        {
            // 모든 필수 의존성을 먼저 검증한다. 실패 시 기존 ReleaseCandidate 자산은 건드리지 않는다.
            var projectile = Load<GameObject>("Assets/ProjectMT/03_Features/CommanderSkill/Prefabs/PF_CS_FireballProjectile.prefab");
            var catalog = Load<CommanderSkillCatalog>(CatalogPath);
            var balance = Load<CommanderSkillBalanceConfig>(BalancePath);
            var castSfx = Load<SfxCue>("Assets/ProjectMT/03_Features/CommanderSkill/Resources/CommanderSkills/Audio/SFX_CS_Fireball_Cast.asset");
            var impactSfx = Load<SfxCue>("Assets/ProjectMT/03_Features/CommanderSkill/Resources/CommanderSkills/Audio/SFX_CS_Fireball_Impact.asset");
            var fallback = catalog.Skills.First(skill => skill != null);
            var icon = fallback.Icon;
            var markVfx = Load<GameObject>("Assets/ProjectMT/04_Contents/04_FallenCommander/Art/VFX/FallenCommanger_Skill_TrackingMark.prefab");
            var slashVfx = Load<GameObject>("Assets/ProjectMT/04_Contents/04_FallenCommander/Art/VFX/FallenCommander_Skill_09_Slash.prefab");
            var burstVfx = Load<GameObject>("Assets/ProjectMT/04_Contents/04_FallenCommander/Art/VFX/FallenCommander_Skill_24.prefab");
            var fieldVfx = Load<GameObject>("Assets/ProjectMT/04_Contents/04_FallenCommander/Art/VFX/FallenCommander_Skill_12_Donut.prefab");
            ValidateRequiredAssets(projectile, catalog, balance, castSfx, impactSfx, icon,
                markVfx, slashVfx, burstVfx, fieldVfx);
            EnsureFolder(Root);
            EnsureFolder(MarkRoot);

            var pursuit = CreateDamageMark("Pursuit", 5f, CommanderMarkTriggerType.HitCount, 5, 1, 1,
                false, 0.8f, MonsterBasicAttackShape.Single, 0.55f, 0.1f, 1, markVfx, slashVfx, impactSfx);
            var rupture = CreateDamageMark("Rupture", 6f, CommanderMarkTriggerType.HitCount, 4, 1, 1,
                false, 1f, MonsterBasicAttackShape.Circle, 0.85f, 2.4f, 6, markVfx, burstVfx, impactSfx);
            var collapse = CreateUnitMark("Collapse", 5f, CommanderMarkTriggerType.HitCount, 3, 1, 1,
                false, 0f, CommanderSkillUnitEffectType.Stun, 0f, 0.45f, markVfx, burstVfx, impactSfx);
            var conquest = CreateDamageMark("Conquest", 3f, CommanderMarkTriggerType.HitCount, 3, 1, 1,
                false, 1f, MonsterBasicAttackShape.Single, 0.45f, 0.1f, 1, markVfx, slashVfx, impactSfx);
            var agitation = CreateDamageMark("Agitation", 4f, CommanderMarkTriggerType.StackReached, 1, 3, 3,
                true, 0f, MonsterBasicAttackShape.Circle, 0.55f, 2.3f, 6, markVfx, burstVfx, impactSfx);
            var deathSentence = CreateRecordedMark("DeathSentence", 5f, 0.4f, 0.12f, 20,
                markVfx, slashVfx, impactSfx);
            var resonance = CreateDamageMark("Resonance", 4f, CommanderMarkTriggerType.HitCount, 4, 1, 1,
                false, 0.8f, MonsterBasicAttackShape.Circle, 0.5f, 2.5f, 8, markVfx, burstVfx, impactSfx);
            var ruin = CreateDamageMark("Ruin", 5f, CommanderMarkTriggerType.StackReached, 1, 4, 4,
                true, 0f, MonsterBasicAttackShape.Circle, 0.85f, 3.5f, 10, markVfx, burstVfx, impactSfx);
            var warGod = CreateDamageMark("WarGodBrand", 10f, CommanderMarkTriggerType.MarkTriggered, 1, 1, 1,
                false, 0.5f, MonsterBasicAttackShape.Single, 0.75f, 0.1f, 1, markVfx, slashVfx, impactSfx, 2f);

            var skills = new List<CommanderSkillDefinition>
            {
                CreateSkill(new SkillSpec("CS_TrackingBlade", "추적의 검인", CommanderSkillRarity.Common, 6.5f, 18f,
                    CommanderSkillTargetSelection.Nearest, MonsterBasicAttackDeliveryModule.Projectile,
                    CommanderSkillTrajectory.Straight, 0f, CommanderSkillPatternType.Burst, 6, 0.14f,
                    1f, 1f, 0f, 1, 4f, MonsterBasicAttackShape.Single, 0.1f, 0.32f, 1, 0.05f),
                    icon, projectile, castSfx, impactSfx, slashVfx, burstVfx, pursuit),
                CreateSkill(new SkillSpec("CS_DoomSpear", "파멸의 창", CommanderSkillRarity.Common, 7f, 20f,
                    CommanderSkillTargetSelection.Nearest, MonsterBasicAttackDeliveryModule.Projectile,
                    CommanderSkillTrajectory.Arc, 4f, CommanderSkillPatternType.Single, 1, 0f,
                    1f, 1f, 0f, 1, 4f, MonsterBasicAttackShape.Circle, 2.8f, 1.45f, 8, 0.05f),
                    icon, projectile, castSfx, impactSfx, slashVfx, burstVfx, rupture),
                CreateSkill(new SkillSpec("CS_AbyssChain", "구속의 사슬", CommanderSkillRarity.Common, 6f, 20f,
                    CommanderSkillTargetSelection.Nearest, MonsterBasicAttackDeliveryModule.Direct,
                    CommanderSkillTrajectory.Straight, 0f, CommanderSkillPatternType.Chain, 1, 0.12f,
                    1f, 1f, 0f, 4, 4.5f, MonsterBasicAttackShape.Single, 0.1f, 0.6f, 1, 0.05f),
                    icon, projectile, castSfx, impactSfx, slashVfx, slashVfx,
                    CreateUnitEffect("abyss_chain_slow", CommanderSkillUnitEffectType.Slow, 0.2f, 2.5f)),
                CreateSkill(new SkillSpec("CS_PhantomCharge", "군세의 돌진", CommanderSkillRarity.Rare, 9f, 15f,
                    CommanderSkillTargetSelection.MostCrowded, MonsterBasicAttackDeliveryModule.Direct,
                    CommanderSkillTrajectory.Straight, 0f, CommanderSkillPatternType.Burst, 3, 0.28f,
                    1f, 1f, 0f, 1, 4f, MonsterBasicAttackShape.Line, 1f, 0.72f, 12, 3.5f),
                    icon, projectile, castSfx, impactSfx, slashVfx, burstVfx, collapse),
                CreateSkill(new SkillSpec("CS_ConquerorSigil", "정복자의 문장", CommanderSkillRarity.Rare, 10f, 20f,
                    CommanderSkillTargetSelection.MostCrowded, MonsterBasicAttackDeliveryModule.TravelingArea,
                    CommanderSkillTrajectory.Straight, 0f, CommanderSkillPatternType.PersistentArea, 1, 0f,
                    6f, 1f, 0f, 1, 4f, MonsterBasicAttackShape.Circle, 4.5f, 0.3f, 10, 0.05f),
                    icon, projectile, castSfx, impactSfx, fieldVfx, fieldVfx, conquest),
                CreateSkill(new SkillSpec("CS_PhantomBarrage", "유령 포격", CommanderSkillRarity.Rare, 9f, 20f,
                    CommanderSkillTargetSelection.MostCrowded, MonsterBasicAttackDeliveryModule.TravelingArea,
                    CommanderSkillTrajectory.Straight, 0f, CommanderSkillPatternType.Barrage, 7, 0.25f,
                    1f, 1f, 4f, 1, 4f, MonsterBasicAttackShape.Circle, 2f, 0.34f, 6, 0.05f),
                    icon, projectile, castSfx, impactSfx, slashVfx, burstVfx, agitation),
                CreateSkill(new SkillSpec("CS_DeathSentence", "사형 선고", CommanderSkillRarity.Epic, 12f, 20f,
                    CommanderSkillTargetSelection.Strongest, MonsterBasicAttackDeliveryModule.Direct,
                    CommanderSkillTrajectory.Straight, 0f, CommanderSkillPatternType.Single, 1, 0f,
                    1f, 1f, 0f, 1, 4f, MonsterBasicAttackShape.Single, 0.1f, 0.4f, 1, 0.05f),
                    icon, projectile, castSfx, impactSfx, slashVfx, slashVfx, deathSentence),
                CreateSkill(new SkillSpec("CS_RuptureMarch", "파열의 행진", CommanderSkillRarity.Epic, 11f, 20f,
                    CommanderSkillTargetSelection.MostCrowded, MonsterBasicAttackDeliveryModule.TravelingArea,
                    CommanderSkillTrajectory.Straight, 0f, CommanderSkillPatternType.Pulse, 5, 0.55f,
                    1f, 1f, 0f, 1, 4f, MonsterBasicAttackShape.Circle, 5f, 0.42f, 12, 0.05f),
                    icon, projectile, castSfx, impactSfx, fieldVfx, burstVfx, rupture),
                CreateSkill(new SkillSpec("CS_HeartOfBattlefield", "전장의 심장", CommanderSkillRarity.Legendary, 14f, 20f,
                    CommanderSkillTargetSelection.MostCrowded, MonsterBasicAttackDeliveryModule.TravelingArea,
                    CommanderSkillTrajectory.Straight, 0f, CommanderSkillPatternType.Pulse, 7, 0.8f,
                    1f, 1f, 0f, 1, 4f, MonsterBasicAttackShape.Circle, 6.5f, 0.48f, 16, 0.05f),
                    icon, projectile, castSfx, impactSfx, fieldVfx, burstVfx, resonance),
                CreateSkill(new SkillSpec("CS_MarchOfDead", "망자의 대행진", CommanderSkillRarity.Legendary, 16f, 18f,
                    CommanderSkillTargetSelection.MostCrowded, MonsterBasicAttackDeliveryModule.Direct,
                    CommanderSkillTrajectory.Straight, 0f, CommanderSkillPatternType.Burst, 4, 0.45f,
                    1f, 1f, 0f, 1, 4f, MonsterBasicAttackShape.Line, 1f, 0.9f, 20, 5f),
                    icon, projectile, castSfx, impactSfx, slashVfx, burstVfx, ruin),
                CreateSkill(new SkillSpec("CS_WarGodBrand", "군신의 낙인", CommanderSkillRarity.Mythic, 18f, 20f,
                    CommanderSkillTargetSelection.Strongest, MonsterBasicAttackDeliveryModule.Direct,
                    CommanderSkillTrajectory.Straight, 0f, CommanderSkillPatternType.Single, 1, 0f,
                    1f, 1f, 0f, 1, 4f, MonsterBasicAttackShape.Single, 0.1f, 0.65f, 1, 0.05f),
                    icon, projectile, castSfx, impactSfx, slashVfx, slashVfx, warGod),
                CreateApocalypse(icon, projectile, castSfx, impactSfx, fieldVfx, burstVfx)
            };

            Register(skills, catalog, balance);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Debug.Log($"COMMANDER_SKILL_RELEASE_CANDIDATE_12=PASS skills={skills.Count} marks=9 root={Root}");
        }

        public static void RunOnceFromCommandLine() => Build();

        private static void ValidateRequiredAssets(params UnityEngine.Object[] assets)
        {
            if (assets == null || assets.Any(asset => asset == null))
                throw new InvalidOperationException("ReleaseCandidate 12 필수 자산 검증에 실패했습니다.");
        }

        private static CommanderSkillDefinition CreateApocalypse(Sprite icon, GameObject projectile, SfxCue castSfx,
            SfxCue impactSfx, GameObject castVfx, GameObject impactVfx)
        {
            var modifier = ScriptableObject.CreateInstance<CommanderGlobalModifierEffectDefinition>();
            modifier.name = "__Effect_GlobalModifier";
            modifier.EditorConfigure("apocalypse_global_modifier", 8f, 0.7f, 1.3f, 1.2f);
            return CreateSkill(new SkillSpec("CS_ApocalypseWar", "종말 전쟁", CommanderSkillRarity.Mythic, 24f, 20f,
                CommanderSkillTargetSelection.MostCrowded, MonsterBasicAttackDeliveryModule.TravelingArea,
                CommanderSkillTrajectory.Straight, 0f, CommanderSkillPatternType.PersistentArea, 1, 0f,
                8f, 1f, 0f, 1, 4f, MonsterBasicAttackShape.Circle, 10f, 0.4f, 24, 0.05f),
                icon, projectile, castSfx, impactSfx, castVfx, impactVfx, modifier);
        }

        private static CommanderSkillDefinition CreateSkill(SkillSpec spec, Sprite icon, GameObject projectile,
            SfxCue castSfx, SfxCue impactSfx, GameObject castVfx, GameObject impactVfx,
            params CommanderSkillEffectDefinition[] extraEffects)
        {
            var path = $"{Root}/{spec.Id}.asset";
            var skill = AssetDatabase.LoadAssetAtPath<CommanderAttackSkillDefinition>(path);
            var created = skill == null;
            if (created)
            {
                skill = ScriptableObject.CreateInstance<CommanderAttackSkillDefinition>();
                skill.name = spec.Id;
                AssetDatabase.CreateAsset(skill, path);
            }
            var targeting = Add<CommanderSkillTargetingDefinition>(skill, "__Targeting");
            targeting.EditorConfigure(CommanderSkillTargetTeam.Enemy, spec.Selection, spec.Range);
            var damage = Add<CommanderAreaDamageEffectDefinition>(skill, "__Damage");
            damage.EditorConfigure(spec.Id + "_damage", CommanderSkillDamageKind.Physical, BaseDamage,
                spec.Multiplier, spec.Shape, MonsterBasicAttackCenter.PrimaryTarget, spec.Radius, 0f, 90f,
                spec.LineWidth, spec.MaxTargets);
            var effects = new List<CommanderSkillEffectDefinition> { damage };
            foreach (var extra in extraEffects)
            {
                if (extra == null) continue;
                effects.Add(MergeGeneratedExtra(skill, extra));
            }
            skill.EditorConfigure(spec.Id, spec.Name, "SkillMaker V2 출시 후보 12종 데이터 조합 스킬.",
                skill.Icon != null ? skill.Icon : icon, 0f, spec.Cooldown, targeting, effects.ToArray(), spec.Delivery,
                skill.ProjectilePrefab != null ? skill.ProjectilePrefab : projectile, 16f, spec.Trajectory, spec.ArcHeight,
                skill.CastVfxPrefab != null ? skill.CastVfxPrefab : castVfx,
                created ? 1f : skill.CastVfxLifetime, skill.CastSfx != null ? skill.CastSfx : castSfx,
                skill.ImpactVfxPrefab != null ? skill.ImpactVfxPrefab : impactVfx,
                created ? 1.5f : skill.ImpactVfxLifetime, skill.ImpactSfx != null ? skill.ImpactSfx : impactSfx);
            var pattern = new CommanderSkillPatternConfig();
            pattern.EditorConfigure(spec.Pattern, spec.Repeat, spec.Interval, spec.Duration, spec.Tick,
                spec.RandomRadius, spec.ChainCount, spec.ChainRadius);
            skill.EditorConfigureV2(spec.Rarity, pattern);
            if (created)
                skill.EditorConfigureFeedbackTransforms(Vector3.zero, Vector3.zero, 1f, Vector3.zero, Vector3.zero, 1f);
            if (spec.Pattern == CommanderSkillPatternType.PersistentArea && skill.PersistentVfxPrefab == null)
                skill.EditorConfigurePersistentFeedback(impactVfx, Vector3.zero, Vector3.zero, 1f,
                    CommanderMarkFeedbackAnchor.WorldPosition);
            if (!skill.TryValidate(out var error)) throw new InvalidOperationException($"{spec.Id}: {error}");
            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssetIfDirty(skill);
            return skill;
        }

        private static CommanderMarkEffectDefinition CreateDamageMark(string id, float duration,
            CommanderMarkTriggerType triggerType, int hits, int requiredStacks, int maxStacks, bool consume,
            float cooldown, MonsterBasicAttackShape shape, float multiplier, float radius, int maxTargets,
            GameObject markVfx, GameObject triggerVfx, SfxCue triggerSfx, float scale = 1f)
        {
            var path = $"{MarkRoot}/MARK_{id}.asset";
            var mark = AssetDatabase.LoadAssetAtPath<CommanderMarkEffectDefinition>(path);
            var created = mark == null;
            if (created)
            {
                mark = ScriptableObject.CreateInstance<CommanderMarkEffectDefinition>();
                mark.name = "MARK_" + id;
                AssetDatabase.CreateAsset(mark, path);
            }
            var damage = Add<CommanderAreaDamageEffectDefinition>(mark, "__Trigger_Damage");
            damage.EditorConfigure(id + "_trigger_damage", CommanderSkillDamageKind.Physical, BaseDamage,
                multiplier, shape, MonsterBasicAttackCenter.PrimaryTarget, radius, 0f, 90f, 0.05f, maxTargets);
            ConfigureMark(mark, id, duration, triggerType, hits, requiredStacks, maxStacks, consume, cooldown,
                new CommanderSkillEffectDefinition[] { damage }, false, markVfx, triggerVfx, triggerSfx, scale, created);
            AssetDatabase.SaveAssetIfDirty(mark);
            return mark;
        }

        private static CommanderMarkEffectDefinition CreateUnitMark(string id, float duration,
            CommanderMarkTriggerType triggerType, int hits, int requiredStacks, int maxStacks, bool consume,
            float cooldown, CommanderSkillUnitEffectType type, float magnitude, float effectDuration,
            GameObject markVfx, GameObject triggerVfx, SfxCue triggerSfx)
        {
            var path = $"{MarkRoot}/MARK_{id}.asset";
            var mark = AssetDatabase.LoadAssetAtPath<CommanderMarkEffectDefinition>(path);
            var created = mark == null;
            if (created)
            {
                mark = ScriptableObject.CreateInstance<CommanderMarkEffectDefinition>();
                mark.name = "MARK_" + id;
                AssetDatabase.CreateAsset(mark, path);
            }
            var unit = Add<CommanderUnitEffectDefinition>(mark, "__Trigger_UnitEffect");
            unit.EditorConfigure(id + "_trigger_effect", type, CommanderSkillEffectValueSource.Flat, magnitude,
                effectDuration, CommanderSkillEffectScope.PrimaryTarget, 0.1f, 1,
                MonsterBuffStackPolicy.RefreshDuration);
            ConfigureMark(mark, id, duration, triggerType, hits, requiredStacks, maxStacks, consume, cooldown,
                new CommanderSkillEffectDefinition[] { unit }, false, markVfx, triggerVfx, triggerSfx, 1f, created);
            AssetDatabase.SaveAssetIfDirty(mark);
            return mark;
        }

        private static CommanderMarkEffectDefinition CreateRecordedMark(string id, float duration,
            float baseMultiplier, float perHit, int cap, GameObject markVfx, GameObject triggerVfx, SfxCue triggerSfx)
        {
            var path = $"{MarkRoot}/MARK_{id}.asset";
            var mark = AssetDatabase.LoadAssetAtPath<CommanderMarkEffectDefinition>(path);
            var created = mark == null;
            if (created)
            {
                mark = ScriptableObject.CreateInstance<CommanderMarkEffectDefinition>();
                mark.name = "MARK_" + id;
                AssetDatabase.CreateAsset(mark, path);
            }
            var recorded = Add<CommanderRecordedHitDamageEffectDefinition>(mark, "__Trigger_RecordedHitDamage");
            recorded.EditorConfigure(id + "_recorded_hit_damage", baseMultiplier, perHit, cap);
            ConfigureMark(mark, id, duration, CommanderMarkTriggerType.Expire, 1, 1, 1, true, 0f,
                new CommanderSkillEffectDefinition[] { recorded }, true, markVfx, triggerVfx, triggerSfx, 1.6f, created);
            AssetDatabase.SaveAssetIfDirty(mark);
            return mark;
        }

        private static void ConfigureMark(CommanderMarkEffectDefinition mark, string id, float duration,
            CommanderMarkTriggerType trigger, int hits, int stacks, int maxStacks, bool consume, float cooldown,
            CommanderSkillEffectDefinition[] effects, bool recordHits, GameObject markVfx,
            GameObject triggerVfx, SfxCue triggerSfx, float scale, bool configureFeedback)
        {
            mark.EditorConfigure(id + "_mark", id, duration, CommanderSkillEffectScope.ImpactTargets,
                0.1f, 64, trigger, hits, stacks, maxStacks, consume, true, cooldown, effects);
            mark.EditorConfigureRecording(recordHits);
            if (configureFeedback)
                mark.EditorConfigureFeedback(
                    Feedback(markVfx, 1f, scale, CommanderMarkFeedbackAnchor.TargetCenter, null),
                    Feedback(markVfx, duration, scale * 0.7f, CommanderMarkFeedbackAnchor.TargetCenter, null),
                    Feedback(markVfx, 0.45f, scale * 0.9f, CommanderMarkFeedbackAnchor.TargetCenter, null),
                    Feedback(triggerVfx, 1.5f, scale, CommanderMarkFeedbackAnchor.TargetCenter, triggerSfx),
                    Feedback(null, 0.5f, 1f, CommanderMarkFeedbackAnchor.TargetCenter, null));
            EditorUtility.SetDirty(mark);
        }

        private static CommanderMarkFeedbackSlot Feedback(GameObject vfx, float lifetime, float scale,
            CommanderMarkFeedbackAnchor anchor, SfxCue sfx)
        {
            var slot = new CommanderMarkFeedbackSlot();
            slot.EditorConfigure(vfx, lifetime, Vector3.zero, Vector3.zero, scale, sfx, anchor);
            return slot;
        }

        private static CommanderUnitEffectDefinition CreateUnitEffect(string id, CommanderSkillUnitEffectType type,
            float magnitude, float duration)
        {
            var effect = ScriptableObject.CreateInstance<CommanderUnitEffectDefinition>();
            effect.name = "__Effect_Unit";
            effect.EditorConfigure(id, type, CommanderSkillEffectValueSource.Flat, magnitude, duration,
                CommanderSkillEffectScope.PrimaryTarget, 0.1f, 1, MonsterBuffStackPolicy.RefreshDuration);
            return effect;
        }

        private static void Register(IReadOnlyList<CommanderSkillDefinition> created,
            CommanderSkillCatalog catalog, CommanderSkillBalanceConfig balance)
        {
            var ids = new HashSet<string>(created.Select(skill => skill.SkillId), StringComparer.Ordinal);
            var definitions = catalog.Skills.Where(skill => skill != null && !ids.Contains(skill.SkillId)).ToList();
            definitions.AddRange(created);
            var rules = balance.SkillRules.Where(rule => rule != null && !ids.Contains(rule.SkillId)).ToList();
            foreach (var skill in created)
            {
                var rule = new CommanderSkillGrowthRule();
                rule.EditorConfigure(skill.SkillId, 200, 1, AnimationCurve.Linear(1f, 1f, 200f, 4.98f));
                rules.Add(rule);
            }
            balance.EditorConfigure(rules.ToArray());
            var summon = catalog.SummonConfig;
            var levels = new CommanderSkillSummonLevelRule[summon.Levels.Count];
            for (var levelIndex = 0; levelIndex < summon.Levels.Count; levelIndex++)
            {
                var sourceLevel = summon.Levels[levelIndex];
                var entries = sourceLevel.Pool
                    .Where(entry => entry != null && !ids.Contains(entry.SkillId))
                    .Select(entry =>
                    {
                        var clone = new CommanderSkillSummonPoolEntry();
                        clone.EditorConfigure(entry.SkillId, entry.Weight);
                        return clone;
                    }).ToList();
                foreach (var skill in created)
                {
                    var entry = new CommanderSkillSummonPoolEntry();
                    entry.EditorConfigure(skill.SkillId, 100);
                    entries.Add(entry);
                }
                var level = new CommanderSkillSummonLevelRule();
                level.EditorConfigure(sourceLevel.RequiredAccumulatedCount, entries.ToArray());
                levels[levelIndex] = level;
            }
            var offers = summon.Offers.Select(source =>
            {
                var clone = new CommanderSkillSummonOffer();
                clone.EditorConfigure(source.DrawCount, source.TicketCost);
                return clone;
            }).ToArray();
            summon.EditorConfigure(summon.TicketItemId, levels, offers, summon.DiamondCostPerMissingTicket);
            catalog.EditorConfigure(balance, summon, definitions.ToArray());
            if (!catalog.TryValidate(out var error)) throw new InvalidOperationException(error);
            EditorUtility.SetDirty(balance);
            EditorUtility.SetDirty(summon);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(balance);
            AssetDatabase.SaveAssetIfDirty(summon);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        private static T Add<T>(ScriptableObject owner, string name) where T : ScriptableObject
        {
            var path = AssetDatabase.GetAssetPath(owner);
            var value = string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>().FirstOrDefault(item => item.name == name);
            if (value == null)
            {
                value = ScriptableObject.CreateInstance<T>();
                value.name = name;
                value.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(value, owner);
            }
            EditorUtility.SetDirty(value);
            return value;
        }

        private static CommanderSkillEffectDefinition MergeGeneratedExtra(CommanderSkillDefinition owner,
            CommanderSkillEffectDefinition generated)
        {
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(generated))) return generated;
            var existing = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(owner))
                .OfType<CommanderSkillEffectDefinition>()
                .FirstOrDefault(item => item.GetType() == generated.GetType() && item.name == generated.name);
            if (existing is CommanderUnitEffectDefinition existingUnit && generated is CommanderUnitEffectDefinition unit)
                existingUnit.EditorConfigure(unit.EffectId, unit.EffectType, unit.ValueSource, unit.Magnitude,
                    unit.Duration, unit.Scope, unit.Radius, unit.MaxTargets, unit.StackPolicy);
            else if (existing is CommanderGlobalModifierEffectDefinition existingModifier &&
                     generated is CommanderGlobalModifierEffectDefinition modifier)
                existingModifier.EditorConfigure(modifier.EffectId, modifier.Duration,
                    modifier.MarkRequiredHitsMultiplier, modifier.MarkTriggerDamageMultiplier,
                    modifier.CooldownRecoveryMultiplier);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            generated.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(generated, owner);
            EditorUtility.SetDirty(generated);
            return generated;
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value == null) throw new InvalidOperationException($"Required asset is missing: {path}");
            return value;
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private readonly struct SkillSpec
        {
            public SkillSpec(string id, string name, CommanderSkillRarity rarity, float cooldown, float range,
                CommanderSkillTargetSelection selection, MonsterBasicAttackDeliveryModule delivery,
                CommanderSkillTrajectory trajectory, float arcHeight, CommanderSkillPatternType pattern,
                int repeat, float interval, float duration, float tick, float randomRadius, int chainCount,
                float chainRadius, MonsterBasicAttackShape shape, float radius, float multiplier,
                int maxTargets, float lineWidth)
            {
                Id = id; Name = name; Rarity = rarity; Cooldown = cooldown; Range = range; Selection = selection;
                Delivery = delivery; Trajectory = trajectory; ArcHeight = arcHeight; Pattern = pattern;
                Repeat = repeat; Interval = interval; Duration = duration; Tick = tick; RandomRadius = randomRadius;
                ChainCount = chainCount; ChainRadius = chainRadius; Shape = shape; Radius = radius;
                Multiplier = multiplier; MaxTargets = maxTargets; LineWidth = lineWidth;
            }
            public readonly string Id, Name;
            public readonly CommanderSkillRarity Rarity;
            public readonly float Cooldown, Range, ArcHeight, Interval, Duration, Tick, RandomRadius, ChainRadius,
                Radius, Multiplier, LineWidth;
            public readonly int Repeat, ChainCount, MaxTargets;
            public readonly CommanderSkillTargetSelection Selection;
            public readonly MonsterBasicAttackDeliveryModule Delivery;
            public readonly CommanderSkillTrajectory Trajectory;
            public readonly CommanderSkillPatternType Pattern;
            public readonly MonsterBasicAttackShape Shape;
        }
    }
}
