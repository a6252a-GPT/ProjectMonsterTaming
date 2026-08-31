using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.Tests.EditMode
{
    public sealed class MonsterActiveAttackVfxContractMatrixTests // 액티브 제작소 계약 전수 QA
    {
        private const BindingFlags StaticFlags =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void Templates_CoverEveryPatternVariantWithOnlyReachableContracts()
        {
            var covered = new HashSet<MonsterActiveAttackPattern>();
            foreach (var source in CreateVariants())
            {
                covered.Add(source.Pattern);
                foreach (var teleport in new[] { false, true })
                {
                    var step = source.Clone();
                    step.EditorConfigureTeleport(teleport, 1f);
                    var contracts = Build(step);
                    step.EditorSetPresentationSlots(contracts);

                    Assert.That(contracts.Length, Is.GreaterThan(0), step.StepId);
                    Assert.That(contracts.Select(slot => slot.SlotId)
                            .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                        Is.EqualTo(contracts.Length), step.StepId);
                    Assert.That(step.TryValidate(out var error), Is.True, step.StepId + ": " + error);
                    Assert.That(contracts.All(slot =>
                            MonsterActiveAttackVfxCompatibility.TryValidateSlot(step, slot, out _)),
                        Is.True, step.StepId);
                    Assert.That(contracts.Count(slot =>
                            slot.Attachment == MonsterActivePresentationAttachment.DeliveryVisual),
                        Is.EqualTo(step.IsProjectile ? 1 : 0), step.StepId);
                    Assert.That(contracts.Any(slot =>
                            slot.Timing == MonsterActivePresentationEvent.DeliverySpawn),
                        Is.EqualTo(step.IsProjectile), step.StepId);
                    Assert.That(contracts.Any(slot =>
                            slot.Timing == MonsterActivePresentationEvent.TeleportExit),
                        Is.EqualTo(teleport), step.StepId);
                    Assert.That(contracts.Any(slot =>
                            slot.EndPolicy == MonsterActivePresentationEndPolicy.MotionEnd),
                        Is.False, step.StepId);
                }
            }

            CollectionAssert.AreEquivalent(
                Enum.GetValues(typeof(MonsterActiveAttackPattern))
                    .Cast<MonsterActiveAttackPattern>(),
                covered);
        }

        [Test]
        public void SavedProfiles_AllUseCanonicalReachableContracts()
        {
            var guids = AssetDatabase.FindAssets(
                "t:MonsterActiveAttackProfile",
                new[] { "Assets/ProjectMT/02_Shared/Unit/Data/ActiveAttackProfiles" });
            var requiredBaselineIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "crimson_meteor",
                "crystal_resonance",
                "flame_dragon_rampage",
                "gale_dance",
                "golden_bone_judgment",
                "sky_break",
                "void_lance"
            };
            var stepCount = 0;
            var slotCount = 0;
            Assert.That(guids.Length, Is.GreaterThanOrEqualTo(requiredBaselineIds.Count));
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<MonsterActiveAttackProfile>(path);
                Assert.That(profile, Is.Not.Null, path);
                Assert.That(profile.TryValidate(out var error), Is.True, path + ": " + error);
                requiredBaselineIds.Remove(profile.ProfileId);
                foreach (var step in profile.Steps)
                {
                    AssertCanonical(step, path + "/" + step.StepId);
                    stepCount++;
                    slotCount += step.PresentationSlots.Count;
                }
            }

            Assert.That(requiredBaselineIds, Is.Empty,
                "기준 공격 액티브 프리셋이 누락되었습니다.");
            Assert.That(stepCount, Is.GreaterThanOrEqualTo(20));
            Assert.That(slotCount, Is.GreaterThanOrEqualTo(85));
        }

        [Test]
        public void CreationRoutes_UseCanonicalTemplatesInsteadOfLegacyCommonSlots()
        {
            var raw = Create("raw_line", MonsterActiveAttackPattern.Line,
                MonsterActiveAttackProgression.Instant);
            Assert.That(raw.PresentationSlots, Is.Empty);

            const string path =
                "Assets/ProjectMT/99_Tests/QA/ActiveSkills/AAP_QA_ContractFactory.asset";
            AssetDatabase.DeleteAsset(path);
            var workshop = FindType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterActiveAttackWorkshopWindow");
            var created = (MonsterActiveAttackProfile)workshop
                .GetMethod("CreateProfileAtPath", StaticFlags)
                .Invoke(null, new object[] { path });
            try
            {
                AssertCanonical(created.Steps[0], "Workshop");
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }

            var api = FindType(
                "ProjectMT.EditorTools.MonsterMaker.OptionalApi.MonsterActiveAttackAuthoringApi");
            var build = api.GetMethod("TryBuildProfile", StaticFlags);
            const string json =
                "{\"profileId\":\"api_beam\",\"displayName\":\"API Beam\",\"steps\":[{\"stepId\":\"beam\",\"displayName\":\"Beam\",\"pattern\":\"PiercingBeam\",\"targetPolicy\":\"SameTarget\",\"progression\":\"Instant\"}]}";
            var args = new object[] { json, null, null };
            Assert.That((bool)build.Invoke(null, args), Is.True, args[2] as string);
            var profile = (MonsterActiveAttackProfile)args[1];
            try
            {
                AssertCanonical(profile.Steps[0], "Optional API");
                Assert.That(profile.Steps[0].PresentationSlots
                    .Any(slot => slot.SlotId == "beam_body"), Is.True);
                Assert.That(profile.Steps[0].PresentationSlots
                    .Any(slot => slot.Attachment ==
                                 MonsterActivePresentationAttachment.DeliveryVisual), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Compatibility_RejectsContractsTheExecutorCannotHonor()
        {
            var beam = Create("beam", MonsterActiveAttackPattern.PiercingBeam,
                MonsterActiveAttackProgression.Instant);
            AssertInvalid(beam, Slot(
                "beam_delivery",
                MonsterActivePresentationEvent.DeliverySpawn,
                MonsterActivePresentationAnchor.ProjectileRoot,
                MonsterActivePresentationMultiplicity.OncePerProjectile,
                MonsterActivePresentationAttachment.DeliveryVisual,
                MonsterActivePresentationEndPolicy.DeliveryEnd));

            var projectile = Create("projectile",
                MonsterActiveAttackPattern.PiercingProjectile,
                MonsterActiveAttackProgression.Instant,
                MonsterActiveProjectileFormation.Fan, 3);
            AssertInvalid(projectile, Slot(
                "travel_root",
                MonsterActivePresentationEvent.Travel,
                MonsterActivePresentationAnchor.ProjectileRoot,
                MonsterActivePresentationMultiplicity.OncePerProjectile));
            AssertInvalid(projectile, Slot(
                "damage_stage",
                MonsterActivePresentationEvent.Impact,
                MonsterActivePresentationAnchor.HitPoint,
                MonsterActivePresentationMultiplicity.PerDamageStage));

            var area = Create("area", MonsterActiveAttackPattern.SelfCircle,
                MonsterActiveAttackProgression.Instant);
            AssertInvalid(area, Slot(
                "follow_area",
                MonsterActivePresentationEvent.AreaResolved,
                MonsterActivePresentationAnchor.AreaCenter,
                MonsterActivePresentationMultiplicity.OncePerStep,
                MonsterActivePresentationAttachment.FollowAnchor));
            AssertInvalid(area, Slot(
                "motion_end",
                MonsterActivePresentationEvent.Launch,
                MonsterActivePresentationAnchor.AttackOrigin,
                MonsterActivePresentationMultiplicity.OncePerStep,
                MonsterActivePresentationAttachment.World,
                MonsterActivePresentationEndPolicy.MotionEnd));
        }

        [Test]
        public void FanDirectionsAndExplosionCenters_MatchRuntimeAndPreview()
        {
            var step = Create("fan", MonsterActiveAttackPattern.ExplosiveProjectile,
                MonsterActiveAttackProgression.Instant,
                MonsterActiveProjectileFormation.Fan, 5);
            var preview = FindType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterMakerPreviewStage");
            var previewDirection = preview.GetMethod(
                "ResolveActiveProjectileDirection", StaticFlags);
            var previewArea = preview.GetMethod(
                "ResolveActivePreviewAreaCenter", StaticFlags);
            var executor = new MonsterActiveAttackExecutor();
            typeof(MonsterActiveAttackExecutor).GetField("currentStep", InstanceFlags)
                .SetValue(executor, step);
            var runtimeDirection = typeof(MonsterActiveAttackExecutor)
                .GetMethod("ResolveProjectileDirection", InstanceFlags);
            var targetPoint = Vector3.forward * 3f;
            var centers = new List<Vector3>();

            for (var index = 0; index < step.ProjectileCount; index++)
            {
                var previewValue = (Vector3)previewDirection.Invoke(
                    null, new object[] { step, Vector3.forward, index });
                var runtimeValue = (Vector3)runtimeDirection.Invoke(
                    executor, new object[] { Vector3.forward, index });
                Assert.That(Vector3.Distance(previewValue, runtimeValue),
                    Is.LessThan(0.0001f), "탄 " + index);
                if (index == 0)
                {
                    Assert.That(Vector3.Distance(previewValue, Vector3.forward),
                        Is.LessThan(0.0001f));
                }
                centers.Add((Vector3)previewArea.Invoke(null,
                    new object[]
                    {
                        step, targetPoint, Vector3.zero, Vector3.forward, index
                    }));
            }

            Assert.That(Vector3.Distance(centers[0], targetPoint), Is.LessThan(0.0001f));
            Assert.That(centers.Select(Round).Distinct().Count(),
                Is.EqualTo(step.ProjectileCount));
            Assert.That(centers.Any(center => center.x < -0.01f), Is.True);
            Assert.That(centers.Any(center => center.x > 0.01f), Is.True);
            Assert.That(Build(step).Single(slot => slot.SlotId == "area_explosion")
                .Multiplicity, Is.EqualTo(
                MonsterActivePresentationMultiplicity.OncePerProjectile));
        }

        private static IEnumerable<MonsterActiveAttackStep> CreateVariants()
        {
            yield return Create("line_instant", MonsterActiveAttackPattern.Line,
                MonsterActiveAttackProgression.Instant);
            yield return Create("line_forward", MonsterActiveAttackPattern.Line,
                MonsterActiveAttackProgression.Forward);
            foreach (var progression in new[]
                     {
                         MonsterActiveAttackProgression.Instant,
                         MonsterActiveAttackProgression.Forward,
                         MonsterActiveAttackProgression.LeftToRight,
                         MonsterActiveAttackProgression.RightToLeft
                     })
            {
                yield return Create("cone_" + progression,
                    MonsterActiveAttackPattern.Cone, progression);
            }
            foreach (var progression in new[]
                     {
                         MonsterActiveAttackProgression.Instant,
                         MonsterActiveAttackProgression.Outward
                     })
            {
                yield return Create("self_" + progression,
                    MonsterActiveAttackPattern.SelfCircle, progression);
                yield return Create("front_" + progression,
                    MonsterActiveAttackPattern.FrontCircle, progression);
            }
            foreach (var pattern in new[]
                     {
                         MonsterActiveAttackPattern.PiercingProjectile,
                         MonsterActiveAttackPattern.ExplosiveProjectile
                     })
            {
                yield return Create(pattern + "_single", pattern,
                    MonsterActiveAttackProgression.Instant,
                    MonsterActiveProjectileFormation.Single, 1);
                yield return Create(pattern + "_fan", pattern,
                    MonsterActiveAttackProgression.Instant,
                    MonsterActiveProjectileFormation.Fan, 5);
            }
            yield return Create("beam", MonsterActiveAttackPattern.PiercingBeam,
                MonsterActiveAttackProgression.Instant);
            foreach (var target in Enum.GetValues(typeof(MonsterActiveInstantMagicTarget))
                         .Cast<MonsterActiveInstantMagicTarget>())
            foreach (var direction in Enum.GetValues(typeof(MonsterActiveMagicDirection))
                         .Cast<MonsterActiveMagicDirection>())
            {
                var step = Create("magic_" + target + "_" + direction,
                    MonsterActiveAttackPattern.InstantMagic,
                    MonsterActiveAttackProgression.Instant);
                step.EditorConfigureInstantMagic(target, direction);
                yield return step;
            }
        }

        private static MonsterActiveAttackStep Create(
            string id,
            MonsterActiveAttackPattern pattern,
            MonsterActiveAttackProgression progression,
            MonsterActiveProjectileFormation formation =
                MonsterActiveProjectileFormation.Single,
            int projectileCount = 1)
        {
            id = id.ToLowerInvariant();
            var step = new MonsterActiveAttackStep();
            step.EditorConfigure(id, id, pattern, 1f, 0f,
                MonsterActiveTargetPolicy.SameTarget, progression);
            step.EditorConfigureGeometry(5f, 2f, 5f, 3f, 120f, 8,
                0.3f, 0f, 0.8f);
            if (pattern is MonsterActiveAttackPattern.PiercingProjectile or
                MonsterActiveAttackPattern.ExplosiveProjectile)
            {
                step.EditorConfigureProjectile(formation, projectileCount,
                    60f, 10f, 0.8f, 2f);
            }
            if (pattern == MonsterActiveAttackPattern.InstantMagic)
            {
                step.EditorConfigureInstantMagic(
                    MonsterActiveInstantMagicTarget.TargetArea,
                    MonsterActiveMagicDirection.GroundUp);
            }
            return step;
        }

        private static MonsterActivePresentationSlot Slot(
            string id,
            MonsterActivePresentationEvent timing,
            MonsterActivePresentationAnchor anchor,
            MonsterActivePresentationMultiplicity multiplicity,
            MonsterActivePresentationAttachment attachment =
                MonsterActivePresentationAttachment.World,
            MonsterActivePresentationEndPolicy end =
                MonsterActivePresentationEndPolicy.Timed)
        {
            var slot = new MonsterActivePresentationSlot();
            slot.EditorConfigure(id, id, timing, anchor,
                playbackMultiplicity: multiplicity,
                playbackAttachment: attachment,
                playbackEndPolicy: end);
            return slot;
        }

        private static void AssertInvalid(
            MonsterActiveAttackStep step,
            MonsterActivePresentationSlot slot)
        {
            Assert.That(MonsterActiveAttackVfxCompatibility.TryValidateSlot(
                step, slot, out _), Is.False, slot.SlotId);
        }

        private static void AssertCanonical(
            MonsterActiveAttackStep step,
            string route)
        {
            var expected = Build(step);
            Assert.That(step.PresentationSlots.Count, Is.EqualTo(expected.Length), route);
            for (var index = 0; index < expected.Length; index++)
            {
                var actual = step.PresentationSlots[index];
                Assert.That(actual.SlotId, Is.EqualTo(expected[index].SlotId), route);
                Assert.That(actual.Timing, Is.EqualTo(expected[index].Timing), route);
                Assert.That(actual.Anchor, Is.EqualTo(expected[index].Anchor), route);
                Assert.That(actual.Multiplicity, Is.EqualTo(expected[index].Multiplicity), route);
                Assert.That(actual.Attachment, Is.EqualTo(expected[index].Attachment), route);
                Assert.That(actual.EndPolicy, Is.EqualTo(expected[index].EndPolicy), route);
            }
        }

        private static MonsterActivePresentationSlot[] Build(
            MonsterActiveAttackStep step)
        {
            var templates = FindType(
                "ProjectMT.EditorTools.MonsterMaker.MonsterActiveAttackVfxContractTemplates");
            return (MonsterActivePresentationSlot[])templates
                .GetMethod("Build", StaticFlags)
                .Invoke(null, new object[] { step });
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .First(type => type != null);
        }

        private static string Round(Vector3 value) =>
            $"{Mathf.RoundToInt(value.x * 1000f)}:" +
            $"{Mathf.RoundToInt(value.y * 1000f)}:" +
            $"{Mathf.RoundToInt(value.z * 1000f)}";
    }
}
