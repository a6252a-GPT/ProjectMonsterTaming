using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace ProjectMT.Contents.CastleRaidHex
{
    [DisallowMultipleComponent]
    public sealed class HexCastleProceduralStage : MonoBehaviour // 저장 자산 없이 현재 난이도의 Cell 성을 소유한다
    {
        [SerializeField, Range(1, 10)] private int difficultyLevel = 1;
        [SerializeField] private int seed;
        [SerializeField] private HexCastleTheme theme;
        [SerializeField, Range(2, 4)] private int defenseLayerCount = 2;
        [SerializeField] private Bounds worldBounds;

        [NonSerialized] private readonly List<Object> generatedAssets = new List<Object>();

        public int DifficultyLevel => difficultyLevel;
        public int Seed => seed;
        public HexCastleTheme Theme => theme;
        public int DefenseLayerCount => defenseLayerCount;
        public Bounds WorldBounds => worldBounds;
        public bool IsComplete => GetComponentsInChildren<HexCastleCellRuntime>(true).Length > 0 &&
                                  GetComponent<HexCastleTurretCombatWorld>() != null &&
                                  GetComponent<HexCastleTrapWorld>() != null;

        internal void Configure(HexCastleLayout layout, Bounds bounds, IEnumerable<Object> assets)
        {
            difficultyLevel = Mathf.Clamp(layout?.DifficultyLevel ?? 1, 1, 10);
            seed = layout?.Seed ?? 0;
            theme = layout?.Theme ?? HexCastleTheme.CentralCompartment;
            defenseLayerCount = Mathf.Clamp(layout?.DefenseLayerCount ?? 2, 2, 4);
            worldBounds = bounds;
            generatedAssets.Clear();
            if (assets != null)
            {
                generatedAssets.AddRange(assets.Where(value => value != null).Distinct());
            }
        }

        private void OnDestroy()
        {
            foreach (var asset in generatedAssets)
            {
                if (asset == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(asset);
                }
                else
                {
                    DestroyImmediate(asset);
                }
            }

            generatedAssets.Clear();
        }
    }

    public static class HexCastleProceduralStageBuilder
    {
        private const float PalaceVisualScale = 2f;
        private const float BuildingVisualScale = 1.2f;
        private const float GoldEquipmentVisualScale = 1.5f;
        private const float TurretHeadFootprintRatio = 0.82f;
        private const float BallistaSeatInset = 0.025f;

        public static HexCastleProceduralStage Build(
            HexCastleLayout layout,
            HexCastleVisualSet visualSet,
            HexCastleTurretAttackCatalog turretAttackCatalog,
            Transform parent)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (visualSet == null || !visualSet.IsRuntimeComplete)
            {
                throw new ArgumentException("Hex 절차 생성 Visual Set이 불완전합니다.", nameof(visualSet));
            }

            if (turretAttackCatalog == null)
            {
                throw new ArgumentNullException(nameof(turretAttackCatalog));
            }

            var root = new GameObject(
                $"Runtime_HEX_T{HexCastleThemeCatalog.ResolveCode(layout.Theme)}_D{layout.DifficultyLevel:00}_" +
                $"W{layout.DefenseLayerCount}_{layout.Seed}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            var stage = root.AddComponent<HexCastleProceduralStage>();
            var combatWorld = root.AddComponent<HexCastleTurretCombatWorld>();
            var trapWorld = root.AddComponent<HexCastleTrapWorld>();
            var cellsRoot = CreateChild("00_Cells", root.transform);
            var trapsRoot = CreateChild("01_Traps", root.transform);
            var generatedAssets = new List<Object>();
            var boardShader = Shader.Find("Universal Render Pipeline/Lit") ?? visualSet.KayKitMaterial.shader;
            var boardMaterial = new Material(boardShader)
            {
                name = "MAT_CRHex_ProceduralGround_Runtime",
                color = new Color(0.48f, 0.68f, 0.30f, 1f)
            };
            if (boardMaterial.HasProperty("_BaseMap"))
            {
                boardMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);
            }
            if (boardMaterial.HasProperty("_BaseColor"))
            {
                boardMaterial.SetColor("_BaseColor", boardMaterial.color);
            }
            if (boardMaterial.HasProperty("_Metallic"))
            {
                boardMaterial.SetFloat("_Metallic", 0f);
            }
            if (boardMaterial.HasProperty("_Smoothness"))
            {
                boardMaterial.SetFloat("_Smoothness", 0.02f);
            }
            generatedAssets.Add(boardMaterial);
            generatedAssets.Add(CreateBoardSurface(root.transform, layout.Cells.Keys, boardMaterial));

            var topology = HexCastleWallTopologyResolver.Build(layout);
            var footprintMeshes = new Dictionary<float, Mesh>();
            foreach (var cell in layout.Cells.Values.OrderBy(value => value.Coordinates))
            {
                CreateCell(
                    cell,
                    cellsRoot,
                    visualSet,
                    cell.IsWallPathCell ? topology[cell.Coordinates] : default,
                    footprintMeshes,
                    combatWorld,
                    turretAttackCatalog);
            }

            generatedAssets.AddRange(footprintMeshes.Values);
            trapWorld.Configure(
                layout,
                trapsRoot,
                visualSet,
                generatedAssets);
            var bounds = ResolveBounds(root.GetComponentsInChildren<Renderer>(true));
            stage.Configure(layout, bounds, generatedAssets);
            return stage;
        }

        private static void CreateCell(
            HexCastleCell cell,
            Transform cellsParent,
            HexCastleVisualSet visualSet,
            HexCastleWallCellTopology topology,
            IDictionary<float, Mesh> footprintMeshes,
            HexCastleTurretCombatWorld combatWorld,
            HexCastleTurretAttackCatalog turretAttackCatalog)
        {
            var cellRoot = CreateChild($"Cell_{cell.Coordinates.Q}_{cell.Coordinates.R}__{cell.Kind}", cellsParent);
            cellRoot.localPosition = HexSpatialContract.ToWorld(cell.Coordinates);
            var tileVisualRoot = CreateChild("TileVisualRoot", cellRoot);
            var contentVisualRoot = CreateChild("ContentVisualRoot", cellRoot);
            Transform turretHead = null;

            if (cell.Kind == HexCastleCellKind.Wall)
            {
                var directions = topology.GetDirections();
                if (directions.Length != 2)
                {
                    throw new InvalidOperationException($"일반 성벽 {cell.Coordinates}은 2방향이어야 합니다.");
                }

                var resolution = HexCastleWallVisualResolver.ResolveDirections(
                    HexCastleCellKind.Wall,
                    directions[0],
                    directions[1]);
                var wall = InstantiateVisual(
                    visualSet.ResolveWall(resolution.VisualKind),
                    contentVisualRoot,
                    "WallVisual");
                wall.transform.localRotation = Quaternion.Euler(0f, resolution.RotationDegrees, 0f);
            }
            else if (cell.Kind == HexCastleCellKind.Tower)
            {
                foreach (var direction in topology.GetDirections())
                {
                    var stub = InstantiateVisual(
                        visualSet.WallStub,
                        contentVisualRoot,
                        $"WallStub_D{direction}");
                    stub.transform.localRotation = Quaternion.Euler(0f, direction * 60f, 0f);
                }

                InstantiateVisual(
                    visualSet.TowerOverlay,
                    contentVisualRoot,
                    "TowerOverlay",
                    visualSet.KayKitMaterial);
            }
            else if (cell.Kind == HexCastleCellKind.Gate)
            {
                var directions = topology.GetDirections();
                if (directions.Length != 2 || topology.ResolveTwoWaySeparation() != 3)
                {
                    throw new InvalidOperationException($"성문 {cell.Coordinates}은 직선 연결이어야 합니다.");
                }

                var gatePrefab = cell.GateRole == HexCastleGateRole.OpenDefenderPassage
                    ? visualSet.OpenGate
                    : visualSet.ClosedGate;
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
            else if (cell.Kind == HexCastleCellKind.Palace && cell.Coordinates == new HexCoordinates(0, 0))
            {
                var palace = InstantiateVisual(
                    visualSet.Palace,
                    contentVisualRoot,
                    "PalaceVisual",
                    visualSet.KayKitMaterial);
                palace.transform.localScale = Vector3.one * PalaceVisualScale;
            }
            else if (cell.IsBuildingCell)
            {
                var buildingPrefab = visualSet.ResolveBuilding(cell.VisualVariantId);
                if (buildingPrefab == null)
                {
                    throw new InvalidOperationException(
                        $"건물 {cell.Coordinates} Visual Prefab이 없습니다: {cell.VisualVariantId}");
                }

                var building = cell.BuildingRole == HexCastleBuildingRole.Turret
                    ? CreateTurretBuildingVisual(cell, contentVisualRoot, buildingPrefab, visualSet)
                    : InstantiateVisual(
                        buildingPrefab,
                        contentVisualRoot,
                        $"BuildingVisual_{cell.BuildingRole}",
                        visualSet.KayKitMaterial);
                if (cell.BuildingRole == HexCastleBuildingRole.Turret)
                {
                    var headMount = building.transform.Find("Joint_TurretHeadMount");
                    turretHead = headMount != null && headMount.childCount == 1
                        ? headMount.GetChild(0)
                        : throw new InvalidOperationException($"포탑 {cell.Coordinates} Head 조립에 실패했습니다.");
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

            if (cell.BuildingRole != HexCastleBuildingRole.Turret)
            {
                return;
            }

            var attackProfile = turretAttackCatalog.Resolve(cell.TurretWeaponKind, cell.BuildingGrade);
            if (attackProfile == null)
            {
                throw new InvalidOperationException(
                    $"포탑 {cell.Coordinates} 공격 Profile이 없습니다: {cell.TurretWeaponKind} Lv{cell.BuildingGrade}");
            }

            var turretVisual = cellRoot.gameObject.AddComponent<HexCastleTurretVisual>();
            turretVisual.Configure(cell.TurretWeaponKind, cell.BuildingGrade, turretHead);
            var turretRuntime = cellRoot.gameObject.AddComponent<HexCastleTurretRuntime>();
            turretRuntime.Configure(combatWorld, runtime, turretVisual, attackProfile);
        }

        private static GameObject CreateTurretBuildingVisual(
            HexCastleCell cell,
            Transform parent,
            GameObject towerBasePrefab,
            HexCastleVisualSet visualSet)
        {
            var assembly = CreateChild($"BuildingVisual_{cell.BuildingRole}", parent).gameObject;
            var towerBase = InstantiateVisual(
                towerBasePrefab,
                assembly.transform,
                "TurretBaseVisual",
                visualSet.KayKitMaterial);
            var baseBounds = ResolveBounds(towerBase.GetComponentsInChildren<Renderer>(true));
            var headPrefab = visualSet.ResolveTurretHead(cell.TurretWeaponKind, cell.BuildingGrade);
            if (headPrefab == null)
            {
                throw new InvalidOperationException(
                    $"포탑 Head Prefab이 없습니다: {cell.TurretWeaponKind} Lv{cell.BuildingGrade}");
            }

            var headMount = CreateChild("Joint_TurretHeadMount", assembly.transform);
            headMount.localPosition = Vector3.up * (baseBounds.max.y - assembly.transform.position.y);
            var head = InstantiateVisual(
                headPrefab,
                headMount,
                $"Head_{cell.TurretWeaponKind}_Lv{cell.BuildingGrade}");
            FitTurretHead(head.transform, Mathf.Min(baseBounds.size.x, baseBounds.size.z));
            SeatBallistaHead(cell.TurretWeaponKind, head.transform, baseBounds.max.y);
            return assembly;
        }

        private static void FitTurretHead(Transform head, float minimumFootprint)
        {
            var model = head.Find("Joint_BodyMount/YawPivot/PitchPivot/Model");
            var renderers = model == null
                ? Array.Empty<Renderer>()
                : model.GetComponentsInChildren<Renderer>(true);
            var bounds = ResolveBounds(renderers);
            var currentFootprint = Mathf.Max(bounds.size.x, bounds.size.z);
            if (currentFootprint <= 0.001f)
            {
                throw new InvalidOperationException($"포탑 Head {head.name} Renderer Bounds가 비었습니다.");
            }

            var targetFootprint = Mathf.Clamp(minimumFootprint * TurretHeadFootprintRatio, 0.82f, 1.55f);
            head.localScale = Vector3.one * Mathf.Clamp(targetFootprint / currentFootprint, 0.65f, 1.35f);
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
            var bounds = ResolveBounds(model == null
                ? Array.Empty<Renderer>()
                : model.GetComponentsInChildren<Renderer>(true));
            head.position += Vector3.up * (baseTopWorldY - BallistaSeatInset - bounds.min.y);
        }

        private static GameObject InstantiateVisual(
            GameObject prefab,
            Transform parent,
            string instanceName,
            Material materialOverride = null)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            var instance = Object.Instantiate(prefab, parent, false);
            instance.name = instanceName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            if (materialOverride != null)
            {
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials = Enumerable.Repeat(
                        materialOverride,
                        renderer.sharedMaterials.Length).ToArray();
                }
            }

            return instance;
        }

        private static Mesh CreateBoardSurface(
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

            var mesh = new Mesh { name = "MESH_CRHex_ProceduralBoard_Runtime" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var board = CreateChild("00_BoardSurface", parent);
            board.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            board.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return mesh;
        }

        private static Mesh CreateHexPrismMesh(float height)
        {
            var vertices = new Vector3[14];
            vertices[0] = Vector3.zero;
            vertices[1] = Vector3.up * height;
            for (var index = 0; index < 6; index++)
            {
                var angle = Mathf.Deg2Rad * (30f + index * 60f);
                var x = Mathf.Cos(angle) * HexSpatialContract.CellOuterRadius * 0.96f;
                var z = Mathf.Sin(angle) * HexSpatialContract.CellOuterRadius * 0.96f;
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
                triangles.AddRange(new[]
                {
                    0, bottom, nextBottom,
                    1, nextTop, top,
                    bottom, top, nextTop,
                    bottom, nextTop, nextBottom
                });
            }

            var mesh = new Mesh { name = $"MESH_CRHex_ProceduralFootprint_{height:0.00}" };
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

        private static float ResolveBuildingVisualScale(HexCastleBuildingRole role)
        {
            return role == HexCastleBuildingRole.GoldStorage ||
                   role == HexCastleBuildingRole.EquipmentForge
                ? GoldEquipmentVisualScale
                : BuildingVisualScale;
        }

        private static Transform CreateChild(string name, Transform parent)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Bounds ResolveBounds(IReadOnlyList<Renderer> renderers)
        {
            if (renderers == null || renderers.Count == 0)
            {
                throw new InvalidOperationException("Hex 절차 생성 Visual Renderer Bounds가 비었습니다.");
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Count; index++)
            {
                if (renderers[index] != null)
                {
                    bounds.Encapsulate(renderers[index].bounds);
                }
            }

            return bounds;
        }
    }
}
