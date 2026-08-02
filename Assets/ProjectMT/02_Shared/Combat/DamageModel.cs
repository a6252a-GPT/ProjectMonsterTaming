using ProjectMT.Shared.Unit;
using UnityEngine;

namespace ProjectMT.Shared.Combat
{
    public readonly struct DamageRequest // 피해 적용 전 요청값
    {
        public DamageRequest(UnitActor source, float amount, Vector3 hitPoint)
        {
            Source = source;
            Amount = amount;
            HitPoint = hitPoint;
        }

        public UnitActor Source { get; } // 공격 주체
        public float Amount { get; } // 요청 피해량
        public Vector3 HitPoint { get; } // 연출 발생 위치
    }

    public readonly struct DamageReport // 피해 적용 후 확정값
    {
        public DamageReport(DamageRequest request, float appliedDamage, float remainingHealth, bool killed)
        {
            Request = request;
            AppliedDamage = appliedDamage;
            RemainingHealth = remainingHealth;
            Killed = killed;
        }

        public DamageRequest Request { get; }
        public float AppliedDamage { get; } // 실제 반영 피해
        public float RemainingHealth { get; } // 남은 체력
        public bool Killed { get; } // 이번 피해 사망 여부
    }
}
