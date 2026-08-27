using System;
using System.Collections.Generic;
using ProjectMT.Shared.Audio;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    public enum MonsterBasicAttackVfxEvent
    {
        MotionStart,
        RecipeExecute,
        DeliverySpawn,
        TargetDamaged,
        OutboundTargetDamaged,
        ReturnTargetDamaged,
        AreaResolved,
        SequenceEnd,
        DeliveryTurn,
        DeliveryEnd,
        MotionEnd
    }
    public enum MonsterBasicAttackVfxAnchor { SourceRoot, AttackOrigin, MarkerSocket, ProjectileRoot, TargetRoot, HitPoint, AreaCenter, TrajectoryOrigin }
    public enum MonsterBasicAttackVfxMultiplicity { OncePerMotion, OncePerExecution, PerProjectile, PerTargetHit, PerDamageStage, ContinuousUntilEnd }
    public enum MonsterBasicAttackVfxAssignmentScope { MonsterShared, MotionSpecific }
    public enum MonsterBasicAttackVfxAttachment { World, FollowAnchor, DeliveryVisual }
    public enum MonsterBasicAttackVfxEndPolicy { Timed, DeliveryEnd, MotionEnd, ParticleDuration }
    public enum MonsterBasicAttackVfxAssignmentState { Undecided, Assigned, Disabled }
    public enum MonsterBasicAttackSfxAssignmentState { Undecided, Assigned, Disabled }

    [Serializable]
    public sealed class MonsterBasicAttackVfxSlot // 조립소가 정의하고 Maker가 채우는 빈칸 계약
    {
        [SerializeField] private string slotId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(1, 3)] private string description;
        [SerializeField] private MonsterBasicAttackVfxEvent eventType;
        [SerializeField] private MonsterBasicAttackVfxAnchor anchor;
        [SerializeField] private MonsterBasicAttackVfxMultiplicity multiplicity;
        [SerializeField] private MonsterBasicAttackVfxAssignmentScope assignmentScope;
        [SerializeField] private MonsterBasicAttackVfxAttachment attachment;
        [SerializeField] private MonsterBasicAttackVfxEndPolicy endPolicy;
        [SerializeField, Min(0.01f)] private float defaultLifetime = 1f;

        public string SlotId => slotId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? SlotId : displayName;
        public string Description => description ?? string.Empty;
        public MonsterBasicAttackVfxEvent EventType => eventType;
        public MonsterBasicAttackVfxAnchor Anchor => anchor;
        public MonsterBasicAttackVfxMultiplicity Multiplicity => multiplicity;
        public MonsterBasicAttackVfxAssignmentScope AssignmentScope => assignmentScope;
        public MonsterBasicAttackVfxAttachment Attachment => attachment;
        public MonsterBasicAttackVfxEndPolicy EndPolicy => endPolicy;
        public float DefaultLifetime => Mathf.Max(0.01f, defaultLifetime);
        public bool IsDeliveryVisual => attachment == MonsterBasicAttackVfxAttachment.DeliveryVisual;
        public bool AllowsMonsterTimingOffset => !IsDeliveryVisual;
        public bool AllowsTimingLead => AllowsMonsterTimingOffset &&
                                        eventType == MonsterBasicAttackVfxEvent.RecipeExecute;

        public float ClampTimingOffset(float value)
        {
            if (!AllowsMonsterTimingOffset || float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }
            return AllowsTimingLead ? value : Mathf.Max(0f, value);
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(slotId) || string.IsNullOrWhiteSpace(displayName) ||
                !MonsterBasicAttackVfxResolver.UsesSafeSlotId(slotId))
            {
                error = $"VFX slot ID or name is invalid. Slot={slotId}";
                return false;
            }
            if (!Enum.IsDefined(typeof(MonsterBasicAttackVfxEvent), eventType) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackVfxAnchor), anchor) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackVfxMultiplicity), multiplicity) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackVfxAssignmentScope), assignmentScope) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackVfxAttachment), attachment) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackVfxEndPolicy), endPolicy) || defaultLifetime <= 0f)
            {
                error = $"VFX slot setting is invalid. Slot={slotId}";
                return false;
            }
            if (attachment == MonsterBasicAttackVfxAttachment.DeliveryVisual &&
                (eventType != MonsterBasicAttackVfxEvent.DeliverySpawn ||
                 anchor != MonsterBasicAttackVfxAnchor.ProjectileRoot ||
                 multiplicity != MonsterBasicAttackVfxMultiplicity.PerProjectile ||
                 endPolicy != MonsterBasicAttackVfxEndPolicy.DeliveryEnd))
            {
                error = $"Delivery visual slot contract is invalid. Slot={slotId}";
                return false;
            }
            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigure(string id, string label, string guide,
            MonsterBasicAttackVfxEvent timing, MonsterBasicAttackVfxAnchor anchorPoint,
            MonsterBasicAttackVfxMultiplicity repeat, MonsterBasicAttackVfxAssignmentScope scope,
            MonsterBasicAttackVfxAttachment attach, MonsterBasicAttackVfxEndPolicy end,
            float lifetime = 1f)
        {
            slotId = id?.Trim();
            displayName = label?.Trim();
            description = guide?.Trim();
            eventType = timing;
            anchor = anchorPoint;
            multiplicity = repeat;
            assignmentScope = scope;
            attachment = attach;
            endPolicy = end;
            defaultLifetime = Mathf.Max(0.01f, lifetime);
        }
