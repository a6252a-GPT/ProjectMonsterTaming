using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Tests.EditMode
{
    public sealed class UiFormalizationV1Tests // 버전 1 공용 창과 실사용 프리팹 연결 검사
    {
        private const string StandardRoot = "Assets/ProjectMT/02_Shared/UI/Prefabs/Standard";
        private const string VerticalPath = StandardRoot + "/PF_UIStandard_PopupVertical.prefab";
        private const string MediumPath = StandardRoot + "/PF_UIStandard_PopupMedium.prefab";
        private const string WidePath = StandardRoot + "/PF_UIStandard_PopupWide.prefab";
        private const string ContentEntryPath = StandardRoot + "/PF_UIStandard_ContentEntry.prefab";
        private const string CompactDialogPath = StandardRoot + "/PF_UIStandard_CompactDialog.prefab";
        private const string CardFramePath =
            "Assets/ThirdParty/08_UI/GUI Pro - Minimal Game Dark/GUI Pro-MinimalGame/Theme_Dark/Prefabs/Prefabs_Frame/CardFrame/CardFrame_04_BasePrefab_LightBg.prefab";
        private const string GrowthDungeonPath =
            "Assets/ProjectMT/03_Features/GrowthDungeon/Prefabs/PF_GrowthDungeonPage.prefab";
        private const string EquipmentPath =
            "Assets/ProjectMT/03_Features/Equipment/Prefabs/PF_CommanderEquipmentPage.prefab";
        private const string MonsterPath =
            "Assets/ProjectMT/03_Features/Formation/Prefabs/PF_MonsterManagementPage.prefab";
        private const string FormationPath =
            "Assets/ProjectMT/03_Features/Formation/Prefabs/PF_FormationPage.prefab";
        private const string CommanderPath =
            "Assets/ProjectMT/03_Features/Commander/Prefabs/PF_CommanderGrowthPage.prefab";
        private const string ManagementPath =
            "Assets/ProjectMT/02_Shared/UI/Prefabs/PF_ManagementUI.prefab";
        private const string FinishFeedbackPath =
            "Assets/ProjectMT/01_Core/Bootstrap/Prefabs/PF_ContentFinishFeedback.prefab";
        private const string GachaResultItemPath =
            "Assets/ProjectMT/03_Features/Shop/Prefabs/PF_GachaResultItem.prefab";
        private const string MonsterCardPath =
            "Assets/ProjectMT/03_Features/Formation/Prefabs/PF_MonsterCard.prefab";
        private const string MainBattleScenePath =
            "Assets/ProjectMT/00_Scenes/01_MainBattle.unity";
        [Test]
        public void StandardPopups_UseOfficialVersionOneFrameAndSizes()
        {
            var standards = new[]
            {
                new StandardExpectation(VerticalPath, new Vector2(820f, 960f)),
                new StandardExpectation(MediumPath, new Vector2(1240f, 960f)),
                new StandardExpectation(WidePath, new Vector2(1600f, 960f)),
                new StandardExpectation(ContentEntryPath, new Vector2(780f, 780f)),
                new StandardExpectation(CompactDialogPath, new Vector2(700f, 360f))
            };

            foreach (var expectation in standards)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(expectation.Path);
                Assert.That(prefab, Is.Not.Null, expectation.Path);
                var actualSize = prefab.GetComponent<RectTransform>().rect.size;
                Assert.That(actualSize.x, Is.EqualTo(expectation.Size.x).Within(0.001f), expectation.Path);
                Assert.That(actualSize.y, Is.EqualTo(expectation.Size.y).Within(0.001f), expectation.Path);
                var frame = prefab.transform.Find("FrameVisual_GUIPro");
                Assert.That(frame, Is.Not.Null, expectation.Path);
                Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(frame.gameObject),
                    Is.EqualTo(CardFramePath), expectation.Path);
                Assert.That(frame.gameObject.activeSelf, Is.True, expectation.Path);
                var blockingGraphics = frame.GetComponentsInChildren<Graphic>(true)
                    .Where(x => x.raycastTarget)
                    .Select(x => AnimationUtility.CalculateTransformPath(x.transform, prefab.transform))
                    .ToArray();
                Assert.That(blockingGraphics, Is.Empty,
                    expectation.Path + " 장식 프레임이 기능 버튼 입력을 가로막으면 안 됩니다.");
                var legacyTitlePlate = prefab.transform.Find("HeaderRoot/TitlePlate_GUIPro");
                if (legacyTitlePlate != null)
                    Assert.That(legacyTitlePlate.gameObject.activeSelf, Is.False, expectation.Path);
                Assert.That(CountMissingScripts(prefab), Is.Zero, expectation.Path);
            }

            AssertTitle(VerticalPath, "HeaderRoot/TitleText", 28f);
            AssertTitle(MediumPath, "HeaderRoot/TitleText", 26f);
            AssertTitle(WidePath, "HeaderRoot/TitleText", 30f);
            AssertTitle(ContentEntryPath, "HeaderRoot/TitleText", 28f);
            AssertTitle(CompactDialogPath, "FrameVisual_GUIPro/Text_Title", 28f);
        }

        [Test]
        public void NewDesignPages_AreVariantsOfOfficialStandards()
        {
            AssertVariantBase(GrowthDungeonPath, VerticalPath);
            AssertVariantBase(EquipmentPath, MediumPath);
        }

        [Test]
        public void ProductionFeaturePrefabs_InheritStandardsAndKeepReferences()
        {
            Assert.That(GetNestedStandardPaths(MonsterPath), Does.Contain(MediumPath));
            Assert.That(GetNestedStandardPaths(FormationPath), Does.Contain(WidePath));
            Assert.That(GetNestedStandardPaths(CommanderPath), Does.Contain(MediumPath));

            var commander = AssetDatabase.LoadAssetAtPath<GameObject>(CommanderPath);
            Assert.That(commander.activeSelf, Is.False);
            var scroll = commander.GetComponentsInChildren<ScrollRect>(true).Single();
            Assert.That(scroll.vertical, Is.True);
            Assert.That(scroll.horizontal, Is.False);
            Assert.That(scroll.viewport, Is.Not.Null);
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            var growthRows = commander.GetComponentsInChildren<RectTransform>(true)
                .Where(x => x.name.StartsWith("GrowthRow_", StringComparison.Ordinal)).ToArray();
            Assert.That(growthRows, Has.Length.EqualTo(7));
            foreach (var row in growthRows)
            {
                Assert.That(row.rect.height, Is.EqualTo(96f).Within(0.01f), row.name);
                Assert.That(row.GetComponentInChildren<Button>(true).GetComponent<RectTransform>().rect.size,
                    Is.EqualTo(new Vector2(134f, 68f)), row.name);
                Assert.That(row.Find("Text"), Is.Not.Null, row.name);
                Assert.That(row.Find("Text_Value"), Is.Not.Null, row.name);
            }
            AssertTitle(CommanderPath, "CommanderGrowthWindow/HeaderRoot/TitleText", 26f);
            AssertReferences(
                FindBehaviour(commander, "GrowthCalculator"),
                "healthButton", "healthLevelText", "attackButton", "attackLevelText",
                "defenseButton", "defenseLevelText", "attackSpeedButton", "attackSpeedLevelText",
                "moveSpeedButton", "moveSpeedLevelText", "attackRangeButton", "attackRangeLevelText");
            AssertReferences(
                FindBehaviour(commander, "CurrentStatsView"),
                "growthCalculator", "healthText", "attackText", "defenseText",
                "attackRangeText", "attackSpeedText", "moveSpeedText");
            Assert.That(new SerializedObject(FindBehaviour(commander, "CurrentStatsView"))
                .FindProperty("valueOnly").boolValue, Is.True);
            AssertReferences(FindBehaviour(commander, "CommanderGrowthPageView"), "closeButton");

            var monster = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPath);
            Assert.That(monster.GetComponentsInChildren<Transform>(true)
                .Count(x => x.name == "CardBorder"), Is.EqualTo(6));
            AssertReferences(
                FindBehaviour(monster, "MonsterManagementPageController"),
                "closeButton", "growthTabButton", "breakthroughTabButton",
                "healthStatLabel", "attackSpeedStatLabel", "attackStatLabel",
                "criticalStatLabel", "defenseStatLabel", "moveSpeedStatLabel", "levelUpButton");
            AssertTitle(MonsterPath, "MediumShell/HeaderRoot/TitleText", 26f);

            var equipment = AssetDatabase.LoadAssetAtPath<GameObject>(EquipmentPath);
            Assert.That(equipment.activeSelf, Is.False);
            Assert.That(equipment.GetComponentsInChildren<Transform>(true)
                .Count(x => x.name == "CardBorder"), Is.EqualTo(6));
            Assert.That(equipment.GetComponentsInChildren<Button>(true), Has.Length.EqualTo(1),
                "장비 시스템 구현 전에는 닫기 버튼만 동작해야 합니다.");
            AssertTitle(EquipmentPath, "HeaderRoot/TitleText", 26f);

            var growthDungeon = AssetDatabase.LoadAssetAtPath<GameObject>(GrowthDungeonPath);
            Assert.That(growthDungeon.activeSelf, Is.False);
            var growthDungeonButtons = growthDungeon.GetComponentsInChildren<Button>(true);
            var foodRiotEnter = growthDungeonButtons.Single(x =>
                x.name == "EnterButton_GUIPro" &&
                AnimationUtility.CalculateTransformPath(x.transform, growthDungeon.transform)
                    .Contains("FoodRiotCardSlot"));
            Assert.That(foodRiotEnter.interactable, Is.True);
            foreach (var button in growthDungeonButtons.Where(x =>
                         x.name != "CloseTouchArea_80x80" && x != foodRiotEnter))
                Assert.That(button.interactable, Is.False,
                    AnimationUtility.CalculateTransformPath(button.transform, growthDungeon.transform));

            AssertTitle(FormationPath, "PageRoot/WideShell/HeaderRoot/TitleText", 30f);

            var management = AssetDatabase.LoadAssetAtPath<GameObject>(ManagementPath);
            Assert.That(GetNestedPrefabPaths(management), Does.Contain(EquipmentPath));
            Assert.That(GetNestedPrefabPaths(management), Does.Contain(GrowthDungeonPath));
            AssertReferences(
                FindBehaviour(management, "MainBattleManagementUiController"),
                "commanderGrowthButton", "shopButton", "monsterManagementButton",
                "equipmentButton", "growthDungeonButton",
                "commanderGrowthPage", "shopPage", "shopCloseButton", "monsterManagementPage",
                "equipmentPage", "equipmentCloseButton",
                "growthDungeonPage", "growthDungeonCloseButton");

            foreach (var path in new[]
                     {
                         MonsterPath, FormationPath, CommanderPath, EquipmentPath,
                         GrowthDungeonPath, ManagementPath
                     })
                Assert.That(CountMissingScripts(AssetDatabase.LoadAssetAtPath<GameObject>(path)), Is.Zero, path);
        }

        [Test]
        public void FinishFeedback_UsesCompactVersionOneWithoutLosingPresenterReferences()
        {
            Assert.That(GetNestedStandardPaths(FinishFeedbackPath), Does.Contain(CompactDialogPath));
            var compactDialog = AssetDatabase.LoadAssetAtPath<GameObject>(CompactDialogPath);
            var retryButton = compactDialog.transform.Find("RetryButton")?.GetComponent<Button>();
            Assert.That(retryButton, Is.Not.Null);
            Assert.That(retryButton.targetGraphic, Is.Not.Null);
            Assert.That(retryButton.targetGraphic.raycastTarget, Is.True,
                "재시도 버튼은 전면 입력 차단창 안에서도 실제 EventSystem 입력을 받아야 합니다.");
            var feedback = AssetDatabase.LoadAssetAtPath<GameObject>(FinishFeedbackPath);
            AssertReferences(
                FindBehaviour(feedback, "ContentFinishFeedbackPresenter"),
                "panelRoot", "titleText", "messageText", "savingVisual", "failedVisual", "retryButton");
            Assert.That(CountMissingScripts(feedback), Is.Zero);
        }

        [Test]
        public void GachaResultItem_ReusesUniversalMonsterCardPrefab()
        {
            var result = AssetDatabase.LoadAssetAtPath<GameObject>(GachaResultItemPath);
            Assert.That(GetNestedPrefabPaths(result), Does.Contain(MonsterCardPath));
            AssertReferences(
                FindBehaviour(result, "GachaResultItemView"),
                "monsterCard", "nameText", "rarityText", "countText", "newBadge",
                "cardName", "levelBadge", "assignmentBadge", "breakthroughReadyBadge");
            Assert.That(CountMissingScripts(result), Is.Zero);
        }

        [Test]
        public void StageEntryPopups_AreContentEntryVariants()
        {
            foreach (var path in new[]
                     {
                         "Assets/ProjectMT/02_Shared/UI/Prefabs/PF_GrowthDungeonStageEntryPopup_AncientGuardianTree.prefab",
                         "Assets/ProjectMT/02_Shared/UI/Prefabs/PF_GrowthDungeonStageEntryPopup_FoodRiot.prefab",
                         "Assets/ProjectMT/02_Shared/UI/Prefabs/PF_GrowthDungeonStageEntryPopup_GiantSpellbook.prefab",
                         "Assets/ProjectMT/02_Shared/UI/Prefabs/PF_GrowthDungeonStageEntryPopup_TreasureSpirit.prefab"
                     })
                AssertVariantBase(path, ContentEntryPath);
        }

        [Test]
        public void MainBattle_GrowthDungeonOwnsFoodRiotEntry()
        {
            var scene = EditorSceneManager.OpenScene(MainBattleScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var sceneRoot = roots.SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true))
                    .FirstOrDefault(x => x != null && x.GetType().Name == "MainBattleSceneRoot");
                Assert.That(sceneRoot, Is.Not.Null);

                var foodRiotButton = new SerializedObject(sceneRoot)
                    .FindProperty("foodRiotButton").objectReferenceValue as Button;
                Assert.That(foodRiotButton, Is.Not.Null);
                var foodRiotPath = AnimationUtility.CalculateTransformPath(
                    foodRiotButton.transform, sceneRoot.transform);
                Assert.That(foodRiotPath, Does.Contain("PF_GrowthDungeonPage"));
                Assert.That(foodRiotPath, Does.Contain("FoodRiotCardSlot"));
                Assert.That(foodRiotButton.name, Is.EqualTo("EnterButton_GUIPro"));

                var legacyDirectButton = roots.SelectMany(x => x.GetComponentsInChildren<Button>(true))
                    .Single(x => x.name == "FoodRiotButton");
                Assert.That(legacyDirectButton.gameObject.activeSelf, Is.False,
                    "기존 직행 버튼과 정식 성장 던전 버튼이 겹치면 안 됩니다.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AssertVariantBase(string variantPath, string expectedBasePath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
            Assert.That(prefab, Is.Not.Null, variantPath);
            Assert.That(PrefabUtility.GetPrefabAssetType(prefab), Is.EqualTo(PrefabAssetType.Variant), variantPath);
            var source = PrefabUtility.GetCorrespondingObjectFromSource(prefab);
            Assert.That(AssetDatabase.GetAssetPath(source), Is.EqualTo(expectedBasePath), variantPath);
            Assert.That(CountMissingScripts(prefab), Is.Zero, variantPath);
        }

        private static IReadOnlyCollection<string> GetNestedStandardPaths(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return GetNestedPrefabPaths(prefab)
                .Where(x => x.StartsWith(StandardRoot, StringComparison.Ordinal))
                .ToArray();
        }

        private static IReadOnlyCollection<string> GetNestedPrefabPaths(GameObject prefab)
        {
            return prefab.GetComponentsInChildren<Transform>(true)
                .Where(x => PrefabUtility.IsAnyPrefabInstanceRoot(x.gameObject))
                .Select(x => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(x.gameObject))
                .Distinct()
                .ToArray();
        }

        private static void AssertTitle(string prefabPath, string objectPath, float fontSize)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var titleObject = prefab.transform.Find(objectPath);
            Assert.That(titleObject, Is.Not.Null, prefabPath + " :: " + objectPath);
            var title = titleObject.GetComponent("TextMeshProUGUI");
            Assert.That(title, Is.Not.Null, prefabPath + " :: " + objectPath);
            var serialized = new SerializedObject(title);
            Assert.That(serialized.FindProperty("m_fontSize").floatValue,
                Is.EqualTo(fontSize).Within(0.01f), prefabPath);
            Assert.That(titleObject.gameObject.activeSelf, Is.True, prefabPath);
        }

        private static MonoBehaviour FindBehaviour(GameObject root, string typeName)
        {
            return root.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(x => x != null && x.GetType().Name == typeName);
        }

        private static void AssertReferences(MonoBehaviour behaviour, params string[] propertyNames)
        {
            Assert.That(behaviour, Is.Not.Null);
            var serialized = new SerializedObject(behaviour);
            foreach (var propertyName in propertyNames)
            {
                var property = serialized.FindProperty(propertyName);
                Assert.That(property, Is.Not.Null, propertyName);
                Assert.That(property.objectReferenceValue, Is.Not.Null, propertyName);
            }
        }

        private static int CountMissingScripts(GameObject root)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Sum(x => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(x.gameObject));
        }

        private readonly struct StandardExpectation
        {
            public StandardExpectation(string path, Vector2 size)
            {
                Path = path;
                Size = size;
            }

            public string Path { get; }
            public Vector2 Size { get; }
        }
    }
}
