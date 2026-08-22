using ProjectMT.Bootstrap;
using ProjectMT.Features.MainBattle;
using ProjectMT.Shared.Combat;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.Combat
{
    public sealed class CombatTuningTableWindow : EditorWindow // 공용 전투 수치 표 편집기
    {
        private const string MenuPath = "JC Tool/Combat/전투 튜닝 테이블";
        private const string ConfigPath =
            "Assets/ProjectMT/02_Shared/Combat/Data/CombatTuningConfig.asset";
        private const string ProjectConfigPath =
            "Assets/ProjectMT/01_Core/Bootstrap/Data/ProjectConfig.asset";
        private const string AIProfilePath =
            "Assets/ProjectMT/03_Features/MainBattle/Resources/MainBattleAIProfileCatalog.asset";

        private static readonly GUIContent[] ImpactColumns =
        {
            new GUIContent("구분", "공격의 기본 타격 등급입니다. Light < Standard < Heavy 순으로 더 큰 반응을 사용합니다."),
            new GUIContent("피격 정지", "피격 대상의 애니메이션을 잠시 멈추는 시간(초)입니다. 높일수록 타격 순간이 묵직해지지만 너무 크면 전투가 끊겨 보입니다."),
            new GUIContent("공격 정지", "공격자 애니메이션을 타격 순간 잠시 멈추는 시간(초)입니다. 원거리 행은 보통 0을 사용합니다."),
            new GUIContent("뒤 반동", "피격 모델이 공격 반대 방향으로 튕기는 최대 거리(m)입니다. 화면 표현만 움직이며 실제 전투 좌표는 바뀌지 않습니다."),
            new GUIContent("상승", "피격 모델이 위로 뜨는 최대 높이(m)입니다. 높일수록 에어본처럼 보입니다."),
            new GUIContent("반응 시간", "뒤 반동과 상승이 진행된 뒤 원위치로 돌아오는 총 시간(초)입니다."),
            new GUIContent("공격 전진", "근접 공격자 모델이 대상을 향해 순간적으로 전진하는 최대 거리(m)입니다. 실제 전투 좌표는 바뀌지 않습니다."),
            new GUIContent("전진 시간", "공격자 모델이 전진했다가 원위치로 돌아오는 총 시간(초)입니다."),
            new GUIContent("카메라", "플레이어 진영의 적중 시 전달하는 카메라 흔들림 강도입니다. 0이면 흔들지 않습니다.")
        };

        private static readonly GUIContent[] AiColumns =
        {
            new GUIContent("몬스터 ID", "런타임 몬스터 ID와 정확히 일치해야 이 행의 AI가 적용됩니다."),
            new GUIContent("역할", "전투 역할입니다. 역할에 따라 같은 적에게 몰리는 정도를 보정하는 분산 가중치도 함께 결정됩니다."),
            new GUIContent("대상 우선", "Nearest=가까운 적, LowestHealth=체력이 가장 낮은 적, RangedFirst=원거리 적을 우선합니다."),
            new GUIContent("희망 사거리 비율", "자기 최종 공격 사거리에 곱하는 비율입니다. 0.8이면 공격 사거리의 80% 지점까지 접근한 뒤 공격합니다."),
            new GUIContent("후퇴 비율", "적과의 거리가 '공격 사거리 × 이 비율'보다 짧아지면 뒤로 물러납니다. 0이면 후퇴하지 않습니다."),
            new GUIContent("재탐색 초", "대상 탐색 판단의 최소 간격(초)입니다. 낮을수록 반응은 빠르지만 방향 전환이 잦아질 수 있습니다.")
        };

        private CombatTuningConfig config;
        private MainBattleAIProfileCatalog aiCatalog;
        private SerializedObject serializedConfig;
        private SerializedObject serializedAI;
        private Vector2 scroll;
        private string statusMessage = "값을 바꾼 뒤 저장하면 다음 전투 Run부터 적용됩니다.";
        private MessageType statusType = MessageType.Info;

        [MenuItem(MenuPath)]
        public static void OpenWindow()
        {
            var window = GetWindow<CombatTuningTableWindow>();
            window.titleContent = new GUIContent("전투 튜닝 테이블");
            window.minSize = new Vector2(1120f, 620f);
            window.Show();
        }

        public static CombatTuningConfig EnsureProjectSetup()
        {
            EnsureFolder("Assets/ProjectMT/02_Shared/Combat/Data");
            var tuning = AssetDatabase.LoadAssetAtPath<CombatTuningConfig>(ConfigPath);
            if (tuning == null)
            {
                tuning = CreateInstance<CombatTuningConfig>();
                tuning.ResetToDefaults();
                AssetDatabase.CreateAsset(tuning, ConfigPath);
            }

            var projectConfig = AssetDatabase.LoadAssetAtPath<ProjectConfig>(ProjectConfigPath);
            if (projectConfig != null && projectConfig.CombatTuningConfig != tuning)
            {
                Undo.RecordObject(projectConfig, "전투 튜닝 자산 연결");
                projectConfig.EditorConfigureCombatTuning(tuning);
                EditorUtility.SetDirty(projectConfig);
            }

            EditorUtility.SetDirty(tuning);
            AssetDatabase.SaveAssets();
            return tuning;
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("전투 튜닝 테이블");
            minSize = new Vector2(1120f, 620f);
            ReloadAssets();
        }

        private void OnGUI()
        {
            DrawTitle();
            if (config == null || serializedConfig == null)
            {
                EditorGUILayout.HelpBox("CombatTuningConfig를 만들거나 불러오지 못했습니다.", MessageType.Error);
                if (GUILayout.Button("다시 불러오기", GUILayout.Height(28f)))
                {
                    ReloadAssets();
                }

                return;
            }

            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Play 중에는 표를 수정하지 않습니다. Play를 종료한 뒤 값을 저장하세요.",
                    MessageType.Warning);
            }

            serializedConfig.Update();
            serializedAI?.Update();
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
            EditorGUI.BeginChangeCheck();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawImpactTable();
            EditorGUILayout.Space(10f);
            DrawReactionTable();
            EditorGUILayout.Space(10f);
            DrawMainBattleRangeTable();
            EditorGUILayout.Space(10f);
            DrawFiveMonsterAiTable();
            EditorGUILayout.EndScrollView();
            if (EditorGUI.EndChangeCheck())
            {
                serializedConfig.ApplyModifiedProperties();
                serializedAI?.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
                if (aiCatalog != null)
                {
                    EditorUtility.SetDirty(aiCatalog);
                }

                statusMessage = "저장 전 변경사항이 있습니다.";
                statusType = MessageType.Warning;
            }

            EditorGUI.EndDisabledGroup();
            DrawBottomToolbar();
        }

        private void DrawTitle()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("공용 전투 튜닝 테이블", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "타격감은 모든 CombatFeedbackPlayer, 사거리·일반 적 AI는 MainBattle Spawn에 적용됩니다.",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                "각 제목과 입력값에 마우스를 올리면 단위·증감 효과·적용 범위를 확인할 수 있습니다.",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(6f);
        }

        private void DrawImpactTable()
        {
            DrawSectionTitle("1. 공용 타격감");
            DrawImpactHeader();
            DrawImpactRow("근접 Light", "meleeLight");
            DrawImpactRow("근접 Standard", "meleeStandard");
            DrawImpactRow("근접 Heavy", "meleeHeavy");
            DrawImpactRow("원거리 Light", "rangedLight");
            DrawImpactRow("원거리 Standard", "rangedStandard");
            DrawImpactRow("원거리 Heavy", "rangedHeavy");
            EditorGUILayout.HelpBox(
                "평타 반동·상승·전진은 Visual/VisualRoot만 움직이며 논리 좌표와 사거리를 바꾸지 않습니다.",
                MessageType.None);
        }

        private static void DrawImpactHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            for (var index = 0; index < ImpactColumns.Length; index++)
            {
                var width = index == 0 ? 120f : 96f;
                GUILayout.Label(ImpactColumns[index], EditorStyles.miniBoldLabel, GUILayout.Width(width));
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawImpactRow(string label, string propertyName)
        {
            var row = serializedConfig.FindProperty(propertyName);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(
                new GUIContent(label, ResolveImpactRowTooltip(label)),
                GUILayout.Width(120f));
            DrawNumber(row, "targetHitStop");
            DrawNumber(row, "attackerHitStop");
            DrawNumber(row, "recoilDistance");
            DrawNumber(row, "recoilHeight");
            DrawNumber(row, "recoilDuration");
            DrawNumber(row, "attackerLungeDistance");
            DrawNumber(row, "attackerLungeDuration");
            DrawNumber(row, "cameraImpulse");
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawNumber(SerializedProperty parent, string relativeName)
        {
            var property = parent?.FindPropertyRelative(relativeName);
            if (property == null)
            {
                GUILayout.Label("-", GUILayout.Width(96f));
                return;
            }

            EditorGUILayout.PropertyField(property, GUIContent.none, GUILayout.Width(96f));
            DrawTooltipOverlay(GUILayoutUtility.GetLastRect(), ResolveImpactValueTooltip(relativeName));
        }

        private void DrawReactionTable()
        {
            DrawSectionTitle("2. 피격 체급·치명/처치 강조");
            EditorGUILayout.BeginHorizontal();
            DrawLabeledProperty("가벼움 거리", "lightReactionDistanceMultiplier");
            DrawLabeledProperty("보통 거리", "standardReactionDistanceMultiplier");
            DrawLabeledProperty("무거움 거리", "heavyReactionDistanceMultiplier");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            DrawLabeledProperty("가벼움 시간", "lightReactionDurationMultiplier");
            DrawLabeledProperty("보통 시간", "standardReactionDurationMultiplier");
            DrawLabeledProperty("무거움 시간", "heavyReactionDurationMultiplier");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            DrawLabeledProperty("치명/처치 강도", "criticalOrKillEmphasis");
            DrawLabeledProperty("치명/처치 정지", "criticalOrKillTargetStopMultiplier");
            DrawLabeledProperty("Hit Stop 상한", "maximumHitStop");
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMainBattleRangeTable()
        {
            DrawSectionTitle("3. MainBattle 실제 넉백·실시간 간격·일반 적 거리 AI");
            EditorGUILayout.LabelField("실시간 전투 간격", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawLabeledProperty("적 스폰 진형 배율", "mainBattleEnemySpawnSpreadMultiplier");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            DrawLabeledProperty("아군끼리 간격", "mainBattlePlayerPairDistance");
            DrawLabeledProperty("적끼리 간격", "mainBattleEnemyPairDistance");
            DrawLabeledProperty("적대 접촉 간격", "mainBattleOpposingPairDistance");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            DrawLabeledProperty("쌍 분리 속도", "mainBattlePairSeparationSpeed");
            DrawLabeledProperty("유닛 보정 상한", "mainBattleUnitCorrectionSpeed");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("실제 피격 넉백", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawLabeledProperty("실제 넉백 거리 배율", "mainBattleActualKnockbackDistanceMultiplier");
            DrawLabeledProperty("실제 넉백 거리 상한", "mainBattleActualKnockbackMaxDistance");
            DrawLabeledProperty("실제 넉백 시간 배율", "mainBattleActualKnockbackDurationMultiplier");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            DrawLabeledProperty("약공 후 경직", "mainBattleLightPostKnockbackStagger");
            DrawLabeledProperty("중간공격 후 경직", "mainBattleStandardPostKnockbackStagger");
            DrawLabeledProperty("강공 후 경직", "mainBattleHeavyPostKnockbackStagger");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("일반 적", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawLabeledProperty("근접 희망 비율", "enemyMeleePreferredRangeRatio");
            DrawLabeledProperty("근접 후퇴 비율", "enemyMeleeRetreatRangeRatio");
            DrawLabeledProperty("근접 재탐색", "enemyMeleeRetargetInterval");
            DrawLabeledProperty("근접 분산", "enemyMeleeTargetLoadPenalty");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            DrawLabeledProperty("원거리 희망 비율", "enemyRangedPreferredRangeRatio");
            DrawLabeledProperty("원거리 후퇴 비율", "enemyRangedRetreatRangeRatio");
            DrawLabeledProperty("원거리 재탐색", "enemyRangedRetargetInterval");
            DrawLabeledProperty("원거리 분산", "enemyRangedTargetLoadPenalty");
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFiveMonsterAiTable()
        {
            DrawSectionTitle("4. 현재 5마리 역할 AI");
            if (serializedAI == null)
            {
                EditorGUILayout.HelpBox("MainBattleAIProfileCatalog를 찾지 못했습니다.", MessageType.Warning);
                return;
            }

            var profiles = serializedAI.FindProperty("profiles");
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(AiColumns[0], EditorStyles.miniBoldLabel, GUILayout.Width(150f));
            GUILayout.Label(AiColumns[1], EditorStyles.miniBoldLabel, GUILayout.Width(130f));
            GUILayout.Label(AiColumns[2], EditorStyles.miniBoldLabel, GUILayout.Width(150f));
            GUILayout.Label(AiColumns[3], EditorStyles.miniBoldLabel, GUILayout.Width(140f));
            GUILayout.Label(AiColumns[4], EditorStyles.miniBoldLabel, GUILayout.Width(110f));
            GUILayout.Label(AiColumns[5], EditorStyles.miniBoldLabel, GUILayout.Width(110f));
            EditorGUILayout.EndHorizontal();
            for (var index = 0; index < profiles.arraySize; index++)
            {
                var profile = profiles.GetArrayElementAtIndex(index);
                EditorGUILayout.BeginHorizontal();
                DrawAiCell(profile, "monsterId", 150f);
                DrawAiCell(profile, "role", 130f);
                DrawAiCell(profile, "targetPriority", 150f);
                DrawAiCell(profile, "preferredRangeRatio", 140f);
                DrawAiCell(profile, "retreatRangeRatio", 110f);
                DrawAiCell(profile, "retargetInterval", 110f);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawLabeledProperty(string label, string propertyName)
        {
            var property = serializedConfig.FindProperty(propertyName);
            var tooltip = ResolveConfigValueTooltip(propertyName);
            EditorGUILayout.PropertyField(
                property,
                new GUIContent(label, tooltip),
                GUILayout.MinWidth(260f));
            DrawTooltipOverlay(GUILayoutUtility.GetLastRect(), tooltip);
        }

        private static void DrawAiCell(SerializedProperty parent, string relativeName, float width)
        {
            var tooltip = ResolveAiValueTooltip(relativeName);
            EditorGUILayout.PropertyField(
                parent.FindPropertyRelative(relativeName),
                GUIContent.none,
                GUILayout.Width(width));
            DrawTooltipOverlay(GUILayoutUtility.GetLastRect(), tooltip);
        }

        private static void DrawTooltipOverlay(Rect rect, string tooltip)
        {
            if (!string.IsNullOrWhiteSpace(tooltip))
            {
                GUI.Label(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none);
            }
        }

        private static string ResolveImpactRowTooltip(string label)
        {
            var attackType = label.StartsWith("근접")
                ? "근접 공격"
                : "원거리 명중";
            var strength = label.EndsWith("Light")
                ? "가벼운 평타·약한 타격"
                : label.EndsWith("Heavy")
                    ? "강화 평타·강한 타격"
                    : "일반 평타·기본 타격";
            return $"{attackType}의 {strength}에 사용하는 기본 행입니다. 대상의 체급 배율과 치명타·처치 강조는 이 값에 추가로 곱해집니다.";
        }

        private static string ResolveImpactValueTooltip(string propertyName)
        {
            return propertyName switch
            {
                "targetHitStop" => "피격 대상 애니메이션 정지 시간(초). 높일수록 묵직하지만 과하면 다수전이 끊겨 보입니다.",
                "attackerHitStop" => "공격자 애니메이션 정지 시간(초). 타격 순간 자세를 잠깐 고정합니다.",
                "recoilDistance" => "피격 반응의 기준 거리(m). MainBattle에서는 실제 XZ 넉백 배율·상한의 원본이며, 실제 넉백을 쓰지 않는 콘텐츠는 모델 뒤 반동으로 표시합니다.",
                "recoilHeight" => "피격 모델이 위로 뜨는 최대 높이(m). 높일수록 에어본 느낌이 강해집니다.",
                "recoilDuration" => "피격 반응 기준 시간(초). MainBattle 실제 넉백 시간과 모델 Y축 에어본 곡선의 원본입니다.",
                "attackerLungeDistance" => "근접 공격자 모델의 순간 전진 거리(m). 0이면 전진하지 않으며 실제 전투 좌표는 변하지 않습니다.",
                "attackerLungeDuration" => "공격자 모델이 전진했다가 원위치로 돌아오는 총 시간(초).",
                "cameraImpulse" => "플레이어 진영 공격이 적중했을 때의 카메라 흔들림 강도. 0이면 흔들지 않습니다.",
                _ => string.Empty
            };
        }

        private static string ResolveConfigValueTooltip(string propertyName)
        {
            return propertyName switch
            {
                "lightReactionDistanceMultiplier" => "피격 대상 체급이 Light일 때 뒤 반동·상승 거리에 곱하는 값. 1보다 크면 더 크게 튕깁니다.",
                "standardReactionDistanceMultiplier" => "피격 대상 체급이 Standard일 때 뒤 반동·상승 거리에 곱하는 값. 1이면 기본 행 그대로입니다.",
                "heavyReactionDistanceMultiplier" => "피격 대상 체급이 Heavy일 때 뒤 반동·상승 거리에 곱하는 값. 1보다 작으면 덜 튕깁니다.",
                "lightReactionDurationMultiplier" => "피격 대상 체급이 Light일 때 반응 시간에 곱하는 값. 낮을수록 빠르게 튕겼다가 돌아옵니다.",
                "standardReactionDurationMultiplier" => "피격 대상 체급이 Standard일 때 반응 시간에 곱하는 값.",
                "heavyReactionDurationMultiplier" => "피격 대상 체급이 Heavy일 때 반응 시간에 곱하는 값. 높을수록 무겁고 느리게 반응합니다.",
                "criticalOrKillEmphasis" => "치명타 또는 처치 시 뒤 반동·상승·공격 전진·카메라 강도에 곱하는 강조 배율입니다.",
                "criticalOrKillTargetStopMultiplier" => "치명타 또는 처치 시 피격 정지 시간에 곱하는 배율입니다. Hit Stop 상한을 넘지는 않습니다.",
                "maximumHitStop" => "치명타·처치 피격 정지의 절대 상한(초)입니다. 다수전이 지나치게 멈추는 것을 막습니다.",
                "mainBattleEnemySpawnSpreadMultiplier" => "MainBattle 적이 입장할 때 진형 간격에 추가로 곱하는 값. 현재 1.8이면 기본 프로필을 포함해 이웃 적이 약 1.99m 간격으로 떨어져 등장합니다. 전투 중 밀어내는 값이 아닙니다.",
                "mainBattlePlayerPairDistance" => "살아 있는 아군 유닛 루트끼리 유지하려는 최소 간격(m). 높일수록 아군 대형이 넓게 퍼지고 모델 겹침이 줄어듭니다.",
                "mainBattleEnemyPairDistance" => "살아 있는 적 유닛 루트끼리 유지하려는 최소 간격(m). 높일수록 적 무리가 한 점에 포개지지 않습니다.",
                "mainBattleOpposingPairDistance" => "아군과 적이 교전할 때 유지하려는 접촉 간격(m). 실제 MainBattle 근접 사거리보다 낮게 두면 공격은 유지하면서 몸체 겹침을 줄일 수 있습니다.",
                "mainBattlePairSeparationSpeed" => "겹친 유닛 한 쌍을 서로 벌리는 최대 속도(m/s). 높일수록 빠르게 풀리지만 과하면 전투 대형이 튀어 보일 수 있습니다.",
                "mainBattleUnitCorrectionSpeed" => "한 유닛이 한 프레임에 받는 전체 간격 보정의 속도 상한(m/s). 여러 유닛이 동시에 붙었을 때 과도하게 밀리는 것을 제한합니다.",
                "mainBattleActualKnockbackDistanceMultiplier" => "공용 피격 기준 거리에 곱하는 MainBattle 실제 XZ 넉백 배율. 0이면 실제 좌표를 밀지 않고 기존 모델 반동을 사용합니다.",
                "mainBattleActualKnockbackMaxDistance" => "한 번의 일반 피격으로 실제 Unit 루트가 이동할 수 있는 최대 거리(m). 강한 공격과 가벼운 체급의 과도한 밀림을 제한합니다.",
                "mainBattleActualKnockbackDurationMultiplier" => "공용 피격 기준 시간에 곱하는 실제 넉백 시간 배율. 앞 65%에 이동하고 뒤 35%는 멈추므로 낮을수록 더 짧게 퍽 밀립니다.",
                "mainBattleLightPostKnockbackStagger" => "빠르고 약한 Light 공격에 밀린 뒤 추가로 행동을 멈추는 시간(초). 현재 0.06초입니다.",
                "mainBattleStandardPostKnockbackStagger" => "중간 Standard 공격에 밀린 뒤 추가로 행동을 멈추는 시간(초). 현재 0.10초입니다.",
                "mainBattleHeavyPostKnockbackStagger" => "느리고 강한 Heavy 공격에 밀린 뒤 추가로 행동을 멈추는 시간(초). 현재 0.15초입니다.",
                "enemyMeleePreferredRangeRatio" => "개별 5마리 프로필이 없는 근접 적의 희망 거리 비율. 최종 공격 사거리 × 이 값까지 접근합니다.",
                "enemyMeleeRetreatRangeRatio" => "일반 근접 적의 후퇴 시작 비율. 공격 사거리 × 이 값보다 가까우면 물러납니다. 0이면 후퇴하지 않습니다.",
                "enemyMeleeRetargetInterval" => "일반 근접 적의 대상 탐색 판단 최소 간격(초). 낮을수록 반응이 빠르지만 전환이 잦아질 수 있습니다.",
                "enemyMeleeTargetLoadPenalty" => "이미 여러 아군이 노리는 대상에 추가하는 선택 페널티. 높일수록 서로 다른 적에게 분산됩니다.",
                "enemyRangedPreferredRangeRatio" => "개별 5마리 프로필이 없는 원거리 적의 희망 거리 비율. 최종 공격 사거리 × 이 값까지 접근합니다.",
                "enemyRangedRetreatRangeRatio" => "일반 원거리 적의 후퇴 시작 비율. 적이 이 거리 안으로 들어오면 거리를 벌립니다. 0이면 후퇴하지 않습니다.",
                "enemyRangedRetargetInterval" => "일반 원거리 적의 대상 탐색 판단 최소 간격(초).",
                "enemyRangedTargetLoadPenalty" => "이미 여러 아군이 노리는 대상에 추가하는 선택 페널티. 높일수록 공격 대상이 분산됩니다.",
                _ => string.Empty
            };
        }

        private static string ResolveAiValueTooltip(string propertyName)
        {
            return propertyName switch
            {
                "monsterId" => "이 AI 행을 적용할 런타임 몬스터 ID. 편성 데이터의 ID와 정확히 같아야 합니다.",
                "role" => "Vanguard=선봉, Guardian=수호, Finisher=마무리, Marksman=사수, BacklineHunter=후열 추적 역할입니다.",
                "targetPriority" => "Nearest=가까운 적, LowestHealth=체력이 낮은 적, RangedFirst=원거리 적 우선.",
                "preferredRangeRatio" => "최종 공격 사거리에 곱하는 희망 거리 비율. 0.8이면 공격 사거리의 80%까지 접근합니다.",
                "retreatRangeRatio" => "적과의 거리가 공격 사거리 × 이 값보다 짧아지면 후퇴합니다. 반드시 희망 비율보다 작아야 하며 0이면 후퇴하지 않습니다.",
                "retargetInterval" => "대상 탐색 판단의 최소 간격(초). 낮을수록 빠르게 대응하지만 방향 전환이 잦아질 수 있습니다.",
                _ => string.Empty
            };
        }

        private void DrawBottomToolbar()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(statusMessage, statusType);
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
            if (GUILayout.Button("변경사항 저장", GUILayout.Height(30f)))
            {
                SaveTuningChanges();
            }

            if (GUILayout.Button("실행 취소", GUILayout.Height(30f)))
            {
                Undo.PerformUndo();
                ReloadSerializedObjects();
                statusMessage = "마지막 표 변경을 취소했습니다.";
                statusType = MessageType.Info;
            }

            if (GUILayout.Button("기본값 복원", GUILayout.Height(30f)) &&
                EditorUtility.DisplayDialog(
                    "전투 튜닝 기본값 복원",
                    "공용 타격감·MainBattle 거리 값을 현재 프로젝트 기본값으로 되돌릴까요?\n5마리 AI 표는 유지합니다.",
                    "복원",
                    "취소"))
            {
                Undo.RecordObject(config, "전투 튜닝 기본값 복원");
                config.ResetToDefaults();
                EditorUtility.SetDirty(config);
                ReloadSerializedObjects();
                statusMessage = "기본값을 복원했습니다. 저장 버튼으로 확정하세요.";
                statusType = MessageType.Warning;
            }

            if (GUILayout.Button("검증", GUILayout.Height(30f)))
            {
                ValidateCurrent();
            }

            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("자산 선택", GUILayout.Height(30f)))
            {
                Selection.activeObject = config;
                EditorGUIUtility.PingObject(config);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void SaveTuningChanges()
        {
            serializedConfig.ApplyModifiedProperties();
            serializedAI?.ApplyModifiedProperties();
            if (!config.TryValidate(out var error))
            {
                statusMessage = $"저장 차단: {error}";
                statusType = MessageType.Error;
                return;
            }

            if (aiCatalog != null && !aiCatalog.TryValidate(out error))
            {
                statusMessage = $"5마리 AI 저장 차단: {error}";
                statusType = MessageType.Error;
                return;
            }

            EditorUtility.SetDirty(config);
            if (aiCatalog != null)
            {
                EditorUtility.SetDirty(aiCatalog);
            }

            AssetDatabase.SaveAssets();
            CombatImpactTuning.Configure(config);
            statusMessage = "전투 튜닝 자산을 저장했습니다. 다음 Run부터 적용됩니다.";
            statusType = MessageType.Info;
            Debug.Log("[Combat Tuning] 표 저장·검증 완료");
        }

        private void ValidateCurrent()
        {
            serializedConfig.ApplyModifiedProperties();
            serializedAI?.ApplyModifiedProperties();
            if (!config.TryValidate(out var error))
            {
                statusMessage = error;
                statusType = MessageType.Error;
                return;
            }

            if (aiCatalog != null && !aiCatalog.TryValidate(out error))
            {
                statusMessage = error;
                statusType = MessageType.Error;
                return;
            }

            var projectConfig = AssetDatabase.LoadAssetAtPath<ProjectConfig>(ProjectConfigPath);
            if (projectConfig == null || projectConfig.CombatTuningConfig != config)
            {
                statusMessage = "ProjectConfig의 CombatTuningConfig 연결이 올바르지 않습니다.";
                statusType = MessageType.Error;
                return;
            }

            statusMessage = "공용 타격감·MainBattle 거리·5마리 AI 표 검증을 통과했습니다.";
            statusType = MessageType.Info;
        }

        private void ReloadAssets()
        {
            config = EnsureProjectSetup();
            aiCatalog = AssetDatabase.LoadAssetAtPath<MainBattleAIProfileCatalog>(AIProfilePath);
            ReloadSerializedObjects();
        }

        private void ReloadSerializedObjects()
        {
            serializedConfig = config == null ? null : new SerializedObject(config);
            serializedAI = aiCatalog == null ? null : new SerializedObject(aiCatalog);
            Repaint();
        }

        private static void DrawSectionTitle(string title)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private static void EnsureFolder(string folderPath)
        {
            var parts = folderPath.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = $"{current}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }
    }
}