#endif
    }

    [Serializable]
    public sealed class MonsterBasicAttackVfxBinding // 한 Monster가 한 연출 슬롯에 저장한 VFX·SFX
    {
        [SerializeField] private string attackId;
        [SerializeField] private string slotId;
        [SerializeField] private string motionId;
        [SerializeField] private MonsterBasicAttackVfxAssignmentState state;
        [SerializeField] private GameObject prefab;
        [SerializeField] private MonsterBasicAttackSfxAssignmentState sfxState;
        [SerializeField] private AudioClip sound;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;
        [SerializeField, HideInInspector] private SfxCue sfx;
        [SerializeField, Min(0.01f)] private float lifetime = 1f;
        [SerializeField, Min(0f)] private float playbackOffset;
        [SerializeField, Min(0.01f)] private float playbackSpeed = 1f;
        [SerializeField] private float eventTimingOffset;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField, Min(0.01f)] private float scale = 1f;

        public string AttackId => attackId ?? string.Empty;
        public string SlotId => slotId ?? string.Empty;
        public string MotionId => motionId ?? string.Empty;
        public MonsterBasicAttackVfxAssignmentState State => state;
        public GameObject Prefab => prefab;
        public MonsterBasicAttackSfxAssignmentState SfxState => sfxState;
        public AudioClip Sound => sound;
        public float SoundVolume => Mathf.Clamp01(soundVolume);
        public SfxCue Sfx => sfx;
        public float Lifetime => Mathf.Max(0.01f, lifetime);
        public float PlaybackOffset => Mathf.Max(0f, playbackOffset);
        public float PlaybackSpeed => float.IsNaN(playbackSpeed) || float.IsInfinity(playbackSpeed)
            ? 1f
            : Mathf.Max(0.01f, playbackSpeed);
        public float EventTimingOffset =>
            float.IsNaN(eventTimingOffset) || float.IsInfinity(eventTimingOffset)
                ? 0f
                : eventTimingOffset;
        public Vector3 LocalPosition => localPosition;
        public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);
        public float Scale => Mathf.Max(0.01f, scale);
        public bool IsAssigned => state == MonsterBasicAttackVfxAssignmentState.Assigned && prefab != null;
        public bool HasSound => sfxState == MonsterBasicAttackSfxAssignmentState.Assigned &&
                                (sound != null || sfx != null && sfx.HasPlayableClip);
        public bool HasPresentation => IsAssigned || HasSound;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(attackId) || string.IsNullOrWhiteSpace(slotId) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackVfxAssignmentState), state) ||
                !Enum.IsDefined(typeof(MonsterBasicAttackSfxAssignmentState), sfxState) ||
                 state == MonsterBasicAttackVfxAssignmentState.Assigned &&
                (prefab == null || lifetime <= 0f || playbackOffset < 0f || playbackSpeed <= 0f ||
                 float.IsNaN(playbackSpeed) || float.IsInfinity(playbackSpeed) || scale <= 0f) ||
                sfxState == MonsterBasicAttackSfxAssignmentState.Assigned &&
                sound == null && (sfx == null || !sfx.HasPlayableClip) ||
                soundVolume < 0f || soundVolume > 1f ||
                float.IsNaN(eventTimingOffset) || float.IsInfinity(eventTimingOffset))
            {
                error = $"Basic Attack VFX binding is invalid. Attack={attackId}, Slot={slotId}";
                return false;
            }
            error = null;
            return true;
        }

