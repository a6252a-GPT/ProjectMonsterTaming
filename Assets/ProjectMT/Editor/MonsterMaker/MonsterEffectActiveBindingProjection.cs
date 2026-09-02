using System;
using System.Linq;
using ProjectMT.Shared.Unit;
using UnityEditor;
using UnityEngine;

namespace ProjectMT.EditorTools.MonsterMaker
{
    public enum MonsterEffectActiveRuntimeSyncState
    {
        Synchronized,
        NoProfile,
        RuntimeMissing,
        ProfileMismatch,
        SkillMismatch,
        MotionMismatch,
        PresentationMismatch
    }

    public static class MonsterEffectActiveBindingProjection
        // 효과형 Maker 원본과 효과형 Runtime의 동기화만 독립적으로 판정한다.
    {
        public static MonsterEffectActiveRuntimeSyncState EvaluateRuntimeSync(
            MonsterMakerDraft draft,
            MonsterEffectActiveSkill runtime,
            MonsterMotionProfile motion,
            out string message)
        {
            var profile = draft?.ActiveEffectProfile;
            if (profile == null)
            {
                message = "효과형 액티브 프리셋이 지정되지 않았습니다.";
                return MonsterEffectActiveRuntimeSyncState.NoProfile;
            }
            if (runtime == null || motion == null)
            {
                message = "정식 효과형 액티브 또는 모션 Runtime 자산이 아직 생성되지 않았습니다.";
                return MonsterEffectActiveRuntimeSyncState.RuntimeMissing;
            }
            if (runtime.SourceProfile == null || !string.Equals(
                    profile.ProfileId,
                    runtime.SourceProfile.ProfileId,
                    StringComparison.OrdinalIgnoreCase))
            {
                message = $"Maker는 [{profile.ProfileId}], 게임 자산은 " +
                          $"[{runtime.SourceProfile?.ProfileId ?? "없음"}]입니다.";
                return MonsterEffectActiveRuntimeSyncState.ProfileMismatch;
            }
            if (!string.Equals(runtime.DisplayName, draft.ActiveSkillName, StringComparison.Ordinal) ||
                runtime.EnergyCost != draft.ActiveEnergyMaximum ||
                runtime.MythicExclusive != (draft.Rarity == MonsterRarity.Mythic))
            {
                message = "스킬 이름·최대 기력·등급 설정이 게임 자산과 다릅니다.";
                return MonsterEffectActiveRuntimeSyncState.SkillMismatch;
            }
            if (!MatchesMotions(draft, motion, out message))
            {
                return MonsterEffectActiveRuntimeSyncState.MotionMismatch;
            }
            if (!MatchesPresentations(draft, runtime, out message))
            {
                return MonsterEffectActiveRuntimeSyncState.PresentationMismatch;
            }
            message = "Maker와 게임 자산의 효과 묶음·모션·연출 연결이 일치합니다.";
            return MonsterEffectActiveRuntimeSyncState.Synchronized;
        }

        private static bool MatchesMotions(
            MonsterMakerDraft draft,
            MonsterMotionProfile motion,
            out string message)
        {
            if (motion.ActiveSteps.Count != draft.ActiveEffectPresentations.Count)
            {
                message = $"효과형 모션 수가 다릅니다. " +
                          $"Maker={draft.ActiveEffectPresentations.Count}, " +
                          $"Runtime={motion.ActiveSteps.Count}";
                return false;
            }
            for (var index = 0; index < draft.ActiveEffectPresentations.Count; index++)
            {
                var source = draft.ActiveEffectPresentations[index];
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
                    message = $"효과형 모션 설정이 다릅니다: {source.StepId}";
                    return false;
                }
            }
            message = string.Empty;
            return true;
        }

