using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectMT.Features.WorldDrops;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.Reward;
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
        [SerializeField] private EnemyStageAppearanceSet enemyAppearanceSet; // 단계별 모듈러 적 원본
        [SerializeField, HideInInspector] private GameObject enemyUnitPrefab; // 기존 테스트용 단일 적 Fallback
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
        [SerializeField] private RectTransform progressFill;
        [SerializeField, Min(1f)] private float progressFillMaxWidth = 360f;

        private IGameProgressService progress; // 진행 조회·저장 계약
        private IRewardPresentationPlayer rewardPresentation; // 저장 확정 보상 표현
        private WorldItemDropRuntime worldItemDrops; // 원정대 전용 표시 풀·획득 버퍼
        private BattlePartySnapshot party; // 다음 Run에 사용할 최신 부대 사진
        private BattlePartySnapshot activeRunParty; // 현재 Run 시작 때 고정한 부대 사진
        private ExpeditionRunMode currentMode; // 도전·반복 상태
        private int currentStage; // 현재 실행 단계
        private int currentWave; // 현재 표시 웨이브
        private float waveElapsed; // 2웨이브 대기 시간
        private float challengeTimeRemaining; // 도전 남은 시간
        private int nextWaveToSpawn; // 다음 데이터 웨이브 번호
        private int waveCount; // 현재 단계의 전체 웨이브 수
        private bool allWavesSpawned; // 모든 데이터 웨이브 출현 완료
        private bool running; // 전투 Tick 허용
        private bool settling; // 결과 저장 중
        private int operationVersion; // 늦은 비동기 결과 무효화
        private int runSequence; // 공간 제어용 Run 변경 번호
        private Vector3 formationOrigin; // 맵 중심 기준 배치 원점
        private bool formationFrameConfigured;
        private bool formationPlacementActive;
        private readonly Dictionary<UnitActor, int> enemyWaveByActor = new Dictionary<UnitActor, int>(); // 적별 소속 웨이브
        private readonly Dictionary<UnitActor, int> playerSlotByActor = new Dictionary<UnitActor, int>(); // 아군별 본부대 자리
        private int[] aliveEnemiesByWave = new int[ExpeditionStageRules.LegacyWaveCount + 1]; // 웨이브별 생존 적
        private bool[] climaxPlayedByWave = new bool[ExpeditionStageRules.LegacyWaveCount + 1]; // 웨이브당 한 번만 재생
        private readonly List<WorldItemDropRequest> worldDropBuffer = new List<WorldItemDropRequest>(4); // 사망 1회 드랍 재사용 버퍼
        private int nextReserveIndex; // 다음에 투입할 예비 순서
        private int runEnemyTotalCount; // 현재 Run 전체 적 수
        private int defeatedEnemyCount; // 현재 Run 처치 적 수
        private bool hudCacheValid; // HUD 중복 할당 방지
        private ExpeditionRunMode displayedMode;
        private int displayedStage;
        private int displayedWave;
        private int displayedWaveCount;
        private int displayedAllyCount;
        private int displayedEnemyCount;
        private int displayedTimerSeconds;
        private int displayedDefeatedEnemyCount;
        private int displayedRunEnemyTotalCount;
        private bool displayedModeInteractable;

        public bool IsRunning => running;
        public bool IsSettling => settling;
        public int RunSequence => runSequence;
        public bool IsFormationPlacementActive => formationPlacementActive;

        public void SetPartyForNextRun(BattlePartySnapshot partySnapshot)
        {
            if (partySnapshot == null || partySnapshot.Units.Length == 0)
            {
                throw new ArgumentException("A non-empty party is required.", nameof(partySnapshot));
            }

            party = partySnapshot; // 현재 소환 유닛은 유지하고 다음 StartRun부터 사용
        }

        public void CollectActiveUnits(List<UnitActor> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            foreach (var pair in playerSlotByActor)
            {
                if (pair.Key != null && pair.Key.IsAlive)
                {
                    destination.Add(pair.Key);
                }
            }

            foreach (var pair in enemyWaveByActor)
            {
                if (pair.Key != null && pair.Key.IsAlive)
                {
                    destination.Add(pair.Key);
                }
            }
        }

        public bool TryGetPlayerSlot(UnitActor actor, out int slotIndex)
        {
            if (actor != null && playerSlotByActor.TryGetValue(actor, out slotIndex))
            {
                return true;
            }

            slotIndex = -1;
            return false;
        }

        public void Initialize(
            IGameProgressService progressService,
            BattlePartySnapshot partySnapshot,
            IRewardPresentationPlayer rewardPlayer = null,
            Collider formationGround = null,
            ItemCatalog itemCatalog = null,
            Transform worldDropPickupTarget = null)
        {
            Shutdown();
            InvalidateHudCache();
            progress = progressService ?? throw new ArgumentNullException(nameof(progressService));
            party = partySnapshot ?? throw new ArgumentNullException(nameof(partySnapshot));
            rewardPresentation = rewardPlayer;
            ConfigureFormationFrame(formationGround);
            ConfigureWorldItemDrops(itemCatalog, worldDropPickupTarget);
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
            formationPlacementActive = false;
            ResetWaveTracking();
            ResetPlayerTracking();
            worldItemDrops?.CollectAllActive(); // 무정산 종료도 남은 드랍을 전부 획득 확정
            _ = FlushWorldDropsCheckpointAsync(); // 전체 획득분은 콘텐츠 전환 전 출구 체크포인트 저장
            combatWorld?.Clear();
            UpdateHud();
        }

        public bool BeginFormationPlacement()
        {
            if (progress == null || party == null || combatWorld == null || settling)
            {
                return false;
            }

            StopWithoutResult();
            activeRunParty = party;
            formationPlacementActive = true;
            combatWorld.SetPaused(true);
            SpawnParty(true);
            return playerSlotByActor.Count > 0;
        }

        public void EndFormationPlacement()
        {
            StopWithoutResult();
        }

        public void Shutdown()
        {
            operationVersion++;
            running = false;
            settling = false;
            formationPlacementActive = false;
            if (modeButton != null)
            {
                modeButton.onClick.RemoveListener(ToggleMode);
            }

            ResetWaveTracking();
            ResetPlayerTracking();
            worldItemDrops?.CollectAllActive(); // 씬 종료 전에 남은 드랍을 전부 획득 확정
            _ = FlushWorldDropsCheckpointAsync(); // 씬 종료 뒤에도 시작한 저장 Task가 획득분을 확정
            worldItemDrops?.Initialize(null, null, null, null);
            combatWorld?.Clear();
            progress = null;
            rewardPresentation = null;
            party = null;
            activeRunParty = null;
            worldItemDrops = null;
            formationFrameConfigured = false;
            InvalidateHudCache();
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

            if (!allWavesSpawned && nextWaveToSpawn <= waveCount &&
                (waveElapsed >= profile.GetWaveSpawnDelay(currentStage, nextWaveToSpawn) ||
                 combatWorld.CountAlive(UnitTeam.Enemy) == 0))
            {
                SpawnWave(nextWaveToSpawn); // 데이터 간격 또는 전멸 시 다음 웨이브
                currentWave = nextWaveToSpawn;
                nextWaveToSpawn++;
                waveElapsed = 0f;
                allWavesSpawned = nextWaveToSpawn > waveCount;
            }

            if (combatWorld.CountAlive(UnitTeam.Player) == 0 ||
                (currentMode == ExpeditionRunMode.Challenge && challengeTimeRemaining <= 0f))
            {
                FinishDefeat();
                return;
            }

            if (allWavesSpawned && combatWorld.CountAlive(UnitTeam.Enemy) == 0)
            {
                FinishVictory();
                return;
            }

            UpdateHud();
        }

        private void StartRun()
        {
            operationVersion++; // 이전 Run 콜백 무효화
            runSequence++;
            ResetWaveTracking();
            ResetPlayerTracking();
            worldItemDrops?.CollectAllActive(); // Run 교체 전 남은 드랍 누락 방지
            activeRunParty = party; // 진행 중 편성 변경은 다음 Run부터 반영
            combatWorld.Clear();
            combatWorld.SetPaused(false);
            formationPlacementActive = false;
            running = true;
            settling = false;
            currentWave = 1;
            waveElapsed = 0f;
            challengeTimeRemaining = profile.ChallengeTimeLimitSeconds;
            waveCount = Mathf.Max(1, profile.GetWaveCount(currentStage));
            nextWaveToSpawn = 2;
            allWavesSpawned = waveCount <= 1;
            aliveEnemiesByWave = new int[waveCount + 1];
            climaxPlayedByWave = new bool[waveCount + 1];
            nextReserveIndex = 0;
            runEnemyTotalCount = profile.GetTotalEnemies(currentStage);
            defeatedEnemyCount = 0;
            InvalidateHudCache();
            if (resultText != null)
            {
                resultText.text = string.Empty;
            }

            SpawnParty(false);
            SpawnWave(1);
            UpdateHud();
        }

        private void SpawnParty(bool placementMode)
        {
            var units = activeRunParty.Units;
            for (var i = 0; i < units.Length && i < 5; i++) // 시드 본부대 최대 5기
            {
                var position = ResolvePlayerSpawnPosition(i);
                var request = new UnitSpawnRequest(
                    units[i].UnitId,
                    units[i].Stats,
                    UnitTeam.Player,
                    canMove: !placementMode,
                    canAttack: !placementMode,
                    visualTint: units[i].VisualTint,
                    runtimeAssetSet: units[i].RuntimeAssetSet);
                TrackPlayerUnit(combatWorld.SpawnUnit(playerUnitPrefab, request, position, Quaternion.identity), i);
            }
        }

        private Vector3 ResolvePlayerSpawnPosition(int slotIndex)
        {
            if (formationFrameConfigured && progress != null &&
                progress.View.MainBattleFormation.TryGetSlotOffset(slotIndex, out var offset))
            {
                var spawnY = playerSpawnPoints != null && slotIndex < playerSpawnPoints.Length &&
                             playerSpawnPoints[slotIndex] != null
                    ? playerSpawnPoints[slotIndex].position.y
                    : transform.position.y;
                return new Vector3(formationOrigin.x + offset.x, spawnY, formationOrigin.z + offset.y);
            }

            return playerSpawnPoints != null && slotIndex < playerSpawnPoints.Length &&
                   playerSpawnPoints[slotIndex] != null
                ? playerSpawnPoints[slotIndex].position
                : transform.position + new Vector3(slotIndex * 0.8f, 0f, 0f);
        }

        private void ConfigureFormationFrame(Collider formationGround)
        {
            formationFrameConfigured = false;
            if (formationGround == null)
            {
                return;
            }

            var bounds = formationGround.bounds;
            formationOrigin = new Vector3(bounds.center.x, 0f, bounds.center.z);
            formationFrameConfigured = true;
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
                    visualTint: reserve.VisualTint,
                    runtimeAssetSet: reserve.RuntimeAssetSet);
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
            var count = profile.GetEnemyCount(currentStage, wave);
            var anchor = enemySpawnAnchor == null ? transform.position + new Vector3(4f, 0f, 4f) : enemySpawnAnchor.position;
            var formationRight = enemySpawnAnchor == null ? Vector3.right : enemySpawnAnchor.right;
            var formationForward = enemySpawnAnchor == null ? Vector3.forward : enemySpawnAnchor.forward;
            for (var i = 0; i < count; i++)
            {
                var formationOffset = ExpeditionStageRules.GetFormationOffset(i, count); // 실제 인원 기준 중앙 정렬
                var position = anchor +
                               formationRight * formationOffset.x +
                               formationForward * (formationOffset.y + profile.GetWaveForwardOffset(currentStage, wave));
                var unitIndex = i + wave * 10;
                var ranged = profile.IsRangedSlot(currentStage, unitIndex);
                var enemyPrefab = enemyAppearanceSet == null
                    ? enemyUnitPrefab
                    : enemyAppearanceSet.ResolvePrefab(profile.ResolveAppearance(currentStage, ranged));
                var stats = profile.CreateEnemyStats(currentStage, ranged);
                var request = new UnitSpawnRequest(
                    $"enemy_{currentStage}_{wave}_{i}",
                    stats,
                    UnitTeam.Enemy,
                    appearanceSeed: CreateEnemyAppearanceSeed(currentStage, wave, i, operationVersion));
                var actor = combatWorld.SpawnUnit(enemyPrefab, request, position, Quaternion.Euler(0f, 180f, 0f));
                TrackWaveEnemy(actor, wave);
            }
        }

        private static int CreateEnemyAppearanceSeed(int stage, int wave, int index, int runVersion)
        {
            unchecked
            {
                var seed = 2166136261u;
                seed = (seed ^ (uint)stage) * 16777619u;
                seed = (seed ^ (uint)wave) * 16777619u;
                seed = (seed ^ (uint)index) * 16777619u;
                seed = (seed ^ (uint)runVersion) * 16777619u;
                seed ^= seed >> 16;
                seed *= 0x7FEB352Du;
                seed ^= seed >> 15;
                seed *= 0x846CA68Bu;
                seed ^= seed >> 16; // 인접 슬롯 시드 동조 방지
                var positiveSeed = (int)(seed & int.MaxValue);
                return positiveSeed == 0 ? 1 : positiveSeed;
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
            defeatedEnemyCount = Mathf.Min(runEnemyTotalCount, defeatedEnemyCount + 1);
            if (running && profile != null &&
                profile.CreateEnemyWorldDrops(currentStage, wave, actor.transform.position, worldDropBuffer) > 0)
            {
                for (var index = 0; index < worldDropBuffer.Count; index++)
                {
                    worldItemDrops?.TrySpawn(worldDropBuffer[index]); // 보상 원본과 분리된 표시 요청
                }
            }

            if (!running || aliveEnemiesByWave[wave] != 0 || climaxPlayedByWave[wave])
            {
                return;
            }

            climaxPlayedByWave[wave] = true;
            combatWorld?.PlayClimax(actor.transform.position, CombatClimaxStrength.Weak);
            _ = FlushWorldDropsCheckpointAsync(); // 이미 흡수한 항목만 웨이브 체크포인트 저장
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
            runEnemyTotalCount = 0;
            defeatedEnemyCount = 0;
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
            worldItemDrops?.CollectAllActive(); // 모드 변경 전 남은 드랍을 전부 획득 확정
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
            worldItemDrops?.CollectAllActive(); // 전투 종료 시 남은 표현도 획득으로 확정
            combatWorld.SetPaused(true); // 결과 연출 동안 전투 정지
            SetResult(currentMode == ExpeditionRunMode.Challenge ? "승리 정산 중..." : string.Empty);
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
            var settledStage = currentStage;
            RewardBundle rewards;
            GameProgressChange change;
            switch (settledMode)
            {
                case ExpeditionRunMode.Challenge:
                    rewards = ExpeditionFirstClearRewardRules.Create(settledStage);
                    change = GameProgressChange.RecordExpeditionFirstClear(settledStage, rewards);
                    break;
                case ExpeditionRunMode.Repeat:
                    rewards = ExpeditionRepeatClearRewardRules.Create(settledStage);
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
                try
                {
                    rewardPresentation?.PlayConfirmed(RewardPresentationRequest.FromBundle(rewards));
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception); // 표현 실패는 저장을 되돌리지 않음
                }

                if (settledMode == ExpeditionRunMode.Challenge)
                {
                    SetResult($"원정대 {settledStage} 승리 · 골드 +{rewards.Gold:N0}");
                }
            }
            else
            {
                SetResult("보상 저장 실패");
            }

            if (settledMode == ExpeditionRunMode.Challenge || !saved)
            {
                await Task.Delay(TimeSpan.FromSeconds(profile.ResultDelaySeconds));
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
            worldItemDrops?.CollectAllActive(); // 패배도 남은 드랍을 전부 획득 확정
            combatWorld.SetPaused(true);
            SetResult("패배");
            _ = ResolveDefeatAsync(++operationVersion); // 실패 단계에서 반복 전환
        }

        private async Task ResolveDefeatAsync(int version)
        {
            await FlushWorldDropsCheckpointAsync(); // 패배 전 남은 드랍까지 전부 저장
            if (this == null || version != operationVersion)
            {
                return;
            }

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

        private void ConfigureWorldItemDrops(ItemCatalog itemCatalog, Transform pickupTarget)
        {
            var visualCatalog = profile == null ? null : profile.WorldItemDropVisualCatalog;
            if (itemCatalog == null || visualCatalog == null || pickupTarget == null)
            {
                worldItemDrops = null;
                return;
            }

            worldItemDrops = GetComponentInChildren<WorldItemDropRuntime>(true);
            if (worldItemDrops == null)
            {
                worldItemDrops = WorldItemDropRuntime.Create(
                    transform,
                    progress,
                    itemCatalog,
                    visualCatalog,
                    pickupTarget,
                    Camera.main);
                return;
            }

            worldItemDrops.Initialize(progress, itemCatalog, visualCatalog, pickupTarget, Camera.main);
        }

        private async Task FlushWorldDropsCheckpointAsync()
        {
            if (worldItemDrops == null || worldItemDrops.PendingItemTypeCount == 0)
            {
                return;
            }

            var saved = await worldItemDrops.FlushAsync();
            if (!saved && this != null)
            {
                Debug.LogWarning("월드 드랍 획득분 저장을 다음 체크포인트에서 다시 시도합니다.");
            }
        }

        private void UpdateHud()
        {
            var modeChanged = !hudCacheValid || displayedMode != currentMode;
            if (modeText != null && modeChanged)
            {
                modeText.text = currentMode == ExpeditionRunMode.Challenge ? "도전" : "반복";
            }

            if (stageText != null && (!hudCacheValid || displayedStage != currentStage))
            {
                stageText.text = $"원정대 {currentStage}";
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
                (currentMode == ExpeditionRunMode.Repeat || progress == null || progress.View.LastClearedStage > 0);
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
            TMP_Text result,
            RectTransform progress = null,
            float progressWidth = 360f)
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
            progressFill = progress;
            progressFillMaxWidth = Mathf.Max(1f, progressWidth);
            InvalidateHudCache();
        }
#endif
    }
}
