using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ProjectMT.Core.SaveIO;
using ProjectMT.Core.SceneFlow;
using ProjectMT.Contents.Framework;
using ProjectMT.Features.Equipment;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.Debugging;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Stats;
using ProjectMT.Shared.UI;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class AppRootHost : MonoBehaviour // 앱 전역 서비스 조립
    {
        [SerializeField] private ProjectConfig projectConfig; // 시작 설정 모음
        [SerializeField] private SceneLoader sceneLoader; // 정식 씬 전환 담당
        [SerializeField] private RewardAcquirePresenter rewardPresenter; // 저장 확정 보상 연출
        [SerializeField] private ContentFinishFeedbackPresenter finishFeedbackPresenter; // 콘텐츠 저장 재시도 표시

        private GameDataService gameDataService; // 진행 데이터 관리자
        private ContentFlow contentFlow; // 콘텐츠 실행 흐름
        private BattlePartySnapshotBuilder partyBuilder; // 저장 편성 해석기
        private CommanderGrowthConfig commanderGrowthConfig; // 군단장 경험치·레벨 규칙
        private DebugPanelController debugPanel; // 개발 빌드 전용 도구 패널
        private bool initialized; // 중복 초기화 방지

        public static AppRootHost Instance { get; private set; } // 전역 AppRoot 한 개
        public bool IsInitialized => initialized;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); // 중복 AppRoot 제거
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 전환 뒤에도 유지
        }

        private async void Start()
        {
            try
            {
                await InitializeAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private async Task InitializeAsync()
        {
            if (initialized)
            {
                return;
            }

            if (projectConfig == null || projectConfig.SceneCatalog == null ||
                projectConfig.ContentCatalog == null || projectConfig.MonsterCatalog == null ||
                projectConfig.ItemCatalog == null)
            {
                throw new InvalidOperationException("ProjectConfig and its catalogs must be assigned.");
            }

            if (!projectConfig.ItemCatalog.TryValidateRuntimeCatalog(out var itemCatalogError))
            {
                throw new InvalidOperationException($"ItemCatalog is invalid. {itemCatalogError}");
            }

            if (sceneLoader == null)
            {
                sceneLoader = GetComponent<SceneLoader>();
            }

            if (sceneLoader == null)
            {
                throw new InvalidOperationException("SceneLoader is missing from AppRoot.");
            }

            if (finishFeedbackPresenter == null)
            {
                throw new InvalidOperationException("ContentFinishFeedbackPresenter is missing from AppRoot.");
            }

            var savePath = Path.Combine(Application.persistentDataPath, "ProjectMT_seed_save.json"); // 시드 저장 위치
            var saveService = new SaveService(new AtomicFileStore(), savePath);
            commanderGrowthConfig = projectConfig.CommanderGrowthConfig;
            if (commanderGrowthConfig == null || !commanderGrowthConfig.TryValidate(out _))
            {
                commanderGrowthConfig = CommanderGrowthConfig.RuntimeDefault; // 미할당·손상 설정 안전 복구
            }

            gameDataService = new GameDataService(
                saveService,
                commanderGrowthConfig,
                projectConfig.ItemCatalog);
            await gameDataService.LoadAsync(); // 씬 초기화 전 저장 로드
            partyBuilder = new BattlePartySnapshotBuilder(projectConfig.MonsterCatalog);

            sceneLoader.Configure(projectConfig.SceneCatalog);
            contentFlow = new ContentFlow(
                projectConfig.ContentCatalog,
                gameDataService,
                sceneLoader,
                projectConfig.MainBattleSceneId,
                projectConfig.ItemCatalog,
                rewardPresenter,
                finishFeedbackPresenter);
            sceneLoader.ContextFactory = CreateSceneContext; // 씬별 권한 봉투 생성
            sceneLoader.SceneFailed += HandleSceneFailed;

            initialized = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CreateDebugPanel();
#endif
            sceneLoader.InitializeCurrentScene(); // 현재 Entry부터 초기화
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void CreateDebugPanel()
        {
            if (debugPanel != null)
            {
                return;
            }

            var prefab = Resources.Load<DebugPanelController>("Debug/PF_DebugPanel");
            if (prefab == null)
            {
                Debug.LogWarning("Debug panel prefab is missing: Resources/Debug/PF_DebugPanel");
                return;
            }

            debugPanel = Instantiate(prefab, transform);
            debugPanel.name = "DebugPanel";
            debugPanel.Configure(
                ResetGameDataForDebugAsync,
                DrawMonsterForDebugAsync,
                AcquireEquipmentForDebugAsync);
        }

        private async Task<bool> ResetGameDataForDebugAsync()
        {
            if (!initialized || gameDataService == null || contentFlow == null ||
                contentFlow.IsRunning || sceneLoader == null || sceneLoader.IsTransitioning)
            {
                return false; // 콘텐츠 실행·씬 전환 중 초기화 금지
            }

            await gameDataService.ResetToDefaultAsync();
            sceneLoader.Load(projectConfig.EntrySceneId); // 새 Snapshot으로 다시 진입
            return true;
        }

        private async Task<string> DrawMonsterForDebugAsync()
        {
            if (!initialized || gameDataService == null || contentFlow == null ||
                contentFlow.IsRunning || sceneLoader == null || sceneLoader.IsTransitioning)
            {
                return "현재 몬스터를 뽑을 수 없습니다";
            }

            var roster = gameDataService.View.Monsters;
            var candidates = new List<MonsterDefinition>();
            var definitions = projectConfig.MonsterCatalog.Definitions;
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (definition != null && !roster.Owns(definition.MonsterId))
                {
                    candidates.Add(definition);
                }
            }

            if (candidates.Count == 0)
            {
                return "모든 카탈로그 몬스터를 보유 중입니다";
            }

            var selected = candidates[UnityEngine.Random.Range(0, candidates.Count)]; // 미보유 항목 균등 선택
            var saved = await gameDataService.TryApplyAndSaveAsync(
                GameProgressChange.AcquireMonster(selected.MonsterId));
            return saved
                ? $"{selected.DisplayName} 획득 완료"
                : "획득 정보를 저장하지 못했습니다";
        }

        private async Task<string> AcquireEquipmentForDebugAsync()
        {
            if (!initialized || gameDataService == null || contentFlow == null ||
                contentFlow.IsRunning || sceneLoader == null || sceneLoader.IsTransitioning)
            {
                return "현재 장비를 받을 수 없습니다";
            }

            var before = gameDataService.View.Equipment.Instances.Count;
            var drops = EquipmentDropRoller.RollDrop();
            var saved = await gameDataService.TryApplyAndSaveAsync(
                GameProgressChange.AcquireEquipment(drops));
            if (!saved)
            {
                return "장비 획득 정보를 저장하지 못했습니다";
            }

            var acquired = gameDataService.View.Equipment.Instances.Count - before;
            return acquired > 0 ? $"장비 {acquired}개 획득 완료" : "장비 보유 한도입니다";
        }
#endif

        private ISceneContext CreateSceneContext(SceneId sceneId)
        {
            if (sceneId == projectConfig.EntrySceneId)
            {
                return new EntrySceneContext(() => sceneLoader.Load(projectConfig.MainBattleSceneId)); // Entry 출구 연결
            }

            if (sceneId == projectConfig.MainBattleSceneId)
            {
                return new MainSceneContext(
                    gameDataService,
                    contentFlow,
                    projectConfig.MonsterCatalog,
                    projectConfig.ItemCatalog,
                    commanderGrowthConfig,
                    projectConfig.EquipmentBalanceConfig,
                    () => partyBuilder.Build(gameDataService.View), // 저장 확정 시 새 부대 사진 생성
                    rewardPresenter);
            }

            return contentFlow.CreateSeparateSceneContext(sceneId); // 별도 콘텐츠 실행 봉투
        }

        private void HandleSceneFailed(SceneId failedSceneId, string error)
        {
            Debug.LogError($"Scene flow failed. Scene={failedSceneId}, Error={error}");
            contentFlow?.NotifySceneLoadFailed(failedSceneId); // 별도 콘텐츠 진입 실패 잠금 해제
            if (failedSceneId != projectConfig.EntrySceneId && !sceneLoader.IsTransitioning)
            {
                sceneLoader.Load(projectConfig.EntrySceneId); // 실패 시 Entry로 복귀
            }
        }

        private async void OnApplicationPause(bool paused)
        {
            if (paused && initialized)
            {
                await SaveCurrentSafelyAsync(); // 백그라운드 전환 저장
            }
        }

        private async void OnApplicationQuit()
        {
            if (initialized)
            {
                await SaveCurrentSafelyAsync(); // 앱 종료 직전 저장
            }
        }

        private async Task SaveCurrentSafelyAsync()
        {
            try
            {
                await gameDataService.SaveCurrentAsync();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                if (sceneLoader != null)
                {
                    sceneLoader.SceneFailed -= HandleSceneFailed;
                }

                Instance = null;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(ProjectConfig config, SceneLoader loader)
        {
            projectConfig = config;
            sceneLoader = loader;
        }

        public void EditorConfigureRewardPresenter(RewardAcquirePresenter presenter)
        {
            rewardPresenter = presenter;
        }

        public void EditorConfigureFinishFeedback(ContentFinishFeedbackPresenter presenter)
        {
            finishFeedbackPresenter = presenter;
        }
#endif
    }
}
