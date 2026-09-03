using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectMT.Contents.CastleRaidHex
{
    public sealed class HexCastleFoundationGenerator // 공통 전투 규칙과 실루엣 테마를 조합한다
    {
        private sealed class WallDraft
        {
            public int DefenseLayer;
            public HexCastleWallRole WallRole;
            public int WallTier;
            public float Health;
            public int RegionId;
            public string PathId;
            public int PathIndex;
            public int PathLength;
            public HexCastleGateRole GateRole;
            public int GatePassageMask;
        }

        private sealed class BuildingSlot
        {
            public HexCoordinates Coordinates;
            public int BandIndex;
            public int RegionId;
            public int Radius;
            public HexCastlePlacementDensity Density;
            public int Score;
            public bool IsPrimary;
        }

        private sealed class GateCandidate
        {
            public HexCoordinates Coordinates;
            public string PathId;
            public int FaceIndex;
            public int PassageMask;
            public bool IsOuterHalf;
            public int Score;
        }

        private sealed class BarracksExitReservation
        {
            public HexCoordinates Exit;
            public BuildingSlot FallbackExitSlot;
            public BuildingSlot OccupiedSlot;
            public BuildingSlot ReplacementSlot;
        }

        public const int PalaceFootprintRadius = 1;
        public const int PalaceGuardBarracksCount = 1;
        public const int PalaceGuardTurretCount = 2;
        public const int FoundationRulesVersionBase = 1001;

        private static readonly int[] CanonicalWallRadii = { 3, 5, 8, 11 };

        public static IReadOnlyList<int> ResolveCanonicalWallRadii(int defenseLayerCount)
        {
            if (defenseLayerCount < 2 || defenseLayerCount > CanonicalWallRadii.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(defenseLayerCount), "방어선은 2~4중이어야 합니다.");
            }

            return CanonicalWallRadii.Take(defenseLayerCount).ToArray();
        }

        public HexCastleLayout Generate(
            int seed,
            int defenseLayerCount,
            HexCastleTheme theme = HexCastleTheme.CentralCompartment,
            HexCastleThemeOneTuning tuning = null)
        {
            return GenerateInternal(seed, defenseLayerCount, theme, tuning, null);
        }

        public HexCastleLayout GenerateForDifficulty(
            int seed,
            int difficultyLevel,
            HexCastleTheme theme = HexCastleTheme.CentralCompartment,
            HexCastleThemeOneTuning tuning = null)
        {
            var profile = HexCastleDifficultyProfile.Resolve(difficultyLevel, seed);
            return GenerateInternal(seed, profile.DefenseLayerCount, theme, tuning, profile);
        }

        private static HexCastleLayout GenerateInternal(
            int seed,
            int defenseLayerCount,
            HexCastleTheme theme,
            HexCastleThemeOneTuning tuning,
            HexCastleDifficultyProfile difficultyProfile)
        {
            if (defenseLayerCount < 2 || defenseLayerCount > CanonicalWallRadii.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(defenseLayerCount), "방어선은 2~4중이어야 합니다.");
            }

            if (!HexCastleSilhouettePlanner.SupportedThemes.Contains(theme))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(theme),
                    $"{theme}은 정식 육각 성 테마가 아닙니다.");
            }

            tuning = tuning ?? HexCastleThemeOneTuning.CreateDraftDefaults();
            tuning.Validate(defenseLayerCount);

            var wallRadii = ResolveCanonicalWallRadii(defenseLayerCount).ToArray();
            var requiredGateSocketCount = difficultyProfile == null
                ? 1
                : Enumerable.Range(0, wallRadii.Length - 1)
                    .Max(bandIndex => ResolveOpenPartitionGateTargetCount(
                        seed,
                        bandIndex,
                        tuning,
                        difficultyProfile));
            var plan = HexCastleSilhouettePlanner.Build(
                theme,
                seed,
                wallRadii,
                requiredGateSocketCount);
            var buildRadius = plan.MaximumRadius;
            var boardRadius = buildRadius + HexSpatialContract.MinimumDeploymentRings;
            var request = new HexCastleGenerationRequest(
                seed,
                theme,
                defenseLayerCount,
                boardRadius,
                buildRadius,
                PalaceFootprintRadius,
                difficultyProfile?.Level ?? 0);
            var cells = HexCoordinates.EnumerateRadius(boardRadius).ToDictionary(
                coordinate => coordinate,
                coordinate => CreateOpenCell(coordinate, buildRadius));

            ApplyPalace(cells, tuning);
            var reservedGateApproaches = ApplyWallNetwork(
                cells,
                seed,
                plan,
                wallRadii,
                tuning,
                difficultyProfile);
            ExpandExteriorDeployment(cells); // 실제 성벽 굴곡 바깥의 열린 바닥까지 배치 영역으로 연결
            var palaceGuardDirection = ApplyPalaceGuardBarracks(
                cells,
                seed,
                defenseLayerCount,
                tuning,
                difficultyProfile);
            ApplyPalaceGuardTurrets(cells, seed, defenseLayerCount, tuning, palaceGuardDirection);
            ApplyFoundationBuildings(
                cells,
                seed,
                plan,
                wallRadii,
                tuning,
                reservedGateApproaches,
                difficultyProfile);
            var traps = HexCastleTrapPlanner.Build(
                cells,
                wallRadii,
                difficultyProfile,
                buildRadius,
                seed);
            return new HexCastleLayout(
                request,
                cells,
                wallRadii,
                FoundationRulesVersionBase + tuning.DraftVersion,
                traps);
        }

        private static HexCastleCell CreateOpenCell(HexCoordinates coordinate, int outerWallRadius)
        {
            return coordinate.DistanceFromOrigin > outerWallRadius
                ? new HexCastleCell(coordinate, HexCastleCellKind.Deployment)
                : new HexCastleCell(coordinate, HexCastleCellKind.Ground);
        }

        private static void ExpandExteriorDeployment(IDictionary<HexCoordinates, HexCastleCell> cells)
        {
            var queue = new Queue<HexCoordinates>(cells.Values
                .Where(cell => cell.Kind == HexCastleCellKind.Deployment)
                .Select(cell => cell.Coordinates));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var direction in HexCoordinates.Directions)
                {
                    var next = current + direction;
                    if (!cells.TryGetValue(next, out var cell) || cell.Kind != HexCastleCellKind.Ground)
                    {
                        continue;
                    }

                    cells[next] = new HexCastleCell(next, HexCastleCellKind.Deployment);
                    queue.Enqueue(next);
                }
            }
        }

        private static void ApplyPalace(
            IDictionary<HexCoordinates, HexCastleCell> cells,
            HexCastleThemeOneTuning tuning)
        {
            foreach (var coordinates in HexCoordinates.EnumerateRadius(PalaceFootprintRadius))
            {
                var isVisualOwner = coordinates.DistanceFromOrigin == 0;
                cells[coordinates] = new HexCastleCell(
                    coordinates,
                    HexCastleCellKind.Palace,
                    hitPoints: tuning.PalaceHealth,
                    rewardValue: isVisualOwner ? tuning.PalaceRewardValue : 0,
                    initialBlocked: true,
                    placementId: $"PALACE_001_C{coordinates.Q}_{coordinates.R}",
                    visualVariantId: isVisualOwner
                        ? "building_castle_blue"
                        : "PalaceFootprint");
            }
        }

        private static IReadOnlyCollection<HexCoordinates> ApplyWallNetwork(
            IDictionary<HexCoordinates, HexCastleCell> cells,
            int seed,
            HexCastleSilhouettePlan plan,
            IReadOnlyList<int> wallRadii,
            HexCastleThemeOneTuning tuning,
            HexCastleDifficultyProfile difficultyProfile)
        {
            var drafts = new Dictionary<HexCoordinates, WallDraft>();
            var connectionMasks = new Dictionary<HexCoordinates, int>();
            foreach (var ring in plan.Rings)
            {
                var path = ring.Cells;
                var oneBasedLayer = ring.DefenseLayer;
                var layerIndex = oneBasedLayer - 1;
                var wallTier = HexCastleParityContract.ResolveWallTier(oneBasedLayer);
                var wallRole = ResolveWallRole(layerIndex, wallRadii.Count);
                var pathId = $"WALL_RING_L{oneBasedLayer:00}";
                for (var pathIndex = 0; pathIndex < path.Count; pathIndex++)
                {
                    var coordinates = path[pathIndex];
                    drafts[coordinates] = new WallDraft
                    {
                        DefenseLayer = oneBasedLayer,
                        WallRole = wallRole,
                        WallTier = wallTier,
                        Health = tuning.ResolveWallHealth(wallTier),
                        RegionId = ResolveRegion(coordinates),
                        PathId = pathId,
                        PathIndex = pathIndex,
                        PathLength = path.Count,
                    };
                    connectionMasks[coordinates] = 0;
                }

                for (var pathIndex = 0; pathIndex < path.Count; pathIndex++)
                {
                    AddConnection(
                        connectionMasks,
                        path[pathIndex],
                        path[(pathIndex + 1) % path.Count]);
                }
            }

            foreach (var partition in plan.Partitions)
            {
                var direction = partition.Direction;
                var partitionPath = partition.Cells;
                for (var pathIndex = 0; pathIndex < partitionPath.Count; pathIndex++)
                {
                    var coordinates = partitionPath[pathIndex];
                    if (drafts.ContainsKey(coordinates))
                    {
                        continue;
                    }

                    drafts.Add(coordinates, new WallDraft
                    {
                        DefenseLayer = partition.BandIndex + 1,
                        WallRole = HexCastleWallRole.Partition,
                        WallTier = HexCastleParityContract.MinimumWallTier,
                        Health = tuning.ResolveWallHealth(HexCastleParityContract.MinimumWallTier),
                        RegionId = direction + 1,
                        PathId = $"PARTITION_B{partition.BandIndex + 1:00}_D{direction}",
                        PathIndex = pathIndex,
                        PathLength = partitionPath.Count,
                    });
                    connectionMasks[coordinates] = 0;
                }

                for (var pathIndex = 0; pathIndex < partitionPath.Count - 1; pathIndex++)
                {
                    AddConnection(
                        connectionMasks,
                        partitionPath[pathIndex],
                        partitionPath[pathIndex + 1]);
                }
            }

            var reservedGateApproaches = ApplyGates(
                cells,
                drafts,
                connectionMasks,
                seed,
                wallRadii,
                tuning,
                difficultyProfile);
            foreach (var pair in drafts)
            {
                var topology = new HexCastleWallCellTopology(
                    pair.Key,
                    connectionMasks[pair.Key]);
                if (topology.ConnectionCount < 2 || topology.ConnectionCount > 4)
                {
                    throw new InvalidOperationException(
                        $"{plan.Theme} 성벽망 {pair.Key}의 연결 수가 잘못됐습니다: {topology.ConnectionCount}");
                }

                var draft = pair.Value;
                var kind = draft.GateRole != HexCastleGateRole.None
                    ? HexCastleCellKind.Gate
                    : topology.IsJunction || !CanResolveAsWall(topology)
                        ? HexCastleCellKind.Tower
                        : HexCastleCellKind.Wall;
                cells[pair.Key] = new HexCastleCell(
                    pair.Key,
                    kind,
                    draft.DefenseLayer,
                    draft.Health,
                    draft.WallRole,
                    districtId: draft.RegionId,
                    regionId: draft.RegionId,
                    initialBlocked: true,
                    wallTier: draft.WallTier,
                    pathId: draft.PathId,
                    pathIndex: draft.PathIndex,
                    placementId: $"CELL_{pair.Key.Q}_{pair.Key.R}",
                    visualVariantId: draft.GateRole == HexCastleGateRole.OpenDefenderPassage
                        ? "GateOpenDoubleSided"
                        : draft.GateRole == HexCastleGateRole.ClosedWall
                            ? "GateClosedDoubleSided"
                            : kind == HexCastleCellKind.Tower
                                ? "TowerHub"
                                : "Auto",
                    wallConnectionMask: topology.ConnectionMask,
                    gateRole: draft.GateRole == HexCastleGateRole.None
                        ? (HexCastleGateRole?)null
                        : draft.GateRole,
                    gatePassageMask: draft.GateRole == HexCastleGateRole.None
                        ? (int?)null
                        : draft.GatePassageMask);
            }

            return reservedGateApproaches;
        }

        private static IReadOnlyCollection<HexCoordinates> ApplyGates(
            IDictionary<HexCoordinates, HexCastleCell> cells,
            IDictionary<HexCoordinates, WallDraft> drafts,
            IDictionary<HexCoordinates, int> connectionMasks,
            int seed,
            IReadOnlyList<int> wallRadii,
            HexCastleThemeOneTuning tuning,
            HexCastleDifficultyProfile difficultyProfile)
        {
            var reservedApproaches = new HashSet<HexCoordinates>();
            var desiredClosedGateCount = difficultyProfile?.ClosedGateCountPerWallRing ??
                                         tuning.ClosedGateCountPerWallRing;
            for (var layerIndex = 0; layerIndex < wallRadii.Count; layerIndex++)
            {
                var closedCandidates = drafts
                    .Where(pair =>
                        pair.Value.DefenseLayer == layerIndex + 1 &&
                        pair.Value.WallRole != HexCastleWallRole.Partition &&
                        IsStraightGateCandidate(connectionMasks[pair.Key]))
                    .Select(pair => new GateCandidate
                    {
                        Coordinates = pair.Key,
                        PathId = pair.Value.PathId,
                        FaceIndex = pair.Value.RegionId - 1,
                        Score = ResolvePlacementScore(
                            seed ^ 0x2C71,
                            layerIndex,
                            pair.Value.RegionId,
                            pair.Key)
                    })
                    .OrderBy(value => value.Score)
                    .ThenBy(value => value.Coordinates)
                    .ToArray();
                var achievableClosedGateCount = closedCandidates
                    .GroupBy(value => value.FaceIndex)
                    .Sum(group => Math.Min(group.Count(), tuning.ClosedGateMaximumPerFace));
                var closedGateCount = Math.Min(
                    desiredClosedGateCount,
                    achievableClosedGateCount);
                if (closedGateCount < 1)
                {
                    throw new InvalidOperationException(
                        $"{layerIndex + 1}중벽에 닫힌 성문용 직선 소켓이 없습니다.");
                }

                var selectedClosed = new List<GateCandidate>();
                var faceCounts = new int[HexCoordinates.Directions.Length];

                foreach (var candidate in closedCandidates)
                {
                    if (faceCounts[candidate.FaceIndex] >= tuning.ClosedGateMaximumPerFace ||
                        selectedClosed.Any(value => value.Coordinates.DistanceTo(candidate.Coordinates) < 3))
                    {
                        continue;
                    }

                    selectedClosed.Add(candidate);
                    faceCounts[candidate.FaceIndex]++;
                    if (selectedClosed.Count == closedGateCount)
                    {
                        break;
                    }
                }

                if (selectedClosed.Count < closedGateCount)
                {
                    foreach (var candidate in closedCandidates.Where(value => !selectedClosed.Contains(value)))
                    {
                        if (faceCounts[candidate.FaceIndex] >= tuning.ClosedGateMaximumPerFace)
                        {
                            continue;
                        }

                        selectedClosed.Add(candidate);
                        faceCounts[candidate.FaceIndex]++;
                        if (selectedClosed.Count == closedGateCount)
                        {
                            break;
                        }
                    }
                }

                if (selectedClosed.Count != closedGateCount)
                {
                    throw new InvalidOperationException(
                        $"{layerIndex + 1}중벽 닫힌 성문 후보가 부족합니다: " +
                        $"{selectedClosed.Count}/{closedGateCount}");
                }

                foreach (var selected in selectedClosed)
                {
                    var draft = drafts[selected.Coordinates];
                    draft.GateRole = HexCastleGateRole.ClosedWall;
                    draft.GatePassageMask = 0;
                    draft.Health = tuning.ResolveClosedGateHealth(draft.WallTier);
                }
            }

            for (var bandIndex = 0; bandIndex < wallRadii.Count - 1; bandIndex++)
            {
                var candidates = new List<GateCandidate>();
                foreach (var pair in drafts)
                {
                    var draft = pair.Value;
                    if (draft.WallRole != HexCastleWallRole.Partition ||
                        draft.DefenseLayer != bandIndex + 1 ||
                        !IsStraightGateCandidate(connectionMasks[pair.Key]) ||
                        !TryResolvePartitionGatePassageMask(
                            pair.Key,
                            connectionMasks[pair.Key],
                            cells,
                            drafts,
                            out var passageMask))
                    {
                        continue;
                    }

                    candidates.Add(new GateCandidate
                    {
                        Coordinates = pair.Key,
                        PathId = draft.PathId,
                        PassageMask = passageMask,
                        IsOuterHalf = draft.PathIndex >= draft.PathLength / 2,
                        Score = ResolvePlacementScore(
                            seed ^ 0x5A17,
                            bandIndex,
                            draft.RegionId,
                            pair.Key)
                    });
                }

                if (candidates.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"격벽 Band {bandIndex + 1}에 직선 열린 성문 소켓이 없습니다.");
                }

                var targetGateCount = Math.Min(
                    ResolveOpenPartitionGateTargetCount(seed, bandIndex, tuning, difficultyProfile),
                    candidates.Count);
                var orderedCandidates = candidates
                    .OrderByDescending(value => value.IsOuterHalf)
                    .ThenByDescending(value => value.Coordinates.DistanceFromOrigin)
                    .ThenBy(value => value.Score)
                    .ThenBy(value => value.Coordinates)
                    .ToArray();
                var selected = new List<GateCandidate>();
                SelectCandidates(requireDifferentPath: true, requireSeparation: true);
                SelectCandidates(requireDifferentPath: false, requireSeparation: true);
                SelectCandidates(requireDifferentPath: false, requireSeparation: false);

                foreach (var candidate in selected)
                {
                    var draft = drafts[candidate.Coordinates];
                    draft.GateRole = HexCastleGateRole.OpenDefenderPassage;
                    draft.GatePassageMask = candidate.PassageMask;
                    for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
                    {
                        if ((candidate.PassageMask & 1 << direction) != 0)
                        {
                            reservedApproaches.Add(candidate.Coordinates.Neighbor(direction));
                        }
                    }
                }

                if (selected.Count != targetGateCount)
                {
                    throw new InvalidOperationException(
                        $"격벽 Band {bandIndex + 1} 열린 성문 후보가 부족합니다: " +
                        $"{selected.Count}/{targetGateCount}");
                }

                void SelectCandidates(bool requireDifferentPath, bool requireSeparation)
                {
                    foreach (var candidate in orderedCandidates)
                    {
                        if (selected.Count == targetGateCount)
                        {
                            return;
                        }

                        if (selected.Any(value => value.Coordinates == candidate.Coordinates) ||
                            requireDifferentPath && selected.Any(value =>
                                string.Equals(value.PathId, candidate.PathId, StringComparison.Ordinal)) ||
                            requireSeparation && selected.Any(value =>
                                value.Coordinates.DistanceTo(candidate.Coordinates) < 3))
                        {
                            continue;
                        }

                        selected.Add(candidate);
                    }
                }
            }

            return reservedApproaches;
        }

        private static bool IsStraightGateCandidate(int connectionMask)
        {
            var topology = new HexCastleWallCellTopology(default, connectionMask);
            return topology.ConnectionCount == 2 && topology.ResolveTwoWaySeparation() == 3;
        }

        internal static int ResolveOpenPartitionGateTargetCount(
            int seed,
            int bandIndex,
            HexCastleThemeOneTuning tuning,
            HexCastleDifficultyProfile difficultyProfile)
        {
            var result = difficultyProfile?.OpenPartitionGateCountPerBand ??
                         tuning.OpenPartitionGateCountPerBand;
            var maximum = difficultyProfile?.OpenPartitionGateMaximumPerBand ??
                          tuning.OpenPartitionGateMaximumPerBand;
            var additionalChance = difficultyProfile?.OpenPartitionAdditionalGateChance ??
                                   tuning.OpenPartitionAdditionalGateChance;
            if (result >= maximum || additionalChance <= 0f)
            {
                return result;
            }

            var score = ResolvePlacementScore(
                seed ^ 0x6B31,
                bandIndex,
                bandIndex + 1,
                new HexCoordinates(bandIndex + 1, -bandIndex));
            var threshold = (int)(additionalChance * 10000f);
            return score % 10000 < threshold
                ? Math.Min(result + 1, maximum)
                : result;
        }

        private static bool CanResolveAsWall(HexCastleWallCellTopology topology)
        {
            if (topology.ConnectionCount != 2)
            {
                return false;
            }

            var directions = topology.GetDirections();
            try
            {
                HexCastleWallVisualResolver.ResolveDirections(
                    HexCastleCellKind.Wall,
                    directions[0],
                    directions[1]);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static bool TryResolvePartitionGatePassageMask(
            HexCoordinates coordinates,
            int connectionMask,
            IDictionary<HexCoordinates, HexCastleCell> cells,
            IDictionary<HexCoordinates, WallDraft> drafts,
            out int passageMask)
        {
            var topology = new HexCastleWallCellTopology(coordinates, connectionMask);
            var wallDirections = topology.GetDirections();
            if (wallDirections.Length != 2 || topology.ResolveTwoWaySeparation() != 3)
            {
                passageMask = 0;
                return false;
            }

            var wallDirection = wallDirections[0];
            var firstCrossing = (1 << PositiveModulo(wallDirection + 1, 6)) |
                                (1 << PositiveModulo(wallDirection + 5, 6));
            var secondCrossing = (1 << PositiveModulo(wallDirection + 2, 6)) |
                                 (1 << PositiveModulo(wallDirection + 4, 6));
            foreach (var candidateMask in new[] { firstCrossing, secondCrossing })
            {
                var valid = true;
                for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
                {
                    if ((candidateMask & 1 << direction) == 0)
                    {
                        continue;
                    }

                    var approach = coordinates.Neighbor(direction);
                    if (!cells.TryGetValue(approach, out var cell) ||
                        cell.Kind != HexCastleCellKind.Ground ||
                        drafts.ContainsKey(approach))
                    {
                        valid = false;
                        break;
                    }
                }

                if (valid)
                {
                    passageMask = candidateMask;
                    return true;
                }
            }

            passageMask = 0;
            return false;
        }

        private static void AddConnection(
            IDictionary<HexCoordinates, int> connectionMasks,
            HexCoordinates first,
            HexCoordinates second)
        {
            if (!connectionMasks.ContainsKey(first) || !connectionMasks.ContainsKey(second))
            {
                throw new InvalidOperationException($"성벽 연결 대상 Cell이 없습니다: {first} <-> {second}");
            }

            var direction = HexCastleWallVisualResolver.ResolveNeighborDirection(first, second);
            var opposite = (direction + 3) % HexCoordinates.Directions.Length;
            connectionMasks[first] |= 1 << direction;
            connectionMasks[second] |= 1 << opposite;
        }

        private static void ApplyFoundationBuildings(
            IDictionary<HexCoordinates, HexCastleCell> cells,
            int seed,
            HexCastleSilhouettePlan plan,
            IReadOnlyList<int> wallRadii,
            HexCastleThemeOneTuning tuning,
            IReadOnlyCollection<HexCoordinates> reservedGateApproaches,
            HexCastleDifficultyProfile difficultyProfile)
        {
            var allSlots = ResolveBuildingSlots(
                cells,
                seed,
                plan,
                tuning,
                reservedGateApproaches).ToList();
            var available = allSlots.Where(slot => slot.IsPrimary).ToList();
            var fallback = allSlots.Where(slot => !slot.IsPrimary).ToList();
            var reservedBarracksExitCells = new HashSet<HexCoordinates>();
            var quota = tuning.ResolveLayerQuota(wallRadii.Count);
            var knightBarracksCount = difficultyProfile?.KnightBarracksCount ?? quota.KnightBarracksCount;
            var farmerBarracksCount = difficultyProfile?.FarmerBarracksCount ?? quota.FarmerBarracksCount;
            var turretCount = difficultyProfile?.TurretCount ?? quota.TurretCount;
            var trainingYardCount = difficultyProfile?.TrainingYardCount ?? quota.TrainingYardCount;
            var churchCount = difficultyProfile?.ChurchCount ?? quota.ChurchCount;
            var minimumBlockerGradeSum = difficultyProfile?.MinimumBlockerGradeSum ??
                                         quota.MinimumBlockerGradeSum;
            var goldStorageCount = difficultyProfile?.GoldStorageCount ?? 1;
            var equipmentForgeCount = difficultyProfile?.EquipmentForgeCount ?? 1;
            var keyVaultCount = difficultyProfile?.KeyVaultCount ?? 1;
            var minimumBarracksDefenseLayer = difficultyProfile?.DefenseLayerCount == 2
                ? 1
                : tuning.MinimumBarracksDefenseLayer;
            var maximumBlockerGrade = tuning.BlockerVariants.Max(value => value.Grade);
            var requiredBlockerSlotCount = (int)Math.Ceiling(
                minimumBlockerGradeSum / (double)Math.Max(1, maximumBlockerGrade));
            var requiredSpecialCount = knightBarracksCount + farmerBarracksCount + turretCount +
                                       trainingYardCount + churchCount + goldStorageCount +
                                       equipmentForgeCount + keyVaultCount;
            var requiredCount = requiredSpecialCount + requiredBlockerSlotCount;
            foreach (var promoted in fallback
                         .OrderBy(value => value.Score)
                         .ThenBy(value => value.Coordinates)
                         .Take(Math.Max(0, requiredCount - available.Count))
                         .ToArray())
            {
                fallback.Remove(promoted);
                promoted.IsPrimary = true;
                available.Add(promoted);
            }
            if (available.Count < requiredCount)
            {
                throw new InvalidOperationException(
                    $"난이도 {difficultyProfile?.Level.ToString() ?? "Legacy"} 정식 육각 성 건물 Slot " +
                    $"{available.Count}개로 필수 건물·길막 등급 {requiredCount}개를 배치할 수 없습니다.");
            }

            var sequence = 0;
            // 소환 출구·병영 간격을 먼저 확보한 뒤 보상 건물이 나머지 Cell을 사용한다.
            PlaceRepeated(
                HexCastleBuildingRole.KnightBarracks,
                knightBarracksCount,
                IsBarracksSlot,
                reserveBarracksExits: true);
            PlaceRepeated(
                HexCastleBuildingRole.FarmerBarracks,
                farmerBarracksCount,
                IsBarracksSlot,
                reserveBarracksExits: true);

            var innerBandTurretCount = Math.Min(
                turretCount,
                Math.Max(
                    0,
                    (int)Math.Round(
                        turretCount * tuning.InnerBandTurretShare,
                        MidpointRounding.AwayFromZero)));
            PlaceRepeated(
                HexCastleBuildingRole.Turret,
                innerBandTurretCount,
                slot => slot.BandIndex == 0,
                preferredBand: 0);
            PlaceRepeated(
                HexCastleBuildingRole.Turret,
                turretCount - innerBandTurretCount,
                slot => wallRadii.Count <= 2 || slot.BandIndex > 0);
            PlaceRepeated(HexCastleBuildingRole.GoldStorage, goldStorageCount);
            PlaceRepeated(HexCastleBuildingRole.EquipmentForge, equipmentForgeCount);
            PlaceRepeated(HexCastleBuildingRole.KeyVault, keyVaultCount);
            PlaceRepeated(HexCastleBuildingRole.TrainingYard, trainingYardCount);
            PlaceRepeated(HexCastleBuildingRole.Church, churchCount);

            var blockerGradeSum = 0;
            while (available.Count > 0)
            {
                var slot = TakeSlot(available, seed, sequence, wallRadii.Count - 1);
                var variants = tuning.BlockerVariants;
                var minimumCurrentGrade = Math.Max(
                    1,
                    minimumBlockerGradeSum - blockerGradeSum - available.Count * maximumBlockerGrade);
                var eligibleVariants = variants
                    .Where(value => value.Grade >= minimumCurrentGrade)
                    .ToArray();
                if (eligibleVariants.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"난이도 {difficultyProfile?.Level.ToString() ?? "Legacy"} 일반 길막 건물의 " +
                        $"남은 Slot {available.Count + 1}개로 최소 등급합 {minimumBlockerGradeSum}을 충족할 수 없습니다.");
                }

                var variant = eligibleVariants[PositiveModulo(
                    seed + slot.Score + sequence * 17,
                    eligibleVariants.Length)];
                PlaceBuildingCell(
                    cells,
                    slot,
                    HexCastleBuildingRole.Blocker,
                    wallRadii.Count,
                    tuning,
                    seed,
                    sequence,
                    variant,
                    difficultyProfile);
                blockerGradeSum += variant.Grade;
                sequence++;
            }

            EnsureFirstRowsAreMaximallyFilled(
                cells,
                plan,
                reservedGateApproaches,
                reservedBarracksExitCells);

            if (blockerGradeSum < minimumBlockerGradeSum)
            {
                throw new InvalidOperationException(
                    $"Theme 1 일반 건물 등급합 {blockerGradeSum}이 임시 최소값 {minimumBlockerGradeSum}보다 작습니다.");
            }

            bool IsBarracksSlot(BuildingSlot slot)
            {
                var separation = available.Any(candidate =>
                    CanHostBarracks(candidate, tuning.PreferredBarracksSeparationCells))
                    ? tuning.PreferredBarracksSeparationCells
                    : tuning.MinimumBarracksSeparationCells;
                return CanHostBarracks(slot, separation);
            }

            bool CanHostBarracks(BuildingSlot slot, int separation)
            {
                return slot.BandIndex + 1 >= minimumBarracksDefenseLayer &&
                       CountOpenNeighborCells(cells, slot.Coordinates) >=
                       tuning.MinimumBarracksOpenNeighbors &&
                       cells.Values
                           .Where(value => value.BuildingRole == HexCastleBuildingRole.KnightBarracks ||
                                           value.BuildingRole == HexCastleBuildingRole.FarmerBarracks)
                           .All(value => value.Coordinates.DistanceTo(slot.Coordinates) >= separation) &&
                       TryPlanBarracksExitCells(
                           cells,
                           available,
                           fallback,
                           reservedBarracksExitCells,
                           slot.Coordinates,
                           tuning.MinimumBarracksOpenNeighbors,
                           seed,
                           sequence + (int)HexCastleBuildingRole.KnightBarracks * 101,
                           out _);
            }

            string ResolveBarracksSlotDiagnostics()
            {
                var allRemainingSlots = available.Concat(fallback).ToArray();
                var outerBandSlots = allRemainingSlots
                    .Where(slot => slot.BandIndex + 1 >= minimumBarracksDefenseLayer)
                    .ToArray();
                var openNeighborSlots = outerBandSlots
                    .Where(slot => CountOpenNeighborCells(cells, slot.Coordinates) >=
                                   tuning.MinimumBarracksOpenNeighbors)
                    .ToArray();
                var separatedSlots = openNeighborSlots
                    .Where(slot => cells.Values
                        .Where(value => value.BuildingRole == HexCastleBuildingRole.KnightBarracks ||
                                        value.BuildingRole == HexCastleBuildingRole.FarmerBarracks)
                        .All(value => value.Coordinates.DistanceTo(slot.Coordinates) >=
                                      tuning.MinimumBarracksSeparationCells))
                    .ToArray();
                var exitReadySlots = separatedSlots.Count(slot => TryPlanBarracksExitCells(
                    cells,
                    available,
                    fallback,
                    reservedBarracksExitCells,
                    slot.Coordinates,
                    tuning.MinimumBarracksOpenNeighbors,
                    seed,
                    sequence + (int)HexCastleBuildingRole.KnightBarracks * 101,
                    out _));
                return $"주 슬롯 {available.Count}, 예비 슬롯 {fallback.Count}, 외곽 Band {outerBandSlots.Length}, " +
                       $"인접 빈칸 충족 {openNeighborSlots.Length}, 최소 간격 충족 {separatedSlots.Length}, " +
                       $"출구 예약 가능 {exitReadySlots}";
            }
            void PlaceRepeated(
                HexCastleBuildingRole role,
                int count,
                Func<BuildingSlot, bool> isAllowed = null,
                int? preferredBand = null,
                bool reserveBarracksExits = false)
            {
                for (var index = 0; index < count; index++)
                {
                    var salt = sequence + (int)role * 101;
                    if (isAllowed != null && !available.Any(isAllowed))
                    {
                        // 병영은 주 배치 열이 소진돼도 같은 방어선의 예비 열에서 출구 조건을 다시 찾는다.
                        var promoted = fallback
                            .Where(isAllowed)
                            .OrderBy(value => value.Score)
                            .ThenBy(value => value.Coordinates)
                            .FirstOrDefault();
                        if (promoted != null)
                        {
                            fallback.Remove(promoted);
                            promoted.IsPrimary = true;
                            available.Add(promoted);
                        }
                    }

                    if (available.Count == 0)
                    {
                        throw new InvalidOperationException(
                            $"난이도 {difficultyProfile?.Level.ToString() ?? "Legacy"} {role} 배치 전에 " +
                            "Theme 1 건물 Slot이 모두 소진됐습니다.");
                    }

                    if (isAllowed != null && !available.Any(isAllowed))
                    {
                        throw new InvalidOperationException(
                            $"난이도 {difficultyProfile?.Level.ToString() ?? "Legacy"} {role} 조건을 만족하는 건물 Slot이 없습니다. " +
                            ResolveBarracksSlotDiagnostics());
                    }

                    var slot = TakeSlot(
                        available,
                        seed,
                        salt,
                        wallRadii.Count - 1,
                        isAllowed,
                        preferredBand);
                    PlaceBuildingCell(
                        cells,
                        slot,
                        role,
                        wallRadii.Count,
                        tuning,
                        seed,
                        sequence,
                        null,
                        difficultyProfile);
                    if (reserveBarracksExits)
                    {
                        ReserveBarracksExitCells(
                            cells,
                            available,
                            fallback,
                            reservedBarracksExitCells,
                            slot.Coordinates,
                            tuning.MinimumBarracksOpenNeighbors,
                            seed,
                            salt);
                    }

                    sequence++;
                }
            }
        }

        private static int ApplyPalaceGuardBarracks(
            IDictionary<HexCoordinates, HexCastleCell> cells,
            int seed,
            int defenseLayerCount,
            HexCastleThemeOneTuning tuning,
            HexCastleDifficultyProfile difficultyProfile)
        {
            var preferredDirection = PositiveModulo(seed + 701, HexCoordinates.Directions.Length);
            for (var offset = 0; offset < HexCoordinates.Directions.Length; offset++)
            {
                var direction = PositiveModulo(preferredDirection + offset, HexCoordinates.Directions.Length);
                var coordinates = HexCoordinates.Directions[direction] * (PalaceFootprintRadius + 1);
                if (!cells.TryGetValue(coordinates, out var cell) ||
                    cell.Kind != HexCastleCellKind.Ground || cell.InitialBlocked ||
                    CountOpenNeighborCells(cells, coordinates) < tuning.MinimumBarracksOpenNeighbors)
                {
                    continue;
                }

                PlaceBuildingCell(
                    cells,
                    new BuildingSlot
                    {
                        Coordinates = coordinates,
                        BandIndex = -1,
                        RegionId = direction + 1,
                        Radius = PalaceFootprintRadius + 1,
                        Density = HexCastlePlacementDensity.Sparse,
                        Score = ResolvePlacementScore(seed, 0, direction + 1, coordinates),
                        IsPrimary = true
                    },
                    HexCastleBuildingRole.KnightBarracks,
                    defenseLayerCount,
                    tuning,
                    seed,
                    0,
                    null,
                    difficultyProfile);
                return direction;
            }

            throw new InvalidOperationException("왕궁 수비용 기사병영과 인접 소환 빈 셀을 확보할 수 없습니다.");
        }

        private static void ApplyPalaceGuardTurrets(
            IDictionary<HexCoordinates, HexCastleCell> cells,
            int seed,
            int defenseLayerCount,
            HexCastleThemeOneTuning tuning,
            int barracksDirection)
        {
            var offsets = new[] { 2, 4 };
            for (var index = 0; index < PalaceGuardTurretCount; index++)
            {
                var direction = PositiveModulo(
                    barracksDirection + offsets[index],
                    HexCoordinates.Directions.Length);
                var coordinates = HexCoordinates.Directions[direction] * (PalaceFootprintRadius + 1);
                if (!cells.TryGetValue(coordinates, out var cell) ||
                    cell.Kind != HexCastleCellKind.Ground || cell.InitialBlocked)
                {
                    throw new InvalidOperationException($"왕궁 경비 포탑 Cell {coordinates}을 확보할 수 없습니다.");
                }

                PlaceBuildingCell(
                    cells,
                    new BuildingSlot
                    {
                        Coordinates = coordinates,
                        BandIndex = -1,
                        RegionId = direction + 1,
                        Radius = PalaceFootprintRadius + 1,
                        Density = HexCastlePlacementDensity.Sparse,
                        Score = ResolvePlacementScore(seed, -1, direction + 1, coordinates),
                        IsPrimary = true
                    },
                    HexCastleBuildingRole.Turret,
                    defenseLayerCount,
                    tuning,
                    seed,
                    index + PalaceGuardBarracksCount,
                    null);
            }
        }

        private static IEnumerable<BuildingSlot> ResolveBuildingSlots(
            IDictionary<HexCoordinates, HexCastleCell> cells,
            int seed,
            HexCastleSilhouettePlan plan,
            HexCastleThemeOneTuning tuning,
            IReadOnlyCollection<HexCoordinates> reservedGateApproaches)
        {
            var boardRadius = cells.Keys.Max(value => value.DistanceFromOrigin);
            for (var bandIndex = 0; bandIndex < plan.Rings.Count - 1; bandIndex++)
            {
                var topology = HexCastleSilhouetteBandResolver.Resolve(plan, boardRadius, bandIndex);
                foreach (var density in new[]
                         {
                             HexCastlePlacementDensity.Dense,
                             HexCastlePlacementDensity.Sparse
                         })
                {
                    var rowCells = density == HexCastlePlacementDensity.Dense
                        ? topology.DenseRow
                        : topology.SparseRows;
                    if (rowCells.Count == 0) continue;
                    var rowSet = rowCells.ToHashSet();
                    var occupancy = density == HexCastlePlacementDensity.Dense
                        ? tuning.DenseOccupancy
                        : tuning.SparseOccupancy;
                    var ringCandidates = cells.Values
                        .Where(cell =>
                            cell.Kind == HexCastleCellKind.Ground &&
                            rowSet.Contains(cell.Coordinates))
                        .Select(cell => new BuildingSlot
                        {
                            Coordinates = cell.Coordinates,
                            BandIndex = bandIndex,
                            RegionId = ResolveRegion(cell.Coordinates),
                            Radius = cell.Coordinates.DistanceFromOrigin,
                            Density = density,
                            Score = ResolvePlacementScore(
                                seed,
                                bandIndex * 10 + (density == HexCastlePlacementDensity.Dense ? 1 : 2),
                                ResolveRegion(cell.Coordinates),
                                cell.Coordinates)
                        })
                        .OrderBy(slot => slot.Score)
                        .ThenBy(slot => slot.Coordinates)
                        .ToArray();
                    var availableCandidates = ringCandidates
                        .Where(slot => reservedGateApproaches == null ||
                                       !reservedGateApproaches.Contains(slot.Coordinates))
                        .ToArray();

                    if (density == HexCastlePlacementDensity.Dense)
                    {
                        foreach (var slot in availableCandidates)
                        {
                            slot.IsPrimary = true;
                            yield return slot;
                        }

                        continue;
                    }

                    for (var regionId = 1; regionId <= HexCoordinates.Directions.Length; regionId++)
                    {
                        var regionCandidates = availableCandidates
                            .Where(slot => slot.RegionId == regionId)
                            .ToArray();
                        if (regionCandidates.Length == 0)
                        {
                            continue;
                        }

                        var regionTarget = Math.Max(
                            1,
                            Math.Min(
                                regionCandidates.Length,
                                (int)Math.Round(
                                    regionCandidates.Length * occupancy,
                                    MidpointRounding.AwayFromZero)));
                        foreach (var slot in regionCandidates.Take(regionTarget))
                        {
                            slot.IsPrimary = true;
                        }
                    }

                    foreach (var slot in availableCandidates)
                    {
                        yield return slot;
                    }
                }
            }
        }

        private static BuildingSlot TakeSlot(
            ICollection<BuildingSlot> available,
            int seed,
            int salt,
            int bandCount,
            Func<BuildingSlot, bool> isAllowed = null,
            int? preferredBandOverride = null)
        {
            if (available.Count == 0)
            {
                throw new InvalidOperationException("Theme 1 건물 Slot이 모두 소진됐습니다.");
            }

            var candidates = available
                .Where(slot => isAllowed == null || isAllowed(slot))
                .ToArray();
            if (candidates.Length == 0)
            {
                throw new InvalidOperationException("Theme 1 조건을 만족하는 건물 Slot이 없습니다.");
            }

            var preferredRegion = PositiveModulo(seed + salt * 17, HexCoordinates.Directions.Length) + 1;
            var preferredBand = preferredBandOverride ??
                                PositiveModulo(seed / 7 + salt * 31, Math.Max(1, bandCount));
            var selected = candidates
                .OrderBy(slot => slot.BandIndex == preferredBand ? 0 : 1)
                .ThenBy(slot => slot.Density == HexCastlePlacementDensity.Dense ? 0 : 1)
                .ThenBy(slot => CircularRegionDistance(slot.RegionId, preferredRegion))
                .ThenBy(slot => ResolvePlacementScore(seed ^ salt, slot.BandIndex, slot.RegionId, slot.Coordinates))
                .ThenBy(slot => slot.Coordinates)
                .First();
            available.Remove(selected);
            return selected;
        }

        private static void EnsureFirstRowsAreMaximallyFilled(
            IDictionary<HexCoordinates, HexCastleCell> cells,
            HexCastleSilhouettePlan plan,
            IReadOnlyCollection<HexCoordinates> reservedGateApproaches,
            IReadOnlyCollection<HexCoordinates> reservedBarracksExitCells)
        {
            var requiredOpenCells = new HashSet<HexCoordinates>();
            if (reservedGateApproaches != null)
            {
                requiredOpenCells.UnionWith(reservedGateApproaches);
            }

            if (reservedBarracksExitCells != null)
            {
                requiredOpenCells.UnionWith(reservedBarracksExitCells);
            }

            var boardRadius = cells.Keys.Max(value => value.DistanceFromOrigin);
            for (var bandIndex = 0; bandIndex < plan.Rings.Count - 1; bandIndex++)
            {
                var denseRow = HexCastleSilhouetteBandResolver.Resolve(
                    plan,
                    boardRadius,
                    bandIndex).DenseRow.ToHashSet();
                var unexpectedOpenCells = cells.Values
                    .Where(cell =>
                        denseRow.Contains(cell.Coordinates) &&
                        cell.Kind == HexCastleCellKind.Ground &&
                        !requiredOpenCells.Contains(cell.Coordinates))
                    .Select(cell => cell.Coordinates)
                    .ToArray();
                if (unexpectedOpenCells.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"Theme 1 방어선 {bandIndex + 1} 바로 바깥 첫 열에 미배치 Cell이 남았습니다: " +
                        string.Join(", ", unexpectedOpenCells));
                }
            }
        }

        private static int CountOpenNeighborCells(
            IDictionary<HexCoordinates, HexCastleCell> cells,
            HexCoordinates coordinates)
        {
            return HexCoordinates.Directions.Count(direction =>
                cells.TryGetValue(coordinates + direction, out var neighbor) &&
                neighbor.Kind == HexCastleCellKind.Ground &&
                !neighbor.InitialBlocked);
        }

        private static void ReserveBarracksExitCells(
            IDictionary<HexCoordinates, HexCastleCell> cells,
            ICollection<BuildingSlot> available,
            ICollection<BuildingSlot> fallback,
            ISet<HexCoordinates> reserved,
            HexCoordinates barracksCoordinates,
            int requiredCount,
            int seed,
            int salt)
        {
            if (!TryPlanBarracksExitCells(
                    cells,
                    available,
                    fallback,
                    reserved,
                    barracksCoordinates,
                    requiredCount,
                    seed,
                    salt,
                    out var plan))
            {
                throw new InvalidOperationException(
                    $"병영 {barracksCoordinates}의 빈 셀 {requiredCount}개와 동일 건물 열 예비 Slot을 함께 확보할 수 없습니다.");
            }

            foreach (var reservation in plan)
            {
                reserved.Add(reservation.Exit);
                if (reservation.FallbackExitSlot != null)
                {
                    fallback.Remove(reservation.FallbackExitSlot);
                }

                if (reservation.OccupiedSlot != null)
                {
                    available.Remove(reservation.OccupiedSlot);
                    if (reservation.ReplacementSlot != null)
                    {
                        fallback.Remove(reservation.ReplacementSlot);
                        available.Add(reservation.ReplacementSlot);
                    }
                }
            }
        }

        private static bool TryPlanBarracksExitCells(
            IDictionary<HexCoordinates, HexCastleCell> cells,
            ICollection<BuildingSlot> available,
            ICollection<BuildingSlot> fallback,
            ISet<HexCoordinates> reserved,
            HexCoordinates barracksCoordinates,
            int requiredCount,
            int seed,
            int salt,
            out IReadOnlyList<BarracksExitReservation> plan)
        {
            var candidates = HexCoordinates.Directions
                .Select(direction => barracksCoordinates + direction)
                .Where(coordinates =>
                    cells.TryGetValue(coordinates, out var neighbor) &&
                    neighbor.Kind == HexCastleCellKind.Ground &&
                    !neighbor.InitialBlocked)
                .OrderBy(coordinates => reserved.Contains(coordinates) ? 1 : 0)
                .ThenBy(coordinates => available.Any(slot => slot.Coordinates == coordinates) ? 1 : 0)
                .ThenBy(coordinates => ResolvePlacementScore(seed, salt, 0, coordinates))
                .ThenBy(coordinates => coordinates)
                .ToArray();
            if (requiredCount <= 0)
            {
                plan = Array.Empty<BarracksExitReservation>();
                return true;
            }

            if (candidates.Length < requiredCount)
            {
                plan = Array.Empty<BarracksExitReservation>();
                return false;
            }

            var selected = new HexCoordinates[requiredCount];
            if (TrySelect(0, 0, out var result))
            {
                plan = result;
                return true;
            }

            plan = Array.Empty<BarracksExitReservation>();
            return false;

            bool TrySelect(
                int candidateIndex,
                int selectedCount,
                out IReadOnlyList<BarracksExitReservation> result)
            {
                if (selectedCount == requiredCount)
                {
                    return TryBuildPlan(selected, out result);
                }

                var remainingRequired = requiredCount - selectedCount;
                for (var index = candidateIndex;
                     index <= candidates.Length - remainingRequired;
                     index++)
                {
                    selected[selectedCount] = candidates[index];
                    if (TrySelect(index + 1, selectedCount + 1, out result))
                    {
                        return true;
                    }
                }

                result = Array.Empty<BarracksExitReservation>();
                return false;
            }

            bool TryBuildPlan(
                IReadOnlyList<HexCoordinates> exits,
                out IReadOnlyList<BarracksExitReservation> result)
            {
                var planned = new List<BarracksExitReservation>(exits.Count);
                var remainingFallback = fallback.ToList();
                var exitSet = new HashSet<HexCoordinates>(exits);
                foreach (var exit in exits)
                {
                    var fallbackExit = remainingFallback.FirstOrDefault(
                        slot => slot.Coordinates == exit);
                    if (fallbackExit != null)
                    {
                        remainingFallback.Remove(fallbackExit);
                    }

                    var occupied = available.FirstOrDefault(slot => slot.Coordinates == exit);
                    BuildingSlot replacement = null;
                    if (occupied != null)
                    {
                        replacement = remainingFallback
                            .Where(slot =>
                                slot.BandIndex == occupied.BandIndex &&
                                !reserved.Contains(slot.Coordinates) &&
                                !exitSet.Contains(slot.Coordinates) &&
                                cells.TryGetValue(slot.Coordinates, out var replacementCell) &&
                                replacementCell.Kind == HexCastleCellKind.Ground &&
                                !replacementCell.InitialBlocked)
                            .OrderBy(slot => slot.Score)
                            .ThenBy(slot => slot.Coordinates)
                            .FirstOrDefault();
                        if (replacement != null)
                        {
                            remainingFallback.Remove(replacement);
                        }
                    }

                    planned.Add(new BarracksExitReservation
                    {
                        Exit = exit,
                        FallbackExitSlot = fallbackExit,
                        OccupiedSlot = occupied,
                        ReplacementSlot = replacement
                    });
                }

                result = planned;
                return true;
            }
        }

        private static void PlaceBuildingCell(
            IDictionary<HexCoordinates, HexCastleCell> cells,
            BuildingSlot slot,
            HexCastleBuildingRole role,
            int defenseLayerCount,
            HexCastleThemeOneTuning tuning,
            int seed,
            int sequence,
            HexCastleBlockerVariantRule blockerVariant,
            HexCastleDifficultyProfile difficultyProfile = null)
        {
            var kind = role == HexCastleBuildingRole.Turret
                ? HexCastleCellKind.DefenseBuilding
                : role == HexCastleBuildingRole.GoldStorage ||
                  role == HexCastleBuildingRole.EquipmentForge ||
                  role == HexCastleBuildingRole.KeyVault
                    ? HexCastleCellKind.RewardBuilding
                    : HexCastleCellKind.Building;
            var lootKind = role == HexCastleBuildingRole.GoldStorage
                ? HexCastleLootKind.Gold
                : role == HexCastleBuildingRole.EquipmentForge
                    ? HexCastleLootKind.Equipment
                    : role == HexCastleBuildingRole.KeyVault
                        ? HexCastleLootKind.Key
                        : HexCastleLootKind.None;
            var turretWeapon = role == HexCastleBuildingRole.Turret
                ? tuning.ResolveTurretWeapon(seed, sequence)
                : HexCastleTurretWeaponKind.None;
            var hitPoints = kind == HexCastleCellKind.RewardBuilding
                ? tuning.RewardBuildingHealth
                : kind == HexCastleCellKind.DefenseBuilding
                    ? tuning.DefenseBuildingHealth
                    : blockerVariant != null
                        ? blockerVariant.Health
                        : tuning.SpecialBuildingHealth;
            var baseGrade = blockerVariant != null
                ? blockerVariant.Grade
                : role == HexCastleBuildingRole.Turret
                    ? tuning.ResolveTurretLevel(
                        defenseLayerCount,
                        slot.BandIndex,
                        turretWeapon)
                    : tuning.ResolveBuildingGrade(role);
            var grade = blockerVariant != null || difficultyProfile == null
                ? baseGrade
                : difficultyProfile.ResolveBuildingGrade(role, baseGrade);
            var turretRangeCells = role == HexCastleBuildingRole.Turret
                ? tuning.ResolveTurretRangeCells(turretWeapon, grade)
                : 0;
            var visualVariant = blockerVariant != null
                ? blockerVariant.VisualVariantId
                : ResolveRoleVisual(role);
            var densityCode = slot.Density == HexCastlePlacementDensity.Dense ? "D" : "S";
            cells[slot.Coordinates] = new HexCastleCell(
                slot.Coordinates,
                kind,
                defenseLayer: slot.BandIndex + 1,
                hitPoints: hitPoints,
                districtId: slot.RegionId,
                rewardValue: tuning.ResolveRewardValue(lootKind),
                regionId: slot.RegionId,
                initialBlocked: true,
                lootKind: lootKind,
                placementId:
                    $"T1_B{slot.BandIndex + 1:00}_{densityCode}_R{slot.RegionId:00}_{role}_{sequence:000}",
                visualVariantId: visualVariant,
                buildingRole: role,
                placementDensity: slot.Density,
                buildingGrade: grade,
                turretWeaponKind: turretWeapon,
                turretRangeCells: turretRangeCells,
                turretCanAttackAcrossWalls:
                    role == HexCastleBuildingRole.Turret && tuning.TurretsCanAttackAcrossWalls);
        }

        private static string ResolveRoleVisual(HexCastleBuildingRole role)
        {
            switch (role)
            {
                case HexCastleBuildingRole.KnightBarracks: return "building_barracks_blue";
                case HexCastleBuildingRole.FarmerBarracks: return "building_tent_blue";
                case HexCastleBuildingRole.TrainingYard: return "building_archeryrange_blue";
                case HexCastleBuildingRole.Church: return "building_church_blue";
                case HexCastleBuildingRole.GoldStorage: return "building_market_yellow";
                case HexCastleBuildingRole.EquipmentForge: return "building_blacksmith_green";
                case HexCastleBuildingRole.KeyVault: return "building_mine_red";
                case HexCastleBuildingRole.Turret:
                    // 빈 KayKit 받침 위에 기존 사각 버전의 무기 헤드를 별도로 조립한다.
                    return "building_tower_base_blue";
                default:
                    throw new ArgumentOutOfRangeException(nameof(role), role, "Theme 1 Visual 역할이 없습니다.");
            }
        }

        private static int CircularRegionDistance(int first, int second)
        {
            var distance = Math.Abs(first - second);
            return Math.Min(distance, HexCoordinates.Directions.Length - distance);
        }

        private static HexCastleWallRole ResolveWallRole(int layerIndex, int layerCount)
        {
            if (layerIndex == layerCount - 1)
            {
                return HexCastleWallRole.OuterPerimeter;
            }

            return layerIndex == 0
                ? HexCastleWallRole.CoreDefense
                : HexCastleWallRole.InnerDefense;
        }

        private static int ResolvePartitionLayer(int distance, IReadOnlyList<int> wallRadii)
        {
            for (var layerIndex = 0; layerIndex < wallRadii.Count - 1; layerIndex++)
            {
                if (distance > wallRadii[layerIndex] && distance < wallRadii[layerIndex + 1])
                {
                    return layerIndex + 1;
                }
            }

            throw new InvalidOperationException($"Partition 거리 {distance}의 방어층을 찾을 수 없습니다.");
        }

        private static int ResolveRegion(HexCoordinates coordinate)
        {
            var bestDirection = 0;
            var bestDistance = int.MaxValue;
            for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
            {
                var axis = HexCoordinates.Directions[direction] * coordinate.DistanceFromOrigin;
                var distance = coordinate.DistanceTo(axis);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestDirection = direction;
                }
            }

            return bestDirection + 1;
        }

        private static int ResolvePlacementScore(
            int seed,
            int bandIndex,
            int regionId,
            HexCoordinates coordinates)
        {
            unchecked
            {
                var value = seed;
                value = value * 397 ^ bandIndex;
                value = value * 397 ^ regionId;
                value = value * 397 ^ coordinates.Q;
                value = value * 397 ^ coordinates.R;
                return value & int.MaxValue;
            }
        }

        private static int PositiveModulo(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
