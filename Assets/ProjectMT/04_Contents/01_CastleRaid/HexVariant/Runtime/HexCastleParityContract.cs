using System;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    public static class HexCastleParityContract
    {
        public const int SquareBattlefieldSize = 50;
        public const int SquareBuildAreaSize = 44;
        public const int DeploymentMargin = 3;
        public const int PalaceFootprintSize = 4;
        public const int MinimumDefenseLayerCount = 2;
        public const int MaximumDefenseLayerCount = 4;
        public const int MinimumCompartmentCount = 8;
        public const int MaximumCompartmentCount = 60;
        public const int PlacementAttemptsPerCompartment = 160;
        public const int CompartmentSpacing = 1;
        public const int MinimumWallTier = 1;
        public const int MaximumWallTier = 3;
        public const int PalaceWallTier = 2;
        public const float PalaceHealth = 700f;
        public const float BuildingHealth = 140f;
        public const float DefenseBuildingHealth = 180f;
        public const float DefenderHealth = 120f;
        public const float LootBuildingHealth = 160f;
        public const int MaximumSpecialCompartmentCount = 3;
        public const int MaximumGoldCompartmentCount = 1;
        public const int MaximumEquipmentCompartmentCount = 1;
        public const int MaximumKeyCompartmentCount = 1;
        public const int MaximumRewardBudget = 120;

        private static readonly float[] WallTierHealth = { 0f, 100f, 180f, 300f };
        private static readonly int[] CanonicalWallRadii = { 5, 10, 14, 17 };

        public static int ResolveCompartmentCount(HexCastleTheme theme, int defenseLayerCount, int seed)
        {
            ValidateDefenseLayers(defenseLayerCount);
            if (theme == HexCastleTheme.CompositeCompartments)
            {
                return defenseLayerCount == 2 ? 12 : defenseLayerCount == 3 ? 32 : 60;
            }
            if (theme == HexCastleTheme.HexHoneycomb)
            {
                var minimum = defenseLayerCount == 2 ? 8 : defenseLayerCount == 3 ? 18 : 30;
                var maximum = defenseLayerCount == 2 ? 12 : defenseLayerCount == 3 ? 26 : 42;
                var span = maximum - minimum + 1;
                return minimum + PositiveModulo(seed * 31 + defenseLayerCount * 17, span);
            }
            if (theme == HexCastleTheme.PetalBloom ||
                theme == HexCastleTheme.CrystalMandala ||
                theme == HexCastleTheme.FractalBastion ||
                theme == HexCastleTheme.VoronoiCrystal ||
                theme == HexCastleTheme.IrisShutter)
            {
                return defenseLayerCount == 2 ? 8 : defenseLayerCount == 3 ? 16 : 24;
            }
            return defenseLayerCount == 2
                ? 8 + PositiveModulo(seed, 3)
                : defenseLayerCount == 3 ? 20 : 36;
        }

        public static int ResolveWallRadius(int zeroBasedLayer)
        {
            if (zeroBasedLayer < 0 || zeroBasedLayer >= CanonicalWallRadii.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(zeroBasedLayer));
            }
            return CanonicalWallRadii[zeroBasedLayer];
        }

        public static int ResolveWallTier(int oneBasedDefenseLayer)
        {
            if (oneBasedDefenseLayer < 1 || oneBasedDefenseLayer > MaximumDefenseLayerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(oneBasedDefenseLayer));
            }
            return oneBasedDefenseLayer == 1 ? 3 : 2;
        }

        public static float ResolveWallHealth(int tier)
        {
            return WallTierHealth[Mathf.Clamp(tier, MinimumWallTier, MaximumWallTier)];
        }

        public static int ResolveRewardBudget(HexCastleLootKind lootKind)
        {
            switch (lootKind)
            {
                case HexCastleLootKind.Gold:
                    return 30;
                case HexCastleLootKind.Equipment:
                    return 60;
                case HexCastleLootKind.Key:
                    return 30;
                default:
                    return 0;
            }
        }

        private static void ValidateDefenseLayers(int defenseLayerCount)
        {
            if (defenseLayerCount < MinimumDefenseLayerCount ||
                defenseLayerCount > MaximumDefenseLayerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(defenseLayerCount));
            }
        }

        private static int PositiveModulo(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
