using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProjectMT.Features.Commander;
using ProjectMT.Features.Equipment;
using ProjectMT.Shared.Commander;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Quest;
using ProjectMT.Shared.Reward;
using UnityEngine;

namespace ProjectMT.Features.Quest
{
    public static partial class QuestRuntime
    {
        // 선형 체인을 다 마친 뒤 보여줄 반복 퀘스트. 아직 시작 전이면 하나를 뽑아 저장을 걸어 두고,
        // 저장이 끝나기 전 이번 프레임에는 미리보기 값(0진행)을 그대로 보여준다.
        private static bool TryGetActiveRepeatingQuest(
            QuestType type,
            out QuestDefinition definition,
            out QuestProgressEntryView progressView)
        {
            definition = null;
            progressView = default;
            if (catalog == null || progress == null)
            {
                return false;
            }

            var activeId = progress.Quests.ActiveRepeatingTemplateId;
            if (activeId.IsValid && catalog.TryGet(activeId, out var activeDefinition) &&
                activeDefinition.IsRepeatingTemplate && activeDefinition.QuestType == type)
            {
                definition = activeDefinition;
                progressView = GetRepeatingProgress(activeDefinition);
                return true;
            }

            if (!TryPickRepeatingTemplate(type, default, out var picked, out _))
            {
                return false; // 카탈로그에 반복 템플릿이 하나도 등록되어 있지 않음
            }

            _ = InitializeActiveRepeatingTemplateAsync(picked.QuestId);
            definition = picked;
            progressView = GetRepeatingProgress(picked);
            return true;
        }

        // 반복 템플릿 하나의 "이번 사이클" 진행도를 계산한다. 임계값형 조건(레벨 도달 등)은 저장값 대신
        // 현재 게임 상태를 즉시 읽고, 카운트형 조건(뽑기 등)은 이벤트로 누적된 저장값을 그대로 쓴다.
        private static QuestProgressEntryView GetRepeatingProgress(QuestDefinition template)
        {
            var saved = GetProgress(template.QuestId);
            var resolvedTarget = ResolveTargetValue(template);
            var currentValue = IsThresholdCondition(template.ConditionType)
                ? ResolveThresholdCurrentValue(template.ConditionType)
                : saved.CurrentProgress;

            var clamped = Math.Max(0L, Math.Min(currentValue, resolvedTarget));
            var completed = clamped >= resolvedTarget;
            return new QuestProgressEntryView(
                template.QuestId,
                clamped,
                completed,
                completed && saved.RewardClaimed,
                saved.RepeatCycleCount);
        }

        // cycleCount(지금까지 이 템플릿을 완료한 횟수)만큼 targetValue에 repeatIncrement를 누적한다.
        private static long ResolveRepeatingTarget(QuestDefinition template, int cycleCount)
        {
            var raw = template.TargetValue + template.RepeatIncrement * cycleCount;
            return Math.Max(1L, raw);
        }

        // 진행도를 이벤트 누적이 아니라 "현재 값"으로 판정하는 조건인지 구분한다.
        private static bool IsThresholdCondition(QuestConditionType type)
        {
            switch (type)
            {
                case QuestConditionType.MonsterOwnedCount:
                case QuestConditionType.MonsterLevelReach:
                case QuestConditionType.CommanderLevelReach:
                case QuestConditionType.CommanderHealthLevelReach:
                case QuestConditionType.CommanderAttackLevelReach:
                case QuestConditionType.CommanderDefenseLevelReach:
                case QuestConditionType.CommanderPowerReach:
                case QuestConditionType.EquipmentSlotUpgradeReach:
                case QuestConditionType.CommanderPotentialUnlockCount:
                    return true;
                default:
                    return false;
            }
        }

