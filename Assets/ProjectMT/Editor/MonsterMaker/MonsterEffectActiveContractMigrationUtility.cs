using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public readonly struct MonsterEffectActiveContractAudit
    {
        public MonsterEffectActiveContractAudit(
            int profiles,
            int groups,
            int slots,
            int durationGroups,
            int persistentGroups,
            int drafts,
            int runtimes,
            int currentBindings,
            int inactiveBindings,
            int runtimeBindings,
            int assignedVfx,
            int assignedSfx)
        {
            ProfileCount = profiles;
            GroupCount = groups;
            SlotCount = slots;
            DurationGroupCount = durationGroups;
            PersistentGroupCount = persistentGroups;
            DraftCount = drafts;
            RuntimeCount = runtimes;
            CurrentBindingCount = currentBindings;
            InactiveBindingCount = inactiveBindings;
            RuntimeBindingCount = runtimeBindings;
            AssignedVfxCount = assignedVfx;
            AssignedSfxCount = assignedSfx;
        }

        public int ProfileCount { get; }
        public int GroupCount { get; }
        public int SlotCount { get; }
        public int DurationGroupCount { get; }
        public int PersistentGroupCount { get; }
        public int DraftCount { get; }
        public int RuntimeCount { get; }
        public int CurrentBindingCount { get; }
        public int InactiveBindingCount { get; }
        public int RuntimeBindingCount { get; }
        public int AssignedVfxCount { get; }
        public int AssignedSfxCount { get; }
        public override string ToString() =>
            $"Profile {ProfileCount} · Group {GroupCount} · Slot {SlotCount} · " +
            $"지속효과 Group {DurationGroupCount} · 지속형 선택 {PersistentGroupCount} · " +
            $"Draft {DraftCount} · Runtime {RuntimeCount} · " +
            $"현재 Binding {CurrentBindingCount} · 보관 Binding {InactiveBindingCount} · " +
            $"Runtime Binding {RuntimeBindingCount} · " +
            $"배정 VFX {AssignedVfxCount} · SFX {AssignedSfxCount}";
    }

    public static class MonsterEffectActiveContractMigrationUtility
    {
        private const string ProfileRoot =
            "Assets/ProjectMT/02_Shared/Unit/Data/ActiveEffectProfiles";

        public static MonsterEffectActiveContractAudit AuditProduction()
        {
            var profiles = LoadProfiles();
            var groups = profiles.SelectMany(profile => profile.Groups)
                .Where(group => group != null)
                .ToArray();
            var drafts = LoadEffectDrafts();
            var runtimeAssets = drafts
                .Select(draft => AssetDatabase.LoadAssetAtPath<MonsterEffectActiveSkill>(
                    MonsterMakerAssetWriter.BuildActivePath(draft.MonsterId)))
                .Where(runtime => runtime != null)
                .ToArray();
            var currentSlots = drafts
                .SelectMany(draft => draft.ActiveEffectPresentations)
                .Where(presentation => presentation != null)
                .SelectMany(presentation => presentation.Slots)
                .Where(slot => slot != null)
                .ToArray();
            return new MonsterEffectActiveContractAudit(
                profiles.Length,
                groups.Length,
                groups.Sum(group => group.PresentationSlots.Count),
                groups.Count(group => group.HasDurationPresentation),
                groups.Count(group =>
                    MonsterEffectActiveVfxContractTemplates.ResolveTargetMode(group) ==
                    MonsterEffectTargetPresentationMode.DurationLifecycle),
                drafts.Length,
                runtimeAssets.Length,
                currentSlots.Length,
                drafts.Sum(CountInactiveEffectBindings),
                runtimeAssets.Sum(runtime => runtime.Presentations.Sum(
                    presentation => presentation?.Slots.Count ?? 0)),
                currentSlots.Count(slot =>
                    slot.VfxState == MonsterBasicAttackVfxAssignmentState.Assigned &&
                    slot.Feedback?.VfxPrefab != null),
                currentSlots.Count(slot =>
                    slot.SfxState == MonsterBasicAttackSfxAssignmentState.Assigned &&
                    (slot.Feedback?.Sound != null || slot.Feedback?.Sfx != null)));
        }

        [MenuItem("JC Tool/Monster/유틸리티/효과형 VFX 계약 단순화 동기화")]
        public static void MigrateProductionContracts()
        {
            var profiles = LoadProfiles();
            foreach (var profile in profiles)
            {
                var groups = new List<MonsterEffectActiveGroup>(profile.Groups.Count);
                foreach (var group in profile.Groups)
                {
                    if (group == null) continue;
                    var mode = MonsterEffectActiveVfxContractTemplates.ResolveTargetMode(group);
                    var slots = MonsterEffectActiveVfxContractTemplates.Build(group, mode);
                    group.EditorConfigure(
                        group.GroupId,
                        group.DisplayName,
                        group.DelayAfterPrevious,
                        group.Target,
                        group.IncludeCaster,
                        group.Radius,
                        group.MaxTargets,
                        group.Effects,
                        slots);
                    groups.Add(group);
                }
                profile.EditorConfigure(
                    profile.ProfileId,
                    profile.DisplayName,
                    profile.Description,
                    profile.Role,
                    groups);
                if (!profile.TryValidate(out var error))
                    throw new InvalidOperationException($"{profile.ProfileId}: {error}");
                EditorUtility.SetDirty(profile);
            }
            AssetDatabase.SaveAssets();

            var failures = new List<string>();
            var drafts = LoadEffectDrafts();
            foreach (var draft in drafts)
            {
                try
                {
                    draft.EditorSyncActiveEffectAuthoring();
                    EditorUtility.SetDirty(draft);
                    AssetDatabase.SaveAssetIfDirty(draft);
                    MonsterMakerAssetWriter.SynchronizeActiveEffectRuntime(draft);
                }
                catch (Exception exception)
                {
                    failures.Add($"{draft.MonsterId}: {exception.Message}");
                }
            }
            AssetDatabase.SaveAssets();
            if (failures.Count > 0)
                throw new InvalidOperationException(string.Join("\n", failures));
            Debug.Log("[Monster Effect Active] " + AuditProduction() + " · 계약/Writer 동기화 완료");
        }

        private static MonsterEffectActiveProfile[] LoadProfiles()
        {
            return AssetDatabase.FindAssets("t:MonsterEffectActiveProfile", new[] { ProfileRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterEffectActiveProfile>)
                .Where(profile => profile != null)
                .OrderBy(profile => profile.ProfileId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static MonsterMakerDraft[] LoadEffectDrafts()
        {
            return AssetDatabase.FindAssets(
                    "t:MonsterMakerDraft",
                    new[] { MonsterMakerAssetWriter.DraftRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterMakerDraft>)
                .Where(draft => draft?.ActiveEffectProfile != null)
                .OrderBy(draft => draft.MonsterId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static int CountInactiveEffectBindings(MonsterMakerDraft draft)
        {
            if (draft == null) return 0;
            var count = draft.ActiveEffectPresentations.Sum(presentation =>
                presentation?.InactiveSlotCount ?? 0);
            var serialized = new SerializedObject(draft);
            var archives = serialized.FindProperty("inactiveActiveAttackAuthoring");
            if (archives == null) return count;
            for (var archiveIndex = 0; archiveIndex < archives.arraySize; archiveIndex++)
            {
                var archive = archives.GetArrayElementAtIndex(archiveIndex);
                var profileId = archive.FindPropertyRelative("profileId")?.stringValue ?? string.Empty;
                if (!profileId.StartsWith("effect:", StringComparison.OrdinalIgnoreCase)) continue;
                var presentations = archive.FindPropertyRelative("presentations");
                if (presentations == null) continue;
                for (var presentationIndex = 0;
                     presentationIndex < presentations.arraySize;
                     presentationIndex++)
                {
                    var presentation = presentations.GetArrayElementAtIndex(presentationIndex);
                    count += presentation.FindPropertyRelative("slots")?.arraySize ?? 0;
                    count += presentation.FindPropertyRelative("inactiveSlots")?.arraySize ?? 0;
                }
            }
            return count;
        }
    }
}
