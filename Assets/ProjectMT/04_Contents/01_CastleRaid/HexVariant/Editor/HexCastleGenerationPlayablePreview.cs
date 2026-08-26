using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    [InitializeOnLoad]
    public static class HexCastleGenerationPlayablePreview // 현재 Foundation Cell을 그대로 쓰는 임시 전투 시험장
    {
        public const string RootName = "__HexCastleGenerationPlayablePreview";

        static HexCastleGenerationPlayablePreview()
        {
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneClosing += OnSceneClosing;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.quitting += ClearAll;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static GameObject Create(
            HexCastleCandidate candidate,
            Vector3 worldOffset,
            HexCastleThemeOneRules rules = null,
            bool requireDedicatedScene = true)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (!candidate.Validation.IsValid)
            {
                throw new InvalidOperationException(string.Join("\n", candidate.Validation.Errors));
            }

            if (!HexCastleSilhouettePlanner.SupportedThemes.Contains(candidate.Layout.Theme) ||
                candidate.Layout.RulesVersion < HexCastleFoundationGenerator.FoundationRulesVersionBase)
            {
                throw new InvalidOperationException(
                    "플레이 미리보기는 정식 A~I Foundation 후보만 지원합니다.");
            }

            rules ??= HexCastleThemeOneRulesAssetUtility.LoadOrCreate();
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException("플레이 미리보기를 만들 열린 Scene이 없습니다.");
            }

            Clear(scene);
            HexCastleFoundationVisualGate.Remove(scene);
            var sceneWasDirty = scene.isDirty;
            HexCastleFoundationVisualGate.Create(
                candidate.Layout.Seed,
                candidate.Layout.DefenseLayerCount,
                candidate.Layout.Theme,
                rules.Tuning,
                requireDedicatedScene);

            var root = scene.GetRootGameObjects()
                .FirstOrDefault(value => value.name == HexCastleFoundationVisualGate.RootName);
            if (root == null)
            {
                throw new InvalidOperationException("Theme 1 Foundation 3D Root 생성에 실패했습니다.");
            }

            root.name = RootName;
            root.transform.position = worldOffset;
            MakeScenePersistent(root);
            var stage = root.AddComponent<HexCastleFoundationPlayableStage>();
            stage.Configure(
                candidate.Layout.Seed,
                candidate.Layout.DefenseLayerCount,
                candidate.Layout.Theme,
                rules,
                sceneWasDirty);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected(false);
            SceneView.RepaintAll();
            return root;
        }

        public static int Clear(Scene scene)
        {
            return Clear(scene, true);
        }

        private static int Clear(Scene scene, bool restoreDirtyState)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return 0;
            }

            var roots = scene.GetRootGameObjects()
                .Where(value => value.name == RootName)
                .ToArray();
            var shouldMarkClean = roots.Any(root =>
            {
                var stage = root.GetComponent<HexCastleFoundationPlayableStage>();
                return stage != null && !stage.SceneWasDirty;
            });
            foreach (var root in roots)
            {
                HexCastleFoundationVisualGate.DestroyPreviewRoot(root);
            }

            if (restoreDirtyState && shouldMarkClean && roots.Length > 0)
            {
                RestoreCleanState(scene);
            }

            SceneView.RepaintAll();
            return roots.Length;
        }

        public static void ClearAll()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                Clear(SceneManager.GetSceneAt(index));
            }
        }

        private static void MakeScenePersistent(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.hideFlags = HideFlags.None;
            }

            foreach (var mesh in root.GetComponentsInChildren<MeshFilter>(true)
                         .Select(value => value.sharedMesh)
                         .Concat(root.GetComponentsInChildren<MeshCollider>(true)
                             .Select(value => value.sharedMesh))
                         .Where(value => value != null && !AssetDatabase.Contains(value))
                         .Distinct())
            {
                mesh.hideFlags = HideFlags.None;
            }

            foreach (var material in root.GetComponentsInChildren<Renderer>(true)
                         .SelectMany(value => value.sharedMaterials)
                         .Where(value => value != null && !AssetDatabase.Contains(value))
                         .Distinct())
            {
                material.hideFlags = HideFlags.None;
            }
        }

        private static void OnSceneSaving(Scene scene, string path)
        {
            Clear(scene, false);
        }

        private static void OnSceneClosing(Scene scene, bool removingScene)
        {
            Clear(scene);
        }

        private static void OnActiveSceneChanged(Scene previous, Scene next)
        {
            Clear(previous);
        }

        private static void OnBeforeAssemblyReload()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                ClearAll();
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                ClearAll();
            }
        }

        private static void RestoreCleanState(Scene scene)
        {
            var method = typeof(EditorSceneManager).GetMethod(
                "ClearSceneDirtiness",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            method?.Invoke(null, new object[] { scene });
        }
    }
}
