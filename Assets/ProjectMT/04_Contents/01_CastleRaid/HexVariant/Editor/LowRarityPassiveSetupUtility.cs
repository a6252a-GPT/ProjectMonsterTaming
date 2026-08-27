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
            new PassiveProfile("nth_hit_power", GenericMonsterPassiveRuntimeKind.RhythmPower, .25f, .01f, trigger: 4),
            new PassiveProfile("same_target_haste", GenericMonsterPassiveRuntimeKind.SameTargetHaste, .015f, .001f, trigger: 1, stacks: 4, duration: 3f),
            new PassiveProfile("nth_hit_splash", GenericMonsterPassiveRuntimeKind.RallySplash, .28f, .01f, trigger: 4, radius: 1.2f, maxTargets: 2),
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
                ["shell_01"] = "crisis_defense",
                ["aru_01"] = "entry_shield",
                ["rage_01"] = "first_wave",
                ["dubi_01"] = "weakpoint_stack",
                ["poi_poison_01"] = "ranged_hunter",
                ["pipi_01"] = "same_target_haste",
                ["nerea_01"] = "nth_hit_splash",
                ["doomba_01"] = "kill_heal",
                ["argo_01"] = "nth_hit_power",
                ["grimpy_01"] = "weakpoint_stack",
                ["rako_01"] = "courage_aura",
                ["hanjaemon_ice_01"] = "ranged_hunter",
                ["kutan_01"] = "weakpoint_stack",
                ["astell_01"] = "nth_hit_splash",
                ["candy_tree_01"] = "formation_bond",
                ["rubea_01"] = "kill_heal",
                ["lumi_01"] = "same_target_haste",
                ["krabi_01"] = "long_range_aim",
                ["shakun_01"] = "low_hp_hunter",
                ["ru_01"] = "nth_hit_heal",
                ["pango_01"] = "formation_bond",
                ["berkan_01"] = "same_target_haste"
            };

        private static readonly Dictionary<string, MainAiProfileData> MainAiProfiles =
            new Dictionary<string, MainAiProfileData>(StringComparer.OrdinalIgnoreCase)
            {
                ["piru_01"] = Main(MainBattleMonsterRole.Vanguard, UnitTargetPriority.Nearest, .72f, 0f, .20f),
                ["kir_01"] = Main(MainBattleMonsterRole.BacklineHunter, UnitTargetPriority.RangedFirst, .70f, 0f, .18f),
                ["wispy_01"] = Main(MainBattleMonsterRole.Marksman, UnitTargetPriority.Nearest, .90f, .45f, .24f),
                ["shell_01"] = Main(MainBattleMonsterRole.Vanguard, UnitTargetPriority.Nearest, .72f, 0f, .20f),
                ["aru_01"] = Main(MainBattleMonsterRole.Guardian, UnitTargetPriority.Nearest, .66f, 0f, .16f),
                ["rage_01"] = Main(MainBattleMonsterRole.Finisher, UnitTargetPriority.LowestHealth, .68f, 0f, .18f),
                ["dubi_01"] = Main(MainBattleMonsterRole.Vanguard, UnitTargetPriority.Nearest, .68f, 0f, .28f),
                ["poi_poison_01"] = Main(MainBattleMonsterRole.BacklineHunter, UnitTargetPriority.RangedFirst, .70f, 0f, .18f),
                ["pipi_01"] = Main(MainBattleMonsterRole.Marksman, UnitTargetPriority.Nearest, .92f, .55f, .26f),
                ["nerea_01"] = Main(MainBattleMonsterRole.Vanguard, UnitTargetPriority.Nearest, .66f, 0f, .18f),
                ["doomba_01"] = Main(MainBattleMonsterRole.Vanguard, UnitTargetPriority.Nearest, .66f, 0f, .18f),
                ["argo_01"] = Main(MainBattleMonsterRole.Marksman, UnitTargetPriority.Nearest, .90f, .45f, .24f),
                ["grimpy_01"] = Main(MainBattleMonsterRole.Vanguard, UnitTargetPriority.Nearest, .68f, 0f, .28f),
                ["rako_01"] = Main(MainBattleMonsterRole.BacklineHunter, UnitTargetPriority.RangedFirst, .70f, 0f, .18f),
                ["hanjaemon_ice_01"] = Main(MainBattleMonsterRole.Marksman, UnitTargetPriority.Nearest, .86f, .30f, .28f),
                ["kutan_01"] = Main(MainBattleMonsterRole.Vanguard, UnitTargetPriority.Nearest, .68f, 0f, .28f),
                ["astell_01"] = Main(MainBattleMonsterRole.Marksman, UnitTargetPriority.Nearest, .90f, .45f, .24f),
                ["candy_tree_01"] = Main(MainBattleMonsterRole.Marksman, UnitTargetPriority.Nearest, .86f, .30f, .28f),
                ["rubea_01"] = Main(MainBattleMonsterRole.Marksman, UnitTargetPriority.Nearest, .90f, .45f, .24f),
                ["lumi_01"] = Main(MainBattleMonsterRole.Vanguard, UnitTargetPriority.Nearest, .68f, 0f, .28f),
                ["krabi_01"] = Main(MainBattleMonsterRole.Marksman, UnitTargetPriority.Nearest, .95f, .55f, .28f),
                ["shakun_01"] = Main(MainBattleMonsterRole.Finisher, UnitTargetPriority.LowestHealth, .68f, 0f, .18f),
                ["ru_01"] = Main(MainBattleMonsterRole.Marksman, UnitTargetPriority.Nearest, .90f, .45f, .24f),
                ["pango_01"] = Main(MainBattleMonsterRole.Guardian, UnitTargetPriority.Nearest, .66f, 0f, .16f),
                ["berkan_01"] = Main(MainBattleMonsterRole.Vanguard, UnitTargetPriority.Nearest, .68f, 0f, .28f)
            };

        private static readonly Dictionary<string, HexAiProfileData> HexAiProfiles =
            new Dictionary<string, HexAiProfileData>(StringComparer.OrdinalIgnoreCase)
            {
                ["piru_01"] = Hex(HexCastleAssaultPattern.GeneralAdvance),
                ["kir_01"] = Hex(HexCastleAssaultPattern.DefenderHunter),
                ["wispy_01"] = Hex(HexCastleAssaultPattern.TurretHunter),
                ["shell_01"] = Hex(HexCastleAssaultPattern.GeneralAdvance),
                ["aru_01"] = Hex(HexCastleAssaultPattern.TacticalSupport, HexCastleAssaultSupportFocus.DefenseBuff),
                ["rage_01"] = Hex(HexCastleAssaultPattern.ThreatSuppressor),
                ["dubi_01"] = Hex(HexCastleAssaultPattern.ResourceRaider),
                ["poi_poison_01"] = Hex(HexCastleAssaultPattern.TurretHunter),
                ["pipi_01"] = Hex(HexCastleAssaultPattern.GeneralAdvance),
                ["nerea_01"] = Hex(HexCastleAssaultPattern.DefenderHunter),
                ["doomba_01"] = Hex(HexCastleAssaultPattern.WallBreaker),
                ["argo_01"] = Hex(HexCastleAssaultPattern.GeneralAdvance),
                ["grimpy_01"] = Hex(HexCastleAssaultPattern.ResourceRaider),
                ["rako_01"] = Hex(HexCastleAssaultPattern.DefenderHunter),
                ["hanjaemon_ice_01"] = Hex(HexCastleAssaultPattern.TurretHunter),
                ["kutan_01"] = Hex(HexCastleAssaultPattern.WallBreaker),
                ["astell_01"] = Hex(HexCastleAssaultPattern.GeneralAdvance),
                ["candy_tree_01"] = Hex(HexCastleAssaultPattern.GeneralAdvance),
                ["rubea_01"] = Hex(HexCastleAssaultPattern.GeneralAdvance),
                ["lumi_01"] = Hex(HexCastleAssaultPattern.ThreatSuppressor),
                ["krabi_01"] = Hex(HexCastleAssaultPattern.ResourceRaider),
                ["shakun_01"] = Hex(HexCastleAssaultPattern.ThreatSuppressor),
                ["ru_01"] = Hex(HexCastleAssaultPattern.TacticalSupport, HexCastleAssaultSupportFocus.Recovery),
                ["pango_01"] = Hex(HexCastleAssaultPattern.GeneralAdvance),
                ["berkan_01"] = Hex(HexCastleAssaultPattern.DefenderHunter)
            };

        [MenuItem("JC Tool/Monster/Apply Low Rarity Passive Plan")]
        public static void Apply()
        {
            var passives = ConfigurePassives(false, out var initialized);
            AssignPassives(passives);
            AssignDraftPassives(passives);
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
                    "Monster Maker에서 조절한 공용 패시브 수치 14개를 기획 초기값으로 되돌립니다. 계속할까요?",
                    "14개 초기화",
                    "취소"))
            {
                return;
            }

            ConfigurePassives(true, out var initialized);
            Debug.Log($"Low rarity passive defaults reset. Passives={initialized}");
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

        private static void AssignPassives(IReadOnlyDictionary<string, GenericMonsterPassiveSkill> passives)
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
                if (monster == null || !MonsterPassives.TryGetValue(monster.MonsterId, out var passiveId))
                {
                    continue;
                }
                entry.FindPropertyRelative("passiveSkill").objectReferenceValue = passives[passiveId];
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

        private static void AssignDraftPassives(IReadOnlyDictionary<string, GenericMonsterPassiveSkill> passives)
        {
            var assigned = 0;
            var guids = AssetDatabase.FindAssets("t:MonsterMakerDraft", new[] { DraftRoot });
            for (var index = 0; index < guids.Length; index++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[index]);
                var draft = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (draft == null)
                {
                    continue;
                }

                var serialized = new SerializedObject(draft);
                var monsterId = serialized.FindProperty("monsterId")?.stringValue;
                if (string.IsNullOrWhiteSpace(monsterId) ||
                    !MonsterPassives.TryGetValue(monsterId, out var passiveId))
                {
                    continue;
                }

                serialized.FindProperty("skillLoadoutConfigured").boolValue = true;
                serialized.FindProperty("rarityPassiveSkill").objectReferenceValue = passives[passiveId];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(draft);
                AssetDatabase.SaveAssetIfDirty(draft);
                assigned++;
            }

            if (assigned != MonsterPassives.Count)
            {
                throw new InvalidOperationException(
                    $"Draft passive assignment count mismatch. Expected={MonsterPassives.Count}, Actual={assigned}");
            }
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
