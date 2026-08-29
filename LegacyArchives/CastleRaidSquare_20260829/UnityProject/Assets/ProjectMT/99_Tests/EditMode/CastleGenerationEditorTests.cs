using System;
using System.Linq;
using NUnit.Framework;
using ProjectMT.Contents.CastleRaid;
using ProjectMT.Contents.CastleRaid.Generation;
using ProjectMT.EditorTools.CastleBake;
using ProjectMT.Shared.Combat;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ProjectMT.Tests.EditMode
{
    public sealed class CastleGenerationEditorTests
    {
        private const string TestOutputRoot = "Assets/ProjectMT/98_Generated/CastleRaid/__EditorTestStageDrafts";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestOutputRoot))
            {
                AssetDatabase.DeleteAsset(TestOutputRoot); // 테스트 전용 출력만 제거
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void DefaultAssetFactory_IsIdempotentAndKeepsTemplateGuids()
        {
            var first = CastleGenerationAssetFactory.CreateOrUpdateDefaults();
            var firstTemplateGuids = first.Templates
                .Select(template => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(template)))
                .ToArray();

            var second = CastleGenerationAssetFactory.CreateOrUpdateDefaults();
            var secondTemplateGuids = second.Templates
                .Select(template => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(template)))
                .ToArray();

            Assert.That(AssetDatabase.GetAssetPath(second), Is.EqualTo(CastleGenerationAssetFactory.DefaultRulesPath));
            Assert.That(second.TryValidate(out var error), Is.True, error);
            Assert.That(secondTemplateGuids, Is.EqualTo(firstTemplateGuids));
        }

        [Test]
        public void PreviewLegend_CoversEveryRenderedColorWithUniqueLabel()
        {
            var entries = CastleGenerationPreviewExporter.LegendEntries;

            Assert.That(entries.Count, Is.EqualTo(18));
            Assert.That(entries.Select(entry => entry.Category).Distinct(), Is.EquivalentTo(new[]
            {
                "공략·건물",
                "보상 건물",
                "성벽",
                "구역 바닥",
                "오류 표시"
            }));
            Assert.That(entries.Select(entry => entry.Label).Distinct().Count(), Is.EqualTo(entries.Count));
            Assert.That(entries.Select(entry => entry.Color).Distinct().Count(), Is.EqualTo(entries.Count));
            Assert.That(entries.All(entry => entry.Color.a > 0f), Is.True);
        }

        [Test]
        public void LayoutWriter_SavesValidatedLayoutAndRejectsDuplicateStageId()
        {
            var rules = CastleGenerationAssetFactory.CreateOrUpdateDefaults();
            var candidate = new CastleGenerator().Generate(rules, 8801);

            var layout = CastleStageLayoutAssetWriter.Create(TestOutputRoot, "castle_stage_test_001", candidate);

            Assert.That(layout.StageId, Is.EqualTo("castle_stage_test_001"));
            Assert.That(layout.LayoutHash, Is.EqualTo(candidate.LayoutHash));
            Assert.That(layout.StructureHash, Is.EqualTo(candidate.StructureHash));
            Assert.That(layout.StructureVariant, Is.EqualTo(candidate.StructureVariant));
            Assert.That(AssetDatabase.GetAssetPath(layout), Does.StartWith(TestOutputRoot + "/"));
            Assert.Throws<InvalidOperationException>(() =>
                CastleStageLayoutAssetWriter.Create(TestOutputRoot, "castle_stage_test_001", candidate));
            Assert.That(
                AssetDatabase.FindAssets("t:CastleStageLayout", new[] { TestOutputRoot }).Length,
                Is.EqualTo(1));
        }

        [Test]
        public void LayoutWriter_RejectsDuplicateBatchBeforeCreatingAnyAsset()
        {
            var rules = CastleGenerationAssetFactory.CreateOrUpdateDefaults();
            var generator = new CastleGenerator();
            var first = generator.Generate(rules, 9901);
            var second = generator.Generate(rules, 9902);

            Assert.Throws<InvalidOperationException>(() =>
                CastleStageLayoutAssetWriter.CreateBatch(
                    TestOutputRoot,
                    new[] { "castle_stage_same", "castle_stage_same" },
                    new[] { first, second }));
            Assert.That(
                AssetDatabase.FindAssets("t:CastleStageLayout", new[] { TestOutputRoot }).Length,
                Is.EqualTo(0));
        }

        [Test]
        public void ScenePreviewBounds_CropEveryThemeToSquareWithoutLosingPlacements()
        {
            var rules = CastleGenerationAssetFactory.CreateOrUpdateDefaults();
            var generator = new CastleGenerator();
            var croppedCandidateCount = 0;
            foreach (var theme in CastleGenerationRules.SupportedLayoutThemes)
            {
                for (var layer = 2; layer <= 4; layer++)
                {
                    var candidate = generator.Generate(rules, 12000 + (int)theme * 10 + layer, theme, layer);
                    var bounds = CastleGenerationScenePreview.ResolveSquareDisplayBounds(candidate);

                    Assert.That(bounds.width, Is.EqualTo(bounds.height), $"{theme} {layer}중벽");
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(0));
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(0));
                    Assert.That(bounds.xMax, Is.LessThanOrEqualTo(candidate.GridWidth));
                    Assert.That(bounds.yMax, Is.LessThanOrEqualTo(candidate.GridHeight));
                    var castleInterior = new RectInt(
                        bounds.xMin + CastleGenerationScenePreview.PreviewGroundMarginCells,
                        bounds.yMin + CastleGenerationScenePreview.PreviewGroundMarginCells,
                        bounds.width - CastleGenerationScenePreview.PreviewGroundMarginCells * 2,
                        bounds.height - CastleGenerationScenePreview.PreviewGroundMarginCells * 2);
                    foreach (var placement in candidate.Placements)
                    {
                        Assert.That(
                            CastleSpatialContract.Contains(castleInterior, placement.Bounds),
                            Is.True,
                            $"{theme} {layer}중벽 {placement.PlacementId}가 외곽 소환 벨트를 침범했습니다.");
                    }

                    Assert.That(
                        CastleGenerationScenePreview.IsPreviewDeploymentCell(
                            new Vector2Int(bounds.xMin, bounds.yMin),
                            bounds),
                        Is.True);
                    Assert.That(
                        CastleGenerationScenePreview.IsPreviewDeploymentCell(
                            new Vector2Int(
                                bounds.xMin + CastleGenerationScenePreview.PreviewGroundMarginCells,
                                bounds.yMin + CastleGenerationScenePreview.PreviewGroundMarginCells),
                            bounds),
                        Is.False);

                    if (bounds.width < candidate.GridWidth)
                    {
                        croppedCandidateCount++;
                    }
                }
            }

            Assert.That(croppedCandidateCount, Is.GreaterThan(0));
        }

        [Test]
        public void ScenePreview_RebuildsOnlyDedicatedTemporaryRoot()
        {
            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var sentinel = new GameObject("UserSceneObject");
                SceneManager.MoveGameObjectToScene(sentinel, previewScene);
                var sceneRoot = new GameObject("00_SceneRoot");
                SceneManager.MoveGameObjectToScene(sceneRoot, previewScene);
                var worldRoot = new GameObject("01_WorldRoot");
                worldRoot.transform.SetParent(sceneRoot.transform, false);
                var existingStage = new GameObject("CastleStage_Seed");
                existingStage.transform.SetParent(worldRoot.transform, false);
                var cameraRoot = new GameObject("03_CameraRoot");
                cameraRoot.transform.SetParent(sceneRoot.transform, false);
                var cameraObject = new GameObject("CastleRaidCamera");
                cameraObject.transform.SetParent(cameraRoot.transform, false);
                cameraObject.transform.SetPositionAndRotation(
                    new Vector3(15f, 18f, -15f),
                    Quaternion.Euler(40.32f, 315f, 0f));
                var castleCamera = cameraObject.AddComponent<Camera>();
                castleCamera.orthographic = true;
                castleCamera.orthographicSize = 11.5f;
                var originalCameraPosition = castleCamera.transform.position;
                var rules = CastleGenerationAssetFactory.CreateOrUpdateDefaults();
                var candidate = new CastleGenerator().Generate(
                    rules,
                    10801,
                    CastleLayoutTheme.TwinSpiralFortress,
                    3);
                var displayBounds = CastleGenerationScenePreview.ResolveSquareDisplayBounds(candidate);

                Assert.That(displayBounds.width, Is.EqualTo(displayBounds.height));
                Assert.That(displayBounds.width, Is.LessThan(candidate.GridWidth));
                var castleInterior = new RectInt(
                    displayBounds.xMin + CastleGenerationScenePreview.PreviewGroundMarginCells,
                    displayBounds.yMin + CastleGenerationScenePreview.PreviewGroundMarginCells,
                    displayBounds.width - CastleGenerationScenePreview.PreviewGroundMarginCells * 2,
                    displayBounds.height - CastleGenerationScenePreview.PreviewGroundMarginCells * 2);
                foreach (var placement in candidate.Placements)
                {
                    Assert.That(
                        CastleSpatialContract.Contains(castleInterior, placement.Bounds),
                        Is.True,
                        $"{placement.PlacementId}가 외곽 소환 벨트를 침범했습니다.");
                }

                var first = CastleGenerationScenePreview.Rebuild(
                    candidate,
                    previewScene,
                    Vector3.zero);
                var firstInstanceId = first.GetInstanceID();

                Assert.That(first.name, Is.EqualTo(CastleGenerationScenePreview.PreviewRootName));
                Assert.That((first.hideFlags & HideFlags.DontSaveInEditor) != 0, Is.True);
                Assert.That(first.transform.Find("00_Base"), Is.Not.Null);
                Assert.That(first.transform.Find("01_Floor"), Is.Not.Null);
                Assert.That(first.transform.Find("02_Walls"), Is.Not.Null);
                Assert.That(first.transform.Find("03_Structures"), Is.Not.Null);
                Assert.That(first.GetComponentsInChildren<MeshRenderer>(true).Length, Is.GreaterThan(4));
                Assert.That(first.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(existingStage.activeSelf, Is.False);
                Assert.That(
                    castleCamera.orthographicSize,
                    Is.EqualTo(CastleGenerationScenePreview.ResolvePreviewCameraSize(candidate)).Within(0.001f));
                var baseRenderer = first.transform.Find("00_Base").GetComponentInChildren<MeshRenderer>();
                Assert.That(baseRenderer.bounds.size.x, Is.EqualTo(displayBounds.width).Within(0.001f));
                Assert.That(baseRenderer.bounds.size.z, Is.EqualTo(displayBounds.height).Within(0.001f));

                var second = CastleGenerationScenePreview.Rebuild(
                    candidate,
                    previewScene,
                    new Vector3(3f, 0f, 5f));

                Assert.That(second.GetInstanceID(), Is.Not.EqualTo(firstInstanceId));
                Assert.That(second.transform.position, Is.EqualTo(new Vector3(3f, 0f, 5f)));
                Assert.That(sentinel, Is.Not.Null);
                Assert.That(sentinel.scene, Is.EqualTo(previewScene));
                Assert.That(existingStage.activeSelf, Is.False);
                Assert.That(
                    castleCamera.orthographicSize,
                    Is.EqualTo(CastleGenerationScenePreview.ResolvePreviewCameraSize(candidate)).Within(0.001f));
                Assert.That(
                    previewScene.GetRootGameObjects().Count(root => root.name == CastleGenerationScenePreview.PreviewRootName),
                    Is.EqualTo(1));

                Assert.That(CastleGenerationScenePreview.Clear(previewScene), Is.EqualTo(1));
                Assert.That(sentinel, Is.Not.Null);
                Assert.That(existingStage.activeSelf, Is.True);
                Assert.That(castleCamera.orthographicSize, Is.EqualTo(11.5f).Within(0.001f));
                Assert.That(castleCamera.transform.position, Is.EqualTo(originalCameraPosition));
                Assert.That(
                    previewScene.GetRootGameObjects().Any(root => root.name == CastleGenerationScenePreview.PreviewRootName),
                    Is.False);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        [Test]
        public void PreviewRoots_AreRemovedWhenActiveSceneChanges()
        {
            var sourceScene = SceneManager.GetActiveScene();
            Scene destinationScene = default;
            try
            {
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
                    typeof(CastleGenerationScenePreview).TypeHandle);
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
                    typeof(CastleGenerationPlayablePreview).TypeHandle);

                var scenePreview = new GameObject(CastleGenerationScenePreview.PreviewRootName);
                SceneManager.MoveGameObjectToScene(scenePreview, sourceScene);
                var playableHost = new GameObject("PlayableHost");
                SceneManager.MoveGameObjectToScene(playableHost, sourceScene);
                var playablePreview = new GameObject(CastleGenerationPlayablePreview.PlayableRootName);
                SceneManager.MoveGameObjectToScene(playablePreview, sourceScene);
                playablePreview.transform.SetParent(playableHost.transform, false);

                destinationScene = EditorSceneManager.OpenScene(
                    "Assets/ProjectMT/00_Scenes/00_Entry.unity",
                    OpenSceneMode.Additive);
                if (SceneManager.GetActiveScene() != destinationScene)
                {
                    Assert.That(
                        SceneManager.SetActiveScene(destinationScene),
                        Is.True,
                        "검증용 목적 Scene을 활성화하지 못했습니다.");
                }

                Assert.That(scenePreview == null, Is.True, "이전 Scene의 3D 프리뷰가 남았습니다.");
                Assert.That(playablePreview == null, Is.True, "이전 Scene의 플레이 프리뷰가 남았습니다.");
                Assert.That(playableHost, Is.Not.Null, "프리뷰 외 사용자 오브젝트가 제거됐습니다.");
            }
            finally
            {
                if (sourceScene.IsValid() && sourceScene.isLoaded)
                {
                    SceneManager.SetActiveScene(sourceScene);
                }

                if (destinationScene.IsValid() && destinationScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(destinationScene, true);
                }
            }
        }

        [Test]
        public void TurretVisualSelector_IsDeterministicAndUsesDefenseDepthForLevel()
        {
            var first = CastleTurretVisualSelector.ResolveFamily(10801, "defense_0042");
            var second = CastleTurretVisualSelector.ResolveFamily(10801, "defense_0042");

            Assert.That(second, Is.EqualTo(first));
            Assert.That((int)first, Is.InRange(0, 2));
            Assert.That(CastleTurretVisualSelector.ResolveLevel(2, 0), Is.EqualTo(2));
            Assert.That(CastleTurretVisualSelector.ResolveLevel(2, 1), Is.EqualTo(1));
            Assert.That(CastleTurretVisualSelector.ResolveLevel(3, 0), Is.EqualTo(3));
            Assert.That(CastleTurretVisualSelector.ResolveLevel(3, 1), Is.EqualTo(2));
            Assert.That(CastleTurretVisualSelector.ResolveLevel(3, 2), Is.EqualTo(1));
            Assert.That(CastleTurretVisualSelector.ResolveLevel(4, 0), Is.EqualTo(3));
            Assert.That(CastleTurretVisualSelector.ResolveLevel(4, 3), Is.EqualTo(1));
        }

        [Test]
        public void TurretAttackCatalog_PreservesOriginalThreeFamiliesAndLevelProfiles()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CastleTurretAttackCatalog>(
                "Assets/ProjectMT/04_Contents/01_CastleRaid/Data/Turrets/CR_TurretAttackCatalog.asset");

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.IsComplete, Is.True);
            for (var family = 0; family < 3; family++)
            {
                for (var level = 1; level <= 3; level++)
                {
                    var profile = catalog.Resolve((CastleTurretFamily)family, level);
                    Assert.That(profile, Is.Not.Null, $"{(CastleTurretFamily)family} Lv{level}");
                    Assert.That(profile.Data.projectilePrefab, Is.Not.Null);
                    Assert.That(profile.Data.fireSfx, Is.Not.Null);
                    Assert.That(profile.Data.fireSfx.HasPlayableClip, Is.True);
                }
            }

            var cannonLv3 = catalog.Resolve(CastleTurretFamily.Cannon, 3).Data;
            Assert.That(cannonLv3.baseDamage, Is.EqualTo(8f).Within(0.001f));
            Assert.That(cannonLv3.cooldown, Is.EqualTo(5.4f).Within(0.001f));
            Assert.That(cannonLv3.projectileCount, Is.EqualTo(6));
            Assert.That(cannonLv3.fireSequentially, Is.True);
            Assert.That(cannonLv3.projectileFireDelay, Is.EqualTo(0.2f).Within(0.001f));

            var ballistaLv2 = catalog.Resolve(CastleTurretFamily.Ballista, 2).Data;
            Assert.That(ballistaLv2.targetPriority, Is.EqualTo(CastleTurretTargetPriority.BossEliteThenFarthest));
            Assert.That(ballistaLv2.projectileCount, Is.EqualTo(3));
            Assert.That(ballistaLv2.fireSequentially, Is.False);
            Assert.That(ballistaLv2.spreadAngle, Is.EqualTo(6f).Within(0.001f));
            Assert.That(ballistaLv2.baseDamage, Is.EqualTo(7f).Within(0.001f));
            Assert.That(ballistaLv2.pierceCount, Is.EqualTo(3));
            Assert.That(ballistaLv2.piercingDamageRatio, Is.EqualTo(0.45f).Within(0.001f));
            Assert.That(ballistaLv2.hitSfx.HasPlayableClip, Is.True);

            var fireballLv3 = catalog.Resolve(CastleTurretFamily.Fireball, 3).Data;
            Assert.That(fireballLv3.impactType, Is.EqualTo(CastleTurretImpactType.ExplosionArea));
            Assert.That(fireballLv3.baseDamage, Is.EqualTo(22f).Within(0.001f));
            Assert.That(fireballLv3.cooldown, Is.EqualTo(4.6f).Within(0.001f));
            Assert.That(fireballLv3.explosionRadius, Is.EqualTo(3.2f).Within(0.001f));
            Assert.That(fireballLv3.explosionSfx.HasPlayableClip, Is.True);
            Assert.That(CastleTurretDamageMath.ResolveExplosionDamage(22f, 3.2f, 0f), Is.EqualTo(22f).Within(0.001f));
            Assert.That(CastleTurretDamageMath.ResolveExplosionDamage(22f, 3.2f, 3.2f), Is.EqualTo(11f).Within(0.001f));
        }

        [Test]
        public void TurretLineOfFire_BlocksOnlySegmentsCrossingAliveWallBounds()
        {
            var wallBounds = new Bounds(Vector3.zero, new Vector3(1f, 2f, 1f));

            Assert.That(CastleTurretLineOfFireMath.IntersectsPlanarBounds(
                new Vector3(-3f, 4f, 0f),
                new Vector3(3f, 0f, 0f),
                wallBounds,
                0.1f), Is.True);
            Assert.That(CastleTurretLineOfFireMath.IntersectsPlanarBounds(
                new Vector3(-3f, 4f, 2f),
                new Vector3(3f, 0f, 2f),
                wallBounds,
                0.1f), Is.False);
        }

        [Test]
        public void BreachLink_UsesWallNormalAndRejectsTheNextAliveWall()
        {
            var horizontalMask = CastleWallNeighborMask.East | CastleWallNeighborMask.West;
            var verticalMask = CastleWallNeighborMask.North | CastleWallNeighborMask.South;
            Assert.That(
                CastleBreachLinkMath.ResolveInwardDirection(Vector3.zero, new Vector3(4f, 0f, 8f), horizontalMask),
                Is.EqualTo(Vector3.forward));
            Assert.That(
                CastleBreachLinkMath.ResolveInwardDirection(Vector3.zero, new Vector3(8f, 0f, 4f), verticalMask),
                Is.EqualTo(Vector3.right));
            Assert.That(
                CastleBreachLinkMath.AreEndpointsOnOppositeSides(
                    Vector3.zero,
                    Vector3.forward,
                    new Vector3(0f, 0f, -1.05f),
                    new Vector3(0f, 0f, 1.05f)),
                Is.True);

            var nextWall = new Bounds(new Vector3(0f, 1f, 1f), new Vector3(0.92f, 2f, 0.92f));
            var sideWall = new Bounds(new Vector3(1f, 1f, 0f), new Vector3(0.92f, 2f, 0.92f));
            Assert.That(CastleTurretLineOfFireMath.IntersectsPlanarBounds(
                new Vector3(0f, 0f, -1.05f),
                new Vector3(0f, 0f, 1.05f),
                nextWall,
                0.525f), Is.True, "다음 방어층 성벽을 링크가 넘어가면 안 됩니다.");
            Assert.That(CastleTurretLineOfFireMath.IntersectsPlanarBounds(
                new Vector3(0f, 0f, -1.05f),
                new Vector3(0f, 0f, 1.05f),
                sideWall,
                0.525f), Is.False, "파괴 타일 양옆 성벽 때문에 중앙 통로까지 막히면 안 됩니다.");
        }

        [Test]
        public void OverheadHealthBar_IsLazyColoredAndHiddenOnDeath()
        {
            var friendlyRoot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var hostileRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var friendlyHealth = friendlyRoot.AddComponent<HealthComponent>();
                var friendlyUnit = friendlyRoot.AddComponent<CastleAssaultUnit>();
                friendlyHealth.Initialize(100f);
                var friendlyReport = default(DamageReport);
                friendlyHealth.Damaged += report => friendlyReport = report;
                friendlyHealth.ApplyDamage(new DamageRequest(null, 25f, friendlyRoot.transform.position));
                var referenceResolver = typeof(CastleAssaultUnit).GetMethod(
                    "ResolveReferences",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var damageHandler = typeof(CastleAssaultUnit).GetMethod(
                    "HandleDamaged",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(referenceResolver, Is.Not.Null);
                Assert.That(damageHandler, Is.Not.Null);
                referenceResolver.Invoke(friendlyUnit, null); // EditMode는 Awake를 자동 실행하지 않는다
                damageHandler.Invoke(friendlyUnit, new object[] { friendlyReport });
                Assert.That(
                    friendlyRoot.TryGetComponent<CastleRaidOverheadHealthBar>(out var friendlyBar),
                    Is.True);

                Assert.That(friendlyBar, Is.Not.Null);
                Assert.That(friendlyBar.IsVisible, Is.True);
                Assert.That(friendlyBar.FillRatio, Is.EqualTo(0.75f).Within(0.001f));
                Assert.That(friendlyBar.FillColor, Is.EqualTo(CastleRaidOverheadHealthBar.FriendlyColor));

                var hostileHealth = hostileRoot.AddComponent<HealthComponent>();
                var hostileTarget = hostileRoot.AddComponent<CastleTarget>();
                hostileTarget.EditorConfigure(CastleTargetKind.Defender, 50f, null, null);
                hostileTarget.Initialize();
                hostileHealth.ApplyDamage(new DamageRequest(null, 10f, hostileRoot.transform.position));

                Assert.That(hostileRoot.TryGetComponent<CastleRaidOverheadHealthBar>(out var hostileBar), Is.True);
                Assert.That(hostileBar.IsVisible, Is.True);
                Assert.That(hostileBar.FillRatio, Is.EqualTo(0.8f).Within(0.001f));
                Assert.That(hostileBar.FillColor, Is.EqualTo(CastleRaidOverheadHealthBar.HostileColor));

                hostileHealth.ApplyDamage(new DamageRequest(null, 100f, hostileRoot.transform.position));
                Assert.That(hostileBar.IsVisible, Is.False);
                hostileTarget.Shutdown();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(friendlyRoot);
                UnityEngine.Object.DestroyImmediate(hostileRoot);
            }
        }

        [Test]
        public void CastleDefenderCatalog_ReferencesAllFormalExpeditionEnemyPrefabs()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CastleDefenderCatalog>(
                "Assets/ProjectMT/04_Contents/01_CastleRaid/Resources/CastleRaidDefenderCatalog.asset");

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.IsComplete, Is.True);
            Assert.That(catalog.DefenderPrefabs.Count, Is.EqualTo(7));
            Assert.That(
                Enumerable.Range(0, 7)
                    .Select(seed => catalog.Resolve(seed, 2).name)
                    .Distinct()
                    .Count(),
                Is.EqualTo(7),
                "현재 임시 규칙에서는 농부·기사 1~3·마법사 1~3이 모두 무작위 후보여야 합니다.");
        }

        [Test]
        public void GeneratedTargetPriority_AfterBreachChoosesPalaceBeforeDefender()
        {
            var controllerObject = new GameObject("CastleRaidController_PriorityTest");
            var palaceObject = new GameObject("Palace_PriorityTest");
            var defenderObject = new GameObject("Defender_PriorityTest");
            try
            {
                var controller = controllerObject.AddComponent<CastleRaidController>();
                var palace = CreateTarget(palaceObject, CastleTargetKind.MainCastle, 700f);
                var defender = CreateTarget(defenderObject, CastleTargetKind.Defender, 80f);
                var controllerType = typeof(CastleRaidController);
                controllerType.GetField(
                        "hasGenerationTargetMetadata",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.SetValue(controller, true);
                controllerType.GetField(
                        "innerPathOpen",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.SetValue(controller, true);
                controllerType.GetField(
                        "mainCastle",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.SetValue(controller, palace);
                var defenders = controllerType.GetField(
                        "aliveDefenders",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.GetValue(controller) as System.Collections.Generic.List<CastleTarget>;
                defenders?.Add(defender);

                Assert.That(controller.FindPriorityTarget(null), Is.SameAs(palace));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(defenderObject);
                UnityEngine.Object.DestroyImmediate(palaceObject);
                UnityEngine.Object.DestroyImmediate(controllerObject);
            }
        }

        private static CastleTarget CreateTarget(
            GameObject targetObject,
            CastleTargetKind kind,
            float healthValue)
        {
            targetObject.AddComponent<HealthComponent>();
            var target = targetObject.AddComponent<CastleTarget>();
            target.EditorConfigure(kind, healthValue, null, null);
            target.Initialize();
            return target;
        }

        [Test]
        public void ComputedAttackSlots_KeepEightDirectionsWithoutChildTransforms()
        {
            var target = new GameObject("ComputedSlotTarget");
            var secondOwner = new GameObject("SecondOwner");
            try
            {
                var provider = target.AddComponent<AttackSlotProvider>();
                provider.ConfigureComputedSlots(new Vector2(2f, 4f), 0.5f);

                Assert.That(provider.UsesComputedSlots, Is.True);
                Assert.That(provider.SlotCount, Is.EqualTo(8));
                Assert.That(target.transform.childCount, Is.Zero);
                Assert.That(provider.TryLeasePosition(
                    provider,
                    new Vector3(0f, 0f, 10f),
                    null,
                    out var firstIndex,
                    out var firstPosition), Is.True);
                Assert.That(firstIndex, Is.EqualTo(0));
                Assert.That(firstPosition, Is.EqualTo(new Vector3(0f, 0f, 2.5f)));
                Assert.That(provider.TryLeasePosition(
                    secondOwner.transform,
                    new Vector3(0f, 0f, 10f),
                    null,
                    out var secondIndex,
                    out _), Is.True);
                Assert.That(secondIndex, Is.Not.EqualTo(firstIndex));

                provider.Release(provider);
                Assert.That(provider.TryLeasePosition(
                    secondOwner.transform,
                    new Vector3(0f, 0f, 10f),
                    null,
                    out var retainedIndex,
                    out _), Is.True);
                Assert.That(retainedIndex, Is.EqualTo(secondIndex));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(secondOwner);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void PlayablePreview_BuildsDamageableTargetsAndRestoresScenePresentation()
        {
            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                var sceneRoot = new GameObject("00_SceneRoot");
                SceneManager.MoveGameObjectToScene(sceneRoot, previewScene);
                var worldRoot = new GameObject("01_WorldRoot");
                worldRoot.transform.SetParent(sceneRoot.transform, false);
                var existingStage = new GameObject("CastleStage_Seed");
                existingStage.transform.SetParent(worldRoot.transform, false);

                var controllerObject = new GameObject("CastleRaidController");
                controllerObject.transform.SetParent(sceneRoot.transform, false);
                controllerObject.AddComponent<CastleRaidController>();

                var cameraObject = new GameObject("CastleRaidCamera");
                cameraObject.transform.SetParent(sceneRoot.transform, false);
                cameraObject.transform.SetPositionAndRotation(
                    new Vector3(15f, 18f, -15f),
                    Quaternion.Euler(40.32f, 315f, 0f));
                var castleCamera = cameraObject.AddComponent<Camera>();
                castleCamera.orthographic = true;
                castleCamera.orthographicSize = 11.5f;
                cameraObject.AddComponent<CastleRaidCameraController>();
                var originalCameraPosition = castleCamera.transform.position;

                var rules = CastleGenerationAssetFactory.CreateOrUpdateDefaults();
                var candidate = new CastleGenerator().Generate(
                    rules,
                    10801,
                    CastleLayoutTheme.TwinSpiralFortress,
                    3);

                var runtimeStage = CastleGenerationPlayablePreview.Rebuild(
                    candidate,
                    previewScene,
                    Vector3.zero,
                    1f);
                var root = runtimeStage.gameObject;
                var targets = runtimeStage.Targets;

                Assert.That(root.name, Is.EqualTo(CastleGenerationPlayablePreview.PlayableRootName));
                Assert.That(existingStage.activeSelf, Is.False);
                Assert.That(targets, Has.Length.EqualTo(candidate.Placements.Count));
                Assert.That(root.GetComponentsInChildren<HealthComponent>(true), Has.Length.EqualTo(targets.Length));
                Assert.That(
                    root.GetComponentsInChildren<Transform>(true)
                        .Count(transform => transform.name.StartsWith("AttackSlot_", StringComparison.Ordinal)),
                    Is.EqualTo(targets.Length * 8));
                Assert.That(root.GetComponentsInChildren<NavMeshObstacle>(true).Length, Is.GreaterThan(0));

                foreach (var target in targets)
                {
                    var placement = candidate.Placements.Single(value => value.PlacementId == target.gameObject.name);
                    Assert.That(target.Health.MaxHealth, Is.EqualTo(placement.EffectiveHealth).Within(0.001f));
                    Assert.That(target.Health.CurrentHealth, Is.EqualTo(placement.EffectiveHealth).Within(0.001f));
                    Assert.That(target.HasGenerationMetadata, Is.True);
                    Assert.That(target.PlacementId, Is.EqualTo(placement.PlacementId));
                    Assert.That(target.DistrictId, Is.EqualTo(placement.DistrictId));
                    Assert.That(target.OwnerDistrictIds, Is.EquivalentTo(placement.OwnerDistrictIds));
                    Assert.That(target.WallBand, Is.EqualTo(placement.WallBand));
                    Assert.That(target.WallDefenseLayer, Is.EqualTo(placement.WallDefenseLayer));
                }

                var palace = targets.Single(target => target.TargetKind == CastleTargetKind.MainCastle);
                Assert.That(palace.Health.MaxHealth, Is.EqualTo(700f).Within(0.001f));

                var wall = targets.First(target => target.TargetKind == CastleTargetKind.Wall);
                var wallCollider = wall.GetComponent<Collider>();
                var wallRenderer = wall.GetComponentInChildren<Renderer>();
                var wallObstacle = wall.GetComponent<NavMeshObstacle>();
                wall.Initialize();
                wall.Health.ApplyDamage(new DamageRequest(
                    null,
                    wall.Health.MaxHealth * 0.5f,
                    wall.transform.position));
                Assert.That(wall.Health.CurrentHealth, Is.EqualTo(wall.Health.MaxHealth * 0.5f).Within(0.001f));
                Assert.That(wallCollider.enabled, Is.True, "생존 성벽 Collider가 꺼졌습니다.");
                Assert.That(wallObstacle.enabled, Is.True, "생존 성벽 NavMeshObstacle이 꺼졌습니다.");

                wall.Health.ApplyDamage(new DamageRequest(
                    null,
                    wall.Health.MaxHealth,
                    wall.transform.position));
                Assert.That(wall.IsAlive, Is.False);
                Assert.That(wallCollider.enabled, Is.False);
                Assert.That(wallRenderer.enabled, Is.False);
                Assert.That(wallObstacle.enabled, Is.False);

                Assert.That(CastleGenerationPlayablePreview.Clear(previewScene), Is.EqualTo(1));
                Assert.That(root == null, Is.True, "플레이 프리뷰 임시 Root가 제거되지 않았습니다.");
                Assert.That(existingStage.activeSelf, Is.True, "기존 CastleStage_Seed가 복원되지 않았습니다.");
                Assert.That(castleCamera.orthographicSize, Is.EqualTo(11.5f).Within(0.001f));
                Assert.That(castleCamera.transform.position, Is.EqualTo(originalCameraPosition));
            }
            finally
            {
                CastleGenerationPlayablePreview.Clear(previewScene);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }
    }
}
