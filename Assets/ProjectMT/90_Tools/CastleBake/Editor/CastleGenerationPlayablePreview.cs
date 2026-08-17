using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Contents.CastleRaid;
using ProjectMT.Contents.CastleRaid.Generation;
using ProjectMT.Shared.Unit;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectMT.EditorTools.CastleBake
{
    [InitializeOnLoad]
    public static class CastleGenerationPlayablePreview // 저장 전 제거되는 생성 성 전투 시험장
    {
        public const string PlayableRootName = "__CastleGenerationPlayablePreview";

        private const string SceneRootName = "00_SceneRoot";
        private const string WorldRootPath = "01_WorldRoot";
        private const string ExistingStagePath = "01_WorldRoot/CastleStage_Seed";
        private const float FullMapCameraSizePerWorldUnit = 11.5f / 20f;
        private const float MinimumCameraSize = 5f;
        private const float SlotPadding = 0.72f;

        private const string DeploymentMaterialPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/Art/Materials/MAT_DeploymentZone.mat";
        private const string WallMaterialPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/Art/Materials/MAT_Castle_Wall.mat";
        private const string BuildingMaterialPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/Art/Materials/MAT_Castle_Building.mat";
        private const string PalaceMaterialPath =
            "Assets/ProjectMT/04_Contents/01_CastleRaid/Art/Materials/MAT_Castle_Main.mat";
        private const string MonsterCatalogPath =
            "Assets/ProjectMT/02_Shared/Unit/Data/MonsterCatalog.asset";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        static CastleGenerationPlayablePreview()
        {
            EditorSceneManager.sceneSaving += HandleSceneSaving;
            EditorSceneManager.sceneClosing += HandleSceneClosing;
            EditorSceneManager.activeSceneChangedInEditMode += HandleActiveSceneChangedInEditMode;
            EditorApplication.quitting += ClearAllOpenScenes;
        }

        public static GeneratedCastleRuntimeStage Rebuild(
            CastleGenerationCandidate candidate,
            Vector3 worldOffset,
            float cellSize,
            bool focusSceneView = true)
        {
            return Rebuild(
                candidate,
                SceneManager.GetActiveScene(),
                worldOffset,
                cellSize,
                focusSceneView);
        }

        public static GeneratedCastleRuntimeStage Rebuild(
            CastleGenerationCandidate candidate,
            Scene scene,
            Vector3 worldOffset,
            float cellSize,
            bool focusSceneView = false)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("플레이 프리뷰를 만들 열린 Scene이 필요합니다.", nameof(scene));
            }

            cellSize = Mathf.Clamp(cellSize, 0.5f, 1.5f);
            CastleGenerationScenePreview.Clear(scene);
            Clear(scene);

            var sceneRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == SceneRootName);
            var worldRoot = sceneRoot?.transform.Find(WorldRootPath);
            var existingStage = sceneRoot?.transform.Find(ExistingStagePath)?.gameObject;
            var raidController = FindComponentInScene<CastleRaidController>(scene);
            var cameraController = FindComponentInScene<CastleRaidCameraController>(scene);
            var targetCamera = cameraController == null ? FindComponentInScene<Camera>(scene) : cameraController.GetComponent<Camera>();
            if (sceneRoot == null || worldRoot == null || existingStage == null ||
                raidController == null || cameraController == null || targetCamera == null)
            {
                throw new InvalidOperationException(
                    "플레이 프리뷰에는 00_SceneRoot, 01_WorldRoot, CastleStage_Seed, CastleRaidController와 CastleRaidCamera가 필요합니다.");
            }

            var displayBounds = CastleGenerationScenePreview.ResolveSquareDisplayBounds(candidate);
            var displayCenter = ResolveDisplayCenter(candidate, displayBounds, cellSize);
            var displaySide = displayBounds.width * cellSize;
            var groundCenter = worldOffset + new Vector3(displayCenter.x, 0f, displayCenter.y);

            var root = CreateChild(PlayableRootName, worldRoot, scene);
            root.transform.position = worldOffset;

            var materials = LoadMaterials();
            var monsterCatalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(MonsterCatalogPath);
            if (monsterCatalog == null)
            {
                Object.DestroyImmediate(root);
                throw new InvalidOperationException("생성 성 플레이 프리뷰용 MonsterCatalog을 찾지 못했습니다.");
            }

            BuildGround(root.transform, scene, displayCenter, displaySide, cellSize, materials.Deployment);
            var targetsRoot = CreateChild("02_Targets", root.transform, scene).transform;
            var targets = new List<CastleTarget>(candidate.Placements.Count);
            var colliders = new List<Collider>(candidate.Placements.Count);
            var obstacles = new List<NavMeshObstacle>(candidate.Placements.Count);
            Transform innerEntry = null;
            foreach (var placement in candidate.Placements)
            {
                var target = BuildTarget(
                    candidate,
                    placement,
                    targetsRoot,
                    scene,
                    cellSize,
                    materials,
                    out var targetCollider,
                    out var targetObstacle);
                targets.Add(target);
                colliders.Add(targetCollider);
                if (targetObstacle != null)
                {
                    obstacles.Add(targetObstacle);
                }

                if (placement.Kind == CastlePlacementKind.Palace)
                {
                    innerEntry = CreateChild("InnerEntry", target.transform, scene).transform;
                    innerEntry.localPosition = Vector3.zero;
                }
            }

            if (innerEntry == null)
            {
                Object.DestroyImmediate(root);
                throw new InvalidOperationException("생성 후보에 왕궁 Placement가 없습니다.");
            }

            var zoneObject = CreateChild("01_DeploymentZone", root.transform, scene);
            zoneObject.transform.localPosition = new Vector3(displayCenter.x, 0f, displayCenter.y);
            var deploymentZone = zoneObject.AddComponent<CastleDeploymentZone>();
            var outerHalfExtents = Vector2.one * (displaySide * 0.5f);
            var innerHalfExtents = Vector2.Max(
                Vector2.zero,
                outerHalfExtents - Vector2.one * (CastleSpatialContract.DeploymentMargin * cellSize));
            deploymentZone.ConfigureBounds(outerHalfExtents, innerHalfExtents, Mathf.Max(0.5f, cellSize));

            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;

            var cameraSize = CastleGenerationScenePreview.ResolvePreviewCameraSize(candidate, cellSize);
            var runtimeStage = root.AddComponent<GeneratedCastleRuntimeStage>();
            runtimeStage.EditorConfigure(
                raidController,
                cameraController,
                deploymentZone,
                innerEntry,
                targets.ToArray(),
                surface,
                colliders.ToArray(),
                obstacles.ToArray(),
                new Vector2(groundCenter.x, groundCenter.z),
                Vector2.one * displaySide,
                cameraSize,
                MinimumCameraSize,
                Mathf.Max(cameraSize, displaySide * FullMapCameraSizePerWorldUnit),
                monsterCatalog,
                true);
            runtimeStage.EditorPreparePreviewPresentation(
                existingStage,
                targetCamera,
                groundCenter,
                cameraSize);

            if (focusSceneView)
            {
                Selection.activeGameObject = root;
                SceneView.lastActiveSceneView?.FrameSelected(false);
                Selection.objects = Array.Empty<Object>();
                SceneView.RepaintAll();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return runtimeStage;
        }

        public static int ClearActive()
        {
            return Clear(SceneManager.GetActiveScene());
        }

        public static int Clear(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return 0;
            }

            var roots = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(transform => transform.name == PlayableRootName ||
                                    transform.GetComponent<GeneratedCastleRuntimeStage>() != null)
                .Select(transform => transform.gameObject)
                .Distinct()
                .Where(candidate => !candidate.transform.GetComponentsInParent<GeneratedCastleRuntimeStage>(true)
                    .Any(stage => stage.gameObject != candidate))
                .ToArray();
            foreach (var root in roots)
            {
                if (Selection.activeGameObject != null &&
                    Selection.activeGameObject.transform.IsChildOf(root.transform))
                {
                    Selection.objects = Array.Empty<Object>();
                }

                root.GetComponent<GeneratedCastleRuntimeStage>()?.RestorePreviewPresentation();
                Object.DestroyImmediate(root);
            }

            return roots.Length;
        }

        private static CastleTarget BuildTarget(
            CastleGenerationCandidate candidate,
            CastlePlacementData placement,
            Transform parent,
            Scene scene,
            float cellSize,
            MaterialSet materials,
            out Collider targetCollider,
            out NavMeshObstacle targetObstacle)
        {
            var targetObject = CreateChild(placement.PlacementId, parent, scene);
            var center = ResolvePlacementCenter(candidate, placement, cellSize);
            targetObject.transform.localPosition = new Vector3(center.x, 0f, center.y);
            var footprint = new Vector2(placement.Width * cellSize, placement.Height * cellSize);
            var height = ResolveHeight(placement, cellSize);
            BuildTargetVisual(targetObject.transform, scene, placement, footprint, height, materials);

            var boxCollider = targetObject.AddComponent<BoxCollider>();
            boxCollider.center = new Vector3(0f, height * 0.5f, 0f);
            boxCollider.size = new Vector3(
                Mathf.Max(0.2f, footprint.x * 0.92f),
                height,
                Mathf.Max(0.2f, footprint.y * 0.92f));
            targetCollider = boxCollider;

            var health = targetObject.AddComponent<HealthComponent>();
            var slots = targetObject.AddComponent<AttackSlotProvider>();
            slots.EditorSetSlots(BuildAttackSlots(targetObject.transform, scene, footprint));

            targetObstacle = null;
            if (placement.Kind != CastlePlacementKind.Defender)
            {
                targetObstacle = targetObject.AddComponent<NavMeshObstacle>();
                targetObstacle.shape = NavMeshObstacleShape.Box;
                targetObstacle.center = boxCollider.center;
                targetObstacle.size = boxCollider.size;
                targetObstacle.carving = true;
                targetObstacle.carveOnlyStationary = true;
            }

            var target = targetObject.AddComponent<CastleTarget>();
            target.EditorConfigure(
                ResolveTargetKind(placement.Kind),
                Mathf.Max(1f, placement.EffectiveHealth),
                slots,
                targetObstacle);
            health.Initialize(Mathf.Max(1f, placement.EffectiveHealth));
            return target;
        }

        private static void BuildTargetVisual(
            Transform parent,
            Scene scene,
            CastlePlacementData placement,
            Vector2 footprint,
            float height,
            MaterialSet materials)
        {
            var material = ResolveMaterial(placement.Kind, materials);
            var color = ResolveColor(placement);
            if (placement.Kind == CastlePlacementKind.Palace)
            {
                CreateVisualBox("Base", parent, scene, footprint.x, height * 0.58f, footprint.y, height * 0.29f, material, color);
                CreateVisualBox("Keep", parent, scene, footprint.x * 0.72f, height * 0.27f, footprint.y * 0.72f, height * 0.715f, material, Color.Lerp(color, Color.white, 0.12f));
                CreateVisualBox("Crown", parent, scene, footprint.x * 0.42f, height * 0.15f, footprint.y * 0.42f, height * 0.925f, material, Color.Lerp(color, Color.white, 0.24f));
                return;
            }

            if (placement.Kind == CastlePlacementKind.Wall)
            {
                CreateVisualBox("Wall", parent, scene, footprint.x * 0.98f, height, footprint.y * 0.98f, height * 0.5f, material, color);
                return;
            }

            CreateVisualBox("Body", parent, scene, footprint.x * 0.88f, height * 0.72f, footprint.y * 0.88f, height * 0.36f, material, color);
            CreateVisualBox("Cap", parent, scene, footprint.x * 0.58f, height * 0.28f, footprint.y * 0.58f, height * 0.86f, material, Color.Lerp(color, Color.white, 0.16f));
        }

        private static Transform[] BuildAttackSlots(Transform parent, Scene scene, Vector2 footprint)
        {
            var halfX = footprint.x * 0.5f + SlotPadding;
            var halfZ = footprint.y * 0.5f + SlotPadding;
            var positions = new[]
            {
                new Vector3(0f, 0f, halfZ),
                new Vector3(halfX, 0f, 0f),
                new Vector3(0f, 0f, -halfZ),
                new Vector3(-halfX, 0f, 0f),
                new Vector3(halfX, 0f, halfZ),
                new Vector3(halfX, 0f, -halfZ),
                new Vector3(-halfX, 0f, -halfZ),
                new Vector3(-halfX, 0f, halfZ)
            };
            var slots = new Transform[positions.Length];
            for (var index = 0; index < positions.Length; index++)
            {
                var slot = CreateChild($"AttackSlot_{index + 1:00}", parent, scene).transform;
                slot.localPosition = positions[index];
                slots[index] = slot;
            }

            return slots;
        }

        private static void BuildGround(
            Transform parent,
            Scene scene,
            Vector2 center,
            float displaySide,
            float cellSize,
            Material deploymentMaterial)
        {
            var groundRoot = CreateChild("00_Ground", parent, scene).transform;
            groundRoot.localPosition = new Vector3(center.x, 0f, center.y);
            CreateGroundBox(
                "DeploymentBelt",
                groundRoot,
                scene,
                displaySide,
                0.2f,
                deploymentMaterial,
                new Color(0.12f, 0.22f, 0.18f),
                true);
            var innerSide = Mathf.Max(
                cellSize,
                displaySide - CastleSpatialContract.DeploymentMargin * 2f * cellSize);
            var inner = CreateGroundBox(
                "BuildArea",
                groundRoot,
                scene,
                innerSide,
                0.04f,
                deploymentMaterial,
                new Color(0.20f, 0.34f, 0.23f),
                false);
            inner.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        }

        private static GameObject CreateGroundBox(
            string name,
            Transform parent,
            Scene scene,
            float side,
            float height,
            Material material,
            Color color,
            bool keepCollider)
        {
            var ground = CreatePrimitive(name, parent, scene);
            ground.transform.localPosition = new Vector3(0f, -height * 0.5f, 0f);
            ground.transform.localScale = new Vector3(side, height, side);
            ApplyMaterial(ground.GetComponent<Renderer>(), material, color);
            if (!keepCollider)
            {
                Object.DestroyImmediate(ground.GetComponent<Collider>());
            }

            return ground;
        }

        private static void CreateVisualBox(
            string name,
            Transform parent,
            Scene scene,
            float width,
            float height,
            float depth,
            float centerY,
            Material material,
            Color color)
        {
            var visual = CreatePrimitive(name, parent, scene);
            visual.transform.localPosition = new Vector3(0f, centerY, 0f);
            visual.transform.localScale = new Vector3(width, height, depth);
            ApplyMaterial(visual.GetComponent<Renderer>(), material, color);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
        }

        private static GameObject CreatePrimitive(string name, Transform parent, Scene scene)
        {
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            primitive.name = name;
            if (primitive.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(primitive, scene);
            }

            primitive.transform.SetParent(parent, false);
            return primitive;
        }

        private static GameObject CreateChild(string name, Transform parent, Scene scene)
        {
            var child = new GameObject(name);
            if (child.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(child, scene);
            }

            child.transform.SetParent(parent, false);
            return child;
        }

        private static void ApplyMaterial(Renderer renderer, Material material, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }

        private static CastleTargetKind ResolveTargetKind(CastlePlacementKind kind)
        {
            switch (kind)
            {
                case CastlePlacementKind.Wall:
                    return CastleTargetKind.Wall;
                case CastlePlacementKind.Defender:
                    return CastleTargetKind.Defender;
                case CastlePlacementKind.Palace:
                    return CastleTargetKind.MainCastle;
                default:
                    return CastleTargetKind.Building;
            }
        }

        private static float ResolveHeight(CastlePlacementData placement, float cellSize)
        {
            switch (placement.Kind)
            {
                case CastlePlacementKind.Wall:
                    return cellSize * Mathf.Lerp(1.05f, 1.55f, Mathf.InverseLerp(1f, 5f, placement.WallTier));
                case CastlePlacementKind.Palace:
                    return cellSize * 4.2f;
                case CastlePlacementKind.Defender:
                    return cellSize * 1.9f;
                case CastlePlacementKind.DefenseBuilding:
                    return cellSize * 2.8f;
                case CastlePlacementKind.LootBuilding:
                    return cellSize * 2.25f;
                default:
                    return cellSize * 2.4f;
            }
        }

        private static Color ResolveColor(CastlePlacementData placement)
        {
            switch (placement.Kind)
            {
                case CastlePlacementKind.Wall:
                    var wallBase = placement.WallBand == CastleWallBand.OuterPerimeter
                        ? new Color(0.43f, 0.34f, 0.25f)
                        : placement.WallBand == CastleWallBand.CoreDefense
                            ? new Color(0.66f, 0.54f, 0.27f)
                            : new Color(0.52f, 0.52f, 0.48f);
                    return Color.Lerp(wallBase, Color.white, Mathf.InverseLerp(1f, 5f, placement.WallTier) * 0.18f);
                case CastlePlacementKind.Palace:
                    return new Color(1f, 0.62f, 0.08f);
                case CastlePlacementKind.Defender:
                    return new Color(0.62f, 0.18f, 0.13f);
                case CastlePlacementKind.DefenseBuilding:
                    return new Color(0.78f, 0.22f, 0.16f);
                case CastlePlacementKind.LootBuilding:
                    if (placement.LootKind == CastleLootKind.Gold)
                    {
                        return new Color(0.95f, 0.72f, 0.12f);
                    }

                    if (placement.LootKind == CastleLootKind.Equipment)
                    {
                        return new Color(0.15f, 0.72f, 0.68f);
                    }

                    return new Color(0.22f, 0.42f, 0.88f);
                default:
                    return new Color(0.54f, 0.61f, 0.66f);
            }
        }

        private static Material ResolveMaterial(CastlePlacementKind kind, MaterialSet materials)
        {
            switch (kind)
            {
                case CastlePlacementKind.Wall:
                    return materials.Wall;
                case CastlePlacementKind.Palace:
                    return materials.Palace;
                default:
                    return materials.Building;
            }
        }

        private static MaterialSet LoadMaterials()
        {
            var result = new MaterialSet(
                AssetDatabase.LoadAssetAtPath<Material>(DeploymentMaterialPath),
                AssetDatabase.LoadAssetAtPath<Material>(WallMaterialPath),
                AssetDatabase.LoadAssetAtPath<Material>(BuildingMaterialPath),
                AssetDatabase.LoadAssetAtPath<Material>(PalaceMaterialPath));
            if (!result.IsValid)
            {
                throw new InvalidOperationException("생성 성 플레이 프리뷰용 CastleRaid Material을 찾지 못했습니다.");
            }

            return result;
        }

        private static Vector2 ResolvePlacementCenter(
            CastleGenerationCandidate candidate,
            CastlePlacementData placement,
            float cellSize)
        {
            return new Vector2(
                (placement.X + placement.Width * 0.5f) * cellSize - candidate.GridWidth * cellSize * 0.5f,
                (placement.Z + placement.Height * 0.5f) * cellSize - candidate.GridHeight * cellSize * 0.5f);
        }

        private static Vector2 ResolveDisplayCenter(
            CastleGenerationCandidate candidate,
            RectInt displayBounds,
            float cellSize)
        {
            return new Vector2(
                (displayBounds.xMin + displayBounds.width * 0.5f) * cellSize - candidate.GridWidth * cellSize * 0.5f,
                (displayBounds.yMin + displayBounds.height * 0.5f) * cellSize - candidate.GridHeight * cellSize * 0.5f);
        }

        private static T FindComponentInScene<T>(Scene scene)
            where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .FirstOrDefault();
        }

        private static void HandleSceneSaving(Scene scene, string path)
        {
            Clear(scene); // 플레이 시험장은 정식 Scene에 저장하지 않는다
        }

        private static void HandleSceneClosing(Scene scene, bool removingScene)
        {
            Clear(scene); // Scene이 닫히기 전에 기존 성과 카메라를 복원한다
        }

        private static void HandleActiveSceneChangedInEditMode(Scene previousScene, Scene nextScene)
        {
            Clear(previousScene); // 다른 Scene으로 이동하면 이전 Scene의 시험장을 남기지 않는다
        }

        private static void ClearAllOpenScenes()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                Clear(SceneManager.GetSceneAt(index));
            }
        }

        private readonly struct MaterialSet
        {
            public MaterialSet(Material deployment, Material wall, Material building, Material palace)
            {
                Deployment = deployment;
                Wall = wall;
                Building = building;
                Palace = palace;
            }

            public Material Deployment { get; }
            public Material Wall { get; }
            public Material Building { get; }
            public Material Palace { get; }
            public bool IsValid => Deployment != null && Wall != null && Building != null && Palace != null;
        }
    }
}
