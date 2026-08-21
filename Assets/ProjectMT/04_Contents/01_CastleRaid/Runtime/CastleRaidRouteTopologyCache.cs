using System.Collections.Generic;

namespace ProjectMT.Contents.CastleRaid
{
    public sealed class CastleRaidRouteTopologyCache // 성 구조 변경 때만 돌파 연결성을 다시 계산한다
    {
        private readonly Dictionary<int, ContinuationEntry> routeContinuations =
            new Dictionary<int, ContinuationEntry>();
        private readonly Dictionary<int, ContinuationEntry> prospectiveContinuations =
            new Dictionary<int, ContinuationEntry>();
        private readonly Dictionary<int, ContinuationEntry> safeBreachPlans =
            new Dictionary<int, ContinuationEntry>();

        private int version = 1;

        public int Version => version;
        public int CachedRouteCount => routeContinuations.Count;

        public bool TryGetContinuation(int routeId, out bool hasContinuation)
        {
            if (routeContinuations.TryGetValue(routeId, out var entry) && entry.Version == version)
            {
                hasContinuation = entry.HasContinuation;
                return true;
            }

            hasContinuation = false;
            return false;
        }

        public void StoreContinuation(int routeId, bool hasContinuation)
        {
            routeContinuations[routeId] = new ContinuationEntry(version, hasContinuation);
        }

        public bool TryGetProspectiveContinuation(int wallId, out bool hasContinuation)
        {
            return TryGetCurrent(prospectiveContinuations, wallId, out hasContinuation);
        }

        public void StoreProspectiveContinuation(int wallId, bool hasContinuation)
        {
            prospectiveContinuations[wallId] = new ContinuationEntry(version, hasContinuation);
        }

        public bool TryGetSafeBreachPlan(int wallId, out bool isSafe)
        {
            return TryGetCurrent(safeBreachPlans, wallId, out isSafe);
        }

        public void StoreSafeBreachPlan(int wallId, bool isSafe)
        {
            safeBreachPlans[wallId] = new ContinuationEntry(version, isSafe);
        }

        public void Invalidate()
        {
            if (version == int.MaxValue)
            {
                routeContinuations.Clear();
                prospectiveContinuations.Clear();
                safeBreachPlans.Clear();
                version = 1;
                return;
            }

            version++;
        }

        public void Reset()
        {
            routeContinuations.Clear();
            prospectiveContinuations.Clear();
            safeBreachPlans.Clear();
            version = 1;
        }

        private bool TryGetCurrent(
            IReadOnlyDictionary<int, ContinuationEntry> entries,
            int key,
            out bool value)
        {
            if (entries.TryGetValue(key, out var entry) && entry.Version == version)
            {
                value = entry.HasContinuation;
                return true;
            }

            value = false;
            return false;
        }

        private readonly struct ContinuationEntry
        {
            public ContinuationEntry(int version, bool hasContinuation)
            {
                Version = version;
                HasContinuation = hasContinuation;
            }

            public int Version { get; }
            public bool HasContinuation { get; }
        }
    }
}
