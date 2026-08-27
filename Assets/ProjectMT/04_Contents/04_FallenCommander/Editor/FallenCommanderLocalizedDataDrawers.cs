using UnityEditor;
using UnityEngine;

namespace ProjectMT.Contents.FallenCommander.Editor
{
    internal static class FallenCommanderInspectorLabels
    {
        public static GUIContent BossConfig(SerializedProperty property)
        {
            return new GUIContent(property.name switch
            {
                "m_Script" => "스크립트",
                "attackInterval" => "공격 간격",
                "attackRange" => "공격 가능 거리",
                "turnSpeed" => "회전 속도",
                "projectileBasicAttack" => "기본 공격 설정",
                "meleeAttack" => "근접 공격 설정",
                "markStrike" => "위치 공격 설정",
                "trackingMark" => "추적 낙인 설정",
                "trackingMarkLockDuration" => "추적 종료 전 위치 고정시간",
                "blackHole" => "블랙홀 공격 설정",
                "blackHoleActiveDuration" => "활성 유지시간",
                "blackHoleCoreRadius" => "중심 피해 범위",
                "blackHoleSpawnMinDistance" => "생성 최소 거리",
                "blackHoleSpawnMaxDistance" => "생성 최대 거리",
                "blackHoleOuterPullSpeed" => "바깥쪽 흡입 속도",
                "blackHoleInnerPullSpeed" => "중심부 흡입 속도",
                "blackHolePullStrengthCurve" => "중심 거리별 흡입 강도",
                "blackHoleArenaHalfExtents" => "생성 가능 영역 반경",
                "blackHoleEndEffects" => "종료 연출 (시각 효과 / 효과음)",
                "lineStrike" => "직선 공격 설정",
                "corruptionRing" => "타락의 고리 설정",
                "corruptionRingSafeRadius" => "안전지대 반지름",
                "finalChargeTelegraphPrefab" => "공격 범위 오브젝트",
                "finalChargeEffects" => "연출 (시각 효과 / 효과음)",
                "finalChargeStartEffectOffset" => "시전 연출 위치 오프셋",
                "timeoutWipe" => "제한시간 전멸기 설정",
                "closeAttackDistance" => "근접 공격 선택 거리",
                "lineStrikeMinimumDistance" => "직선 공격 최소 거리",
                "lineStrikeAlignmentThreshold" => "직선 공격 정면 판정 기준",
                "phaseConfig" => "페이즈 설정 파일",
                "deathMotion" => "보스 사망 모션",
                "deathMotionDuration" => "보스 사망 모션 재생시간 (0 = 자동)",
                "commanderDeathMotion" => "군단장 사망 모션",
                "commanderDeathMotionDuration" => "군단장 사망 모션 재생시간 (0 = 자동)",
                "deathResultDelay" => "사망 후 결과창 대기시간",
                "maxBreakGauge" => "최대 브레이크 게이지",
                "breakGaugePerHit" => "피격 1회당 브레이크 게이지",
                "breakGaugeAttackPowerMultiplier" => "공격력 반영 배율",
                "breakGaugePhaseTwoMultiplier" => "2페이즈 브레이크 획득 배율",
                "breakGaugePhaseThreeMultiplier" => "3페이즈 브레이크 획득 배율",
                "breakDuration" => "브레이크 지속시간",
                "breakDamageMultiplier" => "브레이크 중 받는 피해 배율",
                "breakMotion" => "브레이크 모션",
                "breakMotionDuration" => "브레이크 모션 재생시간 (0 = 자동)",
                _ => property.displayName
            });
        }

        public static string BasicAttack(string propertyName)
        {
            return propertyName switch
            {
                "telegraphPrefab" => "공격 범위 오브젝트",
                "projectilePrefab" => "투사체 오브젝트",
                "effects" => "연출 (시각 효과 / 효과음)",
                "warningDuration" => "공격 전 경고시간",
                "projectileSpeed" => "투사체 이동 속도",
                "projectileRadius" => "투사체 피격 반지름",
                "maxDistance" => "투사체 최대 이동거리",
                "projectileHeight" => "투사체 생성 높이",
                "repeatInterval" => "기본 공격 반복 간격",
                "patternOverlapDelay" => "다른 패턴 시작 후 대기시간",
                _ => ObjectNames.NicifyVariableName(propertyName)
            };
        }

