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
    public static partial class QuestRuntime
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

        // 현재 탭에서 수령 가능한 퀘스트의 보상과 수령 상태를 한 저장으로 함께 확정한다.
        // 반환된 Reward는 호출부가 수령 연출을 만들 때 쓴다(비동기라 out 대신 튜플로 반환).
        public static async Task<(bool Success, RewardBundle Reward)> TryClaimAllRewardsAsync(QuestType type)
        {
            if (!IsReady || (type != QuestType.Daily && type != QuestType.Weekly))
            {
                return (false, RewardBundle.Empty);
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
                    return (false, RewardBundle.Empty);
                }

                if (!RewardBundle.TryCombine(combined, bundle, out combined))
                {
                    Debug.LogWarning(
                        $"[Quest] 일괄 보상 수령 실패: 합산 중 수치가 허용 범위를 넘음 " +
                        $"(questId={definition.QuestId.Value})");
                    return (false, RewardBundle.Empty);
                }

                ids.Add(definition.QuestId);
                bundles.Add(bundle);
            }

            if (ids.Count == 0)
            {
                return (false, RewardBundle.Empty);
            }

            var applied = await progress.TryApplyAndSaveAsync(GameProgressChange.ClaimQuestRewards(ids, combined));
            if (!applied)
            {
                return (false, RewardBundle.Empty);
            }

            for (var i = 0; i < ids.Count; i++)
            {
                RewardClaimed?.Invoke(ids[i], bundles[i]);
            }

            Debug.Log($"[Quest] {QuestTypeInfo.GetDisplayName(type)} 임무 보상 {ids.Count}개 일괄 수령");
            return (true, combined);
        }
    }
}
