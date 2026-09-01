using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public static class MonsterActiveAttackBlockContractTemplates // 기본공격 규칙을 쓰되 액티브 Step 소유 계약을 생성
    {
        public static MonsterBasicAttackVfxSlot[] Build(MonsterActiveAttackStep step)
        {
            if (step == null) return Array.Empty<MonsterBasicAttackVfxSlot>();
            var compiled = ScriptableObject.CreateInstance<MonsterBasicAttackProfile>();
            compiled.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                step.EditorCompileAttackBlock(compiled);
                return MonsterBasicAttackVfxContractTemplates.Build(compiled)
                    .Select(slot => slot.EditorClone())
                    .ToArray();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(compiled);
            }
        }

        public static List<MonsterBasicAttackVfxSlot> Reconcile(
            MonsterActiveAttackStep step,
            out MonsterBasicAttackContractReconcileResult result)
        {
            var active = step?.AttackBlockVfxSlots.Where(slot => slot != null).ToList() ??
                         new List<MonsterBasicAttackVfxSlot>();
            var stored = new List<MonsterBasicAttackVfxSlot>(active);
            if (step != null)
            {
                stored.AddRange(step.InactiveAttackBlockVfxSlots.Where(slot => slot != null));
            }

            var templates = Build(step);
            var reconciled = new List<MonsterBasicAttackVfxSlot>(templates.Length);
            var consumed = new HashSet<MonsterBasicAttackVfxSlot>();
            var retained = 0;
            var added = 0;
            foreach (var template in templates)
            {
                var existing = stored.LastOrDefault(candidate =>
                    !consumed.Contains(candidate) &&
                    string.Equals(candidate.SlotId, template.SlotId, StringComparison.OrdinalIgnoreCase));
                var canonical = template.EditorClone();
                if (existing != null)
                {
                    canonical.EditorSetProductionMemo(existing.ProductionMemo);
                    consumed.Add(existing);
                    retained++;
                }
                else
                {
                    added++;
                }
                reconciled.Add(canonical);
            }

            result = new MonsterBasicAttackContractReconcileResult(
                retained,
                added,
                active.Count(candidate => !consumed.Contains(candidate)));
            return reconciled;
        }

        public static bool TryValidateCanonical(
            MonsterActiveAttackStep step,
            out string error)
        {
            if (step == null)
            {
                error = "공격 Step이 비어 있습니다.";
                return false;
            }
            if (step.PresentationSlots.Count > 0)
            {
                error = $"구형 액티브 전용 계약이 남아 있습니다. Step={step.StepId}";
                return false;
            }

            var expected = Build(step);
            if (step.AttackBlockVfxSlots.Count != expected.Length)
            {
                error =
                    $"공용 공격 블록 계약 수가 다릅니다. Step={step.StepId}, " +
                    $"Current={step.AttackBlockVfxSlots.Count}, Expected={expected.Length}";
                return false;
            }
            for (var index = 0; index < expected.Length; index++)
            {
                var actual = step.AttackBlockVfxSlots[index];
                var canonical = expected[index];
                if (!MatchesDefinition(actual, canonical))
                {
                    error =
                        $"공용 공격 블록 계약이 공격 형태와 다릅니다. Step={step.StepId}, " +
                        $"Slot={actual?.SlotId ?? "비어 있음"}, Expected={canonical.SlotId}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public static bool TryValidateCurrent(
            MonsterActiveAttackStep step,
            out string error)
        {
            if (step == null)
            {
                error = "공격 Step이 비어 있습니다.";
                return false;
            }
            if (step.PresentationSlots.Count > 0)
            {
                error = $"구형 액티브 전용 계약이 남아 있습니다. Step={step.StepId}";
                return false;
            }

            var compiled = ScriptableObject.CreateInstance<MonsterBasicAttackProfile>();
            compiled.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                step.EditorCompileAttackBlock(compiled);
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var deliveryVisualCount = 0;
                for (var index = 0; index < step.AttackBlockVfxSlots.Count; index++)
                {
                    var slot = step.AttackBlockVfxSlots[index];
                    var slotError = "공용 공격 블록 공간이 비어 있습니다.";
                    if (slot == null || !slot.TryValidate(out slotError))
                    {
                        error =
                            $"공용 공격 블록 공간 {index + 1:00}이 유효하지 않습니다. " +
                            $"Step={step.StepId}, Detail={slotError}";
                        return false;
                    }
                    if (!ids.Add(slot.SlotId))
                    {
                        error =
                            $"공용 공격 블록 공간 ID가 중복되었습니다. " +
                            $"Step={step.StepId}, Slot={slot.SlotId}";
                        return false;
                    }
                    if (!MonsterBasicAttackVfxCompatibility.TryValidateSlot(
                            compiled, slot, out var compatibilityError))
                    {
                        error =
                            $"공용 공격 블록 공간이 현재 공격 형태에서 발생할 수 없습니다. " +
                            $"Step={step.StepId}, Slot={slot.SlotId}, Detail={compatibilityError}";
                        return false;
                    }
                    if (slot.IsDeliveryVisual) deliveryVisualCount++;
                }
                if (deliveryVisualCount > 1)
                {
                    error =
                        $"이동체 외형 공간은 Step마다 하나만 사용할 수 있습니다. Step={step.StepId}";
                    return false;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(compiled);
            }

            error = string.Empty;
            return true;
        }

        private static bool MatchesDefinition(
            MonsterBasicAttackVfxSlot actual,
            MonsterBasicAttackVfxSlot expected)
        {
            return actual != null && expected != null &&
                   string.Equals(actual.SlotId, expected.SlotId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(actual.DisplayName, expected.DisplayName, StringComparison.Ordinal) &&
                   string.Equals(actual.Description, expected.Description, StringComparison.Ordinal) &&
                   actual.EventType == expected.EventType &&
                   actual.Anchor == expected.Anchor &&
                   actual.Multiplicity == expected.Multiplicity &&
                   actual.AssignmentScope == expected.AssignmentScope &&
                   actual.Attachment == expected.Attachment &&
                   actual.EndPolicy == expected.EndPolicy &&
                   Mathf.Approximately(actual.DefaultLifetime, expected.DefaultLifetime);
        }

        public static IReadOnlyList<string> GetLegacySlotIds(
            MonsterActiveAttackStep step,
            string commonSlotId)
        {
            var ids = new List<string> { commonSlotId };
            if (string.Equals(commonSlotId, "dash_exit", StringComparison.OrdinalIgnoreCase))
                ids.Add("teleport_exit");
            if (string.Equals(commonSlotId, "dash_enter", StringComparison.OrdinalIgnoreCase))
                ids.Add("teleport_enter");
            if (step == null) return ids;

            string legacy = step.Pattern switch
            {
                MonsterActiveAttackPattern.Line when commonSlotId == "thrust_path" => "line_path",
                MonsterActiveAttackPattern.Line when commonSlotId == "path_hit" => "target_hit",
                MonsterActiveAttackPattern.Cone when commonSlotId == "sweep_plane" => "cone_sweep",
                MonsterActiveAttackPattern.SelfCircle when commonSlotId == "ground_contact" => "self_cast",
                MonsterActiveAttackPattern.FrontCircle when commonSlotId == "ground_contact" => "front_cast",
                MonsterActiveAttackPattern.PiercingProjectile when commonSlotId == "multi_launch" => "launch",
                MonsterActiveAttackPattern.PiercingProjectile when commonSlotId == "hit" => "pierce_hit",
                MonsterActiveAttackPattern.ExplosiveProjectile when commonSlotId == "contact" => "target_hit",
                MonsterActiveAttackPattern.PiercingBeam when commonSlotId == "charge" => "beam_charge",
                MonsterActiveAttackPattern.PiercingBeam when commonSlotId == "contact_hit" => "target_hit",
                MonsterActiveAttackPattern.PiercingBeam when commonSlotId == "end" => "beam_end",
                MonsterActiveAttackPattern.InstantMagic when commonSlotId == "cast" => "magic_cast",
                MonsterActiveAttackPattern.InstantMagic when commonSlotId == "hit" => "target_hit",
                _ => string.Empty
            };
            if (!string.IsNullOrWhiteSpace(legacy) &&
                !ids.Contains(legacy, StringComparer.OrdinalIgnoreCase))
            {
                ids.Add(legacy);
            }
            return ids;
        }
    }
}
