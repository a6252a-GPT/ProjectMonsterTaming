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
    public sealed partial class SfxManagerWindow
    {
        private const string SpaceInventoryPath = "Assets/ProjectMT/Editor/SfxManager/SfxSpaceInventory.txt";
        private const string SpaceCatalogPath = CatalogDirectory + "/SfxSpaceCatalog_ProjectMT.asset";

        private readonly List<SfxSpaceEntry> filteredSpaces = new List<SfxSpaceEntry>();
        private SfxSpaceCatalog spaceCatalog;
        private SfxSpaceEntry selectedSpace;
        private ListView spaceListView;
        private Label spaceEmptyLabel;

        private void EnsureSpaceCatalogAndSynchronize(bool userInitiated)
        {
            EnsureFolder(CatalogDirectory);
            var definitions = LoadSpaceDefinitions(out var loadError);
            if (!string.IsNullOrWhiteSpace(loadError))
            {
                SetStatus(loadError, false, true);
                return;
            }

            var created = false;
            spaceCatalog = AssetDatabase.LoadAssetAtPath<SfxSpaceCatalog>(SpaceCatalogPath);
            if (spaceCatalog == null)
            {
                spaceCatalog = CreateInstance<SfxSpaceCatalog>();
                spaceCatalog.name = "SfxSpaceCatalog_ProjectMT";
                AssetDatabase.CreateAsset(spaceCatalog, SpaceCatalogPath);
                created = true;
            }

            var changed = spaceCatalog.EditorSynchronize(definitions);
            if (created || changed)
            {
                EditorUtility.SetDirty(spaceCatalog);
                AssetDatabase.SaveAssets();
            }

            selectedSpace = string.IsNullOrWhiteSpace(selectedSpaceId)
                ? null
                : spaceCatalog.EditorFindEntry(selectedSpaceId);
            if (userInitiated)
            {
                SetStatus($"전수조사 기준 SFX 공간 {spaceCatalog.Entries.Count}개를 다시 읽었습니다.");
                RefreshAll();
            }
        }

        private static IReadOnlyList<SfxSpaceDefinition> LoadSpaceDefinitions(out string error)
        {
            error = string.Empty;
            var inventory = AssetDatabase.LoadAssetAtPath<TextAsset>(SpaceInventoryPath);
            if (inventory == null)
            {
                error = $"SFX 공간 인벤토리를 찾지 못했습니다: {SpaceInventoryPath}";
                return Array.Empty<SfxSpaceDefinition>();
            }

            var definitions = new List<SfxSpaceDefinition>();
            var lines = inventory.text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var columns = line.Split('\t');
                if (columns.Length != 8 ||
                    !Enum.TryParse(columns[1], out SfxSpacePriority priority) ||
                    !Enum.TryParse(columns[5], out SfxSpaceCoverageState coverage))
                {
                    error = $"SFX 공간 인벤토리 {index + 1}행 형식이 올바르지 않습니다.";
                    return Array.Empty<SfxSpaceDefinition>();
                }

                definitions.Add(new SfxSpaceDefinition(
                    columns[0],
                    priority,
                    columns[2],
                    columns[3],
                    columns[4],
                    coverage,
                    columns[6],
                    columns[7]));
            }

            if (definitions.Count != 110)
            {
                error = $"SFX 공간 인벤토리는 110개여야 합니다. 현재 {definitions.Count}개입니다.";
                return Array.Empty<SfxSpaceDefinition>();
            }

            return definitions;
        }

        private VisualElement BuildSpacePanel()
        {
            var panel = new VisualElement { name = "sfx-space-panel" };
            panel.AddToClassList("sfx-cue-panel");
            panel.AddToClassList("sfx-space-panel");

            var heading = new VisualElement();
            heading.AddToClassList("sfx-cue-heading");
            var title = new Label("SFX 공간");
            title.AddToClassList("sfx-panel-heading");
            var description = new Label("우선순위 · 현재 연결 · 결정 상태");
            description.AddToClassList("sfx-heading-note");
            heading.Add(title);
            heading.Add(description);
            panel.Add(heading);

            spaceListView = new ListView
            {
                name = "sfx-space-list",
                itemsSource = filteredSpaces,
                fixedItemHeight = 84f,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                makeItem = MakeSpaceRow,
                bindItem = BindSpaceRow
            };
            spaceListView.AddToClassList("sfx-cue-list");
            spaceListView.AddToClassList("sfx-space-list");
            spaceListView.selectionChanged += OnSpaceSelectionChanged;
            panel.Add(spaceListView);

            spaceEmptyLabel = new Label("이 영역에는 표시할 SFX 공간이 없습니다.\n검색어와 영역 필터를 확인해보세요.");
            spaceEmptyLabel.AddToClassList("sfx-empty-label");
            panel.Add(spaceEmptyLabel);
            return panel;
        }

        private VisualElement MakeSpaceRow()
        {
            var row = new VisualElement();
            row.AddToClassList("sfx-cue-row");
            row.AddToClassList("sfx-space-row");

            var accent = new VisualElement();
            accent.AddToClassList("sfx-row-accent");
            row.Add(accent);

            var content = new VisualElement();
            content.AddToClassList("sfx-row-content");
            var top = new VisualElement();
            top.AddToClassList("sfx-row-top");
            var id = new Label();
            id.AddToClassList("sfx-space-id");
            var priority = new Label();
            priority.AddToClassList("sfx-space-priority");
            var coverage = new Label();
            coverage.AddToClassList("sfx-row-badge");
            top.Add(id);
            top.Add(priority);
            top.Add(coverage);

            var name = new Label();
            name.AddToClassList("sfx-row-name");
            var meta = new Label();
            meta.AddToClassList("sfx-row-meta");
            content.Add(top);
            content.Add(name);
            content.Add(meta);
            row.Add(content);
            row.userData = new SpaceRowElements(accent, id, priority, coverage, name, meta);
            return row;
        }

        private void BindSpaceRow(VisualElement element, int index)
        {
            if (index < 0 || index >= filteredSpaces.Count || element.userData is not SpaceRowElements row)
            {
                return;
            }

            var entry = filteredSpaces[index];
            var category = FindCategory(entry.CategoryId);
            row.Accent.style.backgroundColor = category?.AccentColor ?? new Color(0.4f, 0.45f, 0.5f);
            row.Id.text = entry.Id;
            row.Priority.text = entry.Priority.ToString();
            row.Priority.EnableInClassList("sfx-space-priority--p0", entry.Priority == SfxSpacePriority.P0);
            row.Coverage.text = CoverageLabel(entry.CoverageState);
            row.Coverage.EnableInClassList("sfx-space-coverage--connected", entry.CoverageState == SfxSpaceCoverageState.Connected);
            row.Coverage.EnableInClassList("sfx-space-coverage--warning", entry.CoverageState == SfxSpaceCoverageState.Partial || entry.CoverageState == SfxSpaceCoverageState.EmptySlot);
            row.Coverage.EnableInClassList("sfx-space-coverage--missing", entry.CoverageState == SfxSpaceCoverageState.MissingHook);
            row.Name.text = entry.EventName;
            row.Meta.text = $"{entry.Area}  ·  {AssignmentLabel(entry)}";
            row.Meta.EnableInClassList("sfx-row-meta--warning", !entry.IsDecisionClosed);
        }

        private void OnSpaceSelectionChanged(IEnumerable<object> selected)
        {
            selectedSpace = selected.OfType<SfxSpaceEntry>().FirstOrDefault();
            selectedSpaceId = selectedSpace?.Id ?? string.Empty;
            RefreshDetails();
        }

        private void RefreshFilteredSpaces()
        {
            filteredSpaces.Clear();
            if (spaceCatalog != null)
            {
                filteredSpaces.AddRange(spaceCatalog.Entries
                    .Where(entry => entry != null)
                    .Where(entry => selectedCategoryId == AllCategoryId || entry.CategoryId == selectedCategoryId)
                    .Where(MatchesSpaceSearch)
                    .OrderBy(entry => entry.Priority)
                    .ThenBy(entry => entry.Id, StringComparer.Ordinal));
            }

            spaceListView?.Rebuild();
            if (visibleCountLabel != null)
            {
                visibleCountLabel.text = $"{filteredSpaces.Count}개 표시";
            }

            if (spaceEmptyLabel != null)
            {
                spaceEmptyLabel.style.display = filteredSpaces.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            var selectedIndex = filteredSpaces.FindIndex(entry => entry == selectedSpace);
            if (selectedIndex >= 0)
            {
                spaceListView?.SetSelectionWithoutNotify(new[] { selectedIndex });
                spaceListView?.ScrollToItem(selectedIndex);
            }
            else
            {
                spaceListView?.ClearSelection();
            }
        }

        private bool MatchesSpaceSearch(SfxSpaceEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(searchText))
            {
                return entry != null;
            }

            var query = searchText.Trim();
            return Contains(entry.Id, query) ||
                   Contains(entry.Area, query) ||
                   Contains(entry.EventName, query) ||
                   Contains(entry.Evidence, query) ||
                   Contains(entry.Note, query) ||
                   (entry.Cue != null && Contains(entry.Cue.name, query));
        }

        private static bool Contains(string source, string query)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                   source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private VisualElement BuildNoSpaceSelectionState()
        {
            var empty = new VisualElement();
            empty.AddToClassList("sfx-detail-empty");
            var icon = new Label("◎");
            icon.AddToClassList("sfx-detail-empty-icon");
            var title = new Label("관리할 SFX 공간을 선택하세요");
            title.AddToClassList("sfx-detail-empty-title");
            var description = new Label("영역과 우선순위로 찾은 뒤\n미결정·사용 안 함·Cue 배정을 기록할 수 있습니다.");
            description.AddToClassList("sfx-detail-empty-description");
            empty.Add(icon);
            empty.Add(title);
            empty.Add(description);
            return empty;
        }

        private VisualElement BuildSelectedSpaceDetails()
        {
            var root = new VisualElement();
            root.AddToClassList("sfx-details");
            root.Add(BuildSpaceDetailHeader());

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.name = "sfx-space-detail-scroll";
            scroll.AddToClassList("sfx-detail-scroll");
            scroll.Add(BuildSpaceCoverageSection());
            scroll.Add(BuildSpaceAssignmentSection());
            scroll.Add(BuildSpaceNoteSection());
            root.Add(scroll);
            return root;
        }

        private VisualElement BuildSpaceDetailHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("sfx-detail-header");

            var text = new VisualElement();
            text.AddToClassList("sfx-detail-title-area");
            var eyebrow = new Label($"{selectedSpace.Id}  ·  {selectedSpace.Priority}");
            eyebrow.AddToClassList("sfx-detail-eyebrow");
            var title = new Label(selectedSpace.EventName);
            title.AddToClassList("sfx-detail-title");
            var path = new Label($"{selectedSpace.Area}  /  {FindCategory(selectedSpace.CategoryId)?.DisplayName ?? selectedSpace.CategoryId}");
            path.AddToClassList("sfx-detail-path");
            text.Add(eyebrow);
            text.Add(title);
            text.Add(path);
            header.Add(text);

            if (selectedSpace.Cue != null)
            {
                var play = new Button(() => PreviewCue(selectedSpace.Cue)) { text = "▶ 배정 Cue" };
                play.SetEnabled(selectedSpace.Cue.HasPlayableClip);
                play.AddToClassList("sfx-preview-button");
                header.Add(play);
            }

            return header;
        }

        private VisualElement BuildSpaceCoverageSection()
        {
            var section = MakeSection("현재 공간", "실제 코드·Prefab·ScriptableObject 전수조사 기준입니다.");
            var badges = new VisualElement();
            badges.AddToClassList("sfx-origin-row");
            var coverage = new Label(CoverageLabel(selectedSpace.CoverageState));
            coverage.AddToClassList(selectedSpace.HasExistingOwner ? "sfx-origin-badge" : "sfx-origin-badge--generated");
            var owner = new Label(selectedSpace.HasExistingOwner ? "기존 시스템 소유" : "새 연결 필요");
            owner.AddToClassList(selectedSpace.HasExistingOwner ? "sfx-saved-badge" : "sfx-dirty-badge");
            badges.Add(coverage);
            badges.Add(owner);
            section.Add(badges);

            var evidence = new Label(selectedSpace.Evidence);
            evidence.AddToClassList("sfx-space-copy");
            section.Add(evidence);
            var existingCueReferences = BuildExistingCueReferences(selectedSpace);
            if (existingCueReferences != null)
            {
                section.Add(existingCueReferences);
            }
            section.Add(MakeNotice(OwnerGuidance(selectedSpace), !selectedSpace.HasExistingOwner));
            return section;
        }

        private VisualElement BuildExistingCueReferences(SfxSpaceEntry entry)
        {
            if (catalog == null || (entry.Id != "CR-01" && entry.Id != "COM-13"))
            {
                return null;
            }

            var related = catalog.Entries
                .Where(candidate => candidate?.Cue != null && candidate.CategoryId == entry.CategoryId)
                .Select(candidate => candidate.Cue)
                .OrderBy(cue => cue.name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (related.Length == 0)
            {
                return null;
            }

            var root = new VisualElement();
            root.AddToClassList("sfx-existing-cues");
            var label = new Label($"기존 연결 Cue  ·  {related.Length}개");
            label.AddToClassList("sfx-existing-cues-title");
            root.Add(label);
            foreach (var cue in related)
            {
                var button = new Button(() =>
                {
                    selectedCue = cue;
                    SetWorkspaceMode(SfxWorkspaceMode.CueLibrary);
                })
                {
                    text = $"↗  {cue.name}"
                };
                button.tooltip = "기존 Cue를 변경하지 않고 Cue 보관함에서 엽니다.";
                button.AddToClassList("sfx-existing-cue-button");
                root.Add(button);
            }

            return root;
        }

        private VisualElement BuildSpaceAssignmentSection()
        {
            var section = MakeSection("사운드 결정", "Cue를 고르는 일과 Runtime 연결 완료는 별도 상태로 관리합니다.");
            var stateLabels = new List<string> { "미결정", "사용 안 함", "Cue 배정" };
            var stateField = new DropdownField("결정", stateLabels, (int)selectedSpace.AssignmentState);
            stateField.name = "sfx-space-state-field";
            stateField.AddToClassList("sfx-field");
            stateField.RegisterValueChangedCallback(evt =>
            {
                var next = (SfxSpaceAssignmentState)Mathf.Max(0, stateLabels.IndexOf(evt.newValue));
                SetSpaceAssignment(next, next == SfxSpaceAssignmentState.Assigned ? selectedSpace.Cue : null);
            });
            section.Add(stateField);

            var cueField = new ObjectField("배정 Cue")
            {
                name = "sfx-space-cue-field",
                objectType = typeof(SfxCue),
                allowSceneObjects = false,
                value = selectedSpace.Cue
            };
            cueField.AddToClassList("sfx-field");
            cueField.RegisterValueChangedCallback(evt =>
            {
                var nextCue = evt.newValue as SfxCue;
                SetSpaceAssignment(
                    nextCue != null ? SfxSpaceAssignmentState.Assigned : SfxSpaceAssignmentState.Undecided,
                    nextCue);
            });
            section.Add(cueField);

            var actions = new VisualElement();
            actions.AddToClassList("sfx-space-actions");
            var library = new Button(() => SetWorkspaceMode(SfxWorkspaceMode.CueLibrary)) { text = "Cue 보관함 열기" };
            library.AddToClassList("sfx-secondary-button");
            actions.Add(library);
            if (selectedSpace.Cue != null)
            {
                var locate = new Button(() =>
                {
                    Selection.activeObject = selectedSpace.Cue;
                    EditorGUIUtility.PingObject(selectedSpace.Cue);
                }) { text = "배정 Cue 위치 표시" };
                locate.AddToClassList("sfx-secondary-button");
                actions.Add(locate);
            }
            section.Add(actions);

            var dropZone = new VisualElement { name = "sfx-space-audio-drop-zone" };
            dropZone.AddToClassList("sfx-drop-zone");
            dropZone.AddToClassList("sfx-space-drop-zone");
            var dropTitle = new Label("AudioClip을 여기에 놓기");
            dropTitle.AddToClassList("sfx-drop-title");
            var dropDescription = new Label("빈 Cue를 만들지 않고, 사운드가 들어간 새 Cue를 생성해 이 공간에 배정합니다.");
            dropDescription.AddToClassList("sfx-drop-description");
            dropZone.Add(dropTitle);
            dropZone.Add(dropDescription);
            RegisterSpaceAudioDropZone(dropZone);
            section.Add(dropZone);

            if (selectedSpace.AssignmentState == SfxSpaceAssignmentState.Assigned && selectedSpace.Cue == null)
            {
                section.Add(MakeNotice("Cue 배정을 선택했습니다. 기존 Cue를 고르거나 AudioClip을 드래그하세요.", true));
            }

            return section;
        }

        private VisualElement BuildSpaceNoteSection()
        {
            var section = MakeSection("완료 기준과 메모", "실제 재생 연결까지 확인할 때 사용할 체크 기준입니다.");
            var criteria = new Label(selectedSpace.CompletionCriteria);
            criteria.AddToClassList("sfx-space-criteria");
            section.Add(criteria);

            var note = new TextField("작업 메모")
            {
                name = "sfx-space-note-field",
                multiline = true,
                value = selectedSpace.Note ?? string.Empty
            };
            note.AddToClassList("sfx-field");
            note.AddToClassList("sfx-space-note");
            note.RegisterValueChangedCallback(evt => SetSpaceNote(evt.newValue));
            section.Add(note);
            return section;
        }

        private void SetSpaceAssignment(SfxSpaceAssignmentState state, SfxCue cue)
        {
            if (spaceCatalog == null || selectedSpace == null)
            {
                return;
            }

            Undo.RecordObject(spaceCatalog, "SFX 공간 결정 변경");
            if (!spaceCatalog.EditorSetAssignment(selectedSpace.Id, state, cue))
            {
                return;
            }

            EditorUtility.SetDirty(spaceCatalog);
            selectedSpace = spaceCatalog.EditorFindEntry(selectedSpace.Id);
            RefreshFilteredSpaces();
            RefreshHeaderCounts();
            RefreshDetails();
            SetStatus($"{selectedSpace.Id} 결정: {AssignmentLabel(selectedSpace)}");
        }

        private void SetSpaceNote(string note)
        {
            if (spaceCatalog == null || selectedSpace == null)
            {
                return;
            }

            Undo.RecordObject(spaceCatalog, "SFX 공간 메모 변경");
            if (!spaceCatalog.EditorSetNote(selectedSpace.Id, note))
            {
                return;
            }

            EditorUtility.SetDirty(spaceCatalog);
            RefreshFilteredSpaces();
        }

        private void RegisterSpaceAudioDropZone(VisualElement dropZone)
        {
            dropZone.RegisterCallback<DragUpdatedEvent>(_ =>
            {
                if (DragAndDrop.objectReferences.OfType<AudioClip>().Any())
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    dropZone.AddToClassList("sfx-drop-zone--active");
                }
            });
            dropZone.RegisterCallback<DragLeaveEvent>(_ => dropZone.RemoveFromClassList("sfx-drop-zone--active"));
            dropZone.RegisterCallback<DragPerformEvent>(_ =>
            {
                var clip = DragAndDrop.objectReferences.OfType<AudioClip>().FirstOrDefault();
                dropZone.RemoveFromClassList("sfx-drop-zone--active");
                if (clip == null)
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                CreateCueForSelectedSpace(clip);
            });
        }

        private void CreateCueForSelectedSpace(AudioClip clip)
        {
            if (selectedSpace == null || clip == null)
            {
                return;
            }

            EnsureCatalogAndSynchronize(false);
            var category = FindCategory(selectedSpace.CategoryId) ?? FindCategory(SfxCatalog.UnassignedCategoryId);
            var folder = BuildCategoryFolder(category);
            EnsureFolder(folder);
            var assetName = $"SFX_{selectedSpace.Id.Replace('-', '_')}";
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}.asset");
            var cue = CreateInstance<SfxCue>();
            cue.name = assetName;
            cue.EditorConfigure(
                new[] { clip },
                new Vector2(0.9f, 1f),
                new Vector2(0.96f, 1.04f),
                DefaultSpatialBlend(selectedSpace.CategoryId),
                0.04f,
                selectedSpace.Priority == SfxSpacePriority.P0 ? SfxPriority.High : SfxPriority.Normal);
            AssetDatabase.CreateAsset(cue, path);
            catalog.EditorSynchronize(new[] { cue }, _ => selectedSpace.CategoryId);
            catalog.EditorSetCategory(cue, selectedSpace.CategoryId);
            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(cue);
            SetSpaceAssignment(SfxSpaceAssignmentState.Assigned, cue);
            AssetDatabase.SaveAssets();
            SetStatus($"{selectedSpace.Id}에 {cue.name}을 만들고 {clip.name}을 배정했습니다.");
        }

        private static float DefaultSpatialBlend(string categoryId)
        {
            return categoryId == "ui" || categoryId == "main_battle" || categoryId == "expedition"
                ? 0f
                : 1f;
        }

        private static string CoverageLabel(SfxSpaceCoverageState state)
        {
            return state switch
            {
                SfxSpaceCoverageState.Connected => "기존 연결",
                SfxSpaceCoverageState.Partial => "부분 연결",
                SfxSpaceCoverageState.EmptySlot => "기존 빈 슬롯",
                SfxSpaceCoverageState.MissingHook => "연결 공간 필요",
                _ => "후속"
            };
        }

        private static string AssignmentLabel(SfxSpaceEntry entry)
        {
            if (entry == null)
            {
                return "미결정";
            }

            return entry.AssignmentState switch
            {
                SfxSpaceAssignmentState.Disabled => "사용 안 함",
                SfxSpaceAssignmentState.Assigned when entry.Cue != null => $"Cue · {entry.Cue.name}",
                SfxSpaceAssignmentState.Assigned => "Cue 선택 필요",
                _ => entry.CoverageState == SfxSpaceCoverageState.Connected ? "현재 연결 유지" : "미결정"
            };
        }

        private static string OwnerGuidance(SfxSpaceEntry entry)
        {
            if (entry.CategoryId == "monster_basic" || entry.CategoryId == "monster_active")
            {
                return "Monster Maker의 기존 Sound/Binding이 원본입니다. Manager는 공간을 중복 생성하거나 제작소 산출물을 덮어쓰지 않습니다.";
            }

            if (entry.CategoryId == "commander_skill")
            {
                return "CommanderSkillDefinition의 Casting/Cast/Impact가 원본입니다. 기존 Cue는 Cue 보관함에서 그대로 관리합니다.";
            }

            if (entry.CategoryId == "treasure_spirit")
            {
                return "보물 정령의 기존 DemoDungeonAudio AudioClip 연결을 원본으로 유지합니다. Loop는 Cue 배정과 별도로 종료 QA가 필요합니다.";
            }

            if (entry.CategoryId == "fallen_commander")
            {
                return "FallenCommander Boss/Phase Config의 기존 AudioClip 공간을 원본으로 유지합니다.";
            }

            if (entry.CategoryId == "castle_raid" && entry.Id == "CR-01")
            {
                return "활성 포탑 Profile의 기존 5개 Cue와 7개 Profile 연결을 그대로 사용합니다.";
            }

            return entry.HasExistingOwner
                ? "기존 시스템의 슬롯과 참조가 원본입니다. 여기서는 배정 결정을 기록하며 기존 연결을 복사하거나 끊지 않습니다."
                : "현재 Runtime Hook이 없는 사건입니다. 여기서 Cue를 결정한 뒤 해당 이벤트 소유 시스템에 연결해야 완료됩니다.";
        }

        private sealed class SpaceRowElements
        {
            public SpaceRowElements(
                VisualElement accent,
                Label id,
                Label priority,
                Label coverage,
                Label name,
                Label meta)
            {
                Accent = accent;
                Id = id;
                Priority = priority;
                Coverage = coverage;
                Name = name;
                Meta = meta;
            }

            public VisualElement Accent { get; }
            public Label Id { get; }
            public Label Priority { get; }
            public Label Coverage { get; }
            public Label Name { get; }
            public Label Meta { get; }
        }
    }
}
