using System;
using System.Collections.Generic;
using System.IO;
using ProjectMT.Contents.CastleRaid.Generation;
using UnityEngine;

namespace ProjectMT.EditorTools.CastleBake
{
    public readonly struct CastlePreviewLegendEntry
    {
        public CastlePreviewLegendEntry(string category, string label, Color color)
        {
            Category = category;
            Label = label;
            Color = color;
        }

        public string Category { get; }
        public string Label { get; }
        public Color Color { get; }
    }

    public static class CastleGenerationPreviewExporter // 도구 미리보기와 PNG 검증의 공통 렌더러
    {
        public const int DefaultCellPixels = 12;

        public static Color PalaceColor => new Color(1f, 0.72f, 0.08f);
        public static Color MandatoryPathColor => new Color(1f, 0.42f, 0.08f);
        public static Color BuildingColor => new Color(0.62f, 0.65f, 0.68f);
        public static Color DefenseBuildingColor => new Color(0.84f, 0.18f, 0.13f);
        public static Color DefenderColor => new Color(0.72f, 0.22f, 0.82f);
        public static Color GoldLootColor => new Color(0.98f, 0.82f, 0.12f);
        public static Color EquipmentLootColor => new Color(0.15f, 0.82f, 0.78f);
        public static Color KeyLootColor => new Color(0.18f, 0.42f, 0.96f);
        public static Color PalaceCoreFloorColor => new Color(0.40f, 0.34f, 0.14f);
        public static Color InnerRingFloorColor => new Color(0.22f, 0.38f, 0.25f);
        public static Color OuterRingFloorColor => new Color(0.19f, 0.31f, 0.37f);
        public static Color BuildableFloorColor => new Color(0.17f, 0.25f, 0.17f);
        public static Color DeploymentMarginColor => new Color(0.08f, 0.11f, 0.10f);
        public static Color InvalidDataColor => Color.magenta;

        private static readonly CastlePreviewLegendEntry[] legendEntries =
        {
            new CastlePreviewLegendEntry("공략·건물", "왕궁", PalaceColor),
            new CastlePreviewLegendEntry("공략·건물", "최단 공략 필수 파괴 대상", MandatoryPathColor),
            new CastlePreviewLegendEntry("공략·건물", "일반 건물", BuildingColor),
            new CastlePreviewLegendEntry("공략·건물", "방어 건물", DefenseBuildingColor),
            new CastlePreviewLegendEntry("공략·건물", "수비대", DefenderColor),
            new CastlePreviewLegendEntry("보상 건물", "골드 보상", GoldLootColor),
            new CastlePreviewLegendEntry("보상 건물", "장비 보상", EquipmentLootColor),
            new CastlePreviewLegendEntry("보상 건물", "열쇠 보상", KeyLootColor),
            new CastlePreviewLegendEntry("성벽", "최외곽 성벽", ResolveWallBandColor(CastleWallBand.OuterPerimeter, 1, 0)),
            new CastlePreviewLegendEntry("성벽", "내부 방어 성벽", ResolveWallBandColor(CastleWallBand.InnerDefense, 2, 1)),
            new CastlePreviewLegendEntry("성벽", "왕궁 방어 성벽", ResolveWallBandColor(CastleWallBand.CoreDefense, 2, 2)),
            new CastlePreviewLegendEntry("성벽", "격실 사이 격벽", ResolveWallBandColor(CastleWallBand.Partition, 2, 1)),
            new CastlePreviewLegendEntry("구역 바닥", "왕궁 코어 내부", PalaceCoreFloorColor),
            new CastlePreviewLegendEntry("구역 바닥", "내부 방어 링", InnerRingFloorColor),
            new CastlePreviewLegendEntry("구역 바닥", "외곽 방어 링", OuterRingFloorColor),
            new CastlePreviewLegendEntry("구역 바닥", "건설 가능 빈 땅", BuildableFloorColor),
            new CastlePreviewLegendEntry("구역 바닥", "외곽 배치 여백", DeploymentMarginColor),
            new CastlePreviewLegendEntry("오류 표시", "미지정·잘못된 데이터", InvalidDataColor)
        };

