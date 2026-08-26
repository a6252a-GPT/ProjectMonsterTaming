using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    public enum HexCastleTheme
    {
        CentralCompartment = 0,
        DiamondRadial = 1,
        CompositeCompartments = 2,
        HexHoneycomb = 3,
        PetalBloom = 4,
        CrystalMandala = 5,
        FractalBastion = 6,
        VoronoiCrystal = 7,
        IrisShutter = 8
    }

    public enum HexCastleWallRole
    {
        None,
        OuterPerimeter,
        InnerDefense,
        CoreDefense,
        Partition
    }

    public enum HexCastleLootKind
    {
        None,
        Gold,
        Equipment,
        Key
    }

    [Serializable]
    public readonly struct HexCastleGenerationRequest : IEquatable<HexCastleGenerationRequest>
    {
        public HexCastleGenerationRequest(
            int seed,
            HexCastleTheme theme,
            int defenseLayerCount,
            int battlefieldRadius,
            int buildRadius,
            int palaceRadius,
            int difficultyLevel = 0)
        {
            Seed = seed;
            Theme = theme;
            DefenseLayerCount = defenseLayerCount;
            BattlefieldRadius = battlefieldRadius;
            BuildRadius = buildRadius;
            PalaceRadius = palaceRadius;
            DifficultyLevel = Mathf.Clamp(difficultyLevel, 0, 10);
        }

        public int Seed { get; }
        public HexCastleTheme Theme { get; }
        public int DefenseLayerCount { get; }
        public int BattlefieldRadius { get; }
        public int BuildRadius { get; }
        public int PalaceRadius { get; }
        public int DifficultyLevel { get; }

        public bool Equals(HexCastleGenerationRequest other)
        {
            return Seed == other.Seed && Theme == other.Theme &&
                   DefenseLayerCount == other.DefenseLayerCount &&
                   BattlefieldRadius == other.BattlefieldRadius &&
                   BuildRadius == other.BuildRadius && PalaceRadius == other.PalaceRadius &&
                   DifficultyLevel == other.DifficultyLevel;
        }

        public override bool Equals(object obj)
        {
            return obj is HexCastleGenerationRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Seed;
                hash = hash * 397 ^ (int)Theme;
                hash = hash * 397 ^ DefenseLayerCount;
                hash = hash * 397 ^ BattlefieldRadius;
                hash = hash * 397 ^ BuildRadius;
                hash = hash * 397 ^ PalaceRadius;
                hash = hash * 397 ^ DifficultyLevel;
                return hash;
            }
        }
    }

    public static class HexCastleThemeCatalog
    {
        private static readonly HexCastleTheme[] themes =
        {
            HexCastleTheme.CentralCompartment,
            HexCastleTheme.DiamondRadial,
            HexCastleTheme.CompositeCompartments,
            HexCastleTheme.HexHoneycomb,
            HexCastleTheme.PetalBloom,
            HexCastleTheme.CrystalMandala,
            HexCastleTheme.FractalBastion,
            HexCastleTheme.VoronoiCrystal,
            HexCastleTheme.IrisShutter
        };

        private static readonly HexCastleTheme[] comparisonThemes =
        {
            HexCastleTheme.CentralCompartment,
            HexCastleTheme.DiamondRadial,
            HexCastleTheme.CompositeCompartments,
            HexCastleTheme.HexHoneycomb,
            HexCastleTheme.PetalBloom,
            HexCastleTheme.CrystalMandala,
            HexCastleTheme.FractalBastion,
            HexCastleTheme.VoronoiCrystal,
            HexCastleTheme.IrisShutter
        };

        public static IReadOnlyList<HexCastleTheme> Themes => themes;
        public static IReadOnlyList<HexCastleTheme> ComparisonThemes => comparisonThemes;

        public static HexCastleTheme ResolveNextProceduralTheme(
            HexCastleTheme currentTheme,
            int seed,
            int difficultyLevel)
        {
            if (themes.Length <= 1)
            {
                return themes[0];
            }

            var currentIndex = Array.IndexOf(themes, currentTheme);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int mixed;
            unchecked
            {
                mixed = seed * 397 ^ Mathf.Clamp(difficultyLevel, 1, 10) * 31;
            }

            var offset = 1 + PositiveModulo(mixed, themes.Length - 1);
            return themes[(currentIndex + offset) % themes.Length];
        }

        public static char ResolveCode(HexCastleTheme theme)
        {
            return (char)('A' + Mathf.Clamp((int)theme, 0, themes.Length - 1));
        }

        public static string ResolveKoreanName(HexCastleTheme theme)
        {
            switch (theme)
            {
                case HexCastleTheme.CentralCompartment:
                    return "중앙 격실 요새";
                case HexCastleTheme.DiamondRadial:
                    return "마름모 방사형 요새";
                case HexCastleTheme.CompositeCompartments:
                    return "복합 격실 요새";
                case HexCastleTheme.HexHoneycomb:
                    return "육각 벌집 요새";
                case HexCastleTheme.PetalBloom:
                    return "꽃잎 군락 요새";
                case HexCastleTheme.CrystalMandala:
                    return "수정 만다라 요새";
                case HexCastleTheme.FractalBastion:
                    return "프랙탈 능보 요새";
                case HexCastleTheme.VoronoiCrystal:
                    return "보로노이 수정 요새";
                case HexCastleTheme.IrisShutter:
                    return "홍채 셔터 요새";
                default:
                    return theme.ToString();
            }
        }

        public static string ResolveLabel(HexCastleTheme theme)
        {
            return $"{ResolveCode(theme)} · {ResolveKoreanName(theme)}";
        }

        public static string ResolveDescription(HexCastleTheme theme)
        {
            switch (theme)
            {
                case HexCastleTheme.CentralCompartment:
                    return "축 격실과 모서리 구획을 밀착한 중심형";
                case HexCastleTheme.DiamondRadial:
                    return "여섯 방사축과 계단형 대각 구획";
                case HexCastleTheme.CompositeCompartments:
                    return "엇갈린 소형 격실을 벽돌처럼 결합";
                case HexCastleTheme.HexHoneycomb:
                    return "육각 셀 군집과 여왕방을 가진 벌집형";
                case HexCastleTheme.PetalBloom:
                    return "여섯 꽃잎과 겹꽃 고리가 퍼지는 곡선형";
                case HexCastleTheme.CrystalMandala:
                    return "짧고 긴 꼭짓점을 교차한 수정 별형";
                case HexCastleTheme.FractalBastion:
                    return "반복되는 단계형 능보와 톱니 외곽";
                case HexCastleTheme.VoronoiCrystal:
                    return "Seed 결정 지점의 최근접 영역으로 갈린 수정형";
                case HexCastleTheme.IrisShutter:
                    return "여섯 블레이드가 비틀려 닫히는 조리개형";
                default:
                    return string.Empty;
            }
        }

        public static Color ResolveAccent(HexCastleTheme theme)
        {
            var hue = Mathf.Repeat(0.48f + (int)theme * 0.071f, 1f);
            return Color.HSVToRGB(hue, 0.62f, 1f);
        }

        private static int PositiveModulo(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
