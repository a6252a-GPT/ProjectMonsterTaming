using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Audio;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.Audio
{
    public sealed partial class SfxManagerWindow : EditorWindow // 영역별 Cue 관리와 미리듣기
    {
        private enum SfxWorkspaceMode
        {
            Spaces,
            CueLibrary
        }

        public const string MenuPath = "JC Tool/Audio/SFX Manager";

        private const string StylePath = "Assets/ProjectMT/Editor/SfxManager/SfxManagerWindow.uss";
        private const string AllCategoryId = "__all";
        private static readonly Vector2 MinimumWindowSize = new Vector2(1180f, 720f);

        [SerializeField] private string selectedCategoryId = AllCategoryId;
        [SerializeField] private string searchText = string.Empty;
        [SerializeField] private SfxCue selectedCue;
        [SerializeField] private string selectedSpaceId = string.Empty;
        [SerializeField] private SfxWorkspaceMode workspaceMode = SfxWorkspaceMode.Spaces;

        private readonly List<SfxCatalogEntry> filteredEntries = new List<SfxCatalogEntry>();
        private SfxCatalog catalog;
        private ToolbarSearchField searchField;
        private VisualElement categoryList;
        private ListView cueListView;
        private Label emptyLabel;
        private Label totalCountLabel;
        private Label playableCountLabel;
        private Label issueCountLabel;
        private Label visibleCountLabel;
        private Label statusLabel;
        private VisualElement detailHost;
        private bool projectRefreshQueued;

        [MenuItem(MenuPath, false, 175)]
        public static void OpenWindow()
        {
            var window = GetWindow<SfxManagerWindow>();
            window.titleContent = new GUIContent("SFX Manager");
            window.minSize = MinimumWindowSize;
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("SFX Manager");
            minSize = MinimumWindowSize;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            StopPreview();
        }

        private void OnUndoRedo()
        {
            EnsureCatalogAndSynchronize(false);
            EnsureSpaceCatalogAndSynchronize(false);
            RefreshAll();
            SetStatus("실행 취소/다시 실행 결과를 반영했습니다.");
        }

        private void OnProjectChange()
        {
            if (projectRefreshQueued)
            {
                return;
            }

            projectRefreshQueued = true;
            EditorApplication.delayCall += RefreshAfterProjectChange;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (style != null)
            {
                rootVisualElement.styleSheets.Add(style);
            }

            rootVisualElement.name = "sfx-manager-root";
            rootVisualElement.AddToClassList("sfx-root");
            EnsureCatalogAndSynchronize(false);
            EnsureSpaceCatalogAndSynchronize(false);
            ResetViewReferences();
            BuildWindow();
            RefreshAll();
        }

        private void BuildWindow()
        {
            rootVisualElement.Add(BuildHeader());
            rootVisualElement.Add(BuildModeSwitch());
            rootVisualElement.Add(BuildToolbar());

            var body = new VisualElement { name = "sfx-manager-body" };
            body.AddToClassList("sfx-body");
            body.Add(BuildCategoryPanel());
            body.Add(workspaceMode == SfxWorkspaceMode.Spaces ? BuildSpacePanel() : BuildCuePanel());

            detailHost = new VisualElement { name = "sfx-detail-host" };
            detailHost.AddToClassList("sfx-detail-host");
            body.Add(detailHost);
            rootVisualElement.Add(body);
            rootVisualElement.Add(BuildStatusBar());
        }

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("sfx-header");

            var titleArea = new VisualElement();
            titleArea.AddToClassList("sfx-title-area");
            var eyebrow = new Label("PROJECT MT  /  AUDIO WORKSPACE");
            eyebrow.AddToClassList("sfx-eyebrow");
            var title = new Label("SFX Manager");
            title.AddToClassList("sfx-title");
            var subtitle = new Label(workspaceMode == SfxWorkspaceMode.Spaces
                ? "110개 게임 사건을 영역별로 훑고, 기존 공간을 지키면서 사운드 결정을 기록합니다."
                : "기존 Cue를 그대로 모아 원본 사운드·볼륨·공간감·재생 제한을 관리합니다.");
            subtitle.AddToClassList("sfx-subtitle");
            titleArea.Add(eyebrow);
            titleArea.Add(title);
            titleArea.Add(subtitle);
            header.Add(titleArea);

            var stats = new VisualElement();
            stats.AddToClassList("sfx-stats");
            totalCountLabel = MakeStat("전체", "0");
            playableCountLabel = MakeStat(
                workspaceMode == SfxWorkspaceMode.Spaces ? "결정 완료" : "재생 가능",
                "0");
            issueCountLabel = MakeStat(
                workspaceMode == SfxWorkspaceMode.Spaces ? "결정 필요" : "확인 필요",
                "0");
            stats.Add(totalCountLabel.parent);
            stats.Add(playableCountLabel.parent);
            stats.Add(issueCountLabel.parent);
            header.Add(stats);
            return header;
        }

        private VisualElement BuildModeSwitch()
        {
            var bar = new VisualElement { name = "sfx-mode-switch" };
            bar.AddToClassList("sfx-mode-switch");
            bar.Add(MakeModeButton(
                SfxWorkspaceMode.Spaces,
                "공간 배정",
                spaceCatalog?.Entries.Count ?? 0,
                "게임 사건별 필요 공간과 결정 상태"));
            bar.Add(MakeModeButton(
                SfxWorkspaceMode.CueLibrary,
                "Cue 보관함",
                catalog?.Entries.Count(entry => entry?.Cue != null) ?? 0,
                "실제 재생 자산과 볼륨 설정"));
            return bar;
        }

        private Button MakeModeButton(SfxWorkspaceMode mode, string label, int count, string tooltip)
        {
            var button = new Button(() => SetWorkspaceMode(mode));
            button.tooltip = tooltip;
            button.AddToClassList("sfx-mode-button");
            button.EnableInClassList("sfx-mode-button--selected", workspaceMode == mode);
            var title = new Label(label);
            title.AddToClassList("sfx-mode-title");
            var badge = new Label(count.ToString());
            badge.AddToClassList("sfx-mode-count");
            button.Add(title);
            button.Add(badge);
            return button;
        }

        private void SetWorkspaceMode(SfxWorkspaceMode mode)
        {
            if (workspaceMode == mode)
            {
                return;
            }

            workspaceMode = mode;
            selectedCategoryId = AllCategoryId;
            searchText = string.Empty;
            StopPreview();
            rootVisualElement.Clear();
            ResetViewReferences();
            BuildWindow();
            RefreshAll();
            SetStatus(mode == SfxWorkspaceMode.Spaces
                ? "공간 배정으로 전환했습니다. 기존 시스템 공간과 새 연결 필요 공간을 함께 표시합니다."
                : "Cue 보관함으로 전환했습니다. 기존 Cue 자산과 설정은 그대로 유지됩니다.");
        }

        private void ResetViewReferences()
        {
            searchField = null;
            categoryList = null;
            cueListView = null;
            spaceListView = null;
            emptyLabel = null;
            spaceEmptyLabel = null;
            totalCountLabel = null;
            playableCountLabel = null;
            issueCountLabel = null;
            visibleCountLabel = null;
            statusLabel = null;
            detailHost = null;
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("sfx-toolbar");

            searchField = new ToolbarSearchField { value = searchText ?? string.Empty };
            searchField.name = "sfx-search-field";
            searchField.AddToClassList("sfx-search");
            searchField.tooltip = workspaceMode == SfxWorkspaceMode.Spaces
                ? "공간 ID, 영역, 게임 사건, 근거, 메모, 배정 Cue 검색"
                : "Cue 이름, AudioClip 이름, 에셋 경로 검색";
            searchField.RegisterValueChangedCallback(evt =>
            {
                searchText = evt.newValue ?? string.Empty;
                RefreshFilteredEntries();
            });
            toolbar.Add(searchField);

            visibleCountLabel = new Label("0개 표시");
            visibleCountLabel.AddToClassList("sfx-visible-count");
            toolbar.Add(visibleCountLabel);

            var spacer = new VisualElement();
            spacer.AddToClassList("sfx-toolbar-spacer");
            toolbar.Add(spacer);

            var refresh = new Button(() =>
            {
                if (workspaceMode == SfxWorkspaceMode.Spaces)
                {
                    EnsureSpaceCatalogAndSynchronize(true);
                }
                else
                {
                    EnsureCatalogAndSynchronize(true);
                }
            })
            {
                text = workspaceMode == SfxWorkspaceMode.Spaces ? "공간 다시 읽기" : "Cue 다시 찾기"
            };
            refresh.name = "refresh-sfx-catalog";
            refresh.tooltip = workspaceMode == SfxWorkspaceMode.Spaces
                ? "전수조사 110개 정의를 다시 읽되 기존 결정과 메모는 보존합니다."
                : "프로젝트의 기존 SfxCue를 다시 찾아 누락된 항목만 등록합니다.";
            refresh.AddToClassList("sfx-toolbar-button");
            toolbar.Add(refresh);

            var create = new Button(workspaceMode == SfxWorkspaceMode.Spaces
                ? () => SetWorkspaceMode(SfxWorkspaceMode.CueLibrary)
                : CreateNewCue)
            {
                text = workspaceMode == SfxWorkspaceMode.Spaces ? "Cue 보관함 열기" : "+ 새 Cue"
            };
            create.name = "create-sfx-cue";
            create.tooltip = workspaceMode == SfxWorkspaceMode.Spaces
                ? "기존 Cue를 고르거나 새 Cue를 만들 수 있는 보관함으로 이동합니다."
                : "현재 영역 폴더에 새 SfxCue 자산을 만듭니다.";
            create.AddToClassList("sfx-primary-button");
            toolbar.Add(create);

            var save = new Button(SaveAllChanges) { text = "변경 저장" };
            save.name = "save-sfx-catalog";
            save.AddToClassList("sfx-save-button");
            toolbar.Add(save);
            return toolbar;
        }

        private VisualElement BuildCategoryPanel()
        {
            var panel = new VisualElement { name = "sfx-category-panel" };
            panel.AddToClassList("sfx-category-panel");

            var heading = new Label(workspaceMode == SfxWorkspaceMode.Spaces ? "사건 영역" : "Cue 영역");
            heading.AddToClassList("sfx-panel-heading");
            panel.Add(heading);

            categoryList = new ScrollView(ScrollViewMode.Vertical);
            categoryList.name = "sfx-category-list";
            categoryList.AddToClassList("sfx-category-list");
            panel.Add(categoryList);

            var hint = new Label(workspaceMode == SfxWorkspaceMode.Spaces
                ? "기존 공간은 원본 소유 구조를 유지합니다. Cue 결정만 별도로 기록합니다."
                : "카테고리를 바꿔도 Cue 파일과 기존 참조는 이동하거나 끊지 않습니다.");
            hint.AddToClassList("sfx-category-hint");
            panel.Add(hint);
            return panel;
        }

        private VisualElement BuildCuePanel()
        {
            var panel = new VisualElement { name = "sfx-cue-panel" };
            panel.AddToClassList("sfx-cue-panel");

            var heading = new VisualElement();
            heading.AddToClassList("sfx-cue-heading");
            var title = new Label("SFX Cue");
            title.AddToClassList("sfx-panel-heading");
            var description = new Label("이름순 · 더블클릭하면 Project에서 선택");
            description.AddToClassList("sfx-heading-note");
            heading.Add(title);
            heading.Add(description);
            panel.Add(heading);

            cueListView = new ListView
            {
                name = "sfx-cue-list",
                itemsSource = filteredEntries,
                fixedItemHeight = 70f,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                makeItem = MakeCueRow,
                bindItem = BindCueRow
            };
            cueListView.AddToClassList("sfx-cue-list");
            cueListView.selectionChanged += OnCueSelectionChanged;
            cueListView.itemsChosen += chosen =>
            {
                var entry = chosen.OfType<SfxCatalogEntry>().FirstOrDefault();
                if (entry?.Cue != null)
                {
                    Selection.activeObject = entry.Cue;
                    EditorGUIUtility.PingObject(entry.Cue);
                }
            };
            panel.Add(cueListView);

            emptyLabel = new Label("이 영역에는 표시할 Cue가 없습니다.\n상단의 ‘새 Cue’로 만들거나 ‘Cue 다시 찾기’를 눌러보세요.");
            emptyLabel.AddToClassList("sfx-empty-label");
            panel.Add(emptyLabel);
            return panel;
        }

        private VisualElement BuildStatusBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("sfx-status-bar");
            statusLabel = new Label("기존 Cue는 참조와 설정을 그대로 유지합니다.");
            statusLabel.AddToClassList("sfx-status");
            bar.Add(statusLabel);
            return bar;
        }

        private VisualElement MakeCueRow()
        {
            var row = new VisualElement();
            row.AddToClassList("sfx-cue-row");

            var accent = new VisualElement();
            accent.AddToClassList("sfx-row-accent");
            row.Add(accent);

            var content = new VisualElement();
            content.AddToClassList("sfx-row-content");
            var top = new VisualElement();
            top.AddToClassList("sfx-row-top");
            var name = new Label();
            name.AddToClassList("sfx-row-name");
            var badge = new Label();
            badge.AddToClassList("sfx-row-badge");
            top.Add(name);
            top.Add(badge);
            var path = new Label();
            path.AddToClassList("sfx-row-path");
            var meta = new Label();
            meta.AddToClassList("sfx-row-meta");
            content.Add(top);
            content.Add(path);
            content.Add(meta);
            row.Add(content);

            Button play = null;
            play = new Button(() => PreviewCue(play.userData as SfxCue)) { text = "▶" };
            play.tooltip = "현재 Cue 미리듣기";
            play.AddToClassList("sfx-row-play");
            row.Add(play);
            row.userData = new CueRowElements(accent, name, badge, path, meta, play);
            return row;
        }

        private void BindCueRow(VisualElement element, int index)
        {
            if (index < 0 || index >= filteredEntries.Count || element.userData is not CueRowElements row)
            {
                return;
            }

            var entry = filteredEntries[index];
            var cue = entry?.Cue;
            if (cue == null)
            {
                return;
            }

            var category = FindCategory(entry.CategoryId);
            var path = AssetDatabase.GetAssetPath(cue);
            var clipCount = CountPlayableClips(cue);
            var generated = !AssetDatabase.IsMainAsset(cue);
            row.Accent.style.backgroundColor = category?.AccentColor ?? new Color(0.4f, 0.45f, 0.5f);
            row.Name.text = cue.name;
            row.Badge.text = generated ? "내장 Cue" : category?.DisplayName ?? "미분류";
            row.Badge.EnableInClassList("sfx-row-badge--warning", generated);
            row.Path.text = BuildCompactPath(path);
            row.Meta.text = clipCount == 0
                ? "사운드 없음 · 확인 필요"
                : $"사운드 {clipCount}개  ·  볼륨 {FormatVolume(cue.VolumeRange)}  ·  {(cue.SpatialBlend < 0.5f ? "2D" : "3D")}";
            row.Meta.EnableInClassList("sfx-row-meta--warning", clipCount == 0);
            row.Play.userData = cue;
            row.Play.SetEnabled(clipCount > 0);
        }

        private void OnCueSelectionChanged(IEnumerable<object> selected)
        {
            var entry = selected.OfType<SfxCatalogEntry>().FirstOrDefault();
            selectedCue = entry?.Cue;
            RefreshDetails();
        }

        private void RefreshAll()
        {
            RefreshCategoryButtons();
            RefreshFilteredEntries();
            RefreshHeaderCounts();
            RefreshDetails();
        }

        private void RefreshCategoryButtons()
        {
            if (categoryList == null || catalog == null)
            {
                return;
            }

            categoryList.Clear();
            var allCount = workspaceMode == SfxWorkspaceMode.Spaces
                ? spaceCatalog?.Entries.Count ?? 0
                : catalog.Entries.Count;
            categoryList.Add(MakeCategoryButton(
                AllCategoryId,
                workspaceMode == SfxWorkspaceMode.Spaces ? "모든 공간" : "모든 SFX",
                allCount));
            foreach (var category in catalog.Categories.Where(category => category != null))
            {
                var count = workspaceMode == SfxWorkspaceMode.Spaces
                    ? spaceCatalog?.Entries.Count(entry => entry != null && entry.CategoryId == category.Id) ?? 0
                    : catalog.Entries.Count(entry => entry != null && entry.CategoryId == category.Id);
                categoryList.Add(MakeCategoryButton(category.Id, category.DisplayName, count, category.AccentColor));
            }
        }

        private Button MakeCategoryButton(
            string categoryId,
            string label,
            int count,
            Color? accent = null)
        {
            var button = new Button(() => SelectCategory(categoryId));
            button.AddToClassList("sfx-category-button");
            button.EnableInClassList("sfx-category-button--selected", selectedCategoryId == categoryId);

            var dot = new VisualElement();
            dot.AddToClassList("sfx-category-dot");
            dot.style.backgroundColor = accent ?? new Color(0.38f, 0.68f, 0.82f);
            var text = new Label(label);
            text.AddToClassList("sfx-category-name");
            var badge = new Label(count.ToString());
            badge.AddToClassList("sfx-category-count");
            button.Add(dot);
            button.Add(text);
            button.Add(badge);
            return button;
        }

        private void SelectCategory(string categoryId)
        {
            selectedCategoryId = string.IsNullOrWhiteSpace(categoryId) ? AllCategoryId : categoryId;
            RefreshCategoryButtons();
            RefreshFilteredEntries();
        }

        private void RefreshFilteredEntries()
        {
            if (workspaceMode == SfxWorkspaceMode.Spaces)
            {
                RefreshFilteredSpaces();
                return;
            }

            filteredEntries.Clear();
            if (catalog != null)
            {
                filteredEntries.AddRange(catalog.Entries
                    .Where(MatchesSelectedCategory)
                    .Where(MatchesSearch)
                    .OrderBy(entry => entry.Cue != null ? entry.Cue.name : string.Empty, StringComparer.OrdinalIgnoreCase));
            }

            cueListView?.Rebuild();
            if (visibleCountLabel != null)
            {
                visibleCountLabel.text = $"{filteredEntries.Count}개 표시";
            }
            if (emptyLabel != null)
            {
                emptyLabel.style.display = filteredEntries.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            var selectedIndex = filteredEntries.FindIndex(entry => entry?.Cue == selectedCue);
            if (selectedIndex >= 0)
            {
                cueListView?.SetSelectionWithoutNotify(new[] { selectedIndex });
                cueListView?.ScrollToItem(selectedIndex);
            }
            else
            {
                cueListView?.ClearSelection();
            }
        }

        private bool MatchesSelectedCategory(SfxCatalogEntry entry)
        {
            return entry?.Cue != null &&
                   (selectedCategoryId == AllCategoryId || entry.CategoryId == selectedCategoryId);
        }

        private bool MatchesSearch(SfxCatalogEntry entry)
        {
            if (entry?.Cue == null || string.IsNullOrWhiteSpace(searchText))
            {
                return entry?.Cue != null;
            }

            var query = searchText.Trim();
            if (entry.Cue.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                AssetDatabase.GetAssetPath(entry.Cue).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return entry.Cue.Clips != null && entry.Cue.Clips.Any(
                clip => clip != null && clip.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void RefreshHeaderCounts()
        {
            if (catalog == null || totalCountLabel == null || playableCountLabel == null || issueCountLabel == null)
            {
                return;
            }

            if (workspaceMode == SfxWorkspaceMode.Spaces)
            {
                var spaces = spaceCatalog?.Entries.Where(entry => entry != null).ToArray() ??
                             Array.Empty<SfxSpaceEntry>();
                var closed = spaces.Count(entry => entry.IsDecisionClosed);
                totalCountLabel.text = spaces.Length.ToString();
                playableCountLabel.text = closed.ToString();
                issueCountLabel.text = (spaces.Length - closed).ToString();
                return;
            }

            var entries = catalog.Entries.Where(entry => entry?.Cue != null).ToArray();
            var playable = entries.Count(entry => entry.Cue.HasPlayableClip);
            totalCountLabel.text = entries.Length.ToString();
            playableCountLabel.text = playable.ToString();
            issueCountLabel.text = (entries.Length - playable).ToString();
        }

        private SfxCatalogCategory FindCategory(string categoryId)
        {
            return catalog?.Categories.FirstOrDefault(
                category => category != null && category.Id == categoryId);
        }

        private static Label MakeStat(string label, string value)
        {
            var card = new VisualElement();
            card.AddToClassList("sfx-stat-card");
            var valueLabel = new Label(value);
            valueLabel.AddToClassList("sfx-stat-value");
            var nameLabel = new Label(label);
            nameLabel.AddToClassList("sfx-stat-label");
            card.Add(valueLabel);
            card.Add(nameLabel);
            return valueLabel;
        }

        private void SetStatus(string message, bool warning = false, bool error = false)
        {
            if (statusLabel == null)
            {
                return;
            }

            statusLabel.text = message;
            statusLabel.EnableInClassList("sfx-status--warning", warning && !error);
            statusLabel.EnableInClassList("sfx-status--error", error);
        }

        private void RefreshAfterProjectChange()
        {
            projectRefreshQueued = false;
            if (this == null || rootVisualElement == null)
            {
                return;
            }

            EnsureCatalogAndSynchronize(false);
            EnsureSpaceCatalogAndSynchronize(false);
            RefreshAll();
        }

        private sealed class CueRowElements
        {
            public CueRowElements(
                VisualElement accent,
                Label name,
                Label badge,
                Label path,
                Label meta,
                Button play)
            {
                Accent = accent;
                Name = name;
                Badge = badge;
                Path = path;
                Meta = meta;
                Play = play;
            }

            public VisualElement Accent { get; }
            public Label Name { get; }
            public Label Badge { get; }
            public Label Path { get; }
            public Label Meta { get; }
            public Button Play { get; }
        }
    }
}
