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
        [SerializeField] private int repeatCycleCount; // 반복 퀘스트 템플릿 전용: 지금까지 완료(보상 수령)한 사이클 수. 목표치 escalation에 사용.

        public QuestId QuestId => questId;
        public long CurrentProgress => Math.Max(0L, currentProgress);
        public bool Completed => completed;
        public bool RewardClaimed => rewardClaimed;
        public int RepeatCycleCount => Math.Max(0, repeatCycleCount);

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
                rewardClaimed = rewardClaimed,
                repeatCycleCount = repeatCycleCount
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
            repeatCycleCount = Math.Max(0, repeatCycleCount);
            if (!completed)
            {
                rewardClaimed = false; // 완료되지 않았는데 수령 처리된 손상 데이터 방지
            }
        }

        // 반복 퀘스트 템플릿이 보상을 수령하고 다음 사이클로 넘어갈 때 호출한다.
        // 사이클 수를 올리고, 이번에 쓴 진행값·완료·수령 여부는 다음 등장을 위해 초기화한다.
        internal void AdvanceRepeatCycle()
        {
            repeatCycleCount++;
            currentProgress = 0L;
            completed = false;
            rewardClaimed = false;
        }

        // 일일·주간 퀘스트가 기준 시각을 넘겨 초기화될 때 호출한다. 진행도·완료·수령 여부만
        // 0으로 되돌리고, 반복 퀘스트 전용 필드(repeatCycleCount)는 일일·주간 퀘스트가 쓰지 않으므로 건드리지 않는다.
        internal void ResetForPeriodRefresh()
        {
            currentProgress = 0L;
            completed = false;
            rewardClaimed = false;
        }
    }

    // 전체 퀘스트 진행도 저장 원본. GameProgressData의 필드로 편입되어 저장·복원된다.
    [Serializable]
    public sealed class QuestProgressData
    {
        [SerializeField] private List<QuestProgressEntryData> entries = new List<QuestProgressEntryData>();
        [SerializeField] private QuestId activeRepeatingTemplateId; // 지금 추적 중인 반복 퀘스트 템플릿(선형 체인이 끝난 뒤 사용)
        [SerializeField] private QuestId lastRepeatingTemplateId; // 바로 직전에 활성이었던 템플릿(같은 퀘스트 연속 등장 방지용)
        [SerializeField] private long lastDailyResetPeriod = -1L; // GrowthDungeonDailyKeyRules와 동일한 KST 05:00 기준 일자 ID(-1 = 아직 초기화 전)
        [SerializeField] private long lastWeeklyResetPeriod = -1L; // 일일과 같은 일자 ID를 7로 나눈 주간 묶음 ID(-1 = 아직 초기화 전)

        public static QuestProgressData CreateDefault()
        {
            return new QuestProgressData();
        }

        internal IReadOnlyList<QuestProgressEntryData> Entries => entries;
        public QuestId ActiveRepeatingTemplateId => activeRepeatingTemplateId;
        public QuestId LastRepeatingTemplateId => lastRepeatingTemplateId;
        public long LastDailyResetPeriod => Math.Max(-1L, lastDailyResetPeriod);
        public long LastWeeklyResetPeriod => Math.Max(-1L, lastWeeklyResetPeriod);

        public QuestProgressData Clone()
        {
            var clone = new QuestProgressData
            {
                activeRepeatingTemplateId = activeRepeatingTemplateId,
                lastRepeatingTemplateId = lastRepeatingTemplateId,
                lastDailyResetPeriod = lastDailyResetPeriod,
                lastWeeklyResetPeriod = lastWeeklyResetPeriod
            };
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null)
                {
                    clone.entries.Add(entries[i].Clone());
                }
            }

            return clone;
        }

        // 다음 사이클로 넘어갈 때 활성 템플릿을 교체한다. previous가 곧 lastRepeatingTemplateId가 되어
        // 다음 선택에서 같은 템플릿이 연속으로 뽑히지 않게 막는 데 쓰인다.
        internal void SetActiveRepeatingTemplate(QuestId next, QuestId previous)
        {
            lastRepeatingTemplateId = previous;
            activeRepeatingTemplateId = next;
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

        // 일일 퀘스트 초기화 기준일(KST 05:00 경계)이 실제로 넘어갔을 때만 통과시킨다.
        // GrowthDungeonDailyKeyRules.GetPeriodId와 같은 방식으로 계산된 기간 ID를 그대로 받아 비교한다.
        // 통과하면 넘겨받은 퀘스트 ID들의 진행도를 전부 0으로 되돌린다(미완료·완료·수령 모두 포함).
        internal bool TryAdvanceDailyResetPeriod(long expectedPeriod, long nextPeriod, IReadOnlyList<QuestId> questIds)
        {
            if (LastDailyResetPeriod != expectedPeriod || nextPeriod <= LastDailyResetPeriod)
            {
                return false;
            }

            lastDailyResetPeriod = nextPeriod;
            if (questIds != null)
            {
                for (var i = 0; i < questIds.Count; i++)
                {
                    GetOrCreateEntry(questIds[i]).ResetForPeriodRefresh();
                }
            }

            return true;
        }

        // 주간 퀘스트 초기화(일일과 같은 일자 ID를 7일 단위로 묶은 기간)가 실제로 넘어갔을 때만 통과시킨다.
        // 계산 방식은 TryAdvanceDailyResetPeriod와 동일하고, 넘겨받는 기간 ID만 7일 단위로 묶인 값이다.
        internal bool TryAdvanceWeeklyResetPeriod(long expectedPeriod, long nextPeriod, IReadOnlyList<QuestId> questIds)
        {
            if (LastWeeklyResetPeriod != expectedPeriod || nextPeriod <= LastWeeklyResetPeriod)
            {
                return false;
            }

            lastWeeklyResetPeriod = nextPeriod;
            if (questIds != null)
            {
                for (var i = 0; i < questIds.Count; i++)
                {
                    GetOrCreateEntry(questIds[i]).ResetForPeriodRefresh();
                }
            }

            return true;
        }

        internal void Repair()
        {
            lastDailyResetPeriod = Math.Max(-1L, lastDailyResetPeriod);
            lastWeeklyResetPeriod = Math.Max(-1L, lastWeeklyResetPeriod);
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
        public QuestProgressEntryView(
            QuestId questId,
            long currentProgress,
            bool completed,
            bool rewardClaimed,
            int repeatCycleCount = 0,
            long targetValue = 0L)
        {
            QuestId = questId;
            CurrentProgress = Math.Max(0L, currentProgress);
            Completed = completed;
            RewardClaimed = rewardClaimed;
            RepeatCycleCount = Math.Max(0, repeatCycleCount);
            TargetValue = Math.Max(0L, targetValue);
        }

        public QuestId QuestId { get; }
        public long CurrentProgress { get; }
        public bool Completed { get; }
        public bool RewardClaimed { get; }
        public int RepeatCycleCount { get; }
        public long TargetValue { get; }
    }

    public readonly struct QuestProgressView // UI 등에 전달할 읽기 전용 값
    {
        private readonly QuestProgressEntryView[] entries;

        public QuestProgressView(QuestProgressData data)
        {
            var source = data?.Entries;
            ActiveRepeatingTemplateId = data?.ActiveRepeatingTemplateId ?? default;
            LastRepeatingTemplateId = data?.LastRepeatingTemplateId ?? default;
            LastDailyResetPeriod = data?.LastDailyResetPeriod ?? -1L;
            LastWeeklyResetPeriod = data?.LastWeeklyResetPeriod ?? -1L;
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
                    e?.RewardClaimed ?? false,
                    e?.RepeatCycleCount ?? 0);
            }
        }

        public IReadOnlyList<QuestProgressEntryView> Entries => entries ?? Array.Empty<QuestProgressEntryView>();
        public QuestId ActiveRepeatingTemplateId { get; }
        public QuestId LastRepeatingTemplateId { get; }
        public long LastDailyResetPeriod { get; }
        public long LastWeeklyResetPeriod { get; }

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
