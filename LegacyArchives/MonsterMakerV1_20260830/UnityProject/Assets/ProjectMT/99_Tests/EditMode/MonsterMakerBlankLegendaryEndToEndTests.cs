using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Tests.EditMode
{
    public sealed class MonsterMakerBlankLegendaryEndToEndTests // 빈 조립소부터 전설 운영 재편입까지 원복하는 수직 회귀
    {
        private const string SourceDraftPath =
            "Assets/ProjectMT/Editor/MonsterMaker/Drafts/Draft_werewolf_01.asset";
        private const string ProjectilePrefabPath =
            "Assets/ProjectMT/02_Shared/Combat/Prefabs/PF_SeedProjectile.prefab";
        private const string LaunchVfxPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Prefabs/TurretVfx/VFX_CR_Fireball_Muzzle.prefab";
        private const string ProjectileVfxPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Prefabs/TurretVfx/VFX_CR_Fireball_Projectile_Orange.prefab";
        private const string ImpactVfxPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Prefabs/TurretVfx/VFX_CR_Fireball_Impact.prefab";
        private const string LaunchSfxPath =
            "Assets/ProjectMT/06_Audio/SFX/CastleRaid/SFX_CR_Turret_Fireball_Fire.asset";
        private const string ImpactSfxPath =
            "Assets/ProjectMT/06_Audio/SFX/CastleRaid/SFX_CR_Turret_Fireball_Explosion.asset";
        private const string MonsterCatalogPath =
            "Assets/ProjectMT/02_Shared/Unit/Data/MonsterCatalog.asset";
        private const string RarityCatalogPath =
            "Assets/ProjectMT/02_Shared/Unit/Data/MonsterRarityCatalog.asset";
        private const string CastleAiCatalogPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Resources/HexCastleAssaultAIProfileCatalog.asset";
        private const string MainBattleAiCatalogPath =
            "Assets/ProjectMT/03_Features/MainBattle/Resources/MainBattleAIProfileCatalog.asset";

        private static readonly string[] ProductionCatalogPaths =
        {
            MonsterCatalogPath,
            RarityCatalogPath,
            CastleAiCatalogPath,
            MainBattleAiCatalogPath
        };

        [Test]
        public void BlankWorkshop_ComposesPersistsReopensAssignsAndRepublishesLegendaryWithoutResidue()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var monsterId = "qa_legend_" + suffix;
            var attackId = "BA_R_QA_" + suffix;
            var draftPath = $"Assets/ProjectMT/Editor/MonsterMaker/Drafts/Draft_{monsterId}.asset";
            var attackPath = $"Assets/ProjectMT/02_Shared/Unit/Data/BasicAttacks/Custom/{attackId}.asset";
            var dataFolder = $"Assets/ProjectMT/02_Shared/Unit/Data/Monsters/{monsterId}";
            var artFolder = $"Assets/ProjectMT/05_Art/Monsters/{monsterId}";
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssertNoResidue(draftPath, attackPath, dataFolder, artFolder);

            var beforeCatalogs = ProductionCatalogPaths.ToDictionary(path => path, ReadAssetBytes);
            var beforeSelection = Selection.activeObject;
            EditorWindow makerWindow = null;
            try
            {
                var draft = CreateLegendaryDraftWithoutAttack(monsterId, draftPath);
                var basicAttack = ComposePersistReopenUpdateAndAssign(draft, attackId, attackPath);
                AssertMakerValidationPasses(draft);

                makerWindow = OpenMakerAndExercisePreview(draft);
                var firstWrite = InvokeWriter(draft);
                var firstPaths = VerifyGeneratedLegendary(
                    firstWrite,
                    basicAttack,
                    monsterId,
                    "폭풍의 감시자 QA",
                    false);
                var firstGuids = firstPaths.ToDictionary(path => path, AssetDatabase.AssetPathToGUID);

                var serialized = new SerializedObject(draft);
                serialized.FindProperty("displayName").stringValue = "폭풍의 감시자 QA 개정";
                serialized.FindProperty("attackPower").floatValue = 47.5f;
                serialized.FindProperty("productionMemo").stringValue += " / 수정 재편입 검증 완료";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(draft);
                AssetDatabase.SaveAssetIfDirty(draft);

                var secondWrite = InvokeWriter(draft);
                var secondPaths = VerifyGeneratedLegendary(
                    secondWrite,
                    basicAttack,
                    monsterId,
                    "폭풍의 감시자 QA 개정",
                    true);
                CollectionAssert.AreEquivalent(firstPaths, secondPaths);
                foreach (var path in secondPaths)
                {
                    Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(firstGuids[path]), path);
                }
            }
            finally
            {
                Selection.activeObject = beforeSelection;
                if (makerWindow != null)
                {
                    MonsterEditorWindowTestUtility.Close(makerWindow);
                }

                DeleteIfPresent(draftPath);
                DeleteIfPresent(attackPath);
                DeleteIfPresent(dataFolder);
                DeleteIfPresent(artFolder);
                RestoreCatalogs(beforeCatalogs);
            }

            MonsterEditorWindowTestUtility.AssertNoOrphanedContainers("Monster Maker");
            foreach (var pair in beforeCatalogs)
            {
                CollectionAssert.AreEqual(pair.Value, ReadAssetBytes(pair.Key), pair.Key);
            }
            AssertNoResidue(draftPath, attackPath, dataFolder, artFolder);
        }

        private static ScriptableObject CreateLegendaryDraftWithoutAttack(string monsterId, string draftPath)
        {
            var source = AssetDatabase.LoadMainAssetAtPath(SourceDraftPath) as ScriptableObject;
            Assert.That(source, Is.Not.Null);
            var draft = ScriptableObject.CreateInstance(source.GetType());
            EditorUtility.CopySerialized(source, draft);
            draft.name = "Draft_" + monsterId;

            var skillCatalog = AssetDatabase.LoadAssetAtPath<MonsterSkillCatalog>(MonsterSkillCatalog.DefaultAssetPath);
            Assert.That(skillCatalog, Is.Not.Null);
            Assert.That(skillCatalog.TryGet("passive_entry_shield", out var passive), Is.True);
            var activeProfile = AssetDatabase.LoadAssetAtPath<MonsterActiveAttackProfile>(
                "Assets/ProjectMT/02_Shared/Unit/Data/ActiveAttackProfiles/AAP_GaleDance.asset");
            Assert.That(activeProfile, Is.Not.Null);
            var projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
            Assert.That(projectilePrefab, Is.Not.Null);

            var serialized = new SerializedObject(draft);
            serialized.FindProperty("monsterId").stringValue = monsterId;
            serialized.FindProperty("displayName").stringValue = "폭풍의 감시자 QA";
            serialized.FindProperty("rarity").enumValueIndex = (int)MonsterRarity.Legendary;
            serialized.FindProperty("productionMemo").stringValue =
                "빈 기본공격 조립, VFX/SFX, Preview, 전설 운영 편입과 같은 GUID 수정 재편입 검증";
            serialized.FindProperty("skillLoadoutConfigured").boolValue = true;
            serialized.FindProperty("rarityPassiveSkill").objectReferenceValue = passive;
            serialized.FindProperty("rarityActiveSkill").objectReferenceValue = null;
            serialized.FindProperty("activeAttackProfile").objectReferenceValue = activeProfile;
            serialized.FindProperty("activeSkillName").stringValue = "폭풍의 심판";
            serialized.FindProperty("activeEnergyMaximum").intValue = 720;
            var passiveTemplate = passive as GenericMonsterPassiveSkill;
            Assert.That(passiveTemplate, Is.Not.Null);
            var initializeTuning = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterPassiveBalanceEditor")
                .GetMethod("EnsureInitialized", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(initializeTuning, Is.Not.Null);
            initializeTuning.Invoke(null, new object[]
            {
                serialized.FindProperty("passiveTuning"),
                passiveTemplate,
                true
            });
            serialized.FindProperty("basicAttackProfile").objectReferenceValue = null;
            serialized.FindProperty("combatType").enumValueIndex = (int)MonsterCombatType.Ranged;
            serialized.FindProperty("projectilePrefab").objectReferenceValue = projectilePrefab;
            serialized.FindProperty("overrideProjectileTuning").boolValue = false;
            serialized.FindProperty("maxHealth").floatValue = 420f;
            serialized.FindProperty("attackPower").floatValue = 44f;
            serialized.FindProperty("defense").floatValue = 21f;
            serialized.FindProperty("attackSpeed").floatValue = 0.9f;
            serialized.FindProperty("moveSpeed").floatValue = 1.9f;
            serialized.FindProperty("attackRange").floatValue = 4.5f;
            serialized.FindProperty("impactStrength").enumValueIndex = (int)MonsterImpactStrength.Heavy;
            serialized.FindProperty("reactionWeight").enumValueIndex = (int)MonsterReactionWeight.Standard;
            serialized.FindProperty("mainBattleRole").enumValueIndex = (int)MainBattleMonsterRole.Marksman;
            serialized.FindProperty("mainBattleTargetPriority").enumValueIndex = (int)UnitTargetPriority.LowestHealth;
            serialized.FindProperty("mainBattlePreferredRangeRatio").floatValue = 0.86f;
            serialized.FindProperty("mainBattleRetreatRangeRatio").floatValue = 0.36f;
            serialized.FindProperty("mainBattleRetargetInterval").floatValue = 0.18f;
            serialized.FindProperty("projectileLaunchRecoilDistance").floatValue = 0.1f;
            serialized.FindProperty("projectileLaunchRecoilDuration").floatValue = 0.1f;
            serialized.FindProperty("ascensionConfigured").boolValue = true;
            ConfigureStatModifier(serialized.FindProperty("ascension1"), "healthRate", 0.12f);
            ConfigureStatModifier(serialized.FindProperty("ascension3"), "attackRate", 0.12f);
            ConfigureStatModifier(serialized.FindProperty("ascension5"), "defenseRate", 0.12f);
            ConfigureAugment(
                serialized.FindProperty("ascension2"),
                monsterId + "_a2",
                "중갑 패시브 강화",
                MonsterSkillAugmentOperation.MagnitudeMultiplier,
                0.18f,
                1);
            ConfigureAugment(
                serialized.FindProperty("ascension4"),
                monsterId + "_a4",
                "회전 강타 추가타",
                MonsterSkillAugmentOperation.RepeatCountBonus,
                0f,
                1);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ConfigureActiveAttackDecisions(draft);

            AssetDatabase.CreateAsset(draft, draftPath);
            AssetDatabase.SaveAssetIfDirty(draft);
            AssetDatabase.ImportAsset(draftPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadMainAssetAtPath(draftPath) as ScriptableObject;
        }

        private static MonsterBasicAttackProfile ComposePersistReopenUpdateAndAssign(
            ScriptableObject draft,
            string attackId,
            string attackPath)
        {
            var windowType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterBasicAttackWorkshopWindow");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var launchVfx = LoadRequired<GameObject>(LaunchVfxPath);
            var projectileVfx = LoadRequired<GameObject>(ProjectileVfxPath);
            var impactVfx = LoadRequired<GameObject>(ImpactVfxPath);
            var launchSfx = LoadRequired<SfxCue>(LaunchSfxPath);
            var impactSfx = LoadRequired<SfxCue>(ImpactSfxPath);

            MonsterBasicAttackProfile saved;
            var firstWindow = ScriptableObject.CreateInstance(windowType);
            try
            {
                windowType.GetField("originDraft", flags).SetValue(firstWindow, draft);
                windowType.GetMethod("StartBlank", flags).Invoke(firstWindow, null);
                Assert.That(windowType.GetField("loadedProfile", flags).GetValue(firstWindow), Is.Null);
                var recipe = windowType.GetField("recipe", flags).GetValue(firstWindow);
                Assert.That(ReadEnumName(recipe, "family"), Is.EqualTo("Melee"),
                    "조립소는 기존 프리셋 복제가 아니라 빈 근거리 단일에서 시작해야 합니다.");

                SetPublicField(recipe, "attackId", attackId);
                SetPublicField(recipe, "displayName", "전설 QA 유도 폭발탄");
                SetPublicField(recipe, "designMemo",
                    "전설 원거리 양산 검증용. Motion Marker에서 발사하고 도착점 원형 범위에 피해를 적용한다.");
                SetEnumField(recipe, "family", "Ranged");
                SetEnumField(recipe, "rangedPattern", "Projectile");
                SetEnumField(recipe, "projectileImpact", "Explosion");
                SetEnumField(recipe, "volley", "Single");
                SetPublicField(recipe, "projectilePath", MonsterBasicAttackProjectileTravel.Homing);
                SetPublicField(recipe, "rangeMultiplier", 1.1f);
                SetPublicField(recipe, "radius", 1.25f);
                SetPublicField(recipe, "maxTargets", 3);
                SetPublicField(recipe, "secondaryDamageRatio", 0.7f);
                SetPublicField(recipe, "projectileSpeed", 10.5f);
                SetPublicField(recipe, "projectileLifetime", 2.4f);
                SetPublicField(recipe, "projectileCollisionRadius", 0.3f);
                SetPublicField(recipe, "hitAreaVisibleDuration", 0.38f);
                SetPublicField(recipe, "launchVfx", launchVfx);
                SetPublicField(recipe, "projectileVfx", projectileVfx);
                SetPublicField(recipe, "impactVfx", impactVfx);
                SetPublicField(recipe, "launchSfx", launchSfx);
                SetPublicField(recipe, "projectileSfx", launchSfx);
                SetPublicField(recipe, "impactSfx", impactSfx);
                SetPublicField(recipe, "launchVfxLifetime", 0.35f);
                SetPublicField(recipe, "projectileVfxLifetime", 2.4f);
                SetPublicField(recipe, "impactVfxLifetime", 0.5f);
                SetPublicField(recipe, "launchVfxPosition", new Vector3(0.08f, 0.12f, 0.18f));
                SetPublicField(recipe, "projectileVfxPosition", new Vector3(0f, 0.06f, 0.14f));
                SetPublicField(recipe, "impactVfxPosition", new Vector3(0f, 0.08f, 0f));
                SetPublicField(recipe, "launchVfxEuler", new Vector3(0f, 12f, 0f));
                SetPublicField(recipe, "projectileVfxEuler", new Vector3(0f, -4f, 0f));
                SetPublicField(recipe, "impactVfxEuler", new Vector3(0f, 18f, 0f));
                SetPublicField(recipe, "launchVfxScale", 0.8f);
                SetPublicField(recipe, "projectileVfxScale", 1.15f);
                SetPublicField(recipe, "impactVfxScale", 1.25f);
                windowType.GetMethod("CompileWorkingProfile", flags).Invoke(firstWindow, null);

                var working = windowType.GetField("workingProfile", flags).GetValue(firstWindow)
                    as MonsterBasicAttackProfile;
                AssertComposedProfile(working, attackId, 10.5f, 1.25f, launchVfx, projectileVfx, impactVfx);
                ExerciseWorkshopPreview(firstWindow, windowType, flags, expectedMoverCount: 1);
                windowType.GetMethod("SaveAsNew", flags).Invoke(firstWindow, null);
                saved = windowType.GetField("loadedProfile", flags).GetValue(firstWindow)
                    as MonsterBasicAttackProfile;
                Assert.That(saved, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(saved), Is.EqualTo(attackPath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstWindow);
            }

            var originalGuid = AssetDatabase.AssetPathToGUID(attackPath);
            Assert.That(originalGuid, Is.Not.Empty);
            var reopened = AssetDatabase.LoadAssetAtPath<MonsterBasicAttackProfile>(attackPath);
            AssertComposedProfile(reopened, attackId, 10.5f, 1.25f, launchVfx, projectileVfx, impactVfx);

            var secondWindow = ScriptableObject.CreateInstance(windowType);
            try
            {
                windowType.GetField("originDraft", flags).SetValue(secondWindow, draft);
                windowType.GetMethod("LoadProfile", flags).Invoke(secondWindow, new object[] { reopened });
                var recipe = windowType.GetField("recipe", flags).GetValue(secondWindow);
                Assert.That(ReadEnumName(recipe, "family"), Is.EqualTo("Ranged"));
                Assert.That(GetPublicField<float>(recipe, "projectileSpeed"), Is.EqualTo(10.5f).Within(0.001f));
                Assert.That(GetPublicField<GameObject>(recipe, "projectileVfx"), Is.SameAs(projectileVfx));
                Assert.That(GetPublicField<SfxCue>(recipe, "impactSfx"), Is.SameAs(impactSfx));

                SetPublicField(recipe, "displayName", "전설 QA 유도 폭발탄 개정");
                SetPublicField(recipe, "designMemo",
                    "재열기 후 같은 프리셋을 수정했다. Motion Marker 발사와 실제 도착 피해의 분리를 검증한다.");
                SetPublicField(recipe, "projectileSpeed", 11.25f);
                SetPublicField(recipe, "impactVfxScale", 1.35f);
                windowType.GetField("workCopyDirty", flags).SetValue(secondWindow, true);
                windowType.GetMethod("UpdateLoaded", flags).Invoke(secondWindow, null);
                Assert.That(AssetDatabase.AssetPathToGUID(attackPath), Is.EqualTo(originalGuid));

                reopened = AssetDatabase.LoadAssetAtPath<MonsterBasicAttackProfile>(attackPath);
                Assert.That(reopened.DisplayName, Is.EqualTo("전설 QA 유도 폭발탄 개정"));
                Assert.That(reopened.ProjectileSpeed, Is.EqualTo(11.25f).Within(0.001f));
                Assert.That(reopened.ImpactFeedback.Scale, Is.EqualTo(1.35f).Within(0.001f));
                windowType.GetMethod("AssignLoadedToOrigin", flags).Invoke(secondWindow, null);
                Assert.That(ReadDraftProfile(draft), Is.SameAs(reopened));
                ExerciseWorkshopPreview(secondWindow, windowType, flags, expectedMoverCount: 1);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(secondWindow);
            }

            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(draft), ImportAssetOptions.ForceSynchronousImport);
            Assert.That(ReadDraftProfile(AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(draft))),
                Is.SameAs(reopened));
            ConfigureBasicAttackDecisions(draft, reopened);
            AssetDatabase.SaveAssetIfDirty(draft);
            return reopened;
        }

        private static void ExerciseWorkshopPreview(
            ScriptableObject window,
            Type windowType,
            BindingFlags flags,
            int expectedMoverCount)
        {
            var movers = windowType.GetField("previewAttackMovers", flags).GetValue(window) as ICollection;
            Assert.That(movers, Is.Not.Null);
            Assert.That(movers.Count, Is.EqualTo(expectedMoverCount));
            windowType.GetMethod("PlayPreviewAttack", flags).Invoke(window, null);
            Assert.That((bool)windowType.GetField("previewUpdateSubscribed", flags).GetValue(window), Is.True);
            windowType.GetField("previewPlaybackStart", flags)
                .SetValue(window, EditorApplication.timeSinceStartup - 100d);
            windowType.GetMethod("TickPreviewPlayback", flags).Invoke(window, null);
            Assert.That((bool)windowType.GetField("previewUpdateSubscribed", flags).GetValue(window), Is.False);
        }

        private static void AssertComposedProfile(
            MonsterBasicAttackProfile profile,
            string attackId,
            float speed,
            float radius,
            GameObject launchVfx,
            GameObject projectileVfx,
            GameObject impactVfx)
        {
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.TryValidate(out var error), Is.True, error);
            Assert.That(profile.AttackId, Is.EqualTo(attackId));
            Assert.That(profile.CombatType, Is.EqualTo(MonsterCombatType.Ranged));
            Assert.That(profile.DeliveryModule, Is.EqualTo(MonsterBasicAttackDeliveryModule.Projectile));
            Assert.That(profile.CollisionModule, Is.EqualTo(MonsterBasicAttackCollisionModule.AreaImpact));
            Assert.That(profile.Shape, Is.EqualTo(MonsterBasicAttackShape.Circle));
            Assert.That(profile.ProjectileTravel, Is.EqualTo(MonsterBasicAttackProjectileTravel.Homing));
            Assert.That(profile.ProjectileSpeed, Is.EqualTo(speed).Within(0.001f));
            Assert.That(profile.Radius, Is.EqualTo(radius).Within(0.001f));
            Assert.That(profile.MaxTargets, Is.EqualTo(3));
            Assert.That(profile.LaunchFeedback.VfxPrefab, Is.SameAs(launchVfx));
            Assert.That(profile.ProjectileFeedback.VfxPrefab, Is.SameAs(projectileVfx));
            Assert.That(profile.ImpactFeedback.VfxPrefab, Is.SameAs(impactVfx));
            Assert.That(profile.LaunchFeedback.LocalPosition, Is.EqualTo(new Vector3(0.08f, 0.12f, 0.18f)));
            Assert.That(profile.ProjectileFeedback.Scale, Is.EqualTo(1.15f).Within(0.001f));
        }

        private static void AssertMakerValidationPasses(ScriptableObject draft)
        {
            var validatorType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerValidator");
            var report = validatorType.GetMethod("Validate", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new object[] { draft });
            Assert.That(report, Is.Not.Null);
            var hasErrors = (bool)report.GetType().GetProperty("HasErrors").GetValue(report);
            if (!hasErrors)
            {
                return;
            }

            var issues = (IEnumerable)report.GetType().GetProperty("Issues").GetValue(report);
            var messages = issues.Cast<object>()
                .Select(issue => issue.GetType().GetProperty("Message")?.GetValue(issue) as string)
                .Where(message => !string.IsNullOrWhiteSpace(message));
            Assert.Fail(string.Join("\n", messages));
        }

        private static EditorWindow OpenMakerAndExercisePreview(ScriptableObject draft)
        {
            var windowType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerWindow");
            var window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            Assert.That(window, Is.Not.Null);
            window.position = new Rect(60f, 60f, 1600f, 900f);
            try
            {
                windowType.GetMethod("SetDraft", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(window, new object[] { draft, false });
                window.ShowUtility();
                window.SendEvent(new Event { type = EventType.Layout });
                window.SendEvent(new Event { type = EventType.Repaint });

                var preview = windowType.GetField("preview", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(window);
                Assert.That(preview, Is.Not.Null);
                var previewType = preview.GetType();
                Assert.That((bool)previewType.GetProperty("HasCombatTarget").GetValue(preview), Is.True);
                Assert.That((bool)previewType.GetMethod("ShowBasicAttackArea", BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(preview, null), Is.True);
                Assert.That((int)previewType.GetProperty("ActiveHitAreaCount").GetValue(preview), Is.GreaterThan(0));
                previewType.GetMethod("PlayAttack", BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(preview, new object[] { 0 });
                var tick = previewType.GetMethod(
                    "Tick",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(float) },
                    null);
                for (var index = 0; index < 80; index++)
                {
                    tick.Invoke(preview, new object[] { 0.08f });
                }
                Assert.That((int)previewType.GetProperty("PreviewHitCount").GetValue(preview), Is.GreaterThanOrEqualTo(1));
                Assert.That((float)previewType.GetProperty("LastAppliedDamage").GetValue(preview), Is.GreaterThan(0f));
                Assert.That((bool)previewType.GetProperty("RequiresContinuousTick").GetValue(preview), Is.False);
                return window;
            }
            catch
            {
                MonsterEditorWindowTestUtility.Close(window);
                throw;
            }
        }

        private static object InvokeWriter(ScriptableObject draft)
        {
            var writerType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerAssetWriter");
            var writer = writerType.GetMethod("BuildAndRegister", BindingFlags.Public | BindingFlags.Static);
            Assert.That(writer, Is.Not.Null);
            try
            {
                return writer.Invoke(null, new object[] { draft, null, null });
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw exception.InnerException;
            }
        }

        private static string[] VerifyGeneratedLegendary(
            object writeResult,
            MonsterBasicAttackProfile basicAttack,
            string monsterId,
            string expectedName,
            bool expectedUpdated)
        {
            Assert.That(writeResult, Is.Not.Null);
            var resultType = writeResult.GetType();
            Assert.That((bool)resultType.GetProperty("UpdatedExisting").GetValue(writeResult),
                Is.EqualTo(expectedUpdated));
            var outputPaths = ((IEnumerable)resultType.GetProperty("OutputPaths").GetValue(writeResult))
                .Cast<string>()
                .ToArray();
            Assert.That(outputPaths, Has.Length.EqualTo(11));
            Assert.That(outputPaths, Has.All.Matches<string>(path =>
                !string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(path))));

            var definition = resultType.GetProperty("Definition").GetValue(writeResult) as MonsterDefinition;
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.MonsterId, Is.EqualTo(monsterId));
            Assert.That(definition.DisplayName, Is.EqualTo(expectedName));
            Assert.That(MonsterDefinitionValidator.Validate(definition, true).HasErrors, Is.False);

            var combat = AssetDatabase.LoadAssetAtPath<MonsterCombatProfile>(outputPaths[3]);
            Assert.That(combat.Action.BasicAttackProfile, Is.SameAs(basicAttack));
            Assert.That(combat.Action, Is.TypeOf<ProjectileActionDefinition>());
            var projectileAction = (ProjectileActionDefinition)combat.Action;
            Assert.That(projectileAction.Speed, Is.EqualTo(11.25f).Within(0.001f));
            Assert.That(projectileAction.DeliveryMode, Is.EqualTo(MonsterRangedDeliveryMode.Projectile));
            Assert.That(combat.TryValidate(out var combatError), Is.True, combatError);

            var ascension = AssetDatabase.LoadAssetAtPath<MonsterAscensionProfile>(outputPaths[4]);
            Assert.That(ascension.TryValidate(out var ascensionError), Is.True, ascensionError);
            var augments = AssetDatabase.LoadAllAssetsAtPath(outputPaths[4]).OfType<MonsterAbilityDefinition>().ToArray();
            Assert.That(augments, Has.Length.EqualTo(2));
            Assert.That(augments.Any(ability => ability.AugmentTarget == MonsterSkillAugmentTarget.Passive), Is.True);
            Assert.That(augments.Any(ability => ability.AugmentTarget == MonsterSkillAugmentTarget.Active), Is.True);

            var runtime = AssetDatabase.LoadAssetAtPath<MonsterRuntimeAssetSet>(outputPaths[6]);
            Assert.That(runtime.TryValidate(out var runtimeError), Is.True, runtimeError);
            var uniquePassive = AssetDatabase.LoadAssetAtPath<GenericMonsterPassiveSkill>(outputPaths[9]);
            Assert.That(uniquePassive, Is.Not.Null);
            Assert.That(uniquePassive.SkillId, Is.EqualTo($"passive_entry_shield_{monsterId}"));
            Assert.That(AssetDatabase.LoadAssetAtPath<MonsterAttackActiveSkill>(outputPaths[10]), Is.Not.Null);

            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(MonsterCatalogPath);
            var rarityCatalog = AssetDatabase.LoadAssetAtPath<MonsterRarityCatalog>(RarityCatalogPath);
            Assert.That(catalog.TryGet(monsterId, out var registered), Is.True);
            Assert.That(registered, Is.SameAs(definition));
            Assert.That(rarityCatalog.TryGetRarity(monsterId, out var rarity), Is.True);
            Assert.That(rarity, Is.EqualTo(MonsterRarity.Legendary));
            var rarityEntry = rarityCatalog.LegendaryMythicEntries.Single(entry => entry.Monster == definition);
            Assert.That(rarityEntry.PassiveSkill, Is.SameAs(uniquePassive));
            var attackActive = rarityEntry.ActiveSkill as MonsterAttackActiveSkill;
            Assert.That(attackActive, Is.Not.Null);
            Assert.That(attackActive.SkillId, Is.EqualTo("active_" + monsterId));
            Assert.That(attackActive.DisplayName, Is.EqualTo("폭풍의 심판"));
            Assert.That(attackActive.SourceProfile.ProfileId, Is.EqualTo("gale_dance"));
            Assert.That(rarityEntry.TryValidateSkillReferences(out var skillError), Is.True, skillError);

            var adapter = AssetDatabase.LoadAssetAtPath<GameObject>(outputPaths[8]);
            Assert.That(adapter.transform.Find("Visual"), Is.Not.Null);
            Assert.That(adapter.transform.Find("AttackOrigin"), Is.Not.Null);
            Assert.That(adapter.transform.Find("HitCenter"), Is.Not.Null);
            Assert.That(adapter.GetComponentInChildren<Animator>(true), Is.Not.Null);
            return outputPaths;
        }

        private static MonsterBasicAttackProfile ReadDraftProfile(UnityEngine.Object draft)
        {
            return draft?.GetType().GetProperty("BasicAttackProfile")?.GetValue(draft)
                as MonsterBasicAttackProfile;
        }

        private static string ReadEnumName(object target, string fieldName)
        {
            return target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(target)?.ToString();
        }

        private static void SetEnumField(object target, string fieldName, string enumName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, Enum.Parse(field.FieldType, enumName));
        }

        private static void SetPublicField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static T GetPublicField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }

        private static T LoadRequired<T>(string path) where T : UnityEngine.Object
        {
            var result = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(result, Is.Not.Null, path);
            return result;
        }

        private static void ConfigureActiveAttackDecisions(ScriptableObject draft)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            draft.GetType().GetMethod("EditorSyncActiveAttackAuthoring", flags).Invoke(draft, null);
            var serialized = new SerializedObject(draft);
            var presentations = serialized.FindProperty("activeAttackPresentations");
            for (var presentationIndex = 0; presentationIndex < presentations.arraySize; presentationIndex++)
            {
                var slots = presentations.GetArrayElementAtIndex(presentationIndex).FindPropertyRelative("slots");
                for (var slotIndex = 0; slotIndex < slots.arraySize; slotIndex++)
                {
                    var slot = slots.GetArrayElementAtIndex(slotIndex);
                    slot.FindPropertyRelative("assignmentStateConfigured").boolValue = true;
                    slot.FindPropertyRelative("vfxState").enumValueIndex =
                        (int)MonsterBasicAttackVfxAssignmentState.Disabled;
                    slot.FindPropertyRelative("sfxState").enumValueIndex =
                        (int)MonsterBasicAttackSfxAssignmentState.Disabled;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBasicAttackDecisions(
            ScriptableObject draft,
            MonsterBasicAttackProfile profile)
        {
            var serialized = new SerializedObject(draft);
            var bindings = serialized.FindProperty("basicAttackVfxBindings");
            bindings.arraySize = 0;
            var attacks = serialized.FindProperty("attacks");
            var motionIds = Enumerable.Range(0, attacks.arraySize)
                .Select(index => attacks.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("motionId").stringValue?.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (motionIds.Length == 0) motionIds = new[] { "attack01" };

            foreach (var contract in profile.VfxSlots.Where(slot => slot != null))
            {
                var resolvedMotionIds = contract.AssignmentScope == MonsterBasicAttackVfxAssignmentScope.MonsterShared
                    ? new[] { string.Empty }
                    : motionIds;
                foreach (var motionId in resolvedMotionIds)
                {
                    var cue = contract.IsDeliveryVisual
                        ? profile.ProjectileFeedback
                        : contract.EventType == MonsterBasicAttackVfxEvent.TargetDamaged ||
                          contract.EventType == MonsterBasicAttackVfxEvent.AreaResolved
                            ? profile.ImpactFeedback
                            : profile.LaunchFeedback;
                    var index = bindings.arraySize;
                    bindings.InsertArrayElementAtIndex(index);
                    var binding = bindings.GetArrayElementAtIndex(index);
                    binding.FindPropertyRelative("attackId").stringValue = profile.AttackId;
                    binding.FindPropertyRelative("slotId").stringValue = contract.SlotId;
                    binding.FindPropertyRelative("motionId").stringValue = motionId;
                    binding.FindPropertyRelative("state").enumValueIndex = cue?.VfxPrefab != null
                        ? (int)MonsterBasicAttackVfxAssignmentState.Assigned
                        : (int)MonsterBasicAttackVfxAssignmentState.Disabled;
                    binding.FindPropertyRelative("prefab").objectReferenceValue = cue?.VfxPrefab;
                    binding.FindPropertyRelative("sfxState").enumValueIndex = cue?.Sfx != null
                        ? (int)MonsterBasicAttackSfxAssignmentState.Assigned
                        : (int)MonsterBasicAttackSfxAssignmentState.Disabled;
                    binding.FindPropertyRelative("sound").objectReferenceValue = cue?.Sfx?.PrimaryClip;
                    binding.FindPropertyRelative("soundVolume").floatValue = 1f;
                    binding.FindPropertyRelative("sfx").objectReferenceValue = null;
                    binding.FindPropertyRelative("lifetime").floatValue = cue?.VfxLifetime ?? contract.DefaultLifetime;
                    binding.FindPropertyRelative("playbackOffset").floatValue = 0f;
                    binding.FindPropertyRelative("playbackSpeed").floatValue = 1f;
                    binding.FindPropertyRelative("eventTimingOffset").floatValue = 0f;
                    binding.FindPropertyRelative("localPosition").vector3Value = cue?.LocalPosition ?? Vector3.zero;
                    binding.FindPropertyRelative("localEulerAngles").vector3Value =
                        cue?.LocalRotation.eulerAngles ?? Vector3.zero;
                    binding.FindPropertyRelative("scale").floatValue = cue?.Scale ?? 1f;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureStatModifier(SerializedProperty modifier, string fieldName, float value)
        {
            modifier.FindPropertyRelative(fieldName).floatValue = value;
        }

        private static void ConfigureAugment(
            SerializedProperty augment,
            string abilityId,
            string displayName,
            MonsterSkillAugmentOperation operation,
            float scalar,
            int integer)
        {
            augment.FindPropertyRelative("abilityId").stringValue = abilityId;
            augment.FindPropertyRelative("displayName").stringValue = displayName;
            augment.FindPropertyRelative("augmentOperation").enumValueIndex = (int)operation;
            augment.FindPropertyRelative("augmentScalarValue").floatValue = scalar;
            augment.FindPropertyRelative("augmentIntegerValue").intValue = integer;
        }

        private static Type FindEditorType(string fullName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }

        private static byte[] ReadAssetBytes(string assetPath)
        {
            var fullPath = ToFullPath(assetPath);
            Assert.That(File.Exists(fullPath), Is.True, assetPath);
            return File.ReadAllBytes(fullPath);
        }

        private static void RestoreCatalogs(IReadOnlyDictionary<string, byte[]> snapshots)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var pair in snapshots)
            {
                File.WriteAllBytes(ToFullPath(pair.Key), pair.Value);
            }
            foreach (var assetPath in snapshots.Keys)
            {
                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void DeleteIfPresent(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath) || AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
            {
                Assert.That(AssetDatabase.DeleteAsset(assetPath), Is.True, assetPath);
            }
        }

        private static void AssertNoResidue(
            string draftPath,
            string attackPath,
            string dataFolder,
            string artFolder)
        {
            Assert.That(AssetDatabase.LoadMainAssetAtPath(draftPath), Is.Null, draftPath);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(attackPath), Is.Null, attackPath);
            Assert.That(AssetDatabase.IsValidFolder(dataFolder), Is.False, dataFolder);
            Assert.That(AssetDatabase.IsValidFolder(artFolder), Is.False, artFolder);
        }

        private static string ToFullPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.That(projectRoot, Is.Not.Null.And.Not.Empty);
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
