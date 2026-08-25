using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    public static class HexCastleSceneSetupUtility
    {
        public const string ScenePath = "Assets/ProjectMT/00_Scenes/DEV_CastleRaidHex.unity";

        [MenuItem("JC Tool/군단의 역습 육각/카메라/Perspective 카메라 적용")]
        public static void ConfigureThemeOnePerspectiveCameraMenu()
        {
            ConfigureThemeOnePerspectiveCamera(true);
        }

        public static void ConfigureThemeOnePerspectiveCamera(bool saveScene)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }

                scene = EditorSceneManager.OpenScene(
                    ScenePath,
                    OpenSceneMode.Single);
            }

            HexCastleGenerationPlayablePreview.Clear(scene);
            HexCastleFoundationVisualGate.Remove(scene);
            var camera = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault();
            if (camera == null)
            {
                throw new InvalidOperationException("DEV_CastleRaidHex Scene에 Camera가 없습니다.");
            }

            var controller = camera.GetComponent<HexCastleCameraController>();
            if (controller == null)
            {
                controller = camera.gameObject.AddComponent<HexCastleCameraController>();
            }

            camera.orthographic = false;
            camera.fieldOfView = 38f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 300f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.17f, 0.20f, 0.22f, 1f);
            controller.EditorConfigure(10);
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene)
            {
                EditorSceneManager.SaveScene(scene);
            }

            Selection.activeGameObject = camera.gameObject;
            Debug.Log(
                "[HexCastle] Theme 1 Perspective 카메라를 3중벽 기본 크기에 맞춰 저장했습니다. " +
                "2·3·4중벽 플레이 미리보기는 생성된 Board 크기로 자동 재설정됩니다.");
        }

    }
}
