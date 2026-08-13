using System;
using System.Collections.Generic;
using ProjectMT.Shared.Reward;
using UnityEngine;

namespace ProjectMT.Shared.Items
{
    [CreateAssetMenu(menuName = "ProjectMT/Items/Fixed Reward Use Effect", fileName = "ItemUse_FixedReward")]
    public sealed class FixedRewardItemUseEffect : ItemUseEffect // 상자·교환권용 고정 보상 효과
    {
        [SerializeField] private long goldPerUse;
        [SerializeField] private long commanderExperiencePerUse;
        [SerializeField] private List<ItemAmount> itemRewardsPerUse = new List<ItemAmount>();

        public override bool TryCreateResult(
            long quantity,
            out ItemUseResult result,
            out string error)
        {
            result = null;
            error = null;
            if (quantity <= 0L)
            {
                error = "Item use quantity must be positive.";
                return false;
            }

            if (!TryValidate(out error) ||
                !TryMultiply(goldPerUse, quantity, out var gold) ||
                !TryMultiply(commanderExperiencePerUse, quantity, out var experience))
            {
                error ??= "Item use reward quantity overflowed.";
                return false;
            }

            var itemRewards = new ItemAmount[itemRewardsPerUse.Count];
            for (var index = 0; index < itemRewardsPerUse.Count; index++)
            {
                var reward = itemRewardsPerUse[index];
                if (!TryMultiply(reward.Amount, quantity, out var amount))
                {
                    error = $"Item use reward quantity overflowed. Item={reward.ItemId}";
                    return false;
                }

                itemRewards[index] = new ItemAmount(reward.ItemId, amount);
            }

            var rewards = new RewardBundle(gold, experience, itemRewards);
            if (rewards.IsEmpty)
            {
                error = "Item use reward is empty.";
                return false;
            }

            result = new ItemUseResult(rewards);
            error = null;
            return true;
        }

        public override bool TryValidate(out string error)
        {
            if (goldPerUse < 0L || commanderExperiencePerUse < 0L)
            {
                error = "Item use reward currency must not be negative.";
                return false;
            }

            itemRewardsPerUse ??= new List<ItemAmount>();
            for (var index = 0; index < itemRewardsPerUse.Count; index++)
            {
                if (!itemRewardsPerUse[index].IsValid)
                {
                    error = $"Item use reward is invalid. Index={index}";
                    return false;
                }
            }

            if (goldPerUse <= 0L && commanderExperiencePerUse <= 0L && itemRewardsPerUse.Count == 0)
            {
                error = "Item use reward is empty.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryMultiply(long value, long multiplier, out long result)
        {
            if (value < 0L || multiplier <= 0L || (value > 0L && multiplier > long.MaxValue / value))
            {
                result = 0L;
                return false;
            }

            result = value * multiplier;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            long gold,
            long commanderExperience,
            IEnumerable<ItemAmount> itemRewards)
        {
            goldPerUse = gold;
            commanderExperiencePerUse = commanderExperience;
            itemRewardsPerUse = itemRewards == null
                ? new List<ItemAmount>()
                : new List<ItemAmount>(itemRewards);
        }
#endif
    }
}
