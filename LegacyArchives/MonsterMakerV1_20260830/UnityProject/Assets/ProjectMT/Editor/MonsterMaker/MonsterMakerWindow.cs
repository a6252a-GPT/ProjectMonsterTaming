using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ProjectMT.Contents.CastleRaidHex;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.Audio;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    internal enum MonsterMakerPreviewPositionValueMode
    {
        RootLocal,
        VisualLocal,
        AnchorOffset
    }

    internal enum MonsterMakerPreviewReference
    {
        None,
        Model,
        Attack,
        Hit
    }

    internal enum MonsterMakerCatalogSortMode
    {
        Default,
        Rarity
    }

    internal sealed class MonsterMakerPreviewPositionBinding
    {
        public MonsterMakerPreviewPositionBinding(
            string propertyPath,
            string label,
            MonsterMakerPreviewPositionValueMode valueMode,
            MonsterMakerPreviewAnchor anchor,
            string socketPath = null)
        {
            PropertyPath = propertyPath ?? string.Empty;
            Label = label ?? "위치";
            ValueMode = valueMode;
            Anchor = anchor;
            SocketPath = socketPath ?? string.Empty;
        }

        public string PropertyPath { get; }
        public string Label { get; }
        public MonsterMakerPreviewPositionValueMode ValueMode { get; }
        public MonsterMakerPreviewAnchor Anchor { get; }
        public string SocketPath { get; }

        public bool Matches(SerializedProperty property)
        {
            return property != null && string.Equals(
                PropertyPath,
                property.propertyPath,
                StringComparison.Ordinal);
        }
    }

    public sealed class MonsterMakerWindow : EditorWindow // 수동 입력·Preview·검증·편입을 한 창에서 처리
    {
        private const string MenuPath = "JC Tool/Monster/Legacy/Monster Maker V1";
        private const int DefinitionPickerId = 8501;
        private const float DraftHeaderHeight = 112f;
        private const float OuterMargin = 8f;
        private const float ColumnGap = 8f;
        private const float ColumnPadding = 10f;
        private const float ActionDockPadding = 9f;
        private const float ActionGap = 6f;
        private const float CatalogColumnWidth = 230f;
        private const float CatalogRowHeight = 52f;
        private const float CatalogRowSpacing = 3f;
        private const float LeftColumnWidth = 430f;
        private const float PreviewColumnMinWidth = 420f;
        private const float ControlHeight = 26f;
        private const float PreviewOverlayMargin = 10f;
        private const float PreviewOverlayGap = 6f;
        private const float CombatPreviewOverlayHeight = 49f;
        private const float PositionPreviewToolbarHeight = 48f;
        private const float ProfileSummaryMinWidth = 286f;
        private const float ProfileSummaryMaxWidth = 318f;
        private const float ProfileSummaryMinHeight = 318f;
        private const float ProfileSummarySideBySideThreshold = 760f;
        private const float BasicAttackVfxTimingGaugeMinRange = 0.5f;
        private const float BasicAttackVfxTimingGaugeRangeStep = 0.5f;
        private const float MinimumWindowWidth = 1180f;
        private const float MinimumWindowHeight = 760f;
        private static readonly Color AccentColor = new Color(0.38f, 0.66f, 1f, 1f);
        private static readonly string[] EnvironmentLabels = Enumerable.Range(0, PrefabPreviewStage.EnvironmentCount)
            .Select(PrefabPreviewStage.GetEnvironmentLabel)
            .ToArray();
        private static readonly string[] RarityLabels = { "일반", "희귀", "영웅", "전설", "신화" };
        private static readonly string[] CatalogSortLabels = { "기본순", "등급순" };
        private static readonly string[] CombatTypeLabels = { "근거리", "원거리", "특수" };
        private static readonly string[] MeleeModeLabels = { "단일", "범위" };
        private static readonly string[] RangedDeliveryLabels = { "투사체", "즉발 마법" };
        private static readonly string[] RangedHitModeLabels = { "단일", "관통", "범위" };
        private static readonly string[] InstantHitModeLabels = { "단일", "범위" };
        private static readonly string[] TargetTeamLabels = { "아군", "적" };
        private static readonly string[] SkillCategoryLabels =
        {
            "전체",
            "공격",
            "방어",
            "지원",
            "제어",
            "기동",
            "소환"
        };
        private static readonly string[] SkillAugmentOperationLabels =
        {
            "효과량 증가율",
            "지속 시간 추가(초)",
            "내부 쿨다운 감소율",
            "필요 발동 횟수 감소",
            "최대 대상 수 증가",
            "반복 횟수 증가"
        };
        private static readonly string[] ImpactStrengthLabels =
        {
            "중간 공격 (Standard)",
            "빠르고 약한 공격 (Light)",
            "느리고 강한 공격 (Heavy)"
        };
        private static readonly string[] ReactionWeightLabels =
        {
            "보통 체급 (Standard)",
            "가벼운 체급 (Light)",
            "무거운 체급 (Heavy)"
        };
        private static readonly string[] MainBattleRoleLabels =
        {
            "선봉 (Vanguard)",
            "수호 (Guardian)",
            "마무리 (Finisher)",
            "사수 (Marksman)",
            "후열 추적 (Backline Hunter)"
        };
        private static readonly string[] TargetPriorityLabels =
        {
            "가장 가까운 적",
            "체력이 낮은 적",
            "원거리 적 우선"
        };
        private static readonly string[] CastleRaidAiPatternLabels =
        {
            "일반 진격형",
            "자원 약탈형",
            "포탑 사냥형",
            "수비대 사냥형",
            "성벽 파괴형",
            "위협 억제형",
            "전술 지원형"
        };
        private static readonly string[] CastleRaidSupportFocusLabels =
        {
            "상황 적응",
            "공격 강화",
            "방어 강화",
            "회복 집중"
        };
        private static readonly MonsterMakerPreviewPositionBinding AttackOriginPreviewBinding =
            new MonsterMakerPreviewPositionBinding(
                "attackOriginLocalPosition",
                "총구/공격 기준점",
                MonsterMakerPreviewPositionValueMode.RootLocal,
                MonsterMakerPreviewAnchor.Root);
        private static readonly MonsterMakerPreviewPositionBinding ModelOriginPreviewBinding =
            new MonsterMakerPreviewPositionBinding(
                "visualLocalPosition",
                "모델 기준점",
                MonsterMakerPreviewPositionValueMode.VisualLocal,
                MonsterMakerPreviewAnchor.Root);
        private static readonly MonsterMakerPreviewPositionBinding HitCenterPreviewBinding =
            new MonsterMakerPreviewPositionBinding(
                "hitCenterLocalPosition",
                "피격 기준점",
                MonsterMakerPreviewPositionValueMode.RootLocal,
                MonsterMakerPreviewAnchor.Root);
        private static readonly MonsterPreviewPositionAxis[] PreviewPositionAxes =
        {
            MonsterPreviewPositionAxis.X,
            MonsterPreviewPositionAxis.Y,
            MonsterPreviewPositionAxis.Z
        };

        private MonsterMakerDraft draft;
        private MonsterMakerDraft initialDraftSnapshot;
        private SerializedObject serializedDraft;
        private MonsterMakerPreviewStage preview;
        private MonsterMakerValidationReport validation;
        private MonsterMakerWriteResult lastWriteResult;
        private Shared.Unit.MonsterCatalog monsterCatalog;
        private Shared.Unit.MonsterRarityCatalog monsterRarityCatalog;
        private Shared.Unit.MonsterSkillCatalog monsterSkillCatalog;
        private Shared.Unit.MonsterDefinition[] catalogDefinitions =
            Array.Empty<Shared.Unit.MonsterDefinition>();
        private Shared.Unit.MonsterDefinition[] displayedCatalogDefinitions =
            Array.Empty<Shared.Unit.MonsterDefinition>();
        private readonly Dictionary<string, MonsterMakerDraft> catalogDraftsById =
            new Dictionary<string, MonsterMakerDraft>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MonsterRarity> catalogRaritiesById =
            new Dictionary<string, MonsterRarity>(StringComparer.OrdinalIgnoreCase);
        private MonsterSkillPopupData[] passiveSkillPopups = Array.Empty<MonsterSkillPopupData>();
        private MonsterSkillPopupData[] genericActiveSkillPopups = Array.Empty<MonsterSkillPopupData>();
        private MonsterSkillPopupData[] mythicActiveSkillPopups = Array.Empty<MonsterSkillPopupData>();
        private readonly MonsterPassiveBalanceEditor passiveBalanceEditor = new MonsterPassiveBalanceEditor();
        private Shared.Unit.MonsterDefinition selectedCatalogDefinition;
        private Vector2 catalogScroll;
        private Vector2 leftScroll;
        private Vector2 animationButtonScroll;
        private Vector2 usageGuideScroll;
        private Vector2 issueScroll;
        private bool ownsTransientDraft;
        private bool initializedPreview;
        private string loadedDraftAssetPath = string.Empty;
        private string loadedDraftMonsterId = string.Empty;
        private string loadedDraftFingerprint = string.Empty;
        [SerializeField] private bool showMonsterCatalog = true;
        [SerializeField] private MonsterMakerCatalogSortMode catalogSortMode;
        [SerializeField] private bool showModelAdvancedSettings;
        [SerializeField] private bool showPreviewModelReference = true;
        [SerializeField] private bool showPreviewAttackReference = true;
        [SerializeField] private bool showPreviewHitReference = true;
        private MonsterMakerPreviewReference selectedPreviewReference;
        private double previewReferenceInfoExpiresAt;
        private MonsterMakerPreviewPositionBinding activePreviewPosition;
        private MonsterPreviewPositionAxis previewPositionAxis = MonsterPreviewPositionAxis.X;
        private bool previewPositionDragging;
        private Vector2 previewPositionDragMouseStart;
        private Vector3 previewPositionDragStartValue;
        private Vector2 previewPositionDragScreenDirection = Vector2.right;
        private float previewPositionDragPixelsPerValueUnit = 40f;
        private int previewPositionHotControl;
        [SerializeField] private int activeSkillCategoryFilter;
        [SerializeField] private bool showPassiveBalanceSettings;
        [SerializeField] private bool showAdvancedActiveStepMotions;
        [SerializeField] private bool showInactiveActiveAttackAuthoring;
        [SerializeField] private bool showInactiveBasicAttackBindings;
        [SerializeField] private bool showActiveStepTunings = true;
        [SerializeField] private bool showActivePresentations;
        private bool showUsageGuide;
        private double lastRepaintTime;
        private GUIStyle headerTitleStyle;
        private GUIStyle headerMetaStyle;
        private GUIStyle columnStyle;
        private GUIStyle columnTitleStyle;
        private GUIStyle columnMetaStyle;
        private GUIStyle sectionTitleStyle;
        private GUIStyle compactButtonStyle;
        private GUIStyle actionButtonStyle;
        private GUIStyle actionPrimaryButtonStyle;
        private GUIStyle workspacePanelStyle;
        private GUIStyle actionDockStyle;
        private GUIStyle actionStatusStyle;
        private GUIStyle centeredLabelStyle;
        private GUIStyle usageLeadStyle;
        private GUIStyle usageStepTitleStyle;
        private GUIStyle usageBodyStyle;
        private GUIStyle usageCautionStyle;
        private GUIStyle catalogRowTitleStyle;
        private GUIStyle catalogRowMetaStyle;
        private GUIStyle catalogRowStateStyle;
        private GUIStyle profileNameStyle;
        private GUIStyle profileBadgeStyle;
        private GUIStyle profileSectionStyle;
        private GUIStyle profileKeyStyle;
        private GUIStyle profileValueStyle;

        [MenuItem(MenuPath)]
        public static void OpenWindow()
        {
            var window = GetWindow<MonsterMakerWindow>();
            window.titleContent = new GUIContent("Monster Maker V1 · Legacy");
            window.ApplyWindowConstraints();
            window.Show();
        }

        public static void OpenDraft(MonsterMakerDraft source)
        {
            OpenWindow();
            var window = GetWindow<MonsterMakerWindow>();
            window.SetDraft(source, false);
            window.Focus();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Monster Maker V1 · Legacy");
            ApplyWindowConstraints();
            preview = new MonsterMakerPreviewStage();
            ReloadCatalogEntries();
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.projectChanged += OnProjectChanged;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            if (Selection.activeObject is MonsterMakerDraft selected)
            {
                SetDraft(selected, false);
            }
            else if (Selection.activeObject is Shared.Unit.MonsterDefinition selectedDefinition &&
                     TryOpenDefinition(selectedDefinition, false))
            {
            }
            else
            {
                CreateTransientDraft();
            }
        }

        private void OnDisable()
        {
            ReleasePreviewPositionControl();
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.projectChanged -= OnProjectChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            preview?.Dispose();
            preview = null;
            ReleaseInitialDraftSnapshot();
            ReleaseTransientDraft();
        }

        private void OnGUI()
        {
            EnsureStyles();
            HandleDefinitionPicker();
            if (draft == null || serializedDraft == null)
            {
                EditorGUILayout.HelpBox("새 몬스터 제작을 시작하거나 기존 제작 원본을 선택하세요.", MessageType.Info);
                return;
            }

            serializedDraft.UpdateIfRequiredOrScript();
            var workspaceRect = CalculateWorkspaceRect(position.width, position.height);
            var previewColumnWidth = CalculateCenterColumnWidth(workspaceRect.width, showMonsterCatalog);

            GUILayout.BeginArea(workspaceRect);
            try
            {
                using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                {
                    if (showMonsterCatalog)
                    {
                        DrawCatalogColumn();
                        GUILayout.Space(ColumnGap);
                    }

                    DrawLeftColumn();
                    GUILayout.Space(ColumnGap);
                    DrawCenterColumn(previewColumnWidth);
                }
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        private static Rect CalculateWorkspaceRect(float windowWidth, float windowHeight)
        {
            return new Rect(
                OuterMargin,
                0f,
                Mathf.Max(1f, windowWidth - OuterMargin * 2f),
                Mathf.Max(1f, windowHeight));
        }

        private static float CalculateCenterColumnWidth(float workspaceWidth, bool catalogVisible)
        {
            var occupiedWidth = LeftColumnWidth + ColumnGap;
            if (catalogVisible)
            {
                occupiedWidth += CatalogColumnWidth + ColumnGap;
            }

            return Mathf.Max(1f, workspaceWidth - occupiedWidth);
        }

        private void DrawDraftHeader()
        {
            var rect = GUILayoutUtility.GetRect(
                LeftColumnWidth,
                DraftHeaderHeight,
                GUILayout.Width(LeftColumnWidth),
                GUILayout.ExpandWidth(false));
            var backgroundRect = new Rect(rect.x, rect.y, rect.width, rect.height - 4f);
            EditorGUI.DrawRect(backgroundRect, new Color(0.105f, 0.12f, 0.145f, 1f));
            EditorGUI.DrawRect(new Rect(backgroundRect.x, backgroundRect.yMax - 2f, backgroundRect.width, 2f), AccentColor);

            const float horizontalPadding = 12f;
            const float selectorLabelWidth = 60f;
            const float buttonGap = 4f;
            const float listToggleWidth = 92f;
            var contentWidth = backgroundRect.width - horizontalPadding * 2f;

            GUI.Label(
                new Rect(backgroundRect.x + horizontalPadding, backgroundRect.y + 7f, contentWidth - listToggleWidth - 8f, 24f),
                "Monster Maker V1 · Legacy",
                headerTitleStyle);
            GUI.Label(
                new Rect(backgroundRect.x + horizontalPadding, backgroundRect.y + 30f, contentWidth - listToggleWidth - 8f, 18f),
                BuildHeaderStatus(),
                headerMetaStyle);

            if (GUI.Button(
                    new Rect(backgroundRect.xMax - horizontalPadding - listToggleWidth, backgroundRect.y + 10f, listToggleWidth, 24f),
                    showMonsterCatalog ? "목록 닫기" : "목록 열기",
                    compactButtonStyle))
            {
                SetMonsterCatalogVisible(!showMonsterCatalog);
            }

            var selectorX = backgroundRect.x + horizontalPadding;
            var selectorWidth = contentWidth;
            var controlY = backgroundRect.y + 49f;
            GUI.Label(new Rect(selectorX, controlY, selectorLabelWidth, ControlHeight), "제작 원본", headerMetaStyle);
            var selected = (MonsterMakerDraft)EditorGUI.ObjectField(
                new Rect(selectorX + selectorLabelWidth, controlY, selectorWidth - selectorLabelWidth, ControlHeight),
                draft,
                typeof(MonsterMakerDraft),
                false);
            if (selected != draft)
            {
                if (selected == null)
                {
                    CreateTransientDraft();
                }
                else
                {
                    SetDraft(selected, false);
                }
            }

            var buttonY = backgroundRect.y + 78f;
            var smallButtonWidth = (contentWidth - buttonGap * 2f) * 0.28f;
            var openButtonWidth = contentWidth - smallButtonWidth * 2f - buttonGap * 2f;
            var buttonX = selectorX;
            if (GUI.Button(new Rect(buttonX, buttonY, smallButtonWidth, ControlHeight), "새 몬스터", compactButtonStyle))
            {
                CreateTransientDraft();
            }

            buttonX += smallButtonWidth + buttonGap;
            if (GUI.Button(new Rect(buttonX, buttonY, smallButtonWidth, ControlHeight), "원본 저장", compactButtonStyle))
            {
                SaveDraft();
            }

            buttonX += smallButtonWidth + buttonGap;
            if (GUI.Button(new Rect(buttonX, buttonY, openButtonWidth, ControlHeight), "기존 항목 열기", compactButtonStyle))
            {
                EditorGUIUtility.ShowObjectPicker<Shared.Unit.MonsterDefinition>(null, false, string.Empty, DefinitionPickerId);
            }
        }

        private void DrawCatalogColumn()
        {
            using (new EditorGUILayout.VerticalScope(
                       columnStyle,
                       GUILayout.Width(CatalogColumnWidth),
                       GUILayout.ExpandWidth(false),
                       GUILayout.ExpandHeight(true)))
            {
                DrawColumnHeader("게임 몬스터", $"{catalogDefinitions.Length}종");
                GUILayout.Space(3f);
                GUILayout.Label(
                    "Maker 제작 항목을 누르면 저장된 제작 원본이 열립니다.",
                    usageBodyStyle);
                GUILayout.Space(5f);

                var nextSortMode = (MonsterMakerCatalogSortMode)GUILayout.Toolbar(
                    (int)catalogSortMode,
                    CatalogSortLabels,
                    GUILayout.Height(22f));
                if (nextSortMode != catalogSortMode)
                {
                    SetCatalogSortMode(nextSortMode);
                }
                GUILayout.Space(5f);

                if (monsterCatalog == null)
                {
                    EditorGUILayout.HelpBox("MonsterCatalog를 찾을 수 없습니다.", MessageType.Error);
                    return;
                }

                var listRect = GUILayoutUtility.GetRect(
                    1f,
                    1f,
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true));
                DrawVirtualizedCatalogList(listRect);
            }
        }

        private void DrawVirtualizedCatalogList(Rect listRect)
        {
            var rowStride = CatalogRowHeight + CatalogRowSpacing;
            var contentHeight = Mathf.Max(
                listRect.height,
                displayedCatalogDefinitions.Length <= 0
                    ? 0f
                    : displayedCatalogDefinitions.Length * rowStride - CatalogRowSpacing);
            var contentRect = new Rect(
                0f,
                0f,
                Mathf.Max(1f, listRect.width - GUI.skin.verticalScrollbar.fixedWidth),
                contentHeight);
            catalogScroll = GUI.BeginScrollView(listRect, catalogScroll, contentRect);
            try
            {
                var visibleRange = CalculateVisibleCatalogRange(
                    catalogScroll.y,
                    listRect.height,
                    displayedCatalogDefinitions.Length);
                for (var index = visibleRange.x; index < visibleRange.y; index++)
                {
                    DrawCatalogRow(
                        displayedCatalogDefinitions[index],
                        index + 1,
                        new Rect(0f, index * rowStride, contentRect.width, CatalogRowHeight));
                }
            }
            finally
            {
                GUI.EndScrollView();
            }
        }

        private static Vector2Int CalculateVisibleCatalogRange(float scrollY, float viewportHeight, int itemCount)
        {
            if (itemCount <= 0)
            {
                return Vector2Int.zero;
            }

            var rowStride = CatalogRowHeight + CatalogRowSpacing;
            var first = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, scrollY) / rowStride), 0, itemCount - 1);
            var visibleCount = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1f, viewportHeight) / rowStride) + 1);
            return new Vector2Int(first, Mathf.Min(itemCount, first + visibleCount));
        }

        private void DrawCatalogRow(
            Shared.Unit.MonsterDefinition definition,
            int displayIndex,
            Rect rowRect)
        {
            if (definition == null)
            {
                return;
            }

            catalogDraftsById.TryGetValue(definition.MonsterId, out var makerDraft);
            var canEdit = makerDraft != null;
            var selected = selectedCatalogDefinition == definition ||
                           draft != null && string.Equals(
                               draft.MonsterId,
                               definition.MonsterId,
                               StringComparison.OrdinalIgnoreCase);
            var background = GetCatalogRowBackground(definition, selected, canEdit);
            EditorGUI.DrawRect(rowRect, background);
            if (selected)
            {
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - 2f, rowRect.width, 2f), AccentColor);
            }

            var portraitRect = new Rect(rowRect.x + 6f, rowRect.y + 6f, 40f, 40f);
            if (TryResolvePortraitPreview(definition.Portrait, out var portraitTexture, out var portraitUv))
            {
                var fittedRect = FitTextureRect(
                    portraitRect,
                    portraitUv.width * portraitTexture.width,
                    portraitUv.height * portraitTexture.height);
                GUI.DrawTextureWithTexCoords(fittedRect, portraitTexture, portraitUv, true);
            }
            else
            {
                var fallback = AssetPreview.GetMiniThumbnail(definition);
                if (fallback != null)
                {
                    GUI.DrawTexture(portraitRect, fallback, ScaleMode.ScaleToFit, true);
                }
            }

            var textX = portraitRect.xMax + 7f;
            var textWidth = rowRect.xMax - textX - 6f;
            GUI.Label(
                new Rect(textX, rowRect.y + 4f, textWidth, 18f),
                $"{displayIndex:00}  {definition.DisplayName}",
                catalogRowTitleStyle);
            GUI.Label(
                new Rect(textX, rowRect.y + 21f, textWidth, 14f),
                $"{definition.MonsterId}  ·  {GetRarityLabel(definition)}",
                catalogRowMetaStyle);
            GUI.Label(
                new Rect(textX, rowRect.y + 35f, textWidth, 13f),
                canEdit ? "Maker 수정 가능" : "기존 호환 · 제작 원본 없음",
                canEdit ? catalogRowStateStyle : catalogRowMetaStyle);

            var tooltip = canEdit
                ? $"{definition.DisplayName}의 저장된 제작 원본 열기"
                : $"{definition.DisplayName}은 Maker 이전 호환 데이터라 제작 원본이 없습니다.";
            if (!GUI.Button(rowRect, new GUIContent(string.Empty, tooltip), GUIStyle.none))
            {
                return;
            }

            selectedCatalogDefinition = definition;
            if (canEdit)
            {
                OpenDefinition(definition);
                return;
            }

            Selection.activeObject = definition;
            EditorGUIUtility.PingObject(definition);
            ShowNotification(new GUIContent("기존 호환 Monster입니다. 제작 원본은 자동 생성하지 않습니다."));
        }

        private static bool TryResolvePortraitPreview(
            Sprite portrait,
            out Texture2D texture,
            out Rect uvRect)
        {
            texture = portrait == null ? null : portrait.texture;
            if (texture == null || texture.width <= 0 || texture.height <= 0)
            {
                uvRect = default;
                return false;
            }

            Rect sourceRect;
            try
            {
                sourceRect = portrait.textureRect;
            }
            catch (InvalidOperationException)
            {
                sourceRect = portrait.rect; // Tight Atlas도 원본 Sprite 영역으로 복귀
            }

            uvRect = new Rect(
                sourceRect.x / texture.width,
                sourceRect.y / texture.height,
                sourceRect.width / texture.width,
                sourceRect.height / texture.height);
            return uvRect.width > 0f && uvRect.height > 0f;
        }

        private static Rect FitTextureRect(Rect bounds, float width, float height)
        {
            if (width <= 0f || height <= 0f)
            {
                return bounds;
            }

            var sourceAspect = width / height;
            var boundsAspect = bounds.width / bounds.height;
            if (sourceAspect > boundsAspect)
            {
                var fittedHeight = bounds.width / sourceAspect;
                return new Rect(bounds.x, bounds.center.y - fittedHeight * 0.5f, bounds.width, fittedHeight);
            }

            var fittedWidth = bounds.height * sourceAspect;
            return new Rect(bounds.center.x - fittedWidth * 0.5f, bounds.y, fittedWidth, bounds.height);
        }

        private string GetRarityLabel(Shared.Unit.MonsterDefinition definition)
        {
            if (!TryGetCatalogRarity(definition, out var rarity))
            {
                return "등급 미지정";
            }

            return GetRarityLabel(rarity);
        }

        private static string GetRarityLabel(MonsterRarity rarity)
        {
            return GetIndexedLabel(RarityLabels, (int)rarity, rarity.ToString());
        }

        private static string GetIndexedLabel(string[] labels, int index, string fallback)
        {
            return labels != null && index >= 0 && index < labels.Length ? labels[index] : fallback;
        }

        private static string GetShortIndexedLabel(string[] labels, int index, string fallback)
        {
            var label = GetIndexedLabel(labels, index, fallback);
            var suffixIndex = label.IndexOf(" (", StringComparison.Ordinal);
            return suffixIndex > 0 ? label.Substring(0, suffixIndex) : label;
        }

        private Color GetCatalogRowBackground(
            Shared.Unit.MonsterDefinition definition,
            bool selected,
            bool canEdit)
        {
            var baseColor = canEdit
                ? new Color(0.13f, 0.145f, 0.17f, 1f)
                : new Color(0.11f, 0.115f, 0.125f, 1f);
            if (!TryGetCatalogRarity(definition, out var rarity))
            {
                return selected ? new Color(0.16f, 0.27f, 0.41f, 1f) : baseColor;
            }

            var rarityColor = GetCatalogRarityColor(rarity);
            var blend = selected ? 0.58f : 0.38f;
            if (!canEdit)
            {
                blend *= 0.82f;
            }

            return Color.Lerp(baseColor, rarityColor, blend); // 카드 등급색을 목록 가독성에 맞게 완화
        }

        private bool TryGetCatalogRarity(
            Shared.Unit.MonsterDefinition definition,
            out MonsterRarity rarity)
        {
            if (definition != null && catalogRaritiesById.TryGetValue(definition.MonsterId, out rarity))
            {
                return true;
            }

            rarity = MonsterRarity.Common;
            return false;
        }

        private static Color GetCatalogRarityColor(MonsterRarity rarity)
        {
            return rarity switch
            {
                MonsterRarity.Rare => new Color32(0x31, 0x5E, 0xA2, 0xFF),
                MonsterRarity.Epic => new Color32(0x84, 0x45, 0xB0, 0xFF),
                MonsterRarity.Legendary => new Color32(0xCA, 0xB0, 0x46, 0xFF),
                MonsterRarity.Mythic => new Color32(0x9B, 0x1F, 0x1B, 0xFF),
                _ => new Color32(0x54, 0x51, 0x4D, 0xFF)
            };
        }

        private void DrawLeftColumn()
        {
            using (new EditorGUILayout.VerticalScope(
                       GUILayout.Width(LeftColumnWidth),
                       GUILayout.ExpandWidth(false),
                       GUILayout.ExpandHeight(true)))
            {
                DrawDraftHeader();
                GUILayout.Space(4f);
                using (new EditorGUILayout.VerticalScope(columnStyle, GUILayout.ExpandHeight(true)))
                {
                    DrawColumnHeader("제작 데이터", "수동 입력");
                    GUILayout.Space(4f);
                    EditorGUI.BeginChangeCheck();
                    leftScroll = EditorGUILayout.BeginScrollView(leftScroll);
                    DrawIdentitySection();
                    DrawModelSection();
                    DrawStatsSection();
                    DrawCombatIdentitySection();
                    DrawMainBattleAiSection();
                    DrawSkillSection();
                    DrawCombatSection();
                    DrawAnimationSection();
                    DrawCastleRaidAiSection();
                    DrawAscensionSection();
                    EditorGUILayout.EndScrollView();
                    if (EditorGUI.EndChangeCheck())
                    {
                        serializedDraft.ApplyModifiedProperties();
                        validation = null;
                        lastWriteResult = null;
                        RefreshPreview();
                    }
                }
            }
        }

        private void DrawCenterColumn(float previewColumnWidth)
        {
            using (var centerPanel = new EditorGUILayout.VerticalScope(
                       columnStyle,
                       GUILayout.Width(previewColumnWidth),
                       GUILayout.ExpandWidth(false),
                       GUILayout.ExpandHeight(true)))
            {
                DrawPreviewPanel();
                GUILayout.Space(6f);
                var contentWidth = Mathf.Max(1f, previewColumnWidth - columnStyle.padding.horizontal);
                DrawBottomWorkspace(contentWidth);
                DrawPanelOutline(centerPanel.rect);
            }
        }

        private void DrawBottomWorkspace(float availableWidth)
        {
            if (availableWidth < ProfileSummarySideBySideThreshold)
            {
                DrawMonsterProfileSummary(availableWidth);
                GUILayout.Space(6f);
                DrawTimeline();
                GUILayout.Space(6f);
                DrawEditControlPanel();
                GUILayout.Space(6f);
                DrawBottomActionPanel(availableWidth);
                return;
            }

            var profileWidth = Mathf.Clamp(
                availableWidth * 0.3f,
                ProfileSummaryMinWidth,
                ProfileSummaryMaxWidth);
            var actionWidth = Mathf.Max(1f, availableWidth - profileWidth - ColumnGap);
            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
            {
                DrawMonsterProfileSummary(profileWidth);
                GUILayout.Space(ColumnGap);
                using (new EditorGUILayout.VerticalScope(
                           GUILayout.Width(actionWidth),
                           GUILayout.ExpandWidth(false)))
                {
                    DrawTimeline();
                    GUILayout.Space(6f);
                    DrawEditControlPanel();
                    GUILayout.Space(6f);
                    DrawBottomActionPanel(actionWidth);
                }
            }
        }

        private void DrawMonsterProfileSummary(float width)
        {
            using (new EditorGUILayout.VerticalScope(
                       workspacePanelStyle,
                       GUILayout.Width(width),
                       GUILayout.ExpandWidth(false),
                       GUILayout.MinHeight(ProfileSummaryMinHeight),
                       GUILayout.ExpandHeight(true)))
            {
                DrawColumnHeader("몬스터 프로필", "현재 설정 요약");
                GUILayout.Space(5f);
                DrawProfileIdentity();
                DrawProfileSeparator();

                GUILayout.Label("기본 능력치", profileSectionStyle);
                DrawProfileStatRow("체력", draft.MaxHealth, "공격", draft.AttackPower);
                DrawProfileStatRow("방어", draft.Defense, "공속", draft.AttackSpeed);
                DrawProfileStatRow("이속", draft.MoveSpeed, "사거리", draft.AttackRange);
                DrawProfileSeparator();

                GUILayout.Label("전투 · 타격감", profileSectionStyle);
                var basicAttack = draft.BasicAttackProfile;
                DrawProfileLine(
                    "기본공격",
                    basicAttack == null ? "미지정" : basicAttack.DisplayName,
                    basicAttack == null ? "기본공격 Profile이 지정되지 않았습니다." : basicAttack.AttackId);
                DrawProfileLine(
                    "타격/피격",
                    $"{GetShortIndexedLabel(ImpactStrengthLabels, (int)draft.ImpactStrength, draft.ImpactStrength.ToString())} / " +
                    GetShortIndexedLabel(ReactionWeightLabels, (int)draft.ReactionWeight, draft.ReactionWeight.ToString()));
                DrawProfileLine("스킬", BuildProfileSkillSummary());
                DrawProfileSeparator();

                GUILayout.Label("AI", profileSectionStyle);
                DrawProfileLine(
                    "메인 전투",
                    $"{GetShortIndexedLabel(MainBattleRoleLabels, (int)draft.MainBattleRole, draft.MainBattleRole.ToString())} · " +
                    GetIndexedLabel(TargetPriorityLabels, (int)draft.MainBattleTargetPriority, draft.MainBattleTargetPriority.ToString()));
                DrawProfileLine(
                    "전투 거리",
                    $"희망 {draft.MainBattlePreferredRangeRatio:0.##} · 후퇴 {draft.MainBattleRetreatRangeRatio:0.##} · 재탐색 {draft.MainBattleRetargetInterval:0.##}초");
                DrawProfileLine(
                    "군단 역습",
                    GetIndexedLabel(CastleRaidAiPatternLabels, (int)draft.CastleRaidAiPattern, draft.CastleRaidAiPattern.ToString()));
                GUILayout.Space(4f);
            }
        }

        private void DrawProfileIdentity()
        {
            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(78f)))
            {
                var portraitRect = GUILayoutUtility.GetRect(
                    78f,
                    78f,
                    GUILayout.Width(78f),
                    GUILayout.Height(78f));
                if (TryResolvePortraitPreview(draft.Portrait, out var portraitTexture, out var portraitUv))
                {
                    var fittedRect = FitTextureRect(
                        portraitRect,
                        portraitUv.width * portraitTexture.width,
                        portraitUv.height * portraitTexture.height);
                    GUI.DrawTextureWithTexCoords(fittedRect, portraitTexture, portraitUv, true);
                }
                else
                {
                    GUI.Label(portraitRect, "초상화\n미지정", centeredLabelStyle);
                }

                GUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    GUILayout.Label(
                        string.IsNullOrWhiteSpace(draft.DisplayName) ? "이름 미지정" : draft.DisplayName,
                        profileNameStyle,
                        GUILayout.Height(24f));
                    var rarityRect = GUILayoutUtility.GetRect(1f, 22f, GUILayout.ExpandWidth(true), GUILayout.Height(22f));
                    EditorGUI.DrawRect(rarityRect, GetCatalogRarityColor(draft.Rarity));
                    var previousContentColor = GUI.contentColor;
                    var previousGuiColor = GUI.color;
                    GUI.contentColor = Color.white;
                    GUI.color = Color.white;
                    GUI.Label(rarityRect, GetRarityLabel(draft.Rarity), profileBadgeStyle);
                    GUI.color = previousGuiColor;
                    GUI.contentColor = previousContentColor;
                    GUILayout.Space(2f);
                    GUILayout.Label(draft.MonsterId, profileValueStyle, GUILayout.Height(16f));
                    GUILayout.Label(
                        $"{GetIndexedLabel(CombatTypeLabels, (int)draft.CombatType, draft.CombatType.ToString())} · " +
                        (draft.SkillLoadoutConfigured ? "스킬 구성" : "스킬 미사용"),
                        profileValueStyle,
                        GUILayout.Height(16f));
                }
            }
        }

        private void DrawProfileStatRow(string firstLabel, float firstValue, string secondLabel, float secondValue)
        {
            var rowRect = GUILayoutUtility.GetRect(1f, 18f, GUILayout.ExpandWidth(true), GUILayout.Height(18f));
            const float gap = 8f;
            var cellWidth = Mathf.Max(1f, (rowRect.width - gap) * 0.5f);
            DrawProfileKeyValue(new Rect(rowRect.x, rowRect.y, cellWidth, rowRect.height), firstLabel, firstValue.ToString("0.##"));
            DrawProfileKeyValue(
                new Rect(rowRect.x + cellWidth + gap, rowRect.y, cellWidth, rowRect.height),
                secondLabel,
                secondValue.ToString("0.##"));
        }

        private void DrawProfileLine(string label, string value, string tooltip = null)
        {
            var rowRect = GUILayoutUtility.GetRect(1f, 18f, GUILayout.ExpandWidth(true), GUILayout.Height(18f));
            DrawProfileKeyValue(rowRect, label, value, tooltip);
        }

        private void DrawProfileKeyValue(Rect rect, string label, string value, string tooltip = null)
        {
            const float keyWidth = 58f;
            GUI.Label(new Rect(rect.x, rect.y, keyWidth, rect.height), label, profileKeyStyle);
            GUI.Label(
                new Rect(rect.x + keyWidth, rect.y, Mathf.Max(1f, rect.width - keyWidth), rect.height),
                new GUIContent(value ?? string.Empty, tooltip ?? value ?? string.Empty),
                profileValueStyle);
        }

        private static void DrawProfileSeparator()
        {
            GUILayout.Space(5f);
            var separatorRect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true), GUILayout.Height(1f));
            EditorGUI.DrawRect(separatorRect, new Color(0.2f, 0.24f, 0.3f, 1f));
            GUILayout.Space(4f);
        }

        private string BuildProfileSkillSummary()
        {
            if (!draft.SkillLoadoutConfigured)
            {
                return "미사용";
            }

            var passive = draft.RarityPassiveSkill == null ? "패시브 미지정" : draft.RarityPassiveSkill.DisplayName;
            var active = draft.RarityActiveSkill == null ? "액티브 미지정" : draft.RarityActiveSkill.DisplayName;
            return draft.Rarity >= MonsterRarity.Epic ? $"{passive} / {active}" : passive;
        }

        private void DrawPreviewPanel()
        {
            using (new EditorGUILayout.VerticalScope(
                       workspacePanelStyle,
                       GUILayout.ExpandWidth(true),
                       GUILayout.ExpandHeight(true)))
            {
                DrawPreviewToolbar();
                GUILayout.Space(6f);
                var previewFrameRect = GUILayoutUtility.GetRect(
                    360f,
                    10000f,
                    showUsageGuide ? 140f : 220f,
                    10000f,
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true));
                EditorGUI.DrawRect(previewFrameRect, new Color(0.025f, 0.03f, 0.04f, 1f));
                var previewRect = new Rect(
                    previewFrameRect.x + 2f,
                    previewFrameRect.y + 2f,
                    Mathf.Max(1f, previewFrameRect.width - 4f),
                    Mathf.Max(1f, previewFrameRect.height - 4f));
                EditorGUI.DrawRect(previewRect, new Color(0.055f, 0.06f, 0.075f, 1f));
                var cameraChanged = preview.HandleInput(previewRect, Event.current);
                var texture = preview.RenderAfterInput(previewRect, cameraChanged);
                if (texture != null)
                {
                    GUI.DrawTexture(previewRect, texture, ScaleMode.StretchToFill, false);
                    DrawCombatPreviewOverlay(previewRect);
                    DrawPreviewPositionOverlay(previewRect);
                }
                else
                {
                    DrawPreviewEmptyState(previewRect);
                }

                DrawPreviewInputHint(previewRect);
                if (cameraChanged)
                {
                    Repaint();
                }
            }
        }

        private static void DrawPanelOutline(Rect rect)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 1f || rect.height <= 1f)
            {
                return;
            }

            var color = EditorGUIUtility.isProSkin
                ? new Color(0.08f, 0.085f, 0.095f, 1f)
                : new Color(0.42f, 0.42f, 0.42f, 1f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        private void DrawPreviewToolbar()
        {
            var previewState = draft.VendorPrefab == null
                ? "모델 미지정"
                : string.IsNullOrWhiteSpace(preview.CurrentClipName)
                    ? $"{draft.VendorPrefab.name} · 모션 대기"
                    : $"{draft.VendorPrefab.name} · {preview.CurrentClipName}";
            DrawColumnHeader("Live Preview", previewState);
            GUILayout.Space(4f);
            var toolbarRect = GUILayoutUtility.GetRect(
                1f,
                ControlHeight,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(ControlHeight));
            GUI.Label(
                new Rect(toolbarRect.x, toolbarRect.y, 54f, toolbarRect.height),
                "보기 설정",
                headerMetaStyle);

            const float environmentWidth = 94f;
            const float viewButtonWidth = 54f;
            const float gap = 4f;
            var controlsWidth = environmentWidth + viewButtonWidth * 4f + gap * 4f;
            var controlX = Mathf.Max(toolbarRect.x + 62f, toolbarRect.xMax - controlsWidth);
            var environmentRect = new Rect(controlX, toolbarRect.y, environmentWidth, toolbarRect.height);
            var environment = EditorGUI.Popup(
                environmentRect,
                preview.EnvironmentIndex,
                EnvironmentLabels);
            if (environment != preview.EnvironmentIndex)
            {
                preview.SetEnvironment(environment);
            }

            controlX = environmentRect.xMax + gap;
            if (GUI.Button(
                    new Rect(controlX, toolbarRect.y, viewButtonWidth, toolbarRect.height),
                    "정면",
                    compactButtonStyle))
            {
                preview.SetView(180f, 8f);
            }

            controlX += viewButtonWidth + gap;
            if (GUI.Button(
                    new Rect(controlX, toolbarRect.y, viewButtonWidth, toolbarRect.height),
                    "측면",
                    compactButtonStyle))
            {
                preview.SetView(90f, 8f);
            }

            controlX += viewButtonWidth + gap;
            if (GUI.Button(
                    new Rect(controlX, toolbarRect.y, viewButtonWidth, toolbarRect.height),
                    "사선",
                    compactButtonStyle))
            {
                preview.SetView(145f, 10f);
            }

        }

        private void DrawPreviewEmptyState(Rect previewRect)
        {
            GUI.Label(
                previewRect,
                "3D 모델 프리팹을 지정하세요.\n왼쪽 2. 모델 설정에서 모델과 Animator를 선택하면 Preview가 시작됩니다.",
                centeredLabelStyle);
        }

        private void DrawPreviewPositionOverlay(Rect previewRect)
        {
            if (preview == null || serializedDraft == null)
            {
                return;
            }

            var toolbarRect = MonsterPositionReferenceOverlay.DrawVisibilityToolbar(
                previewRect,
                255f,
                ref showPreviewModelReference,
                ref showPreviewAttackReference,
                ref showPreviewHitReference);
            DrawPreviewReferencePoint(
                previewRect,
                ModelOriginPreviewBinding,
                MonsterMakerPreviewReference.Model,
                MonsterPositionReferenceOverlay.ModelColor,
                showPreviewModelReference);
            DrawPreviewReferencePoint(
                previewRect,
                AttackOriginPreviewBinding,
                MonsterMakerPreviewReference.Attack,
                MonsterPositionReferenceOverlay.AttackColor,
                showPreviewAttackReference);
            DrawPreviewReferencePoint(
                previewRect,
                HitCenterPreviewBinding,
                MonsterMakerPreviewReference.Hit,
                MonsterPositionReferenceOverlay.HitColor,
                showPreviewHitReference);
            DrawPreviewReferenceInfoCard(previewRect, toolbarRect);
        }

        private void DrawPreviewReferencePoint(
            Rect previewRect,
            MonsterMakerPreviewPositionBinding binding,
            MonsterMakerPreviewReference reference,
            Color color,
            bool visible)
        {
            if (!visible || !TryGetPreviewPositionWorld(binding, out var worldPosition) ||
                !MonsterPositionReferenceOverlay.TryGetGuiPoint(
                    preview.Camera,
                    previewRect,
                    worldPosition,
                    out var guiPoint))
            {
                return;
            }

            var selected = selectedPreviewReference == reference &&
                           EditorApplication.timeSinceStartup < previewReferenceInfoExpiresAt;
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                Vector2.Distance(Event.current.mousePosition, guiPoint) <= 10f)
            {
                selectedPreviewReference = reference;
                previewReferenceInfoExpiresAt = EditorApplication.timeSinceStartup + 3d;
                selected = true;
                Event.current.Use();
                Repaint();
            }

            MonsterPositionReferenceOverlay.DrawPoint(guiPoint, color, selected);
        }

        private void DrawPreviewReferenceInfoCard(Rect previewRect, Rect toolbarRect)
        {
            if (selectedPreviewReference == MonsterMakerPreviewReference.None ||
                EditorApplication.timeSinceStartup >= previewReferenceInfoExpiresAt ||
                !TryGetSelectedPreviewReference(out var label, out var value, out var color))
            {
                return;
            }

            var width = Mathf.Min(232f, Mathf.Max(120f, previewRect.width - PreviewOverlayMargin * 2f));
            var cardRect = new Rect(
                previewRect.xMax - PreviewOverlayMargin - width,
                toolbarRect.yMax + 6f,
                width,
                42f);
            EditorGUI.DrawRect(cardRect, new Color(0.025f, 0.035f, 0.05f, 0.9f));
            EditorGUI.DrawRect(new Rect(cardRect.x, cardRect.y, 3f, cardRect.height), color);
            GUI.Label(
                new Rect(cardRect.x + 9f, cardRect.y + 3f, cardRect.width - 14f, 17f),
                label,
                EditorStyles.miniBoldLabel);
            GUI.Label(
                new Rect(cardRect.x + 9f, cardRect.y + 21f, cardRect.width - 14f, 17f),
                $"X {value.x:0.###}  ·  Y {value.y:0.###}  ·  Z {value.z:0.###}",
                EditorStyles.miniLabel);
        }

        private bool TryGetSelectedPreviewReference(out string label, out Vector3 value, out Color color)
        {
            MonsterMakerPreviewPositionBinding binding;
            switch (selectedPreviewReference)
            {
                case MonsterMakerPreviewReference.Model:
                    binding = ModelOriginPreviewBinding;
                    label = "모델 기준";
                    color = MonsterPositionReferenceOverlay.ModelColor;
                    break;
                case MonsterMakerPreviewReference.Attack:
                    binding = AttackOriginPreviewBinding;
                    label = "공격 기준";
                    color = MonsterPositionReferenceOverlay.AttackColor;
                    break;
                case MonsterMakerPreviewReference.Hit:
                    binding = HitCenterPreviewBinding;
                    label = "피격 기준";
                    color = MonsterPositionReferenceOverlay.HitColor;
                    break;
                default:
                    label = string.Empty;
                    value = Vector3.zero;
                    color = Color.clear;
                    return false;
            }

            return TryGetPreviewPositionValue(binding, out value);
        }

        private void DrawPreviewPositionToolbar(Rect previewRect)
        {
            var toolbar = CalculatePreviewPositionToolbarRect(
                previewRect,
                activePreviewPosition != null);
            EditorGUI.DrawRect(toolbar, new Color(0.025f, 0.035f, 0.05f, 0.9f));
            GUI.Label(
                new Rect(toolbar.x + 8f, toolbar.y + 3f, toolbar.width - 16f, 18f),
                activePreviewPosition == null
                    ? "청록 총구는 항상 표시 · 점을 눌러 축 조절"
                    : $"조절 중 · {activePreviewPosition.Label} · 화살표를 끌어 이동",
                EditorStyles.miniBoldLabel);
            if (activePreviewPosition == null)
            {
                return;
            }

            var x = toolbar.x + 8f;
            var y = toolbar.y + 23f;
            GUI.Label(
                new Rect(x, y, 246f, 20f),
                "빨강 X  ·  초록 Y  ·  파랑 Z  ·  축을 직접 드래그",
                EditorStyles.miniLabel);
            x += 254f;
            if (GUI.Button(new Rect(x, y, 82f, 20f), "편집 종료", EditorStyles.miniButtonRight))
            {
                activePreviewPosition = null;
                ReleasePreviewPositionControl();
                Repaint();
            }
        }

        private static Rect CalculatePreviewPositionToolbarRect(Rect previewRect, bool isEditing)
        {
            var preferredWidth = isEditing ? 360f : 294f;
            var availableWidth = Mathf.Max(1f, previewRect.width - PreviewOverlayMargin * 2f);
            return new Rect(
                previewRect.x + PreviewOverlayMargin,
                previewRect.y + PreviewOverlayMargin + CombatPreviewOverlayHeight + PreviewOverlayGap,
                Mathf.Min(preferredWidth, availableWidth),
                PositionPreviewToolbarHeight);
        }

        private void HandlePreviewPositionInput(Rect previewRect, Event current)
        {
            if (current == null || preview.Camera == null)
            {
                return;
            }

            if (previewPositionDragging)
            {
                if (current.rawType == EventType.MouseUp || current.type == EventType.MouseLeaveWindow)
                {
                    ReleasePreviewPositionControl();
                    current.Use();
                    Repaint();
                    return;
                }

                if (current.type != EventType.MouseDrag || activePreviewPosition == null)
                {
                    return;
                }

                var signedPixelDelta = Vector2.Dot(
                    current.mousePosition - previewPositionDragMouseStart,
                    previewPositionDragScreenDirection);
                var value = CalculatePreviewAxisDragValue(
                    previewPositionDragStartValue,
                    previewPositionAxis,
                    signedPixelDelta,
                    previewPositionDragPixelsPerValueUnit);
                ApplyPreviewPositionValue(activePreviewPosition, value);
                current.Use();
                return;
            }

            if (current.type != EventType.MouseDown || current.button != 0 ||
                !previewRect.Contains(current.mousePosition))
            {
                return;
            }

            if (activePreviewPosition != null &&
                TryResolvePreviewPositionAxis(
                    previewRect,
                    activePreviewPosition,
                    current.mousePosition,
                    out var selectedAxis))
            {
                BeginPreviewPositionAxisDrag(
                    previewRect,
                    activePreviewPosition,
                    selectedAxis,
                    current);
                return;
            }

            var binding = ResolveClosestPreviewPositionBinding(previewRect, current.mousePosition);
            if (binding != null)
            {
                if (preview.IsPlaying)
                {
                    preview.TogglePause();
                }
                activePreviewPosition = binding;
                previewPositionAxis = MonsterPreviewPositionAxis.X;
                ReleasePreviewPositionControl();
                current.Use();
                Repaint();
            }
        }

        private void BeginPreviewPositionAxisDrag(
            Rect previewRect,
            MonsterMakerPreviewPositionBinding binding,
            MonsterPreviewPositionAxis axis,
            Event current)
        {
            if (!TryGetPreviewPositionValue(binding, out var startValue) ||
                !TryGetPreviewPositionAxisScreenData(
                    previewRect,
                    binding,
                    axis,
                    out _,
                    out var screenDirection,
                    out var pixelsPerValueUnit))
            {
                return;
            }

            if (preview.IsPlaying)
            {
                preview.TogglePause();
            }
            serializedDraft.ApplyModifiedProperties();
            Undo.RecordObject(draft, $"{binding.Label} {axis}축 위치 조절");
            previewPositionAxis = axis;
            previewPositionDragMouseStart = current.mousePosition;
            previewPositionDragStartValue = startValue;
            previewPositionDragScreenDirection = screenDirection;
            previewPositionDragPixelsPerValueUnit = pixelsPerValueUnit;
            previewPositionDragging = true;
            previewPositionHotControl = GUIUtility.GetControlID(
                "MonsterMakerPositionAxisHandle".GetHashCode(),
                FocusType.Passive);
            GUIUtility.hotControl = previewPositionHotControl;
            current.Use();
            Repaint();
        }

        private void DrawPreviewPositionAxisHandles(
            Rect previewRect,
            MonsterMakerPreviewPositionBinding binding)
        {
            Handles.BeginGUI();
            try
            {
                for (var index = 0; index < PreviewPositionAxes.Length; index++)
                {
                    var axis = PreviewPositionAxes[index];
                    if (!TryGetPreviewPositionAxisScreenData(
                            previewRect,
                            binding,
                            axis,
                            out var origin,
                            out var direction,
                            out _))
                    {
                        continue;
                    }

                    var selected = previewPositionAxis == axis;
                    var length = CalculatePreviewAxisHandleLength(previewRect, origin, direction);
                    var end = origin + direction * length;
                    var perpendicular = new Vector2(-direction.y, direction.x);
                    var color = GetPreviewPositionAxisColor(axis);
                    Handles.color = selected ? Color.Lerp(color, Color.white, 0.25f) : color;
                    Handles.DrawAAPolyLine(selected ? 5f : 4f, origin, end);
                    Handles.DrawAAConvexPolygon(
                        end,
                        end - direction * 13f + perpendicular * 6f,
                        end - direction * 13f - perpendicular * 6f);

                    var labelRect = new Rect(end.x - 9f, end.y - 9f, 18f, 18f);
                    labelRect.x = Mathf.Clamp(labelRect.x, previewRect.x, previewRect.xMax - labelRect.width);
                    labelRect.y = Mathf.Clamp(labelRect.y, previewRect.y, previewRect.yMax - labelRect.height);
                    EditorGUI.DrawRect(labelRect, new Color(0.02f, 0.025f, 0.035f, 0.9f));
                    var previousColor = GUI.color;
                    GUI.color = color;
                    GUI.Label(labelRect, axis.ToString(), EditorStyles.centeredGreyMiniLabel);
                    GUI.color = previousColor;
                }
            }
            finally
            {
                Handles.EndGUI();
            }
        }

        private bool TryResolvePreviewPositionAxis(
            Rect previewRect,
            MonsterMakerPreviewPositionBinding binding,
            Vector2 mousePosition,
            out MonsterPreviewPositionAxis axis)
        {
            axis = previewPositionAxis;
            var closestDistance = 15f;
            var found = false;
            for (var index = 0; index < PreviewPositionAxes.Length; index++)
            {
                var candidate = PreviewPositionAxes[index];
                if (!TryGetPreviewPositionAxisScreenData(
                        previewRect,
                        binding,
                        candidate,
                        out var origin,
                        out var direction,
                        out _))
                {
                    continue;
                }

                var length = CalculatePreviewAxisHandleLength(previewRect, origin, direction);
                var end = origin + direction * length;
                var distance = DistanceToSegment(mousePosition, origin + direction * 5f, end);
                if (distance > closestDistance)
                {
                    continue;
                }

                axis = candidate;
                closestDistance = distance;
                found = true;
            }

            return found;
        }

        private bool TryGetPreviewPositionAxisScreenData(
            Rect previewRect,
            MonsterMakerPreviewPositionBinding binding,
            MonsterPreviewPositionAxis axis,
            out Vector2 origin,
            out Vector2 direction,
            out float pixelsPerValueUnit)
        {
            origin = Vector2.zero;
            direction = GetPreviewPositionAxisFallbackDirection(axis);
            pixelsPerValueUnit = 40f;
            if (!TryGetPreviewPositionValue(binding, out var value) ||
                !TryGetPreviewPositionWorld(binding, value, out var worldOrigin) ||
                !MonsterPreviewPositionHandleUtility.TryWorldToGuiPoint(
                    preview.Camera,
                    previewRect,
                    worldOrigin,
                    out origin))
            {
                return false;
            }

            const float sampleValue = 0.25f;
            var sampledValue = value + GetPreviewPositionAxisVector(axis) * sampleValue;
            if (!TryGetPreviewPositionWorld(binding, sampledValue, out var worldEnd) ||
                !MonsterPreviewPositionHandleUtility.TryWorldToGuiPoint(
                    preview.Camera,
                    previewRect,
                    worldEnd,
                    out var sampledGui))
            {
                return true;
            }

            var projectedPerValueUnit = (sampledGui - origin) / sampleValue;
            if (projectedPerValueUnit.sqrMagnitude < 16f)
            {
                return true;
            }

            direction = projectedPerValueUnit.normalized;
            pixelsPerValueUnit = Mathf.Clamp(projectedPerValueUnit.magnitude, 24f, 240f);
            return true;
        }

        private static float CalculatePreviewAxisHandleLength(
            Rect previewRect,
            Vector2 origin,
            Vector2 direction)
        {
            const float preferredLength = 64f;
            const float edgeMargin = 10f;
            var available = preferredLength;
            if (direction.x > 0.0001f)
            {
                available = Mathf.Min(available, (previewRect.xMax - edgeMargin - origin.x) / direction.x);
            }
            else if (direction.x < -0.0001f)
            {
                available = Mathf.Min(available, (previewRect.x + edgeMargin - origin.x) / direction.x);
            }
            if (direction.y > 0.0001f)
            {
                available = Mathf.Min(available, (previewRect.yMax - edgeMargin - origin.y) / direction.y);
            }
            else if (direction.y < -0.0001f)
            {
                available = Mathf.Min(available, (previewRect.y + edgeMargin - origin.y) / direction.y);
            }

            return Mathf.Clamp(available, 8f, preferredLength);
        }

        private static Vector3 CalculatePreviewAxisDragValue(
            Vector3 startValue,
            MonsterPreviewPositionAxis axis,
            float signedPixelDelta,
            float pixelsPerValueUnit)
        {
            return startValue + GetPreviewPositionAxisVector(axis) *
                (signedPixelDelta / Mathf.Max(1f, pixelsPerValueUnit));
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            var segment = end - start;
            var lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.0001f)
            {
                return Vector2.Distance(point, start);
            }

            var t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * t);
        }

        private static Vector2 GetPreviewPositionAxisFallbackDirection(MonsterPreviewPositionAxis axis)
        {
            switch (axis)
            {
                case MonsterPreviewPositionAxis.Y:
                    return Vector2.down;
                case MonsterPreviewPositionAxis.Z:
                    return new Vector2(0.7f, 0.7f).normalized;
                default:
                    return Vector2.right;
            }
        }

        private static Vector3 GetPreviewPositionAxisVector(MonsterPreviewPositionAxis axis)
        {
            switch (axis)
            {
                case MonsterPreviewPositionAxis.Y:
                    return Vector3.up;
                case MonsterPreviewPositionAxis.Z:
                    return Vector3.forward;
                default:
                    return Vector3.right;
            }
        }

        private static Color GetPreviewPositionAxisColor(MonsterPreviewPositionAxis axis)
        {
            switch (axis)
            {
                case MonsterPreviewPositionAxis.Y:
                    return new Color(0.35f, 0.95f, 0.38f, 1f);
                case MonsterPreviewPositionAxis.Z:
                    return new Color(0.3f, 0.62f, 1f, 1f);
                default:
                    return new Color(1f, 0.3f, 0.28f, 1f);
            }
        }

        private MonsterMakerPreviewPositionBinding ResolveClosestPreviewPositionBinding(
            Rect previewRect,
            Vector2 mousePosition)
        {
            MonsterMakerPreviewPositionBinding closest = null;
            var closestDistance = 14f;
            var candidates = activePreviewPosition == null ||
                             string.Equals(
                                 activePreviewPosition.PropertyPath,
                                 AttackOriginPreviewBinding.PropertyPath,
                                 StringComparison.Ordinal)
                ? new[] { AttackOriginPreviewBinding }
                : new[] { activePreviewPosition, AttackOriginPreviewBinding };
            foreach (var candidate in candidates)
            {
                if (!TryGetPreviewPositionWorld(candidate, out var worldPosition) ||
                    !MonsterPreviewPositionHandleUtility.TryWorldToGuiPoint(
                        preview.Camera,
                        previewRect,
                        worldPosition,
                        out var guiPoint))
                {
                    continue;
                }

                var distance = Vector2.Distance(mousePosition, guiPoint);
                if (distance <= closestDistance)
                {
                    closest = candidate;
                    closestDistance = distance;
                }
            }

            return closest;
        }

        private void DrawPreviewPositionMarker(
            Rect previewRect,
            MonsterMakerPreviewPositionBinding binding,
            Color color,
            bool alwaysVisible)
        {
            if (!TryGetPreviewPositionWorld(binding, out var worldPosition) ||
                !MonsterPreviewPositionHandleUtility.TryWorldToGuiPoint(
                    preview.Camera,
                    previewRect,
                    worldPosition,
                    out var guiPoint) ||
                !previewRect.Contains(guiPoint))
            {
                return;
            }

            var selected = activePreviewPosition != null &&
                           string.Equals(
                               activePreviewPosition.PropertyPath,
                               binding.PropertyPath,
                               StringComparison.Ordinal);
            Handles.BeginGUI();
            Handles.color = selected ? Color.white : color;
            Handles.DrawSolidDisc(guiPoint, Vector3.forward, selected ? 7f : 5f);
            Handles.color = Color.black;
            Handles.DrawWireDisc(guiPoint, Vector3.forward, selected ? 8f : 6f);
            Handles.EndGUI();

            if (!alwaysVisible && !selected)
            {
                return;
            }

            TryGetPreviewPositionValue(binding, out var value);
            var label = $"{binding.Label} ({value.x:0.##}, {value.y:0.##}, {value.z:0.##})";
            var labelRect = new Rect(guiPoint.x + 9f, guiPoint.y - 9f, 224f, 18f);
            EditorGUI.DrawRect(labelRect, new Color(0.02f, 0.025f, 0.035f, 0.82f));
            GUI.Label(labelRect, label, EditorStyles.miniLabel);
        }

        private bool TryGetPreviewPositionWorld(
            MonsterMakerPreviewPositionBinding binding,
            out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (!TryGetPreviewPositionValue(binding, out var value))
            {
                return false;
            }

            return TryGetPreviewPositionWorld(binding, value, out worldPosition);
        }

        private bool TryGetPreviewPositionWorld(
            MonsterMakerPreviewPositionBinding binding,
            Vector3 value,
            out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (binding == null)
            {
                return false;
            }

            if (binding.ValueMode == MonsterMakerPreviewPositionValueMode.VisualLocal)
            {
                value += Vector3.up * (serializedDraft.FindProperty("groundOffset")?.floatValue ?? 0f);
                return preview.TryGetWorldPoint(MonsterMakerPreviewAnchor.Root, string.Empty, value, out worldPosition);
            }
            if (binding.ValueMode == MonsterMakerPreviewPositionValueMode.RootLocal)
            {
                return preview.TryGetWorldPoint(MonsterMakerPreviewAnchor.Root, string.Empty, value, out worldPosition);
            }

            return preview.TryGetWorldPoint(binding.Anchor, binding.SocketPath, value, out worldPosition);
        }

        private bool TryConvertPreviewWorldToValue(
            MonsterMakerPreviewPositionBinding binding,
            Vector3 worldPosition,
            out Vector3 value)
        {
            if (binding.ValueMode == MonsterMakerPreviewPositionValueMode.AnchorOffset)
            {
                return preview.TryGetLocalPoint(binding.Anchor, binding.SocketPath, worldPosition, out value);
            }

            if (!preview.TryGetLocalPoint(MonsterMakerPreviewAnchor.Root, string.Empty, worldPosition, out value))
            {
                return false;
            }
            if (binding.ValueMode == MonsterMakerPreviewPositionValueMode.VisualLocal)
            {
                value -= Vector3.up * (serializedDraft.FindProperty("groundOffset")?.floatValue ?? 0f);
            }
            return true;
        }

        private bool TryGetPreviewPositionValue(
            MonsterMakerPreviewPositionBinding binding,
            out Vector3 value)
        {
            var property = binding == null ? null : serializedDraft.FindProperty(binding.PropertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.Vector3)
            {
                value = Vector3.zero;
                return false;
            }

            value = property.vector3Value;
            return true;
        }

        private void ApplyPreviewPositionValue(
            MonsterMakerPreviewPositionBinding binding,
            Vector3 value)
        {
            var property = binding == null ? null : serializedDraft.FindProperty(binding.PropertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.Vector3)
            {
                return;
            }

            property.vector3Value = value;
            serializedDraft.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(draft);
            validation = null;
            lastWriteResult = null;
            preview.ApplyDraftPositionOverrides();
            Repaint();
        }

        private void ReleasePreviewPositionControl()
        {
            if (previewPositionHotControl != 0 && GUIUtility.hotControl == previewPositionHotControl)
            {
                GUIUtility.hotControl = 0;
            }

            previewPositionHotControl = 0;
            previewPositionDragging = false;
        }

        private void DrawPreviewInputHint(Rect previewRect)
        {
            const float width = 190f;
            const float height = 24f;
            var rect = new Rect(previewRect.xMax - width - 10f, previewRect.yMax - height - 10f, width, height);
            EditorGUI.DrawRect(rect, new Color(0.035f, 0.045f, 0.06f, 0.82f));
            GUI.Label(rect, "우클릭 회전  ·  휠 확대/축소", EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawBottomActionPanel(float centerContentWidth)
        {
            using (new EditorGUILayout.VerticalScope(
                       actionDockStyle,
                       GUILayout.ExpandWidth(true),
                       GUILayout.ExpandHeight(true)))
            {
                DrawCommandDockHeader();

                GUILayout.Space(7f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("모션 미리보기", sectionTitleStyle, GUILayout.Width(90f));
                    GUILayout.Space(8f);
                    GUILayout.Label("모션·Marker 피해·피격 연출을 함께 확인", headerMetaStyle);
                }

                GUILayout.Space(3f);
                var actionContentWidth = Mathf.Max(
                    1f,
                    centerContentWidth - actionDockStyle.padding.horizontal);
                DrawMotionPlaybackRow(actionContentWidth);
                GUILayout.Space(7f);

                var separatorRect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(separatorRect, new Color(0.2f, 0.24f, 0.3f, 1f));
                GUILayout.Space(7f);

                var currentReport = validation ?? MonsterMakerValidator.Validate(draft);
                DrawCommandActionRow(currentReport);

                DrawValidationIssues();
                DrawUsageGuide();
            }
        }

        private void DrawCommandDockHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("미리보기 · 반영", columnTitleStyle);
                GUILayout.Space(8f);
                GUILayout.Label("모션 확인 → 입력 검증 → 전투 반영", columnMetaStyle);
                GUILayout.FlexibleSpace();
                var guideButtonLabel = showUsageGuide ? "도움말 닫기 ▲" : "도움말 ▼";
                if (GUILayout.Button(guideButtonLabel, compactButtonStyle, GUILayout.Width(106f), GUILayout.Height(26f)))
                {
                    showUsageGuide = !showUsageGuide;
                }
            }
        }

        private void DrawEditControlPanel()
        {
            using (new EditorGUILayout.VerticalScope(workspacePanelStyle, GUILayout.ExpandWidth(true)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("재생 · 편집 관리", sectionTitleStyle, GUILayout.Width(106f));
                    GUILayout.Space(6f);
                    GUILayout.Label("미리보기 재생과 현재 제작 원본의 수정 기록을 관리합니다", headerMetaStyle);
                }

                GUILayout.Space(4f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(
                            preview.IsPlaying ? "Ⅱ  일시정지" : "▶  계속 재생",
                            actionButtonStyle,
                            GUILayout.Width(108f),
                            GUILayout.Height(38f)))
                    {
                        TogglePreviewPause();
                    }

                    GUILayout.Space(ActionGap);
                    if (GUILayout.Button(
                            "↺  처음부터",
                            actionButtonStyle,
                            GUILayout.Width(108f),
                            GUILayout.Height(38f)))
                    {
                        RestartPreview();
                    }

                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(
                            new GUIContent("↶  Undo", "가장 최근 수정 내용을 되돌립니다"),
                            actionButtonStyle,
                            GUILayout.Width(94f),
                            GUILayout.Height(38f)))
                    {
                        PerformMakerUndo(false);
                    }

                    GUILayout.Space(ActionGap);
                    if (GUILayout.Button(
                            new GUIContent("↷  Redo", "되돌린 수정 내용을 다시 적용합니다"),
                            actionButtonStyle,
                            GUILayout.Width(102f),
                            GUILayout.Height(38f)))
                    {
                        PerformMakerUndo(true);
                    }

                    GUILayout.Space(ActionGap);
                    if (GUILayout.Button(
                            new GUIContent("↺  초기 상태 복원", "현재 제작 원본을 이 창에서 처음 연 상태로 복원합니다"),
                            actionButtonStyle,
                            GUILayout.Width(126f),
                            GUILayout.Height(38f)))
                    {
                        RestoreInitialDraftSnapshot();
                    }
                }
            }
        }

        private string BuildCommandStatus(MonsterMakerValidationReport currentReport)
        {
            if (currentReport.HasErrors)
            {
                var errorCount = currentReport.Issues.Count(issue =>
                    issue.Severity == MonsterMakerIssueSeverity.Error);
                return $"입력 오류 {errorCount}개 · 왼쪽 항목을 수정하세요";
            }

            if (lastWriteResult != null)
            {
                var mode = lastWriteResult.UpdatedExisting ? "GUID 유지 갱신 완료" : "신규 생성 완료";
                return $"{mode} · {lastWriteResult.Definition.MonsterId}";
            }

            return "입력 검증 후 전투 데이터에 반영합니다";
        }

        private void DrawMotionPlaybackRow(float availableWidth)
        {
            var utilityButtonWidth = Mathf.Max(1f, (availableWidth - ActionGap * 2f) / 3f);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLargeActionButton("대기 모션", Color.white, utilityButtonWidth, preview.PlayIdle, 42f);
                GUILayout.Space(ActionGap);
                DrawLargeActionButton("이동 모션", Color.white, utilityButtonWidth, preview.PlayMove, 42f);
                GUILayout.Space(ActionGap);
                DrawLargeActionButton(
                    "사망 모션",
                    new Color(0.94f, 0.84f, 0.84f, 1f),
                    utilityButtonWidth,
                    preview.PlayDeath,
                    42f);
            }

            GUILayout.Space(6f);

            var attackCount = draft.Attacks.Count;
            const float minimumAttackButtonWidth = 96f;
            const float randomButtonSize = 44f;
            var showRandomButton = attackCount >= 2;
            var showActiveButton = draft.HasActiveProfile;
            var playbackButtonCount = attackCount + (showActiveButton ? 1 : 0);
            var randomButtonSpace = showRandomButton ? randomButtonSize + ActionGap : 0f;
            var playbackRowWidth = Mathf.Max(1f, availableWidth - randomButtonSpace);
            var fittedPlaybackWidth = playbackButtonCount > 0
                ? (playbackRowWidth - ActionGap * (playbackButtonCount - 1)) / playbackButtonCount
                : playbackRowWidth;
            var needsScroll = playbackButtonCount > 0 && fittedPlaybackWidth < minimumAttackButtonWidth;
            var playbackButtonWidth = needsScroll ? minimumAttackButtonWidth : fittedPlaybackWidth;
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(playbackRowWidth)))
                {
                    if (playbackButtonCount <= 0)
                    {
                        GUILayout.Label("재생할 공격 모션이 없습니다.", centeredLabelStyle, GUILayout.Height(44f));
                    }
                    else
                    {
                        if (needsScroll)
                        {
                            animationButtonScroll.y = 0f;
                            animationButtonScroll = EditorGUILayout.BeginScrollView(
                                animationButtonScroll,
                                true,
                                false,
                                GUILayout.Height(56f));
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            for (var index = 0; index < attackCount; index++)
                            {
                                if (index > 0)
                                {
                                    GUILayout.Space(ActionGap);
                                }

                                var selectedIndex = index;
                                DrawLargeActionButton(
                                    $"공격 {index + 1:00} 재생",
                                    new Color(0.72f, 0.84f, 1f, 1f),
                                    playbackButtonWidth,
                                    () => preview.PlayAttack(selectedIndex),
                                    44f);
                            }

                            if (showActiveButton)
                            {
                                if (attackCount > 0)
                                {
                                    GUILayout.Space(ActionGap);
                                }

                                using (new EditorGUI.DisabledScope(!preview.CanPlayActiveSkill))
                                {
                                    var tooltip = preview.CanPlayActiveSkill
                                        ? $"{draft.ActiveSkillName} · 액티브 모션과 조립된 공격/효과·VFX/SFX를 함께 확인합니다."
                                        : $"{draft.ActiveSkillName} · 액티브 모션 Clip이 필요합니다.";
                                    var previousBackground = GUI.backgroundColor;
                                    GUI.backgroundColor = Color.Lerp(
                                        Color.white,
                                        MonsterWorkshopVisualTheme.FeelColor,
                                        0.58f);
                                    var playActive = GUILayout.Button(
                                            new GUIContent("◆ 액티브 재생", tooltip),
                                            actionButtonStyle,
                                            GUILayout.Width(playbackButtonWidth),
                                            GUILayout.Height(44f));
                                    GUI.backgroundColor = previousBackground;
                                    if (playActive)
                                    {
                                        preview.PlayActiveSkill();
                                        Repaint();
                                    }
                                }
                            }
                        }

                        if (needsScroll)
                        {
                            EditorGUILayout.EndScrollView();
                        }
                    }
                }

                if (showRandomButton)
                {
                    GUILayout.Space(ActionGap);
                    if (GUILayout.Button(
                            new GUIContent("랜덤\n재생", "등록된 공격 중 하나를 무작위로 재생"),
                            actionButtonStyle,
                            GUILayout.Width(randomButtonSize),
                            GUILayout.Height(randomButtonSize)))
                    {
                        preview.PlayRandomAttack();
                        Repaint();
                    }
                }

            }
        }

        private void DrawCommandActionRow(MonsterMakerValidationReport currentReport)
        {
            var statusRect = GUILayoutUtility.GetRect(
                1f,
                30f,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(30f));
            GUI.Label(
                statusRect,
                BuildCommandStatus(currentReport),
                actionStatusStyle);

            GUILayout.Space(5f);
            var publishRect = GUILayoutUtility.GetRect(
                1f,
                48f,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(48f));
            var validateWidth = Mathf.Clamp(publishRect.width * 0.34f, 168f, 230f);
            var publishWidth = Mathf.Max(1f, publishRect.width - validateWidth - 8f);

            DrawRectActionButton(
                new Rect(publishRect.x, publishRect.y, validateWidth, publishRect.height),
                "1. 입력 검증",
                new Color(1f, 0.88f, 0.62f, 1f),
                actionButtonStyle,
                ValidateDraft);

            using (new EditorGUI.DisabledScope(currentReport.HasErrors))
            {
                var actionLabel = IsEditingExistingMonster()
                    ? "2. 수정 내용 전투 반영"
                    : "2. 신규 몬스터 전투 편입";
                DrawRectActionButton(
                    new Rect(publishRect.x + validateWidth + 8f, publishRect.y, publishWidth, publishRect.height),
                    actionLabel,
                    currentReport.HasErrors ? Color.white : new Color(0.68f, 0.82f, 1f, 1f),
                    actionPrimaryButtonStyle,
                    BuildAndRegister);
            }
        }

        private void DrawLargeActionButton(string label, Color tint, float width, Action action, float height = 40f)
        {
            var previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = tint;
            if (GUILayout.Button(label, actionButtonStyle, GUILayout.Width(width), GUILayout.Height(height)))
            {
                action?.Invoke();
                Repaint();
            }

            GUI.backgroundColor = previousBackground;
        }

        private void TogglePreviewPause()
        {
            preview?.TogglePause();
        }

        private void PerformMakerUndo(bool redo)
        {
            ReleasePreviewPositionControl();
            serializedDraft?.ApplyModifiedProperties();
            if (redo)
            {
                Undo.PerformRedo();
            }
            else
            {
                Undo.PerformUndo();
            }
        }

        private void OnUndoRedoPerformed()
        {
            if (draft == null)
            {
                return;
            }

            serializedDraft = new SerializedObject(draft);
            validation = null;
            lastWriteResult = null;
            initializedPreview = false;
            RefreshPreview();
            Repaint();
        }

        private void RestoreInitialDraftSnapshot()
        {
            if (initialDraftSnapshot == null || draft == null ||
                !EditorUtility.DisplayDialog(
                    "초기 상태 복원",
                    "현재 제작 원본의 모든 값을 Maker에서 처음 열었을 때 상태로 되돌립니다.\n복원 후에도 '수정 되돌리기'로 다시 복구할 수 있습니다.",
                    "초기 상태로 복원",
                    "취소"))
            {
                return;
            }

            ApplyInitialDraftSnapshot();
        }

        private void ApplyInitialDraftSnapshot()
        {
            if (initialDraftSnapshot == null || draft == null)
            {
                return;
            }

            serializedDraft?.ApplyModifiedProperties();
            Undo.RegisterCompleteObjectUndo(draft, "Monster Maker · 초기 상태 복원");
            var draftName = draft.name;
            var draftHideFlags = draft.hideFlags;
            EditorUtility.CopySerialized(initialDraftSnapshot, draft);
            draft.name = draftName;
            draft.hideFlags = draftHideFlags;
            EditorUtility.SetDirty(draft);
            activePreviewPosition = null;
            ReleasePreviewPositionControl();
            serializedDraft = new SerializedObject(draft);
            validation = null;
            lastWriteResult = null;
            initializedPreview = false;
            RefreshPreview();
            Repaint();
        }

        private void RestartPreview()
        {
            preview?.Restart();
        }

        private static void DrawRectActionButton(
            Rect rect,
            string label,
            Color tint,
            GUIStyle style,
            Action action)
        {
            var previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = tint;
            if (GUI.Button(rect, label, style))
            {
                action();
            }

            GUI.backgroundColor = previousBackground;
        }

        private void DrawUsageGuide()
        {
            if (!showUsageGuide)
            {
                return;
            }

            GUILayout.Space(8f);
            GUILayout.Label("왼쪽 입력  →  중앙 확인  →  오류 검증  →  전투 반영", usageLeadStyle);
            GUILayout.Space(5f);
            usageGuideScroll.x = 0f;
            usageGuideScroll = EditorGUILayout.BeginScrollView(usageGuideScroll, GUILayout.Height(184f));
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(220f), GUILayout.ExpandWidth(true)))
                {
                    DrawUsageStep("1", "제작 원본을 준비합니다", "새 Monster는 [새 몬스터], 기존 Monster는 [기존 항목 열기]로 시작하고 작업 중 입력은 [원본 저장]으로 보관합니다.");
                    DrawUsageStep("2", "이름과 모델을 넣습니다", "ID·표시 이름·등급·초상화와 프로젝트의 원본 프리팹·실제 Animator를 직접 지정합니다.");
                    DrawUsageStep("3", "전투 기본값을 정합니다", "능력치, 타격 강도·피격 체급과 메인 전투 역할 AI를 정합니다. 여기서는 공격 모양이나 투사체 방식을 고르지 않습니다.");
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(220f), GUILayout.ExpandWidth(true)))
                {
                    DrawUsageStep("4", "기본공격 하나를 고릅니다", "저장된 프리셋을 선택하거나 [기본공격 조립소]에서 방식·판정·연타·투사체·공용 VFX/SFX를 한 번만 조립합니다.");
                    DrawUsageStep("5", "공격 동작과 발생 시점을 맞춥니다", "공격 Clip을 넣고 근접 접촉 또는 투사체 발사 순간을 Motion마다 0~1 값 하나로 지정합니다.");
                    DrawUsageStep("6", "나머지 Motion을 지정합니다", "대기·이동·사망 Clip을 넣습니다. 몬스터 하나만 다른 연출이 필요할 때만 기본공격의 고급 예외 보정을 펼칩니다.");
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(220f), GUILayout.ExpandWidth(true)))
                {
                    DrawUsageStep("7", "아래 작업대에서 확인합니다", "대기·이동·공격·사망과 재생 제어를 사용해 Preview 타이밍과 실제 표준 적 피해를 봅니다.");
                    DrawUsageStep("8", "검증 결과를 고칩니다", "검증은 Asset을 바꾸지 않습니다. Error가 있으면 왼쪽 입력을 고쳐야 편입할 수 있습니다.");
                    DrawUsageStep("9", "편입 뒤 실제 전투를 봅니다", "GUID 유지 반영 뒤 00_Entry → 01_MainBattle에서 이동·공격·피해·사망을 다시 확인합니다.");
                }
            }
            EditorGUILayout.EndScrollView();
            GUILayout.Label(
                "카드 초상화와 3D 모델은 다른 자산입니다. Vendor 원본은 수정하지 않으며 Maker Preview 통과가 실제 전투 검증 완료를 뜻하지 않습니다.",
                usageCautionStyle);
        }

        private void DrawUsageStep(string number, string title, string body)
        {
            GUILayout.Label($"{number}. {title}", usageStepTitleStyle);
            GUILayout.Label(body, usageBodyStyle);
            GUILayout.Space(7f);
        }

        private void DrawTimeline()
        {
            using (new EditorGUILayout.VerticalScope(workspacePanelStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("재생 위치", sectionTitleStyle, GUILayout.Width(64f));
                    var clipLabel = string.IsNullOrWhiteSpace(preview.CurrentClipName)
                        ? "모션 미선택"
                        : preview.CurrentClipName;
                    GUILayout.Label(clipLabel, headerMetaStyle);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(preview.NormalizedTime.ToString("0.000"), headerMetaStyle, GUILayout.Width(42f));
                }

                EditorGUI.BeginChangeCheck();
                var normalized = EditorGUILayout.Slider(preview.NormalizedTime, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    preview.Scrub(normalized);
                }

                var profile = draft?.BasicAttackProfile;
                var timingName = profile != null && profile.UsesProjectileVisual ? "발사" : "타격";
                GUILayout.Label(
                    $"0                         {timingName} Marker · 왼쪽 기본공격 Motion에서 수동 지정                         1",
                    EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawCombatPreviewOverlay(Rect previewRect)
        {
            const float overlayWidth = 255f;
            var overlayRect = new Rect(
                previewRect.x + PreviewOverlayMargin,
                previewRect.y + PreviewOverlayMargin,
                Mathf.Min(overlayWidth, previewRect.width - PreviewOverlayMargin * 2f),
                CombatPreviewOverlayHeight);
            if (overlayRect.width <= 60f)
            {
                return;
            }

            EditorGUI.DrawRect(overlayRect, new Color(0.035f, 0.045f, 0.06f, 0.9f));
            EditorGUI.DrawRect(new Rect(overlayRect.x, overlayRect.y, 3f, overlayRect.height), AccentColor);
            var hp = preview.CombatTargetMaximumHealth <= 0f
                ? "준비 안 됨"
                : $"HP {preview.CombatTargetCurrentHealth:N0} / {preview.CombatTargetMaximumHealth:N0}";
            GUI.Label(
                new Rect(overlayRect.x + 9f, overlayRect.y + 4f, overlayRect.width - 13f, 18f),
                $"실전 타격 시험 · {preview.CombatTargetLabel}",
                headerMetaStyle);
            GUI.Label(
                new Rect(overlayRect.x + 9f, overlayRect.y + 23f, overlayRect.width - 13f, 20f),
                preview.PreviewHitCount > 0 ? $"{hp} · 최근 피해 {preview.LastAppliedDamage:N0}" : preview.CombatStatus,
                headerMetaStyle);
        }

        private void DrawValidationIssues()
        {
            if (validation == null)
            {
                return;
            }

            var errors = validation.Issues.Count(issue => issue.Severity == MonsterMakerIssueSeverity.Error);
            var warnings = validation.Issues.Count - errors;
            GUILayout.Space(8f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var resultColor = errors > 0
                    ? new Color(1f, 0.48f, 0.42f, 1f)
                    : warnings > 0
                        ? new Color(1f, 0.76f, 0.34f, 1f)
                        : new Color(0.42f, 0.82f, 0.62f, 1f);
                var previousColor = GUI.contentColor;
                GUI.contentColor = resultColor;
                GUILayout.Label($"검증 결과  ·  오류 {errors}  /  경고 {warnings}", sectionTitleStyle);
                GUI.contentColor = previousColor;

                if (validation.Issues.Count == 0)
                {
                    GUILayout.Label("필수 검증을 통과했습니다. 전투 반영 버튼을 사용할 수 있습니다.", EditorStyles.wordWrappedMiniLabel);
                }
                else
                {
                    issueScroll = EditorGUILayout.BeginScrollView(issueScroll, GUILayout.Height(72f));
                    foreach (var issue in validation.Issues)
                    {
                        GUILayout.Label($"[{issue.Severity}] {issue.Code} · {issue.Message}", EditorStyles.wordWrappedMiniLabel);
                    }

                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawIdentitySection()
        {
            DrawSectionHeader("1. 기본 정보");
            using (new EditorGUI.DisabledScope(EditorUtility.IsPersistent(draft)))
            {
                DrawProperty("monsterId", "몬스터 ID");
            }

            if (EditorUtility.IsPersistent(draft))
            {
                GUILayout.Label("저장된 제작 원본의 ID는 파일 소유권 보호를 위해 고정됩니다.", EditorStyles.wordWrappedMiniLabel);
            }

            DrawProperty("displayName", "표시 이름");
            DrawEnumProperty("rarity", "등급", RarityLabels);
            DrawProperty("portrait", "카드 초상화");
            DrawProperty("productionMemo", "제작 메모");
        }

        private void DrawSkillSection()
        {
            DrawSectionHeader("6. 스킬");
            var rarity = (MonsterRarity)serializedDraft.FindProperty("rarity").enumValueIndex;
            var configured = serializedDraft.FindProperty("skillLoadoutConfigured");
            EditorGUILayout.PropertyField(configured, new GUIContent("스킬 사용"));
            if (!configured.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "패시브를 배정하려면 스킬 사용을 켜세요.",
                    MessageType.None);
                return;
            }

            if (monsterSkillCatalog == null)
            {
                EditorGUILayout.HelpBox(
                    "MonsterSkillCatalog을 찾을 수 없습니다. JC Tool/Monster/Rebuild Generic Skill Presets를 실행하세요.",
                    MessageType.Error);
                return;
            }

            var passiveProperty = serializedDraft.FindProperty("rarityPassiveSkill");
            var passiveOptions = passiveSkillPopups != null && passiveSkillPopups.Length > 0
                ? passiveSkillPopups[0]
                : MonsterSkillPopupData.Empty;
            var previousPassive = passiveProperty.objectReferenceValue;
            DrawSkillPopup(
                passiveProperty,
                "패시브 종류",
                passiveOptions,
                false);
            var selectedPassive = passiveProperty.objectReferenceValue as GenericMonsterPassiveSkill;
            var passiveTuning = serializedDraft.FindProperty("passiveTuning");
            if (!ReferenceEquals(previousPassive, selectedPassive))
            {
                MonsterPassiveBalanceEditor.EnsureInitialized(passiveTuning, selectedPassive, true);
            }
            passiveBalanceEditor.Draw(
                selectedPassive,
                passiveTuning,
                serializedDraft.FindProperty("displayName").stringValue,
                ref showPassiveBalanceSettings);

            var activeProperty = serializedDraft.FindProperty("rarityActiveSkill");
            var activeProfileProperty = serializedDraft.FindProperty("activeAttackProfile");
            var activeEffectProfileProperty = serializedDraft.FindProperty("activeEffectProfile");
            if (rarity < MonsterRarity.Legendary)
            {
                if (activeProperty.objectReferenceValue != null ||
                    activeProfileProperty.objectReferenceValue != null ||
                    activeEffectProfileProperty.objectReferenceValue != null)
                {
                    EditorGUILayout.HelpBox(
                        "일반·희귀·영웅 등급은 액티브를 사용할 수 없습니다. 아래 버튼으로 기존 연결을 정리하세요.",
                        MessageType.Error);
                    if (GUILayout.Button("액티브 연결 제거", GUILayout.Height(28f)))
                    {
                        Undo.RecordObject(draft, "액티브 연결 제거");
                        activeProperty.objectReferenceValue = null;
                        activeProfileProperty.objectReferenceValue = null;
                        activeEffectProfileProperty.objectReferenceValue = null;
                        serializedDraft.ApplyModifiedPropertiesWithoutUndo();
                        draft.EditorClearActiveProfiles();
                        EditorUtility.SetDirty(draft);
                        serializedDraft.UpdateIfRequiredOrScript();
                    }
                }
                else
                {
                    GUILayout.Label("일반·희귀·영웅 등급은 패시브 1개만 사용합니다.", EditorStyles.wordWrappedMiniLabel);
                }

                return;
            }

            if (activeEffectProfileProperty.objectReferenceValue != null)
            {
                DrawActiveEffectAuthoring(
                    rarity,
                    activeProfileProperty,
                    activeEffectProfileProperty,
                    activeProperty);
            }
            else
            {
                DrawActiveAttackAuthoring(
                    rarity,
                    activeProfileProperty,
                    activeEffectProfileProperty,
                    activeProperty);
            }
        }

        private void DrawActiveAttackAuthoring(
            MonsterRarity rarity,
            SerializedProperty profileProperty,
            SerializedProperty effectProfileProperty,
            SerializedProperty generatedSkillProperty)
        {
            GUILayout.Space(7f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("액티브 스킬 · 공격형", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "공격 구조는 조립소에서 만들고, 이 몬스터의 이름·기력·모션·수치·VFX/SFX는 여기서 정합니다.",
                    MessageType.None);

                var profile = profileProperty.objectReferenceValue as MonsterActiveAttackProfile;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(
                            profile == null ? "액티브 스킬 선택" : "액티브 스킬 변경",
                            GUILayout.Height(30f)))
                    {
                        ShowActiveSkillPresetMenu(profileProperty, effectProfileProperty);
                    }

                    if (GUILayout.Button("액티브 조립소 열기", GUILayout.Height(30f)))
                    {
                        serializedDraft.ApplyModifiedProperties();
                        MonsterActiveAttackWorkshopWindow.OpenFor(profile, draft);
                    }
                }

                if (profile == null)
                {
                    EditorGUILayout.HelpBox(
                        "먼저 저장된 액티브 스킬을 선택하거나 공격 조립소에서 새 프리셋을 만들어야 합니다.",
                        MessageType.Warning);
                    if (generatedSkillProperty.objectReferenceValue != null)
                    {
                        EditorGUILayout.PropertyField(generatedSkillProperty, new GUIContent("기존 액티브 (읽기 전용)"));
                    }
                    return;
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(
                        $"현재 액티브 스킬 · [{profile.ProfileId}] {profile.DisplayName}",
                        EditorStyles.boldLabel);
                    GUILayout.Label(
                        $"공격 스텝 {profile.Steps.Count}개 · 같은 프리셋도 몬스터별 수치와 연출로 다르게 조정됩니다.",
                        EditorStyles.wordWrappedMiniLabel);
                }

                if (GUILayout.Button("프로필 Step 다시 동기화", GUILayout.Height(24f)))
                {
                    serializedDraft.ApplyModifiedProperties();
                    Undo.RecordObject(draft, "액티브 Step 동기화");
                    draft.EditorSyncActiveAttackAuthoring();
                    EditorUtility.SetDirty(draft);
                    serializedDraft.UpdateIfRequiredOrScript();
                }

                DrawProperty("activeSkillName", "몬스터 고유 스킬 이름");
                DrawProperty("activeEnergyMaximum", "최대 기력");
                GUILayout.Label(
                    $"공용 획득 · 초당 {MonsterActiveEnergyConfig.SharedEnergyPerSecond:0.#} / 기본공격당 " +
                    $"{MonsterActiveEnergyConfig.SharedEnergyPerBasicAttack:0.#} · 몬스터별 밸런스는 최대 기력만 조정",
                    EditorStyles.wordWrappedMiniLabel);

                DrawActiveAttackRuntimeSyncStatus();

                DrawActiveStepMotions();
                DrawActiveStepTunings();
                DrawActivePresentations();

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(generatedSkillProperty, new GUIContent("생성된 액티브 에셋"));
                }
                GUILayout.Label(
                    rarity == MonsterRarity.Mythic
                        ? "신화 전용 에셋으로 생성됩니다. 같은 프로필도 몬스터 튜닝에 따라 다른 스킬이 됩니다."
                        : "전설 공격 액티브로 생성됩니다. 신화 전용 실행기와 구분됩니다.",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void ShowActiveSkillPresetMenu(
            SerializedProperty attackProperty,
            SerializedProperty effectProperty)
        {
            var menu = new GenericMenu();
            var currentAttack = attackProperty.objectReferenceValue as MonsterActiveAttackProfile;
            var currentEffect = effectProperty.objectReferenceValue as MonsterEffectActiveProfile;
            var attacks = FindActiveAttackPresets();
            var effects = FindActiveEffectPresets();
            if (attacks.Length == 0 && effects.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("저장된 액티브 스킬 없음"));
            }
            foreach (var preset in attacks)
            {
                var captured = preset;
                menu.AddItem(
                    new GUIContent($"공격형/[공격] [{preset.ProfileId}] {preset.DisplayName}"),
                    preset == currentAttack,
                    () => AssignActiveAttackPreset(captured));
            }
            foreach (var preset in effects)
            {
                var captured = preset;
                menu.AddItem(
                    new GUIContent(
                        $"효과형/{GetEffectRoleLabel(preset.Role)}/[{preset.ProfileId}] {preset.DisplayName}"),
                    preset == currentEffect,
                    () => AssignActiveEffectPreset(captured));
            }
            menu.ShowAsContext();
        }

        private static MonsterActiveAttackProfile[] FindActiveAttackPresets()
        {
            return AssetDatabase.FindAssets(
                    "t:MonsterActiveAttackProfile",
                    new[] { MonsterActiveAttackWorkshopWindow.ProfileRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterActiveAttackProfile>)
                .Where(profile => profile != null)
                .OrderBy(profile => profile.DisplayName, StringComparer.CurrentCulture)
                .ThenBy(profile => profile.ProfileId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static MonsterEffectActiveProfile[] FindActiveEffectPresets()
        {
            return AssetDatabase.FindAssets(
                    "t:MonsterEffectActiveProfile",
                    new[] { MonsterEffectActiveAuthoringService.ProfileRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterEffectActiveProfile>)
                .Where(profile => profile != null)
                .OrderBy(profile => profile.Role)
                .ThenBy(profile => profile.DisplayName, StringComparer.CurrentCulture)
                .ThenBy(profile => profile.ProfileId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private void AssignActiveAttackPreset(MonsterActiveAttackProfile profile)
        {
            if (draft == null || profile == null || draft.ActiveAttackProfile == profile) return;
            serializedDraft.ApplyModifiedProperties();
            Undo.RecordObject(draft, "공격형 액티브 프리셋 선택");
            draft.EditorSetActiveAttackProfile(profile);
            FinishActivePresetAssignment();
        }

        private void AssignActiveEffectPreset(MonsterEffectActiveProfile profile)
        {
            if (draft == null || profile == null || draft.ActiveEffectProfile == profile) return;
            serializedDraft.ApplyModifiedProperties();
            Undo.RecordObject(draft, "효과형 액티브 프리셋 선택");
            draft.EditorSetActiveEffectProfile(profile);
            FinishActivePresetAssignment();
        }

        private void FinishActivePresetAssignment()
        {
            EditorUtility.SetDirty(draft);
            serializedDraft.UpdateIfRequiredOrScript();
            validation = null;
            lastWriteResult = null;
            RefreshPreview();
            Repaint();
        }

        private static string GetEffectRoleLabel(MonsterEffectActiveRole role) => role switch
        {
            MonsterEffectActiveRole.Support => "지원",
            MonsterEffectActiveRole.Guard => "수호",
            MonsterEffectActiveRole.Debuff => "디버프",
            _ => role.ToString()
        };
        private void DrawActiveEffectAuthoring(
            MonsterRarity rarity,
            SerializedProperty attackProfileProperty,
            SerializedProperty effectProfileProperty,
            SerializedProperty generatedSkillProperty)
        {
            GUILayout.Space(7f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("액티브 스킬 · 효과형", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "지원·수호·디버프 효과 구조는 조립소에서 만들고, 이 몬스터의 이름·기력·모션·VFX/SFX는 여기서 정합니다.",
                    MessageType.None);
                var profile = effectProfileProperty.objectReferenceValue as MonsterEffectActiveProfile;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(
                            profile == null ? "액티브 스킬 선택" : "액티브 스킬 변경",
                            GUILayout.Height(30f)))
                    {
                        ShowActiveSkillPresetMenu(attackProfileProperty, effectProfileProperty);
                    }
                    if (GUILayout.Button("액티브 조립소 열기", GUILayout.Height(30f)))
                    {
                        serializedDraft.ApplyModifiedProperties();
                        MonsterEffectActiveWorkshopWindow.OpenFor(profile, draft);
                    }
                }

                if (profile == null)
                {
                    EditorGUILayout.HelpBox(
                        "저장된 효과형 액티브를 선택하거나 조립소에서 새 프리셋을 만드세요.",
                        MessageType.Warning);
                    return;
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(
                        $"현재 액티브 스킬 · [{GetEffectRoleLabel(profile.Role)}] " +
                        $"[{profile.ProfileId}] {profile.DisplayName}",
                        EditorStyles.boldLabel);
                    GUILayout.Label(
                        $"효과 묶음 {profile.Groups.Count}개 · 공격형과 같은 기력·발동 강조·HUD 흐름을 사용합니다.",
                        EditorStyles.wordWrappedMiniLabel);
                }

                if (GUILayout.Button("프로필 효과 묶음 다시 동기화", GUILayout.Height(24f)))
                {
                    serializedDraft.ApplyModifiedProperties();
                    Undo.RecordObject(draft, "효과형 액티브 동기화");
                    draft.EditorSyncActiveEffectAuthoring();
                    EditorUtility.SetDirty(draft);
                    serializedDraft.UpdateIfRequiredOrScript();
                }

                DrawProperty("activeSkillName", "몬스터 고유 스킬 이름");
                DrawProperty("activeEnergyMaximum", "최대 기력");
                GUILayout.Label(
                    $"공용 획득 · 초당 {MonsterActiveEnergyConfig.SharedEnergyPerSecond:0.#} / 기본공격당 " +
                    $"{MonsterActiveEnergyConfig.SharedEnergyPerBasicAttack:0.#} · 몬스터별 밸런스는 최대 기력만 조정",
                    EditorStyles.wordWrappedMiniLabel);

                DrawActiveEffectMotions(profile);
                DrawActiveEffectPresentations(profile);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(generatedSkillProperty, new GUIContent("생성된 액티브 에셋"));
                }
                GUILayout.Label(
                    rarity == MonsterRarity.Mythic
                        ? "신화 전용 효과형 액티브로 생성됩니다."
                        : "전설 효과형 액티브로 생성됩니다.",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawActiveEffectMotions(MonsterEffectActiveProfile profile)
        {
            var presentations = serializedDraft.FindProperty("activeEffectPresentations");
            var useCustomMotions = serializedDraft.FindProperty("useCustomActiveStepMotions");
            showAdvancedActiveStepMotions = EditorGUILayout.Foldout(
                showAdvancedActiveStepMotions,
                useCustomMotions.boolValue
                    ? $"고급 · 액티브 스킬 모션 · 전용 {presentations.arraySize}개"
                    : "고급 · 액티브 스킬 모션 · 기본 공격 사용",
                true);
            if (!showAdvancedActiveStepMotions) return;

            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                useCustomMotions,
                new GUIContent(
                    "묶음별 전용 모션 사용",
                    "끄면 모든 효과 묶음이 기본 공격 01 모션을 사용합니다."));
            if (EditorGUI.EndChangeCheck())
            {
                serializedDraft.ApplyModifiedProperties();
                validation = null;
                lastWriteResult = null;
                RefreshPreview();
            }
            if (!useCustomMotions.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "기본 설정입니다. 효과형도 기본 공격 01의 모션·재생 속도·첫 판정 시점을 사용합니다.",
                    MessageType.None);
                EditorGUI.indentLevel--;
                return;
            }
            if (profile == null || presentations.arraySize != profile.Groups.Count)
            {
                EditorGUILayout.HelpBox(
                    "프로필 효과 묶음과 모션 수가 다릅니다. 위 동기화 버튼을 누르세요.",
                    MessageType.Error);
                EditorGUI.indentLevel--;
                return;
            }
            if (presentations.arraySize > 1)
            {
                var first = presentations.GetArrayElementAtIndex(0);
                using (new EditorGUI.DisabledScope(
                           first.FindPropertyRelative("motionClip").objectReferenceValue == null))
                {
                    if (GUILayout.Button("1번 모션 설정을 전체 묶음에 적용", GUILayout.Height(24f)))
                    {
                        Undo.RecordObject(draft, "효과형 액티브 모션 전체 적용");
                        for (var index = 1; index < presentations.arraySize; index++)
                        {
                            CopyActiveStepMotion(first, presentations.GetArrayElementAtIndex(index));
                        }
                        serializedDraft.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(draft);
                    }
                }
            }
            for (var index = 0; index < presentations.arraySize; index++)
            {
                var presentation = presentations.GetArrayElementAtIndex(index);
                var group = profile.Groups[index];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label($"#{index + 1:00} {group.DisplayName}", EditorStyles.boldLabel);
                    DrawRelativeProperty(presentation, "motionClip", "액티브 모션 Clip");
                    DrawRelativeProperty(presentation, "motionPlaybackSpeed", "재생 속도");
                    DrawRelativeProperty(presentation, "motionCrossFadeDuration", "전환 시간(초)");
                    DrawRelativeProperty(presentation, "motionCommitNormalizedTime", "효과 적용 시점(0~1)");
                }
            }
            EditorGUI.indentLevel--;
        }

        private void DrawActiveEffectPresentations(MonsterEffectActiveProfile profile)
        {
            var presentations = serializedDraft.FindProperty("activeEffectPresentations");
            showActivePresentations = EditorGUILayout.Foldout(
                showActivePresentations,
                $"묶음별 VFX/SFX 연결 · {presentations.arraySize}개",
                true);
            if (!showActivePresentations) return;

            EditorGUI.indentLevel++;
            GUILayout.Label(
                "조립소에서 만든 현재 공간만 표시합니다. VFX·SFX 사용 여부를 먼저 정한 뒤 필요한 자산만 연결합니다.",
                EditorStyles.wordWrappedMiniLabel);
            if (profile == null || presentations.arraySize != profile.Groups.Count)
            {
                EditorGUILayout.HelpBox(
                    "프로필 효과 묶음과 연출 연결 수가 다릅니다. 위 동기화 버튼을 누르세요.",
                    MessageType.Error);
                EditorGUI.indentLevel--;
                return;
            }

            for (var index = 0; index < presentations.arraySize; index++)
            {
                var presentation = presentations.GetArrayElementAtIndex(index);
                var group = profile.Groups[index];
                presentation.isExpanded = EditorGUILayout.Foldout(
                    presentation.isExpanded,
                    $"#{index + 1:00} {group.DisplayName} · 공간 {group.PresentationSlots.Count}개",
                    true);
                if (!presentation.isExpanded) continue;

                EditorGUI.indentLevel++;
                var slots = presentation.FindPropertyRelative("slots");
                var decided = 0;
                for (var slotIndex = 0; slotIndex < group.PresentationSlots.Count; slotIndex++)
                {
                    var contract = group.PresentationSlots[slotIndex];
                    var slot = FindActivePresentationSlot(slots, contract?.SlotId);
                    if (slot == null) continue;
                    if ((MonsterBasicAttackVfxAssignmentState)slot
                            .FindPropertyRelative("vfxState").enumValueIndex !=
                        MonsterBasicAttackVfxAssignmentState.Undecided) decided++;
                    if ((MonsterBasicAttackSfxAssignmentState)slot
                            .FindPropertyRelative("sfxState").enumValueIndex !=
                        MonsterBasicAttackSfxAssignmentState.Undecided) decided++;
                }
                var progress = EditorGUILayout.GetControlRect(false, 18f);
                EditorGUI.ProgressBar(
                    progress,
                    group.PresentationSlots.Count > 0
                        ? decided / (float)(group.PresentationSlots.Count * 2)
                        : 0f,
                    $"VFX/SFX 결정 {decided}/{group.PresentationSlots.Count * 2}");

                for (var slotIndex = 0; slotIndex < group.PresentationSlots.Count; slotIndex++)
                {
                    var contract = group.PresentationSlots[slotIndex];
                    if (contract == null) continue;
                    var slot = FindActivePresentationSlot(slots, contract.SlotId);
                    if (slot == null)
                    {
                        EditorGUILayout.HelpBox(
                            $"공간 [{contract.SlotId}] 연결 데이터가 없습니다. 동기화 버튼을 누르세요.",
                            MessageType.Error);
                        continue;
                    }
                    var duration = contract.UseDuration ? $" · {contract.Duration:0.##}초 유지" : string.Empty;
                    DrawActivePresentationAssignment(
                        slot,
                        contract,
                        $"{slotIndex + 1:00} · {contract.DisplayName}",
                        $"{GetActivePresentationEventLabel(contract.Timing)} · " +
                        $"{GetActivePresentationAnchorLabel(contract.Anchor)}{duration} · {contract.Description}");
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--;
        }
        private void DrawActiveStepMotions()
        {
            var profile = serializedDraft.FindProperty("activeAttackProfile").objectReferenceValue as
                MonsterActiveAttackProfile;
            var presentations = serializedDraft.FindProperty("activeAttackPresentations");
            var useCustomMotions = serializedDraft.FindProperty("useCustomActiveStepMotions");
            showAdvancedActiveStepMotions = EditorGUILayout.Foldout(
                showAdvancedActiveStepMotions,
                useCustomMotions.boolValue
                    ? $"고급 · 액티브 스킬 모션 · 전용 {presentations.arraySize}개"
                    : "고급 · 액티브 스킬 모션 · 기본 공격 사용",
                true);
            if (!showAdvancedActiveStepMotions) return;

            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                useCustomMotions,
                new GUIContent(
                    "스텝별 전용 공격 모션 사용",
                    "끄면 모든 액티브 스텝이 기본 공격 01 모션을 사용합니다."));
            if (EditorGUI.EndChangeCheck())
            {
                serializedDraft.ApplyModifiedProperties();
                validation = null;
                lastWriteResult = null;
                RefreshPreview();
            }
            if (!useCustomMotions.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "기본 설정입니다. 모든 스텝이 기본 공격 01의 모션·재생 속도·첫 판정 시점을 사용합니다.",
                    MessageType.None);
                EditorGUI.indentLevel--;
                return;
            }

            GUILayout.Label(
                "필요할 때만 스텝별 전용 모션을 지정합니다. 같은 모션은 1번 설정을 전체에 적용할 수 있습니다.",
                EditorStyles.wordWrappedMiniLabel);
            if (profile == null || presentations.arraySize != profile.Steps.Count)
            {
                EditorGUILayout.HelpBox(
                    "프로필 스텝과 모션 수가 다릅니다. 위 동기화 버튼을 누르세요.",
                    MessageType.Error);
                EditorGUI.indentLevel--;
                return;
            }

            if (presentations.arraySize > 1)
            {
                var firstMotion = presentations.GetArrayElementAtIndex(0);
                using (new EditorGUI.DisabledScope(
                           firstMotion.FindPropertyRelative("motionClip").objectReferenceValue == null))
                {
                    if (GUILayout.Button(
                            new GUIContent(
                                "1번 모션 설정을 전체 스텝에 적용",
                                "1번 스텝의 Clip·재생 속도·전환 시간·판정 시작 시점을 나머지 스텝에 복사합니다."),
                            GUILayout.Height(24f)))
                    {
                        Undo.RecordObject(draft, "액티브 스텝 모션 전체 적용");
                        for (var index = 1; index < presentations.arraySize; index++)
                        {
                            CopyActiveStepMotion(
                                firstMotion,
                                presentations.GetArrayElementAtIndex(index));
                        }
                        serializedDraft.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(draft);
                        validation = null;
                        lastWriteResult = null;
                        RefreshPreview();
                    }
                }
            }

            for (var index = 0; index < presentations.arraySize; index++)
            {
                var presentation = presentations.GetArrayElementAtIndex(index);
                var source = profile.Steps[index];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label($"#{index + 1:00} {source.DisplayName}", EditorStyles.boldLabel);
                    DrawRelativeProperty(presentation, "motionClip", "공격 모션 Clip");
                    DrawRelativeProperty(presentation, "motionPlaybackSpeed", "재생 속도");
                    DrawRelativeProperty(presentation, "motionCrossFadeDuration", "전환 시간(초)");
                    DrawRelativeProperty(presentation, "motionCommitNormalizedTime", "판정 시작 시점(0~1)");
                }
            }
            EditorGUI.indentLevel--;
        }

        private void DrawActiveAttackRuntimeSyncStatus()
        {
            if (draft == null || draft.ActiveAttackProfile == null || string.IsNullOrWhiteSpace(draft.MonsterId))
            {
                return;
            }
            var paths = MonsterMakerAssetWriter.BuildPaths(draft.MonsterId);
            var active = AssetDatabase.LoadAssetAtPath<MonsterAttackActiveSkill>(
                MonsterMakerAssetWriter.BuildActivePath(draft.MonsterId));
            var motion = AssetDatabase.LoadAssetAtPath<MonsterMotionProfile>(paths[2]);
            var state = MonsterActiveAttackBindingProjection.EvaluateRuntimeSync(
                draft,
                active,
                motion,
                out var message);
            if (state == MonsterActiveAttackRuntimeSyncState.Synchronized)
            {
                GUILayout.Label("● 액티브 게임 자산 최신 · 공격/모션/VFX/SFX 일치", EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.HelpBox($"액티브 게임 자산 미반영 · {message}", MessageType.Warning);
            using (new EditorGUI.DisabledScope(!EditorUtility.IsPersistent(draft)))
            {
                if (!GUILayout.Button(
                        new GUIContent(
                            "액티브만 게임 자산에 반영",
                            "기본공격·스탯은 건드리지 않고 액티브 공격, Step 모션, VFX/SFX 및 Catalog 연결만 갱신합니다."),
                        GUILayout.Height(25f)))
                {
                    return;
                }
            }

            serializedDraft.ApplyModifiedProperties();
            try
            {
                var synchronized = MonsterMakerAssetWriter.SynchronizeActiveAttackRuntime(draft);
                serializedDraft.UpdateIfRequiredOrScript();
                validation = null;
                lastWriteResult = null;
                Selection.activeObject = synchronized;
                EditorGUIUtility.PingObject(synchronized);
                ShowNotification(new GUIContent("액티브 공격·모션·연출 게임 자산 반영 완료"));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("액티브 게임 자산 반영", exception.Message, "확인");
            }
            GUIUtility.ExitGUI();
        }

        private static void CopyActiveStepMotion(SerializedProperty source, SerializedProperty destination)
        {
            destination.FindPropertyRelative("motionConfigured").boolValue = true;
            destination.FindPropertyRelative("motionClip").objectReferenceValue =
                source.FindPropertyRelative("motionClip").objectReferenceValue;
            destination.FindPropertyRelative("motionPlaybackSpeed").floatValue =
                source.FindPropertyRelative("motionPlaybackSpeed").floatValue;
            destination.FindPropertyRelative("motionCrossFadeDuration").floatValue =
                source.FindPropertyRelative("motionCrossFadeDuration").floatValue;
            destination.FindPropertyRelative("motionCommitNormalizedTime").floatValue =
                source.FindPropertyRelative("motionCommitNormalizedTime").floatValue;
        }

        private void DrawActiveStepTunings()
        {
            var profile = serializedDraft.FindProperty("activeAttackProfile").objectReferenceValue as
                MonsterActiveAttackProfile;
            var tunings = serializedDraft.FindProperty("activeAttackStepTunings");
            showActiveStepTunings = EditorGUILayout.Foldout(
                showActiveStepTunings,
                $"몬스터별 스텝 수치 · {tunings.arraySize}개",
                true);
            if (!showActiveStepTunings) return;

            EditorGUI.indentLevel++;
            GUILayout.Label("1배가 프로필 원본입니다. 슬라이더 옆 숫자로 정확한 값을 입력할 수 있습니다.",
                EditorStyles.wordWrappedMiniLabel);
            if (profile == null || tunings.arraySize != profile.Steps.Count)
            {
                EditorGUILayout.HelpBox(
                    "프로필 스텝과 튜닝 수가 다릅니다. 위 동기화 버튼을 누르세요.",
                    MessageType.Error);
                EditorGUI.indentLevel--;
                return;
            }

            for (var index = 0; index < tunings.arraySize; index++)
            {
                var tuning = tunings.GetArrayElementAtIndex(index);
                var source = profile.Steps[index];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label($"#{index + 1:00} {source.DisplayName} · {source.BuildSummary()}",
                        EditorStyles.wordWrappedMiniLabel);
                    using (new EditorGUI.DisabledScope(true))
                    {
                        DrawRelativeProperty(tuning, "stepId", "스텝 ID");
                    }
                    DrawTuningScale(tuning.FindPropertyRelative("damageScale"), "피해 배율");
                    DrawTuningScale(tuning.FindPropertyRelative("sizeScale"), "크기/범위 배율");
                    DrawTuningScale(tuning.FindPropertyRelative("timingScale"), "시간 배율");
                    if (source.IsProjectile)
                    {
                        var projectileCount = tuning.FindPropertyRelative("projectileCountOverride");
                        projectileCount.intValue = EditorGUILayout.IntSlider(
                            new GUIContent("투사체 개수 덮어쓰기", "0이면 프로필 원본 개수를 사용합니다."),
                            projectileCount.intValue,
                            0,
                            12);
                        GUILayout.Label("0 = 프로필 원본", EditorStyles.miniLabel);
                    }
                }
            }
            EditorGUI.indentLevel--;
        }

        private static void DrawTuningScale(SerializedProperty property, string label)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                property.floatValue = EditorGUILayout.Slider(label, property.floatValue, 0.25f, 3f);
                if (GUILayout.Button("1배", EditorStyles.miniButton, GUILayout.Width(38f)))
                {
                    property.floatValue = 1f;
                    GUI.FocusControl(null);
                }
            }
        }

        private void DrawActivePresentations()
        {
            var profile = serializedDraft.FindProperty("activeAttackProfile").objectReferenceValue as
                MonsterActiveAttackProfile;
            var presentations = serializedDraft.FindProperty("activeAttackPresentations");
            showActivePresentations = EditorGUILayout.Foldout(
                showActivePresentations,
                $"Step별 VFX/SFX 연결 · {presentations.arraySize}개",
                true);
            if (!showActivePresentations) return;

            EditorGUI.indentLevel++;
            GUILayout.Label("조립소에서 만든 현재 공간만 표시합니다. VFX·SFX는 각각 사용 여부를 결정하고 FEEL은 공격 프로필의 공통 프리셋을 사용합니다.",
                EditorStyles.wordWrappedMiniLabel);
            if (profile == null || presentations.arraySize != profile.Steps.Count)
            {
                EditorGUILayout.HelpBox("프로필 Step과 연출 연결 수가 다릅니다. 위 동기화 버튼을 누르세요.", MessageType.Error);
                EditorGUI.indentLevel--;
                return;
            }

            for (var index = 0; index < presentations.arraySize; index++)
            {
                var presentation = presentations.GetArrayElementAtIndex(index);
                var source = profile.Steps[index];
                presentation.isExpanded = EditorGUILayout.Foldout(
                    presentation.isExpanded,
                    $"#{index + 1:00} {source.DisplayName} · 공간 {source.PresentationSlots.Count}개",
                    true);
                if (!presentation.isExpanded) continue;

                EditorGUI.indentLevel++;
                var slots = presentation.FindPropertyRelative("slots");
                var vfxDecided = 0;
                var sfxDecided = 0;
                for (var slotIndex = 0; slotIndex < source.PresentationSlots.Count; slotIndex++)
                {
                    var contract = source.PresentationSlots[slotIndex];
                    var slot = FindActivePresentationSlot(slots, contract?.SlotId);
                    if (slot == null) continue;
                    var vfxState = (MonsterBasicAttackVfxAssignmentState)slot
                        .FindPropertyRelative("vfxState").enumValueIndex;
                    var sfxState = (MonsterBasicAttackSfxAssignmentState)slot
                        .FindPropertyRelative("sfxState").enumValueIndex;
                    if (vfxState != MonsterBasicAttackVfxAssignmentState.Undecided) vfxDecided++;
                    if (sfxState != MonsterBasicAttackSfxAssignmentState.Undecided) sfxDecided++;
                }
                var progressRect = EditorGUILayout.GetControlRect(false, 18f);
                EditorGUI.ProgressBar(
                    progressRect,
                    source.PresentationSlots.Count > 0
                        ? (vfxDecided + sfxDecided) / (float)(source.PresentationSlots.Count * 2)
                        : 0f,
                    $"VFX 결정 {vfxDecided}/{source.PresentationSlots.Count} · SFX 결정 {sfxDecided}/{source.PresentationSlots.Count}");

                if (source.PresentationSlots.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "이 Step에는 VFX/SFX 공간 계약이 없습니다. 액티브 조립소에서 추가할 수 있습니다.",
                        MessageType.None);
                }
                for (var slotIndex = 0; slotIndex < source.PresentationSlots.Count; slotIndex++)
                {
                    var contract = source.PresentationSlots[slotIndex];
                    if (contract == null) continue;
                    var slot = FindActivePresentationSlot(slots, contract.SlotId);
                    if (slot == null)
                    {
                        EditorGUILayout.HelpBox(
                            $"공간 [{contract.SlotId}] 연결 데이터가 없습니다. 액티브 프로필 동기화를 실행하세요.",
                            MessageType.Error);
                        continue;
                    }
                    var timing = GetActivePresentationEventLabel(contract.Timing);
                    var anchor = GetActivePresentationAnchorLabel(contract.Anchor);
                    var duration = contract.UseDuration ? $" · VFX {contract.Duration:0.##}초 유지" : string.Empty;
                    var help = string.IsNullOrWhiteSpace(contract.Description)
                        ? $"{timing} 시점 · {anchor} 기준{duration}"
                        : $"{timing} 시점 · {anchor} 기준{duration} · {contract.Description}";
                    DrawActivePresentationAssignment(
                        slot,
                        contract,
                        $"{slotIndex + 1:00} · {contract.DisplayName}",
                        help);
                }
                EditorGUI.indentLevel--;
            }
            DrawInactiveActiveAttackAuthoring();
            EditorGUI.indentLevel--;
        }

        private void DrawActivePresentationAssignment(
            SerializedProperty slot,
            MonsterActivePresentationSlot contract,
            string label,
            string help)
        {
            var feedback = slot.FindPropertyRelative("feedback");
            var vfxState = slot.FindPropertyRelative("vfxState");
            var sfxState = slot.FindPropertyRelative("sfxState");
            if (feedback == null || vfxState == null || sfxState == null) return;

            var currentVfxState = (MonsterBasicAttackVfxAssignmentState)vfxState.enumValueIndex;
            var currentSfxState = (MonsterBasicAttackSfxAssignmentState)sfxState.enumValueIndex;
            var fullyDisabled = currentVfxState == MonsterBasicAttackVfxAssignmentState.Disabled &&
                                currentSfxState == MonsterBasicAttackSfxAssignmentState.Disabled;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (fullyDisabled)
                    {
                        slot.isExpanded = EditorGUILayout.Foldout(
                            slot.isExpanded,
                            new GUIContent(label, help),
                            true);
                    }
                    else
                    {
                        GUILayout.Label(new GUIContent(label, help), EditorStyles.miniBoldLabel);
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(
                        fullyDisabled ? "SFX/VFX 모두 사용 안 함" : "선택",
                        EditorStyles.miniLabel);
                }
                if (fullyDisabled && !slot.isExpanded) return;

                GUILayout.Label("SFX", EditorStyles.miniBoldLabel);
                var selectedSfx = EditorGUILayout.Popup(
                    "사용 상태",
                    ToSfxAssignmentPopupIndex(currentSfxState),
                    new[] { "미결정", "사용 안 함", "SFX 사용" });
                var resolvedSfx = FromSfxAssignmentPopupIndex(selectedSfx);
                sfxState.enumValueIndex = (int)resolvedSfx;
                var sound = feedback.FindPropertyRelative("sound");
                var legacyCue = feedback.FindPropertyRelative("sfx");
                if (resolvedSfx == MonsterBasicAttackSfxAssignmentState.Undecided)
                {
                    DrawBasicAttackVfxNotice("SFX 사용 여부를 결정하세요.", MessageType.None);
                }
                else if (resolvedSfx == MonsterBasicAttackSfxAssignmentState.Disabled)
                {
                    SfxEditorAudioPreview.StopAll();
                    DrawBasicAttackVfxNotice("이 공간에서는 SFX를 사용하지 않습니다.", MessageType.None);
                }
                else
                {
                    var legacy = legacyCue.objectReferenceValue as SfxCue;
                    var displayed = sound.objectReferenceValue as AudioClip ?? ResolveFirstAudioClip(legacy);
                    EditorGUI.BeginChangeCheck();
                    var selected = EditorGUILayout.ObjectField(
                        "SFX 원본 클립",
                        displayed,
                        typeof(AudioClip),
                        false) as AudioClip;
                    if (EditorGUI.EndChangeCheck())
                    {
                        sound.objectReferenceValue = selected;
                        legacyCue.objectReferenceValue = null;
                    }
                    if (selected == null)
                    {
                        DrawBasicAttackVfxNotice(
                            "SFX 사용 상태이지만 AudioClip이 비어 있습니다.",
                            MessageType.Error);
                    }
                    else
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("SFX 미리듣기", compactButtonStyle))
                            {
                                SfxEditorAudioPreview.Play(selected, 0, false, 1f);
                            }
                            if (GUILayout.Button("SFX 정지", compactButtonStyle))
                            {
                                SfxEditorAudioPreview.StopAll();
                            }
                        }
                    }
                }

                GUILayout.Space(3f);
                GUILayout.Label("VFX", EditorStyles.miniBoldLabel);
                var selectedVfx = EditorGUILayout.Popup(
                    "사용 상태",
                    ToVfxAssignmentPopupIndex(currentVfxState),
                    new[] { "미결정", "사용 안 함", "VFX 사용" });
                var resolvedVfx = FromVfxAssignmentPopupIndex(selectedVfx);
                vfxState.enumValueIndex = (int)resolvedVfx;
                if (resolvedVfx == MonsterBasicAttackVfxAssignmentState.Undecided)
                {
                    DrawBasicAttackVfxNotice("VFX 사용 여부를 결정하세요.", MessageType.None);
                }
                else if (resolvedVfx == MonsterBasicAttackVfxAssignmentState.Disabled)
                {
                    DrawBasicAttackVfxNotice("이 공간에서는 VFX를 사용하지 않습니다.", MessageType.None);
                }
                else
                {
                    var vfx = feedback.FindPropertyRelative("vfxPrefab");
                    EditorGUILayout.PropertyField(
                        vfx,
                        new GUIContent(
                            contract.Attachment == MonsterActivePresentationAttachment.DeliveryVisual
                                ? "이동체 VFX Prefab"
                                : "VFX Prefab"));
                    if (vfx.objectReferenceValue == null)
                    {
                        DrawBasicAttackVfxNotice(
                            "VFX 사용 상태이지만 Prefab이 비어 있습니다.",
                            MessageType.Error);
                    }
                    else
                    {
                        if (contract.EndPolicy is MonsterActivePresentationEndPolicy.Timed or
                            MonsterActivePresentationEndPolicy.ParticleDuration)
                        {
                            if (contract.UseDuration)
                            {
                                GUILayout.Label(
                                    $"계약 지속시간 {contract.Duration:0.##}초 사용",
                                    EditorStyles.miniLabel);
                            }
                            else
                            {
                                DrawRelativeProperty(feedback, "vfxLifetime", "VFX 유지 시간");
                            }
                        }
                        var position = feedback.FindPropertyRelative("localPosition");
                        DrawRelativeProperty(feedback, "localPosition", "VFX 위치 보정");
                        DrawPreviewPositionButton(
                            position,
                            label + " VFX",
                            MonsterMakerPreviewPositionValueMode.AnchorOffset,
                            ResolveActivePresentationPreviewAnchor(contract.Anchor));
                        DrawRelativeProperty(feedback, "localEulerAngles", "VFX 회전 보정");
                        DrawRelativeProperty(feedback, "scale", "VFX 크기");
                    }
                }
                GUILayout.Label(help, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawInactiveActiveAttackAuthoring()
        {
            if (draft == null || draft.InactiveActiveAttackAuthoringCount <= 0) return;
            showInactiveActiveAttackAuthoring = EditorGUILayout.Foldout(
                showInactiveActiveAttackAuthoring,
                $"고급 · 이전 액티브 프리셋/공간 값 {draft.InactiveActiveAttackAuthoringCount}개 보관 중",
                true);
            if (!showInactiveActiveAttackAuthoring) return;

            EditorGUILayout.HelpBox(
                "현재 액티브에는 사용되지 않지만 이전 프리셋이나 제거된 공간으로 돌아갈 때 복원됩니다. 미리보기와 게임 자산에는 출력되지 않습니다.",
                MessageType.None);
            var archives = serializedDraft.FindProperty("inactiveActiveAttackAuthoring");
            for (var index = 0; archives != null && index < archives.arraySize; index++)
            {
                var archive = archives.GetArrayElementAtIndex(index);
                var id = archive.FindPropertyRelative("profileId").stringValue;
                var tuningCount = archive.FindPropertyRelative("tunings").arraySize;
                var presentationCount = archive.FindPropertyRelative("presentations").arraySize;
                GUILayout.Label(
                    $"[{id}] 수치 {tuningCount} · Step/공간 {presentationCount}",
                    EditorStyles.miniLabel);
            }
        }

        private static SerializedProperty FindActivePresentationSlot(SerializedProperty slots, string slotId)
        {
            if (slots == null || string.IsNullOrWhiteSpace(slotId)) return null;
            for (var index = 0; index < slots.arraySize; index++)
            {
                var slot = slots.GetArrayElementAtIndex(index);
                if (string.Equals(
                        slot.FindPropertyRelative("slotId").stringValue,
                        slotId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return slot;
                }
            }
            return null;
        }

        private static bool HasActivePresentationFeedback(SerializedProperty feedback)
        {
            if (feedback == null) return false;
            return feedback.FindPropertyRelative("sound").objectReferenceValue != null ||
                   feedback.FindPropertyRelative("sfx").objectReferenceValue != null ||
                   feedback.FindPropertyRelative("vfxPrefab").objectReferenceValue != null;
        }

        private static MonsterMakerPreviewAnchor ResolveActivePresentationPreviewAnchor(
            MonsterActivePresentationAnchor anchor) => anchor switch
        {
            MonsterActivePresentationAnchor.AttackOrigin => MonsterMakerPreviewAnchor.AttackOrigin,
            MonsterActivePresentationAnchor.MarkerSocket => MonsterMakerPreviewAnchor.AttackOrigin,
            MonsterActivePresentationAnchor.TrajectoryOrigin => MonsterMakerPreviewAnchor.AttackOrigin,
            MonsterActivePresentationAnchor.TargetPoint => MonsterMakerPreviewAnchor.HitCenter,
            MonsterActivePresentationAnchor.TargetRoot => MonsterMakerPreviewAnchor.HitCenter,
            MonsterActivePresentationAnchor.HitPoint => MonsterMakerPreviewAnchor.HitCenter,
            MonsterActivePresentationAnchor.AreaCenter => MonsterMakerPreviewAnchor.HitCenter,
            _ => MonsterMakerPreviewAnchor.Root
        };

        private static string GetActivePresentationEventLabel(MonsterActivePresentationEvent timing) => timing switch
        {
            MonsterActivePresentationEvent.Telegraph => "판정 예고",
            MonsterActivePresentationEvent.Launch => "공격 발동",
            MonsterActivePresentationEvent.Travel => "이동체 / 빔",
            MonsterActivePresentationEvent.Impact => "실제 타격",
            MonsterActivePresentationEvent.TeleportExit => "순간이동 출발",
            MonsterActivePresentationEvent.TeleportEnter => "순간이동 도착",
            MonsterActivePresentationEvent.MotionStart => "모션 시작",
            MonsterActivePresentationEvent.DeliverySpawn => "이동체 생성",
            MonsterActivePresentationEvent.AreaResolved => "범위 판정 완료",
            MonsterActivePresentationEvent.DeliveryEnd => "이동체 종료",
            MonsterActivePresentationEvent.StepEnd => "Step 종료",
            _ => timing.ToString()
        };

        private static string GetActivePresentationAnchorLabel(MonsterActivePresentationAnchor anchor) => anchor switch
        {
            MonsterActivePresentationAnchor.CasterRoot => "시전자 중심",
            MonsterActivePresentationAnchor.AttackOrigin => "공격 원점",
            MonsterActivePresentationAnchor.TargetPoint => "대상 지점",
            MonsterActivePresentationAnchor.MarkerSocket => "모션 소켓",
            MonsterActivePresentationAnchor.ProjectileRoot => "이동체 중심",
            MonsterActivePresentationAnchor.TargetRoot => "대상 중심",
            MonsterActivePresentationAnchor.HitPoint => "실제 명중점",
            MonsterActivePresentationAnchor.AreaCenter => "범위 중심",
            MonsterActivePresentationAnchor.TrajectoryOrigin => "진행 경로 원점",
            _ => anchor.ToString()
        };

        private static MonsterSkillPopupData DrawSkillCategoryFilter(
            MonsterSkillPopupData[] cachedPopups,
            ref int filter,
            string label)
        {
            filter = Mathf.Clamp(filter, 0, SkillCategoryLabels.Length - 1);
            filter = EditorGUILayout.Popup(label, filter, SkillCategoryLabels);
            var popup = cachedPopups != null && filter < cachedPopups.Length
                ? cachedPopups[filter]
                : MonsterSkillPopupData.Empty;
            GUILayout.Label($"표시 중 {Mathf.Max(0, popup.Options.Length - 1)}개", EditorStyles.miniLabel);
            return popup;
        }

        private static void DrawSkillPopup(
            SerializedProperty property,
            string label,
            MonsterSkillPopupData popup,
            bool showRecipe = true)
        {
            var current = property.objectReferenceValue as MonsterSkillDefinitionBase;
            var options = popup?.Options ?? MonsterSkillPopupData.Empty.Options;
            var labels = popup?.Labels ?? MonsterSkillPopupData.Empty.Labels;
            var currentIndex = Array.IndexOf(options, current);
            if (current != null && currentIndex < 0)
            {
                var expandedOptions = new MonsterSkillDefinitionBase[options.Length + 1];
                var expandedLabels = new string[labels.Length + 1];
                Array.Copy(options, expandedOptions, options.Length);
                Array.Copy(labels, expandedLabels, labels.Length);
                expandedOptions[expandedOptions.Length - 1] = current;
                expandedLabels[expandedLabels.Length - 1] = BuildSkillPopupLabel(current);
                options = expandedOptions;
                labels = expandedLabels;
                currentIndex = expandedOptions.Length - 1;
            }
            currentIndex = Mathf.Max(0, currentIndex);
            var selectedIndex = EditorGUILayout.Popup(label, currentIndex, labels);
            property.objectReferenceValue = options[Mathf.Clamp(selectedIndex, 0, options.Length - 1)];

            var selected = property.objectReferenceValue as MonsterSkillDefinitionBase;
            if (selected == null)
            {
                return;
            }

            if (!selected.AuthoringEnabled)
            {
                EditorGUILayout.HelpBox(
                    "현재 P0 고도화 대상이 아닌 비활성 스킬입니다. 다른 스킬로 바꾸거나 제거해야 저장할 수 있습니다.",
                    MessageType.Warning);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (!string.IsNullOrWhiteSpace(selected.Description))
                {
                    GUILayout.Label(selected.Description, EditorStyles.wordWrappedMiniLabel);
                }

                if (showRecipe)
                {
                    GUILayout.Label(selected.RecipeSummary, EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        private static string BuildSkillPopupLabel(MonsterSkillDefinitionBase skill)
        {
            if (skill == null)
            {
                return "<미설정>";
            }

            return $"[{SkillCategoryLabels[(int)skill.Category + 1]}] {skill.DisplayName}" +
                   (!skill.AuthoringEnabled ? " · 비활성" : string.Empty) +
                   (skill is MonsterActiveSkill active &&
                    active.ExecutionKind == MonsterActiveExecutionKind.DedicatedMythic
                       ? " · 신화 전용"
                       : string.Empty);
        }

        private void DrawModelSection()
        {
            DrawSectionHeader("2. 모델 설정");
            DrawProperty("vendorPrefab", "3D 모델 프리팹");
            DrawProperty("animatorSource", "모델 애니메이터");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                showModelAdvancedSettings = EditorGUILayout.Foldout(
                    showModelAdvancedSettings,
                    "모델 상세 보정 (필요할 때만)",
                    true);
                if (showModelAdvancedSettings)
                {
                    EditorGUI.indentLevel++;
                    DrawProperty("visualScale", "모델 크기");
                    DrawPositionPropertyWithPreviewHandle(
                        "visualLocalPosition",
                        "모델 위치",
                        "모델 위치",
                        MonsterMakerPreviewPositionValueMode.VisualLocal,
                        MonsterMakerPreviewAnchor.Root);
                    DrawProperty("groundOffset", "바닥 높이 보정");
                    DrawProperty("facingYawOffset", "정면 회전 보정");
                    DrawPositionPropertyWithPreviewHandle(
                        "attackOriginLocalPosition",
                        "공격 기준점 위치",
                        "총구/공격 기준점",
                        MonsterMakerPreviewPositionValueMode.RootLocal,
                        MonsterMakerPreviewAnchor.Root);
                    DrawPositionPropertyWithPreviewHandle(
                        "hitCenterLocalPosition",
                        "피격 기준점 위치",
                        "피격 중심",
                        MonsterMakerPreviewPositionValueMode.RootLocal,
                        MonsterMakerPreviewAnchor.Root);
                    EditorGUI.indentLevel--;
                }
                else
                {
                    GUILayout.Label(
                        "크기·위치·바닥·정면·공격/피격 기준점",
                        EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        private void DrawStatsSection()
        {
            DrawSectionHeader("3. 기본 능력치");
            DrawProperty("maxHealth", "체력");
            DrawProperty("attackPower", "공격력");
            DrawProperty("defense", "방어력");
            DrawProperty("attackSpeed", "공격 속도");
            DrawProperty("moveSpeed", "이동 속도");
            DrawProperty("attackRange", "기준 공격 거리");
            EditorGUILayout.HelpBox(
                "여기는 몬스터의 전투 수치입니다. 공격 모양·연타·투사체·판정 배율은 7번 기본공격 프리셋에서 정하며, 최종 판정 거리는 이 기준 공격 거리에 프리셋 배율을 적용합니다.",
                MessageType.None);
        }

        private void DrawAnimationSection()
        {
            DrawSectionHeader("8. 대기 · 이동 · 사망 Motion");
            DrawProperty("idleClip", "대기 애니메이션");
            DrawProperty("moveClip", "이동 애니메이션");
            DrawProperty("deathClip", "사망 애니메이션");
            DrawOptionalAnimationFeedback(
                serializedDraft.FindProperty("deathFeedback"),
                "사망 애니메이션 시작",
                "사망 사운드",
                "사망 사운드는 사망 애니메이션을 시작할 때 재생됩니다. AudioClip만 지정하면 전투 편입 때 SFX Cue를 자동 생성합니다.",
                MonsterMakerPreviewAnchor.HitCenter);
        }

        private void DrawAttackList()
        {
            var attacks = serializedDraft.FindProperty("attacks");
            if (attacks == null)
            {
                return;
            }

            GUILayout.Space(3f);
            GUILayout.Label("공격 애니메이션", EditorStyles.boldLabel);
            for (var attackIndex = 0; attackIndex < attacks.arraySize; attackIndex++)
            {
                var attack = attacks.GetArrayElementAtIndex(attackIndex);
                if (DrawAttack(attack, attackIndex, attacks.arraySize > 1))
                {
                    attacks.DeleteArrayElementAtIndex(attackIndex);
                    break;
                }
            }

            if (GUILayout.Button("공격 애니메이션 추가", compactButtonStyle, GUILayout.Height(24f)))
            {
                AddAttack(attacks);
            }
        }

        private bool DrawAttack(SerializedProperty attack, int attackIndex, bool canRemove)
        {
            var clip = attack.FindPropertyRelative("clip");
            var clipName = clip.objectReferenceValue == null ? "미지정" : clip.objectReferenceValue.name;
            attack.isExpanded = EditorGUILayout.Foldout(
                attack.isExpanded,
                $"공격 {attackIndex + 1:00} · {clipName}",
                true);
            if (!attack.isExpanded)
            {
                return false;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawRelativeProperty(attack, "clip", "공격 애니메이션");
                DrawRelativeProperty(attack, "playbackSpeed", "재생 속도");
                DrawRelativeProperty(attack, "crossFadeDuration", "전환 시간");
                if (canRemove)
                {
                    DrawRelativeProperty(attack, "weight", "무작위 선택 비중");
                    DrawRelativeProperty(attack, "preventImmediateRepeat", "같은 동작 연속 방지");
                }
                DrawAttackMarkers(attack.FindPropertyRelative("markers"));
                DrawBreathDurationOverride(attack);
                if (canRemove)
                {
                    if (GUILayout.Button("이 공격 삭제", compactButtonStyle, GUILayout.Height(22f)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void DrawAttackMarkers(SerializedProperty markers)
        {
            var profile = serializedDraft.FindProperty("basicAttackProfile")?.objectReferenceValue as
                MonsterBasicAttackProfile;
            var isProjectile = profile != null && profile.UsesProjectileVisual;
            var timingName = isProjectile ? "발사" : "타격";
            GUILayout.Space(3f);
            GUILayout.Label("Recipe 실행 시점", EditorStyles.boldLabel);
            if (markers.arraySize != 1)
            {
                EditorGUILayout.HelpBox(
                    "기본공격 Motion마다 Recipe를 시작하는 Marker가 정확히 1개 필요합니다.",
                    MessageType.Error);
                if (GUILayout.Button("실행 Marker 1개로 정리", compactButtonStyle, GUILayout.Height(22f)))
                {
                    if (markers.arraySize == 0)
                    {
                        AddMarker(markers);
                    }
                    while (markers.arraySize > 1)
                    {
                        markers.DeleteArrayElementAtIndex(markers.arraySize - 1);
                    }
                    if (markers.arraySize == 1)
                    {
                        markers.GetArrayElementAtIndex(0).FindPropertyRelative("powerRatio").floatValue = 1f;
                    }
                }
                return;
            }

            var marker = markers.GetArrayElementAtIndex(0);
            marker.FindPropertyRelative("powerRatio").floatValue = 1f;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"{timingName} 시점", EditorStyles.miniBoldLabel);
                DrawRelativeProperty(marker, "normalizedTime", "동작 진행률 (0~1)");
            }

            EditorGUILayout.HelpBox(
                isProjectile
                    ? "이 값은 애니메이션의 실제 발사 순간입니다. 피해는 이 지점이 아니라 투사체가 닿을 때 발생하며, 이동 속도·수명은 기본공격 프리셋이 소유합니다."
                    : profile != null && profile.UsesBreathDurationContract
                        ? $"이 값에서 브레스가 시작됩니다. {profile.HitCount}단계 피해와 본체 VFX는 아래 브레스 유지 시간 계약을 함께 사용합니다."
                    : profile != null && profile.HitCount > 1
                        ? $"이 값은 첫 타격 순간입니다. 이후 {profile.HitCount - 1}회는 기본공격 프리셋의 타격 간격으로 이어집니다."
                        : "이 값은 애니메이션의 실제 타격 순간입니다. 공격 방식과 판정은 기본공격 프리셋이 소유합니다.",
                MessageType.None);
        }

        private void DrawBreathDurationOverride(SerializedProperty attack)
        {
            var profile = serializedDraft.FindProperty("basicAttackProfile")?.objectReferenceValue as
                MonsterBasicAttackProfile;
            if (profile == null || !profile.UsesBreathDurationContract)
            {
                return;
            }

            var useOverride = attack.FindPropertyRelative("overrideBreathDuration");
            var duration = attack.FindPropertyRelative("breathDuration");
            if (useOverride == null || duration == null)
            {
                return;
            }

            GUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                useOverride.boolValue = EditorGUILayout.ToggleLeft(
                    $"몬스터 고유 브레스 시간 사용 · 기본 {profile.BreathDuration:0.###}초",
                    useOverride.boolValue);
                var effectiveDuration = useOverride.boolValue
                    ? Mathf.Max(0.01f, duration.floatValue)
                    : profile.BreathDuration;
                if (useOverride.boolValue)
                {
                    var gaugeMax = Mathf.Max(
                        1.5f,
                        Mathf.Ceil(effectiveDuration / BasicAttackVfxTimingGaugeRangeStep) *
                        BasicAttackVfxTimingGaugeRangeStep);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("유지 시간", GUILayout.Width(52f));
                        var gaugeValue = GUILayout.HorizontalSlider(effectiveDuration, 0.01f, gaugeMax);
                        var numericValue = EditorGUILayout.FloatField(gaugeValue, GUILayout.Width(64f));
                        GUILayout.Label("초", GUILayout.Width(14f));
                        if (GUILayout.Button("기본값", compactButtonStyle, GUILayout.Width(52f)))
                        {
                            useOverride.boolValue = false;
                            numericValue = profile.BreathDuration;
                        }
                        duration.floatValue = Mathf.Max(0.01f, numericValue);
                        effectiveDuration = useOverride.boolValue
                            ? duration.floatValue
                            : profile.BreathDuration;
                    }
                }

                var clip = attack.FindPropertyRelative("clip")?.objectReferenceValue as AnimationClip;
                var markers = attack.FindPropertyRelative("markers");
                if (clip != null && markers != null && markers.arraySize > 0)
                {
                    var markerTime = markers.GetArrayElementAtIndex(0)
                        .FindPropertyRelative("normalizedTime").floatValue;
                    var authoredSpeed = Mathf.Max(
                        0.01f,
                        attack.FindPropertyRelative("playbackSpeed").floatValue);
                    var attackSpeed = Mathf.Max(
                        0.01f,
                        serializedDraft.FindProperty("attackSpeed").floatValue);
                    var resolvedSpeed = MonsterAnimationDriver.ResolveAttackPlaybackSpeed(
                        clip,
                        authoredSpeed,
                        1f / attackSpeed);
                    var recipeStart = clip.length * Mathf.Clamp01(markerTime) / resolvedSpeed;
                    var motionEnd = clip.length / resolvedSpeed;
                    var breathEnd = recipeStart + effectiveDuration;
                    var poseHold = Mathf.Max(0f, breathEnd - motionEnd);
                    var hitInterval = effectiveDuration / Mathf.Max(1, profile.HitCount);
                    GUILayout.Label(
                        $"적용 {effectiveDuration:0.###}초 · 피해 간격 {hitInterval:0.###}초 · " +
                        $"공격 시작 기준 종료 {breathEnd:0.###}초" +
                        (poseHold > 0f ? $" · 마지막 자세 유지 {poseHold:0.###}초" : string.Empty),
                        EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        private void DrawOptionalAnimationFeedback(
            SerializedProperty feedback,
            string timingLabel,
            string soundLabel,
            string helpText,
            MonsterMakerPreviewAnchor positionAnchor,
            string socketPath = null)
        {
            if (feedback == null)
            {
                return;
            }

            var sound = feedback.FindPropertyRelative("sound");
            var legacyCue = feedback.FindPropertyRelative("sfx");
            var vfx = feedback.FindPropertyRelative("vfxPrefab");
            var assignedSound = sound?.objectReferenceValue as AudioClip;
            var legacy = legacyCue?.objectReferenceValue as SfxCue;
            var displayedSound = assignedSound != null ? assignedSound : ResolveFirstAudioClip(legacy);
            var hasSound = displayedSound != null || legacy != null;
            var hasVfx = vfx.objectReferenceValue != null;
            var state = hasSound || hasVfx
                ? $"{(hasSound ? "사운드" : string.Empty)}{(hasSound && hasVfx ? " + " : string.Empty)}{(hasVfx ? "VFX" : string.Empty)} 연결됨"
                : "없음 (정상)";
            feedback.isExpanded = EditorGUILayout.Foldout(
                feedback.isExpanded,
                $"{timingLabel} · 선택 사운드/VFX · {state}",
                true);
            if (!feedback.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            var selectedSound = EditorGUILayout.ObjectField(
                new GUIContent(soundLabel + " (선택)"),
                displayedSound,
                typeof(AudioClip),
                false) as AudioClip;
            if (EditorGUI.EndChangeCheck())
            {
                sound.objectReferenceValue = selectedSound;
                legacyCue.objectReferenceValue = null;
                hasSound = selectedSound != null;
            }

            EditorGUILayout.PropertyField(vfx, new GUIContent("VFX (선택)"));
            if (hasVfx)
            {
                DrawRelativeProperty(feedback, "vfxLifetime", "VFX 유지 시간");
                var localPosition = feedback.FindPropertyRelative("localPosition");
                DrawRelativeProperty(feedback, "localPosition", "VFX 위치 보정");
                DrawPreviewPositionButton(
                    localPosition,
                    timingLabel + " VFX",
                    MonsterMakerPreviewPositionValueMode.AnchorOffset,
                    positionAnchor,
                    socketPath);
                DrawRelativeProperty(feedback, "localEulerAngles", "VFX 회전 보정");
                DrawRelativeProperty(feedback, "scale", "VFX 크기");
            }

            EditorGUILayout.HelpBox(
                helpText + " 사운드와 VFX는 둘 다 비어 있어도 정상입니다.",
                MessageType.None);
            EditorGUI.indentLevel--;
        }

        private static AudioClip ResolveFirstAudioClip(SfxCue cue)
        {
            if (cue == null)
            {
                return null;
            }

            var serializedCue = new SerializedObject(cue);
            var clips = serializedCue.FindProperty("clips");
            for (var index = 0; clips != null && index < clips.arraySize; index++)
            {
                if (clips.GetArrayElementAtIndex(index).objectReferenceValue is AudioClip clip)
                {
                    return clip;
                }
            }

            return null;
        }

        private void DrawCombatSection()
        {
            DrawSectionHeader("7. 기본공격 제작");
            var profileProperty = serializedDraft.FindProperty("basicAttackProfile");
            var editorResult = MonsterBasicAttackRecipeEditor.Draw(profileProperty, draft);
            var profile = profileProperty.objectReferenceValue as MonsterBasicAttackProfile ?? editorResult.Profile;
            if (profile == null)
            {
                EditorGUILayout.HelpBox(
                    "먼저 저장된 기본공격을 선택하거나 조립소에서 새 프리셋을 만들어야 합니다.",
                    MessageType.Warning);
            }
            else
            {
                serializedDraft.FindProperty("combatType").enumValueIndex = (int)profile.CombatType;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label($"현재 기본공격 · [{profile.AttackId}] {profile.DisplayName}", EditorStyles.boldLabel);
                    GUILayout.Label(BuildBasicAttackKoreanSummary(profile), EditorStyles.wordWrappedMiniLabel);
                    if (!string.IsNullOrWhiteSpace(profile.DesignMemo))
                    {
                        GUILayout.Label("기획 의도 · " + profile.DesignMemo, EditorStyles.wordWrappedMiniLabel);
                    }
                }
            }

            DrawBasicAttackVfxAssignments(profile);
            GUILayout.Space(5f);
            GUILayout.Label("공격 동작 · 실제 발생 시점", EditorStyles.boldLabel);
            DrawAttackList();

            using (new EditorGUI.DisabledScope(profile == null || !preview.HasCombatTarget))
            {
                if (GUILayout.Button("현재 기본공격 판정범위 표시", GUILayout.Height(28f)))
                {
                    preview.ShowBasicAttackArea();
                }
            }

            EditorGUILayout.HelpBox(
                "조립소는 공격 방식과 연출 공간만 정의합니다. 이 Monster의 VFX와 SFX는 같은 공간 카드에서 배정하고, 공격 동작에서는 Clip과 Marker 시점만 정합니다.",
                MessageType.Info);
        }

        private void DrawBasicAttackVfxAssignments(MonsterBasicAttackProfile profile)
        {
            GUILayout.Space(5f);
            GUILayout.Label("몬스터 고유 기본공격 연출", EditorStyles.boldLabel);
            if (profile == null)
            {
                EditorGUILayout.HelpBox(
                    "기본공격을 선택하면 해당 공격이 요구하는 VFX·SFX 연출 공간이 여기에 자동으로 나타납니다.",
                    MessageType.None);
                return;
            }

            var slots = profile.VfxSlots;
            if (slots.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "이 기본공격에는 아직 연출 공간 계약이 없습니다. 기본공격 조립소에서 공간을 먼저 정의하세요.",
                    MessageType.Warning);
                return;
            }

            var bindings = serializedDraft.FindProperty("basicAttackVfxBindings");
            var rows = new List<(
                MonsterBasicAttackVfxSlot Slot,
                SerializedProperty Binding,
                string MotionId)>();
            var vfxDecided = 0;
            var sfxDecided = 0;
            var total = 0;
            for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                var slot = slots[slotIndex];
                if (slot == null)
                {
                    continue;
                }

                var motionIds = ResolveBasicAttackVfxMotionIds(slot);
                for (var motionIndex = 0; motionIndex < motionIds.Count; motionIndex++)
                {
                    var motionId = motionIds[motionIndex];
                    var binding = FindOrCreateBasicAttackVfxBinding(bindings, profile, slot, motionId);
                    var state = (MonsterBasicAttackVfxAssignmentState)binding
                        .FindPropertyRelative("state")
                        .enumValueIndex;
                    var hasPrefab = binding.FindPropertyRelative("prefab").objectReferenceValue != null;
                    var hasSound = binding.FindPropertyRelative("sound").objectReferenceValue != null;
                    var hasValidAssignment =
                        state == MonsterBasicAttackVfxAssignmentState.Assigned && hasPrefab;
                    if (hasValidAssignment || state == MonsterBasicAttackVfxAssignmentState.Disabled)
                    {
                        vfxDecided++;
                    }
                    var sfxState = (MonsterBasicAttackSfxAssignmentState)binding
                        .FindPropertyRelative("sfxState")
                        .enumValueIndex;
                    if (sfxState == MonsterBasicAttackSfxAssignmentState.Disabled ||
                        sfxState == MonsterBasicAttackSfxAssignmentState.Assigned && hasSound)
                    {
                        sfxDecided++;
                    }
                    total++;
                    rows.Add((slot, binding, motionId));
                }
            }

            var spaceSummary = slots.Count == total
                ? $"연출 공간 {total}개"
                : $"연출 공간 {slots.Count}종 · 배정 단위 {total}개";
            GUILayout.Label(
                $"[{profile.AttackId}] {profile.DisplayName} · {spaceSummary}",
                EditorStyles.miniBoldLabel);
            var progressRect = EditorGUILayout.GetControlRect(false, 18f);
            var progress = total > 0 ? (vfxDecided + sfxDecided) / (total * 2f) : 0f;
            EditorGUI.ProgressBar(
                progressRect,
                progress,
                $"VFX 결정 {vfxDecided}/{total} · SFX 결정 {sfxDecided}/{total}");

            DrawBasicAttackRuntimeSyncStatus();

            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                DrawBasicAttackVfxBindingCard(row.Slot, row.Binding, row.MotionId);
            }

            DrawInactiveBasicAttackBindings();

            GUILayout.Label(
                "VFX 보정과 SFX 원본 클립은 제작 원본에 보존됩니다. 정식 생성·수정 시 SFX Cue는 역할별로 자동 생성됩니다.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawBasicAttackRuntimeSyncStatus()
        {
            if (draft == null || string.IsNullOrWhiteSpace(draft.MonsterId))
            {
                return;
            }

            var paths = MonsterMakerAssetWriter.BuildPaths(draft.MonsterId);
            var combat = AssetDatabase.LoadAssetAtPath<MonsterCombatProfile>(paths[3]);
            var feedback = AssetDatabase.LoadAssetAtPath<MonsterFeedbackProfile>(paths[5]);
            var state = MonsterBasicAttackBindingProjection.EvaluateRuntimeSync(
                draft,
                combat,
                feedback,
                out var message);
            if (state == MonsterBasicAttackRuntimeSyncState.Synchronized)
            {
                GUILayout.Label("● 게임 자산 최신 · 현재 활성 계약만 사용 중", EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.HelpBox(
                $"게임 자산 미반영 · {message}\n아래 정식 생성·수정 버튼으로 현재 Maker 값을 반영할 수 있습니다.",
                MessageType.Warning);
        }

        private void DrawInactiveBasicAttackBindings()
        {
            var inactive = MonsterBasicAttackBindingProjection.BuildInactiveBindings(draft);
            if (inactive.Count == 0)
            {
                return;
            }

            showInactiveBasicAttackBindings = EditorGUILayout.Foldout(
                showInactiveBasicAttackBindings,
                $"고급 · 이전 프리셋·모션 연결 {inactive.Count}개 보관 중",
                true);
            if (!showInactiveBasicAttackBindings)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.HelpBox(
                    "현재 기본공격에는 사용되지 않지만, 이전 프리셋이나 모션으로 돌아갈 때 복원할 편집값입니다. 미리보기와 게임 자산 출력에서는 제외됩니다.",
                    MessageType.None);
                foreach (var binding in inactive)
                {
                    if (binding == null)
                    {
                        continue;
                    }
                    var motion = string.IsNullOrWhiteSpace(binding.MotionId) ? "공통" : binding.MotionId;
                    GUILayout.Label(
                        $"[{binding.AttackId}] {binding.SlotId} · {motion}",
                        EditorStyles.miniLabel);
                }
            }
        }

        private List<string> ResolveBasicAttackVfxMotionIds(MonsterBasicAttackVfxSlot slot)
        {
            var result = new List<string>();
            if (slot.AssignmentScope == MonsterBasicAttackVfxAssignmentScope.MonsterShared)
            {
                result.Add(string.Empty);
                return result;
            }

            var attacks = serializedDraft.FindProperty("attacks");
            for (var index = 0; attacks != null && index < attacks.arraySize; index++)
            {
                var motionId = attacks.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("motionId").stringValue?.Trim();
                if (!string.IsNullOrWhiteSpace(motionId) && !result.Contains(motionId))
                {
                    result.Add(motionId);
                }
            }
            if (result.Count == 0)
            {
                result.Add("attack01");
            }
            return result;
        }

        private static SerializedProperty FindOrCreateBasicAttackVfxBinding(
            SerializedProperty bindings,
            MonsterBasicAttackProfile profile,
            MonsterBasicAttackVfxSlot slot,
            string motionId)
        {
            for (var index = 0; index < bindings.arraySize; index++)
            {
                var candidate = bindings.GetArrayElementAtIndex(index);
                if (string.Equals(
                        candidate.FindPropertyRelative("attackId").stringValue,
                        profile.AttackId,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        candidate.FindPropertyRelative("slotId").stringValue,
                        slot.SlotId,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        candidate.FindPropertyRelative("motionId").stringValue,
                        motionId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            var newIndex = bindings.arraySize;
            bindings.InsertArrayElementAtIndex(newIndex);
            var created = bindings.GetArrayElementAtIndex(newIndex);
            created.FindPropertyRelative("attackId").stringValue = profile.AttackId;
            created.FindPropertyRelative("slotId").stringValue = slot.SlotId;
            created.FindPropertyRelative("motionId").stringValue = motionId;
            created.FindPropertyRelative("state").enumValueIndex =
                (int)MonsterBasicAttackVfxAssignmentState.Undecided;
            created.FindPropertyRelative("prefab").objectReferenceValue = null;
            created.FindPropertyRelative("sfxState").enumValueIndex =
                (int)MonsterBasicAttackSfxAssignmentState.Undecided;
            created.FindPropertyRelative("sound").objectReferenceValue = null;
            created.FindPropertyRelative("soundVolume").floatValue = 1f;
            created.FindPropertyRelative("sfx").objectReferenceValue = null;
            created.FindPropertyRelative("lifetime").floatValue = slot.DefaultLifetime;
            created.FindPropertyRelative("playbackOffset").floatValue = 0f;
            created.FindPropertyRelative("playbackSpeed").floatValue = 1f;
            created.FindPropertyRelative("eventTimingOffset").floatValue = 0f;
            created.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
            created.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
            created.FindPropertyRelative("scale").floatValue = 1f;
            return created;
        }

        private static int ToVfxAssignmentPopupIndex(MonsterBasicAttackVfxAssignmentState state)
        {
            return state switch
            {
                MonsterBasicAttackVfxAssignmentState.Disabled => 1,
                MonsterBasicAttackVfxAssignmentState.Assigned => 2,
                _ => 0
            };
        }

        private static MonsterBasicAttackVfxAssignmentState FromVfxAssignmentPopupIndex(int index)
        {
            return index switch
            {
                1 => MonsterBasicAttackVfxAssignmentState.Disabled,
                2 => MonsterBasicAttackVfxAssignmentState.Assigned,
                _ => MonsterBasicAttackVfxAssignmentState.Undecided
            };
        }

        private static int ToSfxAssignmentPopupIndex(MonsterBasicAttackSfxAssignmentState state)
        {
            return state switch
            {
                MonsterBasicAttackSfxAssignmentState.Disabled => 1,
                MonsterBasicAttackSfxAssignmentState.Assigned => 2,
                _ => 0
            };
        }

        private static MonsterBasicAttackSfxAssignmentState FromSfxAssignmentPopupIndex(int index)
        {
            return index switch
            {
                1 => MonsterBasicAttackSfxAssignmentState.Disabled,
                2 => MonsterBasicAttackSfxAssignmentState.Assigned,
                _ => MonsterBasicAttackSfxAssignmentState.Undecided
            };
        }

        private void DrawBasicAttackVfxBindingCard(
            MonsterBasicAttackVfxSlot slot,
            SerializedProperty binding,
            string motionId)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var sfxState = binding.FindPropertyRelative("sfxState");
                var state = binding.FindPropertyRelative("state");
                var currentSfxState = (MonsterBasicAttackSfxAssignmentState)sfxState.enumValueIndex;
                var currentState = (MonsterBasicAttackVfxAssignmentState)state.enumValueIndex;
                var scopeLabel = string.IsNullOrWhiteSpace(motionId)
                    ? MonsterBasicAttackVfxEditorLabels.Get(slot.AssignmentScope)
                    : $"{MonsterBasicAttackVfxEditorLabels.Get(slot.AssignmentScope)} · {motionId}";
                var roleLabel = BasicAttackWorkshopVfxRoles.GetLabel(
                    BasicAttackWorkshopVfxRoles.Resolve(slot));
                var fullyDisabled = IsBasicAttackPresentationFullyDisabled(
                    currentSfxState,
                    currentState);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (fullyDisabled)
                    {
                        binding.isExpanded = EditorGUILayout.Foldout(
                            binding.isExpanded,
                            new GUIContent(roleLabel, slot.Description),
                            true);
                    }
                    else
                    {
                        GUILayout.Label(
                            new GUIContent(roleLabel, slot.Description),
                            EditorStyles.miniBoldLabel);
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(
                        fullyDisabled
                            ? $"SFX/VFX 모두 사용 안 함 · {scopeLabel}"
                            : $"선택 · {scopeLabel}",
                        EditorStyles.miniLabel);
                }
                if (fullyDisabled && !binding.isExpanded)
                {
                    return;
                }

                GUILayout.Label("SFX", EditorStyles.miniBoldLabel);
                var selectedSfxIndex = EditorGUILayout.Popup(
                    "사용 상태",
                    ToSfxAssignmentPopupIndex(currentSfxState),
                    new[] { "미결정", "사용 안 함", "SFX 사용" });
                var resolvedSfxState = FromSfxAssignmentPopupIndex(selectedSfxIndex);
                if (resolvedSfxState != currentSfxState)
                {
                    sfxState.enumValueIndex = (int)resolvedSfxState;
                    if (resolvedSfxState != MonsterBasicAttackSfxAssignmentState.Assigned)
                    {
                        SfxEditorAudioPreview.StopAll();
                    }
                    if (IsBasicAttackPresentationFullyDisabled(
                            resolvedSfxState,
                            (MonsterBasicAttackVfxAssignmentState)state.enumValueIndex))
                    {
                        binding.isExpanded = false;
                    }
                }
                var sound = binding.FindPropertyRelative("sound");
                if (resolvedSfxState == MonsterBasicAttackSfxAssignmentState.Undecided)
                {
                    DrawBasicAttackVfxNotice(
                        "SFX 사용 여부를 결정하세요.",
                        MessageType.None);
                }
                else if (resolvedSfxState == MonsterBasicAttackSfxAssignmentState.Disabled)
                {
                    DrawBasicAttackVfxNotice(
                        "이 공간에서는 SFX를 사용하지 않습니다.",
                        MessageType.None);
                }
                else
                {
                    EditorGUILayout.PropertyField(sound, new GUIContent("SFX 원본 클립"));
                    var soundVolume = binding.FindPropertyRelative("soundVolume");
                    soundVolume.floatValue = EditorGUILayout.Slider(
                        "볼륨",
                        soundVolume.floatValue,
                        0f,
                        1f);
                    var assignedSound = sound.objectReferenceValue as AudioClip;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(assignedSound == null))
                        {
                            if (GUILayout.Button("SFX 미리듣기", compactButtonStyle))
                            {
                                SfxEditorAudioPreview.Play(
                                    assignedSound,
                                    0,
                                    false,
                                    soundVolume.floatValue);
                            }
                        }
                        if (GUILayout.Button("SFX 정지", compactButtonStyle))
                        {
                            SfxEditorAudioPreview.StopAll();
                        }
                    }
                    if (assignedSound == null)
                    {
                        DrawBasicAttackVfxNotice(
                            "SFX 사용 상태이지만 AudioClip이 비어 있습니다.",
                            MessageType.Error);
                    }
                    else
                    {
                        GUILayout.Label(
                            "정식 생성·수정 시 이 클립과 볼륨으로 전용 SFX Cue가 자동 생성됩니다.",
                            EditorStyles.wordWrappedMiniLabel);
                    }
                }

                GUILayout.Space(3f);
                GUILayout.Label("VFX", EditorStyles.miniBoldLabel);
                var selectedStateIndex = EditorGUILayout.Popup(
                    "사용 상태",
                    ToVfxAssignmentPopupIndex(currentState),
                    new[] { "미결정", "사용 안 함", "VFX 사용" });
                var resolvedState = FromVfxAssignmentPopupIndex(selectedStateIndex);
                state.enumValueIndex = (int)resolvedState;
                if (resolvedState != currentState &&
                    IsBasicAttackPresentationFullyDisabled(resolvedSfxState, resolvedState))
                {
                    binding.isExpanded = false;
                }
                if (resolvedState == MonsterBasicAttackVfxAssignmentState.Undecided)
                {
                    DrawBasicAttackVfxNotice(
                        "VFX 사용 여부를 결정하세요.",
                        MessageType.None);
                    return;
                }
                if (resolvedState == MonsterBasicAttackVfxAssignmentState.Disabled)
                {
                    DrawBasicAttackVfxNotice(
                        "이 공간에서는 VFX를 사용하지 않습니다.",
                        MessageType.None);
                    return;
                }

                var prefab = binding.FindPropertyRelative("prefab");
                EditorGUILayout.PropertyField(prefab, new GUIContent(
                    slot.IsDeliveryVisual ? "이동체 VFX Prefab" : "VFX Prefab"));
                var assignedPrefab = prefab.objectReferenceValue as GameObject;
                if (assignedPrefab == null)
                {
                    DrawBasicAttackVfxNotice(
                        "배정 상태이지만 VFX Prefab이 비어 있습니다.",
                        MessageType.Error);
                    return;
                }

                if (slot.EndPolicy is MonsterBasicAttackVfxEndPolicy.Timed or
                    MonsterBasicAttackVfxEndPolicy.ParticleDuration)
                {
                    DrawRelativeProperty(binding, "lifetime", "VFX 유지 시간");
                }
                var eventTimingOffset = binding.FindPropertyRelative("eventTimingOffset");
                if (slot.AllowsMonsterTimingOffset && eventTimingOffset != null)
                {
                    DrawBasicAttackVfxTimingToggle(slot, eventTimingOffset);
                }
                var localPosition = binding.FindPropertyRelative("localPosition");
                var localEulerAngles = binding.FindPropertyRelative("localEulerAngles");
                var scale = binding.FindPropertyRelative("scale");
                var playbackOffset = binding.FindPropertyRelative("playbackOffset");
                var playbackSpeed = binding.FindPropertyRelative("playbackSpeed");
                GUILayout.Label(
                    $"보정 · 위치 {FormatBasicAttackVfxVector(localPosition.vector3Value)} · " +
                    $"회전 {FormatBasicAttackVfxVector(localEulerAngles.vector3Value)} · " +
                    $"크기 {scale.floatValue:0.##} · 내부 시작 {playbackOffset.floatValue:0.##}초 · " +
                    $"속도 {Mathf.Max(0.01f, playbackSpeed?.floatValue ?? 1f):0.##}배" +
                    (slot.AllowsMonsterTimingOffset && eventTimingOffset != null
                        ? $" · 발생 {eventTimingOffset.floatValue:+0.##;-0.##;0}초"
                        : string.Empty),
                    EditorStyles.wordWrappedMiniLabel);

                var isPlaying = preview?.IsPlaying == true;
                var canPreviewAdjust = MonsterPositionAdjustWindow.CanOpen(draft) &&
                                       !isPlaying;
                var isWrapper = MonsterBasicAttackVfxPrefabUtility.IsMonsterWrapper(
                    assignedPrefab,
                    draft?.MonsterId);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!canPreviewAdjust))
                    {
                        var previewButtonLabel = isPlaying
                            ? "재생 중 · 먼저 일시정지"
                            : "VFX 보정·재생";
                        if (GUILayout.Button(previewButtonLabel, compactButtonStyle))
                        {
                            OpenBasicAttackVfxAdjustWindow(
                                slot,
                                binding,
                                assignedPrefab);
                        }
                    }

                    if (GUILayout.Button(
                            isWrapper ? "전용 Prefab 편집" : "전용 래퍼 만들기",
                            compactButtonStyle))
                    {
                        if (isWrapper)
                        {
                            AssetDatabase.OpenAsset(assignedPrefab);
                            EditorGUIUtility.PingObject(assignedPrefab);
                        }
                        else
                        {
                            CreateBasicAttackVfxWrapper(slot, binding, motionId);
                        }
                    }
                }
                if (slot.IsDeliveryVisual)
                {
                    GUILayout.Label(
                        "이 Prefab은 이동체 외형이며 이동·충돌·수명은 기본공격이 처리합니다.",
                        EditorStyles.miniLabel);
                }
            }
        }

        private static bool IsBasicAttackPresentationFullyDisabled(
            MonsterBasicAttackSfxAssignmentState sfxState,
            MonsterBasicAttackVfxAssignmentState vfxState)
        {
            return sfxState == MonsterBasicAttackSfxAssignmentState.Disabled &&
                   vfxState == MonsterBasicAttackVfxAssignmentState.Disabled;
        }

        private static void DrawBasicAttackVfxNotice(string message, MessageType type)
        {
            var prefix = type switch
            {
                MessageType.Error => "오류 · ",
                MessageType.Warning => "확인 필요 · ",
                _ => string.Empty
            };
            GUILayout.Label(prefix + message, EditorStyles.wordWrappedMiniLabel);
        }

        private static string FormatBasicAttackVfxVector(Vector3 value)
        {
            return $"({value.x:0.##}, {value.y:0.##}, {value.z:0.##})";
        }

        private static string BuildBasicAttackVfxTimingSummary(
            MonsterBasicAttackVfxEvent eventType,
            float offset)
        {
            var eventLabel = MonsterBasicAttackVfxEditorLabels.Get(eventType);
            if (Mathf.Abs(offset) < 0.0001f)
            {
                return $"계약 시점 그대로 · {eventLabel}";
            }
            return offset < 0f
                ? $"{eventLabel}보다 {Mathf.Abs(offset):0.##}초 먼저 재생"
                : $"{eventLabel}보다 {offset:0.##}초 늦게 재생";
        }

        private static void DrawBasicAttackVfxTimingToggle(
            MonsterBasicAttackVfxSlot slot,
            SerializedProperty eventTimingOffset)
        {
            var value = slot.ClampTimingOffset(eventTimingOffset.floatValue);
            eventTimingOffset.floatValue = value;
            var toggleSummary = Mathf.Abs(value) < 0.0001f
                ? "정시"
                : value < 0f
                    ? $"{Mathf.Abs(value):0.##}초 먼저"
                    : $"{value:0.##}초 늦게";
            eventTimingOffset.isExpanded = EditorGUILayout.Foldout(
                eventTimingOffset.isExpanded,
                new GUIContent(
                    $"VFX 시점 보정 · {toggleSummary}",
                    "필요할 때만 펼쳐 게이지와 정확한 숫자값을 조절합니다."),
                true);
            if (!eventTimingOffset.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            DrawBasicAttackVfxTimingGauge(slot, eventTimingOffset);
            if (!slot.AllowsTimingLead)
            {
                GUILayout.Label(
                    "실제 발생 전 위치를 알 수 없는 계약은 0초 이후 지연만 적용됩니다.",
                    EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUI.indentLevel--;
        }

        private static void DrawBasicAttackVfxTimingGauge(
            MonsterBasicAttackVfxSlot slot,
            SerializedProperty eventTimingOffset)
        {
            var value = slot.ClampTimingOffset(eventTimingOffset.floatValue);
            var gaugeRange = ResolveBasicAttackVfxTimingGaugeRange(value);
            var gaugeMin = slot.AllowsTimingLead ? -gaugeRange : 0f;
            var gaugeMax = gaugeRange;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(
                    slot.AllowsTimingLead ? "먼저" : "정시",
                    EditorStyles.miniLabel,
                    GUILayout.Width(30f));
                var gaugeRect = GUILayoutUtility.GetRect(
                    100f,
                    18f,
                    GUILayout.ExpandWidth(true));
                EditorGUI.BeginChangeCheck();
                var gaugeValue = GUI.HorizontalSlider(
                    gaugeRect,
                    Mathf.Clamp(value, gaugeMin, gaugeMax),
                    gaugeMin,
                    gaugeMax);
                if (EditorGUI.EndChangeCheck())
                {
                    value = Mathf.Round(gaugeValue * 100f) * 0.01f;
                }
                if (slot.AllowsTimingLead)
                {
                    var zeroX = Mathf.Lerp(gaugeRect.xMin, gaugeRect.xMax, 0.5f);
                    EditorGUI.DrawRect(
                        new Rect(zeroX, gaugeRect.yMax - 5f, 1f, 5f),
                        new Color(0.75f, 0.75f, 0.75f, 0.8f));
                }

                GUILayout.Label(
                    "늦게",
                    EditorStyles.miniLabel,
                    GUILayout.Width(30f));
                EditorGUI.BeginChangeCheck();
                var exactValue = EditorGUILayout.FloatField(value, GUILayout.Width(62f));
                if (EditorGUI.EndChangeCheck())
                {
                    value = exactValue;
                }
                GUILayout.Label("초", EditorStyles.miniLabel, GUILayout.Width(14f));
                if (GUILayout.Button(
                        new GUIContent("0", "계약 시점 그대로 되돌립니다."),
                        EditorStyles.miniButton,
                        GUILayout.Width(26f)))
                {
                    value = 0f;
                    GUI.FocusControl(null);
                }
            }

            eventTimingOffset.floatValue = slot.ClampTimingOffset(value);
            GUILayout.Label(
                BuildBasicAttackVfxTimingSummary(
                    slot.EventType,
                    eventTimingOffset.floatValue),
                EditorStyles.wordWrappedMiniLabel);
            GUILayout.Label(
                "게이지는 현재 값에 맞춰 자동 확장 · 숫자 직접 입력은 제한 없음",
                EditorStyles.centeredGreyMiniLabel);
        }

        private static float ResolveBasicAttackVfxTimingGaugeRange(float value)
        {
            var magnitude = float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Abs(value);
            return Mathf.Max(
                BasicAttackVfxTimingGaugeMinRange,
                Mathf.Ceil(magnitude / BasicAttackVfxTimingGaugeRangeStep) *
                BasicAttackVfxTimingGaugeRangeStep);
        }

        private void OpenBasicAttackVfxAdjustWindow(
            MonsterBasicAttackVfxSlot slot,
            SerializedProperty binding,
            GameObject assignedPrefab)
        {
            var position = binding?.FindPropertyRelative("localPosition");
            var euler = binding?.FindPropertyRelative("localEulerAngles");
            var scale = binding?.FindPropertyRelative("scale");
            var lifetime = binding?.FindPropertyRelative("lifetime");
            var playbackOffset = binding?.FindPropertyRelative("playbackOffset");
            var playbackSpeed = binding?.FindPropertyRelative("playbackSpeed");
            if (position == null || euler == null || scale == null || lifetime == null ||
                playbackOffset == null || playbackSpeed == null || assignedPrefab == null)
            {
                return;
            }

            serializedDraft.ApplyModifiedProperties();
            var targetDraft = draft;
            var positionPath = position.propertyPath;
            var eulerPath = euler.propertyPath;
            var scalePath = scale.propertyPath;
            var lifetimePath = lifetime.propertyPath;
            var playbackOffsetPath = playbackOffset.propertyPath;
            var playbackSpeedPath = playbackSpeed.propertyPath;
            var positionBinding = new MonsterMakerPreviewPositionBinding(
                positionPath,
                slot.DisplayName,
                MonsterMakerPreviewPositionValueMode.AnchorOffset,
                ResolveBasicAttackVfxPreviewAnchor(slot.Anchor));
            MonsterPositionAdjustWindow.OpenVfx(
                this,
                targetDraft,
                positionBinding,
                assignedPrefab,
                position.vector3Value,
                euler.vector3Value,
                scale.floatValue,
                lifetime.floatValue,
                playbackOffset.floatValue,
                playbackSpeed.floatValue,
                (changedPosition, changedEuler, changedScale, changedLifetime, changedPlaybackOffset, changedPlaybackSpeed) =>
                    ApplyPopupVfxValues(
                        targetDraft,
                        positionPath,
                        eulerPath,
                        scalePath,
                        lifetimePath,
                        playbackOffsetPath,
                        playbackSpeedPath,
                        slot.DisplayName,
                        changedPosition,
                        changedEuler,
                        changedScale,
                        changedLifetime,
                        changedPlaybackOffset,
                        changedPlaybackSpeed));
        }

        private bool ApplyPopupVfxValues(
            MonsterMakerDraft targetDraft,
            string positionPath,
            string eulerPath,
            string scalePath,
            string lifetimePath,
            string playbackOffsetPath,
            string playbackSpeedPath,
            string label,
            Vector3 position,
            Vector3 euler,
            float scale,
            float lifetime,
            float playbackOffset,
            float playbackSpeed)
        {
            if (targetDraft == null || draft != targetDraft || serializedDraft == null ||
                preview?.IsPlaying == true)
            {
                return false;
            }

            serializedDraft.UpdateIfRequiredOrScript();
            var positionProperty = serializedDraft.FindProperty(positionPath);
            var eulerProperty = serializedDraft.FindProperty(eulerPath);
            var scaleProperty = serializedDraft.FindProperty(scalePath);
            var lifetimeProperty = serializedDraft.FindProperty(lifetimePath);
            var playbackOffsetProperty = serializedDraft.FindProperty(playbackOffsetPath);
            var playbackSpeedProperty = serializedDraft.FindProperty(playbackSpeedPath);
            if (positionProperty == null || eulerProperty == null || scaleProperty == null ||
                lifetimeProperty == null ||
                playbackOffsetProperty == null || playbackSpeedProperty == null)
            {
                return false;
            }

            Undo.RecordObject(targetDraft, $"{label} VFX 보정");
            positionProperty.vector3Value = position;
            eulerProperty.vector3Value = euler;
            scaleProperty.floatValue = Mathf.Max(0.01f, scale);
            lifetimeProperty.floatValue = Mathf.Max(0.01f, lifetime);
            playbackOffsetProperty.floatValue = Mathf.Max(0f, playbackOffset);
            playbackSpeedProperty.floatValue = float.IsNaN(playbackSpeed) || float.IsInfinity(playbackSpeed)
                ? 1f
                : Mathf.Max(0.01f, playbackSpeed);
            serializedDraft.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(targetDraft);
            validation = null;
            lastWriteResult = null;
            preview.ApplyDraftPositionOverrides();
            Repaint();
            return true;
        }

        private void CreateBasicAttackVfxWrapper(
            MonsterBasicAttackVfxSlot slot,
            SerializedProperty binding,
            string motionId)
        {
            var source = binding?.FindPropertyRelative("prefab")?.objectReferenceValue as GameObject;
            if (source == null || draft == null)
            {
                return;
            }

            var bindingPath = binding.propertyPath;
            var attackId = binding.FindPropertyRelative("attackId").stringValue;
            if (!MonsterBasicAttackVfxPrefabUtility.TryCreateWrapper(
                    draft.MonsterId,
                    attackId,
                    slot.SlotId,
                    motionId,
                    source,
                    binding.FindPropertyRelative("localPosition").vector3Value,
                    binding.FindPropertyRelative("localEulerAngles").vector3Value,
                    binding.FindPropertyRelative("scale").floatValue,
                    out var wrapper,
                    out var error))
            {
                EditorUtility.DisplayDialog(
                    "전용 VFX 래퍼 생성 실패",
                    string.IsNullOrWhiteSpace(error) ? "알 수 없는 오류입니다." : error,
                    "확인");
                return;
            }

            serializedDraft.UpdateIfRequiredOrScript();
            var refreshedBinding = serializedDraft.FindProperty(bindingPath);
            if (refreshedBinding == null)
            {
                return;
            }
            Undo.RecordObject(draft, $"{slot.DisplayName} 전용 VFX 래퍼 생성");
            refreshedBinding.FindPropertyRelative("prefab").objectReferenceValue = wrapper;
            refreshedBinding.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
            refreshedBinding.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
            refreshedBinding.FindPropertyRelative("scale").floatValue = 1f;
            serializedDraft.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(draft);
            validation = null;
            lastWriteResult = null;
            EditorGUIUtility.PingObject(wrapper);
            ShowNotification(new GUIContent("전용 VFX 래퍼 생성 · 보정값 0/0/1 정리"));
            GUIUtility.ExitGUI();
        }

        private static MonsterMakerPreviewAnchor ResolveBasicAttackVfxPreviewAnchor(
            MonsterBasicAttackVfxAnchor anchor)
        {
            return anchor switch
            {
                MonsterBasicAttackVfxAnchor.AttackOrigin or
                MonsterBasicAttackVfxAnchor.MarkerSocket or
                MonsterBasicAttackVfxAnchor.ProjectileRoot or
                MonsterBasicAttackVfxAnchor.TrajectoryOrigin =>
                    MonsterMakerPreviewAnchor.AttackOrigin,
                MonsterBasicAttackVfxAnchor.TargetRoot or
                MonsterBasicAttackVfxAnchor.HitPoint or
                MonsterBasicAttackVfxAnchor.AreaCenter =>
                    MonsterMakerPreviewAnchor.HitCenter,
                _ => MonsterMakerPreviewAnchor.Root
            };
        }

        private static string BuildBasicAttackKoreanSummary(MonsterBasicAttackProfile profile)
        {
            var family = profile.AttackId.StartsWith("BA_S_", StringComparison.OrdinalIgnoreCase)
                ? "특수"
                : profile.CombatType == MonsterCombatType.Melee ? "근거리" : "원거리";
            var delivery = profile.PresentationKind switch
            {
                MonsterBasicAttackPresentationKind.Returning => "왕복 투사체",
                MonsterBasicAttackPresentationKind.Breath => "브레스",
                MonsterBasicAttackPresentationKind.Beam => "빔",
                MonsterBasicAttackPresentationKind.Wave => "진행 파동",
                MonsterBasicAttackPresentationKind.Instant => "즉발",
                _ when profile.UsesProjectileVisual => "투사체",
                _ => "직접 타격"
            };
            var shape = profile.Shape switch
            {
                MonsterBasicAttackShape.Fan => "부채꼴",
                MonsterBasicAttackShape.Line => "직선",
                MonsterBasicAttackShape.Circle => "원형",
                _ => "단일"
            };
            var hit = profile.HitCount > 1 ? $"{profile.HitCount}타" : "단타";
            var movement = profile.MovementModule == MonsterBasicAttackMovementModule.Dash ? " · 실제 돌진" : string.Empty;
            return $"{family} · {delivery} · {shape} · {hit}{movement} · 최대 {profile.MaxTargets}명";
        }

        private void DrawAscensionSection()
        {
            DrawSectionHeader("10. 돌파 옵션");
            var configured = serializedDraft.FindProperty("ascensionConfigured");
            EditorGUILayout.PropertyField(configured, new GUIContent("돌파 옵션 사용"));
            if (!configured.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "미설정 상태입니다. 전투 편입은 가능하며 돌파 능력치와 스킬은 적용되지 않습니다.",
                    MessageType.None);
                return;
            }

            DrawStatModifier(serializedDraft.FindProperty("ascension1"), "1돌파 능력치");
            if (!serializedDraft.FindProperty("skillLoadoutConfigured").boolValue)
            {
                EditorGUILayout.HelpBox(
                    "기존 운영 Monster의 2·4돌파 Ability 값은 숨긴 채 보존합니다. 범용 스킬 구성을 켜면 기존 스킬 강화로 전환됩니다.",
                    MessageType.None);
                DrawStatModifier(serializedDraft.FindProperty("ascension3"), "3돌파 능력치");
                DrawStatModifier(serializedDraft.FindProperty("ascension5"), "5돌파 능력치");
                return;
            }

            DrawSkillAugment(serializedDraft.FindProperty("ascension2"), "2돌파 · 패시브 강화", false);
            DrawStatModifier(serializedDraft.FindProperty("ascension3"), "3돌파 능력치");
            var rarity = (MonsterRarity)serializedDraft.FindProperty("rarity").enumValueIndex;
            DrawSkillAugment(
                serializedDraft.FindProperty("ascension4"),
                rarity >= MonsterRarity.Legendary ? "4돌파 · 액티브 강화" : "4돌파 · 패시브 추가 강화",
                rarity >= MonsterRarity.Legendary);
            DrawStatModifier(serializedDraft.FindProperty("ascension5"), "5돌파 능력치");
        }

        private void DrawCastleRaidAiSection()
        {
            DrawSectionHeader("9. 군단의 역습 AI");
            var pattern = (HexCastleAssaultPattern)DrawEnumProperty(
                "castleRaidAiPattern",
                "행동 패턴",
                CastleRaidAiPatternLabels);
            EditorGUILayout.HelpBox(
                "군단의 역습에서만 사용하는 목표 선택 규칙입니다. 메인 전투 AI에는 영향을 주지 않습니다.",
                MessageType.None);
            if (pattern != HexCastleAssaultPattern.TacticalSupport)
            {
                return;
            }

            DrawEnumProperty("castleRaidSupportFocus", "지원 성향", CastleRaidSupportFocusLabels);
            DrawProperty("castleRaidSupportRange", "지원 범위");
            DrawProperty("castleRaidSupportCooldown", "지원 재사용 시간");
            DrawProperty("castleRaidSupportDuration", "강화 지속 시간");
            DrawProperty("castleRaidHealRatio", "최대 체력 회복 비율");
            DrawProperty("castleRaidAttackBuffRate", "공격력 증가 비율");
            DrawProperty("castleRaidDefenseDamageMultiplier", "받는 피해 배율");
        }

        private void DrawCombatIdentitySection()
        {
            DrawSectionHeader("4. 타격감 · 피격 반응");
            DrawEnumProperty("impactStrength", "타격 강도", ImpactStrengthLabels);
            DrawEnumProperty("reactionWeight", "피격 체급", ReactionWeightLabels);
            EditorGUILayout.HelpBox(
                "타격 강도는 공격 방식이 아니라 맞은 적의 넉백·에어본·경직 세기를 정합니다. 피격 체급은 이 몬스터가 맞았을 때 얼마나 튕기는지를 정합니다.",
                MessageType.None);
        }

        private void DrawMainBattleAiSection()
        {
            DrawSectionHeader("5. 메인 전투 역할 AI");
            var role = (MainBattleMonsterRole)DrawEnumProperty(
                "mainBattleRole",
                "전투 역할",
                MainBattleRoleLabels);
            DrawEnumProperty("mainBattleTargetPriority", "대상 우선순위", TargetPriorityLabels);
            DrawProperty("mainBattlePreferredRangeRatio", "희망 거리 비율");
            DrawProperty("mainBattleRetreatRangeRatio", "후퇴 시작 비율");
            DrawProperty("mainBattleRetargetInterval", "대상 재탐색 간격");
            EditorGUILayout.HelpBox(ResolveMainBattleRoleHelp(role), MessageType.None);
        }

        private static string ResolveMainBattleRoleHelp(MainBattleMonsterRole role)
        {
            return role switch
            {
                MainBattleMonsterRole.Guardian => "수호: 전열을 지키며 대상이 한 곳에 몰리지 않게 분산합니다.",
                MainBattleMonsterRole.Finisher => "마무리: 체력이 낮은 적을 우선해 전투 수를 빠르게 줄이는 역할입니다.",
                MainBattleMonsterRole.Marksman => "사수: 원거리 희망 거리를 유지하며 적이 너무 가까우면 후퇴합니다.",
                MainBattleMonsterRole.BacklineHunter => "후열 추적: 원거리 적을 우선 선택하고 안전거리를 유지합니다.",
                _ => "선봉: 가까운 적에게 빠르게 접근해 전투선을 먼저 형성합니다."
            };
        }

        private void DrawStatModifier(SerializedProperty modifier, string label)
        {
            modifier.isExpanded = EditorGUILayout.Foldout(modifier.isExpanded, label, true);
            if (!modifier.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            DrawRelativeProperty(modifier, "healthRate", "체력 증가율");
            DrawRelativeProperty(modifier, "attackRate", "공격력 증가율");
            DrawRelativeProperty(modifier, "defenseRate", "방어력 증가율");
            DrawRelativeProperty(modifier, "attackSpeedRate", "공격 속도 증가율");
            DrawRelativeProperty(modifier, "moveSpeedRate", "이동 속도 증가율");
            DrawRelativeProperty(modifier, "attackRangeRate", "공격 사거리 증가율");
            EditorGUI.indentLevel--;
        }

        private void DrawSkillAugment(SerializedProperty ability, string label, bool targetsActive)
        {
            ability.isExpanded = EditorGUILayout.Foldout(ability.isExpanded, label, true);
            if (!ability.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            DrawRelativeProperty(ability, "abilityId", "스킬 ID");
            DrawRelativeProperty(ability, "displayName", "강화 이름");
            GUILayout.Label(
                targetsActive ? "대상: 현재 선택한 액티브" : "대상: 현재 선택한 패시브",
                EditorStyles.wordWrappedMiniLabel);
            var operation = (MonsterSkillAugmentOperation)DrawRelativeEnumProperty(
                ability,
                "augmentOperation",
                "강화 방식",
                SkillAugmentOperationLabels);
            switch (operation)
            {
                case MonsterSkillAugmentOperation.MagnitudeMultiplier:
                    DrawRelativeProperty(ability, "augmentScalarValue", "효과량 증가율");
                    break;
                case MonsterSkillAugmentOperation.DurationBonusSeconds:
                    DrawRelativeProperty(ability, "augmentScalarValue", "추가 지속 시간(초)");
                    break;
                case MonsterSkillAugmentOperation.CooldownReductionRate:
                    DrawRelativeProperty(ability, "augmentScalarValue", "쿨다운 감소율");
                    break;
                default:
                    DrawRelativeProperty(ability, "augmentIntegerValue", "증감 횟수");
                    break;
            }
            EditorGUI.indentLevel--;
        }

        private void DrawProperty(string propertyName, string label)
        {
            var property = serializedDraft.FindProperty(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
            }
        }

        private void DrawPositionPropertyWithPreviewHandle(
            string propertyName,
            string label,
            string previewLabel,
            MonsterMakerPreviewPositionValueMode valueMode,
            MonsterMakerPreviewAnchor anchor)
        {
            var property = serializedDraft.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent(label), true);
            DrawPreviewPositionButton(property, previewLabel, valueMode, anchor);
        }

        private void DrawPreviewPositionButton(
            SerializedProperty property,
            string label,
            MonsterMakerPreviewPositionValueMode valueMode,
            MonsterMakerPreviewAnchor anchor,
            string socketPath = null)
        {
            if (property == null || property.propertyType != SerializedPropertyType.Vector3)
            {
                return;
            }

            var isPlaying = preview?.IsPlaying == true;
            var canOpen = MonsterPositionAdjustWindow.CanOpen(draft) && !isPlaying;
            using (new EditorGUI.DisabledScope(!canOpen))
            {
                var buttonLabel = isPlaying
                    ? "재생 중 · 먼저 일시정지"
                    : $"직접 조절 · {label}";
                if (GUILayout.Button(buttonLabel, compactButtonStyle, GUILayout.Height(22f)))
                {
                    serializedDraft.ApplyModifiedProperties();
                    var targetDraft = draft;
                    var binding = new MonsterMakerPreviewPositionBinding(
                        property.propertyPath,
                        label,
                        valueMode,
                        anchor,
                        socketPath);
                    MonsterPositionAdjustWindow.Open(
                        this,
                        targetDraft,
                        binding,
                        property.vector3Value,
                        value => ApplyPopupPositionValue(targetDraft, binding, value));
                }
            }
        }

        private bool ApplyPopupPositionValue(
            MonsterMakerDraft targetDraft,
            MonsterMakerPreviewPositionBinding binding,
            Vector3 value)
        {
            if (targetDraft == null || draft != targetDraft || serializedDraft == null ||
                preview?.IsPlaying == true)
            {
                return false;
            }

            serializedDraft.UpdateIfRequiredOrScript();
            var property = serializedDraft.FindProperty(binding.PropertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.Vector3)
            {
                return false;
            }

            Undo.RecordObject(targetDraft, $"{binding.Label} 좌표 조절");
            property.vector3Value = value;
            serializedDraft.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(targetDraft);
            validation = null;
            lastWriteResult = null;
            preview.ApplyDraftPositionOverrides();
            Repaint();
            return true;
        }

        private int DrawEnumProperty(string propertyName, string label, string[] labels)
        {
            var property = serializedDraft.FindProperty(propertyName);
            var index = Mathf.Clamp(property.enumValueIndex, 0, labels.Length - 1);
            property.enumValueIndex = EditorGUILayout.Popup(label, index, labels);
            return property.enumValueIndex;
        }

        private static int DrawRelativeEnumProperty(
            SerializedProperty parent,
            string propertyName,
            string label,
            string[] labels)
        {
            var property = parent.FindPropertyRelative(propertyName);
            var index = Mathf.Clamp(property.enumValueIndex, 0, labels.Length - 1);
            property.enumValueIndex = EditorGUILayout.Popup(label, index, labels);
            return property.enumValueIndex;
        }

        private static void DrawRelativeProperty(
            SerializedProperty parent,
            string propertyName,
            string label)
        {
            var property = parent.FindPropertyRelative(propertyName);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
            }
        }

        private static void AddAttack(SerializedProperty attacks)
        {
            var motionId = BuildNextAttackMotionId(attacks);
            var index = attacks.arraySize;
            attacks.InsertArrayElementAtIndex(index);
            var attack = attacks.GetArrayElementAtIndex(index);
            attack.FindPropertyRelative("motionId").stringValue = motionId;
            attack.FindPropertyRelative("clip").objectReferenceValue = null;
            attack.FindPropertyRelative("playbackSpeed").floatValue = 1f;
            attack.FindPropertyRelative("crossFadeDuration").floatValue = 0.06f;
            attack.FindPropertyRelative("weight").floatValue = 1f;
            attack.FindPropertyRelative("preventImmediateRepeat").boolValue = false;
            attack.FindPropertyRelative("overrideBreathDuration").boolValue = false;
            attack.FindPropertyRelative("breathDuration").floatValue = 0.8f;
            var markers = attack.FindPropertyRelative("markers");
            markers.arraySize = 1;
            ResetMarker(markers.GetArrayElementAtIndex(0));
            attack.isExpanded = true;
        }

        private static string BuildNextAttackMotionId(SerializedProperty attacks)
        {
            for (var number = 1; ; number++)
            {
                var candidate = $"attack{number:00}";
                var alreadyUsed = false;
                for (var index = 0; index < attacks.arraySize; index++)
                {
                    var existing = attacks.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("motionId")
                        .stringValue;
                    if (string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyUsed = true;
                        break;
                    }
                }

                if (!alreadyUsed)
                {
                    return candidate;
                }
            }
        }

        private Shared.Unit.MonsterProjectileAttackMode DrawRangedHitMode(
            Shared.Unit.MonsterRangedDeliveryMode deliveryMode)
        {
            if (deliveryMode == Shared.Unit.MonsterRangedDeliveryMode.Projectile)
            {
                return (Shared.Unit.MonsterProjectileAttackMode)DrawEnumProperty(
                    "projectileMode",
                    "타격 방식",
                    RangedHitModeLabels);
            }

            var property = serializedDraft.FindProperty("projectileMode");
            var current = property.enumValueIndex == (int)Shared.Unit.MonsterProjectileAttackMode.Area ? 1 : 0;
            var selected = EditorGUILayout.Popup("타격 방식", current, InstantHitModeLabels);
            var resolved = selected == 0
                ? Shared.Unit.MonsterProjectileAttackMode.Single
                : Shared.Unit.MonsterProjectileAttackMode.Area;
            property.enumValueIndex = (int)resolved;
            return resolved;
        }

        private static void AddMarker(SerializedProperty markers)
        {
            var index = markers.arraySize;
            markers.InsertArrayElementAtIndex(index);
            ResetMarker(markers.GetArrayElementAtIndex(index));
        }

        private static void ResetMarker(SerializedProperty marker)
        {
            marker.FindPropertyRelative("normalizedTime").floatValue = 0.5f;
            marker.FindPropertyRelative("powerRatio").floatValue = 1f;
            marker.FindPropertyRelative("socketOverride").stringValue = string.Empty;
        }

        private static void ResetFeedback(SerializedProperty feedback)
        {
            feedback.FindPropertyRelative("sound").objectReferenceValue = null;
            feedback.FindPropertyRelative("sfx").objectReferenceValue = null;
            feedback.FindPropertyRelative("vfxPrefab").objectReferenceValue = null;
            feedback.FindPropertyRelative("vfxLifetime").floatValue = 1f;
            feedback.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
            feedback.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
            feedback.FindPropertyRelative("scale").floatValue = 1f;
        }

        private void DrawColumnHeader(string title, string meta)
        {
            var rect = GUILayoutUtility.GetRect(10f, 22f, GUILayout.ExpandWidth(true));
            GUI.Label(rect, title, columnTitleStyle);
            GUI.Label(new Rect(rect.x + rect.width * 0.45f, rect.y, rect.width * 0.55f, rect.height), meta, columnMetaStyle);
        }

        private void DrawSectionHeader(string title)
        {
            GUILayout.Space(5f);
            var rect = GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true));
            GUI.Label(rect, title, sectionTitleStyle);
            GUILayout.Space(2f);
        }

        private string BuildHeaderStatus()
        {
            if (draft == null)
            {
                return "제작 원본을 선택해 제작을 시작하세요";
            }

            var id = string.IsNullOrWhiteSpace(draft.MonsterId) ? "ID 미입력" : draft.MonsterId;
            if (!EditorUtility.IsPersistent(draft))
            {
                return $"{id}  ·  신규 제작 모드";
            }

            return IsEditingExistingMonster()
                ? $"{id}  ·  기존 몬스터 수정 모드 (GUID 유지)"
                : $"{id}  ·  저장된 제작 원본";
        }

        private bool IsEditingExistingMonster()
        {
            return draft != null && EditorUtility.IsPersistent(draft) && catalogDefinitions.Any(
                definition => definition != null && string.Equals(
                    definition.MonsterId,
                    draft.MonsterId,
                    StringComparison.OrdinalIgnoreCase));
        }

        private void ApplyWindowConstraints()
        {
            var catalogWidth = showMonsterCatalog ? CatalogColumnWidth + ColumnGap : 0f;
            minSize = new Vector2(MinimumWindowWidth + catalogWidth, MinimumWindowHeight);
        }

        private void SetMonsterCatalogVisible(bool visible)
        {
            if (showMonsterCatalog == visible)
            {
                return;
            }

            showMonsterCatalog = visible;
            ApplyWindowConstraints();
            Repaint();
        }

        private void SetCatalogSortMode(MonsterMakerCatalogSortMode sortMode)
        {
            if (catalogSortMode == sortMode)
            {
                return;
            }

            catalogSortMode = sortMode;
            catalogScroll = Vector2.zero;
            RebuildCatalogDisplayOrder();
            Repaint();
        }

        private void RebuildCatalogDisplayOrder()
        {
            displayedCatalogDefinitions = catalogSortMode == MonsterMakerCatalogSortMode.Rarity
                ? catalogDefinitions
                    .OrderByDescending(GetCatalogRaritySortValue)
                    .ToArray()
                : catalogDefinitions;
        }

        private int GetCatalogRaritySortValue(Shared.Unit.MonsterDefinition definition)
        {
            return TryGetCatalogRarity(definition, out var rarity) ? (int)rarity : -1;
        }

        private void ReloadCatalogEntries()
        {
            monsterCatalog = AssetDatabase.LoadAssetAtPath<Shared.Unit.MonsterCatalog>(
                MonsterMakerAssetWriter.MonsterCatalogPath);
            monsterRarityCatalog = AssetDatabase.LoadAssetAtPath<Shared.Unit.MonsterRarityCatalog>(
                MonsterMakerAssetWriter.MonsterRarityCatalogPath);
            monsterSkillCatalog = AssetDatabase.LoadAssetAtPath<Shared.Unit.MonsterSkillCatalog>(
                Shared.Unit.MonsterSkillCatalog.DefaultAssetPath);
            RebuildSkillPopupCaches();
            catalogDefinitions = monsterCatalog == null
                ? Array.Empty<Shared.Unit.MonsterDefinition>()
                : monsterCatalog.Definitions.Where(candidate => candidate != null).ToArray();
            catalogDraftsById.Clear();
            catalogRaritiesById.Clear();
            for (var index = 0; index < catalogDefinitions.Length; index++)
            {
                var definition = catalogDefinitions[index];
                catalogDraftsById[definition.MonsterId] = LoadDraftForDefinition(definition);
                if (monsterRarityCatalog != null &&
                    monsterRarityCatalog.TryGetRarity(definition.MonsterId, out var rarity))
                {
                    catalogRaritiesById[definition.MonsterId] = rarity;
                }
            }

            RebuildCatalogDisplayOrder();

            if (draft != null)
            {
                selectedCatalogDefinition = catalogDefinitions.FirstOrDefault(candidate => string.Equals(
                    candidate.MonsterId,
                    draft.MonsterId,
                    StringComparison.OrdinalIgnoreCase));
            }
            else if (selectedCatalogDefinition != null && !catalogDefinitions.Contains(selectedCatalogDefinition))
            {
                selectedCatalogDefinition = null;
            }

            Repaint();
        }

        private void RebuildSkillPopupCaches()
        {
            if (monsterSkillCatalog == null)
            {
                passiveSkillPopups = Array.Empty<MonsterSkillPopupData>();
                genericActiveSkillPopups = Array.Empty<MonsterSkillPopupData>();
                mythicActiveSkillPopups = Array.Empty<MonsterSkillPopupData>();
                return;
            }

            passiveSkillPopups = BuildSkillPopupCache(
                monsterSkillCatalog.PassiveSkills.Cast<MonsterSkillDefinitionBase>());
            genericActiveSkillPopups = BuildSkillPopupCache(
                monsterSkillCatalog.ActiveSkills
                    .Where(skill => skill != null && skill.ExecutionKind == MonsterActiveExecutionKind.Generic)
                    .Cast<MonsterSkillDefinitionBase>());
            mythicActiveSkillPopups = BuildSkillPopupCache(
                monsterSkillCatalog.ActiveSkills.Cast<MonsterSkillDefinitionBase>());
        }

        private static MonsterSkillPopupData[] BuildSkillPopupCache(
            IEnumerable<MonsterSkillDefinitionBase> source)
        {
            var enabledSkills = (source ?? Enumerable.Empty<MonsterSkillDefinitionBase>())
                .Where(skill => skill != null && skill.AuthoringEnabled)
                .OrderBy(skill => skill.Category)
                .ThenBy(skill => skill.DisplayName, StringComparer.CurrentCulture)
                .ToArray();
            var result = new MonsterSkillPopupData[SkillCategoryLabels.Length];
            for (var filter = 0; filter < result.Length; filter++)
            {
                var filtered = filter <= 0
                    ? enabledSkills
                    : enabledSkills.Where(skill => skill.Category == (MonsterSkillCategory)(filter - 1)).ToArray();
                var options = new MonsterSkillDefinitionBase[filtered.Length + 1];
                Array.Copy(filtered, 0, options, 1, filtered.Length);
                result[filter] = new MonsterSkillPopupData(
                    options,
                    options.Select(BuildSkillPopupLabel).ToArray());
            }
            return result;
        }

        private sealed class MonsterSkillPopupData
        {
            public static readonly MonsterSkillPopupData Empty = new MonsterSkillPopupData(
                new MonsterSkillDefinitionBase[] { null },
                new[] { "<미설정>" });

            public MonsterSkillPopupData(MonsterSkillDefinitionBase[] options, string[] labels)
            {
                Options = options ?? Empty.Options;
                Labels = labels ?? Empty.Labels;
            }

            public MonsterSkillDefinitionBase[] Options { get; }
            public string[] Labels { get; }
        }

        private void OnProjectChanged()
        {
            ReloadCatalogEntries();
        }

        private void ValidateDraft()
        {
            serializedDraft.ApplyModifiedProperties();
            validation = MonsterMakerValidator.Validate(draft);
            Repaint();
        }

        private void BuildAndRegister()
        {
            serializedDraft.ApplyModifiedProperties();
            validation = MonsterMakerValidator.Validate(draft);
            if (validation.HasErrors)
            {
                return;
            }

            try
            {
                if (!SaveDraft())
                {
                    return;
                }

                lastWriteResult = MonsterMakerAssetWriter.BuildAndRegister(draft);
                selectedCatalogDefinition = lastWriteResult.Definition;
                ReloadCatalogEntries();
                Selection.activeObject = lastWriteResult.Definition;
                EditorGUIUtility.PingObject(lastWriteResult.Definition);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Monster Maker", exception.Message, "확인");
            }
        }

        private bool SaveDraft()
        {
            try
            {
                if (draft == null)
                {
                    return false;
                }

                serializedDraft?.ApplyModifiedProperties();
                if (EditorUtility.IsPersistent(draft))
                {
                    if (!ValidatePersistentDraftOwnership(out var ownershipError))
                    {
                        throw new InvalidOperationException(ownershipError);
                    }

                    EditorUtility.SetDirty(draft);
                    AssetDatabase.SaveAssetIfDirty(draft);
                    CapturePersistentDraftIdentity();
                    return true;
                }

                if (!MonsterMakerValidator.UsesSafeId(draft.MonsterId))
                {
                    throw new InvalidOperationException(
                        "제작 원본을 처음 저장하려면 영문·숫자·밑줄·하이픈으로 된 Monster ID가 필요합니다.");
                }

                var productionCatalog = AssetDatabase.LoadAssetAtPath<Shared.Unit.MonsterCatalog>(
                    MonsterMakerAssetWriter.MonsterCatalogPath);
                if (productionCatalog != null && productionCatalog.TryGet(draft.MonsterId, out _))
                {
                    throw new InvalidOperationException(
                        "게임 Catalog에 이미 같은 ID가 있습니다. 새 제작 원본으로 덮어쓰지 말고 왼쪽 목록에서 기존 항목을 여세요.");
                }

                EnsureDraftFolder();
                var path = MonsterMakerAssetWriter.BuildDraftPath(draft.MonsterId);
                var existing = AssetDatabase.LoadAssetAtPath<MonsterMakerDraft>(path);
                if (existing != null)
                {
                    throw new InvalidOperationException(
                        $"같은 ID의 제작 원본이 이미 있습니다. 덮어쓰지 말고 게임 몬스터 목록에서 여세요.\n{path}");
                }

                draft.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(draft, path);
                ownsTransientDraft = false;
                serializedDraft = new SerializedObject(draft);
                AssetDatabase.SaveAssetIfDirty(draft);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                CapturePersistentDraftIdentity();
                Selection.activeObject = draft;
                EditorGUIUtility.PingObject(draft);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(exception.Message, draft);
                EditorUtility.DisplayDialog("Monster Maker", exception.Message, "확인");
                return false;
            }
        }

        private bool ValidatePersistentDraftOwnership(out string error)
        {
            var currentPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(draft));
            var expectedPath = NormalizeAssetPath(MonsterMakerAssetWriter.BuildDraftPath(draft.MonsterId));
            if (string.IsNullOrWhiteSpace(loadedDraftAssetPath) ||
                !string.Equals(currentPath, loadedDraftAssetPath, StringComparison.OrdinalIgnoreCase))
            {
                error = "열었던 제작 원본과 현재 Asset 경로가 달라졌습니다. 저장하지 말고 제작 원본을 다시 여세요.";
                return false;
            }

            if (!string.Equals(draft.MonsterId, loadedDraftMonsterId, StringComparison.Ordinal) ||
                !string.Equals(currentPath, expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                error = "저장된 제작 원본의 Monster ID 또는 파일명이 바뀌었습니다. 기존 ID는 변경할 수 없습니다.";
                return false;
            }

            var currentFingerprint = ComputeDraftFileFingerprint(currentPath);
            if (string.IsNullOrWhiteSpace(currentFingerprint) ||
                !string.Equals(currentFingerprint, loadedDraftFingerprint, StringComparison.Ordinal))
            {
                error = "제작 원본 파일이 창 밖에서 변경되었습니다. 현재 입력을 덮어쓰지 말고 제작 원본을 다시 여세요.";
                return false;
            }

            error = null;
            return true;
        }

        private void CapturePersistentDraftIdentity()
        {
            if (draft == null || !EditorUtility.IsPersistent(draft))
            {
                loadedDraftAssetPath = string.Empty;
                loadedDraftMonsterId = string.Empty;
                loadedDraftFingerprint = string.Empty;
                return;
            }

            loadedDraftAssetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(draft));
            loadedDraftMonsterId = draft.MonsterId ?? string.Empty;
            loadedDraftFingerprint = ComputeDraftFileFingerprint(loadedDraftAssetPath);
        }

        private static string ComputeDraftFileFingerprint(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return string.Empty;
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                return string.Empty;
            }

            var fullPath = Path.GetFullPath(Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(fullPath))
            {
                return string.Empty;
            }

            using var stream = File.OpenRead(fullPath);
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private void HandleDefinitionPicker()
        {
            var current = Event.current;
            if (current.commandName != "ObjectSelectorClosed" ||
                EditorGUIUtility.GetObjectPickerControlID() != DefinitionPickerId)
            {
                return;
            }

            var definition = EditorGUIUtility.GetObjectPickerObject() as Shared.Unit.MonsterDefinition;
            if (definition != null)
            {
                OpenDefinition(definition);
            }

            current.Use();
        }

        private void OpenDefinition(Shared.Unit.MonsterDefinition definition)
        {
            TryOpenDefinition(definition, true);
        }

        private bool TryOpenDefinition(Shared.Unit.MonsterDefinition definition, bool notifyWhenMissing)
        {
            if (definition == null)
            {
                return false;
            }

            selectedCatalogDefinition = definition;
            var existingDraft = LoadDraftForDefinition(definition);
            if (existingDraft == null)
            {
                Selection.activeObject = definition;
                EditorGUIUtility.PingObject(definition);
                if (notifyWhenMissing)
                {
                    ShowNotification(new GUIContent(
                        "Maker 이전 호환 데이터입니다. 제작 원본을 자동 생성하지 않습니다."));
                }

                Repaint();
                return false;
            }

            SetDraft(existingDraft, false);
            Selection.activeObject = existingDraft;
            return true;
        }

        private static MonsterMakerDraft LoadDraftForDefinition(Shared.Unit.MonsterDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.MonsterId))
            {
                return null;
            }

            var draftPath = $"{MonsterMakerAssetWriter.DraftRoot}/Draft_{SanitizeFileName(definition.MonsterId)}.asset";
            return AssetDatabase.LoadAssetAtPath<MonsterMakerDraft>(draftPath);
        }

        private void CreateTransientDraft()
        {
            ReleaseTransientDraft();
            var created = CreateInstance<MonsterMakerDraft>();
            created.name = "Draft_monster";
            created.hideFlags = HideFlags.HideAndDontSave;
            SetDraft(created, true);
        }

        private void SetDraft(MonsterMakerDraft source, bool transient)
        {
            if (draft == source)
            {
                CapturePersistentDraftIdentity();
                return;
            }

            ReleaseInitialDraftSnapshot();
            ReleaseTransientDraft();
            activePreviewPosition = null;
            selectedPreviewReference = MonsterMakerPreviewReference.None;
            previewReferenceInfoExpiresAt = 0d;
            ReleasePreviewPositionControl();
            draft = source;
            ownsTransientDraft = transient && source != null;
            if (source?.ActiveAttackProfile != null)
            {
                source.EditorSyncActiveAttackAuthoring();
            }
            CaptureInitialDraftSnapshot();
            serializedDraft = source == null ? null : new SerializedObject(source);
            validation = null;
            lastWriteResult = null;
            initializedPreview = false;
            selectedCatalogDefinition = source == null
                ? selectedCatalogDefinition
                : catalogDefinitions.FirstOrDefault(candidate => string.Equals(
                    candidate.MonsterId,
                    source.MonsterId,
                    StringComparison.OrdinalIgnoreCase));
            CapturePersistentDraftIdentity();
            RefreshPreview();
            Repaint();
        }

        private void CaptureInitialDraftSnapshot()
        {
            if (draft == null)
            {
                return;
            }

            initialDraftSnapshot = Instantiate(draft);
            initialDraftSnapshot.name = draft.name + " [Maker Initial Snapshot]";
            initialDraftSnapshot.hideFlags = HideFlags.HideAndDontSave;
        }

        private void ReleaseInitialDraftSnapshot()
        {
            if (initialDraftSnapshot != null)
            {
                DestroyImmediate(initialDraftSnapshot);
                initialDraftSnapshot = null;
            }
        }

        private void ReleaseTransientDraft()
        {
            if (ownsTransientDraft && draft != null && !EditorUtility.IsPersistent(draft))
            {
                DestroyImmediate(draft);
            }

            ownsTransientDraft = false;
            draft = null;
            serializedDraft = null;
            loadedDraftAssetPath = string.Empty;
            loadedDraftMonsterId = string.Empty;
            loadedDraftFingerprint = string.Empty;
        }

        private void RefreshPreview()
        {
            if (serializedDraft != null)
            {
                serializedDraft.ApplyModifiedProperties();
            }

            if (preview == null)
            {
                return;
            }

            preview.SetDraft(draft);
            initializedPreview = true;
        }

        private void OnEditorUpdate()
        {
            if (preview == null || draft == null)
            {
                return;
            }

            if (!initializedPreview)
            {
                RefreshPreview();
            }

            var needsRepaint = preview.Tick();
            var now = EditorApplication.timeSinceStartup;
            if (selectedPreviewReference != MonsterMakerPreviewReference.None &&
                now >= previewReferenceInfoExpiresAt)
            {
                selectedPreviewReference = MonsterMakerPreviewReference.None;
                needsRepaint = true;
            }
            if (needsRepaint || now - lastRepaintTime > 0.2d)
            {
                lastRepaintTime = now;
                Repaint();
            }
        }

        private static void EnsureDraftFolder()
        {
            var parts = MonsterMakerAssetWriter.DraftRoot.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(value.Where(character => !invalid.Contains(character)).ToArray()).Trim();
        }

        private void EnsureStyles()
        {
            if (columnStyle != null && workspacePanelStyle != null && actionDockStyle != null && actionButtonStyle != null &&
                actionPrimaryButtonStyle != null && actionStatusStyle != null && usageLeadStyle != null &&
                usageStepTitleStyle != null && usageBodyStyle != null && usageCautionStyle != null &&
                catalogRowTitleStyle != null && catalogRowMetaStyle != null && catalogRowStateStyle != null &&
                profileNameStyle != null && profileBadgeStyle != null && profileSectionStyle != null &&
                profileKeyStyle != null && profileValueStyle != null)
            {
                return;
            }

            headerTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 19,
                normal = { textColor = new Color(0.94f, 0.96f, 1f, 1f) }
            };

            headerMetaStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.72f, 0.78f, 0.86f, 1f) }
            };

            columnStyle = new GUIStyle(EditorStyles.helpBox)
            {
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(
                    Mathf.RoundToInt(ColumnPadding),
                    Mathf.RoundToInt(ColumnPadding),
                    8,
                    10)
            };

            columnTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.92f, 0.95f, 1f, 1f) }
            };

            columnMetaStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.58f, 0.66f, 0.76f, 1f) }
            };

            sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.84f, 0.89f, 0.96f, 1f) }
            };

            compactButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                margin = new RectOffset(1, 1, 0, 0)
            };

            actionButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(8, 8, 6, 6),
                normal = { textColor = new Color(0.95f, 0.97f, 1f, 1f) }
            };

            actionPrimaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(10, 10, 6, 6),
                normal = { textColor = Color.white }
            };

            workspacePanelStyle = new GUIStyle(EditorStyles.helpBox)
            {
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(8, 8, 7, 8)
            };

            actionDockStyle = new GUIStyle(EditorStyles.helpBox)
            {
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(
                    Mathf.RoundToInt(ActionDockPadding),
                    Mathf.RoundToInt(ActionDockPadding),
                    8,
                    9)
            };

            actionStatusStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                padding = new RectOffset(6, 6, 4, 4),
                normal = { textColor = new Color(0.74f, 0.82f, 0.92f, 1f) }
            };

            centeredLabelStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = 12
            };

            usageLeadStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                padding = new RectOffset(7, 7, 7, 7),
                normal = { textColor = new Color(0.84f, 0.9f, 0.98f, 1f) }
            };

            usageStepTitleStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = new Color(0.55f, 0.76f, 1f, 1f) }
            };

            usageBodyStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = new Color(0.78f, 0.82f, 0.88f, 1f) }
            };

            usageCautionStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                padding = new RectOffset(8, 8, 7, 7),
                normal = { textColor = new Color(1f, 0.82f, 0.48f, 1f) }
            };

            catalogRowTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.93f, 0.95f, 0.98f, 1f) }
            };

            catalogRowMetaStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.58f, 0.63f, 0.7f, 1f) }
            };

            catalogRowStateStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.5f, 0.73f, 1f, 1f) }
            };

            profileNameStyle = new GUIStyle(EditorStyles.whiteBoldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                normal = { textColor = new Color(0.95f, 0.97f, 1f, 1f) }
            };

            profileBadgeStyle = new GUIStyle(EditorStyles.whiteBoldLabel)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                hover = { textColor = Color.white },
                active = { textColor = Color.white },
                focused = { textColor = Color.white }
            };

            profileSectionStyle = new GUIStyle(EditorStyles.whiteBoldLabel)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.68f, 0.84f, 1f, 1f) }
            };

            profileKeyStyle = new GUIStyle(EditorStyles.whiteLabel)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                padding = new RectOffset(5, 3, 0, 0),
                normal = { textColor = new Color(0.82f, 0.87f, 0.94f, 1f) }
            };

            profileValueStyle = new GUIStyle(EditorStyles.whiteLabel)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                padding = new RectOffset(3, 3, 0, 0),
                normal = { textColor = new Color(0.97f, 0.98f, 1f, 1f) }
            };
        }
    }
}
