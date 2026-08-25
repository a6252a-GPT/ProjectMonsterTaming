using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    public enum HexCastleCellKind
    {
        Ground = 0,
        Deployment = 1,
        Wall = 2,
        Building = 3,
        DefenseBuilding = 4,
        Defense = DefenseBuilding,
        Palace = 5,
        Tower = 6,
        Gate = 7,
        RewardBuilding = 8,
        Reserved = 9
    }

    public enum HexCastleGateRole
    {
        None = 0,
        ClosedWall = 1,
        OpenDefenderPassage = 2
    }

    public enum HexCastleTraversalFaction
    {
        Assault = 0,
        Defender = 1
    }

    public sealed class HexCastleCell
    {
        public HexCastleCell(
            HexCoordinates coordinates,
            HexCastleCellKind kind,
            int defenseLayer = 0,
            float hitPoints = 0f,
            HexCastleWallRole wallRole = HexCastleWallRole.None,
            int districtId = 0,
            int rewardValue = 0,
            int regionId = 0,
            bool? initialBlocked = null,
            bool noDeploy = false,
            int wallTier = 0,
            HexCastleLootKind lootKind = HexCastleLootKind.None,
            string pathId = null,
            int pathIndex = -1,
            string placementId = null,
            string visualVariantId = null,
            int wallConnectionMask = -1,
            HexCastleBuildingRole buildingRole = HexCastleBuildingRole.None,
            HexCastlePlacementDensity placementDensity = HexCastlePlacementDensity.None,
            int buildingGrade = 0,
            HexCastleTurretWeaponKind turretWeaponKind = HexCastleTurretWeaponKind.None,
            int? turretRangeCells = null,
            bool? turretCanAttackAcrossWalls = null,
            HexCastleGateRole? gateRole = null,
            int? gatePassageMask = null)
        {
            Coordinates = coordinates;
            Kind = kind;
            DefenseLayer = Mathf.Max(0, defenseLayer);
            MaxHealth = Mathf.Max(0f, hitPoints);
            WallRole = wallRole;
            DistrictId = districtId;
            RewardValue = Mathf.Max(0, rewardValue);
            RegionId = regionId;
            // null은 기존 Hex 생성기 이식 기간의 호환 경로다. 정식 생성기는 반드시 값을 명시한다.
            InitialBlocked = initialBlocked ?? ResolveLegacyInitialBlocked(kind, MaxHealth);
            NoDeploy = noDeploy || ResolveDefaultNoDeploy(kind);
            WallTier = Mathf.Max(0, wallTier);
            LootKind = lootKind;
            PathId = pathId ?? string.Empty;
            PathIndex = pathIndex;
            PlacementId = placementId ?? string.Empty;
            VisualVariantId = visualVariantId ?? string.Empty;
            WallConnectionMask = wallConnectionMask < 0 ? -1 : wallConnectionMask & 0x3F;
            BuildingRole = buildingRole;
            PlacementDensity = placementDensity;
            BuildingGrade = Mathf.Max(0, buildingGrade);
            TurretWeaponKind = turretWeaponKind;
            if (turretRangeCells.HasValue != turretCanAttackAcrossWalls.HasValue)
            {
                throw new ArgumentException("포탑 사거리와 벽 관통 설정은 함께 지정해야 합니다.");
            }

            HasExplicitTurretCombatState =
                buildingRole == HexCastleBuildingRole.Turret && turretRangeCells.HasValue;
            TurretRangeCells = Mathf.Max(
                0,
                turretRangeCells ??
                (buildingRole == HexCastleBuildingRole.Turret ? 2 : 0));
            TurretCanAttackAcrossWalls = turretCanAttackAcrossWalls ??
                                         buildingRole == HexCastleBuildingRole.Turret;
            HasExplicitGateState = kind == HexCastleCellKind.Gate && gateRole.HasValue;
            GateRole = gateRole ??
                       (kind == HexCastleCellKind.Gate
                           ? HexCastleGateRole.ClosedWall
                           : HexCastleGateRole.None);
            GatePassageMask = gatePassageMask ?? 0;
            ValidateState();
        }

        public HexCoordinates Coordinates { get; }
        public HexCastleCellKind Kind { get; }
        public int DefenseLayer { get; }
        public float MaxHealth { get; }
        public float HitPoints => MaxHealth;
        public HexCastleWallRole WallRole { get; }
        public int DistrictId { get; }
        public int RewardValue { get; }
        public int RegionId { get; }
        public bool InitialBlocked { get; }
        public bool NoDeploy { get; }
        public int WallTier { get; }
        public HexCastleLootKind LootKind { get; }
        public string PathId { get; }
        public int PathIndex { get; }
        public string PlacementId { get; }
        public string VisualVariantId { get; }
        public int WallConnectionMask { get; }
        public HexCastleBuildingRole BuildingRole { get; }
        public HexCastlePlacementDensity PlacementDensity { get; }
        public int BuildingGrade { get; }
        public HexCastleTurretWeaponKind TurretWeaponKind { get; }
        public int TurretRangeCells { get; }
        public bool TurretCanAttackAcrossWalls { get; }
        public bool HasExplicitTurretCombatState { get; }
        public HexCastleGateRole GateRole { get; }
        public int GatePassageMask { get; }
        public bool HasExplicitGateState { get; }
        public bool AllowsDefenderTraversal => GateRole == HexCastleGateRole.OpenDefenderPassage;
        public bool HasExplicitWallConnections => WallConnectionMask >= 0;
        public bool IsBreakable => InitialBlocked && MaxHealth > 0f;
        public bool IsOpen => !InitialBlocked;
        public bool IsBuildingCell => Kind == HexCastleCellKind.Building ||
                                      Kind == HexCastleCellKind.DefenseBuilding ||
                                      Kind == HexCastleCellKind.RewardBuilding;
        public bool IsWallPathCell => Kind == HexCastleCellKind.Wall ||
                                      Kind == HexCastleCellKind.Tower ||
                                      Kind == HexCastleCellKind.Gate;

        public bool CanTraverseWithoutBreaking(HexCastleTraversalFaction faction)
        {
            return !InitialBlocked ||
                   faction == HexCastleTraversalFaction.Defender && AllowsDefenderTraversal;
        }

        public bool CanEnterFrom(int direction, HexCastleTraversalFaction faction)
        {
            if (!InitialBlocked)
            {
                return true;
            }

            return faction == HexCastleTraversalFaction.Defender &&
                   AllowsDefenderTraversal &&
                   direction >= 0 && direction < HexCoordinates.Directions.Length &&
                   (GatePassageMask & 1 << direction) != 0;
        }

        public bool CanTraverseBetween(
            int entryDirection,
            int exitDirection,
            HexCastleTraversalFaction faction)
        {
            if (!InitialBlocked)
            {
                return true;
            }

            return entryDirection != exitDirection &&
                   CanEnterFrom(entryDirection, faction) &&
                   CanEnterFrom(exitDirection, faction);
        }

        public static bool IsValidGatePassageMask(int wallMask, int passageMask)
        {
            wallMask &= 0x3F;
            passageMask &= 0x3F;
            if (CountBits(wallMask) != 2 || CountBits(passageMask) != 2 ||
                (wallMask & passageMask) != 0)
            {
                return false;
            }

            return ResolveTwoWaySeparation(wallMask) == 3 &&
                   ResolveTwoWaySeparation(passageMask) == 2;
        }

        public static bool ResolveDefaultBlocked(HexCastleCellKind kind)
        {
            switch (kind)
            {
                case HexCastleCellKind.Wall:
                case HexCastleCellKind.Tower:
                case HexCastleCellKind.Gate:
                case HexCastleCellKind.Building:
                case HexCastleCellKind.DefenseBuilding:
                case HexCastleCellKind.RewardBuilding:
                case HexCastleCellKind.Palace:
                    return true;
                default:
                    return false;
            }
        }

        public static bool ResolveDefaultNoDeploy(HexCastleCellKind kind)
        {
            return kind != HexCastleCellKind.Deployment;
        }

        private static bool ResolveLegacyInitialBlocked(HexCastleCellKind kind, float maxHealth)
        {
            return ResolveDefaultBlocked(kind) && maxHealth > 0f;
        }

        private void ValidateState()
        {
            if (InitialBlocked && MaxHealth <= 0f)
            {
                throw new ArgumentException($"차단 Cell {Coordinates}은 MaxHealth가 필요합니다.");
            }

            if (!InitialBlocked && MaxHealth > 0f)
            {
                throw new ArgumentException($"열린 Cell {Coordinates}은 체력을 가질 수 없습니다.");
            }

            if ((Kind == HexCastleCellKind.Ground || Kind == HexCastleCellKind.Deployment ||
                 Kind == HexCastleCellKind.Reserved) && InitialBlocked)
            {
                throw new ArgumentException($"{Kind} Cell {Coordinates}은 길을 막을 수 없습니다.");
            }

            if (Kind == HexCastleCellKind.RewardBuilding &&
                (LootKind == HexCastleLootKind.None || RewardValue <= 0))
            {
                throw new ArgumentException($"RewardBuilding Cell {Coordinates}의 LootKind 또는 RewardValue가 없습니다.");
            }

            if (Kind != HexCastleCellKind.RewardBuilding && LootKind != HexCastleLootKind.None)
            {
                throw new ArgumentException($"{Kind} Cell {Coordinates}은 LootKind를 가질 수 없습니다.");
            }

            if (!IsWallPathCell && HasExplicitWallConnections)
            {
                throw new ArgumentException($"{Kind} Cell {Coordinates}은 성벽 연결 마스크를 가질 수 없습니다.");
            }

            if (Kind != HexCastleCellKind.Gate &&
                (GateRole != HexCastleGateRole.None || GatePassageMask != 0))
            {
                throw new ArgumentException($"{Kind} Cell {Coordinates}은 성문 상태를 가질 수 없습니다.");
            }

            if (Kind == HexCastleCellKind.Gate && HasExplicitGateState)
            {
                if (!InitialBlocked || MaxHealth <= 0f)
                {
                    throw new ArgumentException($"성문 Cell {Coordinates}은 Cell 체력과 길막을 가져야 합니다.");
                }

                if (GateRole == HexCastleGateRole.OpenDefenderPassage)
                {
                    if (WallRole != HexCastleWallRole.Partition ||
                        GatePassageMask <= 0 || GatePassageMask > 0x3F ||
                        !HasExplicitWallConnections ||
                        !IsValidGatePassageMask(WallConnectionMask, GatePassageMask))
                    {
                        throw new ArgumentException(
                            $"열린 격벽 성문 {Coordinates}의 역할 또는 통로 방향이 잘못됐습니다.");
                    }
                }
                else if (GateRole == HexCastleGateRole.ClosedWall)
                {
                    if (WallRole == HexCastleWallRole.None ||
                        WallRole == HexCastleWallRole.Partition ||
                        GatePassageMask != 0)
                    {
                        throw new ArgumentException(
                            $"닫힌 성벽 성문 {Coordinates}의 역할 또는 통로 방향이 잘못됐습니다.");
                    }
                }
                else
                {
                    throw new ArgumentException($"성문 Cell {Coordinates}의 성문 역할이 없습니다.");
                }
            }

            if (!IsBuildingCell &&
                (BuildingRole != HexCastleBuildingRole.None ||
                 PlacementDensity != HexCastlePlacementDensity.None ||
                 BuildingGrade > 0 ||
                 TurretWeaponKind != HexCastleTurretWeaponKind.None ||
                 TurretRangeCells > 0 ||
                 TurretCanAttackAcrossWalls))
            {
                throw new ArgumentException($"{Kind} Cell {Coordinates}은 건물 역할 데이터를 가질 수 없습니다.");
            }

            if (BuildingRole != HexCastleBuildingRole.None)
            {
                ValidateBuildingRole();
            }

            if (IsWallPathCell && HasExplicitWallConnections)
            {
                var connectionCount = 0;
                var value = WallConnectionMask;
                while (value != 0)
                {
                    connectionCount += value & 1;
                    value >>= 1;
                }

                if (connectionCount < 2 || connectionCount > 4)
                {
                    throw new ArgumentException(
                        $"성벽 Cell {Coordinates}의 연결 수는 2~4여야 합니다: {connectionCount}");
                }
            }
        }

        private static int CountBits(int value)
        {
            var count = 0;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }

            return count;
        }

        private static int ResolveTwoWaySeparation(int mask)
        {
            var first = -1;
            var second = -1;
            for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
            {
                if ((mask & 1 << direction) == 0)
                {
                    continue;
                }

                if (first < 0)
                {
                    first = direction;
                }
                else
                {
                    second = direction;
                    break;
                }
            }

            if (first < 0 || second < 0)
            {
                return 0;
            }

            var delta = Math.Abs(first - second);
            return Math.Min(delta, HexCoordinates.Directions.Length - delta);
        }

        private void ValidateBuildingRole()
        {
            if (PlacementDensity == HexCastlePlacementDensity.None || BuildingGrade <= 0)
            {
                throw new ArgumentException($"건물 Cell {Coordinates}의 배치 열 또는 등급이 없습니다.");
            }

            var isRewardRole = BuildingRole == HexCastleBuildingRole.GoldStorage ||
                               BuildingRole == HexCastleBuildingRole.EquipmentForge ||
                               BuildingRole == HexCastleBuildingRole.KeyVault;
            if (isRewardRole != (Kind == HexCastleCellKind.RewardBuilding))
            {
                throw new ArgumentException($"건물 Cell {Coordinates}의 역할과 CellKind가 맞지 않습니다.");
            }

            if (BuildingRole == HexCastleBuildingRole.Turret)
            {
                if (Kind != HexCastleCellKind.DefenseBuilding ||
                    TurretWeaponKind == HexCastleTurretWeaponKind.None ||
                    TurretRangeCells < 2 || TurretRangeCells > 4 ||
                    !TurretCanAttackAcrossWalls)
                {
                    throw new ArgumentException(
                        $"포탑 Cell {Coordinates}의 무기·사거리·벽 관통 또는 CellKind가 잘못됐습니다.");
                }
            }
            else if (!isRewardRole && Kind != HexCastleCellKind.Building)
            {
                throw new ArgumentException($"일반·특수 건물 Cell {Coordinates}의 CellKind가 잘못됐습니다.");
            }
            else if (TurretWeaponKind != HexCastleTurretWeaponKind.None ||
                     TurretRangeCells > 0 ||
                     TurretCanAttackAcrossWalls)
            {
                throw new ArgumentException($"비포탑 Cell {Coordinates}은 포탑 전투 데이터를 가질 수 없습니다.");
            }

            var expectedLoot = BuildingRole == HexCastleBuildingRole.GoldStorage
                ? HexCastleLootKind.Gold
                : BuildingRole == HexCastleBuildingRole.EquipmentForge
                    ? HexCastleLootKind.Equipment
                    : BuildingRole == HexCastleBuildingRole.KeyVault
                        ? HexCastleLootKind.Key
                        : HexCastleLootKind.None;
            if (LootKind != expectedLoot)
            {
                throw new ArgumentException($"건물 Cell {Coordinates}의 역할과 LootKind가 맞지 않습니다.");
            }
        }
    }

    public sealed class HexCastleLayout
    {
        private readonly Dictionary<HexCoordinates, HexCastleCell> cells;
        private readonly int[] wallRadii;
        private readonly HexCastleTrapPlacement[] trapPlacements;

        internal HexCastleLayout(
            HexCastleGenerationRequest request,
            Dictionary<HexCoordinates, HexCastleCell> cells,
            IEnumerable<int> wallRadii,
            int rulesVersion,
            IEnumerable<HexCastleTrapPlacement> traps = null)
        {
            Request = request;
            this.cells = cells ?? throw new ArgumentNullException(nameof(cells));
            this.wallRadii = wallRadii?.ToArray() ?? throw new ArgumentNullException(nameof(wallRadii));
            trapPlacements = traps?.ToArray() ?? Array.Empty<HexCastleTrapPlacement>();
            RulesVersion = rulesVersion;
            StructureSignature = CalculateSignature(false);
            LayoutSignature = CalculateSignature(true);
        }

        public HexCastleGenerationRequest Request { get; }
        public int RulesVersion { get; }
        public int Seed => Request.Seed;
        public HexCastleTheme Theme => Request.Theme;
        public int DefenseLayerCount => Request.DefenseLayerCount;
        public int BattlefieldRadius => Request.BattlefieldRadius;
        public int BuildRadius => Request.BuildRadius;
        public int PalaceRadius => Request.PalaceRadius;
        public int DifficultyLevel => Request.DifficultyLevel;
        public string StructureSignature { get; }
        public string LayoutSignature { get; }
        public string Signature => LayoutSignature;
        public IReadOnlyDictionary<HexCoordinates, HexCastleCell> Cells => cells;
        public IReadOnlyList<int> WallRadii => wallRadii;
        public IReadOnlyList<HexCastleTrapPlacement> TrapPlacements => trapPlacements;

        public bool TryGetCell(HexCoordinates coordinates, out HexCastleCell cell)
        {
            return cells.TryGetValue(coordinates, out cell);
        }

        public IEnumerable<HexCastleCell> Enumerate(HexCastleCellKind kind)
        {
            return cells.Values.Where(cell => cell.Kind == kind);
        }

        public IEnumerable<HexCastleCell> EnumerateBlocked()
        {
            return cells.Values.Where(cell => cell.InitialBlocked);
        }

        private string CalculateSignature(bool includeContents)
        {
            unchecked
            {
                const ulong offset = 1469598103934665603UL;
                const ulong prime = 1099511628211UL;
                var hash = offset;
                var source = includeContents
                    ? cells.Values
                    : cells.Values.Where(cell =>
                        cell.IsWallPathCell || cell.Kind == HexCastleCellKind.Palace);
                foreach (var cell in source.OrderBy(value => value.Coordinates))
                {
                    hash ^= (uint)cell.Coordinates.Q;
                    hash *= prime;
                    hash ^= (uint)cell.Coordinates.R;
                    hash *= prime;
                    hash ^= (uint)cell.Kind;
                    hash *= prime;
                    hash ^= (uint)cell.DefenseLayer;
                    hash *= prime;
                    hash ^= (uint)cell.WallRole;
                    hash *= prime;
                    hash ^= (uint)cell.DistrictId;
                    hash *= prime;
                    hash ^= (uint)(cell.WallConnectionMask + 1);
                    hash *= prime;
                    if (cell.HasExplicitGateState)
                    {
                        hash ^= 0x47415433u;
                        hash *= prime;
                        hash ^= (uint)cell.GateRole;
                        hash *= prime;
                        hash ^= (uint)cell.GatePassageMask;
                        hash *= prime;
                    }
                    if (includeContents)
                    {
                        hash ^= (uint)Mathf.RoundToInt(cell.HitPoints * 10f);
                        hash *= prime;
                        hash ^= (uint)cell.RewardValue;
                        hash *= prime;
                        if (cell.BuildingRole != HexCastleBuildingRole.None ||
                            cell.PlacementDensity != HexCastlePlacementDensity.None ||
                            cell.BuildingGrade > 0 ||
                            cell.TurretWeaponKind != HexCastleTurretWeaponKind.None ||
                            cell.TurretRangeCells > 0 ||
                            cell.TurretCanAttackAcrossWalls)
                        {
                            hash ^= 0x48425831u;
                            hash *= prime;
                            hash ^= (uint)cell.BuildingRole;
                            hash *= prime;
                            hash ^= (uint)cell.PlacementDensity;
                            hash *= prime;
                            hash ^= (uint)cell.BuildingGrade;
                            hash *= prime;
                            hash ^= (uint)cell.TurretWeaponKind;
                            hash *= prime;
                            if (cell.HasExplicitTurretCombatState)
                            {
                                hash ^= 0x54524332u;
                                hash *= prime;
                                hash ^= (uint)cell.TurretRangeCells;
                                hash *= prime;
                                hash ^= cell.TurretCanAttackAcrossWalls ? 1u : 0u;
                                hash *= prime;
                            }
                            hash ^= (uint)cell.LootKind;
                            hash *= prime;
                            hash = HashString(hash, prime, cell.PlacementId);
                            hash = HashString(hash, prime, cell.VisualVariantId);
                        }
                    }
                }

                if (includeContents)
                {
                    foreach (var trap in trapPlacements
                                 .OrderBy(value => value.Coordinates)
                                 .ThenBy(value => value.TrapType))
                    {
                        hash ^= 0x54524150u;
                        hash *= prime;
                        hash ^= (uint)trap.Coordinates.Q;
                        hash *= prime;
                        hash ^= (uint)trap.Coordinates.R;
                        hash *= prime;
                        hash ^= (uint)trap.TrapType;
                        hash *= prime;
                        hash ^= (uint)trap.DefenseBand;
                        hash *= prime;
                        hash ^= (uint)trap.RegionId;
                        hash *= prime;
                        hash = HashString(hash, prime, trap.PlacementId);
                    }
                }

                return hash.ToString("X16");
            }
        }

        private static ulong HashString(ulong hash, ulong prime, string value)
        {
            foreach (var character in value ?? string.Empty)
            {
                hash ^= character;
                hash *= prime;
            }

            hash ^= 0xFFu;
            hash *= prime;
            return hash;
        }
    }
}
