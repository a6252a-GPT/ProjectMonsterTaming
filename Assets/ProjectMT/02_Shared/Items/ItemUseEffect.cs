using ProjectMT.Shared.Reward;
using UnityEngine;

namespace ProjectMT.Shared.Items
{
    public sealed class ItemUseResult // 사용 차감과 같은 후보에 적용할 지속 결과
    {
        public ItemUseResult(RewardBundle rewards)
        {
            Rewards = rewards ?? RewardBundle.Empty;
        }

        public RewardBundle Rewards { get; }
        public bool IsEmpty => Rewards.IsEmpty;
    }

    public abstract class ItemUseEffect : ScriptableObject // SO는 결과만 계산하고 저장은 GameData가 담당
    {
        public abstract bool TryCreateResult(
            long quantity,
            out ItemUseResult result,
            out string error);

        public abstract bool TryValidate(out string error);
    }
}