        // 다음 반복 템플릿을 "셔플백" 방식으로 고른다: 이번 라운드에 안 나온 후보 중에서만 뽑아
        // 전부 한 번씩 나오기 전엔 같은 템플릿이 먼저 두 번 나오지 않게 한다. excludeId·소진된
        // 템플릿·선행 조건(RepeatPrerequisiteQuestIds) 미충족 템플릿은 제외하며, startsNewCycle이
        // true면 호출부가 저장 시 셔플백 목록을 비워야 한다(GameProgressChange 참고).
        private static bool TryPickRepeatingTemplate(
            QuestType type,
            QuestId excludeId,
            out QuestDefinition result,
            out bool startsNewCycle)
        {
            var usedThisCycle = progress?.Quests.RepeatCycleUsedTemplateIds;
            startsNewCycle = false;

            var candidates = new List<QuestDefinition>();
            foreach (var candidate in catalog.GetRepeatingTemplates(type))
            {
                if (candidate.QuestId == excludeId || IsRepeatingTemplateExhausted(candidate) ||
                    !AreRepeatPrerequisitesMet(candidate) || ContainsId(usedThisCycle, candidate.QuestId))
                {
                    continue;
                }

                candidates.Add(candidate);
            }

            if (candidates.Count == 0)
            {
                // 라운드 후보를 모두 소진했으면 방금 끝난 템플릿만 제외하고 새 라운드를 시작한다.
                startsNewCycle = true;
                foreach (var candidate in catalog.GetRepeatingTemplates(type))
                {
                    if (candidate.QuestId != excludeId && !IsRepeatingTemplateExhausted(candidate) &&
                        AreRepeatPrerequisitesMet(candidate))
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                // 안전장치: 선행 조건을 만족하는 후보가 없으면(설정 문제 포함) 조건을 잠시 무시하고
                // 소진되지 않은 템플릿 중에서 고른다(풀이 멈추는 것보다 안전).
                foreach (var candidate in catalog.GetRepeatingTemplates(type))
                {
                    if (candidate.QuestId != excludeId && !IsRepeatingTemplateExhausted(candidate))
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                // 후보가 없으면(방금 그 템플릿뿐이거나 나머지가 전부 소진) 제외 조건 없이 다시 시도한다.
                foreach (var candidate in catalog.GetRepeatingTemplates(type))
                {
                    if (!IsRepeatingTemplateExhausted(candidate))
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                result = null;
                return false;
            }

            result = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return true;
        }

        // RepeatPrerequisiteQuestIds에 적힌 템플릿들이 전부 한 번 이상 완료됐는지 확인한다(비어 있으면 항상 통과).
        private static bool AreRepeatPrerequisitesMet(QuestDefinition template)
        {
            var prerequisites = template.RepeatPrerequisiteQuestIds;
            for (var i = 0; i < prerequisites.Count; i++)
            {
                if (GetProgress(prerequisites[i]).RepeatCycleCount < 1)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsId(IReadOnlyList<QuestId> ids, QuestId id)
        {
            if (ids == null)
            {
                return false;
            }

            for (var i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRepeatingTemplateExhausted(QuestDefinition template)
        {
            var maxOccurrences = template.RepeatMaxOccurrences;
            return maxOccurrences > 0 && GetProgress(template.QuestId).RepeatCycleCount >= maxOccurrences;
        }

        // 선형 체인이 끝난 뒤 반복 퀘스트 풀을 처음 켤 때 1회만 저장을 시도한다.
        // 이미 다른 호출이 초기화했으면 곧바로 종료한다(중복 초기화 방지).
        private static async Task InitializeActiveRepeatingTemplateAsync(QuestId templateId)
        {
            for (var attempt = 0; attempt < MaxAdvanceRetryCount; attempt++)
            {
                if (!IsReady || progress.Quests.ActiveRepeatingTemplateId.IsValid)
                {
                    return;
                }

                if (await progress.TryApplyAndSaveAsync(GameProgressChange.InitializeActiveRepeatingTemplate(templateId)))
                {
                    return;
                }
            }
        }

        // 반복 퀘스트 템플릿 전용 보상 수령. 임계값형 조건은 최신 값을 저장에 반영해서 검증을 통과시키고,
        // 성공하면 다음에 추적할 템플릿을 셔플백 방식으로 골라 같은 저장 요청 안에서 사이클 전환까지 함께 처리한다.
        private static async Task<bool> TryClaimRepeatingRewardAsync(QuestId templateId, QuestDefinition definition)
        {
            if (progress.Quests.ActiveRepeatingTemplateId != templateId)
            {
                return false; // 이미 다음 사이클로 넘어간 뒤의 낡은 요청
            }

            await SyncRepeatingThresholdProgressAsync(definition);

            if (!CanClaimReward(templateId))
            {
                return false;
            }

            if (!definition.TryCreateRewardBundle(out var bundle))
            {
                return false;
            }

            var claimBundle = ResolveQuestClaimReward(bundle, out var rewardCapped);

            if (!TryPickRepeatingTemplate(definition.QuestType, templateId, out var nextDefinition, out var startsNewCycle))
            {
                return false; // 카탈로그에 반복 템플릿이 하나도 없음(설정 확인 필요)
            }

            var applied = await progress.TryApplyAndSaveAsync(
                GameProgressChange.ClaimRepeatingQuestReward(
                    templateId,
                    claimBundle,
                    nextDefinition.QuestId,
                    startsNewCycle));
            if (applied)
            {
                Debug.Log($"[Quest] 반복 임무 보상 수령: {definition.DisplayName} -> 다음: {nextDefinition.DisplayName}");
                LogCappedQuestReward(definition, rewardCapped);
                RewardClaimed?.Invoke(templateId, claimBundle);
            }

            return applied;
        }

        // 레벨 도달 등 임계값형 조건은 진행도를 이벤트로 누적하지 않으므로, 보상 수령 검증이 저장 데이터를
        // 보게 만들려면 여기서 현재 값을 한 번 저장에 반영해 둬야 한다.
        private static async Task SyncRepeatingThresholdProgressAsync(QuestDefinition definition)
        {
            var resolvedTarget = ResolveRepeatingTarget(definition, GetProgress(definition.QuestId).RepeatCycleCount);
            await SyncThresholdProgressAsync(definition.QuestId, definition.ConditionType, resolvedTarget);
        }
    }
}