        public static string Attack(string propertyName)
        {
            return propertyName switch
            {
                "telegraphPrefab" => "공격 범위 오브젝트",
                "effects" => "연출 (시각 효과 / 효과음)",
                "preCastMotion" => "시전 모션",
                "preCastMotionSpeed" => "시전 모션 속도",
                "preCastMotionStart" => "시전 모션 시작 지점",
                "preCastMotionEnd" => "시전 모션 종료 지점",
                "castMotion" => "공격 모션",
                "castMotionSpeed" => "공격 모션 속도",
                "castMotionStart" => "공격 모션 시작 지점",
                "castMotionEnd" => "공격 모션 종료 지점",
                "warningDuration" => "공격 전 경고시간",
                "radius" => "원형 공격 반지름",
                "width" => "직선 공격 너비",
                "length" => "직선 공격 길이",
                "stunDuration" => "기절 지속시간",
                _ => ObjectNames.NicifyVariableName(propertyName)
            };
        }

        public static string Effects(string propertyName)
        {
            return propertyName switch
            {
                "startVfxPrefab" => "시전 효과",
                "startVfxDuration" => "시전 효과 유지시간 (0 = 자동)",
                "startVfxAnchor" => "시전 효과 위치 기준",
                "startVfxPositionOffset" => "시전 효과 위치 오프셋",
                "startVfxRotationOffset" => "시전 효과 회전 오프셋",
                "startVfxScale" => "시전 효과 크기",
                "resolveVfxPrefab" => "적중 효과",
                "resolveVfxDuration" => "적중 효과 유지시간 (0 = 자동)",
                "resolveVfxAnchor" => "적중 효과 위치 기준",
                "resolveVfxPositionOffset" => "적중 효과 위치 오프셋",
                "resolveVfxRotationOffset" => "적중 효과 회전 오프셋",
                "resolveVfxScale" => "적중 효과 크기",
                "startSfx" => "시전 효과음",
                "startSfxDuration" => "시전 효과음 유지시간 (0 = 자동)",
                "resolveSfx" => "적중 효과음",
                "resolveSfxDuration" => "적중 효과음 유지시간 (0 = 자동)",
                "sfxVolume" => "효과음 볼륨",
                _ => ObjectNames.NicifyVariableName(propertyName)
            };
        }

        public static string TimeoutWipe(string propertyName)
        {
            return propertyName switch
            {
                "effects" => "연출 (시각 효과 / 효과음)",
                "preCastMotion" => "시전 모션",
                "preCastMotionSpeed" => "시전 모션 속도",
                "preCastMotionStart" => "시전 모션 시작 지점",
                "preCastMotionEnd" => "시전 모션 종료 지점",
                "castMotion" => "전멸 발동 모션",
                "castMotionSpeed" => "전멸 발동 모션 속도",
                "castMotionStart" => "전멸 발동 모션 시작 지점",
                "castMotionEnd" => "전멸 발동 모션 종료 지점",
                "warningDuration" => "발동 전 경고시간",
                "resultDelay" => "결과창 대기시간",
                "warningMessage" => "전멸 경고 문구",
                "warningPulseInterval" => "경고 점멸 간격",
                _ => ObjectNames.NicifyVariableName(propertyName)
            };
        }

        public static string Phase(string propertyName)
        {
            return propertyName switch
            {
                "phase" => "페이즈",
                "healthRatio" => "진입 체력 비율 (읽기 전용)",
                "availableAttacks" => "사용할 공격 목록",
                "allowOverlappingBasicAttack" => "기본 투사체 중복 공격 허용",
                "hasSignatureAttack" => "페이즈 대표 공격 사용",
                "signatureAttack" => "페이즈 대표 공격",
                "transitionMessage" => "페이즈 전환 문구",
                "transitionSound" => "페이즈 전환 사운드",
                "transitionDuration" => "페이즈 전환시간",
                _ => ObjectNames.NicifyVariableName(propertyName)
            };
        }
    }

