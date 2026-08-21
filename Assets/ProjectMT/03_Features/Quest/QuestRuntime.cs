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
    // 퀘스트 카탈로그 조회 + GameProgressData 연동 파사드(CommanderPotentialRuntime과 동일한 구조).
    // 진행도 갱신·보상 수령은 GameProgressChange를 거쳐 저장까지 확정된다.
    public static class QuestRuntime
    {
        // 같은 프레임에 이벤트가 여러 번 겹칠 때(광역 처치로 여러 마리 동시 사망 등) 낙관적 동시성
        // 충돌로 거절된 진행도 증가를 최신 값으로 다시 계산해 재시도하는 횟수.
        private const int MaxAdvanceRetryCount = 8;
        private const int ProgressBatchDelayMilliseconds = 250;

        // 진행도 증가 호출(AdvanceAllOfConditionAsync/TryAdvanceProgressAsync) 전체를 이 게이트로 직렬화한다.
        // 그렇지 않으면 광역 처치처럼 같은 조건의 이벤트가 짧은 시간에 몰릴 때 같은 퀘스트의 이전 값을 두고
        // 경쟁하다가 재시도 한도(MaxAdvanceRetryCount)를 넘겨 일부 증가가 누락될 수 있다.
        private static readonly SemaphoreSlim advanceGate = new SemaphoreSlim(1, 1);

        // 반복 퀘스트는 사이클마다 목표 수치가 바뀌므로, 에셋 설명에 숫자를 직접 적어두는 대신
        // 이 토큰을 넣어두면 화면·로그에 표시할 때 지금 사이클의 실제 목표 수치로 바꿔서 보여준다.
        private const string TargetPlaceholder = "{target}";

        private static IGameProgressService progress;
        private static QuestCatalog catalog;
        private static float reportedCommanderPower;
        private static readonly object pendingProgressSync = new object();
        private static readonly Dictionary<QuestConditionType, long> pendingProgressAmounts =
            new Dictionary<QuestConditionType, long>();
        private static Task pendingProgressFlushTask;
        private static long configurationVersion;

        public static event Action Changed;

        // 보상 수령 후 연출·로그가 필요한 화면에서 쓰는 알림. 실제 보상은 저장 데이터에 즉시 반영한다.
        public static event Action<QuestId, RewardBundle> RewardClaimed;

        public static void Configure(IGameProgressService progressService, QuestCatalog questCatalog)
        {
            IGameProgressService previousProgress;
            bool configurationUnchanged;
            lock (pendingProgressSync)
            {
                configurationUnchanged = ReferenceEquals(progress, progressService) &&
                                         ReferenceEquals(catalog, questCatalog);
                previousProgress = progress;
                if (!configurationUnchanged)
                {
                    progress = progressService;
                    catalog = questCatalog;
                    configurationVersion++;
                    pendingProgressAmounts.Clear();
                    pendingProgressFlushTask = null;
                }
            }

            if (configurationUnchanged)
            {
                Changed?.Invoke();
                if (IsReady)
                {
                    _ = RefreshPeriodsSafelyAsync();
                }

                return;
            }

            if (previousProgress != null)
            {
                previousProgress.Changed -= HandleProgressChanged;
            }

            if (progress != null)
            {
                progress.Changed += HandleProgressChanged;
            }

            Changed?.Invoke();

            // 로그인·재연결 시점에 KST 05:00 경계(또는 7일 경계)를 이미 넘겼으면 일일·주간 퀘스트를 초기화한다.
            // AppRootHost의 초기화 순서와 무관하게 항상 "준비 완료 직후"에 걸리도록 여기서 직접 호출한다.
            if (IsReady)
            {
                _ = RefreshPeriodsSafelyAsync();
            }
        }

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

        public static bool IsReady => progress != null && progress.IsLoaded && catalog != null;

        private static void HandleProgressChanged() => Changed?.Invoke();

        public static IReadOnlyList<QuestDefinition> Definitions =>
            catalog != null ? catalog.Definitions : Array.Empty<QuestDefinition>();

        public static bool TryGetDefinition(QuestId questId, out QuestDefinition definition)
        {
            if (catalog == null)
            {
                definition = null;
                return false;
            }

            return catalog.TryGet(questId, out definition);
        }

        // 일일·주간처럼 여러 퀘스트가 동시에 진행되는 화면용 목록 조회. 메인 퀘스트의 선행 체인과 달리
        // 활성화된 정의를 카탈로그 등록 순서 그대로 전부 돌려준다(반복 퀘스트 템플릿은 별도 풀이므로 제외).
        public static IReadOnlyList<QuestDefinition> GetQuestsByType(QuestType type)
        {
            if (catalog == null)
            {
                return Array.Empty<QuestDefinition>();
            }

            var result = new List<QuestDefinition>();
            var definitions = catalog.Definitions;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition != null && definition.IsEnabled && !definition.IsRepeatingTemplate &&
                    definition.QuestType == type)
                {
                    result.Add(definition);
                }
            }

            return result;
        }

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

            if (!TryPickRepeatingTemplate(type, default, out var picked))
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

        // 다음에 추적할 반복 템플릿을 무작위로 고른다. excludeId(방금 끝난 템플릿)는 같은 퀘스트가
        // 연달아 나오지 않도록 제외하고, 이미 최대 등장 횟수를 채운 템플릿도 후보에서 뺀다.
        private static bool TryPickRepeatingTemplate(QuestType type, QuestId excludeId, out QuestDefinition result)
        {
            var candidates = new List<QuestDefinition>();
            foreach (var candidate in catalog.GetRepeatingTemplates(type))
            {
                if (candidate.QuestId == excludeId || IsRepeatingTemplateExhausted(candidate))
                {
                    continue;
                }

                candidates.Add(candidate);
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

        // 완료된 퀘스트의 보상을 수령한다. 우편함이 아직 없어서 지급과 함께 전체 정보를 콘솔에 출력하고,
        // 동시에 RewardClaimed 이벤트로 questId·bundle 값을 내보낸다(우편함 도입 시 그대로 재사용).
        public static async Task<bool> TryClaimRewardAsync(QuestId questId)
        {
            if (!IsReady)
            {
                Debug.LogWarning($"[Quest] 보상 수령 실패: QuestRuntime이 아직 준비되지 않음 (questId={questId.Value})");
                return false;
            }

            if (!catalog.TryGet(questId, out var definition))
            {
                Debug.LogWarning($"[Quest] 보상 수령 실패: 카탈로그에 없는 퀘스트 ID (questId={questId.Value})");
                return false;
            }

            if (definition.IsRepeatingTemplate)
            {
                await FlushPendingProgressAsync();
                return await TryClaimRepeatingRewardAsync(questId, definition);
            }

            await FlushPendingProgressAsync();

            // 원정대 클리어 퀘스트는 화면에 LastClearedStage 기준 진행도를 보여준다(위 GetTrackedProgress 참고).
            // 이벤트 연결 전에 이미 그 단계를 깬 세이브처럼 저장된 카운터가 아직 못 따라간 경우,
            // 저장 값을 먼저 맞춰야 보상 수령 검증(RejectInvalidQuestClaim)을 통과할 수 있다.
            await SyncExpeditionProgressAsync(definition);
            // 보유 종 수 도달 등 임계값형 조건도 마찬가지로, 퀘스트가 생기기 전부터 이미 조건을 채워 둔
            // 경우를 대비해 저장 값을 현재 실제 값으로 한 번 맞춰 둔다.
            await SyncThresholdProgressAsync(definition.QuestId, definition.ConditionType, definition.TargetValue);

            if (!CanClaimReward(questId))
            {
                var view = GetProgress(questId);
                Debug.LogWarning(
                    $"[Quest] 보상 수령 실패: 완료되지 않았거나 이미 수령함 (questId={questId.Value}, " +
                    $"진행도={view.CurrentProgress}/{ResolveTargetValue(definition)}, 완료={view.Completed}, 수령={view.RewardClaimed})");
                return false;
            }

            if (!definition.TryCreateRewardBundle(out var bundle))
            {
                Debug.LogWarning(
                    $"[Quest] 보상 수령 실패: 보상 정의가 비어있거나 잘못됨 (questId={questId.Value}, " +
                    $"reward={(definition.Reward != null ? definition.Reward.name : "null")})");
                return false;
            }

            var applied = await progress.TryApplyAndSaveAsync(GameProgressChange.ClaimQuestReward(questId, bundle));
            if (applied)
            {
                Debug.Log($"[Quest] 보상 수령: {definition.DisplayName}");
                LogQuestSnapshot(definition, GetProgress(questId));
                RewardClaimed?.Invoke(questId, bundle);
            }
            else
            {
                Debug.LogWarning(
                    $"[Quest] 보상 수령 실패: 저장 적용이 거절됨(questId={questId.Value}). " +
                    "같은 프레임에 다른 진행도 갱신과 겹쳤을 수 있으니 다시 시도해 보세요.");
            }

            return applied;
        }

        // 현재 탭에서 수령 가능한 퀘스트의 보상과 수령 상태를 한 저장으로 함께 확정한다.
        public static async Task<bool> TryClaimAllRewardsAsync(QuestType type)
        {
            if (!IsReady || (type != QuestType.Daily && type != QuestType.Weekly))
            {
                return false;
            }

            await FlushPendingProgressAsync();
            var ids = new List<QuestId>();
            var bundles = new List<RewardBundle>();
            var combined = RewardBundle.Empty;
            var definitions = GetQuestsByType(type);
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (!CanClaimReward(definition.QuestId))
                {
                    continue;
                }

                if (!definition.TryCreateRewardBundle(out var bundle))
                {
                    Debug.LogWarning(
                        $"[Quest] 일괄 보상 수령 실패: 보상 정의가 비어있거나 잘못됨 " +
                        $"(questId={definition.QuestId.Value})");
                    return false;
                }

                if (!RewardBundle.TryCombine(combined, bundle, out combined))
                {
                    Debug.LogWarning(
                        $"[Quest] 일괄 보상 수령 실패: 합산 중 수치가 허용 범위를 넘음 " +
                        $"(questId={definition.QuestId.Value})");
                    return false;
                }

                ids.Add(definition.QuestId);
                bundles.Add(bundle);
            }

            if (ids.Count == 0)
            {
                return false;
            }

            var applied = await progress.TryApplyAndSaveAsync(GameProgressChange.ClaimQuestRewards(ids, combined));
            if (!applied)
            {
                return false;
            }

            for (var i = 0; i < ids.Count; i++)
            {
                RewardClaimed?.Invoke(ids[i], bundles[i]);
            }

            Debug.Log($"[Quest] {QuestTypeInfo.GetDisplayName(type)} 임무 보상 {ids.Count}개 일괄 수령");
            return true;
        }

        // 반복 퀘스트 템플릿 전용 보상 수령. 임계값형 조건은 최신 값을 저장에 반영해서 검증을 통과시키고,
        // 성공하면 다음에 추적할 템플릿을 무작위로 골라 같은 저장 요청 안에서 사이클 전환까지 함께 처리한다.
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

            if (!TryPickRepeatingTemplate(definition.QuestType, templateId, out var nextDefinition))
            {
                return false; // 카탈로그에 반복 템플릿이 하나도 없음(설정 확인 필요)
            }

            var applied = await progress.TryApplyAndSaveAsync(
                GameProgressChange.ClaimRepeatingQuestReward(templateId, bundle, nextDefinition.QuestId));
            if (applied)
            {
                Debug.Log($"[Quest] 반복 임무 보상 수령: {definition.DisplayName} -> 다음: {nextDefinition.DisplayName}");
                RewardClaimed?.Invoke(templateId, bundle);
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
