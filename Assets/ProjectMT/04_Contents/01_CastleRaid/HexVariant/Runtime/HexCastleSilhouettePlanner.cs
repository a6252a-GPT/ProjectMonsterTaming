using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectMT.Contents.CastleRaidHex
{
    public sealed class HexCastleRingPath
    {
        public HexCastleRingPath(
            int defenseLayer,
            IReadOnlyList<HexCoordinates> cells,
            IReadOnlyCollection<HexCoordinates> majorTowerCells)
        {
            DefenseLayer = defenseLayer;
            Cells = cells ?? throw new ArgumentNullException(nameof(cells));
            MajorTowerCells = majorTowerCells ?? throw new ArgumentNullException(nameof(majorTowerCells));
        }

        public int DefenseLayer { get; }
        public IReadOnlyList<HexCoordinates> Cells { get; }
        public IReadOnlyCollection<HexCoordinates> MajorTowerCells { get; }
    }

    public sealed class HexCastlePartitionPath
    {
        public HexCastlePartitionPath(int direction, IReadOnlyList<HexCoordinates> cells)
            : this(direction, -1, cells)
        {
        }

        public HexCastlePartitionPath(int direction, int bandIndex, IReadOnlyList<HexCoordinates> cells)
        {
            Direction = direction;
            BandIndex = bandIndex;
            Cells = cells ?? throw new ArgumentNullException(nameof(cells));
        }

        public int Direction { get; }
        public int BandIndex { get; }
        public IReadOnlyList<HexCoordinates> Cells { get; }
    }

    public sealed class HexCastleSilhouettePlan
    {
        public HexCastleSilhouettePlan(
            HexCastleTheme theme,
            IReadOnlyList<HexCastleRingPath> rings,
            IReadOnlyList<HexCastlePartitionPath> partitions)
        {
            Theme = theme;
            Rings = rings ?? throw new ArgumentNullException(nameof(rings));
            Partitions = partitions ?? throw new ArgumentNullException(nameof(partitions));
            MaximumRadius = Rings.SelectMany(value => value.Cells)
                .Concat(Partitions.SelectMany(value => value.Cells))
                .Select(value => value.DistanceFromOrigin)
                .DefaultIfEmpty(0)
                .Max();
        }

        public HexCastleTheme Theme { get; }
        public IReadOnlyList<HexCastleRingPath> Rings { get; }
        public IReadOnlyList<HexCastlePartitionPath> Partitions { get; }
        public int MaximumRadius { get; }
    }

    public sealed class HexCastleBandTopology
    {
        public HexCastleBandTopology(
            IReadOnlyCollection<HexCoordinates> cells,
            IReadOnlyCollection<HexCoordinates> denseRow,
            IReadOnlyCollection<HexCoordinates> sparseRows)
        {
            Cells = cells ?? throw new ArgumentNullException(nameof(cells));
            DenseRow = denseRow ?? throw new ArgumentNullException(nameof(denseRow));
            SparseRows = sparseRows ?? throw new ArgumentNullException(nameof(sparseRows));
        }

        public IReadOnlyCollection<HexCoordinates> Cells { get; }
        public IReadOnlyCollection<HexCoordinates> DenseRow { get; }
        public IReadOnlyCollection<HexCoordinates> SparseRows { get; }
    }

    public static class HexCastleSilhouetteBandResolver
    {
        public static HexCastleBandTopology Resolve(
            HexCastleSilhouettePlan plan,
            int boardRadius,
            int bandIndex)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (bandIndex < 0 || bandIndex >= plan.Rings.Count - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bandIndex));
            }

            var domain = HexCoordinates.EnumerateRadius(boardRadius).ToHashSet();
            var innerWall = plan.Rings[bandIndex].Cells.ToHashSet();
            var outerWall = plan.Rings[bandIndex + 1].Cells.ToHashSet();
            var allWallCells = plan.Rings.SelectMany(value => value.Cells)
                .Concat(plan.Partitions.SelectMany(value => value.Cells))
                .ToHashSet();
            var outsideInner = ResolveOutside(domain, innerWall, boardRadius);
            var outsideOuter = ResolveOutside(domain, outerWall, boardRadius);
            var bandCells = domain
                .Where(value => outsideInner.Contains(value) &&
                                !outsideOuter.Contains(value) &&
                                !allWallCells.Contains(value))
                .ToHashSet();
            var denseRow = bandCells
                .Where(value => HexCoordinates.Directions.Any(direction =>
                    innerWall.Contains(value + direction)))
                .ToHashSet();
            var sparseRows = bandCells.Where(value => !denseRow.Contains(value)).ToHashSet();
            return new HexCastleBandTopology(bandCells, denseRow, sparseRows);
        }

        private static HashSet<HexCoordinates> ResolveOutside(
            ISet<HexCoordinates> domain,
            ISet<HexCoordinates> wall,
            int boardRadius)
        {
            var outside = new HashSet<HexCoordinates>();
            var queue = new Queue<HexCoordinates>();
            foreach (var coordinates in domain.Where(value =>
                         value.DistanceFromOrigin == boardRadius && !wall.Contains(value)))
            {
                if (outside.Add(coordinates)) queue.Enqueue(coordinates);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var direction in HexCoordinates.Directions)
                {
                    var next = current + direction;
                    if (!domain.Contains(next) || wall.Contains(next) || !outside.Add(next)) continue;
                    queue.Enqueue(next);
                }
            }

            return outside;
        }
    }

    public static class HexCastleSilhouettePlanner // 사각 테마의 생성 원리를 육각 폐곡선으로 다시 만든다
    {
        private sealed class RadialRingDraft
        {
            public HexCastleRingPath Ring;
            public HexCoordinates[] Tips;
            public HexCoordinates[] Valleys;
        }

        private sealed class SectorRingDraft
        {
            public HexCastleRingPath Ring;
            public HexCoordinates[] Axes;
            public HexCoordinates[] FirstJoints;
            public HexCoordinates[] SecondJoints;
        }

        public static readonly IReadOnlyList<HexCastleTheme> SupportedThemes = new[]
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

        public static HexCastleSilhouettePlan Build(
            HexCastleTheme theme,
            int seed,
            IReadOnlyList<int> wallRadii,
            int requiredGateSocketCountPerBand = 1)
        {
            ValidateRadii(wallRadii);
            requiredGateSocketCountPerBand = Math.Max(1, Math.Min(2, requiredGateSocketCountPerBand));
            HexCastleSilhouettePlan result;
            switch (theme)
            {
                case HexCastleTheme.CentralCompartment:
                    result = BuildCentralPlan(wallRadii);
                    break;
                case HexCastleTheme.DiamondRadial:
                    result = BuildDiamondPlan(seed, wallRadii);
                    break;
                case HexCastleTheme.CompositeCompartments:
                    result = BuildCompositePlan(seed, wallRadii);
                    break;
                case HexCastleTheme.HexHoneycomb:
                    result = BuildHoneycombPlan(seed, wallRadii);
                    break;
                case HexCastleTheme.PetalBloom:
                    result = BuildPetalPlan(seed, wallRadii);
                    break;
                case HexCastleTheme.CrystalMandala:
                    result = BuildCrystalPlan(seed, wallRadii);
                    break;
                case HexCastleTheme.FractalBastion:
                    result = BuildFractalBastionPlan(seed, wallRadii);
                    break;
                case HexCastleTheme.VoronoiCrystal:
                    result = BuildVoronoiCrystalPlan(seed, wallRadii);
                    break;
                case HexCastleTheme.IrisShutter:
                    result = BuildIrisShutterPlan(seed, wallRadii);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"{theme}은 아직 실제 육각 생성 규칙이 구현되지 않은 테마입니다.");
            }

            result = EnsureFormalGateSockets(result, seed, requiredGateSocketCountPerBand);
            ValidatePlan(result);
            return result;
        }

        public static IReadOnlyList<HexCastlePartitionPath> BuildPartitionPaths(
            HexCastleTheme theme,
            int seed,
            IReadOnlyList<int> wallRadii)
        {
            return Build(theme, seed, wallRadii).Partitions;
        }

        private static HexCastleSilhouettePlan BuildCentralPlan(IReadOnlyList<int> wallRadii)
        {
            var rings = wallRadii
                .Select((radius, index) => new HexCastleRingPath(
                    index + 1,
                    HexCoordinates.EnumerateRing(radius).ToArray(),
                    HexCoordinates.Directions.Select(direction => direction * radius).ToArray()))
                .ToArray();
            var partitions = new List<HexCastlePartitionPath>();
            for (var bandIndex = 0; bandIndex < wallRadii.Count - 1; bandIndex++)
            {
                for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
                {
                    partitions.Add(new HexCastlePartitionPath(
                        direction,
                        bandIndex,
                        BuildLine(
                            HexCoordinates.Directions[direction] * wallRadii[bandIndex],
                            HexCoordinates.Directions[direction] * wallRadii[bandIndex + 1])));
                }
            }

            return new HexCastleSilhouettePlan(
                HexCastleTheme.CentralCompartment,
                rings,
                partitions);
        }

        private static HexCastleSilhouettePlan BuildDiamondPlan(
            int seed,
            IReadOnlyList<int> wallRadii)
        {
            var drafts = wallRadii
                .Select((radius, index) => BuildRadialRing(
                    HexCastleTheme.DiamondRadial,
                    seed,
                    index,
                    radius))
                .ToArray();
            var partitions = new List<HexCastlePartitionPath>();
            for (var bandIndex = 0; bandIndex < drafts.Length - 1; bandIndex++)
            {
                var inner = drafts[bandIndex];
                var outer = drafts[bandIndex + 1];
                var straightSector = PositiveModulo(seed + bandIndex * 2, 6);
                var oppositeStraightSector = PositiveModulo(straightSector + 3, 6);
                for (var sector = 0; sector < 6; sector++)
                {
                    HexCoordinates start;
                    HexCoordinates end;
                    if (sector == straightSector || sector == oppositeStraightSector)
                    {
                        start = inner.Tips[sector];
                        end = outer.Tips[sector];
                    }
                    else if ((sector + bandIndex) % 2 == 0)
                    {
                        start = inner.Valleys[sector];
                        end = outer.Tips[sector];
                    }
                    else
                    {
                        start = inner.Tips[(sector + 1) % 6];
                        end = outer.Valleys[sector];
                    }

                    partitions.Add(new HexCastlePartitionPath(
                        sector,
                        bandIndex,
                        BuildLine(start, end)));
                }
            }

            return new HexCastleSilhouettePlan(
                HexCastleTheme.DiamondRadial,
                drafts.Select(value => value.Ring).ToArray(),
                partitions);
        }

        private static HexCastleSilhouettePlan BuildCompositePlan(
            int seed,
            IReadOnlyList<int> wallRadii)
        {
            var drafts = wallRadii
                .Select((radius, index) => BuildCompositeRing(seed, index, radius))
                .ToArray();
            var partitions = new List<HexCastlePartitionPath>();
            var phase = PositiveModulo(seed, 2);
            for (var bandIndex = 0; bandIndex < drafts.Length - 1; bandIndex++)
            {
                // 지그재그 문법은 유지하되 서로 마주 보는 두 격벽에는 직선 성문 소켓을 보장한다.
                var straightSector = PositiveModulo(seed + bandIndex, 6);
                var oppositeStraightSector = PositiveModulo(straightSector + 3, 6);
                for (var sector = 0; sector < 6; sector++)
                {
                    var useStraightSocket = sector == straightSector || sector == oppositeStraightSector;
                    var useFirstJoint = PositiveModulo(sector + bandIndex + phase, 2) == 0;
                    var start = useStraightSocket
                        ? drafts[bandIndex].Axes[sector]
                        : useFirstJoint
                            ? drafts[bandIndex].FirstJoints[sector]
                            : drafts[bandIndex].SecondJoints[sector];
                    var end = useStraightSocket
                        ? drafts[bandIndex + 1].Axes[sector]
                        : useFirstJoint
                            ? drafts[bandIndex + 1].SecondJoints[sector]
                            : drafts[bandIndex + 1].FirstJoints[sector];
                    partitions.Add(new HexCastlePartitionPath(
                        sector,
                        bandIndex,
                        BuildLine(start, end)));
                }
            }

            return new HexCastleSilhouettePlan(
                HexCastleTheme.CompositeCompartments,
                drafts.Select(value => value.Ring).ToArray(),
                partitions);
        }

        private static HexCastleSilhouettePlan BuildHoneycombPlan(
            int seed,
            IReadOnlyList<int> wallRadii)
        {
            var drafts = wallRadii
                .Select((radius, index) => BuildHoneycombRing(seed, index, radius))
                .ToArray();
            var partitions = new List<HexCastlePartitionPath>();
            var clockwise = PositiveModulo(seed, 2) == 0;
            for (var bandIndex = 0; bandIndex < drafts.Length - 1; bandIndex++)
            {
                // 굽은 벌집 격벽 사이의 두 축에는 열린 성문용 직선 소켓을 남긴다.
                var straightSector = PositiveModulo(seed + bandIndex, 6);
                var oppositeStraightSector = PositiveModulo(straightSector + 3, 6);
                for (var sector = 0; sector < 6; sector++)
                {
                    if (sector == straightSector || sector == oppositeStraightSector)
                    {
                        partitions.Add(new HexCastlePartitionPath(
                            sector,
                            bandIndex,
                            BuildLine(
                                drafts[bandIndex].Axes[sector],
                                drafts[bandIndex + 1].Axes[sector])));
                        continue;
                    }

                    var innerDirection = clockwise ? sector : PositiveModulo(sector + 1, 6);
                    var outerDirection = clockwise ? PositiveModulo(sector + 1, 6) : sector;
                    var start = drafts[bandIndex].Axes[innerDirection];
                    var end = drafts[bandIndex + 1].Axes[outerDirection];
                    var middleRadius = (wallRadii[bandIndex] + wallRadii[bandIndex + 1]) / 2;
                    var pivot = ResolveSectorCell(middleRadius, sector, 1, 2);
                    partitions.Add(new HexCastlePartitionPath(
                        sector,
                        bandIndex,
                        BuildPolyline(start, pivot, end)));
                }
            }

            return new HexCastleSilhouettePlan(
                HexCastleTheme.HexHoneycomb,
                drafts.Select(value => value.Ring).ToArray(),
                partitions);
        }

        private static HexCastleSilhouettePlan BuildPetalPlan(
            int seed,
            IReadOnlyList<int> wallRadii)
        {
            var drafts = wallRadii
                .Select((radius, index) => BuildRadialRing(
                    HexCastleTheme.PetalBloom,
                    seed,
                    index,
                    radius))
                .ToArray();
            var partitions = new List<HexCastlePartitionPath>();
            for (var bandIndex = 0; bandIndex < drafts.Length - 1; bandIndex++)
            {
                // 사각 원본의 꽃잎별 격실 분할을 육각의 여섯 꽃잎 경계선으로 이식한다.
                for (var direction = 0; direction < 6; direction++)
                {
                    partitions.Add(new HexCastlePartitionPath(
                        direction,
                        bandIndex,
                        BuildLine(
                            drafts[bandIndex].Valleys[direction],
                            drafts[bandIndex + 1].Valleys[direction])));
                }
            }

            return new HexCastleSilhouettePlan(
                HexCastleTheme.PetalBloom,
                drafts.Select(value => value.Ring).ToArray(),
                partitions);
        }

        private static HexCastleSilhouettePlan BuildCrystalPlan(
            int seed,
            IReadOnlyList<int> wallRadii)
        {
            var drafts = wallRadii
                .Select((radius, index) => BuildCrystalRing(seed, index, radius))
                .ToArray();
            var partitions = new List<HexCastlePartitionPath>();
            var phase = PositiveModulo(seed, 6);
            for (var bandIndex = 0; bandIndex < drafts.Length - 1; bandIndex++)
            {
                // 결정 홈은 유지하되 서로 마주 보는 두 축에는 직선 성문 소켓을 둔다.
                var straightSector = PositiveModulo(seed + bandIndex, 6);
                var oppositeStraightSector = PositiveModulo(straightSector + 3, 6);
                for (var sector = 0; sector < 6; sector++)
                {
                    var direction = PositiveModulo(sector + phase, 6);
                    var useStraightSocket = sector == straightSector || sector == oppositeStraightSector;
                    partitions.Add(new HexCastlePartitionPath(
                        sector,
                        bandIndex,
                        BuildLine(
                            useStraightSocket
                                ? drafts[bandIndex].Axes[direction]
                                : drafts[bandIndex].FirstJoints[direction],
                            useStraightSocket
                                ? drafts[bandIndex + 1].Axes[direction]
                                : drafts[bandIndex + 1].FirstJoints[direction])));
                }
            }

            return new HexCastleSilhouettePlan(
                HexCastleTheme.CrystalMandala,
                drafts.Select(value => value.Ring).ToArray(),
                partitions);
        }

        private static HexCastleSilhouettePlan BuildFractalBastionPlan(
            int seed,
            IReadOnlyList<int> wallRadii)
        {
            var drafts = wallRadii
                .Select((radius, index) => BuildFractalBastionRing(seed, index, radius))
                .ToArray();
            return new HexCastleSilhouettePlan(
                HexCastleTheme.FractalBastion,
                drafts.Select(value => value.Ring).ToArray(),
                BuildAlignedPartitions(drafts, true));
        }

        private static HexCastleSilhouettePlan BuildVoronoiCrystalPlan(
            int seed,
            IReadOnlyList<int> wallRadii)
        {
            var drafts = wallRadii
                .Select((radius, index) => BuildVoronoiCrystalRing(seed, index, radius))
                .ToArray();
            return new HexCastleSilhouettePlan(
                HexCastleTheme.VoronoiCrystal,
                drafts.Select(value => value.Ring).ToArray(),
                BuildAlignedPartitions(drafts, false));
        }

        private static HexCastleSilhouettePlan BuildIrisShutterPlan(
            int seed,
            IReadOnlyList<int> wallRadii)
        {
            var drafts = wallRadii
                .Select((radius, index) => BuildIrisShutterRing(seed, index, radius))
                .ToArray();
            var partitions = new List<HexCastlePartitionPath>();
            var clockwise = PositiveModulo(seed, 2) == 0;
            for (var bandIndex = 0; bandIndex < drafts.Length - 1; bandIndex++)
            {
                for (var sector = 0; sector < 6; sector++)
                {
                    var useStraightSocket = sector == 0 || sector == 3;
                    var start = useStraightSocket
                        ? drafts[bandIndex].Axes[sector]
                        : clockwise
                            ? drafts[bandIndex].FirstJoints[sector]
                            : drafts[bandIndex].SecondJoints[sector];
                    var end = useStraightSocket
                        ? drafts[bandIndex + 1].Axes[sector]
                        : clockwise
                            ? drafts[bandIndex + 1].SecondJoints[sector]
                            : drafts[bandIndex + 1].FirstJoints[sector];
                    partitions.Add(new HexCastlePartitionPath(
                        sector,
                        bandIndex,
                        BuildLine(start, end)));
                }
            }

            return new HexCastleSilhouettePlan(
                HexCastleTheme.IrisShutter,
                drafts.Select(value => value.Ring).ToArray(),
                partitions);
        }

        private static IReadOnlyList<HexCastlePartitionPath> BuildAlignedPartitions(
            IReadOnlyList<SectorRingDraft> drafts,
            bool useFirstJoint)
        {
            var partitions = new List<HexCastlePartitionPath>();
            for (var bandIndex = 0; bandIndex < drafts.Count - 1; bandIndex++)
            {
                for (var sector = 0; sector < 6; sector++)
                {
                    var useStraightSocket = sector == 0 || sector == 3;
                    var start = useStraightSocket
                        ? drafts[bandIndex].Axes[sector]
                        : useFirstJoint
                            ? drafts[bandIndex].FirstJoints[sector]
                            : drafts[bandIndex].SecondJoints[sector];
                    var end = useStraightSocket
                        ? drafts[bandIndex + 1].Axes[sector]
                        : useFirstJoint
                            ? drafts[bandIndex + 1].FirstJoints[sector]
                            : drafts[bandIndex + 1].SecondJoints[sector];
                    partitions.Add(new HexCastlePartitionPath(
                        sector,
                        bandIndex,
                        BuildLine(start, end)));
                }
            }

            return partitions;
        }

        private static SectorRingDraft BuildCompositeRing(int seed, int layerIndex, int baseRadius)
        {
            var axes = new HexCoordinates[6];
            var firstJoints = new HexCoordinates[6];
            var secondJoints = new HexCoordinates[6];
            var phase = PositiveModulo(seed + layerIndex, 2);
            for (var sector = 0; sector < 6; sector++)
            {
                axes[sector] = HexCoordinates.Directions[sector] * baseRadius;
                var firstOffset = layerIndex == 0
                    ? 0
                    : PositiveModulo(sector + phase, 2) == 0 ? 1 : 0;
                var secondOffset = layerIndex == 0 ? 0 : 1 - firstOffset;
                firstJoints[sector] = ResolveSectorCell(baseRadius + firstOffset, sector, 1, 3);
                secondJoints[sector] = ResolveSectorCell(baseRadius + secondOffset, sector, 2, 3);
            }

            if (layerIndex == 0)
            {
                return new SectorRingDraft
                {
                    Axes = axes,
                    FirstJoints = firstJoints,
                    SecondJoints = secondJoints,
                    Ring = new HexCastleRingPath(
                        1,
                        HexCoordinates.EnumerateRing(baseRadius).ToArray(),
                        axes)
                };
            }

            return BuildSectorRing(layerIndex, axes, firstJoints, secondJoints, axes);
        }

        private static SectorRingDraft BuildHoneycombRing(int seed, int layerIndex, int baseRadius)
        {
            var axes = new HexCoordinates[6];
            var firstJoints = new HexCoordinates[6];
            var secondJoints = new HexCoordinates[6];
            var phase = PositiveModulo(seed, 2);
            var majorTowers = new List<HexCoordinates>();
            for (var sector = 0; sector < 6; sector++)
            {
                axes[sector] = HexCoordinates.Directions[sector] * baseRadius;
                var hasBud = PositiveModulo(sector + phase, 2) == 0;
                var budRadius = baseRadius + (hasBud ? 1 : 0);
                firstJoints[sector] = ResolveSectorCell(budRadius, sector, 1, 3);
                secondJoints[sector] = ResolveSectorCell(budRadius, sector, 2, 3);
                if (hasBud)
                {
                    majorTowers.Add(ResolveSectorCell(budRadius, sector, 1, 2));
                }
            }

            return BuildSectorRing(layerIndex, axes, firstJoints, secondJoints, majorTowers);
        }

        private static SectorRingDraft BuildCrystalRing(int seed, int layerIndex, int baseRadius)
        {
            var axes = new HexCoordinates[6];
            var firstJoints = new HexCoordinates[6];
            var secondJoints = new HexCoordinates[6];
            var phase = PositiveModulo(seed, 2);
            for (var sector = 0; sector < 6; sector++)
            {
                var longSpike = PositiveModulo(sector + phase, 2) == 0;
                axes[sector] = HexCoordinates.Directions[sector] * (baseRadius + (longSpike ? 1 : 0));
                var valleyRadius = layerIndex <= 1 ? baseRadius : baseRadius - 1;
                firstJoints[sector] = ResolveSectorCell(valleyRadius, sector, 1, 3);
                secondJoints[sector] = ResolveSectorCell(baseRadius, sector, 2, 3);
            }

            return BuildSectorRing(layerIndex, axes, firstJoints, secondJoints, axes);
        }

        private static SectorRingDraft BuildFractalBastionRing(int seed, int layerIndex, int baseRadius)
        {
            var axes = new HexCoordinates[6];
            var firstJoints = new HexCoordinates[6];
            var secondJoints = new HexCoordinates[6];
            var phase = PositiveModulo(seed + layerIndex, 2);
            for (var sector = 0; sector < 6; sector++)
            {
                if (layerIndex == 0)
                {
                    axes[sector] = HexCoordinates.Directions[sector] * baseRadius;
                    firstJoints[sector] = ResolveSectorCell(baseRadius, sector, 1, 3);
                    secondJoints[sector] = ResolveSectorCell(baseRadius, sector, 2, 3);
                    continue;
                }

                axes[sector] = HexCoordinates.Directions[sector] * (baseRadius + 1);
                var shallowStep = PositiveModulo(sector + phase, 2) == 0 ? baseRadius + 1 : baseRadius;
                firstJoints[sector] = ResolveSectorCell(shallowStep, sector, 1, 3);
                var recessRadius = layerIndex <= 1 ? baseRadius : baseRadius - 1;
                secondJoints[sector] = ResolveSectorCell(recessRadius, sector, 2, 3);
            }

            if (layerIndex == 0)
            {
                return new SectorRingDraft
                {
                    Axes = axes,
                    FirstJoints = firstJoints,
                    SecondJoints = secondJoints,
                    Ring = new HexCastleRingPath(
                        1,
                        HexCoordinates.EnumerateRing(baseRadius).ToArray(),
                        axes)
                };
            }

            return BuildSectorRing(layerIndex, axes, firstJoints, secondJoints, axes);
        }

        private static SectorRingDraft BuildVoronoiCrystalRing(int seed, int layerIndex, int baseRadius)
        {
            var axes = new HexCoordinates[6];
            var firstJoints = new HexCoordinates[6];
            var secondJoints = new HexCoordinates[6];
            var majorTowers = new List<HexCoordinates>();
            for (var sector = 0; sector < 6; sector++)
            {
                if (layerIndex == 0)
                {
                    axes[sector] = HexCoordinates.Directions[sector] * baseRadius;
                    firstJoints[sector] = ResolveSectorCell(baseRadius, sector, 1, 3);
                    secondJoints[sector] = ResolveSectorCell(baseRadius, sector, 2, 3);
                    majorTowers.Add(axes[sector]);
                    continue;
                }

                var axisOffset = ResolveSeedOffset(seed, layerIndex, sector, 17);
                var firstOffset = ResolveSeedOffset(seed, layerIndex, sector, 43);
                var secondOffset = ResolveSeedOffset(seed, layerIndex, sector, 79);
                if (layerIndex <= 1)
                {
                    axisOffset = Math.Max(0, axisOffset);
                    firstOffset = Math.Max(0, firstOffset);
                    secondOffset = Math.Max(0, secondOffset);
                }
                axes[sector] = HexCoordinates.Directions[sector] * (baseRadius + axisOffset);
                firstJoints[sector] = ResolveSectorCell(baseRadius + firstOffset, sector, 1, 3);
                secondJoints[sector] = ResolveSectorCell(baseRadius + secondOffset, sector, 2, 3);
                if (axisOffset >= 0)
                {
                    majorTowers.Add(axes[sector]);
                }
            }

            if (layerIndex == 0)
            {
                return new SectorRingDraft
                {
                    Axes = axes,
                    FirstJoints = firstJoints,
                    SecondJoints = secondJoints,
                    Ring = new HexCastleRingPath(
                        1,
                        HexCoordinates.EnumerateRing(baseRadius).ToArray(),
                        axes)
                };
            }

            return BuildSectorRing(layerIndex, axes, firstJoints, secondJoints, majorTowers);
        }

        private static SectorRingDraft BuildIrisShutterRing(int seed, int layerIndex, int baseRadius)
        {
            var axes = new HexCoordinates[6];
            var firstJoints = new HexCoordinates[6];
            var secondJoints = new HexCoordinates[6];
            var clockwise = PositiveModulo(seed, 2) == 0;
            for (var sector = 0; sector < 6; sector++)
            {
                axes[sector] = HexCoordinates.Directions[sector] * baseRadius;
                var firstRadius = layerIndex == 0
                    ? baseRadius
                    : baseRadius + (clockwise ? 1 : -1);
                var secondRadius = layerIndex == 0
                    ? baseRadius
                    : baseRadius + (clockwise ? -1 : 1);
                firstJoints[sector] = ResolveSectorCell(Math.Max(3, firstRadius), sector, 1, 3);
                secondJoints[sector] = ResolveSectorCell(Math.Max(3, secondRadius), sector, 2, 3);
            }

            if (layerIndex == 0)
            {
                return new SectorRingDraft
                {
                    Axes = axes,
                    FirstJoints = firstJoints,
                    SecondJoints = secondJoints,
                    Ring = new HexCastleRingPath(
                        1,
                        HexCoordinates.EnumerateRing(baseRadius).ToArray(),
                        axes)
                };
            }

            return BuildSectorRing(layerIndex, axes, firstJoints, secondJoints, axes);
        }

        private static int ResolveSeedOffset(int seed, int layerIndex, int sector, int salt)
        {
            unchecked
            {
                var hash = seed;
                hash = hash * 397 ^ (layerIndex + 1) * 101;
                hash = hash * 397 ^ (sector + 1) * salt;
                hash ^= hash >> 16;
                return PositiveModulo(hash, 3) - 1;
            }
        }

        private static SectorRingDraft BuildSectorRing(
            int layerIndex,
            HexCoordinates[] axes,
            HexCoordinates[] firstJoints,
            HexCoordinates[] secondJoints,
            IReadOnlyCollection<HexCoordinates> majorTowers)
        {
            var vertices = new List<HexCoordinates>(18);
            for (var sector = 0; sector < 6; sector++)
            {
                vertices.Add(axes[sector]);
                vertices.Add(firstJoints[sector]);
                vertices.Add(secondJoints[sector]);
            }

            return new SectorRingDraft
            {
                Axes = axes,
                FirstJoints = firstJoints,
                SecondJoints = secondJoints,
                Ring = new HexCastleRingPath(
                    layerIndex + 1,
                    BuildClosedPath(vertices),
                    majorTowers.Distinct().ToArray())
            };
        }

        private static RadialRingDraft BuildRadialRing(
            HexCastleTheme theme,
            int seed,
            int layerIndex,
            int baseRadius)
        {
            var tips = new HexCoordinates[6];
            var valleys = new HexCoordinates[6];
            var rotatePetalsHalfSector = theme == HexCastleTheme.PetalBloom &&
                                         PositiveModulo(seed, 8) == 1;
            for (var direction = 0; direction < 6; direction++)
            {
                var tipRadius = ResolveTipRadius(theme, seed, layerIndex, baseRadius, direction);
                var valleyInset = ResolveValleyInset(theme, layerIndex);
                var valleyRadius = Math.Max(2, baseRadius - valleyInset);
                if (rotatePetalsHalfSector)
                {
                    tips[direction] = ResolveBetweenDirectionsCell(tipRadius, direction);
                    valleys[direction] = HexCoordinates.Directions[(direction + 1) % 6] * valleyRadius;
                }
                else
                {
                    tips[direction] = HexCoordinates.Directions[direction] * tipRadius;
                    valleys[direction] = ResolveBetweenDirectionsCell(valleyRadius, direction);
                }
            }

            var vertices = new List<HexCoordinates>(12);
            for (var direction = 0; direction < 6; direction++)
            {
                vertices.Add(tips[direction]);
                vertices.Add(valleys[direction]);
            }

            var cells = BuildClosedPath(vertices);
            return new RadialRingDraft
            {
                Tips = tips,
                Valleys = valleys,
                Ring = new HexCastleRingPath(layerIndex + 1, cells, tips)
            };
        }

        private static int ResolveTipRadius(
            HexCastleTheme theme,
            int seed,
            int layerIndex,
            int baseRadius,
            int direction)
        {
            if (theme == HexCastleTheme.DiamondRadial)
            {
                var profileAxis = PositiveModulo(seed, 3);
                if (PositiveModulo(direction, 3) == profileAxis)
                {
                    return baseRadius + 1;
                }

                return baseRadius;
            }

            // 기본형은 여섯 꽃잎의 길이를 동일하게 유지한다. 원본의 교대 길이형은 후속 Seed 프로필이다.
            return layerIndex == 0 ? baseRadius : baseRadius + 1;
        }

        private static int ResolveValleyInset(HexCastleTheme theme, int layerIndex)
        {
            if (theme == HexCastleTheme.DiamondRadial)
            {
                return layerIndex <= 1 ? 0 : 1;
            }

            if (layerIndex <= 1)
            {
                return 0;
            }

            return layerIndex == 2 ? 1 : 2;
        }

        private static HexCoordinates ResolveBetweenDirectionsCell(int radius, int direction)
        {
            var first = HexCoordinates.Directions[PositiveModulo(direction, 6)];
            var second = HexCoordinates.Directions[PositiveModulo(direction + 1, 6)];
            var firstWorldX = first.Q + first.R * 0.5d;
            var firstWorldZ = first.R * 0.8660254037844386d;
            var secondWorldX = second.Q + second.R * 0.5d;
            var secondWorldZ = second.R * 0.8660254037844386d;
            var targetAngle = Math.Atan2(firstWorldZ + secondWorldZ, firstWorldX + secondWorldX);
            return HexCoordinates.EnumerateRing(radius)
                .OrderBy(value => AngularDistance(ResolveAngle(value), targetAngle))
                .ThenBy(value => value)
                .First();
        }

        private static HexCoordinates ResolveSectorCell(
            int radius,
            int direction,
            int numerator,
            int denominator)
        {
            if (denominator <= 0 || numerator < 0 || numerator > denominator)
            {
                throw new ArgumentOutOfRangeException(nameof(numerator));
            }

            var first = HexCoordinates.Directions[PositiveModulo(direction, 6)];
            var second = HexCoordinates.Directions[PositiveModulo(direction + 1, 6)];
            var firstWeight = denominator - numerator;
            var secondWeight = numerator;
            var targetX = (first.Q + first.R * 0.5d) * firstWeight +
                          (second.Q + second.R * 0.5d) * secondWeight;
            var targetZ = first.R * 0.8660254037844386d * firstWeight +
                          second.R * 0.8660254037844386d * secondWeight;
            var targetAngle = Math.Atan2(targetZ, targetX);
            return HexCoordinates.EnumerateRing(radius)
                .OrderBy(value => AngularDistance(ResolveAngle(value), targetAngle))
                .ThenBy(value => value)
                .First();
        }

        private static double ResolveAngle(HexCoordinates coordinates)
        {
            var x = coordinates.Q + coordinates.R * 0.5d;
            var z = coordinates.R * 0.8660254037844386d;
            return Math.Atan2(z, x);
        }

        private static double AngularDistance(double first, double second)
        {
            var difference = Math.Abs(first - second) % (Math.PI * 2d);
            return Math.Min(difference, Math.PI * 2d - difference);
        }

        private static IReadOnlyList<HexCoordinates> BuildClosedPath(
            IReadOnlyList<HexCoordinates> vertices)
        {
            var result = new List<HexCoordinates>();
            for (var index = 0; index < vertices.Count; index++)
            {
                var segment = BuildLine(vertices[index], vertices[(index + 1) % vertices.Count]);
                var end = index == vertices.Count - 1 ? segment.Count - 1 : segment.Count;
                for (var cellIndex = index == 0 ? 0 : 1; cellIndex < end; cellIndex++)
                {
                    result.Add(segment[cellIndex]);
                }
            }

            return result;
        }

        private static IReadOnlyList<HexCoordinates> BuildLine(
            HexCoordinates start,
            HexCoordinates end)
        {
            var distance = start.DistanceTo(end);
            if (distance == 0)
            {
                return new[] { start };
            }

            var result = new List<HexCoordinates>(distance + 1);
            for (var step = 0; step <= distance; step++)
            {
                var t = step / (double)distance;
                var coordinates = RoundAxial(
                    start.Q + (end.Q - start.Q) * t,
                    start.R + (end.R - start.R) * t);
                if (result.Count == 0 || result[result.Count - 1] != coordinates)
                {
                    result.Add(coordinates);
                }
            }

            return result;
        }

        private static IReadOnlyList<HexCoordinates> BuildPolyline(
            HexCoordinates start,
            HexCoordinates pivot,
            HexCoordinates end)
        {
            var result = new List<HexCoordinates>();
            AppendSegment(result, BuildLine(start, pivot));
            AppendSegment(result, BuildLine(pivot, end));
            return result;
        }

        private static void AppendSegment(
            ICollection<HexCoordinates> target,
            IReadOnlyList<HexCoordinates> segment)
        {
            foreach (var coordinates in segment)
            {
                if (target.Count == 0 || !target.Last().Equals(coordinates))
                {
                    target.Add(coordinates);
                }
            }
        }

        private static HexCoordinates RoundAxial(double q, double r)
        {
            var s = -q - r;
            var roundedQ = (int)Math.Round(q, MidpointRounding.AwayFromZero);
            var roundedR = (int)Math.Round(r, MidpointRounding.AwayFromZero);
            var roundedS = (int)Math.Round(s, MidpointRounding.AwayFromZero);
            var qDifference = Math.Abs(roundedQ - q);
            var rDifference = Math.Abs(roundedR - r);
            var sDifference = Math.Abs(roundedS - s);
            if (qDifference > rDifference && qDifference > sDifference)
            {
                roundedQ = -roundedR - roundedS;
            }
            else if (rDifference > sDifference)
            {
                roundedR = -roundedQ - roundedS;
            }

            return new HexCoordinates(roundedQ, roundedR);
        }

        private static HexCastleSilhouettePlan EnsureFormalGateSockets(
            HexCastleSilhouettePlan plan,
            int seed,
            int requiredGateSocketCountPerBand)
        {
            var partitions = plan.Partitions.ToList();
            for (var bandIndex = 0; bandIndex < plan.Rings.Count - 1; bandIndex++)
            {
                var socketCount = CountGateSocketPaths(plan.Rings, partitions, bandIndex);
                if (socketCount >= requiredGateSocketCountPerBand)
                {
                    continue;
                }

                var replacementIndices = partitions
                    .Select((partition, index) => new { Partition = partition, Index = index })
                    .Where(value => value.Partition.BandIndex == bandIndex)
                    .OrderBy(value => PositiveModulo(
                        seed + bandIndex * 31 + value.Partition.Direction * 17,
                        997))
                    .Select(value => value.Index)
                    .ToArray();
                foreach (var replacementIndex in replacementIndices)
                {
                    if (socketCount >= requiredGateSocketCountPerBand)
                    {
                        break;
                    }

                    var otherPartitionCells = partitions
                        .Where((_, index) => index != replacementIndex)
                        .SelectMany(value => value.Cells)
                        .ToHashSet();
                    foreach (var candidate in FindStraightGateSocketPaths(
                                 plan,
                                 bandIndex,
                                 seed,
                                 otherPartitionCells))
                    {
                        var previous = partitions[replacementIndex];
                        partitions[replacementIndex] = new HexCastlePartitionPath(
                            previous.Direction,
                            bandIndex,
                            candidate);
                        var candidateSocketCount = CountGateSocketPaths(
                            plan.Rings,
                            partitions,
                            bandIndex);
                        if (candidateSocketCount > socketCount)
                        {
                            socketCount = candidateSocketCount;
                            break;
                        }

                        partitions[replacementIndex] = previous;
                    }
                }

                if (socketCount < 1)
                {
                    throw new InvalidOperationException(
                        $"{plan.Theme} Band {bandIndex + 1}에 자연스러운 직선 성문 소켓을 만들 수 없습니다.");
                }
            }

            return new HexCastleSilhouettePlan(plan.Theme, plan.Rings, partitions);
        }

        internal static int CountFormalGateSocketPaths(
            HexCastleSilhouettePlan plan,
            int bandIndex)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            return CountGateSocketPaths(plan.Rings, plan.Partitions, bandIndex);
        }

        private static IEnumerable<IReadOnlyList<HexCoordinates>> FindStraightGateSocketPaths(
            HexCastleSilhouettePlan plan,
            int bandIndex,
            int seed,
            ISet<HexCoordinates> otherPartitionCells)
        {
            var innerRing = plan.Rings[bandIndex].Cells;
            var outerRing = plan.Rings[bandIndex + 1].Cells.ToHashSet();
            var allRingCells = plan.Rings.SelectMany(value => value.Cells).ToHashSet();
            var maximumSteps = Math.Max(8, plan.MaximumRadius * 2 + 4);
            var candidates = new List<IReadOnlyList<HexCoordinates>>();
            foreach (var start in innerRing
                         .OrderBy(value => ResolveGateSocketScore(seed, bandIndex, value, 0))
                         .ThenBy(value => value))
            {
                for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
                {
                    if (otherPartitionCells.Contains(start))
                    {
                        continue;
                    }

                    var path = new List<HexCoordinates> { start };
                    var current = start;
                    var previousRadius = start.DistanceFromOrigin;
                    for (var step = 1; step <= maximumSteps; step++)
                    {
                        current = current.Neighbor(direction);
                        var radius = current.DistanceFromOrigin;
                        if (radius < previousRadius)
                        {
                            break;
                        }

                        previousRadius = radius;
                        path.Add(current);
                        if (outerRing.Contains(current))
                        {
                            if (path.Count >= 3 && !otherPartitionCells.Contains(current))
                            {
                                candidates.Add(path.ToArray());
                            }
                            break;
                        }

                        if (allRingCells.Contains(current) || otherPartitionCells.Contains(current))
                        {
                            break;
                        }
                    }
                }
            }

            return candidates
                .OrderBy(value => ResolveGateSocketScore(
                    seed,
                    bandIndex,
                    value[0],
                    HexCastleWallVisualResolver.ResolveNeighborDirection(value[0], value[1])))
                .ThenBy(value => value.Count)
                .ThenBy(value => value[0]);
        }

        private static int CountGateSocketPaths(
            IReadOnlyList<HexCastleRingPath> rings,
            IReadOnlyList<HexCastlePartitionPath> partitions,
            int bandIndex)
        {
            var ringCells = rings.SelectMany(value => value.Cells).ToHashSet();
            var allWallCells = ringCells
                .Concat(partitions.SelectMany(value => value.Cells))
                .ToHashSet();
            var masks = BuildFormalConnectionMasks(rings, partitions);
            var result = 0;
            foreach (var partition in partitions.Where(value => value.BandIndex == bandIndex))
            {
                var hasSocket = false;
                for (var pathIndex = 1;
                     pathIndex < partition.Cells.Count - 1;
                     pathIndex++)
                {
                    var coordinates = partition.Cells[pathIndex];
                    if (ringCells.Contains(coordinates) || !masks.TryGetValue(coordinates, out var mask))
                    {
                        continue;
                    }

                    var topology = new HexCastleWallCellTopology(coordinates, mask);
                    if (topology.ConnectionCount != 2 || topology.ResolveTwoWaySeparation() != 3)
                    {
                        continue;
                    }

                    var wallDirection = topology.GetDirections()[0];
                    var firstCrossing = (1 << PositiveModulo(wallDirection + 1, 6)) |
                                        (1 << PositiveModulo(wallDirection + 5, 6));
                    var secondCrossing = (1 << PositiveModulo(wallDirection + 2, 6)) |
                                         (1 << PositiveModulo(wallDirection + 4, 6));
                    if (HasClearGateApproaches(coordinates, firstCrossing, allWallCells) ||
                        HasClearGateApproaches(coordinates, secondCrossing, allWallCells))
                    {
                        hasSocket = true;
                        break;
                    }
                }

                if (hasSocket)
                {
                    result++;
                }
            }

            return result;
        }

        private static bool HasClearGateApproaches(
            HexCoordinates coordinates,
            int passageMask,
            ISet<HexCoordinates> allWallCells)
        {
            for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
            {
                if ((passageMask & 1 << direction) != 0 &&
                    allWallCells.Contains(coordinates.Neighbor(direction)))
                {
                    return false;
                }
            }

            return true;
        }

        private static Dictionary<HexCoordinates, int> BuildFormalConnectionMasks(
            IReadOnlyList<HexCastleRingPath> rings,
            IReadOnlyList<HexCastlePartitionPath> partitions)
        {
            var masks = new Dictionary<HexCoordinates, int>();
            foreach (var ring in rings)
            {
                foreach (var coordinates in ring.Cells)
                {
                    if (!masks.ContainsKey(coordinates)) masks.Add(coordinates, 0);
                }
                for (var index = 0; index < ring.Cells.Count; index++)
                {
                    AddConnection(masks, ring.Cells[index], ring.Cells[(index + 1) % ring.Cells.Count]);
                }
            }

            foreach (var partition in partitions)
            {
                foreach (var coordinates in partition.Cells)
                {
                    if (!masks.ContainsKey(coordinates)) masks.Add(coordinates, 0);
                }
                for (var index = 0; index < partition.Cells.Count - 1; index++)
                {
                    AddConnection(masks, partition.Cells[index], partition.Cells[index + 1]);
                }
            }

            return masks;
        }

        private static int ResolveGateSocketScore(
            int seed,
            int bandIndex,
            HexCoordinates coordinates,
            int direction)
        {
            unchecked
            {
                var hash = seed;
                hash = hash * 397 ^ (bandIndex + 1) * 101;
                hash = hash * 397 ^ coordinates.Q * 193;
                hash = hash * 397 ^ coordinates.R * 389;
                hash = hash * 397 ^ direction * 769;
                hash ^= hash >> 16;
                return hash & int.MaxValue;
            }
        }

        private static void ValidateRadii(IReadOnlyList<int> wallRadii)
        {
            if (wallRadii == null || wallRadii.Count < 2 || wallRadii.Count > 4)
            {
                throw new ArgumentException("성벽 반경은 2~4개여야 합니다.", nameof(wallRadii));
            }

            for (var index = 0; index < wallRadii.Count; index++)
            {
                if (wallRadii[index] < 3 || index > 0 && wallRadii[index] <= wallRadii[index - 1])
                {
                    throw new ArgumentException("성벽 반경은 3 이상 오름차순이어야 합니다.", nameof(wallRadii));
                }
            }
        }

        private static void ValidatePlan(HexCastleSilhouettePlan plan)
        {
            var ringOwners = new Dictionary<HexCoordinates, int>();
            foreach (var ring in plan.Rings)
            {
                if (ring.Cells.Count < 6 || ring.Cells.Distinct().Count() != ring.Cells.Count)
                {
                    throw new InvalidOperationException($"{plan.Theme} {ring.DefenseLayer}중벽 폐곡선이 중복됐습니다.");
                }

                ValidateAdjacentPath(ring.Cells, true, $"{plan.Theme} {ring.DefenseLayer}중벽");
                foreach (var coordinates in ring.Cells)
                {
                    if (ringOwners.TryGetValue(coordinates, out var owner))
                    {
                        throw new InvalidOperationException(
                            $"{plan.Theme} {owner}/{ring.DefenseLayer}중벽이 {coordinates}에서 겹칩니다.");
                    }

                    ringOwners.Add(coordinates, ring.DefenseLayer);
                }
            }

            var connectionMasks = new Dictionary<HexCoordinates, int>();
            foreach (var ring in plan.Rings)
            {
                foreach (var coordinates in ring.Cells)
                {
                    if (!connectionMasks.ContainsKey(coordinates)) connectionMasks.Add(coordinates, 0);
                }
                for (var index = 0; index < ring.Cells.Count; index++)
                {
                    AddConnection(connectionMasks, ring.Cells[index], ring.Cells[(index + 1) % ring.Cells.Count]);
                }
            }

            foreach (var partition in plan.Partitions)
            {
                if (partition.BandIndex < 0 || partition.BandIndex >= plan.Rings.Count - 1 ||
                    partition.Cells.Count < 2)
                {
                    throw new InvalidOperationException($"{plan.Theme} 격벽 Band 정보가 잘못됐습니다.");
                }

                var inner = plan.Rings[partition.BandIndex].Cells;
                var outer = plan.Rings[partition.BandIndex + 1].Cells;
                if (!inner.Contains(partition.Cells[0]) || !outer.Contains(partition.Cells[partition.Cells.Count - 1]))
                {
                    throw new InvalidOperationException($"{plan.Theme} 격벽이 안쪽·바깥쪽 성벽에 닿지 않습니다.");
                }

                ValidateAdjacentPath(partition.Cells, false, $"{plan.Theme} Band {partition.BandIndex + 1} 격벽");
                foreach (var coordinates in partition.Cells)
                {
                    if (!connectionMasks.ContainsKey(coordinates)) connectionMasks.Add(coordinates, 0);
                }
                for (var index = 0; index < partition.Cells.Count - 1; index++)
                {
                    AddConnection(connectionMasks, partition.Cells[index], partition.Cells[index + 1]);
                }
            }

            foreach (var pair in connectionMasks)
            {
                var topology = new HexCastleWallCellTopology(pair.Key, pair.Value);
                if (topology.ConnectionCount < 2 || topology.ConnectionCount > 4)
                {
                    throw new InvalidOperationException(
                        $"{plan.Theme} 성벽 {pair.Key} 연결 수가 잘못됐습니다: {topology.ConnectionCount}");
                }
            }
        }

        private static void ValidateAdjacentPath(
            IReadOnlyList<HexCoordinates> path,
            bool closed,
            string label)
        {
            var count = closed ? path.Count : path.Count - 1;
            for (var index = 0; index < count; index++)
            {
                var next = closed ? (index + 1) % path.Count : index + 1;
                if (path[index].DistanceTo(path[next]) != 1)
                {
                    throw new InvalidOperationException($"{label}이 {path[index]} -> {path[next]}에서 끊겼습니다.");
                }
            }
        }

        private static void AddConnection(
            IDictionary<HexCoordinates, int> masks,
            HexCoordinates first,
            HexCoordinates second)
        {
            var direction = HexCastleWallVisualResolver.ResolveNeighborDirection(first, second);
            var opposite = PositiveModulo(direction + 3, 6);
            masks[first] |= 1 << direction;
            masks[second] |= 1 << opposite;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
