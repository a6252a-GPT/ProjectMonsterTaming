using System.Collections.Generic;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    internal sealed class MonsterPassiveBalanceEditor // 공용 원본은 적용 전까지 건드리지 않습니다.
    {
        private GenericMonsterPassiveSkill skill;
        private float primaryBase;
        private float primaryStep;
        private float secondaryBase;
        private float secondaryStep;
        private int triggerCount;
        private int maxStacks;
        private float duration;
        private float cooldown;
        private float threshold;
        private float radius;
        private int maxTargets;

        public GenericMonsterPassiveSkill Skill => skill;

        public bool HasPendingChanges => skill != null &&
            (!Mathf.Approximately(primaryBase, skill.PrimaryBase) ||
             !Mathf.Approximately(primaryStep, skill.PrimaryPerLevelStep) ||
             !Mathf.Approximately(secondaryBase, skill.SecondaryBase) ||
             !Mathf.Approximately(secondaryStep, skill.SecondaryPerLevelStep) ||
             triggerCount != skill.TriggerCount ||
             maxStacks != skill.MaxStacks ||
             !Mathf.Approximately(duration, skill.Duration) ||
             !Mathf.Approximately(cooldown, skill.Cooldown) ||
             !Mathf.Approximately(threshold, skill.Threshold) ||
             !Mathf.Approximately(radius, skill.Radius) ||
             maxTargets != skill.MaxTargets);

        public bool TrySelect(GenericMonsterPassiveSkill next)
        {
            if (ReferenceEquals(skill, next))
            {
                return true;
            }

            if (skill != null && HasPendingChanges)
            {
                var choice = EditorUtility.DisplayDialogComplex(
                    "공용 패시브 변경사항",
                    $"{skill.DisplayName}의 미적용 수치가 있습니다. 어떻게 처리할까요?",
                    "적용 후 전환",
                    "전환 취소",
                    "변경 버리기");
                if (choice == 0)
                {
                    if (!TryApply(out var error))
                    {
                        EditorUtility.DisplayDialog("패시브 수치 적용 실패", error, "확인");
                        return false;
                    }
                }
                else if (choice == 1)
                {
                    return false;
                }
            }

            Load(next);
            return true;
        }

        public void Draw(MonsterRarityCatalog rarityCatalog, ref bool expanded)
        {
            if (skill == null)
            {
                return;
            }

            if (skill.NeedsRuntimeInitialization)
            {
                EditorGUILayout.HelpBox(
                    "선택한 패시브는 아직 실행 수치 Profile이 없습니다. 실행 기능 연결 뒤 수치를 조절할 수 있습니다.",
                    MessageType.Warning);
                return;
            }

            GUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                expanded = EditorGUILayout.Foldout(expanded, "공용 패시브 수치 조절", true);
                GUILayout.Label(BuildCompactSummary(), EditorStyles.wordWrappedMiniLabel);
                if (!expanded)
                {
                    return;
                }

                var users = CollectUsers(rarityCatalog);
                EditorGUILayout.HelpBox(
                    users.Count == 0
                        ? "현재 게임 데이터에서 이 패시브를 사용하는 몬스터가 없습니다."
                        : $"공용 원본입니다. 적용하면 다음 {users.Count}종이 함께 변경됩니다.\n{string.Join(", ", users)}",
                    users.Count == 0 ? MessageType.Info : MessageType.Warning);

                EditorGUILayout.LabelField("실행 방식", GetKindLabel(skill.RuntimeKind));
                DrawEffectFields();
                DrawContentRule();

                GUILayout.Space(4f);
                GUILayout.Label("레벨별 최종 수치", EditorStyles.boldLabel);
                DrawLevelPreview("Lv1", 0);
                DrawLevelPreview("Lv20", 1);
                DrawLevelPreview("Lv100", 5);
                DrawLevelPreview("Lv200", 10);

                var valid = TryValidateDraft(out var error);
                if (!valid)
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
                else if (HasPendingChanges)
                {
                    EditorGUILayout.HelpBox("아직 공용 자산에 적용하지 않은 변경사항입니다.", MessageType.Info);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!HasPendingChanges))
                    {
                        if (GUILayout.Button("변경 취소"))
                        {
                            Load(skill);
                            GUI.FocusControl(null);
                        }
                    }

                    using (new EditorGUI.DisabledScope(!HasPendingChanges || !valid))
                    {
                        if (GUILayout.Button($"공용값 적용{(users.Count > 0 ? $" · {users.Count}종" : string.Empty)}"))
                        {
                            if (!TryApply(out error))
                            {
                                EditorUtility.DisplayDialog("패시브 수치 적용 실패", error, "확인");
                            }
                            else
                            {
                                GUI.FocusControl(null);
                            }
                        }
                    }
                }

                using (new EditorGUI.DisabledScope(HasPendingChanges))
                {
                    if (GUILayout.Button("저장된 공용값 다시 불러오기"))
                    {
                        Load(skill);
                    }
                }
            }
        }

        internal void Load(GenericMonsterPassiveSkill source)
        {
            skill = source;
            if (skill == null)
            {
                return;
            }

            primaryBase = skill.PrimaryBase;
            primaryStep = skill.PrimaryPerLevelStep;
            secondaryBase = skill.SecondaryBase;
            secondaryStep = skill.SecondaryPerLevelStep;
            triggerCount = skill.TriggerCount;
            maxStacks = skill.MaxStacks;
            duration = skill.Duration;
            cooldown = skill.Cooldown;
            threshold = skill.Threshold;
            radius = skill.Radius;
            maxTargets = skill.MaxTargets;
        }

        internal bool TryApply(out string error)
        {
            if (!TryValidateDraft(out error))
            {
                return false;
            }

            Undo.RecordObject(skill, "공용 패시브 수치 조절");
            skill.EditorConfigureRuntime(
                skill.RuntimeKind,
                primaryBase,
                primaryStep,
                secondaryBase,
                secondaryStep,
                triggerCount,
                maxStacks,
                duration,
                cooldown,
                threshold,
                radius,
                maxTargets);
            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssetIfDirty(skill);
            Load(skill);
            error = string.Empty;
            return true;
        }

        private void DrawEffectFields()
        {
            primaryBase = DrawPercent(GetPrimaryLabel(skill.RuntimeKind), primaryBase);
            primaryStep = DrawPercent("20레벨당 증가", primaryStep);

            if (UsesSecondary(skill.RuntimeKind))
            {
                secondaryBase = DrawPercent(GetSecondaryLabel(skill.RuntimeKind), secondaryBase);
                secondaryStep = DrawPercent("보조값 20레벨당 증가", secondaryStep);
            }

            if (UsesTriggerCount(skill.RuntimeKind))
            {
                triggerCount = Mathf.Max(1, EditorGUILayout.IntField("발동 필요 타수", triggerCount));
            }

            if (skill.RuntimeKind == GenericMonsterPassiveRuntimeKind.SameTargetHaste)
            {
                maxStacks = Mathf.Max(1, EditorGUILayout.IntField("최대 중첩", maxStacks));
            }

            if (UsesDuration(skill.RuntimeKind))
            {
                duration = Mathf.Max(0f, EditorGUILayout.FloatField("지속시간 (초)", duration));
            }

            if (UsesCooldown(skill.RuntimeKind))
            {
                cooldown = Mathf.Max(0f, EditorGUILayout.FloatField("재사용 대기 (초)", cooldown));
            }

            if (UsesHealthThreshold(skill.RuntimeKind))
            {
                threshold = EditorGUILayout.Slider("발동 체력 기준 (%)", threshold * 100f, 1f, 100f) / 100f;
            }
            else if (skill.RuntimeKind == GenericMonsterPassiveRuntimeKind.LongRangeAim)
            {
                threshold = Mathf.Max(0f, EditorGUILayout.FloatField("최소 거리 (m)", threshold));
            }

            if (UsesRadius(skill.RuntimeKind))
            {
                radius = Mathf.Max(0f, EditorGUILayout.FloatField("효과 반경 (m)", radius));
            }

            if (skill.RuntimeKind == GenericMonsterPassiveRuntimeKind.RallySplash)
            {
                maxTargets = Mathf.Max(1, EditorGUILayout.IntField("추가 대상 수", maxTargets));
            }
        }

        private void DrawContentRule()
        {
            var message = skill.RuntimeKind switch
            {
                GenericMonsterPassiveRuntimeKind.EmergencyEntry =>
                    "MainBattle은 예비 교체 합류 때만 자신·아군 보호막이 발동합니다. 육각 수동 배치는 자신 보호막의 50%만 적용합니다.",
                GenericMonsterPassiveRuntimeKind.FirstWave =>
                    "MainBattle 초기·예비 배치와 육각 수동 배치 직후에 같은 지속시간으로 발동합니다.",
                GenericMonsterPassiveRuntimeKind.FrontlineBond =>
                    "현재 실행 조건은 반경 안에 자신을 제외한 아군 2기 이상입니다.",
                GenericMonsterPassiveRuntimeKind.ThreatMark =>
                    "MainBattle은 원거리·보스, 육각은 수비대·포탑을 고위협 대상으로 판단합니다.",
                _ => "MainBattle과 육각 군단의 역습이 이 공용 수치를 함께 사용합니다."
            };
            EditorGUILayout.HelpBox(message, MessageType.None);
        }

        private void DrawLevelPreview(string label, int stage)
        {
            var primary = primaryBase + primaryStep * stage;
            var text = $"주 효과 {FormatPercent(primary)}";
            if (UsesSecondary(skill.RuntimeKind))
            {
                text += $"  ·  보조 효과 {FormatPercent(secondaryBase + secondaryStep * stage)}";
            }
            EditorGUILayout.LabelField(label, text);
        }

        private string BuildCompactSummary()
        {
            var levelTwoHundred = primaryBase + primaryStep * 10f;
            var summary = $"{GetKindLabel(skill.RuntimeKind)}  ·  Lv1 {FormatPercent(primaryBase)} → Lv200 {FormatPercent(levelTwoHundred)}";
            if (UsesSecondary(skill.RuntimeKind))
            {
                summary += $"  ·  보조 {FormatPercent(secondaryBase)} → {FormatPercent(secondaryBase + secondaryStep * 10f)}";
            }
            return summary;
        }

        private bool TryValidateDraft(out string error)
        {
            if (skill == null || skill.NeedsRuntimeInitialization)
            {
                error = "실행 수치 Profile이 연결된 공용 패시브만 조절할 수 있습니다.";
                return false;
            }

            if (primaryBase < 0f || primaryStep < 0f || secondaryBase < 0f || secondaryStep < 0f)
            {
                error = "효과 수치와 레벨 증가량은 0 이상이어야 합니다.";
                return false;
            }

            if (UsesDuration(skill.RuntimeKind) && duration <= 0f)
            {
                error = "이 패시브의 지속시간은 0보다 커야 합니다.";
                return false;
            }

            if (skill.RuntimeKind == GenericMonsterPassiveRuntimeKind.LongRangeAim && threshold <= 0f)
            {
                error = "장거리 조준의 최소 거리는 0보다 커야 합니다.";
                return false;
            }

            if (UsesRadius(skill.RuntimeKind) && radius <= 0f)
            {
                error = "이 패시브의 효과 반경은 0보다 커야 합니다.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private List<string> CollectUsers(MonsterRarityCatalog catalog)
        {
            var result = new List<string>();
            if (catalog == null || skill == null)
            {
                return result;
            }

            for (var index = 0; index < catalog.CommonToEpicEntries.Count; index++)
            {
                var entry = catalog.CommonToEpicEntries[index];
                if (entry?.Monster != null && ReferenceEquals(entry.PassiveSkill, skill))
                {
                    result.Add($"{entry.Monster.DisplayName} [{entry.Monster.MonsterId}]");
                }
            }

            for (var index = 0; index < catalog.LegendaryMythicEntries.Count; index++)
            {
                var entry = catalog.LegendaryMythicEntries[index];
                if (entry?.Monster != null && ReferenceEquals(entry.PassiveSkill, skill))
                {
                    result.Add($"{entry.Monster.DisplayName} [{entry.Monster.MonsterId}]");
                }
            }

            return result;
        }

        private static float DrawPercent(string label, float value)
        {
            return Mathf.Max(0f, EditorGUILayout.FloatField(label, value * 100f)) / 100f;
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
                   kind == GenericMonsterPassiveRuntimeKind.RallySplash ||
                   kind == GenericMonsterPassiveRuntimeKind.FractureMark ||
                   kind == GenericMonsterPassiveRuntimeKind.HealingShot;
        }

        private static bool UsesDuration(GenericMonsterPassiveRuntimeKind kind)
        {
            return kind == GenericMonsterPassiveRuntimeKind.SameTargetHaste ||
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

        private static bool UsesRadius(GenericMonsterPassiveRuntimeKind kind)
        {
            return kind == GenericMonsterPassiveRuntimeKind.RallySplash ||
                   kind == GenericMonsterPassiveRuntimeKind.FrontlineBond;
        }

        private static string GetPrimaryLabel(GenericMonsterPassiveRuntimeKind kind)
        {
            return kind switch
            {
                GenericMonsterPassiveRuntimeKind.RhythmPower => "강화 공격 추가 피해 (%)",
                GenericMonsterPassiveRuntimeKind.SameTargetHaste => "중첩당 공격속도 증가 (%)",
                GenericMonsterPassiveRuntimeKind.RallySplash => "주변 피해 계수 (%)",
                GenericMonsterPassiveRuntimeKind.LowHealthHunter => "저체력 대상 추가 피해 (%)",
                GenericMonsterPassiveRuntimeKind.LongRangeAim => "장거리 추가 피해 (%)",
                GenericMonsterPassiveRuntimeKind.CrisisDefense => "받는 피해 감소 (%)",
                GenericMonsterPassiveRuntimeKind.FrontlineBond => "받는 피해 감소 (%)",
                GenericMonsterPassiveRuntimeKind.FractureMark => "팀 피해 노출 (%)",
                GenericMonsterPassiveRuntimeKind.ThreatMark => "개인 추가 피해 (%)",
                GenericMonsterPassiveRuntimeKind.KillHeal => "최대 체력 회복 (%)",
                GenericMonsterPassiveRuntimeKind.CourageAura => "아군 공격력 증가 (%)",
                GenericMonsterPassiveRuntimeKind.HealingShot => "공격력 기반 회복 계수 (%)",
                GenericMonsterPassiveRuntimeKind.EmergencyEntry => "자신 최대 체력 보호막 (%)",
                GenericMonsterPassiveRuntimeKind.FirstWave => "공격력 증가 (%)",
                _ => "주 효과 (%)"
            };
        }

        private static string GetSecondaryLabel(GenericMonsterPassiveRuntimeKind kind)
        {
            return kind switch
            {
                GenericMonsterPassiveRuntimeKind.ThreatMark => "팀 피해 노출 (%)",
                GenericMonsterPassiveRuntimeKind.EmergencyEntry => "아군 최대 체력 보호막 (%)",
                _ => "보조 효과 (%)"
            };
        }

        private static string GetKindLabel(GenericMonsterPassiveRuntimeKind kind)
        {
            return kind switch
            {
                GenericMonsterPassiveRuntimeKind.RhythmPower => "박자 강화",
                GenericMonsterPassiveRuntimeKind.SameTargetHaste => "가속 연타",
                GenericMonsterPassiveRuntimeKind.RallySplash => "폭발 타격",
                GenericMonsterPassiveRuntimeKind.LowHealthHunter => "피 냄새",
                GenericMonsterPassiveRuntimeKind.LongRangeAim => "장거리 조준",
                GenericMonsterPassiveRuntimeKind.CrisisDefense => "위기 방어",
                GenericMonsterPassiveRuntimeKind.FrontlineBond => "진형 결속",
                GenericMonsterPassiveRuntimeKind.FractureMark => "약점 누적",
                GenericMonsterPassiveRuntimeKind.ThreatMark => "후열 사냥",
                GenericMonsterPassiveRuntimeKind.KillHeal => "흡수 본능",
                GenericMonsterPassiveRuntimeKind.CourageAura => "용기 오라",
                GenericMonsterPassiveRuntimeKind.HealingShot => "치유 탄환",
                GenericMonsterPassiveRuntimeKind.EmergencyEntry => "합류 보호막",
                GenericMonsterPassiveRuntimeKind.FirstWave => "첫 파도",
                _ => "미연결"
            };
        }
    }
}
