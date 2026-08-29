using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid.Generation
{
    public enum CastleLayoutTheme // 성벽 색이 아닌 성 구축 방식
    {
        CentralCompartmentFortress = 0,
        [Obsolete("중앙 격실 요새로 교체된 프로토타입 테마입니다.")]
        CompactCompartments = 1,
        [Obsolete("후속 정식 테마 설계 전까지 사용하지 않습니다.")]
        SymmetricRadial = 2,
        [Obsolete("폐기된 사각 외곽 링 프로토타입입니다.")]
        CitadelDoubleRing = 3,
        DiamondRadialFortress = 4,
        HoneycombCompartmentFortress = 5, // 직렬화 호환을 위해 이름은 보존한다
        HexHoneycombFortress = 6,
        PetalBloomFortress = 7,
        CrystalMandalaFortress = 8,
        TwinSpiralFortress = 9,
        FractalBastionFortress = 10,
        VoronoiCrystalFortress = 11,
        IrisShutterFortress = 12
    }

    public enum CastleLayoutSymmetry // 테마 검수용 회전 대칭 계약
    {
        None,
        HalfTurn,
        FourWay
    }

    public enum CastleStructureVariant // 같은 배치 테마 안에서 달라지는 성벽 골격
    {
        CentralAdaptive = 0,
        DiamondBalanced = 10,
        DiamondHorizontalWide = 11,
        DiamondVerticalTall = 12,
        DiamondDiagonalNorthEast = 13,
        DiamondDiagonalSouthEast = 14,
        DiamondDiagonalSouthWest = 15,
        DiamondDiagonalNorthWest = 16,
        DiamondStaggeredClockwise = 17,
        DiamondStaggeredCounterClockwise = 18,
        HoneycombBalanced = 20,
        HoneycombHorizontalWide = 21,
        HoneycombVerticalTall = 22,
        HoneycombDiagonalNorthEast = 23,
        HoneycombDiagonalSouthEast = 24,
        HoneycombDiagonalSouthWest = 25,
        HoneycombDiagonalNorthWest = 26,
        HoneycombStaggeredClockwise = 27,
        HoneycombStaggeredCounterClockwise = 28,
        HexHoneycombFlatPhaseA = 30,
        HexHoneycombFlatPhaseB = 31,
        HexHoneycombFlatPhaseC = 32,
        HexHoneycombFlatPhaseD = 33,
        HexHoneycombPointyPhaseA = 34,
        HexHoneycombPointyPhaseB = 35,
        HexHoneycombPointyPhaseC = 36,
        HexHoneycombPointyPhaseD = 37,
        PetalBloomBalanced = 40,
        PetalBloomRotated = 41,
        PetalBloomLongCardinal = 42,
        PetalBloomLongDiagonal = 43,
        PetalBloomClockwise = 44,
        PetalBloomCounterClockwise = 45,
        PetalBloomTightHeart = 46,
        PetalBloomWideCrown = 47,
        CrystalMandalaBalanced = 50,
        CrystalMandalaRotated = 51,
        CrystalMandalaLongCardinal = 52,
        CrystalMandalaLongDiagonal = 53,
        CrystalMandalaClockwise = 54,
        CrystalMandalaCounterClockwise = 55,
        CrystalMandalaTightCore = 56,
        CrystalMandalaWideCrown = 57,
        TwinSpiralBalanced = 60,
        TwinSpiralRotated = 61,
        TwinSpiralWide = 62,
        TwinSpiralTall = 63,
        TwinSpiralClockwise = 64,
        TwinSpiralCounterClockwise = 65,
        TwinSpiralTight = 66,
        TwinSpiralOpen = 67,
        FractalBastionBalanced = 70,
        FractalBastionRotated = 71,
        FractalBastionLongCardinal = 72,
        FractalBastionLongDiagonal = 73,
        FractalBastionClockwise = 74,
        FractalBastionCounterClockwise = 75,
        FractalBastionDense = 76,
        FractalBastionExpanded = 77,
        VoronoiCrystalBalanced = 80,
        VoronoiCrystalRotated = 81,
        VoronoiCrystalWide = 82,
        VoronoiCrystalTall = 83,
        VoronoiCrystalClockwise = 84,
        VoronoiCrystalCounterClockwise = 85,
        VoronoiCrystalDense = 86,
        VoronoiCrystalExpanded = 87,
        IrisShutterBalanced = 90,
        IrisShutterRotated = 91,
        IrisShutterLongCardinal = 92,
        IrisShutterLongDiagonal = 93,
        IrisShutterClockwise = 94,
        IrisShutterCounterClockwise = 95,
        IrisShutterTight = 96,
        IrisShutterWide = 97
    }

    public enum CastleDefenseLayerPreset // 왕궁까지 반드시 돌파할 성벽 겹 수
    {
        Double = 2,
        Triple = 3,
        Quadruple = 4
    }

    public enum CastleCompartmentRole // 왕궁에서 바깥으로 이어지는 격실 층
    {
        PalaceCore,
        InnerRing,
        OuterRing
    }

    [Flags]
    public enum CastleWallNeighborMask // 1×1 성벽 외형 선택용 직교 이웃
    {
        None = 0,
        North = 1 << 0,
        East = 1 << 1,
        South = 1 << 2,
        West = 1 << 3
    }

    public enum CastleWallBand // 생성 완료 후 바깥에서 안쪽으로 판정한 방어선 역할
    {
        None,
        OuterPerimeter,
        InnerDefense,
        CoreDefense,
        Partition
    }

    [Serializable]
    public sealed class CastleLayoutThemeRule // 테마별 G2 구조 상한
    {
        [SerializeField] private CastleLayoutTheme theme;
        [SerializeField, Min(1)] private int minimumCompartmentCount;
        [SerializeField, Min(1)] private int maximumCompartmentCount;
        [SerializeField, Min(1)] private int minimumProtectionDepth;
        [SerializeField] private CastleLayoutSymmetry symmetry;

        public CastleLayoutThemeRule(
            CastleLayoutTheme layoutTheme,
            int minimumCompartments,
            int maximumCompartments,
            int protectionDepth,
            CastleLayoutSymmetry requiredSymmetry)
        {
            theme = layoutTheme;
            minimumCompartmentCount = Mathf.Max(1, minimumCompartments);
            maximumCompartmentCount = Mathf.Max(minimumCompartmentCount, maximumCompartments);
            minimumProtectionDepth = Mathf.Max(1, protectionDepth);
            symmetry = requiredSymmetry;
        }

        public CastleLayoutTheme Theme => theme;
        public int MinimumCompartmentCount => minimumCompartmentCount;
        public int MaximumCompartmentCount => maximumCompartmentCount;
        public int MinimumProtectionDepth => minimumProtectionDepth;
        public CastleLayoutSymmetry Symmetry => symmetry;

        public CastleLayoutThemeRule Clone()
        {
            return new CastleLayoutThemeRule(
                theme,
                minimumCompartmentCount,
                maximumCompartmentCount,
                minimumProtectionDepth,
                symmetry);
        }
    }

    [Serializable]
    public sealed class CastleCompartmentData // 공유 성벽으로 연결되는 논리 격실
    {
        [SerializeField] private string compartmentId;
        [SerializeField] private string templateId;
        [SerializeField] private CastleCompartmentRole role;
        [SerializeField, Min(0)] private int defenseRing;
        [SerializeField] private RectInt bounds;
        [SerializeField, Range(1, 2)] private int wallLayers = 1;
        [SerializeField] private List<string> connectedCompartmentIds = new List<string>();
        [SerializeField] private List<Vector2Int> footprintCells = new List<Vector2Int>();

        public CastleCompartmentData(
            string id,
            string sourceTemplateId,
            CastleCompartmentRole compartmentRole,
            RectInt compartmentBounds,
            int layers,
            IEnumerable<string> connections)
            : this(
                id,
                sourceTemplateId,
                compartmentRole,
                compartmentRole == CastleCompartmentRole.PalaceCore ? 0 : 1,
                compartmentBounds,
                layers,
                connections,
                null)
        {
        }

        public CastleCompartmentData(
            string id,
            string sourceTemplateId,
            CastleCompartmentRole compartmentRole,
            int ring,
            RectInt compartmentBounds,
            int layers,
            IEnumerable<string> connections,
            IEnumerable<Vector2Int> customFootprintCells = null)
        {
            compartmentId = id ?? string.Empty;
            templateId = sourceTemplateId ?? string.Empty;
            role = compartmentRole;
            defenseRing = Mathf.Max(0, ring);
            bounds = compartmentBounds;
            wallLayers = Mathf.Clamp(layers, 1, 2);
            if (connections != null)
            {
                connectedCompartmentIds.AddRange(connections.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct());
            }

            if (customFootprintCells != null)
            {
                footprintCells.AddRange(customFootprintCells
                    .Distinct()
                    .OrderBy(value => value.y)
                    .ThenBy(value => value.x));
            }
        }

        public string CompartmentId => compartmentId;
        public string TemplateId => templateId;
        public CastleCompartmentRole Role => role;
        public int DefenseRing => defenseRing;
        public RectInt Bounds => bounds;
        public int WallLayers => wallLayers;
        public IReadOnlyList<string> ConnectedCompartmentIds => connectedCompartmentIds;
        public IReadOnlyList<Vector2Int> FootprintCells => footprintCells;
        public bool HasCustomFootprint => footprintCells.Count > 0;

        public bool ContainsFootprintCell(Vector2Int cell)
        {
            return HasCustomFootprint ? footprintCells.Contains(cell) : bounds.Contains(cell);
        }

        public bool IsFootprintBoundaryCell(Vector2Int cell)
        {
            if (!ContainsFootprintCell(cell))
            {
                return false;
            }

            return !ContainsFootprintCell(cell + Vector2Int.up) ||
                   !ContainsFootprintCell(cell + Vector2Int.right) ||
                   !ContainsFootprintCell(cell + Vector2Int.down) ||
                   !ContainsFootprintCell(cell + Vector2Int.left);
        }

        public IEnumerable<Vector2Int> EnumerateFootprintCells()
        {
            if (HasCustomFootprint)
            {
                return footprintCells;
            }

            var cells = new List<Vector2Int>(bounds.width * bounds.height);
            for (var x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (var z = bounds.yMin; z < bounds.yMax; z++)
                {
                    cells.Add(new Vector2Int(x, z));
                }
            }

            return cells;
        }

        public CastleCompartmentData Clone()
        {
            return new CastleCompartmentData(
                compartmentId,
                templateId,
                role,
                defenseRing,
                bounds,
                wallLayers,
                connectedCompartmentIds,
                footprintCells);
        }
    }

    public enum CastlePlacementKind // 생성 성의 논리 배치 종류
    {
        Wall,
        Building,
        DefenseBuilding,
        Defender,
        Palace,
        LootBuilding
    }

    public enum CastleLootKind // 선택 약탈 건물 종류
    {
        None,
        Gold,
        Equipment,
        Key
    }

    [Serializable]
    public sealed class CastlePlacementData // 승인 전후에 공유하는 순수 배치 데이터
    {
        [SerializeField] private string placementId;
        [SerializeField] private string districtId;
        [SerializeField] private string templateId;
        [SerializeField] private CastlePlacementKind kind;
        [SerializeField] private CastleLootKind lootKind;
        [SerializeField] private int x;
        [SerializeField] private int z;
        [SerializeField, Min(1)] private int width = 1;
        [SerializeField, Min(1)] private int height = 1;
        [SerializeField, Min(0)] private int wallTier;
        [SerializeField, Min(0f)] private float effectiveHealth;
        [SerializeField, Min(0)] private int rewardBudgetCost;
        [SerializeField] private CastleWallNeighborMask wallNeighborMask;
        [SerializeField] private CastleWallBand wallBand;
        [SerializeField, Min(0)] private int wallDefenseLayer;
        [SerializeField] private string wallLineId;
        [SerializeField] private List<string> ownerDistrictIds = new List<string>();

        public CastlePlacementData(
            string id,
            string ownerDistrictId,
            string ownerTemplateId,
            CastlePlacementKind placementKind,
            CastleLootKind placementLootKind,
            int cellX,
            int cellZ,
            int cellWidth,
            int cellHeight,
            int placementWallTier,
            float health,
            int budgetCost,
            CastleWallNeighborMask neighborMask = CastleWallNeighborMask.None,
            IEnumerable<string> owners = null,
            CastleWallBand placementWallBand = CastleWallBand.None,
            int defenseLayer = 0,
            string lineId = "")
        {
            placementId = id ?? string.Empty;
            districtId = ownerDistrictId ?? string.Empty;
            templateId = ownerTemplateId ?? string.Empty;
            kind = placementKind;
            lootKind = placementLootKind;
            x = cellX;
            z = cellZ;
            width = Mathf.Max(1, cellWidth);
            height = Mathf.Max(1, cellHeight);
            wallTier = Mathf.Max(0, placementWallTier);
            effectiveHealth = Mathf.Max(0f, health);
            rewardBudgetCost = Mathf.Max(0, budgetCost);
            wallNeighborMask = neighborMask;
            wallBand = placementWallBand;
            wallDefenseLayer = Mathf.Max(0, defenseLayer);
            wallLineId = lineId ?? string.Empty;
            if (owners != null)
            {
                ownerDistrictIds.AddRange(owners.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct());
            }

            if (ownerDistrictIds.Count == 0 && !string.IsNullOrWhiteSpace(districtId))
            {
                ownerDistrictIds.Add(districtId);
            }
        }

        public string PlacementId => placementId;
        public string DistrictId => districtId;
        public string TemplateId => templateId;
        public CastlePlacementKind Kind => kind;
        public CastleLootKind LootKind => lootKind;
        public int X => x;
        public int Z => z;
        public int Width => width;
        public int Height => height;
        public int WallTier => wallTier;
        public float EffectiveHealth => effectiveHealth;
        public int RewardBudgetCost => rewardBudgetCost;
        public string StructureId => placementId;
        public CastleWallNeighborMask WallNeighborMask => wallNeighborMask;
        public CastleWallBand WallBand => wallBand;
        public int WallDefenseLayer => wallDefenseLayer;
        public string WallLineId => wallLineId;
        public IReadOnlyList<string> OwnerDistrictIds => ownerDistrictIds;
        public RectInt Bounds => new RectInt(x, z, width, height);

        public bool Occupies(int cellX, int cellZ)
        {
            return cellX >= x && cellX < x + width && cellZ >= z && cellZ < z + height;
        }

        public CastlePlacementData Clone()
        {
            return new CastlePlacementData(
                placementId,
                districtId,
                templateId,
                kind,
                lootKind,
                x,
                z,
                width,
                height,
                wallTier,
                effectiveHealth,
                rewardBudgetCost,
                wallNeighborMask,
                ownerDistrictIds,
                wallBand,
                wallDefenseLayer,
                wallLineId);
        }
    }

    [Serializable]
    public sealed class CastleGenerationValidationIssue // 후보 폐기 이유
    {
        [SerializeField] private string code;
        [SerializeField] private string message;
        [SerializeField] private string placementId;
        [SerializeField] private Vector2Int cell;

        public CastleGenerationValidationIssue(string issueCode, string issueMessage, string targetId, Vector2Int issueCell)
        {
            code = issueCode ?? string.Empty;
            message = issueMessage ?? string.Empty;
            placementId = targetId ?? string.Empty;
            cell = issueCell;
        }

        public string Code => code;
        public string Message => message;
        public string PlacementId => placementId;
        public Vector2Int Cell => cell;
    }

    [Serializable]
    public sealed class CastleGenerationValidationReport // 생성 후보 자동 검수 결과
    {
        [SerializeField] private List<CastleGenerationValidationIssue> issues = new List<CastleGenerationValidationIssue>();

        public CastleGenerationValidationReport(IEnumerable<CastleGenerationValidationIssue> validationIssues)
        {
            if (validationIssues != null)
            {
                issues.AddRange(validationIssues);
            }
        }

        public IReadOnlyList<CastleGenerationValidationIssue> Issues => issues;
        public bool IsValid => issues.Count == 0;
    }

    [Serializable]
    public sealed class CastleDifficultyReport // 정적 공략 난이도 보고서
    {
        [SerializeField] private bool hasClearPath;
        [SerializeField, Min(0f)] private float minimumClearDamage;
        [SerializeField, Min(0f)] private float mandatoryObstacleDamage;
        [SerializeField, Min(0f)] private float palaceDamage;
        [SerializeField, Min(0f)] private float defensePressure;
        [SerializeField, Min(0f)] private float totalDestructionDamage;
        [SerializeField] private float goldLootDamage = -1f;
        [SerializeField] private float equipmentLootDamage = -1f;
        [SerializeField] private float keyLootDamage = -1f;
        [SerializeField] private List<string> mandatoryPlacementIds = new List<string>();

        public CastleDifficultyReport(
            bool clearPathExists,
            float clearDamage,
            float obstacleDamage,
            float finalTargetDamage,
            float defenseScore,
            float totalDamage,
            float goldDamage,
            float equipmentDamage,
            float keyDamage,
            IEnumerable<string> mandatoryIds)
        {
            hasClearPath = clearPathExists;
            minimumClearDamage = Mathf.Max(0f, clearDamage);
            mandatoryObstacleDamage = Mathf.Max(0f, obstacleDamage);
            palaceDamage = Mathf.Max(0f, finalTargetDamage);
            defensePressure = Mathf.Max(0f, defenseScore);
            totalDestructionDamage = Mathf.Max(0f, totalDamage);
            goldLootDamage = goldDamage;
            equipmentLootDamage = equipmentDamage;
            keyLootDamage = keyDamage;
            if (mandatoryIds != null)
            {
                mandatoryPlacementIds.AddRange(mandatoryIds);
            }
        }

        public bool HasClearPath => hasClearPath;
        public float MinimumClearDamage => minimumClearDamage;
        public float MandatoryObstacleDamage => mandatoryObstacleDamage;
        public float PalaceDamage => palaceDamage;
        public float DefensePressure => defensePressure;
        public float TotalDestructionDamage => totalDestructionDamage;
        public float GoldLootDamage => goldLootDamage;
        public float EquipmentLootDamage => equipmentLootDamage;
        public float KeyLootDamage => keyLootDamage;
        public IReadOnlyList<string> MandatoryPlacementIds => mandatoryPlacementIds;
    }
}
