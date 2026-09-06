using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Features.CommanderSkill;
using ProjectMT.Shared.CommanderSkill;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectMT.EditorTools.CommanderSkillWorkshop
{
    public sealed class CommanderSkillWorkshopWindow : EditorWindow // 군단장 공격형·효과형 통합 제작소
    {
        private const string SharedStylePath =
            "Assets/ProjectMT/Editor/MonsterMakerV2/UI/MonsterWorkshopV2Window.uss";
        private const string StylePath =
            "Assets/ProjectMT/Editor/CommanderSkillWorkshop/CommanderSkillWorkshopWindow.uss";
        private const string SessionPrefix = "ProjectMT.CommanderSkillWorkshop.";

        private CommanderSkillWorkshopDraft draft;
        private SerializedObject draftSerialized;
        private CommanderSkillDefinition loaded;
        private CommanderSkillWorkshopPreview preview;
        private bool dirty;
        private bool showHelp = true;
        private bool suppressCallbacks;
        private int assemblerBindingVersion;
        private bool rebuildQueued;
        private bool sessionRestored;
        private string searchText = string.Empty;
        private string statusMessage = string.Empty;
        private bool statusMessageIsError;

        private Label stateBadge;
        private Label loadedLabel;
        private Label libraryCount;
        private ScrollView libraryScroll;
        private ScrollView assemblerScroll;
        private VisualElement validationHost;
        private Label previewSummary;
        private IMGUIContainer previewCanvas;
        private VisualElement iconPreview;
        private Label iconPreviewHint;
        private Button attackTab;
        private Button effectTab;
        private Button saveNewButton;
        private Button forkButton;
        private Button updateButton;
        private Button pingButton;
        private Label saveStatusLabel;

        [MenuItem("JC Tool/Commander/군단장 스킬 제작소", priority = 150)]
        public static void Open()
        {
            var window = GetWindow<CommanderSkillWorkshopWindow>();
            window.titleContent = new GUIContent("군단장 스킬 제작소");
            window.minSize = new Vector2(1120f, 720f);
            window.Show();
        }

        private void OnEnable()
        {
            EnsureDraft();
            EnsurePreviewForEditorState();
            EditorApplication.update -= HandlePreviewUpdate;
            EditorApplication.update += HandlePreviewUpdate;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            RestoreSession();
            saveChangesMessage = "군단장 스킬 제작소에서 편집 중인 스킬에 저장하지 않은 변경이 있습니다.";
        }

        public void CreateGUI()
        {
            BuildRoot();
        }

        private void BuildRoot()
        {
            EnsureDraft();
            var root = rootVisualElement;
            root.Clear();
            var sharedStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(SharedStylePath);
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (sharedStyle != null) root.styleSheets.Add(sharedStyle);
            if (style != null) root.styleSheets.Add(style);
            root.AddToClassList("workshop-root");
            ApplyHelpVisibility();

            root.Add(BuildHeader());
            root.Add(BuildModeTabs());

            var columns = new VisualElement();
            columns.AddToClassList("workshop-columns");
            columns.Add(BuildLibraryPanel());
            columns.Add(BuildAssemblerPanel());
            columns.Add(BuildPreviewPanel());
            root.Add(columns);

            RefreshLibrary();
            RebuildAssembler();
            RefreshState();
        }

        private VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("workshop-header");

            var heading = new VisualElement();
            heading.AddToClassList("workshop-heading");
            var title = new Label("군단장 스킬 제작소");
            title.AddToClassList("workshop-title");
            var caption = new Label("군단장 모델과 VFX를 보며 공격·버프·디버프 스킬을 실제 제작합니다.");
            caption.AddToClassList("workshop-caption");
            heading.Add(title);
            heading.Add(caption);
            header.Add(heading);

            Button help = null;
            help = new Button(() =>
            {
                showHelp = !showHelp;
                ApplyHelpVisibility();
                help.text = showHelp ? "도움말 끄기" : "도움말 켜기";
                help.EnableInClassList("workshop-help-toggle--hidden", !showHelp);
            }) { text = showHelp ? "도움말 끄기" : "도움말 켜기" };
            help.AddToClassList("workshop-help-toggle");
            help.EnableInClassList("workshop-help-toggle--hidden", !showHelp);
            header.Add(help);

            stateBadge = new Label();
            stateBadge.AddToClassList("workshop-state");
            header.Add(stateBadge);
            return header;
        }

        private VisualElement BuildModeTabs()
        {
            var tabs = new VisualElement();
            tabs.AddToClassList("mode-tabs");
            attackTab = new Button(() => SetMode(CommanderSkillCategory.Attack)) { text = "공격형" };
            effectTab = new Button(() =>
                SetMode(draft.Category == CommanderSkillCategory.Debuff
                    ? CommanderSkillCategory.Debuff
                    : CommanderSkillCategory.Buff)) { text = "효과형 · 버프/디버프" };
            attackTab.AddToClassList("mode-tab");
            effectTab.AddToClassList("mode-tab");
            tabs.Add(attackTab);
            tabs.Add(effectTab);
            return tabs;
        }

        private VisualElement BuildLibraryPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("workshop-panel");
            panel.AddToClassList("library-panel");

            var heading = new VisualElement();
            heading.AddToClassList("panel-heading-row");
            var text = new VisualElement();
            var title = new Label("군단장 프리셋");
            title.AddToClassList("panel-title");
            var caption = new Label("게임에서 사용하는 실제 스킬을 불러와 수정하거나 새로 만듭니다.");
            caption.AddToClassList("panel-caption");
            text.Add(title);
            text.Add(caption);
            heading.Add(text);
            libraryCount = new Label("0개");
            libraryCount.AddToClassList("count-badge");
            heading.Add(libraryCount);
            panel.Add(heading);

            var search = new TextField("검색") { value = searchText };
            search.AddToClassList("search-field");
            search.RegisterValueChangedCallback(evt =>
            {
                searchText = evt.newValue ?? string.Empty;
                RefreshLibrary();
            });
            panel.Add(search);

            var newRow = new VisualElement();
            newRow.AddToClassList("commander-new-row");
            newRow.Add(NewButton("+ 공격형", () => NewDraft(CommanderSkillCategory.Attack)));
            newRow.Add(NewButton("+ 효과형", () => NewDraft(CommanderSkillCategory.Buff), true));
            panel.Add(newRow);

            libraryScroll = new ScrollView(ScrollViewMode.Vertical);
            libraryScroll.AddToClassList("library-scroll");
            panel.Add(libraryScroll);

            loadedLabel = new Label();
            loadedLabel.AddToClassList("message-label");
            panel.Add(loadedLabel);
            return panel;
        }

        private VisualElement BuildAssemblerPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("workshop-panel");
            panel.AddToClassList("assembler-panel");

            var heading = new VisualElement();
            heading.AddToClassList("panel-heading-row");
            var text = new VisualElement();
            text.AddToClassList("assembler-heading-text");
            var title = new Label("조립 작업대");
            title.AddToClassList("panel-title");
            var caption = new Label("시전 흐름·판정·효과·연출을 조립하고 실제 스킬 자산으로 저장합니다.");
            caption.AddToClassList("panel-caption");
            text.Add(title);
            text.Add(caption);
            heading.Add(text);
            panel.Add(heading);

            assemblerScroll = new ScrollView(ScrollViewMode.Vertical);
            assemblerScroll.AddToClassList("assembler-scroll");
            panel.Add(assemblerScroll);
            panel.Add(BuildSaveFooter());
            return panel;
        }

        private VisualElement BuildSaveFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("assembler-footer");
            var row = new VisualElement();
            row.AddToClassList("save-row");

            saveNewButton = new Button(() => SaveCurrent(true)) { text = "새 스킬 저장" };
            saveNewButton.AddToClassList("action-button");
            saveNewButton.AddToClassList("action-button--save-new");
            row.Add(saveNewButton);
            updateButton = new Button(() => SaveCurrent(false)) { text = "현재 자산 갱신" };
            updateButton.AddToClassList("action-button");
            updateButton.AddToClassList("action-button--update");
            row.Add(updateButton);
            forkButton = new Button(ForkCurrent) { text = "복제 후 새 작업" };
            forkButton.AddToClassList("action-button");
            forkButton.AddToClassList("action-button--save-new");
            forkButton.AddToClassList("save-row-last");
            row.Add(forkButton);
            footer.Add(row);

            pingButton = new Button(() =>
            {
                if (loaded == null) return;
                Selection.activeObject = loaded;
                EditorGUIUtility.PingObject(loaded);
            }) { text = "현재 자산 Project에서 보기" };
            pingButton.AddToClassList("action-button");
            pingButton.AddToClassList("action-button--assign");
            footer.Add(pingButton);
            saveStatusLabel = new Label();
            saveStatusLabel.AddToClassList("message-label");
            footer.Add(saveStatusLabel);
            return footer;
        }

        private VisualElement BuildPreviewPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("workshop-panel");
            panel.AddToClassList("preview-panel");

            var heading = new VisualElement();
            heading.AddToClassList("panel-heading-row");
            var text = new VisualElement();
            var title = new Label("군단장 스킬 3D Preview");
            title.AddToClassList("panel-title");
            var caption = new Label("군단장 모델에서 캐스팅→발동→적중과 VFX를 직접 재생합니다.");
            caption.AddToClassList("panel-caption");
            text.Add(title);
            text.Add(caption);
            heading.Add(text);
            panel.Add(heading);

            var iconCard = new VisualElement();
            iconCard.AddToClassList("commander-icon-card");
            iconPreview = new VisualElement();
            iconPreview.AddToClassList("commander-icon-slot");
            iconPreviewHint = new Label("SKILL\nIMAGE");
            iconPreviewHint.AddToClassList("commander-icon-hint");
            iconPreview.Add(iconPreviewHint);
            iconCard.Add(iconPreview);
            var iconText = new Label("스킬 이미지\n소환 결과·보유 카드·장착 슬롯·상세창에서 같은 Sprite를 사용합니다.");
            iconText.AddToClassList("commander-icon-copy");
            iconCard.Add(iconText);
            panel.Add(iconCard);

            var toolbar = new VisualElement();
            toolbar.AddToClassList("preview-toolbar");
            var play = new Button(() => preview?.Play()) { text = "▶ 스킬 재생" };
            var stop = new Button(() => preview?.Stop()) { text = "■ 정지" };
            Button loop = null;
            loop = new Button(() =>
            {
                if (preview == null) return;
                preview.SetLooping(!preview.Looping);
                loop.text = preview.Looping ? "↻ 반복 켬" : "↻ 반복 끔";
            }) { text = preview?.Looping == false ? "↻ 반복 끔" : "↻ 반복 켬" };
            var environments = Enumerable.Range(0, PrefabPreviewStage.EnvironmentCount)
                .Select(PrefabPreviewStage.GetEnvironmentLabel)
                .ToList();
            var environment = new PopupField<string>(
                environments,
                Mathf.Clamp(preview?.EnvironmentIndex ?? 0, 0, environments.Count - 1));
            environment.RegisterValueChangedCallback(evt =>
            {
                var index = environments.IndexOf(evt.newValue);
                if (index >= 0) preview?.SetEnvironment(index);
                previewCanvas?.MarkDirtyRepaint();
            });
            play.AddToClassList("commander-preview-play");
            stop.AddToClassList("commander-preview-stop");
            loop.AddToClassList("commander-preview-loop");
            environment.AddToClassList("commander-preview-environment");
            toolbar.Add(play);
            toolbar.Add(stop);
            toolbar.Add(loop);
            toolbar.Add(environment);
            panel.Add(toolbar);

            var growthToolbar = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var previewLevel = new IntegerField("확인 레벨") { value = preview?.PreviewLevel ?? 1, isDelayed = true };
            var previewStar = new IntegerField("확인 별") { value = preview?.PreviewStar ?? 0, isDelayed = true };
            previewLevel.style.flexGrow = 1;
            previewStar.style.flexGrow = 1;
            previewLevel.RegisterValueChangedCallback(evt => preview?.SetProgress(evt.newValue, preview.PreviewStar));
            previewStar.RegisterValueChangedCallback(evt => preview?.SetProgress(preview.PreviewLevel, evt.newValue));
            growthToolbar.Add(previewLevel);
            growthToolbar.Add(previewStar);
            panel.Add(growthToolbar);
            previewCanvas = new IMGUIContainer(DrawPreview);
            previewCanvas.AddToClassList("commander-preview-canvas");
            panel.Add(previewCanvas);
            var previewHelp = new Label("우클릭 드래그: 회전  ·  마우스 휠: 확대/축소");
            previewHelp.AddToClassList("commander-preview-help");
            panel.Add(previewHelp);
            previewSummary = new Label();
            previewSummary.AddToClassList("preview-summary");
            panel.Add(previewSummary);

            var validationTitle = new Label("저장 전 검사");
            validationTitle.AddToClassList("section-title");
            validationTitle.AddToClassList("commander-validation-title");
            panel.Add(validationTitle);
            validationHost = new ScrollView(ScrollViewMode.Vertical);
            validationHost.AddToClassList("commander-validation");
            panel.Add(validationHost);
            return panel;
        }

        private void RefreshLibrary()
        {
            if (libraryScroll == null) return;
            libraryScroll.Clear();
            var assets = FindDefinitions()
                .Where(candidate => string.IsNullOrWhiteSpace(searchText) ||
                                    candidate.SkillId.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    candidate.DisplayName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(candidate => candidate.Category)
                .ThenBy(candidate => candidate.DisplayName, StringComparer.Ordinal)
                .ToArray();
            libraryCount.text = $"{assets.Length}개";

            for (var index = 0; index < assets.Length; index++)
            {
                var asset = assets[index];
                var row = new VisualElement();
                row.AddToClassList("preset-row");
                row.EnableInClassList("preset-row--selected", asset == loaded);
                var button = new Button(() => LoadAsset(asset))
                {
                    text = $"[{CategoryLabel(asset.Category)}]  {asset.DisplayName}\n{asset.SkillId}"
                };
                button.AddToClassList("preset-button");
                row.Add(button);
                libraryScroll.Add(row);
            }
        }

        private static IReadOnlyList<CommanderSkillDefinition> FindDefinitions()
        {
            var roots = new[] { "Assets/ProjectMT/03_Features/CommanderSkill/Resources/CommanderSkills" };
            return AssetDatabase.FindAssets("t:CommanderAttackSkillDefinition", roots)
                .Concat(AssetDatabase.FindAssets("t:CommanderEffectSkillDefinition", roots))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(AssetDatabase.LoadAssetAtPath<CommanderSkillDefinition>)
                .Where(asset => asset != null)
                .ToArray();
        }

        private void RebuildAssembler()
        {
            if (assemblerScroll == null || draft == null) return;
            assemblerBindingVersion++;
            suppressCallbacks = true;
            draftSerialized.Update();
            assemblerScroll.Clear();

            var identity = Section("1. 기본 정보", "ID는 저장 파일·Catalog·세이브 데이터의 안정 키입니다.");
            AddBoundField(identity, "skillId", "스킬 ID");
            AddBoundField(identity, "displayName", "표시 이름");
            AddBoundField(identity, "description", "설명");
            AddBoundField(identity, "icon", "아이콘");
            AddBoundField(identity, "rarity", "등급");
            assemblerScroll.Add(identity);

            var flow = Section("2. 공통 실행 흐름", "모든 군단장 스킬은 캐스팅 완료 뒤 발동하고, 발동 성공 뒤 쿨타임을 시작합니다.");
            AddBoundField(flow, "castTime", "캐스팅 시간 (초)");
            AddBoundField(flow, "autoUseCondition", "AUTO 조건", true);
            if (draft.AutoUseCondition == CommanderSkillAutoUseCondition.AllyHealthBelow)
                AddBoundField(flow, "autoHealthThreshold", "아군 체력 비율 미만");
            AddBoundField(flow, "cooldown", "쿨타임 (초)");
            assemblerScroll.Add(flow);

            var target = Section("3. 대상 선택", "공격·디버프는 적, 버프는 아군으로 자동 고정됩니다.");
            var team = new Label($"대상 진영   {TargetTeamLabel(draft.TargetTeam)}");
            team.AddToClassList("commander-readonly-row");
            target.Add(team);
            AddEnumPopup(
                target,
                "targetSelection",
                "우선 대상",
                new[] { CommanderSkillTargetSelection.Nearest, CommanderSkillTargetSelection.LowestHealth,
                    CommanderSkillTargetSelection.HighestHealth, CommanderSkillTargetSelection.Strongest,
                    CommanderSkillTargetSelection.Random, CommanderSkillTargetSelection.MostCrowded },
                new[] { "가장 가까운 대상", "체력 비율이 가장 낮은 대상", "체력 비율이 가장 높은 대상",
                    "최대 체력이 가장 높은 대상", "무작위 대상", "주변 적이 가장 많은 대상" });
            AddBoundField(target, "targetRange", "탐색 거리 (m)");
            assemblerScroll.Add(target);

            if (draft.Category == CommanderSkillCategory.Attack)
            {
                BuildAttackAssembler();
                BuildAttackAdditionalEffects();
            }
            else
            {
                BuildEffectAssembler();
                BuildPatternAssembler();
            }

            var feedback = new Foldout { text = "5. VFX / SFX 실제 연출", value = true };
            feedback.AddToClassList("section-card");
            feedback.Add(Help("Prefab을 슬롯에 넣으면 우측 군단장 3D Preview에서 실제 크기와 위치로 재생됩니다."));
            feedback.Add(Help("사운드는 AudioClip만 넣으면 저장할 때 해당 스킬 전용 런타임 Cue가 자동 생성·재사용됩니다."));
            var castingFeedback = new VisualElement();
            castingFeedback.AddToClassList("sub-card");
            castingFeedback.Add(CardHeader("캐스팅 시작 연출"));
            castingFeedback.Add(Help("캐스팅 시간이 0초보다 클 때 입력 승인 직후 재생됩니다."));
            AddBoundField(castingFeedback, "castingVfxPrefab", "VFX Prefab", true);
            if (draft.CastingVfxPrefab != null)
            {
                AddBoundField(castingFeedback, "castingVfxLifetime", "재생 수명 (초)");
                AddBoundField(castingFeedback, "castingVfxLocalOffset", "위치 보정");
                AddBoundField(castingFeedback, "castingVfxLocalEuler", "회전 보정");
                AddBoundField(castingFeedback, "castingVfxScale", "크기 배율");
            }
            AddBoundField(castingFeedback, "castingSound", "사운드 (AudioClip)");
            feedback.Add(castingFeedback);

            var castFeedback = new VisualElement();
            castFeedback.AddToClassList("sub-card");
            castFeedback.Add(CardHeader("발동·발사 연출"));
            AddBoundField(castFeedback, "castVfxPrefab", "VFX Prefab", true);
            if (draft.CastVfxPrefab != null)
            {
                AddBoundField(castFeedback, "castVfxLifetime", "재생 수명 (초)");
                AddBoundField(castFeedback, "castVfxLocalOffset", "위치 보정");
                AddBoundField(castFeedback, "castVfxLocalEuler", "회전 보정");
                AddBoundField(castFeedback, "castVfxScale", "크기 배율");
            }
            AddBoundField(castFeedback, "castSound", "사운드 (AudioClip)");
            feedback.Add(castFeedback);

            var impactFeedback = new VisualElement();
            impactFeedback.AddToClassList("sub-card");
            impactFeedback.Add(CardHeader("적중·효과 적용 연출"));
            AddBoundField(impactFeedback, "impactVfxPrefab", "VFX Prefab", true);
            if (draft.ImpactVfxPrefab != null)
            {
                AddBoundField(impactFeedback, "impactVfxLifetime", "재생 수명 (초)");
                AddBoundField(impactFeedback, "impactVfxLocalOffset", "위치 보정");
                AddBoundField(impactFeedback, "impactVfxLocalEuler", "회전 보정");
                AddBoundField(impactFeedback, "impactVfxScale", "크기 배율");
            }
            AddBoundField(impactFeedback, "impactSound", "사운드 (AudioClip)");
            feedback.Add(impactFeedback);

            if (draft.PatternType == CommanderSkillPatternType.PersistentArea)
            {
                var persistentFeedback = new VisualElement();
                persistentFeedback.AddToClassList("sub-card");
                persistentFeedback.Add(CardHeader("PersistentArea 지속 연출"));
                persistentFeedback.Add(Help("PersistentArea 시작 시 1회 생성되고 Pattern 종료 또는 Shutdown 시 반환됩니다."));
                AddBoundField(persistentFeedback, "persistentVfxPrefab", "VFX Prefab", true);
                AddBoundField(persistentFeedback, "persistentVfxLocalOffset", "위치 보정");
                AddBoundField(persistentFeedback, "persistentVfxLocalEuler", "회전 보정");
                AddBoundField(persistentFeedback, "persistentVfxScale", "크기 배율");
                AddBoundField(persistentFeedback, "persistentVfxAnchor", "Anchor");
                feedback.Add(persistentFeedback);
            }
            assemblerScroll.Add(feedback);

            var catalog = new Foldout { text = "6. Catalog · 성장 · 소환 연결", value = true };
            catalog.AddToClassList("section-card");
            catalog.Add(Help("한 번의 저장으로 운영 Catalog, 성장 규칙, 단계별 소환 풀을 함께 검증하고 갱신합니다."));
            AddBoundField(catalog, "registerInCatalog", "Catalog 등록/갱신", true);
            if (draft.RegisterInCatalog)
            {
                AddBoundField(catalog, "maxLevel", "최대 레벨");
                catalog.Add(Help("별각성은 공통 비용 1/2/4/8/16개를 사용합니다. 골드 강화는 중복을 소비하지 않습니다."));
                AddBoundField(catalog, "baseGoldCost", "강화 시작 골드");
                AddBoundField(catalog, "goldCostGrowthMultiplier", "레벨당 골드 증가율");
                AddBoundField(catalog, "maxLevelEffectMultiplier", "최대 레벨 효과 배율");
                AddBoundField(catalog, "includeInSummonPool", "소환 풀 포함", true);
                if (draft.IncludeInSummonPool)
                {
                    AddBoundField(catalog, "minimumSummonLevel", "소환 해금 단계");
                    AddBoundField(catalog, "summonWeight", "소환 가중치");
                }
            }
            assemblerScroll.Add(catalog);
            var awakening = new Foldout { text = "7. 별각성 · 단계별 누적 설정", value = false };
            awakening.AddToClassList("section-card");
            awakening.Add(Help("1~5성 각각의 누적 결과를 입력합니다. 이전 별의 배율을 다시 곱하지 않습니다. 대상은 Effect ID, Trigger는 각인ID/효과ID입니다."));
            AddBoundField(awakening, "awakeningStages", "각성 5단계");
            assemblerScroll.Add(awakening);

            suppressCallbacks = false;
            RefreshState();
        }

        private void BuildAttackAssembler()
        {
            var attack = Section("4. 공격 스킬 만들기", "전달 방식과 타격 모양을 고르면 우측 군단장 Preview에 실제 사거리와 적중 범위가 표시됩니다.");
            AddEnumPopup(
                attack,
                "deliveryModule",
                "전달 방식",
                new[] { MonsterBasicAttackDeliveryModule.Direct, MonsterBasicAttackDeliveryModule.Projectile,
                    MonsterBasicAttackDeliveryModule.TravelingArea },
                new[] { "즉시 판정", "투사체", "지점 판정" },
                true);
            AddEnumPopup(
                attack,
                "damageKind",
                "피해 속성",
                new[]
                {
                    CommanderSkillDamageKind.Physical,
                    CommanderSkillDamageKind.Fire,
                    CommanderSkillDamageKind.Ice,
                    CommanderSkillDamageKind.Arcane
                },
                new[] { "물리", "화염", "냉기", "비전" });
            AddBoundField(attack, "baseDamage", "기본 피해");
            AddBoundField(attack, "perHitMultiplier", "타격당 피해 배율");
            AddEnumPopup(
                attack,
                "shape",
                "판정 모양",
                new[]
                {
                    MonsterBasicAttackShape.Single,
                    MonsterBasicAttackShape.Fan,
                    MonsterBasicAttackShape.Line,
                    MonsterBasicAttackShape.Circle
                },
                new[] { "단일", "부채꼴", "직선", "원형" },
                true);

            if (draft.Shape == MonsterBasicAttackShape.Circle)
            {
                AddEnumPopup(
                    attack,
                    "center",
                    "원형 중심",
                    new[]
                    {
                        MonsterBasicAttackCenter.PrimaryTarget,
                        MonsterBasicAttackCenter.Source,
                        MonsterBasicAttackCenter.Forward
                    },
                    new[] { "주 대상", "시전자", "시전자 전방" },
                    true);
                AddBoundField(attack, "radius", "반경 (m)");
                if (draft.Center == MonsterBasicAttackCenter.Forward)
                {
                    AddBoundField(attack, "forwardOffset", "전방 중심 거리 (m)");
                }
            }
            else if (draft.Shape == MonsterBasicAttackShape.Fan)
            {
                AddBoundField(attack, "angle", "부채꼴 각도");
            }
            else if (draft.Shape == MonsterBasicAttackShape.Line)
            {
                AddBoundField(attack, "lineWidth", "직선 폭 (m)");
            }
            if (draft.Shape != MonsterBasicAttackShape.Single)
            {
                AddBoundField(attack, "maxTargets", "최대 대상 수");
            }

            if (draft.DeliveryModule == MonsterBasicAttackDeliveryModule.Projectile)
            {
                var projectile = new VisualElement();
                projectile.AddToClassList("sub-card");
                projectile.Add(CardHeader("투사체 전달 설정"));
                AddBoundField(projectile, "projectilePrefab", "Projectile Prefab");
                AddBoundField(projectile, "projectileSpeed", "이동 속도 (m/s)");
                AddEnumPopup(
                    projectile,
                    "trajectory",
                    "궤적",
                    new[] { CommanderSkillTrajectory.Straight, CommanderSkillTrajectory.Arc },
                    new[] { "직선", "포물선" },
                    true);
                if (draft.Trajectory == CommanderSkillTrajectory.Arc)
                {
                    AddBoundField(projectile, "arcHeight", "포물선 높이");
                }
                attack.Add(projectile);
            }
            assemblerScroll.Add(attack);
            BuildPatternAssembler();
        }

        private void BuildPatternAssembler()
        {
            var pattern = Section("5. 공격 패턴", "Single을 기본으로 반복·장판·연쇄 실행을 데이터로 조립합니다.");
            AddBoundField(pattern, "patternType", "패턴", true);
            if (draft.PatternType is CommanderSkillPatternType.Burst or CommanderSkillPatternType.Barrage or CommanderSkillPatternType.Pulse)
            {
                AddBoundField(pattern, "repeatCount", "반복 횟수");
                AddBoundField(pattern, "repeatInterval", "반복 간격 (초)");
            }
            if (draft.PatternType == CommanderSkillPatternType.Barrage)
            {
                AddBoundField(pattern, "randomRadius", "분산 반경 (m)");
                AddBoundField(pattern, "firstBarrageHitAtTarget", "첫 포격은 중심 확정");
            }
            if (draft.PatternType == CommanderSkillPatternType.PersistentArea)
            {
                AddBoundField(pattern, "patternDuration", "지속 시간 (초)");
                AddBoundField(pattern, "tickInterval", "틱 간격 (초)");
            }
            if (draft.PatternType == CommanderSkillPatternType.Chain)
            {
                AddBoundField(pattern, "chainCount", "연쇄 횟수");
                AddBoundField(pattern, "chainRadius", "연쇄 반경 (m)");
                AddBoundField(pattern, "repeatInterval", "연쇄 간격 (초)");
            }
            assemblerScroll.Add(pattern);
        }

        private void BuildAttackAdditionalEffects()
        {
            var section = Section("6. 추가 효과", "피해와 함께 상태 효과를 순서대로 적용합니다.");
            var effectsProperty = draftSerialized.FindProperty("effects");
            for (var index = 0; index < effectsProperty.arraySize; index++) BuildEffectCard(section, effectsProperty, index);
            var add = new Button(AddEffect) { text = "+ 상태 효과 추가" };
            add.AddToClassList("add-button");
            add.SetEnabled(effectsProperty.arraySize < 8);
            section.Add(add);
            assemblerScroll.Add(section);
        }

        private void BuildEffectAssembler()
        {
            var effectSection = Section("4. 효과형 액티브 조립", "효과 카드는 같은 발동 시점에 순서대로 적용됩니다. 지속 효과는 각 카드의 지속시간을 사용합니다.");
            var categoryRow = new VisualElement();
            categoryRow.AddToClassList("commander-category-row");
            var buff = new Button(() => SetMode(CommanderSkillCategory.Buff)) { text = "버프형" };
            var debuff = new Button(() => SetMode(CommanderSkillCategory.Debuff)) { text = "디버프형" };
            buff.AddToClassList("commander-category-button");
            debuff.AddToClassList("commander-category-button");
            debuff.AddToClassList("commander-category-button--last");
            buff.EnableInClassList("commander-category-button--active", draft.Category == CommanderSkillCategory.Buff);
            debuff.EnableInClassList("commander-category-button--active", draft.Category == CommanderSkillCategory.Debuff);
            categoryRow.Add(buff);
            categoryRow.Add(debuff);
            effectSection.Add(categoryRow);

            var effectsProperty = draftSerialized.FindProperty("effects");
            for (var index = 0; index < effectsProperty.arraySize; index++)
            {
                BuildEffectCard(effectSection, effectsProperty, index);
            }
            var add = new Button(AddEffect) { text = "+ 효과 카드 추가" };
            add.AddToClassList("add-button");
            add.SetEnabled(effectsProperty.arraySize < 8);
            effectSection.Add(add);
            assemblerScroll.Add(effectSection);
        }

        private void BuildEffectCard(VisualElement parent, SerializedProperty effectsProperty, int index)
        {
            var card = new VisualElement();
            card.AddToClassList("sub-card");
            var up = SmallButton("↑", () => MoveEffect(index, index - 1), index > 0);
            var down = SmallButton("↓", () => MoveEffect(index, index + 1), index < effectsProperty.arraySize - 1);
            var duplicate = SmallButton("복제", () => DuplicateEffect(index), true);
            var remove = SmallButton("삭제", () => RemoveEffect(index), draft.Category == CommanderSkillCategory.Attack || effectsProperty.arraySize > 1, true);
            card.Add(CardHeader($"효과 {index + 1:00}", up, down, duplicate, remove));

            var element = effectsProperty.GetArrayElementAtIndex(index);
            AddBoundField(card, element.FindPropertyRelative("effectId"), "효과 ID");
            AddBoundField(card, element.FindPropertyRelative("kind"), "효과 블록", true);
            var kind = (CommanderSkillWorkshopEffectKind)element.FindPropertyRelative("kind").intValue;
            if (kind == CommanderSkillWorkshopEffectKind.CommanderMark)
            {
                AddBoundField(card, element.FindPropertyRelative("sharedMarkDefinition"), "공용 Mark Definition", true);
                if (element.FindPropertyRelative("sharedMarkDefinition").objectReferenceValue != null)
                {
                    card.Add(new HelpBox("공용 Mark 자산을 그대로 참조합니다. 값을 바꾸려면 공용 자산에서 편집하거나 참조를 비우세요.", HelpBoxMessageType.Info));
                    var sharedPath = element.FindPropertyRelative("sharedMarkDefinition").propertyPath;
                    foreach (var slotName in new[] { "Apply", "Loop", "Stack", "Trigger", "Remove" })
                    {
                        var selectedSlot = slotName;
                        card.Add(new Button(() =>
                        {
                            draftSerialized.ApplyModifiedProperties();
                            var shared = draftSerialized.FindProperty(sharedPath).objectReferenceValue as CommanderMarkEffectDefinition;
                            if (shared == null) return;
                            var slot = selectedSlot switch
                            {
                                "Apply" => shared.OnApply, "Loop" => shared.Loop, "Stack" => shared.OnStack,
                                "Trigger" => shared.OnTrigger, _ => shared.OnRemove
                            };
                            PreviewMarkSlot(CommanderMarkFeedbackDraft.FromDefinition(slot));
                        }) { text = $"{selectedSlot} 미리보기" });
                    }
                    parent.Add(card);
                    return;
                }
                AddBoundField(card, element.FindPropertyRelative("markId"), "Mark ID");
                AddBoundField(card, element.FindPropertyRelative("duration"), "지속 시간 (초)");
                AddBoundField(card, element.FindPropertyRelative("scope"), "적용 범위");
                AddBoundField(card, element.FindPropertyRelative("radius"), "적용/발동 반경 (m)");
                AddBoundField(card, element.FindPropertyRelative("maxTargets"), "최대 대상 수");
                AddBoundField(card, element.FindPropertyRelative("markTrigger"), "발동 조건", true);
                var trigger = (CommanderMarkTriggerType)element.FindPropertyRelative("markTrigger").intValue;
                if (trigger == CommanderMarkTriggerType.HitCount) AddBoundField(card, element.FindPropertyRelative("requiredHits"), "필요 피격 수");
                if (trigger == CommanderMarkTriggerType.StackReached) AddBoundField(card, element.FindPropertyRelative("requiredStacks"), "필요 스택");
                AddBoundField(card, element.FindPropertyRelative("markMaxStacks"), "최대 스택");
                AddBoundField(card, element.FindPropertyRelative("consumeOnTrigger"), "발동 후 소모");
                AddBoundField(card, element.FindPropertyRelative("refreshDurationOnApply"), "재적용 시 시간 갱신");
                AddBoundField(card, element.FindPropertyRelative("triggerCooldown"), "발동 내부 쿨타임");
                AddBoundField(card, element.FindPropertyRelative("recordHitCount"), "피격 수 기록");
                var originFilter = new Foldout { text = "고급 · Trigger Count Source", value = false };
                AddBoundField(originFilter, element.FindPropertyRelative("countBasicAttack"), "Basic Attack");
                AddBoundField(originFilter, element.FindPropertyRelative("countMonsterSkill"), "Monster Skill");
                AddBoundField(originFilter, element.FindPropertyRelative("countCommanderSkill"), "Commander Skill");
                AddBoundField(originFilter, element.FindPropertyRelative("countCommanderMarkTrigger"), "Commander Mark Trigger");
                card.Add(originFilter);
                BuildTriggerEffects(card, element, index);
                AddMarkFeedbackFoldout(card, element, "onApply", "OnApply");
                AddMarkFeedbackFoldout(card, element, "loop", "Loop");
                AddMarkFeedbackFoldout(card, element, "onStack", "OnStack");
                AddMarkFeedbackFoldout(card, element, "onTrigger", "OnTrigger");
                AddMarkFeedbackFoldout(card, element, "onRemove", "OnRemove");
                parent.Add(card);
                return;
            }
            if (kind == CommanderSkillWorkshopEffectKind.Pull)
            {
                card.Add(Help("첫 타격의 실제 피해 대상만 1회 당깁니다. 레벨·별로 거리/시간이 늘어나지 않습니다. 안전 구역을 모르는 콘텐츠에서는 미적용됩니다."));
                AddBoundField(card, element.FindPropertyRelative("pullCenter"), "당김 중심");
                AddBoundField(card, element.FindPropertyRelative("pullDistance"), "최대 거리 (m)");
                AddBoundField(card, element.FindPropertyRelative("pullDuration"), "이동 시간 (초)");
                AddBoundField(card, element.FindPropertyRelative("pullStopDistance"), "중심 여유거리 (m)");
                AddBoundField(card, element.FindPropertyRelative("pullMaxTargets"), "최대 대상");
                parent.Add(card);
                return;
            }
            if (kind == CommanderSkillWorkshopEffectKind.RecordedHitDamage)
            {
                AddBoundField(card, element.FindPropertyRelative("recordedBaseMultiplier"), "기본 배율");
                AddBoundField(card, element.FindPropertyRelative("recordedMultiplierPerHit"), "기록 1회당 배율");
                AddBoundField(card, element.FindPropertyRelative("maximumRecordedHits"), "최대 기록 수");
                parent.Add(card);
                return;
            }
            if (kind == CommanderSkillWorkshopEffectKind.AreaDamage)
            {
                BuildDamageFields(card, element);
                parent.Add(card);
                return;
            }
            if (kind == CommanderSkillWorkshopEffectKind.GlobalModifier)
            {
                AddBoundField(card, element.FindPropertyRelative("duration"), "지속 시간 (초)");
                AddBoundField(card, element.FindPropertyRelative("markRequiredHitsMultiplier"), "Mark 필요 Hit 배율");
                AddBoundField(card, element.FindPropertyRelative("markTriggerDamageMultiplier"), "Mark 발동 피해 배율");
                AddBoundField(card, element.FindPropertyRelative("cooldownRecoveryMultiplier"), "쿨타임 회복 배율");
                parent.Add(card);
                return;
            }
            var options = EffectTypeOptions(draft.Category);
            AddEnumPopup(
                card,
                element.FindPropertyRelative("effectType"),
                "효과 종류",
                options.Values,
                options.Labels,
                true);
            var type = (CommanderSkillUnitEffectType)element.FindPropertyRelative("effectType").intValue;
            var sourceOptions = EffectValueSourceOptions(type);
            var sourceProperty = element.FindPropertyRelative("valueSource");
            if (!sourceOptions.Values.Contains((CommanderSkillEffectValueSource)sourceProperty.intValue))
            {
                sourceProperty.intValue = (int)sourceOptions.Values[0];
                draftSerialized.ApplyModifiedPropertiesWithoutUndo();
            }
            AddEnumPopup(
                card,
                sourceProperty,
                "수치 기준",
                sourceOptions.Values,
                sourceOptions.Labels);
            var source = (CommanderSkillEffectValueSource)sourceProperty.intValue;
            AddBoundField(
                card,
                element.FindPropertyRelative("magnitude"),
                EffectMagnitudeLabel(type, source));

            if (CommanderUnitEffectDefinition.RequiresDuration(type))
            {
                AddBoundField(card, element.FindPropertyRelative("duration"), "지속 시간 (초)");
            }
            AddEnumPopup(
                card,
                element.FindPropertyRelative("scope"),
                "적용 범위",
                new[] { CommanderSkillEffectScope.PrimaryTarget, CommanderSkillEffectScope.Area, CommanderSkillEffectScope.ImpactTargets },
                new[] { "주 대상 1기", "주 대상 주변", "실제 피격 대상" },
                true);
            var scope = (CommanderSkillEffectScope)element.FindPropertyRelative("scope").intValue;
            if (scope == CommanderSkillEffectScope.Area)
            {
                AddBoundField(card, element.FindPropertyRelative("radius"), "적용 반경 (m)");
                AddBoundField(card, element.FindPropertyRelative("maxTargets"), "최대 대상 수");
            }

            var advanced = new Foldout { text = "고급 · 중첩 규칙", value = false };
            AddEnumPopup(
                advanced,
                element.FindPropertyRelative("stackPolicy"),
                "같은 효과 재적용",
                new[] { MonsterBuffStackPolicy.RefreshDuration, MonsterBuffStackPolicy.ReplaceIfStronger },
                new[] { "지속시간 갱신", "더 강할 때 교체" });
            card.Add(advanced);
            parent.Add(card);
        }

        private void BuildTriggerEffects(VisualElement card, SerializedProperty mark, int markIndex)
        {
            var foldout = new Foldout { text = "Trigger Effects", value = true };
            var effects = mark.FindPropertyRelative("triggerEffects");
            for (var index = 0; index < effects.arraySize; index++)
                BuildTriggerEffectCard(foldout, effects, markIndex, index);
            var add = new Button(() => AddTriggerEffect(markIndex)) { text = "+ Trigger Effect 추가" };
            add.SetEnabled(effects.arraySize < 8);
            add.AddToClassList("add-button");
            foldout.Add(add);
            card.Add(foldout);
        }

        private void BuildTriggerEffectCard(VisualElement parent, SerializedProperty effects,
            int markIndex, int triggerIndex)
        {
            var element = effects.GetArrayElementAtIndex(triggerIndex);
            var card = new VisualElement();
            card.AddToClassList("sub-card");
            card.Add(CardHeader($"Trigger {triggerIndex + 1:00}",
                SmallButton("삭제", () => RemoveTriggerEffect(markIndex, triggerIndex), true, true)));
            AddBoundField(card, element.FindPropertyRelative("effectId"), "효과 ID");
            AddEnumPopup(card, element.FindPropertyRelative("kind"), "효과 블록",
                new[] { CommanderSkillWorkshopEffectKind.AreaDamage, CommanderSkillWorkshopEffectKind.UnitEffect,
                    CommanderSkillWorkshopEffectKind.RecordedHitDamage },
                new[] { "Area Damage", "Unit Effect", "Recorded Hit Damage" }, true);
            var kind = (CommanderSkillWorkshopEffectKind)element.FindPropertyRelative("kind").intValue;
            if (kind == CommanderSkillWorkshopEffectKind.AreaDamage)
                BuildDamageFields(card, element);
            else if (kind == CommanderSkillWorkshopEffectKind.RecordedHitDamage)
            {
                AddBoundField(card, element.FindPropertyRelative("recordedBaseMultiplier"), "기본 배율");
                AddBoundField(card, element.FindPropertyRelative("recordedMultiplierPerHit"), "기록 1회당 배율");
                AddBoundField(card, element.FindPropertyRelative("maximumRecordedHits"), "최대 기록 수");
            }
            else
            {
                AddBoundField(card, element.FindPropertyRelative("effectType"), "효과 종류", true);
                AddBoundField(card, element.FindPropertyRelative("valueSource"), "수치 기준");
                AddBoundField(card, element.FindPropertyRelative("magnitude"), "효과 수치");
                AddBoundField(card, element.FindPropertyRelative("duration"), "지속 시간 (초)");
                AddEnumPopup(card, element.FindPropertyRelative("scope"), "적용 범위",
                    new[] { CommanderSkillEffectScope.PrimaryTarget, CommanderSkillEffectScope.Area,
                        CommanderSkillEffectScope.ImpactTargets },
                    new[] { "주 대상 1기", "주 대상 주변", "실제 피격 대상" });
                if ((CommanderSkillEffectScope)element.FindPropertyRelative("scope").intValue == CommanderSkillEffectScope.Area)
                {
                    AddBoundField(card, element.FindPropertyRelative("radius"), "적용 반경 (m)");
                    AddBoundField(card, element.FindPropertyRelative("maxTargets"), "최대 대상 수");
                }
            }
            parent.Add(card);
        }

        private void BuildDamageFields(VisualElement card, SerializedProperty element)
        {
            AddBoundField(card, element.FindPropertyRelative("damageKind"), "피해 종류");
            AddBoundField(card, element.FindPropertyRelative("baseDamage"), "기본 피해");
            AddBoundField(card, element.FindPropertyRelative("perHitMultiplier"), "타격 배율");
            AddBoundField(card, element.FindPropertyRelative("damageShape"), "판정 Shape", true);
            AddBoundField(card, element.FindPropertyRelative("damageCenter"), "판정 중심");
            AddBoundField(card, element.FindPropertyRelative("radius"), "반경 (m)");
            AddBoundField(card, element.FindPropertyRelative("forwardOffset"), "전방 Offset");
            AddBoundField(card, element.FindPropertyRelative("angle"), "부채꼴 각도");
            AddBoundField(card, element.FindPropertyRelative("lineWidth"), "선 폭");
            AddBoundField(card, element.FindPropertyRelative("maxTargets"), "최대 대상 수");
        }

        private void AddTriggerEffect(int markIndex)
        {
            draftSerialized.Update();
            var triggers = draftSerialized.FindProperty("effects").GetArrayElementAtIndex(markIndex)
                .FindPropertyRelative("triggerEffects");
            var index = triggers.arraySize;
            triggers.arraySize++;
            InitializeTriggerEffect(triggers.GetArrayElementAtIndex(index), triggers, index);
            ApplyStructuralChange();
        }

        private void RemoveTriggerEffect(int markIndex, int triggerIndex)
        {
            draftSerialized.Update();
            var triggers = draftSerialized.FindProperty("effects").GetArrayElementAtIndex(markIndex)
                .FindPropertyRelative("triggerEffects");
            if (triggerIndex < 0 || triggerIndex >= triggers.arraySize) return;
            triggers.DeleteArrayElementAtIndex(triggerIndex);
            ApplyStructuralChange();
        }

        private void InitializeTriggerEffect(SerializedProperty element, SerializedProperty triggers, int index)
        {
            element.FindPropertyRelative("effectId").stringValue = CreateUniqueEffectId(triggers, $"trigger_{index + 1:00}");
            element.FindPropertyRelative("kind").intValue = (int)CommanderSkillWorkshopEffectKind.AreaDamage;
            element.FindPropertyRelative("damageKind").intValue = (int)draft.DamageKind;
            element.FindPropertyRelative("baseDamage").floatValue = draft.BaseDamage;
            element.FindPropertyRelative("perHitMultiplier").floatValue = 1f;
            element.FindPropertyRelative("damageShape").intValue = (int)MonsterBasicAttackShape.Circle;
            element.FindPropertyRelative("damageCenter").intValue = (int)MonsterBasicAttackCenter.PrimaryTarget;
            element.FindPropertyRelative("radius").floatValue = 2f;
            element.FindPropertyRelative("maxTargets").intValue = 8;
        }

        private void AddMarkFeedbackFoldout(VisualElement card, SerializedProperty effect,
            string propertyName, string label)
        {
            var slot = effect.FindPropertyRelative(propertyName);
            var foldout = new Foldout { text = $"Mark Feedback · {label}", value = false };
            AddBoundField(foldout, slot.FindPropertyRelative("vfxPrefab"), "VFX Prefab");
            AddBoundField(foldout, slot.FindPropertyRelative("sound"), "SFX");
            AddBoundField(foldout, slot.FindPropertyRelative("lifetime"), "Lifetime");
            AddBoundField(foldout, slot.FindPropertyRelative("localOffset"), "Offset");
            AddBoundField(foldout, slot.FindPropertyRelative("localEuler"), "Rotation");
            AddBoundField(foldout, slot.FindPropertyRelative("scale"), "Scale");
            AddBoundField(foldout, slot.FindPropertyRelative("anchor"), "Anchor");
            var slotPath = slot.propertyPath;
            foldout.Add(new Button(() =>
            {
                draftSerialized.ApplyModifiedProperties();
                PreviewMarkSlot(draftSerialized.FindProperty(slotPath).boxedValue as CommanderMarkFeedbackDraft);
            }) { text = "미리보기" });
            card.Add(foldout);
        }

        private void PreviewMarkSlot(CommanderMarkFeedbackDraft slot)
        {
            EnsurePreviewForEditorState();
            if (preview == null) return;
            preview.SetSource(draft);
            preview.PlayMarkFeedback(slot);
            previewCanvas?.MarkDirtyRepaint();
        }

        private void AddEffect()
        {
            draftSerialized.Update();
            var effects = draftSerialized.FindProperty("effects");
            var index = effects.arraySize;
            effects.arraySize++;
            InitializeEffect(effects.GetArrayElementAtIndex(index), index);
            ApplyStructuralChange();
        }

        private void DuplicateEffect(int index)
        {
            draftSerialized.Update();
            var effects = draftSerialized.FindProperty("effects");
            effects.InsertArrayElementAtIndex(index);
            var copy = effects.GetArrayElementAtIndex(index + 1);
            copy.FindPropertyRelative("effectId").stringValue = CreateUniqueEffectId(effects, "effect_copy");
            ApplyStructuralChange();
        }

        private void RemoveEffect(int index)
        {
            draftSerialized.Update();
            var effects = draftSerialized.FindProperty("effects");
            if (effects.arraySize <= (draft.Category == CommanderSkillCategory.Attack ? 0 : 1)) return;
            effects.DeleteArrayElementAtIndex(index);
            ApplyStructuralChange();
        }

        private void MoveEffect(int from, int to)
        {
            draftSerialized.Update();
            var effects = draftSerialized.FindProperty("effects");
            if (from < 0 || from >= effects.arraySize || to < 0 || to >= effects.arraySize) return;
            effects.MoveArrayElement(from, to);
            ApplyStructuralChange();
        }

        private void InitializeEffect(SerializedProperty element, int index)
        {
            var debuff = draft.Category == CommanderSkillCategory.Debuff;
            element.FindPropertyRelative("effectId").stringValue =
                CreateUniqueEffectId(draftSerialized.FindProperty("effects"), $"effect_{index + 1:00}");
            element.FindPropertyRelative("kind").intValue = (int)CommanderSkillWorkshopEffectKind.UnitEffect;
            element.FindPropertyRelative("effectType").intValue = (int)(debuff
                ? CommanderSkillUnitEffectType.Slow
                : CommanderSkillUnitEffectType.Heal);
            element.FindPropertyRelative("valueSource").intValue = (int)(debuff
                ? CommanderSkillEffectValueSource.Flat
                : CommanderSkillEffectValueSource.TargetMissingHealthRatio);
            element.FindPropertyRelative("magnitude").floatValue = debuff ? 0.2f : 0.25f;
            element.FindPropertyRelative("duration").floatValue = debuff ? 4f : 0f;
            element.FindPropertyRelative("scope").intValue = (int)CommanderSkillEffectScope.Area;
            element.FindPropertyRelative("radius").floatValue = 5f;
            element.FindPropertyRelative("maxTargets").intValue = 8;
            element.FindPropertyRelative("stackPolicy").intValue = (int)MonsterBuffStackPolicy.RefreshDuration;
        }

        private void ApplyStructuralChange()
        {
            draftSerialized.ApplyModifiedPropertiesWithoutUndo();
            MarkDirty();
            QueueRebuild();
        }

        private void DrawPreview()
        {
            var rect = GUILayoutUtility.GetRect(220f, 270f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            preview?.Render(rect);
        }

        private void RefreshState()
        {
            if (draft == null) return;
            hasUnsavedChanges = dirty;
            if (stateBadge != null)
            {
                stateBadge.text = loaded == null
                    ? dirty ? "미저장" : "새 작업"
                    : dirty ? "수정됨" : "저장됨";
                stateBadge.EnableInClassList("workshop-state--dirty", dirty || loaded == null);
                stateBadge.EnableInClassList("workshop-state--saved", !dirty && loaded != null);
            }
            attackTab?.EnableInClassList("mode-tab--active", draft.Category == CommanderSkillCategory.Attack);
            effectTab?.EnableInClassList("mode-tab--active", draft.Category != CommanderSkillCategory.Attack);

            var sameIdentity = loaded != null && string.Equals(
                loaded.SkillId,
                draft.SkillId,
                StringComparison.Ordinal);
            var sameType = loaded != null &&
                           (draft.Category == CommanderSkillCategory.Attack
                               ? loaded is CommanderAttackSkillDefinition
                               : loaded is CommanderEffectSkillDefinition);
            updateButton?.SetEnabled(sameIdentity && sameType);
            if (saveNewButton != null)
            {
                saveNewButton.style.display = loaded == null ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (updateButton != null)
            {
                updateButton.style.display = loaded == null ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (forkButton != null)
            {
                forkButton.style.display = loaded == null ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (pingButton != null)
            {
                pingButton.style.display = loaded == null ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (loadedLabel != null)
            {
                var source = loaded == null
                    ? "현재: 새 스킬"
                    : $"현재: {AssetDatabase.GetAssetPath(loaded)}";
                loadedLabel.text = string.IsNullOrWhiteSpace(statusMessage)
                    ? source
                    : $"{statusMessage}\n{source}";
                loadedLabel.EnableInClassList("message-label--error", statusMessageIsError);
            }
            if (saveStatusLabel != null)
            {
                saveStatusLabel.text = string.IsNullOrWhiteSpace(statusMessage)
                    ? loaded == null
                        ? "새 ID로 실제 스킬 자산을 저장합니다."
                        : sameIdentity && sameType
                            ? "현재 자산을 갱신하거나 복제해 새 ID로 작업할 수 있습니다."
                            : "종류 또는 ID가 달라졌습니다. 복제 후 새 작업으로 전환하세요."
                    : statusMessage;
                saveStatusLabel.EnableInClassList("message-label--error", statusMessageIsError);
            }

            if (iconPreview != null)
            {
                iconPreview.style.backgroundImage = draft.Icon == null
                    ? new StyleBackground()
                    : new StyleBackground(draft.Icon);
                iconPreviewHint.style.display = draft.Icon == null ? DisplayStyle.Flex : DisplayStyle.None;
            }
            preview?.SetSource(draft);
            if (previewSummary != null)
            {
                previewSummary.text = $"{preview?.PhaseLabel ?? "Preview 준비 전"}\n{BuildPreviewSummary()}";
            }
            previewCanvas?.MarkDirtyRepaint();
            RefreshValidation();
        }

        private string BuildPreviewSummary()
        {
            if (draft.Category == CommanderSkillCategory.Attack)
            {
                var delivery = draft.DeliveryModule == MonsterBasicAttackDeliveryModule.Projectile
                    ? "투사체"
                    : "즉시 판정";
                return $"{CategoryLabel(draft.Category)} · {delivery} · {draft.PatternType} · {ShapeLabel(draft.Shape)} · " +
                       $"피해 {draft.BaseDamage:0.##} · 최대 {Mathf.Max(1, draft.MaxTargets)}기";
            }

            var effectNames = draft.Effects == null
                ? string.Empty
                : string.Join(" + ", draft.Effects.Select(FormatEffectSummary));
            return $"{CategoryLabel(draft.Category)} · {TargetTeamLabel(draft.TargetTeam)} · " +
                    $"효과 {draft.Effects?.Count ?? 0}개 · {effectNames}";
        }

        private void RefreshValidation()
        {
            if (validationHost == null) return;
            validationHost.Clear();
            var validation = CommanderSkillWorkshopValidator.Validate(draft);
            if (validation.IsValid)
            {
                var success = new Label("실제 스킬 자산으로 저장할 준비가 됐습니다.");
                success.AddToClassList("commander-validation-success");
                validationHost.Add(success);
            }
            for (var index = 0; index < validation.Errors.Count; index++)
            {
                var label = new Label("오류 · " + validation.Errors[index]);
                label.AddToClassList("error-text");
                validationHost.Add(label);
            }
            for (var index = 0; index < validation.Warnings.Count; index++)
            {
                var label = new Label("확인 · " + validation.Warnings[index]);
                label.AddToClassList("warning-text");
                validationHost.Add(label);
            }
        }

        private void SetMode(CommanderSkillCategory category)
        {
            if (draft.Category == category) return;
            draft.SetCategory(category);
            draftSerialized.Update();
            MarkDirty();
            QueueRebuild();
        }

        private void NewDraft(CommanderSkillCategory category)
        {
            if (!ResolveUnsavedChanges()) return;
            assemblerBindingVersion++;
            loaded = null;
            draft.ResetDraft(category);
            draftSerialized.Update();
            dirty = false;
            statusMessage = "새 스킬 편집을 시작했습니다.";
            statusMessageIsError = false;
            PersistSession();
            RefreshLibrary();
            QueueRebuild();
        }

        private void LoadAsset(CommanderSkillDefinition asset)
        {
            if (asset == null || asset == loaded || !ResolveUnsavedChanges()) return;
            assemblerBindingVersion++;
            loaded = asset;
            draft.Load(asset);
            LoadRegistrationSettings(asset);
            draftSerialized.Update();
            dirty = false;
            statusMessage = $"불러오기 완료: {asset.DisplayName}";
            statusMessageIsError = false;
            PersistSession();
            RefreshLibrary();
            QueueRebuild();
        }

        private bool SaveCurrent(bool saveAsNew)
        {
            draftSerialized.ApplyModifiedProperties();
            var result = saveAsNew
                ? CommanderSkillWorkshopWriter.SaveNew(draft)
                : CommanderSkillWorkshopWriter.Update(draft, loaded);
            statusMessage = result.Message;
            statusMessageIsError = !result.Success;
            if (!result.Success)
            {
                RefreshState();
                return false;
            }

            loaded = result.Asset;
            assemblerBindingVersion++;
            draft.Load(loaded);
            LoadRegistrationSettings(loaded);
            draftSerialized.Update();
            dirty = false;
            hasUnsavedChanges = false;
            Selection.activeObject = loaded;
            EditorGUIUtility.PingObject(loaded);
            PersistSession();
            RefreshLibrary();
            QueueRebuild();
            return true;
        }

        private void ForkCurrent()
        {
            if (loaded == null)
            {
                return;
            }

            var nextId = CreateUniqueSkillId(draft.SkillId);
            var nextName = string.IsNullOrWhiteSpace(draft.DisplayName)
                ? "새 군단장 스킬 복사본"
                : draft.DisplayName.EndsWith("복사본", StringComparison.Ordinal)
                    ? draft.DisplayName
                    : draft.DisplayName + " 복사본";
            loaded = null;
            draft.PrepareFork(nextId, nextName);
            draftSerialized.Update();
            dirty = true;
            hasUnsavedChanges = true;
            statusMessage = $"복제본을 새 작업으로 열었습니다. 새 ID: {nextId}";
            statusMessageIsError = false;
            PersistSession();
            RefreshLibrary();
            QueueRebuild();
        }

        private bool ResolveUnsavedChanges()
        {
            if (!dirty) return true;
            var choice = EditorUtility.DisplayDialogComplex(
                "군단장 스킬 편집",
                "저장하지 않은 변경이 있습니다.",
                "저장 후 계속",
                "취소",
                "변경 버리기");
            if (choice == 0)
            {
                return SaveCurrent(loaded == null);
            }
            if (choice == 2)
            {
                dirty = false;
                hasUnsavedChanges = false;
                return true;
            }
            return false;
        }

        private void LoadRegistrationSettings(CommanderSkillDefinition asset)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CommanderSkillCatalog>(
                CommanderSkillWorkshopWriter.CatalogPath);
            var registered = asset != null && catalog != null && catalog.Skills.Contains(asset);
            CommanderSkillGrowthRule rule = null;
            if (registered)
            {
                catalog.BalanceConfig.TryGetRule(asset.SkillId, out rule);
            }
            draft.LoadGrowth(rule, registered);
            draft.LoadSummon(catalog?.SummonConfig, registered);
        }

        private void AddBoundField(
            VisualElement parent,
            string propertyPath,
            string label,
            bool rebuildOnChange = false)
        {
            AddBoundField(parent, draftSerialized.FindProperty(propertyPath), label, rebuildOnChange);
        }

        private void AddBoundField(
            VisualElement parent,
            SerializedProperty property,
            string label,
            bool rebuildOnChange = false)
        {
            if (property == null) return;
            var path = property.propertyPath;
            var bindingVersion = assemblerBindingVersion;
            var lastValue = SerializedValueSignature(property);
            var field = new PropertyField(property.Copy(), label);
            field.AddToClassList("editor-field");
            field.Bind(draftSerialized);
            field.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                if (suppressCallbacks || bindingVersion != assemblerBindingVersion) return;
                draftSerialized.ApplyModifiedProperties();
                var liveProperty = draftSerialized.FindProperty(path);
                var nextValue = SerializedValueSignature(liveProperty);
                if (string.Equals(lastValue, nextValue, StringComparison.Ordinal))
                {
                    return;
                }
                lastValue = nextValue;
                if (path == "registerInCatalog")
                {
                    draft.NormalizeCatalogOptions();
                    draftSerialized.Update();
                }
                MarkDirty();
                if (rebuildOnChange) QueueRebuild();
            });
            parent.Add(field);
        }

        private static string SerializedValueSignature(SerializedProperty property)
        {
            if (property == null)
            {
                return string.Empty;
            }

            return property.propertyType switch
            {
                SerializedPropertyType.Generic => property.contentHash.ToString(),
                SerializedPropertyType.Integer => $"i:{property.longValue}",
                SerializedPropertyType.Boolean => $"b:{property.boolValue}",
                SerializedPropertyType.Float => $"f:{property.doubleValue:R}",
                SerializedPropertyType.String => "s:" + property.stringValue,
                SerializedPropertyType.Enum => $"e:{property.enumValueIndex}",
                SerializedPropertyType.ObjectReference =>
                    $"o:{property.objectReferenceInstanceIDValue}",
                SerializedPropertyType.Vector2 => "v2:" + property.vector2Value,
                SerializedPropertyType.Vector3 => "v3:" + property.vector3Value,
                SerializedPropertyType.Vector4 => "v4:" + property.vector4Value,
                _ => property.propertyPath + ":" + property.boxedValue
            };
        }

        private void AddEnumPopup<TEnum>(
            VisualElement parent,
            string propertyPath,
            string label,
            IReadOnlyList<TEnum> values,
            IReadOnlyList<string> labels,
            bool rebuildOnChange = false)
            where TEnum : struct, Enum
        {
            AddEnumPopup(
                parent,
                draftSerialized.FindProperty(propertyPath),
                label,
                values,
                labels,
                rebuildOnChange);
        }

        private void AddEnumPopup<TEnum>(
            VisualElement parent,
            SerializedProperty property,
            string label,
            IReadOnlyList<TEnum> values,
            IReadOnlyList<string> labels,
            bool rebuildOnChange = false)
            where TEnum : struct, Enum
        {
            if (property == null || values == null || values.Count == 0 || labels == null ||
                labels.Count != values.Count)
            {
                return;
            }
            var selectedIndex = 0;
            for (var index = 0; index < values.Count; index++)
            {
                if (Convert.ToInt32(values[index]) == property.intValue)
                {
                    selectedIndex = index;
                    break;
                }
            }
            var choices = labels.ToList();
            var popup = new PopupField<string>(label, choices, selectedIndex);
            popup.AddToClassList("editor-field");
            var path = property.propertyPath;
            var bindingVersion = assemblerBindingVersion;
            popup.RegisterValueChangedCallback(evt =>
            {
                if (suppressCallbacks || bindingVersion != assemblerBindingVersion) return;
                var nextIndex = choices.IndexOf(evt.newValue);
                if (nextIndex < 0) return;
                draftSerialized.Update();
                var live = draftSerialized.FindProperty(path);
                if (live == null) return;
                live.intValue = Convert.ToInt32(values[nextIndex]);
                draftSerialized.ApplyModifiedPropertiesWithoutUndo();
                MarkDirty();
                if (rebuildOnChange) QueueRebuild();
            });
            parent.Add(popup);
        }

        private void MarkDirty()
        {
            dirty = true;
            hasUnsavedChanges = true;
            statusMessage = string.Empty;
            statusMessageIsError = false;
            PersistSession();
            RefreshState();
        }

        private void QueueRebuild()
        {
            if (rebuildQueued) return;
            rebuildQueued = true;
            EditorApplication.delayCall += RebuildWhenReady;
        }

        private void RebuildWhenReady()
        {
            EditorApplication.delayCall -= RebuildWhenReady;
            rebuildQueued = false;
            if (this == null || assemblerScroll == null || draft == null) return;
            RebuildAssembler();
        }

        private void EnsureDraft()
        {
            if (draft != null)
            {
                draft.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                draftSerialized ??= new SerializedObject(draft);
                return;
            }
            draft = CreateInstance<CommanderSkillWorkshopDraft>();
            draft.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            draft.ResetDraft(CommanderSkillCategory.Attack);
            draftSerialized = new SerializedObject(draft);
        }

        private void RestoreSession()
        {
            if (sessionRestored) return;
            sessionRestored = true;
            var json = SessionState.GetString(SessionPrefix + "draft", string.Empty);
            if (!string.IsNullOrWhiteSpace(json))
            {
                EditorJsonUtility.FromJsonOverwrite(json, draft);
            }
            draft.NormalizeCatalogOptions();
            var loadedPath = SessionState.GetString(SessionPrefix + "asset", string.Empty);
            loaded = AssetDatabase.LoadAssetAtPath<CommanderSkillDefinition>(loadedPath);
            dirty = SessionState.GetBool(SessionPrefix + "dirty", false);
            showHelp = SessionState.GetBool(SessionPrefix + "help", true);
            draftSerialized.Update();
            hasUnsavedChanges = dirty;
        }

        private void PersistSession()
        {
            if (draft == null) return;
            SessionState.SetString(SessionPrefix + "draft", EditorJsonUtility.ToJson(draft));
            SessionState.SetString(SessionPrefix + "asset", AssetDatabase.GetAssetPath(loaded));
            SessionState.SetBool(SessionPrefix + "dirty", dirty);
            SessionState.SetBool(SessionPrefix + "help", showHelp);
        }

        private void ApplyHelpVisibility()
        {
            rootVisualElement?.EnableInClassList("workshop-root--context-help-hidden", !showHelp);
            PersistSession();
        }

        public override void SaveChanges()
        {
            if (SaveCurrent(loaded == null))
            {
                base.SaveChanges();
            }
        }

        public override void DiscardChanges()
        {
            assemblerBindingVersion++;
            if (loaded != null)
            {
                draft.Load(loaded);
                LoadRegistrationSettings(loaded);
            }
            else
            {
                draft.ResetDraft(draft.Category);
            }
            draftSerialized.Update();
            dirty = false;
            hasUnsavedChanges = false;
            statusMessage = "편집 중인 변경을 버렸습니다.";
            statusMessageIsError = false;
            PersistSession();
            QueueRebuild();
            base.DiscardChanges();
        }

        private void OnDisable()
        {
            PersistSession();
            EditorApplication.delayCall -= RebuildWhenReady;
            EditorApplication.update -= HandlePreviewUpdate;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            rebuildQueued = false;
            preview?.Dispose();
            preview = null;
        }

        private void OnDestroy()
        {
            EditorApplication.delayCall -= RebuildWhenReady;
            EditorApplication.update -= HandlePreviewUpdate;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            preview?.Dispose();
            preview = null;
            if (draft != null)
            {
                DestroyImmediate(draft);
                draft = null;
            }
        }

        private void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                preview?.Dispose();
                preview = null;
                previewCanvas?.MarkDirtyRepaint();
                return;
            }

            if (state != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            EnsurePreviewForEditorState();
            preview?.SetSource(draft);
            RefreshState();
        }

        private void EnsurePreviewForEditorState()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            preview ??= new CommanderSkillWorkshopPreview();
        }

        private void HandlePreviewUpdate()
        {
            if (preview?.IsPlaying != true)
            {
                return;
            }
            if (previewSummary != null)
            {
                previewSummary.text = $"{preview.PhaseLabel}\n{BuildPreviewSummary()}";
            }
            previewCanvas?.MarkDirtyRepaint();
            Repaint();
        }

        private static VisualElement Section(string title, string help = null)
        {
            var card = new VisualElement();
            card.AddToClassList("section-card");
            var heading = new Label(title);
            heading.AddToClassList("section-title");
            card.Add(heading);
            if (!string.IsNullOrWhiteSpace(help)) card.Add(Help(help));
            return card;
        }

        private static Label Help(string text)
        {
            var label = new Label(text);
            label.AddToClassList("help-text");
            return label;
        }

        private static VisualElement CardHeader(string title, params Button[] actions)
        {
            var row = new VisualElement();
            row.AddToClassList("sub-card-header");
            var label = new Label(title);
            label.AddToClassList("sub-card-title");
            row.Add(label);
            for (var index = 0; index < actions.Length; index++) row.Add(actions[index]);
            return row;
        }

        private static Button SmallButton(string text, Action action, bool enabled, bool danger = false)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("mini-action");
            if (danger) button.AddToClassList("danger-action");
            button.SetEnabled(enabled);
            return button;
        }

        private static Button NewButton(string text, Action action, bool isLast = false)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("add-button");
            button.AddToClassList("commander-new-button");
            if (isLast) button.AddToClassList("commander-new-button--last");
            return button;
        }

        private static string CreateUniqueEffectId(SerializedProperty effects, string preferred)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < effects.arraySize; index++)
            {
                ids.Add(effects.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("effectId").stringValue ?? string.Empty);
            }
            if (!ids.Contains(preferred)) return preferred;
            for (var suffix = 2; suffix < 1000; suffix++)
            {
                var candidate = $"{preferred}_{suffix:00}";
                if (!ids.Contains(candidate)) return candidate;
            }
            return preferred + "_copy";
        }

        private static string CreateUniqueSkillId(string current)
        {
            var basis = string.IsNullOrWhiteSpace(current)
                ? "commander_skill_copy"
                : current.Trim() + "_copy";
            var ids = new HashSet<string>(
                FindDefinitions().Select(definition => definition.SkillId),
                StringComparer.OrdinalIgnoreCase);
            if (!ids.Contains(basis))
            {
                return basis;
            }

            for (var suffix = 2; suffix < 1000; suffix++)
            {
                var candidate = $"{basis}_{suffix:00}";
                if (!ids.Contains(candidate))
                {
                    return candidate;
                }
            }
            return basis + "_new";
        }

        private static (CommanderSkillUnitEffectType[] Values, string[] Labels) EffectTypeOptions(
            CommanderSkillCategory category)
        {
            if (category == CommanderSkillCategory.Attack)
            {
                return (
                    (CommanderSkillUnitEffectType[])Enum.GetValues(typeof(CommanderSkillUnitEffectType)),
                    new[] { "회복", "보호막", "공격력 증가", "방어력 증가", "공격속도 증가", "받는 피해 감소",
                        "피해 반사", "약화 정화", "기력 회복", "공격력 감소", "방어력 감소", "공격속도 감소",
                        "이동속도 감소", "감속", "기절", "기존 노출 표식", "기력 감소" });
            }
            if (category == CommanderSkillCategory.Debuff)
            {
                return (
                    new[]
                    {
                        CommanderSkillUnitEffectType.AttackDebuff,
                        CommanderSkillUnitEffectType.DefenseDebuff,
                        CommanderSkillUnitEffectType.AttackSpeedDebuff,
                        CommanderSkillUnitEffectType.MoveSpeedDebuff,
                        CommanderSkillUnitEffectType.Slow,
                        CommanderSkillUnitEffectType.Stun,
                        CommanderSkillUnitEffectType.Mark,
                        CommanderSkillUnitEffectType.EnergyDrain
                    },
                    new[] { "공격력 감소", "방어력 감소", "공격속도 감소", "이동속도 감소", "감속", "기절", "표식", "기력 감소" });
            }
            return (
                new[]
                {
                    CommanderSkillUnitEffectType.Heal,
                    CommanderSkillUnitEffectType.Shield,
                    CommanderSkillUnitEffectType.AttackBuff,
                    CommanderSkillUnitEffectType.DefenseBuff,
                    CommanderSkillUnitEffectType.AttackSpeedBuff,
                    CommanderSkillUnitEffectType.DamageReduction,
                    CommanderSkillUnitEffectType.DamageReflect,
                    CommanderSkillUnitEffectType.Cleanse,
                    CommanderSkillUnitEffectType.EnergyGain
                },
                new[] { "회복", "보호막", "공격력 증가", "방어력 증가", "공격속도 증가", "받는 피해 감소", "피해 반사", "약화 정화", "기력 회복" });
        }

        private static (CommanderSkillEffectValueSource[] Values, string[] Labels) EffectValueSourceOptions(
            CommanderSkillUnitEffectType type)
        {
            if (type == CommanderSkillUnitEffectType.Heal)
            {
                return (
                    new[]
                    {
                        CommanderSkillEffectValueSource.Flat,
                        CommanderSkillEffectValueSource.TargetMaxHealthRatio,
                        CommanderSkillEffectValueSource.TargetMissingHealthRatio
                    },
                    new[] { "고정값", "대상 최대 체력 비율", "대상 잃은 체력 비율" });
            }
            if (type == CommanderSkillUnitEffectType.Shield)
            {
                return (
                    new[]
                    {
                        CommanderSkillEffectValueSource.Flat,
                        CommanderSkillEffectValueSource.TargetMaxHealthRatio
                    },
                    new[] { "고정값", "대상 최대 체력 비율" });
            }
            if (type is CommanderSkillUnitEffectType.EnergyGain or CommanderSkillUnitEffectType.EnergyDrain)
            {
                return (
                    new[]
                    {
                        CommanderSkillEffectValueSource.Flat,
                        CommanderSkillEffectValueSource.TargetEnergyCapacityRatio
                    },
                    new[] { "고정값", "대상 기력 용량 비율" });
            }
            return (
                new[] { CommanderSkillEffectValueSource.Flat },
                new[] { "효과 비율" });
        }

        private static string EffectMagnitudeLabel(
            CommanderSkillUnitEffectType type,
            CommanderSkillEffectValueSource source)
        {
            return CommanderUnitEffectDefinition.UsesRatioMagnitude(type, source)
                ? "효과 비율 (0~1, 0.2 = 20%)"
                : "효과 고정값";
        }

        private static string FormatEffectSummary(CommanderSkillWorkshopEffectDraft effect)
        {
            if (effect == null)
            {
                return "빈 효과";
            }

            if (effect.Kind == CommanderSkillWorkshopEffectKind.CommanderMark)
                return $"Mark {effect.MarkId} / {effect.MarkTrigger} / {effect.Duration:0.##}초";
            if (effect.Kind == CommanderSkillWorkshopEffectKind.RecordedHitDamage)
                return $"기록 피해 {effect.RecordedBaseMultiplier:0.##} + Hit×{effect.RecordedMultiplierPerHit:0.##} (최대 {effect.MaximumRecordedHits})";
            if (effect.Kind == CommanderSkillWorkshopEffectKind.GlobalModifier)
                return $"전역 Modifier {effect.Duration:0.##}초 / Hit {effect.MarkRequiredHitsMultiplier:0.##} / 피해 {effect.MarkTriggerDamageMultiplier:0.##} / 쿨 {effect.CooldownRecoveryMultiplier:0.##}";
            var magnitude = CommanderUnitEffectDefinition.UsesRatioMagnitude(
                effect.EffectType,
                effect.ValueSource)
                ? $"{effect.Magnitude * 100f:0.##}%"
                : $"{effect.Magnitude:0.##}";
            var duration = CommanderUnitEffectDefinition.RequiresDuration(effect.EffectType)
                ? $" / {effect.Duration:0.##}초"
                : string.Empty;
            var scope = effect.Scope == CommanderSkillEffectScope.Area
                ? $" / 반경 {effect.Radius:0.##}m"
                : " / 단일";
            return $"{EffectTypeLabel(effect.EffectType)} {magnitude}{duration}{scope}";
        }

        private static string CategoryLabel(CommanderSkillCategory category)
        {
            return category switch
            {
                CommanderSkillCategory.Buff => "버프형",
                CommanderSkillCategory.Debuff => "디버프형",
                _ => "공격형"
            };
        }

        private static string TargetTeamLabel(CommanderSkillTargetTeam team)
        {
            return team == CommanderSkillTargetTeam.Ally ? "아군" : "적";
        }

        private static string ShapeLabel(MonsterBasicAttackShape shape)
        {
            return shape switch
            {
                MonsterBasicAttackShape.Fan => "부채꼴",
                MonsterBasicAttackShape.Line => "직선",
                MonsterBasicAttackShape.Circle => "원형",
                _ => "단일"
            };
        }

        private static string EffectTypeLabel(CommanderSkillUnitEffectType type)
        {
            return type switch
            {
                CommanderSkillUnitEffectType.Heal => "회복",
                CommanderSkillUnitEffectType.Shield => "보호막",
                CommanderSkillUnitEffectType.AttackBuff => "공격 증가",
                CommanderSkillUnitEffectType.DefenseBuff => "방어 증가",
                CommanderSkillUnitEffectType.AttackSpeedBuff => "공속 증가",
                CommanderSkillUnitEffectType.DamageReduction => "피해 감소",
                CommanderSkillUnitEffectType.DamageReflect => "피해 반사",
                CommanderSkillUnitEffectType.Cleanse => "정화",
                CommanderSkillUnitEffectType.EnergyGain => "기력 회복",
                CommanderSkillUnitEffectType.AttackDebuff => "공격 감소",
                CommanderSkillUnitEffectType.DefenseDebuff => "방어 감소",
                CommanderSkillUnitEffectType.AttackSpeedDebuff => "공속 감소",
                CommanderSkillUnitEffectType.MoveSpeedDebuff => "이속 감소",
                CommanderSkillUnitEffectType.Slow => "감속",
                CommanderSkillUnitEffectType.Stun => "기절",
                CommanderSkillUnitEffectType.Mark => "표식",
                _ => "기력 감소"
            };
        }

    }
}
