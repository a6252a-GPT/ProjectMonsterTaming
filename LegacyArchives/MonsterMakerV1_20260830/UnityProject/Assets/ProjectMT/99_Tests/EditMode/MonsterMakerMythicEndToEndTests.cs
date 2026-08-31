using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Tests.EditMode
{
    public sealed class MonsterMakerMythicEndToEndTests // 실제 운영 편입 후 원복하는 신화 제작 회귀 검사
    {
        private const string SourceDraftPath =
            "Assets/ProjectMT/Editor/MonsterMaker/Drafts/Draft_mingyu_mythic_01.asset";
        private const string BasicAttackSourcePath =
            "Assets/ProjectMT/02_Shared/Unit/Data/BasicAttacks/BA_S_03.asset";
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
        public void MythicMaker_ComposesPreviewsBuildsAndRestoresProductionCatalogs()
        {
            var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var monsterId = "qa_mythic_" + suffix;
            var basicAttackId = "BA_S_QA_" + suffix;
            var draftPath = $"Assets/ProjectMT/Editor/MonsterMaker/Drafts/Draft_{monsterId}.asset";
            var basicAttackPath =
                $"Assets/ProjectMT/02_Shared/Unit/Data/BasicAttacks/Custom/{basicAttackId}.asset";
            var dataFolder = $"Assets/ProjectMT/02_Shared/Unit/Data/Monsters/{monsterId}";
            var artFolder = $"Assets/ProjectMT/05_Art/Monsters/{monsterId}";
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(draftPath), Is.Null, draftPath);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(basicAttackPath), Is.Null, basicAttackPath);
            Assert.That(AssetDatabase.IsValidFolder(dataFolder), Is.False, dataFolder);
            Assert.That(AssetDatabase.IsValidFolder(artFolder), Is.False, artFolder);

            var beforeCatalogs = ProductionCatalogPaths.ToDictionary(path => path, ReadAssetBytes);
            var beforeSelection = Selection.activeObject;
            EditorWindow makerWindow = null;
            try
            {
                var basicAttack = CreateComposedBasicAttack(basicAttackId, basicAttackPath);
                var draft = CreateMythicDraft(monsterId, draftPath, basicAttack);
                AssertMakerValidationPasses(draft);

                makerWindow = OpenMakerAndExercisePreview(draft);
                var writeResult = InvokeWriter(draft);
                VerifyGeneratedMythic(writeResult, basicAttack, monsterId);
            }
            finally
            {
                Selection.activeObject = beforeSelection;
                if (makerWindow != null)
                {
                    MonsterEditorWindowTestUtility.Close(makerWindow);
                }

                DeleteIfPresent(draftPath);
                DeleteIfPresent(basicAttackPath);
                DeleteIfPresent(dataFolder);
                DeleteIfPresent(artFolder);
                RestoreCatalogs(beforeCatalogs);
            }

            MonsterEditorWindowTestUtility.AssertNoOrphanedContainers("Monster Maker");
            foreach (var pair in beforeCatalogs)
            {
                CollectionAssert.AreEqual(pair.Value, ReadAssetBytes(pair.Key), pair.Key);
            }
            Assert.That(AssetDatabase.LoadMainAssetAtPath(draftPath), Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(basicAttackPath), Is.Null);
            Assert.That(AssetDatabase.IsValidFolder(dataFolder), Is.False, dataFolder);
            Assert.That(AssetDatabase.IsValidFolder(artFolder), Is.False, artFolder);
        }

        [Test]
        public void MakerCatalog_CachesOwnershipVirtualizesRowsAndKeepsSafeIdleTick()
        {
            var windowType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerWindow");
            var window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            Assert.That(window, Is.Not.Null);
            try
            {
                const BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
                const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.NonPublic;
                windowType.GetMethod("ReloadCatalogEntries", instanceFlags)?.Invoke(window, null);
                var definitions = windowType.GetField("catalogDefinitions", instanceFlags)?.GetValue(window)
                    as MonsterDefinition[];
                var draftCache = windowType.GetField("catalogDraftsById", instanceFlags)?.GetValue(window)
                    as IDictionary;
                var rarityCache = windowType.GetField("catalogRaritiesById", instanceFlags)?.GetValue(window)
                    as IDictionary;
                Assert.That(definitions, Is.Not.Null.And.Not.Empty);
                Assert.That(draftCache, Is.Not.Null);
                Assert.That(rarityCache, Is.Not.Null);
                Assert.That(draftCache.Count, Is.EqualTo(definitions.Length));
                Assert.That(rarityCache.Count, Is.EqualTo(definitions.Length));

                var calculateRange = windowType.GetMethod("CalculateVisibleCatalogRange", staticFlags);
                Assert.That(calculateRange, Is.Not.Null);
                var top = (Vector2Int)calculateRange.Invoke(null, new object[] { 0f, 720f, definitions.Length });
                var bottom = (Vector2Int)calculateRange.Invoke(
                    null,
                    new object[] { definitions.Length * 55f, 720f, definitions.Length });
                Assert.That(top.x, Is.EqualTo(0));
                Assert.That(top.y - top.x, Is.GreaterThan(1).And.LessThan(definitions.Length));
                Assert.That(bottom.x, Is.GreaterThan(0));
                Assert.That(bottom.y, Is.EqualTo(definitions.Length));

                var editorUpdate = windowType.GetMethod("OnEditorUpdate", instanceFlags);
                Assert.That(editorUpdate, Is.Not.Null);
                Assert.That(windowType.GetField("editorUpdateSubscribed", instanceFlags), Is.Null,
                    "동적 update 구독 방식은 일부 Clip의 SampleAnimation을 끊어 보이게 만든 회귀 경로라 다시 도입하면 안 됩니다.");
                Assert.That(windowType.GetMethod("RequestPreviewRefresh", instanceFlags), Is.Null);
                Assert.That(windowType.GetField("previewRefreshRequestedAt", instanceFlags), Is.Null);
                var preview = windowType.GetField("preview", instanceFlags)?.GetValue(window);
                Assert.That(preview, Is.Not.Null);
                var requiresTick = preview.GetType().GetProperty("RequiresContinuousTick");
                Assert.That(requiresTick, Is.Not.Null);
                Assert.That((bool)requiresTick.GetValue(preview), Is.False,
                    "재생·VFX·타격 예약이 없는 Maker Preview는 유휴 상태여야 합니다.");
                editorUpdate.Invoke(window, null);
                Assert.That((bool)requiresTick.GetValue(preview), Is.False,
                    "상시 update 콜백은 유휴 상태에서 Preview를 활성화하면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static MonsterBasicAttackProfile CreateComposedBasicAttack(
            string basicAttackId,
            string basicAttackPath)
        {
            var source = AssetDatabase.LoadAssetAtPath<MonsterBasicAttackProfile>(BasicAttackSourcePath);
            Assert.That(source, Is.Not.Null);
            Assert.That(AssetDatabase.IsValidFolder(Path.GetDirectoryName(basicAttackPath)?.Replace('\\', '/')), Is.True);

            var profile = ScriptableObject.CreateInstance<MonsterBasicAttackProfile>();
            profile.name = basicAttackId;
            var recipeType = FindEditorType("ProjectMT.EditorTools.MonsterMaker.BasicAttackWorkshopRecipe");
            var recipe = Activator.CreateInstance(recipeType, true);
            recipeType.GetMethod("Load", BindingFlags.Public | BindingFlags.Instance)
                ?.Invoke(recipe, new object[] { source });
            SetPublicField(recipe, "attackId", basicAttackId);
            SetPublicField(recipe, "displayName", "신화 QA 직선 빔");
            SetPublicField(
                recipe,
                "designMemo",
                "신화 Monster Maker 끝단 검증용. 직선 다중 판정과 실제 Motion Marker 연동을 확인한다.");
            SetPublicField(recipe, "rangeMultiplier", 1.2f);
            SetPublicField(recipe, "lineWidth", 0.9f);
            SetPublicField(recipe, "maxTargets", 5);
            recipeType.GetMethod("Compile", BindingFlags.Public | BindingFlags.Instance)
                ?.Invoke(recipe, new object[] { profile });

            Assert.That(profile.TryValidate(out var error), Is.True, error);
            AssetDatabase.CreateAsset(profile, basicAttackPath);
            AssetDatabase.SaveAssetIfDirty(profile);
            AssetDatabase.ImportAsset(basicAttackPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<MonsterBasicAttackProfile>(basicAttackPath);
        }

        private static ScriptableObject CreateMythicDraft(
            string monsterId,
            string draftPath,
            MonsterBasicAttackProfile basicAttack)
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
                "Assets/ProjectMT/02_Shared/Unit/Data/ActiveAttackProfiles/AAP_SkyBreak.asset");
            Assert.That(activeProfile, Is.Not.Null);

            var serialized = new SerializedObject(draft);
            serialized.FindProperty("monsterId").stringValue = monsterId;
            serialized.FindProperty("displayName").stringValue = "빙하의 파수꾼 QA";
            serialized.FindProperty("rarity").enumValueIndex = (int)MonsterRarity.Mythic;
            serialized.FindProperty("productionMemo").stringValue =
                "Monster Maker 신화 수직 검증: 조립 기본공격, 범용 패시브/액티브, 2·4돌파, Preview, 운영 편입/원복";
            serialized.FindProperty("skillLoadoutConfigured").boolValue = true;
            serialized.FindProperty("rarityPassiveSkill").objectReferenceValue = passive;
            serialized.FindProperty("rarityActiveSkill").objectReferenceValue = null;
            serialized.FindProperty("activeAttackProfile").objectReferenceValue = activeProfile;
            serialized.FindProperty("activeSkillName").stringValue = "빙하 천공 분쇄";
            serialized.FindProperty("activeEnergyMaximum").intValue = 840;
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
            serialized.FindProperty("basicAttackProfile").objectReferenceValue = basicAttack;
            serialized.FindProperty("combatType").enumValueIndex = (int)basicAttack.CombatType;
            serialized.FindProperty("maxHealth").floatValue = 480f;
            serialized.FindProperty("attackPower").floatValue = 42f;
            serialized.FindProperty("defense").floatValue = 28f;
            serialized.FindProperty("attackSpeed").floatValue = 0.82f;
            serialized.FindProperty("moveSpeed").floatValue = 1.8f;
            serialized.FindProperty("attackRange").floatValue = 4.2f;
            serialized.FindProperty("impactStrength").enumValueIndex = (int)MonsterImpactStrength.Heavy;
            serialized.FindProperty("reactionWeight").enumValueIndex = (int)MonsterReactionWeight.Heavy;
            serialized.FindProperty("mainBattleRole").enumValueIndex = (int)MainBattleMonsterRole.BacklineHunter;
            serialized.FindProperty("mainBattleTargetPriority").enumValueIndex = (int)UnitTargetPriority.RangedFirst;
            serialized.FindProperty("mainBattlePreferredRangeRatio").floatValue = 0.82f;
            serialized.FindProperty("mainBattleRetreatRangeRatio").floatValue = 0.32f;
            serialized.FindProperty("mainBattleRetargetInterval").floatValue = 0.16f;
            serialized.FindProperty("projectileLaunchRecoilDistance").floatValue = 0.12f;
            serialized.FindProperty("projectileLaunchRecoilDuration").floatValue = 0.12f;
            serialized.FindProperty("ascensionConfigured").boolValue = true;
            ConfigureStatModifier(serialized.FindProperty("ascension1"), "healthRate", 0.15f);
            ConfigureStatModifier(serialized.FindProperty("ascension3"), "attackRate", 0.12f);
            ConfigureStatModifier(serialized.FindProperty("ascension5"), "defenseRate", 0.15f);
            ConfigureAugment(
                serialized.FindProperty("ascension2"),
                monsterId + "_a2",
                "빙결 갑주 강화",
                MonsterSkillAugmentOperation.MagnitudeMultiplier,
                0.2f,
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
            ConfigureBasicAttackDecisions(draft, basicAttack);

            AssetDatabase.CreateAsset(draft, draftPath);
            AssetDatabase.SaveAssetIfDirty(draft);
            AssetDatabase.ImportAsset(draftPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadMainAssetAtPath(draftPath) as ScriptableObject;
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
                Assert.That((bool)previewType.GetProperty("RequiresContinuousTick").GetValue(preview), Is.False);
                Assert.That(
                    (bool)previewType.GetMethod("ShowBasicAttackArea", BindingFlags.Public | BindingFlags.Instance)
                        .Invoke(preview, null),
                    Is.True);
                Assert.That((int)previewType.GetProperty("ActiveHitAreaCount").GetValue(preview), Is.GreaterThan(0));
                Assert.That((bool)previewType.GetProperty("RequiresContinuousTick").GetValue(preview), Is.True);

                previewType.GetMethod("PlayAttack", BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(preview, new object[] { 0 });
                var tick = previewType.GetMethod(
                    "Tick",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(float) },
                    null);
                Assert.That(tick, Is.Not.Null);
                for (var index = 0; index < 50; index++)
                {
                    tick.Invoke(preview, new object[] { 0.1f });
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

        private static void VerifyGeneratedMythic(
            object writeResult,
            MonsterBasicAttackProfile basicAttack,
            string monsterId)
        {
            Assert.That(writeResult, Is.Not.Null);
            var resultType = writeResult.GetType();
            Assert.That(
                (bool)resultType.GetProperty("UpdatedExisting").GetValue(writeResult),
                Is.False,
                "검증용 ID는 매 실행마다 신규 생성으로 판정되어야 합니다.");
            var outputPaths = ((IEnumerable)resultType.GetProperty("OutputPaths").GetValue(writeResult))
                .Cast<string>()
                .ToArray();
            Assert.That(outputPaths, Has.Length.EqualTo(11));
            Assert.That(outputPaths, Has.All.Matches<string>(path => !string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(path))));

            var definition = resultType.GetProperty("Definition").GetValue(writeResult) as MonsterDefinition;
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.MonsterId, Is.EqualTo(monsterId));
            Assert.That(definition.DisplayName, Is.EqualTo("빙하의 파수꾼 QA"));
            Assert.That(
                MonsterDefinitionValidator.Validate(definition, true).HasErrors,
                Is.False,
                "생성된 신화 MonsterDefinition의 정식 Runtime 검증이 실패했습니다.");

            var combat = AssetDatabase.LoadAssetAtPath<MonsterCombatProfile>(outputPaths[3]);
            Assert.That(combat, Is.Not.Null);
            Assert.That(combat.Action.BasicAttackProfile, Is.SameAs(basicAttack));
            Assert.That(combat.TryValidate(out var combatError), Is.True, combatError);

            var ascension = AssetDatabase.LoadAssetAtPath<MonsterAscensionProfile>(outputPaths[4]);
            Assert.That(ascension, Is.Not.Null);
            Assert.That(ascension.IsConfigured, Is.True);
            Assert.That(ascension.TryValidate(out var ascensionError), Is.True, ascensionError);
            var augments = AssetDatabase.LoadAllAssetsAtPath(outputPaths[4]).OfType<MonsterAbilityDefinition>().ToArray();
            Assert.That(augments, Has.Length.EqualTo(2));
            Assert.That(augments.Any(ability => ability.AugmentTarget == MonsterSkillAugmentTarget.Passive), Is.True);
            Assert.That(augments.Any(ability => ability.AugmentTarget == MonsterSkillAugmentTarget.Active), Is.True);

            var runtime = AssetDatabase.LoadAssetAtPath<MonsterRuntimeAssetSet>(outputPaths[6]);
            Assert.That(runtime, Is.Not.Null);
            Assert.That(runtime.TryValidate(out var runtimeError), Is.True, runtimeError);
            var uniquePassive = AssetDatabase.LoadAssetAtPath<GenericMonsterPassiveSkill>(outputPaths[9]);
            Assert.That(uniquePassive, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<MonsterAttackActiveSkill>(outputPaths[10]), Is.Not.Null);
            Assert.That(uniquePassive.SkillId, Is.EqualTo($"passive_entry_shield_{monsterId}"));

            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(MonsterCatalogPath);
            var rarityCatalog = AssetDatabase.LoadAssetAtPath<MonsterRarityCatalog>(RarityCatalogPath);
            Assert.That(catalog.TryGet(monsterId, out var registered), Is.True);
            Assert.That(registered, Is.SameAs(definition));
            Assert.That(rarityCatalog.TryGetRarity(monsterId, out var rarity), Is.True);
            Assert.That(rarity, Is.EqualTo(MonsterRarity.Mythic));
            var rarityEntry = rarityCatalog.LegendaryMythicEntries.Single(entry => entry.Monster == definition);
            Assert.That(rarityEntry.PassiveSkill, Is.SameAs(uniquePassive));
            var attackActive = rarityEntry.ActiveSkill as MonsterAttackActiveSkill;
            Assert.That(attackActive, Is.Not.Null);
            Assert.That(attackActive.SkillId, Is.EqualTo("active_" + monsterId));
            Assert.That(attackActive.DisplayName, Is.EqualTo("빙하 천공 분쇄"));
            Assert.That(attackActive.SourceProfile.ProfileId, Is.EqualTo("sky_break"));
            Assert.That(attackActive.MythicExclusive, Is.True);
            Assert.That(rarityEntry.TryValidateSkillReferences(out var skillError), Is.True, skillError);

            var adapter = AssetDatabase.LoadAssetAtPath<GameObject>(outputPaths[8]);
            Assert.That(adapter, Is.Not.Null);
            Assert.That(adapter.transform.Find("Visual"), Is.Not.Null);
            Assert.That(adapter.transform.Find("AttackOrigin"), Is.Not.Null);
            Assert.That(adapter.transform.Find("HitCenter"), Is.Not.Null);
            Assert.That(adapter.GetComponentInChildren<Animator>(true), Is.Not.Null);
        }

        private static void ConfigureStatModifier(SerializedProperty modifier, string fieldName, float value)
        {
            modifier.FindPropertyRelative(fieldName).floatValue = value;
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
                    var index = bindings.arraySize;
                    bindings.InsertArrayElementAtIndex(index);
                    var binding = bindings.GetArrayElementAtIndex(index);
                    binding.FindPropertyRelative("attackId").stringValue = profile.AttackId;
                    binding.FindPropertyRelative("slotId").stringValue = contract.SlotId;
                    binding.FindPropertyRelative("motionId").stringValue = motionId;
                    binding.FindPropertyRelative("state").enumValueIndex =
                        (int)MonsterBasicAttackVfxAssignmentState.Disabled;
                    binding.FindPropertyRelative("prefab").objectReferenceValue = null;
                    binding.FindPropertyRelative("sfxState").enumValueIndex =
                        (int)MonsterBasicAttackSfxAssignmentState.Disabled;
                    binding.FindPropertyRelative("sound").objectReferenceValue = null;
                    binding.FindPropertyRelative("soundVolume").floatValue = 1f;
                    binding.FindPropertyRelative("sfx").objectReferenceValue = null;
                    binding.FindPropertyRelative("lifetime").floatValue = contract.DefaultLifetime;
                    binding.FindPropertyRelative("playbackOffset").floatValue = 0f;
                    binding.FindPropertyRelative("playbackSpeed").floatValue = 1f;
                    binding.FindPropertyRelative("eventTimingOffset").floatValue = 0f;
                    binding.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
                    binding.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
                    binding.FindPropertyRelative("scale").floatValue = 1f;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
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

        private static void SetPublicField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
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

        private static string ToFullPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.That(projectRoot, Is.Not.Null.And.Not.Empty);
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
