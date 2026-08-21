using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Contents.CastleRaid.Generation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProjectMT.EditorTools.CastleBake
{
    public enum CastleScenePreviewColorMode
    {
        Architecture,
        Analysis
    }

    internal sealed class CastleGenerationScenePreviewState : MonoBehaviour
    {
        [SerializeField] private GameObject hiddenStage;
        [SerializeField] private bool hiddenStageWasActive;
        [SerializeField] private bool hiddenStageHadActiveOverride;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private float previousOrthographicSize;
        [SerializeField] private Vector3 previousCameraPosition;
        [SerializeField] private bool restored;

        public void HideStage(GameObject stage)
        {
            hiddenStage = stage;
            hiddenStageWasActive = stage != null && stage.activeSelf;
            hiddenStageHadActiveOverride = HasActiveOverride(stage);
            restored = false;
            if (hiddenStageWasActive)
            {
                stage.SetActive(false);
            }
        }

        public void RestoreStage()
        {
            if (restored)
            {
                return;
            }

            restored = true;
            if (hiddenStage != null && hiddenStage.activeSelf != hiddenStageWasActive)
            {
                hiddenStage.SetActive(hiddenStageWasActive);
            }

            if (hiddenStage != null && !hiddenStageHadActiveOverride)
            {
                RevertActiveOverride(hiddenStage);
            }

            if (previewCamera != null && previewCamera.orthographic)
            {
                previewCamera.orthographicSize = previousOrthographicSize;
                previewCamera.transform.position = previousCameraPosition;
            }
        }

        public void FrameCamera(Camera targetCamera, Vector3 groundCenter, float orthographicSize)
        {
            previewCamera = targetCamera;
            if (previewCamera == null || !previewCamera.orthographic)
            {
                return;
            }

            previousOrthographicSize = previewCamera.orthographicSize;
            previousCameraPosition = previewCamera.transform.position;
            MoveGroundCenterTo(previewCamera, groundCenter);
            previewCamera.orthographicSize = Mathf.Max(1f, orthographicSize);
        }

        private void OnDestroy()
        {
            RestoreStage();
        }

        private static bool HasActiveOverride(GameObject stage)
        {
            if (stage == null || !PrefabUtility.IsPartOfPrefabInstance(stage))
            {
                return false;
            }

            var serializedStage = new SerializedObject(stage);
            return serializedStage.FindProperty("m_IsActive")?.prefabOverride == true;
        }

        private static void RevertActiveOverride(GameObject stage)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(stage))
            {
                return;
            }

            var serializedStage = new SerializedObject(stage);
            var activeProperty = serializedStage.FindProperty("m_IsActive");
            if (activeProperty?.prefabOverride == true)
            {
                PrefabUtility.RevertPropertyOverride(activeProperty, InteractionMode.AutomatedAction);
            }
        }

        private static void MoveGroundCenterTo(Camera targetCamera, Vector3 destination)
        {
            var forward = targetCamera.transform.forward;
            if (Mathf.Abs(forward.y) < 0.001f)
            {
                return;
            }

            var distance = (destination.y - targetCamera.transform.position.y) / forward.y;
            var currentCenter = targetCamera.transform.position + forward * distance;
            targetCamera.transform.position += new Vector3(
                destination.x - currentCenter.x,
                0f,
                destination.z - currentCenter.z);
        }
    }

    [InitializeOnLoad]
    public static class CastleGenerationScenePreview // 저장하지 않는 3D 후보 시각화
    {
        public const string PreviewRootName = "__CastleGeneration3DPreview";
        public const float DefaultCellSize = 1f;
        public const int PreviewGroundMarginCells = CastleSpatialContract.DeploymentMargin;

        public static readonly Vector3 DefaultWorldOffset = Vector3.zero;

        private const float FloorGapRatio = 0.02f;
        private const string ExistingStagePath = "01_WorldRoot/CastleStage_Seed";
        private const string CastleCameraPath = "03_CameraRoot/CastleRaidCamera";
        private const float FullMapCameraSizePerWorldUnit = 11.5f / 20f;
        private const float DefaultVisibleMapRatio = 0.72f;
        private const float CameraSizePerWorldUnit = FullMapCameraSizePerWorldUnit * DefaultVisibleMapRatio;

        private static readonly HideFlags PreviewFlags =
            HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

        static CastleGenerationScenePreview()
        {
            EditorSceneManager.sceneSaving += HandleSceneSaving;
            EditorSceneManager.sceneClosing += HandleSceneClosing;
            EditorSceneManager.activeSceneChangedInEditMode += HandleActiveSceneChangedInEditMode;
            AssemblyReloadEvents.beforeAssemblyReload += ClearAllOpenScenes;
            EditorApplication.quitting += ClearAllOpenScenes;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        public static GameObject Rebuild(
            CastleGenerationCandidate candidate,
            Vector3 worldOffset,
            bool focusSceneView = true)
        {
            return Rebuild(
                candidate,
                SceneManager.GetActiveScene(),
                worldOffset,
                DefaultCellSize,
                CastleScenePreviewColorMode.Architecture,
                focusSceneView);
        }

        public static GameObject Rebuild(
            CastleGenerationCandidate candidate,
            Vector3 worldOffset,
            float cellSize,
            CastleScenePreviewColorMode colorMode,
            bool focusSceneView = true)
        {
            return Rebuild(
                candidate,
                SceneManager.GetActiveScene(),
                worldOffset,
                cellSize,
                colorMode,
                focusSceneView);
        }

        public static GameObject Rebuild(
            CastleGenerationCandidate candidate,
            Scene targetScene,
            Vector3 worldOffset,
            bool focusSceneView = false)
        {
            return Rebuild(
                candidate,
                targetScene,
                worldOffset,
                DefaultCellSize,
                CastleScenePreviewColorMode.Architecture,
                focusSceneView);
        }

        public static GameObject Rebuild(
            CastleGenerationCandidate candidate,
            Scene targetScene,
            Vector3 worldOffset,
            float cellSize,
            CastleScenePreviewColorMode colorMode,
            bool focusSceneView = false)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (!targetScene.IsValid() || !targetScene.isLoaded)
            {
                throw new InvalidOperationException("3D 프리뷰를 만들 대상 Scene이 열려 있지 않습니다.");
            }

            cellSize = Mathf.Clamp(cellSize, 0.1f, 2f);
            Clear(targetScene);

            var root = new GameObject(PreviewRootName)
            {
                hideFlags = PreviewFlags
            };
            SceneManager.MoveGameObjectToScene(root, targetScene);
            root.transform.position = worldOffset;
            var previewState = root.AddComponent<CastleGenerationScenePreviewState>();
            previewState.HideStage(FindExistingStage(targetScene));
            var displayBounds = ResolveSquareDisplayBounds(candidate);

            var baseRoot = CreateChild("00_Base", root.transform);
            var floorRoot = CreateChild("01_Floor", root.transform);
            var wallRoot = CreateChild("02_Walls", root.transform);
            var structureRoot = CreateChild("03_Structures", root.transform);

            BuildBase(candidate, displayBounds, baseRoot.transform, cellSize, colorMode);
            BuildFloors(candidate, displayBounds, floorRoot.transform, cellSize, colorMode);
            BuildPlacements(candidate, wallRoot.transform, structureRoot.transform, cellSize, colorMode);
            var localGroundCenter = ResolveDisplayCenter(candidate, displayBounds, cellSize);
            previewState.FrameCamera(
                FindCastleCamera(targetScene),
                root.transform.TransformPoint(new Vector3(localGroundCenter.x, 0f, localGroundCenter.y)),
                ResolvePreviewCameraSize(candidate, cellSize));

            if (focusSceneView)
            {
                Selection.activeGameObject = root;
                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.FrameSelected(false);
                }

                Selection.objects = Array.Empty<UnityEngine.Object>();
                SceneView.RepaintAll();
            }

            return root;
        }

        public static RectInt ResolveSquareDisplayBounds(CastleGenerationCandidate candidate)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (candidate.Placements == null || candidate.Placements.Count == 0)
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

            minimumX = Mathf.Max(0, minimumX - PreviewGroundMarginCells);
            minimumZ = Mathf.Max(0, minimumZ - PreviewGroundMarginCells);
            maximumX = Mathf.Min(candidate.GridWidth, maximumX + PreviewGroundMarginCells);
            maximumZ = Mathf.Min(candidate.GridHeight, maximumZ + PreviewGroundMarginCells);

            var sideLength = Mathf.Max(maximumX - minimumX, maximumZ - minimumZ);
            sideLength = Mathf.Min(sideLength, Mathf.Min(candidate.GridWidth, candidate.GridHeight));
            ExpandAxisToSize(ref minimumX, ref maximumX, sideLength, candidate.GridWidth);
            ExpandAxisToSize(ref minimumZ, ref maximumZ, sideLength, candidate.GridHeight);
            return new RectInt(minimumX, minimumZ, sideLength, sideLength);
        }

        public static bool IsPreviewDeploymentCell(Vector2Int cell, RectInt displayBounds)
        {
            if (!displayBounds.Contains(cell))
            {
                return false;
            }

            var margin = PreviewGroundMarginCells;
            return cell.x < displayBounds.xMin + margin ||
                   cell.x >= displayBounds.xMax - margin ||
                   cell.y < displayBounds.yMin + margin ||
                   cell.y >= displayBounds.yMax - margin;
        }

        public static float ResolvePreviewCameraSize(
            CastleGenerationCandidate candidate,
            float cellSize = DefaultCellSize)
        {
            return ResolveSquareDisplayBounds(candidate).width *
                   Mathf.Clamp(cellSize, 0.1f, 2f) *
                   CameraSizePerWorldUnit;
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

            var ownedRoots = new HashSet<GameObject>();
            foreach (var sceneRoot in scene.GetRootGameObjects())
            {
                foreach (var transform in sceneRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name == PreviewRootName ||
                        transform.GetComponent<CastleGenerationScenePreviewState>() != null)
                    {
                        ownedRoots.Add(transform.gameObject);
                    }
                }
            }

            var roots = ownedRoots
                .Where(candidate => candidate != null && !ownedRoots.Any(other =>
                    other != null && other != candidate && candidate.transform.IsChildOf(other.transform)))
                .ToArray();
            foreach (var root in roots)
            {
                root.GetComponent<CastleGenerationScenePreviewState>()?.RestoreStage();
                DestroyPreviewRoot(root);
            }

            if (roots.Length > 0)
            {
                SceneView.RepaintAll();
            }

            return roots.Length;
        }

        private static GameObject FindExistingStage(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != "00_SceneRoot")
                {
                    continue;
                }

                var stage = root.transform.Find(ExistingStagePath);
                return stage != null ? stage.gameObject : null;
            }

            return null;
        }

        private static Camera FindCastleCamera(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != "00_SceneRoot")
                {
                    continue;
                }

                var cameraTransform = root.transform.Find(CastleCameraPath);
                return cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            }

            return null;
        }

        private static void HandleSceneSaving(Scene scene, string path)
        {
            Clear(scene); // 실제 Scene에는 기존 성의 활성 상태만 저장한다
        }

        private static void HandleSceneClosing(Scene scene, bool removingScene)
        {
            Clear(scene); // Scene이 닫히기 전에 임시 프리뷰 상태를 복원한다
        }

        private static void HandleActiveSceneChangedInEditMode(Scene previousScene, Scene nextScene)
        {
            Clear(previousScene); // 다른 Scene으로 이동하면 이전 Scene의 프리뷰를 남기지 않는다
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                ClearAllOpenScenes();
            }
        }

        private static void ClearAllOpenScenes()
        {
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                Clear(SceneManager.GetSceneAt(index));
            }
        }

        private static void BuildBase(
            CastleGenerationCandidate candidate,
            RectInt displayBounds,
            Transform parent,
            float cellSize,
            CastleScenePreviewColorMode colorMode)
        {
            var draft = new MeshDraft("Base");
            var minimumX = displayBounds.xMin * cellSize - candidate.GridWidth * cellSize * 0.5f;
            var maximumX = displayBounds.xMax * cellSize - candidate.GridWidth * cellSize * 0.5f;
            var minimumZ = displayBounds.yMin * cellSize - candidate.GridHeight * cellSize * 0.5f;
            var maximumZ = displayBounds.yMax * cellSize - candidate.GridHeight * cellSize * 0.5f;
            AddBox(
                draft,
                minimumX,
                maximumX,
                -0.12f,
                -0.02f,
                minimumZ,
                maximumZ);
            var baseColor = ResolveFloorColor(
                new Vector2Int(-1, -1),
                null,
                displayBounds,
                colorMode) * 0.42f;
            baseColor.a = 1f;
            CreateMeshObject(
                parent,
                draft,
                baseColor);
        }

        private static void BuildFloors(
            CastleGenerationCandidate candidate,
            RectInt displayBounds,
            Transform parent,
            float cellSize,
            CastleScenePreviewColorMode colorMode)
        {
            var roles = BuildCompartmentCells(candidate);
            var groups = new Dictionary<Color32, MeshDraft>();
            for (var x = displayBounds.xMin; x < displayBounds.xMax; x++)
            {
                for (var z = displayBounds.yMin; z < displayBounds.yMax; z++)
                {
                    var color = ResolveFloorColor(
                        new Vector2Int(x, z),
                        roles[x, z],
                        displayBounds,
                        colorMode);
                    var draft = ResolveDraft(groups, color, "Floor");
                    var center = ResolveCellCenter(candidate, x, z, cellSize);
                    var halfSize = cellSize * (1f - FloorGapRatio) * 0.5f;
                    AddTopFace(
                        draft,
                        center.x - halfSize,
                        center.x + halfSize,
                        0f,
                        center.y - halfSize,
                        center.y + halfSize);
                }
            }

            CreateMeshGroups(parent, groups);
        }

        private static Vector2 ResolveDisplayCenter(
            CastleGenerationCandidate candidate,
            RectInt displayBounds,
            float cellSize)
        {
            return new Vector2(
                (displayBounds.xMin + displayBounds.width * 0.5f) * cellSize -
                candidate.GridWidth * cellSize * 0.5f,
                (displayBounds.yMin + displayBounds.height * 0.5f) * cellSize -
                candidate.GridHeight * cellSize * 0.5f);
        }

        private static void ExpandAxisToSize(
            ref int minimum,
            ref int maximum,
            int targetSize,
            int limit)
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

        private static void BuildPlacements(
            CastleGenerationCandidate candidate,
            Transform wallParent,
            Transform structureParent,
            float cellSize,
            CastleScenePreviewColorMode colorMode)
        {
            var walls = new Dictionary<Color32, MeshDraft>();
            var structures = new Dictionary<Color32, MeshDraft>();
            var mandatory = new HashSet<string>(
                candidate.Difficulty.MandatoryPlacementIds,
                StringComparer.Ordinal);

            foreach (var placement in candidate.Placements)
            {
                var color = ResolvePlacementColor(placement, mandatory, colorMode);
                var isWall = placement.Kind == CastlePlacementKind.Wall;
                var draft = ResolveDraft(isWall ? walls : structures, color, isWall ? "Wall" : "Structure");
                AddPlacement(candidate, placement, draft, cellSize);
            }

            CreateMeshGroups(wallParent, walls);
            CreateMeshGroups(structureParent, structures);
        }

        private static void AddPlacement(
            CastleGenerationCandidate candidate,
            CastlePlacementData placement,
            MeshDraft draft,
            float cellSize)
        {
            var centerX = (placement.X + placement.Width * 0.5f) * cellSize
                          - candidate.GridWidth * cellSize * 0.5f;
            var centerZ = (placement.Z + placement.Height * 0.5f) * cellSize
                          - candidate.GridHeight * cellSize * 0.5f;
            var inset = cellSize * ResolvePlacementInsetRatio(placement.Kind);
            var minimumHalfSize = ResolveMinimumHalfSize(placement.Kind);
            var halfWidth = Mathf.Max(minimumHalfSize, placement.Width * cellSize * 0.5f - inset);
            var halfHeight = Mathf.Max(minimumHalfSize, placement.Height * cellSize * 0.5f - inset);

            if (placement.Kind == CastlePlacementKind.Wall)
            {
                var height = ResolveWallHeight(placement);
                var capStart = Mathf.Max(0.45f, height - 0.12f);
                AddBox(
                    draft,
                    centerX - halfWidth,
                    centerX + halfWidth,
                    0.03f,
                    capStart,
                    centerZ - halfHeight,
                    centerZ + halfHeight);
                var capScale = IsWallJoint(placement.WallNeighborMask) ? 1.11f : 1.04f;
                AddUpperBlock(
                    draft,
                    centerX,
                    centerZ,
                    halfWidth * capScale,
                    halfHeight * capScale,
                    capStart,
                    height + (capScale > 1.05f ? 0.07f : 0f));
                return;
            }

            switch (placement.Kind)
            {
                case CastlePlacementKind.Palace:
                    AddUpperBlock(draft, centerX, centerZ, halfWidth, halfHeight, 0.03f, 2f);
                    AddUpperBlock(draft, centerX, centerZ, halfWidth * 0.72f, halfHeight * 0.72f, 2f, 3.2f);
                    AddUpperBlock(draft, centerX, centerZ, halfWidth * 0.42f, halfHeight * 0.42f, 3.2f, 4.2f);
                    break;
                case CastlePlacementKind.DefenseBuilding:
                    AddUpperBlock(draft, centerX, centerZ, halfWidth, halfHeight, 0.03f, 1.65f);
                    AddUpperBlock(draft, centerX, centerZ, halfWidth * 0.48f, halfHeight * 0.48f, 1.65f, 2.6f);
                    break;
                case CastlePlacementKind.Defender:
                    AddUpperBlock(draft, centerX, centerZ, halfWidth, halfHeight, 0.03f, 0.9f);
                    AddUpperBlock(draft, centerX, centerZ, halfWidth * 0.62f, halfHeight * 0.62f, 0.9f, 1.35f);
                    break;
                case CastlePlacementKind.LootBuilding:
                    AddUpperBlock(draft, centerX, centerZ, halfWidth, halfHeight, 0.03f, 1.35f);
                    AddUpperBlock(draft, centerX, centerZ, halfWidth * 0.82f, halfHeight * 0.82f, 1.35f, 2f);
                    break;
                default:
                    AddUpperBlock(draft, centerX, centerZ, halfWidth, halfHeight, 0.03f, 1.35f);
                    AddUpperBlock(draft, centerX, centerZ, halfWidth * 0.72f, halfHeight * 0.72f, 1.35f, 2f);
                    break;
            }
        }

        private static void AddUpperBlock(
            MeshDraft draft,
            float centerX,
            float centerZ,
            float halfWidth,
            float halfHeight,
            float minimumY,
            float maximumY)
        {
            AddBox(
                draft,
                centerX - halfWidth,
                centerX + halfWidth,
                minimumY,
                maximumY,
                centerZ - halfHeight,
                centerZ + halfHeight);
        }

        private static float ResolvePlacementInsetRatio(CastlePlacementKind kind)
        {
            switch (kind)
            {
                case CastlePlacementKind.Wall:
                    return 0.01f;
                case CastlePlacementKind.Defender:
                    return 0.08f;
                default:
                    return 0.06f;
            }
        }

        private static float ResolveMinimumHalfSize(CastlePlacementKind kind)
        {
            switch (kind)
            {
                case CastlePlacementKind.Palace:
                    return 1.05f;
                case CastlePlacementKind.DefenseBuilding:
                    return 0.45f;
                case CastlePlacementKind.Defender:
                    return 0.28f;
                case CastlePlacementKind.LootBuilding:
                    return 0.5f;
                case CastlePlacementKind.Building:
                    return 0.45f;
                default:
                    return 0.08f;
            }
        }

        private static float ResolveWallHeight(CastlePlacementData placement)
        {
            return Mathf.Clamp(
                0.9f + placement.WallTier * 0.08f + placement.WallDefenseLayer * 0.1f,
                1.1f,
                1.55f);
        }

        private static bool IsWallJoint(CastleWallNeighborMask mask)
        {
            return mask != (CastleWallNeighborMask.North | CastleWallNeighborMask.South) &&
                   mask != (CastleWallNeighborMask.East | CastleWallNeighborMask.West);
        }

        private static Vector2 ResolveCellCenter(
            CastleGenerationCandidate candidate,
            int x,
            int z,
            float cellSize)
        {
            return new Vector2(
                (x + 0.5f) * cellSize - candidate.GridWidth * cellSize * 0.5f,
                (z + 0.5f) * cellSize - candidate.GridHeight * cellSize * 0.5f);
        }

        private static Color ResolveFloorColor(
            Vector2Int cell,
            CastleCompartmentRole? role,
            RectInt displayBounds,
            CastleScenePreviewColorMode colorMode)
        {
            if (IsPreviewDeploymentCell(cell, displayBounds))
            {
                return colorMode == CastleScenePreviewColorMode.Analysis
                    ? CastleGenerationPreviewExporter.DeploymentMarginColor
                    : new Color(0.12f, 0.22f, 0.18f);
            }

            if (colorMode == CastleScenePreviewColorMode.Analysis)
            {
                return CastleGenerationPreviewExporter.ResolveFloorColor(cell, role);
            }

            if (role.HasValue)
            {
                switch (role.Value)
                {
                    case CastleCompartmentRole.PalaceCore:
                        return new Color(0.28f, 0.24f, 0.14f);
                    case CastleCompartmentRole.InnerRing:
                        return new Color(0.22f, 0.34f, 0.23f);
                    case CastleCompartmentRole.OuterRing:
                        return new Color(0.20f, 0.29f, 0.25f);
                }
            }

            return CastleSpatialContract.BuildableBounds.Contains(cell)
                ? new Color(0.20f, 0.34f, 0.23f)
                : new Color(0.12f, 0.22f, 0.18f);
        }

        private static Color ResolvePlacementColor(
            CastlePlacementData placement,
            ISet<string> mandatory,
            CastleScenePreviewColorMode colorMode)
        {
            if (colorMode == CastleScenePreviewColorMode.Analysis)
            {
                return CastleGenerationPreviewExporter.ResolvePlacementColor(placement, mandatory);
            }

            switch (placement.Kind)
            {
                case CastlePlacementKind.Wall:
                    return ResolveArchitectureWallColor(placement);
                case CastlePlacementKind.Palace:
                    return new Color(0.78f, 0.54f, 0.14f);
                case CastlePlacementKind.Building:
                    return new Color(0.43f, 0.49f, 0.54f);
                case CastlePlacementKind.DefenseBuilding:
                    return new Color(0.60f, 0.22f, 0.16f);
                case CastlePlacementKind.Defender:
                    return new Color(0.40f, 0.27f, 0.52f);
                case CastlePlacementKind.LootBuilding:
                    switch (placement.LootKind)
                    {
                        case CastleLootKind.Gold:
                            return new Color(0.84f, 0.68f, 0.18f);
                        case CastleLootKind.Equipment:
                            return new Color(0.18f, 0.58f, 0.56f);
                        case CastleLootKind.Key:
                            return new Color(0.22f, 0.38f, 0.70f);
                    }

                    break;
            }

            return CastleGenerationPreviewExporter.InvalidDataColor;
        }

        private static Color ResolveArchitectureWallColor(CastlePlacementData placement)
        {
            Color baseColor;
            switch (placement.WallBand)
            {
                case CastleWallBand.OuterPerimeter:
                    baseColor = new Color(0.43f, 0.34f, 0.25f);
                    break;
                case CastleWallBand.InnerDefense:
                    baseColor = new Color(0.53f, 0.51f, 0.45f);
                    break;
                case CastleWallBand.CoreDefense:
                    baseColor = new Color(0.66f, 0.54f, 0.27f);
                    break;
                case CastleWallBand.Partition:
                    baseColor = new Color(0.37f, 0.42f, 0.46f);
                    break;
                default:
                    return CastleGenerationPreviewExporter.InvalidDataColor;
            }

            var light = Mathf.InverseLerp(1f, 5f, placement.WallTier) * 0.13f +
                        Mathf.Clamp01(placement.WallDefenseLayer / 3f) * 0.07f;
            return Color.Lerp(baseColor, Color.white, light);
        }

        private static CastleCompartmentRole?[,] BuildCompartmentCells(CastleGenerationCandidate candidate)
        {
            var result = new CastleCompartmentRole?[candidate.GridWidth, candidate.GridHeight];
            foreach (var compartment in candidate.Compartments)
            {
                foreach (var cell in compartment.EnumerateFootprintCells())
                {
                    if (cell.x >= 0 && cell.y >= 0 &&
                        cell.x < candidate.GridWidth && cell.y < candidate.GridHeight)
                    {
                        result[cell.x, cell.y] = compartment.Role;
                    }
                }
            }

            return result;
        }

        private static MeshDraft ResolveDraft(
            IDictionary<Color32, MeshDraft> groups,
            Color color,
            string label)
        {
            var key = (Color32)color;
            if (groups.TryGetValue(key, out var draft))
            {
                return draft;
            }

            draft = new MeshDraft(label);
            groups.Add(key, draft);
            return draft;
        }

        private static void CreateMeshGroups(Transform parent, IDictionary<Color32, MeshDraft> groups)
        {
            foreach (var pair in groups)
            {
                CreateMeshObject(parent, pair.Value, pair.Key);
            }
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            var child = new GameObject(name)
            {
                hideFlags = PreviewFlags
            };
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void CreateMeshObject(Transform parent, MeshDraft draft, Color color)
        {
            if (draft.Vertices.Count == 0)
            {
                return;
            }

            var colorCode = ColorUtility.ToHtmlStringRGB(color);
            var child = CreateChild($"{draft.Label}_{colorCode}", parent);
            var mesh = new Mesh
            {
                name = $"CastlePreview_{draft.Label}_{colorCode}",
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = draft.Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(draft.Vertices);
            mesh.SetTriangles(draft.Triangles, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateMaterial(color, colorCode);
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Material CreateMaterial(Color color, string colorCode)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("3D 프리뷰용 기본 Shader를 찾지 못했습니다.");
            }

            var material = new Material(shader)
            {
                name = $"CastlePreviewMat_{colorCode}",
                hideFlags = HideFlags.HideAndDontSave,
                enableInstancing = true
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.16f);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            return material;
        }

        private static void DestroyPreviewRoot(GameObject root)
        {
            if (Selection.activeGameObject != null &&
                (Selection.activeGameObject == root || Selection.activeGameObject.transform.IsChildOf(root.transform)))
            {
                Selection.objects = Array.Empty<UnityEngine.Object>(); // Reload 때 Inspector가 파괴된 프리뷰를 잡지 않게 한다
            }

            var meshes = new HashSet<Mesh>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null)
                {
                    meshes.Add(filter.sharedMesh);
                }
            }

            var materials = new HashSet<Material>();
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null)
                    {
                        materials.Add(material);
                    }
                }
            }

            UnityEngine.Object.DestroyImmediate(root);
            foreach (var mesh in meshes)
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }

            foreach (var material in materials)
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static void AddTopFace(
            MeshDraft draft,
            float minimumX,
            float maximumX,
            float y,
            float minimumZ,
            float maximumZ)
        {
            AddFace(
                draft,
                new Vector3(minimumX, y, minimumZ),
                new Vector3(minimumX, y, maximumZ),
                new Vector3(maximumX, y, maximumZ),
                new Vector3(maximumX, y, minimumZ));
        }

        private static void AddBox(
            MeshDraft draft,
            float minimumX,
            float maximumX,
            float minimumY,
            float maximumY,
            float minimumZ,
            float maximumZ)
        {
            AddTopFace(draft, minimumX, maximumX, maximumY, minimumZ, maximumZ);
            AddFace(
                draft,
                new Vector3(minimumX, minimumY, minimumZ),
                new Vector3(maximumX, minimumY, minimumZ),
                new Vector3(maximumX, minimumY, maximumZ),
                new Vector3(minimumX, minimumY, maximumZ));
            AddFace(
                draft,
                new Vector3(minimumX, minimumY, maximumZ),
                new Vector3(maximumX, minimumY, maximumZ),
                new Vector3(maximumX, maximumY, maximumZ),
                new Vector3(minimumX, maximumY, maximumZ));
            AddFace(
                draft,
                new Vector3(maximumX, minimumY, minimumZ),
                new Vector3(minimumX, minimumY, minimumZ),
                new Vector3(minimumX, maximumY, minimumZ),
                new Vector3(maximumX, maximumY, minimumZ));
            AddFace(
                draft,
                new Vector3(maximumX, minimumY, maximumZ),
                new Vector3(maximumX, minimumY, minimumZ),
                new Vector3(maximumX, maximumY, minimumZ),
                new Vector3(maximumX, maximumY, maximumZ));
            AddFace(
                draft,
                new Vector3(minimumX, minimumY, minimumZ),
                new Vector3(minimumX, minimumY, maximumZ),
                new Vector3(minimumX, maximumY, maximumZ),
                new Vector3(minimumX, maximumY, minimumZ));
        }

        private static void AddFace(
            MeshDraft draft,
            Vector3 first,
            Vector3 second,
            Vector3 third,
            Vector3 fourth)
        {
            var start = draft.Vertices.Count;
            draft.Vertices.Add(first);
            draft.Vertices.Add(second);
            draft.Vertices.Add(third);
            draft.Vertices.Add(fourth);
            draft.Triangles.Add(start);
            draft.Triangles.Add(start + 1);
            draft.Triangles.Add(start + 2);
            draft.Triangles.Add(start);
            draft.Triangles.Add(start + 2);
            draft.Triangles.Add(start + 3);
        }

        private sealed class MeshDraft
        {
            public MeshDraft(string label)
            {
                Label = label;
            }

            public string Label { get; }
            public List<Vector3> Vertices { get; } = new List<Vector3>();
            public List<int> Triangles { get; } = new List<int>();
        }
    }
}
