using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.Expedition
{
    [DisallowMultipleComponent]
    public sealed class ExpeditionController : MonoBehaviour // 원정대 Run·Wave·정산 관리
    {
        [Header("Runtime")]
        [SerializeField] private ExpeditionSeedProfile profile; // 시드 밸런스 원본
        [SerializeField] private CombatWorld combatWorld; // 공용 전투 영역
        [SerializeField] private GameObject playerUnitPrefab; // 아군 원본
        [SerializeField] private GameObject enemyUnitPrefab; // 적 원본
        [SerializeField] private Transform[] playerSpawnPoints; // 아군 시작 위치
        [SerializeField] private Transform enemySpawnAnchor; // 적 진형 기준점

        [Header("HUD")]
        [SerializeField] private Button modeButton;
        [SerializeField] private TMP_Text modeText;
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text waveText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text resultText;

        private IGameProgressService progress; // 진행 조회·저장 계약
        private BattlePartySnapshot party; // 다음 Run에 사용할 최신 부대 사진
        private BattlePartySnapshot activeRunParty; // 현재 Run 시작 때 고정한 부대 사진
        private ExpeditionRunMode currentMode; // 도전·반복 상태
        private int currentStage; // 현재 실행 단계
        private int currentWave; // 현재 표시 웨이브
        private float waveElapsed; // 2웨이브 대기 시간
        private float challengeTimeRemaining; // 도전 남은 시간
        private bool waveTwoSpawned; // 두 번째 웨이브 출현 여부
        private bool running; // 전투 Tick 허용
        private bool settling; // 결과 저장 중
        private int operationVersion; // 늦은 비동기 결과 무효화
        private readonly Dictionary<UnitActor, int> enemyWaveByActor = new Dictionary<UnitActor, int>(); // 적별 소속 웨이브
        private readonly Dictionary<UnitActor, int> playerSlotByActor = new Dictionary<UnitActor, int>(); // 아군별 본부대 자리
        private readonly int[] aliveEnemiesByWave = new int[ExpeditionStageRules.WaveCount + 1]; // 웨이브별 생존 적
        private readonly bool[] climaxPlayedByWave = new bool[ExpeditionStageRules.WaveCount + 1]; // 웨이브당 한 번만 재생
        private int nextReserveIndex; // 다음에 투입할 예비 순서

        public bool IsRunning => running;
        public bool IsSettling => settling;

        public void SetPartyForNextRun(BattlePartySnapshot partySnapshot)
        {
            if (partySnapshot == null || partySnapshot.Units.Length == 0)
            {
                throw new ArgumentException("A non-empty party is required.", nameof(partySnapshot));
            }

            party = partySnapshot; // 현재 소환 유닛은 유지하고 다음 StartRun부터 사용
        }

        public void Initialize(IGameProgressService progressService, BattlePartySnapshot partySnapshot)
        {
            Shutdown();
            progress = progressService ?? throw new ArgumentNullException(nameof(progressService));
            party = partySnapshot ?? throw new ArgumentNullException(nameof(partySnapshot));
            if (modeButton != null)
            {
                modeButton.onClick.AddListener(ToggleMode);
            }

            StartFromSavedMode();
        }

        public void StartFromSavedMode()
        {
            if (progress == null || party == null)
            {
                return;
            }

            var view = progress.View;
            currentMode = view.ExpeditionMode;
            if (currentMode == ExpeditionRunMode.Repeat && view.LastClearedStage <= 0)
            {
                currentMode = ExpeditionRunMode.Challenge;
            }

            currentStage = currentMode == ExpeditionRunMode.Challenge // 모드별 실행 단계 선택
                ? view.CurrentChallengeStage
                : view.LastClearedStage;
            StartRun();
        }

        public void StopWithoutResult()
        {
            operationVersion++; // 진행 중 비동기 정산 취소
            running = false;
            settling = false;
            ResetWaveTracking();
            ResetPlayerTracking();
            combatWorld?.Clear();
            UpdateHud();
        }

        public void Shutdown()
        {
            operationVersion++;
            running = false;
            settling = false;
            if (modeButton != null)
            {
                modeButton.onClick.RemoveListener(ToggleMode);
            }

            ResetWaveTracking();
            ResetPlayerTracking();
            combatWorld?.Clear();
            progress = null;
            party = null;
            activeRunParty = null;
        }

        private void Update()
        {
            if (!running || combatWorld == null)
            {
                return;
            }

            waveElapsed += Time.deltaTime;
            if (currentMode == ExpeditionRunMode.Challenge)
            {
                challengeTimeRemaining = Mathf.Max(0f, challengeTimeRemaining - Time.deltaTime);
            }

            if (!waveTwoSpawned &&
                (waveElapsed >= profile.WaveIntervalSeconds || combatWorld.CountAlive(UnitTeam.Enemy) == 0))
            {
                SpawnWave(2); // 시간 경과 또는 전멸 시 두 번째 웨이브
                waveTwoSpawned = true;
                currentWave = 2;
            }

            if (combatWorld.CountAlive(UnitTeam.Player) == 0 ||
                (currentMode == ExpeditionRunMode.Challenge && challengeTimeRemaining <= 0f))
            {
                FinishDefeat();
                return;
            }

            if (waveTwoSpawned && combatWorld.CountAlive(UnitTeam.Enemy) == 0)
            {
                FinishVictory();
                return;
            }

            UpdateHud();
        }

        private void StartRun()
        {
            operationVersion++; // 이전 Run 콜백 무효화
            ResetWaveTracking();
            ResetPlayerTracking();
            activeRunParty = party; // 진행 중 편성 변경은 다음 Run부터 반영
            combatWorld.Clear();
            combatWorld.SetPaused(false);
            running = true;
            settling = false;
            currentWave = 1;
            waveElapsed = 0f;
            challengeTimeRemaining = profile.ChallengeTimeLimitSeconds;
            waveTwoSpawned = false;
            nextReserveIndex = 0;
            if (resultText != null)
            {
                resultText.text = string.Empty;
            }

            SpawnParty();
            SpawnWave(1);
            UpdateHud();
        }

        private void SpawnParty()
        {
            var units = activeRunParty.Units;
            for (var i = 0; i < units.Length && i < 5; i++) // 시드 본부대 최대 5기
            {
                var position = playerSpawnPoints != null && i < playerSpawnPoints.Length && playerSpawnPoints[i] != null
                    ? playerSpawnPoints[i].position
                    : transform.position + new Vector3(i * 0.8f, 0f, 0f);
                var request = new UnitSpawnRequest(
                    units[i].UnitId,
                    units[i].Stats,
                    UnitTeam.Player,
                    visualTint: units[i].VisualTint);
                TrackPlayerUnit(combatWorld.SpawnUnit(playerUnitPrefab, request, position, Quaternion.identity), i);
            }
        }

        private void TrackPlayerUnit(UnitActor actor, int slotIndex)
        {
            if (actor == null)
            {
                return;
            }

            playerSlotByActor[actor] = slotIndex;
            actor.Died += HandlePlayerUnitDied;
        }

        private void HandlePlayerUnitDied(UnitActor actor)
        {
            if (actor == null || !playerSlotByActor.TryGetValue(actor, out var slotIndex))
            {
                return;
            }

            actor.Died -= HandlePlayerUnitDied;
            playerSlotByActor.Remove(actor);
            if (!running)
            {
                return;
            }

            TryDeployNextReserve(slotIndex, actor.transform.position); // 쓰러진 자리로 순차 대타 투입
        }

        private bool TryDeployNextReserve(int slotIndex, Vector3 position)
        {
            var reserves = activeRunParty?.ReserveUnits ?? Array.Empty<BattleUnitSnapshot>();
            while (nextReserveIndex < reserves.Length)
            {
                var reserve = reserves[nextReserveIndex++];
                if (reserve == null)
                {
                    continue;
                }

                var request = new UnitSpawnRequest(
                    reserve.UnitId,
                    reserve.Stats,
                    UnitTeam.Player,
                    visualTint: reserve.VisualTint);
                var actor = combatWorld.SpawnUnit(playerUnitPrefab, request, position, Quaternion.identity);
                if (actor == null)
                {
                    continue;
                }

                TrackPlayerUnit(actor, slotIndex);
                return true;
            }

            return false;
        }

        private void ResetPlayerTracking()
        {
            foreach (var pair in playerSlotByActor)
            {
                if (pair.Key != null)
                {
                    pair.Key.Died -= HandlePlayerUnitDied;
                }
            }

            playerSlotByActor.Clear();
            nextReserveIndex = 0;
        }

        private void SpawnWave(int wave)
        {
            var count = ExpeditionStageRules.GetEnemiesPerWave(currentStage);
            var anchor = enemySpawnAnchor == null ? transform.position + new Vector3(4f, 0f, 4f) : enemySpawnAnchor.position;
            var formationRight = enemySpawnAnchor == null ? Vector3.right : enemySpawnAnchor.right;
            var formationForward = enemySpawnAnchor == null ? Vector3.forward : enemySpawnAnchor.forward;
            for (var i = 0; i < count; i++)
            {
                var formationOffset = ExpeditionStageRules.GetFormationOffset(i, count); // 실제 인원 기준 중앙 정렬
                var position = anchor +
                               formationRight * formationOffset.x +
                               formationForward * (formationOffset.y + (wave - 1) * 1.15f);
                var stats = profile.CreateEnemyStats(currentStage, i + wave * 10);
                var request = new UnitSpawnRequest($"enemy_{currentStage}_{wave}_{i}", stats, UnitTeam.Enemy);
                var actor = combatWorld.SpawnUnit(enemyUnitPrefab, request, position, Quaternion.Euler(0f, 180f, 0f));
                TrackWaveEnemy(actor, wave);
            }
        }

        private void TrackWaveEnemy(UnitActor actor, int wave)
        {
            if (actor == null || wave <= 0 || wave >= aliveEnemiesByWave.Length)
            {
                return;
            }

            enemyWaveByActor[actor] = wave;
            aliveEnemiesByWave[wave]++;
            actor.Died += HandleWaveEnemyDied;
        }

        private void HandleWaveEnemyDied(UnitActor actor)
        {
            if (actor == null || !enemyWaveByActor.TryGetValue(actor, out var wave))
            {
                return;
            }

            actor.Died -= HandleWaveEnemyDied;
            enemyWaveByActor.Remove(actor);
            aliveEnemiesByWave[wave] = Mathf.Max(0, aliveEnemiesByWave[wave] - 1);
            if (!running || aliveEnemiesByWave[wave] != 0 || climaxPlayedByWave[wave])
            {
                return;
            }

            climaxPlayedByWave[wave] = true;
            combatWorld?.PlayClimax(actor.transform.position, CombatClimaxStrength.Weak);
        }

        private void ResetWaveTracking()
        {
            foreach (var pair in enemyWaveByActor)
            {
                if (pair.Key != null)
                {
                    pair.Key.Died -= HandleWaveEnemyDied;
                }
            }

            enemyWaveByActor.Clear();
            Array.Clear(aliveEnemiesByWave, 0, aliveEnemiesByWave.Length);
            Array.Clear(climaxPlayedByWave, 0, climaxPlayedByWave.Length);
        }

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
            if (nextMode == ExpeditionRunMode.Repeat && view.LastClearedStage <= 0)
            {
                return;
            }

            var version = ++operationVersion; // 이전 모드 변경 결과 무효화
            running = false;
            settling = true;
            ResetWaveTracking();
            ResetPlayerTracking();
            combatWorld.Clear();
            SetResult("모드 변경 중...");
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
            combatWorld.SetPaused(true); // 결과 연출 동안 전투 정지
            SetResult("승리");
            _ = ResolveVictoryAsync(++operationVersion); // 저장 후 새 Run 시작
        }

        private async Task ResolveVictoryAsync(int version)
        {
            if (currentMode == ExpeditionRunMode.Challenge)
            {
                await progress.TryApplyAndSaveAsync(GameProgressChange.RecordChallengeVictory(currentStage));
            }

            await Task.Delay(TimeSpan.FromSeconds(profile.ResultDelaySeconds));
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
            combatWorld.SetPaused(true);
            SetResult("패배");
            _ = ResolveDefeatAsync(++operationVersion); // 실패 단계에서 반복 전환
        }

        private async Task ResolveDefeatAsync(int version)
        {
            if (currentMode == ExpeditionRunMode.Challenge && progress.View.LastClearedStage > 0)
            {
                await progress.TryApplyAndSaveAsync(GameProgressChange.SetExpeditionMode(ExpeditionRunMode.Repeat)); // 마지막 성공 단계 반복
            }

            await Task.Delay(TimeSpan.FromSeconds(profile.ResultDelaySeconds));
            if (this == null || version != operationVersion)
            {
                return;
            }

            settling = false;
            StartFromSavedMode();
        }

        private void UpdateHud()
        {
            if (modeText != null)
            {
                modeText.text = currentMode == ExpeditionRunMode.Challenge ? "도전" : "반복";
            }

            if (stageText != null)
            {
                stageText.text = $"원정대 {currentStage}";
            }

            if (waveText != null)
            {
                waveText.text = $"웨이브 {currentWave}/{ExpeditionStageRules.WaveCount}";
            }

            if (countText != null && combatWorld != null)
            {
                countText.text = $"아군 {combatWorld.CountAlive(UnitTeam.Player)}  적군 {combatWorld.CountAlive(UnitTeam.Enemy)}";
            }

            if (timerText != null)
            {
                timerText.text = currentMode == ExpeditionRunMode.Challenge
                    ? $"남은 시간 {Mathf.CeilToInt(challengeTimeRemaining)}초"
                    : "시간 제한 없음";
            }

            if (modeButton != null)
            {
                modeButton.interactable = !settling &&
                    (currentMode == ExpeditionRunMode.Repeat || progress == null || progress.View.LastClearedStage > 0);
            }
        }

        private void SetResult(string message)
        {
            if (resultText != null)
            {
                resultText.text = message;
            }

            UpdateHud();
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            ExpeditionSeedProfile seedProfile,
            CombatWorld world,
            GameObject allyPrefab,
            GameObject enemyPrefab,
            Transform[] allySpawns,
            Transform enemyAnchor,
            Button toggleButton,
            TMP_Text mode,
            TMP_Text stage,
            TMP_Text wave,
            TMP_Text count,
            TMP_Text timer,
            TMP_Text result)
        {
            profile = seedProfile;
            combatWorld = world;
            playerUnitPrefab = allyPrefab;
            enemyUnitPrefab = enemyPrefab;
            playerSpawnPoints = allySpawns;
            enemySpawnAnchor = enemyAnchor;
            modeButton = toggleButton;
            modeText = mode;
            stageText = stage;
            waveText = wave;
            countText = count;
            timerText = timer;
            resultText = result;
        }
#endif
    }
}
