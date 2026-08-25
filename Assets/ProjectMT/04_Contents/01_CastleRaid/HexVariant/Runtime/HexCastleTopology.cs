using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    [Serializable]
    public readonly struct HexVertexKey : IEquatable<HexVertexKey>, IComparable<HexVertexKey>
    {
        private static readonly Vector2Int[] CornerOffsets =
        {
            new Vector2Int(1, 1),
            new Vector2Int(0, 2),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1),
            new Vector2Int(0, -2),
            new Vector2Int(1, -1)
        };

        public HexVertexKey(int u, int v)
        {
            U = u;
            V = v;
        }

        public int U { get; }
        public int V { get; }

        public static HexVertexKey FromCellCorner(HexCoordinates coordinates, int cornerIndex)
        {
            var offset = CornerOffsets[PositiveModulo(cornerIndex, CornerOffsets.Length)];
            return new HexVertexKey(2 * coordinates.Q + coordinates.R + offset.x, 3 * coordinates.R + offset.y);
        }

        public Vector3 ToWorld()
        {
            return new Vector3(
                HexSpatialContract.CellOuterRadius * Mathf.Sqrt(3f) * 0.5f * U,
                0f,
                HexSpatialContract.CellOuterRadius * 0.5f * V);
        }

        public bool Equals(HexVertexKey other)
        {
            return U == other.U && V == other.V;
        }

        public override bool Equals(object obj)
        {
            return obj is HexVertexKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return U * 397 ^ V;
            }
        }

        public int CompareTo(HexVertexKey other)
        {
            var uComparison = U.CompareTo(other.U);
            return uComparison != 0 ? uComparison : V.CompareTo(other.V);
        }

        public override string ToString()
        {
            return $"V({U},{V})";
        }

        public static bool operator ==(HexVertexKey left, HexVertexKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HexVertexKey left, HexVertexKey right)
        {
            return !left.Equals(right);
        }

        private static int PositiveModulo(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }

    [Serializable]
    public readonly struct HexEdgeKey : IEquatable<HexEdgeKey>, IComparable<HexEdgeKey>
    {
        public HexEdgeKey(HexVertexKey first, HexVertexKey second)
        {
            if (first == second)
            {
                throw new ArgumentException("육각 Edge의 두 꼭짓점은 달라야 합니다.");
            }

            if (first.CompareTo(second) <= 0)
            {
                Start = first;
                End = second;
            }
            else
            {
                Start = second;
                End = first;
            }
        }

        public HexVertexKey Start { get; }
        public HexVertexKey End { get; }

        public static HexEdgeKey FromCellSide(HexCoordinates coordinates, int direction)
        {
            var normalized = PositiveModulo(direction, HexCoordinates.Directions.Length);
            var firstCorner = PositiveModulo(5 - normalized, 6);
            var secondCorner = (firstCorner + 1) % 6;
            return new HexEdgeKey(
                HexVertexKey.FromCellCorner(coordinates, firstCorner),
                HexVertexKey.FromCellCorner(coordinates, secondCorner));
        }

        public Vector3 Midpoint => (Start.ToWorld() + End.ToWorld()) * 0.5f;
        public float Length => Vector3.Distance(Start.ToWorld(), End.ToWorld());

        public bool Equals(HexEdgeKey other)
        {
            return Start.Equals(other.Start) && End.Equals(other.End);
        }

        public override bool Equals(object obj)
        {
            return obj is HexEdgeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return Start.GetHashCode() * 397 ^ End.GetHashCode();
            }
        }

        public int CompareTo(HexEdgeKey other)
        {
            var startComparison = Start.CompareTo(other.Start);
            return startComparison != 0 ? startComparison : End.CompareTo(other.End);
        }

        public override string ToString()
        {
            return $"E[{Start}-{End}]";
        }

        public static bool operator ==(HexEdgeKey left, HexEdgeKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HexEdgeKey left, HexEdgeKey right)
        {
            return !left.Equals(right);
        }

        private static int PositiveModulo(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }

    public enum HexDistrictRole
    {
        PalaceCore,
        Regular,
        Defense,
        Reward
    }

    public enum HexWallJunctionRole
    {
        End,
        Straight,
        Corner,
        Branch,
        GateSide
    }

    public sealed class HexCastleDistrict
    {
        private readonly HashSet<HexCoordinates> footprintCells;

        public HexCastleDistrict(
            int districtId,
            string templateId,
            int defenseRing,
            HexDistrictRole role,
            IEnumerable<HexCoordinates> footprintCells)
        {
            if (districtId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(districtId));
            }

            if (string.IsNullOrWhiteSpace(templateId))
            {
                throw new ArgumentException("구획 TemplateId가 비어 있습니다.", nameof(templateId));
            }

            DistrictId = districtId;
            TemplateId = templateId;
            DefenseRing = defenseRing;
            Role = role;
            this.footprintCells = new HashSet<HexCoordinates>(
                footprintCells ?? throw new ArgumentNullException(nameof(footprintCells)));
            if (this.footprintCells.Count == 0)
            {
                throw new ArgumentException("구획 Footprint가 비어 있습니다.", nameof(footprintCells));
            }
        }

        public int DistrictId { get; }
        public string TemplateId { get; }
        public int DefenseRing { get; }
        public HexDistrictRole Role { get; }
        public IReadOnlyCollection<HexCoordinates> FootprintCells => footprintCells;
        public bool Contains(HexCoordinates coordinates) => footprintCells.Contains(coordinates);
    }

    public sealed class HexCastleWallEdge
    {
        private readonly HashSet<int> ownerDistrictIds = new HashSet<int>();

        internal HexCastleWallEdge(
            string wallId,
            HexEdgeKey edgeKey,
            HexCoordinates cell,
            HexCoordinates neighbor,
            int direction,
            int ownerDistrictId)
        {
            WallId = wallId;
            EdgeKey = edgeKey;
            Cell = cell;
            Neighbor = neighbor;
            Direction = direction;
            ownerDistrictIds.Add(ownerDistrictId);
        }

        public string WallId { get; internal set; }
        public HexEdgeKey EdgeKey { get; }
        public HexCoordinates Cell { get; }
        public HexCoordinates Neighbor { get; }
        public int Direction { get; }
        public IReadOnlyCollection<int> OwnerDistrictIds => ownerDistrictIds;
        public HexCastleWallRole WallBand { get; internal set; }
        public int WallDefenseLayer { get; internal set; }
        public int WallTier { get; internal set; }
        public string WallLineId { get; internal set; }
        public string DefenseLineGroup { get; internal set; }
        public float EffectiveHealth { get; internal set; }
        public bool IsGate { get; internal set; }

        internal void AddOwner(int districtId)
        {
            ownerDistrictIds.Add(districtId);
        }

        public void ConfigureDefense(
            HexCastleWallRole wallBand,
            int wallDefenseLayer,
            int wallTier,
            string wallLineId,
            string defenseLineGroup,
            float effectiveHealth)
        {
            if (wallDefenseLayer < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(wallDefenseLayer));
            }
            if (wallTier < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(wallTier));
            }
            if (effectiveHealth <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(effectiveHealth));
            }

            WallBand = wallBand;
            WallDefenseLayer = wallDefenseLayer;
            WallTier = wallTier;
            WallLineId = wallLineId ?? string.Empty;
            DefenseLineGroup = defenseLineGroup ?? string.Empty;
            EffectiveHealth = effectiveHealth;
        }
    }

    public sealed class HexCastleWallJunction
    {
        internal HexCastleWallJunction(
            string junctionId,
            HexVertexKey vertexKey,
            HexWallJunctionRole role,
            IEnumerable<string> connectedWallIds)
        {
            JunctionId = junctionId;
            VertexKey = vertexKey;
            Role = role;
            ConnectedWallIds = connectedWallIds.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        public string JunctionId { get; }
        public HexVertexKey VertexKey { get; }
        public HexWallJunctionRole Role { get; }
        public IReadOnlyList<string> ConnectedWallIds { get; }
        public bool RequiresTower => Role == HexWallJunctionRole.End ||
                                     Role == HexWallJunctionRole.Corner ||
                                     Role == HexWallJunctionRole.Branch ||
                                     Role == HexWallJunctionRole.GateSide;
    }

    public sealed class HexCastleWallTopology
    {
        internal HexCastleWallTopology(
            IEnumerable<HexCastleWallEdge> edges,
            IEnumerable<HexCastleWallJunction> junctions)
        {
            Edges = edges.ToDictionary(edge => edge.EdgeKey);
            Junctions = junctions.ToDictionary(junction => junction.VertexKey);
        }

        public IReadOnlyDictionary<HexEdgeKey, HexCastleWallEdge> Edges { get; }
        public IReadOnlyDictionary<HexVertexKey, HexCastleWallJunction> Junctions { get; }
    }

    public static class HexCastleWallTopologyBuilder
    {
        public static HexCastleWallTopology Build(IEnumerable<HexCastleDistrict> districts)
        {
            if (districts == null)
            {
                throw new ArgumentNullException(nameof(districts));
            }

            var districtArray = districts.OrderBy(district => district.DistrictId).ToArray();
            var cellOwners = new Dictionary<HexCoordinates, HexCastleDistrict>();
            foreach (var district in districtArray)
            {
                foreach (var cell in district.FootprintCells)
                {
                    if (cellOwners.TryGetValue(cell, out var existing))
                    {
                        throw new InvalidOperationException(
                            $"구획 {existing.DistrictId}와 {district.DistrictId}가 Cell {cell}을 겹쳐 점유합니다.");
                    }

                    cellOwners.Add(cell, district);
                }
            }

            var edgeMap = new Dictionary<HexEdgeKey, HexCastleWallEdge>();
            foreach (var district in districtArray)
            {
                foreach (var cell in district.FootprintCells.OrderBy(value => value))
                {
                    for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
                    {
                        var neighbor = cell.Neighbor(direction);
                        if (cellOwners.TryGetValue(neighbor, out var neighborDistrict) &&
                            neighborDistrict.DistrictId == district.DistrictId)
                        {
                            continue;
                        }

                        var edgeKey = HexEdgeKey.FromCellSide(cell, direction);
                        if (edgeMap.TryGetValue(edgeKey, out var shared))
                        {
                            shared.AddOwner(district.DistrictId);
                            continue;
                        }

                        edgeMap.Add(
                            edgeKey,
                            new HexCastleWallEdge(
                                string.Empty,
                                edgeKey,
                                cell,
                                neighbor,
                                direction,
                                district.DistrictId));
                    }
                }
            }

            var orderedEdges = edgeMap.Values.OrderBy(edge => edge.EdgeKey).ToArray();
            for (var index = 0; index < orderedEdges.Length; index++)
            {
                orderedEdges[index].WallId = $"WALL_{index + 1:00000}";
            }

            var edgesByVertex = new Dictionary<HexVertexKey, List<HexCastleWallEdge>>();
            foreach (var edge in orderedEdges)
            {
                AddVertexEdge(edgesByVertex, edge.EdgeKey.Start, edge);
                AddVertexEdge(edgesByVertex, edge.EdgeKey.End, edge);
            }

            var junctions = edgesByVertex
                .OrderBy(pair => pair.Key)
                .Select((pair, index) => new HexCastleWallJunction(
                    $"JUNCTION_{index + 1:00000}",
                    pair.Key,
                    ResolveJunctionRole(pair.Key, pair.Value),
                    pair.Value.Select(edge => edge.WallId)))
                .ToArray();
            return new HexCastleWallTopology(orderedEdges, junctions);
        }

        private static void AddVertexEdge(
            IDictionary<HexVertexKey, List<HexCastleWallEdge>> map,
            HexVertexKey vertex,
            HexCastleWallEdge edge)
        {
            if (!map.TryGetValue(vertex, out var edges))
            {
                edges = new List<HexCastleWallEdge>();
                map.Add(vertex, edges);
            }

            edges.Add(edge);
        }

        private static HexWallJunctionRole ResolveJunctionRole(
            HexVertexKey vertex,
            IReadOnlyList<HexCastleWallEdge> edges)
        {
            if (edges.Count <= 1)
            {
                return HexWallJunctionRole.End;
            }

            if (edges.Count >= 3)
            {
                return HexWallJunctionRole.Branch;
            }

            var firstOther = ResolveOtherVertex(edges[0].EdgeKey, vertex);
            var secondOther = ResolveOtherVertex(edges[1].EdgeKey, vertex);
            var firstDirection = new Vector2Int(firstOther.U - vertex.U, firstOther.V - vertex.V);
            var secondDirection = new Vector2Int(secondOther.U - vertex.U, secondOther.V - vertex.V);
            return firstDirection.x == -secondDirection.x && firstDirection.y == -secondDirection.y
                ? HexWallJunctionRole.Straight
                : HexWallJunctionRole.Corner;
        }

        private static HexVertexKey ResolveOtherVertex(HexEdgeKey edge, HexVertexKey vertex)
        {
            return edge.Start == vertex ? edge.End : edge.Start;
        }
    }
}
