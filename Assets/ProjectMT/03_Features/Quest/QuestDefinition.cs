using System;
using System.Collections.Generic;
using ProjectMT.Shared.Quest;
using ProjectMT.Shared.Reward;
using UnityEngine;

namespace ProjectMT.Features.Quest
{
    // 퀘스트 1개의 고정 기획 데이터(ID·이름·설명·조건·목표·선행·보상·해금 대상).
    // 저장되는 진행값(현재 진행도·완료·보상 수령 여부)은 Shared의 QuestProgressData가 담당한다.
    [CreateAssetMenu(menuName = "ProjectMT/Quest/Definition", fileName = "QuestDefinition")]
    public sealed class QuestDefinition : ScriptableObject
    {
        [SerializeField] private QuestId questId;
        [SerializeField] private string displayName;
        [TextArea(2, 5)]
        [SerializeField] private string description;
        [SerializeField] private QuestType questType = QuestType.Main;
        [SerializeField] private QuestConditionType conditionType;
        [SerializeField] private long targetValue = 1L;
        [SerializeField] private QuestId prerequisiteQuestId; // 비어 있으면 선행 조건 없음(체인 시작 퀘스트)
        [SerializeField] private RewardDefinition reward;
        [SerializeField] private List<QuestUnlockTarget> unlockTargets = new List<QuestUnlockTarget>();

        public QuestId QuestId => questId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? questId.Value : displayName.Trim();
        public string Description => description ?? string.Empty;
        public QuestType QuestType => questType;
        public QuestConditionType ConditionType => conditionType;
        public long TargetValue => Math.Max(1L, targetValue);
        public QuestId PrerequisiteQuestId => prerequisiteQuestId;
        public bool HasPrerequisite => prerequisiteQuestId.IsValid;
        public RewardDefinition Reward => reward;
        public IReadOnlyList<QuestUnlockTarget> UnlockTargets =>
            (IReadOnlyList<QuestUnlockTarget>)unlockTargets ?? Array.Empty<QuestUnlockTarget>();

        // 보상 정의를 실제 지급 단위(RewardBundle)로 변환한다. 보상이 비어 있으면 실패로 취급한다.
        public bool TryCreateRewardBundle(out RewardBundle bundle)
        {
            if (reward != null && reward.TryCreate(1L, out bundle))
            {
                return true;
            }

            bundle = RewardBundle.Empty;
            return false;
        }

        public bool TryValidate(out string error)
        {
            if (!questId.IsValid)
            {
                error = $"Quest ID is blank. Asset={name}";
                return false;
            }

            if (targetValue <= 0L)
            {
                error = $"Quest target value must be positive. Quest={questId.Value}";
                return false;
            }

            if (reward == null)
            {
                error = $"Quest reward is not assigned. Quest={questId.Value}";
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            QuestId id,
            string questName,
            string desc,
            QuestType type,
            QuestConditionType condition,
            long target,
            QuestId prerequisite,
            RewardDefinition rewardDefinition,
            IEnumerable<QuestUnlockTarget> unlocks)
        {
            questId = id;
            displayName = questName?.Trim();
            description = desc?.Trim();
            questType = type;
            conditionType = condition;
            targetValue = Math.Max(1L, target);
            prerequisiteQuestId = prerequisite;
            reward = rewardDefinition;
            unlockTargets = unlocks == null
                ? new List<QuestUnlockTarget>()
                : new List<QuestUnlockTarget>(unlocks);
        }

        public void EditorSetReward(RewardDefinition rewardDefinition)
        {
            reward = rewardDefinition;
        }
#endif
    }
}