    internal static class FallenCommanderLocalizedPropertyGUI
    {
        public static float GetHeight(
            SerializedProperty property,
            System.Func<string, string> labelResolver)
        {
            var height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
            {
                return height;
            }

            var child = property.Copy();
            var end = child.GetEndProperty();
            if (!child.NextVisible(true))
            {
                return height;
            }

            while (!SerializedProperty.EqualContents(child, end))
            {
                if (child.depth == property.depth + 1)
                {
                    var label = new GUIContent(labelResolver(child.name));
                    height += EditorGUIUtility.standardVerticalSpacing +
                        EditorGUI.GetPropertyHeight(child, label, true);
                }

                if (!child.NextVisible(false))
                {
                    break;
                }
            }

            return height;
        }

        public static void Draw(
            Rect position,
            SerializedProperty property,
            GUIContent label,
            System.Func<string, string> labelResolver)
        {
            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(
                position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(
                line,
                property.isExpanded,
                label,
                true);

            if (property.isExpanded)
            {
                var child = property.Copy();
                var end = child.GetEndProperty();
                if (child.NextVisible(true))
                {
                    EditorGUI.indentLevel++;
                    while (!SerializedProperty.EqualContents(child, end))
                    {
                        if (child.depth == property.depth + 1)
                        {
                            var childLabel = new GUIContent(labelResolver(child.name));
                            var childHeight = EditorGUI.GetPropertyHeight(
                                child,
                                childLabel,
                                true);
                            line.y += line.height + EditorGUIUtility.standardVerticalSpacing;
                            line.height = childHeight;
                            EditorGUI.PropertyField(
                                line,
                                child,
                                childLabel,
                                true);
                        }

                        if (!child.NextVisible(false))
                        {
                            break;
                        }
                    }

                    EditorGUI.indentLevel--;
                }
            }

            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(FallenCommanderBasicAttackData))]
    public sealed class FallenCommanderBasicAttackDataDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            FallenCommanderLocalizedPropertyGUI.Draw(
                position,
                property,
                label,
                FallenCommanderInspectorLabels.BasicAttack);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return FallenCommanderLocalizedPropertyGUI.GetHeight(
                property,
                FallenCommanderInspectorLabels.BasicAttack);
        }
    }

    [CustomPropertyDrawer(typeof(FallenCommanderAttackData))]
    public sealed class FallenCommanderAttackDataDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            FallenCommanderLocalizedPropertyGUI.Draw(
                position,
                property,
                label,
                FallenCommanderInspectorLabels.Attack);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return FallenCommanderLocalizedPropertyGUI.GetHeight(
                property,
                FallenCommanderInspectorLabels.Attack);
        }
    }

    [CustomPropertyDrawer(typeof(FallenCommanderAttackEffectData))]
    public sealed class FallenCommanderAttackEffectDataDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            FallenCommanderLocalizedPropertyGUI.Draw(
                position,
                property,
                label,
                FallenCommanderInspectorLabels.Effects);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return FallenCommanderLocalizedPropertyGUI.GetHeight(
                property,
                FallenCommanderInspectorLabels.Effects);
        }
    }

    [CustomPropertyDrawer(typeof(FallenCommanderPhaseData))]
    public sealed class FallenCommanderPhaseDataDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            FallenCommanderLocalizedPropertyGUI.Draw(
                position,
                property,
                label,
                FallenCommanderInspectorLabels.Phase);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return FallenCommanderLocalizedPropertyGUI.GetHeight(
                property,
                FallenCommanderInspectorLabels.Phase);
        }
    }

    [CustomPropertyDrawer(typeof(FallenCommanderTimeoutWipeData))]
    public sealed class FallenCommanderTimeoutWipeDataDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            FallenCommanderLocalizedPropertyGUI.Draw(
                position,
                property,
                label,
                FallenCommanderInspectorLabels.TimeoutWipe);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return FallenCommanderLocalizedPropertyGUI.GetHeight(
                property,
                FallenCommanderInspectorLabels.TimeoutWipe);
        }
    }

    [CustomEditor(typeof(FallenCommanderPhaseConfig))]
    public sealed class FallenCommanderPhaseConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var script = serializedObject.FindProperty("m_Script");
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(script, new GUIContent("스크립트"));
            }

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("phases"),
                new GUIContent("페이즈 목록"),
                true);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
