using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [Serializable]
    public sealed class HexCastleCellRecord
    {
        [SerializeField] private int q;
        [SerializeField] private int r;
        [SerializeField] private HexCastleCellKind kind;
        [SerializeField] private int defenseLayer;
        [SerializeField] private float hitPoints;
        [SerializeField] private HexCastleWallRole wallRole;
        [SerializeField] private int districtId;
        [SerializeField] private int rewardValue;
        [SerializeField] private int wallConnectionMask = -1;
        [SerializeField] private bool hasExtendedState;
        [SerializeField] private int regionId;
        [SerializeField] private bool initialBlocked;
        [SerializeField] private bool noDeploy;
        [SerializeField] private int wallTier;
        [SerializeField] private HexCastleLootKind lootKind;
        [SerializeField] private string pathId;
        [SerializeField] private int pathIndex = -1;
        [SerializeField] private string placementId;
        [SerializeField] private string visualVariantId;
        [SerializeField] private HexCastleBuildingRole buildingRole;
        [SerializeField] private HexCastlePlacementDensity placementDensity;
        [SerializeField] private int buildingGrade;
        [SerializeField] private HexCastleTurretWeaponKind turretWeaponKind;
        [SerializeField] private bool hasTurretCombatState;
        [SerializeField] private int turretRangeCells;
        [SerializeField] private bool turretCanAttackAcrossWalls;
        [SerializeField] private bool hasGateState;
        [SerializeField] private HexCastleGateRole gateRole;
        [SerializeField] private int gatePassageMask;

        public HexCastleCellRecord(HexCastleCell cell)
        {
            q = cell.Coordinates.Q;
            r = cell.Coordinates.R;
            kind = cell.Kind;
            defenseLayer = cell.DefenseLayer;
            hitPoints = cell.HitPoints;
            wallRole = cell.WallRole;
            districtId = cell.DistrictId;
            rewardValue = cell.RewardValue;
            wallConnectionMask = cell.WallConnectionMask;
            hasExtendedState = true;
            regionId = cell.RegionId;
            initialBlocked = cell.InitialBlocked;
            noDeploy = cell.NoDeploy;
            wallTier = cell.WallTier;
            lootKind = cell.LootKind;
            pathId = cell.PathId;
            pathIndex = cell.PathIndex;
            placementId = cell.PlacementId;
            visualVariantId = cell.VisualVariantId;
            buildingRole = cell.BuildingRole;
            placementDensity = cell.PlacementDensity;
            buildingGrade = cell.BuildingGrade;
            turretWeaponKind = cell.TurretWeaponKind;
            hasTurretCombatState = cell.HasExplicitTurretCombatState;
            turretRangeCells = cell.TurretRangeCells;
            turretCanAttackAcrossWalls = cell.TurretCanAttackAcrossWalls;
            hasGateState = cell.HasExplicitGateState;
            gateRole = cell.GateRole;
            gatePassageMask = cell.GatePassageMask;
        }

        public HexCastleCell Build()
        {
            if (!hasExtendedState)
            {
                return new HexCastleCell(
                    new HexCoordinates(q, r),
                    kind,
                    defenseLayer,
                    hitPoints,
                    wallRole,
                    districtId,
                    rewardValue,
                    wallConnectionMask: wallConnectionMask);
            }

            return new HexCastleCell(
                new HexCoordinates(q, r),
                kind,
                defenseLayer,
                hitPoints,
                wallRole,
                districtId,
                rewardValue,
                regionId,
                initialBlocked,
                noDeploy,
                wallTier,
                lootKind,
                pathId,
                pathIndex,
                placementId,
                visualVariantId,
                wallConnectionMask,
                buildingRole,
                placementDensity,
                buildingGrade,
                turretWeaponKind,
                hasTurretCombatState ? turretRangeCells : (int?)null,
                hasTurretCombatState ? turretCanAttackAcrossWalls : (bool?)null,
                hasGateState ? gateRole : (HexCastleGateRole?)null,
                hasGateState ? gatePassageMask : (int?)null);
        }
    }

    [CreateAssetMenu(
        fileName = "HexCastleStageLayout",
        menuName = "ProjectMT/Castle Raid Hex/Stage Layout")]
    public sealed class HexCastleStageLayout : ScriptableObject
    {
        [SerializeField] private string stageId;
        [SerializeField] private int rulesVersion =
            HexCastleFoundationGenerator.FoundationRulesVersionBase + HexCastleThemeOneTuning.CurrentDraftVersion;
        [SerializeField] private int seed;
        [SerializeField] private HexCastleTheme theme;
        [SerializeField, Range(0, 10)] private int difficultyLevel;
        [SerializeField, Range(2, 4)] private int defenseLayerCount = 3;
        [SerializeField] private int battlefieldRadius = 10;
        [SerializeField] private int buildRadius = 8;
        [SerializeField] private int palaceRadius = HexCastleFoundationGenerator.PalaceFootprintRadius;
        [SerializeField] private string structureSignature;
        [SerializeField] private string layoutSignature;
        [SerializeField] private float difficultyScore;
        [SerializeField] private int suggestedStage;
        [SerializeField] private int[] wallRadii = Array.Empty<int>();
        [SerializeField] private List<HexCastleCellRecord> cells = new List<HexCastleCellRecord>();

        public string StageId => string.IsNullOrWhiteSpace(stageId)
            ? $"HEX_{HexCastleThemeCatalog.ResolveCode(theme)}_{defenseLayerCount}W"
            : stageId;
        public int RulesVersion => rulesVersion;
        public int Seed => seed;
        public HexCastleTheme Theme => theme;
        public int DifficultyLevel => difficultyLevel;
        public int DefenseLayerCount => defenseLayerCount;
        public string StructureSignature => structureSignature;
        public string LayoutSignature => layoutSignature;
        public float DifficultyScore => difficultyScore;
        public int SuggestedStage => suggestedStage;
        public int CellCount => cells.Count;

        public void Configure(HexCastleCandidate candidate, string id = null)
        {
            if (candidate == null || candidate.Layout == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (!candidate.Validation.IsValid)
            {
                throw new InvalidOperationException("검수에 실패한 육각 후보는 승인 Layout으로 저장할 수 없습니다.");
            }

            var layout = candidate.Layout;
            stageId = string.IsNullOrWhiteSpace(id)
                ? $"HEX_{HexCastleThemeCatalog.ResolveCode(layout.Theme)}_{layout.DefenseLayerCount}W"
                : id.Trim();
            rulesVersion = layout.RulesVersion;
            seed = layout.Seed;
            theme = layout.Theme;
            difficultyLevel = layout.DifficultyLevel;
            defenseLayerCount = layout.DefenseLayerCount;
            battlefieldRadius = layout.BattlefieldRadius;
            buildRadius = layout.BuildRadius;
            palaceRadius = layout.PalaceRadius;
            structureSignature = layout.StructureSignature;
            layoutSignature = layout.LayoutSignature;
            difficultyScore = candidate.Difficulty.Score;
            suggestedStage = candidate.Difficulty.SuggestedStage;
            wallRadii = layout.WallRadii.ToArray();
            cells = layout.Cells.Values
                .OrderBy(cell => cell.Coordinates)
                .Select(cell => new HexCastleCellRecord(cell))
                .ToList();
        }

        public HexCastleLayout BuildLayout()
        {
            var request = new HexCastleGenerationRequest(
                seed,
                theme,
                defenseLayerCount,
                battlefieldRadius,
                buildRadius,
                palaceRadius,
                difficultyLevel);
            var map = cells.Select(record => record.Build()).ToDictionary(cell => cell.Coordinates);
            var layout = new HexCastleLayout(request, map, wallRadii, rulesVersion);
            if (!string.Equals(layout.LayoutSignature, layoutSignature, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"승인 Layout Hash가 다릅니다. 저장 {layoutSignature}, 복원 {layout.LayoutSignature}");
            }

            return layout;
        }
    }
}
