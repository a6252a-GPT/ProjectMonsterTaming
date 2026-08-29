using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Contents.CastleRaid.Generation;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.CastleBake
{
    public sealed class CastleGenerationWindow : EditorWindow // 무저장 후보 생성·검수·승인 도구
    {
        private const float PreviewMaximumSize = 560f;
        private const int CurrentScenePreviewSettingsVersion = 3;

        [SerializeField] private CastleGenerationRules rules;
        [SerializeField] private int seed = 1001;
        [SerializeField] private CastleLayoutTheme layoutTheme = CastleLayoutTheme.CentralCompartmentFortress;
        [SerializeField, Range(2, 4)] private int defenseLayerCount = 2;
        [SerializeField, Range(1, 100)] private int batchCount = 12;
        [SerializeField] private bool autoBuildScenePreview = true;
        [SerializeField] private Vector3 scenePreviewOffset = Vector3.zero;
        [SerializeField, Range(0.5f, 1.5f)] private float scenePreviewCellSize = CastleGenerationScenePreview.DefaultCellSize;
        [SerializeField] private CastleScenePreviewColorMode scenePreviewColorMode = CastleScenePreviewColorMode.Architecture;
        [SerializeField] private int scenePreviewSettingsVersion;
        [SerializeField] private string singleStageId = "castle_stage_001";
        [SerializeField, Min(1)] private int firstStageNumber = 1;

        private readonly List<CastleGenerationCandidate> candidates = new List<CastleGenerationCandidate>();
        private Vector2 scrollPosition;
        private Vector2 candidateScrollPosition;
        private int selectedIndex = -1;
        private string lastMessage = "기본 생성 규칙을 선택한 뒤 후보를 생성하세요.";

        [MenuItem("JC Tool/Castle Raid/Castle Generator")]
        private static void OpenWindow()
        {
            var window = GetWindow<CastleGenerationWindow>();
            window.titleContent = new GUIContent("Castle Generator");
            window.minSize = new Vector2(760f, 720f);
            window.Show();
        }

        private void OnEnable()
        {
            if (scenePreviewSettingsVersion < CurrentScenePreviewSettingsVersion)
            {
                scenePreviewOffset = CastleGenerationScenePreview.DefaultWorldOffset;
                scenePreviewCellSize = CastleGenerationScenePreview.DefaultCellSize;
                scenePreviewColorMode = CastleScenePreviewColorMode.Architecture;
                scenePreviewSettingsVersion = CurrentScenePreviewSettingsVersion;
            }

            if (rules == null)
            {
                rules = AssetDatabase.LoadAssetAtPath<CastleGenerationRules>(CastleGenerationAssetFactory.DefaultRulesPath);
            }
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.LabelField("군단의 역습 정식 성 생성", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "후보 생성·검수·난이도 계산은 메모리에서 실행합니다. 일반 3D 프리뷰는 저장되지 않는 시각화이고, 플레이 가능 프리뷰는 현재 Scene에 임시 전투 오브젝트를 만들지만 Scene 저장 직전에 자동 제거됩니다. 아래 승인 저장 버튼을 누르기 전에는 Prefab·Catalog를 수정하지 않습니다.",
                MessageType.Info);

            DrawRulesSection();
            DrawGenerationSection();
            DrawCandidateList();

            var selected = ResolveSelectedCandidate();
            if (selected != null)
            {
                DrawDifficulty(selected);
                DrawPreview(selected);
                DrawApprovalSection(selected);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("현재 결과", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(lastMessage, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void DrawRulesSection()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("1. 생성 규칙", EditorStyles.boldLabel);
            rules = (CastleGenerationRules)EditorGUILayout.ObjectField(
                "Generation Rules",
                rules,
                typeof(CastleGenerationRules),
                false);

            if (rules == null)
            {
                if (GUILayout.Button("기본 템플릿·규칙 자산 생성"))
                {
                    rules = CastleGenerationAssetFactory.CreateOrUpdateDefaults();
                    lastMessage = "정식 중앙 격실 요새의 2중·3중·4중벽 생성 규칙을 갱신했습니다.";
                }

                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("규칙 버전", rules.RulesVersion);
                EditorGUILayout.Vector2IntField("그리드", new Vector2Int(rules.GridWidth, rules.GridHeight));
                EditorGUILayout.IntField("구역 수 최소", rules.MinimumDistrictCount);
                EditorGUILayout.IntField("구역 수 최대", rules.MaximumDistrictCount);
                EditorGUILayout.IntField("특수 구역 최대", rules.MaximumSpecialDistrictCount);
                EditorGUILayout.IntField("보상 예산 최대", rules.MaximumRewardBudget);
            }

            if (!rules.TryValidate(out var error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }

        private void DrawGenerationSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("2. 후보 생성", EditorStyles.boldLabel);
            var supportedThemes = CastleGenerationRules.SupportedLayoutThemes.ToArray();
            var themeLabels = supportedThemes.Select(ResolveThemeLabel).ToArray();
            var themeIndex = Mathf.Max(0, Array.IndexOf(supportedThemes, layoutTheme));
            themeIndex = EditorGUILayout.Popup("배치 테마", themeIndex, themeLabels);
            layoutTheme = supportedThemes[themeIndex];
            defenseLayerCount = EditorGUILayout.IntPopup(
                "성벽 겹 수",
                defenseLayerCount,
                new[] { "2중벽", "3중벽", "4중벽" },
                new[] { 2, 3, 4 });
            if (rules != null && rules.TryValidate(out _))
            {
                var themeRule = rules.ResolveThemeRule(layoutTheme);
                CastleGenerationRules.ResolveCompartmentCountRange(
                    layoutTheme,
                    defenseLayerCount,
                    out var minimumCount,
                    out var maximumCount);
                var compartmentText = minimumCount == maximumCount
                    ? minimumCount.ToString()
                    : $"{minimumCount}~{maximumCount}";
                EditorGUILayout.LabelField(
                    "테마 구조 계약",
                    $"{defenseLayerCount}중벽 · 일반 격실 {compartmentText} · 보호 깊이 {defenseLayerCount} · {themeRule.Symmetry}");
                if (layoutTheme == CastleLayoutTheme.HexHoneycombFortress)
                {
                    EditorGUILayout.HelpBox(
                        "행 폭 3→5→7→5→3의 기본 육각 셀을 같은 방어층 안에서만 합쳐 소형 1셀·중형 2셀·육아방 3셀로 구성합니다. 중앙은 7셀 여왕방이며, 외곽 싹은 별도 방벽을 유지해 요청한 2~4중 보호 깊이를 보존합니다. Bounds의 빈 모서리는 실제 바닥이나 방이 아닙니다.",
                        MessageType.Info);
                }
                else if (layoutTheme == CastleLayoutTheme.PetalBloomFortress)
                {
                    EditorGUILayout.HelpBox(
                        "중앙 왕궁을 꽃술로 두고, 방어층마다 8개의 독립 꽃잎 격실을 둘러싼니다. 안쪽은 작고 넓은 꽃받침, 바깥쪽은 길고 날카로운 꽃잎으로 확장되며, 성벽 격수가 늘어나도 기존 내부 꽃잎은 보존됩니다.",
                        MessageType.Info);
                }
                else if (CastleGenerationRules.IsGeometricTheme(layoutTheme))
                {
                    EditorGUILayout.HelpBox(ResolveGeometricThemeHelp(layoutTheme), MessageType.Info);
                }
            }
            seed = EditorGUILayout.IntField("시작 Seed", seed);
            batchCount = EditorGUILayout.IntSlider("일괄 후보 수", batchCount, 1, 100);
            EditorGUILayout.Space(4f);
            autoBuildScenePreview = EditorGUILayout.ToggleLeft(
                "생성·후보 선택 시 3D 씬 프리뷰 자동 갱신",
                autoBuildScenePreview);
            scenePreviewOffset = EditorGUILayout.Vector3Field("3D 프리뷰 기준 위치", scenePreviewOffset);
            scenePreviewCellSize = EditorGUILayout.Slider("1 Cell 월드 크기", scenePreviewCellSize, 0.5f, 1.5f);
            scenePreviewColorMode = (CastleScenePreviewColorMode)EditorGUILayout.EnumPopup(
                "3D 색상 모드",
                scenePreviewColorMode);
            if (rules != null)
            {
                EditorGUILayout.LabelField(
                    "논리 그리드 크기",
                    $"{rules.GridWidth * scenePreviewCellSize:0.0} × {rules.GridHeight * scenePreviewCellSize:0.0} World Unit");
            }
            EditorGUILayout.HelpBox(
                "논리 좌표는 50×50을 유지합니다. 3D 바닥은 생성된 성 바깥에 정식 소환 영역 3 Cell을 사방으로 반드시 남기고, 그 바깥 여분만 최소 정사각형으로 잘라냅니다. 맨 끝의 진한 바닥이 소환 벨트이며 카메라는 이 영역까지 전부 보이도록 자동 확대·이동합니다. 기존 CastleStage_Seed와 카메라는 프리뷰 제거·Play 진입·Scene 저장 전에 원상 복구합니다.",
                MessageType.None);
            using (new EditorGUI.DisabledScope(rules == null || !rules.TryValidate(out _)))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Seed 하나 생성", GUILayout.Height(32f)))
                {
                    GenerateCandidates(1);
                }

                if (GUILayout.Button("연속 Seed 일괄 생성", GUILayout.Height(32f)))
                {
                    GenerateCandidates(batchCount);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private static string ResolveThemeLabel(CastleLayoutTheme theme)
        {
            switch (theme)
            {
                case CastleLayoutTheme.CentralCompartmentFortress:
                    return "테마 A · 중앙 격실 요새";
                case CastleLayoutTheme.DiamondRadialFortress:
                    return "테마 B · 마름모 방사형 요새";
                case CastleLayoutTheme.HoneycombCompartmentFortress:
                    return "테마 C · 복합 사각 격실 요새";
                case CastleLayoutTheme.HexHoneycombFortress:
                    return "테마 D · 육각 벌집 요새";
                case CastleLayoutTheme.PetalBloomFortress:
                    return "테마 E · 꽃잎 군락 요새";
                case CastleLayoutTheme.CrystalMandalaFortress:
                    return "테마 F · 수정 만다라 성채";
                case CastleLayoutTheme.TwinSpiralFortress:
                    return "테마 G · 쌍나선 성채";
                case CastleLayoutTheme.FractalBastionFortress:
                    return "테마 H · 프랙탈 능보";
                case CastleLayoutTheme.VoronoiCrystalFortress:
                    return "테마 I · 보로노이 수정군";
                case CastleLayoutTheme.IrisShutterFortress:
                    return "테마 J · 홍채 셔터 요새";
                default:
                    return theme.ToString();
            }
        }

        private static string ResolveStructureVariantLabel(CastleStructureVariant variant)
        {
            if (TryResolveGeometricVariantLabel(variant, out var geometricLabel))
            {
                return geometricLabel;
            }

            switch (variant)
            {
                case CastleStructureVariant.CentralAdaptive:
                    return "중앙 가변 격실형";
                case CastleStructureVariant.DiamondBalanced:
                    return "균형 마름모형";
                case CastleStructureVariant.DiamondHorizontalWide:
                    return "가로 확장형";
                case CastleStructureVariant.DiamondVerticalTall:
                    return "세로 확장형";
                case CastleStructureVariant.DiamondDiagonalNorthEast:
                    return "북동 편향형";
                case CastleStructureVariant.DiamondDiagonalSouthEast:
                    return "남동 편향형";
                case CastleStructureVariant.DiamondDiagonalSouthWest:
                    return "남서 편향형";
                case CastleStructureVariant.DiamondDiagonalNorthWest:
                    return "북서 편향형";
                case CastleStructureVariant.DiamondStaggeredClockwise:
                    return "시계방향 엇갈림형";
                case CastleStructureVariant.DiamondStaggeredCounterClockwise:
                    return "반시계방향 엇갈림형";
                case CastleStructureVariant.HoneycombBalanced:
                    return "복합 격실 균형형";
                case CastleStructureVariant.HoneycombHorizontalWide:
                    return "복합 격실 가로 확장형";
                case CastleStructureVariant.HoneycombVerticalTall:
                    return "복합 격실 세로 확장형";
                case CastleStructureVariant.HoneycombDiagonalNorthEast:
                    return "복합 격실 북동 편향형";
                case CastleStructureVariant.HoneycombDiagonalSouthEast:
                    return "복합 격실 남동 편향형";
                case CastleStructureVariant.HoneycombDiagonalSouthWest:
                    return "복합 격실 남서 편향형";
                case CastleStructureVariant.HoneycombDiagonalNorthWest:
                    return "복합 격실 북서 편향형";
                case CastleStructureVariant.HoneycombStaggeredClockwise:
                    return "복합 격실 시계방향 엇갈림형";
                case CastleStructureVariant.HoneycombStaggeredCounterClockwise:
                    return "복합 격실 반시계방향 엇갈림형";
                case CastleStructureVariant.HexHoneycombFlatPhaseA:
                    return "육각 가로형 · 삼엽 왕관 군락";
                case CastleStructureVariant.HexHoneycombFlatPhaseB:
                    return "육각 가로형 · 쌍각 군락";
                case CastleStructureVariant.HexHoneycombFlatPhaseC:
                    return "육각 가로형 · 오엽 나선 군락";
                case CastleStructureVariant.HexHoneycombFlatPhaseD:
                    return "육각 가로형 · 육방성 군락";
                case CastleStructureVariant.HexHoneycombPointyPhaseA:
                    return "육각 세로형 · 삼엽 왕관 군락";
                case CastleStructureVariant.HexHoneycombPointyPhaseB:
                    return "육각 세로형 · 쌍각 군락";
                case CastleStructureVariant.HexHoneycombPointyPhaseC:
                    return "육각 세로형 · 오엽 나선 군락";
                case CastleStructureVariant.HexHoneycombPointyPhaseD:
                    return "육각 세로형 · 육방성 군락";
                case CastleStructureVariant.PetalBloomBalanced:
                    return "8엽 균형 개화형";
                case CastleStructureVariant.PetalBloomRotated:
                    return "8엽 대각 회전형";
                case CastleStructureVariant.PetalBloomLongCardinal:
                    return "직교축 장엽형";
                case CastleStructureVariant.PetalBloomLongDiagonal:
                    return "대각축 장엽형";
                case CastleStructureVariant.PetalBloomClockwise:
                    return "시계방향 겹꽃잎형";
                case CastleStructureVariant.PetalBloomCounterClockwise:
                    return "반시계방향 겹꽃잎형";
                case CastleStructureVariant.PetalBloomTightHeart:
                    return "조밀한 꽃술형";
                case CastleStructureVariant.PetalBloomWideCrown:
                    return "넓은 화관형";
                default:
                    return variant.ToString();
            }
        }

        private static string ResolveGeometricThemeHelp(CastleLayoutTheme theme)
        {
            switch (theme)
            {
                case CastleLayoutTheme.CrystalMandalaFortress:
                    return "교차 회전한 16각 장·단 반경을 사각 Cell로 래스터화해 수정 단면처럼 날카로운 8개 격실을 만듭니다. 방어층이 늘면 안쪽 결정을 보존하고 바깥 결정 고리만 증축합니다.";
                case CastleLayoutTheme.TwinSpiralFortress:
                    return "반대편 두 흐름이 왕궁을 감싸도록 격벽과 외곽 블레이드를 함께 회전합니다. 외곽은 두 개의 돌출 날과 후퇴 골을 방어층마다 비틀어 단순한 둥근 사각형을 피하고, 내부는 8개 폐쇄 격실로 잠가 실제 보호 깊이를 보장합니다.";
                case CastleLayoutTheme.FractalBastionFortress:
                    return "4방향 능보와 8방향 작은 가지를 단계적으로 반복하는 재귀형 윤곽입니다. 축 끝은 길게, 축 사이는 짧게 잘라 십자·H·T 골격이 여러 크기로 반복됩니다.";
                case CastleLayoutTheme.VoronoiCrystalFortress:
                    return "반대편 Seed를 쌍으로 배치한 Voronoi 분할로 크기가 다른 결정 격실을 만듭니다. 외곽도 반대편이 대응하는 불규칙 결정 반경을 사용해 균형과 변화량을 함께 유지합니다.";
                case CastleLayoutTheme.IrisShutterFortress:
                    return "8개의 톱니형 반경과 회전하는 격벽을 결합해 카메라 조리개처럼 왕궁을 잠급니다. 실제 벽 겹침 없이 각 방어층의 칼날 위상을 달리해 셔터가 포개진 인상을 만듭니다.";
                default:
                    return string.Empty;
            }
        }

        private static bool TryResolveGeometricVariantLabel(
            CastleStructureVariant variant,
            out string label)
        {
            var value = (int)variant;
            var first = value / 10 * 10;
            string theme;
            switch (first)
            {
                case 50:
                    theme = "수정 만다라";
                    break;
                case 60:
                    theme = "쌍나선";
                    break;
                case 70:
                    theme = "프랙탈 능보";
                    break;
                case 80:
                    theme = "보로노이 수정";
                    break;
                case 90:
                    theme = "홍채 셔터";
                    break;
                default:
                    label = string.Empty;
                    return false;
            }

            var profileLabels = new[]
            {
                "균형형",
                "회전형",
                "직교 확장형",
                "교차 확장형",
                "시계방향 변주형",
                "반시계방향 변주형",
                "조밀형",
                "확장형"
            };
            var profileIndex = value - first;
            if (profileIndex < 0 || profileIndex >= profileLabels.Length)
            {
                label = string.Empty;
                return false;
            }

            label = $"{theme} · {profileLabels[profileIndex]}";
            return true;
        }

        private void DrawCandidateList()
        {
            if (candidates.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField($"3. 난이도순 후보 ({candidates.Count})", EditorStyles.boldLabel);
            candidateScrollPosition = EditorGUILayout.BeginScrollView(candidateScrollPosition, GUILayout.Height(150f));
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                var valid = candidate.Validation.IsValid && candidate.Difficulty.HasClearPath;
                var regularCount = candidate.Compartments.Count(value => value.Role != CastleCompartmentRole.PalaceCore);
                var label = valid
                    ? $"{index + 1:00}. {candidate.RequestedDefenseLayerCount}중벽 · {ResolveStructureVariantLabel(candidate.StructureVariant)} · Seed {candidate.Seed} · 격실 {regularCount} · 깊이 {candidate.ProtectionDepth} · 최소 피해 {candidate.Difficulty.MinimumClearDamage:N0} · 구조 {ShortHash(candidate.StructureHash)}"
                    : $"{index + 1:00}. Seed {candidate.Seed} · 검수 실패 {candidate.Validation.Issues.Count}건";
                var style = index == selectedIndex ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                if (GUILayout.Button(label, style))
                {
                    selectedIndex = index;
                    singleStageId = $"castle_stage_{index + 1:000}";
                    if (autoBuildScenePreview)
                    {
                        SetScenePreview(candidate);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static void DrawDifficulty(CastleGenerationCandidate candidate)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("4. 검수·난이도 보고서", EditorStyles.boldLabel);
            if (!candidate.Validation.IsValid)
            {
                foreach (var issue in candidate.Validation.Issues)
                {
                    EditorGUILayout.HelpBox($"[{issue.Code}] {issue.Message} {issue.PlacementId}", MessageType.Error);
                }

                return;
            }

            EditorGUILayout.LabelField("검수", "통과");
            EditorGUILayout.LabelField("Seed / 규칙", $"{candidate.Seed} / v{candidate.RulesVersion}");
            EditorGUILayout.LabelField("배치 테마", ResolveThemeLabel(candidate.Theme));
            EditorGUILayout.LabelField("구조 프로필", ResolveStructureVariantLabel(candidate.StructureVariant));
            EditorGUILayout.LabelField("요청 성벽 겹 수", $"{candidate.RequestedDefenseLayerCount}중벽");
            EditorGUILayout.LabelField("일반 격실 수", candidate.Compartments.Count(value => value.Role != CastleCompartmentRole.PalaceCore).ToString("N0"));
            EditorGUILayout.LabelField("왕궁 코어 노출면", candidate.PalaceExposedSideCount.ToString("N0"));
            EditorGUILayout.LabelField("최소 보호 깊이", candidate.ProtectionDepth.ToString("N0"));
            EditorGUILayout.LabelField("격실 밀집도", candidate.Compactness.ToString("P1"));
            EditorGUILayout.LabelField("병합 공유 성벽", candidate.Placements.Count(value => value.Kind == CastlePlacementKind.Wall && value.OwnerDistrictIds.Count > 1).ToString("N0"));
            var walls = candidate.Placements.Where(value => value.Kind == CastlePlacementKind.Wall).ToArray();
            EditorGUILayout.LabelField("성벽 라인 수", walls.Select(value => value.WallLineId).Distinct().Count().ToString("N0"));
            EditorGUILayout.LabelField(
                "방어선 분류",
                string.Join(" · ", walls
                    .GroupBy(value => value.WallBand)
                    .OrderBy(value => value.Key)
                    .Select(group => $"{group.Key} {group.Count()}")));
            EditorGUILayout.LabelField("Structure Hash", candidate.StructureHash);
            EditorGUILayout.LabelField("Layout Hash", candidate.LayoutHash);
            EditorGUILayout.LabelField("최소 클리어 피해량", candidate.Difficulty.MinimumClearDamage.ToString("N0"));
            EditorGUILayout.LabelField("왕궁 제외 필수 파괴", candidate.Difficulty.MandatoryObstacleDamage.ToString("N0"));
            EditorGUILayout.LabelField("왕궁 피해량", candidate.Difficulty.PalaceDamage.ToString("N0"));
            EditorGUILayout.LabelField("방어 압박 기준값", candidate.Difficulty.DefensePressure.ToString("N0"));
            EditorGUILayout.LabelField("전체 파괴 피해량", candidate.Difficulty.TotalDestructionDamage.ToString("N0"));
            EditorGUILayout.LabelField("골드 접근 피해", FormatOptionalDamage(candidate.Difficulty.GoldLootDamage));
            EditorGUILayout.LabelField("장비 접근 피해", FormatOptionalDamage(candidate.Difficulty.EquipmentLootDamage));
            EditorGUILayout.LabelField("열쇠 접근 피해", FormatOptionalDamage(candidate.Difficulty.KeyLootDamage));
            EditorGUILayout.LabelField("최단 경로 필수 대상", candidate.Difficulty.MandatoryPlacementIds.Count.ToString("N0"));
        }

        private void DrawPreview(CastleGenerationCandidate candidate)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("5. 그리드 미리보기", EditorStyles.boldLabel);
            var sceneDisplayBounds = CastleGenerationScenePreview.ResolveSquareDisplayBounds(candidate);
            EditorGUILayout.LabelField(
                "3D 정사각형 표시 영역",
                $"{sceneDisplayBounds.width * scenePreviewCellSize:0.0} × {sceneDisplayBounds.height * scenePreviewCellSize:0.0} World Unit · X {sceneDisplayBounds.xMin}~{sceneDisplayBounds.xMax - 1} · Z {sceneDisplayBounds.yMin}~{sceneDisplayBounds.yMax - 1}");
            EditorGUILayout.LabelField(
                "맨 끝 소환 벨트",
                $"사방 {CastleGenerationScenePreview.PreviewGroundMarginCells} Cell · 구조물 배치 금지");
            EditorGUILayout.LabelField(
                "3D 자동 카메라 Size",
                CastleGenerationScenePreview.ResolvePreviewCameraSize(candidate, scenePreviewCellSize).ToString("0.00"));
            var availableWidth = Mathf.Max(240f, EditorGUIUtility.currentViewWidth - 70f);
            var previewSize = Mathf.Min(PreviewMaximumSize, availableWidth);
            var cellSize = Mathf.Max(4f, Mathf.Floor(previewSize / Mathf.Max(candidate.GridWidth, candidate.GridHeight)));
            var width = candidate.GridWidth * cellSize;
            var height = candidate.GridHeight * cellSize;
            var rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.1f, 0.11f));

            var cellPlacements = new CastlePlacementData[candidate.GridWidth, candidate.GridHeight];
            var compartmentRoles = new CastleCompartmentRole?[candidate.GridWidth, candidate.GridHeight];
            foreach (var compartment in candidate.Compartments)
            {
                foreach (var cell in compartment.EnumerateFootprintCells())
                {
                    var x = cell.x;
                    var z = cell.y;
                    if (x >= 0 && z >= 0 && x < candidate.GridWidth && z < candidate.GridHeight)
                    {
                        compartmentRoles[x, z] = compartment.Role;
                    }
                }
            }

            foreach (var placement in candidate.Placements)
            {
                for (var x = placement.X; x < placement.X + placement.Width; x++)
                {
                    for (var z = placement.Z; z < placement.Z + placement.Height; z++)
                    {
                        if (x >= 0 && z >= 0 && x < candidate.GridWidth && z < candidate.GridHeight)
                        {
                            cellPlacements[x, z] = placement;
                        }
                    }
                }
            }

            var mandatory = new HashSet<string>(candidate.Difficulty.MandatoryPlacementIds, StringComparer.Ordinal);
            for (var x = 0; x < candidate.GridWidth; x++)
            {
                for (var z = 0; z < candidate.GridHeight; z++)
                {
                    var placement = cellPlacements[x, z];
                    var color = placement != null
                        ? CastleGenerationPreviewExporter.ResolvePlacementColor(placement, mandatory)
                        : CastleGenerationPreviewExporter.ResolveFloorColor(
                            new Vector2Int(x, z),
                            compartmentRoles[x, z]);
                    var cellRect = new Rect(
                        rect.x + x * cellSize + 0.5f,
                        rect.y + (candidate.GridHeight - 1 - z) * cellSize + 0.5f,
                        Mathf.Max(1f, cellSize - 1f),
                        Mathf.Max(1f, cellSize - 1f));
                    EditorGUI.DrawRect(cellRect, color);
                }
            }

            DrawPreviewLegend();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("선택 후보 3D 씬 프리뷰 생성", GUILayout.Height(28f)))
            {
                SetScenePreview(candidate);
            }

            if (GUILayout.Button("3D 씬 프리뷰 제거", GUILayout.Height(28f)))
            {
                var removed = CastleGenerationScenePreview.ClearActive();
                lastMessage = removed > 0
                    ? "3D 씬 프리뷰를 제거했습니다. 실제 Scene 자산은 변경되지 않았습니다."
                    : "제거할 3D 씬 프리뷰가 없습니다.";
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(
                "플레이 가능 프리뷰는 각 성벽·건물·수비대·왕궁을 개별 CastleTarget으로 만들고 Placement의 EffectiveHealth, Collider, 공격 자리, NavMeshObstacle을 연결합니다. 생성 후 Play하면 기존 CastleRaidController와 몬스터 공격을 그대로 사용합니다. 정식 Prefab 베이크가 아니라 현재 Scene 전용 임시 시험장입니다.",
                MessageType.Warning);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("선택 후보 플레이 가능 프리뷰 생성", GUILayout.Height(32f)))
            {
                SetPlayablePreview(candidate);
            }

            if (GUILayout.Button("플레이 가능 프리뷰 제거", GUILayout.Height(32f)))
            {
                var removed = CastleGenerationPlayablePreview.ClearActive();
                lastMessage = removed > 0
                    ? "플레이 가능 프리뷰를 제거하고 기존 성·카메라를 복원했습니다."
                    : "제거할 플레이 가능 프리뷰가 없습니다.";
            }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("선택 후보 PNG 미리보기 내보내기"))
            {
                var path = CastleGenerationPreviewExporter.ExportToTemp(candidate);
                Debug.Log($"Castle Raid 미리보기를 내보냈습니다: {path}");
            }
        }

        private static void DrawPreviewLegend()
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("미리보기 색상 범례", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "주황색은 원래 종류 색상보다 우선하는 최단 공략 필수 파괴 표시입니다. 성벽 계열은 같은 색 안에서 밝을수록 단계 또는 방어 깊이가 높습니다.",
                MessageType.None);

            var groups = CastleGenerationPreviewExporter.LegendEntries
                .GroupBy(entry => entry.Category)
                .ToArray();
            for (var index = 0; index < groups.Length; index += 2)
            {
                EditorGUILayout.BeginHorizontal();
                DrawLegendGroup(groups[index]);
                if (index + 1 < groups.Length)
                {
                    DrawLegendGroup(groups[index + 1]);
                }
                else
                {
                    GUILayout.FlexibleSpace();
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private static void DrawLegendGroup(IEnumerable<CastlePreviewLegendEntry> entries)
        {
            var values = entries.ToArray();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinWidth(280f));
            EditorGUILayout.LabelField(values[0].Category, EditorStyles.miniBoldLabel);
            foreach (var entry in values)
            {
                DrawLegendRow(entry);
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawLegendRow(CastlePreviewLegendEntry entry)
        {
            var line = EditorGUILayout.GetControlRect(false, 19f);
            var border = new Rect(line.x, line.y + 1f, 18f, 17f);
            var swatch = new Rect(border.x + 1f, border.y + 1f, border.width - 2f, border.height - 2f);
            EditorGUI.DrawRect(border, new Color(0.03f, 0.03f, 0.03f));
            EditorGUI.DrawRect(swatch, entry.Color);
            EditorGUI.LabelField(
                new Rect(line.x + 25f, line.y, Mathf.Max(40f, line.width - 25f), line.height),
                entry.Label,
                EditorStyles.miniLabel);
        }

        private void DrawApprovalSection(CastleGenerationCandidate selected)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("6. 승인 레이아웃 저장", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "이 단계는 실제 성 Prefab이나 NavMesh를 만들지 않고 검수된 논리 배치를 StageLayout 자산으로 고정합니다. Prefab·NavMesh·광원 베이크는 다음 제작 단계에서 이 자산만 입력으로 사용합니다.",
                MessageType.Warning);

            singleStageId = EditorGUILayout.TextField("선택 후보 StageId", singleStageId);
            using (new EditorGUI.DisabledScope(!selected.Validation.IsValid || !selected.Difficulty.HasClearPath || string.IsNullOrWhiteSpace(singleStageId)))
            {
                if (GUILayout.Button("선택 후보 승인 StageLayout 저장", GUILayout.Height(34f)))
                {
                    SaveSingle(selected);
                }
            }

            firstStageNumber = Mathf.Max(1, EditorGUILayout.IntField("일괄 시작 Stage 번호", firstStageNumber));
            using (new EditorGUI.DisabledScope(candidates.All(candidate => !candidate.Validation.IsValid || !candidate.Difficulty.HasClearPath)))
            {
                if (GUILayout.Button("유효 후보 전체를 난이도순 StageLayout으로 저장", GUILayout.Height(34f)))
                {
                    SaveAllValid();
                }
            }
        }

        private void GenerateCandidates(int count)
        {
            try
            {
                var generator = new CastleGenerator();
                candidates.Clear();
                for (var index = 0; index < count; index++)
                {
                    candidates.Add(generator.Generate(
                        rules,
                        unchecked(seed + index),
                        layoutTheme,
                        defenseLayerCount));
                }

                candidates.Sort(CompareCandidates);
                selectedIndex = candidates.Count > 0 ? 0 : -1;
                if (selectedIndex >= 0)
                {
                    singleStageId = "castle_stage_001";
                }

                var validCount = candidates.Count(candidate => candidate.Validation.IsValid && candidate.Difficulty.HasClearPath);
                lastMessage = $"{layoutTheme} {defenseLayerCount}중벽으로 Seed {seed}부터 {count}개를 생성했습니다. 유효 {validCount}개, 검수 실패 {count - validCount}개입니다.";
                if (autoBuildScenePreview && selectedIndex >= 0)
                {
                    try
                    {
                        CastleGenerationPlayablePreview.ClearActive();
                        var preview = CastleGenerationScenePreview.Rebuild(
                            candidates[selectedIndex],
                            scenePreviewOffset,
                            scenePreviewCellSize,
                            scenePreviewColorMode,
                            true);
                        lastMessage += $" 첫 후보를 {preview.name}에 3D로 표시했습니다.";
                    }
                    catch (Exception previewException)
                    {
                        lastMessage += $" 3D 프리뷰 생성은 실패했습니다: {previewException.Message}";
                        Debug.LogException(previewException);
                    }
                }
            }
            catch (Exception exception)
            {
                candidates.Clear();
                selectedIndex = -1;
                lastMessage = exception.Message;
                Debug.LogException(exception);
            }
        }

        private void SaveSingle(CastleGenerationCandidate candidate)
        {
            try
            {
                var layout = CastleStageLayoutAssetWriter.Create(
                    CastleStageLayoutAssetWriter.DefaultStageDraftRoot,
                    singleStageId.Trim(),
                    candidate);
                Selection.activeObject = layout;
                EditorGUIUtility.PingObject(layout);
                lastMessage = $"승인 레이아웃을 저장했습니다: {AssetDatabase.GetAssetPath(layout)}";
            }
            catch (Exception exception)
            {
                lastMessage = exception.Message;
                Debug.LogException(exception);
            }
        }

        private void SaveAllValid()
        {
            try
            {
                var valid = candidates
                    .Where(candidate => candidate.Validation.IsValid && candidate.Difficulty.HasClearPath)
                    .OrderBy(candidate => candidate.Difficulty.MinimumClearDamage)
                    .ThenBy(candidate => candidate.Difficulty.DefensePressure)
                    .ThenBy(candidate => candidate.Seed)
                    .ToArray();
                var stageIds = Enumerable.Range(firstStageNumber, valid.Length)
                    .Select(number => $"castle_stage_{number:000}")
                    .ToArray();
                var created = CastleStageLayoutAssetWriter.CreateBatch(
                    CastleStageLayoutAssetWriter.DefaultStageDraftRoot,
                    stageIds,
                    valid);
                if (created.Count > 0)
                {
                    Selection.activeObject = created[0];
                    EditorGUIUtility.PingObject(created[0]);
                }

                lastMessage = $"유효 후보 {created.Count}개를 Stage {firstStageNumber}부터 난이도순으로 고정했습니다.";
            }
            catch (Exception exception)
            {
                lastMessage = exception.Message;
                Debug.LogException(exception);
            }
        }

        private CastleGenerationCandidate ResolveSelectedCandidate()
        {
            return selectedIndex >= 0 && selectedIndex < candidates.Count ? candidates[selectedIndex] : null;
        }

        private void SetScenePreview(CastleGenerationCandidate candidate)
        {
            try
            {
                CastleGenerationPlayablePreview.ClearActive();
                var preview = CastleGenerationScenePreview.Rebuild(
                    candidate,
                    scenePreviewOffset,
                    scenePreviewCellSize,
                    scenePreviewColorMode,
                    true);
                lastMessage = $"Seed {candidate.Seed} 후보를 {preview.name}에 3D로 표시했습니다. 이 프리뷰는 Scene에 저장되지 않습니다.";
            }
            catch (Exception exception)
            {
                lastMessage = exception.Message;
                Debug.LogException(exception);
            }
        }

        private void SetPlayablePreview(CastleGenerationCandidate candidate)
        {
            try
            {
                var runtimeStage = CastleGenerationPlayablePreview.Rebuild(
                    candidate,
                    scenePreviewOffset,
                    scenePreviewCellSize,
                    true);
                lastMessage =
                    $"Seed {candidate.Seed}을 {runtimeStage.name}에 전투 시험장으로 만들었습니다. " +
                    $"목표 {runtimeStage.Targets.Length}개에 실제 체력을 연결했습니다. Play로 검증하고 Scene 저장 전에는 자동 제거됩니다.";
            }
            catch (Exception exception)
            {
                lastMessage = exception.Message;
                Debug.LogException(exception);
            }
        }

        private static int CompareCandidates(CastleGenerationCandidate left, CastleGenerationCandidate right)
        {
            var leftValid = left.Validation.IsValid && left.Difficulty.HasClearPath;
            var rightValid = right.Validation.IsValid && right.Difficulty.HasClearPath;
            if (leftValid != rightValid)
            {
                return leftValid ? -1 : 1;
            }

            var comparison = left.Difficulty.MinimumClearDamage.CompareTo(right.Difficulty.MinimumClearDamage);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.Difficulty.DefensePressure.CompareTo(right.Difficulty.DefensePressure);
            return comparison != 0 ? comparison : left.Seed.CompareTo(right.Seed);
        }

        private static string FormatOptionalDamage(float value)
        {
            return value < 0f ? "없음" : value.ToString("N0");
        }

        private static string ShortHash(string hash)
        {
            return string.IsNullOrEmpty(hash) || hash.Length <= 12 ? hash : hash.Substring(0, 12);
        }

    }
}
