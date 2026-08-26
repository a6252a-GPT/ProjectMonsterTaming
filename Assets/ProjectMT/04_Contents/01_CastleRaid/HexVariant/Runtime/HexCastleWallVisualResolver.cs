using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    public enum HexCastleWallCurvePlacement
    {
        Inside,
        Outside
    }

    public readonly struct HexCastleWallVisualResolution
    {
        public HexCastleWallVisualResolution(
            HexCastleWallVisualKind visualKind,
            int rotationStep,
            int previousDirection,
            int nextDirection)
        {
            VisualKind = visualKind;
            RotationStep = rotationStep;
            PreviousDirection = previousDirection;
            NextDirection = nextDirection;
        }

        public HexCastleWallVisualKind VisualKind { get; }
        public int RotationStep { get; }
        public float RotationDegrees => RotationStep * 60f;
        public int PreviousDirection { get; }
        public int NextDirection { get; }
    }

    public static class HexCastleWallVisualResolver
    {
        private static readonly int[] StraightSourcePair = { 3, 0 };
        private static readonly int[] CornerASourcePair = { 3, 5 };
        private static readonly int[] CornerBSourcePair = { 3, 4 };

        public static HexCastleWallVisualResolution Resolve(
            HexCastleCellKind cellKind,
            HexCoordinates previous,
            HexCoordinates current,
            HexCoordinates next,
            HexCastleWallCurvePlacement curvePlacement)
        {
            if (cellKind != HexCastleCellKind.Wall && cellKind != HexCastleCellKind.Tower &&
                cellKind != HexCastleCellKind.Gate)
            {
                throw new ArgumentOutOfRangeException(nameof(cellKind), $"{cellKind}은 Wall Path Cell이 아닙니다.");
            }

            var previousDirection = ResolveNeighborDirection(current, previous);
            var nextDirection = ResolveNeighborDirection(current, next);
            return ResolveDirections(cellKind, previousDirection, nextDirection, curvePlacement);
        }

        public static HexCastleWallVisualResolution ResolveDirections(
            HexCastleCellKind cellKind,
            int previousDirection,
            int nextDirection,
            HexCastleWallCurvePlacement curvePlacement = HexCastleWallCurvePlacement.Outside)
        {
            if (cellKind != HexCastleCellKind.Wall && cellKind != HexCastleCellKind.Tower &&
                cellKind != HexCastleCellKind.Gate)
            {
                throw new ArgumentOutOfRangeException(nameof(cellKind), $"{cellKind}은 Wall Path Cell이 아닙니다.");
            }

            previousDirection = PositiveModulo(previousDirection, HexCoordinates.Directions.Length);
            nextDirection = PositiveModulo(nextDirection, HexCoordinates.Directions.Length);
            if (previousDirection == nextDirection)
            {
                throw new InvalidOperationException("Wall Path의 이전·다음 Edge가 같습니다.");
            }

            var separation = CircularSeparation(previousDirection, nextDirection);
            var sourcePair = ResolveSourcePair(separation);
            var visualKind = ResolveVisualKind(cellKind, separation, curvePlacement);
            var rotations = ResolveRotations(sourcePair, previousDirection, nextDirection);
            var expectedCount = separation == 3 ? 2 : 1;
            if (rotations.Count != expectedCount)
            {
                throw new InvalidOperationException(
                    $"Wall Path 회전 후보 수가 잘못됐습니다. Separation={separation}, Count={rotations.Count}");
            }

            return new HexCastleWallVisualResolution(
                visualKind,
                ResolveMinimum(rotations),
                previousDirection,
                nextDirection);
        }

        public static int ResolveNeighborDirection(HexCoordinates current, HexCoordinates neighbor)
        {
            var delta = neighbor - current;
            for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
            {
                if (HexCoordinates.Directions[direction] == delta)
                {
                    return direction;
                }
            }

            throw new InvalidOperationException($"{neighbor}은 {current}의 이웃 Cell이 아닙니다.");
        }

        private static int[] ResolveSourcePair(int separation)
        {
            switch (separation)
            {
                case 3:
                    return StraightSourcePair;
                case 2:
                    return CornerASourcePair;
                case 1:
                    return CornerBSourcePair;
                default:
                    throw new InvalidOperationException($"지원하지 않는 Wall Edge Separation입니다: {separation}");
            }
        }

        private static HexCastleWallVisualKind ResolveVisualKind(
            HexCastleCellKind cellKind,
            int separation,
            HexCastleWallCurvePlacement curvePlacement)
        {
            _ = curvePlacement; // 양면 파생 코너는 안쪽·바깥쪽 선택이 없다
            if (cellKind == HexCastleCellKind.Gate)
            {
                switch (separation)
                {
                    case 3:
                        return HexCastleWallVisualKind.StraightGate;
                    case 2:
                        return HexCastleWallVisualKind.CornerAGate;
                    default:
                        throw new InvalidOperationException("KayKit 원본에는 Corner B Gate가 없습니다.");
                }
            }

            switch (separation)
            {
                case 3:
                    return HexCastleWallVisualKind.Straight;
                case 2:
                    return HexCastleWallVisualKind.CornerAOutside;
                case 1:
                    return HexCastleWallVisualKind.CornerBOutside;
                default:
                    throw new InvalidOperationException($"지원하지 않는 Wall Edge Separation입니다: {separation}");
            }
        }

        private static List<int> ResolveRotations(
            IReadOnlyList<int> sourcePair,
            int previousDirection,
            int nextDirection)
        {
            var result = new List<int>();
            for (var step = 0; step < 6; step++)
            {
                var first = PositiveModulo(sourcePair[0] + step, 6);
                var second = PositiveModulo(sourcePair[1] + step, 6);
                if ((first == previousDirection && second == nextDirection) ||
                    (first == nextDirection && second == previousDirection))
                {
                    result.Add(step);
                }
            }

            return result;
        }

        private static int CircularSeparation(int first, int second)
        {
            var difference = Mathf.Abs(first - second);
            return Mathf.Min(difference, 6 - difference);
        }

        private static int PositiveModulo(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private static int ResolveMinimum(IReadOnlyList<int> values)
        {
            var result = values[0];
            for (var index = 1; index < values.Count; index++)
            {
                result = Mathf.Min(result, values[index]);
            }

            return result;
        }
    }
}
