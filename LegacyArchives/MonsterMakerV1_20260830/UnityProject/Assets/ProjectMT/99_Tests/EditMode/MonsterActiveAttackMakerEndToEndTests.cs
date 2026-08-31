using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectMT.Tests.EditMode
{
    public sealed class MonsterActiveAttackMakerEndToEndTests // 실제 Maker Writer 왕복 회귀
    {
        private const string SourceDraftPath =
            "Assets/ProjectMT/99_Tests/QA/ActiveSkills/Draft_QA_ActivePreview.asset";

        [Test, Order(1)]
        public void SkyBreak_IsBuiltTwiceWithStableGuidMotionAndDynamicContracts()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var monsterId = "codex_active_" + suffix;
            var draftPath = $"Assets/ProjectMT/Editor/MonsterMaker/Drafts/Draft_{monsterId}.asset";
            var catalogPath = $"Assets/ProjectMT/99_Tests/QA/ActiveSkills/MonsterCatalog_{suffix}.asset";
            var rarityCatalogPath =
                $"Assets/ProjectMT/99_Tests/QA/ActiveSkills/MonsterRarityCatalog_{suffix}.asset";
            var dataFolder = $"Assets/ProjectMT/02_Shared/Unit/Data/Monsters/{monsterId}";
            var artFolder = $"Assets/ProjectMT/05_Art/Monsters/{monsterId}";
            Cleanup(draftPath, dataFolder, artFolder, catalogPath, rarityCatalogPath);
            ScriptableObject draft = null;
            MonsterCatalog catalog = null;
            MonsterRarityCatalog rarityCatalog = null;
            try
            {
                draft = CreateDraft(monsterId, draftPath);
                catalog = ScriptableObject.CreateInstance<MonsterCatalog>();
                rarityCatalog = ScriptableObject.CreateInstance<MonsterRarityCatalog>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
                AssetDatabase.CreateAsset(rarityCatalog, rarityCatalogPath);
                var serializedRarityCatalog = new SerializedObject(rarityCatalog);
                serializedRarityCatalog.FindProperty("sourceCatalog").objectReferenceValue = catalog;
                serializedRarityCatalog.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();

                var first = InvokeWriter(draft, catalog, rarityCatalog, "first");
                Assert.That(first, Is.Not.Null);
                var activePath = $"{dataFolder}/MSA_{monsterId}_Active.asset";
                var active = AssetDatabase.LoadAssetAtPath<MonsterAttackActiveSkill>(activePath);
                AssertBuiltActive(active, draft);
                var activeGuid = AssetDatabase.AssetPathToGUID(activePath);
                Assert.That(activeGuid, Is.Not.Empty);

                draft = AssetDatabase.LoadMainAssetAtPath(draftPath) as ScriptableObject;
                catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(catalogPath);
                rarityCatalog = AssetDatabase.LoadAssetAtPath<MonsterRarityCatalog>(rarityCatalogPath);
                var second = InvokeWriter(draft, catalog, rarityCatalog, "second");
                Assert.That((bool)second.GetType().GetProperty("UpdatedExisting").GetValue(second), Is.True);
                Assert.That(AssetDatabase.AssetPathToGUID(activePath), Is.EqualTo(activeGuid));
                active = AssetDatabase.LoadAssetAtPath<MonsterAttackActiveSkill>(activePath);
                AssertBuiltActive(active, draft);

                Assert.That(catalog.TryGet(monsterId, out var definition), Is.True);
                Assert.That(definition.DisplayName, Is.EqualTo("천공 분쇄 QA 몬스터"));
                Assert.That(rarityCatalog.TryGetRarity(monsterId, out var rarity), Is.True);
                Assert.That(rarity, Is.EqualTo(MonsterRarity.Legendary));
                var entry = rarityCatalog.LegendaryMythicEntries.Single(item => item.Monster == definition);
                Assert.That(entry.ActiveSkill, Is.SameAs(active));
                Assert.That(entry.TryValidateSkillReferences(out var entryError), Is.True, entryError);

                var motion = AssetDatabase.LoadAssetAtPath<MonsterMotionProfile>(
                    $"{dataFolder}/MM_{monsterId}.asset");
                Assert.That(motion.Active, Is.Not.Null);
                var serializedDraft = new SerializedObject(draft);
                Assert.That(serializedDraft.FindProperty("useCustomActiveStepMotions").boolValue, Is.False);
                var expectedClip = serializedDraft.FindProperty("attacks")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("clip")
                    .objectReferenceValue;
                Assert.That(motion.Active.Clip, Is.SameAs(expectedClip));
                Assert.That(motion.ActiveSteps.Count, Is.EqualTo(3));
                CollectionAssert.AreEqual(
                    new[] { "step_01", "step_02", "step_03" },
                    motion.ActiveSteps.Select(step => step.StepId).ToArray());
                Assert.That(motion.ActiveSteps.All(step => step.Clip == expectedClip), Is.True);
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    $"{artFolder}/AC_{monsterId}.controller");
                Assert.That(controller, Is.Not.Null);
                var controllerStates = controller.layers[0].stateMachine.states
                    .Select(child => child.state)
                    .ToArray();
                var activeState = controllerStates
                    .Single(state => state.name == MonsterMotionProfile.ActiveStateName);
                Assert.That(activeState.motion, Is.SameAs(expectedClip));
                foreach (var stepMotion in motion.ActiveSteps)
                {
                    var stepState = controllerStates.Single(state => state.name == stepMotion.StateName);
                    Assert.That(stepState.motion, Is.SameAs(stepMotion.Clip));
                }

                var baselineHealth = definition.MaxHealth;
                var baselineAction = definition.RuntimeAssetSet.CombatProfile.Action;
                var baselineBasicAttack = baselineAction.BasicAttackProfile;
                var activeOnlyDraft = new SerializedObject(draft);
                activeOnlyDraft.FindProperty("activeSkillName").stringValue = "천공 분쇄 액티브 전용 갱신";
                activeOnlyDraft.FindProperty("activeEnergyMaximum").intValue = 777;
                activeOnlyDraft.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(draft);

                var writerType = FindEditorType(
                    "ProjectMT.EditorTools.MonsterMaker.MonsterMakerAssetWriter");
                var synchronized = writerType.GetMethod(
                        "SynchronizeActiveAttackRuntime",
                        BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { draft, catalog, rarityCatalog }) as MonsterAttackActiveSkill;
                Assert.That(synchronized, Is.SameAs(active),
                    "액티브 전용 반영은 기존 Runtime 자산의 GUID를 유지해야 합니다.");
                Assert.That(AssetDatabase.AssetPathToGUID(activePath), Is.EqualTo(activeGuid));
                Assert.That(synchronized.DisplayName, Is.EqualTo("천공 분쇄 액티브 전용 갱신"));
                Assert.That(synchronized.EnergyCost, Is.EqualTo(777f).Within(0.001f));
                Assert.That(definition.MaxHealth, Is.EqualTo(baselineHealth),
                    "액티브 전용 반영이 몬스터 스탯을 덮어쓰면 안 됩니다.");
                Assert.That(definition.RuntimeAssetSet.CombatProfile.Action, Is.SameAs(baselineAction));
                Assert.That(definition.RuntimeAssetSet.CombatProfile.Action.BasicAttackProfile,
                    Is.SameAs(baselineBasicAttack),
                    "액티브 전용 반영이 기본공격 연결을 바꾸면 안 됩니다.");

                var projectionType = FindEditorType(
                    "ProjectMT.EditorTools.MonsterMaker.MonsterActiveAttackBindingProjection");
                var projectionArguments = new object[] { draft, synchronized, motion, null };
                var projectionState = projectionType.GetMethod(
                        "EvaluateRuntimeSync",
                        BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, projectionArguments);
                Assert.That(projectionState.ToString(), Is.EqualTo("Synchronized"),
                    projectionArguments[3] as string);
            }
            finally
            {
                Cleanup(draftPath, dataFolder, artFolder, catalogPath, rarityCatalogPath);
                if (catalog != null && !EditorUtility.IsPersistent(catalog)) UnityEngine.Object.DestroyImmediate(catalog);
                if (rarityCatalog != null && !EditorUtility.IsPersistent(rarityCatalog)) UnityEngine.Object.DestroyImmediate(rarityCatalog);
            }

            Assert.That(AssetDatabase.LoadMainAssetAtPath(draftPath), Is.Null);
            Assert.That(AssetDatabase.IsValidFolder(dataFolder), Is.False);
            Assert.That(AssetDatabase.IsValidFolder(artFolder), Is.False);
        }

        [Test, Order(2)]
        public void Workshop_OpensFormalPresetAndCompletesStandardOneTargetPreview()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var profile = AssetDatabase.LoadAssetAtPath<MonsterActiveAttackProfile>(
                "Assets/ProjectMT/02_Shared/Unit/Data/ActiveAttackProfiles/AAP_SkyBreak.asset");
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.TryValidate(out var profileError), Is.True, profileError);
            var sceneWasDirty = EditorSceneManager.GetActiveScene().isDirty;
            var previewType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterActiveAttackAuthoringPreview");
            var preview = Activator.CreateInstance(previewType, true);
            try
            {
                previewType.GetMethod("SetProfile", flags).Invoke(preview, new object[] { profile });
                var targets = previewType.GetField("targets", flags).GetValue(preview) as ICollection;
                Assert.That(targets, Is.Not.Null);
                Assert.That(targets.Count, Is.EqualTo(1), "독립 Preview는 기본공격 조립소처럼 표준 대상 1명만 사용합니다.");
                previewType.GetMethod("PlayAll", flags).Invoke(preview, null);
                Assert.That((bool)previewType.GetProperty("IsPlaying").GetValue(preview), Is.True);
                previewType.GetField("playbackStartedAt", flags)
                    .SetValue(preview, EditorApplication.timeSinceStartup - 100d);
                previewType.GetMethod("Tick", flags).Invoke(preview, null);
                Assert.That((bool)previewType.GetProperty("IsPlaying").GetValue(preview), Is.False);
                Assert.That((string)previewType.GetProperty("Status").GetValue(preview),
                    Is.EqualTo("재생 완료 · 3 Step"));
            }
            finally
            {
                previewType.GetMethod("Dispose", flags)?.Invoke(preview, null);
            }

            var windowType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterActiveAttackWorkshopWindow");
            var window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            var windowInstanceId = window.GetInstanceID();
            try
            {
                window.position = new Rect(70f, 70f, 1100f, 700f);
                windowType.GetMethod("SetProfile", flags).Invoke(window, new object[] { profile });
                window.ShowUtility();
                window.SendEvent(new Event { type = EventType.Layout });
                window.SendEvent(new Event { type = EventType.Repaint });
                var profiles = windowType.GetField("profiles", flags).GetValue(window) as ICollection;
                Assert.That(profiles, Is.Not.Null);
                Assert.That(profiles.Cast<object>().Contains(profile), Is.True,
                    "정식 천공 분쇄 프리셋은 좌측 저장 목록에 표시되어야 합니다.");
                Assert.That(window.minSize, Is.EqualTo(new Vector2(1100f, 700f)));
                var contentRect = (Rect)windowType.GetField("lastAssemblerContentRect", flags).GetValue(window);
                var viewportRect = (Rect)windowType.GetField("lastAssemblerViewportRect", flags).GetValue(window);
                var rightmostRect = (Rect)windowType.GetField("lastStepHeaderRightmostRect", flags).GetValue(window);
                Assert.That(contentRect.width, Is.EqualTo(450f).Within(0.1f),
                    "최소 창 폭에서도 중앙 스크롤 콘텐츠는 실제 편집 폭 450 안에 고정되어야 합니다.");
                Assert.That(viewportRect.width, Is.GreaterThanOrEqualTo(contentRect.width),
                    "중앙 콘텐츠가 세로 스크롤의 표시 폭보다 넓어지면 안 됩니다.");
                Assert.That(rightmostRect.width, Is.GreaterThan(0f), "Step 우측 조작 버튼이 실제로 그려져야 합니다.");
                Assert.That(rightmostRect.xMax, Is.LessThanOrEqualTo(contentRect.width + 0.1f),
                    "Step 우측 조작 버튼이 중앙 편집 영역을 넘어 미리보기 열을 침범하면 안 됩니다.");
            }
            finally
            {
                MonsterEditorWindowTestUtility.Close(window);
            }
            Assert.That(Resources.FindObjectsOfTypeAll<EditorWindow>()
                .Any(candidate => candidate.GetInstanceID() == windowInstanceId), Is.False,
                "조립소 QA 뒤 임시 EditorWindow가 남으면 안 됩니다.");
            MonsterEditorWindowTestUtility.AssertNoOrphanedContainers("공격 액티브 조립소");
            Assert.That(EditorSceneManager.GetActiveScene().isDirty, Is.EqualTo(sceneWasDirty));
        }

        [Test, Order(3)]
        public void Workshop_UsesIsolatedWorkCopyThenSavesAndAssignsLikeBasicAttack()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            const string customRoot =
                "Assets/ProjectMT/02_Shared/Unit/Data/ActiveAttackProfiles/Custom";
            var source = AssetDatabase.LoadAssetAtPath<MonsterActiveAttackProfile>(
                "Assets/ProjectMT/02_Shared/Unit/Data/ActiveAttackProfiles/AAP_SkyBreak.asset");
            Assert.That(source, Is.Not.Null);
            var sourceName = source.DisplayName;
            var customFolderExisted = AssetDatabase.IsValidFolder(customRoot);
            var windowType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterActiveAttackWorkshopWindow");
            var window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            var windowInstanceId = window.GetInstanceID();
            ScriptableObject draft = null;
            string createdPath = null;
            try
            {
                window.ShowUtility();
                draft = UnityEngine.Object.Instantiate(
                    AssetDatabase.LoadMainAssetAtPath(SourceDraftPath) as ScriptableObject);
                windowType.GetMethod("SetProfile", flags).Invoke(window, new object[] { source });
                var working = windowType.GetField("profile", flags).GetValue(window) as MonsterActiveAttackProfile;
                Assert.That(working, Is.Not.Null);
                Assert.That(working, Is.Not.SameAs(source));
                Assert.That(EditorUtility.IsPersistent(working), Is.False,
                    "저장 프리셋을 선택해도 실제 자산이 아니라 작업 사본을 편집해야 합니다.");
                Assert.That(working.hideFlags & HideFlags.NotEditable, Is.EqualTo(HideFlags.None),
                    "작업 사본은 저장되지 않아야 하지만 조립소에서 직접 편집할 수 있어야 합니다.");
                Assert.That(working.hideFlags & HideFlags.DontSave, Is.EqualTo(HideFlags.DontSave));
                Assert.That(windowType.GetField("loadedProfile", flags).GetValue(window), Is.SameAs(source));
                Assert.That((bool)windowType.GetField("workCopyDirty", flags).GetValue(window), Is.False);
                Assert.That(window.hasUnsavedChanges, Is.False);

                var serializedWorking = new SerializedObject(working);
                serializedWorking.FindProperty("displayName").stringValue = "작업 사본 이름";
                serializedWorking.ApplyModifiedPropertiesWithoutUndo();
                windowType.GetMethod("OnWorkingProfileChanged", flags).Invoke(window, null);
                Assert.That(source.DisplayName, Is.EqualTo(sourceName),
                    "작업 사본 편집은 저장 전 공식 프리셋을 바꾸면 안 됩니다.");
                Assert.That((bool)windowType.GetField("workCopyDirty", flags).GetValue(window), Is.True);
                Assert.That(window.hasUnsavedChanges, Is.True,
                    "실제 편집이 시작되면 창 닫기와 프리셋 전환을 보호하는 미저장 상태가 켜져야 합니다.");

                windowType.GetMethod("ForkLoadedAsNew", flags).Invoke(window, null);
                Assert.That(windowType.GetField("loadedProfile", flags).GetValue(window), Is.Null);
                Assert.That(working.ProfileId, Does.StartWith("active_custom_"));
                createdPath = $"{customRoot}/AAP_{working.ProfileId}.asset";
                AssetDatabase.DeleteAsset(createdPath);

                windowType.GetMethod("SaveAsNew", flags).Invoke(window, null);
                var saved = AssetDatabase.LoadAssetAtPath<MonsterActiveAttackProfile>(createdPath);
                Assert.That(saved, Is.Not.Null);
                Assert.That(saved.TryValidate(out var saveError), Is.True, saveError);
                Assert.That(windowType.GetField("loadedProfile", flags).GetValue(window), Is.SameAs(saved));
                Assert.That((bool)windowType.GetField("workCopyDirty", flags).GetValue(window), Is.False);
                Assert.That(window.hasUnsavedChanges, Is.False,
                    "저장이 끝나면 Unity 창의 미저장 상태도 함께 해제되어야 합니다.");

                windowType.GetField("originDraft", flags).SetValue(window, draft);
                windowType.GetMethod("AssignLoadedToOrigin", flags).Invoke(window, null);
                var assigned = new SerializedObject(draft)
                    .FindProperty("activeAttackProfile")
                    .objectReferenceValue;
                Assert.That(assigned, Is.SameAs(saved));
            }
            finally
            {
                MonsterEditorWindowTestUtility.Close(window);
                if (draft != null) UnityEngine.Object.DestroyImmediate(draft);
                if (!string.IsNullOrWhiteSpace(createdPath)) AssetDatabase.DeleteAsset(createdPath);
                if (!customFolderExisted && AssetDatabase.IsValidFolder(customRoot) &&
                    AssetDatabase.FindAssets(string.Empty, new[] { customRoot }).Length == 0)
                {
                    AssetDatabase.DeleteAsset(customRoot);
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            Assert.That(Resources.FindObjectsOfTypeAll<EditorWindow>()
                .Any(candidate => candidate.GetInstanceID() == windowInstanceId), Is.False,
                "저장·배정 QA 뒤 임시 EditorWindow가 남으면 안 됩니다.");
            Assert.That(source.DisplayName, Is.EqualTo(sourceName));
        }

        [Test, Order(4)]
        public void MonsterMaker_QaDraftPlaysActiveSkillThroughSharedPreview()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var source = AssetDatabase.LoadMainAssetAtPath(SourceDraftPath) as ScriptableObject;
            Assert.That(source, Is.Not.Null);
            var draft = UnityEngine.Object.Instantiate(source);
            MonsterActiveAttackProfile previewProfile = null;
            GameObject loopVfxPrefab = null;
            var loopVfxScene = default(UnityEngine.SceneManagement.Scene);
            var serialized = new SerializedObject(draft);
            previewProfile = UnityEngine.Object.Instantiate(
                serialized.FindProperty("activeAttackProfile").objectReferenceValue as MonsterActiveAttackProfile);
            var serializedProfile = new SerializedObject(previewProfile);
            var firstContract = serializedProfile.FindProperty("steps")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("presentationSlots")
                .GetArrayElementAtIndex(0);
            firstContract.FindPropertyRelative("useDuration").boolValue = true;
            firstContract.FindPropertyRelative("duration").floatValue = 12.34f;
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();
            serialized.FindProperty("activeAttackProfile").objectReferenceValue = previewProfile;
            serialized.FindProperty("activeStepMotionModeConfigured").boolValue = true;
            serialized.FindProperty("useCustomActiveStepMotions").boolValue = false;
            var presentations = serialized.FindProperty("activeAttackPresentations");
            for (var index = 0; index < presentations.arraySize; index++)
            {
                var presentation = presentations.GetArrayElementAtIndex(index);
                presentation.FindPropertyRelative("motionConfigured").boolValue = true;
                presentation.FindPropertyRelative("motionClip").objectReferenceValue = null;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            draft.GetType().GetMethod("EditorSyncActiveAttackAuthoring", flags).Invoke(draft, null);
            loopVfxScene = EditorSceneManager.NewPreviewScene();
            loopVfxPrefab = new GameObject("QA_LoopVfx");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(loopVfxPrefab, loopVfxScene);
            var synchronized = new SerializedObject(draft);
           var feedback = synchronized.FindProperty("activeAttackPresentations")
               .GetArrayElementAtIndex(0)
               .FindPropertyRelative("slots")
               .GetArrayElementAtIndex(0)
               .FindPropertyRelative("feedback");
            var firstPreviewSlot = synchronized.FindProperty("activeAttackPresentations")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("slots")
                .GetArrayElementAtIndex(0);
            firstPreviewSlot.FindPropertyRelative("assignmentStateConfigured").boolValue = true;
            firstPreviewSlot.FindPropertyRelative("vfxState").enumValueIndex =
                (int)MonsterBasicAttackVfxAssignmentState.Assigned;
            feedback.FindPropertyRelative("vfxPrefab").objectReferenceValue = loopVfxPrefab;
            feedback.FindPropertyRelative("vfxLifetime").floatValue = 0.02f;
            synchronized.ApplyModifiedPropertiesWithoutUndo();
            var sceneWasDirty = EditorSceneManager.GetActiveScene().isDirty;
            var windowType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerWindow");
            var window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            try
            {
                window.position = new Rect(40f, 40f, 1600f, 900f);
                windowType.GetMethod("SetDraft", flags).Invoke(window, new object[] { draft, false });
                window.ShowUtility();
                window.SendEvent(new Event { type = EventType.Layout });
                window.SendEvent(new Event { type = EventType.Repaint });
                Assert.That((bool)windowType.GetField("showAdvancedActiveStepMotions", flags).GetValue(window), Is.False,
                    "선택형 액티브 모션은 기본적으로 닫힌 고급 기능이어야 합니다.");
                var preview = windowType.GetField("preview", flags).GetValue(window);
                var previewType = preview.GetType();
                Assert.That((bool)previewType.GetProperty("CanPlayActiveSkill").GetValue(preview), Is.True);
                Assert.That((bool)previewType.GetProperty("HasCombatTarget").GetValue(preview), Is.True);
                previewType.GetMethod("PlayActiveSkill", flags).Invoke(preview, null);
                var pendingEvents = previewType.GetField("pendingActiveEvents", flags).GetValue(preview) as ICollection;
                Assert.That(pendingEvents, Is.Not.Null);
                Assert.That(pendingEvents.Count, Is.EqualTo(12), "3개 스텝은 모션·예고·발동·타격 이벤트를 각각 가져야 합니다.");
                Assert.That(
                    pendingEvents.Cast<object>().Count(item =>
                        string.Equals(item.GetType().GetProperty("Type").GetValue(item).ToString(), "Motion")),
                    Is.EqualTo(3),
                    "액티브 스텝마다 기본 또는 전용 모션 재생 이벤트가 하나씩 필요합니다.");
                var tick = previewType.GetMethod(
                    "Tick", flags, null, new[] { typeof(float) }, null);
                tick.Invoke(preview, new object[] { 0.01f });
                var activeVfx = previewType.GetField("activeVfx", flags).GetValue(preview) as ICollection;
                Assert.That(activeVfx, Is.Not.Null);
                Assert.That(activeVfx.Count, Is.GreaterThan(0));
                Assert.That(activeVfx.Cast<object>().Any(item =>
                        Mathf.Abs((float)item.GetType().GetProperty("Lifetime").GetValue(item) - 12.34f) < 0.001f),
                    Is.True,
                    "Monster Maker Preview도 공간 계약의 VFX 지속시간을 사용해야 합니다.");
                for (var index = 0; index < 220; index++)
                    tick.Invoke(preview, new object[] { 0.08f });

                Assert.That((int)previewType.GetProperty("PreviewHitCount").GetValue(preview),
                    Is.GreaterThanOrEqualTo(3));
                Assert.That((float)previewType.GetProperty("LastAppliedDamage").GetValue(preview),
                    Is.GreaterThan(0f));
                Assert.That((string)previewType.GetProperty("CombatStatus").GetValue(preview),
                    Does.StartWith("액티브 완료"));
            }
            finally
            {
                MonsterEditorWindowTestUtility.Close(window);
                if (loopVfxScene.IsValid()) EditorSceneManager.ClosePreviewScene(loopVfxScene);
                if (previewProfile != null) UnityEngine.Object.DestroyImmediate(previewProfile);
                UnityEngine.Object.DestroyImmediate(draft);
            }
            Assert.That(EditorSceneManager.GetActiveScene().isDirty, Is.EqualTo(sceneWasDirty));
        }

        [Test, Order(5)]
        public void LegacyActiveClip_MigratesToEnabledPerStepAdvancedMotion()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var source = AssetDatabase.LoadMainAssetAtPath(SourceDraftPath) as ScriptableObject;
            Assert.That(source, Is.Not.Null);
            var draft = UnityEngine.Object.Instantiate(source);
            try
            {
                var serialized = new SerializedObject(draft);
                serialized.FindProperty("activeStepMotionModeConfigured").boolValue = false;
                serialized.FindProperty("useCustomActiveStepMotions").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                draft.GetType().GetMethod("EditorSyncActiveAttackAuthoring", flags).Invoke(draft, null);
                serialized.UpdateIfRequiredOrScript();
                Assert.That(serialized.FindProperty("activeStepMotionModeConfigured").boolValue, Is.True);
                Assert.That(serialized.FindProperty("useCustomActiveStepMotions").boolValue, Is.True,
                    "기존 전용 액티브 Clip이 있는 Draft는 사용자 의도를 보존해 고급 옵션을 켜야 합니다.");
                var expectedClip = serialized.FindProperty("activeSkillClip").objectReferenceValue;
                var presentations = serialized.FindProperty("activeAttackPresentations");
                Assert.That(presentations.arraySize, Is.EqualTo(3));
                for (var index = 0; index < presentations.arraySize; index++)
                {
                    Assert.That(
                        presentations.GetArrayElementAtIndex(index)
                            .FindPropertyRelative("motionClip")
                            .objectReferenceValue,
                        Is.SameAs(expectedClip));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test, Order(6)]
        public void EveryOfficialPreset_StaysInsideMinimumWorkshopWidth()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var windowType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterActiveAttackWorkshopWindow");
            var paths = AssetDatabase.FindAssets(
                    "t:MonsterActiveAttackProfile",
                    new[] { "Assets/ProjectMT/02_Shared/Unit/Data/ActiveAttackProfiles" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            Assert.That(paths.Length, Is.GreaterThanOrEqualTo(4));

            var window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            try
            {
                window.position = new Rect(70f, 70f, 1100f, 700f);
                window.ShowUtility();
                foreach (var path in paths)
                {
                    var preset = AssetDatabase.LoadAssetAtPath<MonsterActiveAttackProfile>(path);
                    windowType.GetMethod("SetProfile", flags).Invoke(window, new object[] { preset });
                    var serialized = (SerializedObject)windowType.GetField("serializedProfile", flags)
                        .GetValue(window);
                    serialized.Update();
                    serialized.FindProperty("description").stringValue = new string('가', 240);
                    var steps = serialized.FindProperty("steps");
                    for (var stepIndex = 0; stepIndex < steps.arraySize; stepIndex++)
                    {
                        var step = steps.GetArrayElementAtIndex(stepIndex);
                        step.isExpanded = true;
                        if (stepIndex == 0)
                        {
                            step.FindPropertyRelative("displayName").stringValue = new string('나', 120);
                        }
                        var slots = step.FindPropertyRelative("presentationSlots");
                        for (var slotIndex = 0; slotIndex < slots.arraySize; slotIndex++)
                        {
                            slots.GetArrayElementAtIndex(slotIndex).isExpanded = true;
                            if (stepIndex == 0 && slotIndex == 0)
                            {
                                var slot = slots.GetArrayElementAtIndex(slotIndex);
                                slot.FindPropertyRelative("displayName").stringValue = new string('다', 120);
                                slot.FindPropertyRelative("useDuration").boolValue = true;
                                slot.FindPropertyRelative("duration").floatValue = 12.34f;
                            }
                        }
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();

                    window.SendEvent(new Event { type = EventType.Layout });
                    window.SendEvent(new Event { type = EventType.Repaint });

                    Rect ReadRect(string field) => (Rect)windowType.GetField(field, flags).GetValue(window);
                    var content = ReadRect("lastAssemblerContentRect");
                    var viewport = ReadRect("lastAssemblerViewportRect");
                    Assert.That(content.width, Is.EqualTo(450f).Within(0.1f), path);
                    Assert.That(viewport.width, Is.GreaterThanOrEqualTo(content.width), path);
                    Assert.That(((Vector2)windowType.GetField("assemblerScroll", flags).GetValue(window)).x,
                        Is.Zero, path + " / 중앙 가로 스크롤");
                    Assert.That(((Vector2)windowType.GetField("libraryScroll", flags).GetValue(window)).x,
                        Is.Zero, path + " / 목록 가로 스크롤");

                    foreach (var field in new[]
                             {
                                 "lastStepHeaderRightmostRect",
                                 "lastDelayRowRightmostRect",
                                 "lastPresentationHeaderRightmostRect",
                                 "lastHitEffectRightmostRect"
                             })
                    {
                        var rect = ReadRect(field);
                        if (rect.width > 0f)
                        {
                            Assert.That(rect.xMax, Is.LessThanOrEqualTo(content.width + 0.1f),
                                path + " / " + field);
                        }
                    }

                    var panel = ReadRect("lastAssemblerPanelRect");
                    var save = ReadRect("lastSaveRightmostRect");
                    var preview = ReadRect("lastPreviewColumnRect");
                    var previewAction = ReadRect("lastPreviewToolbarRightmostRect");
                    Assert.That(save.xMax, Is.LessThanOrEqualTo(panel.xMax + 0.1f), path + " / 저장 버튼");
                    Assert.That(preview.xMax, Is.LessThanOrEqualTo(window.position.width + 0.1f),
                        path + " / 미리보기 열");
                    Assert.That(previewAction.xMax, Is.LessThanOrEqualTo(preview.xMax + 0.1f),
                        path + " / Step 재생 버튼");
                }
            }
            finally
            {
                MonsterEditorWindowTestUtility.Close(window);
            }
            MonsterEditorWindowTestUtility.AssertNoOrphanedContainers("공격 액티브 조립소");
        }

        [Test, Order(7)]
        public void Workshop_AllStructuralButtonHandlersCommitImmediately()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var source = AssetDatabase.LoadAssetAtPath<MonsterActiveAttackProfile>(
                "Assets/ProjectMT/02_Shared/Unit/Data/ActiveAttackProfiles/AAP_GaleDance.asset");
            Assert.That(source, Is.Not.Null);
            var windowType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterActiveAttackWorkshopWindow");
            var window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            try
            {
                window.ShowUtility();
                windowType.GetMethod("SetProfile", flags).Invoke(window, new object[] { source });
                var working = windowType.GetField("profile", flags).GetValue(window) as MonsterActiveAttackProfile;
                var serialized = windowType.GetField("serializedProfile", flags).GetValue(window) as SerializedObject;
                Assert.That(working, Is.Not.Null);
                Assert.That(serialized, Is.Not.Null);

                void Invoke(string methodName, params object[] arguments)
                {
                    var method = windowType.GetMethod(methodName, flags);
                    Assert.That(method, Is.Not.Null, methodName + " 버튼 처리 함수");
                    method.Invoke(window, arguments);
                }
                SerializedProperty Steps()
                {
                    serialized.UpdateIfRequiredOrScript();
                    return serialized.FindProperty("steps");
                }
                SerializedProperty Slots()
                {
                    return Steps().GetArrayElementAtIndex(0).FindPropertyRelative("presentationSlots");
                }
                SerializedProperty Effects()
                {
                    return Steps().GetArrayElementAtIndex(0).FindPropertyRelative("hitEffects");
                }

                var initialStepCount = working.Steps.Count;
                Invoke("AddStepAndCommit", Steps(), MonsterActiveAttackPattern.Line);
                Assert.That(working.Steps.Count, Is.EqualTo(initialStepCount + 1), "Step 추가");
                Assert.That((bool)windowType.GetField("workCopyDirty", flags).GetValue(window), Is.True,
                    "구조 버튼은 즉시 미저장 상태를 켜야 합니다.");

                var firstStepTitle = working.Steps[0].DisplayName;
                Invoke("DuplicateStepAndCommit", Steps(), 0, firstStepTitle);
                Assert.That(working.Steps.Count, Is.EqualTo(initialStepCount + 2), "Step 복제");
                Assert.That(working.Steps.Select(step => step.StepId).Distinct().Count(),
                    Is.EqualTo(working.Steps.Count), "복제 Step ID 고유성");

                var firstStepId = working.Steps[0].StepId;
                Invoke("MoveStepAndCommit", Steps(), 0, 1);
                Assert.That(working.Steps[1].StepId, Is.EqualTo(firstStepId), "Step 아래 이동");
                Invoke("MoveStepAndCommit", Steps(), 1, 0);
                Assert.That(working.Steps[0].StepId, Is.EqualTo(firstStepId), "Step 위 이동");
                Invoke("DeleteStepAndCommit", Steps(), 1);
                Assert.That(working.Steps.Count, Is.EqualTo(initialStepCount + 1), "Step 삭제");

                var initialSlotCount = working.Steps[0].PresentationSlots.Count;
                Invoke("AddPresentationSlotAndCommit", Slots(), MonsterActivePresentationEvent.TeleportExit);
                Assert.That(working.Steps[0].PresentationSlots.Count, Is.EqualTo(initialSlotCount + 1),
                    "VFX 공간 추가");

                var firstSlotTitle = working.Steps[0].PresentationSlots[0].DisplayName;
                Invoke("DuplicatePresentationSlotAndCommit", Slots(), 0, firstSlotTitle);
                Assert.That(working.Steps[0].PresentationSlots.Count, Is.EqualTo(initialSlotCount + 2),
                    "VFX 공간 복제");
                Assert.That(working.Steps[0].PresentationSlots.Select(slot => slot.SlotId).Distinct().Count(),
                    Is.EqualTo(working.Steps[0].PresentationSlots.Count), "복제 VFX 공간 ID 고유성");

                var firstSlotId = working.Steps[0].PresentationSlots[0].SlotId;
                Invoke("MovePresentationSlotAndCommit", Slots(), 0, 1);
                Assert.That(working.Steps[0].PresentationSlots[1].SlotId, Is.EqualTo(firstSlotId),
                    "VFX 공간 아래 이동");
                Invoke("MovePresentationSlotAndCommit", Slots(), 1, 0);
                Assert.That(working.Steps[0].PresentationSlots[0].SlotId, Is.EqualTo(firstSlotId),
                    "VFX 공간 위 이동");
                Invoke("DeletePresentationSlotAndCommit", Slots(), 1);
                Assert.That(working.Steps[0].PresentationSlots.Count, Is.EqualTo(initialSlotCount + 1),
                    "VFX 공간 삭제");

                var initialEffectCount = working.Steps[0].HitEffects.Count;
                Invoke("AddEffectAndCommit", Effects(), MonsterActiveHitEffectType.Pull);
                Assert.That(working.Steps[0].HitEffects.Count, Is.EqualTo(initialEffectCount + 1),
                    "타격 효과 추가");
                Assert.That(working.Steps[0].HitEffects.Last().Type, Is.EqualTo(MonsterActiveHitEffectType.Pull));
                Invoke("DeleteEffectAndCommit", Effects(), working.Steps[0].HitEffects.Count - 1);
                Assert.That(working.Steps[0].HitEffects.Count, Is.EqualTo(initialEffectCount),
                    "타격 효과 삭제");
            }
            finally
            {
                MonsterEditorWindowTestUtility.Close(window);
            }
            MonsterEditorWindowTestUtility.AssertNoOrphanedContainers("공격 액티브 조립소");
        }

        [Test, Order(8)]
        public void Draft_ProfileSwitch_RestoresArchivedTuningMotionAndSlotDecisions()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var source = AssetDatabase.LoadMainAssetAtPath(SourceDraftPath) as ScriptableObject;
            var firstProfile = AssetDatabase.LoadAssetAtPath<MonsterActiveAttackProfile>(
                "Assets/ProjectMT/02_Shared/Unit/Data/ActiveAttackProfiles/AAP_GaleDance.asset");
            var secondProfile = AssetDatabase.LoadAssetAtPath<MonsterActiveAttackProfile>(
                "Assets/ProjectMT/02_Shared/Unit/Data/ActiveAttackProfiles/AAP_SkyBreak.asset");
            Assert.That(source, Is.Not.Null);
            Assert.That(firstProfile, Is.Not.Null);
            Assert.That(secondProfile, Is.Not.Null);
            var draft = UnityEngine.Object.Instantiate(source);
            try
            {
                var setProfile = draft.GetType().GetMethod("EditorSetActiveAttackProfile", flags);
                Assert.That(setProfile, Is.Not.Null);
                setProfile.Invoke(draft, new object[] { firstProfile });

                var serialized = new SerializedObject(draft);
                var tuning = serialized.FindProperty("activeAttackStepTunings").GetArrayElementAtIndex(0);
                tuning.FindPropertyRelative("damageScale").floatValue = 2.375f;
                var presentation = serialized.FindProperty("activeAttackPresentations").GetArrayElementAtIndex(0);
                presentation.FindPropertyRelative("motionPlaybackSpeed").floatValue = 1.625f;
                var slot = presentation.FindPropertyRelative("slots").GetArrayElementAtIndex(0);
                slot.FindPropertyRelative("assignmentStateConfigured").boolValue = true;
                slot.FindPropertyRelative("vfxState").enumValueIndex = 2;
                slot.FindPropertyRelative("sfxState").enumValueIndex = 2;
                slot.FindPropertyRelative("feedback").FindPropertyRelative("vfxLifetime").floatValue = 7.25f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                setProfile.Invoke(draft, new object[] { secondProfile });
                Assert.That((int)draft.GetType().GetProperty("InactiveActiveAttackAuthoringCount")
                    .GetValue(draft), Is.GreaterThan(0), "전환한 프리셋의 입력값이 보관되어야 합니다.");
                setProfile.Invoke(draft, new object[] { firstProfile });

                serialized = new SerializedObject(draft);
                tuning = serialized.FindProperty("activeAttackStepTunings").GetArrayElementAtIndex(0);
                presentation = serialized.FindProperty("activeAttackPresentations").GetArrayElementAtIndex(0);
                slot = presentation.FindPropertyRelative("slots").GetArrayElementAtIndex(0);
                Assert.That(tuning.FindPropertyRelative("damageScale").floatValue,
                    Is.EqualTo(2.375f).Within(0.0001f));
                Assert.That(presentation.FindPropertyRelative("motionPlaybackSpeed").floatValue,
                    Is.EqualTo(1.625f).Within(0.0001f));
                Assert.That(slot.FindPropertyRelative("vfxState").enumValueIndex, Is.EqualTo(2));
                Assert.That(slot.FindPropertyRelative("sfxState").enumValueIndex, Is.EqualTo(2));
                Assert.That(slot.FindPropertyRelative("feedback").FindPropertyRelative("vfxLifetime").floatValue,
                    Is.EqualTo(7.25f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test, Order(9)]
        public void ActiveValidation_RequiresExplicitThreeStateDecisionsForVfxAndSfx()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var source = AssetDatabase.LoadMainAssetAtPath(SourceDraftPath) as ScriptableObject;
            Assert.That(source, Is.Not.Null);
            var draft = UnityEngine.Object.Instantiate(source);
            try
            {
                draft.GetType().GetMethod("EditorSyncActiveAttackAuthoring", flags).Invoke(draft, null);
                var serialized = new SerializedObject(draft);
                serialized.FindProperty("skillLoadoutConfigured").boolValue = true;
                serialized.FindProperty("rarity").enumValueIndex = (int)MonsterRarity.Legendary;
                var presentations = serialized.FindProperty("activeAttackPresentations");
                for (var presentationIndex = 0; presentationIndex < presentations.arraySize; presentationIndex++)
                {
                    var slots = presentations.GetArrayElementAtIndex(presentationIndex)
                        .FindPropertyRelative("slots");
                    for (var slotIndex = 0; slotIndex < slots.arraySize; slotIndex++)
                    {
                        var candidate = slots.GetArrayElementAtIndex(slotIndex);
                        candidate.FindPropertyRelative("assignmentStateConfigured").boolValue = true;
                        candidate.FindPropertyRelative("vfxState").enumValueIndex = 2;
                        candidate.FindPropertyRelative("sfxState").enumValueIndex = 2;
                    }
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var firstSlot = serialized.FindProperty("activeAttackPresentations")
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("slots")
                    .GetArrayElementAtIndex(0);
                var validator = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerValidator");
                var validate = validator.GetMethod("ValidateActiveAttack", BindingFlags.Public | BindingFlags.Static);
                string[] Codes()
                {
                    var report = validate.Invoke(null, new object[] { draft });
                    var issues = report.GetType().GetProperty("Issues").GetValue(report) as IEnumerable;
                    return issues.Cast<object>()
                        .Select(issue => (string)issue.GetType().GetProperty("Code").GetValue(issue))
                        .ToArray();
                }

                Assert.That(Codes(), Does.Not.Contain("MAKER-ACTIVE-VFX-PENDING"));
                Assert.That(Codes(), Does.Not.Contain("MAKER-ACTIVE-SFX-PENDING"));

                firstSlot.FindPropertyRelative("vfxState").enumValueIndex = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(Codes(), Does.Contain("MAKER-ACTIVE-VFX-PENDING"));

                serialized.UpdateIfRequiredOrScript();
                firstSlot = serialized.FindProperty("activeAttackPresentations")
                    .GetArrayElementAtIndex(0).FindPropertyRelative("slots").GetArrayElementAtIndex(0);
                firstSlot.FindPropertyRelative("vfxState").enumValueIndex = 1;
                firstSlot.FindPropertyRelative("feedback").FindPropertyRelative("vfxPrefab")
                    .objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var assignedVfxCodes = Codes();
                Assert.That(assignedVfxCodes, Does.Not.Contain("MAKER-ACTIVE-VFX-PENDING"));
                Assert.That(assignedVfxCodes, Does.Contain("MAKER-ACTIVE-VFX-MISSING"));

                serialized.UpdateIfRequiredOrScript();
                firstSlot = serialized.FindProperty("activeAttackPresentations")
                    .GetArrayElementAtIndex(0).FindPropertyRelative("slots").GetArrayElementAtIndex(0);
                firstSlot.FindPropertyRelative("vfxState").enumValueIndex = 2;
                firstSlot.FindPropertyRelative("sfxState").enumValueIndex = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var pendingSfxCodes = Codes();
                Assert.That(pendingSfxCodes, Does.Not.Contain("MAKER-ACTIVE-VFX-MISSING"));
                Assert.That(pendingSfxCodes, Does.Contain("MAKER-ACTIVE-SFX-PENDING"));

                serialized.UpdateIfRequiredOrScript();
                firstSlot = serialized.FindProperty("activeAttackPresentations")
                    .GetArrayElementAtIndex(0).FindPropertyRelative("slots").GetArrayElementAtIndex(0);
                firstSlot.FindPropertyRelative("sfxState").enumValueIndex = 1;
                var feedback = firstSlot.FindPropertyRelative("feedback");
                feedback.FindPropertyRelative("sound").objectReferenceValue = null;
                feedback.FindPropertyRelative("sfx").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var assignedSfxCodes = Codes();
                Assert.That(assignedSfxCodes, Does.Not.Contain("MAKER-ACTIVE-SFX-PENDING"));
                Assert.That(assignedSfxCodes, Does.Contain("MAKER-ACTIVE-SFX-MISSING"));

                serialized.UpdateIfRequiredOrScript();
                firstSlot = serialized.FindProperty("activeAttackPresentations")
                    .GetArrayElementAtIndex(0).FindPropertyRelative("slots").GetArrayElementAtIndex(0);
                firstSlot.FindPropertyRelative("sfxState").enumValueIndex = 2;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var disabledCodes = Codes();
                Assert.That(disabledCodes, Does.Not.Contain("MAKER-ACTIVE-SFX-PENDING"));
                Assert.That(disabledCodes, Does.Not.Contain("MAKER-ACTIVE-SFX-MISSING"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test, Order(10)]
        public void PangoGaleDance_AssignedVfxUsesSemanticAnchorsAndCleansUp()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var draft = AssetDatabase.LoadMainAssetAtPath(
                "Assets/ProjectMT/Editor/MonsterMaker/Drafts/Draft_pango_01.asset") as ScriptableObject;
            Assert.That(draft, Is.Not.Null);
            var previewType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerPreviewStage");
            var preview = Activator.CreateInstance(previewType);
            try
            {
                previewType.GetMethod("SetDraft", flags).Invoke(preview, new object[] { draft });
                Assert.That((bool)previewType.GetProperty("CanPlayActiveSkill", flags).GetValue(preview), Is.True);
                previewType.GetMethod("PlayActiveSkill", flags).Invoke(preview, null);

                var tick = previewType.GetMethod("Tick", flags, null, new[] { typeof(float) }, null);
                var activeVfxField = previewType.GetField("activeVfx", flags);
                var stage = previewType.GetField("stage", flags).GetValue(preview);
                var previewRoot = stage.GetType().GetProperty("PreviewRoot", flags).GetValue(stage) as GameObject;
                var target = previewType.GetField("dummyTargetActor", flags).GetValue(preview) as UnitActor;
                var resolveHitPoint = previewType.GetMethod(
                    "ResolveCombatTargetHitPoint",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(UnitActor) },
                    null);
                Assert.That(previewRoot, Is.Not.Null);
                Assert.That(target, Is.Not.Null);
                Assert.That(resolveHitPoint, Is.Not.Null);

                var hitPoint = (Vector3)resolveHitPoint.Invoke(null, new object[] { target });
                var forward = hitPoint - previewRoot.transform.position;
                forward.y = 0f;
                forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
                var rotation = Quaternion.LookRotation(forward, Vector3.up);
                var attackOrigin = previewRoot.transform.Find((string)draft.GetType()
                    .GetProperty("AttackOriginPath").GetValue(draft)) ?? previewRoot.transform;
                var expectedLine = attackOrigin.position + rotation * new Vector3(0f, 0.8f, 0.45f);
                var dummyTargets = previewType.GetField("dummyTargets", flags).GetValue(preview) as IEnumerable;
                var expectedHitActors = dummyTargets.Cast<object>()
                    .Select(item => item.GetType().GetProperty("Actor", flags).GetValue(item) as UnitActor)
                    .Where(actor => actor != null)
                    .ToArray();
                Assert.That(expectedHitActors.Length, Is.GreaterThan(0));
                var sawLine = false;
                var sawCone = false;
                var sawHit = false;
                var sawDisabledSlot = false;
                var checkedHitInstances = new HashSet<int>();

                for (var frame = 0; frame < 700; frame++)
                {
                    tick.Invoke(preview, new object[] { 0.02f });
                    var activeVfx = activeVfxField.GetValue(preview) as IEnumerable;
                    foreach (var entry in activeVfx.Cast<object>())
                    {
                        var instance = entry.GetType().GetProperty("Instance").GetValue(entry) as GameObject;
                        if (instance == null) continue;
                        if (instance.name.Contains("일자 공격 경로"))
                        {
                            sawLine = true;
                            Assert.That(Vector3.Distance(instance.transform.position, expectedLine),
                                Is.LessThan(0.03f), "일자 공격은 AttackOrigin 기준 보정 위치에 있어야 합니다.");
                        }
                        if (instance.name.Contains("부채꼴 공격 면"))
                        {
                            sawCone = true;
                            Assert.That(Vector3.Distance(instance.transform.position, expectedLine),
                                Is.LessThan(0.03f), "부채꼴 공격도 AttackOrigin 기준 보정 위치에 있어야 합니다.");
                        }
                        if (instance.name.Contains("대상별 실제 명중"))
                        {
                            sawHit = true;
                            if (checkedHitInstances.Add(instance.GetInstanceID()))
                            {
                                var actual = instance.transform.position;
                                var nearestActor = expectedHitActors
                                    .OrderBy(actor => Vector2.Distance(
                                        new Vector2(actual.x, actual.z),
                                        new Vector2(actor.transform.position.x, actor.transform.position.z)))
                                    .First();
                                var targetRoot = nearestActor.transform.position;
                                var horizontalDistance = Vector2.Distance(
                                    new Vector2(actual.x, actual.z),
                                    new Vector2(targetRoot.x, targetRoot.z));
                                Assert.That(horizontalDistance,
                                    Is.LessThan(0.1f),
                                    $"명중 VFX의 수평 위치는 실제 대상과 일치해야 합니다. actual={actual}, target={targetRoot}");
                                Assert.That(actual.y,
                                    Is.InRange(targetRoot.y + 0.2f, targetRoot.y + 2.5f),
                                    $"명중 VFX는 대상 Root가 아니라 애니메이션 중인 상체 HitPoint 높이에 생성되어야 합니다. " +
                                    $"actual={actual}, target={targetRoot}");
                            }
                        }
                        if (instance.name.Contains("판정 예고") || instance.name.Contains("판정 완료"))
                            sawDisabledSlot = true;
                    }
                }

                Assert.That(sawLine, Is.True);
                Assert.That(sawCone, Is.True);
                Assert.That(sawHit, Is.True);
                Assert.That(sawDisabledSlot, Is.False, "비활성화한 슬롯의 잔존 Prefab은 재생하면 안 됩니다.");
                Assert.That((int)previewType.GetProperty("PreviewHitCount", flags).GetValue(preview),
                    Is.GreaterThanOrEqualTo(2));
                Assert.That((int)previewType.GetProperty("ActiveMarkerVfxCount", flags).GetValue(preview),
                    Is.EqualTo(0), "계약 수명이 지난 액티브 VFX가 Preview Scene에 남으면 안 됩니다.");
            }
            finally
            {
                (preview as IDisposable)?.Dispose();
            }
        }

        private static ScriptableObject CreateDraft(string monsterId, string draftPath)
        {
            var source = AssetDatabase.LoadMainAssetAtPath(SourceDraftPath) as ScriptableObject;
            Assert.That(source, Is.Not.Null, "액티브 QA 원본 Draft가 필요합니다.");
            var draft = ScriptableObject.CreateInstance(source.GetType());
            EditorUtility.CopySerialized(source, draft);
            draft.name = "Draft_" + monsterId;
            var serialized = new SerializedObject(draft);
            serialized.FindProperty("monsterId").stringValue = monsterId;
            serialized.FindProperty("displayName").stringValue = "천공 분쇄 QA 몬스터";
            serialized.FindProperty("rarity").enumValueIndex = (int)MonsterRarity.Legendary;
            serialized.FindProperty("activeSkillName").stringValue = "천공 분쇄";
            serialized.FindProperty("activeEnergyMaximum").intValue = 600;
            serialized.FindProperty("activeStepMotionModeConfigured").boolValue = true;
            serialized.FindProperty("useCustomActiveStepMotions").boolValue = false;
            var presentations = serialized.FindProperty("activeAttackPresentations");
            for (var index = 0; index < presentations.arraySize; index++)
            {
                var presentation = presentations.GetArrayElementAtIndex(index);
                presentation.FindPropertyRelative("motionConfigured").boolValue = true;
                presentation.FindPropertyRelative("motionClip").objectReferenceValue = null;
            }
            serialized.FindProperty("productionMemo").stringValue =
                "공격 액티브 조립소 Step, 계약 슬롯, 기본 공격 모션 대체, Writer 동일 GUID 재편입 QA";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            draft.GetType().GetMethod("EditorSyncActiveAttackAuthoring", BindingFlags.Public | BindingFlags.Instance)
                .Invoke(draft, null);
            serialized = new SerializedObject(draft);
            var syncedPresentations = serialized.FindProperty("activeAttackPresentations");
            for (var presentationIndex = 0; presentationIndex < syncedPresentations.arraySize; presentationIndex++)
            {
                var slots = syncedPresentations.GetArrayElementAtIndex(presentationIndex)
                    .FindPropertyRelative("slots");
                for (var slotIndex = 0; slotIndex < slots.arraySize; slotIndex++)
                {
                    var slot = slots.GetArrayElementAtIndex(slotIndex);
                    slot.FindPropertyRelative("assignmentStateConfigured").boolValue = true;
                    var vfxState = slot.FindPropertyRelative("vfxState");
                    var sfxState = slot.FindPropertyRelative("sfxState");
                    if (vfxState.enumValueIndex == 0) vfxState.enumValueIndex = 2;
                    if (sfxState.enumValueIndex == 0) sfxState.enumValueIndex = 2;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(draft, draftPath);
            AssetDatabase.SaveAssetIfDirty(draft);
            return AssetDatabase.LoadMainAssetAtPath(draftPath) as ScriptableObject;
        }

        private static object InvokeWriter(
            ScriptableObject draft,
            MonsterCatalog catalog,
            MonsterRarityCatalog rarityCatalog,
            string stage)
        {
            var writerType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerAssetWriter"))
                .First(type => type != null);
            var method = writerType.GetMethod("BuildAndRegister", BindingFlags.Public | BindingFlags.Static);
            try
            {
                return method.Invoke(null, new object[] { draft, catalog, rarityCatalog });
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw new InvalidOperationException(stage + " Maker Writer 실패: " + exception.InnerException.Message,
                    exception.InnerException);
            }
        }

        private static Type FindEditorType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .First(type => type != null);
        }

        private static void AssertBuiltActive(MonsterAttackActiveSkill active, ScriptableObject draft)
        {
            Assert.That(active, Is.Not.Null);
            Assert.That(active.TryValidate(out var error), Is.True, error);
            Assert.That(active.DisplayName, Is.EqualTo("천공 분쇄"));
            Assert.That(active.EnergyCost, Is.EqualTo(600f).Within(0.001f));
            Assert.That(active.Steps.Count, Is.EqualTo(3));
            Assert.That(active.Presentations.Count, Is.EqualTo(3));
            CollectionAssert.AreEqual(
                new[] { 6, 4, 3 },
                active.Presentations.Select(binding => binding.Slots.Count).ToArray());
            Assert.That(active.Presentations.SelectMany(binding => binding.Slots)
                .All(slot => slot.TryValidate(out _)), Is.True);
            var profile = new SerializedObject(draft).FindProperty("activeAttackProfile").objectReferenceValue;
            Assert.That(active.SourceProfile, Is.SameAs(profile));
        }

        private static void Cleanup(
            string draftPath,
            string dataFolder,
            string artFolder,
            string catalogPath,
            string rarityCatalogPath)
        {
            AssetDatabase.DeleteAsset(draftPath);
            AssetDatabase.DeleteAsset(dataFolder);
            AssetDatabase.DeleteAsset(artFolder);
            AssetDatabase.DeleteAsset(catalogPath);
            AssetDatabase.DeleteAsset(rarityCatalogPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
