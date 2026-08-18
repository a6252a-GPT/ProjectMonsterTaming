using System;
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
            if (!change.HasClaimQuestReward)
            {
                return;
            }

            quests ??= QuestProgressData.CreateDefault();
            if (!quests.TryGetEntry(change.ClaimQuestRewardQuestId, out var claimTarget) ||
                !claimTarget.Completed ||
                claimTarget.RewardClaimed)
            {
                rejected = true;
            }
        }

        partial void ApplyQuestProgress(GameProgressChange change, ref bool rejected)
        {
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
            if (!change.HasClaimQuestReward)
            {
                return;
            }

            quests.GetOrCreateEntry(change.ClaimQuestRewardQuestId).TryClaimReward();
        }
    }

    public sealed partial class GameProgressChange
    {
        internal bool HasSetQuestProgress { get; private set; }
        internal QuestId QuestProgressQuestId { get; private set; }
        internal long ExpectedQuestProgress { get; private set; }
        internal long NewQuestProgress { get; private set; }
        internal long QuestProgressTargetValue { get; private set; }
        internal bool HasClaimQuestReward { get; private set; }
        internal QuestId ClaimQuestRewardQuestId { get; private set; }

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

        public static GameProgressChange ClaimQuestReward(QuestId questId, RewardBundle reward)
        {
            return new GameProgressChange
            {
                HasClaimQuestReward = true,
                ClaimQuestRewardQuestId = questId,
                Rewards = reward ?? RewardBundle.Empty
            };
        }
    }
}
