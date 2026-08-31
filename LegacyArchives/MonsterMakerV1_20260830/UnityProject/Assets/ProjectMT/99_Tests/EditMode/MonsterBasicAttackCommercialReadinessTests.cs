using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Tests.EditMode
{
    public sealed class MonsterBasicAttackCommercialReadinessTests // 공식 수치·Writer·Preview 시간축 계약
    {
        private const string ProfileRoot = "Assets/ProjectMT/02_Shared/Unit/Data/BasicAttacks";
        private const string DraftRoot = "Assets/ProjectMT/Editor/MonsterMaker/Drafts";
        private const string MonsterRoot = "Assets/ProjectMT/02_Shared/Unit/Data/Monsters";
        private static readonly IReadOnlyDictionary<string, string> ExpectedSignatures =
            new Dictionary<string, string>
            {
                ["BA_M_01"] = "Melee|Direct|DirectResolve|Single|None|Contact|Single|PrimaryTarget|None|Simultaneous|1|0.35|60|0.4|1|1|0|9|3|0.25|1|1|0.08|0|0.1",
                ["BA_M_02"] = "Melee|Direct|DirectResolve|Single|None|Sweep|Fan|Source|None|Simultaneous|1|0.35|100|1|3|1|0|9|3|0.25|1|0.65|0.08|0|0.1",
                ["BA_M_03"] = "Melee|Direct|DirectResolve|Single|None|Thrust|Line|Source|None|Simultaneous|1.1|0.3|20|0.65|3|1|0|9|3|0.25|1|0.75|0.08|0|0.1",
                ["BA_M_04"] = "Melee|Direct|DirectResolve|Single|None|Slam|Circle|PrimaryTarget|None|Simultaneous|1|1.6|180|1|4|1|0|9|3|0.25|1|0.65|0.08|0|0.1",
                ["BA_M_05"] = "Melee|Direct|DirectResolve|Single|Dash|Dash|Single|PrimaryTarget|None|Simultaneous|1|0.45|35|0.5|1|1|0|9|3|0.25|1|1|0.08|1.2|0.11",
                ["BA_M_06"] = "Melee|Direct|DirectResolve|Burst|None|Combo|Single|PrimaryTarget|None|Simultaneous|1|0.4|30|0.4|1|1|0|9|3|0.25|0.3,0.3,0.4|1|0.08|0|0.1",
                ["BA_R_01"] = "Ranged|Projectile|StopOnFirstTarget|Single|None|Shot|Single|PrimaryTarget|Homing|Simultaneous|1|0.25|30|0.3|1|1|0|9|3|0.25|1|1|0.08|0|0.1",
                ["BA_R_02"] = "Ranged|Projectile|Pierce|Single|None|Shot|Line|Source|Straight|Simultaneous|1.2|0.28|10|0.55|3|1|0|9|3|0.25|1|0.8|0.08|0|0.1",
                ["BA_R_03"] = "Ranged|Projectile|AreaImpact|Single|None|Explosion|Circle|PrimaryTarget|Homing|Simultaneous|1|1.55|180|1|4|1|0|9|3|0.25|1|0.65|0.08|0|0.1",
                ["BA_R_04"] = "Ranged|Direct|DirectResolve|Single|None|Instant|Single|PrimaryTarget|None|Simultaneous|1|0.35|20|0.3|1|1|0|9|3|0.25|1|1|0.08|0|0.1",
                ["BA_R_05"] = "Ranged|Projectile|StopOnFirstTarget|Single|None|Scatter|Fan|Source|Straight|Simultaneous|1.1|0.24|35|0.35|3|3|28|9|3|0.25|1|0.55|0.08|0|0.1",
                ["BA_S_01"] = "Ranged|Projectile|PassThrough|ReturnPasses|None|Returning|Line|Source|Returning|Simultaneous|1.1|0.32|15|0.65|3|1|0|9|3|0.25|0.6,0.4|0.7|0.08|0|0.1",
                ["BA_S_02"] = "Ranged|Direct|DirectResolve|Burst|None|Breath|Fan|Source|None|Simultaneous|1|0.4|55|1|4|1|0|9|3|0.25|0.34,0.33,0.33|0.6|0.07|0|0.1",
                ["BA_S_03"] = "Ranged|Direct|DirectResolve|Single|None|Beam|Line|Source|None|Simultaneous|1.1|0.28|10|0.5|4|1|0|9|3|0.25|1|0.75|0.08|0|0.1",
                ["BA_S_04"] = "Ranged|TravelingArea|PassThrough|Single|None|Wave|Line|Source|Straight|Simultaneous|1.3|0.6|20|1.2|4|1|0|8|3|0.25|1|0.7|0.08|0|0.1"
            };
        private static readonly IReadOnlyDictionary<string, string> ExpectedImpactFeelPresets =
            new Dictionary<string, string>
            {
                ["BA_M_01"] = "BAFeel_DirectHit",
                ["BA_M_02"] = "BAFeel_SweepHit",
                ["BA_M_03"] = "BAFeel_PierceHit",
                ["BA_M_04"] = "BAFeel_SlamHit",
                ["BA_M_05"] = "BAFeel_DirectHit",
                ["BA_M_06"] = "BAFeel_RapidHit",
                ["BA_R_01"] = "BAFeel_PierceHit",
                ["BA_R_02"] = "BAFeel_PierceHit",
                ["BA_R_03"] = "BAFeel_BlastHit",
                ["BA_R_04"] = "BAFeel_DirectHit",
                ["BA_R_05"] = "BAFeel_PierceHit",
                ["BA_S_01"] = "BAFeel_PierceHit",
                ["BA_S_02"] = "BAFeel_RapidHit",
                ["BA_S_03"] = "BAFeel_PierceHit",
                ["BA_S_04"] = "BAFeel_WaveHit"
            };

        [Test]
        public void OfficialProfiles_MatchTheExactValuesUsedByTheFifteenRuntimeScenarios()
        {
            foreach (var pair in ExpectedSignatures)
            {
                var profile = LoadProfile(pair.Key);
                Assert.That(profile, Is.Not.Null, pair.Key);
                Assert.That(BuildSignature(profile), Is.EqualTo(pair.Value), pair.Key);
            }
        }

        [Test]
        public void OfficialProfiles_UseTheProductionFeelStyleAssignedToTheirAttackKind()
        {
            foreach (var pair in ExpectedImpactFeelPresets)
            {
                var profile = LoadProfile(pair.Key);
                Assert.That(profile.ImpactFeel.HasFeel, Is.True, pair.Key);
                Assert.That(profile.ImpactFeel.Prefab.name, Is.EqualTo(pair.Value), pair.Key);
                Assert.That(profile.ImpactFeel.TryValidate(out var error), Is.True, $"{pair.Key}: {error}");
            }
        }

        [Test]
        public void ProductionFeelPresets_KeepFifteenLayerContractAndSixGlobalBudgetLayers()
        {
            var names = ExpectedImpactFeelPresets.Values.Distinct().OrderBy(value => value).ToArray();
            Assert.That(names, Has.Length.EqualTo(7));
            foreach (var name in names)
            {
                var path = $"Assets/ProjectMT/05_Art/FeelPresets/BasicAttack/Production/{name}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                var runtime = prefab.GetComponent(typeof(IBasicAttackFeelRuntime)) as IBasicAttackFeelRuntime;
                Assert.That(runtime, Is.Not.Null, path);
                Assert.That(runtime.IsBasicAttackFeelConfigured, Is.True, path);

                var adapter = prefab.GetComponents<MonoBehaviour>()
                    .Single(component => component != null &&
                                         component.GetType().FullName ==
                                         "ProjectMT.Integrations.Feel.BasicAttackFeelRuntimeAdapter");
                var sharedCombatClassifier = adapter.GetType().GetMethod(
                    "IsSharedCombatGlobalFeedback",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(sharedCombatClassifier, Is.Not.Null, path);
                var player = adapter.GetType().GetProperty("Player")?.GetValue(adapter);
                var feedbacks = ((IEnumerable)player?.GetType().GetField("FeedbacksList")?.GetValue(player))
                    ?.Cast<object>()
                    .ToArray();
                Assert.That(feedbacks, Is.Not.Null, path);
                Assert.That(feedbacks, Has.Length.EqualTo(15), path);
                var globalCount = feedbacks.Count(feedback =>
                    ((string)feedback.GetType().GetField("Label")?.GetValue(feedback))
                    ?.StartsWith("[Global]", StringComparison.Ordinal) == true);
                Assert.That(globalCount, Is.EqualTo(6), path);

                var lightFeedback = feedbacks.Single(feedback =>
                    feedback.GetType().FullName == "MoreMountains.Feedbacks.MMF_Light");
                var lightLabel = (string)lightFeedback.GetType().GetField("Label")?.GetValue(lightFeedback);
                var boundLight = lightFeedback.GetType().GetField("BoundLight")?.GetValue(lightFeedback) as Light;
                Assert.That(lightLabel, Does.StartWith("[Global]").And.Contain("[PrefabTarget]"), path);
                Assert.That(boundLight, Is.Not.Null, path);
                Assert.That(boundLight.transform.IsChildOf(prefab.transform), Is.True, path);
                Assert.That(boundLight.enabled, Is.False, path);
                Assert.That(boundLight.shadows, Is.EqualTo(LightShadows.None), path);
                Assert.That(sharedCombatClassifier.Invoke(null, new[] { lightFeedback }), Is.False,
                    $"{path}/local light must remain available in runtime");

                foreach (var feedback in feedbacks.Where(feedback =>
                             feedback.GetType().FullName is
                                 "MoreMountains.Feedbacks.MMF_CameraShake" or
                                 "MoreMountains.Feedbacks.MMF_CameraFieldOfView" or
                                 "MoreMountains.Feedbacks.MMF_FreezeFrame"))
                {
                    Assert.That(sharedCombatClassifier.Invoke(null, new[] { feedback }), Is.True,
                        $"{path}/{feedback.GetType().Name}");
                }
            }

            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    "Assets/ProjectMT/00_Scenes/DEV_FEEL_BasicAttackImpactLab.unity"),
                Is.Not.Null);
        }

        [TestCase(0.62f, 1, 2, 3)]
        [TestCase(1f, 1, 6, 5)]
        [TestCase(1.45f, 7, 6, 8)]
        public void ProductionFeelRuntime_SelectsOneSpringPerTransformChannel(
            float intensity,
            int expectedPositionIndex,
            int expectedRotationIndex,
            int expectedScaleIndex)
        {
            const string root = "Assets/ProjectMT/05_Art/FeelPresets/BasicAttack/Production";
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            Assert.That(paths, Has.Length.EqualTo(7));

            foreach (var path in paths)
            {
                var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                try
                {
                    var adapter = instance.GetComponents<MonoBehaviour>().Single(component =>
                        component != null && component.GetType().FullName ==
                        "ProjectMT.Integrations.Feel.BasicAttackFeelRuntimeAdapter");
                    var adapterType = adapter.GetType();
                    adapterType.GetMethod("EnsureRuntimeSafeSpringFeedbacks",
                            BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.Invoke(adapter, null);
                    var selected = adapterType.GetMethod("SelectTargetMotionFeedbacks",
                            BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.Invoke(adapter, new object[] { intensity });
                    Assert.That(selected, Is.EqualTo(true), path);

                    var player = adapterType.GetProperty("Player")?.GetValue(adapter);
                    var feedbacks = ((IEnumerable)player?.GetType().GetField("FeedbacksList")
                            ?.GetValue(player))
                        ?.Cast<object>()
                        .ToArray();
                    Assert.That(feedbacks, Is.Not.Null, path);
                    Assert.That(ActiveIndices(feedbacks, "MMF_PositionSpring"),
                        Is.EqualTo(new[] { expectedPositionIndex }), path);
                    Assert.That(ActiveIndices(feedbacks, "MMF_RotationSpring"),
                        Is.EqualTo(new[] { expectedRotationIndex }), path);
                    Assert.That(ActiveIndices(feedbacks, "MMF_ScaleSpring", "MMF_SquashAndStretchSpring"),
                        Is.EqualTo(new[] { expectedScaleIndex }), path);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        [Test]
        public void ProductionFeelRuntime_RepairsInvalidScaleAndHandsRepeatedHitOwnershipOverCleanly()
        {
            const string path =
                "Assets/ProjectMT/05_Art/FeelPresets/BasicAttack/Production/BAFeel_DirectHit.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var first = UnityEngine.Object.Instantiate(prefab);
            var second = UnityEngine.Object.Instantiate(prefab);
            var target = new GameObject("FeelRuntimeScaleSafetyTarget");
            var baseline = new Vector3(0.75f, 1.2f, 1.6f);
            target.transform.localScale = baseline;
            try
            {
                var firstAdapter = first.GetComponents<MonoBehaviour>().Single(component =>
                    component != null && component.GetType().FullName ==
                    "ProjectMT.Integrations.Feel.BasicAttackFeelRuntimeAdapter");
                var secondAdapter = second.GetComponents<MonoBehaviour>().Single(component =>
                    component != null && component.GetType().FullName ==
                    "ProjectMT.Integrations.Feel.BasicAttackFeelRuntimeAdapter");
                var adapterType = firstAdapter.GetType();
                var prepare = adapterType.GetMethod("EnsureRuntimeSafeSpringFeedbacks",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var select = adapterType.GetMethod("SelectTargetMotionFeedbacks",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var acquire = adapterType.GetMethod("AcquireTargetMotion",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(prepare, Is.Not.Null);
                Assert.That(select, Is.Not.Null);
                Assert.That(acquire, Is.Not.Null);

                prepare.Invoke(firstAdapter, null);
                select.Invoke(firstAdapter, new object[] { 0.62f });
                var player = adapterType.GetProperty("Player")?.GetValue(firstAdapter);
                var feedbacks = ((IEnumerable)player?.GetType().GetField("FeedbacksList")
                        ?.GetValue(player))
                    ?.Cast<object>()
                    .ToArray();
                var safeSquash = feedbacks?[3];
                Assert.That(safeSquash?.GetType().Name,
                    Is.EqualTo("BasicAttackSafeSquashAndStretchSpring"));
                FindField(safeSquash.GetType(), "AnimateScaleTarget")?.SetValue(safeSquash, target.transform);
                safeSquash.GetType().GetMethod("GetInitialValues",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(safeSquash, null);
                FindField(safeSquash.GetType(), "_currentValue")?.SetValue(safeSquash, float.NaN);
                FindField(safeSquash.GetType(), "_velocity")?.SetValue(safeSquash, float.PositiveInfinity);
                safeSquash.GetType().GetMethod("ApplyValue",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(safeSquash, null);
                Assert.That(target.transform.localScale, Is.EqualTo(baseline));

                acquire.Invoke(firstAdapter, new object[] { target });
                target.transform.localScale = Vector3.one * 9f;
                prepare.Invoke(secondAdapter, null);
                select.Invoke(secondAdapter, new object[] { 1.45f });
                acquire.Invoke(secondAdapter, new object[] { target });
                Assert.That(target.transform.localScale, Is.EqualTo(baseline),
                    "새 명중은 이전 FEEL의 변형을 먼저 원복한 뒤 대상 소유권을 이어받아야 합니다.");
            }
            finally
            {
                if (first.GetComponent(typeof(IBasicAttackFeelRuntime)) is IBasicAttackFeelRuntime firstRuntime)
                {
                    firstRuntime.ResetBasicAttackFeel();
                }
                if (second.GetComponent(typeof(IBasicAttackFeelRuntime)) is IBasicAttackFeelRuntime secondRuntime)
                {
                    secondRuntime.ResetBasicAttackFeel();
                }
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        private static int[] ActiveIndices(object[] feedbacks, params string[] baseTypeNames)
        {
            return feedbacks
                .Select((feedback, index) => new { feedback, index })
                .Where(item => item.feedback != null &&
                               (bool)FindField(item.feedback.GetType(), "Active").GetValue(item.feedback) &&
                               baseTypeNames.Any(name => InheritsFrom(item.feedback.GetType(), name)))
                .Select(item => item.index)
                .ToArray();
        }

        private static bool InheritsFrom(Type type, string typeName)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current.Name == typeName)
                {
                    return true;
                }
            }
            return false;
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
            }
            return null;
        }

        [Test]
        public void EveryProductionAction_KeepsDeliveryPrefabAndDraftResolvedTuningInSync()
        {
            var paths = AssetDatabase.FindAssets("t:MonsterCombatProfile", new[] { MonsterRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToArray();
            Assert.That(paths.Length, Is.GreaterThanOrEqualTo(44));
            foreach (var path in paths)
            {
                var combat = AssetDatabase.LoadAssetAtPath<MonsterCombatProfile>(path);
                var action = combat?.Action;
                var profile = action?.BasicAttackProfile;
                Assert.That(profile, Is.Not.Null, path);
                if (profile.CombatType == MonsterCombatType.Melee)
                {
                    Assert.That(action, Is.TypeOf<MeleeActionDefinition>(), path);
                    continue;
                }

                Assert.That(action, Is.TypeOf<ProjectileActionDefinition>(), path);
                var projectile = (ProjectileActionDefinition)action;
                var monsterId = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path));
                var draft = AssetDatabase.LoadMainAssetAtPath($"{DraftRoot}/Draft_{monsterId}.asset");
                Assert.That(draft, Is.Not.Null, $"{path}에 대응하는 Draft가 없습니다.");
                var resolvedSpeed = (float)draft.GetType().GetProperty("ResolvedProjectileSpeed")
                    .GetValue(draft);
                var resolvedLifetime = (float)draft.GetType().GetProperty("ResolvedProjectileLifetime")
                    .GetValue(draft);
                var resolvedHitRadius = (float)draft.GetType().GetProperty("ResolvedProjectileHitRadius")
                    .GetValue(draft);
                var overridesProfile = (bool)draft.GetType().GetProperty("OverrideProjectileTuning")
                    .GetValue(draft);
                Assert.That(
                    projectile.DeliveryMode,
                    Is.EqualTo(profile.UsesProjectileVisual
                        ? MonsterRangedDeliveryMode.Projectile
                        : MonsterRangedDeliveryMode.Instant),
                    path);
                Assert.That(projectile.OverrideBasicAttackProfileTuning, Is.EqualTo(overridesProfile), path);
                Assert.That(projectile.ResolvedSpeed, Is.EqualTo(resolvedSpeed).Within(0.001f), path);
                Assert.That(projectile.ResolvedLifetime, Is.EqualTo(resolvedLifetime).Within(0.001f), path);
                Assert.That(projectile.ResolvedHitRadius, Is.EqualTo(resolvedHitRadius).Within(0.001f), path);
                Assert.That(projectile.HitRadius, Is.EqualTo(resolvedHitRadius).Within(0.001f), path);
                Assert.That(projectile.Mode, Is.EqualTo(profile.LegacyProjectileMode), path);
                Assert.That(projectile.MaxPiercingTargets, Is.EqualTo(profile.MaxTargets), path);
                Assert.That(projectile.ImpactRadius, Is.EqualTo(profile.Radius).Within(0.001f), path);
                Assert.That(projectile.MaxImpactTargets, Is.EqualTo(profile.MaxTargets), path);
                Assert.That(
                    projectile.ProjectilePrefab != null,
                    Is.EqualTo(profile.UsesProjectileVisual),
                    path);
            }
        }

        [Test]
        public void Validator_RejectsProjectileTuningThatCannotReachTheAuthoredMaximumRange()
        {
            var draft = CloneDraft("rabi_queen_01");
            try
            {
                var serialized = new SerializedObject(draft);
                serialized.FindProperty("overrideProjectileTuning").boolValue = true;
                serialized.FindProperty("projectileSpeed").floatValue = 0.5f;
                serialized.FindProperty("projectileLifetime").floatValue = 0.5f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                CollectionAssert.Contains(ValidateDraftCodes(draft), "MAKER-PROJECTILE-RANGE");

                serialized.Update();
                serialized.FindProperty("projectileSpeed").floatValue = 20f;
                serialized.FindProperty("projectileLifetime").floatValue = 1f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                CollectionAssert.DoesNotContain(ValidateDraftCodes(draft), "MAKER-PROJECTILE-RANGE");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void ProfileValidation_RejectsMovingMeleeDeliveryAndIrrelevantDirectionalSweep()
        {
            var profile = CloneProfile("BA_R_01");
            try
            {
                var serialized = new SerializedObject(profile);
                serialized.FindProperty("combatType").enumValueIndex = (int)MonsterCombatType.Melee;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(profile.TryValidate(out var error), Is.False);
                StringAssert.Contains("requires Ranged", error);

                EditorUtility.CopySerialized(LoadProfile("BA_R_01"), profile);
                serialized = new SerializedObject(profile);
                serialized.FindProperty("sweepDirection").enumValueIndex =
                    (int)MonsterBasicAttackSweepDirection.LeftToRight;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(profile.TryValidate(out error), Is.False);
                StringAssert.Contains("Directional sweep", error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ExplicitPresetAssignment_AdoptsProfileTuningWhileMigrationCanPreserveLegacyOverride()
        {
            var draft = CloneDraft("rabi_queen_01");
            try
            {
                var draftType = draft.GetType();
                var profile = LoadProfile("BA_R_01");
                draftType.GetMethod("EditorSetBasicAttackProfile").Invoke(draft, new object[] { profile });
                draftType.GetMethod("EditorPreserveLegacyProjectileTuning").Invoke(draft, null);
                Assert.That((bool)draftType.GetProperty("OverrideProjectileTuning").GetValue(draft), Is.True,
                    "기존 Ru의 12.5 속도는 전체 매칭/마이그레이션에서 보존되어야 합니다.");
                Assert.That((float)draftType.GetProperty("ResolvedProjectileSpeed").GetValue(draft),
                    Is.EqualTo(12.5f).Within(0.001f));

                draftType.GetMethod("EditorAdoptBasicAttackProfileTuning").Invoke(draft, null);
                Assert.That((bool)draftType.GetProperty("OverrideProjectileTuning").GetValue(draft), Is.False,
                    "사용자가 프리셋을 명시적으로 선택하면 숨은 legacy 값이 새 프리셋을 덮으면 안 됩니다.");
                Assert.That((float)draftType.GetProperty("ResolvedProjectileSpeed").GetValue(draft),
                    Is.EqualTo(profile.ProjectileSpeed).Within(0.001f));
                Assert.That((float)draftType.GetProperty("ProjectileSpeed").GetValue(draft),
                    Is.EqualTo(profile.ProjectileSpeed).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void EveryOfficialProfile_CompletesWorkshopAndMonsterMakerPreviewWithoutLingeringTickOrProjectile()
        {
            var workshopType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterBasicAttackWorkshopWindow");
            var previewType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerPreviewStage");
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var attackId in ExpectedSignatures.Keys.OrderBy(id => id, StringComparer.Ordinal))
            {
                var profile = LoadProfile(attackId);
                var draft = CloneDraft("rabi_queen_01");
                AssignProfileAndClearOverrides(draft, profile);

                var workshop = ScriptableObject.CreateInstance(workshopType);
                try
                {
                    workshopType.GetField("originDraft", flags).SetValue(workshop, draft);
                    workshopType.GetMethod("LoadProfile", flags).Invoke(workshop, new object[] { profile });
                    Assert.That((bool)workshopType.GetField("previewUpdateSubscribed", flags).GetValue(workshop),
                        Is.False, attackId + " / idle update");
                    var movers = (ICollection)workshopType.GetField("previewAttackMovers", flags).GetValue(workshop);
                    Assert.That(movers.Count,
                        Is.EqualTo(profile.UsesProjectileVisual ? profile.ProjectileCount : 0),
                        attackId + " / preview mover count");
                    var duration = (float)workshopType.GetMethod("ResolvePreviewDuration", flags)
                        .Invoke(workshop, null);
                    var impactTimes = ReadFloatList(workshopType
                        .GetMethod("ResolvePreviewImpactTimesSeconds", flags)
                        .Invoke(workshop, new object[] { duration }));
                    Assert.That(impactTimes.Count, Is.EqualTo(profile.HitCount), attackId + " / impact count");
                    Assert.That(impactTimes, Is.Ordered, attackId + " / impact ordering");

                    workshopType.GetMethod("PlayPreviewAttack", flags).Invoke(workshop, null);
                    workshopType.GetField("previewPlaybackStart", flags)
                        .SetValue(workshop, EditorApplication.timeSinceStartup - 100d);
                    workshopType.GetMethod("TickPreviewPlayback", flags).Invoke(workshop, null);
                    Assert.That((bool)workshopType.GetField("previewUpdateSubscribed", flags).GetValue(workshop),
                        Is.False, attackId + " / completed update");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(workshop);
                }

                var preview = Activator.CreateInstance(previewType);
                try
                {
                    previewType.GetMethod("SetDraft", flags).Invoke(preview, new[] { draft });
                    Assert.That((bool)previewType.GetMethod("ShowBasicAttackArea", flags).Invoke(preview, null),
                        Is.True, attackId + " / hit area");
                    Assert.That((int)previewType.GetProperty("ActiveHitAreaCount", flags).GetValue(preview),
                        Is.GreaterThan(0), attackId + " / visible hit area");
                    previewType.GetMethod("PlayAttack", flags).Invoke(preview, new object[] { 0 });
                    var tick = previewType.GetMethod("Tick", flags, null, new[] { typeof(float) }, null);
                    for (var index = 0; index < 1000 &&
                         ((int)previewType.GetProperty("PreviewHitCount", flags).GetValue(preview) == 0 ||
                          (bool)previewType.GetProperty("RequiresContinuousTick", flags).GetValue(preview));
                         index++)
                    {
                        tick.Invoke(preview, new object[] { 0.02f });
                    }
                    Assert.That((int)previewType.GetProperty("PreviewHitCount", flags).GetValue(preview),
                        Is.GreaterThanOrEqualTo(1), attackId + " / preview damage");
                    Assert.That((int)previewType.GetProperty("ActiveProjectileCount", flags).GetValue(preview),
                        Is.Zero, attackId + " / projectile cleanup");
                    Assert.That((bool)previewType.GetProperty("RequiresContinuousTick", flags).GetValue(preview),
                        Is.False, attackId + " / continuous tick cleanup");
                }
                finally
                {
                    (preview as IDisposable)?.Dispose();
                    UnityEngine.Object.DestroyImmediate(draft);
                }
            }
        }

        [Test]
        public void EveryOfficialProfile_StaysInsideMinimumWorkshopWidth()
        {
            var windowType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterBasicAttackWorkshopWindow");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            try
            {
                window.position = new Rect(70f, 70f, 1100f, 700f);
                window.ShowUtility();
                foreach (var attackId in ExpectedSignatures.Keys.OrderBy(id => id, StringComparer.Ordinal))
                {
                    windowType.GetMethod("LoadProfile", flags).Invoke(window, new object[] { LoadProfile(attackId) });
                    var recipe = windowType.GetField("recipe", flags).GetValue(window);
                    var recipeType = recipe.GetType();
                    recipeType.GetField("displayName").SetValue(recipe, new string('라', 120));
                    recipeType.GetField("designMemo").SetValue(recipe, new string('마', 240));
                    var vfxSlots = (IList)recipeType.GetField("vfxSlots").GetValue(recipe);
                    if (vfxSlots.Count > 0)
                    {
                        var slot = vfxSlots[0];
                        slot.GetType().GetField("displayName").SetValue(slot, new string('바', 120));
                        slot.GetType().GetField("description").SetValue(slot, new string('사', 240));
                    }
                    window.SendEvent(new Event { type = EventType.Layout });
                    window.SendEvent(new Event { type = EventType.Repaint });

                    Rect ReadRect(string field) => (Rect)windowType.GetField(field, flags).GetValue(window);
                    var content = ReadRect("lastAssemblerContentRect");
                    var viewport = ReadRect("lastAssemblerViewportRect");
                    Assert.That(content.width, Is.EqualTo(450f).Within(0.1f), attackId);
                    Assert.That(viewport.width, Is.GreaterThanOrEqualTo(content.width), attackId);
                    Assert.That(((Vector2)windowType.GetField("recipeScroll", flags).GetValue(window)).x,
                        Is.Zero, attackId + " / 중앙 가로 스크롤");
                    Assert.That(((Vector2)windowType.GetField("libraryScroll", flags).GetValue(window)).x,
                        Is.Zero, attackId + " / 목록 가로 스크롤");

                    var vfx = ReadRect("lastVfxHeaderRightmostRect");
                    if (vfx.width > 0f)
                    {
                        Assert.That(vfx.xMax, Is.LessThanOrEqualTo(content.width + 0.1f),
                            attackId + " / VFX 공간 버튼");
                    }
                    var panel = ReadRect("lastAssemblerPanelRect");
                    var save = ReadRect("lastSaveRightmostRect");
                    var preview = ReadRect("lastPreviewColumnRect");
                    var previewAction = ReadRect("lastPreviewToolbarRightmostRect");
                    Assert.That(save.xMax, Is.LessThanOrEqualTo(panel.xMax + 0.1f), attackId + " / 저장 버튼");
                    Assert.That(preview.xMax, Is.LessThanOrEqualTo(window.position.width + 0.1f),
                        attackId + " / 미리보기 열");
                    Assert.That(previewAction.xMax, Is.LessThanOrEqualTo(preview.xMax + 0.1f),
                        attackId + " / VFX 위치 버튼");
                }
            }
            finally
            {
                MonsterEditorWindowTestUtility.Close(window);
            }
            MonsterEditorWindowTestUtility.AssertNoOrphanedContainers("기본공격 조립소");
        }

        [Test]
        public void WorkshopTimeline_SeparatesMotionMarkerFromProjectileArrivalAndBurstFollowups()
        {
            var windowType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterBasicAttackWorkshopWindow");
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var window = ScriptableObject.CreateInstance(windowType);
            try
            {
                var ru = AssetDatabase.LoadMainAssetAtPath($"{DraftRoot}/Draft_rabi_queen_01.asset");
                windowType.GetField("originDraft", flags).SetValue(window, ru);
                windowType.GetMethod("LoadProfile", flags).Invoke(window, new object[] { LoadProfile("BA_R_01") });
                var duration = (float)windowType.GetMethod("ResolvePreviewDuration", flags).Invoke(window, null);
                var activation = (float)windowType.GetMethod("ResolvePreviewActivationTimeSeconds", flags)
                    .Invoke(window, new object[] { duration });
                var impactTimes = ReadFloatList(windowType.GetMethod("ResolvePreviewImpactTimesSeconds", flags)
                    .Invoke(window, new object[] { duration }));
                var marker = ReadFirstMarkerTime(ru);
                Assert.That(activation, Is.EqualTo(duration * marker).Within(0.0001f));
                Assert.That(impactTimes.Count, Is.EqualTo(1));
                Assert.That(impactTimes[0], Is.GreaterThan(activation + 0.2f), "투사체는 Marker에서 즉시 명중하면 안 됩니다.");

                var lucy = AssetDatabase.LoadMainAssetAtPath($"{DraftRoot}/Draft_lucy_01.asset");
                windowType.GetField("originDraft", flags).SetValue(window, lucy);
                windowType.GetMethod("LoadProfile", flags).Invoke(window, new object[] { LoadProfile("BA_M_06") });
                duration = (float)windowType.GetMethod("ResolvePreviewDuration", flags).Invoke(window, null);
                impactTimes = ReadFloatList(windowType.GetMethod("ResolvePreviewImpactTimesSeconds", flags)
                    .Invoke(window, new object[] { duration }));
                Assert.That(impactTimes.Count, Is.EqualTo(3));
                Assert.That(impactTimes[1] - impactTimes[0], Is.EqualTo(0.08f).Within(0.0001f));
                Assert.That(impactTimes[2] - impactTimes[1], Is.EqualTo(0.08f).Within(0.0001f));

                windowType.GetMethod("LoadProfile", flags).Invoke(window, new object[] { LoadProfile("BA_R_05") });
                var movers = (ICollection)windowType.GetField("previewAttackMovers", flags).GetValue(window);
                Assert.That(movers.Count, Is.EqualTo(3), "3발 확산은 Preview에서도 이동체 3개가 보여야 합니다.");

                windowType.GetMethod("PlayPreviewAttack", flags).Invoke(window, null);
                Assert.That((bool)windowType.GetField("previewUpdateSubscribed", flags).GetValue(window), Is.True);
                windowType.GetField("previewPlaybackStart", flags)
                    .SetValue(window, EditorApplication.timeSinceStartup - 100d);
                windowType.GetMethod("TickPreviewPlayback", flags).Invoke(window, null);
                Assert.That((bool)windowType.GetField("previewUpdateSubscribed", flags).GetValue(window), Is.False,
                    "재생이 끝난 조립소가 Editor update를 계속 점유하면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MonsterMakerPreview_UsesPresentationSlotVfxAndPreservesBurstIntervals()
        {
            var draft = CloneDraft("lucy_01");
            var profile = CloneProfile("BA_M_06");
            var launchVfx = new GameObject("CommercialLaunchVfxProbe");
            var impactVfx = new GameObject("CommercialImpactVfxProbe");
            try
            {
                AssignProfileAndClearOverrides(draft, profile);
                AssignPresentationVfx(
                    draft,
                    profile,
                    MonsterBasicAttackVfxEvent.RecipeExecute,
                    launchVfx);
                AssignPresentationVfx(
                    draft,
                    profile,
                    MonsterBasicAttackVfxEvent.TargetDamaged,
                    impactVfx);

                var previewType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerPreviewStage");
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var preview = Activator.CreateInstance(previewType);
                try
                {
                    previewType.GetMethod("SetDraft", flags).Invoke(preview, new object[] { draft });
                    previewType.GetMethod("PlayAttack", flags).Invoke(preview, new object[] { 0 });
                    var tick = previewType.GetMethod("Tick", flags, null, new[] { typeof(float) }, null);
                    var hitCount = previewType.GetProperty("PreviewHitCount", flags);
                    for (var index = 0; index < 300 && (int)hitCount.GetValue(preview) == 0; index++)
                    {
                        tick.Invoke(preview, new object[] { 0.01f });
                    }

                    Assert.That((int)hitCount.GetValue(preview), Is.EqualTo(1));
                    Assert.That(
                        (int)previewType.GetProperty("ActiveMarkerVfxCount", flags).GetValue(preview),
                        Is.GreaterThanOrEqualTo(2),
                        "연출 슬롯의 공격 실행 VFX와 첫 실제 피해 VFX가 모두 보여야 합니다.");
                    tick.Invoke(preview, new object[] { 0.07f });
                    Assert.That((int)hitCount.GetValue(preview), Is.EqualTo(1));
                    tick.Invoke(preview, new object[] { 0.02f });
                    Assert.That((int)hitCount.GetValue(preview), Is.EqualTo(2));
                    tick.Invoke(preview, new object[] { 0.08f });
                    Assert.That((int)hitCount.GetValue(preview), Is.EqualTo(3));
                }
                finally
                {
                    (preview as IDisposable)?.Dispose();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(launchVfx);
                UnityEngine.Object.DestroyImmediate(impactVfx);
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void MonsterMakerPreview_UsesProfileProjectileVfxUntilActualImpact()
        {
            var draft = CloneDraft("rabi_queen_01");
            var profile = CloneProfile("BA_R_01");
            var launchVfx = new GameObject("CommercialProjectileLaunchVfxProbe");
            var projectileVfx = new GameObject("CommercialProjectileBodyVfxProbe");
            var impactVfx = new GameObject("CommercialProjectileImpactVfxProbe");
            try
            {
                projectileVfx.transform.localScale = Vector3.one * 1.4f;
                profile.EditorSetPresentationFeedback(
                    CreateFeedback(launchVfx, 2f),
                    CreateFeedback(projectileVfx, 3f, new Vector3(0.12f, 0f, 0f), 1.25f),
                    CreateFeedback(impactVfx, 2f));
                AssignProfileAndClearOverrides(draft, profile);

                var previewType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerPreviewStage");
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var preview = Activator.CreateInstance(previewType);
                try
                {
                    previewType.GetMethod("SetDraft", flags).Invoke(preview, new object[] { draft });
                    previewType.GetMethod("PlayAttack", flags).Invoke(preview, new object[] { 0 });
                    var tick = previewType.GetMethod("Tick", flags, null, new[] { typeof(float) }, null);
                    var projectileCount = previewType.GetProperty("ActiveProjectileCount", flags);
                    var hitCount = previewType.GetProperty("PreviewHitCount", flags);
                    for (var index = 0; index < 300 && (int)projectileCount.GetValue(preview) == 0; index++)
                    {
                        tick.Invoke(preview, new object[] { 0.01f });
                    }

                    Assert.That((int)projectileCount.GetValue(preview), Is.EqualTo(1));
                    Assert.That((int)hitCount.GetValue(preview), Is.Zero, "Marker는 발사이고 피해는 도착 뒤입니다.");
                    var movingVisual = Resources.FindObjectsOfTypeAll<GameObject>()
                        .FirstOrDefault(item => item.name.Contains("CommercialProjectileBodyVfxProbe"));
                    Assert.That(movingVisual, Is.Not.Null, "Profile 이동 VFX가 legacy fallback보다 우선해야 합니다.");

                    for (var index = 0; index < 500 && (int)hitCount.GetValue(preview) == 0; index++)
                    {
                        tick.Invoke(preview, new object[] { 0.01f });
                    }
                    Assert.That((int)hitCount.GetValue(preview), Is.EqualTo(1));
                    Assert.That((int)projectileCount.GetValue(preview), Is.Zero);
                    Assert.That(
                        Resources.FindObjectsOfTypeAll<GameObject>()
                            .Any(item => item.name.Contains("CommercialProjectileImpactVfxProbe")),
                        Is.True,
                        "Profile 명중 VFX는 실제 피해 때 보여야 합니다.");
                }
                finally
                {
                    (preview as IDisposable)?.Dispose();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(launchVfx);
                UnityEngine.Object.DestroyImmediate(projectileVfx);
                UnityEngine.Object.DestroyImmediate(impactVfx);
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void MonsterMakerPreview_UsesTheMonstersResolvedProjectileOverrideInsteadOfSharedProfileSpeed()
        {
            var draft = CloneDraft("rabi_queen_01");
            var previewType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerPreviewStage");
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var preview = Activator.CreateInstance(previewType);
            try
            {
                Assert.That((float)draft.GetType().GetProperty("ResolvedProjectileSpeed").GetValue(draft),
                    Is.EqualTo(12.5f).Within(0.001f));
                previewType.GetMethod("SetDraft", flags).Invoke(preview, new object[] { draft });
                previewType.GetMethod("PlayAttack", flags).Invoke(preview, new object[] { 0 });
                var tick = previewType.GetMethod("Tick", flags, null, new[] { typeof(float) }, null);
                for (var index = 0; index < 300 &&
                     (int)previewType.GetProperty("ActiveProjectileCount", flags).GetValue(preview) == 0;
                     index++)
                {
                    tick.Invoke(preview, new object[] { 0.01f });
                }

                var active = previewType.GetField("activeProjectiles", flags).GetValue(preview) as IList;
                Assert.That(active, Is.Not.Null.And.Not.Empty);
                var speed = (float)active[0].GetType().GetProperty("Speed").GetValue(active[0]);
                Assert.That(speed, Is.EqualTo(12.5f).Within(0.001f),
                    "Preview 비행 속도는 Writer/Runtime과 같은 Draft 해석값이어야 합니다.");
            }
            finally
            {
                (preview as IDisposable)?.Dispose();
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        private static string BuildSignature(MonsterBasicAttackProfile profile)
        {
            var values = new[]
            {
                profile.CombatType.ToString(), profile.DeliveryModule.ToString(), profile.CollisionModule.ToString(),
                profile.SequenceModule.ToString(), profile.MovementModule.ToString(), profile.PresentationKind.ToString(),
                profile.Shape.ToString(), profile.Center.ToString(), profile.ProjectileTravel.ToString(),
                profile.SweepDirection.ToString(), F(profile.RangeMultiplier), F(profile.Radius), F(profile.Angle),
                F(profile.LineWidth), profile.MaxTargets.ToString(), profile.ProjectileCount.ToString(),
                F(profile.ProjectileSpreadAngle), F(profile.ProjectileSpeed), F(profile.ProjectileLifetime),
                F(profile.ProjectileCollisionRadius),
                string.Join(",", Enumerable.Range(0, profile.HitCount).Select(index => F(profile.ResolveDamageRatio(index)))),
                F(profile.SecondaryDamageRatio), F(profile.RepeatHitInterval), F(profile.DashDistance),
                F(profile.DashDuration)
            };
            return string.Join("|", values);
        }

        private static string F(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static MonsterBasicAttackProfile LoadProfile(string id)
        {
            return AssetDatabase.LoadAssetAtPath<MonsterBasicAttackProfile>($"{ProfileRoot}/{id}.asset");
        }

        private static UnityEngine.Object FindDraftUsing(MonsterBasicAttackProfile profile)
        {
            return AssetDatabase.FindAssets("t:MonsterMakerDraft", new[] { DraftRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadMainAssetAtPath)
                .FirstOrDefault(draft => draft != null &&
                    ReferenceEquals(draft.GetType().GetProperty("BasicAttackProfile")?.GetValue(draft), profile));
        }

        private static MonsterBasicAttackProfile CloneProfile(string id)
        {
            var result = ScriptableObject.CreateInstance<MonsterBasicAttackProfile>();
            EditorUtility.CopySerialized(LoadProfile(id), result);
            return result;
        }

        private static string[] ValidateDraftCodes(ScriptableObject draft)
        {
            var validatorType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerValidator");
            var report = validatorType.GetMethod("Validate", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { draft });
            var issues = report.GetType().GetProperty("Issues").GetValue(report) as IEnumerable;
            return issues.Cast<object>()
                .Select(issue => (string)issue.GetType().GetProperty("Code").GetValue(issue))
                .ToArray();
        }

        private static ScriptableObject CloneDraft(string id)
        {
            var source = AssetDatabase.LoadMainAssetAtPath($"{DraftRoot}/Draft_{id}.asset") as ScriptableObject;
            Assert.That(source, Is.Not.Null, id);
            var result = ScriptableObject.CreateInstance(source.GetType());
            EditorUtility.CopySerialized(source, result);
            return result;
        }

        private static MonsterFeedbackCue CreateFeedback(
            GameObject vfx,
            float lifetime,
            Vector3 position = default,
            float scale = 1f)
        {
            var result = new MonsterFeedbackCue();
            result.EditorConfigure(null, vfx, lifetime, position, Vector3.zero, scale);
            return result;
        }

        private static void AssignPresentationVfx(
            ScriptableObject draft,
            MonsterBasicAttackProfile profile,
            MonsterBasicAttackVfxEvent eventType,
            GameObject prefab)
        {
            var slot = profile.VfxSlots.First(candidate =>
                candidate != null &&
                candidate.EventType == eventType &&
                !candidate.IsDeliveryVisual);
            var serialized = new SerializedObject(draft);
            var bindings = serialized.FindProperty("basicAttackVfxBindings");
            bindings.InsertArrayElementAtIndex(bindings.arraySize);
            var binding = bindings.GetArrayElementAtIndex(bindings.arraySize - 1);
            binding.FindPropertyRelative("attackId").stringValue = profile.AttackId;
            binding.FindPropertyRelative("slotId").stringValue = slot.SlotId;
            binding.FindPropertyRelative("motionId").stringValue =
                slot.AssignmentScope == MonsterBasicAttackVfxAssignmentScope.MotionSpecific
                    ? serialized.FindProperty("attacks").GetArrayElementAtIndex(0)
                        .FindPropertyRelative("motionId").stringValue
                    : string.Empty;
            binding.FindPropertyRelative("state").enumValueIndex =
                (int)MonsterBasicAttackVfxAssignmentState.Assigned;
            binding.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            binding.FindPropertyRelative("sound").objectReferenceValue = null;
            binding.FindPropertyRelative("sfx").objectReferenceValue = null;
            binding.FindPropertyRelative("lifetime").floatValue = 2f;
            binding.FindPropertyRelative("playbackOffset").floatValue = 0f;
            binding.FindPropertyRelative("playbackSpeed").floatValue = 1f;
            binding.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
            binding.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
            binding.FindPropertyRelative("scale").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignProfileAndClearOverrides(ScriptableObject draft, MonsterBasicAttackProfile profile)
        {
            var serialized = new SerializedObject(draft);
            serialized.FindProperty("basicAttackProfile").objectReferenceValue = profile;
            serialized.FindProperty("combatType").enumValueIndex = (int)profile.CombatType;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static float ReadFirstMarkerTime(UnityEngine.Object draft)
        {
            var serialized = new SerializedObject(draft);
            return serialized.FindProperty("attacks")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("markers")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("normalizedTime")
                .floatValue;
        }

        private static List<float> ReadFloatList(object value)
        {
            return ((IEnumerable)value).Cast<float>().ToList();
        }

        private static Type FindEditorType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }
            Assert.Fail($"Editor type not found: {fullName}");
            return null;
        }
    }
}
