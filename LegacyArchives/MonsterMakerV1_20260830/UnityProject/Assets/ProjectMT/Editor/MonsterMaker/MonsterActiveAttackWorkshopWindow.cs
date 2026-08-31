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
        private const string MenuPath = "JC Tool/Monster/Legacy/액티브 스킬 조립소 V1";
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
            {
                if (!staleWindow.TryResolveUnsavedChanges("다른 공격 액티브 조립소를 열기"))
                {
                    staleWindow.Focus();
                    return;
                }
                staleWindow.Close();
            }
            var window = CreateInstance<MonsterActiveAttackWorkshopWindow>();
            window.titleContent = new GUIContent("액티브 스킬 조립소");
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

        public override void SaveChanges()
        {
            if (loadedProfile == null) SaveAsNew();
            else UpdateLoaded();
            if (!workCopyDirty) base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            SetWorkCopyDirty(false);
            base.DiscardChanges();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("액티브 스킬 조립소");
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
                "액티브 스킬 조립소",
                "공격형과 효과형이 같은 기력·모션·발동 연출 흐름을 사용합니다");
            DrawActiveModeToolbar();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLibrary();
                DrawAssembler();
                DrawPreview();
            }
        }

        private void DrawActiveModeToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var previous = GUI.backgroundColor;
                GUI.backgroundColor = Color.Lerp(Color.white, MonsterWorkshopVisualTheme.PreviewColor, 0.55f);
                GUILayout.Button("공격형", GUILayout.Height(30f));
                GUI.backgroundColor = previous;
                if (GUILayout.Button("효과형 · 지원 / 수호 / 디버프", GUILayout.Height(30f)))
                {
                    var draft = originDraft;
                    var effect = draft?.ActiveEffectProfile;
                    EditorApplication.delayCall += () =>
                    {
                        MonsterEffectActiveWorkshopWindow.OpenFor(effect, draft);
                        Close();
                    };
                    GUIUtility.ExitGUI();
                }
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
                    TryStartBlank();
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
                            TryLoadProfile(assigned);
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
                        TryLoadProfile(candidate);
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
                    TryStartBlank();
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
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(teleport, new GUIContent("공격 전 순간이동"));
                if (teleport.boolValue)
                {
                    EditorGUILayout.PropertyField(step.FindPropertyRelative("teleportFrontDistance"),
                        new GUIContent("타깃 앞 거리(m)"));
                }
                if (EditorGUI.EndChangeCheck())
                {
                    ReconcileStepContractAndCommit(index, "순간이동 설정");
                    GUIUtility.ExitGUI();
                }

                GUILayout.Space(5f);
                GUILayout.Label("2. 공격 방식", EditorStyles.miniBoldLabel);
                var pattern = step.FindPropertyRelative("pattern");
                EditorGUI.BeginChangeCheck();
                DrawPattern(pattern);
                if (EditorGUI.EndChangeCheck())
                {
                    ReconcileStepContractAndCommit(index, "공격 형태");
                    GUIUtility.ExitGUI();
                }
                DrawProgression(step, (MonsterActiveAttackPattern)pattern.enumValueIndex);

                GUILayout.Space(5f);
                GUILayout.Label("3. 판정 수치", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(step.FindPropertyRelative("damageMultiplier"),
                    new GUIContent("공격력 배율"));
                EditorGUILayout.PropertyField(step.FindPropertyRelative("maxTargets"),
                    new GUIContent("최대 타깃"));
                var magicTarget = step.FindPropertyRelative("instantMagicTarget");
                var previousMagicTarget = magicTarget.enumValueIndex;
                DrawPatternFields(step, (MonsterActiveAttackPattern)pattern.enumValueIndex);
                if ((MonsterActiveAttackPattern)pattern.enumValueIndex ==
                        MonsterActiveAttackPattern.InstantMagic &&
                    magicTarget.enumValueIndex != previousMagicTarget)
                {
                    ReconcileStepContractAndCommit(index, "즉발 마법 대상");
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.PropertyField(step.FindPropertyRelative("telegraphDelay"),
                    new GUIContent("예고 후 판정(초)"));
                EditorGUILayout.PropertyField(step.FindPropertyRelative("visualDuration"),
                    new GUIContent("연출 유지(초)"));

                GUILayout.Space(6f);
                GUILayout.Label("4. 타격 효과", EditorStyles.miniBoldLabel);
                DrawHitEffects(step.FindPropertyRelative("hitEffects"));
                GUILayout.Space(4f);
                DrawActivePresentationContract(step, (MonsterActiveAttackPattern)pattern.enumValueIndex);
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
                        MoveStepAndCommit(steps, index, index - 1);
                        GUIUtility.ExitGUI();
                    }
                }
                using (new EditorGUI.DisabledScope(index >= steps.arraySize - 1))
                {
                    if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(26f)))
                    {
                        MoveStepAndCommit(steps, index, index + 1);
                        GUIUtility.ExitGUI();
                    }
                }
                if (GUILayout.Button("복제", EditorStyles.miniButtonMid, GUILayout.Width(40f)))
                {
                    DuplicateStepAndCommit(steps, index, title);
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
                    DeleteStepAndCommit(steps, index);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void MoveStepAndCommit(SerializedProperty steps, int sourceIndex, int destinationIndex)
        {
            steps.MoveArrayElement(sourceIndex, destinationIndex);
            CommitStructuralChange(steps);
        }

        private void DuplicateStepAndCommit(SerializedProperty steps, int index, string title)
        {
            steps.InsertArrayElementAtIndex(index + 1);
            var clone = steps.GetArrayElementAtIndex(index + 1);
            clone.FindPropertyRelative("stepId").stringValue = BuildNextStepId(steps);
            clone.FindPropertyRelative("displayName").stringValue = title + " 복제";
            clone.isExpanded = true;
            CommitStructuralChange(steps);
        }

        private void DeleteStepAndCommit(SerializedProperty steps, int index)
        {
            steps.DeleteArrayElementAtIndex(index);
            CommitStructuralChange(steps);
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
            GUILayout.Label("5. 몬스터 고유 VFX/SFX 공간 계약", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "이 Step이 사용할 연출 종류·발생 시점·기준 위치만 정의합니다. 실제 VFX와 SFX는 Monster Maker에서 몬스터별로 연결합니다.",
                MessageType.Info);
            if (GUILayout.Button("공격 방식에 맞춰 VFX/SFX 공간 정리", GUILayout.Height(24f)))
            {
                var stepIndex = ParseArrayIndex(step.propertyPath);
                ReconcileStepContractAndCommit(stepIndex, "수동 정리");
                GUIUtility.ExitGUI();
            }

            for (var index = 0; index < slots.arraySize; index++)
            {
                var slot = slots.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var title = slot.FindPropertyRelative("displayName").stringValue;
                        var useDuration = slot.FindPropertyRelative("useDuration").boolValue;
                        var durationSuffix = useDuration
                            ? $" · {Mathf.Max(0.05f, slot.FindPropertyRelative("duration").floatValue):0.##}초 유지"
                            : string.Empty;
                        slot.isExpanded = GUILayout.Toggle(
                            slot.isExpanded,
                            $"VFX 공간 {index + 1:00} · {(string.IsNullOrWhiteSpace(title) ? "이름 없음" : title)}{durationSuffix}",
                            EditorStyles.foldout,
                            GUILayout.MinWidth(0f),
                            GUILayout.ExpandWidth(true));
                        using (new EditorGUI.DisabledScope(index == 0))
                        {
                            if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(26f)))
                            {
                                MovePresentationSlotAndCommit(slots, index, index - 1);
                                GUIUtility.ExitGUI();
                            }
                        }
                        using (new EditorGUI.DisabledScope(index >= slots.arraySize - 1))
                        {
                            if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(26f)))
                            {
                                MovePresentationSlotAndCommit(slots, index, index + 1);
                                GUIUtility.ExitGUI();
                            }
                        }
                        if (GUILayout.Button("복제", EditorStyles.miniButtonMid, GUILayout.Width(40f)))
                        {
                            DuplicatePresentationSlotAndCommit(slots, index, title);
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
                            DeletePresentationSlotAndCommit(slots, index);
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
                        var multiplicity = slot.FindPropertyRelative("multiplicity");
                        multiplicity.isExpanded = EditorGUILayout.Foldout(
                            multiplicity.isExpanded,
                            "고급 · 재생/부착/종료 정책",
                            true);
                        if (multiplicity.isExpanded)
                        {
                            EditorGUI.indentLevel++;
                            DrawPresentationMultiplicity(multiplicity);
                            DrawPresentationAttachment(slot.FindPropertyRelative("attachment"));
                            DrawPresentationEndPolicy(slot.FindPropertyRelative("endPolicy"));
                            EditorGUI.indentLevel--;
                        }
                        var useDuration = slot.FindPropertyRelative("useDuration");
                        var endPolicy = (MonsterActivePresentationEndPolicy)slot
                            .FindPropertyRelative("endPolicy").enumValueIndex;
                        if (endPolicy == MonsterActivePresentationEndPolicy.Timed)
                        {
                            EditorGUILayout.PropertyField(
                                useDuration,
                                new GUIContent("지속시간 사용", "루프 VFX를 지정한 시간 동안 유지한 뒤 풀로 반환합니다."));
                            if (useDuration.boolValue)
                            {
                                var duration = slot.FindPropertyRelative("duration");
                                EditorGUILayout.PropertyField(
                                    duration,
                                    new GUIContent("지속 시간(초)", "피해 반복과 무관한 VFX 재생 유지 시간입니다."));
                                duration.floatValue = Mathf.Max(0.05f, duration.floatValue);
                            }
                        }
                        else
                        {
                            useDuration.boolValue = false;
                            GUILayout.Label(
                                $"수명은 [{GetPresentationEndPolicyLabel(endPolicy)}] 시점에 자동 정리됩니다.",
                                EditorStyles.wordWrappedMiniLabel);
                        }
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
                ShowAddPresentationSlotMenu(step, slots);
            }
        }

        private void MovePresentationSlotAndCommit(
            SerializedProperty slots,
            int sourceIndex,
            int destinationIndex)
        {
            slots.MoveArrayElement(sourceIndex, destinationIndex);
            CommitStructuralChange(slots);
        }

        private void DuplicatePresentationSlotAndCommit(SerializedProperty slots, int index, string title)
        {
            slots.InsertArrayElementAtIndex(index + 1);
            var clone = slots.GetArrayElementAtIndex(index + 1);
            clone.FindPropertyRelative("slotId").stringValue = BuildNextPresentationSlotId(slots);
            clone.FindPropertyRelative("displayName").stringValue = title + " 복제";
            CommitStructuralChange(slots);
        }

        private void DeletePresentationSlotAndCommit(SerializedProperty slots, int index)
        {
            slots.DeleteArrayElementAtIndex(index);
            CommitStructuralChange(slots);
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

        private static void DrawPresentationMultiplicity(SerializedProperty property)
        {
            var values = (MonsterActivePresentationMultiplicity[])Enum.GetValues(
                typeof(MonsterActivePresentationMultiplicity));
            var labels = values.Select(GetPresentationMultiplicityLabel).ToArray();
            var current = Mathf.Max(0, Array.IndexOf(values,
                (MonsterActivePresentationMultiplicity)property.enumValueIndex));
            property.enumValueIndex = (int)values[EditorGUILayout.Popup("재생 횟수 기준", current, labels)];
        }

        private static void DrawPresentationAttachment(SerializedProperty property)
        {
            var values = (MonsterActivePresentationAttachment[])Enum.GetValues(
                typeof(MonsterActivePresentationAttachment));
            var labels = values.Select(GetPresentationAttachmentLabel).ToArray();
            var current = Mathf.Max(0, Array.IndexOf(values,
                (MonsterActivePresentationAttachment)property.enumValueIndex));
            property.enumValueIndex = (int)values[EditorGUILayout.Popup("부착 방식", current, labels)];
        }

        private static void DrawPresentationEndPolicy(SerializedProperty property)
        {
            var values = (MonsterActivePresentationEndPolicy[])Enum.GetValues(
                typeof(MonsterActivePresentationEndPolicy));
            var labels = values.Select(GetPresentationEndPolicyLabel).ToArray();
            var current = Mathf.Max(0, Array.IndexOf(values,
                (MonsterActivePresentationEndPolicy)property.enumValueIndex));
            property.enumValueIndex = (int)values[EditorGUILayout.Popup("정리 시점", current, labels)];
        }

        private void DrawPresentationCompatibilityNotice(
            SerializedProperty step,
            MonsterActiveAttackPattern pattern,
            SerializedProperty slot)
        {
            var stepIndex = ParseArrayIndex(step.propertyPath);
            if (profile == null || stepIndex < 0 || stepIndex >= profile.Steps.Count) return;
            var contract = new MonsterActivePresentationSlot();
            contract.EditorConfigure(
                slot.FindPropertyRelative("slotId").stringValue,
                slot.FindPropertyRelative("displayName").stringValue,
                (MonsterActivePresentationEvent)slot.FindPropertyRelative("timing").enumValueIndex,
                (MonsterActivePresentationAnchor)slot.FindPropertyRelative("anchor").enumValueIndex,
                slot.FindPropertyRelative("description").stringValue,
                slot.FindPropertyRelative("useDuration").boolValue,
                slot.FindPropertyRelative("duration").floatValue,
                (MonsterActivePresentationMultiplicity)slot.FindPropertyRelative("multiplicity").enumValueIndex,
                (MonsterActivePresentationAttachment)slot.FindPropertyRelative("attachment").enumValueIndex,
                (MonsterActivePresentationEndPolicy)slot.FindPropertyRelative("endPolicy").enumValueIndex);
            if (!MonsterActiveAttackVfxCompatibility.TryValidateSlot(profile.Steps[stepIndex], contract, out var error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Warning);
            }
        }

        private void ShowAddPresentationSlotMenu(
            SerializedProperty step,
            SerializedProperty slots)
        {
            var menu = new GenericMenu();
            var stepIndex = ParseArrayIndex(step.propertyPath);
            var runtimeStep = profile != null && stepIndex >= 0 && stepIndex < profile.Steps.Count
                ? profile.Steps[stepIndex]
                : null;
            foreach (MonsterActivePresentationEvent timing in Enum.GetValues(typeof(MonsterActivePresentationEvent)))
            {
                if (runtimeStep != null && !MonsterActiveAttackVfxCompatibility.SupportsEvent(runtimeStep, timing))
                    continue;
                var captured = timing;
                menu.AddItem(new GUIContent(GetPresentationEventLabel(timing)), false,
                    () => AddPresentationSlotAndCommit(slots, captured));
            }
            menu.ShowAsContext();
        }

        private void AddPresentationSlotAndCommit(
            SerializedProperty slots,
            MonsterActivePresentationEvent timing)
        {
            AddPresentationSlot(slots, timing);
            CommitStructuralChange(slots);
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
            slot.FindPropertyRelative("multiplicity").enumValueIndex =
                (int)GetDefaultPresentationMultiplicity(timing);
            slot.FindPropertyRelative("attachment").enumValueIndex =
                (int)GetDefaultPresentationAttachment(timing);
            slot.FindPropertyRelative("endPolicy").enumValueIndex =
                (int)GetDefaultPresentationEndPolicy(timing);
            slot.FindPropertyRelative("description").stringValue = string.Empty;
            slot.FindPropertyRelative("useDuration").boolValue = false;
            slot.FindPropertyRelative("duration").floatValue = 1f;
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
                GUILayout.Label("선택 사항 · 에어본/넉백/끌어당기기/기절/출혈/둔화를 함께 조립할 수 있습니다.",
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
                            DeleteEffectAndCommit(effects, index);
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
                        case MonsterActiveHitEffectType.Pull:
                            DrawRelative(effect, "magnitude", "끌어당김 거리(m, 최대 2)");
                            DrawRelative(effect, "duration", "이동 시간(초, 최대 1.5)");
                            effect.FindPropertyRelative("magnitude").floatValue = Mathf.Clamp(
                                effect.FindPropertyRelative("magnitude").floatValue,
                                0f,
                                MonsterActiveHitEffect.MaximumPullDistance);
                            effect.FindPropertyRelative("duration").floatValue = Mathf.Clamp(
                                effect.FindPropertyRelative("duration").floatValue,
                                0f,
                                MonsterActiveHitEffect.MaximumPullDuration);
                            break;
                    }
                }
            }
        }

        private void ShowAddStepMenu(SerializedProperty steps)
        {
            var menu = new GenericMenu();
            foreach (MonsterActiveAttackPattern pattern in Enum.GetValues(typeof(MonsterActiveAttackPattern)))
            {
                var captured = pattern;
                menu.AddItem(new GUIContent(GetPatternLabel(pattern)), false, () => AddStepAndCommit(steps, captured));
            }
            menu.ShowAsContext();
        }

        private void AddStepAndCommit(SerializedProperty steps, MonsterActiveAttackPattern pattern)
        {
            AddStep(steps, pattern);
            CommitStructuralChange(steps);
            ReconcileStepContractAndCommit(steps.arraySize - 1, "새 공격");
        }

        private void ReconcileStepContractAndCommit(int stepIndex, string reason)
        {
            if (profile == null || serializedProfile == null ||
                stepIndex < 0 || stepIndex >= profile.Steps.Count)
            {
                return;
            }

            serializedProfile.ApplyModifiedProperties();
            var target = profile.Steps[stepIndex];
            var reconciled = MonsterActiveAttackVfxContractTemplates.Reconcile(
                target,
                target.PresentationSlots,
                out var result);
            target.EditorSetPresentationSlots(reconciled);
            serializedProfile.Update();
            OnWorkingProfileChanged();
            message =
                $"{reason} 기준으로 공간을 정리했습니다. 유지 {result.Retained} · 추가 {result.Added} · 제외 {result.Archived}";
            messageType = MessageType.Info;
        }

        private static int ParseArrayIndex(string propertyPath)
        {
            if (string.IsNullOrWhiteSpace(propertyPath)) return -1;
            var marker = propertyPath.LastIndexOf("Array.data[", StringComparison.Ordinal);
            if (marker < 0) return -1;
            marker += "Array.data[".Length;
            var end = propertyPath.IndexOf(']', marker);
            return end > marker && int.TryParse(propertyPath.Substring(marker, end - marker), out var index)
                ? index
                : -1;
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

        private void ShowAddEffectMenu(SerializedProperty effects)
        {
            var menu = new GenericMenu();
            foreach (MonsterActiveHitEffectType type in Enum.GetValues(typeof(MonsterActiveHitEffectType)))
            {
                var captured = type;
                menu.AddItem(new GUIContent(GetEffectLabel(type)), false, () => AddEffectAndCommit(effects, captured));
            }
            menu.ShowAsContext();
        }

        private void AddEffectAndCommit(SerializedProperty effects, MonsterActiveHitEffectType type)
        {
            AddEffect(effects, type);
            CommitStructuralChange(effects);
        }

        private void DeleteEffectAndCommit(SerializedProperty effects, int index)
        {
            effects.DeleteArrayElementAtIndex(index);
            CommitStructuralChange(effects);
        }

        private void CommitStructuralChange(SerializedProperty collection)
        {
            collection.serializedObject.ApplyModifiedProperties();
            OnWorkingProfileChanged();
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
                MonsterActiveHitEffectType.Pull => 0.6f,
                _ => 0.3f
            };
            effect.FindPropertyRelative("duration").floatValue = type switch
            {
                MonsterActiveHitEffectType.Stun => 1f,
                MonsterActiveHitEffectType.Bleed => 3f,
                MonsterActiveHitEffectType.Slow => 2f,
                MonsterActiveHitEffectType.Pull => 0.2f,
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
                using (new EditorGUI.DisabledScope(loadedProfile != null))
                {
                    var createContent = loadedProfile == null
                        ? new GUIContent("새 프리셋으로 저장")
                        : new GUIContent("복제 후 새 프리셋 저장", "위의 '다른 프리셋으로 복제'를 먼저 사용하세요.");
                    if (MonsterWorkshopVisualTheme.DrawTintedButton(
                            createContent,
                            MonsterWorkshopVisualTheme.PrimaryColor,
                            30f))
                    {
                        SaveAsNew();
                    }
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
            profile.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave; // 임시 사본은 저장하지 않되 편집은 허용
            var step = new MonsterActiveAttackStep();
            step.EditorConfigure("step_01", "일자 피해", MonsterActiveAttackPattern.Line);
            step.EditorSetPresentationSlots(MonsterActiveAttackVfxContractTemplates.Build(step));
            profile.EditorConfigure(
                FindNextProfileId(),
                "새 공격 액티브",
                "Step을 조립해 공격 흐름을 완성하세요.",
                new[] { step });
            loadedProfile = null;
            serializedProfile = new SerializedObject(profile);
            SetWorkCopyDirty(false);
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
            profile.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave; // 임시 사본은 저장하지 않되 편집은 허용
            loadedProfile = target;
            serializedProfile = new SerializedObject(profile);
            SetWorkCopyDirty(false);
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
            SetWorkCopyDirty(true);
            preview?.Refresh();
            Repaint();
        }

        private void TryStartBlank()
        {
            if (!TryResolveUnsavedChanges("빈 공격 액티브를 시작하기")) return;
            StartBlank();
        }

        private void TryLoadProfile(MonsterActiveAttackProfile target)
        {
            if (target == null || target == loadedProfile) return;
            if (!TryResolveUnsavedChanges($"[{target.ProfileId}] {target.DisplayName} 프리셋을 불러오기")) return;
            LoadProfile(target);
        }

        private bool TryResolveUnsavedChanges(string nextAction)
        {
            if (!workCopyDirty) return true;
            var choice = EditorUtility.DisplayDialogComplex(
                "미저장 변경",
                $"현재 공격 액티브의 변경 사항이 저장되지 않았습니다.\n\n다음 작업: {nextAction}",
                "저장 후 계속",
                "계속 편집",
                "저장하지 않고 계속");
            if (choice == 1) return false;
            if (choice == 2)
            {
                SetWorkCopyDirty(false);
                return true;
            }

            SaveChanges();
            return !workCopyDirty;
        }

        private void SetWorkCopyDirty(bool value)
        {
            workCopyDirty = value;
            hasUnsavedChanges = value;
            saveChangesMessage = "공격 액티브 조립소의 변경 사항을 저장하시겠습니까?";
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
            SetWorkCopyDirty(false);
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
            SetWorkCopyDirty(false);
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
            SetWorkCopyDirty(true);
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
            MonsterWorkshopAssignmentEvents.NotifyPresetAssigned();
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
            step.EditorSetPresentationSlots(MonsterActiveAttackVfxContractTemplates.Build(step));
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
            MonsterActivePresentationEvent.MotionStart => "모션 시작",
            MonsterActivePresentationEvent.DeliverySpawn => "이동체 생성",
            MonsterActivePresentationEvent.AreaResolved => "범위 판정 완료",
            MonsterActivePresentationEvent.DeliveryEnd => "이동체 종료",
            MonsterActivePresentationEvent.StepEnd => "Step 종료",
            _ => value.ToString()
        };

        private static string GetPresentationAnchorLabel(MonsterActivePresentationAnchor value) => value switch
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
            _ => value.ToString()
        };

        private static string GetPresentationMultiplicityLabel(MonsterActivePresentationMultiplicity value) => value switch
        {
            MonsterActivePresentationMultiplicity.OncePerStep => "Step당 1회",
            MonsterActivePresentationMultiplicity.OncePerProjectile => "투사체마다",
            MonsterActivePresentationMultiplicity.PerTargetHit => "실제 명중 대상마다",
            MonsterActivePresentationMultiplicity.PerDamageStage => "피해 단계마다",
            MonsterActivePresentationMultiplicity.ContinuousUntilEnd => "종료까지 지속",
            _ => value.ToString()
        };

        private static string GetPresentationAttachmentLabel(MonsterActivePresentationAttachment value) => value switch
        {
            MonsterActivePresentationAttachment.World => "월드 위치 고정",
            MonsterActivePresentationAttachment.FollowAnchor => "기준 위치 추적",
            MonsterActivePresentationAttachment.DeliveryVisual => "실제 이동체 외형",
            _ => value.ToString()
        };

        private static string GetPresentationEndPolicyLabel(MonsterActivePresentationEndPolicy value) => value switch
        {
            MonsterActivePresentationEndPolicy.Timed => "설정 시간",
            MonsterActivePresentationEndPolicy.DeliveryEnd => "이동체 종료",
            MonsterActivePresentationEndPolicy.StepEnd => "Step 종료",
            MonsterActivePresentationEndPolicy.MotionEnd => "모션 종료",
            MonsterActivePresentationEndPolicy.ParticleDuration => "파티클 자체 수명",
            _ => value.ToString()
        };

        private static MonsterActivePresentationAnchor GetDefaultPresentationAnchor(
            MonsterActivePresentationEvent value) => value switch
        {
            MonsterActivePresentationEvent.Telegraph => MonsterActivePresentationAnchor.TargetPoint,
            MonsterActivePresentationEvent.Impact => MonsterActivePresentationAnchor.TargetPoint,
            MonsterActivePresentationEvent.TeleportExit => MonsterActivePresentationAnchor.CasterRoot,
            MonsterActivePresentationEvent.TeleportEnter => MonsterActivePresentationAnchor.CasterRoot,
            MonsterActivePresentationEvent.DeliverySpawn => MonsterActivePresentationAnchor.ProjectileRoot,
            MonsterActivePresentationEvent.DeliveryEnd => MonsterActivePresentationAnchor.ProjectileRoot,
            MonsterActivePresentationEvent.AreaResolved => MonsterActivePresentationAnchor.AreaCenter,
            _ => MonsterActivePresentationAnchor.AttackOrigin
        };

        private static MonsterActivePresentationMultiplicity GetDefaultPresentationMultiplicity(
            MonsterActivePresentationEvent value) => value switch
        {
            MonsterActivePresentationEvent.Impact => MonsterActivePresentationMultiplicity.PerTargetHit,
            MonsterActivePresentationEvent.DeliverySpawn =>
                MonsterActivePresentationMultiplicity.OncePerProjectile,
            MonsterActivePresentationEvent.DeliveryEnd =>
                MonsterActivePresentationMultiplicity.OncePerProjectile,
            _ => MonsterActivePresentationMultiplicity.OncePerStep
        };

        private static MonsterActivePresentationAttachment GetDefaultPresentationAttachment(
            MonsterActivePresentationEvent value) => value ==
            MonsterActivePresentationEvent.DeliverySpawn
                ? MonsterActivePresentationAttachment.DeliveryVisual
                : MonsterActivePresentationAttachment.World;

        private static MonsterActivePresentationEndPolicy GetDefaultPresentationEndPolicy(
            MonsterActivePresentationEvent value) => value ==
            MonsterActivePresentationEvent.DeliverySpawn
                ? MonsterActivePresentationEndPolicy.DeliveryEnd
                : MonsterActivePresentationEndPolicy.Timed;

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
            MonsterActiveHitEffectType.Pull => "끌어당기기",
            _ => value.ToString()
        };
    }
}
