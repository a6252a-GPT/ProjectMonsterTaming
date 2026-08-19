using System;
using System.Collections.Generic;
using System.Text;
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

        // 반복 퀘스트는 사이클마다 목표 수치가 바뀌므로, 에셋 설명에 숫자를 직접 적어두는 대신
        // 이 토큰을 넣어두면 화면·로그에 표시할 때 지금 사이클의 실제 목표 수치로 바꿔서 보여준다.
        private const string TargetPlaceholder = "{target}";

        private static IGameProgressService progress;
        private static QuestCatalog catalog;
        private static float reportedCommanderPower;

        public static event Action Changed;

        // 우편함이 생기면 이 이벤트를 구독해서 즉시 지급 대신 questId·bundle 값으로 우편을 발송하도록 바꿀 수 있다.
        public static event Action<QuestId, RewardBundle> RewardClaimed;

        public static void Configure(IGameProgressService progressService, QuestCatalog questCatalog)
        {
            if (progress != null)
            {
                progress.Changed -= HandleProgressChanged;
            }

            progress = progressService;
            catalog = questCatalog;

            if (progress != null)
            {
                progress.Changed += HandleProgressChanged;
            }

            Changed?.Invoke();
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
        // 아직 실제 메뉴·버튼에는 연결하지 않았고, 필요할 때 이 메서드를 호출해서 잠그면 된다.
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

        // 실제 게임 이벤트(몬스터 뽑기 등)가 발생했을 때 호출하는 진입점.
        // 해당 조건 종류를 쓰는 미완료 퀘스트를 전부 amount만큼 진행시킨다(카탈로그에 여러 개 있어도 안전).
        // 반복 퀘스트 템플릿은 이 일반 루프에서 제외하고, 지금 활성인 템플릿만 별도로 진행시킨다.
        public static async Task AdvanceAllOfConditionAsync(QuestConditionType conditionType, long amount)
        {
            // 임계값형 조건은 이벤트로 누적하지 않고 조회 시점에 현재 값을 바로 읽으므로,
            // 이 경로로 들어오는 호출(과거 이벤트 연결 등)은 전부 무시해도 된다.
            if (!IsReady || amount <= 0L || IsThresholdCondition(conditionType))
            {
                return;
            }

            var definitions = catalog.Definitions;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition != null && definition.IsEnabled && !definition.IsRepeatingTemplate &&
                    definition.ConditionType == conditionType)
                {
                    await TryAdvanceProgressAsync(definition.QuestId, amount);
                }
            }

            await TryAdvanceActiveRepeatingProgressAsync(conditionType, amount);
        }

        // 지금 활성인 반복 템플릿이 이 조건과 같은 카운트형 조건일 때만 진행도를 올린다.
        // 임계값형 조건(레벨 도달 등)은 조회 시점에 현재 값을 바로 읽으므로 이벤트로 누적하지 않는다.
        private static async Task TryAdvanceActiveRepeatingProgressAsync(QuestConditionType conditionType, long amount)
        {
            if (IsThresholdCondition(conditionType))
            {
                return;
            }

            var activeId = progress.Quests.ActiveRepeatingTemplateId;
            if (!activeId.IsValid || !catalog.TryGet(activeId, out var activeDefinition) ||
                !activeDefinition.IsRepeatingTemplate || activeDefinition.ConditionType != conditionType)
            {
                return;
            }

            for (var attempt = 0; attempt < MaxAdvanceRetryCount; attempt++)
            {
                var current = GetProgress(activeId);
                if (current.Completed)
                {
                    return;
                }

                var resolvedTarget = ResolveRepeatingTarget(activeDefinition, current.RepeatCycleCount);
                var newProgress = current.CurrentProgress + amount;
                var applied = await progress.TryApplyAndSaveAsync(
                    GameProgressChange.SetQuestProgress(activeId, current.CurrentProgress, newProgress, resolvedTarget));
                if (applied)
                {
                    return;
                }
            }
        }

        // 실제 게임 이벤트(몬스터 처치 등)가 붙었을 때 진행도를 검증된 방식으로 올리는 진입점.
        // 6.2(진행 이벤트 연결) 단계의 각 시스템 이벤트 핸들러가 이 메서드(또는 AdvanceAllOfConditionAsync)를 호출한다.
        //
        // 광역 처치처럼 같은 프레임에 이 메서드가 여러 번 겹쳐 호출되면, 먼저 저장된 호출이 진행도를 올린 뒤
        // 나중 호출은 "기대했던 이전 값"이 낡아서 거절될 수 있다(GameProgressData의 낙관적 동시성 검증).
        // 이런 경우 진행도를 잃지 않도록 최신 값을 다시 읽어 재시도한다.
        public static async Task<bool> TryAdvanceProgressAsync(QuestId questId, long amount)
        {
            if (!IsReady || amount == 0L || !catalog.TryGet(questId, out var definition) ||
                IsThresholdCondition(definition.ConditionType))
            {
                return false;
            }

            for (var attempt = 0; attempt < MaxAdvanceRetryCount; attempt++)
            {
                var current = GetProgress(questId);
                if (current.Completed)
                {
                    return false;
                }

                var newProgress = current.CurrentProgress + amount;
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
                return await TryClaimRepeatingRewardAsync(questId, definition);
            }

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
