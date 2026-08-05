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
        private const string MonsterCardPath =
            "Assets/ProjectMT/03_Features/Formation/Prefabs/PF_MonsterCard.prefab";
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
                "{\"dataVersion\":4,\"gameData\":{\"monsters\":{" +
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
                "{\"dataVersion\":4,\"gameData\":{\"gold\":21,\"monsters\":{" +
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
                "{\"dataVersion\":4,\"gameData\":{\"monsters\":{" +
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
            var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterCardPath);
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
            Assert.That(cardPrefab.GetComponent<MonsterCardView>(), Is.Not.Null);

            var ownedPanel = pagePrefab.transform.Find("PageRoot/OwnedMonsterPanel");
            var ownedTitle = ownedPanel.Find("Title").GetComponent("TextMeshProUGUI");
            var viewport = ownedPanel.Find("Viewport").GetComponent<RectTransform>();
            var content = viewport.Find("Content").GetComponent<RectTransform>();
            var grid = content.GetComponent<GridLayoutGroup>();
            var scroll = ownedPanel.GetComponent<ScrollRect>();
            var preview = pagePrefab.transform.Find("PageRoot/SelectedMonsterPanel/MonsterPreview");
            var aspect = preview.GetComponent<AspectRatioFitter>();

            Assert.That(ownedTitle, Is.Not.Null);
            Assert.That(new SerializedObject(ownedTitle).FindProperty("m_text").stringValue,
                Is.EqualTo("전체 보유 몬스터"));
            Assert.That(grid.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
            Assert.That(grid.constraintCount, Is.EqualTo(7));
            Assert.That(scroll.horizontal, Is.False);
            Assert.That(scroll.vertical, Is.True);
            Assert.That(scroll.content, Is.SameAs(content));
            Assert.That(scroll.viewport, Is.SameAs(viewport));
            Assert.That(aspect.aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.HeightControlsWidth));
            Assert.That(aspect.aspectRatio, Is.EqualTo(1f));

            var pageSerialized = new SerializedObject(pagePrefab.GetComponent<FormationPageController>());
            Assert.That(pageSerialized.FindProperty("cardPrefab").objectReferenceValue, Is.Not.Null);
            Assert.That(pageSerialized.FindProperty("previewCamera").objectReferenceValue, Is.Not.Null);
            Assert.That(pageSerialized.FindProperty("previewLight").objectReferenceValue, Is.Not.Null);
            Assert.That(pageSerialized.FindProperty("worldCamera").objectReferenceValue, Is.Null);

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
                        .Find("01_MainGameplayRoot/04_UIRoot/MainBattleHUD/ModeButton")
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
                var minimumCenterDistance = (modeButton.rect.height + openFormationButton.rect.height) * 0.5f;
                Assert.That(
                    Mathf.Abs(modeButton.anchoredPosition.y - openFormationButton.anchoredPosition.y),
                    Is.GreaterThanOrEqualTo(minimumCenterDistance));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void FormationAssetsAndMainBattleScene_HaveNoMissingScripts()
        {
            foreach (var prefabPath in new[] { MonsterCardPath, FormationPagePath, DebugPanelPath })
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
