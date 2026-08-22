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
    [DisallowMultipleComponent]
    public sealed class ExpeditionController : MonoBehaviour // 원정대 Run·Wave·정산 관리
    {
        [Header("Runtime")]
        [SerializeField] private ExpeditionSeedProfile profile; // 시드 밸런스 원본
        [SerializeField] private CombatWorld combatWorld; // 공용 전투 영역
        [SerializeField] private GameObject playerUnitPrefab; // 아군 원본
        [SerializeField] private EnemyStageAppearanceSet enemyAppearanceSet; // 단계별 모듈러 적 원본
        [SerializeField, HideInInspector] private GameObject enemyUnitPrefab; // 기존 테스트용 단일 적 Fallback
        [SerializeField] private Transform playerFormationAnchor; // 아군 진형 전체 기준점
        [SerializeField] private Transform[] playerSpawnPoints; // 아군 기본 5자리
        [SerializeField] private Transform enemySpawnAnchor; // 적 도착 진형 기준점
        [SerializeField] private Transform[] enemyEntryPoints; // 적 실제 등장 위치 3곳
        [SerializeField] private MainBattleAIProfileCatalog mainBattleAIProfiles; // 첫 편성 역할 AI

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
        [SerializeField] private ExpeditionBossHudPresenter bossHud;
        [SerializeField] private ExpeditionBossIntroPresenter bossIntro;
        [SerializeField] private TMP_Text reinforcementWarningText;

        private IGameProgressService progress; // 진행 조회·저장 계약
        private IRewardPresentationPlayer rewardPresentation; // 저장 확정 보상 표현
        private ItemCatalog itemCatalog; // 결과 안내의 아이템 이름 조회
        private WorldItemDropRuntime worldItemDrops; // 원정대 전용 표시 풀·획득 버퍼
        private EquipmentWorldDropRuntime equipmentWorldDrops; // 고유 장비 상자·저장 버퍼
        private EquipmentBalanceConfig equipmentBalanceConfig; // 장비 등급·옵션 원본
        private System.Random equipmentRandom; // 한 Run 안에서 연속 장비 판정 공유
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
        private Coroutine bossIntroRoutine; // 보스 등장 전 전투 지연
        private int operationVersion; // 늦은 비동기 결과 무효화
        private int runSequence; // 공간 제어용 Run 변경 번호
        private Vector3 formationOrigin; // 아군 기준 오브젝트 기반 배치 원점
        private bool formationFrameConfigured;
        private bool formationPlacementActive;
        private readonly Dictionary<UnitActor, int> enemyWaveByActor = new Dictionary<UnitActor, int>(); // 적별 소속 웨이브
        private readonly Dictionary<UnitActor, int> playerSlotByActor = new Dictionary<UnitActor, int>(); // 아군별 본부대 자리
        private readonly List<EnemyArrivalUnit> arrivingEnemies = new List<EnemyArrivalUnit>(8);
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
        private bool waveArrivalActive;
        private bool firstWaveReady;
        private int arrivalWave;
        private int arrivalTotalCount;
        private int arrivalNextSpawnIndex;
        private float arrivalSpawnTimer;
        private bool reinforcementWarningActive;
        private int reinforcementWarningWave;
        private float reinforcementWarningRemaining;
        private float reinforcementNoticeRemaining;
        private bool ownsRuntimeReinforcementWarning;

        public bool IsRunning => running;
        public bool IsSettling => settling;
        public int RunSequence => runSequence;
        public bool IsFormationPlacementActive => formationPlacementActive;
        public bool IsWaveArrivalActive => waveArrivalActive;
        public bool IsReinforcementWarningActive => reinforcementWarningActive;

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
                if (pair.Key != null && pair.Key.IsAlive && pair.Key.IsCombatReady)
                {
                    destination.Add(pair.Key);
                }
            }

            foreach (var pair in enemyWaveByActor)
            {
                if (pair.Key != null && pair.Key.IsAlive && pair.Key.IsCombatReady)
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
            Transform worldDropPickupTarget = null,
            EquipmentBalanceConfig equipmentBalance = null,
            Transform formationAnchor = null)
        {
            Shutdown();
            InvalidateHudCache();
            progress = progressService ?? throw new ArgumentNullException(nameof(progressService));
            party = partySnapshot ?? throw new ArgumentNullException(nameof(partySnapshot));
            rewardPresentation = rewardPlayer;
            this.itemCatalog = itemCatalog;
            bossIntro ??= FindFirstObjectByType<ExpeditionBossIntroPresenter>(FindObjectsInactive.Include);
            mainBattleAIProfiles ??= MainBattleAIProfileCatalog.LoadDefault();
            EnsureReinforcementWarningText();
            equipmentBalanceConfig = equipmentBalance ?? EquipmentBalanceConfig.RuntimeDefault;
            equipmentRandom = new System.Random();
            playerFormationAnchor = formationAnchor ?? playerFormationAnchor;
            ConfigureFormationFrame(formationGround);
            ConfigureWorldItemDrops(itemCatalog, worldDropPickupTarget);
            ConfigureEquipmentWorldDrops(worldDropPickupTarget);
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
            StopBossIntro();
            bossHud?.Hide();
            ResetWaveTracking();
            ResetPlayerTracking();
            CollectAllWorldDrops(); // 무정산 종료도 남은 드랍을 전부 획득 확정
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
            StopBossIntro();
            bossHud?.Hide();
            if (modeButton != null)
            {
                modeButton.onClick.RemoveListener(ToggleMode);
            }

            ResetWaveTracking();
            ResetPlayerTracking();
            CollectAllWorldDrops(); // 씬 종료 전에 남은 드랍을 전부 획득 확정
            _ = FlushWorldDropsCheckpointAsync(); // 씬 종료 뒤에도 시작한 저장 Task가 획득분을 확정
            worldItemDrops?.Initialize(null, null, null, null);
            equipmentWorldDrops?.Initialize(null, null, null, null);
            combatWorld?.Clear();
            progress = null;
            rewardPresentation = null;
            itemCatalog = null;
            equipmentBalanceConfig = null;
            equipmentRandom = null;
            party = null;
            activeRunParty = null;
            worldItemDrops = null;
            equipmentWorldDrops = null;
            formationFrameConfigured = false;
            ReleaseRuntimeReinforcementWarningText();
            InvalidateHudCache();
        }

        private void Update()
        {
            if (!running || combatWorld == null)
            {
                return;
            }

            var deltaTime = Time.deltaTime;
            TickReinforcementNotice(deltaTime);
            TickReinforcementWarning(deltaTime);
            TickWaveArrival(deltaTime);
            if (!firstWaveReady)
            {
                UpdateHud();
                return; // 첫 웨이브 Ready 전에는 전투 시간도 시작하지 않음
            }

            waveElapsed += deltaTime;
            if (currentMode == ExpeditionRunMode.Challenge)
            {
                challengeTimeRemaining = Mathf.Max(0f, challengeTimeRemaining - deltaTime);
            }

            if (!allWavesSpawned && !waveArrivalActive && !reinforcementWarningActive &&
                nextWaveToSpawn <= waveCount)
            {
                TryScheduleNextWave();
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
            CollectAllWorldDrops(); // Run 교체 전 남은 드랍 누락 방지
            activeRunParty = party; // 진행 중 편성 변경은 다음 Run부터 반영
            combatWorld.Clear();
            combatWorld.SetPaused(true);
            formationPlacementActive = false;
            bossHud?.Hide();
            StopBossIntro();
            running = false;
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
            if (profile.IsBossStage(currentStage) && bossIntro != null)
            {
                bossIntroRoutine = StartCoroutine(PlayBossIntroThenBegin(operationVersion));
            }
            else
            {
                BeginCombatRun();
            }

            UpdateHud();
        }

        private IEnumerator PlayBossIntroThenBegin(int version)
        {
            yield return bossIntro.Play(currentStage);
            bossIntroRoutine = null;
            if (this == null || version != operationVersion || settling || formationPlacementActive)
            {
                yield break;
            }

            BeginCombatRun();
        }

        private void BeginCombatRun()
        {
            combatWorld.SetPaused(false);
            running = true;
            StartWaveArrival(1); // 첫 웨이브는 행군 완료 뒤 동시에 전투 시작
        }

        private void StopBossIntro()
        {
            if (bossIntroRoutine != null)
            {
                StopCoroutine(bossIntroRoutine);
                bossIntroRoutine = null;
            }

            bossIntro?.Hide();
        }

        private void SpawnParty(bool placementMode)
        {
            var units = activeRunParty.Units;
            for (var i = 0; i < units.Length && i < 5; i++) // 시드 본부대 최대 5기
            {
                var formationOffset = ResolvePlayerFormationOffset(i);
                var position = ResolvePlayerSpawnPosition(i, formationOffset);
                var formationStats = MainBattleFormationBuffRules.ApplyStats(units[i].Stats, formationOffset);
                var request = new UnitSpawnRequest(
                    units[i].UnitId,
                    formationStats,
                    UnitTeam.Player,
                    canMove: !placementMode,
                    canAttack: !placementMode,
                    visualTint: units[i].VisualTint,
                    runtimeAssetSet: units[i].RuntimeAssetSet,
                    supportOutputMultiplier: MainBattleFormationBuffRules.GetSupportOutputMultiplier(formationOffset));
                var actor = combatWorld.SpawnUnit(playerUnitPrefab, request, position, Quaternion.identity);
                ApplyPlayerAIProfile(actor, units[i].UnitId);
                TrackPlayerUnit(actor, i);
            }
        }

        private Vector2 ResolvePlayerFormationOffset(int slotIndex)
        {
            if (progress != null &&
                progress.View.MainBattleFormation.TryGetSlotOffset(slotIndex, out var savedOffset))
            {
                return MainBattleFormationRules.IsHexPosition(savedOffset)
                    ? savedOffset
                    : MainBattleFormationRules.SnapToHex(savedOffset);
            }

            return MainBattleFormationRules.GetDefaultOffset(slotIndex);
        }

        private Vector3 ResolvePlayerSpawnPosition(int slotIndex, Vector2 formationOffset)
        {
            if (formationFrameConfigured)
            {
                var spawnY = ResolvePlayerSpawnHeight(slotIndex);
                return new Vector3(
                    formationOrigin.x + formationOffset.x,
                    spawnY,
                    formationOrigin.z + formationOffset.y);
            }

            if (playerSpawnPoints != null && slotIndex >= 0 && slotIndex < playerSpawnPoints.Length &&
                playerSpawnPoints[slotIndex] != null)
            {
                return playerSpawnPoints[slotIndex].position;
            }

            var fallbackOrigin = playerFormationAnchor == null ? transform.position : playerFormationAnchor.position;
            return new Vector3(
                fallbackOrigin.x + formationOffset.x,
                fallbackOrigin.y,
                fallbackOrigin.z + formationOffset.y);
        }

        private float ResolvePlayerSpawnHeight(int slotIndex)
        {
            if (playerSpawnPoints != null && slotIndex >= 0 && slotIndex < playerSpawnPoints.Length &&
                playerSpawnPoints[slotIndex] != null)
            {
                return playerSpawnPoints[slotIndex].position.y;
            }

            return playerFormationAnchor == null ? transform.position.y : playerFormationAnchor.position.y;
        }

        private void ApplyPlayerAIProfile(UnitActor actor, string monsterId)
        {
            if (actor != null && mainBattleAIProfiles != null &&
                mainBattleAIProfiles.TryResolve(monsterId, out var profile))
            {
                actor.SetCombatBehavior(profile.CreateBehavior());
            }
        }

        private static void ApplyEnemyAIProfile(UnitActor actor, bool ranged)
        {
            if (actor == null)
            {
                return;
            }

            actor.SetCombatBehavior(CombatImpactTuning.ActiveConfig.CreateMainBattleEnemyBehavior(ranged));
        }

        private void ConfigureFormationFrame(Collider formationGround)
        {
            formationFrameConfigured = false;
            if (playerFormationAnchor != null)
            {
                var anchorPosition = playerFormationAnchor.position;
                formationOrigin = new Vector3(anchorPosition.x, 0f, anchorPosition.z);
                formationFrameConfigured = true;
                return;
            }

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

                ApplyPlayerAIProfile(actor, reserve.UnitId);
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

        private void TryScheduleNextWave()
        {
            var previousWave = Mathf.Clamp(nextWaveToSpawn - 1, 1, waveCount);
            var alive = previousWave < aliveEnemiesByWave.Length ? aliveEnemiesByWave[previousWave] : 0;
            if (alive <= 0)
            {
                StartWaveArrival(nextWaveToSpawn); // 전멸 시 대기 없이 증원 행군 시작
                return;
            }

            var initialCount = Mathf.Max(1, profile.GetEnemyCount(currentStage, previousWave));
            var aliveRatio = (float)alive / initialCount;
            var minimumDelay = Mathf.Max(
                profile.ReinforcementMinimumDelaySeconds,
                profile.GetWaveSpawnDelay(currentStage, nextWaveToSpawn));
            var warningLead = profile.ReinforcementWarningSeconds;
            var forceWarningTime = Mathf.Max(
                minimumDelay,
                profile.ReinforcementForceDelaySeconds - warningLead);
            var weakened = waveElapsed >= minimumDelay && aliveRatio <= profile.ReinforcementAliveRatio;
            var forced = waveElapsed >= forceWarningTime;
            if (weakened || forced)
            {
                BeginReinforcementWarning(nextWaveToSpawn);
            }
        }

        private void BeginReinforcementWarning(int wave)
        {
            reinforcementWarningActive = true;
            reinforcementWarningWave = wave;
            reinforcementWarningRemaining = profile.ReinforcementWarningSeconds;
            reinforcementNoticeRemaining = Mathf.Max(0.35f, reinforcementWarningRemaining + 0.15f);
            ShowReinforcementWarning(true);
            if (reinforcementWarningRemaining <= 0f)
            {
                reinforcementWarningActive = false;
                StartWaveArrival(wave);
            }
        }

        private void TickReinforcementWarning(float deltaTime)
        {
            if (!reinforcementWarningActive)
            {
                return;
            }

            reinforcementWarningRemaining = Mathf.Max(0f, reinforcementWarningRemaining - deltaTime);
            if (reinforcementWarningRemaining > 0f)
            {
                return;
            }

            var wave = reinforcementWarningWave;
            reinforcementWarningActive = false;
            reinforcementWarningWave = 0;
            StartWaveArrival(wave);
        }

        private void TickReinforcementNotice(float deltaTime)
        {
            if (reinforcementNoticeRemaining <= 0f)
            {
                return;
            }

            reinforcementNoticeRemaining = Mathf.Max(0f, reinforcementNoticeRemaining - deltaTime);
            if (reinforcementNoticeRemaining <= 0f)
            {
                ShowReinforcementWarning(false);
            }
        }

        private void StartWaveArrival(int wave)
        {
            if (profile == null || combatWorld == null || wave <= 0 || wave > waveCount)
            {
                return;
            }

            waveArrivalActive = true;
            arrivalWave = wave;
            arrivalTotalCount = Mathf.Max(0, profile.GetEnemyCount(currentStage, wave));
            arrivalNextSpawnIndex = 0;
            arrivalSpawnTimer = 0f;
            arrivingEnemies.Clear();
            currentWave = wave;
            nextWaveToSpawn = wave + 1;
            allWavesSpawned = nextWaveToSpawn > waveCount;
            waveElapsed = 0f;

            if (wave > 1)
            {
                reinforcementNoticeRemaining = Mathf.Max(
                    reinforcementNoticeRemaining,
                    Mathf.Max(0.35f, profile.ReinforcementWarningSeconds));
                ShowReinforcementWarning(true);
                ResolveEnemyFormationAxes(out _, out var formationForward);
                var cuePosition = ResolveEnemyEntryCuePosition(formationForward);
                combatWorld.PlayClimax(cuePosition, CombatClimaxStrength.Weak); // 기존 VFX/SFX로 증원 방향 강조
            }

            if (arrivalTotalCount <= 0)
            {
                CompleteWaveArrival();
                return;
            }

            SpawnNextArrivalEnemy(); // 첫 기는 경고 종료와 동시에 보이게 함
            arrivalSpawnTimer = profile.EnemySpawnIntervalSeconds;
        }

        private void TickWaveArrival(float deltaTime)
        {
            if (!waveArrivalActive)
            {
                return;
            }

            arrivalSpawnTimer -= Mathf.Max(0f, deltaTime);
            while (arrivalNextSpawnIndex < arrivalTotalCount && arrivalSpawnTimer <= 0f)
            {
                SpawnNextArrivalEnemy();
                arrivalSpawnTimer += profile.EnemySpawnIntervalSeconds;
                if (profile.EnemySpawnIntervalSeconds <= 0f)
                {
                    arrivalSpawnTimer = 0f;
                }
            }

            var allReached = arrivalNextSpawnIndex >= arrivalTotalCount;
            for (var index = 0; index < arrivingEnemies.Count; index++)
            {
                var arrival = arrivingEnemies[index];
                if (arrival.Actor == null || arrival.Reached)
                {
                    continue;
                }

                arrival.Elapsed += Mathf.Max(0f, deltaTime);
                var ratio = Mathf.Clamp01(arrival.Elapsed / arrival.Duration);
                var eased = ratio * ratio * (3f - 2f * ratio);
                arrival.Actor.transform.position = Vector3.Lerp(arrival.EntryPosition, arrival.ReadyPosition, eased);
                var direction = arrival.ReadyPosition - arrival.Actor.transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    arrival.Actor.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }

                if (ratio < 1f)
                {
                    arrival.Actor.AnimationDriver?.PlayMove();
                    allReached = false;
                    continue;
                }

                arrival.Reached = true;
                arrival.Actor.AnimationDriver?.PlayIdle(true);
            }

            if (allReached)
            {
                CompleteWaveArrival();
            }
        }

        private void SpawnNextArrivalEnemy()
        {
            if (arrivalNextSpawnIndex >= arrivalTotalCount)
            {
                return;
            }

            var index = arrivalNextSpawnIndex++;
            var readyPosition = ResolveEnemyFormationPosition(arrivalWave, index, arrivalTotalCount);
            var readyLaneOffset = ExpeditionStageRules.GetFormationOffset(index, arrivalTotalCount).x;
            ResolveEnemyFormationAxes(out var formationRight, out var formationForward);
            var entryPosition = ResolveEnemyEntryPosition(
                readyPosition,
                readyLaneOffset,
                formationRight,
                formationForward);
            var unitIndex = index + arrivalWave * 10;
            var ranged = profile.IsRangedSlot(currentStage, unitIndex);
            var enemyPrefab = enemyAppearanceSet == null
                ? enemyUnitPrefab
                : enemyAppearanceSet.ResolvePrefab(profile.ResolveAppearance(currentStage, ranged));
            var boss = profile.IsBossStage(currentStage);
            var request = new UnitSpawnRequest(
                boss ? $"boss_{currentStage}" : $"enemy_{currentStage}_{arrivalWave}_{index}",
                profile.CreateEnemyStats(currentStage, ranged),
                UnitTeam.Enemy,
                appearanceSeed: CreateEnemyAppearanceSeed(currentStage, arrivalWave, index, operationVersion),
                visualScaleMultiplier: boss ? profile.BossVisualScaleMultiplier : 1f,
                isBoss: boss);
            var direction = readyPosition - entryPosition;
            direction.y = 0f;
            var rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : Quaternion.identity;
            var actor = combatWorld.SpawnUnit(enemyPrefab, request, entryPosition, rotation);
            if (actor == null)
            {
                return;
            }

            ApplyEnemyAIProfile(actor, ranged);
            actor.SetCombatReady(false);
            actor.AnimationDriver?.PlayMove();
            TrackWaveEnemy(actor, arrivalWave);
            arrivingEnemies.Add(new EnemyArrivalUnit(
                actor,
                entryPosition,
                readyPosition,
                profile.EnemyMarchDurationSeconds,
                boss));
        }

        private Vector3 ResolveEnemyEntryPosition(
            Vector3 readyPosition,
            float readyLaneOffset,
            Vector3 formationRight,
            Vector3 formationForward)
        {
            if (TryResolveEnemyEntryPoint(readyLaneOffset, formationRight, out var entryPosition))
            {
                return entryPosition;
            }

            return readyPosition + formationForward * profile.EnemyEntryDistance;
        }

        private Vector3 ResolveEnemyEntryCuePosition(Vector3 formationForward)
        {
            var total = Vector3.zero;
            var count = 0;
            if (enemyEntryPoints != null)
            {
                for (var index = 0; index < enemyEntryPoints.Length; index++)
                {
                    if (enemyEntryPoints[index] == null)
                    {
                        continue;
                    }

                    total += enemyEntryPoints[index].position;
                    count++;
                }
            }

            if (count > 0)
            {
                return total / count;
            }

            return enemySpawnAnchor == null
                ? transform.position
                : enemySpawnAnchor.position + formationForward * profile.EnemyEntryDistance;
        }

        private bool TryResolveEnemyEntryPoint(
            float readyLaneOffset,
            Vector3 formationRight,
            out Vector3 position)
        {
            var anchorPosition = enemySpawnAnchor == null ? transform.position : enemySpawnAnchor.position;
            var sideThreshold = ExpeditionStageRules.FormationSpacing * 0.75f;
            var bestScore = float.PositiveInfinity;
            position = default;
            var found = false;
            if (enemyEntryPoints != null)
            {
                for (var index = 0; index < enemyEntryPoints.Length; index++)
                {
                    var entryPoint = enemyEntryPoints[index];
                    if (entryPoint == null)
                    {
                        continue;
                    }

                    var entryLane = Vector3.Dot(entryPoint.position - anchorPosition, formationRight);
                    var score = readyLaneOffset < -sideThreshold
                        ? entryLane
                        : readyLaneOffset > sideThreshold
                            ? -entryLane
                            : Mathf.Abs(entryLane);
                    if (score >= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    position = entryPoint.position;
                    found = true;
                }
            }

            return found; // 좌·중·우 Ready 열과 같은 입장선을 골라 행군선 교차 방지
        }

        private Vector3 ResolveEnemyFormationPosition(int wave, int index, int count)
        {
            var anchor = enemySpawnAnchor == null
                ? transform.position + new Vector3(4f, 0f, 4f)
                : enemySpawnAnchor.position;
            ResolveEnemyFormationAxes(out var formationRight, out var formationForward);
            var tuning = CombatImpactTuning.ActiveConfig;
            var spawnSpread = profile.EnemyFormationSpread *
                              (tuning == null ? 1f : tuning.MainBattleEnemySpawnSpreadMultiplier);
            var formationOffset = ExpeditionStageRules.GetFormationOffset(index, count) * spawnSpread;
            return anchor +
                   formationRight * formationOffset.x +
                   formationForward * (formationOffset.y + profile.GetWaveForwardOffset(currentStage, wave));
        }

        private void ResolveEnemyFormationAxes(out Vector3 formationRight, out Vector3 formationForward)
        {
            var fallbackForward = enemySpawnAnchor == null ? Vector3.forward : enemySpawnAnchor.forward;
            var anchorPosition = enemySpawnAnchor == null
                ? transform.position + new Vector3(4f, 0f, 4f)
                : enemySpawnAnchor.position;
            formationForward = ExpeditionStageRules.ResolveBattleForward(
                formationFrameConfigured ? formationOrigin : transform.position,
                anchorPosition,
                fallbackForward);
            formationRight = Vector3.Cross(Vector3.up, formationForward).normalized;
        }

        private void CompleteWaveArrival()
        {
            for (var index = 0; index < arrivingEnemies.Count; index++)
            {
                var arrival = arrivingEnemies[index];
                if (arrival.Actor == null || !arrival.Actor.IsAlive)
                {
                    continue;
                }

                arrival.Actor.transform.position = arrival.ReadyPosition;
                arrival.Actor.SetCombatReady(true);
                arrival.Actor.AnimationDriver?.PlayIdle(true);
                if (arrival.IsBoss)
                {
                    bossHud?.Show(arrival.Actor, currentStage); // Ready 시점부터 보스 HUD 표시
                }
            }

            arrivingEnemies.Clear();
            waveArrivalActive = false;
            firstWaveReady = true;
            arrivalWave = 0;
            arrivalTotalCount = 0;
            arrivalNextSpawnIndex = 0;
            arrivalSpawnTimer = 0f;
            waveElapsed = 0f;
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
            _ = QuestRuntime.AdvanceAllOfConditionAsync(QuestConditionType.MonsterKill, 1L); // 처치 1마리당 퀘스트 진행
            if (running && profile != null &&
                profile.CreateEnemyWorldDrops(currentStage, wave, actor.transform.position, worldDropBuffer) > 0)
            {
                for (var index = 0; index < worldDropBuffer.Count; index++)
                {
                    worldItemDrops?.TrySpawn(worldDropBuffer[index]); // 보상 원본과 분리된 표시 요청
                }
            }

            TrySpawnNormalEnemyEquipment(actor.transform.position);

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
            ResetArrivalState();
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

        private void ResetArrivalState()
        {
            arrivingEnemies.Clear();
            waveArrivalActive = false;
            firstWaveReady = false;
            arrivalWave = 0;
            arrivalTotalCount = 0;
            arrivalNextSpawnIndex = 0;
            arrivalSpawnTimer = 0f;
            reinforcementWarningActive = false;
            reinforcementWarningWave = 0;
            reinforcementWarningRemaining = 0f;
            reinforcementNoticeRemaining = 0f;
            ShowReinforcementWarning(false);
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
                    SetResult(ExpeditionResultNoticeFormatter.ChallengeVictory(
                        settledStage,
                        RewardPresentationRequest.FromBundle(rewards, itemCatalog)));

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
            CollectAllWorldDrops(); // 패배도 남은 드랍을 전부 획득 확정
            combatWorld.SetPaused(true);
            SetResult(currentMode == ExpeditionRunMode.Challenge ? "도전 실패" : string.Empty);
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
                var lastClearedStage = progress.View.LastClearedStage;
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

                SetResult(ExpeditionResultNoticeFormatter.ChallengeDefeat(
                    lastClearedStage,
                    repeatModeSaved));
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

        private void ConfigureEquipmentWorldDrops(Transform pickupTarget)
        {
            var visualCatalog = profile == null ? null : profile.EquipmentDropChestVisualCatalog;
            if (visualCatalog == null || pickupTarget == null)
            {
                equipmentWorldDrops = null;
                return;
            }

            equipmentWorldDrops = GetComponentInChildren<EquipmentWorldDropRuntime>(true);
            if (equipmentWorldDrops == null)
            {
                equipmentWorldDrops = EquipmentWorldDropRuntime.Create(
                    transform,
                    progress,
                    visualCatalog,
                    pickupTarget,
                    Camera.main);
                return;
            }

            equipmentWorldDrops.Initialize(progress, visualCatalog, pickupTarget, Camera.main);
        }

        private void TrySpawnNormalEnemyEquipment(Vector3 position)
        {
            if (!running || profile == null || equipmentWorldDrops == null ||
                equipmentWorldDrops.AvailableCapacity <= 0 || equipmentBalanceConfig == null)
            {
                return;
            }

            equipmentRandom ??= new System.Random();
            if (!profile.ShouldDropNormalEnemyEquipment((float)equipmentRandom.NextDouble()))
            {
                return;
            }

            var instance = EquipmentDropRoller.RollSingle(equipmentBalanceConfig, equipmentRandom);
            equipmentWorldDrops.TrySpawn(new EquipmentWorldDropRequest(instance, position));
        }

        private void CollectAllWorldDrops()
        {
            worldItemDrops?.CollectAllActive();
            equipmentWorldDrops?.CollectAllActive();
        }

        private async Task FlushWorldDropsCheckpointAsync()
        {
            var itemDrops = worldItemDrops;
            var equipmentDrops = equipmentWorldDrops;
            if ((itemDrops == null || itemDrops.PendingItemTypeCount == 0) &&
                (equipmentDrops == null || equipmentDrops.PendingCount == 0))
            {
                return;
            }

            var itemFlush = itemDrops == null || itemDrops.PendingItemTypeCount == 0
                ? Task.FromResult(true)
                : itemDrops.FlushAsync();
            var equipmentFlush = equipmentDrops == null || equipmentDrops.PendingCount == 0
                ? Task.FromResult(true)
                : equipmentDrops.FlushAsync(); // Shutdown 참조 해제 전 두 저장 계약을 먼저 고정
            var itemSaved = await itemFlush;
            var equipmentSaved = await equipmentFlush;
            if ((!itemSaved || !equipmentSaved) && this != null)
            {
                Debug.LogWarning("월드 드랍 획득분 저장을 다음 체크포인트에서 다시 시도합니다.");
            }
        }

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

        private sealed class EnemyArrivalUnit
        {
            public EnemyArrivalUnit(
                UnitActor actor,
                Vector3 entryPosition,
                Vector3 readyPosition,
                float duration,
                bool isBoss)
            {
                Actor = actor;
                EntryPosition = entryPosition;
                ReadyPosition = readyPosition;
                Duration = Mathf.Max(0.1f, duration);
                IsBoss = isBoss;
            }

            public UnitActor Actor { get; }
            public Vector3 EntryPosition { get; }
            public Vector3 ReadyPosition { get; }
            public float Duration { get; }
            public bool IsBoss { get; }
            public float Elapsed { get; set; }
            public bool Reached { get; set; }
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
            float progressWidth = 360f,
            ExpeditionBossHudPresenter bossPresenter = null,
            ExpeditionBossIntroPresenter bossIntroPresenter = null)
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
            bossHud = bossPresenter;
            bossIntro = bossIntroPresenter;
            InvalidateHudCache();
        }
#endif
    }
}
