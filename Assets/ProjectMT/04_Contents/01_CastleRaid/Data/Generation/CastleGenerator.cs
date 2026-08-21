using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid.Generation
{
    public sealed class CastleGenerator // Seed와 배치 테마로 밀집 요새 골격을 만든다
    {
        private enum AttachDirection
        {
            North,
            East,
            South,
            West
        }

        private sealed class CompartmentDraft
        {
            public string Id;
            public CastleDistrictTemplate Template;
            public RectInt Bounds;
            public CastleCompartmentRole Role;
        }

        private sealed class WallDraft
        {
            public string TemplateId;
            public int WallTier;
            public CastleWallBand WallBand;
            public int DefenseLayer;
            public string LineId;
            public readonly HashSet<string> OwnerIds = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> SourceLineKeys = new HashSet<string>(StringComparer.Ordinal);
        }

        private sealed class WallLineDraft
        {
            public string Id;
            public readonly List<Vector2Int> Cells = new List<Vector2Int>();
        }

        private sealed class AttachmentSpec
        {
            public CompartmentDraft Parent;
            public AttachDirection Direction;
            public int Size;
        }

        public CastleGenerationCandidate Generate(CastleGenerationRules rules, int seed)
        {
            return Generate(
                rules,
                seed,
                CastleLayoutTheme.CentralCompartmentFortress,
                CastleGenerationRules.MinimumDefenseLayerCount);
        }

        public CastleGenerationCandidate Generate(
            CastleGenerationRules rules,
            int seed,
            CastleLayoutTheme theme)
        {
            return Generate(rules, seed, theme, CastleGenerationRules.MinimumDefenseLayerCount);
        }

        public CastleGenerationCandidate Generate(
            CastleGenerationRules rules,
            int seed,
            CastleLayoutTheme theme,
            int defenseLayerCount)
        {
            return CastleCentralFortressGenerator.Generate(rules, seed, theme, defenseLayerCount);
        }

#pragma warning disable CS0618 // v4 프로토타입 보존 구간
        private static List<CompartmentDraft> BuildCompartmentPlan(
            CastleGenerationRules rules,
            System.Random random,
            CastleLayoutTheme theme)
        {
            var drafts = new List<CompartmentDraft>();
            var core = new CompartmentDraft
            {
                Id = "palace_core",
                Template = rules.PalaceTemplate,
                Bounds = CastleSpatialContract.CenteredBounds(
                    rules.PalaceTemplate.Width,
                    rules.PalaceTemplate.Height),
                Role = CastleCompartmentRole.PalaceCore
            };
            drafts.Add(core);

            var named = AddCompleteInnerRing(rules, core, drafts);
            switch (theme)
            {
                case CastleLayoutTheme.CompactCompartments:
                    AddCompactOuterCompartments(rules, random, named, drafts);
                    break;
                case CastleLayoutTheme.SymmetricRadial:
                    AddFourWayOuterCompartments(rules, random, named, drafts);
                    break;
                case CastleLayoutTheme.CitadelDoubleRing:
                    break; // 3×3 중앙 성채에 외곽 링을 밀착시킨다
                default:
                    throw new ArgumentOutOfRangeException(nameof(theme), theme, "지원하지 않는 배치 테마입니다.");
            }

            return drafts;
        }

        private static Dictionary<string, CompartmentDraft> AddCompleteInnerRing(
            CastleGenerationRules rules,
            CompartmentDraft core,
            ICollection<CompartmentDraft> drafts)
        {
            var result = new Dictionary<string, CompartmentDraft>(StringComparer.Ordinal);
            var size = core.Bounds.width;
            var westX = core.Bounds.xMin - (size - 1);
            var eastX = core.Bounds.xMax - 1;
            var southZ = core.Bounds.yMin - (size - 1);
            var northZ = core.Bounds.yMax - 1;
            AddNamed("sw", westX, southZ);
            AddNamed("s", core.Bounds.xMin, southZ);
            AddNamed("se", eastX, southZ);
            AddNamed("w", westX, core.Bounds.yMin);
            AddNamed("e", eastX, core.Bounds.yMin);
            AddNamed("nw", westX, northZ);
            AddNamed("n", core.Bounds.xMin, northZ);
            AddNamed("ne", eastX, northZ);
            return result;

            void AddNamed(string name, int x, int z)
            {
                var draft = CreateRegularDraft(
                    rules,
                    $"inner_{name}",
                    new RectInt(x, z, size, size),
                    CastleCompartmentRole.InnerRing);
                EnsureCompatibleWithExisting(draft.Bounds, drafts);
                drafts.Add(draft);
                result.Add(name, draft);
            }
        }

        private static void AddCompactOuterCompartments(
            CastleGenerationRules rules,
            System.Random random,
            IReadOnlyDictionary<string, CompartmentDraft> inner,
            ICollection<CompartmentDraft> drafts)
        {
            var themeRule = rules.ResolveThemeRule(CastleLayoutTheme.CompactCompartments);
            var target = random.Next(themeRule.MinimumCompartmentCount, themeRule.MaximumCompartmentCount + 1);
            var needed = Mathf.Clamp(target - 8, 2, 8);
            var specs = new List<AttachmentSpec>
            {
                new AttachmentSpec { Parent = inner["n"], Direction = AttachDirection.North, Size = random.Next(5, 8) },
                new AttachmentSpec { Parent = inner["s"], Direction = AttachDirection.South, Size = random.Next(5, 8) },
                new AttachmentSpec { Parent = inner["e"], Direction = AttachDirection.East, Size = random.Next(5, 8) },
                new AttachmentSpec { Parent = inner["w"], Direction = AttachDirection.West, Size = random.Next(5, 8) },
                new AttachmentSpec { Parent = inner["nw"], Direction = AttachDirection.North, Size = 5 },
                new AttachmentSpec { Parent = inner["ne"], Direction = AttachDirection.North, Size = 5 },
                new AttachmentSpec { Parent = inner["sw"], Direction = AttachDirection.South, Size = 5 },
                new AttachmentSpec { Parent = inner["se"], Direction = AttachDirection.South, Size = 5 }
            };
            Shuffle(specs, random);

            var added = 0;
            foreach (var spec in specs)
            {
                if (added >= needed)
                {
                    break;
                }

                var bounds = Attach(spec.Parent.Bounds, spec.Direction, spec.Size);
                if (!CastleSpatialContract.Contains(rules.BuildableBounds, bounds) ||
                    !IsCompatibleWithExisting(bounds, drafts))
                {
                    continue;
                }

                drafts.Add(CreateRegularDraft(
                    rules,
                    $"outer_{added + 1:00}",
                    bounds,
                    CastleCompartmentRole.OuterRing));
                added++;
            }

            if (added < needed)
            {
                throw new InvalidOperationException($"밀집 격실형 외곽 구역을 {needed}개 중 {added}개만 배치했습니다.");
            }
        }

        private static void AddFourWayOuterCompartments(
            CastleGenerationRules rules,
            System.Random random,
            IReadOnlyDictionary<string, CompartmentDraft> inner,
            ICollection<CompartmentDraft> drafts)
        {
            var size = random.Next(5, 8);
            var bounds = Attach(inner["n"].Bounds, AttachDirection.North, size);
            for (var index = 0; index < 4; index++)
            {
                EnsureCompatibleWithExisting(bounds, drafts);
                drafts.Add(CreateRegularDraft(
                    rules,
                    $"outer_axis_{index + 1:00}",
                    bounds,
                    CastleCompartmentRole.OuterRing));
                bounds = Rotate90(bounds, rules.GridWidth);
            }
        }

        private static CompartmentDraft CreateRegularDraft(
            CastleGenerationRules rules,
            string id,
            RectInt bounds,
            CastleCompartmentRole role)
        {
            var template = rules.EnumerateRegularTemplates()
                .Where(value => value.WallLayers == 1 && value.Width == bounds.width && value.Height == bounds.height)
                .OrderByDescending(value => value.SelectionWeight)
                .FirstOrDefault();
            if (template == null)
            {
                throw new InvalidOperationException($"{bounds.width}×{bounds.height} 단일 성벽 격실 템플릿이 없습니다.");
            }

            return new CompartmentDraft
            {
                Id = id,
                Template = template,
                Bounds = bounds,
                Role = role
            };
        }

        private static RectInt Attach(RectInt parent, AttachDirection direction, int size)
        {
            var centeredX = parent.xMin + Mathf.FloorToInt((parent.width - size) * 0.5f);
            var centeredZ = parent.yMin + Mathf.FloorToInt((parent.height - size) * 0.5f);
            switch (direction)
            {
                case AttachDirection.North:
                    return new RectInt(centeredX, parent.yMax - 1, size, size);
                case AttachDirection.East:
                    return new RectInt(parent.xMax - 1, centeredZ, size, size);
                case AttachDirection.South:
                    return new RectInt(centeredX, parent.yMin - (size - 1), size, size);
                case AttachDirection.West:
                    return new RectInt(parent.xMin - (size - 1), centeredZ, size, size);
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        private static RectInt Rotate90(RectInt bounds, int gridSize)
        {
            return new RectInt(
                gridSize - bounds.yMax,
                bounds.xMin,
                bounds.height,
                bounds.width);
        }

        private static List<CastleCompartmentData> BuildCompartmentData(IReadOnlyList<CompartmentDraft> drafts)
        {
            var result = new List<CastleCompartmentData>(drafts.Count);
            foreach (var draft in drafts)
            {
                var connections = drafts
                    .Where(other => !ReferenceEquals(other, draft) && SharedEdgeLength(draft.Bounds, other.Bounds) >= 2)
                    .Select(other => other.Id)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();
                result.Add(new CastleCompartmentData(
                    draft.Id,
                    draft.Template.TemplateId,
                    draft.Role,
                    draft.Bounds,
                    draft.Template.WallLayers,
                    connections));
            }

            return result;
        }

        private static Dictionary<Vector2Int, WallDraft> BuildWallDrafts(
            CastleGenerationRules rules,
            IReadOnlyList<CompartmentDraft> drafts,
            CastleLayoutTheme theme)
        {
            var result = new Dictionary<Vector2Int, WallDraft>();
            foreach (var draft in drafts)
            {
                AddWallRings(
                    result,
                    draft.Bounds,
                    draft.Template.WallLayers,
                    draft.Id,
                    draft.Template.TemplateId);
            }

            if (theme == CastleLayoutTheme.CitadelDoubleRing)
            {
                var castleBounds = Encapsulate(drafts.Select(value => value.Bounds));
                var outerRing = new RectInt(
                    castleBounds.xMin - 2,
                    castleBounds.yMin - 2,
                    castleBounds.width + 4,
                    castleBounds.height + 4);
                if (!CastleSpatialContract.Contains(rules.BuildableBounds, outerRing))
                {
                    throw new InvalidOperationException("중앙 성채 외곽 링이 44×44 건설 영역을 벗어났습니다.");
                }

                AddWallRings(
                    result,
                    outerRing,
                    1,
                    "castle_envelope",
                    rules.CastleEnvelopeTemplate.TemplateId);
            }

            ClassifyWallLines(rules, drafts, result);
            return result;
        }

        private static void AddWallRings(
            IDictionary<Vector2Int, WallDraft> walls,
            RectInt bounds,
            int layers,
            string ownerId,
            string templateId)
        {
            for (var layer = 0; layer < layers; layer++)
            {
                var minX = bounds.xMin + layer;
                var maxX = bounds.xMax - 1 - layer;
                var minZ = bounds.yMin + layer;
                var maxZ = bounds.yMax - 1 - layer;
                for (var x = minX; x <= maxX; x++)
                {
                    AddWallCell(walls, new Vector2Int(x, minZ), layer, ownerId, templateId);
                    AddWallCell(walls, new Vector2Int(x, maxZ), layer, ownerId, templateId);
                }

                for (var z = minZ + 1; z < maxZ; z++)
                {
                    AddWallCell(walls, new Vector2Int(minX, z), layer, ownerId, templateId);
                    AddWallCell(walls, new Vector2Int(maxX, z), layer, ownerId, templateId);
                }
            }
        }

        private static void AddWallCell(
            IDictionary<Vector2Int, WallDraft> walls,
            Vector2Int cell,
            int sourceLayer,
            string ownerId,
            string templateId)
        {
            if (!walls.TryGetValue(cell, out var wall))
            {
                wall = new WallDraft
                {
                    TemplateId = templateId
                };
                walls.Add(cell, wall);
            }

            wall.OwnerIds.Add(ownerId);
            wall.SourceLineKeys.Add($"{ownerId}:{sourceLayer}");
        }

        private static void ClassifyWallLines(
            CastleGenerationRules rules,
            IReadOnlyList<CompartmentDraft> compartments,
            IReadOnlyDictionary<Vector2Int, WallDraft> walls)
        {
            var lines = BuildWallLines(walls);
            var remaining = new HashSet<WallLineDraft>(lines);
            var openedWallCells = new HashSet<Vector2Int>();
            var defenseLayer = 0;
            while (remaining.Count > 0)
            {
                var reachable = FloodReachableCells(
                    rules.GridWidth,
                    rules.GridHeight,
                    walls,
                    openedWallCells);
                var frontier = remaining
                    .Where(line => line.Cells.Any(cell => HasReachableNeighbor(cell, reachable)))
                    .OrderBy(line => line.Id, StringComparer.Ordinal)
                    .ToArray();
                if (frontier.Length == 0)
                {
                    throw new InvalidOperationException("완성 성벽망을 외곽에서 안쪽으로 분류하지 못했습니다.");
                }

                foreach (var line in frontier)
                {
                    foreach (var cell in line.Cells)
                    {
                        walls[cell].DefenseLayer = defenseLayer;
                        openedWallCells.Add(cell);
                    }

                    remaining.Remove(line);
                }

                defenseLayer++;
            }

            var maximumLayer = Mathf.Max(0, defenseLayer - 1);
            var roleByOwner = compartments.ToDictionary(value => value.Id, value => value.Role, StringComparer.Ordinal);
            foreach (var line in lines)
            {
                var first = walls[line.Cells[0]];
                var band = ResolveWallBand(first, roleByOwner);
                var normalizedDepth = maximumLayer == 0 ? 0f : first.DefenseLayer / (float)maximumLayer;
                var tier = Mathf.RoundToInt(Mathf.Lerp(
                    rules.MinimumWallTier,
                    rules.MaximumWallTier,
                    normalizedDepth));
                if (band == CastleWallBand.CoreDefense)
                {
                    tier = Mathf.Max(tier, rules.PalaceWallTier);
                }

                tier = Mathf.Clamp(tier, rules.MinimumWallTier, rules.MaximumWallTier);
                foreach (var cell in line.Cells)
                {
                    var wall = walls[cell];
                    wall.WallBand = band;
                    wall.WallTier = tier;
                }
            }
        }

        private static List<WallLineDraft> BuildWallLines(IReadOnlyDictionary<Vector2Int, WallDraft> walls)
        {
            var result = new List<WallLineDraft>();
            var unvisited = new HashSet<Vector2Int>(walls.Keys);
            while (unvisited.Count > 0)
            {
                var start = unvisited.OrderBy(value => value.y).ThenBy(value => value.x).First();
                var sourceSignature = ResolveSourceSignature(walls[start]);
                var line = new WallLineDraft { Id = $"wall_line_{result.Count:000}" };
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(start);
                unvisited.Remove(start);
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    line.Cells.Add(cell);
                    walls[cell].LineId = line.Id;
                    foreach (var neighbor in EnumerateNeighbors(cell))
                    {
                        if (unvisited.Contains(neighbor) &&
                            walls.TryGetValue(neighbor, out var neighborWall) &&
                            string.Equals(sourceSignature, ResolveSourceSignature(neighborWall), StringComparison.Ordinal))
                        {
                            unvisited.Remove(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                line.Cells.Sort(CompareCells);
                result.Add(line);
            }

            return result;
        }

        private static HashSet<Vector2Int> FloodReachableCells(
            int width,
            int height,
            IReadOnlyDictionary<Vector2Int, WallDraft> walls,
            ISet<Vector2Int> openedWallCells)
        {
            var result = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            for (var x = 0; x < width; x++)
            {
                TryEnqueue(new Vector2Int(x, 0));
                TryEnqueue(new Vector2Int(x, height - 1));
            }

            for (var z = 1; z < height - 1; z++)
            {
                TryEnqueue(new Vector2Int(0, z));
                TryEnqueue(new Vector2Int(width - 1, z));
            }

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var neighbor in EnumerateNeighbors(cell))
                {
                    if (neighbor.x >= 0 && neighbor.y >= 0 && neighbor.x < width && neighbor.y < height)
                    {
                        TryEnqueue(neighbor);
                    }
                }
            }

            return result;

            void TryEnqueue(Vector2Int cell)
            {
                if ((!walls.ContainsKey(cell) || openedWallCells.Contains(cell)) && result.Add(cell))
                {
                    queue.Enqueue(cell);
                }
            }
        }

        private static bool HasReachableNeighbor(Vector2Int cell, ISet<Vector2Int> reachable)
        {
            return EnumerateNeighbors(cell).Any(reachable.Contains);
        }

        private static CastleWallBand ResolveWallBand(
            WallDraft wall,
            IReadOnlyDictionary<string, CastleCompartmentRole> roleByOwner)
        {
            if (wall.DefenseLayer == 0)
            {
                return CastleWallBand.OuterPerimeter;
            }

            if (wall.OwnerIds.Contains("palace_core"))
            {
                return CastleWallBand.CoreDefense;
            }

            var ownerRoles = wall.OwnerIds
                .Where(roleByOwner.ContainsKey)
                .Select(owner => roleByOwner[owner])
                .Distinct()
                .ToArray();
            if (wall.OwnerIds.Count >= 2 && ownerRoles.Length == 1)
            {
                return CastleWallBand.Partition;
            }

            return CastleWallBand.InnerDefense;
        }

        private static string ResolveSourceSignature(WallDraft wall)
        {
            return string.Join("|", wall.SourceLineKeys.OrderBy(value => value, StringComparer.Ordinal));
        }

        private static IEnumerable<Vector2Int> EnumerateNeighbors(Vector2Int cell)
        {
            yield return cell + Vector2Int.up;
            yield return cell + Vector2Int.right;
            yield return cell + Vector2Int.down;
            yield return cell + Vector2Int.left;
        }

        private static int CompareCells(Vector2Int left, Vector2Int right)
        {
            var z = left.y.CompareTo(right.y);
            return z != 0 ? z : left.x.CompareTo(right.x);
        }

        private static void PlaceWalls(
            CastleGenerationRules rules,
            IReadOnlyDictionary<Vector2Int, WallDraft> walls,
            int[,] occupied,
            ICollection<CastlePlacementData> placements,
            ref int placementSerial)
        {
            foreach (var pair in walls.OrderBy(value => value.Key.y).ThenBy(value => value.Key.x))
            {
                var owners = pair.Value.OwnerIds.OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var placement = new CastlePlacementData(
                    NextId("wall", ref placementSerial),
                    owners[0],
                    pair.Value.TemplateId,
                    CastlePlacementKind.Wall,
                    CastleLootKind.None,
                    pair.Key.x,
                    pair.Key.y,
                    1,
                    1,
                    pair.Value.WallTier,
                    rules.ResolveWallHealth(pair.Value.WallTier),
                    0,
                    ResolveNeighborMask(walls, pair.Key),
                    owners,
                    pair.Value.WallBand,
                    pair.Value.DefenseLayer,
                    pair.Value.LineId);
                AddPlacement(placement, occupied, placements);
            }
        }

        private static CastleWallNeighborMask ResolveNeighborMask(
            IReadOnlyDictionary<Vector2Int, WallDraft> walls,
            Vector2Int cell)
        {
            var result = CastleWallNeighborMask.None;
            if (walls.ContainsKey(cell + Vector2Int.up))
            {
                result |= CastleWallNeighborMask.North;
            }

            if (walls.ContainsKey(cell + Vector2Int.right))
            {
                result |= CastleWallNeighborMask.East;
            }

            if (walls.ContainsKey(cell + Vector2Int.down))
            {
                result |= CastleWallNeighborMask.South;
            }

            if (walls.ContainsKey(cell + Vector2Int.left))
            {
                result |= CastleWallNeighborMask.West;
            }

            return result;
        }

        private static void PlacePalace(
            CastleGenerationRules rules,
            int[,] occupied,
            ICollection<CastlePlacementData> placements,
            ref int placementSerial)
        {
            var palaceBounds = CastleSpatialContract.PalaceBounds;
            AddPlacement(
                new CastlePlacementData(
                    NextId("palace", ref placementSerial),
                    "palace_core",
                    rules.PalaceTemplate.TemplateId,
                    CastlePlacementKind.Palace,
                    CastleLootKind.None,
                    palaceBounds.x,
                    palaceBounds.y,
                    palaceBounds.width,
                    palaceBounds.height,
                    0,
                    rules.PalaceHealth,
                    0),
                occupied,
                placements);
        }

        private static void PopulateCompartments(
            CastleGenerationRules rules,
            System.Random random,
            IEnumerable<CompartmentDraft> drafts,
            int[,] occupied,
            ICollection<CastlePlacementData> placements,
            ref int placementSerial)
        {
            var regular = drafts.Where(value => value.Role != CastleCompartmentRole.PalaceCore).ToList();
            var shuffled = regular.OrderBy(_ => random.Next()).ToArray();
            var lootPlan = BuildLootPlan(rules, random);
            var lootByDistrict = new Dictionary<string, CastleLootKind>(StringComparer.Ordinal);
            for (var index = 0; index < Mathf.Min(lootPlan.Count, shuffled.Length); index++)
            {
                lootByDistrict[shuffled[index].Id] = lootPlan[index];
            }

            foreach (var draft in regular)
            {
                var interior = Shrink(draft.Bounds, draft.Template.WallLayers);
                var maximumSize = Mathf.Min(CastleSpatialContract.MaximumBuildingSize, Mathf.Min(interior.width, interior.height));
                var hasLoot = lootByDistrict.TryGetValue(draft.Id, out var lootKind);
                var size = hasLoot
                    ? Mathf.Min(3, maximumSize)
                    : random.Next(Mathf.Min(2, maximumSize), maximumSize + 1);
                var x = interior.xMin + (interior.width - size) / 2;
                var z = interior.yMin + (interior.height - size) / 2;
                var kind = hasLoot
                    ? CastlePlacementKind.LootBuilding
                    : random.Next(100) < 62
                        ? CastlePlacementKind.Building
                        : CastlePlacementKind.DefenseBuilding;
                var health = kind == CastlePlacementKind.LootBuilding
                    ? rules.LootBuildingHealth
                    : kind == CastlePlacementKind.Building
                        ? rules.BuildingHealth
                        : rules.DefenseBuildingHealth;
                AddPlacement(
                    new CastlePlacementData(
                        NextId(kind == CastlePlacementKind.LootBuilding ? "loot" : kind == CastlePlacementKind.Building ? "building" : "defense", ref placementSerial),
                        draft.Id,
                        draft.Template.TemplateId,
                        kind,
                        hasLoot ? lootKind : CastleLootKind.None,
                        x,
                        z,
                        size,
                        size,
                        0,
                        health,
                        hasLoot ? rules.ResolveRewardBudgetCost(lootKind) : 0),
                    occupied,
                    placements);

                if (random.NextDouble() >= 0.42d)
                {
                    continue;
                }

                var defenderCells = CollectFreeCells(interior, occupied);
                if (defenderCells.Count == 0)
                {
                    continue;
                }

                var defenderCell = defenderCells[random.Next(defenderCells.Count)];
                AddPlacement(
                    new CastlePlacementData(
                        NextId("defender", ref placementSerial),
                        draft.Id,
                        draft.Template.TemplateId,
                        CastlePlacementKind.Defender,
                        CastleLootKind.None,
                        defenderCell.x,
                        defenderCell.y,
                        1,
                        1,
                        0,
                        rules.DefenderHealth,
                        0),
                    occupied,
                    placements);
            }
        }

        private static List<CastleLootKind> BuildLootPlan(CastleGenerationRules rules, System.Random random)
        {
            var candidates = new List<CastleLootKind>();
            AddRepeated(candidates, CastleLootKind.Gold, rules.MaximumGoldDistrictCount);
            AddRepeated(candidates, CastleLootKind.Equipment, rules.MaximumEquipmentDistrictCount);
            AddRepeated(candidates, CastleLootKind.Key, rules.MaximumKeyDistrictCount);
            Shuffle(candidates, random);

            var result = new List<CastleLootKind>();
            var budget = 0;
            foreach (var candidate in candidates)
            {
                if (result.Count >= rules.MaximumSpecialDistrictCount)
                {
                    break;
                }

                var cost = rules.ResolveRewardBudgetCost(candidate);
                if (budget + cost > rules.MaximumRewardBudget)
                {
                    continue;
                }

                result.Add(candidate);
                budget += cost;
            }

            return result;
        }

        private static int CountPalaceCoreExposedSides(IReadOnlyList<CompartmentDraft> drafts)
        {
            var core = drafts.Single(value => value.Role == CastleCompartmentRole.PalaceCore);
            var others = drafts.Where(value => value.Role != CastleCompartmentRole.PalaceCore).ToArray();
            var sides = new[]
            {
                Enumerable.Range(core.Bounds.xMin, core.Bounds.width).Select(x => new Vector2Int(x, core.Bounds.yMin)),
                Enumerable.Range(core.Bounds.xMin, core.Bounds.width).Select(x => new Vector2Int(x, core.Bounds.yMax - 1)),
                Enumerable.Range(core.Bounds.yMin, core.Bounds.height).Select(z => new Vector2Int(core.Bounds.xMin, z)),
                Enumerable.Range(core.Bounds.yMin, core.Bounds.height).Select(z => new Vector2Int(core.Bounds.xMax - 1, z))
            };
            return sides.Count(side => side.Any(cell => !others.Any(other => IsPerimeterCell(other.Bounds, cell))));
        }

        private static int CountMandatoryWallDepth(
            CastleGenerationCandidate candidate,
            CastleDifficultyReport difficulty)
        {
            var byId = candidate.Placements.ToDictionary(value => value.PlacementId, StringComparer.Ordinal);
            return difficulty.MandatoryPlacementIds.Count(id =>
                byId.TryGetValue(id, out var placement) && placement.Kind == CastlePlacementKind.Wall);
        }

        private static float CalculateCompactness(IEnumerable<CompartmentDraft> drafts)
        {
            var all = drafts.ToArray();
            var bounds = Encapsulate(all.Select(value => value.Bounds));
            var cells = new HashSet<Vector2Int>();
            foreach (var draft in all)
            {
                for (var x = draft.Bounds.xMin; x < draft.Bounds.xMax; x++)
                {
                    for (var z = draft.Bounds.yMin; z < draft.Bounds.yMax; z++)
                    {
                        cells.Add(new Vector2Int(x, z));
                    }
                }
            }

            return bounds.width * bounds.height > 0 ? cells.Count / (float)(bounds.width * bounds.height) : 0f;
        }

        private static bool IsCompatibleWithExisting(RectInt candidate, IEnumerable<CompartmentDraft> drafts)
        {
            return drafts.All(existing => IsWallOnlyIntersection(candidate, existing.Bounds));
        }

        private static void EnsureCompatibleWithExisting(RectInt candidate, IEnumerable<CompartmentDraft> drafts)
        {
            if (!IsCompatibleWithExisting(candidate, drafts))
            {
                throw new InvalidOperationException($"격실 내부가 겹칩니다: {candidate}");
            }
        }

        private static bool IsWallOnlyIntersection(RectInt left, RectInt right)
        {
            var minX = Mathf.Max(left.xMin, right.xMin);
            var maxX = Mathf.Min(left.xMax, right.xMax);
            var minZ = Mathf.Max(left.yMin, right.yMin);
            var maxZ = Mathf.Min(left.yMax, right.yMax);
            if (minX >= maxX || minZ >= maxZ)
            {
                return true;
            }

            var width = maxX - minX;
            var height = maxZ - minZ;
            if (width > 1 && height > 1)
            {
                return false;
            }

            for (var x = minX; x < maxX; x++)
            {
                for (var z = minZ; z < maxZ; z++)
                {
                    var cell = new Vector2Int(x, z);
                    if (!IsPerimeterCell(left, cell) || !IsPerimeterCell(right, cell))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static int SharedEdgeLength(RectInt left, RectInt right)
        {
            var minX = Mathf.Max(left.xMin, right.xMin);
            var maxX = Mathf.Min(left.xMax, right.xMax);
            var minZ = Mathf.Max(left.yMin, right.yMin);
            var maxZ = Mathf.Min(left.yMax, right.yMax);
            var width = Mathf.Max(0, maxX - minX);
            var height = Mathf.Max(0, maxZ - minZ);
            if (width == 1 && height >= 2)
            {
                return height;
            }

            return height == 1 && width >= 2 ? width : 0;
        }

        private static bool IsPerimeterCell(RectInt bounds, Vector2Int cell)
        {
            return bounds.Contains(cell) &&
                   (cell.x == bounds.xMin || cell.x == bounds.xMax - 1 ||
                    cell.y == bounds.yMin || cell.y == bounds.yMax - 1);
        }

        private static RectInt Encapsulate(IEnumerable<RectInt> bounds)
        {
            var values = bounds.ToArray();
            if (values.Length == 0)
            {
                return new RectInt();
            }

            var minX = values.Min(value => value.xMin);
            var minZ = values.Min(value => value.yMin);
            var maxX = values.Max(value => value.xMax);
            var maxZ = values.Max(value => value.yMax);
            return new RectInt(minX, minZ, maxX - minX, maxZ - minZ);
        }

        private static RectInt Shrink(RectInt bounds, int amount)
        {
            return new RectInt(
                bounds.xMin + amount,
                bounds.yMin + amount,
                bounds.width - amount * 2,
                bounds.height - amount * 2);
        }

        private static List<Vector2Int> CollectFreeCells(RectInt bounds, int[,] occupied)
        {
            var result = new List<Vector2Int>();
            for (var x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (var z = bounds.yMin; z < bounds.yMax; z++)
                {
                    if (occupied[x, z] < 0)
                    {
                        result.Add(new Vector2Int(x, z));
                    }
                }
            }

            return result;
        }

        private static void AddPlacement(
            CastlePlacementData placement,
            int[,] occupied,
            ICollection<CastlePlacementData> placements)
        {
            var placementIndex = placements.Count;
            for (var x = placement.X; x < placement.X + placement.Width; x++)
            {
                for (var z = placement.Z; z < placement.Z + placement.Height; z++)
                {
                    if (x < 0 || z < 0 || x >= occupied.GetLength(0) || z >= occupied.GetLength(1) || occupied[x, z] >= 0)
                    {
                        throw new InvalidOperationException($"생성 중 배치 충돌이 발생했습니다: {placement.PlacementId} ({x}, {z})");
                    }
                }
            }

            placements.Add(placement);
            for (var x = placement.X; x < placement.X + placement.Width; x++)
            {
                for (var z = placement.Z; z < placement.Z + placement.Height; z++)
                {
                    occupied[x, z] = placementIndex;
                }
            }
        }

        private static string ComputeLayoutHash(
            int rulesVersion,
            int width,
            int height,
            CastleLayoutTheme theme,
            IEnumerable<CastleCompartmentData> compartments,
            IEnumerable<CastlePlacementData> placements)
        {
            var builder = new StringBuilder();
            builder.Append(rulesVersion).Append('|').Append(width).Append('|').Append(height).Append('|').Append((int)theme);
            foreach (var compartment in compartments.OrderBy(value => value.CompartmentId, StringComparer.Ordinal))
            {
                builder.Append("|C:")
                    .Append(compartment.CompartmentId).Append(':')
                    .Append(compartment.TemplateId).Append(':')
                    .Append((int)compartment.Role).Append(':')
                    .Append(compartment.Bounds.x).Append(':')
                    .Append(compartment.Bounds.y).Append(':')
                    .Append(compartment.Bounds.width).Append(':')
                    .Append(compartment.Bounds.height).Append(':')
                    .Append(compartment.WallLayers);
            }

            foreach (var placement in placements.OrderBy(value => value.PlacementId, StringComparer.Ordinal))
            {
                builder.Append("|P:")
                    .Append(placement.PlacementId).Append(':')
                    .Append(placement.DistrictId).Append(':')
                    .Append(placement.TemplateId).Append(':')
                    .Append((int)placement.Kind).Append(':')
                    .Append((int)placement.LootKind).Append(':')
                    .Append(placement.X).Append(':')
                    .Append(placement.Z).Append(':')
                    .Append(placement.Width).Append(':')
                    .Append(placement.Height).Append(':')
                    .Append(placement.WallTier).Append(':')
                    .Append((int)placement.WallNeighborMask).Append(':')
                    .Append((int)placement.WallBand).Append(':')
                    .Append(placement.WallDefenseLayer).Append(':')
                    .Append(placement.WallLineId).Append(':')
                    .Append(placement.EffectiveHealth.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                    .Append(placement.RewardBudgetCost).Append(':')
                    .Append(string.Join(",", placement.OwnerDistrictIds.OrderBy(value => value, StringComparer.Ordinal)));
            }

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static string NextId(string prefix, ref int serial)
        {
            return $"{prefix}_{serial++:0000}";
        }

        private static void Fill(int[,] values, int value)
        {
            for (var x = 0; x < values.GetLength(0); x++)
            {
                for (var z = 0; z < values.GetLength(1); z++)
                {
                    values[x, z] = value;
                }
            }
        }

        private static void AddRepeated(List<CastleLootKind> values, CastleLootKind value, int count)
        {
            for (var index = 0; index < count; index++)
            {
                values.Add(value);
            }
        }

        private static void Shuffle<T>(IList<T> values, System.Random random)
        {
            for (var index = values.Count - 1; index > 0; index--)
            {
                var swapIndex = random.Next(index + 1);
                (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
            }
        }
#pragma warning restore CS0618
    }
}
