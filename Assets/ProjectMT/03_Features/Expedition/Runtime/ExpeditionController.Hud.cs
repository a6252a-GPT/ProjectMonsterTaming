using System;
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
        private void ShowReinforcementWarning(bool visible)
        {
            if (reinforcementWarningText != null)
            {
                reinforcementWarningText.gameObject.SetActive(visible);
            }
        }

        private void UpdateHud()
        {
            var modeChanged = !hudCacheValid || displayedMode != currentMode ||
                              displayedDifficulty != currentDifficulty;
            if (modeText != null && modeChanged)
            {
                var difficulty = currentDifficulty == ExpeditionDifficulty.Hard ? "하드" : "일반";
                var runMode = currentMode == ExpeditionRunMode.Challenge ? "도전" : "반복";
                modeText.text = $"{difficulty} · {runMode}";
            }

            if (stageText != null && (!hudCacheValid || displayedStage != currentStage))
            {
                var difficulty = currentDifficulty == ExpeditionDifficulty.Hard ? "하드" : "일반";
                stageText.text = $"{difficulty} 원정대 {currentStage}";
            }

            if (waveText != null &&
                (!hudCacheValid || displayedWave != currentWave || displayedWaveCount != waveCount))
            {
                waveText.text = $"웨이브 {currentWave}/{Mathf.Max(1, waveCount)}";
            }

            var allyCount = combatWorld == null ? 0 : combatWorld.CountAlive(UnitTeam.Player);
            var enemyCount = combatWorld == null ? 0 : combatWorld.CountAlive(UnitTeam.Enemy);
            if (countText != null && (!hudCacheValid || displayedAllyCount != allyCount ||
                                      displayedEnemyCount != enemyCount))
            {
                countText.text = $"아군 {allyCount}  적군 {enemyCount}";
            }

            var timerSeconds = currentMode == ExpeditionRunMode.Challenge
                ? Mathf.CeilToInt(challengeTimeRemaining)
                : -1;
            if (timerText != null && (modeChanged || displayedTimerSeconds != timerSeconds))
            {
                timerText.text = currentMode == ExpeditionRunMode.Challenge
                    ? $"남은 시간 {timerSeconds}초"
                    : "시간 제한 없음";
            }

            var modeInteractable = !settling &&
                (currentMode == ExpeditionRunMode.Repeat || progress == null ||
                 progress.View.ActiveLastClearedStage > 0);
            if (modeButton != null && (!hudCacheValid || displayedModeInteractable != modeInteractable))
            {
                modeButton.interactable = modeInteractable;
            }

            if (progressFill != null && (!hudCacheValid ||
                                          displayedDefeatedEnemyCount != defeatedEnemyCount ||
                                          displayedRunEnemyTotalCount != runEnemyTotalCount))
            {
                var progressRatio = runEnemyTotalCount <= 0
                    ? 0f
                    : Mathf.Clamp01((float)defeatedEnemyCount / runEnemyTotalCount);
                var size = progressFill.sizeDelta;
                size.x = progressFillMaxWidth * progressRatio;
                progressFill.sizeDelta = size;
            }

            displayedMode = currentMode;
            displayedDifficulty = currentDifficulty;
            displayedStage = currentStage;
            displayedWave = currentWave;
            displayedWaveCount = waveCount;
            displayedAllyCount = allyCount;
            displayedEnemyCount = enemyCount;
            displayedTimerSeconds = timerSeconds;
            displayedDefeatedEnemyCount = defeatedEnemyCount;
            displayedRunEnemyTotalCount = runEnemyTotalCount;
            displayedModeInteractable = modeInteractable;
            hudCacheValid = true;
        }

        private void InvalidateHudCache()
        {
            hudCacheValid = false;
        }
    }
}
