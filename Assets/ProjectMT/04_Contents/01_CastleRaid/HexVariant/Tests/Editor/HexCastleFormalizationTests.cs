using System.Linq;
using NUnit.Framework;
using ProjectMT.Contents.CastleRaidHex.Editor;
using ProjectMT.Contents.Framework;
using ProjectMT.Core.Config;
using ProjectMT.Core.SceneFlow;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectMT.Contents.CastleRaidHex.Editor.Tests
{
    public sealed class HexCastleFormalizationTests
    {
        [Test]
        public void ProductionCastleRaidResultAdapter_AcceptsHexObjectiveResult()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ContentDefinition>(
                HexCastleProductionSceneSetupUtility.ContentDefinitionPath);

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.ResultAdapter, Is.Not.Null);
            Assert.That(
                definition.ResultAdapter.TryCreateProgressChange(new HexCastleRaidResult(true), out var change),
                Is.True);
            Assert.That(change, Is.Not.Null);
            Assert.That(
                definition.ResultAdapter.TryCreateProgressChange(new HexCastleRaidResult(false), out _),
                Is.False);
        }

        [Test]
        public void CastleRaidDefinition_ResolvesOnlyProductionHexScene()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ContentDefinition>(
                HexCastleProductionSceneSetupUtility.ContentDefinitionPath);

            Assert.That(definition, Is.Not.Null);
            Assert.That(
                definition.TryResolveSceneId(default, out var hexScene),
                Is.True);
            Assert.That(hexScene, Is.EqualTo(new SceneId("castle_raid_hex")));
            Assert.That(
                definition.TryResolveSceneId(new ContentVariantId("square"), out _),
                Is.False);
        }

        [Test]
        public void SceneCatalogAndBuildSettings_ContainProductionHexScene()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SceneCatalog>(
                HexCastleProductionSceneSetupUtility.SceneCatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryGet(new SceneId("castle_raid_hex"), out var entry), Is.True);
            Assert.That(catalog.TryGet(new SceneId("castle_raid"), out _), Is.False);
            Assert.That(entry.ScenePath, Is.EqualTo(HexCastleProductionSceneSetupUtility.HexScenePath));
            Assert.That(entry.SceneKind, Is.EqualTo(SceneKind.SeparateContent));
            Assert.That(EditorBuildSettings.scenes.Any(value =>
                    value.enabled && value.path == HexCastleProductionSceneSetupUtility.HexScenePath),
                Is.True);
            Assert.That(EditorBuildSettings.scenes.Any(value =>
                    value.path == HexCastleProductionSceneSetupUtility.LegacySquareScenePath),
                Is.False);
        }

        [Test]
        public void ProductionHexRuntimeVisualSet_IsCompleteWithoutBakedCatalog()
        {
            var visualSet = AssetDatabase.LoadAssetAtPath<HexCastleVisualSet>(
                HexCastleRuntimeVisualSetAssetUtility.AssetPath);

            Assert.That(visualSet, Is.Not.Null);
            Assert.That(visualSet.IsRuntimeComplete, Is.True);
            Assert.That(AssetDatabase.IsValidFolder(
                "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Data/Baked"), Is.False);
            Assert.That(AssetDatabase.IsValidFolder(
                "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Prefabs/Baked"), Is.False);
        }

        [Test]
        public void ProductionHexScene_HasCompleteRuntimeAndSharedHudWiring()
        {
            var scene = EditorSceneManager.OpenPreviewScene(HexCastleProductionSceneSetupUtility.HexScenePath);
            try
            {
                var roots = scene.GetRootGameObjects();
                var sceneRoot = roots.SelectMany(value =>
                        value.GetComponentsInChildren<HexCastleRaidSceneRoot>(true))
                    .Single();
                var controller = roots.SelectMany(value =>
                        value.GetComponentsInChildren<HexCastleRaidController>(true))
                    .Single();
                var camera = roots.SelectMany(value => value.GetComponentsInChildren<Camera>(true)).Single();
                var inputSurface = roots.SelectMany(value =>
                        value.GetComponentsInChildren<HexCastleDeploymentInputSurface>(true))
                    .Where(value => value.gameObject.activeInHierarchy)
                    .Single();
                var productionHud = roots
                    .SelectMany(value => value.GetComponentsInChildren<Canvas>(true))
                    .Select(value => value.gameObject)
                    .Single(value =>
                        value.activeInHierarchy &&
                        value.name.StartsWith("PF_CastleRaidHexHUD", System.StringComparison.Ordinal));
                var controllerData = new SerializedObject(controller);
                var inputData = new SerializedObject(inputSurface);
                var rootData = new SerializedObject(sceneRoot);
                var localEventSystems = roots.SelectMany(value =>
                    value.GetComponentsInChildren<EventSystem>(true)).ToArray();
                Assert.That(localEventSystems, Is.Empty, "정식 씬은 AppRoot 전역 EventSystem만 사용해야 합니다.");

                AssertReference(rootData, "controller");
                AssertReference(controllerData, "themeRules");
                AssertReference(controllerData, "visualSet");
                AssertReference(controllerData, "turretAttackCatalog");
                AssertReference(controllerData, "stageAnchor");
                AssertReference(controllerData, "deploymentCamera");
                AssertReference(controllerData, "cameraController");
                AssertReference(controllerData, "poolScope");
                AssertReference(controllerData, "sfxPool");
                AssertReference(controllerData, "combatFeedback");
                AssertReference(controllerData, "deploymentText");
                AssertReference(controllerData, "statusText");
                AssertReference(controllerData, "castleInfoText");
                AssertReference(controllerData, "aiDescriptionPanel");
                AssertReference(controllerData, "aiDescriptionText");
                AssertReference(controllerData, "regenerateCastleButton");
                AssertReference(controllerData, "rotateCameraLeftButton");
                AssertReference(controllerData, "rotateCameraRightButton");
                AssertReference(controllerData, "exitButton");
                AssertReference(controllerData, "inputSurface");
                AssertReference(inputData, "cameraController");
                var unitSlotCount = controllerData.FindProperty("unitButtons").arraySize;
                Assert.That(unitSlotCount, Is.EqualTo(10));
                Assert.That(controllerData.FindProperty("unitButtonLabels").arraySize, Is.EqualTo(unitSlotCount));
                Assert.That(controllerData.FindProperty("unitAiTagButtons").arraySize, Is.EqualTo(unitSlotCount));
                Assert.That(controllerData.FindProperty("unitAiTagLabels").arraySize, Is.EqualTo(unitSlotCount));
                Assert.That(controllerData.FindProperty("difficultyButtons").arraySize, Is.EqualTo(10));
                Assert.That(productionHud.GetComponentsInChildren<Button>(false)
                    .Count(value => value.name == "AITag"), Is.EqualTo(8));
                Assert.That(controllerData.FindProperty("difficultyLevel").intValue, Is.EqualTo(4));
                Assert.That(controllerData.FindProperty("generationSeed").intValue, Is.EqualTo(10801));
                Assert.That(camera.orthographic, Is.False);
                var stageMap = roots.Single(value => value.name == "PF_StageMap_hex1");
                Assert.That(stageMap.activeSelf, Is.True);
                Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(stageMap),
                    Is.EqualTo(HexCastleProductionSceneSetupUtility.StageMapPrefabPath));
                Assert.That(stageMap.transform.position,
                    Is.EqualTo(new Vector3(0f, -4.34f, 4.73f)));
                Assert.That(stageMap.transform.rotation, Is.EqualTo(Quaternion.identity));
                Assert.That(stageMap.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(productionHud.GetComponentsInChildren<Button>(false)
                    .Count(value => value.name == "RotateCameraLeftButton" || value.name == "RotateCameraRightButton"),
                    Is.EqualTo(2));
                Assert.That(productionHud.GetComponentsInChildren<HexCastleCameraHoldButton>(false).Length,
                    Is.EqualTo(2));
                var combatFeedback = roots.SelectMany(value =>
                        value.GetComponentsInChildren<CombatFeedbackPlayer>(true))
                    .Single();
                var floatingNumbers = combatFeedback.GetComponent<FloatingNumberPresenter>();
                Assert.That(floatingNumbers, Is.Not.Null);
                var feedbackData = new SerializedObject(combatFeedback);
                var floatingData = new SerializedObject(floatingNumbers);
                AssertReference(feedbackData, "floatingNumbers");
                AssertReference(floatingData, "poolScope");
                AssertReference(floatingData, "numberPrefab");
                AssertReference(floatingData, "worldCamera");
                var generationControls = productionHud.GetComponentsInChildren<Transform>(true)
                    .Single(value => value.name == "GenerationControls");
                Assert.That(generationControls.gameObject.activeSelf, Is.True);
                Assert.That(Enumerable.Range(1, 10)
                    .All(level => generationControls.Find($"Difficulty{level}Button")?.GetComponent<Button>() != null),
                    Is.True);
                Assert.That(generationControls.Find("RegenerateCastleButton")?.GetComponent<Button>(), Is.Not.Null);
                Assert.That(inputSurface, Is.InstanceOf<IPointerDownHandler>());
                Assert.That(inputSurface, Is.InstanceOf<IPointerUpHandler>());
                Assert.That(inputSurface, Is.InstanceOf<IDragHandler>());
                Assert.That(inputSurface, Is.InstanceOf<IScrollHandler>());
                Assert.That(scene.isDirty, Is.False);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void MainBattleScene_HasNoLegacyGridModeDialog()
        {
            var scene = EditorSceneManager.OpenPreviewScene(HexCastleProductionSceneSetupUtility.MainBattleScenePath);
            try
            {
                var roots = scene.GetRootGameObjects();
                var sceneRoot = roots.SelectMany(value => value.GetComponentsInChildren<MainBattleSceneRoot>(true))
                    .Single();
                var rootData = new SerializedObject(sceneRoot);
                Assert.That(rootData.FindProperty("castleRaidGridModeDialog"), Is.Null);
                Assert.That(roots.SelectMany(value => value.GetComponentsInChildren<Transform>(true))
                    .Any(value => value.name == "CastleRaidGridModeDialog"), Is.False);
                Assert.That(scene.isDirty, Is.False);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void AssertReference(SerializedObject serialized, string propertyName)
        {
            var property = serialized.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            Assert.That(property.objectReferenceValue, Is.Not.Null, propertyName);
        }
    }
}
