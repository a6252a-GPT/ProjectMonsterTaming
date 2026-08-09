using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Tests.EditMode
{
    public sealed class MonsterMakerSafetyContractTests // Maker 원자성·런타임 동일 Preview 구조 회귀 검사
    {
        private const string SpikeDraftPath =
            "Assets/ProjectMT/Editor/MonsterMaker/Drafts/Draft_spike_01.asset";
        private const string ProductionCatalogPath =
            "Assets/ProjectMT/02_Shared/Unit/Data/MonsterCatalog.asset";
        private const string DataRoot = "Assets/ProjectMT/02_Shared/Unit/Data/Monsters";
        private const string ArtRoot = "Assets/ProjectMT/05_Art/Monsters";

        [Test]
        public void BuildAndRegister_PreservesGuidsAndRollsBackEveryTouchedAssetOnFailure()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var monsterId = "maker_tx_" + suffix;
            var failedNewId = "maker_fail_" + suffix;
            var tempRoot = "Assets/ProjectMT/99_Tests/_MonsterMakerTemp_" + suffix;
            var dataFolder = DataRoot + "/" + monsterId;
            var artFolder = ArtRoot + "/" + monsterId;
            var failedDataFolder = DataRoot + "/" + failedNewId;
            var failedArtFolder = ArtRoot + "/" + failedNewId;
            ScriptableObject draft = null;

            Assert.That(AssetDatabase.IsValidFolder(dataFolder), Is.False, "임시 Monster ID가 기존 데이터와 충돌합니다.");
            Assert.That(AssetDatabase.IsValidFolder(artFolder), Is.False, "임시 Monster ID가 기존 아트와 충돌합니다.");
            AssetDatabase.CreateFolder("Assets/ProjectMT/99_Tests", "_MonsterMakerTemp_" + suffix);
            try
            {
                var sourceDraft = AssetDatabase.LoadMainAssetAtPath(SpikeDraftPath) as ScriptableObject;
                Assert.That(sourceDraft, Is.Not.Null);
                draft = ScriptableObject.CreateInstance(sourceDraft.GetType());
                EditorUtility.CopySerialized(sourceDraft, draft);
                ConfigureDraftIdentity(draft, monsterId);
                var draftPath = tempRoot + "/Draft.asset";
                AssetDatabase.CreateAsset(draft, draftPath);

                var catalog = ScriptableObject.CreateInstance<MonsterCatalog>();
                catalog.EditorSetDefinitions(Array.Empty<MonsterDefinition>());
                var catalogPath = tempRoot + "/MonsterCatalog.asset";
                AssetDatabase.CreateAsset(catalog, catalogPath);

                var rarityCatalog = ScriptableObject.CreateInstance<MonsterRarityCatalog>();
                var rarityCatalogPath = tempRoot + "/MonsterRarityCatalog.asset";
                AssetDatabase.CreateAsset(rarityCatalog, rarityCatalogPath);
                var raritySerialized = new SerializedObject(rarityCatalog);
                raritySerialized.FindProperty("sourceCatalog").objectReferenceValue = catalog;
                raritySerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rarityCatalog);
                AssetDatabase.SaveAssets();

                InvokeWriter(draft, catalog, rarityCatalog);
                var outputPaths = BuildOutputPaths(monsterId);
                var firstGuids = outputPaths.ToDictionary(path => path, AssetDatabase.AssetPathToGUID);
                Assert.That(firstGuids.Values, Has.All.Not.Empty);

                InvokeWriter(draft, catalog, rarityCatalog);
                foreach (var path in outputPaths)
                {
                    Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(firstGuids[path]), path);
                }

                Assert.That(
                    catalog.Definitions.Count(candidate => candidate != null && candidate.MonsterId == monsterId),
                    Is.EqualTo(1));
                Assert.That(catalog.TryValidate(out var validCatalogError), Is.True, validCatalogError);
                Assert.That(rarityCatalog.TryValidate(out var validRarityError), Is.True, validRarityError);

                var generated = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(outputPaths[0]);
                var expectedAttackPower = generated.AttackPower;
                var touchedPaths = outputPaths.Concat(new[] { catalogPath, rarityCatalogPath }).ToArray();

                AddInvalidRarityEntry(rarityCatalog);
                SetFloat(draft, "attackPower", expectedAttackPower + 11f);
                AssetDatabase.SaveAssets();
                var beforeRarityFailure = CaptureStates(touchedPaths);
                var rarityFailure = Assert.Throws<InvalidOperationException>(
                    () => InvokeWriter(draft, catalog, rarityCatalog));
                StringAssert.Contains("missing", rarityFailure.Message.ToLowerInvariant());
                AssertStatesEqual(beforeRarityFailure);
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<MonsterDefinition>(outputPaths[0]).AttackPower,
                    Is.EqualTo(expectedAttackPower).Within(0.0001f));
                RemoveInvalidRarityEntries(rarityCatalog);
                Assert.That(rarityCatalog.TryValidate(out var repairedRarityError), Is.True, repairedRarityError);

                generated = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(outputPaths[0]);
                var productionCatalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(ProductionCatalogPath);
                var unrelated = productionCatalog.Definitions.First(candidate => candidate != null);
                catalog.EditorSetDefinitions(new[] { unrelated, unrelated, generated });
                EditorUtility.SetDirty(catalog);
                SetFloat(draft, "attackPower", generated.AttackPower + 17f);
                AssetDatabase.SaveAssets();

                var beforeFailure = CaptureStates(touchedPaths);
                var failure = Assert.Throws<InvalidOperationException>(() => InvokeWriter(draft, catalog, rarityCatalog));
                StringAssert.Contains("duplicated", failure.Message.ToLowerInvariant());
                AssertStatesEqual(beforeFailure);
                var restored = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(outputPaths[0]);
                Assert.That(restored.AttackPower, Is.EqualTo(expectedAttackPower).Within(0.0001f));

                ConfigureDraftIdentity(draft, failedNewId);
                EditorUtility.SetDirty(draft);
                AssetDatabase.SaveAssets();
                var beforeNewFailure = CaptureStates(new[] { catalogPath, rarityCatalogPath });
                Assert.Throws<InvalidOperationException>(() => InvokeWriter(draft, catalog, rarityCatalog));
                Assert.That(AssetDatabase.IsValidFolder(failedDataFolder), Is.False);
                Assert.That(AssetDatabase.IsValidFolder(failedArtFolder), Is.False);
                AssertStatesEqual(beforeNewFailure);
            }
            finally
            {
                DeleteAssetIfPresent(failedArtFolder);
                DeleteAssetIfPresent(failedDataFolder);
                DeleteAssetIfPresent(artFolder);
                DeleteAssetIfPresent(dataFolder);
                DeleteAssetIfPresent(tempRoot);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        [Test]
        public void Preview_UsesAdapterSocketsAndAppliesPreviewScaleToFraming()
        {
            var sourceDraft = AssetDatabase.LoadMainAssetAtPath(SpikeDraftPath) as ScriptableObject;
            Assert.That(sourceDraft, Is.Not.Null);
            var draft = ScriptableObject.CreateInstance(sourceDraft.GetType());
            EditorUtility.CopySerialized(sourceDraft, draft);
            const float expectedFramingScale = 1.7f;
            SetFloat(draft, "previewScale", expectedFramingScale);

            var previewType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerPreviewStage");
            var preview = Activator.CreateInstance(previewType);
            try
            {
                previewType.GetMethod("SetDraft", BindingFlags.Instance | BindingFlags.Public)
                    ?.Invoke(preview, new object[] { draft });
                var sharedStage = previewType.GetField("stage", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(preview);
                Assert.That(sharedStage, Is.Not.Null);
                var root = sharedStage.GetType().GetProperty("PreviewRoot", BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(sharedStage) as GameObject;
                Assert.That(root, Is.Not.Null);
                Assert.That(root.transform.Find("Visual"), Is.Not.Null);

                var serialized = new SerializedObject(draft);
                var attackOrigin = root.transform.Find(serialized.FindProperty("attackOriginPath").stringValue);
                var hitCenter = root.transform.Find(serialized.FindProperty("hitCenterPath").stringValue);
                Assert.That(attackOrigin, Is.Not.Null);
                Assert.That(hitCenter, Is.Not.Null);
                Assert.That(
                    attackOrigin.localPosition,
                    Is.EqualTo(serialized.FindProperty("attackOriginLocalPosition").vector3Value));
                Assert.That(
                    hitCenter.localPosition,
                    Is.EqualTo(serialized.FindProperty("hitCenterLocalPosition").vector3Value));

                var framingScale = (float)sharedStage.GetType()
                    .GetField("framingScale", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(sharedStage);
                Assert.That(framingScale, Is.EqualTo(expectedFramingScale).Within(0.0001f));

                var defaultSocket = previewType
                    .GetMethod("ResolvePreviewSocket", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(preview, new object[] { null }) as Transform;
                Assert.That(defaultSocket, Is.SameAs(attackOrigin));
                var invalidSocket = previewType
                    .GetMethod("ResolvePreviewSocket", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(preview, new object[] { "Visual/SocketThatDoesNotExist" }) as Transform;
                Assert.That(invalidSocket, Is.SameAs(attackOrigin));
            }
            finally
            {
                (preview as IDisposable)?.Dispose();
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void Preview_RightDragFollowsPointerDirection()
        {
            var stageType = FindEditorType("PrefabPreviewStage");
            var stage = Activator.CreateInstance(stageType);
            try
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var yawField = stageType.GetField("cameraYaw", flags);
                var pitchField = stageType.GetField("cameraPitch", flags);
                Assert.That(yawField, Is.Not.Null);
                Assert.That(pitchField, Is.Not.Null);
                yawField.SetValue(stage, 0f);
                pitchField.SetValue(stage, 0f);

                var drag = new Event
                {
                    type = EventType.MouseDrag,
                    button = 1,
                    mousePosition = new Vector2(50f, 50f),
                    delta = new Vector2(20f, 10f)
                };
                stageType.GetMethod("HandleInput", BindingFlags.Instance | BindingFlags.Public)
                    ?.Invoke(stage, new object[] { new Rect(0f, 0f, 100f, 100f), drag });

                Assert.That((float)yawField.GetValue(stage), Is.LessThan(0f), "오른쪽 Drag는 VFX Preview와 같은 Yaw 방향이어야 합니다.");
                Assert.That((float)pitchField.GetValue(stage), Is.GreaterThan(0f), "아래쪽 Drag는 VFX Preview와 같은 Pitch 방향이어야 합니다.");
            }
            finally
            {
                (stage as IDisposable)?.Dispose();
            }
        }

        [Test]
        public void Validation_TreatsEmptySoundAndVfxAsOptional()
        {
            var sourceDraft = AssetDatabase.LoadMainAssetAtPath(SpikeDraftPath) as ScriptableObject;
            Assert.That(sourceDraft, Is.Not.Null);

            var validatorType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerValidator");
            var makerReport = validatorType
                .GetMethod("Validate", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new object[] { sourceDraft });
            Assert.That(makerReport, Is.Not.Null);
            var makerIssues = makerReport.GetType().GetProperty("Issues")?.GetValue(makerReport) as System.Collections.IEnumerable;
            Assert.That(makerIssues, Is.Not.Null);
            var makerCodes = makerIssues.Cast<object>()
                .Select(issue => issue.GetType().GetProperty("Code")?.GetValue(issue) as string)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToArray();
            Assert.That(makerCodes.Any(code => code.StartsWith("MAKER-FX", StringComparison.Ordinal)), Is.False);

            var definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(
                "Assets/ProjectMT/02_Shared/Unit/Data/Monsters/spike_01/MD_spike_01.asset");
            Assert.That(definition, Is.Not.Null);
            var runtimeReport = MonsterDefinitionValidator.Validate(definition, true);
            Assert.That(
                runtimeReport.Issues.Any(issue => issue.Code.StartsWith("MON-FX", StringComparison.Ordinal)),
                Is.False);
        }

        [Test]
        public void Preview_SoundUsesSfxCueAndHasNoStandalonePlayback()
        {
            var feedbackType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerFeedbackDraft");
            Assert.That(
                feedbackType.GetProperty("Sfx", BindingFlags.Instance | BindingFlags.Public)?.PropertyType.FullName,
                Is.EqualTo("ProjectMT.Shared.Audio.SfxCue"));

            var previewType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerPreviewStage");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            Assert.That(previewType.GetMethod("PlayHitFeedback", flags), Is.Null);
            Assert.That(previewType.GetMethod("PlaySpecialFeedback", flags), Is.Null);
        }

        [Test]
        public void Preview_AttackSpeedAndDraftResetMatchTheRuntimeContract()
        {
            var sourceDraft = AssetDatabase.LoadMainAssetAtPath(SpikeDraftPath) as ScriptableObject;
            Assert.That(sourceDraft, Is.Not.Null);
            var draft = ScriptableObject.CreateInstance(sourceDraft.GetType());
            EditorUtility.CopySerialized(sourceDraft, draft);
            SetFloat(draft, "attackSpeed", 10f);

            var serialized = new SerializedObject(draft);
            var attack = serialized.FindProperty("attacks").GetArrayElementAtIndex(0);
            var clip = attack.FindPropertyRelative("clip").objectReferenceValue as AnimationClip;
            var authoredSpeed = attack.FindPropertyRelative("playbackSpeed").floatValue;
            var expectedSpeed = MonsterAnimationDriver.ResolveAttackPlaybackSpeed(clip, authoredSpeed, 0.1f);
            var previewType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerPreviewStage");
            var preview = Activator.CreateInstance(previewType);
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                previewType.GetMethod("SetDraft", flags)?.Invoke(preview, new object[] { draft });
                previewType.GetMethod("PlayAttack", flags)?.Invoke(preview, new object[] { 0 });
                var playbackSpeed = (float)previewType.GetField("playbackSpeed", flags).GetValue(preview);
                Assert.That(playbackSpeed, Is.EqualTo(expectedSpeed).Within(0.0001f));

                previewType.GetField("lastRandomAttackIndex", flags)?.SetValue(preview, 7);
                previewType.GetMethod("SetDraft", flags)?.Invoke(preview, new object[] { draft });
                Assert.That((int)previewType.GetField("lastRandomAttackIndex", flags).GetValue(preview), Is.EqualTo(-1));

                var tickVfx = previewType.GetMethod("TickVfx", flags);
                Assert.That(tickVfx, Is.Not.Null);
                Assert.That(tickVfx.GetParameters().Select(parameter => parameter.ParameterType),
                    Is.EqualTo(new[] { typeof(float) }));
            }
            finally
            {
                (preview as IDisposable)?.Dispose();
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void Validation_RejectsNonAsciiIdAndModeSpecificProjectileValues()
        {
            var sourceDraft = AssetDatabase.LoadMainAssetAtPath(SpikeDraftPath) as ScriptableObject;
            Assert.That(sourceDraft, Is.Not.Null);
            var draft = ScriptableObject.CreateInstance(sourceDraft.GetType());
            var projectile = new GameObject("ProjectileValidationProbe");
            projectile.AddComponent<MonsterProjectileActor>();
            try
            {
                EditorUtility.CopySerialized(sourceDraft, draft);
                var serialized = new SerializedObject(draft);
                serialized.FindProperty("monsterId").stringValue = "몬스터_01";
                serialized.FindProperty("combatType").enumValueIndex = (int)MonsterCombatType.Ranged;
                serialized.FindProperty("projectilePrefab").objectReferenceValue = projectile;
                serialized.FindProperty("projectileMode").enumValueIndex = (int)MonsterProjectileAttackMode.Piercing;
                serialized.FindProperty("projectileHitRadius").floatValue = 0f;
                serialized.FindProperty("projectileMaxPiercingTargets").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var codes = GetMakerIssueCodes(draft);
                Assert.That(codes, Does.Contain("MAKER-ID-CHAR"));
                Assert.That(codes, Does.Contain("MAKER-PROJECTILE-PIERCING"));

                serialized.Update();
                serialized.FindProperty("projectileMode").enumValueIndex = (int)MonsterProjectileAttackMode.Area;
                serialized.FindProperty("projectileImpactRadius").floatValue = 0f;
                serialized.FindProperty("projectileMaxImpactTargets").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(GetMakerIssueCodes(draft), Does.Contain("MAKER-PROJECTILE-AREA"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(projectile);
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void Validation_RejectsAnewDraftThatReusesAnExistingGameMonsterId()
        {
            var sourceDraft = AssetDatabase.LoadMainAssetAtPath(SpikeDraftPath) as ScriptableObject;
            var draft = ScriptableObject.CreateInstance(sourceDraft.GetType());
            try
            {
                EditorUtility.CopySerialized(sourceDraft, draft);
                Assert.That(GetMakerIssueCodes(draft), Does.Contain("MAKER-ID-CATALOG"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void WindowDraftOwnership_DetectsIdPathAndExternalFingerprintChanges()
        {
            var previousSelection = Selection.activeObject;
            Selection.activeObject = null;
            var windowType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerWindow");
            var window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            Assert.That(window, Is.Not.Null);
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var sourceDraft = AssetDatabase.LoadMainAssetAtPath(SpikeDraftPath) as ScriptableObject;
                windowType.GetMethod("SetDraft", flags)?.Invoke(window, new object[] { sourceDraft, false });
                var validate = windowType.GetMethod("ValidatePersistentDraftOwnership", flags);
                Assert.That(validate, Is.Not.Null);

                var arguments = new object[] { null };
                Assert.That((bool)validate.Invoke(window, arguments), Is.True, arguments[0] as string);

                var loadedId = windowType.GetField("loadedDraftMonsterId", flags);
                var actualId = (string)sourceDraft.GetType().GetProperty("MonsterId")?.GetValue(sourceDraft);
                loadedId?.SetValue(window, "another_id");
                arguments[0] = null;
                Assert.That((bool)validate.Invoke(window, arguments), Is.False);
                StringAssert.Contains("ID", arguments[0] as string);

                loadedId?.SetValue(window, actualId);
                windowType.GetField("loadedDraftFingerprint", flags)?.SetValue(window, "STALE_FINGERPRINT");
                arguments[0] = null;
                Assert.That((bool)validate.Invoke(window, arguments), Is.False);
                StringAssert.Contains("창 밖", arguments[0] as string);

                windowType.GetMethod("CapturePersistentDraftIdentity", flags)?.Invoke(window, null);
                windowType.GetField("loadedDraftAssetPath", flags)?.SetValue(window, "Assets/Elsewhere/Draft.asset");
                arguments[0] = null;
                Assert.That((bool)validate.Invoke(window, arguments), Is.False);
                StringAssert.Contains("경로", arguments[0] as string);
            }
            finally
            {
                Selection.activeObject = previousSelection;
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void Writer_ProductionCatalogRejectsTransientDraftBeforeAnyAssetWrite()
        {
            var sourceDraft = AssetDatabase.LoadMainAssetAtPath(SpikeDraftPath) as ScriptableObject;
            var draft = ScriptableObject.CreateInstance(sourceDraft.GetType());
            EditorUtility.CopySerialized(sourceDraft, draft);
            try
            {
                var writerType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerAssetWriter");
                var guard = writerType.GetMethod(
                    "ValidateProductionDraftOwnership",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(guard, Is.Not.Null);
                var exception = Assert.Throws<TargetInvocationException>(() => guard.Invoke(
                    null,
                    new object[]
                    {
                        draft,
                        ProductionCatalogPath,
                        "Assets/ProjectMT/02_Shared/Unit/Data/MonsterRarityCatalog.asset"
                    }));
                Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
                StringAssert.Contains("먼저 저장", exception.InnerException.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void WindowCatalog_ReflectsTheProductionCatalogAndOnlyEnablesSavedMakerDrafts()
        {
            var windowType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerWindow");
            var window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            Assert.That(window, Is.Not.Null);
            try
            {
                const BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
                windowType.GetMethod("ReloadCatalogEntries", instanceFlags)?.Invoke(window, null);
                var listed = windowType.GetField("catalogDefinitions", instanceFlags)?.GetValue(window)
                    as MonsterDefinition[];
                var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(ProductionCatalogPath);
                Assert.That(listed, Is.Not.Null);
                Assert.That(catalog, Is.Not.Null);
                Assert.That(listed.Length, Is.EqualTo(catalog.Definitions.Count(candidate => candidate != null)));

                var shell = listed.Single(definition => definition.MonsterId == "shell_01");
                var tofu = listed.Single(definition => definition.MonsterId == "tofu_01");
                var loadDraft = windowType.GetMethod(
                    "LoadDraftForDefinition",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(loadDraft, Is.Not.Null);
                Assert.That(loadDraft.Invoke(null, new object[] { shell }), Is.Not.Null);
                Assert.That(loadDraft.Invoke(null, new object[] { tofu }), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WindowCatalog_SelectingAFormalMonsterOpensItsPersistentDraftForEditing()
        {
            var previousSelection = Selection.activeObject;
            var windowType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerWindow");
            var window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            Assert.That(window, Is.Not.Null);
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var shell = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(
                    "Assets/ProjectMT/02_Shared/Unit/Data/Monsters/shell_01/MD_shell_01.asset");
                var expectedDraft = AssetDatabase.LoadMainAssetAtPath(
                    "Assets/ProjectMT/Editor/MonsterMaker/Drafts/Draft_shell_01.asset");
                Assert.That(shell, Is.Not.Null);
                Assert.That(expectedDraft, Is.Not.Null);

                var opened = (bool)windowType.GetMethod("TryOpenDefinition", flags)
                    .Invoke(window, new object[] { shell, false });
                var activeDraft = windowType.GetField("draft", flags)?.GetValue(window);
                var ownsTransient = (bool)windowType.GetField("ownsTransientDraft", flags).GetValue(window);
                Assert.That(opened, Is.True);
                Assert.That(activeDraft, Is.SameAs(expectedDraft));
                Assert.That(ownsTransient, Is.False);
                Assert.That(Selection.activeObject, Is.SameAs(expectedDraft));
            }
            finally
            {
                Selection.activeObject = previousSelection;
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WindowCatalog_ToggleChangesOnlyTheLeftmostPanelAndKeepsTheEditingDraft()
        {
            var previousSelection = Selection.activeObject;
            var windowType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerWindow");
            var window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            Assert.That(window, Is.Not.Null);
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                const BindingFlags constantFlags = BindingFlags.Static | BindingFlags.NonPublic;
                var shell = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(
                    "Assets/ProjectMT/02_Shared/Unit/Data/Monsters/shell_01/MD_shell_01.asset");
                windowType.GetMethod("TryOpenDefinition", flags)
                    ?.Invoke(window, new object[] { shell, false });
                var expectedDraft = windowType.GetField("draft", flags)?.GetValue(window);
                var setVisible = windowType.GetMethod("SetMonsterCatalogVisible", flags);
                Assert.That(setVisible, Is.Not.Null);
                Assert.That(windowType.GetMethod("DrawDraftHeader", flags), Is.Not.Null);
                Assert.That(windowType.GetMethod("DrawHeader", flags), Is.Null);

                var minimumWidth = (float)windowType.GetField("MinimumWindowWidth", constantFlags)
                    .GetRawConstantValue();
                var catalogWidth = (float)windowType.GetField("CatalogColumnWidth", constantFlags)
                    .GetRawConstantValue();
                var columnGap = (float)windowType.GetField("ColumnGap", constantFlags)
                    .GetRawConstantValue();

                setVisible.Invoke(window, new object[] { false });
                Assert.That(window.minSize.x, Is.EqualTo(minimumWidth).Within(0.001f));
                Assert.That(windowType.GetField("draft", flags)?.GetValue(window), Is.SameAs(expectedDraft));

                setVisible.Invoke(window, new object[] { true });
                Assert.That(window.minSize.x, Is.EqualTo(minimumWidth + catalogWidth + columnGap).Within(0.001f));
                Assert.That(windowType.GetField("draft", flags)?.GetValue(window), Is.SameAs(expectedDraft));
            }
            finally
            {
                Selection.activeObject = previousSelection;
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static void InvokeWriter(
            ScriptableObject draft,
            MonsterCatalog catalog,
            MonsterRarityCatalog rarityCatalog)
        {
            var writerType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerAssetWriter");
            var method = writerType.GetMethod("BuildAndRegister", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            try
            {
                method.Invoke(null, new object[] { draft, catalog, rarityCatalog });
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw exception.InnerException;
            }
        }

        private static Type FindEditorType(string fullName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, fullName + " 형식을 찾지 못했습니다.");
            return type;
        }

        private static string[] GetMakerIssueCodes(ScriptableObject draft)
        {
            var validatorType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerValidator");
            var report = validatorType.GetMethod("Validate", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new object[] { draft });
            var issues = report?.GetType().GetProperty("Issues")?.GetValue(report) as System.Collections.IEnumerable;
            Assert.That(issues, Is.Not.Null);
            return issues.Cast<object>()
                .Select(issue => issue.GetType().GetProperty("Code")?.GetValue(issue) as string)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToArray();
        }

        private static void ConfigureDraftIdentity(ScriptableObject draft, string monsterId)
        {
            var serialized = new SerializedObject(draft);
            serialized.FindProperty("monsterId").stringValue = monsterId;
            serialized.FindProperty("displayName").stringValue = "Maker Transaction Probe";
            serialized.FindProperty("ascension2").FindPropertyRelative("abilityId").stringValue = monsterId + "_a2";
            serialized.FindProperty("ascension4").FindPropertyRelative("abilityId").stringValue = monsterId + "_a4";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(ScriptableObject target, string propertyName, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void AddInvalidRarityEntry(MonsterRarityCatalog catalog)
        {
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("commonToEpicEntries");
            entries.InsertArrayElementAtIndex(entries.arraySize);
            var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("monster").objectReferenceValue = null;
            entry.FindPropertyRelative("passiveSkill").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void RemoveInvalidRarityEntries(MonsterRarityCatalog catalog)
        {
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("commonToEpicEntries");
            for (var index = entries.arraySize - 1; index >= 0; index--)
            {
                if (entries.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("monster")
                        .objectReferenceValue == null)
                {
                    entries.DeleteArrayElementAtIndex(index);
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        private static string[] BuildOutputPaths(string monsterId)
        {
            var data = DataRoot + "/" + monsterId;
            var art = ArtRoot + "/" + monsterId;
            return new[]
            {
                data + "/MD_" + monsterId + ".asset",
                data + "/MB_" + monsterId + ".asset",
                data + "/MM_" + monsterId + ".asset",
                data + "/MC_" + monsterId + ".asset",
                data + "/MA_" + monsterId + ".asset",
                data + "/MF_" + monsterId + ".asset",
                data + "/MR_" + monsterId + ".asset",
                art + "/AC_" + monsterId + ".controller",
                art + "/PF_" + monsterId + "_VisualAdapter.prefab"
            };
        }

        private static Dictionary<string, AssetState> CaptureStates(IEnumerable<string> paths)
        {
            return paths.ToDictionary(path => path, path => new AssetState(
                AssetDatabase.AssetPathToGUID(path),
                HashFile(path),
                HashFile(path + ".meta")));
        }

        private static void AssertStatesEqual(IReadOnlyDictionary<string, AssetState> expected)
        {
            foreach (var pair in expected)
            {
                var actual = new AssetState(
                    AssetDatabase.AssetPathToGUID(pair.Key),
                    HashFile(pair.Key),
                    HashFile(pair.Key + ".meta"));
                Assert.That(actual.Guid, Is.EqualTo(pair.Value.Guid), pair.Key + " GUID");
                Assert.That(actual.FileHash, Is.EqualTo(pair.Value.FileHash), pair.Key + " file");
                Assert.That(actual.MetaHash, Is.EqualTo(pair.Value.MetaHash), pair.Key + " meta");
            }
        }

        private static string HashFile(string assetPath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var fullPath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                return "<missing>";
            }

            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(fullPath))).Replace("-", string.Empty);
        }

        private static void DeleteAssetIfPresent(string path)
        {
            if (AssetDatabase.IsValidFolder(path) || AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        private readonly struct AssetState
        {
            public AssetState(string guid, string fileHash, string metaHash)
            {
                Guid = guid;
                FileHash = fileHash;
                MetaHash = metaHash;
            }

            public string Guid { get; }
            public string FileHash { get; }
            public string MetaHash { get; }
        }
    }
}
