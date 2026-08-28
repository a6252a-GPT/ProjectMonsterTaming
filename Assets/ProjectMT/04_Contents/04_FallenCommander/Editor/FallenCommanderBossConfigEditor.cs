using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander.Editor
{
    [CustomEditor(typeof(FallenCommanderBossConfig))]
    public sealed class FallenCommanderBossConfigEditor : UnityEditor.Editor
    {
        private const string SelectedAttackKey =
            "ProjectMT.FallenCommander.BossConfig.SelectedAttack";
        private const string ShowAllAttacksKey =
            "ProjectMT.FallenCommander.BossConfig.ShowAllAttacks";
        private const string FinalChargeExpandedKey =
            "ProjectMT.FallenCommander.BossConfig.FinalChargeExpanded";

        private sealed class AttackInspectorDefinition
        {
            public AttackInspectorDefinition(
                string label,
                FallenCommanderAttackPreviewKind previewKind,
                string[] propertyNames,
                System.Func<FallenCommanderBossConfig, FallenCommanderAttackData> resolveAttack = null)
            {
                Label = label;
                PreviewKind = previewKind;
                PropertyNames = propertyNames;
                ResolveAttack = resolveAttack;
            }

            public string Label { get; }
            public FallenCommanderAttackPreviewKind PreviewKind { get; }
            public string[] PropertyNames { get; }
            private System.Func<FallenCommanderBossConfig, FallenCommanderAttackData> ResolveAttack { get; }

            // 정의에 연결된 일반 공격 데이터를 반환한다.
            public FallenCommanderAttackData ResolveAttackData(FallenCommanderBossConfig config)
            {
                return ResolveAttack?.Invoke(config);
            }
        }

        private static readonly AttackInspectorDefinition[] AttackDefinitions =
        {
            new(
                "1. 기본",
                FallenCommanderAttackPreviewKind.Basic,
                new[] { "projectileBasicAttack" }),
            new(
                "2. 근접",
                FallenCommanderAttackPreviewKind.Melee,
                new[] { "meleeAttack" },
                config => config.MeleeAttack),
            new(
                "3. 위치",
                FallenCommanderAttackPreviewKind.MarkStrike,
                new[] { "markStrike" },
                config => config.MarkStrike),
            new(
                "4. 추적",
                FallenCommanderAttackPreviewKind.TrackingMark,
                new[] { "trackingMark", "trackingMarkLockDuration" },
                config => config.TrackingMark),
            new(
                "5. 블랙홀",
                FallenCommanderAttackPreviewKind.BlackHole,
                new[]
                {
                    "blackHole",
                    "blackHoleActiveDuration",
                    "blackHoleCoreRadius",
                    "blackHoleSpawnMinDistance",
                    "blackHoleSpawnMaxDistance",
                    "blackHoleOuterPullSpeed",
                    "blackHoleInnerPullSpeed",
                    "blackHolePullStrengthCurve",
                    "blackHoleArenaHalfExtents",
                    "blackHoleEndEffects"
                },
                config => config.BlackHole),
            new(
                "6. 직선",
                FallenCommanderAttackPreviewKind.LineStrike,
                new[] { "lineStrike" },
                config => config.LineStrike),
            new(
                "7. 고리",
                FallenCommanderAttackPreviewKind.CorruptionRing,
                new[] { "corruptionRing", "corruptionRingSafeRadius" },
                config => config.CorruptionRing),
            new(
                "8. 충전",
                FallenCommanderAttackPreviewKind.FinalCharge,
                new[]
                {
                    "finalChargeHealthRatio",
                    "finalChargeDuration",
                    "finalChargeTelegraphPrefab",
                    "finalChargeTelegraphHoldDuration",
                    "finalChargeRadius",
                    "finalChargeEffects",
                    "finalChargePreCastMotion",
                    "finalChargePreCastMotionSpeed",
                    "finalChargePreCastMotionStart",
                    "finalChargePreCastMotionEnd",
                    "finalChargeCastMotion",
                    "finalChargeCastMotionSpeed",
                    "finalChargeCastMotionStart",
                    "finalChargeCastMotionEnd",
                    "finalChargeStartEffectOffset"
                }),
            new(
                "9. 전멸",
                FallenCommanderAttackPreviewKind.TimeoutWipe,
                new[] { "timeoutWipe" })
        };

        private int selectedAttack;
        private bool showAllAttacks;
        private bool finalChargeExpanded;

        // 마지막으로 선택한 공격 탭과 전체 보기 상태를 불러온다.
        private void OnEnable()
        {
            selectedAttack = Mathf.Clamp(
                EditorPrefs.GetInt(SelectedAttackKey, 0),
                0,
                AttackDefinitions.Length - 1);
            showAllAttacks = EditorPrefs.GetBool(ShowAllAttacksKey, false);
            finalChargeExpanded = EditorPrefs.GetBool(FinalChargeExpandedKey, true);
        }

        // 공통 데이터와 선택된 공격 탭의 데이터만 순서대로 표시한다.
        public override void OnInspectorGUI()
        {
            if (!serializedObject.hasModifiedProperties)
            {
                serializedObject.UpdateIfRequiredOrScript();
            }

            DrawChangeControls();
            var property = serializedObject.GetIterator();
            var enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "projectileBasicAttack")
                {
                    DrawAttackTabs();
                    continue;
                }

                if (IsAttackProperty(property.propertyPath))
                {
                    continue;
                }

                using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                {
                    EditorGUILayout.PropertyField(
                        property,
                        FallenCommanderInspectorLabels.BossConfig(property),
                        true);
                }
            }

            if (serializedObject.hasModifiedProperties)
            {
                Repaint();
            }
        }

        // 인스펙터 변경사항을 명시적으로 적용하거나 마지막 저장 상태로 되돌린다.
        private void DrawChangeControls()
        {
            EditorGUILayout.Space(2f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("변경 관리", EditorStyles.boldLabel);
                if (!serializedObject.hasModifiedProperties)
                {
                    EditorGUILayout.LabelField(
                        "현재 모든 값이 적용된 상태입니다.",
                        EditorStyles.miniLabel);
                    return;
                }

                EditorGUILayout.HelpBox(
                    "아직 적용되지 않은 변경사항이 있습니다. 적용 전에는 미리보기를 실행할 수 없습니다.",
                    MessageType.Warning);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("변경 적용"))
                    {
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(target);
                        AssetDatabase.SaveAssetIfDirty(target);
                        serializedObject.Update();
                        GUI.FocusControl(null);
                    }

                    if (GUILayout.Button("변경 취소"))
                    {
                        serializedObject.Update();
                        GUI.FocusControl(null);
                    }
                }
            }
        }

        // 두 줄 공격 버튼과 전체 보기 전환 버튼을 표시한다.
        private void DrawAttackTabs()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("공격 설정", EditorStyles.boldLabel);
                var nextShowAll = GUILayout.Toggle(
                    showAllAttacks,
                    "전체 보기",
                    EditorStyles.miniButton,
                    GUILayout.Width(78f));
                if (nextShowAll != showAllAttacks)
                {
                    showAllAttacks = nextShowAll;
                    EditorPrefs.SetBool(ShowAllAttacksKey, showAllAttacks);
                }
            }

            const int columnCount = 3;
            var rowCount = Mathf.CeilToInt(AttackDefinitions.Length / (float)columnCount);
            for (var row = 0; row < rowCount; row++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (var column = 0; column < columnCount; column++)
                    {
                        var index = row * columnCount + column;
                        if (index >= AttackDefinitions.Length)
                        {
                            break;
                        }

                        var selected = GUILayout.Toggle(
                            selectedAttack == index,
                            AttackDefinitions[index].Label,
                            EditorStyles.miniButton);
                        if (selected && selectedAttack != index)
                        {
                            selectedAttack = index;
                            EditorPrefs.SetInt(SelectedAttackKey, selectedAttack);
                            GUI.FocusControl(null);
                        }
                    }
                }
            }

            EditorGUILayout.Space(4f);
            if (showAllAttacks)
            {
                for (var index = 0; index < AttackDefinitions.Length; index++)
                {
                    DrawAttackProperties(
                        AttackDefinitions[index],
                        index < AttackDefinitions.Length - 1);
                }

                return;
            }

            DrawAttackProperties(AttackDefinitions[selectedAttack], false);
        }

        // 선택된 공격에 포함된 SerializedProperty와 전용 도구를 표시한다.
        private void DrawAttackProperties(
            AttackInspectorDefinition definition,
            bool addBottomSpacing)
        {
            if (definition.PreviewKind == FallenCommanderAttackPreviewKind.FinalCharge)
            {
                DrawFinalChargeProperties(definition);
            }
            else
            {
                foreach (var propertyName in definition.PropertyNames)
                {
                    if (definition.PreviewKind == FallenCommanderAttackPreviewKind.BlackHole &&
                        propertyName == "blackHoleEndEffects")
                    {
                        continue;
                    }

                    var attackProperty = serializedObject.FindProperty(propertyName);
                    if (attackProperty != null)
                    {
                        EditorGUILayout.PropertyField(
                            attackProperty,
                            FallenCommanderInspectorLabels.BossConfig(attackProperty),
                            true);
                    }
                }
            }

            DrawValidationWarnings(definition);
            using (new EditorGUI.DisabledScope(serializedObject.hasModifiedProperties))
            {
                DrawAttackPreviewTools(
                    (FallenCommanderBossConfig)target,
                    definition);
            }

            if (showAllAttacks && addBottomSpacing)
            {
                EditorGUILayout.Space(6f);
            }
        }

        // 충전 광역기의 분산된 설정을 다른 공격과 같은 하나의 접이식 영역에 표시한다.
        private void DrawFinalChargeProperties(AttackInspectorDefinition definition)
        {
            var nextExpanded = EditorGUILayout.Foldout(
                finalChargeExpanded,
                "충전 광역기 설정",
                true);
            if (nextExpanded != finalChargeExpanded)
            {
                finalChargeExpanded = nextExpanded;
                EditorPrefs.SetBool(FinalChargeExpandedKey, finalChargeExpanded);
            }

            if (!finalChargeExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var propertyName in definition.PropertyNames)
                {
                    var property = serializedObject.FindProperty(propertyName);
                    if (property == null)
                    {
                        continue;
                    }

                    EditorGUILayout.PropertyField(
                        property,
                        FallenCommanderInspectorLabels.BossConfig(property),
                        true);

                    if (propertyName == "finalChargeHealthRatio")
                    {
                        var maxHealth = serializedObject.FindProperty("baseMaxHealth")
                            ?.floatValue ?? 1f;
                        DrawFinalChargeHealthSummary(property.floatValue, maxHealth);
                    }
                }
            }
        }

        // 충전 광역기의 발동 비율을 퍼센트와 설정된 기본 최대 체력 기준 수치로 안내한다.
        private static void DrawFinalChargeHealthSummary(
            float healthRatio,
            float baseMaxHealth)
        {
            var clampedRatio = Mathf.Clamp01(healthRatio);
            var percent = clampedRatio * 100f;
            var resolvedMaxHealth = Mathf.Max(1f, baseMaxHealth);
            var triggerHealth = resolvedMaxHealth * clampedRatio;
            EditorGUILayout.HelpBox(
                $"현재 설정: 체력 {percent:0.#}% 이하에서 발동\n" +
                $"기본 최대 체력 {resolvedMaxHealth:N0} 기준 남은 체력 {triggerHealth:N0} 이하",
                MessageType.Info);
        }

        // 선택한 공격의 직렬화 값을 검사해 런타임에서 보정되거나 무시될 조합을 경고한다.
        private void DrawValidationWarnings(AttackInspectorDefinition definition)
        {
            var primary = serializedObject.FindProperty(definition.PropertyNames[0]);
            if (primary == null)
            {
                return;
            }

            switch (definition.PreviewKind)
            {
                case FallenCommanderAttackPreviewKind.Basic:
                    ValidateRequiredObject(primary, "telegraphPrefab", "공격 범위 오브젝트가 비어 있습니다.");
                    ValidateEffects(primary.FindPropertyRelative("effects"));
                    break;

                case FallenCommanderAttackPreviewKind.FinalCharge:
                    ValidateRequiredObject(
                        serializedObject,
                        "finalChargeTelegraphPrefab",
                        "충전 광역기의 공격 범위 오브젝트가 비어 있습니다.");
                    ValidateEffects(serializedObject.FindProperty("finalChargeEffects"));
                    ValidateFinalChargeMotionRanges();
                    break;

                case FallenCommanderAttackPreviewKind.TimeoutWipe:
                    ValidateRequiredObject(
                        primary,
                        "telegraphPrefab",
                        "전멸기의 공격 범위 오브젝트가 비어 있습니다.");
                    ValidateMotionRanges(primary);
                    ValidateEffects(primary.FindPropertyRelative("effects"));
                    break;

                default:
                    ValidateRequiredObject(primary, "telegraphPrefab", "공격 범위 오브젝트가 비어 있습니다.");
                    ValidateMotionRanges(primary);
                    ValidateEffects(primary.FindPropertyRelative("effects"));
                    break;
            }

            ValidateAttackSpecificCombination(definition, primary);
        }

        // 공격 종류별로 서로 연관된 범위와 시간 값의 잘못된 조합을 검사한다.
        private void ValidateAttackSpecificCombination(
            AttackInspectorDefinition definition,
            SerializedProperty attack)
        {
            switch (definition.PreviewKind)
            {
                case FallenCommanderAttackPreviewKind.TrackingMark:
                {
                    var warning = attack.FindPropertyRelative("warningDuration")?.floatValue ?? 0f;
                    var lockDuration = serializedObject.FindProperty("trackingMarkLockDuration")?.floatValue ?? 0f;
                    if (lockDuration > warning)
                    {
                        DrawWarning("위치 고정시간이 경고시간보다 길어 추적이 시작 즉시 종료됩니다.");
                    }

                    break;
                }

                case FallenCommanderAttackPreviewKind.BlackHole:
                {
                    var radius = attack.FindPropertyRelative("radius")?.floatValue ?? 0f;
                    var coreRadius = serializedObject.FindProperty("blackHoleCoreRadius")?.floatValue ?? 0f;
                    var minimum = serializedObject.FindProperty("blackHoleSpawnMinDistance")?.floatValue ?? 0f;
                    var maximum = serializedObject.FindProperty("blackHoleSpawnMaxDistance")?.floatValue ?? 0f;
                    var outerPull = serializedObject.FindProperty("blackHoleOuterPullSpeed")?.floatValue ?? 0f;
                    var innerPull = serializedObject.FindProperty("blackHoleInnerPullSpeed")?.floatValue ?? 0f;

                    if (coreRadius >= radius)
                    {
                        DrawWarning("중심 피해 범위는 블랙홀 전체 반지름보다 작아야 합니다.");
                    }

                    if (minimum > maximum)
                    {
                        DrawWarning("생성 최소 거리는 생성 최대 거리보다 클 수 없습니다.");
                    }

                    if (innerPull < outerPull)
                    {
                        DrawWarning("중심부 흡입 속도는 바깥쪽 흡입 속도보다 빠르거나 같아야 합니다.");
                    }

                    ValidateEffects(serializedObject.FindProperty("blackHoleEndEffects"));
                    break;
                }

                case FallenCommanderAttackPreviewKind.LineStrike:
                {
                    var width = attack.FindPropertyRelative("width")?.floatValue ?? 0f;
                    var length = attack.FindPropertyRelative("length")?.floatValue ?? 0f;
                    if (width > length)
                    {
                        DrawWarning("직선 공격 너비가 길이보다 커서 범위가 옆으로 더 넓게 표시됩니다.");
                    }

                    break;
                }

                case FallenCommanderAttackPreviewKind.CorruptionRing:
                {
                    var outerRadius = attack.FindPropertyRelative("radius")?.floatValue ?? 0f;
                    var safeRadius = serializedObject.FindProperty("corruptionRingSafeRadius")?.floatValue ?? 0f;
                    if (safeRadius >= outerRadius)
                    {
                        DrawWarning("안전지대 반지름은 타락의 고리 전체 반지름보다 작아야 합니다.");
                    }

                    break;
                }
            }
        }

        // 시전·공격 모션의 시작 지점이 종료 지점을 넘는지 검사한다.
        private static void ValidateMotionRanges(SerializedProperty attack)
        {
            ValidateMotionRange(
                attack,
                "preCastMotion",
                "preCastMotionStart",
                "preCastMotionEnd",
                "시전 모션");
            ValidateMotionRange(
                attack,
                "castMotion",
                "castMotionStart",
                "castMotionEnd",
                "공격 모션");
        }

        // 충전 광역기의 루트 직렬화 필드에서 시전·공격 모션 구간을 검사한다.
        private void ValidateFinalChargeMotionRanges()
        {
            ValidateRootMotionRange(
                "finalChargePreCastMotion",
                "finalChargePreCastMotionStart",
                "finalChargePreCastMotionEnd",
                "시전 모션");
            ValidateRootMotionRange(
                "finalChargeCastMotion",
                "finalChargeCastMotionStart",
                "finalChargeCastMotionEnd",
                "공격 모션");
        }

        // 루트에 저장된 모션이 있을 때 시작 지점과 종료 지점의 순서를 검사한다.
        private void ValidateRootMotionRange(
            string motionName,
            string startName,
            string endName,
            string label)
        {
            var motion = serializedObject.FindProperty(motionName);
            var start = serializedObject.FindProperty(startName);
            var end = serializedObject.FindProperty(endName);
            if (motion?.objectReferenceValue != null &&
                start != null &&
                end != null &&
                start.floatValue >= end.floatValue)
            {
                DrawWarning($"{label} 시작 지점은 종료 지점보다 작아야 합니다.");
            }
        }

        // 지정된 모션이 있을 때 시작 지점과 종료 지점의 순서를 검사한다.
        private static void ValidateMotionRange(
            SerializedProperty owner,
            string motionName,
            string startName,
            string endName,
            string label)
        {
            var motion = owner.FindPropertyRelative(motionName);
            var start = owner.FindPropertyRelative(startName);
            var end = owner.FindPropertyRelative(endName);
            if (motion?.objectReferenceValue != null &&
                start != null &&
                end != null &&
                start.floatValue >= end.floatValue)
            {
                DrawWarning($"{label} 시작 지점은 종료 지점보다 작아야 합니다.");
            }
        }

        // VFX·SFX 참조와 유지시간이 서로 어긋난 설정을 검사한다.
        private static void ValidateEffects(SerializedProperty effects)
        {
            if (effects == null)
            {
                return;
            }

            ValidateDurationReference(
                effects,
                "startVfxPrefab",
                "startVfxDuration",
                "시전 효과");
            ValidateDurationReference(
                effects,
                "resolveVfxPrefab",
                "resolveVfxDuration",
                ResolveEffectLabel(effects));
            if (SupportsHitEffects(effects))
            {
                ValidateDurationReference(
                    effects,
                    "hitVfxPrefab",
                    "hitVfxDuration",
                    "적중 VFX");
            }
            ValidateAudioDuration(
                effects,
                "startSfx",
                "startSfxDuration",
                "시전 효과음");
            ValidateAudioDuration(
                effects,
                "resolveSfx",
                "resolveSfxDuration",
                $"{ResolveEffectLabel(effects)}음");
            if (SupportsHitEffects(effects))
            {
                ValidateAudioDuration(
                    effects,
                    "hitSfx",
                    "hitSfxDuration",
                    "적중 SFX");
            }
        }

        // 공격 데이터 경로를 기준으로 기존 Resolve 슬롯의 실제 역할 이름을 반환한다.
        private static string ResolveEffectLabel(SerializedProperty effects)
        {
            return effects.propertyPath switch
            {
                "projectileBasicAttack.effects" => "적중 효과",
                "blackHoleEndEffects" => "종료 효과",
                _ => "발동 효과"
            };
        }

        // 실제 별도 적중 단계가 있는 공격 연출인지 반환한다.
        private static bool SupportsHitEffects(SerializedProperty effects)
        {
            return effects.propertyPath != "projectileBasicAttack.effects" &&
                effects.propertyPath != "blackHoleEndEffects";
        }

        // 참조가 비어 있는데 유지시간만 입력된 연출 설정을 경고한다.
        private static void ValidateDurationReference(
            SerializedProperty owner,
            string referenceName,
            string durationName,
            string label)
        {
            var reference = owner.FindPropertyRelative(referenceName);
            var duration = owner.FindPropertyRelative(durationName);
            if (reference?.objectReferenceValue == null && duration?.floatValue > 0f)
            {
                DrawWarning($"{label}이 비어 있어 입력한 유지시간이 사용되지 않습니다.");
            }
        }

        // 효과음 참조와 유지시간 및 원본 클립 길이의 조합을 검사한다.
        private static void ValidateAudioDuration(
            SerializedProperty owner,
            string clipName,
            string durationName,
            string label)
        {
            var clipProperty = owner.FindPropertyRelative(clipName);
            var durationProperty = owner.FindPropertyRelative(durationName);
            var clip = clipProperty?.objectReferenceValue as AudioClip;
            var duration = durationProperty?.floatValue ?? 0f;
            if (clip == null)
            {
                if (duration > 0f)
                {
                    DrawWarning($"{label}이 비어 있어 입력한 유지시간이 사용되지 않습니다.");
                }

                return;
            }

            if (duration > clip.length + 0.001f)
            {
                DrawWarning($"{label} 유지시간이 원본 길이보다 길어 원본 길이까지만 재생됩니다.");
            }
        }

        // 필수 오브젝트 참조가 비어 있으면 오류 안내를 표시한다.
        private static void ValidateRequiredObject(
            SerializedProperty owner,
            string propertyName,
            string message)
        {
            var property = owner.FindPropertyRelative(propertyName) ??
                owner.serializedObject.FindProperty(propertyName);
            if (property?.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(message, MessageType.Error);
            }
        }

        // 최상위 필수 오브젝트 참조가 비어 있으면 오류 안내를 표시한다.
        private static void ValidateRequiredObject(
            SerializedObject owner,
            string propertyName,
            string message)
        {
            if (owner.FindProperty(propertyName)?.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(message, MessageType.Error);
            }
        }

        // 잘못된 조합을 인스펙터 경고 상자로 표시한다.
        private static void DrawWarning(string message)
        {
            EditorGUILayout.HelpBox(message, MessageType.Warning);
        }

        // 공격 탭 안에 시전·공격·전체 미리보기와 종료 버튼을 표시한다.
        private static void DrawAttackPreviewTools(
            FallenCommanderBossConfig config,
            AttackInspectorDefinition definition)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("공격 연출 미리보기", EditorStyles.boldLabel);

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                FallenCommanderAttackEditorPreview.Stop();
                EditorGUILayout.HelpBox(
                    "프리팹 편집 화면에서는 원본 보호를 위해 미리보기를 실행할 수 없습니다. " +
                    "프리팹 편집 화면을 닫고 개발용 군단장 씬에서 실행하세요.",
                    MessageType.Warning);
                return;
            }

            if (!TryBuildAttackPreviewSpec(config, definition, out var previewSpec))
            {
                EditorGUILayout.HelpBox(
                    "현재 씬에서 군단장 보스 실행 오브젝트를 찾을 수 없습니다.",
                    MessageType.Info);
                return;
            }

            var hasPreCast = HasPreCastPresentation(previewSpec);
            var hasCast = HasCastPresentation(previewSpec);
            if (!hasPreCast && !hasCast)
            {
                EditorGUILayout.HelpBox(
                    "이 공격에는 모션·시각 효과·효과음이 지정되지 않았습니다.",
                    MessageType.Info);
            }
            else if (definition.PreviewKind == FallenCommanderAttackPreviewKind.Basic)
            {
                EditorGUILayout.HelpBox(
                    "전체 미리보기는 직선 경고범위가 차오른 뒤 기본 공격 구체가 " +
                    "군단장 방향으로 이동하며, 충돌 시 적중 연출을 재생합니다.",
                    MessageType.None);
            }
            else if (definition.PreviewKind == FallenCommanderAttackPreviewKind.TimeoutWipe)
            {
                EditorGUILayout.HelpBox(
                    "전체 미리보기는 전멸 경고 종료 후 발동 모션과 적중 연출을 재생합니다. " +
                    "실제 하트 피해는 적용하지 않습니다.",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"전체 미리보기는 {previewSpec.WarningDuration:0.##}초 동안 범위가 차오르고 " +
                    $"{previewSpec.TelegraphHoldDuration:0.##}초 유지된 뒤 " +
                    "공격 모션·적중 시각 효과·효과음을 재생합니다.",
                    MessageType.None);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!hasPreCast))
                {
                    if (GUILayout.Button("▶ 시전"))
                    {
                        FallenCommanderAttackEditorPreview.Play(
                            previewSpec,
                            FallenCommanderAttackPreviewMode.PreCast);
                    }
                }

                using (new EditorGUI.DisabledScope(!hasCast))
                {
                    if (GUILayout.Button("▶ 공격"))
                    {
                        FallenCommanderAttackEditorPreview.Play(
                            previewSpec,
                            FallenCommanderAttackPreviewMode.Cast);
                    }
                }

                using (new EditorGUI.DisabledScope(!hasPreCast && !hasCast))
                {
                    if (GUILayout.Button("▶ 전체"))
                    {
                        FallenCommanderAttackEditorPreview.Play(
                            previewSpec,
                            FallenCommanderAttackPreviewMode.Full);
                    }
                }

                using (new EditorGUI.DisabledScope(
                    !FallenCommanderAttackEditorPreview.IsActive))
                {
                    if (GUILayout.Button("■ 종료"))
                    {
                        FallenCommanderAttackEditorPreview.Stop();
                    }
                }
            }
        }

        // 현재 Scene 참조와 선택된 공격 데이터를 범용 미리보기 명세로 묶는다.
        private static bool TryBuildAttackPreviewSpec(
            FallenCommanderBossConfig config,
            AttackInspectorDefinition definition,
            out FallenCommanderAttackPreviewSpec previewSpec)
        {
            previewSpec = null;
            foreach (var controller in Object.FindObjectsByType<FallenCommanderController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                var controllerData = new SerializedObject(controller);
                var bossPrefab = controllerData.FindProperty("bossPrefab")
                    ?.objectReferenceValue as GameObject;
                if (bossPrefab == null)
                {
                    continue;
                }

                var spawnPoint = controllerData.FindProperty("bossSpawnPoint")
                    ?.objectReferenceValue as Transform;
                var commanderRoot = controllerData.FindProperty("commanderRoot")
                    ?.objectReferenceValue as GameObject;
                var kind = definition.PreviewKind;
                var attack = definition.ResolveAttackData(config);
                var basicAttack = kind == FallenCommanderAttackPreviewKind.Basic
                    ? config.BasicAttack
                    : null;
                var timeoutWipe = kind == FallenCommanderAttackPreviewKind.TimeoutWipe
                    ? config.TimeoutWipe
                    : null;
                var effects = kind == FallenCommanderAttackPreviewKind.FinalCharge
                    ? config.FinalChargeEffects
                    : kind == FallenCommanderAttackPreviewKind.TimeoutWipe
                        ? timeoutWipe?.Effects
                    : kind == FallenCommanderAttackPreviewKind.Basic
                        ? basicAttack?.Effects
                        : attack?.Effects;
                var warningDuration = kind == FallenCommanderAttackPreviewKind.FinalCharge
                    ? config.FinalChargeDuration
                    : kind == FallenCommanderAttackPreviewKind.TimeoutWipe
                        ? timeoutWipe?.WarningDuration ?? 0.1f
                    : kind == FallenCommanderAttackPreviewKind.Basic
                        ? basicAttack?.WarningDuration ?? 0.1f
                        : attack?.WarningDuration ?? 0.1f;
                var telegraphHoldDuration = kind == FallenCommanderAttackPreviewKind.FinalCharge
                    ? config.FinalChargeTelegraphHoldDuration
                    : kind == FallenCommanderAttackPreviewKind.TimeoutWipe
                        ? timeoutWipe?.TelegraphHoldDuration ?? 0f
                    : kind == FallenCommanderAttackPreviewKind.Basic
                        ? basicAttack?.TelegraphHoldDuration ?? 0f
                        : attack?.TelegraphHoldDuration ?? 0f;
                var telegraphPrefab = kind == FallenCommanderAttackPreviewKind.Basic
                    ? basicAttack?.TelegraphPrefab
                    : kind == FallenCommanderAttackPreviewKind.FinalCharge
                        ? config.FinalChargeTelegraphPrefab
                    : kind == FallenCommanderAttackPreviewKind.TimeoutWipe
                        ? timeoutWipe?.TelegraphPrefab
                        : attack?.TelegraphPrefab;
                var telegraphRadius = kind == FallenCommanderAttackPreviewKind.FinalCharge
                    ? config.FinalChargeRadius
                    : kind == FallenCommanderAttackPreviewKind.TimeoutWipe
                        ? timeoutWipe?.Radius ?? 0f
                    : attack?.Radius ?? 0f;
                float ResolveFinalChargeRadius()
                {
                    return config.FinalChargeRadius;
                }

                previewSpec = new FallenCommanderAttackPreviewSpec
                {
                    Kind = kind,
                    Label = definition.Label,
                    Config = config,
                    BossPrefab = bossPrefab,
                    SpawnPoint = spawnPoint,
                    FacingTarget = commanderRoot == null ? null : commanderRoot.transform,
                    BasicAttack = basicAttack,
                    Effects = effects,
                    TelegraphPrefab = telegraphPrefab,
                    TelegraphRadius = telegraphRadius,
                    TelegraphWidth = kind == FallenCommanderAttackPreviewKind.Basic
                        ? (basicAttack?.ProjectileRadius ?? 0f) * 2f
                        : attack?.Width ?? 0f,
                    TelegraphLength = kind == FallenCommanderAttackPreviewKind.Basic
                        ? basicAttack?.MaxDistance ?? 0f
                        : attack?.Length ?? 0f,
                    SecondaryTelegraphRadius = kind == FallenCommanderAttackPreviewKind.CorruptionRing
                        ? config.CorruptionRingSafeRadius
                        : 0f,
                    TelegraphRadiusProvider = kind == FallenCommanderAttackPreviewKind.FinalCharge
                        ? ResolveFinalChargeRadius
                        : kind == FallenCommanderAttackPreviewKind.TimeoutWipe
                            ? () => timeoutWipe?.Radius ?? 0f
                        : () => attack?.Radius ?? 0f,
                    TelegraphWidthProvider = kind == FallenCommanderAttackPreviewKind.Basic
                        ? () => (basicAttack?.ProjectileRadius ?? 0f) * 2f
                        : () => attack?.Width ?? 0f,
                    TelegraphLengthProvider = kind == FallenCommanderAttackPreviewKind.Basic
                        ? () => basicAttack?.MaxDistance ?? 0f
                        : () => attack?.Length ?? 0f,
                    SecondaryTelegraphRadiusProvider = kind == FallenCommanderAttackPreviewKind.CorruptionRing
                        ? () => config.CorruptionRingSafeRadius
                        : null,
                    BlackHoleActiveDuration = kind == FallenCommanderAttackPreviewKind.BlackHole
                        ? config.BlackHoleActiveDuration
                        : 0f,
                    BlackHoleEndEffects = kind == FallenCommanderAttackPreviewKind.BlackHole
                        ? config.BlackHoleEndEffects
                        : null,
                    PreCastMotion = kind == FallenCommanderAttackPreviewKind.FinalCharge
                        ? config.FinalChargePreCastMotion
                        : timeoutWipe?.PreCastMotion ?? attack?.PreCastMotion,
                    PreCastMotionSpeed = kind == FallenCommanderAttackPreviewKind.FinalCharge
                        ? config.FinalChargePreCastMotionSpeed
                        : timeoutWipe?.PreCastMotionSpeed ??
                        attack?.PreCastMotionSpeed ?? 1f,
                    PreCastMotionStart = kind == FallenCommanderAttackPreviewKind.FinalCharge
                        ? config.FinalChargePreCastMotionStart
                        : timeoutWipe?.PreCastMotionStart ??
                        attack?.PreCastMotionStart ?? 0f,
                    PreCastMotionEnd = kind == FallenCommanderAttackPreviewKind.FinalCharge
                        ? config.FinalChargePreCastMotionEnd
                        : timeoutWipe?.PreCastMotionEnd ??
                        attack?.PreCastMotionEnd ?? 1f,
                    CastMotion = kind == FallenCommanderAttackPreviewKind.FinalCharge
                        ? config.FinalChargeCastMotion
                        : timeoutWipe?.CastMotion ?? attack?.CastMotion,
                    CastMotionDuration = kind == FallenCommanderAttackPreviewKind.FinalCharge
                        ? config.FinalChargeCastMotionDuration
                        : timeoutWipe?.CastMotionDuration ??
                        attack?.CastMotionDuration ?? 0f,
                    CastMotionSpeed = kind == FallenCommanderAttackPreviewKind.FinalCharge
                        ? config.FinalChargeCastMotionSpeed
                        : timeoutWipe?.CastMotionSpeed ??
                        attack?.CastMotionSpeed ?? 1f,
                    CastMotionStart = kind == FallenCommanderAttackPreviewKind.FinalCharge
                        ? config.FinalChargeCastMotionStart
                        : timeoutWipe?.CastMotionStart ??
                        attack?.CastMotionStart ?? 0f,
                    CastMotionEnd = kind == FallenCommanderAttackPreviewKind.FinalCharge
                        ? config.FinalChargeCastMotionEnd
                        : timeoutWipe?.CastMotionEnd ??
                        attack?.CastMotionEnd ?? 1f,
                    WarningDuration = Mathf.Max(0.1f, warningDuration),
                    TelegraphHoldDuration = Mathf.Max(0f, telegraphHoldDuration),
                    StartEffectLocalOffset = kind == FallenCommanderAttackPreviewKind.FinalCharge
                        ? config.FinalChargeStartEffectOffset
                        : Vector3.zero
                };
                return true;
            }

            return false;
        }

        // 시전 단계에 모션·VFX·SFX 중 하나라도 있는지 확인한다.
        private static bool HasPreCastPresentation(FallenCommanderAttackPreviewSpec previewSpec)
        {
            return previewSpec.BasicAttack != null ||
                previewSpec.TelegraphPrefab != null ||
                previewSpec.PreCastMotion != null ||
                previewSpec.Effects?.StartVfxPrefab != null ||
                previewSpec.Effects?.StartSfx != null;
        }

        // 공격 단계에 모션·VFX·SFX 중 하나라도 있는지 확인한다.
        private static bool HasCastPresentation(FallenCommanderAttackPreviewSpec previewSpec)
        {
            return previewSpec.BasicAttack != null ||
                previewSpec.CastMotion != null ||
                previewSpec.Effects?.ResolveVfxPrefab != null ||
                previewSpec.Effects?.ResolveSfx != null;
        }

        // 현재 속성이 공격 탭에서 별도로 표시되는 데이터인지 확인한다.
        private static bool IsAttackProperty(string propertyPath)
        {
            foreach (var definition in AttackDefinitions)
            {
                foreach (var propertyName in definition.PropertyNames)
                {
                    if (propertyPath == propertyName)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // 충전 광역기 데이터 바로 아래에 VFX 미리보기 조작부를 표시한다.
        private static void DrawFinalChargePreview(FallenCommanderBossConfig config)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("충전 광역기 시각 효과 미리보기", EditorStyles.boldLabel);

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                FallenCommanderFinalChargeVfxEditorPreview.Stop();
                EditorGUILayout.HelpBox(
                    "프리팹 편집 화면에서는 원본 보호를 위해 미리보기를 실행할 수 없습니다. " +
                    "프리팹 편집 화면을 닫고 개발용 군단장 씬에서 실행하세요.",
                    MessageType.Warning);
                return;
            }

            var startVfx = config.FinalChargeEffects?.StartVfxPrefab;
            if (startVfx == null)
            {
                EditorGUILayout.HelpBox(
                    "8. 충전 광역기의 시전 시각 효과를 먼저 지정해 주세요.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "게임을 실행하지 않고 임시 보스에 시전 시각 효과를 붙여 표시합니다. " +
                "시전 연출 위치 오프셋을 바꾸면 씬 미리보기 위치도 바로 갱신됩니다.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                var previewLabel = FallenCommanderFinalChargeVfxEditorPreview.IsActive
                    ? "미리보기 다시 재생"
                    : "충전 시전 시각 효과 미리보기";
                if (GUILayout.Button(previewLabel))
                {
                    if (!TryStartPreview(config))
                    {
                        EditorUtility.DisplayDialog(
                            "충전 시각 효과 미리보기",
                            "현재 씬에서 군단장 보스 실행 오브젝트를 찾을 수 없습니다.",
                            "확인");
                    }
                }

                using (new EditorGUI.DisabledScope(
                    !FallenCommanderFinalChargeVfxEditorPreview.IsActive))
                {
                    if (GUILayout.Button("미리보기 종료", GUILayout.Width(110f)))
                    {
                        FallenCommanderFinalChargeVfxEditorPreview.Stop();
                    }
                }
            }
        }

        // 데이터 인스펙터가 닫히면 임시 미리보기 오브젝트를 정리한다.
        private void OnDisable()
        {
            FallenCommanderFinalChargeVfxEditorPreview.Stop();
            FallenCommanderAttackEditorPreview.Stop();
        }

        // 현재 Scene의 컨트롤러에서 보스 프리팹과 생성 위치를 찾아 미리보기를 시작한다.
        private static bool TryStartPreview(FallenCommanderBossConfig config)
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                return false;
            }

            foreach (var controller in Object.FindObjectsByType<FallenCommanderController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                var controllerData = new SerializedObject(controller);
                var prefab = controllerData.FindProperty("bossPrefab")
                    ?.objectReferenceValue as GameObject;
                var spawnPoint = controllerData.FindProperty("bossSpawnPoint")
                    ?.objectReferenceValue as Transform;
                var commanderRoot = controllerData.FindProperty("commanderRoot")
                    ?.objectReferenceValue as GameObject;
                if (prefab == null)
                {
                    continue;
                }

                return FallenCommanderFinalChargeVfxEditorPreview.Play(
                    config,
                    prefab,
                    spawnPoint,
                    commanderRoot == null ? null : commanderRoot.transform);
            }

            return false;
        }
    }

    [InitializeOnLoad]
    internal static class FallenCommanderFinalChargeVfxEditorPreview
    {
        private static FallenCommanderBossConfig activeConfig;
        private static GameObject previewBoss;
        private static GameObject previewVfx;
        private static ParticleSystem[] particles = System.Array.Empty<ParticleSystem>();
        private static double lastTime;

        public static bool IsActive => previewBoss != null && previewVfx != null;

        // 에디터 재생·종료·컴파일 시 임시 미리보기가 남지 않도록 정리 경로를 등록한다.
        static FallenCommanderFinalChargeVfxEditorPreview()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += _ => Stop();
            EditorApplication.quitting += Stop;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
        }

        // 임시 보스와 시전 VFX를 Scene에 생성하고 에디터 재생을 시작한다.
        public static bool Play(
            FallenCommanderBossConfig config,
            GameObject bossPrefab,
            Transform spawnPoint,
            Transform facingTarget)
        {
            Stop();
            if (PrefabStageUtility.GetCurrentPrefabStage() != null ||
                config?.FinalChargeEffects?.StartVfxPrefab == null ||
                bossPrefab == null)
            {
                return false;
            }

            GameObject createdBoss = null;
            GameObject createdVfx = null;
            var initialized = false;
            try
            {
                createdBoss = PrefabUtility.InstantiatePrefab(bossPrefab) as GameObject;
                if (createdBoss == null)
                {
                    return false;
                }

                createdBoss.name = $"[충전 시각 효과 미리보기] {bossPrefab.name}";
                createdBoss.hideFlags = HideFlags.HideAndDontSave;
                if (spawnPoint != null)
                {
                    createdBoss.transform.SetPositionAndRotation(
                        spawnPoint.position,
                        spawnPoint.rotation);
                }

                FaceTarget(createdBoss.transform, facingTarget);

                foreach (var behaviour in createdBoss.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    behaviour.enabled = false;
                }

                createdVfx = Object.Instantiate(
                    config.FinalChargeEffects.StartVfxPrefab,
                    createdBoss.transform,
                    false);
                createdVfx.name = $"[미리보기] {config.FinalChargeEffects.StartVfxPrefab.name}";
                createdVfx.hideFlags = HideFlags.HideAndDontSave;
                createdVfx.transform.localPosition = config.FinalChargeStartEffectOffset;
                createdVfx.transform.localRotation = Quaternion.identity;

                activeConfig = config;
                previewBoss = createdBoss;
                previewVfx = createdVfx;
                particles = previewVfx.GetComponentsInChildren<ParticleSystem>(true);
                RestartParticles();
                lastTime = EditorApplication.timeSinceStartup;
                initialized = true;
                SceneView.lastActiveSceneView?.Frame(
                    new Bounds(previewVfx.transform.position, Vector3.one * 4f),
                    true);
                SceneView.RepaintAll();
                return true;
            }
            finally
            {
                if (!initialized)
                {
                    if (createdVfx != null)
                    {
                        Object.DestroyImmediate(createdVfx);
                    }

                    if (createdBoss != null)
                    {
                        Object.DestroyImmediate(createdBoss);
                    }
                }
            }
        }

        // 임시 보스와 VFX를 제거하고 미리보기 상태를 초기화한다.
        public static void Stop()
        {
            if (previewBoss != null)
            {
                Object.DestroyImmediate(previewBoss);
            }
            else if (previewVfx != null)
            {
                Object.DestroyImmediate(previewVfx);
            }

            activeConfig = null;
            previewBoss = null;
            previewVfx = null;
            particles = System.Array.Empty<ParticleSystem>();
            lastTime = 0d;
            SceneView.RepaintAll();
        }

        // 데이터 오프셋을 실시간 반영하고 파티클을 Scene 뷰에서 진행시킨다.
        private static void Update()
        {
            if (!IsActive || activeConfig == null)
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Stop();
                return;
            }

            previewVfx.transform.localPosition = activeConfig.FinalChargeStartEffectOffset;
            var now = EditorApplication.timeSinceStartup;
            var deltaTime = Mathf.Clamp((float)(now - lastTime), 0f, 0.1f);
            lastTime = now;

            foreach (var particle in particles)
            {
                if (particle != null)
                {
                    particle.Simulate(deltaTime, false, false, false);
                }
            }

            SceneView.RepaintAll();
        }

        // 모든 자식 파티클을 처음 상태로 되돌리고 다시 재생한다.
        private static void RestartParticles()
        {
            foreach (var particle in particles)
            {
                if (particle == null)
                {
                    continue;
                }

                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Simulate(0f, false, true, true);
                particle.Play(false);
            }
        }

        // 데이터 미리보기의 임시 보스를 Scene 군단장 방향으로 회전시킨다.
        private static void FaceTarget(Transform bossTransform, Transform facingTarget)
        {
            if (bossTransform == null || facingTarget == null)
            {
                return;
            }

            var direction = facingTarget.position - bossTransform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            bossTransform.rotation = Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);
        }
    }
}
