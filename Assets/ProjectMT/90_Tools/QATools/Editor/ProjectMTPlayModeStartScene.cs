#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectMT.EditorTools
{
    [InitializeOnLoad]
    internal static class ProjectMTPlayModeStartScene // 필요할 때만 Entry 시작을 강제
    {
        private static readonly bool ForceEntryStartEnabled = false; // 현재 씬 직접 실행 사용
        internal const string EntryScenePath = "Assets/ProjectMT/00_Scenes/00_Entry.unity"; // 정식 진입 씬
        private const string DevScenePrefix = "DEV_"; // 직접 실행 허용 개발 씬
        private const string TestScenePrefix = "InitTestScene"; // 테스트 러너 임시 씬

        static ProjectMTPlayModeStartScene()
        {
            if (!ForceEntryStartEnabled)
            {
                EditorSceneManager.playModeStartScene = null; // 기존 강제 시작 설정 해제
                return;
            }

            EditorSceneManager.activeSceneChangedInEditMode += HandleActiveSceneChanged;
            EditorSceneManager.sceneSaved += HandleSceneSaved;
            EditorApplication.delayCall += ApplyForActiveScene; // 에디터 로드가 끝난 뒤 최초 적용
        }

        private static void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            ApplyForScene(nextScene);
        }

        private static void HandleSceneSaved(Scene scene)
        {
            if (scene == EditorSceneManager.GetActiveScene())
            {
                ApplyForScene(scene);
            }
        }

        private static void ApplyForActiveScene()
        {
            ApplyForScene(EditorSceneManager.GetActiveScene());
        }

        private static void ApplyForScene(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (AllowsDirectPlay(scene)) // DEV와 Test Runner는 현재 씬 직접 실행
            {
                if (EditorSceneManager.playModeStartScene != null)
                {
                    EditorSceneManager.playModeStartScene = null;
                }

                return;
            }

            var entryScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(EntryScenePath);
            if (entryScene == null)
            {
                Debug.LogError($"ProjectMT Entry scene is missing: {EntryScenePath}");
                return;
            }

            if (EditorSceneManager.playModeStartScene != entryScene)
            {
                EditorSceneManager.playModeStartScene = entryScene; // 일반 씬 Play도 Entry부터 시작
            }
        }

        private static bool AllowsDirectPlay(Scene scene)
        {
            if (!scene.IsValid())
            {
                return false;
            }

            return scene.name.StartsWith(DevScenePrefix, StringComparison.OrdinalIgnoreCase)
                || scene.name.StartsWith(TestScenePrefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
