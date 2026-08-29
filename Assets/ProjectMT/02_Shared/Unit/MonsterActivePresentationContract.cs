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
        TeleportExit,
        TeleportEnter
    }

    public enum MonsterActivePresentationAnchor
    {
        CasterRoot,
        AttackOrigin,
        TargetPoint
    }

    [Serializable]
    public sealed class MonsterActivePresentationSlot // 한 Step이 요구하는 몬스터별 VFX/SFX 공간
    {
        [SerializeField] private string slotId;
        [SerializeField] private string displayName;
        [SerializeField] private MonsterActivePresentationEvent timing;
        [SerializeField] private MonsterActivePresentationAnchor anchor;
        [SerializeField, TextArea(1, 3)] private string description;

        public string SlotId => slotId?.Trim() ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? SlotId : displayName.Trim();
        public MonsterActivePresentationEvent Timing => timing;
        public MonsterActivePresentationAnchor Anchor => anchor;
        public string Description => description?.Trim() ?? string.Empty;

        public bool TryValidate(out string error)
        {
            if (!ActiveAttackValue.UsesSafeId(SlotId) || string.IsNullOrWhiteSpace(DisplayName) ||
                !Enum.IsDefined(typeof(MonsterActivePresentationEvent), timing) ||
                !Enum.IsDefined(typeof(MonsterActivePresentationAnchor), anchor))
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
            string body = "")
        {
            slotId = id?.Trim();
            displayName = title?.Trim();
            timing = eventTiming;
            anchor = positionAnchor;
            description = body?.Trim();
        }
#endif
    }
}