        public static IReadOnlyList<CastlePreviewLegendEntry> LegendEntries => legendEntries;

        public static string ExportToTemp(CastleGenerationCandidate candidate, int cellPixels = DefaultCellPixels)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                              ?? throw new InvalidOperationException("Unity 프로젝트 루트를 찾지 못했습니다.");
            var outputRoot = Path.Combine(projectRoot, "Temp", "CastleRaidPreviews");
            return ExportToFolder(candidate, outputRoot, cellPixels);
        }

        public static string ExportToFolder(
            CastleGenerationCandidate candidate,
            string outputRoot,
            int cellPixels = DefaultCellPixels)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (string.IsNullOrWhiteSpace(outputRoot))
            {
                throw new ArgumentException("출력 폴더가 비어 있습니다.", nameof(outputRoot));
            }

            Directory.CreateDirectory(outputRoot);
            var outputPath = Path.Combine(
                outputRoot,
                $"Castle_{candidate.Theme}_{candidate.RequestedDefenseLayerCount}Layers_Seed{candidate.Seed}.png");
            var texture = Render(candidate, cellPixels);
            try
            {
                File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            return outputPath;
        }

        public static Texture2D Render(CastleGenerationCandidate candidate, int cellPixels)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            cellPixels = Mathf.Clamp(cellPixels, 4, 32);
            var texture = new Texture2D(
                candidate.GridWidth * cellPixels,
                candidate.GridHeight * cellPixels,
                TextureFormat.RGBA32,
                false)
            {
                name = $"CastlePreview_{candidate.Theme}_{candidate.RequestedDefenseLayerCount}Layers_{candidate.Seed}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var placements = BuildPlacementCells(candidate);
            var compartmentRoles = BuildCompartmentCells(candidate);
            var mandatory = new HashSet<string>(candidate.Difficulty.MandatoryPlacementIds, StringComparer.Ordinal);
            for (var x = 0; x < candidate.GridWidth; x++)
            {
                for (var z = 0; z < candidate.GridHeight; z++)
                {
                    var color = ResolveColor(
                        x,
                        z,
                        placements[x, z],
                        compartmentRoles[x, z],
                        mandatory);
                    PaintCell(texture, x, z, cellPixels, color);
                }
            }

            texture.Apply(false, false);
            return texture;
        }

        public static Color ResolvePlacementColor(CastlePlacementData placement, ISet<string> mandatory)
        {
            if (placement == null)
            {
                return Color.clear;
            }

            if (placement.Kind == CastlePlacementKind.Palace)
            {
                return PalaceColor;
            }

            if (mandatory != null && mandatory.Contains(placement.PlacementId))
            {
                return MandatoryPathColor;
            }

            switch (placement.Kind)
            {
                case CastlePlacementKind.Wall:
                    return ResolveWallBandColor(placement);
                case CastlePlacementKind.Building:
                    return BuildingColor;
                case CastlePlacementKind.DefenseBuilding:
                    return DefenseBuildingColor;
                case CastlePlacementKind.Defender:
                    return DefenderColor;
                case CastlePlacementKind.LootBuilding:
                    switch (placement.LootKind)
                    {
                        case CastleLootKind.Gold:
                            return GoldLootColor;
                        case CastleLootKind.Equipment:
                            return EquipmentLootColor;
                        case CastleLootKind.Key:
                            return KeyLootColor;
                    }

                    break;
            }

            return InvalidDataColor;
        }

        private static Color ResolveWallBandColor(CastlePlacementData placement)
        {
            return ResolveWallBandColor(
                placement.WallBand,
                placement.WallTier,
                placement.WallDefenseLayer);
        }

