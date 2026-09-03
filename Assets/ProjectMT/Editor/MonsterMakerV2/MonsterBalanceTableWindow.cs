using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.EditorTools.ExpeditionBalance;
using ProjectMT.EditorTools.MonsterMaker;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.MonsterMakerV2
{
    public sealed class MonsterBalanceTableWindow : EditorWindow
    {
        private const string MenuPath = "JC Tool/Monster/Monster Balance Table";
        private const string StylePath =
            "Assets/ProjectMT/Editor/MonsterMakerV2/UI/MonsterBalanceTableWindow.uss";

        private static readonly string[] RarityChoices =
            { "전체", "일반", "희귀", "영웅", "전설", "신화" };
        private static readonly string[] CombatChoices = { "전체", "근거리", "원거리", "특수" };
        private static readonly string[] SortChoices =
            { "Catalog 순", "등급 높은순", "이름순", "체력 높은순", "기본 DPS 높은순", "액티브 DPS 높은순" };

        private readonly List<RowModel> rows = new List<RowModel>();
        private TextField searchField;
        private DropdownField rarityField;
        private DropdownField combatField;
        private DropdownField sortField;
        private Toggle dirtyOnlyToggle;
        private Label countLabel;
        private Label dirtyLabel;
        private Label statusLabel;
        private VisualElement tableBody;
        private Button saveButton;
        private bool building;
        private int catalogCount;

        [MenuItem(MenuPath, false, 22)]
        public static void OpenWindow()
        {
            var window = GetWindow<MonsterBalanceTableWindow>();
            window.titleContent = new GUIContent("Monster Balance");
            window.minSize = new Vector2(1380f, 700f);
            window.Show();
            window.Focus();
        }

        public override void SaveChanges()
        {
            if (CommitChanges(false)) base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            ReloadRows();
            base.DiscardChanges();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Monster Balance");
            minSize = new Vector2(1380f, 700f);
            saveChangesMessage = "밸런스 표에 아직 반영하지 않은 능력치 변경이 있습니다.";
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
        }

        public void CreateGUI()
        {
            building = true;
            rootVisualElement.Clear();
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (style != null) rootVisualElement.styleSheets.Add(style);
            rootVisualElement.AddToClassList("balance-root");

            BuildHeader();
            BuildToolbar();
            BuildFormulaStrip();
            BuildTable();
            BuildFooter();
            ReloadRows();
            building = false;
        }

        private void BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("balance-header");
            var titles = new VisualElement();
            titles.AddToClassList("balance-title-area");
            var title = new Label("몬스터 능력치 밸런스 표");
            title.AddToClassList("balance-title");
            var subtitle = new Label("운영 Catalog 44종 전체의 전투 능력치를 한 화면에서 비교·수정합니다.");
            subtitle.AddToClassList("balance-subtitle");
            titles.Add(title);
            titles.Add(subtitle);
            countLabel = new Label("불러오는 중");
            countLabel.AddToClassList("balance-count");
            header.Add(titles);
            header.Add(countLabel);
            rootVisualElement.Add(header);
        }

        private void BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("balance-toolbar");
            searchField = new TextField("검색");
            searchField.AddToClassList("balance-search");
            searchField.tooltip = "몬스터 이름 또는 ID";
            searchField.RegisterValueChangedCallback(_ => RebuildVisibleRows());
            rarityField = new DropdownField("등급", RarityChoices.ToList(), 0);
            combatField = new DropdownField("전투", CombatChoices.ToList(), 0);
            sortField = new DropdownField("정렬", SortChoices.ToList(), 1);
            rarityField.RegisterValueChangedCallback(_ => RebuildVisibleRows());
            combatField.RegisterValueChangedCallback(_ => RebuildVisibleRows());
            sortField.RegisterValueChangedCallback(_ => RebuildVisibleRows());
            dirtyOnlyToggle = new Toggle("변경된 행만");
            dirtyOnlyToggle.RegisterValueChangedCallback(_ => RebuildVisibleRows());
            toolbar.Add(searchField);
            toolbar.Add(rarityField);
            toolbar.Add(combatField);
            toolbar.Add(sortField);
            toolbar.Add(dirtyOnlyToggle);
            toolbar.Add(MakeButton("적 웨이브 표", ExpeditionWaveBalanceTableWindow.OpenWindow, "balance-button"));
            toolbar.Add(MakeButton("적 리스트 표", ExpeditionEnemyBalanceTableWindow.OpenWindow, "balance-button"));
            toolbar.Add(MakeButton("새로고침", () => ReloadRowsWithConfirmation(), "balance-button"));
            toolbar.Add(MakeButton("변경 취소", DiscardBufferedChanges, "balance-button"));
            saveButton = MakeButton("변경 능력치 반영", () => CommitChanges(true),
                "balance-button", "balance-button--primary");
            toolbar.Add(saveButton);
            rootVisualElement.Add(toolbar);
        }

        private void BuildFormulaStrip()
        {
            var formula = new Label(
                "계산 기준  ·  기본 DPS = 공격력 × 초당 공격 횟수 (단일 대상)  |  " +
                "공격형 액티브 = Step 기대 배율 합 × 공격력 ÷ 프로필 예상 시전시간  |  " +
                "대상 수는 DPS에 곱하지 않음  |  지원·수호·약화형은 DPS 대신 역할 표시");
            formula.AddToClassList("balance-formula");
            rootVisualElement.Add(formula);
        }

        private void BuildTable()
        {
            var frame = new VisualElement();
            frame.AddToClassList("balance-table-frame");

            var headerScroll = new ScrollView(ScrollViewMode.Horizontal)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Hidden
            };
            headerScroll.AddToClassList("balance-table-header-scroll");
            var headerContent = new VisualElement();
            headerContent.AddToClassList("balance-table-content");
            headerContent.AddToClassList("balance-table-header-content");
            headerContent.Add(BuildTableHeader());
            headerScroll.Add(headerContent);

            var bodyScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            bodyScroll.AddToClassList("balance-table-body-scroll");
            var bodyContent = new VisualElement();
            bodyContent.AddToClassList("balance-table-content");
            tableBody = new VisualElement();
            tableBody.AddToClassList("balance-table-body");
            bodyContent.Add(tableBody);
            bodyScroll.Add(bodyContent);
            bodyScroll.horizontalScroller.valueChanged += value =>
                headerScroll.horizontalScroller.value = value;

            frame.Add(headerScroll);
            frame.Add(bodyScroll);
            rootVisualElement.Add(frame);
        }

        private VisualElement BuildTableHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("balance-table-header");
            header.Add(HeaderCell("프로필", "w-portrait"));
            header.Add(HeaderCell("등급", "w-rarity"));
            header.Add(HeaderCell("이름 / ID", "w-name"));
            header.Add(HeaderCell("체력", "w-stat"));
            header.Add(HeaderCell("공격력", "w-stat"));
            header.Add(HeaderCell("방어력", "w-stat"));
            header.Add(HeaderCell("공속(/초)", "w-stat"));
            header.Add(HeaderCell("이동속도", "w-stat"));
            header.Add(HeaderCell("사거리", "w-stat"));
            header.Add(HeaderCell("전투", "w-combat"));
            header.Add(HeaderCell("기본 대상", "w-target"));
            header.Add(HeaderCell("기본 방식", "w-pattern"));
            header.Add(HeaderCell("기본 DPS", "w-dps"));
            header.Add(HeaderCell("액티브", "w-active-name"));
            header.Add(HeaderCell("유형", "w-active-type"));
            header.Add(HeaderCell("액티브 대상", "w-active-target"));
            header.Add(HeaderCell("총피해", "w-dps"));
            header.Add(HeaderCell("시전(초)", "w-duration"));
            header.Add(HeaderCell("액티브 DPS", "w-dps"));
            header.Add(HeaderCell("Maker", "w-open"));
            return header;
        }

        private void BuildFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("balance-footer");
            dirtyLabel = new Label("변경 없음");
            dirtyLabel.AddToClassList("balance-dirty-count");
            statusLabel = new Label("Catalog를 불러옵니다.");
            statusLabel.AddToClassList("balance-status");
            footer.Add(dirtyLabel);
            footer.Add(statusLabel);
            rootVisualElement.Add(footer);
        }

        private void ReloadRows()
        {
            rows.Clear();
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>(
                MonsterMakerAssetWriter.MonsterCatalogPath);
            if (catalog == null)
            {
                catalogCount = 0;
                SetStatus("MonsterCatalog을 찾을 수 없습니다.", true);
                RebuildVisibleRows();
                return;
            }

            catalogCount = catalog.Definitions.Count(definition => definition != null);
            var catalogIndex = 0;
            foreach (var definition in catalog.Definitions)
            {
                if (definition == null) continue;
                var draft = AssetDatabase.LoadAssetAtPath<MonsterMakerDraft>(
                    MonsterMakerAssetWriter.BuildDraftPath(definition.MonsterId));
                if (draft != null)
                {
                    rows.Add(new RowModel(draft, definition, catalogIndex));
                }
                catalogIndex++;
            }

            SetStatus(rows.Count == catalogCount
                ? "능력치 수정은 저장 전까지 표 안에만 보관됩니다."
                : $"Catalog {catalogCount}종 중 영구 Maker Draft가 있는 {rows.Count}종만 편집할 수 있습니다.",
                rows.Count != catalogCount);
            RebuildVisibleRows();
            UpdateDirtyState();
        }

        private void RebuildVisibleRows()
        {
            if (tableBody == null) return;
            tableBody.Clear();
            IEnumerable<RowModel> visible = rows;
            var query = searchField?.value?.Trim();
            if (!string.IsNullOrWhiteSpace(query))
            {
                visible = visible.Where(row =>
                    row.Draft.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    row.Draft.MonsterId.Contains(query, StringComparison.OrdinalIgnoreCase));
            }
            if (rarityField != null && rarityField.index > 0)
            {
                var rarity = (MonsterRarity)(rarityField.index - 1);
                visible = visible.Where(row => row.Draft.Rarity == rarity);
            }
            if (combatField != null && combatField.index > 0)
            {
                var label = CombatChoices[combatField.index];
                visible = visible.Where(row =>
                    MonsterBalanceTableMetrics.Evaluate(row.Draft, row.Attack, row.AttackSpeed).CombatType == label);
            }
            if (dirtyOnlyToggle?.value == true) visible = visible.Where(row => row.IsDirty);
            visible = ApplySort(visible);
            var visibleRows = visible.ToList();
            foreach (var row in visibleRows) tableBody.Add(BuildRow(row));
            countLabel.text = $"{visibleRows.Count} 표시 / {rows.Count} 대상 / {catalogCount} 전체";
        }

        private IEnumerable<RowModel> ApplySort(IEnumerable<RowModel> source)
        {
            return sortField?.index switch
            {
                1 => source.OrderByDescending(row => row.Draft.Rarity)
                    .ThenBy(row => row.CatalogIndex),
                2 => source.OrderBy(row => row.Draft.DisplayName, StringComparer.CurrentCultureIgnoreCase),
                3 => source.OrderByDescending(row => row.Health),
                4 => source.OrderByDescending(row => row.Attack * row.AttackSpeed),
                5 => source.OrderByDescending(row =>
                    MonsterBalanceTableMetrics.Evaluate(row.Draft, row.Attack, row.AttackSpeed).ActiveDps),
                _ => source.OrderBy(row => row.CatalogIndex)
            };
        }

        private VisualElement BuildRow(RowModel row)
        {
            var element = new VisualElement();
            element.AddToClassList("balance-row");
            row.Element = element;

            var portrait = new Image { sprite = row.Draft.Portrait, scaleMode = ScaleMode.ScaleToFit };
            portrait.AddToClassList("balance-portrait");
            element.Add(Cell(portrait, "w-portrait"));

            var rarity = new Label(MonsterBalanceTableMetrics.GetRarityLabel(row.Draft.Rarity));
            rarity.AddToClassList("balance-rarity");
            rarity.AddToClassList(MonsterBalanceTableMetrics.GetRarityClass(row.Draft.Rarity));
            element.Add(Cell(rarity, "w-rarity"));

            var name = new VisualElement();
            name.AddToClassList("balance-name-stack");
            var displayName = new Label(row.Draft.DisplayName);
            displayName.AddToClassList("balance-name");
            var id = new Label(row.Draft.MonsterId);
            id.AddToClassList("balance-id");
            name.Add(displayName);
            name.Add(id);
            element.Add(Cell(name, "w-name"));

            element.Add(FloatCell(row, row.Health, value => row.Health = value, "w-stat"));
            element.Add(FloatCell(row, row.Attack, value => row.Attack = value, "w-stat"));
            element.Add(FloatCell(row, row.Defense, value => row.Defense = value, "w-stat"));
            element.Add(FloatCell(row, row.AttackSpeed, value => row.AttackSpeed = value, "w-stat"));
            element.Add(FloatCell(row, row.MoveSpeed, value => row.MoveSpeed = value, "w-stat"));
            element.Add(FloatCell(row, row.Range, value => row.Range = value, "w-stat"));

            var metrics = MonsterBalanceTableMetrics.Evaluate(row.Draft, row.Attack, row.AttackSpeed);
            element.Add(LabelCell(metrics.CombatType, "w-combat"));
            element.Add(LabelCell(metrics.BasicTarget, "w-target"));
            element.Add(LabelCell(metrics.BasicPattern, "w-pattern"));
            row.BasicDpsLabel = ValueLabel(metrics.BasicDps, "0.##");
            element.Add(Cell(row.BasicDpsLabel, "w-dps", "balance-derived"));
            element.Add(LabelCell(metrics.ActiveName, "w-active-name"));
            element.Add(LabelCell(metrics.ActiveType, "w-active-type"));
            element.Add(LabelCell(metrics.ActiveTarget, "w-active-target"));
            row.ActiveBurstLabel = ValueLabel(metrics.ActiveBurst, "0.##", metrics.HasActiveDamage);
            row.ActiveDurationLabel = ValueLabel(metrics.ActiveDuration, "0.##", metrics.ActiveDuration > 0f);
            row.ActiveDpsLabel = ValueLabel(metrics.ActiveDps, "0.##", metrics.HasActiveDamage);
            element.Add(Cell(row.ActiveBurstLabel, "w-dps", "balance-derived"));
            element.Add(Cell(row.ActiveDurationLabel, "w-duration", "balance-derived"));
            element.Add(Cell(row.ActiveDpsLabel, "w-dps", "balance-derived"));
            var open = MakeButton("열기", () => OpenInMaker(row), "balance-open-button");
            element.Add(Cell(open, "w-open"));
            row.RefreshVisual();
            return element;
        }

        private VisualElement FloatCell(RowModel row, float value, Action<float> setter, string widthClass)
        {
            var field = new FloatField { value = value, isDelayed = true };
            field.AddToClassList("balance-number-field");
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                RefreshDerived(row);
                UpdateDirtyState();
            });
            return Cell(field, widthClass);
        }

        private void RefreshDerived(RowModel row)
        {
            var metrics = MonsterBalanceTableMetrics.Evaluate(row.Draft, row.Attack, row.AttackSpeed);
            if (row.BasicDpsLabel != null) row.BasicDpsLabel.text = metrics.BasicDps.ToString("0.##");
            if (row.ActiveBurstLabel != null)
                row.ActiveBurstLabel.text = metrics.HasActiveDamage ? metrics.ActiveBurst.ToString("0.##") : "—";
            if (row.ActiveDpsLabel != null)
                row.ActiveDpsLabel.text = metrics.HasActiveDamage ? metrics.ActiveDps.ToString("0.##") : "—";
            row.RefreshVisual();
        }

        private void UpdateDirtyState()
        {
            var dirtyCount = rows.Count(row => row.IsDirty);
            hasUnsavedChanges = dirtyCount > 0;
            dirtyLabel.text = dirtyCount > 0 ? $"변경 {dirtyCount}종" : "변경 없음";
            dirtyLabel.EnableInClassList("balance-dirty-count--active", dirtyCount > 0);
            saveButton?.SetEnabled(dirtyCount > 0);
            foreach (var row in rows) row.RefreshVisual();
        }

        private bool CommitChanges(bool confirm)
        {
            var dirtyRows = rows.Where(row => row.IsDirty).ToList();
            if (dirtyRows.Count == 0) return true;
            foreach (var row in dirtyRows)
            {
                if (!row.TryValidate(out var error))
                {
                    EditorUtility.DisplayDialog("능력치 확인", $"{row.Draft.DisplayName}: {error}", "확인");
                    return false;
                }
                if (!row.SourceStillMatches())
                {
                    EditorUtility.DisplayDialog(
                        "외부 변경 감지",
                        $"{row.Draft.DisplayName}의 원본 능력치가 표를 연 뒤 변경되었습니다. 새로고침 후 다시 수정하세요.",
                        "확인");
                    return false;
                }
            }

            if (confirm && !EditorUtility.DisplayDialog(
                    "능력치 반영",
                    $"변경한 {dirtyRows.Count}종의 능력치를 Maker Draft와 정식 MonsterDefinition에 함께 반영합니다.",
                    "반영", "취소"))
            {
                return false;
            }

            var saved = 0;
            try
            {
                foreach (var row in dirtyRows)
                {
                    MonsterMakerAssetWriter.SynchronizeBalanceStats(
                        row.Draft, row.Health, row.Attack, row.Defense,
                        row.AttackSpeed, row.MoveSpeed, row.Range);
                    saved++;
                }
                ReloadRows();
                SetStatus($"{saved}종 능력치를 Draft와 Runtime Definition에 반영했습니다.", false);
                return true;
            }
            catch (Exception exception)
            {
                ReloadRows();
                EditorUtility.DisplayDialog(
                    "능력치 반영 실패",
                    $"{saved}종 반영 뒤 중단되었습니다. 실패 행은 자동 원상복구했습니다.\n\n{exception.Message}",
                    "확인");
                SetStatus("일부 반영이 중단되었습니다. 표를 다시 확인하세요.", true);
                return false;
            }
        }

        private void DiscardBufferedChanges()
        {
            if (!hasUnsavedChanges || EditorUtility.DisplayDialog(
                    "변경 취소", "표에서 수정한 값을 모두 버리고 원본을 다시 불러올까요?", "버리기", "취소"))
            {
                ReloadRows();
            }
        }

        private void ReloadRowsWithConfirmation()
        {
            if (!hasUnsavedChanges || EditorUtility.DisplayDialog(
                    "새로고침", "아직 반영하지 않은 표의 변경값을 버리고 다시 불러올까요?", "새로고침", "취소"))
            {
                ReloadRows();
            }
        }

        private void OpenInMaker(RowModel row)
        {
            if (hasUnsavedChanges)
            {
                EditorUtility.DisplayDialog(
                    "몬스터 밸런스 표",
                    "표의 변경 능력치를 먼저 반영하거나 취소한 뒤 Monster Maker를 여세요.",
                    "확인");
                return;
            }
            MonsterMakerV2Window.OpenDraft(row.Draft);
        }

        private void OnProjectChanged()
        {
            if (building) return;
            if (hasUnsavedChanges)
            {
                SetStatus("Project 변경을 감지했습니다. 현재 표 값을 보호하려면 반영하거나 취소하세요.", true);
                return;
            }
            ReloadRows();
        }

        private void SetStatus(string message, bool warning)
        {
            if (statusLabel == null) return;
            statusLabel.text = message;
            statusLabel.EnableInClassList("balance-status--warning", warning);
        }

        private static Button MakeButton(string text, Action clicked, params string[] classes)
        {
            var button = new Button(clicked) { text = text };
            foreach (var className in classes) button.AddToClassList(className);
            return button;
        }

        private static VisualElement HeaderCell(string text, string widthClass)
        {
            var label = new Label(text);
            label.AddToClassList("balance-header-label");
            return Cell(label, widthClass, "balance-header-cell");
        }

        private static VisualElement LabelCell(string text, string widthClass) =>
            Cell(new Label(text), widthClass, "balance-text-cell");

        private static Label ValueLabel(float value, string format, bool visible = true)
        {
            var label = new Label(visible ? value.ToString(format) : "—");
            label.AddToClassList("balance-value");
            return label;
        }

        private static VisualElement Cell(VisualElement child, string widthClass, params string[] classes)
        {
            var cell = new VisualElement();
            cell.AddToClassList("balance-cell");
            cell.AddToClassList(widthClass);
            foreach (var className in classes) cell.AddToClassList(className);
            cell.Add(child);
            return cell;
        }

        private sealed class RowModel
        {
            private readonly float originalHealth;
            private readonly float originalAttack;
            private readonly float originalDefense;
            private readonly float originalAttackSpeed;
            private readonly float originalMoveSpeed;
            private readonly float originalRange;

            public RowModel(MonsterMakerDraft draft, MonsterDefinition definition, int catalogIndex)
            {
                Draft = draft;
                Definition = definition;
                CatalogIndex = catalogIndex;
                Health = originalHealth = draft.MaxHealth;
                Attack = originalAttack = draft.AttackPower;
                Defense = originalDefense = draft.Defense;
                AttackSpeed = originalAttackSpeed = draft.AttackSpeed;
                MoveSpeed = originalMoveSpeed = draft.MoveSpeed;
                Range = originalRange = draft.AttackRange;
            }

            public MonsterMakerDraft Draft { get; }
            public MonsterDefinition Definition { get; }
            public int CatalogIndex { get; }
            public float Health { get; set; }
            public float Attack { get; set; }
            public float Defense { get; set; }
            public float AttackSpeed { get; set; }
            public float MoveSpeed { get; set; }
            public float Range { get; set; }
            public VisualElement Element { get; set; }
            public Label BasicDpsLabel { get; set; }
            public Label ActiveBurstLabel { get; set; }
            public Label ActiveDurationLabel { get; set; }
            public Label ActiveDpsLabel { get; set; }

            public bool IsDirty =>
                !Mathf.Approximately(Health, originalHealth) ||
                !Mathf.Approximately(Attack, originalAttack) ||
                !Mathf.Approximately(Defense, originalDefense) ||
                !Mathf.Approximately(AttackSpeed, originalAttackSpeed) ||
                !Mathf.Approximately(MoveSpeed, originalMoveSpeed) ||
                !Mathf.Approximately(Range, originalRange);

            public bool SourceStillMatches() =>
                Mathf.Approximately(Draft.MaxHealth, originalHealth) &&
                Mathf.Approximately(Draft.AttackPower, originalAttack) &&
                Mathf.Approximately(Draft.Defense, originalDefense) &&
                Mathf.Approximately(Draft.AttackSpeed, originalAttackSpeed) &&
                Mathf.Approximately(Draft.MoveSpeed, originalMoveSpeed) &&
                Mathf.Approximately(Draft.AttackRange, originalRange);

            public bool TryValidate(out string error)
            {
                if (!Positive(Health)) error = "체력은 0보다 큰 유한값이어야 합니다.";
                else if (!NonNegative(Attack)) error = "공격력은 0 이상의 유한값이어야 합니다.";
                else if (!NonNegative(Defense)) error = "방어력은 0 이상의 유한값이어야 합니다.";
                else if (!Positive(AttackSpeed)) error = "공격속도는 0보다 큰 유한값이어야 합니다.";
                else if (!NonNegative(MoveSpeed)) error = "이동속도는 0 이상의 유한값이어야 합니다.";
                else if (!Positive(Range)) error = "사거리는 0보다 큰 유한값이어야 합니다.";
                else { error = string.Empty; return true; }
                return false;
            }

            public void RefreshVisual()
            {
                Element?.EnableInClassList("balance-row--dirty", IsDirty);
            }

            private static bool Positive(float value) =>
                !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

            private static bool NonNegative(float value) =>
                !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

    }
}
