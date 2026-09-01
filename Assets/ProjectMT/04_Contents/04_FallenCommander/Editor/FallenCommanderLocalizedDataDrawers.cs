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
                "markStrike" => "연속 위치 공격 설정",
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
                "blackHoleEndEffects" => "종료 연출 (효과 / 효과음)",
                "lineStrike" => "직선 공격 설정",
                "corruptionRing" => "타락의 고리 설정",
                "corruptionRingSafeRadius" => "안전지대 반지름",
                "finalChargeHealthRatio" => "발동 체력 비율",
                "finalChargeDuration" => "충전 시간",
                "finalChargeTelegraphPrefab" => "공격 범위 오브젝트",
                "finalChargeTelegraphHoldDuration" => "충전 완료 유지시간",
                "finalChargeRadius" => "공격 범위 반지름",
                "finalChargeDamageDelay" => "피해 판정 지연시간",
                "finalChargeWarningMessage" => "패턴 경고 문구",
                "finalChargeUseStun" => "기절 적용",
                "finalChargeStunDuration" => "기절 지속시간",
                "finalChargeEffects" => "연출 (효과 / 효과음)",
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
                "twistedBattlefield" => "연속 장판 공격 설정",
                "fallingBarrage" => "낙하 탄막 공격 설정",
                "closeAttackDistance" => "근접 공격 선택 거리",
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
                "effects" => "연출 (효과 / 효과음)",
                "damageDelay" => "피해 판정 지연시간",
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
                "effects" => "연출 (효과 / 효과음)",
                "damageDelay" => "피해 판정 지연시간",
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
                "useStun" => "기절 적용",
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
                "hitVfxPrefab" => "적중 효과",
                "hitVfxDuration" => "적중 효과 유지시간 (0 = 자동)",
                "hitVfxPositionOffset" => "적중 효과 위치 오프셋",
                "hitVfxRotationOffset" => "적중 효과 회전 오프셋",
                "hitVfxScaleMultiplier" => "적중 효과 전체 크기 배율",
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
                "damageDelay" => "피해 판정 지연시간",
                "riseHeight" => "시전 중 상승 높이",
                "riseCurve" => "시전 중 상승 곡선",
                "descentDuration" => "공격 모션 종료 후 하강시간",
                "descentCurve" => "하강 곡선",
                "effects" => "연출 (효과 / 효과음)",
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
                "allowBasicAttackDuringBlackHole" => "블랙홀 중 기본 투사체 중복 허용",
                "allowBasicAttackDuringFallingBarrage" => "낙하 탄막 중 기본 투사체 중복 허용",
                "hasSignatureAttack" => "페이즈 대표 공격 사용",
                "signatureAttack" => "페이즈 대표 공격",
                "transitionMessage" => "페이즈 전환 문구",
                "transitionSound" => "페이즈 전환 사운드",
                "transitionDuration" => "페이즈 전환시간",
                "bossPrefabOverride" => "보스 프리팹 교체",
                "bossScaleMultiplier" => "보스 크기 배율",
                "markStrikePattern" => "연속 위치 공격 패턴 설정",
                "blackHolePattern" => "블랙홀 공격 페이즈 설정",
                "twistedBattlefieldPattern" => "연속 장판 공격 페이즈 설정",
                "fallingBarragePattern" => "낙하 탄막 공격 페이즈 설정",
                _ => ObjectNames.NicifyVariableName(propertyName)
            };
        }

        public static string TwistedBattlefield(string propertyName)
        {
            return propertyName switch
            {
                "telegraphPrefab" => "공격 범위 오브젝트",
                "effects" => "연출 (효과 / 효과음)",
                "damageDelay" => "피해 판정 지연시간",
                "preCastMotion" => "시전 모션",
                "preCastMotionSpeed" => "시전 모션 속도",
                "preCastMotionStart" => "시전 모션 시작 지점",
                "preCastMotionEnd" => "시전 모션 종료 지점",
                "castMotion" => "공격 모션",
                "castMotionSpeed" => "공격 모션 속도",
                "castMotionStart" => "공격 모션 시작 지점",
                "castMotionEnd" => "공격 모션 종료 지점",
                "arenaHalfExtents" => "전장 가로·세로 반경",
                "columnCount" => "세로 분할 개수",
                "rowCount" => "가로 분할 개수",
                "tileGap" => "장판 사이 간격",
                "attackInterval" => "공격 사이 회피시간",
                "dangerColor" => "위험 장판 색상",
                "safeColor" => "안전지대 색상",
                _ => ObjectNames.NicifyVariableName(propertyName)
            };
        }

        public static string TwistedBattlefieldPhase(string propertyName)
        {
            return propertyName switch
            {
                "selectionChance" => "패턴 등장 확률",
                "beatCount" => "연속 발동 횟수",
                "warningDuration" => "공격 전 경고시간",
                "telegraphHoldDuration" => "충전 완료 유지시간",
                "beatInterval" => "다음 장판 전환 간격",
                _ => ObjectNames.NicifyVariableName(propertyName)
            };
        }

        public static string BlackHolePhase(string propertyName)
        {
            return propertyName switch
            {
                "minimumCount" => "동시 생성 최소 개수",
                "maximumCount" => "동시 생성 최대 개수",
                "minimumCoreSpacing" => "중심부 최소 간격",
                _ => ObjectNames.NicifyVariableName(propertyName)
            };
        }

        public static string FallingBarrage(string propertyName)
        {
            return propertyName switch
            {
                "projectilePrefab" => "낙하 탄막 오브젝트",
                "projectileSpawnPresentationDuration" => "구체 생성 연출 유지시간",
                "telegraphPrefab" => "공격 범위 오브젝트",
                "effects" => "연출 (시각 효과 / 효과음)",
                "impactEffects" => "착탄 효과 (VFX / SFX)",
                "damageDelay" => "피해 판정 지연시간",
                "preCastMotion" => "탄막 생성 모션",
                "preCastMotionSpeed" => "탄막 생성 모션 속도",
                "preCastMotionStart" => "탄막 생성 모션 시작 지점",
                "preCastMotionEnd" => "탄막 생성 모션 종료 지점",
                "castMotion" => "착탄 모션",
                "castMotionSpeed" => "착탄 모션 속도",
                "castMotionStart" => "착탄 모션 시작 지점",
                "castMotionEnd" => "착탄 모션 종료 지점",
                "arenaHalfExtents" => "랜덤 생성 영역 반경",
                "spawnHeight" => "탄막 생성 높이",
                "projectileCount" => "한 묶음 탄막 개수",
                "airHoldDuration" => "공중 대기시간",
                "telegraphHoldDuration" => "충전 완료 유지시간",
                "fallSpeedCurve" => "낙하 가속 곡선",
                "warningMessage" => "패턴 경고 문구",
                "warningMessageDuration" => "경고 문구 유지시간",
                "barrageStartDelay" => "문구 시작 후 탄막 생성 대기시간",
                "impactRadius" => "착탄 피해 반지름",
                "minimumSpacing" => "탄막 최소 배치 간격",
                "commanderSafetyRadius" => "군단장 주변 최소 안전거리",
                "initialPoolSize" => "풀 초기 준비 개수",
                "telegraphColor" => "경고 장판 색상",
                _ => ObjectNames.NicifyVariableName(propertyName)
            };
        }

        public static string FallingBarragePhase(string propertyName)
        {
            return propertyName switch
            {
                "selectionChance" => "패턴 등장 확률",
                "waveCount" => "반복 묶음 횟수",
                "waveInterval" => "묶음 사이 간격",
                "spawnInterval" => "기본 생성 간격",
                "spawnTimeJitter" => "생성 시간 무작위 범위",
                "fallDuration" => "착탄까지 걸리는 시간",
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
            System.Func<string, string> labelResolver,
            System.Func<SerializedProperty, bool> shouldDraw = null)
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
                    if (shouldDraw != null && !shouldDraw(child))
                    {
                        if (!child.NextVisible(false))
                        {
                            break;
                        }

                        continue;
                    }

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
            System.Func<string, string> labelResolver,
            System.Func<SerializedProperty, bool> shouldDraw = null)
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
                            if (shouldDraw != null && !shouldDraw(child))
                            {
                                if (!child.NextVisible(false))
                                {
                                    break;
                                }

                                continue;
                            }

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

    internal static class FallenCommanderAdvancedPropertyGUI
    {
        public static void Draw(
            Rect position,
            SerializedProperty property,
            GUIContent label,
            System.Func<string, string> labelResolver,
            string[] commonProperties,
            string[] advancedProperties,
            string advancedTitle)
        {
            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(
                position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;
            DrawProperties(ref line, property, labelResolver, commonProperties);
            line.y += line.height + EditorGUIUtility.standardVerticalSpacing;
            line.height = EditorGUIUtility.singleLineHeight;
            var advancedExpanded = GetAdvancedExpanded(property);
            var nextExpanded = EditorGUI.Foldout(
                line,
                advancedExpanded,
                advancedTitle,
                true);
            if (nextExpanded != advancedExpanded)
            {
                SetAdvancedExpanded(property, nextExpanded);
                advancedExpanded = nextExpanded;
            }

            if (advancedExpanded)
            {
                EditorGUI.indentLevel++;
                DrawProperties(ref line, property, labelResolver, advancedProperties);
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        public static float GetHeight(
            SerializedProperty property,
            System.Func<string, string> labelResolver,
            string[] commonProperties,
            string[] advancedProperties,
            string advancedTitle)
        {
            var height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
            {
                return height;
            }

            height += GetPropertiesHeight(property, labelResolver, commonProperties);
            height += EditorGUIUtility.standardVerticalSpacing +
                EditorGUIUtility.singleLineHeight;
            if (GetAdvancedExpanded(property))
            {
                height += GetPropertiesHeight(property, labelResolver, advancedProperties);
            }

            return height;
        }

        private static void DrawProperties(
            ref Rect line,
            SerializedProperty owner,
            System.Func<string, string> labelResolver,
            string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                var child = owner.FindPropertyRelative(propertyName);
                if (child == null)
                {
                    continue;
                }

                var childLabel = new GUIContent(labelResolver(child.name));
                line.y += line.height + EditorGUIUtility.standardVerticalSpacing;
                line.height = EditorGUI.GetPropertyHeight(child, childLabel, true);
                EditorGUI.PropertyField(line, child, childLabel, true);
            }
        }

        private static float GetPropertiesHeight(
            SerializedProperty owner,
            System.Func<string, string> labelResolver,
            string[] propertyNames)
        {
            var height = 0f;
            foreach (var propertyName in propertyNames)
            {
                var child = owner.FindPropertyRelative(propertyName);
                if (child == null)
                {
                    continue;
                }

                var childLabel = new GUIContent(labelResolver(child.name));
                height += EditorGUIUtility.standardVerticalSpacing +
                    EditorGUI.GetPropertyHeight(child, childLabel, true);
            }

            return height;
        }

        private static bool GetAdvancedExpanded(SerializedProperty property)
        {
            return SessionState.GetBool(GetStateKey(property), false);
        }

        private static void SetAdvancedExpanded(SerializedProperty property, bool expanded)
        {
            SessionState.SetBool(GetStateKey(property), expanded);
        }

        private static string GetStateKey(SerializedProperty property)
        {
            var targetId = property.serializedObject.targetObject == null
                ? 0
                : property.serializedObject.targetObject.GetInstanceID();
            return $"ProjectMT.FallenCommander.TwistedAdvanced.{targetId}." +
                property.propertyPath;
        }
    }

    [CustomPropertyDrawer(typeof(FallenCommanderBasicAttackData))]
    public sealed class FallenCommanderBasicAttackDataDrawer : PropertyDrawer
    {
        private static readonly string[] CommonProperties =
        {
            "telegraphPrefab",
            "warningDuration",
            "telegraphHoldDuration",
            "projectilePrefab",
            "effects",
            "damageDelay",
            "projectileSpeed",
            "repeatInterval"
        };

        private static readonly string[] AdvancedProperties =
        {
            "projectileRadius",
            "maxDistance",
            "projectileHeight",
            "patternOverlapDelay"
        };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            FallenCommanderAdvancedPropertyGUI.Draw(
                position,
                property,
                label,
                FallenCommanderInspectorLabels.BasicAttack,
                CommonProperties,
                AdvancedProperties,
                "기본 공격 투사체 전용 설정 더보기");
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return FallenCommanderAdvancedPropertyGUI.GetHeight(
                property,
                FallenCommanderInspectorLabels.BasicAttack,
                CommonProperties,
                AdvancedProperties,
                "기본 공격 투사체 전용 설정 더보기");
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
                FallenCommanderInspectorLabels.Attack,
                child => ShouldDrawProperty(property, child));
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return FallenCommanderLocalizedPropertyGUI.GetHeight(
                property,
                FallenCommanderInspectorLabels.Attack,
                child => ShouldDrawProperty(property, child));
        }

        private static bool ShouldDrawProperty(
            SerializedProperty owner,
            SerializedProperty child)
        {
            if (child.name != "stunDuration")
            {
                return true;
            }

            return owner.FindPropertyRelative("useStun")?.boolValue == true;
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
                    "효과 설정",
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

        // 기본 공격은 시전 슬롯을 숨기고 블랙홀 종료 연출은 시전·적중 슬롯을 숨긴다.
        private static bool ShouldDrawProperty(
            SerializedProperty owner,
            string propertyName)
        {
            if (owner.propertyPath == "projectileBasicAttack.effects" &&
                propertyName.StartsWith("start"))
            {
                return false;
            }

            if (owner.propertyPath == "blackHoleEndEffects")
            {
                return !propertyName.StartsWith("start") &&
                    !propertyName.StartsWith("hit");
            }

            if (owner.propertyPath == "fallingBarrage.impactEffects")
            {
                return propertyName.StartsWith("resolve") || propertyName == "sfxVolume";
            }

            if (!propertyName.StartsWith("hit"))
            {
                return true;
            }

            return owner.propertyPath != "blackHoleEndEffects";
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
                "projectileBasicAttack.effects" => "발동",
                "blackHoleEndEffects" => "종료",
                "fallingBarrage.impactEffects" => "착탄",
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

    [CustomPropertyDrawer(typeof(FallenCommanderTwistedBattlefieldData))]
    public sealed class FallenCommanderTwistedBattlefieldDataDrawer : PropertyDrawer
    {
        private static readonly string[] CommonProperties =
        {
            "telegraphPrefab",
            "effects",
            "damageDelay",
            "preCastMotion",
            "preCastMotionSpeed",
            "preCastMotionStart",
            "preCastMotionEnd",
            "castMotion",
            "castMotionSpeed",
            "castMotionStart",
            "castMotionEnd"
        };

        private static readonly string[] AdvancedProperties =
        {
            "arenaHalfExtents",
            "columnCount",
            "rowCount",
            "tileGap",
            "attackInterval",
            "dangerColor",
            "safeColor"
        };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            FallenCommanderAdvancedPropertyGUI.Draw(
                position,
                property,
                label,
                FallenCommanderInspectorLabels.TwistedBattlefield,
                CommonProperties,
                AdvancedProperties,
                "연속 장판 전용 설정 더보기");
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return FallenCommanderAdvancedPropertyGUI.GetHeight(
                property,
                FallenCommanderInspectorLabels.TwistedBattlefield,
                CommonProperties,
                AdvancedProperties,
                "연속 장판 전용 설정 더보기");
        }
    }

    [CustomPropertyDrawer(typeof(FallenCommanderLegacyFallingBarrageData))]
    public sealed class FallenCommanderLegacyFallingBarrageDataDrawer : PropertyDrawer
    {
        private static readonly string[] CommonProperties =
        {
            "warningMessage",
            "warningMessageDuration",
            "barrageStartDelay",
            "projectilePrefab",
            "projectileSpawnPresentationDuration",
            "telegraphPrefab",
            "arenaHalfExtents",
            "spawnHeight",
            "projectileCount",
            "airHoldDuration",
            "telegraphHoldDuration",
            "impactRadius",
            "damageDelay",
            "preCastMotion",
            "preCastMotionSpeed",
            "preCastMotionStart",
            "preCastMotionEnd",
            "castMotion",
            "castMotionSpeed",
            "castMotionStart",
            "castMotionEnd"
        };

        private static readonly string[] AdvancedProperties =
        {
            "fallSpeedCurve",
            "minimumSpacing",
            "commanderSafetyRadius",
            "initialPoolSize",
            "telegraphColor"
        };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            FallenCommanderAdvancedPropertyGUI.Draw(
                position,
                property,
                label,
                FallenCommanderInspectorLabels.FallingBarrage,
                CommonProperties,
                AdvancedProperties,
                "낙하 탄막 전용 설정 더보기");
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return FallenCommanderAdvancedPropertyGUI.GetHeight(
                property,
                FallenCommanderInspectorLabels.FallingBarrage,
                CommonProperties,
                AdvancedProperties,
                "낙하 탄막 전용 설정 더보기");
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
                FallenCommanderInspectorLabels.Phase,
                ShouldDrawProperty);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return FallenCommanderLocalizedPropertyGUI.GetHeight(
                property,
                FallenCommanderInspectorLabels.Phase,
                ShouldDrawProperty);
        }

        private static bool ShouldDrawProperty(SerializedProperty child)
        {
            return child.name != "bossPrefabOverride" &&
                   child.name != "bossScaleMultiplier" &&
                   child.name != "transitionFadeColor" &&
                   child.name != "transitionFadeAlpha" &&
                   child.name != "transitionFadeDuration";
        }
    }

    [CustomPropertyDrawer(typeof(FallenCommanderFallingBarrageData))]
    public sealed class FallenCommanderFallingBarrageDataDrawer : PropertyDrawer
    {
        private static readonly string[] CommonProperties =
        {
            "warningMessage", "warningMessageDuration", "barrageStartDelay",
            "projectilePrefab", "telegraphPrefab", "effects", "impactEffects", "arenaHalfExtents",
            "spawnHeight", "projectileCount", "airHoldDuration", "telegraphHoldDuration",
            "impactRadius", "damageDelay", "preCastMotion", "preCastMotionSpeed",
            "preCastMotionStart", "preCastMotionEnd", "castMotion", "castMotionSpeed",
            "castMotionStart", "castMotionEnd"
        };

        private static readonly string[] AdvancedProperties =
        {
            "fallSpeedCurve", "minimumSpacing", "commanderSafetyRadius", "initialPoolSize"
        };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            FallenCommanderAdvancedPropertyGUI.Draw(
                position, property, label, FallenCommanderInspectorLabels.FallingBarrage,
                CommonProperties, AdvancedProperties, "낙하 탄막 2 전용 설정 더보기");
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return FallenCommanderAdvancedPropertyGUI.GetHeight(
                property, FallenCommanderInspectorLabels.FallingBarrage,
                CommonProperties, AdvancedProperties, "낙하 탄막 2 전용 설정 더보기");
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
        private void OnDisable()
        {
            FallenCommanderPhaseTransitionEditorPreview.Stop();
        }

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
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "페이즈 전용 설정",
                EditorStyles.boldLabel);
            DrawPhaseTwoPresentation(
                FindPhaseProperty(FallenCommanderBossPhase.Phase2));
            DrawPhaseThreePresentation(
                FindPhaseProperty(FallenCommanderBossPhase.Phase3));
            DrawTransitionPreviewButtons(
                FindPhaseProperty(FallenCommanderBossPhase.Phase2),
                FindPhaseProperty(FallenCommanderBossPhase.Phase3));
            serializedObject.ApplyModifiedProperties();
        }

        private SerializedProperty FindPhaseProperty(FallenCommanderBossPhase phase)
        {
            var phases = serializedObject.FindProperty("phases");
            if (phases == null)
            {
                return null;
            }

            for (var index = 0; index < phases.arraySize; index++)
            {
                var item = phases.GetArrayElementAtIndex(index);
                var phaseProperty = item.FindPropertyRelative("phase");
                if (phaseProperty != null && phaseProperty.intValue == (int)phase)
                {
                    return item;
                }
            }

            return null;
        }

        private static void DrawPhaseTwoPresentation(SerializedProperty phase)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("2페이즈 전용 설정", EditorStyles.boldLabel);
                DrawProperty(
                    phase,
                    "bossPrefabOverride",
                    "보스 프리팹 교체");
                DrawTransitionScreenFade(phase);
            }
        }

        private static void DrawPhaseThreePresentation(SerializedProperty phase)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("3페이즈 전용 설정", EditorStyles.boldLabel);
                DrawProperty(phase, "bossScaleMultiplier", "보스 크기 배율");
                DrawTransitionScreenFade(phase);
            }
        }

        private static void DrawProperty(
            SerializedProperty phase,
            string propertyName,
            string label)
        {
            var property = phase?.FindPropertyRelative(propertyName);
            if (property == null)
            {
                EditorGUILayout.LabelField(label, "설정 없음");
                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent(label));
        }

        private static void DrawTransitionScreenFade(SerializedProperty phase)
        {
            EditorGUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
            EditorGUILayout.LabelField("전환 화면 가리기", EditorStyles.boldLabel);
            DrawProperty(phase, "transitionFadeColor", "화면 가리기 색상");
            DrawProperty(phase, "transitionFadeAlpha", "화면 가리기 최대 어두움");
            DrawProperty(phase, "transitionFadeDuration", "화면 가리기 페이드 시간");
        }

        private static void DrawTransitionPreviewButtons(
            SerializedProperty phaseTwo,
            SerializedProperty phaseThree)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("2페이즈 전환"))
                {
                    PlayTransitionPreview(phaseTwo);
                }

                if (GUILayout.Button("3페이즈 전환"))
                {
                    PlayTransitionPreview(phaseThree);
                }

                if (GUILayout.Button("재시작"))
                {
                    RestartTransitionPreview();
                }
            }
        }

        private static void PlayTransitionPreview(SerializedProperty phaseProperty)
        {
            if (!TryGetPreviewContext(
                    phaseProperty,
                    out var config,
                    out var phase,
                    out var baseBossPrefab,
                    out var spawnPoint,
                    out var fadeColor,
                    out var fadeAlpha,
                    out var fadeDuration) ||
                !FallenCommanderPhaseTransitionEditorPreview.Play(
                    config,
                    phase,
                    baseBossPrefab,
                    spawnPoint,
                    fadeColor,
                    fadeAlpha,
                    fadeDuration))
            {
                EditorUtility.DisplayDialog(
                    "페이즈 전환 미리보기",
                    "선택한 페이즈의 보스 프리팹과 전환 설정을 확인해 주세요.",
                    "확인");
            }
        }

        private static void RestartTransitionPreview()
        {
            if (!FallenCommanderPhaseTransitionEditorPreview.Restart())
            {
                EditorUtility.DisplayDialog(
                    "페이즈 전환 미리보기",
                    "먼저 2페이즈 또는 3페이즈 전환을 실행해 주세요.",
                    "확인");
            }
        }

        private static bool TryGetPreviewContext(
            SerializedProperty phaseProperty,
            out FallenCommanderPhaseConfig config,
            out FallenCommanderBossPhase phase,
            out GameObject baseBossPrefab,
            out Transform spawnPoint,
            out Color fadeColor,
            out float fadeAlpha,
            out float fadeDuration)
        {
            config = null;
            phase = FallenCommanderBossPhase.Phase1;
            baseBossPrefab = null;
            spawnPoint = null;
            fadeColor = Color.black;
            fadeAlpha = 1f;
            fadeDuration = 0.15f;
            if (phaseProperty == null ||
                phaseProperty.serializedObject.targetObject is not FallenCommanderPhaseConfig phaseConfig)
            {
                return false;
            }

            var phasePropertyValue = phaseProperty.FindPropertyRelative("phase");
            if (phasePropertyValue == null)
            {
                return false;
            }

            var controllers = Object.FindObjectsByType<FallenCommanderController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < controllers.Length; index++)
            {
                var controllerData = new SerializedObject(controllers[index]);
                var prefab = controllerData.FindProperty("bossPrefab")
                    ?.objectReferenceValue as GameObject;
                var controllerSpawnPoint = controllerData.FindProperty("bossSpawnPoint")
                    ?.objectReferenceValue as Transform;
                if (prefab != null)
                {
                    config = phaseConfig;
                    phase = (FallenCommanderBossPhase)phasePropertyValue.intValue;
                    baseBossPrefab = prefab;
                    spawnPoint = controllerSpawnPoint;
                    ResolvePreviewFadeSettings(
                        phaseProperty,
                        out fadeColor,
                        out fadeAlpha,
                        out fadeDuration);
                    return true;
                }
            }

            return false;
        }

        private static void ResolvePreviewFadeSettings(
            SerializedProperty phase,
            out Color fadeColor,
            out float fadeAlpha,
            out float fadeDuration)
        {
            fadeColor = Color.black;
            fadeAlpha = 1f;
            fadeDuration = 0.15f;
            var color = phase?.FindPropertyRelative("transitionFadeColor");
            var alpha = phase?.FindPropertyRelative("transitionFadeAlpha");
            var duration = phase?.FindPropertyRelative("transitionFadeDuration");
            if (color != null)
            {
                fadeColor = color.colorValue;
            }

            if (alpha != null)
            {
                fadeAlpha = alpha.floatValue;
            }

            if (duration != null)
            {
                fadeDuration = duration.floatValue;
            }
        }
    }

    [CustomPropertyDrawer(typeof(FallenCommanderBlackHolePhaseData))]
    public sealed class FallenCommanderBlackHolePhaseDataDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            FallenCommanderLocalizedPropertyGUI.Draw(
                position,
                property,
                label,
                FallenCommanderInspectorLabels.BlackHolePhase);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return FallenCommanderLocalizedPropertyGUI.GetHeight(
                property,
                FallenCommanderInspectorLabels.BlackHolePhase);
        }
    }

    [CustomPropertyDrawer(typeof(FallenCommanderTwistedBattlefieldPhaseData))]
    public sealed class FallenCommanderTwistedBattlefieldPhaseDataDrawer : PropertyDrawer
    {
        private static readonly string[] CommonProperties =
        {
            "warningDuration",
            "telegraphHoldDuration"
        };

        private static readonly string[] AdvancedProperties =
        {
            "selectionChance",
            "beatCount",
            "beatInterval"
        };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            FallenCommanderAdvancedPropertyGUI.Draw(
                position,
                property,
                label,
                FallenCommanderInspectorLabels.TwistedBattlefieldPhase,
                CommonProperties,
                AdvancedProperties,
                "연속 장판 전용 설정 더보기");
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return FallenCommanderAdvancedPropertyGUI.GetHeight(
                property,
                FallenCommanderInspectorLabels.TwistedBattlefieldPhase,
                CommonProperties,
                AdvancedProperties,
                "연속 장판 전용 설정 더보기");
        }
    }

    [CustomPropertyDrawer(typeof(FallenCommanderFallingBarragePhaseData))]
    public sealed class FallenCommanderFallingBarragePhaseDataDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            FallenCommanderLocalizedPropertyGUI.Draw(
                position, property, label, FallenCommanderInspectorLabels.FallingBarragePhase);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return FallenCommanderLocalizedPropertyGUI.GetHeight(
                property, FallenCommanderInspectorLabels.FallingBarragePhase);
        }
    }
}
