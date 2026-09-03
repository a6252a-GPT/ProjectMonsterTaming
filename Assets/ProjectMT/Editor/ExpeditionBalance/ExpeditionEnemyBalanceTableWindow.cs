using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Features.Expedition;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.ExpeditionBalance
{
    public sealed class ExpeditionEnemyBalanceTableWindow : EditorWindow
    {
        private const string MenuPath = "JC Tool/Balance/Expedition Enemy Table";
        private const string StylePath =
            "Assets/ProjectMT/Editor/ExpeditionBalance/ExpeditionEnemyBalanceTableWindow.uss";
        private static readonly string[] SortChoices = { "종류순", "체력 배율 높은순", "공격 배율 높은순" };

        private readonly List<RowModel> rows = new List<RowModel>();
        private ExpeditionSeedProfile profile;
        private EnemyStageAppearanceSet appearanceSet;
        private string sourceJson = string.Empty;
        private DropdownField sortField;
        private VisualElement tableBody;
        private Label countLabel;
        private Label dirtyLabel;
        private Label statusLabel;
        private Button saveButton;

        [MenuItem(MenuPath, false, 31)]
        public static void OpenWindow()
        {
            var window = GetWindow<ExpeditionEnemyBalanceTableWindow>();
            window.titleContent = new GUIContent("Expedition Enemies");
            window.minSize = new Vector2(1180f, 620f);
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
            titleContent = new GUIContent("Expedition Enemies");
            minSize = new Vector2(1180f, 620f);
            saveChangesMessage = "적 리스트 표에 아직 반영하지 않은 변경이 있습니다.";
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (style != null) rootVisualElement.styleSheets.Add(style);
            rootVisualElement.AddToClassList("enemy-root");
            BuildHeader();
            BuildToolbar();
            BuildFormula();
            BuildTable();
            BuildFooter();
            ReloadRows();
        }

        private void BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("enemy-header");
            var titles = new VisualElement();
            var title = new Label("원정대 적 리스트 밸런스 표");
            title.AddToClassList("enemy-title");
            var subtitle = new Label("적 13종의 역할과 전투 배율을 한 행씩 비교하고 바로 수정합니다.");
            subtitle.AddToClassList("enemy-subtitle");
            titles.Add(title);
            titles.Add(subtitle);
            countLabel = new Label("불러오는 중");
            countLabel.AddToClassList("enemy-count");
            header.Add(titles);
            header.Add(countLabel);
            rootVisualElement.Add(header);
        }

        private void BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("enemy-toolbar");
            sortField = new DropdownField("정렬", SortChoices.ToList(), 0);
            sortField.RegisterValueChangedCallback(_ => RebuildRows());
            toolbar.Add(sortField);
            toolbar.Add(MakeButton("몬스터 표", ProjectMT.EditorTools.MonsterMakerV2.MonsterBalanceTableWindow.OpenWindow));
            toolbar.Add(MakeButton("웨이브 표", ExpeditionWaveBalanceTableWindow.OpenWindow));
            toolbar.Add(MakeButton("새로고침", ReloadWithConfirmation));
            toolbar.Add(MakeButton("변경 취소", DiscardBufferedChanges));
            saveButton = MakeButton("적 원본에 반영", () => CommitChanges(true), true);
            toolbar.Add(saveButton);
            rootVisualElement.Add(toolbar);
        }

        private void BuildFormula()
        {
            var formula = new Label(
                "체력·공격·방어 배율은 단계 기본값에 곱해집니다. 공속은 초당 공격 횟수이며 역할과 Prefab은 현재 원정대 구성에서 자동 표시됩니다.");
            formula.AddToClassList("enemy-formula");
            rootVisualElement.Add(formula);
        }

        private void BuildTable()
        {
            var frame = new VisualElement();
            frame.AddToClassList("enemy-table-frame");
            frame.Add(BuildTableHeader());
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("enemy-table-scroll");
            tableBody = new VisualElement();
            tableBody.AddToClassList("enemy-table-content");
            scroll.Add(tableBody);
            frame.Add(scroll);
            rootVisualElement.Add(frame);
        }

        private VisualElement BuildTableHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("enemy-table-header");
            header.Add(HeaderCell("프로필", "ew-profile"));
            header.Add(HeaderCell("적 종류", "ew-name"));
            header.Add(HeaderCell("역할", "ew-role"));
            header.Add(HeaderCell("체력 배율", "ew-multiplier"));
            header.Add(HeaderCell("공격 배율", "ew-multiplier"));
            header.Add(HeaderCell("방어 배율", "ew-multiplier"));
            header.Add(HeaderCell("공속(/초)", "ew-stat"));
            header.Add(HeaderCell("이동속도", "ew-stat"));
            header.Add(HeaderCell("사거리", "ew-stat"));
            header.Add(HeaderCell("1단계 체력", "ew-derived"));
            header.Add(HeaderCell("1단계 공격", "ew-derived"));
            header.Add(HeaderCell("1단계 방어", "ew-derived"));
            header.Add(HeaderCell("Prefab", "ew-open"));
            return header;
        }

        private void BuildFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("enemy-footer");
            dirtyLabel = new Label("변경 없음");
            dirtyLabel.AddToClassList("enemy-dirty");
            statusLabel = new Label("운영 적 데이터를 불러옵니다.");
            statusLabel.AddToClassList("enemy-status");
            footer.Add(dirtyLabel);
            footer.Add(statusLabel);
            rootVisualElement.Add(footer);
        }

        private void ReloadRows()
        {
            profile = ExpeditionEnemyBalanceAssetWriter.LoadProfile();
            appearanceSet = ExpeditionEnemyBalanceAssetWriter.LoadAppearanceSet();
            rows.Clear();
            if (profile == null)
            {
                sourceJson = string.Empty;
                RebuildRows();
                SetStatus("운영 ExpeditionSeedProfile_Seed.asset을 찾을 수 없습니다.", true);
                return;
            }

            sourceJson = ExpeditionEnemyBalanceAssetWriter.CaptureSourceJson(profile);
            foreach (EnemyAppearanceGroup group in Enum.GetValues(typeof(EnemyAppearanceGroup)))
            {
                var balance = profile.ResolveEnemyTypeBalance(group);
                var serialized = profile.EnemyTypeBalances.Any(value => value != null && value.Group == group);
                rows.Add(new RowModel(balance, appearanceSet?.ResolvePrefab(group), serialized));
            }
            RebuildRows();
            SetStatus("수정값은 반영 버튼을 누르기 전까지 표 안에만 보관됩니다.", false);
            UpdateDirtyState();
        }

        private void RebuildRows()
        {
            if (tableBody == null) return;
            tableBody.Clear();
            IEnumerable<RowModel> visible = rows;
            visible = sortField?.index switch
            {
                1 => visible.OrderByDescending(row => row.HealthMultiplier).ThenBy(row => row.Group),
                2 => visible.OrderByDescending(row => row.DamageMultiplier).ThenBy(row => row.Group),
                _ => visible.OrderBy(row => row.Group)
            };
            foreach (var row in visible) tableBody.Add(BuildRow(row));
            countLabel.text = profile == null ? "원본 없음" : $"{rows.Count}종 · 편집 {rows.Count * 6}개 값";
        }

        private VisualElement BuildRow(RowModel row)
        {
            var element = new VisualElement();
            element.AddToClassList("enemy-row");
            element.EnableInClassList("enemy-row--dirty", row.IsDirty);
            row.Element = element;
            var portrait = new Image
            {
                image = row.Prefab == null ? null : AssetPreview.GetMiniThumbnail(row.Prefab),
                scaleMode = ScaleMode.ScaleToFit
            };
            portrait.AddToClassList("enemy-portrait");
            element.Add(Cell(portrait, "ew-profile"));
            element.Add(LabelCell(GetGroupLabel(row.Group), "ew-name"));
            element.Add(LabelCell(GetRoleLabel(row.Role), "ew-role"));
            element.Add(FloatCell(row.HealthMultiplier, value => row.HealthMultiplier = value, row, "ew-multiplier"));
            element.Add(FloatCell(row.DamageMultiplier, value => row.DamageMultiplier = value, row, "ew-multiplier"));
            element.Add(FloatCell(row.DefenseMultiplier, value => row.DefenseMultiplier = value, row, "ew-multiplier"));
            element.Add(FloatCell(row.AttacksPerSecond, value => row.AttacksPerSecond = value, row, "ew-stat"));
            element.Add(FloatCell(row.MoveSpeed, value => row.MoveSpeed = value, row, "ew-stat"));
            element.Add(FloatCell(row.AttackRange, value => row.AttackRange = value, row, "ew-stat"));
            row.HealthLabel = ValueLabel(ResolveStageOneHealth(row), "0");
            row.DamageLabel = ValueLabel(ResolveStageOneDamage(row), "0");
            row.DefenseLabel = ValueLabel(ResolveStageOneDefense(row), "0.##");
            element.Add(Cell(row.HealthLabel, "ew-derived", "enemy-derived"));
            element.Add(Cell(row.DamageLabel, "ew-derived", "enemy-derived"));
            element.Add(Cell(row.DefenseLabel, "ew-derived", "enemy-derived"));
            var open = MakeButton("열기", () => OpenPrefab(row.Prefab));
            open.SetEnabled(row.Prefab != null);
            element.Add(Cell(open, "ew-open"));
            return element;
        }

        private VisualElement FloatCell(float value, Action<float> setter, RowModel row, string widthClass)
        {
            var field = new FloatField { value = value, isDelayed = true };
            field.AddToClassList("enemy-number-field");
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
            if (row.HealthLabel != null) row.HealthLabel.text = ResolveStageOneHealth(row).ToString("0");
            if (row.DamageLabel != null) row.DamageLabel.text = ResolveStageOneDamage(row).ToString("0");
            if (row.DefenseLabel != null) row.DefenseLabel.text = ResolveStageOneDefense(row).ToString("0.##");
            row.Element?.EnableInClassList("enemy-row--dirty", row.IsDirty);
        }

        private float ResolveStageOneHealth(RowModel row) =>
            profile == null ? 0f : Mathf.Max(1f, Mathf.Floor(profile.EnemyBaseHealth * row.HealthMultiplier));

        private float ResolveStageOneDamage(RowModel row) =>
            profile == null ? 0f : Mathf.Max(1f, Mathf.Floor(profile.EnemyBaseDamage * row.DamageMultiplier));

        private float ResolveStageOneDefense(RowModel row) =>
            profile == null ? 0f : profile.EnemyBaseDefense * row.DefenseMultiplier;

        private void UpdateDirtyState()
        {
            var dirtyCount = rows.Count(row => row.IsDirty);
            hasUnsavedChanges = dirtyCount > 0;
            dirtyLabel.text = dirtyCount > 0 ? $"변경 {dirtyCount}종" : "변경 없음";
            dirtyLabel.EnableInClassList("enemy-dirty--active", dirtyCount > 0);
            saveButton?.SetEnabled(dirtyCount > 0);
            foreach (var row in rows) row.Element?.EnableInClassList("enemy-row--dirty", row.IsDirty);
        }

        private bool CommitChanges(bool confirm)
        {
            if (!hasUnsavedChanges) return true;
            if (confirm && !EditorUtility.DisplayDialog(
                    "적 리스트 원본 반영",
                    "변경한 적 13종의 전투값을 운영 Expedition Seed Profile에 반영할까요?",
                    "반영", "취소")) return false;
            var values = rows.OrderBy(row => row.Group).Select(row => row.ToValues()).ToArray();
            if (!ExpeditionEnemyBalanceAssetWriter.TryApply(values, sourceJson, out var error))
            {
                SetStatus(error, true);
                return false;
            }
            ReloadRows();
            SetStatus("적 13종 전투값을 운영 Seed Profile에 반영했습니다.", false);
            return true;
        }

        private void ReloadWithConfirmation()
        {
            if (hasUnsavedChanges && !EditorUtility.DisplayDialog(
                    "적 리스트 새로고침",
                    "표에서 수정한 값을 버리고 원본을 다시 불러올까요?",
                    "새로고침", "취소")) return;
            ReloadRows();
        }

        private void DiscardBufferedChanges()
        {
            if (!hasUnsavedChanges || EditorUtility.DisplayDialog(
                    "적 리스트 변경 취소",
                    "표에서 수정한 값을 모두 버릴까요?",
                    "변경 취소", "계속 편집")) ReloadRows();
        }

        private static void OpenPrefab(GameObject prefab)
        {
            if (prefab == null) return;
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }

        private void SetStatus(string message, bool warning)
        {
            statusLabel.text = message;
            statusLabel.EnableInClassList("enemy-status--warning", warning);
        }

        private static Button MakeButton(string text, Action action, bool primary = false)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("enemy-button");
            if (primary) button.AddToClassList("enemy-button--primary");
            return button;
        }

        private static VisualElement HeaderCell(string text, string widthClass)
        {
            var label = new Label(text);
            label.AddToClassList("enemy-header-label");
            return Cell(label, widthClass, "enemy-header-cell");
        }

        private static VisualElement LabelCell(string text, string widthClass) => Cell(new Label(text), widthClass);

        private static Label ValueLabel(float value, string format)
        {
            var label = new Label(value.ToString(format));
            label.AddToClassList("enemy-value");
            return label;
        }

        private static VisualElement Cell(VisualElement child, string widthClass, params string[] classes)
        {
            var cell = new VisualElement();
            cell.AddToClassList("enemy-cell");
            cell.AddToClassList(widthClass);
            foreach (var className in classes) cell.AddToClassList(className);
            cell.Add(child);
            return cell;
        }

        private static string GetGroupLabel(EnemyAppearanceGroup group) => group switch
        {
            EnemyAppearanceGroup.Peasant => "농부",
            EnemyAppearanceGroup.FemalePeasant => "여성 농부",
            EnemyAppearanceGroup.KnightTier1 => "기사 1",
            EnemyAppearanceGroup.KnightTier2 => "기사 2",
            EnemyAppearanceGroup.KnightTier3 => "기사 3",
            EnemyAppearanceGroup.MageTier1 => "마법사 1",
            EnemyAppearanceGroup.MageTier2 => "마법사 2",
            EnemyAppearanceGroup.MageTier3 => "마법사 3",
            EnemyAppearanceGroup.UpperKnightLower => "상위기사 하급",
            EnemyAppearanceGroup.UpperKnightMid => "상위기사 중급",
            EnemyAppearanceGroup.UpperKnightHigh => "상위기사 고급",
            EnemyAppearanceGroup.UpperKnightFinal => "상위기사 최종",
            EnemyAppearanceGroup.Ninja => "닌자",
            _ => group.ToString()
        };

        private static string GetRoleLabel(ExpeditionEnemyRole role) => role switch
        {
            ExpeditionEnemyRole.Ranged => "원거리",
            ExpeditionEnemyRole.Flanker => "측면 기동",
            _ => "근거리"
        };

        private sealed class RowModel
        {
            private readonly ExpeditionEnemyBalanceValues original;
            private readonly bool originallySerialized;

            public RowModel(ExpeditionEnemyTypeBalance source, GameObject prefab, bool serialized)
            {
                Group = source.Group;
                HealthMultiplier = source.HealthMultiplier;
                DamageMultiplier = source.DamageMultiplier;
                DefenseMultiplier = source.DefenseMultiplier;
                AttacksPerSecond = source.AttacksPerSecond;
                MoveSpeed = source.MoveSpeed;
                AttackRange = source.AttackRange;
                Prefab = prefab;
                originallySerialized = serialized;
                original = ToValues();
            }

            public EnemyAppearanceGroup Group { get; }
            public ExpeditionEnemyRole Role => ExpeditionSpawnPoolEntry.ResolveRole(Group);
            public GameObject Prefab { get; }
            public float HealthMultiplier { get; set; }
            public float DamageMultiplier { get; set; }
            public float DefenseMultiplier { get; set; }
            public float AttacksPerSecond { get; set; }
            public float MoveSpeed { get; set; }
            public float AttackRange { get; set; }
            public VisualElement Element { get; set; }
            public Label HealthLabel { get; set; }
            public Label DamageLabel { get; set; }
            public Label DefenseLabel { get; set; }
            public bool IsDirty => !originallySerialized ||
                                   !Mathf.Approximately(HealthMultiplier, original.HealthMultiplier) ||
                                   !Mathf.Approximately(DamageMultiplier, original.DamageMultiplier) ||
                                   !Mathf.Approximately(DefenseMultiplier, original.DefenseMultiplier) ||
                                   !Mathf.Approximately(AttacksPerSecond, original.AttacksPerSecond) ||
                                   !Mathf.Approximately(MoveSpeed, original.MoveSpeed) ||
                                   !Mathf.Approximately(AttackRange, original.AttackRange);

            public ExpeditionEnemyBalanceValues ToValues() => new ExpeditionEnemyBalanceValues(
                Group, HealthMultiplier, DamageMultiplier, DefenseMultiplier,
                AttacksPerSecond, MoveSpeed, AttackRange);
        }
    }
}
