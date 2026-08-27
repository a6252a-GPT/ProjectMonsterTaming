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
                "baseMaxHealth" => "기본 최대 체력",
                "baseDefense" => "기본 방어력",
                "baseMoveSpeed" => "기본 이동속도",
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
                "blackHoleEndEffects" => "종료 연출 (VFX / SFX)",
                "lineStrike" => "직선 공격 설정",
                "corruptionRing" => "타락의 고리 설정",
                "corruptionRingSafeRadius" => "안전지대 반지름",
                "finalChargeHealthRatio" => "발동 체력 비율",
                "finalChargeDuration" => "충전 시간",
                "finalChargeTelegraphPrefab" => "공격 범위 오브젝트",
                "finalChargeTelegraphHoldDuration" => "충전 완료 유지시간",
                "finalChargeRadius" => "공격 범위 반지름",
                "finalChargeEffects" => "연출 (VFX / SFX)",
                "finalChargePreCastMotion" => "시전 모션",
                "finalChargePreCastMotionSpeed" => "시전 모션 속도",
                "finalChargePreCastMotionStart" => "시전 모션 시작 지점",
                "finalChargePreCastMotionEnd" => "시전 모션 종료 지점",
                "finalChargeCastMotion" => "공격 모션",
                "finalChargeCastMotionSpeed" => "공격 모션 속도",
                "finalChargeCastMotionStart" => "공격 모션 시작 지점",
                "finalChargeCastMotionEnd" => "공격 모션 종료 지점",
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
                "effects" => "연출 (VFX / SFX)",
                "warningDuration" => "공격 전 경고시간",
                "telegraphHoldDuration" => "충전 완료 유지시간",
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
                "effects" => "연출 (VFX / SFX)",
                "preCastMotion" => "시전 모션",
                "preCastMotionSpeed" => "시전 모션 속도",
                "preCastMotionStart" => "시전 모션 시작 지점",
                "preCastMotionEnd" => "시전 모션 종료 지점",
                "castMotion" => "공격 모션",
                "castMotionSpeed" => "공격 모션 속도",
                "castMotionStart" => "공격 모션 시작 지점",
                "castMotionEnd" => "공격 모션 종료 지점",
                "warningDuration" => "공격 전 경고시간",
                "telegraphHoldDuration" => "충전 완료 유지시간",
                "radius" => "원형 공격 반지름",
                "width" => "직선 공격 너비",
                "length" => "직선 공격 길이",
                "stunDuration" => "기절 지속시간",
                _ => ObjectNames.NicifyVariableName(propertyName)
            };
        }

        public static string Effects(string propertyName, string resolveLabel)
        {
            return propertyName switch
            {
                "startVfxPrefab" => "시전 효과",
                "startVfxDuration" => "시전 효과 유지시간 (0 = 자동)",
                "startVfxAnchor" => "시전 효과 위치 기준",
                "startVfxPositionOffset" => "시전 효과 위치 오프셋",
                "startVfxRotationOffset" => "시전 효과 회전 오프셋",
                "startVfxScaleMultiplier" => "시전 효과 전체 크기 배율",
                "resolveVfxPrefab" => $"{resolveLabel} 효과",
                "resolveVfxDuration" => $"{resolveLabel} 효과 유지시간 (0 = 자동)",
                "resolveVfxAnchor" => $"{resolveLabel} 효과 위치 기준",
                "resolveVfxPositionOffset" => $"{resolveLabel} 효과 위치 오프셋",
                "resolveVfxRotationOffset" => $"{resolveLabel} 효과 회전 오프셋",
                "resolveVfxScaleMultiplier" => $"{resolveLabel} 효과 전체 크기 배율",
                "hitVfxPrefab" => "적중 VFX",
                "hitVfxDuration" => "적중 VFX 유지시간 (0 = 자동)",
                "hitVfxPositionOffset" => "적중 VFX 위치 오프셋",
                "hitVfxRotationOffset" => "적중 VFX 회전 오프셋",
                "hitVfxScaleMultiplier" => "적중 VFX 전체 크기 배율",
                "startSfx" => "시전 효과음",
                "startSfxDuration" => "시전 효과음 유지시간 (0 = 자동)",
                "resolveSfx" => $"{resolveLabel} 효과음",
                "resolveSfxDuration" => $"{resolveLabel} 효과음 유지시간 (0 = 자동)",
                "sfxVolume" => "효과음 볼륨",
                "hitSfx" => "적중 SFX",
                "hitSfxDuration" => "적중 SFX 유지시간 (0 = 자동)",
                "hitSfxVolume" => "적중 SFX 볼륨",
                _ => ObjectNames.NicifyVariableName(propertyName)
            };
        }

        public static string TimeoutWipe(string propertyName)
        {
            return propertyName switch
            {
                "telegraphPrefab" => "공격 범위 오브젝트",
                "radius" => "공격 범위 반지름 (연출용)",
                "effects" => "연출 (VFX / SFX)",
                "preCastMotion" => "시전 모션",
                "preCastMotionSpeed" => "시전 모션 속도",
                "preCastMotionStart" => "시전 모션 시작 지점",
                "preCastMotionEnd" => "시전 모션 종료 지점",
                "castMotion" => "전멸 발동 모션",
                "castMotionSpeed" => "전멸 발동 모션 속도",
                "castMotionStart" => "전멸 발동 모션 시작 지점",
                "castMotionEnd" => "전멸 발동 모션 종료 지점",
                "warningDuration" => "발동 전 경고시간",
                "telegraphHoldDuration" => "충전 완료 유지시간",
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
                "markStrikePattern" => "위치 공격 다중 패턴 설정",
                _ => ObjectNames.NicifyVariableName(propertyName)
            };
        }

        public static string MarkStrikePhase(string propertyName)
        {
            return propertyName switch
            {
                "totalCount" => "총 공격 개수",
                "simultaneousCount" => "동시 생성 개수",
                "groupInterval" => "다음 묶음 생성 간격",
                "warningDuration" => "개별 공격 경고시간",
                "arenaHalfExtents" => "랜덤 생성 영역 반경",
                "minimumSpacing" => "랜덤 위치 최소 간격",
                "clusterGroups" => "묶음 밀집 배치",
                "clusterRadius" => "묶음 배치 반경",
                "maxDamagePerGroup" => "묶음당 최대 피해 횟수",
                "stunDuration" => "피격 기절시간",
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
        private const float SectionPadding = 4f;

        private static readonly string[] VfxProperties =
        {
            "startVfxPrefab",
            "startVfxDuration",
            "startVfxAnchor",
            "startVfxPositionOffset",
            "startVfxRotationOffset",
            "startVfxScaleMultiplier",
            "resolveVfxPrefab",
            "resolveVfxDuration",
            "resolveVfxAnchor",
            "resolveVfxPositionOffset",
            "resolveVfxRotationOffset",
            "resolveVfxScaleMultiplier",
            "hitVfxPrefab",
            "hitVfxDuration",
            "hitVfxPositionOffset",
            "hitVfxRotationOffset",
            "hitVfxScaleMultiplier"
        };

        private static readonly string[] SfxProperties =
        {
            "startSfx",
            "startSfxDuration",
            "resolveSfx",
            "resolveSfxDuration",
            "sfxVolume",
            "hitSfx",
            "hitSfxDuration",
            "hitSfxVolume"
        };

        // 연출 데이터를 VFX와 SFX 접이식 영역으로 나누어 표시한다.
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
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
                line.y += line.height + EditorGUIUtility.standardVerticalSpacing;
                line.height = DrawSection(
                    line,
                    property,
                    "VFX 설정",
                    "Vfx",
                    VfxProperties);
                line.y += line.height + EditorGUIUtility.standardVerticalSpacing;
                line.height = DrawSection(
                    line,
                    property,
                    "SFX 설정",
                    "Sfx",
                    SfxProperties);

                var blackHoleEndEffects = FindBlackHoleEndEffects(property);
                if (blackHoleEndEffects != null)
                {
                    line.y += line.height + EditorGUIUtility.standardVerticalSpacing;
                    line.height = EditorGUI.GetPropertyHeight(
                        blackHoleEndEffects,
                        new GUIContent("종료 연출"),
                        true);
                    EditorGUI.PropertyField(
                        line,
                        blackHoleEndEffects,
                        new GUIContent("종료 연출"),
                        true);
                }
            }

            EditorGUI.EndProperty();
        }

        // 연출 전체와 각 하위 영역의 펼침 상태를 반영해 높이를 계산한다.
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
            {
                return height;
            }

            height += EditorGUIUtility.standardVerticalSpacing +
                GetSectionHeight(property, "Vfx", VfxProperties);
            height += EditorGUIUtility.standardVerticalSpacing +
                GetSectionHeight(property, "Sfx", SfxProperties);
            var blackHoleEndEffects = FindBlackHoleEndEffects(property);
            if (blackHoleEndEffects != null)
            {
                height += EditorGUIUtility.standardVerticalSpacing +
                    EditorGUI.GetPropertyHeight(
                        blackHoleEndEffects,
                        new GUIContent("종료 연출"),
                        true);
            }

            return height;
        }

        // 하나의 VFX 또는 SFX 영역을 도움말 상자 안에 그린다.
        private static float DrawSection(
            Rect position,
            SerializedProperty owner,
            string title,
            string stateSuffix,
            string[] propertyNames)
        {
            var expanded = GetSectionExpanded(owner, stateSuffix);
            var resolveLabel = GetResolveLabel(owner);
            var height = GetSectionHeight(owner, stateSuffix, propertyNames);
            var box = new Rect(position.x, position.y, position.width, height);
            GUI.Box(box, GUIContent.none, EditorStyles.helpBox);

            var content = new Rect(
                box.x + SectionPadding,
                box.y + SectionPadding,
                box.width - SectionPadding * 2f,
                EditorGUIUtility.singleLineHeight);
            var nextExpanded = EditorGUI.Foldout(content, expanded, title, true);
            if (nextExpanded != expanded)
            {
                SetSectionExpanded(owner, stateSuffix, nextExpanded);
                expanded = nextExpanded;
            }

            if (!expanded)
            {
                return height;
            }

            EditorGUI.indentLevel++;
            foreach (var propertyName in propertyNames)
            {
                if (!ShouldDrawProperty(owner, propertyName))
                {
                    continue;
                }

                var child = owner.FindPropertyRelative(propertyName);
                if (child == null)
                {
                    continue;
                }

                var childLabel = new GUIContent(
                    FallenCommanderInspectorLabels.Effects(
                        child.name,
                        resolveLabel));
                content.y += content.height + EditorGUIUtility.standardVerticalSpacing;
                content.height = EditorGUI.GetPropertyHeight(child, childLabel, true);
                EditorGUI.PropertyField(content, child, childLabel, true);
            }

            EditorGUI.indentLevel--;
            return height;
        }

        // 지정한 영역에 포함된 속성 높이를 모두 더한다.
        private static float GetSectionHeight(
            SerializedProperty owner,
            string stateSuffix,
            string[] propertyNames)
        {
            var height = SectionPadding * 2f + EditorGUIUtility.singleLineHeight;
            if (!GetSectionExpanded(owner, stateSuffix))
            {
                return height;
            }

            var resolveLabel = GetResolveLabel(owner);

            foreach (var propertyName in propertyNames)
            {
                if (!ShouldDrawProperty(owner, propertyName))
                {
                    continue;
                }

                var child = owner.FindPropertyRelative(propertyName);
                if (child == null)
                {
                    continue;
                }

                var childLabel = new GUIContent(
                    FallenCommanderInspectorLabels.Effects(
                        child.name,
                        resolveLabel));
                height += EditorGUIUtility.standardVerticalSpacing +
                    EditorGUI.GetPropertyHeight(child, childLabel, true);
            }

            return height;
        }

        // 기본 공격과 블랙홀 종료 연출에서는 별도의 적중 슬롯을 숨긴다.
        private static bool ShouldDrawProperty(
            SerializedProperty owner,
            string propertyName)
        {
            if (owner.propertyPath == "blackHoleEndEffects")
            {
                return !propertyName.StartsWith("start") &&
                    !propertyName.StartsWith("hit");
            }

            if (!propertyName.StartsWith("hit"))
            {
                return true;
            }

            return owner.propertyPath != "projectileBasicAttack.effects" &&
                owner.propertyPath != "blackHoleEndEffects";
        }

        // 블랙홀 본체 연출을 그릴 때만 같은 에셋의 종료 연출 데이터를 함께 반환한다.
        private static SerializedProperty FindBlackHoleEndEffects(
            SerializedProperty property)
        {
            return property.propertyPath == "blackHole.effects"
                ? property.serializedObject.FindProperty("blackHoleEndEffects")
                : null;
        }

        // 기본 공격·블랙홀 종료·일반 공격을 구분해 실제 재생 조건에 맞는 명칭을 반환한다.
        private static string GetResolveLabel(SerializedProperty property)
        {
            return property.propertyPath switch
            {
                "projectileBasicAttack.effects" => "적중",
                "blackHoleEndEffects" => "종료",
                _ => "발동"
            };
        }

        // 에셋 값을 변경하지 않고 현재 에디터 세션의 영역 펼침 상태를 읽는다.
        private static bool GetSectionExpanded(
            SerializedProperty property,
            string stateSuffix)
        {
            return SessionState.GetBool(GetSectionStateKey(property, stateSuffix), true);
        }

        // 에셋 값을 변경하지 않고 현재 에디터 세션의 영역 펼침 상태를 저장한다.
        private static void SetSectionExpanded(
            SerializedProperty property,
            string stateSuffix,
            bool expanded)
        {
            SessionState.SetBool(
                GetSectionStateKey(property, stateSuffix),
                expanded);
        }

        // 선택한 에셋과 속성 경로별로 겹치지 않는 펼침 상태 키를 만든다.
        private static string GetSectionStateKey(
            SerializedProperty property,
            string stateSuffix)
        {
            var targetId = property.serializedObject.targetObject == null
                ? 0
                : property.serializedObject.targetObject.GetInstanceID();
            return $"ProjectMT.FallenCommander.Effects.{targetId}." +
                $"{property.propertyPath}.{stateSuffix}";
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

    [CustomPropertyDrawer(typeof(FallenCommanderMarkStrikePhaseData))]
    public sealed class FallenCommanderMarkStrikePhaseDataDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            FallenCommanderLocalizedPropertyGUI.Draw(
                position,
                property,
                label,
                FallenCommanderInspectorLabels.MarkStrikePhase);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return FallenCommanderLocalizedPropertyGUI.GetHeight(
                property,
                FallenCommanderInspectorLabels.MarkStrikePhase);
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
