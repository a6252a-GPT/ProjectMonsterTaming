using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid.Generation
{
    public static class CastleDifficultyEvaluator // 외곽에서 목표까지의 가중 최단 피해량 계산
    {
        private const float CostTolerance = 0.001f;

        private sealed class GraphNode
        {
            public CastlePlacementData Placement;
            public readonly List<int> Neighbors = new List<int>();
        }

        private sealed class Graph
        {
            public readonly List<GraphNode> Nodes = new List<GraphNode>();
            public readonly HashSet<int> Sources = new HashSet<int>();
            public readonly Dictionary<string, int> PlacementNodes = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        private sealed class PathResult
        {
            public bool Found;
            public float Damage;
            public readonly List<string> PlacementIds = new List<string>();
        }

        private readonly struct QueueEntry
        {
            public QueueEntry(int node, float damage, int pathSteps)
            {
                Node = node;
                Damage = damage;
                Steps = pathSteps;
            }

            public int Node { get; }
            public float Damage { get; }
            public int Steps { get; }
        }

        private sealed class MinHeap // 후보 100개 일괄 평가용 감소키 없는 최소 힙
        {
            private readonly List<QueueEntry> entries = new List<QueueEntry>();

            public int Count => entries.Count;

            public void Push(QueueEntry entry)
            {
                entries.Add(entry);
                var index = entries.Count - 1;
                while (index > 0)
                {
                    var parent = (index - 1) / 2;
                    if (!ComesBefore(entries[index], entries[parent]))
                    {
                        break;
                    }

                    (entries[index], entries[parent]) = (entries[parent], entries[index]);
                    index = parent;
                }
            }

            public QueueEntry Pop()
            {
                var result = entries[0];
                var last = entries[entries.Count - 1];
                entries.RemoveAt(entries.Count - 1);
                if (entries.Count == 0)
                {
                    return result;
                }

                entries[0] = last;
                var index = 0;
                while (true)
                {
                    var left = index * 2 + 1;
                    if (left >= entries.Count)
                    {
                        break;
                    }

                    var right = left + 1;
                    var best = right < entries.Count && ComesBefore(entries[right], entries[left]) ? right : left;
                    if (!ComesBefore(entries[best], entries[index]))
                    {
                        break;
                    }

                    (entries[index], entries[best]) = (entries[best], entries[index]);
                    index = best;
                }

                return result;
            }

            private static bool ComesBefore(QueueEntry left, QueueEntry right)
            {
                return left.Damage < right.Damage - CostTolerance ||
                       Mathf.Abs(left.Damage - right.Damage) <= CostTolerance && left.Steps < right.Steps;
            }
        }

        public static CastleDifficultyReport Evaluate(CastleGenerationCandidate candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            var palace = candidate.Placements.SingleOrDefault(placement => placement.Kind == CastlePlacementKind.Palace);
            if (palace == null)
            {
                return EmptyReport();
            }

            var graph = BuildGraph(candidate);
            if (!graph.PlacementNodes.TryGetValue(palace.PlacementId, out var palaceNode))
            {
                return EmptyReport();
            }

            var clearPath = FindPath(graph, palaceNode);
            var mandatory = clearPath.PlacementIds.Where(id => !string.Equals(id, palace.PlacementId, StringComparison.Ordinal)).ToArray();
            var obstacleDamage = clearPath.Found ? Mathf.Max(0f, clearPath.Damage - palace.EffectiveHealth) : 0f;
            var defensePressure = candidate.Placements
                .Where(placement => placement.Kind == CastlePlacementKind.Defender || placement.Kind == CastlePlacementKind.DefenseBuilding)
                .Sum(placement => placement.EffectiveHealth);
            var totalDamage = candidate.Placements.Sum(placement => placement.EffectiveHealth);

            return new CastleDifficultyReport(
                clearPath.Found,
                clearPath.Found ? clearPath.Damage : 0f,
                obstacleDamage,
                palace.EffectiveHealth,
                defensePressure,
                totalDamage,
                ResolveLootDamage(graph, candidate, CastleLootKind.Gold),
                ResolveLootDamage(graph, candidate, CastleLootKind.Equipment),
                ResolveLootDamage(graph, candidate, CastleLootKind.Key),
                mandatory);
        }

        private static float ResolveLootDamage(Graph graph, CastleGenerationCandidate candidate, CastleLootKind lootKind)
        {
            var best = float.PositiveInfinity;
            foreach (var placement in candidate.Placements)
            {
                if (placement.Kind != CastlePlacementKind.LootBuilding || placement.LootKind != lootKind ||
                    !graph.PlacementNodes.TryGetValue(placement.PlacementId, out var node))
                {
                    continue;
                }

                var path = FindPath(graph, node);
                if (path.Found)
                {
                    best = Mathf.Min(best, path.Damage);
                }
            }

            return float.IsPositiveInfinity(best) ? -1f : best;
        }

        private static Graph BuildGraph(CastleGenerationCandidate candidate)
        {
            var graph = new Graph();
            var placementByCell = new int[candidate.GridWidth, candidate.GridHeight];
            Fill(placementByCell, -1);
            for (var placementIndex = 0; placementIndex < candidate.Placements.Count; placementIndex++)
            {
                var placement = candidate.Placements[placementIndex];
                for (var x = placement.X; x < placement.X + placement.Width; x++)
                {
                    for (var z = placement.Z; z < placement.Z + placement.Height; z++)
                    {
                        if (x >= 0 && z >= 0 && x < candidate.GridWidth && z < candidate.GridHeight && placementByCell[x, z] < 0)
                        {
                            placementByCell[x, z] = placementIndex;
                        }
                    }
                }
            }

            for (var placementIndex = 0; placementIndex < candidate.Placements.Count; placementIndex++)
            {
                var placement = candidate.Placements[placementIndex];
                if (placement.Kind == CastlePlacementKind.Defender)
                {
                    continue; // 이동을 막지 않는 수비대는 별도 압박 점수로 계산
                }

                var node = graph.Nodes.Count;
                graph.Nodes.Add(new GraphNode { Placement = placement });
                graph.PlacementNodes.Add(placement.PlacementId, node);
            }

            var nodeByCell = new int[candidate.GridWidth, candidate.GridHeight];
            Fill(nodeByCell, -1);
            for (var x = 0; x < candidate.GridWidth; x++)
            {
                for (var z = 0; z < candidate.GridHeight; z++)
                {
                    var placementIndex = placementByCell[x, z];
                    var placement = placementIndex >= 0 ? candidate.Placements[placementIndex] : null;
                    if (placement != null && placement.Kind != CastlePlacementKind.Defender)
                    {
                        nodeByCell[x, z] = graph.PlacementNodes[placement.PlacementId];
                    }
                    else
                    {
                        nodeByCell[x, z] = graph.Nodes.Count;
                        graph.Nodes.Add(new GraphNode());
                    }

                    if (x == 0 || z == 0 || x == candidate.GridWidth - 1 || z == candidate.GridHeight - 1)
                    {
                        graph.Sources.Add(nodeByCell[x, z]);
                    }
                }
            }

            for (var x = 0; x < candidate.GridWidth; x++)
            {
                for (var z = 0; z < candidate.GridHeight; z++)
                {
                    if (x + 1 < candidate.GridWidth)
                    {
                        Connect(graph, nodeByCell[x, z], nodeByCell[x + 1, z]);
                    }

                    if (z + 1 < candidate.GridHeight)
                    {
                        Connect(graph, nodeByCell[x, z], nodeByCell[x, z + 1]);
                    }
                }
            }

            return graph;
        }

        private static PathResult FindPath(Graph graph, int targetNode)
        {
            var nodeCount = graph.Nodes.Count;
            var distance = new float[nodeCount];
            var steps = new int[nodeCount];
            var previous = new int[nodeCount];
            var visited = new bool[nodeCount];
            for (var index = 0; index < nodeCount; index++)
            {
                distance[index] = float.PositiveInfinity;
                steps[index] = int.MaxValue;
                previous[index] = -1;
            }

            foreach (var source in graph.Sources)
            {
                distance[source] = EntryCost(graph.Nodes[source]);
                steps[source] = 0;
            }

            var queue = new MinHeap();
            foreach (var source in graph.Sources)
            {
                queue.Push(new QueueEntry(source, distance[source], 0));
            }

            while (queue.Count > 0)
            {
                var entry = queue.Pop();
                var current = entry.Node;
                if (visited[current] || entry.Damage > distance[current] + CostTolerance || entry.Steps != steps[current])
                {
                    continue;
                }

                visited[current] = true;
                if (current == targetNode)
                {
                    break;
                }

                foreach (var neighbor in graph.Nodes[current].Neighbors)
                {
                    if (visited[neighbor])
                    {
                        continue;
                    }

                    var candidateDistance = distance[current] + EntryCost(graph.Nodes[neighbor]);
                    var candidateSteps = steps[current] + 1;
                    if (candidateDistance < distance[neighbor] - CostTolerance ||
                        Mathf.Abs(candidateDistance - distance[neighbor]) <= CostTolerance && candidateSteps < steps[neighbor])
                    {
                        distance[neighbor] = candidateDistance;
                        steps[neighbor] = candidateSteps;
                        previous[neighbor] = current;
                        queue.Push(new QueueEntry(neighbor, candidateDistance, candidateSteps));
                    }
                }
            }

            var result = new PathResult
            {
                Found = !float.IsPositiveInfinity(distance[targetNode]),
                Damage = float.IsPositiveInfinity(distance[targetNode]) ? 0f : distance[targetNode]
            };
            if (!result.Found)
            {
                return result;
            }

            for (var node = targetNode; node >= 0; node = previous[node])
            {
                var placement = graph.Nodes[node].Placement;
                if (placement != null)
                {
                    result.PlacementIds.Add(placement.PlacementId);
                }
            }

            result.PlacementIds.Reverse();
            return result;
        }

        private static float EntryCost(GraphNode node)
        {
            return node.Placement == null ? 0f : node.Placement.EffectiveHealth;
        }

        private static void Connect(Graph graph, int first, int second)
        {
            if (first == second)
            {
                return;
            }

            if (!graph.Nodes[first].Neighbors.Contains(second))
            {
                graph.Nodes[first].Neighbors.Add(second);
            }

            if (!graph.Nodes[second].Neighbors.Contains(first))
            {
                graph.Nodes[second].Neighbors.Add(first);
            }
        }

        private static CastleDifficultyReport EmptyReport()
        {
            return new CastleDifficultyReport(false, 0f, 0f, 0f, 0f, 0f, -1f, -1f, -1f, Array.Empty<string>());
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
    }
}
