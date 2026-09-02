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
        // 짧은 시간에 몰린 전투 이벤트를 조건별로 합친 뒤 한 저장 요청으로 확정한다.
        public static Task AdvanceAllOfConditionAsync(QuestConditionType conditionType, long amount)
        {
            if (!IsReady || amount <= 0L || IsThresholdCondition(conditionType))
            {
                return Task.CompletedTask;
            }

            lock (pendingProgressSync)
            {
                pendingProgressAmounts.TryGetValue(conditionType, out var pending);
                pendingProgressAmounts[conditionType] = SaturatingAdd(pending, amount);
                if (pendingProgressFlushTask == null || pendingProgressFlushTask.IsCompleted)
                {
                    pendingProgressFlushTask = FlushPendingProgressAfterDelayAsync(configurationVersion);
                }

                return pendingProgressFlushTask;
            }
        }

        private static async Task FlushPendingProgressAfterDelayAsync(long expectedConfigurationVersion)
        {
            while (true)
            {
                await Task.Delay(ProgressBatchDelayMilliseconds);
                if (!TryTakePendingProgressSnapshot(expectedConfigurationVersion, out var snapshot))
                {
                    return;
                }

                if (snapshot.Count > 0)
                {
                    await ApplyPendingProgressAsync(snapshot, expectedConfigurationVersion);
                }

                lock (pendingProgressSync)
                {
                    if (expectedConfigurationVersion != configurationVersion)
                    {
                        return;
                    }

                    if (pendingProgressAmounts.Count == 0)
                    {
                        pendingProgressFlushTask = null;
                        return;
                    }
                }
            }
        }

        public static async Task FlushPendingProgressAsync()
        {
            long expectedConfigurationVersion;
            lock (pendingProgressSync)
            {
                expectedConfigurationVersion = configurationVersion;
            }

            if (TryTakePendingProgressSnapshot(expectedConfigurationVersion, out var snapshot) && snapshot.Count > 0)
            {
                await ApplyPendingProgressAsync(snapshot, expectedConfigurationVersion);
            }
        }

        private static bool TryTakePendingProgressSnapshot(
            long expectedConfigurationVersion,
            out Dictionary<QuestConditionType, long> snapshot)
        {
            lock (pendingProgressSync)
            {
                if (expectedConfigurationVersion != configurationVersion)
                {
                    snapshot = null;
                    return false;
                }

                snapshot = new Dictionary<QuestConditionType, long>(pendingProgressAmounts);
                pendingProgressAmounts.Clear();
                return true;
            }
        }

        private static async Task ApplyPendingProgressAsync(
            IReadOnlyDictionary<QuestConditionType, long> amounts,
            long expectedConfigurationVersion)
        {
            await advanceGate.WaitAsync();
            try
            {
                for (var attempt = 0; attempt < MaxAdvanceRetryCount; attempt++)
                {
                    IGameProgressService targetProgress;
                    List<QuestProgressUpdate> updates;
                    lock (pendingProgressSync)
                    {
                        if (expectedConfigurationVersion != configurationVersion)
                        {
                            return;
                        }

                        if (!IsReady)
                        {
                            targetProgress = null;
                            updates = null;
                        }
                        else
                        {
                            targetProgress = progress;
                            updates = BuildProgressUpdates(amounts);
                        }
                    }

                    if (targetProgress == null)
                    {
                        RestorePendingProgress(amounts, expectedConfigurationVersion);
                        return;
                    }

                    if (updates.Count == 0)
                    {
                        return;
                    }

                    // Configure가 직후에 바뀌더라도 캡처한 이전 서비스로만 저장해 새 계정에 이벤트가 섞이지 않는다.
                    if (await targetProgress.TryApplyAndSaveAsync(GameProgressChange.SetQuestProgressBatch(updates)))
                    {
                        return;
                    }
                }

                RestorePendingProgress(amounts, expectedConfigurationVersion);
                Debug.LogWarning("[Quest] 묶음 진행도 저장이 반복 충돌로 지연됐습니다. 다음 배치에서 다시 시도합니다.");
            }
            catch (Exception exception)
            {
                RestorePendingProgress(amounts, expectedConfigurationVersion);
                Debug.LogException(exception);
            }
            finally
            {
                advanceGate.Release();
            }
        }

        private static List<QuestProgressUpdate> BuildProgressUpdates(
            IReadOnlyDictionary<QuestConditionType, long> amounts)
        {
            var updates = new List<QuestProgressUpdate>();
            var definitions = catalog.Definitions;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null || !definition.IsEnabled || definition.IsRepeatingTemplate ||
                    !amounts.TryGetValue(definition.ConditionType, out var amount) || amount <= 0L ||
                    !ShouldTrackDefinition(definition))
                {
                    continue;
                }

                AddProgressUpdate(updates, definition, amount);
            }

            var activeId = progress.Quests.ActiveRepeatingTemplateId;
            if (activeId.IsValid && catalog.TryGet(activeId, out var activeDefinition) &&
                activeDefinition.IsRepeatingTemplate &&
                amounts.TryGetValue(activeDefinition.ConditionType, out var repeatingAmount) &&
                repeatingAmount > 0L)
            {
                AddProgressUpdate(updates, activeDefinition, repeatingAmount);
            }

            return updates;
        }

        private static void AddProgressUpdate(
            ICollection<QuestProgressUpdate> updates,
            QuestDefinition definition,
            long amount)
        {
            var current = GetProgress(definition.QuestId);
            if (current.Completed)
            {
                return;
            }

            var target = ResolveTargetValue(definition);
            updates.Add(new QuestProgressUpdate(
                definition.QuestId,
                current.CurrentProgress,
                SaturatingAdd(current.CurrentProgress, amount),
                target));
        }

        private static bool ShouldTrackDefinition(QuestDefinition definition)
        {
            if (definition.QuestType != QuestType.Main || !definition.RequiresActiveTracking)
            {
                return true;
            }

            return TryGetTrackedQuest(QuestType.Main, out var active, out _) &&
                   active != null && active.QuestId == definition.QuestId;
        }

        private static void RestorePendingProgress(
            IReadOnlyDictionary<QuestConditionType, long> amounts,
            long expectedConfigurationVersion)
        {
            lock (pendingProgressSync)
            {
                if (expectedConfigurationVersion != configurationVersion)
                {
                    return;
                }

                foreach (var pair in amounts)
                {
                    pendingProgressAmounts.TryGetValue(pair.Key, out var pending);
                    pendingProgressAmounts[pair.Key] = SaturatingAdd(pending, pair.Value);
                }
            }
        }

        private static long SaturatingAdd(long first, long second) =>
            first > long.MaxValue - second ? long.MaxValue : first + second;

        // 실제 게임 이벤트(몬스터 처치 등)가 붙었을 때 진행도를 검증된 방식으로 올리는 진입점.
        // 6.2(진행 이벤트 연결) 단계의 각 시스템 이벤트 핸들러가 이 메서드(또는 AdvanceAllOfConditionAsync)를 호출한다.
        // advanceGate로 QuestRuntime을 거치는 모든 진행도 증가 호출을 직렬화해 두었으므로, 여기서는 더 이상
        // 같은 조건을 공유하는 다른 호출과 경쟁하지 않는다(재시도는 그 밖의 드문 저장 충돌에 대한 안전망).
        public static async Task<bool> TryAdvanceProgressAsync(QuestId questId, long amount)
        {
            if (!IsReady || amount <= 0L || !catalog.TryGet(questId, out var definition) ||
                IsThresholdCondition(definition.ConditionType) || !ShouldTrackDefinition(definition))
            {
                return false;
            }

            await advanceGate.WaitAsync();
            try
            {
                return await AdvanceProgressCoreAsync(questId, definition, amount);
            }
            finally
            {
                advanceGate.Release();
            }
        }

        // 실제 진행도 읽기-계산-저장 루프(advanceGate를 이미 잡고 있는 호출부에서만 사용).
        // 저장 시점에 "기대했던 이전 값"이 낡아서 거절되면(GameProgressData의 낙관적 동시성 검증) 최신 값을
        // 다시 읽어 재시도해서 진행도를 잃지 않는다.
        private static async Task<bool> AdvanceProgressCoreAsync(QuestId questId, QuestDefinition definition, long amount)
        {
            for (var attempt = 0; attempt < MaxAdvanceRetryCount; attempt++)
            {
                var current = GetProgress(questId);
                if (current.Completed)
                {
                    return false;
                }

                var newProgress = SaturatingAdd(current.CurrentProgress, amount);
                var applied = await progress.TryApplyAndSaveAsync(
                    GameProgressChange.SetQuestProgress(
                        questId,
                        current.CurrentProgress,
                        newProgress,
                        definition.TargetValue));
                if (applied)
                {
                    return true;
                }
            }

            return false;
        }

        // 임계값형 조건 전용 동기화 진입점(1회성·반복 공용). 조건 자체가 임계값형이 아니거나 이미 완료
        // 처리됐으면 아무것도 하지 않고, 현재 실제 값이 목표를 넘겼을 때만 저장 값을 그 값으로 맞춘다.
        private static async Task SyncThresholdProgressAsync(QuestId questId, QuestConditionType conditionType, long targetValue)
        {
            if (!IsThresholdCondition(conditionType))
            {
                return;
            }

            var saved = GetProgress(questId);
            if (saved.Completed)
            {
                return;
            }

            var currentValue = ResolveThresholdCurrentValue(conditionType);
            if (currentValue < targetValue)
            {
                return;
            }

            await progress.TryApplyAndSaveAsync(
                GameProgressChange.SetQuestProgress(questId, saved.CurrentProgress, currentValue, targetValue));
        }

        private static async Task SyncExpeditionProgressAsync(QuestDefinition definition)
        {
            if (definition.ConditionType != QuestConditionType.ExpeditionClear)
            {
                return;
            }

            var saved = GetProgress(definition.QuestId);
            if (saved.Completed)
            {
                return;
            }

            var derived = Math.Max(0L, progress.View.LastClearedStage);
            if (derived < definition.TargetValue)
            {
                return;
            }

            await progress.TryApplyAndSaveAsync(
                GameProgressChange.SetQuestProgress(
                    definition.QuestId,
                    saved.CurrentProgress,
                    derived,
                    definition.TargetValue));
        }
    }
}
