using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ProjectMT.Contents.Framework;
using ProjectMT.Contents.FoodRiot;
using ProjectMT.Core.SaveIO;
using ProjectMT.Core.SceneFlow;
using ProjectMT.Features.Expedition;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Reward;
using ProjectMT.Shared.UI;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectMT.Tests.EditMode
{
    public sealed class SeedContractTests // 시드 구조·자산 계약 회귀 검사
    {
        [Test]
        public void SceneId_IsCaseInsensitiveAndRejectsBlank() // 씬 ID 비교·빈값 방어
        {
            Assert.That(new SceneId("main_battle"), Is.EqualTo(new SceneId("MAIN_BATTLE")));
            Assert.That(new SceneId(" ").IsValid, Is.False);
        }

        [Test]
        public async Task SaveService_LoadsLegacyFoodRiotBestKillsKey() // 개명 전 저장 기록 호환
        {
            const string legacyJson =
                "{\"dataVersion\":1,\"savedAtUtc\":\"2026-08-03T00:00:00.0000000Z\",\"gameData\":{\"vegetableRiotBestKills\":7}}";
            var fileStore = new MemoryFileStore(Encoding.UTF8.GetBytes(legacyJson));
            var saveService = new SaveService(fileStore, "memory://project-mt-save");

            var data = await saveService.LoadAsync();

            Assert.That(data.FoodRiotBestKills, Is.EqualTo(7));
            Assert.That(data.Commander.Level, Is.EqualTo(1));
        }

        [Test]
        public void SeedParty_HasExactlyFiveCombatMonsters() // 기본 전투 파티 5마리 보장
        {
            var party = SeedBattlePartySnapshotFactory.Create();

            Assert.That(party.Units, Has.Length.EqualTo(5));
            Assert.That(party.TotalPower, Is.GreaterThan(0f));
        }

        [Test]
        public void DefaultProgress_StartsCommanderAtLevelOne() // 군단장 최초 성장값
        {
            var progress = GameProgressData.CreateDefault();

            Assert.That(progress.Commander.Level, Is.EqualTo(1));
            Assert.That(progress.Commander.Experience, Is.Zero);

            var calculationInput = new CommanderProgressView(12, 345L);
            Assert.That(calculationInput.Level, Is.EqualTo(12));
            Assert.That(calculationInput.Experience, Is.EqualTo(345L));
        }

        [Test]
        public void LegionStatBonus_ExposesSixGrowthRates() // 군단 공용 능력치 6종
        {
            var bonus = new LegionStatBonus(0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f);

            Assert.That(bonus.HealthRate, Is.EqualTo(0.1f));
            Assert.That(bonus.AttackRate, Is.EqualTo(0.2f));
            Assert.That(bonus.DefenseRate, Is.EqualTo(0.3f));
            Assert.That(bonus.AttackSpeedRate, Is.EqualTo(0.4f));
            Assert.That(bonus.MoveSpeedRate, Is.EqualTo(0.5f));
            Assert.That(bonus.AttackRangeRate, Is.EqualTo(0.6f));
        }

        [Test]
        public void UnitStatsSnapshot_HasDefenseValue() // 전투 Snapshot 방어력 자리
        {
            var stats = new UnitStatsSnapshot { defense = 12f };

            Assert.That(stats.defense, Is.EqualTo(12f));
        }

        [TestCase(1, 4)]
        [TestCase(10, 4)]
        [TestCase(11, 5)]
        [TestCase(20, 5)]
        [TestCase(21, 6)]
        [TestCase(31, 7)]
        public void ExpeditionEnemyCount_IncreasesEveryTenStages(int stage, int expectedPerWave) // 10단계별 웨이브 증원 규칙
        {
            Assert.That(ExpeditionStageRules.GetEnemiesPerWave(stage), Is.EqualTo(expectedPerWave));
            Assert.That(ExpeditionStageRules.GetTotalEnemies(stage), Is.EqualTo(expectedPerWave * 2));

            for (var rowStart = 0; rowStart < expectedPerWave; rowStart += ExpeditionStageRules.FormationColumns)
            {
                var rowCount = Mathf.Min(
                    ExpeditionStageRules.FormationColumns,
                    expectedPerWave - rowStart);
                var rowCenter = Enumerable.Range(rowStart, rowCount)
                    .Average(index => ExpeditionStageRules.GetFormationOffset(index, expectedPerWave).x);
                Assert.That(rowCenter, Is.EqualTo(0f).Within(0.001f));
            }
        }

        [Test]
        public void FormalizedScenes_AreCentralizedAndRegistered() // 정식 씬 위치·등록 상태 검사
        {
            var runtimeScenes = new[]
            {
                "Assets/ProjectMT/00_Scenes/00_Entry.unity",
                "Assets/ProjectMT/00_Scenes/01_MainBattle.unity",
                "Assets/ProjectMT/00_Scenes/02_CastleRaid.unity"
            };

            foreach (var scenePath in runtimeScenes)
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath), Is.Not.Null, scenePath);
            }

            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/ProjectMT/00_Scenes/DEV_01_FoodRiot.unity"),
                Is.Not.Null);
            Assert.That(AssetDatabase.IsValidFolder("Assets/Scenes"), Is.False);
            Assert.That(
                AssetDatabase.FindAssets("t:Scene", new[] { "Assets/ProjectMT" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => path.StartsWith("Assets/"))
                    .All(path => path.StartsWith("Assets/ProjectMT/00_Scenes/")),
                Is.True);
            Assert.That(
                EditorBuildSettings.scenes.Select(scene => scene.path),
                Is.EquivalentTo(runtimeScenes));
            Assert.That(EditorBuildSettings.scenes.All(scene => scene.enabled), Is.True);
        }

        [Test]
        public void ArchitectureWorkAreas_ArePresent() // 9차 구조 필수 폴더 검사
        {
            var moduleRoots = new[]
            {
                "Assets/ProjectMT/01_Core/Time",
                "Assets/ProjectMT/02_Shared/Skill",
                "Assets/ProjectMT/02_Shared/Reward",
                "Assets/ProjectMT/02_Shared/Audio",
                "Assets/ProjectMT/03_Features/MonsterGrowth",
                "Assets/ProjectMT/03_Features/Formation",
                "Assets/ProjectMT/03_Features/Commander",
                "Assets/ProjectMT/03_Features/Equipment",
                "Assets/ProjectMT/03_Features/Potential",
                "Assets/ProjectMT/03_Features/Summon",
                "Assets/ProjectMT/03_Features/Quest",
                "Assets/ProjectMT/03_Features/OfflineReward",
                "Assets/ProjectMT/03_Features/Attendance",
                "Assets/ProjectMT/03_Features/Collection",
                "Assets/ProjectMT/03_Features/Mail",
                "Assets/ProjectMT/03_Features/Shop",
                "Assets/ProjectMT/03_Features/Settings",
                "Assets/ProjectMT/03_Features/TutorialUnlock",
                "Assets/ProjectMT/04_Contents/00_Framework/Runtime",
                "Assets/ProjectMT/04_Contents/00_Framework/Data",
                "Assets/ProjectMT/05_Art/Characters",
                "Assets/ProjectMT/05_Art/Environment",
                "Assets/ProjectMT/05_Art/Props",
                "Assets/ProjectMT/05_Art/VFX",
                "Assets/ProjectMT/05_Art/UI",
                "Assets/ProjectMT/05_Art/Animation",
                "Assets/ProjectMT/05_Art/Materials",
                "Assets/ProjectMT/05_Art/Fonts",
                "Assets/ProjectMT/06_Audio/BGM",
                "Assets/ProjectMT/06_Audio/SFX",
                "Assets/ProjectMT/06_Audio/Ambience",
                "Assets/ProjectMT/06_Audio/Voice",
                "Assets/ProjectMT/07_Localization/Tables",
                "Assets/ProjectMT/90_Tools/DataTools/Editor",
                "Assets/ProjectMT/90_Tools/CastleBake/Editor",
                "Assets/ProjectMT/98_Generated/Art",
                "Assets/ProjectMT/98_Generated/Data",
                "Assets/ProjectMT/98_Generated/Bakes",
                "Assets/ThirdParty"
            };

            foreach (var moduleRoot in moduleRoots)
            {
                Assert.That(AssetDatabase.IsValidFolder(moduleRoot), Is.True, moduleRoot);
            }

            var contentRoots = new[]
            {
                "01_CastleRaid",
                "02_FoodRiot",
                "03_TreasureSpirit",
                "04_GiantSpellbook",
                "05_GuardianTrial"
            };
            var contentSubfolders = new[]
            {
                "Runtime",
                "Prefabs",
                "UI",
                "Data",
                "Art",
                "Audio",
                "Tests"
            };

            foreach (var contentRoot in contentRoots)
            {
                foreach (var subfolder in contentSubfolders)
                {
                    var path = $"Assets/ProjectMT/04_Contents/{contentRoot}/{subfolder}";
                    Assert.That(AssetDatabase.IsValidFolder(path), Is.True, path);
                }
            }

            foreach (var unnumberedRoot in new[]
                     {
                         "Framework",
                         "CastleRaid",
                         "FoodRiot",
                         "TreasureSpirit",
                         "GiantSpellbook",
                         "GuardianTrial"
                     })
            {
                Assert.That(
                    AssetDatabase.IsValidFolder($"Assets/ProjectMT/04_Contents/{unnumberedRoot}"),
                    Is.False,
                    unnumberedRoot);
            }
        }

        [Test]
        public void SeedAssets_AreStoredByOwningModule() // 주요 자산의 담당 모듈 위치 검사
        {
            var ownedAssets = new[]
            {
                "Assets/ProjectMT/01_Core/Bootstrap/Data/ProjectConfig.asset",
                "Assets/ProjectMT/01_Core/Config/SceneCatalog.asset",
                "Assets/ProjectMT/04_Contents/00_Framework/Data/ContentCatalog.asset",
                "Assets/ProjectMT/04_Contents/02_FoodRiot/Prefabs/PF_Vegetable.prefab",
                "Assets/ProjectMT/04_Contents/02_FoodRiot/Art/Materials/MAT_Vegetable.mat",
                "Assets/ProjectMT/04_Contents/03_TreasureSpirit/ProjectMT.Contents.TreasureSpirit.asmdef",
                "Assets/ProjectMT/04_Contents/04_GiantSpellbook/ProjectMT.Contents.GiantSpellbook.asmdef",
                "Assets/ProjectMT/04_Contents/05_GuardianTrial/ProjectMT.Contents.GuardianTrial.asmdef",
                "Assets/ProjectMT/04_Contents/01_CastleRaid/Prefabs/PF_CastleAssaultTofu.prefab",
                "Assets/ProjectMT/04_Contents/01_CastleRaid/Art/Materials/MAT_Castle_Wall.mat",
                "Assets/ProjectMT/05_Art/Materials/MAT_Tofu_Friendly.mat",
                "Assets/ProjectMT/05_Art/Fonts/FontAssets/TMP_SpoqaHanSansNeo_Body.asset",
                "Assets/ProjectMT/05_Art/Fonts/FontAssets/TMP_HakgyoansimYeohaeng_Title.asset",
                "Assets/ProjectMT/05_Art/Fonts/FontAssets/TMP_NoonnuBasicGothic_Button.asset"
            };

            foreach (var ownedAsset in ownedAssets)
            {
                Assert.That(AssetDatabase.LoadMainAssetAtPath(ownedAsset), Is.Not.Null, ownedAsset);
            }
        }

        [Test]
        public void EventSystem_IsGlobalAndExcludedFromHostedPrefab() // 전역 입력 시스템 중복 방지
        {
            const string inputActionsPath = "Assets/ProjectMT/02_Shared/Input/ProjectInputActions.asset";
            var appRoot = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ProjectMT/01_Core/Bootstrap/Prefabs/PF_AppRoot.prefab");
            var hostedRuntime = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ProjectMT/04_Contents/02_FoodRiot/Prefabs/PF_FoodRiotRuntime.prefab");
            var inputActions = AssetDatabase.LoadMainAssetAtPath(inputActionsPath);

            Assert.That(appRoot, Is.Not.Null);
            Assert.That(hostedRuntime, Is.Not.Null);
            Assert.That(inputActions, Is.Not.Null);
            Assert.That(
                AssetDatabase.LoadMainAssetAtPath(
                    "Assets/ProjectMT/02_Shared/Input/ProjectInputActions.inputactions"),
                Is.Null);
            Assert.That(
                AssetDatabase.LoadMainAssetAtPath("Assets/ProjectMT/02_Shared/Input/SeedUIActions.asset"),
                Is.Null);
            Assert.That(
                EditorBuildSettings.TryGetConfigObject(
                    "com.unity.input.settings.actions",
                    out UnityEngine.Object configuredInputActions),
                Is.True);
            Assert.That(AssetDatabase.GetAssetPath(configuredInputActions), Is.EqualTo(inputActionsPath));
            Assert.That(CountComponents(appRoot, "UnityEngine.EventSystems.EventSystem"), Is.EqualTo(1));
            Assert.That(CountComponents(hostedRuntime, "UnityEngine.EventSystems.EventSystem"), Is.Zero);
            Assert.That(hostedRuntime.activeSelf, Is.False);

            var inputModule = appRoot.GetComponentsInChildren<Component>(true)
                .FirstOrDefault(component => component != null &&
                                             component.GetType().FullName ==
                                             "UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            Assert.That(inputModule, Is.Not.Null);
            var serializedModule = new SerializedObject(inputModule);
            foreach (var propertyName in new[]
                     {
                         "m_ActionsAsset",
                         "m_PointAction",
                         "m_MoveAction",
                         "m_SubmitAction",
                         "m_CancelAction",
                         "m_LeftClickAction",
                         "m_MiddleClickAction",
                         "m_RightClickAction",
                         "m_ScrollWheelAction"
                     })
            {
                Assert.That(
                    serializedModule.FindProperty(propertyName)?.objectReferenceValue,
                    Is.Not.Null,
                    propertyName);
            }

            Assert.That(
                AssetDatabase.GetAssetPath(
                    serializedModule.FindProperty("m_ActionsAsset")?.objectReferenceValue),
                Is.EqualTo(inputActionsPath));
        }

        [Test]
        public void CastleStage_HasAuthoredGroupsAndPersistentNavMesh() // 성 스테이지 구성·NavMesh 보장
        {
            var stage = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ProjectMT/04_Contents/01_CastleRaid/Prefabs/CastleStage_Seed.prefab");

            Assert.That(stage, Is.Not.Null);
            Assert.That(
                Enumerable.Range(0, stage.transform.childCount)
                    .Select(index => stage.transform.GetChild(index).name),
                Is.EqualTo(new[]
                {
                    "00_GroundRoot",
                    "01_DeploymentRoot",
                    "02_EntryRoot",
                    "03_WallsRoot",
                    "04_DefendersRoot",
                    "05_BuildingsRoot",
                    "06_MainCastleRoot"
                }));

            var wallsRoot = stage.transform.Find("03_WallsRoot");
            Assert.That(wallsRoot, Is.Not.Null);
            Assert.That(wallsRoot.childCount, Is.EqualTo(36));
            var minimum = new Vector3(float.PositiveInfinity, 0f, float.PositiveInfinity);
            var maximum = new Vector3(float.NegativeInfinity, 0f, float.NegativeInfinity);
            for (var index = 0; index < wallsRoot.childCount; index++)
            {
                var position = stage.transform.InverseTransformPoint(wallsRoot.GetChild(index).position);
                minimum = Vector3.Min(minimum, position);
                maximum = Vector3.Max(maximum, position);
            }

            var wallCenter = (minimum + maximum) * 0.5f;
            Assert.That(wallCenter.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(wallCenter.z, Is.EqualTo(0f).Within(0.001f));

            var surface = stage.GetComponents<Component>()
                .FirstOrDefault(component => component != null &&
                                             component.GetType().FullName == "Unity.AI.Navigation.NavMeshSurface");
            Assert.That(surface, Is.Not.Null);
            var serializedSurface = new SerializedObject(surface);
            var navMeshData = serializedSurface.FindProperty("m_NavMeshData")?.objectReferenceValue;
            Assert.That(navMeshData, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(navMeshData),
                Is.EqualTo("Assets/ProjectMT/04_Contents/01_CastleRaid/Data/Baked/CastleStage_Seed_NavMesh.asset"));
        }

        [Test]
        public void ContentClearOverlay_IsAuthoredForGrowthAndCastle() // 두 콘텐츠 결과 화면 연결 검사
        {
            var clearOverlayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ProjectMT/02_Shared/UI/Prefabs/PF_ContentClearOverlay.prefab");
            var hostedRuntime = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ProjectMT/04_Contents/02_FoodRiot/Prefabs/PF_FoodRiotRuntime.prefab");

            Assert.That(clearOverlayPrefab, Is.Not.Null);
            Assert.That(clearOverlayPrefab.GetComponent<ContentClearOverlay>(), Is.Not.Null);
            Assert.That(clearOverlayPrefab.activeSelf, Is.False);
            Assert.That(hostedRuntime.GetComponentsInChildren<ContentClearOverlay>(true), Has.Length.EqualTo(1));

            var castleScene = EditorSceneManager.OpenScene(
                "Assets/ProjectMT/00_Scenes/02_CastleRaid.unity",
                OpenSceneMode.Additive);
            try
            {
                var overlays = castleScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<ContentClearOverlay>(true))
                    .ToArray();
                Assert.That(overlays, Has.Length.EqualTo(1));
            }
            finally
            {
                EditorSceneManager.CloseScene(castleScene, true);
            }
        }

        [Test]
        public void ContentFlow_SuccessMovesPlayingFinishingIdleAndPresentsAfterSave() // 저장 확정 수직 흐름 검사
        {
            var sequence = new System.Collections.Generic.List<string>();
            var progress = new RecordingProgressService(sequence, true);
            var presentation = new RecordingRewardPresentation(sequence);
            var feedback = new RecordingFinishFeedback();
            var factory = ScriptableObject.CreateInstance<TestStartDataFactory>();
            var adapter = ScriptableObject.CreateInstance<TestResultAdapter>();
            var definition = ScriptableObject.CreateInstance<ContentDefinition>();
            definition.EditorConfigure(
                new ContentId("reward_test"),
                ContentOpenMode.MainBattleHosted,
                new SceneId(string.Empty),
                factory,
                adapter);
            var catalog = ScriptableObject.CreateInstance<ContentCatalog>();
            catalog.EditorSetDefinitions(new[] { definition });
            var runner = new RecordingHostedRunner();
            var flow = new ContentFlow(
                catalog,
                progress,
                new RecordingSceneNavigator(),
                new SceneId("main_battle"),
                presentation,
                feedback);

            Assert.That(flow.Phase, Is.EqualTo(ContentFlowPhase.Idle));
            Assert.That(
                flow.StartHosted(new ContentId("reward_test"), SeedBattlePartySnapshotFactory.Create(), runner),
                Is.True);
            Assert.That(flow.Phase, Is.EqualTo(ContentFlowPhase.Playing));

            runner.Context.Exit.Complete(new TestResult(7));

            Assert.That(flow.Phase, Is.EqualTo(ContentFlowPhase.Idle));
            Assert.That(presentation.PlayCount, Is.EqualTo(1));
            Assert.That(sequence, Is.EqualTo(new[] { "save", "present" }));
            Assert.That(runner.CloseCount, Is.EqualTo(1));
            Assert.That(feedback.ShowSavingCount, Is.EqualTo(1));
            Assert.That(feedback.HideCount, Is.EqualTo(1));
            Assert.That(adapter.ProgressChangeCount, Is.EqualTo(1));
            Assert.That(adapter.PresentationCount, Is.EqualTo(1));
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(adapter);
            Object.DestroyImmediate(factory);
        }

        [Test]
        public void ContentFlow_SaveFailureKeepsFinishingAndRetryUsesSameResultOnce() // 실패 결과 보존·수동 재시도 검사
        {
            var sequence = new System.Collections.Generic.List<string>();
            var progress = new RecordingProgressService(sequence, false, true);
            var presentation = new RecordingRewardPresentation(sequence);
            var feedback = new RecordingFinishFeedback();
            var factory = ScriptableObject.CreateInstance<TestStartDataFactory>();
            var adapter = ScriptableObject.CreateInstance<TestResultAdapter>();
            var definition = ScriptableObject.CreateInstance<ContentDefinition>();
            definition.EditorConfigure(
                new ContentId("retry_test"),
                ContentOpenMode.MainBattleHosted,
                new SceneId(string.Empty),
                factory,
                adapter);
            var catalog = ScriptableObject.CreateInstance<ContentCatalog>();
            catalog.EditorSetDefinitions(new[] { definition });
            var runner = new RecordingHostedRunner();
            ContentFlow flow = null;
            runner.Closing += () => Assert.That(flow.Phase, Is.EqualTo(ContentFlowPhase.Finishing));
            flow = new ContentFlow(
                catalog,
                progress,
                new RecordingSceneNavigator(),
                new SceneId("main_battle"),
                presentation,
                feedback);

            Assert.That(flow.StartHosted(new ContentId("retry_test"), SeedBattlePartySnapshotFactory.Create(), runner), Is.True);
            LogAssert.Expect(LogType.Error, "Content progress could not be saved. Content=retry_test");
            runner.Context.Exit.Complete(new TestResult(9));

            Assert.That(flow.Phase, Is.EqualTo(ContentFlowPhase.Finishing));
            Assert.That(flow.IsRunning, Is.True);
            Assert.That(runner.CloseCount, Is.Zero);
            Assert.That(presentation.PlayCount, Is.Zero);
            Assert.That(feedback.ShowFailedCount, Is.EqualTo(1));
            Assert.That(sequence, Is.EqualTo(new[] { "save" }));

            feedback.Retry();

            Assert.That(flow.Phase, Is.EqualTo(ContentFlowPhase.Idle));
            Assert.That(flow.IsRunning, Is.False);
            Assert.That(sequence, Is.EqualTo(new[] { "save", "save", "present" }));
            Assert.That(runner.CloseCount, Is.EqualTo(1));
            Assert.That(presentation.PlayCount, Is.EqualTo(1));
            Assert.That(adapter.ProgressChangeCount, Is.EqualTo(1));
            Assert.That(adapter.PresentationCount, Is.EqualTo(1));
            Assert.That(feedback.ShowSavingCount, Is.EqualTo(2));
            Assert.That(feedback.HideCount, Is.EqualTo(1));

            feedback.Retry(); // 성공 뒤 남은 콜백 없음
            Assert.That(sequence, Has.Count.EqualTo(3));
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(adapter);
            Object.DestroyImmediate(factory);
        }

        [Test]
        public void ContentFlow_SeparateSceneFailureReturnsEnteringToIdle() // 별도 씬 진입 실패 잠금 해제 검사
        {
            var sequence = new System.Collections.Generic.List<string>();
            var factory = ScriptableObject.CreateInstance<TestStartDataFactory>();
            var definition = ScriptableObject.CreateInstance<ContentDefinition>();
            var sceneId = new SceneId("separate_test_scene");
            definition.EditorConfigure(
                new ContentId("separate_test"),
                ContentOpenMode.SeparateScene,
                sceneId,
                factory,
                null);
            var catalog = ScriptableObject.CreateInstance<ContentCatalog>();
            catalog.EditorSetDefinitions(new[] { definition });
            var flow = new ContentFlow(
                catalog,
                new RecordingProgressService(sequence, true),
                new RecordingSceneNavigator(),
                new SceneId("main_battle"),
                null,
                new RecordingFinishFeedback());

            Assert.That(flow.StartSeparate(new ContentId("separate_test"), SeedBattlePartySnapshotFactory.Create()), Is.True);
            Assert.That(flow.Phase, Is.EqualTo(ContentFlowPhase.Entering));
            Assert.That(flow.NotifySceneLoadFailed(sceneId), Is.True);
            Assert.That(flow.Phase, Is.EqualTo(ContentFlowPhase.Idle));
            Assert.That(flow.IsRunning, Is.False);
            Assert.That(flow.NotifySceneLoadFailed(sceneId), Is.False);

            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(factory);
        }

        [Test]
        public void ContentFlow_RequiresFinishFeedback() // 저장 실패 복구 UI 필수 계약
        {
            var sequence = new System.Collections.Generic.List<string>();
            var catalog = ScriptableObject.CreateInstance<ContentCatalog>();

            var exception = Assert.Throws<System.ArgumentNullException>(() => new ContentFlow(
                catalog,
                new RecordingProgressService(sequence, true),
                new RecordingSceneNavigator(),
                new SceneId("main_battle"),
                null,
                null));

            Assert.That(exception.ParamName, Is.EqualTo("finishFeedback"));
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void FoodRiotRewardPresentation_MatchesSavedSeedGold() // 식량 대소동 저장값·표시값 일치
        {
            var adapter = ScriptableObject.CreateInstance<FoodRiotResultAdapter>();

            Assert.That(
                adapter.TryCreateRewardPresentation(new FoodRiotResult(11), out var presentation),
                Is.True);
            Assert.That(presentation.Items.Count, Is.EqualTo(1));
            Assert.That(presentation.Items[0].Kind, Is.EqualTo(RewardPresentationKind.Gold));
            Assert.That(presentation.Items[0].Amount, Is.EqualTo(11));

            Object.DestroyImmediate(adapter);
        }

        [Test]
        public void FeedbackPresentationAssets_AreAuthoredAndWired() // 런타임 생성 루트가 아닌 정식 Prefab·Scene 연결 검사
        {
            var floatingNumber = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ProjectMT/02_Shared/Combat/Prefabs/PF_FloatingNumber.prefab");
            var rewardItem = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ProjectMT/02_Shared/UI/Prefabs/PF_RewardAcquireItem.prefab");
            var rewardOverlay = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ProjectMT/02_Shared/UI/Prefabs/PF_RewardAcquireOverlay.prefab");
            var finishFeedback = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ProjectMT/01_Core/Bootstrap/Prefabs/PF_ContentFinishFeedback.prefab");
            var appRoot = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ProjectMT/01_Core/Bootstrap/Prefabs/PF_AppRoot.prefab");
            var hostedRuntime = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ProjectMT/04_Contents/02_FoodRiot/Prefabs/PF_FoodRiotRuntime.prefab");

            Assert.That(floatingNumber?.GetComponent<ProjectMT.Shared.Combat.FloatingNumberView>(), Is.Not.Null);
            Assert.That(floatingNumber.transform.localScale.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(rewardItem?.GetComponent<RewardAcquireView>(), Is.Not.Null);
            Assert.That(rewardOverlay?.GetComponent<RewardAcquirePresenter>(), Is.Not.Null);
            Assert.That(CountComponents(finishFeedback, "ProjectMT.Bootstrap.ContentFinishFeedbackPresenter"), Is.EqualTo(1));
            Assert.That(appRoot?.GetComponentsInChildren<RewardAcquirePresenter>(true), Has.Length.EqualTo(1));
            Assert.That(CountComponents(appRoot, "ProjectMT.Bootstrap.ContentFinishFeedbackPresenter"), Is.EqualTo(1));
            AssertSerializedReference(appRoot, "ProjectMT.Bootstrap.AppRootHost", "finishFeedbackPresenter");
            AssertSerializedReference(finishFeedback, "ProjectMT.Bootstrap.ContentFinishFeedbackPresenter", "panelRoot");
            AssertSerializedReference(finishFeedback, "ProjectMT.Bootstrap.ContentFinishFeedbackPresenter", "messageText");
            AssertSerializedReference(finishFeedback, "ProjectMT.Bootstrap.ContentFinishFeedbackPresenter", "retryButton");
            AssertCombatFeedbackExtensions(hostedRuntime);

            foreach (var scenePath in new[]
                     {
                         "Assets/ProjectMT/00_Scenes/01_MainBattle.unity",
                         "Assets/ProjectMT/00_Scenes/02_CastleRaid.unity"
                     })
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                try
                {
                    var feedbackRoots = scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<ProjectMT.Shared.Combat.CombatFeedbackPlayer>(true))
                        .Select(feedback => feedback.gameObject)
                        .ToArray();
                    Assert.That(feedbackRoots, Is.Not.Empty, scenePath);
                    foreach (var feedbackRoot in feedbackRoots)
                    {
                        AssertCombatFeedbackExtensions(feedbackRoot);
                    }
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void AssertCombatFeedbackExtensions(GameObject root) // 전투 피드백 로컬 소유권 검사
        {
            Assert.That(root, Is.Not.Null);
            var feedback = root.GetComponentsInChildren<ProjectMT.Shared.Combat.CombatFeedbackPlayer>(true).Single();
            Assert.That(feedback.GetComponent<ProjectMT.Shared.Combat.FloatingNumberPresenter>(), Is.Not.Null);
            Assert.That(feedback.GetComponent<ProjectMT.Shared.Audio.SfxPool>(), Is.Not.Null);
        }

        private static int CountComponents(GameObject root, string fullTypeName) // 하위 오브젝트의 특정 컴포넌트 집계
        {
            return root.GetComponentsInChildren<Component>(true)
                .Count(component => component != null && component.GetType().FullName == fullTypeName);
        }

        private static void AssertSerializedReference(GameObject root, string fullTypeName, string propertyName) // 필수 Inspector 참조 검사
        {
            var component = root.GetComponentsInChildren<Component>(true)
                .Single(item => item != null && item.GetType().FullName == fullTypeName);
            var property = new SerializedObject(component).FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Serialized property is missing. Type={fullTypeName}, Property={propertyName}");
            Assert.That(property.objectReferenceValue, Is.Not.Null, $"Serialized reference is missing. Type={fullTypeName}, Property={propertyName}");
        }

        private sealed class MemoryFileStore : IAtomicFileStore // 저장 마이그레이션 테스트용 메모리 파일
        {
            private readonly byte[] bytes;

            public MemoryFileStore(byte[] bytes)
            {
                this.bytes = bytes;
            }

            public Task<byte[]> ReadAsync(string path)
            {
                return Task.FromResult(bytes);
            }

            public Task ReplaceAsync(string path, byte[] replacement)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class TestStartData : IContentStartData
        {
        }

        private sealed class TestResult : IContentResultData
        {
            public TestResult(int reward)
            {
                Reward = reward;
            }

            public int Reward { get; }
        }

        private sealed class TestStartDataFactory : ContentStartDataFactory
        {
            public override IContentStartData Create(BattlePartySnapshot party)
            {
                return new TestStartData();
            }
        }

        private sealed class TestResultAdapter : ContentResultAdapter
        {
            public int ProgressChangeCount { get; private set; }
            public int PresentationCount { get; private set; }

            public override bool TryCreateProgressChange(IContentResultData result, out GameProgressChange change)
            {
                ProgressChangeCount++;
                if (!(result is TestResult testResult))
                {
                    change = null;
                    return false;
                }

                change = GameProgressChange.RecordFoodRiot(
                    testResult.Reward,
                    RewardBundle.FromGold(testResult.Reward));
                return true;
            }

            public override bool TryCreateRewardPresentation(
                IContentResultData result,
                out RewardPresentationRequest presentation)
            {
                PresentationCount++;
                if (!(result is TestResult testResult))
                {
                    presentation = null;
                    return false;
                }

                presentation = RewardPresentationRequest.Gold(testResult.Reward);
                return true;
            }
        }

        private sealed class RecordingHostedRunner : IHostedContentRunner
        {
            public ContentContext Context { get; private set; }
            public int CloseCount { get; private set; }
            public event System.Action Closing;

            public bool Open(ContentContext context)
            {
                Context = context;
                return true;
            }

            public void Close()
            {
                Closing?.Invoke();
                CloseCount++;
            }
        }

        private sealed class RecordingProgressService : IGameProgressService
        {
            private readonly System.Collections.Generic.Queue<bool> saveResults;
            private readonly System.Collections.Generic.List<string> sequence;

            public RecordingProgressService(System.Collections.Generic.List<string> sequence, params bool[] saveResults)
            {
                this.sequence = sequence;
                this.saveResults = new System.Collections.Generic.Queue<bool>(saveResults);
            }

            public GameProgressView View => new GameProgressView(GameProgressData.CreateDefault());
            public bool IsLoaded => true;
            public event System.Action Changed
            {
                add { }
                remove { }
            }

            public Task<bool> TryApplyAndSaveAsync(GameProgressChange change)
            {
                sequence.Add("save");
                return Task.FromResult(saveResults.Count > 0 && saveResults.Dequeue());
            }

            public Task SaveCurrentAsync()
            {
                return Task.CompletedTask;
            }
        }

        private sealed class RecordingFinishFeedback : IContentFinishFeedback
        {
            private System.Action retry;

            public int ShowSavingCount { get; private set; }
            public int ShowFailedCount { get; private set; }
            public int HideCount { get; private set; }

            public void ShowSaving()
            {
                ShowSavingCount++;
                retry = null;
            }

            public void ShowSaveFailed(System.Action retryAction)
            {
                ShowFailedCount++;
                retry = retryAction;
            }

            public void Hide()
            {
                HideCount++;
                retry = null;
            }

            public void Retry()
            {
                var action = retry;
                retry = null;
                action?.Invoke();
            }
        }

        private sealed class RecordingRewardPresentation : IRewardPresentationPlayer
        {
            private readonly System.Collections.Generic.List<string> sequence;

            public RecordingRewardPresentation(System.Collections.Generic.List<string> sequence)
            {
                this.sequence = sequence;
            }

            public int PlayCount { get; private set; }

            public void PlayConfirmed(RewardPresentationRequest request)
            {
                PlayCount++;
                sequence.Add("present");
            }
        }

        private sealed class RecordingSceneNavigator : ISceneNavigator
        {
            public bool IsTransitioning => false;

            public void Load(SceneId sceneId)
            {
            }
        }
    }
}
