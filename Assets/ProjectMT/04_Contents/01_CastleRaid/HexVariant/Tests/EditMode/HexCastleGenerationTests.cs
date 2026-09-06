using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaidHex.Tests
{
    public sealed class HexCastleGenerationTests
    {
        [Test]
        public void AxialCoordinates_UseSixEqualNeighbors()
        {
            var origin = new HexCoordinates(0, 0);
            var neighbors = Enumerable.Range(0, 6).Select(origin.Neighbor).ToArray();

            Assert.That(neighbors.Distinct().Count(), Is.EqualTo(6));
            Assert.That(neighbors.All(value => origin.DistanceTo(value) == 1), Is.True);
        }

        [Test]
        public void WorldConversion_RoundTripsAcrossLargestFoundationBoard()
        {
            var boardRadius = HexCastleFoundationGenerator.ResolveCanonicalWallRadii(4).Last() +
                              HexSpatialContract.MinimumDeploymentRings;
            foreach (var coordinates in HexCoordinates.EnumerateRadius(boardRadius))
            {
                Assert.That(
                    HexSpatialContract.FromWorld(HexSpatialContract.ToWorld(coordinates)),
                    Is.EqualTo(coordinates),
                    coordinates.ToString());
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void RoutePlanner_CrossesEveryRequestedDefenseLayer(int defenseLayers)
        {
            var layout = new HexCastleFoundationGenerator().Generate(3317, defenseLayers);
            var planner = new HexRoutePlanner();
            for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
            {
                var start = HexCoordinates.Directions[direction] * layout.BattlefieldRadius;
                var route = planner.FindMinimumBreachRoute(layout, start);

                Assert.That(route.IsComplete, Is.True, $"direction={direction}");
                Assert.That(route.Path.First(), Is.EqualTo(start));
                Assert.That(route.Path.Last(), Is.EqualTo(new HexCoordinates(0, 0)));
                Assert.That(route.CrossedDefenseLayers, Is.EqualTo(Enumerable.Range(1, defenseLayers)));
                Assert.That(route.WallCellsToBreak, Is.GreaterThanOrEqualTo(defenseLayers));
                Assert.That(route.Path.Zip(route.Path.Skip(1), (left, right) => left.DistanceTo(right))
                    .All(distance => distance == 1), Is.True);
            }
        }

        [Test]
        public void ThemeOneFoundation_RoundTripsBuildingRolesWithoutHashDrift()
        {
            var candidate = new HexCastleGenerationPipeline().GenerateFoundation(10801, 4);
            var asset = ScriptableObject.CreateInstance<HexCastleStageLayout>();
            try
            {
                asset.Configure(candidate);
                var restored = asset.BuildLayout();
                var originalBuildings = candidate.Layout.Cells.Values
                    .Where(cell => cell.IsBuildingCell)
                    .OrderBy(cell => cell.Coordinates)
                    .ToArray();
                var restoredBuildings = restored.Cells.Values
                    .Where(cell => cell.IsBuildingCell)
                    .OrderBy(cell => cell.Coordinates)
                    .ToArray();
                var originalGates = candidate.Layout.Enumerate(HexCastleCellKind.Gate)
                    .OrderBy(cell => cell.Coordinates)
                    .ToArray();
                var restoredGates = restored.Enumerate(HexCastleCellKind.Gate)
                    .OrderBy(cell => cell.Coordinates)
                    .ToArray();

                Assert.That(restored.LayoutSignature, Is.EqualTo(candidate.Layout.LayoutSignature));
                Assert.That(asset.RulesVersion, Is.EqualTo(
                    HexCastleFoundationGenerator.FoundationRulesVersionBase +
                    HexCastleThemeOneTuning.CurrentDraftVersion));
                Assert.That(restored.RulesVersion, Is.EqualTo(asset.RulesVersion));
                Assert.That(restoredBuildings.Select(cell => cell.BuildingRole),
                    Is.EqualTo(originalBuildings.Select(cell => cell.BuildingRole)));
                Assert.That(restoredBuildings.Select(cell => cell.PlacementDensity),
                    Is.EqualTo(originalBuildings.Select(cell => cell.PlacementDensity)));
                Assert.That(restoredBuildings.Select(cell => cell.BuildingGrade),
                    Is.EqualTo(originalBuildings.Select(cell => cell.BuildingGrade)));
                Assert.That(restoredBuildings.Select(cell => cell.TurretWeaponKind),
                    Is.EqualTo(originalBuildings.Select(cell => cell.TurretWeaponKind)));
                Assert.That(restoredBuildings.Select(cell => cell.TurretRangeCells),
                    Is.EqualTo(originalBuildings.Select(cell => cell.TurretRangeCells)));
                Assert.That(restoredBuildings.Select(cell => cell.TurretCanAttackAcrossWalls),
                    Is.EqualTo(originalBuildings.Select(cell => cell.TurretCanAttackAcrossWalls)));
                Assert.That(restoredBuildings.Select(cell => cell.VisualVariantId),
                    Is.EqualTo(originalBuildings.Select(cell => cell.VisualVariantId)));
                Assert.That(restoredGates.Select(cell => cell.GateRole),
                    Is.EqualTo(originalGates.Select(cell => cell.GateRole)));
                Assert.That(restoredGates.Select(cell => cell.GatePassageMask),
                    Is.EqualTo(originalGates.Select(cell => cell.GatePassageMask)));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ThemeOneFoundation_IsDeterministicAndSeedChangesBuildingLayout()
        {
            var generator = new HexCastleFoundationGenerator();
            var first = generator.Generate(10801, 3);
            var repeat = generator.Generate(10801, 3);
            var changed = generator.Generate(10802, 3);

            Assert.That(repeat.LayoutSignature, Is.EqualTo(first.LayoutSignature));
            Assert.That(repeat.StructureSignature, Is.EqualTo(first.StructureSignature));
            Assert.That(changed.StructureSignature, Is.Not.EqualTo(first.StructureSignature));
            Assert.That(changed.LayoutSignature, Is.Not.EqualTo(first.LayoutSignature));
        }

        [Test]
        public void FormalThemeCatalog_IsExactlyAThroughIAndPreservesInnerRingsWhenExpanded()
        {
            const int seed = 10801;
            var themes = HexCastleThemeCatalog.ComparisonThemes;
            Assert.That(themes, Is.EqualTo(new[]
            {
                HexCastleTheme.CentralCompartment,
                HexCastleTheme.DiamondRadial,
                HexCastleTheme.CompositeCompartments,
                HexCastleTheme.HexHoneycomb,
                HexCastleTheme.PetalBloom,
                HexCastleTheme.CrystalMandala,
                HexCastleTheme.FractalBastion,
                HexCastleTheme.VoronoiCrystal,
                HexCastleTheme.IrisShutter
            }));
            Assert.That(themes.Select(HexCastleThemeCatalog.ResolveCode),
                Is.EqualTo("ABCDEFGHI".ToCharArray()));
            var signatures = new HashSet<string>();
            foreach (var theme in themes)
            {
                HexCastleSilhouettePlan previous = null;
                for (var defenseLayers = 2; defenseLayers <= 4; defenseLayers++)
                {
                    var radii = HexCastleFoundationGenerator.ResolveCanonicalWallRadii(defenseLayers);
                    var plan = HexCastleSilhouettePlanner.Build(theme, seed, radii);
                    Assert.That(plan.Rings.Count, Is.EqualTo(defenseLayers), theme.ToString());
                    if (previous != null)
                    {
                        for (var index = 0; index < previous.Rings.Count; index++)
                        {
                            Assert.That(plan.Rings[index].Cells,
                                Is.EqualTo(previous.Rings[index].Cells),
                                $"{theme}의 {index + 1}중벽 안쪽 골격이 증축 중 바뀌었습니다.");
                        }
                    }

                    previous = plan;
                }

                var largestPlan = previous;
                var signature = string.Join("|", largestPlan.Rings.Select(ring =>
                    string.Join(";", ring.Cells.Select(cell => $"{cell.Q},{cell.R}"))));
                Assert.That(signatures.Add(signature), Is.True, $"{theme} 외곽·중벽이 다른 후보와 같습니다.");
            }
        }

        [TestCase(HexCastleTheme.DiamondRadial, 6)]
        [TestCase(HexCastleTheme.CompositeCompartments, 6)]
        [TestCase(HexCastleTheme.HexHoneycomb, 6)]
        [TestCase(HexCastleTheme.PetalBloom, 6)]
        [TestCase(HexCastleTheme.CrystalMandala, 6)]
        [TestCase(HexCastleTheme.FractalBastion, 6)]
        [TestCase(HexCastleTheme.VoronoiCrystal, 6)]
        [TestCase(HexCastleTheme.IrisShutter, 6)]
        public void FormalTheme_UsesNaturalConnectedWallModulesAndGameplayCells(
            HexCastleTheme theme,
            int partitionsPerBand)
        {
            var generator = new HexCastleFoundationGenerator();
            for (var defenseLayers = 2; defenseLayers <= 4; defenseLayers++)
            {
                HexCastleLayout layout = null;
                Assert.DoesNotThrow(
                    () => layout = generator.Generate(10801, defenseLayers, theme),
                    $"{theme}/{defenseLayers}중벽 정식 생성 실패");
                var topology = HexCastleWallTopologyResolver.Build(layout);
                var plan = HexCastleSilhouettePlanner.Build(
                    theme,
                    10801,
                    HexCastleFoundationGenerator.ResolveCanonicalWallRadii(defenseLayers));
                Assert.That(plan.Partitions.Count,
                    Is.EqualTo((defenseLayers - 1) * partitionsPerBand));
                Assert.That(layout.Enumerate(HexCastleCellKind.Palace).Count(), Is.EqualTo(7));
                Assert.That(layout.Cells.Values.Any(cell => cell.IsBuildingCell), Is.True);
                Assert.That(layout.Enumerate(HexCastleCellKind.Gate).Any(), Is.True);

                foreach (var pair in topology)
                {
                    Assert.That(pair.Value.ConnectionCount, Is.InRange(2, 4), pair.Key.ToString());
                    var cell = layout.Cells[pair.Key];
                    if (cell.Kind != HexCastleCellKind.Wall)
                    {
                        Assert.That(cell.Kind,
                            Is.EqualTo(HexCastleCellKind.Tower).Or.EqualTo(HexCastleCellKind.Gate));
                        continue;
                    }

                    Assert.That(pair.Value.ConnectionCount, Is.EqualTo(2));
                    var directions = pair.Value.GetDirections();
                    _ = HexCastleWallVisualResolver.ResolveDirections(
                        HexCastleCellKind.Wall,
                        directions[0],
                        directions[1]);
                }

                Assert.That(layout.Enumerate(HexCastleCellKind.Tower).Any(), Is.True,
                    $"{theme}의 모서리·격벽 접합 타워가 없습니다.");
            }
        }

        [Test]
        public void FormalThemes_SeedSweepPreservesConnectedTopologyAndValidation()
        {
            var pipeline = new HexCastleGenerationPipeline();
            var themes = HexCastleThemeCatalog.Themes;
            foreach (var theme in themes)
            {
                for (var defenseLayers = 2; defenseLayers <= 4; defenseLayers++)
                {
                    for (var index = 0; index < 24; index++)
                    {
                        var seed = unchecked(10801 + index * 7919);
                        HexCastleCandidate candidate = null;
                        Assert.DoesNotThrow(() => candidate = pipeline.GenerateFoundation(
                            seed,
                            defenseLayers,
                            theme), $"{theme}/{defenseLayers}중벽/Seed {seed}");
                        Assert.That(candidate.Validation.IsValid, Is.True,
                            $"{theme}/{defenseLayers}중벽/Seed {seed}: " +
                            string.Join(" | ", candidate.Validation.Errors));
                    }
                }
            }
        }

        [Test]
        public void FormalThemes_SeedSweepAlwaysPreservesBarracksExitCells()
        {
            var generator = new HexCastleFoundationGenerator();
            foreach (var theme in HexCastleThemeCatalog.Themes)
            {
                for (var defenseLayerCount = 2; defenseLayerCount <= 4; defenseLayerCount++)
                {
                    for (var index = 0; index < 32; index++)
                    {
                        var seed = unchecked(10801 + index * 7919);
                        HexCastleLayout layout = null;
                        Assert.DoesNotThrow(
                            () => layout = generator.Generate(seed, defenseLayerCount, theme),
                            $"{theme}/L{defenseLayerCount}/Seed {seed} 생성 실패");

                        var barracks = layout.Cells.Values.Where(cell =>
                            cell.BuildingRole == HexCastleBuildingRole.KnightBarracks ||
                            cell.BuildingRole == HexCastleBuildingRole.FarmerBarracks);
                        foreach (var cell in barracks)
                        {
                            var openNeighbors = HexCoordinates.Directions.Count(direction =>
                                layout.TryGetCell(cell.Coordinates + direction, out var neighbor) &&
                                neighbor.Kind == HexCastleCellKind.Ground &&
                                neighbor.IsOpen);
                            Assert.That(
                                openNeighbors,
                                Is.GreaterThanOrEqualTo(2),
                                $"{theme}/L{defenseLayerCount}/Seed {seed}/병영 {cell.Coordinates}");
                        }
                    }
                }
            }
        }

        [Test]
        public void DifficultyReport_UsesSixEntryRoutesAndProducesStage()
        {
            var candidate = new HexCastleGenerationPipeline().GenerateFoundation(10801, 4);

            Assert.That(candidate.Validation.EntryRoutes.Count, Is.EqualTo(6));
            Assert.That(candidate.Difficulty.MinimumBreachCost, Is.GreaterThan(0f));
            Assert.That(candidate.Difficulty.MaximumBreachCost,
                Is.GreaterThanOrEqualTo(candidate.Difficulty.MinimumBreachCost));
            Assert.That(candidate.Difficulty.SuggestedStage, Is.InRange(1, 50));
        }

        [Test]
        public void RuntimeAssembly_DoesNotReferenceSquareCastleRaidAssembly()
        {
            var references = typeof(HexCoordinates).Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain("ProjectMT.Contents.CastleRaid"));
        }

        [Test]
        public void RuntimeAssembly_DoesNotReferenceNavigationAssemblies()
        {
            var references = typeof(HexCoordinates).Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain("Unity.AI.Navigation"));
            Assert.That(references, Does.Not.Contain("UnityEngine.AIModule"));
        }

        [Test]
        public void PerspectiveCamera_AutoFitDistanceGrowsWithTwoThreeAndFourWallBoards()
        {
            var cameraObject = new GameObject("HexPerspectiveCameraFitTest");
            var renderTexture = new RenderTexture(960, 540, 16);
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.targetTexture = renderTexture;
                var controller = cameraObject.AddComponent<HexCastleCameraController>();

                controller.ConfigureBounds(7, HexSpatialContract.CellOuterRadius);
                var twoWallDistance = controller.DefaultDistance;
                var twoWallShadowDistance = controller.RequiredShadowDistance;
                controller.ConfigureBounds(10, HexSpatialContract.CellOuterRadius);
                var threeWallDistance = controller.DefaultDistance;
                var threeWallShadowDistance = controller.RequiredShadowDistance;
                controller.ConfigureBounds(13, HexSpatialContract.CellOuterRadius);
                var fourWallDistance = controller.DefaultDistance;
                var fourWallShadowDistance = controller.RequiredShadowDistance;

                Assert.That(camera.orthographic, Is.False);
                Assert.That(controller.IsPerspective, Is.True);
                Assert.That(twoWallDistance, Is.GreaterThan(0f));
                Assert.That(threeWallDistance, Is.GreaterThan(twoWallDistance));
                Assert.That(fourWallDistance, Is.GreaterThan(threeWallDistance));
                Assert.That(threeWallShadowDistance, Is.GreaterThan(twoWallShadowDistance));
                Assert.That(fourWallShadowDistance, Is.GreaterThan(threeWallShadowDistance));
                Assert.That(fourWallShadowDistance, Is.GreaterThan(120f));
                Assert.That(controller.MinimumDistance, Is.LessThan(controller.DefaultDistance));
                Assert.That(controller.MaximumDistance, Is.GreaterThan(controller.DefaultDistance));
                Assert.That(controller.InitialZoomRatio, Is.EqualTo(0.70f).Within(0.001f));
                Assert.That(controller.VerticalScreenOffset, Is.EqualTo(0.10f).Within(0.001f));
                var palaceViewport = camera.WorldToViewportPoint(Vector3.zero);
                Assert.That(palaceViewport.x, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(palaceViewport.y, Is.EqualTo(0.60f).Within(0.001f));
                Assert.That(controller.MinimumPanRange, Is.GreaterThanOrEqualTo(6f));
                Assert.That(controller.ExtraPanRange, Is.GreaterThanOrEqualTo(3.5f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(renderTexture);
            }
        }

        [Test]
        public void PerspectiveCamera_ShadowDistanceOverrideIsScopedAndRestored()
        {
            var pipelineAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            var shadowDistanceProperty = pipelineAsset?.GetType().GetProperty("shadowDistance");
            Assert.That(shadowDistanceProperty, Is.Not.Null, "현재 URP Asset의 shadowDistance를 찾지 못했습니다.");

            var originalShadowDistance = (float)shadowDistanceProperty.GetValue(pipelineAsset);
            var cameraObject = new GameObject("HexShadowDistanceScopeTest");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                var controller = cameraObject.AddComponent<HexCastleCameraController>();
                controller.ConfigureBounds(13, HexSpatialContract.CellOuterRadius);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var begin = typeof(HexCastleCameraController).GetMethod("HandleBeginCameraRendering", flags);
                var end = typeof(HexCastleCameraController).GetMethod("HandleEndCameraRendering", flags);
                Assert.That(begin, Is.Not.Null);
                Assert.That(end, Is.Not.Null);

                begin.Invoke(controller, new object[]
                {
                    default(UnityEngine.Rendering.ScriptableRenderContext),
                    camera
                });
                Assert.That(
                    (float)shadowDistanceProperty.GetValue(pipelineAsset),
                    Is.GreaterThanOrEqualTo(controller.RequiredShadowDistance));

                end.Invoke(controller, new object[]
                {
                    default(UnityEngine.Rendering.ScriptableRenderContext),
                    camera
                });
                Assert.That(
                    (float)shadowDistanceProperty.GetValue(pipelineAsset),
                    Is.EqualTo(originalShadowDistance).Within(0.001f));
            }
            finally
            {
                shadowDistanceProperty.SetValue(pipelineAsset, originalShadowDistance);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void PerspectiveCamera_ScrollZoomAndDragPanChangeTargets()
        {
            var cameraObject = new GameObject("HexPerspectiveCameraInputTest");
            var renderTexture = new RenderTexture(512, 512, 16);
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.targetTexture = renderTexture;
                var controller = cameraObject.AddComponent<HexCastleCameraController>();
                controller.ConfigureBounds(10, HexSpatialContract.CellOuterRadius);
                var initialDistance = controller.TargetDistance;
                var screenCenter = new Vector2(256f, 256f);

                controller.ZoomByScroll(screenCenter, 120f);
                Assert.That(controller.TargetDistance,
                    Is.EqualTo(initialDistance * Mathf.Exp(-0.18f)).Within(0.001f));
                Assert.That(controller.TargetDistance, Is.GreaterThan(controller.MinimumDistance));
                controller.EditorStep(1f);
                Assert.That(controller.CurrentDistance, Is.LessThan(initialDistance));

                controller.ResetView();
                var pinchStartDistance = controller.TargetDistance;
                controller.BeginPointer(1, new Vector2(206f, 256f));
                controller.BeginPointer(2, new Vector2(306f, 256f));
                controller.MovePointer(1, new Vector2(156f, 256f));
                Assert.That(controller.TargetDistance, Is.LessThan(pinchStartDistance));
                controller.EndPointer(1);
                controller.EndPointer(2);

                var beforePan = controller.TargetGroundCenter;
                controller.BeginPointer(-1, screenCenter);
                controller.MovePointer(-1, screenCenter + Vector2.right * 96f);
                controller.EndPointer(-1);

                Assert.That(controller.TargetGroundCenter, Is.Not.EqualTo(beforePan));
                Assert.That(controller.ConsumeClickSuppression(-1), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(renderTexture);
            }
        }

        [Test]
        public void PerspectiveCamera_HoldRotationOrbitsContinuouslyAroundFocus()
        {
            var cameraObject = new GameObject("HexPerspectiveCameraRotationTest");
            var renderTexture = new RenderTexture(960, 540, 16);
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.targetTexture = renderTexture;
                var controller = cameraObject.AddComponent<HexCastleCameraController>();
                controller.ConfigureBounds(10, HexSpatialContract.CellOuterRadius);
                var initialPosition = camera.transform.position;
                var initialDistance = controller.CurrentDistance;

                var screenCenter = new Vector2(480f, 270f);
                controller.BeginPointer(-1, screenCenter);
                controller.MovePointer(-1, screenCenter + Vector2.right * 160f);
                controller.EndPointer(-1);
                for (var index = 0; index < 10; index++)
                {
                    controller.EditorStep(0.05f);
                }

                var pivotBeforeRotation = ResolveGroundCenter(camera, controller.VerticalScreenOffset);
                var palaceCenter = controller.RotationFocusGroundCenter;
                var initialOffsetFromPalace = Vector2.Distance(pivotBeforeRotation, palaceCenter);
                Assert.That(initialOffsetFromPalace, Is.GreaterThan(0.1f));

                controller.BeginRotateRight();
                controller.EditorStep(controller.RotationCenteringDuration * 0.5f);
                var pivotDuringCentering = ResolveGroundCenter(camera, controller.VerticalScreenOffset);
                Assert.That(controller.YawDegrees,
                    Is.EqualTo(controller.RotationSpeedDegrees * controller.RotationCenteringDuration * 0.5f)
                        .Within(0.01f));
                Assert.That(controller.IsRotationCentering, Is.True);
                Assert.That(Vector2.Distance(pivotDuringCentering, palaceCenter),
                    Is.LessThan(initialOffsetFromPalace));

                controller.EditorStep(controller.RotationCenteringDuration * 0.5f);
                var pivotAfterCentering = ResolveGroundCenter(camera, controller.VerticalScreenOffset);
                Assert.That(controller.IsRotationCentering, Is.False);
                Assert.That(controller.YawDegrees,
                    Is.EqualTo(controller.RotationSpeedDegrees * controller.RotationCenteringDuration)
                        .Within(0.01f));
                Assert.That(pivotAfterCentering.x, Is.EqualTo(palaceCenter.x).Within(0.01f));
                Assert.That(pivotAfterCentering.y, Is.EqualTo(palaceCenter.y).Within(0.01f));

                controller.EditorStep(0.5f);
                var pivotDuringRotation = ResolveGroundCenter(camera, controller.VerticalScreenOffset);

                Assert.That(controller.YawDegrees,
                    Is.EqualTo(controller.RotationSpeedDegrees *
                        (controller.RotationCenteringDuration + 0.5f)).Within(0.01f));
                Assert.That(camera.transform.position, Is.Not.EqualTo(initialPosition));
                Assert.That(controller.CurrentDistance, Is.EqualTo(initialDistance).Within(0.01f));
                Assert.That(pivotDuringRotation.x, Is.EqualTo(palaceCenter.x).Within(0.01f));
                Assert.That(pivotDuringRotation.y, Is.EqualTo(palaceCenter.y).Within(0.01f));

                controller.StopRotation();
                var stoppedYaw = controller.YawDegrees;
                controller.EditorStep(0.5f);
                var pivotAfterRotation = ResolveGroundCenter(camera, controller.VerticalScreenOffset);
                Assert.That(controller.YawDegrees, Is.EqualTo(stoppedYaw).Within(0.01f));
                Assert.That(pivotAfterRotation.x, Is.EqualTo(palaceCenter.x).Within(0.01f));
                Assert.That(pivotAfterRotation.y, Is.EqualTo(palaceCenter.y).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(renderTexture);
            }
        }

        [Test]
        public void ExteriorDeployment_FollowsWallContourAcrossAllThemesAndLayers()
        {
            var generator = new HexCastleFoundationGenerator();
            var expandedCount = 0;
            foreach (var theme in HexCastleSilhouettePlanner.SupportedThemes)
            for (var layers = 2; layers <= 4; layers++)
            {
                var layout = generator.Generate(26639, layers, theme);
                var outerWall = layout.Cells.Values.Where(cell => cell.IsWallPathCell &&
                    cell.DefenseLayer == layers).Select(cell => cell.Coordinates).ToHashSet();
                var radius = outerWall.Max(coordinates => coordinates.DistanceFromOrigin);
                var deployments = layout.Enumerate(HexCastleCellKind.Deployment).ToArray();
                expandedCount += deployments.Count(cell => cell.Coordinates.DistanceFromOrigin <= radius);
                Assert.That(deployments.All(cell => !cell.NoDeploy && !cell.InitialBlocked), Is.True);
                foreach (var cell in deployments)
                foreach (var direction in HexCoordinates.Directions)
                {
                    if (!layout.Cells.TryGetValue(cell.Coordinates + direction, out var adjacent)) continue;
                    Assert.That(adjacent.Kind, Is.Not.EqualTo(HexCastleCellKind.Ground),
                        $"{theme}/{layers}: 외부 열린 바닥 누락 {adjacent.Coordinates}");
                    Assert.That(adjacent.IsBuildingCell, Is.False, $"{theme}/{layers}: 성 외부 건물");
                }

                Assert.That(layout.Enumerate(HexCastleCellKind.Ground).Any(), Is.True);
                Assert.That(layout.Cells.Values.Where(cell => cell.IsWallPathCell || cell.IsBuildingCell)
                    .All(cell => cell.NoDeploy), Is.True);
                var plan = HexCastleSilhouettePlanner.Build(theme, 26639,
                    HexCastleFoundationGenerator.ResolveCanonicalWallRadii(layers));
                for (var band = 0; band < layers - 1; band++)
                foreach (var coordinate in HexCastleSilhouetteBandResolver.Resolve(plan,
                             layout.BattlefieldRadius, band).Cells)
                    Assert.That(layout.Cells[coordinate].NoDeploy, Is.True,
                        $"{theme}/{layers}: 성 안쪽 배치 허용 {coordinate}");
            }

            Assert.That(expandedCount, Is.GreaterThan(0), "굴곡 안쪽 반경의 성 외부 배치 칸이 추가되어야 한다.");
        }

        [Test]
        public void DeploymentAreaVisual_ShowsOnlyAvailableDeploymentHexCells()
        {
            var root = new GameObject("HexDeploymentAreaVisualTest");
            try
            {
                var first = CreateRuntimeCell(
                    new HexCastleCell(new HexCoordinates(7, 0), HexCastleCellKind.Deployment),
                    root.transform);
                var second = CreateRuntimeCell(
                    new HexCastleCell(new HexCoordinates(6, 1), HexCastleCellKind.Deployment),
                    root.transform);
                var ground = CreateRuntimeCell(
                    new HexCastleCell(new HexCoordinates(5, 0), HexCastleCellKind.Ground),
                    root.transform);
                var visual = root.AddComponent<HexCastleDeploymentAreaVisual>();

                visual.Configure(new[] { first, second, ground });

                Assert.That(visual.AllowedCellCount, Is.EqualTo(2));
                Assert.That(visual.IsVisible, Is.False);
                var renderer = visual.GetComponentInChildren<MeshRenderer>(true);
                var mesh = visual.GetComponentInChildren<MeshFilter>(true).sharedMesh;
                Assert.That(renderer, Is.Not.Null);
                Assert.That(mesh.subMeshCount, Is.EqualTo(2));
                Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(2));
                Assert.That(renderer.sharedMaterials[0].color.a, Is.LessThan(0.5f));

                visual.SetVisible(true);
                Assert.That(visual.IsVisible, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static HexCastleCellRuntime CreateRuntimeCell(HexCastleCell cell, Transform parent)
        {
            var cellObject = new GameObject($"Cell_{cell.Coordinates.Q}_{cell.Coordinates.R}");
            cellObject.transform.SetParent(parent, false);
            cellObject.transform.localPosition = HexSpatialContract.ToWorld(cell.Coordinates);
            var tile = new GameObject("TileVisualRoot").transform;
            tile.SetParent(cellObject.transform, false);
            var content = new GameObject("ContentVisualRoot").transform;
            content.SetParent(cellObject.transform, false);
            var runtime = cellObject.AddComponent<HexCastleCellRuntime>();
            runtime.Configure(cell, null, null, tile, content);
            return runtime;
        }

        private static Vector2 ResolveGroundCenter(Camera camera, float verticalScreenOffset)
        {
            var screenCenter = new Vector2(
                camera.pixelWidth * 0.5f,
                camera.pixelHeight * (0.5f + verticalScreenOffset));
            var ray = camera.ScreenPointToRay(screenCenter);
            var plane = new Plane(Vector3.up, Vector3.zero);
            Assert.That(plane.Raycast(ray, out var distance), Is.True);
            var point = ray.GetPoint(distance);
            return new Vector2(point.x, point.z);
        }
    }
}