#if UNITY_EDITOR
        public void EditorConfigureIdentity(string basicAttackId, string basicAttackSlotId, string attackMotionId)
        {
            attackId = basicAttackId?.Trim();
            slotId = basicAttackSlotId?.Trim();
            motionId = attackMotionId?.Trim();
        }

        public void EditorConfigure(string basicAttackId, string basicAttackSlotId, string attackMotionId,
            MonsterBasicAttackVfxAssignmentState assignmentState, GameObject vfxPrefab, float vfxLifetime,
            Vector3 position, Vector3 eulerAngles, float scaleMultiplier, float vfxPlaybackOffset = 0f,
            AudioClip sourceSound = null, SfxCue runtimeSfx = null,
            MonsterBasicAttackSfxAssignmentState sfxAssignmentState =
                MonsterBasicAttackSfxAssignmentState.Undecided,
            float sourceSoundVolume = 1f,
            float vfxEventTimingOffset = 0f,
            float vfxPlaybackSpeed = 1f)
        {
            EditorConfigureIdentity(basicAttackId, basicAttackSlotId, attackMotionId);
            state = assignmentState;
            prefab = vfxPrefab;
            lifetime = Mathf.Max(0.01f, vfxLifetime);
            playbackOffset = Mathf.Max(0f, vfxPlaybackOffset);
            playbackSpeed = float.IsNaN(vfxPlaybackSpeed) || float.IsInfinity(vfxPlaybackSpeed)
                ? 1f
                : Mathf.Max(0.01f, vfxPlaybackSpeed);
            eventTimingOffset = float.IsNaN(vfxEventTimingOffset) ||
                                float.IsInfinity(vfxEventTimingOffset)
                ? 0f
                : vfxEventTimingOffset;
            localPosition = position;
            localEulerAngles = eulerAngles;
            scale = Mathf.Max(0.01f, scaleMultiplier);
            sfxState = sfxAssignmentState;
            sound = sourceSound;
            soundVolume = Mathf.Clamp01(sourceSoundVolume);
            sfx = runtimeSfx;
        }

        public MonsterBasicAttackVfxBinding EditorCloneForRuntime(SfxCue runtimeSfx)
        {
            var result = new MonsterBasicAttackVfxBinding();
            result.EditorConfigure(
                AttackId,
                SlotId,
                MotionId,
                state,
                prefab,
                Lifetime,
                localPosition,
                localEulerAngles,
                Scale,
                PlaybackOffset,
                sound,
                runtimeSfx,
                SfxState,
                SoundVolume,
                EventTimingOffset,
                PlaybackSpeed);
            return result;
        }
#endif
    }

    public static class MonsterBasicAttackVfxResolver // Draft와 Runtime이 같은 선택 규칙을 사용
    {
        public static bool UsesSafeSlotId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!char.IsLetterOrDigit(character) && character != '_' && character != '-') return false;
            }
            return true;
        }

        public static bool TryResolve(IReadOnlyList<MonsterBasicAttackVfxBinding> bindings,
            string attackId, MonsterBasicAttackVfxSlot slot, string motionId,
            out MonsterBasicAttackVfxBinding binding)
        {
            binding = null;
            if (bindings == null || slot == null) return false;
            var requiredMotion = slot.AssignmentScope == MonsterBasicAttackVfxAssignmentScope.MotionSpecific
                ? motionId ?? string.Empty : string.Empty;
            for (var index = bindings.Count - 1; index >= 0; index--)
            {
                var candidate = bindings[index];
                if (candidate == null ||
                    !string.Equals(candidate.AttackId, attackId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(candidate.SlotId, slot.SlotId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(candidate.MotionId, requiredMotion, StringComparison.OrdinalIgnoreCase)) continue;
                binding = candidate;
                return candidate.IsAssigned;
            }
            return false;
        }

        public static bool TryResolvePresentation(
            IReadOnlyList<MonsterBasicAttackVfxBinding> bindings,
            string attackId,
            MonsterBasicAttackVfxSlot slot,
            string motionId,
            out MonsterBasicAttackVfxBinding binding)
        {
            binding = null;
            if (bindings == null || slot == null) return false;
            var requiredMotion = slot.AssignmentScope == MonsterBasicAttackVfxAssignmentScope.MotionSpecific
                ? motionId ?? string.Empty : string.Empty;
            for (var index = bindings.Count - 1; index >= 0; index--)
            {
                var candidate = bindings[index];
                if (candidate == null ||
                    !string.Equals(candidate.AttackId, attackId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(candidate.SlotId, slot.SlotId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(candidate.MotionId, requiredMotion, StringComparison.OrdinalIgnoreCase)) continue;
                binding = candidate;
                return candidate.HasPresentation;
            }
            return false;
        }

        public static bool TryResolveDeliveryVisual(MonsterBasicAttackProfile profile,
            IReadOnlyList<MonsterBasicAttackVfxBinding> bindings, string motionId,
            out MonsterBasicAttackVfxSlot slot, out MonsterBasicAttackVfxBinding binding)
        {
            slot = null;
            binding = null;
            var slots = profile?.VfxSlots;
            if (slots == null) return false;
            for (var index = 0; index < slots.Count; index++)
            {
                var candidate = slots[index];
                if (candidate == null || !candidate.IsDeliveryVisual ||
                    !TryResolve(bindings, profile.AttackId, candidate, motionId, out var resolved)) continue;
                slot = candidate;
                binding = resolved;
                return true;
            }
            return false;
        }
    }
}
