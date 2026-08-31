using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.EditorTools.MonsterMaker;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    // 세 조립소가 목록·작업 사본·저장·배정·Preview 문법을 공유하는 V2 진입점
    public sealed partial class MonsterWorkshopV2Window : EditorWindow
    {
        internal enum WorkshopMode { Basic, Attack, Effect }
        private const string UxmlPath = "Assets/ProjectMT/Editor/MonsterMakerV2/UI/MonsterWorkshopV2Window.uxml";
        private const string SessionPrefix = "ProjectMT.MonsterWorkshopV2.";
        private const HideFlags EditableWorkCopyFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;

        [SerializeField] private WorkshopMode mode;
        [SerializeField] private MonsterMakerDraft originDraft;
        [SerializeField] private MonsterBasicAttackProfile requestedBasic;
        [SerializeField] private MonsterActiveAttackProfile requestedAttack;
        [SerializeField] private MonsterEffectActiveProfile requestedEffect;

        private MonsterBasicAttackWorkshopSession basicSession;
        private MonsterBasicAttackWorkshopPreviewV2 basicPreview;
        private MonsterActiveAttackProfile attackWorking, attackLoaded;
        private MonsterEffectActiveProfile effectWorking, effectLoaded;
        private SerializedObject attackSerialized, effectSerialized;
        private bool attackDirty, effectDirty, discardingChanges, suppressUiCallbacks;
        private string attackMessage = string.Empty, effectMessage = string.Empty, search = string.Empty;
        private string attackBaselineJson = string.Empty, effectBaselineJson = string.Empty;
        private int selectedBasicMotion, selectedAttackStep, selectedEffectGroup;
        private MonsterActiveAttackAuthoringPreview attackPreview;
        private double effectPreviewStartedAt;
        private bool effectPreviewPlaying, effectPreviewAll, topDownPreview;

        private Label titleLabel, captionLabel, dirtyBadge, libraryCaption, libraryCount;
        private Label assemblerTitle, assemblerCaption, previewStatus, previewSummary, messageLabel;
        private TextField searchField;
        private ScrollView libraryScroll, assemblerScroll;
        private VisualElement previewToolbar, previewHost;
        private Button modeBasicButton, modeAttackButton, modeEffectButton;
        private Button newButton, forkButton, saveNewButton, updateButton, assignButton;
        private IMGUIContainer previewSurface;
        private bool sessionPersistScheduled;
        private double sessionPersistAt;
        private bool libraryDirty = true;
        private bool rebuildPending;


        [MenuItem("JC Tool/Monster/기본공격 조립소")]
        public static void OpenBasicMenu() => OpenBasic(null, null);
        [MenuItem("JC Tool/Monster/공격 액티브 조립소")]
        public static void OpenAttackMenu() => OpenAttack(null, null);
        [MenuItem("JC Tool/Monster/효과형 액티브 조립소")]
        public static void OpenEffectMenu() => OpenEffect(null, null);

        public static void OpenBasic(MonsterMakerDraft draft, MonsterBasicAttackProfile target = null) =>
            Open(WorkshopMode.Basic, draft, target, null, null);
        public static void OpenAttack(MonsterActiveAttackProfile target, MonsterMakerDraft draft) =>
            Open(WorkshopMode.Attack, draft, null, target, null);
        public static void OpenEffect(MonsterEffectActiveProfile target, MonsterMakerDraft draft) =>
            Open(WorkshopMode.Effect, draft, null, null, target);

        private static void Open(WorkshopMode requestedMode, MonsterMakerDraft draft,
            MonsterBasicAttackProfile basic, MonsterActiveAttackProfile attack, MonsterEffectActiveProfile effect)
        {
            var window = Resources.FindObjectsOfTypeAll<MonsterWorkshopV2Window>().FirstOrDefault();
            if (window == null)
            {
                window = CreateInstance<MonsterWorkshopV2Window>();
                window.titleContent = new GUIContent("몬스터 조립소 V2");
                window.minSize = new Vector2(1100f, 700f);
                window.position = new Rect(80f, 70f, 1460f, 900f);
                window.Show();
            }
            // 메뉴에서 독립 실행한 창이 이전 Maker의 배정 대상을 계속 기억하면 잘못된 몬스터에 저장될 수 있다.
            // 탭 전환은 이 메서드를 거치지 않으므로, 외부 진입마다 전달된 대상을 그대로 기준으로 삼는다.
            window.originDraft = draft;
            window.requestedBasic = basic;
            window.requestedAttack = attack;
            window.requestedEffect = effect;
            window.EnsureSessions();
            window.SwitchMode(requestedMode);
            window.basicSession?.SetOriginDraft(window.originDraft);
            if (window.TryResolveCurrentWork("Monster Maker V2에서 프리셋 불러오기"))
            {
                window.LoadRequestedSelection(requestedMode);
                window.RebuildCurrent();
            }
            else
            {
                window.requestedBasic = null; window.requestedAttack = null; window.requestedEffect = null;
            }
            window.Focus();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("몬스터 조립소 V2");
            minSize = new Vector2(1100f, 700f);
            saveChangesMessage = "조립소 V2에 미저장 작업 사본이 있습니다.";
            EnsureSessions();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (tree == null)
            {
                rootVisualElement.Add(new HelpBox($"조립소 V2 UI를 찾지 못했습니다: {UxmlPath}", HelpBoxMessageType.Error));
                return;
            }
            tree.CloneTree(rootVisualElement);
            BindShell();
            SwitchMode(mode, true);
        }

        private void BindShell()
        {
            titleLabel = Q<Label>("workshop-title"); captionLabel = Q<Label>("workshop-caption");
            dirtyBadge = Q<Label>("dirty-badge"); libraryCaption = Q<Label>("library-caption");
            libraryCount = Q<Label>("library-count"); assemblerTitle = Q<Label>("assembler-title");
            assemblerCaption = Q<Label>("assembler-caption"); previewStatus = Q<Label>("preview-status");
            previewSummary = Q<Label>("preview-summary"); messageLabel = Q<Label>("message-label");
            searchField = Q<TextField>("search-field"); libraryScroll = Q<ScrollView>("library-scroll");
            assemblerScroll = Q<ScrollView>("assembler-scroll"); previewToolbar = Q<VisualElement>("preview-toolbar");
            previewHost = Q<VisualElement>("preview-host"); modeBasicButton = Q<Button>("mode-basic");
            modeAttackButton = Q<Button>("mode-attack"); modeEffectButton = Q<Button>("mode-effect");
            newButton = Q<Button>("new-button"); forkButton = Q<Button>("fork-button");
            saveNewButton = Q<Button>("save-new-button"); updateButton = Q<Button>("update-button");
            assignButton = Q<Button>("assign-button");
            libraryScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            assemblerScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            modeBasicButton.clicked += () => SwitchMode(WorkshopMode.Basic);
            modeAttackButton.clicked += () => SwitchMode(WorkshopMode.Attack);
            modeEffectButton.clicked += () => SwitchMode(WorkshopMode.Effect);
            newButton.clicked += StartBlankCurrent; forkButton.clicked += ForkCurrent;
            saveNewButton.clicked += SaveCurrentAsNew; updateButton.clicked += UpdateCurrent;
            assignButton.clicked += AssignCurrent;
            searchField.RegisterValueChangedCallback(evt =>
            {
                search = evt.newValue ?? string.Empty;
                libraryDirty = true;
                RefreshLibrary();
            });
            Q<Button>("preview-help-button").clicked += () => EditorUtility.DisplayDialog("조립소 V2 미리보기",
                "현재 작업 사본의 판정과 순서를 확인합니다. VFX/SFX 공간은 계약이며 실제 몬스터 자산은 Monster Maker V2에서 연결합니다.", "확인");
        }

        private T Q<T>(string name) where T : VisualElement => rootVisualElement.Q<T>(name);

        private void EnsureSessions()
        {
            if (basicSession == null || basicSession.WorkingProfile == null)
            {
                basicSession?.Dispose();
                basicSession = new MonsterBasicAttackWorkshopSession();
                var target = requestedBasic != null
                    ? requestedBasic
                    : LoadAssetFromSession<MonsterBasicAttackProfile>("basic.path");
                basicSession.Initialize(originDraft, target, SessionState.GetString(Key("basic.json"), null),
                    SessionState.GetBool(Key("basic.dirty"), false));
            }
            if (attackWorking == null)
            {
                attackWorking = CreateInstance<MonsterActiveAttackProfile>();
                attackWorking.hideFlags = EditableWorkCopyFlags;
                var json = SessionState.GetString(Key("attack.json"), string.Empty);
                if (string.IsNullOrWhiteSpace(json)) ResetAttackBlank(); else JsonUtility.FromJsonOverwrite(json, attackWorking);
                attackSerialized = new SerializedObject(attackWorking);
                attackDirty = SessionState.GetBool(Key("attack.dirty"), false);
                attackLoaded = LoadAssetFromSession<MonsterActiveAttackProfile>("attack.path");
                attackBaselineJson = attackLoaded != null ? JsonUtility.ToJson(attackLoaded) :
                    attackDirty ? string.Empty : JsonUtility.ToJson(attackWorking);
            }
            else if (attackSerialized == null) attackSerialized = new SerializedObject(attackWorking);
            if (effectWorking == null)
            {
                effectWorking = CreateInstance<MonsterEffectActiveProfile>();
                effectWorking.hideFlags = EditableWorkCopyFlags;
                var json = SessionState.GetString(Key("effect.json"), string.Empty);
                if (string.IsNullOrWhiteSpace(json)) ResetEffectBlank(); else JsonUtility.FromJsonOverwrite(json, effectWorking);
                effectSerialized = new SerializedObject(effectWorking);
                effectDirty = SessionState.GetBool(Key("effect.dirty"), false);
                effectLoaded = LoadAssetFromSession<MonsterEffectActiveProfile>("effect.path");
                effectBaselineJson = effectLoaded != null ? JsonUtility.ToJson(effectLoaded) :
                    effectDirty ? string.Empty : JsonUtility.ToJson(effectWorking);
            }
            else if (effectSerialized == null) effectSerialized = new SerializedObject(effectWorking);
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= RebuildIfAlive;
            EditorApplication.update -= PersistSessionsWhenReady;
            sessionPersistScheduled = false;
            rebuildPending = false;
            if (!discardingChanges) PersistSessions();
            basicPreview?.Dispose();
            basicPreview = null;
            attackPreview?.Dispose();
            attackPreview = null;
            effectPreviewPlaying = false;
        }

        private void LoadRequestedSelection(WorkshopMode requestedMode)
        {
            if (requestedMode == WorkshopMode.Basic && requestedBasic != null) basicSession.Load(requestedBasic);
            if (requestedMode == WorkshopMode.Attack && requestedAttack != null) LoadAttack(requestedAttack);
            if (requestedMode == WorkshopMode.Effect && requestedEffect != null) LoadEffect(requestedEffect);
            requestedBasic = null; requestedAttack = null; requestedEffect = null;
        }

        private void SwitchMode(WorkshopMode next, bool force = false)
        {
            if (!force && mode == next && assemblerScroll != null) return;
            StopAllPreviews(); mode = next; selectedAttackStep = 0; selectedEffectGroup = 0;
            if (rootVisualElement.childCount == 0 || assemblerScroll == null) return;
            foreach (var button in new[] { modeBasicButton, modeAttackButton, modeEffectButton })
                button.RemoveFromClassList("mode-tab--active");
            (mode == WorkshopMode.Basic ? modeBasicButton : mode == WorkshopMode.Attack ? modeAttackButton : modeEffectButton)
                .AddToClassList("mode-tab--active");
            titleLabel.text = mode switch { WorkshopMode.Basic => "기본공격 조립소 V2", WorkshopMode.Attack => "공격 액티브 조립소 V2", _ => "효과형 액티브 조립소 V2" };
            captionLabel.text = mode switch
            {
                WorkshopMode.Basic => "기본공격 방식 · 판정 · 공통 FEEL · VFX/SFX 공간 계약",
                WorkshopMode.Attack => "Step 조립 · 타격 효과 · 공통 FEEL · VFX/SFX 공간 계약",
                _ => "지원 · 수호 · 디버프 효과 묶음과 VFX/SFX 공간 계약"
            };
            libraryCaption.text = "공식·사용자 구분 없이 한 목록에서 직접 수정합니다.";
            searchField.SetValueWithoutNotify(search);
            libraryDirty = true;
            RebuildCurrent();
        }

        private void RebuildCurrent() { RefreshLibrary(); RebuildAssembler(); RebuildPreview(); RefreshState(); }

        private void RefreshLibrary()
        {
            if (libraryScroll == null || !libraryDirty) return;
            libraryDirty = false;
            libraryScroll.Clear();
            var usageCounts = BuildUsageCounts();
            var rows = new List<(UnityEngine.Object asset, string id, string title, int uses)>();
            if (mode == WorkshopMode.Basic)
                rows.AddRange(basicSession.FindProfiles().Select(x => ((UnityEngine.Object)x, x.AttackId, x.DisplayName, UsageOf(usageCounts, x))));
            else if (mode == WorkshopMode.Attack)
                rows.AddRange(FindAssets<MonsterActiveAttackProfile>(MonsterActiveAttackAuthoringService.ProfileRoot)
                    .Select(x => ((UnityEngine.Object)x, x.ProfileId, x.DisplayName, UsageOf(usageCounts, x))));
            else
                rows.AddRange(FindAssets<MonsterEffectActiveProfile>(MonsterEffectActiveAuthoringService.ProfileRoot)
                    .Select(x => ((UnityEngine.Object)x, x.ProfileId, x.DisplayName, UsageOf(usageCounts, x))));

            var filter = search.Trim();
            if (filter.Length > 0)
                rows = rows.Where(x => x.id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       x.title.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            libraryCount.text = $"{rows.Count}개";
            foreach (var row in rows.OrderBy(x => x.id, StringComparer.OrdinalIgnoreCase))
            {
                var shell = new VisualElement();
                shell.AddToClassList("preset-row");
                if (row.asset == CurrentLoadedAsset()) shell.AddToClassList("preset-row--selected");
                shell.style.flexDirection = FlexDirection.Row;
                var button = new Button(() => LoadAsset(row.asset)) { text = $"[{row.id}] {row.title}" };
                button.AddToClassList("preset-button");
                var usage = new Label(row.uses.ToString()) { tooltip = $"배정된 몬스터 {row.uses}개" };
                usage.AddToClassList("preset-usage");
                shell.Add(button); shell.Add(usage); libraryScroll.Add(shell);
            }
        }

        private void LoadAsset(UnityEngine.Object asset)
        {
            if (!TryResolveCurrentWork("다른 프리셋 불러오기")) return;
            StopAllPreviews();
            if (asset is MonsterBasicAttackProfile basic) basicSession.Load(basic);
            else if (asset is MonsterActiveAttackProfile attack) LoadAttack(attack);
            else if (asset is MonsterEffectActiveProfile effect) LoadEffect(effect);
            libraryDirty = true;
            RebuildCurrent();
            PersistSessions(); // 프리셋 선택 경로는 창이 바로 닫혀도 복구할 수 있게 즉시 기록한다.
        }

        private void RebuildAssembler()
        {
            suppressUiCallbacks = true;
            try
            {
                assemblerScroll.Clear();
                if (mode == WorkshopMode.Basic) BuildBasicAssembler();
                else if (mode == WorkshopMode.Attack) BuildAttackAssembler();
                else BuildEffectAssembler();
            }
            finally
            {
                suppressUiCallbacks = false;
            }
        }

        private void RebuildPreview()
        {
            previewToolbar.Clear(); previewHost.Clear();
            previewSurface = new IMGUIContainer(DrawCurrentPreview) { name = "preview-surface" };
            previewSurface.style.flexGrow = 1f;
            previewHost.Add(previewSurface);
            BuildPreviewToolbar();
        }

        private void RefreshState()
        {
            var dirty = CurrentDirty();
            dirtyBadge.text = dirty ? "미저장" : "저장됨";
            dirtyBadge.EnableInClassList("workshop-state--dirty", dirty);
            dirtyBadge.EnableInClassList("workshop-state--saved", !dirty);
            var hasLoadedAsset = CurrentLoadedAsset() != null;
            assemblerTitle.text = hasLoadedAsset ? CurrentLoadedName() : "새 작업 사본";
            assemblerCaption.text = hasLoadedAsset
                ? "원본 자산을 직접 건드리지 않는 작업 사본입니다. 저장할 때만 원본에 반영합니다."
                : "새 프리셋으로 저장하기 전까지 자산과 완전히 분리됩니다.";
            forkButton.style.display = hasLoadedAsset ? DisplayStyle.Flex : DisplayStyle.None;
            forkButton.SetEnabled(hasLoadedAsset);
            saveNewButton.style.display = hasLoadedAsset ? DisplayStyle.None : DisplayStyle.Flex;
            saveNewButton.SetEnabled(!hasLoadedAsset);
            updateButton.style.display = hasLoadedAsset ? DisplayStyle.Flex : DisplayStyle.None;
            updateButton.SetEnabled(hasLoadedAsset && dirty);
            assignButton.style.display = originDraft == null ? DisplayStyle.None : DisplayStyle.Flex;
            assignButton.SetEnabled(hasLoadedAsset && !dirty && originDraft != null);
            messageLabel.text = CurrentMessage();
            messageLabel.EnableInClassList("message-label--error", CurrentMessageIsError());
            hasUnsavedChanges = AnyDirty();
            SchedulePersistSessions();
            previewSurface?.MarkDirtyRepaint();
        }

        private void StartBlankCurrent()
        {
            if (!TryResolveCurrentWork("빈 프리셋 시작")) return;
            StopAllPreviews();
            if (mode == WorkshopMode.Basic) basicSession.StartBlank();
            else if (mode == WorkshopMode.Attack) { ResetAttackBlank(); attackDirty = false; attackLoaded = null; }
            else { ResetEffectBlank(); effectDirty = false; effectLoaded = null; }
            libraryDirty = true;
            RebuildCurrent();
        }

        private void ForkCurrent()
        {
            if (mode == WorkshopMode.Basic) basicSession.Fork();
            else if (mode == WorkshopMode.Attack)
            {
                attackLoaded = null; SetString(attackSerialized, "profileId", string.Empty); attackDirty = true;
                attackMessage = "복제된 작업 사본입니다. 새 프리셋 ID를 입력하세요.";
            }
            else
            {
                effectLoaded = null; SetString(effectSerialized, "profileId", string.Empty); effectDirty = true;
                effectMessage = "복제된 작업 사본입니다. 새 프리셋 ID를 입력하세요.";
            }
            libraryDirty = true;
            RebuildCurrent();
        }

        private void SaveCurrentAsNew()
        {
            if (mode == WorkshopMode.Basic) basicSession.SaveAsNew(out _);
            else if (mode == WorkshopMode.Attack)
            {
                attackSerialized.ApplyModifiedProperties();
                if (MonsterActiveAttackAuthoringService.TryCreate(attackWorking, out var created, out _, out var error))
                { LoadAttack(created); attackMessage = "새 공격 액티브 프리셋을 저장했습니다."; }
                else attackMessage = "오류: " + error;
            }
            else
            {
                effectSerialized.ApplyModifiedProperties();
                if (MonsterEffectActiveAuthoringService.TryCreate(effectWorking, out var created, out _, out var error))
                { LoadEffect(created); effectMessage = "새 효과형 액티브 프리셋을 저장했습니다."; }
                else effectMessage = "오류: " + error;
            }
            libraryDirty = true;
            RebuildCurrent();
        }

        private void UpdateCurrent()
        {
            if (mode == WorkshopMode.Basic) basicSession.UpdateLoaded();
            else if (mode == WorkshopMode.Attack)
            {
                attackSerialized.ApplyModifiedProperties();
                if (MonsterActiveAttackAuthoringService.TryUpdate(attackWorking, attackLoaded, out var error))
                { var loaded = attackLoaded; LoadAttack(loaded); attackMessage = "현재 공격 액티브 프리셋을 저장했습니다."; }
                else attackMessage = "오류: " + error;
            }
            else
            {
                effectSerialized.ApplyModifiedProperties();
                if (MonsterEffectActiveAuthoringService.TryUpdate(effectWorking, effectLoaded, out var error))
                { var loaded = effectLoaded; LoadEffect(loaded); effectMessage = "현재 효과형 액티브 프리셋을 저장했습니다."; }
                else effectMessage = "오류: " + error;
            }
            libraryDirty = true;
            RebuildCurrent();
        }

        private void AssignCurrent()
        {
            if (originDraft == null || CurrentDirty() || CurrentLoadedAsset() == null) return;
            Undo.RecordObject(originDraft, "몬스터 조립소 V2 프리셋 배정");
            if (mode == WorkshopMode.Basic) basicSession.AssignToOrigin();
            else if (mode == WorkshopMode.Attack) originDraft.EditorSetActiveAttackProfile(attackLoaded);
            else originDraft.EditorSetActiveEffectProfile(effectLoaded);
            EditorUtility.SetDirty(originDraft);
            MonsterWorkshopAssignmentEvents.NotifyPresetAssigned();
            libraryDirty = true;
            RefreshState();
        }

        private bool TryResolveCurrentWork(string action)
        {
            if (!CurrentDirty()) return true;
            var choice = EditorUtility.DisplayDialogComplex(
                "조립소 V2 · 미저장 작업",
                $"{action} 전에 현재 작업 사본을 어떻게 처리할까요?",
                "현재 프리셋 저장",
                "취소",
                "저장하지 않고 계속");
            if (choice == 1) return false;
            if (choice == 2) return true;
            if (CurrentLoadedAsset() == null) SaveCurrentAsNew(); else UpdateCurrent();
            return !CurrentDirty();
        }

        private void MarkCurrentDirty(string message = null, bool rebuild = false)
        {
            if (mode == WorkshopMode.Basic) basicSession.NotifyChanged(false);
            else if (mode == WorkshopMode.Attack)
            { attackSerialized.ApplyModifiedProperties(); attackDirty = !string.Equals(attackBaselineJson, JsonUtility.ToJson(attackWorking), StringComparison.Ordinal); if (message != null) attackMessage = message; }
            else
            { effectSerialized.ApplyModifiedProperties(); effectDirty = !string.Equals(effectBaselineJson, JsonUtility.ToJson(effectWorking), StringComparison.Ordinal); if (message != null) effectMessage = message; }
            if (rebuild) RebuildCurrent(); else RefreshState();
        }

        private void LoadAttack(MonsterActiveAttackProfile source)
        {
            if (source == null) return;
            EditorUtility.CopySerialized(source, attackWorking); attackWorking.hideFlags = EditableWorkCopyFlags;
            attackSerialized = new SerializedObject(attackWorking); attackLoaded = source; attackDirty = false;
            attackBaselineJson = JsonUtility.ToJson(attackWorking);
            attackMessage = $"작업 사본으로 불러왔습니다: [{source.ProfileId}] {source.DisplayName}";
        }

        private void LoadEffect(MonsterEffectActiveProfile source)
        {
            if (source == null) return;
            EditorUtility.CopySerialized(source, effectWorking); effectWorking.hideFlags = EditableWorkCopyFlags;
            effectSerialized = new SerializedObject(effectWorking); effectLoaded = source; effectDirty = false;
            effectBaselineJson = JsonUtility.ToJson(effectWorking);
            effectMessage = $"작업 사본으로 불러왔습니다: [{source.ProfileId}] {source.DisplayName}";
        }

        private void ResetAttackBlank()
        {
            attackWorking ??= CreateInstance<MonsterActiveAttackProfile>(); attackWorking.hideFlags = EditableWorkCopyFlags;
            var step = new MonsterActiveAttackStep();
            step.EditorSetPresentationSlots(MonsterActiveAttackVfxContractTemplates.Build(step));
            attackWorking.EditorConfigure(string.Empty, "새 공격 액티브", string.Empty, new[] { step });
            attackSerialized = new SerializedObject(attackWorking); attackLoaded = null; attackMessage = "빈 공격 액티브 작업 사본입니다.";
            attackBaselineJson = JsonUtility.ToJson(attackWorking);
        }

        private void ResetEffectBlank()
        {
            effectWorking ??= CreateInstance<MonsterEffectActiveProfile>(); effectWorking.hideFlags = EditableWorkCopyFlags;
            var effect = new MonsterSkillEffect();
            effect.EditorConfigure("effect_01", MonsterSkillEffectType.Heal, MonsterSkillValueSource.AttackPowerRatio, 1f);
            var group = new MonsterEffectActiveGroup();
            group.EditorConfigure("group_01", "효과 1", 0f, MonsterSkillTargetType.AllAllies, true, 5f, 8,
                new[] { effect }, CreateEffectDefaultSlots());
            effectWorking.EditorConfigure(string.Empty, "새 지원 액티브", string.Empty, MonsterEffectActiveRole.Support, new[] { group });
            effectSerialized = new SerializedObject(effectWorking); effectLoaded = null; effectMessage = "빈 효과형 액티브 작업 사본입니다.";
            effectBaselineJson = JsonUtility.ToJson(effectWorking);
        }

        private void PersistSessions()
        {
            if (basicSession != null)
            {
                SessionState.SetString(Key("basic.json"), basicSession.CaptureJson());
                SessionState.SetBool(Key("basic.dirty"), basicSession.IsDirty);
                SessionState.SetString(Key("basic.path"), AssetDatabase.GetAssetPath(basicSession.LoadedProfile));
            }
            if (attackWorking != null)
            {
                SessionState.SetString(Key("attack.json"), JsonUtility.ToJson(attackWorking));
                SessionState.SetBool(Key("attack.dirty"), attackDirty);
                SessionState.SetString(Key("attack.path"), AssetDatabase.GetAssetPath(attackLoaded));
            }
            if (effectWorking != null)
            {
                SessionState.SetString(Key("effect.json"), JsonUtility.ToJson(effectWorking));
                SessionState.SetBool(Key("effect.dirty"), effectDirty);
                SessionState.SetString(Key("effect.path"), AssetDatabase.GetAssetPath(effectLoaded));
            }
        }

        private void SchedulePersistSessions()
        {
            sessionPersistAt = EditorApplication.timeSinceStartup + 0.25d;
            if (sessionPersistScheduled) return;
            sessionPersistScheduled = true;
            EditorApplication.update += PersistSessionsWhenReady;
        }

        private void PersistSessionsWhenReady()
        {
            if (EditorApplication.timeSinceStartup < sessionPersistAt) return;
            EditorApplication.update -= PersistSessionsWhenReady;
            sessionPersistScheduled = false;
            if (this != null && !discardingChanges) PersistSessions();
        }

        public override void SaveChanges()
        {
            var originalMode = mode;
            foreach (var candidate in new[] { WorkshopMode.Basic, WorkshopMode.Attack, WorkshopMode.Effect })
            {
                mode = candidate;
                if (!CurrentDirty()) continue;
                if (CurrentLoadedAsset() == null) SaveCurrentAsNew();
                else UpdateCurrent();
                if (!CurrentDirty()) continue;

                SwitchMode(candidate, true);
                return; // 유효성 오류가 난 탭을 보여주고 창을 유지한다.
            }
            if (mode != originalMode) SwitchMode(originalMode, true);
            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            discardingChanges = true;
            EditorApplication.update -= PersistSessionsWhenReady;
            sessionPersistScheduled = false;
            attackDirty = false; effectDirty = false;
            foreach (var suffix in new[] { "basic.json", "attack.json", "effect.json" }) SessionState.EraseString(Key(suffix));
            foreach (var suffix in new[] { "basic.dirty", "attack.dirty", "effect.dirty" }) SessionState.EraseBool(Key(suffix));
            foreach (var suffix in new[] { "basic.path", "attack.path", "effect.path" }) SessionState.EraseString(Key(suffix));
            base.DiscardChanges();
        }

        private void OnDestroy()
        {
            EditorApplication.update -= PersistSessionsWhenReady;
            sessionPersistScheduled = false;
            if (!discardingChanges) PersistSessions();
            basicPreview?.Dispose();
            basicPreview = null;
            attackPreview?.Dispose(); attackPreview = null;
            effectPreviewPlaying = false;
            basicSession?.Dispose();
            basicSession = null;
            if (attackWorking != null) DestroyImmediate(attackWorking);
            if (effectWorking != null) DestroyImmediate(effectWorking);
        }

        private void OnInspectorUpdate()
        {
            if (effectPreviewPlaying || attackPreview?.IsPlaying == true ||
                (mode == WorkshopMode.Basic && basicPreview?.IsPlaying == true))
                previewSurface?.MarkDirtyRepaint();
        }

        private static string Key(string suffix) => SessionPrefix + suffix;
        private static T LoadAssetFromSession<T>(string suffix) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(SessionState.GetString(Key(suffix), string.Empty));
        private static IReadOnlyList<T> FindAssets<T>(string root) where T : UnityEngine.Object =>
            !AssetDatabase.IsValidFolder(root) ? Array.Empty<T>() : AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root })
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null).ToArray();

        private static Dictionary<UnityEngine.Object, int> BuildUsageCounts()
        {
            var result = new Dictionary<UnityEngine.Object, int>();
            foreach (var guid in AssetDatabase.FindAssets("t:MonsterMakerDraft"))
            {
                var draft = AssetDatabase.LoadAssetAtPath<MonsterMakerDraft>(AssetDatabase.GUIDToAssetPath(guid));
                if (draft == null) continue;
                AddUsage(result, draft.BasicAttackProfile);
                AddUsage(result, draft.ActiveAttackProfile);
                AddUsage(result, draft.ActiveEffectProfile);
            }
            return result;
        }

        private static void AddUsage(Dictionary<UnityEngine.Object, int> counts, UnityEngine.Object profile)
        {
            if (profile == null) return;
            counts.TryGetValue(profile, out var count);
            counts[profile] = count + 1;
        }

        private static int UsageOf(Dictionary<UnityEngine.Object, int> counts, UnityEngine.Object profile) =>
            profile != null && counts.TryGetValue(profile, out var count) ? count : 0;

        private UnityEngine.Object CurrentLoadedAsset() => mode switch
        { WorkshopMode.Basic => basicSession?.LoadedProfile, WorkshopMode.Attack => attackLoaded, _ => effectLoaded };
        private string CurrentLoadedName() => mode switch
        {
            WorkshopMode.Basic => basicSession?.LoadedProfile == null ? "새 작업 사본" : $"[{basicSession.LoadedProfile.AttackId}] {basicSession.LoadedProfile.DisplayName}",
            WorkshopMode.Attack => attackLoaded == null ? "새 작업 사본" : $"[{attackLoaded.ProfileId}] {attackLoaded.DisplayName}",
            _ => effectLoaded == null ? "새 작업 사본" : $"[{effectLoaded.ProfileId}] {effectLoaded.DisplayName}"
        };
        private bool CurrentDirty() => mode switch
        { WorkshopMode.Basic => basicSession?.IsDirty == true, WorkshopMode.Attack => attackDirty, _ => effectDirty };
        private bool AnyDirty() => basicSession?.IsDirty == true || attackDirty || effectDirty;
        private string CurrentMessage() => mode switch
        { WorkshopMode.Basic => basicSession?.Message ?? string.Empty, WorkshopMode.Attack => attackMessage, _ => effectMessage };
        private bool CurrentMessageIsError() => CurrentMessage().StartsWith("오류:", StringComparison.Ordinal);

        private static void SetString(SerializedObject serialized, string property, string value)
        {
            serialized.Update(); serialized.FindProperty(property).stringValue = value; serialized.ApplyModifiedProperties();
        }

        private void StopAllPreviews()
        {
            basicPreview?.Dispose();
            basicPreview = null;
            attackPreview?.Dispose(); attackPreview = null;
            effectPreviewPlaying = false;
        }

        private void ScheduleRebuild()
        {
            rebuildPending = true;
            QueueRebuildWhenInputIsIdle();
        }

        private void QueueRebuildWhenInputIsIdle()
        {
            if (!rebuildPending) return;
            EditorApplication.delayCall -= RebuildIfAlive;
            EditorApplication.delayCall += RebuildIfAlive;
        }

        private void RebuildIfAlive()
        {
            EditorApplication.delayCall -= RebuildIfAlive;
            if (!rebuildPending) return;
            if (HasActivePointerCapture())
            {
                EditorApplication.delayCall += RebuildIfAlive;
                return;
            }
            rebuildPending = false;
            if (this == null || assemblerScroll == null || attackWorking == null || effectWorking == null) return;
            RebuildAssembler();
            RefreshPreviewAfterAuthoringChange();
            RefreshState();
        }

        private bool HasActivePointerCapture()
        {
            var panel = rootVisualElement?.panel;
            return panel != null &&
                   PointerCaptureHelper.GetCapturingElement(panel, PointerId.mousePointerId) != null;
        }

        private VisualElement Section(string title, string help = null)
        {
            var card = new VisualElement(); card.AddToClassList("section-card");
            var heading = new Label(title); heading.AddToClassList("section-title"); card.Add(heading);
            if (!string.IsNullOrWhiteSpace(help)) { var note = new Label(help); note.AddToClassList("help-text"); card.Add(note); }
            return card;
        }

        private static Button SmallButton(string text, Action action, bool danger = false, bool enabled = true)
        {
            var button = new Button(action) { text = text }; button.AddToClassList("mini-action");
            if (danger) button.AddToClassList("danger-action"); button.SetEnabled(enabled); return button;
        }

        private static MonsterActivePresentationSlot[] CreateEffectDefaultSlots()
        {
            var caster = new MonsterActivePresentationSlot();
            caster.EditorConfigure("caster_vfx", "시전자 VFX", MonsterActivePresentationEvent.Launch,
                MonsterActivePresentationAnchor.CasterRoot, "스킬을 발동한 몬스터에게 재생합니다.");
            var target = new MonsterActivePresentationSlot();
            target.EditorConfigure("target_vfx", "효과 대상 VFX", MonsterActivePresentationEvent.Impact,
                MonsterActivePresentationAnchor.TargetRoot, "효과를 받은 아군 또는 적에게 재생합니다.");
            return new[] { caster, target };
        }

        private static Button AddButton(string text, Action action)
        { var button = new Button(action) { text = text }; button.AddToClassList("add-button"); return button; }

        private static VisualElement CardHeader(string title, params Button[] actions)
        {
            var row = new VisualElement(); row.AddToClassList("sub-card-header");
            var label = new Label(title); label.AddToClassList("sub-card-title"); row.Add(label);
            foreach (var button in actions) row.Add(button); return row;
        }

        private PropertyField BoundProperty(SerializedObject serialized, SerializedProperty property, string label, Action changed = null)
        {
            var field = new PropertyField(property.Copy(), label); field.AddToClassList("editor-field"); field.Bind(serialized);
            field.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                if (suppressUiCallbacks) return;
                serialized.ApplyModifiedProperties(); changed?.Invoke(); MarkCurrentDirty();
            });
            return field;
        }

        private Label Help(string text, bool warning = false)
        { var label = new Label(text); label.AddToClassList(warning ? "warning-text" : "help-text"); return label; }

        partial void BuildBasicAssembler();
        partial void BuildAttackAssembler();
        partial void BuildEffectAssembler();
        partial void BuildPreviewToolbar();
        partial void DrawCurrentPreview();
    }
}
