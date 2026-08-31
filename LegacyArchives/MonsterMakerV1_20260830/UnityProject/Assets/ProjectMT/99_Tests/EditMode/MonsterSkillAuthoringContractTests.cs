using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Tests.EditMode
{
    public sealed class MonsterSkillAuthoringContractTests // 범용 스킬 카탈로그와 Maker 편입 계약
    {
        private const string DraftPath =
            "Assets/ProjectMT/Editor/MonsterMaker/Drafts/Draft_lumi_01.asset";
        private const string DraftRoot = "Assets/ProjectMT/Editor/MonsterMaker/Drafts";
        private const string MonsterCatalogPath =
            "Assets/ProjectMT/02_Shared/Unit/Data/MonsterCatalog.asset";
        private const string RarityCatalogPath =
            "Assets/ProjectMT/02_Shared/Unit/Data/MonsterRarityCatalog.asset";

        private static readonly string[] P0PassiveIds =
        {
            "passive_kain_duality",
            "passive_nth_hit_power",
            "passive_impact_strike",
            "passive_low_hp_hunter",
            "passive_ranged_hunter",
            "passive_entry_shield",
            "passive_nth_hit_heal",
            "passive_same_target_haste",
            "passive_long_range_aim",
            "passive_crisis_defense",
            "passive_formation_bond",
            "passive_weakpoint_stack",
            "passive_kill_heal",
            "passive_courage_aura",
            "passive_first_wave"
        };

        private static readonly string[] P0ActiveIds =
        {
            "active_kain_tenfold_rush",
            "active_cone_strike",
            "active_spin_attack",
            "active_execute_strike",
            "active_multihit_single",
            "active_piercing_projectile",
            "active_explosive_projectile",
            "active_rear_snipe",
            "active_taunt_shield",
            "active_group_shield",
            "active_defense_stance",
            "active_single_heal",
            "active_life_wave",
            "active_team_haste",
            "active_courage_song",
            "active_attack_mark"
        };

        [Test]
        public void DefaultCatalog_ContainsValidatedReusablePresetPack()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterSkillCatalog>(MonsterSkillCatalog.DefaultAssetPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryValidate(out var error), Is.True, error);
            Assert.That(catalog.PassiveSkills.Count, Is.EqualTo(15));
            Assert.That(catalog.ActiveSkills.Count, Is.EqualTo(29));
            Assert.That(catalog.PassiveSkills, Has.All.InstanceOf<GenericMonsterPassiveSkill>());
            Assert.That(catalog.ActiveSkills, Has.All.InstanceOf<GenericMonsterActiveSkill>());
            Assert.That(catalog.PassiveSkills.Select(skill => skill.SkillId)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(15));
            Assert.That(catalog.ActiveSkills.Select(skill => skill.SkillId)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(29));
            Assert.That(catalog.PassiveSkills.Select(skill => skill.Category).Distinct().Count(),
                Is.GreaterThanOrEqualTo(4));
            Assert.That(catalog.ActiveSkills.Select(skill => skill.Category).Distinct().Count(),
                Is.GreaterThanOrEqualTo(5));
            CollectionAssert.AreEquivalent(
                P0PassiveIds,
                catalog.PassiveSkills.Where(skill => skill.AuthoringEnabled).Select(skill => skill.SkillId));
            CollectionAssert.AreEquivalent(
                P0ActiveIds,
                catalog.ActiveSkills.Where(skill => skill.AuthoringEnabled).Select(skill => skill.SkillId));
            Assert.That(catalog.PassiveSkills.Count(skill => !skill.AuthoringEnabled), Is.Zero);
            Assert.That(catalog.ActiveSkills.Count(skill => !skill.AuthoringEnabled), Is.EqualTo(13));
        }

        [Test]
        public void RepresentativePresets_KeepTriggerRepeatAndDelaySemantics()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterSkillCatalog>(MonsterSkillCatalog.DefaultAssetPath);

            var impact = Get<GenericMonsterPassiveSkill>(catalog, "passive_impact_strike");
            Assert.That(impact.Recipe.Trigger, Is.EqualTo(MonsterSkillTriggerType.BasicAttackNthHit));
            Assert.That(impact.Recipe.TriggerCount, Is.EqualTo(4));

            var haste = Get<GenericMonsterPassiveSkill>(catalog, "passive_same_target_haste");
            Assert.That(haste.Recipe.Conditions.Any(condition =>
                condition.Type == MonsterSkillConditionType.SameTargetContinuous), Is.True);

            var multihit = Get<GenericMonsterActiveSkill>(catalog, "active_multihit_single");
            Assert.That(multihit.Recipe.Effects.Single().RepeatCount, Is.EqualTo(3));
            StringAssert.Contains("x3", multihit.RecipeSummary);

            var delayed = Get<GenericMonsterActiveSkill>(catalog, "active_delayed_mark");
            Assert.That(delayed.Recipe.Effects.Count, Is.EqualTo(2));
            Assert.That(delayed.Recipe.Effects[1].Delay, Is.EqualTo(2f).Within(0.001f));
            StringAssert.Contains("+2s", delayed.RecipeSummary);

            var duality = Get<GenericMonsterPassiveSkill>(catalog, "passive_kain_duality");
            Assert.That(duality.Recipe.Effects.Single().ResolveMagnitude(0f), Is.EqualTo(0.01f).Within(0.0001f));
            Assert.That(duality.Recipe.Effects.Single().ResolveMagnitude(1f), Is.EqualTo(5f).Within(0.0001f));

            var tenfold = Get<GenericMonsterActiveSkill>(catalog, "active_kain_tenfold_rush");
            Assert.That(tenfold.Recipe.Effects.Single().RepeatCount, Is.EqualTo(10));
            Assert.That(tenfold.Recipe.Effects.Single().RepeatInterval, Is.EqualTo(0.075f).Within(0.0001f));
        }

        [Test]
        public void MakerValidator_RejectsActiveForEpicAndCommon()
        {
            var source = AssetDatabase.LoadMainAssetAtPath(DraftPath) as ScriptableObject;
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterSkillCatalog>(MonsterSkillCatalog.DefaultAssetPath);
            Assert.That(source, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);

            var draft = ScriptableObject.CreateInstance(source.GetType());
            try
            {
                EditorUtility.CopySerialized(source, draft);
                ConfigureSkills(
                    draft,
                    MonsterRarity.Epic,
                    Get<GenericMonsterPassiveSkill>(catalog, "passive_nth_hit_power"),
                    Get<GenericMonsterActiveSkill>(catalog, "active_cone_strike"));
                Assert.That(GetMakerIssueCodes(draft), Does.Contain("MAKER-SKILL-ACTIVE-RARITY"));

                ConfigureSkills(
                    draft,
                    MonsterRarity.Common,
                    Get<GenericMonsterPassiveSkill>(catalog, "passive_nth_hit_power"),
                    Get<GenericMonsterActiveSkill>(catalog, "active_cone_strike"));
                Assert.That(GetMakerIssueCodes(draft), Does.Contain("MAKER-SKILL-ACTIVE-RARITY"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void MakerValidator_AllowsEpicPassiveOnlyWithoutActiveRequirement()
        {
            var source = AssetDatabase.LoadMainAssetAtPath(DraftPath) as ScriptableObject;
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterSkillCatalog>(MonsterSkillCatalog.DefaultAssetPath);
            Assert.That(source, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);

            var draft = ScriptableObject.CreateInstance(source.GetType());
            try
            {
                EditorUtility.CopySerialized(source, draft);
                ConfigureSkills(
                    draft,
                    MonsterRarity.Epic,
                    Get<GenericMonsterPassiveSkill>(catalog, "passive_same_target_haste"),
                    null);

                var codes = GetMakerIssueCodes(draft);
                Assert.That(codes.Where(code => code.StartsWith("MAKER-SKILL-ACTIVE", StringComparison.Ordinal)),
                    Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void MakerValidator_LegacyGenericActiveDoesNotReplaceLegendaryAttackProfile()
        {
            var source = AssetDatabase.LoadMainAssetAtPath(DraftPath) as ScriptableObject;
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterSkillCatalog>(MonsterSkillCatalog.DefaultAssetPath);
            Assert.That(source, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);

            var draft = ScriptableObject.CreateInstance(source.GetType());
            try
            {
                EditorUtility.CopySerialized(source, draft);
                ConfigureSkills(
                    draft,
                    MonsterRarity.Legendary,
                    Get<GenericMonsterPassiveSkill>(catalog, "passive_nth_hit_power"),
                    Get<GenericMonsterActiveSkill>(catalog, "active_dash_line"));

                var codes = GetMakerIssueCodes(draft);
                Assert.That(codes, Does.Not.Contain("MAKER-SKILL-PASSIVE-DISABLED"));
                Assert.That(codes, Does.Contain("MAKER-SKILL-ACTIVE-PENDING"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void MakerValidator_LegacyDraftWithoutSkillOptIn_RemainsBackwardCompatible()
        {
            var source = AssetDatabase.LoadMainAssetAtPath(DraftPath) as ScriptableObject;
            Assert.That(source, Is.Not.Null);

            var draft = ScriptableObject.CreateInstance(source.GetType());
            try
            {
                EditorUtility.CopySerialized(source, draft);
                var serialized = new SerializedObject(draft);
                serialized.FindProperty("skillLoadoutConfigured").boolValue = false;
                serialized.FindProperty("rarityPassiveSkill").objectReferenceValue = null;
                serialized.FindProperty("rarityActiveSkill").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(GetMakerIssueCodes(draft).Where(code =>
                    code.StartsWith("MAKER-SKILL", StringComparison.Ordinal)), Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void RegisterRarity_PublishesPassiveOnlyForEpic()
        {
            var source = AssetDatabase.LoadMainAssetAtPath(DraftPath) as ScriptableObject;
            var skillCatalog = AssetDatabase.LoadAssetAtPath<MonsterSkillCatalog>(MonsterSkillCatalog.DefaultAssetPath);
            var draft = ScriptableObject.CreateInstance(source.GetType());
            var definition = ScriptableObject.CreateInstance<MonsterDefinition>();
            var rarityCatalog = ScriptableObject.CreateInstance<MonsterRarityCatalog>();
            try
            {
                EditorUtility.CopySerialized(source, draft);
                var passive = Get<GenericMonsterPassiveSkill>(skillCatalog, "passive_impact_strike");
                ConfigureSkills(draft, MonsterRarity.Epic, passive, null);
                definition.EditorConfigure("skill_authoring_probe", 100f, 10f, 0f, 1f, 2f, 1f, false);

                var writerType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerAssetWriter");
                var register = writerType.GetMethod("RegisterRarity", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(register, Is.Not.Null);
                register.Invoke(null, new object[] { rarityCatalog, definition, draft, passive, null });

                Assert.That(rarityCatalog.CommonToEpicEntries, Has.Count.EqualTo(1));
                var entry = rarityCatalog.CommonToEpicEntries[0];
                Assert.That(entry.Rarity, Is.EqualTo(MonsterRarity.Epic));
                Assert.That(entry.PassiveSkill, Is.SameAs(passive));
                Assert.That(entry.ActiveSkill, Is.Null);
                Assert.That(entry.TryValidateSkillReferences(out var error), Is.True, error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rarityCatalog);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void MakerWindow_ExposesCatalogSelectionAndOnlyActiveCategoryFilter()
        {
            var windowType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerWindow");

            Assert.That(windowType.GetMethod("DrawSkillSection", BindingFlags.NonPublic | BindingFlags.Instance),
                Is.Not.Null);
            Assert.That(windowType.GetField("monsterSkillCatalog", BindingFlags.NonPublic | BindingFlags.Instance),
                Is.Not.Null);
            Assert.That(windowType.GetField("passiveSkillCategoryFilter", BindingFlags.NonPublic | BindingFlags.Instance),
                Is.Null);
            Assert.That(windowType.GetField("activeSkillCategoryFilter", BindingFlags.NonPublic | BindingFlags.Instance),
                Is.Not.Null);
            Assert.That(windowType.GetMethod("DrawSkillAugment", BindingFlags.NonPublic | BindingFlags.Instance),
                Is.Not.Null);
            Assert.That(windowType.GetField("passiveBalanceEditor", BindingFlags.NonPublic | BindingFlags.Instance),
                Is.Not.Null);
            var balanceEditor = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterPassiveBalanceEditor");
            Assert.That(balanceEditor.GetMethod("Draw", BindingFlags.Public | BindingFlags.Instance), Is.Not.Null);
            Assert.That(balanceEditor.GetMethod("EnsureInitialized", BindingFlags.NonPublic | BindingFlags.Static), Is.Not.Null);
        }

        [Test]
        public void PassiveBalanceEditor_StoresMonsterValueWithoutChangingTemplate()
        {
            var source = AssetDatabase.LoadMainAssetAtPath(DraftPath) as ScriptableObject;
            var draft = ScriptableObject.CreateInstance(source.GetType());
            var template = ScriptableObject.CreateInstance<GenericMonsterPassiveSkill>();
            try
            {
                EditorUtility.CopySerialized(source, draft);
                template.EditorConfigureRuntime(
                    GenericMonsterPassiveRuntimeKind.RhythmPower,
                    .25f,
                    .01f,
                    requiredHits: 3);
                ConfigureSkills(draft, MonsterRarity.Rare, template, null);
                var serialized = new SerializedObject(draft);
                serialized.FindProperty("passiveTuning").FindPropertyRelative("primaryBase").floatValue = .40f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(template.PrimaryBase, Is.EqualTo(.25f).Within(.0001f));
                var verify = new SerializedObject(draft).FindProperty("passiveTuning");
                Assert.That(verify.FindPropertyRelative("primaryBase").floatValue, Is.EqualTo(.40f).Within(.0001f));
                Assert.That(verify.FindPropertyRelative("triggerCount").intValue, Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(template);
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void SkillAugment_EnhancesExistingSlotWithoutCreatingAnotherActiveMode()
        {
            var passiveAugment = ScriptableObject.CreateInstance<MonsterAbilityDefinition>();
            var activeAugment = ScriptableObject.CreateInstance<MonsterAbilityDefinition>();
            try
            {
                passiveAugment.EditorConfigureSkillAugment(
                    "probe_passive_a2",
                    "패시브 효과량 강화",
                    MonsterSkillAugmentTarget.Passive,
                    MonsterSkillAugmentOperation.MagnitudeMultiplier,
                    0.2f,
                    1);
                activeAugment.EditorConfigureSkillAugment(
                    "probe_active_a4",
                    "액티브 반복 강화",
                    MonsterSkillAugmentTarget.Active,
                    MonsterSkillAugmentOperation.RepeatCountBonus,
                    0f,
                    1);

                Assert.That(passiveAugment.TryValidate(out var passiveError), Is.True, passiveError);
                Assert.That(activeAugment.TryValidate(out var activeError), Is.True, activeError);
                Assert.That(passiveAugment.IsSkillAugment, Is.True);
                Assert.That(passiveAugment.AugmentTarget, Is.EqualTo(MonsterSkillAugmentTarget.Passive));
                Assert.That(activeAugment.AugmentTarget, Is.EqualTo(MonsterSkillAugmentTarget.Active));
                Assert.That(activeAugment.Mode, Is.EqualTo(MonsterAbilityMode.Passive));
                Assert.That(activeAugment.TriggerPolicyId, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(activeAugment);
                UnityEngine.Object.DestroyImmediate(passiveAugment);
            }
        }

        [Test]
        public void ProductionSources_MirrorRuntimeRarityAndSkillAssignments()
        {
            var monsterCatalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(MonsterCatalogPath);
            var rarityCatalog = AssetDatabase.LoadAssetAtPath<MonsterRarityCatalog>(RarityCatalogPath);
            Assert.That(monsterCatalog, Is.Not.Null);
            Assert.That(rarityCatalog, Is.Not.Null);
            Assert.That(monsterCatalog.TryValidate(out var monsterError), Is.True, monsterError);
            Assert.That(rarityCatalog.TryValidate(out var rarityError), Is.True, rarityError);

            foreach (var definition in monsterCatalog.Definitions)
            {
                var monsterId = definition.MonsterId;
                var draftPath = $"{DraftRoot}/Draft_{monsterId}.asset";
                var source = AssetDatabase.LoadAssetAtPath<ScriptableObject>(draftPath);
                Assert.That(source, Is.Not.Null, draftPath);

                var serialized = new SerializedObject(source);
                var sourceRarity = serialized.FindProperty("rarity");
                var configured = serialized.FindProperty("skillLoadoutConfigured");
                var sourcePassive = serialized.FindProperty("rarityPassiveSkill");
                var sourceActive = serialized.FindProperty("rarityActiveSkill");
                Assert.That(sourceRarity, Is.Not.Null, monsterId);
                Assert.That(configured, Is.Not.Null, monsterId);
                Assert.That(sourcePassive, Is.Not.Null, monsterId);
                Assert.That(sourceActive, Is.Not.Null, monsterId);

                Assert.That(rarityCatalog.TryGetRarity(monsterId, out var runtimeRarity), Is.True, monsterId);
                rarityCatalog.TryGetSkillLoadout(monsterId, out var runtimePassive, out var runtimeActive);
                Assert.That((MonsterRarity)sourceRarity.enumValueIndex, Is.EqualTo(runtimeRarity), monsterId);
                var tuning = serialized.FindProperty("passiveTuning");
                if (sourcePassive.objectReferenceValue is GenericMonsterPassiveSkill template &&
                    runtimePassive is GenericMonsterPassiveSkill unique &&
                    tuning != null && tuning.FindPropertyRelative("initialized").boolValue)
                {
                    Assert.That(unique.RuntimeKind, Is.EqualTo(template.RuntimeKind), monsterId);
                    Assert.That(unique, Is.Not.SameAs(template), monsterId);
                }
                else
                {
                    Assert.That(sourcePassive.objectReferenceValue, Is.SameAs(runtimePassive), monsterId);
                }
                Assert.That(sourceActive.objectReferenceValue, Is.SameAs(runtimeActive), monsterId);
                Assert.That(
                    configured.boolValue,
                    Is.EqualTo(sourcePassive.objectReferenceValue != null || sourceActive.objectReferenceValue != null),
                    monsterId);
            }
        }

        private static TSkill Get<TSkill>(MonsterSkillCatalog catalog, string skillId)
            where TSkill : MonsterSkillDefinitionBase
        {
            Assert.That(catalog.TryGet(skillId, out var skill), Is.True, skillId);
            Assert.That(skill, Is.InstanceOf<TSkill>());
            return (TSkill)skill;
        }

        private static void ConfigureSkills(
            ScriptableObject draft,
            MonsterRarity rarity,
            MonsterPassiveSkill passive,
            MonsterActiveSkill active)
        {
            var serialized = new SerializedObject(draft);
            serialized.FindProperty("rarity").enumValueIndex = (int)rarity;
            serialized.FindProperty("skillLoadoutConfigured").boolValue = true;
            serialized.FindProperty("rarityPassiveSkill").objectReferenceValue = passive;
            serialized.FindProperty("rarityActiveSkill").objectReferenceValue = active;
            if (passive is GenericMonsterPassiveSkill generic)
            {
                var tuning = serialized.FindProperty("passiveTuning");
                tuning.FindPropertyRelative("initialized").boolValue = true;
                tuning.FindPropertyRelative("runtimeKind").enumValueIndex = (int)generic.RuntimeKind;
                tuning.FindPropertyRelative("primaryBase").floatValue = generic.PrimaryBase;
                tuning.FindPropertyRelative("primaryPerLevelStep").floatValue = generic.PrimaryPerLevelStep;
                tuning.FindPropertyRelative("secondaryBase").floatValue = generic.SecondaryBase;
                tuning.FindPropertyRelative("secondaryPerLevelStep").floatValue = generic.SecondaryPerLevelStep;
                tuning.FindPropertyRelative("triggerCount").intValue = generic.TriggerCount;
                tuning.FindPropertyRelative("maxStacks").intValue = generic.MaxStacks;
                tuning.FindPropertyRelative("duration").floatValue = generic.Duration;
                tuning.FindPropertyRelative("cooldown").floatValue = generic.Cooldown;
                tuning.FindPropertyRelative("threshold").floatValue = generic.Threshold;
                tuning.FindPropertyRelative("radius").floatValue = generic.Radius;
                tuning.FindPropertyRelative("maxTargets").intValue = generic.MaxTargets;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string[] GetMakerIssueCodes(ScriptableObject draft)
        {
            var validatorType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerValidator");
            var report = validatorType.GetMethod("Validate", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new object[] { draft });
            var issues = report?.GetType().GetProperty("Issues")?.GetValue(report) as IEnumerable;
            Assert.That(issues, Is.Not.Null);
            return issues.Cast<object>()
                .Select(issue => issue.GetType().GetProperty("Code")?.GetValue(issue) as string)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToArray();
        }

        private static Type FindEditorType(string fullName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, fullName + " 형식을 찾지 못했습니다.");
            return type;
        }
    }
}
