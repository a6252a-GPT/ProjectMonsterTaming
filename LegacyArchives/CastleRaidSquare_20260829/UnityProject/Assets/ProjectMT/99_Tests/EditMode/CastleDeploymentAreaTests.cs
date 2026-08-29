using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectMT.Contents.CastleRaid;
using ProjectMT.Contents.CastleRaid.Generation;
using ProjectMT.EditorTools.CastleBake;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Tests.EditMode
{
    public sealed class CastleDeploymentAreaTests
    {
        private const string RulesPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/Data/Generation/CastleGenerationRules_Default.asset";

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void ExteriorArea_LeavesOneCellClearanceAroundEveryWall(int defenseLayerCount)
        {
            var candidate = new CastleGenerator().Generate(
                LoadRules(),
                20260816,
                CastleLayoutTheme.CentralCompartmentFortress,
                defenseLayerCount);
            var bounds = CastleGenerationScenePreview.ResolveSquareDisplayBounds(candidate);
            var exterior = new HashSet<Vector2Int>(
                CastleDeploymentAreaResolver.ResolveExteriorCells(candidate, bounds));

            Assert.That(exterior, Is.Not.Empty);
            foreach (var placement in candidate.Placements)
            {
                foreach (var cell in exterior)
                {
                    Assert.That(placement.Occupies(cell.x, cell.y), Is.False,
                        $"{placement.PlacementId} 위에 배치 셀이 겹쳤습니다: {cell}");
                    if (placement.Kind != CastlePlacementKind.Wall)
                    {
                        continue;
                    }

                    var wall = placement.Bounds;
                    var isWithinClearance = cell.x >= wall.xMin - 1 && cell.x <= wall.xMax &&
                                            cell.y >= wall.yMin - 1 && cell.y <= wall.yMax;
                    Assert.That(isWithinClearance, Is.False,
                        $"성벽 {placement.PlacementId}의 1칸 여백에 배치 셀이 있습니다: {cell}");
                }
            }
        }

        [Test]
        public void TwinSpiralExterior_ReachesBeyondTheOldRectangularBelt()
        {
            var candidate = new CastleGenerator().Generate(
                LoadRules(),
                10801,
                CastleLayoutTheme.TwinSpiralFortress,
                3);
            var bounds = CastleGenerationScenePreview.ResolveSquareDisplayBounds(candidate);
            var exterior = CastleDeploymentAreaResolver.ResolveExteriorCells(candidate, bounds);

            Assert.That(exterior.Any(cell => !CastleGenerationScenePreview.IsPreviewDeploymentCell(cell, bounds)), Is.True,
                "쌍나선 성의 굴곡진 외곽까지 배치 영역이 확장되지 않았습니다.");
        }

        [Test]
        public void DeploymentZone_UsesExactCellMaskAndSmoothColliderFreeVisual()
        {
            var host = new GameObject("CastleDeploymentAreaTest");
            try
            {
                var zone = host.AddComponent<CastleDeploymentZone>();
                zone.ConfigureExteriorCells(
                    new RectInt(10, 20, 4, 4),
                    new[]
                    {
                        new Vector2Int(10, 20),
                        new Vector2Int(11, 20),
                        new Vector2Int(10, 21)
                    },
                    1f);

                Assert.That(zone.UsesExteriorCellMask, Is.True);
                Assert.That(zone.AllowedCellCount, Is.EqualTo(3));
                Assert.That(zone.ContainsWorldPosition(new Vector3(-1.5f, 0f, -1.5f)), Is.True);
                Assert.That(zone.ContainsWorldPosition(new Vector3(0.5f, 0f, 0.5f)), Is.False);
                Assert.That(host.GetComponentsInChildren<Collider>(true), Is.Empty);

                var visual = host.transform.Find("DeploymentAreaVisual/DeploymentAreaSmooth");
                Assert.That(visual, Is.Not.Null);
                var texture = visual.GetComponent<MeshRenderer>().sharedMaterial.mainTexture as Texture2D;
                Assert.That(texture, Is.Not.Null);
                Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Bilinear));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static CastleGenerationRules LoadRules()
        {
            var rules = AssetDatabase.LoadAssetAtPath<CastleGenerationRules>(RulesPath);
            Assert.That(rules, Is.Not.Null, RulesPath);
            return rules;
        }
    }
}
