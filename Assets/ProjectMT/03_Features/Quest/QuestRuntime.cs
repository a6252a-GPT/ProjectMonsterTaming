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

        // 실제 게임 이벤트(몬스터 처치 등)가 붙기 전까지 진행도를 검증된 방식으로 올리는 진입점.
        // 6.2(진행 이벤트 연결) 단계에서 각 시스템의 이벤트 핸들러가 이 메서드를 호출하게 된다.
        public static async Task<bool> TryAdvanceProgressAsync(QuestId questId, long amount)
        {
            if (!IsReady || amount == 0L || !catalog.TryGet(questId, out var definition))
            {
                return false;
            }

            var current = GetProgress(questId);
            if (current.Completed)
            {
                return false;
            }

            var newProgress = current.CurrentProgress + amount;
            return await progress.TryApplyAndSaveAsync(
                GameProgressChange.SetQuestProgress(
                    questId,
                    current.CurrentProgress,
                    newProgress,
                    definition.TargetValue));
        }

        // 완료된 퀘스트의 보상을 수령한다. 우편함이 아직 없어서 지급과 함께 전체 정보를 콘솔에 출력한다.
        public static async Task<bool> TryClaimRewardAsync(QuestId questId)
        {
            if (!IsReady || !catalog.TryGet(questId, out var definition) || !CanClaimReward(questId))
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
