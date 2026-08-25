using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectMT.Contents.CastleRaidHex
{
    public readonly struct HexCastleWallCellTopology
    {
        public HexCastleWallCellTopology(HexCoordinates coordinates, int connectionMask)
        {
            Coordinates = coordinates;
            ConnectionMask = connectionMask & 0x3F;
            ConnectionCount = CountBits(ConnectionMask);
        }

        public HexCoordinates Coordinates { get; }
        public int ConnectionMask { get; }
        public int ConnectionCount { get; }
        public bool IsJunction => ConnectionCount >= 3;

        public bool Connects(int direction)
        {
            var normalized = PositiveModulo(direction, HexCoordinates.Directions.Length);
            return (ConnectionMask & (1 << normalized)) != 0;
        }

        public int[] GetDirections()
        {
            return Enumerable.Range(0, HexCoordinates.Directions.Length)
                .Where(Connects)
                .ToArray();
        }

        public int ResolveTwoWaySeparation()
        {
            if (ConnectionCount != 2)
            {
                throw new InvalidOperationException(
                    $"{Coordinates}은 2방향 성벽 Cell이 아닙니다: {ConnectionCount}");
            }

            var directions = GetDirections();
            var difference = Math.Abs(directions[0] - directions[1]);
            return Math.Min(difference, HexCoordinates.Directions.Length - difference);
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

        private static int PositiveModulo(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }

    public static class HexCastleWallTopologyResolver
    {
        public static HexCastleWallCellTopology Resolve(HexCastleLayout layout, HexCoordinates coordinates)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (!layout.TryGetCell(coordinates, out var cell) || !cell.IsWallPathCell)
            {
                throw new InvalidOperationException($"{coordinates}은 성벽망 Cell이 아닙니다.");
            }

            if (cell.HasExplicitWallConnections)
            {
                return new HexCastleWallCellTopology(coordinates, cell.WallConnectionMask);
            }

            return Resolve(layout.Cells.Values
                .Where(value => value.IsWallPathCell)
                .Select(value => value.Coordinates)
                .ToHashSet(), coordinates);
        }

        public static HexCastleWallCellTopology Resolve(
            ISet<HexCoordinates> wallCoordinates,
            HexCoordinates coordinates)
        {
            if (wallCoordinates == null)
            {
                throw new ArgumentNullException(nameof(wallCoordinates));
            }

            if (!wallCoordinates.Contains(coordinates))
            {
                throw new InvalidOperationException($"성벽망에 {coordinates}이 없습니다.");
            }

            var mask = 0;
            for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
            {
                if (wallCoordinates.Contains(coordinates.Neighbor(direction)))
                {
                    mask |= 1 << direction;
                }
            }

            return new HexCastleWallCellTopology(coordinates, mask);
        }

        public static IReadOnlyDictionary<HexCoordinates, HexCastleWallCellTopology> Build(HexCastleLayout layout)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            var coordinates = layout.Cells.Values
                .Where(value => value.IsWallPathCell)
                .Select(value => value.Coordinates)
                .ToHashSet();
            var result = coordinates.ToDictionary(
                value => value,
                value => layout.Cells[value].HasExplicitWallConnections
                    ? new HexCastleWallCellTopology(value, layout.Cells[value].WallConnectionMask)
                    : Resolve(coordinates, value));

            foreach (var pair in result)
            {
                foreach (var direction in pair.Value.GetDirections())
                {
                    var neighbor = pair.Key.Neighbor(direction);
                    var opposite = (direction + 3) % HexCoordinates.Directions.Length;
                    if (!result.TryGetValue(neighbor, out var neighborTopology) ||
                        !neighborTopology.Connects(opposite))
                    {
                        throw new InvalidOperationException(
                            $"성벽 연결이 서로 맞지 않습니다: {pair.Key} D{direction} -> {neighbor}");
                    }
                }
            }

            return result;
        }
    }
}
