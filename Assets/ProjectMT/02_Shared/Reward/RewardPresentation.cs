using System;
using System.Collections.Generic;

namespace ProjectMT.Shared.Reward
{
    public enum RewardPresentationKind // 지급 로직과 분리된 표시 종류
    {
        Gold,
        CommanderExperience,
        Item
    }

    public readonly struct RewardPresentationItem // 저장 성공 뒤 보여줄 읽기 전용 값
    {
        public RewardPresentationItem(RewardPresentationKind kind, string label, long amount)
        {
            Kind = kind;
            Label = label ?? string.Empty;
            Amount = Math.Max(0L, amount);
        }

        public RewardPresentationKind Kind { get; }
        public string Label { get; }
        public long Amount { get; }
        public bool IsValid => Amount > 0L;
    }

    public sealed class RewardPresentationRequest // 실제 지급 권한이 없는 연출 전용 묶음
    {
        private readonly RewardPresentationItem[] items;

        public RewardPresentationRequest(params RewardPresentationItem[] rewardItems)
        {
            if (rewardItems == null || rewardItems.Length == 0)
            {
                items = Array.Empty<RewardPresentationItem>();
                return;
            }

            var validItems = new List<RewardPresentationItem>(rewardItems.Length);
            for (var i = 0; i < rewardItems.Length; i++)
            {
                if (rewardItems[i].IsValid)
                {
                    validItems.Add(rewardItems[i]);
                }
            }

            items = validItems.ToArray();
        }

        public IReadOnlyList<RewardPresentationItem> Items => items;
        public bool IsEmpty => items.Length == 0;

        public static RewardPresentationRequest Gold(long amount)
        {
            return new RewardPresentationRequest(
                new RewardPresentationItem(RewardPresentationKind.Gold, "골드", amount));
        }

        public static RewardPresentationRequest FromBundle(RewardBundle bundle)
        {
            return bundle == null || bundle.IsEmpty
                ? new RewardPresentationRequest()
                : Gold(bundle.Gold);
        }
    }

    public interface IRewardPresentationPlayer // 저장 성공 뒤 Bootstrap이 호출하는 표현 계약
    {
        void PlayConfirmed(RewardPresentationRequest request);
    }
}
