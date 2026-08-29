using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    public readonly struct CastleRaidCohortAssignment
    {
        public CastleRaidCohortAssignment(int cohortId, int routeId, int sectorId)
        {
            CohortId = cohortId;
            RouteId = routeId;
            SectorId = sectorId;
        }

        public int CohortId { get; }
        public int RouteId { get; }
        public int SectorId { get; }
        public bool IsValid => CohortId > 0 && RouteId != 0;
    }

    public sealed class CastleRaidAssaultCoordinator // 순차 소환 유닛을 느슨한 경로 집단으로만 묶는다
    {
        private const float CohortJoinDistance = 6f;
        private const float CohortJoinSeconds = 4.5f;
        private const int MaximumCohortMembers = 8;

        private sealed class CohortState
        {
            public int CohortId;
            public int RouteId;
            public int SectorId;
            public Vector3 LastSpawnPosition;
            public float LastSpawnTime;
            public int MemberCount;
        }

        private readonly List<CohortState> cohorts = new List<CohortState>();
        private readonly Dictionary<int, CastleRaidCohortAssignment> assignments =
            new Dictionary<int, CastleRaidCohortAssignment>();
        private int nextCohortId = 1;

        public int CohortCount => cohorts.Count;

        public CastleRaidCohortAssignment RegisterSequentialSpawn(
            int unitId,
            Vector3 spawnPosition,
            CastleRaidRoutePlan routePlan,
            float spawnTime)
        {
            if (unitId == 0 || !routePlan.IsValid)
            {
                return default;
            }

            if (assignments.TryGetValue(unitId, out var existing))
            {
                return existing; // 늦게 소환된 유닛이 기존 유닛의 경로를 다시 쓰지 않는다
            }

            CohortState best = null;
            var bestDistance = float.PositiveInfinity;
            for (var index = 0; index < cohorts.Count; index++)
            {
                var candidate = cohorts[index];
                var distance = PlanarDistance(candidate.LastSpawnPosition, spawnPosition);
                if (candidate.RouteId != routePlan.RouteId ||
                    CircularSectorDistance(candidate.SectorId, routePlan.SectorId) > 1 ||
                    spawnTime - candidate.LastSpawnTime > CohortJoinSeconds ||
                    candidate.MemberCount >= MaximumCohortMembers ||
                    distance > CohortJoinDistance || distance >= bestDistance)
                {
                    continue;
                }

                best = candidate;
                bestDistance = distance;
            }

            if (best == null)
            {
                best = new CohortState
                {
                    CohortId = nextCohortId++,
                    RouteId = routePlan.RouteId,
                    SectorId = routePlan.SectorId
                };
                cohorts.Add(best);
            }

            best.LastSpawnPosition = spawnPosition;
            best.LastSpawnTime = spawnTime;
            best.MemberCount++;
            var assignment = new CastleRaidCohortAssignment(best.CohortId, routePlan.RouteId, routePlan.SectorId);
            assignments.Add(unitId, assignment);
            return assignment;
        }

        public bool TryGetAssignment(int unitId, out CastleRaidCohortAssignment assignment)
        {
            return assignments.TryGetValue(unitId, out assignment);
        }

        public void Remove(int unitId)
        {
            if (!assignments.TryGetValue(unitId, out var assignment))
            {
                return;
            }

            assignments.Remove(unitId);
            for (var index = cohorts.Count - 1; index >= 0; index--)
            {
                var cohort = cohorts[index];
                if (cohort.CohortId != assignment.CohortId)
                {
                    continue;
                }

                cohort.MemberCount = Mathf.Max(0, cohort.MemberCount - 1);
                if (cohort.MemberCount == 0)
                {
                    cohorts.RemoveAt(index);
                }

                break;
            }
        }

        public void Clear()
        {
            cohorts.Clear();
            assignments.Clear();
            nextCohortId = 1;
        }

        private static int CircularSectorDistance(int left, int right)
        {
            var direct = Mathf.Abs(left - right) % 8;
            return Mathf.Min(direct, 8 - direct);
        }

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            var delta = left - right;
            delta.y = 0f;
            return delta.magnitude;
        }
    }

    public sealed class CastleRaidRouteBreachLedger // 전역 대기 대신 경로별 외곽 돌파 하나만 예약
    {
        private readonly Dictionary<int, int> wallByRoute = new Dictionary<int, int>();
        private readonly List<int> releaseBuffer = new List<int>();

        public int Count => wallByRoute.Count;

        public bool TryReserve(int routeId, int wallId)
        {
            if (routeId == 0 || wallId == 0)
            {
                return false;
            }

            if (wallByRoute.TryGetValue(routeId, out var reservedWallId))
            {
                return reservedWallId == wallId;
            }

            wallByRoute.Add(routeId, wallId);
            return true;
        }

        public bool HasDifferentReservation(int routeId, int wallId)
        {
            return routeId != 0 && wallByRoute.TryGetValue(routeId, out var reservedWallId) &&
                   reservedWallId != wallId;
        }

        public void ReleaseWall(int wallId)
        {
            if (wallId == 0 || wallByRoute.Count == 0)
            {
                return;
            }

            releaseBuffer.Clear();
            foreach (var reservation in wallByRoute)
            {
                if (reservation.Value == wallId)
                {
                    releaseBuffer.Add(reservation.Key);
                }
            }

            for (var index = 0; index < releaseBuffer.Count; index++)
            {
                wallByRoute.Remove(releaseBuffer[index]);
            }
            releaseBuffer.Clear();
        }

        public void Clear()
        {
            wallByRoute.Clear();
            releaseBuffer.Clear();
        }
    }
}
