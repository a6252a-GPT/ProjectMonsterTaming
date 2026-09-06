using System.Linq;
using NUnit.Framework;
using ProjectMT.Contents.CastleRaidHex.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectMT.Contents.CastleRaidHex.Editor.Tests
{
    public sealed class HexCastleEditorToolTests
    {
        [Test]
        public void Legend_HasUniqueColorsForEveryArchitectureCategory()
        {
            const int architectureCategoryCount = 17; // 기반 7종 + 건물 역할 9종 + 왕궁
            Assert.That(HexCastleVisualPalette.Legend.Count, Is.EqualTo(architectureCategoryCount));
            Assert.That(
                HexCastleVisualPalette.Legend.Select(entry => entry.Label).Distinct().Count(),
                Is.EqualTo(architectureCategoryCount));
            Assert.That(
                HexCastleVisualPalette.Legend.Select(entry => entry.Color).Distinct().Count(),
                Is.EqualTo(architectureCategoryCount));
        }

        [Test]
        public void LegacyPrototypeGeneratorAndAssets_AreAbsent()
        {
            Assert.That(System.Type.GetType(
                "ProjectMT.Contents.CastleRaidHex.HexCastleGenerator, ProjectMT.Contents.CastleRaidHex"),
                Is.Null);
            Assert.That(System.Type.GetType(
                "ProjectMT.Contents.CastleRaidHex.HexCastleSilhouettePrototypeGenerator, " +
                "ProjectMT.Contents.CastleRaidHex"), Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(
                "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Data/Generation/HexCastleGenerationRules.asset"),
                Is.Null);
            Assert.That(AssetDatabase.IsValidFolder(
                "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Data/Generation/Themes"),
                Is.False);
        }

        [Test]
        public void RuntimeVisualSet_ResolvesEveryProcedurallyGeneratedBuildingAndTurretHead()
        {
            var rules = HexCastleThemeOneRulesAssetUtility.Load();
            Assert.That(rules, Is.Not.Null);
            var visualSet = HexCastleRuntimeVisualSetAssetUtility.LoadOrCreate();
            Assert.That(visualSet.IsRuntimeComplete, Is.True);
            for (var difficulty = 1; difficulty <= 10; difficulty++)
            {
                var layout = new HexCastleFoundationGenerator().GenerateForDifficulty(
                    10801 + difficulty,
                    difficulty,
                    HexCastleTheme.CentralCompartment,
                    rules.Tuning);
                foreach (var building in layout.Cells.Values.Where(value => value.IsBuildingCell))
                {
                    Assert.That(visualSet.ResolveBuilding(building.VisualVariantId), Is.Not.Null,
                        $"난이도 {difficulty}/{building.VisualVariantId}");
                    if (building.BuildingRole == HexCastleBuildingRole.Turret)
                    {
                        Assert.That(visualSet.ResolveTurretHead(
                            building.TurretWeaponKind,
                            building.BuildingGrade), Is.Not.Null,
                            $"난이도 {difficulty}/{building.TurretWeaponKind} Lv{building.BuildingGrade}");
                    }
                }
            }

            Assert.That(AssetDatabase.IsValidFolder("Assets/Traps"), Is.False);
            Assert.That(AssetDatabase.IsValidFolder("Assets/ThirdParty2/05_오브젝트장식/Traps"), Is.True);
            Assert.That(visualSet.TrapVisuals.Count, Is.EqualTo(4));
            Assert.That(visualSet.TrapVisuals.Count(value =>
                value.TrapType == HexCastleTrapType.Snare), Is.EqualTo(1));
            Assert.That(visualSet.TrapVisuals.Count(value =>
                value.TrapType == HexCastleTrapType.SpikePlate), Is.EqualTo(3));
            Assert.That(visualSet.TrapVisuals.Select(value => value.VisualVariantId).Distinct().Count(),
                Is.EqualTo(4));
            Assert.That(visualSet.TrapVisuals.All(value =>
                AssetDatabase.GetAssetPath(value.Prefab).StartsWith(
                    "Assets/ThirdParty2/05_오브젝트장식/Traps/Prefabs/")), Is.True);
            Assert.That(visualSet.TrapVisuals.All(value =>
                value.Prefab.GetComponentInChildren<Animator>(true) != null), Is.True);
            Assert.That(visualSet.TrapVisuals.All(value =>
                value.MaterialOverride.shader != null &&
                value.MaterialOverride.shader.name == "Universal Render Pipeline/Lit"), Is.True);
            Assert.That(visualSet.ResolveTrapVisual(HexCastleTrapType.Snare, "A"), Is.Not.Null);
            Assert.That(visualSet.ResolveTrapVisual(HexCastleTrapType.SpikePlate, "A"), Is.Not.Null);
            Assert.That(visualSet.ResolveTrapVisual(HexCastleTrapType.BlastMine, "A"), Is.Null);
            Assert.That(new[] { "TRAP_D04_07", "TRAP_D04_08", "TRAP_D04_09" }
                .Select(value => visualSet.ResolveTrapVisual(
                    HexCastleTrapType.SpikePlate, value).VisualVariantId)
                .Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void PreviewExporter_RendersThemeOneForTwoThreeAndFourWalls()
        {
            var pipeline = new HexCastleGenerationPipeline();
            foreach (var defenseLayerCount in new[] { 2, 3, 4 })
            {
                var candidate = pipeline.GenerateFoundation(
                    10801,
                    defenseLayerCount,
                    HexCastleTheme.CentralCompartment);
                var texture = HexCastlePreviewExporter.BuildTexture(candidate, 320);
                try
                {
                    Assert.That(texture.width, Is.EqualTo(320));
                    Assert.That(texture.height, Is.EqualTo(320));
                    Assert.That(
                        texture.GetPixels32().Distinct().Count(),
                        Is.GreaterThan(8),
                        $"{defenseLayerCount}중벽");
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }

        [Test]
        public void EditorAssembly_DoesNotReferenceLegacyOrNavigationAssemblies()
        {
            var references = typeof(HexCastleAuthoringWindow).Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain("ProjectMT.Contents.CastleRaid"));
            Assert.That(references, Does.Not.Contain("ProjectMT.Tools.CastleBake.Editor"));
            Assert.That(references, Does.Not.Contain("Unity.AI.Navigation"));
            Assert.That(references, Does.Not.Contain("UnityEngine.AIModule"));
        }

        [Test]
        public void ThemeOneMatrix_ContainsTwoThreeAndFourWallSlots()
        {
            var keys = new[] { 2, 3, 4 }
                .Select(layers => $"HEX_T1_{layers}W_10801")
                .ToArray();

            Assert.That(keys.Length, Is.EqualTo(3));
            Assert.That(keys.Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void ObsoleteBakedStageAssetsAndCatalog_AreAbsent()
        {
            Assert.That(AssetDatabase.IsValidFolder(
                "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Data/Baked"), Is.False);
            Assert.That(AssetDatabase.IsValidFolder(
                "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Prefabs/Baked"), Is.False);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(HexCastleAssetWriter.CatalogPath), Is.Null);
        }

        [Test]
        public void ProceduralDifficultyMatrix_GeneratesAllLevelsWithoutSavedStageAssets()
        {
            var pipeline = new HexCastleGenerationPipeline();
            for (var difficulty = 1; difficulty <= 10; difficulty++)
            {
                var candidate = pipeline.GenerateFoundationForDifficulty(
                    10801 + difficulty,
                    difficulty,
                    HexCastleTheme.CentralCompartment);
                Assert.That(candidate.Validation.IsValid, Is.True,
                    $"난이도 {difficulty}: {string.Join(" | ", candidate.Validation.Errors)}");
                Assert.That(candidate.Layout.DifficultyLevel, Is.EqualTo(difficulty));
                Assert.That(candidate.Layout.DefenseLayerCount,
                    Is.EqualTo(HexCastleDifficultyProfile.ResolveDefenseLayerCount(
                        difficulty,
                        10801 + difficulty)));
            }
        }

        [Test]
        public void PlayablePreview_UsesFoundationCellsAndRestoresCamera()
        {
            var scene = SceneManager.GetActiveScene();
            var cameraObject = new GameObject("HexPlayablePreviewCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.transform.position = new Vector3(3f, 7f, -11f);
            cameraObject.transform.SetSiblingIndex(0); // 도구가 선택할 첫 Scene Camera를 테스트가 명시한다
            var originalPosition = camera.transform.position;
            try
            {
                var rules = HexCastleThemeOneRulesAssetUtility.LoadOrCreate();
                var candidate = new HexCastleGenerationPipeline().GenerateFoundation(
                    10801,
                    3,
                    HexCastleTheme.CentralCompartment,
                    rules.Tuning);
                var root = HexCastleGenerationPlayablePreview.Create(
                    candidate,
                    Vector3.zero,
                    rules,
                    false);
                var stage = root.GetComponent<HexCastleFoundationPlayableStage>();
                var cells = root.GetComponentsInChildren<HexCastleCellRuntime>(true);
                var blocked = cells.Where(value => value.InitialBlocked).ToArray();

                Assert.That(root.name, Is.EqualTo(HexCastleGenerationPlayablePreview.RootName));
                Assert.That(stage, Is.Not.Null);
                Assert.That(stage.Seed, Is.EqualTo(candidate.Layout.Seed));
                Assert.That(stage.DefenseLayerCount, Is.EqualTo(3));
                Assert.That(cells.Length, Is.EqualTo(candidate.Layout.Cells.Count));
                Assert.That(blocked.Length, Is.GreaterThan(10));
                Assert.That(blocked.All(value =>
                    value.Health != null && value.Health.transform == value.transform &&
                    value.FootprintCollider != null && value.FootprintCollider.transform == value.transform),
                    Is.True);
                Assert.That(root.GetComponentsInChildren<Transform>(true)
                    .All(value => value.gameObject.hideFlags == HideFlags.None), Is.True);
                Assert.That(camera.orthographic, Is.False);
                var boardRenderer = root.transform.Find("00_BoardSurface")?.GetComponent<MeshRenderer>();
                Assert.That(boardRenderer, Is.Not.Null);
                Assert.That(boardRenderer.enabled, Is.False, "DEV 배경을 가리는 절차 바닥은 숨겨야 합니다.");

                HexCastleGenerationPlayablePreview.Clear(scene);
                Assert.That(camera.orthographic, Is.True);
                Assert.That(camera.orthographicSize, Is.EqualTo(8f));
                Assert.That(camera.transform.position, Is.EqualTo(originalPosition));
            }
            catch (System.Exception exception)
            {
                Assert.Fail(exception.ToString());
            }
            finally
            {
                HexCastleGenerationPlayablePreview.Clear(scene);
                HexCastleFoundationVisualGate.Remove(scene);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void ScenePreview_RemovalClearsOnlyObjectsOwnedByPreviewSelection()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var previewRoot = new GameObject(HexCastleFoundationVisualGate.RootName);
            var previewChild = new GameObject("SelectedPreviewChild");
            var retainedObject = new GameObject("RetainedSelection");
            try
            {
                SceneManager.MoveGameObjectToScene(previewRoot, scene);
                SceneManager.MoveGameObjectToScene(retainedObject, scene);
                previewChild.transform.SetParent(previewRoot.transform, false);
                Selection.objects = new Object[] { previewChild, retainedObject };

                HexCastleFoundationVisualGate.Remove(scene, false);

                Assert.That(previewRoot == null, Is.True);
                Assert.That(Selection.objects, Is.EqualTo(new Object[] { retainedObject }));
            }
            finally
            {
                Selection.objects = System.Array.Empty<Object>();
                if (previewRoot != null)
                {
                    Object.DestroyImmediate(previewRoot);
                }

                if (retainedObject != null)
                {
                    Object.DestroyImmediate(retainedObject);
                }

                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void BallistaLv2_KeepsOriginalSocketsLoadedArrowsAndAttackAssets()
        {
            const string headPath =
                "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Prefabs/TurretHeads/PF_CR_TurretHead_Ballista_Lv2.prefab";
            const string hexProfilePath =
                "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Data/Turrets/CRHex_TurretAttack_Ballista_Lv2.asset";
            var head = AssetDatabase.LoadAssetAtPath<GameObject>(headPath);
            var pitch = head?.transform.Find("Joint_BodyMount/YawPivot/PitchPivot");
            var muzzle = pitch?.Find("Muzzle");
            var loaded = pitch?.Find("LoadedProjectiles");

            Assert.That(head, Is.Not.Null);
            Assert.That(muzzle, Is.Not.Null);
            Assert.That(loaded, Is.Not.Null);
            Assert.That(muzzle.localPosition.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(muzzle.localPosition.y, Is.EqualTo(0.72f).Within(0.0001f));
            Assert.That(muzzle.localPosition.z, Is.EqualTo(0.95f).Within(0.0001f));
            Assert.That(muzzle.Find("VFX_Muzzle"), Is.Not.Null);
            Assert.That(loaded.childCount, Is.EqualTo(3));
            Assert.That(
                Enumerable.Range(0, loaded.childCount)
                    .Select(index => loaded.GetChild(index).localPosition.x),
                Is.EqualTo(new[] { -0.22f, 0f, 0.22f }).Within(0.0001f));
            Assert.That(
                Enumerable.Range(0, loaded.childCount)
                    .Select(index => loaded.GetChild(index).localPosition.y),
                Is.All.EqualTo(0.72f).Within(0.0001f));
            Assert.That(
                Enumerable.Range(0, loaded.childCount)
                    .Select(index => loaded.GetChild(index).localPosition.z),
                Is.All.EqualTo(0.48f).Within(0.0001f));

            var hexProfile = AssetDatabase.LoadAssetAtPath<HexCastleTurretAttackProfile>(hexProfilePath);
            Assert.That(hexProfile, Is.Not.Null);
            Assert.That(hexProfile.HasCompletePresentation, Is.True);
            var projectile = hexProfile.Data.projectilePrefab;
            Assert.That(projectile, Is.Not.Null);
            Assert.That(projectile.GetComponentsInChildren<TrailRenderer>(true).Length, Is.GreaterThan(0));
            Assert.That(
                projectile.GetComponentsInChildren<Transform>(true).Any(value => value.name == "VFX_Tail"),
                Is.True);
        }

        [Test]
        public void SupportedTurretLevels_KeepHexPresentationAssets()
        {
            const string hexRoot =
                "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Data/Turrets/";
            const string headRoot =
                "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Prefabs/TurretHeads/";
            var catalog = HexCastleTurretAttackAssetUtility.LoadOrCreateCatalog();

            Assert.That(catalog.HasCompletePresentation, Is.True);
            foreach (var weaponKind in new[]
                     {
                         HexCastleTurretWeaponKind.Cannon,
                         HexCastleTurretWeaponKind.Ballista,
                         HexCastleTurretWeaponKind.Fireball
                     })
            {
                var family = weaponKind.ToString();
                for (var level = 1;
                     level <= HexCastleTurretAttackCatalog.ResolveSupportedMaximumLevel(weaponKind);
                     level++)
                {
                    var profile = catalog.Resolve(weaponKind, level);
                    Assert.That(
                        AssetDatabase.GetAssetPath(profile),
                        Is.EqualTo($"{hexRoot}CRHex_TurretAttack_{family}_Lv{level}.asset"));
                    var head = AssetDatabase.LoadAssetAtPath<GameObject>(
                        $"{headRoot}PF_CR_TurretHead_{family}_Lv{level}.prefab");
                    var pitch = head.transform.Find("Joint_BodyMount/YawPivot/PitchPivot");
                    var muzzleVfx = pitch.Find("Muzzle/VFX_Muzzle");
                    var loaded = pitch.Find("LoadedProjectiles");
                    var data = profile.Data;

                    Assert.That(profile.HasCompletePresentation, Is.True, $"{family} Lv{level}");
                    Assert.That(data.projectilePrefab, Is.Not.Null, $"{family} Lv{level} Projectile");
                    Assert.That(data.fireSfx?.HasPlayableClip, Is.True, $"{family} Lv{level} Fire SFX");
                    Assert.That(muzzleVfx, Is.Not.Null, $"{family} Lv{level} Muzzle socket");
                    if (weaponKind == HexCastleTurretWeaponKind.Cannon)
                    {
                        Assert.That(muzzleVfx.GetComponentsInChildren<ParticleSystem>(true), Is.Not.Empty);
                        Assert.That(data.impactVfxPrefab, Is.Not.Null);
                        Assert.That(
                            data.impactVfxPrefab.GetComponentsInChildren<ParticleSystem>(true),
                            Is.Not.Empty);
                    }
                    else if (weaponKind == HexCastleTurretWeaponKind.Ballista)
                    {
                        Assert.That(loaded.childCount, Is.GreaterThan(0));
                        Assert.That(data.projectilePrefab.GetComponentsInChildren<TrailRenderer>(true), Is.Not.Empty);
                        Assert.That(data.hitSfx?.HasPlayableClip, Is.True);
                    }
                    else
                    {
                        Assert.That(muzzleVfx.GetComponentsInChildren<ParticleSystem>(true), Is.Not.Empty);
                        Assert.That(data.projectilePrefab.GetComponentsInChildren<ParticleSystem>(true), Is.Not.Empty);
                        Assert.That(data.impactVfxPrefab, Is.Not.Null);
                        Assert.That(data.explosionSfx?.HasPlayableClip, Is.True);
                    }
                }
            }

            Assert.That(catalog.Resolve(HexCastleTurretWeaponKind.Cannon, 3), Is.Null);
            Assert.That(catalog.Resolve(HexCastleTurretWeaponKind.Ballista, 3), Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(
                $"{hexRoot}CRHex_TurretAttack_Ballista_Lv3.asset"), Is.Null);
        }

        private static string[] FindAssetsInExistingFolder(string filter, string folder)
        {
            return AssetDatabase.IsValidFolder(folder)
                ? AssetDatabase.FindAssets(filter, new[] { folder })
                : System.Array.Empty<string>();
        }

        private static string ResolveStagePrefix(HexCastleTheme theme)
        {
            return theme == HexCastleTheme.CentralCompartment
                ? "HEX_T1_"
                : $"HEX_T{HexCastleThemeCatalog.ResolveCode(theme)}_";
        }
    }
}
