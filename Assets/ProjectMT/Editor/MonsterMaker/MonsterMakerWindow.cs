using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ProjectMT.Contents.CastleRaid;
using ProjectMT.Shared.Audio;
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
        private const float CatalogColumnWidth = 230f;
        private const float CatalogRowHeight = 52f;
        private const float LeftColumnWidth = 430f;
        private const float RightColumnWidth = 285f;
        private const float RightColumnContentWidth = 240f;
        private const float PreviewColumnMinWidth = 420f;
        private const float ControlHeight = 26f;
        private const float MinimumWindowWidth = 1180f;
        private const float MinimumWindowHeight = 680f;
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
        private static readonly string[] AbilityModeLabels = { "패시브", "자동 액티브" };
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
        private Shared.Unit.MonsterDefinition[] catalogDefinitions =
            Array.Empty<Shared.Unit.MonsterDefinition>();
        private Shared.Unit.MonsterDefinition selectedCatalogDefinition;
        private Vector2 catalogScroll;
        private Vector2 leftScroll;
        private Vector2 rightScroll;
        private Vector2 issueScroll;
        private bool ownsTransientDraft;
        private bool initializedPreview;
        private string loadedDraftAssetPath = string.Empty;
        private string loadedDraftMonsterId = string.Empty;
        private string loadedDraftFingerprint = string.Empty;
        [SerializeField] private bool showMonsterCatalog = true;
        private bool showUsageGuide = true;
        private double lastRepaintTime;
        private GUIStyle headerTitleStyle;
        private GUIStyle headerMetaStyle;
        private GUIStyle columnStyle;
        private GUIStyle columnTitleStyle;
        private GUIStyle columnMetaStyle;
        private GUIStyle sectionTitleStyle;
        private GUIStyle compactButtonStyle;
        private GUIStyle primaryButtonStyle;
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
            var previewColumnWidth = Mathf.Max(
                PreviewColumnMinWidth,
                position.width - OuterMargin * 2f - ColumnGap * 2f - LeftColumnWidth - RightColumnWidth -
                (showMonsterCatalog ? CatalogColumnWidth + ColumnGap : 0f));
            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                GUILayout.Space(OuterMargin);
                if (showMonsterCatalog)
                {
                    DrawCatalogColumn();
                    GUILayout.Space(ColumnGap);
                }

                DrawLeftColumn();
                GUILayout.Space(ColumnGap);
                DrawCenterColumn(previewColumnWidth);
                GUILayout.Space(ColumnGap);
                DrawRightColumn();
                GUILayout.Space(OuterMargin);
            }
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
            var background = selected
                ? new Color(0.16f, 0.27f, 0.41f, 1f)
                : canEdit
                    ? new Color(0.13f, 0.145f, 0.17f, 1f)
                    : new Color(0.11f, 0.115f, 0.125f, 1f);
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
            if (monsterRarityCatalog == null || definition == null ||
                !monsterRarityCatalog.TryGetRarity(definition.MonsterId, out var rarity))
            {
                return "등급 미지정";
            }

            var index = (int)rarity;
            return index >= 0 && index < RarityLabels.Length ? RarityLabels[index] : rarity.ToString();
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
                    DrawAnimationSection();
                    DrawCombatSection();
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
            using (new EditorGUILayout.VerticalScope(
                       columnStyle,
                       GUILayout.Width(previewColumnWidth),
                       GUILayout.ExpandWidth(false),
                       GUILayout.ExpandHeight(true)))
            {
                DrawColumnHeader("Live Preview", "창 너비에 맞춰 확장");
                GUILayout.Space(4f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(preview.CurrentClipName, headerMetaStyle, GUILayout.MinWidth(120f));
                    GUILayout.FlexibleSpace();
                    var environment = EditorGUILayout.Popup(
                        preview.EnvironmentIndex,
                        EnvironmentLabels,
                        GUILayout.Width(88f),
                        GUILayout.Height(ControlHeight));
                    if (environment != preview.EnvironmentIndex)
                    {
                        preview.SetEnvironment(environment);
                    }

                    if (GUILayout.Button("정면", compactButtonStyle, GUILayout.Width(46f), GUILayout.Height(ControlHeight)))
                    {
                        preview.SetView(180f, 8f);
                    }

                    if (GUILayout.Button("측면", compactButtonStyle, GUILayout.Width(46f), GUILayout.Height(ControlHeight)))
                    {
                        preview.SetView(90f, 8f);
                    }

                    if (GUILayout.Button("사선", compactButtonStyle, GUILayout.Width(46f), GUILayout.Height(ControlHeight)))
                    {
                        preview.SetView(145f, 10f);
                    }
                }

                GUILayout.Space(4f);
                var previewFrameRect = GUILayoutUtility.GetRect(
                    360f,
                    10000f,
                    Mathf.Max(300f, position.height - 285f),
                    Mathf.Max(300f, position.height - 285f),
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(false));
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
                    GUI.Label(previewRect, "Vendor Prefab을 수동으로 지정하세요.", centeredLabelStyle);
                }

                preview.HandleInput(previewRect, Event.current);
                DrawTimeline();
                DrawValidationIssues();
            }
        }

        private void DrawRightColumn()
        {
            using (new EditorGUILayout.VerticalScope(
                       columnStyle,
                       GUILayout.Width(RightColumnWidth),
                       GUILayout.ExpandWidth(false),
                       GUILayout.ExpandHeight(true)))
            {
                DrawColumnHeader("동작 · 완성", "미리보기 · 검증");
                GUILayout.Space(4f);
                rightScroll.x = 0f;
                rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(RightColumnContentWidth)))
                {
                    DrawSectionHeader("애니메이션 재생");
                    if (GUILayout.Button("대기 (Idle)", compactButtonStyle, GUILayout.Height(28f)))
                    {
                        preview.PlayIdle();
                    }

                    if (GUILayout.Button("이동 (Move)", compactButtonStyle, GUILayout.Height(28f)))
                    {
                        preview.PlayMove();
                    }

                    GUILayout.Space(3f);
                    var attackCount = draft.Attacks.Count;
                    for (var index = 0; index < attackCount; index++)
                    {
                        var attack = draft.Attacks[index];
                        var label = attack?.Clip == null
                            ? $"공격 {index + 1:00}"
                            : $"공격 {index + 1:00} · {attack.Clip.name}";
                        var selectedIndex = index;
                        if (GUILayout.Button(label, compactButtonStyle, GUILayout.Height(28f)))
                        {
                            preview.PlayAttack(selectedIndex);
                        }
                    }

                    if (attackCount > 1 &&
                        GUILayout.Button("무작위 공격 시험", compactButtonStyle, GUILayout.Height(26f)))
                    {
                        preview.PlayRandomAttack();
                    }

                    EditorGUILayout.HelpBox(
                        "공격 버튼은 중앙의 표준 적에게 실제 피해를 적용합니다. Marker 순간의 피격 펄스·공용 타격 VFX·게임용 데미지 플로팅을 보며 시점을 맞추세요.",
                        MessageType.Info);

                    if (GUILayout.Button("사망 (Death)", compactButtonStyle, GUILayout.Height(28f)))
                    {
                        preview.PlayDeath();
                    }

                    GUILayout.Space(8f);
                    DrawSectionHeader("재생 제어");
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(preview.IsPlaying ? "정지" : "재생", compactButtonStyle, GUILayout.Height(ControlHeight)))
                        {
                            preview.TogglePause();
                        }

                        if (GUILayout.Button("처음부터", compactButtonStyle, GUILayout.Height(ControlHeight)))
                        {
                            preview.Restart();
                        }
                    }

                    GUILayout.Space(10f);
                    DrawSectionHeader("검증 · 신규 편입/수정 반영");
                    if (GUILayout.Button("검증", compactButtonStyle, GUILayout.Height(32f)))
                    {
                        ValidateDraft();
                    }

                    GUILayout.Space(8f);
                    var currentReport = validation ?? MonsterMakerValidator.Validate(draft);
                    using (new EditorGUI.DisabledScope(currentReport.HasErrors))
                    {
                        var previousBackground = GUI.backgroundColor;
                        GUI.backgroundColor = currentReport.HasErrors ? Color.white : new Color(0.48f, 0.7f, 1f, 1f);
                        var actionLabel = IsEditingExistingMonster()
                            ? "기존 몬스터 수정 반영"
                            : "신규 몬스터 생성 및 게임 편입";
                        if (GUILayout.Button(actionLabel, primaryButtonStyle, GUILayout.Height(42f)))
                        {
                            BuildAndRegister();
                        }

                        GUI.backgroundColor = previousBackground;
                    }

                    if (currentReport.HasErrors)
                    {
                        EditorGUILayout.HelpBox("오류를 해결해야 편입할 수 있습니다.", MessageType.Error);
                    }
                    else if (lastWriteResult != null)
                    {
                        var mode = lastWriteResult.UpdatedExisting ? "GUID 유지 갱신" : "신규 생성";
                        EditorGUILayout.HelpBox($"{mode} 완료\n{lastWriteResult.Definition.MonsterId}", MessageType.Info);
                    }

                    DrawUsageGuide();
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawUsageGuide()
        {
            GUILayout.Space(12f);
            DrawSectionHeader("처음 사용하는 제작자용 사용법");
            GUILayout.Label("왼쪽 입력  →  중앙 확인  →  검증  →  게임 편입", usageLeadStyle);
            GUILayout.Space(4f);

            var guideButtonLabel = showUsageGuide ? "상세 사용법 접기  ▲" : "상세 사용법 펼치기  ▼";
            if (GUILayout.Button(guideButtonLabel, compactButtonStyle, GUILayout.Height(30f)))
            {
                showUsageGuide = !showUsageGuide;
            }

            if (!showUsageGuide)
            {
                return;
            }

            GUILayout.Space(5f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawUsageStep(
                    "1",
                    "Draft를 준비합니다",
                    "새 Monster는 상단의 [새 Draft], 기존 Monster 수정은 [기존 항목 열기]로 시작합니다. 작업 중에는 [Draft 저장]으로 입력값을 먼저 보관하세요.");
                DrawUsageStep(
                    "2",
                    "이름과 모델을 넣습니다",
                    "왼쪽 1~2번에서 ID·표시 이름·등급·카드 초상화를 입력합니다. ID에는 영문·숫자·밑줄(_)·하이픈(-)만 씁니다. 프로젝트에 저장된 원본 프리팹과 그 프리팹 안에서 실제 몸을 움직이는 애니메이터를 제작자가 직접 지정합니다.");
                DrawUsageStep(
                    "3",
                    "크기·능력치·공격 방식을 정합니다",
                    "모델 크기·위치·바닥·정면·공격 기준점·피격 기준점을 중앙 화면으로 확인하며 맞춥니다. 체력·공격·방어·공속·이속·사거리를 입력합니다. 원거리는 투사체 또는 즉발 마법을 고른 뒤 화면에 나타난 세부 값만 채웁니다. 즉발 마법은 현재 단일·범위만 사용합니다.");
                DrawUsageStep(
                    "4",
                    "전용 Animation을 직접 지정합니다",
                    "대기·이동·공격 1개 이상·사망에 해당 몬스터 팩의 전용 애니메이션 클립을 넣습니다. 이 도구는 클립을 검색하거나 역할을 자동 배치하지 않으며 In Place도 자동 추천하지 않습니다. 대기·이동은 반복, 공격·사망은 비반복이 맞는지 눈으로 확인하세요.");
                DrawUsageStep(
                    "5",
                    "실제 타격·발사 Marker를 맞춥니다",
                    "근거리·즉발은 손·발·입·마법이 맞는 순간, 투사체는 실제로 발사되는 순간을 0~1 값으로 직접 입력합니다. 공격 ID는 도구가 내부에서 자동 관리합니다. 투사체는 Marker에서 출발하고 실제 도착 뒤에 피해·명중 사운드·VFX·데미지 플로팅이 나옵니다. 시점이 여러 개면 피해 비율 합계가 1이 되게 맞춥니다.");
                DrawUsageStep(
                    "6",
                    "필요한 선택 사운드·VFX를 연결합니다",
                    "돌파가 기획되지 않은 Monster는 [돌파 옵션 사용]을 끈 채 비워 둡니다. 공격 동작 사운드는 애니메이션 시작, 투사체 발사 사운드는 Marker 발사, 타격·명중 사운드는 실제 피해 순간에 재생됩니다. 투사체 VFX가 있으면 넣고 없으면 비워 두면 공용 임시 구슬이 나옵니다. 제작자는 AudioClip만 넣으면 되고 SFX Cue는 편입 때 자동 생성됩니다.");
                DrawUsageStep(
                    "7",
                    "오른쪽 버튼으로 모두 확인합니다",
                    "대기·이동·각 공격·무작위 공격·사망을 차례로 누릅니다. 공격 시작, 투사체 발사, 실제 명중 사운드가 각각 맞는 순간에 들리는지 구분합니다. 투사체는 표준 적에게 도착하기 전 피해 숫자가 나오면 안 됩니다. 중앙에서는 우클릭 회전, 휠 확대, 환경·정면·측면·사선, 재생 위치와 [재생/정지]·[처음부터]를 사용합니다.");
                DrawUsageStep(
                    "8",
                    "검증 결과를 읽고 고칩니다",
                    "[검증]은 Asset과 Catalog를 바꾸지 않습니다. Error가 있으면 아래 결과를 읽고 왼쪽 입력을 고쳐야 하며 편입 버튼도 비활성입니다. 사운드와 VFX는 선택 항목이므로 비어 있어도 Warning이 아닙니다.");
                DrawUsageStep(
                    "9",
                    "게임에 편입하고 실제 전투를 봅니다",
                    "오류가 0이면 [몬스터 생성 및 게임 편입]을 누릅니다. 지정한 AudioClip은 공격 시작·발사·명중 등 역할별 SFX Cue로 자동 생성·갱신되고, 같은 ID는 기존 에셋 GUID를 유지합니다. 완료 뒤에는 반드시 00_Entry → 01_MainBattle에서 이동·발사체·각 사운드·피해·사망을 다시 확인하세요.");

                GUILayout.Label(
                    "중요: 카드 초상화와 3D 모델은 서로 다른 자산입니다. Vendor 원본은 수정하지 않으며, Maker Preview 통과가 실제 전투 검증 완료를 뜻하지 않습니다.",
                    usageCautionStyle);
                GUILayout.Space(6f);
                if (GUILayout.Button("동작 버튼 맨 위로", compactButtonStyle, GUILayout.Height(28f)))
                {
                    rightScroll.y = 0f;
                }
            }
        }

        private void DrawUsageStep(string number, string title, string body)
        {
            GUILayout.Label($"{number}. {title}", usageStepTitleStyle);
            GUILayout.Label(body, usageBodyStyle);
            GUILayout.Space(7f);
        }

        private void DrawTimeline()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("재생 위치", headerMetaStyle, GUILayout.Width(64f));
                EditorGUI.BeginChangeCheck();
                var normalized = EditorGUILayout.Slider(preview.NormalizedTime, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    preview.Scrub(normalized);
                }
            }

            EditorGUILayout.LabelField("0 ─────────────── 타격 Marker ─────────────── 1", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField("공격 버튼 = 실제 적 피해·플로팅·피격 피드백 · Marker는 수동 지정", EditorStyles.centeredGreyMiniLabel);
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
                EditorGUILayout.HelpBox("검증 버튼은 Asset을 만들거나 Catalog를 바꾸지 않습니다.", MessageType.None);
                return;
            }

            var errors = validation.Issues.Count(issue => issue.Severity == MonsterMakerIssueSeverity.Error);
            var warnings = validation.Issues.Count - errors;
            var type = errors > 0 ? MessageType.Error : warnings > 0 ? MessageType.Warning : MessageType.Info;
            EditorGUILayout.HelpBox($"검증 결과: 오류 {errors} / 경고 {warnings}", type);
            issueScroll = EditorGUILayout.BeginScrollView(issueScroll, GUILayout.Height(88f));
            if (validation.Issues.Count == 0)
            {
                GUILayout.Label("필수 검증을 통과했습니다.");
            }
            else
            {
                foreach (var issue in validation.Issues)
                {
                    GUILayout.Label($"[{issue.Severity}] {issue.Code} · {issue.Message}", EditorStyles.wordWrappedMiniLabel);
                }
            }

            EditorGUILayout.EndScrollView();
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

        private void DrawModelSection()
        {
            DrawSectionHeader("2. 모델 설정");
            DrawProperty("vendorPrefab", "3D 모델 프리팹");
            DrawProperty("animatorSource", "모델 애니메이터");
            DrawProperty("visualScale", "모델 크기");
            DrawProperty("visualLocalPosition", "모델 위치");
            DrawProperty("groundOffset", "바닥 높이 보정");
            DrawProperty("facingYawOffset", "정면 회전 보정");
            DrawProperty("attackOriginLocalPosition", "공격 기준점 위치");
            DrawProperty("hitCenterLocalPosition", "피격 기준점 위치");
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
            DrawSectionHeader("4. 애니메이션 지정");
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
            DrawSectionHeader("5. 공격 방식");
            var combatType = (Shared.Unit.MonsterCombatType)DrawEnumProperty(
                "combatType",
                "공격 종류",
                CombatTypeLabels);
            switch (combatType)
            {
                case Shared.Unit.MonsterCombatType.Melee:
                    var meleeMode = (Shared.Unit.MonsterMeleeAttackMode)DrawEnumProperty(
                        "meleeMode",
                        "근거리 방식",
                        MeleeModeLabels);
                    if (meleeMode == Shared.Unit.MonsterMeleeAttackMode.Area)
                    {
                        DrawProperty("meleeAreaRadius", "범위 반경");
                        DrawProperty("meleeMaxTargets", "최대 대상 수");
                    }
                    break;
                case Shared.Unit.MonsterCombatType.Ranged:
                    var deliveryMode = (Shared.Unit.MonsterRangedDeliveryMode)DrawEnumProperty(
                        "rangedDeliveryMode",
                        "전달 방식",
                        RangedDeliveryLabels);
                    var projectileMode = DrawRangedHitMode(deliveryMode);
                    if (deliveryMode == Shared.Unit.MonsterRangedDeliveryMode.Projectile)
                    {
                        DrawProperty("projectilePrefab", "투사체 VFX (선택)");
                        DrawProperty("projectileLaunchSound", "투사체 발사 사운드 (선택)");
                        DrawProperty("projectileSpeed", "투사체 속도");
                        EditorGUILayout.HelpBox(
                            "투사체 VFX를 비우면 공용 임시 원형 구슬을 자동 사용합니다. VFX를 지정하면 임시 구슬은 나오지 않습니다.",
                            MessageType.None);
                    }

                    if (projectileMode == Shared.Unit.MonsterProjectileAttackMode.Piercing)
                    {
                        DrawProperty("projectileMaxPiercingTargets", "최대 관통 수");
                    }
                    else if (projectileMode == Shared.Unit.MonsterProjectileAttackMode.Area)
                    {
                        var areaLabel = deliveryMode == Shared.Unit.MonsterRangedDeliveryMode.Projectile
                            ? "폭발"
                            : "범위";
                        DrawProperty("projectileImpactRadius", areaLabel + " 반경");
                        DrawProperty("projectileMaxImpactTargets", "최대 대상 수");
                    }
                    break;
                case Shared.Unit.MonsterCombatType.Special:
                    EditorGUILayout.HelpBox("특수형은 현재 범위 버프의 최소 규격만 사용합니다.", MessageType.Info);
                    DrawProperty("specialEffectId", "효과 ID");
                    DrawEnumProperty("specialTargetTeam", "적용 대상", TargetTeamLabels);
                    DrawProperty("specialRadius", "적용 반경");
                    DrawProperty("specialMaxTargets", "최대 대상 수");
                    DrawProperty("specialDuration", "지속 시간");
                    DrawStatModifier(serializedDraft.FindProperty("specialModifier"), "버프 능력치");
                    break;
            }
        }

        private void DrawAscensionSection()
        {
            DrawSectionHeader("7. 돌파 옵션");
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
            DrawAbility(serializedDraft.FindProperty("ascension2"), "2돌파 스킬");
            DrawStatModifier(serializedDraft.FindProperty("ascension3"), "3돌파 능력치");
            DrawAbility(serializedDraft.FindProperty("ascension4"), "4돌파 스킬");
            DrawStatModifier(serializedDraft.FindProperty("ascension5"), "5돌파 능력치");
        }

        private void DrawCastleRaidAiSection()
        {
            DrawSectionHeader("6. 군단의 역습 AI");
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

        private void DrawAbility(SerializedProperty ability, string label)
        {
            ability.isExpanded = EditorGUILayout.Foldout(ability.isExpanded, label, true);
            if (!ability.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            DrawRelativeProperty(ability, "abilityId", "스킬 ID");
            DrawRelativeProperty(ability, "displayName", "스킬 이름");
            var mode = DrawRelativeEnumProperty(ability, "mode", "스킬 방식", AbilityModeLabels);
            if ((Shared.Unit.MonsterAbilityMode)mode == Shared.Unit.MonsterAbilityMode.AutoActive)
            {
                DrawRelativeProperty(ability, "triggerPolicyId", "자동 발동 조건 ID");
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
            if (columnStyle != null && usageLeadStyle != null && usageStepTitleStyle != null &&
                usageBodyStyle != null && usageCautionStyle != null && catalogRowTitleStyle != null &&
                catalogRowMetaStyle != null && catalogRowStateStyle != null)
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
                padding = new RectOffset(10, 10, 8, 10)
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

            primaryButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.93f, 0.97f, 1f, 1f) }
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
