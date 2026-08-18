using System;
using System.Collections.Generic;
using ProjectMT.Contents.CastleRaid.Generation;
using ProjectMT.Shared.Unit;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectMT.Contents.CastleRaid
{
    [DisallowMultipleComponent]
    public sealed class CastleRuntimeStageGenerator : MonoBehaviour // 입장·재도전 때 검수된 랜덤 성을 만든다
    {
        private const string RuntimeStageName = "CastleStage_RuntimeGenerated";
        private const int DisplayMarginCells = CastleSpatialContract.DeploymentMargin;
        private const float FullMapCameraSizePerWorldUnit = 11.5f / 20f;
        private const float CameraSizePerWorldUnit = FullMapCameraSizePerWorldUnit * 0.72f;
        private const float MinimumCameraSize = 5f;
        private const float SlotPadding = 0.72f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Generation")]
        [SerializeField] private CastleGenerationRules rules;
        [SerializeField, Range(2, 4)] private int defaultDefenseLayerCount = 2;
        [SerializeField, Range(1, 64)] private int maximumGenerationAttempts = 32;
        [SerializeField, Range(0.5f, 1.5f)] private float cellSize = 1f;

        [Header("Scene")]
        [SerializeField] private CastleRaidController raidController;
        [SerializeField] private CastleRaidCameraController cameraController;
        [SerializeField] private Transform worldRoot;
        [SerializeField] private GameObject fallbackStage;

        [Header("Materials")]
        [SerializeField] private Material deploymentMaterial;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material buildingMaterial;
        [SerializeField] private Material palaceMaterial;

        private GameObject currentStageRoot;
        private int currentSeed;
        private CastleLayoutTheme currentTheme;
        private int currentDefenseLayerCount;

        public int CurrentSeed => currentSeed;
        public CastleLayoutTheme CurrentTheme => currentTheme;
        public int CurrentDefenseLayerCount => currentDefenseLayerCount;
        public string CurrentSummary => currentStageRoot == null
            ? "성을 준비하는 중입니다"
            : $"{ResolveThemeLabel(currentTheme)} · {currentDefenseLayerCount}중벽 · Seed {currentSeed}";

        public void EnsureGeneratedStage()
        {
            if (currentStageRoot == null)
            {
                GenerateRandomStage(defaultDefenseLayerCount);
            }
        }

        public void GenerateRandomStage(int defenseLayerCount)
        {
            if (!Application.isPlaying)
            {
                throw new InvalidOperationException("런타임 성 생성은 Play Mode에서만 실행할 수 있습니다.");
            }

            ValidateReferences();
            defenseLayerCount = Mathf.Clamp(defenseLayerCount, 2, 4);
            var candidate = GenerateValidCandidate(defenseLayerCount);
            var nextStage = BuildStage(candidate);
            var previousStage = currentStageRoot;

            try
            {
                previousStage?.SetActive(false);
                fallbackStage?.SetActive(false);
                nextStage.SetActive(true); // Awake에서 NavMesh·카메라·전투 참조를 연결한다

                currentStageRoot = nextStage;
                currentSeed = candidate.Seed;
                currentTheme = candidate.Theme;
                currentDefenseLayerCount = candidate.RequestedDefenseLayerCount;
                if (previousStage != null)
                {
                    Destroy(previousStage);
                }

                Debug.Log(
                    $"Castle Raid runtime stage generated. Theme={candidate.Theme}, Layers={candidate.RequestedDefenseLayerCount}, " +
                    $"Seed={candidate.Seed}, Placements={candidate.Placements.Count}",
                    this);
            }
            catch
            {
                Destroy(nextStage);
                previousStage?.SetActive(true);
                if (previousStage == null)
                {
                    fallbackStage?.SetActive(true);
                }

                throw;
            }
        }

        private CastleGenerationCandidate GenerateValidCandidate(int defenseLayerCount)
        {
            var generator = new CastleGenerator();
            CastleGenerationCandidate lastCandidate = null;
            Exception lastException = null;
            for (var attempt = 0; attempt < maximumGenerationAttempts; attempt++)
            {
                var seed = CreateRandomSeed();
                var themes = CastleGenerationRules.SupportedLayoutThemes;
                var theme = themes[seed % themes.Count]; // 전투 전역 Random 상태는 건드리지 않는다
                try
                {
                    var candidate = generator.Generate(rules, seed, theme, defenseLayerCount);
                    lastCandidate = candidate;
                    if (candidate.Validation != null && candidate.Validation.IsValid &&
                        candidate.Difficulty != null && candidate.Difficulty.HasClearPath)
                    {
                        return candidate;
                    }
                }
                catch (Exception exception)
                {
                    lastException = exception;
                }
            }

            var issue = lastCandidate?.Validation?.Issues.Count > 0
                ? lastCandidate.Validation.Issues[0].Message
                : lastException?.Message ?? "유효한 후보가 나오지 않았습니다.";
            throw new InvalidOperationException(
                $"{maximumGenerationAttempts}회 안에 {defenseLayerCount}중벽 성을 생성하지 못했습니다. {issue}",
                lastException);
        }

        private int CreateRandomSeed()
        {
            var seed = Guid.NewGuid().GetHashCode() & int.MaxValue;
            if (seed == 0 || seed == currentSeed)
            {
                seed = currentSeed >= int.MaxValue ? 1 : Mathf.Max(1, currentSeed + 1);
            }

            return seed;
        }

        private GameObject BuildStage(CastleGenerationCandidate candidate)
        {
            var displayBounds = ResolveSquareDisplayBounds(candidate);
            var exteriorCells = CastleDeploymentAreaResolver.ResolveExteriorCells(candidate, displayBounds);
            if (exteriorCells.Count == 0)
            {
                throw new InvalidOperationException("생성된 성에서 배치 가능한 외곽 셀을 찾지 못했습니다.");
            }

            var displayCenter = ResolveDisplayCenter(candidate, displayBounds, cellSize);
            var displaySide = displayBounds.width * cellSize;
            var groundCenter = new Vector2(displayCenter.x, displayCenter.y);
            var root = new GameObject(RuntimeStageName);
            root.SetActive(false);
            root.transform.SetParent(worldRoot, false);

            try
            {
                BuildGround(root.transform, displayCenter, displaySide);
                var targetsRoot = CreateChild("02_Targets", root.transform).transform;
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
                        cellSize,
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
                        innerEntry = CreateChild("InnerEntry", target.transform).transform;
                    }
                }

                if (innerEntry == null)
                {
                    throw new InvalidOperationException("생성 후보에 왕궁 Placement가 없습니다.");
                }

                var zoneObject = CreateChild("01_DeploymentZone", root.transform);
                zoneObject.transform.localPosition = new Vector3(displayCenter.x, 0f, displayCenter.y);
                var deploymentZone = zoneObject.AddComponent<CastleDeploymentZone>();
                deploymentZone.ConfigureExteriorCells(
                    displayBounds,
                    exteriorCells,
                    cellSize,
                    Mathf.Max(0.5f, cellSize));

                var surface = root.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.layerMask = ~0;

                var cameraSize = displaySide * CameraSizePerWorldUnit;
                var runtimeStage = root.AddComponent<GeneratedCastleRuntimeStage>();
                runtimeStage.Configure(
                    raidController,
                    cameraController,
                    deploymentZone,
                    innerEntry,
                    targets.ToArray(),
                    surface,
                    colliders.ToArray(),
                    obstacles.ToArray(),
                    groundCenter,
                    Vector2.one * displaySide,
                    cameraSize,
                    MinimumCameraSize,
                    Mathf.Max(cameraSize, displaySide * FullMapCameraSizePerWorldUnit));
                return root;
            }
            catch
            {
                Destroy(root);
                throw;
            }
        }

        private CastleTarget BuildTarget(
            CastleGenerationCandidate candidate,
            CastlePlacementData placement,
            Transform parent,
            float targetCellSize,
            out Collider targetCollider,
            out NavMeshObstacle targetObstacle)
        {
            var targetObject = CreateChild(placement.PlacementId, parent);
            var center = ResolvePlacementCenter(candidate, placement, targetCellSize);
            targetObject.transform.localPosition = new Vector3(center.x, 0f, center.y);
            var footprint = new Vector2(placement.Width * targetCellSize, placement.Height * targetCellSize);
            var height = ResolveHeight(placement, targetCellSize);
            BuildTargetVisual(targetObject.transform, placement, footprint, height);

            var boxCollider = targetObject.AddComponent<BoxCollider>();
            boxCollider.center = new Vector3(0f, height * 0.5f, 0f);
            boxCollider.size = new Vector3(
                Mathf.Max(0.2f, footprint.x * 0.92f),
                height,
                Mathf.Max(0.2f, footprint.y * 0.92f));
            targetCollider = boxCollider;

            var health = targetObject.AddComponent<HealthComponent>();
            var slots = targetObject.AddComponent<AttackSlotProvider>();
            slots.ConfigureSlots(BuildAttackSlots(targetObject.transform, footprint));

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
            target.Configure(
                ResolveTargetKind(placement.Kind),
                Mathf.Max(1f, placement.EffectiveHealth),
                slots,
                targetObstacle);
            health.Initialize(Mathf.Max(1f, placement.EffectiveHealth));
            return target;
        }

        private void BuildTargetVisual(
            Transform parent,
            CastlePlacementData placement,
            Vector2 footprint,
            float height)
        {
            var material = ResolveMaterial(placement.Kind);
            var color = ResolveColor(placement);
            if (placement.Kind == CastlePlacementKind.Palace)
            {
                CreateVisualBox("Base", parent, footprint.x, height * 0.58f, footprint.y, height * 0.29f, material, color);
                CreateVisualBox("Keep", parent, footprint.x * 0.72f, height * 0.27f, footprint.y * 0.72f, height * 0.715f, material, Color.Lerp(color, Color.white, 0.12f));
                CreateVisualBox("Crown", parent, footprint.x * 0.42f, height * 0.15f, footprint.y * 0.42f, height * 0.925f, material, Color.Lerp(color, Color.white, 0.24f));
                return;
            }

            if (placement.Kind == CastlePlacementKind.Wall)
            {
                CreateVisualBox("Wall", parent, footprint.x * 0.98f, height, footprint.y * 0.98f, height * 0.5f, material, color);
                return;
            }

            CreateVisualBox("Body", parent, footprint.x * 0.88f, height * 0.72f, footprint.y * 0.88f, height * 0.36f, material, color);
            CreateVisualBox("Cap", parent, footprint.x * 0.58f, height * 0.28f, footprint.y * 0.58f, height * 0.86f, material, Color.Lerp(color, Color.white, 0.16f));
        }

        private static Transform[] BuildAttackSlots(Transform parent, Vector2 footprint)
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
                var slot = CreateChild($"AttackSlot_{index + 1:00}", parent).transform;
                slot.localPosition = positions[index];
                slots[index] = slot;
            }

            return slots;
        }

        private void BuildGround(
            Transform parent,
            Vector2 center,
            float displaySide)
        {
            var groundRoot = CreateChild("00_Ground", parent).transform;
            groundRoot.localPosition = new Vector3(center.x, 0f, center.y);
            CreateGroundBox(
                "BattlefieldGround",
                groundRoot,
                displaySide,
                0.2f,
                deploymentMaterial,
                new Color(0.20f, 0.34f, 0.23f),
                true);
        }

        private static GameObject CreateGroundBox(
            string name,
            Transform parent,
            float side,
            float height,
            Material material,
            Color color,
            bool keepCollider)
        {
            var ground = CreatePrimitive(name, parent);
            ground.transform.localPosition = new Vector3(0f, -height * 0.5f, 0f);
            ground.transform.localScale = new Vector3(side, height, side);
            ApplyMaterial(ground.GetComponent<Renderer>(), material, color);
            if (!keepCollider)
            {
                DisableAndDestroy(ground.GetComponent<Collider>());
            }

            return ground;
        }

        private static void CreateVisualBox(
            string name,
            Transform parent,
            float width,
            float height,
            float depth,
            float centerY,
            Material material,
            Color color)
        {
            var visual = CreatePrimitive(name, parent);
            visual.transform.localPosition = new Vector3(0f, centerY, 0f);
            visual.transform.localScale = new Vector3(width, height, depth);
            ApplyMaterial(visual.GetComponent<Renderer>(), material, color);
            DisableAndDestroy(visual.GetComponent<Collider>());
        }

        private static GameObject CreatePrimitive(string name, Transform parent)
        {
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            return primitive;
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void DisableAndDestroy(Collider targetCollider)
        {
            if (targetCollider == null)
            {
                return;
            }

            targetCollider.enabled = false; // 같은 프레임 NavMesh 수집에서 제외한다
            Destroy(targetCollider);
        }

        private static void ApplyMaterial(Renderer targetRenderer, Material material, Color color)
        {
            if (targetRenderer == null)
            {
                return;
            }

            targetRenderer.sharedMaterial = material;
            targetRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            targetRenderer.receiveShadows = true;
            var block = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            targetRenderer.SetPropertyBlock(block);
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

        private static float ResolveHeight(CastlePlacementData placement, float targetCellSize)
        {
            switch (placement.Kind)
            {
                case CastlePlacementKind.Wall:
                    return targetCellSize * Mathf.Lerp(1.05f, 1.55f, Mathf.InverseLerp(1f, 5f, placement.WallTier));
                case CastlePlacementKind.Palace:
                    return targetCellSize * 4.2f;
                case CastlePlacementKind.Defender:
                    return targetCellSize * 1.9f;
                case CastlePlacementKind.DefenseBuilding:
                    return targetCellSize * 2.8f;
                case CastlePlacementKind.LootBuilding:
                    return targetCellSize * 2.25f;
                default:
                    return targetCellSize * 2.4f;
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

        private Material ResolveMaterial(CastlePlacementKind kind)
        {
            switch (kind)
            {
                case CastlePlacementKind.Wall:
                    return wallMaterial;
                case CastlePlacementKind.Palace:
                    return palaceMaterial;
                default:
                    return buildingMaterial;
            }
        }

        private static RectInt ResolveSquareDisplayBounds(CastleGenerationCandidate candidate)
        {
            if (candidate.Placements.Count == 0)
            {
                var fallbackSize = Mathf.Min(candidate.GridWidth, candidate.GridHeight);
                return new RectInt(
                    (candidate.GridWidth - fallbackSize) / 2,
                    (candidate.GridHeight - fallbackSize) / 2,
                    fallbackSize,
                    fallbackSize);
            }

            var minimumX = candidate.GridWidth;
            var minimumZ = candidate.GridHeight;
            var maximumX = 0;
            var maximumZ = 0;
            foreach (var placement in candidate.Placements)
            {
                minimumX = Mathf.Min(minimumX, placement.X);
                minimumZ = Mathf.Min(minimumZ, placement.Z);
                maximumX = Mathf.Max(maximumX, placement.X + placement.Width);
                maximumZ = Mathf.Max(maximumZ, placement.Z + placement.Height);
            }

            minimumX = Mathf.Max(0, minimumX - DisplayMarginCells);
            minimumZ = Mathf.Max(0, minimumZ - DisplayMarginCells);
            maximumX = Mathf.Min(candidate.GridWidth, maximumX + DisplayMarginCells);
            maximumZ = Mathf.Min(candidate.GridHeight, maximumZ + DisplayMarginCells);
            var sideLength = Mathf.Max(maximumX - minimumX, maximumZ - minimumZ);
            sideLength = Mathf.Min(sideLength, Mathf.Min(candidate.GridWidth, candidate.GridHeight));
            ExpandAxisToSize(ref minimumX, ref maximumX, sideLength, candidate.GridWidth);
            ExpandAxisToSize(ref minimumZ, ref maximumZ, sideLength, candidate.GridHeight);
            return new RectInt(minimumX, minimumZ, sideLength, sideLength);
        }

        private static void ExpandAxisToSize(ref int minimum, ref int maximum, int targetSize, int limit)
        {
            var missing = targetSize - (maximum - minimum);
            minimum -= missing / 2;
            maximum += missing - missing / 2;
            if (minimum < 0)
            {
                maximum -= minimum;
                minimum = 0;
            }

            if (maximum > limit)
            {
                minimum -= maximum - limit;
                maximum = limit;
            }

            minimum = Mathf.Max(0, minimum);
        }

        private static Vector2 ResolvePlacementCenter(
            CastleGenerationCandidate candidate,
            CastlePlacementData placement,
            float targetCellSize)
        {
            return new Vector2(
                (placement.X + placement.Width * 0.5f) * targetCellSize - candidate.GridWidth * targetCellSize * 0.5f,
                (placement.Z + placement.Height * 0.5f) * targetCellSize - candidate.GridHeight * targetCellSize * 0.5f);
        }

        private static Vector2 ResolveDisplayCenter(
            CastleGenerationCandidate candidate,
            RectInt displayBounds,
            float targetCellSize)
        {
            return new Vector2(
                (displayBounds.xMin + displayBounds.width * 0.5f) * targetCellSize - candidate.GridWidth * targetCellSize * 0.5f,
                (displayBounds.yMin + displayBounds.height * 0.5f) * targetCellSize - candidate.GridHeight * targetCellSize * 0.5f);
        }

        private static string ResolveThemeLabel(CastleLayoutTheme theme)
        {
            switch (theme)
            {
                case CastleLayoutTheme.CentralCompartmentFortress:
                    return "A 중앙 격실";
                case CastleLayoutTheme.DiamondRadialFortress:
                    return "B 마름모 방사형";
                case CastleLayoutTheme.HoneycombCompartmentFortress:
                    return "C 복합 사각 격실";
                case CastleLayoutTheme.HexHoneycombFortress:
                    return "D 육각 벌집";
                case CastleLayoutTheme.PetalBloomFortress:
                    return "E 꽃잎 군락";
                case CastleLayoutTheme.CrystalMandalaFortress:
                    return "F 수정 만다라";
                case CastleLayoutTheme.TwinSpiralFortress:
                    return "G 쌍나선";
                case CastleLayoutTheme.FractalBastionFortress:
                    return "H 프랙탈 능보";
                case CastleLayoutTheme.VoronoiCrystalFortress:
                    return "I 보로노이 수정군";
                case CastleLayoutTheme.IrisShutterFortress:
                    return "J 홍채 셔터";
                default:
                    return theme.ToString();
            }
        }

        private void ValidateReferences()
        {
            if (rules == null || raidController == null || cameraController == null || worldRoot == null ||
                deploymentMaterial == null || wallMaterial == null || buildingMaterial == null || palaceMaterial == null)
            {
                throw new InvalidOperationException("Castle Raid 런타임 생성기 참조가 완성되지 않았습니다.");
            }
        }

        private void OnDestroy()
        {
            if (currentStageRoot != null)
            {
                Destroy(currentStageRoot);
            }

            fallbackStage?.SetActive(true);
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            CastleGenerationRules generationRules,
            CastleRaidController controller,
            CastleRaidCameraController raidCamera,
            Transform generatedWorldRoot,
            GameObject fixedFallbackStage,
            Material deployment,
            Material wall,
            Material building,
            Material palace)
        {
            rules = generationRules;
            raidController = controller;
            cameraController = raidCamera;
            worldRoot = generatedWorldRoot;
            fallbackStage = fixedFallbackStage;
            deploymentMaterial = deployment;
            wallMaterial = wall;
            buildingMaterial = building;
            palaceMaterial = palace;
        }
#endif
    }
}
