using System;
using System.Collections;
using System.Collections.Generic;
using ProjectMT.Core.Config;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectMT.Core.SceneFlow
{
    [DisallowMultipleComponent]
    public sealed class SceneLoader : MonoBehaviour, ISceneNavigator // 정식 씬 단일 전환 담당
    {
        [SerializeField] private SceneCatalog sceneCatalog; // ID를 실제 경로로 변환

        private ISceneRoot currentRoot; // 현재 씬의 종료 대상
        private bool isTransitioning; // 중복 전환 차단

        public Func<SceneId, ISceneContext> ContextFactory { get; set; } // 씬별 권한 생성기
        public bool IsTransitioning => isTransitioning;
        public event Action<SceneId> SceneLoadStarted; // 로딩 화면 표시 시점
        public event Action<SceneId> SceneReady; // 초기화 완료 알림
        public event Action<SceneId, string> SceneFailed; // 전환 실패 알림

        public void Configure(SceneCatalog catalog)
        {
            sceneCatalog = catalog;
        }

        public void InitializeCurrentScene()
        {
            if (!isTransitioning)
            {
                StartCoroutine(InitializeCurrentSceneRoutine()); // 최초 Entry 초기화
            }
        }

        public void Load(SceneId sceneId)
        {
            if (isTransitioning)
            {
                Debug.LogWarning($"Scene transition is already running. Requested={sceneId}");
                return;
            }

            StartCoroutine(LoadRoutine(sceneId)); // 한 번에 한 전환만 실행
        }

        private IEnumerator InitializeCurrentSceneRoutine()
        {
            isTransitioning = true;
            yield return null;

            var activeScene = SceneManager.GetActiveScene();
            if (!TryInitializeRoot(activeScene, out var sceneId, out var error))
            {
                isTransitioning = false;
                SceneFailed?.Invoke(default, error);
                Debug.LogError(error);
                yield break;
            }

            isTransitioning = false;
            SceneReady?.Invoke(sceneId);
        }

        private IEnumerator LoadRoutine(SceneId sceneId)
        {
            isTransitioning = true;

            if (sceneCatalog == null || !sceneCatalog.TryGet(sceneId, out var entry))
            {
                Fail(sceneId, $"Scene is not registered: {sceneId}");
                yield break;
            }

            SceneLoadStarted?.Invoke(sceneId);

            try
            {
                currentRoot?.Shutdown(); // 기존 씬 자원 먼저 정리
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            currentRoot = null;
            AsyncOperation operation;
            try
            {
                operation = SceneManager.LoadSceneAsync(entry.ScenePath, LoadSceneMode.Single); // 기존 씬 교체
            }
            catch (Exception exception)
            {
                Fail(sceneId, exception.Message);
                yield break;
            }

            if (operation == null)
            {
                Fail(sceneId, $"Unity did not create a load operation: {entry.ScenePath}");
                yield break;
            }

            while (!operation.isDone)
            {
                yield return null;
            }

            yield return null;
            if (!TryInitializeRoot(SceneManager.GetActiveScene(), out var initializedId, out var error))
            {
                Fail(sceneId, error);
                yield break;
            }

            isTransitioning = false;
            SceneReady?.Invoke(initializedId);
        }

        private bool TryInitializeRoot(Scene scene, out SceneId sceneId, out string error)
        {
            var roots = new List<ISceneRoot>(); // 비활성 자식까지 Root 검색
            foreach (var rootObject in scene.GetRootGameObjects())
            {
                var behaviours = rootObject.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var behaviour in behaviours)
                {
                    if (behaviour is ISceneRoot root)
                    {
                        roots.Add(root);
                    }
                }
            }

            if (roots.Count != 1) // 정식 씬은 Root가 정확히 한 개
            {
                sceneId = default;
                error = $"Scene must contain exactly one ISceneRoot. Scene={scene.path}, Count={roots.Count}";
                return false;
            }

            currentRoot = roots[0];
            sceneId = currentRoot.SceneId;

            if (ContextFactory == null)
            {
                error = $"Scene context factory is missing. Scene={sceneId}";
                return false;
            }

            var context = ContextFactory(sceneId); // 필요한 권한만 씬에 전달
            if (context == null)
            {
                error = $"Scene context is missing. Scene={sceneId}";
                return false;
            }

            try
            {
                currentRoot.Initialize(context);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = $"Scene initialization failed. Scene={sceneId}, Error={exception.Message}";
                Debug.LogException(exception);
                return false;
            }
        }

        private void Fail(SceneId sceneId, string error)
        {
            isTransitioning = false;
            SceneFailed?.Invoke(sceneId, error);
            Debug.LogError(error);
        }
    }
}
