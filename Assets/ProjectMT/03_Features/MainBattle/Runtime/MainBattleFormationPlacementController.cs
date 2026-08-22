using System;
using System.Collections.Generic;
using ProjectMT.Features.Expedition;
using ProjectMT.Shared.GameData;
using ProjectMT.Shared.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectMT.Features.MainBattle
{
    [DisallowMultipleComponent]
    public sealed class MainBattleFormationPlacementController : MonoBehaviour // 본부대 시작 위치 편집
    {
        private const float WorldVisualHeight = 0.2f;

        private sealed class SlotVisual
        {
            public int SlotIndex;
            public UnitActor Unit;
            public LineRenderer Ring;
            public RectTransform BuffBadge;
            public Image BuffBadgeBackground;
            public TMP_Text BuffLabel;
            public float BuffBadgeHeight;
            public MainBattleFormationLine Line;
            public bool PreviewOverride;
            public Vector3 PreviewPosition;
        }

        private readonly List<SlotVisual> slotVisuals = new List<SlotVisual>(MainBattleFormationRules.SlotCount);

        private IGameProgressService progress;
        private ExpeditionController expedition;
        private MainBattleMonsterDragController drag;
        private Camera worldCamera;
        private Collider ground;
        private Terrain placementTerrain;
        private Transform formationAnchor;
        private Transform commander;
        private Transform uiRoot;
        private GameObject normalHudRoot;
        private GameObject globalDebugPanel;
        private TMP_FontAsset uiFont;

        private Vector3 commanderStartPosition;
        private Vector3 mapCenter;
        private Vector2[] workingOffsets;
        private bool normalHudWasActive;
        private bool globalDebugPanelWasActive;
        private bool saving;

        private GameObject placementCanvasRoot;
        private RectTransform safeAreaRoot;
        private MainBattlePlacementDimGraphic dimGraphic;
        private Button saveButton;
        private Button resetButton;
        private TMP_Text statusLabel;
        private Rect lastSafeArea;
        private int lastScreenWidth;
        private int lastScreenHeight;

        private GameObject worldVisualRoot;
        private Material worldFillMaterial;
        private Material lineMaterial;
        private Mesh areaMesh;

        public bool IsActive { get; private set; }
        public event Action Completed;

        public void Configure(
            IGameProgressService progressService,
            ExpeditionController expeditionController,
            MainBattleMonsterDragController dragController,
            Camera camera,
            Collider groundCollider,
            Transform formationAnchorRoot,
            Transform commanderRoot,
            Transform runtimeUiRoot,
            GameObject hudRoot)
        {
            Abort();
            progress = progressService ?? throw new ArgumentNullException(nameof(progressService));
            expedition = expeditionController ?? throw new ArgumentNullException(nameof(expeditionController));
            drag = dragController ?? throw new ArgumentNullException(nameof(dragController));
            worldCamera = camera ?? throw new ArgumentNullException(nameof(camera));
            ground = groundCollider ?? throw new ArgumentNullException(nameof(groundCollider));
            placementTerrain = FindPlacementTerrain();
            formationAnchor = formationAnchorRoot ?? throw new ArgumentNullException(nameof(formationAnchorRoot));
            commander = commanderRoot ?? throw new ArgumentNullException(nameof(commanderRoot));
            uiRoot = runtimeUiRoot ?? throw new ArgumentNullException(nameof(runtimeUiRoot));
            normalHudRoot = hudRoot ?? throw new ArgumentNullException(nameof(hudRoot));
            uiFont = normalHudRoot.GetComponentInChildren<TMP_Text>(true)?.font;

            commanderStartPosition = commander.position;
            var anchorPosition = formationAnchor.position;
            mapCenter = new Vector3(anchorPosition.x, commanderStartPosition.y, anchorPosition.z);
            enabled = false;
        }

        public bool Begin()
        {
            if (IsActive || progress == null || expedition == null || drag == null || !progress.IsLoaded)
            {
                return false;
            }

            var savedOffsets = progress.View.MainBattleFormation.CopyOffsets();
            if (!MainBattleFormationRules.TryCreateSnappedOffsets(savedOffsets, out workingOffsets))
            {
                workingOffsets = MainBattleFormationRules.CreateDefaultOffsets();
            }

            if (!expedition.BeginFormationPlacement())
            {
                return false;
            }

            commander.position = commanderStartPosition;
            normalHudWasActive = normalHudRoot.activeSelf;
            normalHudRoot.SetActive(false);
            globalDebugPanel = FindGlobalDebugPanel();
            if (globalDebugPanel != null)
            {
                globalDebugPanelWasActive = globalDebugPanel.activeSelf;
                globalDebugPanel.SetActive(false);
            }
            BuildPlacementUi();
            BuildWorldVisuals();
            BindPlacementUnits();
            if (slotVisuals.Count == 0)
            {
                Abort();
                expedition.EndFormationPlacement();
                return false;
            }

            IsActive = true;
            saving = false;
            SetStatus("몬스터를 드래그해 빈 육각 칸에 배치하세요");
            drag.ConfigurePlacement(
                worldCamera,
                ground,
                () => IsActive && !saving,
                CanSelectUnit,
                ResolveHexWorldPosition,
                CanDropUnit,
                HandleDragPreviewChanged,
                HandleDragReleased);
            enabled = true;
            RefreshProjectedArea();
            return true;
        }

        public void Abort()
        {
            if (drag != null)
            {
                drag.CancelCurrentInteraction();
            }

            IsActive = false;
            saving = false;
            enabled = false;
            slotVisuals.Clear();
            DestroyRuntimeVisuals();
            if (normalHudRoot != null)
            {
                normalHudRoot.SetActive(normalHudWasActive);
            }

            if (globalDebugPanel != null)
            {
                globalDebugPanel.SetActive(globalDebugPanelWasActive);
            }

            if (commander != null)
            {
                commander.position = commanderStartPosition;
            }
        }

        public void Shutdown()
        {
            Abort();
            Completed = null;
            progress = null;
            expedition = null;
            drag = null;
            worldCamera = null;
            ground = null;
            placementTerrain = null;
            formationAnchor = null;
            commander = null;
            uiRoot = null;
            normalHudRoot = null;
            globalDebugPanel = null;
            uiFont = null;
            workingOffsets = null;
        }

        private void Update()
        {
            if (!IsActive)
            {
                return;
            }

            var buttonsEnabled = !saving && drag != null && !drag.IsInteracting;
            if (saveButton != null)
            {
                saveButton.interactable = buttonsEnabled;
            }

            if (resetButton != null)
            {
                resetButton.interactable = buttonsEnabled;
            }

            UpdateRingPositions();
            UpdateBuffBadgePositions();
            if (lastScreenWidth != Screen.width || lastScreenHeight != Screen.height || lastSafeArea != Screen.safeArea)
            {
                RefreshSafeArea();
                RefreshProjectedArea();
            }
        }

        private bool CanSelectUnit(UnitActor unit)
        {
            return IsActive && !saving && expedition.TryGetPlayerSlot(unit, out _);
        }

        private bool CanDropUnit(UnitActor unit, Vector3 worldPosition)
        {
            if (!expedition.TryGetPlayerSlot(unit, out var slotIndex) ||
                slotIndex < 0 || slotIndex >= workingOffsets.Length)
            {
                return false;
            }

            var candidate = WorldToOffset(worldPosition);
            if (!MainBattleFormationRules.IsHexPosition(candidate))
            {
                return false;
            }

            for (var index = 0; index < workingOffsets.Length; index++)
            {
                if (index != slotIndex && !MainBattleFormationRules.DoNotOverlap(candidate, workingOffsets[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private void HandleDragPreviewChanged(UnitActor unit, Vector3 worldPosition, bool valid)
        {
            var visual = FindSlotVisual(unit);
            if (visual == null)
            {
                return;
            }

            visual.PreviewOverride = true;
            visual.PreviewPosition = worldPosition;
            SetRingColor(visual.Ring, valid ? ValidColor : InvalidColor);
            if (valid)
            {
                UpdateSlotBuffVisual(
                    visual,
                    MainBattleFormationRules.SnapToHex(WorldToOffset(worldPosition)),
                    false);
            }
        }

        private void HandleDragReleased(UnitActor unit, Vector3 worldPosition, bool valid)
        {
            var visual = FindSlotVisual(unit);
            if (visual == null)
            {
                return;
            }

            if (valid)
            {
                workingOffsets[visual.SlotIndex] = MainBattleFormationRules.SnapToHex(WorldToOffset(worldPosition));
            }

            visual.PreviewOverride = false;
            UpdateSlotBuffVisual(visual, workingOffsets[visual.SlotIndex], true);
        }

        private void HandleResetClicked()
        {
            if (!IsActive || saving || drag == null || drag.IsInteracting)
            {
                return;
            }

            workingOffsets = MainBattleFormationRules.CreateDefaultOffsets();
            for (var index = 0; index < slotVisuals.Count; index++)
            {
                var visual = slotVisuals[index];
                if (visual.Unit != null)
                {
                    visual.Unit.transform.position = OffsetToWorld(workingOffsets[visual.SlotIndex]);
                    visual.PreviewOverride = false;
                    UpdateSlotBuffVisual(visual, workingOffsets[visual.SlotIndex], true);
                }
            }

            SetStatus("기본 위치로 초기화했습니다");
        }

        private async void HandleSaveClicked()
        {
            if (!IsActive || saving || drag == null || drag.IsInteracting ||
                !MainBattleFormationRules.IsHexFormation(workingOffsets))
            {
                return;
            }

            saving = true;
            SetStatus("저장 중...");
            var saved = false;
            try
            {
                saved = await progress.TryApplyAndSaveAsync(
                    GameProgressChange.SetMainBattleFormation(workingOffsets));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            if (this == null || !IsActive)
            {
                return;
            }

            if (!saved)
            {
                saving = false;
                SetStatus("저장하지 못했습니다 · 다시 시도해 주세요");
                return;
            }

            Abort();
            Completed?.Invoke();
        }

        private void BindPlacementUnits()
        {
            slotVisuals.Clear();
            var units = FindObjectsByType<UnitActor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var unit in units)
            {
                if (unit == null || unit.Team != UnitTeam.Player ||
                    !expedition.TryGetPlayerSlot(unit, out var slotIndex))
                {
                    continue;
                }

                if (slotIndex >= 0 && slotIndex < workingOffsets.Length)
                {
                    var snappedPosition = OffsetToWorld(workingOffsets[slotIndex]);
                    snappedPosition.y = unit.transform.position.y;
                    unit.transform.position = snappedPosition;
                }

                var visual = new SlotVisual
                {
                    SlotIndex = slotIndex,
                    Unit = unit,
                    Ring = CreateRing($"PlacementRing_{slotIndex + 1:00}"),
                    BuffBadgeHeight = ResolveBuffBadgeHeight(unit)
                };
                CreateBuffBadge(visual);
                UpdateSlotBuffVisual(visual, workingOffsets[slotIndex], true);
                slotVisuals.Add(visual);
            }
        }

        private void BuildWorldVisuals()
        {
            worldVisualRoot = new GameObject("MainBattleFormationPlacementWorldVisuals");
            worldVisualRoot.transform.SetParent(transform, false);

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("UI/Default");
            }

            worldFillMaterial = new Material(shader)
            {
                name = "Runtime_MainBattlePlacementArea",
                color = new Color(0.16f, 0.88f, 0.72f, 0.10f),
                hideFlags = HideFlags.DontSave
            };
            lineMaterial = new Material(shader)
            {
                name = "Runtime_MainBattlePlacementLines",
                color = Color.white,
                hideFlags = HideFlags.DontSave
            };

            var corners = GetAreaWorldCorners(WorldVisualHeight);
            areaMesh = new Mesh
            {
                name = "Runtime_MainBattlePlacementAreaMesh",
                hideFlags = HideFlags.DontSave,
                vertices = corners,
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
                uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right }
            };
            areaMesh.RecalculateBounds();

            var fill = new GameObject("PlacementAreaFill", typeof(MeshFilter), typeof(MeshRenderer));
            fill.transform.SetParent(worldVisualRoot.transform, false);
            fill.GetComponent<MeshFilter>().sharedMesh = areaMesh;
            fill.GetComponent<MeshRenderer>().sharedMaterial = worldFillMaterial;

            var outline = CreateLineRenderer("PlacementAreaOutline", 0.08f);
            outline.loop = true;
            outline.positionCount = corners.Length;
            outline.SetPositions(corners);
            SetRingColor(outline, new Color(0.24f, 1f, 0.82f, 0.95f));
            BuildHexGuide();
        }

        private void BuildHexGuide()
        {
            var offsets = MainBattleFormationRules.CopyHexOffsets();
            for (var hexIndex = 0; hexIndex < offsets.Length; hexIndex++)
            {
                var line = CreateLineRenderer($"PlacementHex_{hexIndex + 1:000}", 0.036f);
                line.loop = true;
                line.useWorldSpace = true;
                line.positionCount = 6;
                var center = OffsetToWorld(offsets[hexIndex]);
                for (var corner = 0; corner < 6; corner++)
                {
                    var angle = (30f + corner * 60f) * Mathf.Deg2Rad;
                    var point = center + new Vector3(
                        Mathf.Cos(angle) * MainBattleFormationRules.HexVisualRadius,
                        0f,
                        Mathf.Sin(angle) * MainBattleFormationRules.HexVisualRadius);
                    line.SetPosition(corner, ProjectToGroundSurface(point, WorldVisualHeight));
                }

                SetRingColor(line, GetLineColor(MainBattleFormationRules.GetLine(offsets[hexIndex]), 0.54f));
            }
        }

        private LineRenderer CreateRing(string objectName)
        {
            const int segmentCount = 64;
            var ring = CreateLineRenderer(objectName, 0.065f);
            ring.loop = true;
            ring.positionCount = segmentCount;
            ring.useWorldSpace = true;
            for (var index = 0; index < segmentCount; index++)
            {
                ring.SetPosition(index, Vector3.zero);
            }

            SetRingColor(ring, NeutralColor);
            return ring;
        }

        private LineRenderer CreateLineRenderer(string objectName, float width)
        {
            var lineObject = new GameObject(objectName, typeof(LineRenderer));
            lineObject.transform.SetParent(worldVisualRoot.transform, false);
            var line = lineObject.GetComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.startWidth = width;
            line.endWidth = width;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.textureMode = LineTextureMode.Stretch;
            return line;
        }

        private void UpdateRingPositions()
        {
            for (var index = 0; index < slotVisuals.Count; index++)
            {
                var visual = slotVisuals[index];
                if (visual.Unit == null || visual.Ring == null)
                {
                    continue;
                }

                var position = visual.PreviewOverride ? visual.PreviewPosition : visual.Unit.transform.position;
                for (var segment = 0; segment < visual.Ring.positionCount; segment++)
                {
                    var angle = segment * Mathf.PI * 2f / visual.Ring.positionCount;
                    var point = position + new Vector3(
                        Mathf.Cos(angle) * MainBattleFormationRules.UnitRadius,
                        0f,
                        Mathf.Sin(angle) * MainBattleFormationRules.UnitRadius);
                    visual.Ring.SetPosition(segment, ProjectToGroundSurface(point, WorldVisualHeight));
                }
            }
        }

        private void CreateBuffBadge(SlotVisual visual)
        {
            var badgeObject = new GameObject(
                $"PlacementBuffBadge_{visual.SlotIndex + 1:00}",
                typeof(RectTransform),
                typeof(Canvas));
            badgeObject.transform.SetParent(worldVisualRoot.transform, false);
            var badgeRect = badgeObject.GetComponent<RectTransform>();
            badgeRect.sizeDelta = new Vector2(250f, 100f);
            badgeRect.localScale = Vector3.one * 0.0062f;
            var canvas = badgeObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = worldCamera;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1200 + visual.SlotIndex;

            var background = CreateImage("Background", badgeRect, new Color(0.015f, 0.025f, 0.04f, 0.95f));
            Stretch(background.rectTransform);
            var label = CreateText("BuffName", badgeRect, string.Empty, 29f, FontStyles.Bold);
            Stretch(label.rectTransform, 8f);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.lineSpacing = -8f;

            visual.BuffBadge = badgeRect;
            visual.BuffBadgeBackground = background;
            visual.BuffLabel = label;
        }

        private void UpdateBuffBadgePositions()
        {
            for (var index = 0; index < slotVisuals.Count; index++)
            {
                var visual = slotVisuals[index];
                if (visual.Unit == null || visual.BuffBadge == null)
                {
                    continue;
                }

                var position = visual.PreviewOverride ? visual.PreviewPosition : visual.Unit.transform.position;
                visual.BuffBadge.position = position + Vector3.up * visual.BuffBadgeHeight;
                if (worldCamera != null)
                {
                    visual.BuffBadge.rotation = worldCamera.transform.rotation;
                }
            }
        }

        private void UpdateSlotBuffVisual(SlotVisual visual, Vector2 offset, bool updateRing)
        {
            if (visual == null)
            {
                return;
            }

            visual.Line = MainBattleFormationRules.GetLine(offset);
            var color = GetLineColor(visual.Line, 0.96f);
            if (visual.BuffBadgeBackground != null)
            {
                visual.BuffBadgeBackground.color = Color.Lerp(
                    new Color(0.015f, 0.025f, 0.04f, 0.96f),
                    color,
                    0.24f);
            }

            if (visual.BuffLabel != null)
            {
                visual.BuffLabel.text = GetLineBuffLabel(visual.Line);
            }

            if (updateRing)
            {
                SetRingColor(visual.Ring, GetLineColor(visual.Line, 0.88f));
            }
        }

        private static float ResolveBuffBadgeHeight(UnitActor unit)
        {
            var highestPoint = unit.transform.position.y + 1.25f;
            var renderers = unit.GetComponentsInChildren<Renderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    highestPoint = Mathf.Max(highestPoint, renderers[index].bounds.max.y);
                }
            }

            return Mathf.Clamp(highestPoint - unit.transform.position.y + 0.58f, 1.6f, 3.2f);
        }

        private static string GetLineBuffLabel(MainBattleFormationLine line)
        {
            return line switch
            {
                MainBattleFormationLine.Front => "전열\n방어력 +20%",
                MainBattleFormationLine.Middle => "중열\n공격력 +20%",
                _ => "후열\n버프 +20%"
            };
        }

        private static Color GetLineColor(MainBattleFormationLine line, float alpha)
        {
            var color = line switch
            {
                MainBattleFormationLine.Front => FrontLineColor,
                MainBattleFormationLine.Middle => MiddleLineColor,
                _ => RearLineColor
            };
            color.a = alpha;
            return color;
        }

        private void BuildPlacementUi()
        {
            placementCanvasRoot = new GameObject(
                "MainBattleFormationPlacementCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            placementCanvasRoot.transform.SetParent(uiRoot, false);
            var canvas = placementCanvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            var scaler = placementCanvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var dimObject = CreateUiObject("OutsideDim", placementCanvasRoot.transform);
            Stretch(dimObject.GetComponent<RectTransform>());
            dimGraphic = dimObject.AddComponent<MainBattlePlacementDimGraphic>();
            dimGraphic.color = new Color(0f, 0f, 0f, 0.62f);
            dimGraphic.raycastTarget = false;

            safeAreaRoot = CreateUiObject("SafeArea", placementCanvasRoot.transform).GetComponent<RectTransform>();
            Stretch(safeAreaRoot);

            var titleBackdrop = CreateImage("TitleBackdrop", safeAreaRoot, new Color(0.03f, 0.08f, 0.10f, 0.88f));
            SetRect(titleBackdrop.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(380f, 82f));
            var title = CreateText("Title", titleBackdrop.transform, "육각 배치 모드", 38f, FontStyles.Bold);
            Stretch(title.rectTransform);

            statusLabel = CreateText("Status", safeAreaRoot, string.Empty, 23f, FontStyles.Normal);
            SetRect(statusLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -105f), new Vector2(760f, 44f));

            saveButton = CreateButton("SaveAndExitButton", safeAreaRoot, "저장하고 나가기", new Color(0.12f, 0.56f, 0.50f, 0.98f));
            SetRect(saveButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-180f, 72f), new Vector2(320f, 76f));
            saveButton.onClick.AddListener(HandleSaveClicked);

            resetButton = CreateButton("ResetButton", safeAreaRoot, "초기화", new Color(0.22f, 0.27f, 0.31f, 0.98f));
            SetRect(resetButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(180f, 72f), new Vector2(240f, 76f));
            resetButton.onClick.AddListener(HandleResetClicked);
            RefreshSafeArea();
        }

        private void RefreshSafeArea()
        {
            if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            var safe = Screen.safeArea;
            safeAreaRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            safeAreaRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            safeAreaRoot.offsetMin = Vector2.zero;
            safeAreaRoot.offsetMax = Vector2.zero;
            lastSafeArea = safe;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
        }

        private void RefreshProjectedArea()
        {
            if (dimGraphic == null || worldCamera == null)
            {
                return;
            }

            var worldCorners = GetAreaWorldCorners(0f);
            var localCorners = new Vector2[worldCorners.Length];
            for (var index = 0; index < worldCorners.Length; index++)
            {
                var screenPoint = worldCamera.WorldToScreenPoint(worldCorners[index]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dimGraphic.rectTransform,
                    screenPoint,
                    null,
                    out localCorners[index]);
            }

            dimGraphic.SetHole(localCorners);
        }

        private Vector3[] GetAreaWorldCorners(float yOffset)
        {
            var y = commanderStartPosition.y + yOffset;
            var minX = mapCenter.x + MainBattleFormationRules.AreaCenterX -
                       MainBattleFormationRules.AreaWidth * 0.5f;
            var maxX = mapCenter.x + MainBattleFormationRules.AreaCenterX +
                       MainBattleFormationRules.AreaWidth * 0.5f;
            var minZ = mapCenter.z + MainBattleFormationRules.AreaCenterZ -
                       MainBattleFormationRules.AreaDepth * 0.5f;
            var maxZ = mapCenter.z + MainBattleFormationRules.AreaCenterZ +
                       MainBattleFormationRules.AreaDepth * 0.5f;
            return new[]
            {
                new Vector3(minX, y, minZ),
                new Vector3(minX, y, maxZ),
                new Vector3(maxX, y, maxZ),
                new Vector3(maxX, y, minZ)
            };
        }

        private Vector2 WorldToOffset(Vector3 position)
        {
            return new Vector2(position.x - mapCenter.x, position.z - mapCenter.z);
        }

        private Vector3 OffsetToWorld(Vector2 offset)
        {
            return new Vector3(
                mapCenter.x + offset.x,
                commanderStartPosition.y,
                mapCenter.z + offset.y);
        }

        private Vector3 ResolveHexWorldPosition(UnitActor unit, Vector3 worldPosition)
        {
            var snapped = OffsetToWorld(MainBattleFormationRules.SnapToHex(WorldToOffset(worldPosition)));
            snapped.y = unit != null ? unit.transform.position.y : worldPosition.y;
            return snapped;
        }

        private Terrain FindPlacementTerrain()
        {
            var terrains = FindObjectsByType<Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var index = 0; index < terrains.Length; index++)
            {
                if (terrains[index] != null && terrains[index].gameObject.scene == gameObject.scene)
                {
                    return terrains[index];
                }
            }

            return null;
        }

        private Vector3 ProjectToGroundSurface(Vector3 point, float lift)
        {
            if (placementTerrain != null && placementTerrain.terrainData != null)
            {
                var local = point - placementTerrain.transform.position;
                var size = placementTerrain.terrainData.size;
                if (local.x >= 0f && local.x <= size.x && local.z >= 0f && local.z <= size.z)
                {
                    point.y = placementTerrain.SampleHeight(point) + placementTerrain.transform.position.y + lift;
                    return point;
                }
            }

            if (ground != null)
            {
                var bounds = ground.bounds;
                var rayHeight = bounds.max.y + 5f;
                var ray = new Ray(new Vector3(point.x, rayHeight, point.z), Vector3.down);
                if (ground.Raycast(ray, out var hit, bounds.size.y + 10f))
                {
                    point.y = hit.point.y + lift;
                    return point;
                }
            }

            point.y = commanderStartPosition.y + lift;
            return point;
        }

        private SlotVisual FindSlotVisual(UnitActor unit)
        {
            for (var index = 0; index < slotVisuals.Count; index++)
            {
                if (slotVisuals[index].Unit == unit)
                {
                    return slotVisuals[index];
                }
            }

            return null;
        }

        private static GameObject FindGlobalDebugPanel()
        {
            var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var index = 0; index < transforms.Length; index++)
            {
                var candidate = transforms[index];
                if (candidate != null && candidate.name == "DebugPanel" &&
                    candidate.gameObject.scene.name == "DontDestroyOnLoad")
                {
                    return candidate.gameObject;
                }
            }

            return null;
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message ?? string.Empty;
            }
        }

        private void DestroyRuntimeVisuals()
        {
            if (saveButton != null)
            {
                saveButton.onClick.RemoveListener(HandleSaveClicked);
            }

            if (resetButton != null)
            {
                resetButton.onClick.RemoveListener(HandleResetClicked);
            }

            if (placementCanvasRoot != null)
            {
                Destroy(placementCanvasRoot);
            }

            if (worldVisualRoot != null)
            {
                Destroy(worldVisualRoot);
            }

            if (areaMesh != null)
            {
                Destroy(areaMesh);
            }

            if (worldFillMaterial != null)
            {
                Destroy(worldFillMaterial);
            }

            if (lineMaterial != null)
            {
                Destroy(lineMaterial);
            }

            placementCanvasRoot = null;
            safeAreaRoot = null;
            dimGraphic = null;
            saveButton = null;
            resetButton = null;
            statusLabel = null;
            worldVisualRoot = null;
            areaMesh = null;
            worldFillMaterial = null;
            lineMaterial = null;
        }

        private static GameObject CreateUiObject(string objectName, Transform parent)
        {
            var result = new GameObject(objectName, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            return result;
        }

        private Image CreateImage(string objectName, Transform parent, Color color)
        {
            var result = CreateUiObject(objectName, parent);
            var image = result.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text CreateText(string objectName, Transform parent, string value, float size, FontStyles style)
        {
            var result = CreateUiObject(objectName, parent);
            var text = result.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = uiFont;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private Button CreateButton(string objectName, Transform parent, string label, Color color)
        {
            var image = CreateImage(objectName, parent, color);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.42f);
            button.colors = colors;
            var text = CreateText("Label", image.transform, label, 29f, FontStyles.Bold);
            Stretch(text.rectTransform, 12f);
            return button;
        }

        private static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetRingColor(LineRenderer line, Color color)
        {
            if (line != null)
            {
                line.startColor = color;
                line.endColor = color;
            }
        }

        private static readonly Color NeutralColor = new Color(0.35f, 0.88f, 1f, 0.78f);
        private static readonly Color ValidColor = new Color(0.25f, 1f, 0.42f, 0.98f);
        private static readonly Color InvalidColor = new Color(1f, 0.22f, 0.18f, 0.98f);
        private static readonly Color FrontLineColor = new Color(0.22f, 0.64f, 1f, 1f);
        private static readonly Color MiddleLineColor = new Color(1f, 0.48f, 0.18f, 1f);
        private static readonly Color RearLineColor = new Color(0.34f, 0.88f, 0.5f, 1f);

        private void OnDestroy()
        {
            Shutdown();
        }
    }

    internal sealed class MainBattlePlacementDimGraphic : MaskableGraphic // 화면 전체에서 배치영역만 뚫린 딤
    {
        private readonly Vector2[] hole = new Vector2[4];
        private bool hasHole;

        public void SetHole(IReadOnlyList<Vector2> points)
        {
            hasHole = points != null && points.Count == hole.Length;
            if (hasHole)
            {
                var rect = rectTransform.rect;
                var outer = new[]
                {
                    new Vector2(rect.xMin, rect.yMin),
                    new Vector2(rect.xMax, rect.yMin),
                    new Vector2(rect.xMax, rect.yMax),
                    new Vector2(rect.xMin, rect.yMax)
                };
                var used = new bool[points.Count];
                for (var outerIndex = 0; outerIndex < outer.Length; outerIndex++)
                {
                    var nearest = -1;
                    var nearestDistance = float.PositiveInfinity;
                    for (var pointIndex = 0; pointIndex < points.Count; pointIndex++)
                    {
                        if (used[pointIndex])
                        {
                            continue;
                        }

                        var distance = (points[pointIndex] - outer[outerIndex]).sqrMagnitude;
                        if (distance < nearestDistance)
                        {
                            nearest = pointIndex;
                            nearestDistance = distance;
                        }
                    }

                    hole[outerIndex] = points[nearest];
                    used[nearest] = true;
                }
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var rect = rectTransform.rect;
            var outer = new[]
            {
                new Vector2(rect.xMin, rect.yMin),
                new Vector2(rect.xMax, rect.yMin),
                new Vector2(rect.xMax, rect.yMax),
                new Vector2(rect.xMin, rect.yMax)
            };

            if (!hasHole)
            {
                AddQuad(vertexHelper, outer[0], outer[1], outer[2], outer[3], color);
                return;
            }

            for (var index = 0; index < outer.Length; index++)
            {
                var next = (index + 1) % outer.Length;
                AddQuad(vertexHelper, outer[index], outer[next], hole[next], hole[index], color);
            }
        }

        private static void AddQuad(
            VertexHelper helper,
            Vector2 first,
            Vector2 second,
            Vector2 third,
            Vector2 fourth,
            Color32 color)
        {
            var start = helper.currentVertCount;
            helper.AddVert(first, color, Vector2.zero);
            helper.AddVert(second, color, Vector2.right);
            helper.AddVert(third, color, Vector2.one);
            helper.AddVert(fourth, color, Vector2.up);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
        }
    }
}
