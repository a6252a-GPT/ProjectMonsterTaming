using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    public enum HexCastlePreviewColorMode
    {
        Architecture,
        Analysis
    }

    public readonly struct HexCastleLegendEntry
    {
        public HexCastleLegendEntry(string label, Color color)
        {
            Label = label;
            Color = color;
        }

        public string Label { get; }
        public Color Color { get; }
    }

    public static class HexCastleVisualPalette
    {
        private const int DeploymentIndex = 0;
        private const int GroundIndex = 1;
        private const int PartitionWallIndex = 2;
        private const int WallIndex = 3;
        private const int TowerIndex = 4;
        private const int ClosedGateIndex = 5;
        private const int OpenGateIndex = 6;
        private const int KnightBarracksIndex = 7;
        private const int FarmerBarracksIndex = 8;
        private const int TurretIndex = 9;
        private const int TrainingYardIndex = 10;
        private const int ChurchIndex = 11;
        private const int GoldStorageIndex = 12;
        private const int EquipmentForgeIndex = 13;
        private const int KeyVaultIndex = 14;
        private const int BlockerIndex = 15;
        private const int PalaceIndex = 16;

        public static IReadOnlyList<HexCastleLegendEntry> Legend { get; } = new[]
        {
            new HexCastleLegendEntry("배치 벨트", Rgb(8, 87, 80)),
            new HexCastleLegendEntry("지면", Rgb(30, 41, 46)),
            new HexCastleLegendEntry("격벽", Rgb(48, 72, 105)),
            new HexCastleLegendEntry("성벽", Rgb(139, 169, 192)),
            new HexCastleLegendEntry("성벽 탑", Rgb(91, 111, 140)),
            new HexCastleLegendEntry("닫힌 외곽 성문", Rgb(242, 153, 74)),
            new HexCastleLegendEntry("열린 수비대 성문", Rgb(45, 223, 197)),
            new HexCastleLegendEntry("기사병영", Rgb(47, 107, 255)),
            new HexCastleLegendEntry("농부병영", Rgb(139, 214, 70)),
            new HexCastleLegendEntry("포탑", Rgb(255, 59, 48)),
            new HexCastleLegendEntry("연습장", Rgb(155, 81, 224)),
            new HexCastleLegendEntry("교회", Rgb(242, 167, 212)),
            new HexCastleLegendEntry("골드 건물", Rgb(242, 201, 76)),
            new HexCastleLegendEntry("장비 건물", Rgb(39, 174, 96)),
            new HexCastleLegendEntry("열쇠 건물", Rgb(176, 0, 58)),
            new HexCastleLegendEntry("일반 길막 건물", Rgb(155, 91, 54)),
            new HexCastleLegendEntry("왕궁", Rgb(255, 244, 176))
        };

        public static Color ResolveColor(HexCastleCell cell, HexCastleTheme theme, HexCastlePreviewColorMode mode)
        {
            if (mode == HexCastlePreviewColorMode.Analysis)
            {
                if (cell.IsWallPathCell && cell.DefenseLayer > 0)
                {
                    return Color.HSVToRGB(Mathf.Repeat(0.58f - cell.DefenseLayer * 0.11f, 1f), 0.72f, 0.95f);
                }
                if (cell.DistrictId > 0 && cell.Kind != HexCastleCellKind.Palace)
                {
                    return Color.HSVToRGB(Mathf.Repeat(cell.DistrictId / 6f, 1f), 0.48f, 0.78f);
                }
            }

            switch (cell.Kind)
            {
                case HexCastleCellKind.Deployment: return Legend[DeploymentIndex].Color;
                case HexCastleCellKind.Ground: return Legend[GroundIndex].Color;
                case HexCastleCellKind.Wall:
                    return cell.WallRole == HexCastleWallRole.Partition
                        ? Legend[PartitionWallIndex].Color
                        : Legend[WallIndex].Color;
                case HexCastleCellKind.Tower: return Legend[TowerIndex].Color;
                case HexCastleCellKind.Gate:
                    return cell.GateRole == HexCastleGateRole.OpenDefenderPassage
                        ? Legend[OpenGateIndex].Color
                        : Legend[ClosedGateIndex].Color;
                case HexCastleCellKind.Building:
                case HexCastleCellKind.DefenseBuilding:
                case HexCastleCellKind.RewardBuilding:
                    return ResolveBuildingColor(cell.BuildingRole);
                case HexCastleCellKind.Palace:
                    return Legend[PalaceIndex].Color;
                default: return Color.gray;
            }
        }

        private static Color ResolveBuildingColor(HexCastleBuildingRole role)
        {
            switch (role)
            {
                case HexCastleBuildingRole.KnightBarracks: return Legend[KnightBarracksIndex].Color;
                case HexCastleBuildingRole.FarmerBarracks: return Legend[FarmerBarracksIndex].Color;
                case HexCastleBuildingRole.Turret: return Legend[TurretIndex].Color;
                case HexCastleBuildingRole.TrainingYard: return Legend[TrainingYardIndex].Color;
                case HexCastleBuildingRole.Church: return Legend[ChurchIndex].Color;
                case HexCastleBuildingRole.GoldStorage: return Legend[GoldStorageIndex].Color;
                case HexCastleBuildingRole.EquipmentForge: return Legend[EquipmentForgeIndex].Color;
                case HexCastleBuildingRole.KeyVault: return Legend[KeyVaultIndex].Color;
                case HexCastleBuildingRole.Blocker: return Legend[BlockerIndex].Color;
                default: return Color.gray;
            }
        }

        private static Color Rgb(byte red, byte green, byte blue)
        {
            return new Color32(red, green, blue, 255);
        }

        public static float ResolveHeight(HexCastleCell cell)
        {
            if (cell == null) return 0.04f;
            switch (cell.Kind)
            {
                case HexCastleCellKind.Deployment: return 0.075f;
                case HexCastleCellKind.Ground: return 0.045f;
                case HexCastleCellKind.Wall:
                case HexCastleCellKind.Tower:
                case HexCastleCellKind.Gate:
                    return cell.WallRole == HexCastleWallRole.Partition ? 0.43f : 0.58f + cell.DefenseLayer * 0.11f;
                case HexCastleCellKind.Building: return 0.58f;
                case HexCastleCellKind.RewardBuilding: return 0.78f;
                case HexCastleCellKind.Defense: return 0.78f + cell.DefenseLayer * 0.08f;
                case HexCastleCellKind.Palace: return cell.Coordinates.DistanceFromOrigin == 0 ? 1.34f : 0.96f;
                default: return 0.04f;
            }
        }
    }
}
