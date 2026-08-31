using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public readonly struct MonsterActiveAttackMigrationReport
    {
        public MonsterActiveAttackMigrationReport(
            int profiles,
            int steps,
            int contractsBefore,
            int contractsAfter,
            int renamedDraftSlots,
            int synchronizedDrafts)
        {
            Profiles = profiles;
            Steps = steps;
            ContractsBefore = contractsBefore;
            ContractsAfter = contractsAfter;
            RenamedDraftSlots = renamedDraftSlots;
            SynchronizedDrafts = synchronizedDrafts;
        }

        public int Profiles { get; }
        public int Steps { get; }
        public int ContractsBefore { get; }
        public int ContractsAfter { get; }
        public int RenamedDraftSlots { get; }
        public int SynchronizedDrafts { get; }
        public override string ToString() =>
            $"Profile={Profiles}, Step={Steps}, Contract={ContractsBefore}->{ContractsAfter}, " +
            $"DraftSlotRename={RenamedDraftSlots}, DraftSync={SynchronizedDrafts}";
    }

    public static class MonsterActiveAttackMigrationUtility // 공식 프리셋을 의미 기반 계약으로 안전하게 승격
    {
        private const string MenuRoot = "Tools/ProjectMT/Monster Maker/액티브 계약 마이그레이션";

        [MenuItem(MenuRoot + "/1. 변경 전 점검")]
        private static void AuditMenu()
        {
            Debug.Log("[ActiveContractAudit] " + BuildAuditSummary());
        }

        [MenuItem(MenuRoot + "/2. 공식 프리셋 및 Draft 마이그레이션")]
        private static void MigrateMenu()
        {
            var report = MigrateProductionProfilesAndDrafts();
            Debug.Log("[ActiveContractMigration] " + report);
        }

        public static string BuildAuditSummary()
        {
            var profiles = LoadProfiles();
            var steps = 0;
            var slots = 0;
            var invalid = 0;
            var nonCanonical = 0;
            foreach (var profile in profiles)
            {
                foreach (var step in profile.Steps)
                {
                    steps++;
                    slots += step.PresentationSlots.Count;
                    var templates = MonsterActiveAttackVfxContractTemplates.Build(step);
                    if (!HasCanonicalContracts(step.PresentationSlots, templates)) nonCanonical++;
                    foreach (var slot in step.PresentationSlots)
                    {
                        if (!MonsterActiveAttackVfxCompatibility.TryValidateSlot(step, slot, out _)) invalid++;
                    }
                }
            }
            return $"Profile={profiles.Length}, Step={steps}, Slot={slots}, Invalid={invalid}, NonCanonicalStep={nonCanonical}";
        }

        public static MonsterActiveAttackMigrationReport MigrateProductionProfilesAndDrafts()
        {
            var profiles = LoadProfiles();
            var drafts = LoadDrafts();
            var stepCount = 0;
            var beforeCount = 0;
            var afterCount = 0;
            var renamed = 0;
            var synchronized = 0;

            foreach (var profile in profiles)
            {
                var mappingsByStep = new Dictionary<string, Dictionary<string, string>>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var step in profile.Steps)
                {
                    stepCount++;
                    beforeCount += step.PresentationSlots.Count;
                    var templates = MonsterActiveAttackVfxContractTemplates.Build(step);
                    afterCount += templates.Length;
                    mappingsByStep[step.StepId] = BuildSlotIdMapping(step, templates);
                }

                foreach (var draft in drafts.Where(candidate => candidate != null &&
                             candidate.ActiveAttackProfile == profile))
                {
                    renamed += RenameDraftSlots(draft, mappingsByStep);
                }

                Undo.RecordObject(profile, "액티브 VFX/SFX 계약 마이그레이션");
                foreach (var step in profile.Steps)
                {
                    step.EditorSetPresentationSlots(MonsterActiveAttackVfxContractTemplates.Build(step));
                }
                EditorUtility.SetDirty(profile);
            }

            foreach (var draft in drafts)
            {
                if (draft?.ActiveAttackProfile == null) continue;
                Undo.RecordObject(draft, "액티브 Maker 연결 마이그레이션");
                draft.EditorSyncActiveAttackAuthoring();
                EditorUtility.SetDirty(draft);
                synchronized++;
            }
            AssetDatabase.SaveAssets();
            return new MonsterActiveAttackMigrationReport(
                profiles.Length,
                stepCount,
                beforeCount,
                afterCount,
                renamed,
                synchronized);
        }

        private static MonsterActiveAttackProfile[] LoadProfiles()
        {
            return AssetDatabase.FindAssets(
                    "t:MonsterActiveAttackProfile",
                    new[] { MonsterActiveAttackAuthoringService.ProfileRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterActiveAttackProfile>)
                .Where(profile => profile != null)
                .OrderBy(profile => profile.ProfileId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static MonsterMakerDraft[] LoadDrafts()
        {
            return AssetDatabase.FindAssets(
                    "t:MonsterMakerDraft",
                    new[] { MonsterMakerAssetWriter.DraftRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterMakerDraft>)
                .Where(draft => draft != null)
                .ToArray();
        }

        private static Dictionary<string, string> BuildSlotIdMapping(
            MonsterActiveAttackStep step,
            IReadOnlyList<MonsterActivePresentationSlot> templates)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var available = step.PresentationSlots.Where(slot => slot != null).ToList();
            foreach (var template in templates)
            {
                var source = available.LastOrDefault(slot => string.Equals(
                    slot.SlotId,
                    template.SlotId,
                    StringComparison.OrdinalIgnoreCase));
                source ??= available.LastOrDefault(slot =>
                    slot.Timing == template.Timing && slot.Anchor == template.Anchor);
                source ??= available.LastOrDefault(slot =>
                    slot.Timing == template.Timing &&
                    MonsterActiveAttackVfxCompatibility.TryValidateSlot(step, slot, out _));
                if (source == null) continue;
                available.Remove(source);
                result[source.SlotId] = template.SlotId;
            }
            return result;
        }

        private static int RenameDraftSlots(
            MonsterMakerDraft draft,
            IReadOnlyDictionary<string, Dictionary<string, string>> mappingsByStep)
        {
            var serialized = new SerializedObject(draft);
            var presentations = serialized.FindProperty("activeAttackPresentations");
            var renamed = 0;
            for (var index = 0; presentations != null && index < presentations.arraySize; index++)
            {
                var presentation = presentations.GetArrayElementAtIndex(index);
                var stepId = presentation.FindPropertyRelative("stepId").stringValue;
                if (!mappingsByStep.TryGetValue(stepId, out var mappings)) continue;
                renamed += RenameSlotArray(presentation.FindPropertyRelative("slots"), mappings);
                renamed += RenameSlotArray(presentation.FindPropertyRelative("inactiveSlots"), mappings);
            }
            if (renamed > 0) serialized.ApplyModifiedPropertiesWithoutUndo();
            return renamed;
        }

        private static int RenameSlotArray(
            SerializedProperty slots,
            IReadOnlyDictionary<string, string> mappings)
        {
            var renamed = 0;
            var occupied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; slots != null && index < slots.arraySize; index++)
            {
                occupied.Add(slots.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("slotId").stringValue);
            }
            for (var index = 0; slots != null && index < slots.arraySize; index++)
            {
                var id = slots.GetArrayElementAtIndex(index).FindPropertyRelative("slotId");
                if (!mappings.TryGetValue(id.stringValue, out var replacement) ||
                    string.Equals(id.stringValue, replacement, StringComparison.OrdinalIgnoreCase) ||
                    occupied.Contains(replacement))
                {
                    continue;
                }
                occupied.Remove(id.stringValue);
                id.stringValue = replacement;
                occupied.Add(replacement);
                renamed++;
            }
            return renamed;
        }

        private static bool HasCanonicalContracts(
            IReadOnlyList<MonsterActivePresentationSlot> current,
            IReadOnlyList<MonsterActivePresentationSlot> expected)
        {
            if (current.Count != expected.Count) return false;
            for (var index = 0; index < expected.Count; index++)
            {
                var left = current[index];
                var right = expected[index];
                if (left == null || right == null ||
                    !string.Equals(left.SlotId, right.SlotId, StringComparison.OrdinalIgnoreCase) ||
                    left.Timing != right.Timing || left.Anchor != right.Anchor ||
                    left.Multiplicity != right.Multiplicity || left.Attachment != right.Attachment ||
                    left.EndPolicy != right.EndPolicy)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
