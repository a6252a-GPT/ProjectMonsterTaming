using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ProjectMT.Core.SaveIO;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Tests.EditMode
{
    public sealed class MonsterPartyContractTests // 실제 보유·편성·Catalog 계약 검사
    {
        private const string CatalogPath =
            "Assets/ProjectMT/02_Shared/Unit/Data/MonsterCatalog.asset";
        private const string MeleePath =
            "Assets/ProjectMT/02_Shared/Unit/Data/Definitions/Monster_Tofu01.asset";
        private const string RangedPath =
            "Assets/ProjectMT/02_Shared/Unit/Data/Definitions/Monster_Tofu02.asset";

        [Test]
        public void DefaultProfile_OwnsAndDeploysOnlyStarterMonster()
        {
            var progress = new GameProgressView(GameProgressData.CreateDefault());

            Assert.That(progress.Monsters.OwnedMonsterIds, Is.EqualTo(new[] { "tofu_01" }));
            Assert.That(progress.Monsters.MainPartySlots.Count, Is.EqualTo(5));
            Assert.That(progress.Monsters.MainPartySlots[0], Is.EqualTo("tofu_01"));
            Assert.That(progress.Monsters.ReservePartySlots.Count, Is.EqualTo(2));
            Assert.That(progress.Monsters.ReservePartySlots[0], Is.Empty);
            Assert.That(progress.Monsters.ReservePartySlots[1], Is.Empty);
        }

        [Test]
        public async Task VersionOneSave_MigratesWithoutLosingProgress()
        {
            const string versionOneJson =
                "{\"dataVersion\":1,\"savedAtUtc\":\"2026-08-04T00:00:00Z\"," +
                "\"gameData\":{\"currentChallengeStage\":4,\"lastClearedStage\":3," +
                "\"temporaryGold\":27,\"commander\":{\"level\":6,\"experience\":4321}}}";
            var store = new RecordingFileStore(Encoding.UTF8.GetBytes(versionOneJson));
            var service = new SaveService(store, "memory://project-mt-save");

            var loaded = await service.LoadAsync();
            var migrated = JsonUtility.FromJson<SaveEnvelope>(Encoding.UTF8.GetString(store.Bytes));

            Assert.That(loaded.CurrentChallengeStage, Is.EqualTo(4));
            Assert.That(loaded.LastClearedStage, Is.EqualTo(3));
            Assert.That(loaded.TemporaryGold, Is.EqualTo(27));
            Assert.That(loaded.Commander.Level, Is.EqualTo(6));
            Assert.That(loaded.Commander.Experience, Is.EqualTo(4321));
            Assert.That(loaded.Monsters.MainPartySlots[0], Is.EqualTo("tofu_01"));
            Assert.That(store.ReplaceCount, Is.EqualTo(1));
            Assert.That(migrated.dataVersion, Is.EqualTo(SaveService.CurrentDataVersion));
        }

        [Test]
        public async Task MissingSave_CreatesVersionTwoProfileBeforeReturn()
        {
            var store = new RecordingFileStore(null);
            var service = new SaveService(store, "memory://project-mt-save");

            var loaded = await service.LoadAsync();
            var saved = JsonUtility.FromJson<SaveEnvelope>(Encoding.UTF8.GetString(store.Bytes));

            Assert.That(store.ReplaceCount, Is.EqualTo(1));
            Assert.That(saved.dataVersion, Is.EqualTo(2));
            Assert.That(loaded.Monsters.MainPartySlots[0], Is.EqualTo("tofu_01"));
        }

        [Test]
        public async Task Repair_RemovesInvalidAndRepeatedFormationSlotsInOrder()
        {
            const string json =
                "{\"dataVersion\":2,\"gameData\":{\"monsters\":{" +
                "\"ownedMonsters\":[{\"monsterId\":\"tofu_01\"},{\"monsterId\":\"tofu_02\"}]," +
                "\"mainPartySlots\":[\"tofu_01\",\"tofu_01\",\"ghost\",\"tofu_02\"]," +
                "\"reservePartySlots\":[\"tofu_02\"]}}}";
            var store = new RecordingFileStore(Encoding.UTF8.GetBytes(json));
            var service = new SaveService(store, "memory://project-mt-save");

            var loaded = await service.LoadAsync();

            Assert.That(loaded.Monsters.MainPartySlots[0], Is.EqualTo("tofu_01"));
            Assert.That(loaded.Monsters.MainPartySlots[1], Is.Empty);
            Assert.That(loaded.Monsters.MainPartySlots[2], Is.Empty);
            Assert.That(loaded.Monsters.MainPartySlots[3], Is.EqualTo("tofu_02"));
            Assert.That(loaded.Monsters.ReservePartySlots[0], Is.Empty);
        }

        [Test]
        public void Catalog_ContainsOneMeleeAndOneRangedTofu()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(CatalogPath);
            var melee = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(MeleePath);
            var ranged = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(RangedPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryValidate(out var error), Is.True, error);
            Assert.That(catalog.Definitions, Has.Count.EqualTo(2));
            Assert.That(melee.MonsterId, Is.EqualTo("tofu_01"));
            Assert.That(melee.Ranged, Is.False);
            Assert.That(ranged.MonsterId, Is.EqualTo("tofu_02"));
            Assert.That(ranged.Ranged, Is.True);
        }

        [Test]
        public void Builder_UsesSavedFormationAndCommanderBonusSeparately()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(CatalogPath);
            var definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(MeleePath);
            var progress = new GameProgressView(GameProgressData.CreateDefault());
            var bonus = new LegionStatBonus(0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f);

            var party = new BattlePartySnapshotBuilder(catalog).Build(progress, bonus);
            var stats = party.Units[0].Stats;

            Assert.That(party.Units, Has.Length.EqualTo(1));
            Assert.That(party.Units[0].UnitId, Is.EqualTo("tofu_01"));
            Assert.That(stats.maxHealth, Is.EqualTo(definition.MaxHealth * 1.1f).Within(0.001f));
            Assert.That(stats.damage, Is.EqualTo(definition.AttackPower * 1.2f).Within(0.001f));
            Assert.That(stats.defense, Is.EqualTo(definition.Defense * 1.3f).Within(0.001f));
            Assert.That(stats.attackInterval, Is.EqualTo(1f / (definition.AttackSpeed * 1.4f)).Within(0.001f));
            Assert.That(stats.moveSpeed, Is.EqualTo(definition.MoveSpeed * 1.5f).Within(0.001f));
            Assert.That(stats.attackRange, Is.EqualTo(definition.AttackRange * 1.6f).Within(0.001f));
        }

        [Test]
        public void Catalog_RejectsRepeatedMonsterId()
        {
            var first = ScriptableObject.CreateInstance<MonsterDefinition>();
            var second = ScriptableObject.CreateInstance<MonsterDefinition>();
            var catalog = ScriptableObject.CreateInstance<MonsterCatalog>();
            try
            {
                first.EditorConfigure("duplicate", 1f, 1f, 0f, 1f, 1f, 1f, false);
                second.EditorConfigure("duplicate", 1f, 1f, 0f, 1f, 1f, 1f, true);
                catalog.EditorSetDefinitions(new[] { first, second });

                Assert.That(catalog.TryValidate(out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(first);
            }
        }

        private sealed class RecordingFileStore : IAtomicFileStore // 저장 여부 확인용 메모리 파일
        {
            public RecordingFileStore(byte[] bytes)
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
