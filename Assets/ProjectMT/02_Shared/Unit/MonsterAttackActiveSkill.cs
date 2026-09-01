using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectMT.Shared.Unit
{
    [Serializable]
    public sealed class MonsterActiveAttackPresentationCueBinding // 공간 계약 하나의 런타임 연출
    {
        [SerializeField] private string slotId;
        [SerializeField] private MonsterActivePresentationEvent timing;
        [SerializeField] private MonsterActivePresentationAnchor anchor;
        [SerializeField] private MonsterActivePresentationMultiplicity multiplicity;
        [SerializeField] private MonsterActivePresentationAttachment attachment;
        [SerializeField] private MonsterActivePresentationEndPolicy endPolicy;
        [SerializeField] private MonsterFeedbackCue feedback;
        [SerializeField] private bool useDuration;
        [SerializeField, Min(0.05f)] private float duration = 1f;

        public string SlotId => slotId?.Trim() ?? string.Empty;
        public MonsterActivePresentationEvent Timing => timing;
        public MonsterActivePresentationAnchor Anchor => anchor;
        public MonsterActivePresentationMultiplicity Multiplicity => multiplicity;
        public MonsterActivePresentationAttachment Attachment => attachment;
        public MonsterActivePresentationEndPolicy EndPolicy => endPolicy;
        public MonsterFeedbackCue Feedback => feedback;
        public bool UseDuration => useDuration;
        public float Duration => Mathf.Max(0.05f, duration);

        public bool TryValidate(out string error)
        {
            if (!ActiveAttackValue.UsesSafeId(SlotId) ||
                !Enum.IsDefined(typeof(MonsterActivePresentationEvent), timing) ||
                !Enum.IsDefined(typeof(MonsterActivePresentationAnchor), anchor) ||
                !Enum.IsDefined(typeof(MonsterActivePresentationMultiplicity), multiplicity) ||
                !Enum.IsDefined(typeof(MonsterActivePresentationAttachment), attachment) ||
                !Enum.IsDefined(typeof(MonsterActivePresentationEndPolicy), endPolicy) ||
                (useDuration && !ActiveAttackValue.IsFinitePositive(duration)))
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
            MonsterFeedbackCue cue,
            bool overrideDuration = false,
            float playbackDuration = 1f,
            MonsterActivePresentationMultiplicity playbackMultiplicity =
                MonsterActivePresentationMultiplicity.OncePerStep,
            MonsterActivePresentationAttachment playbackAttachment =
                MonsterActivePresentationAttachment.World,
            MonsterActivePresentationEndPolicy playbackEndPolicy =
                MonsterActivePresentationEndPolicy.Timed)
        {
            slotId = id?.Trim();
            timing = eventTiming;
            anchor = positionAnchor;
            multiplicity = playbackMultiplicity;
            attachment = playbackAttachment;
            endPolicy = playbackEndPolicy;
            feedback = cue;
            useDuration = overrideDuration;
            duration = Mathf.Max(0.05f, playbackDuration);
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
        [FormerlySerializedAs("teleportExit")]
        [SerializeField] private MonsterFeedbackCue dashExit;
        [FormerlySerializedAs("teleportEnter")]
        [SerializeField] private MonsterFeedbackCue dashEnter;
        [SerializeField] private MonsterActiveAttackPresentationCueBinding[] slots =
            Array.Empty<MonsterActiveAttackPresentationCueBinding>();
        [SerializeField] private MonsterBasicAttackVfxBinding[] attackBlockBindings =
            Array.Empty<MonsterBasicAttackVfxBinding>();

        public string StepId => stepId?.Trim() ?? string.Empty;
        public MonsterFeedbackCue Telegraph => telegraph;
        public MonsterFeedbackCue Launch => launch;
        public MonsterFeedbackCue Travel => travel;
        public MonsterFeedbackCue Impact => impact;
        public MonsterFeedbackCue DashExit => dashExit;
        public MonsterFeedbackCue DashEnter => dashEnter;
        public IReadOnlyList<MonsterActiveAttackPresentationCueBinding> Slots => slots ??
            Array.Empty<MonsterActiveAttackPresentationCueBinding>();
        public IReadOnlyList<MonsterBasicAttackVfxBinding> AttackBlockBindings => attackBlockBindings ??
            Array.Empty<MonsterBasicAttackVfxBinding>();

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(StepId))
            {
                error = "액티브 연출의 Step ID가 비어 있습니다.";
                return false;
            }
            var cues = new[] { telegraph, launch, travel, impact, dashExit, dashEnter };
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
            var blockKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < AttackBlockBindings.Count; index++)
            {
                var binding = AttackBlockBindings[index];
                var bindingError = "공용 공격 블록 연결이 비어 있습니다.";
                var key = binding == null
                    ? string.Empty
                    : $"{binding.AttackId}|{binding.SlotId}|{binding.MotionId}";
                if (binding == null || !binding.TryValidate(out bindingError) || !blockKeys.Add(key))
                {
                    error = $"액티브 Step 공용 공격 블록 연결이 유효하지 않습니다. Step={StepId}, Detail={bindingError}";
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
            MonsterFeedbackCue dashOut,
            MonsterFeedbackCue dashIn,
            MonsterActiveAttackPresentationCueBinding[] slotBindings = null,
            MonsterBasicAttackVfxBinding[] basicAttackBindings = null)
        {
            stepId = id?.Trim();
            telegraph = warning;
            launch = cast;
            travel = moving;
            impact = hit;
            dashExit = dashOut;
            dashEnter = dashIn;
            slots = slotBindings ?? Array.Empty<MonsterActiveAttackPresentationCueBinding>();
            attackBlockBindings = basicAttackBindings ?? Array.Empty<MonsterBasicAttackVfxBinding>();
        }
#endif
    }

    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Skill/Compiled Attack Active", fileName = "MSA_Monster")]
    public sealed class MonsterAttackActiveSkill : MonsterActiveSkill // Maker가 몬스터별로 컴파일한 공격 액티브
    {
        [SerializeField] private MonsterActiveAttackProfile sourceProfile;
        [SerializeField] private MonsterActiveAttackStep[] steps = Array.Empty<MonsterActiveAttackStep>();
        [SerializeField] private MonsterBasicAttackProfile[] attackBlocks =
            Array.Empty<MonsterBasicAttackProfile>();
        [SerializeField] private MonsterActiveAttackPresentationBinding[] presentations =
            Array.Empty<MonsterActiveAttackPresentationBinding>();
        [SerializeField, Range(0f, 1f)] private float commitNormalizedTime = 0.25f;
        [SerializeField] private bool mythicExclusive;

        public MonsterActiveAttackProfile SourceProfile => sourceProfile;
        public IReadOnlyList<MonsterActiveAttackStep> Steps => steps ?? Array.Empty<MonsterActiveAttackStep>();
        public IReadOnlyList<MonsterBasicAttackProfile> AttackBlocks => attackBlocks ??
            Array.Empty<MonsterBasicAttackProfile>();
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

        public MonsterBasicAttackProfile ResolveAttackBlock(string stepId)
        {
            if (string.IsNullOrWhiteSpace(stepId)) return null;
            var expectedId = "active_" + stepId.Trim();
            for (var index = 0; index < AttackBlocks.Count; index++)
            {
                var block = AttackBlocks[index];
                if (block != null && string.Equals(
                        block.AttackId,
                        expectedId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return block;
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
            if (Presentations.Count != Steps.Count)
            {
                error = $"컴파일된 공격 Step과 연출 연결 수가 다릅니다. Skill={SkillId}, Step={Steps.Count}, Presentation={Presentations.Count}";
                return false;
            }
            if (AttackBlocks.Count != Steps.Count)
            {
                error = $"컴파일된 공격 Step과 공용 공격 블록 수가 다릅니다. Skill={SkillId}, Step={Steps.Count}, Block={AttackBlocks.Count}";
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
                var block = ResolveAttackBlock(step.StepId);
                var blockError = "공용 공격 블록이 비어 있습니다.";
                if (block == null || !block.TryValidate(out blockError))
                {
                    error = $"컴파일된 공용 공격 블록이 유효하지 않습니다. Skill={SkillId}, Step={step.StepId}, Detail={blockError}";
                    return false;
                }
                if (step.PresentationSlots.Count > 0 || step.AttackBlockVfxSlots.Count == 0)
                {
                    error = $"공격 Step에 구형 액티브 전용 계약이 남아 있거나 공용 공격 블록 계약이 없습니다. Skill={SkillId}, Step={step.StepId}";
                    return false;
                }
                if (!MatchesAttackBlockContract(step, block, out blockError))
                {
                    error = $"컴파일된 공용 공격 블록 계약이 Step 원본과 다릅니다. Skill={SkillId}, Step={step.StepId}, Detail={blockError}";
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
                var step = ResolveStep(binding.StepId);
                if (!MatchesPresentationContract(step, binding, out bindingError))
                {
                    error = $"공격 Step 연출 계약이 원본과 다릅니다. Skill={SkillId}, Detail={bindingError}";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private MonsterActiveAttackStep ResolveStep(string stepId)
        {
            for (var index = 0; index < Steps.Count; index++)
            {
                var step = Steps[index];
                if (step != null && string.Equals(step.StepId, stepId, StringComparison.OrdinalIgnoreCase))
                {
                    return step;
                }
            }
            return null;
        }

        private static bool MatchesPresentationContract(
            MonsterActiveAttackStep step,
            MonsterActiveAttackPresentationBinding binding,
            out string error)
        {
            if (step == null || binding == null || step.AttackBlockVfxSlots.Count == 0 ||
                binding.Slots.Count > 0 ||
                step.AttackBlockVfxSlots.Count != binding.AttackBlockBindings.Count)
            {
                error = $"Step={binding?.StepId}, 공용 공격 블록 공간 수가 다르거나 구형 슬롯이 포함되어 있습니다.";
                return false;
            }
            for (var contractIndex = 0; contractIndex < step.AttackBlockVfxSlots.Count; contractIndex++)
            {
                var contract = step.AttackBlockVfxSlots[contractIndex];
                var expectedMotion = contract.AssignmentScope ==
                                     MonsterBasicAttackVfxAssignmentScope.MotionSpecific
                    ? step.StepId
                    : string.Empty;
                var found = false;
                for (var bindingIndex = 0; bindingIndex < binding.AttackBlockBindings.Count; bindingIndex++)
                {
                    var candidate = binding.AttackBlockBindings[bindingIndex];
                    if (candidate != null &&
                        string.Equals(candidate.AttackId, "active_" + step.StepId,
                            StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(candidate.SlotId, contract.SlotId,
                            StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(candidate.MotionId, expectedMotion,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    error = $"Step={step.StepId}, Slot={contract?.SlotId ?? "비어 있음"}";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private static bool MatchesAttackBlockContract(
            MonsterActiveAttackStep step,
            MonsterBasicAttackProfile block,
            out string error)
        {
            if (step == null || block == null ||
                step.AttackBlockVfxSlots.Count != block.VfxSlots.Count)
            {
                error = "VFX/SFX 공간 수가 다릅니다.";
                return false;
            }
            for (var index = 0; index < step.AttackBlockVfxSlots.Count; index++)
            {
                var source = step.AttackBlockVfxSlots[index];
                var compiled = block.VfxSlots[index];
                if (source == null || compiled == null ||
                    !string.Equals(source.SlotId, compiled.SlotId, StringComparison.OrdinalIgnoreCase) ||
                    source.EventType != compiled.EventType ||
                    source.Anchor != compiled.Anchor ||
                    source.Multiplicity != compiled.Multiplicity ||
                    source.AssignmentScope != compiled.AssignmentScope ||
                    source.Attachment != compiled.Attachment ||
                    source.EndPolicy != compiled.EndPolicy ||
                    !Mathf.Approximately(source.DefaultLifetime, compiled.DefaultLifetime))
                {
                    error = $"Slot={source?.SlotId ?? "비어 있음"}";
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
            var blocks = new List<MonsterBasicAttackProfile>();
            if (profile != null)
            {
                for (var index = 0; index < profile.Steps.Count; index++)
                {
                    var step = profile.Steps[index];
                    if (step == null) continue;
                    var block = ScriptableObject.CreateInstance<MonsterBasicAttackProfile>();
                    block.name = "__ActiveAttackBlock_" + step.StepId;
                    block.hideFlags = HideFlags.HideAndDontSave;
                    step.EditorCompileAttackBlock(block);
                    block.EditorSetFeelFeedback(null, null, profile.ImpactFeel);
                    blocks.Add(block);
                }
            }
            EditorConfigure(
                id,
                title,
                body,
                icon,
                profile,
                blocks.ToArray(),
                tunings,
                stepPresentations,
                maximumEnergy,
                commitTime,
                isMythic);
        }

        public void EditorConfigure(
            string id,
            string title,
            string body,
            Sprite icon,
            MonsterActiveAttackProfile profile,
            MonsterBasicAttackProfile[] compiledAttackBlocks,
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
                    compiledSteps.Add(sourceStep.Clone());
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
            attackBlocks = compiledAttackBlocks ?? Array.Empty<MonsterBasicAttackProfile>();
            presentations = stepPresentations ?? Array.Empty<MonsterActiveAttackPresentationBinding>();
            commitNormalizedTime = Mathf.Clamp01(commitTime);
            mythicExclusive = isMythic;
        }
#endif
    }
}
