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
        [SerializeField] private bool isEnabled = true; // 꺼두면 이 퀘스트는 선행 체인·반복 풀 순회에서 통째로 건너뛴다(콘텐츠 준비 전 임시 비활성화용).
        [SerializeField] private string displayName;
        [TextArea(2, 5)]
        [SerializeField] private string description;
        [SerializeField] private QuestType questType = QuestType.Main;
        [SerializeField] private QuestConditionType conditionType;
        [SerializeField] private long targetValue = 1L;
        [SerializeField] private QuestId prerequisiteQuestId; // 비어 있으면 선행 조건 없음(체인 시작 퀘스트)
        [SerializeField] private RewardDefinition reward;
        [SerializeField] private List<QuestUnlockTarget> unlockTargets = new List<QuestUnlockTarget>();
        [SerializeField] private bool unlockGateEnabled; // 기본 꺼짐(전부 열림). 켜면 QuestRuntime.IsUnlocked가 이 퀘스트 보상 수령 전까지 unlockTargets를 잠금으로 취급한다.
        [SerializeField] private bool requiresActiveTracking; // 메인 튜토리얼 행동은 이 퀘스트가 현재 목표일 때만 누적

        [Header("반복 퀘스트 템플릿(선형 체인이 끝난 뒤 순환 등장)")]
        [SerializeField] private bool isRepeatingTemplate; // 켜면 선행 퀘스트 체인 대신 반복 퀘스트 풀에서 무작위로 뽑혀 등장한다.
        [SerializeField] private long repeatIncrement; // 이 템플릿이 다시 등장할 때마다 targetValue에 더해지는 값(0이면 목표 고정).
        [SerializeField] private int repeatMaxOccurrences; // 이 템플릿이 등장할 수 있는 최대 횟수(0 = 제한 없이 계속 등장).
        // 여기 적힌 반복 템플릿들이 각각 한 번 이상 완료(보상 수령)되기 전까지는 이 템플릿이 반복 풀 후보에서 제외된다.
        // 비어 있으면 제한 없이 처음부터 후보가 된다(다른 반복 템플릿과 순서 관계가 필요할 때만 사용).
        [SerializeField] private List<QuestId> repeatPrerequisiteQuestIds = new List<QuestId>();

        public QuestId QuestId => questId;
        public bool IsEnabled => isEnabled;
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
        public bool UnlockGateEnabled => unlockGateEnabled;
        public bool RequiresActiveTracking => requiresActiveTracking;
        public bool IsRepeatingTemplate => isRepeatingTemplate;
        public long RepeatIncrement => Math.Max(0L, repeatIncrement);
        public int RepeatMaxOccurrences => Math.Max(0, repeatMaxOccurrences);
        public IReadOnlyList<QuestId> RepeatPrerequisiteQuestIds =>
            (IReadOnlyList<QuestId>)repeatPrerequisiteQuestIds ?? Array.Empty<QuestId>();

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

        public void EditorSetProgressPolicy(bool activeTrackingRequired)
        {
            requiresActiveTracking = activeTrackingRequired;
        }

        public void EditorSetUnlockGate(bool enabled, IEnumerable<QuestUnlockTarget> targets)
        {
            unlockGateEnabled = enabled;
            unlockTargets = targets == null
                ? new List<QuestUnlockTarget>()
                : new List<QuestUnlockTarget>(targets);
        }

        public void EditorSetRepeating(bool repeating, long increment, int maxOccurrences)
        {
            isRepeatingTemplate = repeating;
            repeatIncrement = Math.Max(0L, increment);
            repeatMaxOccurrences = Math.Max(0, maxOccurrences);
        }

        public void EditorSetRepeatPrerequisites(IEnumerable<QuestId> prerequisiteIds)
        {
            repeatPrerequisiteQuestIds = prerequisiteIds == null
                ? new List<QuestId>()
                : new List<QuestId>(prerequisiteIds);
        }
#endif
    }
}
