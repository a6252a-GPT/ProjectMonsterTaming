using System.Collections.Generic;
using System;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    [DisallowMultipleComponent]
    public sealed class AttackSlotProvider : MonoBehaviour // 대상 주변 공격 자리 대여
    {
        private const int ComputedSlotCount = 8;

        [SerializeField] private Transform[] slots; // 겹침 방지 공격 위치

        private readonly Dictionary<Component, int> leases = new Dictionary<Component, int>(); // 공격자별 대여 번호
        private readonly HashSet<int> occupied = new HashSet<int>(); // 현재 사용 중 번호
        private bool usesComputedSlots;
        private Vector2 computedFootprint;
        private float computedPadding;

        public bool UsesComputedSlots => usesComputedSlots;
        public int SlotCount => usesComputedSlots ? ComputedSlotCount : slots == null ? 0 : slots.Length;

        public bool TryResolveAvailableSlot(Component owner, Vector3 fromPosition, out Transform slot)
        {
            return TryResolveAvailableSlot(owner, fromPosition, null, out slot);
        }

        public bool TryResolveAvailableSlot(
            Component owner,
            Vector3 fromPosition,
            Predicate<Transform> isUsable,
            out Transform slot)
        {
            slot = null;
            if (usesComputedSlots)
            {
                return false; // 기존 Transform 계약은 고정 Stage만 사용한다
            }

            if (owner != null && leases.TryGetValue(owner, out var leasedIndex) &&
                TryGetTransformSlot(leasedIndex, out slot))
            {
                return isUsable == null || isUsable(slot);
            }

            var nearestDistance = float.PositiveInfinity;
            if (owner == null || slots == null)
            {
                return false;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                var candidate = slots[i];
                if (candidate == null || occupied.Contains(i))
                {
                    continue;
                }

                if (isUsable != null && !isUsable(candidate))
                {
                    continue;
                }

                var distance = (candidate.position - fromPosition).sqrMagnitude; // 가장 가까운 빈 자리 선택
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    slot = candidate;
                }
            }

            if (slot == null)
            {
                return false;
            }

            return true;
        }

        public bool TryResolveAvailablePosition(
            Component owner,
            Vector3 fromPosition,
            Predicate<Vector3> isUsable,
            out int slotIndex,
            out Vector3 slotPosition)
        {
            if (owner != null && leases.TryGetValue(owner, out slotIndex) &&
                TryGetSlotPosition(slotIndex, out slotPosition))
            {
                return isUsable == null || isUsable(slotPosition);
            }

            slotIndex = -1;
            slotPosition = default;
            var nearestDistance = float.PositiveInfinity;
            if (owner == null)
            {
                return false;
            }

            for (var index = 0; index < SlotCount; index++)
            {
                if (occupied.Contains(index) || !TryGetSlotPosition(index, out var candidate))
                {
                    continue;
                }

                if (isUsable != null && !isUsable(candidate))
                {
                    continue;
                }

                var distance = (candidate - fromPosition).sqrMagnitude;
                if (distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = distance;
                slotIndex = index;
                slotPosition = candidate;
            }

            return slotIndex >= 0;
        }

        public bool TryLease(Component owner, Vector3 fromPosition, out Transform slot)
        {
            return TryLease(owner, fromPosition, null, out slot);
        }

        public bool TryLease(
            Component owner,
            Vector3 fromPosition,
            Predicate<Transform> isUsable,
            out Transform slot)
        {
            if (!TryResolveAvailableSlot(owner, fromPosition, isUsable, out slot))
            {
                return false;
            }

            if (leases.ContainsKey(owner))
            {
                return true;
            }

            var slotIndex = Array.IndexOf(slots, slot);
            if (slotIndex < 0)
            {
                return false;
            }

            leases[owner] = slotIndex; // 같은 공격자는 같은 자리 유지
            occupied.Add(slotIndex);
            return true;
        }

        public bool TryLeasePosition(
            Component owner,
            Vector3 fromPosition,
            Predicate<Vector3> isUsable,
            out int slotIndex,
            out Vector3 slotPosition)
        {
            if (!TryResolveAvailablePosition(owner, fromPosition, isUsable, out slotIndex, out slotPosition))
            {
                return false;
            }

            if (leases.ContainsKey(owner))
            {
                return true;
            }

            leases[owner] = slotIndex;
            occupied.Add(slotIndex);
            return true;
        }

        public bool TryGetSlotPosition(int slotIndex, out Vector3 slotPosition)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
            {
                slotPosition = default;
                return false;
            }

            if (!usesComputedSlots)
            {
                if (!TryGetTransformSlot(slotIndex, out var slot))
                {
                    slotPosition = default;
                    return false;
                }

                slotPosition = slot.position;
                return true;
            }

            var halfX = computedFootprint.x * 0.5f + computedPadding;
            var halfZ = computedFootprint.y * 0.5f + computedPadding;
            switch (slotIndex)
            {
                case 0:
                    slotPosition = new Vector3(0f, 0f, halfZ);
                    break;
                case 1:
                    slotPosition = new Vector3(halfX, 0f, 0f);
                    break;
                case 2:
                    slotPosition = new Vector3(0f, 0f, -halfZ);
                    break;
                case 3:
                    slotPosition = new Vector3(-halfX, 0f, 0f);
                    break;
                case 4:
                    slotPosition = new Vector3(halfX, 0f, halfZ);
                    break;
                case 5:
                    slotPosition = new Vector3(halfX, 0f, -halfZ);
                    break;
                case 6:
                    slotPosition = new Vector3(-halfX, 0f, -halfZ);
                    break;
                default:
                    slotPosition = new Vector3(-halfX, 0f, halfZ);
                    break;
            }

            slotPosition = transform.TransformPoint(slotPosition);
            return true;
        }

        public void Release(Component owner)
        {
            if (owner != null && leases.TryGetValue(owner, out var slotIndex))
            {
                leases.Remove(owner);
                occupied.Remove(slotIndex);
            }
        }

        public void ReleaseAll()
        {
            leases.Clear();
            occupied.Clear();
        }

        private void OnDisable()
        {
            ReleaseAll();
        }

        public void ConfigureSlots(Transform[] attackSlots)
        {
            slots = attackSlots ?? Array.Empty<Transform>();
            usesComputedSlots = false;
            computedFootprint = default;
            computedPadding = 0f;
            ReleaseAll();
        }

        public void ConfigureComputedSlots(Vector2 footprint, float padding)
        {
            slots = Array.Empty<Transform>();
            usesComputedSlots = true;
            computedFootprint = Vector2.Max(Vector2.zero, footprint);
            computedPadding = Mathf.Max(0f, padding);
            ReleaseAll();
        }

        private bool TryGetTransformSlot(int slotIndex, out Transform slot)
        {
            slot = slots != null && slotIndex >= 0 && slotIndex < slots.Length ? slots[slotIndex] : null;
            return slot != null;
        }

#if UNITY_EDITOR
        public void EditorSetSlots(Transform[] attackSlots)
        {
            ConfigureSlots(attackSlots);
        }
#endif
    }
}
