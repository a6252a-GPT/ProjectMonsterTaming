using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ProjectMT.Tools.StageMapSlicer;

namespace ProjectMT.EditorTools.StageMapSlicer
{
    public sealed class StageMapSlicerWindow : EditorWindow
    {
        private const string DefaultOutputRoot = "Assets/ProjectMT/98_Generated/Stages";
        private const int PreviewDrawLimit = 64;
        private static readonly string[] SmallVegetationKeywords =
        {
            "grass",
            "flower",
            "fern",
            "weed",
            "reed",
            "clover",
            "herb",
            "groundcover",
            "ground_cover",
            "ground cover",
            "plant_",
            "_plant",
            "잔디",
            "풀",
            "꽃"
        };

        private enum SliceShape
        {
            Rectangle,
            Hexagon
        }

        [SerializeField] private string stageName = "Stage_01";
        [SerializeField] private string outputRoot = DefaultOutputRoot;
        [SerializeField] private float outputScale = 1f;
        [SerializeField] private Vector2 centerXZ;
        [SerializeField] private Vector2 sizeXZ = new Vector2(100f, 100f);
        [SerializeField] private SliceShape sliceShape = SliceShape.Rectangle;
        [SerializeField] private float rotationY;
        [SerializeField] private bool editBoundsInScene = true;
        [SerializeField] private bool includeInactive = true;
        [SerializeField] private bool includeLighting = true;
        [SerializeField] private bool disableDecorationColliders = true;
        [SerializeField] private bool disableSmallVegetationShadows = true;
        [SerializeField] private bool enableRepeatedVegetationGpuInstancing = true;
        [SerializeField] private bool enableVegetationDistanceCulling;
        [SerializeField] private float vegetationCullDistance = 28f;
        [SerializeField] private float vegetationCullCellSize = 6f;
        [SerializeField] private bool enableTerrainDrawInstanced = true;
        [SerializeField] private bool overrideTerrainDistances;
        [SerializeField] private float terrainDetailDistance = 35f;
        [SerializeField] private float terrainTreeDistance = 100f;
        [SerializeField] private bool showGeneratedStages = true;

        private readonly BoxBoundsHandle boundsHandle = new BoxBoundsHandle();
        private readonly List<GameObject> previewRoots = new List<GameObject>();
        private readonly List<GameObject> previewEnvironmentRoots = new List<GameObject>();
        private readonly List<WaterCrop> previewWaterCrops = new List<WaterCrop>();
        private readonly List<Bounds> previewBounds = new List<Bounds>();
        private readonly List<RendererRecord> rendererCache = new List<RendererRecord>();
        private Vector2 scrollPosition;
        private string lastSummary = "영역을 지정한 뒤 미리보기를 실행하세요.";
        private int previewRendererCount;
        private int previewTerrainCount;
        private int previewLightingCount;
        private Terrain[] terrainCache = Array.Empty<Terrain>();
        private int cachedSceneHandle = -1;
        private bool sceneCacheDirty = true;

        [MenuItem("JC Tool/Map/Stage Map Slicer")]
        private static void OpenWindow()
        {
            StageMapSlicerWindow window = GetWindow<StageMapSlicerWindow>();
            window.titleContent = new GUIContent("Stage Map Slicer");
            window.minSize = new Vector2(420f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            boundsHandle.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Z;
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.hierarchyChanged += InvalidateSceneCache;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.hierarchyChanged -= InvalidateSceneCache;
        }

        private void OnGUI()
        {
            Scene scene = SceneManager.GetActiveScene();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField("Stage Map Slicer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "회전 가능한 사각형/육각형에 경계가 조금이라도 겹치는 프리팹 인스턴스는 연결을 유지한 채 통째로 포함하고, Terrain과 평면형 물·용암만 도형 경계로 잘라 바로 배치 가능한 단일 맵 프리팹을 생성합니다. 원본 씬은 수정하거나 저장하지 않습니다.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("현재 씬", scene.IsValid() ? scene.name : "없음");
                EditorGUILayout.TextField("씬 경로", scene.IsValid() ? scene.path : string.Empty);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("출력 설정", EditorStyles.boldLabel);
            stageName = EditorGUILayout.TextField("스테이지 이름", stageName);
            outputRoot = EditorGUILayout.TextField("출력 루트", outputRoot);
            EditorGUI.BeginChangeCheck();
            outputScale = EditorGUILayout.FloatField(
                new GUIContent("출력 배율", "절단 영역 안의 Terrain, 모델, 물을 이 배율로 축소해 저장합니다."),
                outputScale);
            if (EditorGUI.EndChangeCheck())
            {
                outputScale = Mathf.Max(0.01f, outputScale);
                ClearPreview();
                SceneView.RepaintAll();
            }

            DrawGeneratedStageList();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("절단 영역 (XZ)", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("도형");
            sliceShape = (SliceShape)GUILayout.Toolbar(
                (int)sliceShape,
                new[] { "사각형", "육각형" });
            centerXZ = EditorGUILayout.Vector2Field("중심 X / Z", centerXZ);
            sizeXZ = EditorGUILayout.Vector2Field("크기 X / Z", sizeXZ);
            rotationY = EditorGUILayout.Slider("회전 Y", rotationY, -180f, 180f);
            editBoundsInScene = EditorGUILayout.Toggle("Scene 이동/회전/크기 핸들", editBoundsInScene);
            includeInactive = EditorGUILayout.Toggle("비활성 모델 포함", includeInactive);
            includeLighting = EditorGUILayout.Toggle("조명/환경 설정 포함", includeLighting);
            if (EditorGUI.EndChangeCheck())
            {
                sizeXZ.x = Mathf.Max(0.1f, sizeXZ.x);
                sizeXZ.y = Mathf.Max(0.1f, sizeXZ.y);
                ClearPreview();
                SceneView.RepaintAll();
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Vector2Field("저장 결과 크기 X / Z", sizeXZ * Mathf.Max(0.01f, outputScale));
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("모바일 최적화 (생성 결과만)", EditorStyles.boldLabel);
            disableDecorationColliders = EditorGUILayout.Toggle(
                "장식 식생 Collider 비활성화",
                disableDecorationColliders);
            disableSmallVegetationShadows = EditorGUILayout.Toggle(
                "작은 식생 그림자 끄기",
                disableSmallVegetationShadows);
            enableRepeatedVegetationGpuInstancing = EditorGUILayout.Toggle(
                "반복 식생 실제 GPU Instancing",
                enableRepeatedVegetationGpuInstancing);
            enableVegetationDistanceCulling = EditorGUILayout.Toggle(
                "식생 거리 컬링",
                enableVegetationDistanceCulling);
            if (enableVegetationDistanceCulling)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    vegetationCullDistance = EditorGUILayout.FloatField("컬링 거리", vegetationCullDistance);
                    vegetationCullCellSize = EditorGUILayout.FloatField("컬링 셀 크기", vegetationCullCellSize);
                }
            }

            enableTerrainDrawInstanced = EditorGUILayout.Toggle(
                "Terrain Draw Instanced",
                enableTerrainDrawInstanced);
            overrideTerrainDistances = EditorGUILayout.Toggle(
                "Terrain 식생 거리 덮어쓰기",
                overrideTerrainDistances);
            if (overrideTerrainDistances)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    terrainDetailDistance = EditorGUILayout.FloatField("Detail 거리", terrainDetailDistance);
                    terrainTreeDistance = EditorGUILayout.FloatField("Tree 거리", terrainTreeDistance);
                }
            }

            vegetationCullDistance = Mathf.Max(1f, vegetationCullDistance);
            vegetationCullCellSize = Mathf.Max(1f, vegetationCullCellSize);
            terrainDetailDistance = Mathf.Max(0f, terrainDetailDistance);
            terrainTreeDistance = Mathf.Max(0f, terrainTreeDistance);
            EditorGUILayout.HelpBox(
                "풀/꽃 식별은 프리팹·오브젝트·Material 이름을 사용합니다. GPU Instancing은 스테이지 전용 Material과 런타임 적용 컴포넌트를 만들고, 해당 식생 Renderer만 SRP Batcher에서 제외해 실제 Instancing 경로를 사용합니다. 전역 SRP Batcher·원본 Shader·원본 Material은 수정하지 않으며, 프리팹 연결과 Terrain Detail/Tree 데이터는 항상 유지합니다.",
                MessageType.None);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("선택 오브젝트에 맞춤"))
            {
                FitBoundsToSelection();
            }

