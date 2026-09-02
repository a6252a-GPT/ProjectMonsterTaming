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
        private static async Task RefreshPeriodsSafelyAsync()
        {
            try
            {
                await RefreshPeriodsAsync(DateTime.UtcNow);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        // 일일 퀘스트 초기화(KST 05:00 경계). GrowthDungeonDailyKeyRules와 같은 기간 ID 계산을 그대로 재사용한다.
        // Configure가 준비 완료 직후 1회 호출하며, 오늘 안에 이미 초기화했으면 아무 것도 하지 않는다.
        public static Task RefreshDailyQuestsAsync() => RefreshDailyQuestsAsync(DateTime.UtcNow);

        public static async Task RefreshDailyQuestsAsync(DateTime utcNow)
        {
            for (var attempt = 0; attempt < MaxAdvanceRetryCount; attempt++)
            {
                if (!IsReady)
                {
                    return;
                }

                var currentPeriod = QuestResetPeriodRules.GetDailyPeriodId(utcNow);
                var previousPeriod = progress.Quests.LastDailyResetPeriod;
                if (currentPeriod <= previousPeriod)
                {
                    return;
                }

                var dailyQuestIds = new List<QuestId>();
                var definitions = catalog.Definitions;
                for (var i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (definition != null && definition.QuestType == QuestType.Daily && !definition.IsRepeatingTemplate)
                    {
                        dailyQuestIds.Add(definition.QuestId);
                    }
                }

                if (await progress.TryApplyAndSaveAsync(
                        GameProgressChange.RefreshDailyQuests(previousPeriod, currentPeriod, dailyQuestIds)))
                {
                    return;
                }
            }
        }

        // 주간 퀘스트 초기화. 월요일 KST 05:00 경계를 사용한다.
        // Configure가 준비 완료 직후 1회 호출하며, 이번 주 안에 이미 초기화했으면 아무 것도 하지 않는다.
        public static Task RefreshWeeklyQuestsAsync() => RefreshWeeklyQuestsAsync(DateTime.UtcNow);

        public static async Task RefreshWeeklyQuestsAsync(DateTime utcNow)
        {
            for (var attempt = 0; attempt < MaxAdvanceRetryCount; attempt++)
            {
                if (!IsReady)
                {
                    return;
                }

                var currentPeriod = QuestResetPeriodRules.GetWeeklyPeriodId(utcNow);
                var previousPeriod = progress.Quests.LastWeeklyResetPeriod;
                if (currentPeriod <= previousPeriod)
                {
                    return;
                }

                var weeklyQuestIds = new List<QuestId>();
                var definitions = catalog.Definitions;
                for (var i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (definition != null && definition.QuestType == QuestType.Weekly && !definition.IsRepeatingTemplate)
                    {
                        weeklyQuestIds.Add(definition.QuestId);
                    }
                }

                if (await progress.TryApplyAndSaveAsync(
                        GameProgressChange.RefreshWeeklyQuests(previousPeriod, currentPeriod, weeklyQuestIds)))
                {
                    return;
                }
            }
        }

        public static async Task RefreshPeriodsAsync(DateTime utcNow)
        {
            await FlushPendingProgressAsync();
            await RefreshDailyQuestsAsync(utcNow);
            await RefreshWeeklyQuestsAsync(utcNow);
        }

        // 디버그/보정용: 일일·주간 퀘스트 진행도만 전부 0으로 되돌린다(메인 스토리 퀘스트·반복 템플릿은 그대로 둔다).
        // 같은 조건을 공유하는 일일·주간 카운터가 서로 어긋나 있을 때 다시 나란히 맞추고 싶으면
        // QuestDebugController 등에서 호출한다.
        public static async Task<bool> ResetDailyAndWeeklyProgressAsync()
        {
            if (!IsReady)
            {
                return false;
            }

            var questIds = new List<QuestId>();
            var definitions = catalog.Definitions;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition != null && !definition.IsRepeatingTemplate &&
                    (definition.QuestType == QuestType.Daily || definition.QuestType == QuestType.Weekly))
                {
                    questIds.Add(definition.QuestId);
                }
            }

            return await progress.TryApplyAndSaveAsync(GameProgressChange.ResetQuestProgress(questIds));
        }
    }
}
