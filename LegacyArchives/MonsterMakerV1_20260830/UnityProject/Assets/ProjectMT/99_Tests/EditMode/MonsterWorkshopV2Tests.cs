using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ProjectMT.Tests.EditMode
{
    public sealed class MonsterWorkshopV2Tests
    {
        private const string WindowTypeName = "ProjectMT.EditorTools.MonsterMakerV2.MonsterWorkshopV2Window";
        private static readonly MethodInfo InvokeClick = typeof(Clickable).GetMethod(
            "Invoke", BindingFlags.Instance | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp() => CloseWorkshop();

        [TearDown]
        public void TearDown() => CloseWorkshop();

        [Test]
        public void PrimaryMenus_OpenOneSharedV2WindowAndSwitchMode()
        {
            Assert.That(EditorApplication.ExecuteMenuItem("JC Tool/Monster/기본공격 조립소"), Is.True);
            var window = FindWorkshop();
            Assert.That(window.rootVisualElement.Q<Label>("workshop-title").text, Is.EqualTo("기본공격 조립소 V2"));

            Assert.That(EditorApplication.ExecuteMenuItem("JC Tool/Monster/공격 액티브 조립소"), Is.True);
            Assert.That(FindWorkshop(), Is.SameAs(window));
            Assert.That(window.rootVisualElement.Q<Label>("workshop-title").text, Is.EqualTo("공격 액티브 조립소 V2"));

            Assert.That(EditorApplication.ExecuteMenuItem("JC Tool/Monster/효과형 액티브 조립소"), Is.True);
            Assert.That(FindWorkshop(), Is.SameAs(window));
            Assert.That(window.rootVisualElement.Q<Label>("workshop-title").text, Is.EqualTo("효과형 액티브 조립소 V2"));
            Assert.That(Resources.FindObjectsOfTypeAll<EditorWindow>().Count(IsWorkshop), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FreshWorkCopies_HaveRequiredContractsAndNoHorizontalScroll()
        {
            Assert.That(EditorApplication.ExecuteMenuItem("JC Tool/Monster/공격 액티브 조립소"), Is.True);
            yield return null;
            var window = FindWorkshop();
            window.position = new Rect(window.position.x, window.position.y, 1100f, 700f);
            yield return null;
            var root = window.rootVisualElement;
            Assert.That(window.hasUnsavedChanges, Is.False);
            Assert.That(SectionTexts(root), Has.Some.EqualTo("5. VFX/SFX 공간 계약 · 3개"));
            Assert.That(root.Q<ScrollView>("library-scroll").horizontalScroller.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(root.Q<ScrollView>("assembler-scroll").horizontalScroller.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(root.Q<VisualElement>("preview-host").worldBound.xMax,
                Is.LessThanOrEqualTo(root.worldBound.xMax + 0.5f));

            Click(root.Q<Button>("mode-effect"));
            Click(root.Q<Button>("new-button"));
            yield return null;
            Assert.That(SectionTexts(root), Has.Some.EqualTo("4. VFX/SFX 공간 계약 · 2개"));
            Assert.That(window.hasUnsavedChanges, Is.False);
        }

        [Test]
        public void StructuralButtons_DuplicateAndDeleteAttackStepWithoutAssetWrites()
        {
            Assert.That(EditorApplication.ExecuteMenuItem("JC Tool/Monster/공격 액티브 조립소"), Is.True);
            var window = FindWorkshop();
            var root = window.rootVisualElement;
            Click(root.Q<Button>("new-button"));
            var assembler = root.Q<ScrollView>("assembler-scroll");

            Click(assembler.Query<Button>().ToList().First(button => button.text == "복제"));
            Assert.That(SectionTexts(root), Has.Some.EqualTo("2. 공격 Step · 2개"));
            assembler = root.Q<ScrollView>("assembler-scroll");
            Click(assembler.Query<Button>().ToList().First(button => button.text == "삭제" && button.enabledSelf));
            Assert.That(SectionTexts(root), Has.Some.EqualTo("2. 공격 Step · 1개"));
            Assert.That(window.hasUnsavedChanges, Is.True);
        }

        [Test]
        public void LoadedAttackPreset_AssignsThenEditsIsolatedAndForks()
        {
            Assert.That(EditorApplication.ExecuteMenuItem("JC Tool/Monster/공격 액티브 조립소"), Is.True);
            var window = FindWorkshop();
            var root = window.rootVisualElement;
            var firstPreset = root.Q<ScrollView>("library-scroll").Query<Button>(className: "preset-button").First();
            Click(firstPreset);

            var fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var loaded = window.GetType().GetField("attackLoaded", fields).GetValue(window) as ProjectMT.Shared.Unit.MonsterActiveAttackProfile;
            Assert.That(loaded, Is.Not.Null);
            Assert.That(window.hasUnsavedChanges, Is.False);

            var draftType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerDraft"))
                .First(type => type != null);
            var draft = ScriptableObject.CreateInstance(draftType);
            try
            {
                window.GetType().GetMethod("OpenAttack", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { loaded, draft });
                var assign = root.Q<Button>("assign-button");
                Assert.That(assign.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(assign.enabledSelf, Is.True);
                Click(assign);
                Assert.That(draftType.GetProperty("ActiveAttackProfile").GetValue(draft), Is.SameAs(loaded));

                var originalName = loaded.DisplayName;
                var working = window.GetType().GetField("attackWorking", fields).GetValue(window) as ProjectMT.Shared.Unit.MonsterActiveAttackProfile;
                var serialized = new SerializedObject(working);
                serialized.FindProperty("displayName").stringValue = originalName + " QA";
                serialized.ApplyModifiedProperties();
                window.GetType().GetMethod("MarkCurrentDirty", fields)
                    .Invoke(window, new object[] { null, false });
                Assert.That(loaded.DisplayName, Is.EqualTo(originalName), "작업 사본 편집이 원본 자산을 바꾸면 안 됩니다.");
                Assert.That(window.hasUnsavedChanges, Is.True);

                Click(root.Q<Button>("fork-button"));
                Assert.That(window.GetType().GetField("attackLoaded", fields).GetValue(window), Is.Null);
                working = window.GetType().GetField("attackWorking", fields).GetValue(window) as ProjectMT.Shared.Unit.MonsterActiveAttackProfile;
                Assert.That(working.ProfileId, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void LoadedBasicPreset_AssignsThenEditsIsolatedAndForksWithoutLegacyWindow()
        {
            Assert.That(EditorApplication.ExecuteMenuItem("JC Tool/Monster/기본공격 조립소"), Is.True);
            var window = FindWorkshop();
            var root = window.rootVisualElement;
            Click(root.Q<ScrollView>("library-scroll").Query<Button>(className: "preset-button").First());

            var fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var session = window.GetType().GetField("basicSession", fields).GetValue(window);
            var loaded = session.GetType().GetProperty("LoadedProfile", fields).GetValue(session) as ProjectMT.Shared.Unit.MonsterBasicAttackProfile;
            Assert.That(loaded, Is.Not.Null);

            var draftType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerDraft"))
                .First(type => type != null);
            var draft = ScriptableObject.CreateInstance(draftType);
            try
            {
                window.GetType().GetMethod("OpenBasic", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { draft, loaded });
                Click(root.Q<Button>("assign-button"));
                Assert.That(draftType.GetProperty("BasicAttackProfile").GetValue(draft), Is.SameAs(loaded));

                var originalName = loaded.DisplayName;
                var recipe = session.GetType().GetProperty("Recipe", fields).GetValue(session);
                recipe.GetType().GetField("displayName").SetValue(recipe, originalName + " QA");
                session.GetType().GetMethod("NotifyChanged", fields).Invoke(session, new object[] { false });
                Assert.That(loaded.DisplayName, Is.EqualTo(originalName), "V2 기본공격 작업 사본이 원본 자산을 바꾸면 안 됩니다.");

                Click(root.Q<Button>("fork-button"));
                Assert.That(session.GetType().GetProperty("LoadedProfile", fields).GetValue(session), Is.Null);
                Assert.That(window.hasUnsavedChanges, Is.True);
                Assert.That(HiddenBasicCoreCount(), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void StandaloneMenu_ClearsPreviousMakerAssignmentTarget()
        {
            Assert.That(EditorApplication.ExecuteMenuItem("JC Tool/Monster/공격 액티브 조립소"), Is.True);
            var window = FindWorkshop();
            var root = window.rootVisualElement;
            Click(root.Q<ScrollView>("library-scroll").Query<Button>(className: "preset-button").First());

            var fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var loaded = window.GetType().GetField("attackLoaded", fields).GetValue(window) as ProjectMT.Shared.Unit.MonsterActiveAttackProfile;
            var draftType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("ProjectMT.EditorTools.MonsterMaker.MonsterMakerDraft"))
                .First(type => type != null);
            var draft = ScriptableObject.CreateInstance(draftType);
            try
            {
                window.GetType().GetMethod("OpenAttack", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { loaded, draft });
                Assert.That(root.Q<Button>("assign-button").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));

                Assert.That(EditorApplication.ExecuteMenuItem("JC Tool/Monster/공격 액티브 조립소"), Is.True);
                Assert.That(root.Q<Button>("assign-button").resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                    "독립 조립소가 이전 Maker의 배정 대상을 유지하면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void DiscardChanges_ClearsLoadedPresetSession()
        {
            Assert.That(EditorApplication.ExecuteMenuItem("JC Tool/Monster/공격 액티브 조립소"), Is.True);
            var window = FindWorkshop();
            Click(window.rootVisualElement.Q<ScrollView>("library-scroll").Query<Button>(className: "preset-button").First());
            Assert.That(SessionState.GetString("ProjectMT.MonsterWorkshopV2.attack.path", string.Empty), Is.Not.Empty);

            window.DiscardChanges();
            window.Close();
            Assert.That(SessionState.GetString("ProjectMT.MonsterWorkshopV2.attack.path", string.Empty), Is.Empty);

            Assert.That(EditorApplication.ExecuteMenuItem("JC Tool/Monster/공격 액티브 조립소"), Is.True);
            window = FindWorkshop();
            var loaded = window.GetType().GetField("attackLoaded", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(window);
            Assert.That(loaded, Is.Null);
            Assert.That(window.rootVisualElement.Q<Label>("assembler-title").text, Is.EqualTo("새 작업 사본"));
        }

        [Test]
        public void StructuralButtons_EditBasicAndEffectContractsWithoutAssetWrites()
        {
            Assert.That(EditorApplication.ExecuteMenuItem("JC Tool/Monster/기본공격 조립소"), Is.True);
            var window = FindWorkshop();
            var root = window.rootVisualElement;
            Click(root.Q<Button>("new-button"));
            var basicAssembler = root.Q<ScrollView>("assembler-scroll");
            var basicBefore = SectionTexts(root).Single(text => text.StartsWith("4. VFX/SFX 공간 계약"));
            Click(basicAssembler.Query<Button>().ToList().First(button => button.text == "+ VFX/SFX 공간 추가"));
            var basicAfter = SectionTexts(root).Single(text => text.StartsWith("4. VFX/SFX 공간 계약"));
            Assert.That(basicAfter, Is.Not.EqualTo(basicBefore));
            Click(root.Q<ScrollView>("assembler-scroll").Query<Button>().ToList()
                .Last(button => button.text == "삭제" && button.enabledSelf));
            Assert.That(SectionTexts(root), Has.Some.EqualTo(basicBefore));

            Click(root.Q<Button>("mode-effect"));
            Click(root.Q<Button>("new-button"));
            var effectAssembler = root.Q<ScrollView>("assembler-scroll");
            Click(effectAssembler.Query<Button>().ToList().First(button => button.text == "+ 효과 묶음 추가"));
            Assert.That(SectionTexts(root), Has.Some.EqualTo("2. 효과 묶음 · 2개"));
            effectAssembler = root.Q<ScrollView>("assembler-scroll");
            Click(effectAssembler.Query<Button>().ToList().First(button => button.text == "삭제" && button.enabledSelf));
            Assert.That(SectionTexts(root), Has.Some.EqualTo("2. 효과 묶음 · 1개"));
            Assert.That(window.hasUnsavedChanges, Is.True);
        }

        [UnityTest]
        public IEnumerator ClosingV2_DisposesIndependentBasicSessionAndReturnsPreviewScenes()
        {
            var previewBaseline = UnityEditor.SceneManagement.EditorSceneManager.previewSceneCount;
            Assert.That(EditorApplication.ExecuteMenuItem("JC Tool/Monster/기본공격 조립소"), Is.True);
            yield return null;
            var window = FindWorkshop();
            var session = window.GetType().GetField("basicSession", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(window);
            Assert.That(session, Is.Not.Null);
            Assert.That(session, Is.Not.InstanceOf<EditorWindow>(), "V2 기본공격 제작 세션이 Legacy EditorWindow에 의존하면 안 됩니다.");
            Assert.That(HiddenBasicCoreCount(), Is.Zero, "V2를 열 때 숨겨진 V1 창이 생성되면 안 됩니다.");
            window.DiscardChanges();
            window.Close();
            yield return null;
            Assert.That(Resources.FindObjectsOfTypeAll<EditorWindow>().Count(IsWorkshop), Is.Zero);
            Assert.That(HiddenBasicCoreCount(), Is.Zero);
            Assert.That(UnityEditor.SceneManagement.EditorSceneManager.previewSceneCount, Is.EqualTo(previewBaseline));
        }

        private static string[] SectionTexts(VisualElement root) => root.Q<ScrollView>("assembler-scroll")
            .Query<Label>(className: "section-title").ToList().Select(label => label.text).ToArray();

        private static void Click(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.enabledSelf, Is.True);
            InvokeClick.Invoke(button.clickable, new object[] { null });
        }

        private static EditorWindow FindWorkshop() => Resources.FindObjectsOfTypeAll<EditorWindow>().Single(IsWorkshop);
        private static bool IsWorkshop(EditorWindow window) => window != null && window.GetType().FullName == WindowTypeName;
        private static int HiddenBasicCoreCount() => Resources.FindObjectsOfTypeAll<EditorWindow>().Count(window =>
            window != null && window.GetType().FullName ==
            "ProjectMT.EditorTools.MonsterMaker.MonsterBasicAttackWorkshopWindow" &&
            (window.hideFlags & HideFlags.HideAndDontSave) != 0);

        private static void CloseWorkshop()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(IsWorkshop).ToArray())
            {
                window.DiscardChanges();
                window.Close();
            }
            SessionState.EraseString("ProjectMT.MonsterWorkshopV2.basic.json");
            SessionState.EraseBool("ProjectMT.MonsterWorkshopV2.basic.dirty");
            SessionState.EraseString("ProjectMT.MonsterWorkshopV2.attack.json");
            SessionState.EraseBool("ProjectMT.MonsterWorkshopV2.attack.dirty");
            SessionState.EraseString("ProjectMT.MonsterWorkshopV2.effect.json");
            SessionState.EraseBool("ProjectMT.MonsterWorkshopV2.effect.dirty");
        }
    }
}
