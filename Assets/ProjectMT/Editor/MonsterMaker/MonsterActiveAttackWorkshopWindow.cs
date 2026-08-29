using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public sealed class MonsterActiveAttackWorkshopWindow : EditorWindow // 공격 Step 구조만 제작하는 전용 창
    {
        public const string ProfileRoot = MonsterActiveAttackAuthoringService.ProfileRoot;
        public const string CustomProfileRoot = MonsterActiveAttackAuthoringService.CustomProfileRoot;
        private const string MenuPath = "JC Tool/Monster/공격 액티브 조립소";
        private const float CardGap = 6f;
        private const float LibraryWidth = 285f;
        private const float AssemblerWidth = 480f;
        private const float AssemblerContentWidth = AssemblerWidth - 30f; // 세로 스크롤바와 패널 여백을 제외한 실제 편집 폭
        private const float MinimumPreviewWidth = 300f;
        private readonly List<MonsterActiveAttackProfile> profiles = new List<MonsterActiveAttackProfile>();
        private readonly Dictionary<MonsterActiveAttackProfile, int> profileUsages =
            new Dictionary<MonsterActiveAttackProfile, int>();
        private MonsterActiveAttackProfile profile; // 저장 자산과 분리된 작업 사본
        private MonsterActiveAttackProfile loadedProfile;
        private MonsterMakerDraft originDraft;
        private SerializedObject serializedProfile;
        private bool workCopyDirty;
        private string message = string.Empty;
        private MessageType messageType = MessageType.Info;
        private Vector2 libraryScroll;
        private Vector2 assemblerScroll;
        private string search = string.Empty;
        private int selectedPreviewStep;
        private MonsterActiveAttackWorkshopPreview preview;
        private GUIStyle stepHeaderStyle;
        private Rect lastAssemblerContentRect; // 최소 창 폭 QA용 실제 중앙 콘텐츠 경계
        private Rect lastAssemblerViewportRect; // 세로 스크롤이 실제로 보여 주는 중앙 폭
        private Rect lastStepHeaderRightmostRect; // Step 헤더의 가장 오른쪽 조작 버튼 경계
        private Rect lastDelayRowRightmostRect; // 펼친 딜레이 행의 마지막 단위 라벨 경계
        private Rect lastPresentationHeaderRightmostRect; // VFX 공간 헤더의 삭제 버튼 경계
        private Rect lastHitEffectRightmostRect; // 타격 효과 헤더의 삭제 버튼 경계
        private Rect lastAssemblerPanelRect; // 저장 영역까지 포함한 중앙 패널 경계
        private Rect lastSaveRightmostRect; // 두 저장 버튼 중 가장 오른쪽 버튼 경계
        private Rect lastPreviewColumnRect; // 우측 미리보기 열 경계
        private Rect lastPreviewToolbarRightmostRect; // Step 미리보기 재생 버튼 경계
        internal int PreviewSceneHandle => preview?.SceneHandle ?? 0;

        public static event Action PresetAssigned;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            OpenFor(null, null);
        }

        public static void OpenFor(MonsterActiveAttackProfile target)
        {
            OpenFor(target, null);
        }

        public static void OpenFor(MonsterActiveAttackProfile target, MonsterMakerDraft draft)
        {
            foreach (var staleWindow in Resources.FindObjectsOfTypeAll<MonsterActiveAttackWorkshopWindow>())
                staleWindow.Close();
            var window = CreateInstance<MonsterActiveAttackWorkshopWindow>();
            window.titleContent = new GUIContent("공격 액티브 조립소");
            window.minSize = new Vector2(1100f, 700f);
            var mainWindow = EditorGUIUtility.GetMainWindowPosition();
            var width = Mathf.Clamp(mainWindow.width - 120f, 1100f, 1380f);
            var height = Mathf.Clamp(mainWindow.height - 120f, 700f, 900f);
            window.position = new Rect(
                mainWindow.x + (mainWindow.width - width) * 0.5f,
                mainWindow.y + (mainWindow.height - height) * 0.5f,
                width,
                height);
            window.originDraft = draft;
            if (target == null) window.StartBlank();
            else window.LoadProfile(target);
            window.ShowUtility();
            window.Focus();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("공격 액티브 조립소");
            minSize = new Vector2(1100f, 700f);
            preview ??= new MonsterActiveAttackWorkshopPreview();
            RefreshProfiles();
            if (profile == null) StartBlank();
            EditorApplication.projectChanged += RefreshProfiles;
            EditorApplication.update += TickPreview;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= RefreshProfiles;
            EditorApplication.update -= TickPreview;
            preview?.Dispose();
            preview = null;
            DisposeWorkingProfile();
        }

        private void OnDestroy()
        {
            preview?.Dispose();
            preview = null;
            DisposeWorkingProfile();
        }

        private void OnGUI()
        {
            EnsureStyles();
            MonsterWorkshopVisualTheme.DrawHeader(
                "공격 액티브 조립소",
                "공격 Step 조립 · 몬스터별 연출 계약 · 독립 판정 미리보기");
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLibrary();
                DrawAssembler();
                DrawPreview();
            }
        }

        private void TickPreview()
        {
            if (preview?.Tick() == true) Repaint();
        }

        private void DrawAssembler()
        {
            using (var assemblerScope = new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox,
                       GUILayout.Width(AssemblerWidth)))
            {
                GUILayout.Label("공격 액티브 조립", EditorStyles.boldLabel);
                GUILayout.Label(
                    loadedProfile == null
                        ? "빈 작업 사본 · 아직 프리셋 자산이 아닙니다."
                        : $"직접 수정 중: {AssetDatabase.GetAssetPath(loadedProfile)}",
                    EditorStyles.wordWrappedMiniLabel);
                if (loadedProfile != null)
                {
                    GUILayout.Label(
                        "프리셋 ID만 잠깁니다. 이름·설명·Step은 바로 편집하고 아래의 업데이트 버튼으로 저장하세요.",
                        EditorStyles.wordWrappedMiniLabel);
                    if (GUILayout.Button("다른 프리셋으로 복제", EditorStyles.miniButton, GUILayout.Height(22f)))
                    {
                        ForkLoadedAsNew();
                    }
                }
                if (profile == null || serializedProfile == null)
                {
                    DrawEmptyState();
                    return;
                }

                serializedProfile.UpdateIfRequiredOrScript();
                assemblerScroll = MonsterWorkshopVisualTheme.BeginVerticalScrollView(assemblerScroll);
                using (var contentScope = new EditorGUILayout.VerticalScope(GUILayout.Width(AssemblerContentWidth)))
                {
                    DrawProfileMetadata();
                    GUILayout.Space(8f);
                    DrawSteps();
                    GUILayout.Space(10f);
                    DrawValidation();
                    if (Event.current.type == EventType.Repaint)
                    {
                        lastAssemblerContentRect = contentScope.rect;
                    }
                }
                EditorGUILayout.EndScrollView();
                if (Event.current.type == EventType.Repaint)
                {
                    lastAssemblerViewportRect = GUILayoutUtility.GetLastRect();
                }

                if (serializedProfile.ApplyModifiedProperties())
                {
                    OnWorkingProfileChanged();
                }

                GUILayout.Space(8f);
                DrawSaveAndAssignControls();
                if (Event.current.type == EventType.Repaint)
                {
                    lastAssemblerPanelRect = assemblerScope.rect;
                }
            }
        }

        private void DrawLibrary()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(LibraryWidth)))
            {
                GUILayout.Label("저장된 프리셋", EditorStyles.boldLabel);
                if (MonsterWorkshopVisualTheme.DrawTintedButton(
                        new GUIContent("+ 빈 공격 액티브 조립"),
                        MonsterWorkshopVisualTheme.PrimaryColor,
                        28f))
                {
                    StartBlank();
                }

                if (originDraft != null)
                {
                    var assigned = originDraft.ActiveAttackProfile;
                    GUILayout.Label(
                        assigned == null
                            ? $"현재 {originDraft.MonsterId} · 미배정"
                            : $"현재 {originDraft.MonsterId} · [{assigned.ProfileId}]",
                        EditorStyles.miniLabel);
                    using (new EditorGUI.DisabledScope(assigned == null))
                    {
                        var label = assigned == null
                            ? "현재 배정 프리셋 없음"
                            : "현재 배정 프리셋 불러오기";
                        if (GUILayout.Button(label, GUILayout.Height(24f)))
                        {
                            LoadProfile(assigned);
                        }
                    }
                }

                search = EditorGUILayout.TextField("검색", search);
                libraryScroll = MonsterWorkshopVisualTheme.BeginVerticalScrollView(libraryScroll);
                DrawProfileList();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawProfileList()
        {
            var visibleCount = profiles.Count(candidate => candidate != null && MatchesSearch(candidate));
            GUILayout.Space(4f);
            GUILayout.Label($"프리셋 {visibleCount}종", EditorStyles.miniBoldLabel);
            for (var index = 0; index < profiles.Count; index++)
            {
                var candidate = profiles[index];
                if (candidate == null || !MatchesSearch(candidate)) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    var usage = profileUsages.TryGetValue(candidate, out var count) ? count : 0;
                    if (MonsterWorkshopVisualTheme.DrawPresetButton(
                            new GUIContent(
                                $"[{candidate.ProfileId}] {candidate.DisplayName}",
                                $"현재 {usage}마리가 사용 · {candidate.Description}"),
                            candidate == loadedProfile))
                    {
                        LoadProfile(candidate);
                    }
                    GUILayout.Label(
                        new GUIContent(usage.ToString(), "사용 중인 몬스터 수"),
                        EditorStyles.centeredGreyMiniLabel,
                        GUILayout.Width(24f));
                }
            }
        }

        private void DrawPreview()
        {
            using (var previewScope = new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                if (profile == null || preview == null)
                {
                    EditorGUILayout.HelpBox("왼쪽에서 공격 액티브 프로필을 선택하세요.", MessageType.Info);
                    return;
                }

                var valid = profile.TryValidate(out _);
                selectedPreviewStep = Mathf.Clamp(selectedPreviewStep, 0, Mathf.Max(0, profile.Steps.Count - 1));
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label("Step 미리보기", EditorStyles.miniBoldLabel, GUILayout.Width(74f));
                    var labels = profile.Steps.Select((step, index) =>
                        $"#{index + 1:00} {step.DisplayName}").ToArray();
                    if (labels.Length > 0)
                    {
                        selectedPreviewStep = EditorGUILayout.Popup(
                            selectedPreviewStep,
                            labels,
                            GUILayout.MinWidth(0f),
                            GUILayout.ExpandWidth(true));
                        using (new EditorGUI.DisabledScope(!valid || preview.IsPlaying))
                        {
                            var playSelected = MonsterWorkshopVisualTheme.DrawTintedButton(
                                new GUIContent("선택 Step 재생"),
                                MonsterWorkshopVisualTheme.PreviewColor,
                                30f,
                                96f);
                            if (Event.current.type == EventType.Repaint)
                            {
                                lastPreviewToolbarRightmostRect = GUILayoutUtility.GetLastRect();
                            }
                            if (playSelected)
                                preview.PlayStep(selectedPreviewStep);
                        }
                    }
                }

                var totalPreviewHeight = Mathf.Max(480f, position.height - 335f);
                var eachHeight = totalPreviewHeight * 0.5f;
                GUILayout.Label("탑다운 판정 평면도", EditorStyles.boldLabel);
                var topDownRect = GUILayoutUtility.GetRect(
                    MinimumPreviewWidth, 10000f, eachHeight, eachHeight, GUILayout.ExpandWidth(true));
                preview.Render(topDownRect, true);

                GUILayout.Label("사선 연출 미리보기", EditorStyles.boldLabel);
                var perspectiveRect = GUILayoutUtility.GetRect(
                    MinimumPreviewWidth, 10000f, eachHeight, eachHeight, GUILayout.ExpandWidth(true));
                preview.Render(perspectiveRect, false);

                using (new EditorGUI.DisabledScope(!valid || profile.Steps.Count == 0 || preview.IsPlaying))
                {
                    if (MonsterWorkshopVisualTheme.DrawTintedButton(
                            new GUIContent("전체 공격 미리보기 재생"),
                            MonsterWorkshopVisualTheme.PreviewColor,
                            30f))
                    {
                        preview.PlayAll();
                    }
                }
                GUILayout.Label(preview.Status, EditorStyles.wordWrappedMiniLabel);
                GUILayout.Label(
                    $"Step {profile.Steps.Count}개 · 예상 {profile.EstimateDuration():0.##}초 · " +
                    "청록색 외곽선이 실제 액티브 판정",
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.HelpBox(
                    "청록색 외곽선이 실제 액티브 판정 모양입니다. VFX·SFX는 몬스터별 Maker에서 연결하는 후속 슬롯입니다.",
                    MessageType.None);
                if (Event.current.type == EventType.Repaint)
                {
                    lastPreviewColumnRect = previewScope.rect;
                }
            }
        }

        private void RefreshProfiles()
        {
            profiles.Clear();
            profileUsages.Clear();
            if (AssetDatabase.IsValidFolder(ProfileRoot))
            {
                var guids = AssetDatabase.FindAssets("t:MonsterActiveAttackProfile", new[] { ProfileRoot });
                for (var index = 0; index < guids.Length; index++)
                {
                    var candidate = AssetDatabase.LoadAssetAtPath<MonsterActiveAttackProfile>(
                        AssetDatabase.GUIDToAssetPath(guids[index]));
                    if (candidate != null) profiles.Add(candidate);
                }
                profiles.Sort((left, right) =>
                {
                    var byName = string.Compare(
                        left?.DisplayName,
                        right?.DisplayName,
                        StringComparison.CurrentCultureIgnoreCase);
                    return byName != 0
                        ? byName
                        : string.Compare(left?.ProfileId, right?.ProfileId, StringComparison.OrdinalIgnoreCase);
                });
            }
            if (AssetDatabase.IsValidFolder(MonsterMakerAssetWriter.DraftRoot))
            {
                var draftGuids = AssetDatabase.FindAssets(
                    "t:MonsterMakerDraft",
                    new[] { MonsterMakerAssetWriter.DraftRoot });
                for (var index = 0; index < draftGuids.Length; index++)
                {
                    var draft = AssetDatabase.LoadAssetAtPath<MonsterMakerDraft>(
                        AssetDatabase.GUIDToAssetPath(draftGuids[index]));
                    var assigned = draft?.ActiveAttackProfile;
                    if (assigned == null) continue;
                    profileUsages.TryGetValue(assigned, out var usage);
                    profileUsages[assigned] = usage + 1;
                }
            }
            Repaint();
        }

        private bool MatchesSearch(MonsterActiveAttackProfile candidate)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            return candidate.ProfileId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   candidate.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawEmptyState()
        {
            GUILayout.Space(70f);
            GUILayout.Label("공격 액티브 프로필을 선택하거나 새로 만드세요.", EditorStyles.centeredGreyMiniLabel);
            GUILayout.Space(12f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (MonsterWorkshopVisualTheme.DrawTintedButton(
                        new GUIContent("빈 공격 액티브 조립"),
                        MonsterWorkshopVisualTheme.PrimaryColor,
                        36f,
                        190f))
                {
                    StartBlank();
                }
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawProfileMetadata()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("프로필 정보", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(loadedProfile != null))
                {
                    EditorGUILayout.PropertyField(
                        serializedProfile.FindProperty("profileId"),
                        new GUIContent("프리셋 ID"));
                }
                if (loadedProfile != null)
                {
                    GUILayout.Label(
                        "저장된 프리셋의 ID는 참조 보호를 위해 고정됩니다. 새 ID가 필요하면 위에서 분기하세요.",
                        EditorStyles.wordWrappedMiniLabel);
                }
                EditorGUILayout.PropertyField(
                    serializedProfile.FindProperty("displayName"),
                    new GUIContent("표시 이름"));
                EditorGUILayout.LabelField("기획 메모");
                var description = serializedProfile.FindProperty("description");
                description.stringValue = MonsterWorkshopVisualTheme.DrawWrappedTextArea(
                    description.stringValue,
                    54f,
                    AssemblerContentWidth - 8f);
                GUILayout.Label("스킬 이름과 최대 기력은 이 프로필이 아니라 각 몬스터의 Maker에서 정합니다.",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawSteps()
        {
            var steps = serializedProfile.FindProperty("steps");
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"공격 Step · {steps.arraySize}개", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (MonsterWorkshopVisualTheme.DrawTintedButton(
                        new GUIContent("+ 공격 추가"),
                        MonsterWorkshopVisualTheme.PrimaryColor,
                        22f,
                        110f))
                {
                    ShowAddStepMenu(steps);
                }
            }

            if (steps.arraySize == 0)
            {
                EditorGUILayout.HelpBox("공격 Step이 없습니다. 위 버튼에서 공격 형태를 선택하세요.", MessageType.Warning);
                return;
            }

            for (var index = 0; index < steps.arraySize; index++)
            {
                GUILayout.Space(index == 0 ? 4f : CardGap);
                DrawStepDelayConnector(steps.GetArrayElementAtIndex(index), index);
                GUILayout.Space(2f);
                DrawStepCard(steps, index);
            }

            GUILayout.Space(10f);
            DrawSharedFeelOptions();
        }

        private void DrawStepCard(SerializedProperty steps, int index)
        {
            var step = steps.GetArrayElementAtIndex(index);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawStepHeader(steps, step, index);
                if (!step.isExpanded) return;
                EditorGUI.indentLevel++;
                GUILayout.Label("1. 공격 흐름", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(step.FindPropertyRelative("stepId"), new GUIContent("Step ID"));
                EditorGUILayout.PropertyField(step.FindPropertyRelative("displayName"), new GUIContent("표시 이름"));
                DrawTargetPolicy(step.FindPropertyRelative("targetPolicy"));
                if ((MonsterActiveTargetPolicy)step.FindPropertyRelative("targetPolicy").enumValueIndex ==
                    MonsterActiveTargetPolicy.DifferentTarget)
                {
                    GUILayout.Label("앞 Step에서 맞힌 대상을 우선 제외하고 다음 적을 선택합니다.",
                        EditorStyles.wordWrappedMiniLabel);
                }

                var teleport = step.FindPropertyRelative("teleportBeforeAttack");
                EditorGUILayout.PropertyField(teleport, new GUIContent("공격 전 순간이동"));
                if (teleport.boolValue)
                {
                    EditorGUILayout.PropertyField(step.FindPropertyRelative("teleportFrontDistance"),
                        new GUIContent("타깃 앞 거리(m)"));
                }

                GUILayout.Space(5f);
                GUILayout.Label("2. 공격 방식", EditorStyles.miniBoldLabel);
                var pattern = step.FindPropertyRelative("pattern");
                DrawPattern(pattern);
                DrawProgression(step, (MonsterActiveAttackPattern)pattern.enumValueIndex);

                GUILayout.Space(5f);
                GUILayout.Label("3. 판정 수치", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(step.FindPropertyRelative("damageMultiplier"),
                    new GUIContent("공격력 배율"));
                EditorGUILayout.PropertyField(step.FindPropertyRelative("maxTargets"),
                    new GUIContent("최대 타깃"));
                DrawPatternFields(step, (MonsterActiveAttackPattern)pattern.enumValueIndex);
                EditorGUILayout.PropertyField(step.FindPropertyRelative("telegraphDelay"),
                    new GUIContent("예고 후 판정(초)"));
                EditorGUILayout.PropertyField(step.FindPropertyRelative("visualDuration"),
                    new GUIContent("연출 유지(초)"));

                GUILayout.Space(6f);
                DrawActivePresentationContract(step, (MonsterActiveAttackPattern)pattern.enumValueIndex);
                GUILayout.Space(4f);
                GUILayout.Label("5. 타격 효과", EditorStyles.miniBoldLabel);
                DrawHitEffects(step.FindPropertyRelative("hitEffects"));
                EditorGUI.indentLevel--;
            }
        }

        private void DrawStepDelayConnector(SerializedProperty step, int index)
        {
            var delay = step.FindPropertyRelative("delayAfterPrevious");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(
                        index == 0 ? "스킬 발동  →  STEP 01" : $"STEP {index:00}  →  STEP {index + 1:00}",
                        EditorStyles.miniBoldLabel,
                        GUILayout.Width(138f));
                    var enabled = delay.floatValue > 0.0001f;
                    EditorGUI.BeginChangeCheck();
                    var requested = EditorGUILayout.ToggleLeft(
                        enabled ? $"딜레이 사용 · {delay.floatValue:0.###}초" : "딜레이 사용",
                        enabled,
                        GUILayout.Width(138f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        delay.floatValue = requested ? 0.1f : 0f;
                        GUI.FocusControl(null);
                    }
                }

                if (delay.floatValue <= 0.0001f) return;

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(138f);
                    GUILayout.Label(index == 0 ? "첫 공격까지" : "다음 공격까지", GUILayout.Width(78f));
                    var current = Mathf.Max(0f, delay.floatValue);
                    EditorGUI.BeginChangeCheck();
                    var slider = GUILayout.HorizontalSlider(Mathf.Clamp(current, 0f, 1f), 0f, 1f,
                        GUILayout.MinWidth(80f));
                    if (EditorGUI.EndChangeCheck()) delay.floatValue = slider;
                    EditorGUI.BeginChangeCheck();
                    var typed = EditorGUILayout.FloatField(delay.floatValue, GUILayout.Width(58f));
                    if (EditorGUI.EndChangeCheck()) delay.floatValue = Mathf.Max(0f, typed);
                    GUILayout.Label("초", GUILayout.Width(14f));
                    if (Event.current.type == EventType.Repaint)
                    {
                        lastDelayRowRightmostRect = GUILayoutUtility.GetLastRect();
                    }
                }
                GUILayout.Label(
                    index == 0
                        ? "액티브 모션이 시작된 뒤 첫 Step이 실행되기 전까지의 간격입니다."
                        : "앞 Step의 판정이 끝난 뒤 다음 Step을 시작하기 전까지의 간격입니다.",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawStepHeader(SerializedProperty steps, SerializedProperty step, int index)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var title = step.FindPropertyRelative("displayName").stringValue;
                step.isExpanded = GUILayout.Toggle(
                    step.isExpanded,
                    $"#{index + 1:00}  {(string.IsNullOrWhiteSpace(title) ? "이름 없음" : title)}",
                    stepHeaderStyle,
                    GUILayout.MinWidth(0f),
                    GUILayout.ExpandWidth(true));
                using (new EditorGUI.DisabledScope(index == 0))
                {
                    if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(26f)))
                    {
                        steps.MoveArrayElement(index, index - 1);
                        GUIUtility.ExitGUI();
                    }
                }
                using (new EditorGUI.DisabledScope(index >= steps.arraySize - 1))
                {
                    if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(26f)))
                    {
                        steps.MoveArrayElement(index, index + 1);
                        GUIUtility.ExitGUI();
                    }
                }
                if (GUILayout.Button("복제", EditorStyles.miniButtonMid, GUILayout.Width(40f)))
                {
                    steps.InsertArrayElementAtIndex(index + 1);
                    var clone = steps.GetArrayElementAtIndex(index + 1);
                    clone.FindPropertyRelative("stepId").stringValue = BuildNextStepId(steps);
                    clone.FindPropertyRelative("displayName").stringValue = title + " 복제";
                    clone.isExpanded = true;
                    GUIUtility.ExitGUI();
                }
                var remove = GUILayout.Button("삭제", EditorStyles.miniButtonRight, GUILayout.Width(40f));
                if (Event.current.type == EventType.Repaint)
                {
                    lastStepHeaderRightmostRect = GUILayoutUtility.GetLastRect();
                }
                if (remove &&
                    EditorUtility.DisplayDialog("Step 삭제", $"#{index + 1:00} Step을 삭제할까요?", "삭제", "취소"))
                {
                    steps.DeleteArrayElementAtIndex(index);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private static void DrawProgression(SerializedProperty step, MonsterActiveAttackPattern pattern)
        {
            var progression = step.FindPropertyRelative("progression");
            var supported = Enum.GetValues(typeof(MonsterActiveAttackProgression))
                .Cast<MonsterActiveAttackProgression>()
                .Where(value => MonsterActiveAttackStep.SupportsProgression(pattern, value))
                .ToArray();
            var labels = supported.Select(GetProgressionLabel).ToArray();
            var current = (MonsterActiveAttackProgression)progression.enumValueIndex;
            var selected = Mathf.Max(0, Array.IndexOf(supported, current));
            selected = EditorGUILayout.Popup("판정 진행", selected, labels);
            progression.enumValueIndex = (int)supported[Mathf.Clamp(selected, 0, supported.Length - 1)];
            if ((MonsterActiveAttackProgression)progression.enumValueIndex != MonsterActiveAttackProgression.Instant)
            {
                EditorGUILayout.PropertyField(step.FindPropertyRelative("progressionDuration"),
                    new GUIContent("순차 진행 시간(초)"));
            }
        }

        private static void DrawTargetPolicy(SerializedProperty property)
        {
            var values = (MonsterActiveTargetPolicy[])Enum.GetValues(typeof(MonsterActiveTargetPolicy));
            var labels = values.Select(GetTargetPolicyLabel).ToArray();
            var current = Mathf.Max(0, Array.IndexOf(values, (MonsterActiveTargetPolicy)property.enumValueIndex));
            property.enumValueIndex = (int)values[EditorGUILayout.Popup("타깃 선택", current, labels)];
        }

        private static void DrawPattern(SerializedProperty property)
        {
            var values = (MonsterActiveAttackPattern[])Enum.GetValues(typeof(MonsterActiveAttackPattern));
            var labels = values.Select(GetPatternLabel).ToArray();
            var current = Mathf.Max(0, Array.IndexOf(values, (MonsterActiveAttackPattern)property.enumValueIndex));
            property.enumValueIndex = (int)values[EditorGUILayout.Popup("공격 형태", current, labels)];
        }

        private static void DrawProjectileFormation(SerializedProperty property)
        {
            var values = (MonsterActiveProjectileFormation[])Enum.GetValues(typeof(MonsterActiveProjectileFormation));
            var labels = values.Select(GetProjectileFormationLabel).ToArray();
            var current = Mathf.Max(0, Array.IndexOf(values,
                (MonsterActiveProjectileFormation)property.enumValueIndex));
            property.enumValueIndex = (int)values[EditorGUILayout.Popup("발사 방식", current, labels)];
        }

        private static void DrawInstantMagicTarget(SerializedProperty property)
        {
            var values = (MonsterActiveInstantMagicTarget[])Enum.GetValues(typeof(MonsterActiveInstantMagicTarget));
            var labels = values.Select(GetInstantMagicTargetLabel).ToArray();
            var current = Mathf.Max(0, Array.IndexOf(values,
                (MonsterActiveInstantMagicTarget)property.enumValueIndex));
            property.enumValueIndex = (int)values[EditorGUILayout.Popup("마법 대상", current, labels)];
        }

        private static void DrawMagicDirection(SerializedProperty property)
        {
            var values = (MonsterActiveMagicDirection[])Enum.GetValues(typeof(MonsterActiveMagicDirection));
            var labels = values.Select(GetMagicDirectionLabel).ToArray();
            var current = Mathf.Max(0, Array.IndexOf(values,
                (MonsterActiveMagicDirection)property.enumValueIndex));
            property.enumValueIndex = (int)values[EditorGUILayout.Popup("등장 방향", current, labels)];
        }

        private static void DrawHitEffectType(SerializedProperty property, string label)
        {
            var values = (MonsterActiveHitEffectType[])Enum.GetValues(typeof(MonsterActiveHitEffectType));
            var labels = values.Select(GetEffectLabel).ToArray();
            var current = Mathf.Max(0, Array.IndexOf(values, (MonsterActiveHitEffectType)property.enumValueIndex));
            property.enumValueIndex = (int)values[EditorGUILayout.Popup(label, current, labels)];
        }

        private static void DrawPatternFields(SerializedProperty step, MonsterActiveAttackPattern pattern)
        {
            switch (pattern)
            {
                case MonsterActiveAttackPattern.Line:
                case MonsterActiveAttackPattern.PiercingBeam:
                    DrawRelative(step, "range", "길이(m)");
                    DrawRelative(step, "width", "폭(m)");
                    break;
                case MonsterActiveAttackPattern.Cone:
                    DrawRelative(step, "range", "사거리(m)");
                    DrawRelative(step, "angle", "부채꼴 각도");
                    break;
                case MonsterActiveAttackPattern.SelfCircle:
                    DrawRelative(step, "radius", "반경(m)");
                    break;
                case MonsterActiveAttackPattern.FrontCircle:
                    DrawRelative(step, "forwardOffset", "전방 중심 거리(m)");
                    DrawRelative(step, "radius", "반경(m)");
                    break;
                case MonsterActiveAttackPattern.PiercingProjectile:
                case MonsterActiveAttackPattern.ExplosiveProjectile:
                    DrawRelative(step, "range", "최대 비행 거리(m)");
                    var formation = step.FindPropertyRelative("projectileFormation");
                    DrawProjectileFormation(formation);
                    if ((MonsterActiveProjectileFormation)formation.enumValueIndex == MonsterActiveProjectileFormation.Fan)
                    {
                        DrawRelative(step, "projectileCount", "투사체 개수");
                        DrawRelative(step, "projectileFanAngle", "부채꼴 각도");
                    }
                    DrawRelative(step, "projectileSpeed", "투사체 속도(m/s)");
                    DrawRelative(step, "projectileCollisionRadius", "충돌 반경(m)");
                    if (pattern == MonsterActiveAttackPattern.ExplosiveProjectile)
                    {
                        DrawRelative(step, "explosionRadius", "폭발 반경(m)");
                    }
                    break;
                case MonsterActiveAttackPattern.InstantMagic:
                    var target = step.FindPropertyRelative("instantMagicTarget");
                    DrawInstantMagicTarget(target);
                    DrawMagicDirection(step.FindPropertyRelative("magicDirection"));
                    if ((MonsterActiveInstantMagicTarget)target.enumValueIndex ==
                        MonsterActiveInstantMagicTarget.TargetArea)
                    {
                        DrawRelative(step, "radius", "범위 반경(m)");
                    }
                    break;
            }
        }

        private void DrawActivePresentationContract(
            SerializedProperty step,
            MonsterActiveAttackPattern pattern)
        {
            var slots = step.FindPropertyRelative("presentationSlots");
            GUILayout.Label("4. 몬스터 고유 VFX/SFX 공간 계약", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "이 Step이 사용할 연출 종류·발생 시점·기준 위치만 정의합니다. 실제 VFX와 SFX는 Monster Maker에서 몬스터별로 연결합니다.",
                MessageType.Info);

            for (var index = 0; index < slots.arraySize; index++)
            {
                var slot = slots.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var title = slot.FindPropertyRelative("displayName").stringValue;
                        slot.isExpanded = GUILayout.Toggle(
                            slot.isExpanded,
                            $"VFX 공간 {index + 1:00} · {(string.IsNullOrWhiteSpace(title) ? "이름 없음" : title)}",
                            EditorStyles.foldout,
                            GUILayout.MinWidth(0f),
                            GUILayout.ExpandWidth(true));
                        using (new EditorGUI.DisabledScope(index == 0))
                        {
                            if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(26f)))
                            {
                                slots.MoveArrayElement(index, index - 1);
                                GUIUtility.ExitGUI();
                            }
                        }
                        using (new EditorGUI.DisabledScope(index >= slots.arraySize - 1))
                        {
                            if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(26f)))
                            {
                                slots.MoveArrayElement(index, index + 1);
                                GUIUtility.ExitGUI();
                            }
                        }
                        if (GUILayout.Button("복제", EditorStyles.miniButtonMid, GUILayout.Width(40f)))
                        {
                            slots.InsertArrayElementAtIndex(index + 1);
                            var clone = slots.GetArrayElementAtIndex(index + 1);
                            clone.FindPropertyRelative("slotId").stringValue = BuildNextPresentationSlotId(slots);
                            clone.FindPropertyRelative("displayName").stringValue = title + " 복제";
                            GUIUtility.ExitGUI();
                        }

                        var previous = GUI.backgroundColor;
                        GUI.backgroundColor = Color.Lerp(Color.white, MonsterWorkshopVisualTheme.DangerColor, 0.55f);
                        var remove = GUILayout.Button("삭제", EditorStyles.miniButtonRight, GUILayout.Width(40f));
                        if (Event.current.type == EventType.Repaint)
                        {
                            lastPresentationHeaderRightmostRect = GUILayoutUtility.GetLastRect();
                        }
                        GUI.backgroundColor = previous;
                        if (remove)
                        {
                            slots.DeleteArrayElementAtIndex(index);
                            GUIUtility.ExitGUI();
                        }
                    }

                    if (slot.isExpanded)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(slot.FindPropertyRelative("slotId"), new GUIContent("공간 ID"));
                        EditorGUILayout.PropertyField(slot.FindPropertyRelative("displayName"), new GUIContent("표시 이름"));
                        DrawPresentationEvent(slot.FindPropertyRelative("timing"));
                        DrawPresentationAnchor(slot.FindPropertyRelative("anchor"));
                        EditorGUILayout.PropertyField(slot.FindPropertyRelative("description"), new GUIContent("제작 메모"));
                        DrawPresentationCompatibilityNotice(step, pattern, slot);
                        EditorGUI.indentLevel--;
                    }
                }
            }

            if (slots.arraySize == 0)
            {
                GUILayout.Label(
                    "연출 공간이 없습니다. 피해 판정은 정상 동작하며 VFX/SFX만 재생되지 않습니다.",
                    EditorStyles.wordWrappedMiniLabel);
            }
            if (MonsterWorkshopVisualTheme.DrawTintedButton(
                    new GUIContent("+ VFX 공간 추가"),
                    MonsterWorkshopVisualTheme.PrimaryColor,
                    26f))
            {
                ShowAddPresentationSlotMenu(slots);
            }
        }

        private static void DrawPresentationEvent(SerializedProperty property)
        {
            var values = (MonsterActivePresentationEvent[])Enum.GetValues(typeof(MonsterActivePresentationEvent));
            var labels = values.Select(GetPresentationEventLabel).ToArray();
            var current = Mathf.Max(0, Array.IndexOf(values, (MonsterActivePresentationEvent)property.enumValueIndex));
            property.enumValueIndex = (int)values[EditorGUILayout.Popup("발생 시점", current, labels)];
        }

        private static void DrawPresentationAnchor(SerializedProperty property)
        {
            var values = (MonsterActivePresentationAnchor[])Enum.GetValues(typeof(MonsterActivePresentationAnchor));
            var labels = values.Select(GetPresentationAnchorLabel).ToArray();
            var current = Mathf.Max(0, Array.IndexOf(values, (MonsterActivePresentationAnchor)property.enumValueIndex));
            property.enumValueIndex = (int)values[EditorGUILayout.Popup("기준 위치", current, labels)];
        }

        private static void DrawPresentationCompatibilityNotice(
            SerializedProperty step,
            MonsterActiveAttackPattern pattern,
            SerializedProperty slot)
        {
            var timing = (MonsterActivePresentationEvent)slot.FindPropertyRelative("timing").enumValueIndex;
            var projectileOrBeam = pattern == MonsterActiveAttackPattern.PiercingProjectile ||
                                   pattern == MonsterActiveAttackPattern.ExplosiveProjectile ||
                                   pattern == MonsterActiveAttackPattern.PiercingBeam;
            if (timing == MonsterActivePresentationEvent.Travel && !projectileOrBeam)
            {
                EditorGUILayout.HelpBox(
                    "현재 공격 형태에는 이동체/빔 구간이 없어 이 공간이 재생되지 않습니다.",
                    MessageType.Warning);
            }
            if ((timing == MonsterActivePresentationEvent.TeleportExit ||
                 timing == MonsterActivePresentationEvent.TeleportEnter) &&
                !step.FindPropertyRelative("teleportBeforeAttack").boolValue)
            {
                EditorGUILayout.HelpBox(
                    "현재 Step은 순간이동을 사용하지 않아 이 공간이 재생되지 않습니다.",
                    MessageType.Warning);
            }
        }

        private static void ShowAddPresentationSlotMenu(SerializedProperty slots)
        {
            var menu = new GenericMenu();
            foreach (MonsterActivePresentationEvent timing in Enum.GetValues(typeof(MonsterActivePresentationEvent)))
            {
                var captured = timing;
                menu.AddItem(new GUIContent(GetPresentationEventLabel(timing)), false,
                    () => AddPresentationSlot(slots, captured));
            }
            menu.ShowAsContext();
        }

        private static void AddPresentationSlot(
            SerializedProperty slots,
            MonsterActivePresentationEvent timing)
        {
            var index = slots.arraySize;
            slots.InsertArrayElementAtIndex(index);
            var slot = slots.GetArrayElementAtIndex(index);
            slot.FindPropertyRelative("slotId").stringValue = BuildNextPresentationSlotId(slots);
            slot.FindPropertyRelative("displayName").stringValue = GetPresentationEventLabel(timing);
            slot.FindPropertyRelative("timing").enumValueIndex = (int)timing;
            slot.FindPropertyRelative("anchor").enumValueIndex = (int)GetDefaultPresentationAnchor(timing);
            slot.FindPropertyRelative("description").stringValue = string.Empty;
            slot.isExpanded = true;
        }

        private static string BuildNextPresentationSlotId(SerializedProperty slots)
        {
            for (var number = 1; ; number++)
            {
                var candidate = $"vfx_{number:00}";
                var exists = false;
                for (var index = 0; index < slots.arraySize; index++)
                {
                    if (string.Equals(
                            slots.GetArrayElementAtIndex(index).FindPropertyRelative("slotId").stringValue,
                            candidate,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists) return candidate;
            }
        }

        private void DrawSharedFeelOptions()
        {
            var feel = serializedProfile.FindProperty("impactFeel");
            if (feel == null)
            {
                EditorGUILayout.HelpBox("공통 FEEL 데이터 계약을 찾을 수 없습니다.", MessageType.Error);
                return;
            }

            GUILayout.Label("6. 액티브 공통 FEEL 타격감", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "기본공격 조립소와 동일하게 실제 명중 FEEL 프리셋 하나만 사용합니다. 모든 Step이 공유하며, 한 Step의 다중 대상에서는 한 번만 재생합니다.",
                MessageType.Info);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("실제 명중 FEEL 프로필", EditorStyles.miniBoldLabel);
                var prefab = feel.FindPropertyRelative("prefab");
                var current = prefab.objectReferenceValue as GameObject;
                var options = BasicAttackFeelPresetUtility.LoadFeelProfileOptions(current);
                var labels = options.Select(option => option.Label).ToArray();
                var currentIndex = 0;
                for (var index = 0; index < options.Length; index++)
                {
                    if (options[index].Profile == current)
                    {
                        currentIndex = index;
                        break;
                    }
                }

                var selectedIndex = EditorGUILayout.Popup("FEEL 프로필", currentIndex, labels);
                if (selectedIndex != currentIndex && selectedIndex >= 0 && selectedIndex < options.Length)
                {
                    current = options[selectedIndex].Profile;
                    prefab.objectReferenceValue = current;
                    ApplyFeelProfileDefaults(feel, current);
                }

                DrawFeelPrefabStatus(current);
                GUILayout.Label(
                    "각 Step에서 실제 피해가 처음 적용되는 대상 지점에 공통 타격감을 재생합니다.",
                    EditorStyles.wordWrappedMiniLabel);
                if (current != null)
                {
                    var metadata = current.GetComponent<BasicAttackFeelProfileMetadata>();
                    var lifetime = metadata?.Lifetime ?? feel.FindPropertyRelative("lifetime").floatValue;
                    var position = metadata?.LocalPosition ?? feel.FindPropertyRelative("localPosition").vector3Value;
                    var euler = metadata?.LocalEulerAngles ?? feel.FindPropertyRelative("localEulerAngles").vector3Value;
                    var scale = metadata?.Scale ?? feel.FindPropertyRelative("scale").floatValue;
                    GUILayout.Label(
                        $"현재 프로필 값 · 수명 {lifetime:0.00}s · 위치 {position} · 회전 {euler} · 배율 {scale:0.00}",
                        EditorStyles.wordWrappedMiniLabel);
                }
                if (MonsterWorkshopVisualTheme.DrawTintedButton(
                        new GUIContent("FEEL 연구소 열기"),
                        MonsterWorkshopVisualTheme.FeelColor,
                        26f))
                {
                    BasicAttackFeelPresetUtility.OpenFormalLab();
                }
            }
        }

        private static void ApplyFeelProfileDefaults(SerializedProperty feel, GameObject profile)
        {
            var metadata = profile != null ? profile.GetComponent<BasicAttackFeelProfileMetadata>() : null;
            feel.FindPropertyRelative("lifetime").floatValue = metadata?.Lifetime ?? 0.85f;
            feel.FindPropertyRelative("localPosition").vector3Value = metadata?.LocalPosition ?? Vector3.zero;
            feel.FindPropertyRelative("localEulerAngles").vector3Value = metadata?.LocalEulerAngles ?? Vector3.zero;
            feel.FindPropertyRelative("scale").floatValue = metadata?.Scale ?? 1f;
        }

        private static void DrawFeelPrefabStatus(GameObject prefab)
        {
            if (prefab == null) return;
            var runtime = prefab.GetComponent(typeof(IBasicAttackFeelRuntime)) as IBasicAttackFeelRuntime;
            if (runtime == null)
            {
                EditorGUILayout.HelpBox("선택한 Prefab 루트에 FEEL 런타임 어댑터가 없습니다.", MessageType.Error);
            }
            else if (!runtime.IsBasicAttackFeelConfigured)
            {
                EditorGUILayout.HelpBox("선택한 FEEL 프리셋의 MMF_Player 연결이 완료되지 않았습니다.", MessageType.Error);
            }
            else
            {
                EditorGUILayout.HelpBox("FEEL 공통 타격감 계약 통과", MessageType.None);
            }
        }

        private void DrawHitEffects(SerializedProperty effects)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"타격 효과 · {effects.arraySize}개", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+ 효과", GUILayout.Width(72f))) ShowAddEffectMenu(effects);
            }
            if (effects.arraySize == 0)
            {
                GUILayout.Label("선택 사항 · 에어본/넉백/기절/출혈/둔화를 함께 조립할 수 있습니다.",
                    EditorStyles.wordWrappedMiniLabel);
                return;
            }

            for (var index = 0; index < effects.arraySize; index++)
            {
                var effect = effects.GetArrayElementAtIndex(index);
                var typeProperty = effect.FindPropertyRelative("type");
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawHitEffectType(typeProperty, $"효과 {index + 1}");
                        var remove = GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(24f));
                        if (Event.current.type == EventType.Repaint)
                        {
                            lastHitEffectRightmostRect = GUILayoutUtility.GetLastRect();
                        }
                        if (remove)
                        {
                            effects.DeleteArrayElementAtIndex(index);
                            GUIUtility.ExitGUI();
                        }
                    }
                    var type = (MonsterActiveHitEffectType)typeProperty.enumValueIndex;
                    switch (type)
                    {
                        case MonsterActiveHitEffectType.Knockback:
                            DrawRelative(effect, "magnitude", "밀어내는 거리(m)");
                            DrawRelative(effect, "duration", "밀림 시간(초)");
                            DrawRelative(effect, "secondaryMagnitude", "후경직(초)");
                            break;
                        case MonsterActiveHitEffectType.Airborne:
                            DrawRelative(effect, "magnitude", "떠오르는 높이(m)");
                            DrawRelative(effect, "duration", "체공 시간(초)");
                            break;
                        case MonsterActiveHitEffectType.Stun:
                            DrawRelative(effect, "duration", "기절 시간(초)");
                            break;
                        case MonsterActiveHitEffectType.Bleed:
                            DrawRelative(effect, "magnitude", "틱당 공격력 배율");
                            DrawRelative(effect, "duration", "출혈 지속(초)");
                            DrawRelative(effect, "tickInterval", "피해 간격(초)");
                            break;
                        case MonsterActiveHitEffectType.Slow:
                            DrawRelative(effect, "magnitude", "감속률(0~1)");
                            DrawRelative(effect, "duration", "감속 지속(초)");
                            break;
                    }
                }
            }
        }

        private static void ShowAddStepMenu(SerializedProperty steps)
        {
            var menu = new GenericMenu();
            foreach (MonsterActiveAttackPattern pattern in Enum.GetValues(typeof(MonsterActiveAttackPattern)))
            {
                var captured = pattern;
                menu.AddItem(new GUIContent(GetPatternLabel(pattern)), false, () => AddStep(steps, captured));
            }
            menu.ShowAsContext();
        }

        private static void AddStep(SerializedProperty steps, MonsterActiveAttackPattern pattern)
        {
            var index = steps.arraySize;
            steps.InsertArrayElementAtIndex(index);
            var step = steps.GetArrayElementAtIndex(index);
            step.FindPropertyRelative("stepId").stringValue = BuildNextStepId(steps);
            step.FindPropertyRelative("displayName").stringValue = GetPatternLabel(pattern);
            step.FindPropertyRelative("delayAfterPrevious").floatValue = index == 0 ? 0f : 0.12f;
            step.FindPropertyRelative("targetPolicy").enumValueIndex = (int)MonsterActiveTargetPolicy.SameTarget;
            step.FindPropertyRelative("teleportBeforeAttack").boolValue = false;
            step.FindPropertyRelative("teleportFrontDistance").floatValue = 1f;
            step.FindPropertyRelative("pattern").enumValueIndex = (int)pattern;
            step.FindPropertyRelative("progression").enumValueIndex = (int)MonsterActiveAttackProgression.Instant;
            step.FindPropertyRelative("damageMultiplier").floatValue = 1f;
            step.FindPropertyRelative("maxTargets").intValue = pattern == MonsterActiveAttackPattern.InstantMagic ? 1 : 8;
            step.FindPropertyRelative("range").floatValue = 4f;
            step.FindPropertyRelative("width").floatValue = 1.2f;
            step.FindPropertyRelative("radius").floatValue = 1.8f;
            step.FindPropertyRelative("forwardOffset").floatValue = 1.5f;
            step.FindPropertyRelative("angle").floatValue = 70f;
            step.FindPropertyRelative("progressionDuration").floatValue = 0.25f;
            step.FindPropertyRelative("telegraphDelay").floatValue = 0.12f;
            step.FindPropertyRelative("visualDuration").floatValue = 0.8f;
            step.FindPropertyRelative("projectileFormation").enumValueIndex = (int)MonsterActiveProjectileFormation.Single;
            step.FindPropertyRelative("projectileCount").intValue = 1;
            step.FindPropertyRelative("projectileFanAngle").floatValue = 50f;
            step.FindPropertyRelative("projectileSpeed").floatValue = 10f;
            step.FindPropertyRelative("projectileCollisionRadius").floatValue = 0.25f;
            step.FindPropertyRelative("explosionRadius").floatValue = 1.8f;
            step.FindPropertyRelative("instantMagicTarget").enumValueIndex = (int)MonsterActiveInstantMagicTarget.SingleTarget;
            step.FindPropertyRelative("magicDirection").enumValueIndex = (int)MonsterActiveMagicDirection.GroundUp;
            step.FindPropertyRelative("hitEffects").arraySize = 0;
            var presentationSlots = step.FindPropertyRelative("presentationSlots");
            presentationSlots.arraySize = 0;
            AddPresentationSlot(presentationSlots, MonsterActivePresentationEvent.Telegraph);
            AddPresentationSlot(presentationSlots, MonsterActivePresentationEvent.Launch);
            if (pattern == MonsterActiveAttackPattern.PiercingProjectile ||
                pattern == MonsterActiveAttackPattern.ExplosiveProjectile ||
                pattern == MonsterActiveAttackPattern.PiercingBeam)
            {
                AddPresentationSlot(presentationSlots, MonsterActivePresentationEvent.Travel);
            }
            AddPresentationSlot(presentationSlots, MonsterActivePresentationEvent.Impact);
            step.isExpanded = true;
        }

        private static void ShowAddEffectMenu(SerializedProperty effects)
        {
            var menu = new GenericMenu();
            foreach (MonsterActiveHitEffectType type in Enum.GetValues(typeof(MonsterActiveHitEffectType)))
            {
                var captured = type;
                menu.AddItem(new GUIContent(GetEffectLabel(type)), false, () => AddEffect(effects, captured));
            }
            menu.ShowAsContext();
        }

        private static void AddEffect(SerializedProperty effects, MonsterActiveHitEffectType type)
        {
            var index = effects.arraySize;
            effects.InsertArrayElementAtIndex(index);
            var effect = effects.GetArrayElementAtIndex(index);
            effect.FindPropertyRelative("type").enumValueIndex = (int)type;
            effect.FindPropertyRelative("magnitude").floatValue = type switch
            {
                MonsterActiveHitEffectType.Airborne => 0.7f,
                MonsterActiveHitEffectType.Bleed => 0.12f,
                MonsterActiveHitEffectType.Slow => 0.3f,
                _ => 0.3f
            };
            effect.FindPropertyRelative("duration").floatValue = type switch
            {
                MonsterActiveHitEffectType.Stun => 1f,
                MonsterActiveHitEffectType.Bleed => 3f,
                MonsterActiveHitEffectType.Slow => 2f,
                _ => 0.45f
            };
            effect.FindPropertyRelative("secondaryMagnitude").floatValue = 0.12f;
            effect.FindPropertyRelative("tickInterval").floatValue = 0.5f;
        }

        private void DrawValidation()
        {
            if (serializedProfile.ApplyModifiedProperties())
            {
                OnWorkingProfileChanged();
            }
            if (profile.TryValidate(out var error))
            {
                EditorGUILayout.HelpBox(
                    $"사용 가능 · Step {profile.Steps.Count}개 · 예상 연출 {profile.EstimateDuration():0.##}초",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }

        private void DrawSaveAndAssignControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (MonsterWorkshopVisualTheme.DrawTintedButton(
                        new GUIContent("새 프리셋으로 저장"),
                        MonsterWorkshopVisualTheme.PrimaryColor,
                        30f))
                {
                    SaveAsNew();
                }
                using (new EditorGUI.DisabledScope(loadedProfile == null))
                {
                    var update = MonsterWorkshopVisualTheme.DrawTintedButton(
                        new GUIContent("현재 프리셋에 저장"),
                        MonsterWorkshopVisualTheme.PreviewColor,
                        30f);
                    if (Event.current.type == EventType.Repaint)
                    {
                        lastSaveRightmostRect = GUILayoutUtility.GetLastRect();
                    }
                    if (update)
                    {
                        UpdateLoaded();
                    }
                }
            }

            GUILayout.Space(4f);
            using (new EditorGUI.DisabledScope(originDraft == null || loadedProfile == null || workCopyDirty))
            {
                var label = originDraft == null
                    ? "몬스터메이커에서 열면 바로 배정할 수 있습니다"
                    : workCopyDirty
                        ? "먼저 저장해야 현재 몬스터에 배정할 수 있습니다"
                        : $"[{loadedProfile?.ProfileId}] → {originDraft.MonsterId}에게 배정";
                if (MonsterWorkshopVisualTheme.DrawTintedButton(
                        new GUIContent(label),
                        MonsterWorkshopVisualTheme.FeelColor,
                        32f))
                {
                    AssignLoadedToOrigin();
                }
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                EditorGUILayout.HelpBox(message, messageType);
            }
        }

        private void SetProfile(MonsterActiveAttackProfile target)
        {
            if (target == null) StartBlank();
            else LoadProfile(target);
        }

        private void StartBlank()
        {
            RefreshProfiles();
            DisposeWorkingProfile();
            profile = CreateInstance<MonsterActiveAttackProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            var step = new MonsterActiveAttackStep();
            step.EditorConfigure("step_01", "일자 피해", MonsterActiveAttackPattern.Line);
            profile.EditorConfigure(
                FindNextProfileId(),
                "새 공격 액티브",
                "Step을 조립해 공격 흐름을 완성하세요.",
                new[] { step });
            loadedProfile = null;
            serializedProfile = new SerializedObject(profile);
            workCopyDirty = true;
            message = "기존 프리셋을 건드리지 않는 빈 공격 액티브 작업 사본에서 시작했습니다.";
            messageType = MessageType.Info;
            assemblerScroll = Vector2.zero;
            selectedPreviewStep = 0;
            preview ??= new MonsterActiveAttackWorkshopPreview();
            preview.SetProfile(profile);
            Repaint();
        }

        private void LoadProfile(MonsterActiveAttackProfile target)
        {
            if (target == null) return;
            DisposeWorkingProfile();
            profile = CreateInstance<MonsterActiveAttackProfile>();
            EditorUtility.CopySerialized(target, profile);
            profile.hideFlags = HideFlags.HideAndDontSave;
            loadedProfile = target;
            serializedProfile = new SerializedObject(profile);
            workCopyDirty = false;
            message = $"프리셋을 작업 사본으로 불러왔습니다: [{target.ProfileId}] {target.DisplayName}";
            messageType = MessageType.Info;
            assemblerScroll = Vector2.zero;
            selectedPreviewStep = 0;
            preview ??= new MonsterActiveAttackWorkshopPreview();
            preview.SetProfile(profile);
            Repaint();
        }

        private void OnWorkingProfileChanged()
        {
            workCopyDirty = true;
            preview?.Refresh();
            Repaint();
        }

        private void DisposeWorkingProfile()
        {
            serializedProfile = null;
            if (profile != null && !EditorUtility.IsPersistent(profile)) DestroyImmediate(profile);
            profile = null;
        }

        private void SyncOriginDraftAuthoring()
        {
            if (originDraft == null || originDraft.ActiveAttackProfile != loadedProfile) return;
            Undo.RecordObject(originDraft, "액티브 연출 계약 동기화");
            originDraft.EditorSyncActiveAttackAuthoring();
            EditorUtility.SetDirty(originDraft);
            AssetDatabase.SaveAssetIfDirty(originDraft);
        }

        private void SaveAsNew()
        {
            serializedProfile?.ApplyModifiedProperties();
            if (!MonsterActiveAttackAuthoringService.TryCreate(
                    profile,
                    out var asset,
                    out var path,
                    out var error))
            {
                SetError(error);
                return;
            }
            loadedProfile = asset;
            workCopyDirty = false;
            message = $"새 프리셋을 저장했습니다: {path}";
            messageType = MessageType.Info;
            RefreshProfiles();
        }

        private void UpdateLoaded()
        {
            if (loadedProfile == null) return;
            serializedProfile?.ApplyModifiedProperties();
            if (!ValidateWorkingCopy(loadedProfile, out var error))
            {
                SetError(error);
                return;
            }

            var usageCount = profileUsages.TryGetValue(loadedProfile, out var usage) ? usage : 0;
            if (usageCount > 0 && !EditorUtility.DisplayDialog(
                    "공유 프리셋 업데이트",
                    $"이 프리셋을 {usageCount}마리가 사용 중입니다. 저장하면 모두에게 적용됩니다.",
                    "업데이트",
                    "취소"))
            {
                return;
            }

            if (!MonsterActiveAttackAuthoringService.TryUpdate(profile, loadedProfile, out error))
            {
                SetError(error);
                return;
            }
            workCopyDirty = false;
            message = $"프리셋을 업데이트했습니다: [{loadedProfile.ProfileId}] {loadedProfile.DisplayName}";
            messageType = MessageType.Info;
            SyncOriginDraftAuthoring();
            RefreshProfiles();
        }

        private void ForkLoadedAsNew()
        {
            if (loadedProfile == null || serializedProfile == null) return;
            loadedProfile = null;
            serializedProfile.UpdateIfRequiredOrScript();
            serializedProfile.FindProperty("profileId").stringValue = FindNextProfileId();
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();
            workCopyDirty = true;
            message = $"새 프리셋 작업 사본으로 분기했습니다. 새 ID는 {profile.ProfileId}입니다.";
            messageType = MessageType.Info;
            preview?.Refresh();
            Repaint();
        }

        private void AssignLoadedToOrigin()
        {
            if (originDraft == null || loadedProfile == null || workCopyDirty) return;
            Undo.RecordObject(originDraft, "공격 액티브 프리셋 배정");
            originDraft.EditorSetActiveAttackProfile(loadedProfile);
            originDraft.EditorSyncActiveAttackAuthoring();
            EditorUtility.SetDirty(originDraft);
            AssetDatabase.SaveAssetIfDirty(originDraft);
            message = $"{originDraft.MonsterId}에게 [{loadedProfile.ProfileId}]을 배정했습니다.";
            messageType = MessageType.Info;
            RefreshProfiles();
            PresetAssigned?.Invoke();
        }

        private bool ValidateWorkingCopy(MonsterActiveAttackProfile excluded, out string error)
        {
            return MonsterActiveAttackAuthoringService.TryValidate(profile, excluded, out error);
        }

        private string FindNextProfileId()
        {
            for (var number = 1; ; number++)
            {
                var candidate = $"active_custom_{number:00}";
                if (profiles.All(item => item == null || !string.Equals(
                        item.ProfileId,
                        candidate,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    return candidate;
                }
            }
        }

        private void SetError(string error)
        {
            message = error;
            messageType = MessageType.Error;
        }

        public static MonsterActiveAttackProfile CreateProfileAtPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("프로필 경로가 비어 있습니다.", nameof(path));
            MonsterActiveAttackAuthoringService.EnsureProfileFolder();
            var profileAsset = ScriptableObject.CreateInstance<MonsterActiveAttackProfile>();
            var id = System.IO.Path.GetFileNameWithoutExtension(path)
                .Replace("AAP_", string.Empty)
                .ToLowerInvariant();
            var step = new MonsterActiveAttackStep();
            step.EditorConfigure("step_01", "일자 피해", MonsterActiveAttackPattern.Line);
            profileAsset.EditorConfigure(id, "새 공격 액티브", "Step을 조립해 공격 흐름을 완성하세요.", new[] { step });
            AssetDatabase.CreateAsset(profileAsset, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssetIfDirty(profileAsset);
            Selection.activeObject = profileAsset;
            return profileAsset;
        }

        private static string BuildNextStepId(SerializedProperty steps)
        {
            for (var number = 1; ; number++)
            {
                var candidate = $"step_{number:00}";
                var exists = false;
                for (var index = 0; index < steps.arraySize; index++)
                {
                    if (string.Equals(
                            steps.GetArrayElementAtIndex(index).FindPropertyRelative("stepId").stringValue,
                            candidate,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists) return candidate;
            }
        }

        private static void DrawRelative(SerializedProperty parent, string name, string label)
        {
            EditorGUILayout.PropertyField(parent.FindPropertyRelative(name), new GUIContent(label));
        }

        private void EnsureStyles()
        {
            stepHeaderStyle ??= new GUIStyle(EditorStyles.foldoutHeader)
            {
                fontSize = 12,
                clipping = TextClipping.Clip
            };
        }

        private static string GetProgressionLabel(MonsterActiveAttackProgression value) => value switch
        {
            MonsterActiveAttackProgression.Instant => "한 번에",
            MonsterActiveAttackProgression.Forward => "앞으로 순차",
            MonsterActiveAttackProgression.LeftToRight => "왼쪽 → 오른쪽",
            MonsterActiveAttackProgression.RightToLeft => "오른쪽 → 왼쪽",
            MonsterActiveAttackProgression.Outward => "바깥쪽으로 순차",
            _ => value.ToString()
        };

        private static string GetTargetPolicyLabel(MonsterActiveTargetPolicy value) => value switch
        {
            MonsterActiveTargetPolicy.SameTarget => "앞 Step과 같은 대상",
            MonsterActiveTargetPolicy.DifferentTarget => "앞 Step과 다른 대상",
            _ => value.ToString()
        };

        private static string GetProjectileFormationLabel(MonsterActiveProjectileFormation value) => value switch
        {
            MonsterActiveProjectileFormation.Single => "단일 발사",
            MonsterActiveProjectileFormation.Fan => "부채꼴 발사",
            _ => value.ToString()
        };

        private static string GetInstantMagicTargetLabel(MonsterActiveInstantMagicTarget value) => value switch
        {
            MonsterActiveInstantMagicTarget.SingleTarget => "단일 대상",
            MonsterActiveInstantMagicTarget.TargetArea => "대상 중심 범위",
            _ => value.ToString()
        };

        private static string GetMagicDirectionLabel(MonsterActiveMagicDirection value) => value switch
        {
            MonsterActiveMagicDirection.GroundUp => "바닥에서 위로",
            MonsterActiveMagicDirection.SkyDown => "위에서 아래로",
            _ => value.ToString()
        };

        private static string GetPresentationEventLabel(MonsterActivePresentationEvent value) => value switch
        {
            MonsterActivePresentationEvent.Telegraph => "판정 예고",
            MonsterActivePresentationEvent.Launch => "공격 발동",
            MonsterActivePresentationEvent.Travel => "이동체 / 빔",
            MonsterActivePresentationEvent.Impact => "실제 타격",
            MonsterActivePresentationEvent.TeleportExit => "순간이동 출발",
            MonsterActivePresentationEvent.TeleportEnter => "순간이동 도착",
            _ => value.ToString()
        };

        private static string GetPresentationAnchorLabel(MonsterActivePresentationAnchor value) => value switch
        {
            MonsterActivePresentationAnchor.CasterRoot => "시전자 중심",
            MonsterActivePresentationAnchor.AttackOrigin => "공격 원점",
            MonsterActivePresentationAnchor.TargetPoint => "대상 지점",
            _ => value.ToString()
        };

        private static MonsterActivePresentationAnchor GetDefaultPresentationAnchor(
            MonsterActivePresentationEvent value) => value switch
        {
            MonsterActivePresentationEvent.Telegraph => MonsterActivePresentationAnchor.TargetPoint,
            MonsterActivePresentationEvent.Impact => MonsterActivePresentationAnchor.TargetPoint,
            MonsterActivePresentationEvent.TeleportExit => MonsterActivePresentationAnchor.CasterRoot,
            MonsterActivePresentationEvent.TeleportEnter => MonsterActivePresentationAnchor.CasterRoot,
            _ => MonsterActivePresentationAnchor.AttackOrigin
        };

        private static string GetPatternLabel(MonsterActiveAttackPattern value) => value switch
        {
            MonsterActiveAttackPattern.Line => "일자 피해",
            MonsterActiveAttackPattern.Cone => "부채꼴 피해",
            MonsterActiveAttackPattern.SelfCircle => "내 주변 원형",
            MonsterActiveAttackPattern.FrontCircle => "내 앞 원형",
            MonsterActiveAttackPattern.PiercingProjectile => "관통 투사체",
            MonsterActiveAttackPattern.ExplosiveProjectile => "폭발 투사체",
            MonsterActiveAttackPattern.PiercingBeam => "관통 빔",
            MonsterActiveAttackPattern.InstantMagic => "즉발 마법",
            _ => value.ToString()
        };

        private static string GetEffectLabel(MonsterActiveHitEffectType value) => value switch
        {
            MonsterActiveHitEffectType.Knockback => "넉백",
            MonsterActiveHitEffectType.Airborne => "에어본",
            MonsterActiveHitEffectType.Stun => "기절",
            MonsterActiveHitEffectType.Bleed => "출혈",
            MonsterActiveHitEffectType.Slow => "둔화",
            _ => value.ToString()
        };
    }
}
