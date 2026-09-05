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

            var changed = spaceCatalog.EditorSynchronize(definitions, seedMissing: created);
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
            var description = new Label("재생 상황 · 적용 범위 · 배정 상태");
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
            row.Coverage.text = SpaceAssignmentBadge(entry);
            element.tooltip = SpaceTiming(entry) + "\n" + SpaceAssignmentGuidance(entry);
            row.Coverage.EnableInClassList("sfx-space-coverage--connected", SfxEvents.Supports(entry.Id) && entry.Id != "COM-04");
            row.Coverage.EnableInClassList("sfx-space-coverage--warning", !SfxEvents.Supports(entry.Id) || entry.Id == "COM-04");
            row.Coverage.EnableInClassList("sfx-space-coverage--missing", false);
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
                   Contains(SpaceTiming(entry), query) ||
                   Contains(SpaceAssignmentGuidance(entry), query) ||
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
            var description = new Label("공간을 고르면 재생 상황·적용 범위와\n여기서 소리를 넣으면 적용되는지 확인할 수 있습니다.");
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
            scroll.Add(BuildSpaceTechnicalDetails());
            var management = MakeSection("공간 관리", "공간만 목록에서 삭제합니다. Cue와 원본 음원은 보존됩니다.");
            var delete = new Button(DeleteSelectedSpace)
            {
                name = "sfx-space-delete-button",
                text = "공간 삭제"
            };
            delete.AddToClassList("sfx-secondary-button");
            management.Add(delete);
            scroll.Add(management);
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

        private static readonly Dictionary<string, string> SpaceTimingDescriptions = new Dictionary<string, string>
        {
            { "SYS-01", "버튼을 눌러 선택하거나 확인할 때." },
            { "SYS-02", "기존 팝업 닫기 동작이 실행될 때." },
            { "SYS-03", "기존 팝업 열기 동작이 실행될 때." },
            { "SYS-04", "탭을 바꾸거나 목록의 정렬·필터를 변경할 때." },
            { "SYS-05", "설정을 켜고 끄거나 슬라이더 값을 조절할 때." },
            { "SYS-06", "잠김·재화 부족 등으로 요청한 조작을 할 수 없을 때." },
            { "SYS-07", "시작 버튼을 누르거나 계정 선택·로그인 결과를 알릴 때." },
            { "SYS-08", "로딩이나 저장이 끝났거나 저장 오류를 알릴 때." },
            { "SYS-09", "설정에서 효과음 음량을 바꾼 뒤 크기를 미리 들어볼 때." },
            { "SYS-10", "앱을 나갔다 돌아온 뒤 소리가 정상적으로 이어지는지 점검하는 항목." },
            { "COM-01", "공용 전투 피격 연출이 실행될 때." },
            { "COM-02", "공용 전투 사망 연출이 실행될 때." },
            { "COM-03", "약한 전투 강조 연출을 요청할 때. 현재 원정대 증원 방향 표시와 적 등장 연출에서도 사용한다." },
            { "COM-04", "강한 전투 강조 연출을 요청할 때. 현재 확인한 게임 코드에는 이 강도를 요청하는 호출이 없다." },
            { "COM-05", "무쿠크·샤탁·루미·아르·루가 기본공격을 시작하거나 발사·타격할 때." },
            { "COM-06", "그 밖의 운영 몬스터가 기본공격을 시작하거나 발사·타격할 때." },
            { "COM-07", "공격형 액티브의 각 단계가 시작되거나 투사체 발사·명중·종료가 일어날 때." },
            { "COM-08", "효과형 액티브를 시전하거나 대상에게 효과가 적용·유지·종료될 때." },
            { "COM-09", "전설 몬스터의 액티브 집중 연출이 시작될 때." },
            { "COM-10", "신화 몬스터의 액티브 집중 연출이 시작될 때." },
            { "COM-11", "유닛에게 버프 또는 디버프가 적용될 때." },
            { "COM-12", "지속 효과가 반복 적용되거나 해제되고, 면역·저항이 발생할 때." },
            { "COM-13", "화염구·얼음 수정구의 시전 준비·발동·명중 단계." },
            { "COM-14", "천둥 창·수호의 깃발·심연의 족쇄의 시전 준비·발동·명중 단계." },
            { "MB-01", "원정대 첫 웨이브의 적 행군이 시작될 때." },
            { "MB-02", "원정대 두 번째 이후 웨이브의 적 행군이 시작될 때." },
            { "MB-03", "원정대에서 보스로 지정된 적이 실제로 생성된 직후." },
            { "MB-04", "파티 기력이 가득 차 액티브를 사용할 수 있게 됐을 때." },
            { "MB-05", "전투 중 몬스터를 선택·드래그하거나 유효한 위치에 배치할 때." },
            { "MB-06", "예비대를 교체하거나 변경한 편성을 적용할 때." },
            { "MB-07", "원정대 승리 또는 공용 콘텐츠 성공 결과가 저장된 뒤." },
            { "MB-08", "원정대 패배 또는 공용 콘텐츠 실패 결과가 확정됐을 때." },
            { "EXP-01", "원정대 단계를 고르고 출정을 확정하거나 첫 전투를 시작할 때." },
            { "EXP-02", "다음 웨이브가 시작되거나 새로운 역할의 적이 처음 등장할 때." },
            { "EXP-03", "원정대의 아군·적이 맞거나 사망하고 전투 강조 연출이 나올 때." },
            { "EXP-04", "원정대에서 승리·패배 결과를 표시하거나 다음 단계로 넘어갈 때." },
            { "EXP-05", "원정대에서 골드·장비·열쇠 보상을 받을 때." },
            { "FORM-01", "편성 슬롯 선택·자리 교환·저장 또는 배치 불가를 알릴 때." },
            { "TUT-01", "튜토리얼 단계를 강조·완료하거나 새 기능을 해금할 때." },
            { "ECO-01", "공용 아이템·재화 획득 알림이 유효한 보상 표시 요청을 받았을 때." },
            { "ECO-02", "월드에 아이템이 나타나거나 플레이어가 주울 때." },
            { "ECO-03", "오프라인 보상을 수령하거나 광고 보상 두 배 적용이 성공했을 때." },
            { "ECO-04", "일일 출석 또는 누적 출석 보상을 받을 때." },
            { "ECO-05", "우편을 열거나 첨부 보상을 개별·일괄 수령할 때." },
            { "ECO-06", "퀘스트 진행이 완료되거나 완료 보상을 수령할 때." },
            { "ECO-07", "상점 상품을 선택하거나 구매 성공·구매 불가를 알릴 때." },
            { "ECO-08", "몬스터 소환 시작·등급 공개·최종 결과 표시 단계." },
            { "ECO-09", "군단장 스킬 소환 시작·내용 공개·최종 결과 표시 단계." },
            { "ECO-10", "장비를 장착·해제·강화·분해하거나 강화 결과를 알릴 때." },
            { "ECO-11", "인벤토리에서 아이템 선택·사용·수량 변경·버리기를 할 때." },
            { "ECO-12", "몬스터·군단장이 레벨업·돌파하거나 잠재능력을 적용할 때." },
            { "ECO-13", "도감에 새 항목을 등록하거나 도감 보상을 받을 때." },
            { "ECO-14", "광고 시청 결과에 따라 보상을 받거나 실패·취소를 알릴 때." },
            { "CR-01", "대포·발리스타·화염구 포탑이 발사하고 공격이 명중·폭발할 때." },
            { "CR-02", "군단의 역습 전장에 입장하고 전투를 시작할 때." },
            { "CR-03", "포탑을 설치하거나 설치할 수 없는 위치임을 알릴 때." },
            { "CR-04", "포탑 업그레이드가 성공하거나 최대 레벨임을 알릴 때." },
            { "CR-05", "성벽·건물·포탑·문이 맞거나 파괴되고 위험을 알릴 때. 현재 전용 연결은 파괴음이다." },
            { "CR-06", "병영 생산이 완료되거나 아군이 전장에 등장할 때." },
            { "CR-07", "방어용 함정이 발동하고 피해를 줄 때." },
            { "CR-08", "적 웨이브가 등장하거나 적이 성벽을 돌파할 때." },
            { "CR-09", "왕궁이 파괴되거나 전투 패배를 알릴 때. 현재 전용 연결은 왕궁 파괴음이다." },
            { "CR-10", "군단의 역습 단계 보상을 받을 때." },
            { "FR-01", "식량 대소동에 입장하고 조작을 시작할 때." },
            { "FR-02", "식량이 나타나거나 주워서 목적지에 배달할 때." },
            { "FR-03", "식량 대소동에서 적·군단장이 맞거나 적이 처치될 때." },
            { "FR-04", "남은 시간이 부족하거나 수집 목표를 달성했을 때." },
            { "FR-05", "식량 대소동의 성공·실패 결과와 보상을 표시할 때." },
            { "TS-01", "보물 정령에서 캐릭터가 점프할 때." },
            { "TS-02", "보물 정령에서 상자를 열 때." },
            { "TS-03", "보물 정령에서 퀴즈 화면이 나타날 때." },
            { "TS-04", "보물 정령에서 열쇠를 얻을 때." },
            { "TS-05", "보물 정령에서 일반 수집물을 얻을 때." },
            { "TS-06", "보물 정령에서 일반 문을 조작할 때." },
            { "TS-07", "보물 정령에서 감옥 문을 조작할 때." },
            { "TS-08", "보물 정령에서 잠금 해제가 실패했을 때." },
            { "TS-09", "보물 정령에서 불이 처음 붙을 때." },
            { "TS-10", "보물 정령에서 불이 타는 동안 반복되는 소리." },
            { "TS-11", "보물 정령에서 가시 함정이 작동할 때." },
            { "TS-12", "보물 정령에서 톱날이 돌아가는 동안 반복되는 소리." },
            { "TS-13", "보물 정령에서 화살 함정이 작동할 때." },
            { "TS-14", "보물 정령에서 미믹이 등장하거나 공격하는 장면." },
            { "TS-15", "보물 정령에서 추종자가 공격할 때." },
            { "TS-16", "보물 정령에서 경비가 공격할 때." },
            { "TS-17", "보물 정령의 전용 클리어 연출." },
            { "TS-18", "보물 정령의 전용 실패 연출." },
            { "TS-19", "보물 정령 전투에서 피격·사망·전투 강조 연출이 나올 때." },
            { "FC-01", "타락 군단장의 투사체 기본공격 준비·발동·명중 단계." },
            { "FC-02", "타락 군단장의 근접공격 준비·발동·명중 단계." },
            { "FC-03", "타락 군단장의 표식 강타 준비·발동·명중 단계." },
            { "FC-04", "타락 군단장의 추적 표식 준비·발동·명중 단계." },
            { "FC-05", "타락 군단장의 블랙홀 준비·발동·피해 단계." },
            { "FC-06", "타락 군단장의 블랙홀이 사라지는 종료 연출." },
            { "FC-07", "타락 군단장의 선형 강타 준비·발동·명중 단계." },
            { "FC-08", "타락 군단장의 타락 고리 준비·발동·피해 단계." },
            { "FC-09", "타락 군단장의 최종 돌진 준비·발동·명중 단계." },
            { "FC-10", "타락 군단장 전투의 시간초과 전멸 연출." },
            { "FC-11", "타락 군단장의 뒤틀린 전장 준비·발동·피해 단계." },
            { "FC-12", "타락 군단장의 낙하 탄막 준비·발동·명중 단계." },
            { "FC-13", "타락 군단장 낙하 탄막의 각 탄이 바닥에 닿는 보조 연출." },
            { "FC-14", "타락 군단장이 다음 전투 단계로 전환할 때." },
            { "FC-15", "타락 군단장이 무력화되거나 무력화에서 회복할 때." },
            { "FC-16", "타락 군단장 전투의 피격·사망·전투 강조 연출." },
            { "FC-17", "타락 군단장 전투에서 플레이어 군단장이 사망하거나 회피할 때." },
            { "FC-18", "타락 군단장 전투 결과를 표시하거나 나가기를 확인할 때." },
            { "GT-01", "고대 수호수에 입장하고 전투를 시작할 때." },
            { "GT-02", "고대 수호수의 새 적 웨이브나 강적이 등장할 때." },
            { "GT-03", "고대 수호수 전투에서 군단장·적이 맞거나 사망할 때." },
            { "GT-04", "수호수가 피해를 받거나 위험 상태가 되고 전투 단계가 올라갈 때." },
            { "GT-05", "고대 수호수의 성공·실패 결과와 보상을 표시할 때." },
        };

        private static string SpaceTiming(SfxSpaceEntry entry)
        {
            return SpaceTimingDescriptions.TryGetValue(entry.Id, out var text)
                ? text : $"{entry.Area}에서 {entry.EventName}에 사용할 효과음 후보입니다.";
        }

        private static string RelatedSpaceSettings(SfxSpaceEntry entry)
        {
            return entry.Id switch
            {
                "EXP-01" => "첫 웨이브 시작음은 MB-01에서 설정합니다. 단계 선택·출정 확인은 별도 연결이 필요합니다.",
                "EXP-02" => "웨이브 시작음은 MB-02, 보스 등장음은 MB-03에서 설정합니다. 적 역할별 첫 등장음은 별도 연결이 필요합니다.",
                "EXP-03" or "FR-03" or "TS-19" or "FC-16" or "GT-03" =>
                    "공용 피격·사망·전투 강조는 COM-01~04에서 설정합니다. 이 항목에 배정해도 별도 적용되지 않습니다.",
                "EXP-04" => "승리·패배는 MB-07·MB-08에서 설정합니다. 다음 단계 진입음은 별도 연결이 필요합니다.",
                "EXP-05" or "CR-10" => "공용 획득 알림을 사용하는 보상음은 ECO-01에서 설정합니다. 별도 보상 화면에는 자동 적용되지 않습니다.",
                "FR-05" or "GT-05" => "공용 성공·실패는 MB-07·MB-08, 공용 획득 알림은 ECO-01에서 설정합니다.",
                "FC-18" => "공용 승리·패배는 MB-07·MB-08에서 설정합니다. 나가기 확인음은 별도입니다.",
                _ => null
            };
        }

        private static string SpaceScope(SfxSpaceEntry entry)
        {
            return entry.Id switch
            {
                "SYS-01" => "기존 공용 버튼음을 호출하는 UI 버튼. 버튼 전체에 새 연결을 추가하는 기능은 아닙니다.",
                "SYS-02" => "기존 공용 닫기음을 호출하는 팝업. 모든 취소·뒤로가기 버튼에 자동 적용되지는 않습니다.",
                "SYS-03" => "기존 공용 열기음을 호출하는 팝업. 모든 패널에 자동 적용되지는 않습니다.",
                "COM-01" or "COM-02" => "공용 전투 피드백을 사용하는 유닛. 개별 몬스터 음성과 구조물 효과음은 별도입니다.",
                "COM-03" => "약한 전투 강조를 호출하는 지점. 원정대 증원 연출과 적 등장에도 영향을 줍니다.",
                "COM-04" => "강한 전투 강조를 호출하도록 연결한 지점. 현재 확인된 게임 호출은 없습니다.",
                "COM-09" or "COM-10" => "몬스터 액티브 집중 연출. 몬스터별 공격음·목소리와 다른 설정입니다.",
                "MB-01" or "MB-02" or "MB-03" => "원정대 웨이브 진행. 다른 콘텐츠의 전투 시작·보스 등장에는 자동 적용되지 않습니다.",
                "MB-07" or "MB-08" => "원정대와 공용 콘텐츠 종료 흐름. 콘텐츠 내부의 별도 연출음은 유지됩니다.",
                "ECO-01" => "공용 아이템·재화 획득 알림. 모든 보상 지급이나 개별 보상 화면을 감지하지는 않습니다.",
                _ => entry.Area
            };
        }

        private static string SpaceAssignmentGuidance(SfxSpaceEntry entry)
        {
            if (entry.Id == "COM-04")
                return "저장하면 공용 설정에 반영되지만, 실제로 들으려면 강한 강조 연출을 호출하는 게임 연결이 필요합니다.";
            if (SfxEvents.Supports(entry.Id))
                return "소리를 배정하고 변경 저장을 누르면 위 범위에 적용됩니다. 미결정은 기존 설정 유지, 사용 안 함은 공용음 끄기입니다.";
            var related = RelatedSpaceSettings(entry);
            if (related != null)
                return related + " 이 공간 자체는 배정 기록용입니다.";
            if (entry.Id == "SYS-10")
                return "소리를 넣는 실행 공간이 아니라 앱 복귀 후 오디오 상태를 확인하는 점검 항목입니다.";
            if (entry.Id == "COM-09" || entry.Id == "COM-10")
                return "액티브 집중 연출 설정에서 소리를 지정해야 합니다. 이곳의 배정은 기록만 남깁니다.";
            if (entry.CategoryId == "monster_basic" || entry.CategoryId == "monster_active")
                return "몬스터메이커에서 몬스터·공격 단계별로 지정하세요. 이곳의 배정은 메이커 설정을 변경하지 않습니다.";
            if (entry.CategoryId == "commander_skill")
                return "군단장 스킬 제작소에서 스킬·연출 단계별로 지정하세요. 이곳의 배정은 제작소 설정을 변경하지 않습니다.";
            if (entry.CategoryId == "treasure_spirit")
                return "보물 정령의 전용 오디오 설정에서 지정하세요. 이곳의 배정은 전용 소리를 바꾸지 않습니다.";
            if (entry.CategoryId == "fallen_commander")
                return "타락 군단장의 공격·단계별 설정에서 연결해야 합니다. 이곳에서는 배정 결정만 기록합니다.";
            if (entry.Id == "CR-01")
                return "포탑 공격 설정의 발사·명중·폭발 소리를 편집해야 합니다. 아래 기술 상세에서 같은 영역의 Cue를 찾을 수 있습니다.";
            if (entry.Id == "CR-05" || entry.Id == "CR-09")
                return "군단의 역습 전용 파괴음 설정에서 지정해야 합니다. 이곳의 배정은 해당 설정을 변경하지 않습니다.";
            return "여기서는 사용할 소리만 기록합니다. 해당 화면·콘텐츠의 재생 시점에 별도로 연결해야 들립니다.";
        }

        private static string SpaceAssignmentBadge(SfxSpaceEntry entry)
        {
            if (entry.Id == "COM-04") return "호출 추가 필요";
            if (SfxEvents.Supports(entry.Id)) return "저장하면 적용";
            if (RelatedSpaceSettings(entry) != null) return "공용 항목 참조";
            if (entry.Id == "SYS-10") return "점검 항목";
            return "별도 설정 필요";
        }

        private static void AddSpaceGuideLine(VisualElement parent, string heading, string text)
        {
            var label = new Label($"{heading}\n{text}");
            label.AddToClassList("sfx-space-copy");
            label.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(label);
        }

        private VisualElement BuildSpaceTechnicalDetails()
        {
            var foldout = new Foldout
            {
                text = "기술 상세 · 조사 근거와 점검 기준",
                value = false,
                name = "sfx-space-technical-details"
            };
            foldout.AddToClassList("sfx-advanced-foldout");
            AddSpaceGuideLine(foldout, "조사 당시 상태", CoverageLabel(selectedSpace.CoverageState) + " · 과거 조사 기록이며 현재 배정 결과와 다를 수 있습니다.");
            AddSpaceGuideLine(foldout, "코드·자산 조사 근거", selectedSpace.Evidence);
            AddSpaceGuideLine(foldout, "점검 기준", selectedSpace.CompletionCriteria);
            var cues = BuildExistingCueReferences(selectedSpace);
            if (cues != null) foldout.Add(cues);
            return foldout;
        }

        private VisualElement BuildSpaceCoverageSection()
        {
            var connected = SfxEvents.Supports(selectedSpace.Id);
            var section = MakeSection("어디에 쓰는 소리인가요?", connected
                ? "실제 연결 범위를 확인하고 소리를 배정하세요."
                : "아래는 이 항목이 다루는 상황입니다. 배정만으로 자동 재생되지는 않습니다.");
            AddSpaceGuideLine(section, connected ? "언제 재생되나요?" : "어떤 상황의 소리인가요?", SpaceTiming(selectedSpace));
            AddSpaceGuideLine(section, "어디에 적용되나요?", SpaceScope(selectedSpace));
            AddSpaceGuideLine(section, "여기서 배정하면?", SpaceAssignmentGuidance(selectedSpace));
            if (selectedSpace.Id == "COM-01" || selectedSpace.Id == "COM-02")
                section.Add(MakeNotice("몬스터메이커에 개별 피격·사망음이 있으면 새 공용음을 덧붙이지 않습니다. 공용 사용 안 함도 메이커 소리를 끄지는 않습니다. 군단의 역습 별도 공격 유닛의 사망 경로는 이 공용 사망음 대상이 아닙니다.", false));
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
            var label = new Label($"같은 영역의 Cue · 참고용 {related.Length}개");
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
            var section = MakeSection("사운드 배정", SpaceAssignmentBadge(selectedSpace) + " · 적용 범위는 위 설명을 확인하세요.");
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
            var section = MakeSection("작업 메모", "선택한 소리나 조정할 내용을 자유롭게 기록하세요.");

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

        private void DeleteSelectedSpace()
        {
            if (spaceCatalog == null || selectedSpace == null) return;
            var id = selectedSpace.Id;
            var message = $"{id} · {selectedSpace.EventName} 공간을 목록에서 삭제합니다.\n공간 정보·배정·메모를 삭제하며 별도 복원 목록은 남기지 않습니다. Cue와 원본 음원은 보존됩니다.";
            if (SfxEvents.Supports(id))
                message += "\n변경 저장 후 이 공간의 공용 배정이 해제되어 기존 설정을 따릅니다. 무음으로 만들려면 사용 안 함을 선택하세요.";
            if (!EditorUtility.DisplayDialog("SFX 공간 삭제", message, "공간 삭제", "취소")) return;
            Undo.RecordObject(spaceCatalog, "SFX 공간 삭제");
            if (!spaceCatalog.EditorDeleteEntry(id)) return;
            EditorUtility.SetDirty(spaceCatalog);
            selectedSpace = null;
            selectedSpaceId = string.Empty;
            RefreshAll();
            SetStatus($"{id} 공간을 삭제했습니다. 변경 저장을 눌러 적용하세요.");
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
                SfxEvents.Supports(selectedSpace.Id) && !selectedSpace.Id.StartsWith("COM-")
                    ? 0f : DefaultSpatialBlend(selectedSpace.CategoryId),
                0.04f,
                selectedSpace.Priority == SfxSpacePriority.P0 ? SfxPriority.High : SfxPriority.Normal);
            AssetDatabase.CreateAsset(cue, path);
            catalog.EditorSynchronize(new[] { cue }, _ => selectedSpace.CategoryId);
            catalog.EditorSetCategory(cue, selectedSpace.CategoryId);
            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(cue);
            SetSpaceAssignment(SfxSpaceAssignmentState.Assigned, cue);
            AssetDatabase.SaveAssets();
            SetStatus($"{selectedSpace.Id}에 {cue.name}을 배정했습니다. 변경 저장을 눌러 Runtime에 적용하세요.");
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
