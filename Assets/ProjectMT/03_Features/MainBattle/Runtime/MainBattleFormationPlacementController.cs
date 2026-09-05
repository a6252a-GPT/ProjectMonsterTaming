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
        private const float WorldVisualHeight = 0.30f;

        private sealed class SlotVisual
        {
            public int SlotIndex;
            public UnitActor Unit;
            public LineRenderer Ring;
            public RectTransform BuffBadge;
            public Image BuffBadgeBackground;
            public Image BuffBadgeAccent;
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
        [SerializeField] private MainBattleFormationPlacementHudView hudPrefab;
        [SerializeField] private MainBattlePlacementBuffBadgeView buffBadgePrefab;

        private Vector3 commanderStartPosition;
        private Vector3 mapCenter;
        private Vector2[] workingOffsets;
        private Vector2[] initialOffsets;
        private SlotVisual selectedVisual;
        private LineRenderer selectedHex;
        private bool normalHudWasActive;
        private bool globalDebugPanelWasActive;
        private bool saving;

        private GameObject placementCanvasRoot;
        private RectTransform safeAreaRoot;
        private MainBattlePlacementDimGraphic dimGraphic;
        private Button saveButton;
        private Button resetButton;
        private TMP_Text statusLabel;
        private TMP_Text unsavedLabel;
        private Rect lastSafeArea;
        private int lastScreenWidth;
        private int lastScreenHeight;

        private GameObject worldVisualRoot;
        [SerializeField] private MainBattlePlacementWorldView worldPrefab;
        private MainBattlePlacementWorldView worldView;
        private Vector3[] selectedHexOffsets;
        private Vector3[] ringOffsets;

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

            initialOffsets = (Vector2[])workingOffsets.Clone();
            selectedVisual = null;

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
            RefreshUnsavedState();
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
            selectedVisual = null;
            initialOffsets = null;
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
            selectedVisual = visual;
            UpdateSelectedHex(worldPosition, valid);
            RefreshBadgeEmphasis();
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
            UpdateSelectedHex(OffsetToWorld(workingOffsets[visual.SlotIndex]), true);
            RefreshBadgeEmphasis();
            RefreshUnsavedState();
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
            selectedVisual = null;
            if (selectedHex != null) selectedHex.gameObject.SetActive(false);
            RefreshBadgeEmphasis();
            RefreshUnsavedState();
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
            RefreshUnsavedState();
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
                RefreshUnsavedState();
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
            if (worldPrefab == null) throw new InvalidOperationException("The placement world prefab is required.");
            worldView = Instantiate(worldPrefab, transform, false);
            worldVisualRoot = worldView.gameObject;
            worldVisualRoot.name = "MainBattleFormationPlacementWorldVisuals";
            selectedHex = worldView.SelectedHex;
            selectedHexOffsets = ReadLinePositions(worldPrefab.SelectedHex);
            ringOffsets = ReadLinePositions(worldView.RingTemplate);
            BuildHexGuide();
        }

        private void BuildHexGuide()
        {
            var offsets = MainBattleFormationRules.CopyHexOffsets();
            for (var hexIndex = 0; hexIndex < offsets.Length; hexIndex++)
            {
                var line = InstantiateLine($"PlacementHex_{hexIndex + 1:000}", worldView.HexTemplate);
                var center = OffsetToWorld(offsets[hexIndex]);
                for (var corner = 0; corner < line.positionCount; corner++)
                {
                    var point = center + worldView.HexTemplate.GetPosition(corner);
                    line.SetPosition(corner, ProjectToGroundSurface(point, WorldVisualHeight));
                }

                SetRingColor(line, GetLineColor(MainBattleFormationRules.GetLine(offsets[hexIndex]), 0.68f));
            }
        }

        private void UpdateSelectedHex(Vector3 position, bool valid)
        {
            if (selectedHex == null) return;
            selectedHex.gameObject.SetActive(true);
            var center = OffsetToWorld(MainBattleFormationRules.SnapToHex(WorldToOffset(position)));
            for (var corner = 0; corner < selectedHexOffsets.Length; corner++)
            {
                var point = center + selectedHexOffsets[corner];
                selectedHex.SetPosition(corner, ProjectToGroundSurface(point, WorldVisualHeight + 0.015f));
            }
            SetRingColor(selectedHex, valid ? new Color32(242, 220, 157, 255) : InvalidColor);
        }

        private void RefreshBadgeEmphasis()
        {
            foreach (var visual in slotVisuals)
            {
                var selected = visual == selectedVisual;
                visual.BuffBadgeBackground.color = selected ? new Color32(46, 43, 39, 250) : PanelColor;
                visual.BuffLabel.color = selectedVisual == null || selected ? IvoryColor : new Color32(190, 186, 176, 255);
            }
        }

        private void RefreshUnsavedState()
        {
            if (unsavedLabel == null) return;
            var changed = false;
            if (initialOffsets != null && workingOffsets != null)
            {
                for (var i = 0; i < workingOffsets.Length; i++)
                {
                    if ((workingOffsets[i] - initialOffsets[i]).sqrMagnitude <= 0.0001f) continue;
                    changed = true;
                    break;
                }
            }
            unsavedLabel.text = changed && !saving ? "저장하지 않은 변경사항" : string.Empty;
        }

        private LineRenderer CreateRing(string objectName)
        {
            return InstantiateLine(objectName, worldView.RingTemplate);
        }

        private LineRenderer InstantiateLine(string objectName, LineRenderer template)
        {
            var line = Instantiate(template, worldVisualRoot.transform, false);
            line.name = objectName;
            line.gameObject.SetActive(true);
            return line;
        }

        private static Vector3[] ReadLinePositions(LineRenderer line)
        {
            var positions = new Vector3[line.positionCount];
            line.GetPositions(positions);
            return positions;
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
                    var point = position + ringOffsets[segment];
                    visual.Ring.SetPosition(segment, ProjectToGroundSurface(point, WorldVisualHeight));
                }
            }
        }

        private void CreateBuffBadge(SlotVisual visual)
        {
            if (buffBadgePrefab == null) throw new InvalidOperationException("The placement buff badge prefab is required.");
            var badge = Instantiate(buffBadgePrefab, worldVisualRoot.transform, false);
            badge.name = $"PlacementBuffBadge_{visual.SlotIndex + 1:00}";
            var canvas = badge.GetComponent<Canvas>();
            canvas.worldCamera = worldCamera;
            canvas.sortingOrder += visual.SlotIndex;
            visual.BuffBadge = (RectTransform)badge.transform;
            visual.BuffBadgeBackground = badge.Background;
            visual.BuffBadgeAccent = badge.Accent;
            visual.BuffLabel = badge.Label;
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
                visual.BuffBadgeBackground.color = PanelColor;
            }

            if (visual.BuffBadgeAccent != null) visual.BuffBadgeAccent.color = color;
            if (visual.BuffLabel != null)
            {
                visual.BuffLabel.text = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{GetLineBuffLabel(visual.Line).Replace("\n", "</color>\n").Replace("+20%", "<color=#F2DCA0>+20%</color>")}";
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
            if (hudPrefab == null) throw new InvalidOperationException("The formation placement HUD prefab is required.");
            var view = Instantiate(hudPrefab, uiRoot, false);
            placementCanvasRoot = view.gameObject;
            placementCanvasRoot.name = "MainBattleFormationPlacementCanvas";
            safeAreaRoot = view.SafeArea;
            dimGraphic = view.Dim;
            saveButton = view.SaveButton;
            resetButton = view.ResetButton;
            statusLabel = view.StatusLabel;
            unsavedLabel = view.UnsavedLabel;
            saveButton.onClick.AddListener(HandleSaveClicked);
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

            placementCanvasRoot = null;
            safeAreaRoot = null;
            dimGraphic = null;
            saveButton = null;
            resetButton = null;
            statusLabel = null;
            unsavedLabel = null;
            selectedHex = null;
            worldVisualRoot = null;
            worldView = null;
            selectedHexOffsets = null;
            ringOffsets = null;
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
        private static readonly Color FrontLineColor = new Color32(133, 186, 224, 255);
        private static readonly Color MiddleLineColor = new Color32(232, 172, 115, 255);
        private static readonly Color RearLineColor = new Color32(155, 205, 167, 255);

        private static readonly Color PanelColor = new Color32(35, 39, 43, 245);
        private static readonly Color BorderColor = new Color32(82, 87, 88, 255);
        private static readonly Color IvoryColor = new Color32(241, 232, 209, 255);

        private void OnDestroy()
        {
            Shutdown();
        }
    }
}
