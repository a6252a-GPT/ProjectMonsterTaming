using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    internal sealed class MonsterPassiveBalanceEditor // 현재 몬스터의 전용 수치만 편집합니다.
    {
        public void Draw(
            GenericMonsterPassiveSkill template,
            SerializedProperty tuning,
            string monsterName,
            ref bool expanded)
        {
            if (template == null || tuning == null)
            {
                return;
            }

            EnsureInitialized(tuning, template, false);
            GUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var ownerLabel = string.IsNullOrWhiteSpace(monsterName) ? "현재 몬스터" : monsterName.Trim();
                expanded = EditorGUILayout.Foldout(
                    expanded,
                    $"{ownerLabel} 전용 패시브 설정",
                    true);
                GUILayout.Label(BuildCompactSummary(template, tuning), EditorStyles.wordWrappedMiniLabel);
                if (!expanded)
                {
                    return;
                }

                EditorGUILayout.HelpBox(
                    "여기서 바꾼 수치는 이 몬스터에게만 저장됩니다. 같은 패시브를 쓰는 다른 몬스터는 바뀌지 않습니다.",
                    MessageType.Info);
                DrawEffectFields(template.RuntimeKind, tuning);
                DrawContentRule(template.RuntimeKind);

                GUILayout.Space(4f);
                GUILayout.Label("레벨별 적용값", EditorStyles.boldLabel);
                DrawLevelPreview("Lv1", 0, template.RuntimeKind, tuning);
                DrawLevelPreview("Lv20", 1, template.RuntimeKind, tuning);
                DrawLevelPreview("Lv100", 5, template.RuntimeKind, tuning);
                DrawLevelPreview("Lv200", 10, template.RuntimeKind, tuning);

                if (GUILayout.Button("이 패시브의 기본값으로 되돌리기"))
                {
                    EnsureInitialized(tuning, template, true);
                    GUI.FocusControl(null);
                }
            }
        }

        internal static void EnsureInitialized(
            SerializedProperty tuning,
            GenericMonsterPassiveSkill template,
            bool force)
        {
            if (tuning == null || template == null)
            {
                return;
            }

            var initialized = tuning.FindPropertyRelative("initialized");
            var runtimeKind = tuning.FindPropertyRelative("runtimeKind");
            if (!force && initialized.boolValue && runtimeKind.enumValueIndex == (int)template.RuntimeKind)
            {
                return;
            }

            initialized.boolValue = true;
            runtimeKind.enumValueIndex = (int)template.RuntimeKind;
            SetFloat(tuning, "primaryBase", template.PrimaryBase);
            SetFloat(tuning, "primaryPerLevelStep", template.PrimaryPerLevelStep);
            SetFloat(tuning, "secondaryBase", template.SecondaryBase);
            SetFloat(tuning, "secondaryPerLevelStep", template.SecondaryPerLevelStep);
            SetInt(tuning, "triggerCount", template.TriggerCount);
            SetInt(tuning, "maxStacks", template.MaxStacks);
            SetFloat(tuning, "duration", template.Duration);
            SetFloat(tuning, "cooldown", template.Cooldown);
            SetFloat(tuning, "threshold", template.Threshold);
            SetFloat(tuning, "radius", template.Radius);
            SetInt(tuning, "maxTargets", template.MaxTargets);
        }

        private static void DrawEffectFields(
            GenericMonsterPassiveRuntimeKind kind,
            SerializedProperty tuning)
        {
            DrawPercent(tuning, "primaryBase", GetPrimaryLabel(kind));
            DrawPercent(tuning, "primaryPerLevelStep", "20레벨마다 증가 (%)");

            if (UsesSecondary(kind))
            {
                DrawPercent(tuning, "secondaryBase", GetSecondaryLabel(kind));
                DrawPercent(tuning, "secondaryPerLevelStep", "보조 효과 20레벨마다 증가 (%)");
            }

            if (UsesTriggerCount(kind))
            {
                var property = tuning.FindPropertyRelative("triggerCount");
                property.intValue = Mathf.Max(1, EditorGUILayout.IntField("몇 번째 공격마다", property.intValue));
            }

            if (kind == GenericMonsterPassiveRuntimeKind.SameTargetHaste)
            {
                var property = tuning.FindPropertyRelative("maxStacks");
                property.intValue = Mathf.Max(1, EditorGUILayout.IntField("최대 가속 중첩", property.intValue));
            }

            if (UsesDuration(kind))
            {
                var property = tuning.FindPropertyRelative("duration");
                property.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(GetDurationLabel(kind), property.floatValue));
            }

            if (UsesCooldown(kind))
            {
                var property = tuning.FindPropertyRelative("cooldown");
                property.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField("다시 발동하기까지 (초)", property.floatValue));
            }

            if (UsesHealthThreshold(kind))
            {
                var property = tuning.FindPropertyRelative("threshold");
                property.floatValue = EditorGUILayout.Slider("발동 체력 기준 (%)", property.floatValue * 100f, 1f, 100f) / 100f;
            }
            else if (kind == GenericMonsterPassiveRuntimeKind.LongRangeAim)
            {
                var property = tuning.FindPropertyRelative("threshold");
                property.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField("효과가 켜지는 거리 (m)", property.floatValue));
            }

            if (kind == GenericMonsterPassiveRuntimeKind.FrontlineBond)
            {
                var property = tuning.FindPropertyRelative("radius");
                property.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField("아군을 확인할 거리 (m)", property.floatValue));
            }
        }

        private static void DrawContentRule(GenericMonsterPassiveRuntimeKind kind)
        {
            var message = kind switch
            {
                GenericMonsterPassiveRuntimeKind.ImpactStrike =>
                    "일반 적은 잠깐 경직되고, 경직되지 않는 보스·구조물은 표시된 추가 피해를 받습니다.",
                GenericMonsterPassiveRuntimeKind.EmergencyEntry =>
                    "메인 전투의 예비 교체 합류 때 자신과 체력이 가장 낮은 아군에게 적용됩니다. 군단의 역습 수동 배치는 자신에게 절반만 적용됩니다.",
                GenericMonsterPassiveRuntimeKind.FirstWave =>
                    "전투에 합류하면 즉시 공격력이 상승합니다.",
                GenericMonsterPassiveRuntimeKind.FrontlineBond =>
                    "표시된 거리 안에 자신을 제외한 아군이 2명 이상이면 발동합니다.",
                GenericMonsterPassiveRuntimeKind.ThreatMark =>
                    "메인 전투는 원거리·보스, 군단의 역습은 수비대·포탑을 우선 대상으로 판단합니다.",
                _ => "메인 전투와 군단의 역습에서 같은 전용 수치가 사용됩니다."
            };
            EditorGUILayout.HelpBox(message, MessageType.None);
        }

        private static void DrawLevelPreview(
            string label,
            int stage,
            GenericMonsterPassiveRuntimeKind kind,
            SerializedProperty tuning)
        {
            var primary = GetFloat(tuning, "primaryBase") + GetFloat(tuning, "primaryPerLevelStep") * stage;
            var text = $"주 효과 {FormatPercent(primary)}";
            if (UsesSecondary(kind))
            {
                var secondary = GetFloat(tuning, "secondaryBase") +
                                GetFloat(tuning, "secondaryPerLevelStep") * stage;
                text += $"  ·  보조 효과 {FormatPercent(secondary)}";
            }
            EditorGUILayout.LabelField(label, text);
        }

        private static string BuildCompactSummary(
            GenericMonsterPassiveSkill template,
            SerializedProperty tuning)
        {
            var primary = GetFloat(tuning, "primaryBase");
            var step = GetFloat(tuning, "primaryPerLevelStep");
            var summary = $"{template.DisplayName}  ·  Lv1 {FormatPercent(primary)} → Lv200 {FormatPercent(primary + step * 10f)}";
            if (UsesTriggerCount(template.RuntimeKind))
            {
                summary += $"  ·  {tuning.FindPropertyRelative("triggerCount").intValue}번째 공격마다";
            }
            return summary;
        }

        private static void DrawPercent(SerializedProperty root, string name, string label)
        {
            var property = root.FindPropertyRelative(name);
            property.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(label, property.floatValue * 100f)) / 100f;
        }

        private static void SetFloat(SerializedProperty root, string name, float value)
        {
            root.FindPropertyRelative(name).floatValue = value;
        }

        private static void SetInt(SerializedProperty root, string name, int value)
        {
            root.FindPropertyRelative(name).intValue = value;
        }

        private static float GetFloat(SerializedProperty root, string name)
        {
            return root.FindPropertyRelative(name).floatValue;
        }

        private static string FormatPercent(float value)
        {
            return $"{value * 100f:0.##}%";
        }

        private static bool UsesSecondary(GenericMonsterPassiveRuntimeKind kind)
        {
            return kind == GenericMonsterPassiveRuntimeKind.ThreatMark ||
                   kind == GenericMonsterPassiveRuntimeKind.EmergencyEntry;
        }

        private static bool UsesTriggerCount(GenericMonsterPassiveRuntimeKind kind)
        {
            return kind == GenericMonsterPassiveRuntimeKind.RhythmPower ||
                   kind == GenericMonsterPassiveRuntimeKind.ImpactStrike ||
                   kind == GenericMonsterPassiveRuntimeKind.FractureMark ||
                   kind == GenericMonsterPassiveRuntimeKind.HealingShot;
        }

        private static bool UsesDuration(GenericMonsterPassiveRuntimeKind kind)
        {
            return kind == GenericMonsterPassiveRuntimeKind.SameTargetHaste ||
                   kind == GenericMonsterPassiveRuntimeKind.ImpactStrike ||
                   kind == GenericMonsterPassiveRuntimeKind.CrisisDefense ||
                   kind == GenericMonsterPassiveRuntimeKind.FractureMark ||
                   kind == GenericMonsterPassiveRuntimeKind.ThreatMark ||
                   kind == GenericMonsterPassiveRuntimeKind.EmergencyEntry ||
                   kind == GenericMonsterPassiveRuntimeKind.FirstWave;
        }

        private static bool UsesCooldown(GenericMonsterPassiveRuntimeKind kind)
        {
            return kind == GenericMonsterPassiveRuntimeKind.CrisisDefense ||
                   kind == GenericMonsterPassiveRuntimeKind.KillHeal;
        }

        private static bool UsesHealthThreshold(GenericMonsterPassiveRuntimeKind kind)
        {
            return kind == GenericMonsterPassiveRuntimeKind.LowHealthHunter ||
                   kind == GenericMonsterPassiveRuntimeKind.CrisisDefense;
        }

        private static string GetDurationLabel(GenericMonsterPassiveRuntimeKind kind)
        {
            return kind == GenericMonsterPassiveRuntimeKind.ImpactStrike
                ? "일반 적 경직 시간 (초)"
                : "효과 지속시간 (초)";
        }

        private static string GetPrimaryLabel(GenericMonsterPassiveRuntimeKind kind)
        {
            return kind switch
            {
                GenericMonsterPassiveRuntimeKind.RhythmPower => "강화 공격 추가 피해 (%)",
                GenericMonsterPassiveRuntimeKind.SameTargetHaste => "중첩당 공격속도 증가 (%)",
                GenericMonsterPassiveRuntimeKind.ImpactStrike => "보스·구조물 추가 피해 (%)",
                GenericMonsterPassiveRuntimeKind.LowHealthHunter => "저체력 대상 추가 피해 (%)",
                GenericMonsterPassiveRuntimeKind.LongRangeAim => "장거리 추가 피해 (%)",
                GenericMonsterPassiveRuntimeKind.CrisisDefense => "받는 피해 감소 (%)",
                GenericMonsterPassiveRuntimeKind.FrontlineBond => "받는 피해 감소 (%)",
                GenericMonsterPassiveRuntimeKind.FractureMark => "대상이 더 받는 피해 (%)",
                GenericMonsterPassiveRuntimeKind.ThreatMark => "고위협 대상 추가 피해 (%)",
                GenericMonsterPassiveRuntimeKind.KillHeal => "최대 체력 회복 (%)",
                GenericMonsterPassiveRuntimeKind.CourageAura => "아군 공격력 증가 (%)",
                GenericMonsterPassiveRuntimeKind.HealingShot => "공격력 기반 회복량 (%)",
                GenericMonsterPassiveRuntimeKind.EmergencyEntry => "자신 최대 체력 보호막 (%)",
                GenericMonsterPassiveRuntimeKind.FirstWave => "공격력 증가 (%)",
                _ => "주 효과 (%)"
            };
        }

        private static string GetSecondaryLabel(GenericMonsterPassiveRuntimeKind kind)
        {
            return kind switch
            {
                GenericMonsterPassiveRuntimeKind.ThreatMark => "대상이 더 받는 피해 (%)",
                GenericMonsterPassiveRuntimeKind.EmergencyEntry => "아군 최대 체력 보호막 (%)",
                _ => "보조 효과 (%)"
            };
        }
    }
}
