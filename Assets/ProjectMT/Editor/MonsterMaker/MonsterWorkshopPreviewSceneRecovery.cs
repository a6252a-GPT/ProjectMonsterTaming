using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    internal static class MonsterWorkshopPreviewSceneRecovery // 조립소 누수로 고갈된 Preview Scene 마스크를 회수
    {
        private const string BasicRootName = "[Basic Attack Workshop Preview]";
        private const string ActiveRootName = "[Active Attack Workshop Preview]";

        internal static bool HasRenderingMask(PreviewRenderUtility utility)
        {
            return utility != null && utility.camera != null &&
                   EditorSceneManager.GetSceneCullingMask(utility.camera.gameObject.scene) != 0;
        }

        internal static int RecoverOrphanedScenesIfNeeded()
        {
            if (EditorSceneManager.CalculateAvailableSceneCullingMask() != 0) return 0;

            var preservedHandles = new HashSet<int>();
            foreach (var window in Resources.FindObjectsOfTypeAll<MonsterBasicAttackWorkshopWindow>())
            {
                if (window != null && window.PreviewSceneHandle != 0)
                    preservedHandles.Add(window.PreviewSceneHandle);
            }
            foreach (var window in Resources.FindObjectsOfTypeAll<MonsterActiveAttackWorkshopWindow>())
            {
                if (window != null && window.PreviewSceneHandle != 0)
                    preservedHandles.Add(window.PreviewSceneHandle);
            }

            var scenes = Resources.FindObjectsOfTypeAll<Camera>()
                .Where(camera => camera != null && camera.gameObject.scene.IsValid() &&
                                 EditorSceneManager.IsPreviewScene(camera.gameObject.scene))
                .Select(camera => camera.gameObject.scene)
                .GroupBy(scene => scene.handle)
                .Select(group => group.First())
                .ToArray();
            var recovered = 0;
            for (var index = 0; index < scenes.Length; index++)
            {
                var scene = scenes[index];
                if (preservedHandles.Contains(scene.handle)) continue;
                var ownedByWorkshop = scene.GetRootGameObjects().Any(root =>
                    root != null && (root.name == BasicRootName || root.name == ActiveRootName));
                if (!ownedByWorkshop) continue;
                if (EditorSceneManager.ClosePreviewScene(scene)) recovered++;
            }
            return recovered;
        }
    }
}
