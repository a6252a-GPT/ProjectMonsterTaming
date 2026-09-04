using System;
using System.Collections.Generic;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Quest;
using ProjectMT.Shared.Reward;
using UnityEngine;

namespace ProjectMT.Shared.Quest
{
    // 퀘스트 런타임이 저장 서비스에 붙는 지점. GameDataService가 로드되면 여기로 들어온다.
    public static class QuestProgressServiceHub
    {
        public static IGameProgressService Current { get; private set; }
        public static event Action<IGameProgressService> Ready;

        public static void Bind(IGameProgressService service)
        {
            Current = service;
            Ready?.Invoke(service);
        }
    }
}

namespace ProjectMT.Shared.GameData
{
    public partial interface IGameProgressService
    {
        QuestProgressView Quests { get; }
    }

    public sealed partial class GameDataService
    {
        public QuestProgressView Quests => current.Quests;

        partial void NotifyProgressReady()
        {
            QuestProgressServiceHub.Bind(this);
        }
    }

    public sealed partial class GameProgressData
    {
        [SerializeField] private QuestProgressData quests = QuestProgressData.CreateDefault();

        public QuestProgressView Quests => (quests ?? QuestProgressData.CreateDefault()).CreateView();

        partial void CopyQuestTo(GameProgressData clone)
        {
            clone.quests = quests?.Clone() ?? QuestProgressData.CreateDefault();
        }

        partial void RepairQuest()
        {
            quests ??= QuestProgressData.CreateDefault();
            quests.Repair();
        }

        partial void RejectInvalidQuestClaim(GameProgressChange change, ref bool rejected)
        {
            quests ??= QuestProgressData.CreateDefault();

            if (change.HasClaimQuestRewards)
            {
                var ids = change.ClaimQuestRewardQuestIds;
                var seen = new HashSet<QuestId>();
                if (ids == null || ids.Count == 0)
                {
                    rejected = true;
                    return;
                }

                for (var i = 0; i < ids.Count; i++)
                {
                    if (!ids[i].IsValid || !seen.Add(ids[i]) ||
                        !quests.TryGetEntry(ids[i], out var target) ||
                        !target.Completed || target.RewardClaimed)
                    {
                        rejected = true;
                        return;
                    }
                }

                return;
            }

            if (change.HasClaimQuestReward)
            {
                if (!quests.TryGetEntry(change.ClaimQuestRewardQuestId, out var claimTarget) ||
                    !claimTarget.Completed ||
                    claimTarget.RewardClaimed)
                {
                    rejected = true;
                }

                return;
            }

            if (change.HasClaimRepeatingQuestReward)
            {
                // 이미 다른 사이클로 넘어간 뒤의 낡은 요청이거나, 아직 완료·수령 조건을 못 채웠으면 거절한다.
                if (quests.ActiveRepeatingTemplateId != change.ClaimRepeatingQuestRewardTemplateId ||
                    !quests.TryGetEntry(change.ClaimRepeatingQuestRewardTemplateId, out var repeatingTarget) ||
                    !repeatingTarget.Completed ||
                    repeatingTarget.RewardClaimed)
                {
                    rejected = true;
                }

                return;
            }

            if (change.HasInitializeActiveRepeatingTemplate && quests.ActiveRepeatingTemplateId.IsValid)
            {
                rejected = true; // 이미 초기화되어 있으면 재초기화 금지(동시 호출 시 낙관적 동시성 보호)
            }
        }

        // 일일 퀘스트 초기화 요청 검증. GrowthDungeonProgressData의 TryAdvanceDailyKeyPeriod와 동일하게
        // "기대했던 이전 기간"이 지금 저장된 값과 다르면(이미 다른 초기화가 먼저 적용됨) 거절한다.
        partial void RejectInvalidQuestDailyReset(GameProgressChange change, ref bool rejected)
        {
            if (!change.HasQuestDailyReset)
            {
                return;
            }

            quests ??= QuestProgressData.CreateDefault();
            if (quests.LastDailyResetPeriod != change.ExpectedQuestDailyResetPeriod ||
                change.QuestDailyResetPeriod <= quests.LastDailyResetPeriod)
            {
                rejected = true;
            }
        }

