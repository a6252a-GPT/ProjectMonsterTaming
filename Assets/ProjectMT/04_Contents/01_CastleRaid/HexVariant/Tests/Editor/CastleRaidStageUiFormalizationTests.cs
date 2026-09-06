using System.Linq;
using NUnit.Framework;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Items;
using ProjectMT.Shared.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Contents.CastleRaidHex.Editor.Tests
{
    public sealed class CastleRaidStageUiFormalizationTests
    {
        private const string PagePrefabPath =
            "Assets/ProjectMT/03_Features/MainBattle/Prefabs/PF_CastleRaidStageSelectionPage.prefab";
        private const string HudPrefabPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Prefabs/PF_CastleRaidHexHUD.prefab";
        private const string MainBattleScenePath =
            "Assets/ProjectMT/00_Scenes/01_MainBattle.unity";
        private const string CastleRaidScenePath =
            "Assets/ProjectMT/00_Scenes/03_CastleRaidHex.unity";
        private const string DevUiScenePath =
            "Assets/ProjectMT/00_Scenes/DEV_UIManagement.unity";
        private const string StandardMediumPath =
            "Assets/ProjectMT/02_Shared/UI/Prefabs/Standard/PF_UIStandard_PopupMedium.prefab";
        private const string DiamondItemDefinitionPath =
            "Assets/ProjectMT/02_Shared/Items/Data/Definitions/Currency/Item_Currency_Diamond.asset";
        private const string SummonTicketItemDefinitionPath =
            "Assets/ProjectMT/02_Shared/Items/Data/Definitions/SummonTicket/Item_Ticket_MonsterSummon.asset";

        [Test]
        public void StagePage_IsAContinuousOneHundredStageRewardList()
        {
            var page = AssetDatabase.LoadAssetAtPath<GameObject>(PagePrefabPath);
            Assert.That(page, Is.Not.Null);
            var controller = page.GetComponent<CastleRaidStageSelectionController>();
            Assert.That(controller, Is.Not.Null);
            var serialized = new SerializedObject(controller);

            AssertArray(serialized, "stageButtons", 100);
            AssertArray(serialized, "stageNumberLabels", 100);
            AssertArray(serialized, "stageRewardLabels", 100);
            AssertArray(serialized, "stageStateLabels", 100);
            AssertReference(serialized, "stageScrollRect");
            AssertReference(serialized, "enterButton");
            AssertReference(serialized, "closeButton");
            AssertReference(serialized, "progressFill");

            var medium = page.transform.Find("MediumShell");
            Assert.That(medium, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(
                    PrefabUtility.GetCorrespondingObjectFromSource(medium.gameObject)),
                Is.EqualTo(StandardMediumPath),
                "군단의 역습은 공용 중형 백패널을 중첩해야 합니다.");
            Assert.That(medium.GetComponent<RectTransform>().sizeDelta,
                Is.EqualTo(new Vector2(1240f, 960f)));
            Assert.That(page.GetComponent<UIPanelPopAnimator>(), Is.Not.Null);
            Assert.That(page.transform.Find("InputBlocker"), Is.Not.Null);

            var transforms = page.GetComponentsInChildren<RectTransform>(true);
            var stageRows = transforms.Where(value => value.name.StartsWith("StageButton_")).ToArray();
            var frontBands = transforms.Where(value => value.name.StartsWith("FrontBand_")).ToArray();
            Assert.That(stageRows.Length, Is.EqualTo(100));
            Assert.That(frontBands.Length, Is.EqualTo(10));
            Assert.That(stageRows.All(value =>
                    Mathf.Approximately(value.anchorMin.y, 1f) &&
                    Mathf.Approximately(value.anchorMax.y, 1f)),
                Is.True,
                "스테이지 행은 긴 목록 Content의 상단을 기준으로 배치되어야 합니다.");
            Assert.That(frontBands.All(value =>
                    Mathf.Approximately(value.anchorMin.y, 1f) &&
                    Mathf.Approximately(value.anchorMax.y, 1f)),
                Is.True,
                "전선 구간 헤더는 긴 목록 Content의 상단을 기준으로 배치되어야 합니다.");

            var navigation = transforms.Single(value => value.name == "NavigationArea");
            var detail = transforms.Single(value => value.name == "DetailArea");
            var divider = transforms.Single(value => value.name == "AreaDivider_2px");
            Assert.That(navigation.anchorMax.x, Is.EqualTo(0.40f).Within(0.001f));
            Assert.That(detail.anchorMin.x, Is.EqualTo(0.40f).Within(0.001f));
            Assert.That(divider.anchorMin.x, Is.EqualTo(0.40f).Within(0.001f));
            Assert.That(transforms.Any(value => value.name == "AccentRail"), Is.False);
            Assert.That(transforms.Any(value => value.name == "Difficulty"), Is.False);
            Assert.That(transforms.Any(value => value.name == "SelectedDifficulty"), Is.False);

            var diamondDefinition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(DiamondItemDefinitionPath);
            var ticketDefinition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(SummonTicketItemDefinitionPath);
            Assert.That(diamondDefinition, Is.Not.Null);
            Assert.That(ticketDefinition, Is.Not.Null);
            Assert.That(diamondDefinition.Icon, Is.Not.Null);
            Assert.That(ticketDefinition.Icon, Is.Not.Null);
            var rewardIcons = page.GetComponentsInChildren<Image>(true);
            Assert.That(rewardIcons.Where(value =>
                    value.name == "DiamondIcon" || value.name == "Diamond")
                .All(value => value.sprite == diamondDefinition.Icon), Is.True,
                "다이아 보상은 인벤토리 ItemDefinition과 동일한 아이콘이어야 합니다.");
            Assert.That(rewardIcons.Where(value =>
                    value.name == "TicketIcon" || value.name == "Ticket")
                .All(value => value.sprite == ticketDefinition.Icon), Is.True,
                "소환권 보상은 인벤토리 ItemDefinition과 동일한 아이콘이어야 합니다.");

            var viewport = transforms.Single(value => value.name == "StageViewport");
            var content = transforms.Single(value => value.name == "Content");
            Assert.That(content.rect.height, Is.GreaterThan(viewport.rect.height * 10f));
            Assert.That(page.GetComponentsInChildren<Transform>(true)
                .Any(value => value.name.IndexOf("Sweep", System.StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False,
                "군단의 역습 선택 화면에는 성장 던전식 소탕 동선이 없어야 합니다.");

            var progressFill = transforms.Single(value => value.name == "ProgressFill");
            Assert.That(progressFill.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(progressFill.anchorMax.x, Is.EqualTo(0.26f).Within(0.001f));
            Assert.That(
                transforms.Count(value => value.name == "Reward"),
                Is.EqualTo(100));
        }

        [Test]
        public void MainBattleAndDevUiScenes_ContainTheSameStagePageContract()
        {
            var mainScene = EditorSceneManager.OpenPreviewScene(MainBattleScenePath);
            try
            {
                var page = mainScene.GetRootGameObjects()
                    .SelectMany(value =>
                        value.GetComponentsInChildren<CastleRaidStageSelectionController>(true))
                    .Single();
                Assert.That(page.gameObject.activeSelf, Is.False);
                Assert.That(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(page.gameObject),
                    Is.EqualTo(PagePrefabPath));
                Assert.That(mainScene.isDirty, Is.False);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(mainScene);
            }

            var devScene = EditorSceneManager.OpenPreviewScene(DevUiScenePath);
            try
            {
                var slot = devScene.GetRootGameObjects()
                    .SelectMany(value => value.GetComponentsInChildren<Transform>(true))
                    .Single(value => value.name == "Slot_PF_CastleRaidStageSelectionPage");
                var preview = slot.GetComponentInChildren<CastleRaidStageSelectionController>(true);
                Assert.That(preview, Is.Not.Null);
                Assert.That(preview.gameObject.activeSelf, Is.True);
                Assert.That(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(preview.gameObject),
                    Is.EqualTo(PagePrefabPath));
                Assert.That(devScene.isDirty, Is.False);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(devScene);
            }
        }

        [Test]
        public void ProductionHud_UsesTenPartySlotsAndPolishedControlGroups()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.GetComponent<Canvas>(), Is.Not.Null);
            Assert.That(
                Enumerable.Range(1, 10).All(index =>
                    hud.GetComponentsInChildren<Button>(true)
                        .Any(value => value.name == $"UnitButton_{index}")),
                Is.True);
            Assert.That(
                Enumerable.Range(1, 10).All(index =>
                    hud.GetComponentsInChildren<Button>(true)
                        .Any(value => value.name == $"Difficulty{index}Button")),
                Is.True);
            Assert.That(hud.GetComponentsInChildren<Transform>(true)
                .Any(value => value.name == "BottomDeploymentDock"), Is.True);
            Assert.That(hud.GetComponentsInChildren<Transform>(true)
                .Any(value => value.name == "StageCard"), Is.True);
            Assert.That(hud.GetComponentsInChildren<Transform>(true)
                .Any(value => value.name == "StatusPanel"), Is.True);

            var scene = EditorSceneManager.OpenPreviewScene(CastleRaidScenePath);
            try
            {
                var sceneHuds = scene.GetRootGameObjects()
                    .SelectMany(value => value.GetComponentsInChildren<Canvas>(true))
                    .Select(value => value.gameObject)
                    .ToArray();
                var productionHuds = sceneHuds
                    .Where(value => value.name.StartsWith(
                        "PF_CastleRaidHexHUD",
                        System.StringComparison.Ordinal))
                    .ToArray();
                var legacyHuds = sceneHuds
                    .Where(value => value.name == "PF_CastleRaidHUD")
                    .ToArray();
                Assert.That(productionHuds, Has.Length.EqualTo(1),
                    "03_CastleRaidHex에는 정식 HUD 인스턴스가 정확히 하나만 있어야 합니다.");
                Assert.That(productionHuds[0].activeSelf, Is.True,
                    "정식 PF_CastleRaidHexHUD는 활성 상태여야 합니다.");
                Assert.That(legacyHuds, Has.Length.EqualTo(1),
                    "구형 PF_CastleRaidHUD는 비교·복구용으로 한 개 보존해야 합니다.");
                Assert.That(legacyHuds[0].activeSelf, Is.False,
                    "구형 PF_CastleRaidHUD는 비활성 상태로만 보존해야 합니다.");
                Assert.That(scene.GetRootGameObjects()
                        .SelectMany(value =>
                            value.GetComponentsInChildren<HexCastleDeploymentInputSurface>(true))
                        .Where(value => value.gameObject.activeInHierarchy)
                        .Count(),
                    Is.EqualTo(1),
                    "활성 배치 입력 표면은 정식 HUD의 한 개만 존재해야 합니다.");
                var controller = scene.GetRootGameObjects()
                    .SelectMany(value => value.GetComponentsInChildren<HexCastleRaidController>(true))
                    .Single();
                var serialized = new SerializedObject(controller);
                AssertArray(serialized, "unitButtons", 10);
                AssertArray(serialized, "unitButtonLabels", 10);
                AssertArray(serialized, "unitAiTagButtons", 10);
                AssertArray(serialized, "unitAiTagLabels", 10);
                AssertArray(serialized, "difficultyButtons", 10);
                Assert.That(scene.isDirty, Is.False);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void EveryProgressionStage_GeneratesAValidDifficultyMappedFortress()
        {
            var rules = AssetDatabase.LoadAssetAtPath<HexCastleThemeOneRules>(
                "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Data/Foundation/" +
                "HexCastleTheme1Rules.asset");
            Assert.That(rules, Is.Not.Null);
            var pipeline = new HexCastleGenerationPipeline();
            var themes = HexCastleThemeCatalog.Themes;

            for (var stage = CastleRaidStageRules.MinimumStage;
                 stage <= CastleRaidStageRules.MaximumStage;
                 stage++)
            {
                var difficulty = CastleRaidStageRules.ResolveDifficulty(stage);
                var theme = themes[(stage - 1) % themes.Count];
                var candidate = pipeline.GenerateFoundationForDifficulty(
                    CastleRaidStageRules.ResolveGenerationSeed(stage),
                    difficulty,
                    theme,
                    rules.Tuning);

                Assert.That(
                    candidate.Validation.IsValid,
                    Is.True,
                    $"stage {stage}: {string.Join(" | ", candidate.Validation.Errors)}");
                Assert.That(candidate.Layout.DifficultyLevel, Is.EqualTo(difficulty), $"stage {stage}");
                Assert.That(candidate.Layout.Theme, Is.EqualTo(theme), $"stage {stage}");
            }
        }

        private static void AssertArray(SerializedObject serialized, string propertyName, int count)
        {
            var property = serialized.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            Assert.That(property.arraySize, Is.EqualTo(count), propertyName);
            for (var index = 0; index < property.arraySize; index++)
            {
                Assert.That(
                    property.GetArrayElementAtIndex(index).objectReferenceValue,
                    Is.Not.Null,
                    $"{propertyName}[{index}]");
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
