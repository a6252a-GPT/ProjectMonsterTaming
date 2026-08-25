using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex
{
    public enum HexCastleAssaultTargetKind
    {
        None = 0,
        Structure = 1,
        Defender = 2,
        Palace = 3,
        Ally = 4
    }

    public enum HexCastleAssaultIntentKind
    {
        None = 0,
        InitialBreach = 1,
        Progress = 2,
        Opportunity = 3,
        Specialist = 4,
        Threat = 5,
        Support = 6,
        Palace = 7
    }

    public readonly struct HexCastleAssaultTarget
    {
        public HexCastleAssaultTarget(HexCastleCellRuntime structure, bool palace)
        {
            Structure = structure;
            Defender = null;
            Ally = null;
            Kind = palace ? HexCastleAssaultTargetKind.Palace : HexCastleAssaultTargetKind.Structure;
        }

        public HexCastleAssaultTarget(HexCastleGarrisonUnit defender)
        {
            Structure = null;
            Defender = defender;
            Ally = null;
            Kind = HexCastleAssaultTargetKind.Defender;
        }

        public HexCastleAssaultTarget(HexCastleAssaultUnit ally)
        {
            Structure = null;
            Defender = null;
            Ally = ally;
            Kind = HexCastleAssaultTargetKind.Ally;
        }

        public HexCastleAssaultTargetKind Kind { get; }
        public HexCastleCellRuntime Structure { get; }
        public HexCastleGarrisonUnit Defender { get; }
        public HexCastleAssaultUnit Ally { get; }
        public bool IsValid => Kind != HexCastleAssaultTargetKind.None && IsAlive;
        public bool IsAlive => Structure != null && Structure.IsAlive || Defender != null && Defender.IsAlive ||
                               Ally != null && Ally.IsAlive;
        public HexCoordinates Coordinates => Structure != null
            ? Structure.Coordinates
            : Defender != null
                ? Defender.Coordinates
                : Ally != null
                    ? Ally.CurrentCoordinates
                    : default;
        public int InstanceId => Structure != null
            ? Structure.GetInstanceID()
            : Defender != null
                ? Defender.GetInstanceID()
                : Ally != null
                    ? Ally.GetInstanceID()
                    : 0;
        public float CurrentHealth => Structure != null
            ? Structure.CurrentHealth
            : Defender?.Health == null
                ? Ally != null ? Ally.CurrentHealth : 0f
                : Defender.Health.CurrentHealth;
    }

    public readonly struct HexCastleAssaultDecision
    {
        public HexCastleAssaultDecision(
            HexCastleAssaultTarget target,
            IReadOnlyList<HexCoordinates> movementPath,
            HexCoordinates approach,
            int routeId,
            int sectorId,
            int topologyVersion,
            HexCastleAssaultIntentKind intent,
            HexCastleAssaultSupportAction supportAction = HexCastleAssaultSupportAction.None)
        {
            Target = target;
            MovementPath = movementPath ?? Array.Empty<HexCoordinates>();
            Approach = approach;
            RouteId = routeId;
            SectorId = sectorId;
            TopologyVersion = topologyVersion;
            Intent = intent;
            SupportAction = supportAction;
        }

        public HexCastleAssaultTarget Target { get; }
        public IReadOnlyList<HexCoordinates> MovementPath { get; }
        public HexCoordinates Approach { get; }
        public int RouteId { get; }
        public int SectorId { get; }
        public int TopologyVersion { get; }
        public HexCastleAssaultIntentKind Intent { get; }
        public HexCastleAssaultSupportAction SupportAction { get; }
        public bool IsValid => Target.IsValid && MovementPath.Count > 0;
    }

    public readonly struct HexCastleAssaultCohortAssignment
    {
        public HexCastleAssaultCohortAssignment(int cohortId, int routeId, int sectorId)
        {
            CohortId = cohortId;
            RouteId = routeId;
            SectorId = sectorId;
        }

        public int CohortId { get; }
        public int RouteId { get; }
        public int SectorId { get; }
    }

    [DisallowMultipleComponent]
    public sealed class HexCastleAssaultWorld : MonoBehaviour // Hex 공격 AI의 전략 판단과 예약을 조율한다
    {
        private const int StrategicDecisionBudgetPerFrame = 1;
        private const int MaximumOuterBreachRoutes = 4;
        private const int MaximumCohortSize = 8;
        private const int CohortJoinDistanceCells = 3;
        private const float CohortJoinSeconds = 4.5f;
        private const int SpecializedTargetRadiusCells = 4;
        private const int SharedThreatRadiusCells = 4;
        private const int ThreatSuppressorRadiusCells = 6;
        private const int SupportSearchRadiusCells = 8;
        private const float ThreatRecordSeconds = 3.5f;
        private const float TentativeSupportClaimSeconds = 0.9f;
        private const float SupportClaimPenalty = 2f;
        private const int HealthRouteBandCount = 4;
        private const float GeneralOpportunityChance = 0.35f;
        private static readonly float[] InitialWallWeights = { 0.55f, 0.30f, 0.15f };

        private sealed class CohortRecord
        {
            public int CohortId;
            public int RouteId;
            public int SectorId;
            public HexCoordinates LastCoordinates;
            public float LastJoinTime;
            public int MemberCount;
        }

        private sealed class ThreatRecord
        {
            public HexCastleAssaultTarget Target;
            public int VictimId;
            public HexCoordinates VictimCoordinates;
            public float ReportedAt;
        }

        private readonly struct SupportClaimKey : IEquatable<SupportClaimKey>
        {
            public SupportClaimKey(int targetId, HexCastleAssaultSupportAction action)
            {
                TargetId = targetId;
                Action = action;
            }

            public int TargetId { get; }
            public HexCastleAssaultSupportAction Action { get; }

            public bool Equals(SupportClaimKey other)
            {
                return TargetId == other.TargetId && Action == other.Action;
            }

            public override bool Equals(object obj)
            {
                return obj is SupportClaimKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return TargetId * 397 ^ (int)Action;
                }
            }
        }

        private sealed class SupportClaimRecord
        {
            public int OwnerId;
            public float ExpiresAt;
        }

        private readonly struct ThreatClaim : IEquatable<ThreatClaim>
        {
            public ThreatClaim(int targetId, int responderId)
            {
                TargetId = targetId;
                ResponderId = responderId;
            }

            public int TargetId { get; }
            public int ResponderId { get; }

            public bool Equals(ThreatClaim other)
            {
                return TargetId == other.TargetId && ResponderId == other.ResponderId;
            }

            public override bool Equals(object obj)
            {
                return obj is ThreatClaim other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return TargetId * 397 ^ ResponderId;
                }
            }
        }

        private IReadOnlyDictionary<HexCoordinates, HexCastleCellRuntime> cells;
        private readonly List<HexCastleAssaultUnit> units = new List<HexCastleAssaultUnit>();
        private readonly List<CohortRecord> cohorts = new List<CohortRecord>();
        private readonly Dictionary<int, HexCastleAssaultCohortAssignment> unitCohorts =
            new Dictionary<int, HexCastleAssaultCohortAssignment>();
        private readonly Dictionary<int, Dictionary<HexCoordinates, int>> attackSlots =
            new Dictionary<int, Dictionary<HexCoordinates, int>>();
        private readonly Dictionary<int, int> routeBreachTargets = new Dictionary<int, int>();
        private readonly Dictionary<int, HashSet<int>> breachRouteOwners =
            new Dictionary<int, HashSet<int>>();
        private readonly Dictionary<int, int> unitBreachRoutes = new Dictionary<int, int>();
        private readonly HashSet<int> outerBreachTargets = new HashSet<int>();
        private readonly HashSet<ThreatClaim> threatClaims = new HashSet<ThreatClaim>();
        private readonly List<ThreatRecord> threatRecords = new List<ThreatRecord>();
        private readonly Dictionary<SupportClaimKey, SupportClaimRecord> supportClaims =
            new Dictionary<SupportClaimKey, SupportClaimRecord>();
        private readonly Dictionary<int, int> unitSpawnOrders = new Dictionary<int, int>();
        private readonly Dictionary<int, int> cellHealthBands = new Dictionary<int, int>();
        private HexCastleAssaultNavigationSnapshot navigation;
        private HexCastleAssaultAIProfileCatalog profileCatalog;
        private HexCastleGarrisonWorld garrisonWorld;
        private HexCastleCellRuntime palaceCore;
        private int defenseLayerCount;
        private int topologyVersion = 1;
        private int strategicCursor;
        private int nextCohortId = 1;
        private int nextUnitSpawnOrder;
        private int stageSeed;
        private bool configured;

        public int TopologyVersion => topologyVersion;
        public int DefenseLayerCount => defenseLayerCount;
        public int ActiveUnitCount => units.Count(value => value != null && value.IsAlive);
        public int ActiveCohortCount => cohorts.Count;
        public int ActiveOuterBreachRouteCount => outerBreachTargets.Count;
        public int ActiveBreachReservationOwnerCount => unitBreachRoutes.Count;
        public int ActiveSupportClaimCount => supportClaims.Count;
        public int CachedRouteFieldCount => navigation?.CachedFieldCount ?? 0;
        public HexCastleCellRuntime PalaceCore => palaceCore;
        public IReadOnlyList<HexCastleAssaultUnit> RegisteredUnits => units;

        public event Action<HexCastleAssaultUnit> UnitRegistered;
        public event Action<HexCastleAssaultUnit> UnitUnregistered;

        public void Configure(
            IReadOnlyDictionary<HexCoordinates, HexCastleCellRuntime> runtimeCells,
            float cellSize,
            int targetDefenseLayerCount,
            HexCastleGarrisonWorld targetGarrisonWorld,
            HexCastleAssaultAIProfileCatalog targetProfileCatalog = null,
            int targetStageSeed = 0)
        {
            Shutdown();
            cells = runtimeCells ?? throw new ArgumentNullException(nameof(runtimeCells));
            defenseLayerCount = Mathf.Clamp(targetDefenseLayerCount, 2, 4);
            garrisonWorld = targetGarrisonWorld;
            stageSeed = targetStageSeed;
            profileCatalog = targetProfileCatalog != null
                ? targetProfileCatalog
                : Resources.Load<HexCastleAssaultAIProfileCatalog>(
                    HexCastleAssaultAIProfileCatalog.DefaultResourcesPath);
            palaceCore = cells.Values.FirstOrDefault(value =>
                value != null && value.Kind == HexCastleCellKind.Palace &&
                value.Coordinates == new HexCoordinates(0, 0));
            if (palaceCore == null)
            {
                throw new InvalidOperationException("Hex 왕궁 중앙 Cell이 없습니다.");
            }

            navigation = new HexCastleAssaultNavigationSnapshot(cells, cellSize);
            foreach (var cell in cells.Values)
            {
                if (cell == null)
                {
                    continue;
                }

                cell.Destroyed -= HandleCellDestroyed;
                cell.Destroyed += HandleCellDestroyed;
                cell.BlockingChanged -= HandleBlockingChanged;
                cell.BlockingChanged += HandleBlockingChanged;
                cell.Damaged -= HandleCellDamaged;
                cell.Damaged += HandleCellDamaged;
                if (cell.IsDamageable)
                {
                    cellHealthBands[cell.GetInstanceID()] = ResolveHealthRouteBand(cell);
                }
            }

            topologyVersion = 1;
            configured = true;
        }

        public HexCastleAssaultAIProfile RegisterUnit(HexCastleAssaultUnit unit, string monsterId)
        {
            if (!configured || unit == null)
            {
                throw new InvalidOperationException("Hex 공격 World를 먼저 구성해야 합니다.");
            }

            if (!units.Contains(unit))
            {
                units.Add(unit);
                unitSpawnOrders[unit.GetInstanceID()] = nextUnitSpawnOrder++;
                UnitRegistered?.Invoke(unit);
            }

            return profileCatalog == null ? new HexCastleAssaultAIProfile() : profileCatalog.Resolve(monsterId);
        }

        public void UnregisterUnit(HexCastleAssaultUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            var removed = units.Remove(unit);
            unitSpawnOrders.Remove(unit.GetInstanceID());
            ReleaseReservations(unit);
            ReleaseCohort(unit);
            if (removed)
            {
                UnitUnregistered?.Invoke(unit);
            }
        }

        public void Shutdown()
        {
            if (cells != null)
            {
                foreach (var cell in cells.Values)
                {
                    if (cell == null)
                    {
                        continue;
                    }

                    cell.Destroyed -= HandleCellDestroyed;
                    cell.BlockingChanged -= HandleBlockingChanged;
                    cell.Damaged -= HandleCellDamaged;
                }
            }

            foreach (var unit in units.Where(value => value != null).ToArray())
            {
                UnitUnregistered?.Invoke(unit);
            }
            units.Clear();
            cohorts.Clear();
            unitCohorts.Clear();
            attackSlots.Clear();
            routeBreachTargets.Clear();
            breachRouteOwners.Clear();
            unitBreachRoutes.Clear();
            outerBreachTargets.Clear();
            threatClaims.Clear();
            threatRecords.Clear();
            supportClaims.Clear();
            unitSpawnOrders.Clear();
            cellHealthBands.Clear();
            cells = null;
            navigation = null;
            profileCatalog = null;
            garrisonWorld = null;
            palaceCore = null;
            strategicCursor = 0;
            nextCohortId = 1;
            nextUnitSpawnOrder = 0;
            stageSeed = 0;
            configured = false;
        }

        public bool TryResolveDecision(
            HexCastleAssaultUnit unit,
            out HexCastleAssaultDecision decision)
        {
            decision = default;
            if (!configured || unit == null || !unit.IsAlive || navigation == null)
            {
                return false;
            }

            ReleaseThreatClaims(unit);

            var policy = ResolveRoutePolicy(unit.AIProfile?.Pattern ?? HexCastleAssaultPattern.GeneralAdvance);
            if (!navigation.TryResolveRoute(
                    unit.CurrentCoordinates,
                    policy,
                    unit.ExpectedDefenseLayer,
                    unit.EstimatedDamagePerSecond,
                    unit.MoveSpeed,
                    topologyVersion,
                    out var route))
            {
                return false;
            }

            if (TryResolveThreat(unit, route, out decision))
            {
                return true;
            }

            if (unit.AIProfile?.Pattern == HexCastleAssaultPattern.TacticalSupport &&
                TryResolveSupportTarget(unit, route, out decision))
            {
                return true;
            }

            if (unit.CommittedTarget.IsValid &&
                TryCreateTargetDecision(
                    unit,
                    unit.CommittedTarget,
                    route,
                    unit.CommittedIntent,
                    HexCastleAssaultSupportAction.None,
                    null,
                    out decision))
            {
                return true;
            }

            if (!route.HasFirstObstacle)
            {
                return TryCreateCellDecision(
                    unit,
                    palaceCore,
                    true,
                    route,
                    HexCastleAssaultIntentKind.Palace,
                    out decision);
            }

            if (!unit.HasSelectedInitialWall && TryResolveInitialBreach(unit, route, out decision))
            {
                return true;
            }

            if (TryResolveSpecializedTarget(unit, route, out decision))
            {
                return true;
            }

            if (TryResolveGeneralOpportunity(unit, route, out decision))
            {
                return true;
            }

            if (!cells.TryGetValue(route.FirstObstacle, out var obstacle) || obstacle == null || !obstacle.IsAlive)
            {
                return false;
            }

            var obstacleRouteId = ResolveDecisionRouteId(route.SectorId, obstacle.Coordinates);
            var reservesOuterBreach = IsOuterRingWall(obstacle);
            if (reservesOuterBreach && !CanReserveBreach(obstacleRouteId, obstacle))
            {
                return false;
            }

            if (!TryCreateCellDecision(
                    unit,
                    obstacle,
                    false,
                    route,
                    HexCastleAssaultIntentKind.Progress,
                    out decision,
                    reservesOuterBreach ? obstacleRouteId : null))
            {
                return false;
            }

            if (reservesOuterBreach)
            {
                CommitBreachReservation(unit, obstacleRouteId, obstacle);
            }

            return true;
        }

        public void ReportThreat(HexCastleAssaultUnit victim, HexCastleAssaultTarget target)
        {
            if (!configured || victim == null || !victim.IsAlive || !target.IsValid)
            {
                return;
            }

            var victimId = victim.GetInstanceID();
            var existing = threatRecords.FirstOrDefault(value =>
                value.VictimId == victimId && value.Target.InstanceId == target.InstanceId);
            if (existing == null)
            {
                existing = new ThreatRecord();
                threatRecords.Add(existing);
            }

            existing.Target = target;
            existing.VictimId = victimId;
            existing.VictimCoordinates = victim.CurrentCoordinates;
            existing.ReportedAt = Time.time;
            for (var index = 0; index < units.Count; index++)
            {
                var responder = units[index];
                if (responder != null && responder.IsAlive &&
                    responder.CurrentCoordinates.DistanceTo(victim.CurrentCoordinates) <= SharedThreatRadiusCells)
                {
                    responder.RequestStrategicDecision(true);
                }
            }
        }

        public void ReleaseReservations(HexCastleAssaultUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            ReleaseAttackSlot(unit);
            ReleaseThreatClaims(unit);
            ReleaseSupportClaims(unit);
            ReleaseBreachReservation(unit);
        }

        public bool IsAttackLaneOpen(HexCoordinates attacker, HexCastleAssaultTarget target)
        {
            if (!configured || !target.IsValid)
            {
                return false;
            }

            return target.Kind == HexCastleAssaultTargetKind.Ally ||
                   !HasIntactWallBetween(attacker, target.Coordinates);
        }

        public bool TryResolveSupportDecision(
            HexCastleAssaultUnit source,
            out HexCastleAssaultUnit target,
            out HexCastleAssaultSupportAction action)
        {
            target = null;
            action = HexCastleAssaultSupportAction.None;
            var profile = source?.AIProfile;
            if (profile == null || profile.Pattern != HexCastleAssaultPattern.TacticalSupport ||
                !source.CanPerformSupportAction)
            {
                return false;
            }

            var bestScore = 0.35f;
            HexCastleAssaultUnit resolvedTarget = null;
            HexCastleAssaultUnit followTarget = null;
            var resolvedAction = HexCastleAssaultSupportAction.None;
            PruneSupportClaims();
            for (var index = 0; index < units.Count; index++)
            {
                var candidate = units[index];
                if (candidate == null || candidate == source || !candidate.IsAlive ||
                    source.CurrentCoordinates.DistanceTo(candidate.CurrentCoordinates) > SupportSearchRadiusCells)
                {
                    continue;
                }

                var missingHealth = 1f - candidate.HealthRatio;
                var healScore = missingHealth * 2f + candidate.RecentDamagePerSecond * 0.02f;
                var defenseScore = candidate.RecentDamagePerSecond * 0.04f +
                                   (candidate.HasDefenseBuff ? -1f : 0.2f);
                var attackScore = candidate.HasCombatTarget ? 0.65f : 0f;
                if (candidate.HasAttackBuff)
                {
                    attackScore -= 1f;
                }

                ApplySupportFocus(profile.SupportFocus, ref healScore, ref defenseScore, ref attackScore);
                Select(HexCastleAssaultSupportAction.Heal, healScore);
                Select(HexCastleAssaultSupportAction.DefenseBuff, defenseScore);
                Select(HexCastleAssaultSupportAction.AttackBuff, attackScore);
                if (followTarget == null && candidate.HasCombatTarget)
                {
                    followTarget = candidate;
                }

                void Select(HexCastleAssaultSupportAction candidateAction, float score)
                {
                    if (IsSupportClaimedByOther(source, candidate, candidateAction))
                    {
                        score -= SupportClaimPenalty;
                    }

                    if (score <= bestScore)
                    {
                        return;
                    }

                    bestScore = score;
                    resolvedTarget = candidate;
                    resolvedAction = candidateAction;
                }
            }

            target = resolvedTarget ?? followTarget;
            action = resolvedTarget != null ? resolvedAction : HexCastleAssaultSupportAction.None;
            if (target != null && action != HexCastleAssaultSupportAction.None)
            {
                ClaimSupport(source, target, action, TentativeSupportClaimSeconds);
            }
            return target != null;
        }

        public void CommitSupportDecision(
            HexCastleAssaultUnit source,
            HexCastleAssaultUnit target,
            HexCastleAssaultSupportAction action,
            float cooldownSeconds)
        {
            ClaimSupport(source, target, action, Mathf.Clamp(cooldownSeconds, 0.5f, 1.5f));
        }

        private bool TryResolveSupportTarget(
            HexCastleAssaultUnit source,
            HexCastleAssaultRoutePlan route,
            out HexCastleAssaultDecision decision)
        {
            decision = default;
            if (!TryResolveSupportDecision(source, out var ally, out var action))
            {
                return false;
            }

            if (action == HexCastleAssaultSupportAction.None &&
                source.CurrentCoordinates.DistanceTo(ally.CurrentCoordinates) <= source.SupportRangeCells)
            {
                return false; // 지원할 일이 없는 근거리 추종은 제자리 대기로 만들지 않는다
            }

            if (TryCreateTargetDecision(
                source,
                new HexCastleAssaultTarget(ally),
                route,
                HexCastleAssaultIntentKind.Support,
                action,
                null,
                out decision))
            {
                return true;
            }

            ReleaseSupportClaim(source, ally, action);
            return false;
        }

        private void Update()
        {
            if (!configured || units.Count == 0)
            {
                return;
            }

            PruneUnits();
            PruneThreatRecords();
            var remainingBudget = StrategicDecisionBudgetPerFrame;
            for (var offset = 0; offset < units.Count && remainingBudget > 0; offset++)
            {
                var index = (strategicCursor + offset) % units.Count;
                var unit = units[index];
                if (unit == null || !unit.IsAlive || !unit.NeedsStrategicDecision)
                {
                    continue;
                }

                strategicCursor = (index + 1) % Mathf.Max(1, units.Count);
                unit.RefreshStrategicDecision();
                remainingBudget--;
            }
        }

        private bool TryResolveThreat(
            HexCastleAssaultUnit unit,
            HexCastleAssaultRoutePlan route,
            out HexCastleAssaultDecision decision)
        {
            decision = default;
            var target = unit.RecentThreat;
            if (target.IsValid && TryClaimThreat(unit, target) &&
                TryCreateTargetDecision(
                    unit,
                    target,
                    route,
                    HexCastleAssaultIntentKind.Threat,
                    HexCastleAssaultSupportAction.None,
                    null,
                    out decision))
            {
                return true;
            }

            var pattern = unit.AIProfile?.Pattern ?? HexCastleAssaultPattern.GeneralAdvance;
            var sharedRadius = pattern == HexCastleAssaultPattern.ThreatSuppressor
                ? ThreatSuppressorRadiusCells
                : pattern == HexCastleAssaultPattern.GeneralAdvance ||
                  pattern == HexCastleAssaultPattern.ResourceRaider ||
                  pattern == HexCastleAssaultPattern.TacticalSupport
                    ? SharedThreatRadiusCells
                    : 0;
            if (sharedRadius <= 0)
            {
                return false;
            }

            foreach (var record in threatRecords
                         .Where(value => value.Target.IsValid &&
                                         Time.time - value.ReportedAt <= ThreatRecordSeconds &&
                                         unit.CurrentCoordinates.DistanceTo(value.VictimCoordinates) <= sharedRadius)
                         .OrderBy(value => unit.CurrentCoordinates.DistanceTo(value.Target.Coordinates))
                         .ThenBy(value => value.Target.CurrentHealth))
            {
                if (!TryClaimThreat(unit, record.Target))
                {
                    continue;
                }

                if (TryCreateTargetDecision(
                        unit,
                        record.Target,
                        route,
                        HexCastleAssaultIntentKind.Threat,
                        HexCastleAssaultSupportAction.None,
                        null,
                        out decision))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveInitialBreach(
            HexCastleAssaultUnit unit,
            HexCastleAssaultRoutePlan route,
            out HexCastleAssaultDecision decision)
        {
            decision = default;
            var candidates = cells.Values
                .Where(value => IsRingWall(value) && value.IsAlive &&
                                value.DefenseLayer == unit.ExpectedDefenseLayer)
                .OrderBy(value => unit.CurrentCoordinates.DistanceTo(value.Coordinates))
                .ThenBy(value => value.Coordinates)
                .Take(3)
                .ToArray();
            if (candidates.Length == 0)
            {
                return false;
            }

            var selectedIndex = TryResolveJoinableCohortBreachCandidate(unit, route, candidates, out var joined)
                ? Array.IndexOf(candidates, joined)
                : ResolveWeightedInitialWallIndex(unit, candidates.Length, unit.ExpectedDefenseLayer);
            for (var offset = 0; offset < candidates.Length; offset++)
            {
                var candidate = candidates[(selectedIndex + offset) % candidates.Length];
                var routeId = ResolveDecisionRouteId(route.SectorId, candidate.Coordinates);
                if (!CanReserveBreach(routeId, candidate))
                {
                    continue;
                }

                if (TryCreateCellDecision(
                        unit,
                        candidate,
                        false,
                        route,
                        HexCastleAssaultIntentKind.InitialBreach,
                        out decision,
                        routeId))
                {
                    CommitBreachReservation(unit, routeId, candidate);
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveGeneralOpportunity(
            HexCastleAssaultUnit unit,
            HexCastleAssaultRoutePlan route,
            out HexCastleAssaultDecision decision)
        {
            decision = default;
            if (unit.AIProfile?.Pattern != HexCastleAssaultPattern.GeneralAdvance ||
                unit.HasEvaluatedOpportunity(unit.ExpectedDefenseLayer))
            {
                return false;
            }

            var roll = ResolveDeterministic01(unit, 2000 + unit.ExpectedDefenseLayer);
            unit.MarkOpportunityEvaluated(unit.ExpectedDefenseLayer);
            if (roll >= GeneralOpportunityChance)
            {
                return false;
            }

            foreach (var candidate in cells.Values
                         .Where(value => value != null && value.IsAlive && value.IsBlocked &&
                                         IsGeneralOpportunityStructure(value) &&
                                         value.Coordinates.DistanceTo(unit.CurrentCoordinates) <=
                                         SpecializedTargetRadiusCells)
                         .OrderBy(value => value.Coordinates.DistanceTo(unit.CurrentCoordinates))
                         .ThenBy(value => value.CurrentHealth))
            {
                if (TryCreateCellDecision(
                        unit,
                        candidate,
                        false,
                        route,
                        HexCastleAssaultIntentKind.Opportunity,
                        out decision))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveSpecializedTarget(
            HexCastleAssaultUnit unit,
            HexCastleAssaultRoutePlan route,
            out HexCastleAssaultDecision decision)
        {
            decision = default;
            var pattern = unit.AIProfile?.Pattern ?? HexCastleAssaultPattern.GeneralAdvance;
            var targetLimit = pattern == HexCastleAssaultPattern.ResourceRaider ? 2 : 1;
            if (!unit.CanSelectSpecialistTarget(unit.ExpectedDefenseLayer, targetLimit))
            {
                return false;
            }

            if (pattern == HexCastleAssaultPattern.DefenderHunter && garrisonWorld != null)
            {
                var defenders = garrisonWorld.Units
                    .Where(value => value != null && value.IsAlive &&
                                    unit.CurrentCoordinates.DistanceTo(value.Coordinates) <=
                                    SpecializedTargetRadiusCells)
                    .OrderBy(value => unit.CurrentCoordinates.DistanceTo(value.Coordinates));
                foreach (var defender in defenders)
                {
                    if (TryCreateTargetDecision(
                            unit,
                            new HexCastleAssaultTarget(defender),
                            route,
                            HexCastleAssaultIntentKind.Specialist,
                            HexCastleAssaultSupportAction.None,
                            null,
                            out decision))
                    {
                        return true;
                    }
                }
            }

            if (pattern != HexCastleAssaultPattern.ResourceRaider &&
                pattern != HexCastleAssaultPattern.TurretHunter)
            {
                return false;
            }

            var candidates = cells.Values
                .Where(value => value != null && value.IsAlive && value.IsBlocked &&
                                value.Coordinates.DistanceTo(unit.CurrentCoordinates) <=
                                SpecializedTargetRadiusCells &&
                                IsSpecializedStructure(value, pattern))
                .OrderBy(value => value.Coordinates.DistanceTo(unit.CurrentCoordinates))
                .ThenBy(value => value.CurrentHealth);
            foreach (var candidate in candidates)
            {
                if (TryCreateCellDecision(
                        unit,
                        candidate,
                        false,
                        route,
                        HexCastleAssaultIntentKind.Specialist,
                        out decision))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryCreateCellDecision(
            HexCastleAssaultUnit unit,
            HexCastleCellRuntime target,
            bool palace,
            HexCastleAssaultRoutePlan route,
            HexCastleAssaultIntentKind intent,
            out HexCastleAssaultDecision decision,
            int? routeIdOverride = null)
        {
            return TryCreateTargetDecision(
                unit,
                new HexCastleAssaultTarget(target, palace),
                route,
                intent,
                HexCastleAssaultSupportAction.None,
                routeIdOverride,
                out decision);
        }

        private bool TryCreateTargetDecision(
            HexCastleAssaultUnit unit,
            HexCastleAssaultTarget target,
            HexCastleAssaultRoutePlan route,
            HexCastleAssaultIntentKind intent,
            HexCastleAssaultSupportAction supportAction,
            int? routeIdOverride,
            out HexCastleAssaultDecision decision)
        {
            decision = default;
            if (!target.IsValid)
            {
                return false;
            }

            var logicalRange = target.Kind == HexCastleAssaultTargetKind.Palace
                ? HexCastleFoundationGenerator.PalaceFootprintRadius + 1
                : target.Kind == HexCastleAssaultTargetKind.Ally
                    ? Mathf.Max(1, unit.SupportRangeCells)
                    : Mathf.Max(1, unit.AttackRangeCells);
            var occupiedApproaches = target.Kind == HexCastleAssaultTargetKind.Ally
                ? Array.Empty<HexCoordinates>()
                : ResolveOccupiedApproaches(unit, target);
            Predicate<HexCoordinates> attackLanePredicate = target.Kind == HexCastleAssaultTargetKind.Ally
                ? null
                : value => IsAttackLaneOpen(value, target);
            IReadOnlyList<HexCoordinates> movementPath;
            HexCoordinates approach;
            var hasApproach = target.Kind == HexCastleAssaultTargetKind.Ally
                ? navigation.TryResolveOpenFollowRoute(
                    unit.CurrentCoordinates,
                    target.Coordinates,
                    logicalRange,
                    out movementPath,
                    out approach)
                : navigation.TryResolveOpenApproachRoute(
                    unit.CurrentCoordinates,
                    target.Coordinates,
                    logicalRange,
                    occupiedApproaches,
                    attackLanePredicate,
                    out movementPath,
                    out approach);
            if (!hasApproach)
            {
                if (target.Kind != HexCastleAssaultTargetKind.Structure ||
                    target.Coordinates != route.FirstObstacle)
                {
                    return false;
                }

                movementPath = route.Path.TakeWhile(value => value != route.FirstObstacle).ToArray();
                approach = route.FirstObstacleApproach;
                if (movementPath.Count == 0)
                {
                    movementPath = new[] { unit.CurrentCoordinates };
                }
            }

            if (target.Kind != HexCastleAssaultTargetKind.Ally &&
                !TryLeaseAttackSlot(unit, target, approach))
            {
                return false;
            }

            var assignment = ResolveCohort(unit, route, routeIdOverride);
            decision = new HexCastleAssaultDecision(
                target,
                movementPath,
                approach,
                assignment.RouteId,
                assignment.SectorId,
                topologyVersion,
                intent,
                supportAction);
            return true;
        }

        private bool HasIntactWallBetween(HexCoordinates start, HexCoordinates end)
        {
            var distance = start.DistanceTo(end);
            if (distance <= 1)
            {
                return false;
            }

            for (var step = 1; step < distance; step++)
            {
                var ratio = step / (float)distance;
                var coordinates = RoundAxial(
                    Mathf.Lerp(start.Q, end.Q, ratio),
                    Mathf.Lerp(start.R, end.R, ratio));
                if (cells.TryGetValue(coordinates, out var cell) && IsIntactWallBarrier(cell))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsIntactWallBarrier(HexCastleCellRuntime cell)
        {
            return cell != null && cell.IsAlive && cell.IsBlocked &&
                   (cell.Kind == HexCastleCellKind.Wall ||
                    cell.Kind == HexCastleCellKind.Tower ||
                    cell.Kind == HexCastleCellKind.Gate);
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

        private bool TryLeaseAttackSlot(
            HexCastleAssaultUnit unit,
            HexCastleAssaultTarget target,
            HexCoordinates approach)
        {
            ReleaseAttackSlot(unit);
            var targetId = target.InstanceId;
            if (!attackSlots.TryGetValue(targetId, out var slots))
            {
                slots = new Dictionary<HexCoordinates, int>();
                attackSlots.Add(targetId, slots);
            }

            if (slots.TryGetValue(approach, out var ownerId) && ownerId != unit.GetInstanceID())
            {
                return false;
            }

            slots[approach] = unit.GetInstanceID();
            return true;
        }

        private IReadOnlyCollection<HexCoordinates> ResolveOccupiedApproaches(
            HexCastleAssaultUnit unit,
            HexCastleAssaultTarget target)
        {
            if (!attackSlots.TryGetValue(target.InstanceId, out var slots) || slots.Count == 0)
            {
                return Array.Empty<HexCoordinates>();
            }

            var unitId = unit.GetInstanceID();
            return slots.Where(pair => pair.Value != unitId).Select(pair => pair.Key).ToArray();
        }

        private void ReleaseAttackSlot(HexCastleAssaultUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            var unitId = unit.GetInstanceID();
            foreach (var targetId in attackSlots.Keys.ToArray())
            {
                var slots = attackSlots[targetId];
                foreach (var coordinate in slots
                             .Where(pair => pair.Value == unitId)
                             .Select(pair => pair.Key)
                             .ToArray())
                {
                    slots.Remove(coordinate);
                }

                if (slots.Count == 0)
                {
                    attackSlots.Remove(targetId);
                }
            }
        }

        private void ReleaseCohort(HexCastleAssaultUnit unit)
        {
            var unitId = unit.GetInstanceID();
            if (!unitCohorts.TryGetValue(unitId, out var assignment))
            {
                return;
            }

            unitCohorts.Remove(unitId);
            var cohort = cohorts.FirstOrDefault(value => value.CohortId == assignment.CohortId);
            if (cohort == null)
            {
                return;
            }

            cohort.MemberCount = Mathf.Max(0, cohort.MemberCount - 1);
            if (cohort.MemberCount <= 0)
            {
                cohorts.Remove(cohort);
            }
        }

        private void ReleaseThreatClaims(HexCastleAssaultUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            var unitId = unit.GetInstanceID();
            foreach (var claim in threatClaims.Where(value => value.ResponderId == unitId).ToArray())
            {
                threatClaims.Remove(claim);
            }
        }

        private bool CanReserveBreach(int routeId, HexCastleCellRuntime wall)
        {
            var wallId = wall.GetInstanceID();
            if (routeBreachTargets.TryGetValue(routeId, out var reservedWallId))
            {
                return reservedWallId == wallId;
            }

            if (!outerBreachTargets.Contains(wallId) &&
                outerBreachTargets.Count >= MaximumOuterBreachRoutes)
            {
                return false;
            }

            return true;
        }

        private void CommitBreachReservation(
            HexCastleAssaultUnit unit,
            int routeId,
            HexCastleCellRuntime wall)
        {
            if (unit == null || wall == null)
            {
                return;
            }

            var unitId = unit.GetInstanceID();
            if (unitBreachRoutes.TryGetValue(unitId, out var previousRouteId) && previousRouteId != routeId)
            {
                ReleaseBreachReservation(unit);
            }

            var wallId = wall.GetInstanceID();
            routeBreachTargets[routeId] = wallId;
            outerBreachTargets.Add(wallId);
            if (!breachRouteOwners.TryGetValue(routeId, out var owners))
            {
                owners = new HashSet<int>();
                breachRouteOwners.Add(routeId, owners);
            }

            owners.Add(unitId);
            unitBreachRoutes[unitId] = routeId;
        }

        private void ReleaseBreachReservation(HexCastleAssaultUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            var unitId = unit.GetInstanceID();
            if (!unitBreachRoutes.TryGetValue(unitId, out var routeId))
            {
                return;
            }

            unitBreachRoutes.Remove(unitId);
            if (!breachRouteOwners.TryGetValue(routeId, out var owners))
            {
                return;
            }

            owners.Remove(unitId);
            if (owners.Count > 0)
            {
                return;
            }

            breachRouteOwners.Remove(routeId);
            if (!routeBreachTargets.TryGetValue(routeId, out var wallId))
            {
                return;
            }

            routeBreachTargets.Remove(routeId);
            if (!routeBreachTargets.ContainsValue(wallId))
            {
                outerBreachTargets.Remove(wallId);
            }
        }

        private bool TryResolveJoinableCohortBreachCandidate(
            HexCastleAssaultUnit unit,
            HexCastleAssaultRoutePlan route,
            IReadOnlyList<HexCastleCellRuntime> candidates,
            out HexCastleCellRuntime candidate)
        {
            candidate = null;
            var now = Time.time;
            foreach (var cohort in cohorts
                         .Where(value => value.SectorId == route.SectorId &&
                                         value.MemberCount < MaximumCohortSize &&
                                         now - value.LastJoinTime <= CohortJoinSeconds &&
                                         value.LastCoordinates.DistanceTo(unit.CurrentCoordinates) <=
                                         CohortJoinDistanceCells)
                         .OrderBy(value => value.LastCoordinates.DistanceTo(unit.CurrentCoordinates))
                         .ThenByDescending(value => value.LastJoinTime))
            {
                if (!routeBreachTargets.TryGetValue(cohort.RouteId, out var wallId))
                {
                    continue;
                }

                candidate = candidates.FirstOrDefault(value =>
                    value != null && value.IsAlive && value.GetInstanceID() == wallId);
                if (candidate != null)
                {
                    return true;
                }
            }

            return false;
        }

        private HexCastleAssaultCohortAssignment ResolveCohort(
            HexCastleAssaultUnit unit,
            HexCastleAssaultRoutePlan route,
            int? routeIdOverride)
        {
            var unitId = unit.GetInstanceID();
            if (unitCohorts.TryGetValue(unitId, out var existing))
            {
                return existing;
            }

            var now = Time.time;
            var resolvedRouteId = routeIdOverride ?? route.RouteId;
            CohortRecord selected = null;
            for (var index = 0; index < cohorts.Count; index++)
            {
                var cohort = cohorts[index];
                if (cohort.RouteId != resolvedRouteId || cohort.SectorId != route.SectorId ||
                    cohort.MemberCount >= MaximumCohortSize ||
                    now - cohort.LastJoinTime > CohortJoinSeconds ||
                    cohort.LastCoordinates.DistanceTo(unit.CurrentCoordinates) > CohortJoinDistanceCells)
                {
                    continue;
                }

                selected = cohort;
                break;
            }

            if (selected == null)
            {
                selected = new CohortRecord
                {
                    CohortId = nextCohortId++,
                    RouteId = resolvedRouteId,
                    SectorId = route.SectorId
                };
                cohorts.Add(selected);
            }

            selected.MemberCount++;
            selected.LastCoordinates = unit.CurrentCoordinates;
            selected.LastJoinTime = now;
            var assignment = new HexCastleAssaultCohortAssignment(
                selected.CohortId,
                selected.RouteId,
                selected.SectorId);
            unitCohorts[unitId] = assignment;
            return assignment;
        }

        private bool TryClaimThreat(HexCastleAssaultUnit unit, HexCastleAssaultTarget target)
        {
            var targetId = target.InstanceId;
            var responderId = unit.GetInstanceID();
            var existing = new ThreatClaim(targetId, responderId);
            if (threatClaims.Contains(existing))
            {
                return true;
            }

            var maximum = target.Kind == HexCastleAssaultTargetKind.Defender ? 2 : 3;
            if (threatClaims.Count(value => value.TargetId == targetId) >= maximum)
            {
                return false;
            }

            threatClaims.Add(existing);
            return true;
        }

        private void HandleCellDestroyed(HexCastleCellRuntime cell)
        {
            if (cell == null)
            {
                return;
            }

            var targetId = cell.GetInstanceID();
            cellHealthBands.Remove(targetId);
            attackSlots.Remove(targetId);
            foreach (var routeId in routeBreachTargets
                         .Where(pair => pair.Value == targetId)
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                if (breachRouteOwners.TryGetValue(routeId, out var owners))
                {
                    foreach (var ownerId in owners)
                    {
                        unitBreachRoutes.Remove(ownerId);
                    }
                    breachRouteOwners.Remove(routeId);
                }
                routeBreachTargets.Remove(routeId);
            }
            outerBreachTargets.Remove(targetId);
            IncrementTopology();
        }

        private void HandleBlockingChanged(HexCastleCellRuntime cell, bool blocked)
        {
            IncrementTopology();
        }

        private void HandleCellDamaged(HexCastleCellRuntime cell, ProjectMT.Shared.Combat.DamageReport report)
        {
            if (cell == null || !cell.IsAlive)
            {
                return;
            }

            var cellId = cell.GetInstanceID();
            var currentBand = ResolveHealthRouteBand(cell);
            if (cellHealthBands.TryGetValue(cellId, out var previousBand) && previousBand == currentBand)
            {
                return;
            }

            cellHealthBands[cellId] = currentBand;
            IncrementTopology();
        }

        private static int ResolveHealthRouteBand(HexCastleCellRuntime cell)
        {
            if (cell == null || cell.MaxHealth <= 0f)
            {
                return 0;
            }

            return Mathf.CeilToInt(
                Mathf.Clamp01(cell.CurrentHealth / cell.MaxHealth) * HealthRouteBandCount);
        }

        private bool IsSupportClaimedByOther(
            HexCastleAssaultUnit source,
            HexCastleAssaultUnit target,
            HexCastleAssaultSupportAction action)
        {
            if (source == null || target == null || action == HexCastleAssaultSupportAction.None)
            {
                return false;
            }

            var key = new SupportClaimKey(target.GetInstanceID(), action);
            return supportClaims.TryGetValue(key, out var claim) &&
                   claim.OwnerId != source.GetInstanceID() && claim.ExpiresAt > Time.time;
        }

        private void ClaimSupport(
            HexCastleAssaultUnit source,
            HexCastleAssaultUnit target,
            HexCastleAssaultSupportAction action,
            float durationSeconds)
        {
            if (source == null || target == null || action == HexCastleAssaultSupportAction.None)
            {
                return;
            }

            var key = new SupportClaimKey(target.GetInstanceID(), action);
            supportClaims[key] = new SupportClaimRecord
            {
                OwnerId = source.GetInstanceID(),
                ExpiresAt = Time.time + Mathf.Max(0.05f, durationSeconds)
            };
        }

        private void ReleaseSupportClaim(
            HexCastleAssaultUnit source,
            HexCastleAssaultUnit target,
            HexCastleAssaultSupportAction action)
        {
            if (source == null || target == null || action == HexCastleAssaultSupportAction.None)
            {
                return;
            }

            var key = new SupportClaimKey(target.GetInstanceID(), action);
            if (supportClaims.TryGetValue(key, out var claim) && claim.OwnerId == source.GetInstanceID())
            {
                supportClaims.Remove(key);
            }
        }

        private void ReleaseSupportClaims(HexCastleAssaultUnit unit)
        {
            if (unit == null)
            {
                return;
            }

            var ownerId = unit.GetInstanceID();
            foreach (var key in supportClaims
                         .Where(value => value.Value.OwnerId == ownerId)
                         .Select(value => value.Key)
                         .ToArray())
            {
                supportClaims.Remove(key);
            }
        }

        private void PruneSupportClaims()
        {
            foreach (var key in supportClaims
                         .Where(value => value.Value.ExpiresAt <= Time.time)
                         .Select(value => value.Key)
                         .ToArray())
            {
                supportClaims.Remove(key);
            }
        }

        private void IncrementTopology()
        {
            topologyVersion++;
            navigation?.Invalidate();
            for (var index = 0; index < units.Count; index++)
            {
                units[index]?.RequestStrategicDecision(true);
            }
        }

        private void PruneUnits()
        {
            for (var index = units.Count - 1; index >= 0; index--)
            {
                if (units[index] == null)
                {
                    units.RemoveAt(index);
                }
            }

            if (strategicCursor >= units.Count)
            {
                strategicCursor = 0;
            }
        }

        private void PruneThreatRecords()
        {
            for (var index = threatRecords.Count - 1; index >= 0; index--)
            {
                var record = threatRecords[index];
                if (record == null || !record.Target.IsValid ||
                    Time.time - record.ReportedAt > ThreatRecordSeconds)
                {
                    threatRecords.RemoveAt(index);
                }
            }
        }

        private int ResolveWeightedInitialWallIndex(
            HexCastleAssaultUnit unit,
            int candidateCount,
            int layer)
        {
            var roll = ResolveDeterministic01(unit, 1000 + layer);
            var accumulated = 0f;
            for (var index = 0; index < candidateCount; index++)
            {
                accumulated += InitialWallWeights[index];
                if (roll < accumulated)
                {
                    return index;
                }
            }

            return candidateCount - 1;
        }

        private float ResolveDeterministic01(HexCastleAssaultUnit unit, int salt)
        {
            unitSpawnOrders.TryGetValue(unit.GetInstanceID(), out var spawnOrder);
            unchecked
            {
                uint value = (uint)(stageSeed * 73856093 ^ spawnOrder * 19349663 ^ salt * 83492791);
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return (value & 0x00FFFFFFu) / 16777216f;
            }
        }

        private static int ResolveDecisionRouteId(int sector, HexCoordinates anchor)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + sector;
                hash = hash * 31 + anchor.Q;
                hash = hash * 31 + anchor.R;
                return hash;
            }
        }

        private static HexCastleAssaultRoutePolicy ResolveRoutePolicy(HexCastleAssaultPattern pattern)
        {
            switch (pattern)
            {
                case HexCastleAssaultPattern.ResourceRaider:
                    return HexCastleAssaultRoutePolicy.ResourceRaider;
                case HexCastleAssaultPattern.TurretHunter:
                    return HexCastleAssaultRoutePolicy.TurretHunter;
                case HexCastleAssaultPattern.WallBreaker:
                    return HexCastleAssaultRoutePolicy.WallBreaker;
                case HexCastleAssaultPattern.ThreatSuppressor:
                    return HexCastleAssaultRoutePolicy.DirectAdvance;
                default:
                    return HexCastleAssaultRoutePolicy.Balanced;
            }
        }

        private bool IsOuterRingWall(HexCastleCellRuntime cell)
        {
            return cell != null && cell.DefenseLayer == defenseLayerCount &&
                   cell.WallRole != HexCastleWallRole.Partition &&
                   (cell.Kind == HexCastleCellKind.Wall || cell.Kind == HexCastleCellKind.Tower ||
                    cell.Kind == HexCastleCellKind.Gate);
        }

        private static bool IsRingWall(HexCastleCellRuntime cell)
        {
            return cell != null && cell.WallRole != HexCastleWallRole.None &&
                   cell.WallRole != HexCastleWallRole.Partition &&
                   (cell.Kind == HexCastleCellKind.Wall || cell.Kind == HexCastleCellKind.Tower ||
                    cell.Kind == HexCastleCellKind.Gate);
        }

        private static bool IsGeneralOpportunityStructure(HexCastleCellRuntime cell)
        {
            return cell != null && cell.BuildingRole != HexCastleBuildingRole.Turret &&
                   (cell.Kind == HexCastleCellKind.Building ||
                    cell.Kind == HexCastleCellKind.RewardBuilding ||
                    cell.Kind == HexCastleCellKind.DefenseBuilding);
        }

        private static bool IsSpecializedStructure(
            HexCastleCellRuntime cell,
            HexCastleAssaultPattern pattern)
        {
            if (pattern == HexCastleAssaultPattern.TurretHunter)
            {
                return cell.BuildingRole == HexCastleBuildingRole.Turret;
            }

            return cell.BuildingRole == HexCastleBuildingRole.GoldStorage ||
                   cell.BuildingRole == HexCastleBuildingRole.EquipmentForge ||
                   cell.BuildingRole == HexCastleBuildingRole.KeyVault;
        }

        private static void ApplySupportFocus(
            HexCastleAssaultSupportFocus focus,
            ref float healScore,
            ref float defenseScore,
            ref float attackScore)
        {
            switch (focus)
            {
                case HexCastleAssaultSupportFocus.AttackBuff:
                    attackScore += 0.45f;
                    break;
                case HexCastleAssaultSupportFocus.DefenseBuff:
                    defenseScore += 0.45f;
                    break;
                case HexCastleAssaultSupportFocus.Recovery:
                    healScore += 0.45f;
                    break;
            }
        }
    }
}
