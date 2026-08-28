using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectMT.Contents.TreasureSpirit.Demo
{
    [InitializeOnLoad]
    internal static class DungeonBakePrefabCleaner
    {
        static DungeonBakePrefabCleaner()
        {
            PrefabStage.prefabSaving += OnPrefabSaving;
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
            EditorApplication.delayCall += CleanOpenDungeonBakeStage;
        }

        [MenuItem("ProjectMT/TreasureSpirit Demo/Clean DungeonBakes Missing Scripts")]
        private static void CleanAllMenu()
        {
            CleanOpenDungeonBakeStage();

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/ProjectMT/04_Contents/03_TreasureSpirit/Demo/DungeonBakes" });
            int totalRemoved = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                totalRemoved += CleanPrefabAsset(path);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[DungeonBakePrefabCleaner] DungeonBakes Missing Script {totalRemoved}개 제거");
        }

        private static void OnPrefabStageOpened(PrefabStage stage)
        {
            EditorApplication.delayCall += CleanOpenDungeonBakeStage;
        }

        private static void OnPrefabSaving(GameObject prefabRoot)
        {
            if (prefabRoot == null)
            {
                return;
            }

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || !IsDungeonBakePath(stage.assetPath))
            {
                return;
            }

            int removed = RemoveMissingRecursive(prefabRoot);
            if (removed > 0)
            {
                Debug.Log($"[DungeonBakePrefabCleaner] 저장 전 Missing Script {removed}개 제거");
            }
        }

        private static void CleanOpenDungeonBakeStage()
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.prefabContentsRoot == null || !IsDungeonBakePath(stage.assetPath))
            {
                return;
            }

            int removed = RemoveMissingRecursive(stage.prefabContentsRoot);
            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(stage.scene);
                Debug.Log($"[DungeonBakePrefabCleaner] 열린 프리팹에서 Missing Script {removed}개 제거: {stage.assetPath}");
            }
        }

        private static int CleanPrefabAsset(string path)
        {
            if (!IsDungeonBakePath(path))
            {
                return 0;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int removed = RemoveMissingRecursive(root);
            if (removed > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }

            PrefabUtility.UnloadPrefabContents(root);
            return removed;
        }

        private static bool IsDungeonBakePath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.Replace('\\', '/').Contains("/DungeonBakes/");
        }

        private static int RemoveMissingRecursive(GameObject root)
        {
            if (root == null)
            {
                return 0;
            }

            int removed = 0;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null)
                {
                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transforms[i].gameObject);
                }
            }

            return removed;
        }
    }
}
