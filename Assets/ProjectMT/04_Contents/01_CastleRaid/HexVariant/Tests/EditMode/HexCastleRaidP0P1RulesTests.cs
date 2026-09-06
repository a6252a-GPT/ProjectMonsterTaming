using System.Collections.Generic;
using NUnit.Framework;
using ProjectMT.Contents.Framework;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex.Tests
{
    public sealed class HexCastleRaidP0P1RulesTests
    {
        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(3, 2)]
        [TestCase(4, 2)]
        [TestCase(5, 3)]
        [TestCase(99, 3)]
        [TestCase(-3, 1)]
        public void SummonsPerSlot_FollowsAscensionBands(int ascension, int expected)
        {
            Assert.That(HexCastleRaidStartData.ResolveSummonsForAscension(ascension), Is.EqualTo(expected));
        }

        [Test]
        public void StartData_UsesPerSlotAscensionAndSumsDeploymentLimit()
        {
            var party = new BattlePartySnapshot(new[]
            {
                Unit("zero", 0, 0),
                Unit("three", 1, 3),
                Unit("five", 2, 5)
            });
            var startData = new HexCastleRaidStartData(party);

            Assert.That(startData.UnitSlotCount, Is.EqualTo(8));
            Assert.That(startData.ResolveSummonsForSlot(0), Is.EqualTo(1));
            Assert.That(startData.ResolveSummonsForSlot(1), Is.EqualTo(2));
            Assert.That(startData.ResolveSummonsForSlot(2), Is.EqualTo(3));
            Assert.That(startData.DeploymentLimit, Is.EqualTo(6));
            Assert.That(startData.SummonsPerSlot, Is.EqualTo(3));
        }

        [Test]
        public void StartData_CapsLegacyTenSlotSnapshotAtFiveMainPartyUnits()
        {
            var units = new BattleUnitSnapshot[10];
            for (var index = 0; index < units.Length; index++)
            {
                units[index] = Unit($"legacy_{index}", index, 5);
            }

            var startData = new HexCastleRaidStartData(new BattlePartySnapshot(units));
            Assert.That(startData.UnitSlotCount, Is.EqualTo(8));
            Assert.That(startData.DeploymentLimit, Is.EqualTo(15));
            Assert.That(startData.ResolveSummonsForSlot(5), Is.Zero);
        }

        [Test]
        public void StartData_IncludesThreeReservesAndPreservesEmptyFormationSlots()
        {
            var main = Unit("main", 1, 0);
            var reserve = Unit("reserve", 7, 5);
            var party = new BattlePartySnapshot(new[] { main }, new[] { reserve });
            var startData = new HexCastleRaidStartData(party);
            Assert.That(startData.Party, Is.SameAs(party));
            Assert.That(startData.UnitSlotCount, Is.EqualTo(8));
            Assert.That(startData.DeploymentUnits[1], Is.SameAs(main));
            Assert.That(startData.DeploymentUnits[7], Is.SameAs(reserve));
            Assert.That(startData.DeploymentUnits[0], Is.Null);
            Assert.That(startData.ResolveSummonsForSlot(0), Is.Zero);
            Assert.That(startData.ResolveSummonsForSlot(7), Is.EqualTo(3));
            Assert.That(startData.DeploymentLimit, Is.EqualTo(4));
            Assert.That(party.Units.Length, Is.EqualTo(1));
            Assert.That(party.ReserveUnits.Length, Is.EqualTo(1));
        }

        [Test]
        public void EquipmentRewardContext_RetryKeepsIdLevelAndOptions()
        {
            var data = GameProgressData.CreateDefault();
            JsonUtility.FromJsonOverwrite(@"{""lastClearedStage"":20}", data);
            var context = new HexEquipmentRewardContext();
            context.Initialize(new GameProgressView(data));
            var balance = ScriptableObject.CreateInstance<EquipmentBalanceConfig>();
            try
            {
                var coordinates = new HexCoordinates(3, -2);
                var first = context.Resolve(coordinates, 0, 12345, balance);
                var retry = context.Resolve(coordinates, 0, 12345, balance);
                Assert.That(retry.InstanceId, Is.EqualTo(first.InstanceId));
                Assert.That(retry.ItemLevel, Is.EqualTo(first.ItemLevel));
                Assert.That(retry.RandomOptions.Count, Is.EqualTo(first.RandomOptions.Count));
                for (var index = 0; index < first.RandomOptions.Count; index++)
                {
                    Assert.That(retry.RandomOptions[index].Type, Is.EqualTo(first.RandomOptions[index].Type));
                    Assert.That(retry.RandomOptions[index].Value, Is.EqualTo(first.RandomOptions[index].Value));
                }
                Assert.That(first.ItemLevel, Is.InRange(18, 20));
                Assert.That(context.BasisStage, Is.EqualTo(20));
            }
            finally
            {
                Object.DestroyImmediate(balance);
            }
        }

        [TestCase(1, 10000L)]
        [TestCase(50, 59000L)]
        [TestCase(100, 109000L)]
        public void GoldReward_IsGenerousAndStageScaled(int stage, long expected)
        {
            Assert.That(HexCastleLootRules.ResolveGoldTotal(stage), Is.EqualTo(expected));
        }

        [TestCase(1, 2)]
        [TestCase(5, 4)]
        [TestCase(10, 6)]
        public void EquipmentForgeReward_GrantsActualEquipmentByDifficulty(int difficulty, int expected)
        {
            Assert.That(HexCastleLootRules.ResolveEquipmentTotal(difficulty), Is.EqualTo(expected));
        }

        [TestCase(1, 2)]
        [TestCase(3, 2)]
        [TestCase(4, 3)]
        [TestCase(7, 4)]
        [TestCase(10, 6)]
        public void KeyReward_FollowsDifficultyBands(int difficulty, int expected)
        {
            Assert.That(HexCastleLootRules.ResolveKeyTotal(difficulty), Is.EqualTo(expected));
        }

        [Test]
        public void RewardShare_PreservesTotalAcrossMultipleBuildings()
        {
            const long total = 109000L;
            const int count = 4;
            var sum = 0L;
            for (var index = 0; index < count; index++)
            {
                sum += HexCastleLootRules.ResolveShare(total, index, count);
            }

            Assert.That(sum, Is.EqualTo(total));
        }

        [Test]
        public void BattleDuration_IsExactlyThreeMinutes()
        {
            Assert.That(HexCastleRaidController.BattleDurationSeconds, Is.EqualTo(180f));
        }

        [Test]
        public void GarrisonBuildingEffects_AreAppliedAndRevertedFromBaseStats()
        {
            var cellRoot = new GameObject("Cell");
            var unitRoot = new GameObject("Garrison");
            var worldRoot = new GameObject("GarrisonWorld");
            try
            {
                var coordinates = new HexCoordinates(0, 0);
                var cell = cellRoot.AddComponent<HexCastleCellRuntime>();
                var tile = new GameObject("Tile").transform;
                tile.SetParent(cellRoot.transform, false);
                var content = new GameObject("Content").transform;
                content.SetParent(cellRoot.transform, false);
                cell.Configure(
                    new HexCastleCell(coordinates, HexCastleCellKind.Ground, initialBlocked: false),
                    null,
                    null,
                    tile,
                    content);

                var visual = new GameObject("Visual").transform;
                visual.SetParent(unitRoot.transform, false);
                var tuning = HexCastleThemeOneTuning.CreateDraftDefaults();
                var unit = unitRoot.AddComponent<HexCastleGarrisonUnit>();
                unit.Configure(
                    HexCastleGarrisonUnitRole.Knight,
                    coordinates,
                    0,
                    visual,
                    new Dictionary<HexCoordinates, HexCastleCellRuntime> { [coordinates] = cell },
                    null,
                    Vector3.zero,
                    1f,
                    tuning);
                var baseAttack = unit.AttackDamage;
                var baseMoveSpeed = unit.MoveSpeed;

                var world = worldRoot.AddComponent<HexCastleGarrisonWorld>();
                typeof(HexCastleGarrisonWorld)
                    .GetField("tuning", System.Reflection.BindingFlags.Instance |
                                       System.Reflection.BindingFlags.NonPublic)
                    ?.SetValue(world, tuning);
                var units = typeof(HexCastleGarrisonWorld)
                    .GetField("units", System.Reflection.BindingFlags.Instance |
                                      System.Reflection.BindingFlags.NonPublic)
                    ?.GetValue(world) as List<HexCastleGarrisonUnit>;
                Assert.That(units, Is.Not.Null);
                units.Add(unit);

                world.ApplyBuildingEffects(hasActiveTrainingYard: true, churchDestroyed: false);
                Assert.That(world.TrainingAttackMultiplier,
                    Is.EqualTo(tuning.TrainingAttackMultiplier).Within(0.001f));
                Assert.That(unit.AttackDamage,
                    Is.EqualTo(baseAttack * tuning.TrainingAttackMultiplier).Within(0.001f));
                Assert.That(unit.MoveSpeed, Is.EqualTo(baseMoveSpeed).Within(0.001f));

                world.ApplyBuildingEffects(hasActiveTrainingYard: false, churchDestroyed: true);
                Assert.That(unit.AttackDamage, Is.EqualTo(baseAttack).Within(0.001f),
                    "연습장 파괴 뒤 공격력은 원래 값으로 돌아가야 합니다.");
                Assert.That(world.ChurchMoveSpeedMultiplier,
                    Is.EqualTo(tuning.ChurchRageMoveSpeedMultiplier).Within(0.001f));
                Assert.That(unit.MoveSpeed,
                    Is.EqualTo(baseMoveSpeed * tuning.ChurchRageMoveSpeedMultiplier).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(worldRoot);
                Object.DestroyImmediate(unitRoot);
                Object.DestroyImmediate(cellRoot);
            }
        }

        [Test]
        public async System.Threading.Tasks.Task ResultAdapter_SettlesLootOnlyOnVictoryAndAllowsReplayLoot()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(
                "Assets/ProjectMT/02_Shared/Items/Data/ItemCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            var adapter = ScriptableObject.CreateInstance<HexCastleRaidResultAdapter>();
            try
            {
                var service = new InMemoryGameProgressService(catalog);
                var runInfo = new ContentRunInfo(
                    new ContentId("castle_raid"),
                    "1",
                    ContentRunMode.Challenge);
                var failedLoot = new RewardBundle(
                    9999L,
                    0L,
                    new[] { new ItemAmount(ItemIds.EquipmentSlotUpgradeStone, 99L) });
                var failed = new HexCastleRaidResult(
                    false,
                    failedLoot,
                    new[] { Equipment("failed_equipment", EquipmentPart.Weapon) },
                    HexCastleRaidFailureReason.TimeExpired);
                Assert.That(adapter.TryCreateProgressChange(failed, service.View, runInfo, out _), Is.False);
                Assert.That(service.View.CastleRaidHighestClearedStage, Is.EqualTo(0));
                Assert.That(service.View.Gold, Is.EqualTo(0L));
                Assert.That(Quantity(service.View, ItemIds.EquipmentSlotUpgradeStone), Is.EqualTo(0L));
                Assert.That(service.View.Equipment.Instances.Count, Is.EqualTo(0));

                var firstLoot = new RewardBundle(
                    10000L,
                    0L,
                    System.Array.Empty<ItemAmount>());
                Assert.That(adapter.TryCreateProgressChange(
                    new HexCastleRaidResult(
                        true,
                        firstLoot,
                        new[] { Equipment("first_equipment", EquipmentPart.Helmet) }),
                    service.View,
                    runInfo,
                    out var firstChange), Is.True);
                Assert.That(await service.TryApplyAndSaveAsync(firstChange), Is.True);
                Assert.That(service.View.CastleRaidHighestClearedStage, Is.EqualTo(1));
                Assert.That(service.View.Gold, Is.EqualTo(10000L));
                Assert.That(Quantity(service.View, ItemIds.Diamond), Is.EqualTo(300L));
                Assert.That(Quantity(service.View, ItemIds.MonsterSummonTicket), Is.EqualTo(10L));
                Assert.That(Quantity(service.View, ItemIds.EquipmentSlotUpgradeStone), Is.EqualTo(0L));
                Assert.That(service.View.Equipment.Instances.Count, Is.EqualTo(1));

                var repeatDiamonds = Quantity(service.View, ItemIds.Diamond);
                var repeatTickets = Quantity(service.View, ItemIds.MonsterSummonTicket);
                var repeatLoot = new RewardBundle(
                    3500L,
                    0L,
                    new[] { new ItemAmount(ItemIds.FoodRiotKey, 2L) });
                var farmingRun = new ContentRunInfo(
                    new ContentId("castle_raid"),
                    "1",
                    ContentRunMode.Farming);
                Assert.That(adapter.TryCreateProgressChange(
                    new HexCastleRaidResult(
                        true,
                        repeatLoot,
                        new[] { Equipment("repeat_equipment", EquipmentPart.Ring) }),
                    service.View,
                    farmingRun,
                    out var repeatChange), Is.True);
                Assert.That(await service.TryApplyAndSaveAsync(repeatChange), Is.True);
                Assert.That(service.View.CastleRaidHighestClearedStage, Is.EqualTo(1));
                Assert.That(service.View.Gold, Is.EqualTo(13500L));
                Assert.That(Quantity(service.View, ItemIds.Diamond), Is.EqualTo(repeatDiamonds));
                Assert.That(Quantity(service.View, ItemIds.MonsterSummonTicket), Is.EqualTo(repeatTickets));
                Assert.That(Quantity(service.View, ItemIds.FoodRiotKey), Is.EqualTo(2L));
                Assert.That(service.View.Equipment.Instances.Count, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(adapter);
            }
        }

        private static long Quantity(GameProgressView view, string itemId)
        {
            return view.Items.TryGetQuantity(itemId, out var quantity) ? quantity : 0L;
        }

        private static EquipmentInstanceData Equipment(string id, EquipmentPart part)
        {
            return new EquipmentInstanceData(
                id,
                part,
                EquipmentGrade.Common, 1,
                new List<EquipmentOptionRollData>());
        }

        private static BattleUnitSnapshot Unit(string id, int slot, int ascension)
        {
            return new BattleUnitSnapshot(
                id,
                new UnitStatsSnapshot(),
                presentation: new MonsterBattlePresentationSnapshot(
                    null,
                    MonsterRarity.Common,
                    slot,
                    1,
                    ascension));
        }
    }
}

