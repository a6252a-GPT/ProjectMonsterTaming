using System;
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
        private const float LeftColumnWidth = 430f;
        private const float PreviewColumnMinWidth = 420f;
        private const float ControlHeight = 26f;
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

        private MonsterMakerDraft draft;
        private SerializedObject serializedDraft;
        private MonsterMakerPreviewStage preview;
        private MonsterMakerValidationReport validation;
        private MonsterMakerWriteResult lastWriteResult;
        private Shared.Unit.MonsterCatalog monsterCatalog;
        private Shared.Unit.MonsterRarityCatalog monsterRarityCatalog;
        private Shared.Unit.MonsterSkillCatalog monsterSkillCatalog;
        private Shared.Unit.MonsterDefinition[] catalogDefinitions =
            Array.Empty<Shared.Unit.MonsterDefinition>();
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
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.projectChanged -= OnProjectChanged;
            preview?.Dispose();
            preview = null;
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

                catalogScroll = EditorGUILayout.BeginScrollView(catalogScroll);
                for (var index = 0; index < catalogDefinitions.Length; index++)
                {
                    DrawCatalogRow(catalogDefinitions[index], index + 1);
                    GUILayout.Space(3f);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawCatalogRow(Shared.Unit.MonsterDefinition definition, int displayIndex)
        {
            if (definition == null)
            {
                return;
            }

            var makerDraft = LoadDraftForDefinition(definition);
            var canEdit = makerDraft != null;
            var selected = selectedCatalogDefinition == definition ||
                           draft != null && string.Equals(
                               draft.MonsterId,
                               definition.MonsterId,
                               StringComparison.OrdinalIgnoreCase);
            var rowRect = GUILayoutUtility.GetRect(
                CatalogColumnWidth - 22f,
                CatalogRowHeight,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(CatalogRowHeight));
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
            if (monsterRarityCatalog != null && definition != null &&
                monsterRarityCatalog.TryGetRarity(definition.MonsterId, out rarity))
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
                var texture = preview.Render(previewRect);
                if (texture != null)
                {
                    GUI.DrawTexture(previewRect, texture, ScaleMode.StretchToFill, false);
                    DrawCombatPreviewOverlay(previewRect);
                }
                else
                {
                    DrawPreviewEmptyState(previewRect);
                }

                DrawPreviewInputHint(previewRect);
                preview.HandleInput(previewRect, Event.current);
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
            var controlsWidth = environmentWidth + viewButtonWidth * 3f + gap * 3f;
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
                preview.TogglePause);
            x += pauseWidth + ActionGap;
            DrawRectActionButton(
                new Rect(x, rowRect.y, restartWidth, rowRect.height),
                "처음부터 다시",
                Color.white,
                actionButtonStyle,
                preview.Restart);
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
                action();
            }

            GUI.backgroundColor = previousBackground;
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
                    DrawUsageStep("3", "전투 기본값을 정합니다", "상세 모델 보정, 능력치, 공격 무게·피격 체급, MainBattle 역할 AI와 공격 방식을 확인합니다.");
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(220f), GUILayout.ExpandWidth(true)))
                {
                    DrawUsageStep("4", "전용 Animation을 지정합니다", "대기·이동·공격·사망 Clip을 직접 넣고 Loop와 In Place 적합성을 눈으로 확인합니다.");
                    DrawUsageStep("5", "타격·발사 Marker를 맞춥니다", "근거리·즉발의 접촉 순간과 투사체 발사 순간을 0~1 값으로 지정합니다.");
                    DrawUsageStep("6", "선택 사운드·VFX를 연결합니다", "필요한 항목만 연결합니다. 공란은 정상이며 AudioClip은 편입 때 역할별 Cue로 생성됩니다.");
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

                GUILayout.Label(
                    "0                         타격 Marker · 왼쪽 Animation에서 수동 지정                         1",
                    EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawCombatPreviewOverlay(Rect previewRect)
        {
            const float overlayWidth = 255f;
            const float overlayHeight = 49f;
            var overlayRect = new Rect(
                previewRect.x + 10f,
                previewRect.y + 10f,
                Mathf.Min(overlayWidth, previewRect.width - 20f),
                overlayHeight);
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
            var passiveOptions = FilterSkillCategory(
                monsterSkillCatalog.PassiveSkills.Cast<MonsterSkillDefinitionBase>(),
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
                    DrawSkillPopup(activeProperty, "제거할 액티브", Array.Empty<MonsterSkillDefinitionBase>());
                }
                else
                {
                    GUILayout.Label("일반·희귀 등급은 패시브 1개만 사용합니다.", EditorStyles.wordWrappedMiniLabel);
                }

                return;
            }

            var allowedActiveOptions = monsterSkillCatalog.ActiveSkills
                .Where(skill => skill != null &&
                                (rarity == MonsterRarity.Mythic ||
                                 skill.ExecutionKind == MonsterActiveExecutionKind.Generic))
                .Cast<MonsterSkillDefinitionBase>()
                .ToArray();
            var activeOptions = FilterSkillCategory(
                allowedActiveOptions,
                ref activeSkillCategoryFilter,
                "액티브 분류");
            DrawSkillPopup(activeProperty, rarity == MonsterRarity.Mythic ? "액티브" : "범용 액티브", activeOptions);
            GUILayout.Label(
                rarity == MonsterRarity.Mythic
                    ? "신화는 범용 액티브와 신화 전용 액티브를 모두 선택할 수 있습니다."
                    : "영웅·전설은 공용 Recipe로 만든 범용 액티브만 선택할 수 있습니다.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private static MonsterSkillDefinitionBase[] FilterSkillCategory(
            System.Collections.Generic.IEnumerable<MonsterSkillDefinitionBase> source,
            ref int filter,
            string label)
        {
            filter = Mathf.Clamp(filter, 0, SkillCategoryLabels.Length - 1);
            filter = EditorGUILayout.Popup(label, filter, SkillCategoryLabels);
            var skills = (source ?? Enumerable.Empty<MonsterSkillDefinitionBase>())
                .Where(skill => skill != null && skill.AuthoringEnabled);
            if (filter > 0)
            {
                var category = (MonsterSkillCategory)(filter - 1);
                skills = skills.Where(skill => skill.Category == category);
            }

            var result = skills
                .OrderBy(skill => skill.Category)
                .ThenBy(skill => skill.DisplayName, StringComparer.CurrentCulture)
                .ToArray();
            GUILayout.Label($"표시 중 {result.Length}개", EditorStyles.miniLabel);
            return result;
        }

        private static void DrawSkillPopup(
            SerializedProperty property,
            string label,
            MonsterSkillDefinitionBase[] catalogOptions)
        {
            var current = property.objectReferenceValue as MonsterSkillDefinitionBase;
            var options = new MonsterSkillDefinitionBase[] { null }
                .Concat(catalogOptions ?? Array.Empty<MonsterSkillDefinitionBase>())
                .Distinct()
                .ToList();
            if (current != null && !options.Contains(current))
            {
                options.Add(current);
            }

            var labels = options
                .Select(skill => skill == null
                    ? "<미설정>"
                    : $"[{SkillCategoryLabels[(int)skill.Category + 1]}] {skill.DisplayName}  [{skill.SkillId}]" +
                      (!skill.AuthoringEnabled ? " · 비활성" : string.Empty) +
                      (skill is MonsterActiveSkill active &&
                       active.ExecutionKind == MonsterActiveExecutionKind.DedicatedMythic
                          ? " · 신화 전용"
                          : string.Empty))
                .ToArray();
            var currentIndex = Mathf.Max(0, options.IndexOf(current));
            var selectedIndex = EditorGUILayout.Popup(label, currentIndex, labels);
            property.objectReferenceValue = options[Mathf.Clamp(selectedIndex, 0, options.Count - 1)];

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
                    DrawProperty("visualLocalPosition", "모델 위치");
                    DrawProperty("groundOffset", "바닥 높이 보정");
                    DrawProperty("facingYawOffset", "정면 회전 보정");
                    DrawProperty("attackOriginLocalPosition", "공격 기준점 위치");
                    DrawProperty("hitCenterLocalPosition", "피격 기준점 위치");
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
            DrawProperty("attackRange", "공격 사거리");
        }

        private void DrawAnimationSection()
        {
            DrawSectionHeader("8. 애니메이션 · 타격 Marker");
            DrawProperty("idleClip", "대기 애니메이션");
            DrawProperty("moveClip", "이동 애니메이션");
            DrawAttackList();
            DrawProperty("deathClip", "사망 애니메이션");
            DrawOptionalAnimationFeedback(
                serializedDraft.FindProperty("deathFeedback"),
                "사망 애니메이션 시작",
                "사망 사운드",
                "사망 사운드는 사망 애니메이션을 시작할 때 재생됩니다. AudioClip만 지정하면 게임 편입 때 SFX Cue를 자동 생성합니다.");
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
                DrawOptionalAnimationFeedback(
                    attack.FindPropertyRelative("attackStartFeedback"),
                    "공격 동작 시작",
                    "공격 동작 사운드",
                    "공격 동작 사운드는 공격 애니메이션을 시작할 때 재생됩니다. AudioClip만 지정하면 게임 편입 때 SFX Cue를 자동 생성합니다.");
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
            var isProjectile = draft != null &&
                               draft.CombatType == Shared.Unit.MonsterCombatType.Ranged &&
                               draft.RangedDeliveryMode == Shared.Unit.MonsterRangedDeliveryMode.Projectile;
            var timingName = isProjectile ? "발사" : "타격";
            GUILayout.Space(3f);
            GUILayout.Label(timingName + " 시점", EditorStyles.boldLabel);
            for (var markerIndex = 0; markerIndex < markers.arraySize; markerIndex++)
            {
                var marker = markers.GetArrayElementAtIndex(markerIndex);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label($"{timingName} {markerIndex + 1}", EditorStyles.miniBoldLabel);
                    DrawRelativeProperty(marker, "normalizedTime", timingName + " 시점 (0~1)");
                    if (markers.arraySize > 1)
                    {
                        DrawRelativeProperty(marker, "powerRatio", "피해 비율");
                    }

                    DrawOptionalAnimationFeedback(
                        marker.FindPropertyRelative("feedback"),
                        isProjectile ? "투사체 명중" : "타격 순간",
                        isProjectile ? "명중 사운드" : "타격 사운드",
                        isProjectile
                            ? "명중 사운드는 Marker에서 발사된 투사체가 실제 대상에 도착할 때 재생됩니다. AudioClip만 지정하면 게임 편입 때 SFX Cue를 자동 생성합니다."
                            : "타격 사운드는 이 Marker의 피해와 함께 재생됩니다. AudioClip만 지정하면 게임 편입 때 SFX Cue를 자동 생성합니다.");
                    if (markers.arraySize > 1)
                    {
                        if (GUILayout.Button($"이 {timingName} 시점 삭제", compactButtonStyle, GUILayout.Height(20f)))
                        {
                            markers.DeleteArrayElementAtIndex(markerIndex);
                            break;
                        }
                    }
                }
            }

            if (GUILayout.Button(timingName + " 시점 추가", compactButtonStyle, GUILayout.Height(22f)))
            {
                AddMarker(markers);
            }
        }

        private void DrawOptionalAnimationFeedback(
            SerializedProperty feedback,
            string timingLabel,
            string soundLabel,
            string helpText)
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
                DrawRelativeProperty(feedback, "localPosition", "VFX 위치 보정");
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
            DrawSectionHeader("7. 공용 기본공격");
            var profileProperty = serializedDraft.FindProperty("basicAttackProfile");
            EditorGUILayout.PropertyField(profileProperty, new GUIContent("기본공격 프로필"));
            var profile = profileProperty.objectReferenceValue as MonsterBasicAttackProfile;
            if (profile == null)
            {
                EditorGUILayout.HelpBox(
                    "BA01~BA15 중 하나를 선택해야 정식 편입할 수 있습니다. 패시브·액티브·시그니처와는 별도입니다.",
                    MessageType.Warning);
                return;
            }

            serializedDraft.FindProperty("combatType").enumValueIndex = (int)profile.CombatType;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"[{profile.AttackId}] {profile.DisplayName}", EditorStyles.boldLabel);
                GUILayout.Label(
                    $"{profile.CombatType} · {profile.Delivery} · {profile.Shape} · 최대 {profile.MaxTargets}명 · " +
                    $"피해 단계 {profile.HitCount}",
                    EditorStyles.wordWrappedMiniLabel);
                GUILayout.Label(
                    $"판정 길이 ×{profile.RangeMultiplier:0.##} · 반경 {profile.Radius:0.##}m · " +
                    $"폭 {profile.LineWidth:0.##}m · 각도 {profile.Angle:0.#}°",
                    EditorStyles.wordWrappedMiniLabel);
            }

            using (new EditorGUI.DisabledScope(!preview.HasCombatTarget))
            {
                if (GUILayout.Button("3D 판정범위 보기", GUILayout.Height(28f)))
                {
                    preview.ShowBasicAttackArea();
                }
            }

            if (profile.CombatType == MonsterCombatType.Ranged)
            {
                DrawProperty("projectileLaunchRecoilDistance", "발사 반동 거리");
                DrawProperty("projectileLaunchRecoilDuration", "발사 반동 시간");
            }

            if (profile.UsesProjectileVisual)
            {
                DrawProperty("projectilePrefab", "투사체 VFX (선택)");
                DrawProperty("projectileLaunchSound", "투사체 발사 사운드 (선택)");
                DrawProperty("projectileSpeed", "투사체 속도");
                DrawProperty("projectileLifetime", "투사체 수명");
                EditorGUILayout.HelpBox(
                    "비우면 현재 공용 임시 구슬을 사용합니다. 최종 VFX/SFX는 몬스터별로 나중에 교체합니다.",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "이 프로필은 실제 투사체 이동 없이 Marker 시점에 공용 판정을 즉시 실행합니다.",
                    MessageType.None);
            }

            EditorGUILayout.HelpBox(
                "위 버튼 또는 공격 재생으로 BA01~BA15의 실제 XZ 판정 외곽선을 확인할 수 있습니다. " +
                "표시는 Profile의 길이·폭·각도·반경과 같은 값을 사용합니다.",
                MessageType.Info);
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
            DrawSectionHeader("4. 공격 무게 · 피격 체급");
            DrawEnumProperty("impactStrength", "공격 무게", ImpactStrengthLabels);
            DrawEnumProperty("reactionWeight", "피격 체급", ReactionWeightLabels);
            EditorGUILayout.HelpBox(
                "공격 무게는 Light < Standard < Heavy 순으로 넉백·에어본·경직 강도를 결정합니다. 피격 체급은 이 몬스터가 맞았을 때 얼마나 튕기는지를 정합니다.",
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
            catalogDefinitions = monsterCatalog == null
                ? Array.Empty<Shared.Unit.MonsterDefinition>()
                : monsterCatalog.Definitions.Where(candidate => candidate != null).ToArray();

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

            ReleaseTransientDraft();
            draft = source;
            ownsTransientDraft = transient && source != null;
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
