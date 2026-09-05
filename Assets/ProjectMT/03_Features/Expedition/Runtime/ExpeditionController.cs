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
    public sealed partial class ExpeditionController : MonoBehaviour // 원정대 Run·Wave·정산 관리
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
        private ExpeditionDifficulty currentDifficulty; // 일반·하드 상태
        private int currentStage; // 현재 실행 단계
        private int currentWave; // 현재 표시 웨이브
        private float waveElapsed; // 후속 웨이브 대기 시간
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
        private ExpeditionDifficulty displayedDifficulty;
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
        [SerializeField] private ExpeditionResultFlashPresenter resultFlash;

        public bool IsRunning => running;
        public bool IsSettling => settling;
        public int RunSequence => runSequence;
        public CombatWorld CombatWorld => combatWorld;
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
            resultFlash?.HideImmediate();
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
            currentDifficulty = view.Difficulty;
            if (currentMode == ExpeditionRunMode.Repeat && view.ActiveLastClearedStage <= 0)
            {
                currentMode = ExpeditionRunMode.Challenge;
            }

            currentStage = currentMode == ExpeditionRunMode.Challenge // 모드별 실행 단계 선택
                ? view.ActiveChallengeStage
                : view.ActiveLastClearedStage;
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
            resultFlash?.HideImmediate();
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
            resultFlash?.HideImmediate();
            if (modeButton != null)
            {
                modeButton.onClick.RemoveListener(ToggleMode);
            }

            ResetWaveTracking();
            ResetPlayerTracking();
            CollectAllWorldDrops(); // 씬 종료 전에 남은 드랍을 전부 획득 확정
            _ = FlushWorldDropsCheckpointAsync(); // 씬 종료 뒤에도 시작한 저장 Task가 획득분을 확정
            if (worldItemDrops != null)
            {
                worldItemDrops.ItemsConfirmed -= HandleWorldItemsConfirmed;
            }

            if (equipmentWorldDrops != null)
            {
                equipmentWorldDrops.EquipmentConfirmed -= HandleEquipmentConfirmed;
            }

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
            ShowReinforcementWarning(false);
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

        private sealed class EnemyArrivalUnit
        {
            public EnemyArrivalUnit(
                UnitActor actor,
                Vector3 entryPosition,
                Vector3 readyPosition,
                float duration,
                bool isBoss,
                bool isNinja,
                int ninjaOrdinal)
            {
                Actor = actor;
                EntryPosition = entryPosition;
                ReadyPosition = readyPosition;
                Duration = Mathf.Max(0.1f, duration);
                IsBoss = isBoss;
                IsNinja = isNinja;
                NinjaOrdinal = ninjaOrdinal;
            }

            public UnitActor Actor { get; }
            public Vector3 EntryPosition { get; }
            public Vector3 ReadyPosition { get; }
            public float Duration { get; }
            public bool IsBoss { get; }
            public bool IsNinja { get; }
            public int NinjaOrdinal { get; }
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
