using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Tests.EditMode
{
    public sealed class CombatFeelAuthoringToolTests // FEEL 프로필 제작·수정 왕복 계약
    {
        private const string LabTypeName = "ProjectMT.Tools.FeelPreview.CombatFeelCatalogPreviewLab, Assembly-CSharp";
        private const string ProfilePath = "Assets/ProjectMT/05_Art/FeelPresets/BasicAttack/Profiles/BAFeel_EditModeRoundTrip.prefab";
        private const string InvalidProfilePath = "Assets/ProjectMT/05_Art/FeelPresets/BasicAttack/Profiles/BAFeel_EditModeInvalid.prefab";
        private GameObject root;
        private GameObject target;
        private Component lab;
        private Type labType;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(ProfilePath);
            AssetDatabase.DeleteAsset(InvalidProfilePath);
            labType = Type.GetType(LabTypeName, true);
            root = new GameObject("CombatFeelAuthoringToolTest");
            target = new GameObject("CombatFeelAuthoringTarget");
            lab = root.AddComponent(labType);
            labType.GetMethod("Configure", BindingFlags.Public | BindingFlags.Instance)?.Invoke(lab, new object[] { target });
            labType.GetMethod("AuthoringCreateBlankForDiagnostics", BindingFlags.Public | BindingFlags.Instance)?.Invoke(lab, null);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(ProfilePath);
            AssetDatabase.DeleteAsset(InvalidProfilePath);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
        }

        [Test]
        public void WorkingCopy_AddsRealLayersWithShowcaseDefaultsAndOriginAnchors()
        {
            foreach (var typeName in new[] { "MMF_PositionSpring", "MMF_RotationSpring", "MMF_ScaleSpring", "MMF_Light", "MMF_Particles" })
                Assert.That(Add(typeName), Is.True, typeName);
            Assert.That(Add("MMF_Bloom_URP"), Is.False, "설치되지 않은 조건부 타입은 가짜 계층으로 대체하지 않는다.");

            var types = (string[])labType.GetMethod("AuthoringLayerTypesForDiagnostics")?.Invoke(lab, null);
            Assert.That(types, Is.EqualTo(new[] { "MMF_PositionSpring", "MMF_RotationSpring", "MMF_ScaleSpring", "MMF_Light", "MMF_Particles" }));
            Assert.That((string)labType.GetMethod("AuthoringValidateForDiagnostics")?.Invoke(lab, null), Does.StartWith("검증 통과"));

            var workingRoot = GetPrivate<GameObject>("workingFeelRoot");
            Assert.That(workingRoot, Is.Not.Null);
            Assert.That(workingRoot.transform.Cast<Transform>().All(child => child.localPosition == Vector3.zero), Is.True);
            var light = workingRoot.GetComponentInChildren<Light>(true);
            var particle = workingRoot.GetComponentInChildren<ParticleSystem>(true);
            Assert.That(light, Is.Not.Null);
            Assert.That(light.range, Is.EqualTo(2.4f).Within(0.001f));
            Assert.That(particle, Is.Not.Null);
            Assert.That(particle.main.maxParticles, Is.EqualTo(64));
            Assert.That(particle.main.playOnAwake, Is.False);
        }

        [Test]
        public void WorkingCopy_StackOperationsPreserveReferenceHolderAndIndependentLayerState()
        {
            foreach (var typeName in new[] { "MMF_Position", "MMF_Rotation", "MMF_Light" })
                Assert.That(Add(typeName), Is.True, typeName);

            Assert.That(InvokeBool("AuthoringDuplicateLayerForDiagnostics", 1), Is.True);
            Assert.That(LayerTypes(), Is.EqualTo(new[] { "MMF_Position", "MMF_Rotation", "MMF_Light", "MMF_Rotation" }));

            Assert.That(InvokeBool("AuthoringSetLayerActiveForDiagnostics", 3, false), Is.True);
            Assert.That(LayerActive(), Is.EqualTo(new[] { true, true, true, false }), "복제본 활성 변경이 원본에 전파되면 안 된다.");

            Assert.That(InvokeBool("AuthoringMoveLayerForDiagnostics", 3, 0), Is.True);
            Assert.That(LayerTypes(), Is.EqualTo(new[] { "MMF_Rotation", "MMF_Position", "MMF_Rotation", "MMF_Light" }));
            Assert.That(LayerActive(), Is.EqualTo(new[] { false, true, true, true }));

            Assert.That(InvokeBool("AuthoringRemoveLayerForDiagnostics", 0), Is.True);
            Assert.That(LayerTypes(), Is.EqualTo(new[] { "MMF_Position", "MMF_Rotation", "MMF_Light" }));
            Assert.That(LayerActive(), Is.All.True);
            Assert.That((string)labType.GetMethod("AuthoringValidateForDiagnostics")?.Invoke(lab, null), Does.StartWith("검증 통과"));
        }

        [Test]
        public void ProfileCapability_ReportsTargetMotionFromTheActualEffectStack()
        {
            Assert.That(Add("MMF_Light"), Is.True);
            var workingRoot = GetPrivate<GameObject>("workingFeelRoot");
            var runtime = workingRoot.GetComponent(typeof(IBasicAttackFeelRuntime)) as IBasicAttackFeelRuntime;
            Assert.That(runtime, Is.Not.Null);
            Assert.That(runtime.HasBasicAttackTargetMotion(), Is.False,
                "타격점 조명만 있는 프로필은 공용 모델 반동을 막으면 안 된다.");

            Assert.That(Add("MMF_Position"), Is.True);
            Assert.That(runtime.HasBasicAttackTargetMotion(), Is.True,
                "활성 위치 효과가 추가된 프로필만 타깃 모션 소유권을 보고해야 한다.");
        }

        [Test]
        public void RuntimeNoOpProfiles_AreBlockedBeforeSave()
        {
            Assert.That(Add("MMF_TimescaleModifier"), Is.True);
            var validation = (string)labType.GetMethod("AuthoringValidateForDiagnostics")?.Invoke(lab, null);
            Assert.That(validation, Does.StartWith("저장 불가"));
            Assert.That(validation, Does.Contain("실전 재생 효과 0개"));
            Assert.That(
                (string)labType.GetMethod("AuthoringSaveProfileForDiagnostics")?.Invoke(
                    lab,
                    new object[] { "BAFeel_EditModeInvalid" }),
                Is.Null);

            labType.GetMethod("AuthoringCreateBlankForDiagnostics")?.Invoke(lab, null);
            Assert.That(Add("MMF_Position"), Is.True);
            Assert.That(InvokeBool("AuthoringSetLayerActiveForDiagnostics", 0, false), Is.True);
            validation = (string)labType.GetMethod("AuthoringValidateForDiagnostics")?.Invoke(lab, null);
            Assert.That(validation, Does.StartWith("저장 불가"));
            Assert.That(validation, Does.Contain("실전 재생 효과 0개"));

            labType.GetMethod("AuthoringCreateBlankForDiagnostics")?.Invoke(lab, null);
            Assert.That(Add("MMF_TimescaleModifier"), Is.True);
            Assert.That(Add("MMF_Light"), Is.True);
            validation = (string)labType.GetMethod("AuthoringValidateForDiagnostics")?.Invoke(lab, null);
            Assert.That(validation, Does.StartWith("검증 통과"));
            Assert.That(validation, Does.Contain("MainBattle 공용 소유 효과"));

            var player = GetPrivate<Component>("workingPlayer");
            var feedbacks = player?.GetType().GetField("FeedbacksList")?.GetValue(player) as IList;
            Assert.That(feedbacks, Is.Not.Null);
            feedbacks.RemoveAt(0);

            Assert.That(
                (string)labType.GetMethod("AuthoringValidateForDiagnostics")?.Invoke(lab, null),
                Does.StartWith("저장 불가"));
            Assert.That(
                (string)labType.GetMethod("AuthoringSaveProfileForDiagnostics")?.Invoke(
                    lab,
                    new object[] { "BAFeel_EditModeInvalid" }),
                Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(InvalidProfilePath), Is.Null);
        }

        [Test]
        public void ProfileRoundTrip_PreservesLayerOrderCuePlacementAndProductionHashes()
        {
            foreach (var typeName in new[] { "MMF_PositionSpring", "MMF_RotationSpring", "MMF_Light" })
                Assert.That(Add(typeName), Is.True, typeName);
            SetPrivate("cueLifetime", 0.97f);
            SetPrivate("cuePosition", new Vector3(0.12f, 0.34f, -0.08f));
            SetPrivate("cueEuler", new Vector3(5f, 25f, -8f));
            SetPrivate("cueScale", 1.17f);

            var productionPaths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/ProjectMT/05_Art/FeelPresets/BasicAttack/Production" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var beforeHashes = productionPaths.ToDictionary(path => path, path => AssetDatabase.GetAssetDependencyHash(path));

            var savedPath = (string)labType.GetMethod("AuthoringSaveProfileForDiagnostics")?.Invoke(lab, new object[] { "BAFeel_EditModeRoundTrip" });
            Assert.That(savedPath, Is.EqualTo(ProfilePath));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(savedPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponents<Component>().Any(component => component != null && component.GetType().Name == "MMF_Player"), Is.True);
            Assert.That(prefab.GetComponents<Component>().Any(component => component != null && component.GetType().Name == "BasicAttackFeelRuntimeAdapter"), Is.True);

            var metadata = prefab.GetComponent<ProjectMT.Shared.Unit.BasicAttackFeelProfileMetadata>();
            Assert.That(metadata, Is.Not.Null);
            Assert.That(metadata.Lifetime, Is.EqualTo(0.97f).Within(0.001f));
            Assert.That(metadata.LocalPosition, Is.EqualTo(new Vector3(0.12f, 0.34f, -0.08f)));
            Assert.That(metadata.LocalEulerAngles, Is.EqualTo(new Vector3(5f, 25f, -8f)));
            Assert.That(metadata.Scale, Is.EqualTo(1.17f).Within(0.001f));

            var cue = new ProjectMT.Shared.Unit.BasicAttackFeelCue();
            cue.EditorConfigure(prefab, 5f, Vector3.one * 9f, Vector3.one * 45f, 4f);
            Assert.That(cue.Lifetime, Is.EqualTo(0.97f).Within(0.001f));
            Assert.That(cue.LocalPosition, Is.EqualTo(new Vector3(0.12f, 0.34f, -0.08f)));
            Assert.That(cue.LocalRotation.eulerAngles.y, Is.EqualTo(25f).Within(0.001f));
            Assert.That(cue.Scale, Is.EqualTo(1.17f).Within(0.001f),
                "신규 FEEL 프로필은 Maker에 복사된 옛 Cue 값보다 프로필 자체 값을 사용해야 한다.");

            labType.GetMethod("AuthoringLoadPresetForDiagnostics")?.Invoke(lab, new object[] { prefab });
            var types = (string[])labType.GetMethod("AuthoringLayerTypesForDiagnostics")?.Invoke(lab, null);
            Assert.That(types, Is.EqualTo(new[] { "MMF_PositionSpring", "MMF_RotationSpring", "MMF_Light" }));
            Assert.That(GetPrivate<float>("cueLifetime"), Is.EqualTo(0.97f).Within(0.001f));
            Assert.That(GetPrivate<Vector3>("cuePosition"), Is.EqualTo(new Vector3(0.12f, 0.34f, -0.08f)));
            Assert.That(GetPrivate<Vector3>("cueEuler"), Is.EqualTo(new Vector3(5f, 25f, -8f)));
            Assert.That(GetPrivate<float>("cueScale"), Is.EqualTo(1.17f).Within(0.001f));
            Assert.That(productionPaths.Where(path => AssetDatabase.GetAssetDependencyHash(path) != beforeHashes[path]), Is.Empty);
        }

        [Test]
        public void PanelLayout_KeepsRightAnchorAcrossModeWidthChangesAndPreservesDraggedPosition()
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var positionField = labType.GetField("panelPosition", flags);
            var layoutWidthField = labType.GetField("panelLayoutWidth", flags);
            var viewportWidthField = labType.GetField("panelViewportWidth", flags);
            var updateLayout = labType.GetMethod("UpdatePanelLayout", flags);
            var collapsedX = labType.GetMethod("GetCollapsedPanelX", flags);

            positionField?.SetValue(lab, new Vector2(1384f, 24f));
            layoutWidthField?.SetValue(lab, 512f);
            viewportWidthField?.SetValue(lab, 1920f);
            updateLayout?.Invoke(lab, new object[] { 1920f, 1080f, 920f, 860f, 24f });
            Assert.That((Vector2)positionField?.GetValue(lab), Is.EqualTo(new Vector2(976f, 24f)),
                "우측 상단에 붙은 패널은 모드 폭이 바뀌어도 24px 여백을 유지해야 한다.");
            Assert.That((float)collapsedX?.Invoke(lab, new object[] { 1920f, 920f, 236f }), Is.EqualTo(1660f),
                "접힌 버튼도 펼친 패널과 같은 24px 우측 여백을 유지해야 한다.");

            positionField?.SetValue(lab, new Vector2(520f, 100f));
            layoutWidthField?.SetValue(lab, 512f);
            viewportWidthField?.SetValue(lab, 1920f);
            updateLayout?.Invoke(lab, new object[] { 1920f, 1080f, 920f, 860f, 24f });
            Assert.That((Vector2)positionField?.GetValue(lab), Is.EqualTo(new Vector2(520f, 100f)),
                "사용자가 드래그한 패널 위치는 모드 전환으로 임의 재배치하면 안 된다.");
            Assert.That((float)collapsedX?.Invoke(lab, new object[] { 1920f, 920f, 236f }), Is.EqualTo(1204f),
                "접을 때도 드래그한 패널의 우측 모서리 위치가 갑자기 점프하면 안 된다.");
        }

        [Test]
        public void AuthoringFlow_IsOneWayFromLabProfileToMakerSelection()
        {
            var labSource = System.IO.File.ReadAllText(
                "Assets/ProjectMT/90_Tools/FeelPreview/CombatFeelCatalogPreviewLab.Authoring.cs");
            var makerSource = System.IO.File.ReadAllText(
                "Assets/ProjectMT/Editor/MonsterMaker/MonsterBasicAttackWorkshopWindow.cs");

            Assert.That(labSource, Does.Not.Contain("MonsterBasicAttackProfile"));
            Assert.That(labSource, Does.Not.Contain("EditorSetFeelFeedback"));
            Assert.That(makerSource, Does.Contain("LoadFeelProfileOptions"));
            Assert.That(makerSource, Does.Contain("현재 프로필 값"));
            Assert.That(makerSource, Does.Contain("BasicAttackFeelProfileMetadata"));
            Assert.That(makerSource, Does.Not.Contain("FEEL 프리셋 Prefab"));
            Assert.That(makerSource, Does.Not.Contain("DrawProductionImpactPicker"));
        }

        private bool Add(string typeName) =>
            (bool)labType.GetMethod("AuthoringAddEffectForDiagnostics")?.Invoke(lab, new object[] { typeName });

        private bool InvokeBool(string methodName, params object[] arguments) =>
            (bool)labType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)?.Invoke(lab, arguments);

        private string[] LayerTypes() =>
            (string[])labType.GetMethod("AuthoringLayerTypesForDiagnostics")?.Invoke(lab, null);

        private bool[] LayerActive() =>
            (bool[])labType.GetMethod("AuthoringLayerActiveForDiagnostics")?.Invoke(lab, null);

        private T GetPrivate<T>(string name) =>
            (T)labType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(lab);

        private void SetPrivate(string name, object value) =>
            labType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(lab, value);
    }
}