        private static bool MatchesPresentations(
            MonsterMakerDraft draft,
            MonsterEffectActiveSkill runtime,
            out string message)
        {
            var profile = draft.ActiveEffectProfile;
            if (runtime.Presentations.Count != profile.Groups.Count)
            {
                message = $"효과 묶음별 연출 연결 수가 다릅니다. " +
                          $"Maker={profile.Groups.Count}, Runtime={runtime.Presentations.Count}";
                return false;
            }
            for (var groupIndex = 0; groupIndex < profile.Groups.Count; groupIndex++)
            {
                var group = profile.Groups[groupIndex];
                var source = draft.ActiveEffectPresentations.FirstOrDefault(candidate =>
                    candidate != null && string.Equals(
                        candidate.StepId,
                        group.GroupId,
                        StringComparison.OrdinalIgnoreCase));
                var actual = runtime.ResolvePresentation(group.GroupId);
                if (source == null || actual == null ||
                    source.Slots.Count != group.PresentationSlots.Count ||
                    actual.Slots.Count != group.PresentationSlots.Count)
                {
                    message = $"효과 묶음 연출 연결이 없습니다: {group.GroupId}";
                    return false;
                }
                for (var slotIndex = 0; slotIndex < group.PresentationSlots.Count; slotIndex++)
                {
                    var contract = group.PresentationSlots[slotIndex];
                    var sourceSlot = source.ResolveSlot(contract.SlotId);
                    var actualSlot = actual.Slots.FirstOrDefault(candidate =>
                        candidate != null && string.Equals(
                            candidate.SlotId,
                            contract.SlotId,
                            StringComparison.OrdinalIgnoreCase));
                    if (!MatchesSlot(contract, sourceSlot, actualSlot))
                    {
                        message = $"효과 연출 공간이 다릅니다: " +
                                  $"{group.GroupId} / {contract.DisplayName}";
                        return false;
                    }
                }
            }
            message = string.Empty;
            return true;
        }

        private static bool MatchesSlot(
            MonsterActivePresentationSlot contract,
            MonsterMakerActivePresentationSlotDraft source,
            MonsterActiveAttackPresentationCueBinding actual)
        {
            if (contract == null || source == null || actual == null ||
                actual.Timing != contract.Timing || actual.Anchor != contract.Anchor ||
                actual.Multiplicity != contract.Multiplicity ||
                actual.Attachment != contract.Attachment ||
                actual.EndPolicy != contract.EndPolicy ||
                actual.UseDuration != contract.UseDuration ||
                actual.UseDuration && !Approximately(actual.Duration, contract.Duration))
            {
                return false;
            }

            var cue = actual.Feedback;
            var sourceFeedback = source.Feedback;
            if (sourceFeedback == null) return false;
            var expectedVfx = source.VfxState == MonsterBasicAttackVfxAssignmentState.Assigned
                ? sourceFeedback.VfxPrefab
                : null;
            if (cue?.VfxPrefab != expectedVfx) return false;
            if (expectedVfx != null &&
                (!Approximately(cue.VfxLifetime, sourceFeedback.VfxLifetime) ||
                 (cue.LocalPosition - sourceFeedback.LocalPosition).sqrMagnitude > 0.000001f ||
                 Quaternion.Angle(
                     cue.LocalRotation,
                     Quaternion.Euler(sourceFeedback.LocalEulerAngles)) > 0.001f ||
                 !Approximately(cue.Scale, sourceFeedback.Scale)))
            {
                return false;
            }
            if (source.SfxState != MonsterBasicAttackSfxAssignmentState.Assigned)
            {
                return cue?.Sfx == null;
            }
            if (cue?.Sfx == null) return false;
            if (sourceFeedback.Sound == null)
            {
                return cue.Sfx == sourceFeedback.Sfx;
            }
            if (cue.Sfx.PrimaryClip != sourceFeedback.Sound) return false;
            var volumeRange = new SerializedObject(cue.Sfx)
                .FindProperty("volumeRange")?.vector2Value ?? Vector2.zero;
            return Approximately(volumeRange.x, sourceFeedback.SoundVolume) &&
                   Approximately(volumeRange.y, sourceFeedback.SoundVolume);
        }

        private static bool Approximately(float left, float right) =>
            Mathf.Abs(left - right) < 0.0001f;
    }
}
