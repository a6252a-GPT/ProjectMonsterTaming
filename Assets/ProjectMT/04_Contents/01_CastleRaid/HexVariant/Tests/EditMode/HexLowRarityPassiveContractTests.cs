using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex.Tests
{
    public sealed class HexLowRarityPassiveContractTests
    {
        private static readonly string[] TargetMonsterIds =
        {
            "piru_01", "kir_01", "wispy_01", "shell_01", "aru_01", "rage_01", "dubi_01",
            "poi_poison_01", "pipi_01", "nerea_01", "doomba_01", "argo_01", "grimpy_01",
            "rako_01", "hanjaemon_ice_01", "kutan_01", "astell_01", "candy_tree_01",
            "rubea_01", "lumi_01", "krabi_01", "shakun_01", "ru_01", "pango_01", "berkan_01"
        };

        [Test]
        public void HexAiCatalog_ContainsAllCommonRareEpicAssignments()
        {
            var catalog = Resources.Load<HexCastleAssaultAIProfileCatalog>(
                HexCastleAssaultAIProfileCatalog.DefaultResourcesPath);
            Assert.That(catalog, Is.Not.Null);
            foreach (var monsterId in TargetMonsterIds)
            {
                Assert.That(catalog.Entries.Any(value => value != null && string.Equals(
                    value.MonsterId,
                    monsterId,
                    StringComparison.OrdinalIgnoreCase)), Is.True, monsterId);
            }
            Assert.That(catalog.Resolve("nerea_01").Pattern, Is.EqualTo(HexCastleAssaultPattern.DefenderHunter));
            Assert.That(catalog.Resolve("aru_01").SupportFocus, Is.EqualTo(HexCastleAssaultSupportFocus.DefenseBuff));
            Assert.That(catalog.Resolve("ru_01").SupportFocus, Is.EqualTo(HexCastleAssaultSupportFocus.Recovery));
        }
    }
}
