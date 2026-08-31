using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [Serializable]
    public sealed class MonsterEffectActivePresentationBinding // 효과 묶음별 VFX/SFX 연결
    {
        [SerializeField] private string groupId;
        [SerializeField] private MonsterActiveAttackPresentationCueBinding[] slots =
            Array.Empty<MonsterActiveAttackPresentationCueBinding>();

        public string GroupId => groupId?.Trim() ?? string.Empty;
        public IReadOnlyList<MonsterActiveAttackPresentationCueBinding> Slots => slots ??
            Array.Empty<MonsterActiveAttackPresentationCueBinding>();

        public bool TryValidate(out string error)
        {
            if (!ActiveAttackValue.UsesSafeId(GroupId))
            {
                error = "효과 묶음 연출 ID가 비어 있습니다.";
                return false;
            }
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < Slots.Count; index++)
            {
                var slot = Slots[index];
                var slotError = "효과 연출 공간이 비어 있습니다.";
                if (slot == null || !slot.TryValidate(out slotError) || !ids.Add(slot.SlotId))
                {
                    error = $"효과 묶음 연출이 유효하지 않습니다. Group={GroupId}, Detail={slotError}";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            MonsterActiveAttackPresentationCueBinding[] slotBindings)
        {
            groupId = id?.Trim();
            slots = slotBindings ?? Array.Empty<MonsterActiveAttackPresentationCueBinding>();
        }
#endif
    }

    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Skill/Compiled Effect Active", fileName = "MSE_Monster")]
    public sealed class MonsterEffectActiveSkill : MonsterActiveSkill // Maker가 몬스터별로 컴파일한 효과형 액티브
    {
        [SerializeField] private MonsterEffectActiveProfile sourceProfile;
        [SerializeField] private MonsterEffectActivePresentationBinding[] presentations =
            Array.Empty<MonsterEffectActivePresentationBinding>();
        [SerializeField, Range(0f, 1f)] private float commitNormalizedTime = 0.25f;
        [SerializeField] private bool mythicExclusive;

        public MonsterEffectActiveProfile SourceProfile => sourceProfile;
        public IReadOnlyList<MonsterEffectActiveGroup> Groups => sourceProfile?.Groups ??
            (IReadOnlyList<MonsterEffectActiveGroup>)Array.Empty<MonsterEffectActiveGroup>();
        public IReadOnlyList<MonsterEffectActivePresentationBinding> Presentations => presentations ??
            Array.Empty<MonsterEffectActivePresentationBinding>();
        public float CommitNormalizedTime => Mathf.Clamp01(commitNormalizedTime);
        public bool MythicExclusive => mythicExclusive;
        public override MonsterActiveExecutionKind ExecutionKind => mythicExclusive
            ? MonsterActiveExecutionKind.DedicatedMythic
            : MonsterActiveExecutionKind.Generic;

        public MonsterEffectActivePresentationBinding ResolvePresentation(string groupId)
        {
            for (var index = 0; index < Presentations.Count; index++)
            {
                var candidate = Presentations[index];
                if (candidate != null && string.Equals(candidate.GroupId, groupId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
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
                error = $"효과형 액티브 원본이 유효하지 않습니다. Skill={SkillId}, Detail={profileError}";
                return false;
            }
            if (Presentations.Count != Groups.Count)
            {
                error = $"효과 묶음과 연출 연결 수가 다릅니다. Skill={SkillId}";
                return false;
            }
            var presentationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < Presentations.Count; index++)
            {
                var binding = Presentations[index];
                var bindingError = "연출 연결이 비어 있습니다.";
                if (binding == null || !binding.TryValidate(out bindingError) ||
                    ResolveGroup(binding.GroupId) == null || !presentationIds.Add(binding.GroupId))
                {
                    error = $"효과형 액티브 연출 연결이 유효하지 않습니다. Detail={bindingError}";
                    return false;
                }
                if (!MatchesPresentationContract(ResolveGroup(binding.GroupId), binding, out bindingError))
                {
                    error = $"효과형 액티브 연출 계약이 원본과 다릅니다. Skill={SkillId}, Detail={bindingError}";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private MonsterEffectActiveGroup ResolveGroup(string groupId)
        {
            for (var index = 0; index < Groups.Count; index++)
            {
                var group = Groups[index];
                if (group != null && string.Equals(group.GroupId, groupId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return group;
                }
            }
            return null;
        }

        private static bool MatchesPresentationContract(
            MonsterEffectActiveGroup group,
            MonsterEffectActivePresentationBinding binding,
            out string error)
        {
            if (group == null || binding == null || group.PresentationSlots.Count != binding.Slots.Count)
            {
                error = $"Group={binding?.GroupId}, 공간 수가 다릅니다.";
                return false;
            }
            for (var contractIndex = 0; contractIndex < group.PresentationSlots.Count; contractIndex++)
            {
                var contract = group.PresentationSlots[contractIndex];
                MonsterActiveAttackPresentationCueBinding compiled = null;
                for (var slotIndex = 0; slotIndex < binding.Slots.Count; slotIndex++)
                {
                    var candidate = binding.Slots[slotIndex];
                    if (candidate != null && contract != null && string.Equals(
                            candidate.SlotId,
                            contract.SlotId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        compiled = candidate;
                        break;
                    }
                }
                if (contract == null || compiled == null ||
                    compiled.Timing != contract.Timing || compiled.Anchor != contract.Anchor ||
                    compiled.Multiplicity != contract.Multiplicity || compiled.Attachment != contract.Attachment ||
                    compiled.EndPolicy != contract.EndPolicy || compiled.UseDuration != contract.UseDuration ||
                    compiled.UseDuration && !Mathf.Approximately(compiled.Duration, contract.Duration))
                {
                    error = $"Group={group.GroupId}, Slot={contract?.SlotId ?? "비어 있음"}";
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
            MonsterEffectActiveProfile profile,
            MonsterEffectActivePresentationBinding[] groupPresentations,
            int maximumEnergy,
            float commitTime,
            bool isMythic)
        {
            var compatibilityEffect = new MonsterSkillEffect();
            compatibilityEffect.EditorConfigure(
                "assembled_effect",
                MonsterSkillEffectType.EnergyGain,
                MonsterSkillValueSource.Flat,
                0f);
            var compatibilityRecipe = new MonsterSkillRecipe();
            compatibilityRecipe.EditorConfigure(
                MonsterSkillTriggerType.EnergyMax,
                1,
                0f,
                MonsterSkillTargetType.Self,
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
            presentations = groupPresentations ?? Array.Empty<MonsterEffectActivePresentationBinding>();
            commitNormalizedTime = Mathf.Clamp01(commitTime);
            mythicExclusive = isMythic;
        }
#endif
    }
}
