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
    }
}
