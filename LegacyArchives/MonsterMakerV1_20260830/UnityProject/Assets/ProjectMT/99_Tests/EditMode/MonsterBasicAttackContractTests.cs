using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Tests.EditMode
{
    public sealed class MonsterBasicAttackContractTests // 15종 Profile·44마리 매칭·판정 표시 계약
    {
        private const string ProfileRoot = "Assets/ProjectMT/02_Shared/Unit/Data/BasicAttacks";
        private const string DraftRoot = "Assets/ProjectMT/Editor/MonsterMaker/Drafts";
        private const string MonsterDataRoot = "Assets/ProjectMT/02_Shared/Unit/Data/Monsters";
        private static readonly string[] BuiltInIds =
        {
            "BA_M_01", "BA_M_02", "BA_M_03", "BA_M_04", "BA_M_05", "BA_M_06",
            "BA_R_01", "BA_R_02", "BA_R_03", "BA_R_04", "BA_R_05",
            "BA_S_01", "BA_S_02", "BA_S_03", "BA_S_04"
        };
        private static readonly IReadOnlyDictionary<string, int> VfxSlotCounts =
            new Dictionary<string, int>
            {
                ["BA_M_01"] = 2, ["BA_M_02"] = 2, ["BA_M_03"] = 2,
                ["BA_M_04"] = 4, ["BA_M_05"] = 3, ["BA_M_06"] = 3,
                ["BA_R_01"] = 3, ["BA_R_02"] = 4, ["BA_R_03"] = 4,
                ["BA_R_04"] = 2, ["BA_R_05"] = 3,
                ["BA_S_01"] = 5, ["BA_S_02"] = 4, ["BA_S_03"] = 4, ["BA_S_04"] = 4
            };

        private static readonly IReadOnlyDictionary<string, string> Assignments =
            new Dictionary<string, string>
            {
                ["aru_01"] = "BA_M_01", ["dubi_01"] = "BA_M_01", ["kir_01"] = "BA_M_01",
                ["piru_01"] = "BA_M_01", ["poi_poison_01"] = "BA_M_01", ["rage_01"] = "BA_M_01",
                ["rabi_queen_01"] = "BA_R_01", ["rabi_01"] = "BA_M_01", ["doomba_01"] = "BA_M_02",
                ["grimpy_01"] = "BA_M_01", ["hanjaemon_ice_01"] = "BA_R_03", ["kutan_01"] = "BA_M_01",
                ["chamchi_01"] = "BA_M_02", ["rako_01"] = "BA_M_01", ["wispy_01"] = "BA_R_01",
                ["berkan_01"] = "BA_M_02", ["krabi_01"] = "BA_R_01", ["lumi_01"] = "BA_M_05",
                ["phoenix_01"] = "BA_R_05", ["shakun_01"] = "BA_M_05", ["castley_01"] = "BA_M_04",
                ["werewolf_01"] = "BA_M_02", ["mukuk_01"] = "BA_S_04", ["never_ice_01"] = "BA_R_02",
                ["silpia_01"] = "BA_S_01", ["floria_01"] = "BA_S_03", ["fryar_01"] = "BA_M_02",
                ["angeonjun_01"] = "BA_S_02", ["kimhyeona_01"] = "BA_R_02", ["lucy_01"] = "BA_M_06",
                ["mingyu_mythic_01"] = "BA_M_03", ["oster_01"] = "BA_R_03", ["pc_bear_01"] = "BA_R_04",
                ["pipi_01"] = "BA_R_01", ["berry_01"] = "BA_R_01", ["pango_01"] = "BA_M_04",
                ["ruby_01"] = "BA_M_05", ["kain_01"] = "BA_M_06", ["argo_01"] = "BA_R_05",
                ["astell_01"] = "BA_S_01", ["candy_tree_01"] = "BA_R_03", ["ignis_01"] = "BA_M_02",
                ["pyron_01"] = "BA_R_03", ["nagaris_01"] = "BA_S_02"
            };

        [Test]
        public void Profiles_UseCategorizedFileAndObjectNamesAndStayInsideCombatCaps()
        {
            var profiles = AssetDatabase.FindAssets(
                    "t:MonsterBasicAttackProfile",
                    new[] { ProfileRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterBasicAttackProfile>)
                .Where(profile => profile != null)
                .OrderBy(profile => profile.AttackId)
                .ToArray();
            var expectedIds = BuiltInIds.OrderBy(id => id).ToArray();

            Assert.That(profiles.Length, Is.EqualTo(15));
            Assert.That(profiles.Select(profile => profile.AttackId), Is.EqualTo(expectedIds));
            foreach (var profile in profiles)
            {
                Assert.That(profile.name, Is.EqualTo(profile.AttackId));
                Assert.That(
                    AssetDatabase.GetAssetPath(profile),
                    Is.EqualTo($"{ProfileRoot}/{profile.AttackId}.asset"));
            }
            foreach (var profile in profiles)
            {
                Assert.That(profile.TryValidate(out var error), Is.True, $"{profile.AttackId}: {error}");
                Assert.That(profile.DesignMemo, Is.Not.Empty, profile.AttackId);
                Assert.That(profile.AttackId, Does.Match("^BA_[MRS]_[A-Za-z0-9_]+$"), profile.AttackId);
                Assert.That(profile.MaxTargets, Is.InRange(1, MonsterBasicAttackProfile.MaximumTargets));
                Assert.That(profile.HitCount, Is.InRange(1, MonsterBasicAttackProfile.MaximumHitCount));
                Assert.That(profile.ProjectileCount, Is.InRange(1, MonsterBasicAttackProfile.MaximumProjectileCount));
            }
        }

        [Test]
        public void EveryProductionRuntimeAction_MatchesItsCurrentMakerDraft()
        {
            var draftGuids = AssetDatabase.FindAssets("t:MonsterMakerDraft", new[] { DraftRoot });
            Assert.That(draftGuids.Length, Is.EqualTo(Assignments.Count));

            foreach (var draftGuid in draftGuids)
            {
                var draftPath = AssetDatabase.GUIDToAssetPath(draftGuid);
                var draft = AssetDatabase.LoadMainAssetAtPath(draftPath) as ScriptableObject;
                Assert.That(draft, Is.Not.Null, draftPath);
                var serializedDraft = new SerializedObject(draft);
                var monsterId = serializedDraft.FindProperty("monsterId").stringValue;
                var draftProfile = serializedDraft.FindProperty("basicAttackProfile").objectReferenceValue
                    as MonsterBasicAttackProfile;
                var draftCombatType = (MonsterCombatType)serializedDraft.FindProperty("combatType").enumValueIndex;
                Assert.That(draftProfile, Is.Not.Null, $"{monsterId}/draft profile");
                Assert.That(draftCombatType, Is.EqualTo(draftProfile.CombatType), monsterId);

                var combatPath = $"{MonsterDataRoot}/{monsterId}/MC_{monsterId}.asset";
                var combat = AssetDatabase.LoadAssetAtPath<MonsterCombatProfile>(combatPath);
                Assert.That(combat, Is.Not.Null, $"{monsterId}/combat profile");
                Assert.That(combat.Action, Is.Not.Null, $"{monsterId}/combat action");
                Assert.That(combat.Action.BasicAttackProfile, Is.SameAs(draftProfile), monsterId);
                Assert.That(combat.CombatType, Is.EqualTo(draftProfile.CombatType), monsterId);
                Assert.That(combat.TryValidate(out var error), Is.True, $"{monsterId}: {error}");
            }
        }

        [Test]
        public void EveryProductionRuntimePresentation_MatchesOnlyItsCurrentMakerBindings()
        {
            var projectionType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterBasicAttackBindingProjection");
            var evaluate = projectionType.GetMethod(
                "EvaluateRuntimeSync",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static);
            Assert.That(evaluate, Is.Not.Null);

            foreach (var draftGuid in AssetDatabase.FindAssets("t:MonsterMakerDraft", new[] { DraftRoot }))
            {
                var draft = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(draftGuid));
                var monsterId = (string)draft.GetType().GetProperty("MonsterId").GetValue(draft);
                var combat = AssetDatabase.LoadAssetAtPath<MonsterCombatProfile>(
                    $"{MonsterDataRoot}/{monsterId}/MC_{monsterId}.asset");
                var feedback = AssetDatabase.LoadAssetAtPath<MonsterFeedbackProfile>(
                    $"{MonsterDataRoot}/{monsterId}/MF_{monsterId}.asset");
                var args = new object[] { draft, combat, feedback, null };
                var state = evaluate.Invoke(null, args);
                Assert.That(state.ToString(), Is.EqualTo("Synchronized"), $"{monsterId}: {args[3]}");
            }
        }

        [Test]
        public void AdvancedProfiles_CoverEveryRequestedDeliveryAndShape()
        {
            AssertProfile("BA_M_02", MonsterBasicAttackDelivery.Contact, MonsterBasicAttackShape.Fan);
            AssertProfile("BA_M_03", MonsterBasicAttackDelivery.Contact, MonsterBasicAttackShape.Line);
            AssertProfile("BA_M_04", MonsterBasicAttackDelivery.Contact, MonsterBasicAttackShape.Circle);
            AssertProfile("BA_R_02", MonsterBasicAttackDelivery.Projectile, MonsterBasicAttackShape.Line);
            AssertProfile("BA_R_03", MonsterBasicAttackDelivery.Projectile, MonsterBasicAttackShape.Circle);
            AssertProfile("BA_R_04", MonsterBasicAttackDelivery.Instant, MonsterBasicAttackShape.Single);
            AssertProfile("BA_M_05", MonsterBasicAttackDelivery.Dash, MonsterBasicAttackShape.Single);
            AssertProfile("BA_M_06", MonsterBasicAttackDelivery.MultiHit, MonsterBasicAttackShape.Single);
            AssertProfile("BA_R_05", MonsterBasicAttackDelivery.Projectile, MonsterBasicAttackShape.Fan);
            AssertProfile("BA_S_01", MonsterBasicAttackDelivery.ReturningProjectile, MonsterBasicAttackShape.Line);
            AssertProfile("BA_S_02", MonsterBasicAttackDelivery.Breath, MonsterBasicAttackShape.Fan);
            AssertProfile("BA_S_03", MonsterBasicAttackDelivery.Beam, MonsterBasicAttackShape.Line);
            AssertProfile("BA_S_04", MonsterBasicAttackDelivery.TravelingWave, MonsterBasicAttackShape.Line);
        }

        [Test]
        public void Breath_UsesProfileDurationAndAngeonjunMotionOverride()
        {
            var profile = AssetDatabase.LoadAssetAtPath<MonsterBasicAttackProfile>(
                $"{ProfileRoot}/BA_S_02.asset");
            var draft = AssetDatabase.LoadMainAssetAtPath($"{DraftRoot}/Draft_angeonjun_01.asset");
            var motion = AssetDatabase.LoadAssetAtPath<MonsterMotionProfile>(
                $"{MonsterDataRoot}/angeonjun_01/MM_angeonjun_01.asset");

            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.UsesBreathDurationContract, Is.True);
            Assert.That(profile.BreathDuration, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(profile.ResolveRepeatHitInterval(), Is.EqualTo(0.8f / 3f).Within(0.001f));
            Assert.That(draft, Is.Not.Null);
            var draftAttack = ((System.Collections.IEnumerable)draft.GetType()
                    .GetProperty("Attacks")
                    .GetValue(draft))
                .Cast<object>()
                .First();
            Assert.That((bool)draftAttack.GetType().GetProperty("OverrideBreathDuration").GetValue(draftAttack),
                Is.True);
            Assert.That((float)draftAttack.GetType().GetProperty("BreathDuration").GetValue(draftAttack),
                Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(motion, Is.Not.Null);
            Assert.That(motion.Attacks[0].OverrideBreathDuration, Is.True);
            Assert.That(motion.Attacks[0].BreathDuration, Is.EqualTo(0.8f).Within(0.001f));
        }

        [Test]
        public void BuiltInProfiles_AreTheExpectedComposableModuleRecipes()
        {
            AssertRecipe("BA_M_01", MonsterBasicAttackDeliveryModule.Direct,
                MonsterBasicAttackCollisionModule.DirectResolve, MonsterBasicAttackSequenceModule.Single,
                MonsterBasicAttackMovementModule.None, MonsterBasicAttackShape.Single);
            AssertRecipe("BA_R_01", MonsterBasicAttackDeliveryModule.Projectile,
                MonsterBasicAttackCollisionModule.StopOnFirstTarget, MonsterBasicAttackSequenceModule.Single,
                MonsterBasicAttackMovementModule.None, MonsterBasicAttackShape.Single);
            AssertRecipe("BA_M_02", MonsterBasicAttackDeliveryModule.Direct,
                MonsterBasicAttackCollisionModule.DirectResolve, MonsterBasicAttackSequenceModule.Single,
                MonsterBasicAttackMovementModule.None, MonsterBasicAttackShape.Fan);
            AssertRecipe("BA_M_03", MonsterBasicAttackDeliveryModule.Direct,
                MonsterBasicAttackCollisionModule.DirectResolve, MonsterBasicAttackSequenceModule.Single,
                MonsterBasicAttackMovementModule.None, MonsterBasicAttackShape.Line);
            AssertRecipe("BA_M_04", MonsterBasicAttackDeliveryModule.Direct,
                MonsterBasicAttackCollisionModule.DirectResolve, MonsterBasicAttackSequenceModule.Single,
                MonsterBasicAttackMovementModule.None, MonsterBasicAttackShape.Circle);
            AssertRecipe("BA_R_02", MonsterBasicAttackDeliveryModule.Projectile,
                MonsterBasicAttackCollisionModule.Pierce, MonsterBasicAttackSequenceModule.Single,
                MonsterBasicAttackMovementModule.None, MonsterBasicAttackShape.Line);
            AssertRecipe("BA_R_03", MonsterBasicAttackDeliveryModule.Projectile,
                MonsterBasicAttackCollisionModule.AreaImpact, MonsterBasicAttackSequenceModule.Single,
                MonsterBasicAttackMovementModule.None, MonsterBasicAttackShape.Circle);
            AssertRecipe("BA_R_04", MonsterBasicAttackDeliveryModule.Direct,
                MonsterBasicAttackCollisionModule.DirectResolve, MonsterBasicAttackSequenceModule.Single,
                MonsterBasicAttackMovementModule.None, MonsterBasicAttackShape.Single);
            AssertRecipe("BA_M_05", MonsterBasicAttackDeliveryModule.Direct,
                MonsterBasicAttackCollisionModule.DirectResolve, MonsterBasicAttackSequenceModule.Single,
                MonsterBasicAttackMovementModule.Dash, MonsterBasicAttackShape.Single);
            AssertRecipe("BA_M_06", MonsterBasicAttackDeliveryModule.Direct,
                MonsterBasicAttackCollisionModule.DirectResolve, MonsterBasicAttackSequenceModule.Burst,
                MonsterBasicAttackMovementModule.None, MonsterBasicAttackShape.Single);
            AssertRecipe("BA_R_05", MonsterBasicAttackDeliveryModule.Projectile,
                MonsterBasicAttackCollisionModule.StopOnFirstTarget, MonsterBasicAttackSequenceModule.Single,
                MonsterBasicAttackMovementModule.None, MonsterBasicAttackShape.Fan);
            AssertRecipe("BA_S_01", MonsterBasicAttackDeliveryModule.Projectile,
                MonsterBasicAttackCollisionModule.PassThrough, MonsterBasicAttackSequenceModule.ReturnPasses,
                MonsterBasicAttackMovementModule.None, MonsterBasicAttackShape.Line);
            AssertRecipe("BA_S_02", MonsterBasicAttackDeliveryModule.Direct,
                MonsterBasicAttackCollisionModule.DirectResolve, MonsterBasicAttackSequenceModule.Burst,
                MonsterBasicAttackMovementModule.None, MonsterBasicAttackShape.Fan);
            AssertRecipe("BA_S_03", MonsterBasicAttackDeliveryModule.Direct,
                MonsterBasicAttackCollisionModule.DirectResolve, MonsterBasicAttackSequenceModule.Single,
                MonsterBasicAttackMovementModule.None, MonsterBasicAttackShape.Line);
            AssertRecipe("BA_S_04", MonsterBasicAttackDeliveryModule.TravelingArea,
                MonsterBasicAttackCollisionModule.PassThrough, MonsterBasicAttackSequenceModule.Single,
                MonsterBasicAttackMovementModule.None, MonsterBasicAttackShape.Line);
        }

        [Test]
        public void EveryProductionMotion_HasOneRecipeActivationMarker()
        {
            var draftPaths = AssetDatabase.FindAssets("t:MonsterMakerDraft", new[] { DraftRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToArray();

            Assert.That(draftPaths.Length, Is.EqualTo(Assignments.Count));
            foreach (var path in draftPaths)
            {
                var draft = AssetDatabase.LoadMainAssetAtPath(path) as ScriptableObject;
                var serialized = new SerializedObject(draft);
                var monsterId = serialized.FindProperty("monsterId").stringValue;
                var attacks = serialized.FindProperty("attacks");
                for (var attackIndex = 0; attackIndex < attacks.arraySize; attackIndex++)
                {
                    var attack = attacks.GetArrayElementAtIndex(attackIndex);
                    var motionId = attack.FindPropertyRelative("motionId").stringValue;
                    var markers = attack.FindPropertyRelative("markers");
                    Assert.That(markers.arraySize, Is.EqualTo(1), $"{monsterId}/{motionId}");
                    Assert.That(markers.GetArrayElementAtIndex(0).FindPropertyRelative("powerRatio").floatValue,
                        Is.EqualTo(1f).Within(0.001f), $"{monsterId}/{motionId}");
                }
            }
        }

        [Test]
        public void Workshop_StartsBlankAndRoundTripsAllBuiltInPresetsThroughOneAssemblyModel()
        {
            var utilityType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterBasicAttackPresetUtility");
            var recipeType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.BasicAttackWorkshopRecipe");
            var resetBlank = recipeType.GetMethod("ResetBlank");
            var load = recipeType.GetMethod("Load");
            var compile = recipeType.GetMethod("Compile");
            var blank = System.Activator.CreateInstance(recipeType);
            resetBlank.Invoke(blank, null);

            Assert.That(recipeType.GetField("attackId").GetValue(blank), Is.EqualTo("BA_M_New"));
            Assert.That(recipeType.GetField("launchFeelPrefab").GetValue(blank), Is.Null);
            Assert.That(recipeType.GetField("projectileFeelPrefab").GetValue(blank), Is.Null);
            Assert.That(recipeType.GetField("impactFeelPrefab").GetValue(blank), Is.Null,
                "판정 Recipe가 PresentationKind를 근거로 FEEL을 자동 선택하면 안 됩니다.");
            Assert.That(utilityType.GetMethod("CreateEditableCopy"), Is.Null,
                "공식 프리셋 복제로 시작하는 이전 작성 경로가 남아 있으면 안 됩니다.");
            Assert.That(FindEditorType("ProjectMT.EditorTools.MonsterMaker.BasicAttackFeelPresetUtility")
                    .GetMethod("ResolveProductionPreset"), Is.Null,
                "판정 종류에서 FEEL 원형을 자동 추천하는 경로가 남아 있으면 안 됩니다.");

            var properties = new[]
            {
                "CombatType", "DeliveryModule", "CollisionModule", "SequenceModule", "MovementModule",
                "PresentationKind", "Shape", "ProjectileTravel", "ProjectileCount", "HitCount", "BreathDuration"
            };
            foreach (var attackId in BuiltInIds)
            {
                var source = LoadProfile(attackId);
                var recipe = System.Activator.CreateInstance(recipeType);
                load.Invoke(recipe, new object[] { source });
                var rebuilt = ScriptableObject.CreateInstance<MonsterBasicAttackProfile>();
                try
                {
                    compile.Invoke(recipe, new object[] { rebuilt });
                    Assert.That(rebuilt.TryValidate(out var error), Is.True, $"{attackId}: {error}");
                    foreach (var propertyName in properties)
                    {
                        var property = typeof(MonsterBasicAttackProfile).GetProperty(propertyName);
                        Assert.That(property.GetValue(rebuilt), Is.EqualTo(property.GetValue(source)),
                            $"{attackId}/{propertyName}");
                    }
                    Assert.That(rebuilt.LaunchFeel?.Prefab, Is.SameAs(source.LaunchFeel?.Prefab),
                        $"{attackId}/LaunchFeel");
                    Assert.That(rebuilt.ProjectileFeel?.Prefab, Is.SameAs(source.ProjectileFeel?.Prefab),
                        $"{attackId}/ProjectileFeel");
                    Assert.That(rebuilt.ImpactFeel?.Prefab, Is.SameAs(source.ImpactFeel?.Prefab),
                        $"{attackId}/ImpactFeel");
                }
                finally
                {
                    Object.DestroyImmediate(rebuilt);
                }
            }
        }

        [Test]
        public void Workshop_PreviewPlaybackReadsActualMotionMarkers()
        {
            var draft = AssetDatabase.LoadAssetAtPath<ScriptableObject>($"{DraftRoot}/Draft_lumi_01.asset");
            Assert.That(draft, Is.Not.Null);
            var draftType = draft.GetType();
            var attacks = (System.Collections.IList)draftType.GetProperty("Attacks").GetValue(draft);
            Assert.That(attacks.Count, Is.GreaterThan(0));
            var attack = attacks[0];
            var expectedDuration = ((AnimationClip)attack.GetType().GetProperty("Clip").GetValue(attack)).length /
                                   (float)attack.GetType().GetProperty("PlaybackSpeed").GetValue(attack);
            var expectedMarkers = ((System.Collections.IEnumerable)attack.GetType().GetProperty("Markers").GetValue(attack))
                .Cast<object>()
                .Select(item => (float)item.GetType().GetProperty("NormalizedTime").GetValue(item))
                .ToArray();

            var windowType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterBasicAttackWorkshopWindow");
            var window = ScriptableObject.CreateInstance(windowType);
            try
            {
                windowType.GetField("originDraft", System.Reflection.BindingFlags.Instance |
                                                   System.Reflection.BindingFlags.NonPublic)
                    .SetValue(window, draft);
                var duration = (float)windowType.GetMethod(
                        "ResolvePreviewDuration",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(window, null);
                var markers = ((System.Collections.IEnumerable)windowType.GetMethod(
                        "ResolvePreviewImpactTimes",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(window, null)).Cast<float>().ToArray();

                Assert.That(duration, Is.EqualTo(expectedDuration).Within(0.0001f));
                Assert.That(markers, Is.EqualTo(expectedMarkers));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void Workshop_SavesLoadsAndAssignsOnePresetWithPresentationSettings()
        {
            var windowType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterBasicAttackWorkshopWindow");
            var recipeType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.BasicAttackWorkshopRecipe");
            var draftType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerDraft");
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var window = ScriptableObject.CreateInstance(windowType);
            var draft = ScriptableObject.CreateInstance(draftType);
            string createdPath = null;
            try
            {
                var recipe = windowType.GetField("recipe", flags).GetValue(window);
                var id = "BA_M_TEST_" + System.Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
                recipeType.GetField("attackId").SetValue(recipe, id);
                recipeType.GetField("displayName").SetValue(recipe, "조립소 저장 계약 테스트");
                recipeType.GetField("designMemo").SetValue(recipe, "저장·불러오기·배정과 VFX 고급값을 함께 검증한다.");
                var vfx = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/ProjectMT/02_Shared/Combat/Prefabs/PF_SeedProjectile.prefab");
                Assert.That(vfx, Is.Not.Null);
                recipeType.GetField("launchVfx").SetValue(recipe, vfx);
                recipeType.GetField("launchVfxPosition").SetValue(recipe, new Vector3(0.1f, 0.2f, 0.3f));
                recipeType.GetField("launchVfxScale").SetValue(recipe, 1.25f);
                recipeType.GetField("launchVfxLifetime").SetValue(recipe, 0.8f);

                windowType.GetField("originDraft", flags).SetValue(window, draft);
                windowType.GetMethod("CompileWorkingProfile", flags).Invoke(window, null);
                windowType.GetMethod("SaveAsNew", flags).Invoke(window, null);
                var saved = windowType.GetField("loadedProfile", flags).GetValue(window) as
                    MonsterBasicAttackProfile;
                Assert.That(saved, Is.Not.Null);
                createdPath = AssetDatabase.GetAssetPath(saved);
                Assert.That(createdPath, Is.EqualTo($"{ProfileRoot}/Custom/{id}.asset"));
                Assert.That(saved.AttackId, Is.EqualTo(id));
                Assert.That(saved.DesignMemo, Does.Contain("저장·불러오기·배정"));
                Assert.That(saved.LaunchFeedback, Is.Not.Null);
                Assert.That(saved.LaunchFeedback.VfxPrefab, Is.SameAs(vfx));
                Assert.That(saved.LaunchFeedback.LocalPosition, Is.EqualTo(new Vector3(0.1f, 0.2f, 0.3f)));
                Assert.That(saved.LaunchFeedback.Scale, Is.EqualTo(1.25f).Within(0.001f));
                Assert.That(saved.LaunchFeedback.VfxLifetime, Is.EqualTo(0.8f).Within(0.001f));

                windowType.GetMethod("LoadProfile", flags).Invoke(window, new object[] { saved });
                windowType.GetMethod("SetWorkCopyDirty", flags).Invoke(window, new object[] { true });
                Assert.That(((EditorWindow)window).hasUnsavedChanges, Is.True,
                    "기본공격 작업 사본 편집도 창 닫기와 프리셋 전환 보호 상태를 사용해야 합니다.");
                windowType.GetMethod("SetWorkCopyDirty", flags).Invoke(window, new object[] { false });
                Assert.That(((EditorWindow)window).hasUnsavedChanges, Is.False);
                windowType.GetMethod("AssignLoadedToOrigin", flags).Invoke(window, null);
                var assigned = draftType.GetProperty("BasicAttackProfile").GetValue(draft);
                Assert.That(assigned, Is.SameAs(saved));
            }
            finally
            {
                Object.DestroyImmediate(draft);
                Object.DestroyImmediate(window);
                if (!string.IsNullOrWhiteSpace(createdPath))
                {
                    AssetDatabase.DeleteAsset(createdPath);
                }
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void MonsterSpecificMotionImpactFeedbackOverridesSharedPresetImpactFeedback()
        {
            var sharedVfx = new GameObject("SharedImpactVfx");
            var motionVfx = new GameObject("MotionImpactVfx");
            var profile = ScriptableObject.CreateInstance<MonsterBasicAttackProfile>();
            try
            {
                var sharedCue = new MonsterFeedbackCue();
                sharedCue.EditorConfigure(null, sharedVfx);
                profile.EditorSetPresentationFeedback(null, null, sharedCue);
                var motionCue = new MonsterFeedbackCue();
                motionCue.EditorConfigure(null, motionVfx);
                var marker = new MonsterAttackMarker();
                marker.EditorConfigure(0.5f, 1f, motionCue);
                var context = new MonsterActionExecutionContext(
                    null,
                    null,
                    null,
                    default,
                    null,
                    marker,
                    null);
                var method = typeof(MonsterBasicAttackExecutor).GetMethod(
                    "ResolveImpactFeedback",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

                Assert.That(method, Is.Not.Null);
                Assert.That(method.Invoke(null, new object[] { context, profile }), Is.SameAs(motionCue));
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(sharedVfx);
                Object.DestroyImmediate(motionVfx);
            }
        }

        [Test]
        public void EveryProfile_HitAreaIndicatorBuildsVisibleWorldGeometry()
        {
            var parent = new GameObject("BasicAttackIndicatorTest");
            try
            {
                foreach (var attackId in BuiltInIds)
                {
                    var profile = LoadProfile(attackId);
                    var indicator = MonsterAttackAreaIndicator.Create(
                        parent.transform,
                        profile,
                        Vector3.zero,
                        Vector3.forward,
                        Vector3.forward * 2f,
                        4f,
                        Color.cyan,
                        false);
                    Assert.That(indicator, Is.Not.Null, attackId);
                    var lines = indicator.GetComponentsInChildren<LineRenderer>(true);
                    Assert.That(lines.Length, Is.GreaterThan(0), attackId);
                    Assert.That(lines.Sum(line => line.positionCount), Is.GreaterThan(3), attackId);
                    Assert.That(indicator.Tick(profile.HitAreaVisibleDuration + 0.01f), Is.False, attackId);
                    Object.DestroyImmediate(indicator.gameObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void FeelCue_UsesARuntimePrefabIndependentFromVfx()
        {
            var feelPrefab = new GameObject("FeelPreset");
            var vfxPrefab = new GameObject("VisualOnlyVfx");
            var profile = ScriptableObject.CreateInstance<MonsterBasicAttackProfile>();
            try
            {
                var feel = new BasicAttackFeelCue();
                feel.EditorConfigure(feelPrefab, 0.35f, new Vector3(0f, 0.2f, 0f), Vector3.zero, 1.1f);
                Assert.That(feel.TryValidate(out var missingRuntime), Is.False);
                Assert.That(missingRuntime, Does.Contain("IBasicAttackFeelRuntime"));

                var adapterType = FindEditorType("ProjectMT.Integrations.Feel.BasicAttackFeelRuntimeAdapter");
                var playerType = FindEditorType("MoreMountains.Feedbacks.MMF_Player");
                var player = feelPrefab.AddComponent(playerType);
                var adapter = feelPrefab.AddComponent(adapterType);
                adapterType.GetMethod("EditorConfigure").Invoke(adapter, new object[] { player, null });
                Assert.That(feel.TryValidate(out var validError), Is.True, validError);

                var vfx = new MonsterFeedbackCue();
                vfx.EditorConfigure(null, vfxPrefab, 0.5f);
                profile.EditorSetPresentationFeedback(vfx, null, null);
                profile.EditorSetFeelFeedback(feel, null, null);

                Assert.That(profile.LaunchFeel.Prefab, Is.SameAs(feelPrefab));
                Assert.That(profile.LaunchFeedback.VfxPrefab, Is.SameAs(vfxPrefab));
                Assert.That(profile.LaunchFeel.Prefab, Is.Not.SameAs(profile.LaunchFeedback.VfxPrefab));
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(feelPrefab);
                Object.DestroyImmediate(vfxPrefab);
            }
        }

        [Test]
        public void Workshop_ShowsOnlyImpactFeelAndKeepsLegacyFieldsForRoundTrip()
        {
            var recipeType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.BasicAttackWorkshopRecipe");
            foreach (var fieldName in new[] { "launchFeelPrefab", "projectileFeelPrefab", "impactFeelPrefab" })
            {
                var field = recipeType.GetField(fieldName);
                Assert.That(field, Is.Not.Null, fieldName);
                Assert.That(field.FieldType, Is.EqualTo(typeof(GameObject)), fieldName);
            }

            foreach (var fieldName in new[] { "launchVfx", "projectileVfx", "impactVfx" })
            {
                Assert.That(recipeType.GetField(fieldName), Is.Not.Null,
                    $"{fieldName}는 이전 Recipe 왕복 호환을 위해서만 유지합니다.");
            }

            Assert.That(recipeType.GetField("launchFeelEffect"), Is.Null);
            Assert.That(recipeType.GetField("projectileFeelEffect"), Is.Null);
            Assert.That(recipeType.GetField("impactFeelEffect"), Is.Null);
            Assert.That(FindTypeOrNull(
                "ProjectMT.EditorTools.MonsterMaker.BasicAttackFeelEffectWorkshopWindow"), Is.Null);
            Assert.That(FindTypeOrNull(
                "ProjectMT.EditorTools.MonsterMaker.BasicAttackFeelEffectCompiler"), Is.Null);

            var workshopSource = File.ReadAllText(
                "Assets/ProjectMT/Editor/MonsterMaker/MonsterBasicAttackWorkshopWindow.cs");
            Assert.That(workshopSource, Does.Contain("실제 명중 FEEL 프로필"));
            Assert.That(workshopSource, Does.Not.Contain("Recipe 시작 · Marker 발사점"));
            Assert.That(workshopSource, Does.Not.Contain("이동체 · Marker 뒤 실제 비행"));
        }

        [Test]
        public void DamageFeedbackOwnership_IsExplicitPerHitAndDefaultsToCommonFeedback()
        {
            var defaultRequest = new DamageRequest(null, 10f, Vector3.zero, false);
            Assert.That(defaultRequest.FeedbackFlags, Is.EqualTo(DamageFeedbackFlags.None));

            var feelRequest = new DamageRequest(
                null,
                10f,
                Vector3.zero,
                false,
                DamageFeedbackFlags.BasicAttackFeelTargetMotion);
            Assert.That(
                feelRequest.FeedbackFlags.HasFlag(DamageFeedbackFlags.BasicAttackFeelTargetMotion),
                Is.True);
        }

        [Test]
        public void OfficialProfiles_UseOnlyProductionFeelPresetsWithoutLegacyGeneratedReferences()
        {
            const string definitionRoot =
                "Assets/ProjectMT/02_Shared/Unit/Data/BasicAttackEffects";
            const string generatedRoot =
                "Assets/ProjectMT/98_Generated/BasicAttackFeelEffects";
            const string productionFeelRoot =
                "Assets/ProjectMT/05_Art/FeelPresets/BasicAttack/Production";

            Assert.That(AssetDatabase.IsValidFolder(definitionRoot), Is.False);
            Assert.That(AssetDatabase.IsValidFolder(generatedRoot), Is.False);
            foreach (var attackId in BuiltInIds)
            {
                var profile = LoadProfile(attackId);
                Assert.That(profile, Is.Not.Null, attackId);
                Assert.That(profile.LaunchFeel?.Prefab, Is.Null, $"{attackId}/launch FEEL");
                Assert.That(profile.ProjectileFeel?.Prefab, Is.Null, $"{attackId}/projectile FEEL");
                Assert.That(profile.ImpactFeel?.Prefab, Is.Not.Null, $"{attackId}/impact FEEL");
                Assert.That(
                    AssetDatabase.GetAssetPath(profile.ImpactFeel.Prefab),
                    Does.StartWith(productionFeelRoot),
                    $"{attackId}/impact FEEL");
                Assert.That(profile.LaunchFeedback?.HasAnyFeedback ?? false, Is.False,
                    $"{attackId}/launch legacy VFX-SFX");
                Assert.That(profile.ProjectileFeedback?.HasAnyFeedback ?? false, Is.False,
                    $"{attackId}/projectile legacy VFX-SFX");
                Assert.That(profile.ImpactFeedback?.HasAnyFeedback ?? false, Is.False,
                    $"{attackId}/impact legacy VFX-SFX");
                foreach (var prefab in new[]
                         {
                             profile.LaunchFeedback?.VfxPrefab,
                             profile.ProjectileFeedback?.VfxPrefab,
                             profile.ImpactFeedback?.VfxPrefab
                         })
                {
                    if (prefab == null)
                    {
                        continue;
                    }

                    Assert.That(AssetDatabase.GetAssetPath(prefab), Does.Not.StartWith(generatedRoot), attackId);
                }

                Assert.That(profile.TryValidate(out var error), Is.True, $"{attackId}: {error}");
            }
        }

        [Test]
        public void FeelRuntimeAdapter_ResetBeforeFirstPlayDoesNotStopUninitializedFeelPlayer()
        {
            var adapterType = FindEditorType("ProjectMT.Integrations.Feel.BasicAttackFeelRuntimeAdapter");
            var playerType = FindEditorType("MoreMountains.Feedbacks.MMF_Player");
            var flickerType = FindEditorType("MoreMountains.Feedbacks.MMF_Flicker");
            var root = new GameObject("FeelPoolFirstRentTest");
            try
            {
                var player = root.AddComponent(playerType);
                var feedbacksField = playerType.GetField("FeedbacksList");
                var feedbacks = feedbacksField.GetValue(player) as System.Collections.IList;
                if (feedbacks == null)
                {
                    feedbacks = System.Activator.CreateInstance(feedbacksField.FieldType) as
                        System.Collections.IList;
                    feedbacksField.SetValue(player, feedbacks);
                }
                feedbacks.Add(System.Activator.CreateInstance(flickerType));

                var adapter = root.AddComponent(adapterType);
                adapterType.GetField("player",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(adapter, player);

                Assert.DoesNotThrow(() => adapterType.GetMethod("ResetBasicAttackFeel")
                    .Invoke(adapter, null));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FeelRuntimeAdapter_RuntimeScopeKeepsLocalLightAndDisablesSharedCameraFreezeAndTimescale()
        {
            var adapterType = FindEditorType("ProjectMT.Integrations.Feel.BasicAttackFeelRuntimeAdapter");
            var playerType = FindEditorType("MoreMountains.Feedbacks.MMF_Player");
            var root = new GameObject("FeelRuntimeOwnershipTest");
            try
            {
                var player = root.AddComponent(playerType);
                var feedbacksField = playerType.GetField("FeedbacksList");
                var feedbacks = feedbacksField.GetValue(player) as System.Collections.IList;
                if (feedbacks == null)
                {
                    feedbacks = System.Activator.CreateInstance(feedbacksField.FieldType) as
                        System.Collections.IList;
                    feedbacksField.SetValue(player, feedbacks);
                }

                var cameraShake = System.Activator.CreateInstance(
                    FindEditorType("MoreMountains.Feedbacks.MMF_CameraShake"));
                var fieldOfView = System.Activator.CreateInstance(
                    FindEditorType("MoreMountains.Feedbacks.MMF_CameraFieldOfView"));
                var freezeFrame = System.Activator.CreateInstance(
                    FindEditorType("MoreMountains.Feedbacks.MMF_FreezeFrame"));
                var timescale = System.Activator.CreateInstance(
                    FindEditorType("MoreMountains.Feedbacks.MMF_TimescaleModifier"));
                var localLight = System.Activator.CreateInstance(
                    FindEditorType("MoreMountains.Feedbacks.MMF_Light"));
                foreach (var feedback in new[] { cameraShake, fieldOfView, freezeFrame, timescale, localLight })
                {
                    feedback.GetType().GetField("Label").SetValue(feedback, "[Global] ownership test");
                    feedbacks.Add(feedback);
                }

                var adapter = root.AddComponent(adapterType);
                adapterType.GetMethod("EditorConfigure").Invoke(adapter, new object[] { player, null });
                var applyOwnership = adapterType.GetMethod(
                    "SetGlobalFeedbacksActive",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(applyOwnership, Is.Not.Null);
                applyOwnership.Invoke(adapter, new object[] { true, false });

                foreach (var feedback in new[] { cameraShake, fieldOfView, freezeFrame, timescale })
                {
                    Assert.That(feedback.GetType().GetField("Active").GetValue(feedback), Is.False,
                        feedback.GetType().Name);
                }
                Assert.That(localLight.GetType().GetField("Active").GetValue(localLight), Is.True);

                applyOwnership.Invoke(adapter, new object[] { true, true });
                foreach (var feedback in new[] { cameraShake, fieldOfView, freezeFrame, timescale, localLight })
                {
                    Assert.That(feedback.GetType().GetField("Active").GetValue(feedback), Is.True,
                        feedback.GetType().Name);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BuiltInProfiles_HaveValidDistinctVfxSpaceContracts()
        {
            Assert.That(
                typeof(MonsterBasicAttackVfxSlot).GetProperty("Required"),
                Is.Null,
                "기본공격 연출 공간에 필수 사용 계약이 다시 생기면 안 됩니다.");
            foreach (var pair in VfxSlotCounts)
            {
                var profile = LoadProfile(pair.Key);
                Assert.That(profile, Is.Not.Null, pair.Key);
                Assert.That(profile.VfxSlots.Count, Is.EqualTo(pair.Value), pair.Key);
                Assert.That(profile.TryValidate(out var error), Is.True, $"{pair.Key}: {error}");
                Assert.That(
                    profile.VfxSlots.Select(slot => slot.SlotId).Distinct().Count(),
                    Is.EqualTo(profile.VfxSlots.Count),
                    pair.Key);
                var deliverySlots = profile.VfxSlots.Where(slot => slot.IsDeliveryVisual).ToArray();
                Assert.That(deliverySlots.Length, Is.LessThanOrEqualTo(1), pair.Key);
                if (deliverySlots.Length == 1)
                {
                    Assert.That(profile.UsesProjectileVisual, Is.True, pair.Key);
                    Assert.That(
                        deliverySlots[0].EventType,
                        Is.EqualTo(MonsterBasicAttackVfxEvent.DeliverySpawn),
                        pair.Key);
                    Assert.That(
                        deliverySlots[0].EndPolicy,
                        Is.EqualTo(MonsterBasicAttackVfxEndPolicy.DeliveryEnd),
                        pair.Key);
                }
            }
        }

        [Test]
        public void BuiltInProfiles_OnlyContainReachableCanonicalVfxContracts()
        {
            var templateType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterBasicAttackVfxContractTemplates");
            var build = templateType.GetMethod(
                "Build",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.That(build, Is.Not.Null);

            foreach (var attackId in BuiltInIds)
            {
                var profile = LoadProfile(attackId);
                var canonical = (MonsterBasicAttackVfxSlot[])build.Invoke(null, new object[] { profile });
                Assert.That(profile.VfxSlots.Count, Is.EqualTo(canonical.Length), attackId);
                for (var index = 0; index < profile.VfxSlots.Count; index++)
                {
                    var actual = profile.VfxSlots[index];
                    var expected = canonical[index];
                    Assert.That(
                        MonsterBasicAttackVfxCompatibility.TryValidateSlot(profile, actual, out var error),
                        Is.True,
                        $"{attackId}/{actual.SlotId}: {error}");
                    Assert.That(actual.SlotId, Is.EqualTo(expected.SlotId), attackId);
                    Assert.That(actual.EventType, Is.EqualTo(expected.EventType), $"{attackId}/{actual.SlotId}");
                    Assert.That(actual.Anchor, Is.EqualTo(expected.Anchor), $"{attackId}/{actual.SlotId}");
                    Assert.That(actual.Multiplicity, Is.EqualTo(expected.Multiplicity), $"{attackId}/{actual.SlotId}");
                    Assert.That(actual.AssignmentScope, Is.EqualTo(expected.AssignmentScope), $"{attackId}/{actual.SlotId}");
                    Assert.That(actual.Attachment, Is.EqualTo(expected.Attachment), $"{attackId}/{actual.SlotId}");
                    Assert.That(actual.EndPolicy, Is.EqualTo(expected.EndPolicy), $"{attackId}/{actual.SlotId}");
                }
            }

            Assert.That(
                LoadProfile("BA_R_03").VfxSlots.First(slot => slot.SlotId == "contact").DisplayName,
                Is.EqualTo("대상별 폭발 명중"));
        }

        [TestCase("pc_bear_01")]
        [TestCase("pango_01")]
        public void ActiveBindingProjection_KeepsOldPresetAndMotionRowsOutOfRuntime(string monsterId)
        {
            var draft = AssetDatabase.LoadMainAssetAtPath($"{DraftRoot}/Draft_{monsterId}.asset");
            var paths = new[]
            {
                $"{MonsterDataRoot}/{monsterId}/MC_{monsterId}.asset",
                $"{MonsterDataRoot}/{monsterId}/MF_{monsterId}.asset"
            };
            var combat = AssetDatabase.LoadAssetAtPath<MonsterCombatProfile>(paths[0]);
            var feedback = AssetDatabase.LoadAssetAtPath<MonsterFeedbackProfile>(paths[1]);
            Assert.That(draft, Is.Not.Null, monsterId);
            Assert.That(combat, Is.Not.Null, monsterId);
            Assert.That(feedback, Is.Not.Null, monsterId);

            var projectionType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterBasicAttackBindingProjection");
            var flags = System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Static;
            var active = (System.Collections.ICollection)projectionType
                .GetMethod("BuildActiveBindings", flags)
                .Invoke(null, new[] { draft });
            var inactive = (System.Collections.ICollection)projectionType
                .GetMethod("BuildInactiveBindings", flags)
                .Invoke(null, new[] { draft });
            Assert.That(active.Count, Is.EqualTo(4), monsterId + " / active");
            Assert.That(inactive.Count, Is.GreaterThan(0), monsterId + " / cache");
            Assert.That(feedback.BasicAttackVfxBindings.Count, Is.EqualTo(active.Count), monsterId + " / runtime");

            var args = new object[] { draft, combat, feedback, null };
            var state = projectionType.GetMethod("EvaluateRuntimeSync", flags).Invoke(null, args);
            Assert.That(state.ToString(), Is.EqualTo("Synchronized"), $"{monsterId}: {args[3]}");
        }

        [Test]
        public void DirectAttack_RejectsMovingDeliveryOnlyVfxContract()
        {
            var direct = LoadProfile("BA_R_04");
            var slot = new MonsterBasicAttackVfxSlot();
            slot.EditorConfigure(
                "invalid_projectile",
                "잘못된 이동체",
                "직접 공격에서는 발생할 수 없습니다.",
                MonsterBasicAttackVfxEvent.DeliverySpawn,
                MonsterBasicAttackVfxAnchor.ProjectileRoot,
                MonsterBasicAttackVfxMultiplicity.PerProjectile,
                MonsterBasicAttackVfxAssignmentScope.MonsterShared,
                MonsterBasicAttackVfxAttachment.DeliveryVisual,
                MonsterBasicAttackVfxEndPolicy.DeliveryEnd,
                1f);

            Assert.That(
                MonsterBasicAttackVfxCompatibility.TryValidateSlot(direct, slot, out var error),
                Is.False);
            Assert.That(error, Does.Contain("cannot occur"));
        }

        [Test]
        public void VfxResolver_KeepsMotionSpecificTriStateAssignments()
        {
            var prefab = new GameObject("VfxResolverTest");
            try
            {
                var slot = new MonsterBasicAttackVfxSlot();
                slot.EditorConfigure(
                    "trail", "궤적", "Motion별 궤적",
                    MonsterBasicAttackVfxEvent.RecipeExecute,
                    MonsterBasicAttackVfxAnchor.AttackOrigin,
                    MonsterBasicAttackVfxMultiplicity.OncePerExecution,
                    MonsterBasicAttackVfxAssignmentScope.MotionSpecific,
                    MonsterBasicAttackVfxAttachment.FollowAnchor,
                    MonsterBasicAttackVfxEndPolicy.MotionEnd);
                var assigned = new MonsterBasicAttackVfxBinding();
                assigned.EditorConfigure(
                    "BA_TEST", "trail", "attack01",
                    MonsterBasicAttackVfxAssignmentState.Assigned,
                    prefab, 1f, Vector3.zero, Vector3.zero, 1f, 0.35f,
                    vfxPlaybackSpeed: 0.5f);
                var disabled = new MonsterBasicAttackVfxBinding();
                disabled.EditorConfigure(
                    "BA_TEST", "trail", "attack02",
                    MonsterBasicAttackVfxAssignmentState.Disabled,
                    null, 1f, Vector3.zero, Vector3.zero, 1f);
                var bindings = new[] { assigned, disabled };

                Assert.That(
                    MonsterBasicAttackVfxResolver.TryResolve(
                        bindings, "BA_TEST", slot, "attack01", out var resolved),
                    Is.True);
                Assert.That(resolved, Is.SameAs(assigned));
                Assert.That(resolved.PlaybackOffset, Is.EqualTo(0.35f).Within(0.0001f));
                Assert.That(resolved.PlaybackSpeed, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(
                    assigned.EditorCloneForRuntime(null).PlaybackSpeed,
                    Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(
                    MonsterBasicAttackVfxResolver.TryResolve(
                        bindings, "BA_TEST", slot, "attack02", out resolved),
                    Is.False);
                Assert.That(resolved, Is.SameAs(disabled));
                Assert.That(disabled.PlaybackSpeed, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void VfxTimingOffset_IsMonsterSpecificAndOnlyLeadClampsByEventKnowledge()
        {
            var prefab = new GameObject("VfxTimingOffsetTest");
            try
            {
                var markerSlot = new MonsterBasicAttackVfxSlot();
                markerSlot.EditorConfigure(
                    "trail", "궤적", "Marker 선행 가능",
                    MonsterBasicAttackVfxEvent.RecipeExecute,
                    MonsterBasicAttackVfxAnchor.AttackOrigin,
                    MonsterBasicAttackVfxMultiplicity.OncePerExecution,
                    MonsterBasicAttackVfxAssignmentScope.MotionSpecific,
                    MonsterBasicAttackVfxAttachment.FollowAnchor,
                    MonsterBasicAttackVfxEndPolicy.MotionEnd);
                var binding = new MonsterBasicAttackVfxBinding();
                binding.EditorConfigure(
                    "BA_TEST",
                    "trail",
                    "attack01",
                    MonsterBasicAttackVfxAssignmentState.Assigned,
                    prefab,
                    1f,
                    Vector3.zero,
                    Vector3.zero,
                    1f,
                    vfxEventTimingOffset: -0.15f);

                Assert.That(binding.EventTimingOffset, Is.EqualTo(-0.15f).Within(0.0001f));
                Assert.That(
                    markerSlot.ClampTimingOffset(binding.EventTimingOffset),
                    Is.EqualTo(-0.15f).Within(0.0001f));

                var targetSlot = new MonsterBasicAttackVfxSlot();
                targetSlot.EditorConfigure(
                    "hit", "대상 명중", "실제 위치 확정 뒤 재생",
                    MonsterBasicAttackVfxEvent.TargetDamaged,
                    MonsterBasicAttackVfxAnchor.HitPoint,
                    MonsterBasicAttackVfxMultiplicity.PerTargetHit,
                    MonsterBasicAttackVfxAssignmentScope.MonsterShared,
                    MonsterBasicAttackVfxAttachment.World,
                    MonsterBasicAttackVfxEndPolicy.Timed);
                Assert.That(targetSlot.ClampTimingOffset(-0.15f), Is.Zero);
                Assert.That(targetSlot.ClampTimingOffset(3.5f), Is.EqualTo(3.5f));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void PresentationResolver_AllowsSfxWithoutVfx()
        {
            var sound = AudioClip.Create("BasicAttackSlotSound", 64, 1, 44100, false);
            try
            {
                var slot = new MonsterBasicAttackVfxSlot();
                slot.EditorConfigure(
                    "hit", "명중", "몬스터 공용 명중",
                    MonsterBasicAttackVfxEvent.TargetDamaged,
                    MonsterBasicAttackVfxAnchor.HitPoint,
                    MonsterBasicAttackVfxMultiplicity.PerTargetHit,
                    MonsterBasicAttackVfxAssignmentScope.MonsterShared,
                    MonsterBasicAttackVfxAttachment.World,
                    MonsterBasicAttackVfxEndPolicy.Timed);
                var binding = new MonsterBasicAttackVfxBinding();
                binding.EditorConfigure(
                    "BA_TEST",
                    "hit",
                    string.Empty,
                    MonsterBasicAttackVfxAssignmentState.Disabled,
                    null,
                    1f,
                    Vector3.zero,
                    Vector3.zero,
                    1f,
                    0f,
                    sound,
                    null,
                    MonsterBasicAttackSfxAssignmentState.Assigned,
                    0.4f);

                Assert.That(
                    MonsterBasicAttackVfxResolver.TryResolve(
                        new[] { binding }, "BA_TEST", slot, string.Empty, out _),
                    Is.False);
                Assert.That(
                    MonsterBasicAttackVfxResolver.TryResolvePresentation(
                        new[] { binding }, "BA_TEST", slot, string.Empty, out var resolved),
                    Is.True);
                Assert.That(resolved.Sound, Is.SameAs(sound));
                Assert.That(resolved.SfxState, Is.EqualTo(MonsterBasicAttackSfxAssignmentState.Assigned));
                Assert.That(resolved.SoundVolume, Is.EqualTo(0.4f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(sound);
            }
        }

        [Test]
        public void VfxPlaybackOffset_RestartsAndAdvancesParticlePreview()
        {
            var root = new GameObject("VfxPlaybackOffsetTest");
            var worldRoot = new GameObject("VfxPlaybackOffsetWorld");
            var poolRoot = new GameObject("VfxPlaybackOffsetPool");
            try
            {
                var particle = root.AddComponent<ParticleSystem>();
                var main = particle.main;
                main.playOnAwake = false;
                main.loop = true;
                main.duration = 2f;
                main.startLifetime = 10f;
                main.startSpeed = 0f;
                var emission = particle.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
                var child = new GameObject("AuthoredQuarterSpeed");
                child.transform.SetParent(root.transform, false);
                var childParticle = child.AddComponent<ParticleSystem>();
                var childMain = childParticle.main;
                childMain.playOnAwake = false;
                childMain.simulationSpeed = 0.25f;

                MonsterBasicAttackVfxPlayback.RestartAtOffset(root, 0.35f, false, 2f);
                var particles = new ParticleSystem.Particle[4];
                Assert.That(particle.GetParticles(particles), Is.EqualTo(1));
                Assert.That(
                    particles[0].remainingLifetime,
                    Is.EqualTo(9.65f).Within(0.03f),
                    "재생 속도와 무관하게 같은 Prefab 내부 시작점에서 시작해야 합니다.");
                Assert.That(particle.isPaused, Is.True);
                Assert.That(particle.main.simulationSpeed, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(childParticle.main.simulationSpeed, Is.EqualTo(0.5f).Within(0.0001f));

                MonsterBasicAttackVfxPlayback.Simulate(root, 0.1f);
                Assert.That(particle.GetParticles(particles), Is.EqualTo(1));
                Assert.That(
                    particles[0].remainingLifetime,
                    Is.EqualTo(9.45f).Within(0.03f),
                    "2배속에서는 실제 0.1초 동안 VFX 내부 시간이 0.2초 진행돼야 합니다.");

                MonsterBasicAttackVfxPlayback.RestartAtOffset(root, 0.35f, false, 0.5f);
                Assert.That(
                    particle.main.simulationSpeed,
                    Is.EqualTo(0.5f).Within(0.0001f),
                    "Pool 재사용 배율은 직전 2배가 아니라 Vendor 원본 1배를 기준으로 다시 계산해야 합니다.");
                Assert.That(childParticle.main.simulationSpeed, Is.EqualTo(0.125f).Within(0.0001f));
                Assert.That(particle.GetParticles(particles), Is.EqualTo(1));
                Assert.That(
                    particles[0].remainingLifetime,
                    Is.EqualTo(9.65f).Within(0.03f));

                var binding = new MonsterBasicAttackVfxBinding();
                binding.EditorConfigure(
                    "BA_TEST",
                    "impact",
                    string.Empty,
                    MonsterBasicAttackVfxAssignmentState.Assigned,
                    root,
                    1f,
                    Vector3.zero,
                    Vector3.zero,
                    0.25f,
                    0.35f,
                    vfxPlaybackSpeed: 2f);
                var world = worldRoot.AddComponent<CombatWorld>();
                var pool = poolRoot.AddComponent<ProjectMT.Shared.Pooling.ScenePoolScope>();
                world.EditorConfigure(pool, null, null);
                var instance = world.SpawnBasicAttackVfx(
                    binding,
                    Vector3.zero,
                    Quaternion.identity);
                Assert.That(instance, Is.Not.Null);
                Assert.That(
                    instance.GetComponent<ParticleSystem>().GetParticles(particles),
                    Is.EqualTo(1));
                Assert.That(
                    particles[0].remainingLifetime,
                    Is.EqualTo(9.65f).Within(0.03f));
                Assert.That(
                    instance.GetComponent<ParticleSystem>().main.simulationSpeed,
                    Is.EqualTo(2f).Within(0.0001f));
                Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one * 0.25f));
                Assert.That(
                    instance.GetComponent<ParticleSystem>().main.scalingMode,
                    Is.EqualTo(ParticleSystemScalingMode.Hierarchy));
                world.ReturnMonsterObject(instance);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(worldRoot);
                Object.DestroyImmediate(poolRoot);
            }
        }

        private static void AssertProfile(
            string attackId,
            MonsterBasicAttackDelivery delivery,
            MonsterBasicAttackShape shape)
        {
            var profile = LoadProfile(attackId);
            Assert.That(profile, Is.Not.Null, attackId);
            Assert.That(profile.Delivery, Is.EqualTo(delivery), attackId);
            Assert.That(profile.Shape, Is.EqualTo(shape), attackId);
        }

        private static void AssertRecipe(
            string attackId,
            MonsterBasicAttackDeliveryModule delivery,
            MonsterBasicAttackCollisionModule collision,
            MonsterBasicAttackSequenceModule sequence,
            MonsterBasicAttackMovementModule movement,
            MonsterBasicAttackShape shape)
        {
            var profile = LoadProfile(attackId);
            Assert.That(profile, Is.Not.Null, attackId);
            Assert.That(profile.IsModularRecipe, Is.True, attackId);
            Assert.That(profile.RecipeVersion, Is.EqualTo(MonsterBasicAttackProfile.CurrentRecipeVersion), attackId);
            Assert.That(profile.DeliveryModule, Is.EqualTo(delivery), attackId);
            Assert.That(profile.CollisionModule, Is.EqualTo(collision), attackId);
            Assert.That(profile.SequenceModule, Is.EqualTo(sequence), attackId);
            Assert.That(profile.MovementModule, Is.EqualTo(movement), attackId);
            Assert.That(profile.Shape, Is.EqualTo(shape), attackId);
        }

        private static System.Type FindEditorType(string fullName)
        {
            var type = FindTypeOrNull(fullName);
            if (type != null)
            {
                return type;
            }

            Assert.Fail($"Editor type not found: {fullName}");
            return null;
        }

        private static System.Type FindTypeOrNull(string fullName)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static MonsterBasicAttackProfile LoadProfile(string attackId)
        {
            return AssetDatabase.LoadAssetAtPath<MonsterBasicAttackProfile>($"{ProfileRoot}/{attackId}.asset");
        }

    }
}
