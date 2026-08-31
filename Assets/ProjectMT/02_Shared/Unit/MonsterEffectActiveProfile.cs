using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public enum MonsterEffectActiveRole
    {
        Support,
        Guard,
        Debuff
    }

    [Serializable]
    public sealed class MonsterEffectActiveGroup // 한 시점에 같은 대상 규칙으로 적용하는 효과 묶음
    {
        [SerializeField] private string groupId = "group_01";
        [SerializeField] private string displayName = "효과 1";
        [SerializeField, Min(0f)] private float delayAfterPrevious;
        [SerializeField] private MonsterSkillTargetType target = MonsterSkillTargetType.AllAllies;
        [SerializeField] private bool includeCaster = true;
        [SerializeField, Min(0f)] private float radius = 5f;
        [SerializeField, Range(1, 32)] private int maxTargets = 8;
        [SerializeField] private List<MonsterSkillEffect> effects = new List<MonsterSkillEffect>();
        [SerializeField] private List<MonsterActivePresentationSlot> presentationSlots =
            new List<MonsterActivePresentationSlot>();

        public string GroupId => groupId?.Trim() ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? GroupId : displayName.Trim();
        public float DelayAfterPrevious => Mathf.Max(0f, delayAfterPrevious);
        public MonsterSkillTargetType Target => target;
        public bool IncludeCaster => includeCaster;
        public float Radius => Mathf.Max(0f, radius);
        public int MaxTargets => Mathf.Clamp(maxTargets, 1, 32);
        public IReadOnlyList<MonsterSkillEffect> Effects => effects ??
            (IReadOnlyList<MonsterSkillEffect>)Array.Empty<MonsterSkillEffect>();
        public IReadOnlyList<MonsterActivePresentationSlot> PresentationSlots => presentationSlots ??
            (IReadOnlyList<MonsterActivePresentationSlot>)Array.Empty<MonsterActivePresentationSlot>();

        public bool TryValidate(MonsterEffectActiveRole role, out string error)
        {
            if (!ActiveAttackValue.UsesSafeId(GroupId) || string.IsNullOrWhiteSpace(DisplayName) ||
                !Enum.IsDefined(typeof(MonsterSkillTargetType), target) ||
                !MonsterEffectActiveProfile.IsTargetAllowed(role, target) ||
                float.IsNaN(delayAfterPrevious) || float.IsInfinity(delayAfterPrevious) ||
                delayAfterPrevious < 0f || float.IsNaN(radius) || float.IsInfinity(radius) ||
                radius < 0f || maxTargets < 1 || maxTargets > 32)
            {
                error = $"효과 묶음 설정이 유효하지 않습니다. Group={GroupId}";
                return false;
            }

            if (Effects.Count == 0)
            {
                error = $"효과 묶음에는 효과가 하나 이상 필요합니다. Group={GroupId}";
                return false;
            }

            var effectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < Effects.Count; index++)
            {
                var effect = Effects[index];
                var effectError = "효과가 비어 있습니다.";
                if (effect == null || !effect.TryValidate(out effectError) ||
                    !MonsterEffectActiveProfile.IsEffectAllowed(role, effect.Type) ||
                    !effectIds.Add(effect.EffectId))
                {
                    error = $"효과 {index + 1}이 유효하지 않습니다. Group={GroupId}, Detail={effectError}";
                    return false;
                }

                if (RequiresDuration(effect.Type) && effect.Duration <= 0f)
                {
                    error = $"지속 효과는 0초보다 긴 지속 시간이 필요합니다. Group={GroupId}, Effect={effect.EffectId}";
                    return false;
                }
                if (effect.Type == MonsterSkillEffectType.Heal && effect.Duration > 0f &&
                    effect.RepeatInterval <= 0f)
                {
                    error = $"지속 회복은 0초보다 긴 회복 간격이 필요합니다. Group={GroupId}, Effect={effect.EffectId}";
                    return false;
                }
            }

            var slotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < PresentationSlots.Count; index++)
            {
                var slot = PresentationSlots[index];
                var slotError = "연출 공간이 비어 있습니다.";
                if (slot == null || !slot.TryValidate(out slotError) || !slotIds.Add(slot.SlotId))
                {
                    error = $"VFX/SFX 계약이 유효하지 않습니다. Group={GroupId}, Detail={slotError}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool RequiresDuration(MonsterSkillEffectType type) =>
            type is MonsterSkillEffectType.Shield or MonsterSkillEffectType.AttackBuff or
                MonsterSkillEffectType.DefenseBuff or MonsterSkillEffectType.AttackSpeedBuff or
                MonsterSkillEffectType.AttackDebuff or MonsterSkillEffectType.DefenseDebuff or
                MonsterSkillEffectType.AttackSpeedDebuff or MonsterSkillEffectType.MoveSpeedDebuff or
                MonsterSkillEffectType.Mark or MonsterSkillEffectType.Slow or MonsterSkillEffectType.Stun or
                MonsterSkillEffectType.Pull or MonsterSkillEffectType.Taunt or
                MonsterSkillEffectType.DamageReduction;

        public float EstimateDuration()
        {
            var duration = DelayAfterPrevious;
            for (var index = 0; index < Effects.Count; index++)
            {
                var effect = Effects[index];
                if (effect != null)
                {
                    duration = Mathf.Max(duration, DelayAfterPrevious + effect.Delay);
                }
            }
            return duration;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string title,
            float delay,
            MonsterSkillTargetType targetType,
            bool casterIncluded,
            float effectRadius,
            int targetLimit,
            IEnumerable<MonsterSkillEffect> groupEffects,
            IEnumerable<MonsterActivePresentationSlot> slots = null)
        {
            groupId = id?.Trim();
            displayName = title?.Trim();
            delayAfterPrevious = Mathf.Max(0f, delay);
            target = targetType;
            includeCaster = casterIncluded;
            radius = Mathf.Max(0f, effectRadius);
            maxTargets = Mathf.Clamp(targetLimit, 1, 32);
            effects = groupEffects == null
                ? new List<MonsterSkillEffect>()
                : new List<MonsterSkillEffect>(groupEffects.Where(effect => effect != null));
            presentationSlots = slots == null
                ? new List<MonsterActivePresentationSlot>()
                : new List<MonsterActivePresentationSlot>(slots.Where(slot => slot != null));
        }
#endif
    }

    [CreateAssetMenu(menuName = "ProjectMT/Unit/Monster Skill/Effect Active Profile", fileName = "EAP_Effect")]
    public sealed class MonsterEffectActiveProfile : ScriptableObject // 지원·수호·디버프 액티브 조립 원본
    {
        public const int MaximumGroupCount = 16;
        [SerializeField] private string profileId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private MonsterEffectActiveRole role;
        [SerializeField] private List<MonsterEffectActiveGroup> groups = new List<MonsterEffectActiveGroup>();

        public string ProfileId => profileId?.Trim() ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? ProfileId : displayName.Trim();
        public string Description => description?.Trim() ?? string.Empty;
        public MonsterEffectActiveRole Role => role;
        public IReadOnlyList<MonsterEffectActiveGroup> Groups => groups ??
            (IReadOnlyList<MonsterEffectActiveGroup>)Array.Empty<MonsterEffectActiveGroup>();

        public bool TryValidate(out string error)
        {
            if (!ActiveAttackValue.UsesSafeId(ProfileId) || string.IsNullOrWhiteSpace(DisplayName) ||
                !Enum.IsDefined(typeof(MonsterEffectActiveRole), role))
            {
                error = $"효과형 액티브 프로필 ID·이름·역할이 유효하지 않습니다. Profile={name}";
                return false;
            }
            if (Groups.Count == 0 || Groups.Count > MaximumGroupCount)
            {
                error = $"효과 묶음은 1~{MaximumGroupCount}개여야 합니다. Profile={ProfileId}";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < Groups.Count; index++)
            {
                var group = Groups[index];
                var groupError = "효과 묶음이 비어 있습니다.";
                if (group == null || !group.TryValidate(role, out groupError) || !ids.Add(group.GroupId))
                {
                    error = $"효과 묶음 {index + 1}이 유효하지 않습니다. {groupError}";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private static bool RequiresDuration(MonsterSkillEffectType type) =>
            type is MonsterSkillEffectType.Shield or MonsterSkillEffectType.AttackBuff or
                MonsterSkillEffectType.DefenseBuff or MonsterSkillEffectType.AttackSpeedBuff or
                MonsterSkillEffectType.AttackDebuff or MonsterSkillEffectType.DefenseDebuff or
                MonsterSkillEffectType.AttackSpeedDebuff or MonsterSkillEffectType.MoveSpeedDebuff or
                MonsterSkillEffectType.Mark or MonsterSkillEffectType.Slow or MonsterSkillEffectType.Stun or
                MonsterSkillEffectType.Pull or MonsterSkillEffectType.Taunt or
                MonsterSkillEffectType.DamageReduction;

        public float EstimateDuration()
        {
            var duration = 0f;
            for (var index = 0; index < Groups.Count; index++)
            {
                duration += Groups[index]?.EstimateDuration() ?? 0f;
            }
            return duration;
        }

        public static bool IsTargetAllowed(MonsterEffectActiveRole activeRole, MonsterSkillTargetType targetType)
        {
            return activeRole switch
            {
                MonsterEffectActiveRole.Support => targetType is MonsterSkillTargetType.Self or
                    MonsterSkillTargetType.LowestHealthAlly or MonsterSkillTargetType.HighestAttackAlly or
                    MonsterSkillTargetType.NearbyAllies or MonsterSkillTargetType.AllAllies,
                MonsterEffectActiveRole.Guard => targetType is MonsterSkillTargetType.Self or
                    MonsterSkillTargetType.LowestHealthAlly or MonsterSkillTargetType.HighestAttackAlly or
                    MonsterSkillTargetType.NearbyAllies or MonsterSkillTargetType.AllAllies or
                    MonsterSkillTargetType.CurrentTarget or MonsterSkillTargetType.NearestEnemy or
                    MonsterSkillTargetType.TargetAreaEnemies,
                MonsterEffectActiveRole.Debuff => targetType is MonsterSkillTargetType.CurrentTarget or
                    MonsterSkillTargetType.NearestEnemy or MonsterSkillTargetType.FarthestEnemy or
                    MonsterSkillTargetType.LowestHealthEnemy or MonsterSkillTargetType.HighestAttackEnemy or
                    MonsterSkillTargetType.RangedEnemyFirst or MonsterSkillTargetType.TargetAreaEnemies,
                _ => false
            };
        }

        public static bool IsEffectAllowed(MonsterEffectActiveRole activeRole, MonsterSkillEffectType effectType)
        {
            return activeRole switch
            {
                MonsterEffectActiveRole.Support => effectType is MonsterSkillEffectType.Heal or
                    MonsterSkillEffectType.AttackBuff or MonsterSkillEffectType.AttackSpeedBuff or
                    MonsterSkillEffectType.EnergyGain,
                MonsterEffectActiveRole.Guard => effectType is MonsterSkillEffectType.Shield or
                    MonsterSkillEffectType.DefenseBuff or MonsterSkillEffectType.DamageReduction or
                    MonsterSkillEffectType.Taunt,
                MonsterEffectActiveRole.Debuff => effectType is MonsterSkillEffectType.AttackDebuff or
                    MonsterSkillEffectType.DefenseDebuff or MonsterSkillEffectType.AttackSpeedDebuff or
                    MonsterSkillEffectType.MoveSpeedDebuff or MonsterSkillEffectType.Mark or
                    MonsterSkillEffectType.Slow or MonsterSkillEffectType.Stun or MonsterSkillEffectType.Pull or
                    MonsterSkillEffectType.EnergyDrain,
                _ => false
            };
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string title,
            string body,
            MonsterEffectActiveRole activeRole,
            IEnumerable<MonsterEffectActiveGroup> effectGroups)
        {
            profileId = id?.Trim();
            displayName = title?.Trim();
            description = body?.Trim();
            role = activeRole;
            groups = effectGroups == null
                ? new List<MonsterEffectActiveGroup>()
                : new List<MonsterEffectActiveGroup>(effectGroups.Where(group => group != null));
        }
#endif
    }
}