            if (GUILayout.Button("Terrain 전체에 맞춤"))
            {
                FitBoundsToTerrains(scene);
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("씬 오브젝트 캐시 새로고침"))
            {
                InvalidateSceneCache();
                lastSummary = "씬 캐시를 비웠습니다. 다음 미리보기에서 한 번 다시 수집합니다.";
                Repaint();
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("실행", EditorStyles.boldLabel);

            if (GUILayout.Button("1. 영역 미리보기", GUILayout.Height(34f)))
            {
                RefreshPreview(scene, true);
            }

            using (new EditorGUI.DisabledScope(!CanBuild(scene)))
            {
                GUI.backgroundColor = new Color(0.55f, 0.9f, 0.6f);
                if (GUILayout.Button("2. 단일 스테이지 프리팹 생성", GUILayout.Height(42f)))
                {
                    BuildStagePrefab(scene);
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("현재 결과", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(lastSummary, MessageType.None);

            if (previewRoots.Count > 0 || previewTerrainCount > 0 || previewWaterCrops.Count > 0 || previewLightingCount > 0)
            {
                EditorGUILayout.LabelField("선택 루트", previewRoots.Count.ToString("N0"));
                EditorGUILayout.LabelField("영역 교차 Renderer", previewRendererCount.ToString("N0"));
                EditorGUILayout.LabelField("잘릴 Terrain", previewTerrainCount.ToString("N0"));
                EditorGUILayout.LabelField("잘릴 물/용암 Plane", previewWaterCrops.Count.ToString("N0"));
                EditorGUILayout.LabelField("포함 조명/프로브", previewLightingCount.ToString("N0"));
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "일반 모델 프리팹은 Renderer 경계가 선택 영역에 닿으면 통째로 포함하며 자르거나 Unpack하지 않습니다. 원본 프리팹 연결과 오버라이드를 유지하며, Water 레이어 또는 물·용암 Material을 사용한 실제 수평 Plane만 선택 도형 경계로 별도 크롭합니다.",
                MessageType.Warning);
            EditorGUILayout.HelpBox(
                "생성 프리팹은 원본 씬(.unity)을 참조하지 않도록 검사합니다. 모델 Mesh/Material과 원본 모델 프리팹 등 프로젝트 에셋 의존성은 유지합니다.",
                MessageType.None);
            EditorGUILayout.HelpBox(
                "조명/환경 설정 포함 시 Light, Probe, Volume, 파티클, VFX, 데칼, Wind Zone, 환경 오디오와 RenderSettings를 저장합니다. 베이크 라이트맵·Occlusion·씬 NavMesh 데이터는 프리팹에 완전 포함되지 않습니다.",
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        private void DrawGeneratedStageList()
        {
            string normalizedRoot = NormalizeAssetFolder(outputRoot);
            string[] stageFolders = AssetDatabase.IsValidFolder(normalizedRoot)
                ? AssetDatabase.GetSubFolders(normalizedRoot)
                : Array.Empty<string>();

            showGeneratedStages = EditorGUILayout.Foldout(
                showGeneratedStages,
                $"생성된 독립 스테이지 ({stageFolders.Length:N0})",
                true);
            if (!showGeneratedStages)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                if (stageFolders.Length == 0)
                {
                    EditorGUILayout.LabelField("아직 생성된 스테이지가 없습니다.", EditorStyles.miniLabel);
                    return;
                }

                foreach (string folder in stageFolders.OrderBy(path => path, StringComparer.Ordinal).Take(20))
                {
                    string folderName = Path.GetFileName(folder);
                    string prefabPath = $"{folder}/PF_StageMap_{SanitizeAssetName(folderName)}.prefab";
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab == null)
                    {
                        string guid = AssetDatabase.FindAssets("t:Prefab", new[] { folder }).FirstOrDefault();
                        prefabPath = string.IsNullOrEmpty(guid) ? string.Empty : AssetDatabase.GUIDToAssetPath(guid);
                        prefab = string.IsNullOrEmpty(prefabPath)
                            ? null
                            : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    }

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(folderName, prefab != null ? EditorStyles.label : EditorStyles.miniLabel);
                    using (new EditorGUI.DisabledScope(prefab == null))
                    {
                        if (GUILayout.Button("선택", GUILayout.Width(54f)))
                        {
                            Selection.activeObject = prefab;
                            EditorGUIUtility.PingObject(prefab);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (stageFolders.Length > 20)
                {
                    EditorGUILayout.LabelField($"외 {stageFolders.Length - 20:N0}개", EditorStyles.miniLabel);
                }
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return;
            }

            float groundY = GetDisplayGroundY(scene);
            SelectionRegion region = GetSelectionRegion();
            DrawSelectionRegion(region, groundY);

            if (editBoundsInScene)
            {
                float handleHeight = Mathf.Max(10f, Mathf.Min(80f, Mathf.Max(sizeXZ.x, sizeXZ.y) * 0.15f));
                Vector3 handleCenter = new Vector3(centerXZ.x, groundY + handleHeight * 0.5f, centerXZ.y);

                EditorGUI.BeginChangeCheck();
                Quaternion handleRotation = Quaternion.Euler(0f, rotationY, 0f);
                Vector3 movedCenter = Handles.PositionHandle(handleCenter, handleRotation);
                if (EditorGUI.EndChangeCheck())
                {
                    centerXZ = new Vector2(movedCenter.x, movedCenter.z);
                    handleCenter.x = movedCenter.x;
                    handleCenter.z = movedCenter.z;
                    ClearPreview();
                    Repaint();
                }

                EditorGUI.BeginChangeCheck();
                Quaternion rotatedHandle = Handles.RotationHandle(handleRotation, handleCenter);
                if (EditorGUI.EndChangeCheck())
                {
                    rotationY = NormalizeAngle(rotatedHandle.eulerAngles.y);
                    ClearPreview();
                    Repaint();
                    handleRotation = Quaternion.Euler(0f, rotationY, 0f);
                }

                Matrix4x4 previousMatrix = Handles.matrix;
                Handles.matrix = Matrix4x4.TRS(handleCenter, handleRotation, Vector3.one);
                boundsHandle.center = Vector3.zero;
                boundsHandle.size = new Vector3(sizeXZ.x, handleHeight, sizeXZ.y);

                EditorGUI.BeginChangeCheck();
                boundsHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    sizeXZ = new Vector2(
                        Mathf.Max(0.1f, boundsHandle.size.x),
                        Mathf.Max(0.1f, boundsHandle.size.z));
                    ClearPreview();
                    Repaint();
                }

                Handles.matrix = previousMatrix;
            }

            DrawPreviewBounds();
        }

        private void DrawSelectionRegion(SelectionRegion region, float y)
        {
            Vector3[] corners = region.WorldPolygon
                .Select(point => new Vector3(point.x, y, point.y))
                .ToArray();

            Color previousColor = Handles.color;
            Handles.color = new Color(0.1f, 0.75f, 1f, 0.08f);
            Handles.DrawAAConvexPolygon(corners);
            Handles.color = new Color(0.1f, 0.8f, 1f, 0.95f);
            Handles.DrawAAPolyLine(2f, corners.Concat(new[] { corners[0] }).ToArray());
            Handles.color = previousColor;
            Vector2 outputSize = sizeXZ * Mathf.Max(0.01f, outputScale);
            Handles.Label(
                corners[0],
                $"  {stageName}  {GetShapeLabel(sliceShape)}  원본 {sizeXZ.x:0.##} x {sizeXZ.y:0.##}  → 저장 {outputSize.x:0.##} x {outputSize.y:0.##}  Y {rotationY:0.#}°");
        }

        private void DrawPreviewBounds()
        {
            if (previewRoots.Count == 0 && previewWaterCrops.Count == 0)
            {
                return;
            }

            Color previousColor = Handles.color;
            Handles.color = new Color(0.25f, 1f, 0.4f, 0.6f);

            int count = Mathf.Min(previewRoots.Count, PreviewDrawLimit);
            count = Mathf.Min(count, previewBounds.Count);
            for (int i = 0; i < count; i++)
            {
                Bounds bounds = previewBounds[i];
                Handles.DrawWireCube(bounds.center, bounds.size);
            }

            Handles.color = new Color(0.1f, 0.75f, 1f, 0.9f);
            int waterCount = Mathf.Min(previewWaterCrops.Count, PreviewDrawLimit);
            for (int i = 0; i < waterCount; i++)
            {
                WaterCrop crop = previewWaterCrops[i];
                Vector3[] polygon = crop.WorldPolygon
                    .Select(point => new Vector3(point.x, crop.WorldY + 0.03f, point.y))
                    .ToArray();
                if (polygon.Length >= 3)
                {
                    Handles.DrawAAPolyLine(2f, polygon.Concat(new[] { polygon[0] }).ToArray());
                }
            }

            Handles.color = previousColor;
        }

        private bool CanBuild(Scene scene)
        {
            return scene.IsValid()
                   && scene.isLoaded
                   && !string.IsNullOrWhiteSpace(scene.path)
                   && !EditorApplication.isPlayingOrWillChangePlaymode
                   && sizeXZ.x > 0.01f
                   && sizeXZ.y > 0.01f
                   && outputScale > 0.001f
                   && !string.IsNullOrWhiteSpace(stageName);
        }

        private void FitBoundsToSelection()
        {
            Renderer[] renderers = Selection.gameObjects
                .SelectMany(go => go.GetComponentsInChildren<Renderer>(true))
                .Where(renderer => renderer != null)
                .ToArray();

            if (renderers.Length == 0)
            {
                EditorUtility.DisplayDialog("Stage Map Slicer", "선택한 오브젝트에서 Renderer를 찾지 못했습니다.", "확인");
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            centerXZ = new Vector2(bounds.center.x, bounds.center.z);
            sizeXZ = new Vector2(Mathf.Max(0.1f, bounds.size.x), Mathf.Max(0.1f, bounds.size.z));
            ClearPreview();
            SceneView.RepaintAll();
            Repaint();
        }

        private void FitBoundsToTerrains(Scene scene)
        {
            Terrain[] terrains = GetCachedTerrains(scene);
            if (terrains.Length == 0)
            {
                EditorUtility.DisplayDialog("Stage Map Slicer", "현재 씬에서 Terrain을 찾지 못했습니다.", "확인");
                return;
            }

            float minX = float.PositiveInfinity;
            float minZ = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxZ = float.NegativeInfinity;

            foreach (Terrain terrain in terrains)
            {
                Vector3 position = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                minX = Mathf.Min(minX, position.x);
                minZ = Mathf.Min(minZ, position.z);
                maxX = Mathf.Max(maxX, position.x + size.x);
                maxZ = Mathf.Max(maxZ, position.z + size.z);
            }

            centerXZ = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            sizeXZ = new Vector2(maxX - minX, maxZ - minZ);
            ClearPreview();
            SceneView.RepaintAll();
            Repaint();
        }

        private void RefreshPreview(Scene scene, bool showDialogOnEmpty)
        {
            previewRoots.Clear();
            previewEnvironmentRoots.Clear();
            previewWaterCrops.Clear();
            previewBounds.Clear();
            previewRendererCount = 0;
            previewTerrainCount = 0;
            previewLightingCount = 0;

            if (!scene.IsValid() || !scene.isLoaded)
            {
                lastSummary = "현재 열린 씬이 없습니다.";
                return;
            }

            SelectionRegion region = GetSelectionRegion();
            EnsureRendererCache(scene);
            previewWaterCrops.AddRange(CollectWaterCrops(scene, region, includeInactive));
            List<GameObject> collectedRoots = CollectModelRoots(scene, region, includeInactive, out int matchedRenderers);
            previewRoots.AddRange(collectedRoots);
            previewRendererCount = matchedRenderers;
            previewTerrainCount = GetCachedTerrains(scene).Count(terrain => TerrainIntersects(terrain, region));

            int drawCount = Mathf.Min(previewRoots.Count, PreviewDrawLimit);
            for (int i = 0; i < drawCount; i++)
            {
                if (TryGetRendererBounds(previewRoots[i], out Bounds bounds))
                {
                    previewBounds.Add(bounds);
                }
            }

            if (includeLighting)
            {
                previewEnvironmentRoots.AddRange(CollectEnvironmentRoots(scene, region, previewRoots, includeInactive));
                previewLightingCount = previewEnvironmentRoots.Count;
            }

            lastSummary =
                $"미리보기 완료: 프리팹/모델 루트 {previewRoots.Count:N0}개, 영역 교차 Renderer {previewRendererCount:N0}개, Terrain {previewTerrainCount:N0}개, 물/용암 Plane {previewWaterCrops.Count:N0}개, 환경/효과 {previewLightingCount:N0}개";
            Debug.Log($"[StageMapSlicer] {lastSummary}");

            if (showDialogOnEmpty
                && previewRoots.Count == 0
                && previewTerrainCount == 0
                && previewWaterCrops.Count == 0
                && previewLightingCount == 0)
            {
                EditorUtility.DisplayDialog("Stage Map Slicer", "지정 영역과 겹치는 모델이나 Terrain을 찾지 못했습니다.", "확인");
            }

            SceneView.RepaintAll();
            Repaint();
        }

        private void ClearPreview()
        {
            previewRoots.Clear();
            previewEnvironmentRoots.Clear();
            previewWaterCrops.Clear();
            previewBounds.Clear();
            previewRendererCount = 0;
            previewTerrainCount = 0;
            previewLightingCount = 0;
            lastSummary = "영역이 변경되었습니다. 미리보기를 다시 실행하세요.";
        }

        private void BuildStagePrefab(Scene sourceScene, bool showResultDialog = true)
        {
            if (!CanBuild(sourceScene))
            {
                EditorUtility.DisplayDialog("Stage Map Slicer", "현재 씬과 출력 설정을 확인하세요.", "확인");
                return;
            }

            string safeStageName = SanitizeAssetName(stageName);
            string normalizedOutputRoot = NormalizeAssetFolder(outputRoot);
            if (string.IsNullOrEmpty(safeStageName) || !normalizedOutputRoot.StartsWith("Assets", StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog("Stage Map Slicer", "스테이지 이름 또는 출력 루트가 올바르지 않습니다.", "확인");
                return;
            }

            EnsureAssetFolder(normalizedOutputRoot);
            string stageFolder = $"{normalizedOutputRoot}/{safeStageName}";
            if (AssetDatabase.IsValidFolder(stageFolder))
            {
                EditorUtility.DisplayDialog(
                    "Stage Map Slicer",
                    $"이미 같은 출력 폴더가 있습니다. 기존 결과를 보호하기 위해 덮어쓰지 않습니다.\n{stageFolder}",
                    "확인");
                return;
            }

            RefreshPreview(sourceScene, false);
            GameObject[] modelRoots = previewRoots.ToArray();
            GameObject[] environmentRoots = previewEnvironmentRoots.ToArray();
            WaterCrop[] waterCrops = previewWaterCrops.ToArray();
            SelectionRegion region = GetSelectionRegion();
            TerrainCrop[] terrainCrops = GetCachedTerrains(sourceScene)
                .Select(terrain => TryCreateTerrainCrop(terrain, region, out TerrainCrop crop) ? crop : default)
                .Where(crop => crop.Terrain != null)
                .ToArray();

            if (modelRoots.Length == 0
                && terrainCrops.Length == 0
                && waterCrops.Length == 0
                && environmentRoots.Length == 0)
            {
                EditorUtility.DisplayDialog("Stage Map Slicer", "영역 안에 생성할 모델, Terrain 또는 환경 효과가 없습니다.", "확인");
                return;
            }

            string parentFolder = normalizedOutputRoot;
            string folderName = safeStageName;
            bool createdStageFolder = false;
            string createdGuid = AssetDatabase.CreateFolder(parentFolder, folderName);
            if (string.IsNullOrEmpty(createdGuid))
            {
                EditorUtility.DisplayDialog("Stage Map Slicer", "출력 폴더를 만들지 못했습니다.", "확인");
                return;
            }
            createdStageFolder = true;

            Scene previewScene = default;
            bool success = false;

            try
            {
                previewScene = EditorSceneManager.NewPreviewScene();
                GameObject exportRoot = new GameObject($"PF_StageMap_{safeStageName}");
                SceneManager.MoveGameObjectToScene(exportRoot, previewScene);

                GameObject groundRoot = new GameObject("Ground");
                SceneManager.MoveGameObjectToScene(groundRoot, previewScene);
                groundRoot.transform.SetParent(exportRoot.transform, false);

                GameObject modelsRoot = new GameObject("Models");
                SceneManager.MoveGameObjectToScene(modelsRoot, previewScene);
                modelsRoot.transform.SetParent(exportRoot.transform, false);

                GameObject waterRoot = null;
                if (waterCrops.Length > 0)
                {
                    waterRoot = new GameObject("Water");
                    SceneManager.MoveGameObjectToScene(waterRoot, previewScene);
                    waterRoot.transform.SetParent(exportRoot.transform, false);
                }

                GameObject environmentRoot = null;
                if (includeLighting)
                {
                    environmentRoot = new GameObject("EnvironmentEffects");
                    SceneManager.MoveGameObjectToScene(environmentRoot, previewScene);
                    environmentRoot.transform.SetParent(exportRoot.transform, false);
                }

                float originY = terrainCrops.Length > 0
                    ? terrainCrops.Min(crop => crop.Terrain.transform.position.y)
                    : modelRoots.Length > 0
                        ? GetLowestModelY(modelRoots)
                        : waterCrops.Min(crop => crop.WorldY);
                StageSpace stageSpace = new StageSpace(
                    new Vector3(centerXZ.x, originY, centerXZ.y),
                    rotationY,
                    outputScale);
                Dictionary<UnityEngine.Object, UnityEngine.Object> objectMap =
                    new Dictionary<UnityEngine.Object, UnityEngine.Object>();
                int linkedPrefabCount = 0;
                int embeddedModelCount = 0;
                int disabledColliderCount = 0;
                int disabledShadowRendererCount = 0;
                int gpuInstancedRendererCount = 0;
                List<GameObject> vegetationRoots = new List<GameObject>();
                HashSet<GameObject> smallVegetationSources = new HashSet<GameObject>(
                    modelRoots.Where(IsSmallVegetationRoot));
                HashSet<Material> repeatedVegetationMaterials = enableRepeatedVegetationGpuInstancing
                    ? CollectRepeatedVegetationMaterials(smallVegetationSources)
                    : new HashSet<Material>();
                Dictionary<Material, bool> sourceMaterialInstancingStates = repeatedVegetationMaterials
                    .ToDictionary(material => material, material => material.enableInstancing);
                Dictionary<Material, Material> stageInstancedMaterials = new Dictionary<Material, Material>();
                List<string> stageInstancedMaterialPaths = new List<string>();

                for (int i = 0; i < modelRoots.Length; i++)
                {
                    EditorUtility.DisplayProgressBar(
                        "Stage Map Slicer",
                        $"모델 복제 중 ({i + 1:N0}/{modelRoots.Length:N0})",
                        modelRoots.Length == 0 ? 0f : (float)i / modelRoots.Length * 0.42f);
                    bool smallVegetation = smallVegetationSources.Contains(modelRoots[i]);
                    GameObject clone = DuplicateModelRoot(
                        modelRoots[i],
                        modelsRoot.transform,
                        previewScene,
                        stageSpace,
                        objectMap,
                        ref linkedPrefabCount,
                        ref embeddedModelCount);
                    if (smallVegetation && clone != null)
                    {
                        ApplySmallVegetationOptimizations(
                            clone,
                            disableDecorationColliders,
                            disableSmallVegetationShadows,
                            ref disabledColliderCount,
                            ref disabledShadowRendererCount);
                        if (enableRepeatedVegetationGpuInstancing)
                        {
                            gpuInstancedRendererCount += ApplyRepeatedVegetationGpuInstancing(
                                clone,
                                repeatedVegetationMaterials,
                                stageFolder,
                                stageInstancedMaterials,
                                stageInstancedMaterialPaths);
                        }

                        if (enableVegetationDistanceCulling)
                        {
                            vegetationRoots.Add(clone);
                        }
                    }
                }

                Material[] gpuInstancingMaterials = stageInstancedMaterials.Values
                    .Where(material => material != null)
                    .Distinct()
                    .ToArray();

                int vegetationCullCellCount = enableVegetationDistanceCulling
                    ? CreateVegetationCullingCells(
                        exportRoot,
                        modelsRoot.transform,
                        previewScene,
                        vegetationRoots,
                        vegetationCullDistance * stageSpace.Scale,
                        vegetationCullCellSize * stageSpace.Scale)
                    : 0;

                Light clonedSun = null;
                if (includeLighting && environmentRoot != null)
                {
                    Light sourceSun = RenderSettings.sun;
                    for (int i = 0; i < environmentRoots.Length; i++)
                    {
                        EditorUtility.DisplayProgressBar(
                            "Stage Map Slicer",
                            $"환경/효과 복제 중 ({i + 1:N0}/{environmentRoots.Length:N0})",
                            0.42f + (float)i / Mathf.Max(1, environmentRoots.Length) * 0.10f);
                        Light duplicatedSun = DuplicateEnvironmentRoot(
                            environmentRoots[i],
                            environmentRoot.transform,
                            previewScene,
                            stageSpace,
                            sourceSun,
                            objectMap,
                            ref linkedPrefabCount,
                            ref embeddedModelCount);
                        if (duplicatedSun != null)
                        {
                            clonedSun = duplicatedSun;
                        }
                    }

                    StageLightingSettings lightingSettings = exportRoot.AddComponent<StageLightingSettings>();
                    lightingSettings.CaptureCurrent(clonedSun);
                }

                for (int i = 0; i < terrainCrops.Length; i++)
                {
                    EditorUtility.DisplayProgressBar(
                        "Stage Map Slicer",
                        $"Terrain 크롭 중 ({i + 1}/{terrainCrops.Length})",
                        0.52f + (float)i / Mathf.Max(1, terrainCrops.Length) * 0.25f);
                    string terrainAssetPath = $"{stageFolder}/{safeStageName}_TerrainData_{i + 1:00}.asset";
                    CreateCroppedTerrain(
                        terrainCrops[i],
                        terrainAssetPath,
                        groundRoot.transform,
                        previewScene,
                        stageSpace,
                        false,
                        overrideTerrainDistances,
                        terrainDetailDistance,
                        terrainTreeDistance,
                        stageSpace.Scale);
                }

                if (gpuInstancingMaterials.Length > 0
                    || enableTerrainDrawInstanced && terrainCrops.Length > 0)
                {
                    StageVegetationGpuInstancingEnabler gpuInstancingEnabler =
                        exportRoot.AddComponent<StageVegetationGpuInstancingEnabler>();
                    gpuInstancingEnabler.Configure(gpuInstancingMaterials, enableTerrainDrawInstanced);
                }

                if (waterRoot != null)
                {
                    for (int i = 0; i < waterCrops.Length; i++)
                    {
                        EditorUtility.DisplayProgressBar(
                            "Stage Map Slicer",
                            $"물/용암 Plane 크롭 중 ({i + 1}/{waterCrops.Length})",
                            0.77f + (float)i / Mathf.Max(1, waterCrops.Length) * 0.13f);
                        string waterMeshPath = $"{stageFolder}/{safeStageName}_WaterMesh_{i + 1:00}.asset";
                        CreateCroppedWater(waterCrops[i], waterMeshPath, waterRoot.transform, previewScene, stageSpace);
                    }
                }

                int remappedSceneReferences = RemapAndClearSceneObjectReferences(exportRoot, objectMap, sourceScene);

                string prefabPath = $"{stageFolder}/PF_StageMap_{safeStageName}.prefab";
                EditorUtility.DisplayProgressBar("Stage Map Slicer", "단일 프리팹 저장 중", 0.94f);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(exportRoot, prefabPath, out bool prefabSaved);
                if (!prefabSaved || prefab == null)
                {
                    throw new InvalidOperationException($"프리팹 저장에 실패했습니다: {prefabPath}");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                ValidateGeneratedOptimizations(
                    prefab,
                    terrainCrops,
                    disabledColliderCount,
                    disabledShadowRendererCount,
                    vegetationCullCellCount,
                    stageFolder,
                    stageInstancedMaterialPaths,
                    gpuInstancedRendererCount,
                    sourceMaterialInstancingStates,
                    enableTerrainDrawInstanced,
                    overrideTerrainDistances,
                    terrainDetailDistance,
                    terrainTreeDistance,
                    stageSpace.Scale);

                string[] sceneDependencies = AssetDatabase.GetDependencies(prefabPath, true)
                    .Where(path => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (sceneDependencies.Length > 0)
                {
                    throw new InvalidOperationException(
                        "생성 프리팹에 원본 씬 의존성이 남았습니다: " + string.Join(", ", sceneDependencies));
                }

                int missingScripts = CountMissingScripts(exportRoot);
                int missingMaterials = CountMissingMaterials(exportRoot);
                lastSummary =
                    $"생성 완료: {prefabPath}\n{GetShapeLabel(sliceShape)} / 출력 배율 x{stageSpace.Scale:0.###} / 원본 {sizeXZ.x:0.##} x {sizeXZ.y:0.##} → 저장 {sizeXZ.x * stageSpace.Scale:0.##} x {sizeXZ.y * stageSpace.Scale:0.##} / Y {rotationY:0.#}° / 연결 프리팹 {linkedPrefabCount:N0}개 / 내장 모델 {embeddedModelCount:N0}개 / Terrain {terrainCrops.Length:N0}개 / 물·용암 {waterCrops.Length:N0}개 / 환경·효과 {environmentRoots.Length:N0}개 / Collider 비활성 {disabledColliderCount:N0}개 / 그림자 해제 Renderer {disabledShadowRendererCount:N0}개 / 실제 Instancing Material {stageInstancedMaterialPaths.Count:N0}개 / SRP 우회 Renderer {gpuInstancedRendererCount:N0}개 / 식생 컬링 셀 {vegetationCullCellCount:N0}개 / 씬 참조 정리 {remappedSceneReferences:N0}개 / Missing Script {missingScripts:N0} / 활성 누락 Material {missingMaterials:N0}";
                Debug.Log($"[StageMapSlicer] {lastSummary}");

                EditorGUIUtility.PingObject(prefab);
                success = true;
                if (showResultDialog)
                {
                    EditorUtility.DisplayDialog("Stage Map Slicer", lastSummary, "확인");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                lastSummary = $"생성 실패: {exception.Message}";
                if (showResultDialog)
                {
                    EditorUtility.DisplayDialog("Stage Map Slicer", lastSummary, "확인");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (previewScene.IsValid())
                {
                    EditorSceneManager.ClosePreviewScene(previewScene);
                }

                if (!success && createdStageFolder && AssetDatabase.IsValidFolder(stageFolder))
                {
                    AssetDatabase.DeleteAsset(stageFolder); // 이번 실행 결과만 정리
                    AssetDatabase.Refresh();
                }

                Repaint();
            }
        }

        private List<GameObject> CollectModelRoots(
            Scene scene,
            SelectionRegion region,
            bool includeInactiveObjects,
            out int matchedRendererCount)
        {
            HashSet<GameObject> candidates = new HashSet<GameObject>();
            matchedRendererCount = 0;

            foreach (RendererRecord record in rendererCache)
            {
                Renderer renderer = record.Renderer;
                if (renderer == null || renderer.gameObject.scene != scene)
                {
                    continue;
                }

                if (!includeInactiveObjects && !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (renderer is ParticleSystemRenderer
                    || IsCroppableWaterRenderer(renderer)
                    || !region.Intersects(record.Bounds))
                {
                    continue;
                }

                matchedRendererCount++;
                GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(renderer.gameObject);
                GameObject candidate = prefabRoot != null ? prefabRoot : renderer.gameObject;
                if (candidate.hideFlags != HideFlags.None)
                {
                    continue;
                }

                candidates.Add(candidate);
            }

            return candidates
                .Where(candidate => !HasCandidateAncestor(candidate.transform.parent, candidates))
                .OrderBy(candidate => candidate.GetInstanceID())
                .ToList();
        }

        private List<WaterCrop> CollectWaterCrops(Scene scene, SelectionRegion region, bool includeInactiveObjects)
        {
            List<WaterCrop> crops = new List<WaterCrop>();
            foreach (RendererRecord record in rendererCache)
            {
                Renderer renderer = record.Renderer;
                if (renderer == null
                    || renderer.gameObject.scene != scene
                    || (!includeInactiveObjects && !renderer.gameObject.activeInHierarchy)
                    || !TryCreateWaterCrop(renderer, record.Bounds, region, out WaterCrop crop))
                {
                    continue;
                }

                crops.Add(crop);
            }

            return crops.OrderBy(crop => crop.Renderer.GetInstanceID()).ToList();
        }

        private static bool TryCreateWaterCrop(
            Renderer renderer,
            Bounds rendererBounds,
            SelectionRegion region,
            out WaterCrop crop)
        {
            crop = default;
            if (!IsCroppableWaterRenderer(renderer) || !region.Intersects(rendererBounds))
            {
                return false;
            }

            List<Vector2> waterBounds = new List<Vector2>
            {
                new Vector2(rendererBounds.min.x, rendererBounds.min.z),
                new Vector2(rendererBounds.max.x, rendererBounds.min.z),
                new Vector2(rendererBounds.max.x, rendererBounds.max.z),
                new Vector2(rendererBounds.min.x, rendererBounds.max.z)
            };
            List<Vector2> clipped = ClipConvexPolygon(waterBounds, region.WorldPolygon);
            if (clipped.Count < 3 || Mathf.Abs(SignedArea(clipped)) <= 0.01f)
            {
                return false;
            }

            crop = new WaterCrop(renderer, clipped.ToArray(), rendererBounds.center.y);
            return true;
        }

        private static bool IsCroppableWaterRenderer(Renderer renderer)
        {
            if (!(renderer is MeshRenderer)
                || renderer.GetComponent<MeshFilter>()?.sharedMesh == null
                || !IsWaterRenderer(renderer))
            {
                return false;
            }

            Transform transform = renderer.transform;
            bool horizontal = Mathf.Abs(Vector3.Dot(transform.up, Vector3.up)) > 0.999f;
            float xAlignment = Mathf.Abs(Vector3.Dot(transform.right, Vector3.right));
            float zAlignment = Mathf.Abs(Vector3.Dot(transform.right, Vector3.forward));
            Vector3 boundsSize = renderer.bounds.size;
            float horizontalSize = Mathf.Max(0.0001f, Mathf.Min(boundsSize.x, boundsSize.z));
            bool flatSurface = boundsSize.y <= Mathf.Max(0.05f, horizontalSize * 0.01f);
            return horizontal && Mathf.Max(xAlignment, zAlignment) > 0.999f && flatSurface;
        }

        private static bool IsWaterRenderer(Renderer renderer)
        {
            if (renderer.gameObject.layer == 4)
            {
                return true;
            }

            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null)
                {
                    continue;
                }

                string materialName = material.name;
                string shaderName = material.shader != null ? material.shader.name : string.Empty;
                if (materialName.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0
                    || shaderName.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0
                    || materialName.IndexOf("lava", StringComparison.OrdinalIgnoreCase) >= 0
                    || shaderName.IndexOf("lava", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureRendererCache(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                rendererCache.Clear();
                return;
            }

            if (cachedSceneHandle != scene.handle)
            {
                cachedSceneHandle = scene.handle;
                sceneCacheDirty = true;
                rendererCache.Clear();
                terrainCache = Array.Empty<Terrain>();
            }

            if (!sceneCacheDirty && rendererCache.Count > 0)
            {
                return;
            }

            rendererCache.Clear();
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                foreach (Renderer renderer in sceneRoot.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer != null && renderer.gameObject.scene == scene)
                    {
                        rendererCache.Add(new RendererRecord(renderer, renderer.bounds));
                    }
                }
            }

            terrainCache = FindTerrains(scene);
            sceneCacheDirty = false;
        }

        private Terrain[] GetCachedTerrains(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return Array.Empty<Terrain>();
            }

            if (cachedSceneHandle != scene.handle)
            {
                cachedSceneHandle = scene.handle;
                sceneCacheDirty = true;
                rendererCache.Clear();
                terrainCache = Array.Empty<Terrain>();
            }

            if (terrainCache.Length == 0)
            {
                terrainCache = FindTerrains(scene);
            }

            return terrainCache;
        }

        private void InvalidateSceneCache()
        {
            sceneCacheDirty = true;
            rendererCache.Clear();
            terrainCache = Array.Empty<Terrain>();
            ClearPreview();
        }

        private static List<GameObject> CollectEnvironmentRoots(
            Scene scene,
            SelectionRegion region,
            IReadOnlyCollection<GameObject> modelRoots,
            bool includeInactiveObjects)
        {
            HashSet<GameObject> candidates = new HashSet<GameObject>();
            AddEnvironmentComponents(scene, Resources.FindObjectsOfTypeAll<Light>(), includeInactiveObjects, light => LightAffectsRegion(light, region), candidates);
            AddEnvironmentComponents(scene, Resources.FindObjectsOfTypeAll<ReflectionProbe>(), includeInactiveObjects, probe => region.Intersects(probe.bounds), candidates);
            AddEnvironmentComponents(scene, Resources.FindObjectsOfTypeAll<LightProbeGroup>(), includeInactiveObjects, group => LightProbeGroupAffectsRegion(group, region), candidates);
            AddEnvironmentComponents(scene, Resources.FindObjectsOfTypeAll<WindZone>(), includeInactiveObjects, zone => WindZoneAffectsRegion(zone, region), candidates);
            AddEnvironmentComponents(scene, Resources.FindObjectsOfTypeAll<ParticleSystem>(), includeInactiveObjects, effect => ComponentAffectsRegion(effect, region), candidates);
            AddEnvironmentComponents(scene, Resources.FindObjectsOfTypeAll<AudioSource>(), includeInactiveObjects, source => AudioSourceAffectsRegion(source, region), candidates);
            AddOptionalEnvironmentComponents(scene, region, includeInactiveObjects, candidates);

            candidates.RemoveWhere(candidate => candidate == null
                                                || IsInsideAnyRoot(candidate.transform, modelRoots)
                                                || candidate.hideFlags != HideFlags.None);

            return candidates
                .Where(candidate => !HasCandidateAncestor(candidate.transform.parent, candidates))
                .OrderBy(candidate => candidate.GetInstanceID())
                .ToList();
        }

        private static void AddEnvironmentComponents<T>(
            Scene scene,
            IEnumerable<T> components,
            bool includeInactiveObjects,
            Func<T, bool> predicate,
            ISet<GameObject> candidates)
            where T : Component
        {
            foreach (T component in components)
            {
                if (component == null
                    || component.gameObject.scene != scene
                    || (!includeInactiveObjects && !component.gameObject.activeInHierarchy)
                    || !predicate(component))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(component.gameObject);
                candidates.Add(prefabRoot != null ? prefabRoot : component.gameObject);
            }
        }

        private static void AddOptionalEnvironmentComponents(
            Scene scene,
            SelectionRegion region,
            bool includeInactiveObjects,
            ISet<GameObject> candidates)
        {
            HashSet<string> supportedTypes = new HashSet<string>(StringComparer.Ordinal)
            {
                "UnityEngine.Rendering.Volume",
                "UnityEngine.VFX.VisualEffect",
                "UnityEngine.Rendering.Universal.DecalProjector",
                "UnityEngine.Rendering.HighDefinition.DecalProjector",
                "UnityEngine.Rendering.HighDefinition.WaterSurface"
            };

            foreach (Type type in TypeCache.GetTypesDerivedFrom<Component>())
            {
                if (!supportedTypes.Contains(type.FullName))
                {
                    continue;
                }

                foreach (UnityEngine.Object foundObject in Resources.FindObjectsOfTypeAll(type))
                {
                    if (!(foundObject is Component component)
                        || component.gameObject.scene != scene
                        || (!includeInactiveObjects && !component.gameObject.activeInHierarchy)
                        || !OptionalComponentAffectsRegion(component, region))
                    {
                        continue;
                    }

                    GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(component.gameObject);
                    candidates.Add(prefabRoot != null ? prefabRoot : component.gameObject);
                }
            }
        }

        private static bool OptionalComponentAffectsRegion(Component component, SelectionRegion region)
        {
            if (component.GetType().FullName == "UnityEngine.Rendering.Volume")
            {
                var isGlobalProperty = component.GetType().GetProperty("isGlobal");
                if (isGlobalProperty != null && isGlobalProperty.GetValue(component) is bool isGlobal && isGlobal)
                {
                    return true;
                }
            }

            return ComponentAffectsRegion(component, region);
        }

        private static bool ComponentAffectsRegion(Component component, SelectionRegion region)
        {
            Collider collider = component.GetComponent<Collider>();
            if (collider != null && region.Intersects(collider.bounds))
            {
                return true;
            }

            Renderer renderer = component.GetComponent<Renderer>();
            if (renderer != null && region.Intersects(renderer.bounds))
            {
                return true;
            }

            Vector3 position = component.transform.position;
            return region.Contains(position.x, position.z);
        }

        private static bool LightAffectsRegion(Light light, SelectionRegion region)
        {
            if (light.type == LightType.Directional)
            {
                return true;
            }

            return region.DistanceSquared(light.transform.position.x, light.transform.position.z)
                   <= light.range * light.range;
        }

        private static bool WindZoneAffectsRegion(WindZone zone, SelectionRegion region)
        {
            if (zone.mode == WindZoneMode.Directional)
            {
                return true;
            }

            return region.DistanceSquared(zone.transform.position.x, zone.transform.position.z)
                   <= zone.radius * zone.radius;
        }

        private static bool AudioSourceAffectsRegion(AudioSource source, SelectionRegion region)
        {
            if (source.loop && source.spatialBlend <= 0.01f)
            {
                return true;
            }

            float range = Mathf.Max(0.1f, source.maxDistance);
            return region.DistanceSquared(source.transform.position.x, source.transform.position.z) <= range * range;
        }

        private static bool LightProbeGroupAffectsRegion(LightProbeGroup group, SelectionRegion region)
        {
            Vector3[] probes = group.probePositions;
            if (probes == null || probes.Length == 0)
            {
                Vector3 position = group.transform.position;
                return region.Contains(position.x, position.z);
            }

            foreach (Vector3 localPosition in probes)
            {
                Vector3 worldPosition = group.transform.TransformPoint(localPosition);
                if (region.Contains(worldPosition.x, worldPosition.z))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInsideAnyRoot(Transform transform, IReadOnlyCollection<GameObject> roots)
        {
            foreach (GameObject root in roots)
            {
                if (root != null && (transform == root.transform || transform.IsChildOf(root.transform)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasCandidateAncestor(Transform parent, HashSet<GameObject> candidates)
        {
            while (parent != null)
            {
                if (candidates.Contains(parent.gameObject))
                {
                    return true;
                }

                parent = parent.parent;
            }

            return false;
        }

        private static GameObject DuplicateModelRoot(
            GameObject source,
            Transform destinationParent,
            Scene destinationScene,
            StageSpace stageSpace,
            IDictionary<UnityEngine.Object, UnityEngine.Object> objectMap,
            ref int linkedPrefabCount,
            ref int embeddedModelCount)
        {
            if (source == null)
            {
                return null;
            }

            GameObject clone = InstantiateLinkedOrEmbedded(
                source,
                destinationParent,
                destinationScene,
                stageSpace,
                out bool linkedPrefab);
            MapHierarchyObjects(source, clone, objectMap);
            if (linkedPrefab)
            {
                linkedPrefabCount++;
            }
            else
            {
                embeddedModelCount++;
            }

            return clone;
        }

        private static bool IsSmallVegetationRoot(GameObject source)
        {
            if (source == null)
            {
                return false;
            }

            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(source);
            if (ContainsSmallVegetationKeyword(source.name)
                || ContainsSmallVegetationKeyword(prefabPath))
            {
                return true;
            }

            foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true))
            {
                if (ContainsSmallVegetationKeyword(renderer.name))
                {
                    return true;
                }

                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null
                        && (ContainsSmallVegetationKeyword(material.name)
                            || ContainsSmallVegetationKeyword(AssetDatabase.GetAssetPath(material))))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ContainsSmallVegetationKeyword(string value)
        {
            return !string.IsNullOrEmpty(value)
                   && SmallVegetationKeywords.Any(keyword =>
                       value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static HashSet<Material> CollectRepeatedVegetationMaterials(
            IEnumerable<GameObject> vegetationRoots)
        {
            Dictionary<Material, int> rootUsage = new Dictionary<Material, int>();
            foreach (GameObject root in vegetationRoots)
            {
                if (root == null)
                {
                    continue;
                }

                HashSet<Material> rootMaterials = new HashSet<Material>();
                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material != null && material.shader != null)
                        {
                            rootMaterials.Add(material);
                        }
                    }
                }

                foreach (Material material in rootMaterials)
                {
                    rootUsage.TryGetValue(material, out int usage);
                    rootUsage[material] = usage + 1;
                }
            }

            return new HashSet<Material>(
                rootUsage.Where(pair => pair.Value > 1).Select(pair => pair.Key));
        }

        private static void ApplySmallVegetationOptimizations(
            GameObject clone,
            bool disableColliders,
            bool disableShadows,
            ref int disabledColliderCount,
            ref int disabledShadowRendererCount)
        {
            if (disableColliders)
            {
                foreach (Collider collider in clone.GetComponentsInChildren<Collider>(true))
                {
                    if (collider.enabled)
                    {
                        collider.enabled = false;
                        RecordPrefabOverride(collider);
                        disabledColliderCount++;
                    }
                }
            }

            if (!disableShadows)
            {
                return;
            }

            foreach (Renderer renderer in clone.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    RecordPrefabOverride(renderer);
                    disabledShadowRendererCount++;
                }
            }
        }

        private static int ApplyRepeatedVegetationGpuInstancing(
            GameObject clone,
            ISet<Material> repeatedMaterials,
            string stageFolder,
            IDictionary<Material, Material> stageMaterialCache,
            ICollection<string> createdMaterialPaths)
        {
            if (clone == null || repeatedMaterials == null || repeatedMaterials.Count == 0)
            {
                return 0;
            }

            int changedRenderers = 0;
            foreach (MeshRenderer renderer in clone.GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material sourceMaterial = materials[i];
                    if (sourceMaterial == null
                        || !repeatedMaterials.Contains(sourceMaterial))
                    {
                        continue;
                    }

                    if (!stageMaterialCache.TryGetValue(sourceMaterial, out Material stageMaterial))
                    {
                        stageMaterial = CreateStageInstancedMaterial(
                            sourceMaterial,
                            stageFolder,
                            createdMaterialPaths);
                        stageMaterialCache.Add(sourceMaterial, stageMaterial);
                    }

                    if (stageMaterial == null)
                    {
                        continue;
                    }

                    materials[i] = stageMaterial;
                    changed = true;
                }

                if (!changed)
                {
                    continue;
                }

                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
                RecordPrefabOverride(renderer);
                changedRenderers++;
            }

            return changedRenderers;
        }

        private static Material CreateStageInstancedMaterial(
            Material sourceMaterial,
            string stageFolder,
            ICollection<string> createdMaterialPaths)
        {
            if (!ShaderSupportsRendererScopedGpuInstancing(sourceMaterial.shader, out string reason))
            {
                Debug.LogWarning(
                    $"[StageMapSlicer] 실제 GPU Instancing 제외: {sourceMaterial.name} ({reason})");
                return null;
            }

            const string materialFolderName = "InstancedMaterials";
            string materialFolder = $"{stageFolder}/{materialFolderName}";
            if (!AssetDatabase.IsValidFolder(materialFolder))
            {
                string folderGuid = AssetDatabase.CreateFolder(stageFolder, materialFolderName);
                if (string.IsNullOrEmpty(folderGuid))
                {
                    throw new InvalidOperationException($"Instancing Material 폴더 생성 실패: {materialFolder}");
                }
            }

            string identity = Mathf.Abs(sourceMaterial.GetInstanceID()).ToString();
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    sourceMaterial,
                    out string sourceGuid,
                    out long localId)
                && !string.IsNullOrEmpty(sourceGuid))
            {
                identity = $"{sourceGuid.Substring(0, Mathf.Min(8, sourceGuid.Length))}_{localId}";
            }

            string safeName = SanitizeAssetName(sourceMaterial.name);
            string materialPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{materialFolder}/{safeName}_{identity}_Instanced.mat");
            Material stageMaterial = new Material(sourceMaterial);
            stageMaterial.name = $"{sourceMaterial.name}_Instanced";
            stageMaterial.enableInstancing = true;
            AssetDatabase.CreateAsset(stageMaterial, materialPath);
            if (!stageMaterial.enableInstancing)
            {
                throw new InvalidOperationException($"GPU Instancing 활성화 실패: {sourceMaterial.name}");
            }

            createdMaterialPaths.Add(materialPath);
            return stageMaterial;
        }

        private static bool ShaderSupportsRendererScopedGpuInstancing(
            Shader shader,
            out string reason)
        {
            if (shader == null)
            {
                reason = "Shader 없음";
                return false;
            }

            string shaderPath = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(shaderPath)
                || !shaderPath.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
            {
                reason = $"검증 가능한 .shader가 아님: {shader.name}";
                return false;
            }

            string sourceText = File.ReadAllText(shaderPath);
            if (sourceText.IndexOf("#pragma multi_compile_instancing", StringComparison.Ordinal) < 0)
            {
                reason = "multi_compile_instancing 없음";
                return false;
            }

            if (sourceText.IndexOf("#pragma instancing_options renderinglayer", StringComparison.Ordinal) < 0)
            {
                reason = "instancing_options renderinglayer 없음";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static void RecordPrefabOverride(Component component)
        {
            if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }

        private static int CreateVegetationCullingCells(
            GameObject exportRoot,
            Transform modelsRoot,
            Scene destinationScene,
            IReadOnlyCollection<GameObject> vegetationRoots,
            float cullDistance,
            float cellSize)
        {
            if (vegetationRoots == null || vegetationRoots.Count == 0)
            {
                return 0;
            }

            float safeCellSize = Mathf.Max(1f, cellSize);
            GameObject cullingRoot = new GameObject("VegetationCulling");
            SceneManager.MoveGameObjectToScene(cullingRoot, destinationScene);
            cullingRoot.transform.SetParent(modelsRoot, false);

            Dictionary<Vector2Int, Transform> cells = new Dictionary<Vector2Int, Transform>();
            foreach (GameObject vegetationRoot in vegetationRoots)
            {
                if (vegetationRoot == null)
                {
                    continue;
                }

                Vector3 localPosition = modelsRoot.InverseTransformPoint(vegetationRoot.transform.position);
                Vector2Int key = new Vector2Int(
                    Mathf.FloorToInt(localPosition.x / safeCellSize),
                    Mathf.FloorToInt(localPosition.z / safeCellSize));
                if (!cells.TryGetValue(key, out Transform cell))
                {
                    GameObject cellObject = new GameObject($"Cell_{key.x}_{key.y}");
                    SceneManager.MoveGameObjectToScene(cellObject, destinationScene);
                    cellObject.transform.SetParent(cullingRoot.transform, false);
                    cellObject.transform.localPosition = new Vector3(
                        (key.x + 0.5f) * safeCellSize,
                        0f,
                        (key.y + 0.5f) * safeCellSize);
                    cell = cellObject.transform;
                    cells.Add(key, cell);
                }

                vegetationRoot.transform.SetParent(cell, true);
            }

            if (cells.Count == 0)
            {
                UnityEngine.Object.DestroyImmediate(cullingRoot);
                return 0;
            }

            StageVegetationDistanceCuller culler = exportRoot.AddComponent<StageVegetationDistanceCuller>();
            culler.Configure(Mathf.Max(1f, cullDistance), cells.Values.ToArray());
            return cells.Count;
        }

        private static Light DuplicateEnvironmentRoot(
            GameObject source,
            Transform destinationParent,
            Scene destinationScene,
            StageSpace stageSpace,
            Light sourceSun,
            IDictionary<UnityEngine.Object, UnityEngine.Object> objectMap,
            ref int linkedPrefabCount,
            ref int embeddedModelCount)
        {
            if (source == null)
            {
                return null;
            }

            string sunPath = null;
            if (sourceSun != null
                && (sourceSun.transform == source.transform || sourceSun.transform.IsChildOf(source.transform)))
            {
                sunPath = AnimationUtility.CalculateTransformPath(sourceSun.transform, source.transform);
            }

            GameObject clone = InstantiateLinkedOrEmbedded(
                source,
                destinationParent,
                destinationScene,
                stageSpace,
                out bool linkedPrefab);
            MapHierarchyObjects(source, clone, objectMap);
            if (linkedPrefab)
            {
                linkedPrefabCount++;
            }
            else
            {
                embeddedModelCount++;
            }

            if (sunPath == null)
            {
                return null;
            }

            Transform clonedSunTransform = string.IsNullOrEmpty(sunPath) ? clone.transform : clone.transform.Find(sunPath);
            return clonedSunTransform != null ? clonedSunTransform.GetComponent<Light>() : null;
        }

        private static GameObject InstantiateLinkedOrEmbedded(
            GameObject source,
            Transform destinationParent,
            Scene destinationScene,
            StageSpace stageSpace,
            out bool linkedPrefab)
        {
            linkedPrefab = false;
            GameObject clone = null;
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(source);
            GameObject prefabAsset = string.IsNullOrEmpty(prefabPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefabAsset != null)
            {
                clone = PrefabUtility.InstantiatePrefab(prefabAsset, destinationScene) as GameObject;
                if (clone != null)
                {
                    PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(source);
                    if (modifications != null && modifications.Length > 0)
                    {
                        PrefabUtility.SetPropertyModifications(clone, modifications);
                    }

                    CopyAddedComponentOverrides(source, clone);

                    linkedPrefab = true;
                }
            }

            if (clone == null)
            {
                clone = UnityEngine.Object.Instantiate(source);
                SceneManager.MoveGameObjectToScene(clone, destinationScene);
            }

            clone.name = source.name;
            clone.transform.SetParent(destinationParent, false);
            clone.transform.SetLocalPositionAndRotation(
                stageSpace.WorldToLocalPosition(source.transform.position),
                stageSpace.WorldToLocalRotation(source.transform.rotation));
            clone.transform.localScale = source.transform.lossyScale * stageSpace.Scale;
            ScaleAbsoluteWorldUnitComponents(clone, stageSpace.Scale);
            clone.SetActive(source.activeSelf);
            return clone;
        }

        private static void ScaleAbsoluteWorldUnitComponents(GameObject root, float outputScale)
        {
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                if (light.type != LightType.Directional)
                {
                    light.range *= outputScale;
                }
            }

            foreach (AudioSource audioSource in root.GetComponentsInChildren<AudioSource>(true))
            {
                audioSource.minDistance *= outputScale;
                audioSource.maxDistance *= outputScale;
            }

            foreach (WindZone windZone in root.GetComponentsInChildren<WindZone>(true))
            {
                windZone.radius *= outputScale;
            }
        }

        private static void CopyAddedComponentOverrides(GameObject sourceRoot, GameObject destinationRoot)
        {
            MethodInfo method = typeof(PrefabUtility).GetMethod(
                "GetAddedComponents",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(GameObject) },
                null);
            if (method == null || !(method.Invoke(null, new object[] { sourceRoot }) is System.Collections.IEnumerable addedComponents))
            {
                return;
            }

            foreach (object addedComponent in addedComponents)
            {
                PropertyInfo componentProperty = addedComponent.GetType().GetProperty(
                    "instanceComponent",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                FieldInfo componentField = addedComponent.GetType().GetField(
                    "instanceComponent",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object componentValue = componentProperty != null
                    ? componentProperty.GetValue(addedComponent)
                    : componentField?.GetValue(addedComponent);
                if (!(componentValue is Component sourceComponent))
                {
                    continue;
                }

                string path = GetIndexedHierarchyPath(sourceComponent.transform, sourceRoot.transform);
                Transform destinationTransform = FindTransformByIndexedPath(destinationRoot.transform, path);
                if (destinationTransform == null)
                {
                    continue;
                }

                Component destinationComponent = destinationTransform.GetComponent(sourceComponent.GetType());
                if (destinationComponent == null)
                {
                    destinationComponent = destinationTransform.gameObject.AddComponent(sourceComponent.GetType());
                }

                if (destinationComponent == null)
                {
                    continue;
                }

                EditorUtility.CopySerialized(sourceComponent, destinationComponent);
            }
        }

        private static Transform FindTransformByIndexedPath(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return root;
            }

            Transform current = root;
            foreach (string part in path.Split('/'))
            {
                if (!int.TryParse(part, out int siblingIndex)
                    || siblingIndex < 0
                    || siblingIndex >= current.childCount)
                {
                    return null;
                }

                current = current.GetChild(siblingIndex);
            }

            return current;
        }

        private static void MapHierarchyObjects(
            GameObject sourceRoot,
            GameObject destinationRoot,
            IDictionary<UnityEngine.Object, UnityEngine.Object> objectMap)
        {
            Dictionary<string, Transform> destinationTransforms = destinationRoot
                .GetComponentsInChildren<Transform>(true)
                .ToDictionary(
                    transform => GetIndexedHierarchyPath(transform, destinationRoot.transform),
                    transform => transform,
                    StringComparer.Ordinal);

            foreach (Transform sourceTransform in sourceRoot.GetComponentsInChildren<Transform>(true))
            {
                string path = GetIndexedHierarchyPath(sourceTransform, sourceRoot.transform);
                if (!destinationTransforms.TryGetValue(path, out Transform destinationTransform))
                {
                    continue;
                }

                objectMap[sourceTransform.gameObject] = destinationTransform.gameObject;
                objectMap[sourceTransform] = destinationTransform;
                Component[] sourceComponents = sourceTransform.GetComponents<Component>();
                Component[] destinationComponents = destinationTransform.GetComponents<Component>();
                foreach (IGrouping<Type, Component> sourceGroup in sourceComponents
                             .Where(component => component != null)
                             .GroupBy(component => component.GetType()))
                {
                    Component[] matchingDestination = destinationComponents
                        .Where(component => component != null && component.GetType() == sourceGroup.Key)
                        .ToArray();
                    Component[] matchingSource = sourceGroup.ToArray();
                    int count = Mathf.Min(matchingSource.Length, matchingDestination.Length);
                    for (int i = 0; i < count; i++)
                    {
                        objectMap[matchingSource[i]] = matchingDestination[i];
                    }
                }
            }
        }

        private static string GetIndexedHierarchyPath(Transform transform, Transform root)
        {
            if (transform == root)
            {
                return string.Empty;
            }

            List<int> indices = new List<int>();
            Transform current = transform;
            while (current != null && current != root)
            {
                indices.Add(current.GetSiblingIndex());
                current = current.parent;
            }

            indices.Reverse();
            return string.Join("/", indices);
        }

        private static int RemapAndClearSceneObjectReferences(
            GameObject exportRoot,
            IReadOnlyDictionary<UnityEngine.Object, UnityEngine.Object> objectMap,
            Scene sourceScene)
        {
            int changedCount = 0;
            foreach (Component component in exportRoot.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                {
                    continue;
                }

                SerializedObject serializedObject = new SerializedObject(component);
                SerializedProperty property = serializedObject.GetIterator();
                bool enterChildren = true;
                bool changed = false;
                while (property.Next(enterChildren))
                {
                    enterChildren = true;
                    if (property.propertyType != SerializedPropertyType.ObjectReference
                        || property.propertyPath == "m_GameObject"
                        || property.propertyPath == "m_Script")
                    {
                        continue;
                    }

                    UnityEngine.Object reference = property.objectReferenceValue;
                    if (reference == null || AssetDatabase.Contains(reference))
                    {
                        continue;
                    }

                    if (!TryGetObjectScene(reference, out Scene referenceScene)
                        || referenceScene != sourceScene)
                    {
                        continue;
                    }

                    property.objectReferenceValue = objectMap.TryGetValue(reference, out UnityEngine.Object mapped)
                        ? mapped
                        : null;
                    changed = true;
                    changedCount++;
                }

                if (changed)
                {
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            return changedCount;
        }

        private static bool TryGetObjectScene(UnityEngine.Object value, out Scene scene)
        {
            if (value is GameObject gameObject)
            {
                scene = gameObject.scene;
                return scene.IsValid();
            }

            if (value is Component component)
            {
                scene = component.gameObject.scene;
                return scene.IsValid();
            }

            scene = default;
            return false;
        }

        private static void CreateCroppedWater(
            WaterCrop crop,
            string meshAssetPath,
            Transform parent,
            Scene destinationScene,
            StageSpace stageSpace)
        {
            Renderer sourceRenderer = crop.Renderer;
            MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
            Mesh sourceMesh = sourceFilter.sharedMesh;
            Vector2 uvMin;
            Vector2 uvMax;
            GetMeshUvRange(sourceMesh, out uvMin, out uvMax);

            Vector3[] worldCorners = crop.WorldPolygon
                .Select(point => new Vector3(point.x, crop.WorldY, point.y))
                .ToArray();
            Vector3[] vertices = worldCorners.Select(stageSpace.WorldToLocalPosition).ToArray();
            Vector2[] uvs = worldCorners
                .Select(world => CalculateWaterUv(sourceRenderer.transform, sourceMesh.bounds, world, uvMin, uvMax))
                .ToArray();
            int[] triangles = new int[Mathf.Max(0, vertices.Length - 2) * 3];
            for (int i = 0; i < vertices.Length - 2; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 2;
                triangles[i * 3 + 2] = i + 1;
            }

            Mesh mesh = new Mesh
            {
                name = Path.GetFileNameWithoutExtension(meshAssetPath),
                vertices = vertices,
                uv = uvs,
                uv2 = uvs,
                normals = Enumerable.Repeat(Vector3.up, vertices.Length).ToArray(),
                triangles = triangles
            };
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, meshAssetPath);

            GameObject waterObject = new GameObject($"{sourceRenderer.gameObject.name}_Cropped");
            SceneManager.MoveGameObjectToScene(waterObject, destinationScene);
            waterObject.transform.SetParent(parent, false);
            waterObject.layer = sourceRenderer.gameObject.layer;
            waterObject.tag = sourceRenderer.gameObject.tag;
            GameObjectUtility.SetStaticEditorFlags(
                waterObject,
                GameObjectUtility.GetStaticEditorFlags(sourceRenderer.gameObject));

            MeshFilter destinationFilter = waterObject.AddComponent<MeshFilter>();
            MeshRenderer destinationRenderer = waterObject.AddComponent<MeshRenderer>();
            destinationFilter.sharedMesh = mesh;
            destinationRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
            destinationRenderer.enabled = sourceRenderer.enabled;
            destinationRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
            destinationRenderer.receiveShadows = sourceRenderer.receiveShadows;
            destinationRenderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
            destinationRenderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
            destinationRenderer.motionVectorGenerationMode = sourceRenderer.motionVectorGenerationMode;
            destinationRenderer.allowOcclusionWhenDynamic = sourceRenderer.allowOcclusionWhenDynamic;
            destinationRenderer.renderingLayerMask = sourceRenderer.renderingLayerMask;
            destinationRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            destinationRenderer.sortingOrder = sourceRenderer.sortingOrder;

            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            sourceRenderer.GetPropertyBlock(propertyBlock);
            destinationRenderer.SetPropertyBlock(propertyBlock);

            MeshCollider sourceCollider = sourceRenderer.GetComponent<MeshCollider>();
            if (sourceCollider != null)
            {
                MeshCollider destinationCollider = waterObject.AddComponent<MeshCollider>();
                destinationCollider.sharedMesh = mesh;
                destinationCollider.sharedMaterial = sourceCollider.sharedMaterial;
                destinationCollider.convex = sourceCollider.convex;
                destinationCollider.isTrigger = sourceCollider.isTrigger;
                destinationCollider.enabled = sourceCollider.enabled;
                destinationCollider.cookingOptions = sourceCollider.cookingOptions;
            }

            waterObject.SetActive(sourceRenderer.gameObject.activeSelf);
        }

        private static Vector2 CalculateWaterUv(
            Transform sourceTransform,
            Bounds sourceMeshBounds,
            Vector3 worldPosition,
            Vector2 uvMin,
            Vector2 uvMax)
        {
            Vector3 localPosition = sourceTransform.InverseTransformPoint(worldPosition);
            float normalizedX = Mathf.InverseLerp(sourceMeshBounds.min.x, sourceMeshBounds.max.x, localPosition.x);
            float normalizedZ = Mathf.InverseLerp(sourceMeshBounds.min.z, sourceMeshBounds.max.z, localPosition.z);
            return new Vector2(
                Mathf.Lerp(uvMin.x, uvMax.x, normalizedX),
                Mathf.Lerp(uvMin.y, uvMax.y, normalizedZ));
        }

        private static void GetMeshUvRange(Mesh mesh, out Vector2 minimum, out Vector2 maximum)
        {
            Vector2[] uvs = mesh.isReadable ? mesh.uv : Array.Empty<Vector2>();
            if (uvs.Length == 0)
            {
                minimum = Vector2.zero;
                maximum = Vector2.one;
                return;
            }

            minimum = uvs[0];
            maximum = uvs[0];
            for (int i = 1; i < uvs.Length; i++)
            {
                minimum = Vector2.Min(minimum, uvs[i]);
                maximum = Vector2.Max(maximum, uvs[i]);
            }
        }

        private static Terrain[] FindTerrains(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return Array.Empty<Terrain>();
            }

            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Terrain>(true))
                .Where(terrain => terrain != null && terrain.terrainData != null)
                .ToArray();
        }

        private static bool TerrainIntersects(Terrain terrain, SelectionRegion region)
        {
            return TryCreateTerrainCrop(terrain, region, out _);
        }

        private static bool TryCreateTerrainCrop(Terrain terrain, SelectionRegion region, out TerrainCrop crop)
        {
            crop = default;
            if (terrain == null || terrain.terrainData == null)
            {
                return false;
            }

            if (Quaternion.Angle(terrain.transform.rotation, Quaternion.identity) > 0.01f
                || Vector3.Distance(terrain.transform.lossyScale, Vector3.one) > 0.001f)
            {
                Debug.LogWarning($"[StageMapSlicer] 회전 또는 스케일이 적용된 Terrain은 제외합니다: {terrain.name}");
                return false;
            }

            Vector3 position = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            List<Vector2> terrainPolygon = new List<Vector2>
            {
                new Vector2(position.x, position.z),
                new Vector2(position.x + size.x, position.z),
                new Vector2(position.x + size.x, position.z + size.z),
                new Vector2(position.x, position.z + size.z)
            };
            List<Vector2> intersection = ClipConvexPolygon(region.WorldPolygon, terrainPolygon);
            if (intersection.Count < 3 || Mathf.Abs(SignedArea(intersection)) <= 0.01f)
            {
                return false;
            }

            Vector2[] localIntersection = intersection.Select(region.WorldToLocal).ToArray();
            float minLocalX = localIntersection.Min(point => point.x);
            float maxLocalX = localIntersection.Max(point => point.x);
            float minLocalZ = localIntersection.Min(point => point.y);
            float maxLocalZ = localIntersection.Max(point => point.y);
            if (maxLocalX - minLocalX <= 0.01f || maxLocalZ - minLocalZ <= 0.01f)
            {
                return false;
            }

            crop = new TerrainCrop(terrain, region, minLocalX, maxLocalX, minLocalZ, maxLocalZ);
            return true;
        }

        private static void CreateCroppedTerrain(
            TerrainCrop crop,
            string terrainAssetPath,
            Transform parent,
            Scene destinationScene,
            StageSpace stageSpace,
            bool drawInstanced,
            bool applyDistanceOverrides,
            float detailDistance,
            float treeDistance,
            float outputScale)
        {
            Terrain sourceTerrain = crop.Terrain;
            TerrainData sourceData = sourceTerrain.terrainData;
            TerrainLayer[] outputTerrainLayers = CreateOutputTerrainLayers(
                sourceData.terrainLayers,
                terrainAssetPath,
                outputScale);
            TerrainData destinationData = new TerrainData
            {
                name = Path.GetFileNameWithoutExtension(terrainAssetPath)
            };
            AssetDatabase.CreateAsset(destinationData, terrainAssetPath); // splat 서브에셋을 먼저 고정

            CopyTerrainSettings(sourceTerrain, destinationData, crop, outputScale, outputTerrainLayers);

            GameObject terrainObject = new GameObject($"{sourceTerrain.name}_Cropped");
            SceneManager.MoveGameObjectToScene(terrainObject, destinationScene);
            terrainObject.transform.SetParent(parent, false);
            terrainObject.layer = sourceTerrain.gameObject.layer;
            terrainObject.tag = sourceTerrain.gameObject.tag;
            GameObjectUtility.SetStaticEditorFlags(
                terrainObject,
                GameObjectUtility.GetStaticEditorFlags(sourceTerrain.gameObject));
            terrainObject.transform.position = new Vector3(
                crop.MinLocalX * outputScale,
                (sourceTerrain.transform.position.y - stageSpace.Origin.y) * outputScale,
                crop.MinLocalZ * outputScale);

            Terrain destinationTerrain = terrainObject.AddComponent<Terrain>();
            TerrainCollider destinationCollider = terrainObject.AddComponent<TerrainCollider>();
            destinationTerrain.terrainData = destinationData;
            destinationCollider.terrainData = destinationData;
            CopyTerrainComponentSettings(sourceTerrain, destinationTerrain, destinationCollider);
            destinationTerrain.drawInstanced = drawInstanced;
            ScaleTerrainWorldDistances(destinationTerrain, outputScale);
            if (applyDistanceOverrides)
            {
                destinationTerrain.detailObjectDistance = Mathf.Max(0f, detailDistance * outputScale);
                destinationTerrain.treeDistance = Mathf.Max(0f, treeDistance * outputScale);
                destinationTerrain.treeBillboardDistance = Mathf.Min(
                    destinationTerrain.treeBillboardDistance,
                    destinationTerrain.treeDistance);
            }

            terrainObject.SetActive(sourceTerrain.gameObject.activeSelf);
        }

        private static void CopyTerrainSettings(
            Terrain sourceTerrain,
            TerrainData destination,
            TerrainCrop crop,
            float outputScale,
            TerrainLayer[] outputTerrainLayers)
        {
            TerrainData source = sourceTerrain.terrainData;
            float normalizedWidth = crop.Width / Mathf.Max(0.0001f, source.size.x);
            float normalizedDepth = crop.Depth / Mathf.Max(0.0001f, source.size.z);
            float normalizedMaxSize = Mathf.Max(normalizedWidth, normalizedDepth);

            int heightResolution = source.heightmapResolution;
            destination.heightmapResolution = heightResolution;
            destination.size = new Vector3(
                crop.Width * outputScale,
                source.size.y * outputScale,
                crop.Depth * outputScale);
            destination.SetHeights(0, 0, CropAndResampleHeights(sourceTerrain, crop, heightResolution));

            destination.terrainLayers = outputTerrainLayers;
            if (source.alphamapLayers > 0 && source.alphamapResolution > 0)
            {
                int alphaResolution = source.alphamapResolution;
                destination.alphamapResolution = alphaResolution;
                destination.baseMapResolution = source.baseMapResolution;
                destination.SetAlphamaps(0, 0, CropAndResampleAlphamaps(sourceTerrain, crop, alphaResolution));
            }

            CopyHoles(sourceTerrain, destination, crop);
            CopyDetails(sourceTerrain, destination, crop, normalizedMaxSize, outputScale);
            CopyTrees(sourceTerrain, destination, crop, outputScale);

            destination.wavingGrassAmount = source.wavingGrassAmount;
            destination.wavingGrassSpeed = source.wavingGrassSpeed;
            destination.wavingGrassStrength = source.wavingGrassStrength;
            destination.wavingGrassTint = source.wavingGrassTint;
            destination.SetBaseMapDirty();
        }

        private static TerrainLayer[] CreateOutputTerrainLayers(
            IReadOnlyList<TerrainLayer> sourceLayers,
            string terrainAssetPath,
            float outputScale)
        {
            if (sourceLayers == null || sourceLayers.Count == 0)
            {
                return Array.Empty<TerrainLayer>();
            }

            if (Mathf.Approximately(outputScale, 1f))
            {
                return sourceLayers.ToArray();
            }

            string folder = Path.GetDirectoryName(terrainAssetPath)?.Replace('\\', '/');
            string terrainName = Path.GetFileNameWithoutExtension(terrainAssetPath);
            TerrainLayer[] outputLayers = new TerrainLayer[sourceLayers.Count];
            for (int i = 0; i < sourceLayers.Count; i++)
            {
                TerrainLayer sourceLayer = sourceLayers[i];
                if (sourceLayer == null)
                {
                    continue;
                }

                TerrainLayer outputLayer = UnityEngine.Object.Instantiate(sourceLayer);
                outputLayer.name = $"{sourceLayer.name}_Scaled";
                outputLayer.tileSize = sourceLayer.tileSize * outputScale;
                outputLayer.tileOffset = sourceLayer.tileOffset * outputScale;
                string layerPath = $"{folder}/{terrainName}_TerrainLayer_{i + 1:00}.terrainlayer";
                AssetDatabase.CreateAsset(outputLayer, layerPath);
                outputLayers[i] = outputLayer;
            }

            return outputLayers;
        }

        private static float[,] CropAndResampleHeights(
            Terrain sourceTerrain,
            TerrainCrop crop,
            int destinationResolution)
        {
            TerrainData source = sourceTerrain.terrainData;
            int sourceResolution = source.heightmapResolution;
            GetSourceSampleWindow(sourceTerrain, crop, sourceResolution, out int x0, out int x1, out int z0, out int z1);
            float[,] sourceValues = source.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            float[,] result = new float[destinationResolution, destinationResolution];
            SampleGrid sampleGrid = CreateSampleGrid(sourceTerrain, crop, sourceResolution, destinationResolution, x0, z0);

            for (int z = 0; z < destinationResolution; z++)
            {
                float rowStartX = sampleGrid.StartX + sampleGrid.RowStepX * z;
                float rowStartZ = sampleGrid.StartZ + sampleGrid.RowStepZ * z;
                for (int x = 0; x < destinationResolution; x++)
                {
                    float sampleX = rowStartX + sampleGrid.ColumnStepX * x;
                    float sampleZ = rowStartZ + sampleGrid.ColumnStepZ * x;
                    result[z, x] = SampleBilinear(sourceValues, sampleX, sampleZ);
                }
            }

            return result;
        }

        private static float[,,] CropAndResampleAlphamaps(
            Terrain sourceTerrain,
            TerrainCrop crop,
            int destinationResolution)
        {
            TerrainData source = sourceTerrain.terrainData;
            int sourceResolution = source.alphamapResolution;
            int layerCount = source.alphamapLayers;
            GetSourceSampleWindow(sourceTerrain, crop, sourceResolution, out int x0, out int x1, out int z0, out int z1);
            float[,,] sourceValues = source.GetAlphamaps(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            float[,,] result = new float[destinationResolution, destinationResolution, layerCount];
            SampleGrid sampleGrid = CreateSampleGrid(sourceTerrain, crop, sourceResolution, destinationResolution, x0, z0);

            for (int z = 0; z < destinationResolution; z++)
            {
                float rowStartX = sampleGrid.StartX + sampleGrid.RowStepX * z;
                float rowStartZ = sampleGrid.StartZ + sampleGrid.RowStepZ * z;
                for (int x = 0; x < destinationResolution; x++)
                {
                    float sampleX = rowStartX + sampleGrid.ColumnStepX * x;
                    float sampleZ = rowStartZ + sampleGrid.ColumnStepZ * x;
                    float sum = 0f;
                    for (int layer = 0; layer < layerCount; layer++)
                    {
                        float value = SampleBilinear(sourceValues, sampleX, sampleZ, layer);
                        result[z, x, layer] = value;
                        sum += value;
                    }

                    if (sum > 0.00001f)
                    {
                        for (int layer = 0; layer < layerCount; layer++)
                        {
                            result[z, x, layer] /= sum;
                        }
                    }
                    else if (layerCount > 0)
                    {
                        result[z, x, 0] = 1f;
                    }

                }
            }

            return result;
        }

        private static void CopyHoles(Terrain sourceTerrain, TerrainData destination, TerrainCrop crop)
        {
            TerrainData source = sourceTerrain.terrainData;
            int sourceResolution = source.holesResolution;
            int destinationResolution = destination.holesResolution;
            if (sourceResolution <= 0 || destinationResolution <= 0)
            {
                return;
            }

            GetSourceSampleWindow(sourceTerrain, crop, sourceResolution, out int x0, out int x1, out int z0, out int z1);
            bool[,] sourceValues = source.GetHoles(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            bool[,] result = new bool[destinationResolution, destinationResolution];
            SampleGrid sampleGrid = CreateSampleGrid(sourceTerrain, crop, sourceResolution, destinationResolution, x0, z0);

            for (int z = 0; z < destinationResolution; z++)
            {
                float tZ = destinationResolution == 1 ? 0f : (float)z / (destinationResolution - 1);
                float rowStartX = sampleGrid.StartX + sampleGrid.RowStepX * z;
                float rowStartZ = sampleGrid.StartZ + sampleGrid.RowStepZ * z;
                for (int x = 0; x < destinationResolution; x++)
                {
                    float tX = destinationResolution == 1 ? 0f : (float)x / (destinationResolution - 1);
                    Vector2 world = crop.GetWorldPoint(tX, tZ);
                    float sampleXFloat = rowStartX + sampleGrid.ColumnStepX * x;
                    float sampleZFloat = rowStartZ + sampleGrid.ColumnStepZ * x;
                    int sampleX = Mathf.Clamp(Mathf.RoundToInt(sampleXFloat), 0, sourceValues.GetLength(1) - 1);
                    int sampleZ = Mathf.Clamp(Mathf.RoundToInt(sampleZFloat), 0, sourceValues.GetLength(0) - 1);
                    result[z, x] = crop.ContainsWorldPoint(world)
                                   && IsInsideTerrain(sourceTerrain, world)
                                   && sourceValues[sampleZ, sampleX];
                }
            }

            destination.SetHoles(0, 0, result);
        }

        private static void CopyDetails(
            Terrain sourceTerrain,
            TerrainData destination,
            TerrainCrop crop,
            float normalizedMaxSize,
            float outputScale)
        {
            TerrainData source = sourceTerrain.terrainData;
            DetailPrototype[] prototypes = source.detailPrototypes
                .Select(prototype =>
                {
                    DetailPrototype scaled = new DetailPrototype(prototype)
                    {
                        minWidth = prototype.minWidth * outputScale,
                        maxWidth = prototype.maxWidth * outputScale,
                        minHeight = prototype.minHeight * outputScale,
                        maxHeight = prototype.maxHeight * outputScale
                    };
                    return scaled;
                })
                .ToArray();
            if (prototypes == null || prototypes.Length == 0 || source.detailResolution <= 0)
            {
                return;
            }

            int patchSize = Mathf.Max(8, source.detailResolutionPerPatch);
            int rawResolution = Mathf.Max(patchSize, Mathf.CeilToInt(source.detailResolution * normalizedMaxSize));
            int destinationResolution = Mathf.Clamp(
                Mathf.CeilToInt(rawResolution / (float)patchSize) * patchSize,
                patchSize,
                4048);

            destination.SetDetailResolution(destinationResolution, patchSize);
            destination.detailPrototypes = prototypes;

            int sourceResolution = source.detailResolution;
            GetSourceSampleWindow(sourceTerrain, crop, sourceResolution, out int x0, out int x1, out int z0, out int z1);

            for (int layer = 0; layer < prototypes.Length; layer++)
            {
                int[,] sourceValues = source.GetDetailLayer(x0, z0, x1 - x0 + 1, z1 - z0 + 1, layer);
                int[,] result = new int[destinationResolution, destinationResolution];
                SampleGrid sampleGrid = CreateSampleGrid(sourceTerrain, crop, sourceResolution, destinationResolution, x0, z0);
                for (int z = 0; z < destinationResolution; z++)
                {
                    float tZ = destinationResolution == 1 ? 0f : (float)z / (destinationResolution - 1);
                    float rowStartX = sampleGrid.StartX + sampleGrid.RowStepX * z;
                    float rowStartZ = sampleGrid.StartZ + sampleGrid.RowStepZ * z;
                    for (int x = 0; x < destinationResolution; x++)
                    {
                        float tX = destinationResolution == 1 ? 0f : (float)x / (destinationResolution - 1);
                        Vector2 world = crop.GetWorldPoint(tX, tZ);
                        float sampleXFloat = rowStartX + sampleGrid.ColumnStepX * x;
                        float sampleZFloat = rowStartZ + sampleGrid.ColumnStepZ * x;
                        if (!crop.ContainsWorldPoint(world) || !IsInsideTerrain(sourceTerrain, world))
                        {
                            result[z, x] = 0;
                            continue;
                        }

                        int sampleX = Mathf.Clamp(Mathf.RoundToInt(sampleXFloat), 0, sourceValues.GetLength(1) - 1);
                        int sampleZ = Mathf.Clamp(Mathf.RoundToInt(sampleZFloat), 0, sourceValues.GetLength(0) - 1);
                        result[z, x] = sourceValues[sampleZ, sampleX];
                    }
                }

                destination.SetDetailLayer(0, 0, layer, result);
            }
        }

        private static void CopyTrees(
            Terrain sourceTerrain,
            TerrainData destination,
            TerrainCrop crop,
            float outputScale)
        {
            TerrainData source = sourceTerrain.terrainData;
            destination.treePrototypes = source.treePrototypes;
            List<TreeInstance> instances = new List<TreeInstance>();

            foreach (TreeInstance sourceInstance in source.treeInstances)
            {
                Vector3 position = sourceInstance.position;
                Vector2 world = new Vector2(
                    sourceTerrain.transform.position.x + position.x * source.size.x,
                    sourceTerrain.transform.position.z + position.z * source.size.z);
                if (!crop.ContainsWorldPoint(world))
                {
                    continue;
                }

                Vector2 local = crop.Region.WorldToLocal(world);
                TreeInstance destinationInstance = sourceInstance;
                destinationInstance.position = new Vector3(
                    Mathf.Clamp01((local.x - crop.MinLocalX) / crop.Width),
                    position.y,
                    Mathf.Clamp01((local.y - crop.MinLocalZ) / crop.Depth));
                destinationInstance.widthScale *= outputScale;
                destinationInstance.heightScale *= outputScale;
                instances.Add(destinationInstance);
            }

            destination.SetTreeInstances(instances.ToArray(), true);
        }

        private static void GetSourceSampleWindow(
            Terrain sourceTerrain,
            TerrainCrop crop,
            int sourceResolution,
            out int x0,
            out int x1,
            out int z0,
            out int z1)
        {
            Vector2[] worldCorners = crop.GetWorldBoundsCorners();
            Vector3 terrainPosition = sourceTerrain.transform.position;
            Vector3 terrainSize = sourceTerrain.terrainData.size;
            float minNormalizedX = worldCorners.Min(point => Mathf.InverseLerp(terrainPosition.x, terrainPosition.x + terrainSize.x, point.x));
            float maxNormalizedX = worldCorners.Max(point => Mathf.InverseLerp(terrainPosition.x, terrainPosition.x + terrainSize.x, point.x));
            float minNormalizedZ = worldCorners.Min(point => Mathf.InverseLerp(terrainPosition.z, terrainPosition.z + terrainSize.z, point.y));
            float maxNormalizedZ = worldCorners.Max(point => Mathf.InverseLerp(terrainPosition.z, terrainPosition.z + terrainSize.z, point.y));
            x0 = Mathf.Clamp(Mathf.FloorToInt(minNormalizedX * (sourceResolution - 1)), 0, sourceResolution - 1);
            x1 = Mathf.Clamp(Mathf.CeilToInt(maxNormalizedX * (sourceResolution - 1)), x0, sourceResolution - 1);
            z0 = Mathf.Clamp(Mathf.FloorToInt(minNormalizedZ * (sourceResolution - 1)), 0, sourceResolution - 1);
            z1 = Mathf.Clamp(Mathf.CeilToInt(maxNormalizedZ * (sourceResolution - 1)), z0, sourceResolution - 1);
        }

        private static void GetSourceSampleCoordinate(
            Terrain sourceTerrain,
            Vector2 world,
            int sourceResolution,
            int x0,
            int z0,
            out float sampleX,
            out float sampleZ)
        {
            Vector3 position = sourceTerrain.transform.position;
            Vector3 size = sourceTerrain.terrainData.size;
            float normalizedX = Mathf.InverseLerp(position.x, position.x + size.x, world.x);
            float normalizedZ = Mathf.InverseLerp(position.z, position.z + size.z, world.y);
            sampleX = normalizedX * (sourceResolution - 1) - x0;
            sampleZ = normalizedZ * (sourceResolution - 1) - z0;
        }

        private static SampleGrid CreateSampleGrid(
            Terrain sourceTerrain,
            TerrainCrop crop,
            int sourceResolution,
            int destinationResolution,
            int x0,
            int z0)
        {
            float step = destinationResolution <= 1 ? 0f : 1f / (destinationResolution - 1);
            Vector2 world00 = crop.GetWorldPoint(0f, 0f);
            Vector2 world10 = crop.GetWorldPoint(step, 0f);
            Vector2 world01 = crop.GetWorldPoint(0f, step);
            GetSourceSampleCoordinate(sourceTerrain, world00, sourceResolution, x0, z0, out float startX, out float startZ);
            GetSourceSampleCoordinate(sourceTerrain, world10, sourceResolution, x0, z0, out float nextColumnX, out float nextColumnZ);
            GetSourceSampleCoordinate(sourceTerrain, world01, sourceResolution, x0, z0, out float nextRowX, out float nextRowZ);
            return new SampleGrid(
                startX,
                startZ,
                nextColumnX - startX,
                nextColumnZ - startZ,
                nextRowX - startX,
                nextRowZ - startZ);
        }

        private static bool IsInsideTerrain(Terrain terrain, Vector2 world)
        {
            Vector3 position = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            return world.x >= position.x && world.x <= position.x + size.x
                   && world.y >= position.z && world.y <= position.z + size.z;
        }

        private static void CopyTerrainComponentSettings(Terrain source, Terrain destination, TerrainCollider destinationCollider)
        {
            CopySerializedComponentSettings(source, destination, "m_TerrainData");

            TerrainCollider sourceCollider = source.GetComponent<TerrainCollider>();
            if (sourceCollider != null)
            {
                CopySerializedComponentSettings(sourceCollider, destinationCollider, "m_TerrainData");
            }
        }

        private static void ScaleTerrainWorldDistances(Terrain terrain, float outputScale)
        {
            terrain.detailObjectDistance *= outputScale;
            terrain.treeDistance *= outputScale;
            terrain.treeBillboardDistance *= outputScale;
            terrain.treeCrossFadeLength *= outputScale;
            terrain.basemapDistance *= outputScale;
        }

        private static void CopySerializedComponentSettings(
            Component source,
            Component destination,
            params string[] excludedPropertyPaths)
        {
            HashSet<string> excluded = new HashSet<string>(excludedPropertyPaths, StringComparer.Ordinal)
            {
                "m_GameObject",
                "m_Script",
                "m_CorrespondingSourceObject",
                "m_PrefabInstance",
                "m_PrefabAsset"
            };
            SerializedObject sourceObject = new SerializedObject(source);
            SerializedObject destinationObject = new SerializedObject(destination);
            SerializedProperty property = sourceObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.depth == 0 && !excluded.Contains(property.propertyPath))
                {
                    destinationObject.CopyFromSerializedProperty(property);
                }
            }

            destinationObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private float GetDisplayGroundY(Scene scene)
        {
            Vector3 samplePosition = new Vector3(centerXZ.x, 0f, centerXZ.y);
            foreach (Terrain terrain in GetCachedTerrains(scene))
            {
                Vector3 terrainPosition = terrain.transform.position;
                Vector3 terrainSize = terrain.terrainData.size;
                bool inside = samplePosition.x >= terrainPosition.x
                              && samplePosition.x <= terrainPosition.x + terrainSize.x
                              && samplePosition.z >= terrainPosition.z
                              && samplePosition.z <= terrainPosition.z + terrainSize.z;
                if (inside)
                {
                    return terrain.SampleHeight(samplePosition) + terrainPosition.y + 0.1f;
                }
            }

            return 0f;
        }

        private SelectionRegion GetSelectionRegion()
        {
            return new SelectionRegion(centerXZ, sizeXZ, rotationY, sliceShape);
        }

        private static float NormalizeAngle(float angle)
        {
            return Mathf.DeltaAngle(0f, angle);
        }

        private static string GetShapeLabel(SliceShape shape)
        {
            return shape == SliceShape.Hexagon ? "육각형" : "사각형";
        }

        private static List<Vector2> ClipConvexPolygon(
            IReadOnlyList<Vector2> subjectPolygon,
            IReadOnlyList<Vector2> clipPolygon)
        {
            List<Vector2> output = subjectPolygon.ToList();
            if (output.Count < 3 || clipPolygon.Count < 3)
            {
                return new List<Vector2>();
            }

            float orientation = Mathf.Sign(SignedArea(clipPolygon));
            for (int edgeIndex = 0; edgeIndex < clipPolygon.Count; edgeIndex++)
            {
                Vector2 edgeStart = clipPolygon[edgeIndex];
                Vector2 edgeEnd = clipPolygon[(edgeIndex + 1) % clipPolygon.Count];
                List<Vector2> input = output;
                output = new List<Vector2>();
                if (input.Count == 0)
                {
                    break;
                }

                Vector2 previous = input[input.Count - 1];
                bool previousInside = IsInsideClipEdge(previous, edgeStart, edgeEnd, orientation);
                foreach (Vector2 current in input)
                {
                    bool currentInside = IsInsideClipEdge(current, edgeStart, edgeEnd, orientation);
                    if (currentInside)
                    {
                        if (!previousInside)
                        {
                            output.Add(LineIntersection(previous, current, edgeStart, edgeEnd));
                        }

                        output.Add(current);
                    }
                    else if (previousInside)
                    {
                        output.Add(LineIntersection(previous, current, edgeStart, edgeEnd));
                    }

                    previous = current;
                    previousInside = currentInside;
                }
            }

            return RemoveNearDuplicatePoints(output);
        }

        private static bool IsInsideClipEdge(
            Vector2 point,
            Vector2 edgeStart,
            Vector2 edgeEnd,
            float orientation)
        {
            return Cross(edgeEnd - edgeStart, point - edgeStart) * orientation >= -0.0001f;
        }

        private static Vector2 LineIntersection(
            Vector2 lineStart,
            Vector2 lineEnd,
            Vector2 edgeStart,
            Vector2 edgeEnd)
        {
            Vector2 lineDirection = lineEnd - lineStart;
            Vector2 edgeDirection = edgeEnd - edgeStart;
            float denominator = Cross(lineDirection, edgeDirection);
            if (Mathf.Abs(denominator) <= 0.000001f)
            {
                return lineEnd;
            }

            float t = Cross(edgeStart - lineStart, edgeDirection) / denominator;
            return lineStart + lineDirection * t;
        }

        private static List<Vector2> RemoveNearDuplicatePoints(IReadOnlyList<Vector2> points)
        {
            List<Vector2> result = new List<Vector2>();
            foreach (Vector2 point in points)
            {
                if (result.Count == 0 || Vector2.SqrMagnitude(result[result.Count - 1] - point) > 0.000001f)
                {
                    result.Add(point);
                }
            }

            if (result.Count > 1 && Vector2.SqrMagnitude(result[0] - result[result.Count - 1]) <= 0.000001f)
            {
                result.RemoveAt(result.Count - 1);
            }

            return result;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static float SignedArea(IReadOnlyList<Vector2> polygon)
        {
            float area = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 current = polygon[i];
                Vector2 next = polygon[(i + 1) % polygon.Count];
                area += current.x * next.y - next.x * current.y;
            }

            return area * 0.5f;
        }

        private static int ChooseHeightResolution(int sourceResolution, float normalizedSize)
        {
            int sampleSpan = Mathf.Max(32, Mathf.CeilToInt((sourceResolution - 1) * normalizedSize));
            return Mathf.Clamp(Mathf.NextPowerOfTwo(sampleSpan) + 1, 33, sourceResolution);
        }

        private static int ChooseTextureResolution(int sourceResolution, float normalizedSize, int minimum)
        {
            int desired = Mathf.Max(minimum, Mathf.CeilToInt(sourceResolution * normalizedSize));
            return Mathf.Clamp(Mathf.NextPowerOfTwo(desired), minimum, sourceResolution);
        }

        private static float SampleBilinear(float[,] values, float x, float y)
        {
            int maxX = values.GetLength(1) - 1;
            int maxY = values.GetLength(0) - 1;
            x = Mathf.Clamp(x, 0f, maxX);
            y = Mathf.Clamp(y, 0f, maxY);
            int x0 = Mathf.FloorToInt(x);
            int x1 = Mathf.Min(x0 + 1, maxX);
            int y0 = Mathf.FloorToInt(y);
            int y1 = Mathf.Min(y0 + 1, maxY);
            float tx = x - x0;
            float ty = y - y0;
            return Mathf.Lerp(
                Mathf.Lerp(values[y0, x0], values[y0, x1], tx),
                Mathf.Lerp(values[y1, x0], values[y1, x1], tx),
                ty);
        }

        private static float SampleBilinear(float[,,] values, float x, float y, int layer)
        {
            int maxX = values.GetLength(1) - 1;
            int maxY = values.GetLength(0) - 1;
            x = Mathf.Clamp(x, 0f, maxX);
            y = Mathf.Clamp(y, 0f, maxY);
            int x0 = Mathf.FloorToInt(x);
            int x1 = Mathf.Min(x0 + 1, maxX);
            int y0 = Mathf.FloorToInt(y);
            int y1 = Mathf.Min(y0 + 1, maxY);
            float tx = x - x0;
            float ty = y - y0;
            return Mathf.Lerp(
                Mathf.Lerp(values[y0, x0, layer], values[y0, x1, layer], tx),
                Mathf.Lerp(values[y1, x0, layer], values[y1, x1, layer], tx),
                ty);
        }

        private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return true;
        }

        private static float GetLowestModelY(IEnumerable<GameObject> roots)
        {
            float lowest = float.PositiveInfinity;
            foreach (GameObject root in roots)
            {
                if (root != null && TryGetRendererBounds(root, out Bounds bounds))
                {
                    lowest = Mathf.Min(lowest, bounds.min.y);
                }
            }

            return float.IsPositiveInfinity(lowest) ? 0f : lowest;
        }

        private static int CountMissingScripts(GameObject root)
        {
            int count = 0;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                count += child.GetComponents<Component>().Count(component => component == null);
            }

            return count;
        }

        private static void ValidateGeneratedOptimizations(
            GameObject prefab,
            IReadOnlyList<TerrainCrop> terrainCrops,
            int expectedDisabledColliders,
            int expectedDisabledShadowRenderers,
            int expectedCullingCells,
            string expectedStageFolder,
            IReadOnlyCollection<string> expectedInstancedMaterialPaths,
            int expectedGpuInstancedRenderers,
            IReadOnlyDictionary<Material, bool> sourceMaterialInstancingStates,
            bool expectedDrawInstanced,
            bool expectedDistanceOverrides,
            float expectedDetailDistance,
            float expectedTreeDistance,
            float expectedOutputScale)
        {
            if (prefab == null)
            {
                throw new InvalidOperationException("생성 프리팹 최적화 검증 대상이 없습니다.");
            }

            int disabledColliders = prefab.GetComponentsInChildren<Collider>(true)
                .Count(collider => !collider.enabled);
            int disabledShadowRenderers = prefab.GetComponentsInChildren<Renderer>(true)
                .Count(renderer => renderer.shadowCastingMode == ShadowCastingMode.Off);
            if (disabledColliders < expectedDisabledColliders)
            {
                throw new InvalidOperationException(
                    $"Collider 비활성화 저장 검증 실패: 예상 {expectedDisabledColliders:N0}, 저장 {disabledColliders:N0}");
            }

            if (disabledShadowRenderers < expectedDisabledShadowRenderers)
            {
                throw new InvalidOperationException(
                    $"식생 그림자 저장 검증 실패: 예상 {expectedDisabledShadowRenderers:N0}, 저장 {disabledShadowRenderers:N0}");
            }

            foreach (KeyValuePair<Material, bool> state in sourceMaterialInstancingStates)
            {
                if (state.Key != null && state.Key.enableInstancing != state.Value)
                {
                    throw new InvalidOperationException($"원본 Material 변경 감지: {state.Key.name}");
                }
            }

            HashSet<Material> savedPrefabMaterials = new HashSet<Material>(
                prefab.GetComponentsInChildren<MeshRenderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null));
            string expectedMaterialFolder = $"{NormalizeAssetFolder(expectedStageFolder)}/InstancedMaterials/";
            HashSet<Material> expectedInstancedMaterials = new HashSet<Material>();
            foreach (string materialPath in expectedInstancedMaterialPaths)
            {
                if (string.IsNullOrEmpty(materialPath)
                    || !materialPath.StartsWith(expectedMaterialFolder, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Instancing Material 저장 위치 검증 실패: {materialPath}");
                }

                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null || material.enableInstancing)
                {
                    throw new InvalidOperationException($"DX12 Editor 안전 Material 저장 검증 실패: {materialPath}");
                }

                if (!savedPrefabMaterials.Contains(material))
                {
                    throw new InvalidOperationException($"생성 프리팹 Material Override 검증 실패: {materialPath}");
                }

                if (!ShaderSupportsRendererScopedGpuInstancing(material.shader, out string reason))
                {
                    throw new InvalidOperationException(
                        $"실제 GPU Instancing Shader 조건 검증 실패: {materialPath} ({reason})");
                }

                expectedInstancedMaterials.Add(material);
            }

            StageVegetationGpuInstancingEnabler gpuInstancingEnabler =
                prefab.GetComponent<StageVegetationGpuInstancingEnabler>();
            bool expectsInstancingController = expectedInstancedMaterials.Count > 0
                || expectedDrawInstanced && terrainCrops.Count > 0;
            if (expectsInstancingController)
            {
                if (gpuInstancingEnabler == null)
                {
                    throw new InvalidOperationException("GPU Instancing 적용 컴포넌트 저장 검증 실패");
                }

                HashSet<Material> configuredMaterials = new HashSet<Material>(
                    gpuInstancingEnabler.TargetMaterials.Where(material => material != null));
                if (!configuredMaterials.SetEquals(expectedInstancedMaterials))
                {
                    throw new InvalidOperationException("GPU Instancing 대상 Material 저장 검증 실패");
                }

                if (gpuInstancingEnabler.EnableTerrainDrawInstanced != expectedDrawInstanced)
                {
                    throw new InvalidOperationException("Terrain Draw Instanced 런타임 설정 저장 검증 실패");
                }

                if (expectedInstancedMaterials.Count > 0)
                {
                    int savedTargetRenderers = prefab.GetComponentsInChildren<MeshRenderer>(true)
                        .Count(renderer => renderer.sharedMaterials.Any(configuredMaterials.Contains));
                    if (savedTargetRenderers != expectedGpuInstancedRenderers)
                    {
                        throw new InvalidOperationException(
                            $"GPU Instancing 대상 Renderer 저장 검증 실패: 예상 {expectedGpuInstancedRenderers:N0}, 저장 {savedTargetRenderers:N0}");
                    }
                }
            }
            else if (gpuInstancingEnabler != null)
            {
                throw new InvalidOperationException("대상 없는 GPU Instancing 컴포넌트가 저장되었습니다.");
            }

            StageVegetationDistanceCuller culler = prefab.GetComponent<StageVegetationDistanceCuller>();
            if (expectedCullingCells > 0
                && (culler == null || culler.CellCount != expectedCullingCells))
            {
                throw new InvalidOperationException("식생 거리 컬링 컴포넌트 저장 검증 실패");
            }

            Terrain[] terrains = prefab.GetComponentsInChildren<Terrain>(true);
            if (terrains.Length != terrainCrops.Count)
            {
                throw new InvalidOperationException(
                    $"Terrain 저장 개수 검증 실패: 예상 {terrainCrops.Count:N0}, 저장 {terrains.Length:N0}");
            }

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain destination = terrains[i];
                TerrainData sourceData = terrainCrops[i].Terrain.terrainData;
                TerrainData destinationData = destination.terrainData;
                if (destinationData == null
                    || destinationData.detailPrototypes.Length != sourceData.detailPrototypes.Length
                    || destinationData.treePrototypes.Length != sourceData.treePrototypes.Length)
                {
                    throw new InvalidOperationException($"Terrain {i + 1} Detail/Tree 데이터 보존 검증 실패");
                }

                Vector3 expectedSize = new Vector3(
                    terrainCrops[i].Width * expectedOutputScale,
                    sourceData.size.y * expectedOutputScale,
                    terrainCrops[i].Depth * expectedOutputScale);
                if (Vector3.Distance(destinationData.size, expectedSize) > 0.001f)
                {
                    throw new InvalidOperationException(
                        $"Terrain {i + 1} 출력 배율 검증 실패: 예상 {expectedSize}, 저장 {destinationData.size}");
                }

                TerrainLayer[] sourceLayers = sourceData.terrainLayers;
                TerrainLayer[] destinationLayers = destinationData.terrainLayers;
                if (sourceLayers.Length != destinationLayers.Length)
                {
                    throw new InvalidOperationException($"Terrain {i + 1} Layer 개수 검증 실패");
                }

                for (int layerIndex = 0; layerIndex < sourceLayers.Length; layerIndex++)
                {
                    TerrainLayer sourceLayer = sourceLayers[layerIndex];
                    TerrainLayer destinationLayer = destinationLayers[layerIndex];
                    if (sourceLayer == null || destinationLayer == null)
                    {
                        if (sourceLayer != destinationLayer)
                        {
                            throw new InvalidOperationException($"Terrain {i + 1} Layer {layerIndex + 1} 누락");
                        }

                        continue;
                    }

                    Vector2 expectedTileSize = sourceLayer.tileSize * expectedOutputScale;
                    Vector2 expectedTileOffset = sourceLayer.tileOffset * expectedOutputScale;
                    if (Vector2.Distance(destinationLayer.tileSize, expectedTileSize) > 0.001f
                        || Vector2.Distance(destinationLayer.tileOffset, expectedTileOffset) > 0.001f)
                    {
                        throw new InvalidOperationException(
                            $"Terrain {i + 1} Layer {layerIndex + 1} 출력 배율 검증 실패");
                    }

                    if (!Mathf.Approximately(expectedOutputScale, 1f) && sourceLayer == destinationLayer)
                    {
                        throw new InvalidOperationException(
                            $"Terrain {i + 1} Layer {layerIndex + 1} 원본 공유 감지");
                    }
                }

                if (destination.drawInstanced)
                {
                    throw new InvalidOperationException($"Terrain {i + 1} DX12 Editor 안전값 저장 검증 실패");
                }

                if (expectedDistanceOverrides
                    && (!Mathf.Approximately(
                            destination.detailObjectDistance,
                            expectedDetailDistance * expectedOutputScale)
                        || !Mathf.Approximately(
                            destination.treeDistance,
                            expectedTreeDistance * expectedOutputScale)))
                {
                    throw new InvalidOperationException($"Terrain {i + 1} 식생 거리 저장 검증 실패");
                }
            }
        }

        private static int CountMissingMaterials(GameObject root)
        {
            int count = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                if (renderer is ParticleSystemRenderer)
                {
                    count += materials.Length > 0 && materials[0] == null ? 1 : 0; // Trail 슬롯은 비어 있어도 정상
                }
                else
                {
                    count += materials.Count(material => material == null);
                }
            }

            return count;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}/{path}";
            }

            return path;
        }

        private static string NormalizeAssetFolder(string path)
        {
            string normalized = string.IsNullOrWhiteSpace(path) ? DefaultOutputRoot : path.Trim();
            normalized = normalized.Replace('\\', '/').TrimEnd('/');
            return normalized;
        }

        private static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string sanitized = value.Trim();
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalidCharacter, '_');
            }

            return sanitized.Replace(' ', '_');
        }

        private static void EnsureAssetFolder(string path)
        {
            string normalized = NormalizeAssetFolder(path);
            if (normalized == "Assets")
            {
                return;
            }

            string[] parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                throw new ArgumentException($"Assets 아래의 경로만 사용할 수 있습니다: {path}");
            }

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private readonly struct SelectionRegion
        {
            private readonly Quaternion rotation;
            private readonly Quaternion inverseRotation;

            public SelectionRegion(Vector2 center, Vector2 size, float rotationY, SliceShape shape)
            {
                Center = center;
                Size = new Vector2(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y));
                Shape = shape;
                RotationY = rotationY;
                rotation = Quaternion.Euler(0f, rotationY, 0f);
                inverseRotation = Quaternion.Inverse(rotation);
                Vector2 half = Size * 0.5f;
                Vector2[] localPolygon = shape == SliceShape.Hexagon
                    ? new[]
                    {
                        new Vector2(-half.x, 0f),
                        new Vector2(-half.x * 0.5f, -half.y),
                        new Vector2(half.x * 0.5f, -half.y),
                        new Vector2(half.x, 0f),
                        new Vector2(half.x * 0.5f, half.y),
                        new Vector2(-half.x * 0.5f, half.y)
                    }
                    : new[]
                    {
                        new Vector2(-half.x, -half.y),
                        new Vector2(half.x, -half.y),
                        new Vector2(half.x, half.y),
                        new Vector2(-half.x, half.y)
                    };
                LocalPolygon = localPolygon;
                Vector2[] worldPolygon = new Vector2[localPolygon.Length];
                for (int i = 0; i < localPolygon.Length; i++)
                {
                    Vector3 rotated = rotation * new Vector3(localPolygon[i].x, 0f, localPolygon[i].y);
                    worldPolygon[i] = center + new Vector2(rotated.x, rotated.z);
                }

                WorldPolygon = worldPolygon;
            }

            public Vector2 Center { get; }
            public Vector2 Size { get; }
            public SliceShape Shape { get; }
            public float RotationY { get; }
            public Vector2[] LocalPolygon { get; }
            public Vector2[] WorldPolygon { get; }

            public Vector2 LocalToWorld(Vector2 local)
            {
                Vector3 rotated = rotation * new Vector3(local.x, 0f, local.y);
                return Center + new Vector2(rotated.x, rotated.z);
            }

            public Vector2 WorldToLocal(Vector2 world)
            {
                Vector2 offset = world - Center;
                Vector3 local = inverseRotation * new Vector3(offset.x, 0f, offset.y);
                return new Vector2(local.x, local.z);
            }

            public bool Contains(float x, float z)
            {
                return ContainsLocal(WorldToLocal(new Vector2(x, z)));
            }

            public bool ContainsLocal(Vector2 local)
            {
                return IsPointInConvexPolygon(local, LocalPolygon);
            }

            public bool Intersects(Bounds bounds)
            {
                List<Vector2> boundsPolygon = new List<Vector2>
                {
                    new Vector2(bounds.min.x, bounds.min.z),
                    new Vector2(bounds.max.x, bounds.min.z),
                    new Vector2(bounds.max.x, bounds.max.z),
                    new Vector2(bounds.min.x, bounds.max.z)
                };
                List<Vector2> intersection = ClipConvexPolygon(WorldPolygon, boundsPolygon);
                return intersection.Count > 0;
            }

            public float DistanceSquared(float x, float z)
            {
                Vector2 point = new Vector2(x, z);
                if (Contains(x, z))
                {
                    return 0f;
                }

                float minimum = float.PositiveInfinity;
                for (int i = 0; i < WorldPolygon.Length; i++)
                {
                    Vector2 start = WorldPolygon[i];
                    Vector2 end = WorldPolygon[(i + 1) % WorldPolygon.Length];
                    Vector2 segment = end - start;
                    float lengthSquared = segment.sqrMagnitude;
                    float t = lengthSquared <= 0.000001f
                        ? 0f
                        : Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
                    minimum = Mathf.Min(minimum, (point - (start + segment * t)).sqrMagnitude);
                }

                return minimum;
            }

            private static bool IsPointInConvexPolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
            {
                float orientation = Mathf.Sign(SignedArea(polygon));
                for (int i = 0; i < polygon.Count; i++)
                {
                    Vector2 start = polygon[i];
                    Vector2 end = polygon[(i + 1) % polygon.Count];
                    if (Cross(end - start, point - start) * orientation < -0.0001f)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private readonly struct StageSpace
        {
            private readonly Quaternion inverseRotation;

            public StageSpace(Vector3 origin, float rotationY, float scale)
            {
                Origin = origin;
                RotationY = rotationY;
                Scale = Mathf.Max(0.01f, scale);
                inverseRotation = Quaternion.Inverse(Quaternion.Euler(0f, rotationY, 0f));
            }

            public Vector3 Origin { get; }
            public float RotationY { get; }
            public float Scale { get; }

            public Vector3 WorldToLocalPosition(Vector3 worldPosition)
            {
                return inverseRotation * (worldPosition - Origin) * Scale;
            }

            public Quaternion WorldToLocalRotation(Quaternion worldRotation)
            {
                return inverseRotation * worldRotation;
            }
        }

        private readonly struct RendererRecord
        {
            public RendererRecord(Renderer renderer, Bounds bounds)
            {
                Renderer = renderer;
                Bounds = bounds;
            }

            public Renderer Renderer { get; }
            public Bounds Bounds { get; }
            public Vector3 Center => Bounds.center;
        }

        private readonly struct SampleGrid
        {
            public SampleGrid(
                float startX,
                float startZ,
                float columnStepX,
                float columnStepZ,
                float rowStepX,
                float rowStepZ)
            {
                StartX = startX;
                StartZ = startZ;
                ColumnStepX = columnStepX;
                ColumnStepZ = columnStepZ;
                RowStepX = rowStepX;
                RowStepZ = rowStepZ;
            }

            public float StartX { get; }
            public float StartZ { get; }
            public float ColumnStepX { get; }
            public float ColumnStepZ { get; }
            public float RowStepX { get; }
            public float RowStepZ { get; }
        }

        private readonly struct WaterCrop
        {
            public WaterCrop(Renderer renderer, Vector2[] worldPolygon, float worldY)
            {
                Renderer = renderer;
                WorldPolygon = worldPolygon;
                WorldY = worldY;
            }

            public Renderer Renderer { get; }
            public Vector2[] WorldPolygon { get; }
            public float WorldY { get; }
        }

        private readonly struct TerrainCrop
        {
            public TerrainCrop(
                Terrain terrain,
                SelectionRegion region,
                float minLocalX,
                float maxLocalX,
                float minLocalZ,
                float maxLocalZ)
            {
                Terrain = terrain;
                Region = region;
                MinLocalX = minLocalX;
                MaxLocalX = maxLocalX;
                MinLocalZ = minLocalZ;
                MaxLocalZ = maxLocalZ;
            }

            public Terrain Terrain { get; }
            public SelectionRegion Region { get; }
            public float MinLocalX { get; }
            public float MaxLocalX { get; }
            public float MinLocalZ { get; }
            public float MaxLocalZ { get; }
            public float Width => MaxLocalX - MinLocalX;
            public float Depth => MaxLocalZ - MinLocalZ;

            public Vector2 GetWorldPoint(float normalizedX, float normalizedZ)
            {
                return Region.LocalToWorld(new Vector2(
                    Mathf.Lerp(MinLocalX, MaxLocalX, normalizedX),
                    Mathf.Lerp(MinLocalZ, MaxLocalZ, normalizedZ)));
            }

            public Vector2[] GetWorldBoundsCorners()
            {
                return new[]
                {
                    Region.LocalToWorld(new Vector2(MinLocalX, MinLocalZ)),
                    Region.LocalToWorld(new Vector2(MaxLocalX, MinLocalZ)),
                    Region.LocalToWorld(new Vector2(MaxLocalX, MaxLocalZ)),
                    Region.LocalToWorld(new Vector2(MinLocalX, MaxLocalZ))
                };
            }

            public bool ContainsWorldPoint(Vector2 world)
            {
                Vector2 local = Region.WorldToLocal(world);
                return Region.ContainsLocal(local)
                       && local.x >= MinLocalX - 0.0001f
                       && local.x <= MaxLocalX + 0.0001f
                       && local.y >= MinLocalZ - 0.0001f
                       && local.y <= MaxLocalZ + 0.0001f;
            }
        }
    }
}
