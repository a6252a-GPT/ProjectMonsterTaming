using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid.Generation
{
    [CreateAssetMenu(
        menuName = "ProjectMT/Castle Raid/Generation/Generation Rules",
        fileName = "CastleGenerationRules")]
    public sealed class CastleGenerationRules : ScriptableObject // 후보 생성·검수·점수의 단일 규칙
    {
        public const int MinimumDefenseLayerCount = (int)CastleDefenseLayerPreset.Double;
        public const int MaximumDefenseLayerCount = (int)CastleDefenseLayerPreset.Quadruple;
        public const string HexQueenTemplateId = "district_hex_queen_15x13";
        public const string PetalTemplateId = "district_petal_4x4_22x22";
        public const string GeometricTemplateId = "district_geometric_4x4_30x30";

        private static readonly CastleLayoutTheme[] SupportedThemes =
        {
            CastleLayoutTheme.CentralCompartmentFortress,
            CastleLayoutTheme.DiamondRadialFortress,
            CastleLayoutTheme.HoneycombCompartmentFortress,
            CastleLayoutTheme.HexHoneycombFortress,
            CastleLayoutTheme.PetalBloomFortress,
            CastleLayoutTheme.CrystalMandalaFortress,
            CastleLayoutTheme.TwinSpiralFortress,
            CastleLayoutTheme.FractalBastionFortress,
            CastleLayoutTheme.VoronoiCrystalFortress,
            CastleLayoutTheme.IrisShutterFortress
        };

        [SerializeField, Min(1)] private int rulesVersion = 15;
        [SerializeField, Min(15)] private int gridWidth = CastleSpatialContract.BattlefieldSize;
        [SerializeField, Min(15)] private int gridHeight = CastleSpatialContract.BattlefieldSize;
        [SerializeField, Min(1)] private int minimumDistrictCount = 8;
        [SerializeField, Min(1)] private int maximumDistrictCount = 60;
        [SerializeField, Min(16)] private int placementAttemptsPerDistrict = 160;
        [SerializeField, Min(1)] private int mapEdgePadding = CastleSpatialContract.DeploymentMargin;
        [SerializeField, Min(0)] private int districtSpacing = 1;
        [SerializeField, Min(1)] private int palaceSize = CastleSpatialContract.PalaceSize;
        [SerializeField, Min(1f)] private float palaceHealth = 700f;
        [SerializeField, Range(1, 5)] private int palaceWallTier = 2;
        [SerializeField, Range(1, 5)] private int minimumWallTier = 1;
        [SerializeField, Range(1, 5)] private int maximumWallTier = 3;
        [SerializeField] private float[] wallTierHealth = { 0f, 100f, 180f, 300f, 480f, 700f };
        [SerializeField, Min(1f)] private float buildingHealth = 140f;
        [SerializeField, Min(1f)] private float defenseBuildingHealth = 180f;
        [SerializeField, Min(1f)] private float defenderHealth = 120f;
        [SerializeField, Min(1f)] private float lootBuildingHealth = 160f;
        [SerializeField, Min(0)] private int maximumSpecialDistrictCount = 3;
        [SerializeField, Min(0)] private int maximumGoldDistrictCount = 1;
        [SerializeField, Min(0)] private int maximumEquipmentDistrictCount = 1;
        [SerializeField, Min(0)] private int maximumKeyDistrictCount = 1;
        [SerializeField, Min(0)] private int maximumRewardBudget = 120;
        [SerializeField, Min(0)] private int goldRewardBudgetCost = 30;
        [SerializeField, Min(0)] private int equipmentRewardBudgetCost = 60;
        [SerializeField, Min(0)] private int keyRewardBudgetCost = 30;
        [SerializeField] private CastleLayoutThemeRule[] layoutThemeRules = Array.Empty<CastleLayoutThemeRule>();
        [SerializeField] private CastleDistrictTemplate[] templates = Array.Empty<CastleDistrictTemplate>();

        public int RulesVersion => rulesVersion;
        public int GridWidth => gridWidth;
        public int GridHeight => gridHeight;
        public int MinimumDistrictCount => minimumDistrictCount;
        public int MaximumDistrictCount => maximumDistrictCount;
        public int PlacementAttemptsPerDistrict => placementAttemptsPerDistrict;
        public int MapEdgePadding => mapEdgePadding;
        public int DeploymentMargin => mapEdgePadding;
        public RectInt BuildableBounds => new RectInt(
            mapEdgePadding,
            mapEdgePadding,
            gridWidth - mapEdgePadding * 2,
            gridHeight - mapEdgePadding * 2);
        public int DistrictSpacing => districtSpacing;
        public int PalaceSize => palaceSize;
        public float PalaceHealth => palaceHealth;
        public int PalaceWallTier => palaceWallTier;
        public int MinimumWallTier => minimumWallTier;
        public int MaximumWallTier => maximumWallTier;
        public float BuildingHealth => buildingHealth;
        public float DefenseBuildingHealth => defenseBuildingHealth;
        public float DefenderHealth => defenderHealth;
        public float LootBuildingHealth => lootBuildingHealth;
        public int MaximumSpecialDistrictCount => maximumSpecialDistrictCount;
        public int MaximumGoldDistrictCount => maximumGoldDistrictCount;
        public int MaximumEquipmentDistrictCount => maximumEquipmentDistrictCount;
        public int MaximumKeyDistrictCount => maximumKeyDistrictCount;
        public int MaximumRewardBudget => maximumRewardBudget;
        public IReadOnlyList<CastleLayoutThemeRule> LayoutThemeRules => layoutThemeRules;
        public IReadOnlyList<CastleDistrictTemplate> Templates => templates;
        public static IReadOnlyList<CastleLayoutTheme> SupportedLayoutThemes => SupportedThemes;

        public CastleDistrictTemplate PalaceTemplate => templates?.FirstOrDefault(template => template != null && template.IsPalaceCore);
        public CastleDistrictTemplate HexQueenTemplate => templates?.FirstOrDefault(template =>
            template != null && string.Equals(template.TemplateId, HexQueenTemplateId, StringComparison.Ordinal));
        public CastleDistrictTemplate PetalTemplate => templates?.FirstOrDefault(template =>
            template != null && string.Equals(template.TemplateId, PetalTemplateId, StringComparison.Ordinal));
        public CastleDistrictTemplate GeometricTemplate => templates?.FirstOrDefault(template =>
            template != null && string.Equals(template.TemplateId, GeometricTemplateId, StringComparison.Ordinal));
        [Obsolete("폐기된 사각 외곽 링 프로토타입 호환용입니다.")]
        public CastleDistrictTemplate CastleEnvelopeTemplate => null;

        public IEnumerable<CastleDistrictTemplate> EnumerateRegularTemplates()
        {
            return templates?.Where(template =>
                       template != null &&
                       !template.IsPalaceCore &&
                       !string.Equals(template.TemplateId, HexQueenTemplateId, StringComparison.Ordinal) &&
                       !string.Equals(template.TemplateId, PetalTemplateId, StringComparison.Ordinal) &&
                       !string.Equals(template.TemplateId, GeometricTemplateId, StringComparison.Ordinal))
                   ?? Enumerable.Empty<CastleDistrictTemplate>();
        }

        public CastleLayoutThemeRule ResolveThemeRule(CastleLayoutTheme theme)
        {
            var rule = layoutThemeRules?.FirstOrDefault(value => value != null && value.Theme == theme);
            if (rule == null)
            {
                throw new InvalidOperationException($"배치 테마 규칙이 없습니다: {theme}");
            }

            return rule;
        }

        public static void ResolveCompartmentCountRange(
            int defenseLayerCount,
            out int minimumCount,
            out int maximumCount)
        {
            switch (defenseLayerCount)
            {
                case (int)CastleDefenseLayerPreset.Double:
                    minimumCount = 8;
                    maximumCount = 10;
                    return;
                case (int)CastleDefenseLayerPreset.Triple:
                    minimumCount = 20;
                    maximumCount = 20;
                    return;
                case (int)CastleDefenseLayerPreset.Quadruple:
                    minimumCount = 36;
                    maximumCount = 36;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(defenseLayerCount),
                        defenseLayerCount,
                        "성벽 겹 수는 2~4만 지원합니다.");
            }
        }

        public static void ResolveCompartmentCountRange(
            CastleLayoutTheme theme,
            int defenseLayerCount,
            out int minimumCount,
            out int maximumCount)
        {
            if (theme == CastleLayoutTheme.PetalBloomFortress || IsGeometricTheme(theme))
            {
                switch (defenseLayerCount)
                {
                    case (int)CastleDefenseLayerPreset.Double:
                        minimumCount = 8;
                        maximumCount = 8;
                        return;
                    case (int)CastleDefenseLayerPreset.Triple:
                        minimumCount = 16;
                        maximumCount = 16;
                        return;
                    case (int)CastleDefenseLayerPreset.Quadruple:
                        minimumCount = 24;
                        maximumCount = 24;
                        return;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(defenseLayerCount),
                            defenseLayerCount,
                            "성벽 겹 수는 2~4만 지원합니다.");
                }
            }

            if (theme == CastleLayoutTheme.HexHoneycombFortress)
            {
                switch (defenseLayerCount)
                {
                    case (int)CastleDefenseLayerPreset.Double:
                        minimumCount = 8;
                        maximumCount = 12;
                        return;
                    case (int)CastleDefenseLayerPreset.Triple:
                        minimumCount = 18;
                        maximumCount = 26;
                        return;
                    case (int)CastleDefenseLayerPreset.Quadruple:
                        minimumCount = 30;
                        maximumCount = 42;
                        return;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(defenseLayerCount),
                            defenseLayerCount,
                            "성벽 겹 수는 2~4만 지원합니다.");
                }
            }

            if (theme != CastleLayoutTheme.HoneycombCompartmentFortress)
            {
                ResolveCompartmentCountRange(defenseLayerCount, out minimumCount, out maximumCount);
                return;
            }

            switch (defenseLayerCount)
            {
                case (int)CastleDefenseLayerPreset.Double:
                    minimumCount = 12;
                    maximumCount = 12;
                    return;
                case (int)CastleDefenseLayerPreset.Triple:
                    minimumCount = 32;
                    maximumCount = 32;
                    return;
                case (int)CastleDefenseLayerPreset.Quadruple:
                    minimumCount = 60;
                    maximumCount = 60;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(defenseLayerCount),
                        defenseLayerCount,
                        "성벽 겹 수는 2~4만 지원합니다.");
            }
        }

        public static bool IsGeometricTheme(CastleLayoutTheme theme)
        {
            return theme >= CastleLayoutTheme.CrystalMandalaFortress &&
                   theme <= CastleLayoutTheme.IrisShutterFortress;
        }

        public float ResolveWallHealth(int tier)
        {
            if (wallTierHealth == null || wallTierHealth.Length <= 1)
            {
                return 1f;
            }

            var clamped = Mathf.Clamp(tier, 1, wallTierHealth.Length - 1);
            return Mathf.Max(1f, wallTierHealth[clamped]);
        }

        public int ResolveRewardBudgetCost(CastleLootKind lootKind)
        {
            switch (lootKind)
            {
                case CastleLootKind.Gold:
                    return goldRewardBudgetCost;
                case CastleLootKind.Equipment:
                    return equipmentRewardBudgetCost;
                case CastleLootKind.Key:
                    return keyRewardBudgetCost;
                default:
                    return 0;
            }
        }

        public bool TryValidate(out string error)
        {
            if (rulesVersion < 1)
            {
                error = "생성 규칙 버전은 1 이상이어야 합니다.";
                return false;
            }

            if (gridWidth != CastleSpatialContract.BattlefieldSize ||
                gridHeight != CastleSpatialContract.BattlefieldSize ||
                mapEdgePadding != CastleSpatialContract.DeploymentMargin ||
                BuildableBounds.width != CastleSpatialContract.BuildAreaSize ||
                BuildableBounds.height != CastleSpatialContract.BuildAreaSize)
            {
                error = "정식 공간 규칙은 전체 50×50, 중앙 건설 영역 44×44, 사방 배치 여백 3셀이어야 합니다.";
                return false;
            }

            if (minimumDistrictCount < 1 || maximumDistrictCount < minimumDistrictCount)
            {
                error = "일반 구역 수 범위가 잘못됐습니다.";
                return false;
            }

            if (layoutThemeRules == null || layoutThemeRules.Length != SupportedThemes.Length)
            {
                error = "현재 지원하는 배치 테마 규칙이 각각 정확히 1개 필요합니다.";
                return false;
            }

            var themeIds = new HashSet<CastleLayoutTheme>();
            foreach (var themeRule in layoutThemeRules)
            {
                if (themeRule == null || !themeIds.Add(themeRule.Theme) ||
                    themeRule.MinimumCompartmentCount < minimumDistrictCount ||
                    themeRule.MaximumCompartmentCount > maximumDistrictCount ||
                    themeRule.MaximumCompartmentCount < themeRule.MinimumCompartmentCount ||
                    themeRule.MinimumProtectionDepth < 1)
                {
                    error = "배치 테마의 격실 수·보호 깊이·중복 설정이 잘못됐습니다.";
                    return false;
                }
            }

            if (!themeIds.SetEquals(SupportedThemes))
            {
                error = "폐기 테마를 제외한 현재 정식 배치 테마 규칙만 등록해야 합니다.";
                return false;
            }

            if (palaceSize != CastleSpatialContract.PalaceSize)
            {
                error = "정식 왕궁 점유는 4×4여야 합니다.";
                return false;
            }

            if (minimumWallTier < 1 || maximumWallTier < minimumWallTier ||
                wallTierHealth == null || maximumWallTier >= wallTierHealth.Length ||
                palaceWallTier > maximumWallTier || palaceWallTier >= wallTierHealth.Length)
            {
                error = "성벽 등급 범위와 체력표가 맞지 않습니다.";
                return false;
            }

            if (maximumSpecialDistrictCount > maximumDistrictCount ||
                maximumGoldDistrictCount + maximumEquipmentDistrictCount + maximumKeyDistrictCount < maximumSpecialDistrictCount)
            {
                error = "특수 구역 종류별 상한이 전체 특수 구역 상한보다 작습니다.";
                return false;
            }

            if (templates == null || templates.Length < 2)
            {
                error = "왕궁과 일반 구역 템플릿이 필요합니다.";
                return false;
            }

            var palaceCount = 0;
            var regularCount = 0;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var template in templates)
            {
                if (template == null)
                {
                    error = "비어 있는 구역 템플릿 참조가 있습니다.";
                    return false;
                }

                if (!template.TryValidate(out error))
                {
                    return false;
                }

                if (!ids.Add(template.TemplateId))
                {
                    error = $"중복 TemplateId가 있습니다: {template.TemplateId}";
                    return false;
                }

                if (template.IsPalaceCore)
                {
                    palaceCount++;
                }
                else
                {
                    regularCount++;
                }
            }

            if (palaceCount != 1 || regularCount < 1)
            {
                error = "왕궁 템플릿은 정확히 1개이고 가변 일반 템플릿은 1개 이상이어야 합니다.";
                return false;
            }

            var palaceTemplate = PalaceTemplate;
            var palaceInteriorWidth = palaceTemplate.MinimumWidth - palaceTemplate.WallLayers * 2;
            var palaceInteriorHeight = palaceTemplate.MinimumHeight - palaceTemplate.WallLayers * 2;
            if (palaceSize > palaceInteriorWidth || palaceSize > palaceInteriorHeight)
            {
                error = "왕궁 크기가 왕궁 템플릿의 내부 공간보다 큽니다.";
                return false;
            }

            if (!palaceTemplate.HasFixedSize ||
                palaceTemplate.MinimumWidth != 12 ||
                palaceTemplate.MinimumHeight != 12 ||
                palaceTemplate.WallLayers != 1)
            {
                error = "왕궁 코어는 중앙 4×4 왕궁과 호위 공간을 감싸는 12×12 단일 성벽 격실이어야 합니다.";
                return false;
            }

            var hexQueenTemplate = HexQueenTemplate;
            if (hexQueenTemplate == null ||
                !hexQueenTemplate.HasFixedSize ||
                hexQueenTemplate.MinimumWidth != 15 ||
                hexQueenTemplate.MinimumHeight != 13 ||
                hexQueenTemplate.SupportsSpecialLoot)
            {
                error = "육각 벌집 여왕방은 전용 15×13 회전 가능 템플릿이어야 합니다.";
                return false;
            }

            var petalTemplate = PetalTemplate;
            if (petalTemplate == null ||
                petalTemplate.IsPalaceCore ||
                petalTemplate.MinimumWidth != 4 ||
                petalTemplate.MinimumHeight != 4 ||
                petalTemplate.MaximumWidth != 22 ||
                petalTemplate.MaximumHeight != 22 ||
                petalTemplate.WallLayers != 1)
            {
                error = "꽃잎 격실은 4×4~22×22 자유 발자국을 지원하는 전용 단일 성벽 템플릿이어야 합니다.";
                return false;
            }

            var geometricTemplate = GeometricTemplate;
            if (geometricTemplate == null ||
                geometricTemplate.IsPalaceCore ||
                geometricTemplate.MinimumWidth != 4 ||
                geometricTemplate.MinimumHeight != 4 ||
                geometricTemplate.MaximumWidth != 30 ||
                geometricTemplate.MaximumHeight != 30 ||
                geometricTemplate.WallLayers != 1)
            {
                error = "기하학 격실은 4×4~30×30 자유 발자국을 지원하는 전용 단일 성벽 템플릿이어야 합니다.";
                return false;
            }

            if (EnumerateRegularTemplates()
                .Any(template =>
                    template.MinimumWidth < 5 || template.MinimumHeight < 5 ||
                    template.MaximumWidth > 14 || template.MaximumHeight > 14))
            {
                error = "일반 격실 크기 계열은 성벽 포함 5×5~14×14 범위여야 합니다.";
                return false;
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigureDefaults(CastleDistrictTemplate[] generationTemplates)
        {
            rulesVersion = 18;
            gridWidth = CastleSpatialContract.BattlefieldSize;
            gridHeight = CastleSpatialContract.BattlefieldSize;
            minimumDistrictCount = 8;
            maximumDistrictCount = 60;
            placementAttemptsPerDistrict = 160;
            mapEdgePadding = CastleSpatialContract.DeploymentMargin;
            districtSpacing = 1;
            palaceSize = CastleSpatialContract.PalaceSize;
            palaceHealth = 700f;
            palaceWallTier = 2;
            minimumWallTier = 1;
            maximumWallTier = 3;
            wallTierHealth = new[] { 0f, 100f, 180f, 300f, 480f, 700f };
            buildingHealth = 140f;
            defenseBuildingHealth = 180f;
            defenderHealth = 120f;
            lootBuildingHealth = 160f;
            maximumSpecialDistrictCount = 3;
            maximumGoldDistrictCount = 1;
            maximumEquipmentDistrictCount = 1;
            maximumKeyDistrictCount = 1;
            maximumRewardBudget = 120;
            goldRewardBudgetCost = 30;
            equipmentRewardBudgetCost = 60;
            keyRewardBudgetCost = 30;
            layoutThemeRules = new[]
            {
                new CastleLayoutThemeRule(
                    CastleLayoutTheme.CentralCompartmentFortress,
                    8,
                    36,
                    2,
                    CastleLayoutSymmetry.None),
                new CastleLayoutThemeRule(
                    CastleLayoutTheme.DiamondRadialFortress,
                    8,
                    36,
                    2,
                    CastleLayoutSymmetry.None),
                new CastleLayoutThemeRule(
                    CastleLayoutTheme.HoneycombCompartmentFortress,
                    12,
                    60,
                    2,
                    CastleLayoutSymmetry.None),
                new CastleLayoutThemeRule(
                    CastleLayoutTheme.HexHoneycombFortress,
                    8,
                    60,
                    2,
                    CastleLayoutSymmetry.None),
                new CastleLayoutThemeRule(
                    CastleLayoutTheme.PetalBloomFortress,
                    8,
                    24,
                    2,
                    CastleLayoutSymmetry.None),
                new CastleLayoutThemeRule(
                    CastleLayoutTheme.CrystalMandalaFortress,
                    8,
                    24,
                    2,
                    CastleLayoutSymmetry.FourWay),
                new CastleLayoutThemeRule(
                    CastleLayoutTheme.TwinSpiralFortress,
                    8,
                    24,
                    2,
                    CastleLayoutSymmetry.HalfTurn),
                new CastleLayoutThemeRule(
                    CastleLayoutTheme.FractalBastionFortress,
                    8,
                    24,
                    2,
                    CastleLayoutSymmetry.FourWay),
                new CastleLayoutThemeRule(
                    CastleLayoutTheme.VoronoiCrystalFortress,
                    8,
                    24,
                    2,
                    CastleLayoutSymmetry.HalfTurn),
                new CastleLayoutThemeRule(
                    CastleLayoutTheme.IrisShutterFortress,
                    8,
                    24,
                    2,
                    CastleLayoutSymmetry.FourWay)
            };
            templates = generationTemplates ?? Array.Empty<CastleDistrictTemplate>();
        }

        public void EditorSetWallTierRange(int minimumTier, int maximumTier)
        {
            minimumWallTier = minimumTier;
            maximumWallTier = maximumTier;
        }

        public void EditorSetGridSize(int width, int height)
        {
            gridWidth = width;
            gridHeight = height;
        }
#endif
    }
}