        // 주간 퀘스트 초기화 요청 검증. 계산 방식은 일일과 동일하고, 기간 ID만 7일 단위로 묶인 값이다.
        partial void RejectInvalidQuestWeeklyReset(GameProgressChange change, ref bool rejected)
        {
            if (!change.HasQuestWeeklyReset)
            {
                return;
            }

            quests ??= QuestProgressData.CreateDefault();
            if (quests.LastWeeklyResetPeriod != change.ExpectedQuestWeeklyResetPeriod ||
                change.QuestWeeklyResetPeriod <= quests.LastWeeklyResetPeriod)
            {
                rejected = true;
            }
        }

        partial void ApplyQuestProgress(GameProgressChange change, ref bool rejected)
        {
            if (change.HasConsumeQuestTutorialHint)
            {
                quests ??= QuestProgressData.CreateDefault();
                if (!quests.TryConsumeTutorialHint(change.QuestTutorialHintId))
                {
                    rejected = true;
                }

                return;
            }

            if (change.HasSetQuestProgressBatch)
            {
                quests ??= QuestProgressData.CreateDefault();
                var updates = change.QuestProgressUpdates;
                var seen = new HashSet<QuestId>();
                if (updates == null || updates.Count == 0)
                {
                    rejected = true;
                    return;
                }

                for (var i = 0; i < updates.Count; i++)
                {
                    var update = updates[i];
                    var entry = quests.GetOrCreateEntry(update.QuestId);
                    if (!update.IsValid || !seen.Add(update.QuestId) || entry.Completed ||
                        entry.CurrentProgress != update.ExpectedProgress)
                    {
                        rejected = true;
                        return;
                    }
                }

                for (var i = 0; i < updates.Count; i++)
                {
                    var update = updates[i];
                    quests.GetOrCreateEntry(update.QuestId).SetProgress(update.NewProgress, update.TargetValue);
                }

                return;
            }

            if (!change.HasSetQuestProgress)
            {
                return;
            }

            quests ??= QuestProgressData.CreateDefault();
            var questEntry = quests.GetOrCreateEntry(change.QuestProgressQuestId);
            if (questEntry.Completed || questEntry.CurrentProgress != change.ExpectedQuestProgress)
            {
                rejected = true;
                return;
            }

            questEntry.SetProgress(change.NewQuestProgress, change.QuestProgressTargetValue);
        }

        partial void ApplyQuestClaim(GameProgressChange change)
        {
            if (change.HasClaimQuestRewards)
            {
                var ids = change.ClaimQuestRewardQuestIds;
                for (var i = 0; i < ids.Count; i++)
                {
                    quests.GetOrCreateEntry(ids[i]).TryClaimReward();
                }

                return;
            }

            if (change.HasClaimQuestReward)
            {
                quests.GetOrCreateEntry(change.ClaimQuestRewardQuestId).TryClaimReward();
                return;
            }

            if (change.HasClaimRepeatingQuestReward)
            {
                var entry = quests.GetOrCreateEntry(change.ClaimRepeatingQuestRewardTemplateId);
                entry.TryClaimReward(); // 이력상 수령 처리 후
                entry.AdvanceRepeatCycle(); // 사이클 +1, 다음 등장을 위해 진행값 초기화
                quests.SetActiveRepeatingTemplate(
                    change.NextRepeatingTemplateId,
                    change.ClaimRepeatingQuestRewardTemplateId,
                    change.NextRepeatingTemplateStartsNewCycle);
                return;
            }

            if (change.HasInitializeActiveRepeatingTemplate)
            {
                // 반복 풀을 처음 켜는 시점이라 셔플백이 비어 있으므로 새 라운드로 시작한다.
                quests.SetActiveRepeatingTemplate(change.InitializeActiveRepeatingTemplateId, default, true);
            }
        }

        partial void ApplyQuestDailyReset(GameProgressChange change)
        {
            if (!change.HasQuestDailyReset)
            {
                return;
            }

            quests.TryAdvanceDailyResetPeriod(
                change.ExpectedQuestDailyResetPeriod,
                change.QuestDailyResetPeriod,
                change.QuestDailyResetQuestIds);
        }

