using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid.Generation
{
    public static class CastleGenerationValidator // 후보 저장 전 구조·예산 검수
    {
        public static CastleGenerationValidationReport Validate(
            CastleGenerationCandidate candidate,
            CastleGenerationRules rules,
            CastleDifficultyReport difficulty)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            var issues = new List<CastleGenerationValidationIssue>();
            if (!rules.TryValidate(out var configurationError))
            {
                Add(issues, "RULES_INVALID", configurationError);
                return new CastleGenerationValidationReport(issues);
            }

            if (candidate.GridWidth != rules.GridWidth || candidate.GridHeight != rules.GridHeight)
            {
                Add(issues, "GRID_SIZE_MISMATCH", "후보 그리드와 생성 규칙 그리드 크기가 다릅니다.");
            }

            var knownTemplateIds = new HashSet<string>(
                rules.Templates.Where(template => template != null).Select(template => template.TemplateId),
                StringComparer.Ordinal);
            var placementIds = new HashSet<string>(StringComparer.Ordinal);
            var wallCells = new Dictionary<Vector2Int, CastlePlacementData>();
            var cellOwners = new string[candidate.GridWidth, candidate.GridHeight];
            foreach (var placement in candidate.Placements)
            {
                if (string.IsNullOrWhiteSpace(placement.PlacementId) || !placementIds.Add(placement.PlacementId))
                {
                    Add(issues, "PLACEMENT_ID_DUPLICATE", "비어 있거나 중복된 PlacementId입니다.", placement.PlacementId);
                }

                if (!knownTemplateIds.Contains(placement.TemplateId))
                {
                    Add(issues, "TEMPLATE_UNKNOWN", "생성 규칙에 없는 TemplateId입니다.", placement.PlacementId);
                }

                if (placement.EffectiveHealth <= 0f)
                {
                    Add(issues, "HEALTH_INVALID", "파괴 대상 유효 체력은 0보다 커야 합니다.", placement.PlacementId);
                }

                if (!CastleSpatialContract.TryValidateFootprint(placement, out var footprintIssue))
                {
                    Add(issues, footprintIssue, "정식 점유 크기 계약을 위반했습니다.", placement.PlacementId);
                }

                if (!CastleSpatialContract.Contains(rules.BuildableBounds, placement.Bounds))
                {
                    Add(
                        issues,
                        "OFF_GRID_PLACEMENT",
                        "배치가 44×44 건설 영역을 벗어났습니다.",
                        placement.PlacementId,
                        new Vector2Int(placement.X, placement.Z));
                }

                if (placement.Kind == CastlePlacementKind.Wall &&
                    (placement.WallTier < 1 || placement.WallTier > rules.MaximumWallTier))
                {
                    Add(issues, "WALL_TIER_INVALID", "생성 규칙 범위를 벗어난 성벽 등급입니다.", placement.PlacementId);
                }

                if (placement.Kind == CastlePlacementKind.Wall)
                {
                    var wallCell = new Vector2Int(placement.X, placement.Z);
                    if (!wallCells.TryAdd(wallCell, placement))
                    {
                        Add(issues, "DUPLICATE_SHARED_WALL", "같은 좌표의 공유 성벽이 둘 이상 생성됐습니다.", placement.PlacementId, wallCell);
                    }
                }

                for (var x = placement.X; x < placement.X + placement.Width; x++)
                {
                    for (var z = placement.Z; z < placement.Z + placement.Height; z++)
                    {
                        if (x < 0 || z < 0 || x >= candidate.GridWidth || z >= candidate.GridHeight)
                        {
                            Add(issues, "OFF_GRID_PLACEMENT", "배치가 전장 경계를 벗어났습니다.", placement.PlacementId, new Vector2Int(x, z));
                            continue;
                        }

                        if (!string.IsNullOrEmpty(cellOwners[x, z]))
                        {
                            Add(issues, "CELL_OVERLAP", $"{cellOwners[x, z]}와 셀이 겹칩니다.", placement.PlacementId, new Vector2Int(x, z));
                        }
                        else
                        {
                            cellOwners[x, z] = placement.PlacementId;
                        }
                    }
                }
            }

            ValidatePalace(candidate, issues);
            ValidateCompartments(candidate, rules, issues);
            ValidateWallGraph(candidate, wallCells, issues);
            ValidateDistricts(candidate, rules, issues);
            ValidateLoot(candidate, rules, issues);
            if (difficulty == null || !difficulty.HasClearPath)
            {
                Add(issues, "PALACE_UNREACHABLE", "합법 외곽 시작점에서 왕궁까지의 파괴 경로가 없습니다.");
            }

            return new CastleGenerationValidationReport(issues);
        }

        private static void ValidatePalace(
            CastleGenerationCandidate candidate,
            List<CastleGenerationValidationIssue> issues)
        {
            var palaces = candidate.Placements.Where(placement => placement.Kind == CastlePlacementKind.Palace).ToArray();
            if (palaces.Length != 1)
            {
                Add(issues, "PALACE_COUNT", "왕궁은 정확히 1개여야 합니다.");
                return;
            }

            var palace = palaces[0];
            if (!palace.Bounds.Equals(CastleSpatialContract.PalaceBounds))
            {
                Add(issues, "INVALID_PALACE_PLACEMENT", "왕궁은 전장 중앙의 4×4 점유여야 합니다.", palace.PlacementId);
            }
        }

        private static void ValidateDistricts(
            CastleGenerationCandidate candidate,
            CastleGenerationRules rules,
            List<CastleGenerationValidationIssue> issues)
        {
            var districtCount = candidate.Compartments.Count(value => value.Role != CastleCompartmentRole.PalaceCore);
            var themeRule = rules.ResolveThemeRule(candidate.Theme);
            if (candidate.RequestedDefenseLayerCount < CastleGenerationRules.MinimumDefenseLayerCount ||
                candidate.RequestedDefenseLayerCount > CastleGenerationRules.MaximumDefenseLayerCount)
            {
                Add(
                    issues,
                    "LAYOUT_THEME_CONTRACT_FAILED",
                    $"요청 성벽 겹 수 {candidate.RequestedDefenseLayerCount}가 지원 범위 2~4를 벗어났습니다.");
                return;
            }

            CastleGenerationRules.ResolveCompartmentCountRange(
                candidate.Theme,
                candidate.RequestedDefenseLayerCount,
                out var minimumCount,
                out var maximumCount);
            if (districtCount < minimumCount || districtCount > maximumCount)
            {
                Add(
                    issues,
                    "LAYOUT_THEME_CONTRACT_FAILED",
                    $"{candidate.RequestedDefenseLayerCount}중벽 일반 격실 수 {districtCount}개가 허용 범위 {minimumCount}~{maximumCount}를 벗어났습니다.");
            }

            if (candidate.ProtectionDepth != candidate.RequestedDefenseLayerCount ||
                candidate.ProtectionDepth < themeRule.MinimumProtectionDepth)
            {
                Add(
                    issues,
                    "LAYOUT_THEME_CONTRACT_FAILED",
                    $"{candidate.RequestedDefenseLayerCount}중벽 후보의 실제 보호 깊이가 {candidate.ProtectionDepth}입니다.");
            }
        }

        private static void ValidateCompartments(
            CastleGenerationCandidate candidate,
            CastleGenerationRules rules,
            List<CastleGenerationValidationIssue> issues)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var templatesById = rules.Templates.ToDictionary(value => value.TemplateId, StringComparer.Ordinal);
            var cores = candidate.Compartments.Where(value => value.Role == CastleCompartmentRole.PalaceCore).ToArray();
            if (cores.Length != 1)
            {
                Add(issues, "PALACE_CORE_COUNT", "왕궁 코어 격실은 정확히 1개여야 합니다.");
                return;
            }

            foreach (var compartment in candidate.Compartments)
            {
                if (string.IsNullOrWhiteSpace(compartment.CompartmentId) || !ids.Add(compartment.CompartmentId))
                {
                    Add(issues, "COMPARTMENT_ID_DUPLICATE", "비어 있거나 중복된 격실 ID입니다.", compartment.CompartmentId);
                }

                if (!CastleSpatialContract.Contains(rules.BuildableBounds, compartment.Bounds))
                {
                    Add(issues, "OFF_GRID_COMPARTMENT", "격실이 44×44 건설 영역을 벗어났습니다.", compartment.CompartmentId);
                }

                if (compartment.HasCustomFootprint)
                {
                    if (compartment.FootprintCells.Any(cell =>
                            !compartment.Bounds.Contains(cell) ||
                            !rules.BuildableBounds.Contains(cell)))
                    {
                        Add(
                            issues,
                            "INVALID_COMPARTMENT_FOOTPRINT",
                            "비정형 격실 셀이 Bounds 또는 44×44 건설 영역을 벗어났습니다.",
                            compartment.CompartmentId);
                    }

                    if (!IsConnectedFootprint(compartment))
                    {
                        Add(
                            issues,
                            "INVALID_COMPARTMENT_FOOTPRINT",
                            "비정형 격실 footprint가 하나의 연결 영역이 아닙니다.",
                            compartment.CompartmentId);
                    }
                }

                if (!templatesById.TryGetValue(compartment.TemplateId, out var template) ||
                    !template.SupportsSize(compartment.Bounds.width, compartment.Bounds.height))
                {
                    Add(
                        issues,
                        "INVALID_COMPARTMENT_SIZE",
                        "격실 크기가 선택한 가변 템플릿 범위를 벗어났습니다.",
                        compartment.CompartmentId);
                }

                var validRing = compartment.Role == CastleCompartmentRole.PalaceCore
                    ? compartment.DefenseRing == 0
                    : compartment.DefenseRing >= 1 &&
                      compartment.DefenseRing < candidate.RequestedDefenseLayerCount;
                if (!validRing)
                {
                    Add(
                        issues,
                        "INVALID_DEFENSE_RING",
                        $"격실 방어 링 {compartment.DefenseRing}이 요청 {candidate.RequestedDefenseLayerCount}중벽 범위와 맞지 않습니다.",
                        compartment.CompartmentId);
                }
            }

            var actualRings = candidate.Compartments
                .Select(value => value.DefenseRing)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            var expectedRings = Enumerable.Range(0, candidate.RequestedDefenseLayerCount).ToArray();
            if (!actualRings.SequenceEqual(expectedRings))
            {
                Add(
                    issues,
                    "DEFENSE_RING_SEQUENCE_INVALID",
                    $"격실 방어 링이 0~{candidate.RequestedDefenseLayerCount - 1} 연속이어야 합니다.");
            }

            var adjacency = candidate.Compartments.ToDictionary(
                value => value.CompartmentId,
                _ => new HashSet<string>(StringComparer.Ordinal),
                StringComparer.Ordinal);
            for (var leftIndex = 0; leftIndex < candidate.Compartments.Count; leftIndex++)
            {
                var left = candidate.Compartments[leftIndex];
                for (var rightIndex = leftIndex + 1; rightIndex < candidate.Compartments.Count; rightIndex++)
                {
                    var right = candidate.Compartments[rightIndex];
                    if (SharedBoundaryCellCount(left, right) < 2)
                    {
                        continue;
                    }

                    adjacency[left.CompartmentId].Add(right.CompartmentId);
                    adjacency[right.CompartmentId].Add(left.CompartmentId);
                }
            }

            foreach (var compartment in candidate.Compartments)
            {
                if (compartment.Role != CastleCompartmentRole.PalaceCore && adjacency[compartment.CompartmentId].Count == 0)
                {
                    Add(issues, "COMPARTMENT_NOT_EDGE_CONNECTED", "격실이 다른 격실과 공유 성벽으로 연결되지 않았습니다.", compartment.CompartmentId);
                }

                if (!adjacency[compartment.CompartmentId].SetEquals(compartment.ConnectedCompartmentIds))
                {
                    Add(issues, "COMPARTMENT_GRAPH_MISMATCH", "저장된 격실 연결과 실제 공유 변이 다릅니다.", compartment.CompartmentId);
                }
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            queue.Enqueue(cores[0].CompartmentId);
            visited.Add(cores[0].CompartmentId);
            while (queue.Count > 0)
            {
                foreach (var neighbor in adjacency[queue.Dequeue()])
                {
                    if (visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            if (visited.Count != candidate.Compartments.Count)
            {
                Add(issues, "DISCONNECTED_CASTLE_GRAPH", "모든 격실이 왕궁 코어 기준 단일 연결 그래프에 포함돼야 합니다.");
            }

            var exposedSides = CountExposedCoreSides(cores[0], candidate.Compartments);
            if (exposedSides != candidate.PalaceExposedSideCount || exposedSides > 0)
            {
                Add(issues, "PALACE_EXPOSED", $"왕궁 코어의 직접 노출면이 {exposedSides}개입니다.");
            }

            var themeRule = rules.ResolveThemeRule(candidate.Theme);
            var wallCells = candidate.Placements
                .Where(placement => placement.Kind == CastlePlacementKind.Wall)
                .Select(placement => new Vector2Int(placement.X, placement.Z));
            if (!HasRequiredSymmetry(wallCells, candidate.GridWidth, themeRule.Symmetry))
            {
                Add(issues, "LAYOUT_THEME_CONTRACT_FAILED", $"{candidate.Theme}의 {themeRule.Symmetry} 대칭 계약을 충족하지 않습니다.");
            }
        }

        private static void ValidateWallGraph(
            CastleGenerationCandidate candidate,
            IReadOnlyDictionary<Vector2Int, CastlePlacementData> walls,
            List<CastleGenerationValidationIssue> issues)
        {
            foreach (var pair in walls)
            {
                var expectedMask = CastleWallNeighborMask.None;
                if (walls.ContainsKey(pair.Key + Vector2Int.up)) expectedMask |= CastleWallNeighborMask.North;
                if (walls.ContainsKey(pair.Key + Vector2Int.right)) expectedMask |= CastleWallNeighborMask.East;
                if (walls.ContainsKey(pair.Key + Vector2Int.down)) expectedMask |= CastleWallNeighborMask.South;
                if (walls.ContainsKey(pair.Key + Vector2Int.left)) expectedMask |= CastleWallNeighborMask.West;
                if (pair.Value.WallNeighborMask != expectedMask)
                {
                    Add(issues, "INVALID_WALL_NEIGHBOR_MASK", "성벽 이웃 마스크가 실제 상하좌우 연결과 다릅니다.", pair.Value.PlacementId, pair.Key);
                }

                var expectedOwners = candidate.Compartments
                    .Where(value => value.IsFootprintBoundaryCell(pair.Key))
                    .Select(value => value.CompartmentId)
                    .ToHashSet(StringComparer.Ordinal);
                if (expectedOwners.Count > 1 && !expectedOwners.IsSubsetOf(pair.Value.OwnerDistrictIds))
                {
                    Add(issues, "DUPLICATE_SHARED_WALL", "공유 성벽 하나가 맞닿은 모든 격실 소유권을 보존하지 못했습니다.", pair.Value.PlacementId, pair.Key);
                }

                ValidateWallBand(pair.Value, pair.Key, issues);
            }

            foreach (var line in walls.Values
                         .Where(value => !string.IsNullOrWhiteSpace(value.WallLineId))
                         .GroupBy(value => value.WallLineId, StringComparer.Ordinal))
            {
                var first = line.First();
                if (line.Any(value =>
                        value.WallTier != first.WallTier ||
                        value.WallBand != first.WallBand ||
                        value.WallDefenseLayer != first.WallDefenseLayer))
                {
                    Add(
                        issues,
                        "WALL_LINE_TIER_MISMATCH",
                        "같은 성벽 라인의 등급·방어선 역할·깊이는 하나로 통일돼야 합니다.",
                        first.WallLineId);
                }

                if (!IsConnectedWallLine(line))
                {
                    Add(
                        issues,
                        "WALL_LINE_DISCONNECTED",
                        "하나의 성벽 라인 ID에 서로 떨어진 성벽 조각이 포함됐습니다.",
                        first.WallLineId);
                }
            }

            var layers = walls.Values.Select(value => value.WallDefenseLayer).Distinct().OrderBy(value => value).ToArray();
            if (layers.Length == 0 || layers[0] != 0 || layers.Where((value, index) => value != index).Any())
            {
                Add(issues, "WALL_LAYER_SEQUENCE_INVALID", "성벽 방어 깊이는 외곽 0부터 끊김 없이 증가해야 합니다.");
            }
        }

        private static void ValidateWallBand(
            CastlePlacementData wall,
            Vector2Int cell,
            ICollection<CastleGenerationValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(wall.WallLineId) || wall.WallBand == CastleWallBand.None)
            {
                Add(issues, "INVALID_WALL_BAND", "성벽 라인 ID와 방어선 역할이 지정되지 않았습니다.", wall.PlacementId, cell);
                return;
            }

            if ((wall.WallDefenseLayer == 0) != (wall.WallBand == CastleWallBand.OuterPerimeter))
            {
                Add(issues, "INVALID_WALL_BAND", "최외곽 성벽은 깊이 0이어야 하며 깊이 0은 최외곽 성벽으로 분류돼야 합니다.", wall.PlacementId, cell);
            }

            if (wall.WallBand == CastleWallBand.CoreDefense && !wall.OwnerDistrictIds.Contains("palace_core"))
            {
                Add(issues, "INVALID_WALL_BAND", "왕궁 방어선이 왕궁 코어 소유권을 포함하지 않습니다.", wall.PlacementId, cell);
            }

            if (wall.WallBand == CastleWallBand.Partition && wall.OwnerDistrictIds.Count < 2)
            {
                Add(issues, "INVALID_WALL_BAND", "격벽은 둘 이상의 격실을 나누는 공유 성벽이어야 합니다.", wall.PlacementId, cell);
            }
        }

        private static bool IsConnectedWallLine(IEnumerable<CastlePlacementData> line)
        {
            var cells = line.Select(value => new Vector2Int(value.X, value.Z)).ToHashSet();
            if (cells.Count == 0)
            {
                return false;
            }

            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            var first = cells.First();
            visited.Add(first);
            queue.Enqueue(first);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var neighbor in new[]
                         {
                             cell + Vector2Int.up,
                             cell + Vector2Int.right,
                             cell + Vector2Int.down,
                             cell + Vector2Int.left
                         })
                {
                    if (cells.Contains(neighbor) && visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return visited.Count == cells.Count;
        }

        private static void ValidateLoot(
            CastleGenerationCandidate candidate,
            CastleGenerationRules rules,
            List<CastleGenerationValidationIssue> issues)
        {
            var loot = candidate.Placements.Where(placement => placement.Kind == CastlePlacementKind.LootBuilding).ToArray();
            if (loot.Length > rules.MaximumSpecialDistrictCount)
            {
                Add(issues, "SPECIAL_LIMIT", "전체 특수 보상 구역 상한을 초과했습니다.");
            }

            ValidateLootKind(loot, CastleLootKind.Gold, rules.MaximumGoldDistrictCount, issues);
            ValidateLootKind(loot, CastleLootKind.Equipment, rules.MaximumEquipmentDistrictCount, issues);
            ValidateLootKind(loot, CastleLootKind.Key, rules.MaximumKeyDistrictCount, issues);
            var budget = loot.Sum(placement => placement.RewardBudgetCost);
            if (budget > rules.MaximumRewardBudget)
            {
                Add(issues, "REWARD_BUDGET", $"보상 예산 {budget}이 상한 {rules.MaximumRewardBudget}을 초과했습니다.");
            }
        }

        private static void ValidateLootKind(
            IEnumerable<CastlePlacementData> loot,
            CastleLootKind kind,
            int maximum,
            List<CastleGenerationValidationIssue> issues)
        {
            var count = loot.Count(placement => placement.LootKind == kind);
            if (count > maximum)
            {
                Add(issues, "LOOT_KIND_LIMIT", $"{kind} 보상 구역 수 {count}개가 상한 {maximum}을 초과했습니다.");
            }
        }

        private static int SharedBoundaryCellCount(
            CastleCompartmentData left,
            CastleCompartmentData right)
        {
            return left.EnumerateFootprintCells()
                .Count(cell => left.IsFootprintBoundaryCell(cell) && right.IsFootprintBoundaryCell(cell));
        }

        private static int CountExposedCoreSides(
            CastleCompartmentData core,
            IReadOnlyList<CastleCompartmentData> compartments)
        {
            var others = compartments.Where(value => !ReferenceEquals(value, core)).ToArray();
            return core.EnumerateFootprintCells()
                .Count(cell => core.IsFootprintBoundaryCell(cell) &&
                               !others.Any(other =>
                                   other.IsFootprintBoundaryCell(cell) ||
                                   other.IsFootprintBoundaryCell(cell + Vector2Int.up) ||
                                   other.IsFootprintBoundaryCell(cell + Vector2Int.right) ||
                                   other.IsFootprintBoundaryCell(cell + Vector2Int.down) ||
                                   other.IsFootprintBoundaryCell(cell + Vector2Int.left)));
        }

        private static bool IsConnectedFootprint(CastleCompartmentData compartment)
        {
            var cells = compartment.FootprintCells.ToHashSet();
            if (cells.Count == 0)
            {
                return false;
            }

            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            var first = cells.First();
            visited.Add(first);
            queue.Enqueue(first);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var neighbor in new[]
                         {
                             cell + Vector2Int.up,
                             cell + Vector2Int.right,
                             cell + Vector2Int.down,
                             cell + Vector2Int.left
                         })
                {
                    if (cells.Contains(neighbor) && visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return visited.Count == cells.Count;
        }

        private static bool HasRequiredSymmetry(
            IEnumerable<Vector2Int> sourceCells,
            int gridSize,
            CastleLayoutSymmetry symmetry)
        {
            if (symmetry == CastleLayoutSymmetry.None)
            {
                return true;
            }

            var cells = sourceCells.ToHashSet();
            foreach (var cell in cells)
            {
                var rotated = Rotate90(cell, gridSize);
                if (symmetry == CastleLayoutSymmetry.HalfTurn)
                {
                    rotated = Rotate90(rotated, gridSize);
                    if (!cells.Contains(rotated))
                    {
                        return false;
                    }

                    continue;
                }

                for (var rotation = 0; rotation < 3; rotation++)
                {
                    if (!cells.Contains(rotated))
                    {
                        return false;
                    }

                    rotated = Rotate90(rotated, gridSize);
                }
            }

            return true;
        }

        private static Vector2Int Rotate90(Vector2Int cell, int gridSize)
        {
            return new Vector2Int(gridSize - 1 - cell.y, cell.x);
        }


        private static void Add(
            ICollection<CastleGenerationValidationIssue> issues,
            string code,
            string message,
            string placementId = "",
            Vector2Int cell = default)
        {
            issues.Add(new CastleGenerationValidationIssue(code, message, placementId, cell));
        }
    }
}
