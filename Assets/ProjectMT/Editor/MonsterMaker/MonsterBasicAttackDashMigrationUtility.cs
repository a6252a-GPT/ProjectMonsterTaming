using System;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public readonly struct MonsterBasicAttackDashMigrationReport
    {
        public MonsterBasicAttackDashMigrationReport(
            int profiles,
            int drafts,
            int contractsBefore,
            int contractsAfter,
            int renamedBindings,
            int promotedBindings,
            int addedBindings)
        {
            Profiles = profiles;
            Drafts = drafts;
            ContractsBefore = contractsBefore;
            ContractsAfter = contractsAfter;
            RenamedBindings = renamedBindings;
            PromotedBindings = promotedBindings;
            AddedBindings = addedBindings;
        }

        public int Profiles { get; }
        public int Drafts { get; }
        public int ContractsBefore { get; }
        public int ContractsAfter { get; }
        public int RenamedBindings { get; }
        public int PromotedBindings { get; }
        public int AddedBindings { get; }

        public override string ToString() =>
            $"Profile={Profiles}, Draft={Drafts}, Contract={ContractsBefore}->{ContractsAfter}, " +
            $"BindingRename={RenamedBindings}, BindingPromote={PromotedBindings}, BindingAdd={AddedBindings}";
    }

    public static class MonsterBasicAttackDashMigrationUtility // 돌진 출발·도착 계약을 기존 배정 손실 없이 승격
    {
        private const string MenuRoot =
            "Tools/ProjectMT/Monster Maker/기본공격 돌진 VFX 계약 마이그레이션";

        [MenuItem(MenuRoot + "/1. 변경 전 점검")]
        private static void AuditMenu()
        {
            Debug.Log("[BasicDashContractAudit] " + BuildAuditSummary());
        }

        [MenuItem(MenuRoot + "/2. 프리셋 및 Draft 마이그레이션")]
        private static void MigrateMenu()
        {
            Debug.Log("[BasicDashContractMigration] " + MigrateProductionProfilesAndDrafts());
        }

        public static string BuildAuditSummary()
        {
            var profiles = LoadDashProfiles();
            var drafts = LoadDrafts();
            var missingExit = profiles.Count(profile => profile.VfxSlots.All(slot =>
                slot == null || !string.Equals(slot.SlotId, "dash_exit", StringComparison.OrdinalIgnoreCase)));
            var missingEnter = profiles.Count(profile => profile.VfxSlots.All(slot =>
                slot == null || !string.Equals(slot.SlotId, "dash_enter", StringComparison.OrdinalIgnoreCase)));
            var legacyBindings = drafts.Sum(draft => draft.BasicAttackVfxBindings.Count(binding =>
                binding != null && string.Equals(
                    binding.SlotId,
                    "dash_start",
                    StringComparison.OrdinalIgnoreCase)));
            return $"Profile={profiles.Length}, MissingExit={missingExit}, MissingEnter={missingEnter}, " +
                   $"LegacyBinding={legacyBindings}";
        }

        public static MonsterBasicAttackDashMigrationReport MigrateProductionProfilesAndDrafts()
        {
            var profiles = LoadDashProfiles();
            var drafts = LoadDrafts();
            var contractsBefore = 0;
            var contractsAfter = 0;
            var migratedDrafts = 0;
            var renamedBindings = 0;
            var promotedBindings = 0;
            var addedBindings = 0;

            foreach (var profile in profiles)
            {
                contractsBefore += profile.VfxSlots.Count;
                var current = profile.VfxSlots
                    .Where(slot => slot != null)
                    .Select(BasicAttackWorkshopVfxSlot.From)
                    .ToArray();
                var reconciled = MonsterBasicAttackVfxContractTemplates.Reconcile(
                    profile,
                    current,
                    out _);
                contractsAfter += reconciled.Count;
                Undo.RecordObject(profile, "기본공격 돌진 VFX 계약 마이그레이션");
                profile.EditorSetVfxSlots(reconciled.Select(slot => slot.Compile()));
                EditorUtility.SetDirty(profile);

                foreach (var draft in drafts.Where(candidate =>
                             candidate != null && candidate.BasicAttackProfile == profile))
                {
                    Undo.RecordObject(draft, "기본공격 돌진 VFX 배정 마이그레이션");
                    var serialized = new SerializedObject(draft);
                    var bindings = serialized.FindProperty("basicAttackVfxBindings");
                    renamedBindings += RenameLegacyBindings(bindings, profile.AttackId);
                    promotedBindings += PromotePopulatedDashExitBindings(bindings, profile.AttackId);
                    foreach (var requirement in MonsterBasicAttackBindingProjection.BuildRequirements(
                                 profile,
                                 draft.Attacks))
                    {
                        if (FindBinding(bindings, requirement.Key) != null) continue;
                        CreateUndecidedBinding(bindings, requirement);
                        addedBindings++;
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(draft);
                    migratedDrafts++;
                }
            }

            AssetDatabase.SaveAssets();
            return new MonsterBasicAttackDashMigrationReport(
                profiles.Length,
                migratedDrafts,
                contractsBefore,
                contractsAfter,
                renamedBindings,
                promotedBindings,
                addedBindings);
        }

        private static int PromotePopulatedDashExitBindings(
            SerializedProperty bindings,
            string attackId)
        {
            var promoted = 0;
            for (var index = 0; bindings != null && index < bindings.arraySize; index++)
            {
                var binding = bindings.GetArrayElementAtIndex(index);
                if (!string.Equals(
                        binding.FindPropertyRelative("attackId").stringValue,
                        attackId,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        binding.FindPropertyRelative("slotId").stringValue,
                        "dash_exit",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var state = binding.FindPropertyRelative("state");
                var prefab = binding.FindPropertyRelative("prefab");
                if (state.enumValueIndex !=
                    (int)MonsterBasicAttackVfxAssignmentState.Undecided ||
                    prefab.objectReferenceValue == null)
                {
                    continue;
                }

                // 3상태 도입 전 dash_start는 Prefab 참조만으로 사용 의도를 저장했다.
                // 명시적 사용 안 함은 그대로 두고, 참조가 남은 미결정 행만 활성 배정으로 승격한다.
                state.enumValueIndex = (int)MonsterBasicAttackVfxAssignmentState.Assigned;
                promoted++;
            }
            return promoted;
        }

        private static MonsterBasicAttackProfile[] LoadDashProfiles()
        {
            return AssetDatabase.FindAssets(
                    "t:MonsterBasicAttackProfile",
                    new[] { MonsterBasicAttackPresetUtility.ProfileRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MonsterBasicAttackProfile>)
                .Where(profile => profile != null &&
                                  profile.MovementModule == MonsterBasicAttackMovementModule.Dash)
                .OrderBy(profile => profile.AttackId, StringComparer.OrdinalIgnoreCase)
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

        private static int RenameLegacyBindings(SerializedProperty bindings, string attackId)
        {
            var renamed = 0;
            for (var index = 0; bindings != null && index < bindings.arraySize; index++)
            {
                var binding = bindings.GetArrayElementAtIndex(index);
                if (!string.Equals(
                        binding.FindPropertyRelative("attackId").stringValue,
                        attackId,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        binding.FindPropertyRelative("slotId").stringValue,
                        "dash_start",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var motionId = binding.FindPropertyRelative("motionId").stringValue;
                var canonicalKey = new MonsterBasicAttackBindingKey(
                    attackId,
                    "dash_exit",
                    motionId);
                if (FindBinding(bindings, canonicalKey) != null) continue;
                binding.FindPropertyRelative("slotId").stringValue = "dash_exit";
                renamed++;
            }
            return renamed;
        }

        private static SerializedProperty FindBinding(
            SerializedProperty bindings,
            MonsterBasicAttackBindingKey key)
        {
            for (var index = 0; bindings != null && index < bindings.arraySize; index++)
            {
                var binding = bindings.GetArrayElementAtIndex(index);
                if (string.Equals(
                        binding.FindPropertyRelative("attackId").stringValue,
                        key.AttackId,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        binding.FindPropertyRelative("slotId").stringValue,
                        key.SlotId,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        binding.FindPropertyRelative("motionId").stringValue,
                        key.MotionId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return binding;
                }
            }
            return null;
        }

        private static void CreateUndecidedBinding(
            SerializedProperty bindings,
            MonsterBasicAttackBindingRequirement requirement)
        {
            var index = bindings.arraySize;
            bindings.InsertArrayElementAtIndex(index);
            var binding = bindings.GetArrayElementAtIndex(index);
            binding.FindPropertyRelative("attackId").stringValue = requirement.Key.AttackId;
            binding.FindPropertyRelative("slotId").stringValue = requirement.Key.SlotId;
            binding.FindPropertyRelative("motionId").stringValue = requirement.Key.MotionId;
            binding.FindPropertyRelative("state").enumValueIndex =
                (int)MonsterBasicAttackVfxAssignmentState.Undecided;
            binding.FindPropertyRelative("prefab").objectReferenceValue = null;
            binding.FindPropertyRelative("sfxState").enumValueIndex =
                (int)MonsterBasicAttackSfxAssignmentState.Undecided;
            binding.FindPropertyRelative("sound").objectReferenceValue = null;
            binding.FindPropertyRelative("soundVolume").floatValue = 1f;
            binding.FindPropertyRelative("sfx").objectReferenceValue = null;
            binding.FindPropertyRelative("lifetime").floatValue = requirement.Slot.DefaultLifetime;
            binding.FindPropertyRelative("playbackOffset").floatValue = 0f;
            binding.FindPropertyRelative("playbackSpeed").floatValue = 1f;
            binding.FindPropertyRelative("eventTimingOffset").floatValue = 0f;
            binding.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
            binding.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
            binding.FindPropertyRelative("scale").floatValue = 1f;
        }
    }
}
