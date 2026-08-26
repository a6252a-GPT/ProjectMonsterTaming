using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace ProjectMT.Contents.CastleRaidHex.Editor.Tests
{
    public sealed class HexCastleGarrisonCatalogTests
    {
        private const string CatalogPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Resources/HexCastleGarrisonCatalog.asset";

        [Test]
        public void Catalog_UsesFormalKnightTiersAndPeasantOnly()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HexCastleGarrisonCatalog>(CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.IsComplete, Is.True);
            Assert.That(catalog.KnightPrefabs.Select(AssetDatabase.GetAssetPath), Is.EquivalentTo(new[]
            {
                "Assets/ProjectMT/03_Features/Expedition/Prefabs/PF_Enemy_Knight_T1.prefab",
                "Assets/ProjectMT/03_Features/Expedition/Prefabs/PF_Enemy_Knight_T2.prefab",
                "Assets/ProjectMT/03_Features/Expedition/Prefabs/PF_Enemy_Knight_T3.prefab"
            }));
            Assert.That(AssetDatabase.GetAssetPath(catalog.FarmerPrefab), Is.EqualTo(
                "Assets/ProjectMT/03_Features/Expedition/Prefabs/PF_Enemy_Peasant.prefab"));
        }
    }
}
