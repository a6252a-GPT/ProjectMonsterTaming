using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Pooling;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    [InitializeOnLoad]
    public static class HexCastleFoundationVisualGate // Cell 판정과 KayKit Scale 1 조립을 눈으로 승인받는 첫 Gate다
    {
        public const string RootName = "__HexCastleFoundationVisualGate";

        private const string RequiredSceneName = "DEV_CastleRaidHex";
        private const int PreviewSeed = 10801;
        private const float PalaceVisualScale = 2f;
        private const float BuildingVisualScale = 1.2f;
        private const float GoldEquipmentVisualScale = 1.5f;
        private const float TurretHeadFootprintRatio = 0.82f;
        private const float BallistaSeatInset = 0.025f;
        private const string KayKitRoot =
            "Assets/ThirdParty2/04_환경맵/KayKit - Medieval Hexagon Pack (for Unity)/Prefabs";
        private const string BlueBuildingPath = KayKitRoot + "/buildings/blue/";
        private const string TowerPath = BlueBuildingPath + "building_tower_A_blue.prefab";
        private const string PalacePath = BlueBuildingPath + "building_castle_blue.prefab";
        private const string DerivedPrefabRoot =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Art/Derived/KayKitDoubleSided/Prefabs/";
        private const string TurretHeadPrefabRoot =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Prefabs/TurretHeads/";
        private const string WallStubPath = DerivedPrefabRoot + "PF_CRHex_WallStub_DoubleSided.prefab";
        private const string ClosedGatePath = DerivedPrefabRoot + "PF_CRHex_Gate_Closed_DoubleSided.prefab";
        private const string OpenGatePath = DerivedPrefabRoot + "PF_CRHex_Gate_Open_DoubleSided.prefab";
        private const string KayKitMaterialPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/HexVariant/Art/Materials/MAT_CRHex_KayKitWall_Spring.mat";

        private static readonly IReadOnlyDictionary<HexCastleWallVisualKind, string> WallPaths =
            new Dictionary<HexCastleWallVisualKind, string>
            {
                {
                    HexCastleWallVisualKind.Straight,
                    DerivedPrefabRoot + "PF_CRHex_WallStraight_DoubleSided.prefab"
                },
                {
                    HexCastleWallVisualKind.CornerAOutside,
                    DerivedPrefabRoot + "PF_CRHex_WallCornerA_DoubleSided.prefab"
                },
                {
                    HexCastleWallVisualKind.CornerBOutside,
                    DerivedPrefabRoot + "PF_CRHex_WallCornerB_DoubleSided.prefab"
                }
            };

        private static int activeDefenseLayerCount = 3;
        private static int activeSeed = PreviewSeed;
        private static HexCastleTheme activeTheme = HexCastleTheme.CentralCompartment;

        static HexCastleFoundationVisualGate()
        {
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneClosing += OnSceneClosing;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            AssemblyReloadEvents.beforeAssemblyReload += ClearAll;
            EditorApplication.quitting += ClearAll;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("JC Tool/Castle Raid Hex/Foundation Gate/Theme 1/Create 2-Layer")]
        public static void CreateTwoLayer()
        {
            Create(2);
        }

        [MenuItem("JC Tool/Castle Raid Hex/Foundation Gate/Theme 1/Create 3-Layer")]
        public static void CreateThreeLayer()
        {
            Create(3);
        }

        [MenuItem("JC Tool/Castle Raid Hex/Foundation Gate/Theme 1/Create 4-Layer")]
        public static void CreateFourLayer()
        {
            Create(4);
        }

        public static void Create(int defenseLayerCount)
        {
            Create(
                PreviewSeed,
                defenseLayerCount,
                HexCastleThemeOneRulesAssetUtility.LoadOrCreate().Tuning);
        }

        public static void Create(int seed, int defenseLayerCount)
        {
            Create(
                seed,
                defenseLayerCount,
                HexCastleThemeOneRulesAssetUtility.LoadOrCreate().Tuning);
        }

        public static void Create(
            int seed,
            int defenseLayerCount,
            HexCastleThemeOneTuning tuning)
        {
            Create(seed, defenseLayerCount, HexCastleTheme.CentralCompartment, tuning, true);
        }

        public static void Create(
            int seed,
            int defenseLayerCount,
            HexCastleThemeOneTuning tuning,
            bool requireDedicatedScene)
        {
            Create(
                seed,
                defenseLayerCount,
                HexCastleTheme.CentralCompartment,
                tuning,
                requireDedicatedScene);
        }

        public static void Create(
            int seed,
            int defenseLayerCount,
            HexCastleTheme theme,
            HexCastleThemeOneTuning tuning,
            bool requireDedicatedScene = true)
        {
            if (defenseLayerCount < 2 || defenseLayerCount > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(defenseLayerCount), "방어선은 2~4중이어야 합니다.");
            }

            activeSeed = seed;
            activeDefenseLayerCount = defenseLayerCount;
            activeTheme = theme;
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || requireDedicatedScene && scene.name != RequiredSceneName)
            {
                throw new InvalidOperationException($"{RequiredSceneName} Scene에서만 Foundation Gate를 만들 수 있습니다.");
            }

            HexCastleGenerationPlayablePreview.Clear(scene);
            var wasDirty = scene.isDirty;
            Remove(scene);

            var camera = scene.GetRootGameObjects()
                .SelectMany(value => value.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault();
            if (camera == null)
            {
                throw new InvalidOperationException("DEV_CastleRaidHex Camera가 없습니다.");
            }

            var towerPrefab = LoadRequiredPrefab(TowerPath);
            var palacePrefab = LoadRequiredPrefab(PalacePath);
            var wallStubPrefab = LoadRequiredPrefab(WallStubPath);
            var gatePrefabs = new Dictionary<HexCastleGateRole, GameObject>
            {
                { HexCastleGateRole.ClosedWall, LoadRequiredPrefab(ClosedGatePath) },
                { HexCastleGateRole.OpenDefenderPassage, LoadRequiredPrefab(OpenGatePath) }
            };
            var kayKitMaterial = LoadRequiredMaterial(KayKitMaterialPath);
            var wallPrefabs = WallPaths.ToDictionary(pair => pair.Key, pair => LoadRequiredPrefab(pair.Value));
            ValidateVisualPrefab(towerPrefab);
            ValidateVisualPrefab(palacePrefab);
            ValidateVisualPrefab(wallStubPrefab);
            foreach (var gatePrefab in gatePrefabs.Values)
            {
                ValidateVisualPrefab(gatePrefab);
            }
            foreach (var wallPrefab in wallPrefabs.Values)
            {
                ValidateVisualPrefab(wallPrefab);
            }

            var layout = new HexCastleFoundationGenerator().Generate(
                seed,
                defenseLayerCount,
                theme,
                tuning);
            var buildingPrefabs = layout.Cells.Values
                .Where(cell => cell.IsBuildingCell)
                .Select(cell => cell.VisualVariantId)
                .Distinct(StringComparer.Ordinal)
                .ToDictionary(
                    visualId => visualId,
                    visualId => LoadRequiredPrefab(ResolveBuildingPrefabPath(visualId)),
                    StringComparer.Ordinal);
            foreach (var buildingPrefab in buildingPrefabs.Values)
            {
                ValidateVisualPrefab(buildingPrefab);
            }
            var turretHeadPrefabs = layout.Cells.Values
                .Where(cell => cell.BuildingRole == HexCastleBuildingRole.Turret)
                .Select(cell => ResolveTurretHeadPrefabPath(cell.TurretWeaponKind, cell.BuildingGrade))
                .Distinct(StringComparer.Ordinal)
                .ToDictionary(path => path, LoadRequiredPrefab, StringComparer.Ordinal);
            foreach (var turretHeadPrefab in turretHeadPrefabs.Values)
            {
                ValidateTurretHeadPrefab(turretHeadPrefab);
            }

            var wallTopology = HexCastleWallTopologyResolver.Build(layout);
            foreach (var pair in wallTopology)
            {
                var cell = layout.Cells[pair.Key];
                var invalidKind = cell.Kind == HexCastleCellKind.Wall && pair.Value.ConnectionCount != 2 ||
                                  cell.Kind != HexCastleCellKind.Wall &&
                                  cell.Kind != HexCastleCellKind.Tower &&
                                  cell.Kind != HexCastleCellKind.Gate;
                if (pair.Value.ConnectionCount < 2 || pair.Value.ConnectionCount > 4 ||
                    invalidKind)
                {
                    throw new InvalidOperationException($"{theme} 성벽 접속 판정이 잘못됐습니다: {pair.Key}");
                }
            }

            var root = new GameObject(RootName)
            {
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
            SceneManager.MoveGameObjectToScene(root, scene);
            var poolScope = root.AddComponent<ScenePoolScope>();
            var sfxPool = root.AddComponent<SfxPool>();
            sfxPool.EditorConfigure(12, 6);
            var turretCombatWorld = root.AddComponent<HexCastleTurretCombatWorld>();
            turretCombatWorld.Configure(
                poolScope,
                sfxPool,
                HexSpatialContract.CellOuterRadius,
                true);
            var turretAttackCatalog = HexCastleTurretAttackAssetUtility.LoadOrCreateCatalog();
            var state = root.AddComponent<HexCastleGenerationScenePreviewState>();
            var hiddenHost = FindSceneRoot(scene, "00_HexCastleRaidRoot");
            state.Capture(hiddenHost, camera);
            if (hiddenHost != null)
            {
                hiddenHost.SetActive(false);
            }

            var cellsRoot = CreateChild(
                $"00_Theme{HexCastleThemeCatalog.ResolveCode(theme)}_{defenseLayerCount}Layer_Cells",
                root.transform);
            var footprintMeshes = new Dictionary<float, Mesh>();
            var materials = HexCastlePreviewUtility.CreateMaterials();
            ConfigureBoardMaterial(materials["ground"]);
            CreateBoardSurface(root.transform, layout.Cells.Keys, materials["ground"]);
            foreach (var cell in layout.Cells.Values.OrderBy(value => value.Coordinates))
            {
                CreateCell(
                    cell,
                    cellsRoot,
                    towerPrefab,
                    palacePrefab,
                    wallStubPrefab,
                    wallPrefabs,
                    gatePrefabs,
                    buildingPrefabs,
                    turretHeadPrefabs,
                    cell.IsWallPathCell ? wallTopology[cell.Coordinates] : default,
                    materials,
                    kayKitMaterial,
                    footprintMeshes,
                    turretCombatWorld,
                    turretAttackCatalog);
            }

            turretCombatWorld.RebuildRegistry(root.transform);

            CreateGridOverlay(root.transform, layout.Cells.Keys, materials["grid"]);
            var monstersRoot = CreateChild("02_ActualMonsterScale", root.transform);
            var monsterZ = -(layout.BattlefieldRadius * HexSpatialContract.RowPitch + 3.2f);
            var monsterHeights = HexCastlePreviewUtility.CreateMonsterScaleRow(
                monstersRoot,
                materials,
                0f,
                monsterZ,
                2.05f);
            ConfigurePerspectiveCamera(camera, root);
            ConfigurePerspectiveSceneView(root);
            SceneView.RepaintAll();
            if (!wasDirty)
            {
                RestoreCleanState(scene);
            }

            Debug.Log(
                $"[Hex Foundation Gate] {HexCastleThemeCatalog.ResolveLabel(theme)} Seed {seed}, " +
                $"{defenseLayerCount}중벽 실루엣, " +
                $"Board R{layout.BattlefieldRadius}, " +
                $"Cell {layout.Cells.Count}, WallNetwork {wallTopology.Count}, " +
                $"Wall {layout.Enumerate(HexCastleCellKind.Wall).Count()}, " +
                $"Tower {layout.Enumerate(HexCastleCellKind.Tower).Count()}, " +
                $"Gate Closed/Open {layout.Enumerate(HexCastleCellKind.Gate).Count(value => value.GateRole == HexCastleGateRole.ClosedWall)}/" +
                $"{layout.Enumerate(HexCastleCellKind.Gate).Count(value => value.GateRole == HexCastleGateRole.OpenDefenderPassage)}, " +
                $"Partition {layout.Cells.Values.Count(value => value.WallRole == HexCastleWallRole.Partition)}, " +
                $"Building {layout.Cells.Values.Count(value => value.IsBuildingCell)}, " +
                $"Dense/Sparse {layout.Cells.Values.Count(value => value.PlacementDensity == HexCastlePlacementDensity.Dense)}/" +
                $"{layout.Cells.Values.Count(value => value.PlacementDensity == HexCastlePlacementDensity.Sparse)}, " +
                $"Monster Height {string.Join(", ", monsterHeights.Select(value => value.ToString("0.00") + "m"))}");
        }

        [MenuItem("JC Tool/Castle Raid Hex/Foundation Gate/Toggle Grid")]
        public static void ToggleGrid()
        {
            var root = FindSceneRoot(SceneManager.GetActiveScene(), RootName);
            var grid = root == null ? null : root.transform.Find("01_HexGridOverlay");
            if (grid == null)
            {
                return;
            }

            grid.gameObject.SetActive(!grid.gameObject.activeSelf);
            SceneView.RepaintAll();
        }

        [MenuItem("JC Tool/Castle Raid Hex/Foundation Gate/Remove")]
        public static void Remove()
        {
            Remove(SceneManager.GetActiveScene());
        }

        public static void Remove(Scene scene, bool restoreDirtyState = true)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            var wasDirty = scene.IsValid() && scene.isDirty;
            var roots = scene.GetRootGameObjects()
                .Where(value => value.name == RootName)
                .ToArray();
            foreach (var root in roots)
            {
                DestroyPreviewRoot(root);
            }

            if (restoreDirtyState && !wasDirty && roots.Length > 0)
            {
                RestoreCleanState(scene);
            }
        }

        public static void ClearAll()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                Remove(SceneManager.GetSceneAt(index));
            }
        }

        internal static void DestroyPreviewRoot(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            ClearPreviewSelection(root);
            root.GetComponent<HexCastleGenerationScenePreviewState>()?.Restore();
            var temporaryMeshes = root.GetComponentsInChildren<MeshFilter>(true)
                .Select(value => value.sharedMesh)
                .Concat(root.GetComponentsInChildren<MeshCollider>(true).Select(value => value.sharedMesh))
                .Where(value => value != null && !AssetDatabase.Contains(value))
                .Distinct()
                .ToArray();
            var temporaryMaterials = root.GetComponentsInChildren<Renderer>(true)
                .SelectMany(value => value.sharedMaterials)
                .Where(value => value != null && !AssetDatabase.Contains(value))
                .Distinct()
                .ToArray();
            Object.DestroyImmediate(root);
            foreach (var mesh in temporaryMeshes)
            {
                Object.DestroyImmediate(mesh);
            }

            foreach (var material in temporaryMaterials)
            {
                Object.DestroyImmediate(material);
            }
        }

        private static void ClearPreviewSelection(GameObject root)
        {
            var selectedObjects = Selection.objects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                return;
            }

            var retainedObjects = selectedObjects
                .Where(selected => !BelongsToPreviewRoot(selected, root))
                .ToArray();
            if (retainedObjects.Length != selectedObjects.Length)
            {
                Selection.objects = retainedObjects;
                ActiveEditorTracker.sharedTracker.ForceRebuild();
            }
        }

        private static bool BelongsToPreviewRoot(Object selected, GameObject root)
        {
            var selectedObject = selected as GameObject;
            if (selected is Component component)
            {
                selectedObject = component.gameObject;
            }

            return selectedObject != null &&
                   (selectedObject == root || selectedObject.transform.IsChildOf(root.transform));
        }

        public static object Inspect()
        {
            var scene = SceneManager.GetActiveScene();
            var root = FindSceneRoot(scene, RootName);
            if (root == null)
            {
                return new { exists = false };
            }

            var cells = root.GetComponentsInChildren<HexCastleCellRuntime>(true);
            var blocked = cells.Where(value => value.InitialBlocked).ToArray();
            var wallCells = cells.Where(value => value.Kind == HexCastleCellKind.Wall).ToArray();
            var towerCells = cells.Where(value => value.Kind == HexCastleCellKind.Tower).ToArray();
            var gateCells = cells.Where(value => value.Kind == HexCastleCellKind.Gate).ToArray();
            var palaceCells = cells.Where(value => value.Kind == HexCastleCellKind.Palace).ToArray();
            var buildingCells = cells.Where(value =>
                value.Kind == HexCastleCellKind.Building ||
                value.Kind == HexCastleCellKind.DefenseBuilding ||
                value.Kind == HexCastleCellKind.RewardBuilding).ToArray();
            var cellMap = cells.ToDictionary(value => value.Coordinates);
            var barracksCells = buildingCells.Where(value =>
                value.BuildingRole == HexCastleBuildingRole.KnightBarracks ||
                value.BuildingRole == HexCastleBuildingRole.FarmerBarracks).ToArray();
            var turretCells = buildingCells.Where(value =>
                value.BuildingRole == HexCastleBuildingRole.Turret).ToArray();
            var visualRoots = cells
                .SelectMany(value => new[] { value.TileVisualRoot, value.ContentVisualRoot })
                .Where(value => value != null)
                .ToArray();
            var visualGameplayComponentCount = visualRoots.Sum(value =>
                value.GetComponentsInChildren<HealthComponent>(true).Length +
                value.GetComponentsInChildren<Collider>(true).Length +
                value.GetComponentsInChildren<NavMeshObstacle>(true).Length +
                value.GetComponentsInChildren<HexCastleCellRuntime>(true).Length +
                value.GetComponentsInChildren<HexCastleTurretVisual>(true).Length +
                value.GetComponentsInChildren<HexCastleTurretRuntime>(true).Length);
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var bounds = HexCastlePreviewUtility.ResolveBounds(renderers);
            var camera = scene.GetRootGameObjects()
                .SelectMany(value => value.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault();
            var palaceVisuals = root.GetComponentsInChildren<Transform>(true)
                .Where(value => value.name == "PalaceVisual")
                .ToArray();
            return new
            {
                exists = true,
                cells = cells.Length,
                blockedCells = blocked.Length,
                blockedWithRootHealth = blocked.Count(value => value.Health != null && value.Health.transform == value.transform),
                blockedWithRootCollider = blocked.Count(value => value.FootprintCollider != null && value.FootprintCollider.transform == value.transform),
                blockedWithRootObstacle = blocked.Count(value => value.NavigationObstacle != null && value.NavigationObstacle.transform == value.transform),
                wallCells = wallCells.Length,
                towerCells = towerCells.Length,
                gateCells = gateCells.Length,
                closedGateCells = gateCells.Count(value => value.GateRole == HexCastleGateRole.ClosedWall),
                openDefenderGateCells = gateCells.Count(value =>
                    value.GateRole == HexCastleGateRole.OpenDefenderPassage &&
                    value.CanTraverse(HexCastleTraversalFaction.Defender) &&
                    !value.CanTraverse(HexCastleTraversalFaction.Assault)),
                palaceCells = palaceCells.Length,
                palaceFootprintRadius = palaceCells
                    .Select(value => value.Coordinates.DistanceFromOrigin)
                    .DefaultIfEmpty(-1)
                    .Max(),
                palaceVisualCount = palaceVisuals.Length,
                palaceVisualScale = palaceVisuals.Length == 1
                    ? palaceVisuals[0].localScale.x
                    : -1f,
                buildingCells = buildingCells.Length,
                buildingVisualCount = root.GetComponentsInChildren<Transform>(true)
                    .Count(value => value.name.StartsWith("BuildingVisual_", StringComparison.Ordinal)),
                buildingVisualScale = BuildingVisualScale,
                goldEquipmentVisualScale = GoldEquipmentVisualScale,
                goldEquipmentVisualCount = root.GetComponentsInChildren<Transform>(true)
                    .Count(value =>
                        value.name == $"BuildingVisual_{HexCastleBuildingRole.GoldStorage}" ||
                        value.name == $"BuildingVisual_{HexCastleBuildingRole.EquipmentForge}"),
                turretBaseVisualCount = root.GetComponentsInChildren<Transform>(true)
                    .Count(value => value.name == "TurretBaseVisual"),
                turretHeadVisualCount = root.GetComponentsInChildren<Transform>(true)
                    .Count(value => value.parent != null &&
                                    value.parent.name == "Joint_TurretHeadMount" &&
                                    value.name.StartsWith("Head_", StringComparison.Ordinal)),
                buildingRoleCounts = buildingCells
                    .GroupBy(value => value.BuildingRole.ToString())
                    .OrderBy(group => group.Key)
                    .ToDictionary(group => group.Key, group => group.Count()),
                innerBandTurretCount = turretCells.Count(value => value.DefenseLayer == 1),
                turretRangeCounts = turretCells
                    .GroupBy(value => value.TurretRangeCells)
                    .OrderBy(group => group.Key)
                    .ToDictionary(group => group.Key, group => group.Count()),
                turretsAcrossWallCount = turretCells.Count(value => value.TurretCanAttackAcrossWalls),
                disabledCannonBallistaLevel3Count = turretCells.Count(value =>
                    (value.TurretWeaponKind == HexCastleTurretWeaponKind.Cannon ||
                     value.TurretWeaponKind == HexCastleTurretWeaponKind.Ballista) &&
                    value.BuildingGrade >= 3),
                barracksMinimumDefenseLayer = barracksCells
                    .Select(value => value.DefenseLayer)
                    .DefaultIfEmpty(0)
                    .Min(),
                palaceGuardBarracksCount = barracksCells.Count(value =>
                    value.BuildingRole == HexCastleBuildingRole.KnightBarracks &&
                    value.DefenseLayer == 0 &&
                    value.Coordinates.DistanceFromOrigin ==
                    HexCastleFoundationGenerator.PalaceFootprintRadius + 1),
                palaceGuardTurretCount = turretCells.Count(value =>
                    value.DefenseLayer == 0 &&
                    value.Coordinates.DistanceFromOrigin ==
                    HexCastleFoundationGenerator.PalaceFootprintRadius + 1),
                outerBarracksMinimumDefenseLayer = barracksCells
                    .Where(value => value.DefenseLayer > 0)
                    .Select(value => value.DefenseLayer)
                    .DefaultIfEmpty(0)
                    .Min(),
                barracksMinimumOpenNeighborCount = barracksCells
                    .Select(value => HexCoordinates.Directions.Count(direction =>
                        cellMap.TryGetValue(value.Coordinates + direction, out var neighbor) &&
                        neighbor.Kind == HexCastleCellKind.Ground &&
                        !neighbor.InitialBlocked))
                    .DefaultIfEmpty(0)
                    .Min(),
                placementDensityCounts = buildingCells
                    .GroupBy(value => value.PlacementDensity.ToString())
                    .OrderBy(group => group.Key)
                    .ToDictionary(group => group.Key, group => group.Count()),
                buildingGradeSum = buildingCells.Sum(value => value.BuildingGrade),
                blockerGradeSum = buildingCells
                    .Where(value => value.BuildingRole == HexCastleBuildingRole.Blocker)
                    .Sum(value => value.BuildingGrade),
                validWallAssemblies = wallCells.Count(value =>
                    value.ContentVisualRoot != null &&
                    value.ContentVisualRoot.Cast<Transform>().Count(child => child.name == "WallVisual") == 1),
                validTowerHubAssemblies = towerCells.Count(value =>
                    value.ContentVisualRoot != null &&
                    value.ContentVisualRoot.Cast<Transform>().Count(child => child.name == "TowerOverlay") == 1 &&
                    value.ContentVisualRoot.Cast<Transform>().Count(child => child.name.StartsWith("WallStub_D", StringComparison.Ordinal)) ==
                        CountBits(value.WallConnectionMask)),
                validGateAssemblies = gateCells.Count(value =>
                    value.ContentVisualRoot != null &&
                    value.ContentVisualRoot.Cast<Transform>().Count(child =>
                        child.name == "GateVisual_Open" || child.name == "GateVisual_Closed") == 1),
                openGateApproachClearCount = gateCells.Count(value =>
                {
                    if (value.GateRole != HexCastleGateRole.OpenDefenderPassage)
                    {
                        return false;
                    }

                    var clear = 0;
                    for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
                    {
                        if ((value.GatePassageMask & 1 << direction) != 0 &&
                            cellMap.TryGetValue(value.Coordinates.Neighbor(direction), out var neighbor) &&
                            neighbor.Kind == HexCastleCellKind.Ground && !neighbor.InitialBlocked)
                        {
                            clear++;
                        }
                    }

                    return clear == 2;
                }),
                towerConnectionCounts = towerCells
                    .GroupBy(value => CountBits(value.WallConnectionMask))
                    .OrderBy(group => group.Key)
                    .ToDictionary(group => group.Key, group => group.Count()),
                cellTileRendererCount = cells.Sum(value =>
                    value.TileVisualRoot == null
                        ? 0
                        : value.TileVisualRoot.GetComponentsInChildren<Renderer>(true).Length),
                boardSurfaceRendererCount = root.transform.Find("00_BoardSurface")?
                    .GetComponentsInChildren<Renderer>(true).Length ?? 0,
                visualGameplayComponentCount,
                scenePoolScopeCount = root.GetComponentsInChildren<ScenePoolScope>(true).Length,
                sfxPoolCount = root.GetComponentsInChildren<SfxPool>(true).Length,
                turretCombatWorldCount = root.GetComponentsInChildren<HexCastleTurretCombatWorld>(true).Length,
                turretCombatRegisteredCellCount = root.GetComponent<HexCastleTurretCombatWorld>()?.RegisteredCellCount ?? 0,
                turretVisualCount = root.GetComponentsInChildren<HexCastleTurretVisual>(true).Length,
                turretRuntimeCount = root.GetComponentsInChildren<HexCastleTurretRuntime>(true).Length,
                validTurretRuntimeWiringCount = turretCells.Count(value =>
                {
                    var turretVisual = value.GetComponent<HexCastleTurretVisual>();
                    var turretRuntime = value.GetComponent<HexCastleTurretRuntime>();
                    return turretVisual != null && turretVisual.IsComplete &&
                           turretRuntime != null && turretRuntime.Structure == value &&
                           turretRuntime.Profile != null && turretRuntime.Profile.IsValid &&
                           turretRuntime.Profile.WeaponKind == value.TurretWeaponKind &&
                           turretRuntime.Profile.Level == value.BuildingGrade;
                }),
                ballistaReferenceBasisCount = root.GetComponentsInChildren<HexCastleTurretVisual>(true)
                    .Count(value => value.WeaponKind == HexCastleTurretWeaponKind.Ballista &&
                                    value.HeadRoot != null &&
                                    ApproximatelyEuler(
                                        value.HeadRoot.Find("Joint_BodyMount/YawPivot/PitchPivot/Model")?.localEulerAngles,
                                        new Vector3(0f, 90f, 0f))),
                scaleViolations = root.GetComponentsInChildren<Transform>(true)
                    .Count(value =>
                        value.name == "PalaceVisual"
                            ? value.localScale != Vector3.one * PalaceVisualScale
                            : value.name.StartsWith("BuildingVisual_", StringComparison.Ordinal)
                                ? value.localScale != Vector3.one * ResolveBuildingVisualScale(value.name)
                                : (value.name == "TileVisual" || value.name == "WallVisual" ||
                                   value.name.StartsWith("GateVisual_", StringComparison.Ordinal) ||
                                   value.name.StartsWith("WallStub_D", StringComparison.Ordinal) ||
                                   value.name == "TowerOverlay") &&
                                  value.localScale != Vector3.one),
                gridVisible = root.transform.Find("01_HexGridOverlay")?.gameObject.activeSelf ?? false,
                theme = activeTheme.ToString(),
                seed = activeSeed,
                defenseLayerCount = activeDefenseLayerCount,
                cameraProjection = camera != null && !camera.orthographic ? "Perspective" : "Orthographic",
                boundsSize = bounds.size,
                sceneDirty = root.scene.isDirty
            };
        }

        private static bool ShouldUseTower(HexCastleWallCellTopology topology)
        {
            if (topology.IsJunction || topology.ConnectionCount != 2)
            {
                return true;
            }

            var directions = topology.GetDirections();
            try
            {
                HexCastleWallVisualResolver.ResolveDirections(
                    HexCastleCellKind.Wall,
                    directions[0],
                    directions[1]);
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        private static int CountBits(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            var count = 0;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }

            return count;
        }

        private static bool ApproximatelyEuler(Vector3? actual, Vector3 expected)
        {
            return actual.HasValue &&
                   Mathf.Abs(Mathf.DeltaAngle(actual.Value.x, expected.x)) <= 0.01f &&
                   Mathf.Abs(Mathf.DeltaAngle(actual.Value.y, expected.y)) <= 0.01f &&
                   Mathf.Abs(Mathf.DeltaAngle(actual.Value.z, expected.z)) <= 0.01f;
        }

        private static float ResolveBuildingVisualScale(HexCastleBuildingRole role)
        {
            return role == HexCastleBuildingRole.GoldStorage ||
                   role == HexCastleBuildingRole.EquipmentForge
                ? GoldEquipmentVisualScale
                : BuildingVisualScale;
        }

        private static float ResolveBuildingVisualScale(string visualRootName)
        {
            return string.Equals(
                       visualRootName,
                       $"BuildingVisual_{HexCastleBuildingRole.GoldStorage}",
                       StringComparison.Ordinal) ||
                   string.Equals(
                       visualRootName,
                       $"BuildingVisual_{HexCastleBuildingRole.EquipmentForge}",
                       StringComparison.Ordinal)
                ? GoldEquipmentVisualScale
                : BuildingVisualScale;
        }

        private static void CreateCell(
            HexCastleCell cell,
            Transform cellsParent,
            GameObject towerPrefab,
            GameObject palacePrefab,
            GameObject wallStubPrefab,
            IReadOnlyDictionary<HexCastleWallVisualKind, GameObject> wallPrefabs,
            IReadOnlyDictionary<HexCastleGateRole, GameObject> gatePrefabs,
            IReadOnlyDictionary<string, GameObject> buildingPrefabs,
            IReadOnlyDictionary<string, GameObject> turretHeadPrefabs,
            HexCastleWallCellTopology wallTopology,
            IReadOnlyDictionary<string, Material> materials,
            Material kayKitMaterial,
            IDictionary<float, Mesh> footprintMeshes,
            HexCastleTurretCombatWorld turretCombatWorld,
            HexCastleTurretAttackCatalog turretAttackCatalog)
        {
            var cellRoot = CreateChild($"Cell_{cell.Coordinates.Q}_{cell.Coordinates.R}__{cell.Kind}", cellsParent);
            cellRoot.localPosition = HexSpatialContract.ToWorld(cell.Coordinates);
            var tileVisualRoot = CreateChild("TileVisualRoot", cellRoot);
            Transform turretHead = null;

            var contentVisualRoot = CreateChild(
                "ContentVisualRoot",
                cellRoot);
            if (cell.Kind == HexCastleCellKind.Wall)
            {
                var directions = wallTopology.GetDirections();
                if (directions.Length != 2)
                {
                    throw new InvalidOperationException($"일반 성벽 Cell {cell.Coordinates}은 2방향이어야 합니다.");
                }

                var resolution = HexCastleWallVisualResolver.ResolveDirections(
                    HexCastleCellKind.Wall,
                    directions[0],
                    directions[1]);
                var wall = InstantiateVisual(
                    wallPrefabs[resolution.VisualKind],
                    contentVisualRoot,
                    "WallVisual");
                wall.transform.localRotation = Quaternion.Euler(0f, resolution.RotationDegrees, 0f);
            }
            else if (cell.Kind == HexCastleCellKind.Tower)
            {
                foreach (var direction in wallTopology.GetDirections())
                {
                    var stub = InstantiateVisual(
                        wallStubPrefab,
                        contentVisualRoot,
                        $"WallStub_D{direction}");
                    stub.transform.localRotation = Quaternion.Euler(0f, direction * 60f, 0f);
                }

                InstantiateVisual(towerPrefab, contentVisualRoot, "TowerOverlay", kayKitMaterial);
            }
            else if (cell.Kind == HexCastleCellKind.Gate)
            {
                var directions = wallTopology.GetDirections();
                if (directions.Length != 2 || wallTopology.ResolveTwoWaySeparation() != 3 ||
                    !gatePrefabs.TryGetValue(cell.GateRole, out var gatePrefab))
                {
                    throw new InvalidOperationException($"성문 Cell {cell.Coordinates}의 직선 연결·Visual 역할이 잘못됐습니다.");
                }

                var resolution = HexCastleWallVisualResolver.ResolveDirections(
                    HexCastleCellKind.Gate,
                    directions[0],
                    directions[1]);
                var gate = InstantiateVisual(
                    gatePrefab,
                    contentVisualRoot,
                    cell.GateRole == HexCastleGateRole.OpenDefenderPassage
                        ? "GateVisual_Open"
                        : "GateVisual_Closed");
                gate.transform.localRotation = Quaternion.Euler(0f, resolution.RotationDegrees, 0f);
            }
            else if (cell.Kind == HexCastleCellKind.Palace &&
                     cell.Coordinates == new HexCoordinates(0, 0))
            {
                var palace = InstantiateVisual(
                    palacePrefab,
                    contentVisualRoot,
                    "PalaceVisual",
                    kayKitMaterial);
                palace.transform.localScale = Vector3.one * PalaceVisualScale;
            }
            else if (cell.IsBuildingCell)
            {
                if (!buildingPrefabs.TryGetValue(cell.VisualVariantId, out var buildingPrefab))
                {
                    throw new InvalidOperationException(
                        $"건물 Cell {cell.Coordinates}의 Visual Prefab이 없습니다: {cell.VisualVariantId}");
                }

                var building = cell.BuildingRole == HexCastleBuildingRole.Turret
                    ? CreateTurretBuildingVisual(
                        cell,
                        contentVisualRoot,
                        buildingPrefab,
                        turretHeadPrefabs,
                        kayKitMaterial)
                    : InstantiateVisual(
                        buildingPrefab,
                        contentVisualRoot,
                        $"BuildingVisual_{cell.BuildingRole}",
                        kayKitMaterial);
                if (cell.BuildingRole == HexCastleBuildingRole.Turret)
                {
                    var headMount = building.transform.Find("Joint_TurretHeadMount");
                    turretHead = headMount != null && headMount.childCount == 1
                        ? headMount.GetChild(0)
                        : throw new InvalidOperationException(
                            $"포탑 Cell {cell.Coordinates}의 Head 조립 계약이 잘못됐습니다.");
                }

                building.transform.localRotation = Quaternion.Euler(
                    0f,
                    Mathf.Max(0, cell.RegionId - 1) * 60f,
                    0f);
                building.transform.localScale = Vector3.one * ResolveBuildingVisualScale(cell.BuildingRole);
            }

            HealthComponent health = null;
            Collider footprintCollider = null;
            NavMeshObstacle obstacle = null;
            if (cell.InitialBlocked)
            {
                health = cellRoot.gameObject.AddComponent<HealthComponent>();
                var height = ResolveFootprintHeight(cell.Kind);
                var meshCollider = cellRoot.gameObject.AddComponent<MeshCollider>();
                if (!footprintMeshes.TryGetValue(height, out var footprintMesh))
                {
                    footprintMesh = CreateHexPrismMesh(height);
                    footprintMeshes.Add(height, footprintMesh);
                }

                meshCollider.sharedMesh = footprintMesh;
                meshCollider.convex = true;
                footprintCollider = meshCollider;
                obstacle = cellRoot.gameObject.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Capsule;
                obstacle.center = Vector3.up * height * 0.5f;
                obstacle.radius = HexSpatialContract.CellInRadius * 0.92f;
                obstacle.height = height;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
            }

            var runtime = cellRoot.gameObject.AddComponent<HexCastleCellRuntime>();
            runtime.Configure(
                cell,
                health,
                footprintCollider,
                obstacle,
                tileVisualRoot,
                contentVisualRoot);

            if (cell.BuildingRole == HexCastleBuildingRole.Turret)
            {
                var attackProfile = turretAttackCatalog.Resolve(
                    cell.TurretWeaponKind,
                    cell.BuildingGrade);
                if (attackProfile == null)
                {
                    throw new InvalidOperationException(
                        $"포탑 Cell {cell.Coordinates}의 Hex 독립 공격 Profile이 없습니다.");
                }

                var turretVisual = cellRoot.gameObject.AddComponent<HexCastleTurretVisual>();
                turretVisual.Configure(cell.TurretWeaponKind, cell.BuildingGrade, turretHead);
                var turretRuntime = cellRoot.gameObject.AddComponent<HexCastleTurretRuntime>();
                turretRuntime.Configure(turretCombatWorld, runtime, turretVisual, attackProfile);
            }
        }

        private static GameObject CreateTurretBuildingVisual(
            HexCastleCell cell,
            Transform parent,
            GameObject towerBasePrefab,
            IReadOnlyDictionary<string, GameObject> turretHeadPrefabs,
            Material kayKitMaterial)
        {
            var assembly = CreateChild($"BuildingVisual_{cell.BuildingRole}", parent).gameObject;
            var towerBase = InstantiateVisual(
                towerBasePrefab,
                assembly.transform,
                "TurretBaseVisual",
                kayKitMaterial);
            var baseRenderers = towerBase.GetComponentsInChildren<Renderer>(true);
            if (baseRenderers.Length == 0)
            {
                throw new InvalidOperationException("KayKit 빈 포탑 받침에 Renderer가 없습니다.");
            }

            var baseBounds = HexCastlePreviewUtility.ResolveBounds(baseRenderers);
            var headPath = ResolveTurretHeadPrefabPath(cell.TurretWeaponKind, cell.BuildingGrade);
            if (!turretHeadPrefabs.TryGetValue(headPath, out var headPrefab))
            {
                throw new InvalidOperationException(
                    $"포탑 Cell {cell.Coordinates}의 기존 헤드 Prefab이 없습니다: {headPath}");
            }

            var headMount = CreateChild("Joint_TurretHeadMount", assembly.transform);
            headMount.localPosition = Vector3.up * (baseBounds.max.y - assembly.transform.position.y);
            var head = InstantiateVisual(
                headPrefab,
                headMount,
                $"Head_{cell.TurretWeaponKind}_Lv{cell.BuildingGrade}");
            FitTurretHead(
                head.transform,
                Mathf.Min(baseBounds.size.x, baseBounds.size.z));
            SeatBallistaHead(cell.TurretWeaponKind, head.transform, baseBounds.max.y);
            return assembly;
        }

        private static void FitTurretHead(Transform head, float minimumFootprint)
        {
            var model = head.Find("Joint_BodyMount/YawPivot/PitchPivot/Model");
            var renderers = model == null
                ? Array.Empty<Renderer>()
                : model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException($"포탑 헤드 {head.name}에서 Model Renderer를 찾지 못했습니다.");
            }

            var bounds = HexCastlePreviewUtility.ResolveBounds(renderers);
            var currentFootprint = Mathf.Max(bounds.size.x, bounds.size.z);
            if (currentFootprint <= 0.001f)
            {
                throw new InvalidOperationException($"포탑 헤드 {head.name}의 Renderer Bounds가 비었습니다.");
            }

            var targetFootprint = Mathf.Clamp(
                minimumFootprint * TurretHeadFootprintRatio,
                0.82f,
                1.55f);
            var scale = Mathf.Clamp(targetFootprint / currentFootprint, 0.65f, 1.35f);
            head.localScale = Vector3.one * scale;
        }

        private static void SeatBallistaHead(
            HexCastleTurretWeaponKind weaponKind,
            Transform head,
            float baseTopWorldY)
        {
            if (weaponKind != HexCastleTurretWeaponKind.Ballista)
            {
                return;
            }

            var model = head.Find("Joint_BodyMount/YawPivot/PitchPivot/Model");
            var renderers = model == null
                ? Array.Empty<Renderer>()
                : model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException($"발리스타 헤드 {head.name}의 Model Renderer가 없습니다.");
            }

            var bounds = HexCastlePreviewUtility.ResolveBounds(renderers);
            var targetBottom = baseTopWorldY - BallistaSeatInset;
            head.position += Vector3.up * (targetBottom - bounds.min.y);
        }

        private static void ConfigureBoardMaterial(Material material)
        {
            var color = new Color(0.32f, 0.39f, 0.24f, 1f);
            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.04f);
            }
        }

        private static void CreateBoardSurface(
            Transform parent,
            IEnumerable<HexCoordinates> coordinates,
            Material material)
        {
            var ordered = coordinates.OrderBy(value => value).ToArray();
            var vertices = new List<Vector3>(ordered.Length * 7);
            var uv = new List<Vector2>(ordered.Length * 7);
            var triangles = new List<int>(ordered.Length * 18);
            foreach (var coordinate in ordered)
            {
                var center = HexSpatialContract.ToWorld(coordinate) + Vector3.down * 0.002f;
                var corners = HexSpatialContract.GetWorldCorners(coordinate);
                var start = vertices.Count;
                vertices.Add(center);
                uv.Add(new Vector2(center.x, center.z) * 0.1f);
                for (var cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    var corner = corners[cornerIndex] + Vector3.down * 0.002f;
                    vertices.Add(corner);
                    uv.Add(new Vector2(corner.x, corner.z) * 0.1f);
                }

                for (var cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    var next = (cornerIndex + 1) % corners.Length;
                    triangles.Add(start);
                    triangles.Add(start + 1 + next);
                    triangles.Add(start + 1 + cornerIndex);
                }
            }

            var mesh = new Mesh
            {
                name = "MESH_HexTheme1_SingleBoardSurface",
                hideFlags = HideFlags.HideAndDontSave
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var board = CreateChild("00_BoardSurface", parent);
            board.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = board.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.enabled = false; // DEV 배경 지형을 가리지 않도록 미리보기 바닥도 숨긴다
        }

        private static void CreateGridOverlay(
            Transform parent,
            IEnumerable<HexCoordinates> coordinates,
            Material material)
        {
            var edges = new HashSet<HexEdgeKey>();
            foreach (var cell in coordinates)
            {
                for (var direction = 0; direction < HexCoordinates.Directions.Length; direction++)
                {
                    edges.Add(HexEdgeKey.FromCellSide(cell, direction));
                }
            }

            var vertices = new List<Vector3>(edges.Count * 2);
            foreach (var edge in edges.OrderBy(value => value))
            {
                vertices.Add(edge.Start.ToWorld() + Vector3.up * 0.018f);
                vertices.Add(edge.End.ToWorld() + Vector3.up * 0.018f);
            }

            var mesh = new Mesh
            {
                name = "MESH_HexFoundationGate_Grid",
                hideFlags = HideFlags.HideAndDontSave
            };
            mesh.SetVertices(vertices);
            mesh.SetIndices(Enumerable.Range(0, vertices.Count).ToArray(), MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            var grid = CreateChild("01_HexGridOverlay", parent);
            grid.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            grid.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Mesh CreateHexPrismMesh(float height, float radiusScale = 0.96f)
        {
            var vertices = new Vector3[14];
            vertices[0] = Vector3.zero;
            vertices[1] = Vector3.up * height;
            for (var index = 0; index < 6; index++)
            {
                var angle = Mathf.Deg2Rad * (30f + index * 60f);
                var x = Mathf.Cos(angle) * HexSpatialContract.CellOuterRadius * radiusScale;
                var z = Mathf.Sin(angle) * HexSpatialContract.CellOuterRadius * radiusScale;
                vertices[2 + index] = new Vector3(x, 0f, z);
                vertices[8 + index] = new Vector3(x, height, z);
            }

            var triangles = new List<int>(72);
            for (var index = 0; index < 6; index++)
            {
                var next = (index + 1) % 6;
                var bottom = 2 + index;
                var nextBottom = 2 + next;
                var top = 8 + index;
                var nextTop = 8 + next;

                triangles.Add(0);
                triangles.Add(bottom);
                triangles.Add(nextBottom);
                triangles.Add(1);
                triangles.Add(nextTop);
                triangles.Add(top);
                triangles.Add(bottom);
                triangles.Add(top);
                triangles.Add(nextTop);
                triangles.Add(bottom);
                triangles.Add(nextTop);
                triangles.Add(nextBottom);
            }

            var mesh = new Mesh
            {
                name = $"MESH_HexPrism_{height:0.00}_{radiusScale:0.00}",
                hideFlags = HideFlags.HideAndDontSave
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float ResolveFootprintHeight(HexCastleCellKind kind)
        {
            switch (kind)
            {
                case HexCastleCellKind.Palace:
                    return 4f;
                case HexCastleCellKind.Tower:
                    return 1.5f;
                default:
                    return 1.1f;
            }
        }

        private static void ConfigurePerspectiveCamera(Camera camera, GameObject root)
        {
            var bounds = ResolveCastlePreviewBounds(root);
            camera.orthographic = false;
            camera.fieldOfView = 32f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 300f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.17f, 0.20f, 0.22f, 1f);
            var controller = camera.GetComponent<HexCastleCameraController>();
            if (controller != null)
            {
                controller.ConfigureBounds(bounds);
                return;
            }

            const float tilt = 38f;
            camera.transform.rotation = Quaternion.Euler(tilt, 0f, 0f);
            var focus = new Vector2(bounds.center.x, bounds.center.z);
            var fitDistance = HexCastleCameraController.ResolveFitDistance(
                camera,
                bounds,
                focus,
                0f,
                tilt,
                1.08f);
            camera.transform.position =
                new Vector3(focus.x, 0f, focus.y) - camera.transform.forward * fitDistance;
        }

        private static void ConfigurePerspectiveSceneView(GameObject root)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                return;
            }

            var bounds = ResolveCastlePreviewBounds(root);
            var forward = new Vector3(0f, -0.615f, 0.7885f).normalized;
            var rotation = Quaternion.LookRotation(forward, Vector3.up);
            var size = Mathf.Max(bounds.size.x, bounds.size.z) * 0.62f;
            sceneView.LookAt(bounds.center, rotation, size, false, true);
        }

        private static Bounds ResolveCastlePreviewBounds(GameObject root)
        {
            var comparisonRoot = root.transform.Find("02_ActualMonsterScale");
            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(value => comparisonRoot == null || !value.transform.IsChildOf(comparisonRoot))
                .ToArray();
            return HexCastlePreviewUtility.ResolveBounds(renderers);
        }

        private static GameObject InstantiateVisual(
            GameObject prefab,
            Transform parent,
            string name,
            Material materialOverride = null)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException($"KayKit Prefab 생성에 실패했습니다: {prefab.name}");
            }

            instance.name = name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            if (materialOverride != null)
            {
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials = Enumerable
                        .Repeat(materialOverride, renderer.sharedMaterials.Length)
                        .ToArray();
                }
            }

            SetDontSaveRecursively(instance);
            return instance;
        }

        private static Transform CreateChild(string name, Transform parent)
        {
            var child = new GameObject(name)
            {
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static GameObject FindSceneRoot(Scene scene, string name)
        {
            return scene.IsValid() && scene.isLoaded
                ? scene.GetRootGameObjects().FirstOrDefault(value => value.name == name)
                : null;
        }

        private static GameObject LoadRequiredPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab != null
                ? prefab
                : throw new InvalidOperationException($"KayKit Prefab이 없습니다: {path}");
        }

        private static string ResolveBuildingPrefabPath(string visualVariantId)
        {
            if (string.IsNullOrWhiteSpace(visualVariantId))
            {
                throw new ArgumentException("건물 VisualVariantId가 비었습니다.", nameof(visualVariantId));
            }

            var colorFolder = visualVariantId.EndsWith("_yellow", StringComparison.Ordinal)
                ? "yellow"
                : visualVariantId.EndsWith("_green", StringComparison.Ordinal)
                    ? "green"
                    : visualVariantId.EndsWith("_red", StringComparison.Ordinal)
                        ? "red"
                        : visualVariantId.EndsWith("_blue", StringComparison.Ordinal)
                            ? "blue"
                            : "neutral";
            return $"{KayKitRoot}/buildings/{colorFolder}/{visualVariantId}.prefab";
        }

        private static string ResolveTurretHeadPrefabPath(
            HexCastleTurretWeaponKind weaponKind,
            int level)
        {
            string family;
            switch (weaponKind)
            {
                case HexCastleTurretWeaponKind.Cannon:
                    family = "Cannon";
                    break;
                case HexCastleTurretWeaponKind.Ballista:
                    family = "Ballista";
                    break;
                case HexCastleTurretWeaponKind.Fireball:
                    family = "Fireball";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(weaponKind),
                        weaponKind,
                        "포탑 Cell에 유효한 무기 종류가 없습니다.");
            }

            return $"{TurretHeadPrefabRoot}PF_CR_TurretHead_{family}_Lv{Mathf.Clamp(level, 1, 3)}.prefab";
        }

        private static Material LoadRequiredMaterial(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            return material != null
                ? material
                : throw new InvalidOperationException($"Hex 전용 KayKit Material이 없습니다: {path}");
        }

        private static void ValidateVisualPrefab(GameObject prefab)
        {
            if (prefab.transform.localScale != Vector3.one ||
                prefab.GetComponentInChildren<HealthComponent>(true) != null ||
                prefab.GetComponentInChildren<Collider>(true) != null ||
                prefab.GetComponentInChildren<NavMeshObstacle>(true) != null ||
                prefab.GetComponentInChildren<HexCastleCellRuntime>(true) != null)
            {
                throw new InvalidOperationException($"{prefab.name}은 Scale 1 순수 Visual Prefab이 아닙니다.");
            }

            if (prefab.GetComponentsInChildren<MeshFilter>(true).Any(value => value.sharedMesh == null) ||
                prefab.GetComponentsInChildren<Renderer>(true).Any(value =>
                    value.sharedMaterials.Any(material => material == null)))
            {
                throw new InvalidOperationException($"{prefab.name}에 Missing Mesh 또는 Material이 있습니다.");
            }
        }

        private static void ValidateTurretHeadPrefab(GameObject prefab)
        {
            var pitch = prefab.transform.Find("Joint_BodyMount/YawPivot/PitchPivot");
            var model = pitch?.Find("Model");
            var muzzle = pitch?.Find("Muzzle");
            var muzzleVfx = muzzle?.Find("VFX_Muzzle");
            var loadedProjectiles = pitch?.Find("LoadedProjectiles");
            if (prefab.transform.localScale != Vector3.one ||
                prefab.GetComponentInChildren<HealthComponent>(true) != null ||
                prefab.GetComponentInChildren<Collider>(true) != null ||
                prefab.GetComponentInChildren<NavMeshObstacle>(true) != null ||
                prefab.GetComponentInChildren<HexCastleCellRuntime>(true) != null ||
                model == null || muzzle == null || muzzleVfx == null || loadedProjectiles == null ||
                model.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                throw new InvalidOperationException(
                    $"{prefab.name}은 기존 포탑 헤드의 순수 Visual·Joint 계약을 만족하지 않습니다.");
            }

            if (model.GetComponentsInChildren<MeshFilter>(true).Any(value => value.sharedMesh == null) ||
                model.GetComponentsInChildren<Renderer>(true).Any(value =>
                    value.sharedMaterials.Any(material => material == null)))
            {
                throw new InvalidOperationException($"{prefab.name}의 Model에 Missing Mesh 또는 Material이 있습니다.");
            }

            if (prefab.name.IndexOf("Ballista", StringComparison.Ordinal) >= 0 &&
                loadedProjectiles.childCount == 0)
            {
                throw new InvalidOperationException($"{prefab.name}에 장전 화살 Visual이 없습니다.");
            }

            if (prefab.name.IndexOf("Ballista", StringComparison.Ordinal) < 0 &&
                muzzleVfx.GetComponentsInChildren<ParticleSystem>(true).Length == 0)
            {
                throw new InvalidOperationException($"{prefab.name}에 총구 VFX가 없습니다.");
            }
        }

        private static void SetDontSaveRecursively(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }
        }

        private static void OnSceneSaving(Scene scene, string path)
        {
            Remove(scene, false);
        }

        private static void OnSceneClosing(Scene scene, bool removingScene)
        {
            Remove(scene);
        }

        private static void OnActiveSceneChanged(Scene previous, Scene next)
        {
            Remove(previous);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                ClearAll();
            }
        }

        private static void RestoreCleanState(Scene scene)
        {
            var method = typeof(EditorSceneManager).GetMethod(
                "ClearSceneDirtiness",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            method?.Invoke(null, new object[] { scene });
        }
    }
}
