using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.Tests.EditMode
{
    public sealed class MonsterMakerV2ParityTests // 독립 V2의 배치·편집 안전·기능 계약 회귀 검사
    {
        private const string V2LayoutPath =
            "Assets/ProjectMT/Editor/MonsterMakerV2/UI/MonsterMakerV2NativeWindow.uxml";
        private const string V2StylePath =
            "Assets/ProjectMT/Editor/MonsterMakerV2/UI/MonsterMakerV2NativeWindow.uss";
        private const string V2AdjustmentLayoutPath =
            "Assets/ProjectMT/Editor/MonsterMakerV2/UI/MonsterMakerV2AdjustmentWindow.uxml";
        private const string V2AdjustmentStylePath =
            "Assets/ProjectMT/Editor/MonsterMakerV2/UI/MonsterMakerV2AdjustmentWindow.uss";
        private const string CatalogPath =
            "Assets/ProjectMT/02_Shared/Unit/Data/MonsterCatalog.asset";
        private const string DraftRoot =
            "Assets/ProjectMT/Editor/MonsterMaker/Drafts";

        [Test]
        public void UIToolkitShell_MatchesV1ThreeColumnAndTenSectionContract()
        {
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(V2LayoutPath);
            Assert.That(layout, Is.Not.Null);
            var root = layout.Instantiate();
            var workspace = root.Q<VisualElement>(className: "maker-workspace");
            var catalog = root.Q<VisualElement>(className: "catalog-panel");
            var editor = root.Q<VisualElement>(className: "editor-panel");
            var preview = root.Q<VisualElement>(className: "preview-panel");
            Assert.That(workspace, Is.Not.Null);
            Assert.That(workspace.IndexOf(catalog), Is.EqualTo(0));
            Assert.That(workspace.IndexOf(editor), Is.EqualTo(1));
            Assert.That(workspace.IndexOf(preview), Is.EqualTo(2));
            Assert.That(root.Query<VisualElement>(className: "draft-section").ToList().Count, Is.EqualTo(10));
            Assert.That(root.Q<ScrollView>("draft-scroll"), Is.Not.Null);
            Assert.That(root.Q<VisualElement>("preview-render-host"), Is.Not.Null);
            Assert.That(root.Q<Slider>("timeline-slider"), Is.Not.Null);
            Assert.That(root.Q<VisualElement>("validation-list"), Is.Not.Null);
        }

        [Test]
        public void BottomWorkspace_UsesTwoMotionRowsPreviewOverlayAndBottomDetails()
        {
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(V2LayoutPath);
            Assert.That(layout, Is.Not.Null);
            var root = layout.Instantiate();
            var previewHost = root.Q<VisualElement>("preview-render-host");
            var combatStatus = root.Q<VisualElement>(className: "combat-status-card");
            var motionCard = root.Q<VisualElement>(className: "motion-card");
            var playbackRow = root.Q<VisualElement>(className: "playback-toolbar");
            var combatPlaybackRow = root.Q<VisualElement>(className: "combat-playback-row");
            var commandStack = root.Q<VisualElement>(className: "command-stack");
            var publish = root.Q<VisualElement>(className: "publish-toolbar");
            var details = root.Q<ScrollView>("command-details-scroll");
            var validation = root.Q<VisualElement>("validation-card");

            Assert.That(combatStatus.parent, Is.EqualTo(previewHost));
            Assert.That(playbackRow.parent, Is.EqualTo(motionCard));
            Assert.That(combatPlaybackRow.parent, Is.EqualTo(motionCard));
            Assert.That(motionCard.IndexOf(playbackRow), Is.LessThan(motionCard.IndexOf(combatPlaybackRow)));
            Assert.That(publish.parent, Is.EqualTo(commandStack));
            Assert.That(details.parent, Is.EqualTo(commandStack));
            Assert.That(commandStack.IndexOf(publish), Is.LessThan(commandStack.IndexOf(details)));
            Assert.That(validation.parent, Is.EqualTo(details));
        }

        [Test]
        public void Styles_PreserveCatalogEditorAndFlexiblePreviewWidths()
        {
            var style = ReadAssetText(V2StylePath);
            StringAssert.Contains(".catalog-panel", style);
            StringAssert.Contains("width: 230px", style);
            StringAssert.Contains(".editor-panel", style);
            StringAssert.Contains("width: 430px", style);
            StringAssert.Contains(".preview-panel", style);
            StringAssert.Contains("min-width: 420px", style);
            StringAssert.Contains("min-height: 760px", style);
        }

        [Test]
        public void Window_IsNativeV2AndDoesNotOwnV1Window()
        {
            var v1Type = Type.GetType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterMakerWindow, Assembly-CSharp-Editor",
                true);
            var v2Type = Type.GetType(
                "ProjectMT.EditorTools.MonsterMakerV2.MonsterMakerV2Window, Assembly-CSharp-Editor",
                true);
            var fields = v2Type.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(fields.Any(field => field.FieldType == v1Type), Is.False);
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Assert.That(v1Type.GetMethod("ConfigureHostedMode", instance), Is.Null);
            Assert.That(v1Type.GetMethod("DrawHosted", instance), Is.Null);
            Assert.That(v1Type.GetMethod("OpenHostedDraft", instance), Is.Null);
            Assert.That(v2Type.GetMethod("CreateGUI", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
            Assert.That(v2Type.GetMethod("OpenDraft", BindingFlags.Static | BindingFlags.Public), Is.Not.Null);
            var openWindow = v2Type.GetMethod("OpenWindow", BindingFlags.Static | BindingFlags.Public);
            var menu = openWindow?.GetCustomAttribute<MenuItem>();
            Assert.That(menu?.menuItem, Is.EqualTo("JC Tool/Monster/Monster Maker"));
        }

        [Test]
        public void V1_RemainsAvailableOnlyAsLegacyEntryPoint()
        {
            var v1Type = Type.GetType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterMakerWindow, Assembly-CSharp-Editor",
                true);
            var openWindow = v1Type.GetMethod("OpenWindow", BindingFlags.Static | BindingFlags.Public);
            var menu = openWindow?.GetCustomAttribute<MenuItem>();
            Assert.That(
                menu?.menuItem,
                Is.EqualTo("JC Tool/Monster/Legacy/Monster Maker V1"));
        }

        [Test]
        public void WorkingCopy_EditAndDiscard_NeverMutatesPersistentDraft()
        {
            var source = LoadFirstDraft();
            var stateType = Type.GetType(
                "ProjectMT.EditorTools.MonsterMakerV2.MonsterMakerV2State, Assembly-CSharp-Editor",
                true);
            var state = Activator.CreateInstance(stateType);
            try
            {
                stateType.GetMethod("Load")?.Invoke(state, new object[] { source });
                var working = (ScriptableObject)stateType.GetProperty("WorkingDraft")?.GetValue(state);
                var serialized = (SerializedObject)stateType.GetProperty("SerializedDraft")?.GetValue(state);
                var sourceSerialized = new SerializedObject(source);
                var original = sourceSerialized.FindProperty("displayName").stringValue;
                Assert.That(EditorUtility.IsPersistent(working), Is.False);

                serialized.FindProperty("displayName").stringValue = original + "__V2_TEST";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                stateType.GetMethod("MarkChanged")?.Invoke(state, null);
                sourceSerialized.Update();
                Assert.That(sourceSerialized.FindProperty("displayName").stringValue, Is.EqualTo(original));
                Assert.That(stateType.GetProperty("IsDirty")?.GetValue(state), Is.EqualTo(true));

                stateType.GetMethod("DiscardChanges")?.Invoke(state, null);
                var restored = (ScriptableObject)stateType.GetProperty("WorkingDraft")?.GetValue(state);
                var restoredSerialized = new SerializedObject(restored);
                Assert.That(restoredSerialized.FindProperty("displayName").stringValue, Is.EqualTo(original));
            }
            finally
            {
                (state as IDisposable)?.Dispose();
            }
        }

        [Test]
        public void Recovery_PreservesDirtyStateAcrossBindingRefreshUntilSaveOrDiscard()
        {
            var source = LoadFirstDraft();
            var stateType = Type.GetType(
                "ProjectMT.EditorTools.MonsterMakerV2.MonsterMakerV2State, Assembly-CSharp-Editor",
                true);
            var state = Activator.CreateInstance(stateType);
            try
            {
                stateType.GetMethod("Load")?.Invoke(state, new object[] { source });
                var working = (UnityEngine.Object)stateType.GetProperty("WorkingDraft")?.GetValue(state);
                var recoveryJson = EditorJsonUtility.ToJson(working);
                stateType.GetMethod("RestoreRecovery")?.Invoke(
                    state,
                    new object[] { recoveryJson, true });
                stateType.GetMethod("MarkChanged")?.Invoke(state, null);
                Assert.That(stateType.GetProperty("IsDirty")?.GetValue(state), Is.EqualTo(true));

                stateType.GetMethod("DiscardChanges")?.Invoke(state, null);
                Assert.That(stateType.GetProperty("IsDirty")?.GetValue(state), Is.EqualTo(false));
            }
            finally
            {
                (state as IDisposable)?.Dispose();
            }
        }

        [Test]
        public void V2Shell_ExposesDirectDraftCatalogAndUnifiedPresetSelection()
        {
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(V2LayoutPath);
            Assert.That(layout, Is.Not.Null);
            var root = layout.Instantiate();
            Assert.That(root.Q<Button>("open-draft-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("catalog-toggle-button"), Is.Not.Null);
            Assert.That(root.Q<VisualElement>("catalog-panel"), Is.Not.Null);

            var windowSource = ReadAssetText(
                "Assets/ProjectMT/Editor/MonsterMakerV2/MonsterMakerV2NativeWindow.cs") +
                ReadAssetText(
                    "Assets/ProjectMT/Editor/MonsterMakerV2/MonsterMakerV2NativeWindow.Catalog.cs");
            var skills = ReadAssetText(
                "Assets/ProjectMT/Editor/MonsterMakerV2/Views/MonsterMakerV2AuthoringView.Skills.cs");
            var combat = ReadAssetText(
                "Assets/ProjectMT/Editor/MonsterMakerV2/Views/MonsterMakerV2AuthoringView.Combat.cs");
            StringAssert.Contains("ShowAllDraftMenu", windowSource);
            StringAssert.Contains("ShowPassivePresetMenu", skills);
            StringAssert.Contains("ShowActivePresetMenu", skills);
            StringAssert.Contains("ShowBasicAttackPresetMenu", combat);
            StringAssert.DoesNotContain(
                "AddProperty(container, \"basicAttackProfile\"",
                combat);
            StringAssert.DoesNotContain(
                "AddProperty(activeArea, \"activeAttackProfile\"",
                skills);
            StringAssert.DoesNotContain(
                "AddProperty(activeArea, \"activeEffectProfile\"",
                skills);
            StringAssert.Contains("MonsterWorkshopAssignmentEvents.PresetAssigned", windowSource);
            StringAssert.DoesNotContain("MonsterBasicAttackWorkshopWindow.PresetAssigned", windowSource);
            StringAssert.DoesNotContain("MonsterActiveAttackWorkshopWindow.PresetAssigned", windowSource);
            StringAssert.DoesNotContain("MonsterEffectActiveWorkshopWindow.PresetAssigned", windowSource);
        }

        [Test]
        public void NativeSources_ContainDedicatedUxForWorkshopsFeedbackAndExactNumberInput()
        {
            var root = "Assets/ProjectMT/Editor/MonsterMakerV2/Views";
            var combatSource =
                ReadAssetText(root + "/MonsterMakerV2AuthoringView.Combat.cs");
            var source = ReadAssetText(root + "/MonsterMakerV2AuthoringView.Skills.cs") +
                         combatSource +
                         ReadAssetText(root + "/MonsterMakerV2AuthoringView.Feedback.cs");
            var cardStart = combatSource.IndexOf(
                "private void BuildBasicVfxCard",
                StringComparison.Ordinal);
            var cardEnd = combatSource.IndexOf(
                "private void PreviewBasicAttackSound",
                cardStart,
                StringComparison.Ordinal);
            var basicVfxCardSource = combatSource.Substring(
                cardStart,
                cardEnd - cardStart);
            StringAssert.Contains("공격 조립소 열기", source);
            StringAssert.Contains("기본공격 조립소 열기", source);
            StringAssert.Contains("전용 래퍼 만들기", source);
            StringAssert.Contains("VFX 위치 직접 조절 · 재생", source);
            StringAssert.Contains("new FloatField", source);
            StringAssert.Contains("유지 시간·시작점·속도·위치·회전·크기", source);
            StringAssert.Contains("ResolveVfxEventLabel", source);
            StringAssert.DoesNotContain("Recipe 실행 시점", source);
            StringAssert.DoesNotContain("FindPropertyRelative(\"lifetime\")", basicVfxCardSource);
            StringAssert.DoesNotContain("FindPropertyRelative(\"playbackOffset\")", basicVfxCardSource);
            StringAssert.DoesNotContain("FindPropertyRelative(\"localPosition\")", basicVfxCardSource);
            StringAssert.DoesNotContain("new MonsterMakerWindow", source);

            var popupSource = ReadAssetText(
                "Assets/ProjectMT/Editor/MonsterMakerV2/MonsterMakerV2AdjustmentWindow.cs");
            StringAssert.Contains("currentLifetime", popupSource);
            StringAssert.Contains("ResolvePlaybackSpeedGaugeExponent", popupSource);
        }

        [Test]
        public void AdjustmentWindow_IsNativeV2ToolkitAndOwnsEveryV2Route()
        {
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(V2AdjustmentLayoutPath);
            Assert.That(layout, Is.Not.Null);
            var root = layout.Instantiate();
            Assert.That(root.Q<VisualElement>("adjust-preview-host"), Is.Not.Null);
            Assert.That(root.Q<Vector3Field>("position-field"), Is.Not.Null);
            Assert.That(root.Q<Vector3Field>("vfx-position-field"), Is.Not.Null);
            Assert.That(root.Q<Vector3Field>("vfx-euler-field"), Is.Not.Null);
            Assert.That(root.Q<FloatField>("vfx-scale-field"), Is.Not.Null);
            Assert.That(root.Q<FloatField>("vfx-lifetime-field"), Is.Not.Null);
            Assert.That(root.Q<FloatField>("vfx-offset-field"), Is.Not.Null);
            Assert.That(root.Q<Slider>("vfx-speed-slider"), Is.Not.Null);
            Assert.That(root.Q<Button>("reset-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("apply-button"), Is.Not.Null);
            Assert.That(root.Q<Button>("cancel-button"), Is.Not.Null);

            var style = ReadAssetText(V2AdjustmentStylePath);
            StringAssert.Contains(".adjust-preview-host", style);
            StringAssert.Contains(".playback-panel", style);
            StringAssert.DoesNotContain(":last-child", style);
            StringAssert.DoesNotContain(":first-child", style);

            var routeSource = ReadAssetText(
                "Assets/ProjectMT/Editor/MonsterMakerV2/MonsterMakerV2NativeWindow.Editing.cs");
            StringAssert.DoesNotContain("MonsterPositionAdjustWindow", routeSource);
            Assert.That(Count(routeSource, "MonsterMakerV2AdjustmentWindow.OpenPosition("), Is.EqualTo(1));
            Assert.That(Count(routeSource, "MonsterMakerV2AdjustmentWindow.OpenVfx("), Is.EqualTo(2));

            var previewSource = ReadAssetText(
                "Assets/ProjectMT/Editor/MonsterMakerV2/MonsterMakerV2AdjustmentWindow.Preview.cs");
            StringAssert.Contains("Handles.SetCamera(ResolveWindowPreviewRect(previewRect), camera)", previewSource);
            StringAssert.Contains("previewIMGUI.worldBound", previewSource);
        }

        [TestCase(0.5f, -1f)]
        [TestCase(1f, 0f)]
        [TestCase(2f, 1f)]
        [TestCase(128f, 7f)]
        public void AdjustmentWindow_PlaybackGauge_IsOneXCenteredAndPreservesExtremes(
            float speed,
            float expectedGauge)
        {
            var type = Type.GetType(
                "ProjectMT.EditorTools.MonsterMakerV2.MonsterMakerV2AdjustmentWindow, Assembly-CSharp-Editor",
                true);
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var toGauge = type.GetMethod("ToPlaybackSpeedGauge", flags);
            var fromGauge = type.GetMethod("FromPlaybackSpeedGauge", flags);
            var exponent = type.GetMethod("ResolvePlaybackSpeedGaugeExponent", flags);
            Assert.That(toGauge, Is.Not.Null);
            Assert.That(fromGauge, Is.Not.Null);
            Assert.That(exponent, Is.Not.Null);

            var gauge = (float)toGauge.Invoke(null, new object[] { speed });
            var restored = (float)fromGauge.Invoke(null, new object[] { gauge });
            var range = (float)exponent.Invoke(null, new object[] { speed });
            Assert.That(gauge, Is.EqualTo(expectedGauge).Within(0.0001f));
            Assert.That(restored, Is.EqualTo(speed).Within(0.0001f));
            Assert.That(range, Is.GreaterThanOrEqualTo(Mathf.Abs(expectedGauge)));
        }

        [Test]
        public void LayoutAndStyles_KeepCatalogAndValidationInsideTheirPanels()
        {
            var layoutText = ReadAssetText(V2LayoutPath);
            var style = ReadAssetText(V2StylePath);
            var windowSource = ReadAssetText(
                "Assets/ProjectMT/Editor/MonsterMakerV2/MonsterMakerV2NativeWindow.cs");
            var authoringSource = ReadAssetText(
                "Assets/ProjectMT/Editor/MonsterMakerV2/Views/MonsterMakerV2AuthoringView.cs");
            var previewSource = ReadAssetText(
                "Assets/ProjectMT/Editor/MonsterMakerV2/MonsterMakerV2NativeWindow.Preview.cs");
            StringAssert.Contains("<Style src=", layoutText);
            StringAssert.Contains("MonsterMakerV2NativeWindow.uss", layoutText);
            StringAssert.Contains("MonsterMakerV2NativeWindow.uss", windowSource);
            StringAssert.DoesNotContain("styleSheets.Clear", windowSource);
            StringAssert.Contains("while (rootVisualElement.styleSheets.Remove(styleSheet))", windowSource);
            StringAssert.DoesNotContain("styleSheets.Add", windowSource);
            StringAssert.Contains("catalog-search-row", layoutText);
            StringAssert.Contains("command-details-scroll", layoutText);
            StringAssert.Contains("bottom-workspace--validation", style);
            StringAssert.Contains("overflow: hidden", style);
            StringAssert.Contains("min-height: 27px", style);
            StringAssert.Contains("min-height: 24px", style);
            StringAssert.Contains(".catalog-list-frame { overflow: hidden; }", style);
            StringAssert.Contains("min-height: 28px", style);
            StringAssert.Contains(".catalog-row.unity-collection-view__item--selected", style);
            StringAssert.Contains("display: none", style);
            StringAssert.Contains("discard-button", layoutText);
            StringAssert.Contains("AlignPropertyFieldColumns", authoringSource);
            StringAssert.Contains("flex-basis: 0", style);
            StringAssert.Contains(".profile-grid > .profile-value { width: 49%; }", style);
            StringAssert.Contains("height: 100px", style);
            StringAssert.Contains("control-row--history", layoutText);
            StringAssert.Contains("↶ 실행 취소", layoutText);
            StringAssert.Contains("DrawPreviewReferenceOverlay", previewSource);
            StringAssert.Contains("MonsterPositionReferenceOverlay.DrawVisibilityToolbar", previewSource);
            StringAssert.Contains("MonsterPositionReferenceOverlay.DrawPoint", previewSource);
            StringAssert.Contains("모델 기준", previewSource);
            StringAssert.Contains("motion-row-label", layoutText);
            StringAssert.Contains(".control-group > Button", style);
            StringAssert.Contains(".attack-buttons { width: 100%;", style);
            StringAssert.Contains("active-motion-slot", layoutText);
            StringAssert.Contains("border-left-width: 3px", style);
        }

        [Test]
        public void ProductionCatalog_AllEntriesHavePersistentDrafts()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Definitions.Count, Is.EqualTo(44));
            foreach (var definition in catalog.Definitions)
            {
                Assert.That(definition, Is.Not.Null);
                Assert.That(
                    AssetDatabase.LoadMainAssetAtPath($"{DraftRoot}/Draft_{definition.MonsterId}.asset"),
                    Is.Not.Null,
                    $"{definition.MonsterId} 제작 원본");
            }
        }

        private static ScriptableObject LoadFirstDraft()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            var definition = catalog.Definitions.First(item => item != null);
            var draft = AssetDatabase.LoadMainAssetAtPath(
                $"{DraftRoot}/Draft_{definition.MonsterId}.asset") as ScriptableObject;
            Assert.That(draft, Is.Not.Null);
            return draft;
        }

        private static string ReadAssetText(string assetPath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(
                Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static int Count(string source, string token)
        {
            var count = 0;
            var offset = 0;
            while ((offset = source.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += token.Length;
            }
            return count;
        }
    }
}
