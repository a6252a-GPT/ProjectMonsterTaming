using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ProjectMT.Contents.CastleRaid;
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
        private const string MenuPath = "JC Tool/Monster/Monster Maker";
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
        private const float MinimumWindowWidth = 1180f;
        private const float MinimumWindowHeight = 760f;
        private static readonly Color AccentColor = new Color(0.38f, 0.66f, 1f, 1f);
        private static readonly string[] EnvironmentLabels = Enumerable.Range(0, PrefabPreviewStage.EnvironmentCount)
            .Select(PrefabPreviewStage.GetEnvironmentLabel)
            .ToArray();
        private static readonly string[] RarityLabels = { "일반", "희귀", "영웅", "전설", "신화" };
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
            "균형 진격형",
            "건물 우선형",
            "방어 시설 우선형",
            "수비대 우선형",
            "방벽 파괴형",
            "왕궁 돌격형",
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
        private readonly Dictionary<string, MonsterMakerDraft> catalogDraftsById =
            new Dictionary<string, MonsterMakerDraft>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MonsterRarity> catalogRaritiesById =
            new Dictionary<string, MonsterRarity>(StringComparer.OrdinalIgnoreCase);
        private MonsterSkillPopupData[] passiveSkillPopups = Array.Empty<MonsterSkillPopupData>();
        private MonsterSkillPopupData[] genericActiveSkillPopups = Array.Empty<MonsterSkillPopupData>();
        private MonsterSkillPopupData[] mythicActiveSkillPopups = Array.Empty<MonsterSkillPopupData>();
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
        [SerializeField] private bool showModelAdvancedSettings;
        [SerializeField] private bool showBasicAttackAdvancedSettings;
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
        [SerializeField] private int passiveSkillCategoryFilter;
        [SerializeField] private int activeSkillCategoryFilter;
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

        [MenuItem(MenuPath)]
        public static void OpenWindow()
        {
            var window = GetWindow<MonsterMakerWindow>();
            window.titleContent = new GUIContent("Monster Maker");
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
            titleContent = new GUIContent("Monster Maker");
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
                EditorGUILayout.HelpBox("새 Draft를 만들거나 기존 Draft를 선택하세요.", MessageType.Info);
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
            const float selectorLabelWidth = 44f;
            const float buttonGap = 4f;
            const float listToggleWidth = 92f;
            var contentWidth = backgroundRect.width - horizontalPadding * 2f;

            GUI.Label(
                new Rect(backgroundRect.x + horizontalPadding, backgroundRect.y + 7f, contentWidth - listToggleWidth - 8f, 24f),
                "Monster Maker",
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
            GUI.Label(new Rect(selectorX, controlY, selectorLabelWidth, ControlHeight), "DRAFT", headerMetaStyle);
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
            if (GUI.Button(new Rect(buttonX, buttonY, smallButtonWidth, ControlHeight), "새 Draft", compactButtonStyle))
            {
                CreateTransientDraft();
            }

            buttonX += smallButtonWidth + buttonGap;
            if (GUI.Button(new Rect(buttonX, buttonY, smallButtonWidth, ControlHeight), "Draft 저장", compactButtonStyle))
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
                    "Maker 제작 항목을 누르면 저장된 수정 Draft가 열립니다.",
                    usageBodyStyle);
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
                catalogDefinitions.Length <= 0
                    ? 0f
                    : catalogDefinitions.Length * rowStride - CatalogRowSpacing);
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
                    catalogDefinitions.Length);
                for (var index = visibleRange.x; index < visibleRange.y; index++)
                {
                    DrawCatalogRow(
                        catalogDefinitions[index],
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
            EditorGUI.DrawRect(portraitRect, new Color(0.075f, 0.08f, 0.09f, 1f));
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
                canEdit ? "Maker 수정 가능" : "기존 호환 · Draft 없음",
                canEdit ? catalogRowStateStyle : catalogRowMetaStyle);

            var tooltip = canEdit
                ? $"{definition.DisplayName}의 저장된 Maker Draft 열기"
                : $"{definition.DisplayName}은 Maker 이전 호환 데이터라 Draft가 없습니다.";
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
            ShowNotification(new GUIContent("기존 호환 Monster입니다. Maker Draft는 자동 생성하지 않습니다."));
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

            var index = (int)rarity;
            return index >= 0 && index < RarityLabels.Length ? RarityLabels[index] : rarity.ToString();
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
                DrawTimeline();
                GUILayout.Space(6f);
                var contentWidth = Mathf.Max(1f, previewColumnWidth - columnStyle.padding.horizontal);
                DrawBottomActionPanel(contentWidth);
                DrawPanelOutline(centerPanel.rect);
            }
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
            using (new EditorGUILayout.VerticalScope(actionDockStyle, GUILayout.ExpandWidth(true)))
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
                GUILayout.Label("모션 확인 → 입력 검증 → 게임 반영", columnMetaStyle);
                GUILayout.FlexibleSpace();
                var guideButtonLabel = showUsageGuide ? "도움말 닫기 ▲" : "도움말 ▼";
                if (GUILayout.Button(guideButtonLabel, compactButtonStyle, GUILayout.Width(106f), GUILayout.Height(26f)))
                {
                    showUsageGuide = !showUsageGuide;
                }
            }

            GUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("편집 기록", headerMetaStyle, GUILayout.Width(64f));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(
                        "↶ 수정 되돌리기",
                        compactButtonStyle,
                        GUILayout.Width(112f),
                        GUILayout.Height(26f)))
                {
                    PerformMakerUndo(false);
                }
                GUILayout.Space(4f);
                if (GUILayout.Button(
                        "↷ 다시 적용",
                        compactButtonStyle,
                        GUILayout.Width(96f),
                        GUILayout.Height(26f)))
                {
                    PerformMakerUndo(true);
                }
                GUILayout.Space(4f);
                if (GUILayout.Button(
                        "↺ 초기 상태 복원",
                        compactButtonStyle,
                        GUILayout.Width(112f),
                        GUILayout.Height(26f)))
                {
                    RestoreInitialDraftSnapshot();
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

            return "입력 검증 후 게임에 반영합니다";
        }

        private void DrawMotionPlaybackRow(float availableWidth)
        {
            var attackCount = draft.Attacks.Count;
            var buttonCount = 3 + attackCount + (attackCount > 1 ? 1 : 0);
            const float minimumButtonWidth = 132f;
            var fittedWidth = (availableWidth - ActionGap * (buttonCount - 1)) / Mathf.Max(1, buttonCount);
            var needsScroll = fittedWidth < minimumButtonWidth;
            var buttonWidth = needsScroll ? minimumButtonWidth : fittedWidth;

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
                DrawLargeActionButton("대기 모션", Color.white, buttonWidth, preview.PlayIdle);
                GUILayout.Space(ActionGap);
                DrawLargeActionButton("이동 모션", Color.white, buttonWidth, preview.PlayMove);
                for (var index = 0; index < attackCount; index++)
                {
                    GUILayout.Space(ActionGap);
                    var selectedIndex = index;
                    DrawLargeActionButton(
                        $"공격 {index + 1:00} 재생",
                        new Color(0.72f, 0.84f, 1f, 1f),
                        buttonWidth,
                        () => preview.PlayAttack(selectedIndex));
                }

                if (attackCount > 1)
                {
                    GUILayout.Space(ActionGap);
                    DrawLargeActionButton(
                        "무작위 공격",
                        new Color(0.78f, 0.86f, 1f, 1f),
                        buttonWidth,
                        preview.PlayRandomAttack);
                }

                GUILayout.Space(ActionGap);
                DrawLargeActionButton("사망 모션", new Color(0.94f, 0.84f, 0.84f, 1f), buttonWidth, preview.PlayDeath);
            }

            if (needsScroll)
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawCommandActionRow(MonsterMakerValidationReport currentReport)
        {
            var rowRect = GUILayoutUtility.GetRect(
                1f,
                42f,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(42f));
            const float sectionGap = 10f;
            const float publishGap = 8f;
            var totalGap = ActionGap + sectionGap * 2f + publishGap;
            var availableWidth = Mathf.Max(1f, rowRect.width - totalGap);

            const float pauseWeight = 1.1f;
            const float restartWeight = 1.2f;
            const float statusWeight = 2f;
            const float validateWeight = 1.5f;
            const float publishWeight = 2.7f;
            const float totalWeight = pauseWeight + restartWeight + statusWeight + validateWeight + publishWeight;

            var x = rowRect.x;
            var pauseWidth = availableWidth * pauseWeight / totalWeight;
            var restartWidth = availableWidth * restartWeight / totalWeight;
            var statusWidth = availableWidth * statusWeight / totalWeight;
            var validateWidth = availableWidth * validateWeight / totalWeight;
            var publishWidth = availableWidth - pauseWidth - restartWidth - statusWidth - validateWidth;

            DrawRectActionButton(
                new Rect(x, rowRect.y, pauseWidth, rowRect.height),
                preview.IsPlaying ? "일시정지" : "계속 재생",
                Color.white,
                actionButtonStyle,
                TogglePreviewPause);
            x += pauseWidth + ActionGap;
            DrawRectActionButton(
                new Rect(x, rowRect.y, restartWidth, rowRect.height),
                "처음부터 다시",
                Color.white,
                actionButtonStyle,
                RestartPreview);
            x += restartWidth + sectionGap;

            GUI.Label(
                new Rect(x, rowRect.y, statusWidth, rowRect.height),
                BuildCommandStatus(currentReport),
                actionStatusStyle);
            x += statusWidth + sectionGap;

            DrawRectActionButton(
                new Rect(x, rowRect.y, validateWidth, rowRect.height),
                "1. 입력 검증",
                new Color(1f, 0.88f, 0.62f, 1f),
                actionButtonStyle,
                ValidateDraft);
            x += validateWidth + publishGap;

            using (new EditorGUI.DisabledScope(currentReport.HasErrors))
            {
                var actionLabel = IsEditingExistingMonster()
                    ? "2. 수정 내용 게임 반영"
                    : "2. 신규 몬스터 게임 편입";
                DrawRectActionButton(
                    new Rect(x, rowRect.y, publishWidth, rowRect.height),
                    actionLabel,
                    currentReport.HasErrors ? Color.white : new Color(0.68f, 0.82f, 1f, 1f),
                    actionPrimaryButtonStyle,
                    BuildAndRegister);
            }
        }

        private void DrawLargeActionButton(string label, Color tint, float width, Action action)
        {
            var previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = tint;
            if (GUILayout.Button(label, actionButtonStyle, GUILayout.Width(width), GUILayout.Height(40f)))
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
                    "현재 Draft의 모든 제작 값을 Maker에서 처음 열었을 때 상태로 되돌립니다.\n복원 후에도 '수정 되돌리기'로 다시 복구할 수 있습니다.",
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
            GUILayout.Label("왼쪽 입력  →  중앙 확인  →  오류 검증  →  게임 반영", usageLeadStyle);
            GUILayout.Space(5f);
            usageGuideScroll.x = 0f;
            usageGuideScroll = EditorGUILayout.BeginScrollView(usageGuideScroll, GUILayout.Height(184f));
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(220f), GUILayout.ExpandWidth(true)))
                {
                    DrawUsageStep("1", "Draft를 준비합니다", "새 Monster는 [새 Draft], 기존 Monster는 [기존 항목 열기]로 시작하고 작업 중 입력은 [Draft 저장]으로 보관합니다.");
                    DrawUsageStep("2", "이름과 모델을 넣습니다", "ID·표시 이름·등급·초상화와 프로젝트의 원본 프리팹·실제 Animator를 직접 지정합니다.");
                    DrawUsageStep("3", "전투 기본값을 정합니다", "능력치, 타격 강도·피격 체급과 MainBattle 역할 AI를 정합니다. 여기서는 공격 모양이나 투사체 방식을 고르지 않습니다.");
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
                    GUILayout.Label("필수 검증을 통과했습니다. 게임 반영 버튼을 사용할 수 있습니다.", EditorStyles.wordWrappedMiniLabel);
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
                GUILayout.Label("저장된 Draft의 ID는 파일 소유권 보호를 위해 고정됩니다.", EditorStyles.wordWrappedMiniLabel);
            }

            DrawProperty("displayName", "표시 이름");
            DrawEnumProperty("rarity", "등급", RarityLabels);
            DrawProperty("portrait", "카드 초상화");
            DrawProperty("productionMemo", "제작 메모");
        }

        private void DrawSkillSection()
        {
            DrawSectionHeader("6. 범용 스킬");
            var rarity = (MonsterRarity)serializedDraft.FindProperty("rarity").enumValueIndex;
            var configured = serializedDraft.FindProperty("skillLoadoutConfigured");
            EditorGUILayout.PropertyField(configured, new GUIContent("범용 스킬 구성 사용"));
            if (!configured.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "기존 운영 Monster 호환 모드입니다. 스킬을 배정하려면 이 옵션을 켜세요.",
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
            var passiveOptions = DrawSkillCategoryFilter(
                passiveSkillPopups,
                ref passiveSkillCategoryFilter,
                "패시브 분류");
            DrawSkillPopup(
                passiveProperty,
                "범용 패시브",
                passiveOptions);

            var activeProperty = serializedDraft.FindProperty("rarityActiveSkill");
            if (rarity < MonsterRarity.Epic)
            {
                if (activeProperty.objectReferenceValue != null)
                {
                    EditorGUILayout.HelpBox("일반·희귀 등급은 액티브를 연결할 수 없습니다.", MessageType.Error);
                    DrawSkillPopup(activeProperty, "제거할 액티브", MonsterSkillPopupData.Empty);
                }
                else
                {
                    GUILayout.Label("일반·희귀 등급은 패시브 1개만 사용합니다.", EditorStyles.wordWrappedMiniLabel);
                }

                return;
            }

            var activeOptions = DrawSkillCategoryFilter(
                rarity == MonsterRarity.Mythic ? mythicActiveSkillPopups : genericActiveSkillPopups,
                ref activeSkillCategoryFilter,
                "액티브 분류");
            DrawSkillPopup(activeProperty, rarity == MonsterRarity.Mythic ? "액티브" : "범용 액티브", activeOptions);
            GUILayout.Label(
                rarity == MonsterRarity.Mythic
                    ? "신화는 범용 액티브와 신화 전용 액티브를 모두 선택할 수 있습니다."
                    : "영웅·전설은 공용 Recipe로 만든 범용 액티브만 선택할 수 있습니다.",
                EditorStyles.wordWrappedMiniLabel);
        }

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
            MonsterSkillPopupData popup)
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

                GUILayout.Label(selected.RecipeSummary, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private static string BuildSkillPopupLabel(MonsterSkillDefinitionBase skill)
        {
            if (skill == null)
            {
                return "<미설정>";
            }

            return $"[{SkillCategoryLabels[(int)skill.Category + 1]}] {skill.DisplayName}  [{skill.SkillId}]" +
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
                "사망 사운드는 사망 애니메이션을 시작할 때 재생됩니다. AudioClip만 지정하면 게임 편입 때 SFX Cue를 자동 생성합니다.",
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
                if (canRemove)
                {
                    DrawRelativeProperty(attack, "weight", "무작위 선택 비중");
                    DrawRelativeProperty(attack, "preventImmediateRepeat", "같은 동작 연속 방지");
                }
                DrawAttackMarkers(attack.FindPropertyRelative("markers"));
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
                    : profile != null && profile.HitCount > 1
                        ? $"이 값은 첫 타격 순간입니다. 이후 {profile.HitCount - 1}회는 기본공격 프리셋의 타격 간격으로 이어집니다."
                        : "이 값은 애니메이션의 실제 타격 순간입니다. 공격 방식과 판정은 기본공격 프리셋이 소유합니다.",
                MessageType.None);
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

            showBasicAttackAdvancedSettings = EditorGUILayout.Foldout(
                showBasicAttackAdvancedSettings,
                "몬스터별 추가 연출 · 예외 보정 (고급)",
                true);
            if (showBasicAttackAdvancedSettings)
            {
                DrawBasicAttackAdvancedOverrides(profile);
            }

            EditorGUILayout.HelpBox(
                "공격 동작은 애니메이션과 발생 시점만 정합니다. 공격 방식·판정·연타·돌진·투사체 이동과 공용 VFX/SFX는 기본공격 프리셋 한 곳에서 정합니다.",
                MessageType.Info);
        }

        private void DrawBasicAttackAdvancedOverrides(MonsterBasicAttackProfile profile)
        {
            EditorGUILayout.HelpBox(
                "비워두면 기본공격 프리셋의 공용 연출과 수치를 그대로 사용합니다. 이 몬스터만 달라야 할 때만 펼쳐서 설정합니다.",
                MessageType.None);

            var attacks = serializedDraft.FindProperty("attacks");
            for (var index = 0; attacks != null && index < attacks.arraySize; index++)
            {
                var attack = attacks.GetArrayElementAtIndex(index);
                var clip = attack.FindPropertyRelative("clip").objectReferenceValue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(
                        $"공격 동작 {index + 1:00} · {(clip == null ? "미지정" : clip.name)}",
                        EditorStyles.miniBoldLabel);
                    DrawRelativeProperty(attack, "crossFadeDuration", "전환 시간");
                    DrawOptionalAnimationFeedback(
                        attack.FindPropertyRelative("attackStartFeedback"),
                        "동작 시작 추가 연출",
                        "동작 시작 사운드",
                        "Motion 시작 순간에 별도 연출이 꼭 필요할 때만 추가합니다. 프리셋의 발사 연출은 실제 공격 발생 시점에 재생됩니다.",
                        MonsterMakerPreviewAnchor.AttackOrigin);
                    var markers = attack.FindPropertyRelative("markers");
                    if (markers != null && markers.arraySize == 1)
                    {
                        DrawOptionalAnimationFeedback(
                            markers.GetArrayElementAtIndex(0).FindPropertyRelative("feedback"),
                            "타격 연출 덮어쓰기",
                            "타격 사운드",
                            "기본공격 프리셋의 명중/폭발 연출보다 이 Motion 전용 연출을 우선할 때만 사용합니다.",
                            MonsterMakerPreviewAnchor.Socket,
                            markers.GetArrayElementAtIndex(0)
                                .FindPropertyRelative("socketOverride")
                                .stringValue);
                    }
                }
            }

            if (profile == null)
            {
                return;
            }
            if (profile.CombatType == MonsterCombatType.Ranged)
            {
                DrawProperty("projectileLaunchRecoilDistance", "발사 반동 거리");
                DrawProperty("projectileLaunchRecoilDuration", "발사 반동 시간");
            }
            if (!profile.UsesProjectileVisual)
            {
                return;
            }

            DrawProperty("projectilePrefab", "몬스터 전용 투사체 VFX");
            DrawProperty("projectileLaunchSound", "몬스터 전용 발사 사운드");
            GUILayout.Label(
                "프리셋에 공용 투사체 VFX/SFX가 지정되어 있으면 공용값을 사용하고, 비어 있을 때 이 몬스터 전용값을 사용합니다.",
                EditorStyles.wordWrappedMiniLabel);
            var overrideTuning = serializedDraft.FindProperty("overrideProjectileTuning");
            EditorGUILayout.PropertyField(overrideTuning, new GUIContent("몬스터 전용 투사체 수치 사용"));
            if (overrideTuning.boolValue)
            {
                DrawProperty("projectileSpeed", "이동 속도");
                DrawProperty("projectileLifetime", "수명");
                DrawProperty("projectileHitRadius", "충돌 반경");
            }
            else
            {
                GUILayout.Label(
                    $"프리셋 수치 · 속도 {profile.ProjectileSpeed:0.##} · 수명 {profile.ProjectileLifetime:0.##}초 · " +
                    $"충돌 반경 {profile.ProjectileCollisionRadius:0.##}m",
                    EditorStyles.wordWrappedMiniLabel);
            }
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
                    "미설정 상태입니다. 게임 편입은 가능하며 돌파 능력치와 스킬은 적용되지 않습니다.",
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
                rarity >= MonsterRarity.Epic ? "4돌파 · 액티브 강화" : "4돌파 · 패시브 추가 강화",
                rarity >= MonsterRarity.Epic);
            DrawStatModifier(serializedDraft.FindProperty("ascension5"), "5돌파 능력치");
        }

        private void DrawCastleRaidAiSection()
        {
            DrawSectionHeader("9. 군단의 역습 AI");
            var pattern = (CastleRaidAiPattern)DrawEnumProperty(
                "castleRaidAiPattern",
                "행동 패턴",
                CastleRaidAiPatternLabels);
            EditorGUILayout.HelpBox(
                "군단의 역습에서만 사용하는 목표 선택 규칙입니다. 메인 전투 AI에는 영향을 주지 않습니다.",
                MessageType.None);
            if (pattern != CastleRaidAiPattern.TacticalSupport)
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
            DrawSectionHeader("5. MainBattle 역할 AI");
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
            ResetFeedback(attack.FindPropertyRelative("attackStartFeedback"));
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
            ResetFeedback(marker.FindPropertyRelative("feedback"));
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
                return "Draft를 선택해 제작을 시작하세요";
            }

            var id = string.IsNullOrWhiteSpace(draft.MonsterId) ? "ID 미입력" : draft.MonsterId;
            if (!EditorUtility.IsPersistent(draft))
            {
                return $"{id}  ·  신규 제작 모드";
            }

            return IsEditingExistingMonster()
                ? $"{id}  ·  기존 몬스터 수정 모드 (GUID 유지)"
                : $"{id}  ·  저장된 신규 Draft";
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
                        "Draft를 처음 저장하려면 영문·숫자·밑줄·하이픈으로 된 Monster ID가 필요합니다.");
                }

                var productionCatalog = AssetDatabase.LoadAssetAtPath<Shared.Unit.MonsterCatalog>(
                    MonsterMakerAssetWriter.MonsterCatalogPath);
                if (productionCatalog != null && productionCatalog.TryGet(draft.MonsterId, out _))
                {
                    throw new InvalidOperationException(
                        "게임 Catalog에 이미 같은 ID가 있습니다. 새 Draft로 덮어쓰지 말고 왼쪽 목록에서 기존 항목을 여세요.");
                }

                EnsureDraftFolder();
                var path = MonsterMakerAssetWriter.BuildDraftPath(draft.MonsterId);
                var existing = AssetDatabase.LoadAssetAtPath<MonsterMakerDraft>(path);
                if (existing != null)
                {
                    throw new InvalidOperationException(
                        $"같은 ID의 Draft가 이미 있습니다. 덮어쓰지 말고 게임 몬스터 목록에서 여세요.\n{path}");
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
                error = "열었던 Draft와 현재 Asset 경로가 달라졌습니다. 저장하지 말고 Draft를 다시 여세요.";
                return false;
            }

            if (!string.Equals(draft.MonsterId, loadedDraftMonsterId, StringComparison.Ordinal) ||
                !string.Equals(currentPath, expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                error = "저장된 Draft의 Monster ID 또는 파일명이 바뀌었습니다. 기존 ID는 변경할 수 없습니다.";
                return false;
            }

            var currentFingerprint = ComputeDraftFileFingerprint(currentPath);
            if (string.IsNullOrWhiteSpace(currentFingerprint) ||
                !string.Equals(currentFingerprint, loadedDraftFingerprint, StringComparison.Ordinal))
            {
                error = "Draft 파일이 창 밖에서 변경되었습니다. 현재 입력을 덮어쓰지 말고 Draft를 다시 여세요.";
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
                        "Maker 이전 호환 데이터입니다. 자동 Draft 변환은 하지 않습니다."));
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
                catalogRowTitleStyle != null && catalogRowMetaStyle != null && catalogRowStateStyle != null)
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
        }
    }
}
