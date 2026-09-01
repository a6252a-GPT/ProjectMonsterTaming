using System;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public enum MonsterActiveAttackRuntimeSyncState
    {
        Synchronized,
        NoProfile,
        RuntimeMissing,
        ProfileMismatch,
        SkillMismatch,
        StepMismatch,
        MotionMismatch,
        PresentationMismatch
    }

    public static class MonsterActiveAttackBindingProjection // Maker 액티브와 정식 런타임 자산의 차이를 한곳에서 판정
    {
        public static MonsterActiveAttackRuntimeSyncState EvaluateRuntimeSync(
            MonsterMakerDraft draft,
            MonsterAttackActiveSkill runtime,
            MonsterMotionProfile motion,
            out string message)
        {
            var profile = draft?.ActiveAttackProfile;
            if (profile == null)
            {
                message = "공격 액티브 프리셋이 지정되지 않았습니다.";
                return MonsterActiveAttackRuntimeSyncState.NoProfile;
            }
            if (runtime == null || motion == null)
            {
                message = "정식 액티브 또는 모션 Runtime 자산이 아직 생성되지 않았습니다.";
                return MonsterActiveAttackRuntimeSyncState.RuntimeMissing;
            }
            if (runtime.SourceProfile == null || !string.Equals(
                    profile.ProfileId,
                    runtime.SourceProfile.ProfileId,
                    StringComparison.OrdinalIgnoreCase))
            {
                message = $"Maker는 [{profile.ProfileId}], 게임 자산은 [{runtime.SourceProfile?.ProfileId ?? "없음"}]입니다.";
                return MonsterActiveAttackRuntimeSyncState.ProfileMismatch;
            }
            if (!string.Equals(runtime.DisplayName, draft.ActiveSkillName, StringComparison.Ordinal) ||
                runtime.EnergyCost != draft.ActiveEnergyMaximum ||
                runtime.MythicExclusive != (draft.Rarity == MonsterRarity.Mythic))
            {
                message = "스킬 이름·최대 기력·등급 설정이 게임 자산과 다릅니다.";
                return MonsterActiveAttackRuntimeSyncState.SkillMismatch;
            }
            if (!MatchesSteps(draft, runtime, out message))
            {
                return MonsterActiveAttackRuntimeSyncState.StepMismatch;
            }
            if (!MatchesAttackBlocks(draft, runtime, out message))
            {
                return MonsterActiveAttackRuntimeSyncState.StepMismatch;
            }
            if (!MatchesMotions(draft, motion, out message))
            {
                return MonsterActiveAttackRuntimeSyncState.MotionMismatch;
            }
            if (!MatchesPresentations(draft, runtime, out message))
            {
                return MonsterActiveAttackRuntimeSyncState.PresentationMismatch;
            }
            message = "Maker와 게임 자산의 액티브 공격·모션·연출이 일치합니다.";
            return MonsterActiveAttackRuntimeSyncState.Synchronized;
        }

        private static bool MatchesSteps(
            MonsterMakerDraft draft,
            MonsterAttackActiveSkill runtime,
            out string message)
        {
            var profile = draft.ActiveAttackProfile;
            if (runtime.Steps.Count != profile.Steps.Count)
            {
                message = $"Step 수가 다릅니다. Maker={profile.Steps.Count}, Runtime={runtime.Steps.Count}";
                return false;
            }
            for (var index = 0; index < profile.Steps.Count; index++)
            {
                var source = profile.Steps[index];
                var expected = source.Clone();
                var actual = runtime.Steps[index];
                if (actual == null || !string.Equals(expected.StepId, actual.StepId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(JsonUtility.ToJson(expected), JsonUtility.ToJson(actual), StringComparison.Ordinal))
                {
                    message = $"Step 수치가 다릅니다: {source.StepId}";
                    return false;
                }
            }
            message = string.Empty;
            return true;
        }

        private static bool MatchesMotions(
            MonsterMakerDraft draft,
            MonsterMotionProfile motion,
            out string message)
        {
            if (motion.ActiveSteps.Count != draft.ActiveAttackPresentations.Count)
            {
                message = $"액티브 모션 수가 다릅니다. Maker={draft.ActiveAttackPresentations.Count}, Runtime={motion.ActiveSteps.Count}";
                return false;
            }
            for (var index = 0; index < draft.ActiveAttackPresentations.Count; index++)
            {
                var source = draft.ActiveAttackPresentations[index];
                var actual = motion.ResolveActiveStep(source.StepId);
                draft.ResolveActiveStepMotion(
                    source,
                    out var clip,
                    out var speed,
                    out var fade,
                    out var commit);
                if (actual == null || actual.Clip != clip ||
                    !Approximately(actual.PlaybackSpeed, speed) ||
                    !Approximately(actual.CrossFadeDuration, fade) ||
                    !Approximately(actual.CommitNormalizedTime, commit))
                {
                    message = $"액티브 모션 설정이 다릅니다: {source.StepId}";
                    return false;
                }
            }
            message = string.Empty;
            return true;
        }

        private static bool MatchesAttackBlocks(
            MonsterMakerDraft draft,
            MonsterAttackActiveSkill runtime,
            out string message)
        {
            var profile = draft.ActiveAttackProfile;
            if (runtime.AttackBlocks.Count != profile.Steps.Count)
            {
                message = $"공용 공격 블록 수가 다릅니다. Maker={profile.Steps.Count}, Runtime={runtime.AttackBlocks.Count}";
                return false;
            }
            for (var index = 0; index < profile.Steps.Count; index++)
            {
                var source = profile.Steps[index];
                var actual = runtime.ResolveAttackBlock(source.StepId);
                var expected = ScriptableObject.CreateInstance<MonsterBasicAttackProfile>();
                expected.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    source.EditorCompileAttackBlock(expected);
                    expected.EditorSetProjectileCarrierPrefab(
                        expected.UsesProjectileVisual
                            ? draft.ProjectilePrefab != null
                                ? draft.ProjectilePrefab
                                : AssetDatabase.LoadAssetAtPath<GameObject>(
                                    MonsterMakerAssetWriter.DefaultProjectilePrefabPath)
                            : null);
                    expected.EditorSetFeelFeedback(null, null, profile.ImpactFeel);
                    if (actual == null || !MatchesRuntimeAttackBlock(expected, actual))
                    {
                        message = $"공용 공격 블록이 다릅니다: {source.StepId}";
                        return false;
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(expected);
                }
            }
            message = string.Empty;
            return true;
        }

        private static bool MatchesRuntimeAttackBlock(
            MonsterBasicAttackProfile expected,
            MonsterBasicAttackProfile actual)
        {
            return string.Equals(
                ToRuntimeAttackBlockJson(expected),
                ToRuntimeAttackBlockJson(actual),
                StringComparison.Ordinal);
        }

        private static string ToRuntimeAttackBlockJson(MonsterBasicAttackProfile source)
        {
            if (source == null) return string.Empty;
            var clone = UnityEngine.Object.Instantiate(source);
            clone.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var serialized = new SerializedObject(clone);
                var inactiveSlots = serialized.FindProperty("inactiveVfxSlots");
                if (inactiveSlots != null)
                {
                    inactiveSlots.arraySize = 0; // 편집 보관 슬롯은 실행 계약 비교에서 제외
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                return JsonUtility.ToJson(clone);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clone);
            }
        }

        private static bool MatchesPresentations(
            MonsterMakerDraft draft,
            MonsterAttackActiveSkill runtime,
            out string message)
        {
            var profile = draft.ActiveAttackProfile;
            if (runtime.Presentations.Count != profile.Steps.Count)
            {
                message = "Step별 연출 연결 수가 다릅니다.";
                return false;
            }
            for (var stepIndex = 0; stepIndex < profile.Steps.Count; stepIndex++)
            {
                var step = profile.Steps[stepIndex];
                var source = draft.ActiveAttackPresentations.FirstOrDefault(candidate => candidate != null &&
                    string.Equals(candidate.StepId, step.StepId, StringComparison.OrdinalIgnoreCase));
                var actual = runtime.ResolvePresentation(step.StepId);
                if (step.AttackBlockVfxSlots.Count > 0)
                {
                    if (source == null || actual == null ||
                        actual.AttackBlockBindings.Count != step.AttackBlockVfxSlots.Count ||
                        source.AttackBlockBindings.Count != step.AttackBlockVfxSlots.Count)
                    {
                        message = $"Step 공용 공격 블록 연결 수가 다릅니다: {step.StepId}";
                        return false;
                    }
                    for (var slotIndex = 0; slotIndex < step.AttackBlockVfxSlots.Count; slotIndex++)
                    {
                        var contract = step.AttackBlockVfxSlots[slotIndex];
                        var expectedMotion = contract.AssignmentScope ==
                                             MonsterBasicAttackVfxAssignmentScope.MotionSpecific
                            ? step.StepId
                            : string.Empty;
                        var sourceBinding = source.AttackBlockBindings.FirstOrDefault(candidate =>
                            candidate != null &&
                            string.Equals(candidate.AttackId, "active_" + step.StepId,
                                StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(candidate.SlotId, contract.SlotId,
                                StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(candidate.MotionId, expectedMotion,
                                StringComparison.OrdinalIgnoreCase));
                        var actualBinding = actual.AttackBlockBindings.FirstOrDefault(candidate =>
                            candidate != null &&
                            string.Equals(candidate.AttackId, "active_" + step.StepId,
                                StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(candidate.SlotId, contract.SlotId,
                                StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(candidate.MotionId, expectedMotion,
                                StringComparison.OrdinalIgnoreCase));
                        if (!MatchesAttackBlockBinding(sourceBinding, actualBinding))
                        {
                            message = $"공용 공격 블록 연출이 다릅니다: {step.StepId} / {contract.DisplayName}";
                            return false;
                        }
                    }
                    continue;
                }
                if (source == null || actual == null || actual.Slots.Count != step.PresentationSlots.Count)
                {
                    message = $"Step 연출 연결이 없습니다: {step.StepId}";
                    return false;
                }
                for (var slotIndex = 0; slotIndex < step.PresentationSlots.Count; slotIndex++)
                {
                    var contract = step.PresentationSlots[slotIndex];
                    var sourceSlot = source.ResolveSlot(contract.SlotId);
                    var actualSlot = actual.Slots.FirstOrDefault(candidate => candidate != null &&
                        string.Equals(candidate.SlotId, contract.SlotId, StringComparison.OrdinalIgnoreCase));
                    if (!MatchesSlot(contract, sourceSlot, actualSlot))
                    {
                        message = $"연출 공간이 다릅니다: {step.StepId} / {contract.DisplayName}";
                        return false;
                    }
                }
            }
            message = string.Empty;
            return true;
        }

        private static bool MatchesAttackBlockBinding(
            MonsterBasicAttackVfxBinding source,
            MonsterBasicAttackVfxBinding actual)
        {
            if (source == null || actual == null ||
                source.State != actual.State || source.Prefab != actual.Prefab ||
                source.SfxState != actual.SfxState || source.Sound != actual.Sound ||
                !Approximately(source.SoundVolume, actual.SoundVolume) ||
                !Approximately(source.Lifetime, actual.Lifetime) ||
                !Approximately(source.PlaybackOffset, actual.PlaybackOffset) ||
                !Approximately(source.PlaybackSpeed, actual.PlaybackSpeed) ||
                !Approximately(source.EventTimingOffset, actual.EventTimingOffset) ||
                (source.LocalPosition - actual.LocalPosition).sqrMagnitude > 0.000001f ||
                Quaternion.Angle(source.LocalRotation, actual.LocalRotation) > 0.001f ||
                !Approximately(source.Scale, actual.Scale))
            {
                return false;
            }
            if (source.SfxState != MonsterBasicAttackSfxAssignmentState.Assigned)
            {
                return actual.Sfx == null;
            }
            if (source.Sound != null)
            {
                return actual.Sfx != null && actual.Sfx.HasPlayableClip;
            }
            return source.Sfx == actual.Sfx;
        }

        private static bool MatchesSlot(
            MonsterActivePresentationSlot contract,
            MonsterMakerActivePresentationSlotDraft source,
            MonsterActiveAttackPresentationCueBinding actual)
        {
            if (contract == null || source == null || actual == null ||
                actual.Timing != contract.Timing || actual.Anchor != contract.Anchor ||
                actual.Multiplicity != contract.Multiplicity || actual.Attachment != contract.Attachment ||
                actual.EndPolicy != contract.EndPolicy || actual.UseDuration != contract.UseDuration ||
                actual.UseDuration && !Approximately(actual.Duration, contract.Duration))
            {
                return false;
            }
            var cue = actual.Feedback;
            var expectedVfx = source.VfxState == MonsterBasicAttackVfxAssignmentState.Assigned
                ? source.Feedback.VfxPrefab
                : null;
            var actualVfx = cue?.VfxPrefab;
            if (expectedVfx != actualVfx) return false;
            if (expectedVfx != null && (!Approximately(cue.VfxLifetime, source.Feedback.VfxLifetime) ||
                                        (cue.LocalPosition - source.Feedback.LocalPosition).sqrMagnitude > 0.000001f ||
                                        Quaternion.Angle(
                                            cue.LocalRotation,
                                            Quaternion.Euler(source.Feedback.LocalEulerAngles)) > 0.001f ||
                                        !Approximately(cue.Scale, source.Feedback.Scale)))
            {
                return false;
            }
            if (source.SfxState != MonsterBasicAttackSfxAssignmentState.Assigned)
            {
                return cue?.Sfx == null;
            }
            if (cue?.Sfx == null)
            {
                return false;
            }
            return source.Feedback.Sound != null
                ? cue.Sfx.PrimaryClip == source.Feedback.Sound
                : cue.Sfx == source.Feedback.Sfx;
        }

        private static bool Approximately(float left, float right)
        {
            return Mathf.Abs(left - right) < 0.0001f;
        }
    }
}
