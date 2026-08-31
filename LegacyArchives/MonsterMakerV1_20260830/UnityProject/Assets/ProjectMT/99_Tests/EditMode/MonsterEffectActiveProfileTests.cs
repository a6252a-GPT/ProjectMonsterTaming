using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.Tests.EditMode
{
    public sealed class MonsterEffectActiveProfileTests
    {
        private static readonly string[] ExamplePaths =
        {
            "Assets/ProjectMT/02_Shared/Unit/Data/ActiveEffectProfiles/Custom/EAP_battle_hymn.asset",
            "Assets/ProjectMT/02_Shared/Unit/Data/ActiveEffectProfiles/Custom/EAP_guardian_sanctuary.asset",
            "Assets/ProjectMT/02_Shared/Unit/Data/ActiveEffectProfiles/Custom/EAP_abyssal_curse.asset"
        };

        [Test]
        public void ProductionExamples_CoverThreeRolesAndPassContracts()
        {
            var profiles = ExamplePaths
                .Select(AssetDatabase.LoadAssetAtPath<MonsterEffectActiveProfile>)
                .ToArray();

            Assert.That(profiles, Has.All.Not.Null);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    MonsterEffectActiveRole.Support,
                    MonsterEffectActiveRole.Guard,
                    MonsterEffectActiveRole.Debuff
                },
                profiles.Select(profile => profile.Role));

            foreach (var profile in profiles)
            {
                Assert.That(profile.TryValidate(out var error), Is.True, error);
                Assert.That(profile.Groups, Is.Not.Empty);
                Assert.That(profile.Groups.SelectMany(group => group.PresentationSlots), Is.Not.Empty);
            }
        }

        [Test]
        public void DurationEffect_RejectsZeroDurationBeforeSave()
        {
            var effect = Effect("attack_up", MonsterSkillEffectType.AttackBuff, 0.25f, 0f);
            var group = Group("group_01", MonsterSkillTargetType.AllAllies, effect);
            var profile = Profile("invalid_duration", MonsterEffectActiveRole.Support, group);

            try
            {
                Assert.That(profile.TryValidate(out var error), Is.False);
                StringAssert.Contains("지속 시간", error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void CompiledSkill_RejectsPresentationThatDiffersFromProfileContract()
        {
            var effect = Effect("heal", MonsterSkillEffectType.Heal, 1f, 0f);
            var contract = new MonsterActivePresentationSlot();
            contract.EditorConfigure(
                "apply",
                "효과 적용",
                MonsterActivePresentationEvent.AreaResolved,
                MonsterActivePresentationAnchor.AreaCenter);
            var group = Group("group_01", MonsterSkillTargetType.AllAllies, effect, contract);
            var profile = Profile("contract_guard", MonsterEffectActiveRole.Support, group);

            var compiledSlot = new MonsterActiveAttackPresentationCueBinding();
            compiledSlot.EditorConfigure(
                "apply",
                MonsterActivePresentationEvent.AreaResolved,
                MonsterActivePresentationAnchor.CasterRoot,
                null);
            var binding = new MonsterEffectActivePresentationBinding();
            binding.EditorConfigure(group.GroupId, new[] { compiledSlot });
            var skill = ScriptableObject.CreateInstance<MonsterEffectActiveSkill>();
            skill.EditorConfigure(
                "contract_guard_skill",
                "계약 검증",
                "원본 계약과 런타임 연결의 일치를 검사합니다.",
                null,
                profile,
                new[] { binding },
                1000,
                0.25f,
                false);

            try
            {
                Assert.That(skill.TryValidate(out var error), Is.False);
                StringAssert.Contains("원본과 다릅니다", error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(skill);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void MonsterMaker_EffectSelectionIsExclusiveAndSyncsGroupContracts()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            const string draftPath =
                "Assets/ProjectMT/Editor/MonsterMaker/Drafts/Draft_pango_01.asset";
            const string attackPath =
                "Assets/ProjectMT/02_Shared/Unit/Data/ActiveAttackProfiles/AAP_GaleDance.asset";
            var source = AssetDatabase.LoadMainAssetAtPath(draftPath) as ScriptableObject;
            var attack = AssetDatabase.LoadAssetAtPath<MonsterActiveAttackProfile>(attackPath);
            var effect = AssetDatabase.LoadAssetAtPath<MonsterEffectActiveProfile>(ExamplePaths[0]);
            Assert.That(source, Is.Not.Null);
            Assert.That(attack, Is.Not.Null);
            Assert.That(effect, Is.Not.Null);

            var draft = UnityEngine.Object.Instantiate(source);
            try
            {
                var draftType = draft.GetType();
                draftType.GetMethod("EditorSetActiveAttackProfile", flags)
                    .Invoke(draft, new object[] { attack });
                draftType.GetMethod("EditorSetActiveEffectProfile", flags)
                    .Invoke(draft, new object[] { effect });

                Assert.That(draftType.GetProperty("ActiveAttackProfile", flags).GetValue(draft),
                    Is.Null, "효과형 선택 시 공격형 연결은 남지 않아야 합니다.");
                Assert.That(draftType.GetProperty("ActiveEffectProfile", flags).GetValue(draft),
                    Is.SameAs(effect));

                var serialized = new SerializedObject(draft);
                var attackPresentations = serialized.FindProperty("activeAttackPresentations");
                var effectPresentations = serialized.FindProperty("activeEffectPresentations");
                Assert.That(attackPresentations.arraySize, Is.Zero);
                Assert.That(effectPresentations.arraySize, Is.EqualTo(effect.Groups.Count));
                for (var index = 0; index < effect.Groups.Count; index++)
                {
                    var presentation = effectPresentations.GetArrayElementAtIndex(index);
                    Assert.That(presentation.FindPropertyRelative("stepId").stringValue,
                        Is.EqualTo(effect.Groups[index].GroupId));
                    Assert.That(presentation.FindPropertyRelative("slots").arraySize,
                        Is.EqualTo(effect.Groups[index].PresentationSlots.Count));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void MonsterMakerV2_EffectModeShowsOnlyRelevantAuthoringControls()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            const string draftPath =
                "Assets/ProjectMT/Editor/MonsterMaker/Drafts/Draft_pango_01.asset";
            var source = AssetDatabase.LoadMainAssetAtPath(draftPath) as ScriptableObject;
            var effect = AssetDatabase.LoadAssetAtPath<MonsterEffectActiveProfile>(ExamplePaths[2]);
            Assert.That(source, Is.Not.Null);
            Assert.That(effect, Is.Not.Null);

            var draft = UnityEngine.Object.Instantiate(source);
            try
            {
                draft.GetType().GetMethod("EditorSetActiveEffectProfile", flags)
                    .Invoke(draft, new object[] { effect });
                var serialized = new SerializedObject(draft);
                var root = new VisualElement();
                var bindingRoot = new VisualElement { name = "draft-scroll" };
                var skills = new VisualElement { name = "content-skills" };
                root.Add(bindingRoot);
                bindingRoot.Add(skills);

                var viewType = FindEditorType(
                    "ProjectMT.EditorTools.MonsterMakerV2.MonsterMakerV2AuthoringView");
                var view = Activator.CreateInstance(
                    viewType,
                    flags,
                    null,
                    new object[] { root, null, null, null, null, null, null, null, null },
                    null);
                viewType.GetField("serializedDraft", flags).SetValue(view, serialized);
                viewType.GetField("draft", flags).SetValue(view, draft);
                viewType.GetMethod("BuildSkills", flags).Invoke(view, null);

                Assert.That(skills.Q<PropertyField>("field-activeEffectProfile"), Is.Null);
                Assert.That(skills.Q<PropertyField>("field-activeAttackProfile"), Is.Null);
                var buttons = skills.Query<Button>().ToList().Select(button => button.text).ToArray();
                Assert.That(buttons, Does.Contain("● 효과형 · 지원/수호/디버프"));
                Assert.That(buttons, Does.Contain("액티브 스킬 변경"));
                Assert.That(buttons, Does.Contain("효과형 조립소 열기"));
                Assert.That(buttons, Does.Contain("프로필 묶음 다시 동기화"));
                Assert.That(buttons, Does.Not.Contain("1번 모션 설정을 전체 Step에 적용"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(draft);
            }
        }

        [Test]
        public void Workshop_MinimumSizeKeepsColumnsAndControlsInsideBounds()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var windowType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterEffectActiveWorkshopWindow");
            var window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<MonsterEffectActiveProfile>(ExamplePaths[1]);
                window.position = new Rect(70f, 70f, 1100f, 700f);
                windowType.GetMethod("LoadProfile", flags).Invoke(window, new object[] { source });
                window.ShowUtility();
                window.SendEvent(new Event { type = EventType.Layout });
                window.SendEvent(new Event { type = EventType.Repaint });

                Rect ReadRect(string field) =>
                    (Rect)windowType.GetField(field, flags).GetValue(window);
                Assert.That(window.minSize, Is.EqualTo(new Vector2(1100f, 700f)));
                Assert.That(ReadRect("lastAssemblerContentRect").width, Is.EqualTo(450f).Within(0.1f));
                Assert.That(ReadRect("lastAssemblerViewportRect").width, Is.GreaterThanOrEqualTo(450f));
                Assert.That(ReadRect("lastGroupHeaderRightmostRect").xMax, Is.LessThanOrEqualTo(450.1f));
                Assert.That(ReadRect("lastSaveRightmostRect").xMax, Is.LessThanOrEqualTo(765.1f));
                var preview = ReadRect("lastPreviewColumnRect");
                Assert.That(preview.xMax, Is.LessThanOrEqualTo(1100.1f));
                Assert.That(ReadRect("lastPreviewToolbarRightmostRect").xMax,
                    Is.LessThanOrEqualTo(preview.xMax + 0.1f));
                Assert.That(((Vector2)windowType.GetField("assemblerScroll", flags).GetValue(window)).x,
                    Is.Zero);
                Assert.That(((Vector2)windowType.GetField("libraryScroll", flags).GetValue(window)).x,
                    Is.Zero);
            }
            finally
            {
                if (window != null)
                    window.DiscardChanges();
                MonsterEditorWindowTestUtility.Close(window);
            }
            MonsterEditorWindowTestUtility.AssertNoOrphanedContainers("액티브 스킬 조립소");
        }

        [Test]
        public void Workshop_StructuralHandlersCommitAndKeepIdsUnique()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var windowType = FindEditorType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterEffectActiveWorkshopWindow");
            var window = ScriptableObject.CreateInstance(windowType) as EditorWindow;
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<MonsterEffectActiveProfile>(ExamplePaths[1]);
                windowType.GetMethod("LoadProfile", flags).Invoke(window, new object[] { source });
                window.ShowUtility();
                var serialized = (SerializedObject)windowType.GetField("serializedProfile", flags)
                    .GetValue(window);
                var working = (MonsterEffectActiveProfile)windowType.GetField("profile", flags)
                    .GetValue(window);

                SerializedProperty Groups()
                {
                    serialized.UpdateIfRequiredOrScript();
                    return serialized.FindProperty("groups");
                }
                object Invoke(string method, params object[] arguments)
                {
                    var handler = windowType.GetMethod(method, flags);
                    Assert.That(handler, Is.Not.Null, method);
                    return handler.Invoke(window, arguments);
                }

                var originalGroupCount = working.Groups.Count;
                Invoke("AddGroup", Groups());
                Assert.That(working.Groups.Count, Is.EqualTo(originalGroupCount + 1), "묶음 추가");
                Assert.That(working.Groups.Select(group => group.GroupId).Distinct().Count(),
                    Is.EqualTo(working.Groups.Count), "묶음 추가 ID");

                Invoke("DuplicateGroupAndCommit", Groups(), 0, working.Groups[0].DisplayName);
                Assert.That(working.Groups.Count, Is.EqualTo(originalGroupCount + 2), "묶음 복제");
                Assert.That(working.Groups.Select(group => group.GroupId).Distinct().Count(),
                    Is.EqualTo(working.Groups.Count), "묶음 복제 ID");

                var beforeMove = working.Groups[0].GroupId;
                Invoke("MoveGroupAndCommit", Groups(), 0, 1);
                Assert.That(working.Groups[1].GroupId, Is.EqualTo(beforeMove), "묶음 이동");

                var effects = Groups().GetArrayElementAtIndex(0).FindPropertyRelative("effects");
                var effectCount = working.Groups[0].Effects.Count;
                Invoke("AddEffect", effects);
                Assert.That(working.Groups[0].Effects.Count, Is.EqualTo(effectCount + 1), "효과 추가");
                effects = Groups().GetArrayElementAtIndex(0).FindPropertyRelative("effects");
                Invoke("DeleteEffectAndCommit", effects, effects.arraySize - 1);
                Assert.That(working.Groups[0].Effects.Count, Is.EqualTo(effectCount), "효과 삭제");

                var slots = Groups().GetArrayElementAtIndex(0).FindPropertyRelative("presentationSlots");
                var slotCount = working.Groups[0].PresentationSlots.Count;
                Invoke("AddSlot", slots, false);
                Assert.That(working.Groups[0].PresentationSlots.Count, Is.EqualTo(slotCount + 1),
                    "VFX/SFX 계약 추가");
                Assert.That(working.Groups[0].PresentationSlots.Select(slot => slot.SlotId).Distinct().Count(),
                    Is.EqualTo(working.Groups[0].PresentationSlots.Count), "VFX/SFX 계약 ID");
                slots = Groups().GetArrayElementAtIndex(0).FindPropertyRelative("presentationSlots");
                Invoke("DeleteSlotAndCommit", slots, slots.arraySize - 1);
                Assert.That(working.Groups[0].PresentationSlots.Count, Is.EqualTo(slotCount),
                    "VFX/SFX 계약 삭제");

                Invoke("DeleteGroupAndCommit", Groups(), working.Groups.Count - 1);
                Assert.That(working.Groups.Count, Is.EqualTo(originalGroupCount + 1), "묶음 삭제");
                Assert.That(window.hasUnsavedChanges, Is.True);
            }
            finally
            {
                if (window != null)
                    window.DiscardChanges();
                MonsterEditorWindowTestUtility.Close(window);
            }
            MonsterEditorWindowTestUtility.AssertNoOrphanedContainers("액티브 스킬 조립소");
        }

        private static Type FindEditorType(string fullName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }
        private static MonsterSkillEffect Effect(
            string id,
            MonsterSkillEffectType type,
            float magnitude,
            float duration)
        {
            var effect = new MonsterSkillEffect();
            effect.EditorConfigure(
                id,
                type,
                MonsterSkillValueSource.Flat,
                magnitude,
                duration);
            return effect;
        }

        private static MonsterEffectActiveGroup Group(
            string id,
            MonsterSkillTargetType target,
            MonsterSkillEffect effect,
            params MonsterActivePresentationSlot[] slots)
        {
            var group = new MonsterEffectActiveGroup();
            group.EditorConfigure(id, id, 0f, target, true, 5f, 8, new[] { effect }, slots);
            return group;
        }

        private static MonsterEffectActiveProfile Profile(
            string id,
            MonsterEffectActiveRole role,
            params MonsterEffectActiveGroup[] groups)
        {
            var profile = ScriptableObject.CreateInstance<MonsterEffectActiveProfile>();
            profile.EditorConfigure(id, id, "테스트 프로필", role, groups);
            return profile;
        }
    }
}
