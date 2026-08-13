using System.Collections.Generic;
using ProjectMT.Shared.Items;
using UnityEngine;

namespace ProjectMT.Shared.Reward
{
    [CreateAssetMenu(menuName = "ProjectMT/Reward/Definition", fileName = "RewardDefinition")]
    public sealed class RewardDefinition : ScriptableObject // 고정 보상 한 단위를 콘텐츠가 배수로 사용
    {
        [SerializeField] private long gold;
        [SerializeField] private long commanderExperience;
        [SerializeField] private List<ItemAmount> items = new List<ItemAmount>();

        public long Gold => gold;
        public long CommanderExperience => commanderExperience;
        public IReadOnlyList<ItemAmount> Items => items;

        public bool TryCreate(long multiplier, out RewardBundle rewards)
        {
            rewards = null;
            if (multiplier < 0L || !TryValidate(out _))
            {
                return false;
            }

            if (multiplier == 0L)
            {
                rewards = RewardBundle.Empty;
                return true;
            }

            if (!TryMultiply(gold, multiplier, out var scaledGold) ||
                !TryMultiply(commanderExperience, multiplier, out var scaledExperience))
            {
                return false;
            }

            var scaledItems = new ItemAmount[items.Count];
            for (var index = 0; index < items.Count; index++)
            {
                if (!TryMultiply(items[index].Amount, multiplier, out var amount))
                {
                    return false;
                }

                scaledItems[index] = new ItemAmount(items[index].ItemId, amount);
            }

            rewards = new RewardBundle(scaledGold, scaledExperience, scaledItems);
            return true;
        }

        public bool TryValidate(out string error)
        {
            if (gold < 0L || commanderExperience < 0L)
            {
                error = $"Reward currency must not be negative. Asset={name}";
                return false;
            }

            items ??= new List<ItemAmount>();
            for (var index = 0; index < items.Count; index++)
            {
                if (!items[index].IsValid)
                {
                    error = $"Reward item is invalid. Asset={name}, Index={index}";
                    return false;
                }
            }

            if (gold <= 0L && commanderExperience <= 0L && items.Count == 0)
            {
                error = $"Reward definition is empty. Asset={name}";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryMultiply(long value, long multiplier, out long result)
        {
            if (value < 0L || multiplier < 0L || (value > 0L && multiplier > long.MaxValue / value))
            {
                result = 0L;
                return false;
            }

            result = value * multiplier;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            long goldAmount,
            long commanderExperienceAmount,
            IEnumerable<ItemAmount> itemRewards)
        {
            gold = goldAmount;
            commanderExperience = commanderExperienceAmount;
            items = itemRewards == null
                ? new List<ItemAmount>()
                : new List<ItemAmount>(itemRewards);
        }
#endif
    }
}