        partial void ApplyQuestWeeklyReset(GameProgressChange change)
        {
            if (!change.HasQuestWeeklyReset)
            {
                return;
            }

            quests.TryAdvanceWeeklyResetPeriod(
                change.ExpectedQuestWeeklyResetPeriod,
                change.QuestWeeklyResetPeriod,
                change.QuestWeeklyResetQuestIds);
        }

        // 디버그/보정용: 넘겨받은 퀘스트 ID들의 진행도·완료·수령 여부만 0으로 되돌린다.
        // 일일·주간 정기 초기화(ApplyQuestDailyReset/ApplyQuestWeeklyReset)와 달리 기간 ID 검증이 없어
        // 아무 때나 적용할 수 있고, 넘기지 않은 퀘스트(메인 스토리·반복 템플릿 등)는 건드리지 않는다.
        partial void ApplyQuestProgressReset(GameProgressChange change)
        {
            if (!change.HasResetQuestProgress)
            {
                return;
            }

            quests ??= QuestProgressData.CreateDefault();
            var ids = change.ResetQuestProgressQuestIds;
            for (var i = 0; i < ids.Count; i++)
            {
                quests.GetOrCreateEntry(ids[i]).ResetForPeriodRefresh();
            }
        }
    }

    public sealed partial class GameProgressChange
    {
        internal bool HasSetQuestProgress { get; private set; }
        internal bool HasSetQuestProgressBatch { get; private set; }
        internal IReadOnlyList<QuestProgressUpdate> QuestProgressUpdates { get; private set; }
        internal QuestId QuestProgressQuestId { get; private set; }
        internal long ExpectedQuestProgress { get; private set; }
        internal long NewQuestProgress { get; private set; }
        internal long QuestProgressTargetValue { get; private set; }
        internal bool HasClaimQuestReward { get; private set; }
        internal QuestId ClaimQuestRewardQuestId { get; private set; }
        internal bool HasClaimQuestRewards { get; private set; }
        internal IReadOnlyList<QuestId> ClaimQuestRewardQuestIds { get; private set; }
        internal bool HasClaimRepeatingQuestReward { get; private set; }
        internal QuestId ClaimRepeatingQuestRewardTemplateId { get; private set; }
        internal QuestId NextRepeatingTemplateId { get; private set; }
        internal bool NextRepeatingTemplateStartsNewCycle { get; private set; }
        internal bool HasInitializeActiveRepeatingTemplate { get; private set; }
        internal QuestId InitializeActiveRepeatingTemplateId { get; private set; }
        internal bool HasQuestDailyReset { get; private set; }
        internal long ExpectedQuestDailyResetPeriod { get; private set; }
        internal long QuestDailyResetPeriod { get; private set; }
        internal IReadOnlyList<QuestId> QuestDailyResetQuestIds { get; private set; }
        internal bool HasQuestWeeklyReset { get; private set; }
        internal long ExpectedQuestWeeklyResetPeriod { get; private set; }
        internal long QuestWeeklyResetPeriod { get; private set; }
        internal IReadOnlyList<QuestId> QuestWeeklyResetQuestIds { get; private set; }
        internal bool HasResetQuestProgress { get; private set; }
        internal IReadOnlyList<QuestId> ResetQuestProgressQuestIds { get; private set; }
        internal bool HasConsumeQuestTutorialHint { get; private set; }
        internal string QuestTutorialHintId { get; private set; }

        public static GameProgressChange SetQuestProgress(
            QuestId questId,
            long expectedProgress,
            long newProgress,
            long targetValue)
        {
            return new GameProgressChange
            {
                HasSetQuestProgress = true,
                QuestProgressQuestId = questId,
                ExpectedQuestProgress = Math.Max(0L, expectedProgress),
                NewQuestProgress = Math.Max(0L, newProgress),
                QuestProgressTargetValue = Math.Max(1L, targetValue)
            };
        }

        public static GameProgressChange SetQuestProgressBatch(IReadOnlyList<QuestProgressUpdate> updates)
        {
            return new GameProgressChange
            {
                HasSetQuestProgressBatch = true,
                QuestProgressUpdates = updates ?? Array.Empty<QuestProgressUpdate>()
            };
        }

        public static GameProgressChange ClaimQuestReward(QuestId questId, RewardBundle reward)
        {
            return new GameProgressChange
            {
                HasClaimQuestReward = true,
                ClaimQuestRewardQuestId = questId,
                Rewards = reward ?? RewardBundle.Empty
            };
        }

