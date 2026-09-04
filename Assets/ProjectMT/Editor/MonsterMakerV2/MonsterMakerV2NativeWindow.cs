using System;
using System.Collections.Generic;
using ProjectMT.EditorTools.ExpeditionBalance;
using ProjectMT.EditorTools.MonsterMaker;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    public sealed partial class MonsterMakerV2Window : EditorWindow // UI Toolkit 기반의 독립 V2 제작 창
    {
        private const string MenuPath = "JC Tool/Monster/Monster Maker";
        private const string LayoutPath =
            "Assets/ProjectMT/Editor/MonsterMakerV2/UI/MonsterMakerV2NativeWindow.uxml";
        private const string StylePath =
            "Assets/ProjectMT/Editor/MonsterMakerV2/UI/MonsterMakerV2NativeWindow.uss";
        private const float CatalogRowHeight = 55f;
        private const float MinimumWindowWidth = 1418f;
        private const float MinimumWindowHeight = 760f;
        private const string RecoverySessionPrefix = "ProjectMT.MonsterMakerV2.Recovery.";

        private static readonly string[] RarityClasses =
        {
            "rarity-common", "rarity-rare", "rarity-epic", "rarity-legendary", "rarity-mythic"
        };

        private readonly List<CatalogEntry> allEntries = new List<CatalogEntry>();
        private readonly List<CatalogEntry> visibleEntries = new List<CatalogEntry>();

        [SerializeField] private string recoverySourcePath = string.Empty;
        [SerializeField] private string recoveryWorkingJson = string.Empty;
        [SerializeField] private bool recoveryDirty;
        [SerializeField] private bool recoveryNew;
        [SerializeField] private bool showPreviewModelReference = true;
        [SerializeField] private bool showPreviewAttackReference = true;
        [SerializeField] private bool showPreviewHitReference = true;
        [SerializeField] private bool catalogExpanded = true;

        private MonsterMakerV2State state;
        private MonsterMakerV2AuthoringView draftView;
        private MonsterMakerV2PreviewAdapter preview;
        private TextField searchField;
        private VisualElement catalogPanel;
        private ListView catalogList;
        private Label catalogCount;
        private Label catalogStatus;
        private Label catalogEmpty;
        private Label draftStatus;
        private Label dirtyBadge;
        private Label clipLabel;
        private Label timelineValue;
        private Label combatStatus;
        private Label combatDetail;
        private Label validationSummary;
        private Label previewState;
        private Image profilePortrait;
        private Label profileName;
        private Label profileRarity;
        private Label profileId;
        private Label profileType;
        private Label profileHealth;
        private Label profileAttack;
        private Label profileDefense;
        private Label profileSpeed;
        private Label profileMove;
        private Label profileRange;
        private Label profileBasicAttack;
        private Label profileImpact;
        private Label profileSkill;
        private Label profileMainAi;
        private Label profileDistance;
        private Label profileCastleAi;
        private VisualElement validationList;
        private VisualElement validationCard;
        private VisualElement attackButtons;
        private VisualElement previewRenderHost;
        private VisualElement bottomWorkspace;
        private ScrollView commandDetailsScroll;
        private Button sortDefaultButton;
        private Button sortRarityButton;
        private Button pingButton;
        private Button openDraftButton;
        private Button balanceTableButton;
        private Button waveTableButton;
        private Button enemyTableButton;
        private Button catalogToggleButton;
        private Button helpToggleButton;
        private Button pauseButton;
        private Button playActiveButton;
        private DropdownField environmentField;
        private Slider timelineSlider;
        private IMGUIContainer previewIMGUI;
        private MonsterDefinition selectedDefinition;
        private CatalogSortMode catalogSortMode;
        private bool suppressCatalogSelection;
        private bool updatingTimeline;
        private bool isBuildingUi;
        private MonsterMakerPreviewReference selectedPreviewReference;
        private double previewReferenceInfoExpiresAt;

        [MenuItem(MenuPath, false, 21)]
        public static void OpenWindow()
        {
            var window = GetWindow<MonsterMakerV2Window>();
            window.titleContent = new GUIContent("Monster Maker V2");
            window.minSize = new Vector2(MinimumWindowWidth, MinimumWindowHeight);
            window.Show();
        }

        public static void OpenDraft(MonsterMakerDraft source)
        {
            OpenWindow();
            var window = GetWindow<MonsterMakerV2Window>();
            window.OpenDraftInternal(source);
            window.Focus();
        }

        public override void SaveChanges()
        {
            if (TrySaveDraft())
            {
                base.SaveChanges();
            }
        }

        public override void DiscardChanges()
        {
            state?.DiscardChanges();
            BindCurrentDraft();
            ClearRecovery();
            base.DiscardChanges();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Monster Maker V2");
            minSize = new Vector2(MinimumWindowWidth, MinimumWindowHeight);
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            MonsterWorkshopAssignmentEvents.PresetAssigned -= OnWorkshopAssigned;
            MonsterWorkshopAssignmentEvents.PresetAssigned += OnWorkshopAssigned;
        }

        private void OnDisable()
        {
            CaptureRecovery();
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.update -= OnEditorUpdate;
            Undo.undoRedoPerformed -= OnUndoRedo;
            MonsterWorkshopAssignmentEvents.PresetAssigned -= OnWorkshopAssigned;
            draftView?.Unbind();
            draftView = null;
            preview?.Dispose();
            preview = null;
            state?.Dispose();
            state = null;
            previewIMGUI = null;
        }

        public void CreateGUI()
        {
            isBuildingUi = true;
            draftView?.Unbind();
            preview?.Dispose();
            state?.Dispose();
            rootVisualElement.Clear();

            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
            if (layout == null)
            {
                ShowSetupError($"V2 UI 레이아웃을 찾을 수 없습니다.\n{LayoutPath}");
                isBuildingUi = false;
                return;
            }

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (styleSheet == null)
            {
                ShowSetupError($"V2 UI 스타일을 찾을 수 없습니다.\n{StylePath}");
                isBuildingUi = false;
                return;
            }

            while (rootVisualElement.styleSheets.Remove(styleSheet))
            {
                // UXML 재복제 전 V2 스타일만 모두 제거하고 Editor 기본 테마는 보존한다.
            }
            layout.CloneTree(rootVisualElement);

            if (!BindRequiredElements())
            {
                ShowSetupError("V2 UI 요소 이름이 코드 계약과 일치하지 않습니다.");
                isBuildingUi = false;
                return;
            }

            state = new MonsterMakerV2State();
            preview = new MonsterMakerV2PreviewAdapter();
            draftView = new MonsterMakerV2AuthoringView(
                rootVisualElement,
                OpenBasicWorkshop,
                OpenActiveWorkshop,
                ShowBasicAttackArea,
                SynchronizeActiveRuntime,
                OpenPositionAdjust,
                OpenVfxAdjust,
                OpenFeedbackVfxAdjust,
                OpenSfxAdjust);
            CreatePreviewSurface();
            ConfigureCatalogList();
            ConfigureActions();
            ConfigurePreviewControls();
            ReloadCatalog(false);
            if (!RestoreRecovery())
            {
                OpenInitialDraft();
            }

            isBuildingUi = false;
            UpdateAllUi();
        }

        private bool BindRequiredElements()
        {
            searchField = rootVisualElement.Q<TextField>("catalog-search");
            catalogPanel = rootVisualElement.Q<VisualElement>("catalog-panel");
            catalogList = rootVisualElement.Q<ListView>("catalog-list");
            catalogCount = rootVisualElement.Q<Label>("catalog-count");
            catalogStatus = rootVisualElement.Q<Label>("catalog-status");
            catalogEmpty = rootVisualElement.Q<Label>("catalog-empty");
            draftStatus = rootVisualElement.Q<Label>("draft-status");
            dirtyBadge = rootVisualElement.Q<Label>("dirty-badge");
            clipLabel = rootVisualElement.Q<Label>("clip-label");
            timelineValue = rootVisualElement.Q<Label>("timeline-value");
            combatStatus = rootVisualElement.Q<Label>("combat-status");
            combatDetail = rootVisualElement.Q<Label>("combat-detail");
            validationSummary = rootVisualElement.Q<Label>("validation-summary");
            previewState = rootVisualElement.Q<Label>("preview-state");
            profilePortrait = rootVisualElement.Q<Image>("profile-portrait");
            profileName = rootVisualElement.Q<Label>("profile-name");
            profileRarity = rootVisualElement.Q<Label>("profile-rarity");
            profileId = rootVisualElement.Q<Label>("profile-id");
            profileType = rootVisualElement.Q<Label>("profile-type");
            profileHealth = rootVisualElement.Q<Label>("profile-health");
            profileAttack = rootVisualElement.Q<Label>("profile-attack");
            profileDefense = rootVisualElement.Q<Label>("profile-defense");
            profileSpeed = rootVisualElement.Q<Label>("profile-speed");
            profileMove = rootVisualElement.Q<Label>("profile-move");
            profileRange = rootVisualElement.Q<Label>("profile-range");
            profileBasicAttack = rootVisualElement.Q<Label>("profile-basic-attack");
            profileImpact = rootVisualElement.Q<Label>("profile-impact");
            profileSkill = rootVisualElement.Q<Label>("profile-skill");
            profileMainAi = rootVisualElement.Q<Label>("profile-main-ai");
            profileDistance = rootVisualElement.Q<Label>("profile-distance");
            profileCastleAi = rootVisualElement.Q<Label>("profile-castle-ai");
            validationList = rootVisualElement.Q<VisualElement>("validation-list");
            validationCard = rootVisualElement.Q<VisualElement>("validation-card");
            attackButtons = rootVisualElement.Q<VisualElement>("attack-buttons");
            previewRenderHost = rootVisualElement.Q<VisualElement>("preview-render-host");
            bottomWorkspace = rootVisualElement.Q<VisualElement>(className: "bottom-workspace");
            commandDetailsScroll = rootVisualElement.Q<ScrollView>("command-details-scroll");
            sortDefaultButton = rootVisualElement.Q<Button>("sort-default");
            sortRarityButton = rootVisualElement.Q<Button>("sort-rarity");
            pingButton = rootVisualElement.Q<Button>("ping-button");
            openDraftButton = rootVisualElement.Q<Button>("open-draft-button");
            balanceTableButton = rootVisualElement.Q<Button>("balance-table-button");
            waveTableButton = rootVisualElement.Q<Button>("wave-table-button");
            enemyTableButton = rootVisualElement.Q<Button>("enemy-table-button");
            catalogToggleButton = rootVisualElement.Q<Button>("catalog-toggle-button");
            helpToggleButton = rootVisualElement.Q<Button>("help-toggle-button");
            pauseButton = rootVisualElement.Q<Button>("pause-button");
            playActiveButton = rootVisualElement.Q<Button>("play-active");
            environmentField = rootVisualElement.Q<DropdownField>("environment-field");
            timelineSlider = rootVisualElement.Q<Slider>("timeline-slider");

            return searchField != null && catalogList != null && catalogCount != null &&
                   catalogStatus != null && catalogEmpty != null && draftStatus != null &&
                   dirtyBadge != null && clipLabel != null && timelineValue != null &&
                   combatStatus != null && combatDetail != null && validationSummary != null &&
                   previewState != null && profilePortrait != null && profileName != null &&
                   profileRarity != null && profileId != null && profileType != null &&
                   profileHealth != null && profileAttack != null && profileDefense != null &&
                   profileSpeed != null && profileMove != null && profileRange != null &&
                   profileBasicAttack != null && profileImpact != null && profileSkill != null &&
                   profileMainAi != null && profileDistance != null && profileCastleAi != null &&
                   validationList != null && validationCard != null &&
                   attackButtons != null && previewRenderHost != null &&
                   bottomWorkspace != null && commandDetailsScroll != null &&
                   sortDefaultButton != null && sortRarityButton != null && pingButton != null &&
                    catalogPanel != null && openDraftButton != null && balanceTableButton != null &&
                    waveTableButton != null && enemyTableButton != null &&
                    catalogToggleButton != null &&
                    helpToggleButton != null &&
                   pauseButton != null && playActiveButton != null && environmentField != null &&
                   timelineSlider != null;
        }

        private void ConfigureActions()
        {
            searchField.tooltip = "몬스터 이름 또는 ID로 찾습니다.";
            searchField.RegisterValueChangedCallback(evt => ApplySearch(evt.newValue));
            rootVisualElement.Q<Button>("reload-button").clicked += () => ReloadCatalog(true);
            sortDefaultButton.clicked += () => SetCatalogSortMode(CatalogSortMode.Default);
            sortRarityButton.clicked += () => SetCatalogSortMode(CatalogSortMode.Rarity);
            pingButton.clicked += PingSelectedDefinition;
            openDraftButton.clicked += ShowAllDraftMenu;
            balanceTableButton.clicked += OpenBalanceTable;
            waveTableButton.clicked += OpenWaveBalanceTable;
            enemyTableButton.clicked += OpenEnemyBalanceTable;
            catalogToggleButton.clicked += ToggleCatalog;
            helpToggleButton.clicked += ToggleContextHelp;
            rootVisualElement.Q<Button>("new-draft-button").clicked += CreateNewDraft;
            rootVisualElement.Q<Button>("save-draft-button").clicked += () => TrySaveDraft();
            rootVisualElement.Q<Button>("discard-button").clicked += DiscardCurrentChanges;
            rootVisualElement.Q<Button>("undo-button").clicked += Undo.PerformUndo;
            rootVisualElement.Q<Button>("redo-button").clicked += Undo.PerformRedo;
            rootVisualElement.Q<Button>("restore-button").clicked += RestoreInitialDraft;
            rootVisualElement.Q<Button>("validate-button").clicked += ValidateDraft;
            rootVisualElement.Q<Button>("publish-button").clicked += PublishDraft;
            ApplyCatalogVisibility();
            ApplyContextHelpVisibility();
        }

        private void OpenBalanceTable()
        {
            if (state?.IsDirty == true)
            {
                EditorUtility.DisplayDialog(
                    "몬스터 밸런스 표",
                    "현재 Monster Maker에 저장하지 않은 변경이 있습니다. 먼저 원본을 저장하거나 변경을 버린 뒤 밸런스 표를 여세요.",
                    "확인");
                return;
            }

            MonsterBalanceTableWindow.OpenWindow();
        }

        private void OpenWaveBalanceTable()
        {
            if (state?.IsDirty == true)
            {
                EditorUtility.DisplayDialog(
                    "원정대 적 웨이브 표",
                    "현재 Monster Maker에 저장하지 않은 변경이 있습니다. 먼저 원본을 저장하거나 변경을 버린 뒤 웨이브 표를 여세요.",
                    "확인");
                return;
            }

            ExpeditionWaveBalanceTableWindow.OpenWindow();
        }

        private void OpenEnemyBalanceTable()
        {
            if (state?.IsDirty == true)
            {
                EditorUtility.DisplayDialog(
                    "원정대 적 리스트 표",
                    "현재 Monster Maker에 저장하지 않은 변경이 있습니다. 먼저 원본을 저장하거나 변경을 버린 뒤 적 리스트 표를 여세요.",
                    "확인");
                return;
            }

            ExpeditionEnemyBalanceTableWindow.OpenWindow();
        }

        private void ToggleContextHelp()
        {
            MonsterMakerV2HelpPreferences.ShowContextHelp =
                !MonsterMakerV2HelpPreferences.ShowContextHelp;
            ApplyContextHelpVisibility();
        }

        private void ApplyContextHelpVisibility()
        {
            var show = MonsterMakerV2HelpPreferences.ShowContextHelp;
            rootVisualElement.EnableInClassList("maker-root--context-help-hidden", !show);
            if (helpToggleButton == null) return;
            helpToggleButton.text = show ? "도움말 끄기" : "도움말 켜기";
            helpToggleButton.EnableInClassList("help-toggle-button--hidden", !show);
        }

        private void OnProjectChanged()
        {
            if (!isBuildingUi)
            {
                ReloadCatalog(true);
            }
        }

        private void UpdateAllUi()
        {
            UpdateDirtyUi();
            UpdatePreviewStatus();
            UpdateProfileSummary();
            pingButton?.SetEnabled(selectedDefinition != null);
        }

        private void UpdateDirtyUi()
        {
            if (state?.WorkingDraft == null || dirtyBadge == null)
            {
                return;
            }

            hasUnsavedChanges = state.IsDirty;
            saveChangesMessage = "Monster Maker V2 작업 사본을 제작 원본에 저장하시겠습니까?";
            dirtyBadge.text = state.IsDirty ? "변경됨" : state.IsNew ? "새 원본" : "저장됨";
            dirtyBadge.EnableInClassList("state-badge--dirty", state.IsDirty);
            dirtyBadge.EnableInClassList("state-badge--saved", !state.IsDirty);
            var id = string.IsNullOrWhiteSpace(state.WorkingDraft.MonsterId)
                ? "ID 미입력"
                : state.WorkingDraft.MonsterId;
            draftStatus.text = state.IsNew
                ? $"{id} · 메모리 작업 사본 · 원본 미생성"
                : $"{id} · 원본과 분리된 작업 사본";
        }

        private void CaptureRecovery()
        {
            if (state?.WorkingDraft == null)
            {
                return;
            }

            recoverySourcePath = state.SourceDraft == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(state.SourceDraft);
            recoveryWorkingJson = EditorJsonUtility.ToJson(state.WorkingDraft);
            recoveryDirty = state.IsDirty;
            recoveryNew = state.IsNew;
            hasUnsavedChanges = state.IsDirty;
            saveChangesMessage = "Monster Maker V2 작업 사본을 제작 원본에 저장하시겠습니까?";
            SessionState.SetString(RecoverySessionPrefix + "Source", recoverySourcePath);
            SessionState.SetString(RecoverySessionPrefix + "Json", recoveryWorkingJson);
            SessionState.SetBool(RecoverySessionPrefix + "Dirty", recoveryDirty);
            SessionState.SetBool(RecoverySessionPrefix + "New", recoveryNew);
            EditorUtility.SetDirty(this);
        }

        private bool RestoreRecovery()
        {
            var sessionJson = SessionState.GetString(RecoverySessionPrefix + "Json", string.Empty);
            if (!string.IsNullOrWhiteSpace(sessionJson))
            {
                recoverySourcePath = SessionState.GetString(
                    RecoverySessionPrefix + "Source",
                    recoverySourcePath);
                recoveryWorkingJson = sessionJson;
                recoveryDirty = SessionState.GetBool(
                    RecoverySessionPrefix + "Dirty",
                    recoveryDirty);
                recoveryNew = SessionState.GetBool(
                    RecoverySessionPrefix + "New",
                    recoveryNew);
            }

            if (state == null || string.IsNullOrWhiteSpace(recoveryWorkingJson))
            {
                return false;
            }

            var source = recoveryNew || string.IsNullOrWhiteSpace(recoverySourcePath)
                ? null
                : AssetDatabase.LoadAssetAtPath<MonsterMakerDraft>(recoverySourcePath);
            if (source == null && !recoveryNew)
            {
                ClearRecovery();
                return false;
            }

            if (source == null)
            {
                state.CreateNew();
            }
            else
            {
                state.Load(source);
                selectedDefinition = FindDefinition(source.MonsterId);
            }

            if (recoveryDirty)
            {
                state.RestoreRecovery(recoveryWorkingJson, true);
            }

            BindCurrentDraft();
            SelectCatalogEntry(allEntries.Find(entry => entry.Draft == source));
            return true;
        }

        private void ClearRecovery()
        {
            recoverySourcePath = string.Empty;
            recoveryWorkingJson = string.Empty;
            recoveryDirty = false;
            recoveryNew = false;
            hasUnsavedChanges = false;
            SessionState.SetString(RecoverySessionPrefix + "Source", string.Empty);
            SessionState.SetString(RecoverySessionPrefix + "Json", string.Empty);
            SessionState.SetBool(RecoverySessionPrefix + "Dirty", false);
            SessionState.SetBool(RecoverySessionPrefix + "New", false);
            EditorUtility.SetDirty(this);
        }

        private void ShowSetupError(string message)
        {
            rootVisualElement.Clear();
            var error = new Label(message);
            error.style.whiteSpace = WhiteSpace.Normal;
            error.style.marginLeft = 16f;
            error.style.marginRight = 16f;
            error.style.marginTop = 16f;
            rootVisualElement.Add(error);
        }

        private enum CatalogSortMode
        {
            Default,
            Rarity
        }

        private sealed class CatalogEntry
        {
            public CatalogEntry(
                MonsterDefinition definition,
                MonsterMakerDraft draft,
                MonsterRarity? rarity,
                int displayIndex)
            {
                Definition = definition;
                Draft = draft;
                Rarity = rarity;
                DisplayIndex = displayIndex;
            }

            public MonsterDefinition Definition { get; }
            public MonsterMakerDraft Draft { get; }
            public MonsterRarity? Rarity { get; }
            public int DisplayIndex { get; }
        }
    }
}
