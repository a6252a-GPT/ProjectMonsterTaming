using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ProjectMT.Core.SaveIO;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectMT.Tests.EditMode
{
    public sealed class MonsterProductionContractTests // 정식 Monster Profile·검증·Snapshot 계약
    {
        [Test]
        public void LegacyDefinition_RemainsValidWithoutFormalRuntimeAssets()
        {
            var definition = ScriptableObject.CreateInstance<MonsterDefinition>();
            var preview = new GameObject("LegacyPreview");
            var texture = new Texture2D(2, 2);
            var portrait = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f);
            try
            {
                definition.EditorConfigure("legacy_01", 100f, 10f, 0f, 1f, 2f, 1f, false);
                definition.EditorConfigurePresentation("레거시 몬스터", portrait, preview);

                Assert.That(definition.TryValidate(out var error), Is.True, error);
                Assert.That(definition.UsesFormalRuntime, Is.False);
                Assert.That(MonsterDefinitionValidator.Validate(definition, false).HasErrors, Is.False);
                Assert.That(MonsterDefinitionValidator.Validate(definition, true).HasErrors, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(portrait);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(preview);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void FormalDefinition_RequiresSortedMarkersWhosePowerSumsToOne()
        {
            using var fixture = FormalMonsterFixture.Create("marker_test", 0);
            fixture.AttackMotion.EditorConfigure(
                "bite",
                fixture.AttackClip,
                1f,
                0.05f,
                1f,
                false,
                new[]
                {
                    CreateMarker(0.6f, 0.5f),
                    CreateMarker(0.4f, 0.5f)
                });

            Assert.That(fixture.MotionProfile.TryValidate(out var error), Is.False);
            StringAssert.Contains("sorted", error);

            fixture.AttackMotion.EditorConfigure(
                "bite",
                fixture.AttackClip,
                1f,
                0.05f,
                1f,
                false,
                new[]
                {
                    CreateMarker(0.4f, 0.4f),
                    CreateMarker(0.6f, 0.4f)
                });

            Assert.That(fixture.MotionProfile.TryValidate(out error), Is.False);
            StringAssert.Contains("sum to 1", error);
        }

        [Test]
        public void AutoActiveAbility_RequiresExplicitTriggerPolicy()
        {
            var ability = ScriptableObject.CreateInstance<MonsterAbilityDefinition>();
            try
            {
                ability.EditorConfigure("ability_01", "자동 스킬", MonsterAbilityMode.AutoActive, null);
                Assert.That(ability.TryValidate(out var error), Is.False);
                StringAssert.Contains("Trigger Policy", error);

                ability.EditorConfigure(
                    "ability_01",
                    "자동 스킬",
                    MonsterAbilityMode.AutoActive,
                    "team_confirmed_trigger");
                Assert.That(ability.TryValidate(out error), Is.True, error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ability);
            }
        }

        [Test]
        public async Task FormalSnapshot_ResolvesAscensionStatsAbilitiesAndRuntimeAssets()
        {
            using var fixture = FormalMonsterFixture.Create("formal_01", 3);
            var catalog = ScriptableObject.CreateInstance<MonsterCatalog>();
            catalog.EditorSetDefinitions(new[] { fixture.Definition });
            var store = new MemoryFileStore(Encoding.UTF8.GetBytes(
                "{\"dataVersion\":5,\"gameData\":{\"monsters\":{" +
                "\"ownedMonsters\":[{\"monsterId\":\"formal_01\",\"level\":1," +
                "\"ascensionLevel\":3}],\"mainPartySlots\":[\"formal_01\"]," +
                "\"reservePartySlots\":[]}}}"));
            try
            {
                var progress = await new SaveService(store, "memory://formal-monster").LoadAsync();
                var party = new BattlePartySnapshotBuilder(catalog).Build(new GameProgressView(progress));
                var unit = party.Units[0];

                Assert.That(unit.RuntimeAssetKey, Is.EqualTo("formal_01"));
                Assert.That(unit.RuntimeAssetSet, Is.SameAs(fixture.RuntimeAssetSet));
                Assert.That(unit.UnlockedAbilityIds, Is.EqualTo(new[] { "passive_02" }));
                Assert.That(unit.Stats.maxHealth, Is.EqualTo(130f).Within(0.001f));
                Assert.That(unit.Stats.damage, Is.EqualTo(13f).Within(0.001f));
                Assert.That(unit.Stats.ranged, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void FormalDefinition_ValidatesWithoutChoosingAnimationsAutomatically()
        {
            using var fixture = FormalMonsterFixture.Create("formal_valid", 0);

            var report = MonsterDefinitionValidator.Validate(fixture.Definition, true);

            Assert.That(report.HasErrors, Is.False);
            Assert.That(fixture.MotionProfile.Idle.Clip, Is.SameAs(fixture.IdleClip));
            Assert.That(fixture.MotionProfile.Move.Clip, Is.SameAs(fixture.MoveClip));
            Assert.That(fixture.MotionProfile.Attacks[0].Clip, Is.SameAs(fixture.AttackClip));
            Assert.That(fixture.MotionProfile.Death.Clip, Is.SameAs(fixture.DeathClip));
        }

        [Test]
        public void MarkerEvaluator_HandlesFrameSkipsAndNeverRepeatsAMarker()
        {
            var markers = new[]
            {
                CreateMarker(0f, 0.2f),
                CreateMarker(0.35f, 0.3f),
                CreateMarker(0.7f, 0.5f)
            };
            var passed = new List<int>();
            var nextMarker = 0;

            MonsterAttackMarkerEvaluator.EvaluatePassed(
                markers,
                -0.001f,
                0f,
                ref nextMarker,
                (index, marker) => passed.Add(index));
            MonsterAttackMarkerEvaluator.EvaluatePassed(
                markers,
                0f,
                0.8f,
                ref nextMarker,
                (index, marker) => passed.Add(index));
            MonsterAttackMarkerEvaluator.EvaluatePassed(
                markers,
                0.8f,
                1f,
                ref nextMarker,
                (index, marker) => passed.Add(index));

            Assert.That(passed, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(nextMarker, Is.EqualTo(markers.Length));
        }

        [Test]
        public void AttackPlaybackSpeed_UsesTheSameAuthoredAndIntervalFitRule()
        {
            var clip = new AnimationClip();
            try
            {
                clip.SetCurve(
                    string.Empty,
                    typeof(Transform),
                    "m_LocalPosition.x",
                    AnimationCurve.Linear(0f, 0f, 2f, 1f));

                Assert.That(
                    MonsterAnimationDriver.ResolveAttackPlaybackSpeed(clip, 1f, 0.5f),
                    Is.EqualTo(4f).Within(0.0001f));
                Assert.That(
                    MonsterAnimationDriver.ResolveAttackPlaybackSpeed(clip, 5f, 0.5f),
                    Is.EqualTo(5f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void FormalRuntime_RejectsEmptySfxCueAndUnknownMarkerSocket()
        {
            using var fixture = FormalMonsterFixture.Create("feedback_socket_test", 0);
            var emptySfx = ScriptableObject.CreateInstance<SfxCue>();
            try
            {
                Assert.That(emptySfx.HasPlayableClip, Is.False);
                var feedback = new MonsterFeedbackCue();
                feedback.EditorConfigure(emptySfx, null);
                Assert.That(feedback.TryValidate(out var feedbackError), Is.False);
                StringAssert.Contains("no playable AudioClip", feedbackError);

                var marker = new MonsterAttackMarker();
                marker.EditorConfigure(0.5f, 1f, feedback, "Visual/MissingSocket");
                fixture.AttackMotion.EditorConfigure(
                    "bite",
                    fixture.AttackClip,
                    1f,
                    0.05f,
                    1f,
                    false,
                    new[] { marker });

                Assert.That(fixture.RuntimeAssetSet.TryValidate(out var runtimeError), Is.False);
                StringAssert.Contains("feedback", runtimeError.ToLowerInvariant());

                marker.EditorConfigure(0.5f, 1f, null, "Visual/MissingSocket");
                Assert.That(fixture.RuntimeAssetSet.TryValidate(out runtimeError), Is.False);
                StringAssert.Contains("socket path", runtimeError.ToLowerInvariant());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(emptySfx);
            }
        }

        [Test]
        public void FormalDefinition_RejectsNonAsciiStableId()
        {
            using var fixture = FormalMonsterFixture.Create("몬스터_01", 0);

            var report = MonsterDefinitionValidator.Validate(fixture.Definition, true);

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues.Select(issue => issue.Code), Does.Contain("MON-ID-CHAR"));
        }

        [Test]
        public void SpikeMakerOutput_IsRegisteredOnceAndKeepsTheManuallyChosenContract()
        {
            const string definitionPath =
                "Assets/ProjectMT/02_Shared/Unit/Data/Monsters/spike_01/MD_spike_01.asset";
            const string adapterPath =
                "Assets/ProjectMT/05_Art/Monsters/spike_01/PF_spike_01_VisualAdapter.prefab";
            const string controllerPath =
                "Assets/ProjectMT/05_Art/Monsters/spike_01/AC_spike_01.controller";
            const string draftPath =
                "Assets/ProjectMT/Editor/MonsterMaker/Drafts/Draft_spike_01.asset";
            var definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(definitionPath);
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(
                "Assets/ProjectMT/02_Shared/Unit/Data/MonsterCatalog.asset");
            var rarityCatalog = AssetDatabase.LoadAssetAtPath<MonsterRarityCatalog>(
                "Assets/ProjectMT/02_Shared/Unit/Data/MonsterRarityCatalog.asset");

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.MonsterId, Is.EqualTo("spike_01"));
            Assert.That(definition.UsesFormalRuntime, Is.True);
            Assert.That(MonsterDefinitionValidator.Validate(definition, true).HasErrors, Is.False);
            Assert.That(catalog.Definitions.Count(candidate => candidate != null && candidate.MonsterId == "spike_01"), Is.EqualTo(1));
            Assert.That(rarityCatalog.TryGetRarity("spike_01", out var rarity), Is.True);
            Assert.That(rarity, Is.EqualTo(MonsterRarity.Common));

            var runtime = definition.RuntimeAssetSet;
            var motion = runtime.MotionProfile;
            Assert.That(motion.Idle.Clip.name, Is.EqualTo("Idle"));
            Assert.That(motion.Move.Clip.name, Is.EqualTo("Walk Forward In Place"));
            Assert.That(motion.Attacks, Has.Length.EqualTo(1));
            Assert.That(motion.Attacks[0].MotionId, Is.EqualTo("claw"));
            Assert.That(motion.Attacks[0].Clip.name, Is.EqualTo("Claw Attack"));
            Assert.That(motion.Attacks[0].Markers, Has.Length.EqualTo(1));
            Assert.That(motion.Attacks[0].Markers[0].NormalizedTime, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(motion.Attacks[0].Markers[0].PowerRatio, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(motion.Death.Clip.name, Is.EqualTo("Die"));
            Assert.That(runtime.CombatProfile.CombatType, Is.EqualTo(MonsterCombatType.Melee));
            Assert.That(runtime.CombatProfile.Action, Is.TypeOf<MeleeActionDefinition>());
            Assert.That(((MeleeActionDefinition)runtime.CombatProfile.Action).Mode, Is.EqualTo(MonsterMeleeAttackMode.Single));

            var visual = runtime.VisualAdapterPrefab.transform.Find("Visual");
            Assert.That(visual, Is.Not.Null);
            var vendorSource = PrefabUtility.GetCorrespondingObjectFromSource(visual.gameObject);
            var vendorPath = AssetDatabase.GetAssetPath(vendorSource);
            StringAssert.Contains("Monsters Ultimate Pack 02 Cute Series", vendorPath);
            StringAssert.Contains("Spike Cute Series/Prefabs/Spike.prefab", vendorPath);
            StringAssert.DoesNotContain("Wolf", vendorPath);
            Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(runtime.VisualAdapterPrefab), Is.Zero);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            var stateNames = controller.layers[0].stateMachine.states
                .Select(child => child.state.name)
                .OrderBy(name => name)
                .ToArray();
            Assert.That(stateNames, Is.EqualTo(new[] { "Attack_claw", "Death", "Idle", "Move" }));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(draftPath), Is.Not.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(definitionPath), Is.EqualTo("67cb9560caba5d348af985604ecd1943"));
            Assert.That(AssetDatabase.AssetPathToGUID(adapterPath), Is.EqualTo("7b1b752814782e24a85a3f05363652ca"));
            Assert.That(AssetDatabase.AssetPathToGUID(controllerPath), Is.EqualTo("55739ba60f619784f94794e6fd0bbf50"));
            Assert.That(AssetDatabase.AssetPathToGUID(draftPath), Is.EqualTo("39d6ce14f953fc74e84cf00c81f577fe"));
        }

        [Test]
        public void ShellMakerOutput_UsesMiniMonsterSoundOnlyAtTheManualAttackMarker()
        {
            const string definitionPath =
                "Assets/ProjectMT/02_Shared/Unit/Data/Monsters/shell_01/MD_shell_01.asset";
            const string adapterPath =
                "Assets/ProjectMT/05_Art/Monsters/shell_01/PF_shell_01_VisualAdapter.prefab";
            const string controllerPath =
                "Assets/ProjectMT/05_Art/Monsters/shell_01/AC_shell_01.controller";
            const string draftPath =
                "Assets/ProjectMT/Editor/MonsterMaker/Drafts/Draft_shell_01.asset";
            const string cuePath =
                "Assets/ProjectMT/06_Audio/SFX/Monsters/SFX_shell_01_Attack.asset";
            var definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(definitionPath);
            var cue = AssetDatabase.LoadAssetAtPath<SfxCue>(cuePath);
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(
                "Assets/ProjectMT/02_Shared/Unit/Data/MonsterCatalog.asset");
            var rarityCatalog = AssetDatabase.LoadAssetAtPath<MonsterRarityCatalog>(
                "Assets/ProjectMT/02_Shared/Unit/Data/MonsterRarityCatalog.asset");

            Assert.That(definition, Is.Not.Null);
            Assert.That(cue, Is.Not.Null);
            Assert.That(definition.MonsterId, Is.EqualTo("shell_01"));
            Assert.That(definition.DisplayName, Is.EqualTo("쉘"));
            Assert.That(definition.UsesFormalRuntime, Is.True);
            Assert.That(MonsterDefinitionValidator.Validate(definition, true).HasErrors, Is.False);
            Assert.That(catalog.Definitions.Count(candidate => candidate != null && candidate.MonsterId == "shell_01"), Is.EqualTo(1));
            Assert.That(rarityCatalog.TryGetRarity("shell_01", out var rarity), Is.True);
            Assert.That(rarity, Is.EqualTo(MonsterRarity.Common));

            var runtime = definition.RuntimeAssetSet;
            var motion = runtime.MotionProfile;
            Assert.That(motion.Idle.Clip.name, Is.EqualTo("Idle"));
            Assert.That(motion.Move.Clip.name, Is.EqualTo("Walk Forward In Place"));
            Assert.That(motion.Death.Clip.name, Is.EqualTo("Die"));
            Assert.That(motion.Attacks, Has.Length.EqualTo(1));
            Assert.That(motion.Attacks[0].MotionId, Is.EqualTo("bite01"));
            Assert.That(motion.Attacks[0].Clip.name, Is.EqualTo("Bite Attack"));
            Assert.That(motion.Attacks[0].Markers, Has.Length.EqualTo(1));
            var marker = motion.Attacks[0].Markers[0];
            Assert.That(marker.NormalizedTime, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(marker.PowerRatio, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(marker.FeedbackOverride, Is.Not.Null);
            Assert.That(marker.FeedbackOverride.Sfx, Is.SameAs(cue));
            Assert.That(marker.FeedbackOverride.VfxPrefab, Is.Null);
            Assert.That(motion.Attacks[0].AttackStartOverride?.HasAnyFeedback ?? false, Is.False);
            Assert.That(motion.Death.StartFeedback?.HasAnyFeedback ?? false, Is.False);

            var feedback = runtime.FeedbackProfile;
            Assert.That(feedback.Spawn?.HasAnyFeedback ?? false, Is.False);
            Assert.That(feedback.AttackStart?.HasAnyFeedback ?? false, Is.False);
            Assert.That(feedback.AttackMarker?.HasAnyFeedback ?? false, Is.False);
            Assert.That(feedback.HitReceived?.HasAnyFeedback ?? false, Is.False);
            Assert.That(feedback.Death?.HasAnyFeedback ?? false, Is.False);
            Assert.That(feedback.Special?.HasAnyFeedback ?? false, Is.False);
            Assert.That(cue.TrySelectClip(out var selectedClip), Is.True);
            Assert.That(cue.HasPlayableClip, Is.True);
            Assert.That(AssetDatabase.GetAssetPath(selectedClip),
                Is.EqualTo("Assets/ThirdParty/11_사운드/PRINCIPLE SOUND DESIGN - Mini Monsters/Mini Cutie/monster_mini_cutie_attack_fast_1.wav"));
            Assert.That(cue.SpatialBlend, Is.EqualTo(1f).Within(0.0001f));

            var visual = runtime.VisualAdapterPrefab.transform.Find("Visual");
            Assert.That(visual, Is.Not.Null);
            var vendorSource = PrefabUtility.GetCorrespondingObjectFromSource(visual.gameObject);
            var vendorPath = AssetDatabase.GetAssetPath(vendorSource);
            StringAssert.Contains("Shell Cute Series/Prefabs/Shell.prefab", vendorPath);
            StringAssert.DoesNotContain("Wolf", vendorPath);
            Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(runtime.VisualAdapterPrefab), Is.Zero);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            var stateNames = controller.layers[0].stateMachine.states
                .Select(child => child.state.name)
                .OrderBy(name => name)
                .ToArray();
            Assert.That(stateNames, Is.EqualTo(new[] { "Attack_bite01", "Death", "Idle", "Move" }));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(draftPath), Is.Not.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(definitionPath), Is.EqualTo("c42578432c454694293379b550c7b6cc"));
            Assert.That(AssetDatabase.AssetPathToGUID(adapterPath), Is.EqualTo("133314aff81555c4d8e7eb95fa724409"));
            Assert.That(AssetDatabase.AssetPathToGUID(controllerPath), Is.EqualTo("d5f53efe17b7a954e8c4eb3851f15d83"));
            Assert.That(AssetDatabase.AssetPathToGUID(draftPath), Is.EqualTo("1b13c8d9fbe1fc542a169b83e6107cd7"));
            Assert.That(AssetDatabase.AssetPathToGUID(cuePath), Is.EqualTo("abb5f609150fe6740b7749f03d1c4422"));
        }

        private static MonsterAttackMarker CreateMarker(float normalizedTime, float powerRatio)
        {
            var marker = new MonsterAttackMarker();
            marker.EditorConfigure(normalizedTime, powerRatio);
            return marker;
        }

        private sealed class FormalMonsterFixture : IDisposable
        {
            private readonly List<UnityEngine.Object> ownedObjects = new List<UnityEngine.Object>();

            public MonsterDefinition Definition { get; private set; }
            public MonsterRuntimeAssetSet RuntimeAssetSet { get; private set; }
            public MonsterMotionProfile MotionProfile { get; private set; }
            public MonsterAttackMotion AttackMotion { get; private set; }
            public AnimationClip IdleClip { get; private set; }
            public AnimationClip MoveClip { get; private set; }
            public AnimationClip AttackClip { get; private set; }
            public AnimationClip DeathClip { get; private set; }

            public static FormalMonsterFixture Create(string monsterId, int ignoredAscensionLevel)
            {
                var fixture = new FormalMonsterFixture();
                fixture.Build(monsterId);
                return fixture;
            }

            public void Dispose()
            {
                for (var index = ownedObjects.Count - 1; index >= 0; index--)
                {
                    if (ownedObjects[index] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(ownedObjects[index]);
                    }
                }
            }

            private void Build(string monsterId)
            {
                var preview = Own(new GameObject("FormalPreview"));
                var adapter = Own(new GameObject("FormalAdapter"));
                var attackOrigin = new GameObject("AttackOrigin");
                attackOrigin.transform.SetParent(adapter.transform, false);
                var hitCenter = new GameObject("HitCenter");
                hitCenter.transform.SetParent(adapter.transform, false);
                var animator = adapter.AddComponent<Animator>();
                var driver = adapter.AddComponent<MonsterAnimationDriver>();
                driver.EditorConfigure(
                    animator,
                    adapter.transform,
                    attackOrigin.transform,
                    hitCenter.transform);
                adapter.AddComponent<UnitActor>();
                var texture = Own(new Texture2D(2, 2));
                var portrait = Own(Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f));
                var controller = Own(new AnimatorOverrideController());
                var body = Own(ScriptableObject.CreateInstance<MonsterBodyProfile>());
                MotionProfile = Own(ScriptableObject.CreateInstance<MonsterMotionProfile>());
                var melee = Own(ScriptableObject.CreateInstance<MeleeActionDefinition>());
                var combat = Own(ScriptableObject.CreateInstance<MonsterCombatProfile>());
                var milestone2 = Own(ScriptableObject.CreateInstance<MonsterAbilityDefinition>());
                var milestone4 = Own(ScriptableObject.CreateInstance<MonsterAbilityDefinition>());
                var ascension = Own(ScriptableObject.CreateInstance<MonsterAscensionProfile>());
                var feedback = Own(ScriptableObject.CreateInstance<MonsterFeedbackProfile>());
                RuntimeAssetSet = Own(ScriptableObject.CreateInstance<MonsterRuntimeAssetSet>());
                Definition = Own(ScriptableObject.CreateInstance<MonsterDefinition>());

                IdleClip = Own(new AnimationClip { name = "Idle" });
                MoveClip = Own(new AnimationClip { name = "Move" });
                AttackClip = Own(new AnimationClip { name = "Attack" });
                DeathClip = Own(new AnimationClip { name = "Death" });

                body.EditorConfigure(
                    Vector3.one,
                    Vector3.zero,
                    0f,
                    0f,
                    0.5f,
                    1f,
                    0.65f,
                    1.2f,
                    string.Empty,
                    "AttackOrigin",
                    "HitCenter",
                    MonsterRigMode.Generic,
                    1f,
                    1f);

                var idle = new MonsterMotionSlot();
                idle.EditorConfigure(IdleClip, 1f, 0.08f, true);
                var move = new MonsterMotionSlot();
                move.EditorConfigure(MoveClip, 1f, 0.08f, true);
                var death = new MonsterMotionSlot();
                death.EditorConfigure(DeathClip, 1f, 0.08f, false);
                AttackMotion = new MonsterAttackMotion();
                AttackMotion.EditorConfigure(
                    "bite",
                    AttackClip,
                    1f,
                    0.05f,
                    1f,
                    false,
                    new[] { CreateMarker(0.5f, 1f) });
                MotionProfile.EditorConfigure(idle, move, new[] { AttackMotion }, death);

                melee.EditorConfigure(MonsterMeleeAttackMode.Single, 1f, 1);
                combat.EditorConfigure(MonsterCombatType.Melee, melee);
                milestone2.EditorConfigure("passive_02", "2돌파 패시브", MonsterAbilityMode.Passive, null);
                milestone4.EditorConfigure(
                    "active_04",
                    "4돌파 액티브",
                    MonsterAbilityMode.AutoActive,
                    "team_confirmed_trigger");
                ascension.EditorConfigure(
                    new MonsterStatModifier(0.1f, 0.1f, 0f, 0f, 0f, 0f),
                    milestone2,
                    new MonsterStatModifier(0.2f, 0.2f, 0f, 0f, 0f, 0f),
                    milestone4,
                    new MonsterStatModifier(0.3f, 0.3f, 0f, 0f, 0f, 0f));
                RuntimeAssetSet.EditorConfigure(adapter, controller, body, MotionProfile, combat, ascension, feedback);

                Definition.EditorConfigure(monsterId, 100f, 10f, 0f, 1f, 2f, 1.2f, false);
                Definition.EditorConfigurePresentation("정식 몬스터", portrait, preview);
                Definition.EditorConfigureFormalRuntime(monsterId, RuntimeAssetSet);
            }

            private T Own<T>(T value) where T : UnityEngine.Object
            {
                ownedObjects.Add(value);
                return value;
            }
        }

        private sealed class MemoryFileStore : IAtomicFileStore
        {
            public MemoryFileStore(byte[] bytes)
            {
                Bytes = bytes;
            }

            public byte[] Bytes { get; private set; }

            public Task<byte[]> ReadAsync(string path)
            {
                return Task.FromResult(Bytes);
            }

            public Task ReplaceAsync(string path, byte[] replacement)
            {
                Bytes = replacement;
                return Task.CompletedTask;
            }
        }
    }
}
