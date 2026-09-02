using System.Collections;
using System.Collections.Generic;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Pooling;
using ProjectMT.Shared.Stats;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public sealed partial class CombatWorld
    {
        public UnitActor FindNearestOpponent(UnitActor seeker, float maxDistance)
        {
            return FindOpponent(seeker, maxDistance, UnitTargetPriority.Nearest);
        }

        public UnitActor FindOpponent(UnitActor seeker, float maxDistance, UnitTargetPriority priority)
        {
            return FindOpponent(seeker, maxDistance, priority, 0f);
        }

        public UnitActor FindOpponent(
            UnitActor seeker,
            float maxDistance,
            UnitTargetPriority priority,
            float targetLoadPenalty)
        {
            if (seeker == null)
            {
                return null;
            }

            var maxDistanceSquared = float.IsPositiveInfinity(maxDistance) ? float.PositiveInfinity : maxDistance * maxDistance; // 제곱 거리로 비교
            var bestScore = float.PositiveInfinity;
            UnitActor best = null;
            for (var i = 0; i < units.Count; i++)
            {
                var candidate = units[i];
                if (candidate == null || candidate == seeker || !candidate.IsAlive ||
                    !candidate.IsCombatReady || candidate.Team == seeker.Team)
                {
                    continue;
                }

                var offset = candidate.transform.position - seeker.transform.position;
                offset.y = 0f;
                var distanceSquared = offset.sqrMagnitude;
                if (distanceSquared > maxDistanceSquared)
                {
                    continue;
                }

                var score = priority switch
                {
                    UnitTargetPriority.LowestHealth => ResolveHealthRatio(candidate) * 10000f + distanceSquared,
                    UnitTargetPriority.RangedFirst => (candidate.IsRanged ? 0f : 1000000f) + distanceSquared,
                    _ => distanceSquared
                };
                score += CountAlliedAttackers(seeker, candidate) * Mathf.Max(0f, targetLoadPenalty) * 9f;
                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                best = candidate;
            }

            return best;
        }

        private int CountAlliedAttackers(UnitActor seeker, UnitActor target)
        {
            var count = 0;
            for (var index = 0; index < units.Count; index++)
            {
                var ally = units[index];
                if (ally != null && ally != seeker && ally.IsAlive && ally.IsCombatReady &&
                    ally.Team == seeker.Team && ally.Target == target)
                {
                    count++;
                }
            }

            return count;
        }

        public int CountAlive(UnitTeam team)
        {
            var count = 0;
            for (var i = 0; i < units.Count; i++)
            {
                if (units[i] != null && units[i].IsAlive && units[i].Team == team)
                {
                    count++;
                }
            }

            return count;
        }

        public void CollectUnits(
            UnitTeam team,
            Vector3 center,
            float radius,
            int maxCount,
            List<UnitActor> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            var radiusSquared = Mathf.Max(0f, radius) * Mathf.Max(0f, radius);
            maxCount = Mathf.Max(1, maxCount);
            for (var unitIndex = 0; unitIndex < units.Count; unitIndex++)
            {
                var candidate = units[unitIndex];
                if (candidate == null || !candidate.IsAlive || !candidate.IsCombatReady || candidate.Team != team)
                {
                    continue;
                }

                var offset = candidate.transform.position - center;
                offset.y = 0f;
                var distanceSquared = offset.sqrMagnitude;
                if (distanceSquared > radiusSquared)
                {
                    continue;
                }

                var insertIndex = 0;
                while (insertIndex < destination.Count)
                {
                    var existingOffset = destination[insertIndex].transform.position - center;
                    existingOffset.y = 0f;
                    if (distanceSquared < existingOffset.sqrMagnitude)
                    {
                        break;
                    }

                    insertIndex++;
                }

                destination.Insert(insertIndex, candidate);
                if (destination.Count > maxCount)
                {
                    destination.RemoveAt(destination.Count - 1);
                }
            }
        }

        public void CollectUnitsInFan(
            UnitTeam team,
            Vector3 origin,
            Vector3 forward,
            float range,
            float angle,
            int maxCount,
            List<UnitActor> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
            range = Mathf.Max(0.05f, range);
            var minimumDot = Mathf.Cos(Mathf.Clamp(angle, 5f, 180f) * 0.5f * Mathf.Deg2Rad);
            maxCount = Mathf.Max(1, maxCount);
            for (var index = 0; index < units.Count; index++)
            {
                var candidate = units[index];
                if (candidate == null || !candidate.IsAlive || !candidate.IsCombatReady || candidate.Team != team)
                {
                    continue;
                }

                var offset = candidate.transform.position - origin;
                offset.y = 0f;
                var distance = offset.magnitude;
                if (distance > range + candidate.BodyRadius ||
                    (distance > 0.001f && Vector3.Dot(forward, offset / distance) < minimumDot))
                {
                    continue;
                }

                InsertByDistance(destination, candidate, origin, maxCount);
            }
        }

        public void CollectUnitsInLine(
            UnitTeam team,
            Vector3 origin,
            Vector3 forward,
            float length,
            float width,
            int maxCount,
            List<UnitActor> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
            length = Mathf.Max(0.05f, length);
            var halfWidth = Mathf.Max(0.025f, width * 0.5f);
            maxCount = Mathf.Max(1, maxCount);
            for (var index = 0; index < units.Count; index++)
            {
                var candidate = units[index];
                if (candidate == null || !candidate.IsAlive || !candidate.IsCombatReady || candidate.Team != team)
                {
                    continue;
                }

                var offset = candidate.transform.position - origin;
                offset.y = 0f;
                var longitudinal = Vector3.Dot(offset, forward);
                var lateral = (offset - forward * longitudinal).magnitude;
                if (longitudinal < -candidate.BodyRadius ||
                    longitudinal > length + candidate.BodyRadius ||
                    lateral > halfWidth + candidate.BodyRadius)
                {
                    continue;
                }

                InsertByDistance(destination, candidate, origin, maxCount);
            }
        }

        private static void InsertByDistance(
            List<UnitActor> destination,
            UnitActor candidate,
            Vector3 origin,
            int maxCount)
        {
            var distanceSquared = (candidate.transform.position - origin).sqrMagnitude;
            var insertIndex = 0;
            while (insertIndex < destination.Count &&
                   (destination[insertIndex].transform.position - origin).sqrMagnitude <= distanceSquared)
            {
                insertIndex++;
            }

            destination.Insert(insertIndex, candidate);
            if (destination.Count > maxCount)
            {
                destination.RemoveAt(destination.Count - 1);
            }
        }
    }
}
