using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectMT.EditorTools.MonsterMaker
{
    internal static class MonsterWorkshopPreviewSceneRecovery // 조립소 누수로 고갈된 Preview Scene 마스크를 회수
    {
        private const string BasicRootName = "[Basic Attack Workshop Preview]";
        private const string BasicV2RootName = "[Basic Attack Workshop V2 Preview]";
        private const string ActiveRootName = "[Active Attack Workshop Preview]";
        private static readonly HashSet<Scene> OwnedScenes = new HashSet<Scene>();

        internal static bool HasRenderingMask(PreviewRenderUtility utility)
        {
            return utility != null && utility.camera != null &&
                   EditorSceneManager.GetSceneCullingMask(utility.camera.gameObject.scene) != 0;
        }

        internal static void RegisterOwner(PreviewRenderUtility utility)
        {
            if (utility?.camera == null) return;
            var scene = utility.camera.gameObject.scene;
            if (scene.IsValid() && EditorSceneManager.IsPreviewScene(scene))
                OwnedScenes.Add(scene);
        }

        internal static void UnregisterOwner(PreviewRenderUtility utility)
        {
            if (utility?.camera == null) return;
            OwnedScenes.Remove(utility.camera.gameObject.scene);
        }

        internal static int RecoverOrphanedScenesIfNeeded()
        {
            if (EditorSceneManager.CalculateAvailableSceneCullingMask() != 0) return 0;

            var scenes = Resources.FindObjectsOfTypeAll<Camera>()
                .Where(camera => camera != null && camera.gameObject.scene.IsValid() &&
                                 EditorSceneManager.IsPreviewScene(camera.gameObject.scene))
                .Select(camera => camera.gameObject.scene)
                .GroupBy(scene => scene.handle)
                .Select(group => group.First())
                .ToArray();
            OwnedScenes.IntersectWith(scenes);
            var preservedScenes = new HashSet<Scene>(OwnedScenes);
            var recovered = 0;
            for (var index = 0; index < scenes.Length; index++)
            {
                var scene = scenes[index];
                if (preservedScenes.Contains(scene)) continue;
                var ownedByWorkshop = scene.GetRootGameObjects().Any(root =>
                    root != null && (root.name == BasicRootName ||
                                     root.name == BasicV2RootName ||
                                     root.name == ActiveRootName));
                if (!ownedByWorkshop) continue;
                if (EditorSceneManager.ClosePreviewScene(scene)) recovered++;
            }
            return recovered;
        }
    }
}
