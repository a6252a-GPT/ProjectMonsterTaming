using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ProjectMT.Core.SaveIO;
using ProjectMT.Features.Expedition;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Tests.EditMode
{
    public sealed class ExpeditionRewardContractTests // 원정대 보상 수직 흐름 회귀 검사
    {
        private const string CatalogPath =
            "Assets/ProjectMT/02_Shared/Unit/Data/MonsterCatalog.asset";

        [Test]
        public async Task VersionThreeTemporaryGold_MigratesToVersionFourGold()
        {
            var store = CreateStore(
                "{\"dataVersion\":3,\"gameData\":{" +
                "\"currentChallengeStage\":4,\"lastClearedStage\":3,\"temporaryGold\":77}}");
            var loaded = await new SaveService(store, "memory://project-mt-save").LoadAsync();
            var migrated = JsonUtility.FromJson<SaveEnvelope>(Encoding.UTF8.GetString(store.Bytes));

            Assert.That(loaded.Gold, Is.EqualTo(77L));
            Assert.That(loaded.CurrentChallengeStage, Is.EqualTo(4));
            Assert.That(loaded.LastClearedStage, Is.EqualTo(3));
            Assert.That(migrated.dataVersion, Is.EqualTo(4));
            Assert.That(migrated.gameData.Gold, Is.EqualTo(77L));
            Assert.That(store.ReplaceCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ChallengeVictory_AdvancesAndGrantsFirstRewardOnlyOnce()
        {
            var store = CreateStore(
                "{\"dataVersion\":4,\"gameData\":{\"currentChallengeStage\":1,\"expeditionMode\":0}}");
            var progress = new GameDataService(new SaveService(store, "memory://project-mt-save"));
            await progress.LoadAsync();
            var rewards = ExpeditionFirstClearRewardRules.Create(1);

            Assert.That(await progress.TryApplyAndSaveAsync(
                GameProgressChange.RecordExpeditionFirstClear(1, rewards)), Is.True);
            Assert.That(progress.View.CurrentChallengeStage, Is.EqualTo(2));
            Assert.That(progress.View.LastClearedStage, Is.EqualTo(1));
            Assert.That(progress.View.Gold, Is.EqualTo(ExpeditionFirstClearRewardRules.Gold));
            Assert.That(store.ReplaceCount, Is.EqualTo(1));

            Assert.That(await progress.TryApplyAndSaveAsync(
                GameProgressChange.RecordExpeditionFirstClear(1, rewards)), Is.False);
            Assert.That(progress.View.Gold, Is.EqualTo(ExpeditionFirstClearRewardRules.Gold));
            Assert.That(store.ReplaceCount, Is.EqualTo(1));
        }

        [Test]
        public async Task RepeatVictory_GrantsRewardEverySavedRunWithoutAdvancingStage()
        {
            var store = CreateStore(
                "{\"dataVersion\":4,\"gameData\":{" +
                "\"currentChallengeStage\":2,\"lastClearedStage\":1,\"expeditionMode\":1}}");
            var progress = new GameDataService(new SaveService(store, "memory://project-mt-save"));
            await progress.LoadAsync();
            var rewards = ExpeditionRepeatClearRewardRules.Create(1);

            Assert.That(await progress.TryApplyAndSaveAsync(
                GameProgressChange.RecordExpeditionRepeatClear(1, rewards)), Is.True);
            Assert.That(await progress.TryApplyAndSaveAsync(
                GameProgressChange.RecordExpeditionRepeatClear(1, rewards)), Is.True);

            Assert.That(progress.View.CurrentChallengeStage, Is.EqualTo(2));
            Assert.That(progress.View.LastClearedStage, Is.EqualTo(1));
            Assert.That(progress.View.Gold, Is.EqualTo(ExpeditionRepeatClearRewardRules.Gold * 2L));
            Assert.That(store.ReplaceCount, Is.EqualTo(2));
        }

        [Test]
        public async Task InvalidExpeditionMode_IsRepairedAndCannotBeSavedAgain()
        {
            var store = CreateStore(
                "{\"dataVersion\":4,\"gameData\":{" +
                "\"currentChallengeStage\":2,\"lastClearedStage\":1,\"expeditionMode\":99}}");
            var progress = new GameDataService(new SaveService(store, "memory://project-mt-save"));
            await progress.LoadAsync();

            Assert.That(progress.View.ExpeditionMode, Is.EqualTo(ExpeditionRunMode.Challenge));
            Assert.That(await progress.TryApplyAndSaveAsync(
                GameProgressChange.SetExpeditionMode((ExpeditionRunMode)99)), Is.False);
            Assert.That(progress.View.ExpeditionMode, Is.EqualTo(ExpeditionRunMode.Challenge));
            Assert.That(store.ReplaceCount, Is.EqualTo(0));
        }

        [Test]
        public async Task FirstAndRepeatClearCommands_CannotCrossRunModes()
        {
            var repeatStore = CreateStore(
                "{\"dataVersion\":4,\"gameData\":{" +
                "\"currentChallengeStage\":2,\"lastClearedStage\":1,\"expeditionMode\":1}}");
            var repeatProgress = new GameDataService(
                new SaveService(repeatStore, "memory://repeat-save"));
            await repeatProgress.LoadAsync();

            Assert.That(await repeatProgress.TryApplyAndSaveAsync(
                GameProgressChange.RecordExpeditionFirstClear(
                    2,
                    ExpeditionFirstClearRewardRules.Create(2))), Is.False);
            Assert.That(repeatProgress.View.Gold, Is.Zero);
            Assert.That(repeatStore.ReplaceCount, Is.Zero);

            var challengeStore = CreateStore(
                "{\"dataVersion\":4,\"gameData\":{" +
                "\"currentChallengeStage\":2,\"lastClearedStage\":1,\"expeditionMode\":0}}");
            var challengeProgress = new GameDataService(
                new SaveService(challengeStore, "memory://challenge-save"));
            await challengeProgress.LoadAsync();

            Assert.That(await challengeProgress.TryApplyAndSaveAsync(
                GameProgressChange.RecordExpeditionRepeatClear(
                    1,
                    ExpeditionRepeatClearRewardRules.Create(1))), Is.False);
            Assert.That(challengeProgress.View.Gold, Is.Zero);
            Assert.That(challengeStore.ReplaceCount, Is.Zero);
        }

        [Test]
        public async Task VictoryLevelUpAndReload_PreserveProgressAndApplyHigherStats()
        {
            var store = CreateStore(
                "{\"dataVersion\":4,\"gameData\":{\"currentChallengeStage\":1,\"expeditionMode\":0}}");
            var progress = new GameDataService(new SaveService(store, "memory://project-mt-save"));
            await progress.LoadAsync();

            Assert.That(await progress.TryApplyAndSaveAsync(
                GameProgressChange.RecordExpeditionFirstClear(
                    1,
                    ExpeditionFirstClearRewardRules.Create(1))), Is.True);
            Assert.That(await progress.TryApplyAndSaveAsync(
                GameProgressChange.LevelUpMonster("tofu_01", 1)), Is.True);

            var reloaded = new GameDataService(new SaveService(store, "memory://project-mt-save"));
            await reloaded.LoadAsync();
            Assert.That(reloaded.View.Gold, Is.EqualTo(10L));
            Assert.That(reloaded.View.CurrentChallengeStage, Is.EqualTo(2));
            Assert.That(reloaded.View.LastClearedStage, Is.EqualTo(1));
            Assert.That(reloaded.View.Monsters.TryGetOwnedMonster("tofu_01", out var owned), Is.True);
            Assert.That(owned.Level, Is.EqualTo(2));

            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryGet("tofu_01", out var definition), Is.True);
            var party = new BattlePartySnapshotBuilder(catalog).Build(reloaded.View);
            Assert.That(
                party.Units[0].Stats.maxHealth,
                Is.EqualTo(definition.MaxHealth * MonsterLevelRules.GetStatMultiplier(2)).Within(0.001f));
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
