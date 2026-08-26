using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    public sealed class HexCastleValidationReport
    {
        internal HexCastleValidationReport(IEnumerable<string> errors, IEnumerable<HexRouteResult> entryRoutes)
        {
            Errors = errors.ToArray();
            EntryRoutes = entryRoutes.ToArray();
        }

        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<HexRouteResult> EntryRoutes { get; }
        public bool IsValid => Errors.Count == 0;
    }

    public sealed class HexCastleDifficultyReport
    {
        internal HexCastleDifficultyReport(
            float minimumBreachCost,
            float averageBreachCost,
            float maximumBreachCost,
            float totalDestructionHealth,
            int rewardValue,
            int totalBuildingGrade,
            float minimumBreachBuildingGrade,
            float averageBreachBuildingGrade,
            float score,
            int suggestedStage)
        {
            MinimumBreachCost = minimumBreachCost;
            AverageBreachCost = averageBreachCost;
            MaximumBreachCost = maximumBreachCost;
            TotalDestructionHealth = totalDestructionHealth;
            RewardValue = rewardValue;
            TotalBuildingGrade = totalBuildingGrade;
            MinimumBreachBuildingGrade = minimumBreachBuildingGrade;
            AverageBreachBuildingGrade = averageBreachBuildingGrade;
            Score = score;
            SuggestedStage = suggestedStage;
        }

        public float MinimumBreachCost { get; }
        public float AverageBreachCost { get; }
        public float MaximumBreachCost { get; }
        public float TotalDestructionHealth { get; }
        public int RewardValue { get; }
        public int TotalBuildingGrade { get; }
        public float MinimumBreachBuildingGrade { get; }
        public float AverageBreachBuildingGrade { get; }
        public float Score { get; }
        public int SuggestedStage { get; }
    }

    public sealed class HexCastleCandidate
    {
        internal HexCastleCandidate(
            HexCastleLayout layout,
            HexCastleValidationReport validation,
            HexCastleDifficultyReport difficulty)
        {
            Layout = layout;
            Validation = validation;
            Difficulty = difficulty;
        }

        public HexCastleLayout Layout { get; }
        public HexCastleValidationReport Validation { get; }
        public HexCastleDifficultyReport Difficulty { get; }
    }

    public sealed class HexCastleGenerationPipeline
    {
        public HexCastleCandidate GenerateFoundation(
            int seed,
            int defenseLayerCount,
            HexCastleTheme theme = HexCastleTheme.CentralCompartment,
            HexCastleThemeOneTuning tuning = null)
        {
            var layout = new HexCastleFoundationGenerator().Generate(
                seed,
                defenseLayerCount,
                theme,
                tuning);
            var validation = new HexCastleValidator().Validate(layout);
            var difficulty = new HexCastleDifficultyEvaluator().Evaluate(layout, validation.EntryRoutes);
            return new HexCastleCandidate(layout, validation, difficulty);
        }

        public HexCastleCandidate GenerateFoundationForDifficulty(
            int seed,
            int difficultyLevel,
            HexCastleTheme theme = HexCastleTheme.CentralCompartment,
            HexCastleThemeOneTuning tuning = null)
        {
            var layout = new HexCastleFoundationGenerator().GenerateForDifficulty(
                seed,
                difficultyLevel,
                theme,
                tuning);
            var validation = new HexCastleValidator().Validate(layout);
            var difficulty = new HexCastleDifficultyEvaluator().Evaluate(layout, validation.EntryRoutes);
            return new HexCastleCandidate(layout, validation, difficulty);
        }

    }

    public sealed class HexCastleValidator
    {
        public HexCastleValidationReport Validate(HexCastleLayout layout)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            var errors = new List<string>();
            var routes = new List<HexRouteResult>();
            if (layout.Cells.Count != 1 + 3 * layout.BattlefieldRadius * (layout.BattlefieldRadius + 1))
            {
                errors.Add("전장 육각 셀 수가 반경 계약과 다릅니다.");
            }

            if (!layout.TryGetCell(new HexCoordinates(0, 0), out var palace) ||
                palace.Kind != HexCastleCellKind.Palace || palace.HitPoints <= 0f)
            {
                errors.Add("중앙 왕궁 전투 목표가 없습니다.");
            }

            var palaceCells = layout.Enumerate(HexCastleCellKind.Palace).ToArray();
            var expectedPalaceCells = 1 + 3 * layout.PalaceRadius * (layout.PalaceRadius + 1);
            if (palaceCells.Length != expectedPalaceCells ||
                palaceCells.Any(cell => cell.Coordinates.DistanceFromOrigin > layout.PalaceRadius))
            {
                errors.Add("왕궁 점유 Cell이 PalaceRadius 계약과 다릅니다.");
            }

            for (var layer = 1; layer <= layout.DefenseLayerCount; layer++)
            {
                if (!layout.Cells.Values.Any(cell =>
                        cell.IsWallPathCell && cell.DefenseLayer == layer))
                {
                    errors.Add($"방어층 {layer} 성벽이 없습니다.");
                }
            }

            var wallTopology = HexCastleWallTopologyResolver.Build(layout);
            foreach (var pair in wallTopology)
            {
                var cell = layout.Cells[pair.Key];
                if (cell.Kind == HexCastleCellKind.Tower && pair.Value.ConnectionCount < 3)
                {
                    errors.Add($"2방향 성벽 {pair.Key}에 불필요한 연결 타워가 남았습니다.");
                }

                if (cell.Kind == HexCastleCellKind.Wall && pair.Value.ConnectionCount != 2)
                {
                    errors.Add($"일반 성벽 {pair.Key}의 연결 수가 2가 아닙니다.");
                }
            }

            var planner = new HexRoutePlanner();
            for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
            {
                var start = HexCoordinates.Directions[direction] * layout.BattlefieldRadius;
                var route = planner.FindMinimumBreachRoute(layout, start);
                routes.Add(route);
                if (!route.IsComplete)
                {
                    errors.Add($"진입 방향 {direction}에서 왕궁까지 경로가 없습니다.");
                    continue;
                }

                var expectedLayers = Enumerable.Range(1, layout.DefenseLayerCount).ToArray();
                if (!route.CrossedDefenseLayers.SequenceEqual(expectedLayers))
                {
                    errors.Add($"진입 방향 {direction}의 방어층 순서가 {string.Join(",", route.CrossedDefenseLayers)}입니다.");
                }
            }

            var deploymentCount = layout.Enumerate(HexCastleCellKind.Deployment).Count();
            if (deploymentCount < layout.BattlefieldRadius * 6)
            {
                errors.Add("외곽 배치 벨트가 부족합니다.");
            }

            var structureCells = layout.Cells.Values
                .Where(cell => cell.Kind != HexCastleCellKind.Ground && cell.Kind != HexCastleCellKind.Deployment)
                .Select(cell => cell.Coordinates)
                .ToArray();
            if (structureCells.Any(coordinates => coordinates.DistanceFromOrigin > layout.BuildRadius))
            {
                errors.Add("건설 반경 밖에 구조물이 있습니다.");
            }

            ValidateFoundationBuildingContract(layout, errors);
            ValidateTrapContract(layout, errors);

            return new HexCastleValidationReport(errors.Distinct(), routes);
        }

        private static void ValidateTrapContract(
            HexCastleLayout layout,
            ICollection<string> errors)
        {
            var profile = layout.DifficultyLevel > 0
                ? HexCastleDifficultyProfile.Resolve(layout.DifficultyLevel, layout.Seed)
                : null;
            var traps = layout.TrapPlacements.ToArray();
            var expectedTotal = profile?.TotalTrapCount ?? 0;
            if (traps.Length != expectedTotal)
            {
                errors.Add($"정식 육각 성 함정 수가 난이도 계약과 다릅니다: {traps.Length}/{expectedTotal}");
            }

            if (profile != null)
            {
                ValidateTypeCount(HexCastleTrapType.Snare, profile.SnareTrapCount);
                ValidateTypeCount(HexCastleTrapType.SpikePlate, profile.SpikePlateTrapCount);
                ValidateTypeCount(HexCastleTrapType.BlastMine, profile.BlastMineCount);
            }

            if (traps.Select(value => value.Coordinates).Distinct().Count() != traps.Length)
            {
                errors.Add("정식 육각 성 함정이 같은 Cell에 중복 배치됐습니다.");
            }

            if (traps.Any(value => string.IsNullOrWhiteSpace(value.PlacementId)) ||
                traps.Select(value => value.PlacementId).Distinct(StringComparer.Ordinal).Count() != traps.Length)
            {
                errors.Add("정식 육각 성 함정 배치 식별자가 비었거나 중복됐습니다.");
            }

            foreach (var trap in traps)
            {
                if (!Enum.IsDefined(typeof(HexCastleTrapType), trap.TrapType))
                {
                    errors.Add($"정식 육각 성 함정 {trap.PlacementId}의 종류가 잘못됐습니다.");
                }

                if (!layout.TryGetCell(trap.Coordinates, out var cell) ||
                    cell.Kind != HexCastleCellKind.Ground || !cell.IsOpen ||
                    trap.Coordinates.DistanceFromOrigin > layout.BuildRadius)
                {
                    errors.Add($"정식 육각 성 함정 {trap.PlacementId}이 열린 Ground Cell 밖에 있습니다.");
                }

                if (trap.Coordinates.DistanceFromOrigin <= layout.PalaceRadius)
                {
                    errors.Add($"정식 육각 성 함정 {trap.PlacementId}이 왕궁 점유 Cell과 겹칩니다.");
                }

                if (trap.DefenseBand < 1 || trap.DefenseBand > layout.DefenseLayerCount ||
                    trap.RegionId < 1 || trap.RegionId > HexCoordinates.Directions.Length)
                {
                    errors.Add($"정식 육각 성 함정 {trap.PlacementId}의 Band·구역 데이터가 잘못됐습니다.");
                }

            }

            for (var leftIndex = 0; leftIndex < traps.Length; leftIndex++)
            {
                for (var rightIndex = leftIndex + 1; rightIndex < traps.Length; rightIndex++)
                {
                    var left = traps[leftIndex];
                    var right = traps[rightIndex];
                    var distance = left.Coordinates.DistanceTo(right.Coordinates);
                    if (left.TrapType == HexCastleTrapType.BlastMine &&
                        right.TrapType == HexCastleTrapType.BlastMine && distance < 2)
                    {
                        errors.Add($"정식 육각 성 지뢰 {left.PlacementId}, {right.PlacementId}의 간격이 너무 좁습니다.");
                    }
                }
            }

            void ValidateTypeCount(HexCastleTrapType trapType, int expectedCount)
            {
                var actualCount = traps.Count(value => value.TrapType == trapType);
                if (actualCount != expectedCount)
                {
                    errors.Add($"정식 육각 성 {trapType} 수가 난이도 계약과 다릅니다: {actualCount}/{expectedCount}");
                }
            }
        }

        private static void ValidateFoundationBuildingContract(
            HexCastleLayout layout,
            ICollection<string> errors)
        {
            var difficultyProfile = layout.DifficultyLevel > 0
                ? HexCastleDifficultyProfile.Resolve(layout.DifficultyLevel, layout.Seed)
                : null;
            var requiredGateSocketCount = ResolveRequiredGateSocketCount(layout, difficultyProfile);
            var plan = HexCastleSilhouettePlanner.Build(
                layout.Theme,
                layout.Seed,
                layout.WallRadii,
                requiredGateSocketCount);
            var bandTopologies = Enumerable.Range(0, layout.WallRadii.Count - 1)
                .Select(index => HexCastleSilhouetteBandResolver.Resolve(
                    plan,
                    layout.BattlefieldRadius,
                    index))
                .ToArray();

            ValidateFoundationGateContract(layout, errors);
            var buildings = layout.Cells.Values.Where(cell => cell.IsBuildingCell).ToArray();
            if (buildings.Any(cell =>
                    cell.BuildingRole == HexCastleBuildingRole.None ||
                    cell.PlacementDensity == HexCastlePlacementDensity.None ||
                    cell.BuildingGrade <= 0))
            {
                errors.Add("정식 육각 성 건물 Cell에 역할·배치 열·등급 데이터가 빠졌습니다.");
            }

            ValidateReward(
                HexCastleBuildingRole.GoldStorage,
                HexCastleLootKind.Gold,
                difficultyProfile?.GoldStorageCount ?? 1);
            ValidateReward(
                HexCastleBuildingRole.EquipmentForge,
                HexCastleLootKind.Equipment,
                difficultyProfile?.EquipmentForgeCount ?? 1);
            ValidateReward(
                HexCastleBuildingRole.KeyVault,
                HexCastleLootKind.Key,
                difficultyProfile?.KeyVaultCount ?? 1);

            if (difficultyProfile != null)
            {
                ValidateRoleCount(
                    HexCastleBuildingRole.KnightBarracks,
                    difficultyProfile.KnightBarracksCount + HexCastleFoundationGenerator.PalaceGuardBarracksCount);
                ValidateRoleCount(HexCastleBuildingRole.FarmerBarracks, difficultyProfile.FarmerBarracksCount);
                ValidateRoleCount(
                    HexCastleBuildingRole.Turret,
                    difficultyProfile.TurretCount + HexCastleFoundationGenerator.PalaceGuardTurretCount);
                ValidateRoleCount(HexCastleBuildingRole.TrainingYard, difficultyProfile.TrainingYardCount);
                ValidateRoleCount(HexCastleBuildingRole.Church, difficultyProfile.ChurchCount);
            }

            var barracksCells = buildings.Where(cell =>
                         cell.BuildingRole == HexCastleBuildingRole.KnightBarracks ||
                         cell.BuildingRole == HexCastleBuildingRole.FarmerBarracks).ToArray();
            var minimumBarracksDefenseLayer = difficultyProfile?.DefenseLayerCount == 2 ? 1 : 2;
            var palaceGuardBarracks = barracksCells.Where(IsPalaceGuardBarracks).ToArray();
            if (palaceGuardBarracks.Length != HexCastleFoundationGenerator.PalaceGuardBarracksCount)
            {
                errors.Add("정식 육각 성 왕궁 바로 바깥에는 수비용 기사병영이 정확히 1개 필요합니다.");
            }

            var palaceGuardTurrets = buildings.Where(IsPalaceGuardTurret).ToArray();
            if (palaceGuardTurrets.Length != HexCastleFoundationGenerator.PalaceGuardTurretCount)
            {
                errors.Add("정식 육각 성 왕궁 바로 바깥에는 경비 포탑이 정확히 2개 필요합니다.");
            }

            if (palaceGuardBarracks.Length == 1 && palaceGuardTurrets.Any(value =>
                    value.Coordinates.DistanceTo(palaceGuardBarracks[0].Coordinates) <= 1))
            {
                errors.Add("정식 육각 성 왕궁 경비 포탑은 기사병영의 인접 소환 칸을 막으면 안 됩니다.");
            }

            foreach (var barracks in barracksCells)
            {
                if (!IsPalaceGuardBarracks(barracks) &&
                    barracks.DefenseLayer < minimumBarracksDefenseLayer)
                {
                    errors.Add(
                        $"정식 육각 성 병영 {barracks.Coordinates}은 {minimumBarracksDefenseLayer}번째 건물 Band부터 배치해야 합니다.");
                }

                var openNeighborCount = HexCoordinates.Directions.Count(direction =>
                    layout.TryGetCell(barracks.Coordinates + direction, out var neighbor) &&
                    neighbor.Kind == HexCastleCellKind.Ground &&
                    neighbor.IsOpen);
                if (openNeighborCount < 2)
                {
                    errors.Add(
                        $"정식 육각 성 병영 {barracks.Coordinates} 인접 이동 가능 빈 셀은 2개 이상이어야 합니다.");
                }
            }

            bool IsPalaceGuardBarracks(HexCastleCell cell)
            {
                return cell.BuildingRole == HexCastleBuildingRole.KnightBarracks &&
                       cell.DefenseLayer == 0 &&
                       cell.Coordinates.DistanceFromOrigin ==
                       HexCastleFoundationGenerator.PalaceFootprintRadius + 1;
            }

            bool IsPalaceGuardTurret(HexCastleCell cell)
            {
                return cell.BuildingRole == HexCastleBuildingRole.Turret &&
                       cell.DefenseLayer == 0 &&
                       cell.Coordinates.DistanceFromOrigin ==
                       HexCastleFoundationGenerator.PalaceFootprintRadius + 1;
            }

            foreach (var turret in buildings.Where(cell =>
                         cell.BuildingRole == HexCastleBuildingRole.Turret))
            {
                if (turret.TurretRangeCells < 2 || turret.TurretRangeCells > 4 ||
                    !turret.TurretCanAttackAcrossWalls)
                {
                    errors.Add($"정식 육각 성 포탑 {turret.Coordinates}의 사거리·벽 관통 규칙이 잘못됐습니다.");
                }

                if ((turret.TurretWeaponKind == HexCastleTurretWeaponKind.Cannon ||
                     turret.TurretWeaponKind == HexCastleTurretWeaponKind.Ballista) &&
                    turret.BuildingGrade > 2)
                {
                    errors.Add($"정식 육각 성 {turret.TurretWeaponKind} {turret.Coordinates}은 Lv2까지만 사용합니다.");
                }
            }

            foreach (var building in buildings)
            {
                if (IsPalaceGuardBarracks(building) || IsPalaceGuardTurret(building))
                {
                    continue;
                }

                var bandIndex = building.DefenseLayer - 1;
                if (bandIndex < 0 || bandIndex >= layout.WallRadii.Count - 1)
                {
                    errors.Add($"정식 육각 성 건물 {building.Coordinates}의 성벽 Band가 잘못됐습니다.");
                    continue;
                }

                var topology = bandTopologies[bandIndex];
                var expectedDensity = topology.DenseRow.Contains(building.Coordinates)
                    ? HexCastlePlacementDensity.Dense
                    : HexCastlePlacementDensity.Sparse;
                if (!topology.Cells.Contains(building.Coordinates) ||
                    building.PlacementDensity != expectedDensity)
                {
                    errors.Add($"정식 육각 성 건물 {building.Coordinates}의 밀집·분산 열 판정이 잘못됐습니다.");
                }
            }

            var openGateApproaches = new HashSet<HexCoordinates>();
            foreach (var gate in layout.Enumerate(HexCastleCellKind.Gate).Where(cell =>
                         cell.GateRole == HexCastleGateRole.OpenDefenderPassage))
            {
                for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
                {
                    if ((gate.GatePassageMask & 1 << direction) != 0)
                    {
                        openGateApproaches.Add(gate.Coordinates.Neighbor(direction));
                    }
                }
            }

            var barracksNeighborCells = new HashSet<HexCoordinates>(
                barracksCells.SelectMany(barracks =>
                    HexCoordinates.Directions.Select(direction =>
                        barracks.Coordinates + direction)));

            for (var bandIndex = 0; bandIndex < layout.WallRadii.Count - 1; bandIndex++)
            {
                var topology = bandTopologies[bandIndex];
                if (!buildings.Any(cell =>
                        cell.DefenseLayer == bandIndex + 1 &&
                        topology.DenseRow.Contains(cell.Coordinates) &&
                        cell.PlacementDensity == HexCastlePlacementDensity.Dense))
                {
                    errors.Add($"정식 육각 성 성벽 {bandIndex + 1} 바깥 밀집 열이 비었습니다.");
                }

                var unexplainedFirstRowGaps = layout.Cells.Values
                    .Where(cell =>
                        topology.DenseRow.Contains(cell.Coordinates) &&
                        cell.Kind == HexCastleCellKind.Ground &&
                        !openGateApproaches.Contains(cell.Coordinates) &&
                        !barracksNeighborCells.Contains(cell.Coordinates))
                    .Select(cell => cell.Coordinates)
                    .ToArray();
                if (unexplainedFirstRowGaps.Length > 0)
                {
                    errors.Add(
                        $"정식 육각 성 성벽 {bandIndex + 1} 바로 바깥 첫 열에 불필요한 빈 Cell이 있습니다: " +
                        string.Join(", ", unexplainedFirstRowGaps));
                }

                if (topology.SparseRows.Count > 0 &&
                    !buildings.Any(cell =>
                        cell.DefenseLayer == bandIndex + 1 &&
                        topology.SparseRows.Contains(cell.Coordinates) &&
                        cell.PlacementDensity == HexCastlePlacementDensity.Sparse))
                {
                    errors.Add($"정식 육각 성 성벽 {bandIndex + 1} 바깥 분산 열이 비었습니다.");
                }
            }

            void ValidateReward(HexCastleBuildingRole role, HexCastleLootKind lootKind, int expectedCount)
            {
                var matches = buildings.Where(cell =>
                    cell.BuildingRole == role && cell.LootKind == lootKind).ToArray();
                if (matches.Length != expectedCount ||
                    matches.Any(value => value.Kind != HexCastleCellKind.RewardBuilding))
                {
                    errors.Add($"정식 육각 성 {role} 보상 건물은 정확히 {expectedCount}개여야 합니다.");
                }
            }

            void ValidateRoleCount(HexCastleBuildingRole role, int expectedCount)
            {
                var actualCount = buildings.Count(cell => cell.BuildingRole == role);
                if (actualCount != expectedCount)
                {
                    errors.Add($"난이도 {layout.DifficultyLevel} {role} 수가 잘못됐습니다: {actualCount}/{expectedCount}");
                }
            }
        }

        private static void ValidateFoundationGateContract(
            HexCastleLayout layout,
            ICollection<string> errors)
        {
            var difficultyProfile = layout.DifficultyLevel > 0
                ? HexCastleDifficultyProfile.Resolve(layout.DifficultyLevel, layout.Seed)
                : null;
            var requiredGateSocketCount = ResolveRequiredGateSocketCount(layout, difficultyProfile);
            var plan = HexCastleSilhouettePlanner.Build(
                layout.Theme,
                layout.Seed,
                layout.WallRadii,
                requiredGateSocketCount);
            var gates = layout.Enumerate(HexCastleCellKind.Gate).ToArray();
            var closed = gates.Where(value => value.GateRole == HexCastleGateRole.ClosedWall).ToArray();
            var open = gates.Where(value => value.GateRole == HexCastleGateRole.OpenDefenderPassage).ToArray();
            if (closed.Length == 0 || closed.Any(value =>
                    !value.HasExplicitGateState ||
                    value.WallRole == HexCastleWallRole.None ||
                    value.WallRole == HexCastleWallRole.Partition ||
                    value.DefenseLayer < 1 ||
                    value.DefenseLayer > plan.Rings.Count ||
                    !plan.Rings[value.DefenseLayer - 1].Cells.Contains(value.Coordinates) ||
                    value.GatePassageMask != 0 ||
                    !value.InitialBlocked))
            {
                errors.Add("정식 육각 성 닫힌 성문은 체력이 있는 1~4중 성벽 Cell이어야 합니다.");
            }

            foreach (var ring in plan.Rings)
            {
                var ringClosed = closed
                    .Where(value => value.DefenseLayer == ring.DefenseLayer)
                    .ToArray();
                if (ringClosed.Length == 0)
                {
                    errors.Add($"정식 테마 {ring.DefenseLayer}중벽에 닫힌 성문이 없습니다.");
                    continue;
                }

                if (ringClosed.GroupBy(value => value.RegionId)
                    .Any(group => group.Count() > 2))
                {
                    errors.Add($"정식 테마 {ring.DefenseLayer}중벽의 한 면에 닫힌 성문이 2개를 초과했습니다.");
                }

                if (difficultyProfile != null &&
                    ringClosed.Length != ResolveExpectedClosedGateCount(ring.DefenseLayer))
                {
                    var expectedCount = ResolveExpectedClosedGateCount(ring.DefenseLayer);
                    errors.Add(
                        $"난이도 {layout.DifficultyLevel} {ring.DefenseLayer}중벽 닫힌 성문 수가 잘못됐습니다: " +
                        $"{ringClosed.Length}/{expectedCount}");
                }
            }

            int ResolveExpectedClosedGateCount(int defenseLayer)
            {
                var straightSocketCount = layout.Cells.Values
                    .Where(value =>
                        value.DefenseLayer == defenseLayer &&
                        value.WallRole != HexCastleWallRole.Partition &&
                        (value.Kind == HexCastleCellKind.Wall ||
                         value.Kind == HexCastleCellKind.Tower ||
                         value.Kind == HexCastleCellKind.Gate))
                    .Where(value =>
                    {
                        var topology = new HexCastleWallCellTopology(
                            value.Coordinates,
                            value.WallConnectionMask);
                        return topology.ConnectionCount == 2 &&
                               topology.ResolveTwoWaySeparation() == 3;
                    })
                    .GroupBy(value => value.RegionId)
                    .Sum(group => Math.Min(group.Count(), 2));
                return Math.Min(
                    difficultyProfile.ClosedGateCountPerWallRing,
                    straightSocketCount);
            }

            foreach (var gate in closed)
            {
                var sameTierWallHealth = layout.Cells.Values
                    .Where(value => (value.Kind == HexCastleCellKind.Wall || value.Kind == HexCastleCellKind.Tower) &&
                                  value.WallTier == gate.WallTier)
                    .Select(value => value.HitPoints)
                    .DefaultIfEmpty(0f)
                    .Max();
                if (sameTierWallHealth <= 0f || gate.HitPoints >= sameTierWallHealth)
                {
                    errors.Add($"닫힌 성문 {gate.Coordinates}의 체력은 같은 등급 성벽보다 낮아야 합니다.");
                }
            }

            for (var bandIndex = 0; bandIndex < layout.WallRadii.Count - 1; bandIndex++)
            {


                var gateCount = open.Count(value => value.DefenseLayer == bandIndex + 1);
                if (gateCount < 1 || gateCount > 2)
                {
                    errors.Add($"정식 육각 성 격벽 Band {bandIndex + 1} 열린 성문 수는 1~2개여야 합니다.");
                }


                if (difficultyProfile != null)
                {
                    var desiredCount = HexCastleFoundationGenerator.ResolveOpenPartitionGateTargetCount(
                        layout.Seed,
                        bandIndex,
                        HexCastleThemeOneTuning.CreateDraftDefaults(),
                        difficultyProfile);
                    var expectedCount = Math.Min(
                        desiredCount,
                        HexCastleSilhouettePlanner.CountFormalGateSocketPaths(plan, bandIndex));
                    if (gateCount != expectedCount)
                    {
                        errors.Add(
                            $"난이도 {layout.DifficultyLevel} 격벽 Band {bandIndex + 1} 열린 성문 수가 잘못됐습니다: " +
                            $"{gateCount}/{expectedCount}");
                    }
                }
            }

            foreach (var gate in open)
            {
                if (!gate.HasExplicitGateState ||
                    gate.WallRole != HexCastleWallRole.Partition ||
                    !gate.InitialBlocked ||
                    !HexCastleCell.IsValidGatePassageMask(
                        gate.WallConnectionMask,
                        gate.GatePassageMask) ||
                    !gate.CanTraverseWithoutBreaking(HexCastleTraversalFaction.Defender) ||
                    gate.CanTraverseWithoutBreaking(HexCastleTraversalFaction.Assault))
                {
                    errors.Add($"열린 격벽 성문 {gate.Coordinates}의 수비대 전용 통행 규칙이 잘못됐습니다.");
                    continue;
                }

                var approachCount = 0;
                for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
                {
                    if ((gate.GatePassageMask & 1 << direction) == 0)
                    {
                        continue;
                    }

                    approachCount++;
                    if (!layout.TryGetCell(gate.Coordinates.Neighbor(direction), out var approach) ||
                        (approach.Kind != HexCastleCellKind.Ground &&
                         approach.Kind != HexCastleCellKind.Reserved) ||
                        !approach.IsOpen)
                    {
                        errors.Add($"열린 격벽 성문 {gate.Coordinates}의 앞뒤 인접 빈 Cell이 보존되지 않았습니다.");
                    }
                }

                if (approachCount != 2)
                {
                    errors.Add($"열린 격벽 성문 {gate.Coordinates}의 통로 방향은 정확히 2개여야 합니다.");
                }
            }
        }

        private static int ResolveRequiredGateSocketCount(
            HexCastleLayout layout,
            HexCastleDifficultyProfile difficultyProfile)
        {
            if (difficultyProfile == null)
            {
                return 1;
            }

            var tuning = HexCastleThemeOneTuning.CreateDraftDefaults();
            return Enumerable.Range(0, layout.WallRadii.Count - 1)
                .Max(bandIndex => HexCastleFoundationGenerator.ResolveOpenPartitionGateTargetCount(
                    layout.Seed,
                    bandIndex,
                    tuning,
                    difficultyProfile));
        }
    }

    public sealed class HexCastleDifficultyEvaluator
    {
        public HexCastleDifficultyReport Evaluate(
            HexCastleLayout layout,
            IReadOnlyList<HexRouteResult> routes)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            var complete = (routes ?? Array.Empty<HexRouteResult>())
                .Where(route => route != null && route.IsComplete)
                .ToArray();
            var minimum = complete.Length == 0 ? 0f : complete.Min(route => route.TotalCost);
            var average = complete.Length == 0 ? 0f : complete.Average(route => route.TotalCost);
            var maximum = complete.Length == 0 ? 0f : complete.Max(route => route.TotalCost);
            var totalHealth = layout.Cells.Values.Where(cell => cell.IsBreakable).Sum(cell => cell.HitPoints);
            var reward = layout.Cells.Values.Sum(cell => cell.RewardValue);
            var totalBuildingGrade = layout.Cells.Values.Where(cell => cell.IsBuildingCell)
                .Sum(cell => cell.BuildingGrade);
            var routeGrades = complete.Select(route => route.Path
                    .Select(coordinates => layout.Cells[coordinates])
                    .Where(cell => cell.IsBuildingCell)
                    .Sum(cell => cell.BuildingGrade))
                .ToArray();
            var minimumRouteGrade = routeGrades.Length == 0 ? 0f : routeGrades.Min();
            var averageRouteGrade = routeGrades.Length == 0 ? 0f : (float)routeGrades.Average();
            var themeComplexity = 1f + (int)layout.Theme * 0.025f;
            var score = (average * 0.035f + maximum * 0.015f + totalHealth * 0.0012f +
                         totalBuildingGrade * 0.25f + averageRouteGrade * 3.5f +
                         layout.DefenseLayerCount * 18f) * themeComplexity;
            var suggestedStage = Mathf.Clamp(Mathf.RoundToInt(score / 18f), 1, 50);
            return new HexCastleDifficultyReport(
                minimum,
                average,
                maximum,
                totalHealth,
                reward,
                totalBuildingGrade,
                minimumRouteGrade,
                averageRouteGrade,
                score,
                suggestedStage);
        }
    }
}
