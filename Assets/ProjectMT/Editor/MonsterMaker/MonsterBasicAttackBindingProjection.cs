using System;
using System.Collections.Generic;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public enum MonsterBasicAttackRuntimeSyncState
    {
        Synchronized,
        NoProfile,
        RuntimeMissing,
        ProfileMismatch,
        BindingMismatch
    }

    public readonly struct MonsterBasicAttackBindingKey : IEquatable<MonsterBasicAttackBindingKey>
    {
        public MonsterBasicAttackBindingKey(string attackId, string slotId, string motionId)
        {
            AttackId = attackId?.Trim() ?? string.Empty;
            SlotId = slotId?.Trim() ?? string.Empty;
            MotionId = motionId?.Trim() ?? string.Empty;
        }

        public string AttackId { get; }
        public string SlotId { get; }
        public string MotionId { get; }

        public bool Equals(MonsterBasicAttackBindingKey other)
        {
            return string.Equals(AttackId, other.AttackId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(SlotId, other.SlotId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(MotionId, other.MotionId, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return obj is MonsterBasicAttackBindingKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var comparer = StringComparer.OrdinalIgnoreCase;
                var hash = comparer.GetHashCode(AttackId);
                hash = hash * 397 ^ comparer.GetHashCode(SlotId);
                hash = hash * 397 ^ comparer.GetHashCode(MotionId);
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{AttackId}|{SlotId}|{MotionId}";
        }
    }

    public readonly struct MonsterBasicAttackBindingRequirement
    {
        public MonsterBasicAttackBindingRequirement(
            MonsterBasicAttackVfxSlot slot,
            string motionId)
        {
            Slot = slot;
            MotionId = motionId ?? string.Empty;
            Key = new MonsterBasicAttackBindingKey(
                string.Empty,
                slot?.SlotId,
                MotionId);
        }

        internal MonsterBasicAttackBindingRequirement(
            string attackId,
            MonsterBasicAttackVfxSlot slot,
            string motionId)
        {
            Slot = slot;
            MotionId = motionId ?? string.Empty;
            Key = new MonsterBasicAttackBindingKey(attackId, slot?.SlotId, MotionId);
        }

        public MonsterBasicAttackVfxSlot Slot { get; }
        public string MotionId { get; }
        public MonsterBasicAttackBindingKey Key { get; }
    }

    public static class MonsterBasicAttackBindingProjection // Draft 보관값에서 현재 공격에 필요한 연결만 투영
    {
        public static IReadOnlyList<MonsterBasicAttackBindingRequirement> BuildRequirements(
            MonsterBasicAttackProfile profile,
            IReadOnlyList<MonsterMakerAttackDraft> attacks)
        {
            var result = new List<MonsterBasicAttackBindingRequirement>();
            if (profile == null)
            {
                return result;
            }

            var motionIds = ResolveMotionIds(attacks);
            foreach (var slot in profile.VfxSlots)
            {
                if (slot == null)
                {
                    continue;
                }

                if (slot.AssignmentScope == MonsterBasicAttackVfxAssignmentScope.MonsterShared)
                {
                    result.Add(new MonsterBasicAttackBindingRequirement(profile.AttackId, slot, string.Empty));
                    continue;
                }

                foreach (var motionId in motionIds)
                {
                    result.Add(new MonsterBasicAttackBindingRequirement(profile.AttackId, slot, motionId));
                }
            }

            return result;
        }

        public static IReadOnlyList<MonsterBasicAttackVfxBinding> BuildActiveBindings(
            MonsterMakerDraft draft)
        {
            var result = new List<MonsterBasicAttackVfxBinding>();
            if (draft?.BasicAttackProfile == null)
            {
                return result;
            }

            foreach (var requirement in BuildRequirements(draft.BasicAttackProfile, draft.Attacks))
            {
                var binding = ResolveLast(draft.BasicAttackVfxBindings, requirement.Key);
                if (binding != null)
                {
                    result.Add(binding);
                }
            }

            return result;
        }

        public static IReadOnlyList<MonsterBasicAttackVfxBinding> BuildInactiveBindings(
            MonsterMakerDraft draft)
        {
            var result = new List<MonsterBasicAttackVfxBinding>();
            if (draft == null)
            {
                return result;
            }

            var activeKeys = new HashSet<MonsterBasicAttackBindingKey>(
                BuildRequirements(draft.BasicAttackProfile, draft.Attacks)
                    .Select(requirement => requirement.Key));
            foreach (var binding in draft.BasicAttackVfxBindings)
            {
                if (binding == null || activeKeys.Contains(ToKey(binding)))
                {
                    continue;
                }
                result.Add(binding);
            }

            return result;
        }

        public static bool IsActive(
            MonsterMakerDraft draft,
            MonsterBasicAttackVfxBinding binding)
        {
            if (draft == null || binding == null)
            {
                return false;
            }

            return BuildRequirements(draft.BasicAttackProfile, draft.Attacks)
                .Any(requirement => requirement.Key.Equals(ToKey(binding)));
        }

        public static MonsterBasicAttackRuntimeSyncState EvaluateRuntimeSync(
            MonsterMakerDraft draft,
            MonsterCombatProfile combat,
            MonsterFeedbackProfile feedback,
            out string message)
        {
            var draftProfile = draft?.BasicAttackProfile;
            if (draftProfile == null)
            {
                message = "기본공격 프리셋이 지정되지 않았습니다.";
                return MonsterBasicAttackRuntimeSyncState.NoProfile;
            }

            var runtimeProfile = combat?.Action?.BasicAttackProfile;
            if (runtimeProfile == null || feedback == null)
            {
                message = "정식 기본공격 Runtime 자산이 아직 생성되지 않았습니다.";
                return MonsterBasicAttackRuntimeSyncState.RuntimeMissing;
            }

            if (!string.Equals(
                    draftProfile.AttackId,
                    runtimeProfile.AttackId,
                    StringComparison.OrdinalIgnoreCase))
            {
                message =
                    $"Maker는 [{draftProfile.AttackId}], 게임 자산은 [{runtimeProfile.AttackId}]입니다.";
                return MonsterBasicAttackRuntimeSyncState.ProfileMismatch;
            }

            var expected = BuildActiveBindings(draft);
            var actual = feedback.BasicAttackVfxBindings;
            if (expected.Count != actual.Count)
            {
                message = $"현재 활성 연결 {expected.Count}개와 게임 자산 {actual.Count}개가 다릅니다.";
                return MonsterBasicAttackRuntimeSyncState.BindingMismatch;
            }

            var usedIndexes = new HashSet<int>();
            foreach (var expectedBinding in expected)
            {
                var matchIndex = -1;
                for (var index = actual.Count - 1; index >= 0; index--)
                {
                    if (usedIndexes.Contains(index) || actual[index] == null ||
                        !ToKey(expectedBinding).Equals(ToKey(actual[index])))
                    {
                        continue;
                    }
                    matchIndex = index;
                    break;
                }

                if (matchIndex < 0 || !HasSamePresentation(expectedBinding, actual[matchIndex]))
                {
                    message = $"게임 자산의 연출 연결이 Maker와 다릅니다: {ToKey(expectedBinding)}";
                    return MonsterBasicAttackRuntimeSyncState.BindingMismatch;
                }
                usedIndexes.Add(matchIndex);
            }

            message = "Maker와 게임 자산의 기본공격 연출이 일치합니다.";
            return MonsterBasicAttackRuntimeSyncState.Synchronized;
        }

        public static MonsterBasicAttackBindingKey ToKey(MonsterBasicAttackVfxBinding binding)
        {
            return binding == null
                ? default
                : new MonsterBasicAttackBindingKey(binding.AttackId, binding.SlotId, binding.MotionId);
        }

        private static MonsterBasicAttackVfxBinding ResolveLast(
            IReadOnlyList<MonsterBasicAttackVfxBinding> bindings,
            MonsterBasicAttackBindingKey key)
        {
            if (bindings == null)
            {
                return null;
            }

            for (var index = bindings.Count - 1; index >= 0; index--)
            {
                if (bindings[index] != null && key.Equals(ToKey(bindings[index])))
                {
                    return bindings[index];
                }
            }
            return null;
        }

        private static IReadOnlyList<string> ResolveMotionIds(
            IReadOnlyList<MonsterMakerAttackDraft> attacks)
        {
            var result = new List<string>();
            if (attacks != null)
            {
                foreach (var attack in attacks)
                {
                    var motionId = attack?.MotionId?.Trim();
                    if (!string.IsNullOrWhiteSpace(motionId) &&
                        !result.Contains(motionId, StringComparer.OrdinalIgnoreCase))
                    {
                        result.Add(motionId);
                    }
                }
            }

            if (result.Count == 0)
            {
                result.Add("attack01");
            }
            return result;
        }

        private static bool HasSamePresentation(
            MonsterBasicAttackVfxBinding expected,
            MonsterBasicAttackVfxBinding actual)
        {
            return expected.State == actual.State &&
                   expected.Prefab == actual.Prefab &&
                   expected.SfxState == actual.SfxState &&
                   expected.Sound == actual.Sound &&
                   Approximately(expected.SoundVolume, actual.SoundVolume) &&
                   Approximately(expected.Lifetime, actual.Lifetime) &&
                   Approximately(expected.PlaybackOffset, actual.PlaybackOffset) &&
                   Approximately(expected.PlaybackSpeed, actual.PlaybackSpeed) &&
                   Approximately(expected.EventTimingOffset, actual.EventTimingOffset) &&
                   (expected.LocalPosition - actual.LocalPosition).sqrMagnitude < 0.000001f &&
                   Quaternion.Angle(expected.LocalRotation, actual.LocalRotation) < 0.001f &&
                   Approximately(expected.Scale, actual.Scale);
        }

        private static bool Approximately(float left, float right)
        {
            return Mathf.Abs(left - right) < 0.0001f;
        }
    }
}
