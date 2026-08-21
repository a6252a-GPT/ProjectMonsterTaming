using System;
using UnityEngine;

namespace ProjectMT.Shared.GameData
{
    [Serializable]
    public sealed class AttendanceProgressData // 28일 누적 출석 원본
    {
        public const int TotalDays = 28;

        [SerializeField] private int nextRewardDay = 1;
        [SerializeField] private int pendingRewardDay;
        [SerializeField] private int cycle = 1;
        [SerializeField] private long lastProcessedPeriod = -1L;
        [SerializeField] private long lastClaimedPeriod = -1L;

        public int NextRewardDay => Mathf.Clamp(nextRewardDay, 1, TotalDays);
        public int PendingRewardDay => pendingRewardDay is >= 1 and <= TotalDays ? pendingRewardDay : 0;
        public int Cycle => Math.Max(1, cycle);
        public long LastProcessedPeriod => Math.Max(-1L, lastProcessedPeriod);
        public long LastClaimedPeriod => Math.Max(-1L, lastClaimedPeriod);
        public bool HasPendingReward => PendingRewardDay > 0;
        public int ClaimedThroughDay => HasPendingReward ? PendingRewardDay - 1 : NextRewardDay - 1;

        public static AttendanceProgressData CreateDefault()
        {
            return new AttendanceProgressData();
        }

        public AttendanceProgressData Clone()
        {
            return new AttendanceProgressData
            {
                nextRewardDay = NextRewardDay,
                pendingRewardDay = PendingRewardDay,
                cycle = Cycle,
                lastProcessedPeriod = LastProcessedPeriod,
                lastClaimedPeriod = LastClaimedPeriod
            };
        }

        internal bool TryRefresh(long expectedPeriod, long nextPeriod)
        {
            if (LastProcessedPeriod != expectedPeriod || nextPeriod <= LastProcessedPeriod)
            {
                return false;
            }

            lastProcessedPeriod = nextPeriod;
            if (!HasPendingReward)
            {
                pendingRewardDay = NextRewardDay; // 미수령이면 같은 일차를 유지
            }

            return true;
        }

        internal bool TryClaim(int expectedDay, long expectedPeriod)
        {
            if (!HasPendingReward || PendingRewardDay != expectedDay || NextRewardDay != expectedDay ||
                LastProcessedPeriod != expectedPeriod)
            {
                return false;
            }

            pendingRewardDay = 0;
            lastClaimedPeriod = expectedPeriod;
            if (expectedDay >= TotalDays)
            {
                nextRewardDay = 1;
                cycle = Cycle + 1;
            }
            else
            {
                nextRewardDay = expectedDay + 1;
            }

            return true;
        }

        internal void Repair()
        {
            nextRewardDay = NextRewardDay;
            pendingRewardDay = PendingRewardDay;
            cycle = Cycle;
            lastProcessedPeriod = LastProcessedPeriod;
            lastClaimedPeriod = LastClaimedPeriod;
            if (pendingRewardDay > 0)
            {
                nextRewardDay = pendingRewardDay;
            }
        }

        public AttendanceProgressView CreateView()
        {
            return new AttendanceProgressView(this);
        }
    }

    public readonly struct AttendanceProgressView
    {
        internal AttendanceProgressView(AttendanceProgressData data)
        {
            NextRewardDay = data?.NextRewardDay ?? 1;
            PendingRewardDay = data?.PendingRewardDay ?? 0;
            Cycle = data?.Cycle ?? 1;
            LastProcessedPeriod = data?.LastProcessedPeriod ?? -1L;
            LastClaimedPeriod = data?.LastClaimedPeriod ?? -1L;
            ClaimedThroughDay = data?.ClaimedThroughDay ?? 0;
        }

        public int NextRewardDay { get; }
        public int PendingRewardDay { get; }
        public int Cycle { get; }
        public long LastProcessedPeriod { get; }
        public long LastClaimedPeriod { get; }
        public int ClaimedThroughDay { get; }
        public bool HasPendingReward => PendingRewardDay > 0;
    }
}
