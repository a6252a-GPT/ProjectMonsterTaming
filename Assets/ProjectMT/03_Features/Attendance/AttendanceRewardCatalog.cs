using System;
using System.Collections.Generic;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
using UnityEngine;

namespace ProjectMT.Features.Attendance
{
    [CreateAssetMenu(menuName = "ProjectMT/Attendance/Reward Catalog", fileName = "AttendanceRewardCatalog")]
    public sealed class AttendanceRewardCatalog : ScriptableObject // 28일 출석 보상표
    {
        [Serializable]
        public struct Entry
        {
            [SerializeField] private int day;
            [SerializeField] private string itemId;
            [SerializeField] private long amount;
            [SerializeField] private bool milestone;

            public Entry(int rewardDay, string rewardItemId, long rewardAmount, bool isMilestone)
            {
                day = rewardDay;
                itemId = rewardItemId?.Trim();
                amount = rewardAmount;
                milestone = isMilestone;
            }

            public int Day => day;
            public string ItemId => itemId?.Trim() ?? string.Empty;
            public long Amount => amount;
            public bool IsMilestone => milestone;
            public bool IsValid => day is >= 1 and <= 28 && !string.IsNullOrWhiteSpace(ItemId) && amount > 0L;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public bool TryGet(int day, out Entry entry)
        {
            for (var index = 0; index < entries.Count; index++)
            {
                if (entries[index].Day == day)
                {
                    entry = entries[index];
                    return entry.IsValid;
                }
            }

            entry = default;
            return false;
        }

        public bool TryCreateReward(int day, out RewardBundle reward)
        {
            if (!TryGet(day, out var entry))
            {
                reward = RewardBundle.Empty;
                return false;
            }

            reward = RewardBundle.FromItems(new ItemAmount(entry.ItemId, entry.Amount));
            return true;
        }

        public bool TryValidate(out string error)
        {
            if (entries == null || entries.Count != 28)
            {
                error = "Attendance reward catalog must contain exactly 28 entries.";
                return false;
            }

            var days = new HashSet<int>();
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (!entry.IsValid || !days.Add(entry.Day))
                {
                    error = $"Attendance reward entry is invalid or duplicated. Index={index}, Day={entry.Day}";
                    return false;
                }

                var expectedMilestone = entry.Day % 7 == 0;
                if (entry.IsMilestone != expectedMilestone)
                {
                    error = $"Attendance milestone flag is invalid. Day={entry.Day}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void EditorSetEntries(IEnumerable<Entry> values)
        {
            entries = values == null ? new List<Entry>() : new List<Entry>(values);
        }
#endif
    }
}
