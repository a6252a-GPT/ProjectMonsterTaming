using System;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public enum MonsterActivePresentationEvent
    {
        Telegraph,
        Launch,
        Travel,
        Impact,
        DashExit,
        DashEnter,
        MotionStart,
        DeliverySpawn,
        AreaResolved,
        DeliveryEnd,
        StepEnd,
        EffectApplied,
        EffectExpired
    }

    public enum MonsterActivePresentationAnchor
    {
        CasterRoot,
        AttackOrigin,
        TargetPoint,
        MarkerSocket,
        ProjectileRoot,
        TargetRoot,
        HitPoint,
        AreaCenter,
        TrajectoryOrigin
    }

    public enum MonsterActivePresentationMultiplicity
    {
        OncePerStep,
        OncePerProjectile,
        PerTargetHit,
        PerDamageStage,
        ContinuousUntilEnd
    }

    public enum MonsterActivePresentationAttachment
    {
        World,
        FollowAnchor,
        DeliveryVisual
    }

    public enum MonsterActivePresentationEndPolicy
    {
        Timed,
        DeliveryEnd,
        StepEnd,
        MotionEnd,
        ParticleDuration
    }

    [Serializable]
    public sealed class MonsterActivePresentationSlot // 한 Step이 요구하는 몬스터별 VFX/SFX 공간
    {
        [SerializeField] private string slotId;
        [SerializeField] private string displayName;
        [SerializeField] private MonsterActivePresentationEvent timing;
        [SerializeField] private MonsterActivePresentationAnchor anchor;
        [SerializeField] private MonsterActivePresentationMultiplicity multiplicity;
        [SerializeField] private MonsterActivePresentationAttachment attachment;
        [SerializeField] private MonsterActivePresentationEndPolicy endPolicy;
        [SerializeField, TextArea(1, 3)] private string description;
        [SerializeField] private bool useDuration;
        [SerializeField, Min(0.05f)] private float duration = 1f;

        public string SlotId => slotId?.Trim() ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? SlotId : displayName.Trim();
        public MonsterActivePresentationEvent Timing => timing;
        public MonsterActivePresentationAnchor Anchor => anchor;
        public MonsterActivePresentationMultiplicity Multiplicity => multiplicity;
        public MonsterActivePresentationAttachment Attachment => attachment;
        public MonsterActivePresentationEndPolicy EndPolicy => endPolicy;
        public string Description => description?.Trim() ?? string.Empty;
        public bool UseDuration => useDuration;
        public float Duration => Mathf.Max(0.05f, duration);

        public bool TryValidate(out string error)
        {
            if (!ActiveAttackValue.UsesSafeId(SlotId) || string.IsNullOrWhiteSpace(DisplayName) ||
                !Enum.IsDefined(typeof(MonsterActivePresentationEvent), timing) ||
                !Enum.IsDefined(typeof(MonsterActivePresentationAnchor), anchor) ||
                !Enum.IsDefined(typeof(MonsterActivePresentationMultiplicity), multiplicity) ||
                !Enum.IsDefined(typeof(MonsterActivePresentationAttachment), attachment) ||
                !Enum.IsDefined(typeof(MonsterActivePresentationEndPolicy), endPolicy) ||
                (useDuration && !ActiveAttackValue.IsFinitePositive(duration)))
            {
                error = $"액티브 연출 공간 계약이 유효하지 않습니다. Slot={SlotId}";
                return false;
            }
            error = string.Empty;
            return true;
        }

        public MonsterActivePresentationSlot Clone()
        {
            return (MonsterActivePresentationSlot)MemberwiseClone();
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string title,
            MonsterActivePresentationEvent eventTiming,
            MonsterActivePresentationAnchor positionAnchor,
            string body = "",
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
            displayName = title?.Trim();
            timing = eventTiming;
            anchor = positionAnchor;
            multiplicity = playbackMultiplicity;
            attachment = playbackAttachment;
            endPolicy = playbackEndPolicy;
            description = body?.Trim();
            useDuration = overrideDuration;
            duration = Mathf.Max(0.05f, playbackDuration);
        }
#endif
    }
}