        public static GameProgressChange ClaimQuestRewards(IReadOnlyList<QuestId> questIds, RewardBundle reward)
        {
            return new GameProgressChange
            {
                HasClaimQuestRewards = true,
                ClaimQuestRewardQuestIds = questIds ?? Array.Empty<QuestId>(),
                Rewards = reward ?? RewardBundle.Empty
            };
        }

        // 반복 퀘스트 템플릿의 보상을 받고, 동시에 다음에 추적할 템플릿(nextTemplateId)으로 넘어간다.
        // 다음 템플릿은 호출부(QuestRuntime)가 카탈로그를 보고 미리 골라서 넘겨준다.
        // startsNewCycle: 셔플백 후보를 이번 선택으로 모두 소진했는지(=다음부터 새 라운드) 여부.
        public static GameProgressChange ClaimRepeatingQuestReward(
            QuestId completedTemplateId,
            RewardBundle reward,
            QuestId nextTemplateId,
            bool startsNewCycle)
        {
            return new GameProgressChange
            {
                HasClaimRepeatingQuestReward = true,
                ClaimRepeatingQuestRewardTemplateId = completedTemplateId,
                NextRepeatingTemplateId = nextTemplateId,
                NextRepeatingTemplateStartsNewCycle = startsNewCycle,
                Rewards = reward ?? RewardBundle.Empty
            };
        }

        // 선형 체인이 끝난 뒤 반복 퀘스트 풀을 처음 시작할 때 1회만 적용된다.
        public static GameProgressChange InitializeActiveRepeatingTemplate(QuestId templateId)
        {
            return new GameProgressChange
            {
                HasInitializeActiveRepeatingTemplate = true,
                InitializeActiveRepeatingTemplateId = templateId
            };
        }

        // 일일 퀘스트 초기화(GrowthDungeonDailyKeyRules와 동일한 KST 05:00 기준 기간 ID 사용).
        // questIds에 넘긴 퀘스트들의 진행도·완료·수령 여부를 전부 0/미완료/미수령으로 되돌린다.
        public static GameProgressChange RefreshDailyQuests(
            long expectedPeriod,
            long nextPeriod,
            IReadOnlyList<QuestId> questIds)
        {
            return new GameProgressChange
            {
                HasQuestDailyReset = true,
                ExpectedQuestDailyResetPeriod = expectedPeriod,
                QuestDailyResetPeriod = nextPeriod,
                QuestDailyResetQuestIds = questIds ?? Array.Empty<QuestId>()
            };
        }

        // 주간 퀘스트 초기화. 월요일 KST 05:00 기준 기간 ID를 넘긴다.
        public static GameProgressChange RefreshWeeklyQuests(
            long expectedPeriod,
            long nextPeriod,
            IReadOnlyList<QuestId> questIds)
        {
            return new GameProgressChange
            {
                HasQuestWeeklyReset = true,
                ExpectedQuestWeeklyResetPeriod = expectedPeriod,
                QuestWeeklyResetPeriod = nextPeriod,
                QuestWeeklyResetQuestIds = questIds ?? Array.Empty<QuestId>()
            };
        }

        // 디버그/보정용: 넘겨받은 퀘스트들의 진행도만 0으로 되돌린다(기간 ID 검증 없음).
        // 같은 조건을 공유하는 일일/주간 퀘스트의 진행도가 서로 어긋나 있을 때 다시 나란히 맞추고
        // 싶으면 사용한다(메인 스토리·반복 템플릿 등 넘기지 않은 퀘스트는 그대로 유지).
        public static GameProgressChange ResetQuestProgress(IReadOnlyList<QuestId> questIds)
        {
            return new GameProgressChange
            {
                HasResetQuestProgress = true,
                ResetQuestProgressQuestIds = questIds ?? Array.Empty<QuestId>()
            };
        }

        public static GameProgressChange ConsumeQuestTutorialHint(string hintId)
        {
            return new GameProgressChange
            {
                HasConsumeQuestTutorialHint = true,
                QuestTutorialHintId = hintId?.Trim() ?? string.Empty
            };
        }
    }
}