        public static Color ResolveWallBandColor(
            CastleWallBand wallBand,
            int wallTier,
            int wallDefenseLayer)
        {
            Color baseColor;
            switch (wallBand)
            {
                case CastleWallBand.OuterPerimeter:
                    baseColor = new Color(0.39f, 0.20f, 0.08f);
                    break;
                case CastleWallBand.InnerDefense:
                    baseColor = new Color(0.48f, 0.44f, 0.38f);
                    break;
                case CastleWallBand.CoreDefense:
                    baseColor = new Color(0.90f, 0.70f, 0.24f);
                    break;
                case CastleWallBand.Partition:
                    baseColor = new Color(0.32f, 0.39f, 0.46f);
                    break;
                default:
                    baseColor = new Color(0.68f, 0.16f, 0.68f);
                    break;
            }

            var tierLight = Mathf.InverseLerp(1f, 5f, wallTier) * 0.22f;
            var layerLight = Mathf.Clamp01(wallDefenseLayer / 3f) * 0.10f;
            return Color.Lerp(baseColor, Color.white, tierLight + layerLight);
        }

        public static Color ResolveFloorColor(Vector2Int cell, CastleCompartmentRole? role)
        {
            if (role.HasValue)
            {
                switch (role.Value)
                {
                    case CastleCompartmentRole.PalaceCore:
                        return PalaceCoreFloorColor;
                    case CastleCompartmentRole.InnerRing:
                        return InnerRingFloorColor;
                    case CastleCompartmentRole.OuterRing:
                        return OuterRingFloorColor;
                    default:
                        return InvalidDataColor;
                }
            }

            return CastleSpatialContract.BuildableBounds.Contains(cell)
                ? BuildableFloorColor
                : DeploymentMarginColor;
        }

        private static Color ResolveColor(
            int x,
            int z,
            CastlePlacementData placement,
            CastleCompartmentRole? role,
            ISet<string> mandatory)
        {
            if (placement != null)
            {
                return ResolvePlacementColor(placement, mandatory);
            }

            return ResolveFloorColor(new Vector2Int(x, z), role);
        }

        private static CastlePlacementData[,] BuildPlacementCells(CastleGenerationCandidate candidate)
        {
            var result = new CastlePlacementData[candidate.GridWidth, candidate.GridHeight];
            foreach (var placement in candidate.Placements)
            {
                for (var x = placement.X; x < placement.X + placement.Width; x++)
                {
                    for (var z = placement.Z; z < placement.Z + placement.Height; z++)
                    {
                        if (x >= 0 && z >= 0 && x < candidate.GridWidth && z < candidate.GridHeight)
                        {
                            result[x, z] = placement;
                        }
                    }
                }
            }

            return result;
        }

        private static CastleCompartmentRole?[,] BuildCompartmentCells(CastleGenerationCandidate candidate)
        {
            var result = new CastleCompartmentRole?[candidate.GridWidth, candidate.GridHeight];
            foreach (var compartment in candidate.Compartments)
            {
                foreach (var cell in compartment.EnumerateFootprintCells())
                {
                    if (cell.x >= 0 && cell.y >= 0 &&
                        cell.x < candidate.GridWidth && cell.y < candidate.GridHeight)
                    {
                        result[cell.x, cell.y] = compartment.Role;
                    }
                }
            }

            return result;
        }

        private static void PaintCell(Texture2D texture, int cellX, int cellZ, int cellPixels, Color color)
        {
            var pixelX = cellX * cellPixels;
            var pixelZ = cellZ * cellPixels;
            var border = new Color(color.r * 0.45f, color.g * 0.45f, color.b * 0.45f, 1f);
            for (var x = 0; x < cellPixels; x++)
            {
                for (var z = 0; z < cellPixels; z++)
                {
                    texture.SetPixel(
                        pixelX + x,
                        pixelZ + z,
                        x == 0 || z == 0 ? border : color);
                }
            }
        }
    }
}
