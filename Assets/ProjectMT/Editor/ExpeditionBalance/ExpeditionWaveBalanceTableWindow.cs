using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.EditorTools.MonsterMakerV2;
using ProjectMT.Features.Expedition;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.ExpeditionBalance
{
    public sealed class ExpeditionWaveBalanceTableWindow : EditorWindow
    {
        private const string MenuPath = "JC Tool/Balance/Expedition Wave Table";
        private const string StylePath =
            "Assets/ProjectMT/Editor/ExpeditionBalance/ExpeditionWaveBalanceTableWindow.uss";

        private static readonly string[] SortChoices = { "단계순", "총 적 많은순", "예상 총 HP 높은순" };
        private static readonly EnemyAppearanceGroup[] AppearanceValues =
            (EnemyAppearanceGroup[])Enum.GetValues(typeof(EnemyAppearanceGroup));
        private static readonly string[] AppearanceChoices = AppearanceValues.Select(GetAppearanceLabel).ToArray();
        private static readonly string[] RarityChoices = { "일반", "희귀", "영웅", "전설", "신화" };

        private readonly List<StageRow> rows = new List<StageRow>();
        private ExpeditionSeedProfile profile;
        private GlobalModel globals;
        private string sourceJson = string.Empty;
        private DropdownField sortField;
        private VisualElement tableHeader;
        private VisualElement tableBody;
        private VisualElement compositionEditor;
        private StageRow selectedCompositionRow;
        private int selectedCompositionWaveIndex = -1;
        private Label countLabel;
        private Label dirtyLabel;
        private Label statusLabel;
        private Button saveButton;
        private bool building;

        [MenuItem(MenuPath, false, 30)]
        public static void OpenWindow()
        {
            var window = GetWindow<ExpeditionWaveBalanceTableWindow>();
            window.titleContent = new GUIContent("Expedition Waves");
            window.minSize = new Vector2(1420f, 720f);
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
            titleContent = new GUIContent("Expedition Waves");
            minSize = new Vector2(1420f, 720f);
            saveChangesMessage = "원정대 웨이브 표에 아직 반영하지 않은 변경이 있습니다.";
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
            rootVisualElement.AddToClassList("wave-root");
            BuildHeader();
            BuildToolbar();
            BuildGlobalStrip();
            BuildFormulaStrip();
            BuildCompositionEditor();
            BuildTable();
            BuildFooter();
            ReloadRows();
            building = false;
        }

        private void BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("wave-header");
            var titles = new VisualElement();
            var title = new Label("원정대 적 웨이브 밸런스 표");
            title.AddToClassList("wave-title");
            var subtitle = new Label("1~100단계를 한 줄씩 훑으며 웨이브 구성과 핵심 배율만 빠르게 조정합니다.");
            subtitle.AddToClassList("wave-subtitle");
            titles.Add(title);
            titles.Add(subtitle);
            countLabel = new Label("불러오는 중");
            countLabel.AddToClassList("wave-count");
            header.Add(titles);
            header.Add(countLabel);
            rootVisualElement.Add(header);
        }

        private void BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("wave-toolbar");
            sortField = new DropdownField("정렬", SortChoices.ToList(), 0);
            sortField.RegisterValueChangedCallback(_ => RebuildTableRows());
            toolbar.Add(sortField);
            toolbar.Add(MakeButton("몬스터 표", MonsterBalanceTableWindow.OpenWindow, "wave-button"));
            toolbar.Add(MakeButton("적 리스트", ExpeditionEnemyBalanceTableWindow.OpenWindow, "wave-button"));
            toolbar.Add(MakeButton("새로고침", ReloadWithConfirmation, "wave-button"));
            toolbar.Add(MakeButton("변경 취소", DiscardBufferedChanges, "wave-button"));
            saveButton = MakeButton("웨이브 원본에 반영", () => CommitChanges(true),
                "wave-button", "wave-button--primary");
            toolbar.Add(saveButton);
            rootVisualElement.Add(toolbar);
        }

        private void BuildGlobalStrip()
        {
            var details = new Foldout
            {
                text = "상세 설정 · 전역 적 성장식과 사거리",
                value = false
            };
            details.AddToClassList("wave-global-foldout");
            var card = new VisualElement { name = "global-strip" };
            card.AddToClassList("wave-global-card");
            details.Add(card);
            rootVisualElement.Add(details);
        }

        private void RebuildGlobalStrip()
        {
            var card = rootVisualElement.Q<VisualElement>("global-strip");
            if (card == null) return;
            card.Clear();
            if (globals == null)
            {
                card.Add(new Label("원정대 전역 전투값을 불러올 수 없습니다."));
                return;
            }

            card.Add(GlobalFloat("1단계 HP", globals.BaseHealth, value => globals.BaseHealth = value));
            card.Add(GlobalFloat("HP 복리/단계", globals.HealthGrowth, value => globals.HealthGrowth = value));
            card.Add(GlobalFloat("1단계 공격", globals.BaseDamage, value => globals.BaseDamage = value));
            card.Add(GlobalFloat("공격 증가/단계", globals.DamageGrowth, value => globals.DamageGrowth = value));
            card.Add(GlobalFloat("1단계 방어", globals.BaseDefense, value => globals.BaseDefense = value));
            card.Add(GlobalFloat("방어 증가/단계", globals.DefenseGrowth, value => globals.DefenseGrowth = value));
            card.Add(GlobalFloat("근접 사거리", globals.MeleeRange, value => globals.MeleeRange = value));
            card.Add(GlobalFloat("원거리 사거리", globals.RangedRange, value => globals.RangedRange = value));
            card.Add(GlobalFloat("제한시간(초)", globals.ChallengeSeconds, value => globals.ChallengeSeconds = value));
            card.Add(GlobalFloat("기본 웨이브간격", globals.WaveInterval, value => globals.WaveInterval = value));
            card.Add(GlobalInt("보스 간격", globals.BossInterval, value => globals.BossInterval = value));
            card.Add(GlobalFloat("보스 HP 배율", globals.BossHealthMultiplier, value => globals.BossHealthMultiplier = value));
        }

        private void BuildFormulaStrip()
        {
            var formula = new Label(
                "HP·공격·방어 %를 바꾸면 해당 단계의 모든 웨이브에 함께 적용됩니다. 웨이브별 예외는 구성 버튼에서 조정합니다.");
            formula.AddToClassList("wave-formula");
            rootVisualElement.Add(formula);
        }

        private void BuildCompositionEditor()
        {
            compositionEditor = new VisualElement { name = "composition-editor" };
            compositionEditor.AddToClassList("wave-composition-editor");
            compositionEditor.style.display = DisplayStyle.None;
            rootVisualElement.Add(compositionEditor);
        }

        private void BuildTable()
        {
            var frame = new VisualElement();
            frame.AddToClassList("wave-table-frame");
            tableHeader = new VisualElement();
            tableHeader.AddToClassList("wave-table-content");
            frame.Add(tableHeader);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("wave-table-scroll");
            tableBody = new VisualElement();
            tableBody.AddToClassList("wave-table-content");
            scroll.Add(tableBody);
            frame.Add(scroll);
            rootVisualElement.Add(frame);
        }

        private VisualElement BuildTableHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("wave-table-header");
            header.Add(HeaderCell("단계", "ww-stage"));
            header.Add(HeaderCell("웨이브", "ww-small"));
            for (var wave = 1; wave <= 3; wave++)
                header.Add(HeaderCell($"W{wave}  수량 / 구성", "ww-wave"));
            header.Add(HeaderCell("체력 %", "ww-percent"));
            header.Add(HeaderCell("공격 %", "ww-percent"));
            header.Add(HeaderCell("방어 %", "ww-percent"));
            header.Add(HeaderCell("총 적", "ww-total"));
            header.Add(HeaderCell("총 HP", "ww-total-hp"));
            return header;
        }

        private void BuildFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("wave-footer");
            dirtyLabel = new Label("변경 없음");
            dirtyLabel.AddToClassList("wave-dirty");
            statusLabel = new Label("원정대 Seed Profile을 불러옵니다.");
            statusLabel.AddToClassList("wave-status");
            footer.Add(dirtyLabel);
            footer.Add(statusLabel);
            rootVisualElement.Add(footer);
        }

        private void ReloadRows()
        {
            profile = ExpeditionWaveBalanceAssetWriter.LoadProfile();
            rows.Clear();
            selectedCompositionRow = null;
            selectedCompositionWaveIndex = -1;
            if (profile == null)
            {
                globals = null;
                sourceJson = string.Empty;
                RebuildGlobalStrip();
                RebuildCompositionEditor();
                RebuildTableRows();
                SetStatus("운영 ExpeditionSeedProfile_Seed.asset을 찾을 수 없습니다.", true);
                return;
            }

            sourceJson = ExpeditionWaveBalanceAssetWriter.CaptureSourceJson(profile);
            globals = new GlobalModel(profile);
            foreach (var definition in profile.Stages)
                if (definition != null) rows.Add(new StageRow(definition, globals.WaveInterval));
            RebuildGlobalStrip();
            RebuildCompositionEditor();
            RebuildTableRows();
            SetStatus("수정값은 반영 버튼을 누르기 전까지 표 안에만 보관됩니다.", false);
            UpdateDirtyState();
        }

        private void RebuildTableRows()
        {
            if (tableBody == null || tableHeader == null) return;
            tableHeader.Clear();
            tableBody.Clear();
            tableHeader.Add(BuildTableHeader());
            IEnumerable<StageRow> visible = rows;
            visible = sortField?.index switch
            {
                1 => visible.OrderByDescending(row => row.ResolveTotal()).ThenBy(row => row.MinimumStage),
                2 => visible.OrderByDescending(ResolveExpectedStageTotalHealth).ThenBy(row => row.MinimumStage),
                _ => visible.OrderBy(row => row.MinimumStage)
            };
            foreach (var row in visible) tableBody.Add(BuildWaveRow(row));
            countLabel.text = profile == null ? "원본 없음" :
                $"{rows.Count}개 단계 · {rows.Sum(row => row.WaveCount)}개 웨이브 · " +
                $"{rows.Sum(row => row.Waves.Take(row.WaveCount).Sum(wave => wave.Pools.Count))}개 웨이브별 적 설정";
        }

        private VisualElement BuildWaveRow(StageRow row)
        {
            var element = new VisualElement();
            element.AddToClassList("wave-row");
            element.EnableInClassList("wave-row--dirty", row.IsDirty);
            element.Add(LabelCell(row.MinimumStage.ToString(), "ww-stage"));
            element.Add(IntCell(row.WaveCount, value =>
            {
                row.WaveCount = Mathf.Clamp(value, 1, 3);
                if (selectedCompositionRow == row && selectedCompositionWaveIndex >= row.WaveCount)
                {
                    selectedCompositionRow = null;
                    selectedCompositionWaveIndex = -1;
                    RebuildCompositionEditor();
                }
                RebuildTableRows();
                UpdateDirtyState();
            }, "ww-small"));

            for (var waveIndex = 0; waveIndex < 3; waveIndex++)
            {
                var index = waveIndex;
                var active = index < row.WaveCount;
                element.Add(active ? WaveSummaryCell(row, index) : LabelCell("—", "ww-wave"));
            }

            element.Add(StagePercentCell(row, wave => wave.HealthPercent,
                (wave, value) => wave.HealthPercent = value, "ww-percent", "체력"));
            element.Add(StagePercentCell(row, wave => wave.DamagePercent,
                (wave, value) => wave.DamagePercent = value, "ww-percent", "공격"));
            element.Add(StagePercentCell(row, wave => wave.DefensePercent,
                (wave, value) => wave.DefensePercent = value, "ww-percent", "방어"));

            element.Add(LabelCell(row.ResolveTotal().ToString(), "ww-total", "wave-derived"));
            element.Add(LabelCell(ResolveExpectedStageTotalHealth(row).ToString("N0"),
                "ww-total-hp", "wave-derived", "wave-derived--accent"));
            return element;
        }

        private VisualElement WaveSummaryCell(StageRow row, int waveIndex)
        {
            var wave = row.Waves[waveIndex];
            var valid = Mathf.Abs(wave.PercentageTotal - 100f) <= 0.05f;
            var inline = new VisualElement();
            inline.AddToClassList("wave-inline");
            var count = new IntegerField { value = wave.EnemyCount, isDelayed = true };
            count.AddToClassList("wave-inline-count");
            count.RegisterValueChangedCallback(evt =>
            {
                wave.EnemyCount = evt.newValue;
                RebuildTableRows();
                UpdateDirtyState();
            });
            var button = MakeButton(
                ResolveCompositionSummary(wave),
                () => OpenCompositionEditor(row, waveIndex),
                "wave-composition-button");
            button.name = $"composition-{row.MinimumStage}-{waveIndex + 1}";
            button.tooltip = "클릭해서 적 종류, 출현 비율, 등급 범위와 웨이브별 예외값을 편집합니다.";
            if (!valid) button.AddToClassList("wave-composition-button--invalid");
            if (selectedCompositionRow == row && selectedCompositionWaveIndex == waveIndex)
                button.AddToClassList("wave-composition-button--selected");
            inline.Add(count);
            inline.Add(button);
            return Cell(inline, "ww-wave");
        }

        private VisualElement StagePercentCell(
            StageRow row,
            Func<WaveModel, float> getter,
            Action<WaveModel, float> setter,
            string widthClass,
            string label)
        {
            var activeWaves = row.Waves.Take(row.WaveCount).ToArray();
            var value = activeWaves.Length == 0 ? 100f : getter(activeWaves[0]);
            var mixed = activeWaves.Any(wave => !Mathf.Approximately(getter(wave), value));
            var field = new FloatField { value = value, isDelayed = true, showMixedValue = mixed };
            field.AddToClassList("wave-number-field");
            field.tooltip = mixed
                ? $"웨이브마다 {label} 배율이 다릅니다. 값을 입력하면 이 단계 전체에 통일됩니다."
                : $"이 단계의 모든 웨이브 {label} 배율";
            field.RegisterValueChangedCallback(evt =>
            {
                foreach (var wave in activeWaves) setter(wave, evt.newValue);
                RebuildTableRows();
                UpdateDirtyState();
            });
            return Cell(field, widthClass);
        }

        private static string ResolveCompositionSummary(WaveModel wave)
        {
            if (wave.Pools.Count == 0) return "구성 없음";
            var labels = wave.Pools.Take(2)
                .Select(pool => $"{GetAppearanceLabel(pool.Appearance)} {pool.Percentage:0.#}%")
                .ToArray();
            return wave.Pools.Count > 2
                ? $"{string.Join(" · ", labels)} 외 {wave.Pools.Count - 2}"
                : string.Join(" · ", labels);
        }

        private void OpenCompositionEditor(StageRow row, int waveIndex)
        {
            selectedCompositionRow = row;
            selectedCompositionWaveIndex = waveIndex;
            RebuildCompositionEditor();
            RebuildTableRows();
        }

        private void RebuildCompositionEditor()
        {
            if (compositionEditor == null) return;
            compositionEditor.Clear();
            var validSelection = selectedCompositionRow != null &&
                                 selectedCompositionWaveIndex >= 0 &&
                                 selectedCompositionWaveIndex < selectedCompositionRow.WaveCount;
            compositionEditor.style.display = validSelection ? DisplayStyle.Flex : DisplayStyle.None;
            if (!validSelection) return;

            var row = selectedCompositionRow;
            var waveIndex = selectedCompositionWaveIndex;
            var wave = row.Waves[waveIndex];
            var top = new VisualElement();
            top.AddToClassList("wave-composition-toolbar");
            var title = new Label($"{row.MinimumStage}단계 · W{waveIndex + 1} 적 구성");
            title.AddToClassList("wave-composition-title");
            top.Add(title);
            top.Add(MakeButton("+ 적 추가", () =>
            {
                wave.AddPool();
                RebuildCompositionEditor();
                RebuildTableRows();
                UpdateDirtyState();
            }, "wave-mini-button"));
            top.Add(MakeButton("100% 정규화", () =>
            {
                wave.NormalizePercentages();
                RebuildCompositionEditor();
                RebuildTableRows();
                UpdateDirtyState();
            }, "wave-mini-button"));
            top.Add(MakeButton("닫기", () =>
            {
                selectedCompositionRow = null;
                selectedCompositionWaveIndex = -1;
                RebuildCompositionEditor();
                RebuildTableRows();
            }, "wave-mini-button"));
            compositionEditor.Add(top);

            var quick = new VisualElement();
            quick.AddToClassList("wave-exception-row");
            quick.Add(CompositionIntField("수량", wave.EnemyCount, value => wave.EnemyCount = value));
            quick.Add(CompositionFloatField("체력 %", wave.HealthPercent, value => wave.HealthPercent = value));
            quick.Add(CompositionFloatField("공격 %", wave.DamagePercent, value => wave.DamagePercent = value));
            quick.Add(CompositionFloatField("방어 %", wave.DefensePercent, value => wave.DefensePercent = value));
            compositionEditor.Add(quick);

            var internalValues = new Foldout { text = "내부값", value = false };
            internalValues.AddToClassList("wave-internal-foldout");
            var internalRow = new VisualElement();
            internalRow.AddToClassList("wave-exception-row");
            internalRow.Add(CompositionFloatField("지연(초)", wave.Delay, value => wave.Delay = value));
            internalRow.Add(CompositionFloatField("전방 위치", wave.ForwardOffset, value => wave.ForwardOffset = value));
            internalValues.Add(internalRow);
            compositionEditor.Add(internalValues);

            var headings = new VisualElement();
            headings.AddToClassList("wave-composition-row");
            headings.Add(CompositionLabel("적 종류", "wc-appearance", true));
            headings.Add(CompositionLabel("역할", "wc-role", true));
            headings.Add(CompositionLabel("출현 %", "wc-percent", true));
            headings.Add(CompositionLabel("최소 등급", "wc-rarity", true));
            headings.Add(CompositionLabel("최대 등급", "wc-rarity", true));
            headings.Add(CompositionLabel("등급 능력치 범위", "wc-stats", true));
            headings.Add(CompositionLabel("편집", "wc-actions", true));
            compositionEditor.Add(headings);

            for (var poolIndex = 0; poolIndex < wave.Pools.Count; poolIndex++)
                compositionEditor.Add(BuildCompositionPoolRow(row, waveIndex, poolIndex));

            var total = new Label($"출현 퍼센트 합계  {wave.PercentageTotal:0.##}%");
            total.AddToClassList("wave-composition-total");
            total.EnableInClassList("wave-invalid", Mathf.Abs(wave.PercentageTotal - 100f) > 0.05f);
            compositionEditor.Add(total);
        }

        private VisualElement CompositionIntField(string label, int value, Action<int> setter)
        {
            var field = new IntegerField(label) { value = value, isDelayed = true };
            field.AddToClassList("wave-exception-field");
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                RebuildTableRows();
                UpdateDirtyState();
            });
            return field;
        }

        private VisualElement CompositionFloatField(string label, float value, Action<float> setter)
        {
            var field = new FloatField(label) { value = value, isDelayed = true };
            field.AddToClassList("wave-exception-field");
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                RebuildTableRows();
                UpdateDirtyState();
            });
            return field;
        }

        private VisualElement BuildCompositionPoolRow(StageRow row, int waveIndex, int poolIndex)
        {
            var wave = row.Waves[waveIndex];
            var pool = wave.Pools[poolIndex];
            var element = new VisualElement();
            element.AddToClassList("wave-composition-row");

            var appearance = new DropdownField(AppearanceChoices.ToList(),
                Mathf.Max(0, Array.IndexOf(AppearanceValues, pool.Appearance)));
            appearance.RegisterValueChangedCallback(_ =>
            {
                pool.Appearance = AppearanceValues[Mathf.Clamp(appearance.index, 0, AppearanceValues.Length - 1)];
                RebuildCompositionEditor();
                RebuildTableRows();
                UpdateDirtyState();
            });
            element.Add(CompositionCell(appearance, "wc-appearance"));
            element.Add(CompositionLabel(GetRoleLabel(pool.Role), "wc-role"));

            var percentage = new FloatField { value = pool.Percentage, isDelayed = true };
            percentage.RegisterValueChangedCallback(evt =>
            {
                pool.Percentage = evt.newValue;
                RebuildCompositionEditor();
                RebuildTableRows();
                UpdateDirtyState();
            });
            element.Add(CompositionCell(percentage, "wc-percent"));

            var minimum = new DropdownField(RarityChoices.ToList(), (int)pool.MinimumRarity);
            minimum.RegisterValueChangedCallback(_ =>
            {
                pool.MinimumRarity = (MonsterRarity)Mathf.Clamp(minimum.index, 0, 4);
                if (pool.MaximumRarity < pool.MinimumRarity) pool.MaximumRarity = pool.MinimumRarity;
                RebuildCompositionEditor();
                RebuildTableRows();
                UpdateDirtyState();
            });
            element.Add(CompositionCell(minimum, "wc-rarity"));

            var maximum = new DropdownField(RarityChoices.ToList(), (int)pool.MaximumRarity);
            maximum.RegisterValueChangedCallback(_ =>
            {
                pool.MaximumRarity = (MonsterRarity)Mathf.Clamp(maximum.index, 0, 4);
                if (pool.MinimumRarity > pool.MaximumRarity) pool.MinimumRarity = pool.MaximumRarity;
                RebuildCompositionEditor();
                RebuildTableRows();
                UpdateDirtyState();
            });
            element.Add(CompositionCell(maximum, "wc-rarity"));
            element.Add(CompositionLabel(
                $"HP ×{ExpeditionEnemyRarityRules.GetHealthMultiplier(pool.MinimumRarity):0.##}~" +
                $"{ExpeditionEnemyRarityRules.GetHealthMultiplier(pool.MaximumRarity):0.##} · " +
                $"공/방 ×{ExpeditionEnemyRarityRules.GetDamageMultiplier(pool.MinimumRarity):0.##}~" +
                $"{ExpeditionEnemyRarityRules.GetDamageMultiplier(pool.MaximumRarity):0.##}",
                "wc-stats"));
            element.Add(CompositionCell(MakeButton("삭제", () =>
            {
                if (wave.Pools.Count <= 1)
                {
                    SetStatus($"{row.MinimumStage}단계 W{waveIndex + 1}에는 적 구성 항목이 하나 이상 필요합니다.", true);
                    return;
                }
                wave.Pools.RemoveAt(poolIndex);
                RebuildCompositionEditor();
                RebuildTableRows();
                UpdateDirtyState();
            }, "wave-mini-button"), "wc-actions"));
            return element;
        }

        private VisualElement GlobalFloat(string label, float value, Action<float> setter)
        {
            var group = new VisualElement();
            group.AddToClassList("wave-global-item");
            group.Add(new Label(label));
            var field = new FloatField { value = value, isDelayed = true };
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                RebuildTableRows();
                UpdateDirtyState();
            });
            group.Add(field);
            return group;
        }

        private VisualElement GlobalInt(string label, int value, Action<int> setter)
        {
            var group = new VisualElement();
            group.AddToClassList("wave-global-item");
            group.Add(new Label(label));
            var field = new IntegerField { value = value, isDelayed = true };
            field.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                RebuildTableRows();
                UpdateDirtyState();
            });
            group.Add(field);
            return group;
        }

        private void UpdateDirtyState()
        {
            var dirtyRows = rows.Count(row => row.IsDirty);
            var globalDirty = globals?.IsDirty == true;
            hasUnsavedChanges = globalDirty || dirtyRows > 0;
            dirtyLabel.text = hasUnsavedChanges
                ? $"변경 {(globalDirty ? "전역 " : string.Empty)}{dirtyRows}개 단계"
                : "변경 없음";
            dirtyLabel.EnableInClassList("wave-dirty--active", hasUnsavedChanges);
            saveButton?.SetEnabled(hasUnsavedChanges);
        }

        private bool CommitChanges(bool confirm)
        {
            if (!hasUnsavedChanges) return true;
            if (globals == null || profile == null) return false;
            if (confirm && !EditorUtility.DisplayDialog(
                    "원정대 웨이브 반영",
                    "표에서 바꾼 전역 적 능력치와 각 웨이브의 수량·구성·HP/공격/방어 배율을 운영 Seed Profile에 반영합니다.",
                    "반영", "취소")) return false;

            try
            {
                ExpeditionWaveBalanceAssetWriter.Apply(
                    profile, sourceJson, globals.ToValues(), rows.Select(row => row.ToValues()).ToArray());
                ReloadRows();
                SetStatus("전역 적 능력치와 100단계의 웨이브별 수량·구성·능력치 배율을 반영했습니다.", false);
                return true;
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("원정대 웨이브 반영 실패", exception.Message, "확인");
                SetStatus(exception.Message, true);
                return false;
            }
        }

        private void ReloadWithConfirmation()
        {
            if (!hasUnsavedChanges || EditorUtility.DisplayDialog(
                    "새로고침", "표에서 수정한 값을 버리고 운영 원본을 다시 불러올까요?", "새로고침", "취소"))
                ReloadRows();
        }

        private void DiscardBufferedChanges()
        {
            if (!hasUnsavedChanges || EditorUtility.DisplayDialog(
                    "변경 취소", "표에서 수정한 값을 모두 버릴까요?", "버리기", "취소"))
                ReloadRows();
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

        private float ResolveHealth(int stage) => globals == null ? 0f :
            Mathf.Floor(globals.BaseHealth * Mathf.Pow(1f + globals.HealthGrowth, Mathf.Max(0, stage - 1)));

        private float ResolveDamage(int stage) => globals == null ? 0f :
            Mathf.Floor(globals.BaseDamage * (1f + globals.DamageGrowth * Mathf.Max(0, stage - 1)));

        private float ResolveDefense(int stage) => globals == null ? 0f :
            globals.BaseDefense * (1f + globals.DefenseGrowth * Mathf.Max(0, stage - 1));

        private float ResolveExpectedStageTotalHealth(StageRow row)
        {
            if (globals == null) return 0f;
            var baseHealth = ResolveHealth(row.MinimumStage);
            var total = 0f;
            for (var waveIndex = 0; waveIndex < row.WaveCount; waveIndex++)
            {
                var wave = row.Waves[waveIndex];
                var count = Mathf.Max(1, wave.EnemyCount);
                var bossSlot = row.MinimumStage % Mathf.Max(1, globals.BossInterval) == 0 &&
                               waveIndex == row.WaveCount - 1;
                var normalCount = count - (bossSlot ? 1 : 0);
                var normalMultiplier = ResolveExpectedHealthMultiplier(wave.Pools, false);
                total += normalCount * Mathf.Floor(baseHealth * normalMultiplier * wave.HealthPercent * 0.01f);
                if (bossSlot)
                {
                    var bossMultiplier = ResolveExpectedHealthMultiplier(wave.Pools, true);
                    total += Mathf.Floor(baseHealth * bossMultiplier * wave.HealthPercent * 0.01f *
                                         globals.BossHealthMultiplier);
                }
            }
            return total;
        }

        private float ResolveExpectedBossHealth(StageRow row)
        {
            if (globals == null || row.WaveCount <= 0) return 0f;
            var wave = row.Waves[row.WaveCount - 1];
            return Mathf.Floor(ResolveHealth(row.MinimumStage) *
                               ResolveExpectedHealthMultiplier(wave.Pools, true) *
                               wave.HealthPercent * 0.01f * globals.BossHealthMultiplier);
        }

        private static float ResolveExpectedHealthMultiplier(IReadOnlyList<PoolModel> pools, bool boss)
        {
            var eligible = pools.Where(pool => pool.Percentage > 0f &&
                (!boss || pool.Role != ExpeditionEnemyRole.Flanker)).ToArray();
            var totalPercentage = eligible.Sum(pool => pool.Percentage);
            if (totalPercentage <= 0.0001f) return 1f;
            var weighted = 0f;
            foreach (var pool in eligible)
            {
                var rarityMultiplier = boss
                    ? ExpeditionEnemyRarityRules.GetHealthMultiplier(pool.MaximumRarity)
                    : ResolveAverageRarityHealthMultiplier(pool.MinimumRarity, pool.MaximumRarity);
                weighted += pool.Percentage / totalPercentage * GetAppearanceHealthMultiplier(pool.Appearance) * rarityMultiplier;
            }
            return weighted;
        }

        private static float ResolveAverageRarityHealthMultiplier(MonsterRarity minimum, MonsterRarity maximum)
        {
            var total = 0f;
            var count = 0;
            for (var rarity = (int)minimum; rarity <= (int)maximum; rarity++)
            {
                total += ExpeditionEnemyRarityRules.GetHealthMultiplier((MonsterRarity)rarity);
                count++;
            }
            return count == 0 ? 1f : total / count;
        }

        private static float GetAppearanceHealthMultiplier(EnemyAppearanceGroup appearance) => appearance switch
        {
            EnemyAppearanceGroup.UpperKnightLower => 1.08f,
            EnemyAppearanceGroup.UpperKnightMid => 1.18f,
            EnemyAppearanceGroup.UpperKnightHigh => 1.3f,
            EnemyAppearanceGroup.UpperKnightFinal => 1.45f,
            EnemyAppearanceGroup.Ninja => 0.7f,
            _ => 1f
        };

        private void SetStatus(string message, bool warning)
        {
            if (statusLabel == null) return;
            statusLabel.text = message;
            statusLabel.EnableInClassList("wave-status--warning", warning);
        }

        private static VisualElement IntCell(int value, Action<int> setter, string widthClass)
        {
            var field = new IntegerField { value = value, isDelayed = true };
            field.AddToClassList("wave-number-field");
            field.RegisterValueChangedCallback(evt => setter(evt.newValue));
            return Cell(field, widthClass);
        }

        private static VisualElement FloatCell(float value, Action<float> setter, string widthClass)
        {
            var field = new FloatField { value = value, isDelayed = true };
            field.AddToClassList("wave-number-field");
            field.RegisterValueChangedCallback(evt => setter(evt.newValue));
            return Cell(field, widthClass);
        }

        private static VisualElement HeaderCell(string text, string widthClass) =>
            LabelCell(text, widthClass, "wave-header-cell");

        private static VisualElement LabelCell(string text, string widthClass, params string[] classes) =>
            Cell(new Label(text), widthClass, classes);

        private static VisualElement Cell(VisualElement child, string widthClass, params string[] classes)
        {
            var cell = new VisualElement();
            cell.AddToClassList("wave-cell");
            cell.AddToClassList(widthClass);
            foreach (var className in classes) cell.AddToClassList(className);
            cell.Add(child);
            return cell;
        }

        private static VisualElement CompositionCell(VisualElement child, string widthClass)
        {
            var cell = new VisualElement();
            cell.AddToClassList("wave-composition-cell");
            cell.AddToClassList(widthClass);
            cell.Add(child);
            return cell;
        }

        private static VisualElement CompositionLabel(string text, string widthClass, bool header = false)
        {
            var label = new Label(text);
            if (header) label.AddToClassList("wave-composition-heading");
            return CompositionCell(label, widthClass);
        }

        private static Button MakeButton(string text, Action clicked, params string[] classes)
        {
            var button = new Button(clicked) { text = text };
            foreach (var className in classes) button.AddToClassList(className);
            return button;
        }

        private static string GetAppearanceLabel(EnemyAppearanceGroup group) => group switch
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

        private sealed class GlobalModel
        {
            private readonly ExpeditionWaveBalanceGlobalValues original;

            public GlobalModel(ExpeditionSeedProfile source)
            {
                BaseHealth = source.EnemyBaseHealth;
                HealthGrowth = source.EnemyHealthGrowthPerStage;
                BaseDamage = source.EnemyBaseDamage;
                DamageGrowth = source.EnemyDamageGrowthPerStage;
                BaseDefense = source.EnemyBaseDefense;
                DefenseGrowth = source.EnemyDefenseGrowthPerStage;
                MeleeRange = source.EnemyMeleeAttackRange;
                RangedRange = source.EnemyRangedAttackRange;
                ChallengeSeconds = source.ChallengeTimeLimitSeconds;
                WaveInterval = source.WaveIntervalSeconds;
                BossInterval = source.BossStageInterval;
                BossHealthMultiplier = source.BossHealthMultiplier;
                original = ToValues();
            }

            public float BaseHealth { get; set; }
            public float HealthGrowth { get; set; }
            public float BaseDamage { get; set; }
            public float DamageGrowth { get; set; }
            public float BaseDefense { get; set; }
            public float DefenseGrowth { get; set; }
            public float MeleeRange { get; set; }
            public float RangedRange { get; set; }
            public float ChallengeSeconds { get; set; }
            public float WaveInterval { get; set; }
            public int BossInterval { get; set; }
            public float BossHealthMultiplier { get; set; }

            public bool IsDirty
            {
                get
                {
                    var value = ToValues();
                    return !Mathf.Approximately(value.BaseHealth, original.BaseHealth) ||
                           !Mathf.Approximately(value.HealthGrowth, original.HealthGrowth) ||
                           !Mathf.Approximately(value.BaseDamage, original.BaseDamage) ||
                           !Mathf.Approximately(value.DamageGrowth, original.DamageGrowth) ||
                           !Mathf.Approximately(value.BaseDefense, original.BaseDefense) ||
                           !Mathf.Approximately(value.DefenseGrowth, original.DefenseGrowth) ||
                           !Mathf.Approximately(value.MeleeRange, original.MeleeRange) ||
                           !Mathf.Approximately(value.RangedRange, original.RangedRange) ||
                           !Mathf.Approximately(value.ChallengeSeconds, original.ChallengeSeconds) ||
                           !Mathf.Approximately(value.WaveInterval, original.WaveInterval) ||
                           value.BossInterval != original.BossInterval ||
                           !Mathf.Approximately(value.BossHealthMultiplier, original.BossHealthMultiplier);
                }
            }

            public ExpeditionWaveBalanceGlobalValues ToValues() => new ExpeditionWaveBalanceGlobalValues(
                BaseHealth, HealthGrowth, BaseDamage, DamageGrowth, BaseDefense, DefenseGrowth,
                MeleeRange, RangedRange, ChallengeSeconds, WaveInterval,
                BossInterval, BossHealthMultiplier);
        }

        private sealed class StageRow
        {
            private readonly int originalWaveCount;

            public StageRow(ExpeditionStageDefinition source, float defaultWaveInterval)
            {
                DefinitionId = source.DefinitionId;
                MinimumStage = source.MinimumStage;
                MaximumStage = source.MaximumStage;
                WaveCount = originalWaveCount = Mathf.Clamp(source.WaveCount, 1, 3);
                WaveModel fallback = null;
                for (var index = 0; index < 3; index++)
                {
                    if (source.TryGetWave(index + 1, out var wave) && wave != null)
                    {
                        var sourcePool = wave.HasSpawnPool ? wave.SpawnPool : source.SpawnPool;
                        fallback = new WaveModel(
                            wave.BaseEnemyCount,
                            wave.SpawnDelaySeconds,
                            wave.FormationForwardOffset,
                            wave.HealthPercent,
                            wave.DamagePercent,
                            wave.DefensePercent,
                            sourcePool);
                    }
                    else
                    {
                        fallback = new WaveModel(
                            fallback?.EnemyCount ?? 8,
                            index == 0 ? 0f : defaultWaveInterval,
                            index * 1.15f,
                            fallback?.HealthPercent ?? 100f,
                            fallback?.DamagePercent ?? 100f,
                            fallback?.DefensePercent ?? 100f,
                            fallback?.Pools.Select(pool => pool.ToValues()).ToArray() ??
                            new[] { new ExpeditionSpawnPoolBalanceValues(
                                EnemyAppearanceGroup.Peasant, 100f,
                                MonsterRarity.Common, MonsterRarity.Common) });
                    }
                    Waves.Add(fallback);
                }
            }

            public string DefinitionId { get; }
            public int MinimumStage { get; }
            public int MaximumStage { get; }
            public int WaveCount { get; set; }
            public List<WaveModel> Waves { get; } = new List<WaveModel>();
            public bool IsDirty => WaveCount != originalWaveCount || Waves.Take(WaveCount).Any(wave => wave.IsDirty);

            public int ResolveTotal() => Waves.Take(WaveCount).Sum(wave => Mathf.Max(1, wave.EnemyCount));

            public ExpeditionWaveBalanceStageValues ToValues() => new ExpeditionWaveBalanceStageValues(
                DefinitionId,
                Waves.Take(WaveCount).Select(wave => wave.ToValues()).ToArray());
        }

        private sealed class PoolModel
        {
            public PoolModel(
                EnemyAppearanceGroup appearance,
                float percentage,
                MonsterRarity minimumRarity,
                MonsterRarity maximumRarity)
            {
                Appearance = appearance;
                Percentage = percentage;
                MinimumRarity = minimumRarity;
                MaximumRarity = maximumRarity;
            }

            public EnemyAppearanceGroup Appearance { get; set; }
            public float Percentage { get; set; }
            public MonsterRarity MinimumRarity { get; set; }
            public MonsterRarity MaximumRarity { get; set; }
            public ExpeditionEnemyRole Role => ExpeditionSpawnPoolEntry.ResolveRole(Appearance);
            public ExpeditionSpawnPoolBalanceValues ToValues() => new ExpeditionSpawnPoolBalanceValues(
                Appearance, Percentage, MinimumRarity, MaximumRarity);
        }

        private sealed class WaveModel
        {
            private readonly ExpeditionWaveBalanceWaveValues original;

            public WaveModel(
                int enemyCount,
                float delay,
                float forwardOffset,
                float healthPercent,
                float damagePercent,
                float defensePercent,
                IEnumerable<ExpeditionSpawnPoolEntry> pools)
                : this(enemyCount, delay, forwardOffset, healthPercent, damagePercent, defensePercent,
                    pools?.Where(pool => pool != null).Select(pool => new ExpeditionSpawnPoolBalanceValues(
                        pool.Appearance, pool.Percentage, pool.MinimumRarity, pool.MaximumRarity)).ToArray())
            {
            }

            public WaveModel(
                int enemyCount,
                float delay,
                float forwardOffset,
                float healthPercent,
                float damagePercent,
                float defensePercent,
                IEnumerable<ExpeditionSpawnPoolBalanceValues> pools)
            {
                EnemyCount = enemyCount;
                Delay = delay;
                ForwardOffset = forwardOffset;
                HealthPercent = healthPercent;
                DamagePercent = damagePercent;
                DefensePercent = defensePercent;
                if (pools != null)
                {
                    foreach (var pool in pools)
                        Pools.Add(new PoolModel(pool.Appearance, pool.Percentage, pool.MinimumRarity, pool.MaximumRarity));
                }
                if (Pools.Count == 0)
                    Pools.Add(new PoolModel(EnemyAppearanceGroup.Peasant, 100f,
                        MonsterRarity.Common, MonsterRarity.Common));
                original = ToValues();
            }

            public int EnemyCount { get; set; }
            public float Delay { get; set; }
            public float ForwardOffset { get; set; }
            public float HealthPercent { get; set; }
            public float DamagePercent { get; set; }
            public float DefensePercent { get; set; }
            public List<PoolModel> Pools { get; } = new List<PoolModel>();
            public float PercentageTotal => Pools.Sum(pool => pool.Percentage);
            public bool IsDirty
            {
                get
                {
                    var current = ToValues();
                    if (current.EnemyCount != original.EnemyCount ||
                        !Mathf.Approximately(current.Delay, original.Delay) ||
                        !Mathf.Approximately(current.ForwardOffset, original.ForwardOffset) ||
                        !Mathf.Approximately(current.HealthPercent, original.HealthPercent) ||
                        !Mathf.Approximately(current.DamagePercent, original.DamagePercent) ||
                        !Mathf.Approximately(current.DefensePercent, original.DefensePercent) ||
                        current.SpawnPool.Count != original.SpawnPool.Count) return true;
                    for (var index = 0; index < current.SpawnPool.Count; index++)
                    {
                        var left = current.SpawnPool[index];
                        var right = original.SpawnPool[index];
                        if (left.Appearance != right.Appearance ||
                            !Mathf.Approximately(left.Percentage, right.Percentage) ||
                            left.MinimumRarity != right.MinimumRarity ||
                            left.MaximumRarity != right.MaximumRarity) return true;
                    }
                    return false;
                }
            }

            public ExpeditionWaveBalanceWaveValues ToValues() => new ExpeditionWaveBalanceWaveValues(
                EnemyCount, Delay, ForwardOffset, HealthPercent, DamagePercent, DefensePercent,
                Pools.Select(pool => pool.ToValues()).ToArray());

            public void AddPool()
            {
                var appearance = AppearanceValues.FirstOrDefault(value => Pools.All(pool => pool.Appearance != value));
                Pools.Add(new PoolModel(appearance, 0f, MonsterRarity.Common, MonsterRarity.Common));
            }

            public void NormalizePercentages()
            {
                var total = PercentageTotal;
                if (total <= 0.0001f)
                {
                    var equal = 100f / Mathf.Max(1, Pools.Count);
                    foreach (var pool in Pools) pool.Percentage = equal;
                    return;
                }
                foreach (var pool in Pools) pool.Percentage = pool.Percentage / total * 100f;
            }
        }
    }
}
