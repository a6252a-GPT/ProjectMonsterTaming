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
        public const string StageMapPrefabPath =
            "Assets/ProjectMT/98_Generated/Stages/hex1/PF_StageMap_hex1.prefab";

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
            EnsureStageMap(scene);
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
            camera.fieldOfView = 32f;
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

        private static void EnsureStageMap(Scene scene)
        {
            var stageMap = scene.GetRootGameObjects()
                .FirstOrDefault(value => value.name == "PF_StageMap_hex1");
            if (stageMap == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StageMapPrefabPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException($"Hex 배경 맵 Prefab이 없습니다: {StageMapPrefabPath}");
                }

                stageMap = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject ??
                           throw new InvalidOperationException("DEV Hex 배경 맵 Prefab 인스턴스 생성에 실패했습니다.");
            }

            stageMap.name = "PF_StageMap_hex1";
            stageMap.transform.SetPositionAndRotation(
                new Vector3(0f, -4.34f, 4.73f),
                Quaternion.identity);
            stageMap.transform.localScale = Vector3.one;
            stageMap.SetActive(true);
            var mapLight = stageMap.GetComponentsInChildren<Light>(true)
                .FirstOrDefault(value => value.name == "Directional Light");
            if (mapLight != null)
            {
                mapLight.gameObject.SetActive(true);
            }

            EditorUtility.SetDirty(stageMap);
        }

    }
}
