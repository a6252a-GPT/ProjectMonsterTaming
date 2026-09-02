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
        private void EnsureReinforcementWarningText()
        {
            if (reinforcementWarningText != null || waveText == null || waveText.canvas == null)
            {
                return;
            }

            var warningObject = new GameObject(
                "ReinforcementWarning",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            warningObject.layer = waveText.gameObject.layer;
            warningObject.transform.SetParent(waveText.canvas.transform, false);
            warningObject.transform.SetAsLastSibling();
            var rect = warningObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -260f); // 상단 HUD보다 아래인 실제 전투 안전 영역
            rect.sizeDelta = new Vector2(420f, 58f);

            reinforcementWarningText = warningObject.GetComponent<TextMeshProUGUI>();
            reinforcementWarningText.text = "증원 접근!";
            reinforcementWarningText.font = waveText.font; // 필요한 글리프가 포함된 정식 Font Asset을 그대로 사용
            reinforcementWarningText.fontSize = Mathf.Max(32f, waveText.fontSize + 10f);
            reinforcementWarningText.fontStyle = FontStyles.Bold;
            reinforcementWarningText.alignment = TextAlignmentOptions.Center;
            reinforcementWarningText.color = new Color(1f, 0.68f, 0.2f, 1f);
            reinforcementWarningText.raycastTarget = false;
            warningObject.SetActive(false);
            ownsRuntimeReinforcementWarning = true;
        }

        private void ShowReinforcementWarning(bool visible)
        {
            if (visible)
            {
                EnsureReinforcementWarningText();
            }

            if (reinforcementWarningText != null)
            {
                reinforcementWarningText.gameObject.SetActive(visible);
            }
        }

        private void ReleaseRuntimeReinforcementWarningText()
        {
            if (!ownsRuntimeReinforcementWarning || reinforcementWarningText == null)
            {
                ShowReinforcementWarning(false);
                return;
            }

            var warningObject = reinforcementWarningText.gameObject;
            reinforcementWarningText = null;
            ownsRuntimeReinforcementWarning = false;
            if (Application.isPlaying)
            {
                Destroy(warningObject);
            }
            // Play Mode 종료 중에는 Unity가 생성한 경고 UI를 함께 정리한다.
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
