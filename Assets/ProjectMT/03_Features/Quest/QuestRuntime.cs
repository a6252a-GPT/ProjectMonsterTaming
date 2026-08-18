using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Quest;
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

        private static IGameProgressService progress;
        private static QuestCatalog catalog;

        public static event Action Changed;

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

        // HUD가 보여줄 현재 메인(또는 지정 종류) 퀘스트.
        // 원정대 클리어 퀘스트는 저장 퀘스트 진행이 아니라 실제 LastClearedStage를 기준으로 본다.
        public static bool TryGetTrackedQuest(
            QuestType type,
            out QuestDefinition definition,
            out QuestProgressEntryView progressView)
        {
            definition = null;
            progressView = default;
            if (catalog == null || !catalog.TryGetFirst(type, out var current))
            {
                return false;
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
                    definition = completed;
                    progressView = view;
                    return true;
                }
            }

            return false;
        }

        // 원정대 1을 아직 깨지 않았으면 해당 퀘스트는 항상 0/1·진행 중으로 표시한다.
        private static QuestProgressEntryView GetTrackedProgress(QuestDefinition definition)
        {
            var saved = GetProgress(definition.QuestId);
            if (definition.ConditionType != QuestConditionType.ExpeditionClear || progress == null)
            {
                return saved;
            }

            var derived = Math.Max(0L, progress.View.LastClearedStage);
            var current = Math.Min(derived, definition.TargetValue);
            var completed = current >= definition.TargetValue;
            return new QuestProgressEntryView(
                definition.QuestId,
                current,
                completed,
                completed && saved.RewardClaimed);
        }

        // 실제 게임 이벤트(몬스터 뽑기 등)가 발생했을 때 호출하는 진입점.
        // 해당 조건 종류를 쓰는 미완료 퀘스트를 전부 amount만큼 진행시킨다(카탈로그에 여러 개 있어도 안전).
        public static async Task AdvanceAllOfConditionAsync(QuestConditionType conditionType, long amount)
        {
            if (!IsReady || amount <= 0L)
            {
                return;
            }

            var definitions = catalog.Definitions;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition != null && definition.ConditionType == conditionType)
                {
                    await TryAdvanceProgressAsync(definition.QuestId, amount);
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
            if (!IsReady || amount == 0L || !catalog.TryGet(questId, out var definition))
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

        // 완료된 퀘스트의 보상을 수령한다. 우편함이 아직 없어서 지급과 함께 전체 정보를 콘솔에 출력한다.
        public static async Task<bool> TryClaimRewardAsync(QuestId questId)
        {
            if (!IsReady || !catalog.TryGet(questId, out var definition))
            {
                return false;
            }

            // 원정대 클리어 퀘스트는 화면에 LastClearedStage 기준 진행도를 보여준다(위 GetTrackedProgress 참고).
            // 이벤트 연결 전에 이미 그 단계를 깬 세이브처럼 저장된 카운터가 아직 못 따라간 경우,
            // 저장 값을 먼저 맞춰야 보상 수령 검증(RejectInvalidQuestClaim)을 통과할 수 있다.
            await SyncExpeditionProgressAsync(definition);

            if (!CanClaimReward(questId))
            {
                return false;
            }

            if (!definition.TryCreateRewardBundle(out var bundle))
            {
                return false;
            }

            var applied = await progress.TryApplyAndSaveAsync(GameProgressChange.ClaimQuestReward(questId, bundle));
            if (applied)
            {
                Debug.Log($"[Quest] 보상 수령: {definition.DisplayName}");
                LogQuestSnapshot(definition, GetProgress(questId));
            }

            return applied;
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

            var builder = new StringBuilder();
            builder.Append($"[Quest] {definition.DisplayName} ({definition.QuestId.Value})\n");
            builder.Append($" - 종류: {QuestTypeInfo.GetDisplayName(definition.QuestType)}\n");
            builder.Append($" - 설명: {definition.Description}\n");
            builder.Append($" - 조건: {QuestConditionTypeInfo.GetDisplayName(definition.ConditionType)}\n");
            builder.Append($" - 목표 수치: {definition.TargetValue}\n");
            builder.Append($" - 현재 진행도: {progressView.CurrentProgress} / {definition.TargetValue}\n");
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
