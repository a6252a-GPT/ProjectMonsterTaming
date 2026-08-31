using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    public static class LowRarityPassiveSetupUtility
    {
        private const string PassiveFolder = "Assets/ProjectMT/02_Shared/Unit/Data/Skills/Passive";
        private const string RarityCatalogPath = "Assets/ProjectMT/02_Shared/Unit/Data/MonsterRarityCatalog.asset";
        private const string DraftRoot = "Assets/ProjectMT/Editor/MonsterMaker/Drafts";
        private const string MainAiPath =
            "Assets/ProjectMT/03_Features/MainBattle/Resources/MainBattleAIProfileCatalog.asset";
        private const string HexAiPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Resources/HexCastleAssaultAIProfileCatalog.asset";

        private readonly struct PassiveProfile
        {
            public PassiveProfile(
                string id,
                GenericMonsterPassiveRuntimeKind kind,
                float primary,
                float primaryStep,
                float secondary = 0f,
                float secondaryStep = 0f,
                int trigger = 1,
                int stacks = 1,
                float duration = 0f,
                float cooldown = 0f,
                float threshold = 0f,
                float radius = 0f,
                int maxTargets = 1)
            {
                Id = id;
                Kind = kind;
                Primary = primary;
                PrimaryStep = primaryStep;
                Secondary = secondary;
                SecondaryStep = secondaryStep;
                Trigger = trigger;
                Stacks = stacks;
                Duration = duration;
                Cooldown = cooldown;
                Threshold = threshold;
                Radius = radius;
                MaxTargets = maxTargets;
            }

            public string Id { get; }
            public GenericMonsterPassiveRuntimeKind Kind { get; }
            public float Primary { get; }
            public float PrimaryStep { get; }
            public float Secondary { get; }
            public float SecondaryStep { get; }
            public int Trigger { get; }
            public int Stacks { get; }
            public float Duration { get; }
            public float Cooldown { get; }
            public float Threshold { get; }
            public float Radius { get; }
            public int MaxTargets { get; }
        }

        private readonly struct MainAiProfileData
        {
            public MainAiProfileData(
                MainBattleMonsterRole role,
                UnitTargetPriority priority,
                float preferred,
                float retreat,
                float retarget)
            {
                Role = role;
                Priority = priority;
                Preferred = preferred;
                Retreat = retreat;
                Retarget = retarget;
            }
            public MainBattleMonsterRole Role { get; }
            public UnitTargetPriority Priority { get; }
            public float Preferred { get; }
            public float Retreat { get; }
            public float Retarget { get; }
        }

        private readonly struct HexAiProfileData
        {
            public HexAiProfileData(HexCastleAssaultPattern pattern, HexCastleAssaultSupportFocus focus = HexCastleAssaultSupportFocus.Adaptive)
            {
                Pattern = pattern;
                Focus = focus;
            }
            public HexCastleAssaultPattern Pattern { get; }
            public HexCastleAssaultSupportFocus Focus { get; }
        }

        private static readonly PassiveProfile[] PassiveProfiles =
        {
            new PassiveProfile("nth_hit_power", GenericMonsterPassiveRuntimeKind.RhythmPower, .25f, .01f, trigger: 3),
            new PassiveProfile("same_target_haste", GenericMonsterPassiveRuntimeKind.SameTargetHaste, .015f, .001f, trigger: 1, stacks: 4, duration: 3f),
            new PassiveProfile("impact_strike", GenericMonsterPassiveRuntimeKind.ImpactStrike, .28f, .01f, trigger: 4, duration: .2f),
            new PassiveProfile("low_hp_hunter", GenericMonsterPassiveRuntimeKind.LowHealthHunter, .08f, .005f, threshold: .35f),
            new PassiveProfile("long_range_aim", GenericMonsterPassiveRuntimeKind.LongRangeAim, .07f, .005f, threshold: 4f),
            new PassiveProfile("crisis_defense", GenericMonsterPassiveRuntimeKind.CrisisDefense, .08f, .005f, duration: 5f, cooldown: 12f, threshold: .35f),
            new PassiveProfile("formation_bond", GenericMonsterPassiveRuntimeKind.FrontlineBond, .04f, .002f, radius: 2.8f),
            new PassiveProfile("weakpoint_stack", GenericMonsterPassiveRuntimeKind.FractureMark, .03f, .002f, trigger: 4, duration: 5f),
            new PassiveProfile("ranged_hunter", GenericMonsterPassiveRuntimeKind.ThreatMark, .08f, .005f, .03f, .002f, duration: 5f),
            new PassiveProfile("kill_heal", GenericMonsterPassiveRuntimeKind.KillHeal, .04f, .002f, cooldown: 1f),
            new PassiveProfile("courage_aura", GenericMonsterPassiveRuntimeKind.CourageAura, .03f, .002f),
            new PassiveProfile("nth_hit_heal", GenericMonsterPassiveRuntimeKind.HealingShot, .20f, .01f, trigger: 5),
            new PassiveProfile("entry_shield", GenericMonsterPassiveRuntimeKind.EmergencyEntry, .06f, .003f, .03f, .002f, duration: 6f),
            new PassiveProfile("first_wave", GenericMonsterPassiveRuntimeKind.FirstWave, .08f, .005f, duration: 8f)
        };

        private static readonly Dictionary<string, string> MonsterPassives =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["piru_01"] = "nth_hit_power",
                ["kir_01"] = "ranged_hunter",
                ["wispy_01"] = "long_range_aim",
                ["rabi_01"] = "crisis_defense",
                ["aru_01"] = "entry_shield",
                ["rage_01"] = "first_wave",
                ["dubi_01"] = "weakpoint_stack",
                ["poi_poison_01"] = "ranged_hunter",
                ["pipi_01"] = "same_target_haste",
                ["chamchi_01"] = "impact_strike",
                ["doomba_01"] = "kill_heal",
                ["argo_01"] = "nth_hit_power",
                ["grimpy_01"] = "weakpoint_stack",
                ["rako_01"] = "courage_aura",
                ["hanjaemon_ice_01"] = "ranged_hunter",
                ["kutan_01"] = "weakpoint_stack",
                ["astell_01"] = "impact_strike",
                ["candy_tree_01"] = "formation_bond",
                ["phoenix_01"] = "kill_heal",
                ["lumi_01"] = "same_target_haste",
                ["krabi_01"] = "long_range_aim",
                ["shakun_01"] = "low_hp_hunter",
                ["rabi_queen_01"] = "nth_hit_heal",
                ["pango_01"] = "formation_bond",
                ["berkan_01"] = "same_target_haste"
            };

        private static readonly Dictionary<string, MainAiProfileData> MainAiProfiles =
            new Dictionary<string, MainAiProfileData>(StringComparer.OrdinalIgnoreCase)
            {
                ["piru_01"] = Main(MainBattleMonsterRole.Vanguard, UnitTargetPriority.Nearest, .72f, 0f, .20f),
                ["kir_01"] = Main(MainBattleMonsterRole.BacklineHunter, UnitTargetPriority.RangedFirst, .70f, 0f, .18f),
                ["wispy_01"] = Main(MainBattleMonsterRole.Marksman, UnitTargetPriority.Nearest, .90f, .45f, .24f),
                ["rabi_01"] = Main(MainBattleMonsterRole.Vanguard, UnitTargetPriority.Nearest, .72f, 0f, .20f),
                ["aru_01"] = Main(MainBattleMonsterRole.Guardian, UnitTargetPriority.Nearest, .66f, 0f, .16f),
                ["rage_01"] = Main(MainBattleMonsterRole.Finisher, UnitTargetPriority.LowestHealth, .68f, 0f, .18f),
                ["dubi_01"] = Main(MainBattleMonsterRole.Vanguard, UnitTargetPriority.Nearest, .68f, 0f, .28f),
                ["poi_poison_01"] = Main(MainBattleMonsterRole.BacklineHunter, UnitTargetPriority.RangedFirst, .70f, 0f, .18f),
                ["pipi_01"] = Main(MainBattleMonsterRole.Marksman, UnitTargetPriority.Nearest, .92f, .55f, .26f),
                ["chamchi_01"] = Main(MainBattleMonsterRole.Vanguard, UnitTargetPriority.Nearest, .66f, 0f, .18f),
                ["doomba_01"] = Main(MainBattleMonsterRole.Vanguard, UnitTargetPriority.Nearest, .66f, 0f, .18f),
                ["argo_01"] = Main(MainBattleMonsterRole.Marksman, UnitTargetPriority.Nearest, .90f, .45f, .24f),
                ["grimpy_01"] = Main(MainBattleMonsterRole.Vanguard, UnitTargetPriority.Nearest, .68f, 0f, .28f),
                ["rako_01"] = Main(MainBattleMonsterRole.BacklineHunter, UnitTargetPriority.RangedFirst, .70f, 0f, .18f),
                ["hanjaemon_ice_01"] = Main(MainBattleMonsterRole.Marksman, UnitTargetPriority.Nearest, .86f, .30f, .28f),
                ["kutan_01"] = Main(MainBattleMonsterRole.Vanguard, UnitTargetPriority.Nearest, .68f, 0f, .28f),
                ["astell_01"] = Main(MainBattleMonsterRole.Marksman, UnitTargetPriority.Nearest, .90f, .45f, .24f),
                ["candy_tree_01"] = Main(MainBattleMonsterRole.Marksman, UnitTargetPriority.Nearest, .86f, .30f, .28f),
                ["phoenix_01"] = Main(MainBattleMonsterRole.Marksman, UnitTargetPriority.Nearest, .90f, .45f, .24f),
                ["lumi_01"] = Main(MainBattleMonsterRole.Vanguard, UnitTargetPriority.Nearest, .68f, 0f, .28f),
                ["krabi_01"] = Main(MainBattleMonsterRole.Marksman, UnitTargetPriority.Nearest, .95f, .55f, .28f),
                ["shakun_01"] = Main(MainBattleMonsterRole.Finisher, UnitTargetPriority.LowestHealth, .68f, 0f, .18f),
                ["rabi_queen_01"] = Main(MainBattleMonsterRole.Marksman, UnitTargetPriority.Nearest, .90f, .45f, .24f),
                ["pango_01"] = Main(MainBattleMonsterRole.Guardian, UnitTargetPriority.Nearest, .66f, 0f, .16f),
                ["berkan_01"] = Main(MainBattleMonsterRole.Vanguard, UnitTargetPriority.Nearest, .68f, 0f, .28f)
            };

        private static readonly Dictionary<string, HexAiProfileData> HexAiProfiles =
            new Dictionary<string, HexAiProfileData>(StringComparer.OrdinalIgnoreCase)
            {
                ["piru_01"] = Hex(HexCastleAssaultPattern.GeneralAdvance),
                ["kir_01"] = Hex(HexCastleAssaultPattern.DefenderHunter),
                ["wispy_01"] = Hex(HexCastleAssaultPattern.TurretHunter),
                ["rabi_01"] = Hex(HexCastleAssaultPattern.GeneralAdvance),
                ["aru_01"] = Hex(HexCastleAssaultPattern.TacticalSupport, HexCastleAssaultSupportFocus.DefenseBuff),
                ["rage_01"] = Hex(HexCastleAssaultPattern.ThreatSuppressor),
                ["dubi_01"] = Hex(HexCastleAssaultPattern.ResourceRaider),
                ["poi_poison_01"] = Hex(HexCastleAssaultPattern.TurretHunter),
                ["pipi_01"] = Hex(HexCastleAssaultPattern.GeneralAdvance),
                ["chamchi_01"] = Hex(HexCastleAssaultPattern.DefenderHunter),
                ["doomba_01"] = Hex(HexCastleAssaultPattern.WallBreaker),
                ["argo_01"] = Hex(HexCastleAssaultPattern.GeneralAdvance),
                ["grimpy_01"] = Hex(HexCastleAssaultPattern.ResourceRaider),
                ["rako_01"] = Hex(HexCastleAssaultPattern.DefenderHunter),
                ["hanjaemon_ice_01"] = Hex(HexCastleAssaultPattern.TurretHunter),
                ["kutan_01"] = Hex(HexCastleAssaultPattern.WallBreaker),
                ["astell_01"] = Hex(HexCastleAssaultPattern.GeneralAdvance),
                ["candy_tree_01"] = Hex(HexCastleAssaultPattern.GeneralAdvance),
                ["phoenix_01"] = Hex(HexCastleAssaultPattern.GeneralAdvance),
                ["lumi_01"] = Hex(HexCastleAssaultPattern.ThreatSuppressor),
                ["krabi_01"] = Hex(HexCastleAssaultPattern.ResourceRaider),
                ["shakun_01"] = Hex(HexCastleAssaultPattern.ThreatSuppressor),
                ["rabi_queen_01"] = Hex(HexCastleAssaultPattern.TacticalSupport, HexCastleAssaultSupportFocus.Recovery),
                ["pango_01"] = Hex(HexCastleAssaultPattern.GeneralAdvance),
                ["berkan_01"] = Hex(HexCastleAssaultPattern.DefenderHunter)
            };

        [MenuItem("JC Tool/Monster/Apply Low Rarity Passive Plan")]
        public static void Apply()
        {
            ValidateApplyTargets();
            var passives = ConfigurePassives(false, out var initialized);
            var monsterPassives = AssignDraftPassives(passives, false);
            AssignPassives(monsterPassives);
            ConfigureMainAi();
            ConfigureHexAi();
            Debug.Log(
                $"Low rarity passive plan applied. Passives={passives.Count}, Initialized={initialized}, " +
                $"Monsters={MonsterPassives.Count}, Drafts={MonsterPassives.Count}");
        }

        [MenuItem("JC Tool/Monster/Reset Low Rarity Passive Defaults")]
        public static void ResetPassiveDefaults()
        {
            if (!EditorUtility.DisplayDialog(
                    "저등급 패시브 공용값 초기화",
                    "일반·희귀·영웅 몬스터 25종의 전용 패시브 수치를 기획 초기값으로 되돌립니다. 계속할까요?",
                    "25종 초기화",
                    "취소"))
            {
                return;
            }

            ValidatePassiveAssets();
            var passives = ConfigurePassives(true, out var initialized);
            var monsterPassives = AssignDraftPassives(passives, true);
            AssignPassives(monsterPassives);
            Debug.Log($"Low rarity passive defaults reset. Templates={initialized}, Monsters={monsterPassives.Count}");
        }

        private static void ValidateApplyTargets()
        {
            ValidatePassiveAssets();

            var plannedMonsterIds = new HashSet<string>(MonsterPassives.Keys, StringComparer.OrdinalIgnoreCase);
            if (!plannedMonsterIds.SetEquals(MainAiProfiles.Keys) ||
                !plannedMonsterIds.SetEquals(HexAiProfiles.Keys))
            {
                throw new InvalidOperationException(
                    "Low rarity plan target sets do not match between passive, MainBattle AI, and CastleRaid AI.");
            }

            var passiveIds = new HashSet<string>(PassiveProfiles.Select(value => value.Id), StringComparer.OrdinalIgnoreCase);
            var unknownPassive = MonsterPassives.Values.FirstOrDefault(value => !passiveIds.Contains(value));
            if (!string.IsNullOrWhiteSpace(unknownPassive))
            {
                throw new InvalidOperationException($"Low rarity plan references an unknown passive: {unknownPassive}");
            }

            var rarityCatalog = AssetDatabase.LoadAssetAtPath<MonsterRarityCatalog>(RarityCatalogPath);
            if (rarityCatalog == null)
            {
                throw new InvalidOperationException($"Rarity catalog is missing: {RarityCatalogPath}");
            }

            var rarityCounts = rarityCatalog.CommonToEpicEntries
                .Where(value => value?.Monster != null)
                .GroupBy(value => value.Monster.MonsterId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(value => value.Key, value => value.Count(), StringComparer.OrdinalIgnoreCase);
            foreach (var monsterId in plannedMonsterIds)
            {
                if (!rarityCounts.TryGetValue(monsterId, out var count) || count != 1)
                {
                    throw new InvalidOperationException(
                        $"Rarity catalog must contain the low rarity target exactly once. Monster={monsterId}, Count={count}");
                }

                var draftPath = $"{DraftRoot}/Draft_{monsterId}.asset";
                var draft = AssetDatabase.LoadAssetAtPath<ScriptableObject>(draftPath);
                if (draft == null)
                {
                    throw new InvalidOperationException($"Monster Maker source is missing: {draftPath}");
                }

                var serialized = new SerializedObject(draft);
                var idProperty = serialized.FindProperty("monsterId");
                var passiveUsageProperty = serialized.FindProperty("usePassiveSkill");
                var activeUsageProperty = serialized.FindProperty("useActiveSkill");
                var splitUsageProperty = serialized.FindProperty("splitSkillUsageConfigured");
                var passiveProperty = serialized.FindProperty("rarityPassiveSkill");
                if (idProperty == null || passiveUsageProperty == null ||
                    activeUsageProperty == null || splitUsageProperty == null ||
                    passiveProperty == null)
                {
                    throw new InvalidOperationException($"Monster Maker source schema is invalid: {draftPath}");
                }

                if (!string.Equals(idProperty.stringValue, monsterId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Monster Maker source ID does not match its canonical path. Expected={monsterId}, Actual={idProperty.stringValue}");
                }
            }

            if (AssetDatabase.LoadAssetAtPath<MainBattleAIProfileCatalog>(MainAiPath) == null)
            {
                throw new InvalidOperationException($"Main AI catalog is missing: {MainAiPath}");
            }

            if (AssetDatabase.LoadAssetAtPath<HexCastleAssaultAIProfileCatalog>(HexAiPath) == null)
            {
                throw new InvalidOperationException($"Hex AI catalog is missing: {HexAiPath}");
            }
        }

        private static void ValidatePassiveAssets()
        {
            foreach (var profile in PassiveProfiles)
            {
                var path = $"{PassiveFolder}/MP_passive_{profile.Id}.asset";
                if (AssetDatabase.LoadAssetAtPath<GenericMonsterPassiveSkill>(path) == null)
                {
                    throw new InvalidOperationException($"Passive asset is missing: {path}");
                }
            }
        }

        private static Dictionary<string, GenericMonsterPassiveSkill> ConfigurePassives(
            bool forceDefaults,
            out int initialized)
        {
            var result = new Dictionary<string, GenericMonsterPassiveSkill>(StringComparer.OrdinalIgnoreCase);
            initialized = 0;
            foreach (var profile in PassiveProfiles)
            {
                var path = $"{PassiveFolder}/MP_passive_{profile.Id}.asset";
                var passive = AssetDatabase.LoadAssetAtPath<GenericMonsterPassiveSkill>(path);
                if (passive == null)
                {
                    throw new InvalidOperationException($"Passive asset is missing: {path}");
                }
                var changed = false;
                if (forceDefaults || passive.NeedsRuntimeInitialization)
                {
                    passive.EditorConfigureRuntime(
                        profile.Kind,
                        profile.Primary,
                        profile.PrimaryStep,
                        profile.Secondary,
                        profile.SecondaryStep,
                        profile.Trigger,
                        profile.Stacks,
                        profile.Duration,
                        profile.Cooldown,
                        profile.Threshold,
                        profile.Radius,
                        profile.MaxTargets);
                    initialized++;
                    changed = true;
                }
                if (!passive.AuthoringEnabled)
                {
                    passive.EditorSetAuthoringEnabled(true);
                    changed = true;
                }
                if (changed)
                {
                    EditorUtility.SetDirty(passive);
                    AssetDatabase.SaveAssetIfDirty(passive);
                }
                result.Add(profile.Id, passive);
            }
            return result;
        }

        private static void AssignPassives(
            IReadOnlyDictionary<string, GenericMonsterPassiveSkill> monsterPassives)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterRarityCatalog>(RarityCatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException($"Rarity catalog is missing: {RarityCatalogPath}");
            }
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("commonToEpicEntries");
            var assigned = 0;
            for (var index = 0; index < entries.arraySize; index++)
            {
                var entry = entries.GetArrayElementAtIndex(index);
                var monster = entry.FindPropertyRelative("monster").objectReferenceValue as MonsterDefinition;
                if (monster == null || !monsterPassives.TryGetValue(monster.MonsterId, out var passive))
                {
                    continue;
                }
                entry.FindPropertyRelative("passiveSkill").objectReferenceValue = passive;
                assigned++;
            }
            if (assigned != MonsterPassives.Count)
            {
                throw new InvalidOperationException(
                    $"Passive assignment count mismatch. Expected={MonsterPassives.Count}, Actual={assigned}");
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        private static Dictionary<string, GenericMonsterPassiveSkill> AssignDraftPassives(
            IReadOnlyDictionary<string, GenericMonsterPassiveSkill> passives,
            bool resetTuning)
        {
            var assigned = 0;
            var result = new Dictionary<string, GenericMonsterPassiveSkill>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in MonsterPassives)
            {
                var path = $"{DraftRoot}/Draft_{pair.Key}.asset";
                var draft = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (draft == null)
                {
                    throw new InvalidOperationException($"Monster Maker source is missing: {path}");
                }

                var template = passives[pair.Value];
                var serialized = new SerializedObject(draft);
                serialized.FindProperty("usePassiveSkill").boolValue = true;
                serialized.FindProperty("useActiveSkill").boolValue = false;
                serialized.FindProperty("splitSkillUsageConfigured").boolValue = true;
                serialized.FindProperty("rarityPassiveSkill").objectReferenceValue = template;
                var tuning = serialized.FindProperty("passiveTuning");
                var initialized = tuning.FindPropertyRelative("initialized");
                var runtimeKind = tuning.FindPropertyRelative("runtimeKind");
                if (resetTuning || !initialized.boolValue ||
                    runtimeKind.enumValueIndex != (int)template.RuntimeKind)
                {
                    CopyTuning(tuning, template);
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(draft);
                AssetDatabase.SaveAssetIfDirty(draft);
                var uniquePassive = BuildOrUpdateUniquePassive(pair.Key, template, tuning);
                if (uniquePassive == null)
                {
                    throw new InvalidOperationException($"Unique passive creation failed: {pair.Key}");
                }
                result.Add(pair.Key, uniquePassive);
                assigned++;
            }

            if (assigned != MonsterPassives.Count)
            {
                throw new InvalidOperationException(
                    $"Draft passive assignment count mismatch. Expected={MonsterPassives.Count}, Actual={assigned}");
            }
            return result;
        }

        private static GenericMonsterPassiveSkill BuildOrUpdateUniquePassive(
            string monsterId,
            GenericMonsterPassiveSkill template,
            SerializedProperty tuning)
        {
            var path = $"Assets/ProjectMT/02_Shared/Unit/Data/Monsters/{monsterId}/MP_{monsterId}_Passive.asset";
            var passive = AssetDatabase.LoadAssetAtPath<GenericMonsterPassiveSkill>(path);
            if (passive == null)
            {
                passive = ScriptableObject.CreateInstance<GenericMonsterPassiveSkill>();
                passive.name = $"MP_{monsterId}_Passive";
                AssetDatabase.CreateAsset(passive, path);
            }

            passive.EditorConfigure(
                $"{template.SkillId}_{monsterId}",
                template.DisplayName,
                template.Description,
                template.PresentationTier,
                template.Recipe,
                template.Icon);
            passive.EditorConfigureRuntime(
                (GenericMonsterPassiveRuntimeKind)tuning.FindPropertyRelative("runtimeKind").enumValueIndex,
                tuning.FindPropertyRelative("primaryBase").floatValue,
                tuning.FindPropertyRelative("primaryPerLevelStep").floatValue,
                tuning.FindPropertyRelative("secondaryBase").floatValue,
                tuning.FindPropertyRelative("secondaryPerLevelStep").floatValue,
                tuning.FindPropertyRelative("triggerCount").intValue,
                tuning.FindPropertyRelative("maxStacks").intValue,
                tuning.FindPropertyRelative("duration").floatValue,
                tuning.FindPropertyRelative("cooldown").floatValue,
                tuning.FindPropertyRelative("threshold").floatValue,
                tuning.FindPropertyRelative("radius").floatValue,
                tuning.FindPropertyRelative("maxTargets").intValue);
            passive.EditorSetAuthoringEnabled(template.AuthoringEnabled);
            EditorUtility.SetDirty(passive);
            AssetDatabase.SaveAssetIfDirty(passive);
            return passive;
        }

        private static void CopyTuning(SerializedProperty tuning, GenericMonsterPassiveSkill template)
        {
            tuning.FindPropertyRelative("initialized").boolValue = true;
            tuning.FindPropertyRelative("runtimeKind").enumValueIndex = (int)template.RuntimeKind;
            tuning.FindPropertyRelative("primaryBase").floatValue = template.PrimaryBase;
            tuning.FindPropertyRelative("primaryPerLevelStep").floatValue = template.PrimaryPerLevelStep;
            tuning.FindPropertyRelative("secondaryBase").floatValue = template.SecondaryBase;
            tuning.FindPropertyRelative("secondaryPerLevelStep").floatValue = template.SecondaryPerLevelStep;
            tuning.FindPropertyRelative("triggerCount").intValue = template.TriggerCount;
            tuning.FindPropertyRelative("maxStacks").intValue = template.MaxStacks;
            tuning.FindPropertyRelative("duration").floatValue = template.Duration;
            tuning.FindPropertyRelative("cooldown").floatValue = template.Cooldown;
            tuning.FindPropertyRelative("threshold").floatValue = template.Threshold;
            tuning.FindPropertyRelative("radius").floatValue = template.Radius;
            tuning.FindPropertyRelative("maxTargets").intValue = template.MaxTargets;
        }

        private static void ConfigureMainAi()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MainBattleAIProfileCatalog>(MainAiPath);
            if (catalog == null)
            {
                throw new InvalidOperationException($"Main AI catalog is missing: {MainAiPath}");
            }
            foreach (var pair in MainAiProfiles)
            {
                var value = pair.Value;
                catalog.EditorUpsert(pair.Key, value.Role, value.Priority, value.Preferred, value.Retreat, value.Retarget);
            }
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        private static void ConfigureHexAi()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HexCastleAssaultAIProfileCatalog>(HexAiPath);
            if (catalog == null)
            {
                throw new InvalidOperationException($"Hex AI catalog is missing: {HexAiPath}");
            }
            var values = catalog.Entries.Where(value => value != null).ToList();
            foreach (var pair in HexAiProfiles)
            {
                values.RemoveAll(value => string.Equals(value.MonsterId, pair.Key, StringComparison.OrdinalIgnoreCase));
                var profile = new HexCastleAssaultAIProfile();
                profile.EditorConfigure(
                    pair.Key,
                    pair.Value.Pattern,
                    pair.Value.Focus,
                    5f,
                    4f,
                    5f,
                    .24f,
                    .20f,
                    .75f);
                values.Add(profile);
            }
            catalog.EditorReplaceEntries(values.OrderBy(value => value.MonsterId, StringComparer.OrdinalIgnoreCase));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        private static MainAiProfileData Main(
            MainBattleMonsterRole role,
            UnitTargetPriority priority,
            float preferred,
            float retreat,
            float retarget)
        {
            return new MainAiProfileData(role, priority, preferred, retreat, retarget);
        }

        private static HexAiProfileData Hex(
            HexCastleAssaultPattern pattern,
            HexCastleAssaultSupportFocus focus = HexCastleAssaultSupportFocus.Adaptive)
        {
            return new HexAiProfileData(pattern, focus);
        }
    }
}
