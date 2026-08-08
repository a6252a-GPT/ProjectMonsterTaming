using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectMT.Features.Expedition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectMT.Tests.EditMode
{
    public sealed class HudQuickMenuTests // 메인 전투 HUD와 확장 메뉴 정식 연결 검사
    {
        private const string HudPrefabPath =
            "Assets/ProjectMT/03_Features/MainBattle/Prefabs/PF_HudQuickMenu.prefab";
        private const string MainBattleScenePath =
            "Assets/ProjectMT/00_Scenes/01_MainBattle.unity";
        private const string BasicFramePath =
            "Assets/ThirdParty/08_UI/GUI Pro - Minimal Game Dark/GUI Pro-MinimalGame/Theme_Dark/Prefabs/Prefabs_Frame/BasicFrame/BasicFrame_Rectangle_04_Border_Dark.prefab";
        [Test]
        public void Prefab_UsesSimpleFramesAndExpectedMenuContract()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            Assert.That(prefab.transform.Cast<Transform>().Select(x => x.name), Is.EqualTo(new[]
            {
                "InputLayer", "StatusLayer", "QuickMenuRoot"
            }));

            var topLeft = prefab.transform.Find("StatusLayer/TopLeftStatus");
            var topCenter = prefab.transform.Find("StatusLayer/TopCenterStatus");
            var quickMenuRoot = prefab.transform.Find("QuickMenuRoot");
            Assert.That(topLeft, Is.Not.Null);
            Assert.That(topCenter, Is.Not.Null);
            Assert.That(quickMenuRoot, Is.Not.Null);
            Assert.That(prefab.transform.Find("StatusLayer")
                .GetComponent("MainBattleHudProgressView"), Is.Not.Null);

            AssertNestedPrefab(topLeft.Find("ProfileRoot/ProfilePanel_GUIPro"), BasicFramePath);
            AssertNestedPrefab(topCenter.Find("StageStatusRoot/StagePanel_GUIPro"), BasicFramePath);
            AssertNestedPrefab(
                quickMenuRoot.Find("ExpandedMenuRoot/MenuBackdrop_GUIPro"),
                BasicFramePath);
            var menuBackdrop = quickMenuRoot.Find("ExpandedMenuRoot/MenuBackdrop_GUIPro")
                .GetComponent<RectTransform>();
            Assert.That(menuBackdrop.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(menuBackdrop.offsetMax, Is.EqualTo(Vector2.zero));

            var fixedRow = quickMenuRoot.Find("FixedQuickRow");
            Assert.That(fixedRow, Is.Not.Null);
            var fixedLayout = fixedRow.GetComponent<HorizontalLayoutGroup>();
            Assert.That(fixedLayout, Is.Not.Null);
            Assert.That(fixedLayout.spacing, Is.EqualTo(4f));
            Assert.That(fixedLayout.padding.left, Is.EqualTo(12));
            Assert.That(fixedLayout.padding.right, Is.EqualTo(12));
            var fixedButtons = fixedRow.Cast<Transform>()
                .Select(x => x.GetComponent<Button>())
                .Where(x => x != null)
                .ToArray();
            Assert.That(fixedButtons.Select(x => x.name), Is.EqualTo(new[]
            {
                "ContentButton", "SummonButton", "ShopButton", "MenuButton"
            }));

            var expanded = quickMenuRoot.Find("ExpandedMenuRoot");
            Assert.That(expanded, Is.Not.Null);
            Assert.That(expanded.gameObject.activeSelf, Is.False);
            Assert.That(expanded.GetComponent<RectTransform>().sizeDelta.x,
                Is.EqualTo(500f).Within(0.01f));
            Assert.That(expanded.GetComponent<RectTransform>().sizeDelta.y,
                Is.EqualTo(418f).Within(0.01f));
            Assert.That(expanded.GetComponent<RectTransform>().anchoredPosition,
                Is.EqualTo(new Vector2(-28f, -28f)));
            AssertRectContains(expanded.GetComponent<RectTransform>(),
                fixedRow.GetComponent<RectTransform>());
            Assert.That(expanded.Find("MenuTitle_SettingsStyle_GUIPro"), Is.Null);
            var menuGrid = expanded.Find("MenuGrid");
            Assert.That(menuGrid, Is.Not.Null);
            Assert.That(menuGrid.GetComponent<RectTransform>().rect.size,
                Is.EqualTo(new Vector2(481.77f, 320f)));
            var gridLayout = menuGrid.GetComponent<GridLayoutGroup>();
            Assert.That(gridLayout, Is.Not.Null);
            Assert.That(gridLayout.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
            Assert.That(gridLayout.constraintCount, Is.EqualTo(4));
            Assert.That(gridLayout.cellSize, Is.EqualTo(new Vector2(112f, 88f)));
            var expandedButtons = menuGrid.Cast<Transform>()
                .Select(x => x.GetComponent<Button>())
                .Where(x => x != null)
                .ToArray();
            Assert.That(expandedButtons, Has.Length.EqualTo(12));
            Assert.That(expandedButtons.Count(x => x.interactable), Is.EqualTo(5));
            Assert.That(expandedButtons.Count(x => !x.interactable), Is.EqualTo(7));

            var outside = prefab.transform.Find("InputLayer/OutsideTapCatcher");
            Assert.That(outside, Is.Not.Null);
            Assert.That(outside.gameObject.activeSelf, Is.False);
            Assert.That(fixedRow.Find("MenuButton/Icon").gameObject.activeSelf, Is.True);
            Assert.That(fixedRow.Find("MenuButton/CloseIcon").gameObject.activeSelf, Is.False);
            var menuText = fixedRow.Find("MenuButton/Text (TMP)").GetComponent("TextMeshProUGUI");
            Assert.That(menuText, Is.Not.Null);
            Assert.That(new SerializedObject(menuText).FindProperty("m_text").stringValue,
                Is.EqualTo("메뉴"));

            var badges = fixedRow.GetComponentsInChildren<Transform>(true)
                .Concat(menuGrid.GetComponentsInChildren<Transform>(true))
                .Where(x => x.name == "NotificationBadge_INACTIVE" &&
                            x.parent != null && x.parent.GetComponent<Button>() != null)
                .ToArray();
            Assert.That(badges, Has.Length.EqualTo(16));
            Assert.That(badges.All(x => !x.gameObject.activeSelf), Is.True);

            Assert.That(fixedButtons.Sum(CountDirectLocks), Is.Zero);
            Assert.That(expandedButtons.Where(x => x.interactable).Sum(CountDirectLocks), Is.Zero);
            Assert.That(expandedButtons.Where(x => !x.interactable)
                .All(x => CountDirectLocks(x) == 1), Is.True);

            var resources = topLeft.Find("ResourceBarRoot_GUIPro");
            Assert.That(resources.GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);
            foreach (var value in resources.GetComponentsInChildren<MonoBehaviour>(true)
                         .Where(x => x != null && x.name == "Text (TMP)" &&
                                     x.GetType().Name == "TextMeshProUGUI"))
            {
                var textSerialized = new SerializedObject(value);
                Assert.That(textSerialized.FindProperty("m_enableAutoSizing").boolValue, Is.True);
                Assert.That(textSerialized.FindProperty("m_TextWrappingMode").intValue, Is.Zero);
            }

            var stage = topCenter.Find("StageStatusRoot");
            Assert.That(stage.GetComponent<RectTransform>().sizeDelta,
                Is.EqualTo(new Vector2(650f, 132f)));
            Assert.That(stage.Find("StageTitleText"), Is.Not.Null);
            Assert.That(stage.Find("StageMetaRow").GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);
            Assert.That(stage.Find("ModeButton"), Is.Not.Null);
            var progressFill = stage.Find("ProgressBarBg_GUIPro/ProgressFill_LAYOUT")
                .GetComponent<RectTransform>();
            Assert.That(progressFill, Is.Not.Null);
            Assert.That(progressFill.pivot, Is.EqualTo(new Vector2(0f, 0.5f)));
            Assert.That(progressFill.sizeDelta.x, Is.EqualTo(360f).Within(0.01f));
            Assert.That(CountMissingScripts(prefab), Is.Zero);
        }

        [Test]
        public void ExpeditionProgressFill_UsesDefeatedShareOfWholeRun()
        {
            var root = new GameObject("ExpeditionProgressTest");
            try
            {
                var controller = root.AddComponent<ExpeditionController>();
                var fill = new GameObject("ProgressFill", typeof(RectTransform))
                    .GetComponent<RectTransform>();
                fill.SetParent(root.transform, false);
                fill.sizeDelta = new Vector2(360f, 10f);
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("progressFill").objectReferenceValue = fill;
                serialized.FindProperty("progressFillMaxWidth").floatValue = 360f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                SetPrivateField(controller, "runEnemyTotalCount", 10);
                SetPrivateField(controller, "defeatedEnemyCount", 5);
                InvokePrivateMethod(controller, "UpdateHud");

                Assert.That(fill.sizeDelta.x, Is.EqualTo(180f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MainBattleScene_UsesPrefabAndKeepsEveryFunctionalReference()
        {
            var scene = EditorSceneManager.OpenScene(MainBattleScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var hud = roots.SelectMany(x => x.GetComponentsInChildren<Transform>(true))
                    .Single(x => x.name == "MainBattleHUD");
                var quickMenu = hud.Find("PF_HudQuickMenu");
                Assert.That(quickMenu, Is.Not.Null);
                Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(quickMenu.gameObject),
                    Is.EqualTo(HudPrefabPath));

                var controller = quickMenu.GetComponent<MonoBehaviour>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.GetType().Name, Is.EqualTo("HudQuickMenuController"));
                var serialized = new SerializedObject(controller);
                foreach (var propertyName in new[]
                         {
                             "managementUi", "formationPage", "shopCategoryMenu", "sceneRoot", "modeButton"
                         })
                {
                    var property = serialized.FindProperty(propertyName);
                    Assert.That(property, Is.Not.Null, propertyName);
                    Assert.That(property.objectReferenceValue, Is.Not.Null, propertyName);
                }

                var management = hud.Find("PF_ManagementUI");
                var formation = hud.Find("FormationPage");
                Assert.That(quickMenu.GetSiblingIndex(), Is.EqualTo(0));
                Assert.That(quickMenu.GetSiblingIndex(), Is.LessThan(management.GetSiblingIndex()));
                Assert.That(quickMenu.GetSiblingIndex(), Is.LessThan(formation.GetSiblingIndex()));
                Assert.That(hud.Find("ModeButton"), Is.Null);
                Assert.That(quickMenu.Find("StatusLayer/TopCenterStatus/StageStatusRoot/ModeButton"),
                    Is.Not.Null);
                Assert.That(management.Find("Buttons").gameObject.activeSelf, Is.False);
                Assert.That(formation.Find("OpenFormationButton").gameObject.activeSelf, Is.False);
                Assert.That(hud.Find("CastleRaidButton").gameObject.activeSelf, Is.False);

                var formationController = formation.GetComponentsInChildren<MonoBehaviour>(true)
                    .Single(x => x.GetType().Name == "FormationPageController");
                Assert.That(new SerializedObject(formationController)
                    .FindProperty("showStandaloneOpenButton").boolValue, Is.False);

                var expedition = roots.SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true))
                    .Single(x => x.GetType().Name == "ExpeditionController");
                var expeditionSerialized = new SerializedObject(expedition);
                foreach (var propertyName in new[]
                         {
                             "stageText", "waveText", "countText", "timerText", "modeButton", "modeText",
                             "progressFill"
                         })
                {
                    var reference = expeditionSerialized.FindProperty(propertyName).objectReferenceValue;
                    Assert.That(reference, Is.Not.Null, propertyName);
                    var component = reference as Component;
                    Assert.That(component, Is.Not.Null, propertyName);
                    Assert.That(component.transform.IsChildOf(quickMenu), Is.True, propertyName);
                }
                Assert.That(expeditionSerialized.FindProperty("progressFillMaxWidth").floatValue,
                    Is.EqualTo(360f).Within(0.01f));
                Assert.That(roots.Sum(CountMissingScripts), Is.Zero);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AssertNestedPrefab(Transform transform, string expectedPath)
        {
            Assert.That(transform, Is.Not.Null, expectedPath);
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(transform.gameObject),
                Is.EqualTo(expectedPath));
        }

        private static int CountMissingScripts(GameObject root)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Sum(x => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(x.gameObject));
        }

        private static int CountDirectLocks(Button button)
        {
            return button.transform.Cast<Transform>().Count(x => x.name == "LockBadge");
        }

        private static void AssertRectContains(RectTransform outer, RectTransform inner)
        {
            var outerCorners = new Vector3[4];
            var innerCorners = new Vector3[4];
            outer.GetWorldCorners(outerCorners);
            inner.GetWorldCorners(innerCorners);
            foreach (var corner in innerCorners)
            {
                Assert.That(corner.x, Is.InRange(outerCorners[0].x, outerCorners[2].x));
                Assert.That(corner.y, Is.InRange(outerCorners[0].y, outerCorners[2].y));
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, null);
        }
    }
}
