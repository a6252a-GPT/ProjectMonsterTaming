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
        // 저장된 진행 기록이 없으면 0진행 기본값을 돌려준다(퀘스트 최초 조회 시).
        public static QuestProgressEntryView GetProgress(QuestId questId)
        {
            if (!IsReady || !progress.Quests.TryGet(questId, out var view))
            {
                return new QuestProgressEntryView(questId, 0L, false, false);
            }

            return view;
        }

        public static bool CanClaimReward(QuestId questId)
        {
            var view = GetProgress(questId);
            return view.Completed && !view.RewardClaimed;
        }

        public static bool IsRepeatingQuest(QuestId questId)
        {
            return catalog != null && catalog.TryGet(questId, out var definition) && definition.IsRepeatingTemplate;
        }

        // 반복 퀘스트 전체에서 이미 수령한 횟수를 합산해 튜토리얼 클릭 힌트를 계속 보여줄지 판정한다.
        // 템플릿마다 따로 세지 않으므로, 어떤 템플릿이 뽑히더라도 첫 10개까지만 안내된다.
        public static bool AreRepeatingQuestClickHintsEnabled(QuestType type, int completedQuestLimit)
        {
            if (catalog == null || completedQuestLimit <= 0)
            {
                return false;
            }

            var completedQuestCount = 0;
            foreach (var candidate in catalog.GetRepeatingTemplates(type))
            {
                completedQuestCount += GetProgress(candidate.QuestId).RepeatCycleCount;
                if (completedQuestCount >= completedQuestLimit)
                {
                    return false;
                }
            }

            return true;
        }

        // 화면에 표시할 "지금 목표 수치"를 돌려준다. 일반 퀘스트는 카탈로그 고정값 그대로,
        // 반복 템플릿은 지금까지 완료한 사이클 수만큼 반영된 값이다(definition.TargetValue는 1회차 기준값일 뿐).
        public static long ResolveTargetValue(QuestDefinition definition)
        {
            if (definition == null)
            {
                return 1L;
            }

            return definition.IsRepeatingTemplate
                ? ResolveRepeatingTarget(definition, GetProgress(definition.QuestId).RepeatCycleCount)
                : definition.TargetValue;
        }

        // 설명에 {target} 토큰이 있으면 지금 사이클의 실제 목표 수치로 치환해서 돌려준다.
        // 토큰이 없는 일반 퀘스트는 원본 설명을 그대로 돌려준다.
        public static string ResolveDescription(QuestDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            var description = definition.Description;
            if (string.IsNullOrEmpty(description) || !description.Contains(TargetPlaceholder))
            {
                return description;
            }

            return description.Replace(TargetPlaceholder, ResolveTargetValue(definition).ToString());
        }

        // AppRootHost가 파티(전투력)를 다시 계산할 때마다 최신 값을 보고한다.
        // CommanderPowerReach 조건은 매번 새로 계산하지 않고 이 캐시된 값을 기준으로 판정한다.
        public static void ReportCommanderPower(float power)
        {
            reportedCommanderPower = Mathf.Max(0f, power);
        }

        // 기능 해금 잠금 조회 API. "해금 잠금 사용"을 체크한 퀘스트가 없으면 항상 true(기본 전부 열림).
        // 체크된 퀘스트가 있으면, 그 퀘스트의 보상을 받기 전까지 대상 콘텐츠를 잠금으로 취급한다.
        // 메인 HUD·확장 메뉴·군단장 잠재력 탭이 이 메서드를 공통으로 사용한다.
        public static bool IsUnlocked(QuestUnlockTarget target)
        {
            if (!IsReady)
            {
                return true;
            }

            var definitions = catalog.Definitions;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null || !definition.UnlockGateEnabled || !ContainsTarget(definition.UnlockTargets, target))
                {
                    continue;
                }

                if (!GetProgress(definition.QuestId).RewardClaimed)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsTarget(IReadOnlyList<QuestUnlockTarget> targets, QuestUnlockTarget target)
        {
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] == target)
                {
                    return true;
                }
            }

            return false;
        }

        // HUD가 보여줄 현재 메인(또는 지정 종류) 퀘스트.
        // 원정대 클리어 퀘스트는 저장 퀘스트 진행이 아니라 실제 LastClearedStage를 기준으로 본다.
        // 선형 체인이 끝까지 완료·수령되면 반복 퀘스트 풀로 자동 전환한다.
        public static bool TryGetTrackedQuest(
            QuestType type,
            out QuestDefinition definition,
            out QuestProgressEntryView progressView)
        {
            definition = null;
            progressView = default;
            if (catalog == null)
            {
                return false;
            }

            if (!catalog.TryGetFirst(type, out var current))
            {
                return TryGetActiveRepeatingQuest(type, out definition, out progressView);
            }

            while (current != null)
            {
                var view = GetTrackedProgress(current);

                // 목표를 채웠어도 보상을 아직 안 받았으면 "완료" 상태로 계속 보여준다.
                // 보상까지 받은 퀘스트만 다음 퀘스트로 넘어간다.
                if (!view.Completed || !view.RewardClaimed)
                {
                    definition = current;
                    progressView = view;
                    return true;
                }

                var completed = current;
                if (!catalog.TryGetNext(completed.QuestId, out current))
                {
                    return TryGetActiveRepeatingQuest(type, out definition, out progressView);
                }
            }

            return TryGetActiveRepeatingQuest(type, out definition, out progressView);
        }

        private static long ResolveThresholdCurrentValue(QuestConditionType type)
        {
            switch (type)
            {
                case QuestConditionType.MonsterOwnedCount:
                    return GetDistinctOwnedMonsterCount();
                case QuestConditionType.MonsterLevelReach:
                    return GetHighestOwnedMonsterLevel();
                case QuestConditionType.CommanderLevelReach:
                    return progress.View.Commander.Level;
                case QuestConditionType.CommanderHealthLevelReach:
                    return progress.View.CommanderLegionGrowth.GetLevel(CommanderLegionStat.MaxHealth);
                case QuestConditionType.CommanderAttackLevelReach:
                    return progress.View.CommanderLegionGrowth.GetLevel(CommanderLegionStat.AttackPower);
                case QuestConditionType.CommanderDefenseLevelReach:
                    return progress.View.CommanderLegionGrowth.GetLevel(CommanderLegionStat.Defense);
                case QuestConditionType.CommanderPowerReach:
                    return (long)reportedCommanderPower;
                case QuestConditionType.EquipmentSlotUpgradeReach:
                    return GetHighestEquipmentSlotLevel();
                case QuestConditionType.CommanderPotentialUnlockCount:
                    return CommanderPotentialRuntime.UnlockedSlotCount;
                default:
                    return 0L;
            }
        }

        // 서로 다른 몬스터를 몇 종 보유 중인지(중복 마리 수는 무시) 현재 로스터에서 직접 센다.
        private static long GetDistinctOwnedMonsterCount()
        {
            var owned = progress.View.Monsters.OwnedMonsters;
            var distinctIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < owned.Count; i++)
            {
                if (!string.IsNullOrEmpty(owned[i].MonsterId))
                {
                    distinctIds.Add(owned[i].MonsterId);
                }
            }

            return distinctIds.Count;
        }

        private static long GetHighestOwnedMonsterLevel()
        {
            var owned = progress.View.Monsters.OwnedMonsters;
            var highest = 0L;
            for (var i = 0; i < owned.Count; i++)
            {
                if (owned[i].Level > highest)
                {
                    highest = owned[i].Level;
                }
            }

            return highest;
        }

        private static long GetHighestEquipmentSlotLevel()
        {
            var highest = 0L;
            foreach (EquipmentPart part in Enum.GetValues(typeof(EquipmentPart)))
            {
                var level = EquipmentSlotUpgradeRuntime.GetLevel(part);
                if (level > highest)
                {
                    highest = level;
                }
            }

            return highest;
        }

        // 원정대 1을 아직 깨지 않았으면 해당 퀘스트는 항상 0/1·진행 중으로 표시한다.
        // 임계값형 조건(보유 종 수 등)도 마찬가지로 저장된 누적 카운터 대신 현재 실제 값을 그대로 보여준다.
        // 그래야 퀘스트가 생기기 전부터 이미 조건을 채워 둔 플레이어도 0부터 다시 채울 필요가 없다.
        private static QuestProgressEntryView GetTrackedProgress(QuestDefinition definition)
        {
            var saved = GetProgress(definition.QuestId);
            if (progress == null)
            {
                return saved;
            }

            if (definition.ConditionType == QuestConditionType.ExpeditionClear)
            {
                var derived = Math.Max(0L, progress.View.LastClearedStage);
                var current = Math.Min(derived, definition.TargetValue);
                var completed = current >= definition.TargetValue;
                return new QuestProgressEntryView(
                    definition.QuestId,
                    current,
                    completed,
                    completed && saved.RewardClaimed,
                    saved.RepeatCycleCount,
                    definition.TargetValue);
            }

            if (IsThresholdCondition(definition.ConditionType))
            {
                var currentValue = Math.Max(0L, ResolveThresholdCurrentValue(definition.ConditionType));
                var current = Math.Min(currentValue, definition.TargetValue);
                var completed = current >= definition.TargetValue;
                return new QuestProgressEntryView(
                    definition.QuestId,
                    current,
                    completed,
                    completed && saved.RewardClaimed,
                    saved.RepeatCycleCount,
                    definition.TargetValue);
            }

            return saved;
        }

        // 우편함이 아직 없어서, 퀘스트 이름·설명·조건·진행도·보상·해금 대상을 전부 콘솔에 대신 출력한다.
        public static void LogQuestSnapshot(QuestDefinition definition, QuestProgressEntryView progressView)
        {
            if (definition == null)
            {
                return;
            }

            var targetValue = ResolveTargetValue(definition);
            var builder = new StringBuilder();
            builder.Append($"[Quest] {definition.DisplayName} ({definition.QuestId.Value})\n");
            builder.Append($" - 종류: {QuestTypeInfo.GetDisplayName(definition.QuestType)}\n");
            builder.Append($" - 설명: {ResolveDescription(definition)}\n");
            builder.Append($" - 조건: {QuestConditionTypeInfo.GetDisplayName(definition.ConditionType)}\n");
            builder.Append($" - 목표 수치: {targetValue}\n");
            builder.Append($" - 현재 진행도: {progressView.CurrentProgress} / {targetValue}\n");
            builder.Append($" - 선행 퀘스트: {(definition.HasPrerequisite ? definition.PrerequisiteQuestId.Value : "없음")}\n");
            builder.Append($" - 보상: {FormatReward(definition)}\n");
            builder.Append($" - 해금 대상: {QuestUnlockTargetInfo.GetDisplayName(definition.UnlockTargets)}\n");
            builder.Append($" - 완료 여부: {progressView.Completed} / 보상 수령 여부: {progressView.RewardClaimed}");

            Debug.Log(builder.ToString());
        }

        public static string FormatReward(QuestDefinition definition)
        {
            if (definition == null || !definition.TryCreateRewardBundle(out var bundle) || bundle.IsEmpty)
            {
                return "없음";
            }

            var parts = new List<string>();
            if (bundle.Gold > 0L)
            {
                parts.Add($"골드 {bundle.Gold}");
            }

            if (bundle.CommanderExperience > 0L)
            {
                parts.Add($"군단장 경험치 {bundle.CommanderExperience}");
            }

            for (var i = 0; i < bundle.Items.Count; i++)
            {
                parts.Add($"{bundle.Items[i].ItemId} x{bundle.Items[i].Amount}");
            }

            return parts.Count == 0 ? "없음" : string.Join(", ", parts);
        }
    }
}
