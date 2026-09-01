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
            int synchronizedDrafts,
            int synchronizedRuntimeAssets)
        {
            Profiles = profiles;
            Steps = steps;
            ContractsBefore = contractsBefore;
            ContractsAfter = contractsAfter;
            RenamedDraftSlots = renamedDraftSlots;
            SynchronizedDrafts = synchronizedDrafts;
            SynchronizedRuntimeAssets = synchronizedRuntimeAssets;
        }

        public int Profiles { get; }
        public int Steps { get; }
        public int ContractsBefore { get; }
        public int ContractsAfter { get; }
        public int RenamedDraftSlots { get; }
        public int SynchronizedDrafts { get; }
        public int SynchronizedRuntimeAssets { get; }
        public override string ToString() =>
            $"Profile={Profiles}, Step={Steps}, Contract={ContractsBefore}->{ContractsAfter}, " +
            $"DraftSlotRename={RenamedDraftSlots}, DraftSync={SynchronizedDrafts}, " +
            $"RuntimeSync={SynchronizedRuntimeAssets}";
    }

    public static class MonsterActiveAttackMigrationUtility // 공식 프리셋을 의미 기반 계약으로 안전하게 승격
    {
        private const string MenuRoot = "Tools/ProjectMT/Monster Maker/액티브 계약 마이그레이션";
        private static readonly string[] QaProfilePaths =
        {
            "Assets/ProjectMT/99_Tests/QA/ActiveSkills/AAP_QA_SkyBreak.asset",
            "Assets/ProjectMT/99_Tests/QA/ActiveSkills/AAP_QA_NebulaFall.asset"
        };

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
                for (var stepIndex = 0; stepIndex < profile.Steps.Count; stepIndex++)
                {
                    var step = profile.Steps[stepIndex];
                    steps++;
                    slots += step.AttackBlockVfxSlots.Count;
                    var templates = MonsterActiveAttackBlockContractTemplates.Build(step);
                    if (step.PresentationSlots.Count > 0 ||
                        !step.HasCanonicalIdentity(stepIndex) ||
                        !HasCanonicalContracts(step.AttackBlockVfxSlots, templates)) nonCanonical++;
                    foreach (var slot in step.AttackBlockVfxSlots)
                    {
                        if (!TryValidateAttackBlockSlot(step, slot)) invalid++;
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
            var runtimeSynchronized = 0;

            foreach (var profile in profiles)
            {
                BuildMigrationMappings(
                    profile,
                    out var stepIdMappings,
                    out var mappingsByStep);
                for (var stepIndex = 0; stepIndex < profile.Steps.Count; stepIndex++)
                {
                    var step = profile.Steps[stepIndex];
                    stepCount++;
                    beforeCount += step.AttackBlockVfxSlots.Count;
                    var templates = MonsterActiveAttackBlockContractTemplates.Build(step);
                    afterCount += templates.Length;
                }

                foreach (var draft in drafts)
                {
                    renamed += RenameDraftAuthoring(
                        draft,
                        profile,
                        stepIdMappings,
                        mappingsByStep);
                }

                Undo.RecordObject(profile, "액티브 VFX/SFX 계약 마이그레이션");
                for (var stepIndex = 0; stepIndex < profile.Steps.Count; stepIndex++)
                {
                    var step = profile.Steps[stepIndex];
                    var reconciled = MonsterActiveAttackBlockContractTemplates.Reconcile(step, out _);
                    step.EditorSetAttackBlockVfxSlots(reconciled);
                    // 구형 계약은 위 Draft 매핑을 만든 뒤 현재 Profile 투영에서 제거한다.
                    // 몬스터별 배정은 Draft의 활성/보관 Binding에 남으므로 자산 선택값은 잃지 않는다.
                    step.EditorSetPresentationSlots(Array.Empty<MonsterActivePresentationSlot>());
                    step.EditorNormalizeIdentity(stepIndex);
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

            // Profile과 Draft만 저장하고 끝내면 기존 Runtime 서브 자산은 구형 계약 수를 유지한다.
            // 연결된 제작 원본마다 정식 Writer를 통과시켜 독립 Step 공격 블록까지 함께 갱신한다.
            foreach (var draft in drafts)
            {
                if (draft?.ActiveAttackProfile == null) continue;
                MonsterMakerAssetWriter.SynchronizeActiveAttackRuntime(draft);
                runtimeSynchronized++;
            }
            return new MonsterActiveAttackMigrationReport(
                profiles.Length,
                stepCount,
                beforeCount,
                afterCount,
                renamed,
                synchronized,
                runtimeSynchronized);
        }

        private static void BuildMigrationMappings(
            MonsterActiveAttackProfile profile,
            out Dictionary<string, string> stepIdMappings,
            out Dictionary<string, Dictionary<string, string>> mappingsByStep)
        {
            stepIdMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            mappingsByStep = new Dictionary<string, Dictionary<string, string>>(
                StringComparer.OrdinalIgnoreCase);
            if (profile == null) return;
            for (var stepIndex = 0; stepIndex < profile.Steps.Count; stepIndex++)
            {
                var step = profile.Steps[stepIndex];
                if (step == null || string.IsNullOrWhiteSpace(step.StepId)) continue;
                var oldStepId = step.StepId;
                stepIdMappings[oldStepId] = MonsterActiveAttackStep.GetCanonicalStepId(stepIndex);
                mappingsByStep[oldStepId] = BuildSlotIdMapping(
                    step,
                    MonsterActiveAttackVfxContractTemplates.Build(step));
            }
        }

        private static MonsterActiveAttackProfile[] LoadProfiles()
        {
            var production = AssetDatabase.FindAssets(
                    "t:MonsterActiveAttackProfile",
                    new[] { MonsterActiveAttackAuthoringService.ProfileRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToList();
            production.AddRange(QaProfilePaths);
            return production
                .Distinct(StringComparer.OrdinalIgnoreCase)
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
            result["teleport_exit"] = "dash_exit";
            result["teleport_enter"] = "dash_enter";
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

        private static int RenameDraftAuthoring(
            MonsterMakerDraft draft,
            MonsterActiveAttackProfile profile,
            IReadOnlyDictionary<string, string> stepIdMappings,
            IReadOnlyDictionary<string, Dictionary<string, string>> mappingsByStep)
        {
            if (draft == null) return 0;
            var serialized = new SerializedObject(draft);
            var renamed = 0;
            if (draft.ActiveAttackProfile == profile)
            {
                renamed += RenameTuningArray(
                    serialized.FindProperty("activeAttackStepTunings"),
                    stepIdMappings);
                renamed += RenamePresentationArray(
                    serialized.FindProperty("activeAttackPresentations"),
                    stepIdMappings,
                    mappingsByStep);
            }

            var archives = serialized.FindProperty("inactiveActiveAttackAuthoring");
            for (var index = 0; archives != null && index < archives.arraySize; index++)
            {
                var archive = archives.GetArrayElementAtIndex(index);
                if (!string.Equals(
                        archive.FindPropertyRelative("profileId").stringValue,
                        profile.ProfileId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                renamed += RenameTuningArray(
                    archive.FindPropertyRelative("tunings"),
                    stepIdMappings);
                renamed += RenamePresentationArray(
                    archive.FindPropertyRelative("presentations"),
                    stepIdMappings,
                    mappingsByStep);
            }
            if (renamed > 0) serialized.ApplyModifiedPropertiesWithoutUndo();
            return renamed;
        }

        private static int RenameTuningArray(
            SerializedProperty tunings,
            IReadOnlyDictionary<string, string> stepIdMappings)
        {
            var renamed = 0;
            for (var index = 0; tunings != null && index < tunings.arraySize; index++)
            {
                var id = tunings.GetArrayElementAtIndex(index).FindPropertyRelative("stepId");
                if (!stepIdMappings.TryGetValue(id.stringValue, out var replacement) ||
                    string.Equals(id.stringValue, replacement, StringComparison.Ordinal))
                {
                    continue;
                }
                id.stringValue = replacement;
                renamed++;
            }
            return renamed;
        }

        private static int RenamePresentationArray(
            SerializedProperty presentations,
            IReadOnlyDictionary<string, string> stepIdMappings,
            IReadOnlyDictionary<string, Dictionary<string, string>> mappingsByStep)
        {
            var renamed = 0;
            for (var index = 0; presentations != null && index < presentations.arraySize; index++)
            {
                var presentation = presentations.GetArrayElementAtIndex(index);
                var id = presentation.FindPropertyRelative("stepId");
                var oldStepId = id.stringValue;
                if (mappingsByStep.TryGetValue(oldStepId, out var mappings))
                {
                    renamed += RenameSlotArray(presentation.FindPropertyRelative("slots"), mappings);
                    renamed += RenameSlotArray(presentation.FindPropertyRelative("inactiveSlots"), mappings);
                }
                if (!stepIdMappings.TryGetValue(oldStepId, out var replacement) ||
                    string.Equals(oldStepId, replacement, StringComparison.Ordinal))
                {
                    continue;
                }
                id.stringValue = replacement;
                renamed++;
            }
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

        private static bool HasCanonicalContracts(
            IReadOnlyList<MonsterBasicAttackVfxSlot> current,
            IReadOnlyList<MonsterBasicAttackVfxSlot> expected)
        {
            if (current.Count != expected.Count) return false;
            for (var index = 0; index < expected.Count; index++)
            {
                var left = current[index];
                var right = expected[index];
                if (left == null || right == null ||
                    !string.Equals(left.SlotId, right.SlotId, StringComparison.OrdinalIgnoreCase) ||
                    left.EventType != right.EventType || left.Anchor != right.Anchor ||
                    left.Multiplicity != right.Multiplicity ||
                    left.AssignmentScope != right.AssignmentScope ||
                    left.Attachment != right.Attachment || left.EndPolicy != right.EndPolicy)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool TryValidateAttackBlockSlot(
            MonsterActiveAttackStep step,
            MonsterBasicAttackVfxSlot slot)
        {
            if (step == null || slot == null) return false;
            var compiled = ScriptableObject.CreateInstance<MonsterBasicAttackProfile>();
            compiled.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                step.EditorCompileAttackBlock(compiled);
                return MonsterBasicAttackVfxCompatibility.TryValidateSlot(compiled, slot, out _);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(compiled);
            }
        }
    }
}
