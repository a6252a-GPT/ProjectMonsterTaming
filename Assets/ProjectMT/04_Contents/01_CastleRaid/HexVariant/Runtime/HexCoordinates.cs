using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [Serializable]
    public readonly struct HexCoordinates : IEquatable<HexCoordinates>, IComparable<HexCoordinates>
    {
        private const float SquareRootThree = 1.7320508075688772f;

        public static readonly HexCoordinates[] Directions =
        {
            new HexCoordinates(1, 0),
            new HexCoordinates(1, -1),
            new HexCoordinates(0, -1),
            new HexCoordinates(-1, 0),
            new HexCoordinates(-1, 1),
            new HexCoordinates(0, 1)
        };

        public HexCoordinates(int q, int r)
        {
            Q = q;
            R = r;
        }

        public int Q { get; }
        public int R { get; }
        public int S => -Q - R;
        public int DistanceFromOrigin => Mathf.Max(Mathf.Abs(Q), Mathf.Abs(R), Mathf.Abs(S));

        public HexCoordinates Neighbor(int direction)
        {
            return this + Directions[PositiveModulo(direction, Directions.Length)];
        }

        public int DistanceTo(HexCoordinates other)
        {
            return (Mathf.Abs(Q - other.Q) + Mathf.Abs(R - other.R) + Mathf.Abs(S - other.S)) / 2;
        }

        public Vector3 ToWorld(float size)
        {
            if (size <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "육각 셀 크기는 0보다 커야 합니다.");
            }

            return new Vector3(
                size * SquareRootThree * (Q + R * 0.5f),
                0f,
                size * 1.5f * R);
        }

        public static HexCoordinates FromWorld(Vector3 position, float size)
        {
            if (size <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "육각 셀 크기는 0보다 커야 합니다.");
            }

            var q = (SquareRootThree / 3f * position.x - position.z / 3f) / size;
            var r = (2f / 3f * position.z) / size;
            return RoundAxial(q, r);
        }

        public static IEnumerable<HexCoordinates> EnumerateRadius(int radius)
        {
            if (radius < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            for (var q = -radius; q <= radius; q++)
            {
                var minimumR = Mathf.Max(-radius, -q - radius);
                var maximumR = Mathf.Min(radius, -q + radius);
                for (var r = minimumR; r <= maximumR; r++)
                {
                    yield return new HexCoordinates(q, r);
                }
            }
        }

        public static IEnumerable<HexCoordinates> EnumerateRing(int radius)
        {
            if (radius < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            if (radius == 0)
            {
                yield return new HexCoordinates(0, 0);
                yield break;
            }

            var current = Directions[4] * radius;
            for (var side = 0; side < Directions.Length; side++)
            {
                for (var step = 0; step < radius; step++)
                {
                    yield return current;
                    current += Directions[side];
                }
            }
        }

        public bool Equals(HexCoordinates other)
        {
            return Q == other.Q && R == other.R;
        }

        public override bool Equals(object obj)
        {
            return obj is HexCoordinates other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return Q * 397 ^ R;
            }
        }

        public int CompareTo(HexCoordinates other)
        {
            var qComparison = Q.CompareTo(other.Q);
            return qComparison != 0 ? qComparison : R.CompareTo(other.R);
        }

        public override string ToString()
        {
            return $"({Q}, {R}, {S})";
        }

        public static HexCoordinates operator +(HexCoordinates left, HexCoordinates right)
        {
            return new HexCoordinates(left.Q + right.Q, left.R + right.R);
        }

        public static HexCoordinates operator -(HexCoordinates left, HexCoordinates right)
        {
            return new HexCoordinates(left.Q - right.Q, left.R - right.R);
        }

        public static HexCoordinates operator *(HexCoordinates value, int multiplier)
        {
            return new HexCoordinates(value.Q * multiplier, value.R * multiplier);
        }

        public static bool operator ==(HexCoordinates left, HexCoordinates right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HexCoordinates left, HexCoordinates right)
        {
            return !left.Equals(right);
        }

        private static HexCoordinates RoundAxial(float q, float r)
        {
            var s = -q - r;
            var roundedQ = Mathf.RoundToInt(q);
            var roundedR = Mathf.RoundToInt(r);
            var roundedS = Mathf.RoundToInt(s);
            var qDifference = Mathf.Abs(roundedQ - q);
            var rDifference = Mathf.Abs(roundedR - r);
            var sDifference = Mathf.Abs(roundedS - s);

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

        private static int PositiveModulo(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
