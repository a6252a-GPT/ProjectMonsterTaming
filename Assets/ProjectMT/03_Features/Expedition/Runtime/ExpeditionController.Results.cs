using System;
using ProjectMT.Shared.Audio;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectMT.Features.Equipment;
using ProjectMT.Features.MainBattle;
using ProjectMT.Features.Quest;
using ProjectMT.Features.WorldDrops;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Equipment;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Quest;
using ProjectMT.Shared.Reward;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Expedition
{
    public sealed partial class ExpeditionController
    {
        private async void ToggleMode()
        {
            if (progress == null || settling)
            {
                return;
            }

            var view = progress.View;
            var nextMode = currentMode == ExpeditionRunMode.Challenge
                ? ExpeditionRunMode.Repeat
                : ExpeditionRunMode.Challenge;
            if (nextMode == ExpeditionRunMode.Repeat && view.ActiveLastClearedStage <= 0)
            {
                return;
            }

            var version = ++operationVersion; // 이전 모드 변경 결과 무효화
            running = false;
            settling = true;
            ResetWaveTracking();
            ResetPlayerTracking();
            CollectAllWorldDrops(); // 모드 변경 전 남은 드랍을 전부 획득 확정
            combatWorld.Clear();
            SetResult("모드 변경 중...");
            await FlushWorldDropsCheckpointAsync(); // 모드 변경도 현재 Run의 전체 획득분 저장
            if (this == null || version != operationVersion)
            {
                return;
            }

            var saved = await progress.TryApplyAndSaveAsync(GameProgressChange.SetExpeditionMode(nextMode));
            if (this == null || version != operationVersion)
            {
                return;
            }

            settling = false;
            if (saved)
            {
                StartFromSavedMode();
            }
        }

        private void FinishVictory()
        {
            if (!running)
            {
                return;
            }

            running = false;
            settling = true;
            CollectAllWorldDrops(); // 전투 종료 시 남은 표현도 획득으로 확정
            combatWorld.SetPaused(true); // 결과 연출 동안 전투 정지
            SetResult(resultFlash == null && currentMode == ExpeditionRunMode.Challenge
                ? "승리 정산 중..."
                : string.Empty);
            _ = ResolveVictoryAsync(++operationVersion); // 저장 후 새 Run 시작
        }

        private async Task ResolveVictoryAsync(int version)
        {
            await FlushWorldDropsCheckpointAsync();
            if (this == null || version != operationVersion)
            {
                return;
            }

            var settledMode = currentMode;
            var settledDifficulty = currentDifficulty;
            var settledStage = currentStage;
            var rewardStage = ExpeditionCampaignRules.ToProgressStage(settledDifficulty, settledStage);
            RewardBundle rewards;
            GameProgressChange change;
            switch (settledMode)
            {
                case ExpeditionRunMode.Challenge:
                    rewards = ExpeditionFirstClearRewardRules.Create(rewardStage);
                    change = GameProgressChange.RecordExpeditionFirstClear(settledStage, rewards);
                    break;
                case ExpeditionRunMode.Repeat:
                    rewards = ExpeditionRepeatClearRewardRules.Create(rewardStage);
                    change = GameProgressChange.RecordExpeditionRepeatClear(settledStage, rewards);
                    break;
                default:
                    Debug.LogError($"지원하지 않는 원정대 모드입니다: {settledMode}");
                    SetResult("원정대 모드 오류");
                    settling = false;
                    return;
            }

            var saved = false;
            try
            {
                saved = await progress.TryApplyAndSaveAsync(change);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            if (this == null || version != operationVersion)
            {
                return;
            }

            if (saved)
            {
                SfxEvents.Play2D(SfxEvents.Victory);
                try
                {
                    rewardPresentation?.PlayConfirmed(RewardPresentationRequest.FromBundle(rewards, itemCatalog));
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception); // 표현 실패는 저장을 되돌리지 않음
                }

                // 도전·반복 모드 관계없이 승리할 때마다 누적되는 일일·주간용 조건(원정대 승리 N회).
                _ = QuestRuntime.AdvanceAllOfConditionAsync(QuestConditionType.ExpeditionVictory, 1L);

                if (settledMode == ExpeditionRunMode.Challenge)
                {
                    var notice = ExpeditionResultNoticeFormatter.ChallengeVictory(
                        settledStage,
                        RewardPresentationRequest.FromBundle(rewards, itemCatalog));
                    if (resultFlash != null)
                    {
                        SetResult(string.Empty);
                        resultFlash.ShowClear(settledStage);
                    }
                    else
                    {
                        SetResult(settledDifficulty == ExpeditionDifficulty.Hard ? $"하드 {notice}" : notice);
                    }

                    // 새로운 단계를 처음 클리어했을 때만 "원정대 클리어" 퀘스트 진행(반복 클리어는 제외).
                    _ = QuestRuntime.AdvanceAllOfConditionAsync(QuestConditionType.ExpeditionClear, 1L);
                }
            }
            else
            {
                SetResult("보상 저장 실패 · 같은 단계 재시도");
            }

            if (settledMode == ExpeditionRunMode.Challenge || !saved)
            {
                await Task.Delay(TimeSpan.FromSeconds(Mathf.Max(
                    profile.ResultDelaySeconds,
                    resultFlash != null ? resultFlash.SequenceDuration : 0f)));
            }

            if (this == null || version != operationVersion)
            {
                return;
            }

            settling = false;
            StartFromSavedMode();
        }

        private void FinishDefeat()
        {
            if (!running)
            {
                return;
            }

            running = false;
            settling = true;
            SfxEvents.Play2D(SfxEvents.Defeat);
            CollectAllWorldDrops(); // 패배도 남은 드랍을 전부 획득 확정
            combatWorld.SetPaused(true);
            SetResult(resultFlash == null && currentMode == ExpeditionRunMode.Challenge
                ? "도전 실패"
                : string.Empty);
            _ = ResolveDefeatAsync(++operationVersion); // 실패 단계에서 반복 전환
        }

        private async Task ResolveDefeatAsync(int version)
        {
            await FlushWorldDropsCheckpointAsync(); // 패배 전 남은 드랍까지 전부 저장
            if (this == null || version != operationVersion)
            {
                return;
            }

            if (currentMode == ExpeditionRunMode.Challenge)
            {
                var lastClearedStage = progress.View.ActiveLastClearedStage;
                var repeatModeSaved = false;
                if (lastClearedStage > 0)
                {
                    try
                    {
                        repeatModeSaved = await progress.TryApplyAndSaveAsync(
                            GameProgressChange.SetExpeditionMode(ExpeditionRunMode.Repeat)); // 마지막 성공 단계 반복
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }

                    if (this == null || version != operationVersion)
                    {
                        return;
                    }
                }

                var notice = ExpeditionResultNoticeFormatter.ChallengeDefeat(
                    lastClearedStage,
                    repeatModeSaved);
                if (resultFlash != null)
                {
                    SetResult(string.Empty);
                    resultFlash.ShowFailure(notice);
                }
                else
                {
                    SetResult(notice);
                }
            }
            else
            {
                resultFlash?.ShowFailure($"{currentStage}단계 반복사냥을 다시 시작합니다");
            }

            await Task.Delay(TimeSpan.FromSeconds(Mathf.Max(
                profile.ResultDelaySeconds,
                resultFlash != null ? resultFlash.SequenceDuration : 0f)));
            if (this == null || version != operationVersion)
            {
                return;
            }

            settling = false;
            StartFromSavedMode();
        }

        private void SetResult(string message)
        {
            if (resultText != null)
            {
                resultText.text = message;
            }

            UpdateHud();
        }
    }
}
