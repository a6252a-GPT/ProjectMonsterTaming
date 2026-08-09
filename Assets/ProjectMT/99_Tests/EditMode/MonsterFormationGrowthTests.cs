using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ProjectMT.Core.SaveIO;
using ProjectMT.Features.Formation;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.Debugging;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectMT.Tests.EditMode
{
    public sealed class MonsterFormationGrowthTests // 편성·획득·레벨업 원자 계약 검사
    {
        private const string CatalogPath =
            "Assets/ProjectMT/02_Shared/Unit/Data/MonsterCatalog.asset";
        private const string FormationPagePath =
            "Assets/ProjectMT/03_Features/Formation/Prefabs/PF_FormationPage.prefab";
        private const string LegacyFormationPagePath =
            "Assets/ProjectMT/03_Features/Formation/Prefabs/Legacy/PF_FormationPage_Legacy.prefab";
        private const string FormationRosterPath =
            "Assets/ProjectMT/03_Features/Formation/Prefabs/PF_MonsterRosterList.prefab";
        private const string ManagementPagePath =
            "Assets/ProjectMT/03_Features/Formation/Prefabs/PF_MonsterManagementPage.prefab";
        private const string MonsterCardPath =
            "Assets/ProjectMT/03_Features/Formation/Prefabs/PF_MonsterCard.prefab";
        private const string VerticalPopupPath =
            "Assets/ProjectMT/02_Shared/UI/Prefabs/Standard/PF_UIStandard_PopupVertical.prefab";
        private const string MediumPopupPath =
            "Assets/ProjectMT/02_Shared/UI/Prefabs/Standard/PF_UIStandard_PopupMedium.prefab";
        private const string WidePopupPath =
            "Assets/ProjectMT/02_Shared/UI/Prefabs/Standard/PF_UIStandard_PopupWide.prefab";
        private const string PlaceholderPortraitPath =
            "Assets/ProjectMT/03_Features/Formation/Art/Portraits/Portrait_Placeholder.png";
        private const string DebugPanelPath =
            "Assets/ProjectMT/02_Shared/Debug/Resources/Debug/PF_DebugPanel.prefab";
        private const string MainBattleScenePath =
            "Assets/ProjectMT/00_Scenes/01_MainBattle.unity";

        [Test]
        public void LevelRules_UseConfirmedTemporaryFormulas()
        {
            Assert.That(MonsterLevelRules.TryGetNextLevelCost(1, out var levelOneCost), Is.True);
            Assert.That(MonsterLevelRules.TryGetNextLevelCost(2, out var levelTwoCost), Is.True);
            Assert.That(MonsterLevelRules.TryGetNextLevelCost(3, out var levelThreeCost), Is.True);
            Assert.That(levelOneCost, Is.EqualTo(10));
            Assert.That(levelTwoCost, Is.EqualTo(11));
            Assert.That(levelThreeCost, Is.EqualTo(12));
            Assert.That(MonsterLevelRules.GetStatMultiplier(1), Is.EqualTo(1f));
            Assert.That(MonsterLevelRules.GetStatMultiplier(10), Is.EqualTo(1.09f).Within(0.0001f));
            Assert.That(MonsterLevelRules.TryGetNextLevelCost(int.MaxValue, out _), Is.False);
        }

        [Test]
        public async Task VersionTwoSave_MigratesOwnedMonsterLevelsAndGoldToCurrentVersion()
        {
            const string json =
                "{\"dataVersion\":2,\"gameData\":{\"temporaryGold\":31,\"monsters\":{" +
                "\"ownedMonsters\":[{\"monsterId\":\"tofu_01\"},{\"monsterId\":\"tofu_02\",\"level\":4}]," +
                "\"mainPartySlots\":[\"tofu_01\"],\"reservePartySlots\":[\"tofu_02\"]}}}";
            var store = new MemoryFileStore(Encoding.UTF8.GetBytes(json));
            var service = new SaveService(store, "memory://project-mt-save");

            var loaded = await service.LoadAsync();
            var migrated = JsonUtility.FromJson<SaveEnvelope>(Encoding.UTF8.GetString(store.Bytes));

            Assert.That(loaded.Monsters.TryGetOwnedMonster("tofu_01", out var starter), Is.True);
            Assert.That(loaded.Monsters.TryGetOwnedMonster("tofu_02", out var ranged), Is.True);
            Assert.That(starter.Level, Is.EqualTo(1));
            Assert.That(ranged.Level, Is.EqualTo(4));
            Assert.That(loaded.Gold, Is.EqualTo(31));
            Assert.That(migrated.dataVersion, Is.EqualTo(SaveService.CurrentDataVersion));
            Assert.That(store.ReplaceCount, Is.EqualTo(1));
        }

        [Test]
        public async Task AcquireAndFormationChanges_SaveWithoutDuplicatesAndKeepMainParty()
        {
            var store = new MemoryFileStore(Encoding.UTF8.GetBytes(
                "{\"dataVersion\":5,\"gameData\":{\"monsters\":{" +
                "\"ownedMonsters\":[{\"monsterId\":\"tofu_01\",\"level\":1}]," +
                "\"mainPartySlots\":[\"tofu_01\"],\"reservePartySlots\":[]}}}"));
            var gameData = new GameDataService(new SaveService(store, "memory://project-mt-save"));
            await gameData.LoadAsync();

            Assert.That(await gameData.TryApplyAndSaveAsync(GameProgressChange.AcquireMonster("tofu_02")), Is.True);
            Assert.That(await gameData.TryApplyAndSaveAsync(GameProgressChange.AcquireMonster("tofu_02")), Is.False);
            Assert.That(await gameData.TryApplyAndSaveAsync(
                GameProgressChange.AssignMonster("tofu_02", MonsterPartyKind.Main)), Is.True);
            Assert.That(gameData.View.Monsters.MainPartySlots, Is.EqualTo(new[] { "tofu_01", "tofu_02", "", "", "" }));

            Assert.That(await gameData.TryApplyAndSaveAsync(
                GameProgressChange.AssignMonster("tofu_01", MonsterPartyKind.Reserve)), Is.True);
            Assert.That(gameData.View.Monsters.MainPartySlots, Is.EqualTo(new[] { "tofu_02", "", "", "", "" }));
            Assert.That(gameData.View.Monsters.ReservePartySlots, Is.EqualTo(new[] { "tofu_01", "" }));

            Assert.That(await gameData.TryApplyAndSaveAsync(GameProgressChange.UnassignMonster("tofu_01")), Is.True);
            Assert.That(gameData.View.Monsters.ReservePartySlots, Is.EqualTo(new[] { "", "" }));
            Assert.That(await gameData.TryApplyAndSaveAsync(
                GameProgressChange.AssignMonster("tofu_02", MonsterPartyKind.Reserve)), Is.False);
            Assert.That(gameData.View.Monsters.MainPartySlots[0], Is.EqualTo("tofu_02"));
            Assert.That(store.ReplaceCount, Is.EqualTo(4));
        }

        [Test]
        public async Task LevelUp_SpendsGoldAndRaisesLevelAsOneSavedChange()
        {
            var store = new MemoryFileStore(Encoding.UTF8.GetBytes(
                "{\"dataVersion\":5,\"gameData\":{\"gold\":21,\"monsters\":{" +
                "\"ownedMonsters\":[{\"monsterId\":\"tofu_01\",\"level\":1}]," +
                "\"mainPartySlots\":[\"tofu_01\"],\"reservePartySlots\":[]}}}"));
            var gameData = new GameDataService(new SaveService(store, "memory://project-mt-save"));
            await gameData.LoadAsync();

            Assert.That(await gameData.TryApplyAndSaveAsync(
                GameProgressChange.LevelUpMonster("tofu_01", 1)), Is.True);
            Assert.That(gameData.View.Gold, Is.EqualTo(11));
            Assert.That(gameData.View.Monsters.TryGetOwnedMonster("tofu_01", out var levelTwo), Is.True);
            Assert.That(levelTwo.Level, Is.EqualTo(2));

            Assert.That(await gameData.TryApplyAndSaveAsync(
                GameProgressChange.LevelUpMonster("tofu_01", 1)), Is.False);
            Assert.That(await gameData.TryApplyAndSaveAsync(
                GameProgressChange.LevelUpMonster("tofu_01", 2)), Is.True);
            Assert.That(gameData.View.Gold, Is.Zero);
            Assert.That(gameData.View.Monsters.TryGetOwnedMonster("tofu_01", out var levelThree), Is.True);
            Assert.That(levelThree.Level, Is.EqualTo(3));

            Assert.That(await gameData.TryApplyAndSaveAsync(
                GameProgressChange.LevelUpMonster("tofu_01", 3)), Is.False);
            Assert.That(gameData.View.Gold, Is.Zero);
            Assert.That(store.ReplaceCount, Is.EqualTo(2));
        }

        [Test]
        public async Task SnapshotBuilder_AppliesMonsterLevelBeforeCommanderBonus()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryGet("tofu_01", out var definition), Is.True);

            var store = new MemoryFileStore(Encoding.UTF8.GetBytes(
                "{\"dataVersion\":5,\"gameData\":{\"monsters\":{" +
                "\"ownedMonsters\":[{\"monsterId\":\"tofu_01\",\"level\":3}]," +
                "\"mainPartySlots\":[\"tofu_01\"],\"reservePartySlots\":[]}}}"));
            var loaded = await new SaveService(store, "memory://project-mt-save").LoadAsync();
            var bonus = new LegionStatBonus(0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f);

            var party = new BattlePartySnapshotBuilder(catalog).Build(new GameProgressView(loaded), bonus);
            var stats = party.Units[0].Stats;
            const float levelMultiplier = 1.02f;

            Assert.That(stats.maxHealth, Is.EqualTo(definition.MaxHealth * levelMultiplier * 1.1f).Within(0.001f));
            Assert.That(stats.damage, Is.EqualTo(definition.AttackPower * levelMultiplier * 1.2f).Within(0.001f));
            Assert.That(stats.defense, Is.EqualTo(definition.Defense * levelMultiplier * 1.3f).Within(0.001f));
            Assert.That(stats.moveSpeed, Is.EqualTo(definition.MoveSpeed * levelMultiplier * 1.5f).Within(0.001f));
            Assert.That(stats.attackRange, Is.EqualTo(definition.AttackRange * levelMultiplier * 1.6f).Within(0.001f));
            Assert.That(
                stats.attackInterval,
                Is.EqualTo(1f / (definition.AttackSpeed * levelMultiplier * 1.4f)).Within(0.001f));
        }

        [Test]
        public void FormationAssets_UseSharedCardPlaceholderAndSceneWiring()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(CatalogPath);
            var pagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FormationPagePath);
            var legacyPagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyFormationPagePath);
            var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterCardPath);
            var managementPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagementPagePath);
            var placeholder = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderPortraitPath);
            var debugPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DebugPanelPath);

            Assert.That(catalog.Definitions, Has.Count.EqualTo(8));
            var visualTints = new HashSet<Color32>();
            for (var index = 1; index <= 8; index++)
            {
                var monsterId = $"tofu_{index:00}";
                Assert.That(catalog.TryGet(monsterId, out var definition), Is.True, monsterId);
                Assert.That(definition.Portrait, Is.SameAs(placeholder), monsterId);
                Assert.That(definition.PreviewPrefab, Is.Not.Null, monsterId);
                Assert.That(visualTints.Add((Color32)definition.VisualTint), Is.True,
                    $"{monsterId}의 표시 색상이 다른 몬스터와 겹칩니다.");
            }
            Assert.That(pagePrefab.GetComponent<FormationPageController>(), Is.Not.Null);
            Assert.That(legacyPagePrefab, Is.Not.Null);
            Assert.That(legacyPagePrefab.activeSelf, Is.False);
            Assert.That(cardPrefab.GetComponent<MonsterCardView>(), Is.Not.Null);

            var pageRoot = pagePrefab.transform.Find("PageRoot");
            var pageRect = pageRoot.GetComponent<RectTransform>();
            var managementRect = managementPrefab.GetComponent<RectTransform>();
            var roster = pageRoot.Find("FormationContent/MonsterList_Common");
            var rosterView = roster.GetComponent<MonsterRosterListView>();
            var ownedTitle = roster.Find("Title").GetComponent("TextMeshProUGUI");
            var preview = pageRoot.Find("FormationContent/FormationBoardPanel/FormationPreviewRawImage")
                .GetComponent<RawImage>();
            var stage = pageRoot.Find("FormationPreviewStage_Runtime");
            var previewCamera = stage.Find("FormationPreviewCamera_Runtime").GetComponent<Camera>();
            var slotsRoot = stage.Find("FormationSlots_CCW_FromLowerLeft");

            Assert.That(pageRoot.gameObject.activeSelf, Is.False);
            Assert.That(
                pageRect.anchoredPosition.x + pageRect.rect.width * 0.5f,
                Is.EqualTo(managementRect.anchoredPosition.x + managementRect.rect.width * 0.5f)
                    .Within(0.001f));
            Assert.That(ownedTitle, Is.Not.Null);
            Assert.That(new SerializedObject(ownedTitle).FindProperty("m_text").stringValue,
                Is.EqualTo("보유 몬스터"));
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(roster.gameObject),
                Is.EqualTo(FormationRosterPath));
            Assert.That(rosterView, Is.Not.Null);
            Assert.That(rosterView.GetComponentInChildren<ScrollRect>(true).vertical, Is.True);
            Assert.That(rosterView.GetComponentInChildren<ScrollRect>(true).horizontal, Is.False);
            Assert.That(rosterView.GetComponentInChildren<RectMask2D>(true), Is.Not.Null);
            Assert.That(rosterView.GetComponentInChildren<GridLayoutGroup>(true).constraintCount, Is.EqualTo(4));
            Assert.That(roster.GetComponentsInChildren<MonsterCardView>(true), Has.Length.EqualTo(10));
            Assert.That(preview.texture, Is.Not.Null);
            Assert.That(previewCamera.targetTexture, Is.SameAs(preview.texture));
            Assert.That(previewCamera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(previewCamera.backgroundColor.a, Is.Zero.Within(0.001f));
            Assert.That(slotsRoot.childCount, Is.EqualTo(10));
            for (var index = 0; index < slotsRoot.childCount; index++)
            {
                var slot = slotsRoot.GetChild(index);
                var anchor = slot.Find("MonsterPreviewAnchor");
                Assert.That(anchor, Is.Not.Null, slot.name);
                Assert.That(anchor.childCount, Is.Zero, slot.name);
                Assert.That(slot.Find("GroundSlotRing"), Is.Not.Null, slot.name);
            }

            var pageSerialized = new SerializedObject(pagePrefab.GetComponent<FormationPageController>());
            Assert.That(pageSerialized.FindProperty("ownedRosterList").objectReferenceValue, Is.Not.Null);
            Assert.That(pageSerialized.FindProperty("cardPrefab").objectReferenceValue, Is.Not.Null);
            Assert.That(pageSerialized.FindProperty("previewCamera").objectReferenceValue, Is.Not.Null);
            Assert.That(pageSerialized.FindProperty("previewLight").objectReferenceValue, Is.Not.Null);
            Assert.That(pageSerialized.FindProperty("formationButton").objectReferenceValue, Is.Not.Null);
            Assert.That(pageSerialized.FindProperty("formationPreviewSlotsRoot").objectReferenceValue, Is.Not.Null);
            Assert.That(pageSerialized.FindProperty("activeSlotMaterial").objectReferenceValue, Is.Not.Null);
            Assert.That(pageSerialized.FindProperty("lockedSlotMaterial").objectReferenceValue, Is.Not.Null);
            Assert.That(pageSerialized.FindProperty("worldCamera").objectReferenceValue, Is.Null);

            var managementSerialized = new SerializedObject(
                managementPrefab.GetComponent<MonsterManagementPageController>());
            Assert.That(managementSerialized.FindProperty("rosterList").objectReferenceValue, Is.Not.Null);

            var debugSerialized = new SerializedObject(debugPrefab.GetComponent<DebugPanelController>());
            Assert.That(debugSerialized.FindProperty("drawMonsterButton").objectReferenceValue, Is.Not.Null);
            Assert.That(debugSerialized.FindProperty("drawMonsterLabel").objectReferenceValue, Is.Not.Null);

            var scene = EditorSceneManager.OpenScene(MainBattleScenePath, OpenSceneMode.Additive);
            try
            {
                MainBattleSceneRoot sceneRoot = null;
                FormationPageController formationPage = null;
                RectTransform modeButton = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    sceneRoot ??= root.GetComponentInChildren<MainBattleSceneRoot>(true);
                    formationPage ??= root.GetComponentInChildren<FormationPageController>(true);
                    modeButton ??= root.transform
                        .Find("01_MainGameplayRoot/04_UIRoot/MainBattleHUD/PF_HudQuickMenu/" +
                              "StatusLayer/TopCenterStatus/StageStatusRoot/ModeButton")
                        ?.GetComponent<RectTransform>();
                }

                Assert.That(sceneRoot, Is.Not.Null);
                Assert.That(formationPage, Is.Not.Null);
                Assert.That(modeButton, Is.Not.Null);
                var rootSerialized = new SerializedObject(sceneRoot);
                Assert.That(rootSerialized.FindProperty("formationPage").objectReferenceValue, Is.SameAs(formationPage));
                var scenePageSerialized = new SerializedObject(formationPage);
                Assert.That(scenePageSerialized.FindProperty("worldCamera").objectReferenceValue, Is.Not.Null);

                var openFormationButton = formationPage.transform.Find("OpenFormationButton")
                    .GetComponent<RectTransform>();
                Assert.That(openFormationButton.gameObject.activeSelf, Is.False);
                Assert.That(scenePageSerialized.FindProperty("showStandaloneOpenButton").boolValue, Is.False);

                var stageStatusRoot = modeButton.parent.GetComponent<RectTransform>();
                var modeRight = modeButton.anchoredPosition.x +
                                modeButton.rect.width * (1f - modeButton.pivot.x);
                Assert.That(modeRight, Is.LessThanOrEqualTo(stageStatusRoot.rect.width * 0.5f));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void SharedRosterList_SupportsOneHundredCardsInFourColumnVerticalScroll()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FormationRosterPath);
            var instance = Object.Instantiate(prefab);
            try
            {
                var rosterView = instance.GetComponent<MonsterRosterListView>();
                var scrollRect = instance.GetComponentInChildren<ScrollRect>(true);
                var grid = instance.GetComponentInChildren<GridLayoutGroup>(true);

                Assert.That(rosterView, Is.Not.Null);
                Assert.That(rosterView.EnsureCardCount(100), Is.EqualTo(100));
                Assert.That(rosterView.Cards, Has.Count.EqualTo(100));
                Assert.That(rosterView.ContentRoot.childCount, Is.EqualTo(100));
                Assert.That(scrollRect.vertical, Is.True);
                Assert.That(scrollRect.horizontal, Is.False);
                Assert.That(grid.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
                Assert.That(grid.constraintCount, Is.EqualTo(4));

                LayoutRebuilder.ForceRebuildLayoutImmediate(rosterView.ContentRoot);
                Assert.That(rosterView.ContentRoot.rect.height,
                    Is.GreaterThan(scrollRect.viewport.rect.height));

                Assert.That(rosterView.EnsureCardCount(7), Is.EqualTo(7));
                Assert.That(rosterView.Cards, Has.Count.EqualTo(100), "생성한 카드는 다시 사용할 수 있어야 합니다.");
                var activeCount = 0;
                foreach (var card in rosterView.Cards)
                {
                    if (card.gameObject.activeSelf)
                    {
                        activeCount++;
                    }
                }

                Assert.That(activeCount, Is.EqualTo(7));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void PopupStandards_ExpandLeftFromOneRightEdge()
        {
            var vertical = AssetDatabase.LoadAssetAtPath<GameObject>(VerticalPopupPath)
                .GetComponent<RectTransform>();
            var medium = AssetDatabase.LoadAssetAtPath<GameObject>(MediumPopupPath)
                .GetComponent<RectTransform>();
            var wide = AssetDatabase.LoadAssetAtPath<GameObject>(WidePopupPath)
                .GetComponent<RectTransform>();

            var verticalRight = vertical.anchoredPosition.x + vertical.rect.width * 0.5f;
            var mediumRight = medium.anchoredPosition.x + medium.rect.width * 0.5f;
            var wideRight = wide.anchoredPosition.x + wide.rect.width * 0.5f;

            Assert.That(verticalRight, Is.EqualTo(mediumRight).Within(0.001f));
            Assert.That(mediumRight, Is.EqualTo(wideRight).Within(0.001f));
        }

        [Test]
        public void FormationPopup_DoesNotChangeWorldCameraRendering()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FormationPagePath);
            var instance = Object.Instantiate(prefab);
            var cameraObject = new GameObject("FormationWorldCameraTest");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 0x1234;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = Color.magenta;

            try
            {
                var controller = instance.GetComponent<FormationPageController>();
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("worldCamera").objectReferenceValue = camera;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var setPageOpen = typeof(FormationPageController).GetMethod(
                    "SetPageOpen",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                setPageOpen?.Invoke(controller, new object[] { true });
                setPageOpen?.Invoke(controller, new object[] { false });

                Assert.That(camera.enabled, Is.False);
                Assert.That(camera.cullingMask, Is.EqualTo(0x1234));
                Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.Skybox));
                Assert.That(camera.backgroundColor, Is.EqualTo(Color.magenta));
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public async Task ManagementRoster_PrioritizesAssignedThenHigherRarity()
        {
            var store = new MemoryFileStore(Encoding.UTF8.GetBytes(
                "{\"dataVersion\":5,\"gameData\":{\"monsters\":{" +
                "\"ownedMonsters\":[" +
                "{\"monsterId\":\"tofu_05\"}," +
                "{\"monsterId\":\"tofu_02\"}," +
                "{\"monsterId\":\"tofu_07\"}," +
                "{\"monsterId\":\"tofu_01\"}," +
                "{\"monsterId\":\"tofu_06\"}]," +
                "\"mainPartySlots\":[\"tofu_01\",\"tofu_07\"]," +
                "\"reservePartySlots\":[]}}}"));
            var progress = new GameDataService(new SaveService(store, "memory://roster-sort"));
            await progress.LoadAsync();
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(CatalogPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagementPagePath);
            var instance = Object.Instantiate(prefab);
            try
            {
                instance.SetActive(false);
                var controller = instance.GetComponent<MonsterManagementPageController>();
                controller.GetType()
                    .GetMethod(
                        "Awake",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(controller, null);
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("previewAnchor").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                controller.Configure(progress, catalog);
                controller.OpenPage();

                var rosterList = controller.GetComponentInChildren<MonsterRosterListView>(true);
                var cards = rosterList.Cards;
                var monsterIdField = typeof(MonsterCardView).GetField(
                    "monsterId",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var visibleOrder = new string[5];
                for (var index = 0; index < visibleOrder.Length; index++)
                {
                    var card = cards[index];
                    visibleOrder[index] = (string)monsterIdField?.GetValue(card);
                }

                CollectionAssert.AreEqual(
                    new[] { "tofu_07", "tofu_01", "tofu_06", "tofu_05", "tofu_02" },
                    visibleOrder);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void FormationAssetsAndMainBattleScene_HaveNoMissingScripts()
        {
            foreach (var prefabPath in new[]
                     {
                         MonsterCardPath,
                         FormationPagePath,
                         LegacyFormationPagePath,
                         FormationRosterPath,
                         ManagementPagePath,
                         DebugPanelPath
                     })
            {
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    Assert.That(CountMissingScripts(root), Is.Zero, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            var scene = EditorSceneManager.OpenScene(MainBattleScenePath, OpenSceneMode.Additive);
            try
            {
                var missingCount = 0;
                foreach (var root in scene.GetRootGameObjects())
                    missingCount += CountMissingScripts(root);

                Assert.That(missingCount, Is.Zero, MainBattleScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static int CountMissingScripts(GameObject root)
        {
            var missingCount = 0;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                missingCount += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                    transform.gameObject);
            }

            return missingCount;
        }

        private sealed class MemoryFileStore : IAtomicFileStore
        {
            public MemoryFileStore(byte[] bytes)
            {
                Bytes = bytes;
            }

            public byte[] Bytes { get; private set; }
            public int ReplaceCount { get; private set; }

            public Task<byte[]> ReadAsync(string path)
            {
                return Task.FromResult(Bytes);
            }

            public Task ReplaceAsync(string path, byte[] bytes)
            {
                Bytes = bytes;
                ReplaceCount++;
                return Task.CompletedTask;
            }
        }
    }
}
