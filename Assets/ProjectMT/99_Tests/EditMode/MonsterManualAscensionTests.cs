using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ProjectMT.Core.SaveIO;
using ProjectMT.Features.Formation;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Tests.EditMode
{
    public sealed class MonsterManualAscensionTests // 중복 재료·수동 돌파 저장 계약 검사
    {
        private const string CatalogPath =
            "Assets/ProjectMT/02_Shared/Unit/Data/MonsterCatalog.asset";
        private const string ManagementPagePath =
            "Assets/ProjectMT/03_Features/Formation/Prefabs/PF_MonsterManagementPage.prefab";
        private const string MonsterCardPath =
            "Assets/ProjectMT/03_Features/Formation/Prefabs/PF_MonsterCard.prefab";

        [Test]
        public async Task VersionFourSave_MigratesWithEmptyAscensionMaterials()
        {
            var store = CreateStore(
                "{\"dataVersion\":4,\"gameData\":{\"monsters\":{" +
                "\"ownedMonsters\":[{\"monsterId\":\"tofu_01\",\"ascensionLevel\":2}]," +
                "\"mainPartySlots\":[\"tofu_01\"],\"reservePartySlots\":[]}}}");

            var loaded = await new SaveService(store, "memory://ascension-save").LoadAsync();
            var migrated = JsonUtility.FromJson<SaveEnvelope>(Encoding.UTF8.GetString(store.Bytes));

            Assert.That(loaded.Monsters.TryGetOwnedMonster("tofu_01", out var owned), Is.True);
            Assert.That(owned.AscensionLevel, Is.EqualTo(2));
            Assert.That(owned.AscensionMaterialCount, Is.Zero);
            Assert.That(migrated.dataVersion, Is.EqualTo(SaveService.CurrentDataVersion));
            Assert.That(store.ReplaceCount, Is.EqualTo(1));
        }

        [Test]
        public async Task DuplicatePull_StoresMaterialWithoutAutomaticAscension()
        {
            var store = CreateStore(
                "{\"dataVersion\":5,\"gameData\":{\"monsters\":{" +
                "\"ownedMonsters\":[{\"monsterId\":\"tofu_01\",\"ascensionLevel\":1}]," +
                "\"mainPartySlots\":[\"tofu_01\"],\"reservePartySlots\":[]}}}");
            var progress = new GameDataService(new SaveService(store, "memory://ascension-save"));
            await progress.LoadAsync();

            Assert.That(await progress.TryApplyAndSaveAsync(
                GameProgressChange.RecordGachaPull("tofu_01", MonsterRarity.Common)), Is.True);
            Assert.That(progress.View.Monsters.TryGetOwnedMonster("tofu_01", out var owned), Is.True);
            Assert.That(owned.AscensionLevel, Is.EqualTo(1));
            Assert.That(owned.AscensionMaterialCount, Is.EqualTo(1));

            var reloaded = await new SaveService(store, "memory://ascension-save").LoadAsync();
            Assert.That(reloaded.Monsters.TryGetOwnedMonster("tofu_01", out var saved), Is.True);
            Assert.That(saved.AscensionLevel, Is.EqualTo(1));
            Assert.That(saved.AscensionMaterialCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ManualAscension_ConsumesOneMaterialAndRejectsStaleCommand()
        {
            var store = CreateStore(
                "{\"dataVersion\":5,\"gameData\":{\"monsters\":{" +
                "\"ownedMonsters\":[{\"monsterId\":\"tofu_01\",\"ascensionLevel\":1," +
                "\"ascensionMaterialCount\":2}]," +
                "\"mainPartySlots\":[\"tofu_01\"],\"reservePartySlots\":[]}}}");
            var progress = new GameDataService(new SaveService(store, "memory://ascension-save"));
            await progress.LoadAsync();

            Assert.That(await progress.TryApplyAndSaveAsync(
                GameProgressChange.AscendMonster("tofu_01", 1)), Is.True);
            Assert.That(await progress.TryApplyAndSaveAsync(
                GameProgressChange.AscendMonster("tofu_01", 1)), Is.False);
            Assert.That(progress.View.Monsters.TryGetOwnedMonster("tofu_01", out var stageTwo), Is.True);
            Assert.That(stageTwo.AscensionLevel, Is.EqualTo(2));
            Assert.That(stageTwo.AscensionMaterialCount, Is.EqualTo(1));
            Assert.That(store.ReplaceCount, Is.EqualTo(1));

            Assert.That(await progress.TryApplyAndSaveAsync(
                GameProgressChange.AscendMonster("tofu_01", 2)), Is.True);
            Assert.That(progress.View.Monsters.TryGetOwnedMonster("tofu_01", out var stageThree), Is.True);
            Assert.That(stageThree.AscensionLevel, Is.EqualTo(3));
            Assert.That(stageThree.AscensionMaterialCount, Is.Zero);
            Assert.That(store.ReplaceCount, Is.EqualTo(2));
        }

        [Test]
        public async Task SurplusDuplicates_ConvertToCurrencyBeforeAndAfterMaximumAscension()
        {
            var store = CreateStore(
                "{\"dataVersion\":5,\"gameData\":{\"ascensionCurrency\":7,\"monsters\":{" +
                "\"ownedMonsters\":[{\"monsterId\":\"tofu_01\",\"ascensionLevel\":4," +
                "\"ascensionMaterialCount\":1}]," +
                "\"mainPartySlots\":[\"tofu_01\"],\"reservePartySlots\":[]}}}");
            var progress = new GameDataService(new SaveService(store, "memory://ascension-save"));
            await progress.LoadAsync();

            Assert.That(await progress.TryApplyAndSaveAsync(
                GameProgressChange.RecordGachaPull("tofu_01", MonsterRarity.Common)), Is.True);
            Assert.That(progress.View.AscensionCurrency, Is.EqualTo(8));
            Assert.That(progress.View.Monsters.TryGetOwnedMonster("tofu_01", out var reserved), Is.True);
            Assert.That(reserved.AscensionMaterialCount, Is.EqualTo(1));

            Assert.That(await progress.TryApplyAndSaveAsync(
                GameProgressChange.AscendMonster("tofu_01", 4)), Is.True);
            Assert.That(progress.View.Monsters.TryGetOwnedMonster("tofu_01", out var maximum), Is.True);
            Assert.That(maximum.AscensionLevel, Is.EqualTo(MonsterAscension.MaxAscensionLevel));
            Assert.That(maximum.AscensionMaterialCount, Is.Zero);

            Assert.That(await progress.TryApplyAndSaveAsync(
                GameProgressChange.RecordGachaPull("tofu_01", MonsterRarity.Common)), Is.True);
            Assert.That(progress.View.AscensionCurrency, Is.EqualTo(9));
            Assert.That(await progress.TryApplyAndSaveAsync(
                GameProgressChange.AscendMonster("tofu_01", MonsterAscension.MaxAscensionLevel)), Is.False);
        }

        [Test]
        public async Task ManagementPage_BreakthroughButtonRunsManualTransaction()
        {
            var store = CreateStore(
                "{\"dataVersion\":5,\"gameData\":{\"monsters\":{" +
                "\"ownedMonsters\":[{\"monsterId\":\"tofu_01\",\"ascensionLevel\":1," +
                "\"ascensionMaterialCount\":1}]," +
                "\"mainPartySlots\":[\"tofu_01\"],\"reservePartySlots\":[]}}}");
            var progress = new GameDataService(new SaveService(store, "memory://ascension-save"));
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
                    ?.Invoke(controller, null); // EditMode 인스턴스에 Runtime 버튼 연결 재현
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("previewAnchor").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo(); // EditMode에서는 Preview 인스턴스를 만들지 않음
                controller.Configure(progress, catalog);
                controller.OpenPage();

                var tabButton = (Button)serialized.FindProperty("breakthroughTabButton").objectReferenceValue;
                var actionButton = (Button)serialized.FindProperty("breakthroughActionButton").objectReferenceValue;
                tabButton.onClick.Invoke();
                Assert.That(actionButton.interactable, Is.True);

                actionButton.onClick.Invoke();
                await Task.Yield();

                Assert.That(progress.View.Monsters.TryGetOwnedMonster("tofu_01", out var owned), Is.True);
                Assert.That(owned.AscensionLevel, Is.EqualTo(2));
                Assert.That(owned.AscensionMaterialCount, Is.Zero);
                Assert.That(store.ReplaceCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public async Task MonsterCard_ShowsNoticeOnlyWhileManualAscensionIsReady()
        {
            var store = CreateStore(
                "{\"dataVersion\":5,\"gameData\":{\"monsters\":{" +
                "\"ownedMonsters\":[{\"monsterId\":\"tofu_01\",\"ascensionLevel\":1," +
                "\"ascensionMaterialCount\":1}]," +
                "\"mainPartySlots\":[\"tofu_01\"],\"reservePartySlots\":[]}}}");
            var progress = new GameDataService(new SaveService(store, "memory://ascension-save"));
            await progress.LoadAsync();
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(CatalogPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterCardPath);
            var instance = Object.Instantiate(prefab);
            try
            {
                Assert.That(catalog.TryGet("tofu_01", out var definition), Is.True);
                Assert.That(progress.View.Monsters.TryGetOwnedMonster("tofu_01", out var ready), Is.True);
                var card = instance.GetComponent<MonsterCardView>();
                var serialized = new SerializedObject(card);
                var notice = (GameObject)serialized
                    .FindProperty("breakthroughReadyBadge")
                    .objectReferenceValue;
                var levelBadge = instance.transform.Find("LevelBadge").GetComponent<RectTransform>();
                var assignmentBadge = instance.transform.Find("AssignmentBadge").GetComponent<RectTransform>();
                var noticeRect = notice.GetComponent<RectTransform>();

                Assert.That(levelBadge.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(levelBadge.anchoredPosition, Is.EqualTo(new Vector2(6f, 30f)));
                Assert.That(levelBadge.GetComponent<Image>().enabled, Is.False);
                Assert.That(assignmentBadge.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(assignmentBadge.anchoredPosition, Is.EqualTo(new Vector2(6f, -6f)));
                Assert.That(noticeRect.anchorMin, Is.EqualTo(Vector2.one));
                Assert.That(noticeRect.anchoredPosition, Is.EqualTo(new Vector2(-5f, -5f)));

                card.BindMonster(definition, ready, false, "본대", null);
                Assert.That(notice.activeSelf, Is.True);

                Assert.That(await progress.TryApplyAndSaveAsync(
                    GameProgressChange.AscendMonster("tofu_01", ready.AscensionLevel)), Is.True);
                Assert.That(progress.View.Monsters.TryGetOwnedMonster("tofu_01", out var consumed), Is.True);
                card.BindMonster(definition, consumed, false, "본대", null);
                Assert.That(notice.activeSelf, Is.False);

                card.BindEmpty("빈 슬롯");
                Assert.That(notice.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static MemoryFileStore CreateStore(string json)
        {
            return new MemoryFileStore(Encoding.UTF8.GetBytes(json));
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

            public Task ReplaceAsync(string path, byte[] replacement)
            {
                Bytes = replacement;
                ReplaceCount++;
                return Task.CompletedTask;
            }
        }
    }
}
