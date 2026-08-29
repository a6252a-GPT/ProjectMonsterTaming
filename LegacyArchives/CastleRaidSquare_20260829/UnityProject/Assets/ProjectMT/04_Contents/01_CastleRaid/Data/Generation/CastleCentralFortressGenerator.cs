using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid.Generation
{
    internal static class CastleCentralFortressGenerator // 중앙 요새 윤곽 뒤에 가변 격실을 채운다
    {
        private sealed class CompartmentDraft
        {
            public string Id;
            public CastleDistrictTemplate Template;
            public RectInt Bounds;
            public CastleCompartmentRole Role;
            public int DefenseRing;
            public HashSet<Vector2Int> FootprintCells;
        }

        private sealed class WallDraft
        {
            public string TemplateId;
            public int WallTier;
            public CastleWallBand WallBand;
            public int DefenseLayer;
            public string LineId;
            public readonly HashSet<string> OwnerIds = new HashSet<string>(StringComparer.Ordinal);
        }

        private sealed class NestedRingThicknessProfile
        {
            public int[] North;
            public int[] East;
            public int[] South;
            public int[] West;
        }

        private sealed class DiamondShellProfile
        {
            public CastleStructureVariant Variant;
            public int[] NorthSteps;
            public int[] EastSteps;
            public int[] SouthSteps;
            public int[] WestSteps;
        }

        private sealed class HoneycombShellProfile
        {
            public CastleStructureVariant Variant;
            public int[] NorthSteps;
            public int[] EastSteps;
            public int[] SouthSteps;
            public int[] WestSteps;
        }

        private sealed class PetalBloomProfile
        {
            public CastleStructureVariant Variant;
            public double BasePhase;
            public double PhaseStep;
            public double LobeSharpness;
            public double ValleyOffset;
            public double LengthOffset;
            public double AlternatingLength;
        }

        private enum RingSide
        {
            North,
            East,
            South,
            West
        }

        public static CastleGenerationCandidate Generate(
            CastleGenerationRules rules,
            int seed,
            CastleLayoutTheme theme,
            int defenseLayerCount)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (!CastleGenerationRules.SupportedLayoutThemes.Contains(theme))
            {
                throw new ArgumentOutOfRangeException(nameof(theme), theme, "폐기됐거나 아직 정식 지원하지 않는 배치 테마입니다.");
            }

            CastleGenerationRules.ResolveCompartmentCountRange(
                defenseLayerCount,
                out _,
                out _);

            if (!rules.TryValidate(out var configurationError))
            {
                throw new InvalidOperationException(configurationError);
            }

            var random = new System.Random(seed);
            var drafts = BuildCompartmentPlan(
                rules,
                random,
                theme,
                defenseLayerCount,
                out var structureVariant);
            var compartments = BuildCompartmentData(drafts);
            var walls = BuildWallDrafts(rules, drafts, random);
            var occupied = new int[rules.GridWidth, rules.GridHeight];
            Fill(occupied, -1);
            var placements = new List<CastlePlacementData>();
            var placementSerial = 0;

            PlaceWalls(rules, walls, occupied, placements, ref placementSerial);
            PlacePalace(rules, occupied, placements, ref placementSerial);
            PopulatePalaceCore(rules, random, drafts[0], occupied, placements, ref placementSerial);
            PopulateCompartments(rules, random, drafts, occupied, placements, ref placementSerial);

            var structureHash = ComputeStructureHash(
                rules.RulesVersion,
                rules.GridWidth,
                rules.GridHeight,
                theme,
                structureVariant,
                defenseLayerCount,
                compartments,
                walls);
            var layoutHash = ComputeLayoutHash(
                rules.RulesVersion,
                rules.GridWidth,
                rules.GridHeight,
                theme,
                structureVariant,
                defenseLayerCount,
                compartments,
                placements);
            var candidate = new CastleGenerationCandidate(
                seed,
                rules.RulesVersion,
                rules.GridWidth,
                rules.GridHeight,
                theme,
                structureVariant,
                structureHash,
                layoutHash,
                compartments,
                placements,
                defenseLayerCount);
            var difficulty = CastleDifficultyEvaluator.Evaluate(candidate);
            candidate.SetStructuralMetrics(
                CountPalaceCoreExposedSides(drafts),
                CountMandatoryWallDepth(candidate, difficulty),
                CalculateCompactness(drafts));
            var validation = CastleGenerationValidator.Validate(candidate, rules, difficulty);
            candidate.SetReports(validation, difficulty);
            return candidate;
        }

        private static List<CompartmentDraft> BuildCompartmentPlan(
            CastleGenerationRules rules,
            System.Random random,
            CastleLayoutTheme theme,
            int defenseLayerCount,
            out CastleStructureVariant structureVariant)
        {
            if (theme == CastleLayoutTheme.HexHoneycombFortress)
            {
                return BuildHexHoneycombFortress(
                    rules,
                    random,
                    defenseLayerCount,
                    out structureVariant);
            }

            var palaceTemplate = rules.PalaceTemplate;
            var core = new CompartmentDraft
            {
                Id = "palace_core",
                Template = palaceTemplate,
                Bounds = CastleSpatialContract.CenteredBounds(
                    palaceTemplate.MinimumWidth,
                    palaceTemplate.MinimumHeight),
                Role = CastleCompartmentRole.PalaceCore,
                DefenseRing = 0
            };
            var drafts = new List<CompartmentDraft> { core };
            switch (theme)
            {
                case CastleLayoutTheme.CentralCompartmentFortress:
                    structureVariant = CastleStructureVariant.CentralAdaptive;
                    if (defenseLayerCount == (int)CastleDefenseLayerPreset.Double)
                    {
                        BuildCentralCompartmentFortress(rules, random, core, drafts);
                    }
                    else
                    {
                        BuildNestedCompartmentFortress(rules, random, core, defenseLayerCount, drafts);
                    }
                    break;
                case CastleLayoutTheme.DiamondRadialFortress:
                    structureVariant = BuildDiamondRadialFortress(
                        rules,
                        random,
                        core,
                        defenseLayerCount,
                        drafts);
                    break;
                case CastleLayoutTheme.HoneycombCompartmentFortress:
                    structureVariant = BuildHoneycombCompartmentFortress(
                        rules,
                        random,
                        core,
                        defenseLayerCount,
                        drafts);
                    break;
                case CastleLayoutTheme.PetalBloomFortress:
                    structureVariant = BuildPetalBloomFortress(
                        rules,
                        random,
                        core,
                        defenseLayerCount,
                        drafts);
                    break;
                case CastleLayoutTheme.CrystalMandalaFortress:
                case CastleLayoutTheme.TwinSpiralFortress:
                case CastleLayoutTheme.FractalBastionFortress:
                case CastleLayoutTheme.VoronoiCrystalFortress:
                case CastleLayoutTheme.IrisShutterFortress:
                    structureVariant = BuildGeometricFortress(
                        rules,
                        random,
                        theme,
                        core,
                        defenseLayerCount,
                        drafts);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(theme), theme, "지원하지 않는 정식 배치 테마입니다.");
            }

            return drafts;
        }

        private static CastleStructureVariant BuildPetalBloomFortress(
            CastleGenerationRules rules,
            System.Random random,
            CompartmentDraft core,
            int defenseLayerCount,
            ICollection<CompartmentDraft> drafts)
        {
            const int petalCount = 8;
            var profile = BuildPetalBloomProfile(random);
            var patternSeed = random.Next();
            core.FootprintCells = BuildFlowerCoreFootprint(core.Bounds);
            var previousDisk = EnumerateFootprintCells(core).ToHashSet();
            for (var defenseRing = 1; defenseRing < defenseLayerCount; defenseRing++)
            {
                var ringPhase = profile.BasePhase + profile.PhaseStep * (defenseRing - 1);
                var outerDisk = BuildPetalBloomDisk(
                    rules,
                    previousDisk,
                    defenseRing,
                    petalCount,
                    ringPhase,
                    profile,
                    patternSeed);
                var previousInterior = previousDisk
                    .Where(cell => EnumerateNeighbors(cell).All(previousDisk.Contains))
                    .ToHashSet();
                var ringCells = outerDisk.Where(cell => !previousInterior.Contains(cell)).ToHashSet();
                var footprints = SplitPetalRingIntoCompartments(
                    ringCells,
                    petalCount,
                    ringPhase);
                var role = defenseRing == 1
                    ? CastleCompartmentRole.InnerRing
                    : CastleCompartmentRole.OuterRing;

                for (var petalIndex = 0; petalIndex < footprints.Length; petalIndex++)
                {
                    AddPetalFootprintDraft(
                        rules,
                        drafts,
                        $"petal_{defenseRing:00}_{petalIndex:00}",
                        footprints[petalIndex],
                        role,
                        defenseRing);
                }

                previousDisk = outerDisk;
            }

            CastleGenerationRules.ResolveCompartmentCountRange(
                CastleLayoutTheme.PetalBloomFortress,
                defenseLayerCount,
                out var minimumCount,
                out var maximumCount);
            var regularCount = drafts.Count - 1;
            if (regularCount < minimumCount || regularCount > maximumCount)
            {
                throw new InvalidOperationException(
                    $"{defenseLayerCount}중벽 꽃잎 군락 요새의 격실 수가 맞지 않습니다: {regularCount}/{minimumCount}~{maximumCount}");
            }

            return profile.Variant;
        }

        private static HashSet<Vector2Int> BuildFlowerCoreFootprint(RectInt bounds)
        {
            var centerX = bounds.xMin + bounds.width * 0.5d;
            var centerZ = bounds.yMin + bounds.height * 0.5d;
            var radius = Math.Min(bounds.width, bounds.height) * 0.5d - 0.15d;
            return EnumerateRectCells(bounds)
                .Where(cell =>
                {
                    var deltaX = cell.x + 0.5d - centerX;
                    var deltaZ = cell.y + 0.5d - centerZ;
                    return deltaX * deltaX + deltaZ * deltaZ <= radius * radius;
                })
                .ToHashSet();
        }

        private static PetalBloomProfile BuildPetalBloomProfile(System.Random random)
        {
            var profileIndex = random.Next(8);
            var phaseJitter = (random.Next(5) - 2) * Math.PI / 180d;
            var profile = new PetalBloomProfile
            {
                Variant = (CastleStructureVariant)((int)CastleStructureVariant.PetalBloomBalanced + profileIndex),
                BasePhase = phaseJitter,
                PhaseStep = 0d,
                LobeSharpness = 1.05d,
                ValleyOffset = 0d,
                LengthOffset = 0d,
                AlternatingLength = 0d
            };

            switch (profileIndex)
            {
                case 1:
                    profile.BasePhase += Math.PI / 8d;
                    break;
                case 2:
                    profile.AlternatingLength = 0.9d;
                    break;
                case 3:
                    profile.BasePhase += Math.PI / 8d;
                    profile.AlternatingLength = 0.9d;
                    break;
                case 4:
                    profile.PhaseStep = Math.PI / 32d;
                    break;
                case 5:
                    profile.PhaseStep = -Math.PI / 32d;
                    break;
                case 6:
                    profile.ValleyOffset = 0.45d;
                    profile.LengthOffset = -0.35d;
                    profile.LobeSharpness = 1.4d;
                    break;
                case 7:
                    profile.ValleyOffset = -0.2d;
                    profile.LengthOffset = 0.35d;
                    profile.LobeSharpness = 0.72d;
                    break;
            }

            return profile;
        }

        private static HashSet<Vector2Int> BuildPetalBloomDisk(
            CastleGenerationRules rules,
            IReadOnlyCollection<Vector2Int> previousDisk,
            int defenseRing,
            int petalCount,
            double phase,
            PetalBloomProfile profile,
            int patternSeed)
        {
            var valleyRadii = new[] { 0d, 8.4d, 12.7d, 16.7d };
            var lobeLengths = new[] { 0d, 5.0d, 5.1d, 4.7d };
            var result = new HashSet<Vector2Int>(previousDisk);
            var centerX = rules.GridWidth * 0.5d;
            var centerZ = rules.GridHeight * 0.5d;
            foreach (var cell in EnumerateRectCells(rules.BuildableBounds))
            {
                var deltaX = cell.x + 0.5d - centerX;
                var deltaZ = cell.y + 0.5d - centerZ;
                var distance = Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
                var angle = Math.Atan2(deltaZ, deltaX);
                var sector = ResolvePetalSector(angle, petalCount, phase);
                var localAngle = NormalizeSignedAngle(angle - phase - sector * Math.PI * 2d / petalCount);
                var lobe = Math.Pow(
                    Math.Max(0d, 0.5d + 0.5d * Math.Cos(localAngle * petalCount)),
                    profile.LobeSharpness);
                var alternating = sector % 2 == 0
                    ? profile.AlternatingLength
                    : -profile.AlternatingLength;
                var organicOffset = ResolvePetalLengthOffset(patternSeed, defenseRing, sector);
                var maximumRadius = valleyRadii[defenseRing] + profile.ValleyOffset +
                                    (lobeLengths[defenseRing] + profile.LengthOffset + alternating + organicOffset) * lobe;
                if (distance <= maximumRadius)
                {
                    result.Add(cell);
                }
            }

            StabilizePetalDisk(result, previousDisk);
            return result;
        }

        private static void StabilizePetalDisk(
            HashSet<Vector2Int> disk,
            IReadOnlyCollection<Vector2Int> protectedInnerDisk)
        {
            var protectedCells = protectedInnerDisk as HashSet<Vector2Int> ??
                                 new HashSet<Vector2Int>(protectedInnerDisk);
            while (true)
            {
                var interior = disk.Where(cell => EnumerateNeighbors(cell).All(disk.Contains)).ToHashSet();
                var fragile = disk
                    .Where(cell =>
                        !protectedCells.Contains(cell) &&
                        !interior.Contains(cell) &&
                        !EnumerateNeighbors(cell).Any(interior.Contains))
                    .ToArray();
                if (fragile.Length == 0)
                {
                    return;
                }

                disk.ExceptWith(fragile);
            }
        }

        private static double ResolvePetalLengthOffset(int patternSeed, int defenseRing, int petalIndex)
        {
            unchecked
            {
                var oppositePair = petalIndex % 4;
                var hash = patternSeed;
                hash = hash * 397 ^ defenseRing;
                hash = hash * 397 ^ oppositePair;
                return ((hash & int.MaxValue) % 5 - 2) * 0.16d;
            }
        }

        private static HashSet<Vector2Int>[] SplitPetalRingIntoCompartments(
            HashSet<Vector2Int> ringCells,
            int petalCount,
            double phase)
        {
            var result = Enumerable.Range(0, petalCount)
                .Select(_ => new HashSet<Vector2Int>())
                .ToArray();
            var owners = new Dictionary<Vector2Int, int>();
            var sharedCells = new List<(Vector2Int Cell, int Owner, int Recipient)>();
            foreach (var cell in ringCells)
            {
                var angle = Math.Atan2(cell.y + 0.5d - 25d, cell.x + 0.5d - 25d);
                var owner = ResolvePetalSector(angle, petalCount, phase);
                owners[cell] = owner;
                result[owner].Add(cell);
            }

            foreach (var cell in ringCells.OrderBy(value => value.y).ThenBy(value => value.x))
            {
                foreach (var neighbor in new[] { cell + Vector2Int.right, cell + Vector2Int.up })
                {
                    if (!owners.TryGetValue(neighbor, out var neighborOwner))
                    {
                        continue;
                    }

                    var owner = owners[cell];
                    if (owner == neighborOwner)
                    {
                        continue;
                    }

                    if (owner < neighborOwner)
                    {
                        result[neighborOwner].Add(cell);
                        sharedCells.Add((cell, owner, neighborOwner));
                    }
                    else
                    {
                        result[owner].Add(neighbor);
                        sharedCells.Add((neighbor, neighborOwner, owner));
                    }
                }
            }

            // 대각 그리드 꿈임에서 공유 셀이 안쪽으로 잠기는 꼭지만 제거한다.
            foreach (var shared in sharedCells)
            {
                if (!IsFootprintBoundaryCell(result[shared.Owner], shared.Cell) ||
                    !IsFootprintBoundaryCell(result[shared.Recipient], shared.Cell))
                {
                    result[shared.Recipient].Remove(shared.Cell);
                }
            }

            MergeDisconnectedPetalFragments(result);
            return result;
        }

        private static void MergeDisconnectedPetalFragments(HashSet<Vector2Int>[] footprints)
        {
            for (var pass = 0; pass < footprints.Length * 2; pass++)
            {
                var changed = false;
                for (var sourceIndex = 0; sourceIndex < footprints.Length; sourceIndex++)
                {
                    var components = FindCardinalComponents(footprints[sourceIndex])
                        .OrderByDescending(value => value.Count)
                        .ToArray();
                    foreach (var fragment in components.Skip(1))
                    {
                        var targetIndex = Enumerable.Range(0, footprints.Length)
                            .Where(index => index != sourceIndex)
                            .Select(index => new
                            {
                                Index = index,
                                Score = fragment.Sum(cell =>
                                    (footprints[index].Contains(cell) ? 8 : 0) +
                                    EnumerateNeighbors(cell).Count(footprints[index].Contains))
                            })
                            .OrderByDescending(value => value.Score)
                            .ThenBy(value => value.Index)
                            .First();
                        if (targetIndex.Score == 0)
                        {
                            throw new InvalidOperationException("떨어진 꽃잎 파편을 인접 꽃잎에 연결할 수 없습니다.");
                        }

                        footprints[sourceIndex].ExceptWith(fragment);
                        footprints[targetIndex.Index].UnionWith(fragment);
                        changed = true;
                    }
                }

                if (!changed || footprints.All(IsCardinallyConnected))
                {
                    return;
                }
            }

            throw new InvalidOperationException("꽃잎 격실의 연결 파편을 정리하지 못했습니다.");
        }

        private static IEnumerable<HashSet<Vector2Int>> FindCardinalComponents(
            IReadOnlyCollection<Vector2Int> footprint)
        {
            var remaining = new HashSet<Vector2Int>(footprint);
            while (remaining.Count > 0)
            {
                var component = new HashSet<Vector2Int>();
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(remaining.First());
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (!remaining.Remove(current))
                    {
                        continue;
                    }

                    component.Add(current);
                    foreach (var neighbor in EnumerateNeighbors(current).Where(remaining.Contains))
                    {
                        queue.Enqueue(neighbor);
                    }
                }

                yield return component;
            }
        }

        private static int ResolvePetalSector(double angle, int petalCount, double phase)
        {
            var sectorSize = Math.PI * 2d / petalCount;
            var normalized = angle - phase;
            while (normalized < 0d)
            {
                normalized += Math.PI * 2d;
            }

            while (normalized >= Math.PI * 2d)
            {
                normalized -= Math.PI * 2d;
            }

            return Mod((int)Math.Floor(normalized / sectorSize + 0.5d), petalCount);
        }

        private static double NormalizeSignedAngle(double angle)
        {
            while (angle <= -Math.PI)
            {
                angle += Math.PI * 2d;
            }

            while (angle > Math.PI)
            {
                angle -= Math.PI * 2d;
            }

            return angle;
        }

        private static CompartmentDraft AddPetalFootprintDraft(
            CastleGenerationRules rules,
            ICollection<CompartmentDraft> drafts,
            string id,
            HashSet<Vector2Int> footprintCells,
            CastleCompartmentRole role,
            int defenseRing)
        {
            var bounds = EncapsulateCells(footprintCells);
            var template = rules.PetalTemplate;
            var supportsSize = template != null && template.SupportsSize(bounds.width, bounds.height);
            var insideBuildArea = footprintCells.All(rules.BuildableBounds.Contains);
            var connected = IsCardinallyConnected(footprintCells);
            var compatible = IsFootprintCompatibleWithExisting(footprintCells, drafts);
            if (!supportsSize || !insideBuildArea || !connected || !compatible)
            {
                var conflict = DescribeFootprintConflict(footprintCells, drafts);
                throw new InvalidOperationException(
                    $"꽃잎 격실을 배치할 수 없습니다: {id} {bounds} " +
                    $"size={supportsSize}, area={insideBuildArea}, connected={connected}, compatible={compatible}, conflict={conflict}");
            }

            var result = new CompartmentDraft
            {
                Id = id,
                Template = template,
                Bounds = bounds,
                Role = role,
                DefenseRing = defenseRing,
                FootprintCells = footprintCells
            };
            drafts.Add(result);
            return result;
        }

        private static CastleStructureVariant BuildGeometricFortress(
            CastleGenerationRules rules,
            System.Random random,
            CastleLayoutTheme theme,
            CompartmentDraft core,
            int defenseLayerCount,
            ICollection<CompartmentDraft> drafts)
        {
            const int compartmentCount = 8;
            var profileIndex = random.Next(8);
            var patternSeed = random.Next();
            core.FootprintCells = BuildGeometricCoreFootprint(theme, core.Bounds);
            var previousDisk = EnumerateFootprintCells(core).ToHashSet();
            for (var defenseRing = 1; defenseRing < defenseLayerCount; defenseRing++)
            {
                var outerDisk = BuildGeometricDisk(
                    rules,
                    previousDisk,
                    theme,
                    defenseRing,
                    profileIndex,
                    patternSeed);
                var previousInterior = previousDisk
                    .Where(cell => EnumerateNeighbors(cell).All(previousDisk.Contains))
                    .ToHashSet();
                var ringCells = outerDisk.Where(cell => !previousInterior.Contains(cell)).ToHashSet();
                var footprints = SplitGeometricRingIntoCompartments(
                    rules,
                    ringCells,
                    previousDisk,
                    theme,
                    defenseRing,
                    profileIndex,
                    patternSeed,
                    compartmentCount);
                var role = defenseRing == 1
                    ? CastleCompartmentRole.InnerRing
                    : CastleCompartmentRole.OuterRing;
                var prefix = ResolveGeometricPrefix(theme);

                for (var compartmentIndex = 0; compartmentIndex < footprints.Length; compartmentIndex++)
                {
                    AddGeometricFootprintDraft(
                        rules,
                        drafts,
                        $"{prefix}_{defenseRing:00}_{compartmentIndex:00}",
                        footprints[compartmentIndex],
                        role,
                        defenseRing,
                        theme);
                }

                previousDisk = outerDisk;
            }

            CastleGenerationRules.ResolveCompartmentCountRange(
                theme,
                defenseLayerCount,
                out var minimumCount,
                out var maximumCount);
            var regularCount = drafts.Count - 1;
            if (regularCount < minimumCount || regularCount > maximumCount)
            {
                throw new InvalidOperationException(
                    $"{defenseLayerCount}중벽 {theme}의 격실 수가 맞지 않습니다: {regularCount}/{minimumCount}~{maximumCount}");
            }

            return ResolveGeometricVariant(theme, profileIndex);
        }

        private static HashSet<Vector2Int> BuildGeometricCoreFootprint(
            CastleLayoutTheme theme,
            RectInt bounds)
        {
            var centerX = bounds.xMin + bounds.width * 0.5d;
            var centerZ = bounds.yMin + bounds.height * 0.5d;
            return EnumerateRectCells(bounds)
                .Where(cell =>
                {
                    var deltaX = cell.x + 0.5d - centerX;
                    var deltaZ = cell.y + 0.5d - centerZ;
                    var absoluteX = Math.Abs(deltaX);
                    var absoluteZ = Math.Abs(deltaZ);
                    var maximum = Math.Max(absoluteX, absoluteZ);
                    var minimum = Math.Min(absoluteX, absoluteZ);
                    switch (theme)
                    {
                        case CastleLayoutTheme.CrystalMandalaFortress:
                        case CastleLayoutTheme.VoronoiCrystalFortress:
                            return maximum + minimum * 0.42d <= 5.85d;
                        case CastleLayoutTheme.FractalBastionFortress:
                            return maximum <= 5.5d &&
                                   (minimum <= 3.5d || maximum <= 4.5d);
                        case CastleLayoutTheme.TwinSpiralFortress:
                        case CastleLayoutTheme.IrisShutterFortress:
                            return true;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(theme), theme, null);
                    }
                })
                .ToHashSet();
        }

        private static HashSet<Vector2Int> BuildGeometricDisk(
            CastleGenerationRules rules,
            IReadOnlyCollection<Vector2Int> previousDisk,
            CastleLayoutTheme theme,
            int defenseRing,
            int profileIndex,
            int patternSeed)
        {
            var result = new HashSet<Vector2Int>(previousDisk);
            if (theme == CastleLayoutTheme.TwinSpiralFortress)
            {
                foreach (var cell in previousDisk)
                {
                    foreach (var neighbor in EnumerateNeighbors(cell))
                    {
                        if (rules.BuildableBounds.Contains(neighbor))
                        {
                            result.Add(neighbor); // 비틀린 골에서도 방어층 한 칸은 보존한다
                        }
                    }
                }
            }

            var centerX = rules.GridWidth * 0.5d;
            var centerZ = rules.GridHeight * 0.5d;
            foreach (var cell in EnumerateRectCells(rules.BuildableBounds))
            {
                var deltaX = cell.x + 0.5d - centerX;
                var deltaZ = cell.y + 0.5d - centerZ;
                if (IsInsideGeometricDisk(
                        theme,
                        deltaX,
                        deltaZ,
                        defenseRing,
                        profileIndex,
                        patternSeed))
                {
                    result.Add(cell);
                }
            }

            StabilizePetalDisk(result, previousDisk);
            return result;
        }

        private static bool IsInsideGeometricDisk(
            CastleLayoutTheme theme,
            double deltaX,
            double deltaZ,
            int defenseRing,
            int profileIndex,
            int patternSeed)
        {
            var minimumRadii = new[] { 0d, 10.55d, 14.65d, 18.65d };
            var maximumRadii = new[] { 0d, 13.45d, 17.45d, 21.35d };
            var distance = Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
            var angle = Math.Atan2(deltaZ, deltaX);
            var phase = ResolveGeometricBasePhase(profileIndex) +
                        ResolveGeometricRingPhaseStep(profileIndex) * (defenseRing - 1);
            if (theme == CastleLayoutTheme.FractalBastionFortress && profileIndex == 6)
            {
                phase = -Math.PI / 16d;
            }
            var radiusOffset = profileIndex == 6 ? -0.3d : profileIndex == 7 ? 0.2d : 0d;
            var minimumRadius = minimumRadii[defenseRing] + radiusOffset;
            var maximumRadius = maximumRadii[defenseRing] + radiusOffset;

            switch (theme)
            {
                case CastleLayoutTheme.CrystalMandalaFortress:
                {
                    var contrast = profileIndex == 2 || profileIndex == 3 ? 1.18d : 1d;
                    var middle = (minimumRadius + maximumRadius) * 0.5d;
                    var halfRange = (maximumRadius - minimumRadius) * 0.5d * contrast;
                    var inner = middle - halfRange;
                    var outer = middle + halfRange;
                    var boundaryRadius = ResolveAlternatingVertexRadius(angle, phase, 16, inner, outer);
                    return distance <= boundaryRadius + 1e-9d;
                }
                case CastleLayoutTheme.TwinSpiralFortress:
                {
                    var extent = new[] { 0d, 11.7d, 15.6d, 19.1d }[defenseRing] + radiusOffset;
                    var extentX = extent;
                    var extentZ = extent;
                    if (profileIndex == 2)
                    {
                        extentX += 0.65d;
                        extentZ -= 0.25d;
                    }
                    else if (profileIndex == 3)
                    {
                        extentX -= 0.25d;
                        extentZ += 0.65d;
                    }

                    var exponent = profileIndex == 6 ? 4.3d : profileIndex == 7 ? 2.4d : 3.1d;
                    var cosine = Math.Cos(angle);
                    var sine = Math.Sin(angle);
                    var superellipseRadius = 1d / Math.Pow(
                        Math.Pow(Math.Abs(cosine) / extentX, exponent) +
                        Math.Pow(Math.Abs(sine) / extentZ, exponent),
                        1d / exponent);
                    var direction = profileIndex == 5 ? -1d : 1d;
                    var twist = profileIndex == 6 ? 0.68d : profileIndex == 7 ? 1.08d : 0.88d;
                    var radialTurn = direction * twist * distance / 21.5d * Math.PI * 0.42d;
                    var warpedAngle = angle - phase - radialTurn;
                    var armPosition = Fraction(
                        NormalizePositiveAngle(warpedAngle * direction) / Math.PI);
                    var bladeStrength = profileIndex == 6 ? 2.05d : profileIndex == 7 ? 2.7d : 2.4d;
                    var sweptBlade = Math.Pow(1d - armPosition, 0.58d);
                    var trailingNotch = Math.Exp(-Math.Pow((armPosition - 0.68d) / 0.11d, 2d));
                    var steppedShoulder = 0.38d * Math.Sin(armPosition * Math.PI * 4d);
                    var boundaryRadius = superellipseRadius +
                                         bladeStrength * (sweptBlade - 0.35d) -
                                         0.62d * trailingNotch +
                                         steppedShoulder;
                    return distance <= boundaryRadius + 1e-9d;
                }
                case CastleLayoutTheme.FractalBastionFortress:
                {
                    var primary = Math.Abs(Math.Cos((angle - phase) * 4d));
                    var secondary = Math.Abs(Math.Cos((angle - phase) * 8d));
                    var level = primary >= 0.82d
                        ? 1d
                        : secondary >= 0.7d ? 0.64d : 0.2d;
                    if (profileIndex == 2 || profileIndex == 3)
                    {
                        level = Math.Min(1d, level + 0.12d);
                    }

                    var boundaryRadius = minimumRadius + (maximumRadius - minimumRadius) * level;
                    return distance <= boundaryRadius + 1e-9d;
                }
                case CastleLayoutTheme.VoronoiCrystalFortress:
                {
                    var sectorCount = 16;
                    var normalized = NormalizePositiveAngle(angle - phase) / (Math.PI * 2d) * sectorCount;
                    var sector = (int)Math.Floor(normalized);
                    var interpolation = normalized - sector;
                    var first = ResolveVoronoiBoundaryRadius(
                        patternSeed,
                        sector,
                        defenseRing,
                        profileIndex,
                        minimumRadius,
                        maximumRadius);
                    var second = ResolveVoronoiBoundaryRadius(
                        patternSeed,
                        sector + 1,
                        defenseRing,
                        profileIndex,
                        minimumRadius,
                        maximumRadius);
                    var boundaryRadius = first + (second - first) * interpolation;
                    if (profileIndex == 2)
                    {
                        boundaryRadius += 0.35d * Math.Cos(angle * 2d);
                    }
                    else if (profileIndex == 3)
                    {
                        boundaryRadius -= 0.35d * Math.Cos(angle * 2d);
                    }

                    return distance <= boundaryRadius + 1e-9d;
                }
                case CastleLayoutTheme.IrisShutterFortress:
                {
                    var direction = profileIndex == 5 ? -1d : 1d;
                    var bladePosition = Fraction(
                        NormalizePositiveAngle((angle - phase) * direction) /
                        (Math.PI * 2d) * 8d);
                    var blade = Math.Pow(1d - bladePosition, profileIndex == 6 ? 1.15d : 0.72d);
                    var boundaryRadius = minimumRadius +
                                         (maximumRadius - minimumRadius) * (0.2d + blade * 0.8d);
                    return distance <= boundaryRadius + 1e-9d;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(theme), theme, null);
            }
        }

        private static HashSet<Vector2Int>[] SplitGeometricRingIntoCompartments(
            CastleGenerationRules rules,
            HashSet<Vector2Int> ringCells,
            IReadOnlyCollection<Vector2Int> previousDisk,
            CastleLayoutTheme theme,
            int defenseRing,
            int profileIndex,
            int patternSeed,
            int compartmentCount)
        {
            var result = Enumerable.Range(0, compartmentCount)
                .Select(_ => new HashSet<Vector2Int>())
                .ToArray();
            var owners = new Dictionary<Vector2Int, int>();
            var sharedCells = new List<(Vector2Int Cell, int Owner, int Recipient)>();
            foreach (var cell in ringCells)
            {
                var owner = ResolveGeometricOwner(
                    rules,
                    cell,
                    theme,
                    defenseRing,
                    profileIndex,
                    patternSeed,
                    compartmentCount);
                owners[cell] = owner;
                result[owner].Add(cell);
            }

            foreach (var cell in ringCells.OrderBy(value => value.y).ThenBy(value => value.x))
            {
                foreach (var neighbor in new[] { cell + Vector2Int.right, cell + Vector2Int.up })
                {
                    if (!owners.TryGetValue(neighbor, out var neighborOwner))
                    {
                        continue;
                    }

                    var owner = owners[cell];
                    if (owner == neighborOwner)
                    {
                        continue;
                    }

                    var forwardDistance = Mod(neighborOwner - owner, compartmentCount);
                    if (forwardDistance > 0 && forwardDistance <= compartmentCount / 2)
                    {
                        result[neighborOwner].Add(cell);
                        sharedCells.Add((cell, owner, neighborOwner));
                    }
                    else
                    {
                        result[owner].Add(neighbor);
                        sharedCells.Add((neighbor, neighborOwner, owner));
                    }
                }
            }

            foreach (var shared in sharedCells)
            {
                if (!IsFootprintBoundaryCell(result[shared.Owner], shared.Cell) ||
                    !IsFootprintBoundaryCell(result[shared.Recipient], shared.Cell))
                {
                    result[shared.Recipient].Remove(shared.Cell);
                }
            }

            MergeDisconnectedGeometricFragments(result, theme);
            var protectedCells = previousDisk as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(previousDisk);
            foreach (var footprint in result)
            {
                var buriedSharedCells = footprint
                    .Where(cell => protectedCells.Contains(cell) && !IsFootprintBoundaryCell(footprint, cell))
                    .ToArray();
                footprint.ExceptWith(buriedSharedCells);
            }

            MergeDisconnectedGeometricFragments(result, theme);
            RemoveBuriedGeometricIntersections(result, owners);
            MergeDisconnectedGeometricFragments(result, theme);
            RemoveBuriedGeometricIntersections(result, owners);
            if (result.Any(footprint => !IsCardinallyConnected(footprint)))
            {
                throw new InvalidOperationException($"{theme}의 격실 교차 정리 후 연결이 끊겼습니다.");
            }

            return result;
        }

        private static void RemoveBuriedGeometricIntersections(
            HashSet<Vector2Int>[] footprints,
            IReadOnlyDictionary<Vector2Int, int> owners)
        {
            var allCells = footprints.SelectMany(footprint => footprint).Distinct().ToArray();
            foreach (var cell in allCells)
            {
                var memberships = Enumerable.Range(0, footprints.Length)
                    .Where(index => footprints[index].Contains(cell))
                    .ToArray();
                if (memberships.Length < 2 || memberships.All(index =>
                        IsFootprintBoundaryCell(footprints[index], cell)))
                {
                    continue;
                }

                var interiorOwners = memberships
                    .Where(index => !IsFootprintBoundaryCell(footprints[index], cell))
                    .ToArray();
                var keep = owners.TryGetValue(cell, out var originalOwner) && interiorOwners.Contains(originalOwner)
                    ? originalOwner
                    : interiorOwners[0];
                foreach (var index in memberships.Where(index => index != keep))
                {
                    footprints[index].Remove(cell);
                }
            }
        }

        private static int ResolveGeometricOwner(
            CastleGenerationRules rules,
            Vector2Int cell,
            CastleLayoutTheme theme,
            int defenseRing,
            int profileIndex,
            int patternSeed,
            int compartmentCount)
        {
            var centerX = rules.GridWidth * 0.5d;
            var centerZ = rules.GridHeight * 0.5d;
            var deltaX = cell.x + 0.5d - centerX;
            var deltaZ = cell.y + 0.5d - centerZ;
            var distance = Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
            var angle = Math.Atan2(deltaZ, deltaX);
            var phase = ResolveGeometricBasePhase(profileIndex);
            var direction = profileIndex == 5 ? -1d : 1d;
            double warpedAngle;

            switch (theme)
            {
                case CastleLayoutTheme.CrystalMandalaFortress:
                    warpedAngle = angle - phase -
                                  ResolveGeometricRingPhaseStep(profileIndex) * (defenseRing - 1);
                    break;
                case CastleLayoutTheme.TwinSpiralFortress:
                {
                    var squareRadius = Math.Max(Math.Abs(deltaX), Math.Abs(deltaZ));
                    var twist = profileIndex == 6 ? 0.62d : profileIndex == 7 ? 1.02d : 0.82d;
                    warpedAngle = angle - phase - direction * twist * squareRadius / 21.5d * Math.PI;
                    break;
                }
                case CastleLayoutTheme.FractalBastionFortress:
                {
                    var step = Math.Floor(distance / 3.5d);
                    var fractalPhase = profileIndex == 5
                        ? -Math.PI / 8d
                        : profileIndex == 6 ? -Math.PI / 16d : phase;
                    warpedAngle = angle - fractalPhase - step * Math.PI / 32d;
                    break;
                }
                case CastleLayoutTheme.VoronoiCrystalFortress:
                    return ResolveVoronoiOwner(
                        cell,
                        centerX,
                        centerZ,
                        defenseRing,
                        profileIndex,
                        patternSeed,
                        compartmentCount);
                case CastleLayoutTheme.IrisShutterFortress:
                {
                    var twist = profileIndex == 6 ? 0.42d : profileIndex == 7 ? 0.74d : 0.58d;
                    warpedAngle = angle - phase - direction * twist * distance / 21.5d * Math.PI;
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(theme), theme, null);
            }

            return ResolvePetalSector(warpedAngle, compartmentCount, 0d);
        }

        private static int ResolveVoronoiOwner(
            Vector2Int cell,
            double centerX,
            double centerZ,
            int defenseRing,
            int profileIndex,
            int patternSeed,
            int compartmentCount)
        {
            var bestOwner = 0;
            var bestScore = double.MaxValue;
            var phase = ResolveGeometricBasePhase(profileIndex);
            var seedRadius = new[] { 0d, 10.9d, 15.2d, 19.2d }[defenseRing];
            for (var owner = 0; owner < compartmentCount; owner++)
            {
                var pairIndex = owner % (compartmentCount / 2);
                var opposite = owner >= compartmentCount / 2;
                var jitter = (ResolveGeometricNoise(patternSeed, defenseRing, pairIndex, profileIndex) - 0.5d) * 0.26d;
                var radialJitter = (ResolveGeometricNoise(patternSeed, pairIndex, defenseRing, 71 + profileIndex) - 0.5d) * 1.5d;
                var seedAngle = phase + pairIndex * Math.PI / 4d + jitter + (opposite ? Math.PI : 0d);
                var radius = seedRadius + radialJitter;
                var seedX = centerX + Math.Cos(seedAngle) * radius;
                var seedZ = centerZ + Math.Sin(seedAngle) * radius;
                var deltaX = cell.x + 0.5d - seedX;
                var deltaZ = cell.y + 0.5d - seedZ;
                var weight = 0.9d + ResolveGeometricNoise(patternSeed, pairIndex, profileIndex, 131) * 0.2d;
                var score = (deltaX * deltaX + deltaZ * deltaZ) * weight;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestOwner = owner;
                }
            }

            return bestOwner;
        }

        private static void MergeDisconnectedGeometricFragments(
            HashSet<Vector2Int>[] footprints,
            CastleLayoutTheme theme)
        {
            for (var pass = 0; pass < footprints.Length * 3; pass++)
            {
                var changed = false;
                for (var sourceIndex = 0; sourceIndex < footprints.Length; sourceIndex++)
                {
                    var components = FindCardinalComponents(footprints[sourceIndex])
                        .OrderByDescending(value => value.Count)
                        .ToArray();
                    foreach (var fragment in components.Skip(1))
                    {
                        var target = Enumerable.Range(0, footprints.Length)
                            .Where(index => index != sourceIndex)
                            .Select(index => new
                            {
                                Index = index,
                                Score = fragment.Sum(cell =>
                                    (footprints[index].Contains(cell) ? 8 : 0) +
                                    EnumerateNeighbors(cell).Count(footprints[index].Contains))
                            })
                            .OrderByDescending(value => value.Score)
                            .ThenBy(value => value.Index)
                            .First();
                        if (target.Score == 0)
                        {
                            throw new InvalidOperationException($"{theme}의 떨어진 격실 파편을 인접 격실에 연결할 수 없습니다.");
                        }

                        footprints[sourceIndex].ExceptWith(fragment);
                        footprints[target.Index].UnionWith(fragment);
                        changed = true;
                    }
                }

                if (!changed || footprints.All(IsCardinallyConnected))
                {
                    return;
                }
            }

            throw new InvalidOperationException($"{theme}의 격실 연결 파편을 정리하지 못했습니다.");
        }

        private static CompartmentDraft AddGeometricFootprintDraft(
            CastleGenerationRules rules,
            ICollection<CompartmentDraft> drafts,
            string id,
            HashSet<Vector2Int> footprintCells,
            CastleCompartmentRole role,
            int defenseRing,
            CastleLayoutTheme theme)
        {
            var bounds = EncapsulateCells(footprintCells);
            var template = rules.GeometricTemplate;
            var supportsSize = template != null && template.SupportsSize(bounds.width, bounds.height);
            var insideBuildArea = footprintCells.All(rules.BuildableBounds.Contains);
            var connected = IsCardinallyConnected(footprintCells);
            var compatible = IsFootprintCompatibleWithExisting(footprintCells, drafts);
            if (!supportsSize || !insideBuildArea || !connected || !compatible)
            {
                var conflict = DescribeFootprintConflict(footprintCells, drafts);
                throw new InvalidOperationException(
                    $"{theme} 격실을 배치할 수 없습니다: {id} {bounds} " +
                    $"size={supportsSize}, area={insideBuildArea}, connected={connected}, compatible={compatible}, conflict={conflict}");
            }

            var result = new CompartmentDraft
            {
                Id = id,
                Template = template,
                Bounds = bounds,
                Role = role,
                DefenseRing = defenseRing,
                FootprintCells = footprintCells
            };
            drafts.Add(result);
            return result;
        }

        private static CastleStructureVariant ResolveGeometricVariant(
            CastleLayoutTheme theme,
            int profileIndex)
        {
            int firstVariant;
            switch (theme)
            {
                case CastleLayoutTheme.CrystalMandalaFortress:
                    firstVariant = (int)CastleStructureVariant.CrystalMandalaBalanced;
                    break;
                case CastleLayoutTheme.TwinSpiralFortress:
                    firstVariant = (int)CastleStructureVariant.TwinSpiralBalanced;
                    break;
                case CastleLayoutTheme.FractalBastionFortress:
                    firstVariant = (int)CastleStructureVariant.FractalBastionBalanced;
                    break;
                case CastleLayoutTheme.VoronoiCrystalFortress:
                    firstVariant = (int)CastleStructureVariant.VoronoiCrystalBalanced;
                    break;
                case CastleLayoutTheme.IrisShutterFortress:
                    firstVariant = (int)CastleStructureVariant.IrisShutterBalanced;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(theme), theme, null);
            }

            return (CastleStructureVariant)(firstVariant + Mathf.Clamp(profileIndex, 0, 7));
        }

        private static string ResolveGeometricPrefix(CastleLayoutTheme theme)
        {
            switch (theme)
            {
                case CastleLayoutTheme.CrystalMandalaFortress:
                    return "mandala";
                case CastleLayoutTheme.TwinSpiralFortress:
                    return "spiral";
                case CastleLayoutTheme.FractalBastionFortress:
                    return "fractal";
                case CastleLayoutTheme.VoronoiCrystalFortress:
                    return "voronoi";
                case CastleLayoutTheme.IrisShutterFortress:
                    return "iris";
                default:
                    throw new ArgumentOutOfRangeException(nameof(theme), theme, null);
            }
        }

        private static double ResolveGeometricBasePhase(int profileIndex)
        {
            switch (profileIndex)
            {
                case 1:
                case 3:
                    return Math.PI / 8d;
                case 6:
                    return Math.PI / 16d;
                case 7:
                    return -Math.PI / 16d;
                default:
                    return 0d;
            }
        }

        private static double ResolveGeometricRingPhaseStep(int profileIndex)
        {
            if (profileIndex == 4)
            {
                return Math.PI / 32d;
            }

            return profileIndex == 5 ? -Math.PI / 32d : 0d;
        }

        private static double ResolveAlternatingVertexRadius(
            double angle,
            double phase,
            int vertexCount,
            double innerRadius,
            double outerRadius)
        {
            var position = NormalizePositiveAngle(angle - phase) / (Math.PI * 2d) * vertexCount;
            var vertex = (int)Math.Floor(position);
            var interpolation = position - vertex;
            var first = (vertex & 1) == 0 ? outerRadius : innerRadius;
            var second = ((vertex + 1) & 1) == 0 ? outerRadius : innerRadius;
            return first + (second - first) * interpolation;
        }

        private static double ResolveVoronoiBoundaryRadius(
            int patternSeed,
            int sector,
            int defenseRing,
            int profileIndex,
            double minimumRadius,
            double maximumRadius)
        {
            var mirroredSector = Mod(sector, 8);
            var noise = ResolveGeometricNoise(patternSeed, defenseRing, mirroredSector, profileIndex);
            return minimumRadius + (maximumRadius - minimumRadius) * (0.18d + noise * 0.82d);
        }

        private static double ResolveGeometricNoise(int seed, int first, int second, int salt)
        {
            unchecked
            {
                var hash = seed;
                hash = hash * 397 ^ first;
                hash = hash * 397 ^ second;
                hash = hash * 397 ^ salt;
                hash ^= hash >> 16;
                return ((uint)hash & 0x00ffffffu) / 16777215d;
            }
        }

        private static double NormalizePositiveAngle(double angle)
        {
            while (angle < 0d)
            {
                angle += Math.PI * 2d;
            }

            while (angle >= Math.PI * 2d)
            {
                angle -= Math.PI * 2d;
            }

            return angle;
        }

        private static double Fraction(double value)
        {
            return value - Math.Floor(value);
        }

        private static string DescribeFootprintConflict(
            IReadOnlyCollection<Vector2Int> candidate,
            IEnumerable<CompartmentDraft> drafts)
        {
            var candidateSet = candidate as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(candidate);
            foreach (var existing in drafts)
            {
                foreach (var cell in EnumerateFootprintCells(existing).Where(candidateSet.Contains))
                {
                    var candidateBoundary = IsFootprintBoundaryCell(candidateSet, cell);
                    var existingBoundary = IsDraftBoundaryCell(existing, cell);
                    if (!candidateBoundary || !existingBoundary)
                    {
                        return $"{existing.Id}@{cell}:candidateBoundary={candidateBoundary},existingBoundary={existingBoundary}";
                    }
                }
            }

            return "none";
        }

        private static bool IsCardinallyConnected(IReadOnlyCollection<Vector2Int> footprint)
        {
            if (footprint.Count == 0)
            {
                return false;
            }

            var cells = footprint as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(footprint);
            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(cells.First());
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!visited.Add(current))
                {
                    continue;
                }

                foreach (var neighbor in EnumerateNeighbors(current).Where(cells.Contains))
                {
                    if (!visited.Contains(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return visited.Count == cells.Count;
        }

        private static IEnumerable<Vector2Int> EnumerateRectCells(RectInt bounds)
        {
            for (var x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (var z = bounds.yMin; z < bounds.yMax; z++)
                {
                    yield return new Vector2Int(x, z);
                }
            }
        }

        private static List<CompartmentDraft> BuildHexHoneycombFortress(
            CastleGenerationRules rules,
            System.Random random,
            int defenseLayerCount,
            out CastleStructureVariant structureVariant)
        {
            var profile = random.Next(8);
            var chamberPatternSeed = random.Next();
            var transposed = profile >= 4;
            var morphology = profile % 4;
            structureVariant = (CastleStructureVariant)((int)CastleStructureVariant.HexHoneycombFlatPhaseA + profile);

            var coreAxialCells = EnumerateHexDisk(1).ToArray();
            var uncenteredCore = BuildHexFootprint(coreAxialCells, Vector2Int.zero, transposed);
            var uncenteredBounds = EncapsulateCells(uncenteredCore);
            var palaceBounds = CastleSpatialContract.PalaceBounds;
            var targetCoreMinimum = new Vector2Int(
                palaceBounds.xMin - (uncenteredBounds.width - palaceBounds.width) / 2,
                palaceBounds.yMin - (uncenteredBounds.height - palaceBounds.height) / 2);
            var origin = targetCoreMinimum - uncenteredBounds.position;
            var coreFootprint = BuildHexFootprint(coreAxialCells, origin, transposed);
            var coreBounds = EncapsulateCells(coreFootprint);
            var coreTemplate = rules.HexQueenTemplate;
            if (coreTemplate == null || !coreTemplate.SupportsSize(coreBounds.width, coreBounds.height))
            {
                throw new InvalidOperationException($"육각 여왕방 크기를 지원하는 템플릿이 없습니다: {coreBounds.size}");
            }

            var drafts = new List<CompartmentDraft>
            {
                new CompartmentDraft
                {
                    Id = "palace_core",
                    Template = coreTemplate,
                    Bounds = coreBounds,
                    Role = CastleCompartmentRole.PalaceCore,
                    DefenseRing = 0,
                    FootprintCells = coreFootprint
                }
            };
            var visited = new HashSet<Vector2Int>(coreAxialCells);
            for (var defenseRing = 1; defenseRing < defenseLayerCount; defenseRing++)
            {
                var role = defenseRing == 1
                    ? CastleCompartmentRole.InnerRing
                    : CastleCompartmentRole.OuterRing;
                var index = 0;
                var baseRadius = defenseRing + 1;
                var shell = EnumerateHexRing(baseRadius)
                    .Where(value => !visited.Contains(value))
                    .ToArray();
                var buds = SelectHiveBudCells(
                        baseRadius + 1,
                        morphology,
                        defenseRing,
                        axialCell => BuildHexCellFootprint(axialCell, origin, transposed)
                            .All(rules.BuildableBounds.Contains))
                    .Where(value => !visited.Contains(value))
                    .ToArray();
                AddMixedHiveChambers(
                    rules,
                    drafts,
                    shell.Concat(buds).ToArray(),
                    buds,
                    origin,
                    transposed,
                    role,
                    defenseRing,
                    chamberPatternSeed,
                    ref index);

                visited.UnionWith(shell);
                visited.UnionWith(buds);
            }

            CastleGenerationRules.ResolveCompartmentCountRange(
                CastleLayoutTheme.HexHoneycombFortress,
                defenseLayerCount,
                out var minimumCount,
                out var maximumCount);
            var regularCount = drafts.Count - 1;
            if (regularCount < minimumCount || regularCount > maximumCount)
            {
                throw new InvalidOperationException(
                    $"{defenseLayerCount}중벽 육각 벌집 요새의 격실 수가 맞지 않습니다: {regularCount}/{minimumCount}~{maximumCount}");
            }

            return drafts;
        }

        private static void AddMixedHiveChambers(
            CastleGenerationRules rules,
            ICollection<CompartmentDraft> drafts,
            IReadOnlyCollection<Vector2Int> axialCells,
            IReadOnlyCollection<Vector2Int> budCells,
            Vector2Int origin,
            bool transposed,
            CastleCompartmentRole role,
            int defenseRing,
            int patternSeed,
            ref int index)
        {
            var unassigned = new HashSet<Vector2Int>(axialCells);
            var budSet = new HashSet<Vector2Int>(budCells);
            var largeTarget = Math.Max(1, axialCells.Count / 8);
            if (CalculateHivePatternScore(Vector2Int.zero, patternSeed, defenseRing * 31) % 3 == 0)
            {
                largeTarget++;
            }

            var mediumTarget = Math.Max(2, axialCells.Count / 5);
            if (CalculateHivePatternScore(Vector2Int.one, patternSeed, defenseRing * 47) % 2 == 0)
            {
                mediumTarget++;
            }

            for (var largeIndex = 0; largeIndex < largeTarget; largeIndex++)
            {
                if (!TryTakeCompactHiveTriple(
                        rules,
                        drafts,
                        unassigned,
                        budSet,
                        origin,
                        transposed,
                        patternSeed,
                        defenseRing * 100 + largeIndex,
                        out var group))
                {
                    break;
                }

                AddHexFootprintDraft(
                    rules,
                    drafts,
                    $"hex_{defenseRing:00}_brood_{++index:00}",
                    BuildHexFootprint(group, origin, transposed),
                    role,
                    defenseRing);
                unassigned.ExceptWith(group);
            }

            for (var mediumIndex = 0; mediumIndex < mediumTarget; mediumIndex++)
            {
                if (!TryTakeHivePair(
                        rules,
                        drafts,
                        unassigned,
                        budSet,
                        origin,
                        transposed,
                        patternSeed,
                        defenseRing * 100 + 50 + mediumIndex,
                        out var group))
                {
                    break;
                }

                AddHexFootprintDraft(
                    rules,
                    drafts,
                    $"hex_{defenseRing:00}_medium_{++index:00}",
                    BuildHexFootprint(group, origin, transposed),
                    role,
                    defenseRing);
                unassigned.ExceptWith(group);
            }

            foreach (var axialCell in OrderHiveCells(
                         unassigned,
                         patternSeed,
                         defenseRing * 100 + 90))
            {
                AddHexFootprintDraft(
                    rules,
                    drafts,
                    $"hex_{defenseRing:00}_small_{++index:00}",
                    BuildHexCellFootprint(axialCell, origin, transposed),
                    role,
                    defenseRing);
            }
        }

        private static bool TryTakeCompactHiveTriple(
            CastleGenerationRules rules,
            IEnumerable<CompartmentDraft> drafts,
            IReadOnlyCollection<Vector2Int> unassigned,
            IReadOnlyCollection<Vector2Int> budCells,
            Vector2Int origin,
            bool transposed,
            int patternSeed,
            int salt,
            out Vector2Int[] result)
        {
            var available = unassigned as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(unassigned);
            var pivots = OrderHiveCells(
                    available.Where(budCells.Contains),
                    patternSeed,
                    salt)
                .Concat(OrderHiveCells(
                    available.Where(value => !budCells.Contains(value)),
                    patternSeed,
                    salt + 1));
            foreach (var pivot in pivots)
            {
                var neighbors = OrderHiveCells(
                        available.Where(value =>
                            CalculateHexDistance(value) == CalculateHexDistance(pivot) &&
                            AreHexNeighbors(pivot, value)),
                        patternSeed,
                        salt + 2)
                    .ToArray();
                foreach (var first in neighbors)
                {
                    foreach (var second in OrderHiveCells(
                                 available.Where(value =>
                                     value != pivot &&
                                     value != first &&
                                     CalculateHexDistance(value) == CalculateHexDistance(pivot) &&
                                     AreHexNeighbors(first, value)),
                                 patternSeed,
                                 salt + 3))
                    {
                        var group = new[] { pivot, first, second };
                        var footprint = BuildHexFootprint(group, origin, transposed);
                        if (!CanPlaceHexFootprintDraft(rules, drafts, footprint))
                        {
                            continue;
                        }

                        result = group;
                        return true;
                    }
                }
            }

            result = Array.Empty<Vector2Int>();
            return false;
        }

        private static bool TryTakeHivePair(
            CastleGenerationRules rules,
            IEnumerable<CompartmentDraft> drafts,
            IReadOnlyCollection<Vector2Int> unassigned,
            IReadOnlyCollection<Vector2Int> budCells,
            Vector2Int origin,
            bool transposed,
            int patternSeed,
            int salt,
            out Vector2Int[] result)
        {
            var available = unassigned as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(unassigned);
            var pivots = OrderHiveCells(
                    available.Where(budCells.Contains),
                    patternSeed,
                    salt)
                .Concat(OrderHiveCells(
                    available.Where(value => !budCells.Contains(value)),
                    patternSeed,
                    salt + 1));
            foreach (var pivot in pivots)
            {
                foreach (var neighbor in OrderHiveCells(
                             available.Where(value =>
                                 CalculateHexDistance(value) == CalculateHexDistance(pivot) &&
                                 AreHexNeighbors(pivot, value)),
                             patternSeed,
                             salt + 2))
                {
                    var group = new[] { pivot, neighbor };
                    var footprint = BuildHexFootprint(group, origin, transposed);
                    if (!CanPlaceHexFootprintDraft(rules, drafts, footprint))
                    {
                        continue;
                    }

                    result = group;
                    return true;
                }
            }

            result = Array.Empty<Vector2Int>();
            return false;
        }

        private static IEnumerable<Vector2Int> OrderHiveCells(
            IEnumerable<Vector2Int> cells,
            int patternSeed,
            int salt)
        {
            return cells
                .OrderBy(value => CalculateHivePatternScore(value, patternSeed, salt))
                .ThenBy(value => value.x)
                .ThenBy(value => value.y);
        }

        private static int CalculateHivePatternScore(Vector2Int cell, int patternSeed, int salt)
        {
            unchecked
            {
                var hash = patternSeed;
                hash = hash * 397 ^ salt;
                hash = hash * 397 ^ cell.x;
                hash = hash * 397 ^ cell.y;
                return hash & int.MaxValue;
            }
        }

        private static bool AreHexNeighbors(Vector2Int first, Vector2Int second)
        {
            return CalculateHexDistance(first - second) == 1;
        }

        private static bool CanPlaceHexFootprintDraft(
            CastleGenerationRules rules,
            IEnumerable<CompartmentDraft> drafts,
            HashSet<Vector2Int> footprintCells)
        {
            var bounds = EncapsulateCells(footprintCells);
            return footprintCells.All(rules.BuildableBounds.Contains) &&
                   IsFootprintCompatibleWithExisting(footprintCells, drafts) &&
                   ResolveRegularTemplate(rules, bounds) != null;
        }

        private static IEnumerable<Vector2Int> EnumerateHexDisk(int radius)
        {
            for (var q = -radius; q <= radius; q++)
            {
                for (var r = -radius; r <= radius; r++)
                {
                    var cell = new Vector2Int(q, r);
                    if (CalculateHexDistance(cell) <= radius)
                    {
                        yield return cell;
                    }
                }
            }
        }

        private static Vector2Int[] EnumerateHexRing(int radius)
        {
            return EnumerateHexDisk(radius)
                .Where(value => CalculateHexDistance(value) == radius)
                .OrderBy(value => Math.Atan2(value.x * 2d + value.y * 4d, value.x * 4d))
                .ThenBy(value => value.x)
                .ThenBy(value => value.y)
                .ToArray();
        }

        private static int CalculateHexDistance(Vector2Int axialCell)
        {
            return Math.Max(
                Math.Abs(axialCell.x),
                Math.Max(Math.Abs(axialCell.y), Math.Abs(axialCell.x + axialCell.y)));
        }

        private static HashSet<Vector2Int> BuildHexFootprint(
            IEnumerable<Vector2Int> axialCells,
            Vector2Int origin,
            bool transposed)
        {
            var result = new HashSet<Vector2Int>();
            foreach (var axialCell in axialCells)
            {
                result.UnionWith(BuildHexCellFootprint(axialCell, origin, transposed));
            }

            return result;
        }

        private static Vector2Int[] SelectHiveBudCells(
            int radius,
            int morphology,
            int defenseRing,
            Func<Vector2Int, bool> isValid)
        {
            var ring = EnumerateHexRing(radius).Where(isValid).ToArray();
            var budCount = 3 + morphology;
            if (ring.Length < budCount)
            {
                throw new InvalidOperationException(
                    $"육각 군락의 외곽 싹 후보가 부족합니다: 반경 {radius}, 후보 {ring.Length}, 필요 {budCount}");
            }

            var selectedIndices = new HashSet<int>();
            var offset = (defenseRing - 1) * (morphology + 1);
            if (morphology == 1)
            {
                selectedIndices.Add(Mod(offset, ring.Length));
                selectedIndices.Add(Mod(offset + 1, ring.Length));
                selectedIndices.Add(Mod(offset + ring.Length / 2, ring.Length));
                selectedIndices.Add(Mod(offset + ring.Length / 2 + 1, ring.Length));
            }
            else
            {
                for (var index = 0; index < budCount; index++)
                {
                    selectedIndices.Add(Mod(
                        offset + (int)Math.Floor(index * ring.Length / (double)budCount),
                        ring.Length));
                }
            }

            return selectedIndices.OrderBy(value => value).Select(value => ring[value]).ToArray();
        }

        private static int Mod(int value, int modulus)
        {
            var result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static HashSet<Vector2Int> BuildHexCellFootprint(
            Vector2Int axialCell,
            Vector2Int origin,
            bool transposed)
        {
            var axialOffset = transposed
                ? new Vector2Int(
                    axialCell.x * 2 + axialCell.y * 4,
                    axialCell.x * 4)
                : new Vector2Int(
                    axialCell.x * 4,
                    axialCell.x * 2 + axialCell.y * 4);
            var result = new HashSet<Vector2Int>();
            var insets = new[] { 2, 1, 0, 1, 2 };
            for (var row = 0; row < insets.Length; row++)
            {
                for (var column = insets[row]; column < 7 - insets[row]; column++)
                {
                    var local = transposed
                        ? new Vector2Int(row, column)
                        : new Vector2Int(column, row);
                    result.Add(origin + axialOffset + local);
                }
            }

            return result;
        }

        private static CompartmentDraft AddHexFootprintDraft(
            CastleGenerationRules rules,
            ICollection<CompartmentDraft> drafts,
            string id,
            HashSet<Vector2Int> footprintCells,
            CastleCompartmentRole role,
            int defenseRing)
        {
            var bounds = EncapsulateCells(footprintCells);
            if (!footprintCells.All(rules.BuildableBounds.Contains) ||
                !IsFootprintCompatibleWithExisting(footprintCells, drafts))
            {
                throw new InvalidOperationException($"육각 격실을 배치할 수 없습니다: {id} {bounds}");
            }

            var template = ResolveRegularTemplate(rules, bounds);
            if (template == null)
            {
                throw new InvalidOperationException($"육각 격실 크기를 지원하는 템플릿이 없습니다: {bounds.size}");
            }

            var result = new CompartmentDraft
            {
                Id = id,
                Template = template,
                Bounds = bounds,
                Role = role,
                DefenseRing = defenseRing,
                FootprintCells = footprintCells
            };
            drafts.Add(result);
            return result;
        }

        private static CastleStructureVariant BuildHoneycombCompartmentFortress(
            CastleGenerationRules rules,
            System.Random random,
            CompartmentDraft core,
            int defenseLayerCount,
            ICollection<CompartmentDraft> drafts)
        {
            var profile = BuildHoneycombShellProfile(random);
            var innerEnvelope = core.Bounds;
            for (var defenseRing = 1; defenseRing < defenseLayerCount; defenseRing++)
            {
                innerEnvelope = AddHoneycombDefenseRing(
                    rules,
                    random,
                    drafts,
                    innerEnvelope,
                    defenseRing,
                    profile);
            }

            CastleGenerationRules.ResolveCompartmentCountRange(
                CastleLayoutTheme.HoneycombCompartmentFortress,
                defenseLayerCount,
                out var minimumCount,
                out var maximumCount);
            var regularCount = drafts.Count - 1;
            if (regularCount < minimumCount || regularCount > maximumCount)
            {
                throw new InvalidOperationException(
                    $"{defenseLayerCount}중벽 벌집 격실 요새의 격실 수가 맞지 않습니다: {regularCount}/{minimumCount}~{maximumCount}");
            }

            return profile.Variant;
        }

        private static RectInt AddHoneycombDefenseRing(
            CastleGenerationRules rules,
            System.Random random,
            ICollection<CompartmentDraft> drafts,
            RectInt innerEnvelope,
            int defenseRing,
            HoneycombShellProfile profile)
        {
            var profileIndex = defenseRing - 1;
            var northThickness = profile.NorthSteps[profileIndex] + 1;
            var eastThickness = profile.EastSteps[profileIndex] + 1;
            var southThickness = profile.SouthSteps[profileIndex] + 1;
            var westThickness = profile.WestSteps[profileIndex] + 1;
            var outerEnvelope = new RectInt(
                innerEnvelope.xMin - (westThickness - 1),
                innerEnvelope.yMin - (southThickness - 1),
                innerEnvelope.width + westThickness + eastThickness - 2,
                innerEnvelope.height + southThickness + northThickness - 2);
            var role = defenseRing == 1
                ? CastleCompartmentRole.InnerRing
                : CastleCompartmentRole.OuterRing;
            var prefix = $"honey_{defenseRing:00}";
            var steppedOuterEdge = defenseRing == (int)CastleDefenseLayerPreset.Triple; // 최외곽 단차만 빈 추가 방어층을 만들지 않는다

            AddRegularDraft(
                rules,
                drafts,
                prefix + "_nw",
                new RectInt(outerEnvelope.xMin, innerEnvelope.yMax - 1, westThickness, northThickness),
                role,
                defenseRing);
            AddRegularDraft(
                rules,
                drafts,
                prefix + "_ne",
                new RectInt(innerEnvelope.xMax - 1, innerEnvelope.yMax - 1, eastThickness, northThickness),
                role,
                defenseRing);
            AddRegularDraft(
                rules,
                drafts,
                prefix + "_sw",
                new RectInt(outerEnvelope.xMin, outerEnvelope.yMin, westThickness, southThickness),
                role,
                defenseRing);
            AddRegularDraft(
                rules,
                drafts,
                prefix + "_se",
                new RectInt(innerEnvelope.xMax - 1, outerEnvelope.yMin, eastThickness, southThickness),
                role,
                defenseRing);

            AddHoneycombSegmentedSide(
                rules,
                random,
                drafts,
                prefix + "_n",
                innerEnvelope,
                RingSide.North,
                northThickness,
                defenseRing,
                role,
                steppedOuterEdge,
                ShouldReverseHoneycombSegments(profile.Variant, RingSide.North, defenseRing));
            AddHoneycombSegmentedSide(
                rules,
                random,
                drafts,
                prefix + "_e",
                innerEnvelope,
                RingSide.East,
                eastThickness,
                defenseRing,
                role,
                steppedOuterEdge,
                ShouldReverseHoneycombSegments(profile.Variant, RingSide.East, defenseRing));
            AddHoneycombSegmentedSide(
                rules,
                random,
                drafts,
                prefix + "_s",
                innerEnvelope,
                RingSide.South,
                southThickness,
                defenseRing,
                role,
                steppedOuterEdge,
                ShouldReverseHoneycombSegments(profile.Variant, RingSide.South, defenseRing));
            AddHoneycombSegmentedSide(
                rules,
                random,
                drafts,
                prefix + "_w",
                innerEnvelope,
                RingSide.West,
                westThickness,
                defenseRing,
                role,
                steppedOuterEdge,
                ShouldReverseHoneycombSegments(profile.Variant, RingSide.West, defenseRing));

            return outerEnvelope;
        }

        private static void AddHoneycombSegmentedSide(
            CastleGenerationRules rules,
            System.Random random,
            ICollection<CompartmentDraft> drafts,
            string idPrefix,
            RectInt innerEnvelope,
            RingSide side,
            int sideThickness,
            int defenseRing,
            CastleCompartmentRole role,
            bool steppedOuterEdge,
            bool reverseSegments)
        {
            var splitAlongX = side == RingSide.North || side == RingSide.South;
            var sideLength = splitAlongX ? innerEnvelope.width : innerEnvelope.height;
            var segmentCount = defenseRing * 2;
            var segmentSizes = BuildHoneycombSegmentSizes(sideLength, segmentCount, random);
            if (reverseSegments)
            {
                segmentSizes.Reverse();
            }

            var cursor = splitAlongX ? innerEnvelope.xMin : innerEnvelope.yMin;
            var maximumInset = steppedOuterEdge ? Mathf.Min(2, sideThickness - 5) : 0;
            var requiredStepIndex = maximumInset > 0 ? random.Next(segmentSizes.Count) : -1;
            var requiredFullIndex = requiredStepIndex >= 0
                ? (requiredStepIndex + 1) % segmentSizes.Count
                : -1;
            for (var index = 0; index < segmentSizes.Count; index++)
            {
                var segmentSize = segmentSizes[index];
                var inset = maximumInset <= 0
                    ? 0
                    : index == requiredStepIndex
                        ? maximumInset
                        : index == requiredFullIndex
                            ? 0
                            : random.Next(0, maximumInset + 1);
                var thickness = sideThickness - inset;
                RectInt bounds;
                switch (side)
                {
                    case RingSide.North:
                        bounds = new RectInt(cursor, innerEnvelope.yMax - 1, segmentSize, thickness);
                        break;
                    case RingSide.East:
                        bounds = new RectInt(innerEnvelope.xMax - 1, cursor, thickness, segmentSize);
                        break;
                    case RingSide.South:
                        bounds = new RectInt(
                            cursor,
                            innerEnvelope.yMin - (thickness - 1),
                            segmentSize,
                            thickness);
                        break;
                    case RingSide.West:
                        bounds = new RectInt(
                            innerEnvelope.xMin - (thickness - 1),
                            cursor,
                            thickness,
                            segmentSize);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(side), side, null);
                }

                AddRegularDraft(
                    rules,
                    drafts,
                    $"{idPrefix}_{index + 1:00}",
                    bounds,
                    role,
                    defenseRing);
                cursor += segmentSize - 1;
            }
        }

        private static List<int> BuildHoneycombSegmentSizes(
            int totalLength,
            int segmentCount,
            System.Random random)
        {
            const int minimumSize = 5;
            const int maximumSize = 8;
            var remaining = totalLength + segmentCount - 1;
            var result = new List<int>(segmentCount);
            for (var index = 0; index < segmentCount; index++)
            {
                var remainingSegments = segmentCount - index - 1;
                var minimum = Mathf.Max(minimumSize, remaining - maximumSize * remainingSegments);
                var maximum = Mathf.Min(maximumSize, remaining - minimumSize * remainingSegments);
                if (minimum > maximum)
                {
                    throw new InvalidOperationException(
                        $"길이 {totalLength}를 {segmentCount}개 벌집 격실로 분할할 수 없습니다.");
                }

                var size = remainingSegments == 0
                    ? remaining
                    : random.Next(minimum, maximum + 1);
                result.Add(size);
                remaining -= size;
            }

            return result;
        }

        private static HoneycombShellProfile BuildHoneycombShellProfile(System.Random random)
        {
            var variants = new[]
            {
                CastleStructureVariant.HoneycombBalanced,
                CastleStructureVariant.HoneycombHorizontalWide,
                CastleStructureVariant.HoneycombVerticalTall,
                CastleStructureVariant.HoneycombDiagonalNorthEast,
                CastleStructureVariant.HoneycombDiagonalSouthEast,
                CastleStructureVariant.HoneycombDiagonalSouthWest,
                CastleStructureVariant.HoneycombDiagonalNorthWest,
                CastleStructureVariant.HoneycombStaggeredClockwise,
                CastleStructureVariant.HoneycombStaggeredCounterClockwise
            };
            var profile = new HoneycombShellProfile
            {
                Variant = variants[random.Next(variants.Length)],
                NorthSteps = BuildHoneycombBalancedSteps(random),
                EastSteps = BuildHoneycombBalancedSteps(random),
                SouthSteps = BuildHoneycombBalancedSteps(random),
                WestSteps = BuildHoneycombBalancedSteps(random)
            };

            switch (profile.Variant)
            {
                case CastleStructureVariant.HoneycombHorizontalWide:
                    profile.EastSteps = new[] { 5, 5, 5 };
                    profile.WestSteps = new[] { 5, 5, 5 };
                    profile.NorthSteps = new[] { 4, 4, 4 };
                    profile.SouthSteps = new[] { 4, 4, 4 };
                    break;
                case CastleStructureVariant.HoneycombVerticalTall:
                    profile.NorthSteps = new[] { 5, 5, 5 };
                    profile.SouthSteps = new[] { 5, 5, 5 };
                    profile.EastSteps = new[] { 4, 4, 4 };
                    profile.WestSteps = new[] { 4, 4, 4 };
                    break;
                case CastleStructureVariant.HoneycombDiagonalNorthEast:
                    ApplyHoneycombDiagonalBias(profile, true, true);
                    break;
                case CastleStructureVariant.HoneycombDiagonalSouthEast:
                    ApplyHoneycombDiagonalBias(profile, false, true);
                    break;
                case CastleStructureVariant.HoneycombDiagonalSouthWest:
                    ApplyHoneycombDiagonalBias(profile, false, false);
                    break;
                case CastleStructureVariant.HoneycombDiagonalNorthWest:
                    ApplyHoneycombDiagonalBias(profile, true, false);
                    break;
                case CastleStructureVariant.HoneycombStaggeredClockwise:
                    profile.NorthSteps = new[] { 5, 4, 5 };
                    profile.EastSteps = new[] { 4, 5, 5 };
                    profile.SouthSteps = new[] { 5, 5, 4 };
                    profile.WestSteps = new[] { 5, 4, 5 };
                    break;
                case CastleStructureVariant.HoneycombStaggeredCounterClockwise:
                    profile.NorthSteps = new[] { 4, 5, 5 };
                    profile.EastSteps = new[] { 5, 4, 5 };
                    profile.SouthSteps = new[] { 5, 4, 5 };
                    profile.WestSteps = new[] { 4, 5, 5 };
                    break;
            }

            return profile;
        }

        private static int[] BuildHoneycombBalancedSteps(System.Random random)
        {
            switch (random.Next(3))
            {
                case 0:
                    return new[] { 4, 4, 5 };
                case 1:
                    return new[] { 4, 5, 5 };
                default:
                    return new[] { 5, 4, 5 };
            }
        }

        private static void ApplyHoneycombDiagonalBias(
            HoneycombShellProfile profile,
            bool north,
            bool east)
        {
            profile.NorthSteps = north ? new[] { 5, 5, 5 } : new[] { 4, 4, 4 };
            profile.SouthSteps = north ? new[] { 4, 4, 4 } : new[] { 5, 5, 5 };
            profile.EastSteps = east ? new[] { 5, 5, 5 } : new[] { 4, 4, 4 };
            profile.WestSteps = east ? new[] { 4, 4, 4 } : new[] { 5, 5, 5 };
        }

        private static bool ShouldReverseHoneycombSegments(
            CastleStructureVariant variant,
            RingSide side,
            int defenseRing)
        {
            var sideIndex = (int)side;
            if (variant == CastleStructureVariant.HoneycombStaggeredClockwise)
            {
                return (sideIndex + defenseRing) % 2 == 0;
            }

            if (variant == CastleStructureVariant.HoneycombStaggeredCounterClockwise)
            {
                return (sideIndex + defenseRing) % 2 != 0;
            }

            return ((int)variant + sideIndex + defenseRing) % 2 == 0;
        }

        private static CastleStructureVariant BuildDiamondRadialFortress(
            CastleGenerationRules rules,
            System.Random random,
            CompartmentDraft core,
            int defenseLayerCount,
            ICollection<CompartmentDraft> drafts)
        {
            var profile = BuildDiamondShellProfile(random);
            for (var defenseRing = 1; defenseRing < defenseLayerCount; defenseRing++)
            {
                AddDiamondDefenseRing(rules, random, drafts, core.Bounds, defenseRing, profile);
            }

            CastleGenerationRules.ResolveCompartmentCountRange(
                defenseLayerCount,
                out var minimumCount,
                out var maximumCount);
            var regularCount = drafts.Count - 1;
            if (regularCount < minimumCount || regularCount > maximumCount)
            {
                throw new InvalidOperationException(
                    $"{defenseLayerCount}중벽 마름모 방사형 요새의 격실 수가 맞지 않습니다: {regularCount}/{minimumCount}~{maximumCount}");
            }

            return profile.Variant;
        }

        private static void AddDiamondDefenseRing(
            CastleGenerationRules rules,
            System.Random random,
            ICollection<CompartmentDraft> drafts,
            RectInt core,
            int defenseRing,
            DiamondShellProfile profile)
        {
            var role = defenseRing == 1
                ? CastleCompartmentRole.InnerRing
                : CastleCompartmentRole.OuterRing;
            var prefix = $"diamond_{defenseRing:00}";
            var reverse = random.Next(2) == 0;
            var northDepth = profile.NorthSteps[defenseRing - 1] + 1;
            var eastDepth = profile.EastSteps[defenseRing - 1] + 1;
            var southDepth = profile.SouthSteps[defenseRing - 1] + 1;
            var westDepth = profile.WestSteps[defenseRing - 1] + 1;

            AddDiamondAxisPair(
                rules,
                random,
                drafts,
                prefix + "_n",
                new RectInt(
                    core.xMin,
                    core.yMax - 1 + SumStepsBefore(profile.NorthSteps, defenseRing),
                    core.width,
                    northDepth),
                true,
                role,
                defenseRing,
                reverse);
            AddDiamondAxisPair(
                rules,
                random,
                drafts,
                prefix + "_e",
                new RectInt(
                    core.xMax - 1 + SumStepsBefore(profile.EastSteps, defenseRing),
                    core.yMin,
                    eastDepth,
                    core.height),
                false,
                role,
                defenseRing,
                !reverse);
            AddDiamondAxisPair(
                rules,
                random,
                drafts,
                prefix + "_s",
                new RectInt(
                    core.xMin,
                    core.yMin - SumStepsThrough(profile.SouthSteps, defenseRing),
                    core.width,
                    southDepth),
                true,
                role,
                defenseRing,
                !reverse);
            AddDiamondAxisPair(
                rules,
                random,
                drafts,
                prefix + "_w",
                new RectInt(
                    core.xMin - SumStepsThrough(profile.WestSteps, defenseRing),
                    core.yMin,
                    westDepth,
                    core.height),
                false,
                role,
                defenseRing,
                reverse);

            for (var horizontalStep = 1; horizontalStep < defenseRing; horizontalStep++)
            {
                var verticalStep = defenseRing - horizontalStep;
                var northZ = core.yMax - 1 + SumStepsBefore(profile.NorthSteps, verticalStep);
                var southZ = core.yMin - SumStepsThrough(profile.SouthSteps, verticalStep);
                var westX = core.xMin - SumStepsThrough(profile.WestSteps, horizontalStep);
                var eastX = core.xMax - 1 + SumStepsBefore(profile.EastSteps, horizontalStep);
                var northStepDepth = profile.NorthSteps[verticalStep - 1] + 1;
                var eastStepDepth = profile.EastSteps[horizontalStep - 1] + 1;
                var southStepDepth = profile.SouthSteps[verticalStep - 1] + 1;
                var westStepDepth = profile.WestSteps[horizontalStep - 1] + 1;
                AddRegularDraft(
                    rules, drafts, $"{prefix}_nw_{horizontalStep:00}",
                    new RectInt(westX, northZ, westStepDepth, northStepDepth), role, defenseRing);
                AddRegularDraft(
                    rules, drafts, $"{prefix}_ne_{horizontalStep:00}",
                    new RectInt(eastX, northZ, eastStepDepth, northStepDepth), role, defenseRing);
                AddRegularDraft(
                    rules, drafts, $"{prefix}_se_{horizontalStep:00}",
                    new RectInt(eastX, southZ, eastStepDepth, southStepDepth), role, defenseRing);
                AddRegularDraft(
                    rules, drafts, $"{prefix}_sw_{horizontalStep:00}",
                    new RectInt(westX, southZ, westStepDepth, southStepDepth), role, defenseRing);
            }
        }

        private static DiamondShellProfile BuildDiamondShellProfile(System.Random random)
        {
            var variants = new[]
            {
                CastleStructureVariant.DiamondBalanced,
                CastleStructureVariant.DiamondHorizontalWide,
                CastleStructureVariant.DiamondVerticalTall,
                CastleStructureVariant.DiamondDiagonalNorthEast,
                CastleStructureVariant.DiamondDiagonalSouthEast,
                CastleStructureVariant.DiamondDiagonalSouthWest,
                CastleStructureVariant.DiamondDiagonalNorthWest,
                CastleStructureVariant.DiamondStaggeredClockwise,
                CastleStructureVariant.DiamondStaggeredCounterClockwise
            };
            var variant = variants[random.Next(variants.Length)];
            var balancedNorth = BuildBalancedSteps(random);
            var balancedEast = BuildBalancedSteps(random);
            var balancedSouth = BuildBalancedSteps(random);
            var balancedWest = BuildBalancedSteps(random);
            var profile = new DiamondShellProfile
            {
                Variant = variant,
                NorthSteps = balancedNorth,
                EastSteps = balancedEast,
                SouthSteps = balancedSouth,
                WestSteps = balancedWest
            };

            switch (variant)
            {
                case CastleStructureVariant.DiamondHorizontalWide:
                    profile.EastSteps = new[] { 6, 5, 5 };
                    profile.WestSteps = new[] { 6, 5, 5 };
                    profile.NorthSteps = new[] { 4, 5, 5 };
                    profile.SouthSteps = new[] { 4, 5, 5 };
                    break;
                case CastleStructureVariant.DiamondVerticalTall:
                    profile.NorthSteps = new[] { 6, 5, 5 };
                    profile.SouthSteps = new[] { 6, 5, 5 };
                    profile.EastSteps = new[] { 4, 5, 5 };
                    profile.WestSteps = new[] { 4, 5, 5 };
                    break;
                case CastleStructureVariant.DiamondDiagonalNorthEast:
                    ApplyDiagonalBias(profile, true, true, random);
                    break;
                case CastleStructureVariant.DiamondDiagonalSouthEast:
                    ApplyDiagonalBias(profile, false, true, random);
                    break;
                case CastleStructureVariant.DiamondDiagonalSouthWest:
                    ApplyDiagonalBias(profile, false, false, random);
                    break;
                case CastleStructureVariant.DiamondDiagonalNorthWest:
                    ApplyDiagonalBias(profile, true, false, random);
                    break;
                case CastleStructureVariant.DiamondStaggeredClockwise:
                    profile.NorthSteps = new[] { 6, 4, 5 };
                    profile.EastSteps = new[] { 4, 6, 5 };
                    profile.SouthSteps = new[] { 5, 6, 4 };
                    profile.WestSteps = new[] { 5, 4, 6 };
                    break;
                case CastleStructureVariant.DiamondStaggeredCounterClockwise:
                    profile.NorthSteps = new[] { 4, 6, 5 };
                    profile.EastSteps = new[] { 6, 4, 5 };
                    profile.SouthSteps = new[] { 5, 4, 6 };
                    profile.WestSteps = new[] { 5, 6, 4 };
                    break;
            }

            return profile;
        }

        private static int[] BuildBalancedSteps(System.Random random)
        {
            return random.Next(2) == 0
                ? new[] { 5, 4, 6 }
                : new[] { 5, 6, 4 };
        }

        private static void ApplyDiagonalBias(
            DiamondShellProfile profile,
            bool north,
            bool east,
            System.Random random)
        {
            var towardNorth = new[] { 6, 5, 5 };
            var towardEast = new[] { 6, 5, 5 };
            var awayNorth = BuildAwaySteps(random);
            var awayEast = BuildAwaySteps(random);
            profile.NorthSteps = north ? towardNorth : awayNorth;
            profile.SouthSteps = north ? awayNorth : towardNorth;
            profile.EastSteps = east ? towardEast : awayEast;
            profile.WestSteps = east ? awayEast : towardEast;
        }

        private static int[] BuildAwaySteps(System.Random random)
        {
            return random.Next(2) == 0
                ? new[] { 4, 4, 5 }
                : new[] { 4, 5, 4 };
        }

        private static int SumStepsBefore(IReadOnlyList<int> steps, int defenseRing)
        {
            var result = 0;
            for (var index = 0; index < defenseRing - 1; index++)
            {
                result += steps[index];
            }

            return result;
        }

        private static int SumStepsThrough(IReadOnlyList<int> steps, int defenseRing)
        {
            return SumStepsBefore(steps, defenseRing) + steps[defenseRing - 1];
        }

        private static void AddDiamondAxisPair(
            CastleGenerationRules rules,
            System.Random random,
            ICollection<CompartmentDraft> drafts,
            string idPrefix,
            RectInt axisBounds,
            bool splitAlongX,
            CastleCompartmentRole role,
            int defenseRing,
            bool reverseSegments)
        {
            var totalLength = splitAlongX ? axisBounds.width : axisBounds.height;
            var segmentSizes = BuildDiamondAxisSegmentSizes(totalLength, random);
            if (reverseSegments)
            {
                segmentSizes.Reverse();
            }

            var cursor = splitAlongX ? axisBounds.xMin : axisBounds.yMin;
            for (var index = 0; index < segmentSizes.Count; index++)
            {
                var segmentSize = segmentSizes[index];
                var bounds = splitAlongX
                    ? new RectInt(cursor, axisBounds.yMin, segmentSize, axisBounds.height)
                    : new RectInt(axisBounds.xMin, cursor, axisBounds.width, segmentSize);
                AddRegularDraft(
                    rules,
                    drafts,
                    $"{idPrefix}_{index + 1:00}",
                    bounds,
                    role,
                    defenseRing);
                cursor += segmentSize - 1;
            }
        }

        private static List<int> BuildDiamondAxisSegmentSizes(int totalLength, System.Random random)
        {
            const int minimumSize = 5;
            const int maximumSize = 8;
            var sharedWallLength = totalLength + 1;
            var first = random.Next(minimumSize, maximumSize + 1);
            var second = sharedWallLength - first;
            if (second < minimumSize || second > maximumSize)
            {
                throw new InvalidOperationException($"길이 {totalLength}를 마름모 축 격실 둘로 분할할 수 없습니다.");
            }

            return new List<int> { first, second };
        }

        private static void BuildCentralCompartmentFortress(
            CastleGenerationRules rules,
            System.Random random,
            CompartmentDraft core,
            ICollection<CompartmentDraft> drafts)
        {
            var northDepth = random.Next(8, 11);
            var eastDepth = random.Next(8, 11);
            var southDepth = random.Next(8, 11);
            var westDepth = random.Next(8, 11);
            CastleGenerationRules.ResolveCompartmentCountRange(
                (int)CastleDefenseLayerPreset.Double,
                out var minimumCount,
                out var maximumCount);
            var target = random.Next(minimumCount, maximumCount + 1);
            var splitDirections = new[] { "n", "e", "s", "w" }.ToList();
            Shuffle(splitDirections, random);
            var splitCount = target - 8;
            var splitSet = new HashSet<string>(splitDirections.Take(splitCount), StringComparer.Ordinal);

            AddAxisDrafts(
                rules,
                drafts,
                "inner_n",
                new RectInt(core.Bounds.xMin, core.Bounds.yMax - 1, core.Bounds.width, northDepth),
                true,
                splitSet.Contains("n"));
            AddAxisDrafts(
                rules,
                drafts,
                "inner_e",
                new RectInt(core.Bounds.xMax - 1, core.Bounds.yMin, eastDepth, core.Bounds.height),
                false,
                splitSet.Contains("e"));
            AddAxisDrafts(
                rules,
                drafts,
                "inner_s",
                new RectInt(core.Bounds.xMin, core.Bounds.yMin - (southDepth - 1), core.Bounds.width, southDepth),
                true,
                splitSet.Contains("s"));
            AddAxisDrafts(
                rules,
                drafts,
                "inner_w",
                new RectInt(core.Bounds.xMin - (westDepth - 1), core.Bounds.yMin, westDepth, core.Bounds.height),
                false,
                splitSet.Contains("w"));

            AddCorner(
                rules, random, drafts, "inner_nw", core.Bounds.xMin, core.Bounds.yMax - 1,
                westDepth, northDepth, true, true);
            AddCorner(
                rules, random, drafts, "inner_ne", core.Bounds.xMax - 1, core.Bounds.yMax - 1,
                eastDepth, northDepth, false, true);
            AddCorner(
                rules, random, drafts, "inner_sw", core.Bounds.xMin, core.Bounds.yMin,
                westDepth, southDepth, true, false);
            AddCorner(
                rules, random, drafts, "inner_se", core.Bounds.xMax - 1, core.Bounds.yMin,
                eastDepth, southDepth, false, false);

            if (drafts.Count - 1 != target)
            {
                throw new InvalidOperationException($"중앙 격실 요새의 목표 격실 수가 맞지 않습니다: {drafts.Count - 1}/{target}");
            }
        }

        private static void BuildNestedCompartmentFortress(
            CastleGenerationRules rules,
            System.Random random,
            CompartmentDraft core,
            int defenseLayerCount,
            ICollection<CompartmentDraft> drafts)
        {
            var innerEnvelope = core.Bounds;
            var thicknessProfile = BuildNestedRingThicknessProfile(random, defenseLayerCount);
            for (var defenseRing = 1; defenseRing < defenseLayerCount; defenseRing++)
            {
                var profileIndex = defenseRing - 1;
                innerEnvelope = AddNestedDefenseRing(
                    rules,
                    random,
                    drafts,
                    innerEnvelope,
                    defenseRing,
                    thicknessProfile.North[profileIndex],
                    thicknessProfile.East[profileIndex],
                    thicknessProfile.South[profileIndex],
                    thicknessProfile.West[profileIndex],
                    defenseRing == defenseLayerCount - 1);
            }

            CastleGenerationRules.ResolveCompartmentCountRange(
                defenseLayerCount,
                out var minimumCount,
                out var maximumCount);
            var regularCount = drafts.Count - 1;
            if (regularCount < minimumCount || regularCount > maximumCount)
            {
                throw new InvalidOperationException(
                    $"{defenseLayerCount}중벽 요새의 격실 수가 맞지 않습니다: {regularCount}/{minimumCount}~{maximumCount}");
            }
        }

        private static NestedRingThicknessProfile BuildNestedRingThicknessProfile(
            System.Random random,
            int defenseLayerCount)
        {
            var ringCount = defenseLayerCount - 1;
            var horizontalMajor = random.Next(2) == 0;
            var majorHigh = defenseLayerCount == (int)CastleDefenseLayerPreset.Triple ? 14 : 16;
            var majorLow = majorHigh - 1;
            var minorHigh = defenseLayerCount == (int)CastleDefenseLayerPreset.Triple ? 10 : 14;
            var minorLow = minorHigh - 1;
            var positiveMajorIsHigh = random.Next(2) == 0;
            var positiveMinorIsHigh = random.Next(2) == 0;

            var northTotal = horizontalMajor
                ? (positiveMinorIsHigh ? minorHigh : minorLow)
                : (positiveMajorIsHigh ? majorHigh : majorLow);
            var southTotal = horizontalMajor
                ? (positiveMinorIsHigh ? minorLow : minorHigh)
                : (positiveMajorIsHigh ? majorLow : majorHigh);
            var eastTotal = horizontalMajor
                ? (positiveMajorIsHigh ? majorHigh : majorLow)
                : (positiveMinorIsHigh ? minorHigh : minorLow);
            var westTotal = horizontalMajor
                ? (positiveMajorIsHigh ? majorLow : majorHigh)
                : (positiveMinorIsHigh ? minorLow : minorHigh);

            return new NestedRingThicknessProfile
            {
                North = BuildRingSideThicknesses(northTotal, ringCount, random),
                East = BuildRingSideThicknesses(eastTotal, ringCount, random),
                South = BuildRingSideThicknesses(southTotal, ringCount, random),
                West = BuildRingSideThicknesses(westTotal, ringCount, random)
            };
        }

        private static int[] BuildRingSideThicknesses(
            int totalExpansion,
            int ringCount,
            System.Random random)
        {
            const int minimumExpansion = 4;
            const int maximumExpansion = 7;
            var expansions = Enumerable.Repeat(minimumExpansion, ringCount).ToArray();
            var remaining = totalExpansion - minimumExpansion * ringCount;
            if (remaining < 0 || totalExpansion > maximumExpansion * ringCount)
            {
                throw new InvalidOperationException(
                    $"{ringCount}개 링에 총 확장 {totalExpansion}셀을 분배할 수 없습니다.");
            }

            if (remaining > 0)
            {
                expansions[ringCount - 1]++;
                remaining--;
            }

            while (remaining > 0)
            {
                var available = Enumerable.Range(0, ringCount)
                    .Where(index => expansions[index] < maximumExpansion)
                    .ToArray();
                var selected = available[random.Next(available.Length)];
                expansions[selected]++;
                remaining--;
            }

            return expansions.Select(value => value + 1).ToArray();
        }

        private static RectInt AddNestedDefenseRing(
            CastleGenerationRules rules,
            System.Random random,
            ICollection<CompartmentDraft> drafts,
            RectInt innerEnvelope,
            int defenseRing,
            int northThickness,
            int eastThickness,
            int southThickness,
            int westThickness,
            bool steppedOuterEdge)
        {
            var outerEnvelope = new RectInt(
                innerEnvelope.xMin - (westThickness - 1),
                innerEnvelope.yMin - (southThickness - 1),
                innerEnvelope.width + westThickness + eastThickness - 2,
                innerEnvelope.height + southThickness + northThickness - 2);
            var role = defenseRing == 1
                ? CastleCompartmentRole.InnerRing
                : CastleCompartmentRole.OuterRing;
            var prefix = $"ring_{defenseRing:00}";

            AddRegularDraft(
                rules,
                drafts,
                prefix + "_nw",
                new RectInt(outerEnvelope.xMin, innerEnvelope.yMax - 1, westThickness, northThickness),
                role,
                defenseRing);
            AddRegularDraft(
                rules,
                drafts,
                prefix + "_ne",
                new RectInt(innerEnvelope.xMax - 1, innerEnvelope.yMax - 1, eastThickness, northThickness),
                role,
                defenseRing);
            AddRegularDraft(
                rules,
                drafts,
                prefix + "_sw",
                new RectInt(outerEnvelope.xMin, outerEnvelope.yMin, westThickness, southThickness),
                role,
                defenseRing);
            AddRegularDraft(
                rules,
                drafts,
                prefix + "_se",
                new RectInt(innerEnvelope.xMax - 1, outerEnvelope.yMin, eastThickness, southThickness),
                role,
                defenseRing);

            AddSegmentedSide(
                rules,
                random,
                drafts,
                prefix + "_n",
                innerEnvelope,
                RingSide.North,
                northThickness,
                defenseRing,
                role,
                steppedOuterEdge);
            AddSegmentedSide(
                rules,
                random,
                drafts,
                prefix + "_e",
                innerEnvelope,
                RingSide.East,
                eastThickness,
                defenseRing,
                role,
                steppedOuterEdge);
            AddSegmentedSide(
                rules,
                random,
                drafts,
                prefix + "_s",
                innerEnvelope,
                RingSide.South,
                southThickness,
                defenseRing,
                role,
                steppedOuterEdge);
            AddSegmentedSide(
                rules,
                random,
                drafts,
                prefix + "_w",
                innerEnvelope,
                RingSide.West,
                westThickness,
                defenseRing,
                role,
                steppedOuterEdge);

            return outerEnvelope;
        }

        private static void AddSegmentedSide(
            CastleGenerationRules rules,
            System.Random random,
            ICollection<CompartmentDraft> drafts,
            string idPrefix,
            RectInt innerEnvelope,
            RingSide side,
            int sideThickness,
            int defenseRing,
            CastleCompartmentRole role,
            bool steppedOuterEdge,
            bool reverseSegments = false)
        {
            var splitAlongX = side == RingSide.North || side == RingSide.South;
            var sideLength = splitAlongX ? innerEnvelope.width : innerEnvelope.height;
            var segmentCount = defenseRing;
            var segmentSizes = BuildSegmentSizes(sideLength, segmentCount, random);
            if (reverseSegments)
            {
                segmentSizes.Reverse();
            }

            var cursor = splitAlongX ? innerEnvelope.xMin : innerEnvelope.yMin;
            var maximumInset = steppedOuterEdge ? Mathf.Min(2, sideThickness - 5) : 0;
            var requiredStepIndex = maximumInset > 0 ? random.Next(segmentSizes.Count) : -1;
            var requiredFullIndex = requiredStepIndex >= 0
                ? (requiredStepIndex + 1) % segmentSizes.Count
                : -1;
            for (var index = 0; index < segmentSizes.Count; index++)
            {
                var segmentSize = segmentSizes[index];
                var inset = maximumInset <= 0
                    ? 0
                    : index == requiredStepIndex
                        ? maximumInset
                        : index == requiredFullIndex
                            ? 0
                            : random.Next(0, maximumInset + 1);
                var thickness = sideThickness - inset;
                RectInt bounds;
                switch (side)
                {
                    case RingSide.North:
                        bounds = new RectInt(cursor, innerEnvelope.yMax - 1, segmentSize, thickness);
                        break;
                    case RingSide.East:
                        bounds = new RectInt(innerEnvelope.xMax - 1, cursor, thickness, segmentSize);
                        break;
                    case RingSide.South:
                        bounds = new RectInt(
                            cursor,
                            innerEnvelope.yMin - (thickness - 1),
                            segmentSize,
                            thickness);
                        break;
                    case RingSide.West:
                        bounds = new RectInt(
                            innerEnvelope.xMin - (thickness - 1),
                            cursor,
                            thickness,
                            segmentSize);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(side), side, null);
                }

                AddRegularDraft(
                    rules,
                    drafts,
                    $"{idPrefix}_{index + 1:00}",
                    bounds,
                    role,
                    defenseRing);
                cursor += segmentSize - 1;
            }
        }

        private static List<int> BuildSegmentSizes(
            int totalLength,
            int segmentCount,
            System.Random random)
        {
            const int minimumSize = 6;
            const int maximumSize = 14;
            var remaining = totalLength + segmentCount - 1;
            var result = new List<int>(segmentCount);
            for (var index = 0; index < segmentCount; index++)
            {
                var remainingSegments = segmentCount - index - 1;
                var minimum = Mathf.Max(minimumSize, remaining - maximumSize * remainingSegments);
                var maximum = Mathf.Min(maximumSize, remaining - minimumSize * remainingSegments);
                if (minimum > maximum)
                {
                    throw new InvalidOperationException(
                        $"길이 {totalLength}를 {segmentCount}개 정식 격실로 분할할 수 없습니다.");
                }

                var size = remainingSegments == 0
                    ? remaining
                    : random.Next(minimum, maximum + 1);
                result.Add(size);
                remaining -= size;
            }

            return result;
        }

        private static void AddAxisDrafts(
            CastleGenerationRules rules,
            ICollection<CompartmentDraft> drafts,
            string id,
            RectInt bounds,
            bool splitAlongX,
            bool split)
        {
            if (!split)
            {
                AddRegularDraft(rules, drafts, id, bounds, CastleCompartmentRole.InnerRing);
                return;
            }

            const int firstSize = 6;
            if (splitAlongX)
            {
                AddRegularDraft(
                    rules,
                    drafts,
                    id + "_a",
                    new RectInt(bounds.xMin, bounds.yMin, firstSize, bounds.height),
                    CastleCompartmentRole.InnerRing);
                AddRegularDraft(
                    rules,
                    drafts,
                    id + "_b",
                    new RectInt(bounds.xMin + firstSize - 1, bounds.yMin, bounds.width - firstSize + 1, bounds.height),
                    CastleCompartmentRole.InnerRing);
                return;
            }

            AddRegularDraft(
                rules,
                drafts,
                id + "_a",
                new RectInt(bounds.xMin, bounds.yMin, bounds.width, firstSize),
                CastleCompartmentRole.InnerRing);
            AddRegularDraft(
                rules,
                drafts,
                id + "_b",
                new RectInt(bounds.xMin, bounds.yMin + firstSize - 1, bounds.width, bounds.height - firstSize + 1),
                CastleCompartmentRole.InnerRing);
        }

        private static CompartmentDraft AddCorner(
            CastleGenerationRules rules,
            System.Random random,
            ICollection<CompartmentDraft> drafts,
            string id,
            int anchorX,
            int anchorZ,
            int maximumWidth,
            int maximumHeight,
            bool extendsWest,
            bool extendsNorth)
        {
            var width = random.Next(6, maximumWidth + 1);
            var height = random.Next(6, maximumHeight + 1);
            var x = extendsWest ? anchorX - (width - 1) : anchorX;
            var z = extendsNorth ? anchorZ : anchorZ - (height - 1);
            return AddRegularDraft(
                rules,
                drafts,
                id,
                new RectInt(x, z, width, height),
                CastleCompartmentRole.InnerRing);
        }

        private static CompartmentDraft AddRegularDraft(
            CastleGenerationRules rules,
            ICollection<CompartmentDraft> drafts,
            string id,
            RectInt bounds,
            CastleCompartmentRole role,
            int defenseRing = 1)
        {
            if (!TryAddRegularDraft(rules, drafts, id, bounds, role, defenseRing, out var result))
            {
                throw new InvalidOperationException($"격실을 배치할 수 없습니다: {id} {bounds}");
            }

            return result;
        }

        private static bool TryAddRegularDraft(
            CastleGenerationRules rules,
            ICollection<CompartmentDraft> drafts,
            string id,
            RectInt bounds,
            CastleCompartmentRole role,
            int defenseRing,
            out CompartmentDraft result)
        {
            result = null;
            if (!CastleSpatialContract.Contains(rules.BuildableBounds, bounds) ||
                !IsCompatibleWithExisting(bounds, drafts))
            {
                return false;
            }

            var template = ResolveRegularTemplate(rules, bounds);
            if (template == null)
            {
                return false;
            }

            result = new CompartmentDraft
            {
                Id = id,
                Template = template,
                Bounds = bounds,
                Role = role,
                DefenseRing = Mathf.Max(1, defenseRing)
            };
            drafts.Add(result);
            return true;
        }

        private static CastleDistrictTemplate ResolveRegularTemplate(
            CastleGenerationRules rules,
            RectInt bounds)
        {
            return rules.EnumerateRegularTemplates()
                .Where(value => value.SupportsSize(bounds.width, bounds.height))
                .OrderBy(value =>
                    (value.MaximumWidth - value.MinimumWidth + 1) *
                    (value.MaximumHeight - value.MinimumHeight + 1))
                .ThenByDescending(value => value.SelectionWeight)
                .FirstOrDefault();
        }

        private static List<CastleCompartmentData> BuildCompartmentData(IReadOnlyList<CompartmentDraft> drafts)
        {
            var result = new List<CastleCompartmentData>(drafts.Count);
            foreach (var draft in drafts)
            {
                var connections = drafts
                    .Where(other => !ReferenceEquals(other, draft) && SharedBoundaryCellCount(draft, other) >= 2)
                    .Select(other => other.Id)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();
                result.Add(new CastleCompartmentData(
                    draft.Id,
                    draft.Template.TemplateId,
                    draft.Role,
                    draft.DefenseRing,
                    draft.Bounds,
                    draft.Template.WallLayers,
                    connections,
                    draft.FootprintCells));
            }

            return result;
        }

        private static Dictionary<Vector2Int, WallDraft> BuildWallDrafts(
            CastleGenerationRules rules,
            IReadOnlyList<CompartmentDraft> drafts,
            System.Random random)
        {
            var result = new Dictionary<Vector2Int, WallDraft>();
            foreach (var draft in drafts)
            {
                AddWallBoundary(result, draft);
            }

            var maximumBaseTier = Mathf.Max(rules.MinimumWallTier, rules.MaximumWallTier - 1);
            var baseTier = random.Next(rules.MinimumWallTier, maximumBaseTier + 1);
            ClassifyWallTopology(rules, drafts, result, baseTier);
            return result;
        }

        private static void AddWallBoundary(
            IDictionary<Vector2Int, WallDraft> walls,
            CompartmentDraft draft)
        {
            if (draft.FootprintCells == null || draft.FootprintCells.Count == 0)
            {
                AddWallRing(walls, draft.Bounds, draft.Id, draft.Template.TemplateId);
                return;
            }

            foreach (var cell in draft.FootprintCells.Where(cell => IsFootprintBoundaryCell(draft.FootprintCells, cell)))
            {
                AddWallCell(walls, cell, draft.Id, draft.Template.TemplateId);
            }
        }

        private static void AddWallRing(
            IDictionary<Vector2Int, WallDraft> walls,
            RectInt bounds,
            string ownerId,
            string templateId)
        {
            for (var x = bounds.xMin; x < bounds.xMax; x++)
            {
                AddWallCell(walls, new Vector2Int(x, bounds.yMin), ownerId, templateId);
                AddWallCell(walls, new Vector2Int(x, bounds.yMax - 1), ownerId, templateId);
            }

            for (var z = bounds.yMin + 1; z < bounds.yMax - 1; z++)
            {
                AddWallCell(walls, new Vector2Int(bounds.xMin, z), ownerId, templateId);
                AddWallCell(walls, new Vector2Int(bounds.xMax - 1, z), ownerId, templateId);
            }
        }

        private static void AddWallCell(
            IDictionary<Vector2Int, WallDraft> walls,
            Vector2Int cell,
            string ownerId,
            string templateId)
        {
            if (!walls.TryGetValue(cell, out var wall))
            {
                wall = new WallDraft { TemplateId = templateId };
                walls.Add(cell, wall);
            }

            wall.OwnerIds.Add(ownerId);
        }

        private static void ClassifyWallTopology(
            CastleGenerationRules rules,
            IReadOnlyList<CompartmentDraft> compartments,
            IReadOnlyDictionary<Vector2Int, WallDraft> walls,
            int baseTier)
        {
            var regionByCell = BuildOpenRegionMap(rules.GridWidth, rules.GridHeight, walls, out var regionCount);
            var regionDepths = BuildRegionDepths(rules.GridWidth, rules.GridHeight, walls, regionByCell, regionCount);
            var roleByOwner = compartments.ToDictionary(value => value.Id, value => value.Role, StringComparer.Ordinal);
            var ringByOwner = compartments.ToDictionary(value => value.Id, value => value.DefenseRing, StringComparer.Ordinal);
            foreach (var pair in walls)
            {
                var adjacentDepths = CollectAdjacentRegionDepths(pair.Key, regionByCell, regionDepths);
                if (adjacentDepths.Count == 0)
                {
                    throw new InvalidOperationException($"성벽 셀 주변의 개방 영역을 찾지 못했습니다: {pair.Key}");
                }

                var layer = adjacentDepths.Min();
                pair.Value.DefenseLayer = layer;
                pair.Value.WallBand = ResolveWallBand(pair.Value, layer, roleByOwner, ringByOwner);
                var tier = Mathf.Clamp(
                    baseTier + layer,
                    rules.MinimumWallTier,
                    rules.MaximumWallTier);
                if (pair.Value.WallBand == CastleWallBand.CoreDefense)
                {
                    tier = Mathf.Max(tier, rules.PalaceWallTier);
                }

                pair.Value.WallTier = tier;
            }

            AssignWallLines(walls);
        }

        private static int[,] BuildOpenRegionMap(
            int width,
            int height,
            IReadOnlyDictionary<Vector2Int, WallDraft> walls,
            out int regionCount)
        {
            var result = new int[width, height];
            Fill(result, -1);
            regionCount = 0;
            for (var x = 0; x < width; x++)
            {
                for (var z = 0; z < height; z++)
                {
                    var start = new Vector2Int(x, z);
                    if (walls.ContainsKey(start) || result[x, z] >= 0)
                    {
                        continue;
                    }

                    var queue = new Queue<Vector2Int>();
                    result[x, z] = regionCount;
                    queue.Enqueue(start);
                    while (queue.Count > 0)
                    {
                        var cell = queue.Dequeue();
                        foreach (var neighbor in EnumerateNeighbors(cell))
                        {
                            if (neighbor.x < 0 || neighbor.y < 0 || neighbor.x >= width || neighbor.y >= height ||
                                walls.ContainsKey(neighbor) || result[neighbor.x, neighbor.y] >= 0)
                            {
                                continue;
                            }

                            result[neighbor.x, neighbor.y] = regionCount;
                            queue.Enqueue(neighbor);
                        }
                    }

                    regionCount++;
                }
            }

            return result;
        }

        private static int[] BuildRegionDepths(
            int width,
            int height,
            IReadOnlyDictionary<Vector2Int, WallDraft> walls,
            int[,] regionByCell,
            int regionCount)
        {
            var adjacency = Enumerable.Range(0, regionCount).Select(_ => new HashSet<int>()).ToArray();
            foreach (var cell in walls.Keys)
            {
                ConnectOppositeRegions(cell + Vector2Int.up, cell + Vector2Int.down);
                ConnectOppositeRegions(cell + Vector2Int.left, cell + Vector2Int.right);
            }

            var depths = Enumerable.Repeat(int.MaxValue, regionCount).ToArray();
            var queue = new Queue<int>();
            for (var x = 0; x < width; x++)
            {
                EnqueueOutside(new Vector2Int(x, 0));
                EnqueueOutside(new Vector2Int(x, height - 1));
            }

            for (var z = 1; z < height - 1; z++)
            {
                EnqueueOutside(new Vector2Int(0, z));
                EnqueueOutside(new Vector2Int(width - 1, z));
            }

            while (queue.Count > 0)
            {
                var region = queue.Dequeue();
                foreach (var neighbor in adjacency[region])
                {
                    if (depths[neighbor] <= depths[region] + 1)
                    {
                        continue;
                    }

                    depths[neighbor] = depths[region] + 1;
                    queue.Enqueue(neighbor);
                }
            }

            if (depths.Any(value => value == int.MaxValue))
            {
                throw new InvalidOperationException("완성 성벽망의 모든 내부 영역에 외곽 침투 깊이를 부여하지 못했습니다.");
            }

            return depths;

            void ConnectOppositeRegions(Vector2Int firstCell, Vector2Int secondCell)
            {
                var first = ResolveRegion(firstCell);
                var second = ResolveRegion(secondCell);
                if (first < 0 || second < 0 || first == second)
                {
                    return;
                }

                adjacency[first].Add(second);
                adjacency[second].Add(first);
            }

            int ResolveRegion(Vector2Int cell)
            {
                return cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height
                    ? regionByCell[cell.x, cell.y]
                    : -1;
            }

            void EnqueueOutside(Vector2Int cell)
            {
                var region = ResolveRegion(cell);
                if (region >= 0 && depths[region] == int.MaxValue)
                {
                    depths[region] = 0;
                    queue.Enqueue(region);
                }
            }
        }

        private static List<int> CollectAdjacentRegionDepths(
            Vector2Int wallCell,
            int[,] regionByCell,
            IReadOnlyList<int> regionDepths)
        {
            var result = new HashSet<int>();
            for (var deltaX = -1; deltaX <= 1; deltaX++)
            {
                for (var deltaZ = -1; deltaZ <= 1; deltaZ++)
                {
                    if (deltaX == 0 && deltaZ == 0)
                    {
                        continue;
                    }

                    var x = wallCell.x + deltaX;
                    var z = wallCell.y + deltaZ;
                    if (x < 0 || z < 0 || x >= regionByCell.GetLength(0) || z >= regionByCell.GetLength(1))
                    {
                        continue;
                    }

                    var region = regionByCell[x, z];
                    if (region >= 0)
                    {
                        result.Add(regionDepths[region]);
                    }
                }
            }

            return result.OrderBy(value => value).ToList();
        }

        private static CastleWallBand ResolveWallBand(
            WallDraft wall,
            int defenseLayer,
            IReadOnlyDictionary<string, CastleCompartmentRole> roleByOwner,
            IReadOnlyDictionary<string, int> ringByOwner)
        {
            if (defenseLayer == 0)
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
            var ownerRings = wall.OwnerIds
                .Where(ringByOwner.ContainsKey)
                .Select(owner => ringByOwner[owner])
                .Distinct()
                .ToArray();
            return wall.OwnerIds.Count >= 2 && ownerRoles.Length == 1 && ownerRings.Length == 1
                ? CastleWallBand.Partition
                : CastleWallBand.InnerDefense;
        }

        private static void AssignWallLines(IReadOnlyDictionary<Vector2Int, WallDraft> walls)
        {
            var unvisited = new HashSet<Vector2Int>(walls.Keys);
            var lineSerial = 0;
            while (unvisited.Count > 0)
            {
                var start = unvisited.OrderBy(value => value.y).ThenBy(value => value.x).First();
                var signature = WallTopologySignature(walls[start]);
                var lineId = $"wall_line_{lineSerial++:000}";
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(start);
                unvisited.Remove(start);
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    walls[cell].LineId = lineId;
                    foreach (var neighbor in EnumerateNeighbors(cell))
                    {
                        if (!unvisited.Contains(neighbor) ||
                            !walls.TryGetValue(neighbor, out var neighborWall) ||
                            !string.Equals(signature, WallTopologySignature(neighborWall), StringComparison.Ordinal))
                        {
                            continue;
                        }

                        unvisited.Remove(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        private static string WallTopologySignature(WallDraft wall)
        {
            return $"{(int)wall.WallBand}:{wall.DefenseLayer}:{wall.WallTier}";
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
                AddPlacement(
                    new CastlePlacementData(
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
                        pair.Value.LineId),
                    occupied,
                    placements);
            }
        }

        private static CastleWallNeighborMask ResolveNeighborMask(
            IReadOnlyDictionary<Vector2Int, WallDraft> walls,
            Vector2Int cell)
        {
            var result = CastleWallNeighborMask.None;
            if (walls.ContainsKey(cell + Vector2Int.up)) result |= CastleWallNeighborMask.North;
            if (walls.ContainsKey(cell + Vector2Int.right)) result |= CastleWallNeighborMask.East;
            if (walls.ContainsKey(cell + Vector2Int.down)) result |= CastleWallNeighborMask.South;
            if (walls.ContainsKey(cell + Vector2Int.left)) result |= CastleWallNeighborMask.West;
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
                var interiorCellCount = CountInteriorCells(draft);
                var desiredStructures = Mathf.Clamp(
                    interiorCellCount / 24,
                    draft.Template.MinimumInteriorPlacements,
                    draft.Template.MaximumInteriorPlacements);
                var placedStructures = 0;
                if (lootByDistrict.TryGetValue(draft.Id, out var lootKind) &&
                    TryPlaceStructure(
                        rules, random, draft, interior, CastlePlacementKind.LootBuilding, lootKind, 3,
                        occupied, placements, ref placementSerial))
                {
                    placedStructures++;
                }

                while (placedStructures < desiredStructures)
                {
                    var kind = random.Next(100) < 55
                        ? CastlePlacementKind.Building
                        : CastlePlacementKind.DefenseBuilding;
                    var maximumSize = Mathf.Min(
                        CastleSpatialContract.MaximumBuildingSize,
                        Mathf.Min(interior.width, interior.height));
                    var preferredSize = random.Next(Mathf.Min(2, maximumSize), maximumSize + 1);
                    if (!TryPlaceStructure(
                            rules, random, draft, interior, kind, CastleLootKind.None, preferredSize,
                            occupied, placements, ref placementSerial))
                    {
                        break;
                    }

                    placedStructures++;
                }

                var defenderCount = interiorCellCount >= 48 && random.NextDouble() < 0.65d ? 2 : 1;
                for (var defenderIndex = 0; defenderIndex < defenderCount; defenderIndex++)
                {
                    var defenderCells = CollectFreeCells(draft, interior, occupied);
                    if (defenderCells.Count == 0 || random.NextDouble() >= 0.58d)
                    {
                        break;
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
        }

        private static void PopulatePalaceCore(
            CastleGenerationRules rules,
            System.Random random,
            CompartmentDraft core,
            int[,] occupied,
            ICollection<CastlePlacementData> placements,
            ref int placementSerial)
        {
            var interior = Shrink(core.Bounds, core.Template.WallLayers);
            for (var index = 0; index < 4; index++)
            {
                if (!TryPlaceStructure(
                        rules,
                        random,
                        core,
                        interior,
                        CastlePlacementKind.DefenseBuilding,
                        CastleLootKind.None,
                        2,
                        occupied,
                        placements,
                        ref placementSerial))
                {
                    break;
                }
            }

            for (var index = 0; index < 2; index++)
            {
                var cells = CollectFreeCells(core, interior, occupied);
                if (cells.Count == 0)
                {
                    break;
                }

                var cell = cells[random.Next(cells.Count)];
                AddPlacement(
                    new CastlePlacementData(
                        NextId("core_defender", ref placementSerial),
                        core.Id,
                        core.Template.TemplateId,
                        CastlePlacementKind.Defender,
                        CastleLootKind.None,
                        cell.x,
                        cell.y,
                        1,
                        1,
                        0,
                        rules.DefenderHealth,
                        0),
                    occupied,
                    placements);
            }
        }

        private static bool TryPlaceStructure(
            CastleGenerationRules rules,
            System.Random random,
            CompartmentDraft draft,
            RectInt interior,
            CastlePlacementKind kind,
            CastleLootKind lootKind,
            int preferredSize,
            int[,] occupied,
            ICollection<CastlePlacementData> placements,
            ref int placementSerial)
        {
            for (var size = Mathf.Min(preferredSize, CastleSpatialContract.MaximumBuildingSize); size >= 1; size--)
            {
                var candidates = new List<RectInt>();
                for (var x = interior.xMin; x <= interior.xMax - size; x++)
                {
                    for (var z = interior.yMin; z <= interior.yMax - size; z++)
                    {
                        var bounds = new RectInt(x, z, size, size);
                        if (IsInsideCompartmentInterior(draft, bounds) &&
                            IsPlacementAreaFree(bounds, occupied) &&
                            HasStructureClearance(bounds, draft.Id, placements))
                        {
                            candidates.Add(bounds);
                        }
                    }
                }

                if (candidates.Count == 0)
                {
                    continue;
                }

                var selected = candidates[random.Next(candidates.Count)];
                var health = kind == CastlePlacementKind.LootBuilding
                    ? rules.LootBuildingHealth
                    : kind == CastlePlacementKind.DefenseBuilding
                        ? rules.DefenseBuildingHealth
                        : rules.BuildingHealth;
                AddPlacement(
                    new CastlePlacementData(
                        NextId(
                            kind == CastlePlacementKind.LootBuilding
                                ? "loot"
                                : kind == CastlePlacementKind.DefenseBuilding ? "defense" : "building",
                            ref placementSerial),
                        draft.Id,
                        draft.Template.TemplateId,
                        kind,
                        lootKind,
                        selected.x,
                        selected.y,
                        selected.width,
                        selected.height,
                        0,
                        health,
                        kind == CastlePlacementKind.LootBuilding
                            ? rules.ResolveRewardBudgetCost(lootKind)
                            : 0),
                    occupied,
                    placements);
                return true;
            }

            return false;
        }

        private static bool IsPlacementAreaFree(RectInt bounds, int[,] occupied)
        {
            for (var x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (var z = bounds.yMin; z < bounds.yMax; z++)
                {
                    if (occupied[x, z] >= 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool HasStructureClearance(
            RectInt bounds,
            string districtId,
            IEnumerable<CastlePlacementData> placements)
        {
            var expanded = new RectInt(bounds.xMin - 1, bounds.yMin - 1, bounds.width + 2, bounds.height + 2);
            return placements
                .Where(value => string.Equals(value.DistrictId, districtId, StringComparison.Ordinal))
                .Where(value => value.Kind != CastlePlacementKind.Wall && value.Kind != CastlePlacementKind.Defender)
                .All(value => !CastleSpatialContract.Overlaps(expanded, value.Bounds));
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
            return EnumerateFootprintCells(core)
                .Count(cell => IsDraftBoundaryCell(core, cell) &&
                               !others.Any(other =>
                                   IsDraftBoundaryCell(other, cell) ||
                                   EnumerateNeighbors(cell).Any(neighbor => IsDraftBoundaryCell(other, neighbor))));
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
                cells.UnionWith(EnumerateFootprintCells(draft));
            }

            return bounds.width * bounds.height > 0 ? cells.Count / (float)(bounds.width * bounds.height) : 0f;
        }

        private static bool IsCompatibleWithExisting(RectInt candidate, IEnumerable<CompartmentDraft> drafts)
        {
            return drafts.All(existing => IsWallOnlyIntersection(candidate, existing.Bounds));
        }

        private static bool IsFootprintCompatibleWithExisting(
            IReadOnlyCollection<Vector2Int> candidate,
            IEnumerable<CompartmentDraft> drafts)
        {
            var candidateSet = candidate as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(candidate);
            foreach (var existing in drafts)
            {
                var intersections = EnumerateFootprintCells(existing).Where(candidateSet.Contains).ToArray();
                if (intersections.Any(cell =>
                        !IsFootprintBoundaryCell(candidateSet, cell) ||
                        !IsDraftBoundaryCell(existing, cell)))
                {
                    return false;
                }
            }

            return true;
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

        private static int SharedBoundaryCellCount(CompartmentDraft left, CompartmentDraft right)
        {
            var rightBoundary = EnumerateFootprintCells(right)
                .Where(cell => IsDraftBoundaryCell(right, cell))
                .ToHashSet();
            return EnumerateFootprintCells(left)
                .Count(cell => IsDraftBoundaryCell(left, cell) && rightBoundary.Contains(cell));
        }

        private static IEnumerable<Vector2Int> EnumerateFootprintCells(CompartmentDraft draft)
        {
            if (draft.FootprintCells != null && draft.FootprintCells.Count > 0)
            {
                return draft.FootprintCells;
            }

            var cells = new List<Vector2Int>(draft.Bounds.width * draft.Bounds.height);
            for (var x = draft.Bounds.xMin; x < draft.Bounds.xMax; x++)
            {
                for (var z = draft.Bounds.yMin; z < draft.Bounds.yMax; z++)
                {
                    cells.Add(new Vector2Int(x, z));
                }
            }

            return cells;
        }

        private static bool IsDraftBoundaryCell(CompartmentDraft draft, Vector2Int cell)
        {
            return draft.FootprintCells != null && draft.FootprintCells.Count > 0
                ? IsFootprintBoundaryCell(draft.FootprintCells, cell)
                : IsPerimeterCell(draft.Bounds, cell);
        }

        private static bool IsFootprintBoundaryCell(IReadOnlyCollection<Vector2Int> footprint, Vector2Int cell)
        {
            if (!footprint.Contains(cell))
            {
                return false;
            }

            return !footprint.Contains(cell + Vector2Int.up) ||
                   !footprint.Contains(cell + Vector2Int.right) ||
                   !footprint.Contains(cell + Vector2Int.down) ||
                   !footprint.Contains(cell + Vector2Int.left);
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

        private static RectInt EncapsulateCells(IEnumerable<Vector2Int> cells)
        {
            var values = cells.ToArray();
            if (values.Length == 0)
            {
                return new RectInt();
            }

            var minX = values.Min(value => value.x);
            var minZ = values.Min(value => value.y);
            var maxX = values.Max(value => value.x) + 1;
            var maxZ = values.Max(value => value.y) + 1;
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

        private static int CountInteriorCells(CompartmentDraft draft)
        {
            if (draft.FootprintCells == null || draft.FootprintCells.Count == 0)
            {
                var interior = Shrink(draft.Bounds, draft.Template.WallLayers);
                return Mathf.Max(0, interior.width * interior.height);
            }

            return draft.FootprintCells.Count(cell => IsCustomFootprintInteriorCell(draft, cell));
        }

        private static bool IsInsideCompartmentInterior(CompartmentDraft draft, RectInt bounds)
        {
            if (draft.FootprintCells == null || draft.FootprintCells.Count == 0)
            {
                return CastleSpatialContract.Contains(Shrink(draft.Bounds, draft.Template.WallLayers), bounds);
            }

            for (var x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (var z = bounds.yMin; z < bounds.yMax; z++)
                {
                    if (!IsCustomFootprintInteriorCell(draft, new Vector2Int(x, z)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsCustomFootprintInteriorCell(CompartmentDraft draft, Vector2Int cell)
        {
            if (draft.FootprintCells == null || !draft.FootprintCells.Contains(cell))
            {
                return false;
            }

            var frontier = new HashSet<Vector2Int> { cell };
            var visited = new HashSet<Vector2Int> { cell };
            for (var layer = 0; layer < draft.Template.WallLayers; layer++)
            {
                var next = new HashSet<Vector2Int>();
                foreach (var current in frontier)
                {
                    foreach (var neighbor in EnumerateNeighbors(current))
                    {
                        if (!draft.FootprintCells.Contains(neighbor))
                        {
                            return false;
                        }

                        if (visited.Add(neighbor))
                        {
                            next.Add(neighbor);
                        }
                    }
                }

                frontier = next;
            }

            return true;
        }

        private static List<Vector2Int> CollectFreeCells(
            CompartmentDraft draft,
            RectInt bounds,
            int[,] occupied)
        {
            var result = new List<Vector2Int>();
            for (var x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (var z = bounds.yMin; z < bounds.yMax; z++)
                {
                    if (occupied[x, z] < 0 &&
                        IsInsideCompartmentInterior(draft, new RectInt(x, z, 1, 1)))
                    {
                        result.Add(new Vector2Int(x, z));
                    }
                }
            }

            return result;
        }

        private static IEnumerable<Vector2Int> EnumerateNeighbors(Vector2Int cell)
        {
            yield return cell + Vector2Int.up;
            yield return cell + Vector2Int.right;
            yield return cell + Vector2Int.down;
            yield return cell + Vector2Int.left;
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

        private static string ComputeStructureHash(
            int rulesVersion,
            int width,
            int height,
            CastleLayoutTheme theme,
            CastleStructureVariant structureVariant,
            int defenseLayerCount,
            IEnumerable<CastleCompartmentData> compartments,
            IReadOnlyDictionary<Vector2Int, WallDraft> walls)
        {
            var builder = new StringBuilder();
            builder.Append(rulesVersion).Append('|').Append(width).Append('|').Append(height).Append('|')
                .Append((int)theme).Append('|').Append((int)structureVariant).Append('|').Append(defenseLayerCount);
            foreach (var compartment in compartments
                         .OrderBy(value => value.DefenseRing)
                         .ThenBy(value => value.Bounds.x)
                         .ThenBy(value => value.Bounds.y)
                         .ThenBy(value => value.Bounds.width)
                         .ThenBy(value => value.Bounds.height))
            {
                builder.Append("|C:")
                    .Append((int)compartment.Role).Append(':')
                    .Append(compartment.DefenseRing).Append(':')
                    .Append(compartment.Bounds.x).Append(':')
                    .Append(compartment.Bounds.y).Append(':')
                    .Append(compartment.Bounds.width).Append(':')
                    .Append(compartment.Bounds.height).Append(':')
                    .Append(compartment.WallLayers);
                foreach (var cell in compartment.FootprintCells.OrderBy(value => value.y).ThenBy(value => value.x))
                {
                    builder.Append(':').Append(cell.x).Append(',').Append(cell.y);
                }
            }

            foreach (var wall in walls.OrderBy(value => value.Key.x).ThenBy(value => value.Key.y))
            {
                builder.Append("|W:")
                    .Append(wall.Key.x).Append(':')
                    .Append(wall.Key.y).Append(':')
                    .Append((int)wall.Value.WallBand).Append(':')
                    .Append(wall.Value.DefenseLayer).Append(':')
                    .Append(string.Join(",", wall.Value.OwnerIds.OrderBy(value => value, StringComparer.Ordinal)));
            }

            return ComputeSha256(builder);
        }

        private static string ComputeLayoutHash(
            int rulesVersion,
            int width,
            int height,
            CastleLayoutTheme theme,
            CastleStructureVariant structureVariant,
            int defenseLayerCount,
            IEnumerable<CastleCompartmentData> compartments,
            IEnumerable<CastlePlacementData> placements)
        {
            var builder = new StringBuilder();
            builder.Append(rulesVersion).Append('|').Append(width).Append('|').Append(height).Append('|')
                .Append((int)theme).Append('|').Append((int)structureVariant).Append('|').Append(defenseLayerCount);
            foreach (var compartment in compartments.OrderBy(value => value.CompartmentId, StringComparer.Ordinal))
            {
                builder.Append("|C:")
                    .Append(compartment.CompartmentId).Append(':')
                    .Append(compartment.TemplateId).Append(':')
                    .Append((int)compartment.Role).Append(':')
                    .Append(compartment.DefenseRing).Append(':')
                    .Append(compartment.Bounds.x).Append(':')
                    .Append(compartment.Bounds.y).Append(':')
                    .Append(compartment.Bounds.width).Append(':')
                    .Append(compartment.Bounds.height).Append(':')
                    .Append(compartment.WallLayers);
                foreach (var cell in compartment.FootprintCells.OrderBy(value => value.y).ThenBy(value => value.x))
                {
                    builder.Append(':').Append(cell.x).Append(',').Append(cell.y);
                }
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

            return ComputeSha256(builder);
        }

        private static string ComputeSha256(StringBuilder builder)
        {
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
    }
}
