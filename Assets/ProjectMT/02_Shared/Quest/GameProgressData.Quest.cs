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
            quests ??= QuestProgressData.CreateDefault();

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
                    change.ClaimRepeatingQuestRewardTemplateId);
                return;
            }

            if (change.HasInitializeActiveRepeatingTemplate)
            {
                quests.SetActiveRepeatingTemplate(change.InitializeActiveRepeatingTemplateId, default);
            }
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
        internal bool HasClaimRepeatingQuestReward { get; private set; }
        internal QuestId ClaimRepeatingQuestRewardTemplateId { get; private set; }
        internal QuestId NextRepeatingTemplateId { get; private set; }
        internal bool HasInitializeActiveRepeatingTemplate { get; private set; }
        internal QuestId InitializeActiveRepeatingTemplateId { get; private set; }

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

        // 반복 퀘스트 템플릿의 보상을 받고, 동시에 다음에 추적할 템플릿(nextTemplateId)으로 넘어간다.
        // 다음 템플릿은 호출부(QuestRuntime)가 카탈로그를 보고 미리 골라서 넘겨준다.
        public static GameProgressChange ClaimRepeatingQuestReward(
            QuestId completedTemplateId,
            RewardBundle reward,
            QuestId nextTemplateId)
        {
            return new GameProgressChange
            {
                HasClaimRepeatingQuestReward = true,
                ClaimRepeatingQuestRewardTemplateId = completedTemplateId,
                NextRepeatingTemplateId = nextTemplateId,
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
    }
}
