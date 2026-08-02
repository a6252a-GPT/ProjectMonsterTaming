using System.Collections.Generic;
using UnityEngine;

namespace ProjectMT.Contents.CastleRaid
{
    [DisallowMultipleComponent]
    public sealed class AttackSlotProvider : MonoBehaviour // 대상 주변 공격 자리 대여
    {
        [SerializeField] private Transform[] slots; // 겹침 방지 공격 위치

        private readonly Dictionary<Component, Transform> leases = new Dictionary<Component, Transform>(); // 공격자별 대여 자리
        private readonly HashSet<Transform> occupied = new HashSet<Transform>(); // 현재 사용 중 자리

        public bool TryLease(Component owner, Vector3 fromPosition, out Transform slot)
        {
            if (owner != null && leases.TryGetValue(owner, out slot) && slot != null)
            {
                return true;
            }

            slot = null;
            var nearestDistance = float.PositiveInfinity;
            if (owner == null || slots == null)
            {
                return false;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                var candidate = slots[i];
                if (candidate == null || occupied.Contains(candidate))
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

            leases[owner] = slot; // 같은 공격자는 같은 자리 유지
            occupied.Add(slot);
            return true;
        }

        public void Release(Component owner)
        {
            if (owner != null && leases.TryGetValue(owner, out var slot))
            {
                leases.Remove(owner);
                occupied.Remove(slot);
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

#if UNITY_EDITOR
        public void EditorSetSlots(Transform[] attackSlots)
        {
            slots = attackSlots;
        }
#endif
    }
}
