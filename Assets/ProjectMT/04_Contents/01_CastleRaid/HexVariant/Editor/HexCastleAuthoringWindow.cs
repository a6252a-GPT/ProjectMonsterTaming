using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectMT.Contents.CastleRaidHex.Editor
{
    public sealed class HexCastleAuthoringWindow : EditorWindow
    {
        public const string MenuPath = "JC Tool/군단의 역습 육각/성 생성기";

        private const int DefaultSeed = 10801;
        private const int DefaultDefenseLayers = 3;
        private const float PalaceVisualScale = 2f;
        private const float BuildingVisualScale = 1.2f;
        private const float GoldEquipmentVisualScale = 1.5f;
        private static readonly System.Random RandomSeedGenerator = new System.Random();

        [SerializeField] private int seed = DefaultSeed;
        [SerializeField, Range(2, 4)] private int defenseLayerCount = DefaultDefenseLayers;
        [SerializeField] private HexCastleTheme theme = HexCastleTheme.CentralCompartment;
        [SerializeField] private bool autoApplyScenePreview = true;
        [SerializeField] private bool gridVisible = true;
        [SerializeField] private string selectedStageId = "HEX_T1_3W_10801";
        [SerializeField] private HexCastleThemeOneRules rulesAsset;
        [SerializeField] private bool showAdvancedRules;
        [SerializeField] private bool showPlacementAndHealthRules;
        [SerializeField] private bool showBuildingRules;
        [SerializeField] private bool showBarracksRules;
        [SerializeField] private bool showTurretRules;
        [SerializeField] private bool showGateRules;
        [SerializeField] private bool showDetailedReport;

        private readonly List<HexCastleCandidate> candidates = new List<HexCastleCandidate>();
        private Vector2 scroll;
        private int selectedIndex;
        private Texture2D inlinePreview;
        private HexCastleCandidate previewCandidate;
        private string lastMessage = "실루엣 테마를 고르고 육각 성 후보를 생성하세요.";

        private HexCastleThemeOneTuning ActiveTuning => rulesAsset != null
            ? rulesAsset.Tuning
            : HexCastleThemeOneTuning.CreateDraftDefaults();

        [MenuItem(MenuPath)]
        public static void OpenWindow()
        {
            var window = GetWindow<HexCastleAuthoringWindow>("육각 성 생성기");
            window.minSize = new Vector2(660f, 780f);
            window.Show();
            window.Focus();
        }

        private void OnDisable()
        {
            DestroyInlinePreview();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("육각 성 생성기");
            rulesAsset = rulesAsset != null
                ? rulesAsset
                : HexCastleThemeOneRulesAssetUtility.LoadOrCreate();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("군단의 역습 · 육각 성 생성기", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "기존 사각 도구의 후보 생성 → 검수 → 2D/3D 확인 흐름을 유지합니다. " +
                "스테이지 배치 승인은 외형·수치 정식화 뒤에만 열립니다. " +
                "달라지는 것은 꼭짓점이 위를 향하는 육각 좌표와 칸 단위 판정뿐입니다.",
                MessageType.Info);

            DrawRules();
            DrawGeneration();
            DrawCandidates();
            DrawReportAndPreview();
            DrawApproval();
            EditorGUILayout.HelpBox(lastMessage, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void DrawRules()
        {
            EditorGUILayout.LabelField("1. 생성 규칙", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var wallRadii = HexCastleFoundationGenerator.ResolveCanonicalWallRadii(defenseLayerCount);
                var boardRadius = wallRadii[wallRadii.Count - 1] + HexSpatialContract.MinimumDeploymentRings;
                rulesAsset = (HexCastleThemeOneRules)EditorGUILayout.ObjectField(
                    "테마 1 생성 규칙 자산",
                    rulesAsset,
                    typeof(HexCastleThemeOneRules),
                    false);
                if (rulesAsset == null && GUILayout.Button("기본 초안 규칙 생성·연결"))
                {
                    rulesAsset = HexCastleThemeOneRulesAssetUtility.LoadOrCreate();
                }

                DrawFixedContract(wallRadii, boardRadius);
                if (rulesAsset == null)
                {
                    EditorGUILayout.HelpBox(
                        "생성 규칙 자산이 연결되지 않아 성을 생성할 수 없습니다.",
                        MessageType.Error);
                    return;
                }

                DrawEditableDraftRules();
                DrawReadinessNotice();
            }
        }

        private static void DrawFixedContract(IReadOnlyList<int> wallRadii, int boardRadius)
        {
            EditorGUILayout.HelpBox(
                $"고정: 왕궁 7칸·외형 {PalaceVisualScale:0.0}배 / 성벽 {string.Join("·", wallRadii.Select(value => $"R{value}"))} / " +
                $"전장 R{boardRadius} / 각 칸이 체력·길막 소유 / 모델은 외형 전용",
                MessageType.None);
        }

        private void DrawEditableDraftRules()
        {
            var serializedRules = new SerializedObject(rulesAsset);
            serializedRules.Update();
            var tuning = serializedRules.FindProperty("tuning");
            if (tuning == null)
            {
                EditorGUILayout.HelpBox("생성 규칙 자산의 세부 데이터를 찾지 못했습니다.", MessageType.Error);
                return;
            }

            try
            {
                var active = ActiveTuning;
                var quota = active.ResolveLayerQuota(defenseLayerCount);
                EditorGUILayout.HelpBox(
                    $"현재 적용: 중벽 바로 바깥 1열은 필수 통로 외 {active.DenseOccupancy:P0}·" +
                    $"그 다음 2열은 {active.SparseOccupancy:P0} / " +
                    $"포탑 {quota.TurretCount}+왕궁 경비 {active.PalaceGuardTurretCount}·" +
                    $"기사 병영 {quota.KnightBarracksCount}+왕궁 경비 {active.PalaceGuardBarracksCount} / " +
                    $"닫힌 성문은 성벽당 {active.ClosedGateCountPerWallRing}개 / " +
                    $"격벽 열린 성문 {active.OpenPartitionGateCountPerBand}개 보장+{active.OpenPartitionAdditionalGateChance:P0} 확률 추가",
                    MessageType.Info);
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox($"생성 규칙 요약 실패: {exception.Message}", MessageType.Error);
            }

            showAdvancedRules = EditorGUILayout.Foldout(
                showAdvancedRules,
                "세부 생성 규칙 직접 편집",
                true);
            if (!showAdvancedRules)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "아래 값은 성 생성기가 직접 읽는 실제 저장값입니다. 변경하면 기존 후보와 3D 미리보기가 제거됩니다.",
                MessageType.Warning);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("초안 형식 버전", ActiveTuning.DraftVersion);
                EditorGUILayout.EnumPopup("정식화 상태", rulesAsset.Readiness);
            }

            EditorGUI.BeginChangeCheck();
            showPlacementAndHealthRules = EditorGUILayout.Foldout(
                showPlacementAndHealthRules,
                "배치 밀도 · 칸 체력 · 보상",
                true);
            if (showPlacementAndHealthRules)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        DrawRuleProperty(tuning, "denseOccupancy", "중벽 바로 바깥 1열 (필수 통로 외 최대 채움)");
                    }
                    DrawRuleProperty(tuning, "sparseOccupancy", "그 다음 2열 점유율");
                    DrawRuleProperty(tuning, "palaceHealth", "왕궁 칸 체력");
                    DrawRuleProperty(tuning, "palaceRewardValue", "왕궁 보상값");
                    DrawRuleProperty(tuning, "wallTier1Health", "성벽 1단계 체력");
                    DrawRuleProperty(tuning, "wallTier2Health", "성벽 2단계 체력");
                    DrawRuleProperty(tuning, "wallTier3Health", "성벽 3단계 체력");
                    DrawRuleProperty(tuning, "rewardBuildingHealth", "보상 건물 체력");
                    DrawRuleProperty(tuning, "specialBuildingHealth", "특수 건물 체력");
                    DrawRuleProperty(tuning, "defenseBuildingHealth", "포탑 건물 체력");
                    DrawRuleProperty(tuning, "goldRewardValue", "골드 건물 보상");
                    DrawRuleProperty(tuning, "equipmentRewardValue", "장비 건물 보상");
                    DrawRuleProperty(tuning, "keyRewardValue", "열쇠 건물 보상");
                }
            }

            showBuildingRules = EditorGUILayout.Foldout(
                showBuildingRules,
                "2·3·4중벽 건물 수 · 등급 · 일반 길막 후보",
                true);
            if (showBuildingRules)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawLayerQuotaRules(tuning);
                    DrawFixedBuildingGradeRules(tuning);
                    DrawBlockerVariantRules(tuning);
                }
            }

            showBarracksRules = EditorGUILayout.Foldout(showBarracksRules, "병영 · 소환 · 건물 효과", true);
            if (showBarracksRules)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawRuleProperty(tuning, "minimumBarracksDefenseLayer", "병영 최소 배치 방어선");
                    DrawRuleProperty(tuning, "minimumBarracksOpenNeighbors", "병영 인접 빈 칸 최소");
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.IntField("왕궁 경비 기사병영 수", ActiveTuning.PalaceGuardBarracksCount);
                        EditorGUILayout.IntField("왕궁 경비 포탑 수", ActiveTuning.PalaceGuardTurretCount);
                    }
                    DrawRuleProperty(tuning, "knightRefillInterval", "기사 리필 간격");
                    DrawRuleProperty(tuning, "knightSearchRadius", "기사 검색 반경 칸 수");
                    DrawRuleProperty(tuning, "knightRefillThreshold", "기사 리필 기준 수");
                    DrawRuleProperty(tuning, "knightsPerRefill", "1회 기사 리필 수");
                    DrawRuleProperty(tuning, "farmerSpawnInterval", "농부 소환 간격");
                    DrawRuleProperty(tuning, "farmersPerSpawn", "1회 농부 소환 수");
                    DrawRuleProperty(tuning, "trainingAttackMultiplier", "연습장 공격력 배율");
                    DrawRuleProperty(tuning, "churchRageMoveSpeedMultiplier", "교회 파괴 이동속도 배율");
                }
            }

            showTurretRules = EditorGUILayout.Foldout(showTurretRules, "포탑 배치 · 무기 · 레벨 · 사거리", true);
            if (showTurretRules)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawRuleProperty(tuning, "innerBandTurretShare", "첫 성벽 바깥 포탑 우선 비율");
                    DrawRuleProperty(tuning, "turretMinimumRangeCells", "포탑 최소 사거리 칸 수");
                    DrawRuleProperty(tuning, "turretMaximumRangeCells", "포탑 최대 사거리 칸 수");
                    DrawRuleProperty(tuning, "turretsCanAttackAcrossWalls", "성벽 넘어 공격 허용");
                    DrawRuleProperty(tuning, "cannonMaximumLevel", "대포 최대 레벨");
                    DrawRuleProperty(tuning, "ballistaMaximumLevel", "발리스타 최대 레벨");
                    DrawRuleProperty(tuning, "fireballMaximumLevel", "화염구 최대 레벨");
                    DrawTurretWeaponCycleRules(tuning);
                    DrawTurretBandLevelRules(tuning);
                }
            }

            showGateRules = EditorGUILayout.Foldout(showGateRules, "닫힌 성문 · 격벽 열린 성문", true);
            if (showGateRules)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawRuleProperty(tuning, "closedGateCountPerWallRing", "성벽 둘레당 닫힌 성문 수");
                    DrawRuleProperty(tuning, "closedGateMaximumPerFace", "한 면의 닫힌 성문 최대");
                    DrawRuleProperty(tuning, "closedGateHealthMultiplier", "닫힌 성문 체력 배율");
                    DrawRuleProperty(tuning, "openPartitionGateCountPerBand", "격벽 구간당 열린 성문 보장 수");
                    DrawRuleProperty(tuning, "openPartitionAdditionalGateChance", "열린 성문 1개 추가 확률");
                    DrawRuleProperty(tuning, "openPartitionGateMaximumPerBand", "격벽 구간당 열린 성문 최대");
                }

                EditorGUILayout.HelpBox(
                    "열린 성문은 격벽 바깥쪽 후보를 우선하고 수비대만 통과합니다. " +
                    "통로 앞뒤 칸은 빈 바닥·비차단 상태로 예약됩니다.",
                    MessageType.None);
            }

            var changed = EditorGUI.EndChangeCheck() && serializedRules.ApplyModifiedProperties();
            if (changed)
            {
                EditorUtility.SetDirty(rulesAsset);
                InvalidateRuleDependentPreviews();
            }

            DrawRuleValidation();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("생성 규칙 자산 선택"))
                {
                    Selection.activeObject = rulesAsset;
                    EditorGUIUtility.PingObject(rulesAsset);
                }

                if (GUILayout.Button("초안 생성 규칙 저장"))
                {
                    AssetDatabase.SaveAssetIfDirty(rulesAsset);
                    lastMessage = "테마 1 초안 생성 규칙을 자산에 저장했습니다.";
                }
            }

            if (GUILayout.Button("양면 성벽·성문 외형 프리팹 다시 만들기"))
            {
                RunSafely(() =>
                {
                    HexCastleDerivedWallPrefabSetupUtility.RebuildAll();
                    lastMessage = "ProjectMT 파생 양면 성벽·성문 외형 프리팹을 배율 1 기준으로 다시 만들었습니다.";
                });
            }
        }

        private static void DrawLayerQuotaRules(SerializedProperty tuning)
        {
            var rules = tuning.FindPropertyRelative("layerQuotas");
            if (!TryDrawRuleListHeader(rules, "방어선별 생성 수량"))
            {
                return;
            }

            for (var index = 0; index < rules.arraySize; index++)
            {
                var rule = rules.GetArrayElementAtIndex(index);
                var layerCount = rule.FindPropertyRelative("defenseLayerCount");
                var label = layerCount != null ? $"{layerCount.intValue}중벽 생성 수량" : $"생성 수량 {index + 1}";
                rule.isExpanded = EditorGUILayout.Foldout(rule.isExpanded, label, true);
                if (!rule.isExpanded)
                {
                    continue;
                }

                using (new EditorGUI.IndentLevelScope())
                {
                    DrawRuleProperty(rule, "defenseLayerCount", "방어선 수");
                    DrawRuleProperty(rule, "knightBarracksCount", "기사 병영 수");
                    DrawRuleProperty(rule, "farmerBarracksCount", "농부 병영 수");
                    DrawRuleProperty(rule, "turretCount", "포탑 수");
                    DrawRuleProperty(rule, "trainingYardCount", "연습장 수");
                    DrawRuleProperty(rule, "churchCount", "교회 수");
                    DrawRuleProperty(rule, "minimumBlockerGradeSum", "일반 길막 최소 등급합");
                    DrawRuleProperty(rule, "futureTrapCount", "후속 함정 수 · 현재 생성 안 함");
                    DrawRuleProperty(rule, "futureInitialDefenderCount", "후속 초기 수비대 수 · 현재 생성 안 함");
                }
            }
        }

        private static void DrawFixedBuildingGradeRules(SerializedProperty tuning)
        {
            var rules = tuning.FindPropertyRelative("fixedBuildingGrades");
            if (!TryDrawRuleListHeader(rules, "특수·보상 건물 등급"))
            {
                return;
            }

            for (var index = 0; index < rules.arraySize; index++)
            {
                var rule = rules.GetArrayElementAtIndex(index);
                var role = rule.FindPropertyRelative("role");
                var roleLabel = role != null
                    ? ResolveBuildingRoleLabel((HexCastleBuildingRole)role.intValue)
                    : $"건물 {index + 1}";
                rule.isExpanded = EditorGUILayout.Foldout(rule.isExpanded, $"{roleLabel} 등급", true);
                if (!rule.isExpanded)
                {
                    continue;
                }

                using (new EditorGUI.IndentLevelScope())
                {
                    DrawRuleProperty(rule, "role", "건물 종류");
                    DrawRuleProperty(rule, "grade", "등급");
                }
            }
        }

        private static void DrawBlockerVariantRules(SerializedProperty tuning)
        {
            var rules = tuning.FindPropertyRelative("blockerVariants");
            if (!TryDrawRuleListHeader(rules, "일반 길막 건물 후보"))
            {
                return;
            }

            for (var index = 0; index < rules.arraySize; index++)
            {
                var rule = rules.GetArrayElementAtIndex(index);
                var ruleId = rule.FindPropertyRelative("id");
                var label = ruleId != null && !string.IsNullOrWhiteSpace(ruleId.stringValue)
                    ? $"후보 {index + 1} · {ruleId.stringValue}"
                    : $"후보 {index + 1}";
                rule.isExpanded = EditorGUILayout.Foldout(rule.isExpanded, label, true);
                if (!rule.isExpanded)
                {
                    continue;
                }

                using (new EditorGUI.IndentLevelScope())
                {
                    DrawRuleProperty(rule, "id", "규칙 식별자");
                    DrawRuleProperty(rule, "visualVariantId", "외형 프리팹 식별자");
                    DrawRuleProperty(rule, "grade", "등급");
                    DrawRuleProperty(rule, "health", "체력");
                }
            }
        }

        private static void DrawTurretWeaponCycleRules(SerializedProperty tuning)
        {
            var rules = tuning.FindPropertyRelative("turretWeaponCycle");
            if (!TryDrawRuleListHeader(rules, "포탑 무기 순환"))
            {
                return;
            }

            for (var index = 0; index < rules.arraySize; index++)
            {
                EditorGUILayout.PropertyField(
                    rules.GetArrayElementAtIndex(index),
                    new GUIContent($"순서 {index + 1}"));
            }
        }

        private static void DrawTurretBandLevelRules(SerializedProperty tuning)
        {
            var rules = tuning.FindPropertyRelative("turretBandLevels");
            if (!TryDrawRuleListHeader(rules, "방어선별 포탑 구간 레벨"))
            {
                return;
            }

            for (var index = 0; index < rules.arraySize; index++)
            {
                var rule = rules.GetArrayElementAtIndex(index);
                var layerCount = rule.FindPropertyRelative("defenseLayerCount");
                var label = layerCount != null
                    ? $"{layerCount.intValue}중벽 포탑 구간 레벨"
                    : $"포탑 구간 레벨 {index + 1}";
                rule.isExpanded = EditorGUILayout.Foldout(rule.isExpanded, label, true);
                if (!rule.isExpanded)
                {
                    continue;
                }

                using (new EditorGUI.IndentLevelScope())
                {
                    DrawRuleProperty(rule, "defenseLayerCount", "방어선 수");
                    DrawRuleProperty(rule, "firstBandLevel", "첫 번째 구간 레벨");
                    DrawRuleProperty(rule, "secondBandLevel", "두 번째 구간 레벨");
                    DrawRuleProperty(rule, "thirdBandLevel", "세 번째 구간 레벨");
                }
            }
        }

        private static bool TryDrawRuleListHeader(SerializedProperty rules, string label)
        {
            if (rules == null || !rules.isArray)
            {
                EditorGUILayout.HelpBox($"{label} 목록을 찾지 못했습니다.", MessageType.Error);
                return false;
            }

            EditorGUILayout.LabelField($"{label} · {rules.arraySize}개", EditorStyles.miniBoldLabel);
            return true;
        }

        private static string ResolveBuildingRoleLabel(HexCastleBuildingRole role)
        {
            switch (role)
            {
                case HexCastleBuildingRole.KnightBarracks: return "기사 병영";
                case HexCastleBuildingRole.FarmerBarracks: return "농부 병영";
                case HexCastleBuildingRole.Turret: return "포탑";
                case HexCastleBuildingRole.TrainingYard: return "연습장";
                case HexCastleBuildingRole.Church: return "교회";
                case HexCastleBuildingRole.GoldStorage: return "골드 건물";
                case HexCastleBuildingRole.EquipmentForge: return "장비 건물";
                case HexCastleBuildingRole.KeyVault: return "열쇠 건물";
                case HexCastleBuildingRole.Blocker: return "일반 길막 건물";
                default: return "없음";
            }
        }

        private static void DrawRuleProperty(
            SerializedProperty tuning,
            string propertyName,
            string label,
            bool includeChildren = false)
        {
            var property = tuning.FindPropertyRelative(propertyName);
            if (property == null)
            {
                EditorGUILayout.HelpBox($"생성 규칙 항목을 찾지 못했습니다: {propertyName}", MessageType.Error);
                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren);
        }

        private void DrawRuleValidation()
        {
            try
            {
                for (var layers = 2; layers <= 4; layers++)
                {
                    ActiveTuning.Validate(layers);
                }

                EditorGUILayout.HelpBox("생성 규칙 검사 통과: 2·3·4중벽 생성 가능", MessageType.Info);
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox($"생성 규칙 오류: {exception.Message}", MessageType.Error);
            }
        }

        private void InvalidateRuleDependentPreviews()
        {
            candidates.Clear();
            selectedIndex = 0;
            DestroyInlinePreview();
            HexCastleFoundationVisualGate.Remove();
            HexCastleGenerationPlayablePreview.Clear(SceneManager.GetActiveScene());
            SceneView.RepaintAll();
            lastMessage = "생성 규칙이 바뀌어 기존 후보와 3D 미리보기를 폐기했습니다. 시드를 다시 생성하세요.";
        }

        private void DrawGeneration()
        {
            EditorGUILayout.LabelField("2. 후보 생성", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var themes = HexCastleThemeCatalog.Themes.ToArray();
                var labels = themes.Select(HexCastleThemeCatalog.ResolveLabel).ToArray();
                var themeIndex = Mathf.Max(0, Array.IndexOf(themes.ToArray(), theme));
                EditorGUI.BeginChangeCheck();
                themeIndex = EditorGUILayout.Popup("성곽 실루엣", themeIndex, labels);
                if (EditorGUI.EndChangeCheck())
                {
                    theme = themes[Mathf.Clamp(themeIndex, 0, themes.Length - 1)];
                    InvalidateRuleDependentPreviews();
                }
                EditorGUILayout.HelpBox(
                    HexCastleThemeCatalog.ResolveDescription(theme) +
                    "\nA~I 모두 같은 건물·성문·포탑·병영·보상·검증 규칙으로 정식 생성됩니다.",
                    MessageType.None);

                defenseLayerCount = EditorGUILayout.IntPopup(
                    "방어선",
                    defenseLayerCount,
                    new[] { "2중벽", "3중벽", "4중벽" },
                    new[] { 2, 3, 4 });
                seed = EditorGUILayout.IntField("시드", seed);
                autoApplyScenePreview = EditorGUILayout.ToggleLeft(
                    "후보 생성 후 KayKit 3D 씬 미리보기 자동 생성",
                    autoApplyScenePreview);
                gridVisible = EditorGUILayout.ToggleLeft("3D 미리보기에 육각 격자 표시", gridVisible);
                EditorGUILayout.HelpBox(
                    "2중 [R3,R5] · 3중 [R3,R5,R8] · 4중 [R3,R5,R8,R11]. " +
                    "성벽 사이의 빈 행은 1 / 2 / 2줄입니다.",
                    MessageType.None);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("현재 시드로 생성", GUILayout.Height(30f)))
                    {
                        GenerateCandidates(1);
                    }

                    if (GUILayout.Button("무작위 시드로 생성", GUILayout.Height(30f)))
                    {
                        GenerateRandomCandidate();
                    }
                }
            }
        }

        private void DrawCandidates()
        {
            EditorGUILayout.LabelField("3. 생성 후보", EditorStyles.boldLabel);
            if (candidates.Count == 0)
            {
                EditorGUILayout.HelpBox("생성된 후보가 없습니다.", MessageType.None);
                return;
            }

            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                var selected = selectedIndex == index;
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    if (GUILayout.Toggle(selected, $"{index + 1:00}", "Button", GUILayout.Width(42f)) && !selected)
                    {
                        selectedIndex = index;
                        RefreshInlinePreview();
                        if (autoApplyScenePreview)
                        {
                            CreateScenePreview();
                        }
                    }

                    EditorGUILayout.LabelField(
                        $"{HexCastleThemeCatalog.ResolveLabel(candidate.Layout.Theme)} · 시드 {candidate.Layout.Seed} · " +
                        $"{(candidate.Validation.IsValid ? "통과" : "실패")} · " +
                        $"돌파 {candidate.Difficulty.MinimumBreachCost:0.0} · 난이도 {candidate.Difficulty.Score:0.0}");
                }
            }
        }

        private void DrawReportAndPreview()
        {
            var selected = Selected;
            if (selected == null)
            {
                return;
            }

            showDetailedReport = EditorGUILayout.Foldout(
                showDetailedReport,
                "4. 상세 검수 보고서",
                true);
            if (showDetailedReport)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var layout = selected.Layout;
                    var difficulty = selected.Difficulty;
                    var palaceCount = layout.Enumerate(HexCastleCellKind.Palace).Count();
                    var wallCount = layout.Enumerate(HexCastleCellKind.Wall).Count();
                    var towerCount = layout.Enumerate(HexCastleCellKind.Tower).Count();
                    var closedGateCount = layout.Enumerate(HexCastleCellKind.Gate)
                        .Count(cell => cell.GateRole == HexCastleGateRole.ClosedWall);
                    var openGateCount = layout.Enumerate(HexCastleCellKind.Gate)
                        .Count(cell => cell.GateRole == HexCastleGateRole.OpenDefenderPassage);
                    var buildingCount = layout.Cells.Values.Count(cell => cell.IsBuildingCell);
                    var denseCount = layout.Cells.Values.Count(cell =>
                        cell.PlacementDensity == HexCastlePlacementDensity.Dense);
                    var sparseCount = layout.Cells.Values.Count(cell =>
                        cell.PlacementDensity == HexCastlePlacementDensity.Sparse);
                    var blockerGradeSum = layout.Cells.Values
                        .Where(cell => cell.BuildingRole == HexCastleBuildingRole.Blocker)
                        .Sum(cell => cell.BuildingGrade);
                    var minimumBlockerGrade = ActiveTuning.ResolveLayerQuota(layout.DefenseLayerCount)
                        .MinimumBlockerGradeSum;
                    var blockedCount = layout.Cells.Values.Count(cell => cell.InitialBlocked);

                    EditorGUILayout.LabelField(
                        $"검수 {(selected.Validation.IsValid ? "통과" : "실패")}  |  규칙 버전 {layout.RulesVersion}  |  " +
                        $"시드 {layout.Seed}  |  {layout.DefenseLayerCount}중벽");
                    EditorGUILayout.LabelField(
                        $"전장 반경 R{layout.BattlefieldRadius} · 전체 칸 {layout.Cells.Count:N0} · 막힌 칸 {blockedCount:N0}");
                    EditorGUILayout.LabelField(
                        $"왕궁 칸 {palaceCount} · 성벽 {wallCount} · 접합 탑 {towerCount} · " +
                        $"성문 닫힘/열림 {closedGateCount}/{openGateCount} · 건물 {buildingCount}");
                    EditorGUILayout.LabelField(
                        $"건물 열 밀집 {denseCount} · 분산 {sparseCount} · 전체 등급합 {difficulty.TotalBuildingGrade}");
                    EditorGUILayout.LabelField(
                        $"일반 길막 등급합 {blockerGradeSum} · 초안 최소값 {minimumBlockerGrade}");
                    EditorGUILayout.LabelField(
                        $"경로상 건물 등급합 최소/평균 {difficulty.MinimumBreachBuildingGrade:0.0} / " +
                        $"{difficulty.AverageBreachBuildingGrade:0.0}");
                    EditorGUILayout.LabelField(
                        "건물 종류 " + string.Join(" · ", layout.Cells.Values
                            .Where(cell => cell.IsBuildingCell)
                            .GroupBy(cell => cell.BuildingRole)
                            .OrderBy(group => group.Key)
                            .Select(group => $"{ResolveBuildingRoleLabel(group.Key)} {group.Count()}")));
                    EditorGUILayout.LabelField(
                        $"성벽 반경 {string.Join(", ", layout.WallRadii.Select(value => $"R{value}"))}");
                    EditorGUILayout.LabelField(
                        $"돌파 최소/평균/최대 {difficulty.MinimumBreachCost:0.0} / " +
                        $"{difficulty.AverageBreachCost:0.0} / {difficulty.MaximumBreachCost:0.0}");
                    EditorGUILayout.LabelField(
                        $"총 파괴 체력 {difficulty.TotalDestructionHealth:0} · 보상 {difficulty.RewardValue} · " +
                        $"난이도 {difficulty.Score:0.0} · 추천 스테이지 {difficulty.SuggestedStage}");
                    EditorGUILayout.LabelField(
                        $"6방향 진입 경로 비용 {string.Join(" / ", selected.Validation.EntryRoutes.Select(route => route.TotalCost.ToString("0")))}");
                    EditorGUILayout.SelectableLabel(
                        $"구조 해시 {layout.StructureSignature}\n배치 해시 {layout.LayoutSignature}",
                        EditorStyles.textArea,
                        GUILayout.Height(38f));
                    if (!selected.Validation.IsValid)
                    {
                        EditorGUILayout.HelpBox(string.Join("\n", selected.Validation.Errors), MessageType.Error);
                    }
                }
            }

            EditorGUILayout.LabelField("5. 육각 배치 미리보기", EditorStyles.boldLabel);
            EnsureInlinePreview();
            if (inlinePreview != null)
            {
                var rect = GUILayoutUtility.GetAspectRect(1f, GUILayout.MaxHeight(430f));
                EditorGUI.DrawPreviewTexture(rect, inlinePreview, null, ScaleMode.ScaleToFit);
            }

            var legend = HexCastleVisualPalette.Legend;
            for (var start = 0; start < legend.Count; start += 4)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    foreach (var entry in legend.Skip(start).Take(4))
                    {
                        var rect = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f));
                        EditorGUI.DrawRect(rect, entry.Color);
                        GUILayout.Label(entry.Label, EditorStyles.miniLabel, GUILayout.MinWidth(90f));
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("KayKit 3D 미리보기 생성", GUILayout.Height(28f)))
                {
                    CreateScenePreview();
                }

                if (GUILayout.Button("3D 미리보기 제거", GUILayout.Height(28f)))
                {
                    RemoveScenePreview();
                }

                if (GUILayout.Button(gridVisible ? "격자 끄기" : "격자 켜기", GUILayout.Height(28f)))
                {
                    ToggleSceneGrid();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("PNG 내보내기"))
                {
                    RunSafely(() => lastMessage = $"PNG 생성: {HexCastlePreviewExporter.Export(selected)}");
                }

                if (GUILayout.Button("A~I 정식 테마 3D 비교 PNG"))
                {
                    RunSafely(() =>
                    {
                        var paths = HexCastleSilhouetteGalleryExporter.ExportAll(seed, defenseLayerCount);
                        lastMessage = $"3D 실루엣 비교 PNG {paths.Count}장 생성: " +
                                      HexCastleSilhouetteGalleryExporter.ResolveOutputFolder();
                    });
                }

                if (GUILayout.Button("육각 개발용 씬 열기"))
                {
                    OpenHexScene();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(
                           !selected.Validation.IsValid || rulesAsset == null))
                {
                    if (GUILayout.Button("플레이 가능 미리보기 생성", GUILayout.Height(30f)))
                    {
                        CreatePlayablePreview();
                    }
                }

                if (GUILayout.Button("플레이 미리보기 제거", GUILayout.Height(30f)))
                {
                    RemovePlayablePreview();
                }
            }
            EditorGUILayout.HelpBox(
                "현재 3D 외형과 같은 Cell Root를 사용합니다. Play Mode에서 공격대가 경로를 따라 " +
                "칸의 체력·Collider·NavMesh 길막을 하나씩 파괴하며, 포탑도 같은 공격대를 표적으로 잡습니다. " +
                "Scene 저장 직전에는 자동 제거됩니다. A~I 모든 정식 테마를 같은 전투 규칙으로 검증합니다.",
                MessageType.Info);
        }

        private void DrawApproval()
        {
            var approvalReady = rulesAsset != null && rulesAsset.CanApproveStageLayout;
            if (!approvalReady)
            {
                EditorGUILayout.HelpBox(
                    "스테이지 배치 승인은 외형·수치 확정 뒤에 열립니다.",
                    MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("6. 스테이지 배치 승인", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                selectedStageId = EditorGUILayout.TextField("선택 스테이지 식별자", selectedStageId);
                using (new EditorGUI.DisabledScope(
                           !approvalReady || Selected == null || !Selected.Validation.IsValid))
                {
                    if (GUILayout.Button("현재 후보를 스테이지 배치로 승인", GUILayout.Height(30f)))
                    {
                        RunSafely(() =>
                        {
                            var asset = HexCastleAssetWriter.SaveApproved(
                                Selected,
                                selectedStageId,
                                rulesAsset,
                                false);
                            Selection.activeObject = asset;
                            EditorGUIUtility.PingObject(asset);
                            lastMessage = $"스테이지 배치 승인 완료: {asset.StageId}";
                        });
                    }
                }

                using (new EditorGUI.DisabledScope(!approvalReady))
                {
                    if (GUILayout.Button("육각 성 스테이지 목록 자산 선택"))
                    {
                        var catalog = HexCastleAssetWriter.LoadOrCreateCatalog(rulesAsset);
                        Selection.activeObject = catalog;
                        EditorGUIUtility.PingObject(catalog);
                        lastMessage = "육각 성 전용 스테이지 목록 자산을 선택했습니다.";
                    }
                }

                using (new EditorGUI.DisabledScope(true))
                {
                    GUILayout.Button("칸 기반 스테이지 프리팹 베이크 · 전투 연결 검증 뒤 활성", GUILayout.Height(26f));
                }
                EditorGUILayout.HelpBox(
                    "승인은 결정론적 배치와 스테이지 목록만 저장합니다. 구형 통짜 메시 생성기는 호출하지 않습니다.",
                    MessageType.None);
            }
        }

        private void DrawReadinessNotice()
        {
            var readiness = rulesAsset != null
                ? rulesAsset.Readiness
                : HexCastleThemeOneReadiness.PreviewDraft;
            switch (readiness)
            {
                case HexCastleThemeOneReadiness.PreviewDraft:
                    EditorGUILayout.HelpBox(
                        "현재 단계: 외형 검수용 초안. 사용자가 외형·크기를 승인하기 전 상태입니다.",
                        MessageType.Warning);
                    break;
                case HexCastleThemeOneReadiness.VisualApprovedBalancePending:
                    EditorGUILayout.HelpBox(
                        "현재 단계: 외형 승인 완료, 수치 미확정. 테마 외형 확장은 가능하지만 스테이지 승인은 잠겨 있습니다.",
                        MessageType.Info);
                    break;
                case HexCastleThemeOneReadiness.StageReady:
                    EditorGUILayout.HelpBox(
                        "현재 단계: 외형·수치 정식화 완료. 스테이지 배치와 목록 자산 승인이 열려 있습니다.",
                        MessageType.Info);
                    break;
            }
        }

        private HexCastleCandidate Selected =>
            selectedIndex >= 0 && selectedIndex < candidates.Count
                ? candidates[selectedIndex]
                : null;

        private void GenerateCandidates(int count)
        {
            RunSafely(() =>
            {
                candidates.Clear();
                var pipeline = new HexCastleGenerationPipeline();
                for (var index = 0; index < count; index++)
                {
                    candidates.Add(pipeline.GenerateFoundation(
                        unchecked(seed + index),
                        defenseLayerCount,
                        theme,
                        ActiveTuning));
                }

                candidates.Sort(CompareCandidates);
                selectedIndex = 0;
                RefreshInlinePreview();
                var selected = Selected;
                if (selected != null)
                {
                    var themeToken = selected.Layout.Theme == HexCastleTheme.CentralCompartment
                        ? "T1"
                        : $"T{HexCastleThemeCatalog.ResolveCode(selected.Layout.Theme)}";
                    selectedStageId = $"HEX_{themeToken}_" +
                                      $"{selected.Layout.DefenseLayerCount}W_{selected.Layout.Seed}";
                }

                lastMessage =
                    $"후보 {candidates.Count}개 중 유효 {candidates.Count(candidate => candidate.Validation.IsValid)}개 · " +
                    "유효성/최소 돌파비용/난이도/시드 순 정렬";
                if (autoApplyScenePreview)
                {
                    CreateScenePreview();
                }
            });
        }

        private void GenerateRandomCandidate()
        {
            var nextSeed = seed;
            while (nextSeed == seed)
            {
                nextSeed = RandomSeedGenerator.Next(1, int.MaxValue);
            }

            seed = nextSeed;
            GenerateCandidates(1);
        }

        private static int CompareCandidates(HexCastleCandidate left, HexCastleCandidate right)
        {
            var validity = right.Validation.IsValid.CompareTo(left.Validation.IsValid);
            if (validity != 0)
            {
                return validity;
            }

            var breach = left.Difficulty.MinimumBreachCost.CompareTo(right.Difficulty.MinimumBreachCost);
            if (breach != 0)
            {
                return breach;
            }

            var score = left.Difficulty.Score.CompareTo(right.Difficulty.Score);
            return score != 0 ? score : left.Layout.Seed.CompareTo(right.Layout.Seed);
        }

        private void CreateScenePreview()
        {
            var selected = Selected;
            if (selected == null)
            {
                return;
            }

            RunSafely(() =>
            {
                if (!EnsureHexSceneOpen())
                {
                    lastMessage = "씬 전환을 취소했습니다.";
                    return;
                }

                HexCastleGenerationPlayablePreview.Clear(SceneManager.GetActiveScene());
                HexCastleFoundationVisualGate.Create(
                    selected.Layout.Seed,
                    selected.Layout.DefenseLayerCount,
                    selected.Layout.Theme,
                    ActiveTuning);
                if (!gridVisible)
                {
                    HexCastleFoundationVisualGate.ToggleGrid();
                }

                lastMessage =
                    $"KayKit 3D 미리보기 생성 완료 · 시드 {selected.Layout.Seed} · " +
                    $"{selected.Layout.DefenseLayerCount}중벽 · 왕궁 외형 배율 {PalaceVisualScale:0.#}";
            });
        }

        private void CreatePlayablePreview()
        {
            var selected = Selected;
            if (selected == null || rulesAsset == null)
            {
                return;
            }

            RunSafely(() =>
            {
                if (!EnsureHexSceneOpen())
                {
                    lastMessage = "씬 전환을 취소했습니다.";
                    return;
                }

                var root = HexCastleGenerationPlayablePreview.Create(
                    selected,
                    Vector3.zero,
                    rulesAsset);
                var grid = root.transform.Find("01_HexGridOverlay");
                if (grid != null)
                {
                    grid.gameObject.SetActive(gridVisible);
                }

                lastMessage =
                    $"플레이 가능 미리보기 생성 완료 · 시드 {selected.Layout.Seed} · " +
                    "Play Mode에서 칸 파괴와 포탑 표적 연결을 확인할 수 있습니다.";
            });
        }

        private void RemovePlayablePreview()
        {
            var removed = HexCastleGenerationPlayablePreview.Clear(SceneManager.GetActiveScene());
            lastMessage = removed > 0
                ? "플레이 가능 미리보기를 제거하고 카메라와 씬 상태를 복구했습니다."
                : "제거할 플레이 가능 미리보기가 없습니다.";
        }

        private void RemoveScenePreview()
        {
            HexCastleFoundationVisualGate.Remove();
            lastMessage = "KayKit 3D 미리보기를 제거하고 씬 원상태를 복구했습니다.";
        }

        private void ToggleSceneGrid()
        {
            gridVisible = !gridVisible;
            var root = GameObject.Find(HexCastleFoundationVisualGate.RootName) ??
                       GameObject.Find(HexCastleGenerationPlayablePreview.RootName);
            var grid = root == null ? null : root.transform.Find("01_HexGridOverlay");
            if (grid != null)
            {
                grid.gameObject.SetActive(gridVisible);
                SceneView.RepaintAll();
            }

            lastMessage = gridVisible ? "육각 격자를 표시했습니다." : "육각 격자를 숨겼습니다.";
        }

        private bool EnsureHexSceneOpen()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.path == HexCastleSceneSetupUtility.ScenePath)
            {
                return true;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return false;
            }

            HexCastleGenerationPlayablePreview.ClearAll();
            EditorSceneManager.OpenScene(HexCastleSceneSetupUtility.ScenePath, OpenSceneMode.Single);
            return true;
        }

        private void OpenHexScene()
        {
            RunSafely(() =>
            {
                if (EnsureHexSceneOpen())
                {
                    lastMessage = "DEV_CastleRaidHex 개발용 씬을 열었습니다.";
                }
            });
        }

        private void EnsureInlinePreview()
        {
            if (previewCandidate == Selected && inlinePreview != null)
            {
                return;
            }

            RefreshInlinePreview();
        }

        private void RefreshInlinePreview()
        {
            DestroyInlinePreview();
            previewCandidate = Selected;
            if (previewCandidate != null)
            {
                inlinePreview = HexCastlePreviewExporter.BuildTexture(previewCandidate, 640);
            }
        }

        private void DestroyInlinePreview()
        {
            if (inlinePreview != null)
            {
                DestroyImmediate(inlinePreview);
            }

            inlinePreview = null;
            previewCandidate = null;
        }

        private void RunSafely(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                lastMessage = exception.Message;
                Debug.LogException(exception);
            }
        }
    }
}
