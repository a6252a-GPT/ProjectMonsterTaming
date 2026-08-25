using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Contents.Framework;
using ProjectMT.Core.Config;
using ProjectMT.Core.SceneFlow;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Pooling;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    public static class HexCastleProductionSceneSetupUtility
    {
        public const string HexScenePath = "Assets/ProjectMT/00_Scenes/03_CastleRaidHex.unity";
        public const string MainBattleScenePath = "Assets/ProjectMT/00_Scenes/01_MainBattle.unity";
        public const string SceneCatalogPath = "Assets/ProjectMT/01_Core/Config/SceneCatalog.asset";
        public const string ContentDefinitionPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/Data/ContentDefinition_CastleRaid.asset";
        public const string HexRulesPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Data/Foundation/HexCastleTheme1Rules.asset";
        public const string HudPrefabPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/Prefabs/PF_CastleRaidHUD.prefab";
        public const string FloatingNumberPrefabPath =
            "Assets/ProjectMT/02_Shared/Combat/Prefabs/PF_FloatingNumber.prefab";

        [MenuItem("JC Tool/군단의 역습 육각/정식 씬·MainBattle 연결")]
        public static void BuildAndConnect()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || string.IsNullOrWhiteSpace(activeScene.path))
            {
                throw new InvalidOperationException("저장된 기준 씬에서 실행해야 합니다.");
            }

            if (activeScene.isDirty)
            {
                throw new InvalidOperationException(
                    $"현재 씬이 수정 상태라 작업을 중단했습니다: {activeScene.path}");
            }

            var returnScenePath = activeScene.path;
            try
            {
                UpdateContentAndSceneCatalogs();
                BuildHexScene();
                ConnectMainBattleDialog();
                UpdateBuildSettings();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("[Hex Formalization] 정식 Hex 씬·MainBattle 전장 선택·Catalog·Build Settings 연결 완료");
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(returnScenePath) &&
                    !string.Equals(SceneManager.GetActiveScene().path, returnScenePath, StringComparison.Ordinal))
                {
                    EditorSceneManager.OpenScene(returnScenePath, OpenSceneMode.Single);
                }
            }
        }

        public static void RunOnceFromCommandLine()
        {
            BuildAndConnect();
        }

        [MenuItem("JC Tool/군단의 역습 육각/절차 생성 씬만 갱신")]
        public static void RebuildHexSceneOnly()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || string.IsNullOrWhiteSpace(activeScene.path) || activeScene.isDirty)
            {
                throw new InvalidOperationException("저장된 깨끗한 씬에서 실행해야 합니다.");
            }

            var returnScenePath = activeScene.path;
            try
            {
                BuildHexScene();
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.OpenScene(returnScenePath, OpenSceneMode.Single);
            }
        }

        private static void UpdateContentAndSceneCatalogs()
        {
            var definition = LoadRequired<ContentDefinition>(ContentDefinitionPath);
            definition.EditorSetSceneVariants(new[]
            {
                new ContentSceneVariant(
                    CastleRaidGridModeDialog.SquareVariant,
                    new SceneId("castle_raid")),
                new ContentSceneVariant(
                    CastleRaidGridModeDialog.HexVariant,
                    new SceneId("castle_raid_hex"))
            });
            EditorUtility.SetDirty(definition);

            var catalog = LoadRequired<SceneCatalog>(SceneCatalogPath);
            var entries = catalog.Entries
                .Where(value => value != null && value.SceneId != new SceneId("castle_raid_hex"))
                .ToList();
            entries.Add(new SceneEntry(
                new SceneId("castle_raid_hex"),
                HexScenePath,
                SceneKind.SeparateContent));
            catalog.EditorSetEntries(entries);
            EditorUtility.SetDirty(catalog);
        }

        private static void BuildHexScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("00_SceneRoot");
            var sceneRoot = root.AddComponent<HexCastleRaidSceneRoot>();
            var controller = root.AddComponent<HexCastleRaidController>();

            var runtimeRoot = CreateChild("01_RuntimeRoot", root.transform);
            var stageAnchor = CreateChild("StageAnchor", runtimeRoot.transform).transform;
            var poolScope = CreateChild("ScenePool", runtimeRoot.transform).AddComponent<ScenePoolScope>();
            var sfxPool = CreateChild("SfxPool", runtimeRoot.transform).AddComponent<SfxPool>();
            sfxPool.EditorConfigure(18, 8);

            var cameraRoot = CreateChild("02_CameraRoot", root.transform);
            var cameraObject = CreateChild("HexCastleCamera", cameraRoot.transform);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 38f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 300f;
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 24f, -24f),
                Quaternion.Euler(38f, 0f, 0f));
            var cameraController = cameraObject.AddComponent<HexCastleCameraController>();
            cameraController.EditorConfigure(10, camera);

            var combatFeedbackObject = CreateChild("CombatFeedback", runtimeRoot.transform);
            var floatingNumbers = combatFeedbackObject.AddComponent<FloatingNumberPresenter>();
            floatingNumbers.EditorConfigure(
                poolScope,
                LoadRequired<GameObject>(FloatingNumberPrefabPath),
                camera);
            var combatFeedback = combatFeedbackObject.AddComponent<CombatFeedbackPlayer>();
            combatFeedback.EditorConfigure(poolScope, null, null);
            combatFeedback.EditorConfigureExtensions(floatingNumbers, sfxPool);

            var lightingRoot = CreateChild("03_LightingRoot", root.transform);
            var sunObject = CreateChild("Directional Light", lightingRoot.transform);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.shadows = LightShadows.Soft;
            sunObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.58f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.38f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.20f, 0.19f);
            RenderSettings.ambientIntensity = 0.82f;

            var eventSystemObject = CreateChild("04_EventSystem", root.transform);
            eventSystemObject.AddComponent<EventSystem>();
            var inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();

            var hudPrefab = LoadRequired<GameObject>(HudPrefabPath);
            var hud = PrefabUtility.InstantiatePrefab(hudPrefab, scene) as GameObject ??
                      throw new InvalidOperationException("Castle Raid HUD Prefab 인스턴스 생성에 실패했습니다.");
            hud.name = "PF_CastleRaidHUD";
            hud.transform.SetParent(root.transform, false);
            var canvas = hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            var inputTransform = RequireChild(hud.transform, "DeploymentInputSurface");
            foreach (var behaviour in inputTransform.GetComponents<MonoBehaviour>())
            {
                if (behaviour != null &&
                    string.Equals(
                        behaviour.GetType().FullName,
                        "ProjectMT.Contents.CastleRaid.CastleDeploymentInputSurface",
                        StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(behaviour);
                }
            }
            var inputSurface = inputTransform.gameObject.AddComponent<HexCastleDeploymentInputSurface>();

            var buttons = new Button[HexUnitSlotCount];
            var labels = new TMP_Text[HexUnitSlotCount];
            var aiTagButtons = new Button[HexUnitSlotCount];
            var aiTagLabels = new TMP_Text[HexUnitSlotCount];
            var hudFont = hud.GetComponentInChildren<TMP_Text>(true)?.font ?? TMP_Settings.defaultFontAsset;
            for (var index = 0; index < buttons.Length; index++)
            {
                var buttonTransform = RequireChild(hud.transform, $"UnitButton_{index + 1}");
                buttons[index] = buttonTransform.GetComponent<Button>() ??
                                 throw new InvalidOperationException($"UnitButton_{index + 1} Button이 없습니다.");
                labels[index] = RequireChild(buttonTransform, "Label").GetComponent<TMP_Text>() ??
                                throw new InvalidOperationException($"UnitButton_{index + 1} Label이 없습니다.");
                aiTagButtons[index] = CreateButton(
                    "AITag",
                    (RectTransform)buttonTransform,
                    hudFont,
                    "AI",
                    new Color(0.11f, 0.44f, 0.38f));
                SetRect(
                    aiTagButtons[index].GetComponent<RectTransform>(),
                    new Vector2(0.5f, 1f),
                    new Vector2(112f, 26f),
                    new Vector2(0f, 16f));
                aiTagLabels[index] = RequireChild(aiTagButtons[index].transform, "Label")
                    .GetComponent<TMP_Text>();
                Stretch(aiTagLabels[index].rectTransform, 2f);
                aiTagLabels[index].fontSize = 14f;
                aiTagLabels[index].enableAutoSizing = true;
                aiTagLabels[index].fontSizeMin = 10f;
                aiTagLabels[index].fontSizeMax = 14f;
            }
            for (var index = HexUnitSlotCount; index < SharedHudUnitSlotCount; index++)
            {
                RequireChild(hud.transform, $"UnitButton_{index + 1}").gameObject.SetActive(false);
            }

            var generationControls = RequireChild(hud.transform, "GenerationControls");
            generationControls.gameObject.SetActive(true);
            foreach (var oldButtonName in new[] { "Difficulty2Button", "Difficulty3Button", "Difficulty4Button" })
            {
                var oldButton = generationControls.Find(oldButtonName);
                if (oldButton != null)
                {
                    Object.DestroyImmediate(oldButton.gameObject);
                }
            }

            var controlsRect = (RectTransform)generationControls;
            controlsRect.sizeDelta = new Vector2(760f, 112f);
            controlsRect.anchoredPosition = new Vector2(0f, -122f);
            var difficultyButtons = new Button[10];
            for (var index = 0; index < difficultyButtons.Length; index++)
            {
                var difficulty = index + 1;
                difficultyButtons[index] = CreateButton(
                    $"Difficulty{difficulty}Button",
                    controlsRect,
                    hudFont,
                    $"난이도\n{difficulty}",
                    Color.Lerp(
                        new Color(0.10f, 0.42f, 0.34f),
                        new Color(0.56f, 0.16f, 0.13f),
                        index / 9f));
                SetRect(
                    difficultyButtons[index].GetComponent<RectTransform>(),
                    new Vector2(0.5f, 1f),
                    new Vector2(68f, 48f),
                    new Vector2(-324f + index * 72f, -25f));
                var label = RequireChild(difficultyButtons[index].transform, "Label")
                    .GetComponent<TMP_Text>();
                label.fontSize = 15f;
                label.enableAutoSizing = true;
                label.fontSizeMin = 10f;
                label.fontSizeMax = 15f;
            }

            var regenerateCastleButton = RequireChild(generationControls, "RegenerateCastleButton")
                                             .GetComponent<Button>() ??
                                         throw new InvalidOperationException("다른 성 생성 Button이 없습니다.");
            SetRect(
                regenerateCastleButton.GetComponent<RectTransform>(),
                new Vector2(0.5f, 1f),
                new Vector2(190f, 48f),
                new Vector2(0f, -82f));

            var cameraControls = CreateUiObject("CameraRotationControls", hud.transform);
            SetRect(cameraControls, new Vector2(1f, 1f), new Vector2(300f, 58f), new Vector2(-170f, -105f));
            var rotateLeftButton = CreateButton(
                "RotateCameraLeftButton",
                cameraControls,
                hudFont,
                "좌회전",
                new Color(0.12f, 0.36f, 0.58f));
            SetRect(rotateLeftButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(140f, 54f), new Vector2(-74f, 0f));
            rotateLeftButton.gameObject.AddComponent<HexCastleCameraHoldButton>()
                .EditorConfigure(cameraController, -1);
            var rotateRightButton = CreateButton(
                "RotateCameraRightButton",
                cameraControls,
                hudFont,
                "우회전",
                new Color(0.12f, 0.36f, 0.58f));
            SetRect(rotateRightButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(140f, 54f), new Vector2(74f, 0f));
            rotateRightButton.gameObject.AddComponent<HexCastleCameraHoldButton>()
                .EditorConfigure(cameraController, 1);

            var aiDescriptionPanel = CreateUiObject("AIProfileDescriptionPanel", hud.transform);
            SetRect(
                aiDescriptionPanel,
                new Vector2(0.5f, 0f),
                new Vector2(720f, 86f),
                new Vector2(0f, 190f));
            var aiDescriptionBackground = aiDescriptionPanel.gameObject.AddComponent<Image>();
            aiDescriptionBackground.color = new Color(0.035f, 0.07f, 0.085f, 0.96f);
            aiDescriptionBackground.raycastTarget = false;
            var aiDescriptionOutline = aiDescriptionPanel.gameObject.AddComponent<Outline>();
            aiDescriptionOutline.effectColor = new Color(0.22f, 0.76f, 0.63f, 0.85f);
            aiDescriptionOutline.effectDistance = new Vector2(2f, -2f);
            var aiDescriptionText = CreateText(
                "Description",
                aiDescriptionPanel,
                hudFont,
                string.Empty,
                17f,
                FontStyles.Normal);
            Stretch(aiDescriptionText.rectTransform, 16f);
            aiDescriptionText.alignment = TextAlignmentOptions.MidlineLeft;
            aiDescriptionText.color = new Color(0.92f, 0.97f, 0.96f);
            aiDescriptionText.raycastTarget = false;
            aiDescriptionPanel.gameObject.SetActive(false);

            var rules = LoadRequired<HexCastleThemeOneRules>(HexRulesPath);
            var runtimeVisualSet = HexCastleRuntimeVisualSetAssetUtility.LoadOrCreate();
            var turretAttackCatalog = LoadRequired<HexCastleTurretAttackCatalog>(
                HexCastleTurretAttackAssetUtility.CatalogPath);
            controller.EditorConfigure(
                rules,
                runtimeVisualSet,
                turretAttackCatalog,
                stageAnchor,
                camera,
                cameraController,
                poolScope,
                sfxPool,
                combatFeedback,
                RequireChild(hud.transform, "DeploymentText").GetComponent<TMP_Text>(),
                RequireChild(hud.transform, "StatusText").GetComponent<TMP_Text>(),
                RequireChild(hud.transform, "CastleInfoText").GetComponent<TMP_Text>(),
                buttons,
                labels,
                aiTagButtons,
                aiTagLabels,
                aiDescriptionPanel.gameObject,
                aiDescriptionText,
                difficultyButtons,
                regenerateCastleButton,
                rotateLeftButton,
                rotateRightButton,
                RequireChild(hud.transform, "ExitButton").GetComponent<Button>(),
                inputSurface,
                DefaultDifficultyLevel,
                DefaultGenerationSeed);
            inputSurface.EditorConfigure(controller, cameraController);
            sceneRoot.EditorConfigure(controller);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, HexScenePath))
            {
                throw new InvalidOperationException("03_CastleRaidHex 씬 저장에 실패했습니다.");
            }
        }

        private static void ConnectMainBattleDialog()
        {
            var scene = EditorSceneManager.OpenScene(MainBattleScenePath, OpenSceneMode.Single);
            var sceneRoot = scene.GetRootGameObjects()
                .Select(value => value.GetComponentInChildren<MainBattleSceneRoot>(true))
                .FirstOrDefault(value => value != null) ??
                throw new InvalidOperationException("01_MainBattle의 MainBattleSceneRoot를 찾지 못했습니다.");
            var hud = sceneRoot.transform.Find("01_MainGameplayRoot/04_UIRoot/MainBattleHUD") ??
                      throw new InvalidOperationException("01_MainBattle의 MainBattleHUD를 찾지 못했습니다.");
            var existing = hud.Find("CastleRaidGridModeDialog");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var dialog = BuildGridModeDialog((RectTransform)hud, out var title, out var square, out var hex, out var cancel);
            dialog.EditorConfigure(dialog.gameObject, title, square, hex, cancel);
            sceneRoot.EditorConfigureCastleRaidGridModeDialog(dialog);
            dialog.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, MainBattleScenePath))
            {
                throw new InvalidOperationException("01_MainBattle 전장 선택 팝업 저장에 실패했습니다.");
            }
        }

        private static CastleRaidGridModeDialog BuildGridModeDialog(
            RectTransform parent,
            out TMP_Text title,
            out Button squareButton,
            out Button hexButton,
            out Button cancelButton)
        {
            var font = parent.GetComponentInChildren<TMP_Text>(true)?.font ?? TMP_Settings.defaultFontAsset;
            var root = CreateUiObject("CastleRaidGridModeDialog", parent);
            Stretch(root);
            var backdrop = root.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.015f, 0.025f, 0.045f, 0.80f);

            var panel = CreateUiObject("Panel", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(690f, 390f);
            panel.anchoredPosition = Vector2.zero;
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = ResolveUiSprite();
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(0.075f, 0.105f, 0.16f, 0.98f);
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.72f, 0.88f, 0.75f);
            outline.effectDistance = new Vector2(2f, -2f);

            title = CreateText("Title", panel, font, "군단의 역습 전장 선택", 34f, FontStyles.Bold);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(620f, 62f), new Vector2(0f, -54f));
            var description = CreateText(
                "Description",
                panel,
                font,
                "게임 규칙과 보상은 동일하며, 전장 그리드와 이동 AI만 달라집니다.",
                20f,
                FontStyles.Normal);
            description.color = new Color(0.76f, 0.83f, 0.90f);
            SetRect(description.rectTransform, new Vector2(0.5f, 1f), new Vector2(620f, 52f), new Vector2(0f, -112f));

            squareButton = CreateButton(
                "SquareGridButton",
                panel,
                font,
                "사각 그리드\n기존 전장",
                new Color(0.16f, 0.40f, 0.72f));
            SetRect(squareButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(270f, 122f), new Vector2(-150f, -15f));
            hexButton = CreateButton(
                "HexGridButton",
                panel,
                font,
                "육각 그리드\nTheme 1",
                new Color(0.12f, 0.58f, 0.42f));
            SetRect(hexButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(270f, 122f), new Vector2(150f, -15f));
            cancelButton = CreateButton(
                "CancelButton",
                panel,
                font,
                "취소",
                new Color(0.25f, 0.29f, 0.36f));
            SetRect(cancelButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(190f, 58f), new Vector2(0f, 48f));

            return root.gameObject.AddComponent<CastleRaidGridModeDialog>();
        }

        private static Button CreateButton(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            string label,
            Color color)
        {
            var rect = CreateUiObject(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = ResolveUiSprite();
            image.type = Image.Type.Sliced;
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            colors.pressedColor = new Color(0.76f, 0.82f, 0.88f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var text = CreateText("Label", rect, font, label, 24f, FontStyles.Bold);
            Stretch(text.rectTransform, 12f);
            return button;
        }

        private static TMP_Text CreateText(
            string name,
            RectTransform parent,
            TMP_FontAsset font,
            string value,
            float size,
            FontStyles style)
        {
            var rect = CreateUiObject(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static RectTransform CreateUiObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static Transform RequireChild(Transform parent, string path)
        {
            return parent.Find(path) ?? throw new InvalidOperationException($"필수 오브젝트가 없습니다: {path}");
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = -Vector2.one * inset;
        }

        private static Sprite ResolveUiSprite()
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        }

        private static void UpdateBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var existingIndex = scenes.FindIndex(value =>
                string.Equals(value.path, HexScenePath, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                scenes[existingIndex] = new EditorBuildSettingsScene(HexScenePath, true);
            }
            else
            {
                scenes.Add(new EditorBuildSettingsScene(HexScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static T LoadRequired<T>(string path) where T : Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path) ??
                   throw new InvalidOperationException($"필수 자산이 없습니다: {path}");
        }

        private const int DefaultDifficultyLevel = 4;
        private const int DefaultGenerationSeed = 10801;
        private const int HexUnitSlotCount = 8;
        private const int SharedHudUnitSlotCount = 10;
    }
}
