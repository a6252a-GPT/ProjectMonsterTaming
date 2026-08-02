using System;
using System.IO;
using System.Threading.Tasks;
using ProjectMT.Core.SaveIO;
using ProjectMT.Core.SceneFlow;
using ProjectMT.Contents.Framework;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.GameData;
using UnityEngine;

namespace ProjectMT.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class AppRootHost : MonoBehaviour // 앱 전역 서비스 조립
    {
        [SerializeField] private ProjectConfig projectConfig; // 시작 설정 모음
        [SerializeField] private SceneLoader sceneLoader; // 정식 씬 전환 담당

        private GameDataService gameDataService; // 진행 데이터 관리자
        private ContentFlow contentFlow; // 콘텐츠 실행 흐름
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

            if (projectConfig == null || projectConfig.SceneCatalog == null || projectConfig.ContentCatalog == null)
            {
                throw new InvalidOperationException("ProjectConfig and its catalogs must be assigned.");
            }

            if (sceneLoader == null)
            {
                sceneLoader = GetComponent<SceneLoader>();
            }

            if (sceneLoader == null)
            {
                throw new InvalidOperationException("SceneLoader is missing from AppRoot.");
            }

            var savePath = Path.Combine(Application.persistentDataPath, "ProjectMT_seed_save.json"); // 시드 저장 위치
            var saveService = new SaveService(new AtomicFileStore(), savePath);
            gameDataService = new GameDataService(saveService);
            await gameDataService.LoadAsync(); // 씬 초기화 전 저장 로드

            sceneLoader.Configure(projectConfig.SceneCatalog);
            contentFlow = new ContentFlow(
                projectConfig.ContentCatalog,
                gameDataService,
                sceneLoader,
                projectConfig.MainBattleSceneId);
            sceneLoader.ContextFactory = CreateSceneContext; // 씬별 권한 봉투 생성
            sceneLoader.SceneFailed += HandleSceneFailed;

            initialized = true;
            sceneLoader.InitializeCurrentScene(); // 현재 Entry부터 초기화
        }

        private ISceneContext CreateSceneContext(SceneId sceneId)
        {
            if (sceneId == projectConfig.EntrySceneId)
            {
                return new EntrySceneContext(() => sceneLoader.Load(projectConfig.MainBattleSceneId)); // Entry 출구 연결
            }

            if (sceneId == projectConfig.MainBattleSceneId)
            {
                return new MainSceneContext(gameDataService, contentFlow); // 메인전투 권한 전달
            }

            return contentFlow.CreateSeparateSceneContext(sceneId); // 별도 콘텐츠 실행 봉투
        }

        private void HandleSceneFailed(SceneId failedSceneId, string error)
        {
            Debug.LogError($"Scene flow failed. Scene={failedSceneId}, Error={error}");
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
#endif
    }
}
