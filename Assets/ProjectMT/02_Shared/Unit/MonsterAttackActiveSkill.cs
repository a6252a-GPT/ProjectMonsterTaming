using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [Serializable]
    public sealed class MonsterActiveAttackPresentationCueBinding // 공간 계약 하나의 런타임 연출
    {
        [SerializeField] private string slotId;
        [SerializeField] private MonsterActivePresentationEvent timing;
        [SerializeField] private MonsterActivePresentationAnchor anchor;
        [SerializeField] private MonsterFeedbackCue feedback;

        public string SlotId => slotId?.Trim() ?? string.Empty;
        public MonsterActivePresentationEvent Timing => timing;
        public MonsterActivePresentationAnchor Anchor => anchor;
        public MonsterFeedbackCue Feedback => feedback;

        public bool TryValidate(out string error)
        {
            if (!ActiveAttackValue.UsesSafeId(SlotId) ||
                !Enum.IsDefined(typeof(MonsterActivePresentationEvent), timing) ||
                !Enum.IsDefined(typeof(MonsterActivePresentationAnchor), anchor))
            {
                error = $"액티브 런타임 연출 공간이 유효하지 않습니다. Slot={SlotId}";
                return false;
            }
            if (feedback != null && !feedback.TryValidate(out error)) return false;
            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            MonsterActivePresentationEvent eventTiming,
            MonsterActivePresentationAnchor positionAnchor,
            MonsterFeedbackCue cue)
        {
            slotId = id?.Trim();
            timing = eventTiming;
            anchor = positionAnchor;
            feedback = cue;
        }
#endif
    }

    [Serializable]
    public sealed class MonsterActiveAttackPresentationBinding // Step별 VFX/SFX 계약
    {
        [SerializeField] private string stepId;
        [SerializeField] private MonsterFeedbackCue telegraph;
        [SerializeField] private MonsterFeedbackCue launch;
        [SerializeField] private MonsterFeedbackCue travel;
        [SerializeField] private MonsterFeedbackCue impact;
        [SerializeField] private MonsterFeedbackCue teleportExit;
        [SerializeField] private MonsterFeedbackCue teleportEnter;
        [SerializeField] private MonsterActiveAttackPresentationCueBinding[] slots =
            Array.Empty<MonsterActiveAttackPresentationCueBinding>();

        public string StepId => stepId?.Trim() ?? string.Empty;
        public MonsterFeedbackCue Telegraph => telegraph;
        public MonsterFeedbackCue Launch => launch;
        public MonsterFeedbackCue Travel => travel;
        public MonsterFeedbackCue Impact => impact;
        public MonsterFeedbackCue TeleportExit => teleportExit;
        public MonsterFeedbackCue TeleportEnter => teleportEnter;
        public IReadOnlyList<MonsterActiveAttackPresentationCueBinding> Slots => slots ??
            Array.Empty<MonsterActiveAttackPresentationCueBinding>();

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(StepId))
            {
                error = "액티브 연출의 Step ID가 비어 있습니다.";
                return false;
            }
            var cues = new[] { telegraph, launch, travel, impact, teleportExit, teleportEnter };
            for (var index = 0; index < cues.Length; index++)
            {
                if (cues[index] != null && !cues[index].TryValidate(out var cueError))
                {
                    error = $"액티브 Step 연출이 유효하지 않습니다. Step={StepId}, Detail={cueError}";
                    return false;
                }
            }
            var slotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < Slots.Count; index++)
            {
                var slot = Slots[index];
                var slotError = "공간 연출이 비어 있습니다.";
                if (slot == null || !slot.TryValidate(out slotError) || !slotIds.Add(slot.SlotId))
                {
                    error = $"액티브 Step 공간 연출이 유효하지 않습니다. Step={StepId}, Detail={slotError}";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            MonsterFeedbackCue warning,
            MonsterFeedbackCue cast,
            MonsterFeedbackCue moving,
            MonsterFeedbackCue hit,
            MonsterFeedbackCue warpOut,
            MonsterFeedbackCue warpIn,
            MonsterActiveAttackPresentationCueBinding[] slotBindings = null)
        {
            stepId = id?.Trim();
            telegraph = warning;
            launch = cast;
            travel = moving;
            impact = hit;
            teleportExit = warpOut;
            teleportEnter = warpIn;
            slots = slotBindings ?? Array.Empty<MonsterActiveAttackPresentationCueBinding>();
        }
#endif
    }

    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Skill/Compiled Attack Active", fileName = "MSA_Monster")]
    public sealed class MonsterAttackActiveSkill : MonsterActiveSkill // Maker가 몬스터별로 컴파일한 공격 액티브
    {
        [SerializeField] private MonsterActiveAttackProfile sourceProfile;
        [SerializeField] private MonsterActiveAttackStep[] steps = Array.Empty<MonsterActiveAttackStep>();
        [SerializeField] private MonsterActiveAttackPresentationBinding[] presentations =
            Array.Empty<MonsterActiveAttackPresentationBinding>();
        [SerializeField, Range(0f, 1f)] private float commitNormalizedTime = 0.25f;
        [SerializeField] private bool mythicExclusive;

        public MonsterActiveAttackProfile SourceProfile => sourceProfile;
        public IReadOnlyList<MonsterActiveAttackStep> Steps => steps ?? Array.Empty<MonsterActiveAttackStep>();
        public IReadOnlyList<MonsterActiveAttackPresentationBinding> Presentations => presentations ??
            Array.Empty<MonsterActiveAttackPresentationBinding>();
        public BasicAttackFeelCue ImpactFeel => sourceProfile?.ImpactFeel;
        public float CommitNormalizedTime => Mathf.Clamp01(commitNormalizedTime);
        public bool MythicExclusive => mythicExclusive;
        public override MonsterActiveExecutionKind ExecutionKind => mythicExclusive
            ? MonsterActiveExecutionKind.DedicatedMythic
            : MonsterActiveExecutionKind.Generic;

        public MonsterActiveAttackPresentationBinding ResolvePresentation(string stepId)
        {
            if (string.IsNullOrWhiteSpace(stepId)) return null;
            for (var index = 0; index < Presentations.Count; index++)
            {
                var binding = Presentations[index];
                if (binding != null && string.Equals(binding.StepId, stepId, StringComparison.OrdinalIgnoreCase))
                {
                    return binding;
                }
            }
            return null;
        }

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error)) return false;
            var profileError = "원본 프로필이 비어 있습니다.";
            if (sourceProfile == null || !sourceProfile.TryValidate(out profileError))
            {
                error = $"공격 액티브 원본 프로필이 유효하지 않습니다. Skill={SkillId}, Detail={profileError}";
                return false;
            }
            if (Steps.Count == 0 || Steps.Count > MonsterActiveAttackProfile.MaximumStepCount)
            {
                error = $"컴파일된 공격 Step 수가 유효하지 않습니다. Skill={SkillId}";
                return false;
            }

            var stepIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < Steps.Count; index++)
            {
                var step = Steps[index];
                var stepError = "Step이 비어 있습니다.";
                if (step == null || !step.TryValidate(out stepError) || !stepIds.Add(step.StepId))
                {
                    error = $"컴파일된 공격 Step이 유효하지 않습니다. Skill={SkillId}, Detail={stepError}";
                    return false;
                }
            }

            var presentationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < Presentations.Count; index++)
            {
                var binding = Presentations[index];
                var bindingError = "연출 연결이 비어 있습니다.";
                if (binding == null || !binding.TryValidate(out bindingError) ||
                    !stepIds.Contains(binding.StepId) || !presentationIds.Add(binding.StepId))
                {
                    error = $"공격 Step 연출 연결이 유효하지 않습니다. Skill={SkillId}, Detail={bindingError}";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string title,
            string body,
            Sprite icon,
            MonsterActiveAttackProfile profile,
            IReadOnlyList<MonsterActiveAttackStepTuning> tunings,
            MonsterActiveAttackPresentationBinding[] stepPresentations,
            int maximumEnergy,
            float commitTime,
            bool isMythic)
        {
            var compiledSteps = new List<MonsterActiveAttackStep>();
            if (profile != null)
            {
                for (var stepIndex = 0; stepIndex < profile.Steps.Count; stepIndex++)
                {
                    var sourceStep = profile.Steps[stepIndex];
                    MonsterActiveAttackStepTuning tuning = null;
                    if (tunings != null)
                    {
                        for (var tuningIndex = 0; tuningIndex < tunings.Count; tuningIndex++)
                        {
                            if (tunings[tuningIndex] != null && string.Equals(
                                    tunings[tuningIndex].StepId,
                                    sourceStep.StepId,
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                tuning = tunings[tuningIndex];
                                break;
                            }
                        }
                    }
                    compiledSteps.Add(sourceStep.CloneWithTuning(tuning));
                }
            }

            var compatibilityEffect = new MonsterSkillEffect();
            compatibilityEffect.EditorConfigure(
                "assembled_attack",
                MonsterSkillEffectType.Damage,
                MonsterSkillValueSource.AttackPowerRatio,
                1f);
            var compatibilityRecipe = new MonsterSkillRecipe();
            compatibilityRecipe.EditorConfigure(
                MonsterSkillTriggerType.EnergyMax,
                1,
                0f,
                MonsterSkillTargetType.CurrentTarget,
                MonsterSkillDeliveryType.Instant,
                MonsterSkillShapeType.Single,
                Array.Empty<MonsterSkillCondition>(),
                new[] { compatibilityEffect });

            EditorConfigureCommon(
                id,
                title,
                body,
                isMythic ? MonsterSkillPresentationTier.Mythic : MonsterSkillPresentationTier.Legendary,
                compatibilityRecipe,
                icon);
            EditorSetEnergyCost(maximumEnergy);
            EditorSetEnergyGeneration(0f, 0f, 0f);
            sourceProfile = profile;
            steps = compiledSteps.ToArray();
            presentations = stepPresentations ?? Array.Empty<MonsterActiveAttackPresentationBinding>();
            commitNormalizedTime = Mathf.Clamp01(commitTime);
            mythicExclusive = isMythic;
        }
#endif
    }
}
