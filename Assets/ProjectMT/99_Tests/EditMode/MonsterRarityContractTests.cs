using System;
using System.Linq;
using NUnit.Framework;
using ProjectMT.Shared.Gacha;
using ProjectMT.Shared.Unit;
using UnityEditor;

namespace ProjectMT.Tests.EditMode
{
    public sealed class MonsterRarityContractTests // 5등급 숫자·카탈로그·뽑기 설정 회귀 검사
    {
        private const string RarityCatalogPath =
            "Assets/ProjectMT/02_Shared/Unit/Data/MonsterRarityCatalog.asset";
        private const string GachaProbabilityPath =
            "Assets/ProjectMT/02_Shared/Unit/Data/GachaProbability.asset";

        [Test]
        public void MonsterRarity_UsesFiveContiguousValues()
        {
            var values = Enum.GetValues(typeof(MonsterRarity)).Cast<MonsterRarity>().ToArray();

            Assert.That(values, Is.EqualTo(new[]
            {
                MonsterRarity.Common,
                MonsterRarity.Rare,
                MonsterRarity.Epic,
                MonsterRarity.Legendary,
                MonsterRarity.Mythic
            }));
            Assert.That(values.Select(value => (int)value), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
        }

        [Test]
        public void SeedRarityCatalog_UsesOnlyFiveGrades()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterRarityCatalog>(RarityCatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.GetMonstersOfRarity(MonsterRarity.Common), Has.Count.EqualTo(4));
            Assert.That(catalog.GetMonstersOfRarity(MonsterRarity.Rare), Has.Count.EqualTo(1));
            Assert.That(catalog.GetMonstersOfRarity(MonsterRarity.Epic), Has.Count.EqualTo(1));
            Assert.That(catalog.GetMonstersOfRarity(MonsterRarity.Legendary), Has.Count.EqualTo(1));
            Assert.That(catalog.GetMonstersOfRarity(MonsterRarity.Mythic), Has.Count.EqualTo(1));
        }

        [Test]
        public void GachaProbability_UsesConfirmedFiveGradeRatesAndPity()
        {
            var probability = AssetDatabase.LoadAssetAtPath<GachaProbability>(GachaProbabilityPath);

            Assert.That(probability, Is.Not.Null);
            Assert.That(probability.RarityRates, Has.Count.EqualTo(5));
            Assert.That(probability.RarityRates.Sum(rate => rate.DropRatePercent), Is.EqualTo(100f).Within(0.0001f));
            AssertRate(probability, MonsterRarity.Common, 68f, 0, 0);
            AssertRate(probability, MonsterRarity.Rare, 20f, 0, 10);
            AssertRate(probability, MonsterRarity.Epic, 8f, 30, 0);
            AssertRate(probability, MonsterRarity.Legendary, 3f, 100, 0);
            AssertRate(probability, MonsterRarity.Mythic, 1f, 300, 0);
        }

        private static void AssertRate(
            GachaProbability probability,
            MonsterRarity rarity,
            float dropRatePercent,
            int ceilingPulls,
            int rareGuaranteeInterval)
        {
            var rates = probability.RarityRates.Where(rate => rate.Rarity == rarity).ToArray();

            Assert.That(rates, Has.Length.EqualTo(1), $"{rarity} 확률 행은 정확히 하나여야 합니다.");
            Assert.That(rates[0].DropRatePercent, Is.EqualTo(dropRatePercent).Within(0.0001f));
            Assert.That(rates[0].CeilingPulls, Is.EqualTo(ceilingPulls));
            Assert.That(rates[0].RareGuaranteeInterval, Is.EqualTo(rareGuaranteeInterval));
        }
    }
}
