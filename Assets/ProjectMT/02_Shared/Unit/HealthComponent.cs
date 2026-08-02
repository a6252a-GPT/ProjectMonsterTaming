using System;
using ProjectMT.Shared.Combat;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [DisallowMultipleComponent]
    public sealed class HealthComponent : MonoBehaviour // 공용 체력·피해 처리
    {
        private float fixedDamagePerHit; // 콘텐츠 전용 고정 피해

        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0f;

        public event Action<DamageReport> Damaged; // 피해 확정 알림
        public event Action<DamageReport> Died; // 첫 사망 알림

        public void Initialize(float maxHealth, float fixedDamagePerHit = 0f)
        {
            MaxHealth = Mathf.Max(1f, maxHealth);
            CurrentHealth = MaxHealth;
            this.fixedDamagePerHit = Mathf.Max(0f, fixedDamagePerHit);
        }

        public bool ApplyDamage(DamageRequest request)
        {
            if (!IsAlive || request.Amount <= 0f)
            {
                return false;
            }

            var requestedDamage = fixedDamagePerHit > 0f ? fixedDamagePerHit : request.Amount; // 고정 피해 규칙 우선
            var appliedDamage = Mathf.Min(CurrentHealth, requestedDamage);
            CurrentHealth -= appliedDamage;
            var killed = CurrentHealth <= 0f;
            var report = new DamageReport(request, appliedDamage, CurrentHealth, killed);
            Damaged?.Invoke(report); // 사망 피해도 피격으로 알림
            if (killed)
            {
                Died?.Invoke(report);
            }

            return true;
        }
    }
}
