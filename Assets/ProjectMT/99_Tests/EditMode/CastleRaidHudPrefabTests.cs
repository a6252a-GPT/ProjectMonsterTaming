using System.Linq;
using NUnit.Framework;
using ProjectMT.Contents.CastleRaid;
using ProjectMT.Shared.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectMT.Tests.EditMode
{
    public sealed class CastleRaidHudPrefabTests // 군단의 역습 HUD 정식 Prefab 계약
    {
        private const string HudPrefabPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/Prefabs/PF_CastleRaidHUD.prefab";
        private const string CastleScenePath = "Assets/ProjectMT/00_Scenes/02_CastleRaid.unity";
        private const string ClearOverlayPrefabPath =
            "Assets/ProjectMT/02_Shared/UI/Prefabs/PF_ContentClearOverlay.prefab";

        [Test]
        public void Prefab_PreservesAuthoredHudAndSharedClearOverlay() // 외형·중첩 원본 유지
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);

            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.GetComponent<Canvas>(), Is.Not.Null);
            Assert.That(hud.GetComponent<CanvasScaler>(), Is.Not.Null);
            Assert.That(hud.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(hud.GetComponentsInChildren<Transform>(true), Has.Length.EqualTo(55));
            Assert.That(hud.GetComponentsInChildren<Button>(true), Has.Length.EqualTo(7));
            Assert.That(hud.GetComponentsInChildren<EventSystem>(true), Is.Empty);
            Assert.That(CountMissingScripts(hud), Is.Zero);

            var clearOverlay = hud.GetComponentsInChildren<ContentClearOverlay>(true).Single();
            Assert.That(
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(clearOverlay.gameObject),
                Is.EqualTo(ClearOverlayPrefabPath));

            var inputSurface = hud.GetComponentInChildren<CastleDeploymentInputSurface>(true);
            Assert.That(inputSurface, Is.Not.Null);
            Assert.That(
                new SerializedObject(inputSurface).FindProperty("controller")?.objectReferenceValue,
                Is.Null,
                "씬 Controller 참조는 Prefab 자산이 아니라 Scene Instance override가 소유해야 합니다.");
        }

        [Test]
        public void CastleScene_UsesConnectedHudInstanceAndKeepsControllerReferences() // 씬 연결·기능 참조 유지
        {
            var scene = EditorSceneManager.OpenScene(CastleScenePath, OpenSceneMode.Additive);
            try
            {
                var sceneObjects = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Select(transform => transform.gameObject)
                    .ToArray();
                var hud = sceneObjects.Single(gameObject => gameObject.name == "CastleRaidHUD");
                var controller = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CastleRaidController>(true))
                    .Single();

                Assert.That(PrefabUtility.GetPrefabInstanceStatus(hud), Is.EqualTo(PrefabInstanceStatus.Connected));
                Assert.That(PrefabUtility.GetNearestPrefabInstanceRoot(hud), Is.SameAs(hud));
                Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hud), Is.EqualTo(HudPrefabPath));
                Assert.That(sceneObjects.SelectMany(gameObject => gameObject.GetComponents<EventSystem>()), Is.Empty);
                Assert.That(CountMissingScripts(hud), Is.Zero);

                var controllerData = new SerializedObject(controller);
                AssertReference(controllerData, "deploymentText");
                AssertReference(controllerData, "statusText");
                AssertArrayReferences(controllerData, "unitButtons", 5);
                AssertArrayReferences(controllerData, "unitButtonLabels", 5);
                AssertReference(controllerData, "exitButton");
                AssertReference(controllerData, "clearOverlay");

                var inputSurface = hud.GetComponentInChildren<CastleDeploymentInputSurface>(true);
                var inputController = new SerializedObject(inputSurface)
                    .FindProperty("controller")?.objectReferenceValue;
                Assert.That(inputController, Is.SameAs(controller));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AssertReference(SerializedObject owner, string propertyName)
        {
            Assert.That(owner.FindProperty(propertyName)?.objectReferenceValue, Is.Not.Null, propertyName);
        }

        private static void AssertArrayReferences(SerializedObject owner, string propertyName, int count)
        {
            var property = owner.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            Assert.That(property.arraySize, Is.EqualTo(count), propertyName);
            for (var index = 0; index < property.arraySize; index++)
            {
                Assert.That(property.GetArrayElementAtIndex(index).objectReferenceValue, Is.Not.Null,
                    $"{propertyName}[{index}]");
            }
        }

        private static int CountMissingScripts(GameObject root)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
        }
    }
}
