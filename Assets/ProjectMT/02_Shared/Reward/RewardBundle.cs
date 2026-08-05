using System;

namespace ProjectMT.Shared.Reward
{
    public sealed class RewardBundle // 실제 지급에 사용하는 최소 보상 묶음
    {
        public static readonly RewardBundle Empty = new RewardBundle(0L);

        public RewardBundle(long gold)
        {
            Gold = Math.Max(0L, gold);
        }

        public long Gold { get; }
        public bool IsEmpty => Gold <= 0L;

        public static RewardBundle FromGold(long amount)
        {
            return amount <= 0L ? Empty : new RewardBundle(amount);
        }
    }
}
