using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.Quest
{
    // 퀘스트 1개의 진행도·완료 여부·보상 수령 여부 저장 원본.
    // GameProgressData에 편입되어 GameProgressChange·TryApply를 거쳐야만 값이 바뀐다(다른 저장 데이터와 동일).
    [Serializable]
    public sealed class QuestProgressEntryData
    {
        [SerializeField] private QuestId questId;
        [SerializeField] private long currentProgress;
        [SerializeField] private bool completed;
        [SerializeField] private bool rewardClaimed;

        public QuestId QuestId => questId;
        public long CurrentProgress => Math.Max(0L, currentProgress);
        public bool Completed => completed;
        public bool RewardClaimed => rewardClaimed;

        internal QuestProgressEntryData()
        {
        }

        internal QuestProgressEntryData(QuestId id)
        {
            questId = id;
        }

        internal QuestProgressEntryData Clone()
        {
            return new QuestProgressEntryData
            {
                questId = questId,
                currentProgress = currentProgress,
                completed = completed,
                rewardClaimed = rewardClaimed
            };
        }

        // targetValue에 도달하면 자동으로 완료 처리한다. 이미 완료된 항목은 값이 줄어도 완료 상태를 유지한다.
        internal void SetProgress(long value, long targetValue)
        {
            var clamped = Math.Max(0L, value);
            currentProgress = targetValue > 0L ? Math.Min(clamped, targetValue) : clamped;
            if (targetValue > 0L && currentProgress >= targetValue)
            {
                completed = true;
            }
        }

        // 완료 후 보상 버튼을 눌러야 보상을 받는 흐름(기획서 3장)의 "보상 받기"에 해당.
        internal bool TryClaimReward()
        {
            if (!completed || rewardClaimed)
            {
                return false;
            }

            rewardClaimed = true;
            return true;
        }

        internal void Repair()
        {
            currentProgress = Math.Max(0L, currentProgress);
            if (!completed)
            {
                rewardClaimed = false; // 완료되지 않았는데 수령 처리된 손상 데이터 방지
            }
        }
    }

    // 전체 퀘스트 진행도 저장 원본. GameProgressData의 필드로 편입되어 저장·복원된다.
    [Serializable]
    public sealed class QuestProgressData
    {
        [SerializeField] private List<QuestProgressEntryData> entries = new List<QuestProgressEntryData>();

        public static QuestProgressData CreateDefault()
        {
            return new QuestProgressData();
        }

        internal IReadOnlyList<QuestProgressEntryData> Entries => entries;

        public QuestProgressData Clone()
        {
            var clone = new QuestProgressData();
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null)
                {
                    clone.entries.Add(entries[i].Clone());
                }
            }

            return clone;
        }

        internal bool TryGetEntry(QuestId id, out QuestProgressEntryData entry)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].QuestId.Equals(id))
                {
                    entry = entries[i];
                    return true;
                }
            }

            entry = null;
            return false;
        }

        // 저장된 진행 기록이 없으면 0진행 항목을 새로 만들어 등록한다(첫 조회 시 자동 초기화).
        internal QuestProgressEntryData GetOrCreateEntry(QuestId id)
        {
            if (TryGetEntry(id, out var entry))
            {
                return entry;
            }

            entry = new QuestProgressEntryData(id);
            entries.Add(entry);
            return entry;
        }

        internal void Repair()
        {
            entries ??= new List<QuestProgressEntryData>();
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i] == null || !entries[i].QuestId.IsValid)
                {
                    entries.RemoveAt(i);
                    continue;
                }

                entries[i].Repair();
            }
        }

        internal QuestProgressView CreateView()
        {
            return new QuestProgressView(this);
        }
    }

    public readonly struct QuestProgressEntryView
    {
        public QuestProgressEntryView(QuestId questId, long currentProgress, bool completed, bool rewardClaimed)
        {
            QuestId = questId;
            CurrentProgress = Math.Max(0L, currentProgress);
            Completed = completed;
            RewardClaimed = rewardClaimed;
        }

        public QuestId QuestId { get; }
        public long CurrentProgress { get; }
        public bool Completed { get; }
        public bool RewardClaimed { get; }
    }

    public readonly struct QuestProgressView // UI 등에 전달할 읽기 전용 값
    {
        private readonly QuestProgressEntryView[] entries;

        public QuestProgressView(QuestProgressData data)
        {
            var source = data?.Entries;
            if (source == null || source.Count == 0)
            {
                entries = Array.Empty<QuestProgressEntryView>();
                return;
            }

            entries = new QuestProgressEntryView[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                var e = source[i];
                entries[i] = new QuestProgressEntryView(
                    e?.QuestId ?? default,
                    e?.CurrentProgress ?? 0L,
                    e?.Completed ?? false,
                    e?.RewardClaimed ?? false);
            }
        }

        public IReadOnlyList<QuestProgressEntryView> Entries => entries ?? Array.Empty<QuestProgressEntryView>();

        public bool TryGet(QuestId id, out QuestProgressEntryView view)
        {
            var list = Entries;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].QuestId.Equals(id))
                {
                    view = list[i];
                    return true;
                }
            }

            view = default;
            return false;
        }
    }
}
