using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Tests.EditMode
{
    public sealed class MonsterMakerSafetyContractTests // Maker 원자성·런타임 동일 Preview 구조 회귀 검사
    {
        private const string SpikeDraftPath =
            "Assets/ProjectMT/Editor/MonsterMaker/Drafts/Draft_lumi_01.asset";
        private const string ProductionCatalogPath =
            "Assets/ProjectMT/02_Shared/Unit/Data/MonsterCatalog.asset";
        private const string SingleProjectileBasicAttackPath =
            "Assets/ProjectMT/02_Shared/Unit/Data/BasicAttacks/BA_R_01.asset";
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
                var sound = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/ThirdParty/11_사운드/PRINCIPLE SOUND DESIGN - Mini Monsters/Mini Cutie/monster_mini_cutie_attack_fast_1.wav");
                Assert.That(sound, Is.Not.Null);
                ConfigureAttackSounds(draft, sound);
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
                AssetDatabase.SaveAssetIfDirty(rarityCatalog);

                InvokeWriter(draft, catalog, rarityCatalog);
                var outputPaths = BuildOutputPaths(monsterId);
                var firstGuids = outputPaths.ToDictionary(path => path, AssetDatabase.AssetPathToGUID);
                Assert.That(firstGuids.Values, Has.All.Not.Empty);
                var firstAscension = AssetDatabase.LoadAssetAtPath<MonsterAscensionProfile>(outputPaths[4]);
                Assert.That(firstAscension, Is.Not.Null);
                Assert.That(firstAscension.IsConfigured, Is.False);
                Assert.That(firstAscension.TryValidate(out var ascensionError), Is.True, ascensionError);
                Assert.That(firstAscension.ResolveStatModifier(5).IsEmpty, Is.True);
                Assert.That(firstAscension.ResolveUnlockedAbilityIds(5), Is.Empty);
                Assert.That(
                    AssetDatabase.LoadAllAssetsAtPath(outputPaths[4]).OfType<MonsterAbilityDefinition>(),
                    Is.Empty,
                    "돌파 미설정 Monster는 Ability 서브에셋을 만들지 않아야 합니다.");
                var firstMotion = AssetDatabase.LoadAssetAtPath<MonsterMotionProfile>(outputPaths[2]);
                var firstFeedback = AssetDatabase.LoadAssetAtPath<MonsterFeedbackProfile>(outputPaths[5]);
                var firstPresentation = firstFeedback.BasicAttackVfxBindings.Single(
                    binding => binding != null && binding.Sound == sound);
                var firstCue = firstPresentation.Sfx;
                var firstCombat = AssetDatabase.LoadAssetAtPath<MonsterCombatProfile>(outputPaths[3]);
                var firstRangedAction = firstCombat.Action as ProjectileActionDefinition;
                Assert.That(firstRangedAction, Is.Not.Null);
                Assert.That(firstRangedAction.DeliveryMode, Is.EqualTo(MonsterRangedDeliveryMode.Projectile));
                Assert.That(
                    firstRangedAction.ProjectilePrefab,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/ProjectMT/02_Shared/Combat/Prefabs/PF_SeedProjectile.prefab")),
                    "투사체 VFX가 비면 공용 임시 구슬이 자동 편입되어야 합니다.");
                Assert.That(firstRangedAction.LaunchSfx, Is.Null);
                Assert.That(firstMotion.Attacks[0].AttackStartOverride?.HasAnyFeedback ?? false, Is.False);
                Assert.That(firstMotion.Attacks[0].Markers[0].FeedbackOverride?.HasAnyFeedback ?? false, Is.False);
                Assert.That(firstCue, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(firstCue), Is.EqualTo(outputPaths[5]));
                Assert.That(firstCue.TrySelectClip(out var firstClip), Is.True);
                Assert.That(firstClip, Is.SameAs(sound));
                Assert.That(firstCue.SelectVolume(), Is.EqualTo(0.42f).Within(0.0001f));
                Assert.That(
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        firstCue,
                        out _,
                        out long firstCueLocalId),
                    Is.True);

                InvokeWriter(draft, catalog, rarityCatalog);
                foreach (var path in outputPaths)
                {
                    Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(firstGuids[path]), path);
                }

                var secondFeedback = AssetDatabase.LoadAssetAtPath<MonsterFeedbackProfile>(outputPaths[5]);
                var secondCue = secondFeedback.BasicAttackVfxBindings.Single(
                    binding => binding != null && binding.Sound == sound).Sfx;
                Assert.That(
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        secondCue,
                        out _,
                        out long secondCueLocalId),
                    Is.True);
                Assert.That(secondCueLocalId, Is.EqualTo(firstCueLocalId));
                Assert.That(
                    AssetDatabase.LoadAllAssetsAtPath(outputPaths[5]).OfType<SfxCue>().Count(),
                    Is.EqualTo(1),
                    "같은 Draft 재편입 시 자동 Cue가 중복 생성되면 안 됩니다.");

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
                AssetDatabase.SaveAssetIfDirty(rarityCatalog);
                AssetDatabase.SaveAssetIfDirty(draft);
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
                AssetDatabase.SaveAssetIfDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(draft);

                var beforeFailure = CaptureStates(touchedPaths);
                var failure = Assert.Throws<InvalidOperationException>(() => InvokeWriter(draft, catalog, rarityCatalog));
                StringAssert.Contains("duplicated", failure.Message.ToLowerInvariant());
                AssertStatesEqual(beforeFailure);
                var restored = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(outputPaths[0]);
                Assert.That(restored.AttackPower, Is.EqualTo(expectedAttackPower).Within(0.0001f));

                ConfigureDraftIdentity(draft, failedNewId);
                EditorUtility.SetDirty(draft);
                AssetDatabase.SaveAssetIfDirty(draft);
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
        public void Window_SaveDraftLeavesUnrelatedDirtyAssetsUnsaved()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var monsterId = "maker_save_" + suffix;
            var draftPath = "Assets/ProjectMT/Editor/MonsterMaker/Drafts/Draft_" + monsterId + ".asset";
            var tempRoot = "Assets/ProjectMT/99_Tests/_MonsterMakerSaveTemp_" + suffix;
            var unrelatedPath = tempRoot + "/Unrelated.asset";
            var previousSelection = Selection.activeObject;
            EditorWindow window = null;
            ScriptableObject draft = null;

            Assert.That(AssetDatabase.LoadMainAssetAtPath(draftPath), Is.Null);
            AssetDatabase.CreateFolder("Assets/ProjectMT/99_Tests", "_MonsterMakerSaveTemp_" + suffix);
            Selection.activeObject = null;
            try
            {
                var sourceDraft = AssetDatabase.LoadMainAssetAtPath(SpikeDraftPath) as ScriptableObject;
                Assert.That(sourceDraft, Is.Not.Null);
                draft = ScriptableObject.CreateInstance(sourceDraft.GetType());
                EditorUtility.CopySerialized(sourceDraft, draft);
                ConfigureDraftIdentity(draft, monsterId);

                var unrelated = CreateUnrelatedDirtyDefinition(
                    unrelatedPath,
                    "unsaved_window_" + suffix,
                    out var unrelatedSavedHash);
                var windowType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerWindow");
                window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
                Assert.That(window, Is.Not.Null);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                windowType.GetMethod("SetDraft", flags)?.Invoke(window, new object[] { draft, true });

                var saved = (bool)windowType.GetMethod("SaveDraft", flags).Invoke(window, null);

                Assert.That(saved, Is.True);
                Assert.That(AssetDatabase.GetAssetPath(draft), Is.EqualTo(draftPath));
                Assert.That(EditorUtility.IsDirty(draft), Is.False);
                Assert.That(HashFile(unrelatedPath), Is.EqualTo(unrelatedSavedHash));
                Assert.That(EditorUtility.IsDirty(unrelated), Is.True);
            }
            finally
            {
                if (window != null)
                {
                    UnityEngine.Object.DestroyImmediate(window);
                }

                if (draft != null && !EditorUtility.IsPersistent(draft))
                {
                    UnityEngine.Object.DestroyImmediate(draft);
                }

                Selection.activeObject = previousSelection;
                DeleteAssetIfPresent(draftPath);
                DeleteAssetIfPresent(tempRoot);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        [Test]
        public void BuildAndRegister_LeavesUnrelatedDirtyAssetsUnsaved()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var monsterId = "maker_scope_" + suffix;
            var tempRoot = "Assets/ProjectMT/99_Tests/_MonsterMakerScopeTemp_" + suffix;
            var dataFolder = DataRoot + "/" + monsterId;
            var artFolder = ArtRoot + "/" + monsterId;

            Assert.That(AssetDatabase.IsValidFolder(dataFolder), Is.False);
            Assert.That(AssetDatabase.IsValidFolder(artFolder), Is.False);
            AssetDatabase.CreateFolder("Assets/ProjectMT/99_Tests", "_MonsterMakerScopeTemp_" + suffix);
            try
            {
                var sourceDraft = AssetDatabase.LoadMainAssetAtPath(SpikeDraftPath) as ScriptableObject;
                Assert.That(sourceDraft, Is.Not.Null);
                var draft = ScriptableObject.CreateInstance(sourceDraft.GetType());
                EditorUtility.CopySerialized(sourceDraft, draft);
                ConfigureDraftIdentity(draft, monsterId);
                AssetDatabase.CreateAsset(draft, tempRoot + "/Draft.asset");

                var catalog = ScriptableObject.CreateInstance<MonsterCatalog>();
                catalog.EditorSetDefinitions(Array.Empty<MonsterDefinition>());
                AssetDatabase.CreateAsset(catalog, tempRoot + "/MonsterCatalog.asset");

                var rarityCatalog = ScriptableObject.CreateInstance<MonsterRarityCatalog>();
                AssetDatabase.CreateAsset(rarityCatalog, tempRoot + "/MonsterRarityCatalog.asset");
                var raritySerialized = new SerializedObject(rarityCatalog);
                raritySerialized.FindProperty("sourceCatalog").objectReferenceValue = catalog;
                raritySerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rarityCatalog);
                AssetDatabase.SaveAssetIfDirty(rarityCatalog);

                var unrelatedPath = tempRoot + "/Unrelated.asset";
                var unrelated = CreateUnrelatedDirtyDefinition(
                    unrelatedPath,
                    "unsaved_writer_" + suffix,
                    out var unrelatedSavedHash);

                InvokeWriter(draft, catalog, rarityCatalog);

                Assert.That(catalog.TryGet(monsterId, out var generated), Is.True);
                Assert.That(generated, Is.Not.Null);
                Assert.That(EditorUtility.IsDirty(catalog), Is.False);
                Assert.That(EditorUtility.IsDirty(rarityCatalog), Is.False);
                Assert.That(HashFile(unrelatedPath), Is.EqualTo(unrelatedSavedHash));
                Assert.That(EditorUtility.IsDirty(unrelated), Is.True);
            }
            finally
            {
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
        public void Preview_PositionEditingUsesDedicatedTransactionalPopup()
        {
            var windowType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterMakerWindow");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.That(windowType.GetMethod("PerformMakerUndo", flags), Is.Not.Null);
            Assert.That(windowType.GetMethod("OnUndoRedoPerformed", flags), Is.Not.Null);
            Assert.That(windowType.GetMethod("ApplyPopupPositionValue", flags), Is.Not.Null);
            Assert.That(windowType.GetMethod("ApplyInitialDraftSnapshot", flags), Is.Not.Null);

            var popupType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterPositionAdjustWindow");
            Assert.That(
                popupType.GetMethod("Open", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
                Is.Not.Null);
            Assert.That(
                popupType.GetMethod("CanOpen", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
                Is.Not.Null);
            Assert.That(popupType.GetMethod("DrawPositionHandle", flags), Is.Not.Null);
            Assert.That(popupType.GetMethod("DrawBottomControls", flags), Is.Not.Null);
            Assert.That(
                popupType.GetMethod(
                    "DrawCompactVector3Field",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Not.Null);
            Assert.That(popupType.GetMethod("RestartVfxPreview", flags), Is.Not.Null);
            Assert.That(popupType.GetMethod("UpdateVfxPreview", flags), Is.Not.Null);
        }

        [Test]
        public void PositionPopup_VfxSpeedGaugeUsesOneAsLogarithmicCenterAndExpands()
        {
            var popupType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterPositionAdjustWindow");
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var resolveExponent = popupType.GetMethod(
                "ResolveVfxPlaybackSpeedGaugeExponent",
                flags);
            var toGauge = popupType.GetMethod("ToVfxPlaybackSpeedGaugeValue", flags);
            var fromGauge = popupType.GetMethod("FromVfxPlaybackSpeedGaugeValue", flags);

            Assert.That(resolveExponent, Is.Not.Null);
            Assert.That(toGauge, Is.Not.Null);
            Assert.That(fromGauge, Is.Not.Null);
            Assert.That((float)toGauge.Invoke(null, new object[] { 1f }), Is.Zero.Within(0.0001f));
            Assert.That(
                (float)fromGauge.Invoke(null, new object[] { -1f }),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                (float)fromGauge.Invoke(null, new object[] { 1f }),
                Is.EqualTo(2f).Within(0.0001f));
            Assert.That(
                (float)resolveExponent.Invoke(null, new object[] { 4f }),
                Is.EqualTo(2f).Within(0.0001f));
            Assert.That(
                (float)resolveExponent.Invoke(null, new object[] { 128f }),
                Is.EqualTo(7f).Within(0.0001f),
                "숫자로 입력한 큰 배율도 게이지가 임의 상한으로 잘라서는 안 됩니다.");
        }

        [Test]
        public void Preview_VfxTickPreservesPlaybackOffsetInsteadOfRestartingFromZero()
        {
            var previewType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterMakerPreviewStage");
            var preview = Activator.CreateInstance(previewType);
            var vfx = new GameObject("PlaybackOffsetRegressionVfx");
            var particle = vfx.AddComponent<ParticleSystem>();
            var activeVfx = previewType
                .GetField("activeVfx", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(preview) as System.Collections.IList;
            try
            {
                var main = particle.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 2f;
                main.startLifetime = 1f;
                main.maxParticles = 8;
                var emission = particle.emission;
                emission.rateOverTime = 0f;

                MonsterBasicAttackVfxPlayback.RestartAtOffset(vfx, 1.31f, false);
                particle.Emit(1);
                particle.Simulate(0.1f, false, false, true);
                Assert.That(particle.particleCount, Is.GreaterThan(0), "오프셋 이후 재생 상태가 준비되어야 합니다.");

                var previewVfxType = previewType.GetNestedType(
                    "PreviewVfx",
                    BindingFlags.NonPublic);
                var previewVfx = Activator.CreateInstance(
                    previewVfxType,
                    vfx,
                    1f,
                    MonsterBasicAttackVfxEndPolicy.Timed,
                    null);
                previewVfxType.GetProperty("Elapsed")?.SetValue(previewVfx, 1.31f);
                Assert.That(activeVfx, Is.Not.Null);
                activeVfx.Add(previewVfx);

                previewType.GetMethod("TickVfx", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(preview, new object[] { 0.016f });

                Assert.That(
                    particle.particleCount,
                    Is.GreaterThan(0),
                    "Preview Tick은 적용된 재생 오프셋을 0초 기준으로 되돌리면 안 됩니다.");
                Assert.That(
                    (float)previewVfxType.GetProperty("Elapsed")?.GetValue(previewVfx),
                    Is.EqualTo(1.326f).Within(0.0001f));
            }
            finally
            {
                activeVfx?.Clear();
                UnityEngine.Object.DestroyImmediate(vfx);
                (preview as IDisposable)?.Dispose();
            }
        }

        [Test]
        public void PositionPopup_OrbitAndZoomMatchCommonPreviewContract()
        {
            var popupType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterPositionAdjustWindow");
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var calculateOrbit = popupType.GetMethod("CalculateOrbit", flags);
            var calculateDistanceScale = popupType.GetMethod("CalculateDistanceScale", flags);
            Assert.That(calculateOrbit, Is.Not.Null);
            Assert.That(calculateDistanceScale, Is.Not.Null);

            var orbit = (Vector2)calculateOrbit.Invoke(
                null,
                new object[] { new Vector2(145f, 12f), new Vector2(20f, -10f) });
            Assert.That(orbit.x, Is.EqualTo(138f).Within(0.0001f));
            Assert.That(orbit.y, Is.EqualTo(8.5f).Within(0.0001f));

            var upperPitch = (Vector2)calculateOrbit.Invoke(
                null,
                new object[] { orbit, new Vector2(0f, 1000f) });
            Assert.That(upperPitch.y, Is.EqualTo(80f));
            Assert.That(
                (float)calculateDistanceScale.Invoke(null, new object[] { 1f, 1f }),
                Is.EqualTo(1.08f).Within(0.0001f));
            Assert.That(
                (float)calculateDistanceScale.Invoke(null, new object[] { 1f, -100f }),
                Is.EqualTo(0.15f));
            Assert.That(
                (float)calculateDistanceScale.Invoke(null, new object[] { 1f, 100f }),
                Is.EqualTo(8f));
        }

        [Test]
        public void Preview_ReferenceToolbarIsResponsiveAndAvoidsCombatStatus()
        {
            var overlayType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterPositionReferenceOverlay");
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var method = overlayType.GetMethod(
                "CalculateVisibilityToolbarRect",
                flags | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);

            var widePreview = new Rect(698f, 118f, 822f, 612f);
            var wideToolbar = (Rect)method.Invoke(null, new object[] { widePreview, 255f });
            Assert.That(wideToolbar.y, Is.EqualTo(widePreview.y + 10f));
            Assert.That(wideToolbar.x, Is.GreaterThanOrEqualTo(widePreview.x));
            Assert.That(wideToolbar.xMax, Is.LessThanOrEqualTo(widePreview.xMax));

            var narrowPreview = new Rect(698f, 118f, 420f, 612f);
            var narrowToolbar = (Rect)method.Invoke(null, new object[] { narrowPreview, 255f });
            Assert.That(narrowToolbar.y, Is.EqualTo(narrowPreview.y + 10f + 55f));
            Assert.That(narrowToolbar.x, Is.GreaterThanOrEqualTo(narrowPreview.x));
            Assert.That(narrowToolbar.xMax, Is.LessThanOrEqualTo(narrowPreview.xMax));
        }

        [Test]
        public void Window_InitialStateRestoreIsUndoable()
        {
            var previousSelection = Selection.activeObject;
            Selection.activeObject = null;
            var windowType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerWindow");
            var window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            Assert.That(window, Is.Not.Null);
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var draft = windowType.GetField("draft", flags)?.GetValue(window) as ScriptableObject;
                var snapshot = windowType.GetField("initialDraftSnapshot", flags)?.GetValue(window);
                Assert.That(draft, Is.Not.Null);
                Assert.That(EditorUtility.IsPersistent(draft), Is.False);
                Assert.That(snapshot, Is.Not.Null);

                var serialized = new SerializedObject(draft);
                var position = serialized.FindProperty("attackOriginLocalPosition");
                var initial = position.vector3Value;
                var modified = initial + new Vector3(0.25f, 0.5f, 0.75f);
                position.vector3Value = modified;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                windowType.GetMethod("ApplyInitialDraftSnapshot", flags)
                    ?.Invoke(window, null);
                serialized = new SerializedObject(draft);
                Assert.That(
                    serialized.FindProperty("attackOriginLocalPosition").vector3Value,
                    Is.EqualTo(initial));

                Undo.PerformUndo();
                serialized.UpdateIfRequiredOrScript();
                Assert.That(
                    serialized.FindProperty("attackOriginLocalPosition").vector3Value,
                    Is.EqualTo(modified));
                Undo.ClearUndo(draft);
            }
            finally
            {
                Selection.activeObject = previousSelection;
                UnityEngine.Object.DestroyImmediate(window);
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
            Assert.That(
                makerCodes.Any(code => code.StartsWith("MAKER-ASCENSION", StringComparison.Ordinal)),
                Is.False,
                "돌파 옵션을 사용하지 않는 Draft는 빈 돌파값으로 편입할 수 있어야 합니다.");

            var definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(
                "Assets/ProjectMT/02_Shared/Unit/Data/Monsters/lumi_01/MD_lumi_01.asset");
            Assert.That(definition, Is.Not.Null);
            var runtimeReport = MonsterDefinitionValidator.Validate(definition, true);
            Assert.That(
                runtimeReport.Issues.Any(issue => issue.Code.StartsWith("MON-FX", StringComparison.Ordinal)),
                Is.False);
        }

        [Test]
        public void BasicAttackPresentation_UsesAudioClipSourceAndGeneratedRuntimeCue()
        {
            var bindingType = typeof(MonsterBasicAttackVfxBinding);
            Assert.That(
                bindingType.GetProperty("Sound", BindingFlags.Instance | BindingFlags.Public)?.PropertyType,
                Is.EqualTo(typeof(AudioClip)));
            Assert.That(
                bindingType.GetProperty("Sfx", BindingFlags.Instance | BindingFlags.Public)?.PropertyType,
                Is.EqualTo(typeof(SfxCue)));
            Assert.That(
                bindingType.GetProperty("SfxState", BindingFlags.Instance | BindingFlags.Public)?.PropertyType,
                Is.EqualTo(typeof(MonsterBasicAttackSfxAssignmentState)));
            Assert.That(
                bindingType.GetProperty("SoundVolume", BindingFlags.Instance | BindingFlags.Public)?.PropertyType,
                Is.EqualTo(typeof(float)));
            Assert.That(
                typeof(MonsterBasicAttackVfxSlot).GetProperty("Required", BindingFlags.Instance | BindingFlags.Public),
                Is.Null);
            var attackType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerAttackDraft");
            Assert.That(
                attackType.GetProperty("AttackStartFeedback", BindingFlags.Instance | BindingFlags.Public),
                Is.Null);

            var previewType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerPreviewStage");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            Assert.That(previewType.GetMethod("PlayHitFeedback", flags), Is.Null);
            Assert.That(previewType.GetMethod("PlaySpecialFeedback", flags), Is.Null);
        }

        [Test]
        public void Window_NewAttackAndMarkerUseUniqueIdsWithoutLegacyFeedbackFields()
        {
            var sourceDraft = AssetDatabase.LoadMainAssetAtPath(SpikeDraftPath) as ScriptableObject;
            Assert.That(sourceDraft, Is.Not.Null);
            var draft = ScriptableObject.CreateInstance(sourceDraft.GetType());
            EditorUtility.CopySerialized(sourceDraft, draft);
            try
            {
                var serialized = new SerializedObject(draft);
                var attacks = serialized.FindProperty("attacks");
                attacks.arraySize = 1; // 참조 Draft의 기존 공격 수와 무관하게 ID 생성만 검사
                var firstAttack = attacks.GetArrayElementAtIndex(0);
                firstAttack.FindPropertyRelative("motionId").stringValue = "attack01";
                Assert.That(firstAttack.FindPropertyRelative("attackStartFeedback"), Is.Null);
                Assert.That(
                    firstAttack.FindPropertyRelative("markers").GetArrayElementAtIndex(0)
                        .FindPropertyRelative("feedback"),
                    Is.Null);

                var windowType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerWindow");
                var addAttack = windowType.GetMethod("AddAttack", BindingFlags.Static | BindingFlags.NonPublic);
                var addMarker = windowType.GetMethod("AddMarker", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(addAttack, Is.Not.Null);
                Assert.That(addMarker, Is.Not.Null);

                addAttack.Invoke(null, new object[] { attacks });
                var secondAttack = attacks.GetArrayElementAtIndex(1);
                Assert.That(secondAttack.FindPropertyRelative("motionId").stringValue, Is.EqualTo("attack02"));

                secondAttack.FindPropertyRelative("motionId").stringValue = "attack03";
                addAttack.Invoke(null, new object[] { attacks });
                var thirdAttack = attacks.GetArrayElementAtIndex(2);
                Assert.That(thirdAttack.FindPropertyRelative("motionId").stringValue, Is.EqualTo("attack02"));

                var firstMarkers = attacks.GetArrayElementAtIndex(0).FindPropertyRelative("markers");
                addMarker.Invoke(null, new object[] { firstMarkers });
                Assert.That(firstMarkers.arraySize, Is.EqualTo(2));
                Assert.That(firstMarkers.GetArrayElementAtIndex(1).FindPropertyRelative("feedback"), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
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
        public void Preview_AttackButtonHitsPeasantWithRuntimeDamageFeedback()
        {
            var draft = AssetDatabase.LoadMainAssetAtPath(SpikeDraftPath) as ScriptableObject;
            Assert.That(draft, Is.Not.Null);
            var previewType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerPreviewStage");
            var preview = Activator.CreateInstance(previewType);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            try
            {
                previewType.GetMethod("SetDraft", flags)?.Invoke(preview, new object[] { draft });
                var target = previewType.GetField("dummyTarget", flags)?.GetValue(preview) as GameObject;
                Assert.That(target, Is.Not.Null);
                Assert.That(target.name, Does.Contain("PF_Enemy_Peasant"));
                Assert.That(target.GetComponent<HealthComponent>(), Is.Not.Null);
                Assert.That(target.GetComponent<UnitActor>(), Is.Not.Null);
                Assert.That(target.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));

                var healthProperty = previewType.GetProperty("CombatTargetCurrentHealth", flags);
                var initialHealth = (float)healthProperty.GetValue(preview);
                previewType.GetMethod("PlayAttack", flags)?.Invoke(preview, new object[] { 0 });
                var tick = previewType.GetMethod("Tick", flags, null, new[] { typeof(float) }, null);
                Assert.That(tick, Is.Not.Null);
                var hitCountProperty = previewType.GetProperty("PreviewHitCount", flags);
                for (var index = 0; index < 200 && (int)hitCountProperty.GetValue(preview) == 0; index++)
                {
                    tick.Invoke(preview, new object[] { 0.01f });
                }

                Assert.That((int)hitCountProperty.GetValue(preview), Is.EqualTo(1));
                var lastDamage = (float)previewType.GetProperty("LastAppliedDamage", flags).GetValue(preview);
                Assert.That(lastDamage, Is.EqualTo(18f).Within(0.001f));
                Assert.That((float)healthProperty.GetValue(preview), Is.EqualTo(initialHealth - lastDamage).Within(0.001f));

                tick.Invoke(preview, new object[] { 0.09f }); // 게임과 같은 0.08초 합산 뒤 숫자 표시
                Assert.That(
                    (int)previewType.GetProperty("ActiveFloatingNumberCount", flags).GetValue(preview),
                    Is.GreaterThan(0));
                var floatingObject = Resources.FindObjectsOfTypeAll<GameObject>()
                    .FirstOrDefault(candidate => candidate.name.StartsWith("[Monster Preview Damage]"));
                Assert.That(floatingObject, Is.Not.Null);
                Assert.That(floatingObject.activeInHierarchy, Is.True, "숫자 Prefab은 원본이 비활성이므로 Preview가 켜야 합니다.");
                Assert.That(floatingObject.GetComponent<Renderer>()?.bounds.size.sqrMagnitude, Is.GreaterThan(0f));
                Assert.That(
                    floatingObject.transform.localScale.x,
                    Is.LessThan(0f),
                    "PreviewRenderUtility에서 숫자가 거울상 없이 읽히도록 TMP 방향을 보정해야 합니다.");
                Assert.That(
                    (int)previewType.GetProperty("ActiveHitVfxCount", flags).GetValue(preview),
                    Is.GreaterThan(0));
                Assert.That(
                    previewType.GetMethod("Render", flags)?.Invoke(
                        preview,
                        new object[] { new Rect(0f, 0f, 960f, 640f) }),
                    Is.Not.Null);
            }
            finally
            {
                (preview as IDisposable)?.Dispose();
            }
        }

        [Test]
        public void Preview_BlankProjectileUsesCarrierAndDamagesOnlyOnImpact()
        {
            var source = AssetDatabase.LoadMainAssetAtPath(SpikeDraftPath) as ScriptableObject;
            Assert.That(source, Is.Not.Null);
            var draft = ScriptableObject.CreateInstance(source.GetType());
            EditorUtility.CopySerialized(source, draft);
            var serialized = new SerializedObject(draft);
            serialized.FindProperty("basicAttackProfile").objectReferenceValue = null;
            serialized.FindProperty("combatType").enumValueIndex = (int)MonsterCombatType.Ranged;
            serialized.FindProperty("rangedDeliveryMode").enumValueIndex = (int)MonsterRangedDeliveryMode.Projectile;
            serialized.FindProperty("projectilePrefab").objectReferenceValue = null;
            serialized.FindProperty("projectileSpeed").floatValue = 2f;
            serialized.FindProperty("projectileLifetime").floatValue = 3f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var previewType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerPreviewStage");
            var preview = Activator.CreateInstance(previewType);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            try
            {
                previewType.GetMethod("SetDraft", flags)?.Invoke(preview, new object[] { draft });
                var targetDistance = previewType.GetProperty("CombatTargetDistance", flags);
                Assert.That(
                    (float)targetDistance.GetValue(preview),
                    Is.GreaterThanOrEqualTo(2.99f),
                    "원거리 Preview는 투사체 이동을 볼 수 있도록 표준 적을 최소 3m 떨어뜨려야 합니다.");
                previewType.GetMethod("PlayAttack", flags)?.Invoke(preview, new object[] { 0 });
                var tick = previewType.GetMethod("Tick", flags, null, new[] { typeof(float) }, null);
                var projectileCount = previewType.GetProperty("ActiveProjectileCount", flags);
                var markerVfxCount = previewType.GetProperty("ActiveMarkerVfxCount", flags);
                var hitCount = previewType.GetProperty("PreviewHitCount", flags);
                for (var index = 0; index < 200 && (int)projectileCount.GetValue(preview) == 0; index++)
                {
                    tick.Invoke(preview, new object[] { 0.01f });
                }

                Assert.That((int)projectileCount.GetValue(preview), Is.EqualTo(1));
                Assert.That((int)hitCount.GetValue(preview), Is.EqualTo(0), "Marker는 발사 시점이고 피해는 도착 뒤여야 합니다.");
                Assert.That(
                    (int)markerVfxCount.GetValue(preview),
                    Is.EqualTo(0),
                    "Marker의 타격 VFX는 투사체 발사 때가 아니라 실제 명중 때 나와야 합니다.");
                for (var index = 0; index < 300 && (int)hitCount.GetValue(preview) == 0; index++)
                {
                    tick.Invoke(preview, new object[] { 0.01f });
                }

                Assert.That((int)hitCount.GetValue(preview), Is.EqualTo(1));
                Assert.That((int)projectileCount.GetValue(preview), Is.EqualTo(0));
                Assert.That((int)markerVfxCount.GetValue(preview), Is.EqualTo(0));
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
            try
            {
                EditorUtility.CopySerialized(sourceDraft, draft);
                var serialized = new SerializedObject(draft);
                serialized.FindProperty("monsterId").stringValue = "몬스터_01";
                serialized.FindProperty("combatType").enumValueIndex = (int)MonsterCombatType.Ranged;
                serialized.FindProperty("rangedDeliveryMode").enumValueIndex = (int)MonsterRangedDeliveryMode.Projectile;
                serialized.FindProperty("projectilePrefab").objectReferenceValue = null;
                serialized.FindProperty("basicAttackProfile").objectReferenceValue = null;
                serialized.FindProperty("projectileMode").enumValueIndex = (int)MonsterProjectileAttackMode.Piercing;
                serialized.FindProperty("projectileHitRadius").floatValue = 0f;
                serialized.FindProperty("projectileMaxPiercingTargets").intValue = 0;
                serialized.FindProperty("projectileLaunchRecoilDistance").floatValue = -0.1f;
                serialized.FindProperty("projectileLaunchRecoilDuration").floatValue = 0f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var codes = GetMakerIssueCodes(draft);
                Assert.That(codes, Does.Contain("MAKER-ID-CHAR"));
                Assert.That(codes, Does.Contain("MAKER-PROJECTILE-PIERCING"));
                Assert.That(codes, Does.Contain("MAKER-PROJECTILE-RECOIL"));

                serialized.Update();
                serialized.FindProperty("projectileMode").enumValueIndex = (int)MonsterProjectileAttackMode.Area;
                serialized.FindProperty("projectileImpactRadius").floatValue = 0f;
                serialized.FindProperty("projectileMaxImpactTargets").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(GetMakerIssueCodes(draft), Does.Contain("MAKER-PROJECTILE-AREA"));

                serialized.Update();
                serialized.FindProperty("rangedDeliveryMode").enumValueIndex = (int)MonsterRangedDeliveryMode.Instant;
                serialized.FindProperty("projectileMode").enumValueIndex = (int)MonsterProjectileAttackMode.Piercing;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var instantCodes = GetMakerIssueCodes(draft);
                Assert.That(instantCodes, Does.Contain("MAKER-INSTANT-PIERCING"));
                Assert.That(instantCodes, Does.Not.Contain("MAKER-PROJECTILE"));
            }
            finally
            {
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

                var loadDraft = windowType.GetMethod(
                    "LoadDraftForDefinition",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(loadDraft, Is.Not.Null);
                Assert.That(listed, Has.All.Matches<MonsterDefinition>(definition =>
                    loadDraft.Invoke(null, new object[] { definition }) != null));
                Assert.That(listed.Any(definition => definition.MonsterId.StartsWith("tofu_")), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WindowCatalog_SwitchesBetweenDefaultAndDescendingRarityOrder()
        {
            var windowType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerWindow");
            var window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            Assert.That(window, Is.Not.Null);
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                windowType.GetMethod("ReloadCatalogEntries", flags)?.Invoke(window, null);
                var definitions = windowType.GetField("catalogDefinitions", flags)?.GetValue(window)
                    as MonsterDefinition[];
                var displayedField = windowType.GetField("displayedCatalogDefinitions", flags);
                var displayed = displayedField?.GetValue(window) as MonsterDefinition[];
                var rarityCache = windowType.GetField("catalogRaritiesById", flags)?.GetValue(window)
                    as System.Collections.IDictionary;
                var sortModeField = windowType.GetField("catalogSortMode", flags);
                var setSortMode = windowType.GetMethod("SetCatalogSortMode", flags);
                var selectedField = windowType.GetField("selectedCatalogDefinition", flags);
                var scrollField = windowType.GetField("catalogScroll", flags);

                Assert.That(definitions, Is.Not.Null.And.Not.Empty);
                Assert.That(displayed, Is.Not.Null);
                Assert.That(rarityCache, Is.Not.Null);
                Assert.That(sortModeField, Is.Not.Null);
                Assert.That(setSortMode, Is.Not.Null);
                Assert.That(displayedField, Is.Not.Null);
                Assert.That(selectedField, Is.Not.Null);
                Assert.That(scrollField, Is.Not.Null);
                CollectionAssert.AreEqual(definitions, displayed);

                var selected = definitions[definitions.Length / 2];
                selectedField?.SetValue(window, selected);
                scrollField?.SetValue(window, new Vector2(0f, 180f));
                var rarityMode = Enum.ToObject(sortModeField.FieldType, 1);
                setSortMode.Invoke(window, new[] { rarityMode });

                displayed = displayedField.GetValue(window) as MonsterDefinition[];
                Assert.That(displayed, Is.Not.Null.And.Length.EqualTo(definitions.Length));
                Assert.That(selectedField.GetValue(window), Is.SameAs(selected));
                Assert.That(((Vector2)scrollField.GetValue(window)).y, Is.Zero);

                var defaultIndices = definitions
                    .Select((definition, index) => new { definition.MonsterId, index })
                    .ToDictionary(entry => entry.MonsterId, entry => entry.index);
                for (var index = 1; index < displayed.Length; index++)
                {
                    var previous = (MonsterRarity)rarityCache[displayed[index - 1].MonsterId];
                    var current = (MonsterRarity)rarityCache[displayed[index].MonsterId];
                    Assert.That((int)previous, Is.GreaterThanOrEqualTo((int)current));
                    if (previous == current)
                    {
                        Assert.That(
                            defaultIndices[displayed[index - 1].MonsterId],
                            Is.LessThan(defaultIndices[displayed[index].MonsterId]));
                    }
                }

                var defaultMode = Enum.ToObject(sortModeField.FieldType, 0);
                setSortMode.Invoke(window, new[] { defaultMode });
                displayed = displayedField.GetValue(window) as MonsterDefinition[];
                CollectionAssert.AreEqual(definitions, displayed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void WindowProfileSummary_UsesTheLeftSideOfTheBottomWorkspaceAndCurrentDraftLabels()
        {
            var windowType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerWindow");
            const BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.NonPublic;

            Assert.That(windowType.GetMethod("DrawBottomWorkspace", instanceFlags), Is.Not.Null);
            Assert.That(windowType.GetMethod("DrawMonsterProfileSummary", instanceFlags), Is.Not.Null);
            Assert.That(windowType.GetMethod("DrawProfileIdentity", instanceFlags), Is.Not.Null);
            Assert.That(windowType.GetMethod("BuildProfileSkillSummary", instanceFlags), Is.Not.Null);

            var minimumWidth = (float)windowType
                .GetField("ProfileSummaryMinWidth", staticFlags)
                .GetRawConstantValue();
            var maximumWidth = (float)windowType
                .GetField("ProfileSummaryMaxWidth", staticFlags)
                .GetRawConstantValue();
            Assert.That(minimumWidth, Is.GreaterThanOrEqualTo(280f));
            Assert.That(maximumWidth, Is.GreaterThan(minimumWidth));

            var rarityLabel = windowType.GetMethod(
                "GetRarityLabel",
                staticFlags,
                null,
                new[] { typeof(MonsterRarity) },
                null);
            Assert.That(rarityLabel, Is.Not.Null);
            Assert.That(rarityLabel.Invoke(null, new object[] { MonsterRarity.Mythic }), Is.EqualTo("신화"));
        }

        [Test]
        public void WindowCatalog_PortraitPreviewUsesEveryAssignedSpriteTexture()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(ProductionCatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Definitions, Has.Count.EqualTo(44));

            var windowType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerWindow");
            var resolve = windowType.GetMethod(
                "TryResolvePortraitPreview",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(resolve, Is.Not.Null);

            foreach (var definition in catalog.Definitions)
            {
                Assert.That(definition, Is.Not.Null);
                Assert.That(definition.Portrait, Is.Not.Null, definition.MonsterId);
                var arguments = new object[] { definition.Portrait, null, default(Rect) };
                var resolved = (bool)resolve.Invoke(null, arguments);

                Assert.That(resolved, Is.True, definition.MonsterId);
                Assert.That(arguments[1], Is.SameAs(definition.Portrait.texture), definition.MonsterId);
                var uv = (Rect)arguments[2];
                Assert.That(uv.width, Is.GreaterThan(0f), definition.MonsterId);
                Assert.That(uv.height, Is.GreaterThan(0f), definition.MonsterId);
                Assert.That(uv.xMin, Is.GreaterThanOrEqualTo(0f), definition.MonsterId);
                Assert.That(uv.yMin, Is.GreaterThanOrEqualTo(0f), definition.MonsterId);
                Assert.That(uv.xMax, Is.LessThanOrEqualTo(1f), definition.MonsterId);
                Assert.That(uv.yMax, Is.LessThanOrEqualTo(1f), definition.MonsterId);
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
                    "Assets/ProjectMT/02_Shared/Unit/Data/Monsters/rabi_01/MD_rabi_01.asset");
                var expectedDraft = AssetDatabase.LoadMainAssetAtPath(
                    "Assets/ProjectMT/Editor/MonsterMaker/Drafts/Draft_rabi_01.asset");
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
                    "Assets/ProjectMT/02_Shared/Unit/Data/Monsters/rabi_01/MD_rabi_01.asset");
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

        [Test]
        public void Window_FullyDisabledPresentationCompactsOnlyWhenBothChannelsAreOff()
        {
            var windowType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerWindow");
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var isFullyDisabled = windowType.GetMethod(
                "IsBasicAttackPresentationFullyDisabled",
                flags);

            Assert.That(isFullyDisabled, Is.Not.Null);
            Assert.That((bool)isFullyDisabled.Invoke(null, new object[]
            {
                MonsterBasicAttackSfxAssignmentState.Disabled,
                MonsterBasicAttackVfxAssignmentState.Disabled
            }), Is.True);
            Assert.That((bool)isFullyDisabled.Invoke(null, new object[]
            {
                MonsterBasicAttackSfxAssignmentState.Assigned,
                MonsterBasicAttackVfxAssignmentState.Disabled
            }), Is.False);
            Assert.That((bool)isFullyDisabled.Invoke(null, new object[]
            {
                MonsterBasicAttackSfxAssignmentState.Disabled,
                MonsterBasicAttackVfxAssignmentState.Assigned
            }), Is.False);
            Assert.That((bool)isFullyDisabled.Invoke(null, new object[]
            {
                MonsterBasicAttackSfxAssignmentState.Undecided,
                MonsterBasicAttackVfxAssignmentState.Undecided
            }), Is.False);
        }

        [Test]
        public void Window_VfxTimingGaugeExpandsWithoutClampingExactValue()
        {
            var windowType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerWindow");
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var drawToggle = windowType.GetMethod("DrawBasicAttackVfxTimingToggle", flags);
            var drawGauge = windowType.GetMethod("DrawBasicAttackVfxTimingGauge", flags);
            var resolveRange = windowType.GetMethod("ResolveBasicAttackVfxTimingGaugeRange", flags);

            Assert.That(drawToggle, Is.Not.Null);
            Assert.That(drawGauge, Is.Not.Null);
            Assert.That(resolveRange, Is.Not.Null);
            Assert.That((float)resolveRange.Invoke(null, new object[] { 0f }), Is.EqualTo(0.5f));
            Assert.That((float)resolveRange.Invoke(null, new object[] { -0.15f }), Is.EqualTo(0.5f));
            Assert.That((float)resolveRange.Invoke(null, new object[] { 3.6f }), Is.EqualTo(4f));
            Assert.That((float)resolveRange.Invoke(null, new object[] { -7.2f }), Is.EqualTo(7.5f));
        }

        [Test]
        public void WindowLayout_UsesEqualOuterMarginsAndKeepsEveryColumnInsideTheWorkspace()
        {
            var windowType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerWindow");
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            const BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            var calculateWorkspace = windowType.GetMethod("CalculateWorkspaceRect", flags);
            var calculateCenterWidth = windowType.GetMethod("CalculateCenterColumnWidth", flags);
            Assert.That(calculateWorkspace, Is.Not.Null);
            Assert.That(calculateCenterWidth, Is.Not.Null);
            Assert.That(windowType.GetMethod("DrawPreviewPanel", instanceFlags), Is.Not.Null);
            Assert.That(windowType.GetMethod("DrawTimeline", instanceFlags), Is.Not.Null);
            Assert.That(windowType.GetMethod("DrawBottomActionPanel", instanceFlags), Is.Not.Null);
            Assert.That(windowType.GetMethod("ResolvePreviewHeight", instanceFlags), Is.Null);

            var outerMargin = (float)windowType.GetField("OuterMargin", flags).GetRawConstantValue();
            var columnGap = (float)windowType.GetField("ColumnGap", flags).GetRawConstantValue();
            var catalogWidth = (float)windowType.GetField("CatalogColumnWidth", flags).GetRawConstantValue();
            var leftWidth = (float)windowType.GetField("LeftColumnWidth", flags).GetRawConstantValue();
            var previewMinimum = (float)windowType.GetField("PreviewColumnMinWidth", flags).GetRawConstantValue();

            var windowWidths = new[] { 1180f, 1418f, 1878f };
            var catalogStates = new[] { false, true, true };
            for (var index = 0; index < windowWidths.Length; index++)
            {
                var windowWidth = windowWidths[index];
                var catalogVisible = catalogStates[index];
                var workspace = (Rect)calculateWorkspace.Invoke(null, new object[] { windowWidth, 900f });
                var centerWidth = (float)calculateCenterWidth.Invoke(
                    null,
                    new object[] { workspace.width, catalogVisible });
                var occupiedWidth = leftWidth + columnGap + centerWidth;
                if (catalogVisible)
                {
                    occupiedWidth += catalogWidth + columnGap;
                }

                Assert.That(workspace.xMin, Is.EqualTo(outerMargin).Within(0.001f));
                Assert.That(windowWidth - workspace.xMax, Is.EqualTo(outerMargin).Within(0.001f));
                Assert.That(occupiedWidth, Is.EqualTo(workspace.width).Within(0.001f));
                Assert.That(centerWidth, Is.GreaterThanOrEqualTo(previewMinimum));
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

        private static void ConfigureAttackSounds(ScriptableObject draft, AudioClip sound)
        {
            var serialized = new SerializedObject(draft);
            var basicAttack = AssetDatabase.LoadAssetAtPath<MonsterBasicAttackProfile>(
                SingleProjectileBasicAttackPath);
            Assert.That(basicAttack, Is.Not.Null);
            serialized.FindProperty("basicAttackProfile").objectReferenceValue = basicAttack;
            serialized.FindProperty("combatType").enumValueIndex = (int)MonsterCombatType.Ranged;
            serialized.FindProperty("rangedDeliveryMode").enumValueIndex = (int)MonsterRangedDeliveryMode.Projectile;
            serialized.FindProperty("projectileMode").enumValueIndex = (int)MonsterProjectileAttackMode.Single;
            serialized.FindProperty("projectilePrefab").objectReferenceValue = null;
            var attack = serialized.FindProperty("attacks").GetArrayElementAtIndex(0);
            var slot = basicAttack.VfxSlots[0];
            var bindings = serialized.FindProperty("basicAttackVfxBindings");
            bindings.arraySize = 1;
            var binding = bindings.GetArrayElementAtIndex(0);
            binding.FindPropertyRelative("attackId").stringValue = basicAttack.AttackId;
            binding.FindPropertyRelative("slotId").stringValue = slot.SlotId;
            binding.FindPropertyRelative("motionId").stringValue =
                slot.AssignmentScope == MonsterBasicAttackVfxAssignmentScope.MotionSpecific
                    ? attack.FindPropertyRelative("motionId").stringValue
                    : string.Empty;
            binding.FindPropertyRelative("state").enumValueIndex =
                (int)MonsterBasicAttackVfxAssignmentState.Undecided;
            binding.FindPropertyRelative("prefab").objectReferenceValue = null;
            binding.FindPropertyRelative("sfxState").enumValueIndex =
                (int)MonsterBasicAttackSfxAssignmentState.Assigned;
            binding.FindPropertyRelative("sound").objectReferenceValue = sound;
            binding.FindPropertyRelative("soundVolume").floatValue = 0.42f;
            binding.FindPropertyRelative("sfx").objectReferenceValue = null;
            binding.FindPropertyRelative("lifetime").floatValue = slot.DefaultLifetime;
            binding.FindPropertyRelative("playbackOffset").floatValue = 0f;
            binding.FindPropertyRelative("playbackSpeed").floatValue = 1f;
            binding.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
            binding.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
            binding.FindPropertyRelative("scale").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static MonsterDefinition CreateUnrelatedDirtyDefinition(
            string path,
            string unsavedMonsterId,
            out string savedHash)
        {
            var definition = ScriptableObject.CreateInstance<MonsterDefinition>();
            AssetDatabase.CreateAsset(definition, path);
            AssetDatabase.SaveAssetIfDirty(definition);
            savedHash = HashFile(path);

            var serialized = new SerializedObject(definition);
            var monsterId = serialized.FindProperty("monsterId");
            Assert.That(monsterId, Is.Not.Null);
            monsterId.stringValue = unsavedMonsterId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            Assert.That(EditorUtility.IsDirty(definition), Is.True);
            return definition;
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
            AssetDatabase.SaveAssetIfDirty(catalog);
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
