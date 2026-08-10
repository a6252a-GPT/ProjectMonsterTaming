using System;
using ProjectMT.Shared.Combat;
using UnityEngine;

namespace ProjectMT.Shared.Unit
{
    [DisallowMultipleComponent]
    // 08.07 안건준 추가 - IDamageable 구현. UnitActor가 없는 정적 구조물(수호자의 탑 방어 건물 등)도
    // 강제 지정 공격 대상으로 쓸 수 있게 한다. 기존 메서드/필드는 변경하지 않았다.
    public sealed class HealthComponent : MonoBehaviour, IDamageable // 공용 체력·피해 처리
    {
        private float fixedDamagePerHit; // 콘텐츠 전용 고정 피해

        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0f;
        Vector3 IDamageable.Position => transform.position; // 08.07 안건준 추가

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
            return ApplyDamage(request, out _);
        }

        // 08.07 안건준 추가 - 실제로 체력에서 깎인 값(사망 시 남은 체력만큼 clamp됨)이 필요한 호출부
        // (예: 건물 피격 숫자 표시)를 위해 out 파라미터로 노출하는 오버로드. 기존 ApplyDamage(request)는
        // 그대로 두고 내부적으로 이 메서드를 호출하도록만 바꿔서 동작 차이가 없다.
        public bool ApplyDamage(DamageRequest request, out float appliedDamage)
        {
            appliedDamage = 0f;
            if (!IsAlive || request.Amount <= 0f)
            {
                return false;
            }

            var requestedDamage = fixedDamagePerHit > 0f ? fixedDamagePerHit : request.Amount; // 고정 피해 규칙 우선
            appliedDamage = Mathf.Min(CurrentHealth, requestedDamage);
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

        // 08.07 안건준 추가 - UnitActor.ForceTarget으로 지정된 강제 공격 대상이 받는 피해 진입점.
        // 08.07 안건준 수정 - 적을 공격할 때(Damaged 이벤트의 report.AppliedDamage)와 동일하게 "실제로
        // 깎인 체력"을 반환해서, 건물 피격 숫자도 같은 방식으로 표시되도록 통일했다.
        float IDamageable.ReceiveDamage(UnitActor source, float amount)
        {
            ApplyDamage(new DamageRequest(source, amount, transform.position + Vector3.up * 0.4f), out var appliedDamage);
            return appliedDamage;
        }

        // 08.07 안건준 추가 - 즉시 체력을 회복한다. (기존에는 회복 수단이 없었음, 콘텐츠 버프 등에서 사용)
        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        }

        // 08.07 안건준 추가 - 최대 체력을 즉시 바꾼다. keepCurrentRatio가 true면 현재 체력 비율을 유지하고,
        // false면 새 최대 체력으로 가득 채운다. (콘텐츠 버프가 걸리거나 풀릴 때 사용)
        public void SetMaxHealth(float newMaxHealth, bool keepCurrentRatio)
        {
            if (!IsAlive)
            {
                return;
            }

            newMaxHealth = Mathf.Max(1f, newMaxHealth);
            var ratio = MaxHealth > 0f ? CurrentHealth / MaxHealth : 1f;
            MaxHealth = newMaxHealth;
            CurrentHealth = keepCurrentRatio ? Mathf.Clamp(MaxHealth * ratio, 1f, MaxHealth) : MaxHealth;
        }
    }
}
